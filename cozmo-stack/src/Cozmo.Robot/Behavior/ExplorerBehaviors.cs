using System.Text.Json;
using Cozmo.Protocol;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Behavior;

/// <summary>
/// The parameters of <c>BehaviorExploreLookAroundInPlace</c> (<c>LoadConfig</c> 0x005E1EC0 reads exactly these keys).
/// </summary>
public sealed record LookAroundParams
{
    public bool ShouldResetTurnDirection { get; init; } = true;
    public bool ResetBodyFacingOnStart { get; init; } = true;
    public bool ShouldLowerLift { get; init; } = true;
    public bool CanCarryCube { get; init; }
    public double DistanceFromRecentLocationMinMm { get; init; }
    public int RecentLocationsMax { get; init; }
    public double AngleOfFocusDeg { get; init; }
    public int NumberOfScansBeforeStop { get; init; }
    public double BodyTurnSpeedDegPerSec { get; init; } = 120;
    public double HeadTurnSpeedWithBodyDegPerSec { get; init; } = 45;
    public double HeadTurnSpeedAloneDegPerSec { get; init; } = 60;
    public double MainTurnCwChance { get; init; } = 0.5;
    public (double Min, double Max) S1Body { get; init; } = (10, 30);
    public (double Min, double Max) S1Head { get; init; } = (-15, -5);
    public (double Min, double Max) S2Wait { get; init; } = (0.5, 1.25);
    public AnimationTrigger? S2WaitAnim { get; init; }
    public (double Min, double Max) S3Body { get; init; } = (5, 25);
    public (double Min, double Max) S3Head { get; init; } = (-5, 5);
    public (double Min, double Max) S4BodyRel { get; init; } = (5, 5);
    public (double Min, double Max) S4Head { get; init; } = (5, 5);
    public (int Min, int Max) S4HeadChanges { get; init; } = (0, 0);
    public (double Min, double Max) S4Wait { get; init; } = (0.1, 0.1);
    public AnimationTrigger? S4WaitAnim { get; init; }
    public (double Min, double Max) S5BodyRel { get; init; } = (5, 5);
    public (double Min, double Max) S5Head { get; init; } = (-5, 5);
    public (double Min, double Max) S6Body { get; init; } = (30, 65);
    public (double Min, double Max) S6Head { get; init; } = (-5, 5);

