using Cozmo.Robot.Vision;
using Xunit;
namespace Cozmo.Protocol.Tests;
public sealed class NativeMotionPolygonTests
{
    [Fact]
    public void SlopedAndRectangularMasksMatchNativeBytes()
    {
        static string Bits(byte[] values)=>string.Concat(values.Select(x=>x==0?'0':'1'));
        var slope=new[]{new NativeMotionPolygon.Point(1,0),new NativeMotionPolygon.Point(4,1),new NativeMotionPolygon.Point(4,4),new NativeMotionPolygon.Point(0,3)};
        Assert.Equal("011000011110111110111110000110",Bits(NativeMotionPolygon.Fill(5,6,slope)));
        var rect=new[]{new NativeMotionPolygon.Point(1,1),new NativeMotionPolygon.Point(4,1),new NativeMotionPolygon.Point(4,3),new NativeMotionPolygon.Point(1,3)};
        Assert.Equal("000000011110011110011110000000",Bits(NativeMotionPolygon.Fill(5,6,rect)));
        var degenerate=Enumerable.Repeat(new NativeMotionPolygon.Point(3,2),4).ToArray();
        Assert.Equal("000000000000000100000000000000",Bits(NativeMotionPolygon.Fill(5,6,degenerate)));
    }
}
