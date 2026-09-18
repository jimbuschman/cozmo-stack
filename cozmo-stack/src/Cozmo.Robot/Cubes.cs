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
/// </summary>
public sealed class CozmoCubes
{
    private readonly CozmoRobot _robot;
    private readonly object _gate = new();
    private readonly Dictionary<uint, Cube> _byFactoryId = new();
    private readonly Dictionary<uint, Cube> _byObjectId = new();

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
        get { lock (_gate) return _byFactoryId.Values.OrderByDescending(c => c.LastSeenUtc).ToArray(); }
    }

    /// <summary>The cubes the robot currently reports as connected.</summary>
    public IReadOnlyList<Cube> ConnectedCubes
    {
        get { lock (_gate) return _byFactoryId.Values.Where(c => c.Connected).ToArray(); }
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
