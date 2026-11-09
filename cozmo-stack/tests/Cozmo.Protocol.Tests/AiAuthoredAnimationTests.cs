using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Emulation;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class AiAuthoredAnimationTests
{
    private sealed class Sink : IPlatformPcmSink
    {
        private readonly object _gate = new();
        private readonly AutoResetEvent _ready = new(false);
        private readonly Queue<ulong> _submitted = new(), _done = new();
        public WaitHandle CompletionReady => _ready;
        public void Submit(ulong token, byte[] pcm) { lock (_gate) _submitted.Enqueue(token); }
        internal void CompleteOne() { lock (_gate) if (_submitted.TryDequeue(out var token)) { _done.Enqueue(token); _ready.Set(); } }
        public IReadOnlyList<ulong> DrainCompleted() { lock (_gate) { var values = _done.ToArray(); _done.Clear(); return values; } }
        public void Dispose() { lock (_gate) _ready.Dispose(); }
    }

    [Fact]
    public async Task SilentAuthoredClipUsesFirmwareCallbacksAndCompletionName()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations:true,pumpAnimations:true,sourceNvEmpty:true);
        var reports=new List<RobotCompletedAction>();
        f.Robot.ActionList.RegisterActionEndedCallback(reports.Add);
        var events=new List<AnimationEventReport>();
        f.Rig.Engine.AnimationEvent += events.Add;
        var raw=new List<(ushort Tag,byte[] Body)>();
        f.Rig.Engine.EngineToGameMessage+=(tag,body)=>raw.Add((tag,body.ToArray()));
        int mark=f.Rig.Mark();
        var task=f.Rig.Robot.Motion.QueueAiAnimationAsync(AnimationTrigger.CozmoSaysSpeakLoop,loops:2);
        var runner=Assert.IsAssignableFrom<TriggerAnimationAction>(f.Robot.ActionList.GetCurrentAction());
        for(int i=0;i<20 && !f.Rig.SentSince(mark).OfType<StartOfAnimation>().Any();i++)f.Rig.Tick();
        byte tag=Assert.Single(f.Rig.SentSince(mark).OfType<StartOfAnimation>()).AnimId;
        f.Rig.Data(new AnimationEnded {AnimId=tag});f.Rig.Tick();
        f.Rig.Data(new AnimationStarted {AnimId=unchecked((byte)(tag+1))});f.Rig.Tick();
        f.Rig.Data(new AnimationEnded {AnimId=tag});f.Rig.Tick();
        Assert.False(task.IsCompleted);
        f.Rig.Data(new AnimationStarted {AnimId=tag});f.Rig.Tick();
        f.Rig.Data(new Cozmo.Protocol.AnimationEvent { Timestamp=123, EventId=(AnimEvent)tag, Field2=2 });f.Rig.Tick();
        f.Rig.Engine.DrainAppDelivery();
        Assert.Equal(new AnimationEventReport(123,2),Assert.Single(events));
        Assert.Contains(raw,x=>x.Tag==0x60 && x.Body.SequenceEqual(new byte[]{123,0,0,0,2}));
        f.Rig.Data(new AnimationEnded {AnimId=tag});f.Rig.Tick();
        Assert.False(task.IsCompleted);
        f.Rig.Data(new AnimationEnded {AnimId=tag});f.Rig.Tick();
        var result=await task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(result.Admitted);
        Assert.Equal(0u,result.EngineResult);
        Assert.StartsWith("anim_cozmosays_speakloop",result.AnimationName);
        var report=Assert.Single(reports.Where(x=>x.Tag==runner.Tag));
        Assert.Equal((byte)1,report.CompletionVariant);
        Assert.Equal(result.AnimationName,report.AnimationName);
        Assert.False(f.Robot.ActionList.ContainsRunner(runner));
        f.Rig.Engine.DrainAppDelivery();
        Assert.Contains(raw,x=>x.Tag==0x5A && x.Body.SequenceEqual(report.PackBody()));
    }

    [Fact]
    public async Task AbortCallbackIgnoresWrongTagAndFailsOnMatchingTagWithoutStart()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations:true,pumpAnimations:true,sourceNvEmpty:true);
        var task=f.Rig.Robot.Motion.QueueAiAnimationAsync(AnimationTrigger.CozmoSaysSpeakLoop);
        var runner=Assert.IsAssignableFrom<TriggerAnimationAction>(f.Robot.ActionList.GetCurrentAction());
        f.Rig.Tick();
        uint tag=runner.AnimationTagForTest;
        f.Rig.Engine.AnimationMessages.Deliver(0x5F,true,tag+256);f.Rig.Tick();
        Assert.False(task.IsCompleted);
        f.Rig.Engine.AnimationMessages.Deliver(0x5F,true,tag);f.Rig.Tick();
        Assert.Equal(0x03000001u,(await task.WaitAsync(TimeSpan.FromSeconds(2))).EngineResult);
    }

    [Fact]
    public async Task LockedRequestedTrackFailsBeforePlayback()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations:true,sourceNvEmpty:true);
        f.Rig.Robot.Motion.LockTracks(1,"fixture");
        int mark=f.Rig.Mark();
        var task=f.Rig.Robot.Motion.QueueAiAnimationAsync(AnimationTrigger.CozmoSaysSpeakLoop,ignoreHeadTrack:true);
        f.Rig.Tick();
        Assert.Equal(0x03000019u,(await task.WaitAsync(TimeSpan.FromSeconds(2))).EngineResult);
        Assert.Empty(f.Rig.SentSince(mark).OfType<StartOfAnimation>());
        f.Rig.Robot.Motion.UnlockTracks(1,"fixture");
    }

    [Fact]
    public async Task AuthoredSpeechSourceCannotStartWithoutAuthorizedProvider()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations:true,sourceNvEmpty:true);
        int mark=f.Rig.Mark();
        var task=f.Rig.Robot.Motion.QueueAiAnimationAsync(AnimationTrigger.MeetCozmoRenameFaceSayName);
        for(int i=0;i<10 && !task.IsCompleted;i++)f.Rig.Tick();
        var result=await task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(0x03000000u,result.EngineResult);
        Assert.Contains("MISSING",result.Detail);
        Assert.Empty(f.Rig.SentSince(mark).OfType<StartOfAnimation>());
    }

    [Fact]
    public async Task PendingCancellationDoesNotStopTheExistingAction()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations:true,sourceNvEmpty:true);
        var head=f.Rig.Robot.Motion.SetHeadAngleAsync(.5f);f.Rig.Tick();
        using var cancel=new CancellationTokenSource();
        var task=f.Rig.Robot.Motion.QueueAiAnimationAsync(AnimationTrigger.CozmoSaysSpeakLoop,cancellationToken:cancel.Token);
        cancel.Cancel();f.Rig.Tick();
        var result=await task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(result.Admitted);
        Assert.Equal(0x02000001u,result.EngineResult);
        Assert.False(head.IsCompleted);
        f.Robot.ActionList.Cancel(-1);f.Rig.Tick();
    }

    [Theory]
    [InlineData(false,7u)]
    [InlineData(true,5u)]
    public async Task SafeLiftUsesCapturedCarryAndTreadsAndPreservesUnsignedLoopBits(bool pickedUp,uint mask)
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations:true,pumpAnimations:true,sourceNvEmpty:true);
        f.Robot.CarryingOwner!.SetCarrying(7);
        f.Rig.State(RobotStatusFlag.HeadInPos | RobotStatusFlag.LiftInPos | (pickedUp ? RobotStatusFlag.IsPickedUp : 0));
        using var cancel=new CancellationTokenSource();
        var task=f.Rig.Robot.Motion.QueueAiAnimationAsync(AnimationTrigger.CozmoSaysSpeakLoop,0x80000000u,
            ignoreBodyTrack:true,ignoreHeadTrack:true,cancellationToken:cancel.Token);
        var runner=Assert.IsAssignableFrom<TriggerAnimationAction>(f.Robot.ActionList.GetCurrentAction());
        Assert.Equal(mask,runner.RequiredTrackMask);
        Assert.Equal(0x80000000u,unchecked((uint)runner.Request.Argument2));
        f.Rig.Tick();
        byte tag=runner.AnimationTagForTest;
        f.Rig.Data(new AnimationStarted{AnimId=tag});f.Rig.Data(new AnimationEnded{AnimId=tag});f.Rig.Tick();
        Assert.False(task.IsCompleted);
        cancel.Cancel();f.Rig.Tick();
        Assert.Equal(pickedUp ? 0x02000001u : 0x02000000u,(await task.WaitAsync(TimeSpan.FromSeconds(2))).EngineResult);
    }

    [Fact]
    public async Task ReplacementDuringStreamInterruptCannotClearSuccessor()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations:true,pumpAnimations:true,sourceNvEmpty:true);
        var library=f.Rig.Robot.Animations.Library!;
        var group=library.GetGroup(library.GetAnimationForTrigger(AnimationTrigger.CozmoSaysSpeakLoop))!;
        var clip=library.GetClip(group.Entries[0].Name);
        Assert.NotNull(f.Rig.Robot.Animations.PlayTracked(clip,numLoops:0));
        bool replaced=false;
        void OnLog(EngineLogEntry entry)
        {
            if(replaced || entry.Name!="AnimationStreamer.SetStreamingAnimation.Aborting")return;
            replaced=true;
            f.Rig.Engine.Robots.RemoveRobot(1,false);
            f.Rig.Engine.Robots.AddRobot(1,withRic:false);
            Assert.NotNull(f.Rig.Robot.Animations.PlayTracked(clip,numLoops:0));
        }
        f.Rig.Engine.LogEntry+=OnLog;
        try
        {
            var task=f.Rig.Robot.Motion.QueueAiAnimationAsync(AnimationTrigger.CozmoSaysSpeakLoop);
            f.Rig.Tick();
            Assert.True(replaced);
            var result=await task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.True(result.OwnerRetired);
            Assert.Equal(clip.Name,f.Rig.Robot.Animations.Scheduler.Playing);
        }
        finally {f.Rig.Engine.LogEntry-=OnLog;}
    }

    [Fact]
    public async Task RealAuthoredAudioPlaysAndForeverCancellationAbortsStream()
    {
        if(!OperatingSystem.IsWindows() || ShippedLibraries.FindLibDir() is null)return;
        string banks=GraphRig.FindMeta() ?? throw new InvalidOperationException("shipped banks missing");
        string archive=Path.Combine(WwiseAssets.SoundDir ?? throw new InvalidOperationException("archive missing"),"AudioAssets.zip");
        var options=new CozmoEngineOptions { ResourcesPath=PickupPyramidNativeExecutionTests.Resources(),
            SpeechSoundBankDirectory=banks,SpeechAudioArchivePath=archive,LabLocaleLanguage="en",BlockPoolPath="" };
        using var f=new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations:true,pumpAnimations:true,sourceNvEmpty:true,engineOptions:options);
        var sink=new Sink();f.Robot.CannedSpeechSinkForTest=()=>sink;
        using var cancel=new CancellationTokenSource();
        int mark=f.Rig.Mark(),seen=0;byte? tag=null;bool audio=false;
        var task=f.Rig.Robot.Motion.QueueAiAnimationAsync(AnimationTrigger.InteractWithFaceTrackingIdle,loops:0,cancellationToken:cancel.Token);
        var until=DateTime.UtcNow+TimeSpan.FromSeconds(25);
        while(DateTime.UtcNow<until && !audio)
        {
            sink.CompleteOne();
            var stream=f.Rig.Robot.Animations.Scheduler.Stream;
            f.Rig.Data(new AnimationState {NumAudioFramesPlayed=stream.FramesStreamed,NumAnimBytesPlayed=stream.BytesStreamed});
            f.Motors(RobotStatusFlag.IsBodyAccMode);
            var sent=f.Rig.SentSince(mark);
            for(;seen<sent.Count;seen++)
                switch(sent[seen])
                {
                    case StartOfAnimation start:tag=start.AnimId;f.Rig.Data(new AnimationStarted {AnimId=start.AnimId});break;
                    case AudioSample sample when sample.Samples.Any(b=>b!=0xFF && b!=0x7F):
                        Assert.Equal(744,sample.Samples.Length);audio=true;break;
                }
            await Task.Delay(10);
        }
        Assert.NotNull(tag);Assert.True(audio);Assert.False(task.IsCompleted);
        cancel.Cancel();f.Rig.Tick();
        var result=await task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(0x02000000u,result.EngineResult);
        f.Rig.Tick();
        Assert.Single(f.Rig.SentSince(mark).OfType<AbortAnimation>());
    }

    [Fact]
    public async Task BusySharedRecipientFailsWithoutClearingSameClipIncumbent()
    {
        if(!OperatingSystem.IsWindows() || ShippedLibraries.FindLibDir() is null)return;
        string banks=GraphRig.FindMeta() ?? throw new InvalidOperationException("shipped banks missing");
        string archive=Path.Combine(WwiseAssets.SoundDir ?? throw new InvalidOperationException("archive missing"),"AudioAssets.zip");
        var options=new CozmoEngineOptions { ResourcesPath=PickupPyramidNativeExecutionTests.Resources(),
            SpeechSoundBankDirectory=banks,SpeechAudioArchivePath=archive,LabLocaleLanguage="en",BlockPoolPath="" };
        using var f=new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations:true,pumpAnimations:true,sourceNvEmpty:true,engineOptions:options);
        var sink=new Sink();f.Robot.CannedSpeechSinkForTest=()=>sink;
        var library=f.Rig.Robot.Animations.Library!;
        var group=library.GetGroup(library.GetAnimationForTrigger(AnimationTrigger.InteractWithFaceTrackingIdle))!;
        var original=group.Entries;
        var entry=original[0];group.Entries=new[]{entry};
        var lease=await f.Robot.GetCannedSpeechOwner()!.AcquireOrdinary(()=>true).WaitAsync(TimeSpan.FromSeconds(15));
        try
        {
            var clip=library.GetClip(entry.Name);
            Assert.NotNull(f.Rig.Robot.Animations.PlayTracked(clip,numLoops:0));
            Assert.Equal(clip.Name,f.Rig.Robot.Animations.Scheduler.Playing);
            var task=f.Rig.Robot.Motion.QueueAiAnimationAsync(AnimationTrigger.InteractWithFaceTrackingIdle);
            for(int i=0;i<10 && !task.IsCompleted;i++)f.Rig.Tick();
            var result=await task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.True(result.Admitted);
            Assert.Equal(0x03000000u,result.EngineResult);
            Assert.Contains("MISSING",result.Detail);
            Assert.Equal(clip.Name,f.Rig.Robot.Animations.Scheduler.Playing);
        }
        finally {group.Entries=original;lease.Retire();}
    }

    [Fact]
    public async Task ConsecutiveQueuedAuthoredActionsBothPlayAfterRetirementFence()
    {
        if(!OperatingSystem.IsWindows() || ShippedLibraries.FindLibDir() is null)return;
        string banks=GraphRig.FindMeta() ?? throw new InvalidOperationException("shipped banks missing");
        string archive=Path.Combine(WwiseAssets.SoundDir ?? throw new InvalidOperationException("archive missing"),"AudioAssets.zip");
        var options=new CozmoEngineOptions {ResourcesPath=PickupPyramidNativeExecutionTests.Resources(),SpeechSoundBankDirectory=banks,
            SpeechAudioArchivePath=archive,LabLocaleLanguage="en",BlockPoolPath=""};
        using var f=new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations:true,pumpAnimations:true,sourceNvEmpty:true,engineOptions:options);
        var sink=new Sink();f.Robot.CannedSpeechSinkForTest=()=>sink;
        int mark=f.Rig.Mark(),seen=0,starts=0,ends=0;byte? active=null;
        var first=f.Rig.Robot.Motion.QueueAiAnimationAsync(AnimationTrigger.InteractWithFaceTrackingIdle);
        var second=f.Rig.Robot.Motion.QueueAiAnimationAsync(AnimationTrigger.InteractWithFaceTrackingIdle);
        f.Rig.DefaultTickMs=0;long startNs=f.Rig.NowNs;var clock=System.Diagnostics.Stopwatch.StartNew();
        var until=DateTime.UtcNow+TimeSpan.FromSeconds(30);
        while(DateTime.UtcNow<until && (!first.IsCompleted || !second.IsCompleted))
        {
            f.Rig.NowNs=startNs+(long)(clock.Elapsed.TotalMilliseconds*1_000_000);
            sink.CompleteOne();
            var stream=f.Rig.Robot.Animations.Scheduler.Stream;
            f.Rig.Data(new AnimationState{NumAudioFramesPlayed=stream.FramesStreamed,NumAnimBytesPlayed=stream.BytesStreamed});
            f.Motors(RobotStatusFlag.IsBodyAccMode);
            var sent=f.Rig.SentSince(mark);
            for(;seen<sent.Count;seen++)
                switch(sent[seen])
                {
                    case StartOfAnimation start: starts++;active=start.AnimId;f.Rig.Data(new AnimationStarted{AnimId=start.AnimId});break;
                    case EndOfAnimation when active is {} tag: ends++;active=null;f.Rig.Data(new AnimationEnded{AnimId=tag});break;
                }
            await Task.Delay(10);
        }
        Assert.Equal(0u,(await first.WaitAsync(TimeSpan.FromSeconds(2))).EngineResult);
        Assert.Equal(0u,(await second.WaitAsync(TimeSpan.FromSeconds(2))).EngineResult);
        Assert.Equal(2,starts);Assert.Equal(2,ends);
    }
}
