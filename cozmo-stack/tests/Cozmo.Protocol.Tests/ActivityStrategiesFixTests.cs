using System.Text.Json;
using Cozmo.Robot.Behavior;
using Cozmo.Robot;
using Xunit;

namespace Cozmo.Protocol.Tests;

public sealed class ActivityStrategiesFixTests
{
    // reach: unit; substituted timer and RNG. M15-003 0x005B52D8..5360.
    [Fact] public void CooldownDrawOnlyAfterQualifyingWaitAndBeforeLaterGate()
    {
        int draws=0;
        var strategy=new ActivityStrategy { CooldownBaseSec=2, StartInCooldown=true,
            RequiredRecentOnTreadsEventSec=1, Draw=x=>{draws++;return 0;}, TickDuration=()=>0.1f };
        Assert.False(strategy.WantsToStart(new(),1,out _)); Assert.Equal(0,draws);
        Assert.False(strategy.WantsToStart(new(),2,out _)); Assert.Equal(1,draws);
        var noCooldown=new ActivityStrategy { CooldownBaseSec=0, Draw=x=>{draws++;return 0;} };
        Assert.True(noCooldown.WantsToStart(new(),2,out _)); Assert.Equal(1,draws);
    }

    // reach: unit; substituted unlock, filtered candidate and weak cache suppliers. M15-097 0x005B4980..4AE2.
    [Fact] public void PyramidRollUsesStrictDeadlineAndFullCacheStillConsumesRoll()
    {
        int draws=0, full=1;
        var strategy=new ActivityStrategy {Type="Pyramid", PyramidUnlocked=()=>true,
            PyramidCandidates=()=>3,PyramidBases=()=>0,PyramidFull=()=>full,
            Draw=x=>{draws++;return 0.4;} };
        Assert.False(strategy.WantsToStart(new(),0,out _)); Assert.Equal(0,draws);
        Assert.False(strategy.WantsToStart(new(),1,out _)); Assert.Equal(1,draws);
        full=0;
        Assert.False(strategy.WantsToStart(new(),301,out _)); Assert.Equal(1,draws);
        Assert.False(strategy.WantsToStart(new(),302,out _)); Assert.Equal(2,draws);
        // An unavailable candidate result still follows the timed roll and cache calls.
        int cacheCalls=0;
        var missing=new ActivityStrategy {Type="Pyramid",PyramidUnlocked=()=>true,
            PyramidCandidates=()=>null,PyramidBases=()=>{cacheCalls++;return null;},
            PyramidFull=()=>{cacheCalls++;return null;},Draw=x=>{draws++;return 0;}};
        Assert.False(missing.WantsToStart(new(),1,out _));Assert.Equal(3,draws);Assert.Equal(2,cacheCalls);
    }

    // reach: unit; real engine objective sender/hub and owner, substituted unlock/query/cache inputs.
    // M15-097 0x005B4C90..4CA2 and 0x005B4ADE..4AE2.
    [Fact] public void PyramidObjectiveLatchesWhileUnselectedAndSuccessfulStartClearsIt()
    {
        using var rig=new M4ControlTests.Rig();rig.ToSynced();rig.State();
        var strategy=new ActivityStrategy{Type="Pyramid"};strategy.BindNative(rig.Engine.Robot!);
        strategy.PyramidUnlocked=()=>true;strategy.PyramidCandidates=()=>0;
        strategy.PyramidBases=()=>1;strategy.PyramidFull=()=>0;
        var inputs=new FreeplayInputs();
        rig.Engine.ReportBehaviorObjective(4,"DroveAsIntended","test");
        Assert.False(strategy.WantsToEnd(inputs,1,out _,1));
        rig.Engine.ReportBehaviorObjective(3,"BuiltPyramid","test");
        Assert.True(strategy.WantsToEnd(inputs,1,out _,1));
        Assert.True(strategy.WantsToStart(inputs,0,out _));
        Assert.False(strategy.WantsToEnd(inputs,1,out _,1));
        strategy.Dispose();
        rig.Engine.ReportBehaviorObjective(3,"BuiltPyramid","test");
        Assert.False(strategy.WantsToEnd(inputs,1,out _,1));
    }

