using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Emulation;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// FACE-ENROLL-VISION-GAPS-1 (SCOPE.md B Faces): the enrollment gate, the vision history's owner on the production frame path,
/// and the turn-to-face head-angle failure branch on the native solver. Every expected value is read from the binary at the cited
/// address, not from the C#.
/// </summary>
[Collection("M5 process statics")]
public sealed class FaceEnrollVisionGapsTests
{
    private static uint B(float f) => BitConverter.SingleToUInt32Bits(f);

    /// <summary>
    /// <c>FaceTracker::Impl::IsEnrollable</c> 0x0086E074 with the only pose production sets (0: <c>FaceWorld::Enroll</c> passes
    /// <c>movs r1,#0</c> at 0x004F5CAC/0x004F5CCE to <c>VisionComponent::SetFaceEnrollmentMode</c>, whose <c>Impl</c> step stores it at
    /// +0x134, 0x0086E372). The confidence word +0xC must be over 500 (<c>cmp.w r0,#0x1f4; ble</c> 0x0086E080); the pose-0 case
    /// (tbb entry 4 -> 0x0086E0A8) needs word +0x24 == 0 (<c>ldr r1,[r5,#0x24]; cmp r1,#0; bne</c>), an intra-eye distance of at least 16.0
    /// (<c>vmov.f32 s2,#16.0; vcmpe; movge</c>) and the TrackedFace byte +0x11 set (<c>ldrb r1,[r4,#0x11]</c>). reach: unit.
    /// </summary>
    [Fact]
    public void TheDefaultPoseEnrollmentGateFollowsIsEnrollable()
    {
        Assert.False(OkaoFaceDetection.IsEnrollableDefaultPose(500, 0, true, 40));      // ble at 0x0086E084: 500 is not enough
        Assert.True(OkaoFaceDetection.IsEnrollableDefaultPose(501, 0, true, 40));
        Assert.False(OkaoFaceDetection.IsEnrollableDefaultPose(900, 1, true, 40));      // +0x24 nonzero
        Assert.False(OkaoFaceDetection.IsEnrollableDefaultPose(900, -45, true, 40));    // the pose 3 value, not accepted by pose 0
        Assert.False(OkaoFaceDetection.IsEnrollableDefaultPose(900, 0, true, 15.999f));
        Assert.True(OkaoFaceDetection.IsEnrollableDefaultPose(900, 0, true, 16));       // movge: 16.0 itself passes
        Assert.False(OkaoFaceDetection.IsEnrollableDefaultPose(900, 0, false, 40));     // +0x11 clear
        Assert.False(OkaoFaceDetection.IsEnrollableDefaultPose(900, 0, false, null));
    }

    /// <summary>
    /// The vision history's wiring guard (VisionSystem.History, M11-069). HandleImageChunk exits unless robot+0x29 is set (0x00535A7A),
    /// and the camera's gate is the engine Robot's flag, false with no Robot: no image reaches vision before ConnectToRobot's AddRobot
    /// (0x004ED1E0..0x004ED1F2) or after RemoveRobot. While the Robot exists the history is its Robot+0x390 (SetNewPose
    /// 0x00512708..0x0051271C). reach: production (ConnectToRobot, the transport's disconnect marker, the camera's chunk handler).
    /// </summary>
    [Fact]
    public void NoImageReachesVisionWithoutARobotAndTheHistoryIsTheRobots()
    {
        using var rig = new M3DeviceTests.Rig();
        var camera = rig.Robot.Camera;
        ImageChunk Chunk(uint id) => new() { ImageId = id, ChunkId = 0, ImageChunkCount = 1, ImageEncoding = 8, ImageResolution = 4, Data = new byte[] { 0, 1 } };
        var frames = new List<CameraFrame>();
        camera.FrameForVision += frames.Add;
        Assert.Null(rig.Engine.Robot);
        camera.Handle(Chunk(1));
        Assert.Equal(1, camera.ChunksIgnoredBeforeSync);
        rig.ToValidated();
        var robot = Assert.IsType<EngineRobot>(rig.Engine.Robot);
        Assert.Same(robot.PoseHistory, rig.Robot.Vision.History);
        robot.TimeSynced = true;
        camera.Handle(Chunk(2));
        Assert.Equal(1, camera.ChunksReceived);
        int framesWithRobot = frames.Count;
        rig.Disconnected();
        rig.Tick();
        Assert.Null(rig.Engine.Robot);
        Assert.Equal(0, camera.ChunksIgnoredBeforeSync);   // the removal rebuilds the camera's state, counters included
        camera.Handle(Chunk(3));
        Assert.Equal(1, camera.ChunksIgnoredBeforeSync);
        Assert.Equal(0, camera.ChunksReceived);
        Assert.Equal(framesWithRobot, frames.Count);
    }

