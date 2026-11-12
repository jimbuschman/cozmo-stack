using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Vision;
using Cozmo.Transport;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M11-044, Robot::LocalizeToObject 0x005154B0, the same-origin path S0..S15 (inventory .scratch/localize-same-origin-geometry-census.md).
/// Every test is reach: unit. LocalizeToObject (or PotentialObjectsForLocalizingTo.LocalizeRobot) is called directly on the production-wired
/// LocalizationWorld of a fixture robot; the observed object O is built by the test, not by CreateObjectsFromMarkers; and the computed history
/// entry at O's time is made by the production ComputeAndInsertKeyedState and then given a known pose (substituted input) so the expected
/// geometry is exact with identity object rotations. No emulator oracle exists for the pose-tree arithmetic of S5/S7/S8/S13 (census): those
/// values are checked to a tolerance; the S9 height blend is checked bit-exactly from the binary's constants.
/// </summary>
[Collection("SteppedBehavior missing-report statics")]
public sealed class LocalizeToObjectSameOriginTests
{
    private const ObjectType Cube1 = ObjectType.Block_LIGHTCUBE1;
    private static readonly float W01 = BitConverter.Int32BitsToSingle(0x3DCCCCCD); // 0x00515ABC
    private static readonly float W09 = BitConverter.Int32BitsToSingle(0x3F666666); // 0x00515AC0

    private sealed class Setup : IDisposable
    {
        internal readonly PickupPyramidNativeExecutionTests.Fixture F;
        internal EngineRobot Owner => F.Robot;
        internal BlockWorld World => F.M.World;
        internal readonly uint T;
        internal readonly HistRobotState Hist;
        internal readonly List<EngineLogEntry> Logs = new();
        internal readonly List<string> Missing = new();
        private readonly bool _error;
        private readonly Action<string> _missing;
        internal Pose3d Origin => Owner.GetNativeOrigin(Owner.CurrentOriginId);

        internal Setup(float histYaw = .25f)
        {
            F = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
            F.Rig.Tick(); // send any vision update pending from start-up
            T = Owner.StoredState!.Timestamp;
            using var computed = new HistRobotState();
            Assert.Equal(0u, Owner.PoseHistory.ComputeAndInsertKeyedState(T, computed, out _, out _));
            HistRobotState? hist = null; uint key = 0;
            Assert.Equal(0u, Owner.PoseHistory.GetComputedNative(T, ref hist, true, ref key));
            Hist = hist!;
            // substituted input: the computed entry's pose is (100, 20, 0), yaw histYaw, parented to the current origin
            using var known = new Pose3d(Rotation3d.FromAngleAxis(histYaw, new Vec3(0, 0, 1)), new Vec3(100, 20, 0), parent: Origin);
            lock (Owner.PoseTreeGate) Hist.Pose.Assign(known);
            F.Rig.Engine.LogEntry += Logs.Add;
            SteppedBehavior.ResetMissingForTests(); // reports are once per message per process
            _missing = Missing.Add; SteppedBehavior.MissingReported += _missing;
            _error = EngineErrorState.ErrorFlagSet; EngineErrorState.ErrorFlagSet = false;
        }

        internal ObservableObject Existing(uint id = 7, float x = 150, float y = 0, float z = 30)
        {
            var e = F.Add(id, x, z);
            if (y != 0) e.Pose = new Pose3d(Rotation3d.Identity, new Vec3(x, y, z), parent: Origin);
            e.ActiveId = 1; e.FromDistance = 100;
            return e;
        }

        internal ObservableObject Observed(uint id = 7, float x = 140, float y = 5, float z = 22, Pose3d? parent = null) =>
            new(id, Cube1, CubeGeometry.MarkersFor(Cube1))
            { Pose = new Pose3d(Rotation3d.Identity, new Vec3(x, y, z), parent: parent ?? Origin), OriginId = Owner.CurrentOriginId, LastObservedTimestamp = T };

        internal void SetMainZ(float z)
        {
            using var pose = new Pose3d(Rotation3d.Identity, new Vec3(1, 2, z), parent: Origin);
            lock (Owner.PoseTreeGate) Owner.SetPose(pose);
        }

        internal bool Logged(string name) => Logs.Any(e => e.Name == name);

        public void Dispose()
        {
            SteppedBehavior.MissingReported -= _missing;
            EngineErrorState.ErrorFlagSet = _error;
            F.Dispose();
        }
    }

    private static void AssertXY(Vec3 t, float x, float y)
    {
        Assert.Equal(x, (float)t.X, 3); Assert.Equal(y, (float)t.Y, 3);
    }

