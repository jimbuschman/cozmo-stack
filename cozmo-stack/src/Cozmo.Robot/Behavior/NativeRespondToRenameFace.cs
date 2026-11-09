using System.Text.Json;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Manipulation;

namespace Cozmo.Robot.Behavior;

// BehaviorRespondToRenameFace 0x600694..0x600A5C; request staging is independent of recognizer success.
internal sealed class NativeRespondToRenameFace : SteppedBehavior, IDisposable
{
    private readonly ManipulationSystem _m;
    private readonly EngineRobot _owner;
    private readonly AnimationTrigger _trigger;
    private readonly RetainedSubscription? _request;
    private string _name="";
    private int _id;
    private long _generation;
    private int _retired;
    internal string StagedName=>_name;
    internal int StagedId=>_id;

    internal NativeRespondToRenameFace(BehaviorFactoryContext source,JsonElement config)
        :base("RespondToRenameFace","RespondToRenameFace")
    {
        _m=source.Manipulation!;_owner=source.NativeRobot!;
        _trigger=AnimationTrigger.MeetCozmoRenameFaceSayName;
        if(config.TryGetProperty("animationTrigger",out var value) && value.ValueKind==JsonValueKind.String)
        {
            if(!Enum.TryParse<AnimationTrigger>(value.GetString(),false,out var trigger))
                throw new NotSupportedException("M14-006 RespondToRenameFace animation trigger unavailable");
            _trigger=trigger;
        }
        if(_owner.HasExternalInterface)
            _request=_owner.Engine.AnimationMessages.SubscribeMessage(0x8C,false,value=>
            {
                if(value is NativeRenameFaceRequest request && OwnerCurrent && !EngineRunning)
                {_name=request.NewName;_id=request.Id;}
            });
    }
    private bool OwnerCurrent=>Volatile.Read(ref _retired)==0 && !_owner.Lifetime.Destroying && ReferenceEquals(_owner.Engine.Robot,_owner) &&
        _owner.NativeBehaviorRuntime is {IsRetired:false} runtime && ReferenceEquals(runtime.Manipulation,_m);
    private bool Current(long generation)=>OwnerCurrent && Interlocked.Read(ref _generation)==generation && EngineRunning;
    protected override IDisposable? EnterHostExecutionOwner(BehaviorContext? context)=>_owner.Engine.EnterSynchronousOwner();
    protected override void HostRunStarting()=>Interlocked.Increment(ref _generation);
    protected override long CaptureHostExecutionVersion()=>Interlocked.Read(ref _generation);
    protected override bool HostExecutionVersionCurrent(long generation)=>OwnerCurrent && Interlocked.Read(ref _generation)==generation;
    protected override bool HostStopContinuationCurrent(long generation)=>Volatile.Read(ref _retired)==0 && Interlocked.Read(ref _generation)==generation+1;
    protected override bool HostResumeContinuationCurrent(long generation)=>OwnerCurrent && (Interlocked.Read(ref _generation)==generation || Interlocked.Read(ref _generation)==generation+1);
    protected override bool RunnableGate20(BehaviorContext context)=>false;
    protected override bool RunnableGate24(BehaviorContext context)=>false;
    protected override bool RunnableGate28(BehaviorContext context)=>false;
    protected override bool IsRunnableInternal(BehaviorContext context)=>_name.Length!=0;
    protected override bool KeepsRunningWithoutAction=>HasCurrentAction;

    protected override void OnStart()
    {
        long generation=CaptureHostExecutionVersion();
        if(!Current(generation))return;
        string name=_name;
        if(name.Length==0){InitFailed=true;return;}
        // Claim this request before any provider or logging callback can stop and restart us.
        _name="";_id=0;
        SayTextAction? speech=null;
        try
        {
            string? resources=_owner.Engine.Options.ResourcesPath;
            if(resources is null)throw new NativeActionUnavailable("M14-006 rename speech resources unavailable");
            var metadata=SpeechMetadata.Load(resources);
            if(!metadata.TrySelect("Name_Normal",name,_owner.Engine.ContextRandom,out var selected))
                throw new NativeActionUnavailable("M14-006 Name_Normal intent unavailable");
            if(!Current(generation))return;
            speech=new SayTextAction(_m.Robot,_owner,name,selected.Style,selected.Duration,selected.Pitch);
            if(!Current(generation))return;
            speech.SetCannedTrigger(_trigger);
            StartActing(speech,null,()=>Current(generation));
            speech=null;
        }
        catch(Exception error) when(error is NativeActionUnavailable or IOException or UnauthorizedAccessException or FormatException or InvalidOperationException or JsonException)
        {if(Current(generation))ReportMissing("M14-006 rename authored speech unavailable: "+error.Message);}
        finally {if(speech is not null && speech.CanRetireNeverAdmittedTree())speech.RetireNeverAdmittedTree();}
    }
    protected override void OnStop(BehaviorStopReason reason)=>Interlocked.Increment(ref _generation);
    public void Dispose(){Interlocked.Exchange(ref _retired,1);_request?.Dispose();RetireActionCompletionOwner();}
}
