using Cozmo.Protocol;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Manipulation;

/// <summary>
/// The marker code <c>TurnTowardsObjectAction</c> is given by <c>SetupTurnAndVerifyAction</c> (M12-037): the dock's own marker code <c>[+0x82]</c>, or
/// <c>*(short*)Marker::ANY_CODE</c> (GOT 0x0103EA10) when <c>[+0xF7]</c> is set. The numeric value of <c>ANY_CODE</c> is not in the inventory, so it is a named case
/// and not a number.
/// </summary>
// fidelity: M12-037
public readonly record struct DockTurnCode(short? Code)
{
    /// <summary><c>Marker::ANY_CODE</c>.</summary>
    public static readonly DockTurnCode Any = new(null);
    public bool IsAny => Code is null;
    public override string ToString() => IsAny ? "ANY_CODE" : Code!.Value.ToString();
}

/// <summary>A sub-action <c>SetupTurnAndVerifyAction</c> adds to its compound (M12-037). Each one stores 1 at <c>[action+0x56]</c> (<see cref="Field0x56"/>).</summary>
// fidelity: M12-037
public abstract record DockSubAction
{
    /// <summary><c>[action+0x56] = 1</c> (0x00551F9C..0x00552164); its meaning is not in the inventory.</summary>
    public bool Field0x56 => true;
}

/// <summary>
/// <c>VisuallyVerifyNoObjectAtPoseAction(robot, pose, halfSize)</c> (0x10C bytes) with <c>AddIgnoreID(obj.ID)</c>: <see cref="Pose"/> is the target's pose in its root frame with the
/// translation raised by the object's Z extent, <see cref="HalfSize"/> is 0.5 * (dx, dy, dz) (M12-037). Its body is M8's and unread.
/// </summary>
public sealed record VisuallyVerifyNoObjectAtPose(Pose3d Pose, Vec3 HalfSize, IReadOnlyList<uint> IgnoreIds) : DockSubAction;

/// <summary>
/// <c>TurnTowardsObjectAction(robot, ObjectID [this+0x7C], short code, Radians(0), true, false)</c> (0x198 bytes, M12-037). The names of its last three arguments are not in the
/// inventory, so they are carried as the literals the record gives. Its body is M13's and unread.
/// </summary>
public sealed record TurnTowardsObject(uint ObjectId, DockTurnCode Code) : DockSubAction
{
    /// <summary>The <c>Radians(0)</c> argument.</summary>
    public double RadiansArgument => 0.0;
    /// <summary>The <c>true</c> argument.</summary>
    public bool BoolArgument5 => true;
    /// <summary>The <c>false</c> argument.</summary>
    public bool BoolArgument6 => false;
}

/// <summary>
/// The <c>CompoundActionSequential(robot, {})</c> (0xAC bytes, <c>[compound+0x56] = 1</c>) that <c>SetupTurnAndVerifyAction</c> stores at <c>IDockAction+0x98</c> (M12-037): the
/// verify action first, then the turn action. The async model runs it to completion: the first sub-action that does not succeed ends it with its result.
/// </summary>
// fidelity: M12-037
public sealed class DockCompound
{
    private readonly List<DockSubAction> _actions = new();
    public IReadOnlyList<DockSubAction> Actions => _actions;
    /// <summary><c>[compound+0x56] = 1</c>.</summary>
    public bool Field0x56 => true;
    internal void Add(DockSubAction a) => _actions.Add(a);

    /// <summary><c>IActionRunner::Update</c> on the compound, run until it is done.</summary>
    public async Task<ActionResult> RunAsync(IDockSubActionExecutor executor, List<string> trace, CancellationToken cancel)
    {
        foreach (var a in _actions)
        {
            var r = await executor.RunAsync(a, trace, cancel);
            if (r != ActionResult.Success) return r;
        }
        return ActionResult.Success;
    }
}

/// <summary>Runs the sub-actions of <see cref="DockCompound"/>. Their engine bodies are M8's and M13's and unread, so the default is <see cref="StandInDockSubActions"/>.</summary>
public interface IDockSubActionExecutor
{
    Task<ActionResult> RunAsync(DockSubAction action, List<string> trace, CancellationToken cancel);
}

/// <summary>
/// LABELLED STAND-IN for the two sub-action bodies the inventory does not settle (M12-037: "VisuallyVerifyNoObjectAtPoseAction / TurnTowardsObjectAction bodies are M8/M13"),
/// which differs from the engine:
/// <list type="bullet">
/// <item><c>VisuallyVerifyNoObjectAtPoseAction</c>: no action exists here; it is a COUNTED NO-OP that returns success (<see cref="UnreadVerifyCalls"/>, and a trace line on every call). The
///   engine's action can fail the dock when an object stands in the box above the target; this stack never does.</item>
/// <item><c>TurnTowardsObjectAction</c>: the existing <see cref="ManipulationSystem.TurnTowardsObjectAsync"/> with its maximum turn set to pi (the turn's <c>Radians(0)</c> argument
///   and the two bools are not mapped; the earlier stand-in used pi) followed by the two-second wait for a visible marker of the object that the earlier stand-in also made
///   (<see cref="ManipulationSystem.WaitForVisibleMarkerAsync"/>); not seeing it is 0x0300001D.</item>
/// </list>
/// </summary>
// fidelity: M12-037
public sealed class StandInDockSubActions : IDockSubActionExecutor
{
    private readonly ManipulationSystem _m;
    public StandInDockSubActions(ManipulationSystem m) { _m = m; }
    /// <summary>How many times the no-op <c>VisuallyVerifyNoObjectAtPoseAction</c> stub ran.</summary>
    public int UnreadVerifyCalls { get; private set; }

    public async Task<ActionResult> RunAsync(DockSubAction action, List<string> trace, CancellationToken cancel)
    {
        switch (action)
        {
            case VisuallyVerifyNoObjectAtPose v:
                UnreadVerifyCalls++;
                trace.Add($"SetupTurnAndVerifyAction: VisuallyVerifyNoObjectAtPoseAction body unread (M8): stub, assumed clear (pose {v.Pose.Translation}, half size {v.HalfSize})");
                return ActionResult.Success;
            case TurnTowardsObject t:
                trace.Add($"SetupTurnAndVerifyAction: TurnTowardsObjectAction body unread (M13): existing turn stand-in for object {t.ObjectId}, code {t.Code}");
                await _m.TurnTowardsObjectAsync(t.ObjectId, Math.PI, cancel);
                var seen = await _m.WaitForVisibleMarkerAsync(t.ObjectId, TimeSpan.FromSeconds(2), cancel);
                if (seen is null) { trace.Add("IDockAction.CheckIfDone.VisualVerifyFailed: VisualVerification of object failed, stopping IDockAction."); return ActionResult.VisualObservationFailed; }
                return ActionResult.Success;
            default:
                throw new NotSupportedException($"unknown dock sub-action {action}");
        }
    }
}

/// <summary>
/// <c>RotationMatrix3d::GetRotatedParentAxis&lt;'Z'&gt;</c> 0x005507A0..0x00550852 (M12-030, verified in 20260929-R-VIS-verify-M12-gap3.md Q6): it reads the row
/// <c>v = (R(2,0), R(2,1), R(2,2))</c> and returns +-1 (X), +-2 (Y) or +-3 (Z), the sign being + when the dominant component is strictly &gt; 0 and - otherwise, the dominant
/// axis chosen with strict &gt; comparisons (ties prefer X, then Y, over Z); 0 is never returned. The 'X' and 'Y' instantiations that
/// <c>SetupTurnAndVerifyAction</c> uses (<c>GetDimInParentFrame&lt;'X'/'Y'&gt;</c>, M12-037) are taken to read rows 0 and 1 in the same way (the record verifies only 'Z').
/// </summary>
// fidelity: M12-030, M12-037
public static class RotatedParentAxis
{
    /// <param name="parentAxis">0 for 'X', 1 for 'Y', 2 for 'Z'.</param>
    public static int Get(Mat3 r, int parentAxis)
    {
        double x = r[parentAxis, 0], y = r[parentAxis, 1], z = r[parentAxis, 2];
        double ax = Math.Abs(x), ay = Math.Abs(y), az = Math.Abs(z);
        int axis = 1; double dominant = ax, value = x;
        if (ay > dominant) { axis = 2; dominant = ay; value = y; }
        if (az > dominant) { axis = 3; value = z; }
        return value > 0 ? axis : -axis;
    }

