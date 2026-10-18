using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Transport;
using Xunit;
namespace Cozmo.Protocol.Tests;

[Collection("ObjectID process statics")]
public sealed class M8CompoundCheckedRowsTests
{
    private sealed class Child : ActionRunner
    {
        private readonly string _name;
        internal uint Result;
        internal bool Locked;
        internal int Updates,Resets,Deletes;
        internal Func<uint>? Script;
        internal Action? OnDelete,OnReset;
        internal Child(string name="a"):base(8,1){_name=name;}
        protected override string ActionName=>_name;
        protected override float EngineClockSeconds=>0;
        protected override bool AreAnyTracksLocked(uint mask)=>Locked;
        public override uint CheckIfDone()=>Result;
        public override bool CanInterrupt()=>false;
        public override uint Update(){Updates++;if(Script is {} script){State=script();return State;}return base.Update();}
        public override void Reset(bool unlock){OnReset?.Invoke();Resets++;base.Reset(unlock);}
        public override void WatcherEnding(){Deletes++;OnDelete?.Invoke();base.WatcherEnding();}
    }
    private sealed class Sequential : CompoundActionSequential
    {
        internal uint HookResult;
        internal Sequential(Func<float> clock,params ActionRunner?[] children):base(clock,children){}
        internal void Delay(float value)=>SetDelay(value);
        protected override uint DerivedPreTick()=>HookResult;
    }
    private sealed class ConstructorGroup : CompoundAction
    {
        internal ConstructorGroup(SourceLogSink log,params ActionRunner?[] children):base(()=>0,children,log){}
    }
    private sealed record Entry(EngineLogLevel Level,string? Channel,string Key,string Text);
    private static SourceLogSink Sink(List<Entry> entries)=>(l,c,k,t)=>entries.Add(new(l,c,k,t));

