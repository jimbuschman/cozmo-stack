using System.Text;
using Cozmo.Robot;
using Cozmo.Robot.Vision;
using Cozmo.Transport;
using Cozmo.Protocol;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("ObjectID process statics")]
public sealed class NativeVisionInitializationTests
{
    private static FirmwareJsonValue Config(int max=4,int min=60,int maxSize=240,int first=4,int next=8,int threshold=900,int lost=2,int steady=15)
    {
        string text=$"{{\"PetTracker\":{{\"MaxPets\":{max},\"MinFaceSize\":{min},\"MaxFaceSize\":{maxSize},\"InitialSearchCycle\":{first},\"NewSearchCycle\":{next},\"DetectionThreshold\":{threshold},\"TrackLostCount\":{lost},\"TrackSteadiness\":{steady}}}}}";
        var value=new FirmwareJsonValue();Assert.True(Json.TryParseInto(Encoding.UTF8.GetBytes(text),value));return value;
    }
    [Fact]
    public void ShippedConfigurationInitializesOriginalOwnerAtConstructorStage()
    {
        var prior=AllVisionModesSchedule.CopyDefault();bool oldError=EngineErrorState.ErrorFlagSet;
        var quality=ImageQualitySettings.Globals;
        var oldQuality=(quality.TooBright,quality.TooDark,quality.MeterFromDetections,quality.Low,quality.Mid,quality.High);
        var oldComponent=(VisionSystem.TimeBeforeQualityErrorMs,VisionSystem.RepeatedQualityErrorIntervalMs,CameraSettings.ConfiguredInitialExposureTimeMs);
        try
        {
            string resources=Ancestors(AppContext.BaseDirectory)
                .Select(dir=>Path.Combine(dir,"re-analysis","obb","assets","cozmo_resources"))
                .First(path=>File.Exists(Path.Combine(path,"config","engine","vision_config.json")));
            using var backup=new BackupTestResources();
            using var rig=new M4ControlTests.Rig(engineOptions:backup.Options(new CozmoEngineOptions{ResourcesPath=resources,BlockPoolPath=""}));
            rig.ToSynced();
            var owner=rig.Engine.Robot!;var vision=rig.Robot.Vision;
            var state=Assert.IsType<NativeVisionInitialization>(vision.NativeInitializationForTest);
            Assert.Same(owner,state.Owner);Assert.True(state.AvailabilityKnown);Assert.True(state.Initialized);
            Assert.Equal(60000,state.ProfilerInfoMs);Assert.Equal(600000,state.ProfilerDasMs);
            Assert.Equal((32d,4,4),(state.ClaheClipLimit,state.ClaheTilesX,state.ClaheTilesY));
            Assert.Equal(0x839E,vision.ModeEnableMask);var front=Assert.IsType<AllVisionModesSchedule>(vision.Schedules);
            Assert.Equal(new[]{true,false},front[(int)VisionMode.DetectingPets].Frames);
            Assert.Equal(new[]{false,true},front[(int)VisionMode.DetectingOverheadEdges].Frames);
            Assert.Equal(15,front[(int)VisionMode.CheckingQuality].Frames.Count);
            Assert.Equal((4,8),(state.Pet.InitialCycle,state.Pet.NewCycle));
            Assert.Equal((60,240),(state.Pet.MinFaceSize,state.Pet.MaxFaceSize));
            Assert.Equal(900,state.Pet.Threshold);Assert.Equal(2,state.Pet.Lost);Assert.Equal(15,state.Pet.Steadiness);
            var face=Assert.IsType<NativeVisionFaceConstructor>(state.Face);
            Assert.NotNull(face.Config); // FaceDetection is copied at 0x86B300..31C.
            var detectionMode=face.Config!.Members[Convert.ToHexString(Encoding.UTF8.GetBytes("DetectionMode"))];
            Assert.Equal("video",Encoding.UTF8.GetString(detectionMode.Bytes));
            Assert.True(face.RecognizerInput.Members.ContainsKey(Convert.ToHexString(Encoding.UTF8.GetBytes("ImageQuality"))));
            Assert.NotNull(state.Motion);Assert.False(owner.VisionNvTailUnavailable);
            rig.State(); // NV dispatch follows the first full state in Robot.Update.
            var orderedTags=new List<uint>();
            for(int i=0;i<40 && orderedTags.Count<4;i++)
            {
                rig.Tick();
                if(rig.Engine.NvStorage!.InFlightTag is not {} tag)continue;
                orderedTags.Add(tag);
                rig.Data(new NVOpResult{Tag=tag,Op=0,Result=1,Data=Array.Empty<byte>()});
                rig.Tick();
            }
            Assert.Equal(new uint[]{0x182000,0x195000,0x184000,0x183000},orderedTags);
            var oldMotion=state.Motion;var oldFront=front;
            var repeat=vision.InitializeNativeVision(owner);
            Assert.True(repeat.Available);Assert.Equal(0u,repeat.Result);
            Assert.NotSame(oldMotion,state.Motion);Assert.NotSame(oldFront,vision.Schedules);
            Assert.Equal(new[]{true,false},vision.Schedules![(int)VisionMode.DetectingPets].Frames);
            // A supplied malformed prefix returns before the system's +58 reset (0x650E36).
            var empty=new FirmwareJsonValue{Kind=FirmwareJsonKind.Object};
            var prefixFailure=vision.InitializeNativeVision(owner,empty);
            Assert.True(prefixFailure.Available);Assert.Equal(1u,prefixFailure.Result);
            Assert.True(state.AvailabilityKnown);Assert.True(state.Initialized);
            // A failure after entering VisionSystem::Init does clear +58 (0x6B0678).
            var systemFailure=new FirmwareJsonValue();
            Assert.True(Json.TryParseInto(File.ReadAllBytes(Path.Combine(resources,"config","engine","vision_config.json")),systemFailure));
            var section=systemFailure.Members[Convert.ToHexString(Encoding.UTF8.GetBytes("ImageQuality"))];
            section.Members.Remove(Convert.ToHexString(Encoding.UTF8.GetBytes("TooBrightValue")));
            var afterEntry=vision.InitializeNativeVision(owner,systemFailure);
            Assert.True(afterEntry.Available);Assert.Equal(1u,afterEntry.Result);
            Assert.True(state.AvailabilityKnown);Assert.False(state.Initialized);
            // 0x6B0976 converts MidValue before the later SubSample lookup.
            var badExposure=new FirmwareJsonValue();
            Assert.True(Json.TryParseInto(File.ReadAllBytes(Path.Combine(resources,"config","engine","vision_config.json")),badExposure));
            var exposure=badExposure.Members[Convert.ToHexString(Encoding.UTF8.GetBytes("ImageQuality"))];
            exposure.Members[Convert.ToHexString(Encoding.UTF8.GetBytes("MidValue"))]=new FirmwareJsonValue{Kind=FirmwareJsonKind.String,Bytes=Encoding.UTF8.GetBytes("bad")};
            exposure.Members.Remove(Convert.ToHexString(Encoding.UTF8.GetBytes("SubSample")));
            var invalid=vision.InitializeNativeVision(owner,badExposure);
            Assert.False(invalid.Available);Assert.False(state.AvailabilityKnown);
            exposure.Members[Convert.ToHexString(Encoding.UTF8.GetBytes("MidValue"))]=new FirmwareJsonValue{Kind=FirmwareJsonKind.UInt,Unsigned=115};
            var missing=vision.InitializeNativeVision(owner,badExposure);
            Assert.True(missing.Available);Assert.Equal(1u,missing.Result);
            var badProfiler=new FirmwareJsonValue();
            Assert.True(Json.TryParseInto(File.ReadAllBytes(Path.Combine(resources,"config","engine","vision_config.json")),badProfiler));
            var perf=badProfiler.Members[Convert.ToHexString(Encoding.UTF8.GetBytes("PerformanceLogging"))];
            perf.Members[Convert.ToHexString(Encoding.UTF8.GetBytes("TimeBetweenProfilerInfoPrints_sec"))]=new FirmwareJsonValue{Kind=FirmwareJsonKind.String,Bytes=Encoding.UTF8.GetBytes("bad")};
            perf.Members.Remove(Convert.ToHexString(Encoding.UTF8.GetBytes("TimeBetweenProfilerDasLogs_sec")));
            Assert.False(vision.InitializeNativeVision(owner,badProfiler).Available);
            perf.Members[Convert.ToHexString(Encoding.UTF8.GetBytes("TimeBetweenProfilerInfoPrints_sec"))]=new FirmwareJsonValue{Kind=FirmwareJsonKind.UInt,Unsigned=60};
            var missingDas=vision.InitializeNativeVision(owner,badProfiler);
            Assert.True(missingDas.Available);Assert.Equal(1u,missingDas.Result);
        }
        finally
        {
            AllVisionModesSchedule.RestoreDefaultForTest(prior);EngineErrorState.ErrorFlagSet=oldError;
            (quality.TooBright,quality.TooDark,quality.MeterFromDetections,quality.Low,quality.Mid,quality.High)=oldQuality;
            (VisionSystem.TimeBeforeQualityErrorMs,VisionSystem.RepeatedQualityErrorIntervalMs,CameraSettings.ConfiguredInitialExposureTimeMs)=oldComponent;
        }
    }
    private static IEnumerable<string> Ancestors(string path)
    { for(var dir=new DirectoryInfo(path);dir is not null;dir=dir.Parent)yield return dir.FullName; }
    [Fact]
    public void MissingOriginalVisionRecipientHoldsOrderedNvTailAndReadiness()
    {
        using var rig=new M4ControlTests.Rig(attachWorld:false);
        rig.ToSynced();
        var owner=rig.Engine.Robot!;
        Assert.True(owner.VisionNvTailUnavailable);
        Assert.False(owner.ReadyToStream);
    }
    [Fact]
    public void SuppliedDefinedInitFailureSkipsAlbumAndKeepsBackupOrder()
    {
        string resources=Ancestors(AppContext.BaseDirectory)
            .Select(dir=>Path.Combine(dir,"re-analysis","obb","assets","cozmo_resources"))
            .First(path=>File.Exists(Path.Combine(path,"config","engine","vision_config.json")));
        using var backup=new BackupTestResources();
        using var rig=new M4ControlTests.Rig(engineOptions:backup.Options(new CozmoEngineOptions{ResourcesPath=resources,BlockPoolPath=""}));
        // Unit seam for the defined nonzero source result; this does not certify normal Init.
        rig.Engine.NativeVisionConstructor=_=>NativeVisionStageResult.Failure(1);
        rig.ToSynced();Assert.False(rig.Engine.Robot!.VisionNvTailUnavailable);rig.State();
        var orderedTags=new List<uint>();
        for(int i=0;i<40 && orderedTags.Count<3;i++)
        {
            rig.Tick();if(rig.Engine.NvStorage!.InFlightTag is not {} tag)continue;
            orderedTags.Add(tag);rig.Data(new NVOpResult{Tag=tag,Op=0,Result=1,Data=Array.Empty<byte>()});rig.Tick();
        }
        Assert.Equal(new uint[]{0x182000,0x195000,0x180000},orderedTags);
    }
    [Fact]
    public void ActualCameraFrameStopsAtKnownUninitializedWorkerAdmission()
    {
        var prior=AllVisionModesSchedule.CopyDefault();bool oldError=EngineErrorState.ErrorFlagSet;
        var quality=ImageQualitySettings.Globals;
        var oldQuality=(quality.TooBright,quality.TooDark,quality.MeterFromDetections,quality.Low,quality.Mid,quality.High);
        var oldComponent=(VisionSystem.TimeBeforeQualityErrorMs,VisionSystem.RepeatedQualityErrorIntervalMs,CameraSettings.ConfiguredInitialExposureTimeMs);
        try
        {
            using var fixture=new PickupPyramidNativeExecutionTests.Fixture();
            var vision=fixture.Rig.Robot.Vision;var owner=fixture.Robot;
            Assert.True(SpinWait.SpinUntil(()=>vision.Enabled,TimeSpan.FromSeconds(5)));
            var input=new FirmwareJsonValue();
            Assert.True(Json.TryParseInto(File.ReadAllBytes(Path.Combine(fixture.Rig.Engine.Options.ResourcesPath!,"config","engine","vision_config.json")),input));
            input.Members[Convert.ToHexString(Encoding.UTF8.GetBytes("ImageQuality"))].Members.Remove(Convert.ToHexString(Encoding.UTF8.GetBytes("TooBrightValue")));
            var result=vision.InitializeNativeVision(owner,input);
            Assert.True(result.Available);Assert.Equal(1u,result.Result);Assert.False(vision.NativeInitializationForTest!.Initialized);
            int before=vision.FramesProcessed;var seen=new ManualResetEventSlim();
            void OnLog(EngineLogEntry entry){if(entry.Name=="VisionSystem.Update.NotInitialized")seen.Set();}
            fixture.Rig.Engine.LogEntry+=OnLog;
            try
            {
                var calibration=vision.Calibration!;var pixels=Enumerable.Repeat((byte)150,calibration.Rows*calibration.Columns).ToArray();
                const int chunk=1200;int count=(pixels.Length+chunk-1)/chunk;uint timestamp=owner.StoredState!.Timestamp;
                for(int i=0;i<count;i++)fixture.Rig.Data(new ImageChunk{ImageId=71,FrameTimestamp=timestamp,ImageEncoding=1,ImageResolution=4,ImageChunkCount=(byte)count,ChunkId=(byte)i,Data=pixels.Skip(i*chunk).Take(chunk).ToArray()});
                fixture.Rig.Tick();
                Assert.True(seen.Wait(TimeSpan.FromSeconds(5)));Assert.Equal(before,vision.FramesProcessed);
            }
            finally {fixture.Rig.Engine.LogEntry-=OnLog;seen.Dispose();}
        }
        finally
        {
            AllVisionModesSchedule.RestoreDefaultForTest(prior);EngineErrorState.ErrorFlagSet=oldError;
            (quality.TooBright,quality.TooDark,quality.MeterFromDetections,quality.Low,quality.Mid,quality.High)=oldQuality;
            (VisionSystem.TimeBeforeQualityErrorMs,VisionSystem.RepeatedQualityErrorIntervalMs,CameraSettings.ConfiguredInitialExposureTimeMs)=oldComponent;
        }
    }
    [Fact]
    public void PrefixLogReplacementCannotContinueOldInitializationIntoNewOwner()
    {
        bool oldError=EngineErrorState.ErrorFlagSet;
        try
        {
            using var rig=new M4ControlTests.Rig();rig.ToSynced();
            var vision=rig.Robot.Vision;var old=rig.Engine.Robot!;EngineRobot? replacement=null;
            bool fired=false;
            void OnLog(EngineLogEntry entry)
            {
                if(fired || entry.Name!="VisionSystem.Init.MissingJsonParameter")return;
                fired=true;rig.Engine.Robots.RemoveRobot(CozmoEngine.RobotId,wasConnecting:false);
                replacement=rig.Engine.Robots.AddRobot(CozmoEngine.RobotId,withRic:false);
            }
            rig.Engine.LogEntry+=OnLog;
            try
            {
                var input=new FirmwareJsonValue{Kind=FirmwareJsonKind.Object};
                var result=vision.InitializeNativeVision(old,input);
                Assert.True(fired);Assert.False(result.Available);Assert.NotNull(replacement);
                Assert.NotSame(old,replacement);Assert.Same(replacement,vision.NativeInitializationForTest!.Owner);
            }
            finally { rig.Engine.LogEntry-=OnLog; }
        }
        finally { EngineErrorState.ErrorFlagSet=oldError; }
    }
    [Fact]
    public void ConstructorLogReentryCannotPublishASecondOwnerForTheSameId()
    {
        bool oldError=EngineErrorState.ErrorFlagSet;
        var prior=AllVisionModesSchedule.CopyDefault();var quality=ImageQualitySettings.Globals;
        var oldQuality=(quality.TooBright,quality.TooDark,quality.MeterFromDetections,quality.Low,quality.Mid,quality.High);
        var oldComponent=(VisionSystem.TimeBeforeQualityErrorMs,VisionSystem.RepeatedQualityErrorIntervalMs,CameraSettings.ConfiguredInitialExposureTimeMs);
        try
        {
            string resources=Ancestors(AppContext.BaseDirectory)
                .Select(dir=>Path.Combine(dir,"re-analysis","obb","assets","cozmo_resources"))
                .First(path=>File.Exists(Path.Combine(path,"config","engine","vision_config.json")));
            using var rig=new M4ControlTests.Rig(resources);
            bool fired=false;Exception? rejection=null;EngineRobot? nested=null;
            void OnLog(EngineLogEntry entry)
            {
                if(fired || entry.Name!="VisionSystem.EnablingMode")return;
                fired=true;
                try { nested=rig.Engine.Robots.AddRobot(CozmoEngine.RobotId,withRic:false); }
                catch(InvalidOperationException e) { rejection=e; }
            }
            rig.Engine.LogEntry+=OnLog;
            try
            {
                var outer=rig.Engine.Robots.AddRobot(CozmoEngine.RobotId,withRic:false);
                Assert.True(fired);Assert.NotNull(outer);Assert.Null(nested);
                Assert.IsType<InvalidOperationException>(rejection);
                Assert.Contains("construction is already in progress",rejection!.Message);
                Assert.Same(outer,rig.Engine.Robots.Get(CozmoEngine.RobotId));
                Assert.Same(outer,rig.Robot.Vision.NativeInitializationForTest!.Owner);
            }
            finally { rig.Engine.LogEntry-=OnLog; }
        }
        finally
        {
            EngineErrorState.ErrorFlagSet=oldError;AllVisionModesSchedule.RestoreDefaultForTest(prior);
            (quality.TooBright,quality.TooDark,quality.MeterFromDetections,quality.Low,quality.Mid,quality.High)=oldQuality;
            (VisionSystem.TimeBeforeQualityErrorMs,VisionSystem.RepeatedQualityErrorIntervalMs,CameraSettings.ConfiguredInitialExposureTimeMs)=oldComponent;
        }
    }
    [Fact]
    public void RetirementClearsOnlyOriginalInitStateAndCalibrationDoesNotReplaceIt()
    {
        bool oldError=EngineErrorState.ErrorFlagSet;
        var prior=AllVisionModesSchedule.CopyDefault();var quality=ImageQualitySettings.Globals;
        var oldQuality=(quality.TooBright,quality.TooDark,quality.MeterFromDetections,quality.Low,quality.Mid,quality.High);
        var oldComponent=(VisionSystem.TimeBeforeQualityErrorMs,VisionSystem.RepeatedQualityErrorIntervalMs,CameraSettings.ConfiguredInitialExposureTimeMs);
        try
        {
            using var resources=new BackupTestResources();using var rig=new M4ControlTests.Rig(resources.Options().ResourcesPath);rig.ToSynced();
            var vision=rig.Robot.Vision;var original=rig.Engine.Robot!;
            var epochField=typeof(VisionSystem).GetField("_nativeInitializationEpoch",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
            int epochBefore=(int)epochField.GetValue(vision)!;
            var oldState=vision.NativeInitializationForTest;
            Assert.NotNull(oldState);Assert.Same(original,oldState!.Owner);
            Assert.Equal(30f,vision.PendingDropStatsSeconds);
            Assert.NotNull(vision.QualityProcessor.Pipeline);Assert.NotNull(vision.Schedules);
            vision.SetNativeOuterCalibration(CameraCalibration.Nominal());
            Assert.Same(oldState,vision.NativeInitializationForTest);
            rig.Engine.Robots.RemoveRobot(CozmoEngine.RobotId,wasConnecting:false);
            Assert.Null(vision.NativeInitializationForTest);Assert.Null(vision.QualityProcessor.Pipeline);
            Assert.Null(vision.Schedules);Assert.Equal(0,vision.ModeEnableMask);
            Assert.Equal(-1f,vision.PendingDropStatsSeconds);
            int retiredEpoch=(int)epochField.GetValue(vision)!;Assert.True(retiredEpoch>epochBefore);
            var replacement=rig.Engine.Robots.AddRobot(CozmoEngine.RobotId,withRic:false);
            Assert.NotNull(replacement);var next=vision.NativeInitializationForTest;
            Assert.NotNull(next);Assert.NotSame(oldState,next);Assert.Same(replacement,next!.Owner);
            Assert.True((int)epochField.GetValue(vision)!>retiredEpoch);
            Assert.False(vision.InitializeNativeVision(original).Available);Assert.Same(next,vision.NativeInitializationForTest);
            Assert.NotNull(vision.QualityProcessor.Pipeline);Assert.NotNull(vision.Schedules);
            Assert.Equal(30f,vision.PendingDropStatsSeconds);
        }
        finally
        {
            EngineErrorState.ErrorFlagSet=oldError;AllVisionModesSchedule.RestoreDefaultForTest(prior);
            (quality.TooBright,quality.TooDark,quality.MeterFromDetections,quality.Low,quality.Mid,quality.High)=oldQuality;
            (VisionSystem.TimeBeforeQualityErrorMs,VisionSystem.RepeatedQualityErrorIntervalMs,CameraSettings.ConfiguredInitialExposureTimeMs)=oldComponent;
        }
    }
    [Fact]
    public void ReentrantReplacementKeepsItsOwnPipelineAndSchedule()
    {
        bool oldError=EngineErrorState.ErrorFlagSet;var prior=AllVisionModesSchedule.CopyDefault();var quality=ImageQualitySettings.Globals;
        var oldQuality=(quality.TooBright,quality.TooDark,quality.MeterFromDetections,quality.Low,quality.Mid,quality.High);
        var oldComponent=(VisionSystem.TimeBeforeQualityErrorMs,VisionSystem.RepeatedQualityErrorIntervalMs,CameraSettings.ConfiguredInitialExposureTimeMs);
        try
        {
            using var resources=new BackupTestResources();using var rig=new M4ControlTests.Rig(resources.Options().ResourcesPath);rig.ToSynced();
            var vision=rig.Robot.Vision;EngineRobot? replacement=null;object? pipeline=null;AllVisionModesSchedule? front=null;
            void OnRemoved()
            {
                replacement=rig.Engine.Robots.AddRobot(CozmoEngine.RobotId,withRic:false);
                pipeline=vision.QualityProcessor.Pipeline;front=vision.Schedules;
            }
            vision.RemovalInvalidated+=OnRemoved;
            try { rig.Engine.Robots.RemoveRobot(CozmoEngine.RobotId,wasConnecting:false); }
            finally { vision.RemovalInvalidated-=OnRemoved; }
            Assert.NotNull(replacement);Assert.NotNull(pipeline);Assert.NotNull(front);
            Assert.Same(replacement,vision.NativeInitializationForTest!.Owner);
            Assert.Same(pipeline,vision.QualityProcessor.Pipeline);Assert.Same(front,vision.Schedules);
            Assert.Equal(30f,vision.PendingDropStatsSeconds);
        }
        finally
        {
            EngineErrorState.ErrorFlagSet=oldError;AllVisionModesSchedule.RestoreDefaultForTest(prior);
            (quality.TooBright,quality.TooDark,quality.MeterFromDetections,quality.Low,quality.Mid,quality.High)=oldQuality;
            (VisionSystem.TimeBeforeQualityErrorMs,VisionSystem.RepeatedQualityErrorIntervalMs,CameraSettings.ConfiguredInitialExposureTimeMs)=oldComponent;
        }
    }
    [Fact]
    public void PetInitUsesOwnedBuffersAndSourceLiteralDefaults()
    {
        var pet=new NativeVisionPetInitialization();Assert.Equal(0u,pet.Initialize(Config()));
        Assert.True(pet.Initialized);Assert.Equal((byte)2,pet.VersionMajor);Assert.Equal((byte)0,pet.VersionMinor);
        Assert.Equal(51472,pet.Common!.Storage.Length);Assert.Equal(0x7C0,pet.Result!.Storage.Length);
        Assert.Equal(0x803803u,pet.AngleMask);Assert.Equal((4,8),(pet.InitialCycle,pet.NewCycle));
        Assert.Equal((60,240),(pet.MinFaceSize,pet.MaxFaceSize));Assert.Equal(900,pet.Threshold);Assert.Equal(2,pet.Lost);Assert.Equal(15,pet.Steadiness);
    }
    [Theory]
    [InlineData(0)] [InlineData(36)]
    public void MaxPetsOutsideNativeRangeFailsCommonHandle(int max)
    {var pet=new NativeVisionPetInitialization();Assert.Equal(0x01000000u,pet.Initialize(Config(max:max)));Assert.False(pet.Initialized);Assert.Null(pet.Common);}
    [Theory]
    [InlineData(0)] [InlineData(1001)]
    public void ThresholdOutsideNativeRangeStopsAfterEarlierSetters(int threshold)
    {
        var pet=new NativeVisionPetInitialization();Assert.Equal(0x03000000u,pet.Initialize(Config(threshold:threshold)));
        Assert.Equal("Threshold",pet.FailedStage);Assert.Equal((4,8),(pet.InitialCycle,pet.NewCycle));Assert.Equal((60,240),(pet.MinFaceSize,pet.MaxFaceSize));Assert.False(pet.Initialized);
    }
    [Theory]
    [InlineData(1)] [InlineData(2)]
    public void RealAllocationFailureIsReportedAtItsActualStage(int failingAllocation)
    {
        int calls=0;var pet=new NativeVisionPetInitialization { Allocator=size=>++calls==failingAllocation?throw new OutOfMemoryException():new byte[size] };
        Assert.Equal(0x01000000u,pet.Initialize(Config()));Assert.False(pet.Initialized);Assert.Equal(failingAllocation,calls);
        Assert.Equal(failingAllocation==1?"CommonHandle":"ResultHandle",pet.FailedStage);
        Assert.Equal(failingAllocation==2,pet.Common is not null);Assert.Null(pet.Result);
    }
    [Fact]
    public void MissingPetParameterPreservesEarlierReadsAndStopsBeforeAllocation()
    {
        var root=Config();
        var petObject=root.Members[Convert.ToHexString(Encoding.UTF8.GetBytes("PetTracker"))];
        petObject.Members.Remove(Convert.ToHexString(Encoding.UTF8.GetBytes("NewSearchCycle")));
        var pet=new NativeVisionPetInitialization();Assert.Equal(0x03000000u,pet.Initialize(root));
        Assert.Equal("NewSearchCycle",pet.FailedStage);Assert.Equal(4,pet.Parameters["InitialSearchCycle"]);
        Assert.False(pet.Parameters.ContainsKey("DetectionThreshold"));Assert.Null(pet.Common);Assert.Null(pet.Result);
        Assert.Equal(0u,pet.Initialize(Config()));var oldCommon=pet.Common;
        Assert.Equal(0x03000000u,pet.Initialize(root));Assert.False(pet.Parameters.ContainsKey("DetectionThreshold"));Assert.Same(oldCommon,pet.Common);
    }
    [Theory]
    [InlineData(19,240)] [InlineData(60,8193)] [InlineData(241,240)]
    public void BadSizeStopsAfterCycleSetter(int min,int max)
    {
        var pet=new NativeVisionPetInitialization();Assert.Equal(0x03000000u,pet.Initialize(Config(min:min,maxSize:max)));
        Assert.Equal("Size",pet.FailedStage);Assert.Equal((4,8),(pet.InitialCycle,pet.NewCycle));Assert.Equal(0,pet.Threshold);
    }
    [Theory]
    [InlineData(0,8)] [InlineData(46,8)] [InlineData(4,4)] [InlineData(4,46)]
    public void BadSearchCycleStopsBeforeSize(int first,int next)
    {
        var pet=new NativeVisionPetInitialization();Assert.Equal(0x03000000u,pet.Initialize(Config(first:first,next:next)));
        Assert.Equal("SearchCycles",pet.FailedStage);Assert.Equal(0,pet.MinFaceSize);
    }
    [Fact]
    public void BadLostAndSteadinessRetainEarlierNativeSetters()
    {
        var lost=new NativeVisionPetInitialization();Assert.Equal(0x03000000u,lost.Initialize(Config(lost:16)));
        Assert.Equal("Lost",lost.FailedStage);Assert.Equal(900,lost.Threshold);Assert.False(lost.Initialized);
        var steady=new NativeVisionPetInitialization();Assert.Equal(0x03000000u,steady.Initialize(Config(steady:31)));
        Assert.Equal("Steadiness",steady.FailedStage);Assert.Equal(2,steady.Lost);Assert.False(steady.Initialized);
    }
    [Fact]
    public void ReinitStoresFailedHandleSlotWithoutResettingTheLaterSlot()
    {
        var pet=new NativeVisionPetInitialization();Assert.Equal(0u,pet.Initialize(Config()));
        var priorResult=pet.Result;
        Assert.Equal(0x01000000u,pet.Initialize(Config(max:0)));
        Assert.Null(pet.Common);Assert.Same(priorResult,pet.Result);Assert.False(pet.Initialized);
        Assert.Equal(0u,pet.Initialize(Config()));
        var priorCommon=pet.Common;int calls=0;
        pet.Allocator=size=>++calls==2?throw new OutOfMemoryException():new byte[size];
        Assert.Equal(0x01000000u,pet.Initialize(Config()));
        Assert.NotNull(pet.Common);Assert.NotSame(priorCommon,pet.Common);Assert.Null(pet.Result);Assert.False(pet.Initialized);
    }
    [Theory]
    [InlineData(1)] [InlineData(35)]
    public void NativeCommonArenaFitsBothValidMaxPetsBoundaries(int max)
    {
        // 93483C requests 49792+48*n; 51472-byte arena covers every n in 1..35.
        var pet=new NativeVisionPetInitialization();Assert.Equal(0u,pet.Initialize(Config(max:max)));
        Assert.True(pet.Initialized);Assert.Equal(51472,pet.Common!.Storage.Length);
    }
    [Fact]
    public void EarlierDefaultScheduleStoreSurvivesLaterDefinedFailure()
    {
        var prior=AllVisionModesSchedule.CopyDefault();bool oldError=EngineErrorState.ErrorFlagSet;
        try
        {
            using var rig=new M4ControlTests.Rig();var vision=rig.Robot.Vision;
            var root=new FirmwareJsonValue();
            Assert.True(Json.TryParseInto(Encoding.UTF8.GetBytes("{\"InitialModeSchedules\":{\"Idle\":false,\"DetectingMarkers\":\"invalid\"}}"),root));
            var result=vision.ApplyNativeInitialSchedules(root,()=>true);
            Assert.True(result.Available);Assert.Equal(1u,result.Result);
            Assert.Equal(new[]{false},AllVisionModesSchedule.CopyDefault()[0].Frames);
            var nullItem=new FirmwareJsonValue();Assert.True(Json.TryParseInto(Encoding.UTF8.GetBytes("{\"InitialModeSchedules\":{\"DetectingMarkers\":null}}"),nullItem));
            var nullResult=vision.ApplyNativeInitialSchedules(nullItem,()=>true);Assert.True(nullResult.Available);Assert.Equal(1u,nullResult.Result);
            Assert.Equal(new[]{false},AllVisionModesSchedule.CopyDefault()[0].Frames);
            var next=new FirmwareJsonValue();Assert.True(Json.TryParseInto(Encoding.UTF8.GetBytes("{\"InitialModeSchedules\":{}}"),next));
            Assert.Equal(0u,vision.ApplyNativeInitialSchedules(next,()=>true).Result);
            Assert.Equal(new[]{false},AllVisionModesSchedule.CopyDefault()[0].Frames);
        }
        finally { AllVisionModesSchedule.RestoreDefaultForTest(prior);EngineErrorState.ErrorFlagSet=oldError; }
    }
    [Fact]
    public void ScheduleArrayUsesJsonCppBooleanConversions()
    {
        var prior=AllVisionModesSchedule.CopyDefault();bool oldError=EngineErrorState.ErrorFlagSet;
        try
        {
            using var rig=new M4ControlTests.Rig();var vision=rig.Robot.Vision;
            var input=new FirmwareJsonValue();
            Assert.True(Json.TryParseInto(Encoding.UTF8.GetBytes("{\"InitialModeSchedules\":{\"Idle\":[0,1,null,2.5,false]}}"),input));
            var result=vision.ApplyNativeInitialSchedules(input,()=>true);
            Assert.True(result.Available);Assert.Equal(0u,result.Result);
            Assert.Equal(new[]{false,true,false,true,false},AllVisionModesSchedule.CopyDefault()[0].Frames);
        }
        finally { AllVisionModesSchedule.RestoreDefaultForTest(prior);EngineErrorState.ErrorFlagSet=oldError; }
    }
    [Fact]
    public void IntegralRealScheduleUsesNativeIsIntWhileFractionalRealFails()
    {
        // Native Json::Value::isInt 0x8E8948..8E898A: inclusive double endpoints.
        Assert.True(VisionSystem.NativeScheduleIsInt(FirmwareJsonValue.FromReal(-2147483648d)));
        Assert.True(VisionSystem.NativeScheduleIsInt(FirmwareJsonValue.FromReal(2147483647d)));
        Assert.False(VisionSystem.NativeScheduleIsInt(FirmwareJsonValue.FromReal(-2147483649d)));
        Assert.False(VisionSystem.NativeScheduleIsInt(FirmwareJsonValue.FromReal(2147483648d)));
        Assert.False(VisionSystem.NativeScheduleIsInt(FirmwareJsonValue.FromReal(double.NaN)));
        var prior=AllVisionModesSchedule.CopyDefault();bool oldError=EngineErrorState.ErrorFlagSet;
        try
        {
            using var rig=new M4ControlTests.Rig();var vision=rig.Robot.Vision;
            var input=new FirmwareJsonValue();
            Assert.True(Json.TryParseInto(Encoding.UTF8.GetBytes("{\"InitialModeSchedules\":{\"Idle\":2.0}}"),input));
            var valid=vision.ApplyNativeInitialSchedules(input,()=>true);
            Assert.True(valid.Available);Assert.Equal(0u,valid.Result);
            Assert.Equal(new[]{true,false},AllVisionModesSchedule.CopyDefault()[0].Frames);
            var fractional=new FirmwareJsonValue();
            Assert.True(Json.TryParseInto(Encoding.UTF8.GetBytes("{\"InitialModeSchedules\":{\"Idle\":1.5}}"),fractional));
            var invalid=vision.ApplyNativeInitialSchedules(fractional,()=>true);
            Assert.True(invalid.Available);Assert.Equal(1u,invalid.Result);
            Assert.Equal(new[]{true,false},AllVisionModesSchedule.CopyDefault()[0].Frames);
        }
        finally {AllVisionModesSchedule.RestoreDefaultForTest(prior);EngineErrorState.ErrorFlagSet=oldError;}
    }
}
