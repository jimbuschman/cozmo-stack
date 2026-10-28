using System.Reflection;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Vision;
using Xunit;
using Fixture=Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;
namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class BehaviorStateLightsNativeTests
{
    private static BehaviorStateLightInfo Entry(uint id,string trigger="InteractingBehaviorLock",bool modifier=false,CubeLightModifier? lights=null)=>new(id,NativeCubeLightTriggers.FromName(trigger),modifier,lights??BehaviorStateLightInfo.Zero());
    [Fact]
    public void OrderedDuplicatesFirstMatchFailedPlayRetentionAndEntryFlagClassOrder()
    {
        // Real manager and lower light recipient with invalid positive ObjectIDs: every failed call still participates in native diff/retention.
        using var f=new Fixture();var manager=f.Robot.BehaviorManagerOwner!;var cube=f.Rig.Robot.Lights.Cubes;
        var calls=new List<string>();bool checking=false;
        cube.BehaviorStateCallObservedForTest=(id,call,trigger,layer,modifier,lights,offset)=>
        {Assert.Equal(1,layer);Assert.Equal(0,offset);calls.Add($"{call}:{id}:{trigger}");if(checking){var current=manager.InspectStateLights();Assert.True(current.Flag);Assert.Equal((byte)BehaviorClass.RollBlock,current.Class);}};
        manager.SetBehaviorStateLights(BehaviorClass.RollBlock,new[]{Entry(77,"Flash"),Entry(77,"DrivingTo"),Entry(78,"Interacting")},false);
        Assert.Equal(new[]{"Play:77:Flash","Play:77:DrivingTo","Play:78:Interacting"},calls);Assert.Equal(3,manager.InspectStateLights().Entries.Count);
        calls.Clear();checking=true;manager.SetBehaviorStateLights(BehaviorClass.PutDownBlock,new[]{Entry(77,"InteractingBehaviorLock"),Entry(77,"Flash"),Entry(79,"Connected")},true);
        Assert.Equal(new[]{"Stop:77:Flash","Play:77:InteractingBehaviorLock","Stop:77:DrivingTo","Play:77:InteractingBehaviorLock","StopResume:78:Interacting","Play:79:Connected"},calls);
        var retained=manager.InspectStateLights();Assert.Equal((byte)BehaviorClass.PutDownBlock,retained.Class);Assert.True(retained.Flag);Assert.Equal(new uint[]{77,77,79},retained.Entries.Select(e=>e.ObjectId));
        checking=false;calls.Clear();manager.SetBehaviorStateLights(BehaviorClass.RollBlock,Array.Empty<BehaviorStateLightInfo>(),false);
        Assert.Equal(new[]{"StopResume:77:InteractingBehaviorLock","StopResume:77:Flash","StopResume:79:Connected"},calls);Assert.Empty(manager.InspectStateLights().Entries);
    }
    [Fact]
    public void ModifierSemanticValuesAreCopiedAndFalseUsesZeroAtLowerRecipient()
    {
        using var f=new Fixture();var manager=f.Robot.BehaviorManagerOwner!;var source=new[]{new LedPattern(new(1,2,3,4),new(5,6,7,8),10,20,30,40,50),default,default,default};
        var modifier=new CubeLightModifier(source,123,2,new(4,5));var received=new List<CubeLightModifier>();
        f.Rig.Robot.Lights.Cubes.BehaviorStateCallObservedForTest=(_,call,_,_,_,lights,_)=>{if(call=="Play")received.Add(lights);};
        manager.SetBehaviorStateLights(BehaviorClass.RollBlock,new[]{Entry(77,"Flash",true,modifier),Entry(78,"Flash",false,modifier)},false);
        source[0]=default;var retained=manager.InspectStateLights();Assert.Equal(10u,retained.Entries[0].Lights.Leds[0].OnMs);Assert.Equal(123u,retained.Entries[0].Lights.RotationPeriodMs);Assert.Equal(new System.Numerics.Vector2(4,5),retained.Entries[0].Lights.RelativePoint);
        Assert.All(received[1].Leds,led=>Assert.Equal(default,led));Assert.Equal(0u,received[1].RotationPeriodMs);Assert.Equal((byte)0,received[1].RelativeMode);
    }
    private sealed class StateBehavior(string id,string cls,Action? init=null):SteppedBehavior(id,cls)
    {
        public override bool IsRunnable(BehaviorContext context)=>true;
        protected override bool KeepsRunningWithoutAction=>true;
        protected override void OnStart()=>init?.Invoke();
    }
    // Explicit RunningAndResumeInfo recipient unit: native identity/raw-byte inputs, no action/helper state overrides.
    private static void Store(BehaviorManager manager,IBehavior? next,IBehavior? resume,ReactionTrigger? trigger)
    {
        var type=typeof(BehaviorManager).GetNestedType("RunningInfo",BindingFlags.NonPublic)!;
        var info=Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,new object?[]{next,resume,trigger},null);
        typeof(BehaviorManager).GetMethod("SetRunningAndResumeInfo",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(manager,new[]{info,new BehaviorScope(),(object)1d});
    }
    [Theory]
    [InlineData(false,false,false,false)] [InlineData(false,true,false,false)]
    [InlineData(true,false,false,true)] [InlineData(true,false,true,false)]
    public void Ms4UsesRawNoneTransitionSavedFlagAndResumeIdentity(bool rawTransition,bool flag,bool sameClass,bool clear)
    {
        using var f=new Fixture();var manager=f.Robot.BehaviorManagerOwner!;var parked=new StateBehavior("park",sameClass?"RollBlock":"Wait");
        Store(manager,null,parked,null);manager.SetBehaviorStateLights(BehaviorClass.RollBlock,new[]{Entry(77)},flag);
        var calls=new List<string>();f.Rig.Robot.Lights.Cubes.BehaviorStateCallObservedForTest=(_,call,_,_,_,_,_)=>calls.Add(call);
        Store(manager,parked,null,rawTransition?ReactionTrigger.CliffDetected:null);
        Assert.Equal(clear?new[]{"StopResume"}:Array.Empty<string>(),calls);Assert.Equal(clear?0:1,manager.InspectStateLights().Entries.Count);
    }
    [Fact]
    public void NullAndSameClassKeepLightsButFreshDifferentClassClearsAfterInit()
    {
        using var scene=new RollFirmwareScene();var f=scene.F;var manager=f.Robot.BehaviorManagerOwner!;var cube=f.Rig.Robot.Lights.Cubes;var calls=new List<string>();
        cube.BehaviorStateCallObservedForTest=(_,call,trigger,_,_,_,_)=>calls.Add(call+":"+trigger);
        manager.SetBehaviorStateLights(BehaviorClass.RollBlock,new[]{Entry(scene.Target)},false);calls.Clear();
        Assert.True(manager.SwitchToBehaviorBase(null!,0).GetAwaiter().GetResult());Assert.Empty(calls);Assert.Single(manager.InspectStateLights().Entries);
        var same=new StateBehavior("same","RollBlock");Assert.True(manager.SwitchToBehaviorBase(same,0).GetAwaiter().GetResult());Assert.Empty(calls);
        bool init=false;var other=new StateBehavior("other","Wait",()=>{Assert.Empty(calls);init=true;});Assert.True(manager.SwitchToBehaviorBase(other,0).GetAwaiter().GetResult());Assert.True(init);Assert.Equal(new[]{"StopResume:InteractingBehaviorLock"},calls);Assert.Empty(manager.InspectStateLights().Entries);
        calls.Clear();var own=new StateBehavior("init-lights","PutDownBlock",()=>manager.SetBehaviorStateLights(BehaviorClass.PutDownBlock,new[]{Entry(scene.Target,"DrivingTo")},false));
        Assert.True(manager.SwitchToBehaviorBase(own,0).GetAwaiter().GetResult());Assert.Equal(new[]{"Play:DrivingTo"},calls);Assert.Single(manager.InspectStateLights().Entries);
    }
    [Fact]
    public void ActualConfiguredRollPlaysTrigger25AndSetterUnitRemovesOldBeforeNewPlay()
    {
        using var scene=new RollFirmwareScene();var f=scene.F;var manager=f.Robot.BehaviorManagerOwner!;var calls=new List<string>();
        f.Rig.Robot.Lights.Cubes.BehaviorStateCallObservedForTest=(id,call,trigger,layer,modifier,_,offset)=>{Assert.Equal(1,layer);Assert.False(modifier);Assert.Equal(0,offset);calls.Add($"{call}:{id}:{trigger}");};
        int mark=f.Rig.Mark();Assert.True(manager.StartAsync("Hiking_RollCube",0).GetAwaiter().GetResult());Assert.Equal(new[]{"Play:"+scene.Target+":InteractingBehaviorLock"},calls);
        Assert.Contains(f.Rig.SentSince(mark),message=>message is CubeID);Assert.Contains(f.Rig.SentSince(mark),message=>message is CubeLights);
        // Setter-unit target replacement below; the preceding native manager admission proves actual Roll caller traffic.
        manager.SetBehaviorStateLights(BehaviorClass.RollBlock,new[]{Entry(99)},false);Assert.Equal("StopResume:"+scene.Target+":InteractingBehaviorLock",calls[^2]);Assert.Equal("Play:99:InteractingBehaviorLock",calls[^1]);
    }

    [Fact]
    public void LiteralNativeTriggerIdentitiesDoNotAliasInteractingLockCountOrUnknown()
    {
        Assert.Equal("Interacting",NativeCubeLightTriggers.Name(24));Assert.Equal("InteractingBehaviorLock",NativeCubeLightTriggers.Name(25));Assert.Equal("Count",NativeCubeLightTriggers.Name(40));Assert.Null(NativeCubeLightTriggers.Name(41));Assert.Null(NativeCubeLightTriggers.Name(uint.MaxValue));
        using var f=new Fixture();var manager=f.Robot.BehaviorManagerOwner!;manager.SetBehaviorStateLights(BehaviorClass.RollBlock,new[]{new BehaviorStateLightInfo(77,41,false,BehaviorStateLightInfo.Zero())},false);
        Assert.Equal(41u,Assert.Single(manager.InspectStateLights().Entries).Trigger); // raw identity retention; no broader unknown-input play claim.
    }
    [Fact]
    public void MissingPositiveIdStopStillUpdatesAnExistingDifferentCubeAnimation()
    {
        // Native lower-stop recipient unit with explicit elapsed timer input, no engine tick between clock and Stop.
        // M4-018 WakeUp source patterns1280ms then5100ms; positive absentID must still call Update(false).
        using var scene=new RollFirmwareScene();var f=scene.F;var cube=f.Rig.Robot.Lights.Cubes;
        Assert.True(cube.PlayLightAnim(ObjectType.Block_LIGHTCUBE1,"WakeUp",CubeLightComponent.EngineLayer));
        var first=cube.PlayingForTest(ObjectType.Block_LIGHTCUBE1,CubeLightComponent.EngineLayer)!.Value;Assert.NotEqual(0u,first.Timer);
        uint now=first.Timer+1;f.Rig.Engine.Timer.UpdateTime(now*1000000L);int mark=f.Rig.Mark();
        cube.StopBehaviorStateLight(999999,25,false);
        var after=cube.PlayingForTest(ObjectType.Block_LIGHTCUBE1,CubeLightComponent.EngineLayer)!.Value;
        Assert.Equal(now+5100u,after.Timer);Assert.Contains(f.Rig.SentSince(mark),message=>message is CubeLights);
    }
}
