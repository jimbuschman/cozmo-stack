using System.Text.Json.Nodes;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Transport;

namespace Cozmo.Conformance;

/// <summary>
/// The checks added for the 2026-10-19 M3+M4 acceptance run (settling pass B, "Hardware coverage for the new M3M4 script",
/// research/20261018-m3m4-settling-b.md): DAS_EVENTS, CAMERA_PARAMS, TRACK_UNLOCK, ANIM_BUDGET, CLIFF, CUBE_LIGHTS and
/// DISCONNECT_QUIET. Same vocabulary as the original seven: OBSERVED_* with its rule, or INCONCLUSIVE with what is missing.
/// Every action goes through the stack's public API, so what reaches the wire is what the production path sends.
/// </summary>
internal sealed partial class M3M4Run
{
    // ------------------------------------------------------------------ M3-055 / M3-060: DAS_EVENTS (passive)

    private JsonObject DasEventsCheck()
    {
        var res = new JsonObject(); var missing = new JsonArray();
        var log = _engineLog.ToArray();
        string[] keys = { "robot.handle_robot_set_head_id", "robot.handle_robot_set_body_id", "das.upload", "das.upload.fail" };
        var found = new JsonObject();
        foreach (var k in keys)
            found[k] = new JsonArray(log.Where(l => l.Line.Contains(": " + k + ": ", StringComparison.Ordinal)).Select(l => (JsonNode)new JsonObject { ["atMs"] = Num(l.T), ["line"] = l.Line }).ToArray());
        res["events"] = found;
        res["note"] = "full entries (level, data, UTF-8 bytes) are the engine-log rows of events.jsonl; $group/$phys globals are not log entries";
        bool head = found["robot.handle_robot_set_head_id"]!.AsArray().Count > 0, body = found["robot.handle_robot_set_body_id"]!.AsArray().Count > 0;
        bool flush = found["das.upload"]!.AsArray().Count + found["das.upload.fail"]!.AsArray().Count > 0;
        if (!head) missing.Add("no robot.handle_robot_set_head_id event (needs the robot's head-ID message)");
        if (!body) missing.Add("no robot.handle_robot_set_body_id event (needs the robot's body-ID message)");
        if (!flush) missing.Add("no startup flush result event (das.upload / das.upload.fail)");
        res["verdict"] = head && body && flush ? "OBSERVED_HEAD_BODY_AND_FLUSH_EVENTS" : head || body || flush ? "OBSERVED_PARTIAL" : "INCONCLUSIVE";
        res["verdictRule"] = "which of the head-ID, body-ID and startup-flush DAS events the engine log carries; the text and data are judged by the manager against M3-060/M3-055";
        if (missing.Count > 0) res["missing"] = missing;
        return res;
    }

    // ------------------------------------------------------------------ M3-021: CAMERA_PARAMS

