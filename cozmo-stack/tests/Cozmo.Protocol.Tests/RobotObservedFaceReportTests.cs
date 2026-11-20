using Cozmo.Robot;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

public sealed class RobotObservedFaceReportTests
{
    [Fact]
    public void SourceFeatureGroupsAreOrderedAndInvalidPointEmptiesOnlyItsGroup()
    {
        var points=Enumerable.Range(0,12).Select(i=>(X:i,Y:i+20)).ToArray();
        points[6]=(-1,26);
        var f=NativeFaceFeatures.FromPoints(points);
        Assert.Equal(new[]{new Vec2(4,24),new Vec2(0,20),new Vec2(3,23)},f.LeftEye);
        Assert.Empty(f.RightEye);
        Assert.Equal(new[]{new Vec2(9,29),new Vec2(10,30)},f.Nose);
        Assert.Equal(new[]{new Vec2(7,27),new Vec2(11,31),new Vec2(8,28),new Vec2(2,22),new Vec2(7,27)},f.Mouth);
    }

    [Fact]
    public void PackedBodyHasNativeFieldOrderAndFixedDisabledValues()
    {
        var f=NativeFaceFeatures.FromPoints(Enumerable.Range(0,12).Select(i=>(X:i,Y:i+20)).ToArray());
        var report=new RobotObservedFaceReport(-7,0x10203040,new EngineGamePose(1,2,3,4,5,6,7,8),new FaceRect(9,10,11,12),"A",f.LeftEye,f.RightEye,f.Nose,f.Mouth);
        using var r=new BinaryReader(new MemoryStream(report.PackBody()));
        Assert.Equal(-7,r.ReadInt32()); Assert.Equal(0x10203040u,r.ReadUInt32());
        for(int i=1;i<=7;i++)Assert.Equal((float)i,r.ReadSingle());
        Assert.Equal(8u,r.ReadUInt32());
        for(int i=9;i<=12;i++)Assert.Equal((float)i,r.ReadSingle());
        Assert.Equal(1,r.ReadByte());Assert.Equal((byte)'A',r.ReadByte());Assert.Equal(0,r.ReadSByte());
        for(int i=0;i<3;i++){Assert.Equal(0,r.ReadByte());Assert.Equal(0,r.ReadSingle());Assert.Equal(0,r.ReadSingle());}
        Assert.Equal(new byte[5],r.ReadBytes(5));
        // FaceWorld 0x004F4A7C..0x004F4AF8 forwards slots 0,1,7,4; the four
        // DetectFaceParts tables at 0x00C98068..0x00C9809B provide these indices.
        int[][] slots = [[4,0,3], [5,1,6], [9,10], [7,11,8,2,7]];
        foreach(var indices in slots)
        {
            Assert.Equal(indices.Length,r.ReadByte());
            foreach(int index in indices){Assert.Equal((float)index,r.ReadSingle());Assert.Equal(index+20f,r.ReadSingle());}
        }
        Assert.Equal(r.BaseStream.Length,r.BaseStream.Position);
    }
}
