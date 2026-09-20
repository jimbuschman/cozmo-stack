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
        Assert.Contains(rig.Sent, m => m is AppendPathSegmentLine l && Math.Abs(BitConverter.UInt32BitsToSingle(l.Field2) - BitConverter.UInt32BitsToSingle(l.Field0)) is > 39 and < 41);
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
        Assert.Equal(60f, line.Field4.SpeedMmps);
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
