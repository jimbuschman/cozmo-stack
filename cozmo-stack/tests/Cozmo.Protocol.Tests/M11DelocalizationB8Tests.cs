using System.Buffers.Binary;
using System.Text.RegularExpressions;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Vision;
using Xunit;
namespace Cozmo.Protocol.Tests;

/// <summary>
/// Job B-M11-8: the fixes from re-analysis/research/20261014-b-m11-1-check.md. Every expected value is the check file's
/// citation (address in each test's summary), never what the code returned.
/// </summary>
[Collection("M5 process statics")]
public sealed class M11DelocalizationB8Tests
{
    private static List<EngineLogEntry> Capture(CozmoEngine engine)
    {
        var list = new List<EngineLogEntry>();
        engine.LogEntry += e => { lock (list) list.Add(e); };
        return list;
    }

    /// <summary>
    /// M11-069, check item 1: FaceWorld::OnRobotDelocalized 0x004F55E4..0x004F563A only erases viz objects (0x004F5614), which produce
    /// no output (V1-V6). Through the live tread-boundary Delocalize (0x00512BA6, as CORE008 drives it), a known face is kept and no
    /// FaceDeleted is raised. Check item 6 (run level): the same delocalize writes no engine log entry keyed "MISSING".
    /// </summary>
    [Fact]
    public void M11_069_ADelocalizeKeepsTheFacesAndLogsNoMissingIntoTheEngineStream()
    {
        using var rig = new Rig();
        var tf = new TrackedFace(new DetectedFace(5, new FaceRect(0, 0, 10, 10), Name: "Jim"), 1000);
        Assert.NotNull(rig.Vision.Faces.AddOrUpdateFace(tf, Pose3d.Identity, false));
        int deleted = 0; rig.Vision.Faces.FaceDeleted += _ => deleted++;
        int delocalized = 0; rig.Vision.RobotDelocalized += _ => delocalized++;
        var log = Capture(rig.Robot.Engine);
        uint before = rig.Robot.Engine.Robot!.CurrentOriginId;
        rig.State(flags: (uint)(RobotStatusFlag.HeadInPos | RobotStatusFlag.LiftInPos | RobotStatusFlag.IsPickedUp));
        Assert.Equal(1, delocalized);
        Assert.Equal(before + 1, rig.Robot.Engine.Robot!.CurrentOriginId);
        Assert.Equal(1, rig.Vision.Faces.Count);
        Assert.Equal(0, deleted);
        lock (log)
        {
            Assert.Contains(log, e => e.Name == "Robot.Delocalize");
            Assert.DoesNotContain(log, e => e.Name == "MISSING" || e.Text.StartsWith("MISSING", StringComparison.Ordinal));
        }
    }

    /// <summary>
    /// M11-069 / M11-053, check item 2: the 101st frame-id mismatch calls Delocalize with the actual carrying state
    /// ([[+0x284]+8] != -1, 0x00512F82..0x00512F96), not status &amp; 2 (that is the tread-boundary site, 0x00512B98). Carrying with
    /// status bit 1 clear therefore passes Y = IsCarryingObject() Y, and D13 (0x00510C5A..0x00510C98) logs no IsCarryingObjectMismatch.
    /// </summary>
    [Fact]
    // reach: unit (native frame/query inputs supplied explicitly; actual history/results, no admission substitution).
    public void M11_053_TheFrameMismatchDelocalizePassesTheCarryingState()
    {
        using var rig = new Rig();
        rig.M.Docking.Carrying.SetCarrying(7);
        var log = Capture(rig.Robot.Engine);
        // Unit fixture advances the native frame after real frame0 samples already exist.
        using(var pose=new Pose3d(rig.Robot.Engine.Robot!.MainPose))rig.Robot.Engine.Robot.SetNewPose(pose);
        void Mismatch() { rig.T += 33; rig.Send(new RobotState { Timestamp = rig.T, PoseOriginId=rig.Robot.Engine.Robot!.CurrentOriginId, PoseFrameId = 0, Pose = new RobotPose(), HeadAngle = -0.15f, Status = (uint)RobotStatusFlag.HeadInPos, Accel = new AccelData { Z = 9800 }, Gyro = new GyroData() }); }
        for (int i = 0; i < 101; i++) Mismatch();
        lock (log)
        {
            Assert.Contains(log, e => e.Name == "Robot.UpdateFullRobotState.MismatchedFrameIDs");
            Assert.Contains(log, e => e.Name == "Robot.Delocalize");
            Assert.DoesNotContain(log, e => e.Name == "Robot.Delocalize.IsCarryingObjectMismatch");
        }
    }

