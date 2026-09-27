using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// The M6-016 engine robot-audio path (Anki side): the up-front alternative draw (A2), the OnDevice and
/// no-buffer branches (A3), the wall-clock BeginBuffering offsets (A5), PostCozmoEvent and event_volume per
/// playing id (A6–A8), the queued callback drain (A9, gapE 6.1–6.4), the routing table (A11), the loading
/// and ready states (A19–A21), abort (A23) and the Nurture silence (A22, gapC 2.8, MD4).
///
/// Every expected value here comes from the row's own numbers (1-based keyframe idx, 744 bytes, the
/// 0x00/0x7F/0xFF mu-law bytes, the bus ids and the +0x40 cursor), not from what the class returns.
/// </summary>
public class WwiseRobotAudioPathTests
{
    // ------------------------------------------------------------------ fakes

    private sealed class FakeHost : IWwiseRobotAudioHost
    {
        public List<(uint GameObject, uint BusId, float Gain)> AuxSends { get; } = new();
        public List<(uint GameObject, float Volume)> OutputVolumes { get; } = new();
        public List<(uint GameObject, int PluginId)> Registrations { get; } = new();
        public Dictionary<uint, IWwiseRobotAudioBuffer> Buffers { get; } = new();

        public void SetGameObjectAuxSendValues(uint gameObject, uint busId, float sendGain) =>
            AuxSends.Add((gameObject, busId, sendGain));

        public void SetGameObjectOutputBusVolume(uint gameObject, float volume) =>
            OutputVolumes.Add((gameObject, volume));

        public void RegisterRobotAudioBuffer(uint gameObject, int pluginId) =>
            Registrations.Add((gameObject, pluginId));

        public IWwiseRobotAudioBuffer? GetRobotAudioBuffer(uint gameObject) =>
            Buffers.GetValueOrDefault(gameObject);
    }

    private sealed class FakeEngine : IWwiseRobotAudioEngine
    {
        public List<(uint EventId, uint GameObject, IWwiseRobotAudioCallback Callback)> Posts { get; } = new();
        public List<(uint PlayingId, float Volume)> EventVolumes { get; } = new();
        public List<uint> StopAlls { get; } = new();
        public int RenderCount { get; private set; }
        /// <summary>The first playing id PostEvent returns; a negative value makes PostEvent return 0 (A8).</summary>
        public int NextPlayingId { get; set; } = 100;

        public uint PostEvent(uint eventId, uint gameObjectId, IWwiseRobotAudioCallback callback)
        {
            Posts.Add((eventId, gameObjectId, callback));
            if (NextPlayingId < 0) return 0;
            return (uint)NextPlayingId++;
        }

        public void SetEventVolume(uint playingId, float volume) => EventVolumes.Add((playingId, volume));
        public void RenderAudio() => RenderCount++;
        public void StopAll(uint gameObjectId) => StopAlls.Add(gameObjectId);
    }

    private sealed class FakeDispatchQueue : IWwiseDispatchQueue
    {
        public List<(double Delay, Action Action)> Scheduled { get; } = new();
        public bool Stopped { get; private set; }

        public void After(double delayMs, Action action) => Scheduled.Add((delayMs, action));

        public void Stop()
        {
            Stopped = true;
            Scheduled.Clear();
        }

        public void RunAll()
        {
            var run = Scheduled.ToList();
            Scheduled.Clear();
            foreach (var (_, action) in run) action();
        }
    }

    private sealed class FakeDispatchFactory : IWwiseDispatchFactory
    {
        public FakeDispatchQueue Queue { get; } = new();
        public int LastPriority { get; private set; } = -1;
        public IWwiseDispatchQueue Create(int priority)
        {
            LastPriority = priority;
            return Queue;
        }
    }

    private sealed class FakeBuffer : IWwiseRobotAudioBuffer
    {
        public bool IsWaitingForReset { get; set; }
        public bool HasStream { get; set; }
        public bool HasData { get; set; }
        public bool IsComplete { get; set; }
        public double CreatedMilliseconds { get; set; }
        public int PopStreamCount { get; private set; }
        public int ResetCount { get; private set; }
        public Queue<float[]> Frames { get; } = new();

