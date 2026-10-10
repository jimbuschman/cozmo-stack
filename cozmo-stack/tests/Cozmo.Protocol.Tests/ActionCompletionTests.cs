using System.Net;
using System.Text;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Transport;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// B-ACTIONS batch 4: the ActionWatcher, RobotCompletedAction and the queue deletion path, from
/// re-analysis/research/20261004-actionlist-extraction.md (rows D1..D9, W1..W17). Every expected value comes from
/// those rows or the shipped Unity/CLAD types, never from the code under test. The records stay
/// IMPLEMENTATION_GAP (M7-020 and the queue/watcher rows).
/// </summary>
[Collection("SteppedBehavior missing-report statics")]
public class ActionCompletionTests
{
    // ------------------------------------------------------------------ rig

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
        public static readonly IPAddress RobotIp = IPAddress.Parse("172.31.1.1");
        public static readonly IPEndPoint RobotEp = new(RobotIp, 5551);
        private uint _ts = 100;

        public Rig() => Robot = CozmoRobot.CreateForTest(Port, () => NowNs, new CozmoEngineOptions { BlockPoolPath = "", ResourcesPath = null });

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

        public void Calibrate()
        {
            Data(new MotorCalibration { MotorID = MotorID.MOTOR_HEAD, CalibStarted = true });
            Data(new MotorCalibration { MotorID = MotorID.MOTOR_LIFT, CalibStarted = true });
            Data(new MotorCalibration { MotorID = MotorID.MOTOR_HEAD, CalibStarted = false });
            Data(new MotorCalibration { MotorID = MotorID.MOTOR_LIFT, CalibStarted = false });
        }

        public RobotState MakeState(RobotStatusFlag flags = RobotStatusFlag.IsBodyAccMode, float head = 0) => new()
        {
            Timestamp = _ts += 33, PoseFrameId = 0, PoseOriginId = 1, Pose = new RobotPose { X = 0, Y = 0 },
            HeadAngle = head, LiftAngle = 0, BatteryVoltage = 4.0f, Status = (uint)flags,
            Accel = new AccelData { Z = 9800 }, Gyro = new GyroData(), CliffDataRaw = new ushort[] { 500, 500, 500, 500 },
        };

        public void State(RobotStatusFlag flags = RobotStatusFlag.IsBodyAccMode, float head = 0) { Data(MakeState(flags, head)); Tick(); }