    public static LookAroundParams FromJson(JsonElement p)
    {
        double D(string k, double d) => p.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : d;
        int I(string k, int d) => p.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : d;
        bool B(string k, bool d) => p.TryGetProperty(k, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False ? v.GetBoolean() : d;
        AnimationTrigger? T(string k) => p.TryGetProperty(k, out var v) && Enum.TryParse<AnimationTrigger>(v.GetString(), out var t) ? t : null;
        (double, double) R(string a, string b, (double, double) d) => (D(a, d.Item1), D(b, d.Item2));
        return new LookAroundParams
        {
            ShouldResetTurnDirection = B("behavior_ShouldResetTurnDirection", true), ResetBodyFacingOnStart = B("behavior_ResetBodyFacingOnStart", true),
            ShouldLowerLift = B("behavior_ShouldLowerLift", true), CanCarryCube = B("behavior_CanCarryCube", false),
            DistanceFromRecentLocationMinMm = D("behavior_DistanceFromRecentLocationMin_mm", 0), RecentLocationsMax = I("behavior_RecentLocationsMax", 0),
            AngleOfFocusDeg = D("behavior_AngleOfFocus_deg", 0), NumberOfScansBeforeStop = I("behavior_NumberOfScansBeforeStop", 0),
            BodyTurnSpeedDegPerSec = D("sx_BodyTurnSpeed_degPerSec", 120), HeadTurnSpeedWithBodyDegPerSec = D("sxt_HeadTurnSpeed_degPerSec", 45), HeadTurnSpeedAloneDegPerSec = D("sxh_HeadTurnSpeed_degPerSec", 60),
            MainTurnCwChance = D("s0_MainTurnCWChance", 0.5),
            S1Body = R("s1_BodyAngleRangeMin_deg", "s1_BodyAngleRangeMax_deg", (10, 30)), S1Head = R("s1_HeadAngleRangeMin_deg", "s1_HeadAngleRangeMax_deg", (-15, -5)),
            S2Wait = R("s2_WaitMin_sec", "s2_WaitMax_sec", (0.5, 1.25)), S2WaitAnim = T("s2_WaitAnimTrigger"),
            S3Body = R("s3_BodyAngleRangeMin_deg", "s3_BodyAngleRangeMax_deg", (5, 25)), S3Head = R("s3_HeadAngleRangeMin_deg", "s3_HeadAngleRangeMax_deg", (-5, 5)),
            S4BodyRel = R("s4_BodyAngleRelativeRangeMin_deg", "s4_BodyAngleRelativeRangeMax_deg", (5, 5)), S4Head = R("s4_HeadAngleRangeMin_deg", "s4_HeadAngleRangeMax_deg", (5, 5)),
            S4HeadChanges = (I("s4_HeadAngleChangesMin", 0), I("s4_HeadAngleChangesMax", 0)), S4Wait = R("s4_WaitBetweenChangesMin_sec", "s4_WaitBetweenChangesMax_sec", (0.1, 0.1)), S4WaitAnim = T("s4_WaitAnimTrigger"),
            S5BodyRel = R("s5_BodyAngleRelativeRangeMin_deg", "s5_BodyAngleRelativeRangeMax_deg", (5, 5)), S5Head = R("s5_HeadAngleRangeMin_deg", "s5_HeadAngleRangeMax_deg", (-5, 5)),
            S6Body = R("s6_BodyAngleRangeMin_deg", "s6_BodyAngleRangeMax_deg", (30, 65)), S6Head = R("s6_HeadAngleRangeMin_deg", "s6_HeadAngleRangeMax_deg", (-5, 5)),
        };
    }
}

/// <summary>
/// <c>BehaviorExploreLookAroundInPlace</c> (0x005E1D80..0x005E3A10): the look-around scan every explorer and
/// search behaviour is built on. <c>InitInternal</c>: the motion profile, the lift lowered when
/// <c>behavior_ShouldLowerLift</c> (<c>MoveLiftToHeightAction</c> preset low), the body facing recorded when
/// <c>behavior_ResetBodyFacingOnStart</c>, <c>DecideTurnDirection</c> (<c>RandDbl</c> against <c>s0_MainTurnCWChance</c>;
/// kept across iterations unless <c>behavior_ShouldResetTurnDirection</c>). States (<c>TransitionToS…</c>):
/// S1 opposite turn (body by a random s1 angle against the main direction, head to a random s1 angle, through
/// <c>CreateBodyAndHeadTurnAction</c>: a <c>PanAndTiltAction</c> with the profile's or the sx speeds);
/// S2 pause (<c>WaitAction</c> of a random s2 time with the s2 animation when one is named); S3 main turn (s3
/// ranges, the main direction); S4 head-only up (a random number of s4 head changes, each a relative body
/// mini-turn and a head angle from the s4 ranges with a wait and the s4 animation, "Triggering %s");
/// S5 head-only down (s5); S6 main turn final (s6); S7 iteration end: the heading turned since the start is
/// accumulated ("Done %.2f deg so far"); with an angle of focus the direction reverses at the cone's side
/// ("Reached cone side %d"), otherwise a full turn (2π, 0x40C90FDB) completes an iteration; after
/// <c>behavior_NumberOfScansBeforeStop</c> iterations (0: for ever) the behaviour completes ("Done (reached max
/// iterations)"), else "Starting another iteration". <c>IsRunnableInternal</c> (0x005E259C): not when carrying a
/// cube unless <c>behavior_CanCarryCube</c>, and not within <c>behavior_DistanceFromRecentLocationMin_mm</c> of
/// one of the last <c>behavior_RecentLocationsMax</c> places it ran. Body and head turns go through
/// <see cref="PanAndTilt"/>.
/// </summary>
public class ExploreLookAroundInPlaceBehavior : ActionBehavior
{
    public enum Phase { Idle, S1_OppositeTurn, S2_Pause, S3_MainTurn, S4_HeadOnlyUp, S5_HeadOnlyDown, S6_MainTurnFinal, S7_IterationEnd }

    protected readonly VisionSystem V;
    protected readonly ManipulationSystem? M;
    private readonly List<Vec3> _recentLocations = new();
    private double _mainSign = 1;
    private double _startHeading, _lastHeading, _doneRad;
    private int _iterations, _s4Left;

    public ExploreLookAroundInPlaceBehavior(VisionSystem v, string id, LookAroundParams p, ManipulationSystem? m = null, string behaviorClass = "ExploreLookAroundInPlace")
        : base(id, behaviorClass) { V = v; Params = p; M = m; }

    public LookAroundParams Params { get; }
    public Phase CurrentPhase { get; protected set; }
    public int Iterations => _iterations;
    public double DoneDeg => _doneRad * 180 / Math.PI;
    public IReadOnlyList<(double BodyDeg, double HeadDeg)> Turns => _turns;
    private readonly List<(double, double)> _turns = new();
    protected override bool KeepsRunningWithoutAction => true;

