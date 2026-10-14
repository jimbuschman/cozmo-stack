using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;
namespace Cozmo.Protocol.Tests;
[Collection("M5 process statics")]
public sealed class M5X19BuilderTests
{
    private sealed class DriveBehavior(ManipulationSystem m):ManipulationBehavior("drive-regression","drive-regression",m)
    {
        internal ActionResult? Result;
        protected override void OnStart()=>RunAction("DriveToPose",ct=>new DriveToPoseAction(M){Goal=new Pose3d(Mat3.Identity,new Vec3(150,0,0))}.RunAsync(ct),r=>{Result=r;Finish();});
    }
    private sealed class Pending:ActionRunner
    { internal Pending():base(0,0){} public override uint CheckIfDone()=>EngineActionResult.Running; public override bool CanInterrupt()=>false; }
    [Fact]
    public void BehaviorDriveWithDefaultInputsPlaysItsEndAnimationThenReturnsSuccess()
    {
        using var signals=SignalTestContext.Install();using var rig=new Rig();
        rig.M.DrivingAnimationsForTest=null;rig.M.ErasePlannerObstaclesForTest=null;
        // Start the real engine runner against the offline firmware, including its normal streamer tick.
        // No manual animation advance, pushed triplet, animation factory, status or timeout input.
        rig.Robot.Engine.StartProduction();
        var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../../re-analysis/obb/assets/cozmo_resources/assets"));
        Assert.True(Directory.Exists(root),"This production-input regression requires the shipped animation assets.");
        rig.Robot.Animations.LoadFrom(root);
        var handler=rig.Robot.Animations.DrivingAnimations;
        Assert.Empty(handler.Entries);Assert.Null(handler.CreateActionForTest);Assert.Null(handler.GetSimpleMoodForTest);Assert.Null(handler.AreNeedsMetForTest);
        var endCompletions=new List<uint>();
        rig.Robot.Engine.Robot!.ActionList.Watcher.RobotCompletedActionBroadcast+=r=>{if(r.Tag==handler.EndId && r.Tag!=0){endCompletions.Add(r.Result);Assert.Equal(4,handler.State);}};
        var behavior=new DriveBehavior(rig.M){ActionTaskRunner=SignalTestContext.Schedule};
        var context=new BehaviorContext{Robot=rig.Robot,Triggers=rig.Robot.Animations.DrivingTriggerMap!};
        behavior.StartAsync(context,new BehaviorScope(),default).GetAwaiter().GetResult();
        int bytesPlayed=0,audioPlayed=0;byte animTag=0;
        for(int i=0;i<1000 && behavior.Result is null;i++)
        {
            behavior.Update(context,rig.Clock.NowMs/1000);
            foreach(var message in rig.Pump(3))
            {
                byte wireTag=(byte)message.Id;
                if(wireTag is >=0x90 and <=0x9B || message is Cozmo.Protocol.AudioSilence)bytesPlayed+=message.ToBytes().Length;
                if(message is Cozmo.Protocol.AudioSample or Cozmo.Protocol.AudioSilence or Cozmo.Protocol.EndOfAnimation)audioPlayed++;
                if(message is Cozmo.Protocol.StartOfAnimation start)animTag=start.ToBytes()[1];
            }
            // Firmware input, derived from delivered wire messages, replenishes the native stream budget.
            rig.Send(new Cozmo.Protocol.AnimationState{Timestamp=rig.T,NumAnimBytesPlayed=bytesPlayed,NumAudioFramesPlayed=audioPlayed,EnabledAnimTracks=7,Tag=animTag});
            SignalTestContext.AdvanceBehavior(behavior);
        }
        behavior.Stop(BehaviorStopReason.Completed);
        Assert.True(behavior.Result==ActionResult.Success,$"{behavior.Result}; end={string.Join(',',endCompletions)}; engineDriven={rig.Robot.Animations.EngineDriven}; gate={rig.Robot.AnimationStreamingOpen}; synced={rig.Robot.Engine.Robot?.TimeSynced}; ready={rig.Robot.Engine.Robot?.ReadyToStream}; ticking={rig.Robot.Animations.IsTicking}; log={string.Join('\n',rig.Log.TakeLast(10))}");Assert.Contains(0u,endCompletions);
        Assert.Contains(rig.Sent,m=>m is Cozmo.Protocol.StartOfAnimation); // actual existing streamer reached the wire
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public void QueueNowDeletesRunningDriveWithItsStoredNativeResult(bool straight)
    {
        using var signals=SignalTestContext.Install();using var rig=new Rig();rig.HoldPath=true;
        IActionRunner drive=straight?new DriveStraightAction(rig.M,100):new DriveToPoseAction(rig.M){Goal=new Pose3d(Mat3.Identity,new Vec3(150,0,0))};
        var result=straight?((DriveStraightAction)drive).RunAsync(default):((DriveToPoseAction)drive).RunAsync(default);
        rig.Pump();Assert.Equal(EngineActionResult.Running,drive.State);
        var queue=rig.Robot.Engine.Robot!.ActionList;var records=new List<RobotCompletedAction>();queue.Watcher.RobotCompletedActionBroadcast+=records.Add;
        queue.QueueAction(QueueActionPosition.AtEnd,new Pending());queue.QueueAction(QueueActionPosition.Now,new Pending());
        Assert.Equal(ActionResult.Running,SignalTestContext.Result(result));
        Assert.Contains(records,r=>r.Tag==drive.Tag && r.Result==0x01000000);
    }
    [Fact]
    public void MissingInputSelectionFallsThroughAndNonDriveMissingIsNotDeduplicated()
    {
        using var robot=CozmoRobot.CreateOffline();var h=robot.Animations.DrivingAnimations;var logs=new List<string>();robot.Engine.StackLogLine+=logs.Add;
        Assert.True(h.Init(0,1,false,false));Assert.Equal(new DrivingAnimations((AnimationTrigger)0x95,(AnimationTrigger)0x91,(AnimationTrigger)0x8D),h.Current);
        Assert.Single(logs.Where(l=>l.Contains("M7-026 GetSimpleMood")));Assert.Single(logs.Where(l=>l.Contains("M15-001 AreNeedsMet")));
        h.Init(0,2,false,false);Assert.Equal(2,logs.Count(l=>l.Contains("M7-026 GetSimpleMood")));
        robot.Engine.Robots.RemoveRobot(CozmoEngine.RobotId,false);logs.Clear();h.ActionIsBeingDestroyed();h.ActionIsBeingDestroyed();
        Assert.Equal(6,logs.Count(l=>l.Contains("ActionList.Cancel recipient absent")));
    }
}
