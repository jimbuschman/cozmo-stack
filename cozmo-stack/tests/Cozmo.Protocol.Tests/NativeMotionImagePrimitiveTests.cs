using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

public sealed class NativeMotionImagePrimitiveTests
{
    private static NativeMotionImage Gray(int n,int stride,uint timestamp=7)
    {
        var bytes=Enumerable.Repeat((byte)251,n*stride).ToArray();
        for(int y=0;y<n;y++)for(int x=0;x<n;x++)bytes[y*stride+x]=(byte)(10*y+x);
        return new(n,n,1,stride,bytes,timestamp);
    }

    [Theory]
    [InlineData(4,2,new byte[]{0,2,20,22})]
    [InlineData(5,3,new byte[]{0,1,3,10,11,13,30,31,33})]
    [InlineData(3,2,new byte[]{0,1,10,11})]
    public void HalfNearestUsesRoundedDestinationAndActualRatio(int size,int dest,byte[] expected)
    {
        var cache=new NativeMotionImageCache();cache.Reset(Gray(size,size+3,0x12345678));
        var half=cache.GetHalf();Assert.Equal(dest,half.Rows);Assert.Equal(dest,half.Columns);
        Assert.Equal(expected,half.Pixels);Assert.Equal(0x12345678u,half.Timestamp);
    }

    [Fact]
    public void OnePixelAndRgbStridePreserveBytesAndTimestamp()
    {
        var cache=new NativeMotionImageCache();cache.Reset(new NativeMotionImage(1,1,1,1,new byte[]{73},12));
        Assert.Equal(new byte[]{73},cache.GetHalf().Pixels);
        byte[] bytes=Enumerable.Repeat((byte)249,5*19).ToArray();
        for(int y=0;y<5;y++)for(int x=0;x<5;x++)
        {int i=y*19+x*3;bytes[i]=(byte)(10*y+x);bytes[i+1]=(byte)(100+x);bytes[i+2]=(byte)(200+y);}
        cache.Reset(new NativeMotionImage(5,5,3,19,bytes,44));Assert.True(cache.IsColor);
        var half=cache.GetHalf();Assert.Equal(44u,half.Timestamp);
        Assert.Equal(new byte[]{1,101,200},half.Pixels[3..6]);
        Assert.Equal(new byte[]{33,103,203},half.Pixels[24..27]);
    }

    [Fact]
    public void SameKindHalfIsMutableCachedAndResetReusesStorage()
    {
        var cache=new NativeMotionImageCache();var source=Gray(5,7,1);cache.Reset(source);
        var first=cache.GetHalf();first.Pixels[0]=199;source.Pixels[0]=77;
        Assert.Same(first,cache.GetHalf());Assert.Equal(199,cache.GetHalf().Pixels[0]);
        cache.Reset(source);var reloaded=cache.GetHalf();Assert.Same(first,reloaded);Assert.Equal(77,reloaded.Pixels[0]);
        cache.Reset(new NativeMotionImage(5,5,3,15,new byte[75],2));var rgb=cache.GetHalf();
        cache.Reset(source);Assert.Same(first,cache.GetHalf());Assert.NotSame(rgb,first);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void ResetCopiesTimestampButSharesFullImagePixels(int channels)
    {
        var bytes=new byte[4*channels];
        var source=new NativeMotionImage(2,2,channels,2*channels,bytes,17);
        var cache=new NativeMotionImageCache();cache.Reset(source);
        source.Timestamp=99;bytes[0]=73;
        var half=cache.GetHalf();
        Assert.Equal(17u,half.Timestamp);Assert.Equal(73,half.Pixels[0]);
    }

    [Fact]
    public void PreviousIsDeepTypedCopyAndSwitchClearsOppositeKind()
    {
        var prev=new NativeMotionPrevious();var gray=Gray(3,5,31);prev.Set(gray,false);
        gray.Pixels[0]=99;Assert.Equal(0,prev.Gray!.Pixels[0]);Assert.Equal(31u,prev.Gray.Timestamp);Assert.False(prev.GrayFiltered);
        prev.Set(gray,true);Assert.True(prev.GrayFiltered);
        var rgb=new NativeMotionImage(1,1,3,3,new byte[]{1,2,3},41);prev.Set(rgb,true);
        Assert.Null(prev.Gray);Assert.False(prev.GrayFiltered);Assert.True(prev.RgbFiltered);rgb.Pixels[0]=88;
        Assert.Equal(1,prev.Rgb!.Pixels[0]);Assert.Equal(41u,prev.Rgb.Timestamp);
        prev.Set(gray,false);Assert.Null(prev.Rgb);Assert.False(prev.RgbFiltered);
    }

    [Fact]
    public void RatioUsesAllByteGatesAndStrictFloatThreshold()
    {
        var previous=new NativeMotionImage(1,4,1,6,new byte[]{11,20,20,10,0,0},1);
        var current=new NativeMotionImage(1,4,1,6,new byte[]{11,25,26,255,0,0},2);
        var gray=NativeMotionRatio.Compare(previous,current);
        Assert.Equal(new byte[]{0,0,255,0},gray.Mask);Assert.Equal(1u,gray.Count);
        var a=new NativeMotionImage(1,2,3,8,new byte[]{20,20,20,10,20,20,0,0},1);
        var b=new NativeMotionImage(1,2,3,8,new byte[]{20,26,20,255,40,20,0,0},2);
        var rgb=NativeMotionRatio.Compare(a,b);
        Assert.Equal(new byte[]{255,0},rgb.Mask);Assert.Equal(1u,rgb.Count);
        var rowsA=new NativeMotionImage(2,2,1,4,new byte[]{20,20,99,99,11,20,99,99},1);
        var rowsB=new NativeMotionImage(2,2,1,4,new byte[]{26,25,1,1,11,26,1,1},2);
        var strided=NativeMotionRatio.Compare(rowsA,rowsB);
        Assert.Equal(new byte[]{255,0,0,255},strided.Mask);Assert.Equal(2u,strided.Count);
    }
}
