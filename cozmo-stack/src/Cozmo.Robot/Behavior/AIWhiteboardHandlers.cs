using Cozmo.Protocol;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Behavior;

/// <summary>One entry of the whiteboard's possible-object list: the pose a possible object was seen at and its type.</summary>
public readonly record struct PossibleObject(Pose3d Pose, ObjectType Type);

/// <summary>
/// The three MessageEngineToGame handlers <c>AIWhiteboard::Init</c> registers (tags 0x44 = 68, 0x45 = 69, 0x35 = 53;
/// subscriptions 0x0056a44c, 0x0056a50c, 0x0056a5cc), built from inventory M8-framework Correction A4 (Codex pre-extraction section 6 with
/// the 2026-09-30 check applied). The production recipient uses the owning robot, its BlockWorld,
/// and the possible-object list on the same whiteboard. Standalone callers can supply those inputs explicitly.
/// </summary>
// fidelity: M8-014
public sealed class AIWhiteboardHandlers
{
    /// <summary>Squared 3-D distance of the same-type removal: 2500.0f, 0x451C4000 (0x0056ad76..0x0056ae12).</summary>
    public static readonly float RemoveDistanceSq = BitConverter.Int32BitsToSingle(0x451C4000);
    /// <summary>The tilt limit: 10 degrees as a float, 0x3E32B8C2 (0x0056c502..0x0056c50c).</summary>
    public static readonly float TiltLimitRad = BitConverter.Int32BitsToSingle(0x3E32B8C2);
    /// <summary>The robot-relative Z ceiling: 30.0f, 0x41F00000 (0x0056c560..0x0056c576).</summary>
    public static readonly float MaxRelativeZ = BitConverter.Int32BitsToSingle(0x41F00000);
    /// <summary>The per-axis threshold passed to <c>FindLocatedClosestMatchingTypeHelper</c>: 50.0f, 0x42480000 (0x0056c588..0x0056c5a4).</summary>
    public static readonly float WorldMatchAxisMm = BitConverter.Int32BitsToSingle(0x42480000);
    /// <summary>The angular threshold passed with it: pi, 0x40490FDB.</summary>
    public static readonly float WorldMatchAngleRad = BitConverter.Int32BitsToSingle(0x40490FDB);
    /// <summary>The possible-object list cap: ten (0x0056c676..0x0056c682).</summary>
    public const int MaxPossibleObjects = 10;
    /// <summary>The OffTreadsState value that records the time: 0, OnTreads (0x0056cce4..0x0056ccec).</summary>
    public const byte OnTreads = 0;

    private readonly AIWhiteboard _whiteboard;
    private readonly Func<double> _nowSec;
    // fidelity: M8-026 — one list at AIWhiteboard+54, shared with ProcessClearQuad.
    private PossibleObjectStorage _possible => _whiteboard.PossibleObjectStorage;

    public AIWhiteboardHandlers(AIWhiteboard whiteboard, Func<double> nowSec)
    {
        _whiteboard = whiteboard;
        _nowSec = nowSec;
    }

    private readonly bool _nativePoseTree;
    internal AIWhiteboardHandlers(AIWhiteboard whiteboard, EngineRobot robot, BlockWorld world)
        : this(whiteboard, () => robot.Engine.Timer.SecondsF)
    {
        _nativePoseTree = true;
        RobotPose = () => robot.MainPose; // Robot::GetPose, 0x0056C54C
        SourceLog = (level, channel, key, message) => robot.Engine.EmitSourceLog(level, channel, key, message);
        FindLocatedClosestMatchingType = (type, pose, tolerance, angle, filter) =>
        {
            // 0x0061FC2E..0x0061FC84 copies the filter and visits every match in order.
            // The predicate at 0x006281F2..0x00628256 narrows the tolerances after each hit.
            var copy = filter.Clone();
            copy.Predicates.Add(obj =>
            {
                if (obj.Type != type) return false;
                var maxAngle = new Radians(angle);
                var difference = new Radians(0);
                var delta = Vec3.Zero;
                if (!obj.NativePose.IsSameAsWithAmbiguity(pose, tolerance, maxAngle, ref delta,
                    ref difference, Array.Empty<Mat3>(), false)) return false;
                tolerance = new Vec3(Math.Abs(delta.X), Math.Abs(delta.Y), Math.Abs(delta.Z));
                angle = Math.Abs(difference.Value);
                return true;
            });
            return world.FindLocatedObjectHelper(copy, null, false) is not null;
        };
    }

