using Cozmo.Robot;
using Cozmo.Robot.Vision;
using Xunit;
namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class NativeQualityRectanglesTests
{
    [Fact]
    public void NativeRectangleConversionsAndBlackoutKeepOriginalValues()
    {
        var marker=new ObservedMarker((MarkerType)1,new[]{new Vec2(-1.9,2.9),new Vec2(7.9,2.1),new Vec2(-1.1,6.9),new Vec2(7.1,6.1)},1,0,0);
        var raw=NativeQualityRectangles.Marker(marker);Assert.Equal(new ImageQualityRectangle(-1,2,8,4),raw);
        Assert.Equal(new ImageQualityRectangle(2,-2,3,4),NativeQualityRectangles.Detection(new FaceRect(1.5,-1.5,2.5,3.5)));
        Assert.Equal(new ImageQualityRectangle(0,2,7,4),NativeQualityRectangles.Intersect(raw,8,6));
        Assert.Equal(default,NativeQualityRectangles.Intersect(new ImageQualityRectangle(8,1,1,1),8,6));
        var image=new GrayImage(8,6);image.Fill(100);var rectangles=new[]{raw};
        var black=NativeQualityRectangles.BlackOut(image,rectangles);
        Assert.Equal(raw,rectangles[0]);Assert.All(image.Pixels,x=>Assert.Equal(100,x));
        for(int y=0;y<6;y++)for(int x=0;x<8;x++)Assert.Equal(y>=2&&x<7?0:100,black[x,y]);
    }
    private sealed class Faces:IFaceDetector
    {
        public bool IsAvailable=>true;public string Description=>"supplied frame output";
        public IReadOnlyList<DetectedFace> Detect(GrayImage image,uint timestamp)
            =>new[]{new DetectedFace(1,new FaceRect(1.5,1.5,2.5,2.5))};
    }
    private sealed class Pets:IPetDetector
    {
        internal byte Inside,Outside;
        public bool IsAvailable=>true;
        public IReadOnlyList<DetectedPet> Detect(GrayImage image,uint timestamp)
        {Inside=image[2,2];Outside=image[1,1];return new[]{new DetectedPet(1,PetType.Cat,new FaceRect(5,0,2,2))};}
    }
    private sealed class Pipeline:IImageQualityPipeline
    {
        internal GrayImage? Mask;
        public uint ComputeExposureAdjustment(GrayImage image,GrayImage? mask,ref float factor){Mask=mask;return 0;}
        public uint ComputePercentile(float percentile)=>128;
    }
    [Fact]
    public void AttachedDetectionsFeedCurrentVectorAndSeparateBlackoutCopies()
    {
        using var robot=CozmoRobot.CreateOffline();var vision=robot.Vision;
        vision.Calibration=new CameraCalibration{Rows=6,Columns=8,FocalLengthX=20,FocalLengthY=20,CenterX=4,CenterY=3};
        vision.ModeEnableMask=(1<<2)|(1<<9)|(1<<7);vision.Schedules=AllVisionModesSchedule.InitDefaultSchedules();
        var pets=new Pets();vision.FaceDetector=new Faces();vision.PetDetector=pets;
        var pipeline=new Pipeline();vision.QualityProcessor.Settings=new ImageQualitySettings{MeterFromDetections=true};
        vision.QualityProcessor.Pipeline=pipeline;Assert.Null(vision.QualityRectangleInput);
        var image=new GrayImage(8,6);image.Fill(100);
        vision.ProcessImage(image,1,1,new VisionPoseData(1,Pose3d.Identity,0,0,false,false));
        Assert.Equal(0,pets.Inside);Assert.Equal(100,pets.Outside);Assert.All(image.Pixels,x=>Assert.Equal(100,x));
        Assert.NotNull(pipeline.Mask);Assert.NotEqual(pipeline.Mask![1,1],pipeline.Mask[2,2]);
    }
}
