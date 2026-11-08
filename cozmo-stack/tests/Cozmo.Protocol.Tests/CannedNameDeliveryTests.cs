using Cozmo.Protocol;
using Cozmo.Robot.Behavior;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Emulation;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class CannedNameDeliveryTests
{
    private sealed class Sink : IPlatformPcmSink
    {
        private readonly object _gate = new();
        private readonly AutoResetEvent _ready = new(false);
        private readonly Queue<ulong> _submitted = new(), _done = new();
        public WaitHandle CompletionReady => _ready;
        public void Submit(ulong token, byte[] pcm) { lock (_gate) _submitted.Enqueue(token); }
        internal void CompleteOne() { lock (_gate) if (_submitted.TryDequeue(out ulong token)) { _done.Enqueue(token); _ready.Set(); } }
        public IReadOnlyList<ulong> DrainCompleted() { lock (_gate) { var result = _done.ToArray(); _done.Clear(); return result; } }
        public void Dispose() { lock (_gate) _ready.Dispose(); }
    }
    private sealed class FaceDetector : IFaceDetector
    {
        public bool IsAvailable => true;
        public string Description => "supplied native camera face";
        public IReadOnlyList<DetectedFace> Detect(GrayImage image, uint timestamp) =>
            new[] { new DetectedFace(7, new FaceRect(100, 70, 90, 90), new Vec2(120, 110), new Vec2(160, 110), Name: "Ada", RollRad: 0) { NativeNewEntryRotatingTooFast = false } };
    }
    private sealed class GateAction : ActionRunner
    {
        internal GateAction() : base(8, 0) { }
        protected override string ActionName => "reaction gate fixture";
        public override bool CanInterrupt() => false;
        public override uint CheckIfDone() => 0;
    }
    private sealed class StopListener(Action stop) : IReactToFaceListener
    {
        public void FinishedReactingToFace(int faceId) => stop();
        public void ClearDesiredTargets() { }
    }

    [Fact]
    public void FactoryGreetingUsesCurrentCameraFaceAndRetainedActionList()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        var vision = f.M.Vision;
        vision.FaceDetector = new FaceDetector();
        vision.ModeEnableMask = 1 << 2;
        vision.Schedules = AllVisionModesSchedule.InitDefaultSchedules();
        f.Rig.State(RobotStatusFlag.HeadInPos | RobotStatusFlag.LiftInPos, origin: f.Robot.CurrentOriginId, head: .1f);
        uint timestamp = f.Robot.StoredState!.Timestamp;
        f.Rig.Data(new ImageImuData { ImageId = 501, Line2Number = 0 });
        f.Rig.Tick();
        f.Rig.CameraGrayFrame(501, timestamp, Enumerable.Repeat((byte)150, 320 * 240).ToArray());
        vision.WaitForProcessorIdle();
        f.Rig.Engine.VisionUpdateAllResultsFailed!();
        Assert.NotNull(vision.Faces.GetNativeFace(f.Robot, 7)?.NativeTrackingTranslation);
        var behavior = Assert.IsType<AcknowledgeFaceBehavior>(f.Runtime.Bound["AcknowledgeFace"]);
        Assert.True(behavior.NativeRetainedProfile);
        behavior.SetTargets(new[] { 7 });
        Assert.True(f.Robot.BehaviorManagerOwner!.StartAsync(behavior.Id, f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        if (f.Robot.BehaviorManagerOwner.Activity?.GetType().Name == "ConfiguredActivity")
            QueuedSelection.Keep(f.Rig.Robot, f.Robot.BehaviorManagerOwner, behavior.Id);
        behavior.Update(f.Runtime.Context, f.Rig.Engine.Timer.SecondsF);
        var turn = Assert.IsType<NativeLastFaceRunner>(behavior.OwnedRunner);
        Assert.Equal(7, turn.RequestedId);
        Assert.True(turn.SayName);
        Assert.Equal(3, turn.ImagesToWait);
        var objectives = new List<uint>();
        using var objective = f.Rig.Engine.AnimationMessages.SubscribeMessage(0x8B, true, value => { if (value is uint id) objectives.Add(id); });
        behavior.AddListener(new StopListener(() => f.Robot.BehaviorManagerOwner.Stop(BehaviorStopReason.Interrupted, f.Rig.Engine.Timer.SecondsF)));
        f.Rig.Engine.AnimationMessages.Deliver(0x5A, true, new RobotCompletedAction(turn.Tag, 0, 0, Array.Empty<uint>(), 0));
        Assert.DoesNotContain(0x1Au, objectives);
        Assert.False(behavior.EngineRunning);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void StockGreetingPlaysAuthoredNameThroughSharedNativeAudio(bool initialGreeting, bool cancelBeforeSpeech)
    {
        if (!OperatingSystem.IsWindows() || ShippedLibraries.FindLibDir() is null) return;
        string banks = GraphRig.FindMeta() ?? throw new InvalidOperationException("shipped banks missing");
        string archive = Path.Combine(WwiseAssets.SoundDir ?? throw new InvalidOperationException("audio archive missing"), "AudioAssets.zip");
        var options = new CozmoEngineOptions { ResourcesPath = PickupPyramidNativeExecutionTests.Resources(),
            SpeechSoundBankDirectory = banks, SpeechAudioArchivePath = archive, LabLocaleLanguage = "en", BlockPoolPath = "" };
        using var f = new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations: true, pumpAnimations: true, sourceNvEmpty: true, engineOptions: options);
        var sink = new Sink();
        f.Robot.CannedSpeechSinkForTest = () => sink;
        var manager = f.Robot.BehaviorManagerOwner!;
        f.Robot.ActionList.Clear();
        Assert.Equal(0, f.Robot.ActionList.QueueAction(QueueActionPosition.Now, new GateAction(), 0));
        manager.CheckReactions(f.Rig.Engine.Timer.Seconds);
        f.Robot.ActionList.Clear();
        f.M.Vision.FaceDetector = new FaceDetector();
        f.M.Vision.ModeEnableMask = 1 << 2;
        f.M.Vision.Schedules = AllVisionModesSchedule.InitDefaultSchedules();
        f.M.Vision.Enabled = true;
        f.Head = .1f;
        f.Rig.State(RobotStatusFlag.HeadInPos | RobotStatusFlag.LiftInPos | RobotStatusFlag.IsBodyAccMode,
            origin: f.Robot.CurrentOriginId, head: f.Head);
        var behavior = Assert.IsType<AcknowledgeFaceBehavior>(f.Runtime.Bound["AcknowledgeFace"]);
        var needsBeforeTurned = new List<bool>();
        void OnNeeds(string line)
        {
            if (line.StartsWith("needs.action_completed SayName:"))
                needsBeforeTurned.Add(f.M.Vision.Faces.GetNativeFace(f.Robot, 7)?.TurnedTowards ?? false);
        }
        f.Rig.Engine.Needs.Log += OnNeeds;
        var objectives = new List<uint>();
        using var objective = f.Rig.Engine.AnimationMessages.SubscribeMessage(0x8B, true, value => { if (value is uint id) objectives.Add(id); });
        if (initialGreeting)
        {
            f.Rig.Robot.ActivateHighLevelActivity("Freeplay"); f.Rig.Tick();
            Assert.True(f.Robot.BehaviorManagerOwner!.GreetingTimeField54 >= 0);
        }
        else Assert.Equal(-1f, f.Robot.BehaviorManagerOwner!.GreetingTimeField54);
        uint firstTimestamp = f.Robot.StoredState!.Timestamp;
        f.Rig.Data(new ImageImuData { ImageId = 700, Line2Number = 0 }); f.Rig.Tick();
        f.Rig.CameraGrayFrame(700, firstTimestamp, Enumerable.Repeat((byte)150, 320 * 240).ToArray());
        f.M.Vision.WaitForProcessorIdle(); f.Rig.Engine.VisionUpdateAllResultsFailed!();
        var reaction = Assert.Single(manager.Reactions.Where(r => r.Strategy.Trigger == ReactionTrigger.FacePositionUpdated));
        Assert.Same(behavior, reaction.Behavior);
        var strategy = Assert.IsType<FacePositionUpdatedStrategy>(reaction.Strategy);
        var observed = f.M.Vision.Faces.GetNativeFace(f.Robot, 7);
        Assert.True(observed is { HasName: true }, $"observed={observed}, enabled={manager.IsReactionTriggerEnabled(ReactionTrigger.FacePositionUpdated)}, frame={f.M.Vision.FramesProcessed}");
        Assert.Contains(7, strategy.DesiredFaces);
        manager.CheckReactions(f.Rig.Engine.Timer.Seconds);
        Assert.Same(behavior, manager.Current);
        Assert.Equal(ReactionTrigger.FacePositionUpdated, manager.CurrentReactionTrigger);
        behavior.Update(f.Runtime.Context, f.Rig.Engine.Timer.SecondsF);
        var turn = Assert.IsType<NativeLastFaceRunner>(behavior.OwnedRunner);
        int mark = f.Rig.Mark();
        byte? tag = null;
        string? clipName = null;
        bool ended = false;
        uint imageId = 701;
        var until = DateTime.UtcNow + TimeSpan.FromSeconds(18);
        while (DateTime.UtcNow < until && !ended)
        {
            sink.CompleteOne();
            var stream = f.Rig.Robot.Animations.Scheduler.Stream;
            f.Rig.Data(new AnimationState { NumAudioFramesPlayed = stream.FramesStreamed, NumAnimBytesPlayed = stream.BytesStreamed });
            f.Motors(RobotStatusFlag.IsBodyAccMode);
            if (imageId < 715) f.Image(imageId++, modes: 1 << 2);
            var sent = f.Rig.SentSince(mark);
            if (tag is null && turn.ChildForTest is SayTextAction { ChildForTest: { } child } && child.AnimationTagForTest != 0 &&
                sent.OfType<StartOfAnimation>().Any(start => start.AnimId == child.AnimationTagForTest))
            {
                tag = child.AnimationTagForTest; clipName = child.GetAnimationCompletionName();
                f.Rig.Data(new AnimationStarted { AnimId = tag.Value });
                if (cancelBeforeSpeech)
                {
                    manager.Stop(BehaviorStopReason.Interrupted, f.Rig.Engine.Timer.SecondsF);
                    for (int i = 0; i < 5; i++) { sink.CompleteOne(); f.Motors(RobotStatusFlag.IsBodyAccMode); }
                    break;
                }
            }
            if (tag is not null && sent.OfType<EndOfAnimation>().Any())
            { f.Rig.Data(new AnimationEnded { AnimId = tag.Value }); f.Rig.Tick(); ended = true; }
            if (!behavior.EngineRunning) break;
            Thread.Sleep(20);
        }
        var messages = f.Rig.SentSince(mark);
        if (cancelBeforeSpeech)
        {
            Assert.NotNull(tag);
            Assert.Contains(messages, message => message is AbortAnimation);
            Assert.DoesNotContain(messages, message => message is EndOfAnimation);
            Assert.Empty(needsBeforeTurned);
            Assert.False(f.M.Vision.Faces.GetNativeFace(f.Robot, 7)?.TurnedTowards);
            f.Rig.Engine.Needs.Log -= OnNeeds;
            return;
        }
        Assert.True(ended, $"state={turn.FaceState}, action={turn.State:X}, running={behavior.EngineRunning}, current={manager.Current?.Id}, reason={manager.CurrentReactionTrigger}, owned={behavior.OwnedRunner?.GetType().Name}, child={turn.ChildForTest?.GetType().Name}, audio={messages.OfType<AudioSample>().Count()}, motions={messages.OfType<SetBodyAngle>().Count()}/{messages.OfType<SetHeadAngle>().Count()}, logs={string.Join('|', f.Rig.Log.Where(x => x.Contains("Acknowledge") || x.Contains("Action") || x.Contains("Face")).TakeLast(30))}");
        Assert.StartsWith(initialGreeting ? "anim_greeting_happy_" : "anim_freeplay_reacttoface_sayname_", clipName);
        Assert.Contains(messages.OfType<AudioSample>(), sample => sample.Samples.Any(b => b != 0xFF && b != 0x7F));
        Assert.Equal(initialGreeting, behavior.GreetingPlayed);
        for (int i = 0; i < 20 && !objectives.Contains(0x1A); i++) f.Motors(RobotStatusFlag.IsBodyAccMode);
        Assert.Equal(new[] { false }, needsBeforeTurned);
        Assert.True(f.M.Vision.Faces.GetNativeFace(f.Robot, 7)?.TurnedTowards);
        Assert.Contains(0x1Au, objectives);
        f.Rig.Engine.Needs.Log -= OnNeeds;
        f.Robot.BehaviorManagerOwner!.Stop(BehaviorStopReason.Interrupted, f.Rig.Engine.Timer.SecondsF);
    }
}
