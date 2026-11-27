using System.Diagnostics;
using Cozmo.Protocol;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot;

public sealed partial class CozmoMotion
{
    // C2 policy (not engine behavior): three distinct accepted sightings,
    // concentrated headings and a fresh, square-on current-origin marker view.
    // The angle bound is the existing native pre-action tolerance at 0x00558C80 /
    // 0x005513B0 (0x3E060A92); requiring it as a viewing gate is our extension.
    internal const int C2DockMinimumSightings = 3, C2DockMaximumAttempts = 3;
    internal const uint C2DockFreshnessMs = 2000;
    internal static readonly TimeSpan C2DockObservationTimeout = TimeSpan.FromSeconds(5);
    private static double C2DockAngle => DriveToObjectAction.PreActionAngleToleranceRad;

    private async Task<AiPickupActionResult> QueueC2MountChargerAsync(uint chargerId,
        PathMotionProfile? profile, CancellationToken cancel)
    {
        var engine=_robot.Engine;
        EngineRobot owner;
        NativeBehaviorRuntime runtime;
        ManipulationSystem manipulation;
        BehaviorManager manager;
        IDisposable? activity=null;
        string lockId=CozmoC2Settings.ChargerDockName+":"+Guid.NewGuid().ToString("N");
        bool acquired=false;
        AiPickupActionResult? last=null;
        bool Current() => AiOwnerReady(engine,owner) && ReferenceEquals(owner.NativeBehaviorRuntime,runtime)
            && !runtime.IsRetired && ReferenceEquals(owner.BehaviorManagerOwner,manager);
        AiPickupActionResult Fail(string reason,bool retired=false)
        {
            string detail=CozmoC2Settings.ChargerDockName+": "+reason;
            engine.EmitStackLog(detail);
            return last is null ? new(false,null,detail,retired) :
                last with { Detail=detail+"; last native mount: "+last.Detail, OwnerRetired=retired||last.OwnerRetired };
        }
        // Native lock table 0x00C55A21 covers index8 only; extending index20
        // through 0x005A27F6/0x005A3A52 is deliberate C2 policy, not native change.
        using(engine.EnterSynchronousOwner())
        {
            if(chargerId>int.MaxValue)return new(false,null,"Object ID must be a nonnegative signed world ID");
            if(cancel.IsCancellationRequested)return new(false,null,"Request cancelled before admission");
            if(engine.Robot is not { } original || !AiOwnerReady(engine,original) ||
                original.NativeBehaviorRuntime is not { IsRetired:false } liveRuntime ||
                liveRuntime.Manipulation is not { } m || original.BehaviorManagerOwner is not { } liveManager)
                return new(false,null,"Robot is not ready for an action");
            owner=original; runtime=liveRuntime; manipulation=m; manager=liveManager;
            if(owner.ActionList.ExternalActionsDisabled)return new(false,null,"External actions are disabled by a reaction");
            if(!engine.Options.C2.ChargerMemory)return Fail("charger-memory extension is disabled");
            if(owner.C2DockInFlight)return Fail("another charger docking command is already in progress");
            if(C2DockUnsafe(owner,manager) is { } unsafeReason)return Fail(unsafeReason);
            owner.C2DockInFlight=true;
        }
        try
        {
            using(engine.EnterSynchronousOwner())
            {
                if(!Current())return Fail("robot owner retired before admission",true);
                if(cancel.IsCancellationRequested)return Fail("cancelled before admission");
                activity=runtime.BeginAiCommand(); // keep Freeplay parked through all stages
                if(!Current())return Fail("robot owner retired while parking activity",true);
                var flags=new bool[BehaviorManager.TriggerCount];
                flags[(int)ReactionTrigger.UnexpectedMovement]=true;
                acquired=true;
                manager.DisableReactionsWithLock(lockId,flags);
                if(!Current())return Fail("robot owner retired while locking the dock reaction",true);
                engine.EmitStackLog($"{CozmoC2Settings.ChargerDockName}: UnexpectedMovement lock held; cliff detection remains enabled");
            }

            for(int attempt=1;attempt<=C2DockMaximumAttempts;attempt++)
            {
                Task<AiHeadActionResult>? viewAction=null;
                uint? observedAfter=null;
                using(engine.EnterSynchronousOwner())
                {
                    if(!Current())return Fail("robot owner retired before attempt "+attempt,true);
                    if(cancel.IsCancellationRequested)return Fail("cancelled before attempt "+attempt);
                    if(C2DockUnsafe(owner,manager) is { } unsafeReason)return Fail(unsafeReason);
                    if(owner.ActionList.ExternalActionsDisabled)return Fail("external actions disabled by a reaction");
                    using var mutation=manipulation.World.EnterPoseMutation();
                    string? reason=C2DockReadiness(owner,manipulation,chargerId);
                    if(attempt>1 || reason is not null)
                    {
                        using var viewing=C2CopyViewingGoal(owner,manipulation,chargerId);
                        if(viewing is null)return Fail("no current-origin charger pose for a viewing approach; find the charger again");
                        engine.EmitStackLog($"{CozmoC2Settings.ChargerDockName}: attempt {attempt} viewing approach ({reason??"re-observe after prior failure"})");
                        if(!Current())return Fail("owner retired before viewing approach",true);
                        uint tag=AllocateAiTag();
                        if(tag==0)return Fail("no external action tag for viewing approach");
                        // Native pre-dock DriveToObject constructs DriveToPose(head=true,
                        // manual=false) at 0x005592E8..0x005592FE. Use the real runner and
                        // normal collision/path planner, with our viewing goal.
                        var drive=new DriveToPoseAction(manipulation,true,false,owner,Current);
                        drive.SetNativeGoals([viewing]);
                        viewAction=AdmitAiAction(drive,owner,tag,cancel,"C2 charger viewing approach ended",true,Current,profile);
                    }
                }
                if(viewAction is not null)
                {
                    var viewed=await viewAction.ConfigureAwait(false);
                    if(!viewed.Admitted || viewed.EngineResult!=0 || viewed.OwnerRetired)
                        return Fail($"viewing approach failed: {viewed.Detail}, native result {viewed.EngineResult:X8}",viewed.OwnerRetired);
                    using(engine.EnterSynchronousOwner())
                    {
                        if(!Current())return Fail("owner retired after viewing approach",true);
                        // Require a new accepted observation after the finished reposition,
                        // not one from while the robot was driving.
                        observedAfter=owner.StoredState?.Timestamp;
                    }
                    string? waitFailure=await C2WaitForDockView(owner,manipulation,manager,chargerId,observedAfter,cancel,Current).ConfigureAwait(false);
                    if(waitFailure is not null)return Fail(waitFailure,!Current());
                }

                Task<AiPickupActionResult> mount;
                using(engine.EnterSynchronousOwner())
                {
                    if(!Current())return Fail("robot owner retired before native mount",true);
                    if(cancel.IsCancellationRequested)return Fail("cancelled before native mount");
                    if(C2DockUnsafe(owner,manager) is { } unsafeReason)return Fail(unsafeReason);
                    using var mutation=manipulation.World.EnterPoseMutation();
                    if(C2DockReadiness(owner,manipulation,chargerId) is { } reason)return Fail(reason);
                    using var fused=engine.C2ChargerMemory.CopyAnchoredPose(owner);
                    var actual=manipulation.World.GetLocatedObjectById(chargerId);
                    if(fused is null || actual is null)return Fail("current fused and native charger pose unavailable");
                    // Detached geometry snapshot for this single native compound, never
                    // inserted into the world. The native camera servo remains live.
                    var geometry=new ObservableObject(chargerId,ObjectType.Charger_Basic,ChargerGeometry.Markers)
                    { OriginId=owner.CurrentOriginId,PoseState=PoseState.Known,LastObservedTimestamp=actual.LastObservedTimestamp,Pose=fused };
                    engine.EmitStackLog($"{CozmoC2Settings.ChargerDockName}: native mount attempt {attempt}/{C2DockMaximumAttempts} using fused flat pose");
                    mount=QueueAiMountChargerNativeAsync(chargerId,profile,cancel,owner,geometry);
                }
                last=await mount.ConfigureAwait(false);
                if(last.Admitted && last.EngineResult==0 && !last.OwnerRetired)
                {
                    engine.EmitStackLog($"{CozmoC2Settings.ChargerDockName}: native mount succeeded on attempt {attempt}");
                    return last with { Detail=$"{CozmoC2Settings.ChargerDockName}: mounted on attempt {attempt}; "+last.Detail };
                }
                if(last.OwnerRetired || !last.Admitted || cancel.IsCancellationRequested ||
                    last.EngineResult is EngineActionResult.Cancelled or EngineActionResult.Interrupted or EngineActionResult.NotStarted or EngineActionResult.TracksLocked ||
                    last.Detail.Contains("MISSING:",StringComparison.Ordinal))
                    return Fail($"native attempt {attempt} ended without a retry",last.OwnerRetired);
                if(attempt==C2DockMaximumAttempts)
                    return Fail($"native mount failed after {attempt} attempts");
                using(engine.EnterSynchronousOwner())
                {
                    if(!Current())return Fail("robot owner retired after native mount",true);
                    if(C2DockUnsafe(owner,manager) is { } unsafeReason)return Fail(unsafeReason);
                    if(owner.ActionList.ExternalActionsDisabled)return Fail("reaction preempted native mount");
                    engine.EmitStackLog($"{CozmoC2Settings.ChargerDockName}: native attempt {attempt} failed ({last.EngineResult:X8}); re-observe before retry");
                }
            }
            return Fail("retry limit reached");
        }
        catch(OperationCanceledException) when(cancel.IsCancellationRequested)
        { return Fail("cancelled while awaiting a fresh charger observation",!Current()); }
        finally
        {
            using(engine.EnterSynchronousOwner())
            {
                if(acquired && !owner.Lifetime.Destroying && ReferenceEquals(engine.Robot,owner) && ReferenceEquals(owner.BehaviorManagerOwner,manager))
                    manager.RemoveDisableReactionsLock(lockId);
                owner.C2DockInFlight=false;
                activity?.Dispose();
                engine.EmitStackLog($"{CozmoC2Settings.ChargerDockName}: docking scope ended; own reaction lock released or owner retired");
            }
        }
    }