    private JsonObject CameraParamsCheck()
    {
        var robot = _robot!; var cs = robot.CameraSettings;
        var res = new JsonObject(); var missing = new JsonArray();
        var wire = Wire();
        var defaults = wire.Where(m => !m.Out && m.Tag == Tag(RobotMessageId.DefaultCameraParams)).ToList();
        var sentBefore = wire.Where(m => m.Out && m.Tag == Tag(RobotMessageId.SetCameraParams)).ToList();
        res["connection"] = new JsonObject
        {
            ["defaultCameraParamsReceived"] = new JsonArray(defaults.Select(m => (JsonNode)MsgJson(m, 0)).ToArray()),
            ["setCameraParamsSent"] = new JsonArray(sentBefore.Select(m => (JsonNode)MsgJson(m, 0)).ToArray()),
            ["limits"] = new JsonObject { ["minExposureMs"] = cs.MinExposureMs, ["maxExposureMs"] = cs.MaxExposureMs, ["minGain"] = Num(cs.MinGain), ["maxGain"] = Num(cs.MaxGain) },
            ["current"] = new JsonObject { ["exposureMs"] = cs.CurrentExposureMs, ["gain"] = Num(cs.CurrentGain) },
            ["engineLog"] = new JsonArray(_engineLog.ToArray().Where(l => l.Line.Contains("CameraParams", StringComparison.Ordinal) || l.Line.Contains("CameraSettings", StringComparison.Ordinal) || l.Line.Contains("M11-059", StringComparison.Ordinal)).Take(30).Select(l => (JsonNode)l.Line).ToArray()),
        };
        var changes = new List<(double T, CurrentCameraParams P)>();
        void onChange(CurrentCameraParams p) { lock (changes) changes.Add((Now(), p)); }
        cs.CurrentCameraParamsChanged += onChange;
        int ex0 = cs.CurrentExposureMs; float g0 = cs.CurrentGain;
        JsonObject Request(string name, ushort exposure, float gain)
        {
            double t0 = Now(); int n0; lock (changes) n0 = changes.Count;
            bool ok; string? error = null;
            try { ok = cs.SetCameraSettings(exposure, gain); } catch (Exception e) { ok = false; error = e.Message; }
            Wait(1500);
            double t1 = Now();
            var sends = Wire().Where(m => m.Out && m.Tag == Tag(RobotMessageId.SetCameraParams) && m.T >= t0 && m.T <= t1).ToList();
            JsonArray ev; lock (changes) ev = new JsonArray(changes.Skip(n0).Select(c => (JsonNode)new JsonObject { ["afterMs"] = Num(c.T - t0), ["exposureMs"] = c.P.ExposureMs, ["gain"] = Num(c.P.Gain), ["autoExposure"] = c.P.AutoExposureEnabled }).ToArray());
            return new JsonObject
            {
                ["request"] = name, ["exposureMs"] = exposure, ["gain"] = Num(gain), ["returned"] = ok, ["exception"] = error,
                ["setCameraParamsSent"] = new JsonArray(sends.Select(m => (JsonNode)MsgJson(m, t0)).ToArray()),
                ["currentCameraParamsChanged"] = ev,
                ["engineLog"] = new JsonArray(LogSince(t0).Where(l => l.Contains("Camera", StringComparison.Ordinal)).Take(20).Select(l => (JsonNode)l).ToArray()),
            };
        }
        var reqs = new JsonArray();
        if (!_cancelled) reqs.Add(Request("valid: 20 ms, gain 1.5", 20, 1.5f));
        if (!_cancelled) reqs.Add(Request("exposure above the limit (65535 ms)", ushort.MaxValue, 1.5f));
        if (!_cancelled) reqs.Add(Request("gain above the limit (100)", 20, 100f));
        if (!_cancelled) reqs.Add(Request($"restore: {ex0} ms, gain {g0}", (ushort)Math.Clamp(ex0, 0, ushort.MaxValue), g0));
        cs.CurrentCameraParamsChanged -= onChange;
        res["requests"] = reqs;
        var r = reqs.Select(x => x!.AsObject()).ToList();
        if (defaults.Count == 0) missing.Add("no DefaultCameraParams from the robot on this connection");
        if (r.Count < 3) { res["verdict"] = "INCONCLUSIVE"; res["verdictRule"] = "the requests did not all run"; missing.Add("run interrupted"); }
        else
        {
            int validSends = r[0]["setCameraParamsSent"]!.AsArray().Count, badSends = r[1]["setCameraParamsSent"]!.AsArray().Count + r[2]["setCameraParamsSent"]!.AsArray().Count;
            res["verdict"] = validSends > 0 && badSends == 0 ? "OBSERVED_VALID_SENT_INVALID_REFUSED" : validSends > 0 ? "OBSERVED_INVALID_ALSO_SENT" : "OBSERVED_VALID_NOT_SENT";
            res["verdictRule"] = $"SetCameraParams sends: {validSends} for the valid request, {badSends} for the two out-of-range ones; payloads and events per request for the manager";
        }
        if (missing.Count > 0) res["missing"] = missing;
        return res;
    }

    // ------------------------------------------------------------------ M4-047: TRACK_UNLOCK