    /// <summary>
    /// S0..S16 success with [Robot+0x2C4] = 0 (S9 skipped: 0x0051570E cbz): N = E ∘ O⁻¹ ∘ H. With identity O and E rotations,
    /// H = (100, 20, 0) yaw .25, O = (140, 5, 22), E = (150, 0, 30): N = (110, 15, 8) yaw .25. S10 0x0051513C: +0x2B0 += 1, +0x2C6 := 1, a vision-only
    /// state at O.t; S12 0x005159B6..0x005159C8 overwrites the computed entry in place (new frame, N, the entry's own head and lift from S6 hist[+0x34]/[+0x38]);
    /// S13 0x005159CC makes MAIN = N; S14 0x0051238C writes +0x2B8 = E.ID, +0x2C4 = 1, +0x2BC = 0; S15 0x00515A88 returns 0. S16 (0x00514710,
    /// 0x00513CBE..0x00513CCC): the next tick sends AbsoluteLocalizationUpdate with t = O.t, the S10 frame and N's x/y/yaw.
    /// </summary>
    // reach: unit — LocalizeToObject called directly; the computed entry's pose and O are substituted inputs (see class summary).
    [Fact]
    public void M11_044_SameOriginSuccessWritesHistoryPoseAndSendsTheLocalizationUpdateNextTick()
    {
        using var s = new Setup();
        var e = s.Existing(); var o = s.Observed();
        s.World.DelocalizeRobotFields();                                         // +0x2C4 = 0
        float head = s.Hist.State.HeadAngle, lift = s.Hist.State.LiftAngle;
        uint frame = s.Owner.PoseFrameId; s.Owner.VisionPoseUpdated = 0;
        int mark = s.F.Rig.Mark();
        Assert.Equal(0u, s.World.LocalizeToObject(o, e));
        Assert.Equal(unchecked(frame + 1), s.Owner.PoseFrameId);
        Assert.Equal(1, s.Owner.VisionPoseUpdated);
        Assert.True(s.Logged("Robot.LocalizeToObject"));                          // S0: E.ID 7 != [Robot+0x2B8] (-1)
        // S12: the live computed entry
        HistRobotState? hist = null; uint key = 0;
        Assert.Equal(0u, s.Owner.PoseHistory.GetComputedNative(s.T, ref hist, false, ref key));
        Assert.Same(s.Hist, hist);
        AssertXY(hist!.Pose.Translation, 110, 15); Assert.Equal(8f, (float)hist.Pose.Translation.Z, 3);
        Assert.Equal(.25f, (float)hist.Pose.AngleAroundZ, 4);
        Assert.Equal(s.Owner.PoseFrameId, hist.State.PoseFrameId);
        Assert.Equal(BitConverter.SingleToInt32Bits(head), BitConverter.SingleToInt32Bits(hist.State.HeadAngle));
        Assert.Equal(BitConverter.SingleToInt32Bits(lift), BitConverter.SingleToInt32Bits(hist.State.LiftAngle));
        // S10: the vision-only state
        var stamp = s.Owner.PoseHistory.VisionOnly[s.T];
        AssertXY(stamp.Pose.Translation, 110, 15);
        Assert.Equal(s.Owner.PoseFrameId, BitConverter.ToUInt32(stamp.State, 4));
        // S13: MAIN
        AssertXY(s.Owner.MainPose.Translation, 110, 15);
        // S14
        Assert.Equal(7u, s.World.LocalizedToObjectId); Assert.Equal((byte)1, s.World.Robot2C4); Assert.False(s.World.RobotMovedSinceLocalized);
        Assert.Equal(0, s.World.UnbuiltLocalizeToObjectCalls);
        Assert.False(EngineErrorState.ErrorFlagSet);
        // S16
        Assert.DoesNotContain(s.F.Rig.SentSince(mark), m => m is AbsoluteLocalizationUpdate);
        s.F.Rig.Tick();
        var sent = Assert.Single(s.F.Rig.SentSince(mark).OfType<AbsoluteLocalizationUpdate>());
        Assert.Equal(s.T, sent.Timestamp); Assert.Equal(s.Owner.PoseFrameId, sent.PoseFrameId);
        Assert.Equal(s.Owner.CurrentOriginId, sent.PoseOriginId);
        Assert.Equal(110f, sent.PoseX, 3); Assert.Equal(15f, sent.PoseY, 3); Assert.Equal(.25f, sent.PoseAngleRad, 4);
    }

    /// <summary>
    /// S9 0x0051570A..0x00515760, [Robot+0x2C4] = 1: z = f32(f32(zN × 0.1f) + f32(zRobot × 0.9f)) with 0.1f = 0x3DCCCCCD (0x00515ABC) and
    /// 0.9f = 0x3F666666 (0x00515AC0); zN = 8 (N above), zRobot = Robot::GetPose() +0x28 = 4 (set before the call); x and y unchanged.
    /// </summary>
    // reach: unit — as above; MAIN's z is a substituted input.
    [Fact]
    public void M11_044_HeightBlendWhileLocalizedIsTheEngineF32Expression()
    {
        using var s = new Setup();
        var e = s.Existing(); var o = s.Observed();
        s.SetMainZ(4);
        s.World.SetLocalizedTo(null);                                            // +0x2C4 = 1 (0x005124EE)
        Assert.Equal((byte)1, s.World.Robot2C4);
        Assert.Equal(0u, s.World.LocalizeToObject(o, e));
        float a = 8f * W01; float b = 4f * W09; float expected = a + b;
        var stamp = s.Owner.PoseHistory.VisionOnly[s.T];
        Assert.Equal(BitConverter.SingleToInt32Bits(expected), BitConverter.SingleToInt32Bits((float)stamp.Pose.Translation.Z));
        Assert.Equal(BitConverter.SingleToInt32Bits(expected), BitConverter.SingleToInt32Bits((float)s.Hist.Pose.Translation.Z));
        AssertXY(stamp.Pose.Translation, 110, 15);
        Assert.NotEqual(BitConverter.SingleToInt32Bits(8f), BitConverter.SingleToInt32Bits(expected));
    }