        public void PopAudioBufferStream() => PopStreamCount++;

        public ReadOnlyMemory<float> PopNextAudioFrameData() =>
            Frames.Count > 0 ? Frames.Dequeue() : ReadOnlyMemory<float>.Empty;

        public void ResetAudioBufferAnimationCompleted() => ResetCount++;
    }

    private sealed record Rig(
        WwiseRobotAudioPath Path, FakeHost Host, FakeEngine Engine,
        FakeDispatchFactory Dispatch, FakeBuffer Buffer, List<(int Count, int Scheduled)> DrawOrder);

    private static Rig Build(uint gameObject = 7, bool registerBuffer = true,
                             params int[] drawIndices)
    {
        var host = new FakeHost();
        var engine = new FakeEngine();
        var dispatch = new FakeDispatchFactory();
        var buffer = new FakeBuffer();
        if (registerBuffer) host.Buffers[gameObject] = buffer;

        var order = new List<(int Count, int Scheduled)>();
        var pending = new Queue<int>(drawIndices);
        WwiseRobotAudioRefSelector selector = (count, _) =>
        {
            order.Add((count, dispatch.Queue.Scheduled.Count));
            return pending.Count > 0 ? pending.Dequeue() : 0;
        };

        var path = new WwiseRobotAudioPath(host, engine, dispatch, selector);
        return new Rig(path, host, engine, dispatch, buffer, order);
    }

    private static WwiseRobotAudioKeyframe Keyframe(uint trigger, params WwiseRobotAudioRef[] refs) =>
        new(trigger, refs, Enumerable.Repeat(1f / refs.Length, refs.Length).ToArray());

    // ------------------------------------------------------------------ A2: the up-front draw

    [Fact]
    public void M6_016_A2_InitAnimationDrawsEveryKeyframesAlternativeUpFront()
    {
        var rig = Build(7, true, 1, 0, 0);
        var keyframes = new[]
        {
            Keyframe(0, new WwiseRobotAudioRef(0x111, 1.0f, false), new WwiseRobotAudioRef(0x222, 0.5f, false)),
            Keyframe(200, new WwiseRobotAudioRef(0x333, 0.25f, true)),
            Keyframe(500, new WwiseRobotAudioRef(0x444, 1.0f, false)),
        };

        rig.Path.InitAnimation(keyframes, 7);

        // A2: GetAudioRefIndex(true) runs for every keyframe, before BeginBuffering posts anything.
        Assert.Equal(new[] { 2, 1, 1 }, rig.DrawOrder.Select(d => d.Count));
        Assert.All(rig.DrawOrder, d => Assert.Equal(0, d.Scheduled));

        var events = rig.Path.Events;
        Assert.Equal(3, events.Count);
        // A2: the native idx is 1-based over the keyframes; the ref's eventId, kf+0xC and ref+4 come through.
        Assert.Equal((ushort)1, events[0].Index);
        Assert.Equal(0x222u, events[0].EventId);
        Assert.Equal(0u, events[0].TriggerTimeMs);
        Assert.Equal(0.5f, events[0].Volume);
        Assert.Equal((ushort)2, events[1].Index);
        Assert.Equal(0x333u, events[1].EventId);
        Assert.Equal(200u, events[1].TriggerTimeMs);
        Assert.Equal(0.25f, events[1].Volume);
        Assert.Equal((ushort)3, events[2].Index);
        Assert.Equal(500u, events[2].TriggerTimeMs);

        // A2: a non-zero ref+0xC sets +0x3D (whose use is UNKNOWN).
        Assert.True(rig.Path.Flag3D);
    }

    [Fact]
    public void M6_016_A2_AnEventIdOfZeroIsNotPushedAndTheIndexCountsPushedEvents()
    {
        var rig = Build(7, true, 0, 0, 0);
        var keyframes = new[]
        {
            Keyframe(0, new WwiseRobotAudioRef(0x111, 1.0f, false)),
            Keyframe(50, new WwiseRobotAudioRef(0, 1.0f, false)),
            Keyframe(100, new WwiseRobotAudioRef(0x333, 1.0f, false)),
        };

        rig.Path.InitAnimation(keyframes, 7);

        Assert.Equal(2, rig.Path.Events.Count);
        Assert.Equal((ushort)1, rig.Path.Events[0].Index);
        Assert.Equal(0x111u, rig.Path.Events[0].EventId);
        // A2 (0x005968BE): the increment is after the eventId == 0 branch, so the third keyframe's pushed
        // event is the second ordinal, not the third keyframe index.
        Assert.Equal((ushort)2, rig.Path.Events[1].Index);
        Assert.Equal(0x333u, rig.Path.Events[1].EventId);
        Assert.False(rig.Path.Flag3D);
    }

