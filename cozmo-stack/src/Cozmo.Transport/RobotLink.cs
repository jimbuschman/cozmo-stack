using System.Net;
using Cozmo.Protocol;

namespace Cozmo.Transport;

/// <summary>
/// Typed facade over <see cref="ReliableTransport"/>: decodes CLAD robot messages, tracks identity/telemetry and
/// performs the minimal engine-style handshake. This is the seam the future Cozmo.Robot layer builds on.
///
/// Handshake (what the official engine and PyCozmo both do after the transport connects):
///  robot -> RobotAvailable(0xC9), FirmwareVersion(0xEE) arrive on their own;
///  engine -> GetManufacturingInfo(0x25) → robot MfgId(0xED);
///  engine -> SyncTime(0x4B) → robot SyncTimeAck(0xC2) and RobotState(0xF0) streaming begins (~30 Hz).
/// </summary>
public sealed class RobotLink : IDisposable
{
    public ReliableTransport Transport { get; }
    public RobotAvailable? Available { get; private set; }
    public FirmwareVersion? Firmware { get; private set; }
    public ManufacturingID? Manufacturing { get; private set; }
    public RobotState? LastState { get; private set; }
    public int StateCount { get; private set; }
    public int MessageCount { get; private set; }
    public DateTime? FirstStateUtc { get; private set; }
    public DateTime? LastStateUtc { get; private set; }
    public bool SyncTimeAcked { get; private set; }

    /// <summary>
    /// Message counts. Written on the transport's dispatch thread, so callers must not enumerate it
    /// directly; use <see cref="HistogramSnapshot"/>.
    /// </summary>
    private readonly Dictionary<RobotMessageId, int> _histogram = new();
    private readonly object _gate = new();

    /// <summary>A copy of the message counts, safe to enumerate while the robot is still talking.</summary>
    public Dictionary<RobotMessageId, int> HistogramSnapshot()
    {
        lock (_gate) return new Dictionary<RobotMessageId, int>(_histogram);
    }

    /// <summary>How many of this message have arrived.</summary>
    public int CountOf(RobotMessageId id)
    {
        lock (_gate) return _histogram.GetValueOrDefault(id);
    }

    public event Action<RobotMessage>? Message;
    public event Action<RobotState>? State;

    public RobotLink(TransportOptions? options = null) : this(new ReliableTransport(options)) { }

    /// <summary>Test seam: a link over a given transport, such as an offline one.</summary>
    internal RobotLink(ReliableTransport transport)
    {
        Transport = transport;
        Transport.DataReceived += OnData;
    }

    private int _handlerFaults;

    /// <summary>Subscribers to <see cref="State"/> or <see cref="Message"/> that threw, counted.</summary>
    public int HandlerFaults => Volatile.Read(ref _handlerFaults);

    /// <summary>Raised with what a <see cref="State"/> or <see cref="Message"/> subscriber threw.</summary>
    public event Action<Exception>? HandlerFaulted;

    public Task ConnectAsync(IPAddress robot, int? port = null, TimeSpan? timeout = null)
    {
        var tcs = new TaskCompletionSource();
        void ok() { Transport.Connected -= ok; tcs.TrySetResult(); }
        void bad(string r) { Transport.Disconnected -= bad; tcs.TrySetException(new IOException($"disconnected while connecting: {r}")); }
        Transport.Connected += ok; Transport.Disconnected += bad;
        Transport.Connect(robot, port);
        var t = timeout ?? TimeSpan.FromSeconds(5);
        return Task.WhenAny(tcs.Task, Task.Delay(t)).ContinueWith(w =>
        {
            Transport.Connected -= ok; Transport.Disconnected -= bad;
            if (!tcs.Task.IsCompleted) throw new TimeoutException($"no ConnectionResponse from {robot}:{port ?? Transport.Peer?.Port} within {t.TotalSeconds:F1}s");
            tcs.Task.GetAwaiter().GetResult();
        });
    }

    /// <summary>Request identity and start telemetry (GetManufacturingInfo + SyncTime), optionally an origin reset like PyCozmo.</summary>
    public void BeginSession(bool sendOriginLikePyCozmo = false)
    {
        Transport.Send(new GetManufacturingInfo(), flush: true);
        if (sendOriginLikePyCozmo) Transport.Send(new AbsoluteLocalizationUpdate { PoseOriginId = 1, PoseAngleRad = -0.0f });   // pycozmo's 0x80000000 is -0.0 as the angle
        Transport.Send(new SyncTime(0, 0), flush: true);
    }

    public void SetHeadAngle(float rad, float speed = 10f, float accel = 10f, float duration = 0f, byte actionId = 1)
        => Transport.Send(new SetHeadAngle(rad, speed, accel, duration, actionId), flush: true);
    public void SetBackpackLights(LightState top, LightState middle, LightState bottom)
        => Transport.Send(new BackpackLightsMiddle(top, middle, bottom), flush: true);
    public void SetHeadlight(bool on) => Transport.Send(new SetHeadlight(on), flush: true);
    public void StopAllMotors() => Transport.Send(new StopAllMotors(), flush: true);
    public void Disconnect() => Transport.Disconnect();
    public void Dispose() => Transport.Dispose();

    private void OnData(byte[] clad)
    {
        RobotMessage m;
        try { m = RobotMessage.Parse(clad); } catch (FormatException) { return; }
        RobotState? newState = null;
        lock (_gate)
        {
            MessageCount++;
            _histogram[m.Id] = _histogram.GetValueOrDefault(m.Id) + 1;
            switch (m)
            {
                case RobotAvailable a: Available = a; break;
                case FirmwareVersion f: Firmware = f; break;
                case ManufacturingID i: Manufacturing = i; break;
                case SyncTimeAck: SyncTimeAcked = true; break;
                case RobotState s:
                    LastState = s; StateCount++; LastStateUtc = DateTime.UtcNow; FirstStateUtc ??= LastStateUtc;
                    newState = s; break;
            }
        }
        // fidelity: M1-034
        // Handler isolation (policy D6): each subscriber is called on its own, so one that throws neither
        // stops the subscribers after it nor keeps Message from being raised.
        if (newState is not null) Fan(State, newState);
        Fan(Message, m);
    }

    private void Fan<T>(Action<T>? handler, T arg)
    {
        if (handler is null) return;
        foreach (var target in handler.GetInvocationList())
        {
            try { ((Action<T>)target)(arg); }
            catch (Exception e)
            {
                Interlocked.Increment(ref _handlerFaults);
                try { HandlerFaulted?.Invoke(e); } catch { }
            }
        }
    }

    public double StateRateHz => StateCount > 1 && FirstStateUtc is { } f && LastStateUtc is { } l && l > f
        ? (StateCount - 1) / (l - f).TotalSeconds : 0;
}
