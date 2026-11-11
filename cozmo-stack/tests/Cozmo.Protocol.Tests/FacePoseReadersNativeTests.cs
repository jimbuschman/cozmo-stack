using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Vision;
using Xunit;
using Fixture=Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

// Job FACE-POSE-READERS-NATIVE-1: the readers of the tracked-face pose read the native entry pose
// (entry+0xF4, parented to the observation origin), never the flat host projection.
[Collection("SteppedBehavior missing-report statics")]
public sealed class FacePoseReadersNativeTests
{
    private sealed class FaceDetector : IFaceDetector
    {
        public bool IsAvailable => true;
        public string Description => "supplied face geometry";
        public IReadOnlyList<DetectedFace> Detect(GrayImage image, uint timestamp) =>
            new[] { new DetectedFace(7, new FaceRect(110,65,58,64), new Vec2(126,91), new Vec2(151,94),
                Name: "Ada", RollRad: .25) { NativeNewEntryRotatingTooFast = false } };
    }

    private static void EnableFaces(Fixture f)
    {
        var vision=f.M.Vision;
        vision.FaceDetector=new FaceDetector();vision.Enabled=true;vision.ModeEnableMask=1<<2;
        vision.Schedules=AllVisionModesSchedule.InitDefaultSchedules();
        f.Rig.Robot.SetVisionWhileMoving(true);f.Rig.Tick();
    }

    private static void Frame(Fixture f,uint id)
    {
        f.Rig.State(RobotStatusFlag.HeadInPos|RobotStatusFlag.LiftInPos,origin:f.Robot.CurrentOriginId,head:f.Head);
        f.NativeCameraFrame(id,150);f.Rig.Tick(); // real F2 worker and EngineRobot.Update drain
    }

    // M13-014. reach: production for the face (a stock queued F2 frame through the worker, the FaceWorld update and the
    // real local 0x46 delivery) and for the runner construction (the 3-argument constructor NativeFindFacesSocialize.cs,
    // NativeNeedsGetInBehavior.cs and NativeExpressNeedsBehavior.cs use, requested id 0). reach: unit for the admission:
    // the runner is queued directly on the ActionList instead of by one of those behaviours.
    // Expected, from the engine: with an invalid requested id the RobotObservedFace handler (0x0054C050) takes
    // GetFace(int) (in-origin gate 0x004F59B6..0x004F59C2), entry+0xF4 with respect to Robot::GetPose, and verifies the
    // face when x*x+y*y+z*z is strictly below +0x184 (0x0054C096..0x0054C114). A verified face makes
    // CreateFineTuneAction register needs action 0x2E (SeeFace, 0x0054C2E0) and build a TurnTowardsPoseAction from
    // entry+0xF4 with respect to Robot::GetPose (0x0054C2D4..0x0054C39C).
    [Fact]
    public void OwnerlessConstructionVerifiesAStockQueuedFaceAndFineTunes()
    {
        using var f=new Fixture(sourceNvEmpty:true);
        EnableFaces(f);f.Head=.1f;
        Frame(f,300);
        var entry=Assert.IsType<FaceEntry>(f.M.Vision.Faces.GetFace(7));
        Assert.NotNull(entry.NativeTrackingTranslation);
        var needs=new List<string>();f.Rig.Engine.Needs.Log+=needs.Add;
        var turn=new NativeLastFaceRunner(f.M,f.Robot,0);
        Assert.False(turn.SayName);Assert.Equal(0,turn.RequestedId);
        f.Robot.ActionList.QueueAction(QueueActionPosition.Now,turn);
        try
        {
            f.Rig.Tick();
            Assert.InRange(turn.FaceState,0,1); // Init found the cached last face (0x0054BD9E..0x0054BDB4): not the no-pose state 3; the handler runs while state <= 1
            Frame(f,301);                   // the stock 0x46 for face 7 (named: the 0x004F4956..0x004F496E gate passes)
            Assert.Equal(7,turn.VerifiedId);
            for(int i=0;i<40&&turn.FaceState<2&&!turn.DestructionObserved;i++)f.Motors();
            Assert.Equal(2,turn.FaceState);
            Assert.Single(needs.Where(line=>line.StartsWith("needs.action_completed SeeFace:")));
            Assert.IsType<NativeTurnTowardsPoseRunner>(turn.ChildForTest);
            for(int i=0;i<40&&!turn.DestructionObserved;i++)f.Motors();
            Assert.True(turn.DestructionObserved);Assert.Equal(0u,turn.State);
            Assert.True(entry.TurnedTowards);
        }
        finally{f.Robot.ActionList.Clear();f.Rig.Engine.Needs.Log-=needs.Add;}
    }

