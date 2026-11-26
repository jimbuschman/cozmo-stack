using System.Collections.Concurrent;
using Cozmo.Protocol;
using Cozmo.Robot;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>LIGHTS-GAPS-2: the cube-light MISSING sites on the SCOPE A/B cube paths.</summary>
[Collection("M5 process statics")]
public sealed class LightsGaps2Tests
{
    // M4-009 / M11-004 PlayLightAnim guard: the production composition always owns the VisionSystem/BlockWorld, so a
    // type with no world ObjectID is a plain refusal, never the world-less MISSING.
    // reach: production - CozmoRobot.CreateOffline (the production constructor, no OmitVisionForTest) and public PlayLightAnim.
    [Fact]
    public void ProductionRobotOwnsTheWorldSoPlayLightAnimNeverPrintsTheWorldlessMissing()
    {
        string res = M4ControlTests.CubeLightResources();
        try
        {
            var log = new ConcurrentQueue<string>();
            using var robot = CozmoRobot.CreateOffline(engineOptions: new CozmoEngineOptions
                { ResourcesPath = res, BlockPoolPath = "" });
            robot.Engine.CaptureSourceLogs(log.Enqueue);
            Assert.NotNull(robot.VisionModeRecipient);
            Assert.Same(robot.Vision, robot.VisionModeRecipient);
            Assert.False(robot.Lights.Cubes.PlayLightAnim(ObjectType.Block_LIGHTCUBE2, "Connected"));
            Assert.Contains(log, l => l.Contains("Stack.CubeLightComponent.PlayLightAnim.NoObjectForType"));
            Assert.DoesNotContain(log, l => l.Contains("MISSING: M4-009 / M11-004"));
        }
        finally { Directory.Delete(res, true); }
    }

    // M4-046: the native ObjectLights is a reference read unchecked at 0x006384EA..0x006384F0 when the modifier bool is
    // set; only the host API can omit it, and that is refused with nothing sent.
    // reach: unit (public host API with an argument no engine caller produces).
    [Fact]
    public void HostModifierWithoutObjectLightsIsRefusedWithoutASend()
    {
        string res = M4ControlTests.CubeLightResources();
        try
        {
            using var rig = new M4ControlTests.Rig(res); rig.ToSynced(); rig.State(); M4ControlTests.ConnectCube(rig);
            int mark = rig.Mark();
            Assert.False(rig.Robot.Lights.Cubes.PlayLightAnim(ObjectType.Block_LIGHTCUBE1, "Connected", modifier: true, lights: null));
            Assert.Empty(rig.SentSince(mark).OfType<CubeLights>());
            Assert.Contains(rig.Log, l => l.Contains("Stack.CubeLightComponent.PlayLightAnim.NullModifier"));
            Assert.DoesNotContain(rig.Log, l => l.Contains("MISSING: M4-046"));
        }
        finally { Directory.Delete(res, true); }
    }

    // M4-018 StopAllAnimsOnLayer 0x00638D80: ObjectID -1 is the all-objects branch (0x00638D88..0x00638D8E, format at
    // 0x00638F10 "Stopping all anims for all objects on layer %s"); an unknown ObjectID logs BadObject with that id,
    // format 0x00BFA9C2 "ObjectId %u does not exist in _objectInfo map unable to stop anims", channel
    // "CubeLightComponent" (0x00BFA62F), and returns before Update (0x00638E5A..0x00638E9A).
    // reach: unit (direct component entry; the engine callers pass a found ObjectID or -1).
    [Fact]
    public void StopAllAnimsOnLayerTakesTheNativeObjectId()
    {
        string res = M4ControlTests.CubeLightResources();
        try
        {
            using var rig = new M4ControlTests.Rig(res); rig.ToSynced(); rig.State(); M4ControlTests.ConnectCube(rig);
            var lights = rig.Robot.Lights.Cubes; var entries = new List<EngineLogEntry>(); rig.Engine.LogEntry += entries.Add;
            lights.StopAllAnimsOnLayer(CubeLightComponent.StateLayer, 999u);
            var bad = Assert.Single(entries, e => e.Name == "CubeLightComponent.StopAllAnimsOnLayer.BadObject");
            Assert.Equal("ObjectId 999 does not exist in _objectInfo map unable to stop anims", bad.Text);
            Assert.DoesNotContain(entries, e => e.Name == "CubeLightComponent.Update.NoAnimsLeftOnLayer");
            entries.Clear();
            lights.StopAllAnimsOnLayer(CubeLightComponent.StateLayer, uint.MaxValue);
            Assert.Contains(entries, e => e.Name == "CubeLightComponent.StopAllAnimsOnLayer.AllObjects" && e.Text == "Stopping all anims for all objects on layer State");
            Assert.DoesNotContain(rig.Log, l => l.Contains("MISSING: M4-018 | StopAllAnimsOnLayer"));
        }
        finally { Directory.Delete(res, true); }
    }

    // M4-018 EnableGameLayerOnly single object: the stops at 0x00639A00/0x00639A0E (enable 1) and 0x00639AA2/0x00639AB0
    // (enable 0) pass EnableGameLayerOnly's own ObjectID (r2 = sl), so the connected cube's State layer is stopped.
    // reach: production - public SetEnableFreeplayLightStates (app Robot.cs:1902-1907 caller) on a connected cube.
    [Fact]
    public void SingleObjectGameLayerOnlyStopsByTheCubesObjectId()
    {
        string res = M4ControlTests.CubeLightResources();
        try
        {
            using var rig = new M4ControlTests.Rig(res); rig.ToSynced(); rig.State(); M4ControlTests.ConnectCube(rig);
            uint id = rig.Robot.LocalizationWorld.ConnectedObjectIdForActiveId(0)!.Value;
            var lights = rig.Robot.Lights.Cubes; var entries = new List<EngineLogEntry>(); rig.Engine.LogEntry += entries.Add;
            Assert.True(lights.PlayLightAnim(ObjectType.Block_LIGHTCUBE1, "Sleep", CubeLightComponent.StateLayer));
            lights.SetEnableFreeplayLightStates(false, unchecked((int)id));
            Assert.Contains(entries, e => e.Name == "CubeLightComponent.EnableGameLayerOnly" && e.Text == $"Enabling game layer only for object {id}");
            Assert.Contains(entries, e => e.Name == "CubeLightComponent.Update.NoAnimsLeftOnLayer" && e.Text.EndsWith($"for object {id} going to State layer"));
            Assert.DoesNotContain(entries, e => e.Name == "CubeLightComponent.StopAllAnimsOnLayer.BadObject");
            Assert.DoesNotContain(rig.Log, l => l.Contains("MISSING: M4-018 | StopAllAnimsOnLayer"));
            entries.Clear();
            lights.SetEnableFreeplayLightStates(true, unchecked((int)id));
            Assert.Contains(entries, e => e.Name == "CubeLightComponent.EnableGameLayerOnly" && e.Text == $"Disabling game layer only for object {id}");
            Assert.DoesNotContain(entries, e => e.Name == "CubeLightComponent.StopAllAnimsOnLayer.BadObject");
        }
        finally { Directory.Delete(res, true); }
    }
}
