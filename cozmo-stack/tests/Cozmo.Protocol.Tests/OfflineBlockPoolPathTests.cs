using System.Reflection;
using Cozmo.Robot;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// Host plumbing: a robot built by CreateOffline or CreateForTest never talks to a real Cozmo, so with no explicit BlockPoolPath its
/// BlockFilter gets the no-path case ("", Save 0x0061B014 then writes nothing) and the user's real pool file
/// (CozmoRobot.DefaultBlockPoolPath) is never read or written. The operator's pool held the test-only id 0xAABBCCDD
/// (research/20261103-idle-robot-run-4-claude-check.md). An explicit path is still used.
/// </summary>
public sealed class OfflineBlockPoolPathTests
{
    private static string PoolPathAfterInit(CozmoRobot robot)
    {
        typeof(CozmoRobot).GetMethod("LoadBlockPool", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(robot, null);
        var connections = robot.Cubes.Connections;
        return (string)connections.GetType().GetField("_poolPath", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(connections)!;
    }

    [Fact]
    public void AnOfflineRobotWithNoPathKeepsNoPoolFile()
    {
        using var robot = CozmoRobot.CreateOffline();
        Assert.Equal("", PoolPathAfterInit(robot));
    }

    [Fact]
    public void ATestRobotWithNoPathKeepsNoPoolFile()
    {
        using var robot = CozmoRobot.CreateForTest(new M4ControlTests.FakePort(), () => 1);
        Assert.Equal("", PoolPathAfterInit(robot));
    }

    [Fact]
    public void AnExplicitPathIsStillUsed()
    {
        var path = Path.Combine(Path.GetTempPath(), "cozmo-pool-" + Guid.NewGuid().ToString("N") + ".txt");
        using var robot = CozmoRobot.CreateOffline(engineOptions: new CozmoEngineOptions { BlockPoolPath = path });
        Assert.Equal(path, PoolPathAfterInit(robot));
    }
}
