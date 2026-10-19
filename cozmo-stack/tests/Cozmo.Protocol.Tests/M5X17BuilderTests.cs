using Cozmo.Protocol;
using Cozmo.Transport;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;
namespace Cozmo.Protocol.Tests;
[Collection("M5 process statics")]
public sealed class M5X17BuilderTests
{
    private sealed class SourceRig:IDisposable
    {
        internal readonly M4ControlTests.Rig Rig=new();internal readonly VisionSystem Vision;internal readonly ManipulationSystem M;
        internal SourceRig(){Rig.Engine.InitOfflineLink(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback,5551));Vision=new(Rig.Robot,CameraCalibration.Nominal()){Enabled=false};M=new(Rig.Robot,Vision);}
        public void Dispose(){M.Dispose();Vision.Dispose();Rig.Dispose();}
    }
    private sealed class Handler:IDrivingAnimationHandler
    {
        internal List<string> Calls=new();public int State{get;set;}public bool InputsReady=>true;internal bool EndWait;
        internal (byte,uint,bool,bool) Arguments;
        public bool Init(byte mask,uint tag,bool suppress,bool repeat){Calls.Add("Init");Arguments=(mask,tag,suppress,repeat);State=0;return true;}
        public void PlayStartAnim()=>Calls.Add("Start");
        public uint PlayEndAnim(){Calls.Add("End");if(State is 3 or 4 or 5)return 0;if(EndWait){State=3;return 1;}return 0;}
        public void ActionIsBeingDestroyed(){Calls.Add("Destroy");State=5;}
    }
    private static DriveToPoseTick Tick(int status,Pose3d? pose=null,ushort sent=1,ushort received=1,sbyte segment=-1)=>new(status,0,pose??Pose3d.Identity,67.7,sent,received,segment);
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void InitRunsBeforeNoGoalFailureWithExactArgumentsAndCleanupOrder()
    {
        using var r=new SourceRig();var h=new Handler();using var d=new DriveToPoseAction(r.M){DrivingAnimations=h,RequiredTrackMask=7,Tag=123,SuppressTrackLocking=true};
        Assert.Equal((int)ActionResult.BadPose,d.Init());Assert.Equal((byte)7,h.Arguments.Item1);Assert.Equal(123u,h.Arguments.Item2);Assert.True(h.Arguments.Item3);Assert.False(h.Arguments.Item4);
        Assert.Equal(0,h.State);var order=new List<string>();d.LifecycleCallObserved=order.Add;r.M.PathIsActiveForTest=()=>true;r.M.ErasePlannerObstaclesForTest=_=>{};d.AbortPath=()=>{};
        d.Dispose();Assert.Equal(new[]{"Abort","ErasePlanner:True","ErasePlanner:False","DestroyDriving"},order);Assert.Equal("Destroy",h.Calls.Last());
    }
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void EndStateHoldsAndFreshPoseAndStatusDetermineTheResultAfterCompletion()
    {
        using var r=new SourceRig();var h=new Handler{EndWait=true};using var d=new DriveToPoseAction(r.M){DrivingAnimations=h,Goal=Pose3d.Identity};
        Assert.Equal(ActionResult.Running,d.CheckIfDone(Tick(4)));Assert.Equal(3,h.State);
        Assert.Equal(ActionResult.Running,d.CheckIfDone(Tick(0)));Assert.Equal(1,h.Calls.Count(c=>c=="End"));
        h.State=4;Assert.Equal(ActionResult.PathPlanningFailedAbort,d.CheckIfDone(Tick(0)));Assert.Equal(1,h.Calls.Count(c=>c=="End"));
        Assert.Equal(ActionResult.Success,d.CheckIfDone(Tick(4)));Assert.Equal(2,h.Calls.Count(c=>c=="End"));
        h.State=0;Assert.Equal(ActionResult.Running,d.CheckIfDone(Tick(4,new Pose3d(Mat3.Identity,new Vec3(200,0,0)))));
        h.State=4;Assert.Equal(ActionResult.Success,d.CheckIfDone(Tick(4))); // No stored failed-arrival result.
    }
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void FollowingCallsStartEveryTickAndNativeOldCounterModuloLogsOneAndEleven()
    {
        using var r=new SourceRig();var h=new Handler();using var d=new DriveToPoseAction(r.M){DrivingAnimations=h,Tag=unchecked((uint)-3)};var entries=new List<EngineLogEntry>();r.Rig.Engine.LogEntry+=entries.Add;
        for(int i=0;i<11;i++)Assert.Equal(ActionResult.Running,d.CheckIfDone(Tick(3,sent:65535,received:9,segment:-5)));
        Assert.Equal(11,h.Calls.Count(c=>c=="Start"));Assert.Equal(11,d.FollowingPathTicks);
        var texts=entries.Where(e=>e.Name=="DriveToPoseAction.CheckIfDone.WaitingForPathCompletion").Select(e=>e.Text).ToArray();Assert.Equal(2,texts.Length);
        Assert.EndsWith("[-3] Waiting for robot to complete its path traversal (1), _currPathSegment=-5, _lastSentPathID=65535, _lastRecvdPathID=9.",texts[0]);
        Assert.Contains("traversal (11)",texts[1]);
    }
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void RealStartSelectsStartOrLoopOnlyFromStateZeroAndQueuesExactRequest()
    {
        using var r=new SourceRig();var h=r.Rig.Robot.Animations.DrivingAnimations;var requests=new List<DrivingAnimationRequest>();
        h.CreateActionForTest=req=>{requests.Add(req);return new Probe();};h.Push(new((AnimationTrigger)0x95,(AnimationTrigger)0x91,(AnimationTrigger)0x8D),"fixture");
        Assert.True(h.Init(4,100,false,false));h.PlayStartAnim();Assert.Equal(1,h.State);Assert.NotEqual(0u,h.StartId);
        var first=Assert.Single(requests);Assert.Equal((AnimationTrigger)0x95,first.Trigger);Assert.Equal(1,first.Argument2);Assert.True(first.Argument3);Assert.Equal(0,first.Argument4);Assert.Equal(0x42700000,BitConverter.SingleToInt32Bits(first.Argument5));Assert.False(first.Argument6);
        h.PlayStartAnim();Assert.Single(requests);h.Push(new((AnimationTrigger)0x23F,(AnimationTrigger)0x91,(AnimationTrigger)0x23F),"loop-fixture");h.Init(4,101,true,true);h.PlayStartAnim();Assert.Equal(2,h.State);Assert.NotEqual(0u,h.LoopId);
        Assert.Equal((AnimationTrigger)0x91,requests[1].Trigger);Assert.Equal(2,r.Rig.Engine.Robot!.ActionList.QueueCount);
    }
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void LoopCompletionDoubleUnlockResendsEnableTracksAndNamesPrintBoundary()
    {
        using var r=new M4ControlTests.Rig();r.ToSynced();r.State();var h=r.Robot.Animations.DrivingAnimations;h.Push(new((AnimationTrigger)0x23F,(AnimationTrigger)0x91,(AnimationTrigger)0x8D),"fixture");// No queued animation fixture: synchronous Cancel/completion re-entrance is the explicitly excluded M8-015 question.
        h.CreateActionForTest=_=>new Probe();
        r.Engine.Robot!.ActionList.Watcher.HasExternalInterface=false; // Component fixture: inject one loop completion below, suppress queue-generated re-entry.
        h.HasPathToFollowForTest=()=>false;h.Init(4,101,false,false);h.PlayStartAnim();
        r.Robot.Motion.LockTracks(4,"101","DriveToPose");int mark=r.Mark();var lines=new List<string>();var entries=new List<EngineLogEntry>();r.Engine.StackLogLine+=lines.Add;r.Engine.LogEntry+=entries.Add;
        h.HandleActionCompleted(h.LoopId,0x02000000); // cancelled loop: avoids the unbuilt zero-ID start completion.
        Assert.Equal(2,r.RawSince(mark).Count(bytes=>bytes[0]==(byte)RobotMessageId.EnableAnimTracks));
        Assert.Contains(entries,e=>e.Name=="MovementComponent.UnlockTracks"&&e.Text.EndsWith("Tracks 0x4 are not currently locked by 101"));Assert.DoesNotContain(lines,l=>l.StartsWith("MISSING: M4-047"));
    }
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void TimeoutBypassesTailAndRetryReinitializesTheSameSharedHandler()
    {
        using var r=new SourceRig();r.Rig.Tick(1000);var h=new Handler();using var timeout=new DriveToPoseAction(r.M){DrivingAnimations=h,Goal=Pose3d.Identity,Initialized=true,StartTime=0,State=EngineActionResult.Running,TimeoutSeconds=0};
        Assert.Equal(EngineActionResult.Timeout,timeout.Update());Assert.DoesNotContain("End",h.Calls);
        using var retry=new DriveToPoseAction(r.M){DrivingAnimations=h,Goal=Pose3d.Identity,RetriesRemain=1,SuppressTrackLocking=true,TimeoutSeconds=100};
        r.M.DriveTickForTest=_=>Tick(4,new Pose3d(Mat3.Identity,new Vec3(100,0,0)));
        Assert.Equal(EngineActionResult.Running,retry.Update());Assert.Equal(1,h.Calls.Count(c=>c=="Init"));
        Assert.Equal((uint)ActionResult.FailedTraversingPath,retry.Update());Assert.Equal(2,h.Calls.Count(c=>c=="Init"));
    }
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void StraightStartsOnceSkipsEndOnPathFailureAndSucceedsAfterEnd()
    {
        using var r=new SourceRig();var h=new Handler{EndWait=true};using var d=new DriveStraightAction(r.M,10){DrivingAnimations=h};d.Init();
        Assert.Equal(ActionResult.Running,d.CheckIfDone(new(false,true,true)));Assert.Equal(ActionResult.Running,d.CheckIfDone(new(false,true,true)));Assert.Equal(1,h.Calls.Count(c=>c=="Start"));
        Assert.Equal(ActionResult.FailedTraversingPath,d.CheckIfDone(new(true,true,false)));Assert.DoesNotContain("End",h.Calls);
        Assert.Equal(ActionResult.Running,d.CheckIfDone(new(false,true,false)));h.State=4;Assert.Equal(ActionResult.Success,d.CheckIfDone(new(false,true,false)));
    }
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void TrackInitRepeatsAndStoredResultAndUnconditionalDestructionMatchT1T3()
    {
        using var r=new SourceRig();var h=new Handler();using var track=new TrackFaceAction(r.Vision,1){DrivingHandlerForTest=h,DrivingAnimation=true};
        track.InitDrivingLifecycle(5,99,true,0);Assert.True(h.Arguments.Item4);Assert.Contains("Start",h.Calls);Assert.Equal(0x02000001u,track.StoredDrivingResult);
        Assert.Equal(0x01000000u,track.FinishWithDrivingAnimation(0x03000008));h.State=3;Assert.Equal(0x01000000u,track.DrivingCompletionGate());h.State=4;Assert.Equal(0x03000008u,track.DrivingCompletionGate());
        track.DrivingAnimation=false;track.Dispose();Assert.Equal("Destroy",h.Calls.Last());
    }
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void ActivityClockStrategyAndBroadcasterPrecedeGatedDrivingAndIdleMissingCalls()
    {
        var a=new Activity{Id="a",Strategy=new ActivityStrategy(),DriveStartAnim=(AnimationTrigger)0x95,IdleAnim=(AnimationTrigger)0x91};var calls=new List<string>();
        a.OnSelected(7,line=>{Assert.Equal(7,a.SelectedAtSec);Assert.Equal(7,a.Strategy.LastStartedSec);calls.Add(line);});
        Assert.Contains("UpdateActivity",calls[0]);Assert.Contains("PushDrivingAnimations",calls[1]);Assert.Contains("SmartPushIdleAnimation",calls[2]);
        calls.Clear();a.OnDeselected(8,calls.Add);Assert.Contains("RemoveIdleAnimation",calls[0]);Assert.Contains("RemoveDrivingAnimations",calls[1]);
        var none=new Activity{Id="none",Strategy=new ActivityStrategy(),DriveStartAnim=(AnimationTrigger)0x23F,IdleAnim=(AnimationTrigger)0x23F};calls.Clear();none.OnSelected(9,calls.Add);none.OnDeselected(10,calls.Add);Assert.Single(calls);Assert.Contains("UpdateActivity",calls[0]);
    }
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void NativeFailureAndPlanningTimeoutUseSourceKeysAndUnequalIdsSetTheErrorFlag()
    {
        using var r=new SourceRig();var h=new Handler();using var d=new DriveToPoseAction(r.M){DrivingAnimations=h,Goal=Pose3d.Identity};
        var entries=new List<EngineLogEntry>();r.Rig.Engine.LogEntry+=entries.Add;d.AbortPath=()=>{};
        Assert.Equal(ActionResult.PathPlanningFailedAbort,d.CheckIfDone(Tick(0)));
        Assert.Contains(entries,e=>e.Level==EngineLogLevel.Info&&e.Channel=="Unnamed"&&e.Name=="DriveToPoseAction.CheckIfDone.Failure"&&e.Text.EndsWith("Robot driving to pose failed"));
        d.CheckIfDone(Tick(1));
        Assert.Equal(ActionResult.PathPlanningFailedAbort,d.CheckIfDone(new DriveToPoseTick(1,4-0.00000001,Pose3d.Identity,67.7,1,1)));
        Assert.Contains(entries,e=>e.Name=="DriveToPoseAction.CheckIfDone.ComputingPathTimeout"&&e.Text.EndsWith("Robot has been planning for more than 4.000000 seconds, aborting"));
        bool oldFlag=EngineErrorState.ErrorFlagSet;try
        {
            EngineErrorState.ErrorFlagSet=false;
            Assert.Equal(ActionResult.FollowingPathButNotTraversing,d.CheckIfDone(Tick(4,new Pose3d(Mat3.Identity,new Vec3(200,0,0)),sent:1,received:2)));
            Assert.True(EngineErrorState.ErrorFlagSet);
            Assert.Contains(entries,e=>e.Level==EngineLogLevel.Error&&e.Name=="DriveToPoseAction.CheckIfDone.Failure"&&e.Text.EndsWith("Robot is not at the goal and did not receive the last path"));
        }
        finally{EngineErrorState.ErrorFlagSet=oldFlag;}
    }
    private sealed class Probe:ActionRunner{internal Probe():base(0,0){}public override uint CheckIfDone()=>EngineActionResult.Running;public override bool CanInterrupt()=>false;}
}
