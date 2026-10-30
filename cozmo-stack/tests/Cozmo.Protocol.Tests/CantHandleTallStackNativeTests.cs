using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using System.Text.Json;
using Xunit;
using Fixture = Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class CantHandleTallStackNativeTests
{
    // Supplied weak control block: its lock result can change between admission and Init.
    private sealed class SuppliedWeak : ITallStackWeakTarget
    {
        internal (uint Bottom, int Height)? Value;
        internal bool Available = true;
        public bool TryLock(out (uint Bottom, int Height)? value) { value = Value; return Available; }
    }
    private static NativeCantHandleTallStackBehavior Configured(Fixture f)
    {
        f.Robot.Progression!.Unlocked.Remove(5);
        f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        return Assert.IsType<NativeCantHandleTallStackBehavior>(f.Runtime.Bound["CantHandleTallStack"]);
    }
    private static void Begin(Fixture f, NativeCantHandleTallStackBehavior behavior, BehaviorScope scope)
        => behavior.StartAsync(f.Runtime.Context, scope, default).GetAwaiter().GetResult();
    private static void SupplyRecipient(Fixture f) => f.Runtime.TallStackOwnerInput = f.Robot.TallStackContainers[0];

    // Configured owner, unavailable canonical input; no claim of an automatic selector.
    [Fact]
    public void MissingInputIsUnavailableRatherThanNativeFalse()
    {
        using var f = new Fixture(); var behavior = Configured(f);
        var result = behavior.EvaluateHostReadiness(f.Runtime.Context);
        Assert.False(result.Available); Assert.False(result.Runnable);
        SupplyRecipient(f);
        result = behavior.EvaluateHostReadiness(f.Runtime.Context);
        Assert.False(result.Available); Assert.False(result.Runnable);
        using var scope = new BehaviorScope(); Begin(f, behavior, scope);
        Assert.True(behavior.InitFailed); Assert.Null(behavior.OwnedRunner);
    }

    // Constructor input unit; JSON is supplied rather than read from a shipped config.
    [Fact]
    public void ConstructorDefaultsAndByteTruncationFollowNativeFields()
    {
        using var f = new Fixture();
        var source = new BehaviorFactoryContext { Manipulation = f.M, NativeRobot = f.Robot, TallStackData = f.Robot.TallStackContainers[0] };
        using var defaults = JsonDocument.Parse("{\"behaviorID\":\"CantHandleTallStack\"}");
        using var defaultBehavior = new NativeCantHandleTallStackBehavior(source, defaults.RootElement);
        Assert.Equal((3f, 3f, 3f, 3f, (byte)3), defaultBehavior.Config);
        using var configured = JsonDocument.Parse("{\"behaviorID\":\"CantHandleTallStack\",\"minimumStackHeight\":259,\"lookingInitialWait_s\":4,\"lookingDownWait_s\":5,\"lookingUpWait_s\":6,\"minBlockMovedThreshold_mm\":7}");
        using var configuredBehavior = new NativeCantHandleTallStackBehavior(source, configured.RootElement);
        Assert.Equal((4f, 5f, 6f, 7f, (byte)3), configuredBehavior.Config);
    }

    // Unit gate seams isolate the three vtable capabilities, not the actual robot state producers.
    [Fact]
    public void AllThreeClassStateCapabilitiesRefuse()
    {
        using var f = new Fixture();
        using var json = JsonDocument.Parse("{\"behaviorID\":\"CantHandleTallStack\"}");
        using var behavior = new NativeCantHandleTallStackBehavior(new BehaviorFactoryContext { Manipulation=f.M, NativeRobot=f.Robot, TallStackData=f.Robot.TallStackContainers[0] }, json.RootElement);
        bool off = false, platform = false, carry = false;
        behavior.RobotState355 = () => off; behavior.RobotState34a = () => platform; behavior.RobotComponent284 = () => carry;
        Assert.True(behavior.IsRunnableBase(f.Runtime.Context));
        off = true; Assert.False(behavior.IsRunnableBase(f.Runtime.Context)); off = false;
        platform = true; Assert.False(behavior.IsRunnableBase(f.Runtime.Context)); platform = false;
        carry = true; Assert.False(behavior.IsRunnableBase(f.Runtime.Context));
    }

    // Direct configured admission with supplied weak lock; no native lifetime producer claimed.
    [Fact]
    public void UnlockPrecedesRefreshAndExpiredWeakRefusesInit()
    {
        using var f = new Fixture(); var behavior = Configured(f); SupplyRecipient(f); f.Add();
        var weak = new SuppliedWeak { Value = (7, 3) }; int refreshes = 0;
        f.Runtime.TallStackWeakInput = () => { refreshes++; return weak; };
        f.Robot.Progression!.Unlocked.Add(5);
        Assert.False(behavior.IsRunnable(f.Runtime.Context)); Assert.Equal(0, refreshes);
        f.Robot.Progression.Unlocked.Remove(5);
        weak.Value = (7, 2);
        Assert.False(behavior.IsRunnable(f.Runtime.Context));
        weak.Value = (7, 3);
        Assert.True(behavior.IsRunnable(f.Runtime.Context)); Assert.Equal(2, refreshes);
        weak.Value = null;
        using var scope = new BehaviorScope(); Begin(f, behavior, scope);
        Assert.True(behavior.InitFailed); Assert.Null(behavior.OwnedRunner);
    }

    [Fact]
    public void LockAndRecipientAvailabilityAreRecheckedAtInit()
    {
        using var f = new Fixture(); var behavior = Configured(f); SupplyRecipient(f); f.Add();
        var weak = new SuppliedWeak { Value = (7, 3) }; f.Runtime.TallStackWeakInput = () => weak;
        Assert.True(behavior.IsRunnable(f.Runtime.Context));
        weak.Available = false;
        using var scope = new BehaviorScope(); Begin(f, behavior, scope);
        Assert.True(behavior.InitFailed); Assert.Null(behavior.OwnedRunner);
        weak.Available = true;
        Assert.True(behavior.IsRunnable(f.Runtime.Context));
        f.Runtime.TallStackOwnerInput = null;
        Begin(f, behavior, scope);
        Assert.True(behavior.InitFailed); Assert.Null(behavior.OwnedRunner);
    }

    [Fact]
    public void AbsentBottomAndKnownEmptyTargetReturnNativeRefusal()
    {
        using var f = new Fixture(); var behavior = Configured(f); SupplyRecipient(f);
        var weak = new SuppliedWeak { Value = (123, 3) }; f.Runtime.TallStackWeakInput = () => weak;
        var readiness = behavior.EvaluateHostReadiness(f.Runtime.Context);
        Assert.True(readiness.Available); Assert.False(readiness.Runnable);
        weak.Value = null;
        readiness = behavior.EvaluateHostReadiness(f.Runtime.Context);
        Assert.True(readiness.Available); Assert.False(readiness.Runnable);
        using var scope = new BehaviorScope(); Begin(f, behavior, scope);
        Assert.True(behavior.InitFailed); Assert.Null(behavior.OwnedRunner);
    }

    [Fact]
    public void DelocalizationBetweenAdmissionAndInitLeavesKnownEmptyWeakPair()
    {
        using var f = new Fixture(); var behavior = Configured(f); SupplyRecipient(f); f.Add();
        var weak = new SuppliedWeak { Value = (7, 3) }; f.Runtime.TallStackWeakInput = () => weak;
        Assert.True(behavior.IsRunnable(f.Runtime.Context));
        f.Robot.Delocalize(false);
        Assert.True(behavior.State!.TargetKnown); Assert.Null(behavior.State.Target);
        using var scope = new BehaviorScope(); Begin(f, behavior, scope);
        Assert.True(behavior.InitFailed); Assert.Null(behavior.OwnedRunner);
    }

    // Direct configured admission and actual ActionList. Terminal result is supplied at the
    // root runner, then the regular completion delivery calls the result-ignoring transition.
    [Fact]
    public void FiveRealChildrenFailureStillStartsDisappointmentAndDelocalizeDoesNotCancel()
    {
        using var f = new Fixture(); var behavior = Configured(f); SupplyRecipient(f); f.Add();
        var weak = new SuppliedWeak { Value = (7, 3) };
        f.Runtime.TallStackWeakInput = () => weak;
        Assert.True(behavior.IsRunnable(f.Runtime.Context));
        using var scope = new BehaviorScope(); Begin(f, behavior, scope);
        var sequence = Assert.IsType<CompoundActionSequential>(f.Robot.ActionList.GetCurrentAction());
        Assert.Same(sequence, behavior.LookingSequence);
        Assert.Equal(5, sequence.Children.Count);
        Assert.IsType<NativeTimedWaitRunner>(sequence.Children[0]);
        Assert.Equal(1f, ((NativeTimedWaitRunner)sequence.Children[0]).Duration);
        Assert.Equal(1f, Assert.IsType<NativeTimedWaitRunner>(sequence.Children[2]).Duration);
        Assert.Equal(1f, Assert.IsType<NativeTimedWaitRunner>(sequence.Children[4]).Duration);
        Assert.Equal(0x12, sequence.Children[1].Type); Assert.Equal(0x12, sequence.Children[3].Type);
        var down = sequence.Children[1].GetType().GetField("Target")!.GetValue(sequence.Children[1]);
        var up = sequence.Children[3].GetType().GetField("Target")!.GetValue(sequence.Children[3]);
        Assert.Equal(BitConverter.UInt32BitsToSingle(0xBEDF66F3), Assert.IsType<float>(down));
        // The shared MoveHead constructor clamps the requested 45 degrees to its 44.5-degree limit.
        Assert.Equal(CozmoMotion.MaxHeadAngleRad, Assert.IsType<float>(up));
        Assert.True(behavior.State!.HasStoredPose); Assert.Same(weak, behavior.State.Target);
        var other = f.Robot.TallStackContainers[1]; other.HasStoredPose = true; other.Target = weak;
        uint tag = behavior.CurrentRunnerTag;
        f.Robot.Delocalize(false);
        Assert.False(behavior.State.HasStoredPose); Assert.Null(behavior.State.Target);
        Assert.False(other.HasStoredPose); Assert.Null(other.Target);
        Assert.Equal(tag, behavior.CurrentRunnerTag);
        sequence.State = 0x03000018; f.Robot.ActionList.Update();
        var disappointment = Assert.IsType<Cozmo.Robot.Animation.TriggerAnimationAction>(f.Robot.ActionList.GetCurrentAction());
        Assert.NotEqual(tag, behavior.CurrentRunnerTag);
        Assert.True(behavior.Update(f.Runtime.Context, f.Rig.Engine.Timer.SecondsF));
        disappointment.State = 0; f.Robot.ActionList.Update();
        Assert.False(behavior.HasCurrentAction);
        Assert.False(behavior.Update(f.Runtime.Context, f.Rig.Engine.Timer.SecondsF));
        behavior.Stop(BehaviorStopReason.Interrupted);
    }

    // Direct configured owner with supplied weak input and supplied terminal head failure;
    // the sequential runner processes the failed child and publishes the root completion.
    [Fact]
    public void FailedHeadChildEndsSequenceAndStillRunsDisappointment()
    {
        using var f = new Fixture(); var behavior = Configured(f); SupplyRecipient(f); f.Add();
        var weak = new SuppliedWeak { Value = (7, 3) }; f.Runtime.TallStackWeakInput = () => weak;
        Assert.True(behavior.IsRunnable(f.Runtime.Context));
        using var scope = new BehaviorScope(); Begin(f, behavior, scope);
        var sequence = Assert.IsType<CompoundActionSequential>(f.Robot.ActionList.GetCurrentAction());
        var wait = Assert.IsType<NativeTimedWaitRunner>(sequence.Children[0]);
        var head = sequence.Children[1];
        wait.State = 0; f.Robot.ActionList.Update();
        head.State = EngineActionResult.TracksLocked; f.Robot.ActionList.Update();
        Assert.IsType<Cozmo.Robot.Animation.TriggerAnimationAction>(f.Robot.ActionList.GetCurrentAction());
        behavior.Stop(BehaviorStopReason.Interrupted);
    }

    // Direct configured owner and supplied weak input; fake motor acknowledgements exercise the
    // real child actions. Wait deadlines must be established only after each head finishes.
    [Fact]
    public void WaitsStartAfterTheirPrecedingHeadCompletes()
    {
        using var f = new Fixture(); var behavior = Configured(f); SupplyRecipient(f); f.Add();
        var weak = new SuppliedWeak { Value = (7, 3) }; f.Runtime.TallStackWeakInput = () => weak;
        Assert.True(behavior.IsRunnable(f.Runtime.Context));
        using var scope = new BehaviorScope(); Begin(f, behavior, scope);
        var sequence = Assert.IsType<CompoundActionSequential>(f.Robot.ActionList.GetCurrentAction());
        var initial = Assert.IsType<NativeTimedWaitRunner>(sequence.Children[0]);
        var down = sequence.Children[1]; var afterDown = Assert.IsType<NativeTimedWaitRunner>(sequence.Children[2]);
        var up = sequence.Children[3]; var afterUp = Assert.IsType<NativeTimedWaitRunner>(sequence.Children[4]);
        f.Rig.Tick(); Assert.True(initial.Deadline >= 0); Assert.Equal(-1f, afterDown.Deadline);
        f.Rig.Tick(1100); Assert.Equal(-1f, afterDown.Deadline);
        for (int i = 0; i < 30 && afterDown.Deadline < 0; i++) f.Motors();
        Assert.True(down.DestructionObserved); Assert.True(afterDown.Deadline >= 0); Assert.Equal(-1f, afterUp.Deadline);
        f.Rig.Tick(1100); Assert.Equal(-1f, afterUp.Deadline);
        for (int i = 0; i < 30 && afterUp.Deadline < 0; i++) f.Motors();
        Assert.True(up.DestructionObserved); Assert.True(afterUp.Deadline >= 0);
        f.Rig.Tick(1100);
        Assert.IsType<Cozmo.Robot.Animation.TriggerAnimationAction>(f.Robot.ActionList.GetCurrentAction());
        behavior.Stop(BehaviorStopReason.Interrupted);
    }

    // Direct configured input: stored bottom pose governs suppression and survives Stop.
    [Fact]
    public void StopPreservesPoseAndSameBottomPoseSuppressesReadmission()
    {
        using var f = new Fixture(); var behavior = Configured(f); SupplyRecipient(f); var bottom = f.Add();
        var weak = new SuppliedWeak { Value = (7, 3) }; f.Runtime.TallStackWeakInput = () => weak;
        Assert.True(behavior.IsRunnable(f.Runtime.Context));
        using var scope = new BehaviorScope(); Begin(f, behavior, scope);
        behavior.Stop(BehaviorStopReason.Interrupted);
        Assert.True(behavior.State.HasStoredPose); Assert.Null(behavior.State.Target);
        Assert.False(behavior.IsRunnable(f.Runtime.Context));
        var otherStack = new SuppliedWeak { Value = (7, 3) }; f.Runtime.TallStackWeakInput = () => otherStack;
        Assert.False(behavior.IsRunnable(f.Runtime.Context)); // same bottom pose, different weak target.
        using var resumed = new BehaviorScope(); behavior.AttachRun(f.Runtime.Context, resumed);
        Assert.True(behavior.Resume(null, f.Rig.Engine.Timer.SecondsF));
        bottom.Pose = new Pose3d(Rotation3d.Identity, new Vec3(169, 0, 33), parent: f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));
        Assert.True(behavior.IsRunnable(f.Runtime.Context)); // x=19 within 20, z=11 exceeds 10.
        f.Robot.Delocalize(false);
        Assert.False(behavior.State!.HasStoredPose);
        Assert.False(behavior.IsRunnable(f.Runtime.Context)); // the old bottom is in the previous origin.
        f.Add(7, 169, 33); // Supplied reobservation in the new origin, not an automatic observation claim.
        Assert.True(behavior.IsRunnable(f.Runtime.Context));
        otherStack.Available = false;
        Assert.True(behavior.Resume(null, f.Rig.Engine.Timer.SecondsF));
        Assert.False(behavior.HasCurrentAction);
        otherStack.Available = true; f.Runtime.TallStackOwnerInput = null;
        Assert.True(behavior.Resume(null, f.Rig.Engine.Timer.SecondsF));
    }
}
