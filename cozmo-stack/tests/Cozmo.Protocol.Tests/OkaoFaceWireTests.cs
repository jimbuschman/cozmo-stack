using System.Text.Json;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Emulation;
using Cozmo.Robot.Vision;
using Cozmo.Protocol;
using Fixture = Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class OkaoFaceWireTests
{
    private sealed class CustomDetector : IFaceDetector
    {
        public bool IsAvailable => true;
        public string Description => "caller-owned detector";
        public IReadOnlyList<DetectedFace> Detect(GrayImage image, uint timestamp) => Array.Empty<DetectedFace>();
    }

    [Fact]
    public void StockDetectorDisposeIsTerminal()
    {
        var detector = new OkaoFaceDetector();
        detector.Dispose();
        Assert.False(detector.IsAvailable);
        Assert.Throws<ObjectDisposedException>(() => detector.Detect(new GrayImage(1, 1), 1));
    }

    [Fact]
    public void OwnerReplacementPreservesTheCallersDetector()
    {
        using var fixture = new Fixture();
        var vision = fixture.M.Vision;
        var custom = new CustomDetector();
        vision.FaceDetector = custom;
        fixture.Rig.Engine.Robots.RemoveRobot(CozmoEngine.RobotId, wasConnecting: false);
        fixture.Rig.Engine.Robots.AddRobot(CozmoEngine.RobotId, withRic: false);
        Assert.Same(custom, vision.FaceDetector);
    }

    [Fact]
    public void FaceRotationUsesTheSourceHistoryCountAndThresholds()
    {
        float head = BitConverter.UInt32BitsToSingle(0x3E32B8C2);
        float body = BitConverter.UInt32BitsToSingle(0x3F060A92);
        Assert.True(VisionSystem.NativeFaceRotationFromHistory(Array.Empty<NativeImageImu>(), 100, false));
        Assert.False(VisionSystem.NativeFaceRotationFromHistory(Array.Empty<NativeImageImu>(), 100, true));
        var bracket = new[] { new NativeImageImu(1, 0, head, body, 0, 90), new NativeImageImu(2, 0, head, body, 0, 110) };
        Assert.False(VisionSystem.NativeFaceRotationFromHistory(bracket, 100, false));
        bracket[1] = bracket[1] with { RateZ = MathF.BitIncrement(body) };
        Assert.True(VisionSystem.NativeFaceRotationFromHistory(bracket, 100, false));
        var recent = new[] { new NativeImageImu(1, 1, head + 1, 0, 0, 90), new NativeImageImu(2, 1, 0, 1, 0, 110) };
        Assert.False(VisionSystem.NativeFaceRotationFromHistory(recent, 100, true));
        recent[0] = recent[0] with { RateZ = 1 };
        Assert.True(VisionSystem.NativeFaceRotationFromHistory(recent, 100, true));
        recent[0] = recent[0] with { ComputedTimestamp = 0 };
        Assert.False(VisionSystem.NativeFaceRotationFromHistory(recent, 100, true));
    }

    [Fact]
    public void CanonicalCameraFramesDeliverFacesAndQualityRectangles()
    {
        if (ShippedLibraries.FindLibDir() is null) return;
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "okao", "okao_oracle.json")));
        var root = doc.RootElement;
        var images = root.GetProperty("images").EnumerateObject().ToDictionary(p => p.Name, p => Convert.FromBase64String(p.Value.GetString()!));
        using var fixture = new Fixture();
        var vision = fixture.M.Vision;
        vision.Enabled = true;
        vision.ModeEnableMask = (1 << 2) | (1 << 7);
        vision.Schedules = AllVisionModesSchedule.InitDefaultSchedules();
        fixture.Rig.Robot.SetVisionWhileMoving(true);
        fixture.Rig.Tick();
        var rectangles = new List<IReadOnlyList<ImageQualityRectangle>>();
        vision.NativeQualityRectanglesObservedForTest = rows => rectangles.Add(rows.ToArray());
        uint id = 100;
        foreach (var frame in root.GetProperty("frames").EnumerateArray())
        {
            string name = frame.GetProperty("name").GetString()!;
            fixture.Rig.State(RobotStatusFlag.HeadInPos | RobotStatusFlag.LiftInPos);
            fixture.NativeCameraFrame(id++, images[frame.GetProperty("image").GetString()!]);
            fixture.Rig.Engine.VisionUpdateAllResultsFailed!();
            fixture.Rig.Tick();
            if (name == "face_s10_2") break;
        }
        Assert.Contains(rectangles, rows => rows.Any(r => r.Width > 0 && r.Height > 0));
        var face = Assert.Single(vision.LastFaces);
        Assert.Equal(-1, face.Id);
        Assert.NotNull(face.Detection.LeftEye);
        Assert.True(vision.Faces.Count > 0);
    }
    [Fact]
    public void TwoFaceShuffleAdvancesOneNativeDrawPerFrame()
    {
        int words = 0;
        var random = new EngineRandom(1) { WordSourceForTest = () => { words++; return 0; } };
        var first = new[] { 0, 1 };
        var second = new[] { 0, 1 };
        OkaoFaceDetection.ShuffleIndices(first, random);
        OkaoFaceDetection.ShuffleIndices(second, random);
        Assert.Equal(new[] { 0, 1 }, first);
        Assert.Equal(new[] { 0, 1 }, second);
        Assert.Equal(4, words); // two mt19937 words for each of two RandInt draws
    }
    [Fact]
    public void DrawnSequenceProjectsNativePartsAndSquareCorners()
    {
        if (Cozmo.Robot.Emulation.ShippedLibraries.FindLibDir() is null) return;
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "okao", "okao_oracle.json")));
        var root = doc.RootElement;
        int width = root.GetProperty("width").GetInt32(), height = root.GetProperty("height").GetInt32();
        var images = root.GetProperty("images").EnumerateObject().ToDictionary(p => p.Name, p => Convert.FromBase64String(p.Value.GetString()!));
        using var detector = new OkaoFaceDetector();
        var got = new Dictionary<string, DetectedFace>();
        uint timestamp = 1;
        foreach (var frame in root.GetProperty("frames").EnumerateArray())
        {
            string name = frame.GetProperty("name").GetString()!;
            var image = new GrayImage(width, height, images[frame.GetProperty("image").GetString()!]);
            var faces = detector.Detect(image, timestamp++);
            if (name is "face_s10_1" or "face_s10_2" or "face_s10_3") got[name] = Assert.Single(faces);
            if (name == "face_s10_3") break;
        }
        Assert.Equal(new FaceRect(116, 77, 94, 94), got["face_s10_1"].Rect);
        Assert.Equal(-1, got["face_s10_1"].Id);
        Assert.Null(got["face_s10_1"].LeftEye);
        var second = got["face_s10_2"];
        Assert.Equal(new FaceRect(119, 87, 90, 90), second.Rect);
        Assert.Equal(new Vec2(144, 117), second.LeftEye);
        Assert.Equal(new Vec2(177, 117), second.RightEye);
        Assert.Equal(1, second.NativeFaceDirectionThirdDegrees);
        Assert.Equal(0, second.RollRad);
        Assert.True(second.NativeTrackerFlagChanged);
        var third = got["face_s10_3"];
        Assert.Equal(new FaceRect(117, 85, 86, 86), third.Rect);
        Assert.Equal(new Vec2(145, 117), third.LeftEye);
        Assert.Equal(new Vec2(176, 117), third.RightEye);
        Assert.Equal(1, third.NativeFaceDirectionThirdDegrees);
        Assert.Equal(3f * BitConverter.UInt32BitsToSingle(0x3C8EFA35), (float)third.RollRad!.Value);
    }
}