    private JsonObject TrackUnlockCheck()
    {
        var robot = _robot!; var res = new JsonObject(); var missing = new JsonArray();
        const byte mask = 0x4; const string who = "m3m4-hw";
        JsonObject Step(string name, Action act)
        {
            double t0 = Now(); act(); Wait(600); double t1 = Now();
            var sends = Wire().Where(m => m.Out && (m.Tag == Tag(RobotMessageId.EnableAnimTracks) || m.Tag == Tag(RobotMessageId.DisableAnimTracks)) && m.T >= t0 && m.T <= t1).ToList();
            return new JsonObject
            {
                ["step"] = name, ["trackSends"] = new JsonArray(sends.Select(m => (JsonNode)MsgJson(m, t0)).ToArray()),
                ["engineLog"] = new JsonArray(LogSince(t0).Where(l => l.Contains("Track", StringComparison.Ordinal) || l.Contains("Lock", StringComparison.Ordinal)).Take(30).Select(l => (JsonNode)l).ToArray()),
            };
        }
        bool first = false, second = false;
        var steps = new JsonArray
        {
            Step($"LockTracks(0x{mask:X}, {who})", () => robot.Motion.LockTracks(mask, who, "m3m4-hw")),
            Step($"UnlockTracks(0x{mask:X}, {who})", () => first = robot.Motion.UnlockTracks(mask, who)),
            Step($"UnlockTracks(0x{mask:X}, {who}) again (the miss path)", () => second = robot.Motion.UnlockTracks(mask, who)),
        };
        res["steps"] = steps; res["unlockReturns"] = new JsonArray(first, second);
        int extra = steps[2]!["trackSends"]!.AsArray().Count(s => s!["tag"]!.GetValue<string>() == $"0x{Tag(RobotMessageId.EnableAnimTracks):X2}");
        bool info = steps[2]!["engineLog"]!.AsArray().Any(l => l!.GetValue<string>().Contains("not currently locked", StringComparison.Ordinal));
        res["verdict"] = extra > 0 ? "OBSERVED_EXTRA_ENABLE_ANIM_TRACKS" : "OBSERVED_NO_EXTRA_SEND";
        res["verdictRule"] = $"the second unlock sent {extra} EnableAnimTracks; its 'not currently locked' info log {(info ? "was" : "was not")} seen; the robot's AnimationState track mask is in frames.jsonl";
        if (!info) missing.Add("no 'not currently locked' log after the second unlock");
        if (missing.Count > 0) res["missing"] = missing;
        return res;
    }

    // ------------------------------------------------------------------ M3-012: ANIM_BUDGET

    private AnimationClip? BudgetClip()
    {
        var lib = _robot!.Animations.Library; if (lib is null) return null;
        var clips = new List<AnimationClip>();
        foreach (var name in lib.ClipNames)
        {
            AnimationClip c; try { c = lib.GetClip(name); } catch { continue; }
            if ((c.Tracks & AnimationTrack.Audio) == 0 || (c.Tracks & AnimationTrack.Body) != 0 || c.LoadTruncated) continue;
            if (c.DurationMs is < 4000 or > 15000) continue;
            clips.Add(c);
        }
        return clips.OrderByDescending(c => c.DurationMs).ThenBy(c => c.Name, StringComparer.Ordinal).FirstOrDefault();
    }

