using Cozmo.Protocol;

namespace Cozmo.Robot;

// fidelity: M4-009
public sealed record CubeTimestampEvent(Cube Cube, uint Timestamp);
public sealed record CubeUpAxisEvent(uint Timestamp, int ObjectID, UpAxis UpAxis);

/// <summary>What is known about one of Cozmo's light cubes.</summary>
public sealed class Cube
{
    /// <summary>The cube's permanent identifier, reported when it advertises and when it connects.</summary>
    public uint FactoryId { get; internal set; }
    internal Cube SnapshotForApp()
    {
        var copy=(Cube)MemberwiseClone();
        if(LastTap is {} tap)copy.LastTap=new ObjectTapped{Timestamp=tap.Timestamp,ObjectID=tap.ObjectID,
            NumTaps=tap.NumTaps,TapTime=tap.TapTime,TapNeg=tap.TapNeg,TapPos=tap.TapPos};
        return copy;
    }
    /// <summary>The id the robot uses for this cube once connected. Null until a connection is reported.</summary>
    public uint? ObjectId { get; internal set; }
    /// <summary>Which of the three cubes this is, as the robot classifies it.</summary>
    public ObjectType Type { get; internal set; } = ObjectType.UnknownObject;
    /// <summary>Signal strength from the last advertisement, in the robot's own units.</summary>
    public sbyte? Rssi { get; internal set; }
    /// <summary>True while the robot reports a live connection to this cube.</summary>
    public bool Connected { get; internal set; }

    /// <summary>When this cube was last heard from, in local time.</summary>
    public DateTime LastSeenUtc { get; internal set; } = DateTime.UtcNow;
    /// <summary>How many advertisements have been seen from this cube.</summary>
    public int Advertisements { get; internal set; }

    /// <summary>
    /// The cube's battery level as the robot reports it. The scale is not established, so the raw byte is
    /// kept rather than converted to a percentage or a voltage.
    /// </summary>
    public byte? BatteryLevelRaw { get; internal set; }

    /// <summary>
    /// The cube's battery in volts: the raw byte is hundredths of a volt.
    /// <c>RobotToEngineImplMessaging::HandleObjectPowerLevel</c> 0x00537130 converts the byte at the
    /// message's +8 with <c>vcvt.f32.u32</c> and divides by 100 (0x00537162..0x00537172) before it reports
    /// anything about it.
    /// </summary>
    public float? BatteryVolts => BatteryLevelRaw is { } b ? b / 100f : null;

    /// <summary>
    /// The percentage the engine reports for that voltage: 100 at or above 1.5 V, zero at or below 1.0 V,
    /// and <c>(V - 1) * 200</c> between them - the compare against 1.5 at 0x00537176, the compare against 1
    /// at 0x00537184, and the <c>(V - 1) * 100</c> doubled at 0x00537196..0x0053719E.
    /// </summary>
    public float? BatteryPercent => BatteryVolts is { } v ? BatteryPercentOf(v) : null;

    // fidelity: M4-008
    /// <summary>
    /// U4/U5 (0x00537156..0x005371A8), in the engine's operations: the percentage starts at 100 (the divisor's register, 0x00537162);
    /// at 1.5 V or more it stays 100 (bge, 0x0053717E); at 1.0 V or less - or unordered, which the ble also takes - it is 0 (0x005371A4);
    /// otherwise <c>(V + -1) * 100</c>, then the result added to itself, which is not collapsed to <c>* 200</c> in the rounding order.
    /// </summary>
    internal static float BatteryPercentOf(float volts)
    {
        if (volts >= 1.5f) return 100f;
        if (!(volts > 1.0f)) return 0f;
        float x = (volts + -1f) * 100f;
        return x + x;
    }
    /// <summary>Packets the robot says it missed from this cube.</summary>
    public uint? MissedPackets { get; internal set; }

    /// <summary>Which way up the cube is, from the last report.</summary>
    public UpAxis? UpAxis { get; internal set; }
    /// <summary>The cube's accelerometer from its last movement report.</summary>
    public Vector3? Accel { get; internal set; }
    /// <summary>True between a moved and a stopped-moving report.</summary>
    public bool Moving { get; internal set; }
    /// <summary>How many ObjectTapped broadcasts this cube has had (taps that passed the tap filter, M4-023).</summary>
    public int Taps { get; internal set; }
    /// <summary>When it was last tapped.</summary>
    public DateTime? LastTapUtc { get; internal set; }
    /// <summary>The last broadcast tap: {timestamp, numTaps, tapTime, tapNeg, tapPos} (CD10e).</summary>
    public ObjectTapped? LastTap { get; internal set; }
    /// <summary>The robot timestamp of the last SetIsMoving (CD10a, CD10b).</summary>
    public uint MovingChangedAt { get; internal set; }
    // fidelity: M4-009 — each event's message timestamp, including repeated movement reports.
    public uint LastBroadcastTimestamp { get; internal set; }

    public override string ToString() =>
        $"Cube 0x{FactoryId:x8} {Type}{(ObjectId is { } o ? $" id={o}" : "")} " +
        $"{(Connected ? "connected" : "seen")}{(Rssi is { } r ? $" rssi={r}" : "")}" +
        $"{(BatteryLevelRaw is { } b ? $" batt={b}" : "")}{(Taps > 0 ? $" taps={Taps}" : "")}";
}

