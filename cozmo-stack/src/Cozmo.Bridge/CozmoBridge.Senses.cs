using System.Buffers.Binary;
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
    // Host presentation only: immutable reports are joined by exact robot timestamp.
    // Never pair a frame with the most recent observation from another frame.
    private sealed class FrameReports
    {
        internal readonly Dictionary<int, RobotObservedFaceReport> Faces = new();
        internal readonly Dictionary<uint, RobotObservedObjectReport> Objects = new();
    }
    private readonly object cameraGate = new();
    private readonly Dictionary<uint, FrameReports> frameReports = new();
    private readonly Queue<uint> reportOrder = new();
    private readonly Dictionary<byte, string> clipNames = new();
    private readonly Dictionary<byte, string?> playingNames = new();
    private readonly Dictionary<byte, string?> lastPlayingNames = new();
    private AnimationPlaybackMonitor? playbackMonitor;
    private CameraFrame? cameraFrame;
    private double? cameraHead;
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
        Robot.Engine.EngineToGameMessage += GameMessage;
        Robot.Sensors.OffTreadsStateChanged += OffTreads;
    }
    private void DetachSenses()
    {
        if (playbackMonitor is {} monitor) { monitor.Issue -= PlaybackIssue; monitor.Dispose(); }
        Robot.Camera.FrameReceived -= CameraReceived;
        Robot.Vision.FrameProcessed -= VisionProcessed;
        Robot.Animations.Scheduler.Diagnostic -= AnimationDiagnostic;
        Robot.Message -= RobotMessage;
        Robot.Engine.EngineToGameMessage -= GameMessage;
        Robot.Sensors.OffTreadsStateChanged -= OffTreads;
        lock (cameraGate) { cameraFrame = null; frameReports.Clear(); reportOrder.Clear(); }
    }
    private void CameraReceived(CameraFrame frame)
    {
        // History supplies the exposure-time head angle; current head can be newer.
        var state = Robot.Vision.History.At(frame.Timestamp);
        double? head = state?.HeadAngleRad;
        state?.RobotPose.Dispose();
        lock (cameraGate) { cameraFrame = frame; cameraHead = head; }
    }
    private void VisionProcessed(VisionFrameResult result)
    {
        lock (cameraGate)
            if (cameraFrame is {} frame && frame.ImageId == result.ImageId && frame.Timestamp == result.Timestamp)
                cameraHead = result.PoseData.HeadAngleRad;
    }
    private FrameReports Reports(uint timestamp)
    {
        if (frameReports.TryGetValue(timestamp, out var found)) return found;
        if (reportOrder.Count == 32) frameReports.Remove(reportOrder.Dequeue());
        reportOrder.Enqueue(timestamp);
        return frameReports[timestamp] = new();
    }
    private void CaptureFace(RobotObservedFaceReport report)
    { lock (cameraGate) Reports(report.Timestamp).Faces[report.FaceId] = report; }
    private void CaptureObject(RobotObservedObjectReport report)
    { lock (cameraGate) Reports(report.Timestamp).Objects[report.ObjectId] = report; }

    /// <summary>Decodes a snapshot without requesting frames or changing vision modes.</summary>
    public BridgeCameraImage GetCameraImage(bool annotate = false)
    {
        ThrowIfDisposed();
        CameraFrame frame;
        double? head;
        RobotObservedFaceReport[] faces;
        RobotObservedObjectReport[] objects;
        lock (cameraGate)
        {
            frame = cameraFrame ?? throw new InvalidOperationException("No camera frame has arrived.");
            head = cameraHead;
            frameReports.TryGetValue(frame.Timestamp, out var reports);
            faces = reports?.Faces.Values.ToArray() ?? [];
            objects = reports?.Objects.Values.Where(o => o.Family == (uint)ObjectFamily.LightCube ||
                o.Type == (uint)ObjectType.Charger_Basic).ToArray() ?? [];
        }
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
            headAngleRad = head, annotated = annotate,
            faces = faces.Select(f => new { f.FaceId, f.Name, f.Rect }),
            objects = objects.Select(o => new { worldObjectId = o.ObjectId, type = ((ObjectType)o.Type).ToString(),
                rect = new FaceRect(o.ImageX,o.ImageY,o.ImageWidth,o.ImageHeight) }),
            observationReportsAvailable = faces.Length != 0 || objects.Length != 0,
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
        if (diagnostic is AnimationClipInitializedObservation clip)
            lock(eventGate) clipNames[clip.Tag] = clip.Name;
    }
    private void AnimationEvent(string type, byte tag)
    {
        lock(eventGate)
        {
            string? name;
            if (type == "animation-started")
                playingNames[tag] = lastPlayingNames[tag] = name = clipNames.GetValueOrDefault(tag);
            else if (playingNames.Remove(tag, out var playing))
                name = playing;
            else if (lastPlayingNames.TryGetValue(tag, out var previous))
                name = previous;
            else name = clipNames.GetValueOrDefault(tag);
            Append(type,null,JsonSerializer.SerializeToElement(new { tag, name },Json));
        }
    }

    private void RobotMessage(RobotMessage message)
    {
        // Existing CA/CB report semantics; do not infer start from initialization.
        if (message is AnimationStarted start) AnimationEvent("animation-started",start.AnimId);
        else if (message is AnimationEnded end) AnimationEvent("animation-ended",end.AnimId);
    }
    private void GameMessage(ushort tag, ReadOnlyMemory<byte> body)
    {
        // Existing AnimationAborted E2G 0x5F, also consumed by LiveNarration.
        if (tag == 0x5F && body.Length == 4)
        {
            uint id = BinaryPrimitives.ReadUInt32LittleEndian(body.Span);
            if (id <= byte.MaxValue) AnimationEvent("animation-aborted",(byte)id);
        }
    }
    private void OffTreads(OffTreadsState from, OffTreadsState to) =>
        Add("off-treads-changed", new { from = from.ToString(), to = to.ToString(),
            pickedUp = to == OffTreadsState.InAir, putDown = from == OffTreadsState.InAir && to == OffTreadsState.OnTreads });
}
