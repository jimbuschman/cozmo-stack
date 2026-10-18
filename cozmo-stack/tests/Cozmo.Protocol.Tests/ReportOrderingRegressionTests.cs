using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Vision;
using Xunit;
namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class ReportOrderingRegressionTests
{
    private sealed class Fixture:IDisposable
    {
        internal readonly M4ControlTests.Rig Rig=new();
        internal readonly string Root=Directory.CreateTempSubdirectory("report-order-").FullName;
        internal readonly VisionSystem Vision;
        internal CozmoRobot Robot=>Rig.Robot;
        internal Fixture()
        {
            Rig.ToSynced();Rig.State();Vision=new(Robot,CameraCalibration.Nominal()){Enabled=false};
            Directory.CreateDirectory(Path.Combine(Root,"animations"));
            Directory.CreateDirectory(Path.Combine(Root,"animationGroups"));
            Directory.CreateDirectory(Path.Combine(Root,"animationGroupMaps"));
            File.WriteAllText(Path.Combine(Root,"animations","a.json"),"{\"a\":[{\"Name\":\"EventKeyFrame\",\"triggerTime_ms\":5000,\"event_id\":\"TAPPED_BLOCK\"}]}");
            File.WriteAllText(Path.Combine(Root,"animationGroups","a.json"),"{\"Animations\":[{\"Name\":\"a\",\"Weight\":1,\"Mood\":\"Default\",\"CooldownTime_Sec\":0}]}");
            File.WriteAllText(Path.Combine(Root,"animationGroupMaps","AnimationTriggerMap.json"),"{\"Pairs\":[{\"CladEvent\":\""+((AnimationTrigger)0x207)+"\",\"AnimName\":\"a\"}]}");
            Robot.Animations.ManualTicking=true;Robot.Animations.LoadFrom(Root);
        }
        internal TriggerLiftSafeAnimationAction Start()
        {
            var action=new TriggerLiftSafeAnimationAction(Robot,new((AnimationTrigger)0x207,1,false,0,60f,false),_=>{});
            Assert.Equal(0,Rig.Engine.Robot!.ActionList.QueueAction(QueueActionPosition.Now,action));
            Rig.Tick();Assert.NotEqual((byte)0,Robot.Animations.CurrentTag);return action;
        }
        public void Dispose(){Vision.Dispose();Rig.Dispose();Directory.Delete(Root,true);}
    }

    // reach: production — real ActionList queue/tick Init, received D4, synchronous Cancel/Abort and FIFO host delivery.
    // The queue request and fake transport are inputs; no direct action Init, Broadcast, or app publication seam.
    [Fact]
    public void CliffStopPublishesAnimationAbortBeforeRobotStoppedAndCompletionHasAHostView()
    {
        using var f=new Fixture();var order=new List<string>();var completed=new List<ActionCompletionReport>();
        f.Rig.Engine.EngineToGameMessage+=(tag,_)=>{if(tag==0x5F)order.Add("5F");};
        f.Robot.Sensors.RobotStopped+=()=>order.Add("stopped");f.Rig.Engine.ActionCompleted+=completed.Add;
        var action=f.Start();int mark=f.Rig.Mark();
        f.Rig.Data(new RobotStopped{Field0=1});f.Rig.Tick();
        Assert.Equal(new[]{"5F","stopped"},order);
        Assert.Contains(f.Rig.RawSince(mark),p=>p[0]==0x8D);
        f.Rig.Tick();Assert.Contains(completed,r=>r.Tag==action.Tag);
    }

    // reach: production — queue/tick action Init, real CA/D5/object-tap packet handlers in one tick, public host observers.
    [Fact]
    public void AnimationEventArrivesBeforeALaterCubeTap()
    {
        using var f=new Fixture();M4ControlTests.ConnectCube(f.Rig);f.Robot.Cubes.SetBlockTapFilterEnabled(false);
        f.Start();byte tag=f.Robot.Animations.CurrentTag;var order=new List<string>();
        f.Rig.Engine.AnimationEvent+=_=>order.Add("60");f.Robot.Cubes.TapBroadcast+=_=>order.Add("tap");
        f.Rig.Data(new AnimationStarted{AnimId=tag});
        f.Rig.Data(new Cozmo.Protocol.AnimationEvent{Timestamp=123,EventId=(AnimEvent)tag,Field2=2});
        f.Rig.Data(new ObjectTapped{ObjectID=0,Timestamp=124,TapPos=100,TapNeg=0});
        f.Rig.Tick();Assert.Equal(new[]{"60","tap"},order);
    }
}
