using Cozmo.Protocol;

namespace Cozmo.Robot.Vision;

/// <summary><c>Anki::Vision::FacialExpression</c> (UNITY, <c>Anki.Vision/FacialExpression.cs</c>).</summary>
public enum FacialExpression : sbyte { Unknown = -1, Neutral = 0, Happiness = 1, Surprise = 2, Anger = 3, Sadness = 4 }

/// <summary><c>Anki::Vision::PetType</c> (UNITY).</summary>
public enum PetType : byte { Unknown = 0, Cat = 1, Dog = 2 }

/// <summary>An axis-aligned image rectangle in pixels (<c>Anki::Rectangle&lt;float&gt;</c>).</summary>
public readonly record struct FaceRect(double X, double Y, double Width, double Height)
{
    public Vec2 Center => new(X + Width / 2, Y + Height / 2);
    /// <summary><c>Rectangle::ComputeOverlapScore</c>: intersection over union.</summary>
    public double OverlapScore(FaceRect o)
    {
        double x0 = Math.Max(X, o.X), y0 = Math.Max(Y, o.Y), x1 = Math.Min(X + Width, o.X + o.Width), y1 = Math.Min(Y + Height, o.Y + o.Height);
        double inter = Math.Max(0, x1 - x0) * Math.Max(0, y1 - y0);
        double union = Width * Height + o.Width * o.Height - inter;
        return union <= 0 ? 0 : inter / union;
    }
}

/// <summary>
/// One face as a detector reports it for one image: the tracker's id (the engine's OKAO tracking id, or a
/// recognised face id), the face rectangle, the eye centres when the part detector found them, the
/// expression scores (<c>TrackedFace::SetExpressionValue</c>, one per <see cref="FacialExpression"/>), and the
/// name when the recogniser knows one.
/// </summary>
public sealed record DetectedFace(int Id, FaceRect Rect, Vec2? LeftEye = null, Vec2? RightEye = null, IReadOnlyList<byte>? ExpressionValues = null, string? Name = null, int Score = 0, double? RollRad = null);

/// <summary>
/// The seam where the engine calls Omron's OKAO Vision library. <c>FaceTracker::Impl::Update</c>
/// (0x0086D6xx) runs <c>OKAO_DT_Detect_GRAY</c>, <c>OKAO_DT_GetResultCount</c>, <c>OKAO_DT_GetRawResultInfo</c>,
/// <c>OKAO_CO_ConvertCenterToSquare</c>, then per face <c>DetectFaceParts</c>, <c>EstimateExpression</c>,
/// <c>DetectSmile</c>, <c>DetectGazeAndBlink</c>, <c>IsEnrollable</c> and hands enrollable faces to
/// <c>FaceRecognizer::SetNextFaceToRecognize</c>. Everything from the image to the face rectangles, parts,
/// expressions and identities is inside that library (177 <c>OKAO_*</c> exports), which is proprietary and
/// not reproduced here. An implementation of this interface supplies faces per image; the shipped stack has
/// none (<see cref="OkaoFaceDetector"/> says so), so the face behaviours run only when a detector is attached.
/// </summary>
public interface IFaceDetector
{
    bool IsAvailable { get; }
    string Description { get; }
    IReadOnlyList<DetectedFace> Detect(GrayImage image, uint timestamp);
}

/// <summary>The stock detector, recorded as an explicit boundary: the OKAO library is not available to this stack.</summary>
public sealed class OkaoFaceDetector : IFaceDetector
{
    public const int OkaoExportCount = 177;
    public bool IsAvailable => false;
    public string Description => $"Omron OKAO Vision (FaceTracker::Impl over {OkaoExportCount} OKAO_* exports): proprietary, not available in this stack";
    public IReadOnlyList<DetectedFace> Detect(GrayImage image, uint timestamp) => Array.Empty<DetectedFace>();
}

