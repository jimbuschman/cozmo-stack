using System.Globalization;
using System.Net;
using System.Text;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Transport;

namespace Cozmo.Conformance;

/// <summary>
/// Hardware acceptance for the M4 control layer: one command per capability group.
///
/// Each reports what the robot itself said, rather than what was sent. Where the robot reports nothing, as
/// with the LEDs, that is stated and left to a person to confirm.
/// </summary>
public static class Control
{
    private static (IPAddress ip, int port)? Target(string[] a)
    {
        if (a.Length < 2 || !IPAddress.TryParse(a[1], out var ip)) return null;
        int port = 5551;
        for (int i = 2; i < a.Length - 1; i++) if (a[i] == "--port") port = int.Parse(a[i + 1]);
        return (ip, port);
    }

    private static double Seconds(string[] a, string flag, double fallback)
    {
        for (int i = 2; i < a.Length - 1; i++)
            if (a[i] == flag) return double.Parse(a[i + 1], CultureInfo.InvariantCulture);
        return fallback;
    }

    /// <summary>Connects, waits until the robot is ready to be driven, and reports why if it is not.</summary>
    private static async Task<(CozmoRobot robot, StreamWriter log, string logPath)?> ReadyRobot(
        IPAddress ip, int port, string prefix)
    {
        var logPath = Path.GetFullPath($"cozmo-{prefix}-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        var log = new StreamWriter(logPath, false, Encoding.UTF8);
        Console.WriteLine($"frame log: {logPath}");

        CozmoRobot robot;
        try { robot = await CozmoRobot.ConnectAsync(ip, port); }
        catch (Exception e) { Console.WriteLine($"FAIL connect: {e.Message}"); log.Dispose(); return null; }

        robot.Transport.FrameTrace += e =>
        {
            lock (log) log.WriteLine($"{e.Utc:O} {(e.Outbound ? "TX" : "RX")} {Hex.Dump(e.Raw)}");
        };
        robot.Transport.Disconnected += r => Console.WriteLine($"  disconnected: {r}");

        Console.WriteLine($"connected: firmware v{robot.State.FirmwareVersionNumber?.ToString() ?? "?"} " +
                          $"serial 0x{robot.State.SerialNumber:x8}");
        var (ready, why) = await robot.WaitUntilReadyAsync(TimeSpan.FromSeconds(20), describe: true);
        Console.WriteLine(ready ? $"ready: {why}" : $"NOT READY: {why}");
        if (!ready)
        {
            Console.WriteLine("Refusing to drive a robot that has not finished calibrating.");
            robot.Disconnect(); log.Dispose();
            return null;
        }
        return (robot, log, logPath);
    }

    private static string Acceptance(string device, bool pass, string humanCheck, object detail, CozmoRobot robot,
                                     string? path = null)
    {
        path = Path.GetFullPath(string.IsNullOrEmpty(path)
            ? $"cozmo-acceptance-{device}-{DateTime.Now:yyyyMMdd-HHmmss}.json" : path);
        var doc = new
        {
            utc = DateTime.UtcNow,
            device,
            automatedChecksPassed = pass,
            humanCheckRequired = humanCheck,
            humanVerdict = "not recorded: set to pass or fail after watching the robot",
            robot = new
            {
                serial = robot.State.SerialNumber is { } sn ? $"0x{sn:x8}" : null,
                firmware = robot.State.FirmwareVersionNumber,
                batteryVolts = robot.Sensors.BatteryVolts,
                onCharger = robot.Sensors.OnCharger,
            },
            detail,
            handlerFaults = robot.Transport.HandlerFaults,
        };
        File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(doc,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        return path;
    }

    private static void Verdict(string device, bool pass, string humanCheck, string record)
    {
        Console.WriteLine();
        Console.WriteLine(pass
            ? $"AUTOMATED CHECKS PASSED ({device}): the robot confirmed everything measurable from here."
            : $"AUTOMATED CHECKS FAILED ({device}).");
        if (humanCheck.Length > 0) Console.WriteLine($"HUMAN CHECK REQUIRED: {humanCheck}");
        Console.WriteLine($"acceptance record: {record}");
    }

    private static string? AcceptancePath(string[] a)
    {
        for (int i = 2; i < a.Length; i++)
        {
            if (a[i] != "--acceptance") continue;
            return i + 1 < a.Length && !a[i + 1].StartsWith("--") ? a[i + 1] : "";
        }
        return "";
    }

    // ------------------------------------------------------------------- motion

    /// <summary>
    /// Head, lift and optionally the wheels. Head and lift are confirmed by the robot's own action
    /// acknowledgement; wheels are confirmed by the speeds it reports.
    /// </summary>
    public static async Task<int> Drive(string[] a)
    {
        if (Target(a) is not var (ip, port)) return 1;
        bool allowDrive = a.Contains("--allow-drive");
        double driveSeconds = Seconds(a, "--drive-seconds", 1.0);
        float speed = (float)Seconds(a, "--speed", 40);

        if (!allowDrive)
            Console.WriteLine("Wheels will NOT be driven. Pass --allow-drive once the robot has clear space around it.");

        var got = await ReadyRobot(ip, port, "drive");
        if (got is not var (robot, log, _)) return 10;
        using var _r = robot;

        var results = new List<object>();
        bool pass = true;
        try
        {
            robot.Sensors.SetStopOnCliff(true);
            Console.WriteLine("stop-on-cliff enabled");

            float start = robot.Sensors.HeadAngleRad ?? 0;
            foreach (var angle in new[] { 0.3f, -0.2f, start })
            {
                var r = await robot.Motion.SetHeadAngleAsync(angle);
                Console.WriteLine($"  head -> {angle:F2} rad: {r}");
                results.Add(new { step = "head", target = angle, result = r.Result.ToString(), detail = r.Detail });
                pass &= r.Ok;
                await Task.Delay(400);
            }

            foreach (var h in new[] { 60f, CozmoMotion.MinLiftHeightMm })
            {
                var r = await robot.Motion.SetLiftHeightAsync(h);
                Console.WriteLine($"  lift -> {h:F0} mm: {r}");
                results.Add(new { step = "lift", target = h, result = r.Result.ToString(), detail = r.Detail });
                pass &= r.Ok;
                await Task.Delay(600);
            }

            if (allowDrive)
            {
                var fwd = await robot.Motion.DriveWheelsAsync(speed, speed);
                Console.WriteLine($"  forward {speed:F0} mm/s: {fwd}");
                results.Add(new { step = "drive_forward", result = fwd.Result.ToString(), detail = fwd.Detail });
                pass &= fwd.Ok;
                await Task.Delay(TimeSpan.FromSeconds(driveSeconds));

                var back = await robot.Motion.DriveWheelsAsync(-speed, -speed);
                Console.WriteLine($"  back {speed:F0} mm/s: {back}");
                results.Add(new { step = "drive_back", result = back.Result.ToString(), detail = back.Detail });
                pass &= back.Ok;
                await Task.Delay(TimeSpan.FromSeconds(driveSeconds));
            }

            var stop = await robot.Motion.StopAllAsync();
            Console.WriteLine($"  stop all: {stop}");
            results.Add(new { step = "stop_all", result = stop.Result.ToString(), detail = stop.Detail });
            pass &= stop.Ok;
        }
        finally
        {
            robot.EmergencyStop();
            await Task.Delay(200);
            log.Dispose();
        }

        string human = allowDrive
            ? "the robot should have nodded, raised and lowered its lift, driven forward and back by roughly the same distance, and stopped"
            : "the robot should have nodded and raised and lowered its lift, without the wheels turning";
        var rec = Acceptance("motion", pass, human,
            new { wheelsDriven = allowDrive, speedMmps = speed, steps = results }, robot, AcceptancePath(a));
        Verdict("motion", pass, human, rec);
        robot.Disconnect();
        return pass ? 0 : 20;
    }

    // ------------------------------------------------------------------- lights

    /// <summary>
    /// Backpack LEDs and the infrared headlight. The robot reports nothing about either, so this is
    /// entirely a human check and says so.
    /// </summary>
    public static async Task<int> Lights(string[] a)
    {
        if (Target(a) is not var (ip, port)) return 1;
        double hold = Seconds(a, "--seconds", 1.5);

        var got = await ReadyRobot(ip, port, "lights");
        if (got is not var (robot, log, _)) return 10;
        using var _r = robot;

        var shown = new List<string>();
        try
        {
            foreach (var (name, c) in new (string, LedColor)[]
                     { ("red", LedColor.Red), ("green", LedColor.Green), ("blue", LedColor.Blue), ("white", LedColor.White) })
            {
                robot.Lights.SetBackpack(c);
                Console.WriteLine($"  backpack all {name} ({c}, packed 0x{c.Packed:x4})");
                shown.Add(name);
                await Task.Delay(TimeSpan.FromSeconds(hold));
            }

            robot.Lights.SetBackpack(LedColor.Red, LedColor.Green, LedColor.Blue);
            Console.WriteLine("  backpack top red, middle green, bottom blue");
            shown.Add("red/green/blue");
            await Task.Delay(TimeSpan.FromSeconds(hold));

            robot.Lights.BlinkBackpack(LedColor.Magenta, LedColor.Off);
            Console.WriteLine("  backpack blinking magenta");
            shown.Add("blink");
            await Task.Delay(TimeSpan.FromSeconds(hold * 2));

            robot.Lights.BackpackOff();
            Console.WriteLine("  backpack off");

            robot.Lights.SetHeadlight(true);
            Console.WriteLine($"  headlight on for {hold:F1}s (infrared: invisible to the eye, visible to a camera)");
            await Task.Delay(TimeSpan.FromSeconds(hold));
            robot.Lights.SetHeadlight(false);
        }
        finally { log.Dispose(); }

        // nothing here can be verified from the robot, so the automated half only says it stayed connected
        bool connected = robot.Transport.State == LinkState.Connected;
        const string human = "the backpack should have shown red, green, blue, white, then red/green/blue, then blinked magenta, then gone dark";
        var rec = Acceptance("lights", connected, human,
            new { coloursShown = shown, headlightToggled = true, note = "the robot reports nothing about its LEDs" },
            robot, AcceptancePath(a));
        Verdict("lights", connected, human, rec);
        robot.Disconnect();
        return connected ? 0 : 21;
    }

    // ------------------------------------------------------------------ sensors

    /// <summary>Reads the robot's own state for a while and reports what it said.</summary>
    public static async Task<int> Sensors(string[] a)
    {
        if (Target(a) is not var (ip, port)) return 1;
        double watch = Seconds(a, "--seconds", 8);

        var got = await ReadyRobot(ip, port, "sensors");
        if (got is not var (robot, log, _)) return 10;
        using var _r = robot;

        var cliffs = new List<string>();
        robot.Sensors.CliffDetected += c => { Console.WriteLine($"  {c}"); cliffs.Add(c.ToString()); };
        robot.Sensors.PickedUpChanged += p => Console.WriteLine($"  picked up: {p}");
        robot.Sensors.OnChargerChanged += p => Console.WriteLine($"  on charger: {p}");

        Console.WriteLine($"watching for {watch:F0}s. Lift the robot, or move it near an edge, to exercise the sensors.");
        robot.Sensors.RequestImuBurst(TimeSpan.FromSeconds(1));

        int imuChunks = 0;
        void countImu(RobotMessage m) { if (m is IMURawDataChunk) imuChunks++; }
        robot.Message += countImu;

        var end = DateTime.UtcNow.AddSeconds(watch);
        while (DateTime.UtcNow < end)
        {
            var s = robot.Sensors;
            Console.WriteLine($"  batt {s.BatteryVolts:F2}V charger={s.OnCharger} charging={s.Charging} " +
                              $"pickedUp={s.PickedUp} cliff={s.CliffDetectedNow} " +
                              $"head={s.HeadAngleRad:F2} lift={s.LiftAngleRad:F3}rad/{s.LiftHeightMm:F1}mm " +
                              $"accel={s.Accelerometer} gyro={s.Gyroscope} " +
                              $"cliffRaw=[{string.Join(",", s.CliffSensorsRaw ?? Array.Empty<ushort>())}]");
            await Task.Delay(1000);
        }
        robot.Message -= countImu;
        log.Dispose();

        var sensors = robot.Sensors;
        bool pass = sensors.BatteryVolts is > 3.0f and < 5.5f
                    && sensors.Accelerometer is { } acc && acc.Magnitude > 100
                    && sensors.CliffSensorsRaw is { Length: 4 }
                    && robot.State.StateCount > 50;
        const string human = "nothing, unless you lifted the robot or moved it near an edge, in which case those events should appear above";
        var rec = Acceptance("sensors", pass, human, new
        {
            stateMessages = robot.State.StateCount,
            batteryVolts = sensors.BatteryVolts,
            onCharger = sensors.OnCharger,
            charging = sensors.Charging,
            pickedUp = sensors.PickedUp,
            cliffDetected = sensors.CliffDetectedNow,
            cliffSensorsRaw = sensors.CliffSensorsRaw,
            accelerometer = sensors.Accelerometer?.ToString(),
            gyroscope = sensors.Gyroscope?.ToString(),
            headAngleRad = sensors.HeadAngleRad,
            liftAngleRad = sensors.LiftAngleRad,
            liftHeightMm = sensors.LiftHeightMm,
            imuRawChunks = imuChunks,
            cliffEvents = cliffs,
        }, robot, AcceptancePath(a));
        Verdict("sensors", pass, human, rec);
        robot.Disconnect();
        return pass ? 0 : 22;
    }

    // -------------------------------------------------------------------- cubes

    /// <summary>Turns discovery on and reports every cube the robot hears.</summary>
    public static async Task<int> Cubes(string[] a)
    {
        if (Target(a) is not var (ip, port)) return 1;
        double watch = Seconds(a, "--seconds", 15);

        var got = await ReadyRobot(ip, port, "cubes");
        if (got is not var (robot, log, _)) return 10;
        using var _r = robot;

        robot.Cubes.CubeDiscovered += c => Console.WriteLine($"  discovered {c}");
        robot.Cubes.ConnectionChanged += c => Console.WriteLine($"  {(c.Connected ? "connected" : "disconnected")} {c}");
        robot.Cubes.CubeTapped += c => Console.WriteLine($"  tapped {c}");
        robot.Cubes.CubeMoved += c => Console.WriteLine($"  {(c.Moving ? "moving" : "still")} {c}");

        Console.WriteLine($"discovery on for {watch:F0}s. Put a cube nearby, and tap it to exercise telemetry.");
        robot.Cubes.SetDiscovery(true);
        await Task.Delay(TimeSpan.FromSeconds(watch));
        robot.Cubes.SetDiscovery(false);
        log.Dispose();

        var cubes = robot.Cubes.DiscoveredCubes;
        Console.WriteLine($"\n{cubes.Count} cube(s) heard, {robot.Cubes.ConnectedCubes.Count} connected");
        foreach (var c in cubes) Console.WriteLine($"  {c}");

        bool pass = cubes.Count > 0;
        if (!pass) Console.WriteLine("No cube was heard. A cube out of range or with a flat battery looks the same from here.");
        const string human = "the cubes you placed nearby should appear above; if you tapped one, its tap count should be non-zero";
        var rec = Acceptance("cubes", pass, human, new
        {
            discovered = cubes.Select(c => new
            {
                factoryId = $"0x{c.FactoryId:x8}",
                objectId = c.ObjectId,
                type = c.Type.ToString(),
                rssi = c.Rssi,
                connected = c.Connected,
                advertisements = c.Advertisements,
                batteryLevelRaw = c.BatteryLevelRaw,
                taps = c.Taps,
                upAxis = c.UpAxis?.ToString(),
            }).ToArray(),
        }, robot, AcceptancePath(a));
        Verdict("cubes", pass, human, rec);
        robot.Disconnect();
        return pass ? 0 : 23;
    }
}
