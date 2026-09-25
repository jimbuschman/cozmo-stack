using Cozmo.Protocol;

namespace Cozmo.Robot;

/// <summary>What is known about one of Cozmo's light cubes.</summary>
public sealed class Cube
{
    /// <summary>The cube's permanent identifier, reported when it advertises and when it connects.</summary>
    public uint FactoryId { get; internal set; }
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
    public float? BatteryPercent => BatteryVolts is { } v
        ? v >= 1.5f ? 100f : v <= 1.0f ? 0f : (v - 1f) * 200f
        : null;
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
    /// telemetry on is that slot, the engine's activeID (S2: vbase+0x40). The bound is recorded and not enforced
    /// here, because this stack also uses the slot as its world ObjectID (M11 interface); see Handle.
    /// </summary>
    public const uint MaxActiveObjectSlot = 4;

    internal CozmoCubes(CozmoRobot robot)
    {
        _robot = robot;
        // fidelity: M4-010
        // The connection path's clock is BaseStationTimer::GetCurrentTimeInSeconds (CD3, CD4, CD8).
        Seconds = () => _robot.Engine.Timer.SecondsF;
        Connections = new CubeConnections(m => _robot.SendMessage(m, reliable: true, flush: true),
                                          () => Seconds());
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

    /// <summary>Whether the tap filter is on (+0x18).</summary>
    public bool BlockTapFilterEnabled { get { lock (_gate) return _tapFilterEnabled; } }

    // fidelity: M4-023
    /// <summary>The game EnableBlockTapFilter (tag 0x62, CD10d): sets +0x18.</summary>
    public void SetBlockTapFilterEnabled(bool enable) { lock (_gate) _tapFilterEnabled = enable; }

    private uint NowMs => _robot.Engine.Timer.TimeStampMs;
    private void Log(string l) => _robot.Engine.Log(l);

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

    // fidelity: M4-009, M4-023
    /// <summary>
    /// Whether HandleActiveObjectMoved returns before SetIsMoving and MarkObjectDirty for this active id (CD10a steps
    /// 1..3): no connected object, the charger, or a movement inside the double-tap window.
    /// </summary>
    internal bool MovedStopsBeforeTheWorld(uint activeId)
    {
        lock (_gate)
            return Connected(activeId) is not { } c || c.Type == ObjectType.Charger_Basic || ShouldIgnoreMovementDueToDoubleTap(c);
    }

    // fidelity: M4-009
    /// <summary>
    /// Whether HandleActiveObjectStopped returns before SetIsMoving(false) for this active id (CD10b): no connected object
    /// or the charger. Its double-tap test's result is discarded (0x00534A62), so it is not applied.
    /// </summary>
    internal bool StoppedStopsBeforeTheWorld(uint activeId)
    {
        lock (_gate)
            return Connected(activeId) is not { } c || c.Type == ObjectType.Charger_Basic;
    }

    // fidelity: M4-023
    /// <summary>ShouldIgnoreMovementDueToDoubleTap: ignore-until &gt; now (CD10g).</summary>
    private bool ShouldIgnoreMovementDueToDoubleTap(Cube c) =>
        _doubleTaps.TryGetValue(c.Type, out var d) && unchecked((int)(d.IgnoreUntil - NowMs)) > 0;

    // fidelity: M4-023
    /// <summary>
    /// CheckForDoubleTap (CD10g, 0x00630DC4..0x00630E58): moving clears the window; else inside the window it is a
    /// double tap (logged, no broadcast), window and pending cleared; else window = ignore-until = now + 500, pending.
    /// </summary>
    private void CheckForDoubleTap(Cube c)
    {
        var d = TapInfo(c);
        uint now = NowMs;
        if (d.Moving) d.WindowEnd = 0;
        else if (d.WindowEnd != 0 && unchecked((int)(now - d.WindowEnd)) < 0)
        {
            Log($"info: BlockTapFilterComponent: Detected double tap on {c.Type}");
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
        if (tapped is not null) CubeTapped?.Invoke(tapped);
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
    /// The carried-object test the Moved and Stopped broadcasts are excluded on (CD10a, CD10b). MISSING interface: the
    /// carried object (M12 CarryingComponent) and the ID at [robot+0x280]+0xC (not read) are not wired, so nothing is
    /// excluded until they are.
    /// </summary>
    internal Func<Cube, bool>? ExcludeFromMovedBroadcast { get; set; }

    /// <summary>Fed every robot message by <see cref="CozmoRobot"/>.</summary>
    internal void Handle(RobotMessage m)
    {
        Cube? discovered = null, connectionChanged = null, tapped = null, moved = null;
        (ObjectType Type, bool Connected)? lights = null;
        lock (_gate)
        {
            switch (m)
            {
                case ObjectAvailable a:
                {
                    // The engine records an advertisement only for a light cube or the charger.
                    if (!IsTrackedActiveObject(a.ObjectType)) break;
                    bool isNew = !_byFactoryId.ContainsKey(a.FactoryId);
                    var c = Track(a.FactoryId);
                    c.Type = a.ObjectType;
                    c.Rssi = a.Rssi;
                    c.LastSeenUtc = DateTime.UtcNow;
                    c.Advertisements++;
                    if (isNew) discovered = c;
                    Connections.OnObjectAvailable(a.FactoryId, a.ObjectType, a.Rssi);
                    break;
                }
                case ObjectConnectionState s:
                {
                    // fidelity: M4-009
                    // LC8a (0x00533B56..0x00533CB2): the engine ignores a slot above 4. NOT REPRODUCED: this stack uses
                    // the slot as its BlockWorld ObjectID (an M11 interface; the engine's ObjectIDs come from
                    // ObservableObject::SetID, LC8e), and its manipulation and vision fixtures connect cubes on slots
                    // 7..9; the bound is recorded (MaxActiveObjectSlot), and CubeConnections applies it. A robot never
                    // reports a slot above 4. Connected: AddConnectedActiveObject
                    // (a slot holding a different factory or type is emptied first, LC8c), HandleConnectedToObject,
                    // then the game-side broadcast, which the CubeLightComponent hears (S1, LC1). Disconnected:
                    // RemoveConnectedActiveObject (the connected entry with that activeID is erased, LC8b), then
                    // HandleDisconnectedFromObject.
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
                    Connections.OnConnectionState(s.ObjectID, s.FactoryID, s.Connected);
                    lights = (s.ObjectType, s.Connected);
                    break;
                }
                case ObjectPowerLevel p when _byObjectId.TryGetValue(p.ObjectID, out var c):
                    c.BatteryLevelRaw = p.BatteryLevel;
                    c.MissedPackets = p.MissedPackets;
                    c.LastSeenUtc = DateTime.UtcNow;
                    break;
                case ObjectTapped t:
                {
                    // fidelity: M4-023
                    // CD10e (0x00630B2E..0x00630C8C): intensity = tapPos − tapNeg; ≤ 60 is dropped; an unknown id is
                    // dropped with a warning; with the filter on and a physical robot (robot+0x14, SetPhysicalRobot from
                    // HandleFirmwareVersion) the tap is queued, the first one setting deadline = now + 75 ms; otherwise
                    // ObjectTapped is broadcast at once. CheckForDoubleTap follows.
                    int intensity = t.TapPos - t.TapNeg;
                    if (intensity <= TapIntensityMin) { Log($"info: BlockTapFilterComponent: Tap ignored {intensity} <= {TapIntensityMin}"); break; }
                    if (Connected(t.ObjectID) is not { } c) { Log($"warning: BlockTapFilterComponent: tap from unknown object {t.ObjectID}"); break; }
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
                    CheckForDoubleTap(c);
                    break;
                }
                case ObjectMoved mv:
                {
                    // fidelity: M4-009
                    // CD10a (0x00533E4C..0x005341BA): the connected object by activeID (unknown: a warning, no
                    // broadcast); the charger's garbage moves and a movement inside the double-tap window stop here;
                    // otherwise SetIsMoving(true, ts) when not already moving, and ObjectMoved is broadcast. BTF's own
                    // Moved handler sets its moving flag only while no double-tap window is open (CD10g).
                    if (Connected(mv.ObjectID) is not { } c) { Log($"warning: HandleActiveObjectMoved: unknown active id {mv.ObjectID}"); break; }
                    var d = TapInfo(c);
                    if (d.WindowEnd == 0) d.Moving = true;
                    if (c.Type == ObjectType.Charger_Basic) { Log("info: Charger sending garbage move messages"); break; }
                    if (ShouldIgnoreMovementDueToDoubleTap(c)) { Log($"info: HandleActiveObjectMoved: ignoring movement of {c.Type} after a double tap"); break; }
                    if (!c.Moving) { c.Moving = true; c.MovingChangedAt = mv.Timestamp; }
                    c.Accel = new Vector3(mv.Accel.X, mv.Accel.Y, mv.Accel.Z);
                    c.UpAxis = mv.AxisOfAccel;
                    c.LastSeenUtc = DateTime.UtcNow;
                    if (ExcludeFromMovedBroadcast?.Invoke(c) != true) moved = c;
                    break;
                }
                case ObjectStoppedMoving sm:
                {
                    // fidelity: M4-009
                    // CD10b (0x00534636..0x00534AA4): the same lookup and charger filter; the double-tap test's result
                    // is discarded; SetIsMoving(false, ts) when moving; ObjectStoppedMoving is broadcast. BTF's Stopped
                    // handler clears its moving flag (CD10g).
                    if (Connected(sm.ObjectID) is not { } c) { Log($"warning: HandleActiveObjectStopped: unknown active id {sm.ObjectID}"); break; }
                    TapInfo(c).Moving = false;
                    if (c.Type == ObjectType.Charger_Basic) { Log("info: Charger sending garbage move messages"); break; }
                    if (c.Moving) { c.Moving = false; c.MovingChangedAt = sm.Timestamp; }
                    c.LastSeenUtc = DateTime.UtcNow;
                    if (ExcludeFromMovedBroadcast?.Invoke(c) != true) moved = c;
                    break;
                }
                case ObjectUpAxisChanged ua:
                {
                    // fidelity: M4-009
                    // CD10c (0x00534D66..0x00534E1E): an unknown id is an error; otherwise ObjectUpAxisChanged is
                    // broadcast with the object's id. Nothing is stored on the engine's object; UpAxis here is the
                    // receiver's record of the broadcast.
                    if (Connected(ua.ObjectID) is not { } c) { Log($"error: HandleActiveObjectUpAxisChanged: unknown active id {ua.ObjectID}"); break; }
                    c.UpAxis = ua.UpAxis;
                    c.LastSeenUtc = DateTime.UtcNow;
                    break;
                }
            }
        }
        if (discovered is not null) CubeDiscovered?.Invoke(discovered);
        if (connectionChanged is not null) ConnectionChanged?.Invoke(connectionChanged);
        // fidelity: M4-018
        // LC7: the game-side ObjectConnectionState reaches the CubeLightComponent synchronously.
        if (lights is { } l) _robot.Lights.Cubes.OnObjectConnectionState(l.Type, l.Connected);
        if (tapped is not null) CubeTapped?.Invoke(tapped);
        if (moved is not null) CubeMoved?.Invoke(moved);
    }
}
