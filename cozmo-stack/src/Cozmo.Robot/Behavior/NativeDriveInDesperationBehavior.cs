using System.Text.Json;
using Cozmo.Protocol;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
namespace Cozmo.Robot.Behavior;

// DRIVE-IN-DESPERATION-ENERGY-1: 005D8AE0..005DA528. Checked Energy and Repair configs.
internal sealed class NativeDriveInDesperationBehavior : SteppedBehavior
{
    private readonly ManipulationSystem _m;
    private readonly EngineRobot _robot;
    private readonly PathMotionProfile _profile;
    private readonly float _min, _max;
    private readonly bool _useCubes;
    private readonly AnimationTrigger _request;
    private readonly BlockWorldFilter _filter = new();
    private int _rounds;
    private float _cooldown = -1f;
    private uint _target = uint.MaxValue;
    private bool _offTreads;

    internal NativeDriveInDesperationBehavior(BehaviorFactoryContext source, JsonElement json)
        : base(BehaviorConfigLoader.ExtractIdFromConfig(json).ToString(), "DriveInDesperation")
    {
        _m = source.Manipulation!; _robot = source.NativeRobot!;
        var profile = json.GetProperty("motionProfile");
        _profile = ExplorerBehaviors.Profile(profile) with { IsCustom = profile.GetProperty("isCustom").GetInt32() != 0 };
        _min = json.GetProperty("minTimeToIdle").GetSingle(); _max = json.GetProperty("maxTimeToIdle").GetSingle();
        _useCubes = json.GetProperty("useCubes").GetBoolean();
        _request = Enum.Parse<AnimationTrigger>(json.GetProperty("requestAnimTrigger").GetString()!);
        _filter.AllowedFamilies.Add((ObjectFamily)2); _filter.Predicates.Add(o => o.IsActive);
        _rounds = _robot.Engine.ContextRandom.RandIntInRange(1, 3);
    }
    protected override bool RunnableGate20(BehaviorContext context) => true;
    protected override bool RunnableGate24(BehaviorContext context) => false;
    protected override bool RunnableGate28(BehaviorContext context) => false;
    protected override bool IsRunnableInternal(BehaviorContext context) => true;
    protected override bool KeepsRunningWithoutAction => _offTreads || HasCurrentAction;
    protected override void OnStart()
    {
        var path = _m.Robot.Motion.Path;
        Scope.SmartSetMotionProfile(() => path.SetNativeCustomMotionProfile(_profile), path.ClearNativeCustomMotionProfile);
        _target = uint.MaxValue;
        Idle();
    }
    private void Idle()
    {
        SetStateName("Idle");
        float duration = (float)_robot.Engine.ContextRandom.RandDblInRange(_min, _max);
        StartActing(new NativeTimedWaitRunner(_m, duration), _ => FromIdle());
    }
    private void FromIdle()
    {
        if (!_useCubes) { NoCubes(); return; }
        float now = _robot.Engine.Timer.SecondsF;
        if (!(_cooldown < 0f || now >= _cooldown)) { NoCubes(); return; }
        _cooldown = (float)((double)now + _robot.Engine.ContextRandom.RandDblInRange(15, 30));
        using var mutation = _m.World.EnterPoseMutation();
        float best = -1f; uint found = uint.MaxValue;
        foreach (var obj in _m.World.FindLocatedMatchingObjects(_filter))
        {
            float distance = 0;
            if (PlacementProducerGeometry.TryDistanceSquared(_robot.GetCheckedMainPose(), obj.NativePose, ref distance) && distance > best)
            { best = distance; found = obj.ObjectId; }
        }
        if (found == uint.MaxValue) { NoCubes(); return; }
        _target = found; DriveToCube();
    }
    private void NoCubes()
    {
        if (_rounds >= 1) { --_rounds; DriveRandom(); return; }
        _rounds = _robot.Engine.ContextRandom.RandIntInRange(1, 3); Request();
    }
    private void DriveRandom()
    {
        SetStateName("DriveRandom");
        var rng = _robot.Engine.ContextRandom;
        double degrees = rng.RandDblInRange(40, 100);
        double coin = rng.RandDbl(1);
        double distance = rng.RandDblInRange(50, 150);
        float delta = (float)degrees * BitConverter.UInt32BitsToSingle(0x3C8EFA35);
        delta *= coin < 0.5 ? -1f : 1f;
        using var goal = new Pose3d(_robot.GetCheckedMainPose());
        var yaw = new Radians(goal.Quaternion.AngleAroundZ);
        var sum = yaw + new Radians(delta);
        var rotation = goal.Quaternion * Rotation3d.FromAngleAxis(sum.Value, new Vec3(0, 0, 1));
        goal.Node!.Transform = new Transform3d(rotation, goal.Translation).TranslateForward((float)distance);
        var action = new DriveToPoseAction(_m, true, false);
        action.SetNativeGoals(new[] { goal });
        StartActing(action, _ => {
                if (!_useCubes) return;
            SetStateName("SearchForCube");
            ReportMissing("M12-010/M8-011: SearchForBlockHelper sentinel target -1, minimum seen 1, maximum stage 1 is unavailable; search motions skipped, no result or callback substituted", new HashSet<string>());
        });
    }
    private void DriveToCube()
    {
        SetStateName("DriveToCube");
        if (_target == uint.MaxValue) { ReportMissing("MISSING: M12-010 sentinel search success target join"); return; }
        using var mutation = _m.World.EnterPoseMutation();
        var obj = _m.World.GetLocatedObjectByIdHelper(_target);
        if (obj is null || !(CubeGeometry.IsCube(obj.Type) || obj.Type == ObjectType.Charger_Basic)) { _target = uint.MaxValue; Idle(); return; }
        var main = _robot.GetCheckedMainPose();
        PreActionPoseOutput pre;
        try
        {
            pre = DockPreActionPoses.GetNative(new(obj, PreActionType.Rolling, false, 0, BitConverter.UInt32BitsToSingle(0x3E860A92), false, 0),
                main, _robot.CarryingOwner!.CarriedObjectId, () => _m.NativeObstacles(main), _m.World);
        }
        catch (NativeActionUnavailable error)
        {
            // fidelity: M12-036
            // 005D995C: unavailable host geometry is not a native PAP result; skip to the existing 005D9A54 continuation.
            ReportMissing("M12-036 DriveInDesperation 0x005D995C: " + error.Message + "; unavailable PAP/drive skipped, continuing at 0x005D9A54 LookAtCube", new HashSet<string>());
            LookAtCube();
            return;
        }
        if (pre.Result != ActionResult.Success || pre.InPosition) { LookAtCube(); return; }
        var action = new DriveToPoseAction(_m, false, false) { DistanceToleranceMm = 30, AngleToleranceRad = BitConverter.UInt32BitsToSingle(0x3E860A92) };
        action.SetNativeGoals(pre.Poses.Select(p => p.WorldPose).ToArray());
        StartActing(action, _ => LookAtCube());
    }
    private void LookAtCube()
    {
        SetStateName("LookAtCube");
        if (_target == uint.MaxValue) { Idle(); return; }
        StartActing(new NativeTurnTowardsObjectRunner(_m, _target, NativeTurnTowardsObjectRunner.AnyCode, Radians.Pi, true, false), r => { if (r.Result == 0) Request(); });
    }
    private void Request()
    {
        SetStateName("Request");
        var animation = new TriggerAnimationAction(_m.Robot, new(_request, 1, true, 0, 60f, false));
        var action = new NativeTurnTowardsFaceWrapperRunner(_m, animation, true, false, new Radians(Radians.Pi), false);
        StartActing(action, _ => {
                if (_target == uint.MaxValue) return;
            StartActing(new NativeTurnTowardsObjectRunner(_m, _target, NativeTurnTowardsObjectRunner.AnyCode, Radians.Pi, false, false), null);
            _target = uint.MaxValue;
        });
    }
    protected override void OnUpdate()
    {
        if (_m.Robot.Sensors.OffTreadsState != OffTreadsState.OnTreads)
        {
            if (!_offTreads) { StopActing(false, false); _offTreads = true; }
            return;
        }
        if (_offTreads) { _target = uint.MaxValue; _offTreads = false; Idle(); }
    }
}
