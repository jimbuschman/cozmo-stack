using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class M10GyroCheckedRowsTests
{
    private sealed class Fixture : IDisposable
    {
        internal readonly CozmoRobot Host=CozmoRobot.CreateOffline();
        internal EngineRobot Robot=>Host.Engine.Robot!;
        internal RobotGyroDriftDetector Detector=>Robot.GyroDetector!;
        internal readonly List<EngineLogEntry> Logs=new();
        internal Fixture(){Host.Engine.LogEntry+=Logs.Add;}
        internal void Yaw(float angle){using var pose=new Pose3d(Rotation3d.FromAngleAxis(angle,new Vec3(0,0,1)),Vec3.Zero,parent:Robot.GetNativeOrigin(Robot.CurrentOriginId));Robot.MainPose.Assign(pose);}
        internal void Drift(uint time,float z=0.01f,bool moving=false,bool cliff=false,byte head=1,uint frame=0)
            =>Detector.DetectGyroDrift(State(time,z:z),moving,cliff,head,frame);
        public void Dispose()=>Host.Dispose();
    }
    private static RobotState State(uint time,float x=0,float y=0,float z=0,uint status=0)=>new(){Timestamp=time,Status=status,Gyro=new GyroData{X=x,Y=y,Z=z}};

    [Fact]
    public void FilterOwnsTheExistingPoolAndItsSixUiSubscriptions()
    {
        using var f=new Fixture();Assert.Same(f.Host.Cubes.Connections,f.Robot.BlockFilterOwner);
        foreach(ushort tag in new ushort[]{0xE7,0xE5,0xE6,0xE9,0xEA,0xEB})Assert.Equal(1,f.Host.Engine.AnimationMessages.Count(tag,false));
        Assert.All(f.Robot.BlockFilterOwner!.PersistentPool,p=>{Assert.Equal(0u,p.FactoryId);Assert.Equal(ObjectType.InvalidObject,p.Type);});
        Assert.False(f.Detector.DriftReported);Assert.False(f.Detector.BiasReported);Assert.All(f.Detector.Minima,v=>Assert.Equal(0f,v));Assert.True(f.Detector.StartYaw.RescaleEnabled);
    }
    [Fact]
    public void NullInterfaceConstructsTheOwnerWithoutSubscribing()
    {
        using var engine=new CozmoEngine(new M4ControlTests.FakePort(),new CozmoEngineOptions(),()=>1_000_000_000);
        engine.BeforeDerivedConstructionForTest=r=>r.HasExternalInterface=false;
        var robot=engine.Robots.AddRobot(1,false)!;Assert.NotNull(robot.BlockFilterOwner);
        foreach(ushort tag in new ushort[]{0xE7,0xE5,0xE6,0xE9,0xEA,0xEB})Assert.Equal(0,engine.AnimationMessages.Count(tag,false));
    }
    [Fact]
    public void NativeRemovalReleasesInReverseBeforeStringTreeAndNullsGyroBeforeItsRawDelete()
    {
        using var f=new Fixture();var native=f.Robot;var oldDetector=f.Detector;var order=new List<string>();
        f.Host.Engine.AnimationMessages.ReleaseObserved=line=>{if(line.StartsWith("unlink G2E E"))order.Add(line);};
        f.Host.Engine.DerivedRetirementObservedForTest=stage=>
        {
            if(stage=="gyro raw delete")Assert.Null(native.GyroDetector);
            else Assert.NotNull(native.BlockFilterOwner); // +44C stays until after filter's allocation release.
            order.Add(stage);
        };
        f.Host.Engine.Robots.RemoveRobot(1,false);
        Assert.Equal(new[]{"unlink G2E EB","unlink G2E EA","unlink G2E E9","unlink G2E E6","unlink G2E E5","unlink G2E E7","filter subscriptions retired","filter string/tree retired","filter allocation retired","gyro raw delete"},order);
        Assert.Null(native.BlockFilterOwner);Assert.DoesNotContain(f.Logs,l=>l.Name?.Contains("gyro")==true||l.Name=="RobotGyroDriftDetector.BiasDetected");
        f.Host.Engine.DerivedRetirementObservedForTest=null;
        var replacement=f.Host.Engine.Robots.AddRobot(1,false)!;Assert.NotSame(oldDetector,replacement.GyroDetector);Assert.Equal(0u,replacement.GyroDetector!.BiasCount);
    }

    [Fact]
    public void DriftUsesTheNativePoseAndReportsAfter5001WithoutTheTriggeringSample()
    {
        using var f=new Fixture();f.Yaw(0);f.Drift(100);f.Yaw(0.02f);f.Drift(5100);
        Assert.False(f.Detector.DriftReported);Assert.Equal(2u,f.Detector.DriftCount);Assert.Equal(0.02f,f.Detector.Sum);
        f.Drift(5101,z:0.011f);Assert.True(f.Detector.DriftReported);Assert.Equal(0u,f.Detector.DriftStart);Assert.Equal(2u,f.Detector.DriftCount);Assert.Equal(0.01f,f.Detector.Maximum);
        var report=Assert.Single(f.Logs.Where(l=>l.Name=="robot.detect_gyro_drift.drift_detected"));
        Assert.Equal(EngineLogLevel.Warning,report.Level);Assert.Equal("1.145916",report.Data["$data"]);
        f.Drift(6000,z:1);Assert.Equal(0u,f.Detector.DriftStart);Assert.Equal(2u,f.Detector.DriftCount);
    }
    [Theory]
    [InlineData(true,false,(byte)1)][InlineData(false,true,(byte)1)][InlineData(false,false,(byte)0)][InlineData(false,false,(byte)2)]
    public void DriftGateClearsOnlyTheStart(bool moving,bool cliff,byte head)
    {
        using var f=new Fixture();f.Drift(100);f.Drift(200,moving:moving,cliff:cliff,head:head);
        Assert.Equal(0u,f.Detector.DriftStart);Assert.Equal(1u,f.Detector.DriftCount);Assert.Equal(0.01f,f.Detector.Baseline);
    }
    [Fact]
    public void DriftRejectsAChangedFrameAndRestartsAfterZeroTimestamp()
    {
        using var f=new Fixture();f.Drift(0);Assert.Equal(0u,f.Detector.DriftStart);f.Drift(100);Assert.Equal(1u,f.Detector.DriftCount);
        f.Drift(200,frame:1);Assert.Equal(0u,f.Detector.DriftStart);Assert.False(f.Detector.DriftReported);
    }
    [Fact]
    public void DriftTimestampWrapUsesUnsignedDifferenceAndUnorderedFinalYawRejects()
    {
        using var f=new Fixture();f.Yaw(0);f.Drift(uint.MaxValue-100);f.Yaw(0.02f);f.Drift(5000);Assert.True(f.Detector.DriftReported);
        f.Detector.DriftReported=false;f.Drift(6000);f.Detector.StartYaw.Value=float.NaN;f.Drift(11001);Assert.False(f.Detector.DriftReported);Assert.Equal(0u,f.Detector.DriftStart);
    }
    [Fact]
    public void NaNZPassesTheOrderedRejectionsButPoisonsTheSum()
    {
        using var f=new Fixture();f.Drift(100,float.NaN);f.Drift(200,float.NaN);
        Assert.Equal(2u,f.Detector.DriftCount);Assert.True(float.IsNaN(f.Detector.Sum));Assert.Equal(100u,f.Detector.DriftStart);
    }

    [Fact]
    public void PositiveInitialBiasWindowIsSuppressedByCtorMinimaThenResetInstallsInfinity()
    {
        using var f=new Fixture();for(uint t=100;t<5000;t+=50)f.Detector.DetectBias(State(t,x:0.01f),0);
        Assert.Equal(0f,f.Detector.Minima[0]);f.Detector.DetectBias(State(5101,x:0.01f),0);
        Assert.False(f.Detector.BiasReported);Assert.Equal(1u,f.Detector.BiasCount);Assert.Equal(0u,f.Detector.BiasStart);Assert.True(f.Detector.Minima[0]>0);
    }
    [Fact]
    public void BiasCanReportAllAxesThenResetsAndIncludesTheCurrentSampleBeforeLatchingNextCall()
    {
        using var f=new Fixture();var d=f.Detector;d.BiasStart=1;d.BiasCount=50;Array.Fill(d.Minima,0.01f);Array.Fill(d.Maxima,0.0101f);
        d.DetectBias(State(5002,x:0.01f,y:0.01f,z:0.01f),0);Assert.True(d.BiasReported);Assert.Equal(1u,d.BiasCount);Assert.Equal(0u,d.BiasStart);
        var reports=f.Logs.Where(l=>l.Name=="RobotGyroDriftDetector.BiasDetected").ToArray();Assert.Equal(3,reports.Length);
        for(int i=0;i<3;i++)Assert.EndsWith($"Gyro bias detected on {"xyz"[i]} axis (min=0.57 deg/sec, max=0.58 deg/sec)",reports[i].Text);
        var events=f.Logs.Where(l=>l.Name=="robot.detect_gyro_bias.bias_detected").ToArray();Assert.Equal(3,events.Length);
        Assert.All(events,e=>{Assert.Equal(EngineLogLevel.Event,e.Level);Assert.Equal("573.000000,579.000000",e.Text);});
        Assert.Equal(new[]{"x","y","z"},events.Select(e=>e.Data["$data"]));
        for(int i=0;i<3;i++)Assert.True(f.Logs.IndexOf(events[i])<f.Logs.IndexOf(reports[i]));
        float hp=d.HighPass;d.DetectBias(State(6000),100);Assert.Equal(hp,d.HighPass);Assert.Equal(1u,d.BiasCount);
    }
    [Theory]
    [InlineData(49u)][InlineData(uint.MaxValue)]
    public void BiasCountGateIsSignedAndRequires50PriorSamples(uint count)
    {
        using var f=new Fixture();var d=f.Detector;d.BiasStart=1;d.BiasCount=count;Array.Fill(d.Minima,0.01f);Array.Fill(d.Maxima,0.0101f);
        d.DetectBias(State(5002),0);Assert.False(d.BiasReported);Assert.Equal(1u,d.BiasCount);Assert.Empty(f.Logs);
    }
    [Fact]
    public void BiasStatusBitZeroResetsOnlyAnExistingWindowAfterUpdatingHighPass()
    {
        using var f=new Fixture();var d=f.Detector;d.BiasStart=100;d.BiasCount=7;d.Filtered[0]=17;d.DetectBias(State(200,status:1),10);
        Assert.Equal(8f,d.HighPass);Assert.Equal(10f,d.PreviousMagnitude);Assert.Equal(17f,d.Filtered[0]);Assert.Equal(0u,d.BiasCount);
        Assert.All(d.Minima,v=>Assert.True(float.IsPositiveInfinity(v)));Assert.All(d.Maxima,v=>Assert.Equal(0f,v));
    }
    [Fact]
    public void HighPassEquality50AndNaNAreEligibleAndNaNFilteredExtremaStayOld()
    {
        using var f=new Fixture();var d=f.Detector;d.DetectBias(State(100),62.5f);Assert.Equal(50f,d.HighPass);Assert.Equal(1u,d.BiasCount);
        d.DetectBias(State(200,x:float.NaN),float.NaN);Assert.Equal(2u,d.BiasCount);Assert.True(float.IsNaN(d.Filtered[0]));Assert.Equal(0f,d.Minima[0]);Assert.Equal(0f,d.Maxima[0]);
    }
    [Fact]
    public void NegativeWindowCannotPassTheZeroMaximumAndThresholdEqualityFails()
    {
        using var f=new Fixture();var d=f.Detector;d.BiasStart=1;d.BiasCount=50;d.Minima[0]=-0.01f;d.Maxima[0]=0;
        d.Minima[1]=BitConverter.Int32BitsToSingle(0x3AE4C388);d.Maxima[1]=d.Minima[1];
        d.DetectBias(State(5002),0);Assert.False(d.BiasReported);Assert.Empty(f.Logs);
    }
    [Fact]
    // reach: production — native admission now reaches the actual bias detector; legacy name retained.
    public void LiveSensorTailComputesMagnitudeButDoesNotInventHistoryAdmission()
    {
        using var rig=new M4ControlTests.Rig();rig.ToSynced();var state=rig.MakeState();state.Accel=new AccelData{X=3,Y=4,Z=12};rig.Data(state);rig.Tick();
        Assert.Equal(13f,rig.Robot.Sensors.OffTreads.RawAccelMagnitude);Assert.Equal(0u,rig.Engine.Robot!.GyroDetector!.DriftStart);Assert.Equal(1u,rig.Engine.Robot.GyroDetector.BiasCount);Assert.Equal(1,rig.Engine.Robot.PoseHistory.Count);
    }
    [Theory]
    [InlineData(float.NaN)][InlineData(float.PositiveInfinity)]
    public void RawMagnitudePreservesNonfiniteInputBehavior(float x)
    {
        var classifier=new OffTreadsClassifier{HeadCalibrated=false};
        classifier.Update(new RobotState{Accel=new AccelData{X=x,Y=4,Z=12}},100);
        if(float.IsNaN(x))Assert.True(float.IsNaN(classifier.RawAccelMagnitude));
        else Assert.True(float.IsPositiveInfinity(classifier.RawAccelMagnitude));
    }
}
