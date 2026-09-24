using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Transport;
using StbImageSharp;

namespace Cozmo.Conformance;

/// <summary>
/// <c>control-check</c>: one scripted, self-judging direct-control run on the operator's robot (PROJECT_STATE.md,
/// hardware-first plan step 2). It connects through the app layer (<see cref="CozmoRobot.Create"/>, then the
/// ConnectToRobot game message and the engine's RobotConnectionResponse, which is exactly what
/// <see cref="CozmoRobot.ConnectAsync"/> does, done step by step so the handshake frames are traced), then runs a
/// fixed sequence of checks through the public device APIs: CONNECT, STATE, HEAD, LIFT, DRIVE (only with
/// --allow-drive), FACE, AUDIO, ANIM, ANIM_CANCEL, CUBES, CAMERA, DISCONNECT. Each check is judged from telemetry
/// against explicit numeric criteria written to result.json. What only a person can judge (that a pattern
/// appeared, that beeps were heard) is a <c>humanNote</c> with <c>humanVerdict: null</c>. It writes one
/// self-contained bundle and never commits anything.
///
/// A pass here is hardware verification only: it does not raise the provenance of any record it cites.
/// </summary>
public static class ControlCheck
{
    public const string TestId = "CONTROL";

    // ---- the fixed criteria and timings (not options, so every bundle ran the same script)
    internal const int ConnectBudgetMs = 10000;
    internal const uint ShippedFirmware = 2381;
    internal const int CalibrationWaitMs = 10000;
    internal const int StateWindowMs = 5000;
    /// <summary>
    /// The RobotState rate band, judged from the robot's own timestamps. It is this test's tolerance, not source: no
    /// engine source gives the rate; the 2026-09-18 smoke capture measured 33.5 Hz
    /// (re-analysis/captures/2026-09-18_fw2457_hw1.5_smoke_head0.4.console.txt).
    /// </summary>
    internal const double StateRateMinHz = 25.0, StateRateMaxHz = 40.0;
    internal const string StateRateCapture = "re-analysis/captures/2026-09-18_fw2457_hw1.5_smoke_head0.4.console.txt";
    internal const float BatteryMinV = 3.0f, BatteryMaxV = 4.5f;
    internal const float HeadUpRad = 0.3f, HeadDownRad = -0.3f, HeadTolRad = 0.05f;
    internal const int HeadWithinMs = 2000;
    internal const float LiftUpMm = 50f, LiftDownMm = 32f, LiftTolMm = 5f;
    internal const int LiftWithinMs = 3000;
    internal const float DriveSpeedMmps = 30f;
    internal const int DriveLegMs = 1000;
    internal const float DriveMinMm = 20f, DriveMaxMm = 40f, WheelStoppedMmps = 1f;
    internal const int WheelStopWithinMs = 2000, DriveSettleMs = 300;
    internal const int FaceHoldMs = 3000, CounterSettleMs = 500;
    internal const int AudioBeeps = 2, AudioMinFramesPlayed = 25, AudioSettleMaxMs = 2000;
    internal const string AnimClip = "anim_bored_01";
    internal const string CancelClip = "anim_codelab_staring_loop";
    internal const int AnimStallGapMs = 250, AnimCompletionSlackMs = 1000, AnimTimeoutSlackMs = 5000;
    internal const int AnimEndGraceMs = 100, AnimEndWatchMs = 1000;
    internal const int CancelAfterMs = 1000, CancelGraceMs = 100, CancelWatchMs = 1500;
    internal const int CubeWaitMs = 20000, CubeAccelWindowMs = 3000, CubeAccelMin = 1;
    internal const int CameraWindowMs = 3000, CameraMinFrames = 30, CameraSaved = 3;
    internal const int CameraWidth = 320, CameraHeight = 240;
    internal const int DisposeBoundMs = 5000, AfterDisposeWatchMs = 1000;
    internal const int WatchdogMs = 240000;
    /// <summary>How long an abnormal exit (watchdog, unhandled exception, Ctrl+C) waits for the motor stop and Dispose.</summary>
    internal const int AbortStopBoundMs = 2000;
    internal const int ExitInterrupted = 4;

    /// <summary>One check: its id, title and the fidelity records it names. Each id must be in re-analysis/fidelity_manifest.json.</summary>
    public sealed record CheckDef(string Id, string Title, string[] Records);

    public static readonly CheckDef[] Checks =
    {
        new("CONNECT", "Connect through the app layer: Success response, firmware logged, time synced, first full state, ready to stream, within 10 s",
            new[] { "M1-024", "M1-025", "M1-026", "M1-028", "M1-029", "M1-030", "M1-033", "M1-040", "M1-041", "M1-042" }),
        new("STATE", "RobotState streams for 5 s at 25..40 Hz by the robot's own timestamps, with a plausible battery voltage and no timeout",
            new[] { "M1-024", "M1-033", "M1-041" }),
        new("HEAD", "Head to +0.3 rad then -0.3 rad, each within 0.05 rad in RobotState within 2 s",
            new[] { "M4-001", "M4-003", "M4-004", "M4-005" }),
        new("LIFT", "Lift to 50 mm then 32 mm, each within 5 mm in RobotState within 3 s",
            new[] { "M2-003", "M4-002", "M4-003", "M4-004", "M4-005" }),
        new("DRIVE", "Drive 30 mm forward and back at 30 mm/s with stop-on-cliff on; the pose moves 20..40 mm each way and the wheels stop",
            new[] { "M4-006", "M4-007" }),
        new("FACE", "A known test image held on the face for 3 s: the robot's animation byte count rises and its drop count stays",
            new[] { "M3-006", "M3-007", "M3-008", "M3-015" }),
        new("AUDIO", "A 1 s two-beep sequence through CozmoAudio.Play: at least 25 audio frames played and the drop count stays",
            new[] { "M1-042", "M3-010", "M3-011", "M3-012", "M3-013", "M3-014", "M3-017" }),
        new("ANIM", "anim_bored_01 plays to completion (only with --allow-drive: it rolls back about 2 cm): every keyframe fires, no stall, streaming stops at the end",
            new[] { "M1-041", "M5-001", "M5-004", "M5-006", "M5-007", "M5-008", "M5-016", "M5-018", "M5-019" }),
        new("ANIM_CANCEL", "A long animation cancelled after 1 s: no animation message sent more than 100 ms after the cancel",
            new[] { "M5-008", "M5-023" }),
        new("CUBES", "Block pool enabled at connect; a cube is heard, one connects, and its accelerometer stream arrives",
            new[] { "M1-042", "M4-008", "M4-009", "M4-010", "M4-011" }),
        new("CAMERA", "The camera stream opened at connect gives at least 30 complete frames in 3 s, each grey frame a 320x240 JPEG",
            new[] { "M1-041", "M3-001", "M3-002", "M3-003", "M3-004", "M3-005", "M3-016" }),
        new("DISCONNECT", "Dispose sends the DisconnectRequest, nothing is sent after it, and Dispose returns",
            new[] { "M1-015", "M1-019", "M1-025" }),
    };

    /// <summary>The HARDWARE_ONLY records whose uncertainty the run depends on; each is also named by a check.</summary>
    public static readonly (string Id, string Note)[] HardwareOnly =
    {
        ("M1-033", "robot-side transport behaviour (how the robot answers, acks and resends) is an input to every check; it has no expected value from source"),
        ("M3-008", "scanline parity of the face image on the robot's display: FACE's humanNote is where a wrong image would be seen"),
        ("M3-016", "the colour camera format: CAMERA records whether any frame looks colour, with no verdict"),
    };

    /// <summary>Every fidelity record the run's result names. Each must exist in re-analysis/fidelity_manifest.json.</summary>
    public static readonly string[] CitedRecords = Checks.SelectMany(c => c.Records).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray();

    public static int Run(string[] args)
    {
        IPAddress? ip = null; string? obb = null; string? outParent = null; bool allowDrive = false, yes = false, noObb = false;
        for (int i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--allow-drive": allowDrive = true; break;
                case "--yes": yes = true; break;
                case "--no-obb": noObb = true; break;
                case "--obb":
                    if (i + 1 >= args.Length) return UsageError("--obb needs a directory");
                    obb = args[++i]; break;
                case "--out":
                    if (i + 1 >= args.Length) return UsageError("--out needs a directory");
                    outParent = args[++i]; break;
                default:
                    if (args[i].StartsWith("--", StringComparison.Ordinal)) return UsageError($"unknown option {args[i]}");
                    if (ip is not null) return UsageError($"unexpected argument {args[i]}");
                    if (!IPAddress.TryParse(args[i], out var parsed)) return UsageError($"not an IP address: {args[i]}");
                    ip = parsed; break;
            }
        }
        ip ??= RobotAddress.DefaultFor(false);                      // 172.31.1.1

        string? repoRoot = LinkCheck.FindRepoRoot();
        if (noObb && obb is not null) return UsageError("--obb and --no-obb together: give one");
        if (noObb) obb = null;
        else
        {
            bool given = obb is not null;
            obb ??= DefaultObb(repoRoot);
            if (obb is not null) obb = Path.GetFullPath(obb);
            if (ObbPreflight(obb, given, repoRoot) is { } why)
            {
                Console.Error.WriteLine("control-check: " + why);
                return 2;
            }
        }
        string? parent = outParent ?? (repoRoot is null ? null : Path.Combine(repoRoot, "re-analysis", "acceptance", "hardware"));
        if (parent is null) return UsageError("the repository root (a directory holding AGENTS.md and re-analysis/fidelity_manifest.json) was not found above the current directory or the tool; pass --out <dir>");

        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var bundle = Path.GetFullPath(Path.Combine(parent, $"{stamp}-{TestId}"));
        var run = new ControlRun(ip, obb, noObb, allowDrive, yes, bundle, repoRoot, args);
        return run.ExecuteAsync().GetAwaiter().GetResult();
    }

    /// <summary>The repository's re-analysis/obb, or cozmo-stack/re-analysis/obb, if either exists.</summary>
    internal static string? DefaultObb(string? repoRoot)
    {
        if (repoRoot is null) return null;
        foreach (var p in new[] { Path.Combine(repoRoot, "re-analysis", "obb"), Path.Combine(repoRoot, "cozmo-stack", "re-analysis", "obb") })
            if (Directory.Exists(p)) return p;
        return null;
    }

    internal static string AnimationAssets(string obb) => Path.Combine(obb, "assets", "cozmo_resources", "assets");
    internal static string Resources(string obb) => Path.Combine(obb, "assets", "cozmo_resources");

    /// <summary>
    /// The clip files ANIM and ANIM_CANCEL need, where the run's loader looks: <see cref="AnimationAssets"/> is
    /// passed to <c>CozmoAnimations.LoadFrom</c>, whose <c>AnimationLibrary.Open</c> reads the <c>*.bin</c> files under
    /// its <c>animations</c> directory.
    /// </summary>
    public static string[] ObbExpectedFiles(string obb) => new[] { AnimClip, CancelClip }
        .Select(n => Path.Combine(AnimationAssets(obb), "animations", n + ".bin")).ToArray();

    /// <summary>Why the OBB cannot serve ANIM and ANIM_CANCEL, or null when every expected clip file is present.</summary>
    public static string? ObbPreflight(string? obb, bool given, string? repoRoot)
    {
        const string hint = "--obb should point at the unpacked OBB root: the directory that holds assets/cozmo_resources/assets/animations. "
                            + "Pass --no-obb to run without it (ANIM and ANIM_CANCEL are then skipped).";
        if (obb is null)
        {
            string where = repoRoot is null ? "the repository root was not found"
                : $"neither {Path.Combine(repoRoot, "re-analysis", "obb")} nor {Path.Combine(repoRoot, "cozmo-stack", "re-analysis", "obb")} exists";
            return $"no OBB directory: --obb was not given and {where}. Expected <obb>/assets/cozmo_resources/assets/animations/{AnimClip}.bin "
                   + $"and {CancelClip}.bin. {hint}";
        }
        var missing = ObbExpectedFiles(obb).Where(f => !File.Exists(f)).ToList();
        if (missing.Count == 0) return null;
        return $"the OBB directory {obb} ({(given ? "from --obb" : "the default")}) lacks the clip file(s) ANIM and ANIM_CANCEL need: "
               + string.Join("; ", missing.Select(m => "expected " + m)) + ". " + hint;
    }

    private static int UsageError(string why)
    {
        Console.Error.WriteLine($"control-check: {why}");
        Console.Error.WriteLine("usage: control-check [robot-ip] [--obb <dir> | --no-obb] [--allow-drive] [--out <dir>] [--yes]");
        return 2;
    }

    // ------------------------------------------------------------------ pure helpers (tested)

    /// <summary>The signed distance travelled along the starting heading, in mm.</summary>
    public static double AlongHeading(float x0, float y0, float heading0, float x1, float y1) =>
        (x1 - x0) * Math.Cos(heading0) + (y1 - y0) * Math.Sin(heading0);

    /// <summary>
    /// The rate, in Hz, of states carrying these robot timestamps (ms, in arrival order): (n-1)*1000/(last-first),
    /// the span taken modulo 2^32. NaN with fewer than two states or a zero span.
    /// </summary>
    public static double RateFromRobotTimestamps(IReadOnlyList<uint> timestampsMs)
    {
        if (timestampsMs.Count < 2) return double.NaN;
        uint span = unchecked(timestampsMs[^1] - timestampsMs[0]);
        return span == 0 ? double.NaN : (timestampsMs.Count - 1) * 1000.0 / span;
    }

    /// <summary>The increase of a byte counter that may wrap at 256.</summary>
    public static int ByteCounterDelta(byte before, byte after) => (after - before + 256) % 256;

    /// <summary>The largest gap between consecutive times, or NaN with fewer than two.</summary>
    public static double MaxGap(IReadOnlyList<double> sortedTimes)
    {
        double max = double.NaN;
        for (int i = 1; i < sortedTimes.Count; i++)
        {
            double g = sortedTimes[i] - sortedTimes[i - 1];
            if (double.IsNaN(max) || g > max) max = g;
        }
        return max;
    }
}

/// <summary>One run of the check. All times are milliseconds since the run began, from the host's UTC clock.</summary>
internal sealed class ControlRun
{
    private sealed record FrameRec(double T, DateTime Utc, bool Out, byte[] Raw, Frame? Frame, string? Error);
    private sealed record EventRec(double T, string Kind, JsonObject Data);
    private sealed record StateRec(double T, RobotState S);
    private sealed record AnimStateRec(double T, AnimationState A);
    private sealed record CamRec(double T, CameraFrame F);
    /// <summary>An outbound CLAD message the first time its reliable sequence id went out (resends are not new sends).</summary>
    private sealed record Sent(double T, byte Tag, byte[] Payload);

