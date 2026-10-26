using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Transport;
using Xunit;
namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class M5X21BuilderTests
{
    private sealed class Fixture : IDisposable
    {
        internal readonly string Root=Directory.CreateTempSubdirectory("b521-").FullName;
        internal readonly M4ControlTests.Rig Rig=new();
        internal string Map=>Path.Combine(Root,"animationGroupMaps","AnimationTriggerMap.json");
        internal readonly List<EngineLogEntry> Logs=new();internal readonly List<string> Stack=new();
        private readonly bool _oldError=EngineErrorState.ErrorFlagSet;
        internal Fixture()
        {
            Directory.CreateDirectory(Path.Combine(Root,"animations"));
            Directory.CreateDirectory(Path.Combine(Root,"animationGroupMaps"));
            File.WriteAllText(Path.Combine(Root,"animations","a.json"),"{\"a\":[]}");
            File.WriteAllText(Map,"{\"Pairs\":[{\"CladEvent\":\"DriveStartHappy\",\"AnimName\":\"g\"}]}");
            Rig.Robot.Animations.LoadFrom(Root);
            Rig.Engine.LogEntry+=Logs.Add;Rig.Engine.StackLogLine+=Stack.Add;EngineErrorState.ErrorFlagSet=false;
        }
        internal void Load(string json){File.WriteAllText(Map,json);Assert.Contains("a",Rig.Robot.Animations.LoadFrom(Root).ClipNames);}
        public void Dispose(){Rig.Dispose();Directory.Delete(Root,true);EngineErrorState.ErrorFlagSet=_oldError;}
    }

    // reach: unit (direct loader/streamer/helper or authored fixture inputs; complete robot entry and native producers are not claimed).
    [Fact]
    public void ReadAsJsonFalseEmptiesMapAndUsesItsOwnErrorFlagWithoutCrashSkip()
    {
        using var f=new Fixture();f.Load("{");
        Assert.NotNull(f.Rig.Robot.Animations.DrivingTriggerMap);Assert.Equal(0,f.Rig.Robot.Animations.DrivingTriggerMap.Count);
        var log=Assert.Single(f.Logs.Where(l=>l.Name=="DataPlatform.readAsJson"));
        Assert.Equal(EngineLogLevel.Error,log.Level);Assert.Null(log.Channel);Assert.EndsWith($"Failed to read [{f.Map}]",log.Text);
        Assert.True(EngineErrorState.ErrorFlagSet);Assert.DoesNotContain(f.Stack,l=>l.Contains("M1-034"));
    }
    // reach: unit (direct loader/streamer/helper or authored fixture inputs; complete robot entry and native producers are not claimed).
    [Theory] [InlineData("{}")] [InlineData("null")]
    public void MissingPairsLeavesANonnullEmptyContainerSilently(string json)
    {
        using var f=new Fixture();var old=f.Rig.Robot.Animations.DrivingTriggerMap;f.Load(json);
        var map=f.Rig.Robot.Animations.DrivingTriggerMap;Assert.NotNull(map);Assert.NotSame(old,map);Assert.Equal(0,map.Count);
        Assert.False(EngineErrorState.ErrorFlagSet);Assert.DoesNotContain(f.Logs,l=>l.Level is EngineLogLevel.Error or EngineLogLevel.Warning);
        Assert.DoesNotContain(f.Stack,l=>l.Contains("M1-034"));
    }
    // reach: unit (direct loader/streamer/helper or authored fixture inputs; complete robot entry and native producers are not claimed).
    [Fact]
    public void IntegerCladEventUsesAsStringAndDoesNotBecomeAnEnumOrDiscardOtherPairs()
    {
        using var f=new Fixture();f.Load("{\"Pairs\":[{\"CladEvent\":1,\"AnimName\":\"numeric\"},{\"CladEvent\":\"DriveStartHappy\",\"AnimName\":\"after\"}]}");
        var map=f.Rig.Robot.Animations.DrivingTriggerMap!;Assert.Equal(2,map.Count);Assert.Contains("1",map.UnknownTriggers);
        Assert.Equal(2,f.Rig.Robot.Animations.Library!.Triggers.Count);
        Assert.Equal("after",f.Rig.Robot.Animations.Library.Triggers.GetResponse(AnimationTrigger.DriveStartHappy));
        Assert.Null(map.GroupFor((AnimationTrigger)1));Assert.Equal("after",map.GroupFor(AnimationTrigger.DriveStartHappy));
        Assert.False(EngineErrorState.ErrorFlagSet);Assert.DoesNotContain(f.Stack,l=>l.Contains("M1-034"));
    }
    // reach: unit (direct loader/streamer/helper or authored fixture inputs; complete robot entry and native producers are not claimed).
    [Theory] [InlineData("[]")] [InlineData("5")] [InlineData("true")]
    public void NonObjectIsMemberTerminationKeepsPriorMapWithVisiblePolicySkip(string json)
    {
        using var f=new Fixture();var prior=f.Rig.Robot.Animations.DrivingTriggerMap;f.Load(json);
        Assert.Same(prior,f.Rig.Robot.Animations.DrivingTriggerMap);
        Assert.Single(f.Stack.Where(l=>l.Contains("M1-034 / M7-002")&&l.Contains("0x006704A0")));
        Assert.DoesNotContain(f.Logs,l=>l.Name=="DataPlatform.readAsJson");Assert.False(EngineErrorState.ErrorFlagSet);
    }
    // reach: unit (direct loader/streamer/helper or authored fixture inputs; complete robot entry and native producers are not claimed).
    [Fact]
    public void AThrowingFreeplayLoadLeavesBothPreviousDrivingInputsUntouched()
    {
        using var f=new Fixture();
        // B-M7-4 unit setup: Freeplay now borrows native Robot+44; create the real owner first (M1-025,0x004ED07C).
        // Original fixture had no Robot. Keep the JsonException and both prior-input expectations unchanged.
        f.Rig.Engine.InvokeEngineRequest(()=>{f.Rig.Engine.Robots.AddRobot(CozmoEngine.RobotId,withRic:false);return 0;});
        var previous=f.Rig.Robot.Animations.DrivingTriggerMap;
        Func<bool> needsInput=()=>false;f.Rig.Robot.Animations.DrivingAnimations.AreNeedsMet=needsInput;
        var context=new BehaviorContext{Robot=f.Rig.Robot,Triggers=AnimationTriggerMap.Load(f.Map)};
        // MoodModel.Load is after the old assignments and before the new publication point.
        File.WriteAllText(Path.Combine(f.Root,"mood_config.json"),"{");
        Assert.ThrowsAny<System.Text.Json.JsonException>(()=>FreeplayStack.Create(f.Root,f.Rig.Robot,context,()=>0,
            needs:new NeedsManager(()=>0),withReactions:false));
        Assert.Same(previous,f.Rig.Robot.Animations.DrivingTriggerMap);
        Assert.Same(needsInput,f.Rig.Robot.Animations.DrivingAnimations.AreNeedsMet);
    }
}
