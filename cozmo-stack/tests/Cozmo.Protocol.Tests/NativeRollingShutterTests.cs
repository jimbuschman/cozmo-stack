using Cozmo.Robot.Vision;
using Xunit;
namespace Cozmo.Protocol.Tests;

public sealed class NativeRollingShutterTests
{
    [Fact]
    public void EmptyHistoryProducesAllZeroShiftsAndWarningFlag()
    {
        var shifts=NativeRollingShutter.Compute(1000,240,Array.Empty<NativeImageImu>(),false,out bool incomplete);
        Assert.True(incomplete);Assert.Equal(120,shifts.Length);Assert.All(shifts,s=>Assert.Equal(new NativeRollingShutter.Shift(0,0),s));
    }
    [Fact]
    public void NativeInterpolationAndConstantCumulativeLiterals()
    {
        var records=new[]{new NativeImageImu(1,0,2,3,0,900),new NativeImageImu(1,0,4,5,120,980)};
        Assert.True(NativeRollingShutter.Step(records,950,60,240,false,out var x));
        Assert.Equal(0x3F3BBBBCu,BitConverter.SingleToUInt32Bits(x.X));Assert.Equal(0u,BitConverter.SingleToUInt32Bits(x.Y));
        Assert.True(NativeRollingShutter.Step(records,950,60,240,true,out var xy));
        Assert.Equal(0x3F0CCCCDu,BitConverter.SingleToUInt32Bits(xy.Y));
        Assert.False(NativeRollingShutter.Step(records,900,60,240,false,out var first));Assert.Equal(default,first);
        Assert.True(NativeRollingShutter.Step(records,981,60,240,false,out var last));
        Assert.Equal(0x3F6AAAABu,BitConverter.SingleToUInt32Bits(last.X));
        var constant=new[]{new NativeImageImu(1,0,0,3,0,1)};
        var shifts=NativeRollingShutter.Compute(1000,240,constant,false,out var incomplete);
        Assert.False(incomplete);Assert.Equal(0x4283FFF9u,BitConverter.SingleToUInt32Bits(shifts[^1].X));
    }
    [Fact]
    public void MarkerCorrectionUsesRawRowAndDoesNotMutateRawCorners()
    {
        var raw=new[]{new Vec2(10,0),new Vec2(11,0),new Vec2(10,2),new Vec2(11,2)};
        var marker=new ObservedMarker((MarkerType)1,raw,7,0,0);
        var shifts=Enumerable.Repeat(new NativeRollingShutter.Shift(1,0),120).ToArray();
        Assert.True(NativeRollingShutter.TryCorrect(marker,320,240,shifts,out var corrected,out var bad));
        Assert.False(bad);Assert.Equal(9d,corrected.Corners[0].X);Assert.Equal(10d,raw[0].X);
        shifts[0]=new NativeRollingShutter.Shift(float.NaN,0);
        Assert.True(NativeRollingShutter.TryCorrect(marker,320,240,shifts,out corrected,out bad));Assert.True(double.IsNaN(corrected.Corners[0].X));
        raw[0]=new Vec2(10,240);
        Assert.False(NativeRollingShutter.TryCorrect(marker,320,240,shifts,out _,out bad));Assert.True(bad);
    }
}
