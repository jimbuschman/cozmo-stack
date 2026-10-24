using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Vision;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Xunit;
namespace Cozmo.Protocol.Tests;
[Collection("M5 process statics")]
public sealed class M5X18BuilderTests
{
    private static void NoAnimationFixtureInputs(Rig rig)
    {
        rig.M.DrivingAnimationsForTest=null;
        Assert.Null(rig.M.DriveTickForTest);Assert.Null(rig.M.StraightTickForTest);
        Assert.Null(rig.M.DriveTimeoutForTest);Assert.Null(rig.M.StraightTimeoutForTest);
        // Actual G2E input, not a test factory: source NoAnimation sentinel 0x23F.
        rig.Robot.Animations.PushDrivingAnimations((AnimationTrigger)0x23F,(AnimationTrigger)0x23F,(AnimationTrigger)0x23F,"fixture-input");
    }
    [Fact]
    public void NoAnimationFixtureDriveCompletesWithSuccessUsingNativeM12_032Status()
    {
        using var signals=SignalTestContext.Install();using var rig=new Rig();NoAnimationFixtureInputs(rig);
        var observed=new List<string>();rig.Robot.Engine.StackLogLine+=observed.Add;
        // A / reach: unit (offline drive): old goal parent=null yielded FailedTraversingPath.
        using var goal=new Pose3d(Mat3.Identity,new Vec3(150,0,0));
        goal.SetParent(rig.Vision.World.GetNativeOrigin(rig.Vision.World.CurrentOriginId));
        using var d=new DriveToPoseAction(rig.M){Goal=goal};
        var result=SignalTestContext.Result(d.RunAsync(default),()=>rig.Pump());
        Assert.Equal(ActionResult.Success,result);Assert.False(d.CanInterrupt());Assert.Equal(0x41F00000,BitConverter.SingleToInt32Bits(d.TimeoutSeconds));
        Assert.DoesNotContain(observed,s=>s.Contains("status/IDs mapping is a stand-in"));
    }
    [Fact]
    public void NoAnimationFixtureStraightCompletesUsingNativeStatusAndTinyDistanceSendsNoPath()
    {
        using var signals=SignalTestContext.Install();using var rig=new Rig();NoAnimationFixtureInputs(rig);
        using var d=new DriveStraightAction(rig.M,100);Assert.Equal(ActionResult.Success,SignalTestContext.Result(d.RunAsync(default),()=>rig.Pump()));Assert.False(d.CanInterrupt());
        int before=rig.M.Paths.Sent.Count;using var tiny=new DriveStraightAction(rig.M,BitConverter.Int32BitsToSingle(0x3727C5AB));
        Assert.Equal(ActionResult.Success,SignalTestContext.Result(tiny.RunAsync(default),rig.Tick));Assert.Equal(before,rig.M.Paths.Sent.Count);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void TokenCancelledBeforeFirstTickDeletesPendingDriveWithNativeNotStartedResult(bool straight)
    {
        using var signals=SignalTestContext.Install();using var rig=new Rig();NoAnimationFixtureInputs(rig);using var cts=new CancellationTokenSource();cts.Cancel();
        Task<ActionResult> task=straight?new DriveStraightAction(rig.M,100).RunAsync(cts.Token):new DriveToPoseAction(rig.M){Goal=Pose3d.Identity}.RunAsync(cts.Token);
        Assert.Equal(0x02000001u,(uint)SignalTestContext.Result(task));Assert.Null(rig.Robot.Engine.Robot!.ActionList.GetCurrentAction());Assert.Empty(rig.M.Paths.Sent);
    }
    [Fact]
    public void CancellationRacingUpdateIsSerializedByActionListGate()
    {
        using var signals=SignalTestContext.Install();using var rig=new Rig();NoAnimationFixtureInputs(rig);using var cts=new CancellationTokenSource();
        using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();using var cancelEntered=new ManualResetEventSlim();
        rig.M.DriveTickForTest=_=>{entered.Set();Assert.True(release.Wait(TimeSpan.FromSeconds(10)));return new DriveToPoseTick(2,0,Pose3d.Identity,67.7,0,0);};
        using var d=new DriveToPoseAction(rig.M){Goal=Pose3d.Identity};var result=d.RunAsync(cts.Token);
        var tick=Task.Run(rig.Tick);Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
        var cancel=Task.Run(()=>{cancelEntered.Set();cts.Cancel();});Assert.True(cancelEntered.Wait(TimeSpan.FromSeconds(10)));release.Set();
        Assert.True(Task.WaitAll(new[]{tick,cancel},TimeSpan.FromSeconds(10)));
        Assert.Equal(ActionResult.CancelledWhileRunning,SignalTestContext.Result(result));Assert.Null(rig.Robot.Engine.Robot!.ActionList.GetCurrentAction());
    }
}
