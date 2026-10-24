using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Vision;
using Xunit;
namespace Cozmo.Protocol.Tests;
[Collection("M5 process statics")]
public sealed class M13RobotMapFixupTests
{
    // reach: production (RobotState dispatch -> Robot::Update -> current map inserts).
    // Old value: first inserted type is Cliff (8), despite no CliffEvent being received.
    [Fact]
    public void CliffStatusWithoutEventWritesClearOfCliff()
    {
        using var rig=new M4ControlTests.Rig();rig.ToSynced();
        var map=Assert.IsType<MemoryMap>(rig.Robot.LocalizationWorld.Maps.Current);
        var writes=new List<MemoryMapContentType>();map.ObjectInsertForTest=(_,d)=>writes.Add(d.Type);
        rig.State(flags:RobotStatusFlag.IsBodyAccMode|RobotStatusFlag.CliffDetected);
        Assert.True(rig.Robot.Sensors.CliffDetectedNow);
        Assert.False(rig.Robot.Sensors.CliffDetectedByEvent);
        Assert.Equal(new[]{MemoryMapContentType.ClearOfCliff,MemoryMapContentType.ClearOfObstacle},writes);
    }
    // reach: unit (supplies origins/poses and counts ProcessClearQuad callbacks; actual map owner/insert path).
    // Old value: returning to the kept first origin adds 2 inserts and 1 ProcessClearQuad (totals 6 and 3).
    [Fact]
    public void ReturningToKeptOriginWithoutMovingDoesNotWrite()
    {
        using var maps=new SourceMapComponent();using var root=new Pose3d("root");
        using var first=new Pose3d("first",root);
        using var moved=new Pose3d(Rotation3d.Identity,new Vec3(100,0,0),parent:root);
        int inserts=0,clears=0;
        void Observe()=>maps.Current!.ObjectInsertForTest=(_,_)=>inserts++;
        maps.CreateLocalizedMemoryMap(1,_=>false);Observe();
        maps.UpdateRobotPose(first,false,1,_=>clears++);
        maps.CreateLocalizedMemoryMap(2,_=>false);Observe();
        maps.UpdateRobotPose(moved,false,2,_=>clears++);
        maps.CreateLocalizedMemoryMap(1,_=>false);
        maps.UpdateRobotPose(moved,false,3,_=>clears++);
        Assert.Equal(4,inserts);Assert.Equal(2,clears);
        // These map maintenance operations also leave MapComponent+0x2C untouched.
        maps.Current!.Clear();maps.UpdateRobotPose(moved,false,4,_=>clears++);
        maps.UpdateMapOrigins(2,1,()=>{});maps.UpdateRobotPose(moved,false,5,_=>clears++);
        maps.CreateLocalizedMemoryMap(1,id=>id==2);maps.UpdateRobotPose(moved,false,6,_=>clears++);
        Assert.Equal(4,inserts);Assert.Equal(2,clears);
    }
}
