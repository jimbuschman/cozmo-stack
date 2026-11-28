using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Vision;

namespace Cozmo.Bridge;

public sealed partial class CozmoBridge
{
    // Our C2 policy selects goals. DriveToPose and the charger's pre-dock goal
    // (0x005592E8..0x005592FE, 0x004E9FE4..0x004EA058) own the approach.
    internal static RoomPose? GoHomeGoal(EngineRobot owner)
    {
        var engine = owner.Engine;
        if (engine.C2ChargerMemory.Get(owner) is { Anchored: true } fused)
            return new(fused.XMm, fused.YMm, fused.HeadingRad);
        if (!engine.Options.C2.MapReanchor) return null;
        foreach (var layer in engine.RoomMap.Snapshot(owner).Where(layer => layer.Anchored))
            if (layer.ToCurrent is { } transform &&
                layer.Landmarks.FirstOrDefault(o => o.Type == ObjectType.Charger_Basic) is { } charger)
                return transform.Apply(charger.Pose);
        return null;
    }

    private async Task<CommandResult> GoHome(CancellationToken token)
    {
        var engine = Robot.Engine;
        var stages = new List<string>();
        void Stage(string detail)
        {
            stages.Add(detail);
            engine.EmitStackLog(CozmoC2Settings.GoHomeName + ": " + detail);
        }
        CommandResult Fail(string reason)
        {
            Stage(reason);
            return new("not-admitted", false, null, null, reason, Report: new { stages });
        }
        var owner = engine.Robot;
        if (!engine.Options.C2.GoHome) return new("not-admitted", false, null, null, "C2:go-home disabled");
        if (owner?.NativeBehaviorRuntime is not { } runtime) return CommandResult.NotReady();
        bool Current() => ReferenceEquals(owner, engine.Robot) && !owner.Lifetime.Destroying && !runtime.IsRetired;
        bool LiveCharger() => engine.C2ChargerMemory.Get(owner) is { Anchored: true } fused &&
            runtime.Manipulation?.World.GetLocatedObjectById(fused.WorldObjectId) is { IsLocated: true, Type: ObjectType.Charger_Basic } obj &&
            obj.OriginId == owner.CurrentOriginId;
        async Task<string?> Scan(Func<bool> goal)
        {
            Stage("looking around in place");
            return await runtime.ScanForC2Goal("Hiking_LookInPlace360", goal, token).ConfigureAwait(false);
        }
        Stage("locating charger");
        if (!engine.Options.C2.ChargerDock || !engine.Options.C2.ChargerMemory) return Fail("charger dock and memory must be enabled");
        if (Robot.Sensors.OnChargerContacts) { Stage("already on charger"); return CommandResult.Confirmed(new { stages }); }
        if (GoHomeGoal(owner) is null && await Scan(() => GoHomeGoal(owner) is not null).ConfigureAwait(false) is { } failure)
            return Fail(failure);
        Task<CommandResult>? drive = null;
        uint origin;
        using (engine.EnterSynchronousOwner())
        {
            token.ThrowIfCancellationRequested();
            if (!Current()) return Fail("robot owner retired");
            origin = owner.CurrentOriginId;
            if (!LiveCharger())
            {
                if (GoHomeGoal(owner) is not { } goal) return Fail("charger map is unanchored");
                using var flat = new Pose3d(Mat3.AboutZ(goal.HeadingRad), new Vec3(goal.XMm, goal.YMm, 0));
                using var pre = ChargerGeometry.PreDockPose(flat);
                Stage("driving to remembered charger viewing pose");
                drive = FromAction(Robot.Motion.QueueAiGotoPoseAsync((float)pre.Translation.X,
                    (float)pre.Translation.Y, (float)pre.AngleAroundZ, token));
            }
        }
        if (drive is not null)
        {
            var result = await drive.ConfigureAwait(false);
            if (result.EngineResult != 0 || result.OwnerRetired == true)
            { Stage("approach failed: " + result.Detail); return result with { Report = new { stages } }; }
        }
        Task<string?>? search = null;
        using (engine.EnterSynchronousOwner())
        {
            if (!Current() || owner.CurrentOriginId != origin) return Fail("robot or map origin changed during approach");
            if (!LiveCharger()) search = Scan(LiveCharger);
        }
        if (search is not null && await search.ConfigureAwait(false) is { } reason) return Fail(reason);
        Task<CommandResult> mount;
        using (engine.EnterSynchronousOwner())
        {
            token.ThrowIfCancellationRequested();
            if (!Current() || !LiveCharger()) return Fail("live charger unavailable for mounting");
            Stage("approaching and mounting live charger");
            mount = FromAction(Robot.Motion.QueueAiMountChargerAsync(engine.C2ChargerMemory.Get(owner)!.WorldObjectId, token));
        }
        var mounted = await mount.ConfigureAwait(false);
        Stage(mounted.EngineResult == 0 && mounted.OwnerRetired != true ? "mounted" : "mount failed: " + mounted.Detail);
        return mounted with { Report = new { stages } };
    }
}
