using System.Globalization;
using System.Text;
using Cozmo.Protocol;

namespace Cozmo.Robot;

/// <summary>Why a motion call finished.</summary>
public enum MotionResult
{
    /// <summary>
    /// Done. For a head or lift move this is the engine's action success: the motor was already in position
    /// (nothing sent), or the robot acknowledged the action and then reported the motor in position and stopped
    /// (M4-016). For a wheel command and StopAll it is this stack's confirmation against the reported state.
    /// </summary>
    Acknowledged,
    /// <summary>Nothing came back in time. The command was sent; whether it took effect is unknown.</summary>
    TimedOut,
    /// <summary>Refused before anything was sent, because the robot was not in a state to accept it.</summary>
    Refused,
    /// <summary>The engine's action failure: see <see cref="MotionOutcome.EngineResult"/> (0x04000004, 0x03000016, 0x03000018).</summary>
    Failed,
}

/// <summary>Outcome of a motion call, with whatever the robot said about it.</summary>
public sealed record MotionOutcome(MotionResult Result, string Detail)
{
    public bool Ok => Result == MotionResult.Acknowledged;
    /// <summary>The engine's ActionResult code for a <see cref="MotionResult.Failed"/> move, else null.</summary>
    public uint? EngineResult { get; init; }
    public override string ToString() => $"{Result}: {Detail}";
}

/// <summary>
/// Cozmo's motors: wheels, head and lift - the engine's MovementComponent and its head and lift actions (M4).
///
/// Head and lift moves follow the game-message path (M4-003, MD1): the caller's speed, acceleration and duration
/// go into the action, with the values the original app passes as the defaults (head 10/20, lift 10/20, duration
/// 0). Nothing is sent when the motor is already in position; otherwise the move completes when the robot has
/// acknowledged its action id and then reports the motor in position and stopped (M4-016).
///
/// Direct drive (DriveWheels, MoveHead, MoveLift) locks the matching animation track while its speed is non-zero,
/// which sends DisableAnimTracks, and unlocks it at zero, which sends EnableAnimTracks (M4-014).
///
/// There is no calibration gate on direct motion (M4-004): the engine has none. One policy of this stack sits on top:
/// a wheel command is confirmed against the reported wheel speeds (M4-006).
/// </summary>
public sealed class CozmoMotion
{
    private readonly CozmoRobot _robot;
    private readonly object _gate = new();

    internal CozmoMotion(CozmoRobot robot)
    {
        _robot = robot;
        Path = new PathComponent(robot);
        // fidelity: M4-003, M4-016
        // Batch 2: the head/lift actions are IActionRunner subclasses queued on Robot::Update's ActionList
        // (0x005140BC), so the list tick drives their IAction::UpdateInternal timeout/CheckIfDone
        // (0x00540D1A..0x00540E80). The old per-tick ActionRunnerUpdate hook is gone.
    }

    // ------------------------------------------------------------ MovementComponent state

    // fidelity: M4-005
    /// <summary>MC+8: 0 at construction (0x0063DA7C).</summary>
    private byte _actionIdCounter;
    // fidelity: M13-021
    /// <summary>MovementComponent's ObjectID (+0x1C, value +0x20), initialized to -1 at 0x0063DA86.
    /// The tracking-action writers remain owned by M4/M13; with no tracking action this stays -1.</summary>
    internal int HeadTrackingObjectId { get; set; } = -1;

    /// <summary>Track bits (M4-014): HEAD 1, LIFT 2, BODY 4.</summary>
    public const byte HeadTrack = 1, LiftTrack = 2, BodyTrack = 4;

    // fidelity: M4-031, M4-014
    /// <summary>
    /// MovementComponent::LockInfo, the node value of one lock tree: the first string at node+0x10 is the key the tree is
    /// ordered and searched by, the second at node+0x1C is printed by PrintLockState (0x00640098 LockTracks takes both;
    /// 0x0063FE5C UnlockTracks builds its search entry from the key and an empty second string).
    /// </summary>
    private sealed record LockInfo(string Key, string Debug);

    /// <summary>
    /// The lock sets per track bit (MC+0x28 + 12*bit: a std::multiset&lt;LockInfo&gt;, begin/end/count). Kept in tree order:
    /// ordered by the key's bytes (memcmp over the shorter length, then the shorter first, as the inline compare in
    /// 0x00642522..0x00642572 does), an equal key inserted after the existing ones (__emplace_multi).
    /// </summary>
    private readonly List<LockInfo>[] _trackLocks = { new(), new(), new(), new(), new(), new(), new(), new() };

    private static int CompareKeys(string a, string b) =>
        Encoding.UTF8.GetBytes(a).AsSpan().SequenceCompareTo(Encoding.UTF8.GetBytes(b));

    /// <summary>__tree::find: the lowest entry whose key equals <paramref name="key"/>, or -1.</summary>
    private static int FindLock(List<LockInfo> tree, string key)
    {
        for (int i = 0; i < tree.Count; i++)
        {
            int c = CompareKeys(tree[i].Key, key);
            if (c < 0) continue;                       // lower_bound: first entry not less than the key
            return CompareKeys(key, tree[i].Key) < 0 ? -1 : i;
        }
        return -1;
    }

    /// <summary>__emplace_multi: insert after every entry whose key is not greater.</summary>
    private static void InsertLock(List<LockInfo> tree, LockInfo info)
    {
        int at = tree.Count;
        for (int i = 0; i < tree.Count; i++)
            if (CompareKeys(info.Key, tree[i].Key) < 0) { at = i; break; }
        tree.Insert(at, info);
    }
    /// <summary>The direct drive's lock holder, MC+0xC4.</summary>
    // fidelity: M4-026, M4-014
    /// <summary>
    /// The five constructor strings 0x0063DA5C..0x0063DB38 (20261010-m4-movement-leftover-rows.md row 1): +0xBC, +0xC0, +0xC4, +0xC8,
    /// +0xCC (and +0xD0 "OnChargerInSDK" below). They are lock keys and debug names of the direct-drive helper.
    /// </summary>
    private const string DirectDriveWheels = "DirectDriveWheels", DirectDriveHead = "DirectDriveHead", DirectDriveLift = "DirectDriveLift",
                         DirectDriveArc = "DirectDriveArc", DirectDriveTurnInPlace = "DirectDriveTurnInPlace";
    /// <summary>MC+0xB8 (body), +0xB9 (head), +0xBA (lift): this direct drive holds the track.</summary>
    private bool _ddBody, _ddHead, _ddLift;
    /// <summary>
    /// MC+0xD4, "direct drive is disabled": 0 from the constructor (0x0063DB0C). Nothing in this stack sets it (its
    /// writers are not in the M4 inventory), so it stays 0.
    /// </summary>
    private bool _directDriveDisabled;

    // fidelity: M4-001
    /// <summary>Robot+0x2FC: −0.436332 from the constructor (MA22, 0x0051007C..0x00510086); written by RS6.</summary>
    private float _headAngle = MinHeadAngleRad;
    /// <summary>MC+0xA, +0xB, +0xC: head, lift and body moving (M2 status bits: !HEAD_IN_POS, !LIFT_IN_POS, ARE_WHEELS_MOVING).</summary>
    private bool _headMoving, _liftMoving, _bodyMoving;
    /// <summary>Robot+0x300: the lift angle from the last handled state (RS7), radians; null before any.</summary>
    private float? _liftAngle;

    private readonly List<MoveAction> _actions = new();

    // fidelity: M1-025, M1-015
    /// <summary>
    /// Back to the state right after construction, for a removed robot (CB33, CC26, CC27): the action-id and lock-owner
    /// counters at 0, no track locks, direct drive holding nothing, the head at −25° and no motor moving. A move in
    /// flight ends as timed out without anything sent.
    /// </summary>
    internal void ResetToConstructed()
    {
        MoveAction[] ending;
        lock (_gate)
        {
            _actionIdCounter = 0;
            foreach (var s in _trackLocks) s.Clear();
            _ddBody = _ddHead = _ddLift = false;
            _directDriveDisabled = false;
            _headAngle = MinHeadAngleRad;
            HeadTrackingObjectId = -1;
            _headMoving = _liftMoving = _bodyMoving = false;
            _liftAngle = null;
            ending = _actions.ToArray();
            _actions.Clear();
            Path.ResetToConstructed();
        }
        // M1-025: a move in flight ends as timed out without anything sent. The queued runners are cancelled and
        // deleted (which runs their destructor tail); the Done tasks already cleared above complete here.
        _robot.Engine.Robot?.ActionList.Cancel(-1);
        foreach (var a in ending) a.Done.TrySetResult(new MotionOutcome(MotionResult.TimedOut, $"{a.What}: the robot was removed"));
    }

    // fidelity: M4-003
    /// <summary>
    /// A new IActionRunner tag (+0x60) from the one global counter every action draws from, as text: the lock
    /// owner key <c>to_string(+0x60)</c> (LockTracks 0x00540584..0x0054058a via 0x004f0f4c). C1/U5: the counter
    /// is <c>IActionRunner::sTagCounter</c>, global, not per robot. An action that is not a head or lift move (an
    /// animation action, M8-007) takes its owner key here so no two actions share one.
    /// </summary>
    internal string NextActionTag() => ActionRunnerTagCounter.Global.NextIdTag().ToString();

    // fidelity: M4-005
    /// <summary>
    /// MA8: the u8 counter at MC+8 is pre-incremented (MoveHeadToAngle 0x006407D8..0x006407E6, MoveLiftToHeight
    /// 0x0064070C..0x0064071A, GetNextMotorActionID 0x006406EC..0x006406F4), so ids run 1..255, 0, 1, ... and head,
    /// lift and body share it.
    /// </summary>
    internal byte NextActionId()
    {
        lock (_gate) return unchecked(++_actionIdCounter);
    }

    // fidelity: M4-001
    /// <summary>
    /// The engine's head limits, written as the engine's float bits. MoveHeadToAngleAction clips a rescaled commanded
    /// angle against them with warnings (MA9, 0x00547F44 min / 0x00547FC2 max; the 2026-09-29 audit corrected the
    /// decimal-rounded 0xBEDF66E8/0x3F46D3FA to 0xBEDF66F3/0x3F46D3F2), and RS6 clamps a reported one (M2 App. B)
    /// against the OOB thresholds.
    /// </summary>
    public static readonly float MinHeadAngleRad = BitConverter.Int32BitsToSingle(unchecked((int)0xBEDF66F3));   // -25 degrees
    public static readonly float MaxHeadAngleRad = BitConverter.Int32BitsToSingle(unchecked((int)0x3F46D3F2));   // +44.5 degrees
    /// <summary>RS6: a reported head angle below this (−28°, 0xBEFA35DD) is stored as −25°.</summary>
    internal static readonly float ReportedHeadLowOob = BitConverter.Int32BitsToSingle(unchecked((int)0xBEFA35DD));
    /// <summary>RS6: a reported head angle above this (47.5°, 0x3F543B67) is stored as 44.5°.</summary>
    internal static readonly float ReportedHeadHighOob = BitConverter.Int32BitsToSingle(unchecked((int)0x3F543B67));

    // fidelity: M4-002
    /// <summary>
    /// The lift preset table at 0x00C54684 (MA14): key 0 LowDock 32, key 1 HighDock 76, key 2 HeightCarry 92, key 3
    /// OutOfFOV −1. MoveLiftToHeightAction::Init clamps a height in [0, ∞) outside [32, 92] with a warning, and sends
    /// a negative height to whichever of preset 0 and preset 2 is nearer the current height (MA13).
    /// </summary>
    public const float LowDockHeightMm = 32.0f, HighDockHeightMm = 76.0f, CarryHeightMm = 92.0f, OutOfFovHeightMm = -1.0f;
    public const float MinLiftHeightMm = LowDockHeightMm;
    public const float MaxLiftHeightMm = CarryHeightMm;
    /// <summary>
    /// Wheel speed the robot is documented to accept, from PyCozmo. Beyond this the robot clamps, it does
    /// not fault, so this is advisory. The engine's own ceiling was not located.
    /// </summary>
    public const float MaxWheelSpeedMmps = 200.0f;

