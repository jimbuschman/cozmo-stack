using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Vision;
using Cozmo.Transport;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("ObjectID process statics")]
public sealed class RobotStateHistoryAdmissionTests
{
    private sealed class Fixture:IDisposable
    {
        internal readonly M4ControlTests.Rig Rig=new();
        internal EngineRobot Robot=>Rig.Engine.Robot!;
        internal RobotStateHistory History=>Robot.PoseHistory;
        private readonly byte _gaps=RobotStateHistory.ConsecutiveGaps;
        private readonly double _timeout=RobotStateHistory.ConnectionTimeoutInMs;
        private readonly bool _error=EngineErrorState.ErrorFlagSet;
        internal Fixture(){RobotStateHistory.ConsecutiveGaps=0;RobotStateHistory.ConnectionTimeoutInMs=5000;Rig.ToSynced();Robot.OfflineSeamAcceptsAnyOrigin=false;}
        internal RobotState State(uint t,float x=0,uint? frame=null,uint? origin=null,ushort cliff=500,RobotStatusFlag flags=RobotStatusFlag.IsBodyAccMode)=>new()
        {
            Timestamp=t,PoseFrameId=frame??Robot.PoseFrameId,PoseOriginId=origin??Robot.CurrentOriginId,
            Pose=new RobotPose{X=x},HeadAngle=0.125f,LiftAngle=0.25f,BatteryVoltage=4,Status=(uint)flags,
            Accel=new AccelData{Z=9800},Gyro=new GyroData(),CliffDataRaw=new ushort[]{cliff,11,22,33}
        };
        internal void Send(RobotState state){Rig.Data(state);Rig.Tick();}
        internal void NewFrame()
        {Rig.Engine.InvokeEngineRequest(()=>{using var pose=new Pose3d(Robot.MainPose);Robot.SetNewPose(pose);return true;});}
        public void Dispose(){Rig.Dispose();RobotStateHistory.ConsecutiveGaps=_gaps;RobotStateHistory.ConnectionTimeoutInMs=_timeout;EngineErrorState.ErrorFlagSet=_error;}
    }
    private static HistRobotState Sentinel()
    {
        var state=new HistRobotState();state.State.Timestamp=0x12345678;state.State.HeadAngle=0.5f;
        state.Pose.Node!.Transform=new Transform3d(Rotation3d.Identity,new Vec3(77,88,99));return state;
    }

    // reach: unit (holds the real pose writer gate to verify the dispatch-built history query shares it; no history substitution).
    [Fact]
    public async Task HistoryQueriesShareTheNativePoseWritersGate()
    {
        using var f=new Fixture();f.Send(f.State(100));
        using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();
        var writer=Task.Run(()=>LogCallbackDeferral.RunGated(f.Robot.PoseTreeGate,()=>{entered.Set();release.Wait(TimeSpan.FromSeconds(10));}));
        Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
        var query=Task.Run(()=>{using var output=new HistRobotState();uint time=0;return f.History.GetRawStateAt(100,ref time,output,true);});
        bool completed;
        try{completed=query.Wait(TimeSpan.FromMilliseconds(100));}finally{release.Set();}
        await writer.WaitAsync(TimeSpan.FromSeconds(5));Assert.Equal(0u,await query.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.False(completed);
    }

    // reach: unit (calls the real bound movement query; detector trigger gates remain its separate M10-007 tests).
    [Fact]
    public void MovementQueryIsBoundToRealHistoryWithInterpolation()
    {
        using var f=new Fixture();f.Send(f.State(100,0));f.Send(f.State(200,10));
        var result=f.Rig.Robot.Sensors.UnexpectedMovement.ComputeStateAt!(150);
        Assert.Equal(HistoryLookupResult.Ok,result.Result);Assert.Equal(5f,(float)result.Pose.Translation.X);
    }

    // reach: unit (direct host ProcessCapture; native camera timestamp selection/result join MISSING at0x00652C22/0x00652C48).
    [Fact]
    public void HostFramePoseUsesTheCheckedInterpolationQuery()
    {
        using var f=new Fixture();using var vision=new VisionSystem(f.Rig.Robot,CameraCalibration.Nominal());
        vision.ModeEnableMask&=~(1<<7); // isolate query from the separately unbuilt image-quality ROI producer
        f.Send(f.State(100,0));f.Send(f.State(200,10));
        var image=new GrayImage(vision.Calibration!.Columns,vision.Calibration.Rows);image.Fill(150);
        var result=vision.ProcessCapture(image,1,150);
        Assert.NotNull(result);Assert.Equal(5f,(float)result!.PoseData.RobotPose.Translation.X);
    }

    // reach: unit (supplies the fraction directly because the 3000ms production window cannot express these adjacent f32s;
    // before/after states still enter by dispatch; no missing history join).
    [Theory][InlineData(0x3EFFFEAF,100)][InlineData(0x3EFFFEB0,200)][InlineData(0x3EFFFEB1,200)]
    public void InterpolationBaseChangesAtTheNativeFloatThreshold(int bits,uint selected)
    {
        using var f=new Fixture();f.Send(f.State(100));f.Send(f.State(200,10));
        using var before=new HistRobotState();using var after=new HistRobotState();using var result=new HistRobotState();uint time=0;
        Assert.Equal(0u,f.History.GetRawStateAt(100,ref time,before,false));
        Assert.Equal(0u,f.History.GetRawStateAt(200,ref time,after,false));
        using var relative=new Pose3d("");Assert.True(Pose3d.GetWithRespectTo(after.Pose,before.Pose,relative));
        Assert.True(f.History.Interpolate(before,after,relative,BitConverter.Int32BitsToSingle(bits),result));
        Assert.Equal(selected,result.State.Timestamp);
    }

    // reach: unit (uses the native SetNewPose producer to build frame boundaries; RobotState history is never substituted).
    [Fact]
    public void CorrectionAccumulatesTheBoundaryDeltaBeforeStoppingAndOnlyWritesFourFields()
    {
        using var f=new Fixture();f.Send(f.State(100,0));f.Send(f.State(150,5));
        f.Send(f.State(100));f.NewFrame();
        f.Send(f.State(200,10));f.Send(f.State(250,15));
        f.Send(f.State(100));f.NewFrame();
        f.Send(f.State(300,20));f.Send(f.State(350,25));
        f.Send(f.State(100));f.NewFrame();
        using var output=Sentinel();uint time=99;
        Assert.Equal(0u,f.History.ComputeStateAt(350,ref time,output,false));
        Assert.Equal(350u,time);Assert.Equal(35f,(float)output.Pose.Translation.X);
        Assert.Equal(2u,output.State.PoseFrameId);Assert.Equal(0.125f,output.State.HeadAngle);Assert.Equal(0.25f,output.State.LiftAngle);
        Assert.Equal(0x12345678u,output.State.Timestamp);Assert.Equal(0x300u,output.State.Status);
        Assert.Equal(new ushort[]{65535,65535,65535,65535},output.State.CliffDataRaw);
    }

    // reach: unit (native SetNewPose creates vision-only state; no raw history substitution).
    [Fact]
    public void LastFrameQueryRequiresRawEvenWithAnExactVisionState()
    {
        using var f=new Fixture();f.NewFrame();using var output=Sentinel();var prior=output.State.ToBytes();
        Assert.Equal(1u,f.History.GetLastStateWithFrameID(f.Robot.PoseFrameId,output));
        Assert.Equal(prior,output.State.ToBytes());Assert.Equal(new Vec3(77,88,99),output.Pose.Translation);
        f.Send(f.State(100));Assert.Equal(0u,f.History.GetLastStateWithFrameID(f.Robot.PoseFrameId,output));
        Assert.Equal(100u,output.State.Timestamp); // newest matching raw wins over vision
    }

    // reach: unit (seeds only auxiliary indices whose insertion owner remains MISSING at M11-044/0x00531BAA;
    // raw timestamps come from dispatch; tests the checked native prefix-erasure unit).
    [Fact]
    public void CullingKeepsTheCutoffAndErasesTheKeyIndexPrefixRatherThanPairs()
    {
        using var f=new Fixture();f.Send(f.State(500));f.Send(f.State(1000));
        f.History.ComputedTimeToKey.Add(500,40);f.History.ComputedTimeToKey.Add(1500,20);f.History.ComputedTimeToKey.Add(3000,10);
        f.History.ComputedKeyToTime.Add(40,500);f.History.ComputedKeyToTime.Add(20,1500);f.History.ComputedKeyToTime.Add(10,3000);
        f.Send(f.State(4000));Assert.Equal(2,f.History.Count);Assert.Equal(1000u,f.History.OldestTimeStamp);
        Assert.Equal(new uint[]{1500,3000},f.History.ComputedTimeToKey.Keys);
        Assert.Equal(new uint[]{20,40},f.History.ComputedKeyToTime.Keys);
    }

    // reach: production â€” all raw entries come from RobotState packets/dispatch; failure outputs are native query contracts.
    [Theory][InlineData(false)][InlineData(true)]
    public void BracketFailurePreservesStateAndOutputTime(bool interpolate)
    {
        using var f=new Fixture();using var value=Sentinel();uint time=99;var prior=value.State.ToBytes();
        Assert.Equal(1u,f.History.GetRawStateAt(100,ref time,value,interpolate));
        f.Send(f.State(100));
        Assert.Equal(1u,f.History.GetRawStateAt(99,ref time,value,interpolate));
        Assert.Equal(1u,f.History.GetRawStateAt(101,ref time,value,interpolate));
        Assert.Equal(99u,time);Assert.Equal(prior,value.State.ToBytes());Assert.Equal(new Vec3(77,88,99),value.Pose.Translation);
    }

    // reach: production â€” actual RobotState dispatch to raw map, then flag0 native query; tie selects the earlier key.
    [Fact]
    public void NearestTieUsesEarlierKeyAndCopiesEveryStateField()
    {
        using var f=new Fixture();var before=f.State(100,2);before.CurrPathSegment=7;before.BackpackTouchSensorRaw=123;
        var after=f.State(200,12);after.CurrPathSegment=9;f.Send(before);f.Send(after);
        using var value=new HistRobotState();uint time=0;
        Assert.Equal(0u,f.History.GetRawStateAt(150,ref time,value,false));Assert.Equal(100u,time);
        Assert.Equal(before.ToBytes(),value.State.ToBytes());Assert.Equal(2f,(float)value.Pose.Translation.X);
        Assert.Equal(0u,f.History.GetRawStateAt(151,ref time,value,false));Assert.Equal(200u,time);
    }

    // reach: unit (raw states through dispatch; actual native SetNewPose supplies the frame/vision producer between them).
    [Theory][InlineData(false)][InlineData(true)]
    public void FrameIdMismatchPrecedesTheFlagTest(bool interpolate)
    {
        using var f=new Fixture();f.Send(f.State(100));f.NewFrame();f.Send(f.State(200));
        using var value=Sentinel();uint time=88;var prior=value.State.ToBytes();
        Assert.Equal(0x06000000u,f.History.GetRawStateAt(150,ref time,value,interpolate));
        Assert.Equal(88u,time);Assert.Equal(prior,value.State.ToBytes());
    }

    // reach: production â€” actual raw dispatch; independent exact f32 values and native endpoint timestamp/serialized pose.
    [Fact]
    public void InterpolationUsesNativeWidthsAndOnlyCliffChannelZero()
    {
        using var f=new Fixture();var a=f.State(100,0,cliff:100);var b=f.State(200,10,cliff:201);
        a.LwheelSpeedMmps=10;a.RwheelSpeedMmps=-10;b.LwheelSpeedMmps=30;b.RwheelSpeedMmps=10;
        b.HeadAngle=0.375f;b.LiftAngle=0.75f;b.Pose.Z=8;b.CurrPathSegment=12;
        f.Send(a);f.Send(b);using var value=new HistRobotState();uint time=999;
        Assert.Equal(0u,f.History.GetRawStateAt(150,ref time,value,true));Assert.Equal(150u,time);
        Assert.Equal(200u,value.State.Timestamp);Assert.Equal(8f,value.State.Pose.Z);Assert.Equal((sbyte)12,value.State.CurrPathSegment);
        Assert.Equal(0x3E800000,BitConverter.SingleToInt32Bits(value.State.HeadAngle));
        Assert.Equal(0x3F000000,BitConverter.SingleToInt32Bits(value.State.LiftAngle));
        Assert.Equal(20f,value.State.LwheelSpeedMmps);Assert.Equal(0f,value.State.RwheelSpeedMmps);
        Assert.Equal(new ushort[]{151,0xFFFF,0xFFFF,0xFFFF},value.State.CliffDataRaw);
        Assert.Equal(5f,(float)value.Pose.Translation.X);Assert.Equal(0f,(float)value.Pose.Translation.Z); // A4 takes current z
    }

    // reach: unit (RobotState dispatch plus actual SetNewPose producer; no direct insertion/substituted history).
    [Fact]
    public void ExactVisionStateHasPriorityEvenWithNoRawBracket()
    {
        using var f=new Fixture();f.Send(f.State(100));f.NewFrame();
        using var value=new HistRobotState();uint time=0;
        Assert.Equal(0u,f.History.ComputeStateAt(100,ref time,value,true));
        Assert.Equal(f.Robot.PoseFrameId,value.State.PoseFrameId);Assert.Equal(1u,value.State.Timestamp);
        Assert.Equal(100u,time);Assert.Equal(0xFFFF,value.State.CliffDataRaw[1]); // native default state, not packet
    }

    // reach: production â€” real calibrated treads transitions produce a vision-only state at the incoming timestamp.
    [Fact]
    public void TreadsStateDelocalizesBeforeTailAndIsNotAdmittedAsRaw()
    {
        using var f=new Fixture();f.Rig.Calibrate();
        f.Send(f.State(100));int raw=f.History.Count;uint origin=f.Robot.CurrentOriginId;
        var lifted=f.State(200,flags:RobotStatusFlag.IsBodyAccMode|RobotStatusFlag.IsPickedUp);f.Send(lifted);
        Assert.Equal(OffTreadsState.InAir,f.Rig.Robot.Sensors.OffTreadsState);
        Assert.NotEqual(origin,f.Robot.CurrentOriginId);Assert.Equal(raw,f.History.Count);
        using var value=new HistRobotState();uint time=0;
        Assert.Equal(0u,f.History.ComputeStateAt(200,ref time,value,false));Assert.Equal(f.Robot.PoseFrameId,value.State.PoseFrameId);
        Assert.Equal(1u,value.State.Timestamp); // Delocalize -> SetNewPose -> native default vision-only state
        Assert.Equal(0u,f.Robot.LastFullStateResult);
    }

    // reach: production â€” raw admission errors from actual packet dispatch, process counter/reset and u32 subtraction wrap.
    [Fact]
    public void SixthLargeGapClearsOnlyRawAndAnOlderKeyWrapsTheDelta()
    {
        using var f=new Fixture();f.Send(f.State(1000));f.NewFrame();
        for(uint i=0;i<5;i++){f.Send(f.State(7001+i));Assert.Equal(1,f.History.Count);}
        Assert.Equal((byte)5,RobotStateHistory.ConsecutiveGaps);int vision=f.History.VisionOnly.Count;
        f.Send(f.State(7006));Assert.Equal(0,f.History.Count);Assert.Equal(vision,f.History.VisionOnly.Count);
        Assert.Equal((byte)0,RobotStateHistory.ConsecutiveGaps);
        f.Send(f.State(10000));Assert.Equal(1,f.History.Count); // empty tree skips gap gate
        f.Send(f.State(9999));Assert.Equal(1,f.History.Count);Assert.Equal((byte)1,RobotStateHistory.ConsecutiveGaps);
        f.Send(f.State(10000));Assert.Equal((byte)0,RobotStateHistory.ConsecutiveGaps);Assert.Equal(1,f.History.Count); // duplicate rejected after reset
    }

    // reach: production â€” a new robot's empty raw map does not reset the native process-global counter.
    [Fact]
    public void EmptyRawMapDoesNotResetTheGapRun()
    {
        using var first=new Fixture();first.Send(first.State(1000));first.Send(first.State(7001));
        Assert.Equal((byte)1,RobotStateHistory.ConsecutiveGaps);
        using var second=new M4ControlTests.Rig();second.ToSynced(); // does not use Fixture, which explicitly isolates test statics
        second.Data(second.MakeState());second.Tick();Assert.Equal((byte)1,RobotStateHistory.ConsecutiveGaps);
        Assert.Equal(1,second.Engine.Robot!.PoseHistory.Count);
    }

    // reach: production â€” duplicate and rejected origin use the actual dispatch path and leave admitted data intact.
    [Fact]
    public void DuplicateAndBadOriginDoNotAdmitOrRunTheTail()
    {
        using var f=new Fixture();f.Send(f.State(100));int mark=f.Rig.Mark();float variance=f.Rig.Robot.Sensors.CliffVariance;
        f.Send(f.State(100,100,cliff:1000));Assert.Equal(1,f.History.Count);Assert.Equal(1u,f.Robot.LastFullStateResult);
        f.Send(f.State(200,100,origin:999,cliff:1000));Assert.Equal(1,f.History.Count);Assert.Null(f.Robot.AcceptedState);
        Assert.Equal(variance,f.Rig.Robot.Sensors.CliffVariance);Assert.DoesNotContain(f.Rig.SentSince(mark),m=>m is SetCliffDetectThreshold);
    }

    // reach: production â€” actual admission, running statistics, stopped message and raw-history walk, no result fixtures.
    [Fact]
    public void ProductionHistoryEnablesFiftyFourHundredAndSuspiciousThresholdSends()
    {
        using var f=new Fixture();int mark=f.Rig.Mark();
        var moving=RobotStatusFlag.IsBodyAccMode|RobotStatusFlag.AreWheelsMoving;
        f.Send(f.State(100,0,cliff:1000,flags:moving));f.Send(f.State(200,60,cliff:500,flags:moving));
        f.Rig.Data(new Cozmo.Protocol.RobotStopped());f.Rig.Tick();f.Send(f.State(300,60,cliff:1000));
        Assert.Equal(new ushort[]{50,400,150},f.Rig.SentSince(mark).OfType<SetCliffDetectThreshold>().Select(m=>m.Field0));
        Assert.True(f.Rig.Robot.Sensors.CliffVariance>10000);
    }

    // reach: production â€” incoming CliffEvent -> real ComputeStateAt(true) -> live AddCliff/BlockWorld/map owner.
    [Fact]
    public void DetectedCliffUsesInterpolatedRobotHistoryAndIsPlacedInTheWorld()
    {
        using var f=new Fixture();f.Send(f.State(100,0));f.Send(f.State(200,10));
        f.Rig.Data(new CliffEvent{Timestamp=150,DetectedFlags=1});f.Rig.Tick();
        var cliff=Assert.Single(f.Rig.Robot.LocalizationWorld.Objects,o=>o.Type==ObjectType.CliffDetection);
        Assert.Equal(5f,(float)cliff.Pose.Translation.X);Assert.Equal(PoseState.Known,cliff.PoseState);
        Assert.Contains(f.Rig.Log,l=>l.Contains("RobotImplMessaging.HandleCliffEvent.Detected"));
        Assert.DoesNotContain(f.Rig.Log,l=>l.Contains("M11-097 ComputeStateAt(timestamp, true)"));
    }
}
