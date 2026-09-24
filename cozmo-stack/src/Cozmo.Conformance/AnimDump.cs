using System.Text;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Transport;

namespace Cozmo.Conformance;

/// <summary>
/// Prints everything an animation contains and everything the player would do with it, without a robot.
///
/// Two halves, so a discrepancy between them is visible rather than inferred. First the decoded asset,
/// every keyframe in timestamp order with its raw fields. Then the executor: the real
/// <see cref="RobotAnimationSink"/> driven against an offline robot, so every message printed is one the
/// live player would genuinely have sent, taken from the transport rather than re-derived.
/// </summary>
public static class AnimDump
{
    public static int Run(string[] a)
    {
        if (a.Length < 3)
        {
            Console.WriteLine("usage: animdump <assets-dir> <clip-name> [--all-clips-in-file] [--out <file>]");
            return 1;
        }
        string assets = a[1], name = a[2];
        string? outPath = null;
        for (int i = 3; i < a.Length - 1; i++) if (a[i] == "--out") outPath = a[i + 1];

        var lib = AnimationLibrary.Open(assets);
        if (!lib.HasClip(name))
        {
            Console.WriteLine($"no clip '{name}'. {lib.ClipNames.Count} clips are loaded.");
            var near = lib.ClipNames.Where(n => n.Contains(name, StringComparison.OrdinalIgnoreCase)).Take(10).ToList();
            if (near.Count > 0) Console.WriteLine("did you mean: " + string.Join(", ", near));
            return 1;
        }

        var sb = new StringBuilder();
        void W(string s) { Console.WriteLine(s); sb.AppendLine(s); }

        var clip = lib.GetClip(name);
        W($"clip: {clip.Name}");
        W($"duration: {clip.DurationMs} ms   keyframes: {clip.Keyframes.Count}   tracks: {clip.Tracks}");
        W("");

        DumpKeyframes(clip, W);
        W("");
        DumpExecution(clip, W);

        if (outPath is not null)
        {
            File.WriteAllText(Path.GetFullPath(outPath), sb.ToString());
            Console.WriteLine($"\nwritten to {Path.GetFullPath(outPath)}");
        }
        return 0;
    }

    /// <summary>What the executor does with each kind of keyframe, and why.</summary>
    private static (string Verdict, string Why) Handling(Keyframe k) => k switch
    {
        HeadKeyframe => ("ACTED ON", "sent as animHeadAngle (0x93) with the duration and the angle plus variability, as the engine streams it"),
        LiftKeyframe => ("ACTED ON", "sent as animLiftHeight (0x94) with the duration and the height plus variability, as the engine streams it"),
        FaceKeyframe => ("ACTED ON", "rendered and sent; blended towards the next face keyframe each frame"),
        BodyKeyframe b when b.EncodedRadius is { } rr =>
            ("ACTED ON", $"sent as animBodyMotion speed={b.Speed} radius={rr}, as the engine does"),
        BodyKeyframe b => ("IGNORED", $"radius '{b.RadiusRaw}' is not a token the engine understands"),
        EventKeyframe => ("ACTED ON", "raised to the caller"),
        AudioKeyframe => ("ACTED ON", "streamed through the audio source on the scheduler tick; silent with no source"),
        LightsKeyframe => ("IGNORED", "the shipping engine never implemented this track from animation assets"),
        FaceAnimationKeyframe => ("IGNORED", "pre-rendered faceAnimations assets are not loaded"),
        RecordHeadingKeyframe => ("IGNORED", "no heading is recorded"),
        TurnToRecordedHeadingKeyframe => ("IGNORED", "depends on a recorded heading"),
        _ => ("IGNORED", "unknown keyframe type"),
    };

