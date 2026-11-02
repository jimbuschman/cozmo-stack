using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

public sealed class NativeMotionMorphologyTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return new object[]{3,3,new byte[]{9,8,7,6,5,4,3,2,1},2,2,new byte[]{1,0,7,255},1,1,
            new byte[]{9,9,8,6,9,8,3,6,5},new byte[]{9,8,7,6,5,4,3,2,1},new byte[]{9,9,8,6,6,8,3,3,5}};
        yield return new object[]{3,3,new byte[]{0,0,0,0,255,0,0,0,0},2,2,new byte[]{1,0,0,0},1,1,
            new byte[]{0,0,0,0,0,0,0,0,255},new byte[]{255,255,255,255,0,0,255,0,255},new byte[]{255,255,255,255,0,0,255,0,0}};
        yield return new object[]{2,2,new byte[]{10,100,50,20},3,3,new byte[]{0,1,0,1,1,1,0,1,0},1,1,
            new byte[]{100,100,50,100},new byte[]{10,10,10,20},new byte[]{50,100,50,50}};
        yield return new object[]{1,5,new byte[]{3,9,1,7,2},1,3,new byte[]{1,0,1},1,0,
            new byte[]{9,3,9,2,7},new byte[]{9,1,7,1,7},new byte[]{3,9,2,7,2}};
        yield return new object[]{1,4,new byte[]{10,20,30,40},1,2,new byte[]{1,0},1,0,
            new byte[]{0,10,20,30},new byte[]{255,10,20,30},new byte[]{255,0,10,20}};
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void NativeLiteralCasesPreserveWholePassAndPadding(int rows,int cols,byte[] pixels,int kr,int kc,byte[] kernel,int ax,int ay,byte[] dilate,byte[] erode,byte[] close)
    {
        var operations=new[]{NativeMotionMorphOperation.Dilate,NativeMotionMorphOperation.Erode,NativeMotionMorphOperation.Close};
        var expected=new[]{dilate,erode,close};
        for(int op=0;op<operations.Length;op++)for(int alias=0;alias<2;alias++)
        {
            int stride=cols+2;var input=Enumerable.Repeat((byte)211,rows*stride).ToArray();
            for(int y=0;y<rows;y++)Array.Copy(pixels,y*cols,input,y*stride,cols);
            var src=new NativeMotionImage(rows,cols,1,stride,input,123);
            var dest=alias==0?src:new NativeMotionImage(rows,cols,1,stride,Enumerable.Repeat((byte)211,rows*stride).ToArray(),0);
            Assert.True(NativeMotionMorphology.TryApply(src,dest,kr,kc,kernel,ax,ay,operations[op]));
            // OpenCV receives Mat, so its destination Image header is not assigned.
            Assert.Equal(alias==0?123u:0u,dest.Timestamp);
            for(int y=0;y<rows;y++)
            {
                Assert.Equal(expected[op][(y*cols)..((y+1)*cols)],dest.Pixels[(y*stride)..(y*stride+cols)]);
                Assert.Equal(211,dest.Pixels[y*stride+cols]);Assert.Equal(211,dest.Pixels[y*stride+cols+1]);
            }
            if(alias!=0)for(int y=0;y<rows;y++)Assert.Equal(pixels[(y*cols)..((y+1)*cols)],src.Pixels[(y*stride)..(y*stride+cols)]);
        }
    }

    [Theory]
    [InlineData(new byte[]{0,0})]
    [InlineData(new byte[]{1,1})]
    public void UnsupportedKernelLeavesDestinationUntouched(byte[] kernel)
    {
        var src=new NativeMotionImage(1,2,1,2,new byte[]{1,2},10);
        var dst=new NativeMotionImage(1,2,1,2,new byte[]{7,8},20);
        Assert.False(NativeMotionMorphology.TryApply(src,dst,1,2,kernel,1,0,NativeMotionMorphOperation.Close));
        Assert.Equal(new byte[]{7,8},dst.Pixels);Assert.Equal(20u,dst.Timestamp);
    }

    [Fact]
    public void UnsupportedChannelsLeaveDestinationUntouched()
    {
        var src=new NativeMotionImage(1,1,3,3,new byte[]{1,2,3},10);
        var dst=new NativeMotionImage(1,1,3,3,new byte[]{7,8,9},20);
        Assert.False(NativeMotionMorphology.TryApply(src,dst,1,2,new byte[]{1,0},1,0,NativeMotionMorphOperation.Dilate));
        Assert.Equal(new byte[]{7,8,9},dst.Pixels);Assert.Equal(20u,dst.Timestamp);
    }
}
