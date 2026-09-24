using System.Net;
using System.Text;
using System.Text.Json;
using Cozmo.Protocol;
using Cozmo.Transport;

namespace Cozmo.Conformance;

/// <summary>
/// Subsystem-by-subsystem protocol verification against a real robot.
///
/// Only messages the catalog marks <see cref="ProbeSafety.ReadOnly"/> or <see cref="ProbeSafety.SafeVisible"/> are
/// sent by default; <see cref="ProbeSafety.Motion"/> needs --include-motion and
/// <see cref="ProbeSafety.StateChange"/> needs --include-state. Destructive messages (NV writes, OTA, recovery,
/// factory test, shutdown, Wi-Fi off, body radio, DTM) are never sent by this tool.
///
/// For every message the robot sends back, the payload is decoded with the generated codec and re-encoded; a
/// byte-identical result promotes that message to "hardware verified" in the results file.
/// </summary>
public static class Probe
{
    private sealed record Step(string Name, Subsystem Subsystem, ProbeSafety Safety, string What,
                               Func<RobotLink, Task> Run, RobotMessageId[] Expect);

    private static async Task Idle(int ms) => await Task.Delay(ms);

    private static List<Step> BuildSteps() => new()
    {
        new("identity", Subsystem.IdentityVersionLogging, ProbeSafety.ReadOnly,
            "GetManufacturingInfo; identity arrives unprompted on connect",
            async l => { l.Transport.Send(new GetManufacturingInfo(), flush: true); await Idle(700); },
            new[] { RobotMessageId.RobotAvailable, RobotMessageId.FirmwareVersion, RobotMessageId.MfgId,
                    RobotMessageId.WifiFlashID, RobotMessageId.Trace }),

        new("logging", Subsystem.IdentityVersionLogging, ProbeSafety.ReadOnly,
            "RequestCrashReports (read-only; robot answers only if it has stored reports)",
            async l => { l.Transport.Send(new RequestCrashReports(), flush: true); await Idle(700); },
            new[] { RobotMessageId.CrashReport }),

        new("state", Subsystem.RobotStateSensors, ProbeSafety.ReadOnly,
            "SyncTime starts the RobotState stream",
            async l => { l.Transport.Send(new SyncTime(0), flush: true); await Idle(1500); },
            new[] { RobotMessageId.SyncTimeAck, RobotMessageId.State }),

        new("imu", Subsystem.RobotStateSensors, ProbeSafety.ReadOnly,
            "IMURequest: ask for a burst of IMU samples",
            async l => { l.Transport.Send(new IMURequest { LengthMs = 1000 }, flush: true); await Idle(1500); },
            new[] { RobotMessageId.ImuDataChunk, RobotMessageId.ImuRawDataChunk, RobotMessageId.ImuTemperature }),

        new("leds", Subsystem.LedsDisplay, ProbeSafety.SafeVisible,
            "backpack LEDs red/green/blue then off, IR headlight on then off",
            async l =>
            {
                l.SetBackpackLights(LightState.Solid(LightState.Rgb(255, 0, 0)),
                                    LightState.Solid(LightState.Rgb(0, 255, 0)),
                                    LightState.Solid(LightState.Rgb(0, 0, 255)));
                l.Transport.Send(new BackpackLightsTurnSignals(), flush: true);
                await Idle(1200);
                l.SetHeadlight(true); await Idle(600); l.SetHeadlight(false);
                l.SetBackpackLights(LightState.Off, LightState.Off, LightState.Off);
                await Idle(400);
            },
            Array.Empty<RobotMessageId>()),

        new("camera", Subsystem.Camera, ProbeSafety.ReadOnly,
            "ImageRequest single shot (grayscale)",
            async l =>
            {
                l.Transport.Send(new EnableColorImages { Enable = false }, flush: true);
                l.Transport.Send(new ImageRequest { Mode = ImageSendMode.SingleShot }, flush: true);
                await Idle(2000);
                l.Transport.Send(new ImageRequest { Mode = ImageSendMode.Off }, flush: true);
                await Idle(300);
            },
            new[] { RobotMessageId.Image, RobotMessageId.ImageGyro, RobotMessageId.DefaultCameraParams }),

        new("cubes", Subsystem.CubesBle, ProbeSafety.SafeVisible,
            "enable accessory discovery and listen for cube advertisements",
            async l => { l.Transport.Send(new SetAccessoryDiscovery { Enable = true }, flush: true); await Idle(3000); },
            new[] { RobotMessageId.ActiveObjectAvailable }),

        new("animation", Subsystem.Animation, ProbeSafety.StateChange,
            "InitController then read AnimationState",
            async l => { l.Transport.Send(new InitController(), flush: true); await Idle(1200); },
            new[] { RobotMessageId.AnimState }),

        new("motors", Subsystem.Motors, ProbeSafety.Motion,
            "head to +0.3 rad and back, lift small move, wheels stop",
            async l =>
            {
                l.SetHeadAngle(0.3f, actionId: 11); await Idle(1500);
                l.Transport.Send(new SetLiftHeight(45f) { ActionId = 12 }, flush: true); await Idle(1500);
                l.SetHeadAngle(0.0f, actionId: 13); await Idle(1500);
                l.Transport.Send(new StopAllMotors(), flush: true); await Idle(300);
            },
            new[] { RobotMessageId.MotorActionAck, RobotMessageId.MotorCalibration }),

        new("localization", Subsystem.LocalizationNavigation, ProbeSafety.StateChange,
            "AbsoluteLocalizationUpdate (sets the pose origin, no movement)",
            async l =>
            {
                l.Transport.Send(new AbsoluteLocalizationUpdate { PoseOriginId = 1, PoseAngleRad = -0.0f }, flush: true);
                await Idle(1200);
            },
            new[] { RobotMessageId.State }),
    };

