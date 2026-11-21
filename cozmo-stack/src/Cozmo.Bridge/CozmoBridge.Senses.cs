using System.Text.Json;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Vision;
using StbImageWriteSharp;

namespace Cozmo.Bridge;

public sealed record BridgeCameraImage(byte[] Png, JsonElement Metadata);

public sealed partial class CozmoBridge
{
    private readonly object cameraGate = new();
    private sealed record ReceivedCameraFrame(EngineRobot? Owner, CameraFrame Frame, double? Head);
    private ReceivedCameraFrame? receivedCamera;
    private ProcessedCameraCapture? processedCamera;
    private double? processedCameraHead;
    private readonly Dictionary<byte, string> clipNames = new();
    private readonly Dictionary<byte, string?> sentClipNames = new();
    private readonly Dictionary<byte, string?> playingNames = new();
    private readonly Dictionary<byte, string?> lastPlayingNames = new();
    private AnimationPlaybackMonitor? playbackMonitor;
    private sealed record RelativePose(double XMm, double YMm, double ZMm, double DistanceMm, double BearingDeg);

    // Caller holds World's pose gate, shared with MAIN/world writers.
    private RelativePose? Relative(Pose3d pose)
    {
        var owner = Robot.Engine.Robot;
        if (owner is null) return null;
        using var relative = new Pose3d();
        if (!Pose3d.GetWithRespectTo(pose, owner.GetCheckedMainPose(), relative)) return null;
        var t = relative.Translation;
        return new(t.X, t.Y, t.Z, Math.Sqrt(t.X*t.X+t.Y*t.Y), Math.Atan2(t.Y,t.X)*180/Math.PI);
    }

    private void AttachSenses()
    {
        playbackMonitor = new AnimationPlaybackMonitor(Robot);
        playbackMonitor.Issue += PlaybackIssue;
        Robot.Camera.FrameReceived += CameraReceived;
        Robot.Vision.FrameProcessed += VisionProcessed;
        Robot.Animations.Scheduler.Diagnostic += AnimationDiagnostic;
        Robot.Message += RobotMessage;
        Robot.Engine.AnimationAbortObserved += AnimationAbort;
        Robot.Sensors.OffTreadsStateChanged += OffTreads;
        Robot.Sensors.CliffDetected += CliffDetected;
        Robot.Sensors.UnexpectedMovementDetected += UnexpectedMovement;
        Robot.State.StateUpdated += PowerState;
    }
    private void DetachSenses()
    {
        if (playbackMonitor is {} monitor) { monitor.Issue -= PlaybackIssue; monitor.Dispose(); }
        Robot.Camera.FrameReceived -= CameraReceived;
        Robot.Vision.FrameProcessed -= VisionProcessed;
        Robot.Animations.Scheduler.Diagnostic -= AnimationDiagnostic;
        Robot.Message -= RobotMessage;
        Robot.Engine.AnimationAbortObserved -= AnimationAbort;
        Robot.Sensors.OffTreadsStateChanged -= OffTreads;
        Robot.Sensors.CliffDetected -= CliffDetected;
        Robot.Sensors.UnexpectedMovementDetected -= UnexpectedMovement;
        Robot.State.StateUpdated -= PowerState;
        lock (cameraGate) { receivedCamera = null; processedCamera = null; }
    }
    private void CameraReceived(CameraFrame frame)
    {
        var owner = Robot.Engine.Robot;
        // History supplies the exposure-time head angle; current head can be newer.
        var state = Robot.Vision.History.At(frame.Timestamp);
        double? head = state?.HeadAngleRad;
        state?.RobotPose.Dispose();
        lock (cameraGate)
        {
            if (!ReferenceEquals(owner, Robot.Engine.Robot) || owner?.Lifetime.Destroying == true) return;
            if (receivedCamera is { } previous && (!ReferenceEquals(previous.Owner, owner) ||
                frame.Timestamp < previous.Frame.Timestamp))
                processedCamera = null;
            receivedCamera = new(owner, frame, head);
        }
    }

