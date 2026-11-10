using Cozmo.Robot;
using Cozmo.Protocol;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Emulation;
using Cozmo.Robot.Vision;
using Xunit;
using Fixture=Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

[Collection("ObjectID process statics")]
public sealed class NativePetReactionTests
{
    private sealed class Gate : ActionRunner
    {
        internal Gate():base(8,0){}
        protected override string ActionName=>"reaction gate";
        public override bool CanInterrupt()=>true;
        public override uint CheckIfDone()=>EngineActionResult.Running;
    }
    private sealed class Detector : IPetDetector
    {
        public bool IsAvailable=>true;
        public bool ProvidesNativeResults=>true;
        public IReadOnlyList<DetectedPet> Result { get; set; }=Array.Empty<DetectedPet>();
        public IReadOnlyList<DetectedPet> Detect(GrayImage image,uint timestamp)=>Result;
    }
    private sealed class Sink : IPlatformPcmSink
    {
        private readonly object _gate=new();private readonly AutoResetEvent _ready=new(false);
        private readonly Queue<ulong> _submitted=new(),_done=new();
        public WaitHandle CompletionReady=>_ready;
        public void Submit(ulong token,byte[] pcm){lock(_gate)_submitted.Enqueue(token);}
        internal void CompleteOne(){lock(_gate)if(_submitted.TryDequeue(out var token)){_done.Enqueue(token);_ready.Set();}}
        public IReadOnlyList<ulong> DrainCompleted(){lock(_gate){var result=_done.ToArray();_done.Clear();return result;}}
        public void Dispose(){lock(_gate)_ready.Dispose();}
    }
    private static DetectedPet Pet(int id,PetType type=PetType.Cat)=>new(id,type,new FaceRect(100,70,60,60)){TrackerResultFlag=true};
    private static void Observe(Fixture f,uint timestamp,params DetectedPet[] pets)
    {
        Assert.True(f.M.Vision.Pets.UpdateNative(pets,timestamp,_=>false,()=>true,(_,_,_)=>{},a=>a()));
    }

