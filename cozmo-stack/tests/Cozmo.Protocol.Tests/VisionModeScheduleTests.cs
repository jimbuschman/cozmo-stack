using Cozmo.Robot;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M11-021 and M11-034: the vision-mode schedule mechanism and the per-frame marker-mode gate. Every
/// expected value comes from the inventory's citations (InitDefaultSchedules 0x006AEFDE, the bool vector at
/// schedule+0 and the wrapping counter at schedule+0xc read by CheckTimeToProcessAndAdvance 0x006AF1F8,
/// ShouldProcessVisionMode 0x006B5AA4, and the shipped vision_config.json), never from the code under test.
/// </summary>
public class VisionModeScheduleTests
{
    /// <summary>M11-034: <c>InitDefaultSchedules</c> 0x006AEFDE fills all 16 modes with a single true and counter 0.</summary>
    [Fact]
    public void TheDefaultScheduleHasSixteenModesEachAlwaysTrue()
    {
        var schedules = AllVisionModesSchedule.InitDefaultSchedules();
        Assert.Equal(16, AllVisionModesSchedule.ModeCount);
        for (int mode = 0; mode < AllVisionModesSchedule.ModeCount; mode++)
        {
            var s = schedules[mode];
            Assert.Equal(new[] { true }, s.Frames);
            Assert.Equal(0, s.Counter);
            // a one-element {true} schedule returns true on every call (0x006AF1F8)
            Assert.True(s.CheckTimeToProcessAndAdvance());
            Assert.True(s.CheckTimeToProcessAndAdvance());
            Assert.Equal(0, s.Counter);
        }
        Assert.Same(AllVisionModesSchedule.Default, AllVisionModesSchedule.Default);
    }

    /// <summary>
    /// M11-034: <c>CheckTimeToProcessAndAdvance</c> returns the bool vector's current bit and advances a
    /// wrapping counter. Over one full cycle every bit is returned exactly once and the counter returns to 0.
    /// </summary>
    [Fact]
    public void AScheduleReturnsItsBitsInOrderAndWraps()
    {
        var allFalse = new VisionModeSchedule(new[] { false, false });
        Assert.False(allFalse.CheckTimeToProcessAndAdvance());
        Assert.False(allFalse.CheckTimeToProcessAndAdvance());
        Assert.Equal(0, allFalse.Counter);

        var half = new VisionModeSchedule(new[] { true, false });
        bool first = half.CheckTimeToProcessAndAdvance();
        bool second = half.CheckTimeToProcessAndAdvance();
        Assert.True(first ^ second);                    // exactly one of the two frames is true
        Assert.Equal(0, half.Counter);                  // wrapped after two calls
        bool third = half.CheckTimeToProcessAndAdvance();
        Assert.Equal(first, third);                     // the cycle repeats
    }

    /// <summary>
    /// M11-021: <c>ShouldProcessVisionMode(mode)</c> 0x006B5AA4 is the mode's bit in the bitmask at
    /// <c>VisionSystem+0xac</c> AND the front schedule's bit. A clear bit returns false without consulting
    /// the schedule.
    /// </summary>
    [Fact]
    public void TheGateIsTheEnableBitAndTheSchedule()
    {
        using var robot = CozmoRobot.CreateOffline();
        using var vision = new VisionSystem(robot, CameraCalibration.Nominal());
        Assert.Equal(VisionSystem.ShippedModeEnableMask, vision.ModeEnableMask);

        // enabled marker mode on the default schedule runs every frame
        Assert.True(vision.ShouldProcessVisionMode(VisionSystem.DetectingMarkers));

        // a mode whose bit is clear is refused
        vision.ModeEnableMask = 0;
        Assert.False(vision.ShouldProcessVisionMode(VisionSystem.DetectingMarkers));

        // a mode whose bit is set but whose schedule is {false} is refused
        vision.ModeEnableMask = 1 << 2;
        var modes = new VisionModeSchedule[AllVisionModesSchedule.ModeCount];
        for (int i = 0; i < modes.Length; i++) modes[i] = new VisionModeSchedule(new[] { true });
        modes[2] = new VisionModeSchedule(new[] { false });
        vision.Schedules = new AllVisionModesSchedule(modes);
        Assert.False(vision.ShouldProcessVisionMode(2));
        // and a mode not in the mask is refused even though its schedule is {true}
        Assert.False(vision.ShouldProcessVisionMode(3));
    }

