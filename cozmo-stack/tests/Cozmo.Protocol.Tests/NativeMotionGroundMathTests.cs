using Cozmo.Robot.Vision;
using Xunit;
namespace Cozmo.Protocol.Tests;
public sealed class NativeMotionGroundMathTests
{
    [Fact]
    public void QuantilesUseIndependentCoordinatesAndRoundfRanks()
    {
        var pixels=new byte[30];foreach(var p in new[]{(0,0),(4,0),(1,2),(3,5)})pixels[p.Item2*5+p.Item1]=255;
        var mask=new NativeMotionImage(6,5,1,5,pixels,0);
        Assert.Equal((4u,3f,2f),NativeMotionGroundMath.Quantile(mask,.5f,.5f));
        Assert.Equal((4u,3f,5f),NativeMotionGroundMath.Quantile(mask,.5f,.95f));
    }
    [Fact]
    public void LuUsesNativeFloatOperationOrderAndPivot()
    {
        var output=new float[3];Assert.True(NativeMotionGroundMath.TrySolve(new float[]{2,1,0,1,3,0,0,0,1},new float[]{1,2,1},output));
        Assert.Equal(new uint[]{0x3E4CCCCC,0x3F19999A,0x3F800000},output.Select(BitConverter.SingleToUInt32Bits));
        Assert.True(NativeMotionGroundMath.TrySolve(new float[]{0,2,0,1,0,0,0,0,1},new float[]{4,3,1},output));Assert.Equal(new float[]{3,2,1},output);
        Assert.False(NativeMotionGroundMath.TrySolve(new float[]{1,0,0,0,1,0,0,0,0},new float[]{1,2,1},output));Assert.Equal(new float[]{0,0,0},output);
    }
}
