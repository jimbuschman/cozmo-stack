using Cozmo.Robot.Behavior;
using Cozmo.Transport;

namespace Cozmo.Robot.Vision;

internal static class NativeCubeObservation
{
    internal static bool TryCreate(ObservableObject cube, Pose3d cameraPose, CameraCalibration? calibration, EngineRobot owner, out RobotObservedObjectReport report)
    {
        report = null!;
        if(cube.NativePose.Node is null) { SteppedBehavior.ReportMissing("M1-034/M11-038 null object pose at 0x00846DB6: report skipped"); return false; }
        float x=0,y=0,w=0,h=0;
        using(var relative=new Pose3d())
        {
            if(cube.NativePose.TryWithRespectTo(cameraPose,relative))
            {
                int coefficients=calibration?.DistortionCoefficients.Length ?? 0;
                if(coefficients is > 0 and < 4) { SteppedBehavior.ReportMissing("M1-034/M11-038 short distortion vector at 0x0085E2AE: report skipped"); return false; }
                (x,y,w,h)=Rectangle(relative,calibration);
            }
            else
            {
                owner.Engine.EmitSourceLog(EngineLogLevel.Error,null,"Camera.AddOccluder.ObjectDoesNotShareOrigin","Object must be in the same pose tree as the camera to add it as an occluder");
                EngineErrorState.StoreAndMaybeBreak();
            }
        }
        using var top=new Pose3d();
        float best=-float.MaxValue;
        foreach(var marker in cube.Markers)
        {
            using var rootMarker=marker.PoseOnObject.GetWithRespectToRoot();
            var normal=RelativeCubeLights.ComputeNormal(marker,rootMarker);
            float dot=(float)normal.X*0f; dot+=(float)normal.Y*0f; dot+=(float)normal.Z;
            if(dot>best) { best=dot; top.Assign(rootMarker); }
        }
        var tq=top.Quaternion;
        float angle=CozmoEngine.PublicationAngle(new EngineLocalizationPose(0,0,0,tq.W,tq.X,tq.Y,tq.Z,0));
        if(!NativeObjectPose.TryConvert(cube.NativePose,owner,out var pose))return false;
        report=new(cube.LastObservedTimestamp,(uint)cube.Family,(uint)cube.Type,cube.ObjectId,x,y,w,h,pose,angle,true);
        return true;
    }

    internal static (float X,float Y,float Width,float Height) Rectangle(Pose3d relative, CameraCalibration? calibration)
    {
        (int X,int Y,int Z)[] corners={(-1,-1,1),(1,-1,1),(-1,-1,-1),(1,-1,-1),(-1,1,1),(1,1,1),(-1,1,-1),(1,1,-1)};
        float minX=0,minY=0,maxX=0,maxY=0;
        for(int i=0;i<corners.Length;i++)
        {
            var c=corners[i]; var p=relative.Apply(new Vec3(c.X*22f,c.Y*22f,c.Z*22f));
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
    private void PublishObservedCube(EngineRobot owner, ObservableObject obj, NativeObservedObject prefix, Func<bool> current)
    {
        if(!current() || owner.Lifetime.Destroying || !ReferenceEquals(_robot.Engine.Robot,owner) || !owner.HasExternalInterface) return;
        var calibration=Calibration;
        using var cameraPose=owner.CopyCurrentCameraPose(_robot.Motion.ObservationHeadAngleRad);
        if(!NativeCubeObservation.TryCreate(obj,cameraPose.Pose,calibration,owner,out var report)) return;
        if(!current() || owner.Lifetime.Destroying || !ReferenceEquals(_robot.Engine.Robot,owner) || !owner.HasExternalInterface) return;
        _robot.Engine.DeliverObservedCubeToApp(report);
        _robot.Engine.AnimationMessages.Deliver(0x44,true,prefix);
    }
}
