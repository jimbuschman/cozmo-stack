using System.Text.Json;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Xunit;
using Fixture=Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class NativeParentSwitchTimeTests
{
    private sealed class ObservingChooser(IBehaviorChooser inner,Action observe):IBehaviorChooser
    {
        public BehaviorChooserType Type=>inner.Type;
        public IReadOnlyList<string> BehaviorIds=>inner.BehaviorIds;
        public ChooserDecision GetDesiredActiveBehavior(IBehavior? current,double running,BehaviorContext context,double now)
        {observe();return inner.GetDesiredActiveBehavior(current,running,context,now);}
    }

    [Fact]
    public void ChangedOrdinaryChildCommitsCountSparkBeforeItsActualChooser()
    {
        // Observation wrapper delegates the real chooser; it supplies no selection or readiness result.
        using var f=new Fixture();var manager=f.Robot.BehaviorManagerOwner!;var runtime=f.Runtime;
        var child=runtime.FreeplayObserver.Freeplay.SubActivities.Single(a=>a.Id=="Socialize");
        var chooser=child.Chooser!;var selected=child.NativePublicStateSelected;var trace=new List<string>();
        child.NativePublicStateSelected=()=>{selected?.Invoke();trace.Add("selected");};
        var property=typeof(Activity).GetProperty("Chooser")!;
        property.SetValue(child,new ObservingChooser(chooser,()=>{trace.Add("desired");Assert.Equal(20f,manager.ParentSwitchTime);}));
        void OnLog(EngineLogEntry entry)
        {
            if(entry.Name!="BehaviorManager.SwitchToRequestedSpark")return;
            Assert.Equal(0x55,manager.ActiveSparkId);Assert.Equal(0x55,manager.RequestedSparkId);
            trace.Add("switch");f.Rig.Engine.Timer.UpdateTime(20_000_000_000);
        }
        f.Rig.Engine.LogEntry+=OnLog;
        try
        {
            f.Rig.Engine.Timer.UpdateTime(10_000_000_000);runtime.AdmitNativeHighLevel("Freeplay");
            Assert.Equal(new[]{"selected","switch","desired"},trace);Assert.Equal(20f,manager.ParentSwitchTime);
        }
        finally{f.Rig.Engine.LogEntry-=OnLog;property.SetValue(child,chooser);child.NativePublicStateSelected=selected;}
    }

    [Fact]
    public void AdapterUsesFreshClockOnceAndKeepsSeparateTimestamp()
    {
        using var f=new Fixture();using var manager=new BehaviorManager(f.Runtime.Context,null,false);
        Assert.Equal(-1f,manager.ParentSwitchTime);Assert.Equal(-1f,manager.FreeplayStartTime);
        f.Rig.Engine.Timer.UpdateTime(10_000_000_000);manager.HighLevelAdapterTail(true);
        Assert.Equal(10f,manager.FreeplayStartTime);Assert.Equal(-1f,manager.ParentSwitchTime);
        f.Rig.Engine.Timer.UpdateTime(20_000_000_000);manager.HighLevelAdapterTail(true);manager.HighLevelAdapterTail(false);
        Assert.Equal(10f,manager.FreeplayStartTime);Assert.Equal(-1f,manager.ParentSwitchTime);
    }
    [Fact]
    public void HighLevelSamplesAfterSelectedAndSameActivityDoesNotRestamp()
    {
        using var f=new Fixture();var runtime=f.Runtime;var manager=f.Robot.BehaviorManagerOwner!;
        var freeplay=runtime.FreeplayObserver.Freeplay;var prior=freeplay.NativePublicStateSelected;int callbacks=0;
        freeplay.NativePublicStateSelected=()=>{prior?.Invoke();callbacks++;f.Rig.Engine.Timer.UpdateTime(12_000_000_000);};
        try
        {
            f.Rig.Engine.Timer.UpdateTime(10_000_000_000);runtime.AdmitNativeHighLevel("Freeplay");
            Assert.Equal(1,callbacks);Assert.Equal(12f,manager.ParentSwitchTime);Assert.Equal(12f,manager.FreeplayStartTime);Assert.Equal(manager.ParentSwitchTime,runtime.HighLevelEntryTime);Assert.Equal(manager.ParentSwitchTime,manager.SparkSwitchTime);
            f.Rig.Engine.Timer.UpdateTime(20_000_000_000);runtime.AdmitNativeHighLevel("Freeplay");
            Assert.Equal(1,callbacks);Assert.Equal(12f,manager.ParentSwitchTime);
        }
        finally{freeplay.NativePublicStateSelected=prior;}
    }

    [Fact]
    public void RequestDoesNotStampButActualSparkSwitchWritesSharedField()
    {
        using var f=new Fixture();var manager=f.Robot.BehaviorManagerOwner!;var runtime=f.Runtime;
        f.Rig.Engine.Timer.UpdateTime(10_000_000_000);runtime.AdmitNativeHighLevel("Freeplay");float before=manager.ParentSwitchTime;
        f.Rig.Engine.Timer.UpdateTime(20_000_000_000);manager.SetNativeRequestedSpark(9,0);
        Assert.Equal(before,manager.ParentSwitchTime);manager.SwitchToNativeRequestedSpark();
        Assert.Equal(20f,manager.ParentSwitchTime);Assert.Equal(manager.ParentSwitchTime,runtime.HighLevelEntryTime);Assert.Equal(9,manager.ActiveSparkId);
    }

    [Fact]
    public void ActualManagerGateUsesFloatBoundaryAndRejectsReplacedOwner()
    {
        using var f=new Fixture();var robot=f.Robot;var manager=robot.BehaviorManagerOwner!;
        using var config=JsonDocument.Parse("""{"requiredRecentSwitchToParent_sec":0.25}""");
        var input=new NativeBehaviorBaseInputs(robot,config.RootElement);
        f.Rig.Engine.Timer.UpdateTime(0);manager.StampParentSwitchTime();
        float boundary=.25f+BitConverter.UInt32BitsToSingle(0x3727C5AC);
        f.Rig.Engine.Timer.UpdateTime((long)Math.Round((double)boundary*1_000_000_000));
        Assert.Equal(boundary,f.Rig.Engine.Timer.SecondsF);Assert.True(input.Allows(f.Runtime.Context));
        f.Rig.Engine.Timer.UpdateTime((long)Math.Ceiling((double)MathF.BitIncrement(boundary)*1_000_000_000));Assert.False(input.Allows(f.Runtime.Context));
        var other=new BehaviorManager(f.Runtime.Context);try{robot.BehaviorManagerOwner=other;Assert.False(SteppedBehavior.EvaluateHostOperation(()=>input.Allows(f.Runtime.Context)).Available);}
        finally{robot.BehaviorManagerOwner=manager;other.Dispose();}
    }

    [Fact]
    public void DefinedConstructorMinusOneIsComparedWithoutSpecialInvalidityGate()
    {
        // Isolated source-field boundary, supplying the manager ctor's literal -1f.
        using var f=new Fixture();var manager=f.Robot.BehaviorManagerOwner!;
        typeof(BehaviorManager).GetProperty("ParentSwitchTime",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.SetValue(manager,-1f);
        using var config=JsonDocument.Parse("""{"requiredRecentSwitchToParent_sec":0.25}""");var input=new NativeBehaviorBaseInputs(f.Robot,config.RootElement);
        f.Rig.Engine.Timer.UpdateTime(-1_000_000_000);Assert.True(input.Allows(f.Runtime.Context));
        f.Rig.Engine.Timer.UpdateTime(0);Assert.False(input.Allows(f.Runtime.Context));
    }

    [Fact]
    public void SparkSwitchLogReplacementDoesNotWriteRetiredManager()
    {
        using var f=new Fixture();var old=f.Robot.BehaviorManagerOwner!;old.SetNativeRequestedSpark(9,0);
        float prior=old.ParentSwitchTime;int active=old.ActiveSparkId;EngineRobot? replacement=null;float replacementStamp=0;
        void OnLog(EngineLogEntry entry)
        {
            if(entry.Name!="BehaviorManager.SwitchToRequestedSpark"||replacement is not null)return;
            f.Rig.Engine.Robots.RemoveRobot(1,false);replacement=f.Rig.Engine.Robots.AddRobot(1,withRic:false);
            replacementStamp=replacement!.BehaviorManagerOwner!.ParentSwitchTime;
        }
        f.Rig.Engine.LogEntry+=OnLog;
        try {f.Rig.Engine.Timer.UpdateTime(20_000_000_000);old.SwitchToNativeRequestedSpark();}
        finally {f.Rig.Engine.LogEntry-=OnLog;}
        Assert.NotNull(replacement);Assert.Equal(prior,old.ParentSwitchTime);Assert.Equal(active,old.ActiveSparkId);
        Assert.NotSame(old,replacement!.BehaviorManagerOwner);Assert.Equal(replacementStamp,replacement.BehaviorManagerOwner!.ParentSwitchTime);
    }

    [Fact]
    public void RobotReplacementDoesNotReuseOldManagerTimestampOrGateOwner()
    {
        using var f=new Fixture();var original=f.Robot;var context=f.Runtime.Context;var old=original.BehaviorManagerOwner!;
        using var config=JsonDocument.Parse("""{"requiredRecentSwitchToParent_sec":0.25}""");var input=new NativeBehaviorBaseInputs(original,config.RootElement);
        f.Rig.Engine.Timer.UpdateTime(1_000_000_000);old.StampParentSwitchTime();
        f.Rig.Engine.Timer.UpdateTime(10_000_000_000);f.Rig.Engine.Robots.RemoveRobot(1,false);
        var replacement=f.Rig.Engine.Robots.AddRobot(1,withRic:false)!;
        Assert.NotSame(old,replacement.BehaviorManagerOwner);Assert.Equal(10f,replacement.BehaviorManagerOwner!.ParentSwitchTime);
        Assert.False(SteppedBehavior.EvaluateHostOperation(()=>input.Allows(context)).Available);
    }
}
