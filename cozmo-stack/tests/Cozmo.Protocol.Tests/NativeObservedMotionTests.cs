using Cozmo.Robot.Vision;
using Xunit;
namespace Cozmo.Protocol.Tests;
public sealed class NativeObservedMotionTests
{
    [Fact]
    public void PackedBodyMatchesNativeLiteral()
    {
        var report=new NativeObservedMotion(0x01020304,1,-1,2,.5f,240,-3,2,4,-5,3,6,-7,4,8,-9);
        Assert.Equal("040302010000803FFFFF02000000003FF000FDFF000000400400FBFF000040400600F9FF000080400800F7FF",Convert.ToHexString(report.PackBody()));
    }
}