    /// <summary>True while the robot says at least one wheel is turning.</summary>
    public bool WheelsMoving => _robot.State.Latest?.Has(RobotStatusFlag.AreWheelsMoving) ?? false;
    /// <summary>Left and right wheel speed in mm/s, as the robot reports them.</summary>
    public (float Left, float Right) WheelSpeeds =>
        _robot.State.Latest is { } s ? (s.LwheelSpeedMmps, s.RwheelSpeedMmps) : (0f, 0f);

    /// <summary>Robot+0x2FC as the engine keeps it: −25° until the head is calibrated, then the RS6-clamped report.</summary>
    public float HeadAngleRad { get { lock (_gate) return _headAngle; } }
    /// <summary>MC+0xA/+0xB/+0xC: whether the head, lift and body are moving, from the last handled state.</summary>
    public bool HeadMoving { get { lock (_gate) return _headMoving; } }
    public bool LiftMoving { get { lock (_gate) return _liftMoving; } }
    public bool BodyMoving { get { lock (_gate) return _bodyMoving; } }

    /// <summary>MC+0xB8 || +0xB9 || +0xBA: direct drive holds a track (M4 MA1; read by the M10 C7 unlock rule).</summary>
    internal bool DirectDriveHoldsAnyTrack { get { lock (_gate) return _ddBody || _ddHead || _ddLift; } }
    /// <summary>MC+0xD4, "direct drive is disabled" (M4 MA1; read by the M10 C7 unlock rule).</summary>
    internal bool DirectDriveDisabled { get { lock (_gate) return _directDriveDisabled; } }

    /// <summary>The tracks currently locked by anyone, as a mask (M4-014).</summary>
    public byte LockedTracks
    {
        get
        {
            lock (_gate)
            {
                byte m = 0;
                for (int b = 0; b < _trackLocks.Length; b++) if (_trackLocks[b].Count > 0) m |= (byte)(1 << b);
                return m;
            }
        }
    }

    // fidelity: M4-004
    /// <summary>
    /// The engine has no calibration gate on direct motion (MA21): the only readers of Robot+0x314/+0x315 are
    /// 0x00511E1C, 0x00512378, 0x0051335E and 0x005151A6, and none of them gates DriveWheels, MoveHead or MoveLift;
    /// the engine reacts to calibration instead (MA20). The stack's old readiness gate - a state must already exist and
    /// the head and lift must have finished calibrating - is removed. The <c>requireCalibration</c> parameter is kept
    /// only so existing callers compile; it has no effect.
    /// </summary>
    private void Log(string line) => _robot.Engine.Log(line);

    // ------------------------------------------------------------------- track locks

    // fidelity: M4-014
    /// <summary>
    /// MovementComponent::LockTracks (MA3, 0x006400D0..0x00640188): a lock entry per bit; the bits whose lock set
    /// reaches size 1 are collected, and a non-zero mask sends DisableAnimTracks 0x9D {mask}, reliable.
    /// </summary>
    private void LockTracksLocked(byte mask, string who, string debugName = "")
    {
        byte newly = 0;
        for (int b = 0; b < _trackLocks.Length; b++)
        {
            if ((mask & (1 << b)) == 0) continue;
            InsertLock(_trackLocks[b], new LockInfo(who, debugName));
            if (_trackLocks[b].Count == 1) newly |= (byte)(1 << b);
        }
        if (newly != 0) _robot.SendMessage(new DisableAnimTracks { Field0 = newly });
    }

    // fidelity: M4-014, M4-026
    /// <summary>
    /// MovementComponent::UnlockTracks (MA3, A10, 0x0063FE5C..0x0063FFDC). For each selected bit the lowest entry with the key
    /// is erased (__tree::find then erase); a missing key logs INFO "Tracks 0x%x are not currently locked by %s" (the whole
    /// u8 mask) and calls PrintLockState (0x0063FF76). The tree's count is then read again (0x0063FF7A), so a track that is
    /// empty adds its bit to the EnableAnimTracks mask whether the key was found or not (0x0063FF7E..0x0063FF88). The return
    /// value is OR'd only when the key was found and erased and the track still has entries (0x0063FF1A..0x0063FF22). The
    /// message goes only when the u8 mask is not zero (0x0063FF96), reliable and not hot, its result ignored.
    /// </summary>
    private bool UnlockTracksLocked(byte mask, string who)
    {
        byte enable = 0;
        bool stillLocked = false;
        for (int b = 0; b < _trackLocks.Length; b++)
        {
            if ((mask & (1 << b)) == 0) continue;
            var tree = _trackLocks[b];
            int at = FindLock(tree, who);
            if (at >= 0)
            {
                tree.RemoveAt(at);
                if (tree.Count != 0) stillLocked = true;
            }
            else
            {
                // sChanneledInfoF channel Unnamed (0x00BE3FEC via the literal at 0x0063FF44), row 2.
                Log($"info: [Unnamed] MovementComponent.UnlockTracks: Tracks 0x{mask:x} are not currently locked by {who}");
                PrintLockStateLocked();
            }
            if (tree.Count == 0) enable |= (byte)(1 << b);
        }
        if (enable != 0) SendOnAbortPath(new EnableAnimTracks { Field0 = enable });
        return stillLocked;
    }

    // fidelity: M4-031
    /// <summary>
    /// AnimTrackHelpers::AnimTrackFlagsToString 0x006305E8 (D2/D3): the names of the set bits, joined with '+', each from
    /// EnumToString(AnimTrackFlag) 0x007BC250. Bits 0..6 are HEAD_TRACK, LIFT_TRACK, BODY_TRACK, FACE_IMAGE_TRACK,
    /// EVENT_TRACK, BACKPACK_LIGHTS_TRACK and AUDIO_TRACK (strings 0x00C20053..0x00C200A7); 0 is NO_TRACKS and 0xFF ALL_TRACKS.
    /// </summary>
    internal static string AnimTrackFlagsToString(byte flags)
    {
        // row 7: 0xFF and 0 are looked up before the bit loop (0x006305F2..0x0063062C): ALL_TRACKS (0x00C200B3), NO_TRACKS (0x00C20049).
        if (flags == 0xFF) return "ALL_TRACKS";
        if (flags == 0) return "NO_TRACKS";
        var sb = new StringBuilder();
        for (int bit = 0; bit < 8; bit++)
        {
            if ((flags & (1 << bit)) == 0) continue;
            // EnumToString(0x80) is NULL (0x007BC2B0) and the next instruction is strlen(NULL) with no check (0x0063071C): the original
            // crashes. Kept as a visible throw, not a label.
            if (bit == 7)
                throw new NotSupportedException("AnimTrackFlagsToString with bit 0x80 set (mask != 0xFF): the original calls strlen(NULL) at 0x0063071C and crashes");
            if (sb.Length != 0) sb.Append('+');
            sb.Append(AnimTrackNames[bit]);
        }
        return sb.ToString();
    }

    private static readonly string[] AnimTrackNames =
    {
        "HEAD_TRACK", "LIFT_TRACK", "BODY_TRACK", "FACE_IMAGE_TRACK", "EVENT_TRACK", "BACKPACK_LIGHTS_TRACK", "AUDIO_TRACK",
    };

