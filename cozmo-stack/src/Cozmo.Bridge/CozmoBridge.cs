using System.Text.Json;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Vision;

namespace Cozmo.Bridge;

public sealed partial class CozmoBridge
{
    private CozmoRobot Robot { get; }
    private readonly int eventCapacity;
    private readonly object eventGate = new();
    private sealed record FeedEntry(BridgeEvent Full, JsonElement? CompactPayload, bool CompactSignificant, bool PushSignificant);

    private readonly Queue<FeedEntry> events = new();
    private long lastCursor;
    private TaskCompletionSource eventAvailable = NewEventSignal();
    private static TaskCompletionSource NewEventSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool disposed;
    private event Action? Disposing;
    private readonly IDisposable faceRenameRecipient;
    private readonly IDisposable faceEraseRecipient;
    private EngineRobot? faceMutationOwner;
    private readonly Dictionary<int, (FaceEntry Face, uint Timestamp, string? Name)> faceMutations = new();

    /// <summary>Subscribes to public reports. The caller retains ownership of the robot and connection.</summary>
    public CozmoBridge(CozmoRobot robot, int eventCapacity = 1024,
        IReadOnlyCollection<string>? significantEventTypes = null, TimeProvider? timeProvider = null)
    {
        Robot = robot ?? throw new ArgumentNullException(nameof(robot));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(eventCapacity);
        this.eventCapacity = eventCapacity;
        this.timeProvider = timeProvider ?? TimeProvider.System;
        significantTypes = significantEventTypes?.ToHashSet(StringComparer.Ordinal) ?? DefaultSignificantTypes;
        var engine = Robot.Engine;
        engine.RobotObservedFace += OnFace;
        engine.RobotChangedObservedFaceId += OnFaceIdChanged;
        engine.RobotDeletedFace += OnFaceLost;
        faceRenameRecipient = engine.AnimationMessages.SubscribeMessage(0x1D, true, payload =>
        {
            if (payload is FaceRenamedReport report) RecordFaceMutation(report.Id, report.Name);
        });
        faceEraseRecipient = engine.AnimationMessages.SubscribeMessage(0x57, true, payload =>
        {
            if (payload is NativeErasedEnrolledFace report) RecordFaceMutation(report.Id, null);
        });
        engine.RobotObservedObject += OnObject;
        Robot.Cubes.MovementBroadcast += OnCubeMoved;
        Robot.Cubes.TapBroadcast += OnCubeTapped;
        engine.ObjectPoseUnknown += OnObjectPoseLost;
        engine.BehaviorTransition += OnBehavior;
        engine.BehaviorRequestBlocked += OnBehaviorRequestBlocked;
        engine.ReactionTransition += OnReaction;
        engine.NeedsStateReported += OnNeedsState;
        engine.Needs.FreeplaySparksAwarded += OnFreeplaySparksAwarded;
        engine.GoingToSleep += OnGoingToSleep;
        engine.ChargerSleepCancelled += OnChargerSleepCancelled;
        engine.RobotDisconnected += OnRobotDisconnected;
        engine.ActionCompleted += OnActionCompleted;
        AttachSenses();
    }

    /// <summary>Reads events strictly after a cursor. Zero is the initial cursor. Gap indicates older events were evicted.</summary>
    public JsonElement ReadEvents(long since = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(since);
        BridgeEvent[] page;
        long next;
        long oldest;
        lock (eventGate)
        {
            ThrowIfDisposed();
            if (since > lastCursor) throw new ArgumentOutOfRangeException(nameof(since), "Cursor belongs to a different or newer event feed.");
            page = events.Select(e => e.Full).Where(e => e.Cursor > since).ToArray();
            next = lastCursor;
            oldest = events.TryPeek(out var first) ? first.Full.Cursor : 1;
        }
        return JsonSerializer.SerializeToElement(new { cursor = next, oldestCursor = oldest, gap = since < oldest - 1, events = page }, Json);
    }

    /// <summary>Waits without consuming events. Registration and cursor check share the append lock.</summary>
    public async Task<JsonElement> ReadEventsAsync(long since = 0, int waitMs = 0, CancellationToken token = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(waitMs);
        if (waitMs > 30000) throw new ArgumentOutOfRangeException(nameof(waitMs));
        Task signal;
        lock (eventGate)
        {
            var page = ReadEvents(since);
            if (waitMs == 0 || lastCursor > since) return page;
            signal = eventAvailable.Task;
        }
        try { await signal.WaitAsync(TimeSpan.FromMilliseconds(waitMs), token).ConfigureAwait(false); }
        catch (TimeoutException) { }
        return ReadEvents(since);
    }

    /// <summary>Returns the latest connection, device, activity, face and cube state exposed by the robot.</summary>
    public JsonElement GetState()
    {
        ThrowIfDisposed();
        using var ownerRead = Robot.Engine.EnterSynchronousOwner();
        using var poseRead = Robot.Vision.World.EnterPoseMutation();
        bool connected = Robot.Engine.ConnectionState == 2;
        var raw = connected ? Robot.State.Latest : null;
        uint? carried = connected ? Robot.Carrying.CarriedObjectId : null;
        var activity = connected ? Robot.GetActivityObservation() : null;
        var world = Robot.Vision.World;
        var faces = new Dictionary<int, KnownFace>();
        if (connected)
        {
            var observed = Robot.Vision.Faces.Faces;
            var mutations = SnapshotFaceMutations(observed);
            foreach (var face in observed)
            {
                string? name = face.Name;
                if (mutations.TryGetValue(face.Id, out var mutation))
                {
                    if (mutation is null) continue;
                    name = mutation;
                }
                faces.Add(face.Id, new(face.Id, name, face.LastObservedTimestamp, Relative: Relative(face.HeadPose)));
            }
        }
        if (connected)
        {
            foreach (var face in Robot.Vision.Recognizer.EnrolledFaces.Where(f => !string.IsNullOrEmpty(f.Name)))
                faces[face.FaceID] = new(face.FaceID, face.Name, faces.GetValueOrDefault(face.FaceID)?.LastObservedTimestamp, true, faces.GetValueOrDefault(face.FaceID)?.Relative);
        }
        var cubes = new List<KnownCube>();
        if (connected)
        {
            uint origin = world.CurrentOriginId;
            var located = world.LocatedObjects.Where(o => o.OriginId == origin && CubeGeometry.IsCube(o.Type))
                .ToDictionary(o => o.ObjectId);
            var connectedFactories = new HashSet<uint>();
            // The native 0x54 report enumerates the connected BlockWorld objects
            // (0x0061E91C). A slot advertisement alone cannot supply a world ID.
            foreach (var cube in world.ConnectedObjects.Where(c => CozmoCubes.IsLightCube(c.Type)))
            {
                connectedFactories.Add(cube.FactoryId);
                located.Remove(cube.ObjectId, out var observed);
                cubes.Add(new(cube.FactoryId, (uint)cube.ActiveId, cube.ObjectId, cube.Type.ToString(), true,
                    observed?.PoseState.ToString(), observed?.LastObservedTimestamp, observed is null ? null : Relative(observed.NativePose)));
            }
            foreach (var cube in Robot.Cubes.DiscoveredCubes.Where(c => !connectedFactories.Contains(c.FactoryId)))
                cubes.Add(new(cube.FactoryId, cube.ObjectId, null, cube.Type.ToString(), false, null, null, null));
            cubes.Sort((a, b) => Nullable.Compare(a.FactoryId, b.FactoryId));
            foreach (var cube in located.Values.OrderBy(o => o.ObjectId))
            {
                if (Robot.Cubes.IsDisconnectedCubePose(cube.ObjectId)) continue;
                cubes.Add(new(cube.FactoryId == 0 ? null : cube.FactoryId, cube.ActiveId < 0 ? null : (uint)cube.ActiveId,
                    cube.ObjectId, cube.Type.ToString(), false, cube.PoseState.ToString(), cube.LastObservedTimestamp, Relative(cube.NativePose)));
            }
        }

        return JsonSerializer.SerializeToElement(new
        {
            timestampUtc = DateTimeOffset.UtcNow,
            c2Extensions = Robot.Engine.Options.C2.ActiveExtensions,
            cliffHoldActive = connected && Robot.Engine.Robot?.C2CliffHoldActive == true,
            connection = new { connected, ready = connected && Robot.Engine.IsReadyDelivered, link = Robot.Transport.State.ToString() },
            pose = ReadWorldPose(),
            batteryVolts = raw?.BatteryVoltage,
            physical = ReadPhysicalState(raw),
            physicalReactionChecks = connected ? ReadPhysicalReactionChecks() : [],
            isCarrying = connected ? carried.HasValue : (bool?)null,
            carriedObjectId = carried,
            isOnCharger = raw?.Has(RobotStatusFlag.IsOnCharger),
            isCharging = connected && raw is not null ? Robot.State.Charging : (bool?)null,
            knownChargers = connected ? world.LocatedObjects.Where(o => o.OriginId == world.CurrentOriginId && o.Type == ObjectType.Charger_Basic)
                .Select(o => new { worldObjectId = o.ObjectId, poseState = o.PoseState.ToString(), o.LastObservedTimestamp, relative = Relative(o.NativePose) }).ToArray() : [],
            activity = activity?.Activity,
            highLevelActivity = activity?.HighLevelActivity,
            freeplay = Robot.GetFreeplayObservation(),
            activeActions = connected && Robot.Engine.Robot is { } actionOwner
                ? actionOwner.ActionList.GetHostActions(eventCaller)
                    .Select(action => new { actionTag = action.Tag, actionType = RobotActionType.NameOf(action.Type), current = action.Current }).ToArray()
                : [],
            behavior = activity?.Behavior,
            reaction = activity?.Reaction,
            reactionLocks = connected ? Robot.Engine.Robot?.BehaviorManagerOwner?.GetReactionLocks() ?? [] : [],
            needs = ReadNeedsState(),
            knownFaces = faces.Values.OrderBy(f => f.FaceId),
            knownCubes = cubes,
        }, Json);
    }

    private sealed record KnownFace(int FaceId, string? Name, uint? LastObservedTimestamp, bool Enrolled = false, RelativePose? Relative = null);
    private sealed record KnownCube(uint? FactoryId, uint? RadioObjectId, uint? WorldObjectId, string Type, bool Connected,
        string? PoseState, uint? LastObservedTimestamp, RelativePose? Relative);

    /// <summary>Authored animation triggers and clip names loaded by the current robot.</summary>
    public JsonElement ListAnimations()
    {
        ThrowIfDisposed();
        var library = Robot.Animations.Library;
        return JsonSerializer.SerializeToElement(new
        {
            loaded = library is not null,
            triggers = Enum.GetValues<Cozmo.Robot.Behavior.AnimationTrigger>()
                .Where(trigger => library?.HasAnimationForTrigger(trigger) == true).Select(trigger => trigger.ToString()),
            clips = library?.ClipNames ?? Array.Empty<string>(),
        }, Json);
    }

    // Both album mutations leave FaceWorld's observation entry intact. The
    // app applies 0x1D/0x57 to its projection (Robot.cs:629–635 and 504–512).
    // Use the synchronous native reports from 0x00657B74..7C / 0x00657AB6..BE
    // to identify the exact cached sighting superseded by the album operation.
    private void RecordFaceMutation(int id, string? name)
    {
        var owner = Robot.Engine.Robot;
        var observed = Robot.Vision.Faces.Faces;
        lock (eventGate)
        {
            if (disposed || owner is null || !ReferenceEquals(Robot.Engine.Robot, owner)) return;
            PruneFaceMutations(owner, observed);
            if (observed.FirstOrDefault(face => face.Id == id) is { } face)
                faceMutations[id] = (face, face.LastObservedTimestamp, name);
        }
    }

    private Dictionary<int, string?> SnapshotFaceMutations(IReadOnlyList<FaceEntry> observed)
    {
        lock (eventGate)
        {
            PruneFaceMutations(Robot.Engine.Robot, observed);
            return faceMutations.ToDictionary(entry => entry.Key, entry => entry.Value.Name);
        }
    }

    private void PruneFaceMutations(EngineRobot? owner, IReadOnlyList<FaceEntry> observed)
    {
        if (!ReferenceEquals(owner, faceMutationOwner))
        {
            faceMutationOwner = owner;
            faceMutations.Clear();
            return;
        }
        foreach (var (id, mutation) in faceMutations.ToArray())
            if (!observed.Any(face => ReferenceEquals(face, mutation.Face) && face.Id == id &&
                face.LastObservedTimestamp == mutation.Timestamp))
                faceMutations.Remove(id);
    }

    private void OnFace(RobotObservedFaceReport report)
    {
        var payload = JsonSerializer.SerializeToElement(report, Json);
        var compact = PresentFace(report);
        lock (eventGate)
        {
            var (newApproach, newRecognition) = TrackFaceApproach(report);
            bool named = !string.IsNullOrEmpty(report.Name);
            Append("face-seen", report.Timestamp, payload, locatedFaces.Add(report.FaceId), compact,
                compactEligible: newApproach && !named);
            if (named)
                Append("face-recognised", report.Timestamp, payload, FirstRecognitionThisMinute(report.FaceId), compact,
                    compactEligible: newRecognition);
        }
    }
    private void OnFaceIdChanged(RobotChangedObservedFaceIdReport report)
    {
        lock (eventGate)
        {
            if (locatedFaces.Remove(report.OldId)) locatedFaces.Add(report.NewId);
            if (faceApproaches.Remove(report.OldId, out var track) &&
                (!faceApproaches.TryGetValue(report.NewId, out var newer) || newer.LastSeen <= track.LastSeen))
            {
                if (track.Approach.RecognisedId == report.OldId) track.Approach.RecognisedId = report.NewId;
                faceApproaches[report.NewId] = track;
            }
            if (recognisedAt.Remove(report.OldId, out var at) &&
                (!recognisedAt.TryGetValue(report.NewId, out var existing) || at > existing))
                recognisedAt[report.NewId] = at;
            Add("face-id-changed", report);
        }
    }
    private void OnFaceLost(RobotDeletedFaceReport report)
    {
        lock (eventGate)
        {
            locatedFaces.Remove(report.FaceId);
            Add("face-lost", report);
        }
    }

    private void OnObject(RobotObservedObjectReport report)
    {
        string type = report.Family == (uint)ObjectFamily.LightCube ? "cube-seen"
            : report.Type == (uint)ObjectType.Charger_Basic ? "charger-seen" : "object-seen";
        lock (eventGate) Add(type, report, report.Timestamp, eligible: locatedObjects.Add(report.ObjectId));
    }

    private void OnCubeMoved(CubeTimestampEvent report) => AddCubeEvent("cube-moved", report);
    private void OnCubeTapped(CubeTimestampEvent report) => AddCubeEvent("cube-tapped", report);

    private void AddCubeEvent(string type, CubeTimestampEvent report)
    {
        var cube = report.Cube;
        Add(type, new
        {
            report.Timestamp,
            // ObjectMoved caller stores the input axis at 0x005341A0;
            // Pack writes that byte at 0x007B01F2..0x007B0200. Stop has no axis.
            AxisOfAccel = type == "cube-moved" && cube.Moving ? cube.UpAxis : null,
            Cube = new
            {
                cube.FactoryId, cube.ObjectId, cube.WorldObjectId, cube.Type, cube.Connected,
                cube.Moving, cube.Accel,
                LastTap = cube.LastTap is { } tap
                    ? new { tap.Timestamp, ObjectId = tap.ObjectID, tap.NumTaps, tap.TapTime, tap.TapNeg, tap.TapPos }
                    : null,
            },
        }, report.Timestamp);
    }
    private void OnObjectPoseLost(ObjectPoseUnknownReport report)
    {
        lock (eventGate)
        {
            locatedObjects.Remove(report.ObjectId);
            Add("object-pose-lost", report);
        }
    }
    private void OnBehaviorRequestBlocked(BehaviorRequestBlockedReport report) => Add("behavior-request-not-runnable", report);
    private void OnBehavior(BehaviorTransitionReport report)
    {
        var context = Robot.Engine.GetBehaviorTransitionContext(report);
        var compact = JsonSerializer.SerializeToElement(new
        {
            report.OldId, report.NewId, report.OldClass, report.OldExecutable, report.NewClass, report.NewExecutable,
            report.DisplayKey,
            oldBehavior = ((Cozmo.Robot.Behavior.BehaviorID)report.OldId).ToString(),
            newBehavior = ((Cozmo.Robot.Behavior.BehaviorID)report.NewId).ToString(),
            activity = context?.Activity,
            reason = context?.Reason,
        }, Json);
        lock (eventGate)
            Append("behavior-changed", null, JsonSerializer.SerializeToElement(report, Json),
                significantTypes.Contains("behavior-changed"), compactPayload: compact);
    }
    private void OnReaction(ReactionTransitionReport report)
    {
        static string TriggerName(byte trigger) => trigger == 0x16 ? "NoneTrigger" :
            Enum.GetName((Cozmo.Robot.Behavior.ReactionTrigger)trigger) ?? $"Unknown ({trigger})";
        var (oldBehavior, newBehavior) = Robot.Engine.GetReactionBehaviorNames(report);
        var compact = JsonSerializer.SerializeToElement(new
        {
            oldTrigger = TriggerName(report.OldTrigger),
            newTrigger = TriggerName(report.NewTrigger),
            oldBehavior,
            newBehavior,
        }, Json);
        lock (eventGate)
            Append("reaction-changed", null, JsonSerializer.SerializeToElement(report, Json),
                report.OldTrigger != 0x16 || report.NewTrigger != 0x16, compactPayload: compact);
    }
    private object ReadNeedsState()
    {
        var engine = Robot.Engine;
        var status = engine.GetNeedsStatus();
        var state = engine.Needs.State;
        // The selected activity is visible before the get-in sets the expression and
        // can outlast SevereNeedsComponent's auto-clear (0x00572BEA..0x00572C3C).
        var runtime = engine.Robot?.NativeBehaviorRuntime;
        NeedId? expressed = (runtime?.AI.SevereNeeds ?? engine.Needs.SevereNeeds).SelectedNeed;
        NeedId? selected = runtime?.SelectedNeedsActivity ?? expressed;
        return new
        {
            status.KeepFull, status.Loaded, status.Applied, status.Paused, status.Repair, status.Energy, status.Play,
            brackets = status.Loaded ? new
            {
                repair = state.GetNeedBracket(NeedId.Repair).ToString(),
                energy = state.GetNeedBracket(NeedId.Energy).ToString(),
                play = state.GetNeedBracket(NeedId.Play).ToString(),
            } : null,
            selectedSevereNeed = status.Loaded ? selected?.ToString() : null,
            expressedSevereNeed = status.Loaded ? expressed?.ToString() : null,
        };
    }

    private void OnNeedsState(NativeNeedsStateReport report)
    {
        // NeedsState 0x62 writes repair, energy and play in that order (0x0069383C..0x006939D2).
        Add("needs-changed", new
        {
            repair = NeedsLevel(report.Levels[(int)NeedId.Repair]),
            energy = NeedsLevel(report.Levels[(int)NeedId.Energy]),
            play = NeedsLevel(report.Levels[(int)NeedId.Play]),
            brackets = new
            {
                repair = ((NeedBracketId)report.Brackets[(int)NeedId.Repair]).ToString(),
                energy = ((NeedBracketId)report.Brackets[(int)NeedId.Energy]).ToString(),
                play = ((NeedBracketId)report.Brackets[(int)NeedId.Play]).ToString(),
            },
            action = ((NeedsActionId)report.Action).ToString(),
        });

        static float? NeedsLevel(uint bits)
        {
            float level = BitConverter.UInt32BitsToSingle(bits);
            return float.IsFinite(level) ? level : null;
        }
    }

    private void OnFreeplaySparksAwarded(FreeplaySparksAwardedReport report) => Add("freeplay-sparks-awarded", report);
    private void OnGoingToSleep(GoingToSleepReport report) => Add("going-to-sleep", report);
    private void OnChargerSleepCancelled(ChargerSleepCancelledReport report) => Add("sleep-cancelled", report);
    private void OnRobotDisconnected(RobotDisconnectedMessage report)
    {
        lock (eventGate)
        {
            locatedFaces.Clear();
            locatedObjects.Clear();
            recognisedAt.Clear();
            faceApproaches.Clear();
            batteryLow = false;
            onCharger = false;
            Add("robot-disconnected", new { report.TimeSinceLastMsgSec, reason = report.Reason.ToString(), reasonCode = (byte)report.Reason });
        }
    }
    private void OnActionCompleted(ActionCompletionReport report)
    {
        var resultName = Enum.GetName((NativeActionResultName)report.Result);
        bool own = ReferenceEquals(report.HostCaller, eventCaller);
        // Operator-approved MCP presentation rule, not an engine result change.
        // Keep failed own completions, and all foreign completions, in channel push.
        // Full and compact read views retain their existing significance and payloads.
        bool push = !own || report.Result != EngineActionResult.Success;
        if (report.ActionType == 0x23 && report.CompletionVariant == 3)
        {
            // TrackFace returns admission. Its signed terminal cache remains in read_events.
            Add("action-completed", new { completion = report, resultName, faceId = unchecked((int)report.NativeCompletionCache) },
                eligible: own, compactEligible: true, pushEligible: push);
            return;
        }
        Add("action-completed", new { completion = report, resultName },
            eligible: own, compactEligible: true, pushEligible: push);
    }
    private void Add<T>(string type, T payload, uint? robotTimestamp = null, bool eligible = true,
        bool? compactEligible = null, bool? pushEligible = null)
    {
        var json = JsonSerializer.SerializeToElement(payload, Json);
        lock (eventGate) Append(type, robotTimestamp, json, eligible, compactEligible: compactEligible, pushEligible: pushEligible);
    }

    private void Append(string type, uint? robotTimestamp, JsonElement payload, bool eligible = true,
        JsonElement? compactPayload = null, bool? compactEligible = null, bool? pushEligible = null)
    {
        if (disposed) return;
        if (events.Count == eventCapacity) events.Dequeue();
        var now = timeProvider.GetUtcNow();
        bool significantType = significantTypes.Contains(type);
        var full = new BridgeEvent(++lastCursor, type, now, robotTimestamp, payload, eligible && significantType);
        events.Enqueue(new(full, compactPayload,
            (compactEligible ?? eligible) && significantType,
            (pushEligible ?? compactEligible ?? eligible) && significantType));
        var signal = eventAvailable;
        eventAvailable = NewEventSignal();
        signal.TrySetResult();
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed), this);

    public void Dispose()
    {
        lock (eventGate)
        {
            if (disposed) return;
            Volatile.Write(ref disposed, true);
            events.Clear();
            faceMutations.Clear();
            faceMutationOwner = null;
            eventAvailable.TrySetResult();
        }

        Disposing?.Invoke();
        DetachSenses();
        var engine = Robot.Engine;
        engine.RobotObservedFace -= OnFace;
        engine.RobotChangedObservedFaceId -= OnFaceIdChanged;
        engine.RobotDeletedFace -= OnFaceLost;
        faceRenameRecipient.Dispose();
        faceEraseRecipient.Dispose();
        engine.RobotObservedObject -= OnObject;
        Robot.Cubes.MovementBroadcast -= OnCubeMoved;
        Robot.Cubes.TapBroadcast -= OnCubeTapped;
        engine.ObjectPoseUnknown -= OnObjectPoseLost;
        engine.BehaviorTransition -= OnBehavior;
        engine.BehaviorRequestBlocked -= OnBehaviorRequestBlocked;
        engine.ReactionTransition -= OnReaction;
        engine.NeedsStateReported -= OnNeedsState;
        engine.Needs.FreeplaySparksAwarded -= OnFreeplaySparksAwarded;
        engine.GoingToSleep -= OnGoingToSleep;
        engine.ChargerSleepCancelled -= OnChargerSleepCancelled;
        engine.RobotDisconnected -= OnRobotDisconnected;
        engine.ActionCompleted -= OnActionCompleted;
    }
}
