using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Transport;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class SparkManagerInputCheckedRowsTests
{
    private static BehaviorContext Context(CozmoRobot r)=>new(){Robot=r,Triggers=new AnimationTriggerMap()};
    private static string Obb()
    {
        for(var d=new DirectoryInfo(AppContext.BaseDirectory);d is not null;d=d.Parent)
        {
            string root=Path.Combine(d.FullName,"re-analysis","obb");
            if(Directory.Exists(Path.Combine(root,"assets","cozmo_resources")))return root;
        }
        throw new DirectoryNotFoundException("shipped OBB fixture required");
    }

    [Theory]
    [InlineData(0x40,"96000040000000")]
    [InlineData(0x55,"96000055000000")]
    [InlineData(-1,"960000FFFFFFFF")]
    [InlineData(int.MinValue,"96000000000080")]
    public void AppEnvelopeHasNoNativePadding(int id,string expected)
        =>Assert.Equal(expected,Convert.ToHexString(new SparkManagerMessage(0,id).ToUnionBytes()));

    [Fact]
    public void ManagerInitializesAllCheckedSparkFields()
    {
        using var robot=CozmoRobot.CreateOffline();using var manager=new BehaviorManager(Context(robot));
        Assert.Equal(0x55,manager.ActiveSparkId);Assert.Equal(0x55,manager.RequestedSparkId);
        Assert.Equal((byte)0,manager.ActiveSparkSoft);Assert.Equal((byte)0,manager.RequestedSparkSoft);Assert.Equal((byte)0,manager.SparkCancelLatch);
        Assert.Equal(unchecked((int)0xBF800000),BitConverter.SingleToInt32Bits(manager.SparkSwitchTime));
        Assert.Equal(unchecked((int)0xBF800000),BitConverter.SingleToInt32Bits(manager.FreeplayStartTime));
    }

    [Fact]
    public void LiveAppApiQueuesFullWidthIDAndDoesNotPretendToSelectActivity()
    {
        SteppedBehavior.ResetMissingForTests(); // collection excludes concurrent users of this process-global deduplicator
        using var robot=CozmoRobot.CreateOffline();
        var missing=new List<string>();var keys=new List<string?>();robot.Engine.StackLogLine+=missing.Add;robot.Engine.LogEntry+=e=>keys.Add(e.Name);
        using var stack=FreeplayStack.Create(Obb(),robot,Context(robot),()=>0,withReactions:false);
        Assert.Equal(1,robot.Engine.AnimationMessages.Count(0x96,false));
        stack.ActivateSpark(int.MinValue); // offline Kick may drain the engine queue immediately
        robot.Engine.Tick();
        Assert.Equal(int.MinValue,stack.Manager.RequestedSparkId);Assert.Equal((byte)0,stack.Manager.RequestedSparkSoft);
        Assert.Equal(0x55,stack.Manager.ActiveSparkId);Assert.Null(stack.Freeplay.Inputs.RequestedSpark);
        Assert.Contains(missing,s=>s.Contains("native UnlockId EnumToString"));
        Assert.Contains(missing,s=>s.Contains("raw-ID Freeplay pick/switch"));
        Assert.Contains(missing,s=>s.Contains("M15-059")&&s.Contains("SparksTrackLaser"));
        Assert.DoesNotContain("BehaviorManager.SetRequestedSpark",keys); // no invented name
        stack.ActivateSpark(0x55);robot.Engine.Tick();Assert.Equal((byte)1,stack.Manager.SparkCancelLatch);
        stack.ActivateSpark(0x40);robot.Engine.Tick();Assert.Equal((byte)1,stack.Manager.SparkCancelLatch);
    }

    [Fact]
    public void SetterStoresBeforeInfoAndUnlockedCaseDoesNotTouchCancelLatch()
    {
        using var robot=CozmoRobot.CreateOffline();using var manager=new BehaviorManager(Context(robot));manager.AttachSparkInput();
        manager.SparkNameInput=id=>id==0x55?"count-input":"name-input";
        var logs=new List<string>();robot.Engine.LogEntry+=e=>{
            if(e.Name!="BehaviorManager.SetRequestedSpark")return;
            Assert.Equal(EngineLogLevel.Info,e.Level);Assert.Equal("Behaviors",e.Channel);Assert.Empty(e.Data);
            Assert.Equal(e.Text.Contains("soft")?(byte)1:(byte)0,manager.RequestedSparkSoft);logs.Add(e.Text);
        };
        robot.Engine.AnimationMessages.Deliver(0x96,false,new SparkManagerMessage(0,0x55));
        robot.Engine.AnimationMessages.Deliver(0x96,false,new SparkManagerMessage(1,0));
        Assert.Equal(0,manager.RequestedSparkId);Assert.Equal((byte)1,manager.RequestedSparkSoft);Assert.Equal((byte)1,manager.SparkCancelLatch);
        Assert.Equal(new[]{"requested hard spark is 'count-input'","requested soft spark is 'name-input'"},logs);
    }

    [Fact]
    public void CheckedSwitchInfoComesBeforeWritesAndRequestedFieldsRemain()
    {
        using var robot=CozmoRobot.CreateOffline();using var manager=new BehaviorManager(Context(robot));
        manager.SparkNameInput=id=>id==0x55?"old-input":"new-input";
        manager.HandleSparkManagerMessage(new(0,0x55));manager.SetNativeRequestedSpark(0x41,2);
        bool seen=false;robot.Engine.LogEntry+=e=>{
            if(e.Name!="BehaviorManager.SwitchToRequestedSpark")return;
            seen=true;Assert.Equal(0x55,manager.ActiveSparkId);Assert.Equal((byte)1,manager.SparkCancelLatch);
            Assert.Equal("switching active spark from 'old-input' -> 'new-input'. From hard -> soft",e.Text);
        };
        manager.SwitchToNativeRequestedSpark();Assert.True(seen);
        Assert.Equal(0x41,manager.ActiveSparkId);Assert.Equal(0x41,manager.RequestedSparkId);
        Assert.Equal((byte)2,manager.ActiveSparkSoft);Assert.Equal((byte)2,manager.RequestedSparkSoft);Assert.Equal((byte)0,manager.SparkCancelLatch);
        Assert.Equal(0,BitConverter.SingleToInt32Bits(manager.SparkSwitchTime)); // no engine tick: clock input is +0
    }

    [Fact]
    public void MissingSwitchNameKeepsEveryStateField()
    {
        using var robot=CozmoRobot.CreateOffline();using var manager=new BehaviorManager(Context(robot));
        manager.HandleSparkManagerMessage(new(0,0x55));manager.SetNativeRequestedSpark(3,1);manager.SwitchToNativeRequestedSpark();
        Assert.Equal(0x55,manager.ActiveSparkId);Assert.Equal((byte)0,manager.ActiveSparkSoft);Assert.Equal((byte)1,manager.SparkCancelLatch);
        Assert.Equal(unchecked((int)0xBF800000),BitConverter.SingleToInt32Bits(manager.SparkSwitchTime));
    }

    [Fact]
    public void BadTagErrorUsesExplicitNameResultThenErrG()
    {
        bool old=EngineErrorState.ErrorFlagSet;
        try
        {
            using var robot=CozmoRobot.CreateOffline();using var manager=new BehaviorManager(Context(robot));
            manager.SparkTagNameInput=_=>"tag-input";EngineErrorState.ErrorFlagSet=false;
            robot.Engine.LogEntry+=e=>{if(e.Name=="BehaviorManager.HandleEvent.UnhandledMessageUnionTag"){
                Assert.Equal(EngineLogLevel.Error,e.Level);Assert.Null(e.Channel);Assert.Empty(e.Data);
                Assert.Equal("Unexpected tag 254 'tag-input'",e.Text);Assert.False(EngineErrorState.ErrorFlagSet);
            }};
            manager.HandleSparkManagerMessage(new(254));Assert.True(EngineErrorState.ErrorFlagSet);
        }
        finally{EngineErrorState.ErrorFlagSet=old;}
    }

    [Fact]
    public void UnbuiltOtherCasesKeepStateAndReportOnlyOnStackChannel()
    {
        using var robot=CozmoRobot.CreateOffline();using var manager=new BehaviorManager(Context(robot));
        var missing=new List<string>();var logs=new List<string?>();robot.Engine.StackLogLine+=missing.Add;robot.Engine.LogEntry+=e=>logs.Add(e.Name);
        manager.HandleSparkManagerMessage(new(2));manager.HandleSparkManagerMessage(new(3));manager.HandleSparkManagerMessage(new(255));
        Assert.Equal(0x55,manager.RequestedSparkId);Assert.Equal(0x55,manager.ActiveSparkId);
        Assert.Contains(missing,s=>s.Contains("AddInventoryAmount(1,-3)"));Assert.Contains(missing,s=>s.Contains("game-request behavior"));
        Assert.Contains(missing,s=>s.Contains("BehaviorManagerMessageUnionTagToString"));Assert.Empty(logs);
    }

    [Fact]
    public void HostDisposeRetiresTheActualInputSubscription()
    {
        using var robot=CozmoRobot.CreateOffline();var manager=new BehaviorManager(Context(robot));manager.AttachSparkInput();
        manager.Dispose();Assert.Equal(0,robot.Engine.AnimationMessages.Count(0x96,false));
        robot.Engine.AnimationMessages.Deliver(0x96,false,new SparkManagerMessage(0,3));Assert.Equal(0x55,manager.RequestedSparkId);
    }
}
