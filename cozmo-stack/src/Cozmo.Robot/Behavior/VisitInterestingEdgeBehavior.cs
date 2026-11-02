using System.Text.Json;
using Cozmo.Protocol;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
namespace Cozmo.Robot.Behavior;

// fidelity: M11-089, M11-092, M8-013
/// <summary>
/// BehaviorVisitInterestingEdge, constructor 0x005E5F64, vtable 0x01029AD4 (+0x48 Init, +0x4C common Resume 0x005BDA94,
/// +0x50 IsRunnable, +0x54 Stop). State word +0x190: 1 moving to the vantage, 2 gathering, 3 observing close, 4 end animations.
/// </summary>
public sealed class VisitInterestingEdgeBehavior : SteppedBehavior, IDisposable
{
    private static readonly float Epsilon=BitConverter.UInt32BitsToSingle(0x3727C5AC);
    private static readonly float Milli=BitConverter.UInt32BitsToSingle(0x3A83126F);
    private static readonly float RoiDistance=BitConverter.UInt32BitsToSingle(0x42200000);     // GroundPlaneROI::_dist 0x00C48F60, via 0x0103F408
    private static readonly float NegativeFront=BitConverter.UInt32BitsToSingle(0xC2006666);   // -32.1f, 0x005E7D78..
    private const int NoTrigger=0x23F; // empty trigger name, 0x005E5FE8..0x005E6232
    private const byte CategorySuccess=0,CategoryAbort=3,CategoryRetry=4; // RobotActionEnums, result word high byte

    private sealed record LifecycleConfig(AnimationTrigger Observe,AnimationTrigger NotFound,AnimationTrigger SquintStart,
        AnimationTrigger SquintLoop,AnimationTrigger SquintEnd,float ConeNear,float ConeFarPlane,float ConeFar,
        float Accuracy,float Observation,float ApproachSpeed)
    {
        private static AnimationTrigger Trigger(JsonElement json,string key)
        {
            string name=json.GetProperty(key).GetString()??"";
            return name.Length==0?(AnimationTrigger)NoTrigger:Enum.Parse<AnimationTrigger>(name);
        }
        // 0x005E5FE8..0x005E6342: the five triggers and the fields InterestingEdgeConfig does not carry.
        internal static LifecycleConfig Read(JsonElement p)=>new(
            Trigger(p,"observeEdgeAnimTrigger"),Trigger(p,"edgesNotFoundAnimTrigger"),Trigger(p,"squintStartAnimTrigger"),
            Trigger(p,"squintLoopAnimTrigger"),Trigger(p,"squintEndAnimTrigger"),
            p.GetProperty("forwardConeHalfWidthAtRobot_mm").GetSingle(),p.GetProperty("forwardConeFarPlaneDistFromRobot_mm").GetSingle(),
            p.GetProperty("forwardConeHalfWidthAtFarPlane_mm").GetSingle(),p.GetProperty("accuracyDistanceFromBorder_mm").GetSingle(),
            p.GetProperty("observationDistanceFromBorder_mm").GetSingle(),p.GetProperty("borderApproachSpeed_mmps").GetSingle());
    }
    // Map slots +0x24 and +0x30 of the current map, the ones PickGoals and the vantage rays call.
    private sealed class LiveMap(MemoryMap map):IInterestingEdgeMap
    {
        public float Scalar24()=>(float)MemoryMap.ContentPrecisionMm;
        public uint Ray30(Point2f first,Point2f second,IReadOnlyList<bool> contentTypes)=>
            map.HasCollisionRayWithTypes(new(first.X,first.Y),new(second.X,second.Y),type=>contentTypes[(int)type])?1u:0u;
    }

    public InterestingEdgeGoalSelector Goals {get;}=new();
    public Func<BehaviorContext,InterestingEdgeInputs?>? NativeInputs {get;set;}
    private EngineRobot? _originalRobot;
    private ManipulationSystem? _m;
    private LifecycleConfig? _life;
    private long _generation;
    private int _retired;
    private uint _waitTag,_squintTag;     // +0x174, +0x180
    private RetainedSubscription? _waiting; // +0x178/+0x17C
    private double _startArea;            // +0x188, written only by StartWaitingForEdges
    internal int NativeState {get;private set;} // +0x190
    internal uint WaitTagForTest=>_waitTag;
    internal uint SquintTagForTest=>_squintTag;

