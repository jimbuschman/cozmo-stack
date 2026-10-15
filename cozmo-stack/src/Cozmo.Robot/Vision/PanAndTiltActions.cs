using Cozmo.Robot.Manipulation;

namespace Cozmo.Robot.Vision;

/// <summary>
/// The sub-action <c>PanAndTiltAction::Init</c> 0x00549C48 builds for the body (M13-020): a
/// <c>TurnInPlaceAction(robot, angle, isAbsolute)</c> (0xE8 bytes) and the fields Init writes on it. The
/// <c>TurnInPlaceAction</c> body is <see cref="TurnInPlaceAction"/> (M13-022); this holds what Init sets, and nothing connects it to that class yet.
/// Field names are not in the binary; they are named by the offsets Init writes.
/// </summary>
public sealed record TurnInPlaceSpec(double AngleRad, bool IsAbsolute, double ToleranceRad, bool Byte0xD8,
                                     bool MaxSpeedSet, double MaxSpeedRadPerSec, double AccelRadPerSec2, bool Byte0xCC);

/// <summary>
/// The sub-action for the head (M13-020): <c>MoveHeadToAngleAction(robot, angle, tolerance, Radians(0))</c>
/// (0xB8 bytes), then <c>[+0x9D] = 0</c>, <c>[+0x9C] = [PanAndTiltAction+0x126]</c> and, when
/// <c>[PanAndTiltAction+0x161]</c>, the eight bytes at <c>[+0x158..0x15F]</c> copied to <c>[+0x90..0x97]</c>. The body is
/// the live <c>CozmoMotion</c> head action (Motion.cs, M4-001 / M4-016); this stack has no second copy of it.
/// </summary>
public sealed record MoveHeadToAngleSpec(double AngleRad, double ToleranceRad, double VarianceRad, bool Byte0x9C, bool Byte0x9D,
                                         bool Bytes0x90Set, uint Word0x90, uint Word0x94);

/// <summary>
/// The engine's <c>PanAndTiltAction</c> (M13-020): a compound action holding a <c>TurnInPlaceAction</c> and a
/// <c>MoveHeadToAngleAction</c>.
///
/// <c>Init</c> 0x00549C48 clears the compound (this+0x78), copies <c>[+0x57]</c> to <c>[+0xCF]</c>, adds the body turn
/// with <c>[+0x114]</c> and <c>[+0x124]</c>, its tolerance from <c>[+0x140]</c>, <c>[tia+0xD8] = [+0x126]</c> and, when
/// <c>[+0x160]</c>, the max speed <c>[+0x148]</c> and the acceleration <c>[+0x14C]</c> (the turn's own default
/// <c>[tia+0x7C]</c> when that is 0, else <c>[tia+0xCC] = 1</c> and <c>[tia+0xC8] = [+0x14C]</c>); adds the head move whose
/// angle is <c>Radians([+0x11C])</c> when <c>[+0x125]</c> else <c>robot[+0x2FC] + [+0x11C]</c>, tolerance
/// <c>[+0x150]</c>; sets <c>[+0xCE] = 1</c>; and returns the compound's <c>Update()</c> with SUCCESS and RUNNING mapped
/// to 0 (0x00549D52 <c>orr r1,r0,#0x1000000</c>, 0x00549D56 <c>cmp.w r1,#0x1000000</c>). <c>CheckIfDone</c> 0x00549D73
/// returns the compound's <c>IActionRunner::Update()</c> unchanged.
///
/// What is NOT built: the compound (<c>CompoundActionParallel</c>, the M8 framework) and the wiring of the two child actions into it.
/// <see cref="TurnInPlaceAction"/> (M13-022) exists as a class and the head move is the live <c>CozmoMotion</c> action (M4-016), but <see cref="Children"/> still
/// holds the specs Init fills (how Init applies its tolerance, through SetTolerance's 2-degree minimum or by a store, is not in the
/// inventory). <see cref="CompoundUpdate"/> is the compound's <c>IActionRunner::Update()</c>; it has no default, and calling
/// <see cref="Init"/> or <see cref="CheckIfDone"/> without one throws <see cref="NotSupportedException"/>. THIS CLASS, TurnTowardsPoseCompound,
/// WaitForImagesAction and TurnInPlaceAction ARE NOT LIVE: nothing outside the tests constructs them, except
/// <see cref="TurnTowardsPoseCompound.InitPose"/>, which the face turn calls for its pan/head computation. The stack's own pan-and-tilt for
/// the live paths is <see cref="Cozmo.Robot.Behavior.PanAndTilt"/>, which is not this class and is not claimed to be.
/// </summary>
// fidelity: M13-020
public class PanAndTiltAction
{
    /// <summary>IActionRunner::Update result SUCCESS.</summary>
    public const uint Success = 0;
    /// <summary>IActionRunner::Update result RUNNING (0x01000000).</summary>
    public const uint Running = 0x01000000;

    // The fields Init reads, named by offset, with the constructor's defaults (0x0054962C..0x00549700, M13-020): +0x126 = 1;
    // +0x140 and +0x150 = Radians(5 deg) (0x3DB2B8C2); +0x148 = [+0x12C] = 5.235988 (0x40A78D36); +0x14C = [+0x130] = 10.0;
    // +0x158 = [+0x138] = 15.0 and +0x15C = [+0x13C] = 20.0; +0x160 = +0x161 = 0. The setters SetMaxPanSpeed 0x005498A0,
    // SetPanAccel 0x005499AC, SetMaxTiltSpeed 0x00549AE8 and SetTiltAccel 0x00549B0C are not built (the limit SetMaxPanSpeed warns
    // above is not in the inventory); a caller sets the fields and the +0x160/+0x161 flags itself.
    public static readonly float DefaultToleranceRad = BitConverter.Int32BitsToSingle(0x3DB2B8C2);    // 0x3DB2B8C2
    public double PanAngleRad { get; set; }                   // +0x114
    public double HeadAngleRad { get; set; }                  // +0x11C
    public bool PanIsAbsolute { get; init; }                  // +0x124
    public bool HeadIsAbsolute { get; init; }                 // +0x125
    public bool Byte0x126 { get; init; } = true;              // +0x126, copied to tia+0xD8 and mh+0x9C; the constructor's only store (0x005496B2)
    public double PanToleranceRad { get; init; } = DefaultToleranceRad;   // +0x140
    public double MaxSpeedRadPerSec { get; init; } = BitConverter.Int32BitsToSingle(0x40A78D36);          // +0x148
    public double MaxAccelRadPerSec2 { get; init; } = 10.0;               // +0x14C
    public double HeadToleranceRad { get; init; } = DefaultToleranceRad;  // +0x150
    public uint HeadSpeedAccelWord0 { get; init; } = 0x41700000;          // +0x158..0x15B (15.0f)
    public uint HeadSpeedAccelWord1 { get; init; } = 0x41A00000;          // +0x15C..0x15F (20.0f)
    public bool HasMaxSpeed { get; init; }                    // +0x160
    public bool HasHeadSpeedAccel { get; init; }              // +0x161
    public byte Byte0x57 { get; init; }                       // +0x57, copied to +0xCF

