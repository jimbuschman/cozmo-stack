using Cozmo.Protocol;
using Cozmo.Transport;

namespace Cozmo.Robot.Vision;

// fidelity: M11-051, M11-052, M11-097
// OV128 H1-H11/A5-A7 as corrected by its manager check; OV12 B1-B11.
// Pose arithmetic/tree operations remain owned by M11-065/066/067; no parallel pose implementation.
internal sealed class HistRobotState : IDisposable
{
    internal readonly Pose3d Pose=new("");
    // Same checked native RobotState defaults used by AddVisionOnlyState (M11-070).
    internal RobotState State=new(){Timestamp=1,PoseOriginId=1,BatteryVoltage=5,Status=0x300,
        CliffDataRaw=new ushort[]{0xFFFF,0xFFFF,0xFFFF,0xFFFF},CurrPathSegment=-1};
    internal void Assign(HistRobotState other){Pose.Assign(other.Pose);State=Copy(other.State);}
    internal static RobotState Copy(RobotState state)=>RobotState.Read(new CladReader(state.ToBytes().AsMemory(1)));
    internal static HistRobotState From(Pose3d pose,RobotState state)
    {var value=new HistRobotState();value.Pose.Assign(pose);value.State=Copy(state);return value;}
    public void Dispose()=>Pose.Dispose();
}

public sealed partial class RobotStateHistory
{
    private readonly object _gate=new();
    internal object? NativePoseTreeGate; // robot-owned histories share every native pose writer's gate
    private readonly SortedDictionary<uint,HistRobotState> _entries=new();
    private readonly SortedDictionary<uint,Pose3d> _computed=new();
    // Native auxiliary indices (+0x24/+0x30). Their insertion owner is still M11-044, named at that join below.
    internal readonly SortedDictionary<uint,uint> ComputedTimeToKey=new(),ComputedKeyToTime=new();
    private readonly Dictionary<uint,Pose3d> _hostOrigins=new(); // only the standalone Add(RobotState) host adapter
    private static readonly object AdmissionGate=new();
    internal static byte ConsecutiveGaps;
    internal static double ConnectionTimeoutInMs=5000.0; // ELF0x01051F48; known Init/Configure writers store5000.0. Census still UNKNOWN.
    // fidelity: M11-044
    public uint WindowMs {get;init;}=3000;
    public int Count {get{lock(_gate)return _entries.Count;}}
    public uint OriginId {get{lock(_gate)return _entries.Count==0?0:_entries.Last().Value.State.PoseOriginId;}}
    internal uint OldestTimeStamp {get{lock(_gate)return _entries.Count==0?0:_entries.First().Key;}}
    internal uint NewestTimeStamp {get{lock(_gate)return _entries.Count==0?0:_entries.Last().Key;}}
    public int InterpolationStandIns=>0; // compatibility counter: there are no nearest-endpoint stand-ins

    private T WithGate<T>(Func<T> body)
    {
        T result=default!;
        void Run()=>LogCallbackDeferral.RunGated(_gate,()=>result=body());
        if(NativePoseTreeGate is {} tree)LogCallbackDeferral.RunGated(tree,Run);else Run();
        return result;
    }

