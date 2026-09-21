using Cozmo.Protocol;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// The search the app runs when a cube is not where it thought (fidelity manifest M12-010):
/// <c>SearchForNearbyObjectAction</c> and the three states of <c>SearchForBlockHelper::SearchForBlock</c>.
/// </summary>
public class SearchForBlockTests
{
    private static Pose3d At(double x, double y, double angle = 0) =>
        new(Mat3.AboutZ(angle), new Vec3(x, y, 0));

    /// <summary>Every number the two classes carry is the engine's.</summary>
    [Fact]
    public void TheSearchCarriesTheEnginesOwnNumbers()
    {
        Assert.Equal(0.261799, SearchForNearbyObjectAction.SearchAngleMinRad, 6);
        Assert.Equal(0.349066, SearchForNearbyObjectAction.SearchAngleMaxRad, 6);
        Assert.Equal(0.8, SearchForNearbyObjectAction.WaitMinSec, 6);
        Assert.Equal(1.2, SearchForNearbyObjectAction.WaitMaxSec, 6);
        Assert.Equal(0.0698132, SearchForNearbyObjectAction.TurnToleranceRad, 6);
        Assert.Equal(0.0349066, SearchForNearbyObjectAction.HeadToleranceRad, 6);

        Assert.Equal(-20.0, SearchForBlockHelper.BackOffMm);
        Assert.Equal(100f, SearchForBlockHelper.SearchSpeedMmps);
        Assert.Equal(20f, SearchForBlockHelper.StageSpeedMmps);
        Assert.Equal(-0.0872665, SearchForBlockHelper.SearchHeadAngleRad, 6);
        Assert.Equal(0.785398, SearchForBlockHelper.StageTurnRad, 6);
        Assert.Equal(3, SearchForBlockHelper.StateCount);
    }

    /// <summary>
    /// The nearby search backs off, drops the head and looks both ways: a turn one way by 15-20 degrees
    /// and then back past centre by another 15-20, each within the 4 degree tolerance the engine sets.
    /// </summary>
    [Fact]
    public void TheNearbySearchBacksOffAndLooksBothWays()
    {
        using var rig = new Rig();
        var act = new SearchForNearbyObjectAction(rig.M, 1, SearchForBlockHelper.BackOffMm,
                                                  SearchForBlockHelper.SearchSpeedMmps,
                                                  SearchForBlockHelper.SearchHeadAngleRad, new Random(4))
            { Wait = (t, c) => Task.CompletedTask };

        var task = act.RunAsync(default);
        SpinUntil(() => task.IsCompleted, () => rig.Pump(), 10_000);
        Assert.Equal(ActionResult.Success, task.Result);

        var (w1, w2, w3, a1, a2, sign) = act.Draw;
        foreach (var w in new[] { w1, w2, w3 })
            Assert.InRange(w, SearchForNearbyObjectAction.WaitMinSec, SearchForNearbyObjectAction.WaitMaxSec);
        foreach (var a in new[] { a1, a2 })
            Assert.InRange(a, SearchForNearbyObjectAction.SearchAngleMinRad, SearchForNearbyObjectAction.SearchAngleMaxRad);
        Assert.True(sign is 1.0 or -1.0);

        // one backing-off line and two point turns, in that order
        var line = Assert.Single(rig.Sent.OfType<AppendPathSegmentLine>());
        Assert.Equal(-SearchForBlockHelper.SearchSpeedMmps, line.Speed.SpeedMmps);
        var turns = rig.Sent.OfType<AppendPathSegmentPointTurn>().ToList();
        Assert.Equal(2, turns.Count);
        Assert.Equal((float)SearchForNearbyObjectAction.TurnToleranceRad, turns[0].AngleToleranceRad, 5);

        // the head went to -5 degrees
        Assert.Contains(rig.Sent, m => m is SetHeadAngle);
    }

    /// <summary>
    /// The helper stops before it starts when the world has already dropped the target: there is nothing
    /// to search for by id, which is the branch at 0x005BB05E.
    /// </summary>
    [Fact]
    public void AnUnlocatedTargetEndsTheSearchAtOnce()
    {
        using var rig = new Rig();
        var helper = new SearchForBlockHelper(rig.M, 999) { Wait = (t, c) => Task.CompletedTask };
        var task = helper.RunAsync(default);
        SpinUntil(() => task.IsCompleted, () => rig.Pump(), 5_000);

        Assert.Equal(ActionResult.BadObject, task.Result);
        Assert.Equal(0, helper.State);
        Assert.DoesNotContain(rig.Sent, m => m is AppendPathSegmentPointTurn);
    }

    /// <summary>
    /// With the target located but its pose never becoming Known, the helper runs all three states and
    /// then gives up: state 0 is the nearby search alone, and states 1 and 2 bracket it with a 20 mm
    /// backing-off drive and two 45 degree turns each way.
    /// </summary>
    [Fact]
    public void TheSearchRunsItsThreeStatesAndThenGivesUp()
    {
        if (MarkerLibrary.EmbeddedOrNull is null) return;
        using var rig = new Rig();
        rig.Cube = At(200, 0);
        var seen = rig.Frame();
        if (seen.Objects.Count == 0) return;
        rig.Pump();
        var target = rig.M.World.LocatedObjects.First();
        // located, but the pose is not Known: the search has something to look for and never finds it
        rig.M.World.MarkDirty(target.ObjectId);
        rig.Sent.Clear();

        var helper = new SearchForBlockHelper(rig.M, target.ObjectId, new Random(7))
            { Wait = (t, c) => Task.CompletedTask };
        var task = helper.RunAsync(default);
        SpinUntil(() => task.IsCompleted, () => rig.Pump(), 30_000);

        Assert.Equal(ActionResult.VisualObservationFailed, task.Result);
        Assert.Equal(SearchForBlockHelper.StateCount, helper.State);

        // state 0: one nearby search (one backing-off line, two turns)
        // states 1 and 2: a 20 mm/s drive, two 45 degree turns, a nearby search, a second 20 mm/s drive
        var lines = rig.Sent.OfType<AppendPathSegmentLine>().ToList();
        Assert.Equal(4, lines.Count(l => Math.Abs(l.Speed.SpeedMmps) == SearchForBlockHelper.StageSpeedMmps));
        Assert.Equal(3, lines.Count(l => Math.Abs(l.Speed.SpeedMmps) == SearchForBlockHelper.SearchSpeedMmps));
        // two look-around turns per nearby search, plus two 45 degree turns in each sweep
        Assert.Equal(3 * 2 + 2 * 2, rig.Sent.OfType<AppendPathSegmentPointTurn>().Count());
    }

    private static void SpinUntil(Func<bool> done, Action pump, int ms)
    {
        var end = DateTime.UtcNow.AddMilliseconds(ms);
        while (DateTime.UtcNow < end && !done()) { pump(); Thread.Sleep(2); }
    }
}
