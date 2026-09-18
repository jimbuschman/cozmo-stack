using System.Net;
using Cozmo.Protocol;
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
    public readonly Dictionary<RobotMessageId, int> Histogram = new();

    /// <summary>The robot recalibrates head and lift on every connect; motors should wait for this.</summary>
    public bool CalibratingMotors { get; private set; }
    public bool CalibrationSeen { get; private set; }

    public event Action<RobotState>? StateUpdated;

    public uint? SerialNumber => Available?.SerialNumberHead;
    public int? FirmwareVersionNumber => Firmware?.Version;
    public float? HeadAngleRad => Latest?.HeadAngle;
    public float? LiftHeight => Latest?.LiftAngle;
    public float? BatteryVolts => Latest?.BatteryVoltage;
    public bool OnCharger => Latest?.Has(RobotStatusFlag.IsOnCharger) ?? false;
    public bool Charging => Latest?.Has(RobotStatusFlag.IsCharging) ?? false;
    public bool PickedUp => Latest?.Has(RobotStatusFlag.IsPickedUp) ?? false;
    public bool CliffDetected => Latest?.Has(RobotStatusFlag.CliffDetected) ?? false;
    public double StateRateHz => StateCount > 1 && FirstStateUtc is { } f && LastStateUtc is { } l && l > f
        ? (StateCount - 1) / (l - f).TotalSeconds : 0;

    public void Handle(RobotMessage m)
    {
        Histogram[m.Id] = Histogram.GetValueOrDefault(m.Id) + 1;
        switch (m)
        {
            case RobotAvailable a: Available = a; break;
            case FirmwareVersion f: Firmware = f; break;
            case ManufacturingID i: Manufacturing = i; break;
            case SyncTimeAck: TimeSynced = true; break;
            case AnimationState a: Animation = a; break;
            case MotorCalibration c:
                CalibrationSeen = true;
                CalibratingMotors = c.CalibStarted;
                break;
            case RobotState s:
                Latest = s; StateCount++;
                LastStateUtc = DateTime.UtcNow; FirstStateUtc ??= LastStateUtc;
                StateUpdated?.Invoke(s);
                break;
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

    /// <summary>Every decoded robot message, after the devices have seen it.</summary>
    public event Action<RobotMessage>? Message;

    private CozmoRobot(TransportOptions? options)
    {
        Transport = new ReliableTransport(options);
        Display = new CozmoDisplay(m => Transport.Send(m, flush: true));
        Audio = new CozmoAudio(m => Transport.Send(m, flush: true));
        // The engine fills every animation tick with both an audio frame and a face keyframe. Mirror that
        // in both directions, so neither pipeline leaves the robot's animation tick half empty.
        Display.BeforeFrame = () => { if (!Audio.Busy) Transport.Send(new AudioSilence(), flush: true); };
        Audio.OnFrameSent += () =>
            Transport.Send(new Protocol.FaceImage { Image = Display.LastPayload ?? BlankFace }, flush: true);
        Transport.DataReceived += OnData;
    }

    /// <summary>
    /// Connects, completes the engine's handshake and waits for telemetry to start.
    /// </summary>
    public static async Task<CozmoRobot> ConnectAsync(IPAddress address, int? port = null,
                                                      TransportOptions? options = null,
                                                      TimeSpan? timeout = null,
                                                      bool enableAnimations = true)
    {
        var robot = new CozmoRobot(options);
        var connected = new TaskCompletionSource();
        void ok() => connected.TrySetResult();
        void bad(string r) => connected.TrySetException(new IOException($"disconnected while connecting: {r}"));
        robot.Transport.Connected += ok;
        robot.Transport.Disconnected += bad;
        try
        {
            robot.Transport.Connect(address, port);
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
        for (int i = 0; i < 40 && robot.State.StateCount == 0; i++) await Task.Delay(50);
        return robot;
    }

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

    /// <summary>Waits for the head and lift calibration the robot runs on connect, so motor commands are not fought.</summary>
    public async Task WaitForMotorCalibrationAsync(TimeSpan? timeout = null)
    {
        var end = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(8));
        while (DateTime.UtcNow < end)
        {
            if (State.CalibrationSeen && !State.CalibratingMotors) return;
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

    public void SetHeadAngle(float radians, byte actionId = 1)
        => Transport.Send(new SetHeadAngle(radians, actionId: actionId), flush: true);

    public void SetBackpackLights(LightState top, LightState middle, LightState bottom)
        => Transport.Send(new BackpackLightsMiddle(top, middle, bottom), flush: true);

    public void SetHeadlight(bool on) => Transport.Send(new SetHeadlight(on), flush: true);

    public void Disconnect() => Transport.Disconnect();
    public void Dispose() => Transport.Dispose();

    /// <summary>The engine's idle face: two "skip 64 columns" commands, i.e. nothing lit.</summary>
    private static readonly byte[] BlankFace = { 0x3F, 0x3F };

    private void OnData(byte[] payload)
    {
        RobotMessage m;
        try { m = RobotMessage.Parse(payload); } catch (FormatException) { return; }
        State.Handle(m);
        Camera.Handle(m);
        Message?.Invoke(m);
    }
}
