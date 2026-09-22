using Cozmo.Robot;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Cozmo.Transport;

namespace Cozmo.Protocol.Tests;

/// <summary>An offline robot with a connected cube, a vision system on a nominal calibration and a fake robot side.</summary>
internal sealed class Rig : IDisposable
{
    private static readonly MarkerLibrary? Lib = MarkerLibrary.EmbeddedOrNull;
    /// <summary>True when the extracted marker library is not present, so a marker test has nothing to render.</summary>
    public bool NoLibrary => Lib is null;
    public readonly ManualClock Clock = new();
    public readonly CozmoRobot Robot;
    public readonly VisionSystem Vision;
    public readonly ManipulationSystem M;
    public readonly CameraCalibration Cal = CameraCalibration.Nominal();
    public uint T = 1000;
    private ushort _seq = 1;
    public float X, Y, Angle, Head = -0.15f;
    public readonly List<string> Log = new();
    /// <summary>Messages the stack sent, decoded from the offline transport's frames.</summary>
    public readonly List<RobotMessage> Sent = new();
    private int _framesSeen;
    /// <summary>What the fake robot answers a dock with.</summary>
    public BlockStatus DockOutcome = BlockStatus.BlockPickedUp;
    public bool DockSucceeds = true;
    public int ErrorSignalsBeforeResult = 1;
    /// <summary>How many pick-and-place results the fake robot has reported.</summary>
    public int DockResults;
    /// <summary>Runs when the fake robot reports a dock result (to move the cube as the dock would).</summary>
    public Action? OnDockResult;
    public Pose3d? Cube;
    /// <summary>Further cubes (type, pose) rendered alongside <see cref="Cube"/> (for stacks and pyramids).</summary>
    public readonly List<(ObjectType Type, Pose3d Pose)> MoreCubes = new();
    /// <summary>A charger in the world, rendered from its marker; the fake robot reports IS_ON_CHARGER inside its footprint.</summary>
    public Pose3d? Charger;
    public bool OnCharger;
    public float LiftMm = 32f;
    public int FaceTurns;
    /// <summary>Every absolute (pan, tilt) the stack commanded through PanAndTilt.</summary>
    public readonly List<(double Pan, double Tilt)> PanTilts = new();
    /// <summary>A fake face detector: faces placed in the world are "detected" where the camera would see them.</summary>
    public readonly FakeFaceDetector FaceDetector = new();

    public Rig()
    {
        Robot = CozmoRobot.CreateOffline(clock: Clock);
        Deliver(new SubMessage(ReliableMessageType.ConnectionResponse, Array.Empty<byte>(), _seq++));
        Vision = new VisionSystem(Robot, Cal) { Enabled = false };
        M = new ManipulationSystem(Robot, Vision);
        // the look-around waits are real seconds on a robot; a rig takes them instantly
        M.Wait = (t, c) => Task.CompletedTask;
        M.Log += Log.Add;
        Vision.World.Log += Log.Add;
        M.TurnOverride = (id, max, ct) =>
        {
            // a turn towards the object: face it
            if (Vision.World.GetLocatedObjectById(id) is { } o)
            {
                var d = o.Pose.Translation - new Vec3(X, Y, 0); Angle = (float)Math.Atan2(d.Y, d.X);
                // the real action also tilts the head to see the object (TurnTowardsPose.HeadAngleToSee)
                Head = (float)Math.Clamp(TurnTowardsPose.HeadAngleToSee(Cal, new Pose3d(Mat3.AboutZ(Angle), new Vec3(X, Y, 0)), o.Pose.Translation), -0.436332, 0.776672);
                State();
            }
            return Task.FromResult(true);
        };
        Vision.TurnOverride = (target, max, ct) =>
        {
            // the face actions' turn: face the pose (unless it is beyond the maximum) and tilt the head to it
            var d = target.Translation - new Vec3(X, Y, 0);
            double rel = Math.Atan2(Math.Sin(Math.Atan2(d.Y, d.X) - Angle), Math.Cos(Math.Atan2(d.Y, d.X) - Angle));
            if (max > 0 && Math.Abs(rel) <= max) Angle = (float)Math.Atan2(d.Y, d.X);
            Head = (float)Math.Clamp(TurnTowardsPose.HeadAngleToSee(Cal, new Pose3d(Mat3.AboutZ(Angle), new Vec3(X, Y, 0)), target.Translation), -0.436332, 0.776672);
            State();
            FaceTurns++;
            return Task.FromResult(true);
        };
        // the tracking pan/tilt path: the body goes to an absolute heading and the head to the angle the
        // caller computed, which is what TrackFaceAction must actually send
        Vision.PanTiltOverride = (pan, tilt, ct) =>
        {
            PanTilts.Add((pan, tilt));
            Angle = (float)pan;
            Head = (float)Math.Clamp(tilt, -0.436332, 0.776672);
            State();
            FaceTurns++;
            return Task.FromResult(true);
        };
        Send(new ObjectConnectionState { ObjectID = 7, FactoryID = 0xABCD, ObjectType = ObjectType.Block_LIGHTCUBE1, Connected = true });
        Send(new ObjectConnectionState { ObjectID = 8, FactoryID = 0xABCE, ObjectType = ObjectType.Block_LIGHTCUBE2, Connected = true });
        Send(new ObjectConnectionState { ObjectID = 9, FactoryID = 0xABCF, ObjectType = ObjectType.Block_LIGHTCUBE3, Connected = true });
        State();
    }

