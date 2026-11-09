using System.Buffers.Binary;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Vision;
using Cozmo.Transport;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class ObjectStateReportTests : IDisposable
{
    private readonly bool _error = EngineErrorState.ErrorFlagSet;
    public void Dispose() => EngineErrorState.ErrorFlagSet = _error;

    [Fact]
    public void RootInputReportsFlattenedCopiesZeroId()
    {
        using var f=new M4ControlTests.Rig();f.ToSynced();
        using var root=new Pose3d("root input"){Id=77};
        Assert.True(NativeObjectPose.TryConvert(root,f.Engine.Robot!,out var pose));
        Assert.Equal(0u,pose.OriginId);
    }
    [Fact]
    public void PublicRequestsPublishOrderedImmutableSnapshotsAndNoIdleSignal()
    {
        using var f=new M4ControlTests.Rig();f.ToSynced();
        var owner=f.Engine.Robot!;var world=f.Robot.LocalizationWorld;
        M4ControlTests.ConnectCube(f,2,ObjectType.Block_LIGHTCUBE2);
        M4ControlTests.ConnectCube(f,0,ObjectType.Block_LIGHTCUBE1);
        var connected=world.ConnectedObjects.OrderBy(o=>(int)o.Type).ToArray();Assert.Equal(2,connected.Length);
        using var objectPose=new Pose3d(Rotation3d.Identity,new Vec3(200,50,22),parent:owner.GetNativeOrigin(owner.CurrentOriginId));
        var located=new ObservableObject(connected[0].ObjectId,ObjectType.Block_LIGHTCUBE1,CubeGeometry.MarkersFor(ObjectType.Block_LIGHTCUBE1))
        {Pose=objectPose,OriginId=owner.CurrentOriginId,PoseState=PoseState.Dirty,LastObservedTimestamp=0x1234};
        world.AddLocatedObject(located);
        var raw=new List<(ushort Tag,byte[] Body)>();var views=new List<object>();var local=new List<ushort>();
        f.Engine.EngineToGameMessage+=(tag,body)=>{if(tag is 0x53 or 0x54){Assert.False(Monitor.IsEntered(world.FrameGate));Assert.True(f.Engine.InvokeEngineRequest(()=>true));raw.Add((tag,body.ToArray()));}};
        f.Engine.LocatedObjectStates+=r=>views.Add(r);f.Engine.ConnectedObjects+=r=>views.Add(r);
        owner.Idle.Start(-1,1000);float idleDeadline=owner.Idle.DisconnectDeadline;
        uint count=f.Engine.NativeUiDeliveryCount;
        using var a=f.Engine.AnimationMessages.SubscribeMessage(0x53,true,_=>{Assert.Empty(raw);Assert.Empty(views);Assert.Equal(count+2,f.Engine.NativeUiDeliveryCount);local.Add(0x53);});
        using var b=f.Engine.AnimationMessages.SubscribeMessage(0x54,true,_=>{Assert.Empty(raw);Assert.Empty(views);Assert.Equal(count+1,f.Engine.NativeUiDeliveryCount);local.Add(0x54);});
        f.Robot.RequestConnectedObjects();f.Robot.RequestLocatedObjectStates();f.Tick();
        Assert.Equal(new ushort[]{0x54,0x53},local);Assert.Equal(new ushort[]{0x54,0x53},raw.Select(x=>x.Tag));Assert.Equal(count+2,f.Engine.NativeUiDeliveryCount);
        Assert.Equal(idleDeadline,owner.Idle.DisconnectDeadline);
        var cb=raw[0].Body;Assert.Equal(25,cb.Length);Assert.Equal(2,cb[0]);
        Assert.Equal(connected[0].ObjectId,BinaryPrimitives.ReadUInt32LittleEndian(cb.AsSpan(1)));Assert.Equal(2u,BinaryPrimitives.ReadUInt32LittleEndian(cb.AsSpan(5)));Assert.Equal(1u,BinaryPrimitives.ReadUInt32LittleEndian(cb.AsSpan(9)));
        Assert.Equal(connected[1].ObjectId,BinaryPrimitives.ReadUInt32LittleEndian(cb.AsSpan(13)));Assert.Equal(2u,BinaryPrimitives.ReadUInt32LittleEndian(cb.AsSpan(21)));
        var lb=raw[1].Body;Assert.Equal(51,lb.Length);Assert.Equal(1,lb[0]);Assert.Equal(located.ObjectId,BinaryPrimitives.ReadUInt32LittleEndian(lb.AsSpan(1)));Assert.Equal(0x1234u,BinaryPrimitives.ReadUInt32LittleEndian(lb.AsSpan(5)));
        Assert.Equal(BitConverter.SingleToUInt32Bits(200f),BinaryPrimitives.ReadUInt32LittleEndian(lb.AsSpan(17)));Assert.Equal(owner.CurrentOriginId,BinaryPrimitives.ReadUInt32LittleEndian(lb.AsSpan(45)));Assert.Equal((byte)2,lb[49]);Assert.Equal((byte)1,lb[50]);
        var saved=Assert.IsType<LocatedObjectStatesReport>(views[1]);Assert.Equal(200f,Assert.Single(saved.Objects).Pose.X);
        located.LastObservedTimestamp=0;using var moved=new Pose3d(Rotation3d.Identity,new Vec3(400,0,22),parent:owner.GetNativeOrigin(owner.CurrentOriginId));located.Pose=moved;
        Assert.Equal(0x1234u,saved.Objects[0].Timestamp);Assert.Equal(200f,saved.Objects[0].Pose.X);
    }

    [Fact]
    public void LocatedRequestIncludesMarkerlessAndExcludesOlderOrigin()
    {
        using var f=new M4ControlTests.Rig();f.ToSynced();var owner=f.Engine.Robot!;var world=f.Robot.LocalizationWorld;
        uint old=owner.CurrentOriginId;
        using var oldPose=new Pose3d(Rotation3d.Identity,new Vec3(500,0,22),parent:owner.GetNativeOrigin(old));
        var oldObject=new ObservableObject(77,ObjectType.Block_LIGHTCUBE1,Array.Empty<KnownMarker>()){Pose=oldPose,OriginId=old,PoseState=PoseState.Known};
        world.AddLocatedObject(oldObject);
        Assert.True(owner.AddOriginWithId(99));world.CurrentOriginId=99;
        M4ControlTests.ConnectCube(f,0,ObjectType.Block_LIGHTCUBE1);
        uint connectedOnly=Assert.Single(world.ConnectedObjects).ObjectId;
        using var current=new Pose3d(Rotation3d.Identity,new Vec3(100,0,0),parent:owner.GetNativeOrigin(99));
        var obstacle=world.AddCollisionObstacle(current);
        using var secondPose=new Pose3d(Rotation3d.Identity,new Vec3(400,0,22),parent:owner.GetNativeOrigin(99));
        using var firstPose=new Pose3d(Rotation3d.Identity,new Vec3(600,0,22),parent:owner.GetNativeOrigin(99));
        var second=new ObservableObject(101,ObjectType.Block_LIGHTCUBE2,Array.Empty<KnownMarker>()){Pose=secondPose,OriginId=99,PoseState=PoseState.Known,LastObservedTimestamp=20};
        var first=new ObservableObject(103,ObjectType.Block_LIGHTCUBE1,Array.Empty<KnownMarker>()){Pose=firstPose,OriginId=99,PoseState=PoseState.Dirty,LastObservedTimestamp=10};
        world.AddLocatedObject(second);world.AddLocatedObject(first);
        LocatedObjectStatesReport? report=null;f.Engine.LocatedObjectStates+=r=>report=r;
        byte[]? body=null;f.Engine.EngineToGameMessage+=(tag,data)=>{if(tag==0x53)body=data.ToArray();};
        f.Robot.RequestLocatedObjectStates();f.Tick();
        var rows=Assert.IsType<LocatedObjectStatesReport>(report).Objects;
        Assert.Equal(new[]{first.ObjectId,second.ObjectId,obstacle.ObjectId},rows.Select(r=>r.ObjectId));
        Assert.All(rows,row=>Assert.False(row.Connected));
        Assert.Equal(99u,rows[0].Pose.OriginId);Assert.Equal(99u,rows[1].Pose.OriginId);
        Assert.Equal(0u,rows[2].Pose.OriginId); // Existing M11-062 markerless ingress supplies a detached pose, not bucket 99.
        Assert.DoesNotContain(rows,row=>row.ObjectId==oldObject.ObjectId || row.ObjectId==connectedOnly);
        Assert.NotNull(body);Assert.Equal(151,body!.Length);Assert.Equal(3,body[0]);
        Assert.Equal(103u,BinaryPrimitives.ReadUInt32LittleEndian(body.AsSpan(1)));Assert.Equal(10u,BinaryPrimitives.ReadUInt32LittleEndian(body.AsSpan(5)));Assert.Equal(1u,BinaryPrimitives.ReadUInt32LittleEndian(body.AsSpan(13)));
        Assert.Equal(101u,BinaryPrimitives.ReadUInt32LittleEndian(body.AsSpan(51)));Assert.Equal(20u,BinaryPrimitives.ReadUInt32LittleEndian(body.AsSpan(55)));Assert.Equal(2u,BinaryPrimitives.ReadUInt32LittleEndian(body.AsSpan(63)));
        Assert.Equal(obstacle.ObjectId,BinaryPrimitives.ReadUInt32LittleEndian(body.AsSpan(101)));Assert.Equal(6u,BinaryPrimitives.ReadUInt32LittleEndian(body.AsSpan(109)));
    }

    [Fact]
    public void RobotConstructionPublishesEmptyStatesBeforeDelocalization()
    {
        using var f=new M4ControlTests.Rig();var raw=new List<(ushort,byte[])>();
        f.Engine.EngineToGameMessage+=(tag,body)=>{if(tag is 0x53 or 0x32)raw.Add((tag,body.ToArray()));};
        f.ToSynced();
        Assert.True(raw.Count>=2);Assert.Equal((ushort)0x53,raw[0].Item1);Assert.Equal(new byte[]{0},raw[0].Item2);
        Assert.Equal((ushort)0x32,raw[1].Item1);Assert.Empty(raw[1].Item2);
    }

    [Fact]
    public void OriginUpdateTailEnqueuesBeforeConfigurationForce()
    {
        using var f=new M4ControlTests.Rig();f.ToSynced();var owner=f.Engine.Robot!;var world=f.Robot.LocalizationWorld;
        bool local=false,forced=false;uint before=f.Engine.NativeUiDeliveryCount;
        using var sub=f.Engine.AnimationMessages.SubscribeMessage(0x53,true,_=>{Assert.Equal(before+1,f.Engine.NativeUiDeliveryCount);local=true;});
        world.BlockConfigurationManagerForceUpdate=()=>{Assert.True(local);forced=true;};
        using var pose=new Pose3d();Assert.Equal(0u,world.UpdateObjectOrigins(owner.CurrentOriginId,owner.CurrentOriginId,pose));
        Assert.True(local);Assert.True(forced);
    }

    [Fact]
    public void DelocalizationLocalSnapshotCannotResumeRetiredOwner()
    {
        using var f=new M4ControlTests.Rig();f.ToSynced();var original=f.Engine.Robot!;
        bool replaced=false;int delocalized=0;
        using var one=f.Engine.AnimationMessages.SubscribeMessage(0x53,true,_=>
        {
            if(replaced)return;replaced=true;
            f.Engine.Robots.RemoveRobot(CozmoEngine.RobotId,false);
            f.Engine.Robots.AddRobot(CozmoEngine.RobotId,withRic:false);
        });
        using var two=f.Engine.AnimationMessages.SubscribeMessage(0x32,true,_=>delocalized++);
        using(f.Engine.EnterSynchronousOwner())original.Delocalize(false);
        Assert.True(replaced);Assert.NotSame(original,f.Engine.Robot);
        Assert.Equal(1,delocalized); // replacement constructor only; no stale original tail.
    }

    [Fact]
    public void OriginUpdateLocalSnapshotCannotForceConfigurationOnReplacement()
    {
        using var f=new M4ControlTests.Rig();f.ToSynced();var original=f.Engine.Robot!;var world=f.Robot.LocalizationWorld;
        bool replaced=false,forced=false;
        world.BlockConfigurationManagerForceUpdate=()=>forced=true;
        using var sub=f.Engine.AnimationMessages.SubscribeMessage(0x53,true,_=>
        {
            if(replaced)return;replaced=true;
            f.Engine.Robots.RemoveRobot(CozmoEngine.RobotId,false);
            f.Engine.Robots.AddRobot(CozmoEngine.RobotId,withRic:false);
        });
        using var pose=new Pose3d();
        using(f.Engine.EnterSynchronousOwner())world.UpdateObjectOrigins(original.CurrentOriginId,original.CurrentOriginId,pose);
        Assert.True(replaced);Assert.NotSame(original,f.Engine.Robot);Assert.False(forced);
    }

    [Fact]
    public void EmptyRepeatedRequestsAreSeparateAndRetiredOwnerCannotReply()
    {
        using var f=new M4ControlTests.Rig();f.ToSynced();var reports=new List<(ushort,byte[])>();
        f.Engine.EngineToGameMessage+=(tag,body)=>{if(tag is 0x53 or 0x54)reports.Add((tag,body.ToArray()));};
        f.Robot.RequestLocatedObjectStates();f.Robot.RequestLocatedObjectStates();f.Robot.RequestConnectedObjects();f.Tick();
        Assert.Equal(new ushort[]{0x53,0x53,0x54},reports.Select(x=>x.Item1));Assert.All(reports,x=>Assert.Equal(new byte[]{0},x.Item2));
        reports.Clear();f.Robot.RequestConnectedObjects();f.Engine.Robots.RemoveRobot(CozmoEngine.RobotId,false);f.Engine.Robots.AddRobot(CozmoEngine.RobotId,withRic:false);f.Tick();
        Assert.DoesNotContain(reports,x=>x.Item1==0x54);
    }

    [Fact]
    public void ConnectedVectorCountWrapDoesNotTruncateRows()
    {
        using var f=new M4ControlTests.Rig();f.ToSynced();byte[]? body=null;
        f.Engine.EngineToGameMessage+=(tag,b)=>{if(tag==0x54)body=b.ToArray();};
        var rows=Enumerable.Range(0,256).Select(i=>new ConnectedObjectState((uint)i,2,1)).ToArray();
        f.Engine.PublishConnectedObjects(new(Array.AsReadOnly(rows)));f.Engine.DrainAppDelivery();
        Assert.NotNull(body);Assert.Equal(1+256*12,body!.Length);Assert.Equal(0,body[0]);Assert.Equal(255u,BinaryPrimitives.ReadUInt32LittleEndian(body.AsSpan(1+255*12)));
    }
}
