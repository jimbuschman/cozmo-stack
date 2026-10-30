using System.Text.Json;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
namespace Cozmo.Robot.Behavior;

// fidelity: M13-014
//K1-K14 actual configured native class; public manual KnockOverCubesBehavior remains separate.
internal sealed class NativeKnockOverCubesBehavior:SteppedBehavior,IDisposable
{
    private readonly ManipulationSystem _m;
    private readonly EngineRobot _robot;
    private readonly RetainedSubscription? _upAxis;
    private WeakReference<NativeStackConfiguration>? _target;
    private readonly HashSet<uint> _tipped=new();
    private readonly bool _always;
    private readonly string? _needsAction;
    private bool _hardSpark;
    private int _epoch;
    internal byte MinimumHeight {get;}
    internal uint Bottom {get;private set;}=uint.MaxValue;
    internal uint Middle {get;private set;}=uint.MaxValue;
    internal uint Top {get;private set;}=uint.MaxValue;
    internal int Attempts {get;private set;}
    internal IReadOnlyCollection<uint> Tipped=>_tipped;
    internal string Phase {get;private set;}="Idle";
    internal NativeDriveAndFlipRunner? Interaction {get;private set;}
    internal CompoundActionSequential? LastSequence {get;private set;}
    internal AnimationTrigger ReachTrigger {get;}
    internal AnimationTrigger PutDownTrigger {get;}
    internal AnimationTrigger EyesTrigger {get;}
    internal AnimationTrigger SuccessTrigger {get;}
    internal AnimationTrigger FailureTrigger {get;}
    internal bool HardSpark=>_hardSpark;
    private static readonly ReactionLockTable InitialLock=new("KnockOverCubes","0xC67CB2",new[]{1,5,8});
    private static readonly ReactionLockTable PrepareLock=new("preparingToKnockOverDisable","0xC67CDC",Array.Empty<int>());
    internal NativeKnockOverCubesBehavior(BehaviorFactoryContext source,JsonElement json):base(BehaviorConfigLoader.ExtractIdFromConfig(json).ToString(),"KnockOverCubes")
    {
        _m=source.Manipulation!;_robot=source.NativeRobot!;
        MinimumHeight=unchecked((byte)(json.TryGetProperty("minimumStackHeight",out var min)?min.GetInt32():3));
        _always=json.TryGetProperty("alwaysStreamline",out var always)&&always.GetBoolean();
        _needsAction=json.TryGetProperty("needsActionID",out var need)?need.GetString():null;
        AnimationTrigger Trigger(string key)=>json.TryGetProperty(key,out var value)?Enum.Parse<AnimationTrigger>(value.GetString()!,false):(AnimationTrigger)0x23F;
        ReachTrigger=Trigger("reachForBlockTrigger");PutDownTrigger=Trigger("knockOverPutDownTrigger");EyesTrigger=Trigger("knockOverEyesTrigger");SuccessTrigger=Trigger("knockOverSuccessTrigger");FailureTrigger=Trigger("knockOverFailureTrigger");
        if(_robot.HasExternalInterface)_upAxis=_m.Robot.Engine.AnimationMessages.SubscribeMessage(0x11,true,OnUpAxis);
    }
    internal void BindNativeCommonInputs(NativeBehaviorBaseInputs inputs)
    {
        UnlockIdentifier=()=>unchecked((int)inputs.RequiredUnlock);
        ActiveSparkIdentifier=()=>RequireManager().ActiveSparkId;
    }
    private BehaviorManager RequireManager()
    {
        if(_robot.BehaviorManagerOwner is {} manager)return manager;
        const string missing="M7-014/M13-014 actual canonical BehaviorManager spark owner unavailable";
        ReportMissing(missing);throw new NativeActionUnavailable(missing);
    }
    protected override bool KeepsRunningWithoutAction=>HasCurrentAction;
    protected override bool RunnableGate20(BehaviorContext context)=>false;
    protected override bool RunnableGate24(BehaviorContext context)=>false;
    protected override bool RunnableGate28(BehaviorContext context)=>false;
    public override bool IsRunnable(BehaviorContext context)=>IsRunnableBase(context)&&IsRunnableInternal(context);
    protected override bool IsRunnableInternal(BehaviorContext context)
    {
        using var mutation=_m.World.EnterPoseMutation();
        try
        {
            var stack=_m.NativeConfigurations.Tallest();
            _target=stack is null?null:new WeakReference<NativeStackConfiguration>(stack);
            return stack is not null&&(uint)stack.BlockIds.Count>=MinimumHeight;
        }
        catch(NativeConfigurationUnavailable error){MarkReadinessUnavailable();ReportMissing("M13-014 actual stack cache unavailable: "+error.Message);return false;}
    }
    private bool InitializeMembers()
    {
        using var mutation=_m.World.EnterPoseMutation();
        //5C31D8 only locks the cached weak; refresh belongs exclusively to IsRunnable.
        if(_target is null||!_target.TryGetTarget(out var stack)||!_m.NativeConfigurations.Snapshot.Stacks.Any(s=>ReferenceEquals(s,stack)))return false;
        Scope.SmartDisableReactionsWithLock(Id,InitialLock);_tipped.Clear();Attempts=0;
        Bottom=stack.Bottom;Middle=stack.Middle;Top=stack.Top;return true;
    }
    protected override void OnStart()
    {
        Interlocked.Increment(ref _epoch);
        var manager=RequireManager();_hardSpark=manager.ActiveSparkId!=0x55&&(manager.ActiveSparkSoft^1)!=0;
        if(!InitializeMembers()){InitFailed=true;return;}
        if(_always||_hardSpark)Knock();else Reach();
    }
    protected override int ResumeInternal()
    {
        BeginRun(Context,Scope);Interlocked.Increment(ref _epoch);
        if(!InitializeMembers())return 1;
        Knock();return 0;
    }
    protected override void OnStop(BehaviorStopReason reason){Interlocked.Increment(ref _epoch);ClearStack();}
    private void ClearStack(){_target=null;Bottom=Middle=Top=uint.MaxValue;}
    private void OnUpAxis(object? payload)
    {
        if(!EngineRunning||payload is not CubeUpAxisEvent axis)return;
        uint id=unchecked((uint)axis.ObjectID);
        if(id==Bottom||id==Top||(Middle!=uint.MaxValue&&id==Middle))_tipped.Add(id);
    }
    private CompoundActionSequential Sequence()
    {var sequence=new KnockSequence(_m,this);sequence.SeedNativeEmptyCompletion();LastSequence=sequence;return sequence;}
    private void Reach()
    {
        Phase="ReachingForBlock";SetStateName(Phase);
        using var mutation=_m.World.EnterPoseMutation();
        var top=_m.World.GetLocatedObjectByIdHelper(Top);if(top is null){ClearStack();return;}
        var sequence=Sequence();sequence.AddAction(new NativeTurnTowardsObjectRunner(_m,Bottom,NativeTurnTowardsObjectRunner.AnyCode,Radians.Pi,false,false),false,false);
        using var relative=new Pose3d("");
        if(top.NativePose.TryWithRespectTo(_robot.GetCheckedMainPose(),relative))
        {float x=(float)relative.Translation.X;if(x+10f>85f)sequence.AddAction(new DriveStraightAction(_m,x-85f,60,true),false,false);}
        sequence.AddAction(LiftSafe(ReachTrigger),false,false);StartActing(sequence,_=>Knock());
    }
    private TriggerLiftSafeAnimationAction LiftSafe(AnimationTrigger trigger)=>new(_m.Robot,new(trigger,1,true,0,60,false));
    private void Prepare()
    {
        _tipped.Clear();IncreaseScoreWhileActing(10f);
        Scope.SmartRemoveDisableReactionsLock(PrepareLock.Name);Scope.SmartDisableReactionsWithLock(PrepareLock.Name,PrepareLock);
    }
    internal void Knock()
    {
        Phase="KnockingOverStack";SetStateName(Phase);
        float maximum=_always||_hardSpark||Attempts>0?0:BitConverter.UInt32BitsToSingle(0x3FC90FDB);
        var interaction=new NativeDriveAndFlipRunner(_m,Bottom,maximum);interaction.SetNameTriggers(0xFD,0xFE);Interaction=interaction;
        var sequence=Sequence();sequence.AddAction(new NativeTurnTowardsObjectRunner(_m,Bottom,NativeTurnTowardsObjectRunner.AnyCode,Radians.Pi,false,false),false,false);
        sequence.AddAction(interaction,false,true);sequence.AddAction(new NativeTimedWaitRunner(_m,.5f),false,false);
        interaction.Unavailable=error=>Unavailable(error,sequence.Tag);
        Prepare();StartActing(sequence,completed=>Respond(completed.Result));
    }
    internal void Respond(uint result)
    {
        if(result==0x03000010){_m.Whiteboard.NoPreDockPosesObjectId=Bottom;return;}
        uint category=result>>24;
        if(category==4){if(Attempts<=1)Knock();else Blind();Attempts=unchecked(Attempts+1);}
        else if(category==0)Reaction();
    }
    private void Blind()
    {
        Phase="BlindlyFlipping";SetStateName(Phase);var sequence=Sequence();
        var flip=new NativeFlipBlockRunner(_m,Bottom){CheckPreActionPose=false};flip.Unavailable=error=>Unavailable(error,sequence.Tag);
        sequence.AddAction(flip,false,false);sequence.AddAction(new NativeTimedWaitRunner(_m,.5f),false,false);
        Prepare();StartActing(sequence,_=>Reaction());
    }
    internal void Reaction()
    {
        Phase="PlayingReaction";SetStateName(Phase);_m.NativeConfigurations.ForceUpdate();
        bool success=_tipped.Count!=0;
        if(success)
        {
            _m.Robot.Engine.ReportBehaviorObjective(13,"KnockedOverBlocks",Id);
            if(_needsAction is {} action)_m.Robot.Engine.Needs.RegisterNeedsActionCompleted(action);
        }
        if(_always||_hardSpark)return;
        StartActing(LiftSafe(success?SuccessTrigger:FailureTrigger),null);
    }
    private void Unavailable(NativeActionUnavailable error,uint tag)
    {
        ReportMissing("M13-014 native KnockOver child unavailable: "+error.Message);
        int epoch=Volatile.Read(ref _epoch);
        _robot.ActionList.DeferHostCleanup(()=>
        {
            if(epoch!=Volatile.Read(ref _epoch)||CurrentRunnerTag!=tag)return;
            if(ReferenceEquals(_robot.BehaviorManagerOwner?.Current,this))_robot.BehaviorManagerOwner.Stop(BehaviorStopReason.Interrupted,_m.Robot.Engine.Timer.SecondsF);
            else Stop(BehaviorStopReason.Interrupted);
        });
    }
    private sealed class KnockSequence:CompoundActionSequential
    {
        private readonly ManipulationSystem _m;private readonly NativeKnockOverCubesBehavior _owner;
        internal KnockSequence(ManipulationSystem m,NativeKnockOverCubesBehavior owner):base(()=>m.Robot.Engine.Timer.SecondsF){_m=m;_owner=owner;SourceLog=(l,c,k,t)=>m.Robot.Engine.EmitSourceLog(l,c,k,t);}
        public override uint Update()
        {
            int depth=Watcher?.HostUpdateDepth??0;
            try{return base.Update();}
            catch(NativeActionUnavailable error){Watcher?.UnwindUnavailableHostUpdate(depth);_owner.Unavailable(error,Tag);return EngineActionResult.Running;}
        }
    }
    public void Dispose(){_upAxis?.Dispose();}
}
