using Cozmo.Robot;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;
using Xunit.Abstractions;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class GotoObstacleReplayTests(ITestOutputHelper output)
{
    [Fact]
    public void CapturedGotoPlanImportsTheKnownCubeWithSoftPadding()
    {
        using var fixture = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        var owner = fixture.Robot;
        var world = fixture.M.World;
        var root = owner.GetNativeOrigin(owner.CurrentOriginId);
        // Last delivered 0x44 before the 134649.2398 ms planner call in M15-CUBE-HW-b22d48.
        var seen = new EngineGamePose(283.6293f, -41.535854f, 19.450602f,
            -0.110112466f, -4.529386e-8f, 3.3410828e-8f, -0.99391913f, 1);
        using var cubePose = new Pose3d(new Rotation3d(seen.Q0, seen.Q1, seen.Q2, seen.Q3),
            new Vec3(seen.X, seen.Y, seen.Z), parent: root);
        var cube = new ObservableObject(0, ObjectType.Block_LIGHTCUBE2, CubeGeometry.MarkersFor(ObjectType.Block_LIGHTCUBE2));
        cube.InitPose(cubePose, PoseState.Known);
        fixture.Rig.Robot.CameraSettings.LastProcessedImageTimestamp = 125440;
        world.AddLocatedObject(cube);

        var source = new List<SourceMapData>();
        world.Maps.Current!.FindContentIf(data => data.Type == MemoryMapContentType.ObstacleObservable ? 1 : 0, source);
        var payload = Assert.IsType<ObservableMapData>(Assert.Single(source));
        Assert.Equal(0u, payload.ObjectId);
        output.WriteLine("Input cube polygon: " + string.Join(", ", payload.Polygon));

        var obb = Path.GetFullPath(Path.Combine(PickupPyramidNativeExecutionTests.Resources(), "..", ".."));
        var primitives = Assert.IsType<MotionPrimitiveSet>(MotionPrimitiveSet.FromObb(obb));
        var planner = new LatticePlanner(new LatticeEnvironment(primitives));
        Assert.Null(owner.CarryingOwner!.CarriedObjectId);
        var start = new StateC(183.233f, -19.5141f, -0.234302f);
        const float yaw = 1.3364791f;
        // M12-032: the no-carry branch shifts the requested base-station goal by -20 mm along its yaw.
        var goal = new StateC(397.22083f - 20f * MathF.Cos(yaw), -70.59313f - 20f * MathF.Sin(yaw), yaw);
        var clear = Assert.IsType<LatticePlan>(planner.ComputePath(start, [goal]));
        Assert.True(planner.ImportBlockWorldObstacles(world, null, tightPadding: false));
        Assert.Equal(1, planner.Env.ObstacleCount);
        Assert.Equal((7f, 6f), (planner.Env.RobotPadding, planner.Env.ObstaclePadding));
        Assert.All(Enumerable.Range(0, primitives.NumAngles),
            heading => Assert.Equal(0.1f, Assert.Single(planner.Env.Bucket(heading)).Penalty));
        var withCube = Assert.IsType<LatticePlan>(planner.ComputePath(start, [goal]));
        Assert.True(withCube.States().Min(state => state.Y) < clear.States().Min(state => state.Y));
        Assert.InRange(Assert.Single(withCube.Penalties.Where(penalty => penalty > 0)), 1.79f, 1.80f);
        output.WriteLine($"Empty map {clear.Expansions} states, cost {clear.Cost}: {string.Join(" ", clear.States())}");
        output.WriteLine($"Cube map {withCube.Expansions} states, cost {withCube.Cost}: {string.Join(" ", withCube.States())}");
        output.WriteLine("Soft penalties: " + string.Join(" ", withCube.Penalties));
    }
}