    private async Task<JsonObject> AnimBudgetCheck()
    {
        var robot = _robot!; var res = new JsonObject(); var missing = new JsonArray();
        string? audio = null;
        if (robot.Animations.AudioSource is null && _repoRoot is not null && EngineStartupResources.Options(_argv).ResourcesPath is { } rp)
        {
            var sound = new[] { Path.Combine(rp, "sound"), Path.Combine(rp, "assets", "sound") }.FirstOrDefault(Directory.Exists);
            if (sound is not null) { try { robot.Animations.AudioSource = Cozmo.Robot.Animation.Wwise.WwiseAudioSource.Load(new[] { sound }); audio = "Wwise banks from " + sound; } catch (Exception e) { audio = "Wwise load failed: " + e.Message; } }
        }
        res["audioSource"] = audio ?? (robot.Animations.AudioSource is null ? "none: audio keyframes stream silence frames, which the budget counts the same way" : "already set");
        var clip = BudgetClip();
        if (clip is null) { res["verdict"] = "INCONCLUSIVE"; res["verdictRule"] = "no clip"; missing.Add("no loaded clip with audio, no body track, 4-15 s"); res["missing"] = missing; return res; }
        res["clip"] = new JsonObject { ["name"] = clip.Name, ["durationMs"] = clip.DurationMs, ["tracks"] = clip.Tracks.ToString(), ["rule"] = "longest clip with an audio track and no body track, 4..15 s, ties by name" };
        Console.WriteLine($"playing {clip.Name} ({clip.DurationMs} ms; head/lift may move, the wheels do not)");
        var sched = robot.Animations.Scheduler;
        var samples = new List<(double T, int Bytes, int Frames)>();
        using var stop = new CancellationTokenSource();
        var sampler = Task.Run(() => { while (!stop.IsCancellationRequested) { lock (samples) samples.Add((Now(), sched.Stream.BytesStreamed, sched.Stream.FramesStreamed)); Thread.Sleep(5); } });
        double t0 = Now();
        var ticket = robot.Animations.PlayTracked(clip);
        AnimationEndReason? reason = null;
        if (ticket is not null)
        {
            var limit = (int)clip.DurationMs + 5000;
            if (await Task.WhenAny(ticket.Completion, Task.Delay(limit)) == ticket.Completion) reason = ticket.Completion.Result;
            else { robot.Animations.StopIfCurrent(ticket.Generation); missing.Add($"not completed within {limit} ms; stopped"); }
        }
        else missing.Add("PlayTracked returned null");
        Wait(1500);
        stop.Cancel(); await sampler;
        double t1 = Now();
        var states = Wire().Where(m => !m.Out && m.Tag == Tag(RobotMessageId.AnimState) && m.T >= t0 && m.T <= t1).Select(m => (m.T, a: Parse(m.Payload) as AnimationState)).Where(x => x.a is not null).ToList();
        (int Bytes, int Frames) At(double t) { lock (samples) { var s = samples.LastOrDefault(x => x.T <= t); return (s.Bytes, s.Frames); } }
        var rows = new JsonArray(); int over = 0;
        foreach (var (t, a) in states)
        {
            var (b, f) = At(t);
            // The counters are compared within this play: both sides as deltas from the first state/sample of the window.
            rows.Add(new JsonObject { ["afterMs"] = Num(t - t0), ["robotTs"] = a!.Timestamp, ["animBytesPlayed"] = a.NumAnimBytesPlayed, ["audioFramesPlayed"] = a.NumAudioFramesPlayed, ["drops"] = a.ClientDropCount, ["engineBytesStreamed"] = b, ["engineFramesStreamed"] = f });
        }
        var outbound = Wire().Where(m => m.Out && m.T >= t0 && m.T <= t1).ToList();
        res["playMs"] = Num(t1 - t0); res["endReason"] = reason?.ToString();
        res["stateRows"] = rows;
        res["outboundByTag"] = Histogram(outbound.Select(m => TagName(m.Tag)));
        var warn = LogSince(t0).Where(l => l.Contains("NegativeMinBytesFreeInRobot", StringComparison.Ordinal) || l.Contains("BytesStreamedOverflow", StringComparison.Ordinal)).ToArray();
        res["budgetLogs"] = new JsonArray(warn.Select(l => (JsonNode)l).ToArray());
        if (states.Count >= 2)
        {
            var (fa, la) = (states[0].a!, states[^1].a!);
            over = states.Count(x => { var (b, _) = At(x.T); return unchecked((int)x.a!.NumAnimBytesPlayed) > b; });
            res["dropDelta"] = (long)la.ClientDropCount - fa.ClientDropCount;
            res["statesWherePlayedExceedsStreamed"] = over;
        }
        else missing.Add("fewer than 2 AnimationState messages during the play");
        if (states.Count < 2) { res["verdict"] = "INCONCLUSIVE"; res["verdictRule"] = "no AnimationState counters to compare"; }
        else
        {
            bool drops = res["dropDelta"]!.GetValue<long>() != 0;
            res["verdict"] = warn.Length > 0 ? "OBSERVED_BUDGET_WARNING" : drops ? "OBSERVED_ROBOT_DROPS" : "OBSERVED_NO_BUDGET_WARNING_NO_DROPS";
            res["verdictRule"] = "a NegativeMinBytesFreeInRobot/BytesStreamedOverflow log during the play; else a rise in the robot's ClientDropCount; else neither. Counters side by side in stateRows (played vs the engine's streamed totals at the state's arrival)";
        }
        if (missing.Count > 0) res["missing"] = missing;
        return res;
    }

