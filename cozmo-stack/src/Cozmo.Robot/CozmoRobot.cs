using System.Net;
using Cozmo.Protocol;
using Cozmo.Robot.Animation;
using Cozmo.Transport;

namespace Cozmo.Robot;

/// <summary>Everything the robot reports about itself, kept current from the telemetry stream.</summary>
public sealed class RobotStateTracker
{
    public RobotAvailable? Available { get; private set; }
    public FirmwareVersion? Firmware { get; private set; }
    public ManufacturingID? Manufacturing { get; private set; }
    public RobotState? Latest { get; private set; }
    public AnimationState? Animation { get; private set; }
    public bool TimeSynced { get; private set; }
    /// <summary>True once the robot has answered the animation-controller init with its own state stream.</summary>
    public bool AnimationsEnabled => Animation is not null;

    public int StateCount { get; private set; }
    public DateTime? FirstStateUtc { get; private set; }
    public DateTime? LastStateUtc { get; private set; }

    /// <summary>Written on the transport's dispatch thread; read it through the members below.</summary>
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

    /// <summary>How many distinct message types have arrived.</summary>
    public int DistinctMessageTypes { get { lock (_gate) return _histogram.Count; } }

    /// <summary>The robot recalibrates head and lift on every connect; motors should wait for this.</summary>
    public bool CalibratingMotors => HeadCalibrating || LiftCalibrating;

    /// <summary>Whether any MotorCalibration message has arrived at all.</summary>
    public bool CalibrationSeen { get; private set; }

    /// <summary>Whether the head is calibrating right now.</summary>
    public bool HeadCalibrating { get; private set; }
    /// <summary>Whether the lift is calibrating right now.</summary>
    public bool LiftCalibrating { get; private set; }

    /// <summary>Whether the head has been seen to start and then finish calibrating.</summary>
    public bool HeadCalibrated { get; private set; }
    /// <summary>Whether the lift has been seen to start and then finish calibrating.</summary>
    public bool LiftCalibrated { get; private set; }

    /// <summary>
    /// Whether both motors have completed calibration.
    ///
    /// Head and lift are tracked apart because the robot calibrates them separately and reports each with
    /// its own <c>MotorID</c>. Collapsing both into one flag meant the lift finishing cleared it while the
    /// head was still moving, so motion was allowed too early. It also means an arriving RobotState is not
    /// on its own evidence of readiness: the calibration messages are.
    /// </summary>
    public bool CalibrationComplete => HeadCalibrated && LiftCalibrated && !CalibratingMotors;

    public event Action<RobotState>? StateUpdated;

    public uint? SerialNumber => Available?.SerialNumberHead;
    public int? FirmwareVersionNumber => Firmware?.Version;
    public float? HeadAngleRad => Latest?.HeadAngle;
    /// <summary>The lift arm angle in radians, as the robot reports it (see <see cref="RobotState.LiftAngleRad"/>).</summary>
    public float? LiftAngleRad => Latest?.LiftAngle;
    /// <summary>The lift height in millimetres, converted as the engine's <c>Robot::GetLiftHeight</c> converts it.</summary>
    public float? LiftHeightMm => Latest?.LiftHeightMm;
    public float? BatteryVolts => Latest?.BatteryVoltage;
    public bool OnCharger => Latest?.Has(RobotStatusFlag.IsOnCharger) ?? false;
    public bool Charging => Latest?.Has(RobotStatusFlag.IsCharging) ?? false;
    public bool PickedUp => Latest?.Has(RobotStatusFlag.IsPickedUp) ?? false;
    public bool CliffDetected => Latest?.Has(RobotStatusFlag.CliffDetected) ?? false;
    public double StateRateHz => StateCount > 1 && FirstStateUtc is { } f && LastStateUtc is { } l && l > f
        ? (StateCount - 1) / (l - f).TotalSeconds : 0;