    // M10-003. reach: production for the strategy (the instance the robot's BehaviorManager builds from the shipped
    // reaction map). reach: unit for its entry and the faces: the stack feeds the strategy from the host FaceObserved
    // event (the 0x46 recipient join is a recorded MISSING), and each native entry is supplied directly with a chosen
    // translation instead of coming from a camera frame, so the distances are exact.
    // Expected, from the engine: the handler runs only on tag 0x46 (0x0060CF70), which AddOrUpdateFace broadcasts only
    // for an entry seen more than 3 times or named (0x004F4956..0x004F496E), so an unnamed face's first three
    // observations never reach it. It measures ComputeDistanceBetween(Robot::GetPose, entry+0xF4) (0x0060CC28..
    // 0x0060CC32) in f32 (0x008477A4..0x008477BC); a face in the close map and not close becomes close when the f32
    // distance is strictly below 300.0f (vcmpe.f32 0x0060CD88..0x0060CD9A; 0x0060CF14..0x0060CF1C), which adds it to
    // the desired faces. A face not yet in the map never "becomes close" on that observation. The flat host
    // projection is given the opposite distances, so a reader of it produces the opposite desired set.
    [Fact]
    public void FacePositionStrategyMeasuresTheNativeEntryPoseAgainstTheRobotMainPose()
    {
        using var f=new Fixture(sourceNvEmpty:true);
        var manager=f.Robot.BehaviorManagerOwner!;
        var strategy=Assert.IsType<FacePositionUpdatedStrategy>(
            Assert.Single(manager.Reactions.Where(r=>r.Strategy.Trigger==ReactionTrigger.FacePositionUpdated)).Strategy);
        Assert.True(manager.IsReactionTriggerEnabled(ReactionTrigger.FacePositionUpdated));
        Assert.Equal(new Vec3(0,0,0),f.Robot.GetCheckedMainPose().Translation);
        var origin=f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId);
        uint time=10;
        void See(int id,Vec3 native,Vec3 flat)
        {
            var tracked=new TrackedFace(new DetectedFace(id,new FaceRect(0,0,10,10),new Vec2(1,1),new Vec2(2,1)),time++)
            {
                HeadPose=new Pose3d(Rotation3d.Identity,native),LegacyHeadPose=new Pose3d(Rotation3d.Identity,flat),
                NativeTrackingTranslation=native,NativeTrackingRotation=Rotation3d.Identity
            };
            Assert.NotNull(f.M.Vision.Faces.AddOrUpdateNativeFace(tracked,f.Robot.GetCheckedMainPose(),false,origin.Node!,true,requireNativePose:true,expectedOwner:f.Robot));
        }
        // The first three unnamed observations are below the 0x46 gate: near or far, they never reach the handler.
        void Ungated(int id,Vec3 native){for(int i=0;i<3;i++)See(id,native,native);}
        // Face 4: near on its first three observations (gated out), then far: never close, never desired.
        Ungated(4,new Vec3(100,0,0));
        Assert.DoesNotContain(4,strategy.DesiredFaces);
        See(4,new Vec3(500,0,0),new Vec3(500,0,0));
        Assert.DoesNotContain(4,strategy.DesiredFaces);
        // Face 5: native far (4th observation, enters the map) then near (flat projection near both times): becomes close.
        Ungated(5,new Vec3(500,0,0));
        See(5,new Vec3(500,0,0),new Vec3(100,0,0));
        Assert.DoesNotContain(5,strategy.DesiredFaces);
        See(5,new Vec3(200,0,0),new Vec3(100,0,0));
        Assert.Contains(5,strategy.DesiredFaces);
        // Face 6: native far both times (flat projection far then near): it never becomes close.
        Ungated(6,new Vec3(500,0,0));
        See(6,new Vec3(500,0,0),new Vec3(500,0,0));
        See(6,new Vec3(450,0,0),new Vec3(100,0,0));
        Assert.DoesNotContain(6,strategy.DesiredFaces);
        // Face 8: the compare is strict. Exactly 300 mm is not close; 299 mm is.
        Ungated(8,new Vec3(500,0,0));
        See(8,new Vec3(500,0,0),new Vec3(500,0,0));
        See(8,new Vec3(300,0,0),new Vec3(500,0,0));
        Assert.DoesNotContain(8,strategy.DesiredFaces);
        See(8,new Vec3(299,0,0),new Vec3(500,0,0));
        Assert.Contains(8,strategy.DesiredFaces);
        // Face 9: the distance is f32. (148.63052368164062, 260.5934753417969, 0) is exactly 300.0f in f32 (not close),
        // though 299.99998660 in double (which would be close).
        Ungated(9,new Vec3(500,0,0));
        See(9,new Vec3(500,0,0),new Vec3(500,0,0));
        See(9,new Vec3(148.63052368164062,260.5934753417969,0),new Vec3(500,0,0));
        Assert.DoesNotContain(9,strategy.DesiredFaces);
    }
}
