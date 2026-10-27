using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
namespace Cozmo.Robot.Behavior;

// Intention0 only:005711E4/00570B44/00570398. All inputs belong to the actual Robot.
internal sealed class NativePickupTargetCache(ManipulationSystem m,EngineRobot robot)
{
    private float _last=-1f;
    private uint _best=uint.MaxValue;
    private IReadOnlyList<uint> _valid=Array.Empty<uint>();
    internal IReadOnlyList<uint> ValidIds=>_valid;
    internal uint Best=>_best;
    internal bool Available {get;private set;}=true;
    internal bool Refresh()
    {
        using var mutation=m.World.EnterPoseMutation(); //retain candidate poses through collection AND DefaultBest conversion.
        float now=robot.Engine.Timer.SecondsF;
        if(MathF.Abs(_last-now)<BitConverter.UInt32BitsToSingle(0x3727C5AC))return Available;
        _last=now; // source stamps before collection, even if a host availability guard stops reconstruction.
        try
        {
            var geometry=new NativeConfigurationGeometry(m.World,robot);
            var ids=geometry.Query(Eligible).Select(o=>o.ObjectId).Distinct().OrderBy(id=>unchecked((int)id)).ToArray();
            uint best=ids.Contains(_best)?_best:Select(ids);
            _valid=Array.AsReadOnly(ids);_best=best;Available=true;return true;
        }
        catch(NativeConfigurationUnavailable error)
        {Available=false;SteppedBehavior.ReportMissing("Intention0 target cache unavailable; prior complete IDs/best retained: "+error.Message);return false;}
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
    internal bool CanPick(ObservableObject obj)
    {
        if(!obj.IsRestingFlat(BitConverter.UInt32BitsToSingle(0x3E32B8C2)))return false;
        uint carried=robot.CarryingOwner?.CarriedObjectId??(robot.CarryingOwner is null?throw new NativeConfigurationUnavailable("actual carry owner absent"):uint.MaxValue);
        if(carried!=uint.MaxValue&&obj.ObjectId==carried)return false;
        using var relative=new Pose3d();
        if(!Pose3d.GetWithRespectTo(obj.NativePose,robot.GetCheckedMainPose(),relative))return false;
        if(new NativeConfigurationGeometry(m.World,robot).Neighbor(obj,true,15f,new BlockWorldFilter()) is not null)return false;
        return !TooHigh(obj,relative);
    }
    private static bool TooHigh(ObservableObject obj,Pose3d relative)
    {
        if(!CubeGeometry.IsCube(obj.Type))throw new NativeConfigurationUnavailable("CanPick admitted virtual dimensions unavailable for "+obj.Type);
        return CubeGeometry.IsPoseTooHigh(obj,relative,2f,15f,.5f);
    }
}
