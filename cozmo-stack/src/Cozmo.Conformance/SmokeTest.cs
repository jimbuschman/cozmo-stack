using System.Globalization;
using System.Net;
using System.Text;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Transport;

namespace Cozmo.Conformance;

/// <summary>
/// <c>connect &lt;robot-ip&gt;</c>: the transport smoke test, as its own entry point so the acceptance runner
/// can call it in process like every other check.
/// </summary>
public static class SmokeTest
{
    /// <summary>
    /// The hardware smoke test: connect, handshake, identity, telemetry, one harmless command, disconnect.
    /// It is the first thing the acceptance campaign runs, because everything below it reads the telemetry
    /// this establishes, and it is the reading the safety check before each movement test compares against.
    /// </summary>
    public static async Task<int> Run(string[] a)
    {
        if (a.Length < 2 || !IPAddress.TryParse(a[1], out var ip)) return 1;
        int port = 5551, seconds = 20; float? head = null; bool led = a.Contains("--led"), headlight = a.Contains("--headlight"), origin = a.Contains("--origin");
        string? log = null;
        for (int i = 2; i < a.Length; i++)
        {
            if (a[i] == "--port") port = int.Parse(a[++i]);
            else if (a[i] == "--seconds") seconds = int.Parse(a[++i]);
            else if (a[i] == "--head") head = float.Parse(a[++i], CultureInfo.InvariantCulture);
            else if (a[i] == "--log") log = a[++i];
        }
        log ??= $"cozmo-frames-{DateTime.Now:yyyyMMdd-HHmmss}.log";
        log = Path.GetFullPath(log);
        using var link = new RobotLink();
        StreamWriter? lw = new StreamWriter(log, false, Encoding.UTF8);
        Console.WriteLine($"frame log: {log}");
        link.Transport.FrameTrace += e =>
        {
            if (lw is null) return;
            lock (lw) lw.WriteLine($"{e.Utc:O} {(e.Outbound ? "TX" : "RX")} {Hex.Dump(e.Raw)}{(e.Error is null ? "" : "  !! " + e.Error)}");
        };
        link.Transport.Warning += w => Console.WriteLine("  warn: " + w);
        link.Transport.Disconnected += r => Console.WriteLine($"  disconnected: {r}");
        link.Message += m => { if (m is not RobotState && m is not AnimationState) Console.WriteLine($"  <- {m}"); };

        Console.WriteLine($"connecting to {ip}:{port} (ConnectionRequest, reliable seq 1) ...");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try { await link.ConnectAsync(ip, port, TimeSpan.FromSeconds(5)); }
        catch (Exception e) { Console.WriteLine($"FAIL connect: {e.Message}"); lw?.Dispose(); Console.WriteLine($"frame log written to: {log}"); return 10; }
        Console.WriteLine($"connected in {sw.ElapsedMilliseconds} ms (ConnectionResponse received)");

        await Task.Delay(300);
        Console.WriteLine($"identity: {(link.Available is null ? "RobotAvailable NOT received" : link.Available.ToString())}");
        Console.WriteLine($"firmware: {(link.Firmware is null ? "FirmwareVersion NOT received" : $"v{link.Firmware.Version} e2r={link.Firmware.EngineToRobotHash} r2e={link.Firmware.RobotToEngineHash} build={link.Firmware.Build}")}");

        Console.WriteLine("handshake: GetManufacturingInfo + SyncTime" + (origin ? " + AbsoluteLocalizationUpdate(PyCozmo default)" : ""));
        link.BeginSession(origin);
        await Task.Delay(1000);
        Console.WriteLine($"mfg: {(link.Manufacturing is null ? "MfgId NOT received" : link.Manufacturing.ToString())}   syncTimeAck={link.SyncTimeAcked}   states so far={link.StateCount}");

        float? headBefore = link.LastState?.HeadAngleRad; float headPeak = headBefore ?? 0f;
        link.State += st => { if (Math.Abs(st.HeadAngleRad) > Math.Abs(headPeak)) headPeak = st.HeadAngleRad; };
        if (head is { } h) { Console.WriteLine($"command: SetHeadAngle {h:F3} rad (action 1)"); link.SetHeadAngle(h); }
        if (led) { Console.WriteLine("command: SetBackpackLightsMiddle blue/green/red solid"); link.SetBackpackLights(LightState.Solid(LightState.Rgb(0, 0, 255)), LightState.Solid(LightState.Rgb(0, 255, 0)), LightState.Solid(LightState.Rgb(255, 0, 0))); }
        if (headlight) { Console.WriteLine("command: SetHeadlight on"); link.SetHeadlight(true); }

        var end = DateTime.UtcNow.AddSeconds(seconds);
        int lastCount = 0; var lastPrint = DateTime.UtcNow;
        while (DateTime.UtcNow < end && link.Transport.State == LinkState.Connected)
        {
            await Task.Delay(250);
            if ((DateTime.UtcNow - lastPrint).TotalSeconds >= 2)
            {
                var c = link.Transport.Connection!;
                Console.WriteLine($"  t+{sw.Elapsed.TotalSeconds,5:F1}s states={link.StateCount} (+{link.StateCount - lastCount}) rate={link.StateRateHz:F1}Hz pending={c.PendingCount} nextOut={c.NextOutSeq} nextIn={c.NextInSeq} dups={c.DuplicateReliableDropped} resends={c.ResendFrames} rtt={c.LastPingRoundTripMs:F1}ms pings={c.PingRepliesSeen}  {(link.LastState is null ? "" : $"head={link.LastState.HeadAngleRad:F3} batt={link.LastState.BatteryVoltage:F2}V status=0x{link.LastState.Status:x}")}");
                lastCount = link.StateCount; lastPrint = DateTime.UtcNow;
            }
        }
        if (head is { } h2) { Console.WriteLine("command: SetHeadAngle back to 0"); link.SetHeadAngle(0f, actionId: 2); await Task.Delay(1500); }
        if (led) { link.SetBackpackLights(LightState.Off, LightState.Off, LightState.Off); }
        if (headlight) link.SetHeadlight(false);
        await Task.Delay(200);

        var conn = link.Transport.Connection!;
        Console.WriteLine("---- result ----");
        Console.WriteLine($"connected: {link.Transport.State == LinkState.Connected}");
        Console.WriteLine($"identity received: available={link.Available is not null} firmware={link.Firmware is not null} mfg={link.Manufacturing is not null}");
        Console.WriteLine($"telemetry: {link.StateCount} RobotState at {link.StateRateHz:F1} Hz; {link.MessageCount} messages total; histogram: {string.Join(", ", link.HistogramSnapshot().OrderByDescending(k => k.Value).Select(k => $"{k.Key}={k.Value}"))}");
        Console.WriteLine($"transport: frames sent={conn.FramesSent} resent={conn.ResendFrames} dupsDropped={conn.DuplicateReliableDropped} pending={conn.PendingCount} lastRTT={conn.LastPingRoundTripMs:F1}ms");
        if (head is { } h3) Console.WriteLine($"head angle before={headBefore?.ToString("F3") ?? "n/a"} peak during run={headPeak:F3} (target {h3:F3}) final={link.LastState?.HeadAngleRad:F3}; MotorActionAck count={link.CountOf(RobotMessageId.MotorActionAck)}");
        bool pass = link.Transport.State == LinkState.Connected && link.Available is not null && link.Manufacturing is not null && link.StateCount > 20 && conn.PendingCount < 8;
        Console.WriteLine(pass ? "SMOKE TEST: PASS" : "SMOKE TEST: FAIL");
        link.Disconnect();
        lw?.Dispose();
        Console.WriteLine($"frame log written to: {log}  (send this file plus the console output)");
        return pass ? 0 : 20;
    }
}
