using Cozmo.Protocol;
using Cozmo.Robot.Behavior;
using Cozmo.Transport;

namespace Cozmo.Robot.Vision;

// fidelity: M11-038, M11-065
internal static class NativeObjectObservation
{
    private static readonly Vec3[] CubeCorners =
    {
        new(-22, -22, 22), new(22, -22, 22), new(-22, -22, -22), new(22, -22, -22),
        new(-22, 22, 22), new(22, 22, 22), new(-22, 22, -22), new(22, 22, -22)
    };

    // Charger::GetCanonicalCorners, 0x004E9A70..0x004E9AC4.
    private static readonly Vec3[] ChargerCorners =
    {
        new(96, -40, 0), new(0, -40, 0), new(0, 40, 0), new(96, 40, 0),
        new(96, -40, 31), new(0, -40, 31), new(0, 40, 31), new(96, 40, 31)
    };
    internal static bool TryCreate(ObservableObject obj, Pose3d cameraPose, CameraCalibration? calibration, EngineRobot owner, out RobotObservedObjectReport report)
    {
        report = null!;
        if(obj.NativePose.Node is null) { SteppedBehavior.ReportMissing("M1-034/M11-038 null object pose at 0x00846DB6: report skipped"); return false; }
        float x=0,y=0,w=0,h=0;
        using(var relative=new Pose3d())
        {
            if(obj.NativePose.TryWithRespectTo(cameraPose,relative))
            {
                int coefficients=calibration?.DistortionCoefficients.Length ?? 0;
                if(coefficients is > 0 and < 4) { SteppedBehavior.ReportMissing("M1-034/M11-038 short distortion vector at 0x0085E2AE: report skipped"); return false; }
                (x,y,w,h)=Rectangle(obj.Type,relative,calibration);
            }
            else
            {
                owner.Engine.EmitSourceLog(EngineLogLevel.Error,null,"Camera.AddOccluder.ObjectDoesNotShareOrigin","Object must be in the same pose tree as the camera to add it as an occluder");
                EngineErrorState.StoreAndMaybeBreak();
            }
        }
        // 0x0061FF18..0x0061FF30 initializes +0 and takes the top-marker branch only for active family 2.
        float angle = obj.IsActive && obj.Family == ObjectFamily.LightCube ? TopMarkerAngle(obj) : 0f;
        if(!NativeObjectPose.TryConvert(obj.NativePose,owner,out var pose))return false;
        report=new(obj.LastObservedTimestamp,(uint)obj.Family,(uint)obj.Type,obj.ObjectId,x,y,w,h,pose,angle,obj.IsActive);
        return true;
    }

    private static float TopMarkerAngle(ObservableObject obj)
    {
        using var top=new Pose3d();
        float best=-float.MaxValue;
        foreach(var marker in obj.Markers)
        {
            using var rootMarker=marker.PoseOnObject.GetWithRespectToRoot();
            var normal=RelativeCubeLights.ComputeNormal(marker,rootMarker);
            float dot=(float)normal.X*0f; dot+=(float)normal.Y*0f; dot+=(float)normal.Z;
            if(dot>best) { best=dot; top.Assign(rootMarker); }
        }
        var tq=top.Quaternion;
        return CozmoEngine.PublicationAngle(new EngineLocalizationPose(0,0,0,tq.W,tq.X,tq.Y,tq.Z,0));
    }

    internal static (float X,float Y,float Width,float Height) Rectangle(ObjectType type, Pose3d relative, CameraCalibration? calibration)
    {
        var corners = type == ObjectType.Charger_Basic ? ChargerCorners : CubeCorners;
        // ProjectObject 0x0085E59E -> GetCorners 0x00876C30 -> ApplyTo(vector) 0x0065671C.
        var transformed = new List<Vec3>(corners.Length);
        relative.ApplyTo(corners, transformed);
        float minX=0,minY=0,maxX=0,maxY=0;
        for(int i=0;i<transformed.Count;i++)
        {
            Vec3 p = transformed[i];
            float px,py;
            if(calibration is null || (float)p.Z<=0f) px=py=BitConverter.UInt32BitsToSingle(0x7FC00000);
            else ObservableObject.ProjectPoint(calibration,(float)p.X,(float)p.Y,(float)p.Z,out px,out py);
            if(i==0) minX=maxX=px; else { if(px<minX)minX=px; if(px>maxX)maxX=px; }
            if(i==0) minY=maxY=py; else { if(py<minY)minY=py; if(py>maxY)maxY=py; }
        }
        return(minX,minY,maxX-minX,maxY-minY);
    }
}

internal static class NativeObjectPose
{
    internal static bool TryConvert(Pose3d input,EngineRobot owner,out EngineGamePose pose)
    {
        pose=default;
        if(input.Node is null){SteppedBehavior.ReportMissing("M1-034/M11-038 null object pose at 0x00846DB6: row skipped");return false;}
        using var flat=input.GetWithRespectToRoot();
        using var root=flat.FindRoot(); // 0x00846D44 reads the flattened output, including root-copy ID zero.
        if(!owner.TryGetNativeOriginOwner(root.Id,out _)) PoseDiagnostics.Verify("Pose3d.ToPoseStruct3d.UnknownOrigin", $"VERIFY(originList.ContainsOriginID(originID)): ID:{root.Id}");
        if(input.HasParent)
        {
            Pose3d parent=null!;
            if(flat.TryGetParent(ref parent))
            {
                using(parent) if(parent.Id!=root.Id) PoseDiagnostics.Verify("Pose3d.ToPoseStruct3d.BadParent", $"VERIFY(IsRoot() || flattenedPose.GetParent().GetID() == originID): ParentID:{parent.Id} OriginID:{root.Id}");
            }
        }
        var t=flat.Translation; var q=flat.Quaternion;
        pose=new EngineGamePose((float)t.X,(float)t.Y,(float)t.Z,(float)q.W,(float)q.X,(float)q.Y,(float)q.Z,root.Id);
        return true;
    }
}

public sealed partial class VisionSystem
{
    private RobotObservedObjectReport? PublishObservedObject(EngineRobot owner, ObservableObject obj, NativeObservedObject prefix, Func<bool> current)
    {
        if(!current() || owner.Lifetime.Destroying || !ReferenceEquals(_robot.Engine.Robot,owner) || !owner.HasExternalInterface) return null;
        var calibration=Calibration;
        using var cameraPose=owner.CopyCurrentCameraPose(_robot.Motion.ObservationHeadAngleRad);
        if(!NativeObjectObservation.TryCreate(obj,cameraPose.Pose,calibration,owner,out var report)) return null;
        if(!current() || owner.Lifetime.Destroying || !ReferenceEquals(_robot.Engine.Robot,owner) || !owner.HasExternalInterface) return null;
        // A new 0x44 observation can supply an independent pose after radio loss.
        // Robot.cs HandleSeeObservedObject updates KnownObjects even without an active cube.
        if (CozmoCubes.IsLightCube(obj.Type)) _robot.Cubes.ReportObservedCube(report.ObjectId);
        _robot.Engine.DeliverObservedObjectToApp(report);
        _robot.Engine.AnimationMessages.Deliver(0x44,true,prefix with { Pose = report.Pose });
        return report;
    }
}