    /// <summary>
    /// M11-069 / M12-027, check item 4: the native runs Delocalize, ComputeLiftPose and the carried-pose write on one thread
    /// (Robot::Update). Here all three take EngineRobot.PoseTreeGate: while another thread holds it, none of them proceeds.
    /// </summary>
    [Fact]
    public void M11_069_DelocalizeLiftPoseAndTheCarriedPoseWriteShareOneGate()
    {
        using var rig = new Rig();
        var r = rig.Robot.Engine.Robot!;
        bool attached = false;
        if (!rig.NoLibrary)
        {
            rig.Cube = new Pose3d(Mat3.Identity, new Vec3(150, 0, 22));
            var cube = Assert.Single(rig.Frame().Objects).Object;
            attached = rig.M.Docking.SetObjectAsAttachedToLift(cube.ObjectId, cube.Markers[0].Code) == 0;
            Assert.True(attached);
        }
        void Blocks(Action a)
        {
            Task t;
            lock (r.PoseTreeGate)
            {
                t = Task.Run(a);
                Assert.False(t.Wait(TimeSpan.FromMilliseconds(300)));
            }
            Assert.True(t.Wait(TimeSpan.FromSeconds(30)));
        }
        Blocks(() => r.ComputeLiftPose(0.1f));
        Blocks(() => r.Delocalize(false));
        if (attached) Blocks(() => rig.M.Docking.UpdateCarriedObjectPose());
    }

    /// <summary>
    /// M11-069 / M11-070, check item 5: one history, Robot+0x390 (SetNewPose 0x00512708..0x0051271C, AddVisionOnlyState 0x005310D8). After
    /// RemoveRobot then AddRobot, the vision system's history is the new robot's, so a Delocalize's vision-only state and the raw states
    /// land in the same history.
    /// </summary>
    [Fact]
    public void M11_070_TheHistoryFollowsTheCurrentRobot()
    {
        using var rig = new Rig();
        Assert.Same(rig.Robot.Engine.Robot!.PoseHistory, rig.Vision.History);
        rig.Robot.Engine.Robots.RemoveRobot(CozmoEngine.RobotId, wasConnecting: false);
        var again = rig.Robot.Engine.Robots.AddRobot(CozmoEngine.RobotId, withRic: false)!;
        Assert.Same(again.PoseHistory, rig.Vision.History);
        again.Delocalize(false);
        Assert.NotEmpty(rig.Vision.History.VisionOnly);
    }

    /// <summary>
    /// M11-053, check item 7: AddNewOrigin (0x00847A84..0x00847AA0) never aborts. A failed AddOriginWithID logs
    /// sVerifyFailedReturnFalse("PoseOriginList.AddNewOrigin.AddWithIDFailed" 0x00847AA8, "VERIFY(%s): ID:%d" 0x00C29737, "success"
    /// 0x00847AD4, id) and returns the attempted ID. Delocalize stores it (0x00510A66..0x00510A70), keeps the unchanged current origin,
    /// runs the rest (the Robot.Delocalize event, the RobotDelocalized tag 0x32) and passes the attempted ID to
    /// BlockWorld::OnRobotDelocalized (0x00510D5A..0x00510D62). There DeleteObjectsFromZombieOrigins keeps the bucket of the
    /// PoseOriginList's current origin ([[this]+0x294]+0x10, 0x00620948..0x00620952), which is unchanged, so a non-localizable (Dirty)
    /// object of the current origin is kept; the argument goes only to CreateLocalizedMemoryMap (r5, 0x006249D6), so the map is created
    /// for the attempted ID.
    /// </summary>
    // reach: unit (direct failing origin insertion/Delocalize; explicit safe app drain replaces the normal tick boundary).
    [Fact]
    public void M11_053_AFailedAddNewOriginVerifiesAndContinuesWithTheAttemptedId()
    {
        using var rig = new Rig();
        if (rig.NoLibrary) return;
        var r = rig.Robot.Engine.Robot!;
        rig.Cube = new Pose3d(Mat3.Identity, new Vec3(150, 0, 22));
        var cube = Assert.Single(rig.Frame().Objects).Object;
        rig.Vision.World.MarkDirty(cube.ObjectId);
        Assert.False(rig.Vision.World.AnyRemainingLocalizableObjects(r.CurrentOriginId)); // only the current-origin rule can keep it
        uint current = r.CurrentOriginId;
        uint attempted = r.OriginNames.NextId;
        Assert.True(r.OriginNames.AddOriginWithId(attempted, null)); // the list now holds the ID AddNewOrigin will try
        var log = Capture(rig.Robot.Engine);
        int app = 0; rig.Robot.Engine.EngineToGameMessage += (tag, _) => { if (tag == 0x32) app++; };
        r.Delocalize(false);rig.Robot.Engine.DrainAppDelivery();
        Assert.Equal(current, r.CurrentOriginId);
        Assert.Equal(current, rig.Vision.World.CurrentOriginId);                     // not the attempted ID
        Assert.NotNull(rig.Vision.World.GetLocatedObjectById(cube.ObjectId));        // the current origin's bucket is kept
        Assert.Equal(attempted, rig.Vision.World.Maps.CurrentOrigin);                // CreateLocalizedMemoryMap(attempted)
        Assert.True(rig.Vision.World.Maps.Maps.ContainsKey(attempted));
        Assert.Equal(1, app);
        lock (log)
        {
            int verify = log.FindIndex(e => e.Name == "PoseOriginList.AddNewOrigin.AddWithIDFailed");
            Assert.True(verify >= 0);
            Assert.Equal(EngineLogLevel.Error, log[verify].Level);
            Assert.EndsWith($"VERIFY(success): ID:{attempted}", log[verify].Text); // the engine's error entry carries its thread prefix ahead of the text
            Assert.True(log.FindIndex(e => e.Name == "Robot.Delocalize") > verify);
        }
    }

