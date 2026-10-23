using Cozmo.Robot;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public class M13ConfigurationPendingTests
{
    private sealed class Fixture : IDisposable
    {
        internal readonly M4ControlTests.Rig Rig = new();
        internal readonly ManipulationSystem M;
        internal BlockWorld World => Rig.Robot.Vision.World;
        internal BlockConfigurationManager Manager => M.Configurations;
        internal Fixture()
        {
            Rig.ToSynced(); Rig.Robot.Vision.Enabled = false;
            M = new ManipulationSystem(Rig.Robot, Rig.Robot.Vision);
            Manager.ForceUpdate = false;
        }
        internal ObservableObject Cube(uint id, float z = 22)
        {
            var obj = new ObservableObject(id, ObjectType.Block_LIGHTCUBE1, CubeGeometry.MarkersFor(ObjectType.Block_LIGHTCUBE1))
                { OriginId = Rig.Engine.Robot!.CurrentOriginId };
            var pose = new Pose3d(Rotation3d.Identity, new Vec3(200,0,z), "cube",
                Rig.Engine.Robot.GetNativeOrigin(obj.OriginId));
            obj.InitPose(pose, PoseState.Known); World.AddLocatedObject(obj); return obj;
        }
        internal void Contact()
        {
            var robot = Rig.Engine.Robot!;
            var state = Rig.MakeState(RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.IsOnCharger,
                origin: robot.CurrentOriginId, frame: robot.PoseFrameId);
            Rig.Data(state); Rig.Tick();
            Assert.Equal(0u, robot.LastFullStateResult);
        }
        public void Dispose() { M.Dispose(); Rig.Dispose(); }
    }

    // reach: production (normal ManipulationSystem composition; admitted RobotState -> contact -> world callback).
    // 0x00624892 / 0x00616D68: old code leaves pending empty (unbound hook) and rebuilds synchronously on events.
    [Fact]
    public void ContactQueuesKnownIdUniquelyWithoutSynchronouslyUpdating()
    {
        using var f = new Fixture(); f.Contact();
        var charger = Assert.Single(f.World.LocatedObjects.Where(o => o.Family == ObjectFamily.Charger));
        Assert.Equal(new[] { charger.ObjectId }, f.Manager.PendingPoseChanges);
        Assert.Equal(0, f.Manager.RebuildCount);
        f.Contact();
        Assert.Equal(new[] { charger.ObjectId }, f.Manager.PendingPoseChanges);
        Assert.Equal(0, f.Manager.RebuildCount);
    }

    // reach: unit (located cube/pose and direct setter substituted; native container population MISSING at 0x0061682C).
    // 0x00616D6C..74: old code has no last-check cache, so HasLastCheckPose is false after the forced update.
    [Fact]
    public void KnownPreservesLastCheckPoseButInvalidErasesIt()
    {
        using var f = new Fixture(); var cube = f.Cube(10);
        f.Manager.ForceUpdate = true; f.Manager.Update();
        Assert.True(f.Manager.HasLastCheckPose(10));
        f.Manager.SetObjectPoseChanged(10, PoseState.Known);
        Assert.True(f.Manager.HasLastCheckPose(10));
        Assert.Equal(new uint[] { 10 }, f.Manager.PendingPoseChanges);
        f.Manager.SetObjectPoseChanged(10, PoseState.Unknown);
        Assert.False(f.Manager.HasLastCheckPose(10));
        Assert.Equal(new uint[] { 10 }, f.Manager.PendingPoseChanges);
    }

    // reach: unit (located cube and direct frame-tail Update substituted; full containers MISSING at 0x0061682C).
    // 0x00616D96 -> DC2: old code gives rebuild/notify delta 1 on the no-movement call, instead of 0.
    [Fact]
    public void NoMovementConsumesPendingAndSkipsRebuildAndNotifyIncludingEmptyGate()
    {
        using var f = new Fixture(); var cube = f.Cube(10);
        f.Manager.ForceUpdate = true; f.Manager.Update();
        int builds = f.Manager.RebuildCount, notifies = f.Manager.NotifyCount;
        f.Manager.SetObjectPoseChanged(10, PoseState.Known); f.Manager.Update();
        Assert.Equal(builds, f.Manager.RebuildCount); Assert.Equal(notifies, f.Manager.NotifyCount);
        Assert.Empty(f.Manager.PendingPoseChanges); Assert.False(f.Manager.ForceUpdate);
        f.Manager.Update();
        Assert.Equal(builds, f.Manager.RebuildCount); Assert.Equal(notifies, f.Manager.NotifyCount);
    }

    // reach: unit (received contact input is production; Update invoked directly instead of vision frame tail).
    // 0x00C777D0 {2,1}, 0x00616E40: old code has pending count 0, not 1, after the second contact observation.
    [Fact]
    public void ChargerIsNeverCachedAndEachPendingObservationRebuilds()
    {
        using var f = new Fixture();
        for (int i = 0; i < 2; i++)
        {
            f.Contact(); var id = Assert.Single(f.Manager.PendingPoseChanges);
            int before = f.Manager.RebuildCount;
            f.Manager.Update();
            Assert.Equal(before + 1, f.Manager.RebuildCount);
            Assert.False(f.Manager.HasLastCheckPose(id)); Assert.Empty(f.Manager.PendingPoseChanges);
        }
    }

    // reach: unit (fixture cubes feed candidate containers; native membership/tallest producer MISSING at 0x0061682C).
    // 0x0064DCF4..DD68: old code leaves byte+6 = 0 and signal count 0 when the tallest stack becomes 2.
    [Fact]
    public void NotifyChangesStackHeightAndSignalsOnlyWhenHeightChangesBeforeReset()
    {
        using var f = new Fixture(); var broadcaster = f.Rig.Engine.Robot!.BehaviorPublicState;
        var heights = new List<byte>();
        using var subscription = broadcaster.Subscribe(s =>
        {
            heights.Add(s.StackHeight);
            Assert.True(f.Manager.ForceUpdate); // 0x00616DBE precedes reset at DCC.
        });
        f.Cube(10); f.Cube(11, 66);
        f.Manager.ForceUpdate = true; f.Manager.Update();
        Assert.Equal((byte)2, broadcaster.State.StackHeight); Assert.Equal(new byte[] { 2 }, heights);
        f.Manager.ForceUpdate = true; f.Manager.Update();
        Assert.Equal(new byte[] { 2 }, heights);
        f.World.MarkUnknown(10); f.World.MarkUnknown(11); // no tallest stack -> byte zero
        f.Manager.ForceUpdate = true; f.Manager.Update();
        Assert.Equal((byte)0, broadcaster.State.StackHeight); Assert.Equal(new byte[] { 2, 0 }, heights);
    }
}