    /// <summary><c>ObservableObject::GetDimInParentFrame&lt;'X'/'Y'/'Z'&gt;</c> 0x00557794 (M12-033): +-3 gives size.z, +-2 size.y, else size.x.</summary>
    public static double DimInParentFrame(ObservableObject o, int parentAxis)
    {
        var size = CubeGeometry.SizeOf(o.Type);
        return Math.Abs(Get(o.Pose.Rotation, parentAxis)) switch { 3 => size.Z, 2 => size.Y, _ => size.X };
    }
}

/// <summary>
/// The engine's <c>IDockAction</c> (0x005502D8..0x00552560) as the base of the dock actions. <see cref="RunAsync"/> is <c>Init</c> in the engine's
/// 14-step order (M12-017; the doc there lists which steps are modelled) followed by <c>CheckIfDone</c>'s dock: the object must be located ("Dock object is
/// null"); with <c>[+0xB9]</c> (<see cref="CheckPreActionPose"/>) <c>GetPreActionPoses</c> (M12-031, <see cref="DockPreActionPoses"/>) decides whether the robot is
/// within the threshold of a pre-action pose (0x04000001 when it is too far); the subclass's <c>SelectDockAction</c> picks the firmware action; <c>InitInternal</c>
/// runs last; the docking squint is added by <c>CheckIfDone</c> (0x00552394), which sends <c>DockWithObject</c> ("Docking with marker %d (%s) using action %s").
/// The subclass's <c>Verify</c> judges the result. The default pre-action angle tolerance is 7.5 degrees from the constructor
/// (<see cref="PreActionAngleToleranceRad"/>). The turn-and-verify compound (<c>SetupTurnAndVerifyAction</c>, M12-037) is built exactly; its two sub-action bodies are M8/M13's
/// and run through <see cref="SubActions"/> (default <see cref="StandInDockSubActions"/>, a labelled stand-in).
/// </summary>
public abstract class DockActionBase
{
    // fidelity: M12-004, M12-017, M12-020
    /// <summary>
    /// The <c>IDockAction</c> constructor's pre-action angle tolerance <c>[this+0x88]</c>: Radians of 7.5 degrees (M12-017, research Q6.19). The
    /// inventory gives the value in degrees, not as a literal; the float used is 0x3E060A92, the literal the same record family uses for the same 7.5 degrees
    /// (<c>DriveToObjectAction+0x14C</c>, 0x0055850C).
    /// </summary>
    public static readonly double PreActionAngleToleranceRad = BitConverter.UInt32BitsToSingle(0x3E060A92);

    protected readonly ManipulationSystem M;
    protected readonly List<string> _trace = new();

    protected DockActionBase(ManipulationSystem m, uint objectId) { M = m; ObjectId = objectId; SubActions = new StandInDockSubActions(m); }

    public uint ObjectId { get; }
    public PathMotionProfile Profile { get; set; } = PathMotionProfile.Default;
    /// <summary>The engine's <c>_shouldCheckPreActionPose</c> (set false by the helpers when they just drove there).</summary>
    public bool CheckPreActionPose { get; set; } = true;
    public DockAction? SelectedDockAction { get; protected set; }
    public DockResult? Result { get; protected set; }
    /// <summary>The bool <c>IDockAction::CheckIfDone</c> stores from <c>AddSquint</c> at <c>IDockAction+0xF4</c> (M12-017).</summary>
    public bool DockSquintAdded { get; private set; }
    public IReadOnlyList<string> Trace => _trace;

    /// <summary>
    /// <c>[+0xF7]</c> (M12-037): selects <c>Marker::ANY_CODE</c> instead of <c>[+0x82]</c> as the turn action's code. The wire factories store 1 for Align, FacePlant, Pickup, PopAWheelie,
    /// RollObject and (through a register) PlaceRelObject (0x00529CF6..0x0052AAC6). The <c>IDockAction</c> constructor's own default is NOT in the inventory (MISSING); this stack's
    /// actions are built by behaviours, not the wire factories, so it is false unless a caller sets it.
    /// </summary>
    // fidelity: M12-037
    public bool Field0xF7 { get; set; }
    /// <summary><c>[+0x82]</c> (M12-017 steps 7 and 8): 3 after step 7, then the chosen marker's code at the join 0x00551844.</summary>
    public short Field0x82 { get; private set; }
    /// <summary><c>[+0x84]</c>: 3 after step 7 and again at the join when <c>vtbl+0x34</c> returns null (the per-subclass results of that virtual are unread, MISSING; every C# subclass takes the null case).</summary>
    public short Field0x84 { get; private set; }
    /// <summary>The compound <c>SetupTurnAndVerifyAction</c> stored at <c>IDockAction+0x98</c> on the last <see cref="RunAsync"/>; null before <c>Init</c> reaches step 9.</summary>
    public DockCompound? TurnAndVerify { get; private set; }
    /// <summary>Runs the compound's sub-actions (M12-037); the default is the labelled stand-in <see cref="StandInDockSubActions"/>.</summary>
    public IDockSubActionExecutor SubActions { get; set; }

    /// <summary><c>[+0xC0]</c>: when non-zero <c>SetupTurnAndVerifyAction</c> adds the verify-no-object action; the <c>IDockAction</c> default is 1 (M12-037). <see cref="PlaceRelObjectAction"/> stores 0 (M12-025).</summary>
    protected virtual bool VerifyNoObjectAtPoseEnabled => true;
    /// <summary><c>[+0xC8]</c>: when non-zero <c>SetupTurnAndVerifyAction</c> adds the turn action; the default is 1 (M12-037).</summary>
    protected virtual bool TurnTowardsObjectEnabled => true;

    protected abstract PreActionType PreActionType { get; }
    protected abstract DockAction? SelectDockAction(ObservableObject target);
    /// <summary>
    /// <c>IDockAction::Init</c> step 5 (vptr+0x38, 0x00551654): the subclass's <c>SelectDockAction</c> returns an ActionResult and a non-zero one is returned
    /// by <c>Init</c> after the warning "IDockAction.Init.DockActionSelectionFailure" (research Q6.5). The default adapts the older nullable form: null is
    /// <c>Abort</c> 0x03000000, which is the C# dock actions' existing result for it (the inventory gives no engine result for them; only
    /// <see cref="PlaceRelObjectAction"/>'s, M12-017, is itemised).
    /// </summary>
    protected virtual ActionResult SelectDockActionResult(ObservableObject target, out DockAction action)
    {
        var selected = SelectDockAction(target);
        action = selected.GetValueOrDefault();
        return selected is null ? ActionResult.Abort : ActionResult.Success;
    }
    protected abstract ActionResult Verify(ObservableObject? target, DockResult result);
    /// <summary>
    /// The virtual <c>InitInternal</c> (vptr+0x30) that <c>IDockAction::Init</c> calls as its LAST setup step (0x00551C0C..0x00551C14: after the pre-action
    /// check, <c>SelectDockAction</c>, the handler registration, <c>SetupTurnAndVerifyAction</c>, the reaction lock, the cube light and <c>RemoveSquint</c>),
    /// returning early on a non-zero result (M12-017 step 13; <c>PlaceRelObjectAction</c>'s body is M12-025).
    /// </summary>
    protected virtual ActionResult InitInternal() => ActionResult.Success;
    protected virtual (double X, double Y, double Angle) PlacementOffset => (0, 0, 0);
    /// <summary>
    /// <c>DockWithObject</c> byte 7, held at <c>IDockAction</c> +0xBB (M12-005): the constructor leaves 0 (the 0x100 word at 0x005503AA) and the complete list of writers
    /// is <c>AlignWithObjectAction</c> 0x005533F6 (2, one alignment type), <c>PickupObjectAction</c> 0x005536EA (2), <c>PlaceRelObjectAction::InitInternal</c> 0x00554E12
    /// (3 iff |B'| &gt;= 1e-5), <c>PlaceRelObjectAction::SelectDockAction</c> 0x005554D0 (0), <c>RollObjectAction</c> 0x005565D6 (0; the 5 stored at 0x005565DC is the
    /// DockAction byte) and <c>DriveToPickupObjectAction::SetDockingMethod</c> 0x0055C5A2 (its argument; that class is not built here). Each subclass overrides this.
    /// </summary>
    // fidelity: M12-005
    protected virtual DockingMethod DockingMethod => DockingMethod.Default;

