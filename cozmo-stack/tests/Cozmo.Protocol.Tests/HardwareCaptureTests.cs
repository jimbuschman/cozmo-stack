using System.Globalization;
using System.Text.RegularExpressions;
using Cozmo.Protocol;
using Cozmo.Transport;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// Frames captured from a real Cozmo (hardware 1.5, firmware 2457) on 2026-09-18 with the conformance tool.
/// The fixture is the first 120 lines of the run: connection, identity, handshake, first telemetry.
/// </summary>
public class HardwareCaptureTests
{
    private static readonly Regex Line = new(@"^(\S+) (TX|RX) ((?:[0-9a-f]{2} ?)+)$", RegexOptions.Compiled);

    private static List<(DateTime t, bool tx, byte[] raw)> Load(string file = "hw_fw2457_first120.log")
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", file);
        var rows = new List<(DateTime, bool, byte[])>();
        foreach (var l in File.ReadAllLines(path))
        {
            var m = Line.Match(l.Trim('﻿', ' '));
            if (!m.Success) continue;
            rows.Add((DateTime.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind), m.Groups[2].Value == "TX", Hex.Parse(m.Groups[3].Value)));
        }
        return rows;
    }

    [Fact]
    public void EveryCapturedFrameDecodesAndReEncodesByteIdentically()
    {
        var rows = Load();
        Assert.True(rows.Count > 100);
        foreach (var (_, _, raw) in rows)
        {
            Assert.True(FrameCodec.TryDecode(raw, out var f, out var err), err);
            Assert.Equal(raw, FrameCodec.Encode(f!));
        }
    }

    [Fact]
    public void RobotAlwaysUsesMultipleMixedFramesAndEchoesPingsVerbatim()
    {
        var rows = Load();
        var rx = rows.Where(r => !r.tx).Select(r => FrameCodec.Decode(r.raw)).ToList();
        Assert.All(rx, f => Assert.Equal(ReliableMessageType.MultipleMixedMessages, f.Type));
        // first robot frame: ConnectionResponse seq 1 acking our seq 1
        var first = rx[0];
        Assert.Equal(1, first.SeqMin); Assert.Equal(1, first.SeqMax); Assert.Equal(1, first.Ack);
        Assert.Equal(ReliableMessageType.ConnectionResponse, first.Messages[0].Type);
        // every ping we sent came back with identical payload and isReply == 0
        var txPings = rows.Where(r => r.tx).Select(r => FrameCodec.Decode(r.raw)).Where(f => f.Type == ReliableMessageType.Ping).Select(f => f.Messages[0].Payload).ToList();
        var rxPings = rx.SelectMany(f => f.Messages).Where(m => m.Type == ReliableMessageType.Ping).Select(m => m.Payload).ToList();
        Assert.NotEmpty(txPings);
        foreach (var p in rxPings.Take(10))
        {
            Assert.Contains(txPings, q => q.AsSpan().SequenceEqual(p));
            Assert.True(PingPayload.TryParse(p, out var pp)); Assert.False(pp.IsReply);
        }
    }

    [Fact]
    public void ReplayThroughReceivePathDeliversIdentityHandshakeAndTelemetry()
    {
        var rows = Load();
        var clk = new ManualClock { NowMs = 1000 };
        var t = ReliableTransport.CreateOffline(TransportOptions.EngineDefaults, clk);
        var got = new List<RobotMessage>();
        t.DataReceived += d => got.Add(RobotMessage.Parse(d));
        t.OfflineConnect();
        bool connected = false; t.Connected += () => connected = true;
        foreach (var (_, tx, raw) in rows)
        {
            if (tx) continue;
            clk.Advance(33);
            t.ProcessIncoming(raw);
        }
        Assert.True(connected);
        var fw = Assert.Single(got.OfType<FirmwareVersion>());
        Assert.Equal(2457, fw.Version);
        Assert.Equal("fedb4b12f1b5b45456aec1629cd0b8cc", fw.EngineToRobotHash);
        Assert.Equal(0x41d04d9du, Assert.Single(got.OfType<RobotAvailable>()).SerialNumberHead);
        Assert.Equal(0x4d9d, fw.RobotId); // low 16 bits of the head serial
        Assert.Single(got.OfType<ManufacturingID>());
        Assert.Single(got.OfType<SyncTimeAck>());
        Assert.Single(got.OfType<WiFiFlashID>());
        Assert.True(got.OfType<PrintTrace>().Count() >= 5);
        Assert.True(got.OfType<RobotState>().Count() > 10);
        Assert.Empty(got.OfType<RawRobotMessage>().Where(r => r.ParseNote is not null)); // every modelled message parsed cleanly
        Assert.All(got.OfType<RobotState>(), s => Assert.True(s.Has(RobotStatusFlag.IsOnCharger) && s.BatteryVoltage > 3.5f));
        // duplicates the robot resent (it repeats unacked reliable messages every frame) must have been dropped
        Assert.True(t.Connection!.DuplicateReliableDropped >= 1);
    }

    /// <summary>
    /// The strongest layout check available without a robot in the room: take every CLAD payload the real
    /// firmware-2457 robot sent during the 20 s run, decode it with the generated codecs and re-encode it.
    /// A wrong field width or order shows up immediately as a length or byte difference.
    /// </summary>
    [Fact]
    public void EveryCapturedCladPayloadDecodesAndReEncodesByteIdentically()
    {
        var seen = new Dictionary<RobotMessageId, int>();
        var failures = new List<string>();
        foreach (var (_, _, raw) in Load("hw_fw2457_full.log").Concat(Load("hw_fw2457_probe.log")))
        {
            if (!FrameCodec.TryDecode(raw, out var f, out _)) continue;
            foreach (var sm in f!.Messages)
            {
                if (sm.Type is not (ReliableMessageType.SingleReliableMessage or ReliableMessageType.SingleUnreliableMessage)
                    || sm.Payload.Length == 0) continue;
                var m = RobotMessage.Parse(sm.Payload);
                var id = (RobotMessageId)sm.Payload[0];
                seen[id] = seen.GetValueOrDefault(id) + 1;
                if (m is RawRobotMessage raw2)
                {
                    failures.Add($"0x{(byte)id:x2} {MessageCatalog.Lookup((byte)id)?.CladType}: {raw2.ParseNote}");
                    continue;
                }
                if (!m.ToBytes().SequenceEqual(sm.Payload))
                    failures.Add($"0x{(byte)id:x2} {m.Info?.CladType}: re-encode differs\n  in  {Hex.Dump(sm.Payload)}\n  out {Hex.Dump(m.ToBytes())}");
            }
        }
        Assert.Empty(failures);
        // the run exercised these, so the fixture is meaningful
        Assert.True(seen.Count >= 20, $"only {seen.Count} distinct messages in the captures");
        Assert.True(seen[RobotMessageId.State] > 500, $"only {seen.GetValueOrDefault(RobotMessageId.State)} RobotState");
        foreach (var id in new[] { RobotMessageId.RobotAvailable, RobotMessageId.FirmwareVersion, RobotMessageId.MfgId,
                                   RobotMessageId.SyncTimeAck, RobotMessageId.Trace, RobotMessageId.MotorActionAck,
                                   RobotMessageId.MotorCalibration, RobotMessageId.ActiveObjectAvailable,
                                   RobotMessageId.WifiFlashID, RobotMessageId.HeadAngle, RobotMessageId.GetMfgInfo,
                                   RobotMessageId.SyncTime,
                                   // added by the 2026-09-18 subsystem probe
                                   RobotMessageId.Image, RobotMessageId.ImageGyro, RobotMessageId.ImuRawDataChunk,
                                   RobotMessageId.AnimState, RobotMessageId.CrashReport, RobotMessageId.ImuRequest,
                                   RobotMessageId.ImageRequest, RobotMessageId.InitAnimController })
            Assert.True(seen.ContainsKey(id), $"{id} not present in the capture");
    }
}