/// <summary>
/// <c>Anki::Vision::TrackedFace</c>: a detected face with its head pose. <c>UpdateTranslation(camera)</c>
/// (0x0087DE24, C2-F23): the selector is the parts/eye-detected flag at +0x30 (here: both eye centres
/// present). With parts, the eye distance is <c>GetIntraEyeDistance</c> 0x0087DC68,
/// <c>sqrt((x1-x2)^2+(y1-y2)^2) / cos(roll)</c> at +0xec (the divisor is 1.0 when |cos| &lt; 1e-5, and
/// 6.0/divisor when the distance is under 1e-5); without parts, the box branch builds
/// A=(x+0.25w, y+0.375h) and B=(x+0.75w, y+0.375h), so the distance is |0.5 w| floored at 6.0. The
/// common tail takes the midpoint of the eye centres (the zeroed (0,0) slots, pixel (0,0), in the box
/// branch), the ray through it (inverse calibration, unit length), scales by
/// <c>focalX * 62.0 / eyeDistance</c> (0x42780000 = 62.0) and parents the pose to the camera pose.
/// </summary>
public sealed class TrackedFace
{
    /// <summary>The human inter-pupil distance the ray is scaled by, 62.0 mm (0x42780000).</summary>
    public const double InterPupilDistanceMm = 62.0;
    /// <summary>The parts-branch fallback and the box-branch floor, 6.0 px (0x0087DD86 / 0x0087DF0A).</summary>
    public const double MinIntraEyeDistancePx = 6.0;
    /// <summary>The 1e-5 literal (0x3727C5AC): the |cos| divisor threshold, and the distance fallback.</summary>
    public const double MinCosOrDistance = 1e-5;

    public TrackedFace(DetectedFace d, uint timestamp) { Detection = d; Timestamp = timestamp; }

    public DetectedFace Detection { get; }
    public uint Timestamp { get; }
    public int Id => Detection.Id;
    public FaceRect Rect => Detection.Rect;
    public string? Name => Detection.Name;
    public Pose3d HeadPose { get; internal set; } = Pose3d.Identity;
    public double DistanceMm { get; private set; }

    /// <summary>The parts/eye-detected flag at +0x30: both eye centres present.</summary>
    public bool HasEyeParts => Detection.LeftEye is { } && Detection.RightEye is { };

    /// <summary>
    /// <c>TrackedFace::GetIntraEyeDistance</c> 0x0087DC68 (C2-F23): the two eye centres' distance divided
    /// by the signed cosine of the roll at +0xec, except that the divisor is 1.0 when <c>|cos| &lt; 1e-5</c>
    /// (the absolute value is only the threshold test). A distance under 1e-5 warns and returns
    /// <c>6.0 / divisor</c>.
    /// </summary>
    public static double GetIntraEyeDistance(Vec2 left, Vec2 right, double rollRad)
    {
        double dx = right.X - left.X, dy = right.Y - left.Y;
        double dist = Math.Sqrt(dx * dx + dy * dy);
        double c = Math.Cos(rollRad);
        double divisor = Math.Abs(c) < MinCosOrDistance ? 1.0 : c;
        return dist < MinCosOrDistance ? MinIntraEyeDistancePx / divisor : dist / divisor;
    }

    /// <summary><c>GetMaxExpression</c>: the highest-scoring expression, Unknown without scores.</summary>
    public FacialExpression MaxExpression()
    {
        var v = Detection.ExpressionValues;
        if (v is null || v.Count == 0) return FacialExpression.Unknown;
        int best = 0; for (int i = 1; i < v.Count; i++) if (v[i] > v[best]) best = i;
        return (FacialExpression)best;
    }