    /// <summary>
    /// M11-053 / U3, check item 6: GetOriginByID's bad-ID VERIFY is sVerifyFailedReturnFalse("PoseOriginList.GetOriginByID.BadID"
    /// 0x00847D00, "VERIFY(%s): %d" 0x00C29749, "iter != _origins.end()" 0x00847D24, id) at 0x00847C64.
    /// </summary>
    [Fact]
    public void M11_053_TheBadOriginIdVerifyUsesTheEngineKeyAndFormat()
    {
        using var rig = new Rig();
        var seen = new List<(EngineLogLevel Level, string Key, string Text)>();
        using (Pose3d.UseSourceLog((l, c, k, t) => seen.Add((l, k, t)), () => { }))
            rig.Robot.Engine.Robot!.GetNativeOrigin(999);
        var v = Assert.Single(seen);
        Assert.Equal(EngineLogLevel.Error, v.Level);
        Assert.Equal("PoseOriginList.GetOriginByID.BadID", v.Key);
        Assert.Equal("VERIFY(iter != _origins.end()): 999", v.Text);
    }

    /// <summary>
    /// M11-070, check item 8: SetNewPose's history head angle is Robot+0x2FC. SetHeadAngle (0x00513358) writes it only when +0x314
    /// (calibrated) is set and clamps a value above 0x3F543B67 to 0x3F46D3F2. The rig's head is calibrated, so a report of 1.0 is
    /// stored as 0x3F46D3F2, and the vision-only state SetNewPose adds (head at +0x28) carries those bits.
    /// </summary>
    [Fact]
    public void M11_070_TheHistoryHeadAngleIsTheClampedRobotField()
    {
        using var rig = new Rig();
        rig.Head = 1.0f; rig.State();
        var r = rig.Robot.Engine.Robot!;
        r.Delocalize(false);
        var stamp = r.PoseHistory.VisionOnly[r.StoredState!.Timestamp];
        Assert.Equal(0x3F46D3F2u, BinaryPrimitives.ReadUInt32LittleEndian(stamp.State.AsSpan(0x28)));
    }

    /// <summary>
    /// M11-070, check item 8: with the head not calibrated, +0x2FC keeps the ctor value 0xBEDF66F3 (0x0051007C..0x00510086) whatever
    /// the state reports.
    /// </summary>
    [Fact]
    public void M11_070_AnUncalibratedHeadLeavesTheCtorAngleInTheHistory()
    {
        using var rig = new M4ControlTests.Rig();
        rig.ToSynced();
        rig.State(head: 0.3f);
        var r = rig.Engine.Robot!;
        r.Delocalize(false);
        var stamp = r.PoseHistory.VisionOnly[r.StoredState!.Timestamp];
        Assert.Equal(0xBEDF66F3u, BinaryPrimitives.ReadUInt32LittleEndian(stamp.State.AsSpan(0x28)));
    }

