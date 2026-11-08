using Cozmo.Robot.Animation.Wwise;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Emulation;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class CozmoVoiceTests
{
    [Fact]
    public void QsortUsesGuestAddressesAndBionicEqualKeyPartition()
    {
        string? lib = ShippedLibraries.FindLibDir();
        if (lib is null) return;
        using var native = new AcapelaNative(lib);
        var machine = native.Machine;
        uint array = machine.StaticAlloc(7 * 4);
        for (uint i = 0; i < 7; i++) machine.W32(array + i * 4, i);
        var calls = new List<(uint, uint)>();
        machine.Libc["test_qsort_compare"] = m => { calls.Add((m.Reg(0), m.Reg(1))); return 0; };
        uint qsort = machine.Library(AcapelaNative.Library).Base + 0x1FA40;
        machine.Call(qsort, new[] { array, 7u, 4u, machine.Stub("test_qsort_compare") });
        Assert.Equal(new uint[] { 3, 1, 2, 0, 4, 5, 6 }, Enumerable.Range(0, 7).Select(i => machine.R32(array + (uint)i * 4)));
        Assert.NotEmpty(calls);
        Assert.All(calls, pair =>
        {
            Assert.InRange(pair.Item1, array, array + 24);
            Assert.InRange(pair.Item2, array, array + 24);
        });
    }

    [Fact]
    public void Rand48UsesSuppliedSeedAnd32BitTime()
    {
        string? lib = ShippedLibraries.FindLibDir();
        if (lib is null) return;
        using var native = new AcapelaNative(lib, () => 1234567890);
        var machine = native.Machine;
        uint time = machine.StaticAlloc(4);
        Assert.Equal(1234567890u, machine.Call(machine.Stub("time"), new[] { time }));
        Assert.Equal(1234567890u, machine.R32(time));
        machine.Call(machine.Stub("srand48"), new[] { 42u });
        ulong state = (42UL << 16) | 0x330E;
        state = (state * 0x5DEECE66DUL + 0xB) & 0xFFFFFFFFFFFFUL;
        Assert.Equal((uint)(state >> 17), machine.Call(machine.Stub("lrand48")));
    }

    [Fact]
    public void WavePortalTransfersOneBufferAndPadsOnlyPartialFrames()
    {
        var portal = new WavePortal();
        portal.Give(new[] { 9f }, 22050);
        portal.Give(new[] { 1f, 2f, 3f }, 22050);
        var (result, voice) = portal.Initialize();
        Assert.Equal(1, result);
        Assert.NotNull(voice);
        Assert.Equal(2, portal.Initialize().Result);
        var frame = new float[2];
        Assert.Equal(WavePortal.DataReady, voice.Execute(frame));
        Assert.Equal(new[] { 1f, 2f }, frame);
        Assert.Equal(WavePortal.NoMoreData, voice.Execute(frame));
        Assert.Equal(new[] { 3f, 0f }, frame);
        voice.Reset();
        var exact = new float[3];
        Assert.Equal(WavePortal.DataReady, voice.Execute(exact));
        Assert.Equal(WavePortal.NoMoreData, voice.Execute(exact));
        Assert.Equal(new[] { 0f, 0f, 0f }, exact);
    }

    [Fact]
    public void JavaOrderSynthesisMatchesTheExistingPcmOracle()
    {
        string? lib = ShippedLibraries.FindLibDir();
        string? ttsDir = AcapelaTtsTests.TtsDir();
        if (lib is null || ttsDir is null) return;
        using var tts = new AcapelaTts(lib, javaGlue: true, unixTime: () => 1700000000);
        tts.LoadVoice(ttsDir, "Ryan");
        Assert.Equal(AcapelaTtsTests.ProbeSamples(), tts.Synthesize(AcapelaTtsTests.ProbeText, 50, 100, 100));
    }

    [Fact]
    public void ShippedNameIntentLoadsItsStyleAndTraitRanges()
    {
        if (WwiseAssets.ObbRoot is not { } obb) return;
        string resources = Path.Combine(obb, "assets", "cozmo_resources");
        var metadata = SpeechMetadata.Load(resources);
        var random = new EngineRandom(7);
        Assert.True(metadata.TrySelect("Name_Normal", "Cozmo", random, out var selected));
        Assert.Equal((byte)1, selected.Style);
        Assert.InRange(selected.Duration, 0.85f, 1.11f);
        Assert.InRange(selected.Pitch, -1f, 1f);
        Assert.False(metadata.TrySelect("not-an-intent", "Cozmo", random, out _));
    }

    [Fact]
    public void MissingIntentDoesNotQueueOrStartSynthesisOrDrawRandomness()
    {
        if (WwiseAssets.ObbRoot is not { } obb) return;
        string resources = Path.Combine(obb, "assets", "cozmo_resources");
        using var rig = new M4ControlTests.Rig(resources);
        rig.Engine.ConnectToRobot(M4ControlTests.Rig.RobotIp);
        rig.Tick();
        var owner = rig.Engine.Robot!;
        Assert.Null(owner.ActionList.GetCurrentAction());
        var tailField = typeof(TextToSpeechComponent).GetField("_tail", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var tail = tailField.GetValue(owner.SpeechComponent);
        int draws = 0;
        rig.Engine.ContextRandom.WordSourceForTest = () => { draws++; return 1; };
        var logs = new List<string>();
        rig.Engine.StackLogLine += logs.Add;
        rig.Robot.QueueSayTextIntentForNativePath("hello", "not-an-intent");
        rig.Tick();
        Assert.Null(owner.ActionList.GetCurrentAction());
        Assert.Same(tail, tailField.GetValue(owner.SpeechComponent));
        Assert.Equal(0, draws);
        Assert.Contains(logs, x => x.Contains("M1-034") && x.Contains("M14-006") && x.Contains("CanNotFind.SayTextIntentConfig") && x.Contains("0x005610A4"));
    }

    [Theory]
    [InlineData(50, 1f, 50)]
    [InlineData(50, 0f, 300)]
    [InlineData(50, 20f, 30)]
    [InlineData(50, float.NaN, 0)]
    public void SpeechRateUsesProviderClamps(int speed, float scalar, int expected)
    {
        Assert.Equal(expected, SpeechProvider.SpeechRate(speed, scalar));
    }

    [Fact]
    public async Task SpeechOperationPublishesAndTransfersOnlyAfterCompletion()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var finish = new ManualResetEventSlim();
        var component = new TextToSpeechComponent((_, _, _) => { started.SetResult(); finish.Wait(TimeSpan.FromSeconds(5)); return new[] { 0.5f, -0.5f }; });
        byte id = component.CreateSpeech("name", 1, 1f);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotEqual((byte)0, id);
        Assert.Equal((byte)1, component.GetOperationState(id));
        Assert.False(component.PrepareAudioEngine(id, out _));
        finish.Set();
        Assert.True(SpinWait.SpinUntil(() => component.GetOperationState(id) == 2, TimeSpan.FromSeconds(5)));
        WavePortal.Shared.Clear();
        try
        {
            Assert.True(component.PrepareAudioEngine(id, out float duration));
            Assert.Equal((float)(2 * 1000.0 / 22050), duration);
            Assert.Equal((byte)0, component.GetOperationState(id));
            var voice = Assert.IsType<WavePortal.Voice>(WavePortal.Shared.Initialize().Source);
            var frame = new float[2];
            Assert.Equal(WavePortal.DataReady, voice.Execute(frame));
            Assert.Equal(new[] { 0.5f, -0.5f }, frame);
        }
        finally { WavePortal.Shared.Clear(); }
    }

    [Fact]
    public async Task ClearedSpeechOperationDiscardsLateSynthesis()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var finish = new ManualResetEventSlim();
        var component = new TextToSpeechComponent((_, _, _) =>
        {
            started.SetResult();
            finish.Wait(TimeSpan.FromSeconds(5));
            completed.SetResult();
            return new[] { 1f };
        });
        byte id = component.CreateSpeech("late", 0, 1f);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        component.CleanupAudioEngine(id);
        finish.Set();
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal((byte)0, component.GetOperationState(id));
        Assert.False(component.PrepareAudioEngine(id, out _));
    }

    [Fact]
    public async Task SpeechOperationIdWrapDoesNotReplaceAnExistingBundle()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var finish = new ManualResetEventSlim();
        var component = new TextToSpeechComponent((_, _, _) =>
        {
            started.TrySetResult();
            finish.Wait(TimeSpan.FromSeconds(5));
            return Array.Empty<float>();
        });
        Assert.Equal((byte)1, component.CreateSpeech("first", 0, 1f));
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        for (int i = 2; i <= 255; i++) Assert.Equal((byte)i, component.CreateSpeech("pending", 0, 1f));
        Assert.Equal((byte)0, component.CreateSpeech("collision", 0, 1f));
        Assert.Equal((byte)1, component.GetOperationState(1));
        component.Retire(() => { });
        finish.Set();
    }

    [Fact]
    public void ShippedVoiceEventSelectsWavePortalAndLeavesUnavailableSourceUnclaimed()
    {
        if (WwiseAssets.Library is not { } library) return;
        var switches = new Dictionary<uint, uint>
        {
            [0x15D4FD9E] = 0x36089802,
            [0x384A9C0D] = 0xC9A81C07,
        };
        var plan = WwisePlayback.Resolve(library, 0xBF0BACE1, switches, new WwiseSelection(new WwiseRng(1), true));
        var plugin = Assert.Single(plan.SourcePlugins);
        Assert.Equal(WavePortal.PluginId, plugin.PluginId);
        Assert.NotEqual(plugin.PluginId, plugin.SourceId);
        WavePortal.Shared.Clear();
        try
        {
            WavePortal.Shared.Give(new[] { 0.25f }, 22050);
            using var source = new WwiseAudioSource(library);
            foreach (var (group, value) in switches) source.SetSwitch(group, value);
            Assert.Null(source.GetPcm(0xBF0BACE1, 1));
            Assert.Contains(source.Misses, m => m.Reason.Contains("MISSING M6-018/M14-006"));
            Assert.Equal(1, WavePortal.Shared.Initialize().Result);
        }
        finally { WavePortal.Shared.Clear(); }
    }
}