    // fidelity: M4-031
    /// <summary>
    /// MovementComponent::PrintLockState 0x006410D8..0x006413CA (D1): tracks 0..7 in ascending order; an empty tree is skipped;
    /// each other track appends its name, ':', the tree's count as an unsigned decimal and a space, then for each entry in tree
    /// order the entry's second string (node+0x1C), '[', its key (node+0x10) and "] ", then a newline. One DEBUG record follows
    /// even when the text is empty: channel Unnamed (0x00BE3FEC), event "MovementComponent.LockState" (0x006414C8), format
    /// "%s" (0x006414E4). It sends nothing and opens no file.
    /// </summary>
    private void PrintLockStateLocked()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < _trackLocks.Length; i++)
        {
            var tree = _trackLocks[i];
            if (tree.Count == 0) continue;
            sb.Append(AnimTrackFlagsToString((byte)(1 << i))).Append(':')
              .Append(((uint)tree.Count).ToString(CultureInfo.InvariantCulture)).Append(' ');
            foreach (var e in tree) sb.Append(e.Debug).Append('[').Append(e.Key).Append("] ");
            sb.Append('\n');
        }
        Log("debug: [Unnamed] MovementComponent.LockState: " + sb);
    }

    /// <summary>PrintLockState as a diagnostic call (0x0063FF76 is its only engine call site).</summary>
    internal void PrintLockState() { lock (_gate) PrintLockStateLocked(); }

    // fidelity: M1-025
    // E11–E14: the message fields do not gate this per-Robot subscriber.
    private readonly string _sdkChargerOwner = "OnChargerInSDK"; // ctor 0x0063DADC
    private readonly byte _sdkChargerMask = 0x07; // ctor 0x0063DB10
    internal void OnExitSdkMode()
    {
        if (!_robot.State.OnCharger) return;
        lock (_gate) UnlockTracksLocked(_sdkChargerMask, _sdkChargerOwner);
    }

    private bool AreAllTracksLockedLocked(byte mask)
    {
        for (int b = 0; b < _trackLocks.Length; b++)
            if ((mask & (1 << b)) != 0 && _trackLocks[b].Count == 0) return false;
        return true;
    }

    private bool IsTrackLockedLocked(byte mask)
    {
        for (int b = 0; b < _trackLocks.Length; b++)
            if ((mask & (1 << b)) != 0 && _trackLocks[b].Count > 0) return true;
        return false;
    }

    // fidelity: M8-007
    /// <summary>
    /// <c>MovementComponent::AreAnyTracksLocked(mask)</c> 0x00640098's reader at <c>0x00540440</c>: any bit
    /// of <paramref name="mask"/> whose lock set is non-empty. <c>IActionRunner::Update</c> 0x00540370
    /// fails an action whose required tracks are locked with 0x03000019 (0x00540572..0x0054057C); it does not
    /// retry (M8-007, M4-003).
    /// </summary>
    public bool AreAnyTracksLocked(byte mask) { lock (_gate) return IsTrackLockedLocked(mask); }

    // fidelity: M4-003
    /// <summary>
    /// <c>MovementComponent::AreAllTracksLockedBy(mask, owner)</c> (0x0064030C): every bit of
    /// <paramref name="mask"/> has <paramref name="owner"/> in its lock set. The ~IActionRunner stop gate
    /// (0x00541138/0x0054115E) only stops a track this action holds.
    /// </summary>
    public bool AreAllTracksLockedBy(byte mask, string owner)
    {
        lock (_gate)
        {
            for (int b = 0; b < _trackLocks.Length; b++)
                if ((mask & (1 << b)) != 0 && FindLock(_trackLocks[b], owner) < 0) return false;
            return true;
        }
    }

    /// <summary>
    /// <c>MovementComponent::LockTracks</c> 0x00640098: one owner entry per bit in the multiset. The engine's second string
    /// (the entry's node+0x1C, which PrintLockState prints) is <paramref name="debugName"/>; callers outside the movement
    /// component do not pass one yet (row 3: IActionRunner passes its name; callers outside this file that pass none get an empty string, reported MISSING).
    /// </summary>
    public void LockTracks(byte mask, string who, string? debugName = null, [System.Runtime.CompilerServices.CallerFilePath] string callerFile = "")
    {
        lock (_gate)
        {
            // fidelity: M4-031
            // The engine's LockTracks always takes a debug name (the entry's node+0x1C, printed by PrintLockState). The callers in the
            // higher layers (M5 animation, M7/M8 behaviours) do not supply one yet: say so, once per calling file, and keep an empty name.
            if (debugName is null)
            {
                if (_missingDebugNameFiles.Add(callerFile))
                    Log($"MISSING: MovementComponent.LockTracks debug name not supplied by {System.IO.Path.GetFileName(callerFile)} (owning layer M5/M7/M8): the engine passes the caller's own string, which the inventory does not give; using an empty name");
                debugName = "";
            }
            LockTracksLocked(mask, who, debugName);
        }
    }
    private readonly HashSet<string> _missingDebugNameFiles = new();

    /// <summary><c>MovementComponent::UnlockTracks</c> 0x0063fe5c: remove the owner's entry per bit; true when a found key left its track still locked (A10).</summary>
    public bool UnlockTracks(byte mask, string who) { lock (_gate) return UnlockTracksLocked(mask, who); }

    // fidelity: M10-004
    /// <summary>
    /// <c>MovementComponent::CompletelyUnlockAllTracks</c> (C1, 0x00640F84, export 0x640F85): walk the per-track lock
    /// sets in ascending index; a track whose set is empty is skipped entirely; every non-empty set is cleared,
    /// whoever holds it, with no filter by lock name and no test of the direct-drive flags. For each cleared track it
    /// logs "Unlocking track %s" with the mask <c>1&lt;&lt;k</c> and sends one <c>EnableAnimTracks</c> whose 1-byte
    /// payload is the track index <c>k</c>, not the mask, reliable and non-hot. This stack models only the three
    /// motion tracks (head, lift, body; M4-014), so indices 0..2 are the only non-empty ones here; the engine walks
    /// 0..7. What the firmware does with an index instead of a mask is HARDWARE_ONLY (M10-004 <c>unresolved</c>): the
    /// byte is kept exact. Unlike <see cref="UnlockTracks"/>, it neither erases one holder nor accumulates one mask.
    /// </summary>
    public void CompletelyUnlockAllTracks()
    {
        lock (_gate)
        {
            for (int k = 0; k < _trackLocks.Length; k++)
            {
                if (_trackLocks[k].Count == 0) continue;                    // empty set: no log, no clear, no message
                Log($"MovementComponent.UnlockAllTracks: Unlocking track {TrackName(k)}");
                _trackLocks[k].Clear();
                _robot.SendMessage(new EnableAnimTracks { Field0 = (byte)k });   // the index k, not 1<<k
            }
        }
    }

    /// <summary>The name <c>EnumToString(AnimTrackFlag)</c> gives the mask <c>1&lt;&lt;k</c> (C1).</summary>
    private static string TrackName(int k) => k switch
    {
        0 => "Head",
        1 => "Lift",
        2 => "Body",
        _ => k.ToString(),
    };

    /// <summary>
    /// The motion-track mask for an animation's tracks: the engine's <c>IActionRunner</c> +0x54 mask uses the
    /// same head/lift/body bits (M4-014 <see cref="HeadTrack"/>/<see cref="LiftTrack"/>/<see cref="BodyTrack"/>);
    /// face/audio/lights/events have no motion track.
    /// </summary>
    public static byte MaskFor(Animation.AnimationTrack tracks)
    {
        byte m = 0;
        if ((tracks & Animation.AnimationTrack.Head) != 0) m |= HeadTrack;
        if ((tracks & Animation.AnimationTrack.Lift) != 0) m |= LiftTrack;
        if ((tracks & Animation.AnimationTrack.Body) != 0) m |= BodyTrack;
        return m;
    }

    // fidelity: M4-014
    /// <summary>
    /// DirectDriveCheckSpeedAndLockTracks (MA2, 0x0063EFB0..0x0063F0B8): |speed| &lt; 1e-5 (0x0063F0F8) clears the flag
    /// and unlocks the mask if all its tracks are locked; otherwise it sets the flag and locks the mask unless all its
    /// tracks are already locked.
    /// </summary>
    private void DirectDriveCheckSpeedAndLockTracksLocked(float speed, ref bool flag, byte mask, string key, string debug)
    {
        // fidelity: M4-026
        // A9 / row 1b (0x0063EFB0..0x0063F0BC). The helper takes two strings: the lock key (stack argument 0) and a debug name (argument 1).
        if (Math.Abs(speed) < 1e-5f)
        {
            flag = false;
            // AreAllTracksLocked, then UnlockTracks; only a result of exactly 1 (a found key that left its track locked) logs.
            if (AreAllTracksLockedLocked(mask) && UnlockTracksLocked(mask, key))
            {
                // sErrorF event "MovementComponent.DirectDriveCheckSpeedAndLockTracks" (0x0063F036), format 0x0063F134: %s is the
                // mask's AnimTrackFlagsToString, %x the mask, then the debug name and the key. _errG is set (0x0063F08C), then the
                // debug-break gate (0x0063F090).
                Log($"error: MovementComponent.DirectDriveCheckSpeedAndLockTracks: Locks left on tracks {AnimTrackFlagsToString(mask)} [0x{mask:x}] after {debug}[{key}] unlocked");
                Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak();
            }
        }
        else
        {
            flag = true;
            if (!AreAllTracksLockedLocked(mask)) LockTracksLocked(mask, key, debug);
        }
    }

    // fidelity: M4-014
    /// <summary>
    /// The direct-drive gates of the game DriveWheels/MoveHead/MoveLift handlers (MA1, MA1-lift): ignored while direct
    /// drive is disabled (MC+0xD4); ignored when this direct drive does not hold the track and someone holds it.
    /// </summary>
    private MotionOutcome? DirectDriveRefused(bool holds, byte mask, string what, string lockedName)
    {
        if (_directDriveDisabled)
        {
            Log($"info: Ignoring {what} message while direct drive is disabled");
            return new MotionOutcome(MotionResult.Refused, $"{what} ignored: direct drive is disabled");
        }
        if (!holds && IsTrackLockedLocked(mask))
        {
            Log($"info: MovementComponent.{lockedName}");
            return new MotionOutcome(MotionResult.Refused, $"{what} ignored: {lockedName}");
        }
        return null;
    }

    // ------------------------------------------------------------------- wheels

    // fidelity: M4-014, M4-006
    /// <summary>
    /// The game DriveWheels (MA1, 0x0063ED82..0x0063EE9A): the direct-drive gates, the BODY track lock for |l|+|r|
    /// (0x0063EE32..0x0063EE56), then DriveWheels verbatim, reliable. Policy M4-006 then confirms it against the wheel
    /// speeds the robot reports (35 % / 5 mm/s); the engine has no counterpart.
    /// </summary>
    public async Task<MotionOutcome> DriveWheelsAsync(float leftMmps, float rightMmps,
                                                      float leftAccelMmps2 = 0f, float rightAccelMmps2 = 0f,
                                                      TimeSpan? confirmWithin = null,
bool requireCalibration = true)
    {
        lock (_gate)
        {
            if (DirectDriveRefused(_ddBody, BodyTrack, "DriveWheels", "WheelsLocked") is { } no) return no;
            DirectDriveCheckSpeedAndLockTracksLocked(Math.Abs(leftMmps) + Math.Abs(rightMmps), ref _ddBody, BodyTrack, DirectDriveWheels, DirectDriveWheels);   // HandleMessage<DriveWheels> 0x0063ED74: key +0xBC at 0x0063EDEC, name +0xBC at 0x0063EE14 (MoveHead +0xC0 at 0x0063F560/0x0063F584, MoveLift +0xC4 at 0x0063F7A4/0x0063F7C8)
            _robot.SendMessage(new DriveWheels(leftMmps, rightMmps, leftAccelMmps2, rightAccelMmps2));
        }

        var outcome = await AwaitState(
            s => WheelMatches(s.LwheelSpeedMmps, leftMmps) && WheelMatches(s.RwheelSpeedMmps, rightMmps),
            confirmWithin ?? TimeSpan.FromSeconds(2));

        var (l, r) = WheelSpeeds;
        return outcome
            ? new MotionOutcome(MotionResult.Acknowledged,
                $"robot reports wheels at {l:F0}/{r:F0} mm/s, asked for {leftMmps:F0}/{rightMmps:F0}")
            : new MotionOutcome(MotionResult.TimedOut,
                $"robot reports wheels at {l:F0}/{r:F0} mm/s, asked for {leftMmps:F0}/{rightMmps:F0}");
    }

    // fidelity: M4-006
    /// <summary>Policy M4-006: whether one wheel's reported speed matches what it was asked for.</summary>
    internal static bool WheelMatches(float reported, float requested)
    {
        const float stopped = 5f;
        if (Math.Abs(requested) <= 0.01f) return Math.Abs(reported) <= stopped;
        if (Math.Sign(reported) != Math.Sign(requested)) return false;
        float tolerance = Math.Max(stopped, Math.Abs(requested) * 0.35f);
        return Math.Abs(Math.Abs(reported) - Math.Abs(requested)) <= tolerance;
    }

    /// <summary>Stops the wheels with a game DriveWheels of zero (which unlocks the BODY track). Does not touch head or lift.</summary>
    public Task<MotionOutcome> StopWheelsAsync(TimeSpan? confirmWithin = null)
        => DriveWheelsAsync(0f, 0f, confirmWithin: confirmWithin, requireCalibration: false);

    // --------------------------------------------------------------- head and lift

    // fidelity: M4-003
    /// <summary>MD1/MA11: the head speed the original app passes (Unity Robot.cs:1443-1450).</summary>
    public const float DefaultHeadSpeedRadPerSec = 10f;
    /// <summary>MD1/MA11: the head acceleration the original app passes.</summary>
    public const float DefaultHeadAccelRadPerSec2 = 20f;
    /// <summary>MD1/MA11: the lift speed the original app passes (Robot.cs:1638-1646).</summary>
    public const float DefaultLiftSpeedRadPerSec = 10f;
    /// <summary>MD1/MA11: the lift acceleration the original app passes.</summary>
    public const float DefaultLiftAccelRadPerSec2 = 20f;
    /// <summary>MA9: MoveHeadToAngleAction's constructor defaults, which only engine-internal callers get: 15 rad/s.</summary>
    public const float ActionDefaultHeadSpeedRadPerSec = 15f;
    /// <summary>MA9: 20 rad/s².</summary>
    public const float ActionDefaultHeadAccelRadPerSec2 = 20f;

    /// <summary>MA10: the game SetHeadAngle builds its action with tolerance 0.0349066 rad, whose engine float is
    /// 0x3D0EFA35 (M4-003 unresolved; the old decimal rounded to 0x3D0EFA39).</summary>
    internal static readonly float GameHeadToleranceRad = BitConverter.Int32BitsToSingle(unchecked((int)0x3D0EFA35));
    /// <summary>MA9: the head tolerance minimum, 2° (0x0054803E..0x005480AC), the same engine float 0x3D0EFA35.</summary>
    internal static readonly float MinHeadToleranceRad = BitConverter.Int32BitsToSingle(unchecked((int)0x3D0EFA35));
    /// <summary>MA12: the game SetLiftHeight builds its action with tolerance 5.0 mm.</summary>
    internal const float GameLiftToleranceMm = 5.0f;

    // fidelity: M4-003
    /// <summary>
    /// MA12: the M12 CarryingComponent interface. True while an object is on the lift
    /// (<c>CarryingComponent(+0x284)+8 != −1</c>). Set by the manipulation layer when it is built.
    /// </summary>
    internal Func<bool>? IsCarryingObject { get; set; }
    /// <summary>
    /// MA12: the M12 interface that runs <c>PlaceObjectOnGroundAction</c>. Set by the manipulation layer.
    /// </summary>
    internal Func<Task<MotionOutcome>>? PlaceObjectOnGroundAsync { get; set; }
    /// <summary>MA15: IsHeadInPosition adds 1e-5 to the tolerance.</summary>
    internal const float HeadInPositionSlack = 1e-5f;
    /// <summary>MA17: StoppedMakingProgress.</summary>
    public const uint ResultStoppedMakingProgress = 0x04000004;
    /// <summary>MA17: the send failed.</summary>
    public const uint ResultSendFailed = 0x03000016;
    /// <summary>M4-003: IActionRunner::Update's required-tracks-locked failure (0x00540572..0x0054057C).</summary>
    public const uint ResultTracksLocked = 0x03000019;
    /// <summary>M4-016: IAction::UpdateInternal's timeout failure (0x00540E80).</summary>
    public const uint ResultTimedOut = 0x03000018;
    /// <summary>M4-016: the IAction timeout slot's default, 30.0 s (0x0052B0C2), not 5 s.</summary>
    internal static readonly TimeSpan DefaultActionTimeout = TimeSpan.FromSeconds(30);

    // fidelity: M4-001
    /// <summary>
    /// <c>Radians::rescale</c> (0x0084C87C), reached from the Radians ctor 0x0084C832: bring an angle into (−π, π].
    /// The engine uses the <c>ceil(v/2π − 0.5)</c> shortcut at |v| ≥ 10 (0x0084C8AE..0x0084C8BA) and a 2π loop
    /// below it (0x0084C8C4..0x0084C900, 0x0084C88A..0x0084C8A8). The ctor rescales every Radians the engine
    /// builds, so MoveHeadToAngleAction clips the rescaled angle, not the raw command.
    ///
    /// The shortcut keeps the engine's <c>vcvt.s32.f32</c> / <c>vcvt.f32.s32</c> round trip at
    /// 0x0084C91E..0x0084C922: the ceil result is converted to s32 and back to f32 before the multiply. ARM's
    /// conversion saturates out-of-range operands to INT_MIN/INT_MAX and maps NaN to 0, so at ±inf the turn count
    /// is a finite float and the result stays ±inf — which the clip then catches — instead of becoming
    /// inf − inf = NaN; huge finite values keep their magnitude instead of being reduced by the (unrepresentable)
    /// turn count. For the small turn counts the game path uses the round trip changes nothing.
    /// </summary>
    internal static float RescaleRadians(float value)
    {
        if (value <= -MathF.PI || value > MathF.PI)
        {
            if (MathF.Abs(value) >= 10f)
            {
                float turns = MathF.Ceiling(value / (2f * MathF.PI) - 0.5f);
                turns = ArmFloatToIntToFloat(turns);
                value -= turns * (2f * MathF.PI);
            }
            else
            {
                while (value <= -MathF.PI) value += 2f * MathF.PI;
                while (value > MathF.PI) value -= 2f * MathF.PI;
            }
        }
        return value;
    }

    // fidelity: M4-001
    /// <summary>
    /// The engine's <c>vcvt.s32.f32</c> then <c>vcvt.f32.s32</c> (0x0084C91E..0x0084C922): round toward zero,
    /// saturating out of the s32 range to INT_MIN/INT_MAX; NaN becomes 0. Used by <see cref="RescaleRadians"/>.
    /// </summary>
    internal static float ArmFloatToIntToFloat(float x)
    {
        int i;
        if (float.IsNaN(x)) i = 0;
        else if (x >= 2147483648f) i = int.MaxValue;      // 2^31: vcvt saturates
        else if (x < -2147483648f) i = int.MinValue;
        else i = (int)x;                                  // vcvt rounds toward zero
        return i;
    }

    // fidelity: M4-001
    /// <summary>The 1e-5 near tolerance, the engine float 0x3727C5AC (0x0084CC64/0x0084CC68).</summary>
    internal static readonly float NearTolerance = BitConverter.Int32BitsToSingle(unchecked((int)0x3727C5AC));

    // fidelity: M4-001
    /// <summary>
    /// <c>Radians::IsNear</c> (0x0084CC0A): <c>|rescale(rescale(a) − b)| &lt; |tolerance|</c>, a strict &lt;
    /// (0x0084CC3C..0x0084CC58).
    /// </summary>
    internal static bool IsNear(float a, float b, float tolerance)
    {
        float d = RescaleRadians(RescaleRadians(a) - b);
        return MathF.Abs(d) < MathF.Abs(tolerance);
    }

    // fidelity: M4-001
    /// <summary>
    /// <c>Anki::operator&gt;</c> (0x0084CC90..0x0084CCD0): <c>a − b &gt; 0 &amp;&amp; !IsNear(a, b, 1e-5)</c>. The clip
    /// at 0x00547F44/0x00547FC2 calls <c>operator&lt;</c> for the min and <c>operator&gt;</c> for the max.
    /// </summary>
    internal static bool RadiansGreaterThan(float a, float b) => a - b > 0f && !IsNear(a, b, NearTolerance);

    // fidelity: M4-001
    /// <summary><c>Anki::operator&lt;</c> (0x0084CD12): <c>operator&gt;(b, a)</c>.</summary>
    internal static bool RadiansLessThan(float a, float b) => RadiansGreaterThan(b, a);

    // fidelity: M4-001, M4-003, M4-016
    /// <summary>
    /// The game SetHeadAngle (MA10): MoveHeadToAngleAction(angle, tolerance 0.0349066, variability 0), whose speed,
    /// acceleration and duration are the caller's. The Radians ctor rescales the angle into (−π, π] first
    /// (0x0084C832 → 0x0084C87C); the ctor then clips the rescaled angle to the head limits with a warning (MA9);
    /// the tolerance is at least 2°; variability 0 leaves the angle as it is. Init sends nothing when the head is
    /// already within tolerance + 1e-5 of the target (MA15), otherwise MoveHeadToAngle with the next action id. The
    /// move completes on the matching ack followed by the head in position and stopped; it fails with 0x04000004 if
    /// the head stops out of position after having moved, and with 0x03000016 if the send fails (MA16, MA17). The
    /// IAction timeout defaults to the engine's 30.0 s slot (M4-016; 0x0052B0C2).
    /// </summary>
    public Task<MotionOutcome> SetHeadAngleAsync(float radians,
                                                 float maxSpeedRadPerSec = DefaultHeadSpeedRadPerSec,
                                                 float accelRadPerSec2 = DefaultHeadAccelRadPerSec2,
                                                 float durationSec = 0f,
                                                 TimeSpan? timeout = null, bool requireCalibration = true)
    {
        var a = BuildHeadAction(radians, maxSpeedRadPerSec, accelRadPerSec2, durationSec, timeout);
        Queue(a);
        return a.Done.Task;
    }

    /// <summary>
    /// Builds the MoveHeadToAngleAction runner without queueing it (M4-001/M4-003/M4-016). The game
    /// SetHeadAngle (MA10) uses this path; M10-008's compound reuses it as a child.
    /// </summary>
    private MoveAction BuildHeadAction(float radians, float maxSpeedRadPerSec, float accelRadPerSec2,
                                       float durationSec, TimeSpan? timeout)
    {
        // fidelity: M4-001
        // Radians ctor 0x0084C832 rescales first (0x0084C87C); so 99 rad → 99 − 16·2π = −1.5310 and clips to the
        // min with AngleTooLow, and −99 rad → −99 + 16·2π = +1.5310 and clips to the max with AngleTooHigh.
        // The clip is Anki::operator< (0x0084CD12) for the min and operator> (0x0084CC90..0x0084CCD0) for the max:
        // a − b > 0 and !IsNear(a, b, 1e-5), so a target within 1e-5 past a limit is sent unclipped.
        float target = RescaleRadians(radians);
        if (RadiansLessThan(target, MinHeadAngleRad))
        {
            // getDegrees 0x0084CD40: one f32 multiply, then promoted for %.1f.
            float degrees = target * BitConverter.Int32BitsToSingle(unchecked((int)0x42652EE1));
            Log(FormattableString.Invariant($"warning: MoveHeadToAngleAction.Constructor.AngleTooLow: Requested head angle ({degrees:F1}deg) less than min head angle (-25.0deg). Clipping."));
            target = MinHeadAngleRad;
        }
        else if (RadiansGreaterThan(target, MaxHeadAngleRad))
        {
            float degrees = target * BitConverter.Int32BitsToSingle(unchecked((int)0x42652EE1));
            Log(FormattableString.Invariant($"warning: MoveHeadToAngleAction.Constructor.AngleTooHigh: Requested head angle ({degrees:F1}deg) more than max head angle (44.5deg). Clipping."));
            target = MaxHeadAngleRad;
        }
        float tolerance = Math.Max(GameHeadToleranceRad, MinHeadToleranceRad);
        // fidelity: M4-003, M4-016
        // MA10: the game handler constructs the action and queues it at QueueActionPosition::NOW
        // (ActionList::QueueActionNow 0x0053DCA0). No synchronous Init/Update (Q4): the first ActionList tick
        // promotes it and IAction::UpdateInternal stamps the start, sends in Init and checks the engine clock.
        var a = new MoveAction(this, isHead: true, target, tolerance, $"head to {target:F3} rad",
                               id => new SetHeadAngle(target, maxSpeedRadPerSec, accelRadPerSec2, durationSec, id));
        a.SetName(MoveAction.HeadName(RescaleRadians(radians)));   // the ctor's name argument: the requested (rescaled, unclipped) angle
        a.TimeoutSeconds = (float)(timeout ?? DefaultActionTimeout).TotalSeconds;
        return a;
    }

    // fidelity: M4-002, M4-003, M4-016
    /// <summary>
    /// The game SetLiftHeight (MA12): MoveLiftToHeightAction(h, tolerance 5.0 mm, variability 0) with the caller's
    /// speed, acceleration and duration. A height of exactly 32 mm while carrying runs PlaceObjectOnGroundAction
    /// instead (MA12, through the M12 <see cref="IsCarryingObject"/>/<see cref="PlaceObjectOnGroundAsync"/> seam).
    /// Init: a height in [0, ∞) outside [32, 92] is clamped with a warning; a
    /// negative height goes to the nearer of 32 and 92 to the current height (MA13). Nothing is sent when
    /// |target − height| &lt; tolerance and the lift is not moving (MA15). Completion as for the head, on the lift's
    /// in-position test and MC+0xB (MA17).
    /// The angular-tolerance clip (≥ 1.5°, MA13; the f32 threshold 0x3CD67750 at 0x005493BC, the "TolTooSmall" warning 0x005492C8) cannot bind at 5 mm: the lift's steepest point, 66 mm/rad at 45 mm,
    /// makes 1.5° at most 1.73 mm. Its formula is not in the inventory and is not needed for this API.
    /// <paramref name="suppressTrackLocking"/> is the action's byte +0x56 (M13-028: FlipBlockAction::CheckIfDone sets it to 1 on its queued carry lift, 0x0055F152..0x0055F154):
    /// IActionRunner::Update 0x00540370 branches over both the AreAnyTracksLocked test and LockTracks when it is non-zero (0x00540428..0x00540434 -> 0x00540592), and the action's
    /// end skips the inline lock release in ~IActionRunner (0x0054120C..0x0054122A; 0x005408EC is IActionRunner::UnlockTracks, called only from the IAction constructor and IAction::Reset), so the move runs while another action holds the lift track and sends no Disable/EnableAnimTracks of its own.
    /// </summary>
    public Task<MotionOutcome> SetLiftHeightAsync(float heightMm,
                                                  float maxSpeedRadPerSec = DefaultLiftSpeedRadPerSec,
                                                  float accelRadPerSec2 = DefaultLiftAccelRadPerSec2,
                                                  float durationSec = 0f,
                                                  TimeSpan? timeout = null, bool requireCalibration = true,
                                                  bool suppressTrackLocking = false,
                                                  QueueActionPosition position = QueueActionPosition.Now)
    {
        // fidelity: M4-003
        // MA12: if the height is exactly 32.0 and something is carried, run PlaceObjectOnGroundAction instead.
        if (heightMm == LowDockHeightMm && IsCarryingObject?.Invoke() == true && PlaceObjectOnGroundAsync is not null)
            return PlaceObjectOnGroundAsync();
        var a = BuildLiftAction(heightMm, maxSpeedRadPerSec, accelRadPerSec2, durationSec, timeout, suppressTrackLocking);
        Queue(a, position);
        return a.Done.Task;
    }

    /// <summary>
    /// Builds the MoveLiftToHeightAction runner without queueing it (M4-002/M4-003/M4-016). M10-008's compound
    /// reuses this path as a child.
    /// </summary>
    private MoveAction BuildLiftAction(float heightMm, float maxSpeedRadPerSec, float accelRadPerSec2,
                                       float durationSec, TimeSpan? timeout, bool suppressTrackLocking)
    {
        // fidelity: M4-002, M4-003, M4-016
        // MA12: the game handler queues the action at NOW; the ActionList tick drives its timeout/Init/CheckIfDone. The clamp, the
        // InvalidHeight warning and the negative-height preset choice are MoveLiftToHeightAction::Init's (MoveAction.Init), not the builder's.
        MoveAction? a = null;
        a = new MoveAction(this, isHead: false, heightMm, GameLiftToleranceMm, $"lift to {heightMm:F1} mm",
                           id => new SetLiftHeight(a!.Target, maxSpeedRadPerSec, accelRadPerSec2, durationSec, id))
            { SuppressTrackLocking = suppressTrackLocking };
        a.SetName(MoveAction.LiftName(heightMm));                  // the ctor's name argument: the requested height
        a.TimeoutSeconds = (float)(timeout ?? DefaultActionTimeout).TotalSeconds;
        return a;
    }

    /// <summary>
    /// Queue a head/lift action on the engine's ActionList (M4-003). The list is the engine's robot+0x250
    /// (EngineRobot.ActionList); the tick at Robot::Update 0x005140BC runs it. The game handlers use NOW; M13-028's
    /// carry lift uses IN_PARALLEL (position 5, 0x0055F164).
    /// </summary>
    private void Queue(MoveAction a, QueueActionPosition position = QueueActionPosition.Now)
    {
        lock (_gate) _actions.Add(a);
        _robot.Engine.Robot!.ActionList.QueueAction(position, a, 0);
    }

    // fidelity: M10-008
    /// <summary>
    /// M10-008/C11 (0x5A2BB8..0x5A2C3A): the restore queues ONE <see cref="CompoundActionParallel"/> at position
    /// NOW whose child list is {MoveHeadToAngleAction, MoveLiftToHeightAction} in that order. The head is
    /// <c>Radians(head)</c> with the action's constructor defaults 15/20 (MA9); the lift is the height with the
    /// constructor constants 10/20. The children are queued only through the compound, so the ActionList ticks
    /// the pair in list order on the same tick.
    /// </summary>
    internal void QueueHeadAndLiftCompound(float headRad, float liftMm)
    {
        var head = BuildHeadAction(new Radians(headRad).Value, ActionDefaultHeadSpeedRadPerSec,
                                   ActionDefaultHeadAccelRadPerSec2, 0f, null);
        var lift = BuildLiftAction(liftMm, DefaultLiftSpeedRadPerSec, DefaultLiftAccelRadPerSec2, 0f, null,
                                   suppressTrackLocking: false);
        lock (_gate)
        {
            _actions.Add(head);
            _actions.Add(lift);
        }
        var compound = new CompoundActionParallel(() => _robot.Engine.Timer.SecondsF,
                                                  new ActionRunner?[] { head, lift });
        _robot.Engine.Robot!.ActionList.QueueAction(QueueActionPosition.Now, compound, 0);
    }

    /// <summary>L11: the lift action's destructor releases its ack subscription before the base tail.</summary>
    private void ReleaseAckSubscription(MoveAction a)
    {
        lock (_gate) _actions.Remove(a);
    }

    /// <summary>The action's destructor tail completed: drop it from the ack dispatch list and map its stored
    /// engine result to the public <see cref="MotionOutcome"/>.</summary>
    private void ActionEnded(MoveAction a)
    {
        lock (_gate) _actions.Remove(a);
        a.Done.TrySetResult(OutcomeFor(a));
    }

    /// <summary>Map the engine's stored +0x18 result to <see cref="MotionOutcome"/> (the public API is unchanged).</summary>
    private static MotionOutcome OutcomeFor(MoveAction a) => a.State switch
    {
        EngineActionResult.Success => new MotionOutcome(MotionResult.Acknowledged,
            $"{a.What}: robot acknowledged action {a.Id} and reports it in position"),
        EngineActionResult.Timeout => new MotionOutcome(MotionResult.Failed,
            a.Acked ? $"{a.What}: action {a.Id} acknowledged but not in position within {a.TimeoutSeconds:F1}s"
                    : $"{a.What}: no acknowledgement of action {a.Id} within {a.TimeoutSeconds:F1}s")
            { EngineResult = ResultTimedOut },
        ResultStoppedMakingProgress => new MotionOutcome(MotionResult.Failed,
            $"{a.What}: action {a.Id} stopped out of position (StoppedMakingProgress)") { EngineResult = ResultStoppedMakingProgress },
        _ => new MotionOutcome(MotionResult.Failed, $"{a.What}: engine result 0x{a.State:X8}") { EngineResult = a.State },
    };

    // fidelity: M4-002
    /// <summary>
    /// MA13 for a negative height: the nearer of preset 0 (32) and preset 2 (92) to the current height; C5
    /// (0x00549104..0x0054913C: vcmpe s0,s4; it mi; vmovmi): 32 only when strictly nearer, so a tie goes to 92.
    /// </summary>
    internal static float NegativeHeightTarget(float currentHeightMm) =>
        Math.Abs(currentHeightMm - LowDockHeightMm) < Math.Abs(currentHeightMm - CarryHeightMm) ? LowDockHeightMm : CarryHeightMm;

    /// <summary>GetLiftHeight: 66·sin(Robot+0x300) + 45 (RS7); 45 mm (angle 0) before any state.</summary>
    private float CurrentLiftHeightMm()
    {
        lock (_gate) return RobotState.LiftHeightMmFromAngle(_liftAngle ?? 0f);
    }

    // fidelity: M4-014
    /// <summary>
    /// The game MoveHead (MA1, 0x0063F4F6..0x0063F5D2): the direct-drive gates ("HeadLocked"), the HEAD track lock
    /// for the speed, then MoveHead verbatim, reliable. No acknowledgement is defined.
    /// </summary>
    public MotionOutcome MoveHead(float radPerSec, bool requireCalibration = true)
    {
        lock (_gate)
        {
            if (DirectDriveRefused(_ddHead, HeadTrack, "MoveHead", "HeadLocked") is { } no) return no;
            DirectDriveCheckSpeedAndLockTracksLocked(radPerSec, ref _ddHead, HeadTrack, DirectDriveHead, DirectDriveHead);
            _robot.SendMessage(new MoveHead { SpeedRadPerSec = radPerSec });
        }
        return new MotionOutcome(MotionResult.Acknowledged, $"head moving at {radPerSec:F2} rad/s (no ack is defined)");
    }

    // fidelity: M4-014
    /// <summary>
    /// The game MoveLift (MA1-lift, 0x0063F73A..0x0063F92A): the direct-drive gates ("LiftLocked"), the LIFT track
    /// lock (mask 2) for the speed, then MoveLift verbatim, reliable.
    /// </summary>
    public MotionOutcome MoveLift(float radPerSec, bool requireCalibration = true)
    {
        lock (_gate)
        {
            if (DirectDriveRefused(_ddLift, LiftTrack, "MoveLift", "LiftLocked") is { } no) return no;
            DirectDriveCheckSpeedAndLockTracksLocked(radPerSec, ref _ddLift, LiftTrack, DirectDriveLift, DirectDriveLift);
            _robot.SendMessage(new MoveLift { SpeedRadPerSec = radPerSec });
        }
        return new MotionOutcome(MotionResult.Acknowledged, $"lift moving at {radPerSec:F2} rad/s (no ack is defined)");
    }

    // -------------------------------------------------------------- calibration

    // fidelity: M4-012
    /// <summary>
    /// CalibrateMotorAction's CalibrateMotors (MA18): StartMotorCalibration 0x58 {byte0 head, byte1 lift} (MA19). The
    /// engine never sends it automatically; this stack sends it only when a caller asks.
    /// </summary>
    public void RequestMotorCalibration(bool head, bool lift) =>
        _robot.SendMessage(new StartMotorCalibration { Head = head, Lift = lift });

    // ------------------------------------------------------------------ stopping

    /// <summary>The outer gate of every Stop function (row 8, 0x00640A10..0x00640A26, 0x0063FBE2..0x0063FBFE): any of +0xB8/+0xB9/+0xBA set and +0xD4 zero.</summary>
    private bool StopGateLocked() => (_ddBody || _ddHead || _ddLift) && !_directDriveDisabled;

    // fidelity: M4-015
    /// <summary>
    /// MovementComponent::StopHead (MA4, row 8, 0x00640A08..0x00640AF6): behind the outer gate ONE helper call (flag +0xB9, mask 1, key and
    /// name +0xC0 DirectDriveHead, 0x00640A72), then always MoveHead{0}, reliable and not hot.
    /// </summary>
    public void StopHead()
    {
        lock (_gate)
        {
            if (StopGateLocked()) DirectDriveCheckSpeedAndLockTracksLocked(0f, ref _ddHead, HeadTrack, DirectDriveHead, DirectDriveHead);
            _robot.SendMessage(new MoveHead { SpeedRadPerSec = 0f });
        }
    }

    // fidelity: M4-015, M4-028
    /// <summary>
    /// MovementComponent::StopLift (MA4, L14, 0x00640B3C..0x00640C2A): when any of the direct-drive flags +0xB8/+0xB9/+0xBA is
    /// set and +0xD4 is zero, ONE zero-speed helper call for the lift (flag +0xBA, mask 2); then, always, MoveLift{0}, reliable
    /// and not hot (0x00640C00..0x00640C2A). It is not StopAllMotors.
    /// </summary>
    public void StopLift()
    {
        lock (_gate)
        {
            if (StopGateLocked()) DirectDriveCheckSpeedAndLockTracksLocked(0f, ref _ddLift, LiftTrack, DirectDriveLift, DirectDriveLift);
            _robot.SendMessage(new MoveLift { SpeedRadPerSec = 0f });
        }
    }

    // fidelity: M4-015
    /// <summary>
    /// MovementComponent::StopBody (MA4, row 8, 0x00640C70..0x00640E4E): behind the outer gate THREE helper calls (flag +0xB8, mask 4,
    /// key +0xBC, names +0xBC, +0xC8, +0xCC: 0x00640CE2, 0x00640D4A, 0x00640DB2), then always DriveWheels{0,0,0,0}, reliable and not hot.
    /// </summary>
    public void StopBody()
    {
        lock (_gate)
        {
            if (StopGateLocked())
            {
                DirectDriveCheckSpeedAndLockTracksLocked(0f, ref _ddBody, BodyTrack, DirectDriveWheels, DirectDriveWheels);
                DirectDriveCheckSpeedAndLockTracksLocked(0f, ref _ddBody, BodyTrack, DirectDriveWheels, DirectDriveArc);
                DirectDriveCheckSpeedAndLockTracksLocked(0f, ref _ddBody, BodyTrack, DirectDriveWheels, DirectDriveTurnInPlace);
            }
            _robot.SendMessage(new DriveWheels(0f, 0f, 0f, 0f));
        }
    }

    // fidelity: M4-007, M4-026
    /// <summary>
    /// MovementComponent::StopAllMotors (MA5, A7, A8, 0x0063FBD8..0x0063FE10 → 0x0064099C..0x006409C2). When any of the
    /// direct-drive flags +0xB8/+0xB9/+0xBA is set and +0xD4 is zero it makes FIVE zero-speed helper calls, all of them once the
    /// outer gate has passed: HEAD (flag +0xB9, mask 1), LIFT (+0xBA, mask 2), then BODY (+0xB8, mask 4) three times
    /// (0x0063FC4C, 0x0063FCB6, 0x0063FD1E, 0x0063FD86, 0x0063FDEE). Then, always, the empty StopAllMotors 0x3B, reliable and
    /// not hot, its result ignored. The engine sends no DriveWheels here.
    /// </summary>
    public void StopAllMotors()
    {
        lock (_gate)
        {
            if (StopGateLocked())
            {
                // key / debug name per call (row 1): head Head/Head, lift Lift/Lift, body Wheels with Wheels, Arc, TurnInPlace
                DirectDriveCheckSpeedAndLockTracksLocked(0f, ref _ddHead, HeadTrack, DirectDriveHead, DirectDriveHead);
                DirectDriveCheckSpeedAndLockTracksLocked(0f, ref _ddLift, LiftTrack, DirectDriveLift, DirectDriveLift);
                DirectDriveCheckSpeedAndLockTracksLocked(0f, ref _ddBody, BodyTrack, DirectDriveWheels, DirectDriveWheels);
                DirectDriveCheckSpeedAndLockTracksLocked(0f, ref _ddBody, BodyTrack, DirectDriveWheels, DirectDriveArc);
                DirectDriveCheckSpeedAndLockTracksLocked(0f, ref _ddBody, BodyTrack, DirectDriveWheels, DirectDriveTurnInPlace);
            }
            SendOnAbortPath(new StopAllMotors());
        }
    }

    // fidelity: M4-007
    /// <summary>
    /// <see cref="StopAllMotors"/>, then this stack's wait for the robot to report the wheels stopped. Never gated on
    /// calibration.
    /// </summary>
    public async Task<MotionOutcome> StopAllAsync(TimeSpan? confirmWithin = null)
    {
        StopAllMotors();

        bool stopped = await AwaitState(
            s => !s.Has(RobotStatusFlag.AreWheelsMoving) && Math.Abs(s.LwheelSpeedMmps) < 1f && Math.Abs(s.RwheelSpeedMmps) < 1f,
            confirmWithin ?? TimeSpan.FromSeconds(2));

        var (l, r) = WheelSpeeds;
        return stopped
            ? new MotionOutcome(MotionResult.Acknowledged, "robot reports all wheels stopped")
            : new MotionOutcome(MotionResult.TimedOut, $"robot still reports wheels at {l:F0}/{r:F0} mm/s");
    }

    // ---------------------------------------------------------------------- AbortAll, teardown, sleep

    /// <summary>The engine's PathComponent (Robot+0x5C), as far as Robot::AbortAll reaches it (A3, A4).</summary>
    internal PathComponent Path { get; }

    // fidelity: M4-026
    /// <summary>
    /// While AbortAll runs, the sends of its path (StopAllMotors, EnableAnimTracks) go through Robot::SendMessage's failure warning
    /// (0x00513558/0x0051356C). Other sends of this class are older and unchanged (queued separately).
    /// </summary>
    private Func<RobotMessage, bool>? _abortSend;
    private bool SendOnAbortPath(RobotMessage m) => _abortSend is { } s ? s(m) : _robot.SendMessage(m);

    // fidelity: M4-026
    /// <summary>
    /// Robot::AbortAll 0x0051194C..0x0051198E (A1). In this order and with no short circuit: ActionList::Cancel(-1) (result
    /// ignored, the queue's cancellation is M8's), PathComponent::Abort, DockingComponent::AbortDocking (A5),
    /// Robot::SendAbortAnimation (A6), MovementComponent::StopAllMotors (A7, A8; no result). The result is
    /// <c>(path | docking | animation) != 0</c> of the three send results, not "an action was cancelled".
    /// The live entry is the Robot's destructor, which calls it before anything else is torn down (0x00511120): the
    /// Lifetime slot -3 bound by <see cref="BindRobotLifetime"/>.
    /// </summary>
    internal int AbortAll(EngineRobot robot)
    {
        // The result is the engine's: Robot::SendMessage returns a Result, 0 success and 1 failure (not connected, 0x0051349C..0x005134BC),
        // and AbortAll returns 1 when ANY of the three failed (orr, orrs, movne r0,#1, 0x00511980..0x00511988). Every send of the path
        // goes through Robot::SendMessage's failure warning.
        _abortSend = robot.SendChecked;
        try
        {
            robot.ActionList.Cancel(-1);
            int path = Path.Abort(robot.SendChecked);
            int docking = robot.SendChecked(new AbortDocking()) ? 0 : 1;          // A5 0x0063BE10: empty AbortDocking, reliable, not hot
            int animation = robot.SendChecked(new AbortAnimation()) ? 0 : 1;      // A6 0x00517DE4: empty AbortAnimation, reliable, not hot
            StopAllMotors();
            return (path | docking | animation) != 0 ? 1 : 0;
        }
        finally { _abortSend = null; }
    }

    // fidelity: M4-027, M4-026
    /// <summary>
    /// The Lifetime slots (<see cref="RobotLifetime"/>) this stack's components are bound to, for one Robot. Called when the
    /// Robot is built. Slot -3 is AbortAll (0x00511120); 0x254 the MovementComponent, 0x278 CubeAccel, 0x274 the backpack
    /// lights, 0x270 the cube lights and 0x450 the tap filter are the deletions at 0x00511500, 0x00511428, 0x0051143E,
    /// 0x0051146A and 0x005111EA. Each owner pointer is cleared before its destructor runs (the Lifetime does that), except slot 0x450 (the tap filter), which is released with clearBefore:false and cleared afterwards (0x00511220),
    /// and none of the destructors sends a message: nothing is unlocked, stopped or switched off by them.
    /// NOT BOUND (reported MISSING): Touch +0x28C and Cliff +0x288 (their RollingFileLogger close; this stack has no Touch
    /// component and the Cliff state lives in <see cref="CozmoSensors"/>).
    /// </summary>
    internal void BindRobotLifetime(EngineRobot robot)
    {
        var lifetime = robot.Lifetime;
        lifetime.Bind(-3, _ => AbortAll(robot));
        lifetime.Bind(0x450, _ => _robot.Cubes.DestroyTapFilter());
        lifetime.Bind(0x278, _ => _robot.CubeAccel.ResetToConstructed());
        lifetime.Bind(0x274, _ => _robot.Lights.Body.ResetToConstructed());
        lifetime.Bind(0x270, _ => _robot.Lights.Cubes.ResetToConstructed());
        lifetime.Bind(0x254, _ => DestroyMovementComponent());
    }

    // fidelity: M4-027
    /// <summary>
    /// MovementComponent::~MovementComponent 0x00641CFC..0x00641D38 (T4/T8/T18): the FaceLayerToRemove tree (+0x88, not modelled
    /// here), then the eight lock trees from +0x7C down to +0x28, then the subscription list (+0x10). It calls no
    /// StopAllMotors, UnlockTracks or EnableAnimTracks: it sends nothing and logs nothing. Those belong to the AbortAll before it.
    /// </summary>
    private void DestroyMovementComponent()
    {
        lock (_gate)
            for (int track = _trackLocks.Length - 1; track >= 0; track--) _trackLocks[track].Clear();
    }

    // fidelity: M4-028
    /// <summary>
    /// The go-to-sleep sequence's lift child (0x0052CF8A..0x0052CFC2, L1..L8): MoveLiftToHeightAction(preset 0, tolerance
    /// 5.0f = 0x40A00000, variability 0). Preset 0 is LowDock (the table at 0x00C54684: key 0 → 0x42000000, 32 mm), the action's
    /// name "MoveLiftToLowDock", track mask 2, speed 10, acceleration 20, duration 0 (L5). Its Init, CheckIfDone, counters and
    /// results are those of every MoveLiftToHeightAction (<see cref="MoveAction"/>). It is not queued here: its parent is the
    /// sequence's parallel compound, which belongs to M5/M8 (<see cref="CozmoEngine.CreateGoToSleepSequence"/> is that seam).
    /// </summary>
    internal ActionRunner CreateGoToSleepLiftChild()
    {
        float height = BitConverter.Int32BitsToSingle(0x42000000);
        float tolerance = BitConverter.Int32BitsToSingle(0x40A00000);
        var child = new MoveAction(this, isHead: false, height, tolerance, "lift to LowDock",
                                   id => new SetLiftHeight(height, DefaultLiftSpeedRadPerSec, DefaultLiftAccelRadPerSec2, 0f, id));
        child.SetName("MoveLiftToLowDock");
        lock (_gate) _actions.Add(child);                     // the ack subscription (+0x9C/+0xA0), installed by the ctor
        return child;
    }

    // fidelity: M4-028
    /// <summary>
    /// L1: the child is added to the sleep sequence's parallel parent with both flags zero (AddAction 0x0052CFB0; the returned
    /// weak handle is released).
    /// </summary>
    internal void AddGoToSleepLiftChild(CompoundActionParallel parent) => parent.AddAction(CreateGoToSleepLiftChild(), false, false);

    // ------------------------------------------------------------------ the actions

    /// <summary>
    /// A MoveHeadToAngleAction or MoveLiftToHeightAction in flight, as the engine's IActionRunner subclass
    /// (L1..L18). The shared lifecycle is in <see cref="ActionRunner"/>; this class supplies the concrete
    /// Init/CheckIfDone and the MovementComponent track primitives.
    /// </summary>
    private sealed class MoveAction : ActionRunner
    {
        // fidelity: M4-016 — process-lifetime u16 debug counters, head 01051028/2A, lift 0105102C/2E.
        private static readonly object DebugCounterGate = new();
        private static ushort _headWaitingAck, _headNotInPosition, _liftWaitingAck, _liftNotInPosition;
        private static bool Eleventh(ref ushort counter)
        {
            counter = unchecked((ushort)(counter + 1));
            return counter >= 11;
        }
        public readonly CozmoMotion Owner;
        public readonly bool IsHead;
        /// <summary>The motor target. For a lift action it is +0x84, which Init derives from <see cref="RequestedHeight"/> (+0x78).</summary>
        public float Target;
        public readonly float Tolerance;
        /// <summary>Lift only, +0x78: the constructor's height argument; Init clamps it in place (0x00549058..0x005490F6).</summary>
        public float RequestedHeight;
        public readonly string What;
        /// <summary>The message builder that carries the action id (MA8): SetHeadAngle / SetLiftHeight.</summary>
        public readonly Func<byte, RobotMessage> Build;
        /// <summary>MA8: the motor action id on the wire (MC+8), taken in MoveHeadToAngle / MoveLiftToHeight.</summary>
        public byte Id;
        /// <summary>+0xAA / +0x95: the command was sent.</summary>
        public bool Sent;
        /// <summary>+0xAB / +0x96: the matching ack arrived.</summary>
        public bool Acked;
        /// <summary>+0xAD / +0x98: the motor was seen moving after the ack.</summary>
        public bool HasMoved;
        /// <summary>Head +0xAC / lift +0x97: in position, latched (C1, C6).</summary>
        public bool InPositionLatched;
        public readonly TaskCompletionSource<MotionOutcome> Done = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public MoveAction(CozmoMotion owner, bool isHead, float target, float tolerance, string what,
                          Func<byte, RobotMessage> build)
            : base(isHead ? 0x12 : 0x13, isHead ? HeadTrack : LiftTrack)   // M13-022: head type 0x12; lift 0x13 (R-ANIM pre-extraction)
        {
            Owner = owner; IsHead = isHead; Target = target; RequestedHeight = target; Tolerance = tolerance; What = what; Build = build;
            // fidelity: M4-016
            // The base IActionRunner warnings/infos (TimedOut, TracksLocked, Cancel, ...) reach the engine log.
            Log = line => Owner.Log(line);
        }

        // fidelity: M4-016
        // The runner's name string (+0x48), from the ctor's name argument: head "MoveHeadTo" + to_string((float)degrees) + "Deg",
        // lift "MoveLiftTo" + to_string((float)height) + "mm"; to_string of a float is printf "%f" (6 decimals).
        private string _name = "";
        internal void SetName(string name) => _name = name;
        protected override string ActionName => _name;
        internal static string HeadName(float requestedRadians) =>
            "MoveHeadTo" + ((double)(requestedRadians * BitConverter.Int32BitsToSingle(unchecked((int)0x42652EE1)))).ToString("F6", System.Globalization.CultureInfo.InvariantCulture) + "Deg";
        internal static string LiftName(float requestedHeightMm) =>
            "MoveLiftTo" + ((double)requestedHeightMm).ToString("F6", System.Globalization.CultureInfo.InvariantCulture) + "mm";

        // L7: the engine clock (BaseStationTimer::GetCurrentTimeInSeconds).
        protected override float EngineClockSeconds => Owner._robot.Engine.Timer.SecondsF;

        protected override bool AreAnyTracksLocked(uint mask) => Owner.AreAnyTracksLocked((byte)mask);
        // fidelity: M4-014
        // Row 3: IActionRunner::Update's LockTracks (0x00540584) passes the tag's decimal string as the key and the action's name (+0x48) as the debug name.
        protected override void LockTracks(uint mask, string owner) => Owner.LockTracks((byte)mask, owner, ActionName);
        protected override void UnlockTracksInternal(uint mask, string owner) => Owner.UnlockTracks((byte)mask, owner);
        protected override bool AreAllTracksLockedBy(uint mask, string owner) => Owner.AreAllTracksLockedBy((byte)mask, owner);
        protected override void StopHeadTrack() => Owner.StopHead();
        protected override void StopLiftTrack() => Owner.StopLift();
        protected override void StopBodyTrack() => Owner.StopBody();
        protected override bool HeadTrackMoving => Owner._headMoving;
        protected override bool LiftTrackMoving => Owner._liftMoving;
        protected override bool BodyTrackMoving => Owner._bodyMoving;

        // IActionRunner::Interrupt 0x00540250 calls virtual +0x14 and acts only on a 1. MoveHeadToAngleAction's
        // vtable `_ZTVN4Anki5Cozmo21MoveHeadToAngleActionE` = 0x102197C (object vptr 0x1021984, +0x14 = 0x1021998)
        // and MoveLiftToHeightAction's `_ZTVN4Anki5Cozmo22MoveLiftToHeightActionE` = 0x10219B4 (object vptr
        // 0x10219BC, +0x14 = 0x10219D0) both resolve to the same target 0x0052B0B2 (Thumb 0x0052B0B3):
        // `movs r0,#0; bx lr`, which returns 0. Q14 NOW_AND_RESUME therefore refuses to interrupt a head or lift
        // move and falls back to QueueNow (Q15).
        public override bool CanInterrupt() => false;

        // fidelity: M4-002, M4-016
        /// <summary>
        /// MoveLiftToHeightAction::Init's height handling (0x0054903C..0x005491D2), run when the action starts, with the lift
        /// height read at that moment. A height that is not negative (and not NaN) and lies below 32 or above 92 logs the
        /// "MoveLiftToHeightAction.Init.InvalidHeight" warning, format "%f mm. Clipping to be in range." (0x0054936C, 0x00549398)
        /// with the double-widened height, then is stored back to +0x78 as 32 (below) or 92 (above). A negative height (after
        /// that) takes the nearer of preset 0 (32) and preset 2 (92) to GetLiftHeight (0x00549104..0x0054913C), strictly
        /// nearer for 32, and skips the second clamp (0x00549140 jumps to 0x005491CE). Otherwise +0x84 = the height and the
        /// second clamp into [32, 92] (0x0054918E..0x005491D2) stores a NaN as 32. A NaN height skips the first clamp (blt at
        /// 0x0054905C is true unordered), is not negative (bpl at 0x00549102 is true unordered) and so reaches the second clamp.
        /// The height-variability step between them (+0x80, 0x00549142..0x0054918A) is not built: see the MISSING note in the report.
        /// </summary>
        private void LiftInitHeightLocked()
        {
            float h = RequestedHeight;
            if (h >= 0f && (h < LowDockHeightMm || h > CarryHeightMm))
            {
                string shown = double.IsPositiveInfinity(h) ? "inf" : ((double)h).ToString("F6", System.Globalization.CultureInfo.InvariantCulture);
                // fidelity: M4-032 (%f rendered by .NET fixed-point, invariant culture: the phone printf boundary)
                Owner.Log($"warning: MoveLiftToHeightAction.Init.InvalidHeight: {shown} mm. Clipping to be in range.");
                h = h < LowDockHeightMm ? LowDockHeightMm : CarryHeightMm;
                RequestedHeight = h;
            }
            if (h < 0f)
            {
                Target = NegativeHeightTarget(Owner.CurrentLiftHeightMm());   // no second clamp
                return;
            }
            Target = float.IsNaN(h) ? LowDockHeightMm : h < LowDockHeightMm ? LowDockHeightMm : h >= CarryHeightMm ? CarryHeightMm : h;
        }

        // fidelity: M4-016
        /// <summary>
        /// The concrete Init (MA15, C6 L1): in position sends nothing and latches; otherwise the next motor
        /// action id goes on the wire. A send failure is 0x03000016 (MA17). Init runs on the first gates-passing
        /// UpdateInternal tick, after the start time is stamped.
        /// </summary>
        public override int Init()
        {
            lock (Owner._gate)
            {
                if (!IsHead) LiftInitHeightLocked();
                if (Owner.InPositionLocked(this)) { InPositionLatched = true; return 0; }
                Id = Owner.NextActionId();                       // MA8: taken in MoveHeadToAngle / MoveLiftToHeight
                if (!Owner._robot.SendMessage(Build(Id))) return (int)ResultSendFailed;
                Sent = true;
                return 0;
            }
        }

        // fidelity: M4-016
        /// <summary>
        /// The concrete CheckIfDone, the same for the head (C1: MoveHeadToAngleAction 0x005485D8..0x005488B6) and
        /// the lift (C6 L2..L6: MoveLiftToHeightAction 0x005493F6..0x00549508):
        /// 1. sent and not acked: Running;
        /// 2. the in-position latch, which stays set once true;
        /// 3. has moved while the motor is moving;
        /// 4. in position: Success if not moving else Running; not in position: moving Running, stopped and has
        ///    moved 0x04000004 StoppedMakingProgress, otherwise Running.
        /// The head's eye-shift block (0x005485F8..0x00548728) never runs in this build (+0xA8 is never set), and
        /// the lift has none (C6), so neither changes the result.
        /// </summary>
        public override uint CheckIfDone()
        {
            lock (Owner._gate)
            {
                string name = IsHead ? "MoveHeadToAngleAction" : "MoveLiftToHeightAction";
                int tag = unchecked((int)Tag); // native %d, not unsigned formatting.
                if (Sent && !Acked)
                {
                    lock (DebugCounterGate)
                    {
                        ref ushort count = ref (IsHead ? ref _headWaitingAck : ref _liftWaitingAck);
                        if (Eleventh(ref count))
                        {
                            Owner.Log(FormattableString.Invariant($"debug: {name}.CheckIfDone.WaitingForAck: [{tag}] ActionID: {Id}"));
                            count = 0; // native reset follows the log.
                        }
                    }
                    return EngineActionResult.Running;
                }
                if (Owner.InPositionLocked(this)) InPositionLatched = true;
                bool moving = Owner.MovingLocked(this);
                if (moving) HasMoved = true;
                if (InPositionLatched)
                {
                    if (IsHead && moving)
                    {
                        float degrees = BitConverter.Int32BitsToSingle(unchecked((int)0x42652EE1));
                        Owner.Log(FormattableString.Invariant($"info: MoveHeadToAngleAction.CheckIfDone.HeadMovingInPosition: [{tag}] Head considered in position at {Target * degrees:F1}deg but still moving at {Owner._headAngle * degrees:F1}deg"));
                    }
                    return moving ? EngineActionResult.Running : EngineActionResult.Success;
                }
                lock (DebugCounterGate)
                {
                    ref ushort count = ref (IsHead ? ref _headNotInPosition : ref _liftNotInPosition);
                    if (Eleventh(ref count))
                    {
                        if (IsHead)
                        {
                            float degrees = BitConverter.Int32BitsToSingle(unchecked((int)0x42652EE1));
                            // Game/compound construction uses variability 0 (MA10), tolerance at +80, variability +88.
                            Owner.Log(FormattableString.Invariant($"debug: MoveHeadToAngleAction.CheckIfDone.NotInPosition: [{tag}] Waiting for head to get in position: {Owner._headAngle * degrees:F1}deg vs. {Target * degrees:F1}deg(+/-{0f:F1}) tol:{Tolerance * degrees:F1}deg"));
                        }
                        else Owner.Log(FormattableString.Invariant($"debug: MoveLiftToHeightAction.CheckIfDone.NotInPosition: [{tag}] Waiting for lift to get in position: {Owner.CurrentLiftHeightMm():F1}mm vs. {Target:F1}mm (tol: {Tolerance:F6})"));
                        count = 0;
                    }
                }
                if (!moving && HasMoved)
                {
                    Owner.Log(FormattableString.Invariant($"warning: {name}.CheckIfDone.StoppedMakingProgress: [{tag}] giving up since we stopped moving"));
                    return ResultStoppedMakingProgress;
                }
                return EngineActionResult.Running;
            }
        }

        // L16/D5: the base runs the stop-before-unlock tail; then the Done task maps the stored result.
        public override void WatcherEnding()
        {
            // fidelity: M4-028
            // L11 (0x0054D138..0x0054D15A): ~MoveLiftToHeightAction releases its MotorActionAck subscription (+0xA0) first, then
            // tails to the IActionRunner destructor (the stop and unlock tail, the watcher's ActionEnding).
            if (!IsHead) Owner.ReleaseAckSubscription(this);
            base.WatcherEnding();
            Owner.ActionEnded(this);
        }
    }

    // fidelity: M4-016
    /// <summary>
    /// MA15 IsHeadInPosition: IsNear(robot+0x2FC, target, tolerance + 1e-5) (0x005484F4..0x0054852C, PLT
    /// 0x0084CC0A). Radians::IsNear (0x0084CC0A) returns |rescale(this − target)| &lt; |tolerance|, a strict &lt;.
    /// Both values are inside (−π, π] here, so rescaling the difference cannot change it.
    /// </summary>
    private bool IsHeadInPositionLocked(MoveAction a) => Math.Abs(_headAngle - a.Target) < a.Tolerance + HeadInPositionSlack;

    // fidelity: M4-016
    /// <summary>MA15 IsLiftInPosition: |target − height| &lt; tolerance and MC+0xB == 0 (0x00548FEA..0x00549034).</summary>
    private bool IsLiftInPositionLocked(MoveAction a) =>
        Math.Abs(a.Target - RobotState.LiftHeightMmFromAngle(_liftAngle ?? 0f)) < a.Tolerance && !_liftMoving;

    private bool InPositionLocked(MoveAction a) => a.IsHead ? IsHeadInPositionLocked(a) : IsLiftInPositionLocked(a);
    private bool MovingLocked(MoveAction a) => a.IsHead ? _headMoving : _liftMoving;

    // fidelity: M4-001, M4-016, M2-002
    /// <summary>
    /// Fed every robot message by <see cref="CozmoRobot"/> (after the state tracker). A MotorActionAck completes the
    /// ack step of the action whose id it carries, when that action's command was sent (MA16). A handled RobotState
    /// updates what the engine keeps: MC+0xA = !HEAD_IN_POS, MC+0xB = !LIFT_IN_POS, MC+0xC = ARE_WHEELS_MOVING (M2
    /// App. B status bits), Robot+0x300 = liftAngle (RS7), and Robot+0x2FC through RS6: ignored until the head is
    /// calibrated, a report below −28° stored as −25° and above 47.5° as 44.5°, with the HeadAngleOOB warning.
    /// Batch 2: the state/ack flags are stored here; CheckIfDone runs on the ActionList tick (L9), not in Handle.
    /// </summary>
    internal void Handle(RobotMessage m)
    {
        lock (_gate)
        {
            switch (m)
            {
                case MotorActionAck ack:
                    foreach (var a in _actions)
                    {
                        // fidelity: M4-028
                        // Row 6: the head callback (0x0054D624..0x0054D68E: guard +0xAA sent and +0xA9 == id; ack flag +0xAB) and the lift one
                        // (0x0054D748..0x0054D7B2: +0x95, +0x94; flag +0x96) are the same shape: they act only when the command was sent and the
                        // id equals the action's, with no test of the ack flag, so a repeated ack logs again. Actions INFO
                        // "<MoveHeadToAngleAction|MoveLiftToHeightAction>.MotorActionAcked", "[%d] ActionID: %d" with (tag +0x60, motor action id).
                        if (!a.Sent || a.Id != ack.ActionId) continue;
                        string name = a.IsHead ? "MoveHeadToAngleAction" : "MoveLiftToHeightAction";
                        Log(FormattableString.Invariant($"info: [Actions] {name}.MotorActionAcked: [{unchecked((int)a.Tag)}] ActionID: {a.Id}"));
                        a.Acked = true;
                    }
                    break;
                case RobotState s:
                    _headMoving = !s.Has(RobotStatusFlag.HeadInPos);
                    _liftMoving = !s.Has(RobotStatusFlag.LiftInPos);
                    _bodyMoving = s.Has(RobotStatusFlag.AreWheelsMoving);
                    _liftAngle = s.LiftAngle;
                    if (_robot.State.HeadCalibrated)
                    {
                        float h = s.HeadAngle;
                        if (h < ReportedHeadLowOob) { Log("warning: Robot.GetCameraHeadPose.HeadAngleOOB"); h = MinHeadAngleRad; }
                        else if (h > ReportedHeadHighOob) { Log("warning: Robot.GetCameraHeadPose.HeadAngleOOB"); h = MaxHeadAngleRad; }
                        _headAngle = h;
                    }
                    break;
            }
        }
    }

    /// <summary>Waits until the robot's reported state satisfies a condition.</summary>
    private async Task<bool> AwaitState(Func<RobotState, bool> condition, TimeSpan within)
    {
        var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        void watch(RobotState s) { if (condition(s)) done.TrySetResult(true); }
        _robot.State.StateUpdated += watch;
        try
        {
            if (_robot.State.Latest is { } now && condition(now)) return true;
            return await Task.WhenAny(done.Task, Task.Delay(within)) == done.Task;
        }
        finally { _robot.State.StateUpdated -= watch; }
    }
}

