using Cozmo.Protocol;

namespace Cozmo.Robot;

/// <summary>
/// The state the engine keeps for one of its five active-object slots, as the numbers it stores at
/// <c>Robot+0x49C + 24 * slot</c>. The engine logs the value as a plain <c>%d</c>, so the names here are
/// descriptive; the numbers and every transition between them are the engine's.
/// </summary>
public enum ActiveObjectSlotState
{
    /// <summary>0: nothing in the slot (<c>ActiveObjectInfo::Reset</c> 0x00517DBC).</summary>
    Empty = 0,
    /// <summary>1: SetPropSlot sent with a cube, no connection reported yet (0x00514BEE).</summary>
    PendingConnection = 1,
    /// <summary>2: the robot reported the cube connected in this slot (0x00517A9E).</summary>
    Connected = 2,
    /// <summary>3: SetPropSlot sent with factory id 0 to empty a slot that holds a cube (0x00514C38).</summary>
    PendingDisconnection = 3,
    /// <summary>4: the robot reported a disconnection nobody asked for; reset after two seconds (0x00517D0A).</summary>
    Disconnected = 4,
}

/// <summary>One of the engine's five active-object slots.</summary>
public readonly record struct ActiveObjectSlot(int Slot, uint FactoryId, ObjectType Type, ActiveObjectSlotState State);

/// <summary>
/// How the official engine decides to connect a cube and asks the robot to do it: the automatic block pool
/// (<c>BlockFilter</c>), the five active-object slots (<c>Robot</c>), and the one message that initiates a
/// connection, <see cref="SetPropSlot"/>.
///
/// The path, all of it from libcozmoEngine.so:
/// <list type="number">
/// <item><c>RobotToEngineImplMessaging::HandleActiveObjectAvailable</c> 0x0053391C records every advertisement
/// from a light cube or the charger in an <c>unordered_map&lt;u32, ActiveObjectInfo&gt;</c> at Robot+0x47C, keyed
/// by factory id: the type, the RSSI byte and the timestamp of the last RobotState (Robot+0x2C, written by
/// <c>UpdateFullRobotState</c> 0x00512954 from the state's first word). <c>Robot::Update</c> 0x00514190..0x00514224
/// drops an entry once the robot's clock is 10001 ms or more past it.</item>
/// <item><c>BlockFilter::Update</c> 0x0061A79A, called from <c>Robot::Update</c> 0x0051422A, does nothing until
/// the game sends <c>BlockPoolEnabledMessage</c> (<c>BlockFilter::Enable</c> 0x0061B59C). The official app sends
/// it from <c>ConnectionFlowController.CubeConnectFlow</c> through <c>BlockPoolTracker.EnableAutoBlockPool</c>,
/// with a discovery time of 0. Once enabled it runs at most every 2 s.</item>
/// <item><c>BlockFilter::UpdateDiscovering</c> 0x0061A7EC: for each type in <c>kObjectTypes</c> {1, 2, 3} that
/// has no entry in the pool yet, the closest advertiser of that type (<c>GetClosestDiscoveredObjectsOfType</c>
/// 0x00518314, RSSI byte at most 150) is noted. When the discovery time has passed and something was noted,
/// each is added to the pool in the first empty entry (<c>AddObjectToPersistentPool</c> 0x0061AC5C) and the
/// pool's five factory ids go to <c>Robot::ConnectToObjects</c>. The pool index is the slot.</item>
/// <item><c>BlockFilter::UpdateConnecting</c> 0x0061AA8C, at most every 5 s after the last connect: a pooled
/// cube that no slot holds is swapped for the closest advertiser of its type, if that is a different cube.</item>
/// <item><c>Robot::ConnectToObjects</c> 0x00517150 marks a slot as pending when the requested factory id
/// differs from the one in the slot. It sends nothing.</item>
/// <item><c>Robot::ConnectToRequestedObjects</c> 0x00514A70, every <c>Robot::Update</c> (0x00514236): for a
/// pending slot whose request differs from what it holds - a request of 0 sends <c>SetPropSlot{0, slot}</c> and
/// marks the slot PendingDisconnection; any other request waits until that cube is in the advertisement
/// table, then sends <c>SetPropSlot{factoryId, slot}</c>, copies the advertisement into the slot and marks it
/// PendingConnection. Both are sent reliable and not hot (<c>Robot::SendMessage(msg, true, false)</c>).</item>
/// <item>The robot answers with <see cref="ObjectConnectionState"/> carrying the slot as its object id.
/// <c>HandleActiveObjectConnectionState</c> 0x00533B3C passes a connection to
/// <c>Robot::HandleConnectedToObject</c> 0x005179C0 (the slot becomes Connected and the cube leaves the
/// advertisement table) and a disconnection to <c>Robot::HandleDisconnectedFromObject</c> 0x00517BBC, which
/// empties a PendingDisconnection slot at once and marks any other Disconnected, and
/// <c>Robot::CheckDisconnectedObjects</c> 0x00514924 empties those two seconds later.</item>
/// </list>
///
/// Nothing on that path sends <see cref="SetAccessoryDiscovery"/>: its only references in the engine are its own
/// CLAD serializers, and the robot reports advertisements without being asked (the 2026-09-18 firmware-2457
/// capture has two before anything but the handshake was sent). The path has no timeout on a slot that never
/// connects, either; a PendingConnection slot stays so until the robot says otherwise.
///
/// The pool is persisted with the engine's text format and the advertisement table reproduces the libc++
/// container order used for equal-RSSI selection. The application lifecycle that enables this filter lives
/// in <see cref="CozmoRobot.ConnectAsync"/>.
/// </summary>
public sealed class CubeConnections
{
    /// <summary><c>MAX_NUM_ACTIVE_OBJECTS</c>: the loops in every function on this path run to 5.</summary>
    public const int SlotCount = 5;
    /// <summary>The RSSI limit both pool updates pass to <c>GetClosestDiscoveredObjectsOfType</c> (movs r2, #0x96).</summary>
    public const byte ClosestRssiLimit = 150;
    /// <summary>An advertisement is dropped when the robot's clock is this far past it (movw sb, #0x2711; blt).</summary>
    public const int UndiscoveredAfterMs = 10001;
    /// <summary><c>BlockFilter::Update</c> runs at most this often (vmov.f32 s2, #2.0 at 0x0061A7C2).</summary>
    public const float PoolUpdateIntervalSeconds = 2f;
    /// <summary><c>UpdateConnecting</c> waits this long after the last connect (vmov.f32 s0, #5.0 at 0x0061AA9C).</summary>
    public const float PoolReconnectDelaySeconds = 5f;
    /// <summary><c>CheckDisconnectedObjects</c>' interval and its delay before emptying a slot (vmov.f64 d8, #2.0).</summary>
    public const double DisconnectResetSeconds = 2.0;

