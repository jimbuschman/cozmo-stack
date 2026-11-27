using System.Text.Json;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Vision;
using StbImageWriteSharp;

namespace Cozmo.Bridge;

public sealed partial class CozmoBridge
{
    private sealed record WorldPose(float XMm, float YMm, float ZMm, float HeadingRad, uint OriginId,
        uint? LocalizedToObjectId, uint PoseFrameId, uint? RobotTimestamp, double? AgeMs);
    internal sealed record MapReply(JsonElement Metadata, byte[]? Png);
    private sealed record MapObject(uint WorldObjectId, string Type, double XMm, double YMm, bool Charger, ChargerEstimate? ChargerEstimate,
        C2ChargerEstimate? FusedCharger = null, bool MemoryOnly = false);

    // Caller holds the engine owner and world pose gates. Use the same current pose
    // supplier as Robot::GetRobotState 0x005180E2..0x00518160, including world yaw.
    private WorldPose? ReadWorldPose()
    {
        if (Robot.Engine.ConnectionState != 2 || Robot.Engine.Robot is not { FirstFullStateHandled: true } owner ||
            owner.Lifetime.Destroying || owner.CurrentOriginId == 0) return null;

        var pose = Robot.Sensors.PublicationPose();
        float heading = CozmoEngine.PublicationAngle(pose);
        if (!float.IsFinite(pose.X) || !float.IsFinite(pose.Y) || !float.IsFinite(pose.Z) || !float.IsFinite(heading))
            return null;

        var odometry = owner.LastOdometryPose;
        bool currentSample = odometry is { } sample && sample.Origin == pose.OriginId && sample.Frame == owner.PoseFrameId;
        return new(pose.X, pose.Y, pose.Z, heading, pose.OriginId, Robot.LocalizationWorld.LocalizedToObjectId,
            owner.PoseFrameId, currentSample ? odometry!.Value.Timestamp : null,
            currentSample ? Math.Max(0, Robot.Engine.Timer.Nanoseconds - odometry!.Value.ReceivedNs) / 1e6 : null);
    }

