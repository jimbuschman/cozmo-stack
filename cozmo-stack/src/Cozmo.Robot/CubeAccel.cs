using Cozmo.Protocol;

namespace Cozmo.Robot;

/// <summary>
/// A one-pole high-pass filter over a cube's acceleration, one coefficient per axis.
///
/// <c>CubeAccelListeners::HighPassFilterListener::UpdateInternal</c> (0x00636598), axis by axis:
/// <c>y = a * (y + x - xPrevious)</c>, and then <c>xPrevious = x</c>. What comes out is what changed,
/// which is what "shaking" means for an accelerometer that also reads gravity.
/// </summary>
public sealed class CubeHighPassFilter
{
    private readonly float _ax, _ay, _az;
    private float _px, _py, _pz;
    private float _x, _y, _z;

    public CubeHighPassFilter(float coefficient) : this(coefficient, coefficient, coefficient) { }

    public CubeHighPassFilter(float ax, float ay, float az) { _ax = ax; _ay = ay; _az = az; }

    /// <summary>The filtered value after the last sample.</summary>
    public (float X, float Y, float Z) Value => (_x, _y, _z);

    public (float X, float Y, float Z) Update(float x, float y, float z)
    {
        _x = _ax * (_x + x - _px);
        _y = _ay * (_y + y - _py);
        _z = _az * (_z + z - _pz);
        _px = x; _py = y; _pz = z;
        return (_x, _y, _z);
    }
}

/// <summary>
/// The engine's shake detector for one cube, as <c>CubeAccelListeners::ShakeListener</c> builds and runs
/// it (constructor 0x00636620, <c>UpdateInternal</c> 0x0063679E).
///
/// The raw acceleration goes through a <see cref="CubeHighPassFilter"/>, and the listener works on the
/// <b>squared</b> magnitude of what comes out: <c>x² + y² + z²</c>, which is what the two thresholds are
/// compared against (the constructor squares them, at 0x0063665A) and what the callback is handed
/// (0x0063680C passes the same register it compared). The two thresholds are hysteresis: while the cube is
/// not already shaking the higher one has to be crossed, and once it is, the lower one holds it there.
/// The callback fires on every sample while the cube counts as shaking, and not at all otherwise.
///
/// <c>BehaviorSinging::InitInternal</c> (0x005EECF4..0x005EED08) builds one of these per connected cube
/// with <b>0.5, 2.5 and 3.9</b>.
/// </summary>
public sealed class CubeShakeListener
{
    private readonly CubeHighPassFilter _filter;
    private readonly float _lowSquared, _highSquared;
    private readonly Action<float> _onShake;
    private bool _shaking;

    /// <summary>The values BehaviorSinging passes: filter coefficient, stop threshold, start threshold.</summary>
    public const float SingingFilterCoefficient = 0.5f, SingingLowThreshold = 2.5f, SingingHighThreshold = 3.9f;

    public CubeShakeListener(float filterCoefficient, float lowThreshold, float highThreshold, Action<float> onShake)
    {
        _filter = new CubeHighPassFilter(filterCoefficient);
        _lowSquared = lowThreshold * lowThreshold;
        _highSquared = highThreshold * highThreshold;
        _onShake = onShake;
    }

    /// <summary>Whether the cube counts as shaking after the last sample.</summary>
    public bool IsShaking => _shaking;

    /// <summary>The squared magnitude of the filtered acceleration after the last sample.</summary>
    public float LastMagnitudeSquared { get; private set; }

    public void Update(float x, float y, float z)
    {
        var (fx, fy, fz) = _filter.Update(x, y, z);
        float magnitudeSquared = fx * fx + fy * fy + fz * fz;
        LastMagnitudeSquared = magnitudeSquared;
        float threshold = _shaking ? _lowSquared : _highSquared;
        _shaking = magnitudeSquared > threshold;
        if (_shaking) _onShake(magnitudeSquared);
    }
}

/// <summary>
/// Cube accelerometer streams and the listeners on them, as <c>CubeAccelComponent</c> runs them.
///
/// Adding the first listener for a cube turns its stream on: <c>CubeAccelComponent::AddListener</c>
/// (0x0063547E) looks the object up in BlockWorld and sends <c>StreamObjectAccel</c> to the robot
/// (0x00635562). Removing the last one turns it off again, which keeps a stream the engine would have
/// running off the wire when nothing wants it.
///
/// An <c>ObjectAccel</c> message (0xF5, twenty bytes: timestamp, object id, three floats) is fed to every
/// listener on that object, in the order they were added.
/// </summary>
public sealed class CubeAccelStreams
{
    private readonly CozmoRobot _robot;
    private readonly object _gate = new();
    private readonly Dictionary<uint, List<CubeShakeListener>> _listeners = new();

    internal CubeAccelStreams(CozmoRobot robot) => _robot = robot;

    /// <summary>The object ids whose accelerometer stream is on because something is listening.</summary>
    public IReadOnlyCollection<uint> Streaming { get { lock (_gate) return _listeners.Keys.ToList(); } }

    /// <summary>Adds a listener, turning the cube's stream on if it is the first.</summary>
    public void AddListener(uint objectId, CubeShakeListener listener)
    {
        bool first;
        lock (_gate)
        {
            if (!_listeners.TryGetValue(objectId, out var list))
                _listeners[objectId] = list = new List<CubeShakeListener>();
            first = list.Count == 0;
            list.Add(listener);
        }
        if (first) _robot.SendMessage(new StreamObjectAccel { ObjectID = objectId, Enable = true }, flush: true);
    }

    /// <summary>Removes a listener, turning the cube's stream off when it was the last.</summary>
    public void RemoveListener(uint objectId, CubeShakeListener listener)
    {
        bool last = false;
        lock (_gate)
        {
            if (_listeners.TryGetValue(objectId, out var list) && list.Remove(listener) && list.Count == 0)
            {
                _listeners.Remove(objectId);
                last = true;
            }
        }
        if (last) _robot.SendMessage(new StreamObjectAccel { ObjectID = objectId, Enable = false }, flush: true);
    }

    // fidelity: M1-025, M1-015
    /// <summary>
    /// Back to the state right after construction, for a removed robot (CB33, CC26, CC27): no listener, so no stream
    /// counts as on. Nothing is sent: the robot is gone.
    /// </summary>
    internal void ResetToConstructed()
    {
        lock (_gate) _listeners.Clear();
    }

    /// <summary>Fed every robot message by <see cref="CozmoRobot"/>.</summary>
    internal void Handle(RobotMessage m)
    {
        if (m is not ObjectAccel a) return;
        CubeShakeListener[] listeners;
        lock (_gate)
        {
            if (!_listeners.TryGetValue(a.ObjectID, out var list) || list.Count == 0) return;
            listeners = list.ToArray();
        }
        foreach (var l in listeners) l.Update(a.Accel.X, a.Accel.Y, a.Accel.Z);
    }
}
