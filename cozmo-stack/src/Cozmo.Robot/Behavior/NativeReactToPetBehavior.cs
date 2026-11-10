using System.Text.Json;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Behavior;

// Configured class 0x43, 0x606E98..0x607560.
internal sealed class NativeReactToPetBehavior : SteppedBehavior, IDisposable
{
    private readonly ManipulationSystem _m;
    private readonly EngineRobot _owner;
    private readonly SortedSet<int> _targets=new();
    private readonly List<Action<IReadOnlyCollection<int>>> _listeners=new();
    private long _generation;
    private int _retired, _selected;
    private float _deadline=-1;
    internal int Selected=>_selected;
    internal IReadOnlyList<int> Targets=>_targets.ToArray();
    internal float Deadline=>_deadline;
    internal NativeReactToPetBehavior(BehaviorFactoryContext context,JsonElement json)
        :base(BehaviorConfigLoader.ExtractIdFromConfig(json).ToString(),"ReactToPet")
    {
        _m=context.Manipulation!;_owner=context.NativeRobot!;
        BindActionCompletionOwner(_m.Robot,_owner);
    }
    internal void AddListener(Action<IReadOnlyCollection<int>> listener){if(!_listeners.Contains(listener))_listeners.Add(listener);}
    internal void RemoveListener(Action<IReadOnlyCollection<int>> listener)=>_listeners.Remove(listener);
    internal void SetTargets(IEnumerable<int> values){_targets.Clear();foreach(int id in values)_targets.Add(id);}
    private bool Available=>Volatile.Read(ref _retired)==0 && !_owner.Lifetime.Destroying && ReferenceEquals(_owner.Engine.Robot,_owner) &&
        _owner.NativeBehaviorRuntime is { IsRetired:false } runtime && ReferenceEquals(runtime.Manipulation,_m) &&
        runtime.Bound.TryGetValue(Id,out var actual) && ReferenceEquals(actual,this);
    protected override IDisposable? EnterHostExecutionOwner(BehaviorContext? context)=>_owner.Engine.EnterSynchronousOwner();
    protected override void HostRunStarting()=>Interlocked.Increment(ref _generation);
    protected override long CaptureHostExecutionVersion()=>Interlocked.Read(ref _generation);
    protected override bool HostExecutionVersionCurrent(long version)=>Available && Interlocked.Read(ref _generation)==version;
    protected override bool HostStopContinuationCurrent(long version)=>Volatile.Read(ref _retired)==0 && Interlocked.Read(ref _generation)==unchecked(version+1);
    protected override bool HostResumeContinuationCurrent(long version)=>Available && (Interlocked.Read(ref _generation)==version || Interlocked.Read(ref _generation)==unchecked(version+1));
    protected override bool KeepsRunningWithoutAction=>_selected==0 || _deadline>=0 || HasCurrentAction;
    protected override bool IsRunnableInternal(BehaviorContext context)=>Available && (_selected!=0 || _targets.Count!=0);
    private bool Current(long generation)=>HostExecutionVersionCurrent(generation);

    protected override void OnStart()
    {
        long generation=CaptureHostExecutionVersion();_selected=0;_deadline=-1;
        if(!Current(generation))return;
        var pets=_m.Vision.Pets.GetNativePets(_owner);
        if(pets is null){InitFailed=true;ReportMissing("M14-004 ReactToPet original native PetWorld unavailable");return;}
        PetEntry? selected=null;
        foreach(int id in _targets)
        {
            selected=pets.FirstOrDefault(p=>p.Id==id);
            if(selected is not null){_selected=id;break;}
        }
        if(_selected==0 || selected is null)
        {
            _owner.Engine.EmitSourceLog(EngineLogLevel.Error,null,"ReactToPet.BeginIteration.NoValidTarget","No valid target");
            if(Current(generation))Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak();
            return;
        }
        int type=(int)selected.Type;
        if(type is 1 or 2 && _owner.Engine.ContextRandom.RandInt(100)<20)type=0;
        var trigger=type switch {0=>AnimationTrigger.PetDetectionSneeze,1=>AnimationTrigger.PetDetectionCat,2=>AnimationTrigger.PetDetectionDog,_=>AnimationTrigger.PetDetectionSneeze};
        float now=_owner.Engine.Timer.SecondsF;
        float duration=(float)_owner.Engine.ContextRandom.RandDblInRange(10d,15d);
        if(!Current(generation))return;
        if(type is <0 or >2){InitFailed=true;ReportMissing("M14-004 ReactToPet unsupported pet type");return;}
        float x=(float)selected.Rect.X+(float)selected.Rect.Width*.5f;
        float y=(float)selected.Rect.Y+(float)selected.Rect.Height*.5f;
        var turn=new NativeImagePointRunner(_m,_owner,x,y,selected.LastObservedTimestamp);
        turn.ScopedParentCurrent=()=>Current(generation);
        var animation=new TriggerAnimationAction(_m.Robot,new(trigger,1,true,0,60f,false));
        if(!Current(generation)){turn.RetireNeverAdmittedTree();animation.RetireNeverAdmittedTree();return;}
        animation.UseOrdinaryAuthoredAudio(_owner);
        var track=new NativeTrackAnyPetRunner(_m,_owner){ScopedParentCurrent=()=>Current(generation)};
        if(!Current(generation)){turn.RetireNeverAdmittedTree();animation.RetireNeverAdmittedTree();track.RetireNeverAdmittedTree();return;}
        var sequence=new CompoundActionSequential(()=>_owner.Engine.Timer.SecondsF);
        if(!sequence.AddActionGuarded(turn,false,()=>Current(generation)))
        {turn.RetireNeverAdmittedTree();animation.RetireNeverAdmittedTree();track.RetireNeverAdmittedTree();sequence.RetireNeverAdmittedTree();return;}
        if(!sequence.AddActionGuarded(animation,false,()=>Current(generation)))
        {animation.RetireNeverAdmittedTree();track.RetireNeverAdmittedTree();sequence.RetireNeverAdmittedTree();return;}
        if(!sequence.AddActionGuarded(track,false,()=>Current(generation)))
        {track.RetireNeverAdmittedTree();sequence.RetireNeverAdmittedTree();return;}
        _deadline=now+duration;
        StartActing(sequence,_=>{if(Current(generation))EndIteration(generation);},()=>Current(generation));
    }
    private void EndIteration(long generation)
    {
        var targets=_targets.ToArray();
        foreach(var listener in _listeners.ToArray())
        {if(!Current(generation))return;listener(targets);}
        if(!Current(generation))return;
        _targets.Clear();_selected=0;_deadline=-1;
    }
    protected override void OnUpdate()
    {
        long generation=CaptureHostExecutionVersion();
        if(!Current(generation))return;
        if(_selected==0 || _deadline>-1 && _owner.Engine.Timer.SecondsF>_deadline)
        {EndIteration(generation);if(Current(generation))Finish();}
    }
    protected override void OnStop(BehaviorStopReason reason)
    {
        long generation=Interlocked.Increment(ref _generation);
        if(Current(generation))EndIteration(generation);
    }
    public void Dispose(){Interlocked.Exchange(ref _retired,1);_listeners.Clear();RetireActionCompletionOwner();}
}