/// <summary>
/// Cozmo's light cubes.
///
/// The robot does the radio work: turning discovery on makes it advertise-scan and report every cube it
/// hears as <see cref="ObjectAvailable"/>, and a connection shows up as <see cref="ObjectConnectionState"/>.
/// Everything here is driven by those reports rather than by assuming a command worked.
///
/// This covers discovery, connection state and basic telemetry only. Cube lights, object pose and anything
/// that needs the vision pipeline are out of scope.
///
/// A cube only connects when the engine asks the robot for it, with <see cref="SetPropSlot"/>. Deciding which
/// cube and sending that is <see cref="Connections"/> - the engine's automatic block pool and its five
/// active-object slots - which does nothing until <see cref="EnableAutoBlockPool"/> is called, as the official
/// app does once it is connected.
///
/// The engine's side of this is four handlers in <c>RobotToEngineImplMessaging</c>, and the field offsets
/// they read match this stack's message layouts exactly:
///
/// <list type="bullet">
/// <item><c>HandleActiveObjectAvailable</c> 0x0053391C reads the factory id at +0, the object type at +4
/// and the RSSI at +8, takes the report only for a light cube or the charger, and writes the type, the
/// RSSI and the current timestamp into its table of active objects.</item>
/// <item><c>HandleActiveObjectConnectionState</c> 0x00533B3C reads the object id at +0, the factory id at
/// +4, the type at +8 and the connected flag at +0xC, drops anything whose object id is above 4, and on a
/// connection calls <c>BlockWorld::AddConnectedActiveObject(id, factoryId, type)</c> - on a disconnection
/// <c>RemoveConnectedActiveObject(id)</c> and <c>Robot::HandleDisconnectedFromObject</c>.</item>
/// <item><c>HandleActiveObjectMoved</c> 0x00533E30 reads the timestamp at +0, the id at +4, the three
/// accelerometer floats at +8, +0xC and +0x10 and the axis at +0x14, and looks the object up by that id
/// (<c>BlockWorld::GetConnectedActiveObjectByActiveIdHelper</c>) - which is what keying telemetry on the
/// object id here means.</item>
/// <item><c>HandleObjectPowerLevel</c> 0x00537130 reads the id at +0, the missed packets at +4 and the
/// battery byte at +8, in hundredths of a volt.</item>
/// </list>
/// </summary>
public sealed class CozmoCubes
{
    private readonly CozmoRobot _robot;
    private readonly object _gate = new();
    private readonly Dictionary<uint, Cube> _byFactoryId = new();
    private readonly Dictionary<uint, Cube> _byObjectId = new();
    // fidelity: M4-008
    // A7 (0x00537206..0x00537240): the two std::map<u32,u32> at this+0x138 and this+0x144 of RobotToEngineImplMessaging (the object the handler runs on, built with the
    // Robot): the time of the last DAS report and the missed-packets count at it, per activeID. They persist and are reset where the Robot is rebuilt.
    private readonly Dictionary<uint, uint> _lastReportTime = new();
    private readonly Dictionary<uint, uint> _lastReportMissed = new();

    /// <summary>
    /// A15 (0x005373C8..0x005373E4): the game-bound ObjectPowerLevel {objectID, missedPackets, cv} that Robot::Broadcast sends; objectID is the connected object's game ObjectID (A13).
    /// </summary>
    public event Action<ObjectPowerLevel>? PowerLevelBroadcast;

    /// <summary>
    /// The object types the engine will take an advertisement for.
    /// <c>HandleActiveObjectAvailable</c> 0x0053391C asks <c>IsValidLightCube</c> 0x007D1D08 and then
    /// <c>IsCharger</c> 0x007D1D50, and returns without recording anything when both say no. The cube test
    /// is a jump table over the object type whose only true entries are 1, 2 and 3 - the three light cubes,
    /// not the ghost at 4 - and the charger test is true for 13.
    /// </summary>
    public static bool IsTrackedActiveObject(ObjectType type) => IsLightCube(type) || type == ObjectType.Charger_Basic;

    /// <summary>The three light cube types, which is what <c>IsValidLightCube</c> 0x007D1D08 accepts.</summary>
    public static bool IsLightCube(ObjectType type) =>
        type is ObjectType.Block_LIGHTCUBE1 or ObjectType.Block_LIGHTCUBE2 or ObjectType.Block_LIGHTCUBE3;

    // fidelity: M4-009
    /// <summary>
    /// The largest slot a connection report from the robot may carry, the last of the engine's
    /// <c>MAX_NUM_ACTIVE_OBJECTS</c> = 5 radio slots. <c>HandleActiveObjectConnectionState</c> reads the slot first
    /// and ignores a report above 4 (S1, LC8a: <c>cmp r7, #4; bhi</c> at 0x00533B58). The id this stack keys cube
    /// telemetry on is that slot, the engine's activeID (S2: vbase+0x40).
    /// </summary>
    public const uint MaxActiveObjectSlot = 4;

    // fidelity: M4-025
    /// <summary>
    /// Robot+0x490: <c>Robot::BroadcastAvailableObjects</c> 0x00517DD8 is its only writer, it is 0 at the ctor
    /// (0x005101DE) and has no caller in the shipped engine, so the engine never broadcasts ObjectAvailable or
    /// ObjectUnavailable to the game (D1..D4; the two readers are 0x0053398E and 0x0051419A). This stack has no
    /// game-facing broadcast for either message either; <see cref="CubeDiscovered"/> is this stack's own
    /// advertisement notification, not the engine's gated broadcast.
    /// </summary>
    internal const bool BroadcastsAvailableObjects = false;

    internal CozmoCubes(CozmoRobot robot)
    {
        _robot = robot;
        // fidelity: M4-010
        // The connection path's clock is BaseStationTimer::GetCurrentTimeInSeconds (CD3, CD4, CD8).
        Seconds = () => _robot.Engine.Timer.SecondsF;
        Connections = new CubeConnections(m => _robot.SendMessage(m, reliable: true, flush: true),
                                          () => Seconds());
        Connections.SourceLog += (level, channel, key, text) =>
        {
            if (key != "BlockFilter.Load.ParseError") _robot.Engine.EmitSourceLog(level, channel, key, text);
        }; // ParseError is delivered through the raw-byte sink; all other string consumers still receive it.
        Connections.SourceLogBytes += (level, channel, key, utf8) => _robot.Engine.EmitSourceLog(level, channel, key, utf8);
        Connections.StackLog += _robot.Engine.EmitStackLog;
        Connections.BlockPoolData += m => _robot.Engine.DeliverBlockPoolData(m);
    }

    /// <summary>The clock the connection path reads, in seconds (the engine clock); replaceable so tests can drive it.</summary>
    internal Func<float> Seconds { get; set; }

    /// <summary>
    /// The engine's connection path: which cube goes in which of the five slots, and the SetPropSlot that asks
    /// the robot to connect it. Fed from <see cref="Handle"/>; see <see cref="CubeConnections"/>.
    /// </summary>
    public CubeConnections Connections { get; }

    /// <summary>
    /// Turns the engine's automatic block pool on or off - what the game's <c>BlockPoolEnabledMessage</c> does.
    /// With it on, the closest cube of each of the three types is put in a slot and connected. The official
    /// app turns it on with a discovery time of 0 (<c>BlockPoolTracker.EnableAutoBlockPool</c>).
    /// </summary>
    public void EnableAutoBlockPool(bool enabled = true, float discoveryTimeSeconds = 0f)
        => Connections.EnableAutoBlockPool(enabled, discoveryTimeSeconds);