    /// <summary>
    /// M11-069, queued items: the Robot ctor's RobotStateHistory::Clear follows its Delocalize (0x00510328, 0x00510330), so the
    /// ctor's vision-only state is gone; the pivot and lift names are "RobotLiftBase" (0x0050FFF8, 13 characters at 0x005109CC) and
    /// "RobotLift" (0x0051004A, 9 characters at 0x005109DC).
    /// </summary>
    [Fact]
    public void M11_069_TheCtorClearsTheHistoryAndNamesTheLiftPoses()
    {
        using var rig = new M4ControlTests.Rig();
        var r = rig.Engine.Robots.AddRobot(CozmoEngine.RobotId, withRic: false)!;
        Assert.Empty(r.PoseHistory.VisionOnly);
        Assert.Equal("RobotLiftBase", r.LiftPivot.Name);
        Assert.Equal("RobotLift", r.LiftPose.Name);
    }

    /// <summary>
    /// M12-006 / native A3-A5 put-down: after a put-down the cube is off the lift tree, so moving the lift (ComputeLiftPose with a new angle) leaves it where
    /// it was put down. The control step shows the same lift move does move a carried cube.
    /// </summary>
    [Fact]
    public void M12_006_APutDownCubeNoLongerMovesWithTheLift()
    {
        using var rig = new Rig();
        if (rig.NoLibrary) return;
        var r = rig.Robot.Engine.Robot!;
        rig.Cube = new Pose3d(Mat3.Identity, new Vec3(150, 0, 22));
        var cube = Assert.Single(rig.Frame().Objects).Object;
        Assert.Equal(0u, rig.M.Docking.SetObjectAsAttachedToLift(cube.ObjectId, cube.Markers[0].Code));
        r.ComputeLiftPose(0.0f);
        var carriedBefore = cube.Pose.Translation;
        r.ComputeLiftPose(0.5f);
        Assert.NotEqual(carriedBefore, cube.Pose.Translation);              // control: carried, it follows the lift
        rig.M.Docking.ReleaseCarriedObject();
        Assert.Same(r.GetNativeOrigin(r.CurrentOriginId).Node,cube.NativePose.Node!.Parent);
        var placed = cube.Pose.Translation;
        r.ComputeLiftPose(0.0f);
        r.ComputeLiftPose(-0.2f);
        Assert.Equal(placed, cube.Pose.Translation);                        // put down: the lift no longer moves it
        Assert.Equal(PoseState.Dirty, cube.PoseState);                      // M12-006: released Dirty (PoseState 2 at 0x00633456)
    }

    /// <summary>
    /// Coordinator follow-up: no log callback runs while EngineRobot.PoseTreeGate is held. A LogEntry subscriber that needs the gate
    /// from another thread (as a subscriber re-entering the engine would) gets it, so the Robot.Delocalize event (emitted inside the
    /// gated D3..D17) is dispatched after the gate is released, and still before the zombie-origin logs that follow D17.
    /// </summary>
    [Fact]
    public void M11_069_NoLogCallbackRunsUnderThePoseTreeGate()
    {
        using var rig = new Rig();
        var r = rig.Robot.Engine.Robot!;
        bool? gateFree = null;
        var names = new List<string?>();
        rig.Robot.Engine.LogEntry += e =>
        {
            lock (names) names.Add(e.Name);
            if (e.Name == "Robot.Delocalize")
                gateFree = Task.Run(() => { lock (r.PoseTreeGate) { } }).Wait(TimeSpan.FromSeconds(10));
        };
        r.Delocalize(false);
        Assert.True(gateFree);
        lock (names)
        {
            int deloc = names.IndexOf("Robot.Delocalize");
            int zombie = names.FindIndex(n => n is not null && n.StartsWith("BlockWorld.DeleteObjectsFromZombieOrigins", StringComparison.Ordinal));
            Assert.True(deloc >= 0);
            if (zombie >= 0) Assert.True(deloc < zombie);
        }
    }

