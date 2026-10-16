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

// fidelity: M11-075, M11-074
internal sealed partial class SourceMapComponent
{
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
            SteppedBehavior.ReportMissing("M11-074 RemoveObservableObject 0x0067EC3A/0x0067F2C4: old-pose root/map selection and whole-map transform recipient are not integrated; no polygon-list deletion substitute");return;
        }
        ObjectRouteForTest?.Invoke("cache");
        SteppedBehavior.ReportMissing("M11-074 UpdateObjectPose cache path 0x0067EB38: per-ObjectID/origin native pose-cache producer and M11-088 virtual+0x5C remain unbuilt; no per-family moving-object assumption or re-add guessed");
        void AddObject()
        {
            ObjectRouteForTest?.Invoke("add");
            SteppedBehavior.ReportMissing("M11-074 AddObservableObject 0x0067ECFC: relative-pose height/cache path and M11-088 bounding-quad virtual+0x50 are not integrated; no yaw-only footprint or fabricated insertion/fill-border success");
        }
    }
    internal SourceMapAging? LastAgingForTest; // observation only
    internal Action? BroadcastDueForTest; // observation only; never supplies a stream
    internal void SetRenderEnabled(byte value)=>Enabled=value; // G2E144, 0x006807EA
    internal void SetBroadcastInterval(float value,float timer)
    {BroadcastInterval=value;BroadcastDeadline=timer;} // G2E145, 0x0068085E

    internal void Update(uint timestamp,float timer)
    {
        // fidelity: M8-026
        SteppedBehavior.ReportMissing("M8-026 / M11-074 MapComponent::UpdateRobotPose ProcessClearQuad join 0x0067E47E: native robot-quad/map writer is unbuilt; no legacy region rectangle is substituted");
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
