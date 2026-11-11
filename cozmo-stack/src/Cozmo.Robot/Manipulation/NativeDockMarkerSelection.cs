using Cozmo.Robot.Vision;
namespace Cozmo.Robot.Manipulation;

// M12-017 PR5-PR8: observed-marker branch at 00551960..00551BE4.
internal static class NativeDockMarkerSelection
{
    internal static uint Observed(ObservableObject target, Pose3d main, out KnownMarker? marker,
        SourceLogSink? log = null, Func<short,string?>? codeName = null)
    {
        var observed=NativeObservedMarkers.Get(target,target.LastObservedTimestamp);
        marker=null;
        if(observed.Count==0)return 0x0300001D;
        if(observed.Count==1){marker=observed[0];return 0;}
        marker=Closest(observed,main,log,codeName);
        if(marker is null)
            throw new NativeActionUnavailable("M1-034 M12-017: no observed marker wins; native null dereference 0055199A/005519A0 -> 0087E1D0; skip owned action");
        return 0;
    }

    /// <summary>
    /// The distance loop of <c>IDockAction::Init</c> for two or more observed markers (census DOCK-MARKER-WRT-SKIP-1 S7..S15):
    /// best = FLT_MAX, chosen = null (0x00551ACA..0x00551AD2); per marker, in list order, <c>GetWithRespectTo(marker pose, Robot::GetPose(), out)</c>
    /// (0x00551B06..0x00551B12); on failure the info log "IDockAction.Init.GetMarkerWRTRobot" and the marker is skipped with best and chosen
    /// unchanged (0x00551B16, 0x00551B90..0x00551BCE); otherwise d2 = f32(f32(x*x + y*y) + z*z) (0x00551B18..0x00551B3C), compared strictly
    /// with f32(best*best) (0x00551B3E..0x00551B4A, NaN loses) and on a win best = sqrtf(d2), chosen = marker (0x00551B4C..0x00551B8C).
    /// Returns null when nothing won (S17: the caller owns the M1-034 skip).
    /// </summary>
    // fidelity: M12-017
    internal static KnownMarker? Closest(IReadOnlyList<KnownMarker> observed, Pose3d main, SourceLogSink? log, Func<short,string?>? codeName)
    {
        KnownMarker? marker=null;
        float best=float.MaxValue;
        foreach(var candidate in observed)
        {
            using var relative=new Pose3d();
            if(!Pose3d.GetWithRespectTo(candidate.PoseOnObject,main,relative)){MarkerWrtRobotFailed(candidate,log,codeName);continue;}
            var t=relative.Translation;float x=(float)t.X,y=(float)t.Y,z=(float)t.Z;
            float d2=(x*x+y*y)+z*z;
            if(d2<best*best){best=MathF.Sqrt(d2);marker=candidate;}
        }
        return marker;
    }

    // S15 0x00551B90..0x00551BAC: sChanneledInfoF("Actions", "IDockAction.Init.GetMarkerWRTRobot", {}, "Failed to get marker %s's pose wrt to robot", Marker::GetCodeName(marker)).
    // GetCodeName 0x0087E1D0 -> GetNameForCode 0x0087E0D4 has no checked port (M11-013); without the bound name input the line is withheld visibly, never invented.
    // fidelity: M12-017
    private static void MarkerWrtRobotFailed(KnownMarker candidate, SourceLogSink? log, Func<short,string?>? codeName)
    {
        string? name=codeName?.Invoke(unchecked((short)candidate.Code));
        if(name is null)
        {
            Behavior.SteppedBehavior.ReportMissing("M11-013/M12-017 IDockAction.Init.GetMarkerWRTRobot info argument GetCodeName0x0087E1D0 -> GetNameForCode0x0087E0D4 has no checked/bound recipient; marker skipped as the engine does, log line withheld (no Enum.ToString or invented name)");
            return;
        }
        log?.Invoke(EngineLogLevel.Info,"Actions","IDockAction.Init.GetMarkerWRTRobot",$"Failed to get marker {name}'s pose wrt to robot");
    }
}
