using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M14: the face pipeline around the OKAO boundary. The stock detector reports itself unavailable; a fake
/// detector places faces in the world so the stack's own geometry (62 mm inter-pupil distance), the face world's
/// rules, the turn / track actions and the face behaviours can be exercised offline.
/// </summary>
public class FaceTests
{
    private static BehaviorContext Ctx(Rig rig) => new() { Robot = rig.Robot, Triggers = new AnimationTriggerMap() };

    /// <summary>
    /// The memory map, and the question the face behaviour asks it.
    /// <c>BehaviorInteractWithFaces::CanDriveIdealDistanceForward</c> 0x005C2420 takes the point 40 mm
    /// ahead and asks <c>HasCollisionRayWithTypes</c> with the eleven-entry mask at 0x00C67962;
    /// <c>TransitionToDrivingForward</c> drives 40 mm when the answer is clear and -15 mm when it is not
    /// (0x005C2566 and the conditional at 0x005C2572). The content types and the family mapping are
    /// <c>ObjectFamilyToMemoryMapContentType</c> 0x0067F4C0, and a markerless object is refused there, so
    /// the collision obstacle an unexpected movement leaves in the world never reaches the map.
    /// </summary>
    [Fact]
    public void TheMemoryMapAnswersWhetherTheRobotCanDriveInToAFace()
    {
        Assert.Equal(MemoryMapContentType.ObstacleObservable, MemoryMapTypes.ContentTypeForFamily(ObjectFamily.LightCube, adding: true));
        Assert.Equal(MemoryMapContentType.ClearOfObstacle, MemoryMapTypes.ContentTypeForFamily(ObjectFamily.LightCube, adding: false));
        Assert.Equal(MemoryMapContentType.ObstacleCharger, MemoryMapTypes.ContentTypeForFamily(ObjectFamily.Charger, adding: true));
        Assert.Equal(MemoryMapContentType.ObstacleChargerRemoved, MemoryMapTypes.ContentTypeForFamily(ObjectFamily.Charger, adding: false));
        Assert.Equal(MemoryMapContentType.Unknown, MemoryMapTypes.ContentTypeForFamily(ObjectFamily.MarkerlessObject, adding: true));
        foreach (var t in new[] { MemoryMapContentType.Unknown, MemoryMapContentType.ClearOfObstacle, MemoryMapContentType.ClearOfCliff, MemoryMapContentType.ObstacleChargerRemoved })
            Assert.False(MemoryMapTypes.BlocksTheRobot(t));
        foreach (var t in new[] { MemoryMapContentType.ObstacleObservable, MemoryMapContentType.ObstacleCharger, MemoryMapContentType.ObstacleProx,
                                  MemoryMapContentType.ObstacleUnrecognized, MemoryMapContentType.Cliff, MemoryMapContentType.InterestingEdge,
                                  MemoryMapContentType.NotInterestingEdge })
            Assert.True(MemoryMapTypes.BlocksTheRobot(t));

        // the robot at the origin facing +x, the 40 mm probe
        var from = new Vec2(0, 0);
        var to = new Vec2(InteractWithFacesBehavior.DriveForwardMm, 0);
        var map = new MemoryMap();
        var near = MemoryMap.Rectangle(new Pose3d(Mat3.Identity, new Vec3(60, 0, 0)), 44, 44);   // spans x 38..82
        map.Insert(near, MemoryMapContentType.ObstacleObservable, objectId: 1);
        Assert.True(map.HasCollisionRayWithTypes(from, to));
        map.Clear();
        map.Insert(MemoryMap.Rectangle(new Pose3d(Mat3.Identity, new Vec3(90, 0, 0)), 44, 44), MemoryMapContentType.ObstacleObservable, objectId: 1);
        Assert.False(map.HasCollisionRayWithTypes(from, to));       // 68 mm away: the probe stops short
        // a cleared region is not in the way
        map.Clear();
        map.Insert(near, MemoryMapContentType.ClearOfObstacle, objectId: 1);
        Assert.False(map.HasCollisionRayWithTypes(from, to));

        // a collision obstacle is a markerless object: in the world, never in the map
        var world = new BlockWorld(Array.Empty<(uint, ObjectType)>);
        world.AddCollisionObstacle(new Pose3d(Mat3.Identity, new Vec3(30, 0, 0)));
        Assert.Single(world.LocatedObjects);
        map.Clear();
        map.SyncFromWorld(world);
        Assert.Empty(map.Regions);
        Assert.False(map.HasCollisionRayWithTypes(from, to));

        Assert.Equal(-15.0, InteractWithFacesBehavior.DriveBackwardMm);
        Assert.Equal(40f, InteractWithFacesBehavior.DriveSpeedMmps);
    }

    /// <summary>
    /// <c>MapComponent::UpdateRobotPose</c> 0x0067E224: the robot lays its own footprint into the map as
    /// ClearOfCliff - the ProxObstacle size halved, so a 10 by 10 square - but only once it has moved 8 mm
    /// or turned 20 degrees from where it last did (the <c>IsSameAs</c> at 0x0067E27C), and as Cliff
    /// instead when something is reporting one (the type byte 2 against the cliff data at 0x0067E3C2).
    /// </summary>
    [Fact]
    public void TheRobotWritesTheGroundItHasBeenOverIntoTheMemoryMap()
    {
        var map = new MemoryMap();
        Assert.NotNull(map.UpdateRobotPose(new Pose3d(Mat3.Identity, new Vec3(0, 0, 0))));
        var first = map.Regions[^1];
        Assert.Equal(MemoryMapContentType.ClearOfCliff, first.Type);
        Assert.Equal(4, first.Polygon.Length);
        Assert.Equal(10, first.Polygon.Max(p => p.X) - first.Polygon.Min(p => p.X), 3);
        Assert.Equal(10, first.Polygon.Max(p => p.Y) - first.Polygon.Min(p => p.Y), 3);

        Assert.Null(map.UpdateRobotPose(new Pose3d(Mat3.Identity, new Vec3(5, 0, 0))));     // not far enough
        Assert.Single(map.Regions);
        Assert.NotNull(map.UpdateRobotPose(new Pose3d(Mat3.Identity, new Vec3(9, 0, 0))));  // 9 mm is
        Assert.Null(map.UpdateRobotPose(new Pose3d(Mat3.AboutZ(0.3), new Vec3(9, 0, 0))));  // 17 degrees is not
        // turning far enough on the spot writes nothing either: that ground is already recorded, which is
        // what the engine's quad tree does with a repeat
        Assert.Null(map.UpdateRobotPose(new Pose3d(Mat3.AboutZ(0.4), new Vec3(9, 0, 0))));
        Assert.Equal(2, map.Regions.Count);

        var cliff = map.UpdateRobotPose(new Pose3d(Mat3.Identity, new Vec3(60, 0, 0)), cliffDetected: true);
        Assert.Equal(MemoryMapContentType.Cliff, cliff!.Type);
        Assert.True(map.HasCollisionRayWithTypes(new Vec2(50, 0), new Vec2(70, 0)));         // and it blocks
    }

    private static bool Runnable(SteppedBehavior b, BehaviorContext ctx) =>
        (bool)typeof(SteppedBehavior).GetMethod("IsRunnableInternal", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(b, new object[] { ctx })!;

    private static void RunToEnd(Rig rig, SteppedBehavior b, BehaviorContext ctx, bool frames = true, int ms = 10000, double stepMs = 33)
    {
        double t = 0;
        b.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (b.Update(ctx, t))
        {
            rig.Pump();
            if (frames) rig.Frame();
            t += stepMs;
            if (sw.ElapsedMilliseconds > ms) throw new TimeoutException("behaviour did not finish: " + string.Join(" | ", b.Trace));
            Thread.Sleep(2);
        }
        b.Stop(BehaviorStopReason.Completed);
    }

    private static Rig FaceRig(params (int Id, Vec3 Head, string? Name)[] faces)
    {
        var rig = new Rig();
        rig.Vision.FaceDetector = rig.FaceDetector;
        rig.FaceDetector.Faces.AddRange(faces);
        rig.Head = 0.3f;
        return rig;
    }

    // ------------------------------------------------------------------ the boundary

    [Fact]
    public void TheStockFaceDetectorIsAnExplicitUnavailableBoundary()
    {
        using var rig = new Rig();
        Assert.IsType<OkaoFaceDetector>(rig.Vision.FaceDetector);
        Assert.False(rig.Vision.FaceDetector.IsAvailable);
        Assert.Contains("OKAO", rig.Vision.FaceDetector.Description);
        Assert.Empty(rig.Vision.FaceDetector.Detect(new GrayImage(320, 240), 1));
        rig.Frame();
        Assert.Empty(rig.Vision.Faces.Faces);                                        // nothing runs without a detector
        Assert.False(rig.Vision.PetDetector.IsAvailable);
        var ctx = Ctx(rig);
        Assert.False(Runnable(new AcknowledgeFaceBehavior(rig.Vision), ctx));
        Assert.False(Runnable(new PlayAnimWithFaceBehavior(rig.Vision, "VC_AlrightyResponse", new[] { AnimationTrigger.VC_Alrighty }), ctx));
    }

    // ------------------------------------------------------------------ tracked faces and the face world

    [Fact]
    public void AFaceRectangleBecomesAHeadPoseAtTheInterPupilDistance()
    {
        var cal = CameraCalibration.Nominal();
        var cam = new CameraModel(cal, new Pose3d(Mat3.Identity, new Vec3(0, 0, 0)).Compose(HeadGeometry.CameraPoseInWorld(Pose3d.Identity, 0)));
        // a 62 px intra-eye distance at f = 290 puts the head 290 mm from the camera
        double f = cal.FocalLengthX;
        var d = new DetectedFace(3, new FaceRect(cal.CenterX - 62, cal.CenterY - 62 + 0.125 * 124, 124, 124));
        var tf = new TrackedFace(d, 1000);
        var (mid, eyePx) = tf.EyeGeometry();
        Assert.Equal(62.0, eyePx, 6);
        Assert.Equal(cal.CenterX, mid.X, 6); Assert.Equal(cal.CenterY, mid.Y, 6);
        tf.UpdateTranslation(cam);
        Assert.Equal(TrackedFace.InterPupilDistanceMm * f / 62.0, tf.DistanceMm, 6);
        Assert.Equal(290.0, (tf.HeadPose.Translation - cam.Pose.Translation).Length, 0.5);
        // detected eyes take precedence, and the distance is floored at 6 px
        var eyes = new TrackedFace(new DetectedFace(4, new FaceRect(0, 0, 10, 10), new Vec2(100, 100), new Vec2(102, 100)), 1000);
        Assert.Equal(TrackedFace.MinIntraEyeDistancePx, eyes.EyeGeometry().IntraEyeDistancePx);
        Assert.Equal(FacialExpression.Unknown, tf.MaxExpression());
        Assert.Equal(FacialExpression.Happiness, new TrackedFace(new DetectedFace(5, new FaceRect(0, 0, 1, 1), ExpressionValues: new byte[] { 10, 80, 5, 0, 0 }), 1).MaxExpression());
    }

    [Fact]
    public void TheFaceWorldMatchesByPoseForgetsUnnamedFacesAndKeepsNamedOnes()
    {
        var world = new FaceWorld();
        var robot = Pose3d.Identity;
        var cam = new CameraModel(CameraCalibration.Nominal(), HeadGeometry.CameraPoseInWorld(robot, 0.2));
        TrackedFace Face(int id, double x, double y, double z, uint ts, string? name = null)
        {
            var px = cam.Project(new Vec3(x, y, z))!.Value; double dist = (new Vec3(x, y, z) - cam.Pose.Translation).Length;
            double eye = 62 * cam.Calibration.FocalLengthX / dist, w = 2 * eye;
            var tf = new TrackedFace(new DetectedFace(id, new FaceRect(px.X - w / 2, px.Y + 0.125 * w - w / 2, w, w), Name: name), ts);
            tf.UpdateTranslation(cam); return tf;
        }
        var o1 = world.AddOrUpdateFace(Face(0, 400, 0, 300, 1000), robot, false);
        Assert.NotNull(o1); Assert.True(o1!.IsNew); Assert.Equal(1, o1.Face.Id);       // no tracker id: a session id
        Assert.InRange((o1.Face.HeadPose.Translation - new Vec3(400, 0, 300)).Length, 0, 5);
        // the same person a little later, 100 mm away: matched by pose, not new
        var o2 = world.AddOrUpdateFace(Face(0, 420, 90, 300, 1100), robot, false);
        Assert.False(o2!.IsNew); Assert.Equal(1, o2.Face.Id); Assert.Equal(2, o2.Face.TimesObserved);
        // a face 500 mm away is another person
        var o3 = world.AddOrUpdateFace(Face(0, 400, -500, 300, 1200, "Jim"), robot, false);
        Assert.True(o3!.IsNew); Assert.Equal(2, o3.Face.Id); Assert.True(o3.Face.HasName);
        // rotating too fast and a face below the robot are ignored; an older observation is rejected
        Assert.Null(world.AddOrUpdateFace(Face(0, 400, 0, 300, 1300), robot, rotatingTooFast: true));
        Assert.Null(world.AddOrUpdateFace(Face(0, 400, 0, -50, 1300), robot, false));
        Assert.Null(world.AddOrUpdateFace(Face(0, 420, 90, 300, 900), robot, false));
        Assert.Equal(new[] { 1, 2 }, world.GetFaceIDs().OrderBy(i => i));
        Assert.Equal(new[] { 2 }, world.GetFaceIDs(namedOnly: true));
        Assert.Equal(2, world.GetLastObservedFace()!.Id);
        Assert.True(world.HasAnyFaces(1150)); Assert.False(world.HasAnyFaces(1300));
        // 15 s later the unnamed face is forgotten, the named one stays
        var removed = world.Update(1100 + FaceWorld.UnnamedFaceLifetimeMs + 1);
        Assert.Equal(new[] { 1 }, removed);
        Assert.Equal(new[] { 2 }, world.GetFaceIDs());
        // a recognised id replaces a tracking id and smart ids follow
        var smart = world.GetSmartFaceID(2);
        Assert.True(world.ChangeFaceID(2, 42, "Jim"));
        Assert.Equal(42, smart.Id); Assert.NotNull(world.GetFace(42)); Assert.Null(world.GetFace(2));
        world.OnRobotDelocalized();
        Assert.Empty(world.Faces);
    }

    [Fact]
    public void FramesWithAFakeDetectorPopulateTheFaceWorldWhereTheFaceIs()
    {
        using var rig = FaceRig((7, new Vec3(500, 100, 250), null));
        rig.Frame(); rig.Frame();
        var face = Assert.Single(rig.Vision.Faces.Faces);
        Assert.Equal(7, face.Id);
        // the engine places the head at 62 f / eye-px along the ray, which is the depth, not the range: a few mm off-axis
        Assert.InRange((face.HeadPose.Translation - new Vec3(500, 100, 250)).Length, 0, 25);
        Assert.Equal(2, face.TimesObserved);
        Assert.Single(rig.Vision.LastFaces);
        // turn the robot away: the face leaves the image and is not reported; it stays known until 15 s pass
        rig.Angle = (float)Math.PI; rig.State(); rig.Frame();
        Assert.Empty(rig.Vision.LastFaces);
        Assert.Single(rig.Vision.Faces.Faces);
    }

    // ------------------------------------------------------------------ the actions

    [Fact]
    public void TurnTowardsFaceTurnsThenFineTunesOnAFreshObservation()
    {
        using var rig = FaceRig((7, new Vec3(400, 150, 250), "Jim"));
        rig.Frame();
        Assert.Single(rig.Vision.Faces.Faces);
        var turn = new TurnTowardsFaceAction(rig.Vision, 7, Math.PI, sayName: true) { SayNameTrigger = AnimationTrigger.AcknowledgeFaceNamed, NoNameTrigger = AnimationTrigger.AcknowledgeFaceUnnamed };
        var events = new List<string>(); turn.EmotionEvent += events.Add;
        var task = turn.RunAsync(default);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!task.IsCompleted && sw.ElapsedMilliseconds < 5000) { rig.Pump(); rig.Frame(); Thread.Sleep(5); }
        Assert.Equal(FaceActionResult.Success, task.Result);
        Assert.InRange(Math.Abs(rig.Angle - Math.Atan2(150, 400)), 0, 0.05);       // facing (400, 150)
        Assert.True(turn.ObservedFace); Assert.True(turn.FineTuned);
        Assert.Equal(new[] { "LookAtFaceVerified" }, events);
        Assert.Equal((AnimationTrigger.AcknowledgeFaceNamed, "Jim"), turn.Reaction!.Value);
        Assert.True(rig.Vision.Faces.HasTurnedTowardsFace(7));
        Assert.Contains(turn.Trace, l => l.Contains("Will fine tune"));
        // no face at all: the action fails as the engine's Init does
        using var empty = FaceRig();
        Assert.Equal(FaceActionResult.NoFace, new TurnTowardsFaceAction(empty.Vision, SmartFaceID.Invalid).RunAsync(default).GetAwaiter().GetResult());
    }