    /// <summary>
    /// <c>DockWithObject</c> field 5, the <c>IDockAction</c> constructor's last bool held at <c>IDockAction+0x95</c> (0x00550382, read at 0x00552278; M12-005).
    /// The C# dock actions other than <see cref="PlaceRelObjectAction"/> have no constructor argument for it and keep the value they always sent (false).
    /// </summary>
    protected virtual bool DockFlag95 => false;

    /// <summary>
    /// <c>DockWithObject</c> field 8, held at <c>IDockAction</c> +0xC1. The constructor leaves 0 and
    /// <c>PickupObjectAction</c> 0x005536F8 writes 1. Its CLAD name is not established.
    /// </summary>
    protected virtual bool DockFlag8 => false;

    /// <summary>
    /// True for the LEGACY <see cref="PlaceRelObjectAction"/> (the constructor <c>(m, id, onTop)</c>, M12-028): its callers' behaviour that the inventory does not settle is kept as it was
    /// before this batch: no <c>SetupTurnAndVerifyAction</c> compound and no observed-marker choice; after <c>InitInternal</c> the turn towards the object (existing stand-in), the marker facing
    /// the robot (<see cref="MarkerFacing"/>) and <see cref="VerifyNoObjectAtPlacementPose"/> as the earlier code did, with NO visible-marker wait.
    /// </summary>
    protected virtual bool UsesLegacyTurnAndVerify => false;

    /// <summary>The target's side marker whose outward normal points most towards the robot (the pre-batch stand-in, kept for the legacy place callers).</summary>
    protected static KnownMarker? MarkerFacing(ObservableObject target, Pose3d robot)
    {
        KnownMarker? best = null; double bestDot = 0.2;
        foreach (var m in target.Markers)
        {
            var n = (target.Pose.Rotation * m.NormalOnObject).Normalized();
            if (Math.Abs(n.Z) > 0.5) continue;
            var toRobot = (robot.Translation - target.Pose.Apply(m.PoseOnObject.Translation)) with { Z = 0 };
            double dot = n.Dot(toRobot.Normalized());
            if (dot > bestDot) { bestDot = dot; best = m; }
        }
        return best;
    }

    /// <summary>The pre-batch stand-in for <c>VisuallyVerifyNoObjectAtPoseAction</c> against the world model (kept for the legacy place callers): no other located object within half a cube of where the carried object goes.</summary>
    protected bool VerifyNoObjectAtPlacementPose(ObservableObject target, Pose3d robot)
    {
        var (ox, oy, _) = PlacementOffset;
        var toTarget = (target.Pose.Translation - robot.Translation) with { Z = 0 };
        var ahead = toTarget.Normalized(); var side = new Vec3(-ahead.Y, ahead.X, 0);
        var where = target.Pose.Translation + ahead * ox + side * oy;
        uint? carried = M.Docking.Carrying.CarriedObjectId;
        foreach (var o in M.World.LocatedObjects)
        {
            if (o.ObjectId == target.ObjectId || o.ObjectId == carried) continue;
            var d = (o.Pose.Translation - where) with { Z = 0 };
            if (d.Length < CubeGeometry.CubeSizeMm / 2) return false;
        }
        return true;
    }

    /// <summary>
    /// <c>ObservableObject::GetObservedMarkers(vec&amp;, sinceTime)</c> 0x00876C50 (M12-037): <c>sinceTime == 0</c> gives an empty vector; otherwise every marker of the object's
    /// list (in list order) whose last-observed time (<c>marker+0x48</c>) is &gt;= <paramref name="sinceTime"/>. This stack keeps no per-marker time (a CHOICE): the markers of the object's
    /// last observation (<see cref="ObservableObject.LastObservedMarkers"/>) carry the object's <see cref="ObservableObject.LastObservedTimestamp"/> and every other marker 0, so
    /// <c>Init</c>'s call with <c>sinceTime = [obj+0x1C]</c> (the object's own last-observed time) returns exactly the last observation's markers.
    /// </summary>
    // fidelity: M12-037
    public static IReadOnlyList<KnownMarker> GetObservedMarkers(ObservableObject obj, uint sinceTime)
    {
        if (sinceTime == 0) return Array.Empty<KnownMarker>();
        if (obj.LastObservedTimestamp < sinceTime) return Array.Empty<KnownMarker>();
        return obj.Markers.Where(m => obj.LastObservedMarkers.Contains(m.Code)).ToList();
    }

    /// <summary>
    /// The marker choice of <c>IDockAction::Init</c> when <c>[+0xB9]</c> is 0 (0x00551960; M12-017): <see cref="GetObservedMarkers"/> with <c>[obj+0x1C]</c>; none is an error log and
    /// 0x0300001D (0x00551A30..0x00551A82), exactly one is that marker, several are compared by the 3-D SQUARED distance of the marker's pose to <c>Robot::GetPose()</c> with a strict
    /// <c>&lt;</c> from FLT_MAX (0x00551B3E..0x00551B4A; a failing <c>GetWithRespectTo</c> would log and skip, which cannot fail here); when every distance is NaN nothing is chosen (null).
    /// </summary>
    // fidelity: M12-017, M12-037
    internal static ActionResult ChooseObservedMarker(ObservableObject target, Pose3d robot, out KnownMarker? marker, List<string> trace)
    {
        marker = null;
        var observed = GetObservedMarkers(target, target.LastObservedTimestamp);
        if (observed.Count == 0)
        {
            trace.Add("IDockAction.Init: no observed markers on the dock object (error); 0x0300001D");
            return ActionResult.VisualObservationFailed;
        }
        if (observed.Count == 1) { marker = observed[0]; return ActionResult.Success; }
        float best = float.MaxValue;
        foreach (var m in observed)
        {
            var t = target.Pose.Compose(m.PoseOnObject).WithRespectTo(robot).Translation;
            float d2 = (float)(t.X * t.X + t.Y * t.Y + t.Z * t.Z);
            if (d2 < best) { best = d2; marker = m; }
        }
        return ActionResult.Success;
    }

    /// <summary>
    /// <c>IDockAction::SetupTurnAndVerifyAction</c> 0x00551F9C (M12-037): the compound at <c>[+0x98]</c> is replaced by a new <c>CompoundActionSequential</c>; when <c>[+0xC0]</c> is set the verify
    /// action is added: the target's pose in its root frame with the translation <c>(x, y, z + dz)</c>, half extents <c>0.5 * (dx, dy, dz)</c> with
    /// <c>dx, dy, dz = GetDimInParentFrame&lt;X/Y/Z&gt;</c>, and the target's id ignored; then, when <c>[+0xC8]</c> is set, the turn action for <c>ObjectID [+0x7C]</c> with the code
    /// <c>[+0xF7] ? ANY_CODE : [+0x82]</c>, after it.
    /// </summary>
    // fidelity: M12-037
    protected void SetupTurnAndVerifyAction(ObservableObject target)
    {
        var compound = new DockCompound();
        if (VerifyNoObjectAtPoseEnabled)
        {
            var pose = target.Pose;
            double dx = RotatedParentAxis.DimInParentFrame(target, 0), dy = RotatedParentAxis.DimInParentFrame(target, 1), dz = RotatedParentAxis.DimInParentFrame(target, 2);
            var raised = new Pose3d(pose.Rotation, new Vec3(pose.Translation.X, pose.Translation.Y, pose.Translation.Z + dz));
            compound.Add(new VisuallyVerifyNoObjectAtPose(raised, new Vec3(0.5 * dx, 0.5 * dy, 0.5 * dz), new[] { target.ObjectId }));
        }
        if (TurnTowardsObjectEnabled)
            compound.Add(new TurnTowardsObject(ObjectId, Field0xF7 ? DockTurnCode.Any : new DockTurnCode(Field0x82)));
        TurnAndVerify = compound;
    }