    /// <summary>S2f 0x00515624..0x00515684: no computed state at O.t: CouldNotFindHistoricalPose "Time %d", errG, the lookup's own result, nothing written.</summary>
    // reach: unit — LocalizeToObject called directly with an O whose time has no computed entry.
    [Fact]
    public void M11_044_NoComputedStateReturnsTheLookupResultAndWritesNothing()
    {
        using var s = new Setup();
        var e = s.Existing(); var o = s.Observed(); o.LastObservedTimestamp = s.T + 1;
        HistRobotState? none = null; uint k = 0;
        uint lookup = s.Owner.PoseHistory.GetComputedNative(s.T + 1, ref none, true, ref k);
        Assert.NotEqual(0u, lookup);
        uint frame = s.Owner.PoseFrameId; s.Owner.VisionPoseUpdated = 0;
        Assert.Equal(lookup, s.World.LocalizeToObject(o, e));
        Assert.Contains(s.Logs, l => l.Name == "Robot.LocalizeToObject.CouldNotFindHistoricalPose" && l.Text.Contains($"Time {s.T + 1}"));
        Assert.True(EngineErrorState.ErrorFlagSet);
        Assert.Equal(frame, s.Owner.PoseFrameId); Assert.Equal(0, s.Owner.VisionPoseUpdated);
        Assert.False(s.Owner.PoseHistory.VisionOnly.ContainsKey(s.T + 1));
        Assert.Null(s.World.LocalizedToObjectId);
        AssertXY(s.Hist.Pose.Translation, 100, 20);
    }

    /// <summary>S5f 0x0051592C..0x00515984: O under a foreign root: ObjectPoseOriginMisMatch, errG, 1, nothing written.</summary>
    // reach: unit — O is parented to a test root.
    [Fact]
    public void M11_044_ObservedObjectUnderAForeignRootReturnsOneAndWritesNothing()
    {
        using var s = new Setup();
        using var foreign = new Pose3d("foreign root") { Id = 99 };
        var e = s.Existing(); var o = s.Observed(parent: foreign);
        uint frame = s.Owner.PoseFrameId; s.Owner.VisionPoseUpdated = 0;
        Assert.Equal(1u, s.World.LocalizeToObject(o, e));
        Assert.True(s.Logged("Robot.LocalizeToObject.ObjectPoseOriginMisMatch"));
        Assert.True(EngineErrorState.ErrorFlagSet);
        Assert.Equal(frame, s.Owner.PoseFrameId); Assert.Equal(0, s.Owner.VisionPoseUpdated);
        Assert.False(s.Owner.PoseHistory.VisionOnly.ContainsKey(s.T));
        AssertXY(s.Hist.Pose.Translation, 100, 20);
        Assert.Null(s.World.LocalizedToObjectId);
    }

    /// <summary>
    /// S10f 0x0051577A..0x005157DA: AddVisionOnlyStateToHistory fails: FailedAddingVisionOnlyPoseToHistory, errG, its result (1); the +0x2C6 and
    /// +0x2B0 writes of 0x0051513C persist; S12..S15 do not run. The census names TooOld as the input; in this stack a computed entry older than the
    /// window is culled at the same cutoff, so the add is made to fail through the failed-emplace result instead (substituted RejectEmplaceForTest).
    /// </summary>
    // reach: unit — the emplace failure is substituted.
    [Fact]
    public void M11_044_FailedVisionOnlyAddKeepsTheFlagAndFrameWrites()
    {
        using var s = new Setup();
        var e = s.Existing(); var o = s.Observed();
        Assert.False(s.Owner.PoseHistory.VisionOnly.ContainsKey(s.T));
        uint frame = s.Owner.PoseFrameId; s.Owner.VisionPoseUpdated = 0;
        s.Owner.PoseHistory.RejectEmplaceForTest = true;
        try { Assert.Equal(1u, s.World.LocalizeToObject(o, e)); }
        finally { s.Owner.PoseHistory.RejectEmplaceForTest = false; }
        Assert.True(s.Logged("Robot.LocalizeToObject.FailedAddingVisionOnlyPoseToHistory"));
        Assert.True(EngineErrorState.ErrorFlagSet);
        Assert.Equal(unchecked(frame + 1), s.Owner.PoseFrameId); Assert.Equal(1, s.Owner.VisionPoseUpdated);
        AssertXY(s.Hist.Pose.Translation, 100, 20);                               // S12 not run
        Assert.Null(s.World.LocalizedToObjectId);
    }