    // ------------------------------------------------------------------ M4-019 / M4-038: CLIFF

    private static readonly RobotMessageId[] CliffTags =
    {
        RobotMessageId.CliffEvent, RobotMessageId.PotentialCliff, RobotMessageId.RobotStopped, RobotMessageId.SetCliffDetectThreshold,
        RobotMessageId.EnableStopOnCliff, RobotMessageId.Stop, RobotMessageId.Drive,
    };

    private async Task<JsonObject> CliffCheck()
    {
        var robot = _robot!; var res = new JsonObject(); var missing = new JsonArray();
        var tags = CliffTags.Select(t => (byte)t).ToHashSet();
        var reports = new List<(double T, CliffReport R)>();
        void onCliff(CliffReport r) { lock (reports) reports.Add((Now(), r)); }
        robot.Sensors.CliffDetected += onCliff;
        JsonArray Seq(double from, double to) => new(Wire().Where(m => tags.Contains(m.Tag) && m.T >= from && m.T <= to).Select(m => (JsonNode)MsgJson(m, from)).ToArray());
        JsonArray Logs(double from) => new(LogSince(from).Where(l => l.Contains("Cliff", StringComparison.Ordinal) || l.Contains("RobotStopped", StringComparison.Ordinal) || l.Contains("Stopped", StringComparison.Ordinal)).Take(60).Select(l => (JsonNode)l).ToArray());
        res["sinceConnect"] = new JsonObject { ["messages"] = Seq(0, Now()), ["thresholdNow"] = robot.Sensors.CliffDetectThreshold };

        // Phase B: drive toward an edge (only with --allow-drive).
        if (!_allowDrive) { res["edge"] = new JsonObject { ["skipped"] = "--allow-drive not given" }; missing.Add("the edge phase needs --allow-drive and a table edge"); }
        else
        {
            Ask("CLIFF", "EDGE_READY", "Put Cozmo on a table facing its edge, about 10 cm away, with a clear straight path. Keep a hand just below the edge to catch him. Ready?", new[] { "ready", "skip" }, photo: false);
            var ready = _operatorAnswers.LastOrDefault(o => o.Key == "EDGE_READY")?.Answer;
            if (ready is "ready" || _yes)
            {
                double t0 = Now();
                robot.Sensors.SetStopOnCliff(true);
                Console.WriteLine("DRIVING forward at 30 mm/s for up to 6 s; it stops on the first CliffEvent or RobotStopped");
                int n0; lock (reports) n0 = reports.Count;
                // Not awaited: the M4-006 confirmation wait (up to 2 s) must not delay the stop watch below.
                var drive = robot.Motion.DriveWheelsAsync(30, 30, requireCalibration: false);
                Poll(() => { lock (reports) if (reports.Count > n0) return true; return Wire().Any(m => !m.Out && m.T >= t0 && (m.Tag == Tag(RobotMessageId.CliffEvent) || m.Tag == Tag(RobotMessageId.RobotStopped))); }, 6000, () => _cancelled);
                // Safety: stop at once (the robot should already have stopped itself); the engine's own reactions are still recorded.
                robot.Motion.StopAllMotors();
                Wait(3000);
                MotionOutcome? driveOutcome = null;
                try { if (drive.IsCompleted) driveOutcome = await drive; } catch (Exception e) { lock (_exceptions) _exceptions.Add("CLIFF drive: " + e.Message); }
                double t1 = Now();
                JsonArray api; lock (reports) api = new JsonArray(reports.Where(r => r.T >= t0).Select(r => (JsonNode)new JsonObject { ["afterMs"] = Num(r.T - t0), ["report"] = r.R.ToString() }).ToArray());
                res["edge"] = new JsonObject
                {
                    ["toolSentEnableStopOnCliffTrue"] = "tool-only safety send (public SetStopOnCliff, SC8) before driving",
                    ["driveOutcome"] = driveOutcome?.ToString(),
                    ["messages"] = Seq(t0, t1), ["engineLog"] = Logs(t0), ["apiCliffDetected"] = api,
                    ["thresholdAfter"] = robot.Sensors.CliffDetectThreshold, ["suspiciousCount"] = robot.Sensors.SuspiciousCliffCount,
                };
                Ask("CLIFF", "STOPPED_AT_EDGE", "Did he stop at the edge by himself?", new[] { "yes", "caught", "no-edge-reached" }, photo: false);
                Console.WriteLine("Put him back on the table, away from the edge.");
                Wait(3000);
            }
            else { res["edge"] = new JsonObject { ["skipped"] = "operator" }; missing.Add("the operator skipped the edge phase"); }
        }

        // Phase C: lifted by hand, not moving.
        if (!_cancelled)
        {
            Console.WriteLine("\nPHASE LIFT (6 s): pick Cozmo up by hand NOW, hold him level 10 cm above the table, then put him back down on his treads.");
            double t0 = Now(); Wait(6000); Wait(2000); double t1 = Now();
            JsonArray api; lock (reports) api = new JsonArray(reports.Where(r => r.T >= t0).Select(r => (JsonNode)new JsonObject { ["afterMs"] = Num(r.T - t0), ["report"] = r.R.ToString() }).ToArray());
            res["lifted"] = new JsonObject { ["messages"] = Seq(t0, t1), ["engineLog"] = Logs(t0), ["apiCliffDetected"] = api };
            Ask("CLIFF", "LIFTED", "Did you lift him and put him back as asked?", new[] { "yes", "no" }, photo: false);
        }
        robot.Sensors.CliffDetected -= onCliff;
        res["operator"] = OperatorJson("CLIFF");
        var all = Wire().Where(m => !m.Out && (m.Tag == Tag(RobotMessageId.CliffEvent) || m.Tag == Tag(RobotMessageId.PotentialCliff))).ToList();
        int detected; lock (reports) detected = reports.Count;
        res["verdict"] = all.Count == 0 ? "INCONCLUSIVE" : detected > 0 ? "OBSERVED_CLIFF_EVENTS_AND_API_REPORTS" : "OBSERVED_CLIFF_MESSAGES_NO_API_REPORT";
        res["verdictRule"] = $"{all.Count} CliffEvent/PotentialCliff from the robot, {detected} CliffDetected API reports; the order of RobotStopped, StopAllMotors, EnableStopOnCliff and threshold sends, and the HandleCliffEvent logs, are judged by the manager";
        if (all.Count == 0) missing.Add("no CliffEvent or PotentialCliff from the robot");
        if (missing.Count > 0) res["missing"] = missing;
        return res;
    }

