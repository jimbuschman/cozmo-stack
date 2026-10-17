using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Animation;
using System.Reflection;
using Xunit;
namespace Cozmo.Protocol.Tests;
[Collection("M5 process statics")]
public sealed class AiTeardownCheckedUnitsTests
{
    private static NativeSharedVectorFinalizer Vector(List<string> trace,string label,params object?[] items)
    {
        var result=new NativeSharedVectorFinalizer{Controls=items,End=items.Length,Allocated=true,FreeAllocation=()=>{trace.Add(label+"-free");return true;}};
        result.ReleaseShared=o=>{trace.Add(label+o+"-end"+result.End);return true;};return result;
    }
    private static NativeVectorFinalizer<object?> Scorers(List<string> trace,string label,params object?[] items)
    {
        var result=new NativeVectorFinalizer<object?>{Elements=items,End=items.Length,Allocated=true,FreeAllocation=()=>{trace.Add(label+"-free");return true;}};
        result.DestroyElement=o=>{trace.Add(label+o+"-end"+result.End);return true;};return result;
    }
    private static NativeFunctionFinalizer Function(List<string> trace,string label,bool inline)=>new(){Inline=inline,Destroy10=()=>{trace.Add(label+"-10");return true;},DestroyDelete14=()=>{trace.Add(label+"-14");return true;}};
    // reach: unit (substitutes native storage/finalizer recipients; joins MISSING at M15-063569D38/56A0AA and M15-0555AD9DE).
    [Fact] public void HelperDestroys28Then10ThenSharedHelpersBackwardThenNullsFactoryBeforeFree()
    {
        var trace=new List<string>();var h=new NativeHelperFinalizer{Completion28=Function(trace,"28",true),Completion10=Function(trace,"10",false),SharedHelpers4=Vector(trace,"helper",1,2),Factory0=new()};
        h.DeleteFactory=_=>{Assert.Null(h.Factory0);trace.Add("factory");return true;};h.DeleteComponent=()=>{trace.Add("owner");return true;};Assert.True(h.Destroy());
        Assert.Equal(new[]{"28-10","10-14","helper2-end1","helper1-end0","helper-free","factory","owner"},trace);
    }
    // reach: unit (substitutes native storage/finalizer recipients; joins MISSING at M15-063569D38/56A0AA and M15-0555AD9DE).
    [Fact] public void ProcessTreeDestroysLeftRightThenCallableAndTwoSetsBeforeNode()
    {
        var trace=new List<string>();NativeProcessFinalizerNode N(string name)=>new(){Callable30=Function(trace,name,true),DestroySet24=()=>{trace.Add(name+"-24");return true;},DestroySet18=()=>{trace.Add(name+"-18");return true;},DeleteNode=()=>{trace.Add(name+"-delete");return true;}};
        var root=N("root");root.Left=N("left");root.Right=N("right");Assert.True(NativeProcessFinalizerNode.Destroy(root));Assert.Equal(new[]{"left-10","left-24","left-18","left-delete","right-10","right-24","right-18","right-delete","root-10","root-24","root-18","root-delete"},trace);
    }
    // reach: unit (substitutes native storage/finalizer recipients; joins MISSING at M15-063569D38/56A0AA and M15-0555AD9DE).
    [Fact] public void ObjectEntryNullsFilterAfterCallableAndDestroysFilterSetsInNativeOffsetOrder()
    {
        var trace=new List<string>();var e=new NativeObjectCacheEntryFinalizer{Callable28=Function(trace,"callable",false),Filter24=new(),LongString4=true};
        e.ClearFilterPredicates60=_=>{Assert.Null(e.Filter24);trace.Add("predicates");return true;};e.DestroyFilterSet=(_,o)=>{trace.Add(o.ToString("X"));return true;};e.DeleteFilter=_=>{trace.Add("filter-free");return true;};e.DestroyObjectIdSet18=()=>{trace.Add("entry-set");return true;};e.DeleteStringC=()=>{trace.Add("string-free");return true;};
        Assert.True(e.Destroy());Assert.Equal(new[]{"callable-14","predicates","54","48","3C","30","24","18","C","0","filter-free","entry-set","string-free"},trace);
    }
    // reach: unit (substitutes native storage/finalizer recipients; joins MISSING at M15-063569D38/56A0AA and M15-0555AD9DE).
    [Fact] public void WorkoutReleasesNameBefore24And18ScorerVectorsAndKeepsReverseOrder()
    {
        var trace=new List<string>();var item=new NativeWorkoutFinalizerEntry{LongName=true,FreeName=()=>{trace.Add("name");return true;},Scorers24=Scorers(trace,"24",1,2),Scorers18=Scorers(trace,"18",3)};
        var vector=new NativeVectorFinalizer<NativeWorkoutFinalizerEntry>{Elements=new[]{item},End=1,Allocated=true,FreeAllocation=()=>{trace.Add("workout-free");return true;}};
        var owner=new NativeAiTeardown{Workout=vector,DeleteWorkout=()=>{trace.Add("owner-free");return true;}};Assert.True(owner.Destroy(0x1C));Assert.Equal(new[]{"name","242-end1","241-end0","24-free","183-end0","18-free","workout-free","owner-free"},trace);
    }
    // reach: unit (substitutes native storage/finalizer recipients; joins MISSING at M15-063569D38/56A0AA and M15-0555AD9DE).
    [Fact] public void WhiteboardOrdersPayloadRecipientsThenFourTreesThenBackwardHandles()
    {
        var trace=new List<string>();var owner=new NativeAiTeardown();owner.WhiteboardBeacons=()=>{Assert.True(owner.WhiteboardVtableReset);trace.Add("beacons");return true;};owner.WhiteboardPossible=()=>{trace.Add("possible");return true;};owner.WhiteboardFailureTree=o=>{trace.Add(o.ToString("X"));return true;};owner.WhiteboardHandles=Vector(trace,"handle",1,2);owner.DeleteWhiteboard=()=>{trace.Add("delete");return true;};
        Assert.True(owner.Destroy(0x18));Assert.Equal(new[]{"beacons","possible","38","2C","20","14","handle2-end1","handle1-end0","handle-free","delete"},trace);
    }
    // reach: unit (substitutes native storage/finalizer recipients; joins MISSING at M15-063569D38/56A0AA and M15-0555AD9DE).
    [Fact] public void RequestGameDestroys18TreeThenCTreeThenHandlesAndOwner()
    {
        var trace=new List<string>();NativePlainTreeFinalizerNode N(string n)=>new(){DeleteNode=()=>{trace.Add(n);return true;}};
        var first=N("18root");first.Left=N("18left");first.Right=N("18right");var owner=new NativeAiTeardown{RequestTreesKnown=true,RequestTree18=first,RequestTreeC=N("C"),RequestGameVector=Vector(trace,"handle",1),DeleteRequestGame=()=>{trace.Add("owner");return true;}};
        Assert.True(owner.Destroy(0x20));Assert.Equal(new[]{"18left","18right","18root","C","handle1-end0","handle-free","owner"},trace);
    }
    // reach: unit (substitutes native storage/finalizer recipients; joins MISSING at M15-063569D38/56A0AA and M15-0555AD9DE).
    [Fact] public void ActualAiChildIsNullBeforeCheckedHelperUnitRunsAndUnreadCallableStopsItsTail()
    {
        var ai=new AIComponent(()=>1);bool called=false;ai.NativeTeardown.Helper=new NativeHelperFinalizer{Completion28=new(){Inline=true,Destroy10=()=>{Assert.True(ai.IsChildRetired(0x10));called=true;return false;}},Completion10=new(){Empty=true},SharedHelpers4=Vector(new(),"unused"),Factory0=new(),DeleteFactory=_=>throw new Exception("must not free after stop")};
        ai.DestroyDirect();Assert.True(called);
    }
    // reach: unit (substitutes current activity and native delegate; production Update join MISSING at M15-0555AD9DE).
    [Fact] public void NativeFreeplayForwardingReloadsCurrentAndPreservesRobotAndReturnValue()
    {
        using var r=CozmoRobot.CreateForTest(new M4ControlTests.FakePort(),()=>1);var robot=r.Engine.Robots.AddRobot(1,false);var context=new BehaviorContext{Robot=r,Triggers=new AnimationTriggerMap()};var root=new Activity{Id="Freeplay",Strategy=new()};var system=new FreeplaySystem(new BehaviorManager(context),context,root,new Dictionary<string,IBehavior>(),new FreeplayInputs());Assert.Equal(0u,system.UpdateNativeFreeplay(robot));
        var first=new Activity{Id="first",Strategy=new()};var second=new Activity{Id="second",Strategy=new()};
        first.NativeUpdate20=input=>{Assert.Same(robot,input);typeof(FreeplaySystem).GetProperty("Current")!.SetValue(system,second);return 23;};second.NativeUpdate20=input=>{Assert.Same(robot,input);return 91;};
        typeof(FreeplaySystem).GetProperty("Current")!.SetValue(system,first);Assert.Equal(23u,system.UpdateNativeFreeplay(robot));Assert.Equal(91u,system.UpdateNativeFreeplay(robot));
    }
    // reach: unit (substitutes native storage/finalizer recipients; joins MISSING at M15-063569D38/56A0AA and M15-0555AD9DE).
    [Fact] public void SharedVectorSkipsNullControlsButStillMovesEndBeforeEveryRelease()
    {var trace=new List<string>();var vector=Vector(trace,"control",1,null,2);Assert.True(vector.Destroy("569F86"));Assert.Equal(new[]{"control2-end2","control1-end0","control-free"},trace);}

    // reach: unit (substitutes native region/control storage; concrete pointee release join MISSING at M15-063569FA0).
    [Fact] public void AnalyzerOwnsTheCheckedOuterToInnerVectorJoinWithoutAnElementAdapter()
    {
        var trace=new List<string>();var inner=Vector(trace,"segment",1,2);
        var regions=new NativeVectorFinalizer<NativeSharedVectorFinalizer>{Elements=new[]{inner},End=1,Allocated=true,FreeAllocation=()=>{trace.Add("regions-free");return true;}};
        var owner=new NativeAiTeardown{BorderRegions=regions,ProcessRootKnown=true,DeleteAnalyzer=()=>{trace.Add("analyzer-free");return true;}};
        Assert.Null(regions.DestroyElement);Assert.True(owner.Destroy(8));Assert.Equal(new[]{"segment2-end1","segment1-end0","segment-free","regions-free","analyzer-free"},trace);
    }

}

