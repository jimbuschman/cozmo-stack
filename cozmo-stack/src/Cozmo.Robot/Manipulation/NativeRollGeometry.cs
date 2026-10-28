using Cozmo.Protocol;
using Cozmo.Robot.Vision;
namespace Cozmo.Robot.Manipulation;

// Proposed Roll R10/R12: exact caller operands/order; ONLY imported atan2f004A4510 body is equivalent (M13-023(e)/M4-052).
internal static class NativeRollGeometry
{
    internal static short TopMarker(ObservableObject obj)
    {
        float best=-float.MaxValue;KnownMarker? selected=obj.Markers.FirstOrDefault();
        foreach(var marker in obj.Markers)
        {
            using var pose=new Pose3d(marker.PoseOnObject);pose.SetParent(obj.NativePose);
            using var root=pose.GetWithRespectToRoot();var n=RelativeCubeLights.ComputeNormal(marker,root);
            float dot=(float)n.X*0f;dot+=(float)n.Y*0f;dot+=(float)n.Z*1f;
            if(dot>best){best=dot;selected=marker;}
        }
        if(selected is null)throw new NativeActionUnavailable("M1-034 proposed Roll GetTopMarker004E69B8: empty marker sentinel read; skip owned action");
        return unchecked((short)selected.Code);
    }
    internal static bool TryApproach(ManipulationSystem m,uint id,out float angle)
    {
        angle=0;using var mutation=m.World.EnterPoseMutation();var owner=m.Robot.Engine.Robot!;
        var obstacles=m.NativeObstacles(owner.GetCheckedMainPose());var target=m.World.GetLocatedObjectByIdHelper(id);
        if(target is null||target.Family is not (ObjectFamily.Block or ObjectFamily.LightCube)||!CubeGeometry.IsCube(target.Type))return false;
        var poses=new List<PreActionPose>();
        try
        {
            CubePreActionPoses.GetCurrentPreActionPoses(target,new[]{PreActionType.Rolling},Array.Empty<MarkerType>(),owner.GetCheckedMainPose(),0,obstacles,poses);
            var bottom=target.Markers.FirstOrDefault(marker=>marker.Face==BlockFace.Bottom);if(bottom is null)return false;
            var chosen=poses.FirstOrDefault(p=>p.Marker.Code==bottom.Code);if(chosen is null)return false;
            var vector=PlacementProducerGeometry.Vector(target.NativePose,chosen.WorldPose);
            angle=MathF.Atan2((float)vector.Y,(float)vector.X);return true;
        }
        finally{foreach(var pose in poses)pose.WorldPose.Dispose();}
    }
}
