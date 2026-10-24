using Cozmo.Robot.Behavior;
namespace Cozmo.Robot.Vision;

// fidelity: M11-075
internal readonly record struct SourceMapAging(uint Now, uint CliffThreshold, uint EdgeThreshold)
{
    internal static SourceMapAging At(uint now)
    {
        float stamp=now;
        uint cliff=now<30001?0:(uint)(stamp+BitConverter.UInt32BitsToSingle(0xC6EA6000));
        uint edge=now<120001?0:(uint)(stamp+BitConverter.UInt32BitsToSingle(0xC7EA6000));
        return new(now,cliff,edge);
    }
    // Native callback 0x006808F4: replace with fresh base data, not a polygon deletion.
    internal SourceMapData Apply(SourceMapData data)
    {
        uint threshold;
        if(data.Type==MemoryMapContentType.Cliff)threshold=CliffThreshold;
        else if(data.Type is MemoryMapContentType.InterestingEdge or MemoryMapContentType.NotInterestingEdge)threshold=EdgeThreshold;
        else return data;
        return data.Last>threshold?data:new SourceMapData(MemoryMapContentType.Unknown,Now,Now);
    }
}

// fidelity: M11-074 — callback instructions 0x006810F2..0x00681172.
internal readonly record struct SourceObjectRemoval(uint ObjectId,MemoryMapContentType RemovalType,uint Timestamp)
{
    internal SourceMapData Apply(SourceMapData data)
    {
        if(data.Type!=MemoryMapContentType.ObstacleObservable)return data; // before the derived read
        if(data is not ObservableMapData observable)
        {
            StackDiagnostics.Write("M1-034 M11-074 skip forged type-3 removal layout read at 0x0068110E; prior data retained");
            return data;
        }
        return observable.ObjectId==ObjectId ? new SourceMapData(RemovalType,Timestamp,Timestamp) : data;
    }
}

