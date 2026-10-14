using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Transport;
using Xunit;
namespace Cozmo.Protocol.Tests;
[Collection("M5 process statics")]
public sealed class M5X14BuilderTests : IDisposable
{
    private readonly bool _error = EngineErrorState.ErrorFlagSet;
    public void Dispose() => EngineErrorState.ErrorFlagSet = _error;
    [Fact]
    public void RobotConstructionReadsNeutralOnceAndLoadCompletionDoesNotRetry()
    {
        using var robot = CozmoRobot.CreateForTest(new Port(), () => 0);
        var entries = new List<string>();
        robot.Engine.LogEntry += e => {if(e.Name?.StartsWith("Animation",StringComparison.Ordinal)==true)entries.Add(e.Name);}; // D5 adds unrelated Robot.Delocalize logs after animation construction.
        robot.Engine.Robots.AddRobot(1, false);
        Assert.Equal(new[] { "AnimationTriggerResponsesContainer::GetResponse", "AnimationGroupContainer.GetAnimationGroup_Const.InvalidName", "AnimationStreamer.Constructor.BadNeutralAnimGroup" }, entries);
        string root = Directory.CreateTempSubdirectory("m514-").FullName;
        Directory.CreateDirectory(Path.Combine(root, "animationGroups"));
        try
        {
            entries.Clear(); robot.Animations.LoadFrom(root);
            Assert.DoesNotContain(entries, e => e.StartsWith("AnimationStreamer.Constructor."));
            robot.Engine.Robots.RemoveRobot(1, false);
            entries.Clear(); robot.Engine.Robots.AddRobot(2, false);
            Assert.Contains("AnimationStreamer.Constructor.BadNeutralAnimGroup", entries);
        }
        finally { Directory.Delete(root, true); }
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ResetMissAndFacelessClipKeepPriorResetData(bool faceless)
    {
        using var robot = CozmoRobot.CreateForTest(new Port(), () => 0);
        var scheduler = new AnimationScheduler(new Sink()) { Catalog = new Catalog(faceless) };
        var lines = new List<string>(); scheduler.Log = lines.Add;
        var prior = ProceduralFacePose.ShippedNeutral(); prior.Left[0] = 17f;
        scheduler.Layers.SetResetData(prior);
        scheduler.LoadNeutralFace(); scheduler.ResetToConstructed();
        Assert.Equal(17f, scheduler.LayerBaseFace.Left[0]);
        if (faceless) { Assert.NotEmpty(lines); Assert.All(lines, s => Assert.Contains("M1-034 / M5-010", s)); }
    }
    [Fact]
    public void ArbitraryBehaviorReportsCustomSongBeforeLibraryLookup()
    {
        using var robot = CozmoRobot.CreateOffline();
        var lines = new List<string>(); robot.Engine.StackLogLine += lines.Add;
        var behavior = new PlayArbitraryAnimBehavior { ClipName = "cozmo_sings_custom" };
        var context = new BehaviorContext { Robot = robot, Triggers = new AnimationTriggerMap() };
        Assert.False(behavior.IsRunnable(context));
        behavior.StartAsync(context, new BehaviorScope(), default).GetAwaiter().GetResult();
        Assert.Contains(lines, s => s.StartsWith("MISSING: M9-029"));
        Assert.False(robot.Animations.IsPlaying);
    }
    private sealed class Catalog(bool faceless) : IAnimationCatalog
    {
        public bool HasAnimationForTrigger(AnimationTrigger t) => true;
        public string GetAnimationForTrigger(AnimationTrigger t) => "ag_neutral_face";
        public (string Name, int Count) GetFirstAnimationName(string g) => ("neutral", 1);
        public string GetAnimationNameFromGroup(string g, bool strict) => "neutral";
        public AnimationClip? GetAnimation(string n) => faceless ? new AnimationClip { Name = n, Keyframes = Array.Empty<Keyframe>() } : null;
    }
    private sealed class Port : IEngineTransport
    {
        public bool TimedOut => false;
        public event Action<ReceiverEvent>? Received { add { } remove { } }
        public void Start() {} public void Connect(System.Net.IPAddress ip, bool simulated) {}
        public void Disconnect(System.Net.IPEndPoint address) {} public void SendData(byte[] data) {}
    }
    private sealed class Sink : IAnimationSink
    {
        public void Face(FaceBitmap b) {} public void Audio(byte[]? b) {} public void Head(sbyte a, uint d) {}
        public void Lift(byte h, uint d) {} public void Body(BodyKeyframe b) {} public void BodyStop() {}
        public void Lights(LightsKeyframe b) {} public void Event(string s) {} public void AnimationStarted(byte b) {}
        public void AnimationEnded() {} public void Finished(string s, bool b) {}
    }
}
