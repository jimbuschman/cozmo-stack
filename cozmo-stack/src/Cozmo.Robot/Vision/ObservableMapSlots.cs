using Cozmo.Protocol;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Behavior;
namespace Cozmo.Robot.Vision;

// fidelity: M11-088
public sealed partial class ObservableObject
{
    /// <summary>ObservableObject-subobject slot+5C: ActiveCube/Charger1; base/markerless0. SDK custom objects are unreachable.</summary>
    public byte ObservableMapSlot5C => CubeGeometry.IsCube(Type) || Type==ObjectType.Charger_Basic ? (byte)1 : (byte)0;

    /// <summary>Slot+50 receives the explicit pose. Reuses the existing native 3D corner/fit units.</summary>
    public bool TryObservableBoundingQuad50(Pose3d pose,float padding,out Quadrilateral? quad)
    {
        quad=null;
        if(CubeGeometry.IsCube(Type)) return Footprint.TryGetBoundingQuadXY(this,pose,padding,out quad);
        if(Type==ObjectType.Charger_Basic)
        {
            try {quad=ObjectFootprint.ChargerBoundingQuadXY(pose,padding);return true;}
            catch(NotSupportedException)
            {SteppedBehavior.ReportMissing("M11-088/M13-024 Charger base quad 0x0087713A -> shared minAreaRect/corner-sort recipient; branch unported");return false;}
        }
        if(Family==ObjectFamily.MarkerlessObject && MarkerlessObject.SizeByType(Type) is {} size)
        {
            if(ObjectFootprint.TryNativeMarkerlessBoundingQuadXY(new Vec3(size.X,size.Y,size.Z),pose,padding,out quad))return true;
            SteppedBehavior.ReportMissing("M11-091/M13-024 MarkerlessObject::GetBoundingQuadXY 0x00502D26: shared minAreaRect/corner-sort branch is unavailable");
            return false;
        }
        SteppedBehavior.ReportMissing("M11-088 ObservableObject+0x50 concrete producer is outside the ActiveCube/Charger/markerless build scope (SDK custom/passive world objects unreachable)");
        return false;
    }
}