    internal MapReply GetMap(double radiusMm, int limit, bool render)
    {
        ThrowIfDisposed();
        if (!double.IsFinite(radiusMm) || radiusMm is < 50 or > 2000)
            throw new ArgumentOutOfRangeException(nameof(radiusMm), "Map radius must be between 50 and 2000 mm.");
        if (limit is < 1 or > 128)
            throw new ArgumentOutOfRangeException(nameof(limit), "Map region limit must be between 1 and 128.");

        WorldPose? pose;
        SourceQuadBounds root;
        MemoryMapCell[] cells;
        MappedHazard[]? rememberedHazards;
        RoomLayer[]? roomMaps;
        C2ChargerEstimate? chargerMemory;
        var objects = new List<MapObject>();
        using (Robot.Engine.EnterSynchronousOwner())
        {
            var world = Robot.LocalizationWorld;
            using var poseRead = world.EnterPoseMutation();
            pose = ReadWorldPose();
            rememberedHazards = Robot.Engine.Options.C2.ObstacleMemory ? Robot.Engine.RoomMap.Hazards(Robot.Engine.Robot) : null;
            roomMaps = Robot.Engine.Options.C2.MapReanchor ? Robot.Engine.RoomMap.Snapshot(pose is null ? null : Robot.Engine.Robot) : null;
            chargerMemory = Robot.Engine.Options.C2.ChargerMemory ? Robot.Engine.C2ChargerMemory.Get(pose is null ? null : Robot.Engine.Robot) : null;
            // MapComponent::GetCurrentMemoryMapHelper 0x0067EA5C..0x0067EA88.
            if (pose is null || world.Maps.CurrentOrigin != pose.OriginId || world.Maps.Current is not { } map)
                return new(JsonSerializer.SerializeToElement(new { available = false, pose, chargerMemory, roomMaps, rememberedHazards, regions = Array.Empty<object>() }, Json), null);

            (root, cells) = map.Tree.CaptureCells(pose.XMm, pose.YMm, radiusMm);
            if (Robot.Engine.Robot!.TryGetNativeOriginOwner(pose.OriginId, out var origin) && origin is not null)
            {
                using var relative = new Pose3d();
                foreach (var item in world.LocatedObjects)
                {
                    if (item.OriginId != pose.OriginId || item.Family == ObjectFamily.MarkerlessObject ||
                        !Pose3d.GetWithRespectTo(item.NativePose, origin, relative)) continue;
                    bool isCharger = item.Type == ObjectType.Charger_Basic;
                    var fused = isCharger && chargerMemory is { Anchored:true } ? chargerMemory : null;
                    var point = fused is not null ? new Vec3(fused.XMm,fused.YMm,0) : relative.Translation;
                    if (Math.Abs(point.X - pose.XMm) <= radiusMm && Math.Abs(point.Y - pose.YMm) <= radiusMm)
                        objects.Add(new(item.ObjectId, item.Type.ToString(), point.X, point.Y, isCharger,
                            isCharger ? ReadChargerEstimate(item, relative, Robot.Engine.Robot!) : null,fused));
                }
            }
            // An anchored, forgotten charger still belongs on the map. Mark it
            // as memory only; its historical ID is not a native command target.
            if (chargerMemory is { Anchored:true } remembered && !objects.Any(o=>o.Charger) &&
                Math.Abs(remembered.XMm-pose.XMm)<=radiusMm && Math.Abs(remembered.YMm-pose.YMm)<=radiusMm)
                objects.Add(new(remembered.WorldObjectId,ObjectType.Charger_Basic.ToString(),remembered.XMm,remembered.YMm,
                    true,null,remembered,MemoryOnly:true));
            if (roomMaps is not null)
            {
                foreach (var layer in roomMaps.Where(layer => layer.Anchored).OrderByDescending(layer => layer.Origin.Id == pose.OriginId))
                foreach (var item in layer.Landmarks)
                {
                    bool charger = item.Type == ObjectType.Charger_Basic;
                    if (objects.Any(o => charger ? o.Charger : o.WorldObjectId == item.WorldObjectId && o.Type == item.Type.ToString())) continue;
                    var point = layer.ToCurrent!.Value.Apply(item.Pose);
                    if (Math.Abs(point.XMm - pose.XMm) <= radiusMm && Math.Abs(point.YMm - pose.YMm) <= radiusMm)
                        objects.Add(new(item.WorldObjectId, item.Type.ToString(), point.XMm, point.YMm, charger, null, MemoryOnly:true));
                }
            }
        }

        var selected = cells.OrderBy(cell => DistanceSquared(cell.Bounds, pose)).Take(limit).Select(cell => new
        {
            type = cell.Type.ToString(),
            worldBoundsMm = Bounds(cell.Bounds),
            robotBoundsMm = RelativeBounds(cell.Bounds, pose),
        }).ToArray();
        var viewport = new[] { pose.XMm - radiusMm, pose.YMm - radiusMm, pose.XMm + radiusMm, pose.YMm + radiusMm };
        var metadata = JsonSerializer.SerializeToElement(new
        {
            available = true,
            pose,
            radiusMm,
            rootBoundsMm = Bounds(root),
            viewportWorldBoundsMm = viewport,
            regionCount = cells.Length,
            truncated = cells.Length > selected.Length,
            regions = selected,
            chargerMemory,
            roomMaps,
            rememberedHazards,
            objects,
        }, Json);
        return new(metadata, render ? RenderMap(pose, radiusMm, cells, objects) : null);
    }

    private static float[] Bounds(SourceQuadBounds b) => [b.MinX, b.MinY, b.MaxX, b.MaxY];

    private static double DistanceSquared(SourceQuadBounds bounds, WorldPose pose)
    {
        double dx = pose.XMm - Math.Clamp(pose.XMm, bounds.MinX, bounds.MaxX);
        double dy = pose.YMm - Math.Clamp(pose.YMm, bounds.MinY, bounds.MaxY);
        return dx * dx + dy * dy;
    }

    private static double[] RelativeBounds(SourceQuadBounds b, WorldPose pose)
    {
        var (sin, cos) = Math.SinCos(pose.HeadingRad);
        double x = (b.MinX + (double)b.MaxX) / 2 - pose.XMm;
        double y = (b.MinY + (double)b.MaxY) / 2 - pose.YMm;
        double halfX = (b.MaxX - (double)b.MinX) / 2, halfY = (b.MaxY - (double)b.MinY) / 2;
        double centerX = x * cos + y * sin, centerY = -x * sin + y * cos;
        double extentX = halfX * Math.Abs(cos) + halfY * Math.Abs(sin);
        double extentY = halfX * Math.Abs(sin) + halfY * Math.Abs(cos);
        return [Math.Round(centerX - extentX, 2), Math.Round(centerY - extentY, 2),
            Math.Round(centerX + extentX, 2), Math.Round(centerY + extentY, 2)];
    }

    private static byte[] RenderMap(WorldPose pose, double radiusMm, MemoryMapCell[] cells, List<MapObject> objects)
    {
        const int size = 192;
        var rgb = new byte[size * size * 3];
        Array.Fill(rgb, (byte)29);
        double scale = size / (radiusMm * 2);
        int X(double x) => (int)Math.Floor((x - pose.XMm) * scale + size / 2);
        int Y(double y) => size - 1 - (int)Math.Floor((y - pose.YMm) * scale + size / 2);
        void Pixel(int x, int y, uint color)
        {
            if ((uint)x >= size || (uint)y >= size) return;
            int p = (y * size + x) * 3;
            rgb[p] = (byte)(color >> 16);
            rgb[p + 1] = (byte)(color >> 8);
            rgb[p + 2] = (byte)color;
        }

        foreach (var cell in cells)
        {
            uint color = cell.Type switch
            {
                MemoryMapContentType.Unknown => 0x303741,
                MemoryMapContentType.ClearOfObstacle or MemoryMapContentType.ClearOfCliff or
                    MemoryMapContentType.ObstacleChargerRemoved => 0x5C6874,
                MemoryMapContentType.Cliff => 0xF16B5C,
                MemoryMapContentType.ObstacleCharger => 0x4EB5D0,
                MemoryMapContentType.InterestingEdge or MemoryMapContentType.NotInterestingEdge => 0xF0C467,
                _ => 0xA88BDD,
            };
            for (int y = Math.Max(0, Y(cell.Bounds.MaxY)); y <= Math.Min(size - 1, Y(cell.Bounds.MinY)); y++)
                for (int x = Math.Max(0, X(cell.Bounds.MinX)); x <= Math.Min(size - 1, X(cell.Bounds.MaxX)); x++)
                    Pixel(x, y, color);
        }

        foreach (var item in objects)
            for (int dy = -3; dy <= 3; dy++)
                for (int dx = -3; dx <= 3; dx++)
                    Pixel(X(item.XMm) + dx, Y(item.YMm) + dy, item.Charger ? 0x56E8FFu : 0xF4E4FFu);

        int cx = X(pose.XMm), cy = Y(pose.YMm);
        for (int dy = -3; dy <= 3; dy++)
            for (int dx = -3; dx <= 3; dx++)
                Pixel(cx + dx, cy + dy, 0xFFFFFF);
        var (sin, cos) = Math.SinCos(pose.HeadingRad);
        for (int i = 1; i <= 11; i++) Pixel(cx + (int)Math.Round(i * cos), cy - (int)Math.Round(i * sin), 0xFFFFFF);

        using var output = new MemoryStream();
        new ImageWriter().WritePng(rgb, size, size, ColorComponents.RedGreenBlue, output);
        return output.ToArray();
    }
}
