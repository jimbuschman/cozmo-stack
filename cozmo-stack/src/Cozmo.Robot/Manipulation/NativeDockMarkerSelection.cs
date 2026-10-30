using Cozmo.Robot.Vision;
namespace Cozmo.Robot.Manipulation;

// M12-017 PR5-PR8: observed-marker branch at 00551960..00551BE4.
internal static class NativeDockMarkerSelection
{
    internal static uint Observed(ObservableObject target, Pose3d main, out KnownMarker? marker)
    {
        var observed=NativeObservedMarkers.Get(target,target.LastObservedTimestamp);
        marker=null;
        if(observed.Count==0)return 0x0300001D;
        if(observed.Count==1){marker=observed[0];return 0;}
        float best=float.MaxValue;
        foreach(var candidate in observed)
        {
            using var relative=new Pose3d();
            if(!Pose3d.GetWithRespectTo(candidate.PoseOnObject,main,relative))continue;
            var t=relative.Translation;float x=(float)t.X,y=(float)t.Y,z=(float)t.Z;
            float d2=(x*x+y*y)+z*z;
            if(d2<best*best){best=MathF.Sqrt(d2);marker=candidate;}
        }
        if(marker is null)
            throw new NativeActionUnavailable("M1-034 M12-017: no observed marker wins; native null dereference 0055199A/005519A0 -> 0087E1D0; skip owned action");
        return 0;
    }
}
