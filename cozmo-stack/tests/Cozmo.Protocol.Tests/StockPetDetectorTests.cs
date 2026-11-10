using System.IO.Compression;
using System.Security.Cryptography;
using Cozmo.Robot.Emulation;
using Cozmo.Robot;
using Cozmo.Protocol;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("ObjectID process statics")]
public sealed class StockPetDetectorTests
{
    internal static readonly Dictionary<string,int> Settings = new()
    {
        ["MaxPets"]=4, ["MinFaceSize"]=60, ["MaxFaceSize"]=240, ["InitialSearchCycle"]=4,
        ["NewSearchCycle"]=8, ["DetectionThreshold"]=900, ["TrackLostCount"]=2, ["TrackSteadiness"]=15
    };

    // Chelsea photograph by Stefan van der Walt, CC0: https://scikit-image.org/docs/stable/api/skimage.data.html#skimage.data.chelsea
    internal static byte[] Photo()
    {
        using var file=File.OpenRead(Path.Combine(AppContext.BaseDirectory,"Fixtures","stock-pet-chelsea.gray.gz"));
        using var gzip=new GZipStream(file,CompressionMode.Decompress);using var output=new MemoryStream();gzip.CopyTo(output);
        byte[] bytes=output.ToArray();Assert.Equal(76800,bytes.Length);
        Assert.Equal("96EE87B0D92B40763309D763DA3C2840F626CED67199047CBED724D915243E1D",Convert.ToHexString(SHA256.HashData(bytes)));
        return bytes;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OriginalGuestTemporalCorpusMatchesRawResults(bool advancingClock)
    {
        string? directory=ShippedLibraries.FindLibDir();if(directory is null)return;
        uint clock=0;
        using var detector=new OkaoPetDetection(directory,()=>advancingClock ? clock+=1000000u : 0);
        Assert.Equal(2,detector.VersionMajor);Assert.Equal(0,detector.VersionMinor);
        var calls=new List<string>();detector.ObserveSetupForTest(calls.Add);
        detector.Initialize(Settings);
        Assert.Equal(new[]{"CreateHandle","CreateResultHandle","SetAngle","MV_SetSearchCycle","SetSizeRange","SetThreshold","MV_SetLostParam","MV_SetSteadinessParam"},calls);
        byte[] photo=Photo(),blank=new byte[76800];
        var counts=new List<int>();var records=new List<int[]>();
        for(int i=0;i<17;i++)
        {
            var result=detector.DetectRaw(new GrayImage(320,240,i==0 || i>=13?blank:photo));
            counts.Add(result.Count);records.AddRange(result.Select(r=>r.Words));
        }
        Assert.Equal(new[]{0,0,0,0,1,1,1,1,1,0,0,0,1,1,1,0,0},counts);
        var expected=new[]
        {
            new[]{800,1048576,1,965,164,116,96,96,0,0,0,256,0,0},
            new[]{800,1048576,1,-1,164,116,96,96,0,0,0,1,0,0},
            new[]{800,1048576,1,-1,164,116,96,96,0,0,0,1,0,0},
            new[]{800,1048576,1,-1,164,116,96,96,0,0,0,1,1,0},
            new[]{800,1048576,1,-1,164,116,96,96,0,0,0,1,2,0},
            new[]{800,1048576,2,965,164,116,96,96,0,0,0,256,0,0},
            new[]{800,1048576,2,-1,164,116,96,96,0,0,0,1,1,0},
            new[]{800,1048576,2,-1,164,116,96,96,0,0,0,1,2,0}
        };
        Assert.Equal(expected.Length,records.Count);
        for(int i=0;i<expected.Length;i++)Assert.Equal(expected[i],records[i]);
    }

    [Fact]
    public void GuestRejectsOutOfRangeThresholdAtNativeSetter()
    {
        string? directory=ShippedLibraries.FindLibDir();if(directory is null)return;
        using var detector=new OkaoPetDetection(directory,()=>0);
        var invalid=new Dictionary<string,int>(Settings) { ["DetectionThreshold"]=1001 };
        var failure=Assert.Throws<PetGuestFailure>(()=>detector.Initialize(invalid));
        Assert.Equal("PetTracker.Init.FaceLibSetThresholdFailed",failure.Key);
        Assert.Equal(0x03000000u,failure.Result);
        Assert.NotEqual(0,failure.Code);
    }

    [Fact]
    public void StockGuestFeedsRealCameraMailboxAndNativePetReport()
    {
        if(ShippedLibraries.FindLibDir() is null)return;
        using var scene=new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty:true);
        var vision=scene.M.Vision;
        Assert.IsType<OkaoPetDetector>(vision.PetDetector);Assert.True(vision.PetDetector.IsAvailable);
        vision.Enabled=true;vision.Schedules=AllVisionModesSchedule.InitDefaultSchedules();
        scene.Rig.Robot.SetVisionWhileMoving(true);scene.Rig.Tick();
        var reports=new List<RobotObservedPetReport>();var bodies=new List<byte[]>();
        scene.Rig.Engine.EngineToGameMessage+=(tag,body)=>{if(tag==0x48)bodies.Add(body.ToArray());};
        using var sub=scene.Rig.Engine.AnimationMessages.SubscribeMessage(0x48,true,p=>reports.Add(Assert.IsType<RobotObservedPetReport>(p)));
        byte[] photo=Photo(),blank=new byte[76800];uint id=1000;
        void Frame(byte[] pixels)
        {
            scene.Rig.State(RobotStatusFlag.HeadInPos|RobotStatusFlag.LiftInPos);
            scene.NativeCameraFrame(id++,pixels);scene.Rig.Tick();
        }
        vision.ModeEnableMask=0;Frame(photo);Assert.Empty(reports);
        vision.ModeEnableMask=1<<9;
        for(int i=0;i<13;i++)Frame(i==0?blank:photo);
        Assert.NotEmpty(reports);
        var first=reports[0];Assert.Equal(1,first.PetId);Assert.Equal(PetType.Cat,first.Type);
        Assert.Equal(1u,first.TimesObserved);Assert.Equal(965f,first.Score);
        Assert.Equal(new FaceRect(116,68,96,96),first.Rect);
        Assert.Contains(reports,r=>r.PetId==1 && r.TimesObserved>1 && r.Score==-1f);
        Assert.NotEmpty(bodies);using var reader=new BinaryReader(new MemoryStream(bodies[0]));
        Assert.Equal(1,reader.ReadInt32());Assert.Equal(first.Timestamp,reader.ReadUInt32());
        Assert.Equal(1u,reader.ReadUInt32());Assert.Equal(965f,reader.ReadSingle());
        Assert.Equal(116f,reader.ReadSingle());Assert.Equal(68f,reader.ReadSingle());
        Assert.Equal(96f,reader.ReadSingle());Assert.Equal(96f,reader.ReadSingle());Assert.Equal((byte)PetType.Cat,reader.ReadByte());
    }

