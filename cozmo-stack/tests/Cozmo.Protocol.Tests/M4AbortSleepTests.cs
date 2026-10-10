using System.Net;
using System.Text;
using Cozmo.Robot;
using Cozmo.Transport;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M4-026 (Robot::AbortAll, PathComponent::Abort/ClearPath, MovementComponent::StopAllMotors/UnlockTracks), M4-027
/// (the Robot's component teardown), M4-028 (the go-to-sleep lift child) and M4-031 (PrintLockState). Every expected value
/// comes from the rows of re-analysis/research/20261009-M3M4-remaining-rows.md (A1..A10, T1..T27, L1..L15, D1..D3) as
/// corrected by re-analysis/jobs/B-M3M4.md "Rows checked (manager, 2026-10-10)", named in each test with its citation
/// (disassembly addresses of libcozmoEngine.so). The live entry is RobotManager::RemoveRobot's deletion of the Robot
/// (RobotLifetime.Destroy: slot -3 is AbortAll at 0x00511120) on the production CozmoRobot.
/// </summary>
[Collection("SteppedBehavior missing-report statics")]
public class M4AbortSleepTests
{
    private sealed class FakePort : IEngineTransport
    {
        public readonly List<byte[]> Sent = new();
        public bool TimedOut { get; set; }
        public event Action<ReceiverEvent>? Received;
        public void Start() { }
        public void Connect(IPAddress ip, bool isSimulated) { }
        public void Disconnect(IPEndPoint address) { }
        public void SendData(byte[] clad) { lock (Sent) Sent.Add(clad); }
        public void Raise(ReceiverMarker m, IPEndPoint? a, byte[]? d = null) => Received?.Invoke(new ReceiverEvent(m, a, d));
    }

    private sealed class Rig : IDisposable
    {
        public long NowNs = 1_000_000_000;
        public readonly FakePort Port = new();
        public readonly CozmoRobot Robot;
        public CozmoEngine Engine => Robot.Engine;
        public readonly List<string> Log = new();
        public static readonly IPAddress RobotIp = IPAddress.Parse("172.31.1.1");
        public static readonly IPEndPoint RobotEp = new(RobotIp, 5551);
        private uint _ts = 100;

        public Rig()
        {
            Robot = CozmoRobot.CreateForTest(Port, () => NowNs, new CozmoEngineOptions { BlockPoolPath = "" });
            Engine.LogLine += l => { lock (Log) Log.Add(l); };
        }

        public void Tick(double ms = 60) { NowNs += (long)(ms * 1_000_000); Engine.Tick(); }
        public void Data(RobotMessage m) => Port.Raise(ReceiverMarker.Data, RobotEp, m.ToBytes());

        public void ToSynced()
        {
            Engine.ConnectToRobot(RobotIp); Tick();
            Port.Raise(ReceiverMarker.OnConnected, RobotEp); Tick();
            Data(new RobotAvailable { SerialNumberHead = 1, HwVersion = 5 });
            Data(new FirmwareVersion { RobotId = 1, Signature = Encoding.UTF8.GetBytes("{\"version\": 2381, \"time\": 1546972025, \"build\": \"DEVELOPMENT\"}") });
            Tick();
            Data(new ManufacturingID { SerialNumber = 2, BodyHwVersion = 7, BodyColor = 2 });
            Tick();
            Data(new SyncTimeAck());
            Tick();
        }

        public void State(RobotStatusFlag flags = RobotStatusFlag.IsBodyAccMode, float liftAngle = 0f, double tickMs = 60)
        {
            Data(new RobotState
            {
                Timestamp = _ts += 33, PoseFrameId = 0, PoseOriginId = 1, Pose = new RobotPose(),
                HeadAngle = 0, LiftAngle = liftAngle, BatteryVoltage = 4.0f, Status = (uint)flags,
                Accel = new AccelData { Z = 9800 }, Gyro = new GyroData(), CliffDataRaw = new ushort[] { 500, 500, 500, 500 },
            });
            Tick(tickMs);
        }

        public int Mark() { lock (Port.Sent) return Port.Sent.Count; }
        public List<string> HexSince(int mark) { lock (Port.Sent) return Port.Sent.Skip(mark).Select(Convert.ToHexString).ToList(); }
        public List<RobotMessage> SentSince(int mark) { lock (Port.Sent) return Port.Sent.Skip(mark).Select(b => RobotMessage.Parse(b)).ToList(); }
        public List<string> LogLines() { lock (Log) return Log.ToList(); }
        public void Dispose() => Robot.Dispose();
    }

    private static void Remove(Rig rig) => rig.Engine.Robots.RemoveRobot(CozmoEngine.RobotId, wasConnecting: false);

    // The engine's four AbortAll messages, in the engine's order: ClearPath{u16 0} 0x3C (A4), AbortDocking 0x43 (A5),
    // AbortAnimation 0x8D (A6), StopAllMotors 0x3B (A8). EnableAnimTracks is 0x9E (A10).
    private const string ClearPathHex = "3C0000", AbortDockingHex = "43", AbortAnimationHex = "8D", StopAllMotorsHex = "3B";

    // ------------------------------------------------------------------------------------------------ M4-026