    /// <summary>The compound's <c>IActionRunner::Update()</c> (PLT 0x004AA984). No default: the compound (CompoundActionParallel) is the M8 framework and is not built.</summary>
    public Func<IReadOnlyList<object>, uint>? CompoundUpdate { get; set; }

    /// <summary>The children Init added, in order: the <see cref="TurnInPlaceSpec"/> then the <see cref="MoveHeadToAngleSpec"/>.</summary>
    public IReadOnlyList<object> Children => _children;
    private readonly List<object> _children = new();

    /// <summary>[+0xCF] after Init (a copy of [+0x57]).</summary>
    public byte Byte0xCF { get; private set; }
    /// <summary>[+0xCE] after Init.</summary>
    public bool Byte0xCE { get; private set; }

    /// <param name="robotHeadAngleRad"><c>robot[+0x2FC]</c>, read only when the head angle is relative.</param>
    /// <param name="turnInPlaceDefaultAccelRadPerSec2"><c>[tia+0x7C]</c>, the TurnInPlaceAction constructor's acceleration (10.0, 0x41200000; M11-015).</param>
    public uint Init(double robotHeadAngleRad, double turnInPlaceDefaultAccelRadPerSec2 = TurnTowardsPose.AccelRadPerSec2)
    {
        var update = CompoundUpdate ?? throw new NotSupportedException("M13-020: the compound (CompoundActionParallel, M8 framework) is not built");
        _children.Clear();                                              // ClearActions(this+0x78), 0x00549C54
        Byte0xCF = Byte0x57;                                            // 0x00549C58..0x00549C5C

        double accel = turnInPlaceDefaultAccelRadPerSec2;
        bool byte0xCC = false;
        if (HasMaxSpeed && MaxAccelRadPerSec2 != 0) { byte0xCC = true; accel = MaxAccelRadPerSec2; }
        _children.Add(new TurnInPlaceSpec(PanAngleRad, PanIsAbsolute, PanToleranceRad, Byte0x126,
                                          HasMaxSpeed, HasMaxSpeed ? MaxSpeedRadPerSec : 0, accel, byte0xCC));   // 0x00549C60..0x00549CC4

        // 0x00549CD0..0x00549CEE: Radians([+0x11C]) when absolute, else Anki::operator+(float, Radians const&) (PLT 0x004A81A0, called at 0x00549CEE)
        // of robot[+0x2FC] and [+0x11C], which yields a normalised Radians (Radians::rescale 0x0084C87C, M12-033).
        double head = HeadIsAbsolute ? HeadAngleRad : EngineRadians.Rescale((float)robotHeadAngleRad + (float)HeadAngleRad);
        _children.Add(new MoveHeadToAngleSpec(head, HeadToleranceRad, 0.0, Byte0x126, false,
                                              HasHeadSpeedAccel, HasHeadSpeedAccel ? HeadSpeedAccelWord0 : 0, HasHeadSpeedAccel ? HeadSpeedAccelWord1 : 0)); // 0x00549CF2..0x00549D2C
        Byte0xCE = true;

        uint r = update(_children);                                     // 0x00549D4C
        return (r | 0x01000000u) == 0x01000000u ? 0u : r;               // 0x00549D52 / 0x00549D56
    }

    /// <summary><c>PanAndTiltAction::CheckIfDone</c> 0x00549D73: the compound's <c>Update()</c>, unchanged.</summary>
    public virtual uint CheckIfDone() =>
        (CompoundUpdate ?? throw new NotSupportedException("M13-020: the compound (CompoundActionParallel, M8 framework) is not built"))(_children);
}

/// <summary>
/// What <c>TurnTowardsPoseAction::Init</c> reads from the robot and the two unread lookups it calls (M13-020, M13-021).
/// <see cref="ComputeHeadAngleToSeePose"/> is <c>Robot::ComputeHeadAngleToSeePose(pose, &amp;angle, 0.01f)</c> (0x3C23D70A), null = its
/// failure result; <see cref="GetAbsoluteHeadAngleToLookAtPose"/> is 0x0054B428. Both bodies are unread (M13-021), so a null
/// seam throws <see cref="NotSupportedException"/> and no default is supplied here.
/// </summary>
public sealed record TurnTowardsPoseEnv(Pose3d RobotPose, double RobotHeadAngleRad,
                                        Func<Pose3d, double?>? ComputeHeadAngleToSeePose,
                                        Func<Vec3, double>? GetAbsoluteHeadAngleToLookAtPose,
                                        Func<Pose3d, Pose3d?>? WithRespectToRobot = null,
                                        Action<string>? Log = null,
                                        SourceLogSink? SourceLog = null,
                                        Pose3d? WorldOrigin = null,
                                        Action? ForceFlush = null);

/// <summary>
/// The engine's <c>TurnTowardsPoseAction</c> as a <see cref="PanAndTiltAction"/> (M13-020). Constructors 0x00549F10 (max turn) and 0x0054B344
/// (pose, max turn): the pan is relative (+0x124 = 0), the tilt absolute (+0x125 = 1), <c>+0x170 = abs(maxTurn)</c>, <c>+0x178</c> = pose set,
/// <c>+0x179 = 0</c>. <see cref="InitPose"/> is <c>TurnTowardsPoseAction::Init</c> 0x0054A8FC up to its call of
/// <c>PanAndTiltAction::Init</c>: <c>+0x179 = 0</c>, <c>+0x114 = 0</c>; no pose set -> BAD_POSE 0x03000005; a parentless pose is
/// re-parented to the world origin (no conversion), a parented one is taken with respect to the robot's pose, failing -> BAD_POSE; when
/// <c>+0x170 &gt; 0</c> the pan is <c>atan2f(y, x)</c> and, if <c>|pan| &lt;= +0x170</c>, becomes <c>+0x114</c>, otherwise <c>+0x179 = 1</c> and Init
/// returns 0 without building the compound (no body turn and no head move); the head angle is
/// <c>ComputeHeadAngleToSeePose(pose, 0.01)</c> (failure: <c>GetAbsoluteHeadAngleToLookAtPose</c>) clamped to [-0.4363323, 0.7766715] into
/// <c>+0x11C</c>. <c>CheckIfDone</c> 0x0054B011 returns 0 when <c>+0x179</c> is set, else the compound's <c>Update()</c>.
///
/// This stack has one pose origin and <see cref="Pose3d"/> has no parent, so "parented" is the <see cref="PoseHasParent"/> flag (true), and the
/// world-to-robot conversion cannot fail unless <see cref="TurnTowardsPoseEnv.WithRespectToRobot"/> says so.
/// </summary>
// fidelity: M13-020
public sealed class TurnTowardsPoseCompound : PanAndTiltAction
{
    /// <summary>BAD_POSE, the ActionResult Init returns for an unset or unreachable pose.</summary>
    public const uint BadPose = 0x03000005;
    public static readonly float HeadMinRad = BitConverter.Int32BitsToSingle(unchecked((int)0xBEDF66F3));          // 0xBEDF66F3
    public static readonly float HeadMaxRad = BitConverter.Int32BitsToSingle(0x3F46D3F2);           // 0x3F46D3F2
    public const float SeePoseToleranceRad = 0.01f;       // 0x3C23D70A

    public TurnTowardsPoseCompound(double maxTurnRad)
    {
        PanIsAbsolute = false; HeadIsAbsolute = true;     // the constructors' fixed arguments to PanAndTiltAction: pan relative, tilt absolute
        MaxTurnAbsRad = MathF.Abs((float)maxTurnRad);     // +0x170 = abs(maxTurn)
    }

    public TurnTowardsPoseCompound(Pose3d pose, double maxTurnRad) : this(maxTurnRad) { Pose = new Pose3d(pose); PoseSet = true; }

    /// <summary>+0x164: the pose to look at, in this stack's world frame.</summary>
    public Pose3d? Pose { get; private set; }
    /// <summary>+0x178.</summary>
    public bool PoseSet { get; private set; }
    /// <summary>+0x170.</summary>
    public double MaxTurnAbsRad { get; }
    /// <summary>Native HasParent, with the retained M11-066 flat-input adapter and an explicit host test override.</summary>
    private bool? _hasParentOverride;
    // Flat host inputs retain the labelled M11-066 adapter; native tree inputs use the checked HasParent branch.
    public bool PoseHasParent { get => _hasParentOverride ?? (Pose is {} pose && (pose.HasLegacyFrameInput || pose.HasParent)); set => _hasParentOverride=value; }
    /// <summary>The byte at +0x179.</summary>
    public bool Byte0x179 { get; private set; }

    /// <summary>
    /// <c>TurnTowardsPoseAction::GetAbsoluteHeadAngleToLookAtPose(Point3f)</c> 0x0054B428..0x0054B564 (M13-021), binary32 in the engine's operation order, for a point relative to the robot: <c>h = z + (-49.0f)</c>,
    /// <c>D = sqrtf(x*x + y*y) + 13.0f</c>, <c>t = (300.0f - D) / 150.0f</c>, <c>u = (0.0f - h) / -10.0f</c>, <c>a = atan2f(h, D)</c>; then the conditional picks of the <c>it pl / it gt</c> pairs at
    /// 0x0054B4C4..0x0054B54A (each condition is the latest <c>vcmpe</c> before its <c>vmrs</c>: <c>pl</c> is "not less than", NaN included; <c>gt</c> is ordered greater) between 0.0f, 0.0872665f (0x3DB2B8C2),
    /// 0.1308997f (0x3E060A92), <c>t*0.0872665f</c> and <c>u*0.1308997f</c>, and the result <c>(s24 + (a + s16)) + 0.0698132f (0x3D8EFA35)</c> as <c>Radians(float)</c>.
    /// </summary>
    // fidelity: M13-021
    public static double AbsoluteHeadAngleToLookAtPose(Vec3 p)
    {
        float x = (float)p.X, y = (float)p.Y, z = (float)p.Z;
        float L1 = -49.0f, L2 = 150.0f, L4 = 300.0f;
        float L5 = BitConverter.Int32BitsToSingle(0x3DB2B8C2), L6 = BitConverter.Int32BitsToSingle(0x3E060A92), L7 = BitConverter.Int32BitsToSingle(0x3D8EFA35);
        float s16 = z + L1;
        float sum = x * x;
        sum = sum + y * y;
        float d = MathF.Sqrt(sum);
        float s8 = 0.0f - s16;
        float dd = d + 13.0f;
        float u = s8 / -10.0f;
        float t = (L4 - dd) / L2;
        float s6 = 0.0f, s0 = 0.0f, s8b = 0.0f, s18 = 0.0f;
        bool Pl(float a, float b) => !(a < b);
        if (Pl(t, 1.0f)) s6 = L5;
        float s4 = t * L5;
        if (t > 0f) s0 = s6;
        float s6b = L6;
        float s16b = s0;
        if (Pl(u, 1.0f)) s8b = s6b;
        if (u > 0f) s18 = s8b;
        float s24 = s18;
        if (t > 0f) s16b = s4;
        float u6 = u * s6b;
        if (u > 0f) s24 = u6;
        if (Pl(t, 1.0f)) s16b = s0;
        float a = MathF.Atan2(s16, dd);
        float r = a + s16b;
        if (Pl(u, 1.0f)) s24 = s18;
        r = s24 + r;
        r = r + L7;
        return Cozmo.Robot.Manipulation.EngineRadians32.Rescale(r);
    }