    public VisitInterestingEdgeBehavior(string id,InterestingEdgeConfig config):base(id,"VisitInterestingEdge")
    {RequiredProcessId=1;Goals.Config=config;}
    public static VisitInterestingEdgeBehavior FromConfig(JsonElement json,EngineRobot? originalRobot=null,ManipulationSystem? manipulation=null)
    {
        var parameters=json.GetProperty("params");
        var result=new VisitInterestingEdgeBehavior(BehaviorConfigLoader.ExtractIdFromConfig(json).ToString(),InterestingEdgeConfig.Read(parameters));
        result._originalRobot=originalRobot;result._m=manipulation;result._life=LifecycleConfig.Read(parameters);
        return result;
    }

    protected override IDisposable? EnterHostExecutionOwner(BehaviorContext? context)=>_originalRobot?.Engine.EnterSynchronousOwner();
    protected override void HostRunStarting()=>Interlocked.Increment(ref _generation);
    protected override long CaptureHostExecutionVersion()=>Interlocked.Read(ref _generation);
    protected override bool HostExecutionVersionCurrent(long value)=>Volatile.Read(ref _retired)==0 && Interlocked.Read(ref _generation)==value &&
        (_originalRobot is not {} robot || ReferenceEquals(robot.Engine.Robot,robot));
    protected override bool HostStopContinuationCurrent(long value)=>Volatile.Read(ref _retired)==0 && Interlocked.Read(ref _generation)==unchecked(value+1);
    protected override bool HostResumeContinuationCurrent(long value)=>Volatile.Read(ref _retired)==0 && (Interlocked.Read(ref _generation)==value || Interlocked.Read(ref _generation)==unchecked(value+1));
    protected override bool KeepsRunningWithoutAction=>HasCurrentAction || NativeState==2; // 0x005E7418: state 2 returns running itself
    protected override bool RunnableGate20(BehaviorContext context)=>false; // vtable stub 0x005BF04C
    protected override bool RunnableGate24(BehaviorContext context)=>false; // vtable stub 0x0059EC12
    protected override bool RunnableGate28(BehaviorContext context)=>false; // vtable stub 0x005E834A

    // 0x005E6520..0x005E6824. Mutates its own vantage state and draws RNG on every evaluation, even when it returns false.
    protected override bool IsRunnableInternal(BehaviorContext context)
    {
        if(NativeInputs is null && _originalRobot is {} robot)
        {
            var runtime=robot.NativeBehaviorRuntime;
            if(_m is not {} m || !ReferenceEquals(robot.Engine.Robot,robot) || runtime is null || !ReferenceEquals(runtime.Context,context) ||
               !runtime.Bound.TryGetValue(Id,out var configured) || !ReferenceEquals(configured,this) ||
               !ReferenceEquals(context.AI,runtime.AI) || runtime.AI.Analyzer.ActualBorderResultEmpty() is null)
            {MarkReadinessUnavailable();ReportMissing("M11-079/M11-089 original analyzer border result unavailable");return false;}
            using var mutation=m.World.EnterPoseMutation();
            if(!robot.TryGetNativeOriginOwner(robot.CurrentOriginId,out var origin) || origin?.Node is null)
            {MarkReadinessUnavailable();ReportMissing("M11-089 original Robot world origin unavailable at 0x005E6C94");return false;}
            var map=m.World.Maps.Current;
            // A null result is a logged M1-034 smallest-unit skip (0x005E68A6, 0x005E6C58, 0x005E6EC0), not missing input.
            // EraseDebugSegments: VizManager drawing, which reaches neither the robot nor the API.
            return Goals.IsRunnable(new InterestingEdgeInputs{RobotPose=robot.GetCheckedMainPose(),WorldOrigin=origin,Regions=runtime.AI.Analyzer.Regions,
                Map=map is null?null:new LiveMap(map),GetRng=()=>robot.Engine.ContextRandom,EraseDebugSegments=_=>{}})==true;
        }
        var input=NativeInputs?.Invoke(context);
        if(input is null)
        {MarkReadinessUnavailable();ReportMissing("M11-089 live native border-vector/map/RNG/robot-root input join at 0x005E6520 -> 0x005E682C");return false;}
        var result=Goals.IsRunnable(input);
        if(result is null)
        {MarkReadinessUnavailable();ReportMissing("M11-089 VisitInterestingEdge goal readiness input unavailable");return false;}
        return result.Value;
    }