    /// <summary>A JSON object whose writes and snapshot are taken under the run's gate, so an abnormal-exit bundle never serialises it mid-write.</summary>
    private sealed class LockedJson
    {
        private readonly object _gate;
        private readonly JsonObject _o = new();
        public LockedJson(object gate) => _gate = gate;
        public JsonNode? this[string key] { set { lock (_gate) _o[key] = value; } }
        public JsonObject Snapshot() { lock (_gate) return (JsonObject)_o.DeepClone(); }
    }

    /// <summary>
    /// One check's record. Every mutable collection is changed and read under the run's gate: the watchdog, the
    /// unhandled-exception handler and Ctrl+C write the bundle from another thread while the main thread may still be
    /// filling a check in.
    /// </summary>
    private sealed class CheckRec
    {
        public readonly ControlCheck.CheckDef Def;
        private readonly object _gate;
        private readonly JsonArray _criteria = new();
        private readonly List<string> _warnings = new();
        public readonly LockedJson Measured, Prerequisites;
        private string? _humanNote, _note, _skipReason;
        private JsonObject? _observation;
        private double _startMs = double.NaN, _endMs = double.NaN;

        public CheckRec(ControlCheck.CheckDef def, object gate)
        {
            Def = def; _gate = gate; Measured = new LockedJson(gate); Prerequisites = new LockedJson(gate);
        }

        public string? HumanNote { get { lock (_gate) return _humanNote; } set { lock (_gate) _humanNote = value; } }
        public string? Note { get { lock (_gate) return _note; } set { lock (_gate) _note = value; } }
        public string? SkipReason { get { lock (_gate) return _skipReason; } set { lock (_gate) _skipReason = value; } }
        public JsonObject? Observation { get { lock (_gate) return _observation; } set { lock (_gate) _observation = value; } }
        public double StartMs { get { lock (_gate) return _startMs; } set { lock (_gate) _startMs = value; } }
        public double EndMs { get { lock (_gate) return _endMs; } set { lock (_gate) _endMs = value; } }

        public bool Skipped { get { lock (_gate) return _skipReason is not null; } }
        /// <summary>
        /// PASS when not skipped, at least one criterion was judged, and every judged criterion passed. A criterion with
        /// <c>pass: null</c> was not judged (its <c>notJudged</c> says why) and does not enter the status; it is listed
        /// in <c>unjudgedCriteria</c>, so a PASS never hides it.
        /// </summary>
        public bool Pass
        {
            get
            {
                lock (_gate)
                {
                    if (_skipReason is not null) return false;
                    var judged = _criteria.Where(c => c!["pass"] is not null).ToList();
                    return judged.Count > 0 && judged.All(c => c!["pass"]!.GetValue<bool>());
                }
            }
        }
        public string Status { get { lock (_gate) return Skipped ? "SKIPPED" : Pass ? "PASS" : "FAIL"; } }
        public string[] Unjudged { get { lock (_gate) return _criteria.Where(c => c!["pass"] is null).Select(c => c!["name"]!.GetValue<string>()).ToArray(); } }
        public string? FirstWarning { get { lock (_gate) return _warnings.FirstOrDefault(); } }

        public void Crit(string name, string expected, JsonNode? measured, bool pass) => Crit(name, expected, measured, (bool?)pass, null);

        /// <summary>A criterion; <paramref name="pass"/> null records it as not judged, for the reason given.</summary>
        public void Crit(string name, string expected, JsonNode? measured, bool? pass, string? notJudged)
        {
            var o = new JsonObject { ["name"] = name, ["expected"] = expected, ["measured"] = measured, ["pass"] = pass };
            if (pass is null) o["notJudged"] = notJudged ?? "not judged";
            lock (_gate) _criteria.Add(o);
        }

        public void ClearCriteria() { lock (_gate) _criteria.Clear(); }
        public void Warn(string w) { lock (_gate) _warnings.Add(w); }
        public string[] CriteriaExpected() { lock (_gate) return _criteria.Select(x => x!["expected"]!.GetValue<string>()).ToArray(); }

        public JsonObject ToJson(Dictionary<string, string> status)
        {
            lock (_gate)
            {
                return new JsonObject
                {
                    ["id"] = Def.Id,
                    ["title"] = Def.Title,
                    ["status"] = Status,
                    ["pass"] = Pass,
                    ["skipReason"] = _skipReason,
                    ["records"] = new JsonArray(Def.Records.Select(r => (JsonNode)new JsonObject { ["id"] = r, ["status"] = status.GetValueOrDefault(r, "?") }).ToArray()),
                    ["prerequisites"] = Prerequisites.Snapshot(),
                    ["criteria"] = _criteria.DeepClone(),
                    ["unjudgedCriteria"] = new JsonArray(Unjudged.Select(u => (JsonNode)u).ToArray()),
                    ["measured"] = Measured.Snapshot(),
                    ["warnings"] = new JsonArray(_warnings.Select(w => (JsonNode)w).ToArray()),
                    ["humanNote"] = _humanNote,
                    ["humanVerdict"] = null,
                    ["observation"] = _observation?.DeepClone(),
                    ["note"] = _note,
                    ["startMs"] = LinkCheck.Num(_startMs), ["endMs"] = LinkCheck.Num(_endMs),
                };
            }
        }
    }

    private static readonly HashSet<byte> AnimStreamTags = new()
    {
        (byte)RobotMessageId.AnimAudioSample, (byte)RobotMessageId.AnimAudioSilence, (byte)RobotMessageId.AnimFaceImage,
        (byte)RobotMessageId.AnimHeadAngle, (byte)RobotMessageId.AnimLiftHeight, (byte)RobotMessageId.AnimBodyMotion,
        (byte)RobotMessageId.AnimStartOfAnimation, (byte)RobotMessageId.AnimEndOfAnimation,
    };
    private static readonly byte AudioSampleTag = (byte)RobotMessageId.AnimAudioSample, AudioSilenceTag = (byte)RobotMessageId.AnimAudioSilence;
    private static readonly byte StartTag = (byte)RobotMessageId.AnimStartOfAnimation, EndTag = (byte)RobotMessageId.AnimEndOfAnimation;
    private static readonly byte ImageChunkTag = (byte)RobotMessageId.Image;

    private readonly IPAddress _ip; private readonly string? _obb; private readonly bool _noObb, _allowDrive, _yes;
    /// <summary>Guards the check records, the robot and animation info, the saved-image list and the camera window against a bundle written from another thread.</summary>
    private readonly object _gate = new();
    private int _aborting;
    private readonly string _bundle; private readonly string? _repoRoot; private readonly string[] _argv;
    private readonly DateTime _t0Utc = DateTime.UtcNow;
    private readonly DateTimeOffset _t0Local = DateTimeOffset.Now;
    private readonly ConcurrentQueue<FrameRec> _frames = new();
    private readonly ConcurrentQueue<EventRec> _events = new();
    private readonly ConcurrentQueue<StateRec> _states = new();
    private readonly ConcurrentQueue<AnimStateRec> _animStates = new();
    private readonly ConcurrentQueue<CamRec> _camFrames = new();
    private readonly ConcurrentQueue<(double T, uint ObjectId)> _objectAccel = new();
    private readonly ConcurrentQueue<(double T, string Line)> _engineLog = new();
    private readonly List<(string Name, double Start)> _phases = new();
    private readonly List<CheckRec> _checks = new();
    private readonly List<string> _exceptions = new();
    private readonly Dictionary<string, double> _marks = new();
    private readonly JsonObject _robotInfo = new();
    private readonly JsonObject _animInfo = new();
    private int _handlerFaults;
    private int _written;
    private CozmoRobot? _robot;
    private RobotConnectionResponse? _response;
    private double _responseT = double.NaN;
    private (double From, double To)? _cameraWindow;
    private readonly List<string> _savedImages = new();

    internal ControlRun(IPAddress ip, string? obb, bool noObb, bool allowDrive, bool yes, string bundle, string? repoRoot, string[] argv)
    {
        _ip = ip; _obb = obb; _noObb = noObb; _allowDrive = allowDrive; _yes = yes; _bundle = bundle; _repoRoot = repoRoot; _argv = argv;
    }

    private double Now() => (DateTime.UtcNow - _t0Utc).TotalMilliseconds;
    private double T(DateTime utc) => (utc - _t0Utc).TotalMilliseconds;
    private void Phase(string name) { lock (_phases) _phases.Add((name, Now())); Event("phase", new JsonObject { ["name"] = name }); }
    private double Mark(string name) { double t = Now(); lock (_marks) _marks[name] = t; return t; }
    private void Event(string kind, JsonObject data) => _events.Enqueue(new EventRec(Now(), kind, data));

    private static bool Poll(Func<bool> cond, int timeoutMs)
    {
        var sw = Stopwatch.StartNew();
        while (!cond()) { if (sw.ElapsedMilliseconds >= timeoutMs) return cond(); Thread.Sleep(2); }
        return true;
    }