    /// <summary><c>SetPose</c> 0x0054A8E8: +0x164 = pose, +0x178 = 1.</summary>
    public void SetPose(Pose3d pose) { if(Pose is null)Pose=new Pose3d(pose);else Pose.AssignInputValue(pose);PoseSet=true; }

    /// <summary><c>TurnTowardsPoseAction::Init</c> 0x0054A8FC as far as <c>PanAndTiltAction::Init</c>; <paramref name="callPanAndTiltInit"/> is false when it returned before it.</summary>
    public uint InitPose(TurnTowardsPoseEnv env, out bool callPanAndTiltInit)
    {
        using var sourceScope=env.SourceLog is {} sink ? Pose3d.UseSourceLog(sink,env.ForceFlush) : null;
        void Emit(EngineLogLevel level,string? channel,string key,string text) => SourceLogDispatch.GetSink(env.SourceLog,env.Log)?.Invoke(level,channel,key,text);
        callPanAndTiltInit = false;
        Byte0x179 = false;                                                    // (1)
        PanAngleRad = 0;
        if (!PoseSet || Pose is not { } pose)
        {
            Emit(EngineLogLevel.Error,null,"TurnTowardsPoseAction.Init.PoseNotSet","");
            Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak();return BadPose;
        }   // (2) 0x0054A9B8
        Vec3 rel;
        Pose3d inRobot = pose;
        if (!PoseHasParent)
        {
            Emit(EngineLogLevel.Info,"Actions","TurnTowardsPoseAction.Init.AssumingRobotOriginAsParent","");
            if(env.WorldOrigin is {} origin)pose.SetParent(origin);
            else Cozmo.Robot.Behavior.SteppedBehavior.ReportMissing("M13-020 parentless Init 0x0054AA14..0x0054AA58: host environment has no native world-origin owner; reparent unit skipped rather than inventing a parent");
            rel=pose.Translation;
        }
        else
        {
            var r = env.WithRespectToRobot is { } f ? f(pose) : pose.WithRespectTo(env.RobotPose);
            if (r is not { } wrt)
            {
                using var targetRoot=pose.FindRoot();using var robotRoot=env.RobotPose.FindRoot();
                Emit(EngineLogLevel.Warning,null,"TurnTowardsPoseAction.Init.PoseOriginFailure",FormattableString.Invariant($"Could not get pose (in frame {unchecked((int)targetRoot.Id)}) w.r.t. robot pose (in frame {unchecked((int)robotRoot.Id)})."));
                return BadPose;
            }   // 0x0054A952
            rel = wrt.Translation;
            pose.AssignInputValue(wrt);inRobot=pose;
        }
        if (MaxTurnAbsRad > 0)                                                // (4) 0x0054AC8A
        {
            float pan = MathF.Atan2((float)rel.Y, (float)rel.X);
            if (MathF.Abs(pan) <= (float)MaxTurnAbsRad) PanAngleRad = pan;
            else { env.Log?.Invoke($"TurnTowardsPoseAction.Init: turn angle {pan * 180 / Math.PI:F1} deg exceeds the maximum {MaxTurnAbsRad * 180 / Math.PI:F1} deg"); Byte0x179 = true; return 0; }
        }
        // (5) 0x0054ABA6..0x0054ACBC
        double? head = (env.ComputeHeadAngleToSeePose ?? throw new NotSupportedException("M13-021: Robot::ComputeHeadAngleToSeePose is unread")).Invoke(inRobot);
        if (head is null)
        {
            env.Log?.Invoke(FormattableString.Invariant($"TurnTowardsPoseAction.Init.FailedToComputedHeadAngle: PoseWrtRobot translation=({rel.X:F6},{rel.Y:F6},{rel.Z:F6})"));       // 0x0054AB66
            head = (env.GetAbsoluteHeadAngleToLookAtPose ?? AbsoluteHeadAngleToLookAtPose).Invoke(rel);       // 0x0054AB8C..0x0054AB9A: the pose was converted in place to the robot frame (0x0054A94C), so the argument is robot-relative
        }
        HeadAngleRad = Math.Min(Math.Max((float)head.Value, HeadMinRad), HeadMaxRad);
        callPanAndTiltInit = true;
        return 0;
    }

    /// <summary>The whole <c>TurnTowardsPoseAction::Init</c>: <see cref="InitPose"/> then <see cref="PanAndTiltAction.Init"/> (6).</summary>
    public uint Init(TurnTowardsPoseEnv env)
    {
        uint r = InitPose(env, out bool go);
        return go ? Init(env.RobotHeadAngleRad) : r;
    }

    public override uint CheckIfDone() => Byte0x179 ? Success : base.CheckIfDone();
}

/// <summary>Result codes the pan-and-tilt actions return (ActionResult.cs values, M13-020/M13-022).</summary>
public static class PanTiltResult
{
    public const uint Success = 0;
    public const uint Running = 0x01000000;
    public const uint Abort = 0x03000000;
    public const uint InvalidOffTreadsState = 0x0300000A;
    public const uint SendMessageToRobotFailed = 0x03000016;
    public const uint MotorStoppedMakingProgress = 0x04000004;
}

/// <summary>
/// What <see cref="TurnInPlaceAction"/> and the live head action read from the robot and send through the
/// <c>MovementComponent</c> (M13-022). <c>MovementComponent::TurnInPlace</c> and <c>MoveHeadToAngle</c> are unread (M13-021), so no
/// implementation exists in this stack: the two methods are the visible stubs, and a test or a later record supplies them.
/// </summary>
internal interface INativeMotorAckSource
{
    IDisposable SubscribeMotorAck(Action<byte> callback);
}

// Native live inputs and layer calls; absent services leave a visible MISSING instead of invented arguments.
internal interface INativePanTiltServices
{
    object StateGate { get; }
    SourceLogSink SourceLog { get; }
    string OffTreadsName { get; }
    void RemoveKeepFaceAlive(int duration);
    byte AddEyeShift(byte tag, float x);
    void RemoveEyeShift(byte tag, int duration);
}

public interface IPanTiltRobot
{
    /// <summary>[robot+0x355] == 0 (OnTreads), what <c>IsOffTreadsStateValid</c> 0x00545EC4 tests.</summary>
    bool OnTreads { get; }
    /// <summary>[robot+0x2B0], the pose-origin counter.</summary>
    uint OriginId { get; }
    /// <summary><c>Robot::GetPose().rotation.GetAngleAroundZaxis()</c>.</summary>
    float PoseAngleAroundZ { get; }
    /// <summary>[robot+0x2FC], the head angle.</summary>
    float HeadAngle { get; }
    /// <summary>[movementComponent+0xC].</summary>
    bool BodyMoving { get; }
    /// <summary>[movementComponent+0xA].</summary>
    bool HeadMoving { get; }
    /// <summary><c>MovementComponent::TurnInPlace</c>; 0 = success. Checked component wrapper (M13-021).</summary>
    uint TurnInPlace(float targetRad, float speed, float accel, float toleranceRad, ushort numHalfRevolutions, bool isAbsolute, out byte actionId);
    /// <summary><c>MovementComponent::MoveHeadToAngle</c>; 0 = success. Checked component wrapper (M13-021).</summary>
    uint MoveHeadToAngle(float angleRad, float speed, float accel, float durationSec, out byte actionId);
}

/// <summary>
/// The engine's <c>TurnInPlaceAction</c> (M13-022): constructor 0x005459D4, <c>SetMaxSpeed</c> 0x00545C08, <c>SetAccel</c> 0x00545D14,
/// <c>SetTolerance</c> 0x00545D50, <c>IsOffTreadsStateValid</c> 0x00545EC4, <c>Init</c> 0x00545FA0, <c>IsBodyInPosition</c> 0x00546374,
/// <c>CheckIfDone</c> 0x0054642C, the motorActionAck handler 0x0054D3F8. Field names are not in the binary; they are named by offset.
/// Angles are float32 and normalised with <see cref="EngineRadians.Rescale"/> (<c>Radians</c>).
///
/// OV07 builds the direct movement wrappers and bounded turn Init/completion rows. The adopted 20261015 turn-eye destructor
/// row adds persistent-layer cleanup before C4 retirement and the existing base-tag teardown (0x00545B94..0x00545BD4).
/// GetCompletionUnion, nonzero-variability RNG and the full IActionRunner lifecycle remain separately unbuilt.
/// </summary>
// fidelity: M13-022
public sealed class TurnInPlaceAction : IDisposable
{
    public const int ActionType = 0x28;
    public const int TracksToLock = 4;
    public static readonly float DefaultMaxSpeed = BitConverter.Int32BitsToSingle(0x40A78D36);     // +0x78, 0x40A78D36
    public const float DefaultAccel = 10.0f;             // +0x7C, 0x41200000
    public const float MaxRevolutions = 25.0f;           // +0x80, 0x41C80000
    public static readonly float MinToleranceRad = BitConverter.Int32BitsToSingle(0x3D0EFA35);   // 0x3D0EFA35 (2 degrees)
    public static readonly float TwoPi = BitConverter.Int32BitsToSingle(0x40C90FDB);               // 0x40C90FDB
    public const int MotorActionAckTag = 0xC4;

    private readonly IPanTiltRobot _robot;
    private readonly object _stateGate;
    internal static readonly object DiagnosticCounterGate = new();
    internal static ushort WaitingAckCounter = 10, AngleNotReachedCounter = 10;
    public uint Tag { get; }
    private bool _disposed;
    private T Gated<T>(Func<T> body) { T result=default!; LogCallbackDeferral.RunGated(_stateGate,()=>result=body()); return result; }
    private void Gated(Action body) => LogCallbackDeferral.RunGated(_stateGate,body);
    private void Emit(EngineLogLevel level, string? channel, string key, string text)
    {
        if (_robot is INativePanTiltServices native) native.SourceLog(level,channel,key,text);
        else LogCallbackDeferral.Run(() => SourceLogDispatch.GetSink(null,Log)?.Invoke(level,channel,key,text));
    }
    private static string Fixed(float v,int decimals=1,bool signed=false)
    {
        if(float.IsNaN(v))return (BitConverter.SingleToInt32Bits(v)<0?"-":signed?"+":"")+"nan";
        if(float.IsPositiveInfinity(v))return signed?"+inf":"inf";
        if(float.IsNegativeInfinity(v))return "-inf";
        string text=((double)v).ToString("F"+decimals,System.Globalization.CultureInfo.InvariantCulture);
        return signed && (BitConverter.SingleToUInt32Bits(v)&0x80000000)==0 ? "+"+text : text;
    }
    private static float Degrees(float v) => v*BitConverter.Int32BitsToSingle(0x42652EE1);
    private string PositionText(string prefix) => FormattableString.Invariant($"[{unchecked((int)Tag)}] {prefix}{Fixed(Degrees(Angle0x8C))}deg vs. {Fixed(Degrees(Angle0x9C))}deg(+/-{Fixed(Degrees(Variability0xB8))}), angDistTravd={Fixed(Degrees(Float0xA8),signed:true)}deg, angDistExpc={Fixed(Degrees(Float0xA4),signed:true)}deg (tol: {Fixed(Degrees(Tolerance0xB0),6)}) (pfid: {unchecked((int)_robot.OriginId)})");
    private readonly IDisposable? _ackSubscription;
    public event Action<string>? Log;
    private readonly List<string> _eyeShiftRequests = new();
    public IReadOnlyList<string> EyeShiftRequests => _eyeShiftRequests;
    /// <summary>The draw for the +0xB8 variability; null unless the caller supplies the engine's RNG (unread).</summary>
    private Func<float,float>? _randomInRange;
    public Func<float,float>? RandomInRange { get { lock(_stateGate)return _randomInRange; } set { lock(_stateGate)_randomInRange=value; } }

    public void Dispose() => Gated(() =>
    {
        if (_disposed) return;
        _disposed = true;
        // M13-022: D1 0x00545B9A..0x00545BC0. Both guards precede the call; zero the tag after removal.
        if (Byte0xD8 && Byte0xD9 != 0)
        {
            if (_robot is INativePanTiltServices native) native.RemoveEyeShift(Byte0xD9, 0);
            Byte0xD9 = 0;
        }
        _ackSubscription?.Dispose();                         // +E4 release, 0x00545BC4..0x00545BCA
        ActionRunnerTagCounter.Global.Release(Tag);           // existing base-tag teardown, after the C4 owner
    });