    /// <summary>
    /// The numbers the face actions wait and turn by, as the engine's constructors set them.
    /// TurnTowardsFaceAction puts 10 at +0x188 (0x0054B798) and IVisuallyVerifyAction the same 10 at +0x8C
    /// (0x0056873E); ITrackAction's constructor sets the tolerances to 2 degrees, the pan duration to 0.4 s
    /// and the tilt duration to 0.15 s (0x00564758), the maximum head angle to 0.776672 rad, the sound
    /// thresholds to 10 degrees, and leaves the eye movement and the driving animation off. This stack had
    /// five frames and a hundred-millisecond poll, both invented.
    /// </summary>
    [Fact]
    public void TheFaceActionConstantsAreTheEnginesOwn()
    {
        Assert.Equal(10, TurnTowardsFaceAction.FramesToWaitForFace);
        Assert.Equal(0.4, TrackFaceAction.PanDurationSec, 6);
        Assert.Equal(0.15, TrackFaceAction.TiltDurationSec, 6);
        Assert.Equal(0.5, TrackFaceAction.DesiredTimeToReachTargetSec, 6);
        Assert.Equal(0.0349066, TrackFaceAction.MinToleranceRad, 6);
        Assert.Equal(0.776672, TrackFaceAction.MaxHeadAngleRad, 6);
        Assert.Equal(0.174533, TrackFaceAction.MinAngleForSoundRad, 6);
        Assert.Equal(10000, TrackFaceAction.TrackAccelRadPerSec2);
        Assert.Equal(33, TrackFaceAction.UpdateIntervalMs);

        using var rig = FaceRig((7, new Vec3(400, 0, 250), null));
        var track = new TrackFaceAction(rig.Vision, 7);
        Assert.False(track.MoveEyes);
        Assert.False(track.DrivingAnimation);
        track.Dispose();
    }