    public void Handle(RobotMessage m)
    {
        RobotState? newState = null;
        lock (_gate)
        {
        _histogram[m.Id] = _histogram.GetValueOrDefault(m.Id) + 1;
        switch (m)
        {
            case RobotAvailable a: Available = a; break;
            case FirmwareVersion f: Firmware = f; break;
            case ManufacturingID i: Manufacturing = i; break;
            case SyncTimeAck: TimeSynced = true; break;
            case AnimationState a: Animation = a; break;
            case MotorCalibration c:
                CalibrationSeen = true;
                switch (c.MotorID)
                {
                    case MotorID.MOTOR_HEAD:
                        // A motor that starts calibrating is no longer calibrated: CalibrationComplete must not
                        // stay true while the head or lift is being re-zeroed, or normal motion would be accepted
                        // in the middle of it.
                        HeadCalibrating = c.CalibStarted;
                        if (c.CalibStarted) HeadCalibrated = false; else HeadCalibrated = true;
                        break;
                    case MotorID.MOTOR_LIFT:
                        LiftCalibrating = c.CalibStarted;
                        if (c.CalibStarted) LiftCalibrated = false; else LiftCalibrated = true;
                        break;
                }
                break;
            case RobotState s:
                Latest = s; StateCount++;
                LastStateUtc = DateTime.UtcNow; FirstStateUtc ??= LastStateUtc;
                newState = s;
                break;
        }
        }
        // The public event goes out one subscriber at a time. A plain multicast invoke runs the whole
        // list as a single call, so a subscriber that throws takes the rest of the list with it - and,
        // because this handler runs first in the robot's routing, the exception would escape into
        // CozmoRobot.OnData and leave the camera, the sensors, the cubes and every Message subscriber
        // without the message at all. A public subscriber must not be able to do that.
        if (newState is not null) EventFan.Raise(StateUpdated, newState, HandlerFaulted);
    }

    /// <summary>
    /// Raised when a subscriber to one of this tracker's events threw, with what it threw. The fault is
    /// contained rather than hidden: the internal routing carries on, and this says that it did.
    /// </summary>
    public event Action<Exception>? HandlerFaulted;
}

/// <summary>
/// Raising an event without letting one subscriber decide for the others.
///
/// The transport already does this for its own events (<c>ReliableTransport.Fan</c>); the device layer
/// needs it for the same reason. Every target is invoked separately, a fault is reported against that
/// target alone, and the rest of the list still runs.
/// </summary>
internal static class EventFan
{
    public static void Raise<T>(Action<T>? handler, T arg, Action<Exception>? faulted)
    {
        if (handler is null) return;
        foreach (var t in handler.GetInvocationList())
        {
            try { ((Action<T>)t)(arg); }
            catch (Exception e) { try { faulted?.Invoke(e); } catch { } }
        }
    }
}

/// <summary>
/// A connected Cozmo, exposed as devices rather than protocol messages.
///
/// <code>
/// using var robot = await CozmoRobot.ConnectAsync(IPAddress.Parse("172.31.1.1"));
/// robot.Camera.FrameReceived += f =&gt; f.Save($"frame{f.ImageId}.jpg");
/// robot.StartCamera();
/// robot.Display.Show(FaceBitmap.TestPattern());
/// robot.Audio.PlayTone(440, TimeSpan.FromSeconds(1));
/// </code>
///
/// This is the device layer only: it turns the verified protocol into camera frames, face images,
/// audio and robot state. Actions, animations, navigation and behaviours belong above it.
/// </summary>
public sealed class CozmoRobot : IDisposable
{
    public ReliableTransport Transport { get; }
    public CozmoCamera Camera { get; } = new();
    public CozmoDisplay Display { get; }
    public CozmoAudio Audio { get; }
    public RobotStateTracker State { get; } = new();
    /// <summary>Wheels, head and lift.</summary>
    public CozmoMotion Motion { get; }
    /// <summary>Backpack LEDs and the infrared headlight.</summary>
    public CozmoLights Lights { get; }
    /// <summary>Everything the robot reports about itself: power, motion, IMU and cliffs.</summary>
    public CozmoSensors Sensors { get; }
    /// <summary>Light cube discovery, connection state and basic telemetry.</summary>
    public CozmoCubes Cubes { get; }
    /// <summary>Cube accelerometer streams and the shake listeners on them.</summary>
    public CubeAccelStreams CubeAccel { get; }
    /// <summary>Cozmo's own animation clips and groups, on one deterministic timeline.</summary>
    public CozmoAnimations Animations { get; }
    /// <summary>The procedural face: nineteen parameters per eye, and named expressions.</summary>
    public CozmoFace Face { get; }

    /// <summary>Every decoded robot message, after the devices have seen it.</summary>
    public event Action<RobotMessage>? Message;

