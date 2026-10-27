using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Xunit;
namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class M7LifetimeCheckedRowsTests
{
    private static string Obb()
    {for(var d=new DirectoryInfo(AppContext.BaseDirectory);d is not null;d=d.Parent){var p=Path.Combine(d.FullName,"re-analysis","obb");if(Directory.Exists(p))return p;}throw new InvalidOperationException("OBB required");}
    private static MoodModel Model()
    {
        var model=new MoodModel();model.AddEvent(new EmotionEvent("fixture",new[]{new EmotionAffector(EmotionType.Happy,.25)}));
        model.AddActionResultEvent("MOVE_HEAD_TO_ANGLE","SUCCESS","fixture");return model;
    }
    [Fact]
    public void ActualRemovalClearsActionListBeforeMoodAndNullsMoodOnlyAfterAllChildren()
    {
        using var robot=CozmoRobot.CreateOffline();var native=robot.Engine.Robot!;var mood=native.MoodOwner!;
        mood.Advance(1);mood.SetMoodEventOnCompletionEnabled("7",false);var before=mood.Snapshot();
        int id=mood.ActionCallbackId;Assert.True(id>0);Assert.All(mood.HistoryCounts,n=>Assert.True(n>0));
        var stackLogs=new List<string>();robot.Engine.StackLogLine+=stackLogs.Add;
        var trace=new List<string>();native.ActionList.LifetimeObservedForTest=trace.Add;var logs=new List<string>();mood.Log=logs.Add;mood.SourceLog=(l,c,k,t)=>logs.Add(k);int reports=0;mood.MoodStateBroadcast+=_=>reports++;
        mood.RetirementObservedForTest=stage=>
        {
            Assert.Null(native.ActionListOwner);Assert.Same(mood,native.MoodOwner);Assert.False(native.Lifetime.IsKnownNull(0x440));Assert.Equal(id,mood.ActionCallbackId);trace.Add(stage);
        };
        robot.Engine.Robots.RemoveRobot(1,false);
        Assert.Equal(new[]{"clear","clear","subscriptions","completion set","action event map","event times","history:8","history:7","history:6","history:5","history:4","history:3","history:2","history:1","history:0","allocation"},trace);
        Assert.True(mood.NativeRetired);Assert.Null(native.MoodOwner);Assert.True(native.Lifetime.IsKnownNull(0x440));Assert.Equal(id,mood.ActionCallbackId);
        Assert.All(mood.HistoryStorageLengths,n=>Assert.Equal(0,n));Assert.Equal(before,mood.Snapshot());Assert.Empty(logs);Assert.Equal(0,reports);Assert.DoesNotContain(stackLogs,line=>line.Contains("MoodMessage C3 operational handler"));
        Assert.Equal(0,robot.Engine.AnimationMessages.SubscriberCountForTest(0xC3,false));Assert.DoesNotContain("unregister",trace);
    }
    [Fact]
    public void RealC3SubscriptionDisconnectsOnlyWhenTheLastSharedOwnerReleasesIt()
    {
        using var robot=CozmoRobot.CreateOffline();var mood=robot.Engine.Robot!.MoodOwner!;
        Assert.Equal(1,robot.Engine.AnimationMessages.SubscriberCountForTest(0xC3,false));var held=mood.LastSubscriptionForTest!.Retain();
        robot.Engine.Robots.RemoveRobot(1,false);Assert.Equal(0,mood.SubscriptionCount);Assert.Equal(1,robot.Engine.AnimationMessages.SubscriberCountForTest(0xC3,false));
        held.Dispose();Assert.Equal(0,robot.Engine.AnimationMessages.SubscriberCountForTest(0xC3,false));
    }
    [Fact]
    public void ReverseSubscriptionRetirementAdvancesTheEndBeforeEachReleaseAndInvokesNoHandlers()
    {
        var mood=new MoodState(Model());var releases=new List<string>();
        mood.AddSubscriptionForTest(new RetainedSubscription(()=>{Assert.Equal(0,mood.SubscriptionCount);releases.Add("first");}));
        mood.AddSubscriptionForTest(new RetainedSubscription(()=>{Assert.Equal(1,mood.SubscriptionCount);releases.Add("second");}));
        mood.DestroyForRobot();Assert.Equal(new[]{"second","first"},releases);mood.DestroyForRobot();Assert.Equal(2,releases.Count);
    }
    [Fact]
    public void ExpiredSignalOwnerRetiresWithoutCallingTheMoodMessageHandler()
    {
        var hub=new UiSubscriptionHub();var mood=new MoodState(Model());int callbacks=0;
        mood.AddSubscriptionForTest(hub.Subscribe(0xC3,false,()=>callbacks++));hub.Dispose();mood.DestroyForRobot();Assert.Equal(0,callbacks);Assert.Equal(0,mood.SubscriptionCount);
    }
    [Fact]
    public void RetiringOneMoodNeverDestroysTheStaticModelOrAnotherMoodsActionMap()
    {
        var model=Model();var first=new MoodState(model);var second=new MoodState(model);first.DestroyForRobot();
        Assert.Single(model.ActionResultEvents);Assert.True(second.HandleActionEnded("MOVE_HEAD_TO_ANGLE","SUCCESS","1",1));Assert.Equal(.25,second[EmotionType.Happy]);
    }
    [Fact]
    public void RuntimeLogCalloutRunsAfterTheSharedLifetimeGateIsReleased()
    {
        var mood=new MoodState(Model());mood.Advance(1);int logs=0;
        mood.SourceLog=(l,c,k,t)=>{Assert.True(Task.Run(()=>mood.Snapshot()).Wait(TimeSpan.FromSeconds(2)));logs++;};
        mood.Advance(1);Assert.Equal(1,logs);
    }
    [Fact]
    public async Task RuntimeMutationCannotCrossHistoryRetirementOrResurrectTheFreedOwner()
    {
        var mood=new MoodState(Model());mood.Trigger("fixture",1);var before=mood.Snapshot();using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();
        mood.RetirementObservedForTest=stage=>{if(stage=="subscriptions"){entered.Set();Assert.True(release.Wait(TimeSpan.FromSeconds(3)));}};
        var retire=Task.Run(mood.DestroyForRobot);Assert.True(entered.Wait(TimeSpan.FromSeconds(2)));var update=Task.Run(()=>mood.Advance(30));
        try{Assert.False(update.IsCompleted);}finally{release.Set();}
        await Task.WhenAll(retire,update).WaitAsync(TimeSpan.FromSeconds(3));Assert.True(mood.NativeRetired);Assert.Equal(before,mood.Snapshot());Assert.All(mood.HistoryStorageLengths,n=>Assert.Equal(0,n));
    }
    [Fact]
    public void LateBindingCannotResurrectAnAlreadyDestroyedRobotMoodSlot()
    {
        using var robot=CozmoRobot.CreateOffline();var native=robot.Engine.Robot!;robot.Engine.Robots.RemoveRobot(1,false);var mood=new MoodState(Model());
        mood.BindNativeLifetime(native,0);Assert.Null(native.MoodOwner);Assert.Equal(0,robot.Engine.AnimationMessages.SubscriberCountForTest(0xC3,false));
    }

    [Fact]
    public void NullRobotGatePreservesNonzeroCallbackIdWithoutUsingTheRetainedList()
    {
        using var robot=CozmoRobot.CreateOffline();var native=robot.Engine.Robot!;var mood=native.MoodOwner!;int actualId=mood.ActionCallbackId;Assert.True(actualId>0);mood.AttachRobot(null);
        int unregistered=0;native.ActionList.LifetimeObservedForTest=operation=>{if(operation=="unregister")unregistered++;};
        mood.DestroyForRobot();Assert.True(mood.NativeRetired);Assert.Equal(actualId,mood.ActionCallbackId);Assert.Equal(0,unregistered);
    }
    [Fact]
    public void ZeroIdGateSkipsUnregisterEvenWithALiveRobotAndList()
    {
        using var robot=CozmoRobot.CreateOffline();var native=robot.Engine.Robot!;var mood=new MoodState(Model());mood.AttachRobot(robot);mood.BindNativeLifetime(native,0);
        int unregistered=0;native.ActionList.LifetimeObservedForTest=operation=>{if(operation=="unregister")unregistered++;};
        mood.DestroyForRobot();Assert.True(mood.NativeRetired);Assert.Equal(0,mood.ActionCallbackId);Assert.Equal(0,unregistered);
    }

}