    /// <summary>
    /// S13f 0x00515A34..0x00515A86: UpdateCurrPoseFromHistory fails (the newest raw state is under another root, so its pose has no
    /// pose w.r.t. the current origin): FailedUpdateCurrPoseFromHistory, errG, 1; the S10 and S12 writes persist; MAIN and the localization fields do not change.
    /// </summary>
    // reach: unit — a newer raw state under a test root is added to the history directly.
    [Fact]
    public void M11_044_FailedCurrentPoseUpdateKeepsTheHistoryWrites()
    {
        using var s = new Setup();
        var e = s.Existing(); var o = s.Observed();
        using var foreign = new Pose3d("foreign root") { Id = 99 };
        using (var raw = new Pose3d(Rotation3d.Identity, new Vec3(0, 0, 0), parent: foreign))
        {
            var state = HistRobotState.Copy(s.Owner.StoredState!); state.Timestamp = s.T + 30; state.PoseFrameId = 1000;
            Assert.Equal(0u, s.Owner.PoseHistory.AddRawOdomState(s.T + 30, raw, state));
        }
        var main = s.Owner.MainPose.Translation;
        uint frame = s.Owner.PoseFrameId;
        Assert.Equal(1u, s.World.LocalizeToObject(o, e));
        Assert.True(s.Logged("Robot.LocalizeToObject.FailedUpdateCurrPoseFromHistory"));
        Assert.True(EngineErrorState.ErrorFlagSet);
        Assert.Equal(unchecked(frame + 1), s.Owner.PoseFrameId);
        Assert.True(s.Owner.PoseHistory.VisionOnly.ContainsKey(s.T));
        AssertXY(s.Hist.Pose.Translation, 110, 15); Assert.Equal(s.Owner.PoseFrameId, s.Hist.State.PoseFrameId);
        Assert.Equal(main, s.Owner.MainPose.Translation);
        Assert.Null(s.World.LocalizedToObjectId);
    }

    /// <summary>
    /// S14f 0x005159DE..0x00515A32: SetLocalizedTo fails with IdNotSet (0x005123A0 -> 0x005124FC; E's ID -1): SetLocalizedToFail, errG, 1; S10, S12 and
    /// S13 persist (MAIN is already N); no localization field is written.
    /// </summary>
    // reach: unit — E is an unlocated test object whose ID is unset.
    [Fact]
    public void M11_044_SetLocalizedToFailureLeavesTheRobotPoseUpdated()
    {
        using var s = new Setup();
        var e = new ObservableObject(ObservableObject.UnassignedId, Cube1, CubeGeometry.MarkersFor(Cube1))
        { Pose = new Pose3d(Rotation3d.Identity, new Vec3(150, 0, 30), parent: s.Origin), OriginId = s.Owner.CurrentOriginId, PoseState = PoseState.Known };
        e.ActiveId = 1; e.FromDistance = 100;
        var o = s.Observed();
        s.World.DelocalizeRobotFields();
        Assert.Equal(1u, s.World.LocalizeToObject(o, e));
        Assert.True(s.Logged("Robot.SetLocalizedTo.IdNotSet"));
        Assert.True(s.Logged("Robot.LocalizeToObject.SetLocalizedToFail"));
        Assert.True(EngineErrorState.ErrorFlagSet);
        AssertXY(s.Owner.MainPose.Translation, 110, 15);
        AssertXY(s.Hist.Pose.Translation, 110, 15);
        Assert.Null(s.World.LocalizedToObjectId); Assert.Equal((byte)0, s.World.Robot2C4);
    }

