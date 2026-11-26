using System.Text.Json;
using Cozmo.Robot;
using Cozmo.Protocol;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Bridge;
using Xunit;
using Fixture=Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

// CLIFF-SPARK-BEHAVIOR-GAPS-1: the listed MISSING sites are either unbound-instance
// seams or wiring guards. These tests pin that production builds the configured owners.
[Collection("SteppedBehavior missing-report statics")]
public sealed class CliffSparkBehaviorGapsTests
{
    // Production reaction rows are the configured instances, so ReactToCliff's
    // TransitionToBackingUp (0x00605318) takes NativeCliffBackUp and ReactToPickup's
    // StartAnim (0x00607A00..0x00607A2C) takes the native SayTextAction branch; the
    // unbound "no back-up drive" and "requires the original Robot speech owner" lines
    // belong to the public standalone constructors only.
    [Fact]
    public void ProductionCliffAndPickupReactionsAreNativeBound()
    {
        using var f=new Fixture();
        var manager=f.Robot.BehaviorManagerOwner!;
        var cliff=Assert.IsType<ReactToCliffBehavior>(Assert.Single(manager.Reactions,r=>r.Strategy.Trigger==ReactionTrigger.CliffDetected).Behavior);
        Assert.True(cliff.NativeBound);
        Assert.Same(f.Runtime.Bound["ReactToCliff"],cliff);
        var pickup=Assert.IsType<ReactToPickupBehavior>(Assert.Single(manager.Reactions,r=>r.Strategy.Trigger==ReactionTrigger.RobotPickedUp).Behavior);
        Assert.True(pickup.NativeBound);
        Assert.Same(f.Runtime.Bound["ReactToPickup"],pickup);
    }

    [Fact]
    public void SessionSevenRawFramesIdentifyTheStopReactionMotion()
    {
        if (Environment.GetEnvironmentVariable("COZMO_SESSION7_CLIFF_BUNDLE") is not { Length: > 0 } bundle) return;
        var raw = RecordedRobotInput.ReadInbound(bundle, time => time >= 187900, 188805,
            [0xD4, 0xC0, 0xF0], discardRetries: true).ToArray();
        Assert.Single(raw.Where(x => x.Message is RobotStopped));
        Assert.Equal(new byte[] { 3, 0 }, raw.Select(x => x.Message).OfType<CliffEvent>().Select(e => e.DetectedFlags));
        Assert.Contains(raw, x => x.At > 188165 && x.At < 188500 && x.Message is RobotState state &&
            state.LwheelSpeedMmps == 0 && state.RwheelSpeedMmps == 0);
        var backward = Assert.Single(raw, x => x.Message is RobotState state && state.Timestamp == 188450);
        var actual = Assert.IsType<RobotState>(backward.Message);
        Assert.Equal(-102f, actual.LwheelSpeedMmps); Assert.Equal(-102f, actual.RwheelSpeedMmps);
        Assert.Equal(new ushort[] { 15, 0, 0, 0 }, actual.CliffDataRaw);
        Assert.True((actual.Status & (uint)RobotStatusFlag.CliffDetected) != 0);
        var captured = RecordedRobotInput.ReadOutbound(bundle, time => time >= 188500, 188805, [0x99, 0x60]).ToArray();
        Assert.Equal(new short[] { -117, 0, 7, 0 }, captured.Select(x => x.Message).OfType<BodyMotion>().Select(body => body.Speed));
        Assert.All(captured.Select(x => x.Message).OfType<BodyMotion>(), body => Assert.Equal(short.MaxValue, body.RadiusMm));
        Assert.Empty(RecordedRobotInput.ReadOutbound(bundle, _ => true, 188805, [0x60]));
        Assert.Single(RecordedRobotInput.ReadOutbound(bundle, time => time >= 187941, 188400, [0x8D]));
        Assert.Empty(RecordedRobotInput.ReadOutbound(bundle, time => time > 187941, 188500, [0x99]));
    }