    private CozmoRobot(TransportOptions? options, ReliableTransport? transport = null)
    {
        Transport = transport ?? new ReliableTransport(options);
        Display = new CozmoDisplay(m => Transport.Send(m, flush: true),
                                   Transport.Options.MaxFramePayloadBytes - CozmoDisplay.MessageOverhead);
        Audio = new CozmoAudio(m => Transport.Send(m, flush: true));
        // The engine fills every animation tick with both an audio frame and a face keyframe. Mirror that
        // in both directions, so neither pipeline leaves the robot's animation tick half empty.
        Display.BeforeFrame = () => { if (!Audio.Busy) Transport.Send(new AudioSilence(), flush: true); };
        Audio.PlayedFrames = () => State.Animation?.NumAudioFramesPlayed ?? 0;
        Audio.OnFrameSent += () =>
            Transport.Send(new Protocol.FaceImage { Image = Display.LastPayload ?? BlankFace }, flush: true);
        Motion = new CozmoMotion(this);
        Lights = new CozmoLights(this);
        Sensors = new CozmoSensors(this, State);
        Cubes = new CozmoCubes(this);
        CubeAccel = new CubeAccelStreams(this);
        Animations = new CozmoAnimations(this);
        Face = new CozmoFace(this);
        Transport.DataReceived += OnData;
    }

    /// <summary>
    /// A robot with no socket, driven by feeding datagrams to <see cref="ReliableTransport.ProcessIncoming"/>.
    /// Used by the replay tool and by tests, so the whole device layer can be exercised against captured
    /// traffic without a robot present. Outgoing messages land in the transport's offline frame list.
    /// </summary>
    public static CozmoRobot CreateOffline(TransportOptions? options = null, INetClock? clock = null)
    {
        var robot = new CozmoRobot(options, ReliableTransport.CreateOffline(options, clock));
        robot.Transport.OfflineConnect();
        return robot;
    }

    // fidelity: M1-001
    /// <summary>
    /// Connects, completes the engine's handshake and waits for telemetry to start. With no
    /// <paramref name="address"/> the IP is the one Unity uses (B1): 172.31.1.1, or 127.0.0.1 when
    /// <paramref name="isSimulated"/>. The remote port is 5552 when simulated, else 5551 (B3).
    /// </summary>
    public static async Task<CozmoRobot> ConnectAsync(IPAddress? address = null, bool isSimulated = false,
                                                      TransportOptions? options = null,
                                                      TimeSpan? timeout = null,
                                                      bool enableAnimations = true,
                                                      string? blockPoolPath = null)
    {
        address ??= RobotAddress.DefaultFor(isSimulated);
        var robot = new CozmoRobot(options);
        var connected = new TaskCompletionSource();
        void ok() => connected.TrySetResult();
        void bad(string r) => connected.TrySetException(new IOException($"disconnected while connecting: {r}"));
        robot.Transport.Connected += ok;
        robot.Transport.Disconnected += bad;
        try
        {
            // fidelity: M1-019
            // R39 / R38: Start opens the socket (once), Connect only queues the ConnectionRequest; both are posted, in order.
            robot.Transport.Start();
            robot.Transport.Connect(address, isSimulated);
            var t = timeout ?? TimeSpan.FromSeconds(5);
            if (await Task.WhenAny(connected.Task, Task.Delay(t)) != connected.Task)
                throw new TimeoutException($"no ConnectionResponse from {address} within {t.TotalSeconds:F1}s");
            await connected.Task;
        }
        finally
        {
            robot.Transport.Connected -= ok;
            robot.Transport.Disconnected -= bad;
        }

        // identity arrives unprompted; ask for the rest and start telemetry
        robot.Transport.Send(new GetManufacturingInfo(), flush: true);
        robot.Transport.Send(new SyncTime(0), flush: true);
        // Without this the robot accepts face and audio frames but never renders or plays them: the
        // animation controller is not running. The robot answers by streaming AnimationState (0xF1).
        if (enableAnimations) robot.EnableAnimations();
        // Robot::SetPhysicalRobot(true) 0x00513914 initialises the block pool from blockPool.txt, which asks for
        // the saved cubes straight away. The engine resolves the file with DataPlatform::pathToResource(scope 4);
        // this stack keeps it under the local application data folder. An empty path turns persistence off.
        robot.Cubes.Connections.Init(blockPoolPath ?? DefaultBlockPoolPath);
        // ConnectionFlowController.CubeConnectFlow enables the app's BlockPoolTracker immediately after
        // the robot connection is established, with a discovery time of zero. This is the application-side
        // lifecycle step that lets a fresh install select newly advertising cubes; Init above only restores
        // the persistent pool and cannot discover a cube that has never been saved.
        robot.Cubes.EnableAutoBlockPool(enabled: true, discoveryTimeSeconds: 0f);
        for (int i = 0; i < 40 && robot.State.StateCount == 0; i++) await Task.Delay(50);
        return robot;
    }

