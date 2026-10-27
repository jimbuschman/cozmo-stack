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
        using var rig=new M4ControlTests.Rig();rig.ToSynced();rig.Calibrate();
        var vision=rig.Robot.Vision;vision.Enabled=true;vision.ModeEnableMask=1<<8;
        vision.Calibration=CameraCalibration.Nominal();
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
                vision.HandOverFrame(new CameraFrame{ImageId=value,Timestamp=stamp,Width=320,Height=240,Encoding=1,RawPayload=bytes});
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
        using var rig=new M4ControlTests.Rig(resources.Options().ResourcesPath);
        rig.ToSynced();rig.Calibrate();rig.State(head:-.15f);
        var vision=rig.Robot.Vision;vision.Calibration=CameraCalibration.Nominal();
        vision.Enabled=true;vision.ModeEnableMask=(1<<7)|(1<<8);
        vision.QualityRectangleInput=()=>new[]{new ImageQualityRectangle(0,0,320,240)};
        var order=new ConcurrentQueue<string>();var reports=new ConcurrentQueue<ProcessedImageReport>();
        using var camera=rig.Engine.AnimationMessages.SubscribeMessage(0x2F,true,_=>order.Enqueue("camera"));
        using var processed=rig.Engine.AnimationMessages.SubscribeMessage(0x43,true,p=>{reports.Enqueue(Assert.IsType<ProcessedImageReport>(p));order.Enqueue("processed");});
        try
        {
            Assert.IsType<ExposurePipeline>(vision.QualityProcessor.Pipeline);
            uint stamp=rig.Engine.Robot!.PoseHistory.NewestTimeStamp;
            vision.HandOverFrame(new CameraFrame{ImageId=1,Timestamp=stamp,Width=320,Height=240,Encoding=1,RawPayload=Enumerable.Repeat((byte)100,320*240).ToArray()});
            vision.WaitForProcessorIdle();Assert.Equal(1,vision.PendingResultCount);
            Assert.False(rig.Engine.VisionUpdateAllResultsFailed!());
            var report=Assert.Single(reports);Assert.Equal(new uint[]{7,8},report.Modes);Assert.Equal((byte)100,report.ImageMean);
            Assert.Equal(new[]{"camera","processed"},order);
            Assert.NotEqual(0u,vision.QualityStamp);Assert.NotEqual((byte)0,vision.LastObservedQuality);
        }
        finally{vision.Dispose();} // actual worker joins before rig/history/ROI input retirement.
    }

    [Fact]
    public void RealWorkerFailureIsStickyAcrossDrainAndGatesOnlyThatEnginePhase()
    {
        // Actual rendered marker worker→world handler→aggregate→Robot.Update gate.
        // World status7 and calibration-presence are explicit substituted inputs;
        // no detector/queue result or completed action is synthesized.
        using var rig=new M4ControlTests.Rig();rig.ToSynced();rig.Calibrate();
        var vision=rig.Robot.Vision;vision.Enabled=true;vision.Calibration=CameraCalibration.Nominal();vision.ModeEnableMask=1<<1;
        var reports=new ConcurrentQueue<ProcessedImageReport>();var frames=new ConcurrentQueue<uint>();
        var priorGate=rig.Engine.HasCameraCalibration;
        using var local=rig.Engine.AnimationMessages.SubscribeMessage(0x43,true,p=>reports.Enqueue(Assert.IsType<ProcessedImageReport>(p)));
        void Processed(VisionFrameResult frame){frames.Enqueue(frame.Timestamp);vision.World.LocalizeRobotResultForTest=()=>0;}
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
                vision.HandOverFrame(new CameraFrame{ImageId=index,Timestamp=stamp,Width=320,Height=240,Encoding=1,RawPayload=image.Pixels});
                vision.WaitForProcessorIdle();
            }
            Assert.Equal(2,vision.PendingResultCount);
            rig.Engine.HasCameraCalibration=()=>true;
            var mood=rig.Engine.Robot!.MoodOwner!;uint before=mood.HistoryCounts[0];
            rig.Tick(10);
            Assert.Equal(2,reports.Count);Assert.Equal(2,frames.Count);
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
