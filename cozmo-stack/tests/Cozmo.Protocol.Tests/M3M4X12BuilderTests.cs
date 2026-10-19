using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Transport;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public class M3M4X12BuilderTests : IDisposable
{
    private readonly bool _error = EngineErrorState.ErrorFlagSet;
    private readonly string _dasRoot = Directory.CreateTempSubdirectory("das12-").FullName;
    public void Dispose() { EngineErrorState.ErrorFlagSet = _error; Directory.Delete(_dasRoot, true); }

    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void DropCountUsesUnsignedMaximumAndStoresBeforeSourceEvent()
    {
        using var robot = CozmoRobot.CreateOffline();
        var state = robot.Engine.Robot!;
        Assert.Equal((byte)0, state.ClientDropCount); // memclr8 +238, length12 at0x0050FD10.
        var entries = new List<EngineLogEntry>();
        robot.Engine.LogEntry += e =>
        {
            if (e.Name != "Robot.ClientDropCount.Incremented") return;
            Assert.Equal(state.ClientDropCount.ToString(), e.Text); entries.Add(e);
        };
        state.TimeSynced = false;
        robot.Engine.Broadcast(new AnimationState { ClientDropCount = 255 }); Assert.Empty(entries);
        state.TimeSynced = true;
        foreach (byte value in new byte[] { 0, 1, 1, 200, 127, 255, 0 })
            robot.Engine.Broadcast(new AnimationState { ClientDropCount = value });
        Assert.Equal(new[] { "1", "200", "255" }, entries.Select(e => e.Text));
        Assert.All(entries, e => { Assert.Equal(EngineLogLevel.Event, e.Level); Assert.Null(e.Channel); Assert.Empty(e.Data); });
        Assert.Equal((byte)255, state.ClientDropCount);
    }

    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void DasStartupControlOrderUsesCheckedArgumentsAndReportsMissingInputs(bool enabled)
    {
        var calls = new List<DasControlCall>(); var missing = new List<string>();
        bool? success = null; string? response = null; bool? pausedAtNeedsEdge = null;
        using var completed = new ManualResetEventSlim();
        using var robot = CozmoRobot.CreateForTest(new Port(), () => 0,
            new CozmoEngineOptions { DataCollectionEnabled = enabled, DasDeviceIdPath = Path.Combine(_dasRoot, "id") }, engineCreated: e =>
            {
                e.Das.CallObserved = calls.Add; e.StackLogLine += missing.Add;
                e.ConnectToRobotHandled += () => pausedAtNeedsEdge = e.Das.Paused;
                e.DasStartupFlushCompleted = (ok, text) => { success = ok; response = text; completed.Set(); };
            });
        Assert.Equal(new[] { "DASSetArchiveLogConfig", enabled ? "DASEnableNetwork" : "DASDisableNetwork",
            "DASConfigure", "DASNativeInit", "DASPauseUploadingToServer", "Appender.Flush", "_DAS_SetGlobal", "DASForceFlushWithCallback" }, calls.Select(c => c.Name));
        Assert.Equal(new object?[] { 0x00666C04u, 0x00666E44u, ".tar.gz" }, calls[0].Arguments);
        Assert.Equal(new object?[] { 2u }, calls[1].Arguments);
        // Native configure relative suffix and DataPlatform scopes; absolute roots explicitly unbuilt.
        Assert.Equal(new object?[] { new DasSourcePath(null, "/DASConfig.json"), new DasSourcePath(2, "DASLogs"), new DasSourcePath(3, "") }, calls[2].Arguments);
        Assert.Equal(new object?[] { "cozmo" }, calls[3].Arguments);
        Assert.Equal(new object?[] { false }, calls[4].Arguments);
        Assert.True(completed.Wait(TimeSpan.FromSeconds(10)));
        Assert.Equal(false, success); Assert.Equal("", response);
        Assert.DoesNotContain(missing, s => s.StartsWith("MISSING: M3-055 DASConfigure"));
        Assert.DoesNotContain(missing, s => s.Contains("$player_id MD5 input") || s.Contains("DasTransferTask.OnTransferReady"));
        calls.Clear(); robot.Engine.RequestDataCollectionOption(true); robot.Engine.Tick();
        Assert.Equal(0u, robot.Engine.Das.DisabledReasons);
        robot.Engine.Das.DisableNetwork(4); robot.Engine.RequestDataCollectionOption(false); robot.Engine.Tick();
        Assert.Equal(6u, robot.Engine.Das.DisabledReasons);
        robot.Engine.RequestDataCollectionOption(true); robot.Engine.Tick(); Assert.Equal(4u, robot.Engine.Das.DisabledReasons);
        calls.Clear(); robot.Engine.ConnectToRobot(System.Net.IPAddress.Loopback); robot.Engine.Tick();
        Assert.Contains(calls, c => c.Name == "DASPauseUploadingToServer" && Equals(c.Arguments[0], true));
        Assert.Equal(false, pausedAtNeedsEdge); // needs edge0x004ED10A precedes pause0x004ED114.
        robot.Engine.NotifyDisconnectServices();
        Assert.Equal("Appender.Flush", calls[^1].Name); Assert.False(robot.Engine.Das.Paused);
        robot.Engine.ClearGlobal("$session_id");
        Assert.Equal(new object?[] { "$session_id", null }, calls[^1].Arguments);
        robot.Engine.Das.SetGlobal("test", "value"); Assert.Equal("value", robot.Engine.Das.GetGlobalsForThisRun()["test"]);
        robot.Engine.ClearGlobal("test"); Assert.DoesNotContain("test", robot.Engine.Das.GetGlobalsForThisRun().Keys);
        robot.Engine.FlushSourceLogger(); Assert.Equal("DASForceFlushNow", calls[^1].Name);
        robot.Dispose(); Assert.Contains(calls, c => c.Name == "DASDisableNetwork" && Equals(c.Arguments[0], 4u));
    }

    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void CameraPendingClearGapIsVisibleAndDoesNotEmitStaleOverwriteWarning()
    {
        using var robot = CozmoRobot.CreateOffline(); var missing = new List<string>(); var keys = new List<string?>();
        robot.Engine.StackLogLine += missing.Add; robot.Engine.LogEntry += e => keys.Add(e.Name);
        robot.CameraSettings.Handle(new DefaultCameraParams { Field0 = 3f, Field1 = 1.5f, Field2 = 1, Field3 = 50, Field4 = new byte[17] });
        Assert.True(robot.CameraSettings.SetCameraSettings(20, 1.5f));
        // Historical name; this unit now asserts the restored native overwrite warning, not a pending-clear gap.
        Assert.Equal(2,keys.Count(k=>k=="VisionSystem.SetNextCameraParams.OverwritingPreviousParams"));
        Assert.DoesNotContain(missing, s => s.StartsWith("MISSING: M11-060"));
        Assert.Equal((20, 1.5f), robot.CameraSettings.NextCameraParams);
    }

    [Fact]
    // reach: unit (direct Broadcast and substituted connected-world lookup; explicit drain checks copied host reports).
    public void CubeReportsCarryTheirMessageTimestampsAndUpAxisHasWorldId()
    {
        using var robot = CozmoRobot.CreateOffline(); var cubes = robot.Cubes;
        cubes.ConnectedObjectIdForActiveId = _ => 41;
        robot.Engine.Broadcast(new ObjectConnectionState { ObjectID = 0, FactoryID = 123, ObjectType = ObjectType.Block_LIGHTCUBE1, Connected = true });
        cubes.SetBlockTapFilterEnabled(false);
        var moved = new List<CubeTimestampEvent>(); var taps = new List<CubeTimestampEvent>(); var axes = new List<CubeUpAxisEvent>();
        cubes.MovementBroadcast += moved.Add; cubes.TapBroadcast += taps.Add; cubes.UpAxisBroadcast += axes.Add;
        robot.Engine.Broadcast(new ObjectMoved { ObjectID = 0, Timestamp = 111 });
        robot.Engine.Broadcast(new ObjectStoppedMoving { ObjectID = 0, Timestamp = 222 });
        robot.Engine.Broadcast(new ObjectUpAxisChanged { ObjectID = 0, Timestamp = 333, UpAxis = (UpAxis)2 });
        robot.Engine.Broadcast(new ObjectTapped { ObjectID = 0, Timestamp = 444, TapPos = 100, TapNeg = 0 });
        Assert.Empty(moved);Assert.Empty(taps);Assert.Empty(axes);robot.Engine.DrainAppDelivery();
        Assert.Equal(new uint[] { 111, 222 }, moved.Select(m => m.Timestamp));
        Assert.Equal(new CubeUpAxisEvent(333, 41, (UpAxis)2), Assert.Single(axes));
        Assert.Equal(444u, Assert.Single(taps).Timestamp);
        Assert.Equal(444u, cubes.ByObjectId(0)!.LastBroadcastTimestamp);
        Assert.True(moved[0].Cube.Moving);Assert.False(moved[1].Cube.Moving);
        taps[0].Cube.LastTap!.Timestamp=999;Assert.Equal(444u,cubes.ByObjectId(0)!.LastTap!.Timestamp);
    }

    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void InvalidStateSourceErrorIsDeliveredBeforeFlag(bool connected)
    {
        var connection = new CubeConnections(_ => { }, () => 0);
        bool? flagAtError = null;
        connection.SourceLog += (l, c, k, t) => { if (k.EndsWith("InvalidState")) flagAtError = EngineErrorState.ErrorFlagSet; };
        if (connected) connection.OnConnectionState(0, 0, true); // initial invalid slot, matching zero factory id.
        EngineErrorState.ErrorFlagSet = false;
        connection.OnConnectionState(0, 0, connected);
        Assert.Equal(false, flagAtError); Assert.True(EngineErrorState.ErrorFlagSet);
    }

    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void ParseErrorDeliversOnceToEachRawStringAndLegacySink()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(path, System.Text.Encoding.ASCII.GetBytes("0x20,bad\0tail\n"));
            var legacy = new List<string>(); var text = new List<string>(); var raw = new List<byte[]>();
            var c = new CubeConnections(_ => { }, () => 0, legacy.Add);
            c.SourceLog += (l, ch, k, t) => { if (k == "BlockFilter.Load.ParseError") text.Add(t); };
            c.SourceLogBytes += (l, ch, k, t) => raw.Add(t.ToArray());
            c.Init(path);
            Assert.Equal("0x20,bad", Assert.Single(text)); Assert.Equal(System.Text.Encoding.ASCII.GetBytes("0x20,bad"), Assert.Single(raw));
            Assert.Single(legacy.Where(s => s.Contains("BlockFilter.Load.ParseError")));
        }
        finally { File.Delete(path); }
    }

    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void ClearSendBufferLogsFifoOutsideLockWithoutSending()
    {
        var stream = new StreamSendBuffer(); var logs = new List<string>(); int sends = 0;
        stream.Buffer(this, new HeadAngle(), _ => { sends++; return true; });
        stream.Buffer(this, new EndOfAnimation(), _ => { sends++; return true; });
        stream.SourceLog = (l, c, k, t) =>
        {
            Assert.True(Task.Run(() => stream.Count).Wait(TimeSpan.FromSeconds(2)));
            Assert.Equal((EngineLogLevel.Debug, "Unnamed", "AnimationStreamer.ClearSendBuffer"), (l, c, k)); logs.Add(t);
        };
        stream.ClearSendBuffer();
        Assert.Equal(new[] { "Clearing unsent animHeadAngle", "Clearing unsent animEndOfAnimation" }, logs);
        Assert.Equal(0, sends); Assert.Equal(0, stream.Count);
    }
    private sealed class Port : IEngineTransport
    {
        public bool TimedOut => false;
        public event Action<ReceiverEvent>? Received { add { } remove { } }
        public void Start() { } public void Connect(System.Net.IPAddress ip, bool simulated) { }
        public void Disconnect(System.Net.IPEndPoint address) { } public void SendData(byte[] data) { }
    }
}
