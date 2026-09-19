using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Transport;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// Regressions from the 2026-09-19 Source Fidelity Sweep (re-analysis/SOURCE_FIDELITY_AUDIT.md). Each
/// asserts a value or a behaviour recovered from libcozmoEngine.so or the shipped OBB against which the
/// previous implementation differed, so that the invented version cannot come back unnoticed. Every
/// constant here names where in the binary it comes from.
/// </summary>
public class SourceFidelityTests
{
    private static string? AssetsRoot()
    {
        var env = Environment.GetEnvironmentVariable("COZMO_ASSETS");
        if (!string.IsNullOrEmpty(env) && Directory.Exists(env)) return env;
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "re-analysis", "obb", "assets", "cozmo_resources", "assets");
            if (Directory.Exists(Path.Combine(candidate, "animations"))) return candidate;
            dir = dir.Parent;
        }
        return null;
    }

    // ================================================================ the face model

    /// <summary>
    /// ProceduralFace::Interpolate at 0x00584290 blends the eye angle (parameter 4) and the face angle as
    /// directions: cos and sin blended separately, then atan2f. Linear blending of the degrees, which this
    /// did before, agrees at the midpoint and nowhere else for large spans.
    /// </summary>
    [Fact]
    public void AnglesInterpolateAsDirectionsNotAsNumbers()
    {
        Assert.Equal(19.1f, Eye.BlendAngleDeg(0f, 120f, 0.25f), 1);      // linear would give 30
        Assert.Equal(60f, Eye.BlendAngleDeg(0f, 120f, 0.5f), 3);
        Assert.Equal(-145f, Eye.BlendAngleDeg(170f, -100f, 0.5f), 1);    // the short way round, through 180 (linear would give 35)
        Assert.Equal(37f, Eye.BlendAngleDeg(37f, 37f, 0.7f), 5);          // equal angles are left alone

        var a = ProceduralFaceRenderer.Nominal();
        var b = ProceduralFaceRenderer.Nominal();
        b.FaceAngle = 120f;
        b.Left[EyeParam.EyeAngle] = 120f;
        var q = a.BlendTo(b, 0.25f);
        Assert.Equal(19.1f, q.FaceAngle, 1);
        Assert.Equal(19.1f, q.Left[EyeParam.EyeAngle], 1);
        Assert.Equal(0f, q.Right[EyeParam.EyeAngle], 5);
    }

    /// <summary>
    /// The clip table ProceduralFace::Clip (0x005847A8) builds from .rodata at 0x00C5A97C: lid angles
    /// within +/-45 degrees, scales at least zero, the eight corner radii and the four lid values within
    /// 0..1; centres and the eye angle unbounded. SetEyeArrayHelper at 0x00583790 clips every asset value
    /// through it on load, and Interpolate clips every blended value.
    /// </summary>
    [Theory]
    [InlineData(EyeParam.UpperLidAngle, 90f, 45f)]
    [InlineData(EyeParam.LowerLidAngle, -90f, -45f)]
    [InlineData(EyeParam.EyeScaleX, -0.5f, 0f)]
    [InlineData(EyeParam.EyeScaleY, 7f, 7f)]
    [InlineData(EyeParam.UpperInnerRadiusX, 1.7f, 1f)]
    [InlineData(EyeParam.LowerOuterRadiusY, -0.2f, 0f)]
    [InlineData(EyeParam.UpperLidY, 1.5f, 1f)]
    [InlineData(EyeParam.LowerLidBend, 2f, 1f)]
    [InlineData(EyeParam.EyeCenterX, 300f, 300f)]
    [InlineData(EyeParam.EyeAngle, 720f, 720f)]
    public void EyeParametersAreClippedToTheEnginesRanges(EyeParam p, float value, float expected)
    {
        Assert.Equal(expected, Eye.Clip(p, value));
        var values = new float[Eye.ParamCount];
        values[(int)p] = value;
        Assert.Equal(expected, Eye.FromAsset(values)[p]);
    }

    [Fact]
    public void ANegativeFaceScaleBlendsToZero()
    {
        var a = ProceduralFaceRenderer.Nominal();
        var b = ProceduralFaceRenderer.Nominal();
        b.FaceScaleX = -3f;
        Assert.Equal(0f, a.BlendTo(b, 0.75f).FaceScaleX);     // -2 before the engine's floor
    }

    /// <summary>
    /// The engine's resting face is the procedural face keyframe of the animation that the group mapped to
    /// AnimationTrigger::NeutralFace holds (AnimationStreamer::AnimationStreamer at 0x00579F78, installed
    /// with ProceduralFace::SetResetData). In this build that is ag_neutral_face -> anim_neutral_eyes_01.
    /// The constants in ShippedNeutral are checked against the asset itself when the OBB is present.
    /// </summary>
    [Fact]
    public void TheShippedNeutralFaceMatchesTheNeutralEyesClip()
    {
        var root = AssetsRoot();
        if (root is null) return;
        var lib = AnimationLibrary.Open(root);
        var group = lib.GetGroup("ag_neutral_face");
        Assert.NotNull(group);
        var entry = Assert.Single(group!.Entries);
        Assert.Equal("anim_neutral_eyes_01", entry.Name);

        var clip = lib.GetClip(entry.Name);
        var face = Assert.Single(clip.Keyframes.OfType<FaceKeyframe>());
        var shipped = ProceduralFacePose.ShippedNeutral();
        for (int i = 0; i < Eye.ParamCount; i++)
        {
            Assert.Equal(face.Pose.Left[i], shipped.Left[i], 5);
            Assert.Equal(face.Pose.Right[i], shipped.Right[i], 5);
        }
        Assert.Equal(face.Pose.FaceScaleX, shipped.FaceScaleX, 5);
        Assert.Equal(face.Pose.FaceScaleY, shipped.FaceScaleY, 5);
        Assert.Equal(face.Pose.FaceAngle, shipped.FaceAngle, 5);
    }

    /// <summary>
    /// The resting face is not the nominal box: its eyes are 1.21 x wider and 0.91 x shorter than nominal
    /// and sit 9-10 px inward. The old default, all radii at 0.5 on the nominal box, was a guess; the
    /// radii happened to be right and the rest was not.
    /// </summary>
    [Fact]
    public void TheDefaultFaceIsTheShippedNeutralNotTheNominalBox()
    {
        using var robot = CozmoRobot.CreateOffline();
        var current = robot.Face.Current;
        Assert.Equal(9.169666f, current.Left[EyeParam.EyeCenterX], 5);
        Assert.Equal(-10.206374f, current.Right[EyeParam.EyeCenterX], 5);
        Assert.Equal(1.214333f, current.Left[EyeParam.EyeScaleX], 5);
        Assert.Equal(0.90528f, current.Right[EyeParam.EyeScaleY], 5);
        Assert.NotEqual(ProceduralFaceRenderer.Nominal().Left[EyeParam.EyeScaleX], current.Left[EyeParam.EyeScaleX]);
    }

    // ================================================================ the idle face

    /// <summary>
    /// The blink is the seven-frame table ProceduralFaceDrawer::GetNextBlinkFrame (0x00585F18) copies from
    /// .rodata at 0x00C5AAD8: scale multipliers (1.05, 0.85), (1.2, 0.6), (2.5, 0.1), (5.0, 0.05),
    /// (2.0, 0.15), (1.2, 0.7), (1.0, 0.9) at 33 ms each except the last at 100 ms, reached at the
    /// cumulative times, then the face restored 33 ms later. The previous blink shut the upper lids for
    /// 100 ms, which the engine never does.
    /// </summary>
    [Theory]
    [InlineData(0.0, 1.0, 1.0)]
    [InlineData(33.0, 1.05, 0.85)]
    [InlineData(66.0, 1.2, 0.6)]
    [InlineData(99.0, 2.5, 0.1)]
    [InlineData(132.0, 5.0, 0.05)]
    [InlineData(165.0, 2.0, 0.15)]
    [InlineData(198.0, 1.2, 0.7)]
    [InlineData(248.0, 1.1, 0.8)]      // half way through the 100 ms frame: interpolated
    [InlineData(298.0, 1.0, 0.9)]
    [InlineData(331.0, 1.0, 1.0)]
    public void TheBlinkFollowsTheEnginesFrameTable(double atMs, double scaleX, double scaleY)
    {
        var b = ProceduralFaceRenderer.Nominal();
        var pose = IdleBehavior.BlinkPose(b, atMs);
        Assert.Equal(scaleX, pose.Left[EyeParam.EyeScaleX], 3);
        Assert.Equal(scaleY, pose.Left[EyeParam.EyeScaleY], 3);
        Assert.Equal(scaleX, pose.Right[EyeParam.EyeScaleX], 3);
        Assert.Equal(scaleY, pose.Right[EyeParam.EyeScaleY], 3);
        Assert.Equal(0f, pose.Left[EyeParam.UpperLidY]);            // the lids are not what blinks
        Assert.Equal(331.0, IdleBehavior.BlinkTotalMs);
    }

    /// <summary>
    /// The blink multiplies onto the base face rather than replacing it (ProceduralFace::Combine at
    /// 0x005846A8 multiplies the two scales), so a base with EyeScaleX 1.2 closes to 1.2 x 5.0.
    /// </summary>
    [Fact]
    public void TheBlinkIsLayeredOntoTheBaseFace()
    {
        var b = ProceduralFacePose.ShippedNeutral();
        var shut = IdleBehavior.BlinkPose(b, 132.0);
        Assert.Equal(b.Left[EyeParam.EyeScaleX] * 5.0f, shut.Left[EyeParam.EyeScaleX], 4);
        Assert.Equal(b.Left[EyeParam.EyeScaleY] * 0.05f, shut.Left[EyeParam.EyeScaleY], 4);
        Assert.Equal(b.Left[EyeParam.EyeCenterX], shut.Left[EyeParam.EyeCenterX]);
    }

    /// <summary>
    /// The eye dart is ProceduralFace::LookAt (0x00584158) called by FaceLayerManager::GenerateEyeShift
    /// (0x0058D100) with xMax = yMax = 5 and the shipped scale tunables. It moves the whole face by (x, y);
    /// only EyeScaleY changes, by a vertical factor from 1.1 (up) to 0.85 (down) and a horizontal
    /// asymmetry of up to 10% favouring the eye on the side looked towards; looking down turns the eyes
    /// inward by up to 2 px. The previous dart shifted EyeCenterX, changed both scales, grew the eye on
    /// the far side, and never moved vertically.
    /// </summary>
    [Fact]
    public void ADartMovesTheWholeFaceAndShapesTheEyesAsLookAtDoes()
    {
        var p = IdleParameters.Default;
        var b = ProceduralFaceRenderer.Nominal();

        var left = IdleBehavior.DartPose(b, -5, 0, p);
        Assert.Equal(-5f, left.FaceCenterX);
        Assert.Equal(0f, left.FaceCenterY);
        Assert.Equal(b.Left[EyeParam.EyeCenterX], left.Left[EyeParam.EyeCenterX]);     // not an eye-centre shift
        float straight = 0.85f + (1.1f - 0.85f) * 0.5f;                                 // 0.975 looking level
        Assert.Equal(straight * 1.1f, left.Left[EyeParam.EyeScaleY], 4);                // the eye looked towards grows
        Assert.Equal(straight * 0.9f, left.Right[EyeParam.EyeScaleY], 4);
        Assert.Equal(1f, left.Left[EyeParam.EyeScaleX]);                                // EyeScaleX untouched

        var up = IdleBehavior.DartPose(b, 0, -5, p);
        Assert.Equal(-5f, up.FaceCenterY);
        Assert.Equal(1.1f, up.Left[EyeParam.EyeScaleY], 4);
        Assert.Equal(1.1f, up.Right[EyeParam.EyeScaleY], 4);
        Assert.Equal(b.Left[EyeParam.EyeCenterX], up.Left[EyeParam.EyeCenterX]);

        var down = IdleBehavior.DartPose(b, 0, 5, p);
        Assert.Equal(0.85f, down.Left[EyeParam.EyeScaleY], 4);
        Assert.Equal(b.Left[EyeParam.EyeCenterX] + 2f, down.Left[EyeParam.EyeCenterX]);  // eyes converge
        Assert.Equal(b.Right[EyeParam.EyeCenterX] - 2f, down.Right[EyeParam.EyeCenterX]);

        var downRightHalf = IdleBehavior.DartPose(b, 2, 3, p);
        float fy = MathF.Min(1f, (5f - 3f) / 10f);
        float vertical = 0.85f + 0.25f * fy;
        Assert.Equal(vertical * (1f - 0.4f * 0.1f), downRightHalf.Left[EyeParam.EyeScaleY], 4);
        Assert.Equal(vertical * (1f + 0.4f * 0.1f), downRightHalf.Right[EyeParam.EyeScaleY], 4);
        Assert.Equal(b.Left[EyeParam.EyeCenterX] + 2f * 0.6f, downRightHalf.Left[EyeParam.EyeCenterX], 5);
    }

    /// <summary>The EyeDartMinScale / EyeDartMaxScale clamp the previous dart applied is not in the engine's LookAt path.</summary>
    [Fact]
    public void ADartIsNotClampedToTheUnusedMinMaxScaleTunables()
    {
        var p = IdleParameters.Default;
        var up = IdleBehavior.DartPose(ProceduralFaceRenderer.Nominal(), -5, -5, p);
        Assert.True(up.Left[EyeParam.EyeScaleY] > (float)p.EyeDartMaxScale,
            $"looking up-left the near eye reaches 1.1 x 1.1 = 1.21, above the 1.08 the old clamp imposed; got {up.Left[EyeParam.EyeScaleY]}");
    }

    // ================================================================ reactions

    /// <summary>
    /// The shipped reactionTrigger_behavior_map.json sends RobotFalling to the behaviour ReactToImpact, and
    /// BehaviorReactToImpact::TransitionToPlayingAnim at 0x00606348 plays AnimationTrigger 0x1A0
    /// (ReactToImpact). The previous table played ReactToFalling on the strength of the name.
    /// </summary>
    [Fact]
    public void FallingReactsWithReactToImpactNotReactToFalling()
    {
        var entry = ReactionTable.Default.For(ReactionTrigger.RobotFalling);
        Assert.NotNull(entry);
        Assert.Equal(AnimationTrigger.ReactToImpact, entry!.Animation);
        Assert.Equal(ReactionEvidence.Shipped, entry.Evidence);
        Assert.All(ReactionTable.Default.Entries, e => Assert.Equal(ReactionEvidence.Shipped, e.Evidence));
    }

    /// <summary>
    /// The ordinals the engine passes are the enum's declaration order: 413 ReactToCliff, 425 ReactToPickup,
    /// 393 PlacedOnCharger, 416 ReactToImpact. If the generated enum ever drifts from the decompiled one,
    /// the immediates read from the binary stop matching and this fails.
    /// </summary>
    [Theory]
    [InlineData(AnimationTrigger.ReactToCliff, 0x19D)]
    [InlineData(AnimationTrigger.ReactToPickup, 0x1A9)]
    [InlineData(AnimationTrigger.PlacedOnCharger, 0x189)]
    [InlineData(AnimationTrigger.ReactToImpact, 0x1A0)]
    [InlineData(AnimationTrigger.NeutralFace, 326)]
    public void TheReactionAnimationTriggersHaveTheOrdinalsTheEnginePasses(AnimationTrigger trigger, int ordinal)
    {
        Assert.Equal(ordinal, (int)trigger);
    }

    /// <summary>
    /// BehaviorReactToImpact::AlwaysHandle (0x00606408) arms the reaction only when FallingStopped's impact
    /// intensity exceeds 1000; a soft landing plays nothing, and the start of the fall never does.
    /// </summary>
    [Fact]
    public void TheImpactReactionFiresOnLandingHarderThanTheThresholdAndNotOnFalling()
    {
        using var rig = new Rig();
        using var reactive = new ReactiveBehavior(rig.Robot, new AnimationTriggerMap(), arbiter: new BehaviorArbiter { AutonomyEnabled = true })
        { Asynchronous = false };
        var decisions = new List<BehaviorDecision>();
        reactive.Reacted += decisions.Add;
        reactive.Start();

        rig.Send(new RobotState { Status = 0 });
        rig.Send(new RobotState { Status = (uint)RobotStatusFlag.IsFalling });
        Assert.Contains(decisions, d => d.Reaction == ReactionTrigger.RobotFalling && d.Outcome == BehaviorOutcome.Unresolved && d.Reason.Contains("waits for the landing"));
        Assert.DoesNotContain(decisions, d => d.Clip is not null || d.Group is not null);

        rig.Send(new FallingStopped { DurationMs = 300, ImpactIntensity = 400f });
        Assert.Contains(decisions, d => d.Reason.Contains("below the engine's threshold"));

        decisions.Clear();
        rig.Send(new FallingStopped { DurationMs = 300, ImpactIntensity = 2500f });
        // No assets are loaded in this rig, so the chain stops at "no animation assets" - but it got there,
        // which is the point: the trigger fired on the hard landing.
        var fired = Assert.Single(decisions, d => d.Reaction == ReactionTrigger.RobotFalling);
        Assert.Equal(AnimationTrigger.ReactToImpact, fired.Animation);
    }

    // ================================================================ the lift reading

    /// <summary>
    /// RobotState.liftAngle is an angle in radians: Robot::UpdateFullRobotState (0x0051291C) stores the
    /// field at RobotState+0x2C into Robot+0x300, and Robot::GetLiftHeight (0x00516F64) turns that field
    /// into millimetres with sinf(angle) * 66 + 45 (Robot::ConvertLiftAngleToLiftHeightMM 0x00516F9C).
    /// The inverse (ConvertLiftHeightToLiftAngleRad 0x005170B0) clamps the height to 32..92 first. The
    /// previous LiftHeightMm returned the raw field, so an angle of 0 rad read as 0 mm instead of 45.
    /// </summary>
    [Fact]
    public void TheLiftAngleIsAnAngleAndConvertsToHeightAsTheEngineDoes()
    {
        Assert.Equal(45f, RobotState.LiftHeightMmFromAngle(0f), 4);
        Assert.Equal(45f, new RobotState { LiftAngle = 0f }.LiftHeightMm, 4);        // was 0 before
        foreach (var mm in new[] { 32f, 45f, 60f, 76f, 92f })
            Assert.Equal(mm, RobotState.LiftHeightMmFromAngle(RobotState.LiftAngleRadFromHeight(mm)), 3);
        Assert.Equal(MathF.Asin(-13f / 66f), RobotState.LiftAngleRadFromHeight(32f), 5);
        Assert.Equal(RobotState.LiftAngleRadFromHeight(32f), RobotState.LiftAngleRadFromHeight(10f), 6);   // raised to 32
        Assert.Equal(MathF.Asin(0.712121f), RobotState.LiftAngleRadFromHeight(120f), 6);                  // the 92 mm constant
        Assert.Equal(RobotState.LiftAngleRadFromHeight(92f), RobotState.LiftAngleRadFromHeight(200f), 6);
    }

    /// <summary>
    /// Corroboration from the wire: the RobotState messages firmware 2457 sent during the committed 20 s
    /// capture carry lift values in the radian range the engine's arm geometry allows, not values in the
    /// 32..92 range a height in millimetres would occupy.
    /// </summary>
    [Fact]
    public void TheCapturedRobotReportsItsLiftInRadiansNotMillimetres()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "hw_fw2457_full.log");
        var line = new System.Text.RegularExpressions.Regex(@"^(\S+) (TX|RX) ((?:[0-9a-f]{2} ?)+)$");
        var states = new List<RobotState>();
        foreach (var l in File.ReadAllLines(path))
        {
            var m = line.Match(l.Trim('﻿', ' '));
            if (!m.Success || m.Groups[2].Value == "TX") continue;
            if (!FrameCodec.TryDecode(Hex.Parse(m.Groups[3].Value), out var f, out _)) continue;
            foreach (var sm in f!.Messages)
                if (sm.Payload.Length > 0 && RobotMessage.Parse(sm.Payload) is RobotState s) states.Add(s);
        }
        Assert.True(states.Count > 100, $"only {states.Count} RobotState in the capture");
        float minAngle = RobotState.LiftAngleRadFromHeight(32f) - 0.1f, maxAngle = RobotState.LiftAngleRadFromHeight(92f) + 0.1f;
        Assert.All(states, s => Assert.InRange(s.LiftAngle, minAngle, maxAngle));
        Assert.All(states, s => Assert.InRange(s.LiftHeightMm, 30f, 94f));
    }

    private sealed class Rig : IDisposable
    {
        public readonly CozmoRobot Robot = CozmoRobot.CreateOffline();
        private ushort _seq = 1;
        public Rig() => Deliver(new SubMessage(ReliableMessageType.ConnectionResponse, Array.Empty<byte>(), _seq++));
        public void Send(RobotMessage m) => Deliver(new SubMessage(ReliableMessageType.SingleReliableMessage, m.ToBytes(), _seq++));
        private void Deliver(SubMessage sm)
        {
            var f = new Frame { Type = ReliableMessageType.MultipleMixedMessages, SeqMin = sm.Seq, SeqMax = sm.Seq, Ack = 0, Messages = new List<SubMessage> { sm } };
            Robot.Transport.ProcessIncoming(FrameCodec.Encode(f));
        }
        public void Dispose() => Robot.Dispose();
    }
}
