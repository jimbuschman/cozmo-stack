using System.Text.Json;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Behavior;

// Configured owner: native action phases, face handler and state-6 retained parent call.
internal sealed class NativeLookForFaceAndCubeBehavior : SteppedBehavior, IDisposable
{
    private readonly ManipulationSystem _m;
    private readonly EngineRobot _robot;
    private readonly float _bodySpeed,_headSpeed,_faceHeadMin,_faceHeadMax,_faceBodyMin,_faceBodyMax,_cubeHeadMin,_cubeHeadMax,_cubeBodyMin,_cubeBodyMax;
    private readonly byte _facePicks,_cubePicks;
    private readonly bool _verify,_stopAny,_stopNamed;
    private readonly bool _validParameters;
    private readonly SortedSet<int> _verified=new();
    private byte _counter;
    private int _state=6;
    private bool _verifying;
    private float _yaw;
    private long _generation;
    private int _retired;
    internal int NativeState=>_state;
    internal byte NativeCounter=>_counter;
    internal bool Verifying=>_verifying;
    internal bool ParametersAvailable=>_validParameters;
    internal IReadOnlyCollection<int> Verified=>_verified;
    internal bool ExecutionAvailable => _validParameters && Volatile.Read(ref _retired)==0 && ReferenceEquals(_robot.Engine.Robot,_robot) &&
        _robot.NativeBehaviorRuntime is {} runtime && runtime.LookInputsAvailable(this);
    internal JsonElement RootConfig { get; }
    internal bool ObjectiveInputsAvailable {get;private set;}=true;
    private string _unsupportedObjectiveInput="";
    internal bool HasCheckedObjective41=>ObjectiveInputsAvailable&&BehaviorObjective==BehaviorObjective.Invalid;
    internal void CheckObjectiveInput(JsonElement scoring,string location)
    {
        if(scoring.ValueKind!=JsonValueKind.Object || !scoring.TryGetProperty("considerThisHasRunForBehaviorObjective",out var value) || value.ValueKind==JsonValueKind.Null)return; //005BC574..57A isNull preserves41.
        ObjectiveInputsAvailable=false;_unsupportedObjectiveInput=location;
        ReportMissing("LookForFaceAndCube changed-config objective input at "+location+"="+value.GetRawText()+
            "; source string mapping/stopstamp mutation unavailable, shipped absent-key invalid41 proof cannot be used");
    }
    private RetainedSubscription? _inactiveFaces,_objective;
    internal NativeLookForFaceAndCubeBehavior(BehaviorFactoryContext source, JsonElement root)
        : base(BehaviorConfigLoader.ExtractIdFromConfig(root).ToString(), "LookForFaceAndCube")
    {
        _m=source.Manipulation!;_robot=source.NativeRobot!;
        RootConfig=root.Clone();
        try
        {
        var p=root.GetProperty("params");
        float Scale(string key)=>new Radians(p.GetProperty(key).GetSingle()*BitConverter.UInt32BitsToSingle(0x3C8EFA35)).Value;
        _bodySpeed=Scale("bodyTurnSpeed_degPerSec");_headSpeed=Scale("headTurnSpeed_degPerSec");
        _faceHeadMin=Scale("face_headAngleAbsRangeMin_deg");_faceHeadMax=Scale("face_headAngleAbsRangeMax_deg");
        _faceBodyMin=Scale("face_bodyAngleRelRangeMin_deg");_faceBodyMax=Scale("face_bodyAngleRelRangeMax_deg");
        _cubeHeadMin=Scale("cube_headAngleAbsRangeMin_rad");_cubeHeadMax=Scale("cube_headAngleAbsRangeMax_rad");
        _cubeBodyMin=Scale("cube_bodyAngleRelRangeMin_rad");_cubeBodyMax=Scale("cube_bodyAngleRelRangeMax_rad");
        _facePicks=unchecked((byte)p.GetProperty("face_sidePicks").GetInt32());_cubePicks=unchecked((byte)p.GetProperty("cube_sidePicks").GetInt32());
        _verify=p.GetProperty("verifySeenFaces").GetBoolean();_stopAny=p.GetProperty("stopBehaviorOnAnyFace").GetBoolean();_stopNamed=p.GetProperty("stopBehaviorOnNamedFace").GetBoolean();
        _validParameters=true;
        }
        catch(Exception error) when(error is KeyNotFoundException or InvalidOperationException or FormatException or OverflowException)
        { ReportMissing("LookForFaceAndCube non-shipped params unavailable: "+error.Message); }
        CheckObjectiveInput(root,"behavior root "+Id);
        // C7.1: IBehavior tail unconditionally calls ScoredConstructor. Both
        // shipped root and activity scoring omit objective, retaining invalid41;
        // actual5BF00C returns BEFORE payload/time access for that value.
        if(source.NativeRobot is {HasExternalInterface:true} && source.Manipulation is {} objectiveOwner)
            _objective=objectiveOwner.Robot.Engine.AnimationMessages.SubscribeMessage(0x8B,true,payload=>
            {
                if(!ObjectiveInputsAvailable)
                {
                    ReportMissing("LookForFaceAndCube objective callback unavailable for explicit raw input at "+_unsupportedObjectiveInput+"; no silent invalid41 filter substituted");
                    return;
                }
                if(BehaviorObjective==BehaviorObjective.Invalid)return; // FIRST guard005BF014/16.
                if(payload is not uint raw || raw!=(uint)BehaviorObjective)return;
                ReportMissing("LookForFaceAndCube changed-config matching objective stopstamp005BF028 producer unavailable; shipped invalid41 unit does not certify this mutation");
            });
        // Shipped verifySeenFaces=true makes the actual constructor subscribe46.
        bool subscribes=root.TryGetProperty("params",out var parameters) &&
            new[]{"verifySeenFaces","stopBehaviorOnAnyFace","stopBehaviorOnNamedFace"}.Any(key=>
                parameters.TryGetProperty(key,out var value) && value.ValueKind==JsonValueKind.True);
        if(subscribes && source.NativeRobot is {HasExternalInterface:true} && source.Manipulation is {} m)
            _inactiveFaces=m.Robot.Engine.AnimationMessages.SubscribeMessage(0x46,true,payload=>
            {
                using var owner=m.Robot.Engine.EnterSynchronousOwner();
                if(EngineRunning && payload is NativeObservedFace face) OnFace(face.Id);
            });
    }
    protected override IDisposable? EnterHostExecutionOwner(BehaviorContext? context)=>_m.Robot.Engine.EnterSynchronousOwner();
    protected override void HostRunStarting()=>Interlocked.Increment(ref _generation);
    protected override long CaptureHostExecutionVersion()=>Interlocked.Read(ref _generation);
    protected override bool HostExecutionVersionCurrent(long version)=>Volatile.Read(ref _retired)==0&&Interlocked.Read(ref _generation)==version;
    protected override bool HostStopContinuationCurrent(long version)=>Volatile.Read(ref _retired)==0&&Interlocked.Read(ref _generation)==unchecked(version+1);
    protected override bool HostResumeContinuationCurrent(long version)=>Volatile.Read(ref _retired)==0&&(Interlocked.Read(ref _generation)==version||Interlocked.Read(ref _generation)==unchecked(version+1));
    protected override bool KeepsRunningWithoutAction=>HasCurrentAction;
    public override bool IsRunnable(BehaviorContext context)
    {
        using var owner=EnterHostExecutionOwner(context);
        long generation=CaptureHostExecutionVersion();
        if(!HostExecutionVersionCurrent(generation) || !IsRunnableBase(context) || !HostExecutionVersionCurrent(generation))return false;
        if(!ExecutionAvailable){MarkReadinessUnavailable();ReportMissing("LookForFaceAndCube actual runtime/parent/pose or parameter input unavailable");return false;}
        return IsRunnableInternal(context);
    }
    protected override bool IsRunnableInternal(BehaviorContext context)=>true; //005EFD54
    protected override bool RunnableGate20(BehaviorContext context)=>false;
    protected override bool RunnableGate24(BehaviorContext context)=>false;
    protected override bool RunnableGate28(BehaviorContext context)=>false;
    protected override void OnStart()
    {
        if(!ExecutionAvailable) { MarkReadinessUnavailable();ReportMissing("LookForFaceAndCube actual runtime/retained parent input unavailable");InitFailed=true;return; }
        using var mutation=_m.World.EnterPoseMutation();
        using var flat=_robot.GetCheckedMainPose().GetWithRespectToRoot();_yaw=(float)flat.AngleAroundZ;
        _state=0;_counter=0;_verified.Clear();_verifying=false;
        long generation=CaptureHostExecutionVersion();
        var parallel=new CompoundActionParallel(()=>_robot.Engine.Timer.SecondsF);
        parallel.AddAction(_m.Robot.Motion.CreateNativeLiftChild(32f,5f),false,false);
        parallel.AddAction(Pan(0,0,_faceHeadMin,_faceHeadMax),false,false);
        StartActing(parallel,_=>Phase(1,generation),()=>HostExecutionVersionCurrent(generation));
    }
    private NativePanTiltRunner Pan(float min,float max,float headMin,float headMax)
    {
        var random=_robot.Engine.ContextRandom;
        float body=(float)random.RandDblInRange(min,max);float head=(float)random.RandDblInRange(headMin,headMax);
        var p=new PanAndTiltAction{PanAngleRad=new Radians(_yaw+body).Value,HeadAngleRad=new Radians(head).Value,PanIsAbsolute=true,HeadIsAbsolute=true};
        p.SetMaxPanSpeed(_bodySpeed);p.SetMaxTiltSpeed(_bodySpeed);
        return new NativePanTiltRunner(_m,p);
    }
    private void Phase(int phase,long generation)
    {
        if(!HostExecutionVersionCurrent(generation))return;
        _state=phase;_counter=unchecked((byte)(_counter+1));
        float min=phase switch {1=>_faceBodyMin,2=>-_faceBodyMin,3=>_cubeBodyMin,4=>-_cubeBodyMin,_=>0};
        float max=phase switch {1=>_faceBodyMax,2=>-_faceBodyMax,3=>_cubeBodyMax,4=>-_cubeBodyMax,_=>0};
        var action=Pan(min,max,phase<=2?_faceHeadMin:_cubeHeadMin,phase<=2?_faceHeadMax:_cubeHeadMax);
        int next=phase;byte limit=phase<=2?_facePicks:_cubePicks;
        if(phase==5){_counter=0;next=6;}
        else if(_counter>=limit){if(phase!=2)_counter=0;next=phase+1;}
        StartActing(action,_=>{if(next==6)FinishSearch(generation);else Phase(next,generation);},()=>HostExecutionVersionCurrent(generation));
    }
    private void FinishSearch(long generation)
    {
        if(!HostExecutionVersionCurrent(generation))return;
        _state=6;
        if(_robot.NativeBehaviorRuntime is not {} runtime || !runtime.CalculateLookDesired(this))
            ReportMissing("M15-008 Look state6 retained Freeplay/world input unavailable; prior desired cache retained");
    }
    private void ResumePhase(long generation)
    { if(_state is >=0 and <=4)Phase(_state+1,generation); }
    protected override int ResumeInternal()
    {
        _verifying=false;_counter=0;
        if(_state==6)return 1;
        if(!ExecutionAvailable){MarkReadinessUnavailable();ReportMissing("Look resume actual runtime/parent unavailable");return 1;}
        ResumePhase(CaptureHostExecutionVersion());return 0;
    }
    private void OnFace(int id)
    {
        long generation=CaptureHostExecutionVersion();
        if(!HostExecutionVersionCurrent(generation))return;
        if(!_verify){StopOnFace(id,generation);return;}
        if(_verifying || (id>=0 && _verified.Contains(id)))return;
        StopActing(false,false);
        if(!HostExecutionVersionCurrent(generation))return;
        var sequence=new CompoundActionSequential(()=>_robot.Engine.Timer.SecondsF);sequence.SeedNativeEmptyCompletion();
        sequence.AddAction(new NativeLastFaceRunner(_m,_robot,id),false,false);
        var face=id>=0?_m.Vision.Faces.GetFace(id):null;
        if(id<0 || (face is not null && !face.HasName))
            sequence.AddAction(new NativeWaitForImagesRunner(_m,id<0?1:3,(VisionMode)0x10,0),false,false);
        _verifying=true;
        StartActing(sequence,result=>
        {
            if(!HostExecutionVersionCurrent(generation))return;
            if(result.Result==0 && id>=0)_verified.Add(id);
            _verifying=false;StopOnFace(id,generation);
            if(HostExecutionVersionCurrent(generation))ResumePhase(generation);
        },()=>HostExecutionVersionCurrent(generation));
    }
    private void StopOnFace(int id,long generation)
    {
        if(id<0)return;
        bool stop=_stopAny || (_stopNamed && _m.Vision.Faces.GetFace(id) is {} face && face.HasName);
        if(!stop)return;
        StopActing(false,false);FinishSearch(generation);
    }
    protected override void OnStop(BehaviorStopReason reason)=>Interlocked.Increment(ref _generation); // native body BX LR; host callback retirement only.
    public void Dispose(){if(Interlocked.Exchange(ref _retired,1)!=0)return;Interlocked.Increment(ref _generation);_inactiveFaces?.Dispose();_inactiveFaces=null;_objective?.Dispose();_objective=null;RetireActionCompletionOwner();}
}
