using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;
using Fixture=Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;
namespace Cozmo.Protocol.Tests;

// Explicit offline scene and firmware inputs. Drive feedback consumes sent segments;
// it never assigns a native planner goal, yaw override, helper result or terminal action state.
internal sealed class PopFirmwareScene:IDisposable
{
    internal readonly Fixture F=new();
    internal readonly uint Target;
    internal bool Rolled,Grounded,Carry;
    internal int Rolls,Grounds,FailRolls;
    internal bool HideMarkers;
    internal float Pitch;
    private Rotation3d PhysicalRotation=>Rotation3d.Identity;
    private int _mark,_motorMark,_next;
    private sbyte _segment=-1;
    private ExecutePath? _pending;
    private readonly List<RobotMessage> _wire=new();
    private readonly HashSet<ushort> _paths=new();
    private readonly HashSet<byte> _animations=new();
    private uint _image;
    private readonly string _behaviorId;
    internal readonly List<string> Trace=new();
    internal PopFirmwareScene(bool carrying=false,string behaviorId="PopAWheelie")
    {
        _behaviorId=behaviorId;
        M4ControlTests.ConnectCube(F.Rig,0,ObjectType.Block_LIGHTCUBE1);
        Target=F.M.World.ConnectedObjects.Single(o=>o.Type==ObjectType.Block_LIGHTCUBE1).ObjectId;
        Add(Target,ObjectType.Block_LIGHTCUBE1,150);
        F.Robot.Progression!.Unlocked.Add(13);
        F.Runtime.Configurations.ForceUpdate();F.Runtime.Configurations.Update();F.Rig.Robot.Animations.LoadFrom(F.Rig.Engine.Options.ResourcesPath!);
        Carry=carrying;
        if(carrying){var obj=F.M.World.GetLocatedObjectByIdHelper(Target)!;Assert.Equal(0u,F.M.Docking.SetObjectAsAttachedToLift(Target,obj.Markers.First(m=>m.Face==BlockFace.Front).Code));}
        F.Rig.Engine.AnimationMessages.SubscribeMessage(0x5A,true,p=>{if(p is RobotCompletedAction r)Trace.Add($"COMPLETE type{r.ActionType:X} result{r.Result:X8}");});
        F.Rig.Engine.AnimationMessages.SubscribeMessage(0x44,true,p=>{if(p is NativeObservedObject o)Trace.Add($"OBSERVED {o.ObjectId}");});
        F.Robot.BehaviorManagerOwner!.DisableReactionsWithLock("pop-offline-scene",Enumerable.Range(0,21).Select(i=>i is 1 or 2 or 8).ToArray(),false);
    }
    private void Add(uint id,ObjectType type,float x)
    {
        var obj=new ObservableObject(id,type,CubeGeometry.MarkersFor(type)){Pose=new Pose3d(PhysicalRotation,new(x,0,22),parent:F.Robot.GetNativeOrigin(F.Robot.CurrentOriginId)),OriginId=F.Robot.CurrentOriginId,PoseState=PoseState.Known,LastObservedTimestamp=F.Robot.StoredState!.Timestamp};
        obj.LastObservedMarkers=obj.Markers.Select(m=>m.Code).ToArray();foreach(var marker in obj.Markers)marker.LastObservedTimestamp=obj.LastObservedTimestamp;F.M.World.AddLocatedObject(obj);
    }
    internal void Motors(RobotStatusFlag extra=0)
    {
        foreach(var message in F.Rig.SentSince(_motorMark))switch(message)
        {
            case SetHeadAngle head:F.Head=head.AngleRad;F.Rig.Data(new MotorActionAck{ActionId=head.ActionId});break;
            case SetBodyAngle body:F.Yaw=body.AngleRad;F.Rig.Data(new MotorActionAck{ActionId=body.ActionId});break;
            case SetLiftHeight lift:F.Lift=(float)Math.Asin((lift.HeightMm-45f)/66f);F.Rig.Data(new MotorActionAck{ActionId=lift.ActionId});break;
        }
        _motorMark=F.Rig.Mark();var state=F.Rig.MakeState(RobotStatusFlag.HeadInPos|RobotStatusFlag.LiftInPos|extra,origin:F.Robot.CurrentOriginId,frame:F.Robot.PoseFrameId,x:F.X,y:F.Y,head:F.Head,liftAngle:F.Lift);
        state.Pose.Angle=F.Yaw;state.Pose.Pitch=Pitch;state.CurrPathSegment=_segment;F.Rig.Data(state);F.Rig.Tick();
    }
    private void Image()
    {
        F.M.Vision.Enabled=true;F.M.Vision.ModeEnableMask=1<<1;uint stamp=F.Robot.StoredState!.Timestamp;
        F.Rig.Data(new ImageImuData{ImageId=stamp,RateX=0,RateY=0,RateZ=0,Line2Number=0});F.Rig.Tick();
        var cal=F.M.Vision.Calibration!;var image=new GrayImage(cal.Columns,cal.Rows);image.Fill(150);
        using var hist=HistRobotState.From(F.Robot.GetCheckedMainPose(),F.Robot.StoredState!);using var camera=F.Robot.CopyHistoricalCameraPose(hist);using var flat=camera.GetWithRespectToRoot();var model=new CameraModel(cal,flat);
        // Independent scripted physical cube; no native goal or estimated world pose is used to draw it.
        if(!Carry&&!HideMarkers)
        {using var cube=new Pose3d(PhysicalRotation,new(150,0,22));OpaqueCube(image,model,cube);MarkerRenderer.DrawObject(image,MarkerLibrary.EmbeddedOrNull!,model,ObjectType.Block_LIGHTCUBE1,cube);}
        F.M.Vision.HandOverFrame(new CameraFrame{ImageId=++_image,Timestamp=stamp,Width=cal.Columns,Height=cal.Rows,Encoding=1,RawPayload=image.Pixels});
        F.M.Vision.WaitForProcessorIdle();F.Rig.Engine.VisionUpdateAllResultsFailed!();F.Rig.Tick();
    }
    // Scene physics: the existing marker-only renderer has no opaque cube body. Mask each
    // independently supplied cube's projected faces before its fiducials, hiding far markers.
    private static void OpaqueCube(GrayImage image,CameraModel camera,Pose3d cube)
    {
        foreach(var marker in CubeGeometry.MarkersFor(ObjectType.Block_LIGHTCUBE1))
        {
            var points=marker.Corners3d().Select(p=>p*(44.0/25.0)).Select(p=>cube.Apply(marker.PoseOnObject.Apply(p))).Select(camera.Project).ToArray();
            if(points.Any(p=>p is null))continue;
            var q=new[]{points[0]!.Value,points[1]!.Value,points[3]!.Value,points[2]!.Value};
            int minX=Math.Max(0,(int)Math.Floor(q.Min(p=>p.X))),maxX=Math.Min(image.Width-1,(int)Math.Ceiling(q.Max(p=>p.X)));
            int minY=Math.Max(0,(int)Math.Floor(q.Min(p=>p.Y))),maxY=Math.Min(image.Height-1,(int)Math.Ceiling(q.Max(p=>p.Y)));
            for(int y=minY;y<=maxY;y++)for(int x=minX;x<=maxX;x++)
            {
                bool pos=false,neg=false;for(int i=0;i<4;i++){var a=q[i];var b=q[(i+1)%4];double z=(b.X-a.X)*(y+.5-a.Y)-(b.Y-a.Y)*(x+.5-a.X);pos|=z>0;neg|=z<0;}
                if(!(pos&&neg))image[x,y]=150;
            }
        }
    }
    private void Receive()
    {
        var sent=F.Rig.SentSince(_mark);_mark=F.Rig.Mark();
        for(int messageIndex=0;messageIndex<sent.Count;messageIndex++)
        {
            var message=sent[messageIndex];
            if(message is ClearPath){_wire.Clear();_pending=null;_next=0;_segment=-1;}
            else if(message is AppendPathSegmentLine or AppendPathSegmentArc or AppendPathSegmentPointTurn)_wire.Add(message);
            else if(message is ExecutePath execute&&_paths.Add(execute.EventId))
            {_pending=execute;_next=0;F.Rig.Data(new PathFollowingEvent{EventId=execute.EventId,EventType=(byte)PathEventType.Started});}
            else if(message is DockWithObject dock)
            {
                // This offline firmware worker has not yet consumed the dock; a later abort
                // in the same input batch cancels it before supplied movement/status/results.
                if(sent.Skip(messageIndex+1).TakeWhile(m=>m is not DockWithObject).Any(m=>m is AbortDocking))
                {Trace.Add("DOCK_ABORTED before supplied firmware movement");continue;}
                Trace.Add("DOCK "+dock.DockAction+" method"+dock.DockingMethod+" C1"+dock.Field8);
                // Labeled firmware input: action9 pitches robot to independently supplied 1.2rad. Cube stays at fixed150/0/22; no goal/world estimate feeds the scene.
                Assert.Equal((byte)9,dock.DockAction);
                Rolls++;F.X=50f;F.Y=0;F.Yaw=0;bool success=FailRolls==0;if(FailRolls>0)FailRolls--;
                Motors(RobotStatusFlag.IsPickingOrPlacing);
                F.Rig.Data(new PickAndPlaceResult{Field1=success,Field3=0});
                if(success){Rolled=true;Pitch=1.2f;}
                Motors(RobotStatusFlag.IsPickingOrPlacing);Motors();
            }
            else if(message is PlaceObjectOnGround)
            {Grounds++;F.Lift=-.1598f;Motors(RobotStatusFlag.IsPickingOrPlacing);Carry=false;Grounded=true;F.Rig.Data(new PickAndPlaceResult{Field1=true,Field3=(byte)BlockStatus.BlockPlaced});F.X=20;Motors(RobotStatusFlag.IsPickingOrPlacing);Motors();}
        }
    }
    internal void Step(Func<bool>? stopBeforeInput=null)
    {
        if(F.M.GetNativePlanners().Lattice is {} runtime&&runtime.CheckPlanningStatus==1)
            Assert.True(SpinWait.SpinUntil(()=>runtime.CheckPlanningStatus!=1,TimeSpan.FromSeconds(10)),"offline planner watchdog");
        Motors();if(stopBeforeInput?.Invoke()==true)return;Receive();
        if(_pending is {} path)
        {
            if(_next<_wire.Count)
            {
                float x=F.X,y=F.Y;switch(_wire[_next])
                {
                    case AppendPathSegmentLine line:x=line.XEndMm;y=line.YEndMm;break;
                    case AppendPathSegmentArc arc:float a=arc.StartRad+arc.SweepRad;x=arc.XCenterMm+arc.RadiusMm*MathF.Cos(a);y=arc.YCenterMm+arc.RadiusMm*MathF.Sin(a);F.Yaw=a+MathF.Sign(arc.SweepRad)*Radians.Pi*.5f;break;
                    case AppendPathSegmentPointTurn turn:x=turn.XMm;y=turn.YMm;F.Yaw=turn.TargetAngleRad;break;
                }
                float offset=!Carry?20f:0f;F.X=x+offset*MathF.Cos(F.Yaw);F.Y=y+offset*MathF.Sin(F.Yaw);_segment=checked((sbyte)_next++);Motors();Receive();
            }
            else if(_next==F.Rig.Robot.Motion.Path.DoleSegmentCount)
            {F.Rig.Data(new PathFollowingEvent{EventId=path.EventId,EventType=(byte)PathEventType.Completed});_pending=null;_segment=-1;Motors();Receive();}
        }
        byte animation=F.Rig.Robot.Animations.CurrentTag;
        if(animation!=0&&_animations.Add(animation)){F.Rig.Data(new AnimationStarted{AnimId=animation});F.Rig.Data(new AnimationEnded{AnimId=animation});F.Rig.Tick();}
        Image();
        var pop=(NativePopAWheelieBehavior)F.Runtime.Bound[_behaviorId];
        Trace.Add($"{F.Rig.Engine.Timer.SecondsF} robot({F.X},{F.Y}) yaw{F.Yaw} head{F.Head} marker{F.M.Vision.LastResult?.Markers.Count} target{pop.Target} retries{pop.Retries} runner{pop.Interaction?.State:X8} pop{pop.Interaction?.Pop.State:X8}");
    }
    public void Dispose()=>F.Dispose();
}
