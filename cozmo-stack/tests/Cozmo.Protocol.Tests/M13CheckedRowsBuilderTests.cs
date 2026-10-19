using System.Reflection;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Cozmo.Transport;
using Xunit;
namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class M13CheckedRowsBuilderTests
{
    private sealed record Entry(EngineLogLevel Level,string? Channel,string Key,string Text);
    private sealed class Input : IPanTiltRobot, INativePanTiltServices
    {
        public object StateGate { get; }=new();
        public bool OnTreads {get;set;}=true;
        public string OffTreadsName {get;set;}="OnBack";
        public uint OriginId {get;set;}=1;
        public float PoseAngleAroundZ {get;set;}
        public float HeadAngle=>0;
        public bool BodyMoving {get;set;}
        public bool HeadMoving=>false;
        internal readonly List<Entry> Logs=new();
        internal readonly List<string> Eyes=new();
        internal float EyeX;
        public SourceLogSink SourceLog => (l,c,k,t)=>Logs.Add(new(l,c,k,t));
        public uint TurnInPlace(float a,float s,float accel,float tol,ushort half,bool absolute,out byte id){id=7;return 0;}
        public uint MoveHeadToAngle(float a,float s,float accel,float duration,out byte id){id=8;return 0;}
        public void RemoveKeepFaceAlive(int duration)=>Eyes.Add("keep:"+duration);
        public byte AddEyeShift(byte tag,float x){Eyes.Add("add:"+tag);EyeX=x;return 9;}
        public void RemoveEyeShift(byte tag,int duration)=>Eyes.Add($"remove:{tag}:{duration}");
    }
    private static void Set(TurnInPlaceAction action,string property,float value) =>
        typeof(TurnInPlaceAction).GetProperty(property)!.SetValue(action,value);

    [Fact]
    public void LiveMovementPrimitivesPreservePayloadBitsShareTheIdAndConsumeFailures()
    {
        using var rig=new M4ControlTests.Rig();rig.ToSynced();int mark=rig.Mark();
        Assert.Equal(0u,rig.Robot.Motion.MoveHeadToAngle(BitConverter.Int32BitsToSingle(0x7FC12345),1,2,BitConverter.Int32BitsToSingle(unchecked((int)0x80000000)),out var head));
        Assert.Equal((byte)1,head);
        Assert.Equal("374523C17F0000803F000000400000008001",Convert.ToHexString(Assert.Single(rig.RawSince(mark))));
        mark=rig.Mark();
        Assert.Equal(0u,rig.Robot.Motion.TurnInPlace(3,-2,3,BitConverter.Int32BitsToSingle(unchecked((int)0x80000000)),0xABCD,true,out var body));
        Assert.Equal((byte)2,body);
        Assert.Equal("3900004040000000C00000404000000080CDAB0102",Convert.ToHexString(Assert.Single(rig.RawSince(mark))));
        var logs=new List<EngineLogEntry>();rig.Engine.LogEntry+=logs.Add;
        rig.Engine.Robot!.SendFault=message=>message is SetHeadAngle?false:null;
        Assert.Equal(1u,rig.Robot.Motion.MoveHeadToAngle(0,0,0,0,out var failed));Assert.Equal((byte)3,failed);
        var warning=Assert.Single(logs.Where(e=>e.Name=="Robot.SendMessage"));
        Assert.Equal("headAngle",warning.Data["$data"]);
        Assert.Equal((byte)4,rig.Robot.Motion.NextActionId());
        for(int i=0;i<251;i++)rig.Robot.Motion.NextActionId();
        Assert.Equal(1u,rig.Robot.Motion.MoveHeadToAngle(0,0,0,0,out var wrapped));Assert.Equal((byte)0,wrapped);
    }

    [Fact]
    public void SeparateGlobalCountersStartAtTenAndWaitBypassesAllOtherChecks()
    {
        lock(TurnInPlaceAction.DiagnosticCounterGate)
        {
            ushort oldWait=TurnInPlaceAction.WaitingAckCounter,oldAngle=TurnInPlaceAction.AngleNotReachedCounter;
            try
            {
                TurnInPlaceAction.WaitingAckCounter=TurnInPlaceAction.AngleNotReachedCounter=10;
                var input=new Input();using var action=new TurnInPlaceAction(input,1,false);Assert.Equal(0u,action.Init());
                input.OnTreads=false;
                Assert.Equal(PanTiltResult.Running,action.CheckIfDone());
                var waiting=Assert.Single(input.Logs);Assert.Equal((EngineLogLevel.Debug,"Actions","TurnInPlaceAction.CheckIfDone.WaitingForAck"),(waiting.Level,waiting.Channel,waiting.Key));
                Assert.Equal($"[{action.Tag}] ActionID: 7",waiting.Text);
                Assert.Equal((ushort)0,TurnInPlaceAction.WaitingAckCounter);Assert.Equal((ushort)10,TurnInPlaceAction.AngleNotReachedCounter);
                input.OnTreads=true;action.HandleMotorActionAck(7);input.BodyMoving=true;
                Assert.Equal(PanTiltResult.Running,action.CheckIfDone());
                var angle=Assert.Single(input.Logs.Where(e=>e.Key.EndsWith("AngleNotReached")));
                Assert.Equal($"[{action.Tag}] Waiting for body to reach angle: 0.0deg vs. 57.3deg(+/-0.0), angDistTravd=+0.0deg, angDistExpc=+57.3deg (tol: 2.000000) (pfid: 1)",angle.Text);
                Assert.Equal((ushort)0,TurnInPlaceAction.AngleNotReachedCounter);
                input.BodyMoving=false;
                Assert.Equal(PanTiltResult.MotorStoppedMakingProgress,action.CheckIfDone());
                Assert.Equal($"[{action.Tag}] giving up since we stopped moving. currentAngle=0.0deg, target=57.3deg, angDistExp=57.3deg, angDistTrav=0.0deg (pfid: 1)",Assert.Single(input.Logs.Where(e=>e.Key=="TurnInPlaceAction.StoppedMakingProgress")).Text);
                TurnInPlaceAction.WaitingAckCounter=ushort.MaxValue;
                using var next=new TurnInPlaceAction(new Input(),1,false);next.Init();next.CheckIfDone();
                Assert.Equal((ushort)0,TurnInPlaceAction.WaitingAckCounter);
            }
            finally {TurnInPlaceAction.WaitingAckCounter=oldWait;TurnInPlaceAction.AngleNotReachedCounter=oldAngle;}
        }
    }

    [Fact]
    public void InPositionEyeRemovalAndOffTreadsLogsHaveSourceOrderAndArguments()
    {
        var input=new Input();using var action=new TurnInPlaceAction(input,1,false);action.Init();action.HandleMotorActionAck(7);
        Assert.Equal((byte)9, action.Byte0xD9); // Created by the checked Init wrapper.
        input.PoseAngleAroundZ=1;input.OnTreads=false;
        Assert.Equal(PanTiltResult.InvalidOffTreadsState,action.CheckIfDone());
        Assert.Equal(new[]{"TurnInPlaceAction.CheckIfDone.RemovingEyeShift","TurnInPlaceAction.CheckIfDone.InPosition","TurnInPlaceAction.OffTreadsStateInvalid"},input.Logs.Select(e=>e.Key));
        Assert.Equal("Currently at 57.3deg, on the way to 57.3deg (traversed 57.3deg)",input.Logs[0].Text);
        Assert.Equal($"[{action.Tag}] In Position: 57.3deg vs. 57.3deg(+/-0.0), angDistTravd=+57.3deg, angDistExpc=+57.3deg (tol: 2.000000) (pfid: 1)",input.Logs[1].Text);
        Assert.Equal($"[{action.Tag}] Off tread state OnBack is invalid for TurnInPlace",input.Logs[2].Text);
        Assert.Equal("remove:9:99",input.Eyes.Last());Assert.Equal((byte)0,action.Byte0xD9);
    }

    [Fact]
    public void FrameChangeSamplesThePredicateBeforeUpdatingTheSavedFrame()
    {
        var input=new Input();using var action=new TurnInPlaceAction(input,1,false);action.Init();action.HandleMotorActionAck(7);
        input.OriginId=2;input.PoseAngleAroundZ=0.25f;
        action.CheckIfDone();
        Assert.Equal($"[{action.Tag}] pose frame ID changed (old=1, new=2). No longer comparing angles to check if done - using angular distance traversed instead. (relocalizedCnt=1) (inPositionNow=0)",Assert.Single(input.Logs.Where(e=>e.Key.EndsWith("PfidChanged"))).Text);
        Assert.Equal(1u,action.OriginChanges0xD4);Assert.Equal(0f,action.Float0xA8);
    }

    [Fact]
    public void UnorderedRemainingAngleFailsAndTheEyeThresholdUsesStrictGreater()
    {
        var input=new Input();using var action=new TurnInPlaceAction(input,0,false);action.Init();
        Set(action,"Float0xA4",float.NaN);Assert.False(action.IsBodyInPosition());
        Set(action,"Float0xA4",BitConverter.Int32BitsToSingle(0x40490FDB));Assert.False(action.IsBodyInPosition());
        Set(action,"Float0xA4",MathF.BitDecrement(BitConverter.Int32BitsToSingle(0x40490FDB)));Assert.True(action.IsBodyInPosition());
        var turning=new Input();using var turn=new TurnInPlaceAction(turning,1,false);turn.Init();turn.HandleMotorActionAck(7);
        typeof(TurnInPlaceAction).GetProperty("Byte0xD9")!.SetValue(turn,(byte)9);
        turning.PoseAngleAroundZ=0.5f;Assert.Equal(PanTiltResult.Running,turn.CheckIfDone());Assert.Equal((byte)9,turn.Byte0xD9);
        turning.PoseAngleAroundZ=MathF.BitIncrement(0.5f);turn.CheckIfDone();Assert.Equal((byte)0,turn.Byte0xD9);
        var nan=new Input();using var nanTurn=new TurnInPlaceAction(nan,float.NaN,false);nanTurn.Init();nanTurn.HandleMotorActionAck(7);typeof(TurnInPlaceAction).GetProperty("Byte0xD9")!.SetValue(nanTurn,(byte)9);nanTurn.CheckIfDone();
        Assert.True(float.IsNaN(nanTurn.Float0xAC));Assert.Equal((byte)9,nanTurn.Byte0xD9);
    }

    [Fact]
    // reach: unit (supplies frame5 directly; actual dispatch/admission, eye layer and motor ack).
    public async Task LiveEyeLayerAndMotorAckUseTheSameGate()
    {
        using var rig=new M4ControlTests.Rig();rig.ToSynced();
        using var vision=new VisionSystem(rig.Robot,CameraCalibration.Nominal()){Enabled=false};using var m=new ManipulationSystem(rig.Robot,vision);
        var service=new MountChargerAction.LivePanTiltRobot(m);
        typeof(EngineRobot).GetProperty(nameof(EngineRobot.PoseFrameId))!.SetValue(rig.Engine.Robot,5u);
        Assert.Equal(5u,service.OriginId);Assert.NotEqual(service.OriginId,rig.Engine.Robot!.CurrentOriginId);
        var state=rig.MakeState(frame:5);state.Pose.Angle=0.25f;rig.Data(state);rig.Tick();
        Assert.InRange(service.PoseAngleAroundZ,0.249999f,0.250001f); // Actual history admission now updates the native MainPose.
        Assert.InRange((float)rig.Engine.Robot.MainPose.AngleAroundZ,0.249999f,0.250001f);
        using var turn=new TurnInPlaceAction(service,1,false);
        Assert.Equal(0u,turn.Init());Assert.NotEqual((byte)0,turn.Byte0xD9);
        rig.Robot.Animations.WithTrackLayers(layers=>
        {
            var layer=Assert.Single(layers.Face.AllLayers.Where(l=>l.Name=="TurnInPlaceEyeDart"));
            Assert.True(layer.IsPersistent);Assert.Equal(2,layer.Track.Count);Assert.Equal(new uint[]{0,132},layer.Track.Frames.Select(f=>f.Trigger));
        });
        Task ack;using var attempted=new ManualResetEventSlim();
        lock(rig.Robot.Animations.NativeLayerGate)
        {
            ack=Task.Run(()=>{attempted.Set();rig.Engine.Broadcast(new MotorActionAck{ActionId=turn.ActionId0xDA});});
            Assert.True(attempted.Wait(TimeSpan.FromSeconds(5)));Assert.False(ack.Wait(TimeSpan.FromMilliseconds(50)));Assert.False(turn.Byte0xDC);
        }
        await ack.WaitAsync(TimeSpan.FromSeconds(5));Assert.True(turn.Byte0xDC);
        turn.Dispose();
        Assert.Equal((byte)0,turn.Byte0xD9);
        rig.Robot.Animations.WithTrackLayers(layers=>Assert.DoesNotContain(layers.Face.AllLayers,l=>l.Name=="TurnInPlaceEyeDart"));
    }

    [Fact]
    public void EyeDisabledReinitKeepsThePriorThreshold()
    {
        var input=new Input();using var action=new TurnInPlaceAction(input,1,false);action.Init();Assert.Equal(0.5f,action.Float0xAC);
        input.Eyes.Clear();action.Byte0xD8=false;action.Init();Assert.Equal(0.5f,action.Float0xAC);Assert.Empty(input.Eyes);
    }

    [Fact]
    public void ParentlessNativePoseIsCopiedReparentedAndKeepsLocalPan()
    {
        using var root=new Pose3d("origin"); // This helper test needs parent identity, not a registered nonzero origin ID.
        using var robot=new Pose3d(Rotation3d.Identity,new Vec3(100,0,0),parent:root);
        using var target=new Pose3d(Rotation3d.Identity,new Vec3(0,100,0));
        var logs=new List<Entry>();SourceLogSink sink=(l,c,k,t)=>logs.Add(new(l,c,k,t));
        var action=new TurnTowardsPoseCompound(target,Math.PI);
        Pose3d? headInput=null;
        var env=new TurnTowardsPoseEnv(robot,0,p=>{headInput=p;return 0;},null,SourceLog:sink,WorldOrigin:root);
        Assert.Equal(0u,action.InitPose(env,out var go));Assert.True(go);Assert.Equal(MathF.PI/2,(float)action.PanAngleRad);
        Assert.False(target.HasParent);Assert.Same(root.Node,action.Pose!.Node!.Parent);Assert.Same(action.Pose,headInput);
        var log=Assert.Single(logs);Assert.Equal((EngineLogLevel.Info,"Actions","TurnTowardsPoseAction.Init.AssumingRobotOriginAsParent",""),(log.Level,log.Channel,log.Key,log.Text));
        bool prior=EngineErrorState.ErrorFlagSet;
        try
        {
            EngineErrorState.ErrorFlagSet=false;logs.Clear();var unset=new TurnTowardsPoseCompound(Math.PI);
            Assert.Equal(0x03000005u,unset.InitPose(env,out go));Assert.False(go);Assert.True(EngineErrorState.ErrorFlagSet);
            Assert.Equal((EngineLogLevel.Error,(string?)null,"TurnTowardsPoseAction.Init.PoseNotSet",""),(Assert.Single(logs).Level,logs[0].Channel,logs[0].Key,logs[0].Text));
        }
        finally{EngineErrorState.ErrorFlagSet=prior;}
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void FourItemSortUsesStableFiniteTiesAndTheLiteralNaNBranchTree(int variant)
    {
        var keys=variant switch{0=>new[]{2f,1f,1f,2f},1=>new[]{float.NaN,-1f,-2f,3f},_=>new[]{2f,float.NaN,1f,0f}};
        var expected=variant switch{0=>new[]{1,2,0,3},1=>new[]{0,2,1,3},_=>new[]{0,1,3,2}};
        Assert.Equal(expected,ObjectFootprint.SortFourAngles(keys));
    }
    [Fact]
    public void QuadStorageKeepsTheSourceSlotOrderAndFloatWords()
    {
        float nan=BitConverter.Int32BitsToSingle(0x7FC12345),minusZero=BitConverter.Int32BitsToSingle(unchecked((int)0x80000000));
        var points=new[]{new Point2f(nan,minusZero),new Point2f(1,nan),new Point2f(2,nan),new Point2f(3,nan)};
        var quad=ObjectFootprint.SortQuadrilateral(points);
        Assert.Equal(0x7FC12345,BitConverter.SingleToInt32Bits(quad[0].X));Assert.Equal(unchecked((int)0x80000000),BitConverter.SingleToInt32Bits(quad[0].Y));
        Assert.Equal(3,quad[1].X);Assert.Equal(1,quad[2].X);Assert.Equal(2,quad[3].X);
    }
    [Fact]
    public void RectangleFallbackPreservesOrderedBoundsAndItsEmptyResult()
    {
        Assert.Equal(new MinAreaRectF.RotatedRect(0,0,0,0,0),MinAreaRectF.RectangleFallback(Array.Empty<Point2f>()));
        var rect=MinAreaRectF.RectangleFallback(new[]{new Point2f(2,0),new Point2f(float.NaN,5),new Point2f(-1,-4)});
        Assert.Equal(new MinAreaRectF.RotatedRect(0.5f,0.5f,3,9,0),rect);
        var firstNan=MinAreaRectF.RectangleFallback(new[]{new Point2f(float.NaN,0),new Point2f(1,1)});Assert.True(float.IsNaN(firstNan.Cx));Assert.True(float.IsNaN(firstNan.W));
    }
    [Fact]
    public void ActionDestructorsDoNotAbortAPathAfterAbortAllMadeItInactive()
    {
        using var rig=new M4ControlTests.Rig();rig.ToSynced();
        using var vision=new VisionSystem(rig.Robot,CameraCalibration.Nominal()){Enabled=false};using var m=new ManipulationSystem(rig.Robot,vision);
        var drive=new DriveToPoseAction(m);var straight=new DriveStraightAction(m,10,20,false);
        rig.Robot.Motion.Path.DriveToPoseStatus=3;
        rig.Robot.Motion.Path.Abort();Assert.False(rig.Robot.Motion.Path.IsActive);
        int mark=rig.Mark();drive.Dispose();straight.Dispose();
        Assert.DoesNotContain(rig.SentSince(mark),message=>message is ClearPath);
    }

    [Fact]
    public void LatticeDestructionJoinsThenReleasesSubscriptionsBeforeTheWaitHandle()
    {
        using var rig=new Rig();
        MotionPrimitiveSet? primitives=null;
        for(var dir=new DirectoryInfo(AppContext.BaseDirectory);dir is not null && primitives is null;dir=dir.Parent)
            primitives=MotionPrimitiveSet.FromObb(Path.Combine(dir.FullName,"re-analysis/obb"));
        Assert.NotNull(primitives);
        var order=new List<string>();
        using var runtime=new NativeLatticePlannerRuntime(new(new LatticeEnvironment(primitives)),rig.Vision.World,()=>null,
            (_,_,key,_)=>order.Add(key),_=>{},rig.Robot.Engine);
        bool closedAtUnlink=true;
        rig.Robot.Engine.AnimationMessages.ReleaseObserved=line=>
        {
            if(line!="unlink G2E 7B")return;
            order.Add("unlink");
            closedAtUnlink=((AutoResetEvent)typeof(NativeLatticePlannerRuntime).GetField("_wake",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(runtime)!).SafeWaitHandle.IsClosed;
        };
        runtime.Dispose();
        Assert.Equal(new[]{"LatticePlanner.DestroyThread.Join","LatticePlanner.DestroyThread.Joined","unlink"},order);
        Assert.False(closedAtUnlink);Assert.True(runtime.F1);Assert.True(runtime.F2);
        // W1: if exit is already set at worker entry, pending work is not consumed.
        runtime.F0=true;
        typeof(NativeLatticePlannerRuntime).GetMethod("Worker",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(runtime,null);
        Assert.True(runtime.F0);Assert.False(runtime.F3);
    }

    [Fact]
    public void RobotTeardownStillAttemptsExactlyTwoClearPaths()
    {
        using var rig=new M4ControlTests.Rig();rig.ToSynced();var sent=new List<RobotMessage>();
        rig.Engine.Robot!.SendFault=message=>{sent.Add(message);return true;};rig.Engine.Dispose();
        Assert.Equal(2,sent.OfType<ClearPath>().Count());
    }
}