// fidelity: M4-026
/// <summary>
/// The engine's PathComponent (Robot+0x5C) as far as Robot::AbortAll reaches it: <c>Abort</c> 0x00649100..0x006491BC (A3) and
/// <c>ClearPath</c> 0x00649220..0x006492A0 (A4). The path planner, the status transition, VizManager and PathDolerOuter belong to
/// M11/M12, so what they do is an interface here (the hooks below) and a missing hook is reported once as MISSING, never
/// replaced by a guess. The fields are named by their engine offsets. +0x38 and +0x40 start at 4 and 0xFF (the constructor,
/// 0x00648B14..0x00648B1C); the other values start at zero here (their constructor values are not in the rows), and nothing in this
/// stack writes them but these two functions.
/// </summary>
internal sealed class PathComponent
{
    /// <summary><c>PoseOriginList::UnknownOriginID</c> (GOT 0x0103E978 → 0x00C97B20), 0 (see <c>EngineRobotState</c>).</summary>
    internal const uint UnknownOriginId = 0;

    private readonly CozmoRobot _robot;
    private readonly HashSet<string> _missingReported = new();

    internal PathComponent(CozmoRobot robot) => _robot = robot;

    /// <summary>
    /// +0x38: the ERobotDriveToPoseStatus (M12-023). 4 (Ready) from the constructor (PathComponent::PathComponent 0x00648B14 movs r0,#4;
    /// 0x00648B16 strd r0,r5,[fp,#0x38]). MISSING: it and +0x42 have no production writer yet; the stack's PathSender._pathId
    /// (Manipulation/RobotPath.cs:113, M13-011) and DriveActions hold the engine's values, and wiring them is M12/M13.
    /// </summary>
    internal int DriveToPoseStatus = 4;
    /// <summary>+0x3C: BaseStationTimer seconds, stored by ClearPath.</summary>
    internal float Field3C;
    /// <summary>+0x40: 0xFF from the constructor (0x00648B1A..0x00648B1C), and set to 0xFF by ClearPath.</summary>
    internal byte Field40 = 0xFF;
    /// <summary>+0x42 and +0x4A: ClearPath copies a non-zero +0x42 into +0x4A.</summary>
    internal ushort Field42, Field4A;
    /// <summary>+0x46 (cleared by Abort) and +0x47 (cleared only when a planner is set).</summary>
    internal bool Field46, Field47;
    /// <summary>The shared path pair at +0x30/+0x34: Abort nulls it and releases it.</summary>
    internal object? SharedPath;
    /// <summary>The 12-byte inline objects of the vector at [+0x50]; each one's destructor is its vtable slot 0.</summary>
    internal readonly List<Action> PlannerObjects = new();
    /// <summary>[[+0x50]+0x0C]: Abort stores <see cref="UnknownOriginId"/> here after destroying the objects (0x006491AC..0x006491B4).</summary>
    internal uint PlannerObjectsField0C;