    public TurnInPlaceAction(IPanTiltRobot robot, float angle, bool isAbsolute)
    {
        _robot = robot;
        _stateGate=(robot as INativePanTiltServices)?.StateGate ?? new object();
        uint tag; do { tag=ActionRunnerTagCounter.Global.NextIdTag(); } while (!ActionRunnerTagCounter.Global.TryReserve(tag));
        Tag=tag;
        _ackSubscription = (robot as INativeMotorAckSource)?.SubscribeMotorAck(HandleMotorActionAck);
        Byte0x84 = false; Byte0x85 = false;
        Angle0x88 = angle;
        Angle0x8C = 0; Angle0x94 = 0; Angle0x9C = 0;
        Float0xA4 = 0; Float0xA8 = 0; Float0xAC = 0;
        Tolerance0xB0 = MinToleranceRad;
        Variability0xB8 = 0;
        IsAbsolute = isAbsolute;
        Speed0xC4 = DefaultMaxSpeed; Accel0xC8 = DefaultAccel; Byte0xCC = false;
        Origin0xD0 = 0; OriginChanges0xD4 = 0;
        Byte0xD8 = true;                                  // the only store here; PanAndTiltAction::Init overwrites it with [+0x126]
        Byte0xD9 = 0;
    }

    public bool Byte0x84 { get; private set; }            // +0x84 body in position
    public bool Byte0x85 { get; private set; }            // +0x85 body has moved
    public float Angle0x88 { get; private set; }          // +0x88 requested angle
    public float Angle0x8C { get; private set; }          // +0x8C current angle
    public float Angle0x94 { get; private set; }
    public float Angle0x9C { get; private set; }          // +0x9C the target passed to TurnInPlace
    public float Float0xA4 { get; private set; }
    public float Float0xA8 { get; private set; }
    public float Float0xAC { get; private set; }
    public float Tolerance0xB0 { get; private set; }
    private float _variability;
    public float Variability0xB8 { get { lock(_stateGate)return _variability; } set { lock(_stateGate)_variability=value; } }
    public bool IsAbsolute { get; }                       // +0xC0
    public float Speed0xC4 { get; private set; }
    public float Accel0xC8 { get; private set; }
    public bool Byte0xCC { get; private set; }
    public uint Origin0xD0 { get; private set; }
    public uint OriginChanges0xD4 { get; private set; }
    public byte ActionId0xDA { get; private set; }
    public bool Byte0xDB { get; private set; }           // a TurnInPlace was sent
    public bool Byte0xDC { get; private set; }           // its ack arrived
    /// <summary>+0xD8, copied from PanAndTiltAction's +0x126 (the constructor sets 1).</summary>
    private bool _useEyes;
    public bool Byte0xD8 { get { lock(_stateGate)return _useEyes; } set { lock(_stateGate)_useEyes=value; } }
    public byte Byte0xD9 { get; private set; }


    /// <summary><c>SetMaxSpeed</c> 0x00545C08: |speed| above 5.235988 warns, sets +0xCC and stores the limit with the caller's sign; 0 restores +0x78 and leaves +0xCC; else +0xCC = 1, +0xC4 = speed.</summary>
    public void SetMaxSpeed(float speed) => Gated(() => SetMaxSpeedUnderGate(speed));
    private void SetMaxSpeedUnderGate(float speed)
    {
        if (MathF.Abs(speed) > DefaultMaxSpeed) { Log?.Invoke("TurnInPlaceAction.SetMaxSpeed: speed above the limit"); Byte0xCC = true; Speed0xC4 = MathF.CopySign(DefaultMaxSpeed, speed); }
        else if (speed == 0) Speed0xC4 = DefaultMaxSpeed;
        else { Byte0xCC = true; Speed0xC4 = speed; }
    }

    /// <summary><c>SetAccel</c> 0x00545D14: 0 -> +0xC8 = [+0x7C]; else +0xCC = 1, +0xC8 = accel.</summary>
    public void SetAccel(float accel) => Gated(() => SetAccelUnderGate(accel));
    private void SetAccelUnderGate(float accel)
    {
        if (accel == 0) Accel0xC8 = DefaultAccel;
        else { Byte0xCC = true; Accel0xC8 = accel; }
    }

    /// <summary><c>SetTolerance</c> 0x00545D50: +0xB0 = |tol|; below 2 degrees it is raised to 2 degrees (info "UseDefault" when |tol| &lt; 1e-5, else a warning).</summary>
    public void SetTolerance(float tolerance) => Gated(() => SetToleranceUnderGate(tolerance));
    private void SetToleranceUnderGate(float tolerance)
    {
        Tolerance0xB0 = MathF.Abs(tolerance);
        if (Tolerance0xB0 >= MinToleranceRad) return;
        Log?.Invoke(MathF.Abs(tolerance) < 1e-5f ? "TurnInPlaceAction.SetTolerance.UseDefault" : "TurnInPlaceAction.SetTolerance: tolerance below the minimum");
        Tolerance0xB0 = MinToleranceRad;
    }

    /// <summary><c>IsOffTreadsStateValid</c> 0x00545EC4: valid iff [robot+0x355] == 0.</summary>
    public bool IsOffTreadsStateValid() => Gated(IsOffTreadsStateValidUnderGate);
    private bool IsOffTreadsStateValidUnderGate()
    {
        if (_robot.OnTreads) return true;
        if (_robot is INativePanTiltServices native)
            Emit(EngineLogLevel.Warning,null,"TurnInPlaceAction.OffTreadsStateInvalid",FormattableString.Invariant($"[{unchecked((int)Tag)}] Off tread state {native.OffTreadsName} is invalid for TurnInPlace"));
        else Cozmo.Robot.Behavior.SteppedBehavior.ReportMissing("M13-022 OffTreadsStateInvalid 0x00545EEA: host IPanTiltRobot supplies only OnTreads, not the native signed enum argument; diagnostic not invented");
        return false;
    }