    /// <summary>Where the block pool is kept between sessions unless ConnectAsync is told otherwise.</summary>
    public static string DefaultBlockPoolPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "cozmo-stack", "blockPool.txt");

    /// <summary>
    /// Starts the robot's animation controller. Face images and audio frames are animation keyframes: until
    /// this is sent the robot receives them and does nothing visible. It answers by streaming AnimationState.
    /// </summary>
    public void EnableAnimations() => Transport.Send(new InitController(), flush: true);

    /// <summary>Stops whatever animation is playing and clears the robot's keyframe buffer.</summary>
    public void EndAnimation() => Transport.Send(new EndOfAnimation(), flush: true);

    /// <summary>Waits until the robot confirms the animation controller is running.</summary>
    public async Task<bool> WaitForAnimationsAsync(TimeSpan? timeout = null)
    {
        var end = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(3));
        while (DateTime.UtcNow < end)
        {
            if (State.AnimationsEnabled) return true;
            await Task.Delay(25);
        }
        return false;
    }

    /// <summary>
    /// Waits until the robot is ready to be driven: telemetry flowing, the animation controller running and
    /// the head and lift calibration it performs on every connect finished. Returns false if any of that
    /// does not happen in time, with <paramref name="why"/> saying which.
    ///
    /// Motion commands refuse to run before this completes, so a program that drives the robot should await
    /// it once after connecting rather than sleeping and hoping.
    /// </summary>
    public async Task<bool> WaitUntilReadyAsync(TimeSpan? timeout = null)
    {
        var (ok, _) = await WaitUntilReadyAsync(timeout, describe: true);
        return ok;
    }

    /// <summary>As <see cref="WaitUntilReadyAsync(TimeSpan?)"/>, but also says what it was still waiting for.</summary>
    public async Task<(bool Ready, string Detail)> WaitUntilReadyAsync(TimeSpan? timeout, bool describe)
    {
        _ = describe;
        var end = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(15));
        while (DateTime.UtcNow < end)
        {
            bool telemetry = State.StateCount > 0;
            bool anim = State.AnimationsEnabled;
            bool calibrated = State.CalibrationComplete;
            if (telemetry && anim && calibrated) return (true, "telemetry, animation controller and motor calibration all ready");
            await Task.Delay(50);
        }
        var missing = new List<string>();
        if (State.StateCount == 0) missing.Add("no telemetry");
        if (!State.AnimationsEnabled) missing.Add("animation controller not running");
        if (!State.CalibrationSeen) missing.Add("motor calibration never reported");
        else if (State.CalibratingMotors)
            missing.Add($"motor calibration still running ({(State.HeadCalibrating ? "head" : "")}" +
                        $"{(State.HeadCalibrating && State.LiftCalibrating ? " and " : "")}" +
                        $"{(State.LiftCalibrating ? "lift" : "")})");
        else if (!State.CalibrationComplete)
            missing.Add($"waiting for calibration to complete (head {(State.HeadCalibrated ? "done" : "pending")}, " +
                        $"lift {(State.LiftCalibrated ? "done" : "pending")})");
        return (false, missing.Count == 0 ? "timed out" : string.Join("; ", missing));
    }

    /// <summary>Waits for the head and lift calibration the robot runs on connect, so motor commands are not fought.</summary>
    public async Task WaitForMotorCalibrationAsync(TimeSpan? timeout = null)
    {
        var end = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(8));
        while (DateTime.UtcNow < end)
        {
            if (State.CalibrationComplete) return;
            await Task.Delay(50);
        }
    }

    /// <summary>Starts the camera. Grayscale QVGA by default, which is what the engine uses.</summary>
    public void StartCamera(bool color = false, bool singleShot = false)
    {
        Camera.Restart();
        Transport.Send(new EnableColorImages { Enable = color }, flush: true);
        Transport.Send(new ImageRequest { Mode = singleShot ? ImageSendMode.SingleShot : ImageSendMode.Stream }, flush: true);
    }

    public void StopCamera() => Transport.Send(new ImageRequest { Mode = ImageSendMode.Off }, flush: true);

    /// <summary>
    /// Waits for the next usable camera frame. Frames from the sensor's warm-up are torn and are skipped
    /// unless <paramref name="includeWarmUp"/> says otherwise, so this can take about half a second longer
    /// than the frame interval right after the camera starts.
    /// </summary>
    public async Task<CameraFrame> NextFrameAsync(TimeSpan? timeout = null, bool includeWarmUp = false)
    {
        var tcs = new TaskCompletionSource<CameraFrame>();
        void handler(CameraFrame f) { if (includeWarmUp || !f.IsWarmUp) tcs.TrySetResult(f); }
        Camera.FrameReceived += handler;
        try
        {
            var t = timeout ?? TimeSpan.FromSeconds(5);
            if (await Task.WhenAny(tcs.Task, Task.Delay(t)) != tcs.Task)
                throw new TimeoutException($"no usable camera frame within {t.TotalSeconds:F1}s");
            return await tcs.Task;
        }
        finally { Camera.FrameReceived -= handler; }
    }

    /// <summary>Sends a head angle without waiting for the acknowledgement. Prefer <see cref="Motion"/>.</summary>
    public void SetHeadAngle(float radians, byte actionId = 1)
        => Transport.Send(new SetHeadAngle(radians, actionId: actionId), flush: true);

    /// <summary>Sets the backpack from raw light states. Prefer <see cref="Lights"/>.</summary>
    public void SetBackpackLights(LightState top, LightState middle, LightState bottom)
        => Transport.Send(new BackpackLightsMiddle(top, middle, bottom), flush: true);

    /// <summary>Prefer <see cref="Lights"/>.</summary>
    public void SetHeadlight(bool on) => Lights.SetHeadlight(on);

    /// <summary>
    /// Stops every motor immediately. Safe to call at any time, including before the robot is ready, and
    /// does not wait for confirmation; <see cref="CozmoMotion.StopAllAsync"/> is the checked version.
    /// </summary>
    public void EmergencyStop()
    {
        Transport.Send(new StopAllMotors(), flush: true);
        Transport.Send(new DriveWheels(0f, 0f, 0f, 0f), flush: true);
    }

    public void Disconnect() => Transport.Disconnect();

    /// <summary>
    /// Stops the motors, then disposes the transport. The stop commands are queued like every robot message;
    /// the transport's dispose then makes its one DisconnectRequest send attempt and closes the socket
    /// without waiting for anything queued to drain (see <see cref="ReliableTransport.Dispose"/>).
    /// </summary>
    public void Dispose()
    {
        try { Animations.Dispose(); } catch { }
        try
        {
            if (Transport.State == LinkState.Connected) EmergencyStop();
        }
        catch { }
        Transport.Dispose();
    }

    /// <summary>The engine's idle face: two "skip 64 columns" commands, i.e. nothing lit.</summary>
    private static readonly byte[] BlankFace = { 0x3F, 0x3F };

    /// <summary>
    /// Routes one message to everything that consumes it.
    ///
    /// The internal devices come first and each is isolated: this is one transport callback carrying the
    /// whole chain, so anything throwing part way through used to leave the rest of the devices without
    /// the message - a partial update of the robot's own state, which nothing downstream can detect. The
    /// public event is fanned out per subscriber for the same reason, and a subscriber that throws is
    /// reported through <see cref="HandlerFaulted"/> rather than taking the others with it.
    /// </summary>
    private void OnData(byte[] payload)
    {
        RobotMessage m;
        try { m = RobotMessage.Parse(payload); } catch (FormatException) { return; }
        Route(() => State.Handle(m));
        Route(() => Camera.Handle(m));
        Route(() => Sensors.Handle(m));
        Route(() => Cubes.Handle(m));
        Route(() => CubeAccel.Handle(m));
        EventFan.Raise(Message, m, e => Fault(e));
    }

    private void Route(Action a)
    {
        try { a(); }
        catch (Exception e) { Fault(e); }
    }

    private void Fault(Exception e)
    {
        try { HandlerFaulted?.Invoke(e); } catch { }
    }

    /// <summary>
    /// Raised when a message consumer or a subscriber to <see cref="Message"/> threw. The rest of the
    /// routing carried on; this is how that is made visible rather than silent.
    /// </summary>
    public event Action<Exception>? HandlerFaulted;
}