    /// <summary>
    /// Verifier round 2, the reader side of item 4: ComputeLiftPose (message thread) rewrites the lift node while the vision worker
    /// flattens the carried pose (ObservableObject.Pose, as AddObservedMarkerOccluders reads located.Pose) without the gate. Each node
    /// transform is published whole, so every flatten equals the flatten of one of the published lift transforms: never a torn mix.
    /// The expected set is the flatten of the carried cube at each of the two angles, taken single-threaded first.
    /// </summary>
    [Fact]
    public void M12_027_AFlattenRacingComputeLiftPoseSeesOnlyPublishedTransforms()
    {
        using var rig = new Rig();
        if (rig.NoLibrary) return;
        var r = rig.Robot.Engine.Robot!;
        rig.Cube = new Pose3d(Mat3.Identity, new Vec3(150, 0, 22));
        var cube = Assert.Single(rig.Frame().Objects).Object;
        Assert.Equal(0u, rig.M.Docking.SetObjectAsAttachedToLift(cube.ObjectId, cube.Markers[0].Code));
        (Rotation3d, Vec3) Read() { var p = cube.Pose; return (p.Quaternion, p.Translation); }
        // round 3: ComputeLiftPose rotates by +angle and back by -angle (OV01 L5-L8), so the published lift rotation is the identity up to
        // rounding. For 0.1 against each of 0.6, 1.2, 0.3, 0.45, 0.7, 0.85, 1.0, -0.2, 0.25, 0.55, 0.9 and 1.1 the rotation came out
        // bit-equal, so no angle pair makes a rotation-field tear visible: it would be harmless. What a tear can mix is the translation
        // (three floats): the two angles below publish translations that differ in X and in Z, so a field mix of them is a third value
        // the assertion rejects. Detection stays probabilistic: a torn read has to happen during the run.
        const float a = 0.1f, b = 1.2f;
        r.ComputeLiftPose(a); var liftA = r.LiftPose.Node!.Transform; var atA = Read();
        r.ComputeLiftPose(b); var liftB = r.LiftPose.Node!.Transform; var atB = Read();
        Assert.Equal(liftA.Rotation, liftB.Rotation);              // the observation above, kept so a change is noticed
        Assert.NotEqual(liftA.Translation.X, liftB.Translation.X);
        Assert.NotEqual(liftA.Translation.Z, liftB.Translation.Z);
        Assert.NotEqual(atA.Item2.X, atB.Item2.X);
        Assert.NotEqual(atA.Item2.Z, atB.Item2.Z);
        Assert.NotEqual(atA, atB);
        var seen = new HashSet<(Rotation3d, Vec3)>();
        using var stop = new CancellationTokenSource();
        var writer = Task.Run(() => { for (int i = 0; !stop.IsCancellationRequested; i++) r.ComputeLiftPose((i & 1) == 0 ? a : b); });
        var reader = Task.Run(() =>
        {
            var local = new HashSet<(Rotation3d, Vec3)>();
            for (int i = 0; i < 200_000; i++) local.Add(Read());
            return local;
        });
        Assert.True(reader.Wait(TimeSpan.FromMinutes(2)));
        stop.Cancel();
        Assert.True(writer.Wait(TimeSpan.FromSeconds(30)));
        foreach (var v in reader.Result) seen.Add(v);
        Assert.All(seen, v => Assert.True(v.Equals(atA) || v.Equals(atB), $"torn or foreign flatten: {v}"));
    }

    /// <summary>
    /// M8-014 row 6.2 (research/20261002-R-BEH2-M8-gap1-extraction.md; verifier round 3): a failed GetWithRespectTo(robot)
    /// (0x0056c55c beq.w 0x56c698) logs sWarningF at 0x0056c6a6 with an empty data vector, key "AIWhiteboard.PossibleObject.NoRelPose"
    /// (0x0056c77c), text "Couldnt get pose WRT to robot" (0x0056c7a4), no varargs, then discards the object. Exactly that one Warning
    /// reaches the source sink, and nothing goes to the MISSING channel.
    /// </summary>
    [Fact]
    public void M8_014_AFailedRobotRelativePoseLogsTheEngineNoRelPoseWarning()
    {
        SteppedBehavior.ResetMissingForTests();
        var missing = new List<string>();
        void On(string l) { lock (missing) missing.Add(l); }
        SteppedBehavior.MissingReported += On;
        try
        {
            var h = new AIWhiteboardHandlers(new Cozmo.Robot.Manipulation.AIWhiteboard(new BlockWorld(() => Array.Empty<(uint, ObjectType)>()), () => 0), () => 0)
            {
                TiltRadiansOf = _ => 0f,
                FindLocatedClosestMatchingType = (_, _, _, _, _) => false,
                RobotPose = () => null,
            };
            var log = new List<string>(); h.Log += log.Add;
            var sink = new List<(EngineLogLevel Level, string? Channel, string Key, string Text)>();
            h.SourceLog = (l, c, k, t) => sink.Add((l, c, k, t));
            h.HandleRobotObservedPossibleObject(new Pose3d(Mat3.Identity, new Vec3(3000, 0, 0)), ObjectType.Block_LIGHTCUBE1);
            Assert.Equal((EngineLogLevel.Warning, (string?)null, "AIWhiteboard.PossibleObject.NoRelPose", "Couldnt get pose WRT to robot"), Assert.Single(sink));
            Assert.Empty(log);
            Assert.Empty(h.PossibleObjects);
            lock (missing) Assert.DoesNotContain(missing, l => l.Contains("NoRelPose", StringComparison.Ordinal) || l.Contains("0x0056c54a", StringComparison.Ordinal));
        }
        finally { SteppedBehavior.MissingReported -= On; }
    }