    private bool TryRelative(Pose3d pose, Pose3d frame, out Vec3 translation)
    {
        if (_nativePoseTree)
        {
            using var relative = new Pose3d("");
            bool converted = Pose3d.GetWithRespectTo(pose, frame, relative);
            translation = relative.Translation;
            return converted;
        }
        using var flattened = pose.WithRespectTo(frame);
        translation = flattened.Translation;
        return true;
    }

    /// <summary>The possible-object list, oldest first (the cap pops the front).</summary>
    public IReadOnlyList<PossibleObject> PossibleObjects => _possible;

    /// <summary>
    /// <c>Robot::GetPose()</c> (0x004EA398, called at 0x0056c54c): the robot's pose; null is a failed relative-pose conversion. Unset reports MISSING once
    /// and the tag-69 handler skips.
    /// </summary>
    public Func<Pose3d?>? RobotPose { get; set; }

    /// <summary>
    /// The tilt of a pose in radians (0x0056c4d2..0x0056c4f2): <c>GetRotationMatrix</c> (0x4a4054), then
    /// <c>RotationMatrix3d::GetRotatedParentAxis&lt;'Z'&gt;</c> (PLT 0x4ab8e4 -> 0x5507a0..0x550854), which reads the matrix row 2,
    /// <c>(R[2,0], R[2,1], R[2,2])</c> (<c>SmallMatrix::operator()(2, i)</c>, 0x557890), and keeps the signed component of largest magnitude:
    /// <c>s = v0; if |v1| &gt; |v0| then s = v1; if |v2| &gt; |s| then s = v2</c> (strict compares, 0x5507c8..0x55084a). The tilt is
    /// <c>acosf(|s|)</c> (the <c>bic #0x80000000</c> at 0x0056c4ec, <c>acosf</c> 0x4acc4c), all in float.
    /// </summary>
    public Func<Pose3d, float> TiltRadiansOf { get; set; } = RotatedParentZTilt;

    /// <summary>The default <see cref="TiltRadiansOf"/>.</summary>
    public static float RotatedParentZTilt(Pose3d pose)
    =>RotatedParentZTiltMatrix(pose.Rotation);

    public static float RotatedParentZTiltMatrix(Mat3 matrix)
    {
        float v0 = (float)matrix[2, 0], v1 = (float)matrix[2, 1], v2 = (float)matrix[2, 2];
        float s = v0;
        if (MathF.Abs(v1) > MathF.Abs(v0)) s = v1;     // 0x5507c8..
        if (MathF.Abs(v2) > MathF.Abs(s)) s = v2;      // ..0x55084a
        return MathF.Acos(MathF.Abs(s));
    }

    /// <summary>
    /// <c>BlockWorld::FindLocatedClosestMatchingTypeHelper</c> as the tag-69 handler calls it (type, pose, per-axis threshold, angle threshold, a
    /// zero-initialised <see cref="BlockWorldFilter"/>, 0x0056c5ac..0x0056c606, passed at 0x0056c60c): true when a located object is found. The native binding is implemented
    /// at 0x0061FBFC. Standalone handlers require an explicit world.
    /// </summary>
    public Func<ObjectType, Pose3d, Vec3, float, BlockWorldFilter, bool>? FindLocatedClosestMatchingType { get; set; }

    private bool _reportedRobotPoseMissing, _reportedFindMissing;

    /// <summary>The possible-object visualisation redraw (<c>VizManager</c>, M11/M12; 0x0056c464 and 0x0056c686..0x0056c692).</summary>
    public event Action? PossibleObjectsRedrawn;

