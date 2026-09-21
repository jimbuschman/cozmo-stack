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
    /// <summary>How many times this cube has been tapped since the connection opened.</summary>
    public int Taps { get; internal set; }
    /// <summary>When it was last tapped.</summary>
    public DateTime? LastTapUtc { get; internal set; }

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

    /// <summary>
    /// The largest id a connection report from the robot may carry, which is the last of the engine's
    /// <c>MAX_NUM_ACTIVE_OBJECTS</c> = 5 radio slots. <c>HandleActiveObjectConnectionState</c> 0x00533B3C
    /// reads the id first and returns when it is above 4 (<c>cmp r7, #4; bhi</c> at 0x00533B58), before it
    /// touches BlockWorld or its DAS event.
    ///
    /// It is documented rather than enforced, because the two sides mean different things by "object id".
    /// The engine has two id spaces: the radio slot the robot reports, which this bounds, and the id
    /// BlockWorld hands back from <c>AddConnectedActiveObject(slot, factoryId, type)</c>, which is what the
    /// rest of the engine passes around. This stack keeps one - the id on the wire is the id the world
    /// model uses - so refusing an id above 4 here would refuse a world object rather than an impossible
    /// radio slot. A robot never sends one above 4 either way.
    /// </summary>
    public const uint MaxActiveObjectSlot = 4;

    internal CozmoCubes(CozmoRobot robot) => _robot = robot;

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
    /// Turns the robot's accessory discovery on or off. With it on the robot reports every cube it hears;
    /// leaving it on indefinitely keeps its radio busy, so turn it off once the cubes are found.
    /// </summary>
    public void SetDiscovery(bool enable)
    {
        _robot.Transport.Send(new SetAccessoryDiscovery { Enable = enable }, flush: true);
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

    // ----------------------------------------------------------------- plumbing

    private Cube Track(uint factoryId)
    {
        if (_byFactoryId.TryGetValue(factoryId, out var c)) return c;
        c = new Cube { FactoryId = factoryId };
        _byFactoryId[factoryId] = c;
        return c;
    }

    /// <summary>Fed every robot message by <see cref="CozmoRobot"/>.</summary>
    internal void Handle(RobotMessage m)
    {
        Cube? discovered = null, connectionChanged = null, tapped = null, moved = null;
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
                    break;
                }
                case ObjectConnectionState s:
                {
                    bool isNew = !_byFactoryId.ContainsKey(s.FactoryID);
                    var c = Track(s.FactoryID);
                    c.ObjectId = s.ObjectID;
                    c.Type = s.ObjectType;
                    c.LastSeenUtc = DateTime.UtcNow;
                    _byObjectId[s.ObjectID] = c;
                    if (c.Connected != s.Connected)
                    {
                        c.Connected = s.Connected;
                        connectionChanged = c;
                    }
                    if (isNew) discovered = c;
                    break;
                }
                case ObjectPowerLevel p when _byObjectId.TryGetValue(p.ObjectID, out var c):
                    c.BatteryLevelRaw = p.BatteryLevel;
                    c.MissedPackets = p.MissedPackets;
                    c.LastSeenUtc = DateTime.UtcNow;
                    break;
                case ObjectTapped t when _byObjectId.TryGetValue(t.ObjectID, out var c):
                    c.Taps++;
                    c.LastTapUtc = DateTime.UtcNow;
                    c.LastSeenUtc = c.LastTapUtc.Value;
                    tapped = c;
                    break;
                case ObjectMoved mv when _byObjectId.TryGetValue(mv.ObjectID, out var c):
                    c.Accel = new Vector3(mv.Accel.X, mv.Accel.Y, mv.Accel.Z);
                    c.UpAxis = mv.AxisOfAccel;
                    c.LastSeenUtc = DateTime.UtcNow;
                    if (!c.Moving) { c.Moving = true; moved = c; }
                    break;
                case ObjectStoppedMoving sm when _byObjectId.TryGetValue(sm.ObjectID, out var c):
                    c.LastSeenUtc = DateTime.UtcNow;
                    if (c.Moving) { c.Moving = false; moved = c; }
                    break;
                case ObjectUpAxisChanged ua when _byObjectId.TryGetValue(ua.ObjectID, out var c):
                    c.UpAxis = ua.UpAxis;
                    c.LastSeenUtc = DateTime.UtcNow;
                    break;
            }
        }
        if (discovered is not null) CubeDiscovered?.Invoke(discovered);
        if (connectionChanged is not null) ConnectionChanged?.Invoke(connectionChanged);
        if (tapped is not null) CubeTapped?.Invoke(tapped);
        if (moved is not null) CubeMoved?.Invoke(moved);
    }
}
