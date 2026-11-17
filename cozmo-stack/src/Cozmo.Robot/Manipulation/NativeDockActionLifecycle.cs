using Cozmo.Robot.Behavior;

namespace Cozmo.Robot.Manipulation;

public abstract partial class DockActionBase
{
    private bool _nativeDockWasActive;
    private float _nativeDockVerifyDeadline;
    private byte _nativeDockSquint;

    // IDockAction::Init writes +0x90 at 0x00551528. A successful DockWithObject
    // clears the active-edge byte +0x94 at 0x00552344 before reading Docking+4.
    internal void ResetNativeDockVerification()
    {
        _nativeDockWasActive = false;
        _nativeDockVerifyDeadline = -1f;
    }

    // 0x005522C0..0x005523F4. All five active IDock subclasses share the
    // same verification delay slot (+0x44 -> 0x0055738A, returns 0.0f).
    internal uint UpdateNativeDockVerification(EngineRobot owner, Func<uint> verify, Func<bool>? current = null)
    {
        if (owner.DockingOwner!.Active != 0)
        {
            if (!_nativeDockWasActive)
            {
                _nativeDockWasActive = true;
                // 0x00552358..0x0055235E compares the raw selection (3 = none) with 1, so no selection adds the squint.
                if (owner.Engine.Needs.SevereNeeds.Selected != 1)
                    _nativeDockSquint = AddDockSquintLayer();
            }
            return current?.Invoke() == false ? EngineActionResult.Cancelled : EngineActionResult.Running;
        }

        // MovementComponent::Update 0x0063E30A..0x0063E328 writes +9 from
        // status bit 0 and +0xA from !HeadInPos. The wheel bit is stored at +0xC.
        if (!_nativeDockWasActive || owner.NativeMovementIsMoving)
            return EngineActionResult.Running;
        float now = owner.Engine.Timer.SecondsF;
        if (owner.NativeMovementHeadMoving)
            _nativeDockVerifyDeadline = -1f;
        if (_nativeDockVerifyDeadline < 0f)
            _nativeDockVerifyDeadline = now + 0f;
        return now >= _nativeDockVerifyDeadline ? verify() : EngineActionResult.Running;
    }

    // 0x00557506 removes the saved squint with a 250 ms transition.
    internal void RemoveNativeDockSquint()
    {
        byte squint = _nativeDockSquint;
        _nativeDockSquint = 0;
        M.Robot.Animations.Scheduler.Layers.Face.RemovePersistentLayer(squint, 250);
    }

    internal void RemoveNativeDockSquintOnInit()
    {
        if (_nativeDockSquint != 0)
            RemoveNativeDockSquint();
    }

    // IDockAction base destruction 0x0055745C..0x00557582. The caller still owns the
    // prepared action and subscriptions, which are released after this prefix.
    internal void CleanUpNativeDock(EngineRobot? owner, bool interactingLight, IActionRunner? preparation,
        bool actionStarted, ScopedAcquisition? reactions = null)
    {
        var path = M.Robot.Motion.Path;
        long pathGeneration = path.ScopeGeneration;
        long dockGeneration = M.Docking.NativeDockGeneration;
        bool OwnerCurrent() => owner is not null && !owner.Lifetime.Destroying &&
            ReferenceEquals(owner.Engine.Robot, owner);
        bool PathCurrent() => OwnerCurrent() && path.ScopeGeneration == pathGeneration;
        bool DockCurrent() => OwnerCurrent() && M.Docking.NativeDockGeneration == dockGeneration;
        bool SharedCurrent() => PathCurrent() && DockCurrent();

        if (SharedCurrent()) M.World.SelectedObjectId = null;
        if (PathCurrent() && path.IsActive)
            M.Paths.AbortOwned(owner!, PathCurrent);
        if (DockCurrent() && owner!.DockingOwner is { } dock)
        {
            if (dock.Active != 0)
                M.Docking.AbortNativeDock(owner, DockCurrent);
            if (DockCurrent())
            {
                dock.MarkerCode = 3;
                dock.TargetId = null;
            }
        }

        if (interactingLight && SharedCurrent())
            M.Robot.Lights.Cubes.StopNativeInteracting(ObjectId, SharedCurrent);
        if (SharedCurrent()) RemoveNativeDockSquint();
        preparation?.Prep();
        if (reactions is not null)
        {
            reactions.Cancel();
            if (!SharedCurrent()) reactions.Completed = false;
            owner?.BehaviorManagerOwner?.RemoveScopedReactions("dockActions", reactions);
        }
        else if (actionStarted && SharedCurrent())
            owner!.BehaviorManagerOwner?.RemoveDisableReactionsLock("dockActions");
    }

}