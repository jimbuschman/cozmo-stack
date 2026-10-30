using System.Text.Json;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Behavior;

// Checked Bouncer own lifecycle. The production factory must supply the
// current-origin-aware face enumeration; no legacy-origin fallback is used.
internal sealed class NativeBouncerBehavior : SteppedBehavior, NativeBouncerController.IActionOwner, IDisposable
{
    private readonly ManipulationSystem _manipulation;
    private readonly EngineRobot _robot;
    private readonly VisionSystem _vision;
    private readonly FeatureGates? _features;
    private readonly Func<IReadOnlyList<int>> _eligibleFaces;
    private SmartFaceID _target = new();
    private readonly NativeBouncerController _controller;
    internal NativeBouncerController Controller => _controller;
    internal Action<string>? TriggerMissingForTest;
    internal uint? LastStaleRetiredTag { get; private set; }
    private long _generation;
    private int _retired;
    internal NativeBouncerBehavior(BehaviorFactoryContext source, JsonElement config, Func<IReadOnlyList<int>> eligibleFaces)
        : base(BehaviorConfigLoader.ExtractIdFromConfig(config).ToString(), "Bouncer")
    {
        _manipulation=source.Manipulation!; _robot=source.BouncerOwner!; _vision=source.Vision!;
        _features=source.Features; _eligibleFaces=eligibleFaces;
        _controller=new(this,(_,next)=>SetStateName(next));
    }
    protected override IDisposable? EnterHostExecutionOwner(BehaviorContext? context) => _manipulation.Robot.Engine.EnterSynchronousOwner();
    protected override void HostRunStarting()=>Interlocked.Increment(ref _generation);
    protected override long CaptureHostExecutionVersion()=>Interlocked.Read(ref _generation);
    protected override bool HostExecutionVersionCurrent(long version)=>Volatile.Read(ref _retired)==0 && Interlocked.Read(ref _generation)==version;
    protected override bool HostStopContinuationCurrent(long version)=>Volatile.Read(ref _retired)==0 && Interlocked.Read(ref _generation)==unchecked(version+1);
    protected override bool HostResumeContinuationCurrent(long version)=>Volatile.Read(ref _retired)==0 && (Interlocked.Read(ref _generation)==version || Interlocked.Read(ref _generation)==unchecked(version+1));
    protected override bool RunnableGate20(BehaviorContext context)=>false;
    protected override bool RunnableGate24(BehaviorContext context)=>false;
    protected override bool RunnableGate28(BehaviorContext context)=>false;
    protected override bool KeepsRunningWithoutAction=>true;
    public override bool IsRunnable(BehaviorContext context)=>IsRunnableBase(context)&&IsRunnableInternal(context);
    protected override bool IsRunnableInternal(BehaviorContext context)=>RunnableCore(context);
    internal bool RunnableCore(BehaviorContext context)
    {
        using var owner=EnterHostExecutionOwner(context);
        long version=CaptureHostExecutionVersion();
        if(_features is null){ReportMissing("Bouncer feature8 map unavailable; M15-007 configured input not substituted");return false;}
        if(!_features.IsEnabled("Bouncer"))return false;
        int[] ids;
        try { ids=_eligibleFaces().OrderBy(id=>id).ToArray(); }
        catch(InvalidOperationException error) when(error.Message.StartsWith("MISSING:",StringComparison.Ordinal))
        { ReportMissing(error.Message); return false; }
        if(!HostExecutionVersionCurrent(version))return false;
        if(ids.Length==0)return false;
        if(ids.Length>1)
        {
            ReportMissing("M14-001/M11-066 Bouncer multi-face ranking needs actual native relative face poses; legacy flattened geometry is not used");
            return false;
        }
        int best=ids[0]; // GetBestFaceToTrack's singleton fast path, before any pose reads.
        var next=_vision.Faces.GetSmartFaceID(best);_target.Dispose();_target=next;
        return _target.IsValid;
    }
    protected override void OnStart()
    {
        long version=CaptureHostExecutionVersion();
        bool reactionsAcquired=Scope.AcquireScopedReactions(Id,ReactionLockTables.Bouncer);
        if(!HostExecutionVersionCurrent(version))return;
        if(!reactionsAcquired){InitFailed=true;return;}
        Scope.AcquireScopedIdle(_manipulation.Robot.Animations.Scheduler,(AnimationTrigger)0x23F,Id,
            ()=>_manipulation.Robot.Engine.Timer.SecondsF*1000.0);
        if(!HostExecutionVersionCurrent(version))return;
        _controller.Initialize(128,64,_manipulation.Robot.Engine.ContextRandom,()=>HostExecutionVersionCurrent(version));
    }
    protected override void OnUpdate()
    {
        long version=CaptureHostExecutionVersion();
        var face=_vision.Faces.GetFace(_target);
        var input=new NativeBouncerController.FaceInput(_target.IsValid,face is not null,face?.LastObservedTimestamp??0,face?.NativeDirectionDcRadians);
        float duration=_manipulation.Robot.Engine.Timer.SecondsF-(float)RunningPenaltyClockSec;
        uint imageTime=_manipulation.Robot.CameraSettings.LastProcessedImageTimestamp;
        if(duration>60f) _manipulation.Robot.Engine.EmitSourceLog(EngineLogLevel.Warning,null,"BehaviorBouncer.UpdateInternal","Behavior has timed out");
        else if(!input.Valid) _manipulation.Robot.Engine.EmitSourceLog(EngineLogLevel.Warning,null,"BehaviorBouncer.UpdateInternal","Target face ID is invalid");
        else if(!input.Found) _manipulation.Robot.Engine.EmitSourceLog(EngineLogLevel.Warning,null,"BehaviorBouncer.UpdateInternal","Target face is missing");
        else if(unchecked(imageTime-input.LastObserved)>=5001u) _manipulation.Robot.Engine.EmitSourceLog(EngineLogLevel.Warning,null,"BehaviorBouncer.UpdateInternal","Target face is too old");
        if(!HostExecutionVersionCurrent(version))return;
        try
        {
            var result=_controller.Update(duration,imageTime,input);
            if(HostExecutionVersionCurrent(version) && result==NativeBouncerController.UpdateResult.Complete) Finish();
        }
        catch(InvalidOperationException error) when(error.Message.StartsWith("MISSING:",StringComparison.Ordinal))
        {
            long missingVersion=CaptureHostExecutionVersion();
            ReportMissing(error.Message+"; stopping Bouncer because actual detector output is unavailable (M11-016/M14-010)");
            if(!HostExecutionVersionCurrent(missingVersion))return;
            StopActing(keepAction:false,viaCallback:false);
            if(!HostExecutionVersionCurrent(missingVersion))return;
            Finish();
        }
    }
    bool NativeBouncerController.IActionOwner.IsActing=>HasCurrentAction;
    void NativeBouncerController.IActionOwner.StartTrigger(int trigger,Action completed)
    {
        long version=CaptureHostExecutionVersion();
        var action=new TriggerAnimationAction(_manipulation.Robot,new((AnimationTrigger)trigger,1,true,0,60f,false),TriggerMissingForTest);
        if(!HostExecutionVersionCurrent(version))
        { if(action.RetireStaleConstructor(()=>HostExecutionVersionCurrent(version)))LastStaleRetiredTag=action.Tag; return; }
        StartActing(action,_=>completed());
    }
    void NativeBouncerController.IActionOwner.StartDisplay(NativeBouncerGame game)
    {
        long version=CaptureHostExecutionVersion();
        var action=new NativeSetFaceImageAction(_manipulation.Robot,NativeBouncerRaster.Draw(game));
        if(!HostExecutionVersionCurrent(version)){action.WatcherEnding();return;}
        StartActing(action,_=>{});
    }
    protected override void OnStop(BehaviorStopReason reason) => Interlocked.Increment(ref _generation); // host generation only; native 005F1C6C is empty
    public void Dispose(){if(Interlocked.Exchange(ref _retired,1)!=0)return;Interlocked.Increment(ref _generation);_target.Dispose();RetireActionCompletionOwner();}
}