    /// <summary>The planner at +0x20, when set: Abort calls its virtual slot +0x10 (M11).</summary>
    internal Action? PlannerSlot10;
    /// <summary>The ERobotDriveToPoseStatus names, 0x0102F610 (row 4: strings 0x00BFC9C6.. ).</summary>
    internal static readonly string[] StatusNames =
    {
        "Failed", "ComputingPath", "WaitingToBeginPath", "FollowingPath", "Ready", "WaitingToCancelPath", "WaitingToCancelPathAndSetFailure",
    };
    /// <summary>Optional override of the name lookup (tests, M11).</summary>
    internal Func<int, string>? StatusName;
    /// <summary><c>PathComponent::SetDriveToPoseStatus</c> 0x006492C4 (M11).</summary>
    internal Action<int>? SetDriveToPoseStatusHook;
    /// <summary><c>VizManager::ErasePath(robotId)</c> 0x006BFACE (M11).</summary>
    internal Action<uint>? ErasePathHook;
    /// <summary>PathDolerOuter at +4, when set: <c>PathDolerOuter::ClearPath</c> 0x005082B0 (M11).</summary>
    internal Action? PathDolerClearPath;

    internal void ResetToConstructed()
    {
        DriveToPoseStatus = 4;
        Field3C = 0f;
        Field40 = 0xFF;
        Field42 = Field4A = 0;
        Field46 = Field47 = false;
        SharedPath = null;
        PlannerObjects.Clear();
        PlannerObjectsField0C = 0;
        _missingReported.Clear();
    }

