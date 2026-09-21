using Cozmo.Protocol;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

// The two timed checks in PickupObjectAction::Verify 0x00553BE0, and the guard on marking a moved
// cube dirty (fidelity manifest M12-007 and M11-009).
public class PickupVerifyTests
{
    private static readonly Lazy<Cozmo.Robot.Animation.Wwise.WwiseSoundLibrary?> Library = new(() => WwiseAssets.Library);

    /// <summary>The constants are the ones the constructor writes at 0x005536CC onwards.</summary>
    [Fact]
    public void TheVerifyTimeoutsAreTheEngines()
    {
        Assert.Equal(500u, PickupObjectAction.StillMovingAllowanceMs);        // +0x118
        Assert.Equal(500u, PickupObjectAction.LowDockObservationTimeoutMs);   // +0x11C
        Assert.Equal(2000u, PickupObjectAction.HighDockObservationTimeoutMs); // +0x120
    }

    /// <summary>
    /// A cube that reports motion goes Dirty and reports itself moving; the stopped report clears the
    /// motion flag. HandleActiveObjectMoved 0x00533E30 does both, the dirtying behind the guard at
    /// 0x00534116 that also requires the robot not to be carrying the object.
    /// </summary>
    [Fact]
    public void AMovedCubeGoesDirtyAndReportsItselfMoving()
    {
        if (Library.Value is null) return;
        using var rig = new Rig();
        rig.Cube = ManipulationTests.CubeAt(150, 0);
        var obj = Assert.Single(rig.Frame().Objects).Object;
        Assert.Equal(PoseState.Known, obj.PoseState);
        Assert.False(obj.IsMoving);

        rig.Send(new ObjectMoved { Timestamp = rig.T, ObjectID = obj.ObjectId });
        rig.Pump();
        Assert.True(obj.IsMoving);
        Assert.Equal(PoseState.Dirty, obj.PoseState);

        rig.Send(new ObjectStoppedMoving { Timestamp = rig.T, ObjectID = obj.ObjectId });
        rig.Pump();
        Assert.False(obj.IsMoving);
    }

    /// <summary>
    /// And the guard: a cube on the lift reporting its own motion does not dirty the pose the lift is
    /// holding it at, because the engine skips MarkObjectDirty while the carrying component owns it.
    /// </summary>
    [Fact]
    public void ACarriedCubeReportingMotionDoesNotGoDirty()
    {
        if (Library.Value is null) return;
        using var rig = new Rig();
        rig.Cube = ManipulationTests.CubeAt(150, 0);
        var obj = Assert.Single(rig.Frame().Objects).Object;
        rig.M.Docking.Carrying.SetCarrying(obj.ObjectId);

        rig.Send(new ObjectMoved { Timestamp = rig.T, ObjectID = obj.ObjectId });
        rig.Pump();
        Assert.True(obj.IsMoving);                       // the motion is still recorded
        Assert.Equal(PoseState.Known, obj.PoseState);    // but the pose is not dirtied
    }

    /// <summary>
    /// Letting go leaves the cube located and Dirty where the lift left it, not forgotten and not Known:
    /// SetCarriedObjectAsUnattached(false) re-registers it through AddRobotRelativeObservation with
    /// PoseState 2 (0x00633458). Forgetting it is the other argument, which a failed pick-up passes.
    /// </summary>
    [Fact]
    public void LettingGoLeavesTheCubeDirtyWhereTheLiftLeftIt()
    {
        if (Library.Value is null) return;
        using var rig = new Rig();
        rig.Cube = ManipulationTests.CubeAt(150, 0);
        var obj = Assert.Single(rig.Frame().Objects).Object;
        rig.M.Docking.Carrying.SetCarrying(obj.ObjectId, obj.Markers[0]);

        rig.M.Docking.ReleaseCarriedObject();
        Assert.Equal(PoseState.Dirty, obj.PoseState);       // still located, just not trusted
        Assert.True(obj.IsLocated);
        Assert.False(rig.M.Docking.Carrying.IsCarryingObject);

        // and the failed-pick-up form, which the engine follows with DeleteLocatedObjects
        rig.M.Docking.Carrying.SetCarrying(obj.ObjectId, obj.Markers[0]);
        rig.M.Docking.ReleaseCarriedObject(forget: true);
        Assert.False(obj.IsLocated);
    }

    /// <summary>
    /// The dock helper gives up after two attempts, which is what PickupBlockHelper allows:
    /// RespondToPickupResult 0x005B8192 retries only while the count is <= 1, and its log is built with
    /// a literal 2 at 0x005B814A.
    /// </summary>
    [Fact]
    public void TheDockHelperAllowsTwoAttempts()
    {
        Assert.Equal(2, Cozmo.Robot.Manipulation.DockHelper.MaxAttempts);
        Assert.Equal(2, new Cozmo.Robot.Manipulation.DockHelper(null!).AttemptLimit);
    }

    /// <summary>
    /// A roll gets three, not two. <c>RollBlockHelper::StartRollingAction</c> compares its own attempt
    /// count at helper+0x124 with the literal 3 (<c>cmp r0, #3</c> at 0x005B9F0E) and calls
    /// <c>MarkTargetAsFailedToRoll</c> at or above it. The limit is per helper, not shared: the roll
    /// helper's constructor at 0x005B98D4 copies only the callback, two words and a Radians out of its
    /// RollBlockParameters and zeroes that counter, so nothing in the parameters carries it.
    /// </summary>
    [Fact]
    public void ARollGetsThreeAttempts()
    {
        Assert.Equal(3, Cozmo.Robot.Manipulation.DockHelper.MaxRollAttempts);
        Assert.Equal(3, new Cozmo.Robot.Manipulation.DockHelper(null!)
                        { AttemptLimit = Cozmo.Robot.Manipulation.DockHelper.MaxRollAttempts }.AttemptLimit);
    }
}
