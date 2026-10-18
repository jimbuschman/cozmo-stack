using System.Reflection;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Xunit;
namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class M2M15HandlerBuilderTests
{
    private sealed class Fixture : IDisposable
    {
        internal readonly M4ControlTests.Rig Rig = new();
        internal CozmoRobot Robot => Rig.Robot;
        internal readonly Cozmo.Robot.Vision.VisionSystem Vision;
        internal readonly ManipulationSystem M;
        internal readonly string Root = Directory.CreateTempSubdirectory("b2m15-handler-").FullName;
        internal readonly List<EngineLogEntry> Logs = new();
        internal Fixture(int trigger=0x207, bool response=true)
        {
            Rig.ToSynced();
            Vision=new(Robot,Cozmo.Robot.Vision.CameraCalibration.Nominal()){Enabled=false};
            M=new(Robot,Vision);
            Directory.CreateDirectory(Path.Combine(Root,"animations"));
            File.WriteAllText(Path.Combine(Root,"animations","a.json"),"{\"a\":[]}");
            Directory.CreateDirectory(Path.Combine(Root,"animationGroupMaps"));
            File.WriteAllText(Path.Combine(Root,"animationGroupMaps","AnimationTriggerMap.json"),response ?
                "{\"Pairs\":[{\"CladEvent\":\""+((AnimationTrigger)trigger)+"\",\"AnimName\":\"a\"}]}" : "{\"Pairs\":[]}");
            Robot.Animations.ManualTicking=true;
            Robot.Animations.LoadFrom(Root);
            Robot.Engine.LogEntry+=Logs.Add;
        }
        internal TriggerLiftSafeAnimationAction Start(int loops=1)
        {
            // The one-member group is an explicit fixture input, not a provenance oracle.
            Directory.CreateDirectory(Path.Combine(Root,"animationGroups"));
            File.WriteAllText(Path.Combine(Root,"animationGroups","a.json"),"{\"Animations\":[{\"Name\":\"a\",\"Weight\":1,\"Mood\":\"Default\",\"CooldownTime_Sec\":0}]}" );
            Robot.Animations.LoadFrom(Root);
            var action=new TriggerLiftSafeAnimationAction(Robot,new((AnimationTrigger)0x207,loops,false,0,60f,false),_=>{});
            Assert.Equal(0,action.Init());return action;
        }
        internal byte Tag => Robot.Animations.CurrentTag;
        internal void Send(RobotMessage m) => Robot.Engine.Broadcast(m);
        public void Dispose(){M.Dispose();Vision.Dispose();Rig.Dispose();Directory.Delete(Root,true);}
    }

    // reach: unit (substitutes direct action Init/registration; native scheduler join MISSING at M8-020 0x00543F20 / M12-017 0x005521AC).
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void AnimationEndRequiresStartAndMatchingByteTagAndDecrementsOnlyFiniteLoops(int loops)
    {
        using var f=new Fixture();var action=f.Start(loops);byte tag=f.Tag;
        f.Send(new AnimationEnded{AnimId=tag});Assert.Equal(EngineActionResult.Running,action.CheckIfDone());Assert.Empty(f.Logs.Where(e=>e.Name.StartsWith("PlayAnimation.End")));
        f.Send(new AnimationStarted{AnimId=unchecked((byte)(tag+1))});
        f.Send(new AnimationEnded{AnimId=tag});Assert.Equal(EngineActionResult.Running,action.CheckIfDone());
        f.Send(new AnimationStarted{AnimId=tag});
        f.Send(new AnimationEnded{AnimId=unchecked((byte)(tag+1))});Assert.Equal(EngineActionResult.Running,action.CheckIfDone());
        f.Send(new AnimationEnded{AnimId=tag});
        if(loops==1)Assert.Equal(0u,action.CheckIfDone());
        else
        {
            Assert.Equal(EngineActionResult.Running,action.CheckIfDone());
            string key=loops==0?"PlayAnimation.EndAnimationHandler.LoopingForever":"PlayAnimation.FinishedLoop";
            var log=Assert.Single(f.Logs.Where(e=>e.Name==key));
            Assert.Equal(loops==0?EngineLogLevel.Info:EngineLogLevel.Debug,log.Level);Assert.Equal("Unnamed",log.Channel);
            Assert.EndsWith(loops==0?$"Animation tag {tag} finished a loop, continuing until timeout or cancel":$"Animation tag {tag} finished a loop, 1 left",log.Text);
            f.Send(new AnimationEnded{AnimId=tag});Assert.Equal(loops==0?EngineActionResult.Running:0u,action.CheckIfDone());
        }
        action.WatcherEnding();
    }

    // reach: unit (substitutes direct action Init/registration; native scheduler join MISSING at M8-020 0x00543F20 / M12-017 0x005521AC).
    [Fact]
    public void D5UsesPayloadOffsetFiveAndQueuesAppBeforeSynchronousLocalRecipients()
    {
        using var f=new Fixture();var action=f.Start();byte tag=f.Tag;var order=new List<string>();
        f.Robot.Engine.EngineToGameMessage+=(t,p)=>{if(t==0x60){Assert.Equal(new byte[]{255,255,255,255,2},p.ToArray());order.Add("payload");}};
        f.Robot.Engine.AnimationEvent+=e=>{Assert.Equal(new AnimationEventReport(uint.MaxValue,2),e);order.Add("game");};
        using var local=f.Robot.Engine.AnimationMessages.SubscribeMessage(0x60,true,p=>{Assert.Equal(new AnimationEventReport(uint.MaxValue,2),p);order.Add("local");});
        var message=new Cozmo.Protocol.AnimationEvent{Timestamp=uint.MaxValue,EventId=(AnimEvent)tag,Field2=2};
        f.Send(message);Assert.Empty(order);f.Send(new AnimationStarted{AnimId=tag});f.Send(message);f.Send(message);
        Assert.Equal(new[]{"local","local"},order);f.Robot.Engine.DrainAppDelivery();
        Assert.Equal(new[]{"local","local","payload","game","payload","game"},order);
        Assert.EndsWith($"Event TAPPED_BLOCK received at time -1 while playing animation tag {tag}",f.Logs.Last(e=>e.Name=="PlayAnimation.AnimationEventHandler").Text);
        Assert.Equal(EngineActionResult.Running,action.CheckIfDone());action.WatcherEnding();
    }

    // reach: unit (substitutes direct action Init/registration; native scheduler join MISSING at M8-020 0x00543F20 / M12-017 0x005521AC).
    [Fact]
    public void AbortHasNoStartedGateDoesNotTruncateUintTagAndEndedWinsOverFailed()
    {
        using var f=new Fixture();var action=f.Start();byte tag=f.Tag;
        f.Robot.Engine.AnimationMessages.Deliver(0x5F,true,(uint)tag+256);Assert.Equal(EngineActionResult.Running,action.CheckIfDone());
        f.Robot.Engine.AnimationMessages.Deliver(0x5F,true,(uint)tag);Assert.Equal(0x03000001u,action.CheckIfDone());
        f.Send(new AnimationStarted{AnimId=tag});f.Send(new AnimationEnded{AnimId=tag});Assert.Equal(0u,action.CheckIfDone());action.WatcherEnding();
    }

    // reach: unit (substitutes direct action Init/registration; native scheduler join MISSING at M8-020 0x00543F20 / M12-017 0x005521AC).
    [Fact]
    public void ReInitReplacesAnimationOwnersAndDestructorReleasesInNativeOrder()
    {
        using var f=new Fixture();var action=f.Start();f.Robot.Animations.StopIfCurrent(f.Robot.Animations.Generation);Assert.Equal(0,action.Init());
        Assert.Equal(1,f.Robot.Engine.AnimationMessages.Count(0x5F,true));var dispatch=new List<string>();
        f.Robot.Engine.MessageDispatchObservedForTest=(tag,name)=>{if(name=="PlayAnimation")dispatch.Add($"{(byte)tag:X2}");};
        f.Send(new AnimationStarted{AnimId=f.Tag});f.Send(new AnimationEnded{AnimId=f.Tag});
        Assert.Equal(new[]{"CA","CB"},dispatch);
        var released=new List<byte>();f.Robot.Engine.MessageSubscriptionReleasedForTest=(tag,name)=>{if(name=="PlayAnimation"){Assert.Equal(0,f.Robot.Engine.AnimationMessages.Count(0x5F,true));released.Add((byte)tag);}};
        action.WatcherEnding();Assert.Equal(new byte[]{0xD5,0xCB,0xCA},released);f.Robot.Engine.MessageSubscriptionReleasedForTest=null;
    }

    // reach: unit (substitutes direct action Init/registration; native scheduler join MISSING at M8-020 0x00543F20 / M12-017 0x005521AC).
    [Fact]
    public void DockLiftLoadStoresEverySubscribedActionsStateWithoutAnIdFilterAndResetsOnReInit()
    {
        using var f=new Fixture();var dock=f.M.Docking;
        var a=new PickupObjectAction(f.M,7);var b=new RollObjectAction(f.M,8);
        using var first=dock.RegisterActionHandlers(a,DockAction.PickupLow);
        using var second=dock.RegisterActionHandlers(b,DockAction.RollLow);
        f.Send(new LiftLoad{Field0=true});Assert.Equal(1,a.LiftLoadState);Assert.Equal(1,b.LiftLoadState);
        f.Send(new LiftLoad());Assert.Equal(2,a.LiftLoadState);Assert.Equal(2,b.LiftLoadState);
        dock.RegisterActionHandlers(a,DockAction.PickupLow);Assert.Equal(0,a.LiftLoadState);Assert.Equal(2,b.LiftLoadState);
    }

    // reach: unit (substitutes direct action Init/registration; native scheduler join MISSING at M8-020 0x00543F20 / M12-017 0x005521AC).
    [Theory] [InlineData(0x23F,true,0,0)] [InlineData(0x207,false,5,0)] [InlineData(0x207,true,5,0)] [InlineData(0x207,true,0,2)]
    public void C5ChecksSentinelThenResponseThenActionAndOnlyQueuesWithoutInit(int trigger,bool response,byte action,int added)
    {
        using var f=new Fixture(trigger,response);
        DockActionBase dock=trigger==0x23F?new AlignWithObjectAction(f.M,7,0,AlignmentType.Custom):new PickupObjectAction(f.M,7);
        using var owner=f.M.Docking.RegisterActionHandlers(dock,DockAction.PickupLow);
        int before=f.Robot.Engine.Robot!.ActionList.QueueCount;
        f.Send(new MovingLiftPostDock{Field0=action});f.Send(new MovingLiftPostDock{Field0=action});
        Assert.Equal(before+added,f.Robot.Engine.Robot.ActionList.QueueCount);
        if(added!=0)for(int i=before+1;i<=before+added;i++)Assert.Equal(EngineActionResult.NotStarted,f.Robot.Engine.Robot.ActionList.QueueAt(i)!.Current?.State ?? f.Robot.Engine.Robot.ActionList.QueueAt(i)!.Pending.First().State);
        var logs=f.Logs.Where(e=>e.Name.StartsWith("IDockAction.MovingLiftPostDockHandler")).ToArray();
        if(trigger==0x23F)Assert.Empty(logs);
        else
        {
            Assert.Equal(2,logs.Length);
            string expected=!response?"InvalidAnimation":action!=0?"ActionMismatch":"";
            Assert.All(logs,e=>Assert.Equal("IDockAction.MovingLiftPostDockHandler"+(expected.Length==0?"":"."+expected),e.Name));
            if(expected=="ActionMismatch")Assert.All(logs,e=>Assert.EndsWith("Expected 0, got 5. Ignoring.",e.Text));
            if(expected.Length==0)Assert.All(logs,e=>Assert.EndsWith("Playing animation SoundOnlyLiftEffortPickup ",e.Text));
        }
    }

    private sealed class Runner : ActionRunner
    {
        internal int InitResult;internal bool SetInitialized;internal uint Result=EngineActionResult.Running;internal int Checks;
        internal Runner():base(0,0){}
        public override int Init(){Initialized=SetInitialized;return InitResult;}
        public override uint CheckIfDone(){Checks++;return Result;}
        public override bool CanInterrupt()=>false;
    }
    // reach: unit (substitutes IActionRunner Init/CheckIfDone outcomes; concrete action join MISSING at M8-020 0x00543F20).
    [Fact]
    public void RunningNeverInvokesCompletionCallbacksAndNonzeroInitWithInitializedFlagStillChecks()
    {
        var action=new Runner{InitResult=unchecked((int)EngineActionResult.Running)};var results=new List<uint>();action.AddCompletionCallback(results.Add);
        Assert.Equal(EngineActionResult.Running,action.Update());Assert.Empty(results);Assert.Equal(0,action.Checks);
        action.SetInitialized=true;action.Result=0x03000004;Assert.Equal(0x03000004u,action.Update());Assert.Equal(new uint[]{0x03000004},results);Assert.Equal(1,action.Checks);
    }
    // reach: production — received packet queue -> MessageHandler.ProcessMessages, before unpack.
    [Fact]
    public void NoRobotDropsEvenMalformedFatalPacketsAndCountsPoppedPacketsRatherThanTicks()
    {
        using var rig=new M4ControlTests.Rig();
        var queue=(Queue<byte[]>)typeof(RobotConnectionManager).GetField("_data",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(rig.Engine.Rcm)!;
        var logs=new List<EngineLogEntry>();rig.Engine.LogEntry+=logs.Add;
        uint before=rig.Engine.Handler.ReceivedPacketCount;queue.Enqueue(Array.Empty<byte>());queue.Enqueue(new byte[]{0xD9,1,0,0,0,1});queue.Enqueue(new byte[]{0xD5,255});
        rig.Engine.Handler.ProcessMessages();Assert.Equal(before+3,rig.Engine.Handler.ReceivedPacketCount);Assert.Empty(logs);Assert.Empty(queue);
        rig.Engine.Handler.ProcessMessages();Assert.Equal(before+3,rig.Engine.Handler.ReceivedPacketCount);
    }
}
