using System.Text.Json;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Xunit;

namespace Cozmo.Protocol.Tests;

public class NativeBouncerRasterTests
{
    [Fact]
    public void InitialFloatGeometryProducesNativeInitialFrame()
    {
        var game=new NativeBouncerGame();
        game.Initialize(128,64,new EngineRandom(1));
        string path=Path.Combine(AppContext.BaseDirectory,"TestData","BouncerNative","frame_initial.gray");
        Assert.Equal(File.ReadAllBytes(path),NativeBouncerRaster.Draw(game));
    }
    [Fact]
    public void MatchesIndependentNativeRasterCorpus()
    {
        string root=Path.Combine(AppContext.BaseDirectory,"TestData","BouncerNative");
        using var document=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"manifest.json")));
        foreach(var item in document.RootElement.GetProperty("cases").EnumerateObject())
        {
            var pixels=new byte[128*64]; var input=item.Value.GetProperty("input");
            if(input.TryGetProperty("rect",out var rectangles))foreach(var r in rectangles.EnumerateArray())
                NativeBouncerRaster.Rectangle(pixels,r[0].GetInt32(),r[1].GetInt32(),r[2].GetInt32(),r[3].GetInt32());
            if(input.TryGetProperty("circle",out var circles))foreach(var c in circles.EnumerateArray())
                NativeBouncerRaster.Circle(pixels,c[0].GetInt32(),c[1].GetInt32());
            if(input.TryGetProperty("text",out var texts))foreach(var t in texts.EnumerateArray())
                NativeBouncerRaster.Text(pixels,t[0].GetString()!,t[1].GetInt32(),t[2].GetInt32());
            Assert.Equal(File.ReadAllBytes(Path.Combine(root,item.Name+".gray")),pixels);
        }
    }
}
