using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class M11MapImageClockTests
{
    private static string Resources()
    {
        for(var d=new DirectoryInfo(AppContext.BaseDirectory);d is not null;d=d.Parent)
        {
            string path=Path.Combine(d.FullName,"re-analysis","obb","assets","cozmo_resources");
            if(Directory.Exists(path))return path;
        }
        throw new DirectoryNotFoundException("Shipped resources required");
    }
    private static void Enable(VisionSystem vision,int mask)
    {vision.Calibration=CameraCalibration.Nominal();vision.Enabled=true;vision.ModeEnableMask=mask;vision.Schedules=AllVisionModesSchedule.InitDefaultSchedules();} // supplied direct-capture schedule
    private static (GrayImage Image,uint Timestamp) Frame(M4ControlTests.Rig rig,bool cube=false)
    {
        rig.State(head:-0.15f);
        var vision=rig.Robot.Vision;uint stamp=rig.Engine.Robot!.PoseHistory.NewestTimeStamp;
        var image=new GrayImage(vision.Calibration.Columns,vision.Calibration.Rows);image.Fill(150);
        if(cube)
        {
            var pd=vision.History.At(stamp)!.Value;
            using var pose=new Pose3d(Mat3.Identity,new Vec3(150,0,22));
            using var cameraPose=pd.CameraPose;
            MarkerRenderer.DrawObject(image,MarkerLibrary.EmbeddedOrNull??throw new InvalidOperationException("Marker library required"),
                new CameraModel(vision.Calibration,cameraPose),ObjectType.Block_LIGHTCUBE1,pose);
        }
        return(image,stamp);
    }

    [Fact]
    public void ARealObservedObjectUsesThePreviousProcessedResultForItsMapStamp()
    {
        // reach: production (RobotState ingress, rendered capture API, real marker detection/confirmer/world/map).
        // Old: second-frame map data stamp166 (current raw frame), expected133 (first processed result).
        // Map getter calls0x0067EFCE/F0A6 ->0x00516EC0/0x006527A2; publication follows handlers0x006549AA.
        using var rig=new M4ControlTests.Rig();rig.ToSynced();var vision=rig.Robot.Vision;
        Enable(vision,1<<VisionSystem.DetectingMarkers);var map=vision.World.Maps.Current!;
        var writes=new List<(uint First,uint Last)>();uint expected=0;
        map.ObjectInsertForTest=(_,data)=>
        {
            if(data.Type!=MemoryMapContentType.ObstacleObservable)return;
            writes.Add((data.First,data.Last));
        };
        try
        {
            Assert.Equal(0u,vision.World.LastImageTimestamp!());Assert.Equal(0u,vision.World.Maps.ImageTimestamp!());
            var first=Frame(rig,true);Assert.NotNull(vision.ProcessCapture(first.Image,1,first.Timestamp));
            Assert.Empty(writes);Assert.Equal(first.Timestamp,rig.Robot.CameraSettings.LastProcessedImageTimestamp);
            expected=first.Timestamp;
            var second=Frame(rig,true);Assert.True(second.Timestamp>expected);
            var result=Assert.IsType<VisionFrameResult>(vision.ProcessCapture(second.Image,2,second.Timestamp));
            Assert.NotEmpty(result.Objects);Assert.NotEmpty(writes);
            Assert.All(writes,w=>Assert.Equal((expected,expected),w));
            Assert.Equal(second.Timestamp,rig.Robot.CameraSettings.LastProcessedImageTimestamp);
        }
        finally {map.ObjectInsertForTest=null;}
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConfirmedRenderedMarkerClearsTheMapAndVisitsTheOwnedWhiteboard(bool sharedRoot)
    {
        using var rig = new M4ControlTests.Rig();
        rig.ToSynced();
        var robot = rig.Engine.Robot!;
        var vision = rig.Robot.Vision;
        Enable(vision, 1 << VisionSystem.DetectingMarkers);
        var map = Assert.IsType<MemoryMap>(vision.World.Maps.Current);
        var whiteboard = robot.NativeBehaviorRuntime!.Manipulation!.Whiteboard;
        var origin = robot.GetNativeOrigin(robot.CurrentOriginId);
        Assert.Same(origin, whiteboard.ClearQuadWorldOrigin!());
        using var sharedOrigin = new Pose3d("shared-root whiteboard input", origin);
        var originInput = whiteboard.ClearQuadWorldOrigin;
        if (sharedRoot) whiteboard.ClearQuadWorldOrigin = () => sharedOrigin;

        var first = Frame(rig, true);
        Assert.Empty(Assert.IsType<VisionFrameResult>(vision.ProcessCapture(first.Image, 1, first.Timestamp)).Objects);
        var inside = new Vec2(75, 0);
        var outside = new Vec2(75, 60);
        void AddPossible(Vec2 point)
        {
            using var location = new Pose3d(Rotation3d.Identity, new Vec3(point.X, point.Y, 0), parent: origin);
            whiteboard.PossibleObjectStorage.Add(new(location, ObjectType.Block_LIGHTCUBE1));
            map.InsertObject(new[] { point+new Vec2(-8,-8), point+new Vec2(8,-8), point+new Vec2(8,8), point+new Vec2(-8,8) },
                new SourceMapData(MemoryMapContentType.ObstacleProx, first.Timestamp, first.Timestamp));
        }
        AddPossible(inside);
        AddPossible(outside);
        bool Has(Vec2 at, MemoryMapContentType type) => map.HasCollisionRayWithTypes(at, at, value => value == type);
        Assert.True(Has(inside, MemoryMapContentType.ObstacleProx));
        Assert.True(Has(outside, MemoryMapContentType.ObstacleProx));
        var markerInserts = new List<(Vec2[] Corners, uint First, uint Last)>();
        map.ObjectInsertForTest = (corners, data) =>
        {
            if (data.Type == MemoryMapContentType.ClearOfObstacle && corners.Any(c => c.X > 90))
                markerInserts.Add((corners, data.First, data.Last));
        };
        try
        {
            var second = Frame(rig, true);
            var result = Assert.IsType<VisionFrameResult>(vision.ProcessCapture(second.Image, 2, second.Timestamp));
            Assert.Single(result.Objects);
            var write = Assert.Single(markerInserts);
            Assert.Equal((first.Timestamp, first.Timestamp), (write.First, write.Last));
            // 0x0067F678..0x0067F6A4: robot's front pair, then the marker's lower pair on the cube's near face.
            Assert.Equal(2, write.Corners.Count(p => Math.Abs(p.X - 22.1) < 0.001 && Math.Abs(Math.Abs(p.Y) - 27.1) < 0.001));
            Assert.Equal(2, write.Corners.Count(p => Math.Abs(p.X - 128) < 3 && Math.Abs(Math.Abs(p.Y) - 12.5) < 3));
            Assert.True(Has(inside, MemoryMapContentType.ClearOfObstacle));
            Assert.False(Has(inside, MemoryMapContentType.ObstacleProx));
            Assert.True(Has(outside, MemoryMapContentType.ObstacleProx));
            // Native whiteboard copies a parentless origin into a different root. Supply a shared-root
            // origin for the deletion case while keeping the real marker, map and whiteboard producer.
            Assert.Equal(sharedRoot ? new[] { outside.Y } : new[] { inside.Y, outside.Y },
                whiteboard.PossibleObjects.Select(item => item.Pose.Translation.Y));
        }
        finally
        {
            whiteboard.ClearQuadWorldOrigin = originInput;
            map.ObjectInsertForTest = null;
        }
    }

    [Fact]
    public void MapClockExistsBeforeVisionSetup()
    {
        using var rig = new M4ControlTests.Rig(attachWorld: false);
        rig.ToSynced();
        var world = rig.Robot.LocalizationWorld;
        Assert.Equal(0u, world.LastImageTimestamp!());
        Assert.Equal(0u, world.Maps.ImageTimestamp!());
    }

    [Fact]
    public void AnObjectAddedDuringTheFirstResultHandlerGetsStampZero()
    {
        // reach: unit (substitutes object admission by AddLocatedObject inside the real result handler;
        // native detector/confirmer admission is not claimed here; actual getter/map/tree stay connected).
        // Old: first raw frame133; expected0 because no earlier processed result exists.0x0067EFCE/0x006549AA.
        using var rig=new M4ControlTests.Rig();rig.ToSynced();var vision=rig.Robot.Vision;Enable(vision,0);
        var world=vision.World;var map=world.Maps.Current!;var writes=new List<(uint First,uint Last)>();
        var obj=new ObservableObject(ObjectType.Block_LIGHTCUBE1,CubeGeometry.MarkersFor(ObjectType.Block_LIGHTCUBE1))
            {OriginId=world.CurrentOriginId};
        using var pose=new Pose3d(Mat3.Identity,new Vec3(150,0,22));
        pose.SetParent(rig.Engine.Robot!.GetNativeOrigin(world.CurrentOriginId));obj.InitPose(pose,PoseState.Known);obj.SetID();
        map.ObjectInsertForTest=(_,data)=>
        {
            if(data.Type!=MemoryMapContentType.ObstacleObservable)return;
            writes.Add((data.First,data.Last));
        };
        void Add(VisionFrameResult _)
        {
            Assert.Equal(0u,rig.Robot.CameraSettings.LastProcessedImageTimestamp);
            world.AddLocatedObject(obj);
        }
        vision.FrameProcessed+=Add;
        try
        {
            var frame=Frame(rig);Assert.NotEqual(0u,frame.Timestamp);
            Assert.NotNull(vision.ProcessCapture(frame.Image,1,frame.Timestamp));Assert.NotEmpty(writes);
            Assert.All(writes,w=>Assert.Equal((0u,0u),w));
            Assert.Equal(frame.Timestamp,rig.Robot.CameraSettings.LastProcessedImageTimestamp);
        }
        finally
        {
            vision.FrameProcessed-=Add;map.ObjectInsertForTest=null;
            try {rig.Dispose();} finally {obj.NativePose.Dispose();}
        }
    }

    [Fact]
    public void TheNativeObjectPositionStrategyReadsTheSamePreTailClock()
    {
        // reach: unit (observes actual native strategy's getter during capture handlers; reaction selection not exercised).
        // Old: the raw-frame diagnostic remains null for these equal-timestamp decoded captures; getter0
        // after first processed133, expected133. Handler expectations are0 then the preceding result133.
        // Checked M10-derived4e/4h:0x00612584/0x00612A9E call Robot::GetLastImageTimeStamp0x00516EC0.
        using var rig=new M4ControlTests.Rig(Resources());rig.ToSynced();var vision=rig.Robot.Vision;Enable(vision,0);
        var strategy=Assert.Single(rig.Robot.BehaviorManager!.Reactions.Select(r=>r.Strategy).OfType<ObjectPositionUpdatedStrategy>());
        uint expected=0;var expectedClocks=new List<uint>();var clocks=new List<uint>();
        void Observe(VisionFrameResult _)=>clocks.Add(strategy.LastImageTimestamp!());
        vision.FrameProcessed+=Observe;
        try
        {
            for(uint frame=1;frame<=2;frame++)
            {
                expectedClocks.Add(expected);
                var input=Frame(rig);Assert.NotNull(vision.ProcessCapture(input.Image,frame,input.Timestamp));
                expected=input.Timestamp;Assert.Equal(expected,strategy.LastImageTimestamp!());
            }
            Assert.Equal(expectedClocks,clocks);
        }
        finally {vision.FrameProcessed-=Observe;}
    }
}