    private void MissingOnce(string what)
    {
        if (_missingReported.Add(what)) _robot.Engine.Log($"MISSING: {what}");
    }

    // fidelity: M4-026
    /// <summary>
    /// PathComponent::Abort (A3), in this order: Planner INFO "Aborting from status '%s'" (event "PathComponent.Abort", the
    /// status name from the table indexed by +0x38); when a planner is set, its virtual +0x10 and +0x47 = 0 (only then); the
    /// shared path pair nulled and released; ClearPath (its result is the return value); for a status of 0, 1 or 4 request
    /// status 4, for 2 or 3 request 5, above 4 nothing (0x0064916E..0x00649188); +0x46 = 0; the stored objects destroyed from the
    /// last to the first through their vtable slot 0, in place, with no operator delete (0x00649198..0x006491A4); then
    /// UnknownOriginID stored into [[+0x50]]+0x0C.
    /// </summary>
    internal int Abort(Func<RobotMessage, bool>? send = null)
    {
        // Row 4: the static table at 0x0102F610 is indexed by +0x38 with no bounds check; slot 7 holds 0 in the file image, so a value of 7 or
        // more has no name and what the engine does with it is UNKNOWN (visible placeholder).
        string name = StatusName?.Invoke(DriveToPoseStatus)
                      ?? ((uint)DriveToPoseStatus < StatusNames.Length ? StatusNames[DriveToPoseStatus]
                          : $"<MISSING: ERobotDriveToPoseStatus name for status {DriveToPoseStatus}, read past the table at 0x0102F610>");
        _robot.Engine.Log($"info: [Planner] PathComponent.Abort: Aborting from status '{name}'");
        if (PlannerSlot10 is { } planner) { planner(); Field47 = false; }
        SharedPath = null;
        int cleared = ClearPath(send);
        int status = DriveToPoseStatus;
        if ((uint)status <= 4)
        {
            int request = ((0x13 >> status) & 1) != 0 ? 4 : 5;
            if (SetDriveToPoseStatusHook is { } set) set(request);
            else MissingOnce("PathComponent::SetDriveToPoseStatus 0x006492C4 (M11): the status transition is not built");
        }
        Field46 = false;
        while (PlannerObjects.Count > 0)
        {
            var destroy = PlannerObjects[^1];
            PlannerObjects.RemoveAt(PlannerObjects.Count - 1);          // the end pointer moves before the call (0x0064919C)
            destroy();
        }
        PlannerObjectsField0C = UnknownOriginId;
        return cleared;
    }