    /// <summary>
    /// Verifier round 2, queued item b (M1-034 isolation of the deferred drain): a throwing dispatch goes to its fault handler and the
    /// later dispatches still run, in order; an unowned throw does not mask an exception already thrown by the gated body.
    /// </summary>
    [Fact]
    public void TheDeferredDrainIsolatesEachDispatchAndNeverMasksTheBodysException()
    {
        var gate = new object();
        var order = new List<string>();
        var faults = new List<Exception>();
        LogCallbackDeferral.RunGated(gate, () =>
        {
            LogCallbackDeferral.Run(() => order.Add("a"));
            LogCallbackDeferral.Run(() => throw new InvalidOperationException("b"), faults.Add);
            LogCallbackDeferral.Run(() => order.Add("c"));
            Assert.Empty(order);                                   // nothing dispatched under the gate
        });
        Assert.Equal(new[] { "a", "c" }, order);
        Assert.Equal("b", Assert.Single(faults).Message);

        var thrown = Assert.Throws<ArgumentException>(() => LogCallbackDeferral.RunGated(gate, () =>
        {
            LogCallbackDeferral.Run(() => throw new InvalidOperationException("unowned"));
            LogCallbackDeferral.Run(() => order.Add("d"));
            throw new ArgumentException("body");
        }));
        Assert.Equal("body", thrown.Message);
        Assert.Equal("d", order[^1]);
    }

    private static string SrcRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !Directory.Exists(Path.Combine(d.FullName, "src", "Cozmo.Robot"))) d = d.Parent;
        return Path.Combine(d!.FullName, "src");
    }

    /// <summary>
    /// Check item 6 (guard), broadened in round 2: a MISSING belongs on the stack channel (SteppedBehavior.ReportMissing), never in the
    /// engine log stream. No source file passes "MISSING" as an engine log key, and no engine-log call (EmitSourceLog, EmitEngineLog,
    /// Source, Verify, Warning, Error, a SourceLog/sink Invoke), however its arguments are split over lines, has a string argument that
    /// contains MISSING anywhere.
    /// </summary>
    [Fact]
    public void NoSourceFileWritesAMissingIntoTheEngineLogStream()
    {
        var call = new Regex(@"(?<!\w)(EmitSourceLog|EmitEngineLog|EmitEngineLogBytes|Source|Verify|Warning|Error)\s*\(|SourceLog\??\.Invoke\(|GetSink\([^()]*\)\??\.Invoke\(|Sink\(");
        var missingLiteral = new Regex(@"""(?:[^""\\]|\\.)*MISSING(?:[^""\\]|\\.)*""");
        var bad = new List<string>();
        foreach (var path in Directory.EnumerateFiles(SrcRoot(), "*.cs", SearchOption.AllDirectories))
        {
            if (path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")) continue;
            var text = File.ReadAllText(path);
            if (text.Contains("\"MISSING\"")) bad.Add($"{Path.GetFileName(path)}: \"MISSING\" literal");
            foreach (Match m in call.Matches(text))
            {
                // the argument list: from the call's '(' to its balancing ')', skipping string contents
                int depth = 0, k = m.Index + m.Length - 1;
                bool inString = false;
                for (; k < text.Length; k++)
                {
                    char c = text[k];
                    if (inString) { if (c == '\\') k++; else if (c == '"') inString = false; continue; }
                    if (c == '"') inString = true;
                    else if (c == '(') depth++;
                    else if (c == ')' && --depth == 0) break;
                }
                var args = text.Substring(m.Index, Math.Min(k + 1, text.Length) - m.Index);
                if (missingLiteral.IsMatch(args))
                {
                    int line = text.Take(m.Index).Count(ch => ch == '\n') + 1;
                    bad.Add($"{Path.GetFileName(path)}:{line}: {args.Split('\n')[0].Trim()}");
                }
            }
        }
        Assert.Empty(bad);
    }
}
