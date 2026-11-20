using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;

namespace Cozmo.Bridge;

public sealed partial class CozmoBridge
{
    // Read-only host reporting. Admission is serialized with the engine so a queued
    // command owns a runner reference, rather than a reusable external action tag.
    private sealed class ActionWhyCapture : IDisposable
    {
        private readonly CozmoBridge bridge;
        private CozmoRobot Robot => bridge.Robot;
        private readonly object gate = new();
        private EngineRobot? owner;
        private IActionRunner? root;
        private (ReactionTrigger Trigger, string? Behavior)? blockedBy;
        private HashSet<IActionRunner> previous = [];
        private NativeDriveToMountChargerRunner? mount;
        private string? poseCheck;
        private bool poseHintCurrent;
        private object? docking;
        private readonly List<object> path = [];
        private bool disposed;

        internal ActionWhyCapture(CozmoBridge bridge)
        {
            this.bridge = bridge;
            lock (bridge.eventGate)
            {
                bridge.ThrowIfDisposed();
                bridge.Disposing += Dispose;
                Robot.Engine.LogEntry += OnLog;
                Robot.MessageInternal += OnMessage;
            }
        }

        private IEnumerable<IActionRunner> Runners()
        {
            if (owner is null || owner.Lifetime.Destroying || !ReferenceEquals(Robot.Engine.Robot, owner) ||
                owner.ActionList.QueueAt(0) is not { } queue) yield break;
            if (queue.Current is { } current) yield return current;
            foreach (var runner in queue.Pending) yield return runner;
        }

        internal void BeforeAdmission()
        {
            owner = Robot.Engine.Robot;
            blockedBy = ReactionBlockingAdmission();
            previous = new HashSet<IActionRunner>(Runners(), ReferenceEqualityComparer.Instance);
        }

        internal void AfterAdmission()
        {
            // BeginAiCommand can first queue the app activity handoff. All action
            // APIs append their requested runner after that synchronous handoff.
            root = Runners().LastOrDefault(r => !previous.Contains(r));
            mount = root as NativeDriveToMountChargerRunner;
            blockedBy ??= ReactionBlockingAdmission();
            previous.Clear();
        }

        private (ReactionTrigger Trigger, string? Behavior)? ReactionBlockingAdmission()
        {
            // The engine sets this gate at the reaction transition (0x005A2106..0x005A2110).
            // Capture it under the same engine owner as this admission attempt.
            if (owner is null || owner.Lifetime.Destroying || !ReferenceEquals(Robot.Engine.Robot, owner) ||
                !owner.ActionList.ExternalActionsDisabled ||
                owner.BehaviorManagerOwner is not { CurrentReactionTrigger: { } trigger } manager)
                return null;
            return (trigger, manager.Current?.Id);
        }

        // These callbacks are synchronous native action/path logs and ingress, on
        // the engine owner. Do not acquire the engine owner from a planner worker.
        private bool Active() => !disposed && root is not null && owner is not null &&
            ReferenceEquals(Robot.Engine.Robot, owner) && !owner.Lifetime.Destroying &&
            ReferenceEquals(owner.ActionList.QueueAt(0)?.Current, root);

        private void OnLog(EngineLogEntry entry)
        {
            if (entry.Name != "CozmoStack.DriveToObject.PoseCheck" && entry.Name != "PathComponent.TransitionStatus" &&
                entry.Name is not ("CompoundActionSequential.Update.NextAction" or "CompoundActionSequential.Update.Retrying" or
                    "CompoundActionParallel.Update.Retrying" or "CompoundActionSequential.UpdateInternal") &&
                !(entry.Name?.StartsWith("PathComponent.Update.Planner.", StringComparison.Ordinal) ?? false)) return;
            lock (gate)
            {
                if (!Active()) return;
                if (entry.Name == "CozmoStack.DriveToObject.PoseCheck")
                {
                    poseCheck = entry.Text;
                    poseHintCurrent = true;
                }
                else if (entry.Name?.StartsWith("CompoundAction", StringComparison.Ordinal) == true)
                {
                    // An ignored/retried check is historical evidence, not a
                    // correction for the final action's later failure.
                    if (entry.Name != "CompoundActionSequential.UpdateInternal" ||
                        entry.Text.StartsWith("Ignoring failure for ", StringComparison.Ordinal))
                        poseHintCurrent = false;
                }
                else
                {
                    // The transition's text includes both states, before SetStatus
                    // writes the destination (NativePathComponent.SetStatus).
                    if (path.Count == 32) path.RemoveAt(0);
                    path.Add(new { source = entry.Name, text = entry.Text,
                        plannerStatus = Robot.Motion.Path.PlannerStatus,
                        driveStatusAtObservation = Robot.Motion.Path.DriveToPoseStatus });
                }
            }
        }

