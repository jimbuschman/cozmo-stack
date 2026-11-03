using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;
namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class M11ProcessedImageTimestampTests
{
    private sealed class Fixture : IDisposable
    {
        internal readonly M4ControlTests.Rig Rig=new();
        internal VisionSystem Vision=>Rig.Robot.Vision;
        internal readonly ManipulationSystem Manipulation;
        internal readonly FreeplayStack Stack;
        internal MemoryMap Map=>Vision.World.Maps.Current!;
        internal readonly GrayImage Image;
        internal Fixture()
        {
            try
            {
                Rig.ToSynced();Rig.State(RobotStatusFlag.IsBodyAccMode|RobotStatusFlag.LiftInPos,head:(float)HeadGeometry.MinHeadAngleRad,liftAngle:(float)Math.Asin((LiftPresets.CarryMm-45.0)/LiftGeometry.ArmLengthMm)); // lift at carry height: with the camera at z -8 (M11-054) a lower arm crosses the ROI at this head and the lift gate abandons the frame
                Vision.Calibration=CameraCalibration.Nominal();Vision.Enabled=true;
                Vision.SetNativeOuterCalibration(Vision.Calibration); // M11-017 row 6: the ground-plane LUT the frame's carrier H comes from (production: the NV calibration read)
                // M11-017 row 5 (0x006AC090..0x006AC09C): Detect runs only with (status & 0x102) == 0x100, so every state here reports LIFT_IN_POS
                Vision.ModeEnableMask=1<<(int)VisionMode.DetectingOverheadEdges;
                Vision.Schedules=AllVisionModesSchedule.InitDefaultSchedules(); // supplied direct-capture schedule
                Image=new(Vision.Calibration.Columns,Vision.Calibration.Rows);
                // A camera can supply these horizontal light/dark bands. The actual detector supplies all edges.
                for(int y=0;y<Image.Height;y++)for(int x=0;x<Image.Width;x++)Image.Pixels[y*Image.Width+x]=(byte)((y/16)%2==0?0:255);
                Manipulation=new(Rig.Robot,Vision);
                try
                {
                    var directory=new DirectoryInfo(AppContext.BaseDirectory);
                    while(directory is not null && !Directory.Exists(Path.Combine(directory.FullName,"re-analysis","obb","assets")))directory=directory.Parent;
                    Assert.NotNull(directory);
                    Stack=FreeplayStack.Create(Path.Combine(directory!.FullName,"re-analysis","obb"),Rig.Robot,
                        new BehaviorContext{Robot=Rig.Robot,Triggers=new AnimationTriggerMap()},()=>Rig.Engine.Timer.Seconds,Vision,Manipulation,withReactions:false);
                }
                catch{Manipulation.Dispose();throw;}
            }
            catch{Rig.Dispose();throw;}
        }
        internal uint Timestamp=>Rig.Engine.Robot!.PoseHistory.NewestTimeStamp;
        internal VisionFrameResult Capture(uint id)=>Assert.IsType<VisionFrameResult>(Vision.ProcessCapture(Image,id,Timestamp));
        public void Dispose(){try{Stack.Dispose();}finally{try{Manipulation.Dispose();}finally{Rig.Dispose();}}}
    }

    [Fact]
    public void BorderReadsThePreviousResultAndTheTailPublishesTheCurrentOne()
    {
        // reach: production (public decoded-capture API, received native state/history, real detector/Freeplay/map).
        // Old: border callbacks=0, handler clock=current timestamp. Native 0x00680396 precedes 0x006549AA.
        using var f=new Fixture();var border=new List<uint>();var handler=new List<uint>();
        f.Map.ObjectBorderForTest=(_,_,data)=>border.Add(data.Last); // observation only; real FillBorder continues
        void Observe(VisionFrameResult _)=>handler.Add(f.Rig.Robot.CameraSettings.LastProcessedImageTimestamp);
        f.Vision.FrameProcessed+=Observe;
        try
        {
            uint first=f.Timestamp;Assert.NotEqual(0u,first);f.Capture(1);
            Assert.Equal(new[]{0u},border);Assert.Equal(new[]{0u},handler);
            Assert.Equal(first,f.Rig.Robot.CameraSettings.LastProcessedImageTimestamp);
            // moved past the first chain: the first capture's own edge cells block a ray to the same ground (OverheadEdgeBlocksObstacles), and a blocked chain writes no segment
            f.Rig.State(RobotStatusFlag.IsBodyAccMode|RobotStatusFlag.LiftInPos,x:60,head:(float)HeadGeometry.MinHeadAngleRad,liftAngle:(float)Math.Asin((LiftPresets.CarryMm-45.0)/LiftGeometry.ArmLengthMm));uint second=f.Timestamp;Assert.True(second>first);
            f.Capture(2);
            Assert.Equal(new[]{0u,first},border);Assert.Equal(new[]{0u,first},handler);
            Assert.Equal(second,f.Rig.Robot.CameraSettings.LastProcessedImageTimestamp);
        }
        finally{f.Vision.FrameProcessed-=Observe;f.Map.ObjectBorderForTest=null;}
    }

    [Fact]
    public void ARealDetectedBorderWritesTypeTenCellsOnTheFirstCapture()
    {
        // reach: production (same capture/detector/history/map path, no substituted polygons or clock).
        // Old: type10 count=0 because the Freeplay getter returned null. Checked first border stamp is0.
        using var f=new Fixture();
        // The border mask requires a neighboring obstacle. Supply it through real contact-charger admission,
        // then a received robot pose looking toward its back edge; no native tree/cell is supplied by the test.
        // x -115: with the camera at z -8 the nearest image band meets the floor about 45 mm ahead, beside the charger's back cells.
        f.Rig.State(RobotStatusFlag.IsBodyAccMode|RobotStatusFlag.IsOnCharger|RobotStatusFlag.LiftInPos,head:(float)HeadGeometry.MinHeadAngleRad,liftAngle:(float)Math.Asin((LiftPresets.CarryMm-45.0)/LiftGeometry.ArmLengthMm));
        f.Rig.State(RobotStatusFlag.IsBodyAccMode|RobotStatusFlag.LiftInPos,x:-115,head:(float)HeadGeometry.MinHeadAngleRad,liftAngle:(float)Math.Asin((LiftPresets.CarryMm-45.0)/LiftGeometry.ArmLengthMm));
        var result=f.Capture(1);
        Assert.Contains(result.OverheadEdges!.Chains,c=>c.IsBorder&&c.Points.Count>1);
        var leaves=new List<SourceQuadNode>();
        void Collect(SourceQuadNode node)
        {if(node.Children.Count==0){leaves.Add(node);return;}foreach(var c in node.Children)Collect(c!);}
        Collect(f.Map.Tree.Root);
        Assert.True(f.Map.NativeContentCounts.GetValueOrDefault(MemoryMapContentType.NotInterestingEdge)>0,
            string.Join(";",leaves.Where(n=>n.Content.Data.Type is MemoryMapContentType.ObstacleCharger or MemoryMapContentType.InterestingEdge).Select(n=>$"{n.Content.Data.Type}({n.X},{n.Y})/{n.Side}")));
        var border=leaves.Where(n=>n.Content.Data.Type==MemoryMapContentType.NotInterestingEdge).ToArray();
        Assert.NotEmpty(border);Assert.All(border,n=>Assert.Equal((0u,0u),(n.Content.Data.First,n.Content.Data.Last)));
        Assert.Equal(result.Timestamp,f.Rig.Robot.CameraSettings.LastProcessedImageTimestamp);
    }

    [Fact]
    public void AThrowingHandlerStillCommitsTheConsumedResultAfterObservingTheOldClock()
    {
        // reach: unit (injects a throwing host subscriber through the real capture entry;
        // native result-mailbox consumer join remains MISSING at 0x00654486, not replaced by this fixture).
        // Old: subscriber sees current timestamp; new sees0 and finally commits current timestamp despite throw.
        using var f=new Fixture();uint seen=uint.MaxValue;
        void Fail(VisionFrameResult _){seen=f.Rig.Robot.CameraSettings.LastProcessedImageTimestamp;throw new InvalidOperationException("handler failed");}
        f.Vision.FrameProcessed+=Fail;
        try
        {
            Assert.Equal("handler failed",Assert.Throws<InvalidOperationException>(()=>f.Capture(1)).Message);
            Assert.Equal(0u,seen);Assert.Equal(f.Timestamp,f.Rig.Robot.CameraSettings.LastProcessedImageTimestamp);
        }
        finally{f.Vision.FrameProcessed-=Fail;}
    }
}
