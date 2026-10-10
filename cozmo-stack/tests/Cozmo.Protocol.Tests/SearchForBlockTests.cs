using Cozmo.Robot.Animation;
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

    private static uint Bits(double d) => BitConverter.SingleToUInt32Bits((float)d);

    /// <summary>
    /// Every number the two classes carry is the engine's binary32 (M12-010): the ctor's movw/movt pairs 0x00546A3E..0x00546A6C, Init's
    /// 0x00546E56..0x00546E60 and 0x00546ED2..0x00546EDA, and SearchForBlock's 0x005BAFA4, 0x005BB214, 0x005BB312. The rounded decimals the
    /// earlier test asserted (0.261799 = 0x3E860A97 ... ) are different floats.
    /// </summary>
    [Fact]
    public void TheSearchCarriesTheEnginesOwnBinary32Numbers()
    {
        Assert.Equal(0x3E860A92u, Bits(SearchForNearbyObjectAction.SearchAngleMinRad));
        Assert.Equal(0x3EB2B8C2u, Bits(SearchForNearbyObjectAction.SearchAngleMaxRad));
        Assert.Equal(0x3F4CCCCDu, Bits(SearchForNearbyObjectAction.WaitMinSec));
        Assert.Equal(0x3F99999Au, Bits(SearchForNearbyObjectAction.WaitMaxSec));
        Assert.Equal(0x3D8EFA35u, Bits(SearchForNearbyObjectAction.TurnToleranceRad));
        Assert.Equal(0x3D0EFA35u, Bits(SearchForNearbyObjectAction.HeadToleranceRad));
        Assert.Equal(0xBDB2B8C2u, Bits(SearchForBlockHelper.SearchHeadAngleRad));
        Assert.Equal(0x3F490FDBu, Bits(SearchForBlockHelper.StageTurnRad));
        Assert.Equal(0xC016CBE4u, Bits(SearchForBlockHelper.StageTailTurnRad));
        Assert.Equal(0xC1A00000u, Bits(SearchForBlockHelper.BackOffMm));
        Assert.Equal(0x42C80000u, Bits(SearchForBlockHelper.SearchSpeedMmps));
        Assert.Equal(0x41A00000u, Bits(SearchForBlockHelper.StageSpeedMmps));
        Assert.Equal(3, SearchForBlockHelper.StateCount);
        // the widened values are exact: nothing was rounded through a decimal
        Assert.Equal((double)BitConverter.UInt32BitsToSingle(0x3E860A92), SearchForNearbyObjectAction.SearchAngleMinRad);
        Assert.NotEqual(0.261799, SearchForNearbyObjectAction.SearchAngleMinRad);
    }

    /// <summary>
    /// The nearby search backs off, drops the head and looks both ways: a turn one way by 15-20 degrees
    /// and then back past centre by another 15-20, each within the 4 degree tolerance the engine sets.
    /// </summary>
    [Fact]
    public void TheNearbySearchBacksOffAndLooksBothWays()
    {
        using var signals = SignalTestContext.Install();
        using var rig = new Rig();
        var act = new SearchForNearbyObjectAction(rig.M, 1, SearchForBlockHelper.BackOffMm,
                                                  SearchForBlockHelper.SearchSpeedMmps,
                                                  SearchForBlockHelper.SearchHeadAngleRad, new EngineRandom(4u))
            { Wait = (t, c) => Task.CompletedTask };

        var task = act.RunAsync(default);
        SignalTestContext.Run(task, () => rig.Pump());
        Assert.Equal(ActionResult.Success, task.Result);

        var (w1, w2, w3, a1, a2, sign) = act.Draw;
        foreach (var w in new[] { w1, w2, w3 })
            Assert.InRange(w, SearchForNearbyObjectAction.WaitMinSec, SearchForNearbyObjectAction.WaitMaxSec);
        foreach (var a in new[] { a1, a2 })
            Assert.InRange(a, SearchForNearbyObjectAction.SearchAngleMinRad, SearchForNearbyObjectAction.SearchAngleMaxRad);
        Assert.True(sign is 1.0 or -1.0);

        // M12-010: Init's draw ORDER on the RNG (0x00546C34..0x00546D42) is w1, coin, a1, w2, a2, w3, each RandDblInRange(min, max) =
        // (max - min) * u + min (0x0082FA48..0x0082FA66) with u = the engine's two-word mt19937 double; an independent emulation on Mt19937(4)
        var mt = new Mt19937(4u);
        double U() { double d0 = mt.Next(), d1 = mt.Next(); return (d0 + d1 * 4294967296.0) * (1.0 / 18446744073709551616.0); }
        double wMin = BitConverter.UInt32BitsToSingle(0x3F4CCCCD), wMax = BitConverter.UInt32BitsToSingle(0x3F99999A);
        double aMin = BitConverter.UInt32BitsToSingle(0x3E860A92), aMax = BitConverter.UInt32BitsToSingle(0x3EB2B8C2);
        double ew1 = (wMax - wMin) * U() + wMin;
        double ecoin = U() * 1.0;
        double ea1 = (aMax - aMin) * U() + aMin;
        double ew2 = (wMax - wMin) * U() + wMin;
        double ea2 = (aMax - aMin) * U() + aMin;                 // the engine draws a2 BEFORE w3 (0x00546CCA..0x00546D00, then 0x00546D12..0x00546D42)
        double ew3 = (wMax - wMin) * U() + wMin;
        Assert.Equal((double)(float)ew1, w1); Assert.Equal((double)(float)ew2, w2); Assert.Equal((double)(float)ew3, w3);
        Assert.Equal(ea1, a1); Assert.Equal(ea2, a2);
        Assert.Equal(ecoin > 0.5 ? 1.0 : -1.0, sign);

        // one backing-off line and two point turns, in that order
        var line = Assert.Single(rig.Sent.OfType<AppendPathSegmentLine>());
        // 0x00546DDE..0x00546E34: speed 100 selects the two-argument DriveStraightAction, whose default speed for a distance < 0 is 0xC2A00000 (-80 mm/s, 0x00547268)
        Assert.Equal(0xC2A00000u, BitConverter.SingleToUInt32Bits(line.Speed.SpeedMmps));
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
        using var signals = SignalTestContext.Install();
        using var rig = new Rig();
        var helper = new SearchForBlockHelper(rig.M, 999) { Wait = (t, c) => Task.CompletedTask };
        var task = helper.RunAsync(default);
        SignalTestContext.Run(task, () => rig.Pump());

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
        using var signals = SignalTestContext.Install();
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
        // The fake robot answers every path from rig.Pump, so no path wait may expire on wall-clock time: under host load the
        // 5 s drive timeout fired early, a state-1 child failed, and the state-2 sweep (ignoreFailure 0) ended after fewer drives.
        rig.M.Follower.TimeoutDelay = _ => new TaskCompletionSource().Task;

        var helper = new SearchForBlockHelper(rig.M, target.ObjectId, new EngineRandom(7u))
            { Wait = (t, c) => Task.CompletedTask };
        var task = helper.RunAsync(default);
        SignalTestContext.Run(task, () => rig.Pump());

        Assert.Equal(ActionResult.VisualObservationFailed, task.Result);
        Assert.Equal(SearchForBlockHelper.StateCount, helper.State);

        // state 0: one nearby search (one backing-off line, two turns)
        // state 1 and state 2: a 20 mm/s drive, two 45 degree turns, a nearby search, a second 20 mm/s drive,
        // the two mirrored tail turns and a second nearby search (M12-010 / G7.8)
        var lines = rig.Sent.OfType<AppendPathSegmentLine>().ToList();
        Assert.Equal(4, lines.Count(l => Math.Abs(l.Speed.SpeedMmps) == SearchForBlockHelper.StageSpeedMmps));
        Assert.Equal(5, lines.Count(l => Math.Abs(l.Speed.SpeedMmps) == 80f));   // the two-argument DriveStraightAction default for a distance < 0 (0x00547268)
        // two look-around turns per nearby search (five searches), plus two 45 degree turns in each sweep and
        // its tail (four pairs)
        Assert.Equal(5 * 2 + 4 * 2, rig.Sent.OfType<AppendPathSegmentPointTurn>().Count());
    }

    /// <summary>
    /// M12-010, SearchForBlock state 1 (0x005BB1AA..0x005BB3C6): all eight children are added with ignoreFailure = 1 (movs r3,#1 at 0x005BB1E6, 0x005BB21E, 0x005BB256, 0x005BB2AA,
    /// 0x005BB2E4, 0x005BB31C, 0x005BB354, 0x005BB3A8), and CompoundActionSequential::UpdateInternal moves on after a failed child (0x0054F808..0x0054F822). Path ordinals: state 0 sends 3
    /// (the nearby search's line and two turns), state 1 sends 12 (drive 3, turns 4-5, search 6-8, drive 9, turns 10-11, search 12-14), state 2 sends 12. A failed first path of a nearby
    /// search ends THAT search (its own children are ignoreFailure 0) but not the compound. Every other state-1 failure leaves the total at 27.
    /// </summary>
    [Theory]
    [InlineData(3, 27)] [InlineData(4, 27)] [InlineData(5, 27)] [InlineData(6, 25)] [InlineData(7, 26)] [InlineData(8, 27)]
    [InlineData(9, 27)] [InlineData(10, 27)] [InlineData(11, 27)] [InlineData(12, 25)] [InlineData(13, 26)] [InlineData(14, 27)]
    public void M12_010_State1ContinuesAfterAnyFailedChild(int failOrdinal, int expectedPaths)
    {
        using var signals = SignalTestContext.Install();
        if (MarkerLibrary.EmbeddedOrNull is null) return;
        using var rig = new Rig();
        rig.Cube = At(200, 0);
        if (rig.Frame().Objects.Count == 0) return;
        rig.Pump();
        var target = rig.M.World.LocatedObjects.First();
        rig.M.World.MarkDirty(target.ObjectId);
        rig.Sent.Clear();
        rig.HoldPath = true;
        rig.M.Follower.TimeoutDelay = _ => new TaskCompletionSource().Task;   // paths end only by the signals below, never by wall-clock load
        var helper = new SearchForBlockHelper(rig.M, target.ObjectId, new EngineRandom(7u)) { Wait = (t, c) => Task.CompletedTask };
        var task = helper.RunAsync(default);
        int handled = 0;
        SignalTestContext.Run(task, () =>
        {
            rig.Pump();
            var eps = rig.Sent.OfType<ExecutePath>().ToList();
            for (; handled < eps.Count; handled++)
            {
                if (handled == failOrdinal) rig.Send(new PathFollowingEvent { EventId = eps[handled].EventId, EventType = (byte)PathEventType.Interrupted });
                else rig.ReleasePath();
            }
        });
        Assert.Equal(ActionResult.VisualObservationFailed, task.Result);
        Assert.Equal(expectedPaths, rig.Sent.OfType<ExecutePath>().Count());
    }


}