    /// <summary><c>BlockFilter::kObjectTypes</c> 0x00C77F20: {1, 2, 3}.</summary>
    private static readonly ObjectType[] PoolTypes =
        { ObjectType.Block_LIGHTCUBE1, ObjectType.Block_LIGHTCUBE2, ObjectType.Block_LIGHTCUBE3 };

    /// <summary><c>Robot::ActiveObjectInfo</c>, 24 bytes.</summary>
    private struct Info
    {
        public uint FactoryId;              // +0x00
        public ObjectType Type;             // +0x04
        public ActiveObjectSlotState State; // +0x08
        public byte Rssi;                   // +0x0C
        public uint LastObservedTime;       // +0x10, robot timestamp in ms
        public float DisconnectedTime;      // +0x14, BaseStationTimer seconds

        public static Info Reset() => new() { Type = ObjectType.InvalidObject };
    }

    private readonly object _gate = new();
    private readonly Action<SetPropSlot> _send;
    private readonly Func<float> _seconds;

    // Robot
    private readonly ActiveObjectTable<Info> _available = new();     // Robot+0x47C, libc++ unordered_map order
    private readonly Info[] _slots = new Info[SlotCount];              // Robot+0x494
    private readonly (uint FactoryId, bool Pending)[] _requested = new (uint, bool)[SlotCount];  // Robot+0x454
    private double _lastDisconnectCheck;                             // Robot+0x510
    private uint _robotTime;                                         // Robot+0x2C

    // BlockFilter
    private readonly (uint FactoryId, ObjectType Type)[] _persistentPool = new (uint, ObjectType)[SlotCount];  // +0x04
    private readonly (uint FactoryId, ObjectType Type)[] _runtimePool = new (uint, ObjectType)[SlotCount];     // +0x2C
    private readonly SortedDictionary<ObjectType, uint> _discovering = new();                                  // +0x54
    private float _discoveryTime, _enableTime, _lastConnectTime, _lastPoolUpdate;                               // +0x6C..+0x78
    private bool _poolEnabled;                                                                                  // +0x7C
    private readonly List<SetPropSlot> _slotRequestsSent = new();