    // reach: unit; direct parsed config. M15-100 0x005B4E98..4EC2.
    [Fact] public void SparkOverwritesConfiguredCeilingAndDoesNotEndInternally()
    {
        using var json=JsonDocument.Parse("{\"type\":\"Spark\",\"activityShouldEndDurationSecs\":60}");
        var strategy=ActivityStrategy.FromJson(json.RootElement);
        Assert.Equal((double)float.MaxValue,strategy.ShouldEndDurationSec);
        Assert.True(strategy.WantsToStart(new(),1,out _));
        Assert.False(strategy.WantsToEnd(new(),100,out _,100));
    }

    // reach: unit; synthetic config enters the native loader with the real engine and needs owner.
    // M15-098 0x005B42DC..42F2/4504..4530, M15-001 0x00693870..C6, 0x006625CE..FA.
    [Fact] public void NeedsDeliveryIsInlineSnapshotsLevelsAndSubscriptionLivesUntilDisposed()
    {
        // The full tag 0x62 report (M15-014, 0x0069383C) needs the shipped level and needs config.
        using var rig=new M4ControlTests.Rig(PickupPyramidNativeExecutionTests.Resources()); rig.ToSynced(); rig.State();
        var robot=rig.Engine.Robot!; var needs=rig.Engine.Needs;
        string root=Directory.CreateTempSubdirectory("strategy-input-").FullName;
        try
        {
            string dir=Path.Combine(root,"config","engine","behaviorSystem");
            Directory.CreateDirectory(Path.Combine(dir,"activities"));
            File.WriteAllText(Path.Combine(dir,"activities_config.json"),"""
                [{"activityID":"Fixture","activityStrategy":{"type":"NeedBasedCooldown","needId":"Energy","cooldownBaseSecs":600,
                 "needCooldownGraph":{"nodes":[{"x":0,"y":10},{"x":1,"y":20}]},
                 "needCooldownRandomnessGraph":{"nodes":[{"x":0,"y":2},{"x":1,"y":4}]}}}]
                """);
            int constructionWords=0;
            var rng=rig.Engine.ContextRandom;
            var previousSource=rng.WordSourceForTest;
            IReadOnlyList<Activity> tree;
            try
            {
                rng.WordSourceForTest=()=>{constructionWords++;return 0;};
                tree=ActivityTreeLoader.LoadResources(root,new Dictionary<string,IBehavior>(),new RepetitionPenalty(),robot);
            }
            finally {rng.WordSourceForTest=previousSource;}
            var strategy=Assert.Single(tree).Strategy; int draws=0;
            Assert.Equal(600,strategy.CurrentCooldownSec); // 0x005B5000..5004, before any message
            Assert.Equal(0,constructionWords);
            var direct=new ActivityStrategy{CooldownBaseSec=42.125};
            Assert.Equal(42.125,direct.CurrentCooldownSec);
            direct.CooldownBaseSec=43.25;
            Assert.Equal(43.25,direct.CurrentCooldownSec); // standalone mutable setup, before its first use
            strategy.Draw=x=>{draws++;return 0;};
            IReadOnlyList<uint>? delivered=null;
            needs.NeedsLevelBitsCopied+=(words,_)=>delivered=words;
            needs.State.StoreLevel(NeedId.Repair,0.125f);
            needs.State.StoreLevel(NeedId.Energy,0.25f);
            needs.State.StoreLevel(NeedId.Play,0.75f);
            needs.SendNeedsStateToGame(NeedsActionId.NoAction);
            Assert.Null(delivered); Assert.Equal(12.5,strategy.EffectiveCooldownSec);
            Assert.Equal(2.5,strategy.CooldownRandomnessSec); Assert.Equal(0,draws);
            strategy.SetCooldown(99,99);
            // Still subscribed while never selected; an identical send overwrites the current value.
            needs.SendNeedsStateToGame(NeedsActionId.NoAction);
            Assert.Equal(12.5,strategy.EffectiveCooldownSec); Assert.Equal(0,draws);
            needs.State.StoreLevel(NeedId.Energy,0.5f);
            rig.Engine.DrainAppDelivery();
            Assert.NotNull(delivered);
            Assert.Equal(new uint[]{0x3E000000,0x3E800000,0x3F400000},delivered!.ToArray());
            strategy.Dispose();strategy.SetCooldown(99,99);
            needs.SendNeedsStateToGame(NeedsActionId.NoAction);
            Assert.Equal(99,strategy.EffectiveCooldownSec);

            robot.HasExternalInterface=false;
            var gated=Assert.Single(ActivityTreeLoader.LoadResources(root,new Dictionary<string,IBehavior>(),new RepetitionPenalty(),robot)).Strategy;
            robot.HasExternalInterface=true;
            gated.SetCooldown(88,88); needs.SendNeedsStateToGame(NeedsActionId.NoAction);
            Assert.Equal(88,gated.EffectiveCooldownSec); gated.Dispose();

            // Host-only loader supplies no native owner and therefore subscribes to no signal.
            string hostObb=Directory.CreateTempSubdirectory("strategy-host-").FullName;
            try
            {
                string hostDir=ActivityTreeLoader.ActivitiesDir(hostObb);
                Directory.CreateDirectory(Path.Combine(hostDir,"activities"));
                File.Copy(Path.Combine(dir,"activities_config.json"),Path.Combine(hostDir,"activities_config.json"));
                var host=Assert.Single(ActivityTreeLoader.Load(hostObb,new Dictionary<string,IBehavior>())).Strategy;
                host.SetCooldown(77,77);needs.SendNeedsStateToGame(NeedsActionId.NoAction);
                Assert.Equal(77,host.EffectiveCooldownSec);host.Dispose();
            }
            finally {Directory.Delete(hostObb,true);}
        }
        finally {robot.HasExternalInterface=true;Directory.Delete(root,true);}
    }