    /// <summary>Raised the first time a cube is heard from.</summary>
    public event Action<Cube>? CubeDiscovered;
    /// <summary>Raised when a cube connects or disconnects.</summary>
    public event Action<Cube>? ConnectionChanged;
    /// <summary>Raised when a cube is tapped.</summary>
    public event Action<Cube>? CubeTapped;
    /// <summary>Raised when a cube starts or stops moving.</summary>
    public event Action<Cube>? CubeMoved;
    // fidelity: M4-009 — immutable message timestamps beside the existing cube notices.
    public event Action<CubeTimestampEvent>? MovementBroadcast;
    public event Action<CubeTimestampEvent>? TapBroadcast;
    public event Action<CubeUpAxisEvent>? UpAxisBroadcast;

    /// <summary>True once discovery has been turned on and not turned off again.</summary>
    public bool DiscoveryEnabled { get; private set; }

    /// <summary>Every cube heard from since discovery started, newest advertisement first.</summary>
    public IReadOnlyList<Cube> DiscoveredCubes
    {
        get
        {
            lock (_gate)
                return _byFactoryId.Values.Where(c => IsLightCube(c.Type))
                                          .OrderByDescending(c => c.LastSeenUtc).ToArray();
        }
    }

    /// <summary>The cubes the robot currently reports as connected.</summary>
    public IReadOnlyList<Cube> ConnectedCubes
    {
        get { lock (_gate) return _byFactoryId.Values.Where(c => c.Connected && IsLightCube(c.Type)).ToArray(); }
    }

    /// <summary>
    /// The charger, if the robot has heard it advertise. The engine keeps it in the same table as the
    /// cubes - one <c>unordered_map&lt;u32, ActiveObjectInfo&gt;</c> at Robot+0x47C, written by
    /// <c>HandleActiveObjectAvailable</c> with the object type, the RSSI and the timestamp it was heard at
    /// (0x00533984..0x0053398A) - so it is tracked here and kept out of the cube lists.
    /// </summary>
    public Cube? Charger
    {
        get { lock (_gate) return _byFactoryId.Values.FirstOrDefault(c => c.Type == ObjectType.Charger_Basic); }
    }

    /// <summary>Looks up a cube by its permanent id.</summary>
    public Cube? ByFactoryId(uint factoryId) { lock (_gate) return _byFactoryId.GetValueOrDefault(factoryId); }
    /// <summary>Looks up a cube by the id the robot assigned it on connection.</summary>
    public Cube? ByObjectId(uint objectId) { lock (_gate) return _byObjectId.GetValueOrDefault(objectId); }

    /// <summary>
    /// Sends SetAccessoryDiscovery. The official engine never sends this message - its only references in
    /// libcozmoEngine.so are its own serializers - and the robot reports advertisements without it, so nothing
    /// on the connection path uses it. What the firmware does with it is not established from source.
    /// </summary>
    public void SetDiscovery(bool enable)
    {
        _robot.SendMessage(new SetAccessoryDiscovery { Enable = enable }, flush: true);
        DiscoveryEnabled = enable;
    }

    /// <summary>
    /// Turns discovery on and waits until at least this many distinct cubes have been heard, or the time
    /// runs out. Returns whatever was found either way; discovery is left on for the caller to turn off.
    /// </summary>
    public async Task<IReadOnlyList<Cube>> DiscoverAsync(int wantAtLeast = 1, TimeSpan? timeout = null)
    {
        SetDiscovery(true);
        var found = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        void watch(Cube _)
        {
            lock (_gate)
            {
                if (_byFactoryId.Count >= wantAtLeast) found.TrySetResult(true);
            }
        }
        CubeDiscovered += watch;
        try
        {
            lock (_gate)
            {
                if (_byFactoryId.Count >= wantAtLeast) return DiscoveredCubes;
            }
            await Task.WhenAny(found.Task, Task.Delay(timeout ?? TimeSpan.FromSeconds(10)));
            return DiscoveredCubes;
        }
        finally { CubeDiscovered -= watch; }
    }

    /// <summary>Waits for the robot to report a cube connecting, or gives up.</summary>
    public async Task<Cube?> WaitForConnectionAsync(TimeSpan timeout)
    {
        var tcs = new TaskCompletionSource<Cube>(TaskCreationOptions.RunContinuationsAsynchronously);
        void watch(Cube c) { if (c.Connected) tcs.TrySetResult(c); }
        ConnectionChanged += watch;
        try
        {
            var already = ConnectedCubes;
            if (already.Count > 0) return already[0];
            return await Task.WhenAny(tcs.Task, Task.Delay(timeout)) == tcs.Task ? tcs.Task.Result : null;
        }
        finally { ConnectionChanged -= watch; }
    }

    // fidelity: M1-025, M1-015
    /// <summary>
    /// Back to the state right after construction, for a removed robot (CB33, CC26, CC27): no cube or charger known,
    /// discovery off, and the connection path as built (<see cref="CubeConnections.ResetToConstructed"/>). The clock
    /// and subscribers are kept.
    /// </summary>
    internal void ResetToConstructed()
    {
        lock (_gate)
        {
            _byFactoryId.Clear();
            _byObjectId.Clear();
            DiscoveryEnabled = false;
            _tapFilterEnabled = true;
            _tapQueue.Clear();
            _tapDeadlineMs = 0;
            _doubleTaps.Clear();
            _lastReportTime.Clear();
            _lastReportMissed.Clear();
        }
        Connections.ResetToConstructed();
    }

    // ------------------------------------------------------ BlockTapFilterComponent (M4-023)

    /// <summary>CD10e: a tap at or below this intensity (tapPos − tapNeg) is dropped.</summary>
    public const int TapIntensityMin = 60;
    /// <summary>CD10e: a physical robot's taps queue this long from the first one.</summary>
    public const uint TapQueueMs = 75;
    /// <summary>CD10g: the double-tap window and movement suppression.</summary>
    public const uint DoubleTapWindowMs = 500;

    /// <summary>+0x18: enabled from the constructor (CD10d).</summary>
    private bool _tapFilterEnabled = true;
    private readonly List<(ObjectTapped Tap, Cube Cube, int Intensity)> _tapQueue = new();
    private uint _tapDeadlineMs;

    /// <summary>DoubleTapInfo: +0x18 window end, +0x1C moving, +0x20 ignore-until, +0x24 pending (CD10g).</summary>
    private sealed class DoubleTapInfo { public uint WindowEnd; public bool Moving; public uint IgnoreUntil; public bool Pending; }
    /// <summary>Per object; keyed by type, one ObjectID per light-cube type (LC8e).</summary>
    private readonly Dictionary<ObjectType, DoubleTapInfo> _doubleTaps = new();

