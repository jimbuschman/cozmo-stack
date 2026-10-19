using System.Buffers.Binary;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Vision;
using Xunit;
namespace Cozmo.Protocol.Tests;
[Collection("M5 process statics")]
public sealed class M11DelocalizationBuilderTests
{
    private static ObservableObject Object(uint id,uint bucket,Pose3d parent)=>new(id,ObjectType.Block_LIGHTCUBE1,Array.Empty<KnownMarker>())
        {OriginId=bucket,Pose=new Pose3d(Rotation3d.Identity,new(12,0,22),parent:parent),PoseState=PoseState.Known,ActiveId=0,FromDistance=1};
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void SameRootDoesNotMoveOrTouchTheConfirmer()
    {
        var old=new Pose3d("Origin7"){Id=7};var world=new BlockWorld{CurrentOriginId=7};world.NativeOrigin=_=>old;
        var obj=Object(18,7,old);world.AddLocatedObject(obj);var node=obj.NativePose.Node;
        Assert.Equal(0u,world.UpdateObjectOrigin(18,7));Assert.Same(node,obj.NativePose.Node);Assert.Equal(7u,obj.OriginId);Assert.Null(obj.Confirmation);
    }
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void ChangedRootRekeysAndUpdatesExistingConfirmerWithoutChangingStateOrPose()
    {
        var old=new Pose3d("Origin7"){Id=7};var next=new Pose3d("Origin9"){Id=9};var world=new BlockWorld{CurrentOriginId=7};world.NativeOrigin=id=>id==7?old:next;
        var obj=Object(18,7,next);world.AddLocatedObject(obj);var node=obj.NativePose.Node;var q=obj.NativePose.Quaternion;var t=obj.NativePose.Translation;
        world.ApplyRelativeObservation(obj,obj.NativePose,PoseState.Known,4);
        obj.Confirmation!.Misses=3;obj.Confirmation.Pose=obj.NativePose.DeepCopy(); // Native emplace owns a distinct pose wrapper.
        var entry=obj.Confirmation;
        Assert.Equal(0u,world.UpdateObjectOrigin(18,7));Assert.Equal(9u,obj.OriginId);Assert.Same(node,obj.NativePose.Node);
        Assert.Equal(q,obj.NativePose.Quaternion);Assert.Equal(t,obj.NativePose.Translation);Assert.Equal(PoseState.Known,obj.PoseState);
        Assert.Same(entry,obj.Confirmation);Assert.Equal(4,entry.Count);Assert.Equal(3,entry.Misses);
        Assert.Equal(1u,world.UpdateObjectOrigin(18,7)); // Last old bucket erased, hence BadOrigin.
    }
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void ALocalizableObjectKeepsTheWholeOldBucketWithoutPoseStateWrites()
    {
        var origin=new Pose3d("Origin7"){Id=7};var world=new BlockWorld{CurrentOriginId=7};world.NativeOrigin=_=>origin;
        var good=Object(18,7,origin);world.AddLocatedObject(good);
        var other=new ObservableObject(19,ObjectType.Charger_Basic,Array.Empty<KnownMarker>()){OriginId=7,PoseState=PoseState.Dirty,Pose=new Pose3d("other",origin)};world.AddLocatedObject(other);
        var changes=0;world.PoseStateChanged+=(_,_,_)=>changes++;
        Assert.True(world.AnyRemainingLocalizableObjects(7));Assert.False(world.AnyRemainingLocalizableObjects(8));
        world.FinishDelocalization(9);Assert.Equal(2,world.Objects.Count);Assert.Equal(PoseState.Known,good.PoseState);Assert.Equal(PoseState.Dirty,other.PoseState);Assert.Equal(0,changes);
    }
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void VisionOnlyDefaultsAndOverwriteRetainTheNativeFieldsAndNoCullBranch()
    {
        var history=new RobotStateHistory();history.Add(new RobotState{Timestamp=5000});history.Add(new RobotState{Timestamp=5001});
        var root=new Pose3d("Origin1"){Id=1};var pose=new Pose3d(Rotation3d.Identity,new(1,2,3),parent:root);
        Assert.Equal(0u,history.AddVisionOnlyState(5001,pose,13,0.25f,0.5f));
        var stamp=history.VisionOnly[5001];var node=stamp.Pose.Node;
        Assert.Equal(1u,BinaryPrimitives.ReadUInt32LittleEndian(stamp.State));Assert.Equal(13u,BinaryPrimitives.ReadUInt32LittleEndian(stamp.State.AsSpan(4)));
        Assert.Equal(0x40A00000u,BinaryPrimitives.ReadUInt32LittleEndian(stamp.State.AsSpan(0x48)));Assert.Equal(0x300u,BinaryPrimitives.ReadUInt32LittleEndian(stamp.State.AsSpan(0x4C)));
        Assert.Equal(uint.MaxValue,BinaryPrimitives.ReadUInt32LittleEndian(stamp.State.AsSpan(0x50)));Assert.Equal(0xFF,stamp.State[0x5A]);
        history.VisionOnly.Add(1,new()); // Deliberate old-map fixture exposes whether the duplicate branch culls.
        Assert.Equal(0u,history.AddVisionOnlyState(5001,pose,14,1,2));Assert.Same(node,history.VisionOnly[5001].Pose.Node);Assert.True(history.VisionOnly.ContainsKey(1));
        Assert.Equal(0u,history.AddVisionOnlyState(5002,pose,15,1,2));Assert.False(history.VisionOnly.ContainsKey(1));
    }
    // reach: unit (direct Delocalize and explicit safe app drain; normal production drain is the tick boundary).
    [Fact]
    public void DelocalizationQueuesAppThenRunsKnownSubscribersBeforeHostDispatch()
    {
        using var rig=new Rig();var r=rig.Robot.Engine.Robot!;var order=new List<string>();
        rig.Robot.Engine.EngineToGameMessage+=(tag,body)=>{Assert.Equal((ushort)0x32,tag);Assert.Empty(body.ToArray());order.Add("app");};
        rig.Robot.Engine.DelocalizationConfigurationHook=()=>order.Add("configuration");rig.Robot.Engine.DelocalizationCubeHook=()=>order.Add("cubes");
        uint before=r.CurrentOriginId,delivered=rig.Robot.Engine.NativeUiDeliveryCount;r.Delocalize(false);rig.Robot.Engine.DrainAppDelivery();
        Assert.Equal(before+1,r.CurrentOriginId);Assert.Equal(new[]{"configuration","cubes","app"},order);Assert.Equal(new Vec3(-20,0,0),r.ForwardPose.Translation);
        Assert.Equal(1u,r.PoseFrameId);Assert.Equal((byte)1,r.VisionPoseUpdated);
        Assert.Equal(delivered+1,rig.Robot.Engine.NativeUiDeliveryCount); // E3 increments once before app delivery.
    }
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void MissingExternalInterfaceSkipsAppAndLocalEmitTogether()
    {
        using var rig=new Rig();var r=rig.Robot.Engine.Robot!;r.HasExternalInterface=false;int calls=0;
        rig.Robot.Engine.EngineToGameMessage+=(_,_)=>calls++;rig.Robot.Engine.DelocalizationConfigurationHook=()=>calls++;rig.Robot.Engine.DelocalizationCubeHook=()=>calls++;
        r.Delocalize(false);Assert.Equal(0,calls);
    }
}
