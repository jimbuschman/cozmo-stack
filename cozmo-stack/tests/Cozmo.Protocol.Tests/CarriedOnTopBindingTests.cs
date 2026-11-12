using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// CARRIED-ONTOP-BINDING-CENSUS-1 (records M11-043, M11-009, M4-009, M12-008). <c>CarryingComponent::IsCarryingObject(ObjectID)</c> 0x00633F88 is true for the
/// carried ID [+8] OR the ID of the object resting on it [+0x14] (0x00633F88..0x00633F9E). With a real attach of a two-cube stack
/// (SetObjectAsAttachedToLift, M12-008 step 8 writes [+0x14] at 0x0063318C), the cube on top is treated as carried by every caller of that body. Expected
/// values come from the census rows cited per test, never from what the code returns.
/// </summary>
public class CarriedOnTopBindingTests
{
    private static readonly MarkerLibrary? Lib = MarkerLibrary.EmbeddedOrNull;

    // The stack of M12RVisBuildTests.M12_008_TheCubeOnTopFollowsAtOnceWhenThePickupResultArrives: cube 7 (LIGHTCUBE1) at (260, 0, 22), cube 8 (LIGHTCUBE2)
    // on it at (260, 0, 66), both seen; then the attach HandlePickAndPlaceResult runs on BlockPickedUp (DockingSystem.SetObjectAsAttachedToLift).
    private static Rig StackOnTheLift(out ObservableObject bottom, out ObservableObject top)
    {
        var rig = new Rig();
        rig.Head = 0.05f;
        rig.Cube = new Pose3d(Mat3.Identity, new Vec3(260, 0, 22));
        rig.MoreCubes.Add((ObjectType.Block_LIGHTCUBE2, new Pose3d(Mat3.Identity, new Vec3(260, 0, 66))));
        rig.Frame(); rig.Frame();
        bottom = rig.Vision.World.GetLocatedObjectById(7)!;
        top = rig.Vision.World.GetLocatedObjectById(8)!;
        var marker = bottom.Markers.First(k => k.Code == MarkerType.LightCubeI_Front).Code;
        Assert.Equal(0u, rig.M.Docking.SetObjectAsAttachedToLift(bottom.ObjectId, marker));
        Assert.Equal(7u, rig.M.Docking.Carrying.CarriedObjectId);
        Assert.Equal(8u, rig.M.Docking.Carrying.CarriedOnTopId);                   // [+0x14] = the top's ID (0x0063318C)
        return rig;
    }

    /// <summary>
    /// C5/C7 (M4-009): HandleActiveObjectMoved and HandleActiveObjectStopped skip the ObjectMoved / ObjectStoppedMoving broadcast when IsCarryingObject(id)
    /// (0x0053417C, 0x0053498C) or the dock target: the cube on top is excluded like the carried cube; an uncarried connected cube (9) is still broadcast.
    /// C4/C6 (M11-009): the dirty guard (0x0053411E..0x0053413C, 0x0053492C..0x0053494A) skips MarkObjectDirty for it, while its motion is still recorded.
    /// </summary>
    // reach: production for the broadcast exclusion and the binding (ObjectMoved / ObjectStoppedMoving through the robot message dispatch; the carry state
    // from DockingSystem's attach on the live wiring). reach: unit for the dirty guard: the top cube's PoseState is substituted Known (after the attach
    // AddObjectRelativeObservation leaves it Dirty, 0x00506D30..0x00506D3E, and no production path re-confirms it while it is carried).
    [Fact]
    public void TheCubeOnTopIsExcludedFromTheMovedBroadcastAndIsNotDirtiedByItsOwnMotion()
    {
        using var signals = SignalTestContext.Install();
        if (Lib is null) return;
        using var rig = StackOnTheLift(out _, out var top);
        Assert.True(rig.Vision.World.IsCarryingObject!(8));                          // the shared binding is the engine body 0x00633F88
        Assert.True(rig.Vision.World.IsCarryingObject!(7));
        Assert.False(rig.Vision.World.IsCarryingObject!(9));
        var moved = new List<bool>();
        rig.Robot.Cubes.CubeMoved += c => moved.Add(c.Moving);
        top.PoseState = PoseState.Known;                                             // substituted (see reach)
        rig.Send(new ObjectMoved { Timestamp = rig.T, ObjectID = rig.ActiveIdOf(8) });
        rig.Pump();
        Assert.True(top.IsMoving);
        Assert.Equal(PoseState.Known, top.PoseState);
        Assert.Empty(moved);
        rig.Send(new ObjectStoppedMoving { Timestamp = rig.T, ObjectID = rig.ActiveIdOf(8) });
        rig.Pump();
        Assert.False(top.IsMoving);
        Assert.Equal(PoseState.Known, top.PoseState);
        Assert.Empty(moved);
        rig.Send(new ObjectMoved { Timestamp = rig.T, ObjectID = rig.ActiveIdOf(9) });  // the control
        rig.Pump();
        Assert.Equal(new[] { true }, moved);
    }

