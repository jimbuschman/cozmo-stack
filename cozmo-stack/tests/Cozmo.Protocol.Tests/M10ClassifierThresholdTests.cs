using Cozmo.Robot;
using Xunit;

namespace Cozmo.Protocol.Tests;

public class M10ClassifierThresholdTests
{
    // reach: unit (literal inspection; independently checked native words, not a second production-derived oracle).
    [Fact]
    public void ThresholdConstantsMatchAllSixNativeWords()
    {
        Assert.Equal(0x3FF5BE0Bu, BitConverter.SingleToUInt32Bits(OffTreadsClassifier.OnFacePitchHighRad));
        Assert.Equal(0xBFB2B8C2u, BitConverter.SingleToUInt32Bits(OffTreadsClassifier.OnFacePitchLowRad));
        Assert.Equal(0x3F490FDBu, BitConverter.SingleToUInt32Bits(OffTreadsClassifier.LevelPitchRad));
        Assert.Equal(0x3FFAEB8260000000L, BitConverter.DoubleToInt64Bits(OffTreadsClassifier.OnBackCentreSimulatedRad));
        Assert.Equal(0x3FF4CDE840000000L, BitConverter.DoubleToInt64Bits(OffTreadsClassifier.OnBackCentrePhysicalRad));
        Assert.Equal(0x3FD0C15240000000L, BitConverter.DoubleToInt64Bits(OffTreadsClassifier.OnBackHalfWidthRad));
    }

    private static RobotState State(uint pitch, uint now) => new()
    {
        Timestamp = now,
        Pose = new RobotPose { Pitch = BitConverter.UInt32BitsToSingle(pitch) },
        Status = (uint)RobotStatusFlag.IsPickedUp,
        Accel = new AccelData { X = 9800 }, // no Y-axis side candidate
        Gyro = new GyroData()
    };

    // reach: unit (real classifier.Update, calibrated physical inputs and explicit BaseStationTimer-ms clock;
    // no packet-routing, downstream owner or hardware claim).
    // 0x511E7C..EFC widens pitch and subtracts the f64 center; 0x511FAE..FBA compares f64 half width.
    // Literal IEEE arithmetic from the native words places the upper boundary between3FC7F1E6 and3FC7F1E7.
    [Theory]
    [InlineData(0x3FC7F1E0u, true)]
    [InlineData(0x3FC7F1E1u, true)] // checked witness: outside old decimal band, inside native band
    [InlineData(0x3FC7F1E2u, true)]
    [InlineData(0x3FC7F1E6u, true)]
    [InlineData(0x3FC7F1E7u, false)]
    public void PhysicalBackBoundaryUsesExactDoubleBandAndNativeOneSecondCommit(uint pitch, bool onBack)
    {
        var c = new OffTreadsClassifier { HeadCalibrated = true, IsPhysical = true };
        bool first = c.Update(State(pitch, 2000), 2000);
        Assert.Equal(onBack ? OffTreadsState.OnBack : OffTreadsState.InAir, c.Candidate);
        Assert.Equal(onBack ? 2750u : 1750u, c.CandidateTimeMs); // +750 for back; picked-up fallback now-250
        Assert.Equal(!onBack, first);
        Assert.False(c.Update(State(pitch, 2999), 2999));
        Assert.Equal(onBack ? OffTreadsState.OnTreads : OffTreadsState.InAir, c.Current);
        Assert.Equal(onBack, c.Update(State(pitch, 3000), 3000));
        Assert.Equal(onBack ? OffTreadsState.OnBack : OffTreadsState.InAir, c.Current);
    }

    // reach: unit (literal independent witness; old decimals are an explicit regression baseline).
    [Fact]
    public void CheckedWitnessWasOutsideTheRoundedDecimalInterval()
    {
        double pitch = BitConverter.UInt32BitsToSingle(0x3FC7F1E1);
        Assert.False(Math.Abs(pitch - 1.30027) <= 0.261799);
        // Native center/width from the checked literal pool, not from OffTreadsClassifier constants.
        Assert.True(Math.Abs(pitch - BitConverter.Int64BitsToDouble(0x3FF4CDE840000000L))
            <= BitConverter.Int64BitsToDouble(0x3FD0C15240000000L));
    }

    // reach: unit (real classifier.Update with a prior committed OnBack candidate, picked-up flag and native clock).
    // 0x51222C..230 compares |pitch| in f32. A level pitch changes the candidate to InAir with the250ms debounce;
    // above the boundary, pickup retains the prior OnBack candidate. This exercises the actual candidate/tail rules.
    [Theory]
    [InlineData(0x3F490FDAu, true)]
    [InlineData(0x3F490FDBu, true)]
    [InlineData(0x3F490FDCu, false)]
    public void LevelBoundaryUsesFloatThresholdAndExistingPickupCandidateSemantics(uint pitch, bool level)
    {
        var c = new OffTreadsClassifier { HeadCalibrated = true, IsPhysical = true };
        Assert.False(c.Update(State(0x3FA66F42, 1000), 1000)); // physical center narrowed to f32
        Assert.True(c.Update(State(0x3FA66F42, 2000), 2000));
        Assert.Equal(OffTreadsState.OnBack, c.Current);
        Assert.False(c.Update(State(pitch, 3000), 3000));
        Assert.Equal(level ? OffTreadsState.InAir : OffTreadsState.OnBack, c.Candidate);
        Assert.Equal(level ? 3000u : 1750u, c.CandidateTimeMs);
        Assert.False(c.Update(State(pitch, 3249), 3249));
        Assert.Equal(OffTreadsState.OnBack, c.Current);
        Assert.Equal(level, c.Update(State(pitch, 3250), 3250));
        Assert.Equal(level ? OffTreadsState.InAir : OffTreadsState.OnBack, c.Current);
    }
}
