using Cozmo.Robot;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// CUBE-TAP-WORLD-OWNER-1 + LOCALIZATION-CANDIDATE-OWNERS-1 (M11-044, M4-023, M12-040, M13-036). Every expected value is a row of the approved
/// inventory .scratch/cube-tap-localization-guards-census.md (sections A, B, C and its build order), cited per assertion. The clock is the engine's
/// (u32)(seconds x 1000.0) (A "Clock", 0x0084BCC0..0x0084BCCC); <see cref="TickTo"/> puts it on an exact millisecond.
/// </summary>
[Collection("M5 process statics")]
public sealed class CubeTapLocalizationGuardTests
{
    private const uint Slot = 1;

    private static void TickTo(M4ControlTests.Rig rig, uint ms)
    {
        long origin = rig.NowNs - rig.Engine.Timer.Nanoseconds;   // the timer counts from the run's first tick
        rig.NowNs = origin + (long)ms * 1_000_000 + 100;          // +100 ns keeps the truncation on ms
        rig.Engine.Tick();
        Assert.Equal(ms, rig.Engine.Timer.TimeStampMs);
    }

    private static (M4ControlTests.Rig Rig, VisionSystem Vision, uint W) Connected()
    {
        var rig = new M4ControlTests.Rig();
        var vision = rig.Robot.Vision; vision.Calibration = CameraCalibration.Nominal(); vision.Enabled = false;
        rig.ToSynced();
        rig.State();
        M4ControlTests.ConnectCube(rig, slot: Slot);
        uint w = vision.World.ConnectedObjectIdForActiveId(Slot)!.Value;
        return (rig, vision, w);
    }

    private static ObjectTapped Tap(uint activeId, sbyte pos = 40, sbyte neg = -40) => new() { Timestamp = 1, ObjectID = activeId, TapPos = pos, TapNeg = neg };

    private static uint Base(M4ControlTests.Rig rig) => rig.Engine.Timer.TimeStampMs + 100;

    // ------------------------------------------------------------------ step 1: keyed on the World ObjectID; predicate by ObjectID

    /// <summary>
    /// A "First tap" 0x006315E8..0x0063163A: now &gt;= doubleTapTime gives {T+500, 0, T+500, 1}. A "Predicate" 0x006316C0..0x006316DC: ignoreMovementUntil &gt; now,
    /// unsigned strict: true at T and T+499, false at T+500; an absent key is false. A "Key" 0x00630BF6..0x00630C04: the map key is the World ObjectID.
    /// </summary>
    // reach: production — ObjectTapped through the robot-message dispatch and the engine tick; the predicate is read through the bound BlockWorld hook.
    [Fact]
    public void M4_023_M11_044_AFirstTapOpensTheWindowOnTheWorldObjectId()
    {
        var (rig, vision, w) = Connected(); using var _ = rig;
        uint t = Base(rig);
        rig.Data(Tap(Slot)); TickTo(rig, t);
        Assert.Equal((t + 500, false, t + 500, true), rig.Robot.Cubes.DoubleTapEntry(w));
        Assert.Null(rig.Robot.Cubes.DoubleTapEntry(Slot == w ? w + 1 : Slot));   // the key is not the activeID
        Assert.True(vision.World.WasObjectTappedRecently!(w));
        Assert.False(vision.World.WasObjectTappedRecently!(w + 100));           // absent key -> 0
        TickTo(rig, t + 499);
        Assert.True(vision.World.WasObjectTappedRecently!(w));
        TickTo(rig, t + 500);
        Assert.False(vision.World.WasObjectTappedRecently!(w));
    }

    /// <summary>
    /// A "Double tap" 0x006315F8..0x0063163A: now &lt; doubleTapTime is a double tap: the info log on channel "BlockPool", key
    /// BlockTapFilterComponent.Update.DoubleTap, "Detected double tap id:%u" (the key); doubleTapTime := 0, isIgnoring := 0; the deadline unchanged.
    /// </summary>
    // reach: production — two ObjectTapped messages through the dispatch and tick.
    [Fact]
    public void M4_023_ASecondTapInsideTheWindowIsADoubleTapAndKeepsTheDeadline()
    {
        var (rig, vision, w) = Connected(); using var _ = rig;
        var entries = new List<EngineLogEntry>(); rig.Engine.LogEntry += entries.Add;
        uint t = Base(rig);
        rig.Data(Tap(Slot)); TickTo(rig, t);
        rig.Data(Tap(Slot)); TickTo(rig, t + 499);
        Assert.Equal((0u, false, t + 500, false), rig.Robot.Cubes.DoubleTapEntry(w));
        Assert.True(vision.World.WasObjectTappedRecently!(w));
        var log = Assert.Single(entries.Where(e => e.Name == "BlockTapFilterComponent.Update.DoubleTap"));
        Assert.Equal((EngineLogLevel.Info, "BlockPool", $"Detected double tap id:{w}"), (log.Level, log.Channel, log.Text));
    }

    /// <summary>A "First tap": at now == doubleTapTime (T+500) the unsigned <c>bhs</c> takes the first-tap branch: until T+1000, isIgnoring 1.</summary>
    // reach: production — two ObjectTapped messages through the dispatch and tick.
    [Fact]
    public void M4_023_ASecondTapAtTheWindowEndIsANewFirstTap()
    {
        var (rig, _, w) = Connected(); using var __ = rig;
        uint t = Base(rig);
        rig.Data(Tap(Slot)); TickTo(rig, t);
        rig.Data(Tap(Slot)); TickTo(rig, t + 500);
        Assert.Equal((t + 1000, false, t + 1000, true), rig.Robot.Cubes.DoubleTapEntry(w));
    }

    /// <summary>
    /// A "Moved handler" 0x00630DB4..0x00630E58: absent: emplace {0,1,0,0}. A "Moving" 0x006315DE..0x006315E6: isMoving set: doubleTapTime := 0 and return, so the
    /// deadline stays 0. A "Stopped handler" 0x00630F66..0x00630F86: found: isMoving := 0.
    /// </summary>
    // reach: production — ObjectMoved, ObjectTapped and ObjectStoppedMoving through the dispatch and tick.
    [Fact]
    public void M4_023_AMovedBeforeAnyTapCreatesAMovingEntryAndTheTapLeavesNoDeadline()
    {
        var (rig, vision, w) = Connected(); using var _ = rig;
        uint t = Base(rig);
        rig.Data(new ObjectMoved { Timestamp = 2, ObjectID = Slot }); TickTo(rig, t);
        Assert.Equal((0u, true, 0u, false), rig.Robot.Cubes.DoubleTapEntry(w));
        rig.Data(Tap(Slot)); TickTo(rig, t + 10);
        Assert.Equal((0u, true, 0u, false), rig.Robot.Cubes.DoubleTapEntry(w));
        Assert.False(vision.World.WasObjectTappedRecently!(w));
        rig.Data(new ObjectStoppedMoving { Timestamp = 3, ObjectID = Slot }); TickTo(rig, t + 20);
        Assert.Equal((0u, false, 0u, false), rig.Robot.Cubes.DoubleTapEntry(w));
    }

    /// <summary>
    /// A "Intake" 0x00630B34..0x00630B42: intensity (s8)pos - (s8)neg &lt;= 60 is ignored with no entry. A "Identity" 0x00630B72..0x00630B80: no connected object:
    /// the warning "Could not find match for active object ID %d" (sWarningF 0x00630CA6) and no entry.
    /// </summary>
    // reach: production — ObjectTapped through the dispatch and tick.
    [Fact]
    public void M4_023_AnIgnoredOrUnknownTapCreatesNoEntry()
    {
        var (rig, _, w) = Connected(); using var __ = rig;
        var entries = new List<EngineLogEntry>(); rig.Engine.LogEntry += entries.Add;
        uint t = Base(rig);
        rig.Data(Tap(Slot, 30, -30)); TickTo(rig, t);
        Assert.Null(rig.Robot.Cubes.DoubleTapEntry(w));
        rig.Data(Tap(3)); TickTo(rig, t + 10);
        Assert.Null(rig.Robot.Cubes.DoubleTapEntry(w));
        var warning = Assert.Single(entries.Where(e => e.Name == "BlockTapFilterComponent.HandleActiveObjectTapped.UnknownActiveID"));
        Assert.Equal(EngineLogLevel.Warning, warning.Level);
        Assert.EndsWith("Could not find match for active object ID 3", warning.Text);   // the warning sink adds its "(tcNNNN) : " prefix
    }

    /// <summary>A "Lifetime" 0x00511202, 0x00630AD4: no entry is ever erased; a cube disconnect leaves the entry and the predicate.</summary>
    // reach: production — ObjectTapped and a disconnecting ObjectConnectionState through the dispatch and tick.
    [Fact]
    public void M4_023_EntriesSurviveACubeDisconnect()
    {
        var (rig, vision, w) = Connected(); using var _ = rig;
        uint t = Base(rig);
        rig.Data(Tap(Slot)); TickTo(rig, t);
        rig.Data(new ObjectConnectionState { ObjectID = Slot, FactoryID = 0x1000 + Slot, ObjectType = ObjectType.Block_LIGHTCUBE1, Connected = false });
        TickTo(rig, t + 10);
        Assert.Null(vision.World.ConnectedObjectIdForActiveId(Slot));
        Assert.Equal((t + 500, false, t + 500, true), rig.Robot.Cubes.DoubleTapEntry(w));
        Assert.True(vision.World.WasObjectTappedRecently!(w));
    }

    /// <summary>A "Predicate": the compare is unsigned (<c>hi</c>): ignoreMovementUntil 0x80000100 against now 0x10 is true (a signed difference would say false).</summary>
    // reach: unit — the engine clock is substituted: it starts near 0x80000000 ms and is wound back to 0x10 ms, which production never does in one run.
    [Fact]
    public void M4_023_ThePredicateComparesUnsigned()
    {
        const uint until = 0x80000100;
        uint tapAt = until - 500;
        var (rig, vision, w) = Connected(); using var _ = rig;
        rig.Data(Tap(Slot)); TickTo(rig, tapAt);
        Assert.Equal((until, false, until, true), rig.Robot.Cubes.DoubleTapEntry(w));
        TickTo(rig, 0x10);
        Assert.True(vision.World.WasObjectTappedRecently!(w));
    }

    // ------------------------------------------------------------------ step 2: expiry

    private static ObservableObject Located(VisionSystem vision, uint w, uint origin, PoseState state)
    {
        var o = new ObservableObject(w, ObjectType.Block_LIGHTCUBE1, CubeGeometry.MarkersFor(ObjectType.Block_LIGHTCUBE1))
        { Pose = Pose3d.Identity, PoseState = state, OriginId = origin };
        vision.World.AddLocatedObject(o);
        return o;
    }

    /// <summary>
    /// A "Expiry flag" 0x00631228..0x00631242: isIgnoring and ignoreMovementUntil &lt;= now (unsigned) clear the flag, so at now = T+500 exactly. A "Expiry dirty"
    /// 0x0063128A..0x00631360: every origin (mode 2), IsActive and ID == key, pose state Known (+0x24 == 1): MarkObjectDirty(obj, false); non-Known copies are
    /// untouched and no connected cube is needed. A "Expiry log" 0x006312EE..0x0063131A: debug, channel "BlockTapFilterComponent", key
    /// BlockTapFilterComponent.Update.ExpiredTap, "Marking object %d as dirty due to tap timeout".
    /// </summary>
    // reach: production — ObjectTapped and the disconnect through the dispatch, the expiry through the engine tick (BlockTapFilterComponent::Update); the
    // located copies in three origins are fixture state.
    [Fact]
    public void M4_023_ExpiryMarksEveryKnownCopyInEveryOriginWithoutAConnectedCube()
    {
        var (rig, vision, w) = Connected(); using var _ = rig;
        var entries = new List<EngineLogEntry>(); rig.Engine.LogEntry += entries.Add;
        uint here = vision.World.CurrentOriginId;
        var a = Located(vision, w, here, PoseState.Known);
        var b = Located(vision, w, here + 7, PoseState.Known);
        var c = Located(vision, w, here + 8, PoseState.Unknown);
        uint t = Base(rig);
        rig.Data(Tap(Slot)); TickTo(rig, t);
        rig.Data(new ObjectConnectionState { ObjectID = Slot, FactoryID = 0x1000 + Slot, ObjectType = ObjectType.Block_LIGHTCUBE1, Connected = false });
        TickTo(rig, t + 499);
        Assert.Equal(PoseState.Known, a.PoseState); Assert.Equal(PoseState.Known, b.PoseState);
        Assert.True(rig.Robot.Cubes.DoubleTapEntry(w)!.Value.IsIgnoring);
        TickTo(rig, t + 500);
        Assert.False(rig.Robot.Cubes.DoubleTapEntry(w)!.Value.IsIgnoring);
        Assert.Equal(PoseState.Dirty, a.PoseState);
        Assert.Equal(PoseState.Dirty, b.PoseState);
        Assert.Equal(PoseState.Unknown, c.PoseState);
        // The ExpiredTap log is once per key, before the marking, and only if GetConnectedActiveObjectByActiveIdHelper(key's ObjectID value used as an
        // active ID) finds an object (0x006312EE..0x006312FA, cbz at 0x006312FA). The cube is disconnected here, so nothing is logged although two
        // copies were marked.
        Assert.DoesNotContain(entries, e => e.Name == "BlockTapFilterComponent.Update.ExpiredTap");
    }

    /// <summary>A "Double tap" clears isIgnoring, so the A "Expiry flag" test (isIgnoring != 0) never passes and nothing is marked.</summary>
    // reach: production — two ObjectTapped messages through the dispatch, the expiry through the engine tick; the located copy is fixture state.
    [Fact]
    public void M4_023_AfterADoubleTapNothingIsMarked()
    {
        var (rig, vision, w) = Connected(); using var _ = rig;
        var a = Located(vision, w, vision.World.CurrentOriginId, PoseState.Known);
        uint t = Base(rig);
        rig.Data(Tap(Slot)); TickTo(rig, t);
        rig.Data(Tap(Slot)); TickTo(rig, t + 100);
        TickTo(rig, t + 600);
        Assert.Equal(PoseState.Known, a.PoseState);
    }

    // ------------------------------------------------------------------ TAP-FILTER-TICK-POSITION-1: where the tap filter runs in the tick

    /// <summary>A queued runner that only observes: it runs inside ActionList::Update and reports Running, so the list keeps it.</summary>
    private sealed class ProbeAction : Cozmo.Robot.IActionRunner
    {
        public uint State { get; set; } = EngineActionResult.NotStarted;
        public byte RetriesRemain { get; set; }
        public uint OriginalTag { get; set; }
        public uint Tag { get; set; }
        public int Type { get; set; }
        public uint RequiredTrackMask { get; set; }
        public bool SuppressTrackLocking { get; set; } = true;
        public Action? OnUpdate;
        public uint Update() { OnUpdate?.Invoke(); State = EngineActionResult.Running; return State; }
        public void Cancel() { if (State != EngineActionResult.NotStarted) State = EngineActionResult.Cancelled; }
        public void Reset(bool unlockTracks) { }
        public bool CanInterrupt() => true;
        public void Prep() { }
        public void WatcherEnding() { }
        public void UnlockTracks() { }
        public bool SetTag(uint tag) { Tag = tag; return true; }
    }

    /// <summary>
    /// M4-023, tick position: Robot::Update calls BlockTapFilterComponent::Update at 0x00513EA0..0x00513EA4, before AIComponent::Update
    /// (0x00513EA8..0x00513EAC) and ActionList::Update (0x005140BC). A "Expiry flag" 0x00631228..0x00631242 (until &lt;= now) and A "Expiry dirty"
    /// 0x0063128A..0x00631360 (MarkObjectDirty) therefore happen on the T+500 tick before the action list runs: an action's Update on that tick
    /// already reads Dirty, and on T and T+499 it reads Known.
    /// </summary>
    // reach: production for the tap (dispatch) and the expiry (engine tick, Robot::Update); the observer is a probe runner queued on the robot's
    // real ActionList (it substitutes a concrete action, whose own reads are not under test), and the located copy is fixture state.
    [Fact]
    public void M4_023_TheExpiryTickMarksDirtyBeforeTheActionListRuns()
    {
        var (rig, vision, w) = Connected(); using var _ = rig;
        var a = Located(vision, w, vision.World.CurrentOriginId, PoseState.Known);
        var seen = new List<(uint Ms, PoseState State)>();
        var probe = new ProbeAction { OnUpdate = () => seen.Add((rig.Engine.Timer.TimeStampMs, a.PoseState)) };
        uint t = Base(rig);
        rig.Data(Tap(Slot)); TickTo(rig, t);
        Assert.Equal(0, rig.Engine.Robot!.ActionList.QueueAction(QueueActionPosition.Now, probe));
        TickTo(rig, t + 499);
        TickTo(rig, t + 500);
        Assert.Equal(new[] { (t + 499, PoseState.Known), (t + 500, PoseState.Dirty) }, seen.Where(x => x.Ms >= t + 499).ToArray());
    }

    /// <summary>
    /// M4-023, tick position: CD10f (0x006310D0..0x0063119E) flushes the tap queue inside BlockTapFilterComponent::Update, which Robot::Update calls at
    /// 0x00513EA0..0x00513EA4, before ActionList::Update (0x005140BC). So on the tick whose BlockTapFilter update broadcasts the tap, the action list
    /// already sees the tap recorded; the broadcast is enqueued on the app FIFO before anything the action list enqueues on that tick.
    /// </summary>
    // reach: production for the tap (dispatch, the 75 ms queue on a physical robot with the filter on) and the flush (engine tick); the observer is a
    // probe runner on the robot's real ActionList. This checks the enqueue point, not a delivered action report.
    [Fact]
    public void M4_023_TheTapFlushRunsBeforeTheActionListInTheSameTick()
    {
        var (rig, _, _) = Connected(); using var r = rig;
        Assert.True(rig.Engine.Robot!.IsPhysicalRobot);
        var cube = rig.Robot.Cubes.ByObjectId(Slot)!;
        var seen = new List<(uint Ms, int Taps)>();
        var delivered = new List<uint>();
        rig.Robot.Cubes.CubeTapped += _ => delivered.Add(rig.Engine.Timer.TimeStampMs);
        var probe = new ProbeAction { OnUpdate = () => seen.Add((rig.Engine.Timer.TimeStampMs, cube.Taps)) };
        Assert.Equal(0, rig.Engine.Robot!.ActionList.QueueAction(QueueActionPosition.Now, probe));
        uint t = Base(rig);
        TickTo(rig, t);
        rig.Data(Tap(Slot)); TickTo(rig, t + 10);
        for (uint ms = t + 20; ms <= t + 200 && delivered.Count == 0; ms += 10) TickTo(rig, ms);
        uint flushTick = Assert.Single(delivered);
        Assert.Equal(1, seen.Single(x => x.Ms == flushTick).Taps);
        Assert.All(seen.Where(x => x.Ms < flushTick), x => Assert.Equal(0, x.Taps));
    }

    // ------------------------------------------------------------------ step 3: both guards bound

    private static ObservableObject Candidate(uint w) => new(w, ObjectType.Block_LIGHTCUBE1, CubeGeometry.MarkersFor(ObjectType.Block_LIGHTCUBE1))
    { Pose = Pose3d.Identity, PoseState = PoseState.Known, ActiveId = (int)Slot, FromDistance = 10 };

    /// <summary>
    /// C "CouldUseObjectForLocalization" 0x0050D170..0x0050D1C6: <c>!WasObjectTappedRecently(id)</c>, which is the A "Predicate" (A "Robot wrapper" 0x00515216..0x0051521A):
    /// false at T+499, true at T+500.
    /// </summary>
    // reach: unit — CouldUseObjectForLocalization is called directly with a candidate that is not in the world (the Insert caller 0x0050CE5E is not driven); the
    // tap predicate is the production one VisionSystem binds, fed by ObjectTapped through the dispatch.
    [Fact]
    public void M11_044_CouldUseIsRefusedInsideTheTapWindowOnly()
    {
        var (rig, vision, w) = Connected(); using var _ = rig;
        var candidate = Candidate(w);
        uint t = Base(rig);
        rig.Data(Tap(Slot)); TickTo(rig, t + 0);
        TickTo(rig, t + 499);
        Assert.False(vision.World.CouldUseObjectForLocalization(candidate));
        TickTo(rig, t + 500);
        Assert.True(vision.World.CouldUseObjectForLocalization(candidate));
    }

    /// <summary>C "CouldUseObjectForLocalization": all operands are fetched before any branch, so the tap predicate is evaluated even when the dock test fails.</summary>
    // reach: unit — the bound hooks are wrapped to count calls; the dock target is the production DockingSystem's after a native DockWithObject.
    [Fact]
    public void M11_044_CouldUseFetchesTheTapPredicateEvenWhenTheDockTestFails()
    {
        var (rig, vision, w) = Connected(); using var _ = rig;
        using var m = new ManipulationSystem(rig.Robot, vision);
        int asked = 0; var bound = vision.World.WasObjectTappedRecently!;
        vision.World.WasObjectTappedRecently = id => { asked++; return bound(id); };
        var dockTarget = vision.World.DockingObjectId!;
        vision.World.DockingObjectId = () => w;
        Assert.False(vision.World.CouldUseObjectForLocalization(Candidate(w)));
        Assert.Equal(1, asked);
        vision.World.DockingObjectId = dockTarget;
    }

    /// <summary>
    /// C "LocalizeToObject head" 0x005154B0..0x00515612: (3) CanBeUsedForLocalization() == 1, (4) only then the tap predicate; a 1 there is the UnlocalizedObject
    /// error and return 1; (5) no Robot field is written before 0x00515612.
    /// </summary>
    // reach: unit — LocalizeToObject is called directly (its UpdateObservedMarkers caller is not driven); the predicate is the bound production one, wrapped to count.
    [Fact]
    public void M11_044_LocalizeToObjectRefusesATappedObjectAndAsksTheTapOnlyAfterTheVtableTest()
    {
        var (rig, vision, w) = Connected(); using var _ = rig;
        int asked = 0; var bound = vision.World.ShouldIgnoreMovementDueToDoubleTap!;
        vision.World.ShouldIgnoreMovementDueToDoubleTap = id => { asked++; return bound(id); };
        uint t = Base(rig);
        rig.Data(Tap(Slot)); TickTo(rig, t);
        TickTo(rig, t + 499);
        var localizedBefore = vision.World.LocalizedToObjectId; int unbuiltBefore = vision.World.UnbuiltLocalizeToObjectCalls;
        Assert.Equal(1u, vision.World.LocalizeToObject(null, Candidate(w)));
        Assert.Equal(1, asked);
        Assert.True(rig.Logged("Robot.LocalizeToObject.UnlocalizedObject"));
        Assert.Equal(localizedBefore, vision.World.LocalizedToObjectId);
        Assert.Equal(unbuiltBefore, vision.World.UnbuiltLocalizeToObjectCalls);
        var notUsable = Candidate(w); notUsable.PoseState = PoseState.Dirty;   // CanBeUsedForLocalization() != 1
        Assert.Equal(1u, vision.World.LocalizeToObject(null, notUsable));
        Assert.Equal(1, asked);
    }

    // ------------------------------------------------------------------ step 4: the dock target owner

    private sealed class DockFixture : IDisposable
    {
        internal readonly M4ControlTests.Rig Rig;
        internal readonly VisionSystem Vision;
        internal readonly ManipulationSystem M;
        internal EngineRobot Native => Rig.Engine.Robot!;
        internal DockFixture()
        {
            Rig = new M4ControlTests.Rig(); Rig.ToSynced();
            Vision = Rig.Robot.Vision; Vision.Calibration = CameraCalibration.Nominal(); Vision.Enabled = false;
            M = new ManipulationSystem(Rig.Robot, Vision);
            Vision.World.CurrentOriginId = Native.CurrentOriginId;
        }
        internal ObservableObject Add(uint id, Vec3 position)
        {
            var obj = new ObservableObject(id, ObjectType.Block_LIGHTCUBE1, CubeGeometry.MarkersFor(ObjectType.Block_LIGHTCUBE1))
            { Pose = new Pose3d(Rotation3d.Identity, position, parent: Native.GetNativeOrigin(Native.CurrentOriginId)), PoseState = PoseState.Known,
              OriginId = Vision.World.CurrentOriginId, FromDistance = 10 };
            Vision.World.AddLocatedObject(obj); return obj;
        }
        public void Dispose() { M.Dispose(); Vision.Dispose(); Rig.Dispose(); }
    }

    /// <summary>
    /// B "DockingComponent+0xC": the constructor's -1 (0x0063BA1E..0x0063BA30) is none; DockWithObject (0x0063BA88..0x0063BAB6) stores the ID after the located
    /// lookup, which C "CouldUseObjectForLocalization" then refuses, other objects unaffected; a failed lookup skips both writes.
    /// </summary>
    // reach: unit — DockingSystem.StartNativeDock is called directly (its IDockAction::CheckIfDone caller 0x005522AE is not driven); the BlockWorld hook is the
    // production one DockingSystem installs.
    [Fact]
    public void M12_040_M11_044_TheDockTargetIsTheDockingComponentsAndExcludesOnlyThatObject()
    {
        using var f = new DockFixture();
        Assert.NotNull(f.Vision.World.DockingObjectId);
        Assert.Null(f.Vision.World.DockingObjectId!());
        var target = f.Add(7, new Vec3(200, 0, 22)); var other = f.Add(8, new Vec3(300, 0, 22));
        bool otherBefore = f.Vision.World.CouldUseObjectForLocalization(other);
        short code = unchecked((short)target.Markers[0].Code);
        f.M.Docking.StartNativeDock(7, code, DockAction.PickupLow, PathMotionProfile.Default);
        Assert.Equal(7u, f.Vision.World.DockingObjectId!());
        Assert.False(f.Vision.World.CouldUseObjectForLocalization(target));
        Assert.Equal(otherBefore, f.Vision.World.CouldUseObjectForLocalization(other));
        Assert.False(f.M.Docking.StartNativeDock(99, code, DockAction.PickupLow, PathMotionProfile.Default));
        Assert.Equal(7u, f.Vision.World.DockingObjectId!());
    }

    /// <summary>
    /// B "IDockAction::~IDockAction" 0x00557474..0x0055748C: at every IDockAction-family end, AbortDocking iff DockingComponent+4 != 0, then marker +0x10 := 3 and
    /// target +0xC := -1. Driven through the legacy <see cref="DockActionBase.RunAsync"/> of PickupObjectAction and AlignWithObjectAction ending on a failure
    /// (the object is not located: "IDockAction.NullDockObject"); success and cancel end in the same finally.
    /// </summary>
    // reach: unit — the actions' RunAsync is called directly and the target is set by StartNativeDock beforehand; the picking-or-placing byte comes from a
    // RobotState through the dispatch (Sensors, 0x00512A9C..0x00512AA0).
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task M12_040_EveryDockActionEndResetsTheTargetAndAbortsOnlyWhilePickingOrPlacing(bool pickingOrPlacing, bool align)
    {
        using var f = new DockFixture();
        var target = f.Add(7, new Vec3(200, 0, 22));
        f.M.Docking.StartNativeDock(7, unchecked((short)target.Markers[0].Code), DockAction.PickupLow, PathMotionProfile.Default);
        Assert.Equal(7u, f.M.Docking.DockTargetObjectId);
        f.Rig.State(flags: pickingOrPlacing ? RobotStatusFlag.IsPickingOrPlacing : RobotStatusFlag.IsBodyAccMode);
        Assert.Equal(pickingOrPlacing ? (byte)1 : (byte)0, f.Native.DockingOwner!.Active);
        int mark = f.Rig.Mark();
        DockActionBase action = align ? new AlignWithObjectAction(f.M, 55, 0f, default) : new PickupObjectAction(f.M, 55);
        Assert.Equal(ActionResult.BadObject, await action.RunAsync(CancellationToken.None));
        Assert.Null(f.M.Docking.DockTargetObjectId);
        Assert.Null(f.Vision.World.DockingObjectId!());
        Assert.Equal((ushort)3, f.Native.DockingOwner!.MarkerCode);
        Assert.Equal(pickingOrPlacing ? 1 : 0, f.Rig.SentSince(mark).Count(m => m is AbortDocking));
    }

    /// <summary>
    /// The engine sends AbortDocking only from ~IDockAction (0x0055747A..0x0055747C, gated by DockingComponent+4) and from
    /// Robot::AbortAll (0x0051194C): a timed-out dock gets one abort, never two. The legacy DockAsync timeout branch sends its own;
    /// the action-end reset must not add a second while picking-or-placing is still set.
    /// </summary>
    // reach: unit — DockAsync and the action-end reset are called directly with a short timeout; the robot never answers.
    [Fact]
    public async Task M12_040_ATimedOutLegacyDockGetsOneAbortNotTwo()
    {
        using var f = new DockFixture();
        var target = f.Add(7, new Vec3(200, 0, 22));
        f.Rig.State(flags: RobotStatusFlag.IsPickingOrPlacing);
        int mark = f.Rig.Mark();
        var result = await f.M.Docking.DockAsync(target, target.Markers[0], DockAction.PickupLow, PathMotionProfile.Default, timeout: TimeSpan.FromMilliseconds(50));
        Assert.Null(result);
        Assert.Equal(1, f.Rig.SentSince(mark).Count(m => m is AbortDocking));
        f.Rig.State(flags: RobotStatusFlag.IsPickingOrPlacing);
        Assert.Equal((byte)1, f.Native.DockingOwner!.Active);
        f.M.Docking.ResetDockTargetAtActionEnd();
        Assert.Equal(1, f.Rig.SentSince(mark).Count(m => m is AbortDocking));
        Assert.Null(f.M.Docking.DockTargetObjectId);
        Assert.Equal((ushort)3, f.Native.DockingOwner!.MarkerCode);
        // The next dock starts clean: its own end aborts again when picking-or-placing is set.
        f.M.Docking.ResetDockTargetAtActionEnd();
        Assert.Equal(2, f.Rig.SentSince(mark).Count(m => m is AbortDocking));
    }

    // ------------------------------------------------------------------ step 5: the tracked-object owner

    /// <summary>B "MovementComponent+0x20": constructor -1 (0x0063DA76..0x0063DA86); only TrackObjectAction writes it, and it is excluded (M13-036), so none.</summary>
    // reach: production — the VisionSystem and ManipulationSystem the robot builds leave the hook unwired.
    [Fact]
    public void M13_036_TheTrackedObjectIdIsConstantNone()
    {
        using var f = new DockFixture();
        Assert.Null(f.Vision.World.MovementComponentObjectId);
    }
}
