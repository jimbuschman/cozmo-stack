using System.Text.Json;
using Cozmo.Robot;

namespace Cozmo.Bridge;

public sealed partial class CozmoBridge
{
    private static readonly TimeSpan FaceApproachGap = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan SequentialFaceGap = TimeSpan.FromMilliseconds(300);
    private const double FaceApproachRadiusMm = 200;
    private readonly Dictionary<int, FaceTrack> faceApproaches = [];
    private sealed record FaceTrack(FaceApproach Approach, DateTimeOffset LastSeen);

    private sealed class FaceApproach
    {
        internal DateTimeOffset LastSeen;
        internal EngineGamePose Pose;
        internal string? RecognisedName;
        internal int? RecognisedId;
    }

    private JsonElement PresentFace(RobotObservedFaceReport report)
    {
        double? distanceMm = null, bearingDeg = null;
        // Both poses use the engine's world origin. A stale origin has no valid bearing.
        if (Robot.Engine.Robot?.LastPublishedState is { } state && state.Pose.OriginId == report.Pose.OriginId)
        {
            double dx = (double)report.Pose.X - state.Pose.X;
            double dy = (double)report.Pose.Y - state.Pose.Y;
            if (double.IsFinite(dx) && double.IsFinite(dy) && float.IsFinite(state.PoseAngleRad))
            {
                distanceMm = Math.Round(Math.Sqrt(dx * dx + dy * dy));
                double relative = Math.Atan2(dy, dx) - state.PoseAngleRad;
                bearingDeg = Math.Round(Math.Atan2(Math.Sin(relative), Math.Cos(relative)) * 180 / Math.PI, 1);
            }
        }
        return JsonSerializer.SerializeToElement(new
        {
            report.FaceId,
            name = string.IsNullOrEmpty(report.Name) ? null : report.Name,
            distanceMm,
            bearingDeg,
            recognised = !string.IsNullOrEmpty(report.Name),
        }, Json);
    }

    // Called under eventGate. The fold affects presentation only. Use a nearby,
    // sequential track only when there is a single plausible prior approach.
    private (bool NewApproach, bool NewRecognition) TrackFaceApproach(RobotObservedFaceReport report)
    {
        var now = timeProvider.GetUtcNow();
        foreach (int id in faceApproaches.Where(p => now - p.Value.LastSeen >= FaceApproachGap).Select(p => p.Key).ToArray())
            faceApproaches.Remove(id);

        var approach = faceApproaches.GetValueOrDefault(report.FaceId)?.Approach;
        if (approach is null && report.FaceId < 0)
        {
            var nearby = faceApproaches.Values.Select(track => track.Approach).Distinct().Where(candidate =>
                now - candidate.LastSeen >= SequentialFaceGap && Nearby(candidate.Pose, report.Pose)).Take(2).ToArray();
            if (nearby.Length == 1) approach = nearby[0];
        }

        bool newApproach = approach is null;
        approach ??= new();
        bool newRecognition = !string.IsNullOrEmpty(report.Name) &&
            (approach.RecognisedName != report.Name || approach.RecognisedId != report.FaceId);
        if (newRecognition)
        {
            approach.RecognisedName = report.Name;
            approach.RecognisedId = report.FaceId;
        }
        approach.LastSeen = now;
        approach.Pose = report.Pose;
        faceApproaches[report.FaceId] = new(approach, now);
        return (newApproach, newRecognition);
    }

    private static bool Nearby(EngineGamePose first, EngineGamePose second)
    {
        double dx = (double)first.X - second.X, dy = (double)first.Y - second.Y, dz = (double)first.Z - second.Z;
        return first.OriginId == second.OriginId &&
            dx * dx + dy * dy + dz * dz <= FaceApproachRadiusMm * FaceApproachRadiusMm;
    }
}
