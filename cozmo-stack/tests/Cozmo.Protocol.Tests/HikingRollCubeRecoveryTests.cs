using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;
using Fixture=Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;
namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class HikingRollCubeRecoveryTests
{
    private sealed class Parent:SteppedBehavior
    {
        internal Action<Parent>? Entry;
        internal Parent():base("roll-recovery-probe","Wait"){}
        public override bool IsRunnable(BehaviorContext context)=>true;
        protected override bool KeepsRunningWithoutAction=>true;
        protected override void OnStart()=>Entry?.Invoke(this);
        internal bool Delegate(HelperRef helper)=>SmartDelegateToHelper(helper,null,null);
    }
    private static void Start(Fixture f,Parent p)
    {p.BindActionCompletionOwner(f.Rig.Robot,f.Robot);f.Robot.BehaviorManagerOwner!.Add(p);Assert.True(f.Robot.BehaviorManagerOwner.StartAsync(p.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());}
    private static void Stop(Fixture f,Parent p)
    {f.Robot.BehaviorManagerOwner!.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);p.RetireActionCompletionOwner();f.Robot.ActionList.Clear();}
    [Fact]
    public void ThreeActualHelperAdmissionsCanFailBeforeAnyDockWireAndThenMarkFailure()
    {
        // Explicit external carry change at the StartRolling retry boundary; real composites, precondition failures,
        // response animations and original-result callbacks. No helper/runner terminal state is assigned.
        using var f=new Fixture();var obj=f.Add();f.Robot.CarryingOwner!.SetCarrying(7);f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        // Explicit helper-unit external admission: real ABORT completions lower Confidence and would
        // otherwise admit ReactToFrustrationMinor. Lock only that independent reaction; retain actual mood/results.
        f.Robot.BehaviorManagerOwner!.DisableReactionsWithLock("supplied-roll-retry-admission",Enumerable.Range(0,21).Select(i=>i==(int)ReactionTrigger.Frustration).ToArray(),false);
        var p=new Parent();var helper=new NativeRollBlockHelper(f.M,p,f.Runtime.AI.Helpers.Factory,7,true,0,null);helper.InitializeOnStack();p.Entry=_=>helper.StartRolling();Start(f,p);
        try
        {
            var reactions=new List<string>();f.Robot.BehaviorManagerOwner!.ReactionTriggered+=reaction=>reactions.Add(reaction.ToString());var logs=new List<string>();f.Rig.Engine.LogEntry+=entry=>logs.Add(entry.ToString());
            string deletionStack="";var trace=new List<string>();var reports=new List<RobotCompletedAction>();using var sub=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x5A,true,payload=>{if(payload is RobotCompletedAction r){reports.Add(r);if(r.ActionType==0x1A&&r.Result==0x02000001){deletionStack=Environment.StackTrace+" QUEUEcurrent="+f.Robot.ActionList.QueueAt(0)?.Current?.Tag+" pending="+f.Robot.ActionList.QueueAt(0)?.PendingCount+" parent="+p.EngineRunning+"/"+p.CurrentRunnerTag;}}});
            var animations=new HashSet<byte>();for(int i=0;i<80&&helper.Status==BehaviorStatus.Running;i++)
            {
                f.Rig.Tick();trace.Add($"attempts{helper.Attempts} helper{helper.Status} owned{p.OwnedRunner?.GetType().Name}:{p.OwnedRunner?.State:X8} anim{f.Rig.Robot.Animations.CurrentTag} leaf{helper.Runner?.Roll.State:X8}");byte tag=f.Rig.Robot.Animations.CurrentTag;if(tag!=0&&animations.Add(tag)){f.Rig.Data(new AnimationStarted{AnimId=tag});f.Rig.Data(new AnimationEnded{AnimId=tag});f.Rig.Tick();}
            }
            Assert.True(helper.Status==BehaviorStatus.Failure,"cap trace: "+string.Join(" | ",trace)+" REPORTS "+string.Join(" | ",reports)+" DELETESTACK "+deletionStack+" REACTIONS "+string.Join(" | ",reactions)+" LOGS "+string.Join(" | ",logs.TakeLast(50)));Assert.Equal(3u,helper.Attempts);Assert.Empty(f.Rig.SentSince(0).OfType<DockWithObject>());
            Assert.Empty(reactions);Assert.Equal(3,animations.Count);Assert.Single(f.M.Whiteboard.GetObjectFailureTable((ObjectActionFailure)3)[7]);
        }
        finally{f.Robot.CarryingOwner.UnsetCarrying();Stop(f,p);}
    }
    [Fact]
    public void ActualCarryingRollDelegatesPlaceTurnGroundWireAndRealCarryCompletionBeforeReevaluation()
    {
        using var f=new Fixture();var carried=f.Add(7,150);var target=f.Add(8,400);target.Pose=new Pose3d(Rotation3d.FromAngleAxis(Radians.Pi*.5f,new(0,1,0)),new(400,0,22),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));
        Assert.Equal(0u,f.M.Docking.SetObjectAsAttachedToLift(7,carried.Markers.First(m=>m.Face==BlockFace.Front).Code));
        var p=new Parent();NativeRollBlockHelper? helper=null;int words=0;f.Rig.Engine.ContextRandom.WordSourceForTest=()=>{words++;return 0;};
        p.Entry=parent=>{helper=new(f.M,parent,f.Runtime.AI.Helpers.Factory,8,true,0,null);IHelper? raw=helper;var handle=f.Runtime.AI.Helpers.AddHelperToComponent(ref raw);try{parent.Delegate(handle);}finally{handle.Release();}};Start(f,p);
        try
        {
            Assert.Equal(4,words);Assert.NotNull(helper!.PlaceHelper);Assert.IsType<NativeTurnInPlaceRunner>(p.OwnedRunner);f.Rig.Engine.ContextRandom.WordSourceForTest=null;
            bool sent=false;for(int i=0;i<80&&helper.PlaceHelper!.Status!=BehaviorStatus.Complete;i++)
            {
                f.Motors();
                if(!sent&&f.Rig.SentSince(0).OfType<PlaceObjectOnGround>().Any())
                {
                    sent=true;f.Motors(RobotStatusFlag.IsPickingOrPlacing);f.Rig.Data(new PickAndPlaceResult{Field1=true,Field3=(byte)BlockStatus.BlockPlaced});f.Motors(RobotStatusFlag.IsPickingOrPlacing);f.Motors();
                }
                // Explicit upstream processed-image/observed-object boundary inputs for the already checked Ground turn/verify recipient.
                // This does not claim its vision detector or physical scene; configured Roll test separately uses rendered images.
                f.Rig.Engine.AnimationMessages.Deliver(0x44,true,new NativeObservedObject(1,2,1,7));
                f.Rig.Engine.AnimationMessages.Deliver(0x43,true,new ProcessedImageReport(f.Robot.StoredState!.Timestamp,new uint[]{1},0));f.Rig.Tick();
            }
            Assert.True(sent);Assert.Single(f.Rig.SentSince(0).OfType<PlaceObjectOnGround>());Assert.Null(f.Robot.CarryingOwner!.CarriedObjectId);
            Assert.Equal(BehaviorStatus.Complete,helper.PlaceHelper!.Status);Assert.True(helper.First);f.Rig.Tick();Assert.DoesNotContain(helper.PlaceHelper,f.Runtime.AI.Helpers.StackHelpers);Assert.True(helper.DriveHelper is not null||helper.Runner is not null,$"carry reevaluation trace helper{helper.Status} first{helper.First} attempts{helper.Attempts} runner{helper.Runner?.State:X8} current{p.OwnedRunner?.GetType().Name}:{p.OwnedRunner?.State:X8} stack:"+string.Join(",",f.Runtime.AI.Helpers.StackHelpers.Select(h=>h.GetType().Name+":"+h.Status)));Assert.Equal(BehaviorStatus.Running,helper.Status);
        }
        finally{f.Rig.Engine.ContextRandom.WordSourceForTest=null;Stop(f,p);}
    }
    [Fact]
    public void ManagerStopRetiresOwnedRollOnlyKeepsUnrelatedActionAndRetainedStateLights()
    {
        using var scene=new RollFirmwareScene();var f=scene.F;var manager=f.Robot.BehaviorManagerOwner!;
        Assert.True(manager.StartAsync("Hiking_RollCube",f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());var roll=Assert.IsType<NativeRollBlockBehavior>(manager.Current);
        var owned=Assert.IsAssignableFrom<ActionRunner>(roll.OwnedRunner);var unrelated=new NativeTimedWaitRunner(f.M,100f);f.Robot.ActionList.QueueAction(QueueActionPosition.InParallel,unrelated);f.Rig.Tick();
        manager.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);f.Robot.ActionList.Watcher.Update();
        Assert.True(owned.DestructionObserved);Assert.False(unrelated.DestructionObserved);Assert.Equal(EngineActionResult.Running,unrelated.State);Assert.Equal(uint.MaxValue,roll.Target);Assert.False(roll.HasLiveHelper);Assert.Single(manager.InspectStateLights().Entries);
        f.Robot.ActionList.Clear();
    }
}
