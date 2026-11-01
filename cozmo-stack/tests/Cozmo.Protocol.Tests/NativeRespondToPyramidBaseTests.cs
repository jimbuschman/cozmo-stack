using System.Reflection;
using System.Text.Json;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Xunit;
using Xunit.Abstractions;
using Fixture=Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class NativeRespondToPyramidBaseTests(ITestOutputHelper output)
{
    private static NativeRespondToPyramidBaseBehavior Owner(Fixture f)=>Assert.IsType<NativeRespondToPyramidBaseBehavior>(f.Runtime.Bound["RespondToPyramidBase"]);
    private static void Unlock(Fixture f)
    {
        // Supplied native NV recipient unit input, not a natural unlock claim.
        var bytes=new byte[2048];System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bytes,3);
        f.Robot.Progression!.ReadSink(new NvResult(0,bytes));
    }
    [Fact]
    public void ChangedProfileIsUnavailableRatherThanUsingShippedBody()
    {
        using var f=new Fixture();var factory=new BehaviorFactory();var context=new BehaviorFactoryContext{NativeRobot=f.Robot,Manipulation=f.M};
        var container=new BehaviorContainer(factory,context,Array.Empty<KeyValuePair<BehaviorID,JsonElement>>());
        using var changed=JsonDocument.Parse("""{"behaviorClass":"OnConfigSeen","behaviorID":"RespondToPyramidBase","requiredUnlockId":"BuildPyramid","configTriggers":["PyramidBase"],"animTriggers":["Count"]}""");
        var result=factory.CreateBehavior(container,BehaviorClass.OnConfigSeen,context,changed.RootElement,out var status);
        Assert.Null(result);Assert.Equal(BehaviorCreateStatus.NotImplemented,status);
    }
    [Fact]
    public void ActualPublishedBaseAndF32ReadinessHistory()
    {
        using var f=new Fixture(preloadAnimations:true,pumpAnimations:true);Unlock(f);
        var owner=Owner(f);Assert.Same(owner,f.Robot.BehaviorManagerOwner!.Find(owner.Id));
        f.Add(7,100);f.Add(8,152);var config=f.Runtime.Configurations;config.ForceUpdate();config.Update();
        Assert.Contains(config.Snapshot.Bases,b=>b.MinId==7 && b.MaxId==8);
        owner.SeedHistoryForTest(0f,0f,0);f.Rig.Engine.Timer.UpdateTime(10_000_000_000);
        Assert.False(owner.EvaluateHostReadiness(f.Runtime.Context).Runnable); // sparse first query stamps growth but elapsed exceeds limit
        f.Rig.Engine.Timer.UpdateTime(10_001_000_000);Assert.False(owner.EvaluateHostReadiness(f.Runtime.Context).Runnable);
        owner.SeedHistoryForTest(9f,0f,0);f.Rig.Engine.Timer.UpdateTime(10_000_000_000);
        var first=owner.EvaluateHostReadiness(f.Runtime.Context);Assert.True(first.Available);Assert.True(first.Runnable);
        Assert.Equal((10f,10f,1),owner.History);
        Assert.True(owner.EvaluateHostReadiness(f.Runtime.Context).Runnable); // exact same clock remains inside epsilon
        f.Rig.Engine.Timer.UpdateTime(10_001_000_000);Assert.False(owner.EvaluateHostReadiness(f.Runtime.Context).Runnable);
        Assert.Equal(10f,owner.History.Growth);
    }
    [Fact]
    public void FreshOwnerObservesActualPublishedBaseAndStarts()
    {
        using var f=new Fixture(preloadAnimations:true,pumpAnimations:true);
        Unlock(f);var owner=Owner(f);
        Assert.Equal((0f,0f,0),owner.History);Assert.Empty(f.Runtime.Configurations.Snapshot.Bases);
        var initial=owner.EvaluateHostReadiness(f.Runtime.Context);Assert.True(initial.Available);
        float now=f.Rig.Engine.Timer.SecondsF;
        f.Add(7,100);f.Add(8,152);var config=f.Runtime.Configurations;config.ForceUpdate();config.Update();
        Assert.Contains(config.Snapshot.Bases,b=>b.MinId==7 && b.MaxId==8);Assert.Equal(now,f.Rig.Engine.Timer.SecondsF);
        var ready=owner.EvaluateHostReadiness(f.Runtime.Context);Assert.True(ready.Available);Assert.True(ready.Runnable);
        Assert.Equal((now,now,1),owner.History);
        Assert.True(f.Robot.BehaviorManagerOwner!.StartAsync(owner.Id,now).GetAwaiter().GetResult());
        Assert.Equal((AnimationTrigger)20,Assert.IsType<TriggerLiftSafeAnimationAction>(owner.OwnedRunner).Request.Trigger);
    }
    [Fact]
    public void StrictF32ThresholdsAndZeroClockUseSourceComparisons()
    {
        using var f=new Fixture();Unlock(f);var owner=Owner(f);f.Rig.Engine.Timer.UpdateTime(0);
        Assert.True(owner.EvaluateHostReadiness(f.Runtime.Context).Runnable); // constructor zero clocks and empty cache
        float interval=BitConverter.UInt32BitsToSingle(0x409FFFEB),epsilon=BitConverter.UInt32BitsToSingle(0x3727C5AC);
        owner.SeedHistoryForTest(-interval,0,0);Assert.False(owner.EvaluateHostReadiness(f.Runtime.Context).Runnable);
        owner.SeedHistoryForTest(0,-epsilon,0);Assert.False(owner.EvaluateHostReadiness(f.Runtime.Context).Runnable);
    }
    [Fact]
    public void UnknownCommonAndUnknownCountPermanentlyWithdrawHistoryButNotExplicitInit()
    {
        using var f=new Fixture(preloadAnimations:true,pumpAnimations:true);var owner=Owner(f);
        var locked=owner.EvaluateHostReadiness(f.Runtime.Context);Assert.True(locked.Available);Assert.False(locked.Runnable);Assert.True(owner.HistoryAvailable);
        var property=typeof(ProgressionUnlockOwner).GetProperty("OverridesKnownEmpty",BindingFlags.Instance|BindingFlags.NonPublic)!;
        property.SetValue(f.Robot.Progression,false);
        try {Assert.False(owner.EvaluateHostReadiness(f.Runtime.Context).Available);Assert.False(owner.HistoryAvailable);}
        finally {property.SetValue(f.Robot.Progression,true);}
        Unlock(f);Assert.False(owner.EvaluateHostReadiness(f.Runtime.Context).Available);
        using var scope=new BehaviorScope();owner.StartAsync(f.Runtime.Context,scope,default).GetAwaiter().GetResult();
        Assert.IsType<TriggerLiftSafeAnimationAction>(owner.OwnedRunner);owner.Stop(BehaviorStopReason.Interrupted);
        Assert.False(owner.HistoryAvailable);Assert.False(owner.Resume(null,f.Rig.Engine.Timer.SecondsF));Assert.Null(owner.OwnedRunner);
        Assert.False(owner.HistoryAvailable);
    }
    [Fact]
    public void FailedActualConfigurationPublicationPoisonsOnlyReachedOwnEvaluation()
    {
        using var f=new Fixture(preloadAnimations:true,pumpAnimations:true);Unlock(f);var owner=Owner(f);
        var carry=f.Robot.CarryingOwner;f.Robot.CarryingOwner=null;
        try {f.Runtime.Configurations.ForceUpdate();f.Runtime.Configurations.Update();Assert.False(f.Runtime.Configurations.InputsAvailable);}
        finally {f.Robot.CarryingOwner=carry;}
        Assert.False(owner.EvaluateHostReadiness(f.Runtime.Context).Available);Assert.False(owner.HistoryAvailable);
        f.Runtime.Configurations.Update();Assert.True(f.Runtime.Configurations.InputsAvailable);
        Assert.False(owner.EvaluateHostReadiness(f.Runtime.Context).Available);
    }
    [Fact]
    public void KnownCommonGateRejectionsDoNotTouchOwnHistory()
    {
        using var f=new Fixture();Unlock(f);var owner=Owner(f);owner.SeedHistoryForTest(9,8,2);var history=owner.History;
        f.Robot.CarryingOwner!.SetCarrying(7);
        try
        {
            var carrying=owner.EvaluateHostReadiness(f.Runtime.Context);Assert.True(carrying.Available);Assert.False(carrying.Runnable);
            f.Robot.CarryingOwner.UnsetCarrying();f.Rig.Robot.Sensors.SetOnChargerPlatform(true);
            var platform=owner.EvaluateHostReadiness(f.Runtime.Context);Assert.True(platform.Available);Assert.False(platform.Runnable);
            f.Rig.Robot.Sensors.SetOnChargerPlatform(false);
            owner.RobotState355=()=>true; // supplied common-byte unit seam; source gate is independently exercised here.
            var offTreads=owner.EvaluateHostReadiness(f.Runtime.Context);Assert.True(offTreads.Available);Assert.False(offTreads.Runnable);
            Assert.True(owner.HistoryAvailable);Assert.Equal(history,owner.History);
        }
        finally {owner.RobotState355=null;f.Rig.Robot.Sensors.SetOnChargerPlatform(false);f.Robot.CarryingOwner.UnsetCarrying();}
    }
    [Theory]
    [InlineData(0u)] [InlineData(0x03000018u)]
    public void ExplicitStartOwnsOneSourceActionAndAnySuppliedResultCompletes(uint result)
    {
        using var f=new Fixture(preloadAnimations:true,pumpAnimations:true);var owner=Owner(f);int objectives=0;
        using var sub=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x8B,true,_=>objectives++);
        long needs=f.Rig.Engine.Needs.NativePendingHistoryEpoch;
        using var scope=new BehaviorScope();owner.StartAsync(f.Runtime.Context,scope,default).GetAwaiter().GetResult();
        var action=Assert.IsType<TriggerLiftSafeAnimationAction>(owner.OwnedRunner);
        Assert.Equal((AnimationTrigger)20,action.Request.Trigger);Assert.Equal(1,action.Request.Argument2);Assert.True(action.Request.Argument3);
        Assert.Equal(0,action.Request.Argument4);Assert.Equal(60f,action.Request.Argument5);Assert.False(action.Request.Argument6);
        // Supplied typed terminal result; not a firmware-completion claim.
        action.State=result;f.Robot.ActionList.Update();Assert.False(owner.Update(f.Runtime.Context,f.Rig.Engine.Timer.SecondsF*1000));
        Assert.Null(owner.OwnedRunner);Assert.Equal(0,objectives);Assert.Equal(needs,f.Rig.Engine.Needs.NativePendingHistoryEpoch);
    }
    [Fact]
    public void OldOwnerCannotStartOrResumeAfterReplacement()
    {
        using var f=new Fixture();var owner=Owner(f);var original=f.Robot;
        using var first=new BehaviorScope();owner.StartAsync(f.Runtime.Context,first,default).GetAwaiter().GetResult();
        var old=Assert.IsAssignableFrom<ActionRunner>(owner.OwnedRunner);uint tag=old.Tag;
        f.Rig.Engine.Robots.RemoveRobot(1,false);var replacement=f.Rig.Engine.Robots.AddRobot(1,withRic:false)!;
        var next=Assert.IsType<NativeRespondToPyramidBaseBehavior>(replacement.NativeBehaviorRuntime!.Bound[owner.Id]);
        int sent=f.Rig.Mark();f.Rig.Engine.AnimationMessages.Deliver(0x5A,true,new RobotCompletedAction(tag,old.Type,0,Array.Empty<uint>(),0));
        Assert.Equal(sent,f.Rig.Mark());Assert.Null(next.OwnedRunner);
        using var scope=new BehaviorScope();Assert.Throws<ObjectDisposedException>(()=>owner.StartAsync(f.Runtime.Context,scope,default).GetAwaiter().GetResult());
        Assert.Null(owner.OwnedRunner);Assert.Null(next.OwnedRunner);Assert.True(next.HistoryAvailable);Assert.NotSame(original,replacement);
    }
    [Fact]
    public void StopFromConstructorDiagnosticDoesNotAdmitStaleAction()
    {
        using var f=new Fixture(preloadAnimations:true,pumpAnimations:true);var owner=Owner(f);bool stopped=false;
        void OnMissing(string text){if(stopped || !text.Contains("M8-021 trigger DAS"))return;stopped=true;owner.Stop(BehaviorStopReason.Interrupted);}
        SteppedBehavior.ResetMissingForTests();SteppedBehavior.MissingReported+=OnMissing;
        try {using var scope=new BehaviorScope();owner.StartAsync(f.Runtime.Context,scope,default).GetAwaiter().GetResult();}
        finally {SteppedBehavior.MissingReported-=OnMissing;}
        Assert.True(stopped);Assert.Null(owner.OwnedRunner);Assert.False(owner.EngineRunning);
    }
    [Fact]
    public void ActualWireAnimationUsesTheOwnedActionAndFirmwareAcknowledgments()
    {
        using var f=new Fixture(preloadAnimations:true,pumpAnimations:true);
        var nv=f.Rig.Engine.NvStorage!;var startup=new List<string>();
        startup.Add($"before full={f.Robot.FirstFullStateHandled} synced={f.Robot.TimeSynced} ready={f.Robot.ReadyToStream} idle={nv.IsIdle} inFlight={nv.InFlightTag} queued={string.Join(',',nv.QueuedTags)}");
        for(int i=0;i<100 && (!nv.IsIdle || !f.Robot.ReadyToStream);i++)
        {
            if(nv.InFlightTag is {} inFlight)
            {
                var command=f.Rig.SentSince(0).OfType<NVCommand>().Last(c=>c.Tag==inFlight);
                startup.Add($"reply tag={command.Tag:X} op={command.Op}");
                f.Rig.Data(new NVOpResult{Tag=command.Tag,Op=command.Op,Result=command.Op==NvStorageComponent.OpRead?(sbyte)-1:(sbyte)0,Length=0,Data=Array.Empty<byte>()});
            }
            f.Rig.Tick();
        }
        output.WriteLine(string.Join(" | ",startup));
        Assert.True(f.Robot.FirstFullStateHandled);Assert.True(f.Robot.TimeSynced);Assert.True(nv.IsIdle);Assert.True(f.Robot.ReadyToStream);
        f.Rig.Tick();Assert.True(f.Rig.Robot.AnimationStreamingOpen);
        startup.Add($"after full={f.Robot.FirstFullStateHandled} synced={f.Robot.TimeSynced} ready={f.Robot.ReadyToStream} idle={nv.IsIdle} gate={f.Rig.Robot.AnimationStreamingOpen}");
        output.WriteLine(startup[^1]);
        Unlock(f);var owner=Owner(f);
        int mark=f.Rig.Mark();var trace=new List<string>();var missing=new List<string>();void Missing(string text)=>missing.Add(text);
        SteppedBehavior.ResetMissingForTests();SteppedBehavior.MissingReported+=Missing;
        byte tag=0;bool start=false,end=false;
        try
        {
            Assert.True(f.Robot.BehaviorManagerOwner!.StartAsync(owner.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
            QueuedSelection.Keep(f.Rig.Robot,f.Robot.BehaviorManagerOwner,owner.Id);
            for(int i=0;i<200 && !end;i++)
            {
                f.Motors();var sent=f.Rig.SentSince(mark);mark=f.Rig.Mark();
                if(i<5||i%20==0)trace.Add($"i={i} time={f.Rig.Engine.Timer.SecondsF} current={f.Robot.BehaviorManagerOwner.Current?.Id} running={owner.EngineRunning} runner={owner.OwnedRunner?.GetType().Name} state={owner.OwnedRunner?.State:X8} tag={owner.OwnedRunner?.Tag} map={f.Runtime.Context.Triggers.GroupFor((AnimationTrigger)20)} clip={f.Rig.Robot.Animations.Playing} gate={f.Rig.Robot.AnimationStreamingOpen} wire={string.Join(',',sent.Select(m=>m.GetType().Name))}");
                foreach(var message in sent)
                {
                    if(message is StartOfAnimation begin){tag=begin.AnimId;start=true;f.Rig.Data(new AnimationStarted{AnimId=tag});}
                    else if(message is EndOfAnimation && start){end=true;f.Rig.Data(new AnimationEnded{AnimId=tag});}
                }
            }
        }
        finally {SteppedBehavior.MissingReported-=Missing;}
        string diagnostic=string.Join(" | ",trace)+" missing="+string.Join(" | ",missing);
        Assert.True(start,"no outbound Start: "+diagnostic);Assert.True(end,"no outbound End: "+diagnostic);Assert.NotEqual((byte)0,tag);
        f.Motors();f.Motors();Assert.Null(owner.OwnedRunner);
    }
}