    /// <summary>
    /// S11 0x0051583C..0x00515850 after S10: E under another root takes the cross-origin branch (census LOCALIZE-CROSS-ORIGIN-1). Here E's root is a test root
    /// whose ID (99) is not in the origin list. X2 (0x00515854..0x005158D0): the RejiggeringOrigins event. X6 (0x00847D94..0x00847D98): the UnknownOrigin VERIFY,
    /// Rejigger returns 1 with no change. X8 (0x0051591A..0x0051592A): the RejiggerFailed VERIFY, no recipients, no Flatten, then S12..S15 (0x005159B6). The
    /// failure-exit table: S10 persists (+0x2B0 += 1, the vision-only state rooted at E's root); S12 writes N = (110, 15, 8) into the computed entry; S13
    /// (0x005135F8, GetWithRespectTo(state, current origin) at 0x0051365A) cannot express that newest state in the current origin, so
    /// FailedUpdateCurrPoseFromHistory and 1, S14 not reached. Next tick (X16, 0x00512734): the vision-only state's root ID is not a list origin (0x0051276E),
    /// so nothing is sent.
    /// </summary>
    // reach: unit — E is parented to a test root (no engine producer of a non-list root is known: census X8 reachability UNKNOWN).
    [Fact]
    public void M11_044_ARejiggerFailureLogsTheVerifyAndFallsThroughToS12()
    {
        using var s = new Setup();
        using var foreign = new Pose3d("foreign root") { Id = 99 };
        var e = s.Existing(); e.Pose = new Pose3d(Rotation3d.Identity, new Vec3(150, 0, 30), parent: foreign);
        var o = s.Observed();
        s.World.DelocalizeRobotFields();                                         // +0x2C4 = 0: S9 skipped
        uint frame = s.Owner.PoseFrameId; uint current = s.Owner.CurrentOriginId; string name = s.Origin.Name;
        int mark = s.F.Rig.Mark();
        Assert.Equal(1u, s.World.LocalizeToObject(o, e));
        Assert.Equal($"Robot {(int)s.Owner.NativeRobotId}'s current origin is {name}, about to localize to origin foreign root.",
            s.Logs.Last(l => l.Name == "Robot.LocalizeToObject.RejiggeringOrigins").Text);
        Assert.True(s.Logged("PoseOriginList.Rejigger.UnknownOrigin"));
        Assert.True(s.Logged("Robot.LocalizeToObject.RejiggerFailed"));
        Assert.True(s.Logged("Robot.LocalizeToObject.FailedUpdateCurrPoseFromHistory"));
        Assert.False(s.Logged("Robot.LocalizeToObject.SetLocalizedToFail"));
        Assert.Equal(unchecked(frame + 1), s.Owner.PoseFrameId);                // S10
        Assert.True(s.Owner.PoseHistory.VisionOnly.ContainsKey(s.T));
        AssertXY(s.Hist.Pose.Translation, 110, 15);                              // S12
        Assert.Equal(current, s.Owner.CurrentOriginId);                          // no Rejigger change, no recipients
        Assert.Equal(name, s.Origin.Name);
        Assert.Null(s.World.LocalizedToObjectId);
        Assert.DoesNotContain(s.Missing, m => m.Contains("LOCALIZE-CROSS-ORIGIN-1"));
        s.F.Rig.Tick();
        Assert.DoesNotContain(s.F.Rig.SentSince(mark), m => m is AbsoluteLocalizationUpdate);
        Assert.True(s.Logged("Robot.SendAbsLocUpdate.InvalidPoseOriginID"));
    }

    /// <summary>
    /// Multi path (LocalizeRobot 0x0050D3DA..0x0050D4D2): the farther current-origin pair localizes first and overwrites the computed entry at O.t (S12), which
    /// sets [Robot+0x2C4] = 1 (S14). The second pair's E is under another root, so it runs S2..S10 on that overwritten entry (S12r: H' = N1 = (110, 15, 8) yaw .25),
    /// with the S9 blend: N2 = E2·O2⁻¹·H' = (-30, 10, -14), z blended with MAIN's 8 to f32(f32(-14 × 0.1f) + f32(8 × 0.9f)). Then the cross-origin branch:
    /// E2's root is not a list origin, so RejiggerFailed (X8) and S12 writes N2; S13 fails (its newest state is rooted at E2's root) and the pair returns 1,
    /// which LocalizeRobot returns (0x0050D4D2). Each pair's S10 advanced the frame (0x00515144..0x00515152); [Robot+0x2B8] stays the first pair's 7.
    /// </summary>
    // reach: unit — PotentialObjectsForLocalizingTo is built and filled directly; inputs as above.
    [Fact]
    public void M11_044_ACrossOriginSecondPairRunsOnTheFirstPairsEntryAndFallsThroughAfterRejiggerFailed()
    {
        using var s = new Setup();
        using var foreign = new Pose3d("foreign root") { Id = 99 };
        var e1 = s.Existing(); var o1 = s.Observed();
        var e2 = new ObservableObject(8, ObjectType.Block_LIGHTCUBE2, CubeGeometry.MarkersFor(ObjectType.Block_LIGHTCUBE2))
        { Pose = new Pose3d(Rotation3d.Identity, new Vec3(0, 0, 0), parent: foreign), OriginId = 99, PoseState = PoseState.Known, LastObservedTimestamp = s.T };
        e2.ActiveId = 2; e2.FromDistance = 100;
        var o2 = new ObservableObject(8, ObjectType.Block_LIGHTCUBE2, CubeGeometry.MarkersFor(ObjectType.Block_LIGHTCUBE2))
        { Pose = new Pose3d(Rotation3d.Identity, new Vec3(140, 5, 22), parent: s.Origin), OriginId = s.Owner.CurrentOriginId, LastObservedTimestamp = s.T };
        s.World.DelocalizeRobotFields();
        // substituted: E1's last pose update has no computed entry, so Insert's P5 (0x0050CEB4..0x0050CF86) does not discard the pair
        if (s.World.EntryFor(7) is { } entry) entry.LastPoseUpdatedTime = s.T - 1;
        uint frame = s.Owner.PoseFrameId; uint current = s.Owner.CurrentOriginId;
        var p = new PotentialObjectsForLocalizingTo(s.World, s.Owner.PoseHistory.GetComputedNativePoseAt);
        Assert.Equal(1, p.Insert(o1, e1, 100f, false, false));
        Assert.Equal(1, p.Insert(o2, e2, 50f, false, false));
        Assert.Equal(1u, p.LocalizeRobot());
        Assert.Equal(unchecked(frame + 2), s.Owner.PoseFrameId);                  // both pairs' S10
        AssertXY(s.Hist.Pose.Translation, -30, 10);                               // the second pair's S12, from the first pair's S12
        float fromObservation = -14f * W01, fromRobot = 8f * W09; float z = fromObservation + fromRobot;
        Assert.Equal(z, (float)s.Hist.Pose.Translation.Z, 3);
        Assert.True(s.Logged("Robot.LocalizeToObject.RejiggerFailed"));
        Assert.Equal(7u, s.World.LocalizedToObjectId);
        Assert.Equal(current, s.Owner.CurrentOriginId);
        Assert.DoesNotContain(s.Missing, m => m.Contains("LOCALIZE-CROSS-ORIGIN-1"));
    }