    protected override bool IsRunnableInternal(BehaviorContext context)
    {
        if (!Params.CanCarryCube && M is not null && M.Docking.Carrying.IsCarryingObject) return false;
        var pose = V.History.Latest?.RobotPose;
        if (pose is null) return false;
        if (Params.RecentLocationsMax > 0 && Params.DistanceFromRecentLocationMinMm > 0)
            foreach (var loc in _recentLocations) { var d = loc - pose.Value.Translation; if (Math.Sqrt(d.X * d.X + d.Y * d.Y) < Params.DistanceFromRecentLocationMinMm) return false; }
        return true;
    }

    protected override void OnStart()
    {
        var pose = V.History.Latest?.RobotPose;
        if (pose is null) { Finish(); return; }
        if (Params.RecentLocationsMax > 0) { _recentLocations.Add(pose.Value.Translation); while (_recentLocations.Count > Params.RecentLocationsMax) _recentLocations.RemoveAt(0); }
        _startHeading = _lastHeading = pose.Value.AngleAroundZ; _doneRad = 0; _iterations = 0; _turns.Clear();
        if (Params.ShouldResetTurnDirection || _mainSign == 0) DecideTurnDirection();
        Log($"{Id}.InitInternal: Starting first iteration (main turn {(_mainSign > 0 ? "CCW" : "CW")})");
        if (Params.ShouldLowerLift) V.Robot.Motion.SetLiftHeightAsync(LiftPresets.LowDockMm, requireCalibration: false);
        BeginStateMachine();
    }

    protected void DecideTurnDirection() => _mainSign = Context.Random.NextDouble() < Params.MainTurnCwChance ? -1 : 1;
    protected double Rand((double Min, double Max) r) => r.Min + Context.Random.NextDouble() * (r.Max - r.Min);

    /// <summary>The engine's <c>BeginStateMachine</c>: S1. Subclasses (FindFaces) start here after their own preamble.</summary>
    protected virtual void BeginStateMachine() => TransitionToS1_OppositeTurn();

    private void TransitionToS1_OppositeTurn()
    {
        CurrentPhase = Phase.S1_OppositeTurn;
        BodyAndHeadTurn(-_mainSign * Rand(Params.S1Body), Rand(Params.S1Head), TransitionToS2_Pause);
    }

    private void TransitionToS2_Pause()
    {
        CurrentPhase = Phase.S2_Pause;
        double wait = Rand(Params.S2Wait);
        if (Params.S2WaitAnim is { } anim) PlayTrigger(anim, () => Wait(wait, TransitionToS3_MainTurn));
        else Wait(wait, TransitionToS3_MainTurn);
    }

    private void TransitionToS3_MainTurn()
    {
        CurrentPhase = Phase.S3_MainTurn;
        BodyAndHeadTurn(_mainSign * Rand(Params.S3Body), Rand(Params.S3Head), TransitionToS4_HeadOnlyUp);
    }

    private void TransitionToS4_HeadOnlyUp()
    {
        CurrentPhase = Phase.S4_HeadOnlyUp;
        _s4Left = Context.Random.Next(Params.S4HeadChanges.Min, Params.S4HeadChanges.Max + 1);
        S4Step();
    }

    private void S4Step()
    {
        if (_s4Left <= 0) { TransitionToS5_HeadOnlyDown(); return; }
        _s4Left--;
        BodyAndHeadTurn(_mainSign * Rand(Params.S4BodyRel), Rand(Params.S4Head), () =>
        {
            double wait = Rand(Params.S4Wait);
            if (Params.S4WaitAnim is { } anim) { Log($"{Id}.S4.StartingPauseAnimAction: Triggering pause"); PlayTrigger(anim, () => Wait(wait, S4Step)); }
            else Wait(wait, S4Step);
        });
    }

    private void TransitionToS5_HeadOnlyDown()
    {
        CurrentPhase = Phase.S5_HeadOnlyDown;
        BodyAndHeadTurn(_mainSign * Rand(Params.S5BodyRel), Rand(Params.S5Head), TransitionToS6_MainTurnFinal);
    }

    private void TransitionToS6_MainTurnFinal()
    {
        CurrentPhase = Phase.S6_MainTurnFinal;
        BodyAndHeadTurn(_mainSign * Rand(Params.S6Body), Rand(Params.S6Head), TransitionToS7_IterationEnd);
    }