    public void Send(RobotMessage m) => Deliver(new SubMessage(ReliableMessageType.SingleReliableMessage, m.ToBytes(), _seq++));

    private void Deliver(SubMessage sm)
    {
        var f = new Cozmo.Protocol.Frame { Type = ReliableMessageType.MultipleMixedMessages, SeqMin = sm.Seq, SeqMax = sm.Seq, Ack = 0, Messages = new List<SubMessage> { sm } };
        Robot.Transport.ProcessIncoming(FrameCodec.Encode(f));
    }

    public void State(uint? flags = null)
    {
        T += 33;
        Send(new RobotState { Timestamp = T, Pose = new RobotPose { X = X, Y = Y, Angle = Angle }, HeadAngle = Head, Status = flags ?? (OnCharger ? (uint)RobotStatusFlag.IsOnCharger : 0u),
                              Accel = new AccelData { Z = 9800 }, Gyro = new GyroData() });
    }

    /// <summary>Renders the cube (if any) from the current pose and processes the frame.</summary>
    public VisionFrameResult Frame()
    {
        State();
        var pd = Vision.History.At(T)!.Value;
        var frame = new GrayImage(Cal.Columns, Cal.Rows);
        frame.Fill(150);
        var cam = new CameraModel(Cal, pd.CameraPose);
        FaceDetector.Camera = cam;
        if (Cube is { } c && Lib is not null) MarkerRenderer.DrawCube(frame, Lib, cam, ObjectType.Block_LIGHTCUBE1, c);
        if (Lib is not null) foreach (var (type, pose) in MoreCubes) MarkerRenderer.DrawObject(frame, Lib, cam, type, pose);
        if (Charger is { } ch && Lib is not null) MarkerRenderer.DrawObject(frame, Lib, cam, ObjectType.Charger_Basic, ch);
        return Vision.ProcessImage(frame, T, T, pd);
    }