    /// <summary>Standalone host adapter. Production supplies its real origin-owned pose to AddRawOdomState.</summary>
    public void Add(RobotState state)
        => WithGate(()=>{AddHostState(state);return 0;});
    private void AddHostState(RobotState state)
    {
        lock(_gate)
        {
            if(!_hostOrigins.TryGetValue(state.PoseOriginId,out var origin))
                _hostOrigins.Add(state.PoseOriginId,origin=new Pose3d(){Id=state.PoseOriginId});
            using var pose=new Pose3d(Rotation3d.FromAngleAxis(state.Pose.Angle,new Vec3(0,0,1)),
                new Vec3(state.Pose.X,state.Pose.Y,state.Pose.Z),parent:origin);
            AddRawOdomState(state.Timestamp,pose,state);
        }
    }
    internal uint AddRawOdomState(uint timestamp,Pose3d pose,RobotState state)
        => WithGate(()=>AddRawOdomStateCore(timestamp,pose,state));
    private uint AddRawOdomStateCore(uint timestamp,Pose3d pose,RobotState state)
    {
        lock(_gate)
        {
            if(_entries.Count!=0)
            {
                uint newest=_entries.Last().Key;
                if(newest>WindowMs && timestamp<newest-WindowMs)
                {Log(EngineLogLevel.Warning,"RobotStateHistory.AddRawOdomState.TimeTooOld",$"newestTime {newest}, oldestAllowedTime {newest-WindowMs}, t {timestamp}");return 1;}
                double timeout=ConnectionTimeoutInMs;
                if(timeout<0)timeout=0;
                if(timeout>uint.MaxValue)timeout=uint.MaxValue;
                uint allowed=double.IsNaN(timeout)?0:timeout<=0?0:unchecked((uint)Math.Min(uint.MaxValue,Math.Floor(timeout+0.5)));
                lock(AdmissionGate)
                {
                    if(unchecked(timestamp-newest)>allowed)
                    {
                        if(ConsecutiveGaps==0)Error("RobotStateHistory.AddRawOdomState.TimestampDeltaTooLarge",$"State with t:{timestamp} is too different from last state with t:{newest}, allowed delta:{allowed}");
                        ConsecutiveGaps=unchecked((byte)(ConsecutiveGaps+1));
                        if(ConsecutiveGaps>=6)
                        {
                            ConsecutiveGaps=0;
                            Log(EngineLogLevel.Warning,"RobotStateHistory.AddRawOdomState.TooManyConsecLargeDeltas",$"Clearing state history after receiving 5 consecutive state messages                                  with timestamps deltas greater than {allowed}");
                            ClearRaw();
                        }
                        return 1;
                    }
                    ConsecutiveGaps=0;
                }
            } // empty raw history deliberately does NOT reset the process counter
            if(pose.HasParent)
            {
                Pose3d parent=null!;
                if(!pose.TryGetParent(ref parent))return 1;
                bool root;using(parent)root=parent.IsRoot;
                if(!root){Error("RobotStateHistory.AddRawOdomState.NonFlattenedPose",$"Pose object inside pose stamp should be flattened ({pose.GetNamedPathToRoot(false)})");return 1;}
            }
            if(_entries.ContainsKey(timestamp))
            {Log(EngineLogLevel.Warning,"RobotStateHistory.AddRawOdomState.AddFailed",$"Time: {timestamp}");return 1;}
            _entries.Add(timestamp,HistRobotState.From(pose,state));CullToWindowSize();return 0;
        }
    }
    private void ClearRaw(){foreach(var entry in _entries.Values)entry.Dispose();_entries.Clear();}
    // fidelity: M11-044, M1-041, M1-025, M1-015
    internal void Clear()
        => WithGate(()=>{ClearCore();return 0;});
    private void ClearCore()
    {
        lock(_gate)
        {
            ClearRaw();foreach(var p in _computed.Values)p.Dispose();_computed.Clear();
            foreach(var v in VisionOnly.Values)v.Pose.Dispose();VisionOnly.Clear();ComputedTimeToKey.Clear();ComputedKeyToTime.Clear();
        }
    }
    internal void ResetToConstructed()=>Clear(); // process-global gap counter is not a Robot-owned field
    internal void CullToWindowSize()
        => WithGate(()=>{CullToWindowSizeCore();return 0;});
    private void CullToWindowSizeCore()
    {
        lock(_gate)
        {
            if(_entries.Count<2)return;
            uint newest=_entries.Last().Key;if(newest<WindowMs)return;
            uint cutoff=newest-WindowMs;
            foreach(uint key in _entries.Keys.TakeWhile(k=>k<cutoff).ToArray()){_entries[key].Dispose();_entries.Remove(key);}
            bool hadVision=VisionOnly.Count!=0;
            foreach(uint key in VisionOnly.Keys.TakeWhile(k=>k<cutoff).ToArray()){VisionOnly[key].Pose.Dispose();VisionOnly.Remove(key);}
            if(hadVision&&VisionOnly.Count==0)SourceLog?.Invoke(EngineLogLevel.Debug,"RobotStateHistory","RobotStateHistory.CullToWindowSize.VisStatesEmpty",$"_visStates is empty after culling to window size {WindowMs}");
            foreach(uint key in _computed.Keys.TakeWhile(k=>k<cutoff).ToArray()){_computed[key].Dispose();_computed.Remove(key);}
            var removedTimes=ComputedTimeToKey.Keys.TakeWhile(k=>k<cutoff).ToArray();
            if(removedTimes.Length>0)
            {
                var lower=ComputedTimeToKey.FirstOrDefault(e=>e.Key>=cutoff);
                if(removedTimes.Length!=ComputedTimeToKey.Count && ComputedKeyToTime.ContainsKey(lower.Value))
                    foreach(uint key in ComputedKeyToTime.Keys.TakeWhile(k=>k<lower.Value).ToArray())ComputedKeyToTime.Remove(key);
                else if(removedTimes.Length!=ComputedTimeToKey.Count)Error("RobotStateHistory.CullToWindowSize.MapsOutOfSync",$"keyByTsMap size: {ComputedTimeToKey.Count}, tsByKeyMap size: {ComputedKeyToTime.Count}");
                foreach(uint time in removedTimes)ComputedTimeToKey.Remove(time);
            }
        }
    }
    internal (uint Timestamp,ushort Cliff0)[] RawCliffsFrom(uint timestamp)
    {lock(_gate)return _entries.Where(e=>e.Key>=timestamp).Select(e=>(e.Key,e.Value.State.CliffDataRaw[0])).ToArray();}