    // fidelity: M4-027
    /// <summary>
    /// The tap filter's deletion at 0x005111EA..0x00511224 (T1): the ObjectTapped list (+0x2C) and the DoubleTapInfo tree (+0x20)
    /// are destroyed, silently (no broadcast, no log), then its retained subscription vector is retired.
    /// Run by the Robot's Lifetime slot 0x450 (<see cref="CozmoMotion.BindRobotLifetime"/>).
    /// </summary>
    // fidelity: M4-027 — Robot+0x450 identity confirmed by small-reads-2 check.
    internal void DestroyTapFilter()
    {
        lock (_gate)
        {
            _tapQueue.Clear();
            _doubleTaps.Clear();
        }
        Behavior.SteppedBehavior.ReportMissing("M2-023/M4-027: BlockTapFilter +0x14 shared filter owner release at 0x00511206..0x0051120A is unbuilt; queue/tree and handle-vector retirement do not implement that object lifetime");
        _tapSubscriptions.Retire(); // M2-020: the component owns B6/B4/B5, not Impl.
    }
    private readonly SubscriptionOwners _tapSubscriptions = new();
    // fidelity: M2-020 — retain the same per-tag registration point, in the component's owner vector.
    internal void AttachRobot(EngineRobot robot)
    {
        // B6 DeviceRoute(m, true) currently has no effect; kept at the checked component registration point.
        _tapSubscriptions.Add(robot.SubscribeMessage(new ObjectTapped().Id,m=>_robot.Engine.DeviceRoute?.Invoke(m,true),"BlockTapFilter"));
        _tapSubscriptions.Add(robot.SubscribeMessage(RobotMessageId.ActiveObjectMoved,m=>_robot.Engine.TapFilterRoute?.Invoke(m),"BlockTapFilter"));
        _tapSubscriptions.Add(robot.SubscribeMessage(RobotMessageId.ActiveObjectStopped,m=>_robot.Engine.TapFilterRoute?.Invoke(m),"BlockTapFilter"));
    }

    /// <summary>Whether the tap filter is on (+0x18).</summary>
    public bool BlockTapFilterEnabled { get { lock (_gate) return _tapFilterEnabled; } }

    // fidelity: M4-023
    /// <summary>The game EnableBlockTapFilter (tag 0x62, CD10d): sets +0x18.</summary>
    public void SetBlockTapFilterEnabled(bool enable) { lock (_gate) _tapFilterEnabled = enable; }

    private uint NowMs => _robot.Engine.Timer.TimeStampMs;
    private void Log(string l) => _robot.Engine.Log(l);
    private void Log(EngineLogLevel level, string? channel, string key, string text) => _robot.Engine.EmitSourceLog(level, channel, key, text);

    private DoubleTapInfo TapInfo(Cube c)
    {
        if (!_doubleTaps.TryGetValue(c.Type, out var d)) _doubleTaps[c.Type] = d = new DoubleTapInfo();
        return d;
    }

    /// <summary>
    /// Raised with the object id of a cube whose pending double-tap entry has ended (CD10g), for BlockWorld's
    /// MarkObjectDirty of its located copies.
    /// </summary>
    internal event Action<uint>? DoubleTapPendingEnded;

    // fidelity: M4-023
    /// <summary>ShouldIgnoreMovementDueToDoubleTap: ignore-until &gt; now (CD10g).</summary>
    private bool ShouldIgnoreMovementDueToDoubleTap(Cube c) =>
        _doubleTaps.TryGetValue(c.Type, out var d) && unchecked((int)(d.IgnoreUntil - NowMs)) > 0;

    // fidelity: M4-023
    /// <summary>
    /// CheckForDoubleTap (CD10g, 0x00630DC4..0x00630E58): moving clears the window; else inside the window it is a
    /// double tap (logged, no broadcast), window and pending cleared; else window = ignore-until = now + 500, pending.
    /// </summary>
    private void CheckForDoubleTap(Cube c, Action<string> stackLog)
    {
        var d = TapInfo(c);
        uint now = NowMs;
        if (d.Moving) d.WindowEnd = 0;
        else if (d.WindowEnd != 0 && unchecked((int)(now - d.WindowEnd)) < 0)
        {
            // MISSING: BlockTapFilterComponent.Update.DoubleTap, call arguments at 0x00631670/0x0063169C
            // are not in the checked rows. This existing stack text is not an engine call.
            stackLog($"Detected double tap on {c.Type}");
            stackLog("MISSING: M4-023 | BlockTapFilterComponent.Update.DoubleTap: checked call arguments required (0x00631670/0x0063169C)");
            d.WindowEnd = 0;
            d.Pending = false;
        }
        else
        {
            d.WindowEnd = d.IgnoreUntil = unchecked(now + DoubleTapWindowMs);
            d.Pending = true;
        }
    }

    private static void RecordTap(Cube c, ObjectTapped t)
    {
        c.Taps++;
        c.LastTap = t;
        c.LastBroadcastTimestamp = t.Timestamp;
        c.LastTapUtc = DateTime.UtcNow;
        c.LastSeenUtc = c.LastTapUtc.Value;
    }

    // fidelity: M4-010, M4-023
    /// <summary>
    /// The components Robot::Update runs for cubes (CD2): BlockTapFilterComponent::Update (0x00513EA4: once now is past
    /// the deadline the queued tap with the greatest intensity is broadcast, strictly greater winning so the earliest
    /// wins a tie, and the queue cleared, CD10f; a pending double-tap entry past its ignore-until is cleared, CD10g - the
    /// MarkObjectDirty that follows is BlockWorld's, M11), then the connection path (BlockFilter, CheckDisconnected,
    /// ConnectToRequested).
    /// </summary>
    internal void Update()
    {
        Cube? tapped = null;
        var ended = new List<uint>();
        lock (_gate)
        {
            uint now = NowMs;
            if (_tapQueue.Count > 0 && unchecked((int)(now - _tapDeadlineMs)) > 0)
            {
                var best = _tapQueue[0];
                foreach (var q in _tapQueue) if (q.Intensity > best.Intensity) best = q;
                _tapQueue.Clear();
                RecordTap(best.Cube, best.Tap);
                tapped = best.Cube;
            }
            foreach (var (type, d) in _doubleTaps)
                if (d.Pending && unchecked((int)(now - d.IgnoreUntil)) > 0)
                {
                    d.Pending = false;
                    if (_byFactoryId.Values.FirstOrDefault(c => c.Type == type && c.ObjectId is not null) is { } c) ended.Add(c.ObjectId!.Value);
                }
        }
        if (tapped is not null)
        {
            var notice = new CubeTimestampEvent(tapped, tapped.LastTap!.Timestamp);
            _robot.Engine.QueueAppEvent(CubeTapped,tapped.SnapshotForApp());
            _robot.Engine.QueueAppBroadcast(TapBroadcast,new CubeTimestampEvent(tapped.SnapshotForApp(),notice.Timestamp));
        }
        // CD10g (0x006311A2..0x006313D8): MarkObjectDirty on every located copy whose pose state is Known (+0x24 == 1);
        // BlockWorld.MarkDirty changes only a Known pose.
        foreach (var id in ended) EventFan.Raise(DoubleTapPendingEnded, id, null);
        Connections.Update();
    }