    // ------------------------------------------------------------------ M4-018 and the cube-light records: CUBE_LIGHTS

    private static readonly RobotMessageId[] LightTags = { RobotMessageId.SetCubeLights, RobotMessageId.SetCubeGamma, RobotMessageId.SetCubeID };

    private async Task<JsonObject> CubeLightsCheck()
    {
        var robot = _robot!; var res = new JsonObject(); var missing = new JsonArray();
        var cube = robot.Cubes.ConnectedCubes.FirstOrDefault() ?? await robot.Cubes.WaitForConnectionAsync(TimeSpan.FromSeconds(_cubeWaitS));
        if (cube is null) { res["verdict"] = "INCONCLUSIVE"; res["verdictRule"] = "no cube connected"; missing.Add("a connected cube"); res["missing"] = missing; return res; }
        res["cube"] = CubeJson(cube);
        var tags = LightTags.Select(t => (byte)t).ToHashSet();
        res["sinceConnect"] = Histogram(Wire().Where(m => m.Out && tags.Contains(m.Tag)).Select(m => TagName(m.Tag)));
        double tDone = double.NaN;
        double t0 = Now();
        bool played = robot.Lights.Cubes.PlayLightAnim(cube.Type, "Flash", completed: () => tDone = Now());
        Console.WriteLine("Watch the cube: it should flash white.");
        Wait(4000);
        double t1 = Now();
        var sends = Wire().Where(m => m.Out && tags.Contains(m.Tag) && m.T >= t0 && m.T <= t1).ToList();
        res["flash"] = new JsonObject
        {
            ["played"] = played, ["completedAfterMs"] = double.IsNaN(tDone) ? null : Num(tDone - t0),
            ["sends"] = new JsonArray(sends.Take(40).Select(m => (JsonNode)MsgJson(m, t0)).ToArray()),
            ["engineLog"] = new JsonArray(LogSince(t0).Where(l => l.Contains("CubeLight", StringComparison.Ordinal) || l.Contains("M1-034", StringComparison.Ordinal)).Take(30).Select(l => (JsonNode)l).ToArray()),
        };
        string? ans = Ask("CUBE_LIGHTS", "FLASHED", "Did the cube flash white, then go back to its previous lights?", new[] { "yes", "flashed-only", "no" }, photo: false);
        res["operator"] = OperatorJson("CUBE_LIGHTS");
        int cubeLights = sends.Count(m => m.Tag == Tag(RobotMessageId.SetCubeLights));
        res["verdict"] = !played ? "OBSERVED_PLAY_REFUSED" : cubeLights == 0 ? "OBSERVED_NO_CUBE_LIGHTS_SENT" : "OBSERVED_CUBE_LIGHTS_SENT";
        res["verdictRule"] = $"PlayLightAnim(Flash) returned {played}; {cubeLights} CubeLights sends in 4 s; payloads, completion time and the operator's view are judged by the manager";
        if (ans is null && !_yes) missing.Add("no operator answer");
        if (missing.Count > 0) res["missing"] = missing;
        return res;
    }

