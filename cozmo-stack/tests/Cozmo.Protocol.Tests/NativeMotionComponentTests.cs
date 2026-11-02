using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

public sealed class NativeMotionComponentTests
{
    private static NativeMotionImage Mask(int rows,int columns,int stride,params byte[] bytes)=>new(rows,columns,1,stride,bytes,0);

    [Fact]
    public void DiagonalPixelsJoinAndDisconnectedBackgroundIsOneLabel()
    {
        var stats=new List<NativeMotionComponent>();
        Assert.True(NativeMotionComponents.TryAppend(Mask(2,2,3,1,0,99,0,2,99),stats,out var labels));
        Assert.Equal(new[]{1,0,0,1},labels);
        Assert.Equal(2,stats.Count);
        Assert.Equal(new NativeMotionComponent(2,.5f,.5f,0,0,2,2),stats[0]);
        Assert.Equal(new NativeMotionComponent(2,.5f,.5f,0,0,2,2),stats[1]);
    }

    [Fact]
    public void LaterBridgeUnionsProvisionalLabelsAndPreservesDenseOrder()
    {
        var stats=new List<NativeMotionComponent>();
        Assert.True(NativeMotionComponents.TryAppend(Mask(2,6,8,1,0,1,0,0,1,99,99, 0,1,1,0,0,1,99,99),stats,out var labels));
        Assert.Equal(new[]{1,0,1,0,0,2, 0,1,1,0,0,2},labels);
        Assert.Equal(3,stats.Count);
        Assert.Equal(new NativeMotionComponent(6,2.5f,.5f,0,0,5,2),stats[0]);
        Assert.Equal(new NativeMotionComponent(4,1.25f,.5f,0,0,3,2),stats[1]);
        Assert.Equal(new NativeMotionComponent(2,5f,.5f,5,0,1,2),stats[2]);
    }

    [Fact]
    public void AllZeroIsOneBackgroundRow()
    {
        var stats=new List<NativeMotionComponent>();
        Assert.True(NativeMotionComponents.TryAppend(Mask(2,3,3,0,0,0,0,0,0),stats,out var labels));
        Assert.Equal(new[]{0,0,0,0,0,0},labels);
        Assert.Equal(new[]{new NativeMotionComponent(6,1f,.5f,0,0,3,2)},stats);
    }

    [Fact]
    public void AllForegroundRetainsEmptyBackgroundAndAppendsWithoutClearing()
    {
        var sentinel=new NativeMotionComponent(77,1,2,3,4,5,6);
        var stats=new List<NativeMotionComponent>{sentinel};
        Assert.True(NativeMotionComponents.TryAppend(Mask(2,2,2,1,1,1,1),stats,out var labels));
        Assert.Equal(new[]{1,1,1,1},labels);Assert.Equal(3,stats.Count);Assert.Equal(sentinel,stats[0]);
        var background=stats[1];Assert.Equal(0u,background.Area);
        Assert.Equal(int.MaxValue,background.Left);Assert.Equal(int.MaxValue,background.Top);
        Assert.Equal(2,background.Width);Assert.Equal(2,background.Height);
        Assert.True(float.IsNaN(background.CenterX));Assert.True(float.IsNaN(background.CenterY));
        Assert.Equal(new NativeMotionComponent(4,.5f,.5f,0,0,2,2),stats[2]);
    }

    [Fact]
    public void SingleBackgroundPixelHasItsOwnBoundsAndCentroid()
    {
        var stats=new List<NativeMotionComponent>();
        Assert.True(NativeMotionComponents.TryAppend(Mask(1,3,3,1,0,1),stats,out var labels));
        Assert.Equal(new[]{1,0,2},labels);
        Assert.Equal(new NativeMotionComponent(1,1,0,1,0,1,1),stats[0]);
        Assert.Equal(1u,stats[1].Area);Assert.Equal(1u,stats[2].Area);
    }

    [Fact]
    public void InexactCentroidsUseNativeDoubleThenFloatRounding()
    {
        var stats=new List<NativeMotionComponent>();
        Assert.True(NativeMotionComponents.TryAppend(Mask(2,3,5,1,1,0,99,99,1,0,0,99,99),stats,out var labels));
        Assert.Equal(new[]{1,1,0,1,0,0},labels);
        Assert.Equal(0x3FD55555u,BitConverter.SingleToUInt32Bits(stats[0].CenterX));
        Assert.Equal(0x3F2AAAABu,BitConverter.SingleToUInt32Bits(stats[0].CenterY));
        Assert.Equal(0x3EAAAAABu,BitConverter.SingleToUInt32Bits(stats[1].CenterX));
        Assert.Equal(0x3EAAAAABu,BitConverter.SingleToUInt32Bits(stats[1].CenterY));
    }
}