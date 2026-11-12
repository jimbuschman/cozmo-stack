using Cozmo.Robot;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;
using Fixture=Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;
namespace Cozmo.Protocol.Tests;

// M12-022 D4 (census LEGACY-DRIVE-TO-POSE-NATIVE-1; research 20260929-R-VIS-M12-gap2-extraction.md Q12.8/Q12.10):
// DriveToObjectAction::InitHelper builds DriveToPoseAction(robot, head r2=1, manual r3=[this+0x8C]) at 0x005592E8..0x005592FE.
// The DriveToPoseAction ctor 0x0055A238 locks track mask 4 unless manual (research 20261028-pickup-children-1-read.md row 9), and
// PathComponent::StartDrivingToPose stores the manual flag at +0x46 (P4 0x0064AD44..0x0064AD5C).
[Collection("SteppedBehavior missing-report statics")]
public sealed class NativeDriveToObjectManualSpeedTests
{
    private static uint RunInit(bool? manual,out bool field46,out bool driveBuilt)
    {
        using var f=new Fixture();var obj=f.Add();
        var runner=new NativeDriveToObjectRunner(f.M,obj.ObjectId);
        if(manual is {} value)runner.Action.UseManualSpeed=value;
        using var goal=new Pose3d(Rotation3d.Identity,new(130,0,0),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));
        var marker=obj.Markers[0];
        runner.SetPosesFunction((ObservableObject _,out IReadOnlyList<PreActionPose> poses,ref bool inPosition)=>
        {poses=new[]{new PreActionPose(PreActionType.Docking,marker,goal,75)};return ActionResult.Success;});
        var path=f.Rig.Robot.Motion.Path;path.Field46=!(manual??false); // the opposite of the expected value, so the write is observed.
        try
        {
            runner.Init();
            driveBuilt=runner.Drive is not null;
            field46=path.Field46;
            return runner.Drive?.RequiredTrackMask??uint.MaxValue;
        }
        finally{runner.Prep();runner.WatcherEnding();}
    }

    [Fact]
    public void M12_022_D4_ManualSpeedSetReachesTheInnerDriveAndThePath()
    {
        // reach: unit. The test sets Action.UseManualSpeed (+0x8C) directly: no native constructor takes the 7-arg ctor's
        // useManualSpeed argument (MISSING, see the job report) and the pose function is substituted. The runner Init,
        // the inner DriveToPoseAction and PathComponent StartNativeDrivingToPose are the real ones.
        uint mask=RunInit(true,out bool field46,out bool built);
        Assert.True(built);
        Assert.Equal(0u,mask);     // 0x0055A238: mask 0 when manual.
        Assert.True(field46);      // P4 0x0064AD44: +0x46 := manual.
    }

    [Fact]
    public void M12_022_D4_DefaultManualSpeedIsFalse()
    {
        // reach: unit. Pose function substituted; +0x8C left at the 7-arg ctor's value for the ordinary path
        // (research 20261028-pickup-children-1-read.md row 4: ordinary ctor 0x0055850C, manual false).
        uint mask=RunInit(null,out bool field46,out bool built);
        Assert.True(built);
        Assert.Equal(4u,mask);     // 0x0055A238: mask 4 unless manual.
        Assert.False(field46);     // P4 0x0064AD44: +0x46 := manual (false).
    }
}