    // fidelity: M14-001
    /// <summary>
    /// <c>TrackedFace::UpdateTranslation(camera)</c> 0x0087DE24 (C2-F23).
    /// The face roll (this+0xec) is the <see cref="DetectedFace.RollRad"/> seam input. A parts face (eye
    /// centres present) with no roll is an incomplete detector input and is not defaulted: it throws,
    /// because the engine always has the parts roll (the production detector is the M11-016 OKAO boundary
    /// recorded by M14-010).
    /// </summary>
    public void UpdateTranslation(CameraModel camera)
    {
        double eyePx;
        Vec2 rayPoint;
        if (HasEyeParts)
        {
            var l = Detection.LeftEye!.Value;
            var r = Detection.RightEye!.Value;
            if (Detection.RollRad is not { } roll)
                throw new NotSupportedException("TrackedFace::UpdateTranslation: the parts branch needs the face roll (this+0xec), which IFaceDetector does not carry (M14-010/M11-016)");
            eyePx = GetIntraEyeDistance(l, r, roll);
            rayPoint = new Vec2((l.X + r.X) / 2, (l.Y + r.Y) / 2);
        }
        else
        {
            // A=(x+0.25w, y+0.375h), B=(x+0.75w, y+0.375h): the x difference is 0.5w, the y difference 0.
            double w = Rect.Width;
            eyePx = Math.Max(MinIntraEyeDistancePx, Math.Abs(0.5 * w));
            // C2-F23: the box branch writes A/B only to scratch slots for the distance and leaves its eye
            // slots zeroed, so the midpoint is pixel (0,0); the engine's invK*(0,0,1) is Ray(pixel (0,0)).
            rayPoint = new Vec2(0, 0);
        }
        DistanceMm = InterPupilDistanceMm * camera.Calibration.FocalLengthX / eyePx;
        var (origin, dir) = camera.Ray(rayPoint);
        var head = origin + dir.Normalized() * DistanceMm;
        HeadPose = new Pose3d(camera.Pose.Rotation, head);
    }
}

/// <summary>
/// <c>Anki::Cozmo::SmartFaceID</c>: a face reference that follows <c>FaceWorld::ChangeFaceID</c> (a tracking
/// id merged into a recognised id) so a behaviour holding it keeps pointing at the same person.
/// </summary>
public sealed class SmartFaceID : IDisposable
{
    public const int Invalid = -1;
    private FaceWorld? _world;
    internal SmartFaceID(FaceWorld? world, int id) { _world = world; Id = id; if (world is not null) world.FaceIdChanged += OnChanged; }
    public SmartFaceID() : this(null, Invalid) { }
    public int Id { get; private set; }
    public bool IsValid => Id != Invalid;
    public bool MatchesFaceID(int id) => Id == id;
    public void Reset() => Id = Invalid;
    private void OnChanged(int oldId, int newId) { if (Id == oldId) Id = newId; }
    public string DebugStr => IsValid ? $"face {Id}" : "no face";
    public override string ToString() => DebugStr;

    /// <summary>
    /// Releases the <see cref="FaceWorld.FaceIdChanged"/> subscription. A smart id lives as long as the action
    /// holding it; without this every face action ever run would stay subscribed for the life of the world.
    /// </summary>
    public void Dispose()
    {
        var world = Interlocked.Exchange(ref _world, null);
        if (world is not null) world.FaceIdChanged -= OnChanged;
    }
}

/// <summary>An entry of the face world (<c>FaceWorld::FaceEntry</c>).</summary>
public sealed class FaceEntry
{
    public int Id { get; internal set; }
    public Pose3d HeadPose { get; internal set; }
    public FaceRect Rect { get; internal set; }
    public uint LastObservedTimestamp { get; internal set; }
    public uint FirstObservedTimestamp { get; internal set; }
    public int TimesObserved { get; internal set; }
    public string? Name { get; internal set; }
    public bool HasName => !string.IsNullOrEmpty(Name);
    public FacialExpression Expression { get; internal set; }
    /// <summary><c>SetTurnedTowardsFace</c> / <c>HasTurnedTowardsFace</c>: whether an action has already turned to this face.</summary>
    public bool TurnedTowards { get; internal set; }
    public override string ToString() => $"face {Id}{(HasName ? " " + Name : "")} at {HeadPose.Translation} t={LastObservedTimestamp}";
}

public sealed record FaceObservation(FaceEntry Face, uint Timestamp, bool IsNew, Pose3d PreviousPose);

