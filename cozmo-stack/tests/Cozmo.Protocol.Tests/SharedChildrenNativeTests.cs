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
public sealed class SharedChildrenNativeTests
{
    // Supplied completion producer unit, not an animation/hardware producer claim.
    private sealed class Payload:ActionRunner
    {
        internal uint Result;
        internal string? Name;
        internal ObjectInteractionCompletion? Interaction;
        internal byte? Variant;
        internal Payload():base(0x1A,0){}
        public override bool CanInterrupt()=>false;
        public override uint CheckIfDone()=>Result;
        public override string? GetAnimationCompletionName()=>Name;
        public override byte? GetCompletionVariant()=>Variant;
        public override ObjectInteractionCompletion? GetObjectInteractionCompletion()=>Interaction;
        public override void WatcherEnding(){Name="retired";Interaction=null;Variant=6;base.WatcherEnding();}
    }
    [Theory][InlineData(false,false)][InlineData(true,false)][InlineData(false,true)][InlineData(true,true)]
    public void GenericWrapperEagerlyBuildsIndependentFlagsWithCopiedMaximumAndSayName(bool before,bool after)
    {
        using var f=new Fixture();var payload=new Payload{Name="authored",Variant=1};var maximum=new Radians(.7f);
        var wrapper=new NativeTurnTowardsFaceWrapperRunner(f.M,payload,before,after,maximum,true);
        try
        {
            Assert.Equal(1+(before?1:0)+(after?1:0),wrapper.Children.Count);Assert.Same(payload,wrapper.Children[before?1:0]);
            var turns=wrapper.Children.OfType<NativeLastFaceRunner>().ToArray();Assert.Equal((before?1:0)+(after?1:0),turns.Length);
            Assert.All(turns,t=>{Assert.True(t.SayName);Assert.Equal(BitConverter.SingleToUInt32Bits(maximum.Value),BitConverter.SingleToUInt32Bits(t.MaximumTurn));Assert.Equal(0,t.RequestedId);});
            if(turns.Length==2)Assert.NotEqual(turns[0].Tag,turns[1].Tag);
            for(int i=0;i<5&&wrapper.State!=0;i++)wrapper.Update();
            Assert.Equal(0u,wrapper.State);Assert.True(payload.DestructionObserved);Assert.Equal("authored",wrapper.GetAnimationCompletionName());Assert.Equal((byte)1,wrapper.GetCompletionVariant());
            Assert.All(turns,t=>Assert.True(t.DestructionObserved));
        }
        finally{wrapper.Prep();wrapper.WatcherEnding();}
    }
    [Theory][InlineData(true)][InlineData(false)]
    public void BeforeOrAfterFailureIsNotIgnoredAndWrappedFullProxySurvivesRetirement(bool beforeFailure)
    {
        using var f=new Fixture();var interaction=new ObjectInteractionCompletion(7,8,9,10,11,5,true);var payload=new Payload{Interaction=interaction,Variant=0};
        var wrapper=new NativeTurnTowardsFaceWrapperRunner(f.M,payload,true,true,new Radians(Radians.Pi),false);
        var failing=(NativeLastFaceRunner)wrapper.Children[beforeFailure?0:2];failing.State=0x03000002; // Supplied stored-result compound-recipient input.
        try
        {
            for(int i=0;i<4&&(wrapper.State==EngineActionResult.NotStarted||wrapper.State==EngineActionResult.Running);i++)wrapper.Update();
            Assert.Equal(0x03000002u,wrapper.State);Assert.Equal(interaction,wrapper.GetObjectInteractionCompletion());Assert.Equal((byte)0,wrapper.GetCompletionVariant());
            if(beforeFailure)Assert.Equal(EngineActionResult.NotStarted,payload.State);else Assert.True(payload.DestructionObserved);
        }
        finally{wrapper.Prep();wrapper.WatcherEnding();}
    }
    private sealed class EmptyFaces:IFaceDetector
    {
        public bool IsAvailable=>true;public string Description=>"supplied empty detector at proprietary boundary";
        public IReadOnlyList<DetectedFace> Detect(GrayImage image,uint timestamp)=>Array.Empty<DetectedFace>();
    }
    private static void Face(Fixture f,bool named)
    {
        for(uint stamp=1;stamp<=4;stamp++)
        {
            var tracked=new TrackedFace(new DetectedFace(11,new FaceRect(0,0,10,10),new Vec2(3,4),new Vec2(7,4),Name:named?"Ada":null),stamp)
            {HeadPose=new Pose3d(Rotation3d.Identity,new Vec3(100,0,60),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId))};
            f.M.Vision.Faces.AddOrUpdateFace(tracked,f.Robot.GetCheckedMainPose(),false);
        }
    }
    [Fact]
    public void QueuedRealAnimationProxyRetainsNameThroughDeletedWrappedActionAndActiveAfterFace()
    {
        using var f=new Fixture();Face(f,false);f.M.Vision.FaceDetector=new EmptyFaces();f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        var animation=new TriggerAnimationAction(f.Rig.Robot,new(AnimationTrigger.PutDownBlockKeepAlive,1,true,0,60f,false));
        var wrapper=new NativeTurnTowardsFaceWrapperRunner(f.M,animation,false,true,new Radians(Radians.Pi),false);
        var after=Assert.IsType<NativeLastFaceRunner>(wrapper.Children[1]);var local=new List<RobotCompletedAction>();var typed=new List<ActionCompletionReport>();var raw=new List<byte[]>();
        using var sub=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x5A,true,p=>{if(p is RobotCompletedAction r)local.Add(r);});f.Rig.Engine.ActionCompleted+=typed.Add;
        f.Rig.Engine.EngineToGameMessage+=(tag,body)=>{if(tag==0x5A)raw.Add(body.ToArray());};var firmware=new Firmware(f);string? selected=null;
        f.Robot.ActionList.QueueAction(QueueActionPosition.Now,wrapper);
        try
        {
            // Capture actual live selection before real CA/CB lets the streamer/action retire it.
            f.Motors();selected=animation.GetAnimationCompletionName();Assert.False(string.IsNullOrEmpty(selected));
            for(int i=0;i<40&&!animation.DestructionObserved;i++)firmware.Step();
            Assert.True(animation.DestructionObserved);Assert.False(after.DestructionObserved);Assert.Equal(EngineActionResult.Running,wrapper.State);
            Assert.Equal(selected,wrapper.GetAnimationCompletionName());Assert.Equal((byte)1,wrapper.GetCompletionVariant());
            for(uint i=1;i<=30&&!wrapper.DestructionObserved;i++){firmware.Step();f.Image(i,false,modes:1<<2);}
            Assert.True(wrapper.DestructionObserved);Assert.Equal(0u,wrapper.State);f.Rig.Engine.DrainAppDelivery();
            var completed=Assert.Single(local.Where(r=>r.Tag==wrapper.Tag));Assert.Equal(selected,completed.AnimationName);Assert.Equal((byte)1,completed.CompletionVariant);
            Assert.Equal(selected,Assert.Single(typed.Where(r=>r.Tag==wrapper.Tag)).AnimationName);
            var body=Assert.Single(raw.Where(b=>System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(b)==wrapper.Tag));int offset=13+4*body[12];
            Assert.Equal((byte)1,body[offset]);int length=body[offset+1];Assert.Equal(selected,System.Text.Encoding.UTF8.GetString(body,offset+2,length));Assert.Equal(offset+2+length,body.Length);
        }
        finally{f.Robot.ActionList.Clear();}
    }
    [Fact]
    public void QueuedActualPickupProxyPublishesRealCarryPayloadAfterWrappedRetirement()
    {
        // Actual native pickup producer, real worker44/DW/load/C5/Verify; supplied scene/firmware, no fake completion.
        using var f=new Fixture();f.Add();var pickup=new NativePickupObjectRunner(f.M,7);
        var wrapper=new NativeTurnTowardsFaceWrapperRunner(f.M,pickup,false,true,new Radians(Radians.Pi),false);
        var reports=new List<RobotCompletedAction>();var typed=new List<ActionCompletionReport>();var raw=new List<byte[]>();
        using var sub=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x5A,true,p=>{if(p is RobotCompletedAction r)reports.Add(r);});f.Rig.Engine.ActionCompleted+=typed.Add;
        f.Rig.Engine.EngineToGameMessage+=(tag,body)=>{if(tag==0x5A)raw.Add(body.ToArray());};bool dock=false;uint image=0;
        f.Robot.ActionList.QueueAction(QueueActionPosition.Now,wrapper);
        try
        {
            for(int i=0;i<100&&!wrapper.DestructionObserved;i++)
            {
                f.Motors();
                if(!dock&&f.Rig.SentSince(0).OfType<DockWithObject>().Any())
                {Assert.True(Assert.Single(f.Rig.SentSince(0).OfType<DockWithObject>()).Field8);f.Motors(RobotStatusFlag.IsPickingOrPlacing);f.Rig.Data(new LiftLoad{Field0=true});f.Rig.Data(new PickAndPlaceResult{Field1=true,Field3=2});f.Motors();dock=true;}
                if(!dock)f.Image(++image,pickup.Prepared?.Children.OfType<NativeTurnTowardsObjectRunner>().Any(c=>c.Initialized)==true);
            }
            Assert.True(dock);Assert.True(pickup.DestructionObserved);Assert.True(wrapper.DestructionObserved);Assert.Equal(0u,wrapper.State);Assert.Equal(7u,f.Robot.CarryingOwner!.CarriedObjectId);
            f.Rig.Engine.DrainAppDelivery();var result=Assert.Single(reports.Where(r=>r.Tag==wrapper.Tag));var payload=Assert.IsType<ObjectInteractionCompletion>(result.ObjectInteraction);Assert.Equal((byte)0,result.CompletionVariant);Assert.Equal((byte)0,wrapper.GetCompletionVariant());
            Assert.Equal(new ObjectInteractionCompletion(7,uint.MaxValue,uint.MaxValue,uint.MaxValue,uint.MaxValue,1,false),payload);
            Assert.Equal(payload,Assert.Single(typed.Where(r=>r.Tag==wrapper.Tag)).ObjectInteraction);
            var body=Assert.Single(raw.Where(b=>System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(b)==wrapper.Tag));int offset=13+4*body[12];Assert.Equal((byte)0,body[offset]);
            Assert.Equal(23,body.Length-offset);Assert.Equal(payload.PackBody(),body.AsSpan(offset+1).ToArray());
        }
        finally{f.Robot.ActionList.Clear();}
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void TrueSayNameUsesActualVerifiedUnnamedPathOrFailsAtNamedProviderFrontier(bool named)
    {
        // Native face recipient unit: real motor acknowledgements and supplied LOCAL46 observation input.
        using var f=new Fixture();Face(f,named);var payload=new Payload();var wrapper=new NativeTurnTowardsFaceWrapperRunner(f.M,payload,true,false,new Radians(Radians.Pi),true);
        var face=Assert.IsType<NativeLastFaceRunner>(wrapper.Children[0]);NativeActionUnavailable? unavailable=null;bool observed=false,verified=false;var trace=new List<string>();
        try
        {
            for(int i=0;i<50&&wrapper.State!=0;i++)
            {
                trace.Add($"wrapper={wrapper.State:X8},face={face.FaceState},init={face.Initialized},verified={face.VerifiedId},destroyed={face.DestructionObserved}");
                try{wrapper.Update();}catch(NativeActionUnavailable error){unavailable=error;break;}
                if(face.Initialized&&!observed){f.Rig.Engine.AnimationMessages.Deliver(0x46,true,new NativeObservedFace(11,4));observed=true;verified=face.VerifiedId==11;}
                f.Motors();
            }
            Assert.True(observed);Assert.True(verified,string.Join(" | ",trace));
            if(named){Assert.NotNull(unavailable);Assert.Contains("M14-006",unavailable!.ToString());Assert.Contains("intent3",unavailable.ToString());Assert.Equal(EngineActionResult.NotStarted,payload.State);}
            else{Assert.Null(unavailable);Assert.Equal(0u,wrapper.State);Assert.True(payload.DestructionObserved);}
        }
        finally{wrapper.Prep();wrapper.WatcherEnding();}
    }
    private sealed class Firmware(Fixture f)
    {
        private int _mark;private ExecutePath? _path;private readonly HashSet<byte> _animations=new();
        internal void Step()
        {
            f.Motors();
            if(_path is {} done){f.Rig.Data(new PathFollowingEvent{EventId=done.EventId,EventType=(byte)PathEventType.Completed});_path=null;f.Motors();}
            var sent=f.Rig.SentSince(_mark);_mark=f.Rig.Mark();
            foreach(var item in sent)
            {
                if(item is AppendPathSegmentLine line){f.X=line.XEndMm;f.Y=line.YEndMm;}
                if(item is ExecutePath path){f.Rig.Data(new PathFollowingEvent{EventId=path.EventId,EventType=(byte)PathEventType.Started});_path=path;f.Motors();}
            }
            byte tag=f.Rig.Robot.Animations.CurrentTag;
            if(tag!=0&&_animations.Add(tag)){f.Rig.Data(new AnimationStarted{AnimId=tag});f.Rig.Data(new AnimationEnded{AnimId=tag});f.Rig.Tick();}
        }
    }
    private static void Carry(Fixture f)
    {
        var obj=new ObservableObject(7,ObjectType.Block_LIGHTCUBE1,CubeGeometry.MarkersFor(ObjectType.Block_LIGHTCUBE1))
        {Pose=new Pose3d(Rotation3d.Identity,new Vec3(0,0,22),parent:f.Robot.LiftPose),OriginId=f.Robot.CurrentOriginId,PoseState=PoseState.Known,NativeCarryingTree=true};
        f.M.World.AddLocatedObject(obj);using var relative=new Pose3d("");f.M.Docking.Carrying.AttachToLift(7,obj.Markers[0],relative,null,null);
    }
    [Fact]
    public void PublicPutDownUsesRealQueueWaitsForTwoImagesAfterParallelAndReleasesAfterActualKeepAlive()
    {
        // Actual public Start→owned initial/look/ActionList→CA/CB and real worker43; supplied firmware/scene, not hardware.
        using var f=new Fixture();Carry(f);f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        using var behavior=new PutDownBlockBehavior(f.M,"public-putdown");var manager=f.Robot.BehaviorManagerOwner!;manager.Add(behavior);var firmware=new Firmware(f);
        var reports=new List<RobotCompletedAction>();using var sub=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x5A,true,p=>{if(p is RobotCompletedAction r)reports.Add(r);});
        try
        {
            Assert.True(manager.StartAsync(behavior.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());Assert.InRange(behavior.BackUpMm,-75,-45);
            for(int i=0;i<100&&behavior.CurrentPhase!=PutDownBlockBehavior.Phase.LookingDown;i++)firmware.Step();
            Assert.Equal(PutDownBlockBehavior.Phase.LookingDown,behavior.CurrentPhase);
            var look=Assert.IsAssignableFrom<CompoundActionSequential>(behavior.OwnedRunner);var wait=Assert.Single(look.Children.OfType<NativeWaitForImagesRunner>());
            f.Image(1);f.Image(2);Assert.False(wait.Initialized);Assert.Equal(0,wait.Count); // Earlier real processed images cannot satisfy the later subscription.
            for(int i=0;i<100&&!wait.Initialized;i++)firmware.Step();Assert.True(wait.Initialized);Assert.Equal(0,wait.Count);
            f.Image(3);Assert.Equal(1,wait.Count);Assert.True(f.M.Docking.Carrying.IsCarryingObject);Assert.DoesNotContain(behavior.Trace,l=>l.Contains("KeepAlive"));
            f.Image(4);Assert.Equal(2,wait.Count);Assert.True(f.M.Docking.Carrying.IsCarryingObject);
            for(int i=0;i<80&&ReferenceEquals(manager.Current,behavior);i++)firmware.Step();
            Assert.Null(manager.Current);Assert.Equal(PutDownBlockBehavior.Phase.Idle,behavior.CurrentPhase);Assert.False(f.M.Docking.Carrying.IsCarryingObject);
            Assert.Equal(2,f.Rig.SentSince(0).OfType<ExecutePath>().Count());Assert.Equal(-80f,f.Rig.SentSince(0).OfType<AppendPathSegmentLine>().Last().Speed.SpeedMmps);
            Assert.Contains(behavior.Trace,l=>l.Contains("PutDownBlockPutDown"));Assert.Contains(behavior.Trace,l=>l.Contains("PutDownBlockKeepAlive"));
            Assert.Contains(reports,r=>r.Tag==look.Tag&&r.Result==0&&r.CompletionVariant==6);
        }
        finally{manager.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);f.Robot.ActionList.Clear();}
    }
    [Fact]
    public void ReentrantReleaseObserverCanQueueReplacementWithoutLosingPublicOwnerLease()
    {
        // Supplied LOCAL5A callback unit, separately from real public queue/worker execution above.
        using var f=new Fixture();Carry(f);using var behavior=new PutDownBlockBehavior(f.M,"public-reentry");var manager=f.Robot.BehaviorManagerOwner!;manager.Add(behavior);
        Assert.True(manager.StartAsync(behavior.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        f.Rig.Engine.AnimationMessages.Deliver(0x5A,true,new RobotCompletedAction(behavior.CurrentRunnerTag,0,0x03000018,Array.Empty<uint>(),0));
        uint look=behavior.CurrentRunnerTag;NativeTimedWaitRunner? replacement=null;
        f.Rig.Engine.LogEntry+=entry=>{if(entry.Name=="BehaviorPutDownBlock.LookDownAtBlock.DidNotSeeBlock"){replacement=new(f.M,100);behavior.StartActing(replacement,null);}};
        int bindings=f.Rig.Engine.AnimationMessages.Count(0x5A,true);
        f.Rig.Engine.AnimationMessages.Deliver(0x5A,true,new RobotCompletedAction(look,0,0x03000018,Array.Empty<uint>(),0));
        Assert.NotNull(replacement);Assert.Equal(replacement!.Tag,behavior.CurrentRunnerTag);Assert.True(behavior.Update(f.Runtime.Context,f.Rig.Engine.Timer.SecondsF));
        Assert.Equal(bindings,f.Rig.Engine.AnimationMessages.Count(0x5A,true));Assert.False(f.M.Docking.Carrying.IsCarryingObject);
        manager.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);Assert.True(replacement.DestructionObserved);
    }
    [Fact]
    public void DisposedCantCannotStartUpstreamHeadWaitsOrAcquireCompletionLease()
    {
        using var f=new Fixture();using var behavior=new CantHandleTallStackBehavior(f.M);
        int before=f.Rig.Engine.AnimationMessages.Count(0x5A,true);behavior.Dispose();
        Assert.Throws<ObjectDisposedException>(()=>behavior.StartAsync(f.Runtime.Context,new BehaviorScope(),default).GetAwaiter().GetResult());
        Assert.Equal(CantHandleTallStackBehavior.Phase.Idle,behavior.CurrentPhase);
        Assert.Equal(before,f.Rig.Engine.AnimationMessages.Count(0x5A,true));Assert.False(behavior.HasCurrentAction);
    }
    [Fact]
    public void CompositeOwnerRetainsChildLeaseThroughStopReuseAndDisposesIt()
    {
        // Owner-lifetime unit: supplied already-carried scene makes the first child end without a target.
        // Direct public Start is used here; no claim about the composite's runnable/selection predicate.
        using var f=new Fixture();Carry(f);f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);using var composite=new PickUpAndPutDownCubeBehavior(f.M);
        int before=f.Rig.Engine.AnimationMessages.Count(0x5A,true);
        for(int run=0;run<2;run++)
        {
            composite.StartAsync(f.Runtime.Context,new BehaviorScope(),default).GetAwaiter().GetResult();
            Assert.True(composite.Update(f.Runtime.Context,f.Rig.Engine.Timer.SecondsF));
            var child=Assert.IsType<PutDownBlockBehavior>(composite.Running);Assert.NotEqual(0u,child.CurrentRunnerTag);
            Assert.Equal(before+1,f.Rig.Engine.AnimationMessages.Count(0x5A,true));
            composite.Stop(BehaviorStopReason.Interrupted);Assert.Null(composite.Running);
            Assert.Equal(0u,child.CurrentRunnerTag);Assert.Equal(before+1,f.Rig.Engine.AnimationMessages.Count(0x5A,true));
        }
        composite.Dispose();composite.Dispose();Assert.Equal(before,f.Rig.Engine.AnimationMessages.Count(0x5A,true));
        Assert.Throws<ObjectDisposedException>(()=>composite.StartAsync(f.Runtime.Context,new BehaviorScope(),default).GetAwaiter().GetResult());
        f.Motors(); // Cancel marks the queued action; its normal engine tick performs retirement.
        Assert.True(f.Robot.ActionList.IsEmpty);
    }
    [Fact]
    public void PublicLeaseSurvivesStopReuseAndDisposeReleasesOnlyItsOwnBinding()
    {
        using var f=new Fixture();Carry(f);using var behavior=new PutDownBlockBehavior(f.M,"public-lease");var manager=f.Robot.BehaviorManagerOwner!;manager.Add(behavior);
        int before=f.Rig.Engine.AnimationMessages.Count(0x5A,true);
        Assert.True(manager.StartAsync(behavior.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());uint old=behavior.CurrentRunnerTag;
        Assert.Equal(before+1,f.Rig.Engine.AnimationMessages.Count(0x5A,true));manager.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);
        Assert.Equal(before+1,f.Rig.Engine.AnimationMessages.Count(0x5A,true));Assert.True(f.M.Docking.Carrying.IsCarryingObject);
        Assert.True(manager.StartAsync(behavior.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());uint current=behavior.CurrentRunnerTag;Assert.NotEqual(old,current);
        f.Rig.Engine.AnimationMessages.Deliver(0x5A,true,new RobotCompletedAction(old,0,0,Array.Empty<uint>(),0));Assert.Equal(current,behavior.CurrentRunnerTag);
        behavior.Dispose();Assert.Equal(before,f.Rig.Engine.AnimationMessages.Count(0x5A,true));Assert.Equal(0u,behavior.CurrentRunnerTag);Assert.True(f.M.Docking.Carrying.IsCarryingObject);
        using var prebound=new PutDownBlockBehavior(f.M,"prebound");prebound.BindActionCompletionOwner(f.Rig.Robot,f.Robot);int bound=f.Rig.Engine.AnimationMessages.Count(0x5A,true);
        manager.Add(prebound);Assert.True(manager.StartAsync(prebound.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());prebound.Dispose();Assert.Equal(bound,f.Rig.Engine.AnimationMessages.Count(0x5A,true));prebound.RetireActionCompletionOwner();
        manager.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);
    }
}