    [Fact]
    public void M6_016_A2_ARejectedRefDoesNotAdvanceThePushedEventIndex()
    {
        // 0x005968A8 blt 0x596908: index < 0 skips the push, and with it the 0x005968BE increment.
        var rig = Build(7, true, 0, -1, 0);
        var keyframes = new[]
        {
            Keyframe(0, new WwiseRobotAudioRef(0x111, 1.0f, false)),
            Keyframe(50, new WwiseRobotAudioRef(0x222, 1.0f, false)),
            Keyframe(100, new WwiseRobotAudioRef(0x333, 1.0f, false)),
        };

        rig.Path.InitAnimation(keyframes, 7);

        Assert.Equal(2, rig.Path.Events.Count);
        Assert.Equal((ushort)1, rig.Path.Events[0].Index);
        Assert.Equal(0x111u, rig.Path.Events[0].EventId);
        Assert.Equal((ushort)2, rig.Path.Events[1].Index);
        Assert.Equal(0x333u, rig.Path.Events[1].EventId);
    }

    [Fact]
    public void M6_016_A2_Flag3DIsSetOnlyWhenAnEventIsPushed()
    {
        // The +0x3D write at 0x005968F8..0x00596904 is inside the push block, so a ref+0xC that belongs to
        // an eventId == 0 keyframe sets nothing.
        var rig = Build(7, true, 0, 0);
        rig.Path.InitAnimation(new[]
        {
            Keyframe(0, new WwiseRobotAudioRef(0, 1.0f, true)),
            Keyframe(50, new WwiseRobotAudioRef(0x222, 1.0f, false)),
        }, 7);

        Assert.Single(rig.Path.Events);
        Assert.False(rig.Path.Flag3D);

        // A pushed ref with +0xC still sets it.
        var rig2 = Build(7, true, 0);
        rig2.Path.InitAnimation(new[] { Keyframe(0, new WwiseRobotAudioRef(0x333, 1.0f, true)) }, 7);
        Assert.True(rig2.Path.Flag3D);
    }

    [Fact]
    public void M6_016_A3_NoEventsCompletesWithoutCreatingADispatchQueue()
    {
        var rig = Build();
        rig.Path.InitAnimation(Array.Empty<WwiseRobotAudioKeyframe>(), 7);

        Assert.Equal(WwiseRobotAudioState.AnimationCompleted, rig.Path.State);
        Assert.Empty(rig.Path.Events);
        Assert.Equal(-1, rig.Dispatch.LastPriority);            // A3: Dispatch::Create never ran
    }

    [Fact]
    public void M6_016_A3_GameObject6TakesTheUnbuiltOnDevicePath()
    {
        var rig = Build(gameObject: 6, registerBuffer: false, drawIndices: 0);
        var keyframes = new[] { Keyframe(0, new WwiseRobotAudioRef(0x111, 1.0f, false)) };

        // A3: game object 6 selects 0x00596DC8, a sibling path this record does not describe.
        var ex = Assert.Throws<NotSupportedException>(() => rig.Path.InitAnimation(keyframes, 6));
        Assert.Contains("OnDevice", ex.Message);
        Assert.Equal(WwiseRobotAudioState.Preparing, rig.Path.State);
    }

    [Fact]
    public void M6_016_A3_AMissingBufferIsState5WithAnError()
    {
        var rig = Build(registerBuffer: false, drawIndices: 0);
        var keyframes = new[] { Keyframe(0, new WwiseRobotAudioRef(0x111, 1.0f, false)) };

        rig.Path.InitAnimation(keyframes, 7);

        Assert.Equal(WwiseRobotAudioState.AnimationError, rig.Path.State);
        Assert.NotNull(rig.Path.LastError);
        Assert.Empty(rig.Engine.Posts);
    }