    private string? C2DockUnsafe(EngineRobot owner,BehaviorManager manager)
    {
        if(!_robot.Sensors.CliffSensorEnabled || !manager.IsReactionTriggerEnabled(ReactionTrigger.CliffDetected))
            return "cliff detection or its reaction is disabled; docking refused";
        if(owner.C2CliffHoldActive || _robot.Sensors.CliffDetectedByEvent || _robot.Sensors.CliffDetectedNow)
            return "cliff detected; docking refused";
        return null;
    }

    private static string? C2DockReadiness(EngineRobot owner,ManipulationSystem m,uint chargerId)
    {
        if(owner.Engine.C2ChargerMemory.Get(owner) is not { Anchored:true } estimate || estimate.WorldObjectId!=chargerId)
            return "no anchored fused pose for requested charger";
        if(estimate.Sightings<C2DockMinimumSightings)return $"only {estimate.Sightings} of {C2DockMinimumSightings} required sightings";
        if(estimate.HeadingConfidence<0.95)return "charger headings disagree";
        if(owner.StoredState is not { } state || unchecked(state.Timestamp-estimate.LastObservedTimestamp)>C2DockFreshnessMs)
            return "charger sighting is stale";
        if(m.World.GetLocatedObjectById(chargerId) is not { Type:ObjectType.Charger_Basic } actual ||
            actual.OriginId!=owner.CurrentOriginId || !actual.IsLocated)return "charger has no live native located object";
        using var fused=owner.Engine.C2ChargerMemory.CopyAnchoredPose(owner);
        using var relative=new Pose3d();
        if(fused is null || !Pose3d.GetWithRespectTo(owner.GetCheckedMainPose(),fused,relative))
            return "charger pose is outside current origin";
        var t=relative.Translation;
        double view=Math.Abs(Math.Atan2(t.Y,-t.X)), heading=Math.Abs(relative.AngleAroundZ);
        if(t.X>=0 || view>C2DockAngle || heading>C2DockAngle ||
            estimate.LastViewingAngleDeg>C2DockAngle*180/Math.PI)
            return FormattableString.Invariant($"not square-on: view {view*180/Math.PI:F1} deg, heading {heading*180/Math.PI:F1} deg, last sighting {estimate.LastViewingAngleDeg:F1} deg");
        return null;
    }