    // fidelity: M4-026
    /// <summary>
    /// PathComponent::ClearPath (A4): a non-zero u16 +0x42 is copied to +0x4A; <c>VizManager::ErasePath</c>(robot id); the
    /// PathDolerOuter's ClearPath when it is set; +0x40 = 0xFF; +0x3C = BaseStationTimer seconds; then ClearPath {u16 0}
    /// sent reliable and not hot, its result returned.
    /// </summary>
    internal int ClearPath(Func<RobotMessage, bool>? send = null)
    {
        if (Field42 != 0) Field4A = Field42;
        if (ErasePathHook is { } erase) erase(CozmoEngine.RobotId);
        else MissingOnce("VizManager::ErasePath 0x006BFACE (M11): the visualization recipient is not built");
        // PathDolerOuter at +4 is built when the context is set (0x00648B56) by the engine (ctor 0x00648B76..0x00648B92; call 0x0064924A..0x0064924E).
        if (PathDolerClearPath is { } doler) doler();
        else MissingOnce("PathDolerOuter::ClearPath 0x005082B0 (M11): the path doler is not built");
        Field40 = 0xFF;
        Field3C = _robot.Engine.Timer.SecondsF;
        // Robot::SendMessage's Result: 0 success, 1 failure (not connected); ClearPath returns it (0x00649280).
        var message = new Cozmo.Protocol.ClearPath { Unknown = 0 };
        return (send is null ? _robot.SendMessage(message) : send(message)) ? 0 : 1;
    }
}
