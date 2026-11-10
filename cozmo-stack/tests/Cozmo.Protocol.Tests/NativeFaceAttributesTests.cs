using System.Text.Json;
using Cozmo.Robot.Emulation;
using Xunit;

namespace Cozmo.Protocol.Tests;

public sealed class NativeFaceAttributesTests
{
    [Theory]
    [InlineData(0x3C00)]
    [InlineData(0x1000)]
    [InlineData(0x2000)]
    public void StockGuestAttributesMatchIndependentNativeWrapper(int mask)
    {
        string? lib=ShippedLibraries.FindLibDir();if(lib is null)return;
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","okao","okao_oracle.json")));
        var root=doc.RootElement;int width=root.GetProperty("width").GetInt32(),height=root.GetProperty("height").GetInt32();
        var images=root.GetProperty("images").EnumerateObject().ToDictionary(p=>p.Name,p=>Convert.FromBase64String(p.Value.GetString()!));
        using var detector=new OkaoFaceDetection(lib);
        bool saw2=false,saw3=false,sawStatus=false;
        foreach(var frame in root.GetProperty("frames").EnumerateArray())
        {
            string name=frame.GetProperty("name").GetString()!;
            var faces=detector.DetectTracked(images[frame.GetProperty("image").GetString()!],width,height,attributeMask:mask);
            if(name is not ("face_s10_1" or "face_s10_2" or "face_s10_3" or "face_s08_0"))continue;
            var face=Assert.Single(faces);var a=Assert.IsType<Cozmo.Robot.Vision.NativeFaceAttributes>(face.NativeAttributes);
            if(name=="face_s08_0")
            {
                Assert.Equal(new byte[]{8,5,82,1,4},a.Scores);
                Assert.False(a.Smile.Valid);Assert.Equal(0f,a.Smile.First);Assert.Equal(0f,a.Smile.Second);
                Assert.True(a.Gaze.Valid);Assert.Equal(1112276992u,BitConverter.SingleToUInt32Bits(a.Gaze.First));
                Assert.Equal(1077936128u,BitConverter.SingleToUInt32Bits(a.Gaze.Second));
                Assert.True(a.Blink.Valid);Assert.Equal(1056058639u,BitConverter.SingleToUInt32Bits(a.Blink.First));
                Assert.Equal(1053575611u,BitConverter.SingleToUInt32Bits(a.Blink.Second));
                sawStatus=true;break;
            }
            if(name=="face_s10_1")
            {
                Assert.Equal(new byte[5],a.Scores);Assert.False(a.Smile.Valid);Assert.False(a.Gaze.Valid);Assert.False(a.Blink.Valid);
                continue;
            }
            if(mask==0x3C00)
            {
                Assert.Equal(name=="face_s10_2"?new byte[]{1,0,99,0,0}:new byte[]{1,0,93,0,6},a.Scores);
                Assert.True(a.Smile.Valid);Assert.Equal(name=="face_s10_2"?1032805417u:1028443340u,BitConverter.SingleToUInt32Bits(a.Smile.First));
                Assert.Equal(981668463u,BitConverter.SingleToUInt32Bits(a.Smile.Second));
            }
            else { Assert.Equal(new byte[5],a.Scores);Assert.False(a.Smile.Valid); }
            if((mask & 0x1000)!=0)
            {
                Assert.True(a.Gaze.Valid);
                Assert.Equal(name=="face_s10_2"?1073741824u:1084227584u,BitConverter.SingleToUInt32Bits(a.Gaze.First));
                Assert.Equal(name=="face_s10_2"?3239051264u:3240099840u,BitConverter.SingleToUInt32Bits(a.Gaze.Second));
            }
            else Assert.False(a.Gaze.Valid);
            if((mask & 0x2000)!=0)
            {
                Assert.True(a.Blink.Valid);
                Assert.Equal(name=="face_s10_2"?1054716462u:1054548690u,BitConverter.SingleToUInt32Bits(a.Blink.First));
                Assert.Equal(name=="face_s10_2"?1056427738u:1056461292u,BitConverter.SingleToUInt32Bits(a.Blink.Second));
            }
            else Assert.False(a.Blink.Valid);
            if(name=="face_s10_2")saw2=true;else saw3=true;
            if(saw2 && saw3 && mask!=0x3C00)break;
        }
        Assert.True(saw2 && saw3);
        if(mask==0x3C00)Assert.True(sawStatus);
    }
}