    /// <summary>
    /// <c>IDockAction::Init</c> 0x005514FC..0x00551C8A in the engine's order (M12-017; research Q6.1-Q6.15, verified in 20260929-R-VIS-verify-M12-gap2.md and
    /// 20260929-R-VIS-verify-M12-gap3.md Q10), then <c>IDockAction::CheckIfDone</c> 0x005521AC's dock:
    /// <list type="number">
    /// <item>(1) remove the reaction locks - NOT MODELLED (M8);</item>
    /// <item>(2) look the object up: missing is 0x03000004 ("IDockAction.NullDockObject");</item>
    /// <item>(3) <c>IsValidLightCube</c> - NOT MODELLED (feeds the cube light, M10);</item>
    /// <item>(4) only if <c>[+0xB9]</c> (<see cref="CheckPreActionPose"/>): <c>GetPreActionPoses</c> with flag A set (M12-031), a non-zero result returned;</item>
    /// <item>(5) <c>SelectDockAction</c>, a non-zero result returned;</item>
    /// <item>(6) subscribe to tags 0xC5 and 0xDA and the external interface - the bodies are unread (M12-034); DockingSystem receives the messages;</item>
    /// <item>(7) <c>[+0x82]</c>/<c>[+0x84]</c> := 3 (<see cref="Field0x82"/>, <see cref="Field0x84"/>);</item>
    /// <item>(8) the marker: with <c>[+0xB9]</c> the closest pose's marker, without it <see cref="ChooseObservedMarker"/>; both join at 0x00551844: a null marker is 0x03000002
    ///   (NullDockMarker), otherwise <c>[+0x82]</c> is the marker's code and <c>[+0x84]</c> is 3;</item>
    /// <item>(9) <see cref="SetupTurnAndVerifyAction"/> builds the compound (M12-037);</item>
    /// <item>(10) <c>DisableReactionsWithLock</c>, (11) the cube light, (12) <c>RemoveSquint(250)</c> when <c>[+0xF4]</c> - NOT MODELLED;</item>
    /// <item>(13) <c>InitInternal</c>, a non-zero result returned;</item>
    /// <item>(14) the compound's <c>Update</c> (0x00551C16..0x00551C1A): here it runs to completion; only {0, RUNNING} continue (to <c>DisableReactionsWithLock('dockActions')</c>, not modelled), any other result is returned.</item>
    /// </list>
    /// </summary>
    // fidelity: M12-017, M12-031, M12-037
    public async Task<ActionResult> RunAsync(CancellationToken cancel)
    {
        _trace.Add("IDockAction.Init: not modelled: reaction locks (1),(10) M8; IsValidLightCube and the cube light (3),(11) M10; handler bodies (6) and RemoveSquint (12) M12-034; slot vtbl+0x34 of every subclass (MISSING)");
        // (2)
        var target = M.World.GetLocatedObjectById(ObjectId);
        if (target is null) { _trace.Add("IDockAction.NullDockObject: Dock object is null"); return ActionResult.BadObject; }
        var robot = M.RobotPose();
        if (robot is null) return ActionResult.Abort;
        // (4) GetPreActionPoses(flag A = [+0xB9], tolerance [+0x88], distanceFromMarker [+0xBC] = 0, no approach angle). The default of [+0xBC] is not itemised
        // (the drive-then-dock compound sets it from its own distance, research Q7.7); 0 is the value every path this stack builds passes (M12-001 E7).
        PreActionPoseOutput? pre = null;
        if (CheckPreActionPose)
        {
            // fidelity: M12-020
            pre = DockPreActionPoses.Get(new PreActionPoseInput(target, PreActionType, FlagA: true, PreActionAngleToleranceRad, 0.0, UseApproachAngle: false, 0.0),
                                         robot.Value, M.Docking.Carrying.CarriedObjectId, () => M.GetObstacles(robot.Value), _trace);
            if (pre.Result != ActionResult.Success) return pre.Result;
        }
        // (5)
        var selection = SelectDockActionResult(target, out var action);
        if (selection != ActionResult.Success) { _trace.Add("IDockAction.Init.DockActionSelectionFailure"); return selection; }
        SelectedDockAction = action;
        // (7)
        Field0x82 = 3; Field0x84 = 3;
        KnownMarker? marker = null;
        if (!UsesLegacyTurnAndVerify)
        {
            // (8), both [+0xB9] branches
            if (pre is not null)
            {
                marker = pre.Poses[pre.ClosestIndex].Marker;
                _trace.Add($"Robot is within ({pre.OffsetX:F1},{pre.OffsetY:F1}) of the nearest pre-action pose, proceeding with docking.");
                _trace.Add("IDockAction.Init.BeginDockingFromPreActionPose");
            }
            else
            {
                var chosen = ChooseObservedMarker(target, robot.Value, out marker, _trace);
                if (chosen != ActionResult.Success) return chosen;
            }
            // the join at 0x00551844
            if (marker is null) { _trace.Add("IDockAction.Init.NullDockMarker"); return ActionResult.BadMarker; }
            Field0x82 = (short)marker.Code;
            Field0x84 = 3;
            // (9)
            SetupTurnAndVerifyAction(target);
        }
        else if (pre is not null) _trace.Add("IDockAction.Init.BeginDockingFromPreActionPose");
        // (13)
        var init = InitInternal();
        if (init != ActionResult.Success) { _trace.Add($"InitInternal -> {init}"); return init; }
        if (UsesLegacyTurnAndVerify)
        {
            // the legacy place callers keep the pre-batch stand-in (see UsesLegacyTurnAndVerify): turn (result ignored as before), the marker facing the robot, and the placement-clear check, no wait
            _trace.Add("SetupTurnAndVerifyAction: legacy PlaceRelObjectAction keeps the pre-batch stand-in (M12-028)");
            await M.TurnTowardsObjectAsync(ObjectId, Math.PI, cancel);
            var robotNow = M.RobotPose() ?? robot.Value;
            marker = MarkerFacing(target, robotNow);
            if (marker is null) { _trace.Add("IDockAction.CheckIfDone.NoMarkerFacingRobot"); return ActionResult.VisualObservationFailed; }
            if (!VerifyNoObjectAtPlacementPose(target, robotNow)) { _trace.Add("IDockAction.CheckIfDone.VisualVerifyFailed: an object is located at the placement pose"); return ActionResult.VisualObservationFailed; }
            _trace.Add("VisuallyVerifyNoObjectAtPose: the placement pose is clear");
        }
        else
        {
            // (14)
            var compound = await TurnAndVerify!.RunAsync(SubActions, _trace, cancel);
            if (compound != ActionResult.Success) return compound;
        }

        // IDockAction::CheckIfDone 0x005521AC: the docking squint (0x00552394; AddDockSquint below installs it), then DockWithObject.
        DockSquintAdded = AddDockSquint();
        _trace.Add($"IDockAction.DockWithObjectHelper.BeginDocking: Docking with marker {marker.Code} using action {action}.");
        var (ox, oy, oa) = PlacementOffset;
        var result = await M.Docking.DockAsync(target, marker, action, Profile, ox, oy, oa,
                                               unlockLiftTrack: DockFlag95, method: DockingMethod, flag8: DockFlag8, cancel: cancel);
        if (result is null) return cancel.IsCancellationRequested ? ActionResult.CancelledWhileRunning : ActionResult.Timeout;
        Result = result;
        _trace.Add($"dock result {result}");
        var verified = Verify(M.World.GetObjectById(ObjectId), result);
        _trace.Add($"Verify -> {verified}");
        return verified;
    }