    /// <summary>Log lines.</summary>
    public event Action<string>? Log;
    public SourceLogSink? SourceLog { get; set; }

    /// <summary>
    /// Tag 0x44 (<c>RobotObservedObject</c>, handler 0x0056c448..0x0056c476): removes every possible-object entry of the same type whose pose
    /// expressed relative to the observed pose has a squared 3-D distance of at most 2500.0f, then redraws.
    /// </summary>
    public void HandleRobotObservedObject(Pose3d observedPose, ObjectType type)
    {
        RemoveNearbyPossibleObjects(observedPose, type);
        PossibleObjectsRedrawn?.Invoke();                                                  // 0x0056c464
    }

    /// <summary>
    /// The shared removal (0x0056ad76..0x0056ae12): same type (0x0056ad76..0x0056ad88), pose expressible relative to the observed pose, squared distance
    /// computed in float and at most <see cref="RemoveDistanceSq"/>. Entries whose pose cannot be expressed in this frame are retained.
    /// </summary>
    private void RemoveNearbyPossibleObjects(Pose3d observedPose, ObjectType type)
    {
        _possible.RemoveAll(p =>
        {
            if (p.Type != type) return false;
            if (!TryRelative(p.Pose, observedPose, out var rel)) return false;
            float x = (float)rel.X, y = (float)rel.Y, z = (float)rel.Z;
            float distSq = x * x + y * y + z * z;
            return distSq <= RemoveDistanceSq;
        });
    }

    /// <summary>
    /// Tag 0x45 (<c>RobotObservedPossibleObject</c>, handler 0x0056c48a; <c>ConsiderNewPossibleObject</c>):
    /// <list type="number">
    /// <item>the tilt must be below 10 degrees through <c>Anki::operator&lt;(Radians, Radians)</c> (0x0056c4d2..0x0056c51a), not a raw float compare;</item>
    /// <item>the pose must be expressible relative to the robot pose, else it logs and stops (0x0056c54a..0x0056c55c);</item>
    /// <item>the robot-relative Z must be at most 30.0f (0x0056c560..0x0056c576);</item>
    /// <item>nearby same-type entries are removed (0x0056c57a..0x0056c584);</item>
    /// <item><c>FindLocatedClosestMatchingTypeHelper</c> with per-axis 50.0f and pi (0x0056c588..0x0056c61a): a hit means nothing is retained;</item>
    /// <item>otherwise a list already at ten pops its front, the new entry is appended and the visualisation redrawn (0x0056c676..0x0056c692).</item>
    /// </list>
    /// </summary>
    public void HandleRobotObservedPossibleObject(Pose3d pose, ObjectType type)
    {
        if (!(new Radians(TiltRadiansOf(pose)) < new Radians(TiltLimitRad))) return;       // 0x0056c4d2..0x0056c51a

        if (RobotPose is not { } robotPose)
        {
            if (!_reportedRobotPoseMissing) { _reportedRobotPoseMissing = true; Log?.Invoke("MISSING: M8-014: Robot::GetPose() (0x004EA398) is not supplied; the possible object is skipped"); }
            return;
        }
        if (robotPose() is not { } robot || !TryRelative(pose, robot, out var relative))
        {
            // M8-014 row 6.2 (research/20261002-R-BEH2-M8-gap1-extraction.md): a failed GetWithRespectTo (0x0056c55c beq.w 0x56c698)
            // calls sWarningF with an empty data vector (0x0056c698..0x0056c6a2), key 0x0056c77c, format 0x0056c7a4 and no varargs
            // (0x0056c6a6), then discards the possible object.
            SourceLogDispatch.GetSink(SourceLog, Log)?.Invoke(EngineLogLevel.Warning, null, "AIWhiteboard.PossibleObject.NoRelPose", "Couldnt get pose WRT to robot");
            return;
        }
        float relZ = (float)relative.Z;
        if (relZ > MaxRelativeZ) return;                                                   // 0x0056c560..0x0056c576

        RemoveNearbyPossibleObjects(pose, type);                                           // 0x0056c57a..0x0056c584
        if (FindLocatedClosestMatchingType is not { } find)
        {
            if (!_reportedFindMissing) { _reportedFindMissing = true; Log?.Invoke("MISSING: M8-014: BlockWorld::FindLocatedClosestMatchingTypeHelper's body is not in the inventory; the possible object is skipped"); }
            return;
        }
        float axis = WorldMatchAxisMm;
        if (find(type, pose, new Vec3(axis, axis, axis), WorldMatchAngleRad, new BlockWorldFilter())) return;   // 0x0056c5a8..0x0056c61a

        if (_possible.Count >= MaxPossibleObjects) _possible.RemoveAt(0);                  // 0x0056c676..0x0056c682
        _possible.Add(new PossibleObject(pose, type));                                     // 0x0056c686
        PossibleObjectsRedrawn?.Invoke();                                                  // 0x0056c692
    }

