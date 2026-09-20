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
public sealed record DetectedFace(int Id, FaceRect Rect, Vec2? LeftEye = null, Vec2? RightEye = null, IReadOnlyList<byte>? ExpressionValues = null, string? Name = null, int Score = 0);

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
/// (0x0087DE24): the eye midpoint and intra-eye distance come from the detected eyes when present, otherwise
/// from the rectangle (midpoint = centre + (0, −0.125 h); eyes at ±0.25 w, so the distance is 0.5 w), floored
/// at 6 px; the head lies along the camera ray through the midpoint at a distance of
/// 62 mm (0x42780000, the human inter-pupil distance) × focal length / intra-eye pixels, and the head pose is
/// parented to the camera pose (<c>SetParent</c>), so it lands in the world.
/// </summary>
public sealed class TrackedFace
{
    public const double InterPupilDistanceMm = 62.0;
    public const double MinIntraEyeDistancePx = 6.0;

    public TrackedFace(DetectedFace d, uint timestamp) { Detection = d; Timestamp = timestamp; }

    public DetectedFace Detection { get; }
    public uint Timestamp { get; }
    public int Id => Detection.Id;
    public FaceRect Rect => Detection.Rect;
    public string? Name => Detection.Name;
    public Pose3d HeadPose { get; private set; } = Pose3d.Identity;
    public double DistanceMm { get; private set; }

    public (Vec2 Midpoint, double IntraEyeDistancePx) EyeGeometry()
    {
        if (Detection.LeftEye is { } l && Detection.RightEye is { } r)
            return (new Vec2((l.X + r.X) / 2, (l.Y + r.Y) / 2), Math.Max(MinIntraEyeDistancePx, (r - l).Length));
        var c = Rect.Center;
        var left = new Vec2(c.X - 0.25 * Rect.Width, c.Y - 0.125 * Rect.Height);
        var right = new Vec2(c.X + 0.25 * Rect.Width, c.Y - 0.125 * Rect.Height);
        return (new Vec2(c.X, c.Y - 0.125 * Rect.Height), Math.Max(MinIntraEyeDistancePx, (right - left).Length));
    }

    /// <summary><c>GetMaxExpression</c>: the highest-scoring expression, Unknown without scores.</summary>
    public FacialExpression MaxExpression()
    {
        var v = Detection.ExpressionValues;
        if (v is null || v.Count == 0) return FacialExpression.Unknown;
        int best = 0; for (int i = 1; i < v.Count; i++) if (v[i] > v[best]) best = i;
        return (FacialExpression)best;
    }

    public void UpdateTranslation(CameraModel camera)
    {
        var (mid, eyePx) = EyeGeometry();
        DistanceMm = InterPupilDistanceMm * camera.Calibration.FocalLengthX / eyePx;
        var (origin, dir) = camera.Ray(mid);
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
    public const double MatchDistanceMm = 220.0;
    public const double MatchOverlapScore = 0.5;
    public const uint UnnamedFaceLifetimeMs = 15000;
    public const double MaxBodyRotationRadPerSec = 0.174533;
    public const double MaxHeadRotationRadPerSec = 0.523599;

    private readonly object _gate = new();
    private readonly Dictionary<int, FaceEntry> _faces = new();
    private int _nextSessionId = 1;

    public event Action<FaceObservation>? FaceObserved;
    public event Action<int>? FaceDeleted;
    public event Action<int, int>? FaceIdChanged;
    public event Action<string>? Log;

    public IReadOnlyList<FaceEntry> Faces { get { lock (_gate) return _faces.Values.ToList(); } }
    public int Count { get { lock (_gate) return _faces.Count; } }

    /// <summary>Adds or updates a face seen in a frame; null when it was ignored.</summary>
    public FaceObservation? AddOrUpdateFace(TrackedFace face, Pose3d robotPoseAtFrame, bool rotatingTooFast)
    {
        if (face.HeadPose.Translation.Z < robotPoseAtFrame.Translation.Z) { Log?.Invoke($"FaceWorld.AddOrUpdateFace.IgnoringFaceBelowRobot z={face.HeadPose.Translation.Z:F1}"); return null; }
        if (rotatingTooFast) { Log?.Invoke("FaceWorld.AddOrUpdateFace: rotating too fast, skipping"); return null; }
        FaceEntry? entry; bool isNew = false; Pose3d prev;
        lock (_gate)
        {
            entry = face.Id > 0 && _faces.TryGetValue(face.Id, out var byId) ? byId : null;
            if (entry is null)
            {
                // no recognition data: match by pose and rectangle
                FaceEntry? best = null; double bestD = MatchDistanceMm * MatchDistanceMm;
                foreach (var e in _faces.Values)
                {
                    var d = e.HeadPose.Translation - face.HeadPose.Translation;
                    double d2 = d.X * d.X + d.Y * d.Y;
                    if (d2 < bestD || e.Rect.OverlapScore(face.Rect) >= MatchOverlapScore) { bestD = d2; best = e; }
                }
                entry = best;
            }
            if (entry is not null && face.Timestamp < entry.LastObservedTimestamp)
            {
                Log?.Invoke($"FaceWorld.UpdateFace.BadTimeStamp: Face observed before previous observation ({face.Timestamp} <= {entry.LastObservedTimestamp})");
                return null;
            }
            if (entry is null)
            {
                int id = face.Id > 0 ? face.Id : _nextSessionId++;
                while (_faces.ContainsKey(id)) id = _nextSessionId++;
                entry = new FaceEntry { Id = id, FirstObservedTimestamp = face.Timestamp };
                _faces[id] = entry; isNew = true;
                Log?.Invoke($"FaceWorld.UpdateFace.NewFace: Added new face with ID={id} at t={face.Timestamp}.");
            }
            prev = entry.HeadPose;
            entry.HeadPose = face.HeadPose; entry.Rect = face.Rect; entry.LastObservedTimestamp = face.Timestamp; entry.TimesObserved++;
            entry.Expression = face.MaxExpression();
            if (face.Name is { Length: > 0 }) entry.Name = face.Name;
        }
        var obs = new FaceObservation(entry, face.Timestamp, isNew, prev);
        FaceObserved?.Invoke(obs);
        return obs;
    }

    /// <summary><c>FaceWorld::Update</c>: forget unnamed faces not seen for 15 s.</summary>
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
        lock (_gate) return _faces.Values.Where(f => f.LastObservedTimestamp >= timestamp && (!namedOnly || f.HasName)).Select(f => f.Id).ToList();
    }
    public IReadOnlyList<int> GetFaceIDs(bool namedOnly = false) => GetFaceIDsObservedSince(0, namedOnly);
    public bool HasAnyFaces(uint seenSinceTimestamp = 0, bool namedOnly = false) => GetFaceIDsObservedSince(seenSinceTimestamp, namedOnly).Count > 0;

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
    public IReadOnlyList<PetEntry> GetKnownPetsWithType(PetType type) => _pets.Values.Select(p => p.Pet).Where(p => type == PetType.Unknown || p.Type == type).ToList();
}
