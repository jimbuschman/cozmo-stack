using Cozmo.Robot.Animation.Wwise;
using Cozmo.Robot.Emulation;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class NativeWwisePlatformTests
{
    private sealed class Callback : IWwiseRobotAudioCallback
    {
        internal readonly System.Collections.Concurrent.ConcurrentQueue<WwiseAudioCallbackType> Events = new();
        public void Invoke(WwiseAudioCallbackType type) => Events.Enqueue(type);
    }
    private sealed class FakeSink : IPlatformPcmSink
    {
        private readonly object _gate = new();
        private readonly AutoResetEvent _ready = new(false);
        private readonly Queue<ulong> _done = new();
        private readonly List<(ulong Token, byte[] Pcm)> _submitted = new();
        internal bool Disposed { get; private set; }
        public WaitHandle CompletionReady => _ready;
        public void Submit(ulong token, byte[] pcm)
        {
            lock (_gate) _submitted.Add((token, pcm.ToArray()));
        }
        public IReadOnlyList<ulong> DrainCompleted()
        {
            lock (_gate)
            {
                var done = _done.ToArray();
                _done.Clear();
                return done;
            }
        }
        internal (ulong Token, byte[] Pcm)[] Submitted { get { lock (_gate) return _submitted.ToArray(); } }
        internal void Complete(ulong token)
        {
            lock (_gate)
            {
                if (Disposed) return;
                _done.Enqueue(token);
                _ready.Set();
            }
        }
        public void Dispose() { lock (_gate) { Disposed = true; _ready.Dispose(); } }
    }

    [Fact]
    public async Task DeviceCompletionAloneReleasesGuestBlocksInOrder()
    {
        string? lib = ShippedLibraries.FindLibDir();
        if (lib is null) return;
        string meta = GraphRig.FindMeta() ?? throw new InvalidOperationException("shipped bank metadata missing");
        var fake = new FakeSink();
        var buffer = new SpeechRobotAudioBuffer();
        var session = await NativeWwiseVoiceSession.OpenAsync(lib, GraphRig.ReadBank(meta, "Init.bnk"),
            GraphRig.ReadBank(meta, "English(US)/Cozmo.bnk"), buffer, () => 100, () => fake);
        var first = fake.Submitted;
        Assert.Equal(4, first.Length);
        Assert.All(first, block => Assert.Equal(4096, block.Pcm.Length));
        Assert.Equal(4, first.Select(block => block.Token).Distinct().Count());
        // A command and elapsed wall time cannot consume a queued device block.
        await session.OfferAsync(new float[] { 0.25f, -0.25f }, 22050);
        Assert.Equal(4, fake.Submitted.Length);
        fake.Complete(first[0].Token);
        Assert.True(SpinWait.SpinUntil(() => fake.Submitted.Length > 4 || session.Failure is not null, TimeSpan.FromSeconds(10)));
        Assert.Null(session.Failure);
        var next = fake.Submitted;
        Assert.Equal(5, next.Length);
        Assert.True(next[4].Token > first[3].Token);
        await session.ClearPendingAsync();
        await session.CloseAsync();
        Assert.True(fake.Disposed);
    }

    [Fact]
    public async Task OutOfOrderCompletionFailsTheSession()
    {
        string? lib = ShippedLibraries.FindLibDir();
        if (lib is null) return;
        string meta = GraphRig.FindMeta() ?? throw new InvalidOperationException("shipped bank metadata missing");
        var fake = new FakeSink();
        var session = await NativeWwiseVoiceSession.OpenAsync(lib, GraphRig.ReadBank(meta, "Init.bnk"),
            GraphRig.ReadBank(meta, "English(US)/Cozmo.bnk"), new SpeechRobotAudioBuffer(), () => 100, () => fake);
        var blocks = fake.Submitted;
        Assert.Equal(4, blocks.Length);
        fake.Complete(blocks[1].Token);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await session.Finished.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.NotNull(session.Failure);
        Assert.True(fake.Disposed);
    }

    [Fact]
    public async Task StopRejectsAnEventDelayedUntilAfterStopAdmission()
    {
        string? lib = ShippedLibraries.FindLibDir();
        if (lib is null) return;
        string meta = GraphRig.FindMeta() ?? throw new InvalidOperationException("shipped bank metadata missing");
        var fake = new FakeSink();
        var buffer = new SpeechRobotAudioBuffer();
        var session = await NativeWwiseVoiceSession.OpenAsync(lib, GraphRig.ReadBank(meta, "Init.bnk"),
            GraphRig.ReadBank(meta, "English(US)/Cozmo.bnk"), buffer, () => 100, () => fake);
        await session.OfferAsync(new float[] { 0.25f, -0.25f }, 22050);
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var callback = new Callback();
        Task<uint> delayedPost = Task.Run(() =>
        {
            entered.SetResult();
            release.Wait();
            return session.PostEvent(0xBF0BACE1, 7, callback);
        });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        session.StopAll(7);
        release.Set();
        Assert.Equal(0u, await delayedPost.WaitAsync(TimeSpan.FromSeconds(10)));
        await session.CloseAsync().WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Empty(callback.Events);
        Assert.False(buffer.HasStream);
        Assert.Equal(0, session.PlatformCompletions.Count);
    }

    [Fact]
    public async Task FailedOpenWaitsForPlatformCleanup()
    {
        var fake = new FakeSink();
        string missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        await Assert.ThrowsAsync<FileNotFoundException>(() => NativeWwiseVoiceSession.OpenAsync(missing,
            new byte[] { 1 }, new byte[] { 1 }, new SpeechRobotAudioBuffer(), () => 100, () => fake));
        Assert.True(fake.Disposed);
    }

    [Fact]
    public async Task ActiveCloseKeepsPumpingUntilNativeStopFinishes()
    {
        string? lib = ShippedLibraries.FindLibDir();
        if (lib is null) return;
        string meta = GraphRig.FindMeta() ?? throw new InvalidOperationException("shipped bank metadata missing");
        var fake = new FakeSink();
        var buffer = new SpeechRobotAudioBuffer();
        var session = await NativeWwiseVoiceSession.OpenAsync(lib, GraphRig.ReadBank(meta, "Init.bnk"),
            GraphRig.ReadBank(meta, "English(US)/Cozmo.bnk"), buffer, () => 100, () => fake);
        float scale = BitConverter.UInt32BitsToSingle(0x38000100);
        await session.OfferAsync(AcapelaTtsTests.ProbeSamples().Select(sample => sample * scale).ToArray(), 22050);
        var callback = new Callback();
        uint playing = session.PostEvent(0xBF0BACE1, 7, callback);
        session.SetEventVolume(playing, 1);
        session.RenderAudio();
        int consumed = 0;
        while (consumed < 20)
        {
            Assert.True(SpinWait.SpinUntil(() => fake.Submitted.Length > consumed || session.Failure is not null, TimeSpan.FromSeconds(10)));
            Assert.Null(session.Failure);
            fake.Complete(fake.Submitted[consumed++].Token);
        }
        Task close = session.CloseAsync();
        Assert.False(close.IsCompleted);
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (!close.IsCompleted && DateTime.UtcNow < deadline)
        {
            var submitted = fake.Submitted;
            if (consumed < submitted.Length) fake.Complete(submitted[consumed++].Token);
            else await Task.Delay(2);
        }
        await close.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(fake.Disposed);
        Assert.Equal(new[] { WwiseAudioCallbackType.Complete }, callback.Events.ToArray());
        Assert.True(session.PlatformCompletions.Count > 0);
    }
}