        private void OnMessage(RobotMessage message)
        {
            if (message is not PickAndPlaceResult result) return;
            lock (gate)
            {
                if (!Active()) return;
                // Unpack 0x007C1AC6, native handler 0x00533780. Preserve the signed
                // error byte; no guessed firmware error-name or recovery advice.
                docking = new { timestamp = result.Field0, success = result.Field1,
                    dockingResult = result.Field2, blockStatus = result.Field3,
                    correlation = "Untagged firmware report observed while this action was current; not proof of its failure cause" };
            }
        }

        internal JsonNode Describe(CommandResult result, bool truncated)
        {
            using var read = Robot.Engine.EnterSynchronousOwner();
            lock (gate)
            {
                var snapshot = new JsonObject();
                string hint = result.Detail ?? "The engine did not provide a more specific failure check.";
                if (poseCheck is { } text)
                {
                    var pose = ParsePose(text);
                    pose["superseded"] = !poseHintCurrent;
                    snapshot["poseCheck"] = pose;
                    if (poseHintCurrent && result.EngineResult == 0x04000001 && pose is not null)
                        hint = PoseHint(pose) ?? hint;
                }
                // Mount retains the exact pitch used by BackupCheckIfDone even
                // after retirement. Never substitute a newer sensor reading.
                string? backup = mount?.Mount.Trace.LastOrDefault(t => t.StartsWith("BackupOntoChargerAction: pitched to ", StringComparison.Ordinal));
                if (backup is not null)
                {
                    var match = Regex.Match(backup, @"pitched to (.+) rad, below (.+); 0x0400000A");
                    snapshot["backup"] = new JsonObject { ["source"] = "BackupOntoChargerAction.CheckIfDone 0x0054E7A8", ["raw"] = backup };
                    if (match.Success && TryFloat(match.Groups[1].Value, out var pitch) && TryFloat(match.Groups[2].Value, out var cutoff))
                    {
                        snapshot["backup"]!["pitchRad"] = pitch;
                        snapshot["backup"]!["cutoffRad"] = cutoff;
                        if (result.EngineResult is 0x0400000A or 0x04000006)
                            hint = FormattableString.Invariant($"Backup pitch was {pitch * 180 / Math.PI:F1} degrees, below the {cutoff * 180 / Math.PI:F1} degree cutoff; the check requires pitch at or above that cutoff.");
                    }
                }
                var drive = mount?.Drive.Drive ?? root as DriveToPoseAction;
                if (result.EngineResult == (uint)ActionResult.FailedTraversingPath && drive?.LastFailedArrival is { } arrival &&
                    double.IsFinite(arrival.ToleranceMm.X) && double.IsFinite(arrival.ToleranceMm.Y) &&
                    double.IsFinite(arrival.ToleranceMm.Z) && float.IsFinite(arrival.RotationLimitRad) &&
                    double.IsFinite(arrival.GoalMm.X + arrival.GoalMm.Y + arrival.GoalMm.Z + arrival.ActualMm.X +
                        arrival.ActualMm.Y + arrival.ActualMm.Z + arrival.OffsetMm.X + arrival.OffsetMm.Y + arrival.OffsetMm.Z))
                {
                    snapshot["pathArrival"] = JsonSerializer.SerializeToNode(arrival, Json);
                    hint = arrival.RotationRad is { } angle && angle > arrival.RotationLimitRad
                        ? FormattableString.Invariant($"DriveToPose completed path {arrival.PathId}, then rejected the arrival rotation ({angle * 180 / Math.PI:F1} degrees against {arrival.RotationLimitRad * 180 / Math.PI:F1} degrees).")
                        : FormattableString.Invariant($"DriveToPose rejected the arrival pose after path {arrival.PathId} completed.");
                }
                if (docking is not null) snapshot["docking"] = JsonSerializer.SerializeToNode(docking, Json);
                if (path.Count != 0) snapshot["pathPlanner"] = JsonSerializer.SerializeToNode(path, Json);
                if (blockedBy is { } reaction && IsReactionRefusal(result))
                {
                    snapshot["reaction"] = new JsonObject
                    {
                        ["trigger"] = reaction.Trigger.ToString(),
                        ["behavior"] = reaction.Behavior,
                        ["source"] = "ActionQueue.QueueAction.ExternalActionsDisabled 0x0053D94C..0x0053D9A2",
                    };
                    hint = reaction is { Trigger: ReactionTrigger.PlacedOnCharger, Behavior: "ReactToOnCharger" }
                        ? "ReactToOnCharger is holding action admission. Pick Cozmo up and set him back on his treads, then retry once the reaction clears. Sliding him off the charger leaves the reaction active."
                        : $"The {reaction.Trigger} reaction is holding action admission. Retry after it clears.";
                }
                if (truncated) hint = "Animation was cut off by robot buffer; see playbackIssues for the captured evidence.";
                return new JsonObject
                {
                    ["engineResult"] = result.EngineResult,
                    ["engineResultName"] = result.EngineResultName,
                    ["snapshot"] = snapshot,
                    ["hint"] = hint,
                    ["checkAvailable"] = snapshot.Count != 0,
                };
            }
        }

