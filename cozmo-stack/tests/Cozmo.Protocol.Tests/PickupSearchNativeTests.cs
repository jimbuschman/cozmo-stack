using System.Reflection;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;
using Fixture=Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;
namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class PickupSearchNativeTests
{
    private sealed class Parent:SteppedBehavior
    {
        internal Action<Parent>? Entry;
        internal Parent():base("native-search-unit","Probe"){}
        public override bool IsRunnable(BehaviorContext context)=>true;
        protected override bool KeepsRunningWithoutAction=>HasCurrentAction;
        protected override void OnStart()=>Entry?.Invoke(this);
        internal bool Delegate(HelperRef helper,Action<CozmoRobot>? success,Action<CozmoRobot>? failure)=>SmartDelegateToHelper(helper,success,failure);
        internal void StopOwned(bool keep)=>StopActing(keep,true);
    }
    private sealed class Firmware(Fixture f)
    {
        private int _mark;
        private ExecutePath? _pending;
        private readonly HashSet<byte> _animations=new();
        internal void Step()
        {
            f.Motors();
            if(_pending is {} finished)
            {
                f.Rig.Data(new PathFollowingEvent{EventId=finished.EventId,EventType=(byte)PathEventType.Completed});
                _pending=null;f.Motors();
            }
            var messages=f.Rig.SentSince(_mark);_mark=f.Rig.Mark();
            foreach(var message in messages)
            {
                if(message is AppendPathSegmentLine line){f.X=line.XEndMm;f.Y=line.YEndMm;}
                if(message is ExecutePath execute)
                {f.Rig.Data(new PathFollowingEvent{EventId=execute.EventId,EventType=(byte)PathEventType.Started});_pending=execute;f.Motors();}
            }
            byte tag=f.Rig.Robot.Animations.CurrentTag;
            if(tag!=0&&_animations.Add(tag))
            {f.Rig.Data(new AnimationStarted{AnimId=tag});f.Rig.Data(new AnimationEnded{AnimId=tag});f.Rig.Tick();}
        }
    }
    [Fact]
    public void TimedWaitUsesEngineBinary32StrictDeadlineAndReinitRatherThanRobotOrHostClock()
    {
        using var f=new Fixture();f.Rig.Engine.Timer.UpdateTime(100_000_000_000);
        var wait=new NativeTimedWaitRunner(f.M,.8f);
        try
        {
            Assert.Equal((byte)6,wait.GetCompletionVariant());Assert.Equal(0x31,wait.Type);Assert.Equal(0u,wait.RequiredTrackMask);Assert.False(wait.CanInterrupt());Assert.Equal(-1f,wait.Deadline);
            Assert.Equal(0,wait.Init());Assert.Equal(100f+.8f,wait.Deadline);
            f.Rig.Engine.Timer.UpdateTime((long)((double)wait.Deadline*1e9));Assert.Equal(EngineActionResult.Running,wait.CheckIfDone());
            f.Rig.Engine.Timer.UpdateTime((long)((double)MathF.BitIncrement(wait.Deadline)*1e9));Assert.Equal(0u,wait.CheckIfDone());
            f.Rig.Engine.Timer.UpdateTime(200_000_000_000);Assert.Equal(0,wait.Init());Assert.Equal(200f+.8f,wait.Deadline);Assert.Equal(EngineActionResult.Running,wait.CheckIfDone());
        }
        finally{wait.Prep();wait.WatcherEnding();}
    }
    [Fact]
    public void NearbyUsesSixSharedDrawsSevenRealChildrenNegativeDefaultSpeedAndRetainsPreinitLatch()
    {
        using var f=new Fixture();f.Add();int words=0;var rng=f.Rig.Engine.ContextRandom;
        rng.WordSourceForTest=()=>++words%2==1?0u:0x80000000u; //exact u=.5 for EACH of six calls.
        var nearby=new NativeNearbySearchRunner(f.M,7,-20,100,-.08726646f);
        try
        {
            f.Rig.Engine.AnimationMessages.Deliver(0x44,true,new NativeObservedObject(0,2,1,7));Assert.True(nearby.SeenLatch);
            Assert.Equal(0,nearby.Init());Assert.Equal(12,words);Assert.Equal(0x21,nearby.Type);Assert.Equal(0u,nearby.RequiredTrackMask);Assert.False(nearby.CanInterrupt());
            var children=nearby.Sequence.Children.ToArray();Assert.Equal(7,children.Length);
            Assert.Equal(new[]{0x31,unchecked((int)0xFFFFFFFE),0x31,0x28,0x31,0x28,0x31},children.Select(c=>c.Type));
            Assert.Equal(1f,((NativeTimedWaitRunner)children[0]).Duration);Assert.Equal(1f,((NativeTimedWaitRunner)children[2]).Duration);Assert.NotSame(children[0],children[2]);
            var parallel=Assert.IsType<CompoundActionParallel>(children[1]);var drive=Assert.IsType<DriveStraightAction>(parallel.Children[0]);
            Assert.Equal(-20,drive.DistanceMm);Assert.Equal(-80f,drive.SpeedMmps);Assert.False(drive.ShouldPlayAnimation);
            Assert.Equal(2,parallel.Children.Count);Assert.Equal(1u,parallel.Children[1].RequiredTrackMask);
            Assert.Equal(-.30543262f,nearby.Draw.Turn1);Assert.Equal(.61086524f,nearby.Draw.Turn2);
            f.Rig.Engine.Timer.UpdateTime((long)((double)MathF.BitIncrement(((NativeTimedWaitRunner)children[0]).Deadline)*1e9));
            Assert.Equal(0u,nearby.CheckIfDone());Assert.False(nearby.SeenLatch);Assert.Equal(0u,children[0].State); //embeddedUpdate happened BEFORE matching latch return.
            f.Rig.Engine.AnimationMessages.Deliver(0x44,true,new NativeObservedObject(1,2,1,7));Assert.Equal(0,nearby.Init());Assert.Equal(24,words);
            Assert.All(children,c=>Assert.True(c.DestructionObserved));Assert.True(nearby.SeenLatch); //Init does NOT clear12C.
        }
        finally{rng.WordSourceForTest=null;nearby.State=EngineActionResult.Running;nearby.Prep();nearby.WatcherEnding();}
    }
    [Fact]
    public void NearbySpecificTargetNormalCompletionFailsAfterRealBackupHeadAndRelativeTurns()
    {
        using var f=new Fixture();f.Add();var firmware=new Firmware(f);var action=new NativeNearbySearchRunner(f.M,7,-20,100,-.08726646f);
        var completed=new List<RobotCompletedAction>();var typed=new List<ActionCompletionReport>();f.Rig.Engine.ActionCompleted+=typed.Add;using var reports=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x5A,true,p=>{if(p is RobotCompletedAction value)completed.Add(value);});
        f.Robot.ActionList.QueueAction(QueueActionPosition.Now,action);
        try
        {
            for(int i=0;i<200&&!action.DestructionObserved;i++)firmware.Step();
            Assert.True(action.DestructionObserved);Assert.Equal(0x0300001Du,action.State);
            var line=Assert.Single(f.Rig.SentSince(0).OfType<AppendPathSegmentLine>());Assert.Equal(-80f,line.Speed.SpeedMmps);
            Assert.NotEmpty(f.Rig.SentSince(0).OfType<SetHeadAngle>());Assert.Equal(2,f.Rig.SentSince(0).OfType<SetBodyAngle>().Count());
            var report=Assert.Single(completed.Where(r=>r.Tag==action.Tag));Assert.Equal(0x0300001Du,report.Result);Assert.Equal((byte)6,report.CompletionVariant);Assert.Equal((byte)6,report.PackBody()[^1]);Assert.Equal(14+4*report.SubActionResults.Count,report.PackBody().Length);
            f.Rig.Engine.DrainAppDelivery();Assert.Equal((byte)6,Assert.Single(typed.Where(r=>r.Tag==action.Tag)).CompletionVariant);
        }
        finally{f.Robot.ActionList.Clear();}
    }
    [Fact]
    public void RealFactoryHelperTargetHitCancelsOwnedActionBeforeSuccessAndRetires44OnlyAtLastOwner()
    {
        //Direct supplied helper entry, actual manager/parent84/helper stack/ActionList. LOCAL44 is an injected recipient input here.
        using var f=new Fixture();f.Add();var parent=new Parent();parent.BindActionCompletionOwner(f.Rig.Robot,f.Robot);
        NativeSearchForBlockHelper? search=null;HelperRef? held=null;CompoundActionSequential? sequence=null;bool success=false,failureCalled=false,delegated=false,destroyedAtSuccess=false;uint tagAtSuccess=uint.MaxValue;
        var local=new List<RobotCompletedAction>();var typed=new List<ActionCompletionReport>();var raw=new List<byte[]>();
        using var completed=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x5A,true,p=>{if(p is RobotCompletedAction value)local.Add(value);});
        f.Rig.Engine.ActionCompleted+=typed.Add;f.Rig.Engine.EngineToGameMessage+=(tag,body)=>{if(tag==0x5A)raw.Add(body.ToArray());};
        parent.Entry=p=>
        {
            var handle=f.Runtime.AI.Helpers.Factory.CreateNativeSearchForBlock(f.M,p,7);held=handle.Copy();search=(NativeSearchForBlockHelper)handle.Pointer;
            try{delegated=p.Delegate(handle,_=>{destroyedAtSuccess=sequence!.DestructionObserved;tagAtSuccess=p.CurrentRunnerTag;success=true;},_=>failureCalled=true);}
            finally{handle.Release();}
        };
        var manager=f.Robot.BehaviorManagerOwner!;manager.Add(parent);
        try
        {
            Assert.True(manager.StartAsync(parent.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
            Assert.True(delegated);sequence=Assert.IsAssignableFrom<CompoundActionSequential>(parent.OwnedRunner);Assert.Equal((byte)6,sequence.GetCompletionVariant());Assert.NotNull(search);Assert.Equal(1,search!.Stage);Assert.True(search.SnapshotAttempted);
            Assert.IsType<NativeTurnTowardsObjectRunner>(sequence.Children[0]);Assert.Same(search.Nearby,sequence.Children[1]);
            f.Rig.Engine.AnimationMessages.Deliver(0x44,true,new NativeObservedObject(9,2,1,7));Assert.Equal(BehaviorStatus.Complete,search.Status);Assert.True(search.Nearby!.SeenLatch);
            f.Runtime.AI.Helpers.Update(f.Rig.Robot);Assert.True(success);Assert.False(failureCalled);Assert.True(destroyedAtSuccess);Assert.Equal(0u,tagAtSuccess);Assert.True(sequence.DestructionObserved);Assert.Equal(0,f.Runtime.AI.Helpers.StackCount);
            f.Rig.Engine.DrainAppDelivery();
            var completion=Assert.Single(local.Where(r=>r.Tag==sequence.Tag));Assert.Equal((byte)6,completion.CompletionVariant);
            Assert.Equal((byte)6,Assert.Single(typed.Where(r=>r.Tag==sequence.Tag)).CompletionVariant);
            var wire=Assert.Single(raw.Where(b=>System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(b)==sequence.Tag));
            Assert.Equal(completion.PackBody(),wire);Assert.Equal((byte)6,wire[^1]);Assert.Equal(14+4*completion.SubActionResults.Count,wire.Length);
            Assert.Equal(1,held!.Control.Owners);Assert.False(search.Retired); //terminal held helper still owns its retained44.
            var filter=new BlockWorldFilter();filter.AllowedIds.Add(7);f.M.World.DeleteLocatedObjects(filter);
            f.Rig.Engine.AnimationMessages.Deliver(0x44,true,new NativeObservedObject(10,2,1,8));Assert.Equal(BehaviorStatus.Failure,search.Status); //held terminal helper still consumes44; targetmissing is defined false.
            held.Release();held=null;Assert.True(search.Retired);
            f.Rig.Engine.AnimationMessages.Deliver(0x44,true,new NativeObservedObject(11,2,1,7));Assert.Equal(BehaviorStatus.Failure,search.Status); //last-owner retirement cannot resurrect it.
        }
        finally{manager.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);held?.Release();parent.RetireActionCompletionOwner();f.Robot.ActionList.Clear();}
    }
    [Fact]
    public void Unrelated44WithUnknownInitVisibilityStopsOnlyOwnedSearchWithoutGateInversionOrFalseSuccess()
    {
        using var f=new Fixture();f.Add();var parent=new Parent();parent.BindActionCompletionOwner(f.Rig.Robot,f.Robot);
        NativeSearchForBlockHelper? search=null;bool callback=false,delegated=false;parent.Entry=p=>
        {
            var handle=f.Runtime.AI.Helpers.Factory.CreateNativeSearchForBlock(f.M,p,7);search=(NativeSearchForBlockHelper)handle.Pointer;
            try{delegated=p.Delegate(handle,_=>callback=true,_=>callback=true);}finally{handle.Release();}
        };
        var manager=f.Robot.BehaviorManagerOwner!;manager.Add(parent);var missing=new List<string>();SteppedBehavior.ResetMissingForTests();SteppedBehavior.MissingReported+=missing.Add;
        using var delivered=new ManualResetEventSlim();Exception? failure=null;Thread? thread=null;
        try
        {
            Assert.True(manager.StartAsync(parent.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());Assert.True(delegated);var owned=Assert.IsAssignableFrom<CompoundActionSequential>(parent.OwnedRunner);
            var unrelated=new NativeWaitForImagesRunner(f.M,10,(VisionMode)1,0);Assert.Equal(0,f.Robot.ActionList.QueueAction(QueueActionPosition.InParallel,unrelated));
            typeof(CozmoEngine).GetProperty("UiMessageProcessingEnabled",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(f.Rig.Engine,false);
            f.Robot.ActionList.WithGate(()=>
            {
                thread=new Thread(()=>{try{using var frame=f.M.World.EnterPoseMutation();f.Rig.Engine.AnimationMessages.Deliver(0x44,true,new NativeObservedObject(1,2,1,8));}catch(Exception error){failure=error;}finally{delivered.Set();}});
                thread.Start();Assert.True(delivered.Wait(TimeSpan.FromSeconds(5)),"44 callback enqueue must not wait on ActionList gate");
            });
            Assert.True(thread!.Join(TimeSpan.FromSeconds(5)));Assert.Null(failure);Assert.Equal(BehaviorStatus.Running,search!.Status);
            f.Rig.Engine.AnimationMessages.Deliver(0x44,true,new NativeObservedObject(2,2,1,7));Assert.Equal(BehaviorStatus.Running,search.Status); //unknown cannot later become faithful success.
            f.Motors();Assert.True(owned.DestructionObserved);Assert.False(parent.HasCurrentAction);Assert.False(callback);
            Assert.False(unrelated!.DestructionObserved);Assert.Equal(EngineActionResult.Running,unrelated.State);
            Assert.Contains(missing,s=>s.Contains("005BB73C")&&s.Contains("INIT camera snapshot"));Assert.Equal(0,f.Rig.Robot.Motion.LockedTracks);
        }
        finally{if(thread is {IsAlive:true})Assert.True(thread.Join(TimeSpan.FromSeconds(5)));SteppedBehavior.MissingReported-=missing.Add;manager.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);parent.RetireActionCompletionOwner();f.Robot.ActionList.Clear();}
    }
    [Theory][InlineData(true)][InlineData(false)]
    public void SearchConstructorRetains44OnlyForActualExternalInterface(bool external)
    {
        //Retained recipient constructor unit; direct LOCAL44, not a publisher-gate claim.
        using var f=new Fixture();f.Robot.HasExternalInterface=external;var parent=new Parent();
        var held=f.Runtime.AI.Helpers.Factory.CreateNativeSearchForBlock(f.M,parent,7);var search=(NativeSearchForBlockHelper)held.Pointer;
        try
        {
            search.InitializeOnStack();f.Rig.Engine.AnimationMessages.Deliver(0x44,true,new NativeObservedObject(0,2,1,7));
            Assert.Equal(external?BehaviorStatus.Complete:BehaviorStatus.Running,search.Status);
        }
        finally{held.Release();}
        Assert.True(search.Retired);
    }
    [Fact]
    public void MissingInternalFlagDoesNotGuessSearchDelegation()
    {
        using var f=new Fixture();var parent=new Parent();var helper=new NativePickupBlockHelper(f.M,parent,f.Runtime.AI.Helpers.Factory,7,0,false,true);
        helper.InitializeOnStack();var missing=new List<string>();SteppedBehavior.ResetMissingForTests();SteppedBehavior.MissingReported+=missing.Add;
        try{helper.Respond(new RobotCompletedAction(1,0x16,0x0300001D,Array.Empty<uint>(),0));Assert.Null(helper.SearchHelper);Assert.Equal(BehaviorStatus.Running,helper.Status);Assert.Contains(missing,s=>s.Contains("defined internal completion flag unavailable"));}
        finally{SteppedBehavior.MissingReported-=missing.Add;}
    }
    [Fact]
    public void Unavailable44BeforeActionPublicationRetiresNewOwnedSequenceWithoutChildMotionOrRng()
    {
        //Controlled recipient race unit; real offline publisher and configured worker path are separate tests.
        using var f=new Fixture();f.Add();var parent=new Parent();parent.BindActionCompletionOwner(f.Rig.Robot,f.Robot);
        NativeSearchForBlockHelper? search=null;HelperRef? held=null;int draws=0;var rng=f.Rig.Engine.ContextRandom;
        parent.Entry=p=>{var h=f.Runtime.AI.Helpers.Factory.CreateNativeSearchForBlock(f.M,p,7);held=h.Copy();search=(NativeSearchForBlockHelper)h.Pointer;
            search.BeforeActionPublicationForTest=()=>f.Rig.Engine.AnimationMessages.Deliver(0x44,true,new NativeObservedObject(1,2,1,8));
            try{p.Delegate(h,null,null);}finally{h.Release();}};
        var manager=f.Robot.BehaviorManagerOwner!;manager.Add(parent);rng.WordSourceForTest=()=>{draws++;return 0;};
        try
        {
            var unrelated=new NativeTimedWaitRunner(f.M,100);f.Robot.ActionList.QueueAction(QueueActionPosition.InParallel,unrelated);
            Assert.True(manager.StartAsync(parent.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
            var sequence=Assert.IsAssignableFrom<CompoundActionSequential>(parent.OwnedRunner);var nearby=search!.Nearby!;
            f.Rig.Tick();Assert.True(sequence.DestructionObserved);Assert.Equal(0u,parent.CurrentRunnerTag);
            Assert.False(nearby.Initialized);Assert.Equal(0,draws);Assert.Empty(f.Rig.SentSince(0).OfType<AppendPathSegmentLine>());Assert.Empty(f.Rig.SentSince(0).OfType<SetHeadAngle>());
            Assert.Equal(BehaviorStatus.Running,search.Status);Assert.False(unrelated.DestructionObserved);Assert.Equal(EngineActionResult.Running,unrelated.State);
            held!.Release();held=null;Assert.True(search.Retired);
        }
        finally{rng.WordSourceForTest=null;manager.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);held?.Release();parent.RetireActionCompletionOwner();f.Robot.ActionList.Clear();}
    }
    [Fact]
    public void RealOfflineImageProducerCanOverlapNativeHelperTicksAndRetireWithoutLostTargetState()
    {
        //Public offline producer with real rendered markers/ApplyResult/local44; concurrent host scheduling is supplied input.
        using var f=new Fixture();f.Add();var parent=new Parent();parent.BindActionCompletionOwner(f.Rig.Robot,f.Robot);
        HelperRef? held=null;NativeSearchForBlockHelper? search=null;bool success=false;
        parent.Entry=p=>{var h=f.Runtime.AI.Helpers.Factory.CreateNativeSearchForBlock(f.M,p,7);held=h.Copy();search=(NativeSearchForBlockHelper)h.Pointer;try{p.Delegate(h,_=>success=true,null);}finally{h.Release();}};
        var manager=f.Robot.BehaviorManagerOwner!;manager.Add(parent);manager.DisableReactionsWithLock("p5-offline",Enumerable.Range(0,21).Select(i=>i==(int)ReactionTrigger.ObjectPositionUpdated).ToArray(),false);
        Thread? producer=null;Exception? error=null;using var go=new ManualResetEventSlim();
        try
        {
            Assert.True(manager.StartAsync(parent.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
            var firmware=new Firmware(f);for(int i=0;i<60&&search!.Nearby?.Initialized!=true;i++)firmware.Step();Assert.True(search!.Nearby!.Initialized);
            uint stamp=f.Robot.StoredState!.Timestamp;f.Rig.Data(new ImageImuData{ImageId=stamp,RateX=0,RateY=0,RateZ=0});f.Rig.Tick();
            var cal=f.M.Vision.Calibration!;var image=new GrayImage(cal.Columns,cal.Rows);image.Fill(150);
            using var hist=HistRobotState.From(f.Robot.GetCheckedMainPose(),f.Robot.StoredState!);using var camera=f.Robot.CopyHistoricalCameraPose(hist);using var flat=camera.GetWithRespectToRoot();using var cubePose=new Pose3d(Rotation3d.Identity,new Vec3(150,0,22));
            MarkerRenderer.DrawObject(image,MarkerLibrary.EmbeddedOrNull!,new CameraModel(cal,flat),ObjectType.Block_LIGHTCUBE1,cubePose);
            f.M.Vision.Enabled=true;f.M.Vision.ModeEnableMask=1<<1;
            var pd=new VisionPoseData(stamp,hist.Pose,f.Head,f.Lift,false,false);
            producer=new Thread(()=>{go.Wait();try{for(uint i=0;i<8;i++)f.M.Vision.ProcessImage(image,100+i,stamp,pd);}catch(Exception e){error=e;}});
            producer.Start();go.Set();
            for(int i=0;i<40&&!success;i++)firmware.Step();
            Assert.True(producer.Join(TimeSpan.FromSeconds(5)),"offline producer/tick lock watchdog");producer=null;Assert.Null(error);
            for(int i=0;i<10&&!success;i++)firmware.Step();
            Assert.True(success);Assert.Equal(BehaviorStatus.Complete,search.Status);Assert.Equal(0u,parent.CurrentRunnerTag);
            held!.Release();held=null;Assert.True(search.Retired);
        }
        finally
        {
            go.Set();if(producer is not null)Assert.True(producer.Join(TimeSpan.FromSeconds(5)),"join producer before owned state disposal");
            manager.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);held?.Release();parent.RetireActionCompletionOwner();f.Robot.ActionList.Clear();
        }
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void NativeStopCancelsOwnedTagAndPreservesNestedReplacementOnlyWhenCallbackAdmitted(bool keep)
    {
        using var f=new Fixture();var parent=new Parent();parent.BindActionCompletionOwner(f.Rig.Robot,f.Robot);
        NativeTimedWaitRunner? original=null,replacement=null;bool callback=false;
        parent.Entry=p=>{original=new(f.M,100);p.StartActing(original,_=>{callback=true;replacement=new(f.M,100);p.StartActing(replacement,null);});};
        var manager=f.Robot.BehaviorManagerOwner!;manager.Add(parent);
        try
        {
            Assert.True(manager.StartAsync(parent.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
            f.Motors();parent.StopOwned(keep);
            Assert.True(original!.DestructionObserved);Assert.Equal(keep,callback);
            if(keep){Assert.Same(replacement,parent.OwnedRunner);Assert.Equal(replacement!.Tag,parent.CurrentRunnerTag);Assert.False(replacement.DestructionObserved);}
            else{Assert.Equal(0u,parent.CurrentRunnerTag);Assert.Null(replacement);}
        }
        finally{manager.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);parent.RetireActionCompletionOwner();f.Robot.ActionList.Clear();}
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void ShippedPickupMissingMarkerRunsRealPK9SearchThenWorker44RetriesAndCarriesWithOriginalReport(bool interruptRetry)
    {
        //Actual configured factory/manager/helper/ActionList; explicit scene, pre-action pose and simulated firmware.
        using var f=new Fixture();var cube=f.Add();f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        var input=DockPreActionPoses.Get(new PreActionPoseInput(cube,PreActionType.Docking,false,DockActionBase.PreActionAngleToleranceRad,0,false,0),
            f.Robot.GetCheckedMainPose(),null,()=>f.M.GetObstacles(f.Robot.GetCheckedMainPose()));
        Assert.Equal(ActionResult.Success,input.Result);using var goal=input.Poses[input.ClosestIndex].WorldPose.GetWithRespectToRoot();
        f.X=(float)goal.Translation.X;f.Y=(float)goal.Translation.Y;f.Yaw=(float)goal.AngleAroundZ;f.Motors();
        cube.LastObservedTimestamp=f.Robot.StoredState!.Timestamp;foreach(var marker in cube.Markers)marker.LastObservedTimestamp=cube.LastObservedTimestamp;
        f.Robot.Progression!.Unlocked.Add(6);var pickup=Assert.IsType<NativePickUpCubeBehavior>(f.Runtime.Bound["SparksPickupSingleCubeForPyramid"]);
        Assert.True(pickup.IsRunnable(f.Runtime.Context));Assert.True(pickup.CheckExecutionAvailability());
        var local=new List<RobotCompletedAction>();using var sub=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x5A,true,p=>{if(p is RobotCompletedAction value)local.Add(value);});
        var app=new List<ActionCompletionReport>();f.Rig.Engine.ActionCompleted+=app.Add;
        var manager=f.Robot.BehaviorManagerOwner!;var firmware=new Firmware(f);bool dock=false;uint frame=0;var trace=new List<string>();var logs=new List<string>();ActionRunner? interruptedRetry=null;
        f.Rig.Engine.LogEntry+=e=>{logs.Add(e.Name+":"+e.Text);if(interruptRetry&&e.Name==pickup.Id+".SmartStopHelper")interruptedRetry??=pickup.OwnedRunner as ActionRunner;};manager.Log+=logs.Add;
        //Supplied positive fixture input: isolate recovery from the separately tested ObjectPositionUpdated reaction.
        if(!interruptRetry)manager.DisableReactionsWithLock("p5-positive-recovery",Enumerable.Range(0,21).Select(i=>i==(int)ReactionTrigger.ObjectPositionUpdated).ToArray(),false);
        try
        {
            Assert.True(manager.StartAsync(pickup.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
            for(int i=0;i<80&&pickup.Helper is null;i++)firmware.Step();
            var firstSequence=Assert.IsAssignableFrom<CompoundActionSequential>(f.Robot.ActionList.GetCurrentAction());
            var pendingPickup=Assert.Single(firstSequence.Children.OfType<NativePickupObjectRunner>());Assert.False(pendingPickup.Initialized);
            //Explicit perception input changes AFTER real admission/pre-action readiness and BEFORE real IDock.Init.
            foreach(var marker in cube.Markers)marker.LastObservedTimestamp=0;
            for(int i=0;i<140&&pickup.Helper?.SearchHelper?.InitHasRun!=true;i++)firmware.Step();
            var helper=Assert.IsType<NativePickupBlockHelper>(pickup.Helper);var search=Assert.IsType<NativeSearchForBlockHelper>(helper.SearchHelper);
            Assert.Equal(1u,helper.Attempts);Assert.Equal(BehaviorStatus.Running,search.Status);Assert.Equal(1,search.Stage);
            var original=Assert.Single(local.Where(r=>r.ActionType==0x16&&r.Result==0x0300001D));
            Assert.NotNull(original.ObjectInteraction);Assert.False(original.ObjectInteraction!.Flag);Assert.False(original.ObjectInteraction.IsPayloadDefined);
            for(int i=0;i<80&&search.Nearby?.Initialized!=true;i++)firmware.Step();
            Assert.True(search.Nearby!.Initialized);Assert.Equal(7,search.Nearby.Sequence.Children.Count);
            Assert.Equal(EngineActionResult.Running,search.Nearby.Sequence.Children[0].State);
            //Real renderer/worker/world confirmer publishes44 during the actual first owned timed wait, before backup.
            //No fabricated helper result, LOCAL44 success injection or injected5A.
            NativeTimedWaitRunner? unrelated=null;
            if(interruptRetry){unrelated=new(f.M,100);f.Robot.ActionList.QueueAction(QueueActionPosition.InParallel,unrelated);}
            for(int i=0;i<10&&search.Status!=BehaviorStatus.Complete;i++){firmware.Step();f.Image(++frame,true);}
            Assert.Equal(BehaviorStatus.Complete,search.Status);
            if(interruptRetry)
            {
                var retry=Assert.IsAssignableFrom<ActionRunner>(interruptedRetry);uint retryTag=retry.Tag;
                Assert.Equal(2u,helper.Attempts);
                Assert.Contains(logs,l=>l.Contains("SwitchingToReaction: ObjectPositionUpdated"));
                Assert.True(retry.DestructionObserved);Assert.NotEqual(retryTag,pickup.CurrentRunnerTag);
                //Stop the resumed initial reaction before any new pickup starts; old queued recovery must stay retired.
                manager.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);
                for(int i=0;i<10;i++)firmware.Step();
                Assert.Empty(f.Rig.SentSince(0).OfType<DockWithObject>());Assert.Null(f.Robot.CarryingOwner!.CarriedObjectId);
                Assert.False(unrelated!.DestructionObserved);Assert.Equal(EngineActionResult.Running,unrelated.State);
                Assert.DoesNotContain(local,r=>r.Tag==retryTag&&r.Result==0x03000017);
                return;
            }
            for(int i=0;i<220&&ReferenceEquals(manager.Current,pickup);i++)
            {
                firmware.Step();
                var debugSequence=f.Robot.ActionList.GetCurrentAction() as CompoundActionSequential;
                var debugPickup=debugSequence?.Children.OfType<NativePickupObjectRunner>().FirstOrDefault();
                trace.Add("manager="+manager.Current?.Id+" tag="+pickup.CurrentRunnerTag+" helper="+helper.Status+" state="+debugPickup?.State.ToString("X8")+" load="+debugPickup?.LoadState+" children="+string.Join(",",debugPickup?.Prepared?.Children.Select(c=>c.Type.ToString("X")+":"+c.State.ToString("X8"))??Array.Empty<string>())+" carry="+f.Robot.CarryingOwner?.CarriedObjectId+" active="+f.Robot.DockingOwner?.Active+" path="+f.Rig.Robot.Motion.Path.DriveToPoseStatus+" raw="+f.Robot.StoredState?.Timestamp+" seen="+cube.LastObservedTimestamp);
                if(!dock&&f.Rig.SentSince(0).OfType<DockWithObject>().Any())
                {
                    Assert.True(Assert.Single(f.Rig.SentSince(0).OfType<DockWithObject>()).Field8);
                    f.Motors(RobotStatusFlag.IsPickingOrPlacing);f.Rig.Data(new LiftLoad{Field0=true});f.Rig.Data(new PickAndPlaceResult{Field1=true,Field3=2});f.Motors();dock=true;
                }
                if(!dock)
                {
                    var sequence=f.Robot.ActionList.GetCurrentAction() as CompoundActionSequential;
                    var retry=sequence?.Children.OfType<NativePickupObjectRunner>().FirstOrDefault();
                    bool visible=retry?.Prepared?.Children.OfType<NativeTurnTowardsObjectRunner>().Any(c=>c.Initialized)==true;
                    f.Image(++frame,visible);
                }
            }
            f.Rig.Engine.DrainAppDelivery();Assert.True(dock);Assert.Equal(2u,helper.Attempts);Assert.True(helper.Status==BehaviorStatus.Complete,string.Join(" | ",trace.TakeLast(20))+" REPORTS="+string.Join(";",local.Select(r=>$"{r.Tag:X}/{r.ActionType:X}/{r.Result:X}/{r.ObjectInteraction?.Flag}"))+" TRACE="+string.Join(";",pickup.Trace)+" SAMEHELPER="+ReferenceEquals(helper,pickup.Helper)+" LOGS="+string.Join(";",logs));Assert.Equal(BehaviorStatus.Complete,helper.Status);
            Assert.Equal(7u,f.Robot.CarryingOwner!.CarriedObjectId);Assert.False(pickup.HasCurrentAction);
            var success=Assert.Single(local.Where(r=>r.Result==0&&r.ObjectInteraction is {IsPayloadDefined:true}));
            Assert.Equal(7u,success.ObjectInteraction!.Word0);Assert.Contains(app,r=>r.Tag==success.Tag&&r.ObjectInteraction==success.ObjectInteraction);
        }
        finally{manager.RemoveDisableReactionsLock("p5-positive-recovery");manager.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);f.Robot.ActionList.Clear();}
    }

}