    public static async Task<int> Run(string[] a)
    {
        if (a.Length < 2 || !IPAddress.TryParse(a[1], out var ip))
        {
            Console.WriteLine("usage: probe <robot-ip> [--simulated] [--include-motion] [--include-state] " +
                              "[--only <subsystem>] [--out results.json] [--log frames.log]");
            return 1;
        }
        // fidelity: M1-001
        if (a.Contains("--port")) { Console.WriteLine("--port is gone (M1-001, B3): the remote port is 5551, or 5552 with --simulated"); return 1; }
        bool simulated = a.Contains("--simulated");   // B3: 5552 when simulated, else 5551
        bool motion = a.Contains("--include-motion"), state = a.Contains("--include-state");
        string? only = null, outPath = "probe-results.json", log = null;
        for (int i = 2; i < a.Length; i++)
        {
            if (a[i] == "--only") only = a[++i];
            else if (a[i] == "--out") outPath = a[++i];
            else if (a[i] == "--log") log = a[++i];
        }
        log ??= $"cozmo-probe-{DateTime.Now:yyyyMMdd-HHmmss}.log";
        log = Path.GetFullPath(log);
        outPath = Path.GetFullPath(outPath);

        using var link = new RobotLink();
        using var lw = new StreamWriter(log, false, Encoding.UTF8);
        link.Transport.FrameTrace += e => { lock (lw) lw.WriteLine($"{e.Utc:O} {(e.Outbound ? "TX" : "RX")} {Hex.Dump(e.Raw)}"); };
        link.Transport.Warning += w => Console.WriteLine("  warn: " + w);

        // Every payload the robot sends is decoded and re-encoded; mismatches are the interesting result.
        // These four are written on the transport's dispatch thread and read by the probe loop below, so
        // every access goes through `gate`.
        var gate = new object();
        var seen = new Dictionary<RobotMessageId, int>();
        var okBytes = new Dictionary<RobotMessageId, int>();
        var problems = new Dictionary<RobotMessageId, string>();
        var samples = new Dictionary<RobotMessageId, string>();
        link.Transport.DataReceived += payload =>
        {
            var id = (RobotMessageId)payload[0];
            lock (gate)
            {
                seen[id] = seen.GetValueOrDefault(id) + 1;
                if (!samples.ContainsKey(id)) samples[id] = Hex.Dump(payload.AsSpan(1), 64);
                var m = RobotMessage.Parse(payload);
                if (m is RawRobotMessage raw) problems.TryAdd(id, raw.ParseNote ?? "undecodable");
                else if (!m.ToBytes().SequenceEqual(payload)) problems.TryAdd(id, "re-encode differs");
                else okBytes[id] = okBytes.GetValueOrDefault(id) + 1;
            }
        };

        Console.WriteLine($"probe: connecting to {ip}:{RobotAddress.RemotePort(simulated)}   frame log: {log}");
        try { await link.ConnectAsync(ip, simulated, TimeSpan.FromSeconds(5)); }
        catch (Exception e) { Console.WriteLine($"FAIL connect: {e.Message}"); return 10; }
        Console.WriteLine("connected; running probes (safe messages only unless --include-motion/--include-state)\n");

        var results = new List<object>();
        foreach (var step in BuildSteps())
        {
            if (only is not null && !step.Name.Equals(only, StringComparison.OrdinalIgnoreCase)) continue;
            if (step.Safety == ProbeSafety.Motion && !motion) { Console.WriteLine($"[skip] {step.Name}: needs --include-motion"); continue; }
            if (step.Safety == ProbeSafety.StateChange && !state) { Console.WriteLine($"[skip] {step.Name}: needs --include-state"); continue; }

            Dictionary<RobotMessageId, int> before;
            lock (gate) before = new Dictionary<RobotMessageId, int>(seen);
            Console.WriteLine($"[{step.Name}] {step.Subsystem} ({step.Safety}): {step.What}");
            try { await step.Run(link); }
            catch (Exception e) { Console.WriteLine($"   !! send failed: {e.Message}"); }

            List<RobotMessageId> got;
            Dictionary<RobotMessageId, string> problemsNow;
            lock (gate)
            {
                got = seen.Where(kv => kv.Value > before.GetValueOrDefault(kv.Key)).Select(kv => kv.Key).ToList();
                problemsNow = new Dictionary<RobotMessageId, string>(problems);
            }
            foreach (var id in step.Expect)
            {
                bool arrived = got.Contains(id);
                string verdict = !arrived ? "not observed"
                    : problemsNow.ContainsKey(id) ? $"MISMATCH ({problemsNow[id]})"
                    : "hardware verified";
                Console.WriteLine($"   {(arrived && !problemsNow.ContainsKey(id) ? "ok " : "-- ")}0x{(byte)id:x2} {MessageCatalog.Lookup((byte)id)?.CladType,-26} {verdict}");
            }
            foreach (var id in got.Except(step.Expect))
                Console.WriteLine($"   +  0x{(byte)id:x2} {MessageCatalog.Lookup((byte)id)?.CladType,-26} also seen");
            results.Add(new
            {
                step = step.Name,
                subsystem = step.Subsystem.ToString(),
                safety = step.Safety.ToString(),
                expected = step.Expect.Select(i => $"0x{(byte)i:X2}").ToArray(),
                observed = got.Select(i => $"0x{(byte)i:X2}").ToArray(),
            });
            Console.WriteLine();
        }

        link.Disconnect();
        await Task.Delay(300);

        // the robot has stopped talking by now, but snapshot anyway rather than rely on that
        Dictionary<RobotMessageId, int> seenFinal, okFinal;
        Dictionary<RobotMessageId, string> problemsFinal, samplesFinal;
        lock (gate)
        {
            seenFinal = new Dictionary<RobotMessageId, int>(seen);
            okFinal = new Dictionary<RobotMessageId, int>(okBytes);
            problemsFinal = new Dictionary<RobotMessageId, string>(problems);
            samplesFinal = new Dictionary<RobotMessageId, string>(samples);
        }
        var perMessage = seenFinal.Keys.OrderBy(k => (byte)k).Select(id => new
        {
            tag = $"0x{(byte)id:X2}",
            cladType = MessageCatalog.Lookup((byte)id)?.CladType,
            subsystem = MessageCatalog.Lookup((byte)id)?.Subsystem.ToString(),
            count = seenFinal[id],
            byteIdenticalRoundTrips = okFinal.GetValueOrDefault(id),
            status = problemsFinal.ContainsKey(id) ? "capture_conflict" : "hardware_verified",
            problem = problemsFinal.GetValueOrDefault(id),
            sample = samplesFinal[id],
        }).ToList();

        var doc = new
        {
            utc = DateTime.UtcNow,
            robot = new
            {
                serial = link.Available is null ? null : $"0x{link.Available.SerialNumberHead:x8}",
                hardware = link.Available?.HwVersion,
                firmware = link.Firmware?.Version,
                engineToRobotHash = link.Firmware?.EngineToRobotHash,
                robotToEngineHash = link.Firmware?.RobotToEngineHash,
                build = link.Firmware?.Build,
                bodyHw = link.Manufacturing?.BodyHwVersion,
                bodyColor = link.Manufacturing?.BodyColor,
            },
            steps = results,
            messages = perMessage,
        };
        File.WriteAllText(outPath, JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true }));

        Console.WriteLine("---- summary ----");
        Console.WriteLine($"robot: serial {doc.robot.serial} hw {doc.robot.hardware} firmware {doc.robot.firmware} " +
                          $"({doc.robot.build}) hashes {doc.robot.engineToRobotHash}/{doc.robot.robotToEngineHash}");
        Console.WriteLine($"distinct robot->engine messages observed: {perMessage.Count}");
        Console.WriteLine($"byte-identical round trips: {perMessage.Count(m => m.status == "hardware_verified")}, " +
                          $"conflicts: {perMessage.Count(m => m.status == "capture_conflict")}");
        foreach (var m in perMessage.Where(m => m.status == "capture_conflict"))
            Console.WriteLine($"  CONFLICT {m.tag} {m.cladType}: {m.problem}\n    sample {m.sample}");
        Console.WriteLine($"results: {outPath}\nframe log: {log}");
        return perMessage.Any(m => m.status == "capture_conflict") ? 20 : 0;
    }
}
