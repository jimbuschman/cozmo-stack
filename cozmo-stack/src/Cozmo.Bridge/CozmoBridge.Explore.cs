using System.Collections.Concurrent;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Vision;

namespace Cozmo.Bridge;

public sealed partial class CozmoBridge
{
    private int exploring;

    // Our C2 policy chooses 90 mm legs. Prefer an unknown endpoint, then a
    // clear endpoint not yet visited. Native collision rays 0x0068176E include
    // the anchored C2 hazard overlay; the actual drive still runs DriveToPose.
    internal static RoomPose? ChooseExploreLeg(MemoryMap map, Vec2 start, double heading, List<Vec2> visited)
    {
        const double length = 90;
        var cells = map.Tree.CaptureCells(start.X, start.Y, length).Cells;
        RoomPose? clear = null;
        foreach (int degrees in new[] { 0, 45, -45, 90, -90, 135, -135, 180 })
        {
            double direction = heading + degrees * Math.PI / 180;
            var forward = new Vec2(Math.Cos(direction), Math.Sin(direction));
            var left = new Vec2(-forward.Y, forward.X);
            var end = start + forward * length;
            if (visited.Any(v => (v - end).Length < length / 2)) continue;
            // Check the center and both sides of the intended corridor. The
            // native planner independently checks footprint and obstacle padding.
            if (new[] { 0, -30, 30 }.Any(side =>
                map.HasCollisionRayWithTypes(start + left * side, end + left * side))) continue;
            var goal = new RoomPose(end.X, end.Y, Math.Atan2(forward.Y, forward.X));
            if (!cells.Any(c => c.Type != MemoryMapContentType.Unknown && c.Bounds.MinX <= end.X &&
                c.Bounds.MaxX >= end.X && c.Bounds.MinY <= end.Y && c.Bounds.MaxY >= end.Y)) return goal;
            clear ??= goal;
        }
        return clear;
    }

    private async Task<CommandResult> Explore(int maxSeconds, CancellationToken token)
    {
        var engine = Robot.Engine;
        if (!engine.Options.C2.MapExplore) return new("not-admitted", false, null, null, "C2:map-explore disabled");
        void Log(string detail) => engine.EmitStackLog($"{CozmoC2Settings.MapExploreName}: {detail}");
        CommandResult Refuse(string reason)
        {
            Log(reason);
            return new("not-admitted", false, null, null, reason);
        }
        var owner = engine.Robot;
        if (owner?.NativeBehaviorRuntime is not { IsRetired: false } runtime || !engine.IsReadyDelivered)
            return CommandResult.NotReady();
        if (!engine.Options.C2.ObstacleMemory) return Refuse("remembered hazard overlay is disabled");
        if (!runtime.Bound.TryGetValue("Hiking_LookInPlace360", out var behavior) ||
            behavior is not NativeSparksLookInPlace scan) return Refuse("native look-around unavailable");
        var initialHazards = engine.RoomMap.Hazards(owner);
        var chargers = new ConcurrentDictionary<uint, byte>();
        var cubes = new ConcurrentDictionary<uint, byte>();
        var faces = new ConcurrentDictionary<int, byte>();
        var visited = new List<Vec2>();
        int legs = 0;
        uint origin = owner.CurrentOriginId;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(maxSeconds));
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(token, deadline.Token);
        string? interruption = null;
        void Halt(string reason)
        {
            if (Interlocked.CompareExchange(ref interruption, reason, null) == null) stop.Cancel();
        }
        void Cliff(CliffReport report) { if (report.Sensors != CliffSensors.None) Halt("cliff detected"); }
        void Treads(OffTreadsState from, OffTreadsState to) { if (to != OffTreadsState.OnTreads) Halt("picked up or off treads"); }
        void ObjectSeen(RobotObservedObjectReport observation)
        {
            if (observation.Type == (uint)ObjectType.Charger_Basic) chargers.TryAdd(observation.ObjectId, 0);
            if (observation.Family == (uint)ObjectFamily.LightCube) cubes.TryAdd(observation.ObjectId, 0);
        }
        void FaceSeen(RobotObservedFaceReport observation) => faces.TryAdd(observation.FaceId, 0);
        string? Block()
        {
            if (!ReferenceEquals(engine.Robot, owner) || owner.Lifetime.Destroying || runtime.IsRetired ||
                owner.CurrentOriginId != origin) return "robot or map origin changed";
            if (Robot.Sensors.OffTreadsState != OffTreadsState.OnTreads) return "picked up or off treads";
            if (Robot.Sensors.CliffDetectedByEvent || Robot.Sensors.CliffDetectedNow) return "cliff detected";
            if (!Robot.Sensors.CliffSensorEnabled || owner.BehaviorManagerOwner?.IsReactionTriggerEnabled(ReactionTrigger.CliffDetected) != true)
                return "cliff protection is disabled";
            if (Robot.Sensors.OnChargerPlatform || Robot.Sensors.OnChargerContacts) return "on charger";
            return null;
        }
        CommandResult Finish(string reason, bool success)
        {
            Log(reason);
            var newHazards = engine.RoomMap.Hazards(owner).Where(h => !initialHazards.Any(old =>
                old.Origin == h.Origin && old.Type == h.Type && old.ObjectId == h.ObjectId &&
                old.LocalPolygon.SequenceEqual(h.LocalPolygon))).ToArray();
            var report = new { reason, completedLegs = legs, chargerIds = chargers.Keys.Order().ToArray(),
                cubeIds = cubes.Keys.Order().ToArray(), faceIds = faces.Keys.Order().ToArray(), newHazards };
            return success ? CommandResult.Confirmed(report) : new("interrupted", false, null, null, reason, Report: report);
        }
        if (Interlocked.CompareExchange(ref exploring, 1, 0) != 0) return Refuse("explore already running");
        Robot.Sensors.CliffDetectedInternal += Cliff;
        Robot.Sensors.OffTreadsStateChangedInternal += Treads;
        engine.RobotObservedObject += ObjectSeen;
        engine.RobotObservedFace += FaceSeen;
        try
        {
            Log($"starting exploration with {maxSeconds} second limit");
            while (legs < 3)
            {
                Task<string?> pendingScan;
                using (engine.EnterSynchronousOwner())
                {
                    stop.Token.ThrowIfCancellationRequested();
                    if (Block() is { } blocked) return Finish(blocked, false);
                    // Hiking_LookInPlace360 uses the engine's scan core. One
                    // completed S7 iteration is recorded at 0x005E3674..0x005E3724.
                    uint iterations = scan.SearchIterations;
                    Log("looking around in place");
                    pendingScan = runtime.ScanForC2Goal("Hiking_LookInPlace360", () => scan.SearchIterations != iterations, stop.Token);
                }
                string? failure = await pendingScan.ConfigureAwait(false);
                stop.Token.ThrowIfCancellationRequested();
                if (failure is not null) return Finish(failure, false);
                Task<AiDriveActionResult> drive;
                using (engine.EnterSynchronousOwner())
                {
                    stop.Token.ThrowIfCancellationRequested();
                    if (Block() is { } blocked) return Finish(blocked, false);
                    using var mutation = Robot.LocalizationWorld.EnterPoseMutation();
                    if (ReadWorldPose() is not { } pose || Robot.LocalizationWorld.Maps.Current is not { } map)
                        return Finish("current map unavailable", false);
                    var start = new Vec2(pose.XMm, pose.YMm);
                    visited.Add(start);
                    if (ChooseExploreLeg(map, start, pose.HeadingRad, visited) is not { } goal)
                        return Finish("no clear unexplored leg", true);
                    visited.Add(new(goal.XMm, goal.YMm));
                    Log(FormattableString.Invariant($"driving leg {legs + 1} to ({goal.XMm:F0}, {goal.YMm:F0}) mm"));
                    drive = Robot.Motion.QueueAiGotoPoseAsync((float)goal.XMm, (float)goal.YMm, (float)goal.HeadingRad, stop.Token);
                }
                var result = await drive.ConfigureAwait(false);
                stop.Token.ThrowIfCancellationRequested();
                if (!result.Admitted || result.OwnerRetired || result.EngineResult != 0)
                    return Finish($"native drive failed: {result.Detail}, result {result.EngineResult}", false);
                legs++;
            }
            return Finish("three legs completed", true);
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
            string reason = interruption ?? (token.IsCancellationRequested ? "cancelled" : "time limit reached");
            return Finish(reason, reason == "time limit reached");
        }
        finally
        {
            using (engine.EnterSynchronousOwner())
            {
                Robot.Sensors.CliffDetectedInternal -= Cliff;
                Robot.Sensors.OffTreadsStateChangedInternal -= Treads;
            }
            engine.RobotObservedObject -= ObjectSeen;
            engine.RobotObservedFace -= FaceSeen;
            Volatile.Write(ref exploring, 0);
        }
    }
}
