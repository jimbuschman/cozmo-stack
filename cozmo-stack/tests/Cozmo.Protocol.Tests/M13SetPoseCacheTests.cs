using System.Reflection;
using System.Runtime.CompilerServices;
using Cozmo.Robot;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class M13SetPoseCacheTests
{
    private static Dictionary<PreActionType, List<CubePreActionPoses.StoredElement>> Cache(ObservableObject obj)
    {
        var field = typeof(CubePreActionPoses).GetField("Caches", BindingFlags.NonPublic | BindingFlags.Static)!;
        return ((ConditionalWeakTable<ObservableObject, Dictionary<PreActionType,
            List<CubePreActionPoses.StoredElement>>>)field.GetValue(null)!).GetOrCreateValue(obj);
    }

    private static bool Query(ObservableObject obj, PreActionType type, Pose3d robot, List<PreActionPose> output,
        IReadOnlyCollection<MarkerType>? codes = null) => CubePreActionPoses.GetCurrentPreActionPoses(obj,
            new[] { type }, codes ?? Array.Empty<MarkerType>(), robot, 0, Array.Empty<PreActionObstacle>(), output);

    // reach: production (public relative-observation API used by SetObjectAsAttachedToLift, real world
    // notifier and cube cache; no replacement SetPose or generation). This covers invalidation, not full docking.
    // Old code: the broadcast-time query returns false because ApplyRelativeObservation bypasses SetPose.
    [Fact]
    public void RelativeObservationApiClearsBeforeDeliveringThePoseStateChange()
    {
        using var rig = new M4ControlTests.Rig(); rig.ToSynced();
        using var pose = new Pose3d(Rotation3d.Identity, new Vec3(100, 0, 22), "cube",
            rig.Engine.Robot!.GetNativeOrigin(rig.Engine.Robot.CurrentOriginId));
        var obj = new ObservableObject(702, ObjectType.Block_LIGHTCUBE1,
            CubeGeometry.MarkersFor(ObjectType.Block_LIGHTCUBE1)) { OriginId = rig.Engine.Robot.CurrentOriginId };
        obj.InitPose(pose, PoseState.Known);
        var world = rig.Robot.Vision.World;
        world.AddLocatedObject(obj);
        var output = new List<PreActionPose>();
        bool? regeneratedAtBroadcast = null;
        Action<ObservableObject, PoseState, PoseState> changed = (o, _, _) =>
        {
            if (o == obj) regeneratedAtBroadcast = Query(obj, PreActionType.Docking, pose, output);
        };
        try
        {
            Assert.True(Query(obj, PreActionType.Docking, pose, output));
            Assert.False(Query(obj, PreActionType.Docking, pose, output));
            world.PoseStateChanged += changed;
            Assert.Equal(0u, ObjectPoseConfirmerRelative.AddObjectRelativeObservation(world, obj, pose));
            Assert.True(regeneratedAtBroadcast);
        }
        finally
        {
            world.PoseStateChanged -= changed;
            foreach (var p in output) p.WorldPose.Dispose();
            foreach (var p in Cache(obj).Values.SelectMany(values => values)) p.Stored.Dispose();
        }
    }

    // reach: unit (substitutes all six cached charger vectors; production generator join MISSING at
    // 0x004DF8BA -> 0x004E9FB0. Calls real SetPose/InitPose/relative-observation entries, not a clear hook).
    // Old code: six vectors retain one entry, selected query returns false. Each case fails Assert.Empty.
    [Theory]
    [InlineData("SetPose")]
    [InlineData("InitPose")]
    [InlineData("RelativeObservation")]
    [InlineData("LiftObservation")]
    public void UnchangedChargerSetPoseClearsAllSixVectorsBeforeTheNextQuery(string entry)
    {
        using var rig = new M4ControlTests.Rig(); rig.ToSynced();
        using var pose = new Pose3d(Rotation3d.Identity, new Vec3(100, 0, 0));
        using var stored = new Pose3d(Rotation3d.Identity, new Vec3(1, 0, 0));
        var obj = new ObservableObject(700, ObjectType.Charger_Basic, ChargerGeometry.Markers);
        obj.InitPose(pose, PoseState.Known);
        var caches = Cache(obj);
        foreach (var type in Enumerable.Range(0, 6).Select(i => (PreActionType)i))
            caches[type] = new() { new(obj.Markers[0], stored, new Vec3(1, 0, 0), 0, 0) };
        // Filter all entries, so this tests collection/regeneration independently of unresolved charger geometry.
        var rejectCode = new[] { unchecked((MarkerType)short.MaxValue) };
        Assert.False(Query(obj, PreActionType.Docking, pose, new(), rejectCode));
        var before = obj.Pose.Translation;
        switch (entry)
        {
            case "SetPose": obj.SetPose(pose, -1, PoseState.Known); break;
            case "InitPose": obj.InitPose(pose, PoseState.Known); break;
            case "RelativeObservation": rig.Robot.Vision.World.ApplyRelativeObservation(obj, pose, PoseState.Known, null); break;
            case "LiftObservation": rig.Robot.Vision.World.ApplyLiftObservation(obj, pose); break;
        }
        Assert.Equal(before, obj.Pose.Translation);
        Assert.All(caches.Values, values => Assert.Empty(values));
        // 0x004DF8AE/BA/BE: empty selected cache calls generation and sets regenerated even with no output.
        Assert.True(Query(obj, PreActionType.Docking, pose, new(), rejectCode));
    }

    // reach: unit (real cube cache generation/export, explicit Pose3d goal copy substitutes DriveToObject's
    // goal consumer; complete stored/world parent binding remains MISSING at 0x0050DFFA..0x0050E002).
    // Old code: next query returns false after same-value SetPose, failing Assert.True; retained goal already
    // keeps its pose on the old code. That assertion is a preservation control, not a claimed old defect.
    [Fact]
    public void GoalCopiedBeforeSameValueSetPoseKeepsItsPoseWhileTheCacheRegenerates()
    {
        // A: old fixture had no object parent and produced 0 goals; production supplies its origin parent.
        using var origin=new Pose3d("origin");
        using var pose = new Pose3d(Rotation3d.Identity, new Vec3(100, 0, 22),parent:origin);
        using var robot = new Pose3d(Rotation3d.Identity, new Vec3(0, 0, 0));
        var obj = new ObservableObject(701, ObjectType.Block_LIGHTCUBE1,
            CubeGeometry.MarkersFor(ObjectType.Block_LIGHTCUBE1));
        obj.InitPose(pose, PoseState.Known);
        var output = new List<PreActionPose>();
        var regenerated = new List<PreActionPose>();
        try
        {
            Assert.True(Query(obj, PreActionType.Docking, robot, output));
            Assert.NotEmpty(output);
            using var goal = new Pose3d(output[0].WorldPose);
            var position = goal.Translation; var rotation = goal.Quaternion; var node = goal.Node;
            Assert.NotSame(output[0].WorldPose.Node, node);
            Assert.False(Query(obj, PreActionType.Docking, robot, regenerated));
            foreach (var p in regenerated) p.WorldPose.Dispose();
            regenerated.Clear();
            obj.SetPose(pose, -1, PoseState.Known);
            Assert.True(Query(obj, PreActionType.Docking, robot, regenerated));
            Assert.Same(node, goal.Node);
            Assert.Equal(position, goal.Translation);
            Assert.Equal(rotation, goal.Quaternion);
        }
        finally
        {
            foreach (var p in output.Concat(regenerated)) p.WorldPose.Dispose();
            foreach (var p in Cache(obj).Values.SelectMany(values => values)) p.Stored.Dispose();
            foreach(var marker in obj.Markers)marker.PoseOnObject.Dispose();obj.NativePose.Dispose();
        }
    }
}