    /// <param name="send">Sends a SetPropSlot to the robot, reliably.</param>
    /// <param name="seconds">The engine's <c>BaseStationTimer::GetCurrentTimeInSeconds</c>.</param>
    public CubeConnections(Action<SetPropSlot> send, Func<float> seconds)
    {
        _send = send;
        _seconds = seconds;
        for (int i = 0; i < SlotCount; i++)
        {
            _slots[i] = Info.Reset();
            _persistentPool[i] = (0, ObjectType.InvalidObject);   // the constructor's {0, -1} at 0x00619A50
            _runtimePool[i] = (0, ObjectType.InvalidObject);
        }
    }

    /// <summary>Raised with each SetPropSlot as it is sent.</summary>
    public event Action<SetPropSlot>? SlotRequested;

    /// <summary>
    /// Every outbound <see cref="SetPropSlot"/> produced by the connection path in this session. This is
    /// diagnostic evidence of the real production path, not another way to request a connection.
    /// </summary>
    public IReadOnlyList<SetPropSlot> SlotRequestsSent
    {
        get { lock (_gate) return _slotRequestsSent.ToArray(); }
    }

    /// <summary>Whether the automatic block pool is on.</summary>
    public bool AutoBlockPoolEnabled { get { lock (_gate) return _poolEnabled; } }

    /// <summary>The five slots as the engine holds them.</summary>
    public IReadOnlyList<ActiveObjectSlot> Slots
    {
        get
        {
            lock (_gate)
                return _slots.Select((s, i) => new ActiveObjectSlot(i, s.FactoryId, s.Type, s.State)).ToArray();
        }
    }

    /// <summary>The factory ids in the pool, by slot; 0 is an empty entry.</summary>
    public IReadOnlyList<uint> PooledFactoryIds { get { lock (_gate) return _runtimePool.Select(p => p.FactoryId).ToArray(); } }

    /// <summary>
    /// <c>BlockFilter::Enable</c> 0x0061B59C, which is what <c>BlockPoolEnabledMessage</c> reaches: stores the
    /// flag and the discovery time, and restarts both the discovery and the reconnect clocks. It leaves the
    /// pool and the last update time alone.
    /// </summary>
    public void EnableAutoBlockPool(bool enabled, float discoveryTimeSeconds = 0f)
    {
        lock (_gate)
        {
            _poolEnabled = enabled;
            _discoveryTime = discoveryTimeSeconds;
            _enableTime = _lastConnectTime = _seconds();
        }
    }

    /// <summary>The advertisement half of <c>HandleActiveObjectAvailable</c>: the caller has already checked the type.</summary>
    public void OnObjectAvailable(uint factoryId, ObjectType type, sbyte rssi)
    {
        lock (_gate)
        {
            var e = _available.TryGet(factoryId, out var found) ? found : Info.Reset();
            e.FactoryId = factoryId;
            e.Type = type;
            e.Rssi = unchecked((byte)rssi);   // ldrb: the engine keeps the byte and compares it unsigned
            e.LastObservedTime = _robotTime;
            _available.Set(factoryId, e);
        }
    }

    /// <summary>
    /// A RobotState arrived: its timestamp becomes the robot clock, as in <c>UpdateFullRobotState</c>, and the
    /// connection part of <c>Robot::Update</c> runs.
    /// </summary>
    public void OnRobotState(uint timestamp)
    {
        List<SetPropSlot> sent;
        lock (_gate)
        {
            _robotTime = timestamp;
            sent = UpdateLocked();
        }
        Raise(sent);
    }

    /// <summary>
    /// The connection half of <c>HandleActiveObjectConnectionState</c>: a slot above 4 is dropped before
    /// anything (0x00533B58), a connection goes to <c>HandleConnectedToObject</c>, a disconnection to
    /// <c>HandleDisconnectedFromObject</c>.
    /// </summary>
    public void OnConnectionState(uint slot, uint factoryId, bool connected)
    {
        if (slot >= SlotCount) return;
        lock (_gate)
        {
            if (connected) HandleConnected((int)slot, factoryId);
            else HandleDisconnected((int)slot, factoryId);
        }
    }