    /// <summary>
    /// Decodes everything the stack has sent since the last call and acts as the robot would. The offline
    /// transport holds reliable messages until they are acknowledged, so each batch is acked (a ping frame
    /// carrying the last sequence) and the connection ticked until nothing new comes out.
    /// </summary>
    public List<RobotMessage> Pump()
    {
        var fresh = new List<RobotMessage>();
        for (int round = 0; round < 50; round++)
        {
            var frames = Robot.Transport.OfflineOutbound;
            int before = fresh.Count;
            ushort lastSeq = 0; bool anyReliable = false;
            for (; _framesSeen < frames.Count; _framesSeen++)
            {
                var f = frames[_framesSeen];
                foreach (var sm in f.Messages)
                {
                    if (sm.IsReliable) { lastSeq = sm.Seq; anyReliable = true; if (!_seenSeqs.Add(sm.Seq)) continue; }   // a resend of an unacked message
                    if (sm.Type is not (ReliableMessageType.SingleReliableMessage or ReliableMessageType.SingleUnreliableMessage)) continue;
                    RobotMessage m;
                    try { m = RobotMessage.Parse(sm.Payload); } catch (Exception) { continue; }
                    fresh.Add(m); Sent.Add(m);
                }
            }
            if (anyReliable)
            {
                var ack = Cozmo.Protocol.Frame.Single(new SubMessage(ReliableMessageType.Ping, new PingPayload(0, 1, 0, true).ToBytes()), lastSeq);
                Robot.Transport.ProcessIncoming(FrameCodec.Encode(ack));
            }
            // the offline connection sends what is pending from its update tick, paced by its clock
            Clock.Advance(50);
            Robot.Transport.OfflineTick();
            if (Robot.Transport.OfflineOutbound.Count == _framesSeen && fresh.Count == before && round > 2) break;
        }
        foreach (var m in fresh)
        {
            switch (m)
            {
                case ExecutePath ep:
                    float wasX = X, wasY = Y;
                    // the fake robot follows the path perfectly: its pose becomes the last segment's end
                    foreach (var s in Sent.OfType<AppendPathSegmentLine>().TakeLast(CountSince<AppendPathSegmentLine>(ep))) { X = s.XEndMm; Y = s.YEndMm; }
                    // arcs end at their sweep's end point, heading tangent
                    int clearIdx = Sent.IndexOf(Sent.OfType<ClearPath>().Last());
                    foreach (var seg in Sent.Skip(clearIdx).TakeWhile(x => x != ep))
                    {
                        if (seg is AppendPathSegmentArc arc)
                        {
                            double cx = arc.XCenterMm, cy = arc.YCenterMm, r = arc.RadiusMm;
                            double a0 = arc.StartRad, sw = arc.SweepRad;
                            X = (float)(cx + r * Math.Cos(a0 + sw)); Y = (float)(cy + r * Math.Sin(a0 + sw)); Angle = (float)(a0 + sw + Math.Sign(sw) * Math.PI / 2);
                        }
                        else if (seg is AppendPathSegmentLine ln) { X = ln.XEndMm; Y = ln.YEndMm; }
                        else if (seg is AppendPathSegmentPointTurn pt) Angle = pt.TargetAngleRad;
                    }
                    UpdateChargerContact(wasX, wasY);
                    State();
                    Send(new PathFollowingEvent { EventId = ep.EventId, EventType = (byte)PathEventType.Started });
                    Send(new PathFollowingEvent { EventId = ep.EventId, EventType = (byte)PathEventType.Completed });
                    break;
                case DockWithObject dw:
                    // the firmware needs error signals to dock: it reports once enough have arrived (see Dock tests)
                    _dockPending = true; _signals = 0; _dockAction = (DockAction)dw.ToBytes()[17];
                    if (_dockAction is DockAction.PlaceLow or DockAction.PlaceHigh or DockAction.PlaceLowBlind)
                    {
                        // the fake firmware places from where it stands, without waiting for a marker signal
                        _dockPending = false; DockResults++; OnDockResult?.Invoke();
                        Send(new PickAndPlaceResult { Field0 = T, Field1 = (byte)(DockSucceeds ? 1 : 0), Field2 = 0, Field3 = (byte)(DockSucceeds ? BlockStatus.BlockPlaced : BlockStatus.NoBlock) });
                    }
                    break;
                case DockingErrorSignal es when _dockPending:
                    if (++_signals >= ErrorSignalsBeforeResult)
                    {
                        _dockPending = false; DockResults++;
                        if (_dockAction is DockAction.Align or DockAction.AlignSpecial)
                        {
                            // an align drives the robot until the signalled error is zero: the fake robot jumps there
                            float alignFromX = X, alignFromY = Y;
                            X += (float)(Math.Cos(Angle) * es.XDist - Math.Sin(Angle) * es.YDist);
                            Y += (float)(Math.Sin(Angle) * es.XDist + Math.Cos(Angle) * es.YDist);
                            UpdateChargerContact(alignFromX, alignFromY); State();
                        }
                        OnDockResult?.Invoke();
                        Send(new PickAndPlaceResult { Field0 = T, Field1 = (byte)(DockSucceeds ? 1 : 0), Field2 = 0, Field3 = (byte)(DockSucceeds ? DockOutcome : BlockStatus.NoBlock) });
                    }
                    break;
                case PlaceObjectOnGround:
                    Send(new PickAndPlaceResult { Field0 = T, Field1 = 1, Field2 = 0, Field3 = (byte)BlockStatus.BlockPlaced });
                    break;
                case SetHeadAngle sh:
                    Head = sh.AngleRad; State();                      // the fake robot's head follows the command at once
                    break;
                case SetLiftHeight sl:
                    LiftMm = sl.HeightMm; LiftHeights.Add(sl.HeightMm);
                    break;
            }
        }
        return fresh;
    }