    /// <summary>
    /// <c>IDockAction::CheckIfDone</c>'s docking squint (0x00552394, M12-017 C-E5):
    /// <c>AddSquint(robot+0xC0, "DockSquint", 1.05, 0.35, -10.0)</c> adds a persistent face layer named
    /// "DockSquint". <c>FaceLayerManager::GenerateSquint</c> 0x0058D738 ignores its float arguments and
    /// clips <c>EyeScaleY = 0.35</c>, <c>EyeScaleX = 1.05</c> and <c>UpperLidAngle = -10.0</c> on both eyes,
    /// with a reset keyframe at 250 ms. The engine stores the returned bool at <c>IDockAction+0xF4</c>.
    /// </summary>
    // fidelity: M12-017
    private bool AddDockSquint()
    {
        var face = new ProceduralFacePose();
        foreach (var eye in new[] { face.Left, face.Right })
        {
            eye[EyeParam.EyeScaleY] = 0.35f;
            eye[EyeParam.EyeScaleX] = 1.05f;
            eye[EyeParam.UpperLidAngle] = -10.0f;
        }
        var track = new StreamTrack<FaceFrame>();
        track.AddKeyFrameToBack(new FaceFrame(0, face));
        track.AddKeyFrameToBack(new FaceFrame(250, new ProceduralFacePose()));
        return M.Robot.Animations.Scheduler.Layers.Face.AddPersistentLayer("DockSquint", track) != 0;
    }
}

/// <summary>
/// <c>PickupObjectAction</c> (0x00553648): <c>SelectDockAction</c> compares the object's height with 33.85
/// (0x42076666): a block resting on the ground (its centre at 22 mm) is a <c>PickupLow</c>, one on top of
/// another (66 mm) a <c>PickupHigh</c>; already carrying → "Already carrying object. Can't pickup object."
/// <c>Verify</c> (0x00553BE0): the robot must think it is carrying the object ("Expecting robot to think
/// it's carrying an object at this point"), and seeing the object still in its original pose means the
/// pick-up failed ("Object pick-up FAILED! (Still seeing object in same place.)"); otherwise "Object
/// pick-up SUCCEEDED!".
///
/// Two timed checks sit alongside those, both of which end with
/// <c>CarryingComponent::SetCarriedObjectAsUnattached(true)</c>. <c>Verify</c> stamps the time of its
/// first call at +0x10C and then:
///
/// <list type="bullet">
/// <item>if the object still reports itself moving, and more than +0x118 = <b>500 ms</b> have passed
///   since that stamp, the pick-up failed: "PickupObjectAction.Verify.ObjectStillMoving" (0x00553C98);</item>
/// <item>otherwise the object must have been seen recently enough - the stamp must not be later than the
///   object's last-observed time plus a timeout picked by the dock action at +0x80: +0x11C =
///   <b>500 ms</b> for a low dock, +0x120 = <b>2000 ms</b> for a high one (0x00553D0A).</item>
/// </list>
///
/// The constructor sets all four numbers at 0x005536CC onwards.
/// </summary>
public sealed class PickupObjectAction : DockActionBase
{
    // fidelity: M12-007
    public const double HighDockHeightMm = 33.85;

    /// <summary>+0x118: how long the object may still be moving after the verify starts.</summary>
    public const uint StillMovingAllowanceMs = 500;
    /// <summary>+0x11C: how stale the last sighting may be for a low dock.</summary>
    public const uint LowDockObservationTimeoutMs = 500;
    /// <summary>+0x120: and for a high one.</summary>
    public const uint HighDockObservationTimeoutMs = 2000;

    private Pose3d _originalPose;
    private uint _verifyStartedAt;

    public PickupObjectAction(ManipulationSystem m, uint objectId) : base(m, objectId) { }
    protected override PreActionType PreActionType => PreActionType.Docking;
    protected override DockingMethod DockingMethod => DockingMethod.Method2;   // 0x005536EA
    protected override bool DockFlag8 => true;                                 // 0x005536F8

    protected override DockAction? SelectDockAction(ObservableObject target)
    {
        if (M.Docking.Carrying.IsCarryingObject) { _trace.Add("PickupObjectAction.SelectDockAction.CarryingObject: Already carrying object. Can't pickup object. Aborting."); return null; }
        _originalPose = target.Pose;
        return target.Pose.Translation.Z > HighDockHeightMm ? DockAction.PickupHigh : DockAction.PickupLow;
    }

    protected override ActionResult Verify(ObservableObject? target, DockResult result)
    {
        if (!result.Succeeded) return ActionResult.Retry;
        if (!M.Docking.Carrying.IsCarrying(ObjectId)) { _trace.Add("PickupObjectAction.Verify.ExpectedCarryingObject"); return ActionResult.Retry; }

        // 0x00553BEE: the first Verify stamps the time and every later one measures against that stamp.
        uint now = M.Robot.State.Latest?.Timestamp ?? result.Timestamp;
        if (_verifyStartedAt == 0) _verifyStartedAt = now;

        if (target is { IsLocated: true })
        {
            if (target.IsMoving)
            {
                // 0x00553C98: still moving past the allowance, so nothing was picked up.
                if (now > _verifyStartedAt + StillMovingAllowanceMs)
                {
                    _trace.Add("PickupObjectAction.Verify.ObjectStillMoving");
                    M.Docking.ReleaseCarriedObject(forget: true);
                    return ActionResult.Retry;
                }
            }
            else
            {
                // 0x00553D0A: the sighting the verify rests on has to be recent enough, and how recent
                // depends on which dock this was.
                uint timeout = SelectedDockAction == DockAction.PickupLow
                    ? LowDockObservationTimeoutMs : HighDockObservationTimeoutMs;
                if (_verifyStartedAt > target.LastObservedTimestamp + timeout)
                {
                    _trace.Add($"PickupObjectAction.Verify.ObjectNotSeenRecentlyEnough: last seen " +
                               $"{target.LastObservedTimestamp}, verify began {_verifyStartedAt}, allowed {timeout} ms");
                    M.Docking.ReleaseCarriedObject(forget: true);
                    return ActionResult.Retry;
                }
            }

            if (target.LastObservedTimestamp > result.Timestamp && target.Pose.IsSameAs(_originalPose, 20, 0.35))
            {
                _trace.Add("PickupObjectAction.Verify.SeeingCarriedObjectInOrigPose: Object pick-up FAILED! (Still seeing object in same place.)");
                M.Docking.ReleaseCarriedObject(forget: true);
                return ActionResult.Retry;
            }
        }
        _trace.Add("PickupObjectAction.Verify.Success: Object pick-up SUCCEEDED!");
        return ActionResult.Success;
    }
}

/// <summary>
/// <c>PlaceRelObjectAction</c> (0x00554BE0): place the carried object relative to a target object (on top of it
/// for a stack: <c>PlaceHigh</c>; beside it: <c>PlaceLow</c>). "Not carrying object" aborts. Success
/// when the robot reports <c>BlockPlaced</c> and the carried object is no longer carried.
///
/// M12-025. The constructor stores <c>A</c> (+0x108) and <c>B</c> (+0x10C), the third argument at +0xA8 (with +0xF0 = 0x208, or 0x209
/// when it is set), the ctor bool at +0x110 (a set bool skips the offset transform), and <c>+0xFC = -1</c>, <c>+0x100 = 0</c>,
/// <c>+0xC0 = 0</c>; the <c>IDockAction</c> type is 0x15. When either <c>|A|</c> or <c>|B|</c> is at least 9.99999975e-06 (0x3727C5AC) it clears
/// <c>+0xB9</c> (here <see cref="DockActionBase.CheckPreActionPose"/>) and logs
/// "PlaceRelObjectAction.Constructor.WillNotCheckPreDockPoses" (0x00554C86..0x00554CDE; the verifier corrected the extractor's inverted
/// condition). <c>InitInternal</c> 0x00554DCC, <c>TransformPlacementOffsetsRelativeObject</c> 0x00554E40 and the wire routes follow.
/// </summary>
public sealed class PlaceRelObjectAction : DockActionBase
{
    /// <summary>9.99999975e-06, the float 0x3727C5AC (0x00554D80).</summary>
    public static readonly double TinyOffset = BitConverter.UInt32BitsToSingle(0x3727C5AC);
    /// <summary>-16.000009, the float 0xC1800005 (0x00555110).</summary>
    public static readonly double MinAdjustedA = BitConverter.UInt32BitsToSingle(0xC1800005);
    /// <summary>The transform's alignment tolerance, 0x3E860A92 = 0.2617994 (15 degrees; 0x005550F0).</summary>
    public static readonly double AlignmentToleranceRad = BitConverter.UInt32BitsToSingle(0x3E860A92);
    /// <summary>The "z-rotated point above the object centre" height argument the transform passes: 0.5 (0x00554E8C).</summary>
    public const double TransformPointAboveCentre = 0.5;

