using System.Reflection;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;
namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class HelperStopAndPyramidLoopTests
{
    private sealed class Probe : SteppedBehavior
    {
        internal Probe():base("stop-probe","Probe"){}
        protected override void OnStart(){}
        internal bool Delegate(HelperRef h)=>SmartDelegateToHelper(h,null,null);
        internal bool StopHelper()=>StopHelperWithoutCallback();
    }
    private sealed class Helper(CozmoRobot r,SteppedBehavior b,BehaviorHelperComponent c,string name="H")
        : IHelper(r,name,b,c.Factory)
    {
        internal Action<bool>? Stopping;
        internal void State(int state)=>StoreStatus((BehaviorStatus)state);
        public override bool ShouldCancelDelegates(CozmoRobot robot)=>false;
        protected override BehaviorStatus Init(CozmoRobot robot)=>BehaviorStatus.Running;
        protected override BehaviorStatus UpdateWhileActiveInternal(CozmoRobot robot)=>BehaviorStatus.Running;
        protected override void OnStopSlot24(bool first)=>Stopping?.Invoke(first);
    }
    private static HelperRef Ref(BehaviorHelperComponent c,IHelper h)
    { IHelper? raw=h;return c.AddHelperToComponent(ref raw); }

    // 5B6850..68EA: status0 failure,2 success,1 active cancel/inactive_stop; other no event.
    [Theory]
    [InlineData(0,true,"failure")] [InlineData(2,false,"success")]
    [InlineData(1,true,"cancel")] [InlineData(1,false,"inactive_stop")] [InlineData(7,true,null)]
    public void StopEventsUseExactStateMappingAndIntegerDuration(int state,bool active,string? suffix)
    {
        using var robot=CozmoRobot.CreateOffline(); var b=new Probe();var c=new BehaviorHelperComponent(()=>1);
        var h=new Helper(robot,b,c);h.State(state);
        robot.Engine.Timer.UpdateTime(3_900_000_000);
        var logs=new List<EngineLogEntry>();robot.Engine.LogEntry+=logs.Add;
        h.Stop(active);
        var events=logs.Where(e=>e.Level==EngineLogLevel.Event).ToArray();
        if(suffix is null){Assert.Empty(events);return;}
        var e=Assert.Single(events);Assert.Equal("robot.behavior_helper."+suffix,e.Name);
        Assert.Null(e.Channel);Assert.EndsWith("H",e.Text);Assert.Equal("3",e.Data["$data"]);
    }

    // 5B69A8 converts before sign test; negative integers log Error, set errG, then still emit duration0.
    [Theory] [InlineData(0.5f,false)] [InlineData(1.5f,true)]
    public void NegativeDurationTruncatesBeforeTheErrorAndStillEmits(float start,bool error)
    {
        using var robot=CozmoRobot.CreateOffline();var b=new Probe();var c=new BehaviorHelperComponent(()=>1);
        var h=new Helper(robot,b,c);h.State(1);
        typeof(IHelper).GetField("_startTime",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(h,start);
        var logs=new List<EngineLogEntry>();robot.Engine.LogEntry+=logs.Add;h.Stop(true);
        Assert.Equal(error,logs.Any(e=>e.Name=="IHelper.Stop.InvalidTime"));
        if(error)Assert.EndsWith("H: Negative duration -1 secs (started 1.500000, stopped 0.000000)",logs.Single(e=>e.Name=="IHelper.Stop.InvalidTime").Text);
        Assert.Equal("0",Assert.Single(logs.Where(e=>e.Level==EngineLogLevel.Event)).Data["$data"]);
        Assert.Equal("IHelper.Stop",logs[0].Name);
    }

    [Fact]
    public void WrapperLocksItsWeakOwnerAndStopsOnlyTheMatchedBottom()
    {
        using var robot=CozmoRobot.CreateOffline();var ai=new AIComponent(()=>1);var b=new Probe();
        b.AttachRun(new BehaviorContext{Robot=robot,Triggers=new(),AI=ai},new BehaviorScope());
        var logs=new List<EngineLogEntry>();robot.Engine.LogEntry+=logs.Add;
        Assert.False(b.StopHelper());
        var h=new Helper(robot,b,ai.Helpers);var r=Ref(ai.Helpers,h);
        Assert.True(b.Delegate(r));int ownersDuring=0;h.Stopping=_=>ownersDuring=r.Control.Owners;
        Assert.True(b.StopHelper());Assert.Equal(3,ownersDuring); // caller,stack,temporary weak lock
        Assert.Equal(1,r.Control.Owners);Assert.Empty(ai.Helpers.StackHelpers);
        Assert.Contains(logs,e=>e.Name=="stop-probe.SmartStopHelper"&&e.Text.EndsWith("Behavior stopping its helper"));
        r.Release();Assert.False(b.StopHelper());
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void StopEventPrecedesActiveActionCancellationAndVirtualTail(bool active)
    {
        using var robot=CozmoRobot.CreateOffline();var b=new Probe();var c=new BehaviorHelperComponent(()=>1);
        b.AttachRun(new BehaviorContext{Robot=robot,Triggers=new()},new BehaviorScope());
        typeof(SteppedBehavior).GetField("_engineRunning",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(b,true);
        b.StartActing();var h=new Helper(robot,b,c);h.State(1);
        var seen=new List<bool>();robot.Engine.LogEntry+=e=>{if(e.Level==EngineLogLevel.Event)seen.Add(b.HasCurrentAction);};
        h.Stopping=_=>seen.Add(b.HasCurrentAction);h.Stop(active);
        Assert.Equal(new[]{true,!active},seen); // 5B67D0 event, D4..E6 active cancel, EA..F2 virtual tail
    }

    [Fact]
    public void ClearReloadsEndAfterStopAndEnforcesExactlyOneThousandPasses()
    {
        using var robot=CozmoRobot.CreateOffline();var b=new Probe();
        var logs=new List<(EngineLogLevel,string?,string,string)>();
        var c=new BehaviorHelperComponent(()=>1,sourceLog:(l,ch,k,t)=>logs.Add((l,ch,k,t)));
        var bottom=Ref(c,new Helper(robot,b,c,"bottom"));c.PushHelperOntoStackAndUpdate(robot,bottom);
        var top=new Helper(robot,b,c,"top");var topRef=Ref(c,top);c.PushHelperOntoStackAndUpdate(robot,topRef);
        // Give the source vector retained spare capacity before capturing its target iterator; callback pushes
        // then stay within storage, rather than relying on an undefined iterator after reallocation.
        var spare=Ref(c,new Helper(robot,b,c,"reserve"));c.PushHelperOntoStackAndUpdate(robot,spare);
        c.ClearStackFromTopToIter(2);spare.Release();
        int calls=0;var flags=new List<bool>();
        top.Stopping=first=>{calls++;flags.Add(first);var h=Ref(c,new Helper(robot,b,c,"temporary"));c.PushHelperOntoStackAndUpdate(robot,h);h.Release();};
        c.ClearStackFromTopToIter(0);
        Assert.Equal(1000,calls);Assert.True(flags[0]);Assert.All(flags.Skip(1),f=>Assert.False(f));
        Assert.Equal(new IHelper[]{bottom.Pointer,top},c.StackHelpers); // appended temporary popped, saved top stays
        var error=Assert.Single(logs.Where(e=>e.Item1==EngineLogLevel.Error));
        Assert.Null(error.Item2);Assert.Equal("LoopBoundOverflow",error.Item3);
        Assert.Equal("../../../../engine/aiComponent/behaviorHelperComponent.cpp:262",error.Item4);
        bottom.Release();topRef.Release();
    }

    private static void Ids(BuildPyramidBaseBehavior b,uint stat,uint moving,uint top)
    {
        typeof(BuildPyramidBaseBehavior).GetProperty("StaticBlockId")!.SetValue(b,stat);
        typeof(BuildPyramidBaseBehavior).GetProperty("BaseBlockId")!.SetValue(b,moving);
        typeof(BuildPyramidBaseBehavior).GetProperty("TopBlockId")!.SetValue(b,top);
    }
    private static BuildPyramidBaseBehavior Pyramid(Rig rig)
    {
        var b=new BuildPyramidBaseBehavior(rig.M,"BuildPyramid",true);
        b.AttachRun(new BehaviorContext{Robot=rig.Robot,Triggers=new(),AI=new AIComponent(()=>1)},new BehaviorScope());
        Ids(b,7,88,8);return b;
    }
    [Fact]
    public void NullLookupUsesStaticAndTopButWarningAndClearUseMovingBaseId()
    {
        using var rig=new Rig();var b=Pyramid(rig);Ids(b,700,88,800);
        var logs=new List<EngineLogEntry>();rig.Robot.Engine.LogEntry+=logs.Add;
        b.TransitionToPlacingTopBlock();
        Assert.Null(b.BaseBlockId);Assert.Equal(700u,b.StaticBlockId);Assert.Equal(800u,b.TopBlockId);
        Assert.EndsWith("Object 88 is NULL",Assert.Single(logs.Where(e=>e.Name=="BehaviorBuildPyramid.TransitionToPlacingTopBlock.NullObject")).Text);
    }
    private static void Add(Rig rig,uint id)
    {
        var type=id==7?ObjectType.Block_LIGHTCUBE1:ObjectType.Block_LIGHTCUBE2;
        rig.M.World.AddLocatedObject(new ObservableObject(id,type,CubeGeometry.MarkersFor(type))
            {Pose=new Pose3d("obj"),PoseState=PoseState.Known,OriginId=rig.M.World.CurrentOriginId});
    }
    [Fact]
    public void SnapshotRetainsDuplicatesTestsTopFirstAndContinuesAfterDelegation()
    {
        using var rig=new Rig();Add(rig,7);Add(rig,8);var b=Pyramid(rig);
        var entry=new PyramidBase(7,8,Vec3.Zero);var cache=new List<PyramidBase>{new(7,99,Vec3.Zero),entry,entry};
        rig.M.Configurations.NativePyramidBaseStrongCache=()=>cache;
        using var root=new Pose3d("root");
        Pose3d? capturedMidpoint=null,capturedCenter=null;
        b.TopBaseMidpoint=(a,t,output)=>{capturedMidpoint=output;using var p=new Pose3d(Rotation3d.Identity,new Vec3(2,3,0),parent:root);output.AssignInputValue(p);cache.Clear();return true;};
        b.TopBaseCenter=_=>capturedCenter=new Pose3d("center",root);
        var contains=new List<uint>();b.TopContainsForTest=(_,id)=>contains.Add(id);
        var helpers=new List<PlaceRelObjectHelper>();b.PlacementHelperCreatedForTest=h=>
        {Assert.NotNull(capturedMidpoint!.Node);Assert.NotNull(capturedCenter!.Node);helpers.Add(h);h.PrecheckForTest=()=>ActionResult.Running;};
        b.TransitionToPlacingTopBlock();
        Assert.Equal(new uint[]{8,8,7,8,7},contains);Assert.Equal(2,helpers.Count);
        Assert.All(helpers,h=>{Assert.Equal(7u,h.Target);Assert.False(h.Low);Assert.Equal(2,h.A);Assert.Equal(3,h.B);Assert.False(h.SkipTransform);});
        Assert.Empty(cache); // actual loop used its retained local snapshot
        Assert.Null(capturedMidpoint!.Node);Assert.Null(capturedCenter!.Node); // released after helper delegation
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public void EitherGeometryFailureBreaksBeforeTheNextCandidate(bool conversionFailure)
    {
        using var rig=new Rig();Add(rig,7);Add(rig,8);var b=Pyramid(rig);
        rig.M.Configurations.NativePyramidBaseStrongCache=()=>new[]{new PyramidBase(7,8,Vec3.Zero),new PyramidBase(7,8,Vec3.Zero)};
        int midpointCalls=0,made=0;
        b.TopBaseMidpoint=(_,_,_)=>{midpointCalls++;return conversionFailure;};
        b.TopBaseCenter=_=>new Pose3d("unrelated");
        b.PlacementHelperCreatedForTest=_=>made++;
        b.TransitionToPlacingTopBlock();Assert.Equal(1,midpointCalls);Assert.Equal(0,made);
    }

    [Fact]
    public void LoopRereadsTheReferencedIdsAfterSynchronousDelegation()
    {
        using var rig=new Rig();Add(rig,7);Add(rig,8);var b=Pyramid(rig);
        var entry=new PyramidBase(7,8,Vec3.Zero);
        rig.M.Configurations.NativePyramidBaseStrongCache=()=>new[]{entry,entry};
        using var root=new Pose3d("root");
        b.TopBaseMidpoint=(_,_,output)=>{using var p=new Pose3d("point",root);output.AssignInputValue(p);return true;};
        b.TopBaseCenter=_=>new Pose3d("center",root);
        int made=0;var contains=new List<uint>();b.TopContainsForTest=(_,id)=>contains.Add(id);
        b.PlacementHelperCreatedForTest=h=>{made++;h.PrecheckForTest=()=>ActionResult.Running;Ids(b,7,88,9);};
        b.TransitionToPlacingTopBlock();
        Assert.Equal(1,made);Assert.Equal(new uint[]{8,7,9},contains); // 5DC188/198 read fields again on next iteration
    }
}
