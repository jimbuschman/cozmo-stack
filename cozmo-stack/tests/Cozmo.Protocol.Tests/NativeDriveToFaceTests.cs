using System.Net;
using Cozmo.Conformance;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;
using Fixture=Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class NativeDriveToFaceTests
{
    private static NativeDriveToFaceBehavior Behavior(Fixture f)=>
        Assert.IsType<NativeDriveToFaceBehavior>(f.Runtime.Bound["VC_ComeHere"]);

    private static void See(Fixture f,float x,int id=-7,float y=0,float z=60)
    {
        var point=new Vec3(x,y,z);
        var face=new TrackedFace(new DetectedFace(id,new FaceRect(100,60,70,80),new Vec2(110,80),new Vec2(140,80)),f.Robot.StoredState!.Timestamp)
        {
            HeadPose=new Pose3d(Rotation3d.Identity,point),
            NativeTrackingTranslation=point,NativeTrackingRotation=Rotation3d.Identity
        };
        // FaceWorld publishes/caches unnamed faces only after its first three observations (0x004F4956).
        for(int i=0;i<4;i++)
            Assert.NotNull(f.M.Vision.Faces.AddOrUpdateNativeFace(face,f.Robot.GetCheckedMainPose(),false,
                f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId).Node!,requireNativePose:true,expectedOwner:f.Robot));
        // VisionSystem's result delivery publishes 0x46 after FaceWorld prepares the entry/cache.
        f.Rig.Engine.AnimationMessages.Deliver(0x46,true,new NativeObservedFace(id,face.Timestamp));
    }
    private static void AdvanceTurn(Fixture f,float x)
    {
        for(int i=0;i<150 && Behavior(f).NativeState==0;i++)
        {f.Motors();See(f,x);f.Rig.Tick();}
        Assert.NotEqual(0,Behavior(f).NativeState);
    }
    private static void AcknowledgeAnimation(Fixture f,ref int mark)
    {
        foreach(var start in f.Rig.SentSince(mark).OfType<StartOfAnimation>().ToArray())
        {f.Rig.Data(new AnimationStarted{AnimId=start.AnimId});f.Rig.Data(new AnimationEnded{AnimId=start.AnimId});}
        mark=f.Rig.Mark();
        f.Motors();
    }

    [Fact]
    public void ConfiguredFarFaceDrivesToTwoHundredMillimetersAndReportsStartStop()
    {
        using var f=new Fixture(preloadAnimations:true,pumpAnimations:true,sourceNvEmpty:true);
        var behavior=Behavior(f);
        var run=new M15CubeRun("","",IPAddress.Loopback,"",[],[],false,false){Personality=true};
        run.SubscribeBehaviorReports(f.Rig.Engine);
        Assert.Null(Assert.Single(PersonalityHw.SelectSteps("drive-to-face")).Check!.Missing);
        Assert.False(PersonalityHw.TryRequestDriveToFace(f.Rig.Robot));
        See(f,500);
        Assert.True(PersonalityHw.TryRequestDriveToFace(f.Rig.Robot));f.Rig.Tick();
        Assert.Same(behavior,f.Robot.BehaviorManagerOwner!.Current);
        var turn=Assert.IsType<NativeLastFaceRunner>(behavior.OwnedRunner);
        Assert.Equal(-7,turn.RequestedId);Assert.False(turn.SayName);Assert.Equal(Radians.Pi,turn.MaximumTurn);
        AdvanceTurn(f,500);
        var drive=Assert.IsType<DriveStraightAction>(behavior.OwnedRunner);
        Assert.Equal(300,drive.DistanceMm);Assert.Equal(220,drive.SpeedMmps);Assert.True(drive.ShouldPlayAnimation);
        int mark=0;
        for(int i=0;i<80 && !f.Rig.SentSince(0).OfType<ExecutePath>().Any();i++)AcknowledgeAnimation(f,ref mark);
        var line=Assert.Single(f.Rig.SentSince(0).OfType<AppendPathSegmentLine>());
        // Init 0x0054762A..0x005476AA starts at the forward pose, not the main pose used by distance.
        float forwardOffset=(float)(f.Robot.GetCheckedForwardPose().Translation.X-f.Robot.GetCheckedMainPose().Translation.X);
        Assert.Equal(200,500-(line.XEndMm-forwardOffset),3);Assert.Equal(0,line.YEndMm,3);
        Assert.Equal(220,line.Speed.SpeedMmps);
        Assert.Equal(BitConverter.UInt32BitsToSingle(0x4326AAAB),line.Speed.DecelMmps2);
        Assert.Equal(BitConverter.UInt32BitsToSingle(0x4326AAAB),drive.DecelMmps2);
        var execute=Assert.Single(f.Rig.SentSince(0).OfType<ExecutePath>());
        f.Rig.Data(new PathFollowingEvent{EventId=execute.EventId,EventType=0});f.Rig.Tick();
        f.X=300;f.Motors();
        f.Rig.Data(new PathFollowingEvent{EventId=execute.EventId,EventType=2});
        for(int i=0;i<80 && behavior.NativeState!=3;i++){See(f,500);AcknowledgeAnimation(f,ref mark);}
        Assert.Equal("TrackFace",behavior.StateName);
        Assert.IsType<NativeTrackFaceRunner>(behavior.OwnedRunner);
        // Native strict now > deadline ends tracking and the configured behavior.
        See(f,500);f.Rig.Tick(4900);
        Assert.Same(behavior,f.Robot.BehaviorManagerOwner.Current);
        See(f,500);f.Rig.Tick(200);
        Assert.NotSame(behavior,f.Robot.BehaviorManagerOwner.Current);
        Assert.Equal(0,behavior.TargetId);
        f.Rig.Engine.DrainAppDelivery();
        Assert.Collection(run.BehaviorTransitions(BehaviorID.VC_ComeHere,0,double.MaxValue),
            start=>Assert.True(PersonalityHw.IsBehaviorStart(start,BehaviorID.VC_ComeHere)),
            stop=>Assert.True(PersonalityHw.IsBehaviorStop(stop,BehaviorID.VC_ComeHere)));
    }

    [Theory]
    [InlineData(199f)]
    [InlineData(200f)]
    public void CloseFaceUsesAlreadyHereInsteadOfAPath(float distance)
    {
        using var f=new Fixture(preloadAnimations:true,pumpAnimations:true,sourceNvEmpty:true);
        See(f,distance);Assert.True(PersonalityHw.TryRequestDriveToFace(f.Rig.Robot));f.Rig.Tick();
        AdvanceTurn(f,distance);
        var behavior=Behavior(f);
        Assert.Equal("AlreadyCloseEnough",behavior.StateName);
        var animation=Assert.IsType<TriggerAnimationAction>(behavior.OwnedRunner);
        Assert.Equal(new DrivingAnimationRequest(AnimationTrigger.ComeHere_AlreadyHere,1,true,0,60f,false),animation.Request);
        Assert.Empty(f.Rig.SentSince(0).OfType<AppendPathSegmentLine>());
        int mark=0;
        for(int i=0;i<100 && behavior.NativeState!=3;i++){See(f,distance);AcknowledgeAnimation(f,ref mark);}
        Assert.Equal(3,behavior.NativeState);
        f.Robot.BehaviorManagerOwner!.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);
        Assert.Equal(0,behavior.TargetId);Assert.Null(behavior.OwnedRunner);
    }

    [Fact]
    public void MostRecentSortedSmartIdTracksRemappingAndDeletionAndUsesNativeGeometry()
    {
        using var f=new Fixture(sourceNvEmpty:true);
        var behavior=Behavior(f);
        See(f,300,id:9,y:400,z:900);See(f,300,id:-7,y:400,z:900);
        Assert.True(behavior.IsRunnable(f.Runtime.Context));Assert.Equal(-7,behavior.TargetId);
        Assert.True(behavior.TryDistance(out float distance));Assert.Equal(500,distance,3);
        Assert.True(f.M.Vision.Faces.ChangeFaceID(-7,-8));Assert.Equal(-8,behavior.TargetId);
        f.Rig.Engine.AnimationMessages.Deliver(0x4B,true,new NativeDeletedFace(-8));
        Assert.Equal(0,behavior.TargetId);
        // Invalid Init has the native failure result, without a fabricated target or path.
        using var scope=new BehaviorScope();
        behavior.StartAsync(f.Runtime.Context,scope,CancellationToken.None).GetAwaiter().GetResult();
        Assert.True(behavior.InitFailed);Assert.Null(behavior.OwnedRunner);
    }

    [Fact]
    public void InterruptedTurnCannotStartADriveAndCanRestart()
    {
        using var f=new Fixture(sourceNvEmpty:true);
        See(f,500);Assert.True(PersonalityHw.TryRequestDriveToFace(f.Rig.Robot));f.Rig.Tick();
        var behavior=Behavior(f);var turn=Assert.IsType<NativeLastFaceRunner>(behavior.OwnedRunner);
        f.Robot.BehaviorManagerOwner!.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);
        Assert.True(turn.DestructionObserved);Assert.Equal(0,behavior.TargetId);
        f.Rig.Tick(1100);See(f,500);Assert.True(PersonalityHw.TryRequestDriveToFace(f.Rig.Robot));f.Rig.Tick();
        Assert.NotSame(turn,Assert.IsType<NativeLastFaceRunner>(behavior.OwnedRunner));
        Assert.Empty(f.Rig.SentSince(0).OfType<AppendPathSegmentLine>());
    }
}