    /// <summary>M11-034 (C2.3): the ids fixed by <c>VisionModeFromString</c> 0x796BD4.</summary>
    [Fact]
    public void TheVisionModeIdsAreTheEngines()
    {
        Assert.Equal(0, (int)VisionMode.Idle);
        Assert.Equal(1, (int)VisionMode.DetectingMarkers);
        Assert.Equal(2, (int)VisionMode.DetectingFaces);
        Assert.Equal(3, (int)VisionMode.DetectingMotion);
        Assert.Equal(4, (int)VisionMode.DetectingOverheadEdges);
        Assert.Equal(5, (int)VisionMode.ReadingToolCode);
        Assert.Equal(6, (int)VisionMode.ComputingCalibration);
        Assert.Equal(7, (int)VisionMode.CheckingQuality);
        Assert.Equal(8, (int)VisionMode.ComputingStatistics);
        Assert.Equal(9, (int)VisionMode.DetectingPets);
        Assert.Equal(10, (int)VisionMode.EstimatingFacialExpression);
        Assert.Equal(11, (int)VisionMode.DetectingSmileAmount);
        Assert.Equal(12, (int)VisionMode.DetectingGaze);
        Assert.Equal(13, (int)VisionMode.DetectingBlinkAmount);
        Assert.Equal(14, (int)VisionMode.LimitedExposure);
        Assert.Equal(15, (int)VisionMode.DetectingLaserPoints);
        Assert.Equal(16, (int)VisionMode.Count);
    }

    /// <summary>
    /// M11-034 (C2.3): the shipped <c>vision_config.json</c> enables markers 1, faces 2, motion 3,
    /// overhead edges 4, quality 7, statistics 8, pets 9 and laser points 15 (bits
    /// 0x0002/0x0004/0x0008/0x0010/0x0080/0x0100/0x0200/0x8000) and disables 10..13.
    /// </summary>
    [Fact]
    public void TheShippedModeEnableMaskEnablesTheConfiguredModes()
    {
        const int expected = 0x0002 | 0x0004 | 0x0008 | 0x0010 | 0x0080 | 0x0100 | 0x0200 | 0x8000;
        Assert.Equal(expected, VisionSystem.ShippedModeEnableMask);

        using var robot = CozmoRobot.CreateOffline();
        using var vision = new VisionSystem(robot, CameraCalibration.Nominal());
        Assert.Equal(expected, vision.ModeEnableMask);
        foreach (var mode in new[]
        {
            VisionMode.DetectingMarkers, VisionMode.DetectingFaces, VisionMode.DetectingMotion,
            VisionMode.DetectingOverheadEdges, VisionMode.CheckingQuality, VisionMode.ComputingStatistics,
            VisionMode.DetectingPets, VisionMode.DetectingLaserPoints,
        })
            Assert.True(vision.ShouldProcessVisionMode((int)mode), mode.ToString());
        foreach (var mode in new[]
        {
            VisionMode.EstimatingFacialExpression, VisionMode.DetectingSmileAmount,
            VisionMode.DetectingGaze, VisionMode.DetectingBlinkAmount,
        })
            Assert.False(vision.ShouldProcessVisionMode((int)mode), mode.ToString());
    }

    /// <summary>
    /// M11-039: <c>VisionSystem::UpdateCameraCalibration</c> 0x006B1E3E installs the calibration and, on
    /// success, calls <c>MarkerDetector::Init</c> 0x006B1E78, which runs <c>Parameters::Initialize</c>
    /// 0x008752F8. The detector's parameters therefore go back to the shipped values on an install.
    /// </summary>
    [Fact]
    public void TheCalibrationInstallReinitialisesTheDetectorParameters()
    {
        using var robot = CozmoRobot.CreateOffline();
        var modified = new QuadDetector(new QuadDetectorParameters { MinQuadArea = 999, MinContrastRatio = 2.5 });
        var detector = new MarkerDetector(modified, new MarkerDecoder());
        using var vision = new VisionSystem(robot, CameraCalibration.Nominal(), detector);
        Assert.Equal(999, vision.Detector.Quads.Parameters.MinQuadArea);

        vision.UpdateCameraCalibration(CameraCalibration.Nominal());

        // the shipped Initialize values (0x008752F8): minQuadArea 25 at +0x30, contrast ratio 1.01 at +0x3C
        Assert.Equal(25, vision.Detector.Quads.Parameters.MinQuadArea);
        Assert.Equal(1.01, vision.Detector.Quads.Parameters.MinContrastRatio);
        Assert.NotNull(vision.Calibration);
    }
}