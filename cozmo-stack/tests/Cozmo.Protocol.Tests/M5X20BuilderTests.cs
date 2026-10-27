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

    // reach: unit (direct loader/streamer/helper or authored fixture inputs; complete robot entry and native producers are not claimed).
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
    public void ConcreteLoopActionsReportRealNoAnimationCompletionAcrossReinitialization()
    {
        using var robot=CozmoRobot.CreateOffline();var handler=robot.Animations.DrivingAnimations;
        var reports=new List<RobotCompletedAction>();
        using var subscription=robot.Engine.AnimationMessages.SubscribeMessage(0x5A,true,p=>reports.Add(Assert.IsType<RobotCompletedAction>(p)));
        for(int pass=0;pass<2;pass++)
        {
            Assert.True(handler.Init(0,99,false,false));
            var ids=new List<uint>();
            for(int i=0;i<3;i++){handler.PlayDrivingLoopAnim();ids.Add(handler.LoopId);}
            Assert.All(ids,id=>Assert.NotEqual(0u,id));Assert.Equal(3,ids.Distinct().Count());
            Assert.Equal(3,robot.Engine.Robot!.ActionList.QueueCount); // each source Play call queues its concrete action
            for(int tick=0;tick<10&&robot.Engine.Robot.ActionList.QueueCount!=0;tick++)robot.Engine.Robot.ActionList.Update();
            Assert.Equal(0,robot.Engine.Robot.ActionList.QueueCount);
            uint end=handler.EndId;Assert.NotEqual(0u,end);Assert.DoesNotContain(end,ids);
            Assert.Equal(ids.Append(end),reports.Skip(pass*4).Select(report=>report.Tag));
            foreach(uint id in ids.Append(end))
            {
                var report=Assert.Single(reports.Where(r=>r.Tag==id));
                Assert.Equal(26,report.ActionType);Assert.Equal(0x0300000Cu,report.Result);
                Assert.Equal((byte)1,report.CompletionVariant);Assert.Equal("",report.AnimationName);
            }
        }
        Assert.Equal(8,reports.Count);
    }

    // reach: unit (offline robot and trigger map inputs; persistence is supplied at engine initialization).
    [Fact]
    public void StandaloneCustomTriggerInputNeverReplacesCanonicalDrivingInput()
    {
        var root=Assets();
        try
        {
            using var robot=CozmoRobot.CreateOffline(engineOptions:new CozmoEngineOptions{NeedsDeviceDirectory=root});
            robot.Animations.ManualTicking=true;robot.Animations.ClockMs=()=>0;
            File.WriteAllText(Path.Combine(root,"animations","manual.json"),"{\"manual_a\":[{\"Name\":\"HeadAngleKeyFrame\",\"triggerTime_ms\":0,\"durationTime_ms\":10000,\"angle_deg\":5,\"angleVariability_deg\":0}]}",System.Text.Encoding.UTF8);
            File.WriteAllText(Path.Combine(root,"animationGroups","manual.json"),"{\"Animations\":[{\"Name\":\"manual_a\",\"Weight\":1,\"Mood\":\"Default\"}]}",System.Text.Encoding.UTF8);
            robot.Animations.LoadFrom(root);var previous=robot.Animations.DrivingTriggerMap!;
            Func<bool> previousNeeds=()=>false;robot.Animations.DrivingAnimations.AreNeedsMet=previousNeeds;
            File.WriteAllText(Path.Combine(root,"animationGroupMaps","AnimationTriggerMap.json"),System.Text.Json.JsonSerializer.Serialize(new { Pairs=new[]{new{CladEvent=((AnimationTrigger)0x95).ToString(),AnimName="manual"}}}),System.Text.Encoding.UTF8);var replacement=AnimationTriggerMap.Load(root);
            var context=new BehaviorContext{Robot=robot,Triggers=replacement};
            var obb=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../../re-analysis/obb"));
            using(var stack=FreeplayStack.Create(obb,robot,context,()=>0,withReactions:false,needsDirectory:root))
            {
                Assert.Same(replacement,stack.Context.Triggers);
                Assert.Same(previous,robot.Animations.DrivingTriggerMap);
                Assert.Same(previousNeeds,robot.Animations.DrivingAnimations.AreNeedsMet);
                var behavior=new PlayAnimBehavior("manual-trigger-probe","PlayAnim",new[]{(AnimationTrigger)0x95});
                try
                {
                    behavior.StartAsync(stack.Context,new BehaviorScope(),default).GetAwaiter().GetResult();
                    robot.Animations.Scheduler.Advance(0);
                    Assert.Contains(behavior.Trace,line=>line=="play "+((AnimationTrigger)0x95)+" -> manual_a");
                    Assert.True(behavior.HasCurrentAction);Assert.Equal("manual_a",robot.Animations.Scheduler.Playing);
                    Assert.Same(previous,robot.Animations.DrivingTriggerMap);Assert.Same(previousNeeds,robot.Animations.DrivingAnimations.AreNeedsMet);
                }
                finally{behavior.Stop(BehaviorStopReason.Interrupted);Assert.True(behavior.AnimationCallbackCompletion.Wait(TimeSpan.FromSeconds(5)));}
            }
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
