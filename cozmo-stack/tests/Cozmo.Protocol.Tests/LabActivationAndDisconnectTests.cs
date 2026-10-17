using System.Text;
using System.Reflection;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Animation;
using Cozmo.Transport;
using Xunit;
namespace Cozmo.Protocol.Tests;
[Collection("M5 process statics")]
public sealed class LabActivationAndDisconnectTests
{
    private static byte[] B(string x)=>Encoding.UTF8.GetBytes(x);
    private static LabExperiment Experiment()=>new(0,B("report_test_auto"),1,B(""),B(""),B(""),B(""),50,Array.Empty<byte[]>(),new[]{new LabVariation(B("control"),50),new LabVariation(B("treatment"),50)});
    private sealed class LabFixture
    {
        internal readonly List<(EngineLogLevel Level,string? Channel,string Key,string Text,IReadOnlyDictionary<string,string>? Data)> Logs=new();
        internal LabExperiment E=Experiment();
        internal LabActivationRuntime Lab;
        internal LabFixture()
        {
            Lab=new(){Definition=()=>new(B("cozmo"),2,1,new[]{E}),Log=(l,c,k,t,d)=>Logs.Add((l,c,k,t,d)),Enabled=true,
                FindExperiment=_=>E,FindAssignment=(_,_,_)=>null,FindVariation=(e,k)=>e.Variations!.SingleOrDefault(v=>v.Key!.AsSpan().SequenceEqual(k)),
                StatusName=s=>B(new[]{"Invalid","ExperimentNotFound","VariantNotFound","ExperimentNotRunning","AudienceMismatch","Unassigned","Assigned","OverrideAssigned","ForceAssigned"}[s]),
                Assignments40=new(),AssignmentSignal64=new()};
        }
    }
    // reach: unit (substitutes native state/lookup/callback inputs; joins MISSING at M15-0624CA6F4 and M15-0615A6038; actual subscription/factory entry is exercised where named).
    [Theory][InlineData("1",44)][InlineData("123",98)][InlineData("test-user",66)][InlineData("cozmo",98)]
    public void NativeCrcExamplesUseKeyThenUserAndFloatBucket(string user,byte expected)
    {var f=new LabFixture();Assert.Equal(expected,f.Lab.HashBucket(B("report_test_auto"),B(user)));}
    // reach: unit (substitutes native state/lookup/callback inputs; joins MISSING at M15-0624CA6F4 and M15-0615A6038; actual subscription/factory entry is exercised where named).
    [Theory][InlineData(0,"control")][InlineData(24,"control")][InlineData(25,null)][InlineData(49,null)][InlineData(50,"treatment")][InlineData(74,"treatment")][InlineData(75,null)][InlineData(99,null)]
    public void PopulationRangesKeepTheNativeGaps(byte bucket,string? expected)
    {var f=new LabFixture();var v=f.Lab.SelectVariation(f.E,bucket);Assert.Equal(expected,v is null?null:Encoding.UTF8.GetString(v));}
    // reach: unit (substitutes native state/lookup/callback inputs; joins MISSING at M15-0624CA6F4 and M15-0615A6038; actual subscription/factory entry is exercised where named).
    [Fact] public void ActivationPrecedenceForcedThenDateThenActiveThenAudienceThenOverrideThenEnabled()
    {
        var f=new LabFixture();var calls=new List<int>();f.Lab.Enabled=false;
        f.Lab.FindAssignment=(offset,k,u)=>{calls.Add(offset);return offset==0x58?new(k,u,B("forced")):null;};
        Assert.Equal(new LabActivation(8,B("forced")).Status,f.Lab.ActivateInternal(B("k"),B("u"),Array.Empty<byte[]>(),1).Status);Assert.Equal(new[]{0x58},calls);
        calls.Clear();f.Lab.FindAssignment=(o,k,u)=>{calls.Add(o);return o==0x40?new(k,u,B("active")):null;};
        Assert.Equal((byte)6,f.Lab.ActivateInternal(B("k"),B("u"),Array.Empty<byte[]>(),1).Status);Assert.Equal(new[]{0x58,0x40},calls);
        f.E=f.E with{AudienceTags=new[]{B("absent")}};f.Lab.FindAssignment=(_,_,_)=>null;
        Assert.Equal((byte)4,f.Lab.ActivateInternal(B("k"),B("u"),Array.Empty<byte[]>(),1).Status);
        f.E=f.E with{AudienceTags=Array.Empty<byte[]>()};f.Lab.FindAssignment=(o,k,u)=>o==0x4C?new(k,u,B("override")):null;
        Assert.Equal((byte)7,f.Lab.ActivateInternal(B("k"),B("u"),Array.Empty<byte[]>(),1).Status);
        f.Lab.FindAssignment=(_,_,_)=>null;Assert.Equal((byte)5,f.Lab.ActivateInternal(B("k"),B("u"),Array.Empty<byte[]>(),1).Status);
        Assert.Contains(f.Logs,l=>l.Key=="AnkiLab.ActivateExperiment.LabIsDisabled"&&l.Text=="");
    }
    // reach: unit (substitutes native state/lookup/callback inputs; joins MISSING at M15-0624CA6F4 and M15-0615A6038; actual subscription/factory entry is exercised where named).
    [Fact] public void SuccessfulAssignmentWritesBeforeSignalAndWrapperReportsStatusEvenOnFailure()
    {
        var f=new LabFixture();bool signaled=false;
        var observer=new LabAssignmentSignalNode();observer.Next=observer;
        observer.Callback=values=>{Assert.Equal(1u,observer.RefCount20);Assert.Single(f.Lab.Assignments40!);Assert.False(Monitor.IsEntered(f.Lab.AssignmentGate));Assert.Same(f.Lab.Assignments40,values);signaled=true;};
        f.Lab.AssignmentSignal64=new(){Head=observer,Release4EB0CA=node=>node.RefCount20--};
        var result=f.Lab.Activate(B("report_test_auto"),B("test-user"),Array.Empty<byte[]>(),1);
        Assert.True(signaled);Assert.Equal((byte)6,result!.Value.Status);Assert.Equal("treatment",Encoding.UTF8.GetString(result.Value.Variation));
        var log=Assert.Single(f.Logs.Where(x=>x.Key=="experiment.assigned"));Assert.Equal(EngineLogLevel.Event,log.Level);Assert.Equal("treatment",log.Data!["$group"]);Assert.Equal("cozmo-2.1:report_test_auto:treatment:assigned",log.Data["$data"]);
        f.Lab.FindExperiment=_=>null;Assert.Equal((byte)1,f.Lab.Activate(B("missing"),B("u"),Array.Empty<byte[]>(),1)!.Value.Status);Assert.Contains(f.Logs,x=>x.Key=="experiment.invalid"&&x.Level==EngineLogLevel.Warning);
    }
    // reach: unit (substitutes native state/lookup/callback inputs; joins MISSING at M15-0624CA6F4 and M15-0615A6038; actual subscription/factory entry is exercised where named).
    [Theory][InlineData(0,EngineLogLevel.Warning,"experiment.invalid")][InlineData(2,EngineLogLevel.Warning,"experiment.invalid")][InlineData(3,EngineLogLevel.Event,"experiment.unassigned")][InlineData(5,EngineLogLevel.Event,"experiment.unassigned")][InlineData(6,EngineLogLevel.Event,"experiment.assigned")][InlineData(8,EngineLogLevel.Event,"experiment.assigned")]
    public void ReportThresholdsAndMetadataFollowUnsignedStatus(byte status,EngineLogLevel level,string key)
    {
        var f=new LabFixture();f.Lab.Report(new(B("report_test_auto"),B("u"),B("control")),status);
        var entry=f.Logs[0];Assert.Equal(key,entry.Key);Assert.Equal(level,entry.Level);Assert.Null(entry.Channel);Assert.Equal("u",entry.Data!["$user"]);Assert.Equal("control",entry.Data["$group"]);Assert.Equal(EngineLogLevel.Debug,f.Logs[1].Level);Assert.Equal("Unnamed",f.Logs[1].Channel);
    }
    // reach: unit (substitutes native state/lookup/callback inputs; joins MISSING at M15-0624CA6F4 and M15-0615A6038; actual subscription/factory entry is exercised where named).
    [Theory][InlineData(9,false)][InlineData(10,true)][InlineData(19,true)][InlineData(20,false)][InlineData(30,false)][InlineData(40,true)][InlineData(50,false)]
    public void DateWindowUsesUnsignedMinusOneAndPauseResume(uint now,bool expected)
    {
        var f=new LabFixture();var e=f.E with{Start=B("10"),Stop=B("50"),Pause=B("20"),Resume=B("40")};f.Lab.ParseSystemDate=b=>uint.Parse(Encoding.ASCII.GetString(b));Assert.Equal(expected,f.Lab.IsRunning(e,now));
    }
    // reach: unit (substitutes native state/lookup/callback inputs; joins MISSING at M15-0624CA6F4 and M15-0615A6038; actual subscription/factory entry is exercised where named).
    [Fact] public void AudienceRequiresDuplicateMultiplicityAndRecomputesOnlyAnEmptyCache()
    {
        var f=new LabFixture();var e=f.E with{AudienceTags=new[]{B("a"),B("a")}};Assert.False(LabActivationRuntime.MatchesAudience(e,new[]{B("a")}));Assert.True(LabActivationRuntime.MatchesAudience(e,new[]{B("z"),B("a"),B("a")}));
        int calls=0;var tags=new LabAudienceTags{Log=(_,_,_,_,_)=>{}};tags.Predicates.Add(B("b"),()=>{calls++;return 2;});tags.GetQualified();tags.GetQualified();Assert.Equal(2,calls);
        tags.Dynamic.Add(()=>B(""));tags.Dynamic.Add(()=>B("x"));tags.Dynamic.Add(()=>B("x"));Assert.Equal(new[]{"","x","x"},tags.GetQualified().Select(Encoding.UTF8.GetString));tags.GetQualified();Assert.Equal(3,calls);
    }
    // reach: unit (substitutes native state/lookup/callback inputs; joins MISSING at M15-0624CA6F4 and M15-0615A6038; actual subscription/factory entry is exercised where named).
    [Theory][InlineData(0,0,true)][InlineData(0,86399,true)][InlineData(0,86400,false)][InlineData(1000000,0,false)][InlineData(-1999999,0,true)]
    public void AppUserD0DividesSignedMicrosBeforeSubtract(long micros,long now,bool expected)=>Assert.Equal(expected,LabAudienceTags.AppUserD0(micros,now));
    // reach: unit (substitutes native state/lookup/callback inputs; joins MISSING at M15-0624CA6F4 and M15-0615A6038; actual subscription/factory entry is exercised where named).
    [Fact] public void DynamicLocaleRegistrationOrderAndAbAssignmentDoNotInvokeCallbacks()
    {
        var tags=new LabAudienceTags{Log=(_,_,_,_,_)=>{}};tags.RegisterEngineTags(()=>0,()=>0,()=>"English",()=>"US");Assert.Equal(new[]{"app_user_d0","locale_language_english","locale_country_us"},tags.GetQualified().Select(Encoding.UTF8.GetString));
        var ab=new LabAbInterface();var calls=new List<string>();ab.AssignmentObserved=calls.Add;Action first=()=>throw new Exception(),second=()=>throw new Exception();ab.Initialize(first,second);Assert.Equal(new[]{"105D948","105D960"},calls);Assert.Same(first,ab.First105D948);Assert.Same(second,ab.Second105D960);
        Assert.True(LabAbInterface.ShouldDisable(1,false));Assert.False(LabAbInterface.ShouldDisable(0,false));Assert.False(LabAbInterface.ShouldDisable(1,true));
    }
    // reach: unit (uses actual body-ID subscription; substitutes Lab definition/lookup/log inputs; activation join MISSING at M15-0624CA6F4).
    [Fact] public void BodyIdMessageAlwaysPassesUnsignedDecimalUserThroughActualSubscription()
    {
        using var robot=CozmoRobot.CreateForTest(new M4ControlTests.FakePort(),()=>1000000000);var native=robot.Engine.Robots.AddRobot(1,false);var lab=robot.Engine.Experiments;lab.Audience.Cache.Add(B("cached"));
        byte[]? user=null;lab.Lab.FindExperiment=_=>null;lab.Lab.StatusName=_=>B("ExperimentNotFound");lab.Lab.Definition=()=>new(B("p"),1,2,new[]{Experiment()});
        lab.Lab.FindExperiment=k=>null;lab.Lab.ClockMicroseconds=()=>1000000;
        // Observe requested user at the wrapper report, after the actual native handler has called automatic activation.
        lab.Lab.Log=(level,channel,key,text,data)=>{if(key=="experiment.invalid")user=B(data!["$user"]);};
        native.DeliverMessage(new ManufacturingID{SerialNumber=uint.MaxValue,BodyHwVersion=0,BodyColor=0});Assert.Equal("4294967295",Encoding.ASCII.GetString(user!));
    }
    // reach: unit (substitutes native state/lookup/callback inputs; joins MISSING at M15-0624CA6F4 and M15-0615A6038; actual subscription/factory entry is exercised where named).
    [Fact] public void SignalRetainsCurrentThenReloadsNextAfterCallbackAndReleasesBeforeHeadTest()
    {
        var signal=new LabAssignmentSignal();var a=new LabAssignmentSignalNode();var b=new LabAssignmentSignalNode();a.Next=b;b.Next=a;signal.Head=a;var trace=new List<string>();
        a.Callback=_=>{Assert.Equal(1u,a.RefCount20);trace.Add("a");};b.Callback=_=>trace.Add("b");signal.Release4EB0CA=n=>{trace.Add(ReferenceEquals(n,a)?"release-a":"release-b");n.RefCount20--;};
        signal.Dispatch(Array.Empty<LabAssignmentDef>());Assert.Equal(new[]{"a","release-a","b","release-b","release-a"},trace);Assert.Equal(0u,a.RefCount20);Assert.Equal(0u,b.RefCount20);
    }
    private static FeedingCubeControllerUnit Controller(int id,List<string> trace)=>new(){Id=id,InnerId=id,Log=(_,_,k,_)=>trace.Add(k),Inner=new(),State4=0,StateName=s=>s==2?"Deactivated":"Activated",SetCubeLights5A65AC=()=>trace.Add("lights"),RemoveListener6356AC=(i,l)=>trace.Add("remove"),DeleteInner=_=>trace.Add("inner"),DeleteOuter=()=>trace.Add("outer")};
    // reach: unit (substitutes native state/lookup/callback inputs; joins MISSING at M15-0624CA6F4 and M15-0615A6038; actual subscription/factory entry is exercised where named).
    [Fact] public void FeedingDisconnectClearsControllerThenDeletesThenResetsSelectedEvenIfAbsent()
    {
        using var r=CozmoRobot.CreateForTest(new M4ControlTests.FakePort(),()=>1);var trace=new List<string>();var f=new FeedingDisconnectUnit{Engine=r.Engine,Selected94=7};f.Controllers78.Add(7,Controller(7,trace));
        Assert.True(f.Handle(new(7,0,ObjectType.Charger_Basic,0)));Assert.Equal(new[]{"lights","remove","FeedingCubeController.SetControllerState.NewState","inner","outer"},trace);Assert.Empty(f.Controllers78);Assert.Equal(-1,f.Selected94);
        f.Selected94=9;Assert.True(f.Handle(new(9,0,ObjectType.UnknownObject,0)));Assert.Equal(-1,f.Selected94);
    }
    // reach: unit (substitutes native state/lookup/callback inputs; joins MISSING at M15-0624CA6F4 and M15-0615A6038; actual subscription/factory entry is exercised where named).
    [Fact] public void FeedingDuplicateConnectConstructsThenDeletesNewWithoutReplacingOld()
    {
        using var r=CozmoRobot.CreateForTest(new M4ControlTests.FakePort(),()=>1);var trace=new List<string>();var old=Controller(-1,trace);var f=new FeedingDisconnectUnit{Engine=r.Engine,Stage6C=0,Selected94=4,ConstructController=id=>{trace.Add("construct");return Controller(id,trace);}};f.Controllers78.Add(-1,old);
        Assert.True(f.Handle(new(uint.MaxValue,0,ObjectType.UnknownObject,1)));Assert.Same(old,f.Controllers78[-1]);Assert.Equal(new[]{"construct","inner","outer"},trace);Assert.Equal(4,f.Selected94);
    }
    // reach: unit (substitutes native state/lookup/callback inputs; joins MISSING at M15-0624CA6F4 and M15-0615A6038; actual subscription/factory entry is exercised where named).
    [Fact] public void FeedingState2CallsRemoveForEmptyPairAndSameStateWarnsWithoutClearing()
    {var trace=new List<string>();var c=Controller(1,trace);Assert.True(c.SetState(2,-1));Assert.Contains("remove",trace);trace.Clear();Assert.True(c.SetState(2,-1));Assert.Equal(new[]{"FeedingCubeController.SetControllerState.StateAlreadySet"},trace);}
    // reach: unit (substitutes native state/lookup/callback inputs; joins MISSING at M15-0624CA6F4 and M15-0615A6038; actual subscription/factory entry is exercised where named).
    [Fact] public void FeedingSelectedSubscriptionIsReleasedAfterState2AndTreeDestruction()
    {
        using var r=CozmoRobot.CreateForTest(new M4ControlTests.FakePort(),()=>1);var trace=new List<string>();var f=new FeedingDisconnectUnit{Engine=r.Engine,Stage6C=0,Selected94=77};f.Controllers78.Add(1,Controller(1,trace));
        f.DestroyControllerTree=map=>{trace.Add("tree");return true;};f.OnSelected();Assert.Single(f.HandlesAC);r.Engine.AnimationMessages.Deliver(13,true,new ObjectConnectionStateMessage(77,0,ObjectType.UnknownObject,0));Assert.Equal(-1,f.Selected94);
        Assert.True(f.OnDeselected());Assert.Empty(f.HandlesAC);Assert.Equal("tree",trace[^1]);f.Selected94=77;r.Engine.AnimationMessages.Deliver(13,true,new ObjectConnectionStateMessage(77,0,ObjectType.UnknownObject,0));Assert.Equal(77,f.Selected94);
    }
    // reach: unit (substitutes native GuardDog running/prehook/action or factory registration inputs; lifecycle join MISSING at M15-0675BF268/5F28DC).
    [Fact] public void GuardDogIgnoresInactiveAndConnectedButAnyRunningDisconnectStopsBeforeState8AndLabel()
    {
        byte running=0;var trace=new List<string>();var g=new GuardDogDisconnectUnit{Id="GuardDog",Log=(_,_,key,_)=>trace.Add(key),PreHook5C=(_,_)=>trace.Add("pre"),RunningA1=()=>running,StateLabel58="Watching"};
        g.StopActing=(a,b)=>{Assert.True(a);Assert.False(b);Assert.Null(g.State11C);trace.Add("stop");};
        g.HandleEvent(13,new ObjectConnectionStateMessage(999,0,ObjectType.Charger_Basic,0));Assert.Equal(new[]{"pre"},trace);running=1;trace.Clear();g.HandleEvent(13,new ObjectConnectionStateMessage(999,0,ObjectType.Charger_Basic,1));Assert.Equal(new[]{"pre"},trace);
        trace.Clear();g.HandleEvent(13,new ObjectConnectionStateMessage(999,0,ObjectType.Charger_Basic,0));Assert.Equal(new[]{"pre","BehaviorGuardDog.HandleObjectConnectionState","stop","Behavior.TransitionToState"},trace);Assert.Equal(8,g.State11C);Assert.Equal("BlockDisconnected",g.StateLabel58);
    }
    // reach: unit (real E2G subscription; substitutes history topology. Producer MISSING at635D6E; discarded walk itself needs no missing side effect).
    [Fact] public void CubeAccelDisconnectPreservesHistoryAndSubscriptionRetiresBeforeHistoryClear()
    {
        using var r=CozmoRobot.CreateForTest(new M4ControlTests.FakePort(),()=>1);r.Engine.Robots.AddRobot(1,false);int calls=0;r.CubeAccel.NativeHistoryRoot=()=>{calls++;return new NativeCubeAccelHistoryNode{Id14=-1,Left=new(){Id14=-2}};};var visited=new List<int>();r.CubeAccel.HistoryNodeVisited=visited.Add;
        r.Engine.AnimationMessages.Deliver(13,true,new ObjectConnectionStateMessage(uint.MaxValue,0,ObjectType.UnknownObject,0));Assert.Equal(0,calls);
        r.Engine.AnimationMessages.Deliver(13,true,new ObjectConnectionStateMessage(uint.MaxValue,0,ObjectType.UnknownObject,1));Assert.Equal(1,calls);Assert.Equal(new[]{-1,-2},visited);r.CubeAccel.ResetToConstructed();Assert.Same(r.CubeAccel,CubeAccelStreams.LastConstructed105B450);
        r.Engine.AnimationMessages.Deliver(13,true,new ObjectConnectionStateMessage(uint.MaxValue,0,ObjectType.UnknownObject,1));Assert.Equal(1,calls);
    }
    // reach: unit (substitutes native state/lookup/callback inputs; joins MISSING at M15-0624CA6F4 and M15-0615A6038; actual subscription/factory entry is exercised where named).
    [Fact] public void EmptyHashAssertionsContinueAfterBothFlushRecipients()
    {
        bool old=EngineErrorState.ErrorFlagSet;
        try{var f=new LabFixture();int flushes=0;f.Lab.AssertCallstackAndFlush=()=>flushes++;Assert.Equal((byte)99,f.Lab.HashBucket(B(""),B("")));Assert.Equal(2,flushes);Assert.Equal(2,f.Logs.Count(x=>x.Level==EngineLogLevel.Error));Assert.True(EngineErrorState.ErrorFlagSet);}
        finally{EngineErrorState.ErrorFlagSet=old;}
    }
    // reach: unit (substitutes native state/lookup/callback inputs; joins MISSING at M15-0624CA6F4 and M15-0615A6038; actual subscription/factory entry is exercised where named).
    [Fact] public void InitEnablesBeforeDeviceBeforeTagsBeforeAutoBeforeVerifyBeforeAbAssignment()
    {
        using var resources=new BackupTestResources();using var engine=new CozmoEngine(new M4ControlTests.FakePort(),resources.Options(new CozmoEngineOptions{DasDeviceId="u",LabLocaleLanguage="English",LabLocaleCountry="US"}),()=>1);
        var lab=engine.Experiments;var trace=new List<string>();lab.InitBeforeNeedsObserved=()=>trace.Add("init-before-needs");engine.LogEntry+=entry=>{if(entry.Name=="AnkiLab.Enable")trace.Add("enable");};lab.NeedsState8Input=()=>{Assert.Equal("u",lab.DeviceIdInput);trace.Add("tags");return 0;};
        lab.Lab.FindExperiment=_=>{trace.Add("activate");return null;};lab.Lab.StatusName=_=>B("ExperimentNotFound");lab.Lab.Definition=()=>new(B("p"),1,1,new[]{Experiment()});lab.KnownAudienceTagsInput=()=>{trace.Add("verify");return Array.Empty<byte[]>();};Action callback=()=>throw new Exception();lab.AbCallbacksInput=()=>{trace.Add("ab");return(callback,callback);};
        lab.InitBeforeNeeds(()=>trace.Add("needs"));Assert.Equal(new[]{"init-before-needs","enable","tags","activate","verify","ab","needs"},trace);
    }
    // reach: unit (substitutes native state/lookup/callback inputs; joins MISSING at M15-0624CA6F4 and M15-0615A6038; actual subscription/factory entry is exercised where named).
    [Fact] public void AudienceVerifyLogsEveryUnknownOccurrenceAndKeepsGoingWithoutInvokingPredicates()
    {
        bool old=EngineErrorState.ErrorFlagSet;
        try{var logs=new List<string>();var tags=new LabAudienceTags{Log=(l,c,k,t,d)=>{Assert.Equal(EngineLogLevel.Error,l);Assert.Null(c);logs.Add(t);}};tags.Predicates.Add(B("known"),()=>throw new Exception());Assert.False(tags.Verify(new[]{B("x"),B("known"),B("x")}));Assert.Equal(new[]{"got unknown tag: x","got unknown tag: x"},logs);}
        finally{EngineErrorState.ErrorFlagSet=old;}
    }
    // reach: unit (substitutes native GuardDog running/prehook/action or factory registration inputs; lifecycle join MISSING at M15-0675BF268/5F28DC).
    [Fact] public void GuardDogFactoryOwnsTheCheckedUnitWithoutClaimingTheUnreadBehaviorClass()
    {
        using var json=System.Text.Json.JsonDocument.Parse("{\"behaviorClass\":\"GuardDog\",\"behaviorID\":\"GuardDog\"}");var units=new List<GuardDogDisconnectUnit>();var context=new BehaviorFactoryContext{RegisterGuardDogDisconnect=units.Add};var factory=new BehaviorFactory();var container=new BehaviorContainer(factory,context,Array.Empty<KeyValuePair<BehaviorID,System.Text.Json.JsonElement>>());
        Assert.Null(factory.CreateBehavior(container,BehaviorClass.GuardDog,context,json.RootElement,out var status));Assert.Equal(BehaviorCreateStatus.NotImplemented,status);Assert.Equal("GuardDog",Assert.Single(units).Id);Assert.Null(units[0].RunningA1);
    }

    // reach: unit (substitutes native state/lookup/callback inputs; joins MISSING at M15-0624CA6F4 and M15-0615A6038; actual subscription/factory entry is exercised where named).
    [Fact] public void State2ReloadAfterClearSuppressesTransitionLogAndRetainsReleaseOrder()
    {
        var trace=new List<string>();var controller=Controller(7,trace);object control=new();controller.ListenerControl=control;controller.Listener=new();controller.RetainListener=c=>{Assert.Same(control,c);trace.Add("retain");};controller.ReleaseListener=c=>{Assert.Same(control,c);trace.Add("release");};
        controller.SetCubeLights5A65AC=()=>{trace.Add("lights");controller.State4=2;};Assert.True(controller.SetState(2,-1));Assert.Equal(new[]{"lights","retain","remove","release","release"},trace);Assert.Null(controller.ListenerControl);Assert.Null(controller.Listener);
    }

}