    /// <summary>
    /// TurnTowardsImagePointAction turns by the angle the pixel subtends and nothing else:
    /// Robot::ComputeTurnTowardsImagePointAngles 0x0051879C is atan2(-(u - cx), fx) added to the heading
    /// and atan2(-(v - cy), fy) added to the head angle. A point at the image centre asks for no turn at
    /// all, whatever the distance to whatever is there - which is what the 200 mm ray this stack used to
    /// build could not say.
    /// </summary>
    [Fact]
    public void TurnTowardsImagePointIsTheAngleThePixelSubtends()
    {
        var cal = CameraCalibration.Nominal();
        var (body, head) = TurnTowardsImagePoint.Angles(cal, cal.CenterX, cal.CenterY, 0.5, 0.2);
        Assert.Equal(0.5, body, 9);
        Assert.Equal(0.2, head, 9);

        // a point to the right of centre turns the body right (negative), one above centre lifts the head
        var (right, _) = TurnTowardsImagePoint.Angles(cal, cal.CenterX + cal.FocalLengthX, cal.CenterY, 0, 0);
        Assert.Equal(-Math.PI / 4, right, 6);
        var (_, up) = TurnTowardsImagePoint.Angles(cal, cal.CenterX, cal.CenterY - cal.FocalLengthY, 0, 0);
        Assert.Equal(Math.PI / 4, up, 6);
    }

    /// <summary>
    /// The pet strategy waits a minute between reactions: ReactionTriggerStrategyPetInitialDetection's
    /// RecentlyReacted 0x00611DD0 is true while the last reaction plus 60 (0x00611DE8) is ahead of now,
    /// and UpdateReactedTo 0x00611E1C records the pet ids it has already reacted to.
    /// </summary>
    [Fact]
    public void ThePetStrategyRemembersWhatItReactedToAndWaitsAMinute()
    {
        using var rig = FaceRig();
        var world = rig.Vision.Pets;
        using var strategy = new PetInitialDetectionStrategy(world);
        var ctx = Ctx(rig);
        var runnable = M10Support.RunnableBehaviour();
        Assert.Equal(60.0, PetInitialDetectionStrategy.RecentlyReactedSec, 6);
        var cat = new DetectedPet(3, PetType.Cat, new FaceRect(10, 10, 20, 20));
        var dog = new DetectedPet(4, PetType.Dog, new FaceRect(50, 10, 20, 20));

        // gap1 8: a pet must have been observed more than twice (threshold 3) before it is a target.
        world.Update(new[] { cat }, 100, false);
        Assert.False(strategy.ShouldTrigger(ctx, null, 0, runnable));
        world.Update(new[] { cat }, 200, false);
        Assert.False(strategy.ShouldTrigger(ctx, null, 0, runnable));
        world.Update(new[] { cat }, 300, false);
        Assert.True(strategy.ShouldTrigger(ctx, null, 0, runnable));
        Assert.Equal(new[] { 3 }, strategy.Targets);

        // reacting to it records the id and starts the minute
        strategy.BehaviorDidReact(new[] { 3 }, 10);
        Assert.True(strategy.RecentlyReacted(11));
        world.Update(new[] { cat }, 400, false);
        Assert.False(strategy.ShouldTrigger(ctx, null, 11, runnable));   // inside the minute
        Assert.False(strategy.RecentlyReacted(71));

        // the minute is up, but the only pet has already reacted: nothing to do
        Assert.False(strategy.ShouldTrigger(ctx, null, 71, runnable));

        // a new pet observed three times after the minute is a target
        world.Update(new[] { cat, dog }, 800, false);
        world.Update(new[] { cat, dog }, 900, false);
        world.Update(new[] { cat, dog }, 1000, false);
        Assert.True(strategy.ShouldTrigger(ctx, null, 71, runnable));
        Assert.Equal(new[] { 4 }, strategy.Targets);
        strategy.BehaviorDidReact(new[] { 4 }, 71);
        world.Update(new[] { cat, dog }, 1100, false);
        Assert.False(strategy.ShouldTrigger(ctx, null, 200, runnable));  // both have reacted now
    }

