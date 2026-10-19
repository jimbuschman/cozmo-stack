using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Cozmo.Transport;
using Xunit;
namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class M5X20BuilderTests
{
    private static string Assets(string group="missing")
    {
        var root=Directory.CreateTempSubdirectory("b520-assets-").FullName;
        Directory.CreateDirectory(Path.Combine(root,"animationGroupMaps"));Directory.CreateDirectory(Path.Combine(root,"animations"));Directory.CreateDirectory(Path.Combine(root,"animationGroups"));
        File.WriteAllText(Path.Combine(root,"animations","a.json"),"{\"a\":[]}");
        File.WriteAllText(Path.Combine(root,"animationGroupMaps","AnimationTriggerMap.json"),System.Text.Json.JsonSerializer.Serialize(new{
            Pairs=new[]{new{CladEvent=((AnimationTrigger)0x95).ToString(),AnimName=group}}
        }));return root;
    }
    private static DrivingAnimationRequest Request(int loops=1,int timeoutBits=0x42700000)
        =>new((AnimationTrigger)0x95,loops,true,0,BitConverter.Int32BitsToSingle(timeoutBits),false);

    [Theory] [InlineData(true)] [InlineData(false)]
    public void OnlyEmptyStoredGroupWarnsWhenTheActionCannotChooseAName(bool storedEmpty)
    {
        var root=Assets(storedEmpty?"":"missing");
        try
        {
            using var robot=CozmoRobot.CreateOffline();robot.Animations.LoadFrom(root);
            var entries=new List<EngineLogEntry>();robot.Engine.LogEntry+=entries.Add;
            var action=new TriggerLiftSafeAnimationAction(robot,Request(),_=>{});
            Assert.Equal(0x0300000C,action.Init());
            var warnings=entries.Where(e=>e.Name=="TriggerAnimationAction.NoAnimationForTrigger").ToArray();
            Assert.Equal(storedEmpty?1:0,warnings.Length);
            if(storedEmpty)Assert.Equal((EngineLogLevel.Warning,(string?)null),(warnings[0].Level,warnings[0].Channel));
        }
        finally{Directory.Delete(root,true);}
    }

    [Theory]
    [InlineData(0,0x42700000,0x7F7FFFFF)]
    [InlineData(1,0x42700000,0x42700000)]
    [InlineData(-1,0x42700000,0x42700000)]
    [InlineData(0,0x426FFFFF,0x426FFFFF)]
    [InlineData(0,0x42700001,0x42700001)]
    [InlineData(0,0x7FC12345,0x7FC12345)]
    [InlineData(0,0,0)]
    public void ForeverTimeoutChangesOnlyAnExactlyDefaultBinary32Timeout(int loops,int bits,int expected)
    {
        using var robot=CozmoRobot.CreateOffline();
        var action=new TriggerLiftSafeAnimationAction(robot,Request(loops,bits),_=>{});
        Assert.Equal(expected,BitConverter.SingleToInt32Bits(action.TimeoutSeconds));
    }

    [Theory]
    [InlineData("[]")] [InlineData("1")]
    public void NonObjectTriggerMapKeepsPriorMapAndStillLoadsClips(string malformed)
    {
        var root=Assets();
        try
        {
            using var robot=CozmoRobot.CreateOffline();robot.Animations.LoadFrom(root);var prior=robot.Animations.DrivingTriggerMap;
            File.WriteAllText(Path.Combine(root,"animationGroupMaps","AnimationTriggerMap.json"),malformed);
            var stack=new List<string>();robot.Engine.StackLogLine+=stack.Add;
            var loaded=robot.Animations.LoadFrom(root);
            Assert.Contains("a",loaded.ClipNames);Assert.Same(prior,robot.Animations.DrivingTriggerMap);
            Assert.Single(stack.Where(s=>s.Contains("M1-034 / M7-002: skipping malformed AnimationTriggerMap.json")));
        }
        finally{Directory.Delete(root,true);}
    }

    [Fact]
    public void ConcreteActionGapIsReportedOncePerDriveEvenWithRepeatedInitTags()
    {
        using var robot=CozmoRobot.CreateOffline();var handler=robot.Animations.DrivingAnimations;var log=new List<string>();
        robot.Engine.StackLogLine+=log.Add;
        handler.Init(0,99,false,false);handler.PlayDrivingLoopAnim();handler.PlayDrivingLoopAnim();handler.PlayDrivingLoopAnim();
        Assert.Single(log.Where(s=>s.Contains("M8-021/M8-005 concrete TriggerLiftSafeAnimationAction")));
        handler.Init(0,99,false,false);handler.PlayDrivingLoopAnim();
        Assert.Equal(2,log.Count(s=>s.Contains("M8-021/M8-005 concrete TriggerLiftSafeAnimationAction")));
    }

    // reach: unit (offline robot and trigger map inputs; persistence is supplied at engine initialization).
    [Fact]
    public void FreeplayDisposeRestoresThePreviousDrivingTriggerInput()
    {
        var root=Assets();
        try
        {
            using var robot=CozmoRobot.CreateOffline(engineOptions:new CozmoEngineOptions{NeedsDeviceDirectory=root});var previous=AnimationTriggerMap.Load(root);robot.Animations.DrivingTriggerMap=previous;
            Func<bool> previousNeeds=()=>false;robot.Animations.DrivingAnimations.AreNeedsMet=previousNeeds;
            File.WriteAllText(Path.Combine(root,"animationGroupMaps","AnimationTriggerMap.json"),"{\"Pairs\":[]}");var replacement=AnimationTriggerMap.Load(root);
            var context=new BehaviorContext{Robot=robot,Triggers=replacement};
            var obb=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../../re-analysis/obb"));
            using(var stack=FreeplayStack.Create(obb,robot,context,()=>0,withReactions:false,needsDirectory:root))
                Assert.Same(replacement,robot.Animations.DrivingTriggerMap);
            Assert.Same(previous,robot.Animations.DrivingTriggerMap);
            Assert.Same(previousNeeds,robot.Animations.DrivingAnimations.AreNeedsMet);
        }
        finally{Directory.Delete(root,true);}
    }

    private sealed record UnknownSegment:PathSegment;
    [Theory] [InlineData(false)] [InlineData(true)]
    public void UnknownSegmentSetsErrorAndKeepsTheLastGoodIndex(bool firstValid)
    {
        using var rig=new M4ControlTests.Rig();rig.ToSynced();rig.State();var path=rig.Robot.Motion.Path;
        var entries=new List<EngineLogEntry>();rig.Engine.LogEntry+=entries.Add;var sent=new List<RobotMessage>();path.SendPathMessage=m=>{sent.Add(m);return true;};
        var line=new PathSegment.Line(0,0,1,0,100,200,500);bool previous=EngineErrorState.ErrorFlagSet;EngineErrorState.ErrorFlagSet=false;
        try
        {
            path.SetDolerPath(firstValid?new PathSegment[]{line,new UnknownSegment(),line}:new PathSegment[]{new UnknownSegment(),line});
            Assert.Equal(firstValid?(ushort)0:(ushort)0xFFFF,path.LastDoledIndex);Assert.Equal(firstValid?1:0,sent.Count);
            var error=Assert.Single(entries.Where(e=>e.Name=="Invalid path segment"));
            Assert.Equal((EngineLogLevel.Error,(string?)null),(error.Level,error.Channel));Assert.EndsWith("Can't send path segment of unknown type",error.Text);
            Assert.True(EngineErrorState.ErrorFlagSet);
        }
        finally{EngineErrorState.ErrorFlagSet=previous;}
    }

    // reach: unit (robot-owned vision instance with direct frame/helper or fixture inputs; full native producer is not claimed).
    [Theory] [InlineData(false)] [InlineData(true)]
    public void HostStraightFailedStartReturnsTheNativeInitFailure(bool failExecute)
    {
        using var rig=new M4ControlTests.Rig();var vision=rig.Robot.Vision; vision.Calibration=CameraCalibration.Nominal(); vision.Enabled=false;
        rig.ToSynced();rig.State();using var m=new ManipulationSystem(rig.Robot,vision);
        rig.Robot.Motion.Path.SendPathMessage=message=>failExecute?message is not ExecutePath:message is not ClearPath;
        var tick=new HostDriveStraightTick(m,100,100);Assert.Equal(0u,tick.Init());
        Assert.Equal(0x03000016u,tick.CheckIfDone());
    }
}
