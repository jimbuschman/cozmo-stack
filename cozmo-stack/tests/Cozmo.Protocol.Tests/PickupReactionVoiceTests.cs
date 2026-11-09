using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Emulation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class PickupReactionVoiceTests
{
    private sealed class Sink : IPlatformPcmSink
    {
        private readonly object _gate=new();
        private readonly AutoResetEvent _ready=new(false);
        private readonly Queue<ulong> _submitted=new(),_done=new();
        public WaitHandle CompletionReady=>_ready;
        public void Submit(ulong token,byte[] pcm){lock(_gate)_submitted.Enqueue(token);}
        internal void CompleteOne(){lock(_gate)if(_submitted.TryDequeue(out var token)){_done.Enqueue(token);_ready.Set();}}
        public IReadOnlyList<ulong> DrainCompleted(){lock(_gate){var result=_done.ToArray();_done.Clear();return result;}}
        public void Dispose(){lock(_gate)_ready.Dispose();}
    }
    private sealed class FaceDetector : IFaceDetector
    {
        public bool IsAvailable=>true;
        public string Description=>"supplied native face";
        public IReadOnlyList<DetectedFace> Detect(GrayImage image,uint timestamp)=>
            new[]{new DetectedFace(7,new FaceRect(100,70,90,90),new Vec2(120,110),new Vec2(160,110),RollRad:0)
                {NativeNewEntryRotatingTooFast=false}};
    }
    private sealed class Gate : ActionRunner
    {
        internal Gate():base(8,0){}
        protected override string ActionName=>"reaction gate";
        public override bool CanInterrupt()=>false;
        public override uint CheckIfDone()=>0;
    }

    [Fact]
    public async Task NativeReactionWithoutManipulationDoesNotUseStandaloneFallback()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty:true);
        var behavior=new ReactToPickupBehavior(f.M.Vision);
        behavior.MarkNative(f.Rig.Robot,f.Robot,f.Robot.BehaviorManagerOwner!);
        var missing=new List<string>();
        void Capture(string line){if(line.Contains("original manipulation owner unavailable"))missing.Add(line);}
        SteppedBehavior.MissingReported+=Capture;
        try
        {
            await behavior.StartAsync(f.Runtime.Context,new BehaviorScope(),default);
            Assert.False(behavior.EngineRunning);
            Assert.Null(behavior.OwnedRunner);
            Assert.Single(missing);
        }
        finally {SteppedBehavior.MissingReported-=Capture;behavior.RetireNativeBinding();}
    }

    [Theory]
    [InlineData(false,0,false,false,false,false)]
    [InlineData(true,0,false,false,false,false)]
    [InlineData(true,1,false,false,false,false)]
    [InlineData(true,2,false,false,false,false)]
    [InlineData(true,0,true,false,false,false)]
    [InlineData(true,0,false,true,false,false)]
    [InlineData(true,0,false,false,true,false)]
    [InlineData(false,0,false,false,false,true)]
    public void ActualPickupReactionSelectsNamedOrUnnamedAfterRetainedWait(bool named,int spark,bool stale,bool carrying,bool stopWaiting,bool stopOnLog)
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations:true,sourceNvEmpty:true);
        var owner=f.Robot;var manager=owner.BehaviorManagerOwner!;
        var reaction=Assert.Single(manager.Reactions.Where(r=>r.Strategy.Trigger==ReactionTrigger.RobotPickedUp));
        var behavior=Assert.IsType<ReactToPickupBehavior>(reaction.Behavior);
        Assert.True(behavior.NativeBound);
        var mask=Enumerable.Range(0,21).Select(i=>i==(int)ReactionTrigger.RobotPickedUp || i==(int)ReactionTrigger.FacePositionUpdated).ToArray();
        manager.DisableReactionsWithLock("pickup-face-fixture",mask,false);
        owner.ActionList.Clear();owner.ActionList.QueueAction(QueueActionPosition.Now,new Gate(),0);
        manager.CheckReactions(f.Rig.Engine.Timer.Seconds);owner.ActionList.Clear();
        f.Rig.Data(new MotorCalibration{MotorID=MotorID.MOTOR_HEAD,CalibStarted=false,AutoStarted=false});
        for(int i=0;i<10;i++)f.Motors(RobotStatusFlag.IsPickedUp);
        Assert.Equal(OffTreadsState.InAir,f.Rig.Robot.Sensors.OffTreadsState);
        f.M.Vision.FaceDetector=new FaceDetector();f.Head=.1f;f.Motors(RobotStatusFlag.IsPickedUp);f.Image(77,modes:1<<2);
        uint stamp=f.Rig.Robot.CameraSettings.LastProcessedImageTimestamp;
        Assert.NotNull(f.M.Vision.Faces.GetNativeFace(owner,7));
        void Add(int id,string? name,uint at)
        {
            var face=new TrackedFace(new DetectedFace(id,new FaceRect(0,0,20,20),Name:name),at)
                {HeadPose=new Pose3d(Rotation3d.Identity,new Vec3(10,0,50))};
            f.M.Vision.Faces.AddOrUpdateNativeFace(face,Pose3d.Identity,false,owner.GetNativeOrigin(owner.CurrentOriginId).Node!);
        }
        if(stale)
        {
            Add(-2,"Ada",stamp);
            owner.Delocalize(false);
            f.Motors(RobotStatusFlag.IsPickedUp);f.Image(78,modes:1<<2);
            stamp=f.Rig.Robot.CameraSettings.LastProcessedImageTimestamp;
        }
        Add(-3,null,stamp);
        if(named && !stale)
        {
            uint edge=stamp>500?stamp-500:0;
            if(edge>0)Add(-4,"Expired",edge-1);
            Add(-2,"Ada",edge);Add(2,"Bob",stamp);
        }
        if(carrying){f.Add(9);owner.CarryingOwner!.SetCarrying(9);}
        int words=0;bool carriedAtDraw=false,stoppedByLog=false;
        void OnLog(EngineLogEntry entry)
        {
            if(!stopOnLog || stoppedByLog || entry.Name!="GetResponseForAnimationTrigger.Found")return;
            stoppedByLog=true;manager.Stop(BehaviorStopReason.Interrupted,owner.Engine.Timer.SecondsF);
        }
        owner.Engine.LogEntry+=OnLog;
        owner.Engine.ContextRandom.WordSourceForTest=()=>{words++;carriedAtDraw|=owner.CarryingOwner?.CarriedObjectId is not null;return 0;};
        try
        {
            manager.RemoveDisableReactionsLock("pickup-face-fixture");
            manager.CheckReactions(owner.Engine.Timer.Seconds);
            Assert.Same(behavior,manager.Current);
            Assert.Equal(ReactionTrigger.RobotPickedUp,manager.CurrentReactionTrigger);
            behavior.Update(f.Runtime.Context,owner.Engine.Timer.SecondsF);
            var wait=Assert.IsType<Cozmo.Robot.Manipulation.NativeTimedWaitRunner>(behavior.OwnedRunner);
            if(stopWaiting)
            {
                manager.DisableReactionsWithLock("pickup-stop-fixture",mask,false);
                manager.Stop(BehaviorStopReason.Interrupted,owner.Engine.Timer.SecondsF);
                for(int i=0;i<15;i++)f.Motors(RobotStatusFlag.IsPickedUp);
                Assert.True(wait.DestructionObserved);Assert.Null(behavior.OwnedRunner);Assert.Equal(0,words);
                return;
            }
            if(spark!=0)
            {
                manager.SparkNameInput=_=>"test";
                manager.SetNativeRequestedSpark(4,(byte)(spark==2?1:0));
                manager.SwitchToNativeRequestedSpark();
            }
            for(int i=0;i<20 && !stoppedByLog && behavior.OwnedRunner is Cozmo.Robot.Manipulation.NativeTimedWaitRunner;i++)f.Motors(RobotStatusFlag.IsPickedUp);
            if(stopOnLog){Assert.True(stoppedByLog);Assert.False(behavior.EngineRunning);Assert.Null(behavior.OwnedRunner);Assert.Equal(0,words);return;}
            if(carrying){Assert.Null(owner.CarryingOwner!.CarriedObjectId);Assert.False(carriedAtDraw);}
            if(named && !stale && spark!=1)
            {
                var say=Assert.IsType<SayTextAction>(behavior.OwnedRunner);
                Assert.Equal("Ada",say.Text);Assert.Equal(4,say.CannedTracks);Assert.Equal(6,words);
            }
            else
            {
                var anim=Assert.IsType<TriggerAnimationAction>(behavior.OwnedRunner);
                Assert.Equal(spark==1?AnimationTrigger.ReactToPickup:AnimationTrigger.AcknowledgeFaceUnnamed,anim.Request.Trigger);
                Assert.Equal(spark==1?0u:4u,anim.RequiredTrackMask);Assert.Equal(2,words);
            }
            Assert.Equal(1.0+ReactToPickupBehavior.RetryScaleStep,behavior.RetryScale);
            Assert.True(behavior.RetryDeadline>owner.Engine.Timer.SecondsF);
        }
        finally {owner.Engine.LogEntry-=OnLog;owner.Engine.ContextRandom.WordSourceForTest=null;manager.Stop(BehaviorStopReason.Interrupted,owner.Engine.Timer.SecondsF);}
    }

    [Theory]
    [InlineData(true,false)]
    [InlineData(true,true)]
    [InlineData(false,false)]
    [InlineData(false,true)]
    public async Task PickupUsesAuthoredAudioAndRetiresItsOwnChild(bool named,bool cancel)
    {
        if(!OperatingSystem.IsWindows() || ShippedLibraries.FindLibDir() is null)return;
        string banks=GraphRig.FindMeta() ?? throw new InvalidOperationException("shipped banks missing");
        string archive=Path.Combine(WwiseAssets.SoundDir ?? throw new InvalidOperationException("audio archive missing"),"AudioAssets.zip");
        var options=new CozmoEngineOptions{ResourcesPath=PickupPyramidNativeExecutionTests.Resources(),SpeechSoundBankDirectory=banks,
            SpeechAudioArchivePath=archive,LabLocaleLanguage="en",BlockPoolPath=""};
        using var f=new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations:true,pumpAnimations:true,sourceNvEmpty:true,engineOptions:options);
        var owner=f.Robot;var manager=owner.BehaviorManagerOwner!;var sink=new Sink();owner.CannedSpeechSinkForTest=()=>sink;
        var needs=new List<string>();
        void OnNeeds(string line){if(line.StartsWith("needs.action_completed SayName:"))needs.Add(line);}
        owner.Engine.Needs.Log+=OnNeeds;
        var behavior=Assert.IsType<ReactToPickupBehavior>(Assert.Single(manager.Reactions.Where(r=>r.Strategy.Trigger==ReactionTrigger.RobotPickedUp)).Behavior);
        var mask=Enumerable.Range(0,21).Select(i=>i==(int)ReactionTrigger.RobotPickedUp || i==(int)ReactionTrigger.FacePositionUpdated).ToArray();
        manager.DisableReactionsWithLock("pickup-audio-fixture",mask,false);
        owner.ActionList.Clear();owner.ActionList.QueueAction(QueueActionPosition.Now,new Gate(),0);manager.CheckReactions(owner.Engine.Timer.Seconds);owner.ActionList.Clear();
        f.Rig.Data(new MotorCalibration{MotorID=MotorID.MOTOR_HEAD,CalibStarted=false,AutoStarted=false});
        for(int i=0;i<10;i++)f.Motors(RobotStatusFlag.IsPickedUp);
        f.M.Vision.FaceDetector=new FaceDetector();f.Head=.1f;f.Motors(RobotStatusFlag.IsPickedUp);f.Image(88,modes:1<<2);
        var face=f.M.Vision.Faces.GetNativeFace(owner,7)!;if(named)face.Name="Ada";
        manager.RemoveDisableReactionsLock("pickup-audio-fixture");manager.CheckReactions(owner.Engine.Timer.Seconds);
        Assert.Same(behavior,manager.Current);
        int mark=f.Rig.Mark();byte? tag=null;uint? actionTag=null;bool ended=false,stopped=false;int startIndex=-1;
        var diagnostics=new System.Collections.Concurrent.ConcurrentQueue<string>();
        void CaptureStack(string message)=>diagnostics.Enqueue(message);
        void CaptureSource(EngineLogEntry entry)
        {
            if(entry.Name is "IAction.Update.TimedOut" or "IActionRunner.Update.ActionCompleted")
                diagnostics.Enqueue($"{owner.Engine.Timer.SecondsF:F3} {entry.Name}: {entry.Text}");
        }
        owner.Engine.StackLogLine+=CaptureStack;owner.Engine.LogEntry+=CaptureSource;
        var wall=System.Diagnostics.Stopwatch.StartNew();float initialEngineTime=owner.Engine.Timer.SecondsF;
        TimeSpan lastTick=TimeSpan.Zero;
        SayTextAction? observedSay=null;TriggerAnimationAction? observedChild=null;int iterations=0;
        var until=TimeSpan.FromSeconds(60);
        try
        {
            while(wall.Elapsed<until && !ended && !stopped)
            {
                iterations++;
                TimeSpan tick=wall.Elapsed;
                f.Rig.DefaultTickMs=(tick-lastTick).TotalMilliseconds;
                lastTick=tick;
                sink.CompleteOne();var stream=f.Rig.Robot.Animations.Scheduler.Stream;
                f.Rig.Data(new AnimationState{NumAudioFramesPlayed=stream.FramesStreamed,NumAnimBytesPlayed=stream.BytesStreamed});
                f.Motors(RobotStatusFlag.IsPickedUp | RobotStatusFlag.IsBodyAccMode);
                var sent=f.Rig.SentSince(mark);
                TriggerAnimationAction? child=behavior.OwnedRunner switch
                {
                    SayTextAction say when named=>say.ChildForTest,
                    TriggerAnimationAction ordinary when !named=>ordinary,
                    _=>null
                };
                if(behavior.OwnedRunner is SayTextAction sayAction)observedSay=sayAction;
                if(child is not null)observedChild=child;
                if(tag is null && child is not null && child.AnimationTagForTest!=0 &&
                    sent.OfType<StartOfAnimation>().Any(s=>s.AnimId==child.AnimationTagForTest))
                {
                    Assert.Equal(4u,child.RequiredTrackMask);tag=child.AnimationTagForTest;
                    actionTag=child.Tag;
                    Assert.True(f.Rig.Robot.Motion.AreAllTracksLockedBy(4,unchecked((int)child.Tag).ToString(System.Globalization.CultureInfo.InvariantCulture)));
                    startIndex=sent.FindIndex(m=>m is StartOfAnimation s && s.AnimId==tag.Value);
                    f.Rig.Data(new AnimationStarted{AnimId=tag.Value});
                }
                if(tag is not null && sent.Skip(startIndex+1).OfType<AudioSample>().Any(s=>s.Samples.Any(b=>b!=0xFF && b!=0x7F)))
                {
                    if(cancel){manager.Stop(BehaviorStopReason.Interrupted,owner.Engine.Timer.SecondsF);stopped=true;}
                    else if(sent.OfType<EndOfAnimation>().Any())
                    {f.Rig.Data(new AnimationEnded{AnimId=tag.Value});f.Rig.Tick();ended=true;}
                }
                await Task.Delay(10);
            }
            string Detail()
            {
                var sent=f.Rig.SentSince(mark);
                return $"named={named} cancel={cancel} wall={wall.Elapsed.TotalSeconds:F2}s engine={initialEngineTime:F2}->{owner.Engine.Timer.SecondsF:F2}s loops={iterations} " +
                    $"behavior={behavior.EngineRunning}/{behavior.OwnedRunner?.GetType().Name ?? "null"} say={observedSay?.State:X8}/{observedSay?.Initialized} " +
                    $"sayStart={observedSay?.StartTime:F2} child={observedChild?.State:X8}/{observedChild?.Initialized} " +
                    $"childStart={observedChild?.StartTime:F2} tag={tag} sentStart={sent.OfType<StartOfAnimation>().Count()} " +
                    $"audio={sent.OfType<AudioSample>().Count()} end={sent.OfType<EndOfAnimation>().Count()} " +
                    $"logs={string.Join(" | ",diagnostics.TakeLast(30))}";
            }
            if(tag is null)Assert.Fail("No owned animation tag. "+Detail());
            if(!(cancel?stopped:ended))Assert.Fail("Audio/terminal milestone missing. "+Detail());
            Assert.Contains(f.Rig.SentSince(mark).Skip(startIndex+1).OfType<AudioSample>(),s=>s.Samples.Any(b=>b!=0xFF && b!=0x7F));
            Assert.Empty(needs);
            if(cancel)Assert.Contains(f.Rig.SentSince(mark),m=>m is AbortAnimation);
            else {for(int i=0;i<5 && behavior.OwnedRunner is not null;i++)f.Motors(RobotStatusFlag.IsPickedUp);Assert.Null(behavior.OwnedRunner);}
            if(named)
            {
                Assert.NotNull(observedSay);
                if(observedSay!.State==EngineActionResult.Timeout)Assert.Fail("Named SayText timed out. "+Detail());
                if(!cancel && observedSay.State!=EngineActionResult.Success)Assert.Fail("Named SayText did not complete successfully. "+Detail());
            }
            Assert.NotNull(actionTag);
            Assert.False(f.Rig.Robot.Motion.AreAllTracksLockedBy(4,unchecked((int)actionTag.Value).ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }
        finally {owner.Engine.StackLogLine-=CaptureStack;owner.Engine.LogEntry-=CaptureSource;owner.Engine.Needs.Log-=OnNeeds;manager.Stop(BehaviorStopReason.Interrupted,owner.Engine.Timer.SecondsF);}
    }
}
