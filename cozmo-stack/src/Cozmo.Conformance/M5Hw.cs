using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Cozmo.Transport;

namespace Cozmo.Conformance;

/// <summary>Operator-run observation harness. Expectations: approved M5 inventory and settling pass B's eleven checks.
/// OBSERVED is evidence, never layer acceptance. No production behavior or manifest is changed.</summary>
public static class M5Hw
{
    public const string TestId = "M5-HW";
    public sealed record CheckDef(string Id, string Title, string[] Records);
    public static readonly string[] HardwareOnlyIds = { "M5-036", "M3-008" };
    public static readonly CheckDef[] Checks = {
        new("TAGS", "Three plays: wire start tags and robot echoes", new[]{"M5-008","M5-036"}),
        new("ABORT", "Mid-stream cancellation and robot counters", new[]{"M5-023","M5-036"}),
        new("KEEP_ALIVE", "Neutral replay, live bursts and persistent face stream", new[]{"M5-010","M5-027","M5-028","M5-029","M5-030","M5-036","M5-057"}),
        new("LOCKED_HEAD", "Head keyframes still stream while head track is locked", new[]{"M4-014","M4-047","M5-035","M5-036"}),
        new("AUDIO", "744-byte samples, silence, counters and pacing", new[]{"M5-008","M5-036","M3-012","M6-042"}),
        new("LAYERS", "Face and backpack layer traffic", new[]{"M5-028","M5-029","M5-031","M5-032","M5-036"}),
        new("LIVE_IDLE", "Calibrated head input and default wiggles", new[]{"M5-030","M5-057","M5-036"}),
        new("PARAMETERS", "Uncapped 45000 ms blink maximum over 60 seconds", new[]{"M5-062","M5-028","M5-036","M3-008"}),
        new("DRIVING", "Short DriveToPose and driving animation sequence", new[]{"M5-063","M12-024","M7-026","M15-001","M8-020","M8-021"}),
        new("SLEEP", "Sleep deadline fallback and known action gap", new[]{"M5-038","M1-045","M8-017","M8-020","M8-021"}),
        new("TEARDOWN", "Dispose during clip, cache cleanup and quiet wire", new[]{"M5-037","M5-053","M5-055","M5-059","M5-060"}),
    };
    public static readonly string[] RecordIds = Checks.SelectMany(x => x.Records).Distinct().ToArray();
    public static string? CheckDefinitionProblem() => Checks.SelectMany(x => x.Records).Except(RecordIds).Any() || RecordIds.Except(Checks.SelectMany(x => x.Records)).Any() ? "check/record mismatch" : null;
    public static List<JsonObject> LoadRecords(string path, List<string> warnings)
    {
        var all = JsonNode.Parse(File.ReadAllText(path))!["records"]!.AsArray().Select(x => x!.AsObject()).ToDictionary(x => x["id"]!.GetValue<string>());
        var missing = RecordIds.Where(id => !all.ContainsKey(id)).ToArray();
        if (missing.Length > 0) throw new InvalidOperationException("Missing fidelity records: " + string.Join(", ", missing));
        foreach (var id in RecordIds) if (all[id]["status"]!.GetValue<string>() is "HARDWARE_ONLY" or "IMPLEMENTATION_GAP" or "RECOVERABLE_GAP" or "BLOCKED_EXTERNAL") warnings.Add(id + ": " + all[id]["status"] + " — " + all[id]["unresolved"]);
        return RecordIds.Select(id => (JsonObject)all[id].DeepClone()).ToList();
    }
    public static string NormaliseAnswer(string answer, string[] choices)
    {
        string normal = answer.Trim().Replace(' ', '-').Replace('_', '-');
        string found = M3M4Hw.NormaliseAnswer(normal, choices);
        return choices.Contains(found) ? found : answer.Trim();
    }
    public sealed record ObservationPlan(string Id, string Watch, string Expected);
    public static readonly ObservationPlan[] ObservationPlans = {
        new("CANCEL", "face screen, head and lift", "A shipped clip plays briefly and is cancelled. Watch its activity stop; afterward the face may return to the loaded resting-eye reference. Compare the two rounded eye shapes in neutral-reference.svg. A brief close/reopen is a blink; a shift of eye positions is an eye dart. Those are separate from continued clip activity. No body-driving clip is selected."),
        new("LIVE", "head, lift and wheel/body movement", "Default ProceduralLive may make small head/lift movements and body wiggles. Watch only this ten-second window; the idle will be removed at its end before questions. Face eye darts and blinks are separate observations."),
        new("LOCKED", "head angle", "The head is locked at its starting angle while a shipped head-animation clip plays. Face/lift may animate. Judge the head only between START and END LOCKED; ignore motion before START and after the announced UNLOCK."),
        new("LAYERS", "face screen and five backpack lights separately", "A short distortion pulse may briefly shift or distort the face image and change backpack lights. Face and backpack need not start or finish together. Watch each separately only between START and END LAYERS."),
        new("BLINKS", "face screen", "For sixty seconds, watch the eyes briefly close and reopen (blink). Sideways/up-down eye shifts are darts, not blinks. An old scheduled blink may happen first; later blink spacing is requested at about44-45 seconds. No movement idle is pushed. Report timing you actually saw or choose unclear/not-observed.")
    };
    public sealed record WireMessage(double AtMs, bool Outbound, byte[] Payload,
        int FrameLine = 0, int SubmessageOrdinal = 0, ushort? ReliableSequence = null, int ConnectionEpoch = 0);
    public static JsonObject GradeAbort(IEnumerable<WireMessage> messages, double cancelMs, double quietEndMs)
    {
        var all = M5HwAnalysis.OrderedUnique(messages).ToArray();
        var rows = all.Where(x => x.Outbound && x.AtMs >= cancelMs && x.AtMs <= quietEndMs && x.Payload.Length > 0).ToArray();
        int abortIndex = Array.FindIndex(rows, x => x.Payload[0] == 0x8D);
        var command = abortIndex < 0 ? null : rows[abortIndex];
        var prior = command is null ? null : all.TakeWhile(x => !ReferenceEquals(x, command))
            .LastOrDefault(x => x.Outbound && x.Payload.Length > 0 && x.Payload[0] is 0x9B or 0x9A or 0x8D);
        bool active = prior is not null && prior.Payload.Length == 2 && prior.Payload[0] == 0x9B;
        var backlog = abortIndex < 0 ? rows : rows.Take(abortIndex).ToArray();
        var after = abortIndex < 0 ? Array.Empty<WireMessage>() : rows.Skip(abortIndex + 1).ToArray();
        int audio = after.Count(x => x.Payload[0] is 0x8E or 0x8F), abort = rows.Count(x => x.Payload[0] == 0x8D),
            end = after.Count(x => x.Payload[0] == 0x9A), faces = after.Count(x => x.Payload[0] == (byte)RobotMessageId.AnimFaceImage);
        bool malformed = rows.Any(x => (x.Payload[0] is 0x8D or 0x9A or 0x8F) && x.Payload.Length != 1 || x.Payload[0] == 0x8E && x.Payload.Length != 745);
        return new JsonObject
        {
            ["verdict"] = active && command is not null && !malformed && quietEndMs - command.AtMs >= 400
                && abort == 1 && end == 0 && faces <= 1 && audio <= 1
                && !after.Any(x => x.Payload[0] == 0x9B) ? "OBSERVED_CANCEL_WIRE_SHAPE" : "INCONCLUSIVE",
            ["abortCount"] = abort, ["endCount"] = end, ["faceCount"] = faces, ["audioFrameCount"] = audio,
            ["cancelCallAtMs"] = cancelMs, ["wireAbort"] = command is null ? null : M5HwAnalysis.WireEvidence(new[] { command }),
            ["quietEndMs"] = quietEndMs, ["preCommandBacklog"] = M5HwAnalysis.WireEvidence(backlog),
            ["postAbortMessages"] = M5HwAnalysis.WireEvidence(after),
            ["verdictRule"] = "M5-023 A22/A23/A25: active stream before actual ordered wire Abort, one Abort, "
                + "no End/new Start and at most one audio marker/face in the following 400 ms. Pre-command backlog "
                + "is separate, even in the Abort datagram; reliable retransmissions count once. The native neutral "
                + "gate uses last-stream time, not cancel wall time. Robot counters/echo semantics remain M5-036 HARDWARE_ONLY."
        };
    }
    public static JsonObject GradeTags(IEnumerable<WireMessage> messages)
    {
        var rows = messages.OrderBy(x => x.AtMs).ToArray();
        var startRows = rows.Where(x => x.Outbound && x.Payload.Length == 2 && x.Payload[0] == 0x9B && x.Payload[1] != 255).Take(3).ToArray();
        var starts = startRows.Select(x => (int)x.Payload[1]).ToArray();
        var echoes = rows.Where(x => !x.Outbound && x.Payload.Length == 2 && x.Payload[0] == (byte)RobotMessageId.AnimStarted).ToArray();
        var ends = rows.Where(x => !x.Outbound && x.Payload.Length == 2 && x.Payload[0] == (byte)RobotMessageId.AnimEnded).ToArray();
        bool ordered = startRows.All(s => echoes.Any(e => e.AtMs >= s.AtMs && e.Payload[1] == s.Payload[1] && ends.Any(end => end.AtMs >= e.AtMs && end.Payload[1] == s.Payload[1])));
        return new JsonObject { ["verdict"] = starts.SequenceEqual(new[] { 1, 2, 3 }) && ordered ? "OBSERVED_TAGS_AND_START_ECHOES" : "INCONCLUSIVE", ["starts"] = JsonSerializer.SerializeToNode(starts), ["startedEchoes"] = JsonSerializer.SerializeToNode(echoes.Select(x => (int)x.Payload[1])), ["endedEchoes"] = JsonSerializer.SerializeToNode(ends.Select(x => (int)x.Payload[1])), ["verdictRule"] = "Fresh connection, three plays before another explicit play; M5-008 increment 1,2,3, ordered matching started then ended robot echoes. Idle FF retained separately; firmware meaning is M5-036." };
    }
    public static int Run(string[] args)
    {
        try
        {
            string? root = LinkCheck.FindRepoRoot(); if (root is null) throw new ArgumentException("Run inside the repository.");
            bool validate = false, yes = false, drive = false; string? resources = null, output = null; IPAddress ip = RobotAddress.DefaultFor(false); HashSet<string>? only = null;
            for (int i = 1; i < args.Length; i++) switch (args[i])
                {
                    case "--validate": validate = true; break;
                    case "--yes": yes = true; break;
                    case "--allow-drive": drive = true; break;
                    case "--resources": resources = args[++i]; break;
                    case "--out": output = args[++i]; break;
                    case "--only": only = args[++i].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToHashSet(); break;
                    default: if (!IPAddress.TryParse(args[i], out var parsed)) throw new ArgumentException("Unknown argument " + args[i]); ip = parsed; break;
                }
            if (only is not null && (only.Count == 0 || only.Except(Checks.Select(x => x.Id)).Any())) throw new ArgumentException("Unknown or empty --only check list.");
            var warnings = new List<string>(); var records = LoadRecords(Path.Combine(root, "re-analysis", "fidelity_manifest.json"), warnings);
            if (validate) { Console.WriteLine($"M5-HW: {Checks.Length} checks, {records.Count} valid records; no network opened.\n" + string.Join("\n", warnings)); return 0; }
            var options = EngineStartupResources.OptionsForRoot(resources);
            string bundle = Path.Combine(output ?? Path.Combine(root, "re-analysis", "acceptance", "hardware"), DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-M5-HW-" + Guid.NewGuid().ToString("N")[..6]);
            return new M5Run(root, bundle, ip, options.ResourcesPath!, records, args, only, yes, drive).Execute();
        }
        catch (Exception e) { Console.Error.WriteLine("m5-hw: " + e.Message + "\nusage: m5-hw [robot-ip] --resources <dir> [--only CHECK,...] [--allow-drive] [--yes] [--out <parent>] | --validate"); return 2; }
    }
}

internal sealed class M5Run
{
    readonly string root, bundle, resources; readonly IPAddress ip; readonly List<JsonObject> records; readonly string[] argv; readonly HashSet<string>? only; readonly bool yes, drive;
    readonly Stopwatch clock = Stopwatch.StartNew(); readonly ConcurrentQueue<JsonObject> events = new(); readonly ConcurrentQueue<(double T, bool Out, byte[] Raw, Frame? Frame, string? Error)> frames = new();
    readonly List<JsonObject> results = new(); readonly ConcurrentQueue<string> errors = new(); readonly CancellationTokenSource cancel = new();
    CozmoRobot? robot; RobotConnectionResponse? response; bool disposed, disposeCompleted, connected; string cache = ""; bool cacheCtor;
    JsonObject neutralReference = new();
    JsonObject? activeObservationWindow;
    double Now => clock.Elapsed.TotalMilliseconds;
    public M5Run(string root, string bundle, IPAddress ip, string resources, List<JsonObject> records, string[] argv, HashSet<string>? only, bool yes, bool drive)
    { this.root = root; this.bundle = bundle; this.ip = ip; this.resources = resources; this.records = records; this.argv = argv; this.only = only; this.yes = yes; this.drive = drive; }
    void Event(string kind, JsonObject data) { data["atMs"] = Now; data["kind"] = kind; events.Enqueue(data); }
    void Wait(int ms) { if (cancel.Token.WaitHandle.WaitOne(ms)) cancel.Token.ThrowIfCancellationRequested(); }
    bool Poll(Func<bool> predicate, int ms) { double end = Now + ms; while (Now < end) { if (predicate()) return true; Wait(10); } return predicate(); }
    JsonObject Observe(string note) => new() { ["verdict"] = "OBSERVED_CAPTURE", ["verdictRule"] = note };
    JsonObject Missing(string note) => new() { ["verdict"] = "INCONCLUSIVE", ["missing"] = note };
    string? ReadLineBounded() { var read = Task.Run(Console.ReadLine); while (!read.IsCompleted) { if (cancel.IsCancellationRequested) return null; Thread.Sleep(50); } return read.GetAwaiter().GetResult(); }
    JsonObject BeginWindow(string check, string phase)
    {
        var plan = M5Hw.ObservationPlans.Single(p => p.Id == phase);
        Console.WriteLine($"Watch {plan.Watch}. {plan.Expected}");
        if (phase == "CANCEL") Console.WriteLine("Engine-derived reference (not proof of firmware display): " + Path.Combine(bundle, "neutral-reference.svg"));
        double from = Now;
        var window = new JsonObject { ["check"] = check, ["phase"] = phase, ["watch"] = plan.Watch,
            ["expected"] = plan.Expected, ["fromMs"] = from };
        activeObservationWindow = window;
        Console.WriteLine($"START {phase} observation ({from:F1} ms)");
        Event("observation-window-start", (JsonObject)window.DeepClone()); return window;
    }
    void EndWindow(JsonObject window)
    {
        if (ReferenceEquals(activeObservationWindow, window)) activeObservationWindow = null;
        if (window["toMs"] is not null) return;
        window["outcome"] ??= "completed";
        window["toMs"] = Now; Console.WriteLine($"END {window["phase"]} observation ({Now:F1} ms). Ignore subsequent activity when answering.");
        Event("observation-window-end", (JsonObject)window.DeepClone());
    }
    void ClosePendingWindow(string reason)
    {
        if (activeObservationWindow is not { } window) return;
        window["outcome"] = "incomplete"; window["reason"] = reason; EndWindow(window);
    }
    JsonObject AskWindow(JsonObject window, string question, params string[] choices)
    {
        if (window["toMs"] is null) throw new InvalidOperationException("Close the observation window before asking.");
        choices = choices.Concat(new[] { "unclear", "not-observed" }).Distinct().ToArray();
        string bounded = $"For the marked {window["phase"]} window {window["fromMs"]}..{window["toMs"]} ms only; exclude activity afterward: {question}";
        Console.WriteLine(bounded + " [" + string.Join(" / ", choices) + "]");
        double asked = Now; string? raw = yes ? null : ReadLineBounded();
        var answer = M5HwAnalysis.ObservationAnswer(window, bounded, raw, choices, asked, Now);
        Event("operator-observation", (JsonObject)answer.DeepClone()); return answer;
    }
    void WindowNotes(JsonObject window)
    {
        Console.WriteLine($"Optional description of anything unexpected during {window["phase"]} ({window["fromMs"]}..{window["toMs"]} ms); Enter to leave blank:");
        window["freeText"] = yes ? null : ReadLineBounded();
        Event("operator-window-notes", (JsonObject)window.DeepClone());
    }
    void SaveNeutralReference()
    {
        var clip = robot!.Animations.Scheduler.NeutralFaceAnimation;
        var frame = clip?.Keyframes.OfType<FaceKeyframe>().FirstOrDefault();
        if (frame is null) { neutralReference = Missing("Loaded neutral clip/first face is unavailable; no invented appearance reference"); Console.WriteLine("Resting-eye reference unavailable; its visual comparison will be incomplete."); return; }
        byte[] canvas = ProceduralFaceRenderer.DrawFace(frame.Pose, 0);
        var svg = new System.Text.StringBuilder("<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 128 64' width='768' height='384'><title>Engine-derived loaded neutral face, scanline parity0; reference only, not firmware display</title><rect width='128' height='64' fill='black'/><g fill='white'>");
        for (int y = 0; y < 64; y++) for (int x = 0; x < 128; x++)
            if (canvas[y * 128 + x] != 0) svg.Append($"<rect x='{x}' y='{y}' width='1' height='1'/>");
        svg.Append("</g></svg>"); string path = Path.Combine(bundle, "neutral-reference.svg"); File.WriteAllText(path, svg.ToString());
        neutralReference = new() { ["clip"] = clip!.Name, ["firstFaceTriggerMs"] = frame.TriggerTimeMs,
            ["file"] = "neutral-reference.svg", ["sha256"] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant(),
            ["basis"] = "M5-010 loaded neutral clip's first ProceduralFace keyframe; existing M5-032 renderer, explicit parity0. This engine-derived grayscale shape reference does not prove firmware color, scanout or display." };
        Event("neutral-reference", (JsonObject)neutralReference.DeepClone()); Console.WriteLine("Resting-eye reference: " + path);
    }
    public int Execute()
    {
        Directory.CreateDirectory(bundle); cache = Path.Combine(bundle, "cache"); Directory.CreateDirectory(cache); File.WriteAllText(Path.Combine(cache, "TestAnim.json"), "{}");
        Console.WriteLine("M5-HW: put Cozmo on the FLOOR, off charger, with clear space; head/lift and face animate. --allow-drive also enables idle body wiggles and short DriveToPose. Ctrl+C stops and preserves evidence. About 3 minutes.\nBundle: " + bundle);

        ConsoleCancelEventHandler interrupt = (_, e) => { e.Cancel = true; cancel.Cancel(); }; Console.CancelKeyPress += interrupt;
        using var timer = new Timer(_ => cancel.Cancel(), null, TimeSpan.FromMinutes(10), Timeout.InfiniteTimeSpan);
        try
        {
            if (!yes) { Console.WriteLine("Press Enter when ready."); ReadLineBounded(); cancel.Token.ThrowIfCancellationRequested(); }
            using var resolution = new HighResolutionTimer();
            robot = CozmoRobot.Create(engineOptions: new CozmoEngineOptions { ResourcesPath = resources, AnimationCachePath = cache, BeforeStart = e => { EngineEvidence.Subscribe(e, line => { }, Event); e.GoToSleepRequested += () => Event("go-to-sleep-requested", new()); e.ActionCompleted += a => Event("action-completed", JsonSerializer.SerializeToNode(a)!.AsObject()); } });
            cacheCtor = !File.Exists(Path.Combine(cache, "TestAnim.json"));
            robot.Transport.FrameTrace += f => frames.Enqueue((Now, f.Outbound, (byte[])f.Raw.Clone(), f.Frame, f.Error));
            robot.Engine.ConnectionResponse += r => { response = r; Event("connection-response", new() { ["result"] = r.Result.ToString(), ["firmware"] = r.FwVersion }); };
            robot.HandlerFaulted += e => Event("handler-fault", new() { ["exception"] = e.ToString() });
            robot.Animations.Event += s => Event("animation-event", new() { ["text"] = s });
            robot.State.StateUpdated += s => Event("state", new() { ["headRad"] = s.HeadAngle, ["motionHeadRad"] = robot.Motion.HeadAngleRad, ["status"] = s.Status, ["streamBytes"] = robot.Animations.Scheduler.Stream.BytesStreamed, ["streamFrames"] = robot.Animations.Scheduler.Stream.FramesStreamed });
            robot.Message += m => { if (m is AnimationState a) Event("animation-state", new() { ["robotTimestamp"] = a.Timestamp, ["bytesPlayed"] = a.NumAnimBytesPlayed, ["audioPlayed"] = a.NumAudioFramesPlayed, ["tag"] = a.Tag, ["drops"] = a.ClientDropCount, ["enabledAnimTracks"] = a.EnabledAnimTracks, ["streamBytes"] = robot.Animations.Scheduler.Stream.BytesStreamed, ["streamFrames"] = robot.Animations.Scheduler.Stream.FramesStreamed }); };
            robot.ConnectToRobot(ip);
            connected = Poll(() => response?.Result == RobotConnectionResult.Success && robot.Engine.Robot is { TimeSynced: true, FirstFullStateHandled: true, ReadyToStream: true }, 15000);
            if (connected) { bool assets = Poll(() => robot.Animations.ClipNames.Count > 0, 15000); bool calibrated = Poll(() => robot.State.CalibrationComplete, 20000); Event("check-readiness", new() { ["assets"] = assets, ["calibrated"] = calibrated }); connected = assets && calibrated; }
            if (connected)
            {
                SaveNeutralReference();
                if (!yes && neutralReference["file"] is not null)
                {
                    Console.WriteLine("Before checks start, open neutral-reference.svg from the bundle in a browser and review the resting-eye shape. This is an engine-derived reference, not a firmware display verdict. Press Enter when ready.");
                    ReadLineBounded(); cancel.Token.ThrowIfCancellationRequested();
                }
            }
            foreach (var def in M5Hw.Checks)
            {
                double start = Now; JsonObject result;
                if (only is not null && !only.Contains(def.Id)) result = Missing("not selected by --only");
                else if (!connected || robot.Transport.State != LinkState.Connected) result = Missing("connection/readiness unavailable");
                else if (cancel.IsCancellationRequested) result = Missing("run interrupted");
                else { Console.WriteLine("Running " + def.Id); Event("check-start", new() { ["check"] = def.Id }); try { result = Check(def.Id); ClosePendingWindow("Check returned without completing the marked phase."); } catch (Exception e) { ClosePendingWindow(e.Message); errors.Enqueue(def.Id + ": " + e); result = Missing(e.Message); } }
                result["id"] = def.Id; result["records"] = JsonSerializer.SerializeToNode(def.Records); result["fromMs"] = start; result["toMs"] = Now;
                result["uncertainty"] = new JsonArray(records.Where(x => def.Records.Contains(x["id"]!.GetValue<string>()) && x["status"]!.GetValue<string>() is "HARDWARE_ONLY" or "BLOCKED_EXTERNAL" or "IMPLEMENTATION_GAP" or "RECOVERABLE_GAP").Select(x => x.DeepClone()).ToArray()); results.Add(result);
            }
        }
        catch (Exception e) { errors.Enqueue(e.ToString()); }
        finally { ClosePendingWindow("Run interrupted before phase completion."); DisposeBounded(); Console.CancelKeyPress -= interrupt; WriteBundle(); }
        Console.WriteLine("M5 observations saved: " + bundle + "\nNo record settled; return this whole folder for review.");
        return errors.Count == 0 && results.Count == 11 && results.All(x => x["verdict"]!.GetValue<string>() != "INCONCLUSIVE") ? 0 : 1;
    }
    List<M5Hw.WireMessage> Wire(double from = 0, double to = double.MaxValue)
    {
        return M5HwAnalysis.ReadWire(frames.ToArray().Select((f, index) =>
            new M5HwAnalysis.CapturedFrame(index + 1, f.T, f.Out, f.Frame)))
            .Where(m => m.AtMs >= from && m.AtMs <= to).ToList();
    }
    AnimationClip? Clip(uint min = 1000, uint max = 10000, AnimationTrack required = AnimationTrack.Face)
    {
        var lib = robot!.Animations.Library; if (lib is null) return null;
        return lib.ClipNames.Select(lib.GetClip).Where(c => !c.LoadTruncated && c.DurationMs >= min && c.DurationMs <= max && (c.Tracks & AnimationTrack.Body) == 0 && (c.Tracks & required) == required).OrderBy(c => c.DurationMs).ThenBy(c => c.Name, StringComparer.Ordinal).FirstOrDefault();
    }
    AnimationTicket Play(AnimationClip clip) { Event("play-call", new() { ["clip"] = clip.Name, ["durationMs"] = clip.DurationMs, ["tracks"] = clip.Tracks.ToString() }); return robot!.Animations.PlayTracked(clip) ?? throw new InvalidOperationException("PlayTracked returned null"); }
    void Complete(AnimationTicket ticket, int timeout) { if (!Poll(() => ticket.Completion.IsCompleted, timeout)) { robot!.Animations.StopIfCurrent(ticket.Generation); throw new TimeoutException("Animation did not complete"); } Event("play-completed", new() { ["tag"] = ticket.Tag, ["result"] = ticket.Completion.GetAwaiter().GetResult().ToString() }); }
    void IdleOn() { robot!.Animations.Scheduler.PushIdleAnimation(AnimationTrigger.ProceduralLive, "m5-hw"); Event("idle-push", new()); }
    void IdleOff() { robot!.Animations.Scheduler.RemoveIdleAnimation("m5-hw", robot.Engine.Timer.SecondsF * 1000.0); robot.Animations.Stop(); Event("idle-remove", new()); }
    JsonObject Check(string id)
    {
        var r = robot!; double start = Now;
        switch (id)
        {
            case "TAGS":
                {
                    var c = Clip(300, 2000); if (c is null) return Missing("No short face clip without body track");
                    for (int i = 0; i < 3; i++) Complete(Play(c), (int)c.DurationMs + 5000);
                    Wait(1500); return M5Hw.GradeTags(Wire(start));
                }
            case "ABORT":
            case "KEEP_ALIVE":
                {
                    var c = Clip(4000, 12000); if (c is null) return Missing("No long clip without body track");
                    var cancelWindow = BeginWindow(id, "CANCEL");
                    var t = Play(c); Wait(750); double at = Now; Console.WriteLine("CANCEL requested now; watch the clip stop, then distinguish resting-eye shape, blinks and darts."); Event("cancel-call", new() { ["tag"] = t.Tag }); r.Animations.Stop();
                    Poll(() => Wire(at).Any(x => x.Outbound && x.Payload[0] == 0x8D), 2000);
                    var wireAbort = Wire(at).FirstOrDefault(x => x.Outbound && x.Payload[0] == 0x8D);
                    double abortAt = wireAbort?.AtMs ?? Now, quietEnd = abortAt + 400;
                    if (Now < quietEnd) Wait((int)Math.Ceiling(quietEnd - Now));
                    var result = M5Hw.GradeAbort(Wire(start, quietEnd), at, quietEnd); Wait(1600);
                    result["robotCounterTail"] = new JsonArray(events.Where(e => e["kind"]?.GetValue<string>() == "animation-state" && e["atMs"]!.GetValue<double>() >= at && e["atMs"]!.GetValue<double>() <= abortAt + 2000).Select(e => e.DeepClone()).ToArray());
                    result["robotEchoTail"] = JsonSerializer.SerializeToNode(Wire(at, abortAt + 2000).Where(x => !x.Outbound && x.Payload[0] is (byte)RobotMessageId.AnimStarted or (byte)RobotMessageId.AnimEnded).Select(x => new { atMs = x.AtMs, payloadHex = Convert.ToHexString(x.Payload) }));
                    var keep = Wire(quietEnd, abortAt + 2000).Where(x => x.Outbound && x.Payload[0] is 0x9B or (byte)RobotMessageId.AnimFaceImage).ToArray();
                    result["keepAliveTail"] = JsonSerializer.SerializeToNode(keep.Select(x => new { atMs = x.AtMs, payloadHex = Convert.ToHexString(x.Payload) }));
                    if (id == "KEEP_ALIVE" && !keep.Any(x => x.Payload[0] == (byte)RobotMessageId.AnimFaceImage)) result["verdict"] = "INCONCLUSIVE";
                    EndWindow(cancelWindow); result["wireObservationVerdict"] = result["verdict"]!.DeepClone();
                    result["cancelAtMs"] = at; result["keepAliveWindowEndMs"] = Now;
                    result["neutralReference"] = neutralReference.DeepClone();
                    result["clipStopped"] = AskWindow(cancelWindow, "After the announced cancel, did the clip's face/head/lift activity stop?", "stopped", "continued");
                    result["restingEyeAppearance"] = AskWindow(cancelWindow, "After cancel, did the two resting-eye shapes resemble the loaded reference?", "resembled-reference", "different");
                    result["blinkObservation"] = AskWindow(cancelWindow, "Did the eyes briefly close and reopen?", "seen", "not-seen");
                    result["eyeDartObservation"] = AskWindow(cancelWindow, "Did the eye shapes shift position within the face?", "seen", "not-seen");
                    WindowNotes(cancelWindow); result["observationWindow"] = cancelWindow;
                    if (neutralReference["file"] is null || result["clipStopped"]!["answer"]!.GetValue<string>() != "stopped"
                        || result["restingEyeAppearance"]!["answer"]!.GetValue<string>() != "resembled-reference") result["verdict"] = "INCONCLUSIVE";
                    result["timingNote"] = "Quiet interval is 400 ms after actual wire Abort; full two-second tail retained. The 0.5 s neutral gate uses native last-stream engine time, unavailable through existing public observations; no cancel-time equality or exact neutral deadline is asserted.";
                    result["nativeLastStreamTime"] = null;
                    result["neutralCandidateStarts"] = M5HwAnalysis.WireEvidence(Wire(quietEnd, abortAt + 2000).Where(x => x.Outbound && x.Payload.Length == 2 && x.Payload[0] == 0x9B && x.Payload[1] != 255));
                    if (id == "KEEP_ALIVE")
                    {
                        if (!drive) { result["liveIdle"] = Missing("--allow-drive required for default body wiggles"); result["verdict"] = "INCONCLUSIVE"; }
                        else
                        {
                            var liveWindow = BeginWindow(id, "LIVE"); double live = Now; IdleOn();
                            try
                            {
                                Wait(10000); double liveEnd = Now;
                                var bursts = M5HwAnalysis.LiveBursts(Wire(live, liveEnd), live, liveEnd);
                                result["liveIdle"] = bursts;
                                if (bursts["verdict"]!.GetValue<string>() != "OBSERVED_FF_BURST_ORDER") result["verdict"] = "INCONCLUSIVE";
                                result["liveFromMs"] = live; result["liveToMs"] = liveEnd;
                            }
                            finally { EndWindow(liveWindow); IdleOff(); }
                            result["bodyObservation"] = AskWindow(liveWindow, "Did the body/wheels make small wiggles?", "seen", "not-seen");
                            WindowNotes(liveWindow); result["liveObservationWindow"] = liveWindow;
                            if (result["bodyObservation"]!["answer"]!.GetValue<string>() != "seen") result["verdict"] = "INCONCLUSIVE";
                        }
                        // A short nonlive play clears the previous idle and its persistent keepalive layers.
                        // Normal KeepFaceAlive then supplies the persistent eye dart; no fabricated layer or hook.
                        var prime = Clip(300, 2000);
                        if (prime is null) { result["persistentFace"] = Missing("No short clip to prime the no-idle layer path"); result["verdict"] = "INCONCLUSIVE"; }
                        else
                        {
                            Complete(Play(prime), (int)prime.DurationMs + 5000);
                            var scheduler = r.Animations.Scheduler;
                            bool Preconditions() => !scheduler.IsPlaying
                                && scheduler.IdleStack.Last().Trigger == AnimationScheduler.IdleCount
                                && scheduler.FaceLayerNames.Contains("KeepAliveEyeDart");
                            if (!Poll(Preconditions, 5000)) { result["persistentFace"] = Missing("No-animation/no-idle persistent KeepAliveEyeDart prerequisites not observed"); result["verdict"] = "INCONCLUSIVE"; }
                            else
                            {
                                double layerFrom = Now; var samples = new JsonArray(); bool held = true;
                                Event("persistent-face-window-start", new());
                                do
                                {
                                    bool valid = Preconditions(); held &= valid;
                                    samples.Add(new JsonObject { ["atMs"] = Now, ["preconditionsHeld"] = valid,
                                        ["faceLayers"] = JsonSerializer.SerializeToNode(scheduler.FaceLayerNames) });
                                    Wait(60);
                                } while (Now - layerFrom < 10000);
                                double layerTo = Now; held &= Preconditions();
                                Event("persistent-face-window-end", new());
                                var persistent = M5HwAnalysis.PersistentFace(Wire(layerFrom, layerTo), layerFrom, layerTo, held);
                                persistent["fromMs"] = layerFrom; persistent["toMs"] = layerTo;
                                persistent["preconditionSamples"] = samples;
                                persistent["preconditionNote"] = "Existing public scheduler snapshots sampled about every 60 ms; not atomic instrumentation. No idle or explicit play is issued during this window. Review raw traffic for intervening activity.";
                                result["persistentFace"] = persistent;
                                if (persistent["verdict"]!.GetValue<string>() != "OBSERVED_PERSISTENT_FACE_WINDOW") result["verdict"] = "INCONCLUSIVE";
                            }
                        }
                    }
                    return result;
                }
            case "LOCKED_HEAD":
                {
                    var c = Clip(1500, 7000, AnimationTrack.Head); if (c is null) return Missing("No head clip without body track");
                    double lockAt = Now; Event("head-lock-request", new());
                    r.Motion.LockTracks(CozmoMotion.HeadTrack, "m5-hw", "hardware test"); Wait(300);
                    var window = BeginWindow(id, "LOCKED"); double clipFrom = window["fromMs"]!.GetValue<double>(), clipTo = clipFrom, unlockAt = clipFrom;
                    try { Complete(Play(c), (int)c.DurationMs + 5000); clipTo = Now; }
                    finally
                    {
                        EndWindow(window); clipTo = window["toMs"]!.GetValue<double>(); unlockAt = Now; Console.WriteLine("UNLOCK head now. Movement after this announcement is outside LOCKED observation.");
                        Event("head-unlock-request", new()); r.Motion.UnlockTracks(CozmoMotion.HeadTrack, "m5-hw");
                    }
                    Wait(300);
                    var result = M5HwAnalysis.LockedHead(Wire(start), lockAt, clipFrom, clipTo, unlockAt, Now);
                    result["wireObservationVerdict"] = result["verdict"]!.DeepClone();
                    result["headStayedPut"] = AskWindow(window, "Did the head stay at its starting angle during the locked clip, excluding all post-UNLOCK motion?", "yes", "no");
                    WindowNotes(window); result["observationWindow"] = window;
                    if (result["headStayedPut"]!["answer"]!.GetValue<string>() != "yes") result["verdict"] = "INCONCLUSIVE";
                    return result;
                }
            case "AUDIO":
                {
                    var c = Clip(5500, 15000, AnimationTrack.Audio); if (c is null) return Missing("No >=5.5 s audio clip without body track");
                    var sound = new[] { Path.Combine(resources, "sound"), Path.Combine(resources, "assets", "sound") }.FirstOrDefault(Directory.Exists);
                    string source = "none (silence only)"; if (sound is not null) { r.Animations.AudioSource = Cozmo.Robot.Animation.Wwise.WwiseAudioSource.Load(new[] { sound }); source = sound; }
                    double playStart = Now; Complete(Play(c), (int)c.DurationMs + 8000); double playEnd = Now; Wait(500);
                    var result = M5HwAnalysis.Audio(Wire(playStart, playEnd), events, playStart, playEnd); result["playFromMs"] = playStart; result["playToMs"] = playEnd;
                    result["clip"] = c.Name; result["audioSource"] = source;
                    return result;
                }
            case "LAYERS":
                {
                    var c = Clip(300, 2000); if (c is null) return Missing("No short clip to clear persistent keepalive layers");
                    var sched = r.Animations.Scheduler; var previous = sched.DesiredFaceDistortion; float timeout = sched.KeepAliveTimeoutSec;
                    try
                    {
                        sched.KeepAliveTimeoutSec = 600; Complete(Play(c), (int)c.DurationMs + 5000);
                        if (!Poll(() => !sched.HaveLayersToSend, 3000)) return Missing("Existing layers did not drain after nonlive InitStream RemoveKeepFaceAlive(99)");
                        var layerWindow = BeginWindow(id, "LAYERS"); double layerStart = Now; int pulse = 1; sched.DesiredFaceDistortion = () => Interlocked.Exchange(ref pulse, 0) == 1 ? 0.8f : 0f;
                        if (!Poll(() => Volatile.Read(ref pulse) == 0, 2000)) return Missing("TLC did not consume distortion pulse");
                        Wait(200); bool drained = Poll(() => !sched.HaveLayersToSend, 5000); Wait(300);
                        double layerEnd = Now; EndWindow(layerWindow);
                        var result = M5HwAnalysis.FiniteLayers(Wire(layerStart, layerEnd), layerStart, layerEnd, drained);
                        result["layerFromMs"] = layerStart; result["layerToMs"] = layerEnd;
                        result["stimulus"] = "Finite public distortion-provider pulse through TLC.Update; 600s harness timeout suppresses keepalive and short nonlive InitStream removes its persistent layers. No per-frame generation flags are exposed; counts alone cannot certify the random finite-glitch trace.";
                        var persistent = results.FirstOrDefault(x => x["id"]?.GetValue<string>() == "KEEP_ALIVE")?["persistentFace"];
                        result["persistentFaceCoverage"] = persistent?.DeepClone() ?? Missing("Separate KEEP_ALIVE persistent-face observation not available in this subset");
                        sched.DesiredFaceDistortion = previous; sched.KeepAliveTimeoutSec = timeout;
                        result["faceObservation"] = AskWindow(layerWindow, "Was a brief face-image distortion or shift visible?", "seen", "not-seen");
                        result["backpackObservation"] = AskWindow(layerWindow, "Did the backpack lights change?", "seen", "not-seen");
                        WindowNotes(layerWindow); result["observationWindow"] = layerWindow;
                        return result;
                    }
                    finally { sched.DesiredFaceDistortion = previous; sched.KeepAliveTimeoutSec = timeout; }
                }
            case "LIVE_IDLE":
                {
                    if (!drive) return Missing("--allow-drive required: default live idle includes body wiggles");
                    double liveEnd = start; IdleOn(); try { Wait(12000); liveEnd = Now; } finally { IdleOff(); }
                    var result = M5HwAnalysis.LiveIdle(Wire(start, liveEnd), events, start, liveEnd);
                    result["uncalibratedCase"] = "Not forced on real hardware; covered by B-M5-23 regression. Any uncalibrated states retained in events."; return result;
                }
            case "PARAMETERS":
                {
                    var prime = Clip(300, 2000); if (prime is null) return Missing("No clip to prime keepalive blink path"); Complete(Play(prime), (int)prime.DurationMs + 5000);
                    double parameterAt = Now; Event("blink-parameters-request", new() { ["minMs"] = 44000, ["maxMs"] = 45000, ["resetExistingDeadline"] = false });
                    r.Animations.SetLiveIdleAnimationParameters(new[] { LiveIdleParam.BlinkSpacingMinTime_ms, LiveIdleParam.BlinkSpacingMaxTime_ms }, new[] { 44000f, 45000f }); Wait(300);
                    var result = Observe("M5-062 B4 base setter accepts 45000 uncapped. Min 44000 and max 45000 avoid the max<=min fallback and make long-spacing observation discriminating; first pre-existing blink deadline may remain. No movement idle is pushed.");
                    result["maxReadback"] = r.Animations.Scheduler.LiveIdleParameters[LiveIdleParam.BlinkSpacingMaxTime_ms]; result["minReadback"] = r.Animations.Scheduler.LiveIdleParameters[LiveIdleParam.BlinkSpacingMinTime_ms];
                    var blinkWindow = BeginWindow(id, "BLINKS");
                    var layerSamples = new JsonArray();
                    try
                    {
                        for (int second = 0; second < 60; second++)
                        {
                            layerSamples.Add(new JsonObject { ["atMs"] = Now, ["engineSecondsSnapshot"] = r.Engine.Timer.SecondsF,
                                ["faceLayers"] = JsonSerializer.SerializeToNode(r.Animations.Scheduler.FaceLayerNames) });
                            Wait(1000);
                        }
                    }
                    finally { EndWindow(blinkWindow); r.Animations.SetLiveIdleAnimationParameters(Array.Empty<LiveIdleParam>(), Array.Empty<float>(), true); }
                    result["observation"] = AskWindow(blinkWindow, "After any initial blink, were subsequent close/reopen blinks about44-45 seconds apart (do not count eye darts)?", "yes", "no");
                    WindowNotes(blinkWindow); result["observationWindow"] = blinkWindow;
                    double blinkEnd = blinkWindow["toMs"]!.GetValue<double>();
                    result["parameterRequestAtMs"] = parameterAt; result["existingDeadline"] = "Retained by B4; actual internal deadline unavailable through allowed public observations.";
                    var evidence = M5HwAnalysis.Blinks(Wire(parameterAt, blinkEnd), parameterAt, blinkEnd,
                        result["minReadback"]!.GetValue<float>(), result["maxReadback"]!.GetValue<float>(), result["observation"]!.AsObject());
                    evidence["layerNameSnapshots"] = layerSamples;
                    evidence["snapshotNote"] = "Wall-clock/public engine-time/layer-name samples are not KeepFaceAlive call numbers or generation/deadline events. Do not infer a call count from elapsed time or fabricate blink events from pixel darkness.";
                    result["blinkEvidence"] = evidence; result["verdict"] = "INCONCLUSIVE";
                    return result;
                }
            case "DRIVING":
                {
                    if (!drive) return Missing("--allow-drive not given; driving skipped");
                    var m = new ManipulationSystem(r, r.Vision); var pose = m.RobotPose(); if (pose is null) { m.Dispose(); return Missing("No world pose for DriveToPose"); }
                    var goal = new Pose3d(Rotation3d.Identity, new Vec3(60, 0, 0), "m5-hw-goal", pose);
                    var action = new DriveToPoseAction(m) { Goal = goal }; using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancel.Token); limit.CancelAfter(15000);
                    var task = action.RunAsync(limit.Token); var states = new JsonArray(); string? last = null;
                    try
                    {
                        while (!task.IsCompleted && !limit.IsCancellationRequested) { string? current = r.Animations.Playing; if (current != last) { states.Add(new JsonObject { ["atMs"] = Now, ["playing"] = current, ["tag"] = r.Animations.CurrentTag }); last = current; } Wait(10); }
                        if (!task.IsCompleted) { limit.Cancel(); if (!task.Wait(2000)) { DisposeBounded(); connected = false; throw new TimeoutException("Drive cancellation did not complete; robot torn down before action cleanup"); } }
                        Wait(100);
                        var result = Observe("Actual DriveToPose.RunAsync on real ActionList, 60 mm forward from current pose; animation names/tags and completion time retained. M12/M8 gaps remain explicit; no successful move is called source proof.");
                        result["actionCompletions"] = new JsonArray(events.Where(e => e["kind"]?.GetValue<string>() == "action-completed" && e["atMs"]!.GetValue<double>() >= start).Select(e => e.DeepClone()).ToArray()); result["animationTransitions"] = states; result["driveCompletedAtMs"] = Now; result["result"] = task.IsCompletedSuccessfully ? task.Result.ToString() : "timed out/cancelled"; result["trace"] = JsonSerializer.SerializeToNode(action.Trace);
                        result["triplet"] = "default trigger ids 0x95 / 0x91 / 0x8D; no pushed overrides";
                        var phaseLogs = events.Where(e => e["atMs"]!.GetValue<double>() >= start && e["name"]?.GetValue<string>() == "GetResponseForAnimationTrigger.Found").ToArray();
                        string[] expected = { ((AnimationTrigger)0x95).ToString(), ((AnimationTrigger)0x91).ToString(), ((AnimationTrigger)0x8D).ToString() };
                        var phaseTimes = expected.Select(name => phaseLogs.FirstOrDefault(e => e["text"]?.GetValue<string>().StartsWith(name + " -> ", StringComparison.Ordinal) == true)?["atMs"]?.GetValue<double>()).ToArray();
                        var completions = events.Where(e => e["kind"]?.GetValue<string>() == "action-completed" && e["atMs"]!.GetValue<double>() >= start).ToArray();
                        var driveDone = completions.FirstOrDefault(e => e["ActionType"]!.GetValue<int>() == 12
                            && e["Tag"]!.GetValue<uint>() == action.Tag);
                        var endEvidence = phaseTimes[2] is { } endAt && driveDone is not null
                            ? M5HwAnalysis.DrivingEnd(Wire(start), completions, endAt, driveDone["atMs"]!.GetValue<double>())
                            : new JsonObject { ["endCompletionBeforeDrive"] = false };
                        bool endBeforeDrive = endEvidence["endCompletionBeforeDrive"]!.GetValue<bool>();
                        result["endEvidence"] = endEvidence;
                        bool ordered = phaseTimes.All(t => t is not null) && phaseTimes[0] <= phaseTimes[1] && phaseTimes[1] <= phaseTimes[2];
                        result["phaseTriggerLogs"] = new JsonArray(phaseLogs.Select(e => e.DeepClone()).ToArray()); result["orderedDefaultPhases"] = ordered; result["endCompletionBeforeDrive"] = endBeforeDrive;
                        if (!task.IsCompletedSuccessfully || task.Result != ActionResult.Success || !ordered || !endBeforeDrive) result["verdict"] = "INCONCLUSIVE";
                        return result;
                    }
                    finally { limit.Cancel(); if (!task.IsCompleted && !task.Wait(2000)) { DisposeBounded(); connected = false; } r.Motion.StopAllMotors(); if (task.IsCompleted || disposeCompleted) { action.Dispose(); goal.Dispose(); m.Dispose(); } else { errors.Enqueue("Drive action dependencies retained until process exit because teardown did not complete"); } }
                }
            case "SLEEP":
                {
                    r.Engine.StartIdleTimeout(0.2f, -1); Wait(1500); r.Engine.CancelIdleTimeout();
                    bool fallback = events.Any(e => e["kind"]?.GetValue<string>() == "go-to-sleep-requested" && e["atMs"]!.GetValue<double>() >= start);
                    bool missing = events.Any(e => e["atMs"]!.GetValue<double>() >= start
                        && e["kind"]?.GetValue<string>() == "stack-log"
                        && e["line"]?.GetValue<string>().StartsWith("MISSING: M5-038 -> M8 ", StringComparison.Ordinal) == true);
                    var sends = Wire(start).Where(x => x.Outbound && x.Payload[0] == 0x9B).ToArray();
                    var result = fallback && missing ? Observe("M5-038/M1-045 fallback and visible MISSING observed; known M8 action gap remains. D2/D5/D4 are factory requests, not wire opcodes; no new named sleep play should appear in logs.") : Missing("Missing fallback or diagnostic"); result["goToSleepRequested"] = fallback; result["missingDiagnostic"] = missing; result["animationStarts"] = sends.Length; return result;
                }
            case "TEARDOWN":
                {
                    var c = Clip(4000, 12000); if (c is null) return Missing("No long clip for Dispose-during-play"); Play(c); Wait(750); File.WriteAllText(Path.Combine(cache, "TestAnim.json"), "{}");
                    double before = Now; DisposeBounded(); double after = Now; Thread.Sleep(1000);
                    var late = Wire(after).Where(x => x.Outbound).ToArray(); var result = Observe("No outbound data after Dispose completes; teardown batch may contain stop/disconnect. DisconnectRequest is not required because M1-006 pacing may retain it."); result["disposeStartMs"] = before; result["disposeEndMs"] = after; result["lateMessages"] = late.Length; result["cacheRemovedOnConstruction"] = cacheCtor; result["cacheRemovedOnDispose"] = !File.Exists(Path.Combine(cache, "TestAnim.json")); if (!disposeCompleted || late.Length > 0 || !cacheCtor || File.Exists(Path.Combine(cache, "TestAnim.json"))) result["verdict"] = "INCONCLUSIVE"; return result;
                }
            default: return Missing("No implementation");
        }
    }
    void DisposeBounded()
    {
        if (disposed || robot is null) return; disposed = true; var r = robot;
        var task = Task.Run(() => { try { r.Dispose(); } catch (Exception e) { lock (errors) errors.Enqueue("Dispose: " + e); } });
        disposeCompleted = task.Wait(5000); if (!disposeCompleted) { lock (errors) errors.Enqueue("Dispose did not finish in 5 seconds; quiet-wire result invalid"); }
        Event("dispose-returned", new() { ["completed"] = task.IsCompleted });
    }
    void WriteBundle()
    {
        foreach (var def in M5Hw.Checks.Where(d => results.All(x => x["id"]?.GetValue<string>() != d.Id))) results.Add(new() { ["id"] = def.Id, ["records"] = JsonSerializer.SerializeToNode(def.Records), ["verdict"] = "INCONCLUSIVE", ["missing"] = "run terminated before check" });
        File.WriteAllLines(Path.Combine(bundle, "frames.jsonl"), frames.Select(f => new JsonObject { ["atMs"] = f.T, ["outbound"] = f.Out, ["rawBase64"] = Convert.ToBase64String(f.Raw), ["error"] = f.Error }.ToJsonString()));
        File.WriteAllLines(Path.Combine(bundle, "messages.jsonl"), Wire().Select(m => new JsonObject { ["atMs"] = m.AtMs, ["outbound"] = m.Outbound, ["tag"] = $"0x{m.Payload[0]:X2}", ["payloadHex"] = Convert.ToHexString(m.Payload), ["frameLine"] = m.FrameLine, ["submessageOrdinal"] = m.SubmessageOrdinal, ["reliableSequence"] = m.ReliableSequence, ["connectionEpoch"] = m.ConnectionEpoch }.ToJsonString()));
        File.WriteAllLines(Path.Combine(bundle, "events.jsonl"), events.Select(e => e.ToJsonString()));
        var files = new JsonArray(); foreach (var relative in new[] { "cozmo-stack/src/Cozmo.Conformance/M5Hw.cs", "cozmo-stack/src/Cozmo.Conformance/M5HwAnalysis.cs", "cozmo-stack/src/Cozmo.Conformance/Program.cs", "re-analysis/acceptance/hardware/scripts/m5-hw-1.md", "re-analysis/fidelity_manifest.json", "re-analysis/research/20261019-m5-settling-b.md" })
        {
            string source = Path.Combine(root, relative); if (!File.Exists(source)) { errors.Enqueue("Missing provenance file " + relative); continue; }
            try { string dest = Path.Combine(bundle, "provenance", relative); Directory.CreateDirectory(Path.GetDirectoryName(dest)!); File.Copy(source, dest, true); files.Add(new JsonObject { ["path"] = relative, ["sha256"] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source))).ToLowerInvariant() }); } catch (Exception e) { errors.Enqueue("Provenance: " + relative + ": " + e.Message); }
        }
        var binaries = new JsonArray(); foreach (var assembly in new[] { typeof(M5Hw).Assembly, typeof(CozmoRobot).Assembly, typeof(RobotMessage).Assembly, typeof(ReliableTransport).Assembly })
        {
            try { string source = assembly.Location; string dest = Path.Combine(bundle, "binaries", Path.GetFileName(source)); Directory.CreateDirectory(Path.GetDirectoryName(dest)!); File.Copy(source, dest, true); binaries.Add(new JsonObject { ["file"] = Path.GetFileName(source), ["sha256"] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source))).ToLowerInvariant() }); } catch (Exception e) { errors.Enqueue("Binary provenance: " + e.Message); }
        }
        var resourceHashes = new JsonArray(); foreach (var directory in new[] { "animations", "animGroups", "sound" }.SelectMany(d => new[] { Path.Combine(resources, d), Path.Combine(resources, "assets", d) }).Where(Directory.Exists).Distinct())
            foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).OrderBy(x => x, StringComparer.Ordinal))
            {
                try { using var stream = File.OpenRead(file); resourceHashes.Add(new JsonObject { ["path"] = Path.GetRelativePath(resources, file), ["sha256"] = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant() }); } catch (Exception e) { errors.Enqueue("Resource hash: " + file + ": " + e.Message); }
            }
        File.WriteAllText(Path.Combine(bundle, "resource-hashes.json"), resourceHashes.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        string? commit = null; try { var p = Process.Start(new ProcessStartInfo("git", "rev-parse HEAD") { WorkingDirectory = root, RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true }); commit = p!.StandardOutput.ReadToEnd().Trim(); p.WaitForExit(); } catch (Exception e) { errors.Enqueue("git: " + e.Message); }
        var report = new JsonObject { ["testId"] = M5Hw.TestId, ["createdUtc"] = DateTime.UtcNow.ToString("O"), ["commit"] = commit, ["argv"] = JsonSerializer.SerializeToNode(argv), ["resources"] = resources, ["allowDrive"] = drive, ["connected"] = connected, ["checks"] = JsonSerializer.SerializeToNode(results), ["records"] = JsonSerializer.SerializeToNode(records), ["provenance"] = files, ["binaries"] = binaries, ["errors"] = JsonSerializer.SerializeToNode(errors), ["acceptance"] = "NOT_JUDGED: operator observations and manager review required; hardware success does not raise provenance" };
        File.WriteAllText(Path.Combine(bundle, "result.json"), report.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllText(Path.Combine(bundle, "README.txt"), "M5-HW evidence bundle. result.json contains every check and source records; messages.jsonl deduplicates reliable resends, frames.jsonl retains every datagram; events.jsonl retains startup logs, API events and counters. Times are host capture times, not device execution times. No acceptance or manifest changes performed. See provenance script for limits.");
    }
}

