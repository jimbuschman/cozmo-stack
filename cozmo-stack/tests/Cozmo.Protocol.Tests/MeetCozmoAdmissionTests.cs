using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Cozmo.Robot.Emulation;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class MeetCozmoAdmissionTests
{
    [Fact]
    public void UnnamedSaveTargetIsRejectedBeforeMissingAudioPreflight()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations: true, sourceNvEmpty: true);
        var origin = f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId);
        var face = new TrackedFace(new DetectedFace(7, new FaceRect(0, 0, 20, 20)), 10)
        {
            HeadPose = new Pose3d(Rotation3d.Identity, new Vec3(100, 0, 50)),
            NativeTrackingTranslation = new Vec3(100, 0, 50),
            NativeTrackingRotation = Rotation3d.Identity,
        };
        Assert.NotNull(f.M.Vision.Faces.AddOrUpdateNativeFace(face, Pose3d.Identity, false, origin.Node!));
        var missing = new List<string>();
        void Capture(string line) { if (line.Contains("enrollment authored animation/audio")) missing.Add(line); }
        SteppedBehavior.MissingReported += Capture;
        try
        {
            f.Rig.Robot.ActivateHighLevelActivity("MeetCozmoFindFaces"); f.Rig.Tick();
            var enrollment = Assert.IsType<NativeEnrollFaceBehavior>(f.Runtime.Bound["EnrollFace"]);
            var reports = new List<object?>();
            using var subscription = f.Rig.Engine.AnimationMessages.SubscribeMessage(0x56, true, reports.Add);
            f.Rig.Robot.SetFaceToEnroll("Ada", 7);
            for (int i = 0; i < 20; i++)
            {
                f.Rig.Tick();
                if (enrollment.StagedName.Length == 0) break;
            }
            Assert.Equal("", enrollment.StagedName);
            Assert.True(enrollment.InitFailed);
            Assert.False(enrollment.EngineRunning);
            Assert.Empty(reports);
            Assert.Empty(missing);
        }
        finally { SteppedBehavior.MissingReported -= Capture; }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ConfiguredEnrollmentRejectsPickedUpPlatformAndCarrying(int condition)
    {
        if (!OperatingSystem.IsWindows() || ShippedLibraries.FindLibDir() is null) return;
        string banks = GraphRig.FindMeta() ?? throw new InvalidOperationException("shipped banks missing");
        string archive = Path.Combine(WwiseAssets.SoundDir ?? throw new InvalidOperationException("audio archive missing"), "AudioAssets.zip");
        var options = new CozmoEngineOptions { ResourcesPath = PickupPyramidNativeExecutionTests.Resources(),
            SpeechSoundBankDirectory = banks, SpeechAudioArchivePath = archive, LabLocaleLanguage = "en", BlockPoolPath = "" };
        using var f = new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations: true, pumpAnimations: true, sourceNvEmpty: true, engineOptions: options);
        var sink = new Sink(); f.Robot.CannedSpeechSinkForTest = () => sink;
        f.Rig.Robot.ActivateHighLevelActivity("MeetCozmoFindFaces"); f.Rig.Tick();
        if (condition == 0)
        {
            f.Rig.State(RobotStatusFlag.IsPickedUp | RobotStatusFlag.HeadInPos | RobotStatusFlag.LiftInPos);
            Assert.Equal(OffTreadsState.InAir, f.Rig.Robot.Sensors.OffTreadsState);
        }
        else if (condition == 1) f.Rig.State(RobotStatusFlag.IsOnCharger | RobotStatusFlag.HeadInPos | RobotStatusFlag.LiftInPos);
        else f.Robot.CarryingOwner!.SetCarrying(99);
        f.Rig.Robot.SetFaceToEnroll("Ada"); f.Rig.Tick();
        var enrollment = Assert.IsType<NativeEnrollFaceBehavior>(f.Runtime.Bound["EnrollFace"]);
        Assert.Equal("Ada", enrollment.StagedName);
        if (condition == 1) Assert.True(f.Rig.Robot.Sensors.OnChargerPlatform);
        Assert.False(enrollment.IsRunnable(f.Runtime.Context));
        var chooser = Assert.IsType<ScoringChooser>(f.Runtime.Tree.Single(activity => activity.Id == "MeetCozmo").Chooser);
        var decision = chooser.GetDesiredActiveBehavior(f.Robot.BehaviorManagerOwner!.Current, 0, f.Runtime.Context, f.Rig.Engine.Timer.SecondsF);
        Assert.DoesNotContain(decision.Scores, score => score.Id == "EnrollFace" && score.Score > 0);
    }

    [Fact]
    public void StockEnrollmentRequestUsesTheRetainedMeetChooserAndCancelsThroughStop()
    {
        if (!OperatingSystem.IsWindows() || ShippedLibraries.FindLibDir() is null) return;
        string banks = GraphRig.FindMeta() ?? throw new InvalidOperationException("shipped banks missing");
        string archive = Path.Combine(WwiseAssets.SoundDir ?? throw new InvalidOperationException("audio archive missing"), "AudioAssets.zip");
        var options = new CozmoEngineOptions { ResourcesPath = PickupPyramidNativeExecutionTests.Resources(),
            SpeechSoundBankDirectory = banks, SpeechAudioArchivePath = archive, LabLocaleLanguage = "en", BlockPoolPath = "" };
        using var f = new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations: true, pumpAnimations: true, sourceNvEmpty: true, engineOptions: options);
        var sink = new Sink();
        f.Robot.CannedSpeechSinkForTest = () => sink;
        f.Rig.Robot.ActivateHighLevelActivity("MeetCozmoFindFaces");
        f.Rig.Tick();
        var enrollment = Assert.IsType<NativeEnrollFaceBehavior>(f.Runtime.Bound["EnrollFace"]);
        var reports = new List<FaceEnrollmentCompletedReport>();
        using var subscription = f.Rig.Engine.AnimationMessages.SubscribeMessage(0x56, true, value => reports.Add(Assert.IsType<FaceEnrollmentCompletedReport>(value)));
        f.Rig.Robot.SetFaceToEnroll("Ada");
        for (int i = 0; i < 20 && !enrollment.EngineRunning; i++) f.Rig.Tick();
        Assert.True(enrollment.EngineRunning);
        Assert.Same(enrollment, f.Robot.BehaviorManagerOwner!.Current);
        Assert.Equal(2, enrollment.Phase);
        Assert.False(f.Robot.BehaviorManagerOwner.IsReactionTriggerEnabled(ReactionTrigger.FacePositionUpdated));
        f.Rig.Robot.CancelFaceEnrollment();
        for (int i = 0; i < 20 && reports.Count == 0; i++) f.Rig.Tick();
        Assert.Equal((byte)6, Assert.Single(reports).Result);
        Assert.Equal("", enrollment.StagedName);
        Assert.True(f.Robot.BehaviorManagerOwner.IsReactionTriggerEnabled(ReactionTrigger.FacePositionUpdated));
    }

    private sealed class GateAction : ActionRunner
    {
        internal GateAction() : base(8, 0) { }
        protected override string ActionName => "reaction gate fixture";
        public override bool CanInterrupt() => false;
        public override uint CheckIfDone() => 0;
    }
    private sealed class FaceDetector(bool named=false) : IFaceDetector
    {
        public bool IsAvailable => true;
        public string Description => "supplied current camera face";
        public IReadOnlyList<DetectedFace> Detect(GrayImage image, uint timestamp) =>
            new[] { new DetectedFace(named?7:-7, new FaceRect(100, 70, 90, 90), new Vec2(120, 110), new Vec2(160, 110), Name:named?"Ada":null, RollRad: 0) { NativeNewEntryRotatingTooFast = false } };
    }
    private sealed class Sink : IPlatformPcmSink
    {
        private readonly object _gate=new();
        private readonly AutoResetEvent _ready=new(false);
        private readonly Queue<ulong> _submitted=new(),_done=new();
        public WaitHandle CompletionReady=>_ready;
        public void Submit(ulong token,byte[] pcm){lock(_gate)_submitted.Enqueue(token);}
        internal void CompleteOne(){lock(_gate)if(_submitted.TryDequeue(out var token)){_done.Enqueue(token);_ready.Set();}}
        public IReadOnlyList<ulong> DrainCompleted(){lock(_gate){var result=_done.ToArray();_done.Clear();return result;}}
        public void Dispose(){lock(_gate)_ready.Dispose();}
    }

    [Theory]
    [InlineData(false,false)]
    [InlineData(true,false)]
    [InlineData(false,true)]
    public async Task PublicMeetRunsVerifiedFaceThroughDriveAndTimedTracking(bool blocked,bool named)
    {
        if(!OperatingSystem.IsWindows() || ShippedLibraries.FindLibDir() is null)return;
        string banks=GraphRig.FindMeta() ?? throw new InvalidOperationException("shipped banks missing");
        string archive=Path.Combine(WwiseAssets.SoundDir ?? throw new InvalidOperationException("audio archive missing"),"AudioAssets.zip");
        var options=new CozmoEngineOptions { ResourcesPath=PickupPyramidNativeExecutionTests.Resources(),
            SpeechSoundBankDirectory=banks,SpeechAudioArchivePath=archive,LabLocaleLanguage="en",BlockPoolPath="" };
        using var f=new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations:true,pumpAnimations:true,sourceNvEmpty:true,engineOptions:options);
        var sink=new Sink();f.Robot.CannedSpeechSinkForTest=()=>sink;
        if(named)f.Robot.BehaviorManagerOwner!.DisableReactionsWithLock("meet-named-fixture",
            Enumerable.Range(0,21).Select(i=>i==(int)ReactionTrigger.FacePositionUpdated).ToArray(),false);
        // Keep the real Needs recipient observable on both the fine-tune and deadline calls.
        var actions=Assert.IsType<Dictionary<string,NeedsActionDelta>>(f.Rig.Engine.Needs.Actions);
        var seeFace=actions["SeeFace"];
        actions["SeeFace"]=seeFace with { CooldownSec=0 };
        f.Head=.1f;
        f.Rig.State(RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.HeadInPos,origin:f.Robot.CurrentOriginId,head:f.Head);
        f.M.Vision.FaceDetector=new FaceDetector(named);
        f.M.Vision.Enabled=true;f.M.Vision.ModeEnableMask=1<<2;f.M.Vision.Schedules=AllVisionModesSchedule.InitDefaultSchedules();
        f.Rig.Robot.ActivateHighLevelActivity("MeetCozmoFindFaces");f.Rig.Tick();
        f.Image(1100,modes:1<<2);
        var interact=Assert.IsType<NativeMeetInteractWithFaces>(f.Runtime.Bound["MeetCozmo_InteractWithFaces"]);
        var objectives=new List<uint>();
        var order=new List<string>();
        var completedActions=new List<string>();
        var verifyTags=new HashSet<uint>();
        var verifyOutcomes=new List<(uint Result,int Phase)>();
        var phases=new List<string>();
        float socialBefore=0, socialAtNeeds=0;
        uint trackTag=0;
        int objectiveMark=-1;
        using var objective=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x8B,true,p=>
        {
            if(p is uint id){objectives.Add(id);if(id==12){order.Add("objective");socialBefore=(float)f.Robot.MoodOwner![EmotionType.Social];trackTag=interact.CurrentRunnerTag;objectiveMark=f.Rig.Mark();}}
        });
        using var completion=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x5A,true,p=>
        {if(p is RobotCompletedAction result){completedActions.Add($"{result.Tag}:{result.ActionType}:{result.Result:X}");if(verifyTags.Contains(result.Tag))verifyOutcomes.Add((result.Result,interact.Phase));if(trackTag!=0 && result.Tag==trackTag)order.Add("completion");}});
        void Needs(string line)
        {
            if(line.StartsWith("needs.action_completed SeeFace:") && objectives.Contains(12))
            {order.Add("needs");socialAtNeeds=(float)f.Robot.MoodOwner![EmotionType.Social];}
        }
        f.Rig.Engine.Needs.Log+=Needs;
        int mark=f.Rig.Mark(), observedMessages=0;
        bool inserted=false;
        var clips=new List<string>();
        byte? activeTag=null;
        string? activeName=null;
        var audibleClips=new HashSet<string>();
        TriggerAnimationAction? CurrentAnimation()
        {
            if(interact.OwnedRunner is not CompoundAction compound)return null;
            var turn=compound.Children.OfType<NativeLastFaceRunner>().FirstOrDefault();
            return turn?.ChildForTest switch
            {
                SayTextAction speech=>speech.ChildForTest,
                TriggerAnimationAction trigger=>trigger,
                _=>compound.Children.OfType<TriggerAnimationAction>().FirstOrDefault()
            };
        }
        uint image=1101;
        int iterations=0;
        f.Rig.DefaultTickMs=0;
        long engineStart=f.Rig.NowNs;
        var elapsed=System.Diagnostics.Stopwatch.StartNew();
        var until=DateTime.UtcNow+TimeSpan.FromSeconds(35);
        while(DateTime.UtcNow<until && !objectives.Contains(12))
        {
            f.Rig.NowNs=engineStart+(long)(elapsed.Elapsed.TotalMilliseconds*1_000_000);
            sink.CompleteOne();
            var stream=f.Rig.Robot.Animations.Scheduler.Stream;
            f.Rig.Data(new AnimationState { NumAudioFramesPlayed=stream.FramesStreamed,NumAnimBytesPlayed=stream.BytesStreamed });
            f.Motors(RobotStatusFlag.IsBodyAccMode);
            if(interact.EngineRunning && interact.Phase==1 && iterations++%10==0)f.Image(image++,modes:1<<2);
            if(interact.EngineRunning && interact.Phase==1 && interact.CurrentRunnerTag!=0)verifyTags.Add(interact.CurrentRunnerTag);
            string phase=$"{interact.Phase}:{interact.EngineRunning}:{f.Robot.BehaviorManagerOwner?.Current?.Id}:{interact.CurrentRunnerTag}";
            if(phases.Count==0 || phases[^1]!=phase)phases.Add(phase);
            if(blocked && !inserted && interact.Phase==1 && f.Rig.Robot.Animations.Scheduler.Playing is { } greeting && greeting.StartsWith("anim_reacttoface_unidentified_"))
            {
                using(f.M.World.EnterPoseMutation())
                {
                    var transform=f.Robot.GetCheckedMainPose().Transform;
                    var point=transform.Apply(new Vec3(25,0,0));
                    f.M.World.Maps.Current!.InsertObject(new[]{new Vec2(point.X-12,point.Y-12),new Vec2(point.X+12,point.Y-12),
                        new Vec2(point.X+12,point.Y+12),new Vec2(point.X-12,point.Y+12)},new SourceMapData(MemoryMapContentType.ObstacleProx,0,0));
                }
                inserted=true;
            }
            var sent=f.Rig.SentSince(mark);
            for(;observedMessages<sent.Count;observedMessages++)
            {
                switch(sent[observedMessages])
                {
                    case StartOfAnimation start when interact.EngineRunning && CurrentAnimation() is { } child && child.AnimationTagForTest==start.AnimId:
                        activeTag=start.AnimId;activeName=child.GetAnimationCompletionName();
                        if(activeName is { } name)clips.Add(name);
                        f.Rig.Data(new AnimationStarted{AnimId=start.AnimId});break;
                    case AudioSample sample when activeName is not null && sample.Samples.Any(b=>b!=0xFF && b!=0x7F):
                        audibleClips.Add(activeName);break;
                    case EndOfAnimation when activeTag is { } tag:
                        f.Rig.Data(new AnimationEnded{AnimId=tag});activeTag=null;activeName=null;break;
                    case ExecutePath execute:
                        f.Rig.Data(new PathFollowingEvent{EventId=execute.EventId,EventType=0});f.Rig.Tick();
                        f.Rig.Data(new PathFollowingEvent{EventId=execute.EventId,EventType=2});break;
                }
            }
            await Task.Delay(20);
        }
        var output=f.Rig.SentSince(mark);
        var currentTurn=(interact.OwnedRunner as CompoundAction)?.Children.OfType<NativeLastFaceRunner>().FirstOrDefault();
        Assert.True(objectives.Contains(12),$"phase={interact.Phase} target={interact.Target} turnState={currentTurn?.FaceState} child={currentTurn?.ChildForTest?.GetType().Name} childState={currentTurn?.ChildForTest?.State:X} animation={CurrentAnimation()?.State:X} animationTag={CurrentAnimation()?.AnimationTagForTest} activeTag={activeTag} phases={string.Join(',',phases.TakeLast(20))} completions={string.Join(',',completedActions.TakeLast(20))} clips={string.Join(',',clips)} playing={f.Rig.Robot.Animations.Scheduler.Playing} audioStreaming={f.Rig.Robot.Animations.Scheduler.AudioStreaming} starts={output.OfType<StartOfAnimation>().Count()} ends={output.OfType<EndOfAnimation>().Count()} audio={output.OfType<AudioSample>().Count()} drive={output.OfType<AppendPathSegmentLine>().Count()} relevantLogs={string.Join('|',f.Rig.Log.Where(x=>x.Contains("audio",StringComparison.OrdinalIgnoreCase)||x.Contains("animation",StringComparison.OrdinalIgnoreCase)||x.Contains("speech",StringComparison.OrdinalIgnoreCase)).TakeLast(20))}");
        Assert.Contains(output.OfType<AudioSample>(),sample=>sample.Samples.Any(b=>b!=0xFF && b!=0x7F));
        var line=Assert.Single(output.OfType<AppendPathSegmentLine>());
        Assert.Equal(blocked?-40f:40f,line.Speed.SpeedMmps);
        if(blocked)Assert.True(inserted);
        Assert.Contains(clips,name=>name.StartsWith(named?"anim_freeplay_reacttoface_sayname_":"anim_reacttoface_unidentified_"));
        Assert.Contains(clips,name=>name.StartsWith("anim_pause_idle_"));
        Assert.Contains(audibleClips,name=>name.StartsWith(named?"anim_freeplay_reacttoface_sayname_":"anim_reacttoface_unidentified_"));
        Assert.Contains(audibleClips,name=>name.StartsWith("anim_pause_idle_"));
        Assert.NotEmpty(verifyOutcomes);
        Assert.Equal(0u,verifyOutcomes[0].Result);
        Assert.True(verifyOutcomes[0].Phase is 3 or 4);
        Assert.Equal(new[]{"objective","completion","needs"},order);
        Assert.True(socialAtNeeds>socialBefore);
        Assert.Contains(f.Rig.SentSince(objectiveMark),message=>message is AbortAnimation);
        Assert.True(interact.Phase is 4 or 5);
        f.Rig.Engine.Needs.Log-=Needs;
        actions["SeeFace"]=seeFace;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void VerifyFailureRetainsRemappedTargetOrRetriesLastFace(bool removeTarget)
    {
        if(!OperatingSystem.IsWindows() || ShippedLibraries.FindLibDir() is null)return;
        string banks=GraphRig.FindMeta() ?? throw new InvalidOperationException("shipped banks missing");
        string archive=Path.Combine(WwiseAssets.SoundDir ?? throw new InvalidOperationException("audio archive missing"),"AudioAssets.zip");
        var options=new CozmoEngineOptions { ResourcesPath=PickupPyramidNativeExecutionTests.Resources(),
            SpeechSoundBankDirectory=banks,SpeechAudioArchivePath=archive,LabLocaleLanguage="en",BlockPoolPath="" };
        using var f=new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations:true,pumpAnimations:true,sourceNvEmpty:true,engineOptions:options);
        var sink=new Sink();f.Robot.CannedSpeechSinkForTest=()=>sink;
        f.Head=.1f;
        f.Rig.State(RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.HeadInPos,origin:f.Robot.CurrentOriginId,head:f.Head);
        f.M.Vision.FaceDetector=new FaceDetector();f.M.Vision.Enabled=true;f.M.Vision.ModeEnableMask=1<<2;
        f.M.Vision.Schedules=AllVisionModesSchedule.InitDefaultSchedules();
        f.Rig.Robot.ActivateHighLevelActivity("MeetCozmoFindFaces");f.Rig.Tick();
        f.Image(1200,modes:1<<2);
        var interact=Assert.IsType<NativeMeetInteractWithFaces>(f.Runtime.Bound["MeetCozmo_InteractWithFaces"]);
        for(int i=0;i<15 && !interact.EngineRunning;i++)f.Motors(RobotStatusFlag.IsBodyAccMode);
        Assert.True(interact.EngineRunning);
        var sequence=Assert.IsType<CompoundActionSequential>(interact.OwnedRunner);
        var turn=Assert.IsType<NativeLastFaceRunner>(Assert.Single(sequence.Children));
        uint firstTag=sequence.Tag;
        if(!removeTarget)
        {
            f.M.Vision.Faces.ChangeFaceID(-7,-8);
            Assert.Equal(-8,interact.Target);
            Assert.Equal(-8,turn.RequestedId);
        }
        for(int i=0;i<25 && turn.FaceState!=1;i++)f.Motors(RobotStatusFlag.IsBodyAccMode);
        Assert.Equal(1,turn.FaceState);
        // The actual wait subscribes to RobotProcessedImage; no face observation is supplied.
        f.Motors(RobotStatusFlag.IsBodyAccMode);
        if(removeTarget)f.M.Vision.Faces.RemoveFaceByID(-7);
        float before=(float)f.Robot.MoodOwner![EmotionType.Confident];
        for(int i=0;i<10;i++)f.Rig.Engine.AnimationMessages.Deliver(0x43,true,new ProcessedImageReport(f.Robot.StoredState!.Timestamp,new uint[]{2},0));
        var completions=new List<(RobotCompletedAction Result,ActionRunner? Next)>();
        using var ended=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x5A,true,p=>
        {if(p is RobotCompletedAction result && result.Tag==firstTag)completions.Add((result,interact.OwnedRunner as ActionRunner));});
        f.Motors(RobotStatusFlag.IsBodyAccMode);
        var completion=Assert.Single(completions);
        Assert.Equal(0x0300000Eu,completion.Result.Result);
        Assert.True(f.Robot.MoodOwner[EmotionType.Confident]<before);
        if(removeTarget)
        {
            Assert.Equal(0,interact.Target);
            var retry=Assert.IsType<CompoundActionSequential>(completion.Next);
            Assert.Equal(0,Assert.IsType<NativeLastFaceRunner>(Assert.Single(retry.Children)).RequestedId);
        }
        else Assert.Null(completion.Next);
    }
    [Fact]
    public void PublicMeetSelectsDistinctConfiguredScannerAndDuplicateKeepsRun()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations: true, sourceNvEmpty: true);
        var manager = f.Robot.BehaviorManagerOwner!;
        var meet = Assert.IsType<NativeFindFacesSocialize>(f.Runtime.Bound["MeetCozmo_FindFaces_Socialize"]);
        var freeplay = Assert.IsType<NativeFindFacesSocialize>(f.Runtime.Bound["FindFaces_socialize"]);
        Assert.NotSame(meet, freeplay);
        int mark = f.Rig.Mark();
        f.Rig.Robot.ActivateHighLevelActivity("MeetCozmoFindFaces");
        f.Rig.Tick();
        Assert.Equal("MeetCozmo", f.Runtime.Observe().Activity);
        Assert.Equal((byte)6, f.Robot.BehaviorPublicState.State.Activity);
        Assert.Contains(FreeplayPauseFlag.GameControl, f.Runtime.Tracker.PausedFlags);
        Assert.Same(meet, manager.Current);
        Assert.NotNull(meet.OwnedRunner);
        Assert.Contains(f.Rig.SentSince(mark), message => message is SetLiftHeight);
        float stamp = manager.ParentSwitchTime;
        uint tag = meet.CurrentRunnerTag;
        f.Rig.Robot.ActivateHighLevelActivity("MeetCozmoFindFaces");
        f.Rig.Tick();
        Assert.Equal(stamp, manager.ParentSwitchTime);
        Assert.Equal(tag, meet.CurrentRunnerTag);
        Assert.Equal(0x55, manager.ActiveSparkId);
        Assert.Equal(0x55, manager.RequestedSparkId);
        f.Rig.Robot.ActivateHighLevelActivity("Selection");
        f.Rig.Tick();
        Assert.Equal("Selection", f.Runtime.Observe().Activity);
        Assert.False(meet.EngineRunning);
    }

    [Fact]
    public void OnChargerSelectsRealDriveOffBeforeMeetScanner()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations: true, sourceNvEmpty: true);
        f.Rig.State(RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.IsOnCharger | RobotStatusFlag.HeadInPos);
        f.Rig.Robot.ActivateHighLevelActivity("MeetCozmoFindFaces");
        f.Rig.Tick();
        Assert.True(f.Rig.Robot.Sensors.OnChargerPlatform);
        Assert.Equal("MeetCozmo", f.Runtime.Observe().Activity);
        Assert.IsType<DriveOffChargerBehavior>(f.Robot.BehaviorManagerOwner!.Current);
        Assert.False(((NativeFindFacesSocialize)f.Runtime.Bound["MeetCozmo_FindFaces_Socialize"]).EngineRunning);
    }

    [Fact]
    public void PositiveInteractLosesToRunningDriveOffButStillConsumesItsDraw()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations: true, sourceNvEmpty: true);
        f.Rig.State(RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.IsOnCharger | RobotStatusFlag.HeadInPos, head: .1f);
        f.Rig.Robot.ActivateHighLevelActivity("MeetCozmoFindFaces"); f.Rig.Tick();
        var drive = Assert.IsType<DriveOffChargerBehavior>(f.Robot.BehaviorManagerOwner!.Current);
        f.Head = .1f;
        f.Rig.State(RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.HeadInPos, origin: f.Robot.CurrentOriginId, head: f.Head);
        f.Rig.Robot.Sensors.SetOnChargerPlatform(false); // native platform clear after contacts leave
        Assert.False(f.Rig.Robot.Sensors.OnChargerPlatform);
        Assert.Same(drive, f.Robot.BehaviorManagerOwner.Current);
        f.M.Vision.FaceDetector = new FaceDetector();
        f.M.Vision.Enabled = true; f.M.Vision.ModeEnableMask = 1 << 2;
        f.M.Vision.Schedules = AllVisionModesSchedule.InitDefaultSchedules();
        f.Image(902, modes: 1 << 2);
        Assert.NotNull(f.M.Vision.Faces.GetNativeFace(f.Robot, -7));
        Assert.Same(drive, f.Robot.BehaviorManagerOwner.Current);
        var chooser = Assert.IsType<ScoringChooser>(f.Runtime.Tree.Single(a => a.Id == "MeetCozmo").Chooser);
        f.Rig.Engine.ContextRandom.SetSeed(71, () => 71);
        var reference = new EngineRandom(); reference.SetSeed(71, () => 71);
        var decision = chooser.GetDesiredActiveBehavior(drive, 0, f.Runtime.Context, f.Rig.Engine.Timer.SecondsF);
        Assert.Same(drive, decision.Behavior);
        Assert.Null(decision.UnbuiltWinner);
        Assert.Contains(decision.Scores, score => score.Id == "MeetCozmo_InteractWithFaces" && score.Score > 1);
        _ = reference.GetNextDbl(); // nonrunning scanner
        _ = reference.GetNextDbl(); // nonrunning Interact, although it loses
        Assert.Equal(reference.GetNextDbl(), f.Rig.Engine.ContextRandom.GetNextDbl());
    }

    [Fact]
    public void CarryingSelectsConfiguredPutDownWhenSearchIsIneligible()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations: true, sourceNvEmpty: true);
        var cube = f.Add();
        using var relative = new Pose3d("");
        f.M.Docking.Carrying.AttachToLift(7, cube.Markers[0], relative, null, null);
        f.Rig.Robot.ActivateHighLevelActivity("MeetCozmoFindFaces"); f.Rig.Tick();
        Assert.Same(f.Runtime.Bound["PutDownBlock"], f.Robot.BehaviorManagerOwner!.Current);
        Assert.False(((NativeFindFacesSocialize)f.Runtime.Bound["MeetCozmo_FindFaces_Socialize"]).EngineRunning);
    }

    [Fact]
    public void MeetSelectionPreservesAnAlreadyRunningReaction()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations: true, sourceNvEmpty: true);
        var manager = f.Robot.BehaviorManagerOwner!;
        f.Robot.ActionList.Clear();
        Assert.Equal(0, f.Robot.ActionList.QueueAction(QueueActionPosition.Now, new GateAction(), 0));
        manager.CheckReactions(f.Rig.Engine.Timer.Seconds);
        f.Robot.ActionList.Clear();
        f.Rig.Data(new MotorCalibration { MotorID = MotorID.MOTOR_HEAD, CalibStarted = true, AutoStarted = true });
        f.Rig.Tick(); manager.CheckReactions(f.Rig.Engine.Timer.Seconds);
        var reaction = Assert.IsType<ReactToMotorCalibrationBehavior>(manager.Current);
        f.Rig.Robot.ActivateHighLevelActivity("MeetCozmoFindFaces"); f.Rig.Tick();
        Assert.Same(reaction, manager.Current);
        Assert.Equal(ReactionTrigger.MotorCalibration, manager.CurrentReactionTrigger);
        Assert.Equal("MeetCozmo", f.Runtime.Observe().Activity);
        Assert.False(((NativeFindFacesSocialize)f.Runtime.Bound["MeetCozmo_FindFaces_Socialize"]).EngineRunning);
    }

    [Fact]
    public void QueuedMeetRequestCannotSelectAReplacementRobot()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations: true, sourceNvEmpty: true);
        var old = f.Robot;
        using (f.Rig.Engine.EnterSynchronousOwner())
        {
            f.Rig.Robot.ActivateHighLevelActivity("MeetCozmoFindFaces");
            f.Rig.Engine.Robots.RemoveRobot(1, false);
            f.Rig.Engine.Robots.AddRobot(1, withRic: false);
        }
        var replacement = f.Rig.Engine.Robot!;
        Assert.NotSame(old, replacement);
        f.Rig.Tick();
        Assert.NotEqual("MeetCozmo", replacement.NativeBehaviorRuntime?.Observe().Activity);
        Assert.NotEqual((byte)6, replacement.BehaviorPublicState.State.Activity);
    }

    [Fact]
    public void CurrentOriginFaceSelectsInteractAndMissingAudioIsVisibleBeforeMotion()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations: true, sourceNvEmpty: true);
        f.Head = .1f;
        f.Rig.State(RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.HeadInPos, origin: f.Robot.CurrentOriginId, head: f.Head);
        f.Rig.Robot.ActivateHighLevelActivity("MeetCozmoFindFaces"); f.Rig.Tick();
        var manager = f.Robot.BehaviorManagerOwner!;
        var scanner = Assert.IsType<NativeFindFacesSocialize>(manager.Current);
        Assert.Equal("MeetCozmo_FindFaces_Socialize", scanner.Id);
        var missing = new List<string>();
        void Capture(string text) { if (text.Contains("M14-006 Meet Interact")) missing.Add(text); }
        SteppedBehavior.MissingReported += Capture;
        try
        {
            f.M.Vision.FaceDetector = new FaceDetector();
            f.M.Vision.Enabled = true;
            f.M.Vision.ModeEnableMask = 1 << 2;
            f.M.Vision.Schedules = AllVisionModesSchedule.InitDefaultSchedules();
            f.Image(901, modes: 1 << 2);
            Assert.NotNull(f.M.Vision.Faces.GetNativeFace(f.Robot, -7));
            for (int i = 0; i < 4 && missing.Count == 0; i++) f.Rig.Tick();
            Assert.NotEmpty(missing);
            Assert.Contains("shared authored robot audio", missing[0]);
            Assert.False(scanner.EngineRunning);
            Assert.IsType<NativeMeetInteractWithFaces>(f.Runtime.Bound["MeetCozmo_InteractWithFaces"]);
        }
        finally { SteppedBehavior.MissingReported -= Capture; }
    }
}