    // reach: unit; synthetic resource tree, actual robot runtime ownership and disposal.
    // M15-098 destructor 0x005B4534..4562 retires the scoped subscription.
    [Fact] public void NativeRuntimeDisposalDisconnectsItsTreeStrategies()
    {
        string root=Directory.CreateTempSubdirectory("strategy-runtime-").FullName;
        try
        {
            string dir=Path.Combine(root,"config","engine","behaviorSystem");
            Directory.CreateDirectory(Path.Combine(dir,"activities"));
            File.WriteAllText(Path.Combine(dir,"activities_config.json"),"""
                [{"activityID":"Fixture","activityStrategy":{"type":"NeedBasedCooldown","needId":"Energy",
                "cooldownBaseSecs":5,"needCooldownGraph":{"nodes":[{"x":0,"y":10},{"x":1,"y":20}]}}}]
                """);
            // The full tag 0x62 report (M15-014, 0x0069383C) needs the shipped level and needs config.
            foreach(string name in new[]{"needs_config.json","needs_level_config.json"})File.Copy(Path.Combine(PickupPyramidNativeExecutionTests.Resources(),"config","engine",name),Path.Combine(root,"config","engine",name));
            using var rig=new M4ControlTests.Rig(root);rig.ToSynced();rig.State();
            var runtime=rig.Engine.Robot!.NativeBehaviorRuntime!;
            var strategy=Assert.Single(runtime.Tree).Strategy;
            rig.Engine.Needs.State.StoreLevel(NeedId.Energy,0.25f);
            rig.Engine.Needs.SendNeedsStateToGame(NeedsActionId.NoAction);
            Assert.Equal(12.5,strategy.EffectiveCooldownSec);
            runtime.Dispose();strategy.SetCooldown(99,0);
            rig.Engine.Needs.SendNeedsStateToGame(NeedsActionId.NoAction);
            Assert.Equal(99,strategy.EffectiveCooldownSec);
        }
        finally {Directory.Delete(root,true);}
    }