    /// <summary>
    /// Tag 0x35 (<c>RobotOffTreadsStateChanged</c>, adapter 0x0056ccdc..0x0056ccf8): reads the one-byte state; nonzero returns, zero (OnTreads) stores
    /// <c>BaseStationTimer::GetCurrentTimeInSeconds()</c> at whiteboard +0x48 (0x0056ccee..0x0056ccf6) through
    /// <see cref="AIWhiteboard.RecordOffTreadsStateChanged"/>.
    /// </summary>
    public void HandleRobotOffTreadsStateChanged(byte state)
    {
        if (state != OnTreads) return;                                                     // 0x0056cce4..0x0056ccec
        // GetCurrentTimeInSeconds (0x4a50b0) returns a float and the adapter does `str r0,[r4,#0x48]`, a 32-bit store (0x0056ccee..0x0056ccf6).
        // R-VIS: AIWhiteboard.OffTreadsStateChangedAtSec should be a float; it is a double, so the narrowed value is stored.
        _whiteboard.RecordOffTreadsStateChanged((double)(float)_nowSec());
    }
}

/// <summary>The two <c>NamedColors</c> <c>UpdateBeaconRender</c> chooses between (0x0056aab0 ORANGE, 0x0056aae2 DARKGREEN).</summary>
public enum BeaconColor { DarkGreen, Orange }

/// <summary>
/// The <c>VizManager</c> calls <c>UpdateBeaconRender</c> makes (<c>EraseSegments</c> PLT 0x004acb20, <c>DrawXYCircleAsSegments&lt;float&gt;</c> PLT
/// 0x004acb50). <c>VizManager</c> is M11/M12's: nothing in this stack implements it.
/// </summary>
public interface IBeaconVizSink
{
    void EraseSegments(string name);
    void DrawXYCircleAsSegments(float centerX, float centerY, float centerZ, float radius, BeaconColor color, bool connectLastToFirst, int segments, float finalArgument);
}

