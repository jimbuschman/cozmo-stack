using System.Security.Cryptography;
using System.Text;
using Cozmo.Robot;
using Cozmo.Robot.Vision;
using Cozmo.Transport;
using Xunit;
namespace Cozmo.Protocol.Tests;
[Collection("M5 process statics")]
public sealed class M3M4X13BuilderTests : IDisposable
{
    private readonly bool _error = EngineErrorState.ErrorFlagSet;
    private readonly string _dasRoot = Directory.CreateTempSubdirectory("das13-fixture-").FullName;
    public void Dispose() { EngineErrorState.ErrorFlagSet = _error; Directory.Delete(_dasRoot, true); }
    private CozmoRobot TestRobot()
    {
        var robot = CozmoRobot.CreateForTest(new Port(), () => 0,
            new CozmoEngineOptions { DasDeviceIdPath = Path.Combine(_dasRoot, Guid.NewGuid().ToString()) });
        robot.Engine.InitOfflineLink(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 5551));
        return robot;
    }
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void OriginNamesWalkParentsBoundTheLoopAndDuplicateDoesNotAdvance()
    {
        using var robot = TestRobot();
        var state = robot.Engine.Robot!;
        Assert.Equal("Origin1", state.OriginNames.Get(1)!.Name);
        Assert.True(state.AddOriginWithId(9)); Assert.Equal(10u, state.OriginNames.NextId);
        Assert.True(state.AddOriginWithId(2)); Assert.Equal(10u, state.OriginNames.NextId);
        Assert.False(state.AddOriginWithId(9)); Assert.Equal(2u, state.CurrentOriginId);
        var root = state.OriginNames.Get(9)!;
        var child = new PoseTreeNameNode("child_REJ") { Parent = root };
        Assert.Same(root, child.FindRoot(null));
        var entries = new List<EngineLogEntry>(); robot.Engine.LogEntry += entries.Add;
        child.Parent = child; EngineErrorState.ErrorFlagSet = false;
        Assert.Same(child, child.FindRoot((l,c,k,t) => robot.Engine.EmitSourceLog(l,c,k,t)));
        var entry = Assert.Single(entries); Assert.Equal("LoopBoundOverflow", entry.Name);
        Assert.Equal("(tc0000) : ../../../../coretech/common/include/anki/common/basestation/math/poseTreeNode.h:325", entry.Text);
        Assert.True(EngineErrorState.ErrorFlagSet);
    }
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void GlobalClearGameSpecialCaseAndLiveNativeFieldsReachDas()
    {
        using var robot = TestRobot(); var das = robot.Engine.Das;
        das.SetGlobal("a", "v"); das.SetGlobal("a", ""); Assert.DoesNotContain("a", das.GetGlobalsForThisRun().Keys);
        das.SetGlobal("$game", "g"); Assert.Equal("g", das.GameAppenderName);
        das.SetGlobal("$game", ""); Assert.Null(das.GameAppenderName);
        robot.Engine.Broadcast(new RobotAvailable { SerialNumberHead = 0x12345678, HwVersion = 0xabcd });
        robot.Engine.Broadcast(new ManufacturingID { SerialNumber = 0x87654321, BodyHwVersion = 0x12345678, BodyColor = unchecked((int)0x87654321) });
        var globals = das.GetGlobalsForThisRun();
        Assert.Equal("0xbeefabcd12345678", globals["$group"]);
        Assert.Equal("0xbeef4321567887654321", globals["$phys"]);
    }
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void StartupDeviceIdIsPersistedAndFailedFlushRunsOnWorkerWithEmptyKv()
    {
        string root = Directory.CreateTempSubdirectory("das13-").FullName;
        string path = Path.Combine(root, "id"); File.WriteAllText(path, "abc\nignored", new UTF8Encoding(false));
        int caller = Environment.CurrentManagedThreadId, worker = 0;
        using var done = new ManualResetEventSlim(); EngineLogEntry? entry = null;
        try
        {
            using var robot = CozmoRobot.CreateForTest(new Port(), () => 0, new CozmoEngineOptions { DasDeviceIdPath = path }, e =>
            {
                e.LogEntry += x => { if (x.Name == "das.upload.fail") { entry = x; worker = Environment.CurrentManagedThreadId; } };
                e.DasStartupFlushCompleted = (_, _) => done.Set();
            });
            Assert.True(done.Wait(TimeSpan.FromSeconds(10))); Assert.NotEqual(caller, worker);
            Assert.Equal("900150983cd24fb0d6963f7d28e17f72", robot.Engine.Das.GetGlobalsForThisRun()["$player_id"]);
            Assert.Equal(EngineLogLevel.Event, entry!.Level); Assert.Equal("live", entry.Text); Assert.Null(entry.Channel); Assert.Empty(entry.Data);
            File.Delete(path);
            using var second = CozmoRobot.CreateForTest(new Port(), () => 0, new CozmoEngineOptions { DasDeviceIdPath = path });
            string id = File.ReadLines(path).First(); Assert.True(Guid.TryParse(id, out _));
            using var third = CozmoRobot.CreateForTest(new Port(), () => 0, new CozmoEngineOptions { DasDeviceIdPath = path });
            Assert.Equal(second.Engine.Das.GetGlobalsForThisRun()["$player_id"], third.Engine.Das.GetGlobalsForThisRun()["$player_id"]);
        }
        finally { Directory.Delete(root, true); }
    }
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void CliffUnknownHistoryStaysVisibleAndKnownBranchesLogBeforeErrorFlag()
    {
        using var robot = TestRobot(); var missing = new List<string>(); var entries = new List<EngineLogEntry>();
        robot.Engine.StackLogLine += missing.Add; robot.Engine.LogEntry += entries.Add;
        robot.Engine.Broadcast(new CliffEvent { Timestamp = 111, DetectedFlags = 1 });
        Assert.DoesNotContain(missing,s=>s.Contains("M11-097"));
        Assert.Equal(new[]{"CliffSensorComponent.ComputeCliffPose.NoHistoricalPose","RobotImplMessaging.HandleCliffEvent.ComputeCliffPoseFailed"},entries.Select(e=>e.Name));entries.Clear();
        robot.Sensors.CliffHistoricalPoseForTest = _ => (0, new Pose3d(Mat3.Identity, new Vec3(12.5, -3.25, 0)));
        robot.Engine.Broadcast(new CliffEvent { Timestamp = 222, DetectedFlags = 0x0a });
        Assert.Equal("at 12.500,-3.250. DetectedFlags = 0x0A", Assert.Single(entries.Where(e => e.Name == "RobotImplMessaging.HandleCliffEvent.Detected")).Text);
        Assert.Contains(entries, e => e.Name == "BlockWorld.AddLocatedObject");
        Assert.Contains(missing, s => s.Contains("M11-062")); Assert.Contains(missing, s => s.Contains("M11-048"));
        entries.Clear(); robot.Sensors.CliffHistoricalPoseForTest = _ => (2, default);
        EngineErrorState.ErrorFlagSet = false; bool? atError = null;
        robot.Engine.LogEntry += e => { if (e.Name == "CliffSensorComponent.ComputeCliffPose.NoHistoricalPose") atError = EngineErrorState.ErrorFlagSet; };
        robot.Engine.Broadcast(new CliffEvent { Timestamp = 333, DetectedFlags = 1 });
        Assert.Equal(new[] { "CliffSensorComponent.ComputeCliffPose.NoHistoricalPose", "RobotImplMessaging.HandleCliffEvent.ComputeCliffPoseFailed" }, entries.Select(e => e.Name));
        Assert.Equal(false, atError); Assert.True(EngineErrorState.ErrorFlagSet);
        entries.Clear(); Assert.Equal(0, robot.Sensors.ComputeCliffPose(new CliffEvent(), out _));
        Assert.Equal("CliffSensorComponent.ComputeCliffPose.NoCliff", Assert.Single(entries).Name);
    }
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void SaveFailureUsesNonVirtualExceptionTextBeforeFlag()
    {
        string directory = Directory.CreateTempSubdirectory("save13-").FullName;
        try
        {
            var pool = new CubeConnections(_ => {}, () => 1);
            pool.Init(directory); pool.EnableAutoBlockPool(true);
            pool.OnObjectAvailable(0x123, ObjectType.Block_LIGHTCUBE1, 10);
            string? text = null; bool? flag = null;
            pool.SourceLog += (l,c,k,t) => { if (k == "BlockFilter.Save.Error") { text = t; flag = EngineErrorState.ErrorFlagSet; Assert.Equal(EngineLogLevel.Error, l); Assert.Null(c); } };
            EngineErrorState.ErrorFlagSet = false; pool.Update();
            Assert.Equal("std::exception", text); Assert.Equal(false, flag); Assert.True(EngineErrorState.ErrorFlagSet);
        }
        finally { Directory.Delete(directory, true); }
    }
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void DifferentIdsWarningUsesFirstLocatedIdThenThisIdAndRootNames()
    {
        using var robot = TestRobot();
        using var vision = new VisionSystem(robot, CameraCalibration.Nominal());
        var world = vision.World;
        world.AddLocatedObject(new ObservableObject(41, ObjectType.Block_LIGHTCUBE1, Array.Empty<KnownMarker>()) { ActiveId = 0, OriginId = 1, PoseState = PoseState.Known });
        world.AddLocatedObject(new ObservableObject(42, ObjectType.Block_LIGHTCUBE1, Array.Empty<KnownMarker>()) { ActiveId = 0, OriginId = 2, PoseState = PoseState.Known });
        robot.Cubes.ConnectedObjectIdForActiveId = _ => 41;
        robot.Engine.Broadcast(new ObjectConnectionState { ObjectID = 0, FactoryID = 123, ObjectType = ObjectType.Block_LIGHTCUBE1, Connected = true });
        var entries = new List<EngineLogEntry>(); robot.Engine.LogEntry += entries.Add;
        robot.Engine.Broadcast(new ObjectMoved { ObjectID = 0, Timestamp = 111 });
        var warning = Assert.Single(entries.Where(e => e.Name!.EndsWith("ActiveObjectInDifferentFramesWithDifferentIDs")));
        Assert.Equal("(tc0000) : First object=41 in 'Origin1'. This object=42 in 'Origin2'.", warning.Text);
    }
    private sealed class Port : IEngineTransport
    {
        public bool TimedOut => false;
        public event Action<ReceiverEvent>? Received { add {} remove {} }
        public void Start() {} public void Connect(System.Net.IPAddress ip, bool simulated) {}
        public void Disconnect(System.Net.IPEndPoint address) {} public void SendData(byte[] data) {}
    }
}
