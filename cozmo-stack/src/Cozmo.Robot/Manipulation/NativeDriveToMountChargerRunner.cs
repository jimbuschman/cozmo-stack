using Cozmo.Robot.Behavior;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Manipulation;

// fidelity: M13-008, M12-022
internal sealed class NativeDriveToMountChargerRunner : CompoundActionSequential
{
    internal NativeDriveToObjectRunner Drive { get; }
    internal MountChargerAction Mount { get; }
    internal TaskCompletionSource<(uint Result, bool OwnerRetired)>? AiNativeEnded;
    internal EngineRobot? AiOwner;
    internal string? HostUnavailable { get; private set; }

    internal NativeDriveToMountChargerRunner(ManipulationSystem m, EngineRobot owner, uint id, Func<bool> current)
        : base(() => m.Robot.Engine.Timer.SecondsF)
    {
        SourceLog = (level, channel, key, text) => m.Robot.Engine.EmitSourceLog(level, channel, key, text);
        SeedNativeEmptyCompletion();
        bool Live() => current() && !DestructionObserved;

        // 0x0054E7FE..0x0054E836: the original robot and ID, Docking(0),
        // distance 0, useApproachAngle false, angle 0, useManualSpeed false (caller stack bool).
        Drive = new(m, owner, id, PreActionType.Docking, Live);
        Drive.Unavailable = Unavailable;
        // 0x0054E856..0x0054E87A: same robot, copied ID, +0x80=false (handler
        // useManualSpeed), +0x81=false (handler literal zero).
        Mount = new(m, id, flag80: false, flag81: false);
        // 0x0054E848 / 0x0054E88A: append both in order, both bools zero; no proxy.
        AddAction(Drive, false, false);
        AddAction(new MountRunner(m, owner, Mount, Live), false, false);
    }

    private void Unavailable(NativeActionUnavailable error)
    {
        HostUnavailable ??= error.Message;
        SteppedBehavior.ReportMissing("M13-008 mount input unavailable: " + error.Message);
        if (AiOwner is { } owner)
            owner.ActionList.DeferHostCleanup(() => { if (owner.ActionList.ContainsRunner(this)) owner.ActionList.Cancel(Tag); });
    }

    public override uint Update()
    {
        int depth = Watcher?.HostUpdateDepth ?? 0;
        try { return base.Update(); }
        catch (NativeActionUnavailable error)
        {
            Watcher?.UnwindUnavailableHostUpdate(depth);
            Unavailable(error);
            return EngineActionResult.Running;
        }
    }

    public override void WatcherEnding()
    {
        if (DestructionObserved) return;
        base.WatcherEnding();
        if (AiOwner is { } owner)
            AiNativeEnded?.TrySetResult((State, owner.Lifetime.Destroying || !ReferenceEquals(owner.Engine.Robot, owner)));
    }

    private sealed class MountRunner : NativeVisionRunner
    {
        private readonly MountChargerAction _mount;
        private readonly EngineRobot _owner;
        private CompoundActionSequential? _stage;
        private Stage _phase;

        private enum Stage { Align, Mount, Retry }
        private bool Live() => ScopedCurrent && !DestructionObserved;
        protected override string ActionName => "MountCharger";
        public override string? NativeName => ActionName;
        public override byte? GetCompletionVariant() => 6;

        internal MountRunner(ManipulationSystem m, EngineRobot owner, MountChargerAction mount, Func<bool> current)
            : base(m, 0x11, 7) // IAction constructor at 0x0054E046.
        {
            ScopedOwner = _owner = owner;
            ScopedParentCurrent = current;
            _mount = mount;
        }

        public override int Init()
        {
            using var mutation = M.World.EnterPoseMutation();
            if (!Live()) return unchecked((int)EngineActionResult.Cancelled);
            var result = _mount.Initialize(); // 0x0054E106..0x0054E126, includes the charger ID store.
            if (result != ActionResult.Success) return unchecked((int)result);

            SetStage(Stage.Align,
                new AlignRunner(M, _owner, _mount.CreateAlignAction(), Live),
                M.Robot.Motion.CreateNativeHeadChild(0, MountChargerAction.HeadToleranceRad,
                    CozmoMotion.ActionDefaultHeadSpeedRadPerSec, CozmoMotion.ActionDefaultHeadAccelRadPerSec2, _owner, Live));
            return 0;
        }