/// <summary>
/// <c>AIWhiteboard::UpdateBeaconRender</c> 0x0056aa3c..0x0056abff. It erases the "AIWhiteboard.UpdateBeaconRender" segments (0x0056aa4c..0x0056aa9e),
/// then for each 20-byte <see cref="AIBeacon"/> record (0x0056aaa0..0x0056aaa8, 0x0056abea..0x0056abf0) draws three XY circles at the beacon's
/// translation with z + 35.0f (0x420C0000, 0x0056ab1c..0x0056ab28), radii R, R - 0.5f, R - 1.0f (R at beacon +0x0C), 8 segments,
/// connectLastToFirst false and a final float 0.0 (0x0056ab40..0x0056abd8), DARKGREEN when abs(beacon +0x10) &lt; 1e-5f (0x3727C5AC)
/// and ORANGE otherwise (0x0056aad6..0x0056ab02). It only draws; it changes no behaviour.
///
/// Beacon positions use the root-frame pose returned by 0x004DF628. Colors use the timestamp stored at beacon+0x10.
/// <see cref="Attach"/> hooks <see cref="AIWhiteboard.BeaconRenderUpdated"/>.
/// </summary>
// fidelity: M8-014
public sealed class AIWhiteboardBeaconRenderer
{
    /// <summary>The static name the segments are filed under.</summary>
    public const string SegmentName = "AIWhiteboard.UpdateBeaconRender";
    /// <summary>The circle centre's lift: 35.0f, 0x420C0000.</summary>
    public static readonly float CenterZOffset = BitConverter.Int32BitsToSingle(0x420C0000);
    /// <summary>The colour test's epsilon: 1e-5f, 0x3727C5AC.</summary>
    public static readonly float FailureTimeEpsilon = BitConverter.Int32BitsToSingle(0x3727C5AC);
    /// <summary>The second circle's radius step: -0.5f, 0xBF000000 (added to R).</summary>
    public static readonly float SecondRadiusStep = BitConverter.Int32BitsToSingle(unchecked((int)0xBF000000));
    /// <summary>The third circle's radius step: -1.0f, 0xBF800000 (added to R).</summary>
    public static readonly float ThirdRadiusStep = BitConverter.Int32BitsToSingle(unchecked((int)0xBF800000));
    /// <summary>The segment count of every circle.</summary>
    public const int Segments = 8;

    private readonly IBeaconVizSink _sink;

    public AIWhiteboardBeaconRenderer(IBeaconVizSink sink) { _sink = sink; }

    /// <summary>Optional replacement for the root-frame pose helper at 0x004DF628.</summary>
    public Func<AIBeacon, Pose3d>? ResolveBeaconPose { get; set; }

    /// <summary>The beacon's +0x10 float: the time <c>AIBeacon::FailedToFindLocation</c> (0x0059c314..0x0059c322) last recorded, 0 until then. Defaults to <see cref="AIBeacon.FailedToFindLocationTimeSec"/> (M15-023).</summary>
    public Func<AIBeacon, float>? LastFailureTimeOf { get; set; } = b => b.FailedToFindLocationTimeSec;

    /// <summary>MISSING reports (once per seam).</summary>
    public event Action<string>? Log;
    public SourceLogSink? SourceLog { get; set; }

    /// <summary>Hooks the whiteboard's render seam so every <c>AddBeacon</c> renders (<c>AddBeacon</c> calls <c>UpdateBeaconRender</c>, 0x0056c3de).</summary>
    public void Attach(AIWhiteboard whiteboard) => whiteboard.BeaconRenderUpdated += () => Render(whiteboard.Beacons);

    /// <summary>The render itself.</summary>
    public void Render(IReadOnlyList<AIBeacon> beacons)
    {
        _sink.EraseSegments(SegmentName);                                                  // 0x0056aa88..0x0056aa9e
        foreach (var beacon in beacons)
        {
            // 0x004DF628 as called at 0x0056AADA: copy a root, otherwise express the pose in its root.
            using var ownedPose = ResolveBeaconPose is null ? beacon.Pose.GetWithRespectToRoot() : null;
            var t = (ownedPose ?? ResolveBeaconPose!(beacon)).Translation;
            float cx = (float)t.X, cy = (float)t.Y, cz = (float)t.Z + CenterZOffset;       // 0x0056ab1c..0x0056ab28
            var color = MathF.Abs(LastFailureTimeOf?.Invoke(beacon) ?? beacon.FailedToFindLocationTimeSec) < FailureTimeEpsilon ? BeaconColor.DarkGreen : BeaconColor.Orange;
            float r = (float)beacon.RadiusMm;                                              // beacon +0x0C
            _sink.DrawXYCircleAsSegments(cx, cy, cz, r, color, false, Segments, 0f);                          // 0x0056ab40..0x0056ab50
            _sink.DrawXYCircleAsSegments(cx, cy, cz, r + SecondRadiusStep, color, false, Segments, 0f);       // 0x0056ab7a..0x0056ab94
            _sink.DrawXYCircleAsSegments(cx, cy, cz, r + ThirdRadiusStep, color, false, Segments, 0f);        // 0x0056abbe..0x0056abd8
        }
    }
}