    /// <summary>
    /// A sighting of the cube on top (the only cube in view) unsets it before AddAndUpdateObjects' carried test, so the observation is NOT dropped:
    /// AddVisualObservation (0x00620C94) runs first; in it UpdatePoseInInstance's isCarried = IsCarryingObject(existing.ID) (0x00505DFE..0x00505E12) bypasses
    /// both skip clauses (0x00505EAA), the write helper runs, then the Info "SeeingCarriedObject" (0x00505F5A) and CarryingComponent::UnSetCarryObject(id)
    /// (0x00505FBC, GOT 0x01041280). Its body 0x00633F6C..0x00633F86 sends the on-top ID [+0x14] to UnSetCarryingObjects(true) (moveq r1,#1 at 0x00633F80),
    /// which clears [+0x14] := -1 (0x00633D80) and keeps the carried ID [+8]. AddAndUpdateObjects' IsCarryingObject (0x00620F06) is then false: no
    /// "Blockworld.AddAndUpdateObjects.SeeingCarriedObject", and the observation proceeds to the located update.
    /// </summary>
    // reach: production (camera frames through VisionSystem, the sighting through AddAndUpdateObjects with the live DockingSystem wiring). The attach is
    // entered through the public DockingSystem.SetObjectAsAttachedToLift, not a dock result; the rendered scene keeps the top cube where it stood (the
    // carried cube is no longer rendered), so its sighting still reaches the confirmer.
    [Fact]
    public void ASightingOfTheCubeOnTopUnsetsItAndIsNotDropped()
    {
        using var signals = SignalTestContext.Install();
        if (Lib is null) return;
        using var rig = StackOnTheLift(out _, out _);
        rig.Cube = null;                                                              // only the top cube stays in view
        int mark;
        lock (rig.Log) mark = rig.Log.Count;
        rig.Frame();
        List<string> since;
        lock (rig.Log) since = rig.Log.Skip(mark).ToList();
        int unset = since.FindIndex(l => l.Contains("ObjectPoseConfirmer.SeeingCarriedObject: We changed the pose of 8,"));
        Assert.True(unset >= 0);
        Assert.Null(rig.M.Docking.Carrying.CarriedOnTopId);                          // [+0x14] := -1 (0x00633D80)
        Assert.Equal(7u, rig.M.Docking.Carrying.CarriedObjectId);                    // [+8] kept: top-only
        Assert.DoesNotContain(since, l => l.Contains("Blockworld.AddAndUpdateObjects.SeeingCarriedObject"));
        Assert.Contains(since.Skip(unset + 1), l => l.StartsWith("seen object 8 "));   // the observation proceeds (0x00621146 onward)
    }

    /// <summary>
    /// A sighting of the carried cube (the only cube in view): UpdatePoseInInstance's UnSetCarryObject(7) (0x00505FBC) takes the carried branch of
    /// 0x00633F6C (moveq r1,#0 at 0x00633F74): UnSetCarryingObjects(false) clears [+8] := -1 (0x00633D78) and [+0x14] := -1 (0x00633D80) and sends
    /// CarryStateUpdate CARRY_NONE. AddAndUpdateObjects' carried test (0x00620F06) is then false and the observation is not dropped.
    /// </summary>
    // reach: production (as above; the scene keeps the carried cube where it stood, the top cube is no longer rendered).
    [Fact]
    public void ASightingOfTheCarriedCubeUnsetsBothAndSendsCarryNone()
    {
        using var signals = SignalTestContext.Install();
        if (Lib is null) return;
        using var rig = StackOnTheLift(out _, out _);
        rig.MoreCubes.Clear();                                                        // only the carried cube stays in view
        int mark, sentMark = rig.Sent.Count;
        lock (rig.Log) mark = rig.Log.Count;
        rig.Frame();
        List<string> since;
        lock (rig.Log) since = rig.Log.Skip(mark).ToList();
        int unset = since.FindIndex(l => l.Contains("ObjectPoseConfirmer.SeeingCarriedObject: We changed the pose of 7,"));
        Assert.True(unset >= 0);
        Assert.Null(rig.M.Docking.Carrying.CarriedObjectId);
        Assert.Null(rig.M.Docking.Carrying.CarriedOnTopId);
        rig.Pump();
        Assert.Contains(rig.Sent.Skip(sentMark), m => m is CarryStateUpdate { State: CarryState.CARRY_NONE });
        Assert.DoesNotContain(since, l => l.Contains("Blockworld.AddAndUpdateObjects.SeeingCarriedObject"));
        Assert.Contains(since.Skip(unset + 1), l => l.StartsWith("seen object 7 "));
    }
}
