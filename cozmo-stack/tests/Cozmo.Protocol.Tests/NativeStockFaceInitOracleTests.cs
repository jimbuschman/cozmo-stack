using System.Text.Json;
using Cozmo.Robot.Emulation;
using Xunit;

namespace Cozmo.Protocol.Tests;

public sealed class NativeStockFaceInitOracleTests
{
    [Fact]
    public void DefinedCleanupFailureStillAttemptsLaterNativeHandles()
    {
        string? libraries=ShippedLibraries.FindLibDir();if(libraries is null)return;
        var guest=new OkaoFaceDetection(libraries,0,true);
        guest.StockVersion();guest.InitializeStockHandles();uint album=guest.FrCreateAlbum();
        Assert.Equal(0,guest.FrCall("OKAO_FR_DeleteAlbumHandle",album));
        var order=new List<string>();
        guest.StockDeleteOverrideForTest=name=>{order.Add(name);return name=="OKAO_SM_DeleteHandle"?1:null;};
        guest.Dispose();
        Assert.Contains("FaceTrackerImpl.Destructor.FaceLibSmileDetectHandleDeleteFail",guest.StockCleanupFailures);
        Assert.True(order.IndexOf("OKAO_SM_DeleteHandle")<order.IndexOf("OKAO_GB_DeleteHandle"));
        Assert.Equal("OKAO_CO_DeleteHandle",order[^1]);
        Assert.Equal(2,order.Count(name=>name=="OKAO_FR_DeleteFeatureHandle"));
    }
    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)0xA5)]
    public void EagerStockHandlesKeepTheIndependentRawCorpus(byte poison)
    {
        string? libraries=ShippedLibraries.FindLibDir();if(libraries is null)return;
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","okao","okao_oracle.json")));
        var root=doc.RootElement;int width=root.GetProperty("width").GetInt32(),height=root.GetProperty("height").GetInt32();
        var images=root.GetProperty("images").EnumerateObject().ToDictionary(p=>p.Name,p=>Convert.FromBase64String(p.Value.GetString()!));
        using var guest=new OkaoFaceDetection(libraries,poison,true);
        Assert.Equal(((byte)6,(byte)0),guest.StockVersion());guest.InitializeStockHandles();uint album=guest.FrCreateAlbum();
        try
        {
            foreach(var frame in root.GetProperty("frames").EnumerateArray())
            {
                var result=guest.Detect(images[frame.GetProperty("image").GetString()!],width,height);
                Assert.Equal(frame.GetProperty("ret").GetInt32(),result.Return);
                Assert.Equal(frame.GetProperty("countRet").GetInt32(),result.CountReturn);
                var expected=frame.GetProperty("faces").EnumerateArray().ToArray();
                Assert.Equal(expected.Length,result.Faces.Count);
                for(int i=0;i<expected.Length;i++)
                {
                    Assert.Equal(expected[i].GetProperty("ret").GetInt32(),result.Faces[i].Return);
                    Assert.Equal(expected[i].GetProperty("words").EnumerateArray().Select(value=>value.GetInt32()).ToArray(),result.Faces[i].Words);
                }
            }
        }
        finally { Assert.Equal(0,guest.FrCall("OKAO_FR_DeleteAlbumHandle",album)); }
    }
}