    internal void VisionProcessed(VisionFrameResult result)
    {
        if (result.CameraCapture is not { } capture) return;
        lock (cameraGate)
        {
            if (receivedCamera is not { } received || capture.Owner.Lifetime.Destroying ||
                !ReferenceEquals(capture.Owner, Robot.Engine.Robot) || !ReferenceEquals(capture.Owner, received.Owner) ||
                capture.Frame.Timestamp > received.Frame.Timestamp)
                return;
            processedCamera = capture;
            processedCameraHead = result.PoseData.HeadAngleRad;
        }
    }

    private Task<CommandResult> SetCameraColor(bool enabled, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        // G2E handler 0x0065835C..0x00658378 uses the same byte store and reliable
        // EnableColorImages robot message as 0x006582CC..0x00658314.
        var engine = Robot.Engine;
        var owner = engine.Robot;
        if (engine.ConnectionState != 2 || !engine.IsReadyDelivered || engine.RobotValidated != true ||
            owner is null || owner.Lifetime.Destroying || !owner.FirstFullStateHandled)
            return Task.FromResult(CommandResult.NotReady());

        Robot.CameraSettings.EnableColorImages(enabled);
        if (owner.Lifetime.Destroying || !ReferenceEquals(engine.Robot, owner))
            return Task.FromResult(CommandResult.Action(false, null, "Robot owner retired during the camera request", true));
        return Task.FromResult(CommandResult.Confirmed(new { enabled }));
    }

    /// <summary>Decodes a snapshot without requesting frames or changing vision modes.</summary>
    public BridgeCameraImage GetCameraImage(bool annotate = false)
    {
        ThrowIfDisposed();
        CameraFrame frame;
        EngineRobot? owner;
        double? head;
        CameraFrame newest;
        ProcessedCameraCapture? reports;
        lock (cameraGate)
        {
            owner = Robot.Engine.Robot;
            if (receivedCamera is not { } received || !ReferenceEquals(owner, received.Owner) ||
                owner?.Lifetime.Destroying == true)
                throw new InvalidOperationException("No camera frame has arrived.");

            newest = received.Frame;
            reports = processedCamera;
            if (!annotate && !ReferenceEquals(reports?.Frame, newest)) reports = null;
            frame = reports?.Frame ?? newest;
            head = reports is null ? received.Head : processedCameraHead;
        }
        var (liftInView, liftRegion) = Robot.Vision.GetLiftImageCoverage(owner, frame.Timestamp);
        var faces = reports?.Faces ?? [];
        var objects = reports?.Objects.Where(o => o.Family == (uint)ObjectFamily.LightCube ||
            o.Type == (uint)ObjectType.Charger_Basic).ToArray() ?? [];
        if (!frame.TryDecodeRgb(out var decoded, out var error))
            throw new InvalidOperationException("Camera frame could not be decoded: " + error);
        byte[] rgb = (byte[])decoded!.Clone();
        const int width = EncodedImageDecoder.Columns, height = EncodedImageDecoder.Rows;
        if (annotate)
        {
            foreach (var face in faces) Box(face.Rect, 0, 255, 0);
            foreach (var obj in objects) Box(new(obj.ImageX,obj.ImageY,obj.ImageWidth,obj.ImageHeight),255,180,0);
        }
        using var output = new MemoryStream();
        new ImageWriter().WritePng(rgb, width, height, ColorComponents.RedGreenBlue, output);
        var metadata = JsonSerializer.SerializeToElement(new
        {
            frame.ImageId, robotTimestamp = frame.Timestamp, frame.ReceivedUtc, width, height,
            frame.IsColor, frame.Encoding,
            headAngleRad = head, annotated = annotate && reports is not null,
            visionProcessed = reports is not null, latestReceivedImageId = newest.ImageId,
            faces = faces.Select(f => new { f.FaceId, f.Name, f.Rect }),
            objects = objects.Select(o => new { worldObjectId = o.ObjectId, type = ((ObjectType)o.Type).ToString(),
                rect = new FaceRect(o.ImageX,o.ImageY,o.ImageWidth,o.ImageHeight) }),
            observationReportsAvailable = faces.Count != 0 || objects.Length != 0,
            liftInView, liftRegion,
        }, Json);
        return new(output.ToArray(), metadata);

        void Box(FaceRect rect, byte r, byte g, byte b)
        {
            if (!double.IsFinite(rect.X) || !double.IsFinite(rect.Y) || !double.IsFinite(rect.Width) ||
                !double.IsFinite(rect.Height) || rect.Width <= 0 || rect.Height <= 0) return;
            int x0=(int)Math.Clamp(rect.X,0,width-1), y0=(int)Math.Clamp(rect.Y,0,height-1);
            int x1=(int)Math.Clamp(rect.X+rect.Width,0,width-1), y1=(int)Math.Clamp(rect.Y+rect.Height,0,height-1);
            for(int x=x0;x<=x1;x++) { Pixel(x,y0);Pixel(x,y1); }
            for(int y=y0;y<=y1;y++) { Pixel(x0,y);Pixel(x1,y); }
            void Pixel(int x,int y) { int i=(y*width+x)*3;rgb[i]=r;rgb[i+1]=g;rgb[i+2]=b; }
        }
    }

