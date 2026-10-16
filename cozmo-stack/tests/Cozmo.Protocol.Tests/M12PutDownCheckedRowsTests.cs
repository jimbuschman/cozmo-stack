using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Cozmo.Transport;
using Xunit;
namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class M12PutDownCheckedRowsTests
{
    private sealed class Fixture : IDisposable
    {
        internal readonly M4ControlTests.Rig Rig=new();
        internal readonly VisionSystem Vision;
        internal readonly ManipulationSystem M;
        internal readonly List<EngineLogEntry> Logs=new();
        private readonly bool _error=EngineErrorState.ErrorFlagSet;
        internal EngineRobot Robot=>Rig.Engine.Robot!;
        internal Fixture()
        {
            Rig.ToSynced();Vision=new VisionSystem(Rig.Robot,CameraCalibration.Nominal()){Enabled=false};M=new ManipulationSystem(Rig.Robot,Vision);
            Vision.World.CurrentOriginId=Robot.CurrentOriginId;Rig.Engine.LogEntry+=Logs.Add;
        }
        internal ObservableObject Add(uint id=7,Pose3d? parent=null)
        {
            var obj=new ObservableObject(id,ObjectType.Block_LIGHTCUBE1,CubeGeometry.MarkersFor(ObjectType.Block_LIGHTCUBE1))
            {Pose=new Pose3d(Rotation3d.Identity,new Vec3(2,3,4),parent:parent??Robot.LiftPose),PoseState=PoseState.Known,OriginId=Robot.CurrentOriginId,NativeCarryingTree=true,LastObservedTimestamp=42,FromDistance=8};
            Vision.World.AddLocatedObject(obj);return obj;
        }
        internal void Carry(ObservableObject obj,uint? topId=null)
            =>M.Docking.Carrying.AttachToLift(obj.ObjectId,obj.Markers[0],new Pose3d(""),topId,null);
        internal uint Release(bool delete=false)=>M.Docking.SetCarriedObjectAsUnattached(delete);
        public void Dispose(){M.Dispose();Vision.Dispose();Rig.Dispose();EngineErrorState.ErrorFlagSet=_error;}
    }
    [Fact]
    public void NoCarriedIdWarnsAndReturnsOneWithoutSending()
    {
        using var f=new Fixture();Assert.Equal(1u,f.Release());
        Assert.Contains(f.Logs,e=>e.Level==EngineLogLevel.Warning&&e.Name=="Robot.SetCarriedObjectAsUnattached.CarryingObjectNotSpecified"&&e.Text.EndsWith("Robot not carrying object, but told to place one. (Possibly actually rolling or balancing or popping a wheelie."));
        Assert.DoesNotContain(f.M.Docking.Sent,m=>m is CarryStateUpdate);
    }
    [Fact]
    public void MissingPrimaryReturnsOneSetsErrorAndDoesNotUnset()
    {
        using var f=new Fixture();f.M.Docking.Carrying.SetCarrying(7);EngineErrorState.ErrorFlagSet=false;
        Assert.Equal(1u,f.Release(true));Assert.Equal(7u,f.M.Docking.Carrying.CarriedObjectId);Assert.True(EngineErrorState.ErrorFlagSet);
        Assert.Contains(f.Logs,e=>e.Name=="Robot.SetCarriedObjectAsUnattached.CarryingObjectDoesNotExist"&&e.Text.EndsWith("Carrying object with ID=7 no longer exists."));
        Assert.DoesNotContain(f.M.Docking.Sent,m=>m is CarryStateUpdate);
    }
    [Fact]
    public void OriginMismatchKeepsThePrimaryAndSkipsUnsetAndDelete()
    {
        using var f=new Fixture();using var foreign=new Pose3d("foreign");var obj=f.Add(parent:foreign);f.Carry(obj);var node=obj.NativePose.Node;EngineErrorState.ErrorFlagSet=false;
        Assert.Equal(1u,f.Release(true));Assert.Equal(7u,f.M.Docking.Carrying.CarriedObjectId);Assert.True(EngineErrorState.ErrorFlagSet);
        Assert.Same(node,obj.NativePose.Node);Assert.Same(foreign.Node,obj.NativePose.Node!.Parent);Assert.Equal(PoseState.Known,obj.PoseState);
        Assert.Contains(f.Logs,e=>e.Name=="Robot.SetCarriedObjectAsUnattached.OriginMisMatch"&&e.Text.EndsWith("Could not get carrying object's pose relative to robot's origin."));
    }
    [Fact]
    public void ReleasedCubeIsDirtyInTheOriginAndSendsTheTwoCarryNoneBytes()
    {
        using var f=new Fixture();var obj=f.Add();f.Carry(obj);var node=obj.NativePose.Node;var placed=obj.Pose.Translation;
        Assert.Equal(0u,f.Release());Assert.Null(f.M.Docking.Carrying.CarriedObjectId);Assert.Equal((ushort)3,f.M.Docking.Carrying.NativeMarkerCode);
        Assert.Same(node,obj.NativePose.Node);Assert.Same(f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId).Node,obj.NativePose.Node!.Parent);Assert.Equal(placed,obj.NativePose.Translation);
        Assert.Equal((byte)1,obj.PoseInitialized);Assert.Equal(PoseState.Dirty,obj.PoseState);Assert.Equal(8,obj.FromDistance);Assert.Equal("Object_7",obj.NativePose.Name);
        Assert.Equal(1,obj.PoseConfirmationCount);Assert.Equal(42u,obj.Confirmation!.LastPoseUpdatedTime);Assert.Same(obj.NativePose.Node.Parent,obj.Confirmation.Pose.Node!.Parent);
        Assert.Equal(new byte[]{0x49,0},Assert.IsType<CarryStateUpdate>(Assert.Single(f.M.Docking.Sent)).ToBytes());
        Assert.Contains(f.Rig.Port.Sent,bytes=>bytes.SequenceEqual(new byte[]{0x49,0}));
        Assert.DoesNotContain(f.Logs,e=>e.Name=="Robot.UnSetCarryingObjects.StillAttached");
        var before=obj.Pose.Translation;f.Robot.ComputeLiftPose(0.5f);Assert.Equal(before,obj.Pose.Translation);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void TopFailureStillUnsetsBothIdsAndPrimaryWasAlreadyObserved(bool missing)
    {
        using var f=new Fixture();var obj=f.Add();using var foreign=new Pose3d("foreign");if(!missing)f.Add(8,foreign);f.Carry(obj,8);
        Assert.Equal(0u,f.Release());Assert.Equal(PoseState.Dirty,obj.PoseState);Assert.Null(f.M.Docking.Carrying.CarriedOnTopId);Assert.Null(f.M.Docking.Carrying.CarriedObjectId);
        Assert.Contains(f.Logs,e=>e.Name=="Robot.SetCarriedObjectAsUnattached."+(missing?"TopCarryingObjectDoesNotExist":"TopOriginMisMatch"));
        Assert.Single(f.M.Docking.Sent.OfType<CarryStateUpdate>());
    }
    [Fact]
    public void BothObjectsLeaveTheLiftTreeBeforeUnset()
    {
        using var f=new Fixture();var obj=f.Add();var top=f.Add(8,obj.NativePose);f.Carry(obj,8);Assert.Equal(0u,f.Release());
        Assert.Equal(PoseState.Dirty,top.PoseState);Assert.Same(f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId).Node,top.NativePose.Node!.Parent);
        Assert.Contains(f.Logs,e=>e.Name=="Robot.SetCarriedObjectAsUnattached.TopObjectPlaced");Assert.DoesNotContain(f.Logs,e=>e.Name=="Robot.UnSetCarryingObjects.StillAttachedToSomething");
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void DirectUnsetWarnsWhilePoseStaysAttachedAndTopOnlyDoesNotSend(bool topOnly)
    {
        using var f=new Fixture();var obj=f.Add();var top=f.Add(8,obj.NativePose);f.Carry(obj,8);f.M.Docking.Carrying.NativeMarkerCode=22;
        f.M.Docking.Carrying.UnsetCarrying(topOnly);
        Assert.Null(f.M.Docking.Carrying.CarriedOnTopId);Assert.Equal(topOnly?7u:(uint?)null,f.M.Docking.Carrying.CarriedObjectId);Assert.Equal(topOnly?(ushort)22:(ushort)3,f.M.Docking.Carrying.NativeMarkerCode);
        Assert.Same(f.Robot.LiftPose.Node,obj.NativePose.Node!.Parent);Assert.Same(obj.NativePose.Node,top.NativePose.Node!.Parent);
        Assert.Equal(topOnly?0:1,f.M.Docking.Sent.OfType<CarryStateUpdate>().Count());
        Assert.Contains(f.Logs,e=>e.Name=="Robot.UnSetCarryingObjects.StillAttachedToSomething");
        Assert.Equal(!topOnly,f.Logs.Any(e=>e.Name=="Robot.UnSetCarryingObjects.StillAttached"));
    }
    [Fact]
    public void DirectUnsetNullObjectErrorsButStillSendsAndClears()
    {
        using var f=new Fixture();f.M.Docking.Carrying.SetCarrying(9);EngineErrorState.ErrorFlagSet=false;f.M.Docking.Carrying.UnsetCarrying();
        Assert.Null(f.M.Docking.Carrying.CarriedObjectId);Assert.True(EngineErrorState.ErrorFlagSet);
        Assert.Contains(f.Logs,e=>e.Name=="Robot.UnSetCarryingObjects.NullObject"&&e.Text.EndsWith("Object 9 robot 1 thought it was carrying no longer exists in the world."));Assert.Single(f.M.Docking.Sent.OfType<CarryStateUpdate>());
    }
    [Fact]
    public void DeleteFilterUsesIdsCapturedBeforeUnset()
    {
        using var f=new Fixture();var obj=f.Add();f.Carry(obj);
        Assert.Equal(0u,f.Release(true));Assert.Null(f.Vision.World.GetLocatedObjectByIdHelper(7));
        Assert.DoesNotContain(f.Logs,e=>e.Name=="BlockWorld.ClearObjectHelper.ClearingCarriedObject");
        // Existing DeleteLocatedObjects is invoked; its remaining body is M12-046 MISSING, not certified by this fixture.
    }
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void MotorAutoDisableCallsTheNativeReleaseOnlyForTheLift(bool lift)
    {
        using var f=new Fixture();var obj=f.Add();f.Carry(obj);
        f.Rig.Data(new MotorAutoEnabled{MotorID=lift?MotorID.MOTOR_LIFT:MotorID.MOTOR_HEAD,Enabled=false});f.Rig.Tick();
        Assert.Equal(lift?(uint?)null:7u,f.M.Docking.Carrying.CarriedObjectId);
    }
    [Fact]
    public void PickAndPlaceSuccessEnablesMarkersAfterReleaseIncludingPrimaryFailure()
    {
        using var f=new Fixture();f.Vision.ModeEnableMask=1;f.M.Docking.Carrying.SetCarrying(7);
        f.Rig.Data(new PickAndPlaceResult{Field1=true,Field3=1});f.Rig.Tick();
        Assert.Equal(2,f.Vision.ModeEnableMask);Assert.Equal(7u,f.M.Docking.Carrying.CarriedObjectId); // A2 failure is discarded by caller, but EnableMode still follows.
        int error=f.Logs.FindIndex(e=>e.Name=="Robot.SetCarriedObjectAsUnattached.CarryingObjectDoesNotExist");int enable=f.Logs.FindIndex(e=>e.Name=="VisionSystem.EnablingMode");Assert.True(error>=0&&enable>error);
    }
    [Fact]
    public void SetPoseClearsPreActionCacheSoTheNextRequestRegenerates()
    {
        using var f=new Fixture();var obj=f.Add();f.Carry(obj);
        bool Request()=>CubePreActionPoses.GetCurrentPreActionPoses(obj,new[]{PreActionType.Docking},Array.Empty<MarkerType>(),f.Robot.MainPose,0,Array.Empty<PreActionObstacle>(),new List<PreActionPose>());
        Assert.True(Request());Assert.False(Request());Assert.Equal(0u,f.Release());Assert.True(Request());
    }
    [Fact]
    public void DirtyObservationClearsLocalizationBeforeThePoseBroadcast()
    {
        using var f=new Fixture();var obj=f.Add();f.Carry(obj);f.Vision.World.MarkerPosesRelativeToCamera=_=>true;
        Assert.True(f.Vision.World.SetLocalizedTo(obj));Assert.Equal(7u,f.Vision.World.LocalizedToObjectId);
        bool saw=false;f.Vision.World.PoseStateChanged+=(changed,oldState,newState)=>{if(changed==obj){saw=true;Assert.Null(f.Vision.World.LocalizedToObjectId);Assert.Equal(PoseState.Dirty,newState);}};
        Assert.Equal(0u,f.Release());Assert.True(saw);
    }
    [Fact]
    public async Task LogsDispatchAfterPoseTreeGateIsReleased()
    {
        using var f=new Fixture();var obj=f.Add();f.Carry(obj);Task? reader=null;
        f.Rig.Engine.LogEntry+=e=>{if(e.Name=="Robot.SetCarriedObjectAsUnattached.ObjectPlaced"){reader=Task.Run(()=>{lock(f.Robot.PoseTreeGate){}});Assert.True(reader.Wait(TimeSpan.FromSeconds(3)));}};
        Assert.Equal(0u,f.Release());Assert.NotNull(reader);await reader!;
    }
}
