using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
namespace Cozmo.Robot.Behavior;

// This behavior's strict current-origin footprint query; no planar stand-in.
internal sealed class NativeBringBeaconGeometry(ManipulationSystem m,EngineRobot robot)
{
    internal IReadOnlyList<ObservableObject> Cubes(AIBeacon beacon,Func<bool> current)
    {
        var filter=new BlockWorldFilter();filter.AllowedFamilies.Add(ObjectFamily.Block);filter.AllowedFamilies.Add(ObjectFamily.LightCube);
        var carry=robot.CarryingOwner??throw new NativeActionUnavailable("M15-020 native carrying owner unavailable");
        filter.Predicates.Add(o=>{if(!current()||carry.IsCarryingObjectId(o.ObjectId))return false;bool within=beacon.IsLocWithinBeacon(o.NativePose,0f);return current()&&within;});
        return m.World.FindLocatedMatchingObjects(filter);
    }
    internal bool Free(ObservableObject carried,Pose3d pose,Func<bool> current)
    {
        if(!current())throw new NativeActionUnavailable("stale Bring footprint query");
        if(!Footprint.TryGetBoundingQuadXY(carried,pose,0f,out var reference))
            throw new NativeActionUnavailable("M15-019/M13-007 native candidate footprint unavailable");
        var filter=new BlockWorldFilter();filter.IgnoreIds.Add(carried.ObjectId);
        foreach(var other in m.World.FindLocatedMatchingObjects(filter))
        {
            if(!current())throw new NativeActionUnavailable("stale Bring footprint query");
            // fidelity: M13-025, M13-007
            // FindLocatedIntersectingObjects predicate 0x0062750E (M12-030): q = o->vtbl[0x50](o, o->GetPose(), padding), the candidate's own
            // slot for every located class (charger base 0x0087713A, markerless 0x00502C32), not the cube-only footprint.
            if(!other.TryObservableBoundingQuad50(other.NativePose,10f,out var quad))
                throw new NativeActionUnavailable("M15-019/M13-007 native obstacle footprint unavailable");
            if(quad!.Intersects(reference!))return false;
        }
        if(!current())throw new NativeActionUnavailable("stale Bring footprint query");
        return true;
    }
}
