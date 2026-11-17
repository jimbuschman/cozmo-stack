using System.Text.Json;
using Cozmo.Bridge;
using Cozmo.Conformance;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Xunit;
using Xunit.Abstractions;
using Fixture=Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class KeepNeedsFullTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FifteenMinutesOfConfiguredFreeplayUsesTheSelectedNeedsMode(bool full)
    {
        string directory=Directory.CreateTempSubdirectory("cozmo-full-needs-").FullName;
        try
        {
            long initial=NativeRewardCanonicalTests.InitialSeconds()+20,wall=initial*1_000_000;
            var options=NativeRewardCanonicalTests.SourceOptions(directory,()=>wall,1,0);
            byte[] stored=NativeRewardCanonicalTests.StoredBlob(.3f,.3f,.3f,0,0,initial);
            var reports=new List<byte[]>();
            var modeLog=new List<string>();
            using var f=new Fixture(preloadAnimations:true,pumpAnimations:true,engineOptions:options,sourceNvEmpty:true,
                sourceNvStored:tag=>tag==NeedsManager.NeedsNvKey?stored:null,keepNeedsFull:full,
                engineCreated:engine=>
                {
                    engine.EngineToGameMessage+=(tag,bytes)=>{if(tag==0x63)reports.Add(bytes.ToArray());};
                    engine.StackLogLine+=modeLog.Add;
                });
            var engine=f.Rig.Engine;var needs=engine.Needs;
            Assert.True(needs.NativeLevelsLoaded);
            Assert.Equal(full,engine.GetNeedsStatus().KeepFull);
            Assert.Equal(full,needs.IsPaused);
            Assert.Contains(modeLog,line=>line==$"Host needs mode: {(full?"full":"real")} (M15-112)");
            if(full)Assert.Equal(new[]{1f,1f,1f},Levels(engine));
            else Assert.All(Levels(engine),level=>Assert.InRange(level,.03f,.3f));
            var chosen=new List<string>();
            var manager=f.Robot.BehaviorManagerOwner!;
            var previous=manager.DasTransition;
            manager.DasTransition=next=>{previous?.Invoke(next);chosen.Add(next.Id);};
            double start=engine.Timer.Seconds,nextAction=start+30;int mark=f.Rig.Mark();byte active=0;
            f.Rig.Robot.SetEnableFreeplayActivity(true);
            while(engine.Timer.Seconds-start<900)
            {
                wall=initial*1_000_000+(long)((engine.Timer.Seconds-start)*1_000_000);
                f.AnswerInFlightNv();
                foreach(var message in f.Rig.SentSince(mark))
                {
                    if(message is StartOfAnimation begin){active=begin.AnimId;f.Rig.Data(new AnimationStarted{AnimId=active});}
                    else if(message is EndOfAnimation)f.Rig.Data(new AnimationEnded{AnimId=active});
                }
                mark=f.Rig.Mark();
                if(engine.Timer.Seconds>=nextAction)
                {
                    needs.RegisterNeedsActionCompleted("Fall"); // shipped needs_action_config: action deltas are also suppressed by global pause.
                    nextAction+=30;
                }
                f.Motors();
            }
            engine.DrainAppDelivery();
            Assert.NotEmpty(chosen);
            bool low=chosen.Any(id=>id.StartsWith("Needs_MildLow",StringComparison.Ordinal)||id.StartsWith("Needs_Severe",StringComparison.Ordinal));
            output.WriteLine($"mode={(full?"full":"real")}; simulated={engine.Timer.Seconds-start:F2}s; choices={chosen.Count}; lowNeeds={low}; levels={string.Join(',',Levels(engine))}");
            if(full)
            {
                Assert.False(low,string.Join(", ",chosen));
                Assert.Equal(new[]{1f,1f,1f},Levels(engine));
                Assert.NotEmpty(reports);
                Assert.All(reports,body=>Assert.Equal(new byte[]{1},body));
                // A valid v5 robot copy needs no rewrite. C6/C9 introduce no robot needs writes.
                Assert.DoesNotContain(f.Rig.SentSince(0).OfType<NVCommand>(),c=>c.Tag==NeedsManager.NeedsNvKey&&c.Op==NvStorageComponent.OpWrite);
                using var device=JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,"needsState.json")));
                Assert.Equal(100000,device.RootElement.GetProperty("CurNeedLevel").GetProperty("Energy").GetInt32());
            }
            else
            {
                Assert.True(low,string.Join(", ",chosen));
                Assert.Contains(Levels(engine),level=>level<.3f);
                Assert.Empty(reports);
            }
        }
        finally{Directory.Delete(directory,true);}
    }

    [Fact]
    public async Task BridgeCanResumeNativeNeedsAndForceLevelsWithTheNativeClamp()
    {
        string directory=Directory.CreateTempSubdirectory("cozmo-needs-tools-").FullName;
        try
        {
            var options=NativeRewardCanonicalTests.SourceOptions(directory,()=>NativeRewardCanonicalTests.FixedClockMicros+20_000_000,1,0);
            using var f=new Fixture(engineOptions:options,sourceNvEmpty:true,keepNeedsFull:true);
            using var bridge=new CozmoBridge(f.Rig.Robot);
            Assert.True(f.Rig.Engine.GetNeedsStatus().Applied);
            var pauses=new List<byte[]>();
            f.Rig.Engine.DrainAppDelivery();
            f.Rig.Engine.EngineToGameMessage+=(tag,body)=>{if(tag==0x63)pauses.Add(body.ToArray());};
            var rejected=await BridgeCommandTests.Invoke(bridge,"set_needs","""{"repair":0.5,"energy":0.6,"play":0.7}""");
            Assert.False(rejected.GetProperty("admitted").GetBoolean());
            var mode=await BridgeCommandTests.Invoke(bridge,"set_needs_mode","""{"mode":"real"}""");
            Assert.Equal("confirmed",mode.GetProperty("status").GetString());
            Assert.False(mode.GetProperty("report").GetProperty("paused").GetBoolean());
            var result=await BridgeCommandTests.Invoke(bridge,"set_needs","""{"repair":0,"energy":0.6,"play":0.7}""");
            Assert.Equal(new[]{(float)f.Rig.Engine.Needs.Config.MinimumNeedLevel,.6f,.7f},Levels(f.Rig.Engine));
            Assert.Equal(JsonValueKind.Null,result.GetProperty("engineResult").ValueKind);
            Assert.Equal("confirmed",result.GetProperty("status").GetString());
            var full=await BridgeCommandTests.Invoke(bridge,"set_needs_mode","""{"mode":"full"}""");
            Assert.True(full.GetProperty("report").GetProperty("applied").GetBoolean());
            Assert.Equal(new[]{1f,1f,1f},Levels(f.Rig.Engine));
            f.Rig.Engine.DrainAppDelivery();
            Assert.Equal(new[]{"00","01"},pauses.Select(body=>Convert.ToHexString(body)));
        }
        finally{Directory.Delete(directory,true);}
    }

    [Fact]
    public void PersonalityOverrideResumesAndThenRestoresFullMode()
    {
        string directory=Directory.CreateTempSubdirectory("cozmo-needs-window-").FullName;
        try
        {
            var options=NativeRewardCanonicalTests.SourceOptions(directory,()=>NativeRewardCanonicalTests.FixedClockMicros+20_000_000,1,0);
            using var f=new Fixture(engineOptions:options,sourceNvEmpty:true,keepNeedsFull:true);
            var step=Assert.Single(PersonalityHw.SelectSteps("express-needs"));
            step.Needs!.Value.WithOverride(f.Rig.Engine,()=>
            {
                Assert.False(f.Rig.Engine.GetNeedsStatus().KeepFull);
                Assert.False(f.Rig.Engine.Needs.IsPaused);
                Assert.Equal(NeedBracketId.Warning,f.Rig.Engine.Needs.State.GetNeedBracket(NeedId.Energy));
            });
            Assert.True(f.Rig.Engine.GetNeedsStatus().Applied);
            Assert.Equal(new[]{1f,1f,1f},Levels(f.Rig.Engine));
        }
        finally{Directory.Delete(directory,true);}
    }

    [Fact]
    public void PendingOrUnusableStoredNeedsAreNotForcedOrPaused()
    {
        using var f=new Fixture(keepNeedsFull:true);
        var status=f.Rig.Engine.GetNeedsStatus();
        Assert.True(status.KeepFull);
        Assert.False(status.Loaded);
        Assert.False(status.Applied);
        Assert.False(status.Paused);
        Assert.Throws<InvalidOperationException>(()=>f.Rig.Engine.SetNeedsLevels(.5f,.5f,.5f));
    }

    [Fact]
    public void HardwareNeedsArgumentScopesTheHostSettingAndRestoresDefault()
    {
        Assert.True(new CozmoEngineOptions().KeepNeedsFull);
        string directory=Directory.CreateTempSubdirectory("cozmo-needs-args-").FullName;
        try
        {
            string[] args=["m15-cube-hw","--needs","real","--validate"];
            using(HardwareEngineOptions.UseNeedsArguments(ref args))
            {
                Assert.Equal(new[]{"m15-cube-hw","--validate"},args);
                Assert.False(NativeRewardCanonicalTests.SourceOptions(directory,()=>0,1,0).KeepNeedsFull);
                Assert.False(EngineStartupResources.OptionsForRoot(PickupPyramidNativeExecutionTests.Resources()).KeepNeedsFull);
            }
            Assert.True(NativeRewardCanonicalTests.SourceOptions(directory,()=>0,1,0).KeepNeedsFull);
            Assert.True(EngineStartupResources.OptionsForRoot(PickupPyramidNativeExecutionTests.Resources()).KeepNeedsFull);
            string[] invalid=["m15-cube-hw","--needs","guessed"];
            Assert.Throws<ArgumentException>(()=>HardwareEngineOptions.UseNeedsArguments(ref invalid));
        }
        finally{Directory.Delete(directory,true);}
    }

    private static float[] Levels(CozmoEngine engine)=>Enum.GetValues<NeedId>().Select(n=>(float)engine.Needs.State.GetNeedLevel(n)).ToArray();
}