    // ----------------------------------------------------------------- plumbing

    private Cube Track(uint factoryId)
    {
        if (_byFactoryId.TryGetValue(factoryId, out var c)) return c;
        c = new Cube { FactoryId = factoryId };
        _byFactoryId[factoryId] = c;
        return c;
    }

    /// <summary>The connected object for an activeID (GetConnectedActiveObjectByActiveIdHelper).</summary>
    private Cube? Connected(uint activeId) =>
        _byObjectId.TryGetValue(activeId, out var c) && c.Connected ? c : null;

    /// <summary>
    /// The exclusion the Moved and Stopped broadcasts apply (CD10a, CD10b): the carried object
    /// (CarryingComponent+8, robot+0x284) and the dock target (DockingComponent+0xC, [robot+0x280]+0xC; C11.1
    /// D1..D3). The M12 manipulation layer wires both; nothing is excluded until it does.
    /// </summary>
    internal Func<Cube, bool>? ExcludeFromMovedBroadcast { get; set; }

    /// <summary>
    /// A12: the world's lookup of the connected object for an activeID, returning its ObjectID (BlockWorld.ConnectedObjectIdForActiveId). Null until a world is attached.
    /// </summary>
    internal Func<uint, uint?>? ConnectedObjectIdForActiveId { get; set; }
    // fidelity: M4-009 — 13.1: -1 at construction, only MountChargerAction.Init (M13-008) may write it; never reset.
    internal int ChargerObjectId { get; private set; } = -1;
    internal void SetChargerObjectIdFromMountInit(uint id) => ChargerObjectId = unchecked((int)id);
    // Whether connected ObjectID -1 is possible belongs to M11-013/M11-041.
    private int? WorldId(Cube c, Action<string>? log = null)
    {
        var id = ConnectedObjectIdForActiveId?.Invoke(c.ObjectId ?? uint.MaxValue);
        if (id is null) (log ?? Log)("MISSING: M4-009 / M11-004 | connected BlockWorld ObjectID lookup unavailable; id-dependent work skipped");
        return id is { } value ? unchecked((int)value) : null;
    }
    private bool IsCharger(Cube c, Action<string>? log = null) => WorldId(c, log) == ChargerObjectId;
    private string F6(float v) => float.IsNaN(v) ? "nan" : float.IsPositiveInfinity(v) ? "inf" : float.IsNegativeInfinity(v) ? "-inf" : v.ToString("F6", System.Globalization.CultureInfo.InvariantCulture);


    /// <summary>Fed every robot message by <see cref="CozmoRobot"/>.</summary>
    // fidelity: M4-023 — constructor subscription precedes Impl on B4/B5.
    internal void HandleTapFilterMovement(RobotMessage message)
    {
        uint id = message is ObjectMoved moved ? moved.ObjectID : ((ObjectStoppedMoving)message).ObjectID;
        bool missing;
        lock (_gate)
        {
            var cube = Connected(id); missing = cube is null;
            if (cube is not null)
            {
                var info = TapInfo(cube);
                if (message is ObjectStoppedMoving) info.Moving = false;
                else if (info.WindowEnd == 0) info.Moving = true;
            }
        }
        if (missing)
        {
            string key = message is ObjectMoved ? "BlockTapFilterComponent.HandleActiveObjectMoved.ObjectIDNull"
                : "BlockTapFilterComponent.HandleActiveObjectStopped.ObjectIDNull";
            _robot.Engine.EmitSourceLog(EngineLogLevel.Warning, null, key, $"Could not find match for active object ID {unchecked((int)id)}");
        }
    }

