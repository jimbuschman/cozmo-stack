using System.Text.Json;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Emulation;
using Cozmo.Robot.Vision;
using Fixture = Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class FaceIdentityTests
{
    private sealed class EnrollmentCamera(bool fresh) : IFaceDetector
    {
        public bool IsAvailable => true;
        public string Description => "supplied enrollment camera face";
        public IReadOnlyList<DetectedFace> Detect(GrayImage image, uint timestamp) =>
            new[] { new DetectedFace(7, new FaceRect(100, 70, 90, 90), new Vec2(120, 110), new Vec2(160, 110), Name: fresh ? null : "Drawn", RollRad: 0) { NativeNewEntryRotatingTooFast = false } };
    }
    private sealed class EnrollmentSink : IPlatformPcmSink
    {
        private readonly object _gate = new();
        private readonly AutoResetEvent _ready = new(false);
        private readonly Queue<ulong> _submitted = new(), _done = new();
        private int _nonSilentBlocks;
        internal int NonSilentBlocks { get { lock (_gate) return _nonSilentBlocks; } }
        public WaitHandle CompletionReady => _ready;
        public void Submit(ulong token, byte[] pcm) { lock (_gate) { _submitted.Enqueue(token); if (pcm.Any(value => value != 0)) _nonSilentBlocks++; } }
        internal void CompleteOne() { lock (_gate) if (_submitted.TryDequeue(out ulong token)) { _done.Enqueue(token); _ready.Set(); } }
        public IReadOnlyList<ulong> DrainCompleted() { lock (_gate) { var values = _done.ToArray(); _done.Clear(); return values; } }
        public void Dispose() { lock (_gate) _ready.Dispose(); }
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    public async Task RetainedEnrollmentRunsSpeechThenBothRealNvCallbacksBeforeReport(bool fresh, bool cancel, bool resumeSave)
    {
        if (!OperatingSystem.IsWindows() || ShippedLibraries.FindLibDir() is null) return;
        string banks = GraphRig.FindMeta() ?? throw new InvalidOperationException("shipped banks missing");
        string archive = Path.Combine(WwiseAssets.SoundDir ?? throw new InvalidOperationException("audio archive missing"), "AudioAssets.zip");
        var options = new CozmoEngineOptions { ResourcesPath = PickupPyramidNativeExecutionTests.Resources(),
            SpeechSoundBankDirectory = banks, SpeechAudioArchivePath = archive, LabLocaleLanguage = "en", BlockPoolPath = "" };
        using var fixture = new Fixture(preloadAnimations: true, pumpAnimations: true, sourceNvEmpty: true, engineOptions: options,
            sourceNvStored: tag => tag switch { VisionSystem.FaceAlbumNvTag => Album(), VisionSystem.FaceEnrollmentNvTag => Enrollment(), _ => null });
        var sink = new EnrollmentSink(); fixture.Robot.CannedSpeechSinkForTest = () => sink;
        var vision = fixture.M.Vision;
        vision.FaceDetector = new EnrollmentCamera(fresh); vision.Enabled = true;
        vision.ModeEnableMask = 1 << 2; vision.Schedules = AllVisionModesSchedule.InitDefaultSchedules();
        fixture.Rig.Robot.ActivateHighLevelActivity("MeetCozmoFindFaces"); fixture.Rig.Tick();
        var behavior = Assert.IsType<Cozmo.Robot.Behavior.NativeEnrollFaceBehavior>(fixture.Runtime.Bound["EnrollFace"]);
        var reports = new List<FaceEnrollmentCompletedReport>();
        var reportOrder = new List<byte>();
        using var erased = fixture.Rig.Engine.AnimationMessages.SubscribeMessage(0x57, true, _ => reportOrder.Add(0x57));
        using var report = fixture.Rig.Engine.AnimationMessages.SubscribeMessage(0x56, true, value => { reportOrder.Add(0x56); reports.Add(Assert.IsType<FaceEnrollmentCompletedReport>(value)); });
        fixture.Rig.Robot.SetFaceToEnroll("Ada", fresh ? 0 : 7);
        int mark = fixture.Rig.Mark(), seen = 0;
        bool pulse = false; byte? activeTag = null; string? activeClip = null; uint image = 2100;
        var nv = new List<uint>();
        var nvErases = new List<uint>();
        var clips = new HashSet<string>();
        bool scanAudio = false, nameAudio = false, scanHasTracker = false;
        bool interruptedSave = false, resumedSave = false;
        NVCommand? heldNv = null;
        fixture.Rig.DefaultTickMs = 0;
        long engineStart = fixture.Rig.NowNs;
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        var until = DateTime.UtcNow + TimeSpan.FromSeconds(40);
        while (DateTime.UtcNow < until && (reports.Count == 0 || resumeSave &&
            (nvErases.Count(id => id == VisionSystem.FaceAlbumNvTag) < 2 || nvErases.Count(id => id == VisionSystem.FaceEnrollmentNvTag) < 2)))
        {
            fixture.Rig.NowNs = engineStart + (long)(elapsed.Elapsed.TotalMilliseconds * 1_000_000);
            sink.CompleteOne();
            var stream = fixture.Rig.Robot.Animations.Scheduler.Stream;
            if (fixture.Rig.Robot.Animations.Scheduler.Playing is { } clip) clips.Add(clip);
            fixture.Rig.Data(new AnimationState { NumAudioFramesPlayed = stream.FramesStreamed, NumAnimBytesPlayed = stream.BytesStreamed });
            fixture.Motors(RobotStatusFlag.IsBodyAccMode);
            if (behavior.EngineRunning && behavior.Phase is 2 or 3) fixture.Image(image++, modes: 1 << 2);
            if (behavior.EngineRunning && behavior.Phase == 3 && behavior.OwnedRunner is CompoundAction scan)
                scanHasTracker |= scan.Children.OfType<NativeTrackFaceRunner>().Any();
            if (!pulse && behavior.EngineRunning && behavior.Phase == 3 && (cancel || scanAudio))
            {
                if (cancel) fixture.Rig.Robot.CancelFaceEnrollment();
                else vision.Faces.ApplyEnrollmentCount(4);
                pulse = true;
            }
            if (resumeSave && !interruptedSave && behavior.EngineRunning && behavior.Phase == 5)
            {
                fixture.Rig.Robot.ActivateHighLevelActivity("Selection");
                fixture.Rig.Tick();
                Assert.False(behavior.EngineRunning);
                Assert.Empty(reports);
                Assert.Equal("Ada", behavior.StagedName);
                interruptedSave = true;
                fixture.Rig.Robot.ActivateHighLevelActivity("MeetCozmoFindFaces");
                fixture.Rig.Tick();
            }
            if (interruptedSave && behavior.EngineRunning && behavior.Phase == 5) resumedSave = true;
            if (resumedSave && heldNv is { } held)
            {
                if (held.Op == NvStorageComponent.OpErase) nvErases.Add(held.Tag);
                nv.Add(held.Tag);
                fixture.Rig.Data(new NVOpResult { Tag = held.Tag, Op = held.Op, Result = 0 });
                heldNv = null;
            }
            var output = fixture.Rig.SentSince(mark);
            for (; seen < output.Count; seen++)
            {
                switch (output[seen])
                {
                    case StartOfAnimation start:
                        activeTag = start.AnimId; activeClip = fixture.Rig.Robot.Animations.Scheduler.Playing;
                        fixture.Rig.Data(new AnimationStarted { AnimId = start.AnimId }); break;
                    case EndOfAnimation when activeTag is { } tag:
                        fixture.Rig.Data(new AnimationEnded { AnimId = tag }); activeTag = null; activeClip = null; break;
                    case AudioSample sample when sample.Samples.Any(b => b != 0xFF && b != 0x7F):
                        if (activeClip?.StartsWith("anim_meetcozmo_lookface_02") == true) scanAudio = true;
                        if (activeClip?.StartsWith("anim_meetcozmo_sayname_") == true || activeClip?.StartsWith("anim_meetcozmo_reenrollment_sayname") == true) nameAudio = true;
                        break;
                    case ExecutePath path: fixture.Rig.Data(new PathFollowingEvent { EventId = path.EventId, EventType = 0 }); fixture.Rig.Tick(); fixture.Rig.Data(new PathFollowingEvent { EventId = path.EventId, EventType = 2 }); break;
                    case NVCommand command when command.Tag is VisionSystem.FaceAlbumNvTag or VisionSystem.FaceEnrollmentNvTag:
                        if (resumeSave && interruptedSave && !resumedSave) { heldNv = command; break; }
                        if (command.Op == NvStorageComponent.OpErase) nvErases.Add(command.Tag);
                        nv.Add(command.Tag); fixture.Rig.Data(new NVOpResult { Tag = command.Tag, Op = command.Op, Result = 0 }); break;
                }
            }
            await Task.Delay(15);
        }
        Assert.True(pulse, $"enrollment never reached scan; phase={behavior.Phase}");
        var completed = Assert.Single(reports);
        Assert.Equal(cancel ? (byte)6 : (byte)0, completed.Result);
        Assert.Equal(7, completed.Id);
        Assert.Equal("Ada", completed.Name);
        if (cancel)
        {
            Assert.Equal(new byte[] { 0x57, 0x56 }, reportOrder);
            Assert.Empty(vision.Recognizer.EnrolledFaces);
            return;
        }
        Assert.Equal(new byte[] { 0x56 }, reportOrder);
        if (resumeSave)
        {
            Assert.True(interruptedSave && resumedSave);
            Assert.True(nvErases.Count(id => id == VisionSystem.FaceAlbumNvTag) >= 2);
            Assert.True(nvErases.Count(id => id == VisionSystem.FaceEnrollmentNvTag) >= 2);
        }
        Assert.Equal("Ada", vision.Recognizer.EnrolledFaces.Single(f => f.FaceID == 7).Name);
        Assert.True(scanHasTracker && scanAudio);
        Assert.True(nameAudio);
        Assert.Contains(clips, clip => clip.StartsWith(fresh ? "anim_meetcozmo_sayname_01" : "anim_meetcozmo_reenrollment_sayname"));
        if (fresh)
        {
            Assert.Contains(clips, clip => clip.StartsWith("anim_meetcozmo_sayname_02"));
            Assert.Contains(clips, clip => clip.StartsWith("anim_meetcozmo_celebration"));
            Assert.True(sink.NonSilentBlocks > 0);
        }
        Assert.Contains(VisionSystem.FaceAlbumNvTag, nv);
        Assert.Contains(VisionSystem.FaceEnrollmentNvTag, nv);
        Assert.True(nv.IndexOf(VisionSystem.FaceAlbumNvTag) < nv.IndexOf(VisionSystem.FaceEnrollmentNvTag));
    }

    [Fact]
    public void StockEraseStartsAlbumSaveBeforePublishingErasedFace()
    {
        if (ShippedLibraries.FindLibDir() is null) return;
        using var fixture = new Fixture(sourceNvEmpty: true, sourceNvStored: tag => tag switch
        {
            VisionSystem.FaceAlbumNvTag => Album(),
            VisionSystem.FaceEnrollmentNvTag => Enrollment(),
            _ => null,
        });
        var vision = fixture.M.Vision;
        var owner = fixture.Rig.Engine.Robot!;
        var reports = new List<NativeErasedEnrolledFace>();
        bool pendingAtReport = false;
        using var subscription = fixture.Rig.Engine.AnimationMessages.SubscribeMessage(0x57, true, payload =>
        {
            reports.Add(Assert.IsType<NativeErasedEnrolledFace>(payload));
            pendingAtReport = !fixture.Rig.Engine.NvStorage!.IsIdle;
        });
        Assert.False(vision.EraseStockEnrolledFace(owner, 99));
        Assert.Empty(reports);
        Assert.True(vision.EraseStockEnrolledFace(owner, 7));
        Assert.True(pendingAtReport);
        Assert.Equal(new NativeErasedEnrolledFace(7, string.Empty), Assert.Single(reports));
        Assert.Empty(vision.Recognizer.EnrolledFaces);
    }

    [Fact]
    public void AutoTickVisionPostDoesNotWaitForEngineOwnerUnderFrameGate()
    {
        using var rig = new M4ControlTests.Rig();
        rig.Engine.StartOffline(autoTick: true);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var delivered = new ManualResetEventSlim();
        var holder = Task.Run(() =>
        {
            using var lease = rig.Engine.EnterSynchronousOwner();
            entered.Set();
            release.Wait();
        });
        Task? post = null;
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
            post = Task.Run(() =>
            {
                lock (rig.Robot.LocalizationWorld.FrameGate)
                    rig.Engine.PostGameMessageFromVisionGate(() => delivered.Set());
            });
            Assert.True(post.Wait(TimeSpan.FromSeconds(2)), "vision post waited for the held engine owner");
        }
        finally { release.Set(); }
        Assert.True(holder.Wait(TimeSpan.FromSeconds(5)));
        Assert.True(post!.Wait(TimeSpan.FromSeconds(5)));
        Assert.True(delivered.Wait(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void UninitializedStockExportFailsRatherThanReturningAnEmptyAlbum()
    {
        using var detector = new OkaoFaceDetector();
        var recognizer = new FaceRecognizer(new NativeOkaoFaceRecognizer(detector));
        Assert.Throws<InvalidOperationException>(() => recognizer.ExportStockData());
    }

    [Fact]
    public void ExportLimitsAlbumToFirstTenNamesButKeepsAllEnrollment()
    {
        if (ShippedLibraries.FindLibDir() is null) return;
        var enrollment = new List<byte>();
        enrollment.AddRange(BitConverter.GetBytes(StoredEnrolledFace.VersionPrefix));
        enrollment.AddRange(BitConverter.GetBytes(12));
        for (int id = 1; id <= 11; id++)
        {
            var face = new StoredEnrolledFace { FaceID = id, Name = $"Face{id}" };
            if (id == 11) { face.AlbumEntryIds.Add(3); face.AlbumEntrySeconds.Add(1); }
            face.Pack(enrollment);
        }
        using var fixture = new Fixture(sourceNvEmpty: true, sourceNvStored: tag => tag switch
        {
            VisionSystem.FaceAlbumNvTag => Album(),
            VisionSystem.FaceEnrollmentNvTag => enrollment.ToArray(),
            _ => null,
        });
        var vision = fixture.M.Vision;
        var exported = vision.GetSerializedFaceData();
        var faces = FaceRecognizer.DeserializeEnrollment(exported.Enrollment, 0, out _);
        Assert.Equal(11, faces!.Count);
        var native = new NativeOkaoFaceRecognizer(Assert.IsType<OkaoFaceDetector>(vision.FaceDetector));
        var restored = native.RestoreAlbum(native.CreateCommon(), exported.Album, exported.Album.Length, out int status);
        Assert.Equal(0, status);
        Assert.NotNull(restored);
        try
        {
            Assert.Equal(0, native.IsRegistered(restored!, 3, 0, out int registered));
            Assert.Equal(0, registered);
        }
        finally { native.DeleteAlbumHandle(restored!); }
    }

    [Fact]
    public void SaveUnderFrameGateDefersAndDropsRetiredOwner()
    {
        if (ShippedLibraries.FindLibDir() is null) return;
        using var fixture = new Fixture(sourceNvEmpty: true, sourceNvStored: tag => tag switch
        {
            VisionSystem.FaceAlbumNvTag => Album(),
            VisionSystem.FaceEnrollmentNvTag => Enrollment(),
            _ => null,
        });
        var vision = fixture.M.Vision;
        int mark = fixture.Rig.Mark();
        lock (vision.World.FrameGate)
        {
            vision.SaveFaceAlbumToRobot();
            Assert.True(fixture.Rig.Engine.NvStorage!.IsIdle);
        }
        fixture.Rig.Engine.Robots.RemoveRobot(1, false);
        fixture.Rig.Engine.Robots.AddRobot(1, withRic: false);
        fixture.Rig.Tick();
        Assert.DoesNotContain(fixture.Rig.SentSince(mark).OfType<NVCommand>(),
            c => c.Tag is VisionSystem.FaceAlbumNvTag or VisionSystem.FaceEnrollmentNvTag);
    }

    [Fact]
    public void UnavailableStockExportDoesNotQueueNvErase()
    {
        if (ShippedLibraries.FindLibDir() is null) return;
        using var fixture = new Fixture(sourceNvEmpty: true, sourceNvStored: tag => tag switch
        {
            VisionSystem.FaceAlbumNvTag => Album(),
            VisionSystem.FaceEnrollmentNvTag => Enrollment(),
            _ => null,
        });
        var vision = fixture.M.Vision;
        Assert.IsType<OkaoFaceDetector>(vision.FaceDetector).Dispose();
        int mark = fixture.Rig.Mark();
        vision.SaveFaceAlbumToRobot();
        fixture.Rig.Tick();
        Assert.DoesNotContain(fixture.Rig.SentSince(mark).OfType<NVCommand>(),
            c => c.Tag is VisionSystem.FaceAlbumNvTag or VisionSystem.FaceEnrollmentNvTag);
    }

    // 332-byte album generated by shipped OKAO from the existing synthetic face_s10_2 image,
    // album entry 3/data 0, capacity (1000,4). SHA-256 af7ea79ea727b395b560b0eb36657d510bbb1b09ff2ac9734f1abd8ced88aac3.
    private static byte[] Album() => Convert.FromBase64String("TAEAAExBvOxTSU5BSTguMAgAAADoAwAABAAAAAEAAAADAAAA6wzeieAP5fD2ADOt6bb+IKQQCewFA9QGAN0M7fIK4vv5yu3A6v4b9xb+58+418/+8wj97BbM5ej01Q8aHia2vxcAwc/jAr4WBw4878YIAyfpK/zl0gwy2AIOBDH7Hfc0MythECne+NrU+TT2Gw3P9P4VKxr+Dvkw7ArwK/4l+uDGAircERAizQ73Av76/P4BAQAAAAAAAADrDN6J4A/l8PYAM63ptv4gpBAJ7AUD1AYA3Qzt8gri+/nK7cDq/hv3Fv7nz7jXz/7zCP3sFszl6PTVDxoeJra/FwDBz+MCvhYHDjzvxggDJ+kr/OXSDDLYAg4EMfsd9zQzK2EQKd742tT5NPYbDc/0/hUrGv4O+TDsCvAr/iX64MYCKtwRECLNDvcC/vr8/gE=");
    private static byte[] Enrollment()
    {
        var entry = new StoredEnrolledFace { FaceID = 7, Name = "Drawn", Timestamp28Seconds = 1, Timestamp30Seconds = 1 };
        entry.AlbumEntryIds.Add(3); entry.AlbumEntrySeconds.Add(1);
        var bytes = new List<byte>();
        bytes.AddRange(BitConverter.GetBytes(StoredEnrolledFace.VersionPrefix));
        bytes.AddRange(BitConverter.GetBytes(8));
        entry.Pack(bytes);
        return bytes.ToArray();
    }

    [Fact]
    public void NativeFramePublishesPositiveMergeBeforeEnrollmentPulseAndObservation()
    {
        if (ShippedLibraries.FindLibDir() is null) return;
        var old = new StoredEnrolledFace { FaceID=7,Name="Drawn",Timestamp28Seconds=1,Timestamp30Seconds=1 };
        old.AlbumEntryIds.Add(3);
        old.AlbumEntrySeconds.Add(1);
        var keep = new StoredEnrolledFace { FaceID=8,Name="Kept",Timestamp28Seconds=1,Timestamp30Seconds=1 };
        var enrollment=new List<byte>();
        enrollment.AddRange(BitConverter.GetBytes(StoredEnrolledFace.VersionPrefix));
        enrollment.AddRange(BitConverter.GetBytes(9));
        old.Pack(enrollment);
        keep.Pack(enrollment);
        using var fixture=new Fixture(sourceNvEmpty:true,sourceNvStored:tag=>tag switch
        {VisionSystem.FaceAlbumNvTag=>Album(),VisionSystem.FaceEnrollmentNvTag=>enrollment.ToArray(),_=>null});
        var vision=fixture.M.Vision;
        long now=10_999_999;
        vision.FaceRecognitionClockForTest=()=>now;
        var source=vision.Recognizer.EnrolledFaces.Single(f=>f.FaceID==7);
        source.Timestamp30Micros=10_000_000;
        vision.SetFaceEnrollmentMode(7);
        vision.Enabled=true;
        vision.ModeEnableMask=(1<<2)|(1<<7);
        vision.Schedules=AllVisionModesSchedule.InitDefaultSchedules();
        fixture.Rig.Robot.SetVisionWhileMoving(true);
        fixture.Rig.Tick();
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","okao","okao_oracle.json")));
        var root=doc.RootElement;
        var images=root.GetProperty("images").EnumerateObject().ToDictionary(p=>p.Name,p=>Convert.FromBase64String(p.Value.GetString()!));
        var frames=root.GetProperty("frames").EnumerateArray().ToDictionary(f=>f.GetProperty("name").GetString()!,f=>images[f.GetProperty("image").GetString()!]);
        var detector=Assert.IsType<OkaoFaceDetector>(vision.FaceDetector);
        uint imageId=800;
        void Frame(string name)
        {
            fixture.Rig.State(RobotStatusFlag.HeadInPos|RobotStatusFlag.LiftInPos);
            fixture.NativeCameraFrame(imageId++,frames[name]);
            fixture.Rig.Engine.VisionUpdateAllResultsFailed!();
            fixture.Rig.Tick();
        }
        Frame("face_s10_1");
        Frame("face_s10_2");
        Assert.True(SpinWait.SpinUntil(()=>detector.RecognitionWorkCompleted,TimeSpan.FromSeconds(10)));
        Frame("face_s10_3");
        Assert.Equal(7,Assert.Single(vision.LastFaces).Id);
        for(int step=1;step<=3;step++)
        {
            now=10_000_000+step*1_000_000;
            for(int attempt=0;attempt<12&&!detector.RecognitionWorkCompleted;attempt++)Frame("face_s10_3");
            Assert.True(SpinWait.SpinUntil(()=>detector.RecognitionWorkCompleted,TimeSpan.FromSeconds(10)));
            Frame("face_s10_3");
            Assert.Equal(4-step,vision.Recognizer.EnrollmentCount);
        }
        Assert.True(vision.AssignStockFaceName(7, "New", 8));
        Assert.Equal("New", vision.Recognizer.EnrolledFaces.Single(f => f.FaceID == 8).Name);
        Assert.Equal("Drawn", vision.Faces.GetFace(7)?.Name);
        var order=new List<string>();
        using var change=fixture.Rig.Engine.AnimationMessages.SubscribeMessage(0x47,true,p=>
        {if(p is NativeChangedFace c && c.OldId==7 && c.NewId==8)order.Add("change");});
        vision.Faces.FaceObserved+=observation=>
        {if(observation.Face.Id==8)order.Add(vision.Faces.EnrollmentCompleted?"pulse-observe":"observe");};
        now=14_000_000;
        for(int attempt=0;attempt<20 && !vision.Faces.EnrollmentCompleted;attempt++)
        {
            if(detector.RecognitionWorkCompleted)Frame("face_s10_3");
            else
            {
                Frame("face_s10_3");
                SpinWait.SpinUntil(()=>detector.RecognitionWorkCompleted,TimeSpan.FromSeconds(10));
            }
        }
        Assert.True(vision.Faces.EnrollmentCompleted);
        Assert.Equal("New", vision.Faces.GetFace(8)?.Name);
        Assert.Equal(8,vision.Recognizer.EnrollmentTargetId);
        Assert.Equal(1,order.Count(x=>x=="change"));
        Assert.Contains("pulse-observe",order);
        Assert.True(order.IndexOf("change")<order.IndexOf("pulse-observe"));
    }

    [Fact]
    public void NativeRecognitionPublishesOneShotEnrollmentBeforeObservations()
    {
        if (ShippedLibraries.FindLibDir() is null) return;
        using var fixture = new Fixture(sourceNvEmpty: true, sourceNvStored: tag => tag switch
        {
            VisionSystem.FaceAlbumNvTag => Album(),
            VisionSystem.FaceEnrollmentNvTag => Enrollment(),
            _ => null,
        });
        var vision = fixture.M.Vision;
        long now = 10_999_999;
        vision.FaceRecognitionClockForTest = () => now;
        var face = Assert.Single(vision.Recognizer.EnrolledFaces);
        face.Timestamp30Micros = 10_000_000;
        vision.SetFaceEnrollmentMode(7);
        Assert.Equal(4, vision.Recognizer.EnrollmentCount);
        vision.Enabled = true;
        vision.ModeEnableMask = (1 << 2) | (1 << 7);
        vision.Schedules = AllVisionModesSchedule.InitDefaultSchedules();
        fixture.Rig.Robot.SetVisionWhileMoving(true);
        fixture.Rig.Tick();
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "okao", "okao_oracle.json")));
        var root = doc.RootElement;
        var images = root.GetProperty("images").EnumerateObject().ToDictionary(p => p.Name, p => Convert.FromBase64String(p.Value.GetString()!));
        var frames = root.GetProperty("frames").EnumerateArray().ToDictionary(f => f.GetProperty("name").GetString()!, f => images[f.GetProperty("image").GetString()!]);
        var detector = Assert.IsType<OkaoFaceDetector>(vision.FaceDetector);
        uint imageId = 500;
        void Frame(string name)
        {
            fixture.Rig.State(RobotStatusFlag.HeadInPos | RobotStatusFlag.LiftInPos);
            fixture.NativeCameraFrame(imageId++, frames[name]);
            fixture.Rig.Engine.VisionUpdateAllResultsFailed!();
            fixture.Rig.Tick();
        }
        bool sawCompletionInObservation = false;
        vision.Faces.FaceObserved += _ => { if (vision.Faces.EnrollmentCompleted) sawCompletionInObservation = true; };
        Frame("face_s10_1");
        Frame("face_s10_2");
        Assert.True(SpinWait.SpinUntil(() => detector.RecognitionWorkCompleted, TimeSpan.FromSeconds(10)));
        Frame("face_s10_3");
        Assert.Equal(4, vision.Recognizer.EnrollmentCount);
        Assert.False(vision.Faces.EnrollmentCompleted);
        for (int step = 1; step <= 4; step++)
        {
            now = 10_000_000 + step * 1_000_000;
            for (int attempt = 0; attempt < 12 && !detector.RecognitionWorkCompleted; attempt++) Frame("face_s10_3");
            Assert.True(SpinWait.SpinUntil(() => detector.RecognitionWorkCompleted, TimeSpan.FromSeconds(10)), $"no FR work at step {step}; faces={vision.LastFaces.Count}");
            Frame("face_s10_3");
            Assert.Equal(4 - step, vision.Recognizer.EnrollmentCount);
        }
        Assert.True(vision.Faces.EnrollmentCompleted);
        Assert.True(sawCompletionInObservation);
        Assert.Equal(0, vision.Recognizer.EnrollmentMode);
        var exported = vision.GetSerializedFaceData();
        Assert.NotEmpty(exported.Album);
        var restored = Assert.Single(FaceRecognizer.DeserializeEnrollment(exported.Enrollment, now, out _) !);
        Assert.Equal(7, restored.FaceID);
        Assert.Equal("Drawn", restored.Name);
        Assert.Equal(14_000_000, restored.Timestamp30Micros);
        Assert.Contains(3, restored.AlbumEntries.Keys);
        vision.SetFaceEnrollmentMode(0);
        Assert.True(vision.Faces.EnrollmentCompleted); // mode setter does not own the sticky reset
        vision.Faces.ResetEnrollmentCompletionForBehavior();
        Assert.False(vision.Faces.EnrollmentCompleted);
        bool callbackReentered = false;
        vision.Log += message =>
        {
            if (message.Contains("No data for enrollmentID=77"))
                callbackReentered = Task.Run(() => vision.SetFaceEnrollmentMode(0)).Wait(TimeSpan.FromSeconds(2));
        };
        vision.SetFaceEnrollmentMode(77);
        Assert.True(callbackReentered); // no vision/recognizer gate is held while delivering the log
    }

    [Fact]
    public void RestoredNativeAlbumMatchesOnLaterCanonicalFrame()
    {
        if (ShippedLibraries.FindLibDir() is null) return;
        byte[] album = Album(), enrollment = Enrollment();
        using var fixture = new Fixture(sourceNvEmpty: true, sourceNvStored: tag => tag switch
        {
            VisionSystem.FaceAlbumNvTag => album,
            VisionSystem.FaceEnrollmentNvTag => enrollment,
            _ => null,
        });
        var vision = fixture.M.Vision;
        Assert.Contains(vision.Recognizer.EnrolledFaces, x => x.FaceID == 7 && x.Name == "Drawn");
        var initialExport = vision.GetSerializedFaceData();
        Assert.Equal(album, initialExport.Album);
        Assert.NotEmpty(initialExport.Enrollment);
        Assert.Empty(vision.Faces.Faces);
        var changes = new List<(int Old, int New)>();
        var order = new List<string>();
        vision.Faces.FaceIdChanged += (oldId, newId) =>
        {
            changes.Add((oldId, newId));
            order.Add("change:" + vision.Faces.GetFace(newId)?.Name);
        };
        vision.Faces.FaceObserved += observation =>
        {
            if (observation.Face.Id == 7) order.Add("observe:" + observation.Face.Name);
        };
        vision.Enabled = true;
        vision.ModeEnableMask = (1 << 2) | (1 << 7);
        vision.Schedules = AllVisionModesSchedule.InitDefaultSchedules();
        fixture.Rig.Robot.SetVisionWhileMoving(true);
        fixture.Rig.Tick();
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "okao", "okao_oracle.json")));
        var root = doc.RootElement;
        var images = root.GetProperty("images").EnumerateObject().ToDictionary(p => p.Name, p => Convert.FromBase64String(p.Value.GetString()!));
        uint id = 100;
        byte[]? lastImage = null;
        foreach (var frame in root.GetProperty("frames").EnumerateArray())
        {
            string name = frame.GetProperty("name").GetString()!;
            fixture.Rig.State(RobotStatusFlag.HeadInPos | RobotStatusFlag.LiftInPos);
            fixture.NativeCameraFrame(id++, images[frame.GetProperty("image").GetString()!]);
            fixture.Rig.Engine.VisionUpdateAllResultsFailed!();
            fixture.Rig.Tick();
            if (name == "face_s10_2")
            {
                Assert.Equal(-1, Assert.Single(vision.LastFaces).Id);
                var detector = Assert.IsType<OkaoFaceDetector>(vision.FaceDetector);
                Assert.True(SpinWait.SpinUntil(() => detector.RecognitionWorkCompleted, TimeSpan.FromSeconds(10)), "native FR extraction did not complete");
            }
            if (name == "face_s10_3") { lastImage = images[frame.GetProperty("image").GetString()!]; break; }
        }
        var recognized = Assert.Single(vision.LastFaces);
        Assert.Equal(7, recognized.Id);
        Assert.Equal("Drawn", recognized.Name);
        Assert.Equal(775, recognized.Detection.Score); // DT confidence, not FR score
        Assert.Equal(new[] { (-1, 7) }, changes);
        Assert.Equal(new[] { "change:Drawn", "observe:Drawn" }, order);
        Assert.Equal("Drawn", vision.Faces.GetFace(7)?.Name);
        fixture.Rig.State(RobotStatusFlag.HeadInPos | RobotStatusFlag.LiftInPos);
        fixture.NativeCameraFrame(id++, lastImage!);
        fixture.Rig.Engine.VisionUpdateAllResultsFailed!();
        fixture.Rig.Tick();
        Assert.Single(changes);
        var exported = vision.GetSerializedFaceData();
        Assert.NotEmpty(exported.Album);
        Assert.NotEmpty(exported.Enrollment);
        int mark = fixture.Rig.Mark();
        vision.SaveFaceAlbumToRobot();
        fixture.Rig.Tick();
        Assert.Contains(fixture.Rig.SentSince(mark).OfType<NVCommand>(),
            command => command.Tag == VisionSystem.FaceAlbumNvTag && command.Op == NvStorageComponent.OpErase);

        int eraseReports = 0;
        vision.EnrolledFacesErasedInternal += () => eraseReports++;
        vision.AdoptFaceAlbum(Array.Empty<byte>(), Array.Empty<byte>());
        Assert.Equal(0, eraseReports);
        Assert.Contains(vision.Recognizer.EnrolledFaces, x => x.FaceID == 7 && x.Name == "Drawn");
    }

    [Fact]
    public void OwnerRemovalDuringIdentityChangeStopsTheOldObservation()
    {
        if (ShippedLibraries.FindLibDir() is null) return;
        byte[] album = Album(), enrollment = Enrollment();
        using var fixture = new Fixture(sourceNvEmpty: true, sourceNvStored: tag => tag switch
        {
            VisionSystem.FaceAlbumNvTag => album,
            VisionSystem.FaceEnrollmentNvTag => enrollment,
            _ => null,
        });
        var vision = fixture.M.Vision;
        var original = fixture.Rig.Engine.Robot;
        int changes = 0, positiveObservations = 0;
        vision.Faces.FaceObserved += observation => { if (observation.Face.Id == 7) positiveObservations++; };
        vision.Faces.FaceIdChanged += (_, _) =>
        {
            changes++;
            fixture.Rig.Engine.Robots.RemoveRobot(1, false);
            fixture.Rig.Engine.Robots.AddRobot(1, withRic: false);
        };
        vision.Enabled = true;
        vision.ModeEnableMask = (1 << 2) | (1 << 7);
        vision.Schedules = AllVisionModesSchedule.InitDefaultSchedules();
        fixture.Rig.Robot.SetVisionWhileMoving(true);
        fixture.Rig.Tick();
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "okao", "okao_oracle.json")));
        var root = doc.RootElement;
        var images = root.GetProperty("images").EnumerateObject().ToDictionary(p => p.Name, p => Convert.FromBase64String(p.Value.GetString()!));
        uint id = 200;
        foreach (var frame in root.GetProperty("frames").EnumerateArray())
        {
            string name = frame.GetProperty("name").GetString()!;
            fixture.Rig.State(RobotStatusFlag.HeadInPos | RobotStatusFlag.LiftInPos);
            fixture.NativeCameraFrame(id++, images[frame.GetProperty("image").GetString()!]);
            // The real vision-result consumer runs under the engine tick owner.
            using (fixture.Rig.Engine.EnterSynchronousOwner())
                fixture.Rig.Engine.VisionUpdateAllResultsFailed!();
            fixture.Rig.Tick();
            if (name == "face_s10_2")
            {
                var detector = Assert.IsType<OkaoFaceDetector>(vision.FaceDetector);
                Assert.True(SpinWait.SpinUntil(() => detector.RecognitionWorkCompleted, TimeSpan.FromSeconds(10)));
            }
            if (name == "face_s10_3") break;
        }
        Assert.Equal(1, changes);
        Assert.Equal(0, positiveObservations);
        Assert.NotSame(original, fixture.Rig.Engine.Robot);
        Assert.Empty(vision.Faces.Faces);
    }
}