/// <summary>
/// The engine's <c>FaceWorld</c> (0x004F3B30..0x004F5B10). <c>AddOrUpdateFace(TrackedFace)</c> (0x004F4278):
/// the robot state at the face's timestamp is looked up (<c>ComputeAndInsertStateAt</c>; "GetComputedStateAtFailed"
/// drops it); a face whose head pose is below the robot is ignored ("IgnoringFaceBelowRobot z=%f"); when the
/// body turned faster than 0.174533 rad/s or the head faster than 0.523599 the observation is skipped
/// (<c>WasRotatingTooFast</c>, the same rule as the markers); the observation matches an existing entry by id,
/// or, when the tracker has no recognition data, by pose within 220 mm (48400 mm², 0x473D1000) and the
/// rectangle overlap (0.5), otherwise "Added new face with ID=%d at t=%d"; an observation older than the
/// entry's last is rejected ("Face observed before previous observation"). <c>RobotObservedFace</c> is
/// broadcast with the pose and the maximum expression. <c>Update()</c> (0x004F52xx): unnamed faces not seen for
/// 15000 ms (0x3A98) are removed ("Removing unnamed face %d at t=%d, because it hasn't been seen since
/// t=%d"; "robot.vision.remove_unobserved_session_only_face"). Queries: <c>GetFaceIDsObservedSince(ts)</c>,
/// <c>HasAnyFaces(ts)</c>, <c>GetLastObservedFace(pose)</c>, <c>GetFace(id)</c>, all restricted to faces whose
/// pose is in the robot's current origin (<c>IsPoseInWorldOrigin</c>: this stack has one origin until a
/// delocalisation, which clears the faces, <c>OnRobotDelocalized</c>).
/// </summary>
public sealed class FaceWorld
{
    // The C2-F7 dead branch's constants: present in the binary (0x004F441A / 0x004F4428) but unreachable,
// because FaceTracker::IsRecognitionSupported 0x0086B244 returns 1 unconditionally. Kept as the record's
// evidence, not used by the reachable match path.
    public const double MatchDistanceMm = 220.0;
    /// <summary>The dead-branch match threshold squared, 220^2 = 48400.0 (0x004F4428 loads 0x473D1000).</summary>
    public const double MatchDistanceSquaredMm = MatchDistanceMm * MatchDistanceMm;
    /// <summary>The dead-branch overlap threshold, 0.5 (0x004F441A).</summary>
    public const double MatchOverlapScore = 0.5;
    public const uint UnnamedFaceLifetimeMs = 15000;
    public const double MaxBodyRotationRadPerSec = 0.174533;
    public const double MaxHeadRotationRadPerSec = 0.523599;

    private readonly object _gate = new();
    private readonly Dictionary<int, FaceEntry> _faces = new();

    // fidelity: M14-008
    public event Action<FaceObservation>? FaceObserved;
    public event Action<int>? FaceDeleted;
    public event Action<int, int>? FaceIdChanged;
    public event Action<string>? Log;

    public IReadOnlyList<FaceEntry> Faces { get { lock (_gate) return _faces.Values.ToList(); } }
    public int Count { get { lock (_gate) return _faces.Count; } }