    internal void Handle(RobotMessage m)
    {
        Cube? discovered = null, connectionChanged = null, tapped = null, moved = null;
        ObjectPowerLevel? powerBroadcast = null;
        CubeTimestampEvent? movementBroadcast = null;
        CubeUpAxisEvent? upAxisBroadcast = null;
        var callouts = new List<Action>();
        void Log(string line) => callouts.Add(() => this.Log(line));
        void SourceLog(EngineLogLevel level, string? channel, string key, string text) => callouts.Add(() => this.Log(level, channel, key, text));
        void StackLog(string line) => callouts.Add(() => _robot.Engine.EmitStackLog(line));
        lock (_gate)
        {
            switch (m)
            {
                case ObjectAvailable a:
                {
                    // fidelity: M4-025
                    // D6: HandleActiveObjectAvailable records the advertisement in the active-object map, then gates
                    // the game-side ObjectAvailable broadcast on robot+0x490, which is never set (D1..D4), so the
                    // engine broadcasts nothing. This stack records the advertisement (the internal tracking the
                    // connection path needs) and raises no ObjectAvailable/ObjectUnavailable broadcast.
                    // The engine records an advertisement only for a light cube or the charger.
                    if (!IsTrackedActiveObject(a.ObjectType))
                    {
                        SourceLog(EngineLogLevel.Warning, null, "Robot.HandleActiveObjectAvailable.UnknownType", $"FactoryID: 0x{a.FactoryId:x}, ObjectType: '{CubeConnections.ObjectTypeName(a.ObjectType)}'");
                        break;
                    }
                    bool isNew = !_byFactoryId.ContainsKey(a.FactoryId);
                    var c = Track(a.FactoryId);
                    c.Type = a.ObjectType;
                    c.Rssi = a.Rssi;
                    c.LastSeenUtc = DateTime.UtcNow;
                    c.Advertisements++;
                    if (isNew) discovered = c;
                    callouts.Add(() => Connections.OnObjectAvailable(a.FactoryId, a.ObjectType, a.Rssi));
                    // 0x0053398E..0x005339C8: the ObjectAvailable log also requires +0x490 and signed RSSI <=49.
                    if (BroadcastsAvailableObjects && a.Rssi <= 49)
                        SourceLog(EngineLogLevel.Info, "Unnamed", "Robot.HandleActiveObjectAvailable.ObjectAvailable", $"Type: {CubeConnections.ObjectTypeName(a.ObjectType)}, FactoryID 0x{a.FactoryId:x}, rssi {a.Rssi}, (currTime {unchecked((int)(_robot.Engine.Robot?.StoredState?.Timestamp ?? 0))})");
                    break;
                }
                case ObjectConnectionState s:
                {
                    // fidelity: M4-009
                    // Retained host view: ByObjectId/ConnectionChanged expose radio slots,
                    // including synthetic slots7..9 used by older fixtures. They do not
                    // supply the native ObjectID. CozmoRobot owns the source gate/world/
                    // pool/Recvd/E2G path; ConnectionStateBroadcast exposes its world ID.
                    callouts.Add(() => Behavior.SteppedBehavior.ReportMissing("M11-013 / M11-041 | CozmoCubes host ByObjectId/ConnectionChanged still expose reported radio slots, including synthetic slots above4; source HandleActiveObjectConnectionState 0x00533B3C uses the Robot-owned BlockWorld returned ID and its own slot gate. Host-view migration remains unbuilt."));
                    bool isNew = !_byFactoryId.ContainsKey(s.FactoryID);
                    var c = Track(s.FactoryID);
                    c.Type = s.ObjectType;
                    c.LastSeenUtc = DateTime.UtcNow;
                    if (s.Connected)
                    {
                        if (_byObjectId.TryGetValue(s.ObjectID, out var held) && !ReferenceEquals(held, c)) held.Connected = false;
                        c.ObjectId = s.ObjectID;
                        _byObjectId[s.ObjectID] = c;
                    }
                    else if (_byObjectId.TryGetValue(s.ObjectID, out var held) && ReferenceEquals(held, c))
                        _byObjectId.Remove(s.ObjectID);
                    if (c.Connected != s.Connected)
                    {
                        c.Connected = s.Connected;
                        connectionChanged = c;
                    }
                    if (isNew) discovered = c;
                    // Source pool/log/E2G ordering lives in CozmoRobot's handler.
                    // This registry is the retained host slot adapter, not its returned ObjectID.
                    break;
                }
                case ObjectPowerLevel p:
                {
                    // fidelity: M4-008, M4-032 (the %.2f renders of volts and percent, F2, are the phone printf boundary)
                    // HandleObjectPowerLevel 0x00537130..0x005373E8 (rows A1..A16). The log lines follow this file's convention (no channel prefix); the channel is "Unnamed".
                    uint robotId = CozmoEngine.RobotId;                                      // A2: Robot+0x10
                    float volts = p.BatteryLevel / 100f;                                      // A3
                    float percent = BatteryPercentOf(volts);
                    SourceLog(EngineLogLevel.Debug, "Unnamed", "RobotToEngine.ObjectPowerLevel.Log", $"RobotID {robotId} activeID {p.ObjectID} at {F2(volts)}V {F2(percent)}%");   // A4
                    // A6: BaseStationTimer::GetCurrentTimeInSeconds (the stack's engine clock, Seconds), truncated to u32 by vcvt.u32.f32.
                    float secs = Seconds();
                    uint now = secs >= 4294967296f ? uint.MaxValue : secs > 0f ? (uint)secs : 0u;
                    _lastReportTime.TryAdd(p.ObjectID, 0u);                                   // A7: operator[] inserts 0
                    _lastReportMissed.TryAdd(p.ObjectID, 0u);
                    uint then = _lastReportTime[p.ObjectID];
                    // A8: skipped only when a report was made, it was under 600 s ago ((now - then) >> 3 <= 0x4A, unsigned) and no more than 512 more packets were missed.
                    bool skip = then != 0 && unchecked(now - then) >> 3 <= 0x4A && unchecked(p.MissedPackets - _lastReportMissed[p.ObjectID]) <= 0x200;
                    if (!skip)
                    {
                        SourceLog(EngineLogLevel.Debug, "Unnamed", "RobotToEngine.ObjectPowerLevel.Report", $"Sending DAS report for robotID {robotId} activeID {p.ObjectID} now {now} then {then}");   // A9
                        callouts.Add(() => _robot.Engine.EmitEngineLog(EngineLogLevel.Event, null, "robot.accessory_powerlevel",
                            $"{p.ObjectID} {F2(volts)}V ({unchecked((int)p.MissedPackets)} lost)",
                            new Dictionary<string, string> { ["$data"] = $"{F2(volts)},{F2(percent)}" }));
                        _lastReportTime[p.ObjectID] = now;                                    // A11, report path only
                        _lastReportMissed[p.ObjectID] = p.MissedPackets;
                    }
                    if (_byObjectId.TryGetValue(p.ObjectID, out var known))
                    {
                        known.BatteryLevelRaw = p.BatteryLevel;
                        known.MissedPackets = p.MissedPackets;
                        known.LastSeenUtc = DateTime.UtcNow;
                    }
                    // A12 (0x00537360..0x0053736A, BlockWorld::GetConnectedActiveObjectByActiveIdHelper 0x0061F720 -> FindConnectedObjectHelper 0x0061F078): the first connected object, in
                    // family / type / ObjectID order, whose activeID (+0x40) is the message's; none: the epilogue, no Broadcast log and no broadcast.
                    // A13 (0x0053736C..0x0053737A, Anki::ObjectID slot 0 at 0x004EF772): objectID is the object's game ObjectID value, not the activeID.
                    if (ConnectedObjectIdForActiveId is null)
                        Log("MISSING: HandleObjectPowerLevel 0x00537360: no BlockWorld is attached, so the connected-object lookup (A12) has nothing to search; nothing is broadcast (M11-004: BlockWorld's connected-object helpers, FindConnectedObjectHelper 0x0061F078)");
                    else if (ConnectedObjectIdForActiveId(p.ObjectID) is { } objectId)
                    {
                        SourceLog(EngineLogLevel.Debug, "Unnamed", "RobotToEngine.ObjectPowerLevel.Broadcast", $"RobotID {robotId} activeID {p.ObjectID} objectID {objectId} at {p.BatteryLevel} cv");   // A14 (objectID is %u)
                        powerBroadcast = new ObjectPowerLevel { ObjectID = objectId, MissedPackets = p.MissedPackets, BatteryLevel = p.BatteryLevel };   // A15
                    }
                    break;
                }
                case ObjectTapped t:
                {
                    // fidelity: M4-023
                    // CD10e (0x00630B2E..0x00630C8C): intensity = tapPos − tapNeg; ≤ 60 is dropped; an unknown id is
                    // dropped with a warning; with the filter on and a physical robot (robot+0x14, SetPhysicalRobot from
                    // HandleFirmwareVersion) the tap is queued, the first one setting deadline = now + 75 ms; otherwise
                    // ObjectTapped is broadcast at once. CheckForDoubleTap follows.
                    int intensity = unchecked((sbyte)t.TapPos) - unchecked((sbyte)t.TapNeg);
                    if (intensity <= TapIntensityMin)
                    {
                        // Manager-checked sChanneledInfoF 0x00630B5C; signed-byte difference 0x00630B2E..0x00630B42.
                        SourceLog(EngineLogLevel.Info, "BlockPool", "BlockTapFilterComponent.HandleEnableTapFilter.Ignored", $"Tap ignored {intensity} <= 60");
                        break;
                    }
                    if (Connected(t.ObjectID) is not { } c)
                    {
                        StackLog($"tap from unknown object {t.ObjectID}");
                        StackLog("MISSING: M4-023 | BlockTapFilterComponent.HandleActiveObjectTapped.UnknownActiveID: checked call arguments required (string 0x00BF9A81)");
                        break;
                    }
                    bool physical = _robot.Engine.Robot?.IsPhysicalRobot ?? false;
                    if (_tapFilterEnabled && physical)
                    {
                        if (_tapQueue.Count == 0) _tapDeadlineMs = unchecked(NowMs + TapQueueMs);
                        _tapQueue.Add((t, c, intensity));
                    }
                    else
                    {
                        RecordTap(c, t);
                        tapped = c;
                    }
                    CheckForDoubleTap(c, StackLog);
                    break;
                }
                case ObjectMoved mv:
                {
                    // fidelity: M4-009
                    // CD10a (0x00533E4C..0x005341BA): the connected object by activeID (unknown: a warning, no
                    // broadcast); the charger's garbage moves and a movement inside the double-tap window stop here;
                    // otherwise SetIsMoving(true, ts) when not already moving, and ObjectMoved is broadcast. BTF's own
                    // Moved handler sets its moving flag only while no double-tap window is open (CD10g).
                    if (Connected(mv.ObjectID) is not { } c)
                    {
                        SourceLog(EngineLogLevel.Warning, null, "Robot.ActiveObjectMoved.UnknownActiveID", $"Could not find match for active object ID {unchecked((int)mv.ObjectID)}");
                        callouts.Add(() => MovementLocatedLoop(mv.ObjectID, -1, true, mv.Timestamp, "Robot.ActiveObjectMoved."));
                        SourceLog(EngineLogLevel.Warning, null, "ObjectMovedOrStoppedHelper.UnknownActiveID", $"Could not find match for active object ID {unchecked((int)mv.ObjectID)}"); break;
                    }
                    if (WorldId(c, Log) is not { } movedWorldId) break;
                    if (IsCharger(c, Log)) { SourceLog(EngineLogLevel.Info, "Unnamed", "Robot.ActiveObjectMoved.Charger", "Charger sending garbage move messages"); break; }
                    SourceLog(EngineLogLevel.Info, "Unnamed", "Robot.ActiveObjectMoved.ObjectMovedOrStopped", $"ObjectID: {movedWorldId} (Active ID {unchecked((int)mv.ObjectID)}), type: {c.Type}, axisOfAccel: {UpAxisName(mv.AxisOfAccel)}, accel: {F6(mv.Accel.X)} {F6(mv.Accel.Y)} {F6(mv.Accel.Z)}, time: {unchecked((int)mv.Timestamp)} ms");
                    if (ShouldIgnoreMovementDueToDoubleTap(c)) { SourceLog(EngineLogLevel.Info, "Unnamed", "Robot.ActiveObjectMoved.IgnoringMessage", $"Waiting for double tap id:{movedWorldId} ignoring movement message"); break; }
                    if (!c.Moving) { c.Moving = true; c.MovingChangedAt = mv.Timestamp; }
                    _robot.VisionModeRecipient?.World.SetConnectedMoving(unchecked((uint)movedWorldId), true);
                    c.Accel = new Vector3(mv.Accel.X, mv.Accel.Y, mv.Accel.Z);
                    c.UpAxis = mv.AxisOfAccel;
                    c.LastSeenUtc = DateTime.UtcNow;
                    callouts.Add(() => MovementLocatedLoop(mv.ObjectID, movedWorldId, true, mv.Timestamp, "Robot.ActiveObjectMoved."));
                    if (ExcludeFromMovedBroadcast?.Invoke(c) != true)
                    {
                        moved = c;
                        c.LastBroadcastTimestamp = mv.Timestamp;
                        movementBroadcast = new CubeTimestampEvent(c, c.LastBroadcastTimestamp);
                    }
                    break;
                }
                case ObjectStoppedMoving sm:
                {
                    // fidelity: M4-009
                    // CD10b (0x00534636..0x00534AA4): the same lookup and charger filter; the double-tap test's result
                    // is discarded; SetIsMoving(false, ts) when moving; ObjectStoppedMoving is broadcast. BTF's Stopped
                    // handler clears its moving flag (CD10g).
                    if (Connected(sm.ObjectID) is not { } c)
                    {
                        SourceLog(EngineLogLevel.Warning, null, "Robot.ActiveObjectStopped.UnknownActiveID", $"Could not find match for active object ID {unchecked((int)sm.ObjectID)}");
                        callouts.Add(() => MovementLocatedLoop(sm.ObjectID, -1, false, sm.Timestamp, "Robot.ActiveObjectStopped."));
                        SourceLog(EngineLogLevel.Warning, null, "ObjectMovedOrStoppedHelper.UnknownActiveID", $"Could not find match for active object ID {unchecked((int)sm.ObjectID)}"); break;
                    }
                    if (WorldId(c, Log) is not { } stoppedWorldId) break;
                    if (IsCharger(c, Log)) { SourceLog(EngineLogLevel.Info, "Unnamed", "Robot.ActiveObjectStopped.Charger", "Charger sending garbage move messages"); break; }
                    SourceLog(EngineLogLevel.Info, "Unnamed", "Robot.ActiveObjectStopped.ObjectMovedOrStopped", $"ObjectID: {stoppedWorldId} (Active ID {unchecked((int)sm.ObjectID)}), type: {c.Type}, axisOfAccel: <unknown>, accel: 0.000000 0.000000 0.000000, time: {unchecked((int)sm.Timestamp)} ms");
                    _ = ShouldIgnoreMovementDueToDoubleTap(c);
                    if (c.Moving) { c.Moving = false; c.MovingChangedAt = sm.Timestamp; }
                    _robot.VisionModeRecipient?.World.SetConnectedMoving(unchecked((uint)stoppedWorldId), false);
                    c.LastSeenUtc = DateTime.UtcNow;
                    callouts.Add(() => MovementLocatedLoop(sm.ObjectID, stoppedWorldId, false, sm.Timestamp, "Robot.ActiveObjectStopped."));
                    if (ExcludeFromMovedBroadcast?.Invoke(c) != true)
                    {
                        moved = c;
                        c.LastBroadcastTimestamp = sm.Timestamp;
                        movementBroadcast = new CubeTimestampEvent(c, sm.Timestamp);
                    }
                    break;
                }
                case ObjectUpAxisChanged ua:
                {
                    // fidelity: M4-009
                    // CD10c (0x00534D66..0x00534E1E): an unknown id is an error; otherwise ObjectUpAxisChanged is
                    // broadcast with the object's id. Nothing is stored on the engine's object; UpAxis here is the
                    // receiver's record of the broadcast.
                    if (Connected(ua.ObjectID) is not { } c) { SourceLog(EngineLogLevel.Error, null, "Robot.HandleActiveObjectUpAxisChanged.UnknownActiveID", $"Could not find match for active object ID {unchecked((int)ua.ObjectID)}"); callouts.Add(Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak); break; }
                    if (WorldId(c, Log) is not { } upWorldId) break;
                    SourceLog(EngineLogLevel.Info, "Unnamed", "Robot.HandleActiveObjectUpAxisChanged.UpAxisChanged", $"Type: {c.Type}, ObjectID: {upWorldId}, UpAxis: {UpAxisName(ua.UpAxis)}");
                    c.UpAxis = ua.UpAxis;
                    c.LastBroadcastTimestamp = ua.Timestamp;
                    upAxisBroadcast = new CubeUpAxisEvent(ua.Timestamp, upWorldId, ua.UpAxis);
                    c.LastSeenUtc = DateTime.UtcNow;
                    break;
                }
            }
        }
        foreach (var callout in callouts) callout();
        // fidelity: M4-025
        // D6/D5: HandleActiveObjectAvailable's game-side ObjectAvailable and the expiry ObjectUnavailable are
        // gated on robot+0x490, which is 0 at the ctor and has no writer in this build (D1..D4), so the engine
        // never broadcasts either. CubeDiscovered is this stack's availability notice, so it takes the same gate
        // and never fires in this build; the advertisement is still tracked for the connection path.
        if (discovered is not null && BroadcastsAvailableObjects) CubeDiscovered?.Invoke(discovered);
        if (connectionChanged is not null) ConnectionChanged?.Invoke(connectionChanged);
        if (tapped is not null)
        {
            var notice = new CubeTimestampEvent(tapped, tapped.LastTap!.Timestamp);
            _robot.Engine.QueueAppEvent(CubeTapped,tapped.SnapshotForApp());
            _robot.Engine.QueueAppBroadcast(TapBroadcast,new CubeTimestampEvent(tapped.SnapshotForApp(),notice.Timestamp));
        }
        if(moved is not null)_robot.Engine.QueueAppEvent(CubeMoved,moved.SnapshotForApp());
        if(movementBroadcast is not null)_robot.Engine.QueueAppBroadcast(MovementBroadcast,new CubeTimestampEvent(movementBroadcast.Cube.SnapshotForApp(),movementBroadcast.Timestamp));
        if(upAxisBroadcast is not null)_robot.Engine.QueueAppBroadcast(UpAxisBroadcast,upAxisBroadcast);
        if(powerBroadcast is not null)_robot.Engine.QueueAppBroadcast(PowerLevelBroadcast,new ObjectPowerLevel{ObjectID=powerBroadcast.ObjectID,MissedPackets=powerBroadcast.MissedPackets,BatteryLevel=powerBroadcast.BatteryLevel});
    }

