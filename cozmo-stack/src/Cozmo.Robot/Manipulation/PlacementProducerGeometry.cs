using Cozmo.Robot.Vision;
namespace Cozmo.Robot.Manipulation;

// fidelity: M12-028 — OV02 B3-B6 and the checked vector/distance helpers.
internal static class PlacementProducerGeometry
{
    internal static bool TryVector(Pose3d first, Pose3d second, ref Vec3 output)
    {
        if (first.Node!.HasSameParent(second.Node!))
        {
            var a=first.Translation; var b=second.Translation;
            output=new Vec3((float)a.X-(float)b.X,(float)a.Y-(float)b.Y,(float)a.Z-(float)b.Z);
            return true;
        }
        using var relative=new Pose3d("");
        if (!Pose3d.GetWithRespectTo(first,second,relative)) return false;
        output=relative.Translation;
        return true;
    }
    internal static Vec3 Vector(Pose3d first, Pose3d second)
    {
        var output=Vec3.Zero;
        if (!TryVector(first,second,ref output))
            PoseDiagnostics.Error("ComputeVectorBetween.NoCommonParent","Could not get pose1 w.r.t. pose2.");
        return output;
    }
    internal static float NormSquared(Vec3 vector)
    {
        float x=(float)vector.X,y=(float)vector.Y,z=(float)vector.Z;
        float value=x*x; value+=y*y; value+=z*z; return value;
    }
    internal static bool TryDistanceSquared(Pose3d first, Pose3d second, ref float output)
    {
        var vector=Vec3.Zero;
        if (!TryVector(first,second,ref vector)) return false;
        output=NormSquared(vector);return true;
    }
    private static Vec3 Dimensions(ObservableObject obj)
    {
        using var flat=obj.NativePose.GetWithRespectToRoot();
        var matrix=flat.Rotation;var size=CubeGeometry.SizeOf(obj.Type);
        double Axis(int axis)=>Math.Abs(RotatedParentAxis.Get(matrix,axis)) switch {3=>size.Z,2=>size.Y,_=>size.X};
        return new Vec3(Axis(0),Axis(1),Axis(2));
    }
    internal static Pose3d Center(ObservableObject obj)
    {
        using var flat=obj.NativePose.GetWithRespectToRoot();using var root=obj.NativePose.FindRoot();
        // M12-033 preserves local x/y and root-relative z/yaw; scalar argument is +0.
        var local=obj.NativePose.Translation;
        float z=(float)flat.Translation.Z+(float)Dimensions(obj).Z*0f;
        return new Pose3d(Rotation3d.FromAngleAxis((float)flat.AngleAroundZ,new Vec3(0,0,1)),new Vec3(local.X,local.Y,z),"",root);
    }
    internal static bool CheckBaseBlockPoseIsFree(ManipulationSystem m,uint staticId,uint movingId)
    {
        var stat=m.World.GetLocatedObjectById(staticId);var moving=m.World.GetLocatedObjectById(movingId);
        if (stat is null || moving is null)
        {
            m.Robot.Engine.EmitSourceLog(EngineLogLevel.Warning,null,"BehaviorBuildPyramidBase.CHeckBaseBlockPoseIsFree.NullObject",FormattableString.Invariant($"Static block with id {unchecked((int)staticId)} or base block with id {unchecked((int)movingId)} is NULL"));
            return false;
        }
        var dimensions=Dimensions(moving);
        using var center=Center(stat);
        using var query=new Pose3d(Rotation3d.Identity,new Vec3(dimensions.X,dimensions.Y,0),"",center);
        using var flat=query.GetWithRespectToRoot();
        var threshold=dimensions;bool found=false;
        foreach(var obj in m.World.LocatedObjects)
        {
            if (obj.ObjectId==staticId || obj.OriginId!=m.World.CurrentOriginId || obj.Family is not (ObjectFamily.LightCube or ObjectFamily.Block)) continue;
            var vector=Vector(flat,obj.NativePose);
            var abs=new Vec3(MathF.Abs((float)vector.X),MathF.Abs((float)vector.Y),MathF.Abs((float)vector.Z));
            float distance=MathF.Sqrt(NormSquared(abs));float limit=MathF.Sqrt(NormSquared(threshold));
            if (distance<limit) { threshold=abs;found=true; }
        }
        return !found;
    }
    internal static void UpdateOffsets(ManipulationSystem m,uint staticId,uint movingId,ref float a,ref float b)
    {
        using var mutation=m.World.EnterPoseMutation();
        using var logs=Pose3d.UseSourceLog((l,c,k,t)=>m.Robot.Engine.EmitSourceLog(l,c,k,t),m.Robot.Engine.Das.ForceFlushNow);
        var stat=m.World.GetLocatedObjectById(staticId);
        if (stat is null)
        {
            m.Robot.Engine.EmitSourceLog(EngineLogLevel.Warning,null,"BehaviorBuildPyramidBase.UpdateBlockPlacementOffset.NullObject",FormattableString.Invariant($"Static block with id {unchecked((int)staticId)} is NULL"));return;
        }
        var size=Dimensions(stat);float dx=(float)size.X+12f,dy=(float)size.Y+12f;
        var candidates=new (float X,float Y)[]{(dx,0),(0,dy),(0,-dy),(-dx,0)};
        a=dx;b=0;float best=float.MaxValue;
        foreach(var candidate in candidates)
        {
            if (!CheckBaseBlockPoseIsFree(m,staticId,movingId)) continue;
            using var center=Center(stat);
            using var child=new Pose3d(Rotation3d.Identity,new Vec3(candidate.X,candidate.Y,-(float)size.Z),"",center);
            float distance=candidate.X; // failed out-conversion retains this aliased input word.
            TryDistanceSquared(m.Robot.Engine.Robot!.GetCheckedMainPose(),child,ref distance);
            if (distance<best) { best=distance;a=candidate.X;b=candidate.Y; }
        }
    }
}
