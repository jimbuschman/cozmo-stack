using System.Buffers.Binary;
using System.Reflection;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;
using Fixture=Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;
namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class SparksKnockOverNativeTests
{
    private static Activity Activity(Fixture f)=>NativeBehaviorRuntime.AllActivities(f.Runtime.Tree).Single(a=>a.Id=="SparksKnockOverCubes");
    private static NativeSparkedActivity Owner(Fixture f)=>Activity(f).SparkedOwner!;
    private static void Entry(Fixture f,byte soft=0)
    {
        // Supplied app admission and canonical picker-success leaf; raw ActivateSpark/picker remains MISSING.
        var manager=f.Robot.BehaviorManagerOwner!;manager.SparkNameInput=id=>id==5?"KnockOverThreeCubeStack":"Count";
        manager.SetNativeRequestedSpark(5,soft);f.Runtime.AdmitActivity("SparksKnockOverCubes");manager.SwitchToNativeRequestedSpark();
    }
    private static void Terminal(Fixture f,uint result=0)
    {
        // Explicit terminal result supplied at actual ActionList root; not a claim of firmware execution.
        Assert.IsAssignableFrom<ActionRunner>(f.Robot.ActionList.GetCurrentAction()).State=result;f.Robot.ActionList.Update();
    }
    [Theory]
    [InlineData(0x55,(byte)0,false)] [InlineData(5,(byte)0,true)] [InlineData(5,(byte)1,false)]
    public void ConfiguredTwoCubeInstanceUsesOwnMinimumAndInvalidMiddleAndCanonicalHardGate(int id,byte soft,bool hard)
    {
        // S1-S3: actual configured native behavior, supplied two-cube scene/unlock/canonical entry leaf.
        using var f=new Fixture();f.Add(7,300,22);f.Add(9,300,66);f.Robot.Progression!.Unlocked.Add(5);f.Runtime.Configurations.ForceUpdate();f.Runtime.Configurations.Update();
        var knock=Assert.IsType<NativeKnockOverCubesBehavior>(f.Runtime.Bound["SparksKnockOverCubes"]);Assert.Equal((byte)2,knock.MinimumHeight);Assert.False(f.Runtime.Bound["KnockOverCubes"].IsRunnable(f.Runtime.Context));
        var manager=f.Robot.BehaviorManagerOwner!;manager.SparkNameInput=_=>"supplied-input";manager.SetNativeRequestedSpark(id,soft);manager.SwitchToNativeRequestedSpark();
        Assert.True(knock.IsRunnable(f.Runtime.Context));Assert.True(manager.StartAsync(knock.Id,0).GetAwaiter().GetResult());
        Assert.Equal(hard,knock.HardSpark);Assert.Equal(hard?"KnockingOverStack":"ReachingForBlock",knock.Phase);Assert.Equal(uint.MaxValue,knock.Middle);
        foreach(int member in new[]{-1,8,99})f.Rig.Engine.AnimationMessages.Deliver(0x11,true,new CubeUpAxisEvent(1,member,UpAxis.XPositive));Assert.Empty(knock.Tipped);
        f.Rig.Engine.AnimationMessages.Deliver(0x11,true,new CubeUpAxisEvent(1,9,UpAxis.XPositive));Assert.Equal(new uint[]{9},knock.Tipped);
    }
    [Fact]
    public void ConstructorLifetimeRecipientsFilterFamilyAndResetOnlyOnSelectionAndRetire()
    {
        // S9,S14,S15,S28/H6: actual constructed owner, local source-prefix carrier supplied.
        using var f=new Fixture();var owner=Owner(f);var bus=f.Rig.Engine.AnimationMessages;
        bus.Deliver(0x8B,true,4u);Assert.Equal(0u,owner.Count);bus.Deliver(0x8B,true,13u);Assert.Equal(1u,owner.Count);
        SourceField(owner,"Count",uint.MaxValue);bus.Deliver(0x8B,true,13u);Assert.Equal(0u,owner.Count); // native unsigned increment wraps.
        foreach(uint family in new uint[]{0,1,2,3})bus.Deliver(0x44,true,new NativeObservedObject(1,family,999,family));
        bus.Deliver(0x44,true,new NativeObservedObject(1,1,0,1));Assert.Equal(new uint[]{1,2},owner.Observed);
        Entry(f,1);Assert.Equal(0u,owner.Count);Assert.Empty(owner.Observed);
        Activity(f).OnDeselected(0);bus.Deliver(0x8B,true,13u);Assert.Equal(1u,owner.Count);
        int count=bus.Count(0x8B,true);owner.Dispose();Assert.Equal(count-1,bus.Count(0x8B,true));bus.Deliver(0x8B,true,13u);Assert.Equal(1u,owner.Count);
    }
    [Theory]
    [InlineData(0,0x211)] [InlineData(1,(int)AnimationTrigger.SoftSparkUpgradeTracks)]
    public void ActualGetInAndNativeSingleMultiSequenceAreOwnedAndStopClearsVector(byte soft,int trigger)
    {
        // P1-P6/S11,S21: actual configured ID3D and manager/ActionList; supplied terminal child results.
        using var f=new Fixture();Entry(f,soft);var manager=f.Robot.BehaviorManagerOwner!;
        manager.Update(0,0);var arbitrary=Assert.IsType<NativePlayArbitraryAnimBehavior>(manager.Current);
        Assert.Equal(new[]{(AnimationTrigger)trigger},arbitrary.Triggers);
        var single=Assert.IsAssignableFrom<TriggerAnimationAction>(f.Robot.ActionList.GetCurrentAction());Assert.Equal((AnimationTrigger)trigger,single.Request.Trigger);Assert.Equal(1,single.Request.Argument2);
        manager.Stop(BehaviorStopReason.Interrupted,0);Assert.True(single.DestructionObserved);Assert.Empty(arbitrary.Triggers);
        manager.Activity=null;
        arbitrary.SetAnimationTriggers(new[]{(AnimationTrigger)0x216,(AnimationTrigger)0x212},2);
        Assert.True(manager.StartAsync(arbitrary.Id,0).GetAwaiter().GetResult());
        var first=Assert.IsType<CompoundActionSequential>(f.Robot.ActionList.GetCurrentAction());
        Assert.Equal(new[]{(AnimationTrigger)0x216,(AnimationTrigger)0x212},first.Children.Cast<TriggerAnimationAction>().Select(a=>a.Request.Trigger));
        Assert.All(first.Children.Cast<TriggerAnimationAction>(),a=>Assert.Equal(1,a.Request.Argument2));
        Terminal(f);var second=Assert.IsType<CompoundActionSequential>(f.Robot.ActionList.GetCurrentAction());Assert.NotSame(first,second);
        Terminal(f);Assert.False(arbitrary.HasCurrentAction);Assert.Null(f.Robot.ActionList.GetCurrentAction());
        manager.Stop(BehaviorStopReason.Interrupted,0);Assert.Empty(arbitrary.Triggers);
    }
    [Fact]
    public void SourceFloatEdgesAreOrderedAndRoundedBeforeNegativeEpsilon()
    {
        // S17/H11 exact f32; NaN comparison must remain false.
        float epsilon=BitConverter.UInt32BitsToSingle(0xB727C5AC);
        Assert.True(NativeSparkedActivity.Deadline(1f,0f,1f+epsilon));
        Assert.False(NativeSparkedActivity.Deadline(1f,0f,MathF.BitDecrement(1f+epsilon)));
        Assert.False(NativeSparkedActivity.Deadline(float.NaN,0f,1f));
        Assert.True(NativeSparkedActivity.Deadline(16777216f,1f,16777216f));
    }
    private static void SourceField<T>(NativeSparkedActivity owner,string property,T value)=>typeof(NativeSparkedActivity).GetProperty(property,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(owner,value);
    [Theory]
    [InlineData((byte)0,0u,false,false,false)]
    [InlineData((byte)0,uint.MaxValue,false,false,false)]
    [InlineData((byte)0,1u,false,false,true)]
    [InlineData((byte)1,1u,false,false,true)]
    [InlineData((byte)0,1u,true,false,true)]
    [InlineData((byte)0,1u,false,true,true)]
    public void CompleteUsesSignedCountCancelAndPendingRequestedPreservation(byte soft,uint count,bool cancel,bool pending,bool success)
    {
        // S24-S26: unit supplied native state4/count at actual constructed owner's virtual desired boundary.
        // This tests Complete, not a recovered transition into state4. Complete must not install a once latch.
        using var f=new Fixture();Entry(f,soft);var owner=Owner(f);var manager=f.Robot.BehaviorManagerOwner!;
        SourceField(owner,"State",4);SourceField(owner,"Count",count);
        if(cancel)manager.HandleSparkManagerMessage(new(0,0x55));
        else if(pending)manager.SetNativeRequestedSpark(6,0);
        var raw=new List<byte[]>();var local=new List<bool>();f.Rig.Engine.EngineToGameMessage+=(tag,body)=>{if(tag==0x84)raw.Add(body.ToArray());};
        using var sub=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x84,true,p=>{Assert.Empty(raw);local.Add(Assert.IsType<bool>(p));});
        Assert.Null(owner.Desired(null,0,f.Runtime.Context));Assert.Equal(4,owner.State);
        Assert.Equal(pending?6:0x55,manager.RequestedSparkId);Assert.Equal(5,manager.ActiveSparkId);
        f.Rig.Engine.DrainAppDelivery();
        if(soft==0&&!cancel){Assert.Equal(new[]{success},local);Assert.Equal(new[]{success?(byte)1:(byte)0},Assert.Single(raw));}
        else{Assert.Empty(local);Assert.Empty(raw);}
    }
    [Fact]
    public void ReactionCompletionUsesActualTriggerAndStateFiveWhileSoftAloneDoesNot()
    {
        // S16: supplied canonical current-trigger field (reaction strategy admission is outside this unit).
        using var f=new Fixture();Entry(f,1);var owner=Owner(f);SourceField(owner,"Count",1u);
        owner.Update();Assert.Equal(0,owner.State); // soft alone is not the native fast path.
        typeof(BehaviorManager).GetField("_currentReaction",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(f.Robot.BehaviorManagerOwner,ReactionTrigger.CliffDetected);
        owner.Update();Assert.Equal(5,owner.State);Assert.Equal(0x55,f.Robot.BehaviorManagerOwner!.RequestedSparkId);
    }
    [Fact]
    public void TimeoutReadsCurrentUnlockAndSchedulesEndThenActualFailureGetOutAndReport()
    {
        // S17-S19/S23: actual manager/ActionList with supplied state2 and terminal action results; no fake timer action.
        using var f=new Fixture();Entry(f);var manager=f.Robot.BehaviorManagerOwner!;manager.Update(0,0);
        var owner=Owner(f);SourceField(owner,"State",2);owner.Maximum=0;owner.Minimum=100;
        var current=f.Robot.ActionList.GetCurrentAction();owner.Update();Assert.Equal(3,owner.State);Assert.Same(current,f.Robot.ActionList.GetCurrentAction());
        Assert.True(manager.HasDisableLock((ReactionTrigger)8,"finalAnimLockReactions_activityLock"));
        Terminal(f);manager.Update(0,0);manager.Update(0,0);
        var sequence=Assert.IsType<CompoundActionSequential>(f.Robot.ActionList.GetCurrentAction());
        Assert.Equal(new[]{(AnimationTrigger)0x210,(AnimationTrigger)0x212},sequence.Children.Cast<TriggerAnimationAction>().Select(a=>a.Request.Trigger));
        var ended=new List<bool>();using var sub=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x84,true,p=>ended.Add((bool)p!));
        Terminal(f);manager.Update(0,0);manager.Update(0,0);Assert.Equal(new[]{false},ended);Assert.Equal(4,owner.State);Assert.True(f.Runtime.AwaitingParentActivityPick);
    }
    [Fact]
    public void InvalidActiveIdDoesNotUseStaleSoftByteAndInterruptionRetainsNativeSwitchGap()
    {
        // S20/S21/S23: supplied canonical leaf values; no fake immediate Stop for existing M8-019 gap.
        using var f=new Fixture();var manager=f.Robot.BehaviorManagerOwner!;manager.SparkNameInput=_=>"input";
        manager.SetNativeRequestedSpark(0x55,1);f.Runtime.AdmitActivity("SparksKnockOverCubes");manager.SwitchToNativeRequestedSpark();
        var owner=Owner(f);var child=Assert.IsType<NativePlayArbitraryAnimBehavior>(owner.Desired(null,0,f.Runtime.Context));Assert.Equal(new[]{(AnimationTrigger)0x211},child.Triggers);
        SourceField(owner,"State",3);Assert.Same(child,owner.Desired(null,0,f.Runtime.Context));Assert.Equal(new[]{(AnimationTrigger)0x210,(AnimationTrigger)0x212},child.Triggers);
        manager.SetNativeRequestedSpark(5,0);manager.SwitchToNativeRequestedSpark();manager.SetNativeRequestedSpark(6,0);SourceField(owner,"State",2);
        owner.Update();Assert.Equal(4,owner.State);Assert.Null(manager.Current);Assert.Equal(6,manager.RequestedSparkId);
        var ended=new List<bool>();using var sub=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x84,true,p=>ended.Add((bool)p!));owner.Desired(null,0,f.Runtime.Context);Assert.Empty(ended);
    }
    [Fact]
    public void PerLocatorBackpackPreservesHostSlotAndStaleStopAndNextActionDoesNotCancel()
    {
        // H1-H5/S17,S19: actual component arbitration/manager/action queue. Supplied action result remains live until terminal.
        using var f=new Fixture();var body=f.Rig.Robot.Lights.Body;
        var other=new BodyLightComponent.Locator();body.StartLooping(Enumerable.Repeat(LedPattern.Solid(LedColor.Red),5).ToArray(),1,other);
        f.Rig.Robot.Lights.SetBackpack(LedColor.Green,LedColor.Green,LedColor.Green);Entry(f);
        Assert.Contains(f.Rig.Robot.Animations.DrivingAnimations.Entries,e=>e.LockName=="SparksKnockOverCubes");
        var arbitrary=Assert.IsType<NativePlayArbitraryAnimBehavior>(f.Runtime.Bound["PlayArbitraryAnim"]);
        f.Robot.BehaviorManagerOwner!.Update(0,0);var current=f.Robot.ActionList.GetCurrentAction();Assert.NotNull(arbitrary.SharedHandle);
        f.Robot.BehaviorManagerOwner.RequestCurrentBehaviorEndOnNextActionComplete();Assert.Same(current,f.Robot.ActionList.GetCurrentAction());Assert.True(arbitrary.HasCurrentAction);Assert.Null(arbitrary.SharedHandle);
        Owner(f).ResetLightsAndAnimations();Assert.DoesNotContain(f.Rig.Robot.Animations.DrivingAnimations.Entries,e=>e.LockName=="SparksKnockOverCubes");
        Assert.True(body.StopLooping(other));Assert.False(body.StopLooping(other));
        Terminal(f);Assert.False(arbitrary.HasCurrentAction);
        var manager=f.Robot.BehaviorManagerOwner!;manager.Activity=null;manager.Stop(BehaviorStopReason.Interrupted,0);
        arbitrary.SetAnimationTriggers(new[]{(AnimationTrigger)0x216,(AnimationTrigger)0x212},2);Assert.True(manager.StartAsync(arbitrary.Id,0).GetAwaiter().GetResult());
        var first=Assert.IsType<CompoundActionSequential>(f.Robot.ActionList.GetCurrentAction());
        manager.RequestCurrentBehaviorEndOnNextActionComplete();Assert.Same(first,f.Robot.ActionList.GetCurrentAction());Assert.False(first.DestructionObserved);
        Terminal(f);Assert.True(first.DestructionObserved);Assert.Null(f.Robot.ActionList.GetCurrentAction());Assert.False(arbitrary.HasCurrentAction);
        manager.Update(0,0);Assert.Null(manager.Current);Assert.Null(f.Robot.ActionList.GetCurrentAction()); // no second loop after owned completion.
    }
    [Fact]
    public void SparkBodyPatternUsesExactWireAndResetRevealsPriorHostAndPriorityEntries()
    {
        // H1-H3 accepted M4 wire/arbitration: actual component sends, supplied earlier host/priority configs.
        using var f=new Fixture();var body=f.Rig.Robot.Lights.Body;
        f.Rig.Robot.Lights.SetBackpack(LedColor.Green,LedColor.Green,LedColor.Green);Entry(f);
        int mark=f.Rig.Mark();body.Update(f.Robot.StoredState!,_=>{});
        var middle=Assert.Single(f.Rig.SentSince(mark).OfType<BackpackLightsMiddle>());
        Assert.All(middle.Field0,led=>{Assert.Equal((ushort)0xFFFF,led.OnColor);Assert.Equal((ushort)0x8000,led.OffColor);Assert.Equal((byte)12,led.OnFrames);Assert.Equal((byte)37,led.OffFrames);Assert.Equal((byte)0,led.TransitionOnFrames);Assert.Equal((byte)0,led.TransitionOffFrames);});
        Assert.Equal(new short[]{0,4,8},middle.Field0.Select(led=>led.Offset));
        Assert.All(Assert.Single(f.Rig.SentSince(mark).OfType<BackpackLightsTurnSignals>()).Field0,led=>{Assert.Equal((ushort)0x8000,led.OnColor);Assert.Equal((ushort)0x8000,led.OffColor);Assert.Equal((byte)0,led.OnFrames);Assert.Equal((byte)0,led.OffFrames);Assert.Equal((short)0,led.Offset);});
        var priority=new BodyLightComponent.Locator();body.StartLooping(Enumerable.Repeat(LedPattern.Solid(LedColor.Red),5).ToArray(),1,priority);
        mark=f.Rig.Mark();body.Update(f.Robot.StoredState!,_=>{});Assert.All(Assert.Single(f.Rig.SentSince(mark).OfType<BackpackLightsMiddle>()).Field0,led=>Assert.Equal((ushort)0xFC00,led.OnColor));
        Owner(f).ResetLightsAndAnimations();Assert.True(body.StopLooping(priority));
        mark=f.Rig.Mark();body.Update(f.Robot.StoredState!,_=>{});Assert.All(Assert.Single(f.Rig.SentSince(mark).OfType<BackpackLightsMiddle>()).Field0,led=>Assert.Equal((ushort)0x83E0,led.OnColor));
    }
    [Fact]
    public void DeselectionReleasesActivityScopesThenPendingRewardBeforeOwnEngineLayerCleanup()
    {
        // S27/H4,H5,H12: actual selected owner and existing Needs recipient. M15-071 amount/raw award stays a named gap.
        using var f=new Fixture();M4ControlTests.ConnectCube(f.Rig);Entry(f);Assert.Equal((byte)21,f.Robot.BehaviorPublicState.State.Activity);
        var manager=f.Robot.BehaviorManagerOwner!;var cubes=f.Rig.Robot.Lights.Cubes;var names=new List<string>();f.Rig.Engine.LogEntry+=entry=>{if(entry.Name=="CubeLightComponent.StopAllAnimsOnLayer.AllObjects")names.Add(entry.Text??"");};
        bool notified=false,recipient=false;f.Rig.Engine.Needs.SparksRewardPending=true;f.Rig.Engine.Needs.SparksRewardAwarded+=()=>notified=true;
        f.Rig.Engine.Needs.Log+=line=>
        {
            if(line!="needs.freeplay_sparks_awarded")return;recipient=true;
            Assert.False(manager.HasDisableLock(ReactionTrigger.CubeMoved,"SparksKnockOverCubes_activityLock"));
            Assert.DoesNotContain(f.Rig.Robot.Animations.Scheduler.IdleStack,e=>e.Lock=="Activity_SparksKnockOverCubes");
            Assert.Contains(f.Rig.Robot.Animations.DrivingAnimations.Entries,e=>e.LockName=="SparksKnockOverCubes");Assert.Empty(names);Assert.False(notified);
        };
        Activity(f).OnDeselected(0);Assert.True(recipient);Assert.False(f.Rig.Engine.Needs.SparksRewardPending);Assert.False(notified);
        Assert.Equal(new[]{"Stopping all anims for all objects on layer Engine"},names);
        f.Rig.Engine.DrainAppDelivery();Assert.True(notified);
    }
    [Fact]
    public void OwnStopAllCallsOneEngineLayerUpdateAndPreservesSuppliedUserAndStateEntries()
    {
        // H12 unit: supplied already-populated three source-layer stacks at the accepted component boundary.
        // This does not assert an app admission that creates this simultaneous combination.
        using var f=new Fixture();M4ControlTests.ConnectCube(f.Rig);Entry(f);var cubes=f.Rig.Robot.Lights.Cubes;
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
        var infos=(System.Collections.IDictionary)typeof(CubeLightComponent).GetField("_infos",flags)!.GetValue(cubes)!;
        var info=infos[ObjectType.Block_LIGHTCUBE1]!;var layers=(Array)info.GetType().GetField("Layers",flags)!.GetValue(info)!;
        var entries=new object[3];
        for(int layer=0;layer<3;layer++)
        {
            var list=(System.Collections.IList)layers.GetValue(layer)!;list.Clear();
            var playing=Activator.CreateInstance(list.GetType().GetGenericArguments()[0],true)!;var type=playing.GetType();
            type.GetField("BaseAnim",flags)!.SetValue(playing,new[]{new CubeLightPattern(Enumerable.Repeat(LedPattern.Solid(LedColor.Blue),4).ToArray(),0,0,false,false,"supplied source-layer hold")});
            type.GetField("Name",flags)!.SetValue(playing,"supplied "+layer);list.Add(playing);entries[layer]=playing;
        }
        info.GetType().GetField("CurrentLayer",flags)!.SetValue(info,1);
        var stops=new List<string>();f.Rig.Engine.LogEntry+=e=>{if(e.Name=="CubeLightComponent.StopAllAnimsOnLayer.AllObjects")stops.Add(e.Text);};
        Activity(f).OnDeselected(0);
        Assert.Equal(new[]{"Stopping all anims for all objects on layer Engine"},stops);
        Assert.Empty((System.Collections.IList)layers.GetValue(1)!);
        foreach(int layer in new[]{0,2})
        {
            var entry=Assert.Single(((System.Collections.IList)layers.GetValue(layer)!).Cast<object>());Assert.Same(entries[layer],entry);Assert.False((bool)entry.GetType().GetField("Stop",flags)!.GetValue(entry)!);
        }
    }
    [Theory]
    [InlineData((byte)0)] [InlineData((byte)1)]
    public void ConfiguredSparkOwnerRunsTwoCubeMotionGetInOutNeedsAndHardReport(byte soft)
    {
        // reach: configured factory/manager/ActionList/owned children. Supplied stack, unlock and firmware path/motor/animation/axis inputs.
        // Actual worker images supply the selected object's verification. Inherited lattice/PnP remain labelled frontiers, no activity is invented.
        using var f=new Fixture();M4ControlTests.ConnectCube(f.Rig);var connected=Assert.Single(f.M.World.ConnectedObjects);uint bottom=connected.ObjectId;
        f.Add(bottom,300,22);f.Add(101,300,66);f.Robot.Progression!.Unlocked.Add(5);
        f.Runtime.Configurations.ForceUpdate();f.Runtime.Configurations.Update();var knock=Assert.IsType<NativeKnockOverCubesBehavior>(f.Runtime.Bound["SparksKnockOverCubes"]);Assert.Equal((byte)2,knock.MinimumHeight);Assert.False(f.Runtime.Bound["KnockOverCubes"].IsRunnable(f.Runtime.Context));Assert.True(knock.IsRunnable(f.Runtime.Context));
        f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        CompoundActionSequential? knockRoot=null;var delivery=new List<string>();var rawReports=new List<byte[]>();
        var reports=new List<RobotCompletedAction>();var phaseTrace=new List<string>();var internalRecords=new List<RobotCompletedAction>();
        int watcherHandle=f.Robot.ActionList.Watcher.RegisterCallback(internalRecords.Add);
        using var local=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x5A,true,p=>{if(p is RobotCompletedAction report){reports.Add(report);if(report.Tag==knockRoot?.Tag)delivery.Add("local");}});
        var app=new List<ActionCompletionReport>();f.Rig.Engine.ActionCompleted+=report=>{app.Add(report);if(report.Tag==knockRoot?.Tag)delivery.Add("typed");};
        f.Rig.Engine.EngineToGameMessage+=(tag,body)=>{if(tag==0x5A&&BinaryPrimitives.ReadUInt32LittleEndian(body.Span)==knockRoot?.Tag){rawReports.Add(body.ToArray());delivery.Add("raw");}};
        var logs=new List<string>();f.Rig.Engine.LogEntry+=e=>logs.Add(e.Name+":"+e.Text);
        var needs=new List<string>();f.Rig.Engine.Needs.Log+=needs.Add;
        var sparkRaw=new List<(ushort Tag,byte[] Bytes)>();var sparkOrder=new List<string>();
        f.Rig.Engine.EngineToGameMessage+=(tag,body)=>{if(tag is 0x83 or 0x84){sparkRaw.Add((tag,body.ToArray()));sparkOrder.Add("raw"+tag);}};
        using var started=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x83,true,p=>{Assert.Equal(5,Assert.IsType<int>(p));Assert.Empty(sparkRaw);sparkOrder.Add("local131");});
        using var ended=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x84,true,p=>{Assert.True(Assert.IsType<bool>(p));sparkOrder.Add("local132");});
        var manager=f.Robot.BehaviorManagerOwner!;manager.DisableReactionsWithLock("knock-offline-inputs",Enumerable.Range(0,21).Select(i=>i==(int)ReactionTrigger.ObjectPositionUpdated).ToArray(),false);
        int mark=0,motorMark=0,nextSegment=0;sbyte currentSegment=-1;ExecutePath? pending=null;
        var wire=new List<RobotMessage>();var paths=new HashSet<ushort>();string finalKind="";int completedPaths=0;
        void Motors()
        {
            foreach(var message in f.Rig.SentSince(motorMark))switch(message)
            {
                case SetHeadAngle head:f.Head=head.AngleRad;f.Rig.Data(new MotorActionAck{ActionId=head.ActionId});break;
                case SetLiftHeight lift:f.Lift=(float)Math.Asin((lift.HeightMm-45f)/66f);f.Rig.Data(new MotorActionAck{ActionId=lift.ActionId});break;
                case SetBodyAngle body:f.Yaw=body.AngleRad;f.Rig.Data(new MotorActionAck{ActionId=body.ActionId});break;
            }
            motorMark=f.Rig.Mark();
            var state=f.Rig.MakeState(RobotStatusFlag.HeadInPos|RobotStatusFlag.LiftInPos,origin:f.Robot.CurrentOriginId,frame:f.Robot.PoseFrameId,x:f.X,y:f.Y,head:f.Head,liftAngle:f.Lift);
            state.Pose.Angle=f.Yaw;state.CurrPathSegment=currentSegment;f.Rig.Data(state);f.Rig.Tick();
        }
        void Receive()
        {
            var sent=f.Rig.SentSince(mark);mark=f.Rig.Mark();
            foreach(var message in sent)
            {
                if(message is ClearPath){wire.Clear();pending=null;nextSegment=0;currentSegment=-1;}
                else if(message is AppendPathSegmentLine or AppendPathSegmentArc or AppendPathSegmentPointTurn)wire.Add(message);
                else if(message is ExecutePath execute&&paths.Add(execute.EventId))
                {pending=execute;nextSegment=0;f.Rig.Data(new PathFollowingEvent{EventId=execute.EventId,EventType=(byte)PathEventType.Started});phaseTrace.Add($"START {execute.EventId} buffered={wire.Count} total={f.Rig.Robot.Motion.Path.DoleSegmentCount}");}
            }
        }
        var animations=new HashSet<byte>();uint image=0;bool tipped=false,approached=false;NativeDriveAndFlipRunner? interaction=null;
        try
        {
            manager.SparkNameInput=id=>id==5?"KnockOverThreeCubeStack":"Count";manager.SetNativeRequestedSpark(5,soft);f.Runtime.AdmitActivity("SparksKnockOverCubes");manager.SwitchToNativeRequestedSpark();f.Rig.Tick();
            for(int tick=0;tick<320&&!f.Runtime.AwaitingParentActivityPick;tick++)
            {
                if(f.M.GetNativePlanners().Lattice is {} runtime&&runtime.CheckPlanningStatus==1)
                    Assert.True(SpinWait.SpinUntil(()=>runtime.CheckPlanningStatus!=1,TimeSpan.FromSeconds(10)),"offline planner watchdog");
                Motors();Receive();
                if(pending is {} active)
                {
                    if(nextSegment<wire.Count)
                    {
                        var segment=wire[nextSegment];float centerX=f.X,centerY=f.Y;
                        switch(segment)
                        {
                            case AppendPathSegmentLine line:centerX=line.XEndMm;centerY=line.YEndMm;break; //no invented line heading.
                            case AppendPathSegmentArc arc:
                                float angle=arc.StartRad+arc.SweepRad;centerX=arc.XCenterMm+arc.RadiusMm*MathF.Cos(angle);centerY=arc.YCenterMm+arc.RadiusMm*MathF.Sin(angle);f.Yaw=angle+MathF.Sign(arc.SweepRad)*Radians.Pi*.5f;break;
                            case AppendPathSegmentPointTurn turn:centerX=turn.XMm;centerY=turn.YMm;f.Yaw=turn.TargetAngleRad;break;
                        }
                        float offset=f.Robot.CarryingOwner!.CarriedObjectId is null?20f:0f;
                        f.X=centerX+offset*MathF.Cos(f.Yaw);f.Y=centerY+offset*MathF.Sin(f.Yaw);
                        currentSegment=checked((sbyte)nextSegment);nextSegment++;finalKind=segment.GetType().Name;
                        phaseTrace.Add($"SEGMENT {active.EventId}/{currentSegment} {finalKind} {Convert.ToHexString(segment.ToBytes())}");Motors();Receive();
                    }
                    else if(nextSegment==f.Rig.Robot.Motion.Path.DoleSegmentCount)
                    {
                        phaseTrace.Add($"COMPLETE {active.EventId} consumed={nextSegment} final={finalKind}");completedPaths++;
                        f.Rig.Data(new PathFollowingEvent{EventId=active.EventId,EventType=(byte)PathEventType.Completed});pending=null;currentSegment=-1;Motors();Receive();
                    }
                }
                byte animation=f.Rig.Robot.Animations.CurrentTag;
                if(animation!=0&&animations.Add(animation)){f.Rig.Data(new AnimationStarted{AnimId=animation});f.Rig.Data(new AnimationEnded{AnimId=animation});f.Rig.Tick();}
                interaction??=knock.Interaction;if(interaction is not null)knockRoot??=knock.LastSequence;
                if(knock.Interaction is {} observed)
                {
                    var goal=observed.Drive?.Drive;
                    phaseTrace.Add($"{tick} phase={knock.Phase}/{knock.Attempts} MAIN={f.Robot.GetCheckedMainPose().Translation} yaw={f.Robot.GetCheckedMainPose().AngleAroundZ} drive={observed.Drive?.State:X8} flip={observed.Flip.State:X8} flag={observed.Flip.CheckPreActionPose} goal="+(goal?.Goals is {Count:>0} gs?string.Join(";",gs.Select(g=>g.Translation+"/"+g.AngleAroundZ)):"none"));
                }
                if(knock.Interaction?.Drive?.Drive is {} drive&&drive.Initialized)approached=true;
                if(knock.Interaction?.Drive?.Sequence.Children.OfType<NativeTurnTowardsObjectRunner>().Any(turn=>turn.Initialized)==true)f.Image(++image,true,300);
                var activeFlip=knock.Phase=="BlindlyFlipping"?knock.LastSequence?.Children.OfType<NativeFlipBlockRunner>().FirstOrDefault():knock.Interaction?.Flip;
                if(!tipped&&activeFlip is {Initialized:true,State:EngineActionResult.Running})
                {f.Rig.Data(new ObjectUpAxisChanged{Timestamp=f.Robot.StoredState!.Timestamp,ObjectID=unchecked((uint)connected.ActiveId),UpAxis=UpAxis.XPositive});f.Rig.Tick();tipped=true;}
            }
            f.Rig.Engine.DrainAppDelivery();string diagnostic=knock.Phase+" attempts="+knock.Attempts+" last="+knock.Interaction?.State.ToString("X8")+" "+string.Join(" | ",logs.TakeLast(20));
            var traceDirectory=Path.Combine(new DirectoryInfo(f.Rig.Engine.Options.ResourcesPath!).Parent!.Parent!.Parent!.Parent!.FullName,".scratch","sparks-knockover-1");Directory.CreateDirectory(traceDirectory);
            File.WriteAllLines(Path.Combine(traceDirectory,"configured-input-trace.txt"),phaseTrace.Concat(reports.Select(r=>$"REPORT {r.Tag}/{r.ActionType:X}/{r.Result:X8}")).Concat(logs));
            Assert.True(approached,diagnostic);Assert.True(tipped,diagnostic);Assert.NotNull(interaction);Assert.True(completedPaths>0);
            Assert.Equal("PlayingReaction",knock.Phase);Assert.False(ReferenceEquals(manager.Current,knock));Assert.NotNull(manager.Activity);Assert.True(f.Runtime.AwaitingParentActivityPick);Assert.Equal(4,Owner(f).State);Assert.Equal(0x55,manager.RequestedSparkId);Assert.Equal(5,manager.ActiveSparkId);Assert.Equal(uint.MaxValue,knock.Middle);
            Assert.Contains(needs,l=>l.StartsWith("needs.action_completed KnockDownCubes_Sparked:"));
            if(soft==0)
            {
                Assert.Equal(new ushort[]{0x83,0x84},sparkRaw.Select(r=>r.Tag));Assert.Equal(new byte[]{5,0,0,0},sparkRaw[0].Bytes);Assert.Equal(new byte[]{1},sparkRaw[1].Bytes);
                Assert.Equal(new[]{"local131","raw131","local132","raw132"},sparkOrder);
            }
            else{Assert.Empty(sparkRaw);Assert.Empty(sparkOrder);}
            int replies=sparkRaw.Count;for(int i=0;i<3;i++)f.Rig.Tick();Assert.Equal(replies,sparkRaw.Count);Assert.Equal(4,Owner(f).State);
            Assert.True(manager.HasDisableLock(ReactionTrigger.CubeMoved,"SparksKnockOverCubes_activityLock"));
            // Explicit supplied changed next-activity admission resumes the unknown parent picker boundary.
            f.Runtime.AdmitActivity("PutDownDispatch");Assert.False(f.Runtime.AwaitingParentActivityPick);
            Assert.False(manager.HasDisableLock(ReactionTrigger.CubeMoved,"SparksKnockOverCubes_activityLock"));
            Assert.DoesNotContain(f.Rig.Robot.Animations.Scheduler.IdleStack,e=>e.Lock=="Activity_SparksKnockOverCubes");
            //53FA5C/78/AA: queued outer root publishes5A;54F050 nested proxy stays in watcher/cache.
            var nested=Assert.Single(internalRecords.Where(r=>r.Tag==interaction!.Tag));Assert.Equal(0x0F,nested.ActionType);Assert.Equal(0u,nested.Result);Assert.Equal((byte)6,nested.CompletionVariant);Assert.Null(nested.ObjectInteraction);
            Assert.True(interaction!.DestructionObserved);Assert.Equal(0,knock.Attempts);
            var root=Assert.Single(reports.Where(r=>r.Tag==knockRoot!.Tag));Assert.Equal(-2,root.ActionType);Assert.Equal(0u,root.Result);Assert.Equal((byte)6,root.CompletionVariant);Assert.Null(root.ObjectInteraction);
            Assert.Contains(0x04000001u,root.SubActionResults);Assert.Contains(0u,root.SubActionResults);Assert.Equal(root.SubActionResults.Order().Distinct(),root.SubActionResults);
            var typed=Assert.Single(app.Where(r=>r.Tag==knockRoot!.Tag));Assert.Equal(root.SubActionResults,typed.SubActionResults);Assert.Equal((byte)6,typed.CompletionVariant);
            var raw=Assert.Single(rawReports);Assert.Equal(14+4*root.SubActionResults.Count,raw.Length);Assert.Equal(knockRoot!.Tag,BinaryPrimitives.ReadUInt32LittleEndian(raw));Assert.Equal(-2,BinaryPrimitives.ReadInt32LittleEndian(raw.AsSpan(4)));Assert.Equal(0u,BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(8)));Assert.Equal((byte)root.SubActionResults.Count,raw[12]);Assert.Equal((byte)6,raw[^1]);
            for(int i=0;i<root.SubActionResults.Count;i++)Assert.Equal(root.SubActionResults[i],BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(13+4*i)));
            Assert.Equal(new[]{"local","raw","typed"},delivery);Assert.Null(f.Robot.CarryingOwner!.CarriedObjectId);
            Assert.DoesNotContain(f.Rig.SentSince(0),m=>m is DockWithObject);Assert.NotEmpty(f.Rig.SentSince(0).OfType<SetLiftHeight>());
        }
        finally{f.Robot.ActionList.Watcher.UnregisterCallback(watcherHandle);manager.RemoveDisableReactionsLock("knock-offline-inputs");manager.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);f.Robot.ActionList.Clear();}
    }
}