// fidelity: M11-075, M11-074
internal sealed partial class SourceMapComponent : IDisposable
{
    private readonly Dictionary<uint,Dictionary<uint,(Pose3d Pose,bool Added)>> _objectPoses = new();
    internal Func<Pose3d?>? RobotPose;
    internal Func<uint?>? ImageTimestamp;
    internal (Pose3d Pose,bool Added)? CachedObjectPose(uint id,uint root) =>
        _objectPoses.TryGetValue(id,out var entries) && entries.TryGetValue(root,out var value) ? value : null;
    private void CacheObjectPose(uint id,uint root,Pose3d pose,bool added)
    {
        if(!_objectPoses.TryGetValue(id,out var entries))_objectPoses.Add(id,entries=new());
        if(entries.Remove(root,out var previous))previous.Pose.Dispose();
        entries.Add(root,(new Pose3d(pose),added));
    }
    public void Dispose()
    {
        foreach(var entries in _objectPoses.Values)foreach(var value in entries.Values)value.Pose.Dispose();
        _objectPoses.Clear();
        _lastRobotPose?.Dispose();_lastRobotPose=null;
    }
    // GetRootID 0x00844B0A: null wrapper returns zero; otherwise the same parent walk as FindRoot.
    private static uint PoseRootId(Pose3d pose)
    {if(pose.Node is null)return 0;using var root=pose.FindRoot();return root.Id;}
    internal Action<string>? ObjectRouteForTest; // observation only; no replacement map operation
    internal void UpdateObjectPose(ObservableObject obj,Pose3d? oldPose,PoseState oldState)
    {
        if(obj.Family==ObjectFamily.MarkerlessObject)return;
        if(oldState==PoseState.Unknown)
        {
            if(obj.PoseState!=PoseState.Unknown){AddObject();return;}
            SourceLog?.Invoke(EngineLogLevel.Error,null,"MapComponent.OnObjectPoseChanged.BothStatesAreInvalid",$"Object {unchecked((int)obj.ObjectId)} changing from Invalid to Invalid");
            Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak();return;
        }
        if(obj.PoseState==PoseState.Unknown)
        {
            ObjectRouteForTest?.Invoke("remove");
            // fidelity: M11-088 -- r2 is the old pose; GetRootID precedes Remove's tailcall.
            if(oldPose is null)
            { StackDiagnostics.Write("M1-034 M11-088: skip Remove old-pose null read at 0x0067EC3C; map state retained");return; }
            uint oldRootId=ReadOldPoseRootId?.Invoke(oldPose) ?? PoseRootId(oldPose);
            if(RemoveObservableObject67F2C4 is {} remove)remove(obj,oldRootId);
            else RemoveObject(obj,oldRootId);
            return;
        }
        ObjectRouteForTest?.Invoke("cache");
        // 0x0067EB58 creates the outer entry even if Add later finds no map.
        if(!_objectPoses.ContainsKey(obj.ObjectId))_objectPoses.Add(obj.ObjectId,new());
        uint root=PoseRootId(obj.NativePose);
        if(CachedObjectPose(obj.ObjectId,root) is {} cached)
        {
            if(cached.Added && obj.NativePose.IsSameAs(cached.Pose,5f,BitConverter.UInt32BitsToSingle(0x3E32B8C2)))return;
            if(obj.ObservableMapSlot5C==1)
            {
                ObjectRouteForTest?.Invoke("remove");
                if(RemoveObservableObject67F2C4 is {} remove)remove(obj,root);else RemoveObject(obj,root);
            }
        }
        AddObject();
        void AddObject()
        {
            ObjectRouteForTest?.Invoke("add");
            if(AddObservableObject67ECFC is {} add)add(obj,obj.NativePose);
            else AddObjectToMap(obj,obj.NativePose);
        }
    }
    // fidelity: M11-074, M13-040, M13-017 — M5-M8, 0x0067ECFC..0x0067F0CC.
    private void AddObjectToMap(ObservableObject obj,Pose3d supplied)
    {
        var type=MemoryMapTypes.ContentTypeForFamily(obj.Family,true);
        if(type==MemoryMapContentType.Unknown)return;
        uint root=PoseRootId(supplied);
        if(!Maps.TryGetValue(root,out var map))return; // before height or cache; never create a map here
        if(map is null){StackDiagnostics.Write("M1-034 M11-074: skip null MemoryMap at 0x0067F002; prior state retained");return;}
        using var relative=RelativeObjectPose(obj,supplied);
        if(relative is null){InvalidRelativePose();return;}
        if(CubeGeometry.IsPoseTooHigh(obj,relative,1f,15f,0f))
        {CacheObjectPose(obj.ObjectId,root,supplied,false);return;}
        using var flat=supplied.GetWithRespectToRoot();
        if(!obj.TryObservableBoundingQuad50(flat,0,out var quad))return;
        if(type!=MemoryMapContentType.ObstacleObservable)
            SourceLog?.Invoke(EngineLogLevel.Warning,null,"MapComponent.AddObservableObject.AddedNonObservableType","AddObservableObject was called to add a non observable object");
        if(ImageTimestamp is not {} clock)
        {SteppedBehavior.ReportMissing("M11-074 Robot::GetLastImageTimeStamp 0x00516EC0 called at 0x0067EFCE: owner is unbound");return;}
        if(clock() is not {} now)return; // unbound host input has already reported its gap
        var polygon=Cozmo.Robot.Manipulation.PolygonF.ImportQuad2d(Enumerable.Range(0,4)
            .Select(i=>new Cozmo.Robot.Manipulation.P2f(quad![i].X,quad[i].Y)).ToArray());
        SourceMapData data=type==MemoryMapContentType.ObstacleObservable
            ?new ObservableMapData(obj.ObjectId,polygon.ToArray(),now):new SourceMapData(type,now,now);
        map.InsertObject(polygon.Select(p=>new Vec2(p.X,p.Y)).ToArray(),data);
        CacheObjectPose(obj.ObjectId,root,flat,true); // Insert's result is deliberately ignored
        if(clock() is {} borderTime)map.FillObjectBorder(borderTime);
    }
    private Pose3d? RelativeObjectPose(ObservableObject obj,Pose3d supplied)
    {
        if(ObjectPoseRelativeToRobot is {} input)return input(obj,supplied);
        if(RobotPose?.Invoke() is not {} robot)
        {SteppedBehavior.ReportMissing("M11-074 Robot::GetPose 0x004EA398 called at 0x0067ED98: owner is unbound");return null;}
        var relative=new Pose3d();
        if(supplied.TryWithRespectTo(robot,relative))return relative;
        relative.Dispose();return null;
    }
    private void InvalidRelativePose()=>SourceLog?.Invoke(EngineLogLevel.Warning,null,"MapComponent.AddObservableObject.InvalidPose","Could not get object's new pose wrt robot. Won't add to map");
    private void RemoveObject(ObservableObject obj,uint root)
    {
        var type=MemoryMapTypes.ContentTypeForFamily(obj.Family,false);
        if(type==MemoryMapContentType.Unknown || !Maps.TryGetValue(root,out var map))return;
        if(map is null){StackDiagnostics.Write("M1-034 M11-074: skip null MemoryMap at 0x0067F368; prior state retained");return;}
        if(ImageTimestamp is not {} clock)
        {SteppedBehavior.ReportMissing("M11-074 Robot::GetLastImageTimeStamp 0x00516EC0 called at 0x0067F32C: owner is unbound");return;}
        if(clock() is {} now)map.RemoveObject(new SourceObjectRemoval(obj.ObjectId,type,now));
    }
    // fidelity: M11-088 -- checked dispatch and height input; M11-074 owns the remaining cache/map recipients.
    internal Action<ObservableObject,Pose3d>? AddObservableObject67ECFC;
    internal Action<ObservableObject,uint>? RemoveObservableObject67F2C4;
    internal Func<Pose3d,uint>? ReadOldPoseRootId;
    internal Func<ObservableObject,Pose3d,Pose3d?>? ObjectPoseRelativeToRobot;
    internal Action<ObservableObject,Pose3d,bool>? AddObjectAfterHeight;
    internal void AddObjectCheckedHeight(ObservableObject obj,Pose3d newPose)
    {
        if(ObjectPoseRelativeToRobot is not {} relativeInput)
        {SteppedBehavior.ReportMissing("M11-074/M11-088 native robot-relative pose input 0x0067EDA2 before IsPoseTooHigh 0x0067EDC0");return;}
        using var relative=relativeInput(obj,newPose);
        if(relative is null)
        {SourceLog?.Invoke(EngineLogLevel.Warning,null,"MapComponent.AddObservableObject.InvalidPose","Could not get object's new pose wrt robot. Won't add to map");return;}
        bool high=CubeGeometry.IsPoseTooHigh(obj,relative,1f,15f,0f);
        if(AddObjectAfterHeight is {} continuation)continuation(obj,newPose,high);
        else SteppedBehavior.ReportMissing("M11-074 native pose-cache/map insert continuation 0x0067EDC8 / 0x0067EEC4 after robot-relative height; no fabricated map insertion");
    }
    internal SourceMapAging? LastAgingForTest; // observation only
    internal Action? BroadcastDueForTest; // observation only; never supplies a stream
    internal void SetRenderEnabled(byte value)=>Enabled=value; // G2E144, 0x006807EA
    internal void SetBroadcastInterval(float value,float timer)
    {BroadcastInterval=value;BroadcastDeadline=timer;} // G2E145, 0x0068085E