    /// <summary>
    /// M4-026 A1 (0x0051194C..0x0051198E; call sites 0x0051195A, 0x00511960, 0x0051196A, 0x00511972, 0x0051197C) with A4/A5/A6/A8:
    /// the Robot's deletion calls AbortAll (0x00511120), which cancels the actions, then Path.Abort (its ClearPath sends
    /// ClearPath{0}, 0x0064927C), AbortDocking (0x0063BE36), SendAbortAnimation (0x00517E0A) and StopAllMotors (0x006409C2), in that
    /// order. With no direct-drive flag set StopAllMotors sends only its own message (A7's outer gate, 0x0063FBE6..0x0063FBF4).
    /// </summary>
    [Fact]
    public void M4_026_A1_RemovingTheRobotAbortsInTheEnginesOrder()
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.State();
        int mark = rig.Mark();
        Remove(rig);
        Assert.Equal(new[] { ClearPathHex, AbortDockingHex, AbortAnimationHex, StopAllMotorsHex }, rig.HexSince(mark));
        Assert.Contains(rig.LogLines(), l => l.StartsWith("info: [Planner] PathComponent.Abort: Aborting from status '"));
    }

    /// <summary>
    /// M4-026 A1 + A7 + A10: with the BODY direct drive set the outer gate passes and the five zero-speed helper calls run
    /// (0x0063FC4C..0x0063FDEE); the first BODY call unlocks the direct drive's entry and its track becomes empty, so one
    /// EnableAnimTracks{4} (tag 0x9E, u8 mask, 0x0063FFA8..0x0063FFB4) goes out before StopAllMotors (A8). The helper calls for
    /// HEAD and LIFT find no lock on their tracks and do nothing, and the later BODY calls find the track empty.
    /// </summary>
    [Fact]
    public void M4_026_A7_A10_TheDirectDriveUnlockGoesOutBeforeStopAllMotors()
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.State();
        _ = rig.Robot.Motion.DriveWheelsAsync(20f, 20f, confirmWithin: TimeSpan.FromMilliseconds(1), requireCalibration: false);
        int mark = rig.Mark();
        Remove(rig);
        Assert.Equal(new[] { ClearPathHex, AbortDockingHex, AbortAnimationHex, "9E04", StopAllMotorsHex }, rig.HexSince(mark));
    }

    /// <summary>
    /// M4-026 A7 (five calls: HEAD 1, LIFT 2, BODY 4, BODY 4, BODY 4; 0x0063FC4C, 0x0063FCB6, 0x0063FD1E, 0x0063FD86, 0x0063FDEE) and
    /// A9 (0x0063EFB0..0x0063F0BC) and A10. The BODY direct drive and a second holder both hold BODY. The first BODY call
    /// unlocks the direct drive's entry and the track is still locked, so UnlockTracks returns true and the helper logs ERROR
    /// "Locks left on tracks %s [0x%x] after %s[%s] unlocked" (0x0063F036..0x0063F048) and sets _errG (0x0063F08C). The second and
    /// third BODY calls find the track still locked, the direct drive's key missing: INFO "Tracks 0x%x are not currently locked
    /// by %s" each (0x0063FF4A) and no error. HEAD and LIFT are empty, so their calls do nothing. No EnableAnimTracks goes out
    /// because BODY is never empty.
    /// </summary>
    [Fact]
    public void M4_026_A7_A9_TheFiveHelperCallsAndTheLocksLeftError()
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.State();
        _ = rig.Robot.Motion.DriveWheelsAsync(20f, 20f, confirmWithin: TimeSpan.FromMilliseconds(1), requireCalibration: false);
        rig.Robot.Motion.LockTracks(Cozmo.Robot.CozmoMotion.BodyTrack, "X");
        int mark = rig.Mark();
        int logMark = rig.LogLines().Count;
        EngineErrorState.ErrorFlagSet = false;
        rig.Robot.Motion.StopAllMotors();
        Assert.Equal(new[] { StopAllMotorsHex }, rig.HexSince(mark));
        var lines = rig.LogLines().Skip(logMark).ToList();
        var errors = lines.Where(l => l.StartsWith("error: MovementComponent.DirectDriveCheckSpeedAndLockTracks: ")).ToList();
        var error = Assert.Single(errors);
        // row 1b / row 1: call 3 (BODY) has key +0xBC and debug name +0xBC; "after %s[%s]" prints the name then the key
        Assert.Equal("error: MovementComponent.DirectDriveCheckSpeedAndLockTracks: Locks left on tracks BODY_TRACK [0x4] after DirectDriveWheels[DirectDriveWheels] unlocked", error);
        Assert.True(EngineErrorState.ErrorFlagSet);
        Assert.Equal(2, lines.Count(l => l == "info: [Unnamed] MovementComponent.UnlockTracks: Tracks 0x4 are not currently locked by DirectDriveWheels"));
        Assert.Equal(2, lines.Count(l => l.StartsWith("debug: [Unnamed] MovementComponent.LockState: ")));
    }

    /// <summary>
    /// M4-026 A1: the engine's Robot::SendMessage returns a Result, 0 success and 1 failure when not connected (0x0051349C..0x005134BC), and
    /// AbortAll returns 1 when any of the three failed (orr, orrs, movne r0,#1: 0x00511980..0x00511988). Connected, all three succeed: 0.
    /// With every send failing: 1.
    /// </summary>
    [Fact]
    public void M4_026_A1_TheResultIsTheEnginesFailIfAnyFailed()
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.State();
        Assert.Equal(0, rig.Robot.Motion.AbortAll(rig.Engine.Robot!));
        var robot = rig.Engine.Robot!;
        robot.SendFault = _ => false;                          // the test seam: every send fails, as with no connection
        Assert.Equal(1, rig.Robot.Motion.AbortAll(robot));
    }

    /// <summary>
    /// M4-026 A1/A3/A4 with Robot::SendMessage's failure warning (0x00513558 channel "Robot.SendMessage", 0x0051356C "Robot %d failed to send a message type %s"):
    /// HandleDisconnectMessage clears the connection data before RemoveRobot (CC23), so every send of AbortAll fails and warns, in AbortAll's order:
    /// ClearPath, AbortDocking, AbortAnimation, StopAllMotors. Nothing goes out on the wire.
    /// </summary>
    [Fact]
    public void M4_026_A1_RemovalAfterTheLinkIsDownWarnsForEachFailedSend()
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.State();
        int mark = rig.Mark(), logMark = rig.LogLines().Count;
        rig.Port.Raise(ReceiverMarker.OnDisconnected, Rig.RobotEp);
        rig.Tick();
        Assert.Empty(rig.HexSince(mark));
        var warnings = rig.LogLines().Skip(logMark).Where(l => l.StartsWith("warning: Robot.SendMessage: Robot 1 failed to send a message type ")).ToList();
        // Type names from EngineToRobotTagToString 0x007AF8D0 (idx = (tag ^ 0x80) & 0xFF into the table at 0x010343A0):
        // 0x3C -> 0x00C1EFA6 "clearPath", 0x43 -> 0x00C1F00E "abortDocking", 0x8D -> 0x00C1F262 "abortAnimation",
        // 0x3B -> 0x00C1EFA1 "stop".
        Assert.Equal(new[]
        {
            "warning: Robot.SendMessage: Robot 1 failed to send a message type clearPath",
            "warning: Robot.SendMessage: Robot 1 failed to send a message type abortDocking",
            "warning: Robot.SendMessage: Robot 1 failed to send a message type abortAnimation",
            "warning: Robot.SendMessage: Robot 1 failed to send a message type stop",
        }, warnings);
    }

    /// <summary>
    /// M4-026 A3 (PathComponent::PathComponent 0x00648B14 movs r0,#4; 0x00648B16 strd r0,r5,[fp,#0x38]): the status starts at 4 (Ready) and +0x40 at
    /// 0xFF (0x00648B1A..0x00648B1C), so a freshly built Robot's removal logs "Aborting from status 'Ready'" (name table row 4).
    /// </summary>
    [Fact]
    public void M4_026_A3_AFreshRobotsRemovalAbortsFromReady()
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.State();
        Assert.Equal(4, rig.Robot.Motion.Path.DriveToPoseStatus);
        Assert.Equal(0xFF, rig.Robot.Motion.Path.Field40);
        Remove(rig);
        Assert.Contains("info: [Planner] PathComponent.Abort: Aborting from status 'Ready'", rig.LogLines());
    }

    /// <summary>M4-026 A4: PathDolerOuter (+4) is always built by the engine (0x00648B76..0x00648B92); an unbuilt one is reported MISSING once.</summary>
    [Fact]
    public void M4_026_A4_AnUnbuiltPathDolerIsReportedMissing()
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.State();
        var path = new PathComponent(rig.Robot);
        path.Abort(); path.Abort();
        Assert.Equal(1, rig.LogLines().Count(l => l.StartsWith("MISSING: PathDolerOuter::ClearPath 0x005082B0")));
    }

    /// <summary>
    /// M4-026 A10 (0x0063FE5C..0x0063FFDC, with the manager's corrections): a missing key on a track that is already empty
    /// still adds its bit to the EnableAnimTracks mask (the count is read again at 0x0063FF7A and falls into 0x0063FF7E..0x0063FF88).
    /// HEAD is held by "A", LIFT is empty, the key "none" is in neither: INFO for each missing bit (the whole u8 mask 0x3 in the
    /// text), the mask is 2 (LIFT) only, and the return value is false (it is OR'd only on a found-and-erased path).
    /// </summary>
    [Fact]
    public void M4_026_A10_AMissingKeyOnAnEmptyTrackStillAddsItsBit()
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.State();
        var m = rig.Robot.Motion;
        m.LockTracks(1, "A");
        int mark = rig.Mark();
        int logMark = rig.LogLines().Count;
        bool result = m.UnlockTracks(3, "none");
        Assert.False(result);
        Assert.Equal(new[] { "9E02" }, rig.HexSince(mark));
        var lines = rig.LogLines().Skip(logMark).ToList();
        Assert.Equal(2, lines.Count(l => l == "info: [Unnamed] MovementComponent.UnlockTracks: Tracks 0x3 are not currently locked by none"));
    }

    /// <summary>
    /// M4-026 A10: the return value is OR'd only on the found-and-erased path, when the track still has entries afterwards
    /// (0x0063FF1A..0x0063FF22): HEAD held by "A" and "B": unlocking "A" returns true and sends nothing; unlocking the missing
    /// "zzz" on the still-locked HEAD returns false and sends nothing; unlocking "B" empties HEAD and sends EnableAnimTracks{1}
    /// (the u8 mask is not zero, 0x0063FF96), returning false.
    /// </summary>
    [Fact]
    public void M4_026_A10_TheReturnValueAndTheMaskGate()
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.State();
        var m = rig.Robot.Motion;
        m.LockTracks(1, "A");
        m.LockTracks(1, "B");
        int mark = rig.Mark();
        Assert.True(m.UnlockTracks(1, "A"));
        Assert.Empty(rig.HexSince(mark));
        Assert.False(m.UnlockTracks(1, "zzz"));
        Assert.Empty(rig.HexSince(mark));
        Assert.False(m.UnlockTracks(1, "B"));
        Assert.Equal(new[] { "9E01" }, rig.HexSince(mark));
        // a mask of zero sends nothing and logs nothing
        int logMark = rig.LogLines().Count;
        Assert.False(m.UnlockTracks(0, "B"));
        Assert.Equal(new[] { "9E01" }, rig.HexSince(mark));
        Assert.Equal(logMark, rig.LogLines().Count);
    }

    /// <summary>
    /// M4-026 A9 through the live direct-drive path: MoveHead(0) after the direct drive and a second holder both hold HEAD. The helper
    /// unlocks its own entry (a found key), the track stays locked, UnlockTracks returns true, and the ERROR is logged
    /// with the mask's AnimTrackFlagsToString ("HEAD_TRACK", strings 0x00C20053) and 0x1 (0x0063F036..0x0063F048).
    /// </summary>
    [Fact]
    public void M4_026_A9_MoveHeadZeroLogsTheLocksLeftError()
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.State();
        var m = rig.Robot.Motion;
        m.MoveHead(2f, requireCalibration: false);
        m.LockTracks(1, "X");
        int logMark = rig.LogLines().Count;
        EngineErrorState.ErrorFlagSet = false;
        m.MoveHead(0f, requireCalibration: false);
        var lines = rig.LogLines().Skip(logMark).ToList();
        var error = Assert.Single(lines.Where(l => l.StartsWith("error: MovementComponent.DirectDriveCheckSpeedAndLockTracks: ")));
        Assert.EndsWith("Locks left on tracks HEAD_TRACK [0x1] after DirectDriveHead[DirectDriveHead] unlocked", error);
        Assert.True(EngineErrorState.ErrorFlagSet);
        Assert.Equal(1, m.LockedTracks);       // "X" still holds HEAD
    }

    /// <summary>
    /// M4-026 A3/A4 (0x00649100..0x006491BC, 0x00649220..0x006492A0), the order and each store, with the manager's corrections:
    /// the INFO first; the planner's virtual +0x10 and +0x47 = 0 (only with a planner); the shared pair nulled; ClearPath (a non-zero
    /// +0x42 copied to +0x4A, VizManager::ErasePath(robot id), PathDolerOuter::ClearPath, +0x40 = 0xFF, +0x3C = the engine seconds,
    /// ClearPath{0} sent); status 0, 1 or 4 request 4 and 2 or 3 request 5 (0x0064916E..0x00649188); +0x46 = 0; the stored objects
    /// destroyed back to front through their vtable slot 0 with no delete (0x00649198..0x006491A4); then UnknownOriginID (0) stored
    /// to [[+0x50]]+0x0C (0x006491AC..0x006491B4). The return value is ClearPath's send result.
    /// </summary>
    [Theory]
    [InlineData(0, 4)]
    [InlineData(1, 4)]
    [InlineData(2, 5)]
    [InlineData(3, 5)]
    [InlineData(4, 4)]
    [InlineData(5, -1)]
    [InlineData(6, -1)]
    public void M4_026_A3_A4_PathAbortSequence(int status, int requested)
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.State();
        var path = new PathComponent(rig.Robot);
        var order = new List<string>();
        path.DriveToPoseStatus = status;
        path.StatusName = s => $"status{s}";
        path.PlannerSlot10 = () => order.Add("planner+0x10");
        path.Field47 = true; path.Field46 = true;
        path.SharedPath = new object();
        path.Field42 = 0x1234; path.Field4A = 0x0001;
        path.PlannerObjectsField0C = 7;
        path.ErasePathHook = id => order.Add($"erase:{id}");
        path.PathDolerClearPath = () => order.Add("doler");
        path.SetDriveToPoseStatusHook = s => order.Add($"status:{s}");
        for (int i = 0; i < 3; i++) { int n = i; path.PlannerObjects.Add(() => { order.Add($"destroy{n}"); Assert.Equal(n, path.PlannerObjects.Count); }); }
        int mark = rig.Mark();
        int result = path.Abort();
        Assert.Equal(0, result);                       // ClearPath's send succeeded: Result 0
        Assert.Equal(new[] { "3C0000" }, rig.HexSince(mark));
        var expected = new List<string> { "planner+0x10", "erase:1", "doler" };
        if (requested >= 0) expected.Add($"status:{requested}");
        expected.AddRange(new[] { "destroy2", "destroy1", "destroy0" });
        Assert.Equal(expected, order);
        Assert.False(path.Field47);
        Assert.False(path.Field46);
        Assert.Null(path.SharedPath);
        Assert.Equal(0x1234, path.Field4A);
        Assert.Equal(0xFF, path.Field40);
        Assert.Equal(0u, path.PlannerObjectsField0C);
        Assert.Empty(path.PlannerObjects);
        Assert.Contains($"info: [Planner] PathComponent.Abort: Aborting from status 'status{status}'", rig.LogLines());
    }

    /// <summary>
    /// M4-026 A3/A4: with no planner +0x47 is not cleared (only that branch writes it, 0x00649154..0x00649156); a zero +0x42 does not
    /// overwrite +0x4A (0x00649234..0x0064923A).
    /// </summary>
    [Fact]
    public void M4_026_A3_A4_NoPlannerLeavesByte47AndAZeroPathIdLeaves4A()
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.State();
        var path = new PathComponent(rig.Robot) { Field47 = true, Field4A = 9, Field42 = 0 };
        path.ErasePathHook = _ => { };
        path.SetDriveToPoseStatusHook = _ => { };
        Assert.Equal(0, path.Abort());
        Assert.True(path.Field47);
        Assert.Equal(9, path.Field4A);
    }

    /// <summary>
    /// M4-026 A3/A4: the status transition and VizManager are M11's. With no hook the component reports MISSING once and does not
    /// guess a result.
    /// </summary>
    [Fact]
    public void M4_026_A3_AnUnbuiltRecipientIsReportedMissing()
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.State();
        var path = new PathComponent(rig.Robot);
        path.Abort();
        path.Abort();
        var lines = rig.LogLines();
        Assert.Equal(1, lines.Count(l => l.StartsWith("MISSING: PathComponent::SetDriveToPoseStatus 0x006492C4")));
        Assert.Equal(1, lines.Count(l => l.StartsWith("MISSING: VizManager::ErasePath 0x006BFACE")));
    }

    // ------------------------------------------------------------------------------------------------ M4-027

    /// <summary>
    /// M4-027 T4/T8/T9/T18 (0x00511500..0x00511512, 0x00641CFC..0x00641D38): the Movement deleting destructor destroys the lock trees
    /// and calls no StopAllMotors, UnlockTracks or EnableAnimTracks. HEAD is held by another holder when the Robot is removed:
    /// the messages are exactly AbortAll's four (the direct-drive flags are clear, so A7's gate keeps its helper calls away), no
    /// EnableAnimTracks follows, and afterwards nothing is locked.
    /// </summary>
    [Fact]
    public void M4_027_T4_TheMovementDestructorSendsNothing()
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.State();
        rig.Robot.Motion.LockTracks(1, "X");
        int mark = rig.Mark();
        Remove(rig);
        Assert.Equal(new[] { ClearPathHex, AbortDockingHex, AbortAnimationHex, StopAllMotorsHex }, rig.HexSince(mark));
        Assert.Equal(0, rig.Robot.Motion.LockedTracks);
    }

    /// <summary>
    /// M4-027 T3/T7/T17 (0x00511428..0x0051143A, 0x00635444..0x0063546E): the CubeAccel deleting destructor clears the subscription
    /// list and destroys the per-object histories and listener sets; it sends no StreamObjectAccel. The stack's component is
    /// emptied and nothing goes out for it.
    /// </summary>
    [Fact]
    public void M4_027_T7_TheCubeAccelDestructorDropsListenersWithoutAMessage()
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.State();
        rig.Robot.CubeAccel.AddListener(3, new CubeShakeListener(0.5f, 2.5f, 3.9f, _ => { }));
        Assert.Single(rig.Robot.CubeAccel.Streaming);
        int mark = rig.Mark();
        Remove(rig);
        Assert.DoesNotContain(rig.SentSince(mark), x => x is StreamObjectAccel);
        Assert.Empty(rig.Robot.CubeAccel.Streaming);
    }

    /// <summary>
    /// M4-027 T3/T15/T16/T24/T25 (0x0051143E..0x0051148C): the backpack and cube light components are deleted without a light-off
    /// send ("Weak release is not an explicit unsubscribe or a light-off send", the CurrentAnimInfo function objects are destroyed,
    /// not invoked). A backpack pattern is playing when the Robot is removed: nothing but AbortAll's four messages goes out.
    /// </summary>
    [Fact]
    public void M4_027_T15_TheLightDestructorsSendNoLightOff()
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.State();
        rig.Robot.Lights.SetBackpack(LedColor.Red);
        int mark = rig.Mark();
        Remove(rig);
        Assert.Equal(new[] { ClearPathHex, AbortDockingHex, AbortAnimationHex, StopAllMotorsHex }, rig.HexSince(mark));
    }

    /// <summary>
    /// M4-027: the slots the stack's components are bound to log no MISSING (they are built); Touch (+0x28C) and Cliff (+0x288),
    /// whose RollingFileLogger close and component are not built, still report the missing owner.
    /// </summary>
    [Fact]
    public void M4_027_BoundSlotsAreQuietAndUnbuiltOnesAreReported()
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.State();
        Remove(rig);
        var lines = rig.LogLines();
        foreach (var offset in new[] { "+0x254", "+0x278", "+0x274", "+0x270", "+0x450" })
            Assert.DoesNotContain(lines, l => l.StartsWith("MISSING: Robot lifetime owner " + offset + " "));
        Assert.Contains(lines, l => l.StartsWith("MISSING: Robot lifetime owner +0x28C "));
        Assert.Contains(lines, l => l.StartsWith("MISSING: Robot lifetime owner +0x288 "));
    }

    // ------------------------------------------------------------------------------------------------ M4-028

    /// <summary>
    /// M4-028 L1..L9/L12/L13 through the live action list: the go-to-sleep sequence's lift child is added to a parallel parent with
    /// both flags zero (0x0052CFB0) and the parent is queued. The child's Init (0x0054903C) is not in position (45 mm vs the preset-0
    /// height 32 mm, tolerance 5.0): it increments the motor action id (u8, 0x00640700..0x00640742) and sends SetLiftHeight{32.0
    /// (0x42000000), speed 10 (0x41200000), accel 20 (0x41A00000), duration 0, id} (0x00640786). The matching MotorActionAck logs Actions
    /// INFO "MoveLiftToHeightAction.MotorActionAcked" "[%d] ActionID: %d" (0x0054D784) each time it arrives. The lift reaching 32 mm and
    /// stopping completes the child, and with it the parent, with SUCCESS.
    /// </summary>
    [Fact]
    public void M4_028_L1_L9_L13_TheSleepLiftChildSendsPresetZeroAndLogsTheAck()
    {
        using var rig = new Rig();
        rig.ToSynced();
        var inPos = RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.HeadInPos | RobotStatusFlag.LiftInPos;
        rig.State(flags: inPos, liftAngle: 0f);                                        // 45 mm
        var parent = new CompoundActionParallel(() => rig.Engine.Timer.SecondsF);
        rig.Robot.Motion.AddGoToSleepLiftChild(parent);
        int mark = rig.Mark();
        rig.Engine.Robot!.ActionList.QueueAction(QueueActionPosition.Now, parent, 0);
        rig.Tick();
        var sent = Assert.IsType<SetLiftHeight>(rig.SentSince(mark).Single(m => m is SetLiftHeight));
        Assert.Equal(0x42000000, BitConverter.SingleToInt32Bits(sent.HeightMm));
        Assert.Equal(0x41200000, BitConverter.SingleToInt32Bits(sent.MaxSpeedRadPerSec));
        Assert.Equal(0x41A00000, BitConverter.SingleToInt32Bits(sent.AccelRadPerSec2));
        Assert.Equal(0f, sent.DurationSec);
        Assert.Equal(1, sent.ActionId);                                                  // 0 at construction, pre-incremented (MA8)
        // an ack for another id is ignored (0x0054D748..0x0054D76A: the ids must be equal)
        rig.Data(new MotorActionAck { ActionId = 9 }); rig.Tick();
        Assert.DoesNotContain(rig.LogLines(), l => l.Contains("MoveLiftToHeightAction.MotorActionAcked"));
        rig.Data(new MotorActionAck { ActionId = sent.ActionId }); rig.Tick();
        rig.Data(new MotorActionAck { ActionId = sent.ActionId }); rig.Tick();
        var acks = rig.LogLines().Where(l => l.Contains("MoveLiftToHeightAction.MotorActionAcked")).ToList();
        Assert.Equal(2, acks.Count);
        Assert.All(acks, a => Assert.EndsWith("] ActionID: 1", a));
        Assert.All(acks, a => Assert.StartsWith("info: [Actions] MoveLiftToHeightAction.MotorActionAcked: [", a));
        rig.State(flags: inPos, liftAngle: MathF.Asin((32f - 45f) / 66f));               // at 32 mm and stopped
        Assert.Equal(EngineActionResult.Success, parent.State);
    }

    /// <summary>
    /// M4-028 L6/L8 (0x0054903C..0x0054933A, IsLiftInPosition 0x00548FEA..0x0054903A): a lift already within the strict tolerance
    /// (|32 - h| &lt; 5.0) and not moving is in position at Init: nothing is sent and the child succeeds. 36.9 mm is inside; the tolerance
    /// is strict.
    /// </summary>
    [Fact]
    public void M4_028_L6_L8_InPositionAtInitSendsNothing()
    {
        using var rig = new Rig();
        rig.ToSynced();
        var inPos = RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.HeadInPos | RobotStatusFlag.LiftInPos;
        rig.State(flags: inPos, liftAngle: MathF.Asin((34f - 45f) / 66f));                // 34 mm
        var parent = new CompoundActionParallel(() => rig.Engine.Timer.SecondsF);
        rig.Robot.Motion.AddGoToSleepLiftChild(parent);
        int mark = rig.Mark();
        rig.Engine.Robot!.ActionList.QueueAction(QueueActionPosition.Now, parent, 0);
        rig.Tick();
        Assert.DoesNotContain(rig.SentSince(mark), m => m is SetLiftHeight);
        Assert.Equal(EngineActionResult.Success, parent.State);
    }

    /// <summary>
    /// M4-028 L2..L4 and the base timeout: the action's name is "MoveLiftTo" + "LowDock" (0x00BEA3A6 prefix, preset 0's name), which
    /// IAction::UpdateInternal's timeout warning prints with the default 30.0 s timeout (slot 0x2C, 0x0052B0C2).
    /// </summary>
    [Fact]
    public void M4_028_L2_L4_TheChildIsNamedMoveLiftToLowDock()
    {
        using var rig = new Rig();
        rig.ToSynced();
        var inPos = RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.HeadInPos | RobotStatusFlag.LiftInPos;
        rig.State(flags: inPos, liftAngle: 0f);
        var parent = new CompoundActionParallel(() => rig.Engine.Timer.SecondsF) { Log = rig.Engine.Log };   // the sequence's parent carries the engine log
        rig.Robot.Motion.AddGoToSleepLiftChild(parent);
        rig.Engine.Robot!.ActionList.QueueAction(QueueActionPosition.Now, parent, 0);
        rig.Tick();
        rig.Tick(30_000);
        Assert.Contains("warning: IAction.Update.TimedOut: MoveLiftToLowDock timed out after 30.0 seconds.", rig.LogLines());
    }

    /// <summary>
    /// M4-028 L14 (0x00640B3C..0x00640C2A): StopLift, when any direct-drive flag is set and +0xD4 is zero, makes ONE zero-speed
    /// helper call for the lift (mask 2) even if the lift's own flag is clear, then always sends MoveLift{0}. The HEAD direct drive
    /// is set and the lift is held by another holder: the helper finds the direct drive's key missing on LIFT (INFO, then
    /// PrintLockState with "LIFT_TRACK:1 [X] "), leaves the lock, and MoveLift{0} follows.
    /// </summary>
    [Fact]
    public void M4_028_L14_StopLiftRunsTheHelperWhenAnyDirectDriveFlagIsSet()
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.State();
        var m = rig.Robot.Motion;
        m.MoveHead(2f, requireCalibration: false);
        m.LockTracks(2, "X");
        int mark = rig.Mark();
        int logMark = rig.LogLines().Count;
        m.StopLift();
        var sent = rig.SentSince(mark);
        Assert.Single(sent);
        var move = Assert.IsType<MoveLift>(sent[0]);
        Assert.Equal(0f, move.SpeedRadPerSec);
        var lines = rig.LogLines().Skip(logMark).ToList();
        Assert.Single(lines, l => l == "info: [Unnamed] MovementComponent.UnlockTracks: Tracks 0x2 are not currently locked by DirectDriveLift");
        Assert.Contains("debug: [Unnamed] MovementComponent.LockState: HEAD_TRACK:1 DirectDriveHead[DirectDriveHead] \nLIFT_TRACK:1 [X] \n", lines);
    }

    /// <summary>
    /// M4-028 L14: with no direct-drive flag set StopLift sends only MoveLift{0}: no helper call, no log.
    /// </summary>
    [Fact]
    public void M4_028_L14_StopLiftWithNoFlagSendsOnlyMoveLift()
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.State();
        rig.Robot.Motion.LockTracks(2, "X");
        int mark = rig.Mark();
        int logMark = rig.LogLines().Count;
        rig.Robot.Motion.StopLift();
        Assert.Single(rig.SentSince(mark));
        Assert.Empty(rig.LogLines().Skip(logMark));
    }

    // ------------------------------------------------------------------------------------------------ M4-031

    /// <summary>
    /// M4-031 D1 (0x006410D8..0x006413CA) and D2/D3 (0x006305E8..0x00630788): tracks 0..7 ascending, an empty one skipped; per
    /// non-empty track the name, ':', the entry count, a space, then per entry (in tree order) the second string, '[', the key, "] ",
    /// then a newline; one DEBUG record "MovementComponent.LockState" (0x006414C8) with format "%s" (0x006414E4) on channel Unnamed. The
    /// tree is ordered by the key's bytes, an equal key inserted after the existing ones (__emplace_multi 0x006423D8; the inline
    /// compare 0x00642522..0x00642572): "aa" twice (d2 then d3), then "zz". Names: HEAD_TRACK (bit 0), LIFT_TRACK (bit 1),
    /// FACE_IMAGE_TRACK (bit 3).
    /// </summary>
    [Fact]
    public void M4_031_D1_D3_TheLockStateTextAndOrder()
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.State();
        var m = rig.Robot.Motion;
        m.LockTracks(1, "zz", "d1");
        m.LockTracks(1, "aa", "d2");
        m.LockTracks(1, "aa", "d3");
        m.LockTracks(2, "k", "dk");
        m.LockTracks(8, "f", "df");
        int logMark = rig.LogLines().Count;
        m.PrintLockState();
        var line = Assert.Single(rig.LogLines().Skip(logMark));
        Assert.Equal("debug: [Unnamed] MovementComponent.LockState: HEAD_TRACK:3 d2[aa] d3[aa] d1[zz] \nLIFT_TRACK:1 dk[k] \nFACE_IMAGE_TRACK:1 df[f] \n", line);
    }

    /// <summary>
    /// M4-031 D1: the debug record is emitted even when the whole string is empty (0x00641326..0x0064134A has no empty test).
    /// </summary>
    [Fact]
    public void M4_031_D1_AnEmptyStateStillLogsOneRecord()
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.State();
        int logMark = rig.LogLines().Count;
        rig.Robot.Motion.PrintLockState();
        Assert.Equal(new[] { "debug: [Unnamed] MovementComponent.LockState: " }, rig.LogLines().Skip(logMark).ToList());
    }

    /// <summary>
    /// M4-031 D1 (the tree order): keys compare by their bytes, then the shorter first (the memcmp at 0x0064255E and the length
    /// compare at 0x0064256E..0x00642572): "B" (0x42) before "a" (0x61), "a" before "ab", "ab" before "b".
    /// </summary>
    [Fact]
    public void M4_031_D1_KeysAreOrderedByBytesThenLength()
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.State();
        var m = rig.Robot.Motion;
        foreach (var key in new[] { "b", "B", "ab", "a" }) m.LockTracks(4, key, key.ToUpperInvariant() + "!");
        int logMark = rig.LogLines().Count;
        m.PrintLockState();
        Assert.Equal("debug: [Unnamed] MovementComponent.LockState: BODY_TRACK:4 B![B] A![a] AB![ab] B![b] \n", rig.LogLines().Skip(logMark).Single());
    }

    /// <summary>
    /// M4-031 D2/D3: a mask of several tracks puts an entry in each track's tree, and the unlock of a missing key prints the state
    /// after the INFO (0x0063FF76): BODY then HEAD.
    /// </summary>
    [Fact]
    public void M4_031_A10_TheMissingKeyDiagnosticPrintsTheLockState()
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.State();
        var m = rig.Robot.Motion;
        m.LockTracks(5, "w", "dw");                                     // HEAD (1) and BODY (4)
        int logMark = rig.LogLines().Count;
        m.UnlockTracks(1, "nope");
        var lines = rig.LogLines().Skip(logMark).ToList();
        Assert.Equal(new[]
        {
            "info: [Unnamed] MovementComponent.UnlockTracks: Tracks 0x1 are not currently locked by nope",
            "debug: [Unnamed] MovementComponent.LockState: HEAD_TRACK:1 dw[w] \nBODY_TRACK:1 dw[w] \n",
        }, lines);
    }

    /// <summary>
    /// M4-031 D2/D3: bit 7 has no name (EnumToString returns null, 0x007BC2B0) and what the caller does with it is not settled, so a
    /// populated bit-7 track makes the diagnostic throw instead of inventing a label.
    /// </summary>
    [Fact]
    public void M4_031_D3_ABitSevenTrackIsNotInventedAName()
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.State();
        rig.Robot.Motion.LockTracks(0x80, "x");
        Assert.Throws<NotSupportedException>(() => rig.Robot.Motion.PrintLockState());
    }

    // ------------------------------------------------------------------------------- fix round (rows 1..8 of 20261010-m4-movement-leftover-rows.md)

    /// <summary>
    /// Row 8 StopBody (0x00640C70..0x00640E4E): behind the outer gate (here the HEAD direct drive's flag +0xB9 is set) THREE helper calls with flag +0xB8,
    /// mask 4, key +0xBC "DirectDriveWheels" (names Wheels, Arc, TurnInPlace; 0x00640CE2, 0x00640D4A, 0x00640DB2), then DriveWheels{0,0,0,0}. BODY is
    /// held by "X" only, so each call finds the key missing: three INFO lines naming the key, no error.
    /// </summary>
    [Fact]
    public void M4_015_Row8_StopBodyMakesThreeHelperCallsBehindTheOuterGate()
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.State();
        var m = rig.Robot.Motion;
        m.MoveHead(2f, requireCalibration: false);
        m.LockTracks(4, "X");
        int mark = rig.Mark(), logMark = rig.LogLines().Count;
        m.StopBody();
        var sent = rig.SentSince(mark);
        Assert.Single(sent);
        Assert.IsType<DriveWheels>(sent[0]);
        var lines = rig.LogLines().Skip(logMark).ToList();
        Assert.Equal(3, lines.Count(l => l == "info: [Unnamed] MovementComponent.UnlockTracks: Tracks 0x4 are not currently locked by DirectDriveWheels"));
        Assert.DoesNotContain(lines, l => l.StartsWith("error:"));
    }

    /// <summary>Row 8: without any direct-drive flag StopHead and StopBody send only their message; with the BODY flag set StopHead runs its ONE helper call (flag +0xB9, key +0xC0).</summary>
    [Fact]
    public void M4_015_Row8_StopHeadAndStopBodyAreGatedOnAnyFlag()
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.State();
        var m = rig.Robot.Motion;
        m.LockTracks(1, "X");
        m.LockTracks(4, "X");
        int mark = rig.Mark(), logMark = rig.LogLines().Count;
        m.StopHead(); m.StopBody();
        Assert.Equal(2, rig.SentSince(mark).Count);
        Assert.Empty(rig.LogLines().Skip(logMark));
        m.UnlockTracks(4, "X");
        _ = m.DriveWheelsAsync(5f, 5f, confirmWithin: TimeSpan.FromMilliseconds(1), requireCalibration: false);
        mark = rig.Mark(); logMark = rig.LogLines().Count;
        m.StopHead();
        Assert.IsType<MoveHead>(Assert.Single(rig.SentSince(mark)));
        Assert.Single(rig.LogLines().Skip(logMark), l => l == "info: [Unnamed] MovementComponent.UnlockTracks: Tracks 0x1 are not currently locked by DirectDriveHead");
    }

    /// <summary>Row 7: AnimTrackFlagsToString(0) is NO_TRACKS (0x00C20049), 0xFF ALL_TRACKS (0x00C200B3), a mask joins names with '+'; bit 0x80 crashes the original (strlen(NULL), 0x0063071C).</summary>
    [Fact]
    public void M4_031_Row7_AnimTrackFlagsToString()
    {
        Assert.Equal("NO_TRACKS", CozmoMotion.AnimTrackFlagsToString(0));
        Assert.Equal("ALL_TRACKS", CozmoMotion.AnimTrackFlagsToString(0xFF));
        Assert.Equal("HEAD_TRACK+LIFT_TRACK", CozmoMotion.AnimTrackFlagsToString(3));
        Assert.Equal("AUDIO_TRACK", CozmoMotion.AnimTrackFlagsToString(0x40));
        Assert.Throws<NotSupportedException>(() => CozmoMotion.AnimTrackFlagsToString(0x80));
        Assert.Throws<NotSupportedException>(() => CozmoMotion.AnimTrackFlagsToString(0x81));
    }

    /// <summary>Row 3: an action's lock carries its name as the debug name: the sleep child's lift lock prints "MoveLiftToLowDock[tag]".</summary>
    [Fact]
    public void M4_031_Row3_AnActionLockCarriesItsNameAsTheDebugName()
    {
        using var rig = new Rig();
        rig.ToSynced();
        var inPos = RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.HeadInPos | RobotStatusFlag.LiftInPos;
        rig.State(flags: inPos, liftAngle: 0f);
        var parent = new CompoundActionParallel(() => rig.Engine.Timer.SecondsF);
        rig.Robot.Motion.AddGoToSleepLiftChild(parent);
        rig.Engine.Robot!.ActionList.QueueAction(QueueActionPosition.Now, parent, 0);
        rig.Tick();
        int logMark = rig.LogLines().Count;
        rig.Robot.Motion.UnlockTracks(2, "no-such-key");
        Assert.Single(rig.LogLines().Skip(logMark), l => l.StartsWith("debug: [Unnamed] MovementComponent.LockState: LIFT_TRACK:1 MoveLiftToLowDock["));
    }

    /// <summary>Row 4: the status names (table 0x0102F610): 0 Failed .. 6 WaitingToCancelPathAndSetFailure; 7 has no name (visible placeholder).</summary>
    [Theory]
    [InlineData(0, "Failed")]
    [InlineData(1, "ComputingPath")]
    [InlineData(2, "WaitingToBeginPath")]
    [InlineData(3, "FollowingPath")]
    [InlineData(4, "Ready")]
    [InlineData(5, "WaitingToCancelPath")]
    [InlineData(6, "WaitingToCancelPathAndSetFailure")]
    public void M4_026_Row4_PathAbortNamesTheStatus(int status, string name)
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.State();
        var path = new PathComponent(rig.Robot) { DriveToPoseStatus = status, ErasePathHook = _ => { }, SetDriveToPoseStatusHook = _ => { } };
        path.Abort();
        Assert.Contains($"info: [Planner] PathComponent.Abort: Aborting from status '{name}'", rig.LogLines());
        var seven = new PathComponent(rig.Robot) { DriveToPoseStatus = 7, ErasePathHook = _ => { } };
        seven.Abort();
        Assert.Contains(rig.LogLines(), l => l.StartsWith("info: [Planner] PathComponent.Abort: Aborting from status '<MISSING"));
    }

    /// <summary>Row 6: the head ack callback (0x0054D624..0x0054D68E) logs "MoveHeadToAngleAction.MotorActionAcked" with (tag, id) once the command was sent, and again on a repeat.</summary>
    [Fact]
    public void M4_028_Row6_TheHeadAckLogs()
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.State(flags: RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.HeadInPos | RobotStatusFlag.LiftInPos);
        int mark = rig.Mark();
        _ = rig.Robot.Motion.SetHeadAngleAsync(0.3f, timeout: TimeSpan.FromSeconds(5));
        rig.Tick();
        var sent = Assert.IsType<SetHeadAngle>(rig.SentSince(mark).Single(m => m is SetHeadAngle));
        rig.Data(new MotorActionAck { ActionId = (byte)(sent.ActionId + 1) }); rig.Tick();
        Assert.DoesNotContain(rig.LogLines(), l => l.Contains("MoveHeadToAngleAction.MotorActionAcked"));
        rig.Data(new MotorActionAck { ActionId = sent.ActionId }); rig.Tick();
        rig.Data(new MotorActionAck { ActionId = sent.ActionId }); rig.Tick();
        var acks = rig.LogLines().Where(l => l.Contains("MoveHeadToAngleAction.MotorActionAcked")).ToList();
        Assert.Equal(2, acks.Count);
        Assert.All(acks, a => { Assert.StartsWith("info: [Actions] MoveHeadToAngleAction.MotorActionAcked: [", a); Assert.EndsWith($"] ActionID: {sent.ActionId}", a); });
    }
}
