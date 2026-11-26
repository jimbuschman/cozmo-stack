using Cozmo.Protocol;

namespace Cozmo.Robot.Vision;

/// <summary>One reported charger estimate, in its explicitly named origin. An unanchored
/// estimate is remembered evidence, not a location usable in the current coordinate system.</summary>
public sealed record C2ChargerEstimate(uint WorldObjectId, long OwnerEpoch, uint OriginId, bool Anchored,
    double XMm, double YMm, double ZMm, double HeadingRad, double PitchDeg, double RollDeg,
    long Sightings, long UnanchoredSightings, uint LastObservedTimestamp, double LastDistanceMm,
    double LastViewingAngleDeg, double HeadingConfidence);

/// <summary>C2:charger-memory. Host-only persistent-in-memory estimate of the unique charger.
/// Do not invent transforms between separate native pose roots: keep their aggregate evidence
/// until an actual native rejoin (or a later explicit C2 re-anchor) establishes the transform.</summary>
internal sealed class C2ChargerMemory
{
    private readonly record struct Origin(long OwnerEpoch, uint Id);
    private sealed class Aggregate
    {
        internal uint ObjectId, Stamp;
        internal long Count, Order;
        internal double Weight, X, Y, Sin, Cos, Distance, View;
        internal void Add(uint id, uint stamp, double x, double y, double yaw, double distance, double view, long order)
        {
            // Explicit C2 policy, not an engine constant: inverse-square range with a
            // one-mm floor against division by zero, times squared front incidence
            // clamped to 0.05 for extreme oblique measurements. No yaw-wrap averaging.
            double weight = Math.Pow(Math.Max(0.05, Math.Cos(view)), 2) / Math.Pow(Math.Max(1.0, distance), 2);
            X += weight*x; Y += weight*y;
            Sin += weight*Math.Sin(yaw); Cos += weight*Math.Cos(yaw);
            Weight += weight; Count++; ObjectId=id; Stamp=stamp; Distance=distance; View=view; Order=order;
        }
        internal void Transform(Pose3d oldInNew)
        {
            // The mean of planar points transforms affinely. Rotate the entire
            // circular heading resultant (not merely the scalar mean).
            var mean = oldInNew.Rotation * new Vec3(X/Weight, Y/Weight, 0) + oldInNew.Translation;
            var heading = oldInNew.Rotation * new Vec3(Cos, Sin, 0);
            X=mean.X*Weight; Y=mean.Y*Weight; Cos=heading.X; Sin=heading.Y;
        }
        internal void Merge(Aggregate other)
        {
            X+=other.X; Y+=other.Y; Sin+=other.Sin; Cos+=other.Cos;
            Weight+=other.Weight; Count+=other.Count;
            if (other.Order>Order)
            { Order=other.Order; Stamp=other.Stamp; ObjectId=other.ObjectId; Distance=other.Distance; View=other.View; }
        }
    }
    private readonly Dictionary<Origin, Aggregate> _estimates = new();
    private readonly object _gate = new();
    private long _order;

    internal void Observe(EngineRobot owner, ObservableObject observed, uint locatedId, Pose3d robotAtSighting)
    {
        // 0x00625540..0x006255AC gives the single observed instance and stamp;
        // 0x00621146..0x006211DC accepts it into BlockWorld. Use that instance,
        // NOT the previously confirmed/stored estimate repeatedly.
        if (!owner.Engine.Options.C2.ChargerMemory || owner.Lifetime.Destroying ||
            !ReferenceEquals(owner.Engine.Robot,owner) || observed.Type!=ObjectType.Charger_Basic ||
            observed.OriginId!=owner.CurrentOriginId || observed.LastObservedTimestamp==0) return;
        using var inOrigin = new Pose3d();
        using var inCharger = new Pose3d();
        var origin = owner.GetNativeOrigin(owner.CurrentOriginId);
        if (!Pose3d.GetWithRespectTo(observed.NativePose, origin, inOrigin) ||
            !Pose3d.GetWithRespectTo(robotAtSighting, observed.NativePose, inCharger)) return;
        var pos=inOrigin.Translation;
        double yaw=inOrigin.AngleAroundZ;
        var r=inCharger.Translation;
        double range=r.Length, xy=Math.Sqrt(r.X*r.X+r.Y*r.Y);
        // The charger-facing reference is its -X axis (native pre-dock pose
        // 0x004E9FE4..0x004EA01A). Use the observation-time pose, not current robot motion.
        double view=Math.Abs(Math.Atan2(r.Y,-r.X));
        if (!double.IsFinite(pos.X) || !double.IsFinite(pos.Y) || !double.IsFinite(yaw) ||
            !double.IsFinite(range) || !double.IsFinite(view) || xy<=0) return;
        C2ChargerEstimate snapshot;
        lock (_gate)
        {
            var key=new Origin(owner.C2MapOwnerEpoch, owner.CurrentOriginId);
            if (!_estimates.TryGetValue(key,out var estimate)) _estimates[key]=estimate=new();
            // One accepted distinct camera frame per origin; monotonic uint
            // timestamps may wrap, so reject an equal or older signed difference.
            if (estimate.Count!=0 && unchecked((int)(observed.LastObservedTimestamp-estimate.Stamp))<=0) return;
            estimate.Add(locatedId,observed.LastObservedTimestamp,pos.X,pos.Y,yaw,range,view,++_order);
            snapshot=SnapshotLocked(key,estimate,true);
        }
        owner.Engine.EmitStackLog(FormattableString.Invariant(
            $"{CozmoC2Settings.ChargerMemoryName}: accepted sighting {snapshot.LastObservedTimestamp} at {range:F1}mm, view {view*180/Math.PI:F1}deg; origin {snapshot.OriginId}, fused {snapshot.Sightings} sightings"));
    }

