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
        double f = cal.FocalLengthX;

        // C2-F23 parts branch: eye centres at the projected head point, eye distance = eyePx, roll 0.
        var target = new Vec3(400, 100, 250);
        var px = cam.Project(target)!.Value;
        double range = (target - cam.Pose.Translation).Length;
        double eyePx = TrackedFace.InterPupilDistanceMm * f / range;
        var parts = new TrackedFace(new DetectedFace(4, new FaceRect(px.X - eyePx, px.Y - eyePx, 2 * eyePx, 2 * eyePx),
            new Vec2(px.X - eyePx / 2, px.Y), new Vec2(px.X + eyePx / 2, px.Y), RollRad: 0.0), 1000);
        Assert.True(parts.HasEyeParts);
        parts.UpdateTranslation(cam);
        Assert.Equal(eyePx, TrackedFace.GetIntraEyeDistance(parts.Detection.LeftEye!.Value, parts.Detection.RightEye!.Value, 0.0), 4);   // binary32 (M14-001): the eye slots are floats
        Assert.Equal(TrackedFace.InterPupilDistanceMm * f / eyePx, parts.DistanceMm, 3);
        Assert.InRange((parts.HeadPose.Translation - target).Length, 0, 0.05);   // range along the ray through px

        // C2-F23 box branch: the zeroed eye slots make the midpoint pixel (0,0); the engine's invK*(0,0,1)
        // is Ray(pixel (0,0)), which is off-axis for a non-centred principal point. A 124 px box gives
        // |0.5 * 124| = 62 px.
        var box = new TrackedFace(new DetectedFace(3, new FaceRect(100, 200, 124, 124)), 1000);
        Assert.False(box.HasEyeParts);
        box.UpdateTranslation(cam);
        Assert.Equal(62.0, TrackedFace.InterPupilDistanceMm * f / box.DistanceMm, 6);
        Assert.Equal(TrackedFace.InterPupilDistanceMm * f / 62.0, box.DistanceMm, 6);
        var (ray0Origin, ray0Dir) = cam.Ray(new Vec2(0, 0));
        var expected0 = ray0Origin + ray0Dir.Normalized() * box.DistanceMm;
        Assert.InRange((box.HeadPose.Translation - expected0).Length, 0, 0.05);
        Assert.NotEqual(0.0, box.HeadPose.Translation.X - cam.Pose.Apply(new Vec3(0, 0, box.DistanceMm)).X);   // not the optical axis

        // C2-F23 GetIntraEyeDistance: dist / divisor, divisor = 1.0 when |cos| < 1e-5 else the signed cos;
        // dist < 1e-5 returns 6.0 / divisor.
        Assert.Equal(62.0, TrackedFace.GetIntraEyeDistance(new Vec2(100, 100), new Vec2(162, 100), 0.0), 6);
        Assert.Equal(70.64863, TrackedFace.GetIntraEyeDistance(new Vec2(100, 100), new Vec2(162, 100), 0.5), 4);   // 62 / cosf(0.5f) in binary32 (numpy float32 emulation of 0x0087DD8A)
        Assert.Equal(-62.0, TrackedFace.GetIntraEyeDistance(new Vec2(100, 100), new Vec2(162, 100), Math.PI), 6);
        Assert.Equal(62.0, TrackedFace.GetIntraEyeDistance(new Vec2(100, 100), new Vec2(162, 100), Math.PI / 2), 6);   // |cos| < 1e-5 -> divisor 1.0
        Assert.Equal(6.0, TrackedFace.GetIntraEyeDistance(new Vec2(100, 100), new Vec2(100, 100), 0.0), 6);            // dist < 1e-5 -> 6.0 / 1.0
        Assert.Equal(0x3727C5ACu, BitConverter.SingleToUInt32Bits((float)TrackedFace.MinCosOrDistance));   // R-FIX M14-001: the literal at 0x0087DDC4, not the double 1e-5

        // MISSING: a parts face (eyes present) with no roll is an incomplete detector input; it is not defaulted.
        var noRoll = new TrackedFace(new DetectedFace(6, new FaceRect(0, 0, 10, 10), new Vec2(100, 100), new Vec2(162, 100)), 1000);
        Assert.Throws<NotSupportedException>(() => noRoll.UpdateTranslation(cam));

        Assert.Equal(FacialExpression.Unknown, box.MaxExpression());
        Assert.Equal(FacialExpression.Happiness, new TrackedFace(new DetectedFace(5, new FaceRect(0, 0, 1, 1), ExpressionValues: new byte[] { 10, 80, 5, 0, 0 }), 1).MaxExpression());
    }

    [Fact]
    public void TheFaceWorldMatchesByIdForgetsUnnamedFacesAndKeepsNamedOnes()
    {
        var world = new FaceWorld();
        var robot = Pose3d.Identity;
        var cam = new CameraModel(CameraCalibration.Nominal(), HeadGeometry.CameraPoseInWorld(robot, 0.2));
        TrackedFace Face(int id, double x, double y, double z, uint ts, string? name = null)
        {
            var px = cam.Project(new Vec3(x, y, z))!.Value; double dist = (new Vec3(x, y, z) - cam.Pose.Translation).Length;
            double eye = 62 * cam.Calibration.FocalLengthX / dist, w = 2 * eye;
            // a normal face has parts (eye centres at the projected point, roll 0)
            var tf = new TrackedFace(new DetectedFace(id, new FaceRect(px.X - w / 2, px.Y + 0.125 * w - w / 2, w, w),
                new Vec2(px.X - eye / 2, px.Y), new Vec2(px.X + eye / 2, px.Y), Name: name, RollRad: 0.0), ts);
            tf.UpdateTranslation(cam); return tf;
        }
        // C2-F7: the reachable match is by the tracker id, not by pose
        var o1 = world.AddOrUpdateFace(Face(1, 400, 0, 300, 1000), robot, false);
        Assert.NotNull(o1); Assert.True(o1!.IsNew); Assert.Equal(1, o1.Face.Id);
        Assert.InRange((o1.Face.HeadPose.Translation - new Vec3(400, 0, 300)).Length, 0, 5);
        // the same tracker id a little later, 100 mm away: update, not a new entry
        var o2 = world.AddOrUpdateFace(Face(1, 420, 90, 300, 1100), robot, false);
        Assert.False(o2!.IsNew); Assert.Equal(1, o2.Face.Id); Assert.Equal(2, o2.Face.TimesObserved);
        // a different tracker id is another person, even at the same pose
        var o3 = world.AddOrUpdateFace(Face(2, 420, 90, 300, 1200, "Jim"), robot, false);
        Assert.True(o3!.IsNew); Assert.Equal(2, o3.Face.Id); Assert.True(o3.Face.HasName);
        // C2-F7c: the rotating gate is new-entry-only; an existing face updates while rotating
        var rotating = world.AddOrUpdateFace(Face(1, 400, 0, 300, 1300), robot, rotatingTooFast: true);
        Assert.NotNull(rotating); Assert.False(rotating!.IsNew); Assert.Equal(1, rotating.Face.Id);
        // below-robot still drops
        Assert.Null(world.AddOrUpdateFace(Face(1, 400, 0, -50, 1300), robot, false));
        // C2-F7b: an older timestamp logs and continues; it is not rejected
        var regression = world.AddOrUpdateFace(Face(1, 420, 90, 300, 900), robot, false);
        Assert.NotNull(regression); Assert.Equal(1, regression!.Face.Id); Assert.Equal(900u, regression.Face.LastObservedTimestamp);
        Assert.Equal(new[] { 1, 2 }, world.GetFaceIDs().OrderBy(i => i));
        Assert.Equal(new[] { 2 }, world.GetFaceIDs(namedOnly: true));
        Assert.True(world.HasAnyFaces(900)); Assert.False(world.HasAnyFaces(1201));
        // 15 s after id 1's last observation the unnamed face is forgotten, the named one stays
        var removed = world.Update(900 + FaceWorld.UnnamedFaceLifetimeMs + 1);
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
        // no face at all, +0x193 clear: Init sets state 3 and returns 0 (0x0054BEB6..0x0054BEBC), so the action succeeds; it never returns 0x0300000B (M13-014)
        using var empty = FaceRig();
        Assert.Equal(FaceActionResult.Success, new TurnTowardsFaceAction(empty.Vision, SmartFaceID.Invalid).RunAsync(default).GetAwaiter().GetResult());
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
        // R-FIX M14-003: the binary32 words the constructor 0x00564xxx stores (0x005646BC..0x0056473A), not rounded decimals
        Assert.Equal(0x3D0EFA35u, BitConverter.SingleToUInt32Bits((float)TrackFaceAction.MinToleranceRad));
        Assert.Equal(0x3F46D3F2u, BitConverter.SingleToUInt32Bits((float)TrackFaceAction.MaxHeadAngleRad));
        Assert.Equal(0x3E32B8C2u, BitConverter.SingleToUInt32Bits((float)TrackFaceAction.MinAngleForSoundRad));
        Assert.Equal((double)(float)TrackFaceAction.MinToleranceRad, TrackFaceAction.MinToleranceRad);
        Assert.Equal(10000, TrackFaceAction.TrackAccelRadPerSec2);
        Assert.Equal(60, TrackFaceAction.UpdateIntervalMs);   // the 60 ms basestation tick, not the M5 33 ms keep-alive

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
        Assert.Equal(0.5, body, 6);       // R-FIX M14-005: binary32 (atan2f(-0, fx) + 0.5f, vadd.f32)
        Assert.Equal(0.2, head, 6);

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

    // ------------------------------------------------------------------ M14-001 / M14-008 / M14-009 / M14-012

    /// <summary>
    /// The constants the inventory records, exactly (SD4): an unnamed face expires after 15000 ms
    /// (0x004F5380 loads 0x3A98), the eye-distance floor is 6.0 (0x0087DF0A vmov.f32 s0,#6.0), and the
    /// 62.0 factor's literal is at 0x0087E084 (vldr 0x0087E014). The 220^2 match distance (0x004F4428 loads 0x473D1000) and the
    /// overlap score are present but on the dead C2-F7 branch, since IsRecognitionSupported 0x0086B244
    /// returns 1 unconditionally.
    /// </summary>
    [Fact]
    public void M14_001_TheFaceMatchForgettingAndEyeDistanceConstantsAreTheEnginesOwn()
    {
        Assert.Equal(15000u, FaceWorld.UnnamedFaceLifetimeMs);
        Assert.Equal(6.0, TrackedFace.MinIntraEyeDistancePx, 6);
        Assert.Equal(0x3727C5ACu, BitConverter.SingleToUInt32Bits((float)TrackedFace.MinCosOrDistance));   // R-FIX M14-001: 0x0087DDC4
        Assert.Equal(62.0, TrackedFace.InterPupilDistanceMm, 6);
        Assert.Equal(0x42780000u, BitConverter.SingleToUInt32Bits((float)TrackedFace.InterPupilDistanceMm));   // literal 0x0087E084
        // the 220^2 / overlap constants are present but on the dead C2-F7 branch
        Assert.Equal(48400.0, FaceWorld.MatchDistanceSquaredMm, 6);
        Assert.Equal(0.5, FaceWorld.MatchOverlapScore, 6);
    }

    /// <summary>
    /// M14-001 (C2-F7/C2-F7b/C2-F7c): the reachable match is the map lookup keyed by the tracked face's
    /// id; a different id is a new entry (no pose matching); a timestamp regression logs and continues; the
    /// rotating gate is on the new-entry path only; and a no-parts observation of a known face keeps its
    /// previous translation.
    /// </summary>
    [Fact]
    public void M14_001_FacesMatchByTrackerIdAndTimestampRegressionsContinue()
    {
        static TrackedFace At(int id, Vec3 head, uint ts, string? name = null) =>
            new(new DetectedFace(id, new FaceRect(1000, 1000, 10, 10), Name: name), ts) { HeadPose = new Pose3d(Mat3.Identity, head) };

        var world = new FaceWorld();
        var first = world.AddOrUpdateFace(At(5, new Vec3(0, 0, 0), 1000), Pose3d.Identity, false)!;
        Assert.True(first.IsNew); Assert.Equal(5, first.Face.Id);
        // the same id updates even when the pose is 400 mm away (no pose matching)
        var same = world.AddOrUpdateFace(At(5, new Vec3(0, 0, 400), 1100), Pose3d.Identity, false)!;
        Assert.False(same.IsNew); Assert.Equal(5, same.Face.Id); Assert.Equal(1, world.Count);
        // a different id is a new entry even at the same pose
        var other = world.AddOrUpdateFace(At(6, new Vec3(0, 0, 400), 1100), Pose3d.Identity, false)!;
        Assert.True(other.IsNew); Assert.Equal(6, other.Face.Id); Assert.Equal(2, world.Count);
        // C2-F7b: a regression logs and continues (it is not rejected)
        var regression = world.AddOrUpdateFace(At(5, new Vec3(0, 0, 400), 900), Pose3d.Identity, false)!;
        Assert.False(regression.IsNew); Assert.Equal(900u, regression.Face.LastObservedTimestamp);
        // C2-F7c: the rotating gate is new-entry-only; an existing face updates while rotating
        var still = world.AddOrUpdateFace(At(5, new Vec3(0, 0, 400), 1200), Pose3d.Identity, rotatingTooFast: true)!;
        Assert.False(still.IsNew); Assert.Equal(5, still.Face.Id);
        // a new id while rotating is not added
        Assert.Null(world.AddOrUpdateFace(At(7, new Vec3(0, 0, 0), 1200), Pose3d.Identity, rotatingTooFast: true));
        Assert.Null(world.GetFace(7));
        // below-robot still drops
        Assert.Null(world.AddOrUpdateFace(At(5, new Vec3(0, 0, -50), 1200), Pose3d.Identity, false));

        // C2-F7c: on the found path a no-parts observation keeps the entry's previous translation; its
        // rotation still comes from the TrackedFace. A parts observation takes the full pose.
        var poseWorld = new FaceWorld();
        var partsPose = new Pose3d(Mat3.AboutZ(0.3), new Vec3(10, 20, 30));
        var parts = poseWorld.AddOrUpdateFace(new TrackedFace(new DetectedFace(9, new FaceRect(0, 0, 10, 10),
            new Vec2(100, 100), new Vec2(162, 100), RollRad: 0.0), 1000) { HeadPose = partsPose }, Pose3d.Identity, false)!;
        Assert.Equal(partsPose.Translation, parts.Face.HeadPose.Translation);
        var noPartsPose = new Pose3d(Mat3.AboutZ(0.9), new Vec3(100, 200, 300));
        var noParts = poseWorld.AddOrUpdateFace(new TrackedFace(new DetectedFace(9, new FaceRect(0, 0, 10, 10)), 1100) { HeadPose = noPartsPose }, Pose3d.Identity, false)!;
        Assert.Equal(partsPose.Translation, noParts.Face.HeadPose.Translation);
        Assert.Equal(0.9, noParts.Face.HeadPose.AngleAroundZ, 6);
    }

    /// <summary>
    /// M14-008: the observable lifecycle the stack's consumers use, as the M11-038 precedent represents
    /// the engine's broadcasts. An add raises FaceObserved, a change raises FaceIdChanged, and the 15 s
    /// expiry raises FaceDeleted.
    /// </summary>
    [Fact]
    public void M14_008_TheFaceWorldRaisesObservedChangedAndDeleted()
    {
        var world = new FaceWorld();
        var observed = new List<int>();
        var deleted = new List<int>();
        var changed = new List<(int Old, int New)>();
        world.FaceObserved += o => observed.Add(o.Face.Id);
        world.FaceDeleted += id => deleted.Add(id);
        world.FaceIdChanged += (a, b) => changed.Add((a, b));

        var tf = new TrackedFace(new DetectedFace(7, new FaceRect(0, 0, 10, 10)), 1000);
        var o = world.AddOrUpdateFace(tf, Pose3d.Identity, false);
        Assert.NotNull(o);
        Assert.Equal(new[] { 7 }, observed);

        int oldId = o.Face.Id;
        Assert.True(world.ChangeFaceID(oldId, 42));
        Assert.Equal((oldId, 42), changed.Single());

        var removed = world.Update(1000 + FaceWorld.UnnamedFaceLifetimeMs + 1);
        Assert.Equal(new[] { 42 }, removed);
        Assert.Equal(new[] { 42 }, deleted);
    }

    /// <summary>
    /// M14-009: <c>FaceWorld::ShouldReturnFace</c> 0x004F55B8 rejects when the entry's last observation
    /// (entry+8) is before the time, rejects an id below 1 when the bool is set (entry+0 &lt; 1), and
    /// <c>FaceWorld::Enroll</c> 0x004F5C9E selects mode 4 for a nonzero id and -1 for zero.
    /// </summary>
    [Fact]
    public void M14_009_ShouldReturnFaceAndEnrollUseTheEnginesGates()
    {
        var world = new FaceWorld();
        var entry = new FaceEntry { Id = 7, LastObservedTimestamp = 2000 };
        Assert.True(world.ShouldReturnFace(entry, 1999));
        Assert.True(world.ShouldReturnFace(entry, 2000));                 // entry+8 == time is not rejected (strict <)
        Assert.False(world.ShouldReturnFace(entry, 2001));

        var sessionOnly = new FaceEntry { Id = 0, LastObservedTimestamp = 2000 };
        Assert.True(world.ShouldReturnFace(sessionOnly, 2000));           // the bool is off: the id is not tested
        Assert.False(world.ShouldReturnFace(sessionOnly, 2000, requireId: true));

        Assert.Equal(4, FaceWorld.EnrollModeKnownFace);
        Assert.Equal(-1, FaceWorld.EnrollModeNewFace);
        Assert.Equal((7, 4), world.Enroll(7));
        Assert.Equal((0, -1), world.Enroll(0));
    }

    /// <summary>
    /// M14-012 (F24/F25/F27): <c>LoadFaceAlbumFromRobot</c> reads NV 0x184000 first and 0x183000 second;
    /// the enrollment completion installs the data and replays the loaded names as
    /// <c>RobotErasedAllEnrolledFaces</c> first, then one <c>LoadedKnownFace</c> per entry.
    /// </summary>
    [Fact]
    public void M14_012_TheFaceAlbumReadsAlbumThenEnrollmentAndReplaysEraseBeforeNames()
    {
        using var rig = new Rig();
        int erased = 0;
        var loaded = new List<(int Id, string Name)>();
        rig.Vision.EnrolledFacesErased += () => erased++;
        rig.Vision.LoadedFaceName += (id, name) => loaded.Add((id, name));
        // a named face already in the world, so the replay has something to carry
        var tf = new TrackedFace(new DetectedFace(5, new FaceRect(0, 0, 10, 10), Name: "Jim"), 1000);
        Assert.NotNull(rig.Vision.Faces.AddOrUpdateFace(tf, Pose3d.Identity, false));

        rig.Vision.LoadFaceAlbumFromRobot();
        rig.Tick();                                                    // M3-026: Update sends the queued read
        rig.Pump();
        var first = rig.Sent.OfType<NVCommand>().Last();
        Assert.Equal(0x184000u, first.Tag);                               // album first
        Assert.Equal(NvStorageComponent.OpRead, first.Op);
        Assert.Equal(NvStorageComponent.NonFactoryReadLength, first.Length);

        ReplyNonFactoryRead(rig, 0x184000, Array.Empty<byte>());           // an empty album
        var second = rig.Sent.OfType<NVCommand>().Last();
        Assert.Equal(0x183000u, second.Tag);                              // enrollment second
        Assert.Equal(NvStorageComponent.OpRead, second.Op);

        ReplyNonFactoryRead(rig, 0x183000, Array.Empty<byte>());
        Assert.Equal(1, erased);
        Assert.Equal(new[] { (5, "Jim") }, loaded);
    }

    /// <summary>
    /// M14-012 (F26/G1-4): <c>SaveFaceAlbumToRobot</c> writes the album (0x184000) first and the
    /// enrollment (0x183000) second, rounds each vector size up to a four-byte boundary, and erases the
    /// tag instead when the data is empty. The serialization format itself is MISSING (G2-6/G3-4).
    /// </summary>
    [Fact]
    public void M14_012_TheFaceAlbumWritesAlbumThenEnrollmentPaddedAndErasesWhenEmpty()
    {
        using var rig = new Rig();
        rig.Vision.InstallSerializedFaceData(new byte[] { 1, 2, 3 }, new byte[] { 4, 5 });
        rig.Vision.SaveFaceAlbumToRobot();
        rig.Tick();                                                        // M3-026: Update sends the queued write
        rig.Pump();

        var album = rig.Sent.OfType<NVCommand>().Last();
        Assert.Equal(0x184000u, album.Tag);                               // album first
        Assert.Equal(NvStorageComponent.OpWrite, album.Op);
        Assert.Equal(4, album.Length);                                    // 3 rounded to a four-byte boundary
        Assert.Equal(new byte[] { 1, 2, 3, 0 }, album.Data);

        ReplyNonFactoryRead(rig, 0x184000, Array.Empty<byte>());           // complete the first write
        var enrollment = rig.Sent.OfType<NVCommand>().Last();
        Assert.Equal(0x183000u, enrollment.Tag);                          // enrollment second
        Assert.Equal(NvStorageComponent.OpWrite, enrollment.Op);
        Assert.Equal(4, enrollment.Length);                               // 2 rounded up
        Assert.Equal(new byte[] { 4, 5, 0, 0 }, enrollment.Data);

        // empty data takes the erase path
        using var empty = new Rig();
        empty.Vision.InstallSerializedFaceData(Array.Empty<byte>(), Array.Empty<byte>());
        empty.Vision.SaveFaceAlbumToRobot();
        empty.Tick();                                                      // M3-026: Update sends the queued erase
        empty.Pump();
        var erase = empty.Sent.OfType<NVCommand>().Last();
        Assert.Equal(0x184000u, erase.Tag);
        Assert.Equal(NvStorageComponent.OpErase, erase.Op);
    }

    /// <summary>Answers the in-flight non-factory NV read/write with a valid 16-byte header and a payload.</summary>
    private static void ReplyNonFactoryRead(Rig rig, uint tag, byte[] payload)
    {
        var data = new byte[16 + payload.Length];
        BitConverter.GetBytes(NvStorageComponent.NonFactoryHeaderMagic).CopyTo(data, 0);
        BitConverter.GetBytes((uint)payload.Length).CopyTo(data, 8);
        payload.CopyTo(data, 16);
        rig.Send(new NVOpResult { Tag = tag, Op = NvStorageComponent.OpRead, Result = NvStorageComponent.ResultOkay, Length = 0, Data = data });
        rig.Pump();
    }
}