    private double _angle;
    private (double X, double Y)? _initialised;

    /// <summary>
    /// The engine-shaped constructor: <c>PlaceRelObjectAction(robot, id, bool +0xA8, float A, float B, bool IDockAction ctor bool, bool +0x110)</c>.
    /// </summary>
    // fidelity: M12-025
    public PlaceRelObjectAction(ManipulationSystem m, uint targetObjectId, bool flagAt0xA8, double a, double b,
                                bool dockActionFlagAt0x95, bool skipOffsetTransform) : base(m, targetObjectId)
    {
        OffsetA = a; OffsetB = b;
        FlagAt0xA8 = flagAt0xA8; Field0xF0 = flagAt0xA8 ? 0x209 : 0x208;
        DockActionFlagAt0x95 = dockActionFlagAt0x95;
        SkipOffsetTransform = skipOffsetTransform;
        ApplyOffsetRuleOfTheConstructor();
    }

    /// <summary>
    /// The LEGACY constructor, for the callers whose engine arguments are not in the inventory (M12-028: the producers of the pyramid and stack behaviours'
    /// <c>PlaceRelObjectParameters</c> are unread). What is settled: <paramref name="onTop"/> false stands for the engine's +0xA8 set (DockAction 3, place low) and true for
    /// +0xA8 clear, in which case <c>SelectDockAction</c> 0x00555434 (M12-017) picks DockAction 2 only when <c>CanStackOnTopOfObject</c> holds and is otherwise 0x03000004
    /// (the callers' result changes from the earlier code, which never asked). A=B=0 as the engine's constructor is given them, so +0xB9 is left set (the pre-action check stays on),
    /// +0xBB stays 0 (<see cref="DockingMethod.Default"/>), the transform is skipped (+0x110 = 1, as on the wire path) and +0x95 is false.
    /// <para>CHOICE, visible here and in M12-025 / M12-028: <see cref="Offsets"/> set on a legacy action are NOT the engine's A and B. They are kept apart
    /// (they never reach A, B, +0xB9, +0xBB or the transform, because no record maps a behaviour's Y offset to B with the transform skipped) and are sent unchanged as the
    /// docking error signal's placement offsets (x, y, angle), which is what these callers always did.</para>
    /// </summary>
    public PlaceRelObjectAction(ManipulationSystem m, uint targetObjectId, bool onTop = true)
        : this(m, targetObjectId, flagAt0xA8: !onTop, a: 0, b: 0, dockActionFlagAt0x95: false, skipOffsetTransform: true) { _legacy = true; }

    private readonly bool _legacy;
    private (double X, double Y, double Angle)? _legacyOffsets;

    /// <summary>True when <c>+0xA8</c> is clear: the engine tries to stack on top (M12-017 SelectDockAction).</summary>
    public bool OnTop => !FlagAt0xA8;
    /// <summary><c>+0x108</c>: A, replaced by A' when the offset transform runs.</summary>
    public double OffsetA { get; private set; }
    /// <summary><c>+0x10C</c>: B, replaced by B' when the offset transform runs.</summary>
    public double OffsetB { get; private set; }
    /// <summary><c>+0xA8</c>, the third constructor argument.</summary>
    public bool FlagAt0xA8 { get; }
    /// <summary><c>+0xF0</c>: 0x208, or 0x209 when <see cref="FlagAt0xA8"/> is set.</summary>
    public int Field0xF0 { get; }
    /// <summary>The <c>IDockAction</c> constructor's bool, held at <c>IDockAction+0x95</c>; it is <c>DockWithObject</c> byte 5 (M12-005).</summary>
    public bool DockActionFlagAt0x95 { get; }
    /// <summary><c>+0x110</c>: when set, <c>InitInternal</c> skips <c>TransformPlacementOffsetsRelativeObject</c>.</summary>
    public bool SkipOffsetTransform { get; }
    /// <summary><c>+0xFC</c>: -1 from the constructor.</summary>
    public int Field0xFC { get; } = -1;
    /// <summary><c>+0x100</c>: 0 from the constructor.</summary>
    public int Field0x100 { get; }
    /// <summary><c>+0xC0</c>: 0 from the constructor.</summary>
    public int Field0xC0 { get; }
    // +0xF7 (set to 1 by the non-pre-dock wire route, 0x0052A750) is DockActionBase.Field0xF7.
    /// <summary>
    /// <c>+0xBB</c>, which is <c>DockWithObject</c> byte 7 (M12-005): 0 from the constructor; <c>SelectDockAction</c> stores 0 on its stack-on-top branch
    /// (0x005554D0); <c>InitInternal</c> then stores 3 iff <c>|B'| &gt;= 9.99999975e-06</c> (0x00554E12), so the final value is 3 or 0 and the
    /// order is fixed by <c>Init</c> (M12-017): <c>SelectDockAction</c> (step 5) before <c>InitInternal</c> (step 13).
    /// </summary>
    public int Field0xBB { get; private set; }
    /// <summary>The <c>ObservableObject</c> queries the transform needs (M12-033); the default is the engine's.</summary>
    public ObjectPoseQueries Queries { get; set; } = ObjectPoseQueries.Engine;

    /// <summary><c>DockWithObject</c> byte 7 is <c>[this+0xBB]</c>.</summary>
    protected override DockingMethod DockingMethod => (DockingMethod)Field0xBB;
    /// <summary><c>DockWithObject</c> byte 5 is <c>[this+0x95]</c>.</summary>
    protected override bool DockFlag95 => DockActionFlagAt0x95;

    /// <summary>
    /// The placement offsets relative to the target (x along the approach, y sideways, angle), the engine's <c>placementOffsetX/Y_mm</c>: (A, B, angle) before <c>InitInternal</c>; on an
    /// engine-shaped action setting them stands for passing A and B to the constructor. On a LEGACY action (<see cref="PlaceRelObjectAction(ManipulationSystem, uint, bool)"/>) they are
    /// the raw error-signal offsets and touch nothing else (see that constructor).
    /// </summary>
    public (double X, double Y, double Angle) Offsets
    {
        get => _legacy ? _legacyOffsets ?? (0, 0, 0) : (OffsetA, OffsetB, _angle);
        set
        {
            if (_legacy) { _legacyOffsets = value; return; }
            OffsetA = value.X; OffsetB = value.Y; _angle = value.Angle; ApplyOffsetRuleOfTheConstructor();
        }
    }

    // fidelity: M12-025
    private void ApplyOffsetRuleOfTheConstructor()
    {
        if (Math.Abs(OffsetA) >= TinyOffset || Math.Abs(OffsetB) >= TinyOffset)
        {
            CheckPreActionPose = false;                    // +0xB9 = 0 (0x00554CC8..0x00554CDE)
            _trace.Add("PlaceRelObjectAction.Constructor.WillNotCheckPreDockPoses");
        }
    }

    /// <summary>
    /// The wire <c>PlaceRelObject</c> message (M12-025). <c>usePreDockPose</c> false builds
    /// <c>PlaceRelObjectAction(robot, id, true, A = [msg+0x30], B = 0.0, [msg+0x3A], true)</c> then stores 1 at +0xF7 and 0 at +0xB9
    /// (0x0052A72E..0x0052A754). <c>usePreDockPose</c> true builds <c>DriveToPlaceRelObjectAction(robot, id, true, A, 0.0, useApproachAngle,
    /// angle, useManualSpeed, Radians(0), false, true)</c> (0x0052A6C2..0x0052A700), a drive-then-dock compound this stack does not have.
    /// NO PRODUCTION CALLER: nothing in this stack decodes the app's <c>PlaceRelObject</c> message (the C# behaviours build the action directly), so this factory is
    /// reached only from tests.
    /// </summary>
    // fidelity: M12-025
    public static PlaceRelObjectAction FromWireMessage(ManipulationSystem m, uint objectId, bool usePreDockPose,
                                                       double placementOffsetXmm, bool useManualSpeed)
    {
        if (usePreDockPose)
            throw new NotSupportedException("MISSING: DriveToPlaceRelObjectAction (0x0055C7D0) composes IDriveToInteractWithObject (ActionType 1) with the PlaceRelObjectAction built at 0x0055C862; the inner constructor's other arguments and the compound's behaviour are not itemised in the inventory.");
        var act = new PlaceRelObjectAction(m, objectId, flagAt0xA8: true, a: placementOffsetXmm, b: 0.0,
                                           dockActionFlagAt0x95: useManualSpeed, skipOffsetTransform: true);
        act.Field0xF7 = true;
        act.CheckPreActionPose = false;                    // 0x0052A754 strb 0,[obj+0xB9]
        return act;
    }