    /// <summary>
    /// Multi path with the other-origin pair farther (LocalizeRobot 0x0050D354..0x0050D3D6, moved here from M11RVisBuildTests' former two-roots case): the
    /// current-origin element is not first, so its matched pointer is cleared and its ID stored (StoringMatchedObjectID); the farther, other-origin pair goes first
    /// (S2..S10, then RejiggerFailed because its root is not a list origin, S12, and the S13 failure: CrossFrameLocalizeFailure), then the current-origin one is
    /// re-found by ID (GetLocatedObjectByIdHelper, family -1) and takes S11 -> S12..S15: its SetLocalizedTo is last, so [Robot+0x2B8] ends as its ID, and the
    /// history frame advanced once per S10.
    /// </summary>
    // reach: unit — PotentialObjectsForLocalizingTo is built and filled directly; inputs as above.
    [Fact]
    public void M11_044_FartherOtherOriginPairGoesFirstAndTheCurrentOneIsReFoundById()
    {
        using var s = new Setup();
        using var foreign = new Pose3d("foreign root") { Id = 99 };
        var near = s.Existing(); var o1 = s.Observed();
        if (s.World.EntryFor(7) is { } entry) entry.LastPoseUpdatedTime = s.T - 1;   // substituted as above (P5)
        var far = new ObservableObject(8, ObjectType.Block_LIGHTCUBE2, CubeGeometry.MarkersFor(ObjectType.Block_LIGHTCUBE2))
        { Pose = new Pose3d(Rotation3d.Identity, new Vec3(0, 0, 0), parent: foreign), OriginId = 99, PoseState = PoseState.Known, LastObservedTimestamp = s.T };
        far.ActiveId = 2; far.FromDistance = 100;
        var o2 = new ObservableObject(8, ObjectType.Block_LIGHTCUBE2, CubeGeometry.MarkersFor(ObjectType.Block_LIGHTCUBE2))
        { Pose = new Pose3d(Rotation3d.Identity, new Vec3(140, 5, 22), parent: s.Origin), OriginId = s.Owner.CurrentOriginId, LastObservedTimestamp = s.T };
        var lines = new List<string>(); s.World.Log += lines.Add;
        uint frame = s.Owner.PoseFrameId;
        var p = new PotentialObjectsForLocalizingTo(s.World, s.Owner.PoseHistory.GetComputedNativePoseAt);
        Assert.Equal(1, p.Insert(o1, near, 50f, false, false));
        Assert.Equal(1, p.Insert(o2, far, 100f, false, false));
        Assert.Equal(1u, p.LocalizeRobot());                                     // the cross-origin pair's failure (fp, 0x0050D4D2)
        Assert.Contains(lines, l => l.Contains("StoringMatchedObjectID") && l.Contains("ID=7"));
        Assert.Contains(lines, l => l.Contains("CrossFrameLocalizeFailure"));
        Assert.True(s.Logged("Robot.LocalizeToObject.RejiggerFailed"));
        Assert.Equal(0, s.World.UnbuiltLocalizeToObjectCalls);
        Assert.Equal(7u, s.World.LocalizedToObjectId);
        Assert.Equal(unchecked(frame + 2), s.Owner.PoseFrameId);                  // one S10 per pair
        Assert.Equal(s.Owner.PoseFrameId, s.Hist.State.PoseFrameId);              // the second (current-origin) S12
    }

