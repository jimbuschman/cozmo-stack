using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Xunit;
using Fixture = Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class DanceMamboNativeTests
{
    private sealed class Listener(Action<EngineRobot> onComplete) : INativeDanceListener
    {
        public void OnDanceAnimationComplete(EngineRobot robot) => onComplete(robot);
    }
    // Reach: unit with supplied StartAsync and RNG words; actual configured factory, connected BlockWorld and light component.
    [Fact]
    public void ConfiguredFactoryUsesConnectedIdentityAndHundredthDraw()
    {
        using var f = new Fixture();
        f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        var dance = Assert.IsType<NativeDanceBehavior>(f.Runtime.Bound["Dance_Mambo"]);
        M4ControlTests.ConnectCube(f.Rig, 0, ObjectType.Block_LIGHTCUBE1);
        M4ControlTests.ConnectCube(f.Rig, 1, ObjectType.Block_LIGHTCUBE2);
        var cubes = f.M.World.ConnectedObjects.ToArray();
        Assert.Equal(2, cubes.Length);
        var calls = new List<(uint Id, uint Trigger, int Offset)>();
        dance.LightPlayObservedForTest = (id, trigger, offset, _) => calls.Add((id, trigger, offset));
        int words = 0; f.Rig.Engine.ContextRandom.WordSourceForTest = () => { words++; return 0; };
        try
        {
            using var scope = new BehaviorScope();
            dance.StartAsync(f.Runtime.Context, scope, default).GetAwaiter().GetResult();
            // D6-D8, 0x005ED5B8..0x005ED650 and 0x005ED82C..0x005ED914:
            // draw 1 picks 4; the next cube's 100th conflicting draw is returned.
            Assert.True(words >= 202);
            Assert.Equal(new[] { (cubes[0].ObjectId, 4u, 0), (cubes[1].ObjectId, 4u, -328) }, calls.Take(2));
            Assert.Equal(4u, dance.Choices[cubes[0].ObjectId]);
            Assert.Equal(4u, dance.Choices[cubes[1].ObjectId]);
            Assert.NotNull(dance.OwnedRunner);
            dance.Stop(BehaviorStopReason.Interrupted);
            Assert.Equal(4, f.Robot.BehaviorPublicState.State.StageTag);
            Assert.Equal(new[] { (cubes[0].ObjectId, 10u, 0), (cubes[1].ObjectId, 10u, 0) }, calls.Skip(2).Take(2));
        }
        finally { f.Rig.Engine.ContextRandom.WordSourceForTest = null; }
    }
    // Reach: unit with supplied StartAsync, listener identities and terminal action result; actual configured behavior and action completion.
    [Fact]
    public void SuppliedListenerIdentitiesAreUniqueOrderedAndReadSuccessorAfterCallback()
    {
        using var f = new Fixture();
        f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        var dance = Assert.IsType<NativeDanceBehavior>(f.Runtime.Bound["Dance_Mambo"]);
        var visited = new List<uint>();
        Listener seven = new(_ => visited.Add(7));
        Listener nine = new(robot => { Assert.Same(f.Robot, robot); visited.Add(9); });
        Listener three = new(robot => { visited.Add(3); dance.AddNativeListener(7, seven); });
        Listener duplicate = new(_ => visited.Add(99));
        dance.AddNativeListener(9, nine); dance.AddNativeListener(3, three); dance.AddNativeListener(3, duplicate);
        using var scope = new BehaviorScope();
        dance.StartAsync(f.Runtime.Context, scope, default).GetAwaiter().GetResult();
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        var action = Assert.IsAssignableFrom<ActionRunner>(f.Robot.ActionList.GetCurrentAction());
        action.State = 0x03000001; f.Robot.ActionList.Update();
        Assert.Equal(new uint[] { 3, 7, 9 }, visited);
        GC.KeepAlive(three); GC.KeepAlive(seven); GC.KeepAlive(nine); GC.KeepAlive(duplicate);
        dance.Stop(BehaviorStopReason.Interrupted);
    }

    // Reach: unit with supplied StartAsync, RNG words and retained callbacks; actual light component and active-state boundaries.
    [Fact]
    public void PriorRunCubeCallbackUsesCurrentActiveStateAndFadeStopsAllEngineLights()
    {
        using var f = new Fixture();
        f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        var dance = Assert.IsType<NativeDanceBehavior>(f.Runtime.Bound["Dance_Mambo"]);
        M4ControlTests.ConnectCube(f.Rig, 0, ObjectType.Block_LIGHTCUBE1);
        M4ControlTests.ConnectCube(f.Rig, 1, ObjectType.Block_LIGHTCUBE2);
        uint firstId = Assert.Single(f.M.World.ConnectedObjects.Where(c => c.Type == ObjectType.Block_LIGHTCUBE1)).ObjectId;
        uint otherId = Assert.Single(f.M.World.ConnectedObjects.Where(c => c.Type == ObjectType.Block_LIGHTCUBE2)).ObjectId;
        var callbacks = new List<(uint Id, uint Trigger, Action Callback)>();
        dance.LightPlayObservedForTest = (id, trigger, _, callback) => callbacks.Add((id, trigger, callback));
        int words = 0; f.Rig.Engine.ContextRandom.WordSourceForTest = () => { words++; return 0; };
        try
        {
            using var firstScope = new BehaviorScope();
            dance.StartAsync(f.Runtime.Context, firstScope, default).GetAwaiter().GetResult();
            var old = callbacks.First(x => x.Trigger == 4);
            dance.Stop(BehaviorStopReason.Interrupted);
            var fade = callbacks.First(x => x.Trigger == 10);
            int afterStop = callbacks.Count; old.Callback(); Assert.Equal(afterStop, callbacks.Count);
            using var secondScope = new BehaviorScope();
            dance.StartAsync(f.Runtime.Context, secondScope, default).GetAwaiter().GetResult();
            int beforeOld = callbacks.Count; old.Callback();
            Assert.Equal(beforeOld + 1, callbacks.Count);
            Assert.Equal(old.Id, callbacks[^1].Id);
            // D12 0x005EDFCA..D0: this old fade callback is unconditional after re-init.
            var lights = f.Rig.Robot.Lights.Cubes;
            var firstBefore = lights.PlayingForTest(ObjectType.Block_LIGHTCUBE1, 1);
            var otherBefore = lights.PlayingForTest(ObjectType.Block_LIGHTCUBE2, 1);
            var stateBefore = lights.PlayingForTest(ObjectType.Block_LIGHTCUBE2, 2);
            Assert.NotNull(firstBefore); Assert.NotNull(otherBefore); Assert.NotNull(stateBefore);
            Assert.Contains("fadeOff_1sec", firstBefore.Value.Name);
            Assert.Contains("fadeOff_1sec", otherBefore.Value.Name);
            int beforeFade = callbacks.Count;
            fade.Callback();
            var firstAfter = lights.PlayingForTest(ObjectType.Block_LIGHTCUBE1, 1);
            var otherAfter = lights.PlayingForTest(ObjectType.Block_LIGHTCUBE2, 1);
            Assert.NotNull(firstAfter); Assert.NotNull(otherAfter);
            Assert.DoesNotContain("fadeOff_1sec", firstAfter.Value.Name);
            Assert.DoesNotContain("fadeOff_1sec", otherAfter.Value.Name);
            Assert.Contains(callbacks.Skip(beforeFade), c => c.Id == firstId && c.Trigger is >= 4 and <= 9);
            Assert.Contains(callbacks.Skip(beforeFade), c => c.Id == otherId && c.Trigger is >= 4 and <= 9);
            var stateAfter = lights.PlayingForTest(ObjectType.Block_LIGHTCUBE2, 2);
            Assert.NotNull(stateAfter); Assert.Equal(stateBefore.Value.Name, stateAfter.Value.Name);
            Assert.Equal(stateBefore.Value.Timer, stateAfter.Value.Timer);
            dance.Stop(BehaviorStopReason.Interrupted);
        }
        finally { f.Rig.Engine.ContextRandom.WordSourceForTest = null; }
    }

    // Reach: unit with supplied StartAsync, RNG words and reentrant observer; actual configured factory and light entry.
    [Fact]
    public void ReentryDuringLightPlayPreservesReplacementActionAndScope()
    {
        using var f = new Fixture();
        f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        var dance = Assert.IsType<NativeDanceBehavior>(f.Runtime.Bound["Dance_Mambo"]);
        M4ControlTests.ConnectCube(f.Rig, 0, ObjectType.Block_LIGHTCUBE1);
        using var oldScope = new BehaviorScope(); using var replacementScope = new BehaviorScope();
        bool reentered = false;
        dance.LightPlayObservedForTest = (_, trigger, _, _) =>
        {
            if (reentered || trigger != 4) return;
            reentered = true; dance.LightPlayObservedForTest = null;
            dance.Stop(BehaviorStopReason.Interrupted);
            dance.StartAsync(f.Runtime.Context, replacementScope, default).GetAwaiter().GetResult();
        };
        f.Rig.Engine.ContextRandom.WordSourceForTest = () => 0;
        try
        {
            dance.StartAsync(f.Runtime.Context, oldScope, default).GetAwaiter().GetResult();
            Assert.True(reentered); Assert.True(dance.EngineRunning);
            Assert.NotNull(dance.OwnedRunner);
            Assert.Same(dance.OwnedRunner, f.Robot.ActionList.GetCurrentAction());
            dance.Stop(BehaviorStopReason.Interrupted);
        }
        finally { f.Rig.Engine.ContextRandom.WordSourceForTest = null; }
    }

    // Reach: unit with supplied StartAsync and RNG words; actual connected objects, clock and natural light completion.
    [Fact]
    public void NaturalLightCompletionRepeatsAndDisconnectedIdentityRemainsInSelection()
    {
        using var f = new Fixture();
        f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        var dance = Assert.IsType<NativeDanceBehavior>(f.Runtime.Bound["Dance_Mambo"]);
        M4ControlTests.ConnectCube(f.Rig, 0, ObjectType.Block_LIGHTCUBE1);
        uint first = Assert.Single(f.M.World.ConnectedObjects).ObjectId;
        var calls = new List<(uint Id, uint Trigger)>();
        dance.LightPlayObservedForTest = (id, trigger, _, _) => calls.Add((id, trigger));
        int word = 0;
        f.Rig.Engine.ContextRandom.WordSourceForTest = () => (word++ < 2 ? 0u : 0x40000000u);
        try
        {
            using var scope = new BehaviorScope();
            dance.StartAsync(f.Runtime.Context, scope, default).GetAwaiter().GetResult();
            for (int i = 0; i < 100 && calls.Count < 2; i++) f.Rig.Tick(100);
            Assert.True(calls.Count >= 2); Assert.Equal(first, calls[1].Id);
            dance.Stop(BehaviorStopReason.Interrupted);
            uint historical = dance.Choices[first];
            f.Rig.Data(new ObjectConnectionState { ObjectID = 0, FactoryID = 0x1000, ObjectType = ObjectType.Block_LIGHTCUBE1, Connected = false }); f.Rig.Tick();
            M4ControlTests.ConnectCube(f.Rig, 1, ObjectType.Block_LIGHTCUBE2);
            uint second = Assert.Single(f.M.World.ConnectedObjects).ObjectId;
            f.Rig.Engine.ContextRandom.WordSourceForTest = () => 0;
            using var next = new BehaviorScope();
            dance.StartAsync(f.Runtime.Context, next, default).GetAwaiter().GetResult();
            Assert.Equal(historical, dance.Choices[first]);
            Assert.NotEqual(historical, dance.Choices[second]);
            dance.Stop(BehaviorStopReason.Interrupted);
        }
        finally { f.Rig.Engine.ContextRandom.WordSourceForTest = null; }
    }

    // Reach: unit with supplied StartAsync and persistent light definitions; actual connected cubes and natural fade completion.
    [Fact]
    public void NaturalFadeCompletionStopsOtherCubeEngineLayerAndPreservesStateLayer()
    {
        using var f = new Fixture();
        f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        var dance = Assert.IsType<NativeDanceBehavior>(f.Runtime.Bound["Dance_Mambo"]);
        M4ControlTests.ConnectCube(f.Rig, 0, ObjectType.Block_LIGHTCUBE1);
        using var scope = new BehaviorScope();
        dance.StartAsync(f.Runtime.Context, scope, default).GetAwaiter().GetResult();
        dance.Stop(BehaviorStopReason.Interrupted);
        // Cube 2 connects while cube 1's real non-overridable fade is in progress.
        M4ControlTests.ConnectCube(f.Rig, 1, ObjectType.Block_LIGHTCUBE2);
        var lights = f.Rig.Robot.Lights.Cubes;
        var enginePatterns = Assert.IsType<CubeLightPattern[]>(lights.Animations.ForTrigger("Flash"));
        var engineOriginal = enginePatterns[0];
        enginePatterns[0] = engineOriginal with { DurationMs = 0 };
        try
        {
            Assert.True(lights.PlayLightAnim(ObjectType.Block_LIGHTCUBE2, "Flash", 1));
            var cube2 = Assert.Single(f.M.World.ConnectedObjects.Where(x => x.Type == ObjectType.Block_LIGHTCUBE2));
            var engineBefore = lights.PlayingForTest(cube2.Type, 1); Assert.NotNull(engineBefore);
            var stateBefore = lights.PlayingForTest(cube2.Type, 2); Assert.NotNull(stateBefore);
            Assert.Equal(0u, engineBefore.Value.Timer);
            Assert.Equal("wakeUp", stateBefore.Value.Name);
            var fadeBefore = lights.PlayingForTest(ObjectType.Block_LIGHTCUBE1, 1); Assert.NotNull(fadeBefore);
            Assert.NotEqual(0u, fadeBefore.Value.Timer);
            Assert.True(stateBefore.Value.Timer > fadeBefore.Value.Timer + 100u);
            // The other-cube entries cannot expire: only the first cube's natural fade completion
            // 0x005EDFCA..D0 can remove the engine layer in this interval.
            for (int i = 0; i < 100 && lights.PlayingForTest(cube2.Type, 1) is not null; i++) f.Rig.Tick(100);
            Assert.Null(lights.PlayingForTest(cube2.Type, 1));
            var stateAfter = lights.PlayingForTest(cube2.Type, 2); Assert.NotNull(stateAfter);
            Assert.True(f.Rig.Engine.Timer.TimeStampMs < stateBefore.Value.Timer);
            Assert.Equal(stateBefore.Value.Name, stateAfter.Value.Name);
            Assert.Equal(stateBefore.Value.Timer, stateAfter.Value.Timer);
        }
        finally { enginePatterns[0] = engineOriginal; }
    }

}