    // ------------------------------------------------------------------ A11: routing

    [Fact]
    public void M6_016_A11_GameObjects7To10RouteToRobotBus1To4WithAuxOneAndDryMuted()
    {
        var rig = Build();

        // A11: the constructor registers each game object's aux send (1.0) and muted dry path (0.0).
        Assert.Equal(
            new (uint, uint, float)[]
            {
                (7, 2678428988u, 1.0f),
                (8, 2678428991u, 1.0f),
                (9, 2678428990u, 1.0f),
                (10, 2678428985u, 1.0f),
                (6, 0u, 1.0f),
            },
            rig.Host.AuxSends);
        Assert.All(rig.Host.OutputVolumes, v => Assert.Equal(0.0f, v.Volume));
        Assert.Equal(new[] { 7u, 8u, 9u, 10u, 6u }, rig.Host.Registrations.Select(r => r.GameObject));
        Assert.Equal(new[] { 1, 2, 3, 4, 0 }, rig.Host.Registrations.Select(r => r.PluginId));

        // A11: the bus ids are Robot_Bus_1..4 (0x9FA5953C, 0x9FA5953F, 0x9FA5953E, 0x9FA59539).
        Assert.Equal(2678428988u, WwiseRobotAudioPath.RobotBus1);
        Assert.Equal(2678428991u, WwiseRobotAudioPath.RobotBus2);
        Assert.Equal(2678428990u, WwiseRobotAudioPath.RobotBus3);
        Assert.Equal(2678428985u, WwiseRobotAudioPath.RobotBus4);
    }

    // ------------------------------------------------------------------ A5/A6/A7/A8: posting

    [Fact]
    public void M6_016_A5_BeginBufferingPostsAtWallClockOffsetsFromTheFirstEvent()
    {
        var rig = Build(7, true, 0, 0, 0);
        var keyframes = new[]
        {
            Keyframe(0, new WwiseRobotAudioRef(0x111, 1.0f, false)),
            Keyframe(120, new WwiseRobotAudioRef(0x222, 1.0f, false)),
            Keyframe(500, new WwiseRobotAudioRef(0x333, 1.0f, false)),
        };

        rig.Path.InitAnimation(keyframes, 7);

        Assert.Equal(WwiseRobotAudioState.LoadingStream, rig.Path.State);   // A5
        Assert.Equal(2, rig.Dispatch.LastPriority);                          // A3: Dispatch::Create(queue, 2)
        // A5: delay = event.time − firstEvent.time, so the third is 500, not 380 (not the previous gap).
        Assert.Equal(new double[] { 0, 120, 500 }, rig.Dispatch.Queue.Scheduled.Select(s => s.Delay));
    }

    [Fact]
    public void M6_016_A6_A8_EachLambdaPostsAndSetsEventVolumeForItsPlayingId()
    {
        var rig = Build(7, true, 0, 0);
        rig.Engine.NextPlayingId = 700;
        var keyframes = new[]
        {
            Keyframe(0, new WwiseRobotAudioRef(0x111, 0.5f, false)),
            Keyframe(10, new WwiseRobotAudioRef(0x222, 0.25f, false)),
        };

        rig.Path.InitAnimation(keyframes, 7);
        rig.Dispatch.Queue.RunAll();

        // A6/A7: both events post synchronously on the dispatch thread with flags 1|... and cookie ctx.
        Assert.Equal(new[] { 0x111u, 0x222u }, rig.Engine.Posts.Select(p => p.EventId));
        Assert.All(rig.Engine.Posts, p => Assert.Equal(7u, p.GameObject));

        // A8: the returned playing id goes to ctx+4, and A6 sets event_volume per playing id.
        Assert.Equal(new (uint, float)[] { (700u, 0.5f), (701u, 0.25f) }, rig.Engine.EventVolumes);
        Assert.Equal(700u, rig.Path.Events[0].PlayingId);
        Assert.Equal(701u, rig.Path.Events[1].PlayingId);
        Assert.Equal(2, rig.Path.PostedCount);                               // A6: +0x44
        Assert.Equal(2, rig.Engine.RenderCount);                             // A6/A10: ProcessEvents each time
    }