    [Fact]
    public void TrackFaceFollowsTheFaceWithinItsTolerances()
    {
        using var rig = FaceRig((7, new Vec3(400, 0, 250), null));
        rig.Frame();
        var track = new TrackFaceAction(rig.Vision, 7) { PanToleranceRad = 0.0698132, TiltToleranceRad = 0.0698132 };
        var task = track.RunAsync(TimeSpan.FromMilliseconds(600), default);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!task.IsCompleted && sw.ElapsedMilliseconds < 3000)
        {
            // the person walks sideways; the fake robot follows on each command
            rig.FaceDetector.Faces[0] = (7, new Vec3(400, Math.Min(200, sw.ElapsedMilliseconds * 0.5), 250), null);
            rig.Pump(); rig.Frame(); Thread.Sleep(5);
        }
        Assert.True(task.Result);
        Assert.True(track.Updates >= 3);
        Assert.True(track.Turns >= 1, $"turns {track.Turns}");
        Assert.InRange(rig.Angle, 0.3, 0.6);                                          // ended facing about atan(200/400)
        // the command that went out is the recovered tracking pan and tilt, not a look-at solve done again:
        // atan((z - 49) / planar distance) for the head, the body's heading plus the pan for the body
        Assert.NotEmpty(rig.PanTilts);
        var (pan, tilt) = rig.PanTilts[^1];
        Assert.Equal(track.LastCommand!.Value.Pan, pan, 6);
        Assert.Equal(track.LastCommand!.Value.Tilt, tilt, 6);
        Assert.InRange(tilt, 0.3, 0.6);                                               // atan(201/~447)
        track.Dispose();
    }

    // ------------------------------------------------------------------ the behaviours

    [Fact]
    public void PlayAnimWithFaceTurnsToTheLastFaceThenPlays()
    {
        using var rig = FaceRig((7, new Vec3(400, -150, 250), null));
        rig.Frame();
        var ctx = Ctx(rig);
        var b = new PlayAnimWithFaceBehavior(rig.Vision, "VC_AlrightyResponse", new[] { AnimationTrigger.VC_Alrighty });
        Assert.True(Runnable(b, ctx));
        RunToEnd(rig, b, ctx);
        Assert.True(b.TurnedToFace);
        Assert.Contains(b.Trace, l => l.Contains("VC_Alrighty"));
        Assert.InRange(Math.Abs(rig.Angle - Math.Atan2(-150, 400)), 0, 0.05);
    }

    [Fact]
    public void AcknowledgeFaceGreetsANewFaceAndNotAgainWithinAMinute()
    {
        using var rig = FaceRig((7, new Vec3(350, 150, 250), "Jim"));
        rig.Frame();
        var ctx = Ctx(rig);
        var b = new AcknowledgeFaceBehavior(rig.Vision);
        b.Clock = () => 1000;
        Assert.True(Runnable(b, ctx));
        RunToEnd(rig, b, ctx);
        Assert.Equal(7, b.TargetFaceId);
        Assert.True(b.PlayedGreeting);
        Assert.Contains(b.Trace, l => l.Contains("shouldPlayGreeting:1"));
        Assert.Contains(b.Trace, l => l.Contains("AcknowledgeFaceNamed"));
        Assert.Contains(b.Trace, l => l.Contains("SayTextAction(\"Jim\")"));
        Assert.Contains(b.Trace, l => l.Contains("ReactedAcknowledgedFace"));
        Assert.False(Runnable(b, ctx));                                               // acknowledged already
        Assert.True(rig.Vision.Faces.HasTurnedTowardsFace(7));
        // the face moves (the manager's FacePositionUpdated trigger re-arms the reaction) within 60 s: turn, no greeting
        b.ReArm(7);
        b.Clock = () => 31000;
        RunToEnd(rig, b, ctx);
        Assert.False(b.PlayedGreeting);
        Assert.Contains(b.Trace, l => l.Contains("shouldPlayGreeting:0"));
        Assert.DoesNotContain(b.Trace, l => l.Contains("AcknowledgeFaceNamed"));
    }

    [Fact]
    public void InteractWithFacesVerifiesDrivesTracksAndFiresTheEmotionEvent()
    {
        using var rig = FaceRig((7, new Vec3(450, 60, 260), null));
        rig.Frame();
        var ctx = Ctx(rig);
        var b = new InteractWithFacesBehavior(rig.Vision, "InteractWithFaces", rig.M) { TrackTimeScale = 0.03 };
        Assert.True(Runnable(b, ctx));
        RunToEnd(rig, b, ctx, ms: 15000);
        Assert.Equal(7, b.TargetFaceId);
        Assert.Contains(b.Trace, l => l.Contains("InteractWithFacesInitialUnnamed"));
        Assert.Contains(b.Trace, l => l.Contains("DriveStraight(40 mm)"));
        Assert.Contains(b.Trace, l => l.Contains("will track for"));
        Assert.InRange(b.TrackSeconds!.Value / b.TrackTimeScale, 8, 15);
        Assert.Contains(b.Trace, l => l.Contains("InteractWithFaceTrackingIdle"));
        Assert.Contains(b.Trace, l => l.Contains("emotion event InteractWithUnnamedFace"));
        Assert.Contains(b.Trace, l => l.Contains("InteractedWithFace"));
        Assert.Contains(rig.Sent, m => m is AppendPathSegmentLine l && Math.Abs(l.XEndMm - l.XStartMm) is > 39 and < 41);
    }

    [Fact]
    public void DriveToFaceDrivesUntilTwoHundredMillimetresAway()
    {
        using var rig = FaceRig((7, new Vec3(600, 0, 250), null));
        rig.Frame();
        var ctx = Ctx(rig);
        var b = new DriveToFaceBehavior(rig.Vision, rig.M) { TrackTimeScale = 0.05 };
        Assert.True(Runnable(b, ctx));
        RunToEnd(rig, b, ctx, ms: 15000);
        Assert.Equal(DriveToFaceBehavior.Phase.Idle, b.CurrentPhase);
        Assert.InRange(b.DistanceMm!.Value, 590, 610);
        var line = rig.Sent.OfType<AppendPathSegmentLine>().Single();
        Assert.Equal(60f, line.Speed.SpeedMmps);
        Assert.InRange(rig.X, 390, 410);                                              // stopped 200 mm from the face
        Assert.Contains(b.Trace, l => l.Contains("VisuallyVerifyFace -> Success"));
        // close already: no drive
        using var near = FaceRig((7, new Vec3(160, 0, 120), null));                   // a low face close by, inside the camera's view
        near.Frame();
        var b2 = new DriveToFaceBehavior(near.Vision, near.M) { TrackTimeScale = 0.05 };
        RunToEnd(near, b2, Ctx(near), ms: 15000);
        Assert.Contains(b2.Trace, l => l.Contains("already close enough"));
        Assert.DoesNotContain(near.Sent, m => m is AppendPathSegmentLine);
    }

    [Fact]
    public void SearchForFacePlaysTheSearchUntilAFaceAppears()
    {
        using var rig = FaceRig();
        var ctx = Ctx(rig);
        var b = new SearchForFaceBehavior(rig.Vision);
        int updates = 0;
        double t = 0;
        b.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        while (b.Update(ctx, t))
        {
            rig.Pump(); rig.Frame(); t += 33;
            if (++updates == 1) rig.FaceDetector.Faces.Add((7, new Vec3(400, 0, 250), null));   // someone walks in during the first search
            if (updates > 500) throw new TimeoutException(string.Join(" | ", b.Trace));
            Thread.Sleep(2);
        }
        Assert.True(b.Found);
        Assert.Contains(b.Trace, l => l.Contains("ComeHere_SearchForFace"));
        Assert.Contains(b.Trace, l => l.Contains("ComeHere_SearchForFace_FoundFace"));
        // nobody: the search repeats up to the bound and gives up
        using var empty = FaceRig();
        var b2 = new SearchForFaceBehavior(empty.Vision);
        RunToEnd(empty, b2, Ctx(empty), frames: false);
        Assert.False(b2.Found);
        Assert.Equal(SearchForFaceBehavior.MaxSearches, b2.Searches);
    }

    [Fact]
    public void ReactToPetTurnsToThePetAndPlaysItsTrigger()
    {
        using var rig = new Rig();
        rig.Vision.Pets.Update(new[] { new DetectedPet(3, PetType.Cat, new FaceRect(100, 80, 60, 60)) }, 1000, false);
        var ctx = Ctx(rig);
        var b = new ReactToPetBehavior(rig.Vision);
        Assert.True(Runnable(b, ctx));
        var rnd = new Random(1);
        Assert.Contains(b.GetAnimationTrigger(PetType.Cat, new Random(2)), new[] { AnimationTrigger.PetDetectionCat, AnimationTrigger.PetDetectionSneeze });
        Assert.Equal(AnimationTrigger.PetDetectionDog, Enumerable.Range(0, 50).Select(_ => b.GetAnimationTrigger(PetType.Dog, rnd)).First(t => t != AnimationTrigger.PetDetectionSneeze));
        RunToEnd(rig, b, ctx, frames: false);
        Assert.Equal(3, b.TargetPetId);
        Assert.Contains(b.Trace, l => l.Contains("PetDetection"));
        // a pet not reported for three frames is dropped
        rig.Vision.Pets.Update(Array.Empty<DetectedPet>(), 1100, false); rig.Vision.Pets.Update(Array.Empty<DetectedPet>(), 1200, false);
        Assert.Single(rig.Vision.Pets.Pets);
        rig.Vision.Pets.Update(Array.Empty<DetectedPet>(), 1300, false);
        Assert.Empty(rig.Vision.Pets.Pets);
    }

    [Fact]
    public void TheFaceSetHasFourteenConfigsAndNeedsADetector()
    {
        using var rig = new Rig();
        var set = ShippedBehaviors.Faces(rig.Vision, rig.M);
        Assert.Equal(14, set.Count);
        Assert.Equal(14, set.Select(b => b.Id).Distinct().Count());
        Assert.Equal(12, ShippedBehaviors.Faces(rig.Vision).Count);                    // the two that drive need the manipulation system
        var ctx = Ctx(rig);
        foreach (var b in set.OfType<SteppedBehavior>().Where(b => b is not SearchForFaceBehavior)) Assert.False(Runnable(b, ctx), b.Id);
    }
}
