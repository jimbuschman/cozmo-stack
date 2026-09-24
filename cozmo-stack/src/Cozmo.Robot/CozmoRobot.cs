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

    // fidelity: M1-025, M1-015
    /// <summary>
    /// Back to the state right after construction, for a removed robot (CB33, CC26: the Robot is deleted; CC27: a
    /// later ConnectToRobot builds it afresh). Subscribers are kept.
    /// </summary>
    internal void ResetToConstructed()
    {
        lock (_gate)
        {
            _histogram.Clear();
            Available = null; Firmware = null; Manufacturing = null; Latest = null; Animation = null;
            TimeSynced = false;
            StateCount = 0; FirstStateUtc = null; LastStateUtc = null;
            CalibrationSeen = false;
            HeadCalibrating = false; LiftCalibrating = false;
            HeadCalibrated = false; LiftCalibrated = false;
        }
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

    // fidelity: M1-024, M1-025, M1-026, M1-027, M1-028, M1-029, M1-030, M1-031, M1-041, M1-040, M1-042
    /// <summary>
    /// The engine's app layer for this robot (<see cref="CozmoEngine"/>): the 60 ms tick, the connection manager,
    /// MessageHandler and the initial-connection handshake. Robot messages reach the devices only from its per-tick
    /// drain (M1-024), and every message the devices send goes through its send path (M1-026).
    /// </summary>
    public CozmoEngine Engine { get; }

    private CozmoRobot(TransportOptions? options, ReliableTransport? transport, CozmoEngineOptions? engineOptions,
                       IEngineTransport? port = null, Func<long>? engineClock = null)
    {
        Transport = transport ?? new ReliableTransport(options);
        var realPort = port is null ? new ReliableTransportPort(Transport) : null;
        Engine = new CozmoEngine(port ?? realPort!, engineOptions, engineClock);
        if (realPort is not null) realPort.Log = Engine.Log;
        Display = new CozmoDisplay(m => SendMessage(m, flush: true),
                                   Transport.Options.MaxFramePayloadBytes - CozmoDisplay.MessageOverhead);
        Audio = new CozmoAudio(m => SendMessage(m, flush: true));
        // The engine fills every animation tick with both an audio frame and a face keyframe. Mirror that
        // in both directions, so neither pipeline leaves the robot's animation tick half empty.
        Display.BeforeFrame = () => { if (!Audio.Busy) SendMessage(new AudioSilence(), flush: true); };
        Audio.PlayedFrames = () => State.Animation?.NumAudioFramesPlayed ?? 0;
        Audio.OnFrameSent += () =>
            SendMessage(new Protocol.FaceImage { Image = Display.LastPayload ?? BlankFace }, flush: true);
        Motion = new CozmoMotion(this);
        Lights = new CozmoLights(this);
        Sensors = new CozmoSensors(this, State);
        Cubes = new CozmoCubes(this);
        CubeAccel = new CubeAccelStreams(this);
        Animations = new CozmoAnimations(this);
        Face = new CozmoFace(this);
        // fidelity: M1-024
        // Messages reach the devices only from the engine's per-tick drain (B25, CD10), not from the transport.
        Engine.DeviceRoute = RouteToDevices;
        Engine.PublicRoute = m => EventFan.Raise(Message, m, e => Fault(e));
        Engine.Faulted = Fault;
        // fidelity: M1-025, M1-015
        // CB33, CC26: RemoveRobot deletes the Robot, and with it every Robot component; CC27: a later ConnectToRobot
        // builds everything afresh. The M1 device objects on this robot (and the VisionSystem built on it) stay here,
        // because callers hold them, so each is put back in its as-constructed state instead (ResetDevices).
        // NOT YET RESET (named residual for M12-M15): the upper-layer Robot components the engine also builds in
        // Robot::Robot 0x0050FBF1 and deletes with it - DockingSystem's Carrying (CarryingComponent), DockingComponent,
        // PathFollower, BehaviorManager, MoodManager, IdleBehavior, ReactiveBehavior, CubeMovedReactionStrategy - are
        // built by callers here and keep their state across a removal.
        Engine.RobotRemoved = ResetDevices;
        // fidelity: M1-042
        Engine.AfterSuccessDefaults = SendAppDefaults;
    }

    // fidelity: M1-042
    /// <summary>
    /// Policy M1-042: after a Success RobotConnectionResponse this stack does what the phone app would: it sends
    /// the stored volume (SetRobotVolume → SetAudioVolume {u16 vol × 65535}, CD27) and enables the block pool the
    /// way BlockPoolEnabledMessage {true, 0} does (CD28; <see cref="CubeConnections.EnableAutoBlockPool"/>). It runs
    /// as a game message, at the start of the tick after the response.
    /// MISSING: when the original calls Robot::SetPhysicalRobot(true), which loads the persistent block pool
    /// (<see cref="CubeConnections.Init"/>), is not in the M1 inventory; this stack keeps doing it here, just
    /// before the pool is enabled, as it did before (M4 interface).
    /// </summary>
    private void SendAppDefaults()
    {
        Engine.SendRobotVolume(Engine.Options.RobotVolume);
        Cubes.Connections.Init(Engine.Options.BlockPoolPath ?? DefaultBlockPoolPath);
        Cubes.EnableAutoBlockPool(enabled: true, discoveryTimeSeconds: 0f);
    }

    /// <summary>
    /// A robot with no socket, driven by feeding datagrams to <see cref="ReliableTransport.ProcessIncoming"/>.
    /// Used by the replay tool and by tests, so the whole device layer can be exercised against captured
    /// traffic without a robot present. Outgoing messages land in the transport's offline frame list.
    ///
    /// Test seam, not a production path: the engine has no 60 ms thread here. Every arrival (and every game
    /// message) runs the engine tick on the calling thread until nothing is pending, so a fed datagram still goes
    /// through the per-tick drain (M1-024) but reaches the devices before the call that fed it returns. The link
    /// starts in the state after a completed handshake: the connection is Connecting on the transport's own
    /// loopback peer (it becomes Connected when a ConnectionResponse is fed, B23), Robot 1 exists with no
    /// RobotInitialConnection (so nothing is filtered, CB27), and it is time synced, ready to stream and has had
    /// its first full state (CD12, CD19, CD20, CD23). No handshake, post-connect or app-default message is sent.
    /// </summary>
    public static CozmoRobot CreateOffline(TransportOptions? options = null, INetClock? clock = null)
    {
        var transport = ReliableTransport.CreateOffline(options, clock);
        Func<long>? nowNs = clock is null ? null : () => (long)(clock.NowMs * 1_000_000.0);
        var robot = new CozmoRobot(options, transport, new CozmoEngineOptions(), null, nowNs);
        robot.Transport.OfflineConnect();
        robot.Engine.InitOfflineLink(transport.Peer!);
        robot.Engine.StartOffline(autoTick: true);
        return robot;
    }

    /// <summary>
    /// Test seam: a robot whose engine talks to <paramref name="port"/> and is ticked only by
    /// <see cref="CozmoEngine.Tick"/>. <see cref="Transport"/> is an unused offline transport.
    /// </summary>
    internal static CozmoRobot CreateForTest(IEngineTransport port, Func<long> engineClock, CozmoEngineOptions? engineOptions = null)
    {
        var robot = new CozmoRobot(null, ReliableTransport.CreateOffline(), engineOptions ?? new CozmoEngineOptions(), port, engineClock);
        robot.Engine.StartOffline(autoTick: false);
        return robot;
    }

    // fidelity: M1-024, M1-019
    /// <summary>
    /// The engine without a connection: the transport is started (RCM::Init's StartClient, B12) and the 60 ms engine
    /// thread runs (B24). Connect with <see cref="ConnectToRobot"/> and watch <see cref="CozmoEngine.ConnectionResponse"/>.
    /// </summary>
    public static CozmoRobot Create(TransportOptions? options = null, CozmoEngineOptions? engineOptions = null)
    {
        var robot = new CozmoRobot(options, null, engineOptions);
        robot.Engine.StartProduction();
        return robot;
    }

    // fidelity: M1-001, M1-025
    /// <summary>
    /// The ConnectToRobot game message (B1, B2, CB1). With no <paramref name="address"/> the IP is the one Unity
    /// uses: 172.31.1.1, or 127.0.0.1 when <paramref name="isSimulated"/>. The remote port is 5552 when simulated,
    /// else 5551 (B3). Ignored while Robot 1 exists ("Robot already connected", CB36).
    /// </summary>
    public void ConnectToRobot(IPAddress? address = null, bool isSimulated = false)
        => Engine.ConnectToRobot(address ?? RobotAddress.DefaultFor(isSimulated), isSimulated);

    // fidelity: M1-001, M1-025, M1-015, M1-028
    /// <summary>
    /// Creates the engine (<see cref="Create"/>), sends ConnectToRobot and returns once the engine's
    /// RobotConnectionResponse arrives. There is no connect timer (CB1, CB31): the answer comes from the handshake,
    /// or, for a robot that never answers the transport connect, from its 5 s timeout as ConnectionRejected (B31,
    /// CB32). A response other than Success disposes the robot and throws <see cref="RobotConnectionException"/>;
    /// <paramref name="cancellationToken"/> lets a caller stop waiting (the robot is then disposed too).
    /// </summary>
    public static async Task<CozmoRobot> ConnectAsync(IPAddress? address = null, bool isSimulated = false,
                                                      TransportOptions? options = null,
                                                      CozmoEngineOptions? engineOptions = null,
                                                      CancellationToken cancellationToken = default)
    {
        address ??= RobotAddress.DefaultFor(isSimulated);
        var robot = Create(options, engineOptions);
        var response = new TaskCompletionSource<RobotConnectionResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        void on(RobotConnectionResponse r) => response.TrySetResult(r);
        robot.Engine.ConnectionResponse += on;
        RobotConnectionResponse resp;
        try
        {
            using var reg = cancellationToken.Register(() => response.TrySetCanceled(cancellationToken));
            robot.ConnectToRobot(address, isSimulated);
            resp = await response.Task.ConfigureAwait(false);
        }
        catch
        {
            robot.Engine.ConnectionResponse -= on;
            robot.Dispose();
            throw;
        }
        robot.Engine.ConnectionResponse -= on;
        if (resp.Result != RobotConnectionResult.Success)
        {
            robot.Dispose();
            throw new RobotConnectionException(resp);
        }
        return robot;
    }

    // fidelity: M1-026
    /// <summary>
    /// Sends a robot message the way the engine sends every one: MessageHandler::SendMessage (B28, CB29). The
    /// <paramref name="reliable"/> and <paramref name="flush"/> arguments are ignored as the engine ignores them:
    /// every message goes reliable with no flush hint (R42), at once (CD13). It returns false, silently, when there
    /// is no connection in state 2 or the handshake has not validated the robot (M1-030).
    /// </summary>
    public bool SendMessage(RobotMessage m, bool reliable = true, bool flush = false) => Engine.SendMessage(m);

    /// <summary>Whether the engine's animation streamer is running (CD12): the animation loop streams only then.</summary>
    public bool AnimationStreamingOpen => Engine.AnimationStreamingOpen;

    /// <summary>Where the block pool is kept between sessions unless the engine options say otherwise.</summary>
    public static string DefaultBlockPoolPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "cozmo-stack", "blockPool.txt");

    /// <summary>
    /// Starts the robot's animation controller. Face images and audio frames are animation keyframes: until
    /// this is sent the robot receives them and does nothing visible. It answers by streaming AnimationState.
    /// </summary>
    public void EnableAnimations() => SendMessage(new InitController(), flush: true);

    /// <summary>Stops whatever animation is playing and clears the robot's keyframe buffer.</summary>
    public void EndAnimation() => SendMessage(new EndOfAnimation(), flush: true);

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
        SendMessage(new EnableColorImages { Enable = color }, flush: true);
        SendMessage(new ImageRequest { Mode = singleShot ? ImageSendMode.SingleShot : ImageSendMode.Stream }, flush: true);
    }

    public void StopCamera() => SendMessage(new ImageRequest { Mode = ImageSendMode.Off }, flush: true);

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
        => SendMessage(new SetHeadAngle(radians, actionId: actionId), flush: true);

    /// <summary>Sets the backpack from raw light states. Prefer <see cref="Lights"/>.</summary>
    public void SetBackpackLights(LightState top, LightState middle, LightState bottom)
        => SendMessage(new BackpackLightsMiddle(top, middle, bottom), flush: true);

    /// <summary>Prefer <see cref="Lights"/>.</summary>
    public void SetHeadlight(bool on) => Lights.SetHeadlight(on);

    /// <summary>
    /// Stops every motor immediately. Safe to call at any time, including before the robot is ready, and
    /// does not wait for confirmation; <see cref="CozmoMotion.StopAllAsync"/> is the checked version.
    /// </summary>
    public void EmergencyStop()
    {
        SendMessage(new StopAllMotors(), flush: true);
        SendMessage(new DriveWheels(0f, 0f, 0f, 0f), flush: true);
    }

    // fidelity: M1-025
    /// <summary>
    /// Asks the engine to disconnect: DisconnectCurrent at the next tick (B33, CC29), then the usual disconnect
    /// handling (RobotDisconnected, the robot removed, CB33). There is no automatic reconnect (CC27).
    /// </summary>
    public void Disconnect() => Engine.DisconnectCurrent();

    /// <summary>
    /// Stops the motors, stops the engine, then disposes the transport. The stop commands are sent like every
    /// robot message (only while connected); the transport's dispose then makes its one DisconnectRequest send
    /// attempt and closes the socket without waiting for anything queued to drain (see
    /// <see cref="ReliableTransport.Dispose"/>).
    /// NOTE: the StopAllMotors / DriveWheels(0) before disconnecting is this stack's, not the engine's (comparison
    /// item 4); it is kept pending a safety-policy decision.
    /// </summary>
    public void Dispose()
    {
        try { Animations.Dispose(); } catch { }
        try
        {
            if (Engine.ConnectionState == 2) EmergencyStop();
        }
        catch { }
        Engine.Dispose();
        Transport.Dispose();
    }

    /// <summary>The engine's idle face: two "skip 64 columns" commands, i.e. nothing lit.</summary>
    private static readonly byte[] BlankFace = { 0x3F, 0x3F };

    // fidelity: M1-024, M1-041
    /// <summary>
    /// The stack's devices as engine subscribers of one broadcast message (CC36), called from the engine's per-tick
    /// dispatch. A RobotState the Robot drops before time sync (CD23) does not reach them.
    ///
    /// Each device is isolated (policy M1-034): anything throwing part way through would otherwise leave the rest of
    /// the devices without the message - a partial update of the robot's own state, which nothing downstream can
    /// detect. A fault is reported through <see cref="HandlerFaulted"/>.
    /// </summary>
    private void RouteToDevices(RobotMessage m, bool stateHandled)
    {
        if (m is RobotState && !stateHandled) return;
        Route(() => State.Handle(m));
        Route(() => Camera.Handle(m));
        Route(() => Sensors.Handle(m));
        Route(() => Cubes.Handle(m));
        Route(() => CubeAccel.Handle(m));
    }

    // fidelity: M1-025, M1-015
    /// <summary>
    /// RemoveRobot's deletion of the Robot as this stack has it (CB33, CC26; CC27: the next ConnectToRobot starts from
    /// a fresh robot). Called on the engine thread from RemoveRobot. Whatever is playing ends first, as the deleted
    /// AnimationStreamer's animation does; then every device goes back to its as-constructed state, keeping its
    /// subscribers and the hooks this object installed; then the systems built on this robot are told
    /// (<see cref="RobotRemoved"/>). Each step is isolated like the message routing (policy M1-034).
    /// </summary>
    private void ResetDevices()
    {
        Route(() => Animations.Stop());
        Route(Animations.ResetToConstructed);
        Route(Face.ResetToConstructed);
        Route(State.ResetToConstructed);
        Route(Camera.ResetToConstructed);
        Route(Display.ResetToConstructed);
        Route(Audio.ResetToConstructed);
        Route(Motion.ResetToConstructed);
        Route(Lights.ResetToConstructed);
        Route(Sensors.ResetToConstructed);
        Route(Cubes.ResetToConstructed);
        Route(CubeAccel.ResetToConstructed);
        if (RobotRemoved is { } removed)
            foreach (var d in removed.GetInvocationList()) Route((Action)d);
    }

    // fidelity: M1-025, M1-015
    /// <summary>
    /// Raised on the engine thread after <see cref="ResetDevices"/> has reset the devices, so a system built on this
    /// robot (<see cref="Vision.VisionSystem"/>) can reset what it keeps for it too.
    /// </summary>
    internal event Action? RobotRemoved;

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