    [Fact]
    public void M6_016_A8_AFailedPostEventIsAnImmediateErrorCallback()
    {
        var rig = Build(7, true, 0);
        rig.Engine.NextPlayingId = -1;                                       // PostEvent returns 0
        rig.Path.InitAnimation(new[] { Keyframe(0, new WwiseRobotAudioRef(0x111, 0.5f, false)) }, 7);

        rig.Dispatch.Queue.RunAll();

        // A8/A9: the type-4 error is delivered immediately, so state 3 and +0x48 happen before the drain.
        Assert.Equal(WwiseRobotAudioEventState.Error, rig.Path.Events[0].State);
        Assert.Equal(1, rig.Path.CallbacksReceived);
        Assert.Empty(rig.Engine.EventVolumes);                               // A6: only when the playing id is non-zero
        Assert.Equal(WwiseRobotAudioState.LoadingStream, rig.Path.State);
    }

    // ------------------------------------------------------------------ A9 / gapE 6: queued callbacks

    [Fact]
    public void M6_016_A9_CompleteAndErrorCallbacksAreQueuedUntilTheDrain()
    {
        var rig = Build(7, true, 0, 0);
        rig.Path.InitAnimation(new[]
        {
            Keyframe(0, new WwiseRobotAudioRef(0x111, 1.0f, false)),
            Keyframe(10, new WwiseRobotAudioRef(0x222, 1.0f, false)),
        }, 7);
        rig.Dispatch.Queue.RunAll();

        var posts = rig.Engine.Posts;
        posts[0].Callback.Invoke(WwiseAudioCallbackType.Complete);
        posts[1].Callback.Invoke(WwiseAudioCallbackType.Error);

        // gapE 6.2: ctx+0x38 = 0, so the trampoline queues; nothing has changed yet.
        Assert.Equal(WwiseRobotAudioEventState.Posted, rig.Path.Events[0].State);
        Assert.Equal(WwiseRobotAudioEventState.Posted, rig.Path.Events[1].State);
        Assert.Equal(0, rig.Path.CallbacksReceived);

        // gapE 6.3: the drain runs them in order, each as HandleCozmoEventCallback (A9).
        Assert.Equal(2, rig.Path.DrainCallbacks());
        Assert.Equal(WwiseRobotAudioEventState.Complete, rig.Path.Events[0].State);
        Assert.Equal(WwiseRobotAudioEventState.Error, rig.Path.Events[1].State);
        Assert.Equal(2, rig.Path.CallbacksReceived);
        Assert.Equal(0, rig.Path.DrainCallbacks());
    }

    // ------------------------------------------------------------------ A19: UpdateLoading

    [Fact]
    public void M6_016_A19_DataStartsTheStreamOnlyWhenTheNextEventsTimeIsReached()
    {
        var rig = Build(7, true, 0);
        rig.Buffer.HasStream = true;
        rig.Buffer.HasData = true;
        rig.Buffer.CreatedMilliseconds = 1000;
        rig.Path.InitAnimation(new[] { Keyframe(1000, new WwiseRobotAudioRef(0x111, 1.0f, false)) }, 7);

        Assert.Equal(WwiseRobotAudioState.LoadingStream, rig.Path.Update(999));   // not yet due
        Assert.Equal(WwiseRobotAudioState.AudioFramesReady, rig.Path.Update(1000)); // +0x50 = 1000 − 1000
    }

    [Fact]
    public void M6_016_A19_NoStreamCompletesOnceEveryEventHasACallback()
    {
        var rig = Build(7, true, 0);
        rig.Buffer.HasStream = false;
        rig.Path.InitAnimation(new[] { Keyframe(0, new WwiseRobotAudioRef(0x111, 1.0f, false)) }, 7);
        rig.Dispatch.Queue.RunAll();

        Assert.Equal(WwiseRobotAudioState.LoadingStream, rig.Path.Update(0));
        rig.Engine.Posts[0].Callback.Invoke(WwiseAudioCallbackType.Complete);
        rig.Path.DrainCallbacks();

        // A19: IsAnimationDone = callbacks ≥ events and no stream.
        Assert.Equal(WwiseRobotAudioState.AnimationCompleted, rig.Path.Update(0));
    }