    /// <summary><c>Init</c> 0x00545FA0.</summary>
    public uint Init() => Gated(InitUnderGate);
    private uint InitUnderGate()
    {
        if (!IsOffTreadsStateValidUnderGate()) return PanTiltResult.InvalidOffTreadsState;                                    // (1)
        Origin0xD0 = _robot.OriginId; OriginChanges0xD4 = 0;                                                         // (2)
        Angle0x8C = _robot.PoseAngleAroundZ;                                                                         // (3)
        float s16 = 0;                                                                                               // (4)
        if (Variability0xB8 != 0)
            s16 = (RandomInRange ?? throw new NotSupportedException("IAction::GetRNG is unread: a non-zero variability needs RandomInRange"))(Variability0xB8);
        if (IsAbsolute)                                                                                              // (5)
        {
            Angle0x9C = Radians.Rescale(Angle0x88 + s16);
            Float0xA4 = Radians.Rescale(Angle0x9C - Angle0x8C);
        }
        else
        {
            if (MathF.Abs(Angle0x88) > MaxRevolutions * TwoPi) { Log?.Invoke("TurnInPlaceAction.Init.AngleTooLarge"); return PanTiltResult.Abort; }   // 0x03000000
            Angle0x88 -= Float0xA8;
            Angle0x9C = Radians.Rescale(Angle0x8C + Angle0x88 + s16);
            Float0xA4 = s16 + Angle0x88;
            uint bits = (BitConverter.SingleToUInt32Bits(Speed0xC4) & 0x7FFFFFFFu) | (BitConverter.SingleToUInt32Bits(Angle0x88) & 0x80000000u);   // bfi r1, r0, #0x1f, #1 (0x0054610C)
            Speed0xC4 = BitConverter.UInt32BitsToSingle(bits);
        }
        Angle0x94 = Angle0x8C; Float0xA8 = 0;                                                                        // (6)
        Byte0x84 = IsBodyInPosition(); Byte0xDB = false; Byte0xDC = false; Byte0x85 = false;                         // (7)
        if (Byte0x84) return 0;
        float turns=MathF.Floor(MathF.Abs(Float0xA4/BitConverter.Int32BitsToSingle(0x40490FDB)));
        uint count=float.IsNaN(turns)||turns<=0 ? 0u : turns>=4294967296f ? uint.MaxValue : (uint)turns;
        ushort halfRevs = IsAbsolute ? (ushort)0 : unchecked((ushort)count);            // (8)
        if (_robot.TurnInPlace(Angle0x9C, Speed0xC4, Accel0xC8, Tolerance0xB0, halfRevs, IsAbsolute, out byte id) != 0) { ActionId0xDA = id; return PanTiltResult.SendMessageToRobotFailed; }
        ActionId0xDA = id;
        Byte0xDB = true;                                                                                             // (9)
        if (Byte0xD8)
        {
            if (_robot is INativePanTiltServices native) native.RemoveKeepFaceAlive(33);
            float clipped=Float0xA4,limit=BitConverter.Int32BitsToSingle(0x3FC6D3F2);
            if(clipped>=limit)clipped=limit; else if(clipped<=-limit)clipped=-limit;
            Float0xAC=MathF.Abs(Float0xA4)*0.5f;
            float x=MathF.Tan(clipped)*BitConverter.Int32BitsToSingle(0x418C28F6);
            x*=BitConverter.Int32BitsToSingle(0x3F9D89D9);
            _eyeShiftRequests.Add("RemoveKeepFaceAlive(33); AddOrUpdateEyeShift(TurnInPlaceEyeDart,132)");
            if (_robot is INativePanTiltServices layers) Byte0xD9 = layers.AddEyeShift(Byte0xD9, x);
            else Cozmo.Robot.Behavior.SteppedBehavior.ReportMissing("M13-021/M13-022 Init 0x00546286: host IPanTiltRobot has no track-layer service; eye creation skipped");
        }
        return 0;
    }

    /// <summary><c>IsBodyInPosition</c> 0x00546374; writes the current angle to +0x8C.</summary>
    public bool IsBodyInPosition() => Gated(IsBodyInPositionUnderGate);
    private bool IsBodyInPositionUnderGate()
    {
        Angle0x8C = _robot.PoseAngleAroundZ;
        float remaining = MathF.Abs(Float0xA4 - Float0xA8);
        if (!(remaining < BitConverter.Int32BitsToSingle(0x40490FDB))) return false;
        bool near = Radians.IsNear(Angle0x8C, Angle0x9C, Tolerance0xB0 + BitConverter.Int32BitsToSingle(0x3727C5AC));
        bool ok = OriginChanges0xD4 != 0 ? near || remaining < MathF.Abs(Tolerance0xB0) : near;
        return ok && !_robot.BodyMoving;
    }

    /// <summary>The motorActionAck handler 0x0054D3F8 (tag 0xC4): the ack of this action's TurnInPlace sets +0xDC.</summary>
    public void HandleMotorActionAck(byte actionId) => Gated(() => { if (Byte0xDB && actionId == ActionId0xDA) Byte0xDC = true; });

