using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Xunit;
namespace Cozmo.Protocol.Tests;

public sealed class NativeTrackerOwnerTests
{
    private sealed class Probe : SteppedBehavior
    {
        internal Probe():base("tracker-order-probe","Probe"){}
        protected override bool IsRunnableInternal(BehaviorContext context)=>true;
        protected override void OnStart(){}
        protected override void OnUpdate(){}
    }
    private sealed class RecordingHelper : IHelper
    {
        private readonly Action _update;
        internal RecordingHelper(CozmoRobot robot,SteppedBehavior behavior,BehaviorHelperComponent owner,Action update):base(robot,"tracker-order",behavior,owner.Factory){_update=update;}
        public override bool ShouldCancelDelegates(CozmoRobot robot)=>false;
        protected override BehaviorStatus Init(CozmoRobot robot)=>BehaviorStatus.Running;
        protected override BehaviorStatus UpdateWhileActiveInternal(CozmoRobot robot){_update();return BehaviorStatus.Running;}
    }
    [Fact]
    public void RealEngineAiRunsAnAdmittedHelperBeforeTheDueTrackerReport()
    {
        using var rig=new M4ControlTests.Rig();rig.ToSynced();rig.State();
        var runtime=rig.Engine.Robot!.NativeBehaviorRuntime!;var tracker=runtime.Tracker;
        tracker.SetFreeplayPauseFlag(FreeplayPauseFlag.GameControl,false);
        var order=new List<string>();bool armed=false;
        IHelper? raw=new RecordingHelper(rig.Robot,new Probe(),runtime.AI.Helpers,()=>{if(armed)order.Add("helper");});
        var handle=runtime.AI.Helpers.AddHelperToComponent(ref raw);
        Assert.True(runtime.AI.Helpers.DelegateToHelper(rig.Robot,handle,null,null));
        tracker.ActiveFreeplayTime+=_=>order.Add("tracker");armed=true;
        try{rig.Tick(30000);Assert.Equal(new[]{"helper","tracker"},order);}
        finally{runtime.AI.Helpers.StopHelperWithoutCallback(handle);handle.Release();}
    }

    [Fact]
    public void ActualRobotRetirementForcesTrackerBeforeAbortAndFreshRobotGetsFreshStorage()
    {
        using var rig=new M4ControlTests.Rig();rig.ToSynced();rig.State();
        var native=rig.Engine.Robot!;var runtime=native.NativeBehaviorRuntime!;var tracker=runtime.Tracker;
        Assert.Same(tracker,runtime.AI.Tracker);
        Assert.Contains(FreeplayPauseFlag.GameControl,tracker.PausedFlags);
        tracker.SetFreeplayPauseFlag(FreeplayPauseFlag.GameControl,false); // explicit source flag input, not app Freeplay admission
        var order=new List<string>();
        rig.Engine.LogEntry+=entry=>{if(entry.Name=="FreeplayDataTracker.SendData.Accumulate")order.Add("accumulate");if(entry.Name=="robot.active_freeplay_time")order.Add("event");};
        native.SendFault=message=>{if(message is AbortDocking)order.Add("abort");return null;}; // observe the actual AbortAll send
        using(var host=new ManipulationSystem(rig.Robot,rig.Robot.Vision))host.Dispose();
        Assert.Empty(order);Assert.Same(tracker,native.NativeBehaviorRuntime!.Tracker);
        rig.Tick(1200);
        rig.Engine.Robots.RemoveRobot(1,false);
        Assert.Equal(new[]{"accumulate","event","abort"},order);
        Assert.True(runtime.AI.IsChildRetired(0x2C));Assert.Empty(tracker.PausedFlags);
        rig.Engine.Robots.AddRobot(1);
        var replacement=rig.Engine.Robot!.NativeBehaviorRuntime!.Tracker;
        Assert.NotSame(tracker,replacement);Assert.Equal(0UL,replacement.AccumulatedNanos);
        Assert.Contains(FreeplayPauseFlag.GameControl,replacement.PausedFlags);
    }

    [Fact]
    public void ActualChargerTransitionAndEngineAiPhaseUpdateTheCanonicalTracker()
    {
        using var rig=new M4ControlTests.Rig();rig.ToSynced();rig.State();
        var runtime=rig.Engine.Robot!.NativeBehaviorRuntime!;var tracker=runtime.Tracker;
        rig.State(RobotStatusFlag.IsBodyAccMode|RobotStatusFlag.IsOnCharger);
        Assert.Contains(FreeplayPauseFlag.OnCharger,tracker.PausedFlags);
        rig.State(x:200);Assert.DoesNotContain(FreeplayPauseFlag.OnCharger,tracker.PausedFlags);
        tracker.SetFreeplayPauseFlag(FreeplayPauseFlag.GameControl,false); // explicit source input
        var reports=new List<double>();tracker.ActiveFreeplayTime+=reports.Add;
        rig.Tick(30000);
        Assert.Single(reports);Assert.Equal(0UL,tracker.AccumulatedNanos);
        Assert.True(tracker.NextSendSec>rig.Engine.Timer.SecondsF);
    }
}