    [Fact]
    public void ConfiguredCliffStopActionStreamsTheShippedBodyTrackWithoutTogglingCliffStop()
    {
        // An isolated real configured reaction, not a reconstruction of the earlier 100-sample
        // cliff variance or Freeplay state. Native Wait lambda 0x006056B8 accepts the unchanged
        // threshold and stationary body; ctor 0x00604FE4..EA uses no body-lock bit.
        using var f = new Fixture(preloadAnimations: true, pumpAnimations: true, sourceNvEmpty: true, keepNeedsFull: true);
        f.Rig.Robot.SetEnableFreeplayActivity(false); f.Rig.Tick(4000);
        var manager = f.Robot.BehaviorManagerOwner!;
        manager.Activity = null; manager.Stop(BehaviorStopReason.Interrupted, f.Rig.Engine.Timer.SecondsF);
        manager.DisableAllReactionsWithLock("cliff-stop-test-isolation");
        var cliff = Assert.IsType<ReactToCliffBehavior>(manager.Reactions.Single(r => r.Strategy.Trigger == ReactionTrigger.CliffDetected).Behavior);
        f.Motors(RobotStatusFlag.CliffDetected);
        Assert.False(f.Rig.Robot.Motion.BodyMoving);
        var library = f.Rig.Robot.Animations.Library!;
        var group = library.GetGroup(f.Rig.Robot.Animations.DrivingTriggerMap!.GroupFor(AnimationTrigger.ReactToCliffDetectorStop)!)!;
        Assert.Equal("anim_reacttocliff_huh_01", Assert.Single(group.Entries).Name);
        var clip = library.GetClip(group.Entries[0].Name);
        Assert.Equal(new short[] { -117, 7 }, clip.Keyframes.OfType<BodyKeyframe>().Select(k => k.Speed));
        f.Rig.Data(new RobotStopped { Field0 = 1 }); f.Rig.Tick();
        int mark = f.Rig.Mark();
        cliff.StartAsync(f.Runtime.Context, new BehaviorScope(), default).GetAwaiter().GetResult();
        try
        {
            f.Rig.Data(new CliffEvent { Timestamp = f.Robot.StoredState!.Timestamp, DetectedFlags = 3, DidStopForCliff = true });
            bool stopAction = false;
            for (int i = 0; i < 80; i++)
            {
                var stream = f.Rig.Robot.Animations.Scheduler.Stream;
                f.Rig.Data(new AnimationState { NumAnimBytesPlayed = stream.BytesStreamed, NumAudioFramesPlayed = stream.FramesStreamed });
                f.Motors(RobotStatusFlag.CliffDetected);
                if (cliff.OwnedRunner is CompoundActionParallel parent && parent.Children[0] is TriggerLiftSafeAnimationAction action)
                {
                    Assert.Equal(AnimationTrigger.ReactToCliffDetectorStop, action.Request.Trigger);
                    Assert.Equal(0u, action.RequiredTrackMask & CozmoMotion.BodyTrack);
                    stopAction = true;
                }
                var body = f.Rig.SentSince(mark).OfType<BodyMotion>().Select(x => x.Speed).ToArray();
                if (body.Contains((short)-117) && body.Contains((short)7) && body.Last() == 0) break;
            }
            Assert.True(stopAction);
            var sent = f.Rig.SentSince(mark);
            Assert.Equal(new short[] { -117, 0, 7, 0 }, sent.OfType<BodyMotion>().Select(x => x.Speed));
            Assert.All(sent.OfType<BodyMotion>(), x => Assert.Equal(short.MaxValue, x.RadiusMm));
            Assert.DoesNotContain(sent, x => x is EnableStopOnCliff);
            Assert.DoesNotContain(sent.OfType<DisableAnimTracks>(), x => (x.Field0 & CozmoMotion.BodyTrack) != 0);
            Assert.True(f.Rig.Robot.Sensors.CliffDetectedByEvent);
        }
        finally { cliff.Stop(BehaviorStopReason.Interrupted); }
    }

