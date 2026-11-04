using System.Text.Json;
using Cozmo.Robot.Emulation;
using Xunit;
using Xunit.Abstractions;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// OKAO face detection in the embedded emulator against the Python oracle (re-analysis/tools/emu/emu_okao.py,
/// gen_okao_fixtures.py): the same shipped code, the same Init settings, a frame sequence of drawn faces and faceless frames fed
/// in order (video-mode tracking carries state). Expected values come only from the oracle. reach: unit (no production caller:
/// the stack's IFaceDetector seam is not wired to it yet).
/// </summary>
[Trait("Category", "Emulator")]
public sealed class NativeOkaoOracleTests
{
    private readonly ITestOutputHelper _out;
    public NativeOkaoOracleTests(ITestOutputHelper output) => _out = output;

    [Fact]
    public void EveryFrameMatchesTheOracleInOrder()
    {
        var libDir = ShippedLibraries.FindLibDir();
        if (libDir is null) { _out.WriteLine("resources/lib/armeabi-v7a not present: skipped"); return; }
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "okao", "okao_oracle.json")));
        var root = doc.RootElement;
        int w = root.GetProperty("width").GetInt32(), h = root.GetProperty("height").GetInt32();
        var images = new Dictionary<string, byte[]>();
        foreach (var p in root.GetProperty("images").EnumerateObject()) images[p.Name] = Convert.FromBase64String(p.Value.GetString()!);

        using var det = new OkaoFaceDetection(libDir);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        int frames = 0, withFace = 0;
        foreach (var f in root.GetProperty("frames").EnumerateArray())
        {
            string name = f.GetProperty("name").GetString()!;
            var r = det.Detect(images[f.GetProperty("image").GetString()!], w, h);
            Assert.True(f.GetProperty("ret").GetInt32() == r.Return, $"{name}: return");
            Assert.True(f.GetProperty("countRet").GetInt32() == r.CountReturn, $"{name}: count return");
            var exp = f.GetProperty("faces").EnumerateArray().ToList();
            Assert.True(exp.Count == r.Faces.Count, $"{name}: {r.Faces.Count} faces, oracle {exp.Count}");
            for (int i = 0; i < exp.Count; i++)
            {
                Assert.Equal(exp[i].GetProperty("ret").GetInt32(), r.Faces[i].Return);
                Assert.Equal(exp[i].GetProperty("words").EnumerateArray().Select(x => x.GetInt32()).ToArray(), r.Faces[i].Words);
            }
            frames++;
            if (r.Faces.Count > 0) withFace++;
        }
        _out.WriteLine($"{frames} frames, {withFace} with a face, {sw.Elapsed.TotalMilliseconds / frames:F0} ms per frame");
        Assert.True(withFace >= 5 && withFace < frames, "the corpus must contain frames with and without a face");
    }
}
