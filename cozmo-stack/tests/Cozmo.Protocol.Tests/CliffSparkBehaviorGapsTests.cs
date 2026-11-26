using System.Text.Json;
using Cozmo.Robot;
using Cozmo.Protocol;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
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