    [Fact]
    public void C2CliffStopReplaySuppressesMotionButPreservesNativeBackupAndOtherTracks()
    {
        // Same configured stop action and the session 7 D4/C0 flags. No hardware or
        // fabricated movement feedback: physical fall prevention is not asserted.
        using var f = new Fixture(preloadAnimations: true, pumpAnimations: true, sourceNvEmpty: true, keepNeedsFull: true,
            engineOptions: new CozmoEngineOptions { ResourcesPath = PickupPyramidNativeExecutionTests.Resources(),
                BlockPoolPath = "", C2 = CozmoC2Settings.McpDefaults });
        using var bridge = new CozmoBridge(f.Rig.Robot);
        Assert.Contains(CozmoC2Settings.CliffHoldStillName,
            bridge.GetState().GetProperty("c2Extensions").EnumerateArray().Select(x => x.GetString()));
        Assert.False(bridge.GetState().GetProperty("cliffHoldActive").GetBoolean());
        f.Rig.Robot.SetEnableFreeplayActivity(false); f.Rig.Tick(4000);
        var manager = f.Robot.BehaviorManagerOwner!;
        manager.Activity = null; manager.Stop(BehaviorStopReason.Interrupted, f.Rig.Engine.Timer.SecondsF);
        manager.DisableAllReactionsWithLock("cliff-stop-test-isolation");
        var cliff = Assert.IsType<ReactToCliffBehavior>(manager.Reactions.Single(r => r.Strategy.Trigger == ReactionTrigger.CliffDetected).Behavior);
        f.Motors(RobotStatusFlag.CliffDetected);
        Assert.False(f.Rig.Robot.Motion.BodyMoving);
        RobotStopped stop = new() { Field0 = 1 };
        CliffEvent detected = new() { DetectedFlags = 3, DidStopForCliff = true };
        if (Environment.GetEnvironmentVariable("COZMO_SESSION7_CLIFF_BUNDLE") is { Length: > 0 } bundle)
        {
            var raw = RecordedRobotInput.ReadInbound(bundle, at => at >= 187900, 188805,
                [0xD4, 0xC0], discardRetries: true).Select(row => row.Message).ToArray();
            stop = Assert.Single(raw.OfType<RobotStopped>());
            detected = raw.OfType<CliffEvent>().Single(e => e.DetectedFlags != 0);
        }

        var stream = f.Rig.Robot.Animations.Scheduler.Stream;
        int queuedSends = 0;
        stream.Buffer(new object(), 5, false, () => { queuedSends++; return true; }, "animBodyMotion");
        stream.Buffer(new object(), 14, false, () => { queuedSends++; return true; }, "animTurnToRecordedHeading");
        int mark = f.Rig.Mark();
        f.Rig.Data(stop); f.Rig.Tick();
        Assert.True(f.Robot.C2CliffHoldActive);
        Assert.True(bridge.GetState().GetProperty("cliffHoldActive").GetBoolean());
        Assert.Equal(0, queuedSends);
        Assert.Contains(f.Rig.SentSince(mark).OfType<DisableAnimTracks>(), x => x.Field0 == CozmoMotion.BodyTrack);
        Assert.False(f.Rig.Robot.Motion.AreAnyTracksLocked(CozmoMotion.BodyTrack));

        cliff.StartAsync(f.Runtime.Context, new BehaviorScope(), default).GetAwaiter().GetResult();
        try
        {
            detected.Timestamp = f.Robot.StoredState!.Timestamp;
            f.Rig.Data(detected);
            bool sawStopAction = false, sawHead = false, sawLift = false, sawFace = false, sawAudio = false;
            byte activeTag = 0;
            int replyMark = f.Rig.Mark();
            DriveStraightAction? backup = null;
            for (int i = 0; i < 300; i++)
            {
                f.Rig.Data(new AnimationState { NumAnimBytesPlayed = stream.BytesStreamed, NumAudioFramesPlayed = stream.FramesStreamed });
                f.Motors(RobotStatusFlag.CliffDetected);
                if (cliff.OwnedRunner is CompoundActionParallel parent && parent.Children[0] is TriggerLiftSafeAnimationAction action)
                {
                    Assert.Equal(AnimationTrigger.ReactToCliffDetectorStop, action.Request.Trigger);
                    Assert.Equal(0u, action.RequiredTrackMask & CozmoMotion.BodyTrack);
                    sawStopAction = true;
                }
                var next = f.Rig.SentSince(replyMark);
                foreach (var message in next)
                {
                    switch (message)
                    {
                        case StartOfAnimation start:
                            activeTag = start.AnimId;
                            f.Rig.Data(new AnimationStarted { AnimId = activeTag });
                            break;
                        case EndOfAnimation:
                            f.Rig.Data(new AnimationEnded { AnimId = activeTag });
                            break;
                    }
                    if (sawStopAction)
                    {
                        sawHead |= message is HeadAngle;
                        sawLift |= message is LiftHeight;
                        sawFace |= message is FaceImage;
                        sawAudio |= message is AudioSample or AudioSilence;
                    }
                }
                replyMark = f.Rig.Mark();
                if (cliff.OwnedRunner is DriveStraightAction drive)
                {
                    backup = drive;
                    break;
                }
            }
            Assert.True(sawStopAction);
            Assert.True(sawHead && sawLift && sawFace && sawAudio);
            var sent = f.Rig.SentSince(mark);
            Assert.DoesNotContain(sent, x => x is BodyMotion or TurnToRecordedHeading);
            Assert.DoesNotContain(sent, x => x is EnableStopOnCliff);
            Assert.DoesNotContain(sent.OfType<EnableAnimTracks>(), x => (x.Field0 & CozmoMotion.BodyTrack) != 0);
            Assert.NotNull(backup);
            Assert.Equal(-60, backup.DistanceMm);
            Assert.Equal(100, backup.SpeedMmps);
            Assert.True(backup.ShouldPlayAnimation);
            for (int i = 0; i < 8 && !f.Rig.SentSince(mark).OfType<AppendPathSegmentLine>().Any(); i++)
                f.Motors(RobotStatusFlag.CliffDetected);
            Assert.Contains(f.Rig.SentSince(mark).OfType<AppendPathSegmentLine>(), line => line.Speed.SpeedMmps == -100);
            Assert.True(f.Robot.C2CliffHoldActive); // C0 still active; backup is not an animation.
            int releaseMark = f.Rig.Mark();
            f.Rig.Data(new CliffEvent { Timestamp = f.Robot.StoredState!.Timestamp, DetectedFlags = 0 });
            f.Motors();
            Assert.False(f.Robot.C2CliffHoldActive);
            // The path owns the actual body lock, not C2. Its native end may enable
            // when no other owner remains, but C2 release must not enable early.
            if (f.Rig.Robot.Motion.AreAnyTracksLocked(CozmoMotion.BodyTrack))
                Assert.DoesNotContain(f.Rig.SentSince(releaseMark).OfType<EnableAnimTracks>(), x => (x.Field0 & CozmoMotion.BodyTrack) != 0);
        }
        finally { cliff.Stop(BehaviorStopReason.Interrupted); }
    }