        public void Dispose() => Robot.Dispose();
    }

    /// <summary>A concrete runner with a settable watcher, so the queue/watcher surface can be driven directly.</summary>
    private sealed class WatchedRunner : ActionRunner
    {
        public WatchedRunner(int type) : base(type, 0) { }
        public ActionWatcher? W { get; set; }
        internal override ActionWatcher? Watcher { get => W; set => W = value; }
        public override uint CheckIfDone() => State;
        public override bool CanInterrupt() => true;
    }

    // ------------------------------------------------------------------ the live head action

    /// <summary>
    /// D1..D7/W10: a head action driven through the live ActionList (queue at NOW, tick to completion) enqueues a
    /// RobotCompletedAction with the action's own tag, its type 0x12 (M4-003: MoveHeadToAngleAction) and the full
    /// result. The in-position action sends nothing and succeeds on the first tick (M4-016 MA15).
    /// </summary>
    [Fact]
    public async Task M7_020_AHeadActionThroughTheLiveListProducesARobotCompletedAction()
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.Calibrate();
        rig.State(flags: RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.HeadInPos, head: 0.3f);

        var list = rig.Engine.Robot!.ActionList;
        var records = new List<RobotCompletedAction>();
        list.RegisterActionEndedCallback(records.Add);

        var pending = rig.Robot.Motion.SetHeadAngleAsync(0.3f + 0.034f);     // within tolerance + 1e-5: in position
        var current = list.GetCurrentAction();
        Assert.NotNull(current);
        uint tag = current!.Tag;

        rig.Tick();

        Assert.Single(records);
        Assert.Equal(tag, records[0].Tag);
        Assert.Equal(0x12, records[0].ActionType);                          // M4-003: head type 0x12
        Assert.Equal(EngineActionResult.Success, records[0].Result);
        var outcome = await pending.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(outcome.Ok, outcome.Detail);
    }

    /// <summary>
    /// D2/D6: the game send is gated by <c>HasExternalInterface</c>. With it false (production) nothing is raised;
    /// with it true the watcher raises the record and reports the missing engine-to-game sink once.
    /// </summary>
    [Fact]
    public async Task M7_020_TheGameSendIsGatedByHasExternalInterface()
    {
        using var rig = new Rig();
        rig.ToSynced();
        rig.Calibrate();
        rig.State(flags: RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.HeadInPos, head: 0.3f);

        var list = rig.Engine.Robot!.ActionList;
        var broadcasts = new List<RobotCompletedAction>();
        list.Watcher.RobotCompletedActionBroadcast += broadcasts.Add;
        Assert.False(list.Watcher.HasExternalInterface);                    // D2: false in production

        var first = rig.Robot.Motion.SetHeadAngleAsync(0.3f + 0.034f);
        rig.Tick();
        await first.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Empty(broadcasts);                                           // gate off: no D6 broadcast

        var reported = new List<string>();
        void On(string s) => reported.Add(s);
        SteppedBehavior.ResetMissingForTests();
        SteppedBehavior.MissingReported += On;
        try
        {
            list.GameSend = _ => { };                                       // D6 sink: turns the gate on
            Assert.True(list.Watcher.HasExternalInterface);
            var second = rig.Robot.Motion.SetHeadAngleAsync(0.3f + 0.034f);
            rig.Tick();
            await second.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Single(broadcasts);
            Assert.Contains(reported, r => r.Contains("RobotCompletedAction") && r.Contains("engine-to-game"));
        }
        finally { SteppedBehavior.MissingReported -= On; }
    }

    // ------------------------------------------------------------------ W1/W2

    /// <summary>
    /// W1/W2: GetSubActionResults clears the output and inserts the unique descendant full results in set order;
    /// the root's own result is excluded. A tag the node map does not hold leaves the output untouched.
    /// </summary>
    [Fact]
    public void M7_020_GetSubActionResultsExcludesTheRootAndReturnsUniqueDescendants()
    {
        var c1 = new WatchedRunner(0) { State = 0x03000018 };   // TIMEOUT
        var c2 = new WatchedRunner(0) { State = 0x03000018 };   // duplicate -> one set entry
        var c3 = new WatchedRunner(0) { State = 0x03000019 };   // TRACKS_LOCKED
        var compound = new CompoundActionParallel(null, c1, c2, c3);

        var w = new ActionWatcher();
        w.ParentActionUpdating(compound);
        w.ActionStartUpdating(compound);
        w.ActionStartUpdating(c1); w.ActionEnding(c1); w.ActionEndUpdating();
        w.ActionStartUpdating(c2); w.ActionEnding(c2); w.ActionEndUpdating();
        w.ActionStartUpdating(c3); w.ActionEnding(c3); w.ActionEndUpdating();

        var subs = new List<uint>();
        w.GetSubActionResults(compound.Tag, subs);

        Assert.Equal(new[] { 0x03000018u, 0x03000019u }, subs);            // W1: unique, set order, root excluded
        Assert.DoesNotContain(compound.State, subs);

        var untouched = new List<uint> { 0xDEADBEEFu };
        w.GetSubActionResults(0x00FFFFFFu, untouched);                     // W1: missing tag leaves it untouched
        Assert.Equal(new[] { 0xDEADBEEFu }, untouched);
    }

    /// <summary>
    /// W7/W8/W9 (Fix 1): QueueAction reaches the watcher for every runner through the robot's ActionList, and a
    /// compound propagates it to its children, so a compound queued on the live list emits its children's records
    /// and then its own. W10/D5: the children's destruction events precede the parent's.
    /// </summary>
    [Fact]
    public void M7_020_ACompoundQueuedOnTheLiveListPropagatesTheWatcherToItsChildren()
    {
        using var robot = CozmoRobot.CreateOffline();
        var engineRobot = robot.Engine.Robots.Get(CozmoEngine.RobotId);
        Assert.NotNull(engineRobot);

        var a = new WatchedRunner(0) { State = EngineActionResult.Success };
        var b = new WatchedRunner(0) { State = EngineActionResult.Success };
        var compound = new CompoundActionParallel(() => robot.Engine.Timer.SecondsF, a, b);
        var records = new List<RobotCompletedAction>();
        engineRobot!.ActionList.RegisterActionEndedCallback(records.Add);

        engineRobot.ActionList.QueueAction(QueueActionPosition.Now, compound, 0);
        Assert.Same(engineRobot.ActionList.Watcher, a.Watcher);            // W7/W8: the compound propagated it
        Assert.Same(engineRobot.ActionList.Watcher, b.Watcher);

        robot.Engine.Tick();

        int ai = records.FindIndex(r => r.Tag == a.Tag);
        int bi = records.FindIndex(r => r.Tag == b.Tag);
        int ci = records.FindIndex(r => r.Tag == compound.Tag);
        Assert.True(ai >= 0 && bi >= 0 && ci >= 0);
        Assert.True(ai < ci && bi < ci);                                  // D5/W10: children before the parent base event
    }

    // ------------------------------------------------------------------ W11/W12

    /// <summary>W11/W12: callbacks drain in signed ascending handle order, and an unregister removes the handle.</summary>
    [Fact]
    public void M7_020_TheCallbackDrainIsSignedAscendingHandleAndUnregisterRemoves()
    {
        var w = new ActionWatcher();
        var order = new List<int>();
        int h1 = w.RegisterCallback(_ => order.Add(1));
        int h2 = w.RegisterCallback(_ => order.Add(2));
        int h3 = w.RegisterCallback(_ => order.Add(3));
        Assert.Equal(1, h1);                                                // W12: handles increment from 1
        Assert.Equal(2, h2);
        Assert.Equal(3, h3);

        w.ActionEnding(new WatchedRunner(0) { W = w, State = 0 });
        w.Update();
        Assert.Equal(new[] { 1, 2, 3 }, order);                             // W11: signed ascending handle

        Assert.True(w.UnregisterCallback(h2));                             // W12: erase the matching handle
        Assert.False(w.UnregisterCallback(h2));
        order.Clear();
        w.ActionEnding(new WatchedRunner(0) { W = w, State = 0 });
        w.Update();
        Assert.Equal(new[] { 1, 3 }, order);
    }

    /// <summary>W11: a record appended during the drain is delivered in the same Update.</summary>
    [Fact]
    public void M7_020_ARecordAppendedDuringTheDrainIsDeliveredInTheSameTick()
    {
        var w = new ActionWatcher();
        var first = new WatchedRunner(0) { W = w, State = 0 };
        var second = new WatchedRunner(0) { W = w, State = 0 };
        var received = new List<uint>();
        w.RegisterCallback(r =>
        {
            received.Add(r.Tag);
            if (r.Tag == first.Tag) w.ActionEnding(second);                 // append during the drain
        });

        w.ActionEnding(first);
        w.Update();

        Assert.Contains(first.Tag, received);
        Assert.Contains(second.Tag, received);                             // W11: same tick
    }

    // ------------------------------------------------------------------ W13/W15/W16/W17: the Mood live path

    private static MoodModel MoodWithAbortEvent()
    {
        var model = new MoodModel();
        model.AddDecayGraph(new DecayGraph("Confident", new[] { (0.0, 1.0) }));
        model.AddEvent(new EmotionEvent("DrivingActionFailedWithAbort",
            new[] { new EmotionAffector(EmotionType.Confident, -0.2) }));
        model.AddActionResultEvent("DRIVE_TO_POSE", "ABORT", "DrivingActionFailedWithAbort");
        return model;
    }

    private static ActionList MoodList(MoodState mood)
    {
        var list = new ActionList();
        // The same mapping FreeplayStack.Create registers (W13/W14/W15): record -> enum names -> HandleActionEnded.
        list.RegisterActionEndedCallback(r =>
        {
            string? type = RobotActionType.NameOf(r.ActionType);
            string? category = ActionResultCategory.NameOf((int)(r.Result >> 24));
            if (type is null || category is null) return;
            mood.HandleActionEnded(type, category, r.Tag.ToString(), 0);
        });
        return list;
    }

    /// <summary>
    /// W13..W16 on the live path: a completed action (type DRIVE_TO_POSE = 12, category ABORT = result 0x03000000)
    /// reaches MoodState.HandleActionEnded and triggers the mapped event.
    /// </summary>
    [Fact]
    public void M7_020_HandleActionEndedIsInvokedOnTheLivePath()
    {
        var mood = new MoodState(MoodWithAbortEvent());
        var list = MoodList(mood);
        var runner = new WatchedRunner(12) { W = list.Watcher, State = 0x03000000 };   // DRIVE_TO_POSE / ABORT
        list.QueueAction(QueueActionPosition.AtEnd, runner);

        list.Update();

        Assert.Equal(-0.2, mood[EmotionType.Confident], 6);
    }

    /// <summary>W15: an action id in the one-shot disabled set at +0x140 produces no event.</summary>
    [Fact]
    public void M7_020_ADisabledActionIdProducesNoEvent()
    {
        var mood = new MoodState(MoodWithAbortEvent());
        var list = MoodList(mood);
        var runner = new WatchedRunner(12) { W = list.Watcher, State = 0x03000000 };
        mood.SetMoodEventOnCompletionEnabled(runner.Tag.ToString(), false);           // +0x140 set
        list.QueueAction(QueueActionPosition.AtEnd, runner);

        list.Update();

        Assert.Equal(0.0, mood[EmotionType.Confident], 6);
    }

    /// <summary>W15: an out-of-range type/category name produces no event (the mapping is skipped).</summary>
    [Fact]
    public void M7_020_AnOutOfRangeTypeProducesNoEvent()
    {
        var mood = new MoodState(MoodWithAbortEvent());
        var list = MoodList(mood);
        var runner = new WatchedRunner(9999) { W = list.Watcher, State = 0x03000000 };  // name is null
        list.QueueAction(QueueActionPosition.AtEnd, runner);

        list.Update();

        Assert.Equal(0.0, mood[EmotionType.Confident], 6);
    }

    // ------------------------------------------------------------------ the shipped tables

    /// <summary>
    /// The enum tables exactly (20261004-actionlist-extraction.md, "Enum tables"): RobotActionType index = value + 2
    /// with 54 entries; ActionResultCategory 5 entries; the inverse returns null out of range and the forward map is
    /// the inverse with the -2 / 0 miss fallbacks.
    /// </summary>
    [Fact]
    public void M7_020_TheEnumTablesAreTheShippedTables()
    {
        var actionTypes = new (int Value, string Name)[]
        {
            (-2, "COMPOUND"), (-1, "UNKNOWN"), (0, "ALIGN_WITH_OBJECT"), (1, "ASCEND_OR_DESCEND_RAMP"),
            (2, "CALIBRATE_MOTORS"), (3, "CROSS_BRIDGE"), (4, "DEVICE_AUDIO"), (5, "DISPLAY_FACE_IMAGE"),
            (6, "DISPLAY_PROCEDURAL_FACE"), (7, "DRIVE_OFF_CHARGER_CONTACTS"), (8, "DRIVE_STRAIGHT"),
            (9, "DRIVE_TO_FLIP_BLOCK_POSE"), (10, "DRIVE_TO_OBJECT"), (11, "DRIVE_PATH"), (12, "DRIVE_TO_POSE"),
            (13, "DRIVE_TO_PLACE_CARRIED_OBJECT"), (14, "FACE_PLANT"), (15, "FLIP_BLOCK"), (16, "HANG"),
            (17, "MOUNT_CHARGER"), (18, "MOVE_HEAD_TO_ANGLE"), (19, "MOVE_LIFT_TO_HEIGHT"), (20, "PAN_AND_TILT"),
            (21, "PICK_AND_PLACE_INCOMPLETE"), (22, "PICKUP_OBJECT_LOW"), (23, "PICKUP_OBJECT_HIGH"),
            (24, "PLACE_OBJECT_LOW"), (25, "PLACE_OBJECT_HIGH"), (26, "PLAY_ANIMATION"),
            (27, "PLAY_ANIMATION_DRONE_MODE_CLIFF_EVENT"), (28, "PLAY_CUBE_ANIMATION"), (29, "POP_A_WHEELIE"),
            (30, "READ_TOOL_CODE"), (31, "ROLL_OBJECT_LOW"), (32, "SAY_TEXT"), (33, "SEARCH_FOR_NEARBY_OBJECT"),
            (34, "TRACK_OBJECT"), (35, "TRACK_FACE"), (36, "TRACK_GROUND_POINT"), (37, "TRACK_MOTION"),
            (38, "TRACK_PET_FACE"), (39, "TRAVERSE_OBJECT"), (40, "TURN_IN_PLACE"), (41, "TURN_TOWARDS_FACE"),
            (42, "TURN_TOWARDS_IMAGE_POINT"), (43, "TURN_TOWARDS_LAST_FACE_POSE"), (44, "TURN_TOWARDS_OBJECT"),
            (45, "TURN_TOWARDS_POSE"), (46, "VISUALLY_VERIFY_OBJECT"), (47, "VISUALLY_VERIFY_FACE"),
            (48, "VISUALLY_VERIFY_NO_OBJECT_AT_POSE"), (49, "WAIT"), (50, "WAIT_FOR_IMAGES"), (51, "WAIT_FOR_LAMBDA"),
        };
        Assert.Equal(54, actionTypes.Length);
        foreach (var (value, name) in actionTypes)
        {
            Assert.Equal(name, RobotActionType.NameOf(value));
            Assert.Equal(value, RobotActionType.FromString(name));
        }
        Assert.Null(RobotActionType.NameOf(-3));
        Assert.Null(RobotActionType.NameOf(52));
        Assert.Equal(-2, RobotActionType.FromString("NOT_A_TYPE"));         // 0x0075A448 miss: COMPOUND

        var categories = new (int Value, string Name)[]
        {
            (0, "SUCCESS"), (1, "RUNNING"), (2, "CANCELLED"), (3, "ABORT"), (4, "RETRY"),
        };
        Assert.Equal(5, categories.Length);
        foreach (var (value, name) in categories)
        {
            Assert.Equal(name, ActionResultCategory.NameOf(value));
            Assert.Equal(value, ActionResultCategory.FromString(name));
        }
        Assert.Null(ActionResultCategory.NameOf(-1));
        Assert.Null(ActionResultCategory.NameOf(5));
        Assert.Equal(0, ActionResultCategory.FromString("NOT_A_CATEGORY"));  // 0x00757E40 miss: SUCCESS
    }

    // ------------------------------------------------------------------ D8/D9: the wire body

    /// <summary>
    /// D8/D9: the packed body is tag4 LE, type4 LE, result4 LE, one-byte count, every sub-result as 4 LE bytes,
    /// then the 4-byte +0x1C union cache; the outer MessageEngineToGame tag is uint16 0x005A. The concrete
    /// ActionCompletedUnion variant is UNKNOWN (report U6): only the cache width is asserted here, not a shipped
    /// variant payload.
    /// </summary>
    [Fact]
    public void M7_020_TheWireBodyIsTheD8D9FieldOrderWithTheFourByteUnionCache()
    {
        var r = new RobotCompletedAction(0x11223344u, 12, 0x03000000u, new uint[] { 0x03000018u, 0x03000019u }, 0xDEADBEEFu);
        var body = r.PackBody();

        Assert.Equal(4 + 4 + 4 + 1 + 2 * 4 + 4, body.Length);
        Assert.Equal(new byte[] { 0x44, 0x33, 0x22, 0x11 }, body[..4]);            // tag4 LE
        Assert.Equal(new byte[] { 0x0C, 0x00, 0x00, 0x00 }, body[4..8]);            // type4 LE
        Assert.Equal(new byte[] { 0x00, 0x00, 0x00, 0x03 }, body[8..12]);           // result4 LE
        Assert.Equal(2, body[12]);                                                  // one-byte count
        Assert.Equal(new byte[] { 0x18, 0x00, 0x00, 0x03 }, body[13..17]);          // first sub-result
        Assert.Equal(new byte[] { 0x19, 0x00, 0x00, 0x03 }, body[17..21]);          // second sub-result
        Assert.Equal(new byte[] { 0xEF, 0xBE, 0xAD, 0xDE }, body[21..25]);          // union cache, 4 LE bytes

        var union = r.ToUnionBytes();
        Assert.Equal(new byte[] { 0x5A, 0x00 }, union[..2]);                        // D9: outer tag 90
        Assert.Equal(body, union[2..]);
    }

    /// <summary>D8: the one-byte count is narrowed, not clamped: a 256-element list still packs every element.</summary>
    [Fact]
    public void M7_020_TheSubActionResultCountIsNarrowedNotClamped()
    {
        var subs = new uint[256];
        var r = new RobotCompletedAction(1, 0, 0, subs, 0);
        var body = r.PackBody();
        Assert.Equal(0, body[12]);                                                  // (byte)256 == 0
        Assert.Equal(4 + 4 + 4 + 1 + 256 * 4 + 4, body.Length);                     // all 256 elements packed
    }
}