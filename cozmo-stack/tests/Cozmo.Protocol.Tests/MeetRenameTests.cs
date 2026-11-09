using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Emulation;
using Cozmo.Robot.Vision;
using Cozmo.Protocol;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class MeetRenameTests
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
    public void PublicRenameSavesAndReportsOnlyMatchingIdentityButStagesEveryRequest()
    {
        if (ShippedLibraries.FindLibDir() is null) return;
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true,
            sourceNvStored: tag => tag switch
            {
                VisionSystem.FaceAlbumNvTag => FaceIdentityTests.Album(),
                VisionSystem.FaceEnrollmentNvTag => FaceIdentityTests.Enrollment(),
                _ => null
            });
        var behavior = Assert.IsType<NativeRespondToRenameFace>(f.Runtime.Bound["RespondToRenameFace"]);
        var local = new List<FaceRenamedReport>();
        using var subscription = f.Rig.Engine.AnimationMessages.SubscribeMessage(0x1D, true,
            value => local.Add(Assert.IsType<FaceRenamedReport>(value)));
        var raw = new List<byte[]>();
        f.Rig.Engine.EngineToGameMessage += (tag, body) => { if (tag == 0x1D) raw.Add(body.ToArray()); };

        f.Rig.Robot.UpdateEnrolledFaceById(7, "wrong", "First");
        f.Rig.Tick();
        Assert.Equal("Drawn", f.M.Vision.Recognizer.EnrolledFaces.Single().Name);
        Assert.Equal("First", behavior.StagedName);
        Assert.Empty(local);

        f.Rig.Robot.UpdateEnrolledFaceById(7, "Drawn", "Åda");
        f.Rig.Tick();
        Assert.Equal("Åda", f.M.Vision.Recognizer.EnrolledFaces.Single().Name);
        Assert.Equal("Åda", behavior.StagedName);
        Assert.Equal(new FaceRenamedReport(7, "Åda"), Assert.Single(local));
        f.Rig.Engine.DrainAppDelivery();
        Assert.Equal(new byte[] { 7, 0, 0, 0, 4, 0xC3, 0x85, 0x64, 0x61 }, Assert.Single(raw));
        Assert.Equal("Åda", f.M.Vision.GetSerializedFaceData().Enrollment.Length > 0
            ? f.M.Vision.Recognizer.EnrolledFaces.Single().Name : "");

        f.Rig.Robot.UpdateEnrolledFaceById(-1, "absent", "");
        f.Rig.Tick();
        Assert.Equal("", behavior.StagedName);
        Assert.Single(local);
    }

    [Fact]
    public void FailedRenameDefersRecognizerLogsAndStillStagesSpeech()
    {
        if (ShippedLibraries.FindLibDir() is null) return;
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true,
            sourceNvStored: tag => tag switch { VisionSystem.FaceAlbumNvTag => FaceIdentityTests.Album(),
                VisionSystem.FaceEnrollmentNvTag => FaceIdentityTests.Enrollment(), _ => null });
        var behavior = Assert.IsType<NativeRespondToRenameFace>(f.Runtime.Bound["RespondToRenameFace"]);
        bool observed = false;
        f.M.Vision.Recognizer.Log += _ =>
        {
            observed = true;
            Assert.True(Task.Run(() => f.M.Vision.Recognizer.EnrolledFaces.Count).Wait(TimeSpan.FromSeconds(2)));
            throw new InvalidOperationException("fixture listener failure");
        };
        f.Rig.Robot.UpdateEnrolledFaceById(7, "wrong", "Ada");
        f.Rig.Tick();
        Assert.True(observed);
        Assert.Equal("Ada", behavior.StagedName);
    }

    [Fact]
    public void RenameReportsBeforeNvRepliesAndWritesAlbumThenUpdatedEnrollment()
    {
        if (ShippedLibraries.FindLibDir() is null) return;
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true,
            sourceNvStored: tag => tag switch { VisionSystem.FaceAlbumNvTag => FaceIdentityTests.Album(),
                VisionSystem.FaceEnrollmentNvTag => FaceIdentityTests.Enrollment(), _ => null });
        var reports = new List<FaceRenamedReport>();
        using var subscription = f.Rig.Engine.AnimationMessages.SubscribeMessage(0x1D, true,
            value => reports.Add(Assert.IsType<FaceRenamedReport>(value)));
        int mark = f.Rig.Mark(), seen = 0;
        f.Rig.Robot.UpdateEnrolledFaceById(7, "Drawn", "Ada");
        f.Rig.Tick();
        Assert.Single(reports); // no NV reply has been supplied
        byte[] enrollment = f.M.Vision.GetSerializedFaceData().Enrollment;
        var commands = new List<NVCommand>();
        for (int i = 0; i < 100 && !commands.Any(x => x.Tag == VisionSystem.FaceEnrollmentNvTag && x.Op == NvStorageComponent.OpWrite); i++)
        {
            var sent = f.Rig.SentSince(mark);
            for (; seen < sent.Count; seen++)
                if (sent[seen] is NVCommand command)
                {
                    commands.Add(command);
                    f.Rig.Data(new NVOpResult { Tag = command.Tag, Op = command.Op, Result = 0 });
                }
            f.Rig.Tick();
        }
        var relevant = commands.Where(c => c.Tag == VisionSystem.FaceAlbumNvTag || c.Tag == VisionSystem.FaceEnrollmentNvTag).ToArray();
        Assert.Equal(new[] { (VisionSystem.FaceAlbumNvTag, NvStorageComponent.OpErase),
            (VisionSystem.FaceAlbumNvTag, NvStorageComponent.OpWrite),
            (VisionSystem.FaceEnrollmentNvTag, NvStorageComponent.OpErase),
            (VisionSystem.FaceEnrollmentNvTag, NvStorageComponent.OpWrite) }, relevant.Select(c => (c.Tag, c.Op)));
        Assert.Equal(enrollment, relevant[^1].Data.Skip(16).Take(enrollment.Length));
        int position = StoredEnrolledFace.HeaderBytes;
        var stored = StoredEnrolledFace.Unpack(enrollment, ref position);
        Assert.Equal(7, stored!.FaceID);
        Assert.Equal("Ada", stored.Name);
        Assert.Contains(3, stored.AlbumEntryIds);
        byte[] savedAlbum = relevant[1].Data.Skip(16).Take(FaceIdentityTests.Album().Length).ToArray();
        byte[] savedEnrollment = relevant[^1].Data.Skip(16).Take(enrollment.Length).ToArray();
        using var restored = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true,
            sourceNvStored: tag => tag switch { VisionSystem.FaceAlbumNvTag => savedAlbum,
                VisionSystem.FaceEnrollmentNvTag => savedEnrollment, _ => null });
        var restoredFace = Assert.Single(restored.M.Vision.Recognizer.EnrolledFaces);
        Assert.Equal(7, restoredFace.FaceID);
        Assert.Equal("Ada", restoredFace.Name);
    }

    [Fact]
    public void QueuedRequestCannotReachReplacementOwner()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        using (f.Rig.Engine.EnterSynchronousOwner())
        {
            f.Rig.Robot.UpdateEnrolledFaceById(7, "Drawn", "Ada");
            f.Rig.Engine.Robots.RemoveRobot(1, false);
            f.Rig.Engine.Robots.AddRobot(1, withRic: false);
        }
        f.Rig.Tick();
        if (f.Rig.Engine.Robot!.NativeBehaviorRuntime?.Bound.TryGetValue("RespondToRenameFace", out var value) == true)
            Assert.Equal("", Assert.IsType<NativeRespondToRenameFace>(value).StagedName);
    }

    [Fact]
    public void MeetChooserRetainsBothRenameAndEnrollmentCandidates()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations: true, sourceNvEmpty: true);
        f.Rig.Robot.SetFaceToEnroll("Enrollment");
        f.Rig.Robot.UpdateEnrolledFaceById(99, "missing", "Rename");
        f.Rig.Tick();
        var rename = Assert.IsType<NativeRespondToRenameFace>(f.Runtime.Bound["RespondToRenameFace"]);
        var enrollment = Assert.IsType<NativeEnrollFaceBehavior>(f.Runtime.Bound["EnrollFace"]);
        Assert.Equal("Rename", rename.StagedName);
        Assert.Equal("Enrollment", enrollment.StagedName);
        var chooser = Assert.IsType<ScoringChooser>(f.Runtime.Tree.Single(a => a.Id == "MeetCozmo").Chooser);
        var decision = chooser.GetDesiredActiveBehavior(null, 0, f.Runtime.Context, f.Rig.Engine.Timer.SecondsF);
        Assert.Contains(decision.Scores, score => score.Id == "RespondToRenameFace" && score.Score > 0);
        Assert.Contains(decision.Scores, score => score.Id == "EnrollFace" && score.Score > 0);
        Assert.Same(rename, decision.Behavior);
        Assert.Equal("Enrollment", enrollment.StagedName);
    }

    [Fact]
    public void ReentrantStopDuringEagerSpeechDoesNotReplayConsumedRequest()
    {
        var options = new CozmoEngineOptions { ResourcesPath = PickupPyramidNativeExecutionTests.Resources() };
        using var f = new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations: true, sourceNvEmpty: true, engineOptions: options);
        f.Rig.Robot.UpdateEnrolledFaceById(99, "missing", "Ada");
        f.Rig.Tick();
        var behavior = Assert.IsType<NativeRespondToRenameFace>(f.Runtime.Bound["RespondToRenameFace"]);
        var manager = f.Robot.BehaviorManagerOwner!;
        bool stopped = false;
        f.Rig.Engine.ContextRandom.WordSourceForTest = () =>
        {
            if (!stopped && behavior.EngineRunning)
            {
                stopped = true;
                behavior.Stop(BehaviorStopReason.Interrupted);
            }
            return 0;
        };
        manager.StartAsync(behavior.Id, f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult();
        Assert.True(stopped);
        Assert.False(behavior.EngineRunning);
        Assert.Equal("", behavior.StagedName);
        Assert.False(behavior.IsRunnable(f.Runtime.Context));
    }

    [Fact]
    public async Task ConfiguredMeetWinnerPlaysAuthoredNameAndIgnoresActiveSpeechRequests()
    {
        if (!OperatingSystem.IsWindows() || ShippedLibraries.FindLibDir() is null) return;
        string banks = GraphRig.FindMeta() ?? throw new InvalidOperationException("shipped banks missing");
        string archive = Path.Combine(WwiseAssets.SoundDir ?? throw new InvalidOperationException("archive missing"), "AudioAssets.zip");
        var options = new CozmoEngineOptions { ResourcesPath = PickupPyramidNativeExecutionTests.Resources(),
            SpeechSoundBankDirectory = banks, SpeechAudioArchivePath = archive, LabLocaleLanguage = "en", BlockPoolPath = "" };
        using var f = new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations: true, pumpAnimations: true,
            sourceNvEmpty: true, engineOptions: options,
            sourceNvStored: tag => tag switch { VisionSystem.FaceAlbumNvTag => FaceIdentityTests.Album(),
                VisionSystem.FaceEnrollmentNvTag => FaceIdentityTests.Enrollment(), _ => null });
        var sink = new Sink();
        f.Robot.CannedSpeechSinkForTest = () => sink;
        var behavior = Assert.IsType<NativeRespondToRenameFace>(f.Runtime.Bound["RespondToRenameFace"]);
        f.Rig.Robot.ActivateHighLevelActivity("MeetCozmoFindFaces");
        f.Rig.Tick();
        f.Rig.Robot.UpdateEnrolledFaceById(7, "wrong", "Ada");
        int mark = f.Rig.Mark(), seen = 0;
        byte? tag = null;
        bool sawOwnedSpeech = false, sentSecond = false, audio = false, completed = false, authoredClip = false;
        string? activeClip = null;
        f.Rig.DefaultTickMs = 0;
        long startNs = f.Rig.NowNs;
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var until = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (DateTime.UtcNow < until && !completed)
        {
            f.Rig.NowNs = startNs + (long)(clock.Elapsed.TotalMilliseconds * 1_000_000);
            sink.CompleteOne();
            var stream = f.Rig.Robot.Animations.Scheduler.Stream;
            f.Rig.Data(new AnimationState { NumAudioFramesPlayed = stream.FramesStreamed, NumAnimBytesPlayed = stream.BytesStreamed });
            f.Motors(RobotStatusFlag.IsBodyAccMode);
            if (behavior.EngineRunning && behavior.OwnedRunner is Cozmo.Robot.Animation.SayTextAction speech)
            {
                sawOwnedSpeech = true;
                Assert.Equal(0, speech.CannedTracks);
                if (!sentSecond) { f.Rig.Robot.UpdateEnrolledFaceById(7, "Drawn", "New"); sentSecond = true; }
            }
            var output = f.Rig.SentSince(mark);
            for (; seen < output.Count; seen++)
            {
                switch (output[seen])
                {
                    case StartOfAnimation start:
                        tag = start.AnimId;
                        activeClip = f.Rig.Robot.Animations.Scheduler.Playing;
                        if (activeClip?.StartsWith("anim_meetcozmo_reenrollment_sayname") == true) authoredClip = true;
                        f.Rig.Data(new AnimationStarted { AnimId = start.AnimId });
                        break;
                    case AudioSample sample when activeClip?.StartsWith("anim_meetcozmo_reenrollment_sayname") == true && sample.Samples.Any(b => b != 0xFF && b != 0x7F): audio = true; break;
                    case EndOfAnimation when tag is { } active:
                        f.Rig.Data(new AnimationEnded { AnimId = active }); tag = null; activeClip = null; break;
                }
            }
            completed = sawOwnedSpeech && sentSecond && !behavior.EngineRunning;
            await Task.Delay(10);
        }
        Assert.True(sawOwnedSpeech);
        Assert.True(authoredClip);
        Assert.True(audio);
        Assert.True(completed);
        Assert.Equal("", behavior.StagedName);
        Assert.Equal("New", f.M.Vision.Recognizer.EnrolledFaces.Single().Name);
        for (int i = 0; i < 10; i++) f.Rig.Tick();
        Assert.False(behavior.EngineRunning);
        Assert.Equal("", behavior.StagedName);
    }
}