    private static Pose3d? C2CopyViewingGoal(EngineRobot owner,ManipulationSystem m,uint chargerId)
    {
        Pose3d? flat=null;
        if(owner.Engine.C2ChargerMemory.Get(owner) is { Anchored:true } estimate && estimate.WorldObjectId==chargerId)
            flat=owner.Engine.C2ChargerMemory.CopyAnchoredPose(owner);
        // A single live native sighting can supply a provisional viewing goal,
        // but never mount admission. Old-root memory cannot supply a path.
        var origin=owner.GetNativeOrigin(owner.CurrentOriginId);
        if(flat is null && m.World.GetLocatedObjectById(chargerId) is { Type:ObjectType.Charger_Basic,IsLocated:true } actual)
        {
            using var local=new Pose3d();
            if(Pose3d.GetWithRespectTo(actual.NativePose,origin,local))
                flat=new Pose3d(Rotation3d.FromAngleAxis((float)local.AngleAroundZ,new Vec3(0,0,1)),
                    new Vec3(local.Translation.X,local.Translation.Y,0),parent:origin);
        }
        if(flat is null)return null;
        using(flat)
        using(var pre=ChargerGeometry.PreDockPose(flat)) // 0x004E9FE4..0x004EA058
        using(var local=new Pose3d())
        {
            // Compose returns a detached transform expressed in flat's parent frame.
            pre.SetParent(origin);
            if(!Pose3d.GetWithRespectTo(pre,origin,local))return null;
            return new Pose3d(Rotation3d.FromAngleAxis((float)local.AngleAroundZ,new Vec3(0,0,1)),
                new Vec3(local.Translation.X,local.Translation.Y,0),"C2ChargerViewingPose",origin);
        }
    }