        private static bool TryFloat(string text, out double value) =>
            (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) ||
             double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) && double.IsFinite(value);

        private static JsonObject ParsePose(string text)
        {
            var pose = new JsonObject { ["source"] = "CozmoStack.DriveToObject.PoseCheck", ["raw"] = text };
            // This diagnostic is invariant, round-trip float text from the actual
            // final comparison (DriveActions.cs; native 0x00559AA0 / 0x00550FD2).
            var match = Regex.Match(text, @"offsetMm=\(([^,]+),([^\)]+)\) thresholdMm=\(([^,]+),([^\)]+)\) yawRad=(\S+) yawLimitRad=(\S+) inPosition=(\S+)");
            if (!match.Success) return pose;
            string[] names = ["offsetXmm", "offsetYmm", "thresholdXmm", "thresholdYmm", "yawRad", "yawLimitRad"];
            for (int i = 0; i < names.Length; i++)
                if (double.TryParse(match.Groups[i + 1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value))
                    pose[names[i]] = value;
            pose["positionFrame"] = "Robot main pose parent axes (not charger-relative)";
            pose["yawTested"] = match.Groups[5].Value != "not-tested";
            if (bool.TryParse(match.Groups[7].Value, out bool inside)) pose["inPosition"] = inside;
            return pose;
        }

        private static string? PoseHint(JsonObject pose)
        {
            foreach (var axis in new[] { "Y", "X" })
                if (pose[$"offset{axis}mm"] is { } offset && pose[$"threshold{axis}mm"] is { } threshold &&
                    threshold.GetValue<double>() > 0 &&
                    Math.Abs(offset.GetValue<double>()) - threshold.GetValue<double>() is var excess && excess > 0)
                    return FormattableString.Invariant($"Robot is {excess:F2} mm outside the approach box on its {axis} axis; reach the pre-dock pose within the recorded thresholds.");
            if (pose["yawRad"] is { } yaw && pose["yawLimitRad"] is { } limit && Math.Abs(yaw.GetValue<double>()) >= limit.GetValue<double>())
                return FormattableString.Invariant($"Relative yaw is {yaw.GetValue<double>() * 180 / Math.PI:F1} degrees; align to less than {limit.GetValue<double>() * 180 / Math.PI:F1} degrees of the approach heading.");
            return null;
        }

        public void Dispose()
        {
            lock (gate)
            {
                if (disposed) return;
                disposed = true;
                Robot.Engine.LogEntry -= OnLog;
                Robot.MessageInternal -= OnMessage;
            }
            lock (bridge.eventGate) bridge.Disposing -= Dispose;
        }
    }
}