    private static void DumpKeyframes(AnimationClip clip, Action<string> W)
    {
        W("=== decoded keyframes, in timestamp order ===");
        W($"{"t_ms",7} {"dur_ms",7} {"track",-7} {"kind",-26} {"executor",-9} detail");
        W(new string('-', 120));

        foreach (var k in clip.Keyframes.OrderBy(x => x.TriggerTimeMs))
        {
            var (verdict, _) = Handling(k);
            string kind = k.GetType().Name.Replace("Keyframe", "");
            string detail = k switch
            {
                HeadKeyframe h => $"angle_deg={h.AngleDeg} ({h.AngleRad:F3} rad) variability_deg={h.VariabilityDeg}",
                LiftKeyframe l => $"height_mm={l.HeightMm} variability_mm={l.VariabilityMm}",
                BodyKeyframe b => $"radius_mm=\"{b.RadiusRaw}\" speed={b.Speed} " +
                                  $"(encoded_radius={(b.EncodedRadius is { } rr ? rr.ToString() : "unrecognised")}, " +
                                  $"straight={b.IsStraight}, turnInPlace={b.IsTurnInPlace}, " +
                                  $"direction={(b.Speed < 0 ? "BACKWARD" : b.Speed > 0 ? "forward" : "stationary")})",
                AudioKeyframe au => $"eventIds=[{string.Join(",", au.EventIds)}] volume={au.Volume:F2} hasAlts={au.HasAlts}",
                LightsKeyframe li => $"L=[{F(li.Left)}] R=[{F(li.Right)}] Front=[{F(li.Front)}] Mid=[{F(li.Middle)}] Back=[{F(li.Back)}]",
                EventKeyframe e => $"event_id=\"{e.EventId}\"",
                FaceAnimationKeyframe fa => $"animName=\"{fa.AnimName}\"",
                FaceKeyframe f => $"face angle={f.Pose.FaceAngle:F1} centre=({f.Pose.FaceCenterX:F1},{f.Pose.FaceCenterY:F1}) " +
                                  $"scale=({f.Pose.FaceScaleX:F2},{f.Pose.FaceScaleY:F2}) " +
                                  $"L.scaleY={f.Pose.Left[EyeParam.EyeScaleY]:F2} R.scaleY={f.Pose.Right[EyeParam.EyeScaleY]:F2}",
                TurnToRecordedHeadingKeyframe t => $"offset_deg={t.OffsetDeg} speed={t.SpeedDegPerSec}",
                _ => "",
            };
            W($"{k.TriggerTimeMs,7} {k.DurationMs,7} {Short(k.Track),-7} {kind,-26} {verdict,-9} {detail}");
        }

        W("");
        W("=== per track ===");
        foreach (var g in clip.Keyframes.GroupBy(k => k.GetType().Name).OrderBy(g => g.Key))
        {
            var (verdict, why) = Handling(g.First());
            W($"  {g.Key,-28} {g.Count(),4}  {verdict,-9} {why}");
        }

        static string F(float[] v) => v.Length == 0 ? "" : string.Join(",", v.Select(x => x.ToString("F2")));
        static string Short(AnimationTrack t) => t.ToString().Replace("Animation", "");
    }