    /// <summary><c>CheckIfDone</c> 0x0054642C.</summary>
    public uint CheckIfDone() => Gated(CheckIfDoneUnderGate);
    private uint CheckIfDoneUnderGate()
    {
        if (Byte0xDB && !Byte0xDC)
        {
            lock(DiagnosticCounterGate)
            {
                WaitingAckCounter=unchecked((ushort)(WaitingAckCounter+1));
                if(WaitingAckCounter>=11)
                {
                    Emit(EngineLogLevel.Debug,"Actions","TurnInPlaceAction.CheckIfDone.WaitingForAck",FormattableString.Invariant($"[{unchecked((int)Tag)}] ActionID: {ActionId0xDA}"));
                    WaitingAckCounter=0;
                }
            }
            return PanTiltResult.Running;
        }
        uint currentFrame=_robot.OriginId;
        if(currentFrame!=Origin0xD0)
        {
            ++OriginChanges0xD4;
            bool inPosition=IsBodyInPositionUnderGate();
            Emit(EngineLogLevel.Info,"Actions","TurnInPlaceAction.CheckIfDone.PfidChanged",FormattableString.Invariant($"[{unchecked((int)Tag)}] pose frame ID changed (old={unchecked((int)Origin0xD0)}, new={unchecked((int)currentFrame)}). No longer comparing angles to check if done - using angular distance traversed instead. (relocalizedCnt={unchecked((int)OriginChanges0xD4)}) (inPositionNow={(inPosition?1:0)})"));
            Origin0xD0=_robot.OriginId;Angle0x94=_robot.PoseAngleAroundZ;
        }
        if(!Byte0x84)Byte0x84=IsBodyInPositionUnderGate();
        Float0xA8+=Radians.Rescale(Angle0x8C-Angle0x94);
        Angle0x94=Angle0x8C;
        if(Byte0xD9!=0 && (Byte0x84 || MathF.Abs(Float0xA8)>Float0xAC))
        {
            Emit(EngineLogLevel.Debug,"Actions","TurnInPlaceAction.CheckIfDone.RemovingEyeShift",$"Currently at {Fixed(Degrees(Angle0x8C))}deg, on the way to {Fixed(Degrees(Angle0x9C))}deg (traversed {Fixed(Degrees(Float0xA8))}deg)");
            if(_robot is INativePanTiltServices native)native.RemoveEyeShift(Byte0xD9,99);
            Byte0xD9=0;_eyeShiftRequests.Add("RemoveEyeShift(+0xD9, 99)");
        }
        bool moving=_robot.BodyMoving;
        if(moving)Byte0x85=true;
        uint result=PanTiltResult.Running;
        if(Byte0x84){Emit(EngineLogLevel.Info,"Actions","TurnInPlaceAction.CheckIfDone.InPosition",PositionText("In Position: "));result=0;}
        else
        {
            lock(DiagnosticCounterGate)
            {
                AngleNotReachedCounter=unchecked((ushort)(AngleNotReachedCounter+1));
                if(AngleNotReachedCounter>=11)
                {
                    Emit(EngineLogLevel.Debug,"Actions","TurnInPlaceAction.CheckIfDone.AngleNotReached",PositionText("Waiting for body to reach angle: "));
                    AngleNotReachedCounter=0;
                }
            }
            if(!moving && Byte0x85)
            {
                Emit(EngineLogLevel.Warning,null,"TurnInPlaceAction.StoppedMakingProgress",FormattableString.Invariant($"[{unchecked((int)Tag)}] giving up since we stopped moving. currentAngle={Fixed(Degrees(Angle0x8C))}deg, target={Fixed(Degrees(Angle0x9C))}deg, angDistExp={Fixed(Degrees(Float0xA4))}deg, angDistTrav={Fixed(Degrees(Float0xA8))}deg (pfid: {unchecked((int)_robot.OriginId)})"));
                result=PanTiltResult.MotorStoppedMakingProgress;
            }
        }
        return IsOffTreadsStateValidUnderGate()?result:PanTiltResult.InvalidOffTreadsState;
    }
}

/// <summary>
/// The engine's <c>WaitForImagesAction</c> (M13-020). Constructor 0x0054CA64: <c>numFrames</c> at +0x78, the
/// <c>afterTimeStamp</c> at +0x7C, the <see cref="VisionMode"/> at +0x88, +0x80/+0x84 (the subscription handle) zero, the
/// count at +0x8C zero, IAction type 0x32 and no tracks locked (0x0054CAC2..0x0054CAC4). <c>Init</c> 0x0054CB68 zeroes
/// the count, subscribes to tag 0x43 <c>RobotProcessedImage</c> and returns 0. The handler 0x0054DD88 counts a message
/// only when its timestamp is greater than +0x7C and either the wanted mode is 0x10 (<see cref="VisionMode.Count"/>,
/// "any image") or the message's vision modes contain it. <c>CheckIfDone</c> 0x0054CC1C is RUNNING while the count is
/// below <c>numFrames</c>, else it drops the subscription and returns 0.
///
/// What is not built: the producer. The stack does not build the <c>RobotProcessedImage</c> broadcast (M11-035), so
/// nothing calls <see cref="HandleRobotProcessedImage"/> yet, and no behaviour in this stack constructs the action
/// (they wait on <c>VisionSystem.FramesProcessed</c>). The action's name text (built with <c>to_string</c>) was not read.
/// </summary>
// fidelity: M13-020
public sealed class WaitForImagesAction
{
    /// <summary>IAction type stored by the constructor (0x0054CAC2).</summary>
    public const int ActionType = 0x32;
    /// <summary>RobotProcessedImage's message tag (0x0054CB8A <c>movs r2,#0x43</c>).</summary>
    public const int RobotProcessedImageTag = 0x43;

    public WaitForImagesAction(int numFrames, VisionMode visionMode, uint afterTimestamp)
    {
        NumFrames = numFrames; VisionMode = visionMode; AfterTimestamp = afterTimestamp;
    }

    public int NumFrames { get; }              // +0x78
    public uint AfterTimestamp { get; }        // +0x7C
    public VisionMode VisionMode { get; }      // +0x88
    public int Count { get; private set; }     // +0x8C
    /// <summary>Whether the tag-0x43 subscription (+0x80/+0x84) is held.</summary>
    public bool IsSubscribed { get; private set; }

    /// <summary><c>Init</c> 0x0054CB68: count = 0 (0x0054CB7A), subscribe, return 0.</summary>
    public uint Init() { Count = 0; IsSubscribed = true; return 0; }

    /// <summary>The handler 0x0054DD88 for one <c>RobotProcessedImage</c> message.</summary>
    public void HandleRobotProcessedImage(uint timestamp, IReadOnlyCollection<VisionMode> visionModes)
    {
        if (!IsSubscribed) return;                           // the engine's handle is released on completion
        if (!(timestamp > AfterTimestamp)) return;           // 0x0054DD9A cmp; bls 0x0054DE36
        if (VisionMode == VisionMode.Count || visionModes.Contains(VisionMode)) Count++;
    }

    /// <summary><c>CheckIfDone</c> 0x0054CC1C.</summary>
    public uint CheckIfDone()
    {
        if (Count < NumFrames) return PanAndTiltAction.Running;
        IsSubscribed = false;
        return PanAndTiltAction.Success;
    }
}