    /// <summary>
    /// <c>InitInternal</c> 0x00554DCC: with <c>+0x110 == 0</c> it calls <see cref="TransformPlacementOffsetsRelativeObject"/> (0x00554DDC); then
    /// <c>+0x9C = A'</c> (0 if A' &lt; -16.000009), <c>+0xA0 = B'</c>, and <c>+0xBB = 3</c> when <c>|B'| &gt;= 9.99999975e-06</c>: the test reads the
    /// POST-transform <c>+0x10C</c> (0x00554DE0) and is <c>movpl</c> (0x00554E10/0x00554E12), so a NaN B' also sets it. The result is the transform's (0 when
    /// skipped); the stores are not conditional on it. <c>IDockAction::Init</c> calls this as its last setup step (M12-017 step 13).
    /// </summary>
    // fidelity: M12-025
    protected override ActionResult InitInternal()
    {
        var result = ActionResult.Success;
        if (!SkipOffsetTransform) result = TransformPlacementOffsetsRelativeObject();
        _initialised = (OffsetA < MinAdjustedA ? 0.0 : OffsetA, OffsetB);
        if (!(Math.Abs(OffsetB) < TinyOffset)) Field0xBB = 3;
        return result;
    }

    /// <summary>
    /// <c>TransformPlacementOffsetsRelativeObject</c> 0x00554E40: the object's z-rotated point above its centre (0.5) with respect to the robot gives
    /// psi = its angle around Z; within 15 degrees (0.2617994) of 0: (A', B') = (-A, B); of +pi/2: (B, A); of -pi/2: (-B, -A); of +-pi: (A, -B); none:
    /// 0x04000001. The compares are raw STRICT compares against 0x3E860A92 (0x00554EC8..0x00554ED8, 0x00554F0A..0x00554F12, 0x00554F38..0x00554F40), not
    /// <c>Radians::IsNear</c>, and a NaN psi is near none. A' &lt; -16.000009 is an error, 0x03000000. A missing object is 0x03000004. Otherwise the pair is
    /// stored into +0x108/+0x10C and 0 is returned.
    /// </summary>
    // fidelity: M12-025
    internal ActionResult TransformPlacementOffsetsRelativeObject()
    {
        var obj = M.World.GetLocatedObjectById(ObjectId);
        if (obj is null) return ActionResult.BadObject;
        var robot = M.RobotPose();
        if (robot is null) return ActionResult.Abort;
        var centre = Queries.ZRotatedPointAboveObjectCenter(obj, TransformPointAboveCentre);
        double psi = EngineRadians.GetAngleAroundZaxis(centre.WithRespectTo(robot.Value).Rotation);
        double halfPi = BitConverter.UInt32BitsToSingle(0x3FC90FDB), pi = BitConverter.UInt32BitsToSingle(0x40490FDB);
        double a = OffsetA, b = OffsetB, a2, b2;
        if (IsWithinAlignmentTolerance(psi)) { a2 = -a; b2 = b; }
        else if (IsWithinAlignmentTolerance(psi - halfPi)) { a2 = b; b2 = a; }
        else if (IsWithinAlignmentTolerance(psi + halfPi)) { a2 = -b; b2 = -a; }
        else if (IsWithinAlignmentTolerance(psi - pi) || IsWithinAlignmentTolerance(psi + pi)) { a2 = a; b2 = -b; }
        else
        {
            _trace.Add("PlaceRelObjectAction.TransformPlacementOffsetsRelativeObject: robot and block are not within alignment threshold");
            return ActionResult.DidNotReachPreActionPose;
        }
        if (a2 < MinAdjustedA) return ActionResult.Abort;
        OffsetA = a2; OffsetB = b2;
        return ActionResult.Success;
    }

    /// <summary>
    /// One of the transform's raw compares: <c>|difference| &lt; 0x3E860A92</c>, STRICT (<c>vcmpe</c> then <c>bpl</c> at 0x00554EC8..0x00554ED8, 0x00554F0A..0x00554F12,
    /// 0x00554F38..0x00554F40); not <c>Radians::IsNear</c>, and a NaN difference is near nothing.
    /// </summary>
    // fidelity: M12-025
    internal static bool IsWithinAlignmentTolerance(double difference) => Math.Abs(difference) < AlignmentToleranceRad;

    /// <summary>Runs <c>InitInternal</c> on its own (a test seam; the dock action's <c>Init</c> calls it as its last setup step).</summary>
    internal ActionResult RunInitInternal() => InitInternal();
    /// <summary>The placement offset the dock uses: <c>+0x9C</c>/<c>+0xA0</c> once <c>InitInternal</c> has run.</summary>
    internal (double X, double Y, double Angle) InitialisedPlacementOffset => PlacementOffset;

    protected override (double X, double Y, double Angle) PlacementOffset =>
        _legacy && _legacyOffsets is { } legacy ? legacy
        : _initialised is { } i ? (i.X, i.Y, _angle) : (OffsetA, OffsetB, _angle);
    protected override PreActionType PreActionType => PreActionType.PlaceRelative;
    /// <summary>The legacy constructor's callers keep the pre-batch turn/marker/placement-clear stand-in (M12-028); an engine-shaped action runs the M12-037 compound.</summary>
    protected override bool UsesLegacyTurnAndVerify => _legacy;
    /// <summary><c>[+0xC0] = 0</c> from the constructor (M12-025), so <c>SetupTurnAndVerifyAction</c> adds only the turn action (M12-037).</summary>
    // fidelity: M12-025, M12-037
    protected override bool VerifyNoObjectAtPoseEnabled => Field0xC0 != 0;

    /// <summary>The nullable form of <see cref="SelectDockActionResult"/>, for the base class's abstract member.</summary>
    protected override DockAction? SelectDockAction(ObservableObject target) =>
        SelectDockActionResult(target, out var action) == ActionResult.Success ? action : null;

    /// <summary>
    /// <c>PlaceRelObjectAction::SelectDockAction</c> 0x00555434..0x005555B5 (M12-017): not carrying is 0x03000011; <c>+0xA8</c> non-zero selects DockAction 3
    /// (<c>PlaceLow</c>; the <c>IDockAction</c> type 0x18 is not modelled); otherwise, if <c>CanStackOnTopOfObject</c> holds, DockAction 2 (<c>PlaceHigh</c>),
    /// <c>+0xBB = 0</c> (0x005554D0) and type 0x19 (not modelled); otherwise a warning and 0x03000004.
    /// </summary>
    // fidelity: M12-017, M12-005
    protected override ActionResult SelectDockActionResult(ObservableObject target, out DockAction action)
    {
        action = default;
        if (!M.Docking.Carrying.IsCarryingObject) { _trace.Add("PlaceRelObjectAction.SelectDockAction.NotCarryingObject"); return ActionResult.NotCarryingObjectAbort; }
        if (FlagAt0xA8) { action = DockAction.PlaceLow; return ActionResult.Success; }
        if (M.Docking.CanStackOnTopOfObject(target)) { action = DockAction.PlaceHigh; Field0xBB = 0; return ActionResult.Success; }
        _trace.Add("PlaceRelObjectAction.SelectDockAction: the target cannot be stacked on");
        return ActionResult.BadObject;
    }

    protected override ActionResult Verify(ObservableObject? target, DockResult result)
    {
        if (!result.Succeeded || result.Status != BlockStatus.BlockPlaced) return ActionResult.Retry;
        return M.Docking.Carrying.IsCarryingObject ? ActionResult.StillCarryingObject : ActionResult.Success;
    }
}

