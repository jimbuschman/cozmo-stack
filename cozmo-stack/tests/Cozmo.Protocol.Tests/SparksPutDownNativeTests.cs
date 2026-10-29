using System.Reflection;
using System.Text.Json;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;
using Fixture = Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class SparksPutDownNativeTests
{
    private const string Id = "SparksPutDownBlock";
    private static NativePutDownBlockBehavior Behavior(Fixture f) =>
        Assert.IsType<NativePutDownBlockBehavior>(f.Runtime.Bound[Id]);

    private static ObservableObject Carry(Fixture f)
    {
        var obj = f.Add();
        obj.Pose = new Pose3d(Rotation3d.Identity, new Vec3(0, 0, 22), parent: f.Robot.LiftPose);
        obj.NativeCarryingTree = true;
        using var relative = new Pose3d("");
        f.M.Docking.Carrying.AttachToLift(7, obj.Markers[0], relative, null, null);
        return obj;
    }

    // Inspection only: no mutation or substituted scope. The all-false native table creates no manager-node locks.
    private static (BehaviorScope Scope, HashSet<string> Names) LockState(NativePutDownBlockBehavior behavior)
    {
        var scope = Assert.IsType<BehaviorScope>(typeof(SteppedBehavior)
            .GetProperty("Scope", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(behavior));
        var names = Assert.IsType<HashSet<string>>(typeof(BehaviorScope)
            .GetField("_reactionLockNames", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(scope));
        return (scope, names);
    }

    private static void Animation(ActionRunner action, int trigger)
    {
        var request = Assert.IsAssignableFrom<TriggerAnimationAction>(action).Request;
        Assert.Equal(trigger, (int)request.Trigger);
        Assert.Equal(1, request.Argument2);
        Assert.True(request.Argument3);
        Assert.Equal(0, request.Argument4);
        Assert.Equal(60f, request.Argument5);
        Assert.False(request.Argument6);
    }

    private static JsonDocument ReadJson(string path) => JsonDocument.Parse(File.ReadAllText(path),
        new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });

    [Fact]
    public void ExactMinimalAssetAndEightDirectStrictInputsRetainSameZeroScoreReceiver()
    {
        // reach: production configured construction; supplied exact chooser JSON construction units.
        // E1-E5/C1-C4:0076C9DC..CA22 (ID154 AFTER string),0076A86C..A89E (class44),
        // factory0059CEE6..CF04; actual direct caller receiver005B1F94..2006/005B3A6C..3A94.
        // Strict0060B0C8..B194 retains manager refs without scored writer005BC4CA..4E0.
        // No candidate or winner is requested: Sparked timeout replacement005B2016..2062,
        // admission/strategy and inherited input producers remain C5/C6 boundaries.
        using var f = new Fixture();
        var behavior = Behavior(f);
        Assert.Equal(154, (int)BehaviorID.SparksPutDownBlock);
        Assert.Equal(44, (int)BehaviorClass.PutDownBlock);
        Assert.Equal(Id, behavior.Id);
        Assert.Equal("PutDownBlock", behavior.Class);
        Assert.Same(behavior, f.Robot.BehaviorManagerOwner!.Find(Id));
        foreach (string sibling in new[] { "PutDownBlock", "PutDownBlockNothingToDo", "PyramidPutDownBlock" })
            Assert.NotSame(f.Runtime.Bound[sibling], behavior);
        string root = Path.Combine(f.Rig.Engine.Options.ResourcesPath!, "config", "engine", "behaviorSystem");
        using var own = ReadJson(Path.Combine(root, "behaviors", "freeplay", "sparkable", "sparksPutDownBlock.json"));
        Assert.Equal(new[] { "behaviorClass", "behaviorID" },
            own.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(n => n));
        Assert.Equal("PutDownBlock", own.RootElement.GetProperty("behaviorClass").GetString());
        Assert.Equal(Id, own.RootElement.GetProperty("behaviorID").GetString());
        // Absent displayNameKey leaves native display empty (005BBC24..BC74/005BBFB4..BFDA).
        // Current retention/typed report remains MISSING M8-012; no delivery is asserted here.
        Assert.Equal(0x55u, behavior.NativeBaseInputs!.RequiredUnlock);
        Assert.Equal((byte?)0x0C, behavior.NativeBaseInputs.CheckedExecutableType);
        Assert.Equal(BehaviorObjective.Invalid, behavior.BehaviorObjective);
        Assert.Equal(0, behavior.RequiredProcessId);
        Assert.Equal(0, behavior.Score);
        var callers = new (string File, string Activity, string[] Order)[]
        {
            ("sparksFistBump.json", "SparksFistBump", new[] { "DriveOffCharger", Id, "SparksFistBump" }),
            ("sparksKnockOverCubes.json", "SparksKnockOverCubes", new[] { "DriveOffCharger", "SparksKnockOverCubes", "SparksCheckForStackAtInterval", Id, "SparksLookInPlace" }),
            ("sparksPeekABoo.json", "SparksPeekABoo", new[] { "DriveOffCharger", Id, "SparksPeekABoo", "SparksFindFaces" }),
            ("sparksPickupCube.json", "SparksPickUpCube", new[] { "DriveOffCharger", "KnockOverCubes", "SparksPickUpCube", "SparksVisitPossibleMarker", Id, "SparksLookInPlace" }),
            ("sparksPopAWheelie.json", "SparksPopAWheelie", new[] { "DriveOffCharger", "KnockOverCubes", Id, "SparksPopAWheelie", "SparksLookInPlace" }),
            ("sparksPounceOnMotion.json", "SparksPounceOnMotion", new[] { "DriveOffCharger", Id, "SparksPounceOnMotion" }),
            ("sparksRollBlock.json", "SparksRollBlock", new[] { "DriveOffCharger", "KnockOverCubes", "SparksRollBlock", Id, "SparksLookInPlace" }),
            ("sparksTrackLaser.json", "SparksTrackLaser", new[] { "DriveOffCharger", Id, "SparksTrackLaser" })
        };
        foreach (var caller in callers)
        {
            using var asset = ReadJson(Path.Combine(root, "activities", "sparks", caller.File));
            var activity = asset.RootElement;
            Assert.Equal(caller.Activity, activity.GetProperty("activityID").GetString());
            Assert.Equal("Sparked", activity.GetProperty("activityType").GetString());
            Assert.False(activity.TryGetProperty("subActivityDelegate", out _));
            var input = activity.GetProperty("behaviorChooser");
            Assert.Equal("StrictPriority", input.GetProperty("type").GetString());
            var raw = input.GetProperty("behaviors").EnumerateArray().ToArray();
            Assert.All(raw, entry => Assert.Equal(JsonValueKind.String, entry.ValueKind));
            Assert.Equal(caller.Order, raw.Select(entry => entry.GetString()));
            var chooser = Assert.IsType<StrictPriorityChooser>(ActivityTreeLoader.BuildChooser(input, f.Runtime.Bound, null, null));
            Assert.Equal(caller.Order, chooser.BehaviorIds);
            var bound = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IBehavior>>(typeof(StrictPriorityChooser)
                .GetField("_bound", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(chooser));
            Assert.Same(behavior, bound[Id]);
            Assert.Equal(0, behavior.Score); // exact direct string inputs do not assign NothingToDo's1.1.
            Assert.Equal(0, behavior.RunningScoreBonus);
        }
    }
    [Fact]
    public void AliasStartUsesOneNativeDrawOrdinary19AAndOwnLockThenStopRetiresOnlyOwnedAction()
    {
        // reach: unit supplied manager admission and carried scene; actual configured factory/manager/ActionList.
        // E6-E9/E12:005C7FDC..8096,005BF02C..F042; lock005BCE46..CE7E/C68B20;
        // stop005BD08C..D174. No parent winner or physical motion claim.
        using var f = new Fixture();
        Carry(f);
        var behavior = Behavior(f);
        f.Rig.Engine.ContextRandom.SetSeed(5489);
        // M5-005 checked native R1 vector: independent MT5489 words, not a production-computed oracle.
        double u = (3499211612.0 + 581869302.0 * 4294967296.0) / 18446744073709551616.0;
        float expected = (float)((-75.0 - (-45.0)) * u + (-45.0));
        var manager = f.Robot.BehaviorManagerOwner!;
        var missing = new List<string>();
        SteppedBehavior.ResetMissingForTests();
        SteppedBehavior.MissingReported += missing.Add;
        try { Assert.True(manager.StartAsync(Id, f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult()); }
        finally { SteppedBehavior.MissingReported -= missing.Add; }
        Assert.Same(behavior, manager.Current);
        // Actual supplied-admission manager switch exposes the implementation gap, not a native report.
        Assert.Contains(missing, text => text.Contains("M8-012") && text.Contains("0x005A22B4..0x005A24AC")
            && text.Contains("BehaviorTransition/displayNameKey delivery is unbuilt"));
        var initial = Assert.IsType<CompoundActionSequential>(f.Robot.ActionList.GetCurrentAction());
        Assert.Equal(2, initial.Children.Count);
        var drive = Assert.IsType<DriveStraightAction>(initial.Children[0]);
        Assert.Equal(expected, behavior.BackupDistance);
        Assert.Equal(expected, drive.DistanceMm);
        Assert.Equal(100f, drive.SpeedMmps);
        Animation(initial.Children[1], 0x19A);
        Assert.Equal((3890346734.0 + 3586334585.0 * 4294967296.0) / 18446744073709551616.0,
            f.Rig.Engine.ContextRandom.GetNextDbl()); // exactly one preceding double draw.
        Assert.Equal(0, behavior.RunningScoreBonus); // score5 preceded a current-action handle, not an unconditional5.
        var held = LockState(behavior);
        Assert.Equal(new[] { Id }, held.Names);
        Assert.True(held.Scope.ReactionsDisabled);
        for (int i = 0; i < 21; i++)
        {
            Assert.False(manager.HasDisableLock((ReactionTrigger)i, Id + "_behaviorLock"));
            Assert.False(manager.HasDisableLock((ReactionTrigger)i, "PutDownBlock_behaviorLock"));
            Assert.False(manager.HasDisableLock((ReactionTrigger)i, "PyramidPutDownBlock_behaviorLock"));
        }
        var unrelated = new NativeTimedWaitRunner(f.M, 100f);
        f.Robot.ActionList.QueueAction(QueueActionPosition.InParallel, unrelated);
        f.Rig.Tick();
        manager.Stop(BehaviorStopReason.Interrupted, f.Rig.Engine.Timer.SecondsF);
        f.Robot.ActionList.Watcher.Update();
        Assert.Empty(held.Names);
        Assert.False(held.Scope.ReactionsDisabled);
        Assert.True(initial.DestructionObserved);
        Assert.False(unrelated.DestructionObserved);
        Assert.True(f.M.Docking.Carrying.IsCarryingObject);
        f.Robot.ActionList.Clear();
    }

    [Fact]
    public void AliasRunsRealBackupAnimationsAndImageWaitBeforeCurrentCarryRelease()
    {
        // reach: unit explicit manager admission/carry scene and firmware states/acks/path/CA/CB/frame inputs.
        // Actual native configured instance -> manager -> ActionList -> wire -> worker/drain43 -> callback.
        // E8-E12:005C7FD0..84D4; M15-012 composition005C8174..82CC; M13-027 wrapper0054C7DC..C8CE.
        // No detector/face producer substitution, Ground44 child, or whole public face payload certification.
        using var f = new Fixture();
        f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        var carried = Carry(f);
        var behavior = Behavior(f);
        var manager = f.Robot.BehaviorManagerOwner!;
        var completions = new List<RobotCompletedAction>();
        using var sub = f.Rig.Engine.AnimationMessages.SubscribeMessage(0x5A, true,
            p => { if (p is RobotCompletedAction done) completions.Add(done); });
        Assert.True(manager.StartAsync(Id, f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        var initial = Assert.IsType<CompoundActionSequential>(f.Robot.ActionList.GetCurrentAction());
        uint firstTag = initial.Tag;
        var paths = new HashSet<ushort>();
        var animations = new HashSet<byte>();
        int images = 0;
        bool inspectedLook = false;
        var held = LockState(behavior);
        try
        {
            for (int tick = 0; tick < 100 && (behavior.HasCurrentAction || f.M.Docking.Carrying.IsCarryingObject); tick++)
            {
                f.Motors();
                foreach (var path in f.Rig.SentSince(0).OfType<ExecutePath>())
                    if (paths.Add(path.EventId))
                    {
                        f.Rig.Data(new PathFollowingEvent { EventId = path.EventId, EventType = 0 });
                        f.Rig.Tick();
                        f.Rig.Data(new PathFollowingEvent { EventId = path.EventId, EventType = 2 });
                        f.Rig.Tick();
                    }
                byte tag = f.Rig.Robot.Animations.CurrentTag;
                if (tag != 0 && animations.Add(tag))
                {
                    f.Rig.Data(new AnimationStarted { AnimId = tag });
                    f.Rig.Data(new AnimationEnded { AnimId = tag });
                    f.Rig.Tick();
                }
                if (f.Robot.ActionList.GetCurrentAction() is CompoundActionSequential look && look.Tag != firstTag)
                {
                    if (!inspectedLook)
                    {
                        Assert.Equal(3, look.Children.Count);
                        var parallel = Assert.IsType<CompoundActionParallel>(look.Children[0]);
                        var backup = Assert.IsType<DriveStraightAction>(parallel.Children[1]);
                        Assert.Equal(-30f, backup.DistanceMm);
                        Assert.Equal(-80f, backup.SpeedMmps);
                        var wrapper = Assert.IsType<NativeFaceBeforeAnimationRunner>(look.Children[2]);
                        Assert.Equal(2, wrapper.Children.Count);
                        Assert.IsType<NativeLastFaceRunner>(wrapper.Children[0]);
                        Animation(wrapper.Children[1], 0x199);
                        Assert.True(f.M.Docking.Carrying.IsCarryingObject);
                        inspectedLook = true;
                    }
                    var wait = look.Children.OfType<NativeWaitForImagesRunner>().FirstOrDefault();
                    if (wait?.Subscribed == true) f.Image((uint)++images);
                }
            }
            Assert.True(inspectedLook);
            Assert.False(behavior.HasCurrentAction);
            Assert.False(f.M.Docking.Carrying.IsCarryingObject);
            Assert.Equal(2, paths.Count);
            Assert.True(animations.Count >= 2);
            Assert.Equal(2, images);
            Assert.Contains(completions, c => c.Tag == firstTag && c.Result == 0);
            Assert.Equal(0u, completions.Last(c => c.Tag != firstTag).Result);
            Assert.Equal(PoseState.Dirty, carried.PoseState);
            manager.Stop(BehaviorStopReason.Completed, f.Rig.Engine.Timer.SecondsF);
            Assert.Empty(held.Names);
        }
        finally
        {
            manager.Stop(BehaviorStopReason.Interrupted, f.Rig.Engine.Timer.SecondsF);
            f.Robot.ActionList.Clear();
        }
    }

    [Fact]
    public void AliasResultlessCallbacksAcceptFailureAndInterruptedCallbackCannotReleaseNewCarry()
    {
        // reach: unit supplied typed LOCAL5A terminal inputs, not actual child execution.
        // E9/E12:005C840A..841E,005C8480..84D4 and common stop005BD08C..D174.
        using var f = new Fixture();
        Carry(f);
        var behavior = Behavior(f);
        var manager = f.Robot.BehaviorManagerOwner!;
        Assert.True(manager.StartAsync(Id, f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        f.Rig.Engine.AnimationMessages.Deliver(0x5A, true,
            new RobotCompletedAction(behavior.CurrentRunnerTag, 0, 0x03000018, Array.Empty<uint>(), 0));
        behavior.Update(f.Runtime.Context, f.Rig.Engine.Timer.SecondsF);
        var look = Assert.IsAssignableFrom<CompoundActionSequential>(f.Robot.ActionList.GetCurrentAction());
        Assert.Equal(3, look.Children.Count);
        uint finalTag = behavior.CurrentRunnerTag;
        f.Rig.Engine.AnimationMessages.Deliver(0x5A, true,
            new RobotCompletedAction(finalTag, 0, 0x03000018, Array.Empty<uint>(), 0));
        behavior.Update(f.Runtime.Context, f.Rig.Engine.Timer.SecondsF);
        Assert.False(f.M.Docking.Carrying.IsCarryingObject);
        manager.Stop(BehaviorStopReason.Interrupted, f.Rig.Engine.Timer.SecondsF);
        Carry(f);
        f.Rig.Engine.AnimationMessages.Deliver(0x5A, true,
            new RobotCompletedAction(finalTag, 0, 0, Array.Empty<uint>(), 0));
        behavior.Update(f.Runtime.Context, f.Rig.Engine.Timer.SecondsF);
        Assert.True(f.M.Docking.Carrying.IsCarryingObject);
    }
}