    public readonly List<float> LiftHeights = new();

    /// <summary>
    /// The fake robot is on the charger when its origin lies within the charger's footprint (its frame:
    /// lip at x=0, +x inwards).
    ///
    /// A move that <em>passes through</em> the footprint counts too, and stops where it entered: the real
    /// robot backing on stops the moment the contacts report rather than driving the rest of its path
    /// (<c>BackupOntoChargerAction::CheckIfDone</c> 0x0054E7A8), so a path that would have carried it out
    /// the far side never happens. Without this the fake robot teleports through a 96 mm charger and
    /// reports nothing.
    /// </summary>
    private void UpdateChargerContact(float fromX, float fromY)
    {
        if (Charger is not { } ch) return;
        var inv = ch.Inverse();
        var local = inv.Apply(new Vec3(X, Y, 0));
        bool Inside(Vec3 p) => p.X >= 0 && p.X <= ChargerGeometry.LengthMm && Math.Abs(p.Y) <= ChargerGeometry.WidthMm / 2;
        if (Inside(local)) { OnCharger = true; return; }

        // did the straight move from (fromX, fromY) to here cross the footprint? step along it and stop
        // at the first point inside. Only a move that arrives from outside counts: one that started on
        // the charger is driving off it, and it is meant to end where it ended.
        var from = inv.Apply(new Vec3(fromX, fromY, 0));
        if (Inside(from)) { if (local.X < -5) OnCharger = false; return; }
        const int steps = 64;
        for (int i = 1; i <= steps; i++)
        {
            double t = i / (double)steps;
            var p = new Vec3(from.X + (local.X - from.X) * t, from.Y + (local.Y - from.Y) * t, 0);
            if (!Inside(p)) continue;
            var world = ch.Apply(p);
            X = (float)world.X; Y = (float)world.Y;
            OnCharger = true;
            return;
        }
        if (local.X < -5) OnCharger = false;
    }

    private bool _dockPending; private int _signals; private DockAction _dockAction;
    private readonly HashSet<ushort> _seenSeqs = new();
    private int CountSince<TMsg>(RobotMessage upTo) where TMsg : RobotMessage
    {
        int idx = Sent.IndexOf(upTo); int clear = Sent.FindLastIndex(idx, x => x is ClearPath);
        return Sent.Skip(clear).Take(idx - clear).OfType<TMsg>().Count();
    }

    public void Dispose() { M.Dispose(); Vision.Dispose(); Robot.Dispose(); }
}

/// <summary>
/// Stands in for a face detector in tests: faces are placed in the world (head position, optional name), and each
/// frame reports those the camera would see as rectangles sized from the 62 mm inter-pupil distance, so the
/// stack's own TrackedFace geometry recovers the placed position.
/// </summary>
internal sealed class FakeFaceDetector : IFaceDetector
{
    public readonly List<(int Id, Vec3 Head, string? Name)> Faces = new();
    public CameraModel? Camera;
    public bool IsAvailable => true;
    public string Description => "fake detector for tests";
    public int Detections;

    public IReadOnlyList<DetectedFace> Detect(GrayImage image, uint timestamp)
    {
        var out_ = new List<DetectedFace>();
        if (Camera is null) return out_;
        foreach (var (id, head, name) in Faces)
        {
            var c = Camera.ToCamera(head);
            if (c.Z <= 50) continue;
            var px = Camera.Project(head);
            if (px is null || px.Value.X < 0 || px.Value.Y < 0 || px.Value.X >= image.Width || px.Value.Y >= image.Height) continue;
            double eyePx = TrackedFace.InterPupilDistanceMm * Camera.Calibration.FocalLengthX / c.Z;
            double w = 2 * eyePx, h = w;
            // the rectangle's eye midpoint (centre − 0.125 h) is the projected head point
            var rect = new FaceRect(px.Value.X - w / 2, px.Value.Y + 0.125 * h - h / 2, w, h);
            out_.Add(new DetectedFace(id, rect, Name: name));
            Detections++;
        }
        return out_;
    }
}
