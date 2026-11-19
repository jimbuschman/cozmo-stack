using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Cozmo.Bridge;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Emulation;
using Cozmo.Robot.Animation;
using Xunit;
using Xunit.Abstractions;
using Fixture = Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// Offline MCP -> SayTextAction -> native Acapela -> native shared Wwise -> real streamer.
/// SPEECH-LATENCY-1: hashes are change-detection baselines from the same route with the
/// pre-watchdog ArmMachine blob 9da8ed354bcd32a423a3384e92e24bf1bf0963d8.
/// They establish unchanged output, not independent proof of the whole route's fidelity.
/// Native ordering: 0x0069F442..0x0069F46A, 0x00561498..0x005614B8.
/// No timing threshold is an acceptance criterion. Enable COZMO_SPEECH_TIMING_REPORT.
/// </summary>
[Collection("SteppedBehavior missing-report statics")]
public sealed class SpeechLatencyTests(ITestOutputHelper output)
{
    [Fact]
    public async Task ProfileRealMcpSpeechThroughFirstRobotAudioSample()
    {
        string? report = Environment.GetEnvironmentVariable("COZMO_SPEECH_TIMING_REPORT");
        if (string.IsNullOrEmpty(report)) return;
        try { await Profile(report); }
        finally { SpeechTiming.Observer = null; }
    }

