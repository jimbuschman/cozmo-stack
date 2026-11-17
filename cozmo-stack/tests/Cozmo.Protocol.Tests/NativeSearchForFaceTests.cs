using System.Net;
using Cozmo.Conformance;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Vision;
using Xunit;
using Fixture = Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class NativeSearchForFaceTests
{
    private static NativeSearchForFaceBehavior Search(Fixture f) =>
        Assert.IsType<NativeSearchForFaceBehavior>(f.Runtime.Bound["VC_SearchForFace"]);

    private static void AddFace(Fixture f, int id, bool currentOrigin = true)
    {
        var origin = f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId);
        using var oldOrigin = new Pose3d("Old origin") { Id = 700 };
        var face = new TrackedFace(new DetectedFace(id, new FaceRect(100, 60, 70, 80)), f.Robot.StoredState!.Timestamp)
        {
            HeadPose = new Pose3d(Rotation3d.Identity, new Vec3(200, 0, 60), parent: currentOrigin ? origin : oldOrigin),
        };
        Assert.NotNull(f.M.Vision.Faces.AddOrUpdateNativeFace(face, f.Robot.GetCheckedMainPose(), false,
            currentOrigin ? origin.Node! : oldOrigin.Node!));
    }

    private static TriggerAnimationAction Animation(NativeSearchForFaceBehavior behavior, AnimationTrigger trigger)
    {
        var action = Assert.IsType<TriggerAnimationAction>(behavior.OwnedRunner);
        Assert.Equal(new DrivingAnimationRequest(trigger, 1, true, 0, 60f, false), action.Request);
        return action;
    }

    [Fact]
    public void ConfiguredSelectionRunsSearchThenFoundAndDeliversHarnessReports()
    {
        using var f = new Fixture(preloadAnimations: true, pumpAnimations: true, sourceNvEmpty: true);
        var behavior = Search(f);
        var step = Assert.Single(PersonalityHw.SelectSteps("search-for-face"));
        Assert.Null(step.Check!.Missing);
        var run = new M15CubeRun("", "", IPAddress.Loopback, "", [], [], false, false) { Personality = true };
        run.SubscribeBehaviorReports(f.Rig.Engine);
        Assert.True(PersonalityHw.TryRequestSearchForFace(f.Rig.Robot));
        f.Rig.Tick();
        Assert.Same(behavior, f.Robot.BehaviorManagerOwner!.Current);
        Assert.Equal("SearchingForFace", behavior.StateName);
        var search = Animation(behavior, AnimationTrigger.ComeHere_SearchForFace);
        Assert.True(behavior.IsRunnable(f.Runtime.Context));

        AddFace(f, -7); // HasAnyFaces(0,false) accepts a tracked, unnamed ID.
        f.Rig.Tick();
        Assert.False(behavior.IsRunnable(f.Runtime.Context));
        Assert.Equal(1, behavior.NativeState);
        Assert.Equal("FoundFace", behavior.StateName);
        Assert.True(search.DestructionObserved);
        var found = Animation(behavior, AnimationTrigger.ComeHere_SearchForFace_FoundFace);

        // Firmware completes the real authored clip. A new camera result is unnecessary after FoundFace.
        int mark = 0;
        for (int i = 0; i < 100 && ReferenceEquals(f.Robot.BehaviorManagerOwner.Current, behavior); i++)
        {
            foreach (var start in f.Rig.SentSince(mark).OfType<StartOfAnimation>().ToArray())
            {
                f.Rig.Data(new AnimationStarted { AnimId = start.AnimId });
                f.Rig.Data(new AnimationEnded { AnimId = start.AnimId });
            }
            mark = f.Rig.Mark();
            f.Motors();
        }
        Assert.True(found.DestructionObserved);
        Assert.NotSame(behavior, f.Robot.BehaviorManagerOwner.Current);
        f.Rig.Engine.DrainAppDelivery();
        var reports = run.BehaviorTransitions(BehaviorID.VC_SearchForFace, 0, double.MaxValue);
        Assert.Collection(reports,
            start => Assert.True(PersonalityHw.IsBehaviorStart(start, BehaviorID.VC_SearchForFace)),
            stop => Assert.True(PersonalityHw.IsBehaviorStop(stop, BehaviorID.VC_SearchForFace)));
    }

    [Fact]
    public void ACompletedSearchEndsWithoutRepeatingWhenNoFaceAppears()
    {
        using var f = new Fixture(preloadAnimations: true, pumpAnimations: true, sourceNvEmpty: true);
        var behavior = Search(f);
        f.Rig.Robot.ExecuteBehaviorById((byte)BehaviorID.VC_SearchForFace, 1);
        f.Rig.Tick();
        var action = Animation(behavior, AnimationTrigger.ComeHere_SearchForFace);
        int mark = 0;
        for (int i = 0; i < 100 && ReferenceEquals(f.Robot.BehaviorManagerOwner!.Current, behavior); i++)
        {
            foreach (var start in f.Rig.SentSince(mark).OfType<StartOfAnimation>().ToArray())
            {
                f.Rig.Data(new AnimationStarted { AnimId = start.AnimId });
                f.Rig.Data(new AnimationEnded { AnimId = start.AnimId });
            }
            mark = f.Rig.Mark();
            f.Motors();
        }
        Assert.True(action.DestructionObserved);
        Assert.NotSame(behavior, f.Robot.BehaviorManagerOwner!.Current);
        Assert.Equal(0, behavior.NativeState);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ReadinessAndInitUseCurrentOriginFacesIncludingAnonymousOnes(bool currentOrigin)
    {
        using var f = new Fixture(sourceNvEmpty: true);
        AddFace(f, -7, currentOrigin);
        var behavior = Search(f);
        Assert.Equal(!currentOrigin, behavior.IsRunnable(f.Runtime.Context));
        if (!currentOrigin) return;
        using var scope = new BehaviorScope();
        behavior.StartAsync(f.Runtime.Context, scope, CancellationToken.None).GetAwaiter().GetResult();
        Assert.True(behavior.InitFailed);
        Assert.Null(behavior.OwnedRunner);
    }

    [Fact]
    public void StopRetiresTheOwnedSearchAndAllowsAnotherRun()
    {
        using var f = new Fixture(preloadAnimations: true, sourceNvEmpty: true);
        var behavior = Search(f);
        f.Rig.Robot.ExecuteBehaviorById((byte)BehaviorID.VC_SearchForFace, 1);
        f.Rig.Tick();
        var first = Animation(behavior, AnimationTrigger.ComeHere_SearchForFace);
        f.Robot.BehaviorManagerOwner!.Stop(BehaviorStopReason.Interrupted, f.Rig.Engine.Timer.SecondsF);
        Assert.True(first.DestructionObserved);
        f.Rig.Tick(1100);
        f.Rig.Robot.ExecuteBehaviorById((byte)BehaviorID.VC_SearchForFace, 1);
        f.Rig.Tick();
        Assert.NotSame(first, Animation(behavior, AnimationTrigger.ComeHere_SearchForFace));
        Assert.Equal(0, behavior.NativeState);
    }
}