    /// <summary>
    /// Replays the clip through the real executor against an offline robot and prints every message it sent,
    /// with the timeline position at which it went out.
    /// </summary>
    private static void DumpExecution(AnimationClip clip, Action<string> W)
    {
        W("=== what the executor actually sends ===");
        W("The real RobotAnimationSink, driven against an offline robot. Every line below is a message the");
        W("live player would have sent, read back from the transport rather than re-derived.");
        W("");

        var clock = new ManualClock { NowMs = 1000 };
        var robot = CozmoRobot.CreateOffline(TransportOptions.EngineDefaults, clock);
        robot.Transport.OfflineAcceptConnection();
        var sink = new RobotAnimationSink(robot);
        var scheduler = new AnimationScheduler(sink);
        var skipped = new List<string>();
        sink.NotImplemented += s => skipped.Add(s);

        // A reliable message the peer never acks is resent every tick, so the same command appears in the
        // outbound list many times. Counting those would report the player sending far more than it does,
        // so each sequence id is counted once. Unreliable messages carry no id and are counted as they come.
        var countedSeqs = new HashSet<ushort>();
        ushort lastAcked = 0;
        int scanned = 0;
        var log = new List<(double T, RobotMessage M)>();
        double frame = 1000.0 / AnimationScheduler.FrameRateHz;

        scheduler.Play(clip, 0);
        for (double t = 0; t <= clip.DurationMs + 200; t += frame)
        {
            scheduler.Advance(t);
            // Let the connection flush and acknowledge what it sent, exactly as a robot would. The engine
            // spaces packets 2 ms apart and batches, and it only sends one unacked packet per update, so
            // without the acknowledgements the queue stalls and later commands never go out at all.
            for (int i = 0; i < 8; i++)
            {
                clock.Advance(10);
                robot.Transport.OfflineTick();
                Ack(robot, ref lastAcked);
            }

            var all = robot.Transport.OfflineOutbound
                .SelectMany(f => f.Messages)
                .Where(m => m.Type is ReliableMessageType.SingleReliableMessage or ReliableMessageType.SingleUnreliableMessage
                            && m.Payload.Length > 0)
                .ToList();
            for (; scanned < all.Count; scanned++)
            {
                var sm = all[scanned];
                if (sm.Seq != 0 && !countedSeqs.Add(sm.Seq)) continue;      // a resend of one already counted
                log.Add((t, RobotMessage.Parse(sm.Payload)));
            }
        }

        // body commands first, because that is what the robot visibly did
        var bodyMsgs = log.Where(e => e.M is Protocol.BodyMotion).ToList();
        W($"--- body motion commands: {bodyMsgs.Count} ---");
        foreach (var (t, m) in bodyMsgs)
        {
            var b = (Protocol.BodyMotion)m;
            string shape = b.RadiusMm == BodyKeyframe.StraightRadius ? "straight"
                         : b.RadiusMm == 0 ? "turn in place" : $"arc r={b.RadiusMm} mm";
            W($"  t={t,7:F0} ms  BodyMotion speed={b.Speed,6} radius={b.RadiusMm,6}  {shape}" +
              (b.Speed == 0 ? "  (stop)" : b.Speed < 0 ? "  BACKWARD" : ""));
        }
        var moving = bodyMsgs.Where(e => ((Protocol.BodyMotion)e.M).Speed != 0).ToList();
        var bodyFrames = clip.Keyframes.OfType<BodyKeyframe>().ToList();
        for (int i = 0; i < moving.Count; i++)
        {
            double stop = bodyMsgs.SkipWhile(e => e.T <= moving[i].T)
                                  .FirstOrDefault(e => ((Protocol.BodyMotion)e.M).Speed == 0).T;
            W($"  => moving stretch {i + 1}: t={moving[i].T:F0} ms to t={stop:F0} ms, i.e. {stop - moving[i].T:F0} ms");
            if (i < bodyFrames.Count)
                W($"     the asset asks for {bodyFrames[i].DurationTimeMs} ms starting at t={bodyFrames[i].TriggerTimeMs} ms");
        }

        var wheels = log.Where(e => e.M is DriveWheels).ToList();
        W("");
        W($"--- wheel commands: {wheels.Count} ---");
        if (wheels.Count == 0) W("  none");
        foreach (var (t, m) in wheels)
        {
            var d = (DriveWheels)m;
            string dir = d.LwheelSpeedMmps < 0 ? "BACKWARD" : d.LwheelSpeedMmps > 0 ? "forward" : "stop";
            W($"  t={t,7:F0} ms  DriveWheels left={d.LwheelSpeedMmps,7:F1} right={d.RwheelSpeedMmps,7:F1}  {dir}");
        }
        // Measure each moving stretch: from a non-zero command to the first zero after it. Taking the last
        // command instead would count the belt-and-braces stop at the end of the clip and report the wheels
        // as running for the whole animation.
        var moves = new List<(double Start, double Stop, float Speed)>();
        for (int i = 0; i < wheels.Count; i++)
        {
            float sp = ((DriveWheels)wheels[i].M).LwheelSpeedMmps;
            if (sp == 0) continue;
            double stop = wheels.Skip(i + 1).FirstOrDefault(e => ((DriveWheels)e.M).LwheelSpeedMmps == 0).T;
            moves.Add((wheels[i].T, stop, sp));
            while (i + 1 < wheels.Count && ((DriveWheels)wheels[i + 1].M).LwheelSpeedMmps == sp) i++;
        }
        var bodies = clip.Keyframes.OfType<BodyKeyframe>().ToList();
        for (int i = 0; i < moves.Count; i++)
        {
            var m = moves[i];
            W($"  => moving stretch {i + 1}: t={m.Start:F0} ms to t={m.Stop:F0} ms at {m.Speed:F0} mm/s, " +
              $"i.e. {m.Stop - m.Start:F0} ms");
            if (i < bodies.Count)
                W($"     the asset asks for {bodies[i].DurationTimeMs} ms starting at t={bodies[i].TriggerTimeMs} ms");
        }
        if (moves.Count == 0 && bodies.Count > 0)
            W($"  => the asset has {bodies.Count} body keyframe(s) but the wheels were never driven");

        W("");
        W("--- every message, in order ---");
        W($"{"t_ms",7}  {"message",-22} detail");
        W(new string('-', 90));
        foreach (var (t, m) in log)
        {
            string detail = m switch
            {
                SetHeadAngle h => $"angle={h.AngleRad:F3} rad duration={h.DurationSec:F3}s action={h.ActionId}",
                SetLiftHeight l => $"height={l.HeightMm:F1} mm duration={l.DurationSec:F3}s action={l.ActionId}",
                Protocol.HeadAngle ha => $"angle_deg={ha.AngleDeg} duration={ha.DurationTimeMs} ms (animation keyframe)",
                Protocol.LiftHeight lh => $"height_mm={lh.HeightMm} duration={lh.DurationTimeMs} ms (animation keyframe)",
                DriveWheels d => $"left={d.LwheelSpeedMmps:F1} right={d.RwheelSpeedMmps:F1}",
                Protocol.BodyMotion b2 => $"speed={b2.Speed} radius={b2.RadiusMm}",
                Protocol.FaceImage f => $"{f.Image.Length} byte face payload",
                AudioSilence => "silence frame",
                AudioSample => "audio frame",
                _ => "",
            };
            W($"{t,7:F0}  {m.GetType().Name,-22} {detail}");
        }

        W("");
        var counts = log.GroupBy(e => e.M.GetType().Name).OrderByDescending(g => g.Count());
        W("--- totals ---");
        foreach (var g in counts) W($"  {g.Key,-24} {g.Count()}");
        if (skipped.Count > 0)
        {
            W("");
            W("--- reported as not implemented during playback ---");
            foreach (var s in skipped.Distinct()) W($"  {s}");
        }
        robot.Dispose();
    }

    /// <summary>Acknowledges everything sent so far, so the reliable queue drains as it would on a robot.</summary>
    private static void Ack(CozmoRobot robot, ref ushort lastAcked)
    {
        ushort highest = 0;
        foreach (var f in robot.Transport.OfflineOutbound)
            foreach (var m in f.Messages)
                if (m.Seq > highest) highest = m.Seq;
        if (highest == 0 || highest == lastAcked) return;
        lastAcked = highest;
        robot.Transport.ProcessIncoming(FrameCodec.Encode(
            Frame.Single(new SubMessage(ReliableMessageType.Ack, Array.Empty<byte>()), highest)));
    }
}
