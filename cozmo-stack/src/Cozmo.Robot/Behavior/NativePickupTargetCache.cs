using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
namespace Cozmo.Robot.Behavior;

// Actual shared object-intention owner. 0:00570398; proposed Stack entry bindings C1-C5:0056F724/57154C.
internal sealed class NativePickupTargetCache(ManipulationSystem m,EngineRobot robot)
{
    private sealed class Entry
    {internal float Last=-1f;internal uint Best=uint.MaxValue;internal IReadOnlyList<uint> Valid=Array.Empty<uint>();internal bool Available=true;internal int Rebuilds;}
    private readonly Dictionary<int,Entry> _entries=new(){{0,new()},{2,new()},{3,new()},{4,new()},{5,new()},{6,new()},{7,new()}};
    internal IReadOnlyList<uint> ValidIds=>_entries[0].Valid;
    internal uint Best=>_entries[0].Best;
    internal bool Available=>_entries[0].Available;
    internal bool Refresh()=>Refresh(0);
    internal uint GetBest(int intention)
    {if(!Refresh(intention))throw new NativeConfigurationUnavailable("intention "+intention+" input unavailable");return _entries[intention].Best;}
    internal IReadOnlyList<uint> GetValid(int intention)
    {if(!Refresh(intention))throw new NativeConfigurationUnavailable("intention "+intention+" input unavailable");return _entries[intention].Valid;}
    internal (float Last,uint Best,int Rebuilds) Inspect(int intention)=> (_entries[intention].Last,_entries[intention].Best,_entries[intention].Rebuilds);
    private bool Refresh(int intention)
    {
        using var mutation=m.World.EnterPoseMutation(); //retain candidate poses through collection AND DefaultBest conversion.
        if(!_entries.TryGetValue(intention,out var entry))throw new NativeConfigurationUnavailable("ordinary intention "+intention+" is not built; no 5/3 substitution");
        if(intention==3&&!Refresh(5))return false; // source dependency, BEFORE this entry's timestamp gate.
        if(intention==2&&!Refresh(4))return false; // O2: independent ordinary dependency, also before its own stamp.
        float now=robot.Engine.Timer.SecondsF;
        if(MathF.Abs(entry.Last-now)<BitConverter.UInt32BitsToSingle(0x3727C5AC))return entry.Available;
        entry.Last=now; // source stamps before collection, even if a host availability guard stops reconstruction.
        try
        {
            var geometry=new NativeConfigurationGeometry(m.World,robot);
            var ids=geometry.Query(obj=>intention switch{0=>Eligible(obj),5=>Top(obj,false),4=>Top(obj,true),3=>Bottom(obj,5,false),2=>Bottom(obj,4,true),6=>RollEligible(obj,false),7=>RollEligible(obj,true),_=>false}).Select(o=>o.ObjectId).Distinct().OrderBy(id=>unchecked((int)id)).ToArray();
            uint best=ids.Contains(entry.Best)?entry.Best:(intention is 6 or 7?SelectRoll(ids):Select(ids));
            entry.Valid=Array.AsReadOnly(ids);entry.Best=best;entry.Available=true;entry.Rebuilds++;return true;
        }
        catch(NativeConfigurationUnavailable error)
        {entry.Available=false;SteppedBehavior.ReportMissing("Intention"+intention+" target cache unavailable; prior complete IDs/best retained: "+error.Message);return false;}
    }
    // Proposed Roll own-path H4/H5:00570676..00570902; generic cache clock/retained-best above is unchanged.
    internal bool RollEligible(ObservableObject obj,bool axis)
    {
        using var root=axis?obj.NativePose.GetWithRespectToRoot():null; // Only AxisCheck flattens BEFORE NoAxisCheck.
        if(m.Whiteboard.DidFailToUse(unchecked((int)obj.ObjectId),new[]{(ObjectActionFailure)3},60f,obj.NativePose,30f,Radians.Pi))return false;
        if(obj.PoseState is not (PoseState.Known or PoseState.Dirty))return false;
        if(!obj.IsRestingFlat(BitConverter.UInt32BitsToSingle(0x3E32B8C2)))return false;
        using var relative=new Pose3d();if(!Pose3d.GetWithRespectTo(obj.NativePose,robot.GetCheckedMainPose(),relative))return false;
        if(CubeGeometry.IsPoseTooHigh(obj,relative,2f,15f,.5f))return false;
        if(m.NativeConfigurations.Snapshot.Stacks.Any(stack=>stack.BlockIds.Count>=3&&stack.ContainsBlock(obj.ObjectId)))return false;
        return !axis||RotatedParentAxis.GetNativeZ(root!.Rotation)!=3;
    }
    private uint SelectRoll(IReadOnlyList<uint> ids)
    {
        var preferred=ids.Where(id=>m.World.GetLocatedObjectByIdHelper(id) is {} obj&&CanPick(obj)&&RotatedParentAxis.GetNativeZ(obj.NativePose.Rotation)!=3).ToArray();
        return Select(preferred.Length==0?ids:preferred);
    }
    // Roll's actual CanActionRollObject recipient0063C880: common interaction then(.5,10,0).
    internal bool CanRollFromGround(ObservableObject obj)=>CanInteract(obj,.5f,10f,0f);
    internal bool Top(ObservableObject obj,bool axis)
    {
        uint carried=robot.CarryingOwner is {} carry?carry.CarriedObjectId??uint.MaxValue:throw new NativeConfigurationUnavailable("actual carry owner absent");
        if(carried!=uint.MaxValue)
        {
            bool same=ReferenceEquals(m.World.GetLocatedObjectByIdHelper(carried),obj);
            if(!axis)return same;
            bool aligned=RotatedParentAxis.GetNativeZ(obj.NativePose.Rotation)==3; // O3 LOCAL, not root.
            return same & aligned;
        }
        if(!Eligible(obj))return false;
        if(!axis)return true;
        if(robot.Progression is not {} progression||!progression.TryIsUnlocked(9,out bool unlocked))
            throw new NativeConfigurationUnavailable("CanPickupAxisCheck actual progression9 unavailable");
        if(!unlocked)return true; // O4 literal direction: only unlocked==1 reaches the ROOT axis test.
        using var root=obj.NativePose.GetWithRespectToRoot();
        return RotatedParentAxis.GetNativeZ(root.Rotation)==3;
    }
    private bool Bottom(ObservableObject obj,int top,bool axis)
    {
        if(obj.ObjectId==_entries[top].Best)return false;
        if(m.Whiteboard.DidFailToUse(unchecked((int)obj.ObjectId),new[]{(ObjectActionFailure)1},3f,obj.NativePose,60f,BitConverter.UInt32BitsToSingle(0x40490FDB)))return false;
        if(obj.Family!=(ObjectFamily)2||!CanStack(obj))return false;
        return !axis||RotatedParentAxis.GetNativeZ(obj.NativePose.Rotation)==3; // O5 LOCAL only after shared helper accepts.
    }
    internal uint Select(IReadOnlyList<uint> ids)
    {
        if(ids.Count==0)return uint.MaxValue;if(ids.Count==1)return ids[0];
        uint best=uint.MaxValue;float distance=float.MaxValue;
        var snapshot=m.World.NativeConfigurations?.Snapshot??throw new NativeConfigurationUnavailable("actual native configurations absent");
        void Consider(uint id)
        {
            if(!ids.Contains(id))return;
            var obj=m.World.GetLocatedObjectByIdHelper(id);
            if(obj is null){SteppedBehavior.ReportMissing("M1-034 intention0 null stack-top00570BC8: skip candidate, retain prior best");return;}
            float value=0;
            if(PlacementProducerGeometry.TryDistanceSquared(robot.GetCheckedMainPose(),obj.NativePose,ref value)&&value<distance)
            {distance=value;best=id;}
        }
        foreach(var stack in snapshot.Stacks)Consider(stack.Top);
        foreach(uint id in ids)
        {if(best!=uint.MaxValue&&snapshot.Stacks.Any(s=>s.Bottom==id))continue;Consider(id);}
        return best;
    }
    internal BlockWorldFilter Filter(Func<ObservableObject,bool>? extra=null)
    {
        var filter=new BlockWorldFilter();filter.AllowedFamilies.UnionWith(new[]{(ObjectFamily)1,(ObjectFamily)2});
        filter.Predicates.Add(Eligible);if(extra is not null)filter.Predicates.Add(extra);return filter;
    }
    private bool Eligible(ObservableObject obj)
    {
        using var root=obj.NativePose.GetWithRespectToRoot();
        bool failed=m.Whiteboard.DidFailToUse(unchecked((int)obj.ObjectId),new[]{(ObjectActionFailure)0,(ObjectActionFailure)3},30f,root,60f,BitConverter.UInt32BitsToSingle(0x40490FDB));
        bool can=CanPick(obj); // source evaluates docking even if whiteboard matched.
        return !failed & can;
    }
    internal bool CanPick(ObservableObject obj)=>CanInteract(obj,2f);
    // fidelity: M12-012 — actual native CanStack body, without the host/legacy configuration adapter.
    internal bool CanStack(ObservableObject obj)=>CanInteract(obj,1f);
    private bool CanInteract(ObservableObject obj,float height,float tolerance=15f,float fraction=.5f)
    {
        if(obj.Family is not (ObjectFamily.Block or ObjectFamily.LightCube))return false;
        if(!obj.IsRestingFlat(BitConverter.UInt32BitsToSingle(0x3E32B8C2)))return false;
        uint carried=robot.CarryingOwner?.CarriedObjectId??(robot.CarryingOwner is null?throw new NativeConfigurationUnavailable("actual carry owner absent"):uint.MaxValue);
        if(carried!=uint.MaxValue&&obj.ObjectId==carried)return false;
        using var relative=new Pose3d();
        if(!Pose3d.GetWithRespectTo(obj.NativePose,robot.GetCheckedMainPose(),relative))return false;
        if(new NativeConfigurationGeometry(m.World,robot).Neighbor(obj,true,15f,new BlockWorldFilter()) is not null)return false;
        return !TooHigh(obj,relative,height,tolerance,fraction);
    }
    private static bool TooHigh(ObservableObject obj,Pose3d relative,float height,float tolerance,float fraction)
    {
        if(!CubeGeometry.IsCube(obj.Type))throw new NativeConfigurationUnavailable("CanPick admitted virtual dimensions unavailable for "+obj.Type);
        return CubeGeometry.IsPoseTooHigh(obj,relative,height,tolerance,fraction);
    }
}