    [Fact]
    public void C2CliffHoldReleasesAfterUndetectedAndRespectsNativeTrackOwners()
    {
        using var f = new Fixture(sourceNvEmpty: true,
            engineOptions: new CozmoEngineOptions { ResourcesPath = PickupPyramidNativeExecutionTests.Resources(),
                BlockPoolPath = "", C2 = CozmoC2Settings.McpDefaults });
        var motion = f.Rig.Robot.Motion;
        f.Robot.BehaviorManagerOwner!.DisableAllReactionsWithLock("c2-test-isolation");
        motion.LockTracks(CozmoMotion.BodyTrack, "existing");
        int mark = f.Rig.Mark();
        f.Rig.Data(new CliffEvent { Timestamp = f.Robot.StoredState!.Timestamp, DetectedFlags = 3 });
        f.Rig.Tick();
        Assert.True(f.Robot.C2CliffHoldActive);
        Assert.DoesNotContain(f.Rig.SentSince(mark).OfType<DisableAnimTracks>(), x => (x.Field0 & CozmoMotion.BodyTrack) != 0);
        motion.UnlockTracks(CozmoMotion.BodyTrack, "existing");
        Assert.False(motion.AreAnyTracksLocked(CozmoMotion.BodyTrack));
        Assert.DoesNotContain(f.Rig.SentSince(mark).OfType<EnableAnimTracks>(), x => (x.Field0 & CozmoMotion.BodyTrack) != 0);
        f.Rig.Data(new CliffEvent { Timestamp = f.Robot.StoredState!.Timestamp, DetectedFlags = 0 });
        f.Rig.Tick();
        Assert.False(f.Robot.C2CliffHoldActive);
        Assert.Contains(f.Rig.SentSince(mark).OfType<EnableAnimTracks>(), x => x.Field0 == CozmoMotion.BodyTrack);
    }