    private async Task<string?> C2WaitForDockView(EngineRobot owner,ManipulationSystem m,BehaviorManager manager,
        uint chargerId,uint? observedAfter,CancellationToken cancel,Func<bool> current)
    {
        var elapsed=Stopwatch.StartNew();
        var store=owner.Engine.C2ChargerMemory;
        string reason="no new charger observation after the viewing approach";
        while(true)
        {
            Task changed;
            using(owner.Engine.EnterSynchronousOwner())
            {
                if(!current())return "robot owner retired while waiting for charger";
                if(C2DockUnsafe(owner,manager) is { } unsafeReason)return unsafeReason;
                if(owner.ActionList.ExternalActionsDisabled)return "reaction preempted the viewing approach";
                using var mutation=m.World.EnterPoseMutation();
                var watch=store.Watch(owner); changed=watch.Changed;
                if(watch.Estimate is { Anchored:true } sample && (!observedAfter.HasValue ||
                    unchecked((int)(sample.LastObservedTimestamp-observedAfter.Value))>0))
                {
                    var denied=C2DockReadiness(owner,m,chargerId);
                    if(denied is null)return null;
                    reason=denied;
                }
            }
            var remaining=C2DockObservationTimeout-elapsed.Elapsed;
            if(remaining<=TimeSpan.Zero)return "timed out re-observing charger: "+reason;
            try { await changed.WaitAsync(remaining,cancel).ConfigureAwait(false); }
            catch(TimeoutException) { return "timed out re-observing charger: "+reason; }
        }
    }
}