    [Fact]
    public void M6_016_A19_ACompleteStreamWithNoDataIsPoppedBackToState1()
    {
        var rig = Build(7, true, 0);
        rig.Buffer.HasStream = true;
        rig.Buffer.HasData = false;
        rig.Buffer.IsComplete = true;
        rig.Path.InitAnimation(new[] { Keyframe(0, new WwiseRobotAudioRef(0x111, 1.0f, false)) }, 7);

        Assert.Equal(WwiseRobotAudioState.LoadingStream, rig.Path.Update(0));
        Assert.Equal(1, rig.Buffer.PopStreamCount);
    }

    [Fact]
    public void M6_016_A19_OnceStartedACompleteStreamThatIsNotDueReturnsToState1()
    {
        var rig = Build(7, true, 0);
        rig.Buffer.HasStream = true;
        rig.Buffer.HasData = true;
        rig.Buffer.CreatedMilliseconds = 1000;
        rig.Path.InitAnimation(new[] { Keyframe(1000, new WwiseRobotAudioRef(0x111, 1.0f, false)) }, 7);

        Assert.Equal(WwiseRobotAudioState.AudioFramesReady, rig.Path.Update(1000));

        // A20 puts it back to state 2 with no data, which is where UpdateLoading runs again (A4).
        rig.Buffer.HasData = false;
        Assert.Equal(WwiseRobotAudioState.LoadingStreamFrames, rig.Path.Update(1000));

        // A19: a later stream (streamCreated 9000) with +0x50 = 0 gives floor(9000 − 0) = 9000; at 100 the
        // event is not due and the offset is not reached, so a complete stream goes back to state 1.
        rig.Buffer.HasData = true;
        rig.Buffer.CreatedMilliseconds = 9000;
        rig.Buffer.IsComplete = true;
        Assert.Equal(WwiseRobotAudioState.LoadingStream, rig.Path.Update(100));
    }

    // ------------------------------------------------------------------ A20: UpdateAudioFramesReady

    [Fact]
    public void M6_016_A20_NoDataStallsAndErrorEventsAreSkipped()
    {
        var rig = Build(7, true, 0, 0);
        rig.Buffer.HasStream = true;
        rig.Buffer.HasData = true;
        rig.Path.InitAnimation(new[]
        {
            Keyframe(0, new WwiseRobotAudioRef(0x111, 1.0f, false)),
            Keyframe(200, new WwiseRobotAudioRef(0x222, 1.0f, false)),
        }, 7);
        rig.Path.Update(0);                                  // state 3, cursor 0

        rig.Path.Events[0].State = WwiseRobotAudioEventState.Error;   // +0x40 skips state 3
        rig.Path.UpdateAudioFramesReady();
        Assert.Equal(1, rig.Path.EventCursor);

        rig.Buffer.HasData = false;
        Assert.Equal(WwiseRobotAudioState.LoadingStreamFrames, rig.Path.Update(0));   // A20: not ready, it stalls
    }

    // ------------------------------------------------------------------ A21: PopRobotAudioMessage

    [Fact]
    public void M6_016_A21_PopRobotAudioMessageEncodesMuLawAndZeroPadsTo744()
    {
        var rig = Build(7, true, 0);
        rig.Path.InitAnimation(new[] { Keyframe(0, new WwiseRobotAudioRef(0x111, 1.0f, false)) }, 7);
        rig.Buffer.HasStream = true;
        rig.Buffer.HasData = true;
        rig.Path.Update(0);
        Assert.Equal(WwiseRobotAudioState.AudioFramesReady, rig.Path.State);

        rig.Buffer.Frames.Enqueue(new[] { 0.0f, 1.0f, -1.0f });
        var frame = rig.Path.PopRobotAudioMessage();

        Assert.NotNull(frame);
        Assert.Equal(744, frame!.Length);                    // A21: a short frame is zero-padded to 744
        Assert.Equal(0x00, frame[0]);                        // encodeMuLaw(0.0)
        Assert.Equal(0x7F, frame[1]);                        // encodeMuLaw(1.0)
        Assert.Equal(0xFF, frame[2]);                        // encodeMuLaw(−1.0)
        Assert.All(frame.Skip(3), b => Assert.Equal(0x00, b));

        // A21: an empty frame is all-silence, exactly as the native pushes it (D2.9).
        var silence = rig.Path.PopRobotAudioMessage();
        Assert.NotNull(silence);
        Assert.All(silence!, b => Assert.Equal(0x00, b));

        // A21: only state 3 pops a message.
        rig.Buffer.HasStream = false;
        rig.Buffer.HasData = false;
        rig.Path.Update(0);
        Assert.Null(rig.Path.PopRobotAudioMessage());
    }

    // ------------------------------------------------------------------ A23: abort

    [Fact]
    public void M6_016_A23_AbortStopsFlushesResetsStopsAllAndCompletes()
    {
        var rig = Build(7, true, 0);
        rig.Path.InitAnimation(new[] { Keyframe(0, new WwiseRobotAudioRef(0x111, 1.0f, false)) }, 7);
        rig.Dispatch.Queue.RunAll();

        rig.Engine.Posts[0].Callback.Invoke(WwiseAudioCallbackType.Complete);   // queued
        int rendersBefore = rig.Engine.RenderCount;

        rig.Path.Abort();

        Assert.True(rig.Dispatch.Queue.Stopped);                 // A23: Dispatch::Stop
        Assert.Equal(1, rig.Path.CallbacksReceived);             // A23: FlushAudioCallbackQueue
        Assert.Equal(WwiseRobotAudioEventState.Complete, rig.Path.Events[0].State);
        Assert.Equal(1, rig.Buffer.ResetCount);                  // A23: ResetAudioBufferAnimationCompleted
        Assert.Equal(new[] { 7u }, rig.Engine.StopAlls);         // A23: StopCozmoEvent = StopAll(gameObj)
        Assert.Equal(rendersBefore + 1, rig.Engine.RenderCount); // A23: StopCozmoEvent + ProcessEvents
        Assert.Equal(WwiseRobotAudioState.AnimationCompleted, rig.Path.State);
    }

    [Fact]
    public void M6_016_A23_APostedLambdaAfterAbortDoesNothing()
    {
        var rig = Build(7, true, 0);
        rig.Path.InitAnimation(new[] { Keyframe(0, new WwiseRobotAudioRef(0x111, 1.0f, false)) }, 7);
        var action = rig.Dispatch.Queue.Scheduled[0].Action;    // save the not-yet-run lambda
        rig.Path.Abort();
        int postsBefore = rig.Engine.Posts.Count;

        action();

        Assert.Equal(postsBefore, rig.Engine.Posts.Count);      // A6: the weak pointer is no longer alive
    }

    // ------------------------------------------------------------------ A22 / gapC 2.8 / MD4: Nurture

    [Fact]
    public void M6_016_A22_TheUseGameAuxZeroNurtureEventSendsNothingToTheRobot()
    {
        // Play__Robot_VO__Nurture_Play_Concern_Short 0xFB5F3165: its RanSeq has aux byte 1 (override on,
        // use off), so no game-defined send reaches Robot_Bus_1 and no Hijack stream is prepared
        // (gapC 2.8). MD4 makes the resulting silence the engine behaviour, not a defect.
        var rig = Build(7, true, 0);
        rig.Buffer.HasStream = false;
        rig.Buffer.HasData = false;
        rig.Path.InitAnimation(new[] { Keyframe(0, new WwiseRobotAudioRef(0xFB5F3165, 1.0f, false)) }, 7);
        rig.Dispatch.Queue.RunAll();

        // A22: while the state is 1 the frame builder yields nothing, so the M3 sender emits AudioSilence.
        Assert.Equal(WwiseRobotAudioState.LoadingStream, rig.Path.Update(0));
        Assert.Null(rig.Path.PopRobotAudioMessage());

        // A22: once EndOfEvent arrives and the queue drains, the animation completes.
        rig.Engine.Posts[0].Callback.Invoke(WwiseAudioCallbackType.Complete);
        rig.Path.DrainCallbacks();
        Assert.Equal(WwiseRobotAudioState.AnimationCompleted, rig.Path.Update(0));
        Assert.Null(rig.Path.PopRobotAudioMessage());
    }
}
