using Cozmo.Robot;
using Cozmo.Robot.Vision;
using Xunit;
namespace Cozmo.Protocol.Tests;
[Collection("M5 process statics")]
public sealed class M13RobotMapWriterTests
{
    // reach: production (robot state dispatch -> Robot::Update -> current map -> native ray, no map substitution).
    // Old engine: no robot-pose map call at 0x00513CD4; selected type-1 ray returns false.
    [Fact]
    public void EngineTickWritesTheActualCurrentMapBeforeFreeplay()
    {
        using var rig=new M4ControlTests.Rig();rig.ToSynced();rig.State();
        var map=Assert.IsType<MemoryMap>(rig.Robot.LocalizationWorld.Maps.Current);
        Assert.True(map.HasCollisionRayWithTypes(new(-40,0),new(-30,0),t=>t==MemoryMapContentType.ClearOfObstacle));
        Assert.Empty(map.Regions);
    }
    // reach: unit (supplies pose/cliff/time to writer, observer only; whiteboard origin MISSING at 0x0056AC92).
    // Old code: zero native writes, one legacy region; rootless repeat is suppressed.
    [Fact]
    public void ParentGateSkipsUnchangedOwnedPoseButRootlessPoseWritesAgain()
    {
        var map=new MemoryMap();using var root=new Pose3d("root");using var pose=new Pose3d("pose",root);
        int writes=0;map.ObjectInsertForTest=(_,_)=>writes++;
        try
        {
            map.UpdateRobotPose(pose);Assert.Equal(2,writes);
            map.UpdateRobotPose(pose);Assert.Equal(2,writes);
            pose.ClearParent();map.UpdateRobotPose(pose);Assert.Equal(4,writes);
            map.UpdateRobotPose(pose);Assert.Equal(6,writes);
            Assert.Empty(map.Regions);
        }
        finally{map.ReleaseRobotPose();}
    }
    // reach: unit (substitutes whiteboard callback; actual public writer, full transform, tree insert and stamps).
    // Old code: 0 native writes and 0 clear notifications, only a yaw-flattened legacy patch.
    [Theory] [InlineData(false)] [InlineData(true)]
    public void PatchThenFootprintShareOneStampAndNotifyBeforeSaving(bool cliff)
    {
        var map=new MemoryMap();using var root=new Pose3d("root");
        using var pose=new Pose3d(Rotation3d.Identity,new Vec3(10,20,0),parent:root);
        var writes=new List<(Vec2[] P,SourceMapData D)>();map.ObjectInsertForTest=(p,d)=>writes.Add((p,d));
        int calls=0;
        try
        {
            map.UpdateRobotPose(pose,cliff,123,q=>
            {
                calls++;Assert.Equal(2,writes.Count);
                Assert.Null(typeof(MemoryMap).GetField("_lastRobotPose",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(map));
                Assert.Equal(new Vec2(15,25),writes[0].P[0]);
                Assert.Equal(new[]{new Vec2(15,25),new Vec2(5,25),new Vec2(5,15),new Vec2(15,15)},writes[0].P);
                Assert.Equal(cliff?MemoryMapContentType.Cliff:MemoryMapContentType.ClearOfCliff,writes[0].D.Type);
                Assert.Equal(MemoryMapContentType.ClearOfObstacle,writes[1].D.Type);
                Assert.All(writes,w=>{Assert.Equal(123u,w.D.First);Assert.Equal(123u,w.D.Last);});
                Assert.Equal((float)(10f+BitConverter.UInt32BitsToSingle(0x41B0CCCC)),(float)q.P0.X);
                Assert.Equal(q.P0,writes[1].P[0]);Assert.Equal(q.P3,writes[1].P[2]);
                if(cliff)Assert.Equal(new Vec2(1,0),Assert.IsType<CliffMapData>(writes[0].D).Normal);
            });
            Assert.Equal(1,calls);map.UpdateRobotPose(pose,cliff,124,_=>calls++);
            Assert.Equal(2,writes.Count);Assert.Equal(1,calls);Assert.Empty(map.Regions);
        }
        finally{map.ReleaseRobotPose();}
    }
    // reach: unit (supplies full 3D pose/cliff/time; whiteboard origin MISSING at 0x0056AC92).
    // Old code: patch centre is (0,0); no native cliff data, and z=25 is discarded.
    [Fact]
    public void TiltUsesLocalZ25AndProjectedXAxisWithoutNormalizing()
    {
        var map=new MemoryMap();using var pose=new Pose3d(Rotation3d.FromAngleAxis(MathF.PI/2,new Vec3(0,1,0)),Vec3.Zero);
        var writes=new List<(Vec2[] P,SourceMapData D)>();map.ObjectInsertForTest=(p,d)=>writes.Add((p,d));
        try
        {
            map.UpdateRobotPose(pose,true,77);Assert.Equal(2,writes.Count);
            Assert.All(writes[0].P,p=>Assert.InRange(p.X,24.999,25.001));
            var data=Assert.IsType<CliffMapData>(writes[0].D);Assert.InRange(Math.Abs(data.Normal.X),0,0.00001);Assert.Equal(0,data.Normal.Y);
        }
        finally{map.ReleaseRobotPose();}
    }
    // reach: unit (supplies pose/time to writer -> actual tree/ray; whiteboard origin MISSING at 0x0056AC92).
    // Old code: native type-1 footprint absent, selected clear-of-obstacle query is false.
    [Fact]
    public void FootprintIsVisibleOnlyToSelectedTypeOneMask()
    {
        var map=new MemoryMap();using var pose=new Pose3d("robot");
        try
        {
            map.UpdateRobotPose(pose,false,91);
            Assert.True(map.HasCollisionRayWithTypes(new(-40,0),new(-30,0),t=>t==MemoryMapContentType.ClearOfObstacle));
            Assert.False(map.HasCollisionRayWithTypes(new(-40,0),new(-30,0),t=>t==MemoryMapContentType.Cliff));
            Assert.False(map.HasCollisionRayWithTypes(new(-40,0),new(-30,0)));
            Assert.Empty(map.Regions);
        }
        finally{map.ReleaseRobotPose();}
    }
}