    /// <summary>Apply an actual old-root to new-root pose relation before native Flatten.
    /// Future C2 map re-anchor can use the same entry point with a measured transform.</summary>
    internal void Reanchor(EngineRobot owner, uint oldOriginId, uint newOriginId, Pose3d oldInNew)
    {
        if (!owner.Engine.Options.C2.ChargerMemory || owner.Lifetime.Destroying ||
            !ReferenceEquals(owner.Engine.Robot,owner) || oldOriginId==newOriginId) return;
        long count;
        lock (_gate)
        {
            var from=new Origin(owner.C2MapOwnerEpoch,oldOriginId);
            var to=new Origin(owner.C2MapOwnerEpoch,newOriginId);
            if (!_estimates.Remove(from,out var previous)) return;
            previous.Transform(oldInNew);
            if (_estimates.TryGetValue(to,out var existing)) { existing.Merge(previous); count=existing.Count; }
            else { _estimates[to]=previous; count=previous.Count; }
        }
        owner.Engine.EmitStackLog($"{CozmoC2Settings.ChargerMemoryName}: rejoined native origin {oldOriginId} -> {newOriginId}; {count} fused sightings");
    }

    internal void OriginChanged(EngineRobot owner)
    {
        if (Get(owner) is { Anchored:false } remembered)
            owner.Engine.EmitStackLog($"{CozmoC2Settings.ChargerMemoryName}: origin {owner.CurrentOriginId} lacks a verified relation to charger origin {remembered.OriginId}; retaining unanchored estimate");
    }

    internal C2ChargerEstimate? Get(EngineRobot? owner)
    {
        lock (_gate)
        {
            if (owner is not null && !owner.Lifetime.Destroying)
            {
                var current=new Origin(owner.C2MapOwnerEpoch,owner.CurrentOriginId);
                if (_estimates.TryGetValue(current,out var estimate)) return SnapshotLocked(current,estimate,true);
            }
            if (_estimates.Count==0) return null;
            var latest=_estimates.MaxBy(kv=>kv.Value.Order);
            return SnapshotLocked(latest.Key,latest.Value,false);
        }
    }

    private C2ChargerEstimate SnapshotLocked(Origin key, Aggregate value, bool anchored) =>
        new(value.ObjectId,key.OwnerEpoch,key.Id,anchored,value.X/value.Weight,value.Y/value.Weight,0,
            Math.Atan2(value.Sin,value.Cos),0,0,value.Count,
            _estimates.Where(kv=>kv.Key!=key).Sum(kv=>kv.Value.Count),value.Stamp,value.Distance,
            value.View*180/Math.PI,Math.Min(1,Math.Sqrt(value.Sin*value.Sin+value.Cos*value.Cos)/value.Weight));

    // Only anchored coordinates, with the actual current origin parent, may feed
    // the future C2 dock action. Do not create a phantom native located-object ID.
    internal Pose3d? CopyAnchoredPose(EngineRobot owner)
    {
        var estimate=Get(owner);
        return estimate is { Anchored:true } ? new Pose3d(
            Rotation3d.FromAngleAxis((float)estimate.HeadingRad,new Vec3(0,0,1)),
            new Vec3(estimate.XMm,estimate.YMm,0),parent:owner.GetNativeOrigin(estimate.OriginId)) : null;
    }
}
