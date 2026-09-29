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

    /// <summary>
    /// M13-018 / Appendix G R1-2..R1-4: <c>PopulateReverseMotionPrims</c> 0x008544C0 negates the end-pose
    /// x/y, sets the end-pose theta to the forward primitive's start heading, stores the result in the
    /// bucket of the forward <em>end</em> theta, and copies the cost, the path (arc/straight) and the
    /// intermediate poses unchanged.
    /// </summary>
    [Fact]
    public void TheReflectedSetNegatesXYAndSwapsTheHeadings()
    {
        if (Set is not { } set) return;
        var fwd = set.ByAngle[0].First(p => p.ActionIndex == 2);        // slight left: (5,1,1) from heading 0
        var refl = set.Reflected[fwd.EndTheta].First(p => p.ActionIndex == 2 && p.EndX == -fwd.EndX);
        Assert.Equal(-fwd.EndX, refl.EndX);
        Assert.Equal(-fwd.EndY, refl.EndY);
        Assert.Equal(fwd.StartTheta, refl.EndTheta);                   // end theta <- forward start heading
        Assert.Equal(fwd.StartTheta, refl.StartTheta);                 // +1 copied unchanged
        Assert.Equal(fwd.Cost, refl.Cost, 6);                          // cost copied
        Assert.Equal(fwd.Arc, refl.Arc);                               // path copied
        Assert.Same(fwd.Intermediate, refl.Intermediate);              // poses copied
    }

    /// <summary>
    /// M13-003 / Appendix G R2-1..R2-3: the first intermediate pose's reciprocal is 0.0; each later one is
    /// <c>1/(halfWheelBase*|dtheta|/maxVelocity + dist)</c> with <c>dist</c> the distance to the previous
    /// pose. For the 0.5 mm short straight at heading 0 the later reciprocals are <c>1/0.5 = 2.0</c>.
    /// </summary>
    [Fact]
    public void TheIntermediateReciprocalIsTheStepFormula()
    {
        if (Set is not { } set) return;
        var prim = set.ByAngle[0].First(p => p.ActionIndex == 0);
        Assert.Equal(0.0, prim.Intermediate[0].Reciprocal, 9);
        Assert.Equal(2.0, prim.Intermediate[1].Reciprocal, 6);
        Assert.Equal(2.0, prim.Intermediate[^1].Reciprocal, 6);
    }

    /// <summary>
    /// M13-004 / C-BM13b: the cost is branched, not a sum. An arc primitive adds the arc term and not the
    /// turn term. Asset angle 0 action 2 ("slight left") has straight 7.639320225, sweep 0.463647609 and
    /// radius 94.72135955, so the cost is
    /// <c>(7.639320225 + 0.463647609*(94.72135955+24))/60 = 1.0447365786280445</c>; the old sum gave
    /// 1.23020 by also adding <c>24*0.463647609/60</c>.
    /// </summary>
    [Fact]
    public void AnArcPrimitiveCostDoesNotAddTheTurnTerm()
    {
        if (Set is not { } set) return;
        var prim = set.ByAngle[0].First(p => p.ActionIndex == 2);
        Assert.NotNull(prim.Arc);
        Assert.Equal(1.0447365786280445, prim.Cost, 9);
        Assert.NotEqual(1.23020, prim.Cost, 4);
    }
}