    /// <summary>
    /// LOCALIZE-PRODUCTION-PATH-TEST-1 (M11-044, with M11-006 / M11-050 for the stored pose). Real rendered cube frames through the queued worker:
    /// the dispatch, ComputeAndInsertKeyedState, CreateObjectsFromMarkers, Insert and LocalizeRobot, on a connected cube in the current origin.
    /// Engine facts asserted (census .scratch/LIVE-OBJECT-POSE-NATIVE-1-census.md, research/20261111-localize-same-origin-claude-check.md):
    /// <list type="bullet">
    /// <item>the stored cube pose is a native world pose: ClampPoseToFlat writes the rotation into its input in place (0x008774D0..0x008774E8), the input being
    ///   the root pose from GetWithRespectToRoot (0x00625540..0x00625578), and InitPose/SetPose copy it with its parent (0x004EF598..0x004EF5A2). So it is
    ///   unflagged, its root is the current origin and PoseWorld is this world (census L7..L11, section 4);</item>
    /// <item>S10 (0x0051513C): the pose frame ID goes up by one and one vision-only state is added, at O's time;</item>
    /// <item>S14 (0x0051238C, 0x00512458/0x0051245E): [Robot+0x2B8] = the cube's ObjectID, [Robot+0x2C4] = 1;</item>
    /// <item>S15 (0x00515A88) / S16 (0x00514710, 0x00513CBE..0x00513CCC): exactly one AbsoluteLocalizationUpdate (0x45), carrying the S10 frame ID and O's time.
    ///   S10 also sets [Robot+0x2C6], so on this path the 0x45 is the observable of the +0x2C6 write; S15's own write is checked by the unit test above;</item>
    /// <item>no visible MISSING naming LIVE-OBJECT-POSE-NATIVE-1 (the stack's fields-only guard is not taken).</item>
    /// </list>
    /// The second sighting localizes: the first creates the PoseConfirmation and returns false (0x0050689A..0x00506982), the second returns Count > 1
    /// (0x00506AC2..0x00506ACC), so the object is inserted and LocalizeRobot runs. The first sighting sends no 0x45.
    /// </summary>
    // reach: production — real rendered frames through the queued worker. Substitutions: the offline test engine's offline flag, cleared by reflection
    // after the first sighting so SetLocalizedTo takes the online (native camera) branch an online engine takes (since C1 the result is the same with it
    // set); and the fixture's sim firmware report and outer calibration (PickupPyramidNativeExecutionTests.Fixture.Image), where sim gates only the
    // rolling-shutter shift, which this join does not use.
    [Fact]
    public void M11_044_ALiveSightingOfAConnectedCubeLocalizesThroughTheNativePath()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        var missing = new System.Collections.Concurrent.ConcurrentQueue<string>();
        void Missing(string line) => missing.Enqueue(line);
        SteppedBehavior.MissingReported += Missing;
        try
        {
            M4ControlTests.ConnectCube(f.Rig, slot: 0);
            var world = f.M.World;
            int first = f.Rig.Mark();
            f.Image(1, cube: true, cubeX: 150); f.Motors();                     // the first sighting, on the offline engine
            Assert.Empty(f.Rig.SentSince(first).OfType<AbsoluteLocalizationUpdate>());
            typeof(CozmoEngine).GetProperty("IsOfflineEngine", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .SetValue(f.Robot.Engine, false);
            uint? cubeId = null;
            bool localized = false;
            for (uint i = 2; i <= 2 && !localized; i++)
            {
                uint frame = f.Robot.PoseFrameId;
                var before = f.Robot.PoseHistory.VisionOnly.Keys.ToHashSet();
                int mark = f.Rig.Mark();
                f.Image(i, cube: true, cubeX: 150); f.Motors();
                var sent = f.Rig.SentSince(mark).OfType<AbsoluteLocalizationUpdate>().ToList();
                if (f.Robot.PoseFrameId == frame) { Assert.Empty(sent); continue; }
                localized = true;
                // S10: the frame ID once, and exactly one new vision-only state
                Assert.Equal(unchecked(frame + 1), f.Robot.PoseFrameId);
                var added = f.Robot.PoseHistory.VisionOnly.Keys.Where(k => !before.Contains(k)).ToList();
                uint stampTime = Assert.Single(added);
                Assert.Equal(f.Robot.PoseFrameId, BitConverter.ToUInt32(f.Robot.PoseHistory.VisionOnly[stampTime].State, 4));
                // S15/S16: exactly one 0x45, with the S10 frame and O's time
                var update = Assert.Single(sent);
                Assert.Equal(f.Robot.PoseFrameId, update.PoseFrameId);
                Assert.Equal(stampTime, update.Timestamp);
                Assert.Equal(f.Robot.CurrentOriginId, update.PoseOriginId);
                // S14
                cubeId = world.ConnectedObjectIdForActiveId(0);
                Assert.NotNull(cubeId);
                Assert.Equal(cubeId, world.LocalizedToObjectId);
                Assert.Equal((byte)1, world.Robot2C4);
            }
            Assert.True(localized, "the second sighting did not localize the robot to the connected cube (0x00506AC2..0x00506ACC)");
            // census L7..L11 and section 4: the stored pose is unflagged, rooted at the current origin, owned by this world
            var stored = world.GetLocatedObjectByIdHelper(cubeId!.Value);
            Assert.NotNull(stored);
            Assert.False(stored!.NativePose.HasLegacyFrameInput);
            Assert.True(stored.NativePose.HasParent);
            Assert.True(PoseTreeNode.HasSameRoot(stored.NativePose.Node, f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId).Node));
            Assert.Same(world, stored.PoseWorld);
            Assert.DoesNotContain(missing, m => m.Contains("LIVE-OBJECT-POSE-NATIVE-1"));
        }
        finally { SteppedBehavior.MissingReported -= Missing; }
    }

    /// <summary>
    /// POSE-SNAPSHOT-FLAG-CACHE-1 (stack machinery, no engine counterpart; the queue row is the inventory). The public Pose snapshot is reused
    /// while transform, parent and name are unchanged, and rebuilt when only the embedded pose's legacy frame flag changes.
    /// </summary>
    // reach: unit — the getter is read directly; the flag change is made in place on the embedded pose, leaving transform, parent and name as they were.
    [Fact]
    public void PoseSnapshotFlagCache1_AFlagOnlyChangeRefreshesTheSnapshot()
    {
        using var s = new Setup();
        var e = s.Existing();
        var first = e.Pose;
        Assert.Same(first, e.Pose); // unchanged: the cached instance
        Assert.False(first.HasLegacyFrameInput);
        var transform = e.NativePose.Transform; var parent = e.NativePose.Node!.Parent; var name = e.NativePose.Name;
        using (var flagged = Pose3d.FromFlatRotation(Rotation3d.Identity, new Vec3(0, 0, 0)))
            e.NativePose.KeepInputFrameContextOf(flagged);
        Assert.True(e.NativePose.HasLegacyFrameInput);
        Assert.Equal(transform, e.NativePose.Transform); Assert.Same(parent, e.NativePose.Node!.Parent); Assert.Equal(name, e.NativePose.Name);
        var second = e.Pose;
        Assert.NotSame(first, second);
        Assert.True(second.HasLegacyFrameInput);
        Assert.Same(second, e.Pose); // unchanged again: cached
    }

    /// <summary>
    /// Regression guard (stack limitation, not engine behaviour). An existing object whose pose genuinely carries the stack's legacy frame flag (a flat
    /// host or offline input, here a Mat3-constructed pose) cannot take the native SetLocalizedTo (S14) camera route on an online engine. Running S2..S13 and
    /// then failing S14 would let S10's +0x2C6 send an AbsoluteLocalizationUpdate while the robot stayed unlocalized, so that case keeps the stack's earlier
    /// result: the SetLocalizedTo field writes, no pose-frame or history change, no 0x45, and a visible MISSING.
    /// </summary>
    // reach: unit — LocalizeToObject called directly; E's flagged pose is a substituted host input, and the offline test engine's offline flag is cleared
    // by reflection so SetLocalizedTo's native-owner check takes the online branch.
    [Fact]
    public void M11_044_AFlaggedHostPoseLocalizesByFieldsAndSendsNoUpdate()
    {
        using var s = new Setup();
        var e = s.Existing();
        // a different translation from Existing()'s (150, 0, 30), so the public Pose snapshot is refreshed from the flagged embedded pose
        using (var flagged = new Pose3d(Mat3.Identity, new Vec3(151, 0, 30)))
        {
            flagged.SetParent(s.Origin);
            e.Pose = flagged;
        }
        Assert.True(e.NativePose.HasLegacyFrameInput);
        Assert.True(e.Pose.HasLegacyFrameInput);
        var o = s.Observed();
        typeof(CozmoEngine).GetProperty("IsOfflineEngine", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(s.Owner.Engine, false);
        s.World.DelocalizeRobotFields();
        uint frame = s.Owner.PoseFrameId; s.Owner.VisionPoseUpdated = 0;
        int mark = s.F.Rig.Mark();
        Assert.Equal(0u, s.World.LocalizeToObject(o, e));
        Assert.Contains(s.Missing, m => m.Contains("LIVE-OBJECT-POSE-NATIVE-1"));
        Assert.Equal(1, s.World.UnbuiltLocalizeToObjectCalls);
        Assert.Equal(frame, s.Owner.PoseFrameId); Assert.Equal(0, s.Owner.VisionPoseUpdated);
        Assert.False(s.Owner.PoseHistory.VisionOnly.ContainsKey(s.T));
        AssertXY(s.Hist.Pose.Translation, 100, 20);
        Assert.Equal(7u, s.World.LocalizedToObjectId); Assert.Equal((byte)1, s.World.Robot2C4);
        s.F.Rig.Tick();
        Assert.DoesNotContain(s.F.Rig.SentSince(mark), m => m is AbsoluteLocalizationUpdate);
    }
}