    /// <summary>
    /// TurnTowardsPoseAction::Init on the native solver overload (the one NativeTurnTowardsPoseRunner, and so the face turn, calls).
    /// A non-zero <c>Robot::ComputeHeadAngleToSeePose</c> result (0x0054AB1A) warns "TurnTowardsPoseAction.Init.FailedToComputedHeadAngle"
    /// with "PoseWrtRobot translation=(%f,%f,%f)" (strings at 0x0069FE4C/0x0069FE7F, the call at 0x0054AB66), falls back to
    /// GetAbsoluteHeadAngleToLookAtPose (0x0054AB9A -> 0x0054B428) and clamps into [0xBEDF66F3, 0x3F46D3F2] (0x0054ABA6..0x0054ACBC).
    /// Straight above the neck (-13, 0, 49 + 351) projects at or behind the camera ("BadProjectedZ", 0x00518494); the fallback there is
    /// atan2f(351, 26) + 0.0872665 + 0.1308997 + 0.0698132 (both ramps saturated), about 1.78 rad, so the clamp gives 0x3F46D3F2. With
    /// no calibration ("NullCamera", 0x0051851E) the (400, 0, 0) fallback is the engine's 0xBD45C00E. Max turn 0 skips the pan
    /// (0x0054AC8A). reach: unit (the runner's env built as NativeTurnTowardsPoseRunner.Init builds it).
    /// </summary>
    [Fact]
    public void TheNativeSolverFailureFallsBackWithTheEngineWarning()
    {
        using var origin = new Pose3d("origin");
        using var main = new Pose3d(Rotation3d.Identity, Vec3.Zero, "main", origin);
        var cal = CameraCalibration.Nominal();
        (uint Result, List<string> Texts, float Head) Run(CameraCalibration? calibration, Vec3 at)
        {
            var texts = new List<string>();
            using var goal = new Pose3d(Rotation3d.Identity, at, "goal", main);
            using var relative = new Pose3d("");
            var env = new TurnTowardsPoseEnv(main, 0,
                p => TurnTowardsPose.ComputeHeadAngleToSeePose(calibration, p, TurnTowardsPoseCompound.SeePoseToleranceRad, out var a, texts.Add, main) == 0 ? a : null,
                null, p => p.TryWithRespectTo(main, relative) ? relative : null, texts.Add);
            var compound = new TurnTowardsPoseCompound(goal, 0);
            uint r = compound.InitPose(env, out bool build);
            Assert.True(build);
            return (r, texts, (float)compound.HeadAngleRad);
        }
        var above = Run(cal, new Vec3(-13, 0, 400));
        Assert.Equal(0u, above.Result);
        Assert.Contains("Robot.ComputeHeadAngleToSeePose.BadProjectedZ", above.Texts);
        Assert.Contains(above.Texts, t => t.StartsWith("TurnTowardsPoseAction.Init.FailedToComputedHeadAngle: PoseWrtRobot translation=(-13.000000,0.000000,400.000000)", StringComparison.Ordinal));
        Assert.Equal(0x3F46D3F2u, B(above.Head));
        var noCal = Run(null, new Vec3(400, 0, 0));
        Assert.Contains("Robot.ComputeHeadAngleToSeePose.NullCamera", noCal.Texts);
        Assert.Contains(noCal.Texts, t => t.StartsWith("TurnTowardsPoseAction.Init.FailedToComputedHeadAngle: PoseWrtRobot translation=(400.000000,0.000000,0.000000)", StringComparison.Ordinal));
        Assert.Equal(0xBD45C00Eu, B(noCal.Head));
        var ahead = Run(cal, new Vec3(200, 0, 60));
        Assert.DoesNotContain(ahead.Texts, t => t.Contains("FailedToComputedHeadAngle", StringComparison.Ordinal));
    }
}
