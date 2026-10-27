using Cozmo.Robot.Vision;
namespace Cozmo.Robot.Manipulation;

// fidelity: M12-049, M12-043, M13-007, M12-012, M12-033
// CM13-05 rows15-26/30 and checked MAX-first/flattened-reference/FIRST-neighbor clarifications.
internal sealed class NativeConfigurationGeometry
{
    private readonly BlockWorld _world;
    private readonly EngineRobot _robot;
    private readonly Pose3d _worldOrigin;
    internal NativeConfigurationGeometry(BlockWorld world,EngineRobot? robot)
    {
        _world=world;_robot=robot??throw new NativeConfigurationUnavailable("actual EngineRobot is absent");
        if(!robot.TryGetNativeOriginOwner(robot.CurrentOriginId,out var origin))throw new NativeConfigurationUnavailable("actual world-origin owner is absent");
        _worldOrigin=origin!;Require(robot.GetCheckedMainPose());_ = Carried;
    }
    private uint Carried=>_robot.CarryingOwner is {} owner?owner.CarriedObjectId??uint.MaxValue:throw new NativeConfigurationUnavailable("actual CarryingOwner is absent");
    private static void Require(Pose3d pose)
    {if(pose.Node is null)throw new NativeConfigurationUnavailable("native pose node is absent");}
    private static BlockWorldFilter Filter()
    {var f=new BlockWorldFilter();f.AllowedFamilies.Add((ObjectFamily)2);f.AllowedFamilies.Add((ObjectFamily)1);return f;}
    internal List<ObservableObject> Query(Func<ObservableObject,bool>? predicate=null)
    {
        var f=Filter();if(predicate is not null)f.Predicates.Add(predicate);
        return _world.FindLocatedMatchingObjects(f);
    }
    private static bool Flat(ObservableObject o)
    {Require(o.NativePose);return o.IsRestingFlat(BitConverter.UInt32BitsToSingle(0x3E32B8C2));}
    private static Vec3 Dimensions(ObservableObject obj,Pose3d flat)
    {
        if(!CubeGeometry.IsCube(obj.Type))throw new NativeConfigurationUnavailable("admitted Block dimensions are not bound for "+obj.Type);
        var r=flat.Rotation;var size=CubeGeometry.SizeOf(obj.Type);
        float Axis(int axis)=>(float)(Math.Abs(RotatedParentAxis.Get(r,axis)) switch{3=>size.Z,2=>size.Y,_=>size.X});
        return new(Axis(0),Axis(1),Axis(2));
    }
    private static bool Ground(ObservableObject obj)
    {
        Require(obj.NativePose);using var flat=obj.NativePose.GetWithRespectToRoot();
        float dim=(float)Dimensions(obj,flat).Z,half=dim*0.5f;
        float surface=(float)flat.Translation.Z-half;
        return MathF.Abs(0f-surface)<20f; // 0x0087767C, strict, no epsilon
    }
    private static Quadrilateral Quad(ObservableObject obj,Pose3d pose)
    {
        Require(pose);
        if(!Footprint.TryGetBoundingQuadXY(obj,pose,0,out var quad))
            throw new NativeConfigurationUnavailable("M13-023/024 admitted virtual footprint unavailable (not a native false)");
        return quad!;
    }
    internal ObservableObject? Neighbor(ObservableObject obj,bool onTop,float tolerance=30f,BlockWorldFilter? suppliedFilter=null)
    {
        Require(obj.NativePose);using var reference=obj.NativePose.GetWithRespectToRoot(); // 0x00626048
        using var parent=reference.FindRoot();var refQuad=Quad(obj,reference);
        float z=(float)reference.Translation.Z,dim=(float)Dimensions(obj,reference).Z;
        float target=z+dim*(onTop?0.5f:-0.5f);
        var filter=suppliedFilter?.Clone()??Filter();filter.IgnoreIds.Add(obj.ObjectId);
        filter.Predicates.Add(candidate=>
        {
            Require(candidate.NativePose);using var relative=new Pose3d();
            if(!Pose3d.GetWithRespectTo(candidate.NativePose,parent,relative))return false;
            var saved=relative.Translation;
            relative.Node!.Transform=new(relative.Quaternion,new Vec3(saved.X,saved.Y,z));
            var quad=Quad(candidate,relative);
            relative.Node.Transform=new(relative.Quaternion,saved); // source restores Z before dimensions/height
            if(!refQuad.Intersects(quad))return false;
            float candidateDim=(float)Dimensions(candidate,relative).Z;
            float surface=(float)saved.Z+candidateDim*(onTop?-0.5f:0.5f);
            return MathF.Abs(target-surface)<=tolerance+BitConverter.UInt32BitsToSingle(0x3727C5AC);
        });
        return _world.FindLocatedObjectHelper(filter,null,true); // FIRST, not collect-all's last return
    }
    internal IEnumerable<NativeStackConfiguration> BuildStack(ObservableObject seed)
    {
        var flat=Query(Flat);var up=new List<ObservableObject>();var down=new List<ObservableObject>();
        var current=seed;
        for(int i=0;i<10;i++)
        {
            var next=Neighbor(current,true);if(next is null)break;
            uint carried=Carried;if(carried!=uint.MaxValue&&next.ObjectId==carried)break;
            up.Add(next);current=next;if(i==9)Overflow();
        }
        current=seed;
        for(int i=0;i<10;i++)
        {
            var next=Neighbor(current,false);if(next is null)break;
            // 0x00619358..378: DOWN has only the null check, unlike UP's raw carry exclusion.
            down.Add(next);current=next;if(i==9)Overflow();
        }
        uint raw=Carried;
        if(raw!=uint.MaxValue&&seed.ObjectId==raw||flat.Count==0||up.Count+down.Count==0)yield break;
        if(!flat.Any(o=>ReferenceEquals(o,seed))&&(down.Count==0||!flat.Any(o=>ReferenceEquals(o,down[^1]))))yield break;
        var ordered=down.AsEnumerable().Reverse().Concat(new[]{seed}).Concat(up).ToArray();
        yield return ordered.Length==2?new(ordered[0].ObjectId,uint.MaxValue,ordered[1].ObjectId):new(ordered[0].ObjectId,ordered[1].ObjectId,ordered[2].ObjectId);
    }
    private static void Overflow()
    {
        PoseDiagnostics.Error("LoopBoundOverflow","Stack configuration neighbor search reached its 10-link bound"); //006192DC/00619388
    }
    internal IEnumerable<NativePyramidBase> BuildBases(ObservableObject seed)
    {
        var flat=Query(Flat);if(flat.Count<2)yield break;
        int index=flat.FindIndex(o=>ReferenceEquals(o,seed));if(index<0)yield break;flat.RemoveAt(index);
        foreach(var candidate in flat)if(FormsBase(seed,candidate))yield return new(seed.ObjectId,candidate.ObjectId);
    }
    internal bool FormsBase(ObservableObject a,ObservableObject b)
    {
        Require(a.NativePose);Require(b.NativePose);
        using var first=a.NativePose.GetWithRespectToRoot();using var second=b.NativePose.GetWithRespectToRoot();
        if(MathF.Abs((float)second.Translation.Z-(float)first.Translation.Z)>10f)return false;
        using var a0=new Pose3d();using var a1=new Pose3d();using var b0=new Pose3d();using var b1=new Pose3d();
        if(!InteriorCorners(a,b,a0,a1)||!InteriorCorners(b,a,b0,b1))return false;
        using var ac=Center(a,0.5f);using var bc=Center(b,0.5f);
        foreach(var pair in new[]{(a0,bc),(a1,bc),(b0,ac),(b1,ac)})
        {float distance=0;if(!PlacementProducerGeometry.TryDistanceSquared(pair.Item1,pair.Item2,ref distance)||!(distance<3600f))return false;}
        return true;
    }
    // Row20 scalar supplement00877574: ROOT dimensions/Z/yaw, ORIGINAL LOCAL XY, f32 multiply then add.
    internal static Pose3d Center(ObservableObject obj,float scalar)
    {
        Require(obj.NativePose);using var flat=obj.NativePose.GetWithRespectToRoot();using var root=flat.FindRoot();
        float product=(float)Dimensions(obj,flat).Z*scalar;
        float z=(float)flat.Translation.Z+product;
        var local=obj.NativePose.Translation;
        return new Pose3d(Rotation3d.FromAngleAxis((float)flat.AngleAroundZ,new Vec3(0,0,1)),new Vec3(local.X,local.Y,z),parent:root);
    }
    internal bool InteriorCorners(ObservableObject a,ObservableObject b,Pose3d first,Pose3d second)
    {
        var quad=Quad(a,a.NativePose);using var flat=a.NativePose.GetWithRespectToRoot();
        float dim=(float)Dimensions(a,flat).Z;var ranked=new List<(float Distance,Pose3d Pose)>();
        try
        {
            for(int i=3;i>=0;i--)
            {
                var p=new Pose3d(Rotation3d.Identity,new Vec3(quad[i].X,quad[i].Y,dim),parent:_worldOrigin);
                float distance=0;
                if(!PlacementProducerGeometry.TryDistanceSquared(p,b.NativePose,ref distance)){p.Dispose();return false;}
                int index=0;while(index<ranked.Count&&!(distance<ranked[index].Distance))index++;
                ranked.Insert(index,(distance,p)); // equal finite keys insert right, physical Q3,Q2,Q1,Q0
            }
            using var p0=ranked[0].Pose.GetWithRespectToRoot();using var p1=ranked[1].Pose.GetWithRespectToRoot();
            first.Assign(p0);second.Assign(p1);return true;
        }
        finally{foreach(var entry in ranked)entry.Pose.Dispose();}
    }
    internal bool InteriorMidpoint(ObservableObject a,ObservableObject b,Pose3d output)
    {
        using var first=new Pose3d();using var second=new Pose3d();
        if(!InteriorCorners(a,b,first,second))return false;
        using var fa=first.GetWithRespectToRoot();using var fb=second.GetWithRespectToRoot();
        using var flat=a.NativePose.GetWithRespectToRoot();
        float x=((float)fa.Translation.X+(float)fb.Translation.X)*0.5f;
        float y=((float)fa.Translation.Y+(float)fb.Translation.Y)*0.5f;
        Require(_robot.GetCheckedMainPose());using var root=_robot.GetCheckedMainPose().FindRoot();
        using var midpoint=new Pose3d(Rotation3d.Identity,new Vec3(x,y,(float)Dimensions(a,flat).Z),parent:root);
        output.Assign(midpoint);return true;
    }
    internal bool OnBase(NativePyramidBase basis,ObservableObject? top)
    {
        var a=_world.GetLocatedObjectByIdHelper(basis.MaxId);var b=_world.GetLocatedObjectByIdHelper(basis.MinId); // MAX first!
        if(a is null||b is null||top is null)return false;
        Require(a.NativePose);Require(b.NativePose);Require(top.NativePose);
        if(!PoseTreeNode.HasSameRoot(top.NativePose.Node,a.NativePose.Node)||!PoseTreeNode.HasSameRoot(b.NativePose.Node,a.NativePose.Node))return false;
        using var first=a.NativePose.GetWithRespectToRoot();using var upper=top.NativePose.GetWithRespectToRoot();
        var size=Dimensions(a,first);float topDim=(float)Dimensions(top,upper).Z;
        float bottom=(float)upper.Translation.Z-topDim*0.5f;
        float surface=(float)first.Translation.Z+MathF.Abs((float)size.Z)*0.5f;
        if(!(MathF.Abs(bottom-surface)<15f))return false;
        using var ab=new Pose3d();using var ba=new Pose3d();
        if(!InteriorMidpoint(a,b,ab)||!InteriorMidpoint(b,a,ba))return false;
        using var root=first.FindRoot();var local=top.NativePose.Translation;
        using var query=new Pose3d(Rotation3d.Identity,new Vec3(local.X,local.Y,0),parent:root);
        var tolerance=new Vec3((float)size.X*0.5f-5f,(float)size.Y*0.5f-5f,100);
        float pi=BitConverter.UInt32BitsToSingle(0x40490FDB);
        return query.IsSameAs(ab,tolerance,pi,out _)&&query.IsSameAs(ba,tolerance,pi,out _);
    }
    internal IEnumerable<NativePyramidConfiguration> BuildPyramids(ObservableObject seed,IReadOnlyList<NativePyramidBase> bases)
    {
        if(!Ground(seed))
        {
            foreach(var basis in bases)if(OnBase(basis,seed)){yield return new(basis,seed.ObjectId);yield break;}
            yield break;
        }
        var tops=Query(o=>o.PoseState==PoseState.Known&&!Ground(o));
        foreach(var basis in bases)
            if(basis.ContainsBlock(seed.ObjectId))foreach(var top in tops)if(OnBase(basis,top))yield return new(basis,top.ObjectId);
    }
}