    // reach: production engine Needs send -> actual runtime's shipped Singing strategy.
    // M15-098 0x005B4504..4530; singing.json graph endpoints (Play 1: 600/900).
    [Fact] public void ShippedSingingReceivesRealNeedsSendWhileUnselected()
    {
        using var fixture=new PickupPyramidNativeExecutionTests.Fixture();
        var strategy=Assert.Single(NativeBehaviorRuntime.AllActivities(fixture.Runtime.Tree)
            .Where(a=>a.Id=="Singing")).Strategy;
        Assert.Equal("NeedBasedCooldown",strategy.Type);
        var needs=fixture.Rig.Engine.Needs;
        needs.SetLevel(NeedId.Play,1);
        strategy.SetCooldown(3,4);
        needs.SendNeedsStateToGame(NeedsActionId.NoAction);
        Assert.Equal(600,strategy.EffectiveCooldownSec);
        Assert.Equal(900,strategy.CooldownRandomnessSec);
        strategy.SetCooldown(8,9);
        needs.SendNeedsStateToGame(NeedsActionId.NoAction);
        Assert.Equal(600,strategy.EffectiveCooldownSec);
        // SayName has positive Play delta even at its minimum variation (shipped needs_action_config.json).
        // At the clamped maximum the level and bracket stay fixed; 0x00696366 still broadcasts.
        var bracket=needs.State.GetNeedBracket(NeedId.Play);
        NeedsActionId? appAction=null;
        needs.NeedsStateSent+=action=>appAction=action;
        strategy.SetCooldown(8,9);
        Assert.True(needs.RegisterNeedsActionCompleted("SayName"));
        Assert.Equal(1,needs.State.GetNeedLevel(NeedId.Play));
        Assert.Equal(bracket,needs.State.GetNeedBracket(NeedId.Play));
        Assert.Equal(600,strategy.EffectiveCooldownSec);
        Assert.Equal(900,strategy.CooldownRandomnessSec);
        Assert.Null(appAction); // app delivery is queued when the synchronous recipient returns.
        fixture.Rig.Engine.DrainAppDelivery();
        Assert.Equal(NeedsActionId.SayName,appAction);
        fixture.Runtime.Dispose();strategy.SetCooldown(8,9);
        needs.SendNeedsStateToGame(NeedsActionId.NoAction);
        Assert.Equal(8,strategy.EffectiveCooldownSec);
    }

    // reach: unit; controlled state and recipient callbacks. M15-099 0x005B4718..47B0/4D88..4E08.
    [Fact] public void NeedsSecondaryClearsSelectedSevereAndTransitionNegatesPrimary()
    {
        var needs=new NeedsManager(()=>0);
        needs.SetLevel(NeedId.Repair,0); needs.SetLevel(NeedId.Energy,0);
        int clears=0;
        needs.SevereNeeds.RemoveDriving=_=>clears++;
        needs.SevereNeeds.RemoveIdle=_=>clears++;
        needs.SevereNeeds.RemoveReactions=_=>clears++;
        needs.SevereNeeds.Set(NeedId.Play);
        using var json=JsonDocument.Parse("""
            {"type":"Needs","wantsToRunStrategyConfig":{"strategyType":"InNeedsBracket","need":"Energy","needBracket":"Critical"},
             "higherPriorityStrategyConfig":{"strategyType":"InNeedsBracket","need":"Repair","needBracket":"Critical"}}
            """);
        var strategy=ActivityStrategy.FromJson(json.RootElement);
        var input=new FreeplayInputs{Needs=needs};
        Assert.True(strategy.WantsToStart(input,1,out _));
        Assert.True(strategy.WantsToEnd(input,1,out _,1)); Assert.Equal(3,clears);
        Assert.True(strategy.WantsToEnd(input,1,out _,1)); Assert.Equal(3,clears);
        var transition=new ActivityStrategy {Type="SevereNeedTransition",Need=NeedId.Energy};
        Assert.True(transition.WantsToStart(input,1,out _));
        Assert.False(transition.WantsToEnd(input,1,out _,1));
        needs.SevereNeeds.Set(NeedId.Energy);
        Assert.True(transition.WantsToEnd(input,1,out _,1));
    }

    // reach: unit; event timestamps supplied directly. M15-003 0x005B5398..53DE.
    [Fact] public void OnTreadsRequiresANewEventAndStrictWindow()
    {
        var strategy=new ActivityStrategy{RequiredRecentOnTreadsEventSec=2};strategy.OnStarted(10);
        var input=new FreeplayInputs{LastOnTreadsEventSec=10};
        Assert.False(strategy.WantsToStart(input,11,out _));
        input.LastOnTreadsEventSec=10.5;
        Assert.True(strategy.WantsToStart(input,11,out _));
        Assert.False(strategy.WantsToStart(input,12.5,out _));
    }
}