    private static void Hold(int ms)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < ms) Thread.Sleep(Math.Min(20, Math.Max(1, ms - (int)sw.ElapsedMilliseconds)));
    }

    private FrameRec[] Frames() => _frames.ToArray();
    private StateRec[] States() => _states.ToArray();
    private AnimStateRec[] AnimStates() => _animStates.ToArray();
    private AnimStateRec? LatestAnimState() => _animStates.ToArray().LastOrDefault();

    private bool LinkUp => _robot is { } r && _response?.Result == RobotConnectionResult.Success && r.Engine.ConnectionState == 2;

    // ------------------------------------------------------------------ the run

    internal async Task<int> ExecuteAsync()
    {
        using var hr = new HighResolutionTimer();
        Console.WriteLine($"control-check: robot {_ip}:{RobotAddress.RemotePort(false)}; bundle {_bundle}");
        PrintSetup();
        if (!_yes)
        {
            Console.Write("Press Enter to start (Ctrl+C to abort) ... ");
            Console.ReadLine();
        }
        Directory.CreateDirectory(_bundle);

        // Every abnormal exit stops the robot first (bounded), then writes the bundle.
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            lock (_exceptions) _exceptions.Add("unhandled: " + e.ExceptionObject);
            Abort("unhandled exception", exitCode: null);
        };
        Console.CancelKeyPress += (_, e) =>
        {
            // A second Ctrl+C while the first is stopping the robot is let through, so the operator can still force it.
            if (Volatile.Read(ref _aborting) == 1) return;
            e.Cancel = true;
            lock (_exceptions) _exceptions.Add("interrupted: Ctrl+C");
            // Off the handler's thread: Environment.Exit is not called from inside the console control handler.
            new Thread(() => Abort("interrupted (Ctrl+C)", ControlCheck.ExitInterrupted)) { IsBackground = false, Name = "control-check-interrupt" }.Start();
        };
        var watchdog = new Thread(() =>
        {
            Thread.Sleep(ControlCheck.WatchdogMs);
            if (Volatile.Read(ref _written) == 1) return;
            lock (_exceptions) _exceptions.Add($"watchdog: the run exceeded {ControlCheck.WatchdogMs} ms");
            Console.WriteLine($"control-check: WATCHDOG after {ControlCheck.WatchdogMs / 1000} s");
            Abort("watchdog", exitCode: 3);
        }) { IsBackground = true, Name = "control-check-watchdog" };
        watchdog.Start();

        CheckRec Rec(string id) { var c = new CheckRec(ControlCheck.Checks.Single(d => d.Id == id), _gate); lock (_checks) _checks.Add(c); return c; }
        var connect = Rec("CONNECT");
        var rest = ControlCheck.Checks.Skip(1).Select(d => Rec(d.Id)).ToDictionary(c => c.Def.Id);

        try
        {
            Phase("create");
            string? resources = _obb is null ? null : ControlCheck.Resources(_obb);
            if (resources is not null && !Directory.Exists(resources)) resources = null;
            var engineOptions = new CozmoEngineOptions { ResourcesPath = resources };
            _robot = CozmoRobot.Create(engineOptions: engineOptions);
            lock (_gate)
            {
                _robotInfo["resourcesPath"] = resources;
                _robotInfo["blockPoolPath"] = CozmoRobot.DefaultBlockPoolPath;
            }
            Subscribe(_robot);
            LoadAnimations(_robot);

            Phase("CONNECT");
            await RunCheck(connect, () => { Connect(connect); return Task.CompletedTask; });

            // Stop-on-cliff before anything moves (anim_bored_01 has a body keyframe; DRIVE drives), as `drive` and `anim` do.
            if (LinkUp)
            {
                Mark("stopOnCliffCall");
                _robot.Sensors.SetStopOnCliff(true);
                Event("stop-on-cliff", new JsonObject { ["enabled"] = true });
            }

            bool calibrated = false;
            foreach (var id in new[] { "STATE", "HEAD", "LIFT", "DRIVE", "FACE", "AUDIO", "ANIM", "ANIM_CANCEL", "CUBES", "CAMERA" })
            {
                var c = rest[id];
                if (!LinkUp)
                {
                    c.SkipReason = _response is null ? "no connection: CONNECT got no RobotConnectionResponse"
                        : _response.Result != RobotConnectionResult.Success ? $"no connection: RobotConnectionResponse {_response.Result}"
                        : "the connection was lost before this check (see events.jsonl)";
                    continue;
                }
                Phase(id);
                switch (id)
                {
                    case "STATE": await RunCheck(c, () => { StateCheck(c); return Task.CompletedTask; }); break;
                    case "HEAD":
                        calibrated = WaitForCalibration(c);
                        if (calibrated) await RunCheck(c, () => HeadCheck(c));
                        break;
                    case "LIFT":
                        c.Prerequisites["motorCalibrationComplete"] = calibrated;
                        if (!calibrated) c.SkipReason = "head and lift calibration did not complete (see HEAD prerequisites); motion is gated on it (policy M4-004)";
                        else await RunCheck(c, () => LiftCheck(c));
                        break;
                    case "DRIVE":
                        c.Prerequisites["allowDrive"] = _allowDrive;
                        c.Prerequisites["motorCalibrationComplete"] = calibrated;
                        if (!_allowDrive) c.SkipReason = "not requested: --allow-drive was not given";
                        else if (!calibrated) c.SkipReason = "head and lift calibration did not complete (see HEAD prerequisites); motion is gated on it (policy M4-004)";
                        else await RunCheck(c, () => DriveCheck(c));
                        break;
                    case "FACE": await RunCheck(c, () => { FaceCheck(c); return Task.CompletedTask; }); break;
                    case "AUDIO": await RunCheck(c, () => { AudioCheck(c); return Task.CompletedTask; }); break;
                    case "ANIM":
                        // anim_bored_01's body keyframe drives the wheels (-75 mm/s for 264 ms, about 2 cm back): as DRIVE, only on request.
                        c.Prerequisites["allowDrive"] = _allowDrive;
                        if (!_allowDrive) c.SkipReason = "anim_bored_01 drives the wheels; rerun with --allow-drive";
                        else await RunCheck(c, () => AnimCheck(c));
                        break;
                    case "ANIM_CANCEL": await RunCheck(c, () => CancelCheck(c)); break;
                    case "CUBES": await RunCheck(c, () => CubesCheck(c)); break;
                    case "CAMERA": await RunCheck(c, () => { CameraCheck(c); return Task.CompletedTask; }); break;
                }
            }

            Phase("DISCONNECT");
            var d = rest["DISCONNECT"];
            bool linkUpAtEnd = LinkUp;
            d.Prerequisites["linkUpBeforeDispose"] = linkUpAtEnd;
            if (!linkUpAtEnd) d.SkipReason = "no connection to disconnect (see CONNECT)";
            await RunCheck(d, () => { DisconnectCheck(d, judge: linkUpAtEnd); return Task.CompletedTask; }, alwaysRun: true);
        }
        catch (Exception e)
        {
            lock (_exceptions) _exceptions.Add(e.ToString());
            Event("exception", new JsonObject { ["exception"] = e.ToString() });
            try { _robot?.Dispose(); } catch (Exception e2) { lock (_exceptions) _exceptions.Add("during dispose: " + e2); }
        }

        string overall = WriteBundleOrReport(crashed: null) ?? "FAIL (the bundle could not be written; see the error above)";
        Console.WriteLine();
        CheckRec[] all; lock (_checks) all = _checks.ToArray();
        foreach (var c in all)
        {
            var unjudged = c.Unjudged;
            Console.WriteLine($"  {c.Status,-7}  {c.Def.Id,-11}  {(c.Skipped ? c.SkipReason : c.Def.Title)}"
                              + (unjudged.Length == 0 ? "" : $"   [not judged: {string.Join(", ", unjudged)}]")
                              + (c.HumanNote is null ? "" : "   [human: " + c.HumanNote + "]"));
        }
        Console.WriteLine($"CONTROL: {overall}");
        Console.WriteLine($"bundle: {_bundle}");
        Console.WriteLine("copy that whole folder back; nothing has been committed.");
        return overall == "PASS" ? 0 : 1;
    }

    /// <summary>
    /// An abnormal exit (watchdog, unhandled exception, Ctrl+C): a best-effort motor stop and Dispose bounded by
    /// <see cref="ControlCheck.AbortStopBoundMs"/>, then the bundle, then <see cref="Environment.Exit"/> when an exit
    /// code is given (an unhandled exception terminates the process by itself). Runs once.
    /// </summary>
    private void Abort(string why, int? exitCode)
    {
        if (Interlocked.Exchange(ref _aborting, 1) == 1) return;
        StopRobotBounded(why);
        if (Volatile.Read(ref _written) == 0) WriteBundleOrReport(crashed: why);
        Console.WriteLine($"control-check: stopped ({why}); bundle {_bundle}");
        if (exitCode is { } code) Environment.Exit(code);
    }

    /// <summary>EmergencyStop (StopAllMotors, DriveWheels 0) and Dispose on a separate thread, waited for at most the bound.</summary>
    private void StopRobotBounded(string why)
    {
        var robot = _robot;
        if (robot is null) return;
        double t = Now();
        var stopper = new Thread(() =>
        {
            try { robot.EmergencyStop(); } catch (Exception e) { lock (_exceptions) _exceptions.Add($"{why}: EmergencyStop: {e}"); }
            try { robot.Dispose(); } catch (Exception e) { lock (_exceptions) _exceptions.Add($"{why}: Dispose: {e}"); }
        }) { IsBackground = true, Name = "control-check-abort-stop" };
        stopper.Start();
        bool done = stopper.Join(ControlCheck.AbortStopBoundMs);
        Event("abort-stop", new JsonObject { ["why"] = why, ["completedWithinBound"] = done, ["ms"] = LinkCheck.Num(Now() - t) });
        if (!done) lock (_exceptions) _exceptions.Add($"{why}: the motor stop and Dispose did not finish within {ControlCheck.AbortStopBoundMs} ms");
    }

    /// <summary>Writes the bundle; on failure prints the exception (the bundle is never lost silently) and returns null.</summary>
    private string? WriteBundleOrReport(string? crashed)
    {
        try { return WriteBundle(crashed); }
        catch (Exception e)
        {
            Console.Error.WriteLine($"control-check: writing the bundle {_bundle} failed: {e}");
            return null;
        }
    }

    private void PrintSetup()
    {
        Console.WriteLine();
        Console.WriteLine("SETUP (once), before the run connects:");
        Console.WriteLine("  - this PC on Cozmo's Wi-Fi;");
        Console.WriteLine("  - Cozmo on the floor or on a cliff-safe table, off the charger, with nothing touching him;");
        Console.WriteLine("  - a light cube within about 30 cm of him, powered (tab pulled / battery in).");
        bool anim = _obb is not null;
        Console.WriteLine("What he will do (about 2 minutes): nod his head, raise and lower his lift, "
                          + (_allowDrive ? "drive about 3 cm forward and back, " : "")
                          + "show a test pattern for 3 s, play two beeps, "
                          + (anim && _allowDrive ? "play anim_bored_01 (which rolls him back about 2 cm) and part of a second animation (face and sound only), "
                             : anim ? "play part of an animation (face and sound only), " : "")
                          + "then disconnect. Stop-on-cliff is turned on before anything moves.");
        if (!_allowDrive)
            Console.WriteLine("  DRIVE and ANIM will be skipped (anim_bored_01 drives the wheels, about 2 cm back): pass --allow-drive once he has clear space around him.");
        if (_noObb) Console.WriteLine("  ANIM and ANIM_CANCEL will be skipped: --no-obb was given.");
        else if (_obb is not null) Console.WriteLine($"  Animations from {ControlCheck.AnimationAssets(_obb)}.");
        Console.WriteLine("  Watch his face during the test pattern and listen for the beeps: the bundle asks what you saw and heard.");
        Console.WriteLine();
    }

    private void Subscribe(CozmoRobot robot)
    {
        robot.Transport.FrameTrace += e => _frames.Enqueue(new FrameRec(T(e.Utc), e.Utc, e.Outbound, e.Raw, e.Frame, e.Error));
        robot.Transport.Warning += w => Event("transport-warning", new JsonObject { ["text"] = w });
        robot.Transport.Disconnected += r => Event("transport-disconnected", new JsonObject { ["reason"] = r });
        robot.Transport.Connected += () => Event("transport-connected", new JsonObject());
        robot.Engine.LogLine += l => { _engineLog.Enqueue((Now(), l)); Event("engine-log", new JsonObject { ["line"] = l }); };
        robot.Engine.ConnectionResponse += r =>
        {
            _responseT = Now();
            _response = r;
            Event("connection-response", new JsonObject
            {
                ["result"] = r.Result.ToString(), ["fwVersion"] = r.FwVersion, ["serial"] = $"0x{r.SerialNumber:x8}",
                ["bodyHwVersion"] = r.BodyHWVersion, ["bodyColor"] = r.BodyColor,
            });
        };
        robot.Engine.RobotDisconnected += m => Event("robot-disconnected", new JsonObject { ["timeSinceLastMsgSec"] = m.TimeSinceLastMsgSec });
        robot.Engine.RobotErrorPassThrough += m => Event("robot-error", new JsonObject { ["code"] = m.Code });
        robot.HandlerFaulted += e => { Interlocked.Increment(ref _handlerFaults); Event("handler-fault", new JsonObject { ["exception"] = e.ToString() }); };
        robot.State.StateUpdated += s =>
        {
            double t = Now();
            _states.Enqueue(new StateRec(t, s));
            Event("robot-state", new JsonObject
            {
                ["ts"] = s.Timestamp, ["x"] = LinkCheck.Num(s.PoseX), ["y"] = LinkCheck.Num(s.PoseY), ["angle"] = LinkCheck.Num(s.PoseAngleRad),
                ["origin"] = s.PoseOriginId, ["frame"] = s.PoseFrameId,
                ["head"] = LinkCheck.Num(s.HeadAngle), ["liftMm"] = LinkCheck.Num(s.LiftHeightMm),
                ["lw"] = LinkCheck.Num(s.LwheelSpeedMmps), ["rw"] = LinkCheck.Num(s.RwheelSpeedMmps),
                ["batt"] = LinkCheck.Num(s.BatteryVoltage), ["status"] = $"0x{s.Status:x}",
            });
        };
        robot.Message += m =>
        {
            double t = Now();
            switch (m)
            {
                case RobotState: case ImageChunk: case ImageImuData: return;
                case AnimationState a:
                    _animStates.Enqueue(new AnimStateRec(t, a));
                    Event("anim-state", new JsonObject
                    {
                        ["ts"] = a.Timestamp, ["animBytes"] = a.NumAnimBytesPlayed, ["audioFrames"] = a.NumAudioFramesPlayed,
                        ["tracks"] = $"0x{a.EnabledAnimTracks:x2}", ["tag"] = a.Tag, ["drops"] = a.ClientDropCount,
                    });
                    return;
                case ObjectAccel oa:
                    _objectAccel.Enqueue((t, oa.ObjectID));
                    return;
            }
            string text;
            try { text = m.ToString() ?? m.Id.ToString(); } catch { text = m.Id.ToString(); }
            Event("robot-message", new JsonObject { ["tag"] = $"0x{(byte)m.Id:X2}", ["id"] = m.Id.ToString(), ["text"] = text.Length > 300 ? text[..300] : text });
        };
        robot.Camera.FrameReceived += f => _camFrames.Enqueue(new CamRec(Now(), f));
        robot.Camera.FrameDropped += (id, why) => Event("camera-dropped", new JsonObject { ["imageId"] = id, ["why"] = why });
        robot.Cubes.CubeDiscovered += c => Event("cube-discovered", CubeJson(c));
        robot.Cubes.ConnectionChanged += c => Event("cube-connection", CubeJson(c));
        robot.Sensors.CliffDetected += c => Event("cliff", new JsonObject { ["text"] = c.ToString() });
        robot.Sensors.MotorCalibrationReported += c => Event("motor-calibration", new JsonObject { ["motor"] = c.MotorID.ToString(), ["started"] = c.CalibStarted, ["auto"] = c.AutoStarted });
    }

    private void LoadAnimations(CozmoRobot robot)
    {
        if (_obb is null)
        {
            lock (_gate) { _animInfo["loaded"] = false; _animInfo["why"] = _noObb ? "--no-obb was given" : "no --obb directory was given or found"; }
            return;
        }
        var assets = ControlCheck.AnimationAssets(_obb);
        lock (_gate) _animInfo["assets"] = assets;
        try
        {
            var sw = Stopwatch.StartNew();
            var lib = robot.Animations.LoadFrom(assets);
            lock (_gate)
            {
                _animInfo["loaded"] = true;
                _animInfo["clips"] = lib.ClipNames.Count;
                _animInfo["loadMs"] = sw.ElapsedMilliseconds;
                _animInfo["audioSource"] = "none: audio keyframes stream silence (the Wwise decoder is not loaded by this run)";
            }
        }
        catch (Exception e)
        {
            lock (_gate) { _animInfo["loaded"] = false; _animInfo["why"] = e.GetType().Name + ": " + e.Message; }
        }
    }

    private async Task RunCheck(CheckRec c, Func<Task> body, bool alwaysRun = false)
    {
        if (c.Skipped && !alwaysRun) return;
        if (double.IsNaN(c.StartMs)) c.StartMs = Now();
        try { await body(); }
        catch (Exception e)
        {
            lock (_exceptions) _exceptions.Add($"{c.Def.Id}: {e}");
            c.Crit("no exception", "the check ran to the end without an exception", e.GetType().Name + ": " + e.Message, false);
        }
        c.EndMs = Now();
        Event("check", new JsonObject { ["id"] = c.Def.Id, ["status"] = c.Status });
        Console.WriteLine($"  {c.Status,-7}  {c.Def.Id}");
    }

    // ------------------------------------------------------------------ CONNECT

    private void Connect(CheckRec c)
    {
        var robot = _robot!;
        double call = Mark("connectCall");
        robot.ConnectToRobot(_ip);
        double deadline = call + ControlCheck.ConnectBudgetMs;
        double syncT = double.NaN, firstFullT = double.NaN, readyT = double.NaN;
        while (Now() < deadline)
        {
            var r = robot.Engine.Robot;
            double now = Now();
            if (r is not null)
            {
                if (double.IsNaN(syncT) && r.TimeSynced) syncT = now;
                if (double.IsNaN(firstFullT) && r.FirstFullStateHandled) firstFullT = now;
                if (double.IsNaN(readyT) && r.ReadyToStream) readyT = now;
            }
            if (_response is { Result: not RobotConnectionResult.Success }) break;
            if (!double.IsNaN(syncT) && !double.IsNaN(firstFullT) && !double.IsNaN(readyT) && _response is not null) break;
            Thread.Sleep(2);
        }
        Mark("connectDone");
        var resp = _response;
        double Rel(double t) => t - call;
        var firmwareLines = _engineLog.ToArray().Where(l => l.Line.Contains("robot firmware", StringComparison.Ordinal)).Select(l => l.Line).ToList();

        c.Measured["connectionResponseAfterMs"] = double.IsNaN(_responseT) ? null : LinkCheck.Num(Rel(_responseT));
        c.Measured["result"] = resp?.Result.ToString();
        c.Measured["fwVersion"] = resp?.FwVersion;
        c.Measured["serial"] = resp is null ? null : $"0x{resp.SerialNumber:x8}";
        c.Measured["bodyHwVersion"] = resp?.BodyHWVersion;
        c.Measured["bodyColor"] = resp?.BodyColor;
        c.Measured["robotValidated"] = robot.Engine.RobotValidated;
        c.Measured["connectionState"] = robot.Engine.ConnectionState;
        c.Measured["expectedFirmwareVersionFromHeader"] = robot.Engine.ExpectedFirmwareVersion;
        c.Measured["expectedFirmwareTimeFromHeader"] = robot.Engine.ExpectedFirmwareTime;
        c.Measured["timeSyncedAfterMs"] = double.IsNaN(syncT) ? null : LinkCheck.Num(Rel(syncT));
        c.Measured["firstFullStateAfterMs"] = double.IsNaN(firstFullT) ? null : LinkCheck.Num(Rel(firstFullT));
        c.Measured["readyToStreamAfterMs"] = double.IsNaN(readyT) ? null : LinkCheck.Num(Rel(readyT));
        c.Measured["syncTimeAckMessages"] = robot.State.CountOf(RobotMessageId.SyncTimeAck);
        c.Measured["firmwareLogLines"] = new JsonArray(firmwareLines.Select(l => (JsonNode)l).ToArray());
        c.Measured["animationStreamingOpen"] = robot.Engine.AnimationStreamingOpen;
        c.Measured["handshakeSends"] = HandshakeSends(call);
        c.Measured["note"] = "times are host ms after the ConnectToRobot call; the flags are polled every 2 ms";

        bool success = resp?.Result == RobotConnectionResult.Success;
        c.Crit("connectionResponse", "RobotConnectionResponse with Result Success", resp?.Result.ToString(), success);
        c.Crit("firmwareLogged", "the engine logged the robot's firmware version (a \"robot firmware: <v>\" line, G5.7 / policy M1-040)",
            firmwareLines.Count, firmwareLines.Any(l => l.Contains("robot firmware:", StringComparison.Ordinal)));
        c.Crit("syncTimeAck", "SyncTimeAck received: Robot 1 time synced", !double.IsNaN(syncT), !double.IsNaN(syncT));
        c.Crit("firstFullState", "the first full RobotState after time sync handled", !double.IsNaN(firstFullT), !double.IsNaN(firstFullT));
        c.Crit("readyToStream", "ready to stream set", !double.IsNaN(readyT), !double.IsNaN(readyT));
        double last = new[] { _responseT, syncT, firstFullT, readyT }.Any(double.IsNaN) ? double.NaN : new[] { _responseT, syncT, firstFullT, readyT }.Max();
        c.Crit("within10s", $"all of the above within {ControlCheck.ConnectBudgetMs} ms of the ConnectToRobot call", LinkCheck.Num(double.IsNaN(last) ? double.NaN : Rel(last)),
            !double.IsNaN(last) && Rel(last) <= ControlCheck.ConnectBudgetMs);
        if (resp is not null && resp.FwVersion != ControlCheck.ShippedFirmware)
            c.Warn($"robot firmware {resp.FwVersion} is not {ControlCheck.ShippedFirmware}, the build the protocol was recovered from (policy M1-040: accepted)");
        c.Note = "Connected with CozmoRobot.Create, the ConnectToRobot game message and the engine's ConnectionResponse event: the body of CozmoRobot.ConnectAsync, done step by step so the handshake frames are in frames.jsonl.";

        var fwJson = robot.State.Firmware?.SignatureJson;
        var fwMsg = robot.State.Firmware?.Version;
        lock (_gate)
        {
            _robotInfo["fwVersion"] = resp?.FwVersion;
            _robotInfo["fwIsShipped2381"] = resp is null ? null : resp.FwVersion == ControlCheck.ShippedFirmware;
            _robotInfo["serial"] = resp is null ? null : $"0x{resp.SerialNumber:x8}";
            _robotInfo["bodyHwVersion"] = resp?.BodyHWVersion;
            _robotInfo["bodyColor"] = resp?.BodyColor;
            _robotInfo["firmwareVersionJson"] = fwJson;
            _robotInfo["firmwareMessageVersion"] = fwMsg;
            _robotInfo["expectedFirmwareVersionFromHeader"] = robot.Engine.ExpectedFirmwareVersion;
            _robotInfo["expectedFirmwareTimeFromHeader"] = robot.Engine.ExpectedFirmwareTime;
            _robotInfo["firmwareWarning"] = c.FirstWarning;
        }
    }

    /// <summary>The first sends of the messages the handshake and post-connect path put out, in order, with their times after the connect call.</summary>
    private JsonArray HandshakeSends(double call)
    {
        var watch = new HashSet<byte>
        {
            (byte)RobotMessageId.GetMfgInfo, (byte)RobotMessageId.SyncTime, (byte)RobotMessageId.InitAnimController,
            (byte)RobotMessageId.ImageRequest, (byte)new SetAppRunID().Id, (byte)new RequestCrashReports().Id,
            (byte)RobotMessageId.SetAudioVolume, (byte)RobotMessageId.SetPropSlot,
        };
        var arr = new JsonArray();
        foreach (var s in FirstSends().Where(s => s.T >= call && watch.Contains(s.Tag)).Take(40))
        {
            var o = new JsonObject { ["afterMs"] = LinkCheck.Num(s.T - call), ["tag"] = $"0x{s.Tag:X2}", ["msg"] = TagName(s.Tag) };
            if (s.Tag == (byte)RobotMessageId.ImageRequest && TryParse(s.Payload) is ImageRequest ir) { o["mode"] = ir.Mode.ToString(); o["resolution"] = ir.ImageResolution; }
            arr.Add(o);
        }
        return arr;
    }

    // ------------------------------------------------------------------ STATE

    private void StateCheck(CheckRec c)
    {
        var robot = _robot!;
        double from = Mark("stateStart");
        int disconnectsBefore = _events.Count(e => e.Kind is "robot-disconnected" or "transport-disconnected");
        Hold(ControlCheck.StateWindowMs);
        double to = Mark("stateEnd");
        var win = States().Where(s => s.T >= from && s.T < to).ToList();
        var batt = win.Select(s => (double)s.S.BatteryVoltage).ToList();
        var times = win.Select(s => s.T).ToList();
        var robotTs = win.Select(s => (double)s.S.Timestamp).ToList();
        int disconnects = _events.Count(e => e.Kind is "robot-disconnected" or "transport-disconnected") - disconnectsBefore;
        bool timedOut = robot.Transport.TimedOut;
        bool stillConnected = robot.Engine.ConnectionState == 2;
        int animStates = AnimStates().Count(a => a.T >= from && a.T < to);

        double robotRate = ControlCheck.RateFromRobotTimestamps(win.Select(s => s.S.Timestamp).ToList());
        bool onCharger = robot.Sensors.OnCharger || win.Any(s => s.S.Has(RobotStatusFlag.IsOnCharger));

        c.Measured["windowMs"] = LinkCheck.Num(to - from);
        c.Measured["robotStates"] = win.Count;
        c.Measured["hostRatePerSecond"] = LinkCheck.Num(win.Count * 1000.0 / (to - from));
        c.Measured["robotTimestampRateHz"] = LinkCheck.Num(robotRate);
        c.Measured["robotTimestampFirst"] = win.Count == 0 ? null : win[0].S.Timestamp;
        c.Measured["robotTimestampLast"] = win.Count == 0 ? null : win[^1].S.Timestamp;
        c.Measured["batteryVolts"] = LinkCheck.Stats(batt);
        c.Measured["maxHostGapMs"] = LinkCheck.Num(ControlCheck.MaxGap(times));
        c.Measured["maxRobotTimestampGapMs"] = LinkCheck.Num(ControlCheck.MaxGap(robotTs));
        c.Measured["onCharger"] = onCharger;
        c.Measured["animationStatesInWindow"] = animStates;
        c.Measured["note"] = "host times are when the engine's 60 ms tick handed each state to the devices, so states arrive in batches and the host count in 5 s is not the rate; the rate is judged from the robot's own timestamps, and the host count and host rate are recorded, not judged";

        c.Crit("stateRate", $"RobotState rate from the robot's own timestamps, (n-1)*1000/(ts_last-ts_first) over the {ControlCheck.StateWindowMs} ms window, within {ControlCheck.StateRateMinHz:F0}..{ControlCheck.StateRateMaxHz:F0} Hz. "
                            + $"The band is this test's tolerance, not source: no engine source gives the rate; the capture {ControlCheck.StateRateCapture} measured 33.5 Hz",
            LinkCheck.Num(robotRate), !double.IsNaN(robotRate) && robotRate >= ControlCheck.StateRateMinHz && robotRate <= ControlCheck.StateRateMaxHz);
        string battExpected = $"every battery voltage in the window within {ControlCheck.BatteryMinV:F1}..{ControlCheck.BatteryMaxV:F1} V";
        var battMeasured = batt.Count == 0 ? null : new JsonObject { ["min"] = LinkCheck.Num(batt.Min()), ["max"] = LinkCheck.Num(batt.Max()) };
        if (onCharger)
            c.Crit("battery", battExpected, battMeasured, null, "on charger; setup says off the charger");
        else
            c.Crit("battery", battExpected, battMeasured, batt.Count > 0 && batt.Min() >= ControlCheck.BatteryMinV && batt.Max() <= ControlCheck.BatteryMaxV);
        c.Crit("noTimeout", "no transport timeout (timed-out flag clear), no disconnect event in the window, and the engine still connected (state 2)",
            new JsonObject { ["timedOutFlag"] = timedOut, ["disconnectEvents"] = disconnects, ["connectionState"] = robot.Engine.ConnectionState },
            !timedOut && disconnects == 0 && stillConnected);
        if (onCharger) c.Warn("the robot reports being on the charger; the battery reading is then the charger's, so the battery criterion is not judged");
    }

    // ------------------------------------------------------------------ HEAD / LIFT

    private bool WaitForCalibration(CheckRec c)
    {
        var robot = _robot!;
        double from = Now();
        c.StartMs = from;
        bool ok = Poll(() => robot.State.CalibrationComplete, ControlCheck.CalibrationWaitMs);
        if (!ok) c.EndMs = Now();
        c.Prerequisites["motorCalibrationComplete"] = ok;
        c.Prerequisites["calibrationWaitMs"] = LinkCheck.Num(Now() - from);
        c.Prerequisites["calibrationSeen"] = robot.State.CalibrationSeen;
        c.Prerequisites["headCalibrated"] = robot.State.HeadCalibrated;
        c.Prerequisites["liftCalibrated"] = robot.State.LiftCalibrated;
        c.Prerequisites["motorCalibrationMessages"] = robot.State.CountOf(RobotMessageId.MotorCalibration);
        if (!ok)
            c.SkipReason = $"head and lift calibration did not complete within {ControlCheck.CalibrationWaitMs} ms (calibration seen: {robot.State.CalibrationSeen}); motion is gated on it (policy M4-004)";
        return ok;
    }

    /// <summary>Sends one positioning action and watches RobotState for the value to come within tolerance.</summary>
    private async Task<JsonObject> MoveAndWatch(string what, Func<Task<MotionOutcome>> act, Func<RobotState, float> read, float target, float tol, int withinMs)
    {
        double cmd = Now();
        var task = act();
        Poll(() => States().Any(s => s.T >= cmd && Math.Abs(read(s.S) - target) <= tol), withinMs);
        var outcome = await task;
        var after = States().Where(s => s.T >= cmd).ToList();
        var reached = after.FirstOrDefault(s => Math.Abs(read(s.S) - target) <= tol);
        var closest = after.Count == 0 ? null : after.MinBy(s => Math.Abs(read(s.S) - target));
        double? reachedMs = reached is null ? null : reached.T - cmd;
        bool pass = reachedMs is { } r && r <= withinMs;
        return new JsonObject
        {
            ["what"] = what, ["target"] = LinkCheck.Num(target), ["tolerance"] = LinkCheck.Num(tol),
            ["startValue"] = States().LastOrDefault(s => s.T < cmd) is { } b ? LinkCheck.Num(read(b.S)) : null,
            ["reachedAfterMs"] = reachedMs is { } rm ? LinkCheck.Num(rm) : null,
            ["valueWhenReached"] = reached is null ? null : LinkCheck.Num(read(reached.S)),
            ["closestValue"] = closest is null ? null : LinkCheck.Num(read(closest.S)),
            ["lastValue"] = after.Count == 0 ? null : LinkCheck.Num(read(after[^1].S)),
            ["statesAfterCommand"] = after.Count,
            ["action"] = outcome.Result.ToString(), ["actionDetail"] = outcome.Detail,
            ["pass"] = pass,
        };
    }

    private async Task HeadCheck(CheckRec c)
    {
        var m = _robot!.Motion;
        var steps = new JsonArray();
        foreach (var target in new[] { ControlCheck.HeadUpRad, ControlCheck.HeadDownRad })
        {
            var r = await MoveAndWatch($"head to {target:+0.0;-0.0} rad",
                () => m.SetHeadAngleAsync(target, timeout: TimeSpan.FromMilliseconds(ControlCheck.HeadWithinMs)),
                s => s.HeadAngle, target, ControlCheck.HeadTolRad, ControlCheck.HeadWithinMs);
            steps.Add(r);
            c.Crit($"head{(target > 0 ? "Up" : "Down")}", $"RobotState head angle within {ControlCheck.HeadTolRad} rad of {target:+0.0;-0.0} rad within {ControlCheck.HeadWithinMs} ms of the command",
                r["reachedAfterMs"]?.DeepClone(), r["pass"]!.GetValue<bool>());
        }
        c.Measured["steps"] = steps;
        c.Note = "commanded with CozmoMotion.SetHeadAngleAsync (SetHeadAngle with the engine's default speed and acceleration); the robot's MotorActionAck is reported, not judged";
    }

    private async Task LiftCheck(CheckRec c)
    {
        var m = _robot!.Motion;
        var steps = new JsonArray();
        foreach (var target in new[] { ControlCheck.LiftUpMm, ControlCheck.LiftDownMm })
        {
            var r = await MoveAndWatch($"lift to {target:F0} mm",
                () => m.SetLiftHeightAsync(target, timeout: TimeSpan.FromMilliseconds(ControlCheck.LiftWithinMs)),
                s => s.LiftHeightMm, target, ControlCheck.LiftTolMm, ControlCheck.LiftWithinMs);
            steps.Add(r);
            c.Crit($"lift{target:F0}mm", $"RobotState lift height (45 + 66 sin(liftAngle)) within {ControlCheck.LiftTolMm} mm of {target:F0} mm within {ControlCheck.LiftWithinMs} ms of the command",
                r["reachedAfterMs"]?.DeepClone(), r["pass"]!.GetValue<bool>());
        }
        c.Measured["steps"] = steps;
        c.Note = "commanded with CozmoMotion.SetLiftHeightAsync; the robot's MotorActionAck is reported, not judged";
    }

    // ------------------------------------------------------------------ DRIVE

    private async Task DriveCheck(CheckRec c)
    {
        var robot = _robot!;
        c.Prerequisites["stopOnCliffEnabledAtMs"] = _marks.TryGetValue("stopOnCliffCall", out var soc) ? LinkCheck.Num(soc) : null;
        c.Prerequisites["stopOnCliffSent"] = FirstSends().Any(s => s.Tag == (byte)RobotMessageId.EnableStopOnCliff);
        var legs = new JsonArray();
        foreach (var (name, sign) in new[] { ("forward", 1f), ("back", -1f) })
        {
            var before = States().LastOrDefault();
            double cmd = Now();
            float v = sign * ControlCheck.DriveSpeedMmps;
            var drive = robot.Motion.DriveWheelsAsync(v, v, confirmWithin: TimeSpan.FromMilliseconds(ControlCheck.DriveLegMs));
            Hold(ControlCheck.DriveLegMs);
            double stopCmd = Now();
            var stop = await robot.Motion.StopWheelsAsync(TimeSpan.FromMilliseconds(ControlCheck.WheelStopWithinMs));
            var driveOutcome = await drive;
            Poll(() => StoppedAfter(stopCmd) is not null, ControlCheck.WheelStopWithinMs);
            var stopped = StoppedAfter(stopCmd);
            Hold(ControlCheck.DriveSettleMs);
            var after = States().LastOrDefault();
            double along = before is null || after is null ? double.NaN
                : ControlCheck.AlongHeading(before.S.PoseX, before.S.PoseY, before.S.PoseAngleRad, after.S.PoseX, after.S.PoseY);
            bool originSame = before is not null && after is not null && before.S.PoseOriginId == after.S.PoseOriginId;
            // No record covers the robot keeping PoseFrameId while it drives; if it changes, the distance is not judged.
            var frameIds = before is null ? new List<uint>()
                : States().Where(s => s.T >= before.T && (after is null || s.T <= after.T)).Select(s => s.S.PoseFrameId).Distinct().ToList();
            bool frameChanged = before is not null && frameIds.Any(f => f != before.S.PoseFrameId);
            var peak = States().Where(s => s.T >= cmd && s.T <= stopCmd).Select(s => (double)Math.Max(Math.Abs(s.S.LwheelSpeedMmps), Math.Abs(s.S.RwheelSpeedMmps))).DefaultIfEmpty(double.NaN).Max();
            var cliffs = _events.Count(e => e.Kind == "cliff" && e.T >= cmd);
            legs.Add(new JsonObject
            {
                ["leg"] = name, ["speedMmps"] = v, ["commandMs"] = ControlCheck.DriveLegMs,
                ["start"] = before is null ? null : Pose(before.S), ["end"] = after is null ? null : Pose(after.S),
                ["alongStartHeadingMm"] = LinkCheck.Num(along), ["dxMm"] = before is null || after is null ? null : LinkCheck.Num(after.S.PoseX - before.S.PoseX),
                ["poseOriginUnchanged"] = originSame,
                ["poseFrameIdChanged"] = frameChanged,
                ["poseFrameIdsSeen"] = new JsonArray(frameIds.Select(f => (JsonNode)f).ToArray()),
                ["peakWheelSpeedMmps"] = LinkCheck.Num(peak),
                ["driveConfirm"] = driveOutcome.ToString(), ["stopConfirm"] = stop.ToString(),
                ["wheelsStoppedAfterMs"] = stopped is null ? null : LinkCheck.Num(stopped.T - stopCmd),
                ["cliffEvents"] = cliffs,
            });
            bool distOk = sign > 0 ? along >= ControlCheck.DriveMinMm && along <= ControlCheck.DriveMaxMm
                                   : along <= -ControlCheck.DriveMinMm && along >= -ControlCheck.DriveMaxMm;
            string distExpected = sign > 0
                ? $"the pose moved +{ControlCheck.DriveMinMm:F0}..+{ControlCheck.DriveMaxMm:F0} mm along the starting heading, pose origin unchanged"
                : $"the pose moved -{ControlCheck.DriveMaxMm:F0}..-{ControlCheck.DriveMinMm:F0} mm along the starting heading, pose origin unchanged";
            if (frameChanged)
            {
                c.Crit($"{name}Distance", distExpected, LinkCheck.Num(along), null,
                    $"PoseFrameId changed during the {name} leg ({string.Join(", ", frameIds)}); no record says what the robot's pose frame does while driving, so the displacement is not judged");
                c.Warn($"PoseFrameId changed during the {name} leg");
            }
            else c.Crit($"{name}Distance", distExpected, LinkCheck.Num(along), distOk && originSame);
            c.Crit($"{name}WheelsStop", $"after the stop command, a RobotState with both wheel speeds at most {ControlCheck.WheelStoppedMmps} mm/s and AreWheelsMoving clear within {ControlCheck.WheelStopWithinMs} ms",
                stopped is null ? null : LinkCheck.Num(stopped.T - stopCmd), stopped is not null);
            if (cliffs > 0) c.Warn($"{cliffs} cliff event(s) during the {name} leg");
        }
        var all = await robot.Motion.StopAllAsync();
        c.Measured["legs"] = legs;
        c.Measured["finalStopAll"] = all.ToString();
        c.Note = "the spec's \"pose x changes\" is measured as the signed displacement along the heading at the start of each leg (the robot's own pose frame; the stack does not reset it at connect), which equals the x change when the heading is 0. Driven with CozmoMotion.DriveWheelsAsync for 1000 ms, then StopWheelsAsync. "
                 + "Robot-side behaviour no manifest record covers, assumed here and not judged: that DriveWheels with accelerations 0 (the stack's default) makes the robot drive at the commanded speed, and that PoseFrameId stays the same while driving (a change is recorded in poseFrameIdChanged and the leg's distance is then not judged).";
    }

    private StateRec? StoppedAfter(double t) =>
        States().FirstOrDefault(s => s.T >= t && Math.Abs(s.S.LwheelSpeedMmps) <= ControlCheck.WheelStoppedMmps
                                     && Math.Abs(s.S.RwheelSpeedMmps) <= ControlCheck.WheelStoppedMmps && !s.S.Has(RobotStatusFlag.AreWheelsMoving));

    private static JsonObject Pose(RobotState s) => new()
    {
        ["x"] = LinkCheck.Num(s.PoseX), ["y"] = LinkCheck.Num(s.PoseY), ["angleRad"] = LinkCheck.Num(s.PoseAngleRad),
        ["originId"] = s.PoseOriginId, ["frameId"] = s.PoseFrameId, ["robotTs"] = s.Timestamp,
    };

    // ------------------------------------------------------------------ FACE / AUDIO

    private void FaceCheck(CheckRec c)
    {
        var robot = _robot!;
        var image = FaceBitmap.TestPattern();
        var payload = FaceBitmapCodec.Encode(image);
        File.WriteAllText(Path.Combine(_bundle, "face-test-pattern.txt"), image.ToText() + Environment.NewLine);
        var before = LatestAnimState();
        int framesBefore = robot.Display.FramesSent;
        double from = Mark("faceStart");
        robot.Display.Hold(image, TimeSpan.FromMilliseconds(ControlCheck.FaceHoldMs));
        double holdEnd = Mark("faceHoldEnd");
        Hold(ControlCheck.CounterSettleMs);
        var after = AnimStates().LastOrDefault(a => a.T >= holdEnd);
        robot.Display.Clear();
        int sent = robot.Display.FramesSent - framesBefore;

        c.Measured["faceFramesSent"] = sent;
        c.Measured["payloadBytes"] = payload.Length;
        c.Measured["payloadSha256"] = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
        c.Measured["holdMs"] = LinkCheck.Num(holdEnd - from);
        c.Measured["animStateBefore"] = AnimJson(before);
        c.Measured["animStateAfter"] = AnimJson(after);
        c.Measured["image"] = "face-test-pattern.txt (FaceBitmap.TestPattern, 128x32; '#' lit)";

        int? bytesDelta = before is null || after is null ? null : after.A.NumAnimBytesPlayed - before.A.NumAnimBytesPlayed;
        int? dropDelta = before is null || after is null ? null : ControlCheck.ByteCounterDelta(before.A.ClientDropCount, after.A.ClientDropCount);
        c.Crit("animBytesPlayedRises", "AnimationState NumAnimBytesPlayed after the hold (+500 ms) is greater than before it", bytesDelta, bytesDelta is > 0);
        c.Crit("noClientDrops", "AnimationState ClientDropCount unchanged across the hold", dropDelta, dropDelta == 0);
        c.HumanNote = "a test pattern (the art in face-test-pattern.txt: a border, a cross and blocks) appeared on Cozmo's face for about 3 s";
        c.Note = "animBytesPlayedRises assumes robot-side behaviour no manifest record covers: that the robot's NumAnimBytesPlayed counts face-image bytes "
                 + "streamed by CozmoDisplay.Hold, which re-sends the image with no StartOfAnimation before it. A criterion failure may be that assumption, not the face path."
                 + (before is null ? " No AnimationState had been received before the hold, so neither counter could be compared." : "");
    }

    private void AudioCheck(CheckRec c)
    {
        var robot = _robot!;
        var pcm = CozmoAudio.Beeps(ControlCheck.AudioBeeps);
        int frames = CozmoAudio.ToFrames(pcm).Count;
        var before = LatestAnimState();
        int sentBefore = robot.Audio.FramesSent;
        double from = Mark("audioStart");
        robot.Audio.Play(pcm);
        robot.Audio.SendSilence();
        double playEnd = Mark("audioPlayEnd");
        // let the robot finish what is in its buffer: until the played count stops changing
        int last = int.MinValue;
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < ControlCheck.AudioSettleMaxMs)
        {
            Hold(100);
            int now = LatestAnimState()?.A.NumAudioFramesPlayed ?? int.MinValue;
            if (now == last && now != int.MinValue) break;
            last = now;
        }
        double settled = Mark("audioSettled");
        var after = AnimStates().LastOrDefault(a => a.T >= playEnd);

        c.Measured["pcmSamples"] = pcm.Length;
        c.Measured["audioFramesInSound"] = frames;
        c.Measured["audioFramesSent"] = robot.Audio.FramesSent - sentBefore;
        c.Measured["playCallMs"] = LinkCheck.Num(playEnd - from);
        c.Measured["settleMs"] = LinkCheck.Num(settled - playEnd);
        c.Measured["animStateBefore"] = AnimJson(before);
        c.Measured["animStateAfter"] = AnimJson(after);
        c.Measured["sound"] = $"CozmoAudio.Beeps({ControlCheck.AudioBeeps}): two 880 Hz beeps of 0.25 s, each followed by 0.25 s of silence, Anki companding";

        int? played = before is null || after is null ? null : after.A.NumAudioFramesPlayed - before.A.NumAudioFramesPlayed;
        int? dropDelta = before is null || after is null ? null : ControlCheck.ByteCounterDelta(before.A.ClientDropCount, after.A.ClientDropCount);
        c.Crit("audioFramesPlayed", $"AnimationState NumAudioFramesPlayed rises by at least {ControlCheck.AudioMinFramesPlayed} over the play", played, played >= ControlCheck.AudioMinFramesPlayed);
        c.Crit("noClientDrops", "AnimationState ClientDropCount unchanged across the play", dropDelta, dropDelta == 0);
        c.HumanNote = $"you heard exactly {ControlCheck.AudioBeeps} separate beeps, evenly spaced";
        if (before is null) c.Note = "no AnimationState had been received before the play, so neither counter could be compared";
    }

    // ------------------------------------------------------------------ ANIM / ANIM_CANCEL

    private AnimationClip? Clip(CheckRec c, string name)
    {
        var lib = _robot!.Animations.Library;
        c.Prerequisites["animationsLoaded"] = lib is not null;
        string? why, assets; lock (_gate) { why = _animInfo["why"]?.GetValue<string>(); assets = _animInfo["assets"]?.GetValue<string>(); }
        if (lib is null) { c.SkipReason = "no animation assets: " + (why ?? "not loaded"); return null; }
        AnimationClip? clip = null;
        try { if (lib.ClipNames.Contains(name)) clip = lib.GetClip(name); } catch { clip = null; }
        c.Prerequisites["clip"] = name;
        c.Prerequisites["clipFound"] = clip is not null;
        if (clip is null) { c.SkipReason = $"the clip {name} is not in {assets}"; return null; }
        c.Prerequisites["animationStreamingOpen"] = _robot.AnimationStreamingOpen;
        if (!_robot.AnimationStreamingOpen) { c.SkipReason = "the engine's animation streaming gate is not open (time synced, ready to stream and the first full state, CD12)"; return null; }
        return clip;
    }

    private async Task AnimCheck(CheckRec c)
    {
        var robot = _robot!;
        var clip = Clip(c, ControlCheck.AnimClip);
        if (clip is null) return;
        int fired = 0;
        void onFired(Keyframe _) => Interlocked.Increment(ref fired);
        var notImplemented = new List<string>();
        void onNi(string w) { lock (notImplemented) notImplemented.Add(w); }
        robot.Animations.Scheduler.KeyframeFired += onFired;
        robot.Animations.NotImplemented += onNi;
        double play = Mark("animPlay");
        double done = double.NaN;
        AnimationEndReason? reason = null;
        var ticket = robot.Animations.PlayTracked(clip);
        if (ticket is not null)
        {
            _ = ticket.Completion.ContinueWith(_ => done = Now(), TaskContinuationOptions.ExecuteSynchronously);
            int limit = (int)clip.DurationMs + ControlCheck.AnimTimeoutSlackMs;
            if (await Task.WhenAny(ticket.Completion, Task.Delay(limit)) == ticket.Completion) reason = ticket.Completion.Result;
            else { robot.Animations.StopIfCurrent(ticket.Generation); c.Warn($"not completed within {limit} ms; stopped"); }
        }
        Poll(() => !double.IsNaN(done), 200);
        int firedProp = robot.Animations.Scheduler.KeyframesFired;
        Hold(ControlCheck.AnimEndWatchMs);
        double watchEnd = Mark("animWatchEnd");
        robot.Animations.Scheduler.KeyframeFired -= onFired;
        robot.Animations.NotImplemented -= onNi;
        bool ticking = robot.Animations.IsTicking;

        var sends = FirstSends().Where(s => s.T >= play && AnimStreamTags.Contains(s.Tag)).ToList();
        var start = sends.FirstOrDefault(s => s.Tag == StartTag);
        var ends = sends.Where(s => s.Tag == EndTag).ToList();
        double endT = ends.Count > 0 ? ends[0].T : done;
        var audio = sends.Where(s => (s.Tag == AudioSampleTag || s.Tag == AudioSilenceTag) && (start is null || s.T >= start.T) && (double.IsNaN(endT) || s.T <= endT)).Select(s => s.T).ToList();
        double maxGap = ControlCheck.MaxGap(audio);
        double wall = start is null || double.IsNaN(endT) ? double.NaN : endT - start.T;
        var late = double.IsNaN(endT) ? sends : sends.Where(s => s.T > endT + ControlCheck.AnimEndGraceMs).ToList();
        var robotTags = AnimStates().Where(a => a.T >= play && a.T <= watchEnd).Select(a => (int)a.A.Tag).Distinct().ToList();

        c.Measured["clip"] = clip.Name;
        c.Measured["clipDurationMs"] = clip.DurationMs;
        c.Measured["clipKeyframes"] = clip.Keyframes.Count;
        c.Measured["clipTracks"] = clip.Tracks.ToString();
        c.Measured["bodyKeyframes"] = clip.Keyframes.Count(k => k is BodyKeyframe);
        c.Measured["keyframesFired"] = firedProp;
        c.Measured["keyframeFiredEvents"] = fired;
        c.Measured["endReason"] = reason?.ToString();
        c.Measured["playToCompletionMs"] = double.IsNaN(done) ? null : LinkCheck.Num(done - play);
        c.Measured["startOfAnimationTag"] = start is { Payload.Length: > 1 } ? JsonValue.Create(start.Payload[1]) : null;
        c.Measured["startOfAnimationAfterPlayMs"] = start is null ? null : LinkCheck.Num(start.T - play);
        c.Measured["endOfAnimationSends"] = ends.Count;
        c.Measured["streamWallMs"] = LinkCheck.Num(wall);
        c.Measured["audioFramesStreamed"] = audio.Count;
        c.Measured["maxAudioFrameGapMs"] = LinkCheck.Num(maxGap);
        c.Measured["animMessagesAfterEnd"] = Histogram(late.Select(s => TagName(s.Tag)));
        c.Measured["tickingAfterWatch"] = ticking;
        c.Measured["robotReportedTags"] = new JsonArray(robotTags.Select(t => (JsonNode)t).ToArray());
        lock (notImplemented) c.Measured["notImplemented"] = Histogram(notImplemented);
        lock (_gate) c.Measured["audioSource"] = _animInfo["audioSource"]?.DeepClone();

        c.Crit("completed", "the animation ended with AnimationEndReason.Completed", reason?.ToString(), reason == AnimationEndReason.Completed);
        c.Crit("keyframesFired", $"keyframes fired == the clip's keyframes ({clip.Keyframes.Count})", firedProp, firedProp == clip.Keyframes.Count && fired == clip.Keyframes.Count);
        c.Crit("noStall", $"no stall: no gap over {ControlCheck.AnimStallGapMs} ms between consecutive animation audio frames sent, and StartOfAnimation to EndOfAnimation within the clip's {clip.DurationMs} ms + {ControlCheck.AnimCompletionSlackMs} ms",
            new JsonObject { ["maxAudioFrameGapMs"] = LinkCheck.Num(maxGap), ["streamWallMs"] = LinkCheck.Num(wall) },
            !double.IsNaN(maxGap) && maxGap <= ControlCheck.AnimStallGapMs && !double.IsNaN(wall) && wall <= clip.DurationMs + ControlCheck.AnimCompletionSlackMs);
        c.Crit("streamingStops", $"no animation message (audio, face, head, lift, body, start/end) first sent more than {ControlCheck.AnimEndGraceMs} ms after the EndOfAnimation, over a {ControlCheck.AnimEndWatchMs} ms watch; the animation loop no longer ticking",
            new JsonObject { ["lateMessages"] = late.Count, ["ticking"] = ticking, ["endOfAnimationSends"] = ends.Count },
            !double.IsNaN(endT) && late.Count == 0 && !ticking && ends.Count == 1);
        c.Note = "the spec's \"no stall reported\" has no stall report in the public API; it is measured as the largest gap between the animation's audio frames on the wire and the stream's wall duration. The clip has a body keyframe (a short backward roll). "
                 + "keyframesFired, noStall and streamingStops are judged from the stack's own side: the scheduler's keyframe count and the messages the stack sent. "
                 + "The robot-side evidence (robotReportedTags, the AnimationState stream in events.jsonl) is recorded, not judged.";
    }

    private async Task CancelCheck(CheckRec c)
    {
        var robot = _robot!;
        var clip = Clip(c, ControlCheck.CancelClip);
        if (clip is null) return;
        double play = Mark("cancelPlay");
        var ticket = robot.Animations.PlayTracked(clip);
        if (ticket is null) { c.Crit("started", "the scheduler accepted the clip", false, false); return; }
        Hold(ControlCheck.CancelAfterMs);
        double cancel = Mark("cancelCall");
        bool stopped = robot.Animations.StopIfCurrent(ticket.Generation);
        AnimationEndReason? reason = await Task.WhenAny(ticket.Completion, Task.Delay(1000)) == ticket.Completion ? ticket.Completion.Result : null;
        Hold(ControlCheck.CancelWatchMs);
        double end = Mark("cancelWatchEnd");
        bool ticking = robot.Animations.IsTicking;

        var sends = FirstSends().Where(s => s.T >= play && AnimStreamTags.Contains(s.Tag)).ToList();
        var beforeCancel = sends.Where(s => s.T <= cancel).ToList();
        var late = sends.Where(s => s.T > cancel + ControlCheck.CancelGraceMs).ToList();
        var afterCancel = sends.Where(s => s.T > cancel).ToList();

        c.Measured["clip"] = clip.Name;
        c.Measured["clipDurationMs"] = clip.DurationMs;
        c.Measured["clipTracks"] = clip.Tracks.ToString();
        c.Measured["cancelAfterPlayMs"] = LinkCheck.Num(cancel - play);
        c.Measured["stopIfCurrent"] = stopped;
        c.Measured["endReason"] = reason?.ToString();
        c.Measured["animMessagesBeforeCancel"] = Histogram(beforeCancel.Select(s => TagName(s.Tag)));
        c.Measured["animMessagesAfterCancel"] = Histogram(afterCancel.Select(s => TagName(s.Tag)));
        c.Measured["lastAnimMessageAfterCancelMs"] = afterCancel.Count == 0 ? null : LinkCheck.Num(afterCancel.Max(s => s.T) - cancel);
        c.Measured["endOfAnimationAfterCancel"] = afterCancel.Count(s => s.Tag == EndTag);
        c.Measured["watchMs"] = LinkCheck.Num(end - cancel);
        c.Measured["tickingAfterWatch"] = ticking;

        c.Crit("streamedBeforeCancel", "the animation was streaming before the cancel: StartOfAnimation and at least one audio frame sent", beforeCancel.Count,
            beforeCancel.Any(s => s.Tag == StartTag) && beforeCancel.Any(s => s.Tag == AudioSampleTag || s.Tag == AudioSilenceTag));
        c.Crit("cancelled", "the animation ended with AnimationEndReason.Cancelled", reason?.ToString(), reason == AnimationEndReason.Cancelled);
        c.Crit("nothingAfterCancel", $"no animation message first sent more than {ControlCheck.CancelGraceMs} ms after the cancel, over a {ControlCheck.CancelWatchMs} ms watch", late.Count, late.Count == 0);
        c.Note = "the scheduler's reading of the engine's Abort (M5-023) sends nothing on a cancel, not even EndOfAnimation; endOfAnimationAfterCancel reports it, unjudged. The clip has face and audio tracks only.";
    }

    // ------------------------------------------------------------------ CUBES

    private async Task CubesCheck(CheckRec c)
    {
        var robot = _robot!;
        double from = Mark("cubesStart");
        bool poolEnabled = robot.Cubes.Connections.AutoBlockPoolEnabled;
        c.Crit("blockPoolEnabled", "the auto block pool was enabled after the Success response (policy M1-042)", poolEnabled, poolEnabled);
        var connected = await robot.Cubes.WaitForConnectionAsync(TimeSpan.FromMilliseconds(ControlCheck.CubeWaitMs));
        double waited = Now() - from;
        var heard = robot.Cubes.DiscoveredCubes;
        var slotRequests = robot.Cubes.Connections.SlotRequestsSent;
        c.Measured["waitedMs"] = LinkCheck.Num(waited);
        c.Measured["cubesHeard"] = new JsonArray(heard.Select(x => (JsonNode)CubeJson(x)).ToArray());
        c.Measured["setPropSlotSent"] = new JsonArray(slotRequests.Select(x => (JsonNode)new JsonObject { ["factoryId"] = $"0x{x.FactoryId:x8}", ["slot"] = x.Slot }).ToArray());
        c.Measured["slots"] = new JsonArray(robot.Cubes.Connections.Slots.Select(s => (JsonNode)new JsonObject { ["slot"] = s.Slot, ["factoryId"] = $"0x{s.FactoryId:x8}", ["type"] = s.Type.ToString(), ["state"] = s.State.ToString() }).ToArray());
        c.Measured["charger"] = robot.Cubes.Charger is { } ch ? CubeJson(ch) : null;
        if (heard.Count == 0 && connected is null)
        {
            c.ClearCriteria();
            c.Measured["blockPoolEnabled"] = poolEnabled;
            c.SkipReason = $"no cube heard within {ControlCheck.CubeWaitMs} ms";
            if (!poolEnabled) { c.SkipReason = null; c.Crit("blockPoolEnabled", "the auto block pool was enabled after the Success response (policy M1-042)", false, false); }
            return;
        }
        c.Crit("cubeHeard", "at least one light cube heard (ObjectAvailable or ObjectConnectionState)", heard.Count, heard.Count >= 1);
        c.Crit("cubeConnected", $"one cube reported Connected by ObjectConnectionState within {ControlCheck.CubeWaitMs} ms", connected is null ? null : $"0x{connected.FactoryId:x8}", connected is not null);
        if (connected?.ObjectId is not { } oid)
        {
            c.Crit("accelStream", "its accelerometer stream arrives", null, false);
            return;
        }
        var listener = new CubeShakeListener(CubeShakeListener.SingingFilterCoefficient, CubeShakeListener.SingingLowThreshold, CubeShakeListener.SingingHighThreshold, _ => { });
        double accelFrom = Mark("cubeAccelStart");
        robot.CubeAccel.AddListener(oid, listener);
        Hold(ControlCheck.CubeAccelWindowMs);
        robot.CubeAccel.RemoveListener(oid, listener);
        double accelTo = Mark("cubeAccelEnd");
        var accel = _objectAccel.ToArray().Where(a => a.ObjectId == oid && a.T >= accelFrom && a.T <= accelTo).ToList();
        c.Measured["objectId"] = oid;
        c.Measured["objectAccelMessages"] = accel.Count;
        c.Measured["firstObjectAccelAfterMs"] = accel.Count == 0 ? null : LinkCheck.Num(accel[0].T - accelFrom);
        c.Measured["objectAccelPerSecond"] = LinkCheck.Num(accel.Count * 1000.0 / (accelTo - accelFrom));
        c.Crit("accelStream", $"at least {ControlCheck.CubeAccelMin} ObjectAccel for that cube in the {ControlCheck.CubeAccelWindowMs} ms after StreamObjectAccel on (CubeAccelStreams.AddListener)",
            accel.Count, accel.Count >= ControlCheck.CubeAccelMin);
        c.Note = "the accelerometer criterion is a presence test (at least one message): the stream's rate is not in any record";
    }

    private static JsonObject CubeJson(Cube c) => new()
    {
        ["factoryId"] = $"0x{c.FactoryId:x8}", ["objectId"] = c.ObjectId, ["type"] = c.Type.ToString(), ["rssi"] = c.Rssi,
        ["connected"] = c.Connected, ["advertisements"] = c.Advertisements, ["batteryLevelRaw"] = c.BatteryLevelRaw,
    };

    // ------------------------------------------------------------------ CAMERA

    private void CameraCheck(CheckRec c)
    {
        var robot = _robot!;
        double from = Mark("cameraStart");
        lock (_gate) _cameraWindow = (from, from + ControlCheck.CameraWindowMs);
        int droppedBefore = robot.Camera.FramesDropped;
        Hold(ControlCheck.CameraWindowMs);
        double to = Mark("cameraEnd");
        lock (_gate) _cameraWindow = (from, to);
        var win = _camFrames.ToArray().Where(f => f.T >= from && f.T < to).ToList();
        // Colour-flagged frames are left out of the geometry verdict: the colour format is HARDWARE_ONLY (M3-016), so
        // their decoded geometry goes only into the observation, with no verdict.
        int decodeFail = 0, wrongGeometry = 0, colourFlagged = 0, threeComponent = 0, colourDecodeFail = 0;
        var geometries = new List<string>();
        var colourGeometries = new List<string>();
        foreach (var f in win)
        {
            bool colour = f.F.IsColor;
            if (colour) colourFlagged++;
            try
            {
                var img = ImageResult.FromMemory(f.F.Jpeg, ColorComponents.Default);
                string geo = $"{img.Width}x{img.Height}x{(int)img.SourceComp}";
                if ((int)img.SourceComp == 3) threeComponent++;
                if (colour) { colourGeometries.Add($"{geo} (reported {f.F.Width}x{f.F.Height}, jpeg width {f.F.JpegWidth})"); continue; }
                geometries.Add(geo);
                if (img.Width != ControlCheck.CameraWidth || img.Height != ControlCheck.CameraHeight || f.F.Width != ControlCheck.CameraWidth || f.F.Height != ControlCheck.CameraHeight) wrongGeometry++;
            }
            catch
            {
                if (colour) { colourDecodeFail++; colourGeometries.Add("did not decode"); }
                else decodeFail++;
            }
        }
        int judgedFrames = win.Count - colourFlagged;
        double fps = win.Count * 1000.0 / (to - from);
        // Save three: the first frames in the window past the warm-up, else the first three.
        var toSave = win.Where(f => !f.F.IsWarmUp).Take(ControlCheck.CameraSaved).ToList();
        if (toSave.Count < ControlCheck.CameraSaved) toSave = win.Take(ControlCheck.CameraSaved).ToList();
        var saved = new JsonArray();
        foreach (var f in toSave)
        {
            var name = $"camera-{f.F.ImageId:D5}.jpg";
            File.WriteAllBytes(Path.Combine(_bundle, name), f.F.Jpeg);
            lock (_gate) _savedImages.Add(name);
            saved.Add(new JsonObject { ["file"] = name, ["imageId"] = f.F.ImageId, ["bytes"] = f.F.Jpeg.Length, ["colourFlag"] = f.F.IsColor, ["channelSpread"] = LinkCheck.Num(ChannelSpread(f.F.Jpeg)) });
        }
        var imageReq = FirstSends().Where(s => s.Tag == (byte)RobotMessageId.ImageRequest).Select(s => TryParse(s.Payload) as ImageRequest).Where(r => r is not null).ToList();

        c.Measured["windowMs"] = LinkCheck.Num(to - from);
        c.Measured["completeFrames"] = win.Count;
        c.Measured["framesPerSecond"] = LinkCheck.Num(fps);
        c.Measured["framesJudgedForGeometry"] = judgedFrames;
        c.Measured["colourFlaggedFramesLeftOut"] = colourFlagged;
        c.Measured["framesDroppedInWindow"] = robot.Camera.FramesDropped - droppedBefore;
        c.Measured["warmUpFramesInWindow"] = win.Count(f => f.F.IsWarmUp);
        c.Measured["decodeFailures"] = decodeFail;
        c.Measured["wrongGeometry"] = wrongGeometry;
        c.Measured["decodedGeometriesUnflaggedFrames"] = Histogram(geometries);
        c.Measured["framesSinceConnect"] = robot.Camera.FramesCompleted;
        c.Measured["imageRequestsSent"] = new JsonArray(imageReq.Select(r => (JsonNode)$"{r!.Mode} res {r.ImageResolution}").ToArray());
        c.Measured["saved"] = saved;
        c.Measured["decoder"] = "StbImageSharp ImageResult.FromMemory on the reconstructed JPEG (CameraFrame.Jpeg)";

        c.Crit("frames", $"at least {ControlCheck.CameraMinFrames} complete frames reassembled in {ControlCheck.CameraWindowMs} ms. "
                         + "The threshold is the test spec's, not source: no record gives the stream's frame rate; the measured rate is framesPerSecond",
            new JsonObject { ["frames"] = win.Count, ["framesPerSecond"] = LinkCheck.Num(fps) }, win.Count >= ControlCheck.CameraMinFrames);
        string decodesExpected = $"every frame in the window that is not colour-flagged decodes as a JPEG of {ControlCheck.CameraWidth}x{ControlCheck.CameraHeight} with reported resolution {ControlCheck.CameraWidth}x{ControlCheck.CameraHeight}; "
                                 + "colour-flagged frames are left out (M3-016 is HARDWARE_ONLY; their geometry is in the observation)";
        var decodesMeasured = new JsonObject { ["framesJudged"] = judgedFrames, ["decodeFailures"] = decodeFail, ["wrongGeometry"] = wrongGeometry };
        if (judgedFrames == 0 && win.Count > 0)
            c.Crit("decodes", decodesExpected, decodesMeasured, null, "every frame in the window was colour-flagged; none is left to judge");
        else
            c.Crit("decodes", decodesExpected, decodesMeasured, judgedFrames > 0 && decodeFail == 0 && wrongGeometry == 0);
        c.Crit("saved", $"{ControlCheck.CameraSaved} frames saved to the bundle", saved.Count, saved.Count == ControlCheck.CameraSaved);
        c.Observation = new JsonObject
        {
            ["record"] = "M3-016", ["status"] = "HARDWARE_ONLY", ["verdict"] = null,
            ["question"] = "does any frame look colour?",
            ["framesWithColourFlag"] = colourFlagged, ["framesDecodedWithThreeComponents"] = threeComponent,
            ["colourFramesNotDecoded"] = colourDecodeFail,
            ["colourFrameGeometries"] = Histogram(colourGeometries),
            ["savedChannelSpread"] = "mean |R-G| + |G-B| of each saved frame decoded as RGB: 0 for a grey image",
        };
        c.Note = "no camera command is sent by this check: the stream is the ImageRequest {Stream, QVGA} the engine sends after SyncTime (M1-041); this check measures a 3 s window of it";
    }

    private static double ChannelSpread(byte[] jpeg)
    {
        try
        {
            var img = ImageResult.FromMemory(jpeg, ColorComponents.RedGreenBlue);
            long sum = 0; int n = img.Width * img.Height;
            for (int i = 0; i < n; i++)
            {
                int r = img.Data[3 * i], g = img.Data[3 * i + 1], b = img.Data[3 * i + 2];
                sum += Math.Abs(r - g) + Math.Abs(g - b);
            }
            return n == 0 ? double.NaN : sum / (double)n;
        }
        catch { return double.NaN; }
    }

    // ------------------------------------------------------------------ DISCONNECT

    private void DisconnectCheck(CheckRec c, bool judge)
    {
        var robot = _robot!;
        double call = Mark("disposeCall");
        string? ex = null;
        var sw = Stopwatch.StartNew();
        try { robot.Dispose(); } catch (Exception e) { ex = e.ToString(); lock (_exceptions) _exceptions.Add("Dispose: " + e); }
        double returned = Mark("disposeReturned");
        Hold(ControlCheck.AfterDisposeWatchMs);
        double end = Mark("afterDisposeEnd");
        var fr = Frames().Where(f => f.T >= call).OrderBy(f => f.T).ToList();
        var type3 = fr.Where(f => f.Out && HasSub(f, ReliableMessageType.DisconnectRequest)).ToList();
        var first3 = type3.FirstOrDefault();
        var outAfter = first3 is null ? new List<FrameRec>() : fr.Where(f => f.Out && f.T > first3.T).ToList();
        var inAfter = first3 is null ? new List<FrameRec>() : fr.Where(f => !f.Out && f.T > first3.T).ToList();
        var beforeType3 = FirstSends().Where(s => s.T >= call && (first3 is null || s.T <= first3.T)).Select(s => TagName(s.Tag)).ToList();

        c.Measured["disposeMs"] = LinkCheck.Num(returned - call);
        c.Measured["exception"] = ex;
        c.Measured["disconnectRequestFrames"] = type3.Count;
        c.Measured["disconnectRequestAfterDisposeMs"] = first3 is null ? null : LinkCheck.Num(first3.T - call);
        c.Measured["messagesSentBetweenDisposeAndType3"] = new JsonArray(beforeType3.Select(t => (JsonNode)t).ToArray());
        c.Measured["outboundFramesAfterType3"] = outAfter.Count;
        c.Measured["inboundFramesTracedAfterType3"] = inAfter.Count;
        c.Measured["watchMs"] = LinkCheck.Num(end - returned);
        c.Measured["transportState"] = robot.Transport.State.ToString();
        if (!judge) return;
        c.Crit("disconnectRequest", "exactly one outbound frame carrying a DisconnectRequest after Dispose", type3.Count, type3.Count == 1);
        c.Crit("nothingAfter", $"no outbound frame after that DisconnectRequest (watched {ControlCheck.AfterDisposeWatchMs} ms after Dispose returned)", outAfter.Count, first3 is not null && outAfter.Count == 0);
        c.Crit("disposeReturns", $"Dispose returned without an exception within {ControlCheck.DisposeBoundMs} ms", LinkCheck.Num(returned - call), ex is null && returned - call <= ControlCheck.DisposeBoundMs);
        c.HumanNote = "the command returned to the prompt by itself after printing the bundle path (the process cannot observe its own exit)";
        c.Note = "CozmoRobot.Dispose sends StopAllMotors and DriveWheels(0) before the DisconnectRequest (a decision note in PROJECT_STATE.md, not engine behaviour); they are listed in messagesSentBetweenDisposeAndType3";
    }

    // ------------------------------------------------------------------ frames

    /// <summary>Outbound CLAD messages at their first send: reliable ones by sequence id (resends dropped), counted from each ConnectionRequest.</summary>
    private List<Sent> FirstSends()
    {
        var list = new List<Sent>();
        var seen = new HashSet<ushort>();
        foreach (var f in Frames().Where(f => f.Out && f.Frame is not null).OrderBy(f => f.T))
            foreach (var m in f.Frame!.Messages)
            {
                if (m.Type == ReliableMessageType.ConnectionRequest) { seen.Clear(); continue; }
                if (m.Type is not (ReliableMessageType.SingleReliableMessage or ReliableMessageType.SingleUnreliableMessage) || m.Payload.Length == 0) continue;
                if (m.IsReliable && !seen.Add(m.Seq)) continue;
                list.Add(new Sent(f.T, m.Payload[0], m.Payload));
            }
        return list;
    }

    private static RobotMessage? TryParse(byte[] payload)
    {
        try { return RobotMessage.Parse(payload); } catch { return null; }
    }

    private static bool HasSub(FrameRec f, ReliableMessageType type) => f.Frame is { } fr && fr.Messages.Any(m => m.Type == type);

    private static string TagName(byte tag) =>
        MessageCatalog.ById.TryGetValue((RobotMessageId)tag, out var info) ? info.Member : $"0x{tag:X2}";

    private static JsonObject? AnimJson(AnimStateRec? a) => a is null ? null : new JsonObject
    {
        ["t"] = LinkCheck.Num(a.T), ["ts"] = a.A.Timestamp, ["numAnimBytesPlayed"] = a.A.NumAnimBytesPlayed,
        ["numAudioFramesPlayed"] = a.A.NumAudioFramesPlayed, ["clientDropCount"] = a.A.ClientDropCount, ["tag"] = a.A.Tag,
        ["enabledAnimTracks"] = $"0x{a.A.EnabledAnimTracks:x2}",
    };

    private static JsonObject Histogram(IEnumerable<string?> keys)
    {
        var o = new JsonObject();
        foreach (var g in keys.GroupBy(k => k ?? "(null)").OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal))
            o[g.Key] = g.Count();
        return o;
    }

    private JsonObject FrameJson(FrameRec f, (double From, double To)? camera)
    {
        var o = new JsonObject { ["t"] = LinkCheck.Num(f.T), ["utc"] = f.Utc.ToString("o", CultureInfo.InvariantCulture), ["dir"] = f.Out ? "out" : "in", ["len"] = f.Raw.Length };
        bool imageOnly = false;
        if (f.Frame is { } fr)
        {
            o["type"] = (byte)fr.Type; o["typeName"] = fr.Type.ToString();
            o["seqMin"] = fr.SeqMin; o["seqMax"] = fr.SeqMax; o["ack"] = fr.Ack;
            var subs = new JsonArray();
            imageOnly = fr.Messages.Count > 0;
            foreach (var m in fr.Messages)
            {
                var s = new JsonObject { ["type"] = (byte)m.Type, ["name"] = m.Type.ToString(), ["seq"] = m.Seq, ["size"] = m.Payload.Length };
                bool data = m.Type is ReliableMessageType.SingleReliableMessage or ReliableMessageType.SingleUnreliableMessage && m.Payload.Length > 0;
                if (data) { s["tag"] = $"0x{m.Payload[0]:X2}"; s["msg"] = TagName(m.Payload[0]); }
                if (!(data && m.Payload[0] == ImageChunkTag)) imageOnly = false;
                subs.Add(s);
            }
            o["subs"] = subs;
        }
        if (f.Error is not null) o["error"] = f.Error;
        bool inCamera = camera is { } w && f.T >= w.From && f.T <= w.To;
        if (!f.Out && imageOnly && !inCamera) o["hexOmitted"] = "image chunks outside the CAMERA window (the frames saved there are the image evidence)";
        else o["hex"] = Convert.ToHexString(f.Raw);
        return o;
    }

    // ------------------------------------------------------------------ bundle

    private string WriteBundle(string? crashed)
    {
        if (Interlocked.Exchange(ref _written, 1) == 1) return "FAIL (bundle already written)";
        Directory.CreateDirectory(_bundle);
        var frames = Frames().OrderBy(f => f.T).ToArray();
        var events = _events.ToArray().OrderBy(e => e.T).ToArray();
        (string Name, double Start)[] phases; lock (_phases) phases = _phases.ToArray();
        string PhaseAt(double t) { string n = "pre"; foreach (var p in phases) { if (p.Start <= t) n = p.Name; else break; } return n; }
        CheckRec[] checks; lock (_checks) checks = _checks.ToArray();
        string[] ex; lock (_exceptions) ex = _exceptions.ToArray();
        // Everything the main thread may still be changing is copied under the gate.
        JsonObject robotInfo, animInfo; string[] savedImages; (double From, double To)? camera; JsonNode[] checkJson;
        var records = ManifestStatuses();
        var status = records.ToDictionary(r => r!["id"]!.GetValue<string>(), r => r!["status"]!.GetValue<string>());
        string[] statuses;
        lock (_gate)
        {
            robotInfo = (JsonObject)_robotInfo.DeepClone();
            animInfo = (JsonObject)_animInfo.DeepClone();
            savedImages = _savedImages.ToArray();
            camera = _cameraWindow;
            checkJson = checks.Select(c => (JsonNode)c.ToJson(status)).ToArray();
            statuses = checks.Select(c => c.Status).ToArray();
        }

        string overall = crashed is not null ? $"FAIL ({crashed})"
            : statuses.Length > 0 && statuses.All(s => s == "PASS") ? "PASS"
            : statuses.Any(s => s == "FAIL") ? "FAIL"
            : "INCOMPLETE";
        var opts = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

        var result = new JsonObject
        {
            ["test"] = ControlCheck.TestId,
            ["overall"] = overall,
            ["overallRule"] = "PASS only if every check is PASS; FAIL if any check is FAIL; INCOMPLETE if none failed but some were SKIPPED (a skipped check is not passed). A check is PASS when every judged criterion passed; a criterion with pass null was not judged (its notJudged says why), does not enter the status and is listed in the check's unjudgedCriteria. humanNote verdicts do not enter it.",
            ["tool"] = "cozmo-conformance control-check",
            ["robot"] = new JsonObject
            {
                ["ip"] = _ip.ToString(), ["port"] = RobotAddress.RemotePort(false),
                ["fwVersion"] = robotInfo["fwVersion"]?.DeepClone(), ["fwIsShipped2381"] = robotInfo["fwIsShipped2381"]?.DeepClone(),
                ["firmwareWarning"] = robotInfo["firmwareWarning"]?.DeepClone(), ["serial"] = robotInfo["serial"]?.DeepClone(),
            },
            ["options"] = new JsonObject { ["allowDrive"] = _allowDrive, ["obb"] = _obb, ["noObb"] = _noObb, ["yes"] = _yes },
            ["provenance"] = "hardware verification only: a PASS does not raise the provenance or status of any cited record (AGENTS.md: hardware success does not upgrade provenance). The manager judges the bundle.",
            ["checks"] = new JsonArray(checkJson),
            ["hardwareOnlyUncertainty"] = new JsonArray(ControlCheck.HardwareOnly.Select(h => (JsonNode)new JsonObject { ["id"] = h.Id, ["status"] = status.GetValueOrDefault(h.Id, "?"), ["note"] = h.Note }).ToArray()),
            ["reports"] = new JsonObject
            {
                ["judged"] = false,
                ["handlerFaults"] = Volatile.Read(ref _handlerFaults),
                ["exceptions"] = new JsonArray(ex.Select(x => (JsonNode)x).ToArray()),
                ["transportWarnings"] = Histogram(events.Where(e => e.Kind == "transport-warning").Select(e => WarningKind(e.Data["text"]?.GetValue<string>() ?? ""))),
                ["engineWarnings"] = new JsonArray(events.Where(e => e.Kind == "engine-log" && (e.Data["line"]?.GetValue<string>() ?? "").StartsWith("warning", StringComparison.Ordinal))
                    .Select(e => e.Data["line"]!.GetValue<string>()).Distinct().Take(40).Select(l => (JsonNode)l).ToArray()),
                ["framesOut"] = frames.Count(f => f.Out), ["framesIn"] = frames.Count(f => !f.Out),
            },
            ["records"] = records,
            ["animation"] = animInfo.DeepClone(),
            ["savedImages"] = new JsonArray(savedImages.Select(s => (JsonNode)s).ToArray()),
            ["phases"] = new JsonArray(phases.Select(p => (JsonNode)new JsonObject { ["name"] = p.Name, ["startMs"] = LinkCheck.Num(p.Start) }).ToArray()),
            ["marksMs"] = MarksJson(),
        };
        // Each file on its own: one that fails is reported and the rest are still written.
        var failures = new List<string>();
        void Write(string file, Action<string> write)
        {
            try { write(Path.Combine(_bundle, file)); }
            catch (Exception e)
            {
                failures.Add(file);
                Console.Error.WriteLine($"control-check: could not write {Path.Combine(_bundle, file)}: {e}");
            }
        }
        Write("result.json", p => File.WriteAllText(p, result.ToJsonString(opts)));
        Write("frames.jsonl", p =>
        {
            using var w = new StreamWriter(p);
            foreach (var f in frames) { var o = FrameJson(f, camera); o["phase"] = PhaseAt(f.T); w.WriteLine(o.ToJsonString()); }
        });
        Write("events.jsonl", p =>
        {
            using var w = new StreamWriter(p);
            foreach (var e in events)
            {
                var o = new JsonObject { ["t"] = LinkCheck.Num(e.T), ["phase"] = PhaseAt(e.T), ["kind"] = e.Kind };
                foreach (var kv in e.Data) o[kv.Key] = kv.Value?.DeepClone();
                w.WriteLine(o.ToJsonString());
            }
        });
        Write("env.json", p => File.WriteAllText(p, Env(robotInfo, animInfo).ToJsonString(opts)));
        Write("README.md", p => File.WriteAllText(p, Readme(overall, checks, robotInfo, savedImages)));
        if (failures.Count > 0) Console.Error.WriteLine($"control-check: the bundle is incomplete: {string.Join(", ", failures)} not written");
        return overall;
    }

    private JsonObject MarksJson()
    {
        lock (_marks) return new JsonObject(_marks.Select(kv => KeyValuePair.Create(kv.Key, LinkCheck.Num(kv.Value))));
    }

    private static string WarningKind(string w)
    {
        int cut = w.IndexOfAny(new[] { ':', ';' });
        var head = cut > 0 ? w[..cut] : w;
        return head.Length > 60 ? head[..60] : head;
    }

    private JsonArray ManifestStatuses()
    {
        var arr = new JsonArray();
        Dictionary<string, string>? byId = null;
        try
        {
            if (_repoRoot is not null)
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(_repoRoot, "re-analysis", "fidelity_manifest.json")));
                byId = doc.RootElement.GetProperty("records").EnumerateArray()
                    .ToDictionary(r => r.GetProperty("id").GetString()!, r => r.GetProperty("status").GetString()!);
            }
        }
        catch { byId = null; }
        foreach (var id in ControlCheck.CitedRecords)
            arr.Add(new JsonObject { ["id"] = id, ["status"] = byId is null ? "manifest not read" : byId.GetValueOrDefault(id, "MISSING FROM MANIFEST") });
        return arr;
    }

    private JsonObject Env(JsonObject robotInfo, JsonObject animInfo)
    {
        string? head = null, gitStatus = null;
        if (_repoRoot is not null)
        {
            head = RunProcess("git", $"-C \"{_repoRoot}\" rev-parse HEAD")?.Trim();
            gitStatus = RunProcess("git", $"-C \"{_repoRoot}\" status --porcelain");
        }
        var statusLines = gitStatus?.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.TrimEnd('\r')).ToArray();
        string? Src(string rel) => _repoRoot is null ? null : Sha256File(Path.Combine(_repoRoot, "cozmo-stack", "src", rel));
        var sources = new JsonObject();
        foreach (var rel in new[]
        {
            "Cozmo.Conformance/ControlCheck.cs", "Cozmo.Conformance/LinkCheck.cs", "Cozmo.Conformance/Program.cs",
            "Cozmo.Robot/CozmoRobot.cs", "Cozmo.Robot/CozmoEngine.cs", "Cozmo.Robot/Motion.cs", "Cozmo.Robot/Sensors.cs",
            "Cozmo.Robot/Display.cs", "Cozmo.Robot/Audio.cs", "Cozmo.Robot/Camera.cs", "Cozmo.Robot/MiniJpeg.cs",
            "Cozmo.Robot/Cubes.cs", "Cozmo.Robot/CubeConnections.cs", "Cozmo.Robot/CubeAccel.cs",
            "Cozmo.Robot/Animation/CozmoAnimations.cs", "Cozmo.Robot/Animation/AnimationScheduler.cs",
            "Cozmo.Transport/ReliableTransport.cs", "Cozmo.Transport/ReliableConnection.cs", "Cozmo.Transport/TransportConstants.cs",
        })
            sources[rel] = Src(rel.Replace('/', Path.DirectorySeparatorChar));
        return new JsonObject
        {
            ["test"] = ControlCheck.TestId,
            ["commandLine"] = "cozmo-conformance " + string.Join(' ', _argv),
            ["gitHead"] = head,
            ["gitDirty"] = statusLines is null ? null : statusLines.Length > 0,
            ["gitStatus"] = statusLines is null ? null : new JsonArray(statusLines.Take(100).Select(l => (JsonNode)l).ToArray()),
            ["toolSourceSha256"] = sources,
            ["assemblySha256"] = new JsonObject
            {
                ["cozmo-conformance.dll"] = Sha256File(typeof(ControlCheck).Assembly.Location),
                ["Cozmo.Robot.dll"] = Sha256File(typeof(CozmoRobot).Assembly.Location),
                ["Cozmo.Transport.dll"] = Sha256File(typeof(ReliableTransport).Assembly.Location),
                ["Cozmo.Protocol.dll"] = Sha256File(typeof(Frame).Assembly.Location),
            },
            ["robot"] = robotInfo.DeepClone(),
            ["robotFirmwareNote"] = "fwVersion is the RobotConnectionResponse's; firmwareVersionJson is the robot's firmwareVersion message (policy M1-040: logged on every connection, warned when not 2381)",
            ["obb"] = _obb, ["noObb"] = _noObb,
            ["animation"] = animInfo.DeepClone(),
            ["allowDrive"] = _allowDrive,
            ["os"] = RuntimeInformation.OSDescription,
            ["osArchitecture"] = RuntimeInformation.OSArchitecture.ToString(),
            ["dotnet"] = RuntimeInformation.FrameworkDescription,
            ["processId"] = Environment.ProcessId,
            ["robotIp"] = _ip.ToString(), ["robotPort"] = RobotAddress.RemotePort(false),
            ["startLocal"] = _t0Local.ToString("o", CultureInfo.InvariantCulture),
            ["startUtc"] = _t0Utc.ToString("o", CultureInfo.InvariantCulture),
            ["endUtc"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            ["criteria"] = new JsonObject
            {
                ["connectBudgetMs"] = ControlCheck.ConnectBudgetMs, ["calibrationWaitMs"] = ControlCheck.CalibrationWaitMs,
                ["stateWindowMs"] = ControlCheck.StateWindowMs, ["stateRateHz"] = $"{ControlCheck.StateRateMinHz}..{ControlCheck.StateRateMaxHz} (robot timestamps; the test's tolerance, not source)",
                ["batteryV"] = $"{ControlCheck.BatteryMinV}..{ControlCheck.BatteryMaxV}",
                ["headTargetsRad"] = $"{ControlCheck.HeadUpRad}, {ControlCheck.HeadDownRad} +/- {ControlCheck.HeadTolRad} within {ControlCheck.HeadWithinMs} ms",
                ["liftTargetsMm"] = $"{ControlCheck.LiftUpMm}, {ControlCheck.LiftDownMm} +/- {ControlCheck.LiftTolMm} within {ControlCheck.LiftWithinMs} ms",
                ["drive"] = $"{ControlCheck.DriveSpeedMmps} mm/s for {ControlCheck.DriveLegMs} ms each way; {ControlCheck.DriveMinMm}..{ControlCheck.DriveMaxMm} mm; stopped <= {ControlCheck.WheelStoppedMmps} mm/s within {ControlCheck.WheelStopWithinMs} ms",
                ["faceHoldMs"] = ControlCheck.FaceHoldMs, ["audioMinFramesPlayed"] = ControlCheck.AudioMinFramesPlayed,
                ["animStallGapMs"] = ControlCheck.AnimStallGapMs, ["animEndGraceMs"] = ControlCheck.AnimEndGraceMs,
                ["cancelAfterMs"] = ControlCheck.CancelAfterMs, ["cancelGraceMs"] = ControlCheck.CancelGraceMs,
                ["cubeWaitMs"] = ControlCheck.CubeWaitMs, ["cubeAccelWindowMs"] = ControlCheck.CubeAccelWindowMs,
                ["cameraWindowMs"] = ControlCheck.CameraWindowMs, ["cameraMinFrames"] = ControlCheck.CameraMinFrames,
                ["afterDisposeWatchMs"] = ControlCheck.AfterDisposeWatchMs, ["watchdogMs"] = ControlCheck.WatchdogMs,
            },
        };
    }

    private string Readme(string overall, CheckRec[] checks, JsonObject robotInfo, string[] savedImages)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# CONTROL hardware run: {overall}");
        sb.AppendLine();
        sb.AppendLine($"Robot {_ip}:{RobotAddress.RemotePort(false)}, firmware {robotInfo["fwVersion"]?.ToString() ?? "unknown"}{(robotInfo["firmwareWarning"] is { } fw ? $" ({fw})" : "")}, started {_t0Local:yyyy-MM-dd HH:mm:ss zzz}. Command: `cozmo-conformance {string.Join(' ', _argv)}`.");
        sb.AppendLine();
        sb.AppendLine("## What was run");
        sb.AppendLine();
        sb.AppendLine("`control-check` (cozmo-stack/src/Cozmo.Conformance/ControlCheck.cs) connects through the app layer: `CozmoRobot.Create`, the ConnectToRobot game message and the engine's RobotConnectionResponse, which is the body of `CozmoRobot.ConnectAsync` done step by step so the handshake is traced. Then it runs the checks below in order, through the public device APIs only. Stop-on-cliff is enabled right after CONNECT, before anything moves. Each check is judged from telemetry against the numeric criteria in `result.json`.");
        sb.AppendLine();
        sb.AppendLine("| check | status | criteria (all must hold) | human |");
        sb.AppendLine("|---|---|---|---|");
        foreach (var c in checks)
        {
            var crit = string.Join("; ", c.CriteriaExpected());
            var unjudged = c.Unjudged;
            sb.AppendLine($"| {c.Def.Id} | {c.Status}{(c.Skipped ? ": " + c.SkipReason : "")}{(unjudged.Length == 0 ? "" : " (not judged: " + string.Join(", ", unjudged) + ")")} | {crit.Replace("|", "/")} | {c.HumanNote ?? ""} |");
        }
        sb.AppendLine();
        sb.AppendLine("## Files");
        sb.AppendLine();
        sb.AppendLine("- `result.json`: `overall` (PASS only if every check passed; INCOMPLETE if none failed but some were skipped; FAIL otherwise). Each check has `status` (PASS, FAIL, SKIPPED with `skipReason`), `records` (fidelity ids with their manifest status), `prerequisites`, `criteria` (each with `expected`, `measured`, `pass`), `measured`, `warnings`, and, where a person has to judge, `humanNote` with `humanVerdict: null` for the operator to fill in. `observation` (CAMERA) is the M3-016 colour observation, without a verdict. `hardwareOnlyUncertainty` lists the HARDWARE_ONLY records the run depends on.");
        sb.AppendLine("- `frames.jsonl`: every datagram the transport's frame trace reported, both directions: time `t` (ms since start), `phase`, header, sub-messages with CLAD tag and name, and the raw datagram as `hex`. Inbound frames holding only camera image chunks outside the CAMERA window carry `hexOmitted` instead of `hex`, to keep the bundle small.");
        sb.AppendLine("- `events.jsonl`: phases, check verdicts, the engine's log, transport warnings, the connection response, every handled RobotState (pose, head, lift, wheels, battery), every AnimationState, other robot messages, cube, cliff and calibration events.");
        sb.AppendLine("- `env.json`: git HEAD and dirty state, SHA-256 of the tool and the stack sources and of the assemblies that ran, the robot's firmware (connection response and firmwareVersion JSON, policy M1-040), OS, .NET, the fixed criteria.");
        sb.AppendLine($"- `face-test-pattern.txt`: the image FACE showed. {string.Join(", ", savedImages.Select(s => $"`{s}`"))}{(savedImages.Length > 0 ? ": camera frames saved by CAMERA." : "")}");
        sb.AppendLine();
        sb.AppendLine("## Limits of what the bundle shows");
        sb.AppendLine();
        sb.AppendLine("- An outbound time is taken just after sendto returns; a robot message's time is when the engine's 60 ms tick handed it to the devices.");
        sb.AppendLine("- \"First send\" of a reliable message is the first frame carrying its sequence id; resends are not counted as new sends.");
        sb.AppendLine("- ANIM's \"no stall\" and DRIVE's distance are measured as described in each check's `note`; the public API has no stall report, and the pose is the robot's own frame.");
        sb.AppendLine("- The engine's live (keep-alive) animation stream is not exercised by this run.");
        sb.AppendLine("- A PASS is hardware verification. It does not raise the provenance of any record.");
        return sb.ToString();
    }

    private static string? RunProcess(string file, string args)
    {
        try
        {
            var psi = new ProcessStartInfo(file, args) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            using var p = Process.Start(psi);
            if (p is null) return null;
            var output = p.StandardOutput.ReadToEndAsync();
            if (!p.WaitForExit(10000)) { try { p.Kill(); } catch { } return null; }
            return output.Result;
        }
        catch { return null; }
    }

    private static string? Sha256File(string? path)
    {
        try { return path is null || !File.Exists(path) ? null : Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant(); }
        catch { return null; }
    }
}
