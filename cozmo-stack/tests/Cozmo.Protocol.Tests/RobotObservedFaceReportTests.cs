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
        Assert.Empty(f.LeftEye);
        Assert.Equal(new[]{9d,10d},f.RightEye.Select(p=>p.X));
        Assert.Equal(new[]{7d,11d,8d,2d,7d},f.Nose.Select(p=>p.X));
        Assert.Equal(new[]{4d,0d,3d},f.Mouth.Select(p=>p.X));
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
        foreach(var group in new[]{f.LeftEye,f.RightEye,f.Nose,f.Mouth})
        {
            Assert.Equal(group.Count,r.ReadByte());
            foreach(var point in group){Assert.Equal((float)point.X,r.ReadSingle());Assert.Equal((float)point.Y,r.ReadSingle());}
        }
        Assert.Equal(r.BaseStream.Length,r.BaseStream.Position);
    }
}
