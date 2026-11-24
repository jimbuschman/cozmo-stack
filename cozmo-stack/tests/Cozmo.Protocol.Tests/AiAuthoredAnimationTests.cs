using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Animation.Wwise;
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
    public async Task DefaultRobotAnimationStreamsShippedAudioFromStartupBanks()
    {
        if (!OperatingSystem.IsWindows() || ShippedLibraries.FindLibDir() is null) return;
        var options = new CozmoEngineOptions
        {
            ResourcesPath = PickupPyramidNativeExecutionTests.Resources(), LabLocaleLanguage = "en", BlockPoolPath = ""
        };
        var sink = new Sink();
        using var f = new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations: true, pumpAnimations: true,
            sourceNvEmpty: true, engineOptions: options, engineCreated: engine => engine.RuntimeAudioSinkForTest = () => sink);
        var runtime = Assert.IsType<CannedSpeechOwner>(f.Rig.Engine.RuntimeWwiseAudio);
        await runtime.Ready.WaitAsync(TimeSpan.FromSeconds(25));
        Assert.Same(runtime, f.Robot.GetCannedSpeechOwner());
        Assert.Null(f.Rig.Robot.Animations.AudioSource);
        var library = f.Rig.Robot.Animations.Library!;
        var group = library.GetGroup(library.GetAnimationForTrigger(AnimationTrigger.InteractWithFaceTrackingIdle))!;
        var clip = library.GetClip(group.Entries[0].Name);
        int mark = f.Rig.Mark();
        Assert.NotNull(f.Rig.Robot.Animations.PlayTracked(clip, numLoops: 0));
        await WaitForAudibleSample(mark, clip.Name);
        Assert.True(f.Rig.Logged("Audio engine loaded InitScene"));

        // The live speech action replaces the incumbent animation and acquires the same context worker.
        var speech = new TextToSpeechComponent((_, _, _) => Enumerable.Repeat(.25f, 44100).ToArray());
        f.Rig.Robot.DirectSpeechComponentForTest = speech;
        using var cancel = new CancellationTokenSource();
        mark = f.Rig.Mark();
        var request = f.Rig.Robot.SayEnglishSentenceAsync("Cozmo", cancel.Token);
        await WaitForAudibleSample(mark, "SayTextAnimation");
        cancel.Cancel();
        f.Rig.Tick();
        Assert.True((await request.WaitAsync(TimeSpan.FromSeconds(2))).Admitted);
        Assert.Same(runtime, f.Rig.Engine.RuntimeWwiseAudio);

        async Task WaitForAudibleSample(int from, string expectedClip)
        {
            var deadline = System.Diagnostics.Stopwatch.StartNew();
            int seen = 0;
            bool started = false;
            while (deadline.Elapsed < TimeSpan.FromSeconds(15))
            {
                sink.CompleteOne();
                var stream = f.Rig.Robot.Animations.Scheduler.Stream;
                f.Rig.Data(new AnimationState { NumAudioFramesPlayed = stream.FramesStreamed, NumAnimBytesPlayed = stream.BytesStreamed });
                f.Motors(RobotStatusFlag.IsBodyAccMode);
                var sent = f.Rig.SentSince(from);
                for (; seen < sent.Count; seen++)
                {
                    if (sent[seen] is StartOfAnimation start)
                    {
                        started = f.Rig.Robot.Animations.Scheduler.Playing == expectedClip;
                        f.Rig.Data(new AnimationStarted { AnimId = start.AnimId });
                    }
                    if (started && sent[seen] is AudioSample sample && sample.Samples.Any(b => b is not (0xFF or 0x7F)))
                    {
                        Assert.Equal(744, sample.Samples.Length);
                        return;
                    }
                }
                await Task.Delay(10);
            }
            Assert.Fail($"{expectedClip} did not stream an audible AudioSample from InitScene");
        }
    }

    [Fact]
    public void MamboBackingLoopUsesThePhoneBusAndIsSeparateFromTheAuthoredEvents()
    {
        var assets = Assert.IsType<WwiseSoundLibrary>(WwiseAssets.Library);
        var path = Path.Combine(PickupPyramidNativeExecutionTests.Resources(), "assets", "animations", "anim_dancing_mambo_02.bin");
        var clip = Assert.Single(AnimationLibrary.ParseFile(path), clip => clip.Name == "anim_dancing_mambo_01");
        var refs = clip.Keyframes.OfType<AudioKeyframe>().SelectMany(k => k.EventIds).Select(id => (uint)id).Distinct();
        Assert.All(refs, id =>
        {
            var audio = assets.Resolve(id);
            Assert.Equal("Cozmo.bnk", audio.Bank);
            var action = Assert.Single(audio.Actions);
            Assert.Equal(WwiseBank.PlayAction, action.ActionType);
            WwiseNode node = Assert.IsType<WwiseRandomSequenceNode>(assets.Node(action.Target));
            while (node.Params.BusId == 0)
                node = Assert.IsType<WwiseActorMixerNode>(assets.Node(node.Params.ParentId));
            Assert.Equal(1723505802u, node.Params.BusId); // Cozmo_Robot, remapped to Robot_Bus_1 for object 7
        });

        var play = Assert.Single(assets.Resolve(1905159696).Actions); // Unity CodeLab Mambo Play
        var stop = Assert.Single(assets.Resolve(1709157641).Actions);
        Assert.DoesNotContain(1905159696u, refs);
        Assert.Equal(play.Target, stop.Target);
        Assert.True(WwiseBank.IsStopAction(stop.ActionType));
        var segment = Assert.IsType<WwiseMusicSegmentNode>(assets.Node(play.Target));
        var track = Assert.IsType<WwiseMusicTrackNode>(assets.Node(Assert.Single(segment.Children)));
        Assert.Equal(633958781u, Assert.Single(track.Sources).SourceId);
        var container = Assert.IsType<WwiseMusicSwitchNode>(assets.Node(segment.Params.ParentId));
        Assert.Equal(3829101743u, container.Params.BusId);
        for (uint bus = container.Params.BusId; bus != 0;)
        {
            var node = Assert.IsType<WwiseBusNode>(assets.Node(bus));
            Assert.DoesNotContain(node.Effects, effect =>
                (assets.Node(effect.EffectId) as WwiseEffectNode)?.PluginId == WwiseEffectNode.AnkiHijackPlugin);
            bus = node.ParentBusId;
        }
    }

    [Fact]
    public async Task CapturedMamboCountersReplayTheAuthoredRobotSoundsAndBodyStops()
    {
        string? bundle = Environment.GetEnvironmentVariable("COZMO_MAMBO_REPLAY_BUNDLE");
        if (string.IsNullOrWhiteSpace(bundle)) return;
        Assert.True(OperatingSystem.IsWindows() && ShippedLibraries.FindLibDir() is not null);
        var options = new CozmoEngineOptions
        {
            ResourcesPath = PickupPyramidNativeExecutionTests.Resources(), LabLocaleLanguage = "en", BlockPoolPath = ""
        };
        var sink = new Sink();
        using var f = new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations: true, pumpAnimations: true,
            sourceNvEmpty: true, engineOptions: options, engineCreated: engine => engine.RuntimeAudioSinkForTest = () => sink);
        await Assert.IsType<CannedSpeechOwner>(f.Rig.Engine.RuntimeWwiseAudio).Ready.WaitAsync(TimeSpan.FromSeconds(25));
        f.Rig.Robot.SetEnableFreeplayActivity(false);
        f.Rig.Tick();
        var scheduler = f.Rig.Robot.Animations.Scheduler;
        var library = f.Rig.Robot.Animations.Library!;
        var clip = library.GetClip("anim_dancing_mambo_01");
        Assert.Equal(15444u, clip.DurationMs);
        Assert.Equal(8, clip.Keyframes.OfType<AudioKeyframe>().Count());

        const double capturedStart = 727118.0822;
        // Rebase the captured F1 frame consumption onto this offline robot.
        // Wwise variants can change the size of emitted packets, so the byte
        // acknowledgements follow the offline stream's actual emitted bytes.
        var captured = RecordedRobotInput.ReadInbound(bundle, at => at >= capturedStart - 100, 745570, [0xF1])
            .Select(row => (row.At, State: (AnimationState)row.Message)).ToArray();
        var baseline = captured.Last(row => row.At < capturedStart).State;
        var baselineFrame = scheduler.Stream.FramesStreamed;
        var feedback = baseline;
        int capturedIndex = Array.FindIndex(captured, row => row.At >= capturedStart);
        Assert.True(capturedIndex > 0);
        int mark = f.Rig.Mark(), seen = 0, frames = 0, audible = 0, faces = 0;
        var bodyPackets = new List<(int Frame, short Speed, short Radius)>();
        bool beforeStartAudio = false;
        byte? playing = null;
        using var cancel = new CancellationTokenSource();
        var request = f.Rig.Robot.Motion.QueueAiAnimationAsync(AnimationTrigger.CodeLabDancingMambo, cancellationToken: cancel.Token);
        f.Rig.DefaultTickMs = 0;
        long startNs = f.Rig.NowNs;
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (clock.Elapsed < TimeSpan.FromSeconds(28) && !request.IsCompleted)
        {
            f.Rig.NowNs = startNs + (long)(clock.Elapsed.TotalMilliseconds * 1_000_000);
            sink.CompleteOne();
            while (capturedIndex < captured.Length && captured[capturedIndex].At <= capturedStart + clock.Elapsed.TotalMilliseconds)
                feedback = captured[capturedIndex++].State;
            f.Rig.Data(new AnimationState
            {
                Tag = feedback.Tag,
                EnabledAnimTracks = feedback.EnabledAnimTracks,
                ClientDropCount = feedback.ClientDropCount,
                Timestamp = feedback.Timestamp,
                NumAudioFramesPlayed = Math.Min(scheduler.Stream.FramesStreamed,
                    baselineFrame + feedback.NumAudioFramesPlayed - baseline.NumAudioFramesPlayed),
                NumAnimBytesPlayed = scheduler.Stream.BytesStreamed,
            });
            f.Motors(RobotStatusFlag.IsBodyAccMode);
            var sent = f.Rig.SentSince(mark);
            for (; seen < sent.Count; seen++)
                switch (sent[seen])
                {
                    case StartOfAnimation start when scheduler.Playing == clip.Name:
                        Assert.True(beforeStartAudio); // the native builder sends audio before 0x9B
                        playing = start.AnimId;
                        frames = 1;
                        f.Rig.Data(new AnimationStarted { AnimId = start.AnimId });
                        break;
                    case EndOfAnimation when playing is { } tag:
                        playing = null;
                        f.Rig.Data(new AnimationEnded { AnimId = tag });
                        break;
                    case AudioSilence or AudioSample when playing is null:
                        beforeStartAudio = true;
                        break;
                    case AudioSilence when playing.HasValue:
                        frames++;
                        break;
                    case AudioSample sample when playing.HasValue:
                        frames++;
                        if (sample.Samples.Any(b => (b & 0x7F) != 0)) audible++;
                        break;
                    case BodyMotion body when playing.HasValue:
                        bodyPackets.Add((frames - 1, body.Speed, body.RadiusMm));
                        break;
                    case FaceImage when playing.HasValue:
                        faces++;
                        break;
                }
            await Task.Delay(10);
        }

        if (!request.IsCompleted) { cancel.Cancel(); f.Rig.Tick(); }
        var result = await request.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(result.EngineResult == 0u,
            $"Mambo ended with {result.EngineResult:X8}; emitted {frames} frames and {faces} faces, replayed F1 {capturedIndex}/{captured.Length}.");
        Assert.True(audible > 0);
        Assert.Equal(469, faces);

        // 0x0057CCAA..0x0057CE5A visits only the current body keyframe each tick.
        // 0x004FBA8C..0x004FBB0E emits a stop at the first tick at or past duration.
        // The next move can begin one tick after that stop, even if its trigger is past.
        var expectedBody = new List<(int Frame, short Speed, short Radius)>();
        int lastStop = -1;
        foreach (var move in clip.Keyframes.OfType<BodyKeyframe>())
        {
            int begin = Math.Max(lastStop + 1, (int)((move.TriggerTimeMs + 32) / 33));
            lastStop = begin + (int)((move.DurationTimeMs + 32) / 33);
            expectedBody.Add((begin, move.Speed, move.EncodedRadius!.Value));
            expectedBody.Add((lastStop, 0, short.MaxValue));
        }
        Assert.Equal(180, expectedBody.Count);
        Assert.Equal(expectedBody, bodyPackets);
        Assert.Equal(538, lastStop + 1); // frame 537 (zero-based) is the last 0x99 in the capture
        Assert.Equal(lastStop + 1, frames);
    }

    [Fact]
    public void MissingStartupArchiveLeavesEngineRunningAndReportsMissing()
    {
        var log = new List<string>();
        using var rig = new M4ControlTests.Rig(engineOptions: new CozmoEngineOptions { BlockPoolPath = "" },
            engineCreated: engine =>
            {
                engine.StackLogLine += log.Add;
                engine.RuntimeAudioSinkForTest = () => throw new InvalidOperationException("Audio opened without banks");
            });
        rig.Tick();
        Assert.Null(rig.Engine.RuntimeWwiseAudio);
        Assert.Contains(log, line => line.Contains("MISSING M6-024 InitScene"));
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
    public async Task MissingSelectedClipInterruptsTheIncumbentAndReturnsAnimationFailure()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations: true,
            pumpAnimations: true, sourceNvEmpty: true);
        var animations = f.Rig.Robot.Animations;
        var library = animations.Library!;
        const AnimationTrigger trigger = AnimationTrigger.CozmoSaysSpeakLoop;
        var group = library.GetGroup(library.GetAnimationForTrigger(trigger))!;
        var clip = library.GetClip(group.Entries[0].Name);
        Assert.NotNull(animations.PlayTracked(clip, numLoops: 0));
        group.Entries = new[] { new AnimationGroupEntry("missing_selected_clip", 1f, 0d, "Default") };
        var logs = new List<EngineLogEntry>();
        f.Rig.Engine.LogEntry += logs.Add;

        var request = f.Rig.Robot.Motion.QueueAiAnimationAsync(trigger);
        f.Rig.Tick();
        Assert.Equal(0x03000001u, (await request.WaitAsync(TimeSpan.FromSeconds(2))).EngineResult);
        Assert.Contains(logs, entry => entry.Name == "CannedAnimationContainer.GetAnimation_Const.InvalidName");
        Assert.Contains(logs, entry => entry.Name == "AnimationStreamer.SetStreamingAnimation.Aborting");
        Assert.NotEqual(clip.Name, animations.Scheduler.Playing);
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