    private async Task Profile(string report)
    {
        var startup = new ConcurrentDictionary<string, long>();
        SpeechTiming.Observer = (phase, at, _) => startup.TryAdd(phase, at);
        var options = new CozmoEngineOptions {
            ResourcesPath = PickupPyramidNativeExecutionTests.Resources(), BlockPoolPath = "",
            SpeechSoundBankDirectory = GraphRig.FindMeta(), LabLocaleLanguage = "en", LabLocaleCountry = "US",
        };
        using var sink = new SilentSink();
        using var f = new Fixture(preloadAnimations: true, pumpAnimations: true, sourceNvEmpty: true,
            engineOptions: options, engineCreated: e => e.RuntimeAudioSinkForTest = () => sink, keepNeedsFull: true);
        await f.Rig.Engine.RuntimeWwiseAudio!.Ready.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.True(startup.ContainsKey("provider-ready"), "Voice must load during robot construction, before a speech request.");
        double startupProviderMs = Stopwatch.GetElapsedTime(startup["provider-start"], startup["provider-ready"]).TotalMilliseconds;
        f.Rig.Robot.SetEnableFreeplayActivity(false); f.Rig.Tick();
        using var bridge = new CozmoBridge(f.Rig.Robot);
        await using var client = new McpServerTests.Client(bridge);
        await client.Initialize();
        var results = new List<object>();
        f.Rig.DefaultTickMs = 0;
        var wall = Stopwatch.StartNew(); long start = f.Rig.NowNs;
        try
        {
            foreach (string text in new[] { "Hello! Look how tall I am!", "Hi.", "I am ready to play.", "Hello there. It is a good day to play together.", "Hello! Look how tall I am!" })
            {
                var times = new ConcurrentDictionary<string, double>();
                string? hash = null; int count = 0;
                long callAt = Stopwatch.GetTimestamp();
                double renderStart = 0, renderTotal = 0;
                Action<AnimationDiagnostic> diagnostic = d => {
                    if (d is SendGateObservation { MessageId: 0x8E, Sent: true } gate)
                        times.TryAdd("first-send", Stopwatch.GetElapsedTime(callAt, gate.Timestamp).TotalMilliseconds);
                };
                f.Rig.Robot.Animations.Scheduler.Diagnostic += diagnostic;
                SpeechTiming.Observer = (phase, at, samples) => {
                    if ((phase.StartsWith("render-", StringComparison.Ordinal) || phase == "hijack-frame") && !times.ContainsKey("offer-start")) return;
                    double ms = Stopwatch.GetElapsedTime(callAt, at).TotalMilliseconds;
                    if (!times.ContainsKey("first-send")) {
                        if (phase == "render-start") renderStart = ms;
                        if (phase == "render-ready") renderTotal += ms - renderStart;
                    }
                    times.TryAdd(phase, ms);
                    if (samples is not null) {
                        count = samples.Length;
                        hash = Convert.ToHexString(SHA256.HashData(MemoryMarshal.AsBytes(samples.AsSpan())));
                    }
                };
                int mark = f.Rig.Mark(); byte? tag = null; int chunks = 0;
                using var wireHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                int call = await client.Send("tools/call", new { name = "say-text", arguments = new { text } });
                Task<JsonElement>? response = null;
                double deadline = wall.Elapsed.TotalSeconds + 100; // Harness liveness only; no engine deadline changed.
                while (response?.IsCompleted != true && wall.Elapsed.TotalSeconds < deadline)
                {
                    f.Rig.NowNs = start + wall.Elapsed.Ticks * 100;
                    var stream = f.Rig.Robot.Animations.Scheduler.Stream;
                    f.Rig.Data(new AnimationState { NumAudioFramesPlayed = stream.FramesStreamed, NumAnimBytesPlayed = stream.BytesStreamed });
                    f.Motors();
                    var messages = f.Rig.SentSince(mark); mark += messages.Count;
                    foreach (var message in messages)
                    {
                        switch (message)
                        {
                            case StartOfAnimation a: tag = a.AnimId; f.Rig.Data(new AnimationStarted { AnimId = a.AnimId }); break;
                            case EndOfAnimation when tag is { } id: f.Rig.Data(new AnimationEnded { AnimId = id }); tag = null; break;
                            case AudioSample audio:
                                response ??= client.Receive(call);
                                chunks++; wireHash.AppendData(audio.Samples); break;
                        }
                    }
                    await Task.Delay(1);
                }
                Assert.True(response?.IsCompleted == true, text + ": MCP speech did not terminate");
                var reply = await response!;
                f.Rig.Robot.Animations.Scheduler.Diagnostic -= diagnostic;
                Assert.False(reply.GetProperty("result").GetProperty("isError").GetBoolean(), reply.ToString());
                string resultsWireHash = Convert.ToHexString(wireHash.GetHashAndReset());
                var expected = text switch
                {
                    "Hello! Look how tall I am!" => (83532, "7D65C74AE9D975D21A6DE5D006149223F8A539EF42A025804563440CBF7A4E02", "DDA175D2DC8A6AB4631B904959FDA0F777FBB0D1A15442937B4D4D8E24749E2C"),
                    "Hi." => (18808, "C8415A6A426366E59000AB59572D05CFE8A5B6E63D925B08956F47B86BFDDFA3", "CEAC2181FF51576B7F66215DE39F9C3F2FAEE7FC96D013EFEB4EB3E3C798412F"),
                    "I am ready to play." => (51088, "B67578BB849603CA10B45CB2EBCDDCD9765D98D0D4CA6B4296D154100AA67A6B", "7B3546ACABAB81F9D5952E4D66A1A224A8FEDD8B6393A8B41EC5FD935423F64F"),
                    "Hello there. It is a good day to play together." => (114372, "BBEE4AAE7C992E61537508A9F90C19275F97D03FE4C82CDAC8738003F4BF692D", "68F95AEE5F8A39D8D29C5B28C9F48CA42B83DD48A61FB1D2FE573FA19029772C"),
                    _ => throw new InvalidOperationException(text),
                };
                Assert.Equal(expected.Item1, count);
                Assert.Equal(expected.Item2, hash);
                Assert.Equal(expected.Item3, resultsWireHash);
                results.Add(new { text, startupProviderMs, renderBeforeFirstSendMs = renderTotal, samples = count, pcmSha256 = hash, wireSha256 = resultsWireHash,
                    chunks, phasesMs = times.OrderBy(k => k.Value).ToDictionary(k => k.Key, k => k.Value), engineResult = reply.GetProperty("result").GetProperty("structuredContent").GetProperty("engineResult") });
                File.WriteAllText(report, JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
                output.WriteLine(text + ": " + JsonSerializer.Serialize(times));
                Assert.True(times.ContainsKey("first-send"), reply.ToString());
                Assert.NotNull(hash);
                Assert.False(times.ContainsKey("provider-start"), "A speech request must reuse the initialized voice.");
                Assert.True(times["pcm-ready"] <= times["offer-start"]);
                Assert.True(times["offer-ready"] <= times["hijack-frame"]);
                Assert.True(times["hijack-frame"] <= times["first-send"]);
            }
        }
        finally { SpeechTiming.Observer = null; }
    }

    internal sealed class SilentSink : IPlatformPcmSink
    {
        private readonly object gate = new();
        private readonly AutoResetEvent ready = new(false);
        private readonly Queue<(ulong Token, double Due)> pending = new();
        private readonly Queue<ulong> done = new();
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly Timer timer;
        private double tail;
        private bool disposed;
        internal SilentSink() => timer = new Timer(_ => Complete(), null, 1, 1);
        public WaitHandle CompletionReady => ready;
        public void Submit(ulong token, byte[] pcm)
        {
            lock (gate)
            {
                if (disposed) return;
                tail = Math.Max(tail, clock.Elapsed.TotalMilliseconds) + pcm.Length / 192.0;
                pending.Enqueue((token, tail));
            }
        }
        private void Complete()
        {
            lock (gate)
            {
                if (disposed) return;
                bool completed = false;
                while (pending.TryPeek(out var item) && item.Due <= clock.Elapsed.TotalMilliseconds)
                { pending.Dequeue(); done.Enqueue(item.Token); completed = true; }
                if (completed) ready.Set();
            }
        }
        public IReadOnlyList<ulong> DrainCompleted()
        { lock (gate) { var result = done.ToArray(); done.Clear(); return result; } }
        public void Dispose()
        { lock (gate) { if (disposed) return; disposed = true; timer.Dispose(); ready.Dispose(); } }
    }
}