    private void TransitionToS7_IterationEnd()
    {
        CurrentPhase = Phase.S7_IterationEnd;
        var pose = V.History.Latest?.RobotPose;
        if (pose is not null)
        {
            double delta = StraightLinePlanner.Wrap(pose.Value.AngleAroundZ - _lastHeading);
            _lastHeading = pose.Value.AngleAroundZ;
            _doneRad += Math.Abs(delta);
        }
        Log($"{Id}.IterationEnd: Done {DoneDeg:F2} deg so far");
        bool iterationDone;
        if (Params.AngleOfFocusDeg > 0)
        {
            double fromStart = StraightLinePlanner.Wrap(_lastHeading - _startHeading) * 180 / Math.PI;
            if (Math.Abs(fromStart) >= Params.AngleOfFocusDeg) { Log($"{Id}.IterationEnd: Reached cone side {(fromStart > 0 ? 1 : -1)}"); _mainSign = -Math.Sign(fromStart); }
            iterationDone = Math.Abs(fromStart) >= Params.AngleOfFocusDeg;
        }
        else iterationDone = _doneRad >= 2 * Math.PI;
        if (iterationDone) { _iterations++; _doneRad = 0; }
        if (iterationDone && Params.NumberOfScansBeforeStop > 0 && _iterations >= Params.NumberOfScansBeforeStop)
        {
            Log($"{Id}.IterationEnd: Done (reached max iterations)");
            CurrentPhase = Phase.Idle; Finish(); return;
        }
        if (iterationDone) Log($"{Id}.IterationEnd: Starting another iteration");
        TransitionToS1_OppositeTurn();
    }

    /// <summary><c>CreateBodyAndHeadTurnAction</c>: a relative body turn (degrees) and an absolute head angle (degrees), as one pan-and-tilt.</summary>
    protected void BodyAndHeadTurn(double bodyDeg, double headDeg, Action onDone)
    {
        _turns.Add((bodyDeg, headDeg));
        var pose = V.History.Latest?.RobotPose;
        if (pose is null) { Finish(); return; }
        double absBody = pose.Value.AngleAroundZ + bodyDeg * Math.PI / 180;
        double head = Math.Clamp(headDeg * Math.PI / 180, HeadGeometry.MinHeadAngleRad, HeadGeometry.MaxHeadAngleRad);
        Log($"{Id}.PanAndTilt: Body {bodyDeg:F2}, Head {headDeg:F2}");
        RunAction($"PanAndTilt({bodyDeg:F0} deg, head {headDeg:F0} deg)", ct => PanAndTilt.RunAsync(V, absBody, head, Params.BodyTurnSpeedDegPerSec * Math.PI / 180, ct), _ => onDone(), false);
    }
}

/// <summary>
/// The engine's <c>PanAndTiltAction</c> as the explorer behaviours use it: an absolute body heading and an
/// absolute head angle, sent as <c>SetBodyAngle</c> (through <see cref="TurnTowardsPose"/>'s message) and
/// <c>SetHeadAngle</c>, waiting for the heading to settle within 2°. Tests replace it through
/// <see cref="VisionSystem.PanTiltOverride"/>.
/// </summary>
public static class PanAndTilt
{
    public static async Task<bool> RunAsync(VisionSystem v, double absoluteBodyRad, double headRad, double bodySpeedRadPerSec, CancellationToken cancel)
    {
        if (v.PanTiltOverride is not null) return await v.PanTiltOverride(absoluteBodyRad, headRad, cancel);
        var t = v.Robot.Transport;
        t.Send(TurnTowardsPose.Message(absoluteBodyRad, bodySpeedRadPerSec, TurnTowardsPose.AccelRadPerSec2, TurnTowardsPose.ToleranceRad, 0, true, 2), flush: true);
        t.Send(new SetHeadAngle { AngleRad = (float)headRad, MaxSpeedRadPerSec = 10f, AccelRadPerSec2 = 10f, DurationSec = 0f, ActionId = 3 }, flush: true);
        var deadline = DateTime.UtcNow.AddSeconds(6);
        while (DateTime.UtcNow < deadline && !cancel.IsCancellationRequested)
        {
            await Task.Delay(33, CancellationToken.None);
            if (v.History.Latest is { } now && Math.Abs(StraightLinePlanner.Wrap(absoluteBodyRad - now.RobotPose.AngleAroundZ)) <= TurnTowardsPose.ToleranceRad && !now.Moving) return true;
        }
        return false;
    }
}

/// <summary>
/// <c>BehaviorFindFaces</c> (0x005C1936..0x005C1E40) on the look-around: config <c>maxFaceAgeToLook_ms</c>
/// (180000 / 120000 / 10000). <c>InitInternal</c>: when the last observed face is younger than that,
/// <c>TransitionToLookAtLastFace</c> (<c>TurnTowardsFaceAction(π)</c>) then the base scan, else
/// <c>TransitionToLookUp</c> (a head-only turn up through <c>CreateHeadTurnAction</c>) then the base scan
/// ("%s is transitioning to base class, setting initial body direction"). Runnable only through the face
/// pipeline's detector for the face branch; the scan itself needs no face.
/// </summary>
public sealed class FindFacesBehavior : ExploreLookAroundInPlaceBehavior
{
    public FindFacesBehavior(VisionSystem v, string id, LookAroundParams p, uint maxFaceAgeToLookMs, ManipulationSystem? m = null) : base(v, id, p, m, "FindFaces") => MaxFaceAgeToLookMs = maxFaceAgeToLookMs;
    public uint MaxFaceAgeToLookMs { get; }
    public bool LookedAtLastFace { get; private set; }