    [Fact]
    public void ConfiguredManagerUsesOneOwnerAndSignedTargetsAndStopListener()
    {
        using var f=new Fixture(preloadAnimations:true,sourceNvEmpty:true);
        var manager=f.Robot.BehaviorManagerOwner!;
        var owner=Assert.IsType<NativeReactToPetBehavior>(f.Runtime.Bound["ReactToPet"]);
        var registration=Assert.Single(manager.Reactions.Where(r=>r.Strategy.Trigger==ReactionTrigger.PetInitialDetection));
        Assert.Same(owner,registration.Behavior);
        var strategy=Assert.IsType<PetInitialDetectionStrategy>(registration.Strategy);
        var detector=new Detector { Result=new[]{Pet(8,PetType.Dog),Pet(-3),Pet(5)} };
        f.M.Vision.PetDetector=detector;f.Rig.Robot.SetVisionWhileMoving(true);f.Rig.Tick();
        for(uint id=200;id<203;id++)f.Image(id,modes:1<<9);
        f.Robot.ActionList.Clear();Assert.Equal(0,f.Robot.ActionList.QueueAction(QueueActionPosition.Now,new Gate(),0));
        manager.CheckReactions(f.Rig.Engine.Timer.Seconds);
        Assert.Same(owner,manager.Current);
        Assert.Equal(new[]{-3,5,8},owner.Targets);Assert.Equal(-3,owner.Selected);
        var sequence=Assert.IsType<CompoundActionSequential>(owner.OwnedRunner);
        Assert.Collection(sequence.Children,
            child=>Assert.IsType<NativeImagePointRunner>(child),
            child=>Assert.IsType<TriggerAnimationAction>(child),
            child=>Assert.IsType<NativeTrackAnyPetRunner>(child));
        Assert.False(strategy.RecentlyReacted(f.Rig.Engine.Timer.Seconds));
        manager.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);
        Assert.True(strategy.RecentlyReacted(f.Rig.Engine.Timer.Seconds));
        Assert.Empty(owner.Targets);Assert.Equal(0,owner.Selected);
    }

    [Fact]
    public void NativeForceIncludesAllCurrentIdsAndEmptyRetainsTargets()
    {
        using var f=new Fixture(sourceNvEmpty:true);
        var manager=f.Robot.BehaviorManagerOwner!;
        var registration=Assert.Single(manager.Reactions.Where(r=>r.Strategy.Trigger==ReactionTrigger.PetInitialDetection));
        var strategy=Assert.IsType<PetInitialDetectionStrategy>(registration.Strategy);
        var behavior=Assert.IsType<NativeReactToPetBehavior>(registration.Behavior);
        Observe(f,10,Pet(5),Pet(-4));
        strategy.Forced=true;Assert.True(strategy.ShouldTriggerBehavior(new ReactionContext(f.Runtime.Context,0),behavior));
        Assert.Equal(new[]{-4,5},behavior.Targets);
        Observe(f,11);
        strategy.Forced=true;Assert.True(strategy.ShouldTriggerBehavior(new ReactionContext(f.Runtime.Context,0),behavior));
        Assert.Equal(new[]{-4,5},behavior.Targets);
    }

    [Fact]
    public void ManagerReactionLockAddsPetsAtBothTransitionsWithoutStartingCooldown()
    {
        using var f=new Fixture(sourceNvEmpty:true);
        var manager=f.Robot.BehaviorManagerOwner!;
        var registration=Assert.Single(manager.Reactions.Where(r=>r.Strategy.Trigger==ReactionTrigger.PetInitialDetection));
        var strategy=Assert.IsType<PetInitialDetectionStrategy>(registration.Strategy);
        var behavior=Assert.IsType<NativeReactToPetBehavior>(registration.Behavior);
        var mask=new bool[21];mask[(int)ReactionTrigger.PetInitialDetection]=true;
        Observe(f,10,Pet(1));
        manager.DisableReactionsWithLock("pet-test",mask,stopCurrent:false);
        Observe(f,11,Pet(1),Pet(2));
        manager.RemoveDisableReactionsLock("pet-test");
        Observe(f,12,Pet(1),Pet(2),Pet(3));
        Observe(f,13,Pet(1),Pet(2),Pet(3));
        Observe(f,14,Pet(1),Pet(2),Pet(3));
        Assert.False(strategy.RecentlyReacted(f.Rig.Engine.Timer.Seconds));
        strategy.ShouldTriggerBehavior(new ReactionContext(f.Runtime.Context,f.Rig.Engine.Timer.Seconds),behavior);
        Assert.Equal(new[]{3},behavior.Targets);
    }
    [Fact]
    public void AnyPetTrackerReselectsSignedFirstAndTimesOutStrictly()
    {
        using var f=new Fixture(sourceNvEmpty:true);
        var owner=f.Robot;var tracker=new NativeTrackAnyPetRunner(f.M,owner);
        Assert.Equal(0,owner.ActionList.QueueAction(QueueActionPosition.Now,tracker,0));
        f.Rig.Tick();
        uint timestamp=owner.StoredState!.Timestamp;
        var left=new DetectedPet(-4,PetType.Cat,new FaceRect(80,80,20,20)){TrackerResultFlag=true};
        var right=new DetectedPet(9,PetType.Dog,new FaceRect(240,80,20,20)){TrackerResultFlag=true};
        Observe(f,timestamp,right,left);
        int mark=f.Rig.Mark();
        Assert.Equal(EngineActionResult.Running,tracker.CheckIfDone());
        var body=Assert.Single(f.Rig.SentSince(mark).OfType<Cozmo.Protocol.SetBodyAngle>());
        Assert.True(body.AngleRad>0); // lower signed ID is left of image centre
        Assert.Equal(timestamp,tracker.LastImageTimestamp);
        mark=f.Rig.Mark();Assert.Equal(EngineActionResult.Running,tracker.CheckIfDone());
        Assert.Empty(f.Rig.SentSince(mark).OfType<Cozmo.Protocol.SetBodyAngle>());
        f.Rig.State();timestamp=owner.StoredState!.Timestamp;Observe(f,timestamp,right);
        f.Rig.Engine.Timer.UpdateTime(10_000_000_000L);
        mark=f.Rig.Mark();Assert.Equal(EngineActionResult.Running,tracker.CheckIfDone());
        body=Assert.Single(f.Rig.SentSince(mark).OfType<Cozmo.Protocol.SetBodyAngle>());
        Assert.True(body.AngleRad<0); // absent lower ID is not pinned
        float last=f.Rig.Engine.Timer.SecondsF;
        float deadline=last+3f;
        f.Rig.Engine.Timer.UpdateTime((long)Math.Round((double)deadline*1_000_000_000d));
        Assert.Equal(EngineActionResult.Running,tracker.CheckIfDone());
        f.Rig.Engine.Timer.UpdateTime((long)Math.Round((double)(deadline+.001f)*1_000_000_000d));
        Assert.Equal(EngineActionResult.Success,tracker.CheckIfDone());
        Assert.Equal((byte)3,tracker.GetCompletionVariant());Assert.Equal(0u,tracker.GetCompletionUnion());
        owner.ActionList.Cancel(tracker.Tag);
    }

    [Fact]
    public void FailedImageTimestampIsConsumedAndNotRetried()
    {
        using var f=new Fixture(sourceNvEmpty:true);
        var owner=f.Robot;var tracker=new NativeTrackAnyPetRunner(f.M,owner);
        Assert.Equal(0,owner.ActionList.QueueAction(QueueActionPosition.Now,tracker,0));f.Rig.Tick();
        Observe(f,uint.MaxValue,Pet(1));int mark=f.Rig.Mark();
        Assert.Equal(EngineActionResult.Running,tracker.CheckIfDone());Assert.Equal(uint.MaxValue,tracker.LastImageTimestamp);
        Assert.Equal(EngineActionResult.Running,tracker.CheckIfDone());
        Assert.Empty(f.Rig.SentSince(mark).OfType<Cozmo.Protocol.SetBodyAngle>());
        Assert.Empty(f.Rig.SentSince(mark).OfType<Cozmo.Protocol.SetHeadAngle>());
        owner.ActionList.Cancel(tracker.Tag);
    }

    [Fact]
    public void ConfiguredReactionPlaysRealAuthoredAudioThenTracksPet()
    {
        if(!OperatingSystem.IsWindows() || ShippedLibraries.FindLibDir() is null)return;
        string banks=GraphRig.FindMeta() ?? throw new InvalidOperationException("shipped banks missing");
        string archive=Path.Combine(WwiseAssets.SoundDir ?? throw new InvalidOperationException("audio archive missing"),"AudioAssets.zip");
        var options=new CozmoEngineOptions { ResourcesPath=PickupPyramidNativeExecutionTests.Resources(),
            SpeechSoundBankDirectory=banks,SpeechAudioArchivePath=archive,LabLocaleLanguage="en",BlockPoolPath="" };
        using var f=new Fixture(preloadAnimations:true,pumpAnimations:true,sourceNvEmpty:true,engineOptions:options);
        var sink=new Sink();f.Robot.CannedSpeechSinkForTest=()=>sink;
        f.Rig.DefaultTickMs=0;
        var detector=new Detector {Result=new[]{Pet(7)}};
        f.M.Vision.PetDetector=detector;f.Rig.Robot.SetVisionWhileMoving(true);f.Rig.Tick(20);
        for(uint id=400;id<403;id++){f.Rig.Tick(20);f.Image(id,modes:1<<9);}
        var owner=Assert.IsType<NativeReactToPetBehavior>(f.Runtime.Bound["ReactToPet"]);
        var manager=f.Robot.BehaviorManagerOwner!;
        f.Robot.ActionList.Clear();Assert.Equal(0,f.Robot.ActionList.QueueAction(QueueActionPosition.Now,new Gate(),0));
        manager.CheckReactions(f.Rig.Engine.Timer.Seconds);Assert.Same(owner,manager.Current);
        var sequence=Assert.IsType<CompoundActionSequential>(owner.OwnedRunner);
        var animation=Assert.IsType<TriggerAnimationAction>(sequence.Children[1]);
        var tracker=Assert.IsType<NativeTrackAnyPetRunner>(sequence.Children[2]);
        int mark=f.Rig.Mark();byte? tag=null;int ownedStart=-1;int ownedEnd=-1;bool ended=false;uint imageId=403;
        var until=DateTime.UtcNow+TimeSpan.FromSeconds(20);
        while(DateTime.UtcNow<until && tracker.LastImageTimestamp==0 && owner.EngineRunning)
        {
            sink.CompleteOne();
            var stream=f.Rig.Robot.Animations.Scheduler.Stream;
            f.Rig.Data(new AnimationState {NumAudioFramesPlayed=stream.FramesStreamed,NumAnimBytesPlayed=stream.BytesStreamed});
            f.Rig.Tick(20);f.Motors(RobotStatusFlag.IsBodyAccMode);
            f.Image(imageId++,modes:1<<9);
            var sent=f.Rig.SentSince(mark);
            if(tag is null && animation.AnimationTagForTest!=0)
            {
                ownedStart=Array.FindIndex(sent.ToArray(),m=>m is StartOfAnimation s && s.AnimId==animation.AnimationTagForTest);
                if(ownedStart>=0){tag=animation.AnimationTagForTest;f.Rig.Data(new AnimationStarted {AnimId=tag.Value});}
            }
            if(tag is not null && !ended)
            {
                ownedEnd=Array.FindIndex(sent.ToArray(),ownedStart+1,m=>m is EndOfAnimation);
                if(ownedEnd>ownedStart && sent.Skip(ownedStart+1).Take(ownedEnd-ownedStart-1)
                    .OfType<AudioSample>().Any(sample=>sample.Samples.Any(b=>b!=0xFF && b!=0x7F)))
                {f.Rig.Data(new AnimationEnded {AnimId=tag.Value});f.Rig.Tick();ended=true;}
            }
            Thread.Sleep(20);
        }
        var messages=f.Rig.SentSince(mark);
        Assert.True(ended,$"tag={tag}, owner={owner.EngineRunning}, tracker={tracker.State:X}, sent={messages.Count}");
        Assert.NotEqual(0u,tracker.LastImageTimestamp);
        Assert.Contains(messages.Skip(ownedStart+1).Take(ownedEnd-ownedStart-1).OfType<AudioSample>(),
            sample=>sample.Samples.Any(b=>b!=0xFF && b!=0x7F));
        Assert.Contains("petdetection",animation.GetAnimationCompletionName(),StringComparison.OrdinalIgnoreCase);
        manager.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);
    }
}