    /// <summary>Adds or updates a face seen in a frame; null when it was ignored.</summary>
    // fidelity: M14-008, M14-001
    public FaceObservation? AddOrUpdateFace(TrackedFace face, Pose3d robotPoseAtFrame, bool rotatingTooFast)
    {
        if (face.HeadPose.Translation.Z < robotPoseAtFrame.Translation.Z) { Log?.Invoke($"FaceWorld.AddOrUpdateFace.IgnoringFaceBelowRobot z={face.HeadPose.Translation.Z:F1}"); return null; }
        FaceEntry? entry; bool isNew = false; Pose3d prev;
        lock (_gate)
        {
            // C2-F7: the reachable match is the map lookup keyed by the TrackedFace id at +0. The
            // pose/overlap loop is dead because FaceTracker::IsRecognitionSupported 0x0086B244 is
            // `movs r0,#1; bx lr`, so the 0x004F43DE cbz never branches and no generated id exists.
            entry = _faces.TryGetValue(face.Id, out var byId) ? byId : null;
            bool existing = entry is not null;
            if (entry is null)
            {
                // C2-F7c: WasRotatingTooFast 0x004F4596 gates the new-entry path only.
                if (rotatingTooFast) { Log?.Invoke("FaceWorld.AddOrUpdateFace: rotating too fast, skipping"); return null; }
                entry = new FaceEntry { Id = face.Id, FirstObservedTimestamp = face.Timestamp };
                _faces[face.Id] = entry; isNew = true;
                Log?.Invoke($"FaceWorld.UpdateFace.NewFace: Added new face with ID={face.Id} at t={face.Timestamp}.");
            }
            else if (face.Timestamp <= entry.LastObservedTimestamp)
            {
                // C2-F7b: a timestamp regression logs and continues with delta 0; it does not reject.
                Log?.Invoke($"FaceWorld.UpdateFace.BadTimeStamp: Face observed before previous observation ({face.Timestamp} <= {entry.LastObservedTimestamp})");
            }
            prev = entry.HeadPose;
            // C2-F7c: on the found path a no-parts observation keeps the entry's existing translation
            // (0x004F4784..0x004F47A8); its rotation still comes from the TrackedFace. A new entry, and a
            // parts observation, take the face's full pose.
            entry.HeadPose = existing && !face.HasEyeParts
                ? new Pose3d(face.HeadPose.Rotation, prev.Translation)
                : face.HeadPose;
            entry.Rect = face.Rect; entry.LastObservedTimestamp = face.Timestamp; entry.TimesObserved++;
            entry.Expression = face.MaxExpression();
            if (face.Name is { Length: > 0 }) entry.Name = face.Name;
        }
        var obs = new FaceObservation(entry, face.Timestamp, isNew, prev);
        FaceObserved?.Invoke(obs);
        return obs;
    }

    /// <summary><c>FaceWorld::Update</c>: forget unnamed faces not seen for 15 s.</summary>
    // fidelity: M14-008
    public IReadOnlyList<int> Update(uint lastProcessedImageTimestamp)
    {
        var removed = new List<int>();
        lock (_gate)
        {
            foreach (var e in _faces.Values.ToList())
            {
                if (e.HasName) continue;
                if (lastProcessedImageTimestamp > e.LastObservedTimestamp && lastProcessedImageTimestamp - e.LastObservedTimestamp > UnnamedFaceLifetimeMs)
                {
                    Log?.Invoke($"FaceWorld.Update.DeletingOldFace: Removing unnamed face {e.Id} at t={lastProcessedImageTimestamp}, because it hasn't been seen since t={e.LastObservedTimestamp}.");
                    _faces.Remove(e.Id); removed.Add(e.Id);
                }
            }
        }
        foreach (var id in removed) FaceDeleted?.Invoke(id);
        return removed;
    }

    /// <summary><c>ChangeFaceID</c> (the recogniser merged a tracking id into a known face).</summary>
    public bool ChangeFaceID(int oldId, int newId, string? newName = null)
    {
        lock (_gate)
        {
            if (!_faces.TryGetValue(oldId, out var e)) { Log?.Invoke($"FaceWorld.ChangeFaceID.UnknownOldID: ID {oldId} does not exist, cannot update to {newId}"); return false; }
            _faces.Remove(oldId);
            e.Id = newId; if (newName is not null) e.Name = newName;
            _faces[newId] = e;
        }
        Log?.Invoke($"FaceWorld.ChangeFaceID.Success: Updating old face {oldId} to new ID {newId}");
        FaceIdChanged?.Invoke(oldId, newId);
        return true;
    }

    public FaceEntry? GetFace(int id) { lock (_gate) return _faces.GetValueOrDefault(id); }
    public FaceEntry? GetFace(SmartFaceID id) => id.IsValid ? GetFace(id.Id) : null;
    public SmartFaceID GetSmartFaceID(int id) => new(this, id);

