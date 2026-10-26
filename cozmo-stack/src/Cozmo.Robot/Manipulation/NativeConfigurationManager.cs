using Cozmo.Robot.Behavior;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Manipulation;

// Native-only producer. The public BlockConfigurationManager is a separate host extension.
// fidelity: M11-037, M12-049, M13-014
internal sealed class NativeConfigurationManager : IDisposable
{
    private readonly BlockWorld _world;
    private readonly EngineRobot _robot;
    private readonly object _gate=new();
    private readonly SortedSet<uint> _pending=new(Comparer<uint>.Create((a,b)=>unchecked((int)a).CompareTo(unchecked((int)b))));
    private Dictionary<uint,Pose3d> _rootCache=new();
    private NativeConfigurationSnapshot _snapshot=NativeConfigurationSnapshot.Empty;
    private bool _retired;
    internal NativeConfigurationSnapshot Snapshot=>Volatile.Read(ref _snapshot);
    internal int RebuildCount {get;private set;}
    internal int NotifyCount {get;private set;}
    internal uint[] Pending {get {lock(_gate)return _pending.ToArray();}}
    internal bool InputsAvailable {get;private set;}=true;
    internal NativeConfigurationManager(BlockWorld world,EngineRobot robot){_world=world;_robot=robot;}
    internal void SetObjectPoseChanged(uint id,PoseState state)
    {
        lock(_world.FrameGate)lock(_gate)
        {
            if(_retired)return;
            _pending.Add(id);
            if(state==PoseState.Unknown && _rootCache.Remove(id,out var pose))lock(_robot.PoseTreeGate)pose.Dispose();
        }
    }
    internal void ForceUpdate()=>_robot.BlockConfigurationForceUpdate=true;
    internal bool Contains(uint id,BlockConfigurationType type)=>Snapshot.Cache(type).Any(c=>c.ContainsBlock(id));
    internal NativeStackConfiguration? Tallest()
    {
        NativeStackConfiguration? best=null;
        foreach(var stack in Snapshot.Stacks)
            if(best is null || stack.BlockIds.Count>best.BlockIds.Count)best=stack;
        return best; // strict greater retains the first tied stack.
    }
    private bool DidPendingObjectsMove()
    {
        bool moved=false;
        foreach(uint id in _pending)
        {
            var obj=_world.GetLocatedObjectByIdHelper(id);
            if(obj is not null)
            {
                if(!_rootCache.TryGetValue(id,out var old)){moved=true;continue;}
                if(!old.IsSameAs(obj.NativePose,5f,BitConverter.UInt32BitsToSingle(0x3F060A92)))return true;
            }
            else if(Enum.GetValues<BlockConfigurationType>().Any(type=>Contains(id,type)))moved=true;
        }
        return moved;
    }
    private void RequireActualOwner()
    {
        if(_retired || !ReferenceEquals(_robot.Engine.Robot,_robot))throw new NativeConfigurationUnavailable("actual current Robot owner unavailable");
    }
    internal void Update()
    {
        lock(_world.FrameGate)lock(_gate)
        {
            if(_retired || (!_robot.BlockConfigurationForceUpdate && _pending.Count==0))return;
            Dictionary<uint,Pose3d>? stagedRoots=null;
            NativeConfigurationSnapshot? next=null;
            try
            {
                RequireActualOwner();
                // Actual pose inputs stay stable through all three containers and root-cache staging.
                lock(_robot.PoseTreeGate)
                {
                    var geometry=new NativeConfigurationGeometry(_world,_robot);
                    if(_robot.BlockConfigurationForceUpdate || DidPendingObjectsMove())
                    {
                        next=new NativeConfigurationProducer(geometry).Build(Snapshot);
                        stagedRoots=new();
                        foreach(var entry in _rootCache)stagedRoots.Add(entry.Key,new Pose3d(entry.Value));
                        foreach(var obj in geometry.Query())
                        {
                            using var flat=obj.NativePose.GetWithRespectToRoot();
                            if(stagedRoots.Remove(obj.ObjectId,out var old))old.Dispose();
                            stagedRoots.Add(obj.ObjectId,new Pose3d(flat));
                        }
                        // Availability is a host guard, not a native no-carry/empty-cache fallback.
                        RequireActualOwner();_ = new NativeConfigurationGeometry(_world,_robot);
                    }
                }
            }
            catch(NativeConfigurationUnavailable error)
            {
                if(stagedRoots is not null)DisposeRoots(stagedRoots.Values);
                InputsAvailable=false;
                SteppedBehavior.ReportMissing("Native configuration publication UNKNOWN: "+error.Message+"; prior complete publication/root cache/counters and pending/force retained for retry");
                return;
            }
            InputsAvailable=true;
            if(next is not null)
            {
                var oldRoots=_rootCache;
                _rootCache=stagedRoots!;
                Volatile.Write(ref _snapshot,next);
                RebuildCount++;
                DisposeRoots(oldRoots.Values);
                NotifyCount++;
                _robot.BehaviorPublicState.UpdateStackHeight(unchecked((byte)(Tallest()?.BlockIds.Count??0)));
            }
            // Native notification occurs before these resets (0x00616DD0..DDC).
            _robot.BlockConfigurationForceUpdate=false;_pending.Clear();
        }
    }
    private void DisposeRoots(IEnumerable<Pose3d> roots)
    {lock(_robot.PoseTreeGate)foreach(var pose in roots)pose.Dispose();}
    public void Dispose()
    {
        lock(_world.FrameGate)lock(_gate)
        {
            if(_retired)return;_retired=true;
            DisposeRoots(_rootCache.Values);
            _rootCache.Clear();_pending.Clear();
            if(ReferenceEquals(_world.NativeConfigurations,this))_world.NativeConfigurations=null;
        }
    }
}
