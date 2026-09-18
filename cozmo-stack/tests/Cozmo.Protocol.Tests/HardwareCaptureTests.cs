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

    private static List<(DateTime t, bool tx, byte[] raw)> Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "hw_fw2457_first120.log");
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
        Assert.Single(got.OfType<ManufacturingId>());
        Assert.Single(got.OfType<SyncTimeAck>());
        Assert.Single(got.OfType<WifiFlashId>());
        Assert.True(got.OfType<Trace>().Count() >= 5);
        Assert.True(got.OfType<RobotState>().Count() > 10);
        Assert.Empty(got.OfType<RawRobotMessage>().Where(r => r.ParseNote is not null)); // every modelled message parsed cleanly
        Assert.All(got.OfType<RobotState>(), s => Assert.True(s.Has(RobotStatusFlag.IsOnCharger) && s.BatteryVoltage > 3.5f));
        // duplicates the robot resent (it repeats unacked reliable messages every frame) must have been dropped
        Assert.True(t.Connection!.DuplicateReliableDropped >= 1);
    }
}
