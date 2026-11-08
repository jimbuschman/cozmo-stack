using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class VoiceRobotDeliveryTests
{
    private sealed class AudioHost(SpeechRobotAudioBuffer buffer) : IWwiseRobotAudioHost
    {
        public void SetGameObjectAuxSendValues(uint obj, uint bus, float gain) { }
        public void SetGameObjectOutputBusVolume(uint obj, float volume) { }
        public void RegisterRobotAudioBuffer(uint obj, int plugin) { }
        public IWwiseRobotAudioBuffer? GetRobotAudioBuffer(uint obj) => obj == 7 ? buffer : null;
    }
    private sealed class AudioEngine : IWwiseRobotAudioEngine
    {
        public IWwiseRobotAudioCallback? Callback;
        public int Stops;
        public uint PostEvent(uint id, uint obj, IWwiseRobotAudioCallback callback) { Assert.Equal(0xBF0BACE1u, id); Callback = callback; return 1; }
        public void SetEventVolume(uint id, float volume) => Assert.Equal(1f, volume);
        public void RenderAudio() { }
        public void StopAll(uint obj) { Assert.Equal(7u, obj); Stops++; }
    }
    private sealed class Dispatch : IWwiseDispatchFactory, IWwiseDispatchQueue
    {
        private readonly Queue<Action> _pending = new();
        public IWwiseDispatchQueue Create(int priority) { Assert.Equal(2, priority); return this; }
        public void After(double delay, Action action) => _pending.Enqueue(action);
        public void Stop() => _pending.Clear();
        public void Run() { while (_pending.TryDequeue(out var action)) action(); }
    }
    private sealed class FakeDirect(WwiseRobotAudioPath path) : IDirectSpeechSession
    {
        public WwiseRobotAudioPath Path => path;
        public Exception? Failure => null;
        public (uint Switch, float Pitch)? Configured;
        public TaskCompletionSource? ConfigurationBarrier;
        public Task Configure(uint voiceSwitch, float pitch) { Configured = (voiceSwitch, pitch); return ConfigurationBarrier?.Task ?? Task.CompletedTask; }
        public bool Retired;
        public void Retire() => Retired = true;
    }

    [Fact]
    public async Task FittedNameUsesShippedFacesAndWaitsForMatchingRobotCompletion()
    {
        if (!OperatingSystem.IsWindows() || Cozmo.Robot.Emulation.ShippedLibraries.FindLibDir() is null) return;
        string banks = Path.Combine(Path.GetTempPath(), "cozmo-fit-test-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(banks, "English(US)"));
        File.WriteAllBytes(Path.Combine(banks, "Init.bnk"), Array.Empty<byte>());
        File.WriteAllBytes(Path.Combine(banks, "English(US)", "Cozmo.bnk"), Array.Empty<byte>());
        try
        {
            var options = new CozmoEngineOptions { ResourcesPath = PickupPyramidNativeExecutionTests.Resources(),
                SpeechSoundBankDirectory = banks, LabLocaleLanguage = "en", BlockPoolPath = "" };
            using var fixture = new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations: true, pumpAnimations: true,
                sourceNvEmpty: true, engineOptions: options);
            var rig = fixture.Rig;
            var library = rig.Robot.Animations.Library!;
            var group = Assert.IsType<AnimationGroup>(library.GetGroup("ag_cozmosays_speakloop"));
            Assert.Equal(3, group.Entries.Count);
            var sources = group.Entries.Select(e => library.GetClip(e.Name)).ToArray();
            var before = sources.Select(c => c.Keyframes.Select(k => k.TriggerTimeMs).ToArray()).ToArray();
            Assert.All(sources, c => Assert.All(c.Keyframes, k => Assert.IsType<FaceKeyframe>(k)));
            var buffer = new SpeechRobotAudioBuffer();
            var audio = new AudioEngine();
            var dispatch = new Dispatch();
            var path = new WwiseRobotAudioPath(new AudioHost(buffer), audio, dispatch, (_, _) => 0);
            var session = new FakeDirect(path);
            session.ConfigurationBarrier = new(TaskCreationOptions.RunContinuationsAsynchronously);
            byte? style = null;
            rig.Robot.DirectSpeechComponentForTest = new TextToSpeechComponent((_, s, _) => { style = s; return Enumerable.Repeat(0.25f, 44100).ToArray(); });
            rig.Robot.DirectSpeechOpenForTest = _ => Task.FromResult<IDirectSpeechSession>(session);
            int mark = rig.Mark();
            var request = rig.Robot.SayEnglishNameWithAnimationAsync("Cozmo");
            Assert.True(SpinWait.SpinUntil(() => { rig.Tick(); return session.Configured is not null; }, TimeSpan.FromSeconds(10)));
            Assert.Equal((byte)1, style);
            Assert.Equal(0x2F8B3BF4u, session.Configured!.Value.Switch);
            Assert.InRange(session.Configured.Value.Pitch, -0.1f, 0.1f);
            Assert.Equal(WwiseRobotAudioState.Preparing, path.State);
            Assert.Null(audio.Callback);
            session.ConfigurationBarrier.SetResult();
            Assert.True(SpinWait.SpinUntil(() => { rig.Tick(); return path.State == WwiseRobotAudioState.LoadingStream; }, TimeSpan.FromSeconds(10)));
            dispatch.Run();
            buffer.Open(rig.Engine.Timer.TimeStampMs); buffer.Push(Enumerable.Repeat(0.25f, 744).ToArray());
            for (int i = 0; i < 30 && (!rig.SentSince(mark).OfType<AudioSample>().Any() || !rig.SentSince(mark).OfType<FaceImage>().Any()); i++) rig.Tick();
            Assert.Contains(rig.SentSince(mark), m => m is AudioSample);
            Assert.Contains(rig.SentSince(mark), m => m is FaceImage);
            var start = Assert.Single(rig.SentSince(mark).OfType<StartOfAnimation>());
            audio.Callback!.Invoke(WwiseAudioCallbackType.Complete); buffer.Close();
            rig.Data(new AnimationEnded { AnimId = start.AnimId }); rig.Tick();
            Assert.False(request.IsCompleted);
            rig.Data(new AnimationStarted { AnimId = start.AnimId }); rig.Tick();
            rig.Data(new AnimationEnded { AnimId = start.AnimId }); rig.Tick(); rig.Tick();
            var result = await request.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(result.Admitted);
            Assert.Equal(0u, result.EngineResult);
            for (int i = 0; i < sources.Length; i++) Assert.Equal(before[i], sources[i].Keyframes.Select(k => k.TriggerTimeMs));
        }
        finally { Directory.Delete(banks, recursive: true); }
    }

    [Fact]
    public async Task FittedSelectionRetirementDoesNotConfigureOrStartReplacementOwner()
    {
        if (!OperatingSystem.IsWindows() || Cozmo.Robot.Emulation.ShippedLibraries.FindLibDir() is null) return;
        string banks = GraphRig.FindMeta() ?? throw new InvalidOperationException("shipped banks missing");
        var options = new CozmoEngineOptions { ResourcesPath = PickupPyramidNativeExecutionTests.Resources(),
            SpeechSoundBankDirectory = banks, LabLocaleLanguage = "en", BlockPoolPath = "" };
        using var fixture = new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations: true, pumpAnimations: true,
            sourceNvEmpty: true, engineOptions: options);
        var rig = fixture.Rig;
        var buffer = new SpeechRobotAudioBuffer();
        var audio = new AudioEngine();
        var dispatch = new Dispatch();
        var session = new FakeDirect(new WwiseRobotAudioPath(new AudioHost(buffer), audio, dispatch, (_, _) => 0));
        rig.Robot.DirectSpeechComponentForTest = new TextToSpeechComponent((_, _, _) => new float[44100]);
        rig.Robot.DirectSpeechOpenForTest = _ => Task.FromResult<IDirectSpeechSession>(session);
        bool replaced = false;
        int selections = 0;
        void Replace(EngineLogEntry entry)
        {
            if (entry.Name == "AnimationGroup.GetAnimation.Found") selections++;
            if (entry.Name != "GetResponseForAnimationTrigger.Found" || replaced) return;
            replaced = true;
            rig.Engine.Robots.RemoveRobot(1, false);
            rig.Engine.Robots.AddRobot(1, withRic: false);
        }
        rig.Engine.LogEntry += Replace;
        int mark = rig.Mark();
        var request = rig.Robot.SayEnglishNameWithAnimationAsync("Cozmo");
        Assert.True(SpinWait.SpinUntil(() => { rig.Tick(); return request.IsCompleted; }, TimeSpan.FromSeconds(10)));
        rig.Engine.LogEntry -= Replace;
        var result = await request.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(replaced);
        Assert.True(result.OwnerRetired);
        Assert.Equal(1, selections);
        Assert.Null(session.Configured);
        Assert.True(session.Retired);
        Assert.DoesNotContain(rig.SentSince(mark), m => m is StartOfAnimation or AudioSample);
    }
    private static AnimationClip Clip() => new()
    {
        Name = "speech", IsLive = true, Tracks = AnimationTrack.Audio,
        Keyframes = new Keyframe[] { new AudioKeyframe(0, new long[] { 0xBF0BACE1 }, 1f, new[] { 1f }, false) }
    };

    [Fact]
    public async Task PublicSpeechRejectsMissingConfigurationAndPrecancelledRequests()
    {
        using var rig = new M4ControlTests.Rig();
        Assert.False((await rig.Robot.SayEnglishSentenceAsync("hello")).Admitted);
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        var result = await rig.Robot.SayEnglishSentenceAsync("hello", cancel.Token);
        Assert.False(result.Admitted);
        Assert.Contains("cancelled", result.Detail);
    }

    [Fact]
    public async Task PublicAdmissionCancelsPendingAndActiveExactRunner()
    {
        if (!OperatingSystem.IsWindows() || Cozmo.Robot.Emulation.ShippedLibraries.FindLibDir() is null) return;
        string dir = Path.Combine(Path.GetTempPath(), "cozmo-speech-test-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(dir, "English(US)"));
        File.WriteAllBytes(Path.Combine(dir, "Init.bnk"), Array.Empty<byte>());
        File.WriteAllBytes(Path.Combine(dir, "English(US)", "Cozmo.bnk"), Array.Empty<byte>());
        try
        {
            var options = new CozmoEngineOptions { ResourcesPath = dir, SpeechSoundBankDirectory = dir, LabLocaleLanguage = "en", BlockPoolPath = "" };
            using var fixture = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true, pumpAnimations: true, engineOptions: options);
            var rig = fixture.Rig;
            rig.Calibrate(); rig.State(flags: RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.HeadInPos, head: 0.1f);
            var buffer = new SpeechRobotAudioBuffer();
            var audioEngine = new AudioEngine();
            var dispatch = new Dispatch();
            var path = new WwiseRobotAudioPath(new AudioHost(buffer), audioEngine, dispatch, (_, _) => 0);
            var session = new FakeDirect(path);
            rig.Robot.DirectSpeechComponentForTest = new TextToSpeechComponent((_, _, _) => new float[] { 0.25f });
            rig.Robot.DirectSpeechOpenForTest = _ => Task.FromResult<IDirectSpeechSession>(session);
            _ = rig.Robot.Motion.SetHeadAngleAsync(0.5f); rig.Tick();
            var current = rig.Engine.Robot!.ActionList.GetCurrentAction()!;
            using var pendingCancel = new CancellationTokenSource();
            var pending = rig.Robot.SayEnglishSentenceAsync("pending", pendingCancel.Token);
            pendingCancel.Cancel(); rig.Tick();
            var pendingResult = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(pendingResult.Admitted);
            Assert.Equal(0x02000001u, pendingResult.EngineResult);
            Assert.Same(current, rig.Engine.Robot.ActionList.GetCurrentAction());
            Assert.True(rig.Engine.Robot.ActionList.Cancel(current.Tag)); rig.Tick();

            using var activeCancel = new CancellationTokenSource();
            var active = rig.Robot.SayEnglishSentenceAsync("active", activeCancel.Token);
            Assert.True(SpinWait.SpinUntil(() => { rig.Tick(); return path.State == WwiseRobotAudioState.LoadingStream; }, TimeSpan.FromSeconds(5)));
            dispatch.Run(); buffer.Open(rig.Engine.Timer.TimeStampMs); buffer.Push(new float[] { 0.25f });
            int mark = rig.Mark();
            for (int i = 0; i < 8 && !rig.SentSince(mark).OfType<AudioSample>().Any(); i++) rig.Tick();
            Assert.Contains(rig.SentSince(mark), m => m is AudioSample);
            activeCancel.Cancel(); rig.Tick();
            var activeResult = await active.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(activeResult.Admitted);
            Assert.Equal(0x02000000u, activeResult.EngineResult);
            Assert.True(session.Retired);
            Assert.Contains(rig.SentSince(mark), m => m is AbortAnimation);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void DirectActionWaitsForMatchingRobotStartedAndEnded()
    {
        using var fixture = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true, pumpAnimations: true);
        var rig = fixture.Rig;
        var owner = rig.Engine.Robot!;
        var buffer = new SpeechRobotAudioBuffer();
        var engine = new AudioEngine();
        var dispatch = new Dispatch();
        var path = new WwiseRobotAudioPath(new AudioHost(buffer), engine, dispatch, (_, _) => 0);
        var session = new FakeDirect(path);
        var speech = new TextToSpeechComponent((_, _, _) => new float[] { 0.25f });
        var action = new SayTextAction(rig.Robot, owner, "hello", 3, 1f, 0f, direct: true,
            speechForTest: speech, openForTest: samples => { Assert.Equal(new[] { 0.25f }, samples); return Task.FromResult<IDirectSpeechSession>(session); });
        var ended = new List<RobotCompletedAction>();
        owner.ActionList.RegisterActionEndedCallback(ended.Add);
        int mark = rig.Mark();
        Assert.Equal(0, owner.ActionList.QueueAction(QueueActionPosition.AtEnd, action));
        Assert.True(SpinWait.SpinUntil(() => { rig.Tick(); return path.State == WwiseRobotAudioState.LoadingStream; }, TimeSpan.FromSeconds(5)));
        dispatch.Run();
        buffer.Open(rig.Engine.Timer.TimeStampMs); buffer.Push(new float[] { 0.25f }); buffer.Close();
        engine.Callback!.Invoke(WwiseAudioCallbackType.Complete);
        for (int i = 0; i < 10 && !rig.SentSince(mark).OfType<StartOfAnimation>().Any(); i++) rig.Tick();
        byte tag = Assert.Single(rig.SentSince(mark).OfType<StartOfAnimation>()).AnimId;
        rig.Data(new AnimationEnded { AnimId = tag }); rig.Tick();
        Assert.Empty(ended.Where(x => x.Tag == action.Tag));
        rig.Data(new AnimationStarted { AnimId = tag }); rig.Tick();
        rig.Data(new AnimationEnded { AnimId = unchecked((byte)(tag + 1)) }); rig.Tick();
        Assert.Empty(ended.Where(x => x.Tag == action.Tag));
        rig.Data(new AnimationEnded { AnimId = tag }); rig.Tick(); rig.Tick();
        Assert.Equal(0u, Assert.Single(ended.Where(x => x.Tag == action.Tag)).Result);
        Assert.True(session.Retired);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ScopedSpeechFlowsThroughRobotStreamerAndCompletesOrAborts(bool abort)
    {
        using var fixture = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true, pumpAnimations: true);
        var rig = fixture.Rig;
        Assert.True(rig.Robot.AnimationStreamingOpen);
        var buffer = new SpeechRobotAudioBuffer();
        var engine = new AudioEngine();
        var dispatch = new Dispatch();
        var path = new WwiseRobotAudioPath(new AudioHost(buffer), engine, dispatch, (_, _) => 0);
        var clip = Clip();
        int mark = rig.Mark();
        var ticket = rig.Robot.Animations.PlaySpeechTracked(clip, path, rig.Engine.Robot!);
        Assert.NotNull(ticket);
        dispatch.Run();
        buffer.Open(rig.Engine.Timer.TimeStampMs);
        float[] samples = Enumerable.Repeat(0.25f, 744).ToArray();
        buffer.Push(samples);
        samples[0] = -1f; // the callback transferred a copy
        void AdvanceRobot()
        {
            var stream = rig.Robot.Animations.Scheduler.Stream;
            rig.Data(new AnimationState { NumAudioFramesPlayed = stream.FramesStreamed, NumAnimBytesPlayed = stream.BytesStreamed });
            rig.Tick();
        }
        for (int i = 0; i < 8 && !rig.SentSince(mark).OfType<AudioSample>().Any(); i++) AdvanceRobot();
        var sent = rig.SentSince(mark);
        var debugStream = rig.Robot.Animations.Scheduler.Stream;
        Assert.True(sent.OfType<AudioSample>().Any(), $"state={path.State} buffer={buffer.HasStream}/{buffer.HasData} open={rig.Robot.AnimationStreamingOpen} pending={debugStream.Count} streamed={debugStream.FramesStreamed}/{debugStream.BytesStreamed} budget={debugStream.NumAudioFramesToSend}/{debugStream.NumBytesToSend} sent={string.Join(',', sent.Select(x => x.GetType().Name))} log={string.Join('|', rig.Log.TakeLast(15))}");
        var audio = Assert.Single(sent.OfType<AudioSample>());
        Assert.Equal(AnkiMuLaw.Encode(0.25f), audio.Samples[0]);
        Assert.Contains(sent, m => m is StartOfAnimation);
        Assert.True(sent.FindIndex(m => m is AudioSample) < sent.FindIndex(m => m is StartOfAnimation));
        if (abort)
        {
            var aborted = new List<object?>();
            using var subscription = rig.Engine.AnimationMessages.SubscribeMessage(0x5F, true, aborted.Add);
            Assert.True(rig.Robot.Animations.StopIfCurrent(ticket!.Generation));
            Assert.Equal(1, engine.Stops);
            AdvanceRobot();
            Assert.Contains(rig.SentSince(mark), m => m is AbortAnimation);
            Assert.DoesNotContain(rig.SentSince(mark), m => m is EndOfAnimation);
            Assert.Equal((uint)ticket.Tag, Assert.IsType<uint>(Assert.Single(aborted)));
            buffer.Push(new float[] { 1f }); // reset gates late callback data
            Assert.False(buffer.HasData);
            buffer.Close();
        }
        else
        {
            buffer.Push(new float[] { -0.25f });
            engine.Callback!.Invoke(WwiseAudioCallbackType.Complete);
            buffer.Close();
            for (int i = 0; i < 8 && !rig.SentSince(mark).OfType<EndOfAnimation>().Any(); i++) AdvanceRobot();
            Assert.True(rig.SentSince(mark).OfType<EndOfAnimation>().Any(), $"state={path.State} buffer={buffer.HasStream}/{buffer.HasData}/{buffer.IsComplete} pending={debugStream.Count} streamed={debugStream.FramesStreamed}/{debugStream.BytesStreamed} budget={debugStream.NumAudioFramesToSend}/{debugStream.NumBytesToSend}");
            var finalWire = rig.SentSince(mark);
            var audioFrames = finalWire.OfType<AudioSample>().ToArray();
            Assert.Equal(2, audioFrames.Length);
            Assert.Equal(AnkiMuLaw.Encode(-0.25f), audioFrames[1].Samples[0]);
            Assert.True(finalWire.FindLastIndex(m => m is AudioSample) < finalWire.FindIndex(m => m is EndOfAnimation));
            Assert.Equal(0, engine.Stops);
        }
        buffer.Retire();
    }
}