    public IReadOnlyList<int> GetFaceIDsObservedSince(uint timestamp, bool namedOnly = false)
    {
        lock (_gate) return _faces.Values.Where(f => ShouldReturnFace(f, timestamp) && (!namedOnly || f.HasName)).Select(f => f.Id).ToList();
    }
    public IReadOnlyList<int> GetFaceIDs(bool namedOnly = false) => GetFaceIDsObservedSince(0, namedOnly);
    public bool HasAnyFaces(uint seenSinceTimestamp = 0, bool namedOnly = false) => GetFaceIDsObservedSince(seenSinceTimestamp, namedOnly).Count > 0;

    // fidelity: M14-009
    /// <summary>
    /// <c>FaceWorld::ShouldReturnFace(entry, time, bool)</c> 0x004F55B8: an entry is returned only when
    /// its last observation is at or after <paramref name="time"/> (the native rejects when entry+8 &lt;
    /// time; entry+8 is the entry's last-observed timestamp, the field the observation queries use), and,
    /// when <paramref name="requireId"/> is set, only when its id is at least 1 (the native rejects when
    /// entry+0 &lt; 1).
    /// </summary>
    /// <remarks>
    /// The engine's third gate is <c>Robot::IsPoseInWorldOrigin(entry+0xF4)</c>. This stack has a single
    /// origin: <see cref="OnRobotDelocalized"/> clears every face when the origin changes, so a stored
    /// face's pose is always in the current origin and that gate cannot reject here.
    /// </remarks>
    public bool ShouldReturnFace(FaceEntry entry, uint time, bool requireId = false)
    {
        if (entry.LastObservedTimestamp < time) return false;
        if (requireId && entry.Id < 1) return false;
        return true;   // Robot::IsPoseInWorldOrigin: faces are cleared on delocalisation (M14-008)
    }

    // fidelity: M14-009
    /// <summary>The enrollment mode <c>FaceWorld::Enroll</c> 0x004F5C9E passes: 4 for a nonzero id.</summary>
    public const int EnrollModeKnownFace = 4;
    /// <summary>The enrollment mode for id 0: -1 (0x004F5CA8 <c>moveq.w r3, #-1</c>).</summary>
    public const int EnrollModeNewFace = -1;

    /// <summary>
    /// <c>FaceWorld::Enroll(int)</c> 0x004F5C9E selects the mode (4 when the id is nonzero, -1 when it is
    /// zero) and forwards id and mode to the VisionComponent at robot+0x258. The tail-call 0x8CAC3C
    /// resolves through VisionComponent/VisionSystem/FaceTracker to
    /// <c>FaceRecognizer::SetAllowedEnrollments</c> 0x008658AC (C1-F11), which stores the mode at
    /// +0x108/+0x10c, the id at +0x100 and the entry data at +0x104.
    /// MISSING: this stack has no FaceRecognizer component; its album semantics are M14-011's
    /// RECOVERABLE_GAP, so only the mode/id selection is built and returned here.
    /// </summary>
    public (int Id, int Mode) Enroll(int id) => (id, id != 0 ? EnrollModeKnownFace : EnrollModeNewFace);

    /// <summary><c>GetLastObservedFace</c>: the most recently seen face's pose.</summary>
    public FaceEntry? GetLastObservedFace(bool namedOnly = false)
    {
        lock (_gate) return _faces.Values.Where(f => !namedOnly || f.HasName).OrderByDescending(f => f.LastObservedTimestamp).FirstOrDefault();
    }

    public void SetTurnedTowardsFace(int id, bool turned) { lock (_gate) if (_faces.TryGetValue(id, out var e)) e.TurnedTowards = turned; }
    public bool HasTurnedTowardsFace(int id) { lock (_gate) return _faces.TryGetValue(id, out var e) && e.TurnedTowards; }

    public void RemoveFaceByID(int id)
    {
        bool removed; lock (_gate) removed = _faces.Remove(id);
        if (removed) { Log?.Invoke($"FaceWorld.RemoveFaceByID: Removing face {id}"); FaceDeleted?.Invoke(id); }
    }

    public void ClearAllFaces() { List<int> ids; lock (_gate) { ids = _faces.Keys.ToList(); _faces.Clear(); } foreach (var id in ids) FaceDeleted?.Invoke(id); }