/// <summary>
/// <c>RollObjectAction</c>: <c>RollLow</c> (or <c>DeepRollLow</c> when deep roll is enabled). The behaviour that
/// uses it judges success by the object's up axis changing; here the dock result is returned and the
/// caller compares up axes.
/// </summary>
public sealed class RollObjectAction : DockActionBase
{
    public RollObjectAction(ManipulationSystem m, uint objectId) : base(m, objectId) { }
    public bool DeepRoll { get; set; }
    protected override PreActionType PreActionType => PreActionType.Rolling;
    protected override DockAction? SelectDockAction(ObservableObject target) =>
        M.Docking.Carrying.IsCarryingObject ? null : DeepRoll ? DockAction.DeepRollLow : DockAction.RollLow;
    protected override ActionResult Verify(ObservableObject? target, DockResult result) => result.Succeeded ? ActionResult.Success : ActionResult.Retry;
}

/// <summary><c>PopAWheelieAction</c>: <c>PopAWheelie</c> against a cube's face.</summary>
public sealed class PopAWheelieAction : DockActionBase
{
    public PopAWheelieAction(ManipulationSystem m, uint objectId) : base(m, objectId) { }
    protected override PreActionType PreActionType => PreActionType.Docking;
    protected override DockAction? SelectDockAction(ObservableObject target) => M.Docking.Carrying.IsCarryingObject ? null : DockAction.PopAWheelie;
    protected override ActionResult Verify(ObservableObject? target, DockResult result) => result.Succeeded ? ActionResult.Success : ActionResult.Retry;
}

/// <summary>
/// <c>PlaceObjectOnGroundAction</c> (0x00554638): not a dock. "executing PlaceObjectOnGroundAction but not
/// carrying object" aborts; otherwise <c>CarryingComponent::PlaceObjectOnGround</c> sends
/// <c>PlaceObjectOnGround</c> (0x44) and the action waits for the robot's <c>PickAndPlaceResult</c> with
/// <c>BlockPlaced</c>; a following face-and-verify failure just clears the object from the world. Message
/// field order INFERRED from the packing at 0x00632BAE..0x00632BEE: {speed, accel, decel, rel_x, rel_y,
/// rel_angle, useApproachAngle} (hardware item O).
/// </summary>
public sealed class PlaceObjectOnGroundAction
{
    // fidelity: M12-015
    private readonly ManipulationSystem _m;
    public PlaceObjectOnGroundAction(ManipulationSystem m) => _m = m;
    public IReadOnlyList<string> Trace => _trace;
    private readonly List<string> _trace = new();

    /// <summary>
    /// The engine's put-down message, from the builder at 0x00632B88 that
    /// <c>CarryingComponent::PlaceObjectOnGround</c> 0x00632A88 is the only caller of.
    ///
    /// The three offsets come first and the caller always passes zero for all three - they are integer
    /// locals it sets to 0 and the builder converts with <c>vcvt.f32.s32</c>. The speeds follow, and they
    /// are not the motion profile's: they are a constant triple at 0xC7CD90, 100 / 200 / 500. The last
    /// byte is the <c>bool</c> the component was called with.
    ///
    /// This stack had the speeds in the first three words and zeros in the rest, so the robot was handed
    /// a docking speed where it reads a placement offset and nothing at all where it reads the speed.
    /// </summary>
    public const float SpeedMmps = 100f, AccelMmps2 = 200f, DecelMmps2 = 500f;

    public static PlaceObjectOnGround Message(bool flag = false) => new()
    {
        RelX = 0f, RelY = 0f, RelAngle = 0f,
        SpeedMmps = SpeedMmps, AccelMmps2 = AccelMmps2, DecelMmps2 = DecelMmps2,
        Field6 = flag,
    };

    /// <summary>
    /// <c>PlaceObjectOnGroundAction+0x84</c>: set to 1 by <c>CheckIfDone</c> while the robot's status bit 0x4
    /// (IS_PICKING_OR_PLACING) is set (0x005549C0), and 0 by <c>Init</c> (0x00554794). <c>CheckIfDone</c> only
    /// runs its face-and-verify sub-action once this latch is set and the bit has cleared.
    /// </summary>
    // fidelity: M2-002
    public bool StatusLatched { get; private set; }

    // fidelity: M2-002
    /// <summary>
    /// <c>PlaceObjectOnGroundAction::CheckIfDone</c> 0x005549B0..0x00554A3B's status gate, in the engine's
    /// order:
    /// <list type="number">
    /// <item>while the robot's status bit 0x4 (IS_PICKING_OR_PLACING, stored at DockingComponent+4,
    /// 0x00512A96) is set, latch <c>+0x84 = 1</c> (0x005549C0) and stay RUNNING;</item>
    /// <item>with bit 0x4 clear, a clear latch also stays RUNNING (0x005549D6);</item>
    /// <item>with the latch set, the MovementComponent+9 byte (status bit 0x1, 0x0063E30A) must also be clear
    /// (0x005549D8..0x005549E0) before the engine runs its face-and-verify sub-action.</item>
    /// </list>
    /// Returns true when the engine would run the sub-action. This stack has no per-tick action list, so
    /// <see cref="RunAsync"/> polls this while the dock is pending.
    /// </summary>
    private bool StatusGateOpen()
    {
        bool pickingOrPlacing = _m.Robot.State.Latest?.Has(RobotStatusFlag.IsPickingOrPlacing) ?? false;
        if (pickingOrPlacing) { StatusLatched = true; return false; }          // 0x005549C0
        if (!StatusLatched) return false;                                      // 0x005549D6
        bool moving = _m.Robot.State.Latest?.Has(RobotStatusFlag.IsMoving) ?? false;
        return !moving;                                                        // 0x005549E0
    }

    public async Task<ActionResult> RunAsync(CancellationToken cancel)
    {
        if (!_m.Docking.Carrying.IsCarryingObject) { _trace.Add("PlaceObjectOnGroundAction.CheckPreconditions.NotCarryingObject"); return ActionResult.NotCarryingObjectAbort; }
        // Init 0x00554794 zeroes +0x84 before sending PlaceObjectOnGround.
        StatusLatched = false;
        var dock = _m.Docking.PlaceOnGroundAsync(Message(), TimeSpan.FromSeconds(10), cancel);
        // fidelity: M2-002
        // The engine's action list calls CheckIfDone once per tick while the place runs; this stack polls the
        // same gate, so the +0x84 latch is set when the robot reports IS_PICKING_OR_PLACING (0x005549C0).
        while (!dock.IsCompleted)
        {
            StatusGateOpen();
            await Task.WhenAny(dock, Task.Delay(1, CancellationToken.None));
        }
        StatusGateOpen();
        var result = await dock;
        if (result is null) return ActionResult.Timeout;
        // fidelity: M2-002
        // CheckIfDone does not accept the sub-action result until the gate opens. The engine would stay
        // RUNNING forever if the robot never reported IS_PICKING_OR_PLACING; this stack has no tick to keep
        // the action alive, so an unlatched gate completes with the dock result (a LOCAL bridge, M2-002).
        while (StatusLatched && !StatusGateOpen() && !cancel.IsCancellationRequested)
            await Task.Delay(1, CancellationToken.None);
        _trace.Add($"PlaceObjectOnGround result {result}");
        return result.Status == BlockStatus.BlockPlaced && !_m.Docking.Carrying.IsCarryingObject ? ActionResult.Success : ActionResult.StillCarryingObject;
    }
}

/// <summary><c>MoveLiftToHeightAction</c>: a lift preset through <c>SetLiftHeight</c>.</summary>
public sealed class MoveLiftToHeightAction
{
    private readonly ManipulationSystem _m;
    public MoveLiftToHeightAction(ManipulationSystem m, float heightMm) { _m = m; HeightMm = heightMm; }
    public float HeightMm { get; }
    public async Task<ActionResult> RunAsync(CancellationToken cancel)
    {
        var o = await _m.Robot.Motion.SetLiftHeightAsync(HeightMm, requireCalibration: false);
        return o.Result == MotionResult.Acknowledged ? ActionResult.Success : ActionResult.Timeout;
    }
}