    // MapComponent+0x2C (0x0067E252/0x0067E486), retained across map/origin changes.
    private Pose3d? _lastRobotPose;
    internal MemoryMapRegion? UpdateRobotPose(Pose3d pose,bool cliff=false,uint timestamp=0,
        Action<Cozmo.Robot.Manipulation.WhiteboardClearQuad>? clear=null)
    {
        using var current=new Pose3d(pose);
        if(_lastRobotPose is { HasParent:true } last &&
            current.IsSameAs(last,new Vec3(8,8,8),MemoryMap.RobotPoseTurnRad,out _)) return null;
        if(Current is {} map)
        {
            var region=map.UpdateRobotPose(current,cliff,timestamp,clear);
            _lastRobotPose?.Dispose();_lastRobotPose=new Pose3d(current);
            return region;
        }
        StackDiagnostics.Write("M1-034 M11-074 null map at 0x0067E38C/0x0067E3E4: skip robot-pose writes and save; prior state retained");
        return null;
    }
    internal void Update(uint timestamp,float timer)
    {
        if(CurrentOrigin!=0 && Current is {} map)
        {
            var aging=SourceMapAging.At(timestamp);LastAgingForTest=aging;
            map.TransformAging(aging);
        }
        // Unknown origin or null map skips only aging; scheduler still runs.
        BroadcastMap(timer);
    }
    internal void BroadcastMap(float timer)
    {
        if(!(BroadcastInterval>=0))return; // negative and NaN return, -0 passes
        float adjusted=timer+BitConverter.UInt32BitsToSingle(0x3727C5AC);
        if(adjusted<BroadcastDeadline)return;
        // Defined source loop: zero/stalled positive intervals are not clamped under M1-034.
        do{BroadcastDeadline=BroadcastInterval+BroadcastDeadline;}while(adjusted>=BroadcastDeadline);
        BroadcastDueForTest?.Invoke();
        if(CurrentOrigin==0 || !Maps.TryGetValue(CurrentOrigin,out var map))return;
        if(map is null)
        {
            StackDiagnostics.Write("M1-034 M11-075 null map at 0x0067DE2A: skip map broadcast after stored deadline; prior map state retained");return;
        }
        SteppedBehavior.ReportMissing("M11-075 MemoryMap stream vt+0x44 at 0x0067DE2A is unreachable with the shipped app's default -1 interval and remains unexposed; no substitute stream is sent");
    }
}