    protected override void BeginStateMachine()
    {
        uint now = V.History.Latest?.Timestamp ?? 0;
        var last = V.Faces.GetLastObservedFace();
        if (last is not null && now - last.LastObservedTimestamp <= MaxFaceAgeToLookMs)
        {
            LookedAtLastFace = true;
            Log("FindFacesLookAtLast");
            var turn = new TurnTowardsFaceAction(V, last.Id);
            RunAction("TurnTowardsFace(last)", turn.RunAsync, _ => { Log("BehaviorFindFaces.TransitionToBaseClass"); base.BeginStateMachine(); }, FaceActionResult.Abort);
        }
        else
        {
            Log("FindFacesLookUp");
            BodyAndHeadTurn(0, Params.S3Head.Max, () => { Log("BehaviorFindFaces.TransitionToBaseClass"); base.BeginStateMachine(); });
        }
    }
}

/// <summary>
/// <c>BehaviorDriveInDesperation</c> (0x005D8AE0..0x005D9F60; configs Needs_SevereLowEnergyState /
/// Needs_SevereLowRepairState: <c>useCubes</c>, <c>minTimeToIdle</c>, <c>maxTimeToIdle</c>, <c>requestAnimTrigger</c>,
/// <c>motionProfile</c>). <c>TransitionToIdle</c>: "idling for %f sec" (random in the range); then
/// <c>TransitionFromIdle</c>: with cubes (<c>useCubes</c> and a located cube) → <c>TransitionToDriveToCube</c>
/// (drive to the closest) → <c>TransitionToLookAtCube</c> → the request; without → <c>RandomizeNumDrivingRounds</c>
/// (1..3, "driving to %d random points next time we drive") and <c>TransitionToDriveRandom</c> that many times
/// (<c>GetRandomDrivingPose</c>: 40..100 mm at 50..150° to a random side, "angle=%fdeg, dist=%fmm";
/// <c>DriveToPoseAction</c> with the config's profile, angle tolerance 0.174533); then <c>TransitionToRequest</c>:
/// <c>TriggerAnimationAction(requestAnimTrigger)</c> wrapped in a <c>TurnTowardsFaceWrapperAction(π)</c>; then idle
/// again, for as long as the needs activity keeps it. Runnable always (the activity's strategy gates it).
/// </summary>
public sealed class DriveInDesperationBehavior : ActionBehavior
{
    public enum Phase { Idle, DriveRandom, DriveToCube, LookAtCube, Request }
    private readonly VisionSystem _v;
    private readonly ManipulationSystem _m;
    private int _roundsLeft;

    public DriveInDesperationBehavior(VisionSystem v, ManipulationSystem m, string id, bool useCubes, double minTimeToIdle, double maxTimeToIdle, AnimationTrigger requestAnim, PathMotionProfile? profile = null)
        : base(id, "DriveInDesperation")
    {
        _v = v; _m = m; UseCubes = useCubes; MinTimeToIdle = minTimeToIdle; MaxTimeToIdle = maxTimeToIdle; RequestAnim = requestAnim; Profile = profile ?? PathMotionProfile.Default;
    }

    public bool UseCubes { get; }
    public double MinTimeToIdle { get; }
    public double MaxTimeToIdle { get; }
    public AnimationTrigger RequestAnim { get; }
    public PathMotionProfile Profile { get; }
    public Phase CurrentPhase { get; private set; }
    public int Requests { get; private set; }
    public int RandomDrives { get; private set; }
    /// <summary>Test hook: scales the idle waits.</summary>
    public double IdleScale { get; set; } = 1.0;
    protected override bool KeepsRunningWithoutAction => true;

    protected override void OnStart() => TransitionToIdle();

    private void TransitionToIdle()
    {
        CurrentPhase = Phase.Idle;
        double idle = (MinTimeToIdle + Context.Random.NextDouble() * (MaxTimeToIdle - MinTimeToIdle)) * IdleScale;
        Log($"{Id}.Idle: idling for {idle / IdleScale:F1} sec");
        Wait(idle, TransitionFromIdle);
    }