    // ------------------------------------------------------------------ M4-027: DISCONNECT_QUIET (after teardown)

    private JsonObject DisconnectQuietResult()
    {
        var res = new JsonObject(); var missing = new JsonArray();
        var frames = Frames().Where(f => f.Out && f.Frame is not null).OrderBy(f => f.T).ToList();
        var disc = frames.FirstOrDefault(f => f.Frame!.Type == ReliableMessageType.DisconnectRequest || f.Frame.Messages.Any(m => m.Type == ReliableMessageType.DisconnectRequest));
        if (disc is null) { res["verdict"] = "INCONCLUSIVE"; res["verdictRule"] = "no DisconnectRequest frame was sent"; missing.Add("the teardown sent no DisconnectRequest"); res["missing"] = missing; return res; }
        var wire = Wire().Where(m => m.Out).ToList();
        var after = wire.Where(m => m.T > disc.T).ToList();
        var before = wire.Where(m => m.T >= disc.T - 2000 && m.T <= disc.T).ToList();
        res["disconnectRequestAtMs"] = Num(disc.T);
        res["dataMessagesInLast2sBefore"] = new JsonArray(before.Select(m => (JsonNode)MsgJson(m, disc.T)).ToArray());
        res["dataMessagesAfter"] = new JsonArray(after.Select(m => (JsonNode)MsgJson(m, disc.T)).ToArray());
        res["note"] = "CozmoRobot.Dispose sends StopAllMotors and DriveWheels(0) before disconnecting (a stack decision note, no engine counterpart); they belong before the DisconnectRequest";
        res["verdict"] = after.Count == 0 ? "OBSERVED_NOTHING_AFTER_DISCONNECT" : "OBSERVED_SENDS_AFTER_DISCONNECT";
        res["verdictRule"] = "outbound data messages (motor, light or other) after the first outbound DisconnectRequest frame";
        return res;
    }
}