    [Fact]
    public void GuestFailureWithholdsTheWholeFrameAndKeepsPriorPetMap()
    {
        if(ShippedLibraries.FindLibDir() is null)return;
        using var scene=new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty:true);
        var vision=scene.M.Vision;var detector=Assert.IsType<OkaoPetDetector>(vision.PetDetector);
        vision.Enabled=true;vision.ModeEnableMask=1<<9;vision.Schedules=AllVisionModesSchedule.InitDefaultSchedules();
        scene.Rig.Robot.SetVisionWhileMoving(true);scene.Rig.Tick();
        var logs=new List<EngineLogEntry>();scene.Rig.Engine.LogEntry+=logs.Add;
        var reports=new List<RobotObservedPetReport>();using var sub=scene.Rig.Engine.AnimationMessages.SubscribeMessage(0x48,true,p=>reports.Add(Assert.IsType<RobotObservedPetReport>(p)));
        byte[] photo=Photo();uint id=2000;
        void Frame(byte[] pixels){scene.Rig.State();scene.NativeCameraFrame(id++,pixels);scene.Rig.Tick();}
        // Seed the existing map directly so the first guest Detect can be intercepted before translation.
        var prior=new DetectedPet(1,PetType.Cat,new FaceRect(116,68,96,96),965){TrackerResultFlag=true};
        Assert.True(vision.Pets.UpdateNative(new[]{prior},1,_=>false,()=>true,(_,_,_)=>{},a=>a()));
        var before=Assert.IsType<PetEntry>(vision.Pets.GetPetByID(1));int count=before.TimesObserved,reportCount=reports.Count,processed=vision.FramesProcessed;
        detector.OverrideDetectResultForTest(7);
        bool error=Cozmo.Transport.EngineErrorState.ErrorFlagSet;
        try
        {
            Frame(photo);
            Assert.Equal(processed,vision.FramesProcessed);Assert.Equal(reportCount,reports.Count);
            Assert.Equal(count,vision.Pets.GetPetByID(1)!.TimesObserved);
            Assert.Contains(logs,l=>l.Name=="PetTracker.Update.OmcvDetectFail" && l.Text.Contains("OMCV Result Code=7"));
            Assert.Contains(logs,l=>l.Name=="VisionSystem.DetectPets.PetTrackerUpdateFailed");
            Assert.Contains(logs,l=>l.Name=="VisionSystem.Update.DetectPetsFailed");
            Assert.True(Cozmo.Transport.EngineErrorState.ErrorFlagSet);
        }
        finally { Cozmo.Transport.EngineErrorState.ErrorFlagSet=error; }
    }

    [Fact]
    public void ReinitializationRetiresOldSessionAndItsQueuedFrame()
    {
        if(ShippedLibraries.FindLibDir() is null)return;
        using var scene=new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty:true);
        var vision=scene.M.Vision;var owner=scene.Robot;
        var old=Assert.IsType<OkaoPetDetector>(vision.PetDetector);Assert.True(old.IsAvailable);
        vision.Enabled=true;vision.ModeEnableMask=1<<9;vision.Schedules=AllVisionModesSchedule.InitDefaultSchedules();
        scene.Rig.Robot.SetVisionWhileMoving(true);scene.Rig.Tick();
        scene.Rig.State();scene.NativeCameraFrame(3000,Photo());
        int before=vision.FramesProcessed;
        var result=vision.InitializeNativeVision(owner);
        Assert.True(result.Available);Assert.Equal(0u,result.Result);
        var replacement=Assert.IsType<OkaoPetDetector>(vision.PetDetector);
        Assert.NotSame(old,replacement);Assert.False(old.IsAvailable);Assert.True(replacement.IsAvailable);
        scene.Rig.Engine.VisionUpdateAllResultsFailed!();scene.Rig.Tick();
        Assert.Equal(before,vision.FramesProcessed);
        var blank=new GrayImage(320,240,new byte[76800]);var photo=new GrayImage(320,240,Photo());
        replacement.Detect(blank,1);
        IReadOnlyList<DetectedPet> last=Array.Empty<DetectedPet>();
        for(int i=0;i<4;i++)last=replacement.Detect(photo,(uint)(i+2));
        Assert.Equal(1,Assert.Single(last).Id);
    }

    [Fact]
    public void RobotReplacementStartsFreshStockTracking()
    {
        if(ShippedLibraries.FindLibDir() is null)return;
        using var scene=new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty:true);
        var vision=scene.M.Vision;var original=Assert.IsType<OkaoPetDetector>(vision.PetDetector);
        var blank=new GrayImage(320,240,new byte[76800]);var photo=new GrayImage(320,240,Photo());
        int FirstId(OkaoPetDetector detector)
        {
            detector.Detect(blank,1);IReadOnlyList<DetectedPet> found=Array.Empty<DetectedPet>();
            for(int i=0;i<4;i++)found=detector.Detect(photo,(uint)(i+2));
            return Assert.Single(found).Id;
        }
        Assert.Equal(1,FirstId(original));
        scene.Rig.Engine.Robots.RemoveRobot(1,false);scene.Rig.Engine.Robots.AddRobot(1,withRic:false);
        var next=Assert.IsType<OkaoPetDetector>(vision.PetDetector);
        Assert.NotSame(original,next);Assert.False(original.IsAvailable);Assert.True(next.IsAvailable);
        Assert.Equal(1,FirstId(next));
        vision.Dispose();Assert.False(next.IsAvailable);
    }

    [Fact]
    public async Task RetirementDuringPublicDetectCompletesWithoutBlockingOwner()
    {
        if(ShippedLibraries.FindLibDir() is null)return;
        using var guest=new OkaoPetDetection(ShippedLibraries.FindLibDir()!,()=>0);guest.Initialize(Settings);
        var detector=new OkaoPetDetector(guest);detector.MarkReady();
        using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();
        detector.OverrideDetectForTest(()=>{entered.Set();release.Wait(TimeSpan.FromSeconds(5));return 1;});
        var running=Task.Run(()=>Assert.Throws<PetGuestFailure>(()=>detector.Detect(new GrayImage(320,240),1)));
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
            var retirement=Task.Run(detector.Retire);
            await retirement.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.False(detector.IsAvailable);
        }
        finally { release.Set(); }
        await running.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(SpinWait.SpinUntil(()=>detector.DisposedForTest,TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void SyntheticInitDoesNotExposeAnOldStockGuest()
    {
        if(ShippedLibraries.FindLibDir() is null)return;
        using var scene=new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty:true);
        var vision=scene.M.Vision;var old=Assert.IsType<OkaoPetDetector>(vision.PetDetector);Assert.True(old.IsAvailable);
        var result=vision.InitializeNativeVision(scene.Robot,scene.Rig.Engine.VisionConfiguration);
        Assert.True(result.Available);Assert.Equal(0u,result.Result);
        Assert.False(old.IsAvailable);Assert.False(vision.PetDetector.IsAvailable);
    }

    [Fact]
    public void DeleteResultFailureStillAttemptsDetectorDeletion()
    {
        if(ShippedLibraries.FindLibDir() is not {} directory)return;
        using var guest=new OkaoPetDetection(directory,()=>0);guest.Initialize(Settings);
        guest.OverrideDeleteResultForTest(7);bool deleted=false;guest.ObserveDeleteHandleForTest(()=>deleted=true);
        var detector=new OkaoPetDetector(guest);detector.MarkReady();var errors=new List<string>();detector.Error=errors.Add;
        detector.Retire();
        Assert.True(deleted);
        Assert.Equal(new[]{"PetTracker.Destructor.OmcvDeleteResultHandleFail"},errors);
        Assert.False(detector.IsAvailable);
    }
}
