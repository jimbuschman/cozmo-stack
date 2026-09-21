using Cozmo.Robot.Manipulation;
using Xunit;

namespace Cozmo.Protocol.Tests;

// The motion primitive file, which turns out to state outright what this stack had been deriving
// (fidelity manifest M13-004).
public class MotionPrimitiveTests
{
    private static MotionPrimitiveSet? Set =>
        WwiseAssets.ObbRoot is { } obb ? MotionPrimitiveSet.FromObb(obb) : null;

    /// <summary>
    /// The nine actions and their cost factors, as the file lists them. An in-place turn costs twice a
    /// plain one; the short straight is a hair more than the long one so the planner prefers long runs;
    /// driving backwards costs 1.2.
    /// </summary>
    [Fact]
    public void TheNineActionsCarryTheEnginesCostFactors()
    {
        if (Set is not { } set) return;
        Assert.Equal(9, set.Actions.Count);
        Assert.Equal(1.0001, set.Actions[0].ExtraCostFactor, 6);   // short straight
        Assert.Equal(1.0, set.Actions[1].ExtraCostFactor, 6);      // long straight
        Assert.Equal(1.0, set.Actions[4].ExtraCostFactor, 6);      // hard left
        Assert.Equal(2.0, set.Actions[6].ExtraCostFactor, 6);      // inplace left
        Assert.Equal(2.0, set.Actions[7].ExtraCostFactor, 6);      // inplace right
        Assert.Equal(1.2, set.Actions[8].ExtraCostFactor, 6);      // backwards short straight
        Assert.True(set.Actions[8].Reverse);
        Assert.False(set.Actions[1].Reverse);
    }

    /// <summary>
    /// A turning primitive carries its own straight run and its own arc, and the arc closes exactly on
    /// the primitive's end cell - which is why there is nothing to reconstruct.
    /// </summary>
    [Theory]
    [InlineData(2, 94.72135954999578, 0.4636476090008061)]   // slight left
    [InlineData(3, 94.72135954999578, -0.4636476090008061)]  // slight right
    [InlineData(4, 34.14213562373096, 0.7853981633974483)]   // hard left
    [InlineData(5, 34.14213562373096, -0.7853981633974483)]  // hard right
    public void ATurningPrimitiveArcEndsOnItsEndCell(int actionIndex, double radius, double sweep)
    {
        if (Set is not { } set) return;
        var prim = set.ByAngle[0].First(p => p.ActionIndex == actionIndex);
        Assert.NotNull(prim.Arc);
        var arc = prim.Arc!.Value;
        Assert.Equal(radius, arc.Radius, 6);
        Assert.Equal(sweep, arc.SweepRad, 6);

        double endAngle = arc.StartRad + arc.SweepRad;
        double ex = arc.CenterX + arc.Radius * Math.Cos(endAngle);
        double ey = arc.CenterY + arc.Radius * Math.Sin(endAngle);
        Assert.Equal(prim.EndX * set.ResolutionMm, ex, 3);
        Assert.Equal(prim.EndY * set.ResolutionMm, ey, 3);

        // and the arc begins where the straight run ends
        Assert.Equal(prim.StraightLengthMm, arc.CenterX + arc.Radius * Math.Cos(arc.StartRad), 3);
        Assert.Equal(0.0, arc.CenterY + arc.Radius * Math.Sin(arc.StartRad), 3);
    }

    /// <summary>
    /// The arc is stored already rotated for its starting heading: the same slight left at heading 90
    /// has its centre at (-94.721, 7.639) with a start angle of zero, not the heading-0 numbers.
    /// </summary>
    [Fact]
    public void TheArcIsStoredInTheWorldFrameForItsStartingHeading()
    {
        if (Set is not { } set) return;
        var at90 = set.ByAngle[4].First(p => p.ActionIndex == 2).Arc!.Value;
        Assert.Equal(-94.72135954999578, at90.CenterX, 6);
        Assert.Equal(7.639320225002117, at90.CenterY, 6);
        Assert.Equal(0.0, at90.StartRad, 6);
    }

    /// <summary>The in-place turns carry a direction and no arc at all.</summary>
    [Fact]
    public void TheInPlaceTurnsCarryADirectionAndNoArc()
    {
        if (Set is not { } set) return;
        var left = set.ByAngle[0].First(p => p.ActionIndex == 6);
        var right = set.ByAngle[0].First(p => p.ActionIndex == 7);
        Assert.Null(left.Arc);
        Assert.Null(right.Arc);
        Assert.Equal(1.0, left.TurnInPlaceDirection);
        Assert.Equal(-1.0, right.TurnInPlaceDirection);
        Assert.Equal(0.0, left.StraightLengthMm);
    }
}
