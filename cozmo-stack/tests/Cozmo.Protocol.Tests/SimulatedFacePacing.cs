using Cozmo.Robot;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// For tests that drive a robot through simulated time. <see cref="CozmoDisplay"/> paces raw face frames with a
/// real 33.3 ms sleep, so a test stepping idle in 20 ms simulated ticks ran in real time: a four-minute idle took
/// about three and a half real minutes. This gives the display a clock of its own that the pacing wait advances,
/// so the frames keep their spacing without any real waiting. The real pacing keeps its own test in DeviceTests.
/// </summary>
internal static class SimulatedFacePacing
{
    public static CozmoRobot Use(CozmoRobot robot)
    {
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        robot.Display.UtcNow = () => now;
        robot.Display.Wait = d => now += d;
        return robot;
    }
}