    private void TransitionFromIdle()
    {
        var cube = UseCubes ? _m.World.LocatedObjects.Where(o => CubeGeometry.IsCube(o.Type)).OrderBy(o => _m.RobotPose() is { } r ? (o.Pose.Translation - r.Translation).Length : 0).FirstOrDefault() : null;
        if (cube is not null) { TransitionToDriveToCube(cube.ObjectId); return; }
        _roundsLeft = Context.Random.Next(1, 4);
        Log($"{Id}.Drive: driving to {_roundsLeft} random points next time we drive");
        TransitionToDriveRandom();
    }

    private void TransitionToDriveRandom()
    {
        if (_roundsLeft <= 0) { TransitionToRequest(); return; }
        _roundsLeft--;
        CurrentPhase = Phase.DriveRandom;
        var robot = _m.RobotPose();
        if (robot is null) { Finish(); return; }
        double dist = 40 + Context.Random.NextDouble() * 60;
        double ang = (50 + Context.Random.NextDouble() * 100) * Math.PI / 180 * (Context.Random.NextDouble() < 0.5 ? 1 : -1);
        double heading = robot.Value.AngleAroundZ + ang;
        var goal = new Pose3d(Mat3.AboutZ(heading), robot.Value.Translation + new Vec3(Math.Cos(heading) * dist, Math.Sin(heading) * dist, 0));
        Log($"BehaviorDriveInDesperation.GetRandomDrivingPose: angle={ang * 180 / Math.PI:F1}deg, dist={dist:F1}mm");
        RandomDrives++;
        var drive = new DriveToPoseAction(_m) { Goal = goal, Profile = Profile };
        RunAction("DriveToPose(random)", drive.RunAsync, _ => TransitionToDriveRandom(), ActionResult.Abort);
    }

    private void TransitionToDriveToCube(uint id)
    {
        CurrentPhase = Phase.DriveToCube;
        var drive = new DriveToObjectAction(_m, id, PreActionType.Docking) { Profile = Profile };
        RunAction($"DriveToCube({id})", drive.RunAsync, _ =>
        {
            CurrentPhase = Phase.LookAtCube;
            RunAction($"LookAtCube({id})", ct => _m.TurnTowardsObjectAsync(id, Math.PI, ct), _ => TransitionToRequest(), false);
        }, ActionResult.Abort);
    }

    private void TransitionToRequest()
    {
        CurrentPhase = Phase.Request;
        Requests++;
        var turn = new TurnTowardsFaceAction(_v, SmartFaceID.Invalid);
        RunAction("TurnTowardsFaceWrapper", turn.RunAsync, _ => PlayTrigger(RequestAnim, TransitionToIdle), FaceActionResult.Abort);
    }
}

/// <summary>
/// <c>BehaviorExpressNeeds</c> (0x005EE19C..0x005EE8C0; six configs): <c>need</c>, <c>needBracket</c>, <c>animTriggers</c>,
/// <c>cooldown</c> (a graph from the need's level to seconds), <c>requiredSevereNeedsState</c>,
/// <c>shouldClearExpressedState</c> / <c>caresAboutExpressedState</c>. <c>IsRunnableInternal</c>:
/// <c>NeedsState::IsNeedAtBracket(need, bracket)</c> and the cooldown since the last run (<c>GetCooldownSec</c>
/// evaluates the graph at the need's level). <c>InitInternal</c>: <c>TurnTowardsFaceAction(−1, π, false)</c>
/// then <c>TriggerAnimationAction</c> for each trigger.
/// </summary>
public sealed class ExpressNeedsBehavior : ActionBehavior
{
    private readonly VisionSystem? _v;
    private readonly NeedsManager _needs;
    private double? _lastRunSec;

    public ExpressNeedsBehavior(NeedsManager needs, string id, NeedId need, NeedBracketId bracket, IReadOnlyList<AnimationTrigger> triggers, Graph2d cooldown, VisionSystem? v = null, NeedId? requiredSevereState = null)
        : base(id, "ExpressNeeds")
    {
        _needs = needs; Need = need; Bracket = bracket; Triggers = triggers; Cooldown = cooldown; _v = v; RequiredSevereState = requiredSevereState;
    }

    public NeedId Need { get; }
    public NeedBracketId Bracket { get; }
    public IReadOnlyList<AnimationTrigger> Triggers { get; }
    public Graph2d Cooldown { get; }
    public NeedId? RequiredSevereState { get; }

    public double CooldownSec => Cooldown.EvaluateY(_needs.State.GetNeedLevel(Need));

    protected override bool IsRunnableInternal(BehaviorContext context)
    {
        if (!_needs.State.IsNeedAtBracket(Need, Bracket)) return false;
        if (RequiredSevereState is { } s && !_needs.State.IsNeedAtBracket(s, NeedBracketId.Critical)) return false;
        return _lastRunSec is not { } last || Clock() / 1000.0 - last >= CooldownSec;
    }

    protected override void OnStart()
    {
        _lastRunSec = Clock() / 1000.0;
        Log($"expressing {Need} {Bracket} (level {_needs.State.GetNeedLevel(Need):F2}, cooldown {CooldownSec:F0} s)");
        void Play()
        {
            int i = 0;
            void Next() { if (i >= Triggers.Count) { Finish(); return; } PlayTrigger(Triggers[i++], Next); }
            Next();
        }
        if (_v is not null && _v.Faces.HasAnyFaces())
        {
            var turn = new TurnTowardsFaceAction(_v, SmartFaceID.Invalid);
            RunAction("TurnTowardsFace(last)", turn.RunAsync, _ => Play(), FaceActionResult.Abort);
        }
        else Play();
    }
}

/// <summary>
/// <c>BehaviorPlayAnimOnNeedsChange</c> (0x005BFDxx; Needs_SevereLow{Energy,Play,Repair}GetIn): a
/// <c>BehaviorPlayAnimSequence</c> for a <c>need</c>; runnable when the need is Critical and its severe state has
/// not been expressed (<c>ShouldGetInBePlayed</c>: <c>IsNeedAtBracket(need, Critical)</c>); <c>StopInternal</c>
/// marks the severe state expressed when the get-in played ("SevereNeedExpressed").
/// </summary>
public sealed class PlayAnimOnNeedsChangeBehavior : SteppedBehavior
{
    private readonly NeedsManager _needs;
    public PlayAnimOnNeedsChangeBehavior(NeedsManager needs, string id, NeedId need, IReadOnlyList<AnimationTrigger> triggers) : base(id, "PlayAnimOnNeedsChange") { _needs = needs; Need = need; Triggers = triggers; }
    public NeedId Need { get; }
    public IReadOnlyList<AnimationTrigger> Triggers { get; }
    private bool _played;

    public bool ShouldGetInBePlayed() => _needs.State.IsNeedAtBracket(Need, NeedBracketId.Critical) && !_needs.IsSevereExpressed(Need);
    protected override bool IsRunnableInternal(BehaviorContext context) => ShouldGetInBePlayed();

    protected override void OnStart()
    {
        _played = false;
        int i = 0;
        void Next() { if (i >= Triggers.Count) { _played = true; Finish(); return; } PlayTrigger(Triggers[i++], Next); }
        Next();
    }

    protected override void OnStop(BehaviorStopReason reason)
    {
        if (_played || reason == BehaviorStopReason.Completed) { _needs.SetSevereExpressed(Need, true); Log($"severe {Need} state expressed"); }
    }
}

/// <summary><c>BehaviorWait</c> (Needs_Wait): does nothing for as long as it is kept; the last resort of the needs activities' priority lists.</summary>
public sealed class WaitBehavior : SteppedBehavior
{
    public WaitBehavior(string id = "Needs_Wait") : base(id, "Wait") { }
    protected override bool KeepsRunningWithoutAction => true;
    protected override void OnStart() => Log("waiting");
}

/// <summary>
/// <c>BehaviorEarnedSparks</c> (0x005DAEC2..0x005DAF80): runnable when the needs manager has a freeplay sparks
/// reward pending (the byte at +0x3D8 read by <c>IsRunnableInternal</c>); <c>InitInternal</c> plays 0xA4
/// <see cref="AnimationTrigger.EarnedSparks"/> lift-safe and the reward is communicated
/// (<c>SparksRewardCommunicatedToUser</c>). The sparks economy itself is the app's.
/// </summary>
public sealed class EarnedSparksBehavior : SteppedBehavior
{
    private readonly NeedsManager _needs;
    public EarnedSparksBehavior(NeedsManager needs, string id = "EarnedSparks") : base(id, "EarnedSparks") => _needs = needs;
    protected override bool IsRunnableInternal(BehaviorContext context) => _needs.SparksRewardPending;
    protected override void OnStart()
    {
        _needs.SparksRewardPending = false;
        PlayTrigger(AnimationTrigger.EarnedSparks, () => { Log("SparksRewardCommunicatedToUser"); Finish(); });
    }
}

/// <summary>Builds the explorer and needs behaviours from their shipped configs.</summary>
public static class ExplorerBehaviors
{
    public static IReadOnlyList<IBehavior> LoadShipped(string obbRoot, VisionSystem v, ManipulationSystem? m, NeedsManager needs, List<string>? problems = null)
    {
        var list = new List<IBehavior>();
        var dir = Path.Combine(obbRoot, "assets", "cozmo_resources", "config", "engine", "behaviorSystem", "behaviors");
        if (!Directory.Exists(dir)) return list;
        foreach (var f in Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories).OrderBy(x => x, StringComparer.Ordinal))
        {
            JsonDocument doc;
            try { doc = JsonDocument.Parse(File.ReadAllText(f), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }); }
            catch (JsonException e) { problems?.Add($"{Path.GetFileName(f)}: {e.Message}"); continue; }
            using (doc)
            {
                var r = doc.RootElement;
                if (!r.TryGetProperty("behaviorClass", out var clsEl) || !r.TryGetProperty("behaviorID", out var idEl)) continue;
                string cls = clsEl.GetString() ?? "", id = idEl.GetString() ?? "";
                IReadOnlyList<AnimationTrigger> Triggers()
                {
                    var l = new List<AnimationTrigger>();
                    if (r.TryGetProperty("animTriggers", out var arr)) foreach (var t in arr.EnumerateArray()) if (Enum.TryParse<AnimationTrigger>(t.GetString(), out var tr)) l.Add(tr); else problems?.Add($"{id}: animTrigger '{t.GetString()}' unknown");
                    return l;
                }
                switch (cls)
                {
                    case "ExploreLookAroundInPlace":
                        list.Add(new ExploreLookAroundInPlaceBehavior(v, id, r.TryGetProperty("params", out var p1) ? LookAroundParams.FromJson(p1) : new LookAroundParams(), m));
                        break;
                    case "FindFaces":
                        list.Add(new FindFacesBehavior(v, id, r.TryGetProperty("params", out var p2) ? LookAroundParams.FromJson(p2) : new LookAroundParams(),
                                                       r.TryGetProperty("maxFaceAgeToLook_ms", out var age) ? (uint)age.GetDouble() : 180000u, m));
                        break;
                    case "DriveInDesperation" when m is not null:
                        list.Add(new DriveInDesperationBehavior(v, m, id, r.TryGetProperty("useCubes", out var uc) && uc.GetBoolean(),
                                                                r.TryGetProperty("minTimeToIdle", out var mn) ? mn.GetDouble() : 1.5, r.TryGetProperty("maxTimeToIdle", out var mx) ? mx.GetDouble() : 6.5,
                                                                r.TryGetProperty("requestAnimTrigger", out var ra) && Enum.TryParse<AnimationTrigger>(ra.GetString(), out var rt) ? rt : AnimationTrigger.NeedsSevereLowEnergyRequest,
                                                                r.TryGetProperty("motionProfile", out var mp) ? Profile(mp) : null));
                        break;
                    case "ExpressNeeds":
                        if (Enum.TryParse<NeedId>(r.GetProperty("need").GetString(), true, out var need) && Enum.TryParse<NeedBracketId>(r.GetProperty("needBracket").GetString(), true, out var bracket))
                        {
                            var cd = r.TryGetProperty("cooldown", out var c) ? Graph2d.FromJson(c) : null;
                            NeedId? severe = r.TryGetProperty("requiredSevereNeedsState", out var rs) && Enum.TryParse<NeedId>(rs.GetString(), true, out var sn) ? sn : null;
                            list.Add(new ExpressNeedsBehavior(needs, id, need, bracket, Triggers(), cd ?? new Graph2d(new[] { (0.0, 20.0) }), v, severe));
                        }
                        else problems?.Add($"{id}: need / needBracket not parsed");
                        break;
                    case "PlayAnimOnNeedsChange":
                        if (Enum.TryParse<NeedId>(r.GetProperty("need").GetString(), true, out var need2)) list.Add(new PlayAnimOnNeedsChangeBehavior(needs, id, need2, Triggers()));
                        break;
                    case "Wait": list.Add(new WaitBehavior(id)); break;
                    case "EarnedSparks": list.Add(new EarnedSparksBehavior(needs, id)); break;
                }
            }
        }
        return list;
    }

    /// <summary>A <c>motionProfile</c> block (the engine's <c>PathMotionProfile</c> from JSON).</summary>
    public static PathMotionProfile Profile(JsonElement mp)
    {
        float F(string k, float d) => mp.TryGetProperty(k, out var v) ? (float)v.GetDouble() : d;
        return new PathMotionProfile
        {
            SpeedMmps = F("speed_mmps", 100), AccelMmps2 = F("accel_mmps2", 200), DecelMmps2 = F("decel_mmps2", 500),
            PointTurnSpeedRadPerSec = F("pointTurnSpeed_rad_per_sec", 2), PointTurnAccelRadPerSec2 = F("pointTurnAccel_rad_per_sec2", 10), PointTurnDecelRadPerSec2 = F("pointTurnDecel_rad_per_sec2", 10),
            DockSpeedMmps = F("dockSpeed_mmps", 60), DockAccelMmps2 = F("dockAccel_mmps2", 200), DockDecelMmps2 = F("dockDecel_mmps2", 500), ReverseSpeedMmps = F("reverseSpeed_mmps", 80),
        };
    }
}
