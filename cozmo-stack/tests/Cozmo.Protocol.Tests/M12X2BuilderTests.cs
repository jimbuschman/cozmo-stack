using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Manipulation;
using Cozmo.Transport;
using Xunit;
namespace Cozmo.Protocol.Tests;
[Collection("M5 process statics")]
public sealed class M12X2BuilderTests
{
    private static PathSegment.Line Line(int i)=>new(i,0,i+1,0,100,200,500);
    [Theory] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void FailedSecondSegmentRetainsLastGoodAndRobotStateRetries(int kind)
    {
        using var r=new M4ControlTests.Rig();r.ToSynced();r.State();var p=r.Robot.Motion.Path;
        PathSegment second=kind switch{2=>new PathSegment.Arc(0,0,10,0,1,100,200,500),3=>new PathSegment.PointTurn(0,0,1,0.1f,1,10,10,true),_=>Line(1)};
        var path=new PathSegment[]{Line(0),second,Line(2),Line(3),Line(4),Line(5),Line(6)};
        var attempts=new List<RobotMessage>();bool failed=false;var entries=new List<EngineLogEntry>();r.Engine.LogEntry+=entries.Add;
        var validated=typeof(RobotInitialConnection).GetField("_validated",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!;
        p.SendPathMessage=m=>
        {
            if(m is AppendPathSegmentLine or AppendPathSegmentArc or AppendPathSegmentPointTurn)
            {
                attempts.Add(m);if(attempts.Count==2&&!failed)
                {
                    failed=true;validated.SetValue(r.Engine.Robots.Ric,false); // controlled handshake input: actual handler filter drops this send
                    try{return r.Robot.SendToHandler(m);}finally{validated.SetValue(r.Engine.Robots.Ric,true);}
                }
                return r.Robot.SendToHandler(m);
            }
            return r.Robot.SendMessage(m);
        };
        bool prior=EngineErrorState.ErrorFlagSet;EngineErrorState.ErrorFlagSet=false;
        try
        {
            Assert.Equal(0,p.ExecuteCustomPath(path,false));Assert.Equal(2,attempts.Count);Assert.Equal(0,p.LastDoledIndex);
            var error=Assert.Single(entries.Where(e=>e.Name=="PathDolerOuter.Dole"));
            Assert.Equal((EngineLogLevel.Error,(string?)null),(error.Level,error.Channel));Assert.EndsWith("ERROR: failed to send message!",error.Text);Assert.True(EngineErrorState.ErrorFlagSet);
            r.Data(new PathFollowingEvent{EventId=p.Field42,EventType=(byte)PathEventType.Started});r.Tick();
            var state=r.MakeState();state.CurrPathSegment=0;r.Data(state);r.Tick();
            Assert.Equal(5,p.LastDoledIndex);Assert.Equal(7,attempts.Count);Assert.Equal(attempts[1].ToBytes(),attempts[2].ToBytes());
            Assert.Single(entries.Where(e=>e.Name=="PathDolerOuter.Dole"));
        }
        finally{EngineErrorState.ErrorFlagSet=prior;}
    }
    [Fact]
    public void OriginMissingBelongsToOuterInputGate()
    {
        using var r=new M4ControlTests.Rig();var p=r.Robot.Motion.Path;var logs=new List<string>();r.Engine.LogLine+=logs.Add;
        p.StartDrivingToPose(1,new(),false,()=>new(2,Array.Empty<PathSegment>(),0),()=>{},_=>{});
        Assert.Contains(logs,s=>s.Contains("goal origin/ComputeDriveCenterPose gates unbuilt"));
        p.GoalRootOriginInput=_=>1;p.ContainsOriginInput=_=>true;logs.Clear();
        p.StartDrivingToPose(1,new(),false,()=>new(2,Array.Empty<PathSegment>(),0),()=>{},_=>{});
        Assert.DoesNotContain(logs,s=>s.Contains("goal origin/ComputeDriveCenterPose gates unbuilt"));
    }
    [Fact]
    public void SegmentRouteSeesHeadLiftCliffTouchBeforeImu()
    {
        using var r=new M4ControlTests.Rig();r.ToSynced();r.Calibrate();r.Tick();r.State();
        float old=r.Robot.Sensors.OffTreads.RawAccelMagnitude;int calls=0;
        r.Engine.PathSegmentRoute=segment=>
        {
            calls++;Assert.Equal(3,segment);Assert.Equal(0.25f,r.Robot.Motion.HeadAngleRad);
            Assert.Equal(0.5f,typeof(CozmoMotion).GetField("_liftAngle",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(r.Robot.Motion));
            Assert.True(r.Robot.Sensors.CliffDetectedStored);Assert.Equal((ushort)123,r.Robot.Sensors.CliffDataRawStored[0]);
            Assert.Equal((ushort)456,r.Engine.Robot!.TouchSensor!.Raw);Assert.Equal(old,r.Robot.Sensors.OffTreads.RawAccelMagnitude);
        };
        var state=r.MakeState(RobotStatusFlag.IsBodyAccMode|RobotStatusFlag.CliffDetected,head:0.25f,liftAngle:0.5f);
        state.CurrPathSegment=3;state.BackpackTouchSensorRaw=456;state.CliffDataRaw[0]=123;state.Accel=new AccelData{Z=4000};r.Data(state);r.Tick();
        Assert.Equal(1,calls);Assert.Equal(4000,r.Robot.Sensors.OffTreads.RawAccelMagnitude);
    }
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task HostRunHandlesEmptyAndBothSendFailuresWithoutReservationThrow(int failure)
    {
        using var rig=new Rig();var p=rig.Robot.Motion.Path;int sends=0;
        p.SendPathMessage=m=>{sends++;return failure==1?false:m is not ExecutePath;};
        using var run=rig.M.StartPath(failure==0?Array.Empty<PathSegment>():new PathSegment[]{Line(0)});
        Assert.Equal(failure==0?0:1,run.Result);int after=sends;
        Assert.Equal(failure==0?PathEventType.Completed:(PathEventType?)null,await run.WaitAsync(TimeSpan.FromSeconds(30),default));
        run.Abort();run.Dispose();Assert.Equal(after,sends);Assert.False(run.Aborted);Assert.False(run.ClearedRobotPath);
    }
}