    [Fact]
    public void NameReassignmentAndLoggingByteFollowEachAdd()
    {
        var a=new Child("a");var b=new Child("b");var logs=new List<Entry>();
        var group=new ConstructorGroup(Sink(logs),null,a){TimeoutWarningEnabled=false};
        Assert.Equal("[a]",group.NativeName);Assert.True(a.TimeoutWarningEnabled);
        group.AddAction(b,false,true);Assert.Equal("[a+b]",group.NativeName);Assert.False(b.TimeoutWarningEnabled);
        Assert.Equal(new Entry(EngineLogLevel.Warning,null,"ICompoundAction.NullActionPointer","Refusing to add a null action pointer to group"),Assert.Single(logs));
    }
    [Fact]
    public void NaNMoveDeadlineWaitsImmediatelyAndOnLaterTicks()
    {
        float now=float.NaN;var a=new Child();var b=new Child("b");var group=new Sequential(()=>now,a,b);
        group.Delay(1);Assert.Equal(0x01000000u,group.Update());Assert.Equal(0,b.Updates);
        now=0;Assert.Equal(0x01000000u,group.Update());Assert.Equal(0,b.Updates);
    }
    [Theory]
    [InlineData(false,1)] [InlineData(true,2)]
    public void CompletedChildrenGetTheExactAlreadyPreppedCount(bool ignored,int expected)
    {
        var logs=new List<Entry>();var child=new Child{Result=ignored?0x03000018u:0};
        var group=new CompoundActionParallel(()=>0){SourceLog=Sink(logs),TimeoutWarningEnabled=false};group.AddAction(child,ignored,false);
        Assert.Equal(0u,group.Update());
        Assert.Equal(expected,logs.Count(e=>e.Key=="IActionRunner.PrepForCompletion.AlreadyPrepped"&&e.Text==$"a [{unchecked((int)child.Tag)}]"));
        Assert.All(logs.Where(e=>e.Key.EndsWith("AlreadyPrepped")),e=>Assert.Equal((EngineLogLevel.Debug,"Actions"),(e.Level,e.Channel)));
    }
    [Fact]
    public void LockFailedChildHasNoAlreadyPreppedAtFirstStore()
    {
        var logs=new List<Entry>();var child=new Child{Locked=true};var group=new CompoundActionSequential(()=>0){SourceLog=Sink(logs)};
        group.AddAction(child,false,false);Assert.Equal(0x03000019u,group.Update());
        Assert.DoesNotContain(logs,e=>e.Key=="IActionRunner.PrepForCompletion.AlreadyPrepped");
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void InvalidCategoryRepeatsSameChildOnlyInParallel(bool parallel)
    {
        var child=new Child();int calls=0;child.Script=()=>++calls==1?0x05000000u:0x01000000u;
        CompoundAction group=parallel?new CompoundActionParallel(()=>0,child):new CompoundActionSequential(()=>0,child);
        Assert.Equal(parallel?0x01000000u:0u,group.Update());Assert.Equal(parallel?2:1,child.Updates);
        Assert.Same(child,Assert.Single(group.Children)); // scripted finite input checks branch mechanics, not a native invalid-loop escape
    }
    [Fact]
    public void DerivedFailureInfoAndNextActionInfoAreUngated()
    {
        var logs=new List<Entry>();var child=new Child();var derived=new Sequential(()=>0,child){HookResult=1,SourceLog=Sink(logs),TimeoutWarningEnabled=false};
        Assert.Equal(0x0300001Cu,derived.Update());Assert.Equal(0,child.Updates);
        Assert.Contains(new Entry(EngineLogLevel.Info,"Unnamed","CompoundActionSequential.UpdateInternal.UpdateDerivedFailed",""),logs);
        logs.Clear();var b=new Child("b"){Result=0x01000000};var sequence=new CompoundActionSequential(()=>0){SourceLog=Sink(logs),TimeoutWarningEnabled=false};
        sequence.AddAction(new Child(),false,false);sequence.AddAction(b,false,false);sequence.Update();
        Assert.Contains(new Entry(EngineLogLevel.Info,"Unnamed","CompoundActionSequential.Update.NextAction",$"Moving to action b [{unchecked((int)b.Tag)}]"),logs);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void RetryDiagnosticsPrecedeReset(bool parallel)
    {
        var logs=new List<Entry>();var child=new Child{Result=0x04000000};
        CompoundAction group=parallel?new CompoundActionParallel(()=>0):new CompoundActionSequential(()=>0);
        group.SourceLog=Sink(logs);group.RetriesRemain=1;group.AddAction(child,false,false);
        child.OnReset=()=>{Assert.Equal(0x02000001u,group.State);Assert.Contains(logs,e=>e.Key.EndsWith("Update.Retrying"));};
        Assert.Equal(0x01000000u,group.Update());
        Assert.Contains(new Entry(EngineLogLevel.Info,parallel?"Actions":"Unnamed",parallel?"CompoundActionParallel.Update.Retrying":"CompoundActionSequential.Update.Retrying","a triggered retry"),logs);
        Assert.Equal((byte)0,group.RetriesRemain);Assert.Equal(1,child.Resets);
    }
    [Fact]
    public void IgnoredSequentialFailureUsesTheCapturedClock()
    {
        int reads=0;var child=new Child{Result=0x03000018};var next=new Child("b"){Result=0x01000000};
        var group=new Sequential(()=>{reads++;return 0;});group.Delay(1);group.AddAction(child,true,false);group.AddAction(next,false,false);
        Assert.Equal(0x01000000u,group.Update());Assert.Equal(1,reads);Assert.Equal(0,next.Updates);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void SequentialFailureAndIgnoreDiagnosticsMatchTheCheckedFormats(bool ignored)
    {
        var logs=new List<Entry>();var child=new Child{Result=0x03000018};var group=new CompoundActionSequential(()=>0){SourceLog=Sink(logs)};
        group.AddAction(child,ignored,false);group.Update();
        Assert.Contains(new Entry(ignored?EngineLogLevel.Info:EngineLogLevel.Debug,"Actions","CompoundActionSequential.UpdateInternal",
            ignored?$"Ignoring failure for a[{unchecked((int)child.Tag)}] moving to next action":$"Current action a[{unchecked((int)child.Tag)}] failed with TIMEOUT deleting"),logs);
    }
    [Fact]
    public void ListOwnershipIsRemovedBeforeChildDestructor()
    {
        var child=new Child();var group=new CompoundActionParallel(()=>0,child);child.OnDelete=()=>Assert.Empty(group.Children);
        group.Prep();group.WatcherEnding();Assert.Equal(1,child.Deletes);
    }
    [Fact]
    public void RealEngineTickDrivesTheNamedCompound()
    {
        using var robot=CozmoRobot.CreateOffline();var group=new CompoundActionParallel(children:new Child());
        Assert.Equal("[a]",group.NativeName);robot.Engine.Robot!.ActionList.QueueAction(QueueActionPosition.AtEnd,group);
        robot.Engine.Tick();Assert.True(robot.Engine.Robot.ActionList.IsEmpty);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void RequestInfoRunsWithoutInitializedOrCurrentGuard(bool initialized)
    {
        using var robot=CozmoRobot.CreateOffline();var logs=new List<EngineLogEntry>();robot.Engine.LogEntry+=logs.Add;
        var context=new BehaviorContext{Robot=robot,Triggers=new AnimationTriggerMap()};using var manager=new BehaviorManager(context){Initialized=initialized};
        manager.RequestCurrentBehaviorEndImmediately("reason");
        Assert.Contains(logs,e=>e.Level==EngineLogLevel.Info&&e.Channel=="Behaviors"&&e.Name=="BehaviorManager.RequestCurrentBehaviorEndImmediately"&&e.Text=="Forcing current behavior to stop: reason");
        // Request body stops at visible M8-019 switch gap. No assertion of complete stop/release/transition fidelity.
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    // reach: unit (substitutes a child action, behavior-end hook and direct Sensors.Handle; production D4 queue/tick case is tested separately).
    public void RobotStoppedCliffGateKeepsRequestCancelBroadcastOrder(bool enabled)
    {
        using var robot=CozmoRobot.CreateOffline();var order=new List<string>();var child=new Child{Result=0x01000000,OnDelete=()=>order.Add("delete")};
        robot.Engine.Robot!.ActionList.QueueAction(QueueActionPosition.AtEnd,child);robot.Engine.Robot.ActionList.Update();
        robot.Sensors.SetCliffSensorEnabled(enabled);robot.Sensors.RequestCurrentBehaviorEndImmediately=r=>{Assert.Equal("HandleRobotStopped",r);order.Add("request");};
        robot.Sensors.RobotStopped+=()=>order.Add("broadcast");robot.Sensors.Handle(new Cozmo.Protocol.RobotStopped());
        Assert.Equal(enabled?new[]{"request","delete"}:Array.Empty<string>(),order);robot.Engine.DrainAppDelivery();
        Assert.Equal(enabled?new[]{"request","delete","broadcast"}:Array.Empty<string>(),order);
        robot.Sensors.RequestCurrentBehaviorEndImmediately=null;
    }
}