    private void Info(string key,string text)=>_originalRobot!.Engine.EmitSourceLog(EngineLogLevel.Info,"Behaviors",Id+key,text);
    private float Now=>_originalRobot!.Engine.Timer.SecondsF;
    private TriggerLiftSafeAnimationAction Trigger(AnimationTrigger trigger,int loops=1)=>
        new(_m!.Robot,new(trigger,loops,true,0,60f,false)); // loops, interrupt, tracks 0, 60f, false: every trigger site here

    // InitInternal 0x005E7018..0x005E7040.
    protected override void OnStart()
    {
        if(_m is null || _originalRobot?.NativeBehaviorRuntime is not {} runtime || _life is null)
        {
            ReportMissing("M11-092 VisitInterestingEdge InitInternal needs its original Robot, manipulation and configuration owners");
            throw new NotSupportedException("M11-092 VisitInterestingEdge has no original owner on this standalone instance");
        }
        runtime.AI.Analyzer.AddDisableRequest(1,Id);
        NativeState=0;
        MoveToVantagePoint(0,CaptureHostExecutionVersion());
    }

    // S1 0x005E7044..0x005E71DE. The drive arguments (10,10,10), 0x3E32B8C2, 4.0 and 1.0 are DriveToPoseAction's own defaults.
    private void MoveToVantagePoint(byte attempt,long g)
    {
        if(!HostExecutionVersionCurrent(g))return;
        NativeState=1;
        if(!SetStateName("ToS1_MoveToVantagePoint",()=>HostExecutionVersionCurrent(g)))return;
        Info(".S1","Moving to vantage point");
        var sequence=new CompoundActionSequential(()=>Now);
        var drive=new DriveToPoseAction(_m!,true,false);
        drive.SetNativeGoals(Goals.Vantage168);
        sequence.AddAction(drive,false,false);
        sequence.AddAction(LowLiftAndLowHead(),false,false);
        if(!HostExecutionVersionCurrent(g)){sequence.RetireNeverAdmittedTree();return;}
        StartActing(sequence,result=>AfterMove(result,attempt,g),()=>HostExecutionVersionCurrent(g));
    }
    // 0x005E79FC..0x005E7AD6. Lift preset 0 is LowDock 32 mm, tolerance 5.0f. Head preset 0 is GetPresetHeadAngle 0x00548440,
    // 0xBE860A92, tolerance 0x3D0EFA35, variability 0, at the action's default speed and acceleration.
    private ActionRunner LowLiftAndLowHead()
    {
        var parallel=new CompoundActionParallel(()=>Now);
        var motion=_m!.Robot.Motion;
        parallel.AddAction(motion.CreateNativeLiftChild(LiftPresets.LowDockMm,5f),false,false);
        parallel.AddAction(motion.CreateNativeHeadChild(BitConverter.UInt32BitsToSingle(0xBE860A92),BitConverter.UInt32BitsToSingle(0x3D0EFA35),
            CozmoMotion.ActionDefaultHeadSpeedRadPerSec,CozmoMotion.ActionDefaultHeadAccelRadPerSec2),false,false);
        parallel.AddAction(Trigger(_life!.SquintStart),false,false);
        return parallel;
    }
    // Callback 0x005E8E74..0x005E8F4A: `cmp attempt,#2; bhi` bails, so attempts 0, 1 and 2 retry.
    private void AfterMove(RobotCompletedAction completed,byte attempt,long g)
    {
        if(!HostExecutionVersionCurrent(g))return;
        switch((byte)(completed.Result>>24))
        {
            case CategorySuccess:GatherBorderPrecision(g);break;
            case CategoryRetry:
                if(attempt<=2)
                {
                    Info(".S1.Retry",$"Trying again ({attempt})");
                    MoveToVantagePoint(unchecked((byte)(attempt+1)),g);
                }
                else Info(".S1.Bail","Could not reach the vantage point; not retrying");
                break;
            case CategoryAbort:Info(".S1.Unhandled",$"Unhandled result 0x{completed.Result:X8}");break;
        }
    }
    // S2 0x005E7BFC..0x005E7CDA: queued directly on the ActionList, not the common acting handle.
    private void GatherBorderPrecision(long g)
    {
        NativeState=2;
        if(!SetStateName("S2_GatherBorderPrecision",()=>HostExecutionVersionCurrent(g)))return;
        Info(".S2","At vantage point, trying to grab more accurate borders");
        var loop=Trigger(_life!.SquintLoop,0);
        _=_originalRobot!.ActionList.QueueAction(QueueActionPosition.InParallel,loop,0);
        if(loop.DestructionObserved)
            StackDiagnostics.Write("M1-034 M11-092 skip freed squint action tag read at 0x005E7CCA; no squint tag stored");
        else _squintTag=loop.Tag;
        StartWaitingForEdges(g);
    }
    // 0x005E7AF4..0x005E7BC2: mutation, then the area read, then the wait, then the 0x5A subscription.
    private void StartWaitingForEdges(long g)
    {
        if(!HostExecutionVersionCurrent(g))return;
        var robot=_originalRobot!;var m=_m!;
        using(var mutation=m.World.EnterPoseMutation())m.World.Maps.FlagGroundPlaneRoiInterestingEdgesAsUncertain(robot.GetCheckedMainPose());
        if(m.World.Maps.Current is {} map)_startArea=map.InterestingEdgeAreaM2;
        else StackDiagnostics.Write("M1-034 M11-092 skip null current map area read at 0x005E7B16; stored area retained");
        var wait=new NativeWaitForImagesRunner(m,5,VisionMode.DetectingOverheadEdges,0);
        if(robot.ActionList.QueueAction(QueueActionPosition.InParallel,wait,0)!=0)return; // 0x005E7B46: no tag, no subscription
        _waitTag=wait.Tag;
        var previous=_waiting;
        _waiting=m.Robot.Engine.AnimationMessages.SubscribeMessage(RobotCompletedAction.UnionTag,true,
            payload=>{if(payload is RobotCompletedAction done && done.Tag==_waitTag)_waitTag=0;}); // 0x005E8B72: no result check
        previous?.Dispose();
    }
    // Update 0x005E7418..0x005E7492: only state 2 does its own work.
    protected override void OnUpdate()
    {
        if(NativeState==2)GatheringAccurateEdge(CaptureHostExecutionVersion());
    }
    // 0x005E7514..0x005E78CC.
    private void GatheringAccurateEdge(long g)
    {
        if(_waitTag!=0)return;
        ReleaseWaiting();_waitTag=0;
        var m=_m!;var life=_life!;
        float distance=m.Whiteboard.OverheadEdgeDistance;
        if(float.IsNaN(distance))
        {
            Info(".GatheringAccurateEdge.Done","Processed edges and did not find any.");
            StopActing(true,false);
            if(!HostExecutionVersionCurrent(g))return;
            var sequence=new CompoundActionSequential(()=>Now);
            sequence.AddAction(Trigger(life.SquintEnd),false,false);
            sequence.AddAction(Trigger(life.NotFound),false,false);
            StopSquintLoop();
            if(!HostExecutionVersionCurrent(g)){sequence.RetireNeverAdmittedTree();return;}
            StartActing(sequence,null,()=>HostExecutionVersionCurrent(g));
            NativeState=4;
            return;
        }
        ReportSmallRegion(m);
        float limit=(RoiDistance+life.Accuracy)+Epsilon; // 0x005E775A..0x005E777C
        if(limit>=distance) // ordered; the far branch takes the unordered case
        {
            Info(".GatheringAccurateEdge.Close","Got a close edge, observe from here");
            ObserveFromClose(g);
            return;
        }
        Info(".GatheringAccurateEdge.Far","Got a far edge, continuing forward fetch");
        if(!HasCurrentAction)StartActing(new DriveStraightAction(m,_originalRobot!,200f,life.ApproachSpeed,false),null,()=>HostExecutionVersionCurrent(g));
        StartWaitingForEdges(g);
    }
    // 0x005E7696..0x005E7756: a log only. It neither rejects nor retries, and the unordered case logs too.
    private void ReportSmallRegion(ManipulationSystem m)
    {
        if(m.World.Maps.Current is not {} map)
        {StackDiagnostics.Write("M1-034 M11-092 skip null current map area read at 0x005E76A0");return;}
        double area=map.InterestingEdgeAreaM2,delta=area-_startArea;
        float side=(float)MemoryMap.ContentPrecisionMm*Milli;
        float square=side*side;
        double required=square*4f;
        if(!(delta>=required))
            Info(".GatheringAccurateEdge.RegionTooSmall",FormattableString.Invariant($"Detected edges, but the region is too small (changed from {_startArea:F8} to {area:F8} = {delta:F8}, required {required:F8} at least). Is this a reflection or noise?"));
    }
    // S3 0x005E7D78..0x005E7F6C: the visited cone goes in before the approach drive has run.
    private void ObserveFromClose(long g)
    {
        NativeState=3;
        if(!SetStateName("S3_ObserveFromClose",()=>HostExecutionVersionCurrent(g)))return;
        var m=_m!;var life=_life!;
        float closer=m.Whiteboard.OverheadEdgeDistance+NegativeFront;
        closer=closer-life.Observation;
        Info(".S3",FormattableString.Invariant($"Observing edges from close distance (moving closer {closer:F2}mm)"));
        FlagVisitedQuad();
        var sequence=new CompoundActionSequential(()=>Now);
        if(closer>0f)sequence.AddAction(new DriveStraightAction(m,_originalRobot!,closer,life.ApproachSpeed,false),false,false); // BLE skips zero and unordered
        sequence.AddAction(Trigger(life.SquintEnd),false,false);
        sequence.AddAction(Trigger(life.Observe),false,false);
        StopActing(true,false);
        if(!HostExecutionVersionCurrent(g)){sequence.RetireNeverAdmittedTree();return;}
        StopSquintLoop();
        if(!HostExecutionVersionCurrent(g)){sequence.RetireNeverAdmittedTree();return;}
        StartActing(sequence,null,()=>HostExecutionVersionCurrent(g));
    }
    // FlagVisitedQuad 0x005E8038..0x005E8104: robot pose wrt root; quad order far+, near+, far-, near-.
    private void FlagVisitedQuad()
    {
        var m=_m!;var life=_life!;
        using var mutation=m.World.EnterPoseMutation();
        using var root=_originalRobot!.GetCheckedMainPose().GetWithRespectToRoot();
        Vec2 Local(float x,float y){var world=root.Apply(new Vec3(x,y,0f));return new((float)world.X,(float)world.Y);}
        m.World.Maps.FlagQuadAsNotInterestingEdges(new[]
            {Local(life.ConeFarPlane,life.ConeFar),Local(0f,life.ConeNear),Local(life.ConeFarPlane,-life.ConeFar),Local(0f,-life.ConeNear)});
    }
    // 0x005E7320..0x005E7378: the tag clears after Cancel returns.
    private void StopSquintLoop()
    {
        if(_squintTag==0)return;
        bool cancelled=_originalRobot!.ActionList.Cancel(_squintTag);
        _squintTag=0;
        if(!cancelled)_originalRobot.Engine.EmitSourceLog(EngineLogLevel.Warning,"Behaviors","BehaviorVisitInterestingEdge.StopSquintLoop.SquintLoopNotFound","Could not cancel squint. Was it interrupted?");
    }
    private void ReleaseWaiting(){var old=_waiting;_waiting=null;old?.Dispose();} // zero the pair before releasing the old control
    // StopInternal 0x005E729C..0x005E7302. Nothing cancels the wait, clears the vantage poses or resets the state here.
    protected override void OnStop(BehaviorStopReason reason)
    {
        if(_m is not null)
        {
            _originalRobot?.NativeBehaviorRuntime?.AI.Analyzer.RemoveDisableRequest(1,Id);
            StopSquintLoop();
            ReleaseWaiting();_waitTag=0;
        }
        Interlocked.Increment(ref _generation);
    }
    public void Dispose()
    {
        if(Interlocked.Exchange(ref _retired,1)!=0)return;
        Interlocked.Increment(ref _generation);
        ReleaseWaiting();
        Goals.ClearVantage();
        RetireActionCompletionOwner();
    }
}
