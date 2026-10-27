using Cozmo.Robot.Manipulation;
namespace Cozmo.Robot.Vision;

//0061F8EC /00627F92..00628064: copied filter and copied mutable bounds, LAST accepted result.
internal static class NativeClosestSpatialQuery
{
    internal static ObservableObject? Find(BlockWorld world,Pose3d target,Vec3 bounds,BlockWorldFilter supplied)
    {
        using var mutation=world.EnterPoseMutation();
        var filter=supplied.Clone();
        var current=new Vec3((float)bounds.X,(float)bounds.Y,(float)bounds.Z);
        float Norm(Vec3 v)=>MathF.Sqrt(PlacementProducerGeometry.NormSquared(v));
        filter.Predicates.Add(candidate=>
        {
            var delta=PlacementProducerGeometry.Vector(target,candidate.NativePose); //failed conversion logs and retains initialized ZERO, as source wrapper.
            var absolute=new Vec3(MathF.Abs((float)delta.X),MathF.Abs((float)delta.Y),MathF.Abs((float)delta.Z));
            if(!(Norm(absolute)<Norm(current)))return false;
            current=absolute;return true;
        });
        return world.FindLocatedObjectHelper(filter,null,false);
    }
}