    private void PlaybackIssue(AnimationPlaybackIssue issue) => Add("animation-cut-off-by-robot", issue);

    private void AnimationDiagnostic(AnimationDiagnostic diagnostic)
    {
        // InitStream identity 0x0057B674 is selection, not robot playback.
        if (diagnostic is not (AnimationClipInitializedObservation or AnimationStartSentObservation)) return;
        lock (eventGate)
        {
            if (diagnostic is AnimationClipInitializedObservation clip)
                clipNames[clip.Tag] = clip.Name;
            else if (diagnostic is AnimationStartSentObservation start)
                sentClipNames[start.Tag] = start.Name;
        }
    }
    private void AnimationEvent(string type, byte tag, string? observedName = null)
    {
        lock(eventGate)
        {
            string? name;
            if (type == "animation-started")
                playingNames[tag] = lastPlayingNames[tag] = name = sentClipNames.TryGetValue(tag, out var sent)
                    ? sent : clipNames.GetValueOrDefault(tag);
            else if (playingNames.Remove(tag, out var playing))
                name = playing;
            else if (lastPlayingNames.TryGetValue(tag, out var previous))
                name = previous;
            else name = clipNames.GetValueOrDefault(tag);
            Append(type, null, JsonSerializer.SerializeToElement(new { tag, name = observedName ?? name }, Json));
        }
    }

    private void RobotMessage(RobotMessage message)
    {
        // Existing CA/CB report semantics; do not infer start from initialization.
        if (message is AnimationStarted start) AnimationEvent("animation-started",start.AnimId);
        else if (message is AnimationEnded end) AnimationEvent("animation-ended",end.AnimId);
    }
    private void AnimationAbort((byte Tag, string? Name) observation) =>
        AnimationEvent("animation-aborted", observation.Tag, observation.Name);
    private void OffTreads(OffTreadsState from, OffTreadsState to)
    {
        bool pickedUp = to == OffTreadsState.InAir;
        bool putDown = from == OffTreadsState.InAir && to == OffTreadsState.OnTreads;
        Add("off-treads-changed", new { from = from.ToString(), to = to.ToString(), pickedUp, putDown },
            eligible: pickedUp || putDown || to == OffTreadsState.OnBack,
            compactEligible: pickedUp || putDown || to == OffTreadsState.OnBack ||
                (from, to) is (OffTreadsState.InAir, OffTreadsState.Falling) or
                              (OffTreadsState.Falling, OffTreadsState.OnTreads));
    }
}