    [Fact]
    public void C2CliffClearBeforeNativeDecisionRetainsTheHold()
    {
        using var f = new Fixture(sourceNvEmpty: true,
            engineOptions: new CozmoEngineOptions { ResourcesPath = PickupPyramidNativeExecutionTests.Resources(),
                BlockPoolPath = "", C2 = CozmoC2Settings.McpDefaults });
        var manager = f.Robot.BehaviorManagerOwner!;
        manager.DisableAllReactionsWithLock("c2-test-isolation");
        var cliff = Assert.IsType<ReactToCliffBehavior>(manager.Reactions.Single(r => r.Strategy.Trigger == ReactionTrigger.CliffDetected).Behavior);
        f.Rig.Data(new RobotStopped { Field0 = 1 }); f.Rig.Tick();
        cliff.StartAsync(f.Runtime.Context, new BehaviorScope(), default).GetAwaiter().GetResult();
        try
        {
            f.Rig.Data(new CliffEvent { Timestamp = f.Robot.StoredState!.Timestamp, DetectedFlags = 3 });
            f.Rig.Tick();
            int mark = f.Rig.Mark();
            f.Rig.Data(new CliffEvent { Timestamp = f.Robot.StoredState!.Timestamp, DetectedFlags = 0 });
            f.Rig.Tick();
            Assert.True(f.Robot.C2CliffHoldActive);
            Assert.DoesNotContain(f.Rig.SentSince(mark).OfType<EnableAnimTracks>(), x => (x.Field0 & CozmoMotion.BodyTrack) != 0);
        }
        finally { cliff.Stop(BehaviorStopReason.Interrupted); }
        Assert.False(f.Robot.C2CliffHoldActive);
    }

    // Wiring guards: NativeSparkedActivity:62, NativeStackBlocksBehavior:55/:61/:73 and
    // NativePutDownActions:21/:22 throw only when an owner the Robot/runtime constructor
    // builds is absent. Production builds every one of them before any behaviour runs.
    [Fact]
    public void ProductionBuildsEveryGuardedOwner()
    {
        using var f=new Fixture();
        Assert.NotNull(f.Robot.BehaviorManagerOwner);
        Assert.Same(f.Robot,f.Rig.Engine.Robot);
        Assert.NotNull(f.Robot.CarryingOwner);                 // 0x0050FE24..0x0050FE4A
        Assert.NotNull(f.Runtime.Context.AI);
        Assert.Same(f.Runtime.AI,f.Runtime.Context.AI);
        Assert.IsType<NativeStackBlocksBehavior>(f.Runtime.Bound["StackBlocks"]);
        Assert.IsType<NativeStackBlocksBehavior>(f.Runtime.Bound["SparksStackBlock"]);
        // Sparked Reload 0x005B1008: BehaviorManager::FindBehaviorByID(0x3D = PlayArbitraryAnim).
        Assert.Equal(0x3D,(int)BehaviorID.PlayArbitraryAnim);
        Assert.IsType<NativePlayArbitraryAnimBehavior>(f.Runtime.Bound["PlayArbitraryAnim"]);
        // The production factory builds the native Bring class, which asks the real
        // ProgressionUnlockOwner; the legacy BringCubeToBeaconBehavior seam is test-only.
        Assert.IsType<NativeBringCubeToBeaconBehavior>(f.Runtime.Bound["Hiking_BringCubeToBeacon"]);
        Assert.IsType<NativeBringCubeToBeaconBehavior>(f.Runtime.Bound["SparksBringCubeToBeacon"]);
        Assert.DoesNotContain(f.Runtime.Bound.Values,b=>b is BringCubeToBeaconBehavior);
    }

    // Sparked Reload 0x005B10AC: JsonTools::ParseUint8("numberOfRepetitions"). Every
    // shipped Sparked activity carries a value inside 0..255, so the ParseUint8 MISSING
    // at NativeSparkedActivity:89 is unreachable with the shipped files.
    [Fact]
    public void ShippedSparkRepetitionsFitParseUint8AndReachTheOwner()
    {
        using var f=new Fixture();
        string dir=Path.Combine(f.Rig.Engine.Options.ResourcesPath!,"config","engine","behaviorSystem","activities","sparks");
        var files=Directory.GetFiles(dir,"*.json");
        Assert.Equal(14,files.Length);
        var activities=NativeBehaviorRuntime.AllActivities(f.Runtime.Tree).ToList();
        foreach(string file in files)
        {
            using var doc=JsonDocument.Parse(File.ReadAllText(file),new JsonDocumentOptions{CommentHandling=JsonCommentHandling.Skip,AllowTrailingCommas=true});
            var root=doc.RootElement;
            int repetitions=root.GetProperty("numberOfRepetitions").GetInt32();
            Assert.InRange(repetitions,0,255);
            string id=root.GetProperty("activityID").GetString()!;
            var owner=Assert.IsType<NativeSparkedActivity>(activities.Single(a=>a.Id==id).SparkedOwner);
            Assert.Equal(repetitions,owner.Repetitions);
        }
    }
}