    internal uint GetRawStateAt(uint timestamp,ref uint outTime,HistRobotState output,bool interpolate)
    {uint time=outTime;uint result=WithGate(()=>GetRawStateAtCore(timestamp,ref time,output,interpolate));outTime=time;return result;}
    private uint GetRawStateAtCore(uint timestamp,ref uint outTime,HistRobotState output,bool interpolate)
    {
        lock(_gate)
        {
            var entries=_entries.ToArray();int next=Array.FindIndex(entries,e=>e.Key>=timestamp);
            if(next<0 || (next==0 && entries[0].Key>timestamp))return 1;
            var after=entries[next];
            if(after.Key==timestamp){outTime=timestamp;output.Assign(after.Value);return 0;}
            var before=entries[next-1];
            if(before.Value.State.PoseFrameId!=after.Value.State.PoseFrameId)
            {
                SourceLog?.Invoke(EngineLogLevel.Info,"Unnamed","RobotStateHistory.GetRawStateAt.MisMatchedFrameIds",$"Cannot interpolate at t={timestamp} because frame IDs don't match: prev={unchecked((int)before.Value.State.PoseFrameId)} vs next={unchecked((int)after.Value.State.PoseFrameId)}");
                return 0x06000000;
            }
            using var relative=new Pose3d("");
            bool same=interpolate?Pose3d.GetWithRespectTo(after.Value.Pose,before.Value.Pose,relative)
                :PoseTreeNode.HasSameRoot(after.Value.Pose.Node,before.Value.Pose.Node);
            if(!same)
            {
                using var previousRoot=before.Value.Pose.FindRoot();using var nextRoot=after.Value.Pose.FindRoot();
                SourceLog?.Invoke(EngineLogLevel.Info,"Unnamed","RobotStateHistory.GetRawStateAt.MisMatchedOrigins",$"Cannot interpolate at t={timestamp} because poses don't share the same origin: prev={previousRoot.Name} vs next={nextRoot.Name}");
                return 0x06000000;
            }
            if(!interpolate)
            {
                var use=after.Key-timestamp>=timestamp-before.Key?before:after;
                outTime=use.Key;output.Assign(use.Value);return 0;
            }
            float numerator=(float)unchecked(timestamp-before.Key),denominator=(float)unchecked(after.Key-before.Key);
            float fraction=numerator/denominator;
            using var blended=new HistRobotState();
            if(!Interpolate(before.Value,after.Value,relative,fraction,blended))return 1;
            output.Assign(blended);outTime=timestamp;return 0;
        }
    }
    internal bool Interpolate(HistRobotState before,HistRobotState after,Pose3d relative,float f,HistRobotState output)
    {
        if(!before.Pose.HasParent)
        {StackDiagnostics.Write("M1-034/M11-052: skipped interpolation with root before pose; native GetParent null-wrapper increment 0x008453D4/0x008452B0; outputs retained");return false;}
        Pose3d parent=null!;if(!before.Pose.TryGetParent(ref parent))return false;
        using(parent)
        {
            RobotState state=HistRobotState.Copy(f<BitConverter.Int32BitsToSingle(0x3EFFFEB0)?before.State:after.State);
            state.PoseFrameId=before.State.PoseFrameId;
            static float Blend(float b,float a,float f){float delta=a-b;float step=delta*f;return b+step;}
            state.HeadAngle=Blend(before.State.HeadAngle,after.State.HeadAngle,f);
            state.LiftAngle=Blend(before.State.LiftAngle,after.State.LiftAngle,f);
            int delta=(int)after.State.CliffDataRaw[0]-before.State.CliffDataRaw[0];float part=(float)delta*f;
            float cliff=(float)(uint)before.State.CliffDataRaw[0]+part;
            float rounded=MathF.Round(cliff,MidpointRounding.AwayFromZero);
            uint unsigned=float.IsNaN(rounded)||rounded<=0?0:rounded>=uint.MaxValue?uint.MaxValue:(uint)rounded;
            state.CliffDataRaw=new ushort[]{unchecked((ushort)unsigned),0xFFFF,0xFFFF,0xFFFF};
            state.LwheelSpeedMmps=Blend(before.State.LwheelSpeedMmps,after.State.LwheelSpeedMmps,f);
            state.RwheelSpeedMmps=Blend(before.State.RwheelSpeedMmps,after.State.RwheelSpeedMmps,f);
            float x=(float)before.Pose.Translation.X+(float)relative.Translation.X*f;
            float y=(float)before.Pose.Translation.Y+(float)relative.Translation.Y*f;
            float z=(float)before.Pose.Translation.Z+(float)relative.Translation.Z*f;
            var angle=new Radians((float)before.Pose.AngleAroundZ)+new Radians((float)relative.AngleAroundZ)*f;
            using var pose=new Pose3d(Rotation3d.FromAngleAxis(angle.Value,new Vec3(0,0,1)),new Vec3(x,y,z),parent:parent);
            output.Pose.Assign(pose);output.State=state;return true;
        }
    }
    internal uint ComputeStateAt(uint timestamp,ref uint outTime,HistRobotState output,bool interpolate)
    {uint time=outTime;uint result=WithGate(()=>ComputeStateAtCore(timestamp,ref time,output,interpolate));outTime=time;return result;}
    private uint ComputeStateAtCore(uint timestamp,ref uint outTime,HistRobotState output,bool interpolate)
    {
        lock(_gate)
        {
            if(VisionOnly.TryGetValue(timestamp,out var exact))
            {outTime=timestamp;output.Pose.Assign(exact.Pose);output.State=RobotState.Read(new CladReader(exact.State));return 0;}
            using var raw=new HistRobotState();uint result=GetRawStateAt(timestamp,ref outTime,raw,interpolate);
            if(result!=0)return result;
            uint rawTime=outTime;var vision=VisionOnly.LastOrDefault(e=>e.Key<=rawTime);
            if(vision.Value is null || ReadFrame(vision.Value.State)<=raw.State.PoseFrameId){output.Assign(raw);return 0;}
            var states=_entries.ToArray();int at=Array.FindIndex(states,e=>e.Key>=vision.Key);
            if(at<0 || !states[at].Value.Pose.HasParent || !vision.Value.Pose.HasParent)
            {StackDiagnostics.Write("M1-034/M11-097: skipped history correction with absent anchor/parent at0x00531904/0x0053193E/0x00531AA0; prior caller state retained");return 1;}
            using var d=new Pose3d("");
            static void Delta(Pose3d from,Pose3d anchor,Pose3d destination)
            {
                Pose3d parent=null!;
                if(!anchor.TryGetParent(ref parent))return;
                using(parent)Pose3d.GetWithRespectTo(from,parent,destination); // return deliberately ignored
                using var inverse=anchor.Inverse();destination.MultiplyBy(inverse);
            }
            if(states[at].Value.State.PoseFrameId==raw.State.PoseFrameId)Delta(raw.Pose,states[at].Value.Pose,d);
            else
            {
                int boundary=at;
                for(int candidate=at;candidate<states.Length;candidate++)
                {
                    if(states[candidate].Value.State.PoseFrameId>states[boundary].Value.State.PoseFrameId)
                    {
                        if(!states[boundary].Value.Pose.HasParent)
                        {StackDiagnostics.Write("M1-034/M11-097: skipped correction GetParent null wrapper at0x00531A00; prior caller state retained");return 1;}
                        using var deltaPose=new Pose3d("");Delta(states[candidate-1].Value.Pose,states[boundary].Value.Pose,deltaPose);
                        d.PreComposeWith(deltaPose);boundary=candidate;
                    }
                    // Boundary delta is accumulated BEFORE the R.frame stop test (OV128 correction9).
                    if(states[candidate].Value.State.PoseFrameId==raw.State.PoseFrameId)break;
                }
            }
            d.MultiplyBy(vision.Value.Pose);
            Pose3d vp=null!;if(!vision.Value.Pose.TryGetParent(ref vp))return 1;
            using(vp)d.SetParent(vp);
            output.Pose.Assign(d);output.State.PoseFrameId=raw.State.PoseFrameId;
            output.State.HeadAngle=raw.State.HeadAngle;output.State.LiftAngle=raw.State.LiftAngle;return 0;
        }
    }
    private static uint ReadFrame(byte[] state)=>System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(state.AsSpan(4));
    internal uint GetLastStateWithFrameID(uint frame,HistRobotState output)
        => WithGate(()=>GetLastStateWithFrameIDCore(frame,output));
    private uint GetLastStateWithFrameIDCore(uint frame,HistRobotState output)
    {
        lock(_gate)
        {
            if(_entries.Count==0){Error("RobotStateHistory.GetLastStateWithFrameID.EmptyHistory","");return 1;}
            foreach(var entry in _entries.Reverse())
            {uint id=entry.Value.State.PoseFrameId;if(id<frame)break;if(id==frame){output.Assign(entry.Value);return 0;}}
            foreach(var entry in VisionOnly.Reverse())
            {uint id=ReadFrame(entry.Value.State);if(id<frame)break;if(id==frame){output.Pose.Assign(entry.Value.Pose);output.State=RobotState.Read(new CladReader(entry.Value.State));return 0;}}
            Error("RobotStateHistory.GetLastStateWithFrameID.FrameIdNotFound",$"Frame ID {frame} not found");return 1;
        }
    }
    internal static VisionPoseData PoseData(uint timestamp,HistRobotState value)
    {
        // Host geometry facade, used by direct ProcessImage/ProcessCapture inputs and legacy convenience APIs.
        // Native query outputs above keep their real parent; no native caller's root join is established by this facade.
        using var flat=value.Pose.GetWithRespectToRoot();var s=value.State;
        return new(timestamp,new Pose3d(flat.Rotation,flat.Translation),s.HeadAngle,s.LiftAngle,(s.Status&1)!=0,false,(s.Status&0x8200)!=0x200);
    }
    public VisionPoseData? At(uint timestamp)
    {using var value=new HistRobotState();uint time=0;return GetRawStateAt(timestamp,ref time,value,false)==0?PoseData(time,value):null;}
    public VisionPoseData? Latest
    {get=>WithGate<VisionPoseData?>(()=>
    {
        if(_entries.Count==0)return null;using var value=new HistRobotState();uint time=0;
        if(ComputeStateAt(_entries.Last().Key,ref time,value,false)!=0)return null;
        // Existing public host facade supplies flat coordinates to legacy geometry callers. Native consumers
        // use ComputeStateAt and retain its actual parent; this adapter does not establish those callers' root joins.
        return PoseData(time,value);
    });}
    // fidelity: M11-044, M11-050
    public bool ComputeAndInsertStateAt(uint timestamp)=>ComputeAndInsertStateAt(timestamp,out _);
    internal bool ComputeAndInsertStateAt(uint timestamp,out uint originId)
    {uint origin=0;bool result=WithGate(()=>ComputeAndInsertCore(timestamp,out origin));originId=origin;return result;}
    private bool ComputeAndInsertCore(uint timestamp,out uint originId)
    {
        lock(_gate)
        {
            originId=0;using var value=new HistRobotState();uint time=0;
            if(ComputeStateAt(timestamp,ref time,value,true)!=0)return false;
            using var root=value.Pose.FindRoot();originId=root.Id;
            InsertComputedStateAt(timestamp,value.Pose);
            Behavior.SteppedBehavior.ReportMissing("M11-044 ComputeAndInsertStateAt native auxiliary key/index ownership after0x00531BAA is not in the adopted history-query rows; bounded timestamp/pose cache only, no invented key/index producer");
            return true;
        }
    }
    internal void InsertComputedStateAt(uint timestamp,Pose3d pose)
    {WithGate(()=>{if(_computed.Remove(timestamp,out var prior))prior.Dispose();_computed[timestamp]=new Pose3d(pose);return 0;});}
    public Pose3d? GetComputedStateAt(uint timestamp){lock(_gate)return _computed.GetValueOrDefault(timestamp);}
    // fidelity: M11-055
    internal EngineLocalizationPose? PublicationPose
    {get{var latest=Latest;return latest is {} p?EngineLocalizationPose.FromPlanar((float)p.RobotPose.Translation.X,(float)p.RobotPose.Translation.Y,(float)p.RobotPose.Translation.Z,(float)p.RobotPose.AngleAroundZ,OriginId):null;}}
    private void Log(EngineLogLevel level,string key,string text)=>SourceLog?.Invoke(level,null,key,text);
}