    /// <summary><c>OnRobotDelocalized</c>: the faces' poses belong to the old origin.</summary>
    public void OnRobotDelocalized() => ClearAllFaces();

    // fidelity: M1-025, M1-015
    /// <summary>
    /// Back to the state right after construction, for a removed robot (CB33, CC26, CC27): no faces, session ids from 1.
    /// Unlike <see cref="ClearAllFaces"/> no FaceDeleted is raised. Subscribers are kept.
    /// </summary>
    internal void ResetToConstructed()
    {
        lock (_gate)
        {
            _faces.Clear();
        }
    }
}

/// <summary>A pet as the detector reports it (the engine's OKAO pet detector fills <c>Vision::TrackedPet</c>).</summary>
public sealed record DetectedPet(int Id, PetType Type, FaceRect Rect, int Score = 0);

/// <summary>The pet detection seam (<c>PetTracker</c> in vision_config.json: max 4 pets, face size 60..240 px, threshold 900). OKAO again; unavailable here.</summary>
public interface IPetDetector
{
    bool IsAvailable { get; }
    IReadOnlyList<DetectedPet> Detect(GrayImage image, uint timestamp);
}

public sealed class OkaoPetDetector : IPetDetector
{
    public bool IsAvailable => false;
    public IReadOnlyList<DetectedPet> Detect(GrayImage image, uint timestamp) => Array.Empty<DetectedPet>();
}

public sealed class PetEntry
{
    public int Id { get; internal set; }
    public PetType Type { get; internal set; }
    public FaceRect Rect { get; internal set; }
    public uint LastObservedTimestamp { get; internal set; }
    public int TimesObserved { get; internal set; }
}

/// <summary>
/// <c>Anki::Cozmo::PetWorld</c> (0x0050B7E4..0x0050BC40): pets by id with the same rotating-too-fast skip
/// (0.174533 / 0.523599), "robot.vision.detected_pet" on a first sighting and <c>RobotObservedPet</c> per
/// observation; <c>GetKnownPetsWithType</c>, <c>GetPetByID</c>. Pets are image-plane entities (no head pose);
/// a pet not reported for <see cref="TrackLostFrames"/> frames is dropped (vision_config <c>TrackLostCount</c> 2).
/// </summary>
public sealed class PetWorld
{
    public const int TrackLostFrames = 2;
    private readonly Dictionary<int, (PetEntry Pet, int Missed)> _pets = new();
    public event Action<PetEntry, bool>? PetObserved;
    public IReadOnlyList<PetEntry> Pets => _pets.Values.Select(p => p.Pet).ToList();

    public void Update(IReadOnlyList<DetectedPet> seen, uint timestamp, bool rotatingTooFast)
    {
        if (rotatingTooFast) return;
        var ids = new HashSet<int>();
        foreach (var d in seen)
        {
            ids.Add(d.Id);
            bool isNew = !_pets.TryGetValue(d.Id, out var cur);
            var pet = isNew ? new PetEntry { Id = d.Id, Type = d.Type } : cur.Pet;
            pet.Rect = d.Rect; pet.LastObservedTimestamp = timestamp; pet.TimesObserved++; pet.Type = d.Type;
            _pets[d.Id] = (pet, 0);
            PetObserved?.Invoke(pet, isNew);
        }
        foreach (var id in _pets.Keys.ToList())
        {
            if (ids.Contains(id)) continue;
            var (pet, missed) = _pets[id];
            if (++missed > TrackLostFrames) _pets.Remove(id); else _pets[id] = (pet, missed);
        }
    }

    public PetEntry? GetPetByID(int id) => _pets.TryGetValue(id, out var p) ? p.Pet : null;

    // fidelity: M1-025, M1-015
    /// <summary>Back to the state right after construction, for a removed robot (CB33, CC26, CC27): no pets. Subscribers are kept.</summary>
    internal void ResetToConstructed() => _pets.Clear();
    public IReadOnlyList<PetEntry> GetKnownPetsWithType(PetType type) => _pets.Values.Select(p => p.Pet).Where(p => type == PetType.Unknown || p.Type == type).ToList();
}
