namespace Cozmo.Robot.Vision;
//00876C50..96, separate from the legacy public code-list adapter.
internal static class NativeObservedMarkers
{
    internal static IReadOnlyList<KnownMarker> Get(ObservableObject target,uint since)
    {
        if(since==0)return Array.Empty<KnownMarker>();
        if(target.Markers.Any(m=>m.LastObservedTimestamp is null))
            throw new NativeActionUnavailable("M11-013 native marker timestamp constructor input absent; native observed-marker query withheld");
        return target.Markers.Where(m=>m.LastObservedTimestamp!.Value>=since).ToArray();
    }
}