        public override uint CheckIfDone()
        {
            if (!Live()) return EngineActionResult.Cancelled;
            uint result = _stage!.Update();
            if (!Live()) return EngineActionResult.Cancelled;
            if (result == EngineActionResult.Running) return result;

            if (_phase == Stage.Align && result == 0)
            {
                // 0x0054E2FA then 0x0054E314: configure and tick the next compound in the same update.
                using var mutation = M.World.EnterPoseMutation();
                var (configured, angle, liftBelow) = _mount.ConfigureTurnAndMount(_owner);
                if (configured != 0) return configured;
                var turn = new NativeTurnInPlaceRunner(M, _owner, angle,
                    MountChargerAction.TurnMaxSpeedRadPerSec, MountChargerAction.TurnAccelRadPerSec2, 0, true)
                    { ScopedParentCurrent = Live };
                var backup = new BackupRunner(M, _owner, _mount);
                if (liftBelow)
                    SetStage(Stage.Mount, turn,
                        M.Robot.Motion.CreateNativeLiftChild(MountChargerAction.LiftHeightForMountMm,
                            MountChargerAction.LiftToleranceMm, _owner, Live), backup);
                else SetStage(Stage.Mount, turn, backup);
                return CheckIfDone();
            }

            if (_phase == Stage.Mount && result != 0)
            {
                using var mutation = M.World.EnterPoseMutation();
                if (!_mount.InsideRetryWindow(_owner))
                {
                    // 0x0054E3D4..0x0054E3E8: start the retry on the same update.
                    EmitSource(EngineLogLevel.Warning, null, "MountChargerAction.CheckIfDone.PositionForRetry",
                        $"Turning and mounting the charger failed (action result = {(ActionResult)result}). Driving forward to position for a retry.");
                    if (!Live()) return EngineActionResult.Cancelled;
                    SetStage(Stage.Retry, new DriveStraightAction(M, _owner,
                        MountChargerAction.RetryDriveMm, MountChargerAction.RetrySpeedMmps, false));
                    return CheckIfDone();
                }
            }
            return _phase == Stage.Retry && result == 0 ? (uint)MountChargerAction.RetryDriveResult : result;
        }

        private void SetStage(Stage phase, params ActionRunner[] children)
        {
            EndStage();
            if (!Live())
            {
                foreach (var child in children) child.RetireNeverAdmittedTree();
                return;
            }
            _phase = phase;
            _stage = new(() => M.Robot.Engine.Timer.SecondsF)
                { SuppressTrackLocking = true, Watcher = Watcher, SourceLog = SourceLog };
            _stage.SeedNativeEmptyCompletion();
            foreach (var child in children) _stage.AddAction(child, false, false);
        }

        private void EndStage()
        {
            var stage = _stage;
            _stage = null;
            stage?.Prep();
            stage?.WatcherEnding();
        }

        public override void WatcherEnding()
        {
            EndStage();
            base.WatcherEnding();
        }
    }

    private sealed class BackupRunner(ManipulationSystem m, EngineRobot owner, MountChargerAction mount)
        : DriveStraightAction(m, owner, MountChargerAction.MountDriveMm, MountChargerAction.MountSpeedMmps, false)
    {
        public override float TimeoutSeconds { get; set; } = MountChargerAction.BackupTimeoutSec;
        public override uint CheckIfDone() => mount.BackupCheckIfDone(base.CheckIfDone, _nativeOwner);
    }

    // fidelity: M13-016, M12-017, M12-037
    private sealed class AlignRunner(ManipulationSystem m, EngineRobot owner, AlignWithObjectAction align, Func<bool> current)
        : NativeVisionRunner(m, 0, 7)
    {
        private CompoundActionSequential? _prepared;
        private IDisposable? _subscriptions;
        private short _marker;
        private bool _sent;
        private bool _ending;
        private ScopedAcquisition? _reactions;

        protected override string ActionName => "AlignWithObject";
        protected override bool HasCustomMotionProfile => Live() && HasSelectedDockProfile;
        // ConfigureAlign at 0x0054E22E calls SetSpeed, which sets the manual flag at 0x00550428.
        public override string? NativeName => ActionName;
        private bool Live() => current() && !_ending && !DestructionObserved && ScopedOwnerCurrent;

        public override int Init()
        {
            ScopedOwner = owner;
            using var mutation = M.World.EnterPoseMutation();
            if (!Live()) return unchecked((int)EngineActionResult.Cancelled);
            align.ResetNativeDockVerification();
            _sent = false;
            owner.BehaviorManagerOwner?.RemoveDisableReactionsLock("dockActions");
            if (!Live()) return unchecked((int)EngineActionResult.Cancelled);
            var target = M.World.GetLocatedObjectByIdHelper(align.ObjectId);
            if (target is null || !target.IsActive) return 0x03000004;
            // IDockAction ctor 0x005503A0..0x005503AA sets +0xB9=1. Mount leaves it set.
            var main = owner.GetCheckedMainPose();
            var pre = DockPreActionPoses.GetNative(new(target, (PreActionType)align.AlignmentPreActionType,
                true, DockActionBase.PreActionAngleToleranceRad, 0, false, 0), main,
                owner.CarryingOwner!.CarriedObjectId, () => M.NativeObstacles(main, owner), M.World);
            if (!Live()) return unchecked((int)EngineActionResult.Cancelled);
            if (pre.Result != ActionResult.Success) return unchecked((int)pre.Result);
            // 0x005534CC..0x005534DE: custom (3) selects ordinary Align (0xA).
            var subscriptions = M.Docking.RegisterActionHandlers(owner, align, DockAction.Align);
            if (!Live() || subscriptions is null)
            {
                subscriptions?.Dispose();
                return unchecked((int)EngineActionResult.Cancelled);
            }
            _subscriptions = subscriptions;
            _marker = unchecked((short)pre.Poses[pre.ClosestIndex].Marker.Code);
            // 0x005533DA clears +0xC0, leaving just the IDock turn child in SetupTurnAndVerifyAction.
            ReleasePreparation();
            _prepared = new CompoundActionSequential(() => owner.Engine.Timer.SecondsF)
                { SuppressTrackLocking = true, Watcher = Watcher };
            _prepared.SeedNativeEmptyCompletion();
            _prepared.AddAction(new NativeTurnTowardsObjectRunner(M, target.ObjectId, _marker, 0f, true, false)
                { SuppressTrackLocking = true, ScopedOwner = owner, ScopedParentCurrent = Live }, false, false);
            align.RemoveNativeDockSquintOnInit();
            uint started = _prepared.Update();
            if (!Live()) return unchecked((int)EngineActionResult.Cancelled);
            if (started != 0 && started != EngineActionResult.Running) return unchecked((int)started);
            if (owner.BehaviorManagerOwner is { } manager)
            {
                var reactions = _reactions = new ScopedAcquisition();
                if (!manager.DisableScopedReactions("dockActions", GroundPlacement.ReactionLockTable, reactions) || !Live())
                    return unchecked((int)EngineActionResult.Cancelled);
                reactions.Completed = true;
            }
            return 0;
        }

        public override uint CheckIfDone()
        {
            if (!Live()) return EngineActionResult.Cancelled;
            if (align.ObjectId == uint.MaxValue) return 0x03000004;
            if (!_sent)
            {
                uint turned = _prepared!.Update();
                if (turned != 0) return turned;
                ReleasePreparation();
                if (!Live()) return EngineActionResult.Cancelled;
                if (!M.Docking.StartNativeDock(owner, align.ObjectId, _marker, DockAction.Align, align.Profile,
                    align.AlignmentDockingMethod, false, Live, align.DockDistanceMm))
                    return Live() ? 0x03000016u : EngineActionResult.Cancelled;
                _sent = true;
            }
            return align.UpdateNativeDockVerification(owner, Verify, Live);
        }

        private uint Verify() => align.VerifyNative(owner);

        private void ReleasePreparation()
        {
            var prepared = _prepared;
            _prepared = null;
            prepared?.WatcherEnding();
        }

        public override void WatcherEnding()
        {
            if (_ending || DestructionObserved) return;
            _ending = true;
            var reactions = _reactions;
            _reactions = null;
            align.CleanUpNativeDock(owner, false, _prepared, State != EngineActionResult.NotStarted, reactions);
            _subscriptions?.Dispose();
            _subscriptions = null;
            ReleasePreparation();
            base.WatcherEnding();
        }
    }
}