    /// <summary><c>Robot::ConnectToObjects</c> 0x00517150.</summary>
    public void ConnectToObjects(IReadOnlyList<uint> factoryIds)
    {
        lock (_gate) ConnectToObjectsLocked(factoryIds);
    }

    // ------------------------------------------------------------------ Robot

    private List<SetPropSlot> UpdateLocked()
    {
        // Robot::Update 0x00514190: drop what has not been heard for 10001 ms of robot time
        _available.RemoveWhere(e => unchecked((int)(_robotTime - e.LastObservedTime)) >= UndiscoveredAfterMs);
        UpdatePool();                        // 0x0051422A
        CheckDisconnectedObjects();          // 0x00514230
        return ConnectToRequestedObjects();  // 0x00514236
    }

    private void ConnectToObjectsLocked(IReadOnlyList<uint> factoryIds)
    {
        for (int i = 0; i < SlotCount; i++)
            if (factoryIds[i] != _slots[i].FactoryId)   // 0x00517404
                _requested[i] = (factoryIds[i], true);  // 0x00517472, 0x00517476
    }

    private List<SetPropSlot> ConnectToRequestedObjects()
    {
        var sent = new List<SetPropSlot>();
        for (int i = 0; i < SlotCount; i++)
        {
            if (!_requested[i].Pending) continue;
            uint want = _requested[i].FactoryId;
            if (want == _slots[i].FactoryId)                   // 0x00514ADC: already there
            {
                _requested[i] = (0, false);
                continue;
            }
            if (want == 0)                                       // 0x00514BF4: empty the slot
            {
                _slots[i].State = ActiveObjectSlotState.PendingDisconnection;
                _requested[i] = (0, false);
                sent.Add(Send(0, i));
                continue;
            }
            if (!_available.TryGet(want, out var info)) continue;   // 0x00514B00: stays pending until it is heard
            for (int j = 0; j < SlotCount; j++)
            {
                // 0x00514B0A..0x00514B6C: another connected object of the same type. The engine warns, clears
                // the request, and then carries on regardless - it still sends, now with the cleared id, and
                // still records the advertisement in the slot.
                if (_slots[j].State == ActiveObjectSlotState.Connected && _slots[j].Type == info.Type)
                    _requested[i] = (0, false);
            }
            sent.Add(Send(_requested[i].FactoryId, i));          // 0x00514BB4..0x00514BCE
            info.State = ActiveObjectSlotState.PendingConnection;  // 0x00514BDE..0x00514BEE
            _slots[i] = info;
            _requested[i] = (0, false);
        }
        return sent;
    }

    private SetPropSlot Send(uint factoryId, int slot)
    {
        var m = new SetPropSlot { FactoryId = factoryId, Slot = (byte)slot };
        _send(m);
        _slotRequestsSent.Add(m);
        return m;
    }

    private void HandleConnected(int slot, uint factoryId)
    {
        ref var s = ref _slots[slot];
        if (s.FactoryId != factoryId) return;   // "Ignoring connection to object ... because expecting ..."
        // any state other than PendingConnection or Disconnected is logged as an error, and handled the same
        _available.Remove(factoryId);                           // __erase_unique at 0x00517A98
        s.State = ActiveObjectSlotState.Connected;
        s.DisconnectedTime = 0;
    }

    private void HandleDisconnected(int slot, uint factoryId)
    {
        ref var s = ref _slots[slot];
        if (s.FactoryId != factoryId) return;   // "Ignoring disconnection from object ..."
        if (s.State == ActiveObjectSlotState.PendingDisconnection)
            s = Info.Reset();                    // 0x00517C92..0x00517CA6
        else
        {
            s.State = ActiveObjectSlotState.Disconnected;
            s.DisconnectedTime = _seconds();     // 0x00517D08..0x00517D14
        }
    }

    private void CheckDisconnectedObjects()
    {
        double now = _seconds();
        if (_lastDisconnectCheck > 0 && _lastDisconnectCheck + DisconnectResetSeconds > now) return;
        for (int i = 0; i < SlotCount; i++)
            if (_slots[i].State == ActiveObjectSlotState.Disconnected
                && (double)_slots[i].DisconnectedTime + DisconnectResetSeconds < now)
                _slots[i] = Info.Reset();
        _lastDisconnectCheck = now;
    }

    /// <summary><c>Robot::GetClosestDiscoveredObjectsOfType</c> 0x00518314: the lowest RSSI byte of the type, ties to the last visited.</summary>
    private uint GetClosestDiscoveredObjectOfType(ObjectType type, byte maxRssi)
    {
        uint best = 0;
        foreach (var e in _available.Values)
        {
            if (e.Type != type || e.Rssi > maxRssi) continue;
            best = e.FactoryId;
            maxRssi = e.Rssi;
        }
        return best;
    }

    private bool IsConnectedToObject(uint factoryId) => _slots.Any(s => s.FactoryId == factoryId);

    // ------------------------------------------------------------------ BlockFilter

    private void UpdatePool()
    {
        if (!_poolEnabled) return;
        float now = _seconds();
        if (_lastPoolUpdate > 0 && now < _lastPoolUpdate + PoolUpdateIntervalSeconds) return;
        UpdateDiscovering();
        UpdateConnecting();
        _lastPoolUpdate = now;
    }

    private void UpdateDiscovering()
    {
        foreach (var type in PoolTypes)
        {
            if (_persistentPool.Any(p => p.Type == type)) continue;
            uint closest = GetClosestDiscoveredObjectOfType(type, ClosestRssiLimit);
            if (closest != 0) _discovering[type] = closest;
        }
        float now = _seconds();
        if (now < _enableTime + _discoveryTime || _discovering.Count == 0) return;
        foreach (var (type, factoryId) in _discovering) AddObjectToPersistentPool(factoryId, type);
        _discovering.Clear();
        Array.Copy(_persistentPool, _runtimePool, SlotCount);
        ConnectToObjectsLocked(_runtimePool.Select(p => p.FactoryId).ToArray());
        _lastConnectTime = now;
    }

    private void UpdateConnecting()
    {
        float now = _seconds();
        if (now < _lastConnectTime + PoolReconnectDelaySeconds) return;
        for (int i = 0; i < SlotCount; i++)
        {
            var (factoryId, type) = _runtimePool[i];
            if (factoryId == 0 || IsConnectedToObject(factoryId)) continue;
            uint closest = GetClosestDiscoveredObjectOfType(type, ClosestRssiLimit);
            if (closest == 0 || closest == factoryId) continue;
            _runtimePool[i].FactoryId = closest;
            ConnectToObjectsLocked(_runtimePool.Select(p => p.FactoryId).ToArray());
        }
    }

    private bool AddObjectToPersistentPool(uint factoryId, ObjectType type)
    {
        int free = -1;
        for (int i = 0; i < SlotCount; i++)
        {
            if (_persistentPool[i].FactoryId == factoryId || _persistentPool[i].Type == type) return false;
            if (free < 0 && _persistentPool[i].FactoryId == 0) free = i;
        }
        if (free < 0) return false;
        _persistentPool[free] = (factoryId, type);
        Save();                                                  // 0x0061ACE2
        return true;
    }

    // ------------------------------------------------------------------ persistence

    private string _poolPath = "";                                           // BlockFilter+0x60

    /// <summary>The file the pool is kept in, empty when there is none.</summary>
    public string PoolPath { get { lock (_gate) return _poolPath; } }

    /// <summary>The persistent pool, by entry.</summary>
    public IReadOnlyList<(uint FactoryId, ObjectType Type)> PersistentPool { get { lock (_gate) return _persistentPool.ToArray(); } }

    /// <summary>
    /// <c>BlockFilter::Init</c> 0x0061A1EC, which <c>Robot::SetPhysicalRobot(true)</c> 0x00513914 calls with
    /// <c>DataPlatform::pathToResource(scope 4, "blockPool.txt")</c>: stores the path, loads the pool, copies it to
    /// the runtime pool and asks for its five factory ids at once (<c>Robot::ConnectToObjects</c> at 0x0061A278) -
    /// before any <c>BlockPoolEnabledMessage</c>.
    /// </summary>
    public void Init(string path)
    {
        lock (_gate)
        {
            _poolPath = path;
            Load();
            Array.Copy(_persistentPool, _runtimePool, SlotCount);
            ConnectToObjectsLocked(_runtimePool.Select(p => p.FactoryId).ToArray());
        }
    }

    /// <summary>
    /// <c>BlockFilter::Load</c> 0x0061A2DC. The file is read a line at a time (getline on a line feed); a file
    /// that cannot be opened leaves the pool as it is. Empty lines are skipped; a sixth non-empty line is an error
    /// that ends the reading (0x0061A41E, 0x0061A5A0); a line not starting "0x" is skipped (compare(0, 2, "0x") at
    /// 0x0061A430); the factory id is stoul(line, &amp;pos, 16), the line is skipped when pos is at its end, the id
    /// is stored in the next entry, and the type is stoul(line.substr(pos + 1), 0, 10) into the same entry, which
    /// then counts. A parse that throws is logged and the line skipped (0x0061A52C..0x0061A58E) - after the id has
    /// been stored, when it is the type that fails.
    /// </summary>
    private void Load()
    {
        string text;
        try { text = File.ReadAllText(_poolPath); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { return; }

        int count = 0;
        int start = 0;
        while (start < text.Length)
        {
            int nl = text.IndexOf('\n', start);
            string line = nl < 0 ? text[start..] : text[start..nl];
            start = nl < 0 ? text.Length : nl + 1;
            if (line.Length == 0) continue;
            if (count >= SlotCount) break;
            if (!line.StartsWith("0x", StringComparison.Ordinal)) continue;
            if (!Stoul(line, 16, out uint factoryId, out int pos)) continue;
            if (pos >= line.Length) continue;
            _persistentPool[count].FactoryId = factoryId;
            if (!Stoul(line[(pos + 1)..], 10, out uint type, out _)) continue;
            _persistentPool[count].Type = (ObjectType)(int)type;
            count++;
        }
    }

    /// <summary>
    /// std::stoul with a 32-bit unsigned long (strtoul): leading white space, an optional sign, an optional 0x in
    /// base 16, then as many digits as parse. No digit is invalid_argument and more than 32 bits is out_of_range;
    /// both come back false, as the caller catches them.
    /// </summary>
    internal static bool Stoul(string s, int radix, out uint value, out int pos)
    {
        value = 0; pos = 0;
        int i = 0;
        while (i < s.Length && (s[i] == ' ' || (s[i] >= '\t' && s[i] <= '\r'))) i++;
        bool negative = false;
        if (i < s.Length && (s[i] == '+' || s[i] == '-')) { negative = s[i] == '-'; i++; }

        if (radix == 16 && i + 2 < s.Length && s[i] == '0' && (s[i + 1] == 'x' || s[i + 1] == 'X') && Uri.IsHexDigit(s[i + 2]))
            i += 2;
        ulong acc = 0;
        int first = i;
        bool overflow = false;
        for (; i < s.Length; i++)
        {
            char c = s[i];
            int d = c >= '0' && c <= '9' ? c - '0'
                  : radix == 16 && c >= 'a' && c <= 'f' ? c - 'a' + 10
                  : radix == 16 && c >= 'A' && c <= 'F' ? c - 'A' + 10 : -1;
            if (d < 0) break;
            if (!overflow) acc = acc * (ulong)radix + (ulong)d;
            if (acc > uint.MaxValue) overflow = true;
        }
        if (i == first) return false;
        if (overflow) return false;
        value = negative ? unchecked((uint)-(long)acc) : (uint)acc;
        pos = i;
        return true;
    }

    /// <summary>
    /// <c>BlockFilter::Save</c> 0x0061B014: nothing without a path; when every entry is empty an existing file is
    /// removed first (stat and remove at 0x0061B058..0x0061B068); then the file is opened for output, truncating
    /// it, and each entry with a non-zero factory id is written as "0x", the id in lower-case hex, ",", the type in
    /// decimal and a line feed (0x0061B0EA..0x0061B154).
    /// </summary>
    private void Save()
    {
        if (_poolPath.Length == 0) return;
        try
        {
            if (_persistentPool.All(p => p.FactoryId == 0) && File.Exists(_poolPath)) File.Delete(_poolPath);
            var sb = new System.Text.StringBuilder();
            foreach (var (factoryId, type) in _persistentPool)
                if (factoryId != 0) sb.Append("0x").Append(factoryId.ToString("x")).Append(',').Append(((int)type).ToString()).Append('\n');
            var dir = Path.GetDirectoryName(_poolPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(_poolPath, sb.ToString());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }


    private void Raise(List<SetPropSlot> sent)
    {
        foreach (var m in sent)
            EventFan.Raise(SlotRequested, m, null);
    }
}
