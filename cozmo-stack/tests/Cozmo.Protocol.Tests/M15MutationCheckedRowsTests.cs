using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class M15MutationCheckedRowsTests
{
    [Fact]
    public void SeededDamageUsesNativePickOrderAndStillConsumesTheRangeOnePair()
    {
        var rng=new EngineRandom(5489);var state=new NeedsState(NeedsConfig.Default){ContextRandom=rng};state.SetNeedLevel(NeedId.Repair,.1);
        var events=new List<(string Value,string Data)>();state.PartDamageLog=(value,data)=>events.Add((value,data["$data"]));
        state.DamagePartsNative(NeedsActionId.Decay);
        // Native PickPartToDamage + RandInt + GetNextDbl execution, MT5489's six first words: [0,2,1].
        Assert.Equal(new[]{("Head","1:Decay"),("Treads","2:Decay"),("Lift","3:Decay")},events);Assert.Equal(3,state.NumDamagedParts());
        // Native GetNextDbl on words7/8, verifies exactly six preceding words were consumed.
        Assert.Equal(unchecked((long)0x3FCC4AD7F0F4E980),BitConverter.DoubleToInt64Bits(rng.GetNextDbl()));
    }
    [Theory]
    [InlineData(0xFFFFFBFFu,2)][InlineData(0xFFFFFC00u,3)][InlineData(0xFFFFFFFFu,3)]
    public void NativeEndpointRoundsToRangeWithoutARetry(uint low,int expected)
    {
        var words=new Queue<uint>(new[]{low,uint.MaxValue});var rng=new EngineRandom(1){WordSourceForTest=()=>words.Dequeue()};
        // Executed native RandInt 0x0082FA74 with high word FFFFFFFF and range3.
        Assert.Equal(expected,rng.RandInt(3));Assert.Empty(words);
    }
    [Fact]
    public void NaNRepairCountsAllThresholdsAndActualDamageCountIsIndependentOfTheLevel()
    {
        var state=new NeedsState(NeedsConfig.Default){ContextRandom=new EngineRandom(5489)};
        state.SetNeedLevel(NeedId.Repair,double.NaN);Assert.Equal(3,state.NumDamagedPartsForRepairLevel(float.NaN));Assert.Equal(0,state.NumDamagedParts());
        state.DamagePartsNative(NeedsActionId.Decay);Assert.Equal(3,state.NumDamagedParts());
        state.SetNeedLevel(NeedId.Repair,1);Assert.Equal(3,state.NumDamagedParts());
    }
    [Fact]
    public void AlreadyDamagedOrNoCrossedThresholdConsumesNoRandomWords()
    {
        var rng=new EngineRandom(1){WordSourceForTest=()=>throw new Xunit.Sdk.XunitException("unexpected draw")};
        var state=new NeedsState(NeedsConfig.Default){ContextRandom=rng};state.DamagePartsNative(NeedsActionId.Decay);
        for(int i=0;i<3;i++)state.SetPartDamageForTest(i,1);
        state.SetNeedLevel(NeedId.Repair,.1);state.DamagePartsNative(NeedsActionId.Decay);Assert.Equal(3,state.NumDamagedParts());
    }
    [Fact]
    public void DecayInfoPrecedesStoreAndPartMutationPrecedesItsStructuredEvent()
    {
        var state=new NeedsState(NeedsConfig.Default){ContextRandom=new EngineRandom(5489)};var order=new List<string>();
        state.SourceLog=(level,channel,key,text)=>
        {
            Assert.Equal(EngineLogLevel.Info,level);Assert.Equal("NeedsSystem",channel);Assert.Equal("NeedsState.ApplyDecay",key);
            Assert.Equal("Decaying need index 0 with elapsed time of 60.000000 seconds",text);Assert.Equal(1,state.GetNeedLevel(NeedId.Repair));order.Add("info");
        };
        state.PartDamageLog=(value,data)=>{Assert.Equal(.75,state.GetNeedLevel(NeedId.Repair));Assert.Equal((byte)1,state.PartDamage[0]);Assert.Equal("Head",value);Assert.Equal("1:Decay",data["$data"]);order.Add("part");};
        var rates=new Dictionary<NeedId,IReadOnlyList<(double,double)>>{{NeedId.Repair,new[]{(.75,.25),(.5,.125),(0.0,.125)}}};
        state.ApplyDecay(NeedId.Repair,new DecayConfig(rates,rates),60,true,1);
        Assert.Equal(new[]{"info","part"},order);
    }
    [Fact]
    public void EndpointInsertsKeyThreeAndNullKeyDeviceWriteSkipsWithoutChangingTheFile()
    {
        var cfg=NeedsConfig.Default with{BrokenPartThresholdList=new[]{.98}};
        var needs=new NeedsManager(()=>100,cfg);var words=new Queue<uint>(new[]{uint.MaxValue,uint.MaxValue});
        needs.DistortionRng=new EngineRandom(1){WordSourceForTest=()=>words.Dequeue()};needs.State.SetNeedLevel(NeedId.Repair,.5);
        needs.State.DamagePartsNative(NeedsActionId.Decay);Assert.Equal(4,needs.State.PartDamage.Count);Assert.Equal((byte)1,needs.State.PartDamage[3]);Assert.Empty(words);
        string root=Directory.CreateTempSubdirectory("m15-endpoint-").FullName,path=Path.Combine(root,"needs.json");
        try{File.WriteAllText(path,"prior");needs.Save(path);Assert.Equal("prior",File.ReadAllText(path));Assert.Equal(4,needs.State.PartDamage.Count);}
        finally{Directory.Delete(root,true);}
    }

    [Theory]
    [InlineData(NeedId.Energy,.5f,.5f,0,0x3F800000u,true)]
    [InlineData(NeedId.Repair,.5f,.1f,0,0x3F7AE1F0u,false)]
    [InlineData(NeedId.Repair,.5f,.5f,1,0x3F7AE0A0u,true)]
    [InlineData(NeedId.Repair,.2f,1f,3,0x3E99984Au,true)]
    [InlineData(NeedId.Repair,.7f,-.2f,0,0x3F000000u,false)]
    [InlineData(NeedId.Play,.5f,.1f,0,0x3F19999Au,false)]
    public void ApplyDeltaMatchesExecutedNativeCore(NeedId need,float old,float center,int damaged,uint bits,bool expected)
    {
        var state=new NeedsState(NeedsConfig.Default){ContextRandom=new EngineRandom(5489)};state.SetNeedLevel(need,old);
        for(int i=0;i<damaged;i++)state.SetPartDamageForTest(i,1);
        // Outputs from native 0x0069C8C8 with existing-node emplace and supplied count, real RandDbl/GetNextDbl.
        Assert.Equal(expected,state.ApplyDelta(need,center,0,NeedsActionId.Decay));Assert.Equal(bits,unchecked((uint)BitConverter.SingleToInt32Bits((float)state.GetNeedLevel(need))));
    }
    [Fact]
    public void ZeroVariationStillConsumesTwoWordsAndNaNDeltaSkipsPositiveConstraintsAndDamage()
    {
        int draws=0;var state=new NeedsState(NeedsConfig.Default){ContextRandom=new EngineRandom(1){WordSourceForTest=()=>{draws++;return 0;}}};
        state.SetNeedLevel(NeedId.Repair,.5);Assert.False(state.ApplyDelta(NeedId.Repair,float.NaN,0,NeedsActionId.Decay));
        Assert.Equal(2,draws);Assert.True(double.IsNaN(state.GetNeedLevel(NeedId.Repair)));Assert.Equal(0,state.NumDamagedParts());
    }
    [Fact]
    public void UpdateTailSendsNeedsBeforePossiblyWritingTheDevice()
    {
        double clock=0;var needs=new NeedsManager(()=>clock);var order=new List<string>();needs.NeedsStateSent+=_=>order.Add("game");needs.WriteToDevice=refresh=>{Assert.False(refresh);order.Add("device");};
        clock=100;needs.Update(100);Assert.Equal(new[]{"game","device"},order);
    }
    [Fact]
    public void RemovalEmitsTheSinglePerfInfoBeforeTheSignedRobotDestructorEvent()
    {
        using var robot=CozmoRobot.CreateOffline();var engine=robot.Engine;var native=engine.Robot!;var order=new List<string>();
        engine.RobotDisconnected+=_=>order.Add("app");engine.ClearDasGlobal=key=>{if(key=="$session_id")order.Add("global");};
        engine.NeedsRobotDisconnected=()=>order.Add("needs");engine.DasPauseUploading=paused=>{Assert.False(paused);order.Add("das");};native.Lifetime.Bind(-3,_=>order.Add("abort"));
        var perf=new List<EngineLogEntry>();engine.LogEntry+=entry=>
        {
            if(entry.Name=="PerfMetric.DumpFiles"){perf.Add(entry);order.Add("perf");}
            if(entry.Name=="robot.destructor"){Assert.Equal("1",entry.Text);Assert.Equal(EngineLogLevel.Event,entry.Level);Assert.Null(entry.Channel);Assert.Empty(entry.Data);order.Add("destructor");}
        };
        engine.Robots.RemoveRobot(1,false);Assert.Equal(new[]{"app","global","needs","perf","das","destructor","abort"},order);
        var info=Assert.Single(perf);Assert.Equal(EngineLogLevel.Info,info.Level);Assert.Equal("PerfMetric",info.Channel);Assert.Equal("Nothing to dump; buffer is empty",info.Text);Assert.Empty(info.Data);
    }
    [Fact]
    public void AiDirectSlotsAreNulledBeforeRecipientsInTheCheckedOrder()
    {
        var ai=new AIComponent(()=>1);var order=new List<int>();int[] offsets={0x30,0x2C,0x28,0x24,0x20,0x1C,0x18,0x14,0x10,0xC,0x8};
        foreach(int offset in offsets)ai.BindDestructionRecipient(offset,()=>{Assert.True(ai.IsChildRetired(offset));order.Add(offset);});
        ai.DestroyDirect();Assert.Equal(offsets,order);ai.DestroyDirect();Assert.Equal(offsets,order);
    }
    [Fact]
    public void LiveStackForcesTheNativeTimerTrackerBeforeAbortAndDoesNotForceOnHostDispose()
    {
        using var robot=CozmoRobot.CreateOffline();
        var directory=new DirectoryInfo(AppContext.BaseDirectory);
        while(directory is not null&&!Directory.Exists(Path.Combine(directory.FullName,"re-analysis","obb")))directory=directory.Parent;
        var obb=Path.Combine(directory!.FullName,"re-analysis","obb");
        Assert.True(Directory.Exists(obb));
        var stack=FreeplayStack.Create(obb,robot,new BehaviorContext{Robot=robot,Triggers=new AnimationTriggerMap()},()=>99,needs:new NeedsManager(()=>0),withReactions:false);
        foreach(var flag in stack.DataTracker.PausedFlags.ToArray())stack.DataTracker.SetFreeplayPauseFlag(flag,false);
        var order=new List<string>();
        robot.Engine.LogEntry+=entry=>{if(entry.Name=="robot.destructor")order.Add("destructor");if(entry.Name=="FreeplayDataTracker.SendData.Accumulate")order.Add("accumulate");if(entry.Name=="robot.active_freeplay_time"){Assert.Equal("2",entry.Text);order.Add("event");}};
        robot.Engine.Robot!.Lifetime.Bind(-3,_=>order.Add("abort"));
        robot.Engine.Timer.UpdateTime(2_000_000_000);
        robot.Engine.Robots.RemoveRobot(1,false);
        Assert.Equal(new[]{"destructor","accumulate","event","abort"},order);
        Assert.True(stack.AI.IsChildRetired(0x2C));
        stack.Dispose();Assert.Equal(4,order.Count);
    }
    [Fact]
    public void DevicePartFlagsUseTheActualSelectedMapNotNeedThresholdCounts()
    {
        var needs=new NeedsManager(()=>0);needs.State.SetPartDamageForTest(2,1);
        string root=Directory.CreateTempSubdirectory("m15-flags-").FullName,path=Path.Combine(root,"needs.json");
        try
        {
            needs.Save(path);using var doc=System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));var parts=doc.RootElement.GetProperty("PartIsDamaged");
            Assert.False(parts.GetProperty("Head").GetBoolean());Assert.False(parts.GetProperty("Lift").GetBoolean());Assert.True(parts.GetProperty("Treads").GetBoolean());
            Assert.True(doc.RootElement.TryGetProperty("TimeLastDisconnect",out _));Assert.False(doc.RootElement.TryGetProperty("TimeLastDisconnected",out _));
        }
        finally{Directory.Delete(root,true);}
    }

}
