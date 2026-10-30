using System.Buffers.Binary;
using System.Numerics;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Behavior;

// GatherCubes 005AF250..866: actual nested activity; the map outlives selection.
internal sealed class NativeGatherCubesActivity : IDisposable
{
    private readonly EngineRobot _robot;
    private readonly Dictionary<uint,bool> _progress = new();
    private long _generation;
    private bool _retired;
    internal bool Completed { get; private set; }
    internal IReadOnlyDictionary<uint,bool> Progress => _progress;
    internal NativeGatherCubesActivity(EngineRobot robot) => _robot=robot;
    private Manipulation.ManipulationSystem? Owner => _robot.NativeBehaviorRuntime?.Manipulation;
    private bool Current(long generation)=>!_retired && generation==_generation;
    internal void Selected()
    {
        ++_generation;
        if(Owner is {} owner)owner.Whiteboard.ClearAllBeacons();
        else SteppedBehavior.ReportMissing("M15-008 Gather selected: actual whiteboard owner unavailable");
    }
    internal void Deselected()
    {
        ++_generation;
        if(Owner is {} owner)owner.Whiteboard.ClearAllBeacons();
        else SteppedBehavior.ReportMissing("M15-008 Gather deselected: actual whiteboard owner unavailable");
        Completed=false;
    }
    internal void Update()
    {
        long generation=_generation;
        if(!Current(generation)||Completed)return;
        var owner=Owner;
        if(owner is null){SteppedBehavior.ReportMissing("M15-008 Gather update: actual world/whiteboard owner unavailable");return;}
        var filter=new BlockWorldFilter();filter.AllowedFamilies.Add(ObjectFamily.LightCube);
        var cubes=new List<ObservableObject>();owner.World.FindConnectedObjectHelper(filter,cubes.Add,false);
        if(!Current(generation))return;
        bool all=owner.Whiteboard.AreAllCubesInBeacons(()=>Current(generation));
        if(!Current(generation))return;
        var lights=_robot.BehaviorManagerOwner!.ContextRobot.Lights.Cubes;
        var zero=new CubeLightModifier(new LedPattern[4],0,0,Vector2.Zero);
        if(all)
        {
            foreach(var cube in cubes)
            {
                if(_progress.GetValueOrDefault(cube.ObjectId))lights.StopAndPlayBehaviorStateLight(cube.ObjectId,17,16,false,zero,()=>Current(generation));
                else lights.PlayBehaviorStateLight(cube.ObjectId,16,false,zero,()=>Current(generation));
                if(!Current(generation))return;
                _progress[cube.ObjectId]=false;
            }
            if(_robot.HasExternalInterface)
            {
                var payload=new byte[4];BinaryPrimitives.WriteUInt32LittleEndian(payload,11);
                _robot.Engine.DeliverAnimationReportToApp(0x8B,payload);
                _robot.Engine.AnimationMessages.Deliver(0x8B,true,11u);
                if(!Current(generation))return;
            }
            _robot.Engine.Needs.RegisterNeedsActionCompleted("GatherCubes");
            if(Current(generation))Completed=true;
            return;
        }
        var beacon=owner.Whiteboard.GetActiveBeacon();
        if(beacon is null)return;
        foreach(var cube in cubes)
        {
            var located=owner.World.GetLocatedObjectByIdHelper(cube.ObjectId);
            bool inside=located is {PoseState:PoseState.Known} && beacon.IsLocWithinBeacon(located.NativePose,0f);
            if(!Current(generation))return;
            bool was=_progress.GetValueOrDefault(cube.ObjectId);
            if(inside&&!was) { lights.PlayBehaviorStateLight(cube.ObjectId,17,false,zero,()=>Current(generation)); if(!Current(generation))return; _progress[cube.ObjectId]=true; }
            else if(!inside&&was) { lights.StopBehaviorStateLight(cube.ObjectId,17,true,()=>Current(generation)); if(!Current(generation))return; _progress[cube.ObjectId]=false; }
            else if(!_progress.ContainsKey(cube.ObjectId))_progress[cube.ObjectId]=false;
        }
    }
    public void Dispose(){_retired=true;++_generation;_progress.Clear();}
}
