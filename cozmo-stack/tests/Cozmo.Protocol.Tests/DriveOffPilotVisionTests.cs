using System.Collections.Concurrent;
using Cozmo.Robot;
using Cozmo.Protocol;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class DriveOffPilotVisionTests
{
    [Fact]
    public void InputRollbackPreservesPendingAndCompletedFrames()
    {
        using var resources=new BackupTestResources();
        using var rig=new M4ControlTests.Rig(engineOptions:resources.Options());
        rig.ToSynced();rig.Calibrate();rig.State(head:-.15f);
        var vision=rig.Robot.Vision;vision.Enabled=true;vision.ModeEnableMask=1<<8;
        vision.Schedules=AllVisionModesSchedule.InitDefaultSchedules();vision.Calibration=CameraCalibration.Nominal();
        vision.SetNativeOuterCalibration(vision.Calibration);
        var priorGate=rig.Engine.HasCameraCalibration;rig.Engine.HasCameraCalibration=()=>false;
        var reports=new List<ProcessedImageReport>();
        using var sub=rig.Engine.AnimationMessages.SubscribeMessage(0x43,true,p=>reports.Add(Assert.IsType<ProcessedImageReport>(p)));
        try
        {
            uint stamp=rig.Engine.Robot!.PoseHistory.NewestTimeStamp;
            byte[] pixels=Enumerable.Repeat((byte)90,320*240).ToArray();
            rig.CameraGrayFrame(1,stamp,pixels);vision.WaitForProcessorIdle();
            Assert.Equal(1,vision.PendingResultCount);
            vision.SkipProcessorPass=true;
            rig.CameraGrayFrame(2,stamp+1,pixels);
            rig.CameraGrayFrame(3,stamp+1,pixels); // equal is accepted
            var pending=Assert.IsType<NativeGroundFrame>(vision.PendingGroundForTest);
            Assert.Equal(stamp+1,vision.NativeInitializationForTest!.LastReceivedTimestamp);
            rig.Robot.CameraSettings.LastProcessedImageTimestamp=777;
            rig.CameraGrayFrame(4,stamp,pixels); // reassembler rejects the first older frame
            rig.CameraGrayFrame(5,stamp,pixels); // next same timestamp reaches SetNextImage
            Assert.Same(pending,vision.PendingGroundForTest);
            Assert.Equal(0u,vision.NativeInitializationForTest.LastReceivedTimestamp);
            Assert.Equal(0u,rig.Robot.CameraSettings.LastProcessedImageTimestamp);
            Assert.Equal(1,vision.PendingResultCount);
            rig.CameraGrayFrame(6,stamp,pixels); // the reset permits the next input
            Assert.NotSame(pending,vision.PendingGroundForTest);
            Assert.Equal(stamp,vision.NativeInitializationForTest.LastReceivedTimestamp);
            rig.Engine.HasCameraCalibration=()=>true;rig.Tick();
            Assert.Equal(stamp,Assert.Single(reports).Timestamp);
            rig.Engine.HasCameraCalibration=()=>false;
            rig.CameraGrayFrame(7,uint.MaxValue,pixels);
            Assert.Equal(uint.MaxValue,vision.NativeInitializationForTest.LastReceivedTimestamp);
            rig.Robot.CameraSettings.LastProcessedImageTimestamp=888;
            rig.CameraGrayFrame(8,0,pixels); // reassembly rejects the first backwards image
            rig.CameraGrayFrame(9,0,pixels);
            Assert.Equal(0u,vision.NativeInitializationForTest.LastReceivedTimestamp);
            Assert.Equal(0u,rig.Robot.CameraSettings.LastProcessedImageTimestamp);
        }
        finally { vision.SkipProcessorPass=false;rig.Engine.HasCameraCalibration=priorGate;vision.Dispose(); }
    }

    [Fact]
    public void InputInitializationAndEnabledGatesLeaveClocksUntouched()
    {
        using var resources=new BackupTestResources();
        using var rig=new M4ControlTests.Rig(engineOptions:resources.Options());
        rig.ToSynced();rig.Calibrate();rig.State(head:-.15f);
        var vision=rig.Robot.Vision;var state=vision.NativeInitializationForTest!;
        uint stamp=rig.Engine.Robot!.PoseHistory.NewestTimeStamp;
        byte[] pixels=Enumerable.Repeat((byte)90,320*240).ToArray();
        state.LastReceivedTimestamp=stamp+1;rig.Robot.CameraSettings.LastProcessedImageTimestamp=777;
        vision.Enabled=false;
        rig.CameraGrayFrame(1,stamp,pixels);
        Assert.Equal(stamp+1,state.LastReceivedTimestamp);Assert.Equal(777u,rig.Robot.CameraSettings.LastProcessedImageTimestamp);
        vision.Enabled=true;state.ComponentInitialized=false;
        rig.CameraGrayFrame(2,stamp,pixels);
        Assert.Equal(stamp+1,state.LastReceivedTimestamp);Assert.Equal(777u,rig.Robot.CameraSettings.LastProcessedImageTimestamp);
        state.ComponentAvailabilityKnown=false;
        rig.CameraGrayFrame(3,stamp,pixels);
        Assert.Equal(stamp+1,state.LastReceivedTimestamp);Assert.Equal(777u,rig.Robot.CameraSettings.LastProcessedImageTimestamp);
        vision.Dispose();
    }

    [Fact]
    public void SameOwnerRebindKeepsBothInputAndProcessedClocks()
    {
        using var resources=new BackupTestResources();
        using var rig=new M4ControlTests.Rig(engineOptions:resources.Options());rig.ToSynced();
        var vision=rig.Robot.Vision;var owner=rig.Engine.Robot!;
        vision.NativeInitializationForTest!.LastReceivedTimestamp=123;
        rig.Robot.CameraSettings.LastProcessedImageTimestamp=456;
        vision.BindNativeGroundOwner(owner);
        Assert.Equal(123u,vision.NativeInitializationForTest!.LastReceivedTimestamp);
        Assert.Equal(456u,rig.Robot.CameraSettings.LastProcessedImageTimestamp);
        vision.Dispose();
    }

    [Fact]
    public void OldFrameCallbackReplacementCannotPublishItsTail()
    {
        using var resources=new BackupTestResources();
        using var rig=new M4ControlTests.Rig(engineOptions:resources.Options());
        rig.ToSynced();rig.Calibrate();rig.State(head:-.15f);
        var vision=rig.Robot.Vision;vision.Enabled=true;vision.ModeEnableMask=1<<8;
        vision.Schedules=AllVisionModesSchedule.InitDefaultSchedules();vision.Calibration=CameraCalibration.Nominal();
        vision.SetNativeOuterCalibration(vision.Calibration);
        var priorGate=rig.Engine.HasCameraCalibration;rig.Engine.HasCameraCalibration=()=>false;
        var original=rig.Engine.Robot;var reports=new List<ProcessedImageReport>();bool replaced=false;
        using var sub=rig.Engine.AnimationMessages.SubscribeMessage(0x43,true,p=>reports.Add(Assert.IsType<ProcessedImageReport>(p)));
        void Replace(VisionFrameResult _)
        {
            replaced=true;rig.Engine.Robots.RemoveRobot(1,false);rig.Engine.Robots.AddRobot(1,withRic:false);
        }
        vision.FrameProcessed+=Replace;
        try
        {
            uint stamp=rig.Engine.Robot!.PoseHistory.NewestTimeStamp;
            rig.CameraGrayFrame(1,stamp,Enumerable.Repeat((byte)80,320*240).ToArray());
            vision.WaitForProcessorIdle();Assert.Equal(1,vision.PendingResultCount);
            rig.Engine.HasCameraCalibration=()=>true;rig.Tick();
            Assert.True(replaced);Assert.NotSame(original,rig.Engine.Robot);
            Assert.Equal(0u,rig.Robot.CameraSettings.LastProcessedImageTimestamp);Assert.Empty(reports);
            vision.FrameProcessed-=Replace;
            var initialized=vision.InitializeNativeVision(rig.Engine.Robot!);
            Assert.True(initialized.Available && initialized.Result==0);
            rig.Data(new SyncTimeAck());rig.Tick();
            rig.State(origin:rig.Engine.Robot!.CurrentOriginId,head:-.15f);
            vision.Enabled=true;vision.ModeEnableMask=1<<8;
            vision.Schedules=AllVisionModesSchedule.InitDefaultSchedules();vision.Calibration=CameraCalibration.Nominal();
            vision.SetNativeOuterCalibration(vision.Calibration);
            rig.Engine.HasCameraCalibration=()=>false;
            uint next=rig.Engine.Robot!.PoseHistory.NewestTimeStamp;
            rig.CameraGrayFrame(2,next,Enumerable.Repeat((byte)81,320*240).ToArray());
            vision.WaitForProcessorIdle();Assert.True(vision.PendingResultCount==1,$"init={vision.NativeInitializationForTest?.Initialized} next={next} logs={string.Join(" | ",rig.Log.TakeLast(12))}");
            rig.Engine.HasCameraCalibration=()=>true;rig.Tick();
            Assert.Equal(next,Assert.Single(reports).Timestamp);
            Assert.Equal(next,rig.Robot.CameraSettings.LastProcessedImageTimestamp);
        }
        finally { vision.FrameProcessed-=Replace;rig.Engine.HasCameraCalibration=priorGate;vision.Dispose(); }
    }

    [Fact]
    public void DisposedVisionCannotPublishTheCurrentFrameTail()
    {
        using var resources=new BackupTestResources();
        using var rig=new M4ControlTests.Rig(engineOptions:resources.Options());
        rig.ToSynced();rig.Calibrate();rig.State(head:-.15f);
        var vision=rig.Robot.Vision;vision.Enabled=true;vision.ModeEnableMask=1<<8;
        vision.Schedules=AllVisionModesSchedule.InitDefaultSchedules();vision.Calibration=CameraCalibration.Nominal();
        vision.SetNativeOuterCalibration(vision.Calibration);
        var priorGate=rig.Engine.HasCameraCalibration;rig.Engine.HasCameraCalibration=()=>false;
        var reports=new List<ProcessedImageReport>();
        using var sub=rig.Engine.AnimationMessages.SubscribeMessage(0x43,true,p=>reports.Add(Assert.IsType<ProcessedImageReport>(p)));
        vision.FrameProcessed+=_=>vision.Dispose();
        try
        {
            uint stamp=rig.Engine.Robot!.PoseHistory.NewestTimeStamp;
            rig.CameraGrayFrame(1,stamp,Enumerable.Repeat((byte)80,320*240).ToArray());
            vision.WaitForProcessorIdle();Assert.Equal(1,vision.PendingResultCount);
            rig.Engine.HasCameraCalibration=()=>true;rig.Tick();
            Assert.Equal(0u,rig.Robot.CameraSettings.LastProcessedImageTimestamp);Assert.Empty(reports);
        }
        finally { rig.Engine.HasCameraCalibration=priorGate;vision.Dispose(); }
    }

    [Fact]
    public void ProcessedImagePreservesRawMeanAndExactWireOrder()
    {
        // Source V1-V4: rawA7 is not a Boolean. This is a serializer unit oracle.
        var report=new ProcessedImageReport(0x01020304,Array.AsReadOnly(new uint[]{1,8}),0xA7);
        Assert.Equal("04030201020100000008000000A7",Convert.ToHexString(report.PackBody()));
    }

    [Fact]
    public void RealWorkerQueuesMultipleMeanResultsUntilActualEngineDrain()
    {
        // Reach: actual worker decode/mean producer and native engine drain/API.
        // Raw gray camera frames and calibration are explicit fixture inputs;
        // this does not claim the robot JPEG ingress or live quality ROI collector.
        using var resources=new BackupTestResources();
        using var rig=new M4ControlTests.Rig(engineOptions:resources.Options());rig.ToSynced();rig.Calibrate();
        var vision=rig.Robot.Vision;vision.Enabled=true;vision.ModeEnableMask=1<<8;
        vision.Schedules=AllVisionModesSchedule.InitDefaultSchedules(); // supplied schedule for this isolated producer unit
        vision.Calibration=CameraCalibration.Nominal();
        rig.State(head:-.15f);
        vision.SetNativeOuterCalibration(vision.Calibration); // supplied outer calibration for this worker unit
        var reports=new ConcurrentQueue<ProcessedImageReport>();
        using var sub=rig.Engine.AnimationMessages.SubscribeMessage(0x43,true,p=>reports.Enqueue(Assert.IsType<ProcessedImageReport>(p)));
        try
        {
            Assert.False(rig.Engine.VisionUpdateAllResultsFailed!());
            foreach(byte value in new byte[]{167,23})
            {
                rig.State(head:-.15f);
                uint stamp=rig.Engine.Robot!.PoseHistory.NewestTimeStamp;
                var bytes=Enumerable.Repeat(value,320*240).ToArray();
                rig.CameraGrayFrame(value,stamp,bytes);
                vision.WaitForProcessorIdle();
            }
            Assert.Equal(2,vision.PendingResultCount);Assert.Empty(reports);
            Assert.False(rig.Engine.VisionUpdateAllResultsFailed!());
            Assert.Equal(new byte[]{167,23},reports.Select(r=>r.ImageMean));
            Assert.All(reports,r=>Assert.Equal(new uint[]{8},r.Modes));
            Assert.Equal(0,vision.PendingResultCount);
            Assert.False(rig.Engine.VisionUpdateAllResultsFailed!());
        }
        finally{vision.Dispose();} // join worker before rig/history/pose owners.
    }

    [Fact]
    public void RealQualityProducerTransportAppliesCameraBeforeProcessedReportWithExplicitRoi()
    {
        // Real worker/ExposurePipeline/mode7 header/receiver/camera/API transport.
        // ROI is an explicit bounded full-frame fixture input. Live rectangle
        // collection and robot JPEG transport remain unverified by this test.
        using var resources=new BackupTestResources();
        using var rig=new M4ControlTests.Rig(engineOptions:resources.Options());
        rig.ToSynced();rig.Calibrate();rig.State(head:-.15f);
        var vision=rig.Robot.Vision;vision.Calibration=CameraCalibration.Nominal();
        vision.SetNativeOuterCalibration(vision.Calibration); // supplied outer calibration for this worker unit
        vision.Enabled=true;vision.ModeEnableMask=(1<<7)|(1<<8);
        vision.Schedules=AllVisionModesSchedule.InitDefaultSchedules();
        vision.QualityRectangleInput=()=>new[]{new ImageQualityRectangle(0,0,320,240)};
        var order=new ConcurrentQueue<string>();var reports=new ConcurrentQueue<ProcessedImageReport>();
        using var camera=rig.Engine.AnimationMessages.SubscribeMessage(0x2F,true,_=>order.Enqueue("camera"));
        using var processed=rig.Engine.AnimationMessages.SubscribeMessage(0x43,true,p=>{reports.Enqueue(Assert.IsType<ProcessedImageReport>(p));order.Enqueue("processed");});
        try
        {
            Assert.IsType<ExposurePipeline>(vision.QualityProcessor.Pipeline);
            uint stamp=rig.Engine.Robot!.PoseHistory.NewestTimeStamp;
            rig.CameraGrayFrame(1,stamp,Enumerable.Repeat((byte)100,320*240).ToArray());
            vision.WaitForProcessorIdle();Assert.Equal(1,vision.PendingResultCount);
            Assert.False(rig.Engine.VisionUpdateAllResultsFailed!());
            var report=Assert.Single(reports);Assert.Equal(new uint[]{7,8},report.Modes);Assert.Equal((byte)100,report.ImageMean);
            Assert.Equal(new[]{"camera","processed"},order);
            Assert.NotEqual(0u,vision.QualityStamp);Assert.NotEqual((byte)0,vision.LastObservedQuality);
        }
        finally{vision.Dispose();} // actual worker joins before rig/history/ROI input retirement.
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RealWorkerFailureIsStickyAcrossDrainAndGatesOnlyThatEnginePhase(bool replaceOnSecond)
    {
        // Actual rendered marker worker→world handler→aggregate→Robot.Update gate.
        // World status7 and calibration-presence are explicit substituted inputs;
        // no detector/queue result or completed action is synthesized.
        using var resources=new BackupTestResources();
        using var rig=new M4ControlTests.Rig(engineOptions:resources.Options());rig.ToSynced();rig.Calibrate();
        var vision=rig.Robot.Vision;vision.Enabled=true;vision.Calibration=CameraCalibration.Nominal();vision.ModeEnableMask=1<<1;
        rig.State(head:-.15f);
        // Supplied simulated firmware selects the native nonphysical marker path for this renderer unit.
        rig.Engine.Robot!.HandleFirmwareVersion(new FirmwareVersion{Signature=System.Text.Encoding.UTF8.GetBytes("{\"sim\":true}")});
        vision.SetNativeOuterCalibration(vision.Calibration);
        vision.Schedules=AllVisionModesSchedule.InitDefaultSchedules();
        var reports=new ConcurrentQueue<ProcessedImageReport>();var frames=new ConcurrentQueue<uint>();
        var priorGate=rig.Engine.HasCameraCalibration;
        rig.Engine.HasCameraCalibration=()=>false;
        using var local=rig.Engine.AnimationMessages.SubscribeMessage(0x43,true,p=>reports.Enqueue(Assert.IsType<ProcessedImageReport>(p)));
        var markerCounts=new List<int>();var previousTimestamps=new List<uint>();
        void Processed(VisionFrameResult frame)
        {
            frames.Enqueue(frame.Timestamp);markerCounts.Add(frame.Markers.Count);
            previousTimestamps.Add(rig.Robot.CameraSettings.LastProcessedImageTimestamp);vision.World.LocalizeRobotResultForTest=()=>0;
            if(replaceOnSecond && frames.Count==2)
            { rig.Engine.Robots.RemoveRobot(1,false);rig.Engine.Robots.AddRobot(1,withRic:false); }
        }
        vision.FrameProcessed+=Processed;vision.World.LocalizeRobotResultForTest=()=>7;
        try
        {
            for(uint index=1;index<=2;index++)
            {
                rig.State(RobotStatusFlag.IsBodyAccMode|RobotStatusFlag.HeadInPos,head:-.15f);
                uint stamp=rig.Engine.Robot!.PoseHistory.NewestTimeStamp;
                var pd=vision.History.At(stamp)!.Value;
                var image=new GrayImage(320,240);image.Fill(150);
                using(var objectPose=new Pose3d(Mat3.Identity,new Vec3(150,0,22)))
                using(var cameraPose=pd.CameraPose)
                    MarkerRenderer.DrawObject(image,MarkerLibrary.EmbeddedOrNull??throw new InvalidOperationException("Required marker assets missing"),new CameraModel(vision.Calibration,cameraPose),ObjectType.Block_LIGHTCUBE1,objectPose);
                rig.Data(new ImageImuData{ImageId=index,Line2Number=239});
                rig.Data(new ImageImuData{ImageId=index+10000,Line2Number=239});
                rig.CameraGrayFrame(index,stamp,image.Pixels);
                vision.WaitForProcessorIdle();
            }
            Assert.Equal(2,vision.PendingResultCount);
            rig.Engine.HasCameraCalibration=()=>true;
            var mood=rig.Engine.Robot!.MoodOwner!;uint before=mood.HistoryCounts[0];
            if(replaceOnSecond)
            {
                using(rig.Engine.EnterSynchronousOwner()) Assert.False(rig.Engine.VisionUpdateAllResultsFailed!());
                Assert.Equal(2,frames.Count);Assert.Single(reports);
                return;
            }
            rig.Tick(10);
            Assert.Equal(2,reports.Count);Assert.Equal(2,frames.Count);
            Assert.All(markerCounts, count=>Assert.True(count>0));
            Assert.Equal(frames,reports.Select(r=>r.Timestamp));
            Assert.True(frames.First()<frames.Last());
            Assert.Equal(new[]{0u,frames.First()},previousTimestamps);
            HistRobotState? computed=null;uint key=uint.MaxValue;
            Assert.Equal(0u,rig.Engine.Robot!.PoseHistory.GetComputedNative(frames.First(),ref computed,true,ref key));
            Assert.NotNull(computed);Assert.True(rig.Engine.Robot.PoseHistory.IsValidComputedKey(key));
            Assert.Equal(before,mood.HistoryCounts[0]); // first handler failed; later success did not clear aggregate.
            Assert.Equal(reports.Last().Timestamp,rig.Robot.CameraSettings.LastProcessedImageTimestamp);
            Assert.Equal(0,vision.PendingResultCount);
            rig.Tick(10);Assert.Equal(before+1,mood.HistoryCounts[0]); // empty next drain resets aggregate.
        }
        finally{rig.Engine.HasCameraCalibration=priorGate;vision.FrameProcessed-=Processed;vision.World.LocalizeRobotResultForTest=null;vision.Dispose();}
    }

    [Theory]
    [InlineData((byte)1,(byte)6)]
    [InlineData((byte)2,(byte)4)]
    [InlineData((byte)3,(byte)5)]
    [InlineData((byte)4,(byte)7)]
    public void QualityReceiverUsesStrictUnsignedDebounceAndRawLastReported(byte quality,byte code)
    {
        // Receiver unit: explicit quality proposal; actual7C delivery, no detector claim.
        using var rig=new M4ControlTests.Rig();rig.ToSynced();
        var vision=rig.Robot.Vision;
        var codes=new List<byte>();
        using var sub=rig.Engine.AnimationMessages.SubscribeMessage(0x7C,true,p=>codes.Add(Assert.IsType<EngineErrorCodeReport>(p).Code));
        var proposal=new ImageQualityProposal(quality,1,1);
        Assert.Equal(0u,vision.UpdateImageQuality(uint.MaxValue-100,proposal));
        uint threshold=vision.QualityThreshold;
        uint at=unchecked(uint.MaxValue-100+threshold);
        vision.UpdateImageQuality(at,proposal);Assert.Empty(codes); // strict >
        vision.UpdateImageQuality(unchecked(at+1),proposal);
        Assert.Equal(new[]{code},codes);Assert.Equal(quality,vision.LastReportedQuality);
        vision.UpdateImageQuality(unchecked(at+2+vision.QualityThreshold),proposal);
        Assert.Equal(quality==1?1:2,codes.Count); // raw2/3 do not become Booleantrue.
    }

    [Fact]
    public void QualityReceiverReloadsLiveStateAfterSynchronousBroadcast()
    {
        using var rig=new M4ControlTests.Rig();rig.ToSynced();var vision=rig.Robot.Vision;
        var dark=new ImageQualityProposal(2,1,1);
        vision.UpdateImageQuality(100,dark);
        using var sub=rig.Engine.AnimationMessages.SubscribeMessage(0x7C,true,_=>vision.LastObservedQuality=3);
        vision.UpdateImageQuality(unchecked(101+vision.QualityThreshold),dark);
        Assert.Equal((byte)3,vision.LastReportedQuality); // LIVE post-broadcast reload.
        Assert.Equal((byte)2,vision.LastObservedQuality); // original result common tail.
    }
}