    /// <summary>The engine's ID-gated E2G tag 0x0D report, with its returned world ObjectID.</summary>
    public event Action<ObjectConnectionStateMessage>? ConnectionStateBroadcast;

    // fidelity: M11-085, M4-018
    internal void BroadcastConnectionState(ObjectConnectionStateMessage report)
    {
        _robot.Engine.QueueAppBroadcast(ConnectionStateBroadcast,report);
        _robot.Engine.AnimationMessages.Deliver(ObjectConnectionStateMessage.UnionTag, true, report);
        // Existing checked CubeLight recipient; CubeAccel's recipient is a no-op.
        // Feeding/GuardDog native owners/subscriptions remain M15-056, not fabricated here.
        _robot.Lights.Cubes.OnObjectConnectionState(report.ObjectType, report.ConnectedByte != 0);

    }

    // fidelity: M4-009
    private void MovementLocatedLoop(uint activeId, int objectId, bool moving, uint timestamp, string prefix)
    {
        var world = _robot.VisionModeRecipient?.World;
        if (world is null)
        {
            Log("MISSING: M4-009 / M11-004 | FindLocatedMatchingObjects needs the attached BlockWorld"); return;
        }
        // Native message work is whole between frames (0x004ED62E before 0x004ED648).
        using var frameMutation = world.EnterPoseMutation();
        var filter = new Vision.BlockWorldFilter { OriginMode = Vision.OriginMode.InAnyFrame };
        filter.Predicates.Add(o => o.ActiveId == unchecked((int)activeId));
        var objects = world.FindLocatedMatchingObjects(filter);
        foreach (var o in objects)
        {
            if (unchecked((int)o.ObjectId) != objectId)
                _robot.Engine.EmitSourceLog(EngineLogLevel.Warning, null, prefix + "ActiveObjectInDifferentFramesWithDifferentIDs",
                    FormattableString.Invariant($"First object={unchecked((int)objects[0].ObjectId)} in '{world.RootName(objects[0])}'. This object={unchecked((int)o.ObjectId)} in '{world.RootName(o)}'."));
            world.ApplyMovedOrStopped(o, moving);
        }
    }

    /// <summary>"%.2f": two decimals, the invariant point (ordinary values; the engine formats with the phone printf).</summary>
    // EnumToString(UpAxis) 0x007B9BE4..0x007B9BF6: unsigned >7 returns NULL; M4-032's %s renders (null).
    private static string UpAxisName(UpAxis axis) => (byte)axis <= 7 ? axis.ToString() : "(null)";

    private static string F2(double v) => v.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

    private static float BatteryPercentOf(float volts) => Cube.BatteryPercentOf(volts);
}
