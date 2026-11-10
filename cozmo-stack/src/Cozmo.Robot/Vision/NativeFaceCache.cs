namespace Cozmo.Robot.Vision;

internal sealed record NativeObservedFace(int Id,uint Timestamp);
internal sealed record NativeChangedFace(int OldId,int NewId);
internal sealed record NativeDeletedFace(int Id);
internal sealed record NativeErasedEnrolledFace(int Id, string Name);

// fidelity: M14-001, M14-008
public sealed partial class FaceWorld
{
    internal Action<EngineRobot,ushort,object>? NativePublish;
    internal Func<int>? NativeGeneration;
    internal Func<Pose3d?>? NativeMainPose;
    internal Func<IDisposable>? NativePoseScope;
    private Pose3d? _nativeLastPose;
    private uint _nativeLastStamp;
    //004F4956..4A38: this owned pose is independent of the live face map.
    private void UpdateNativeLastFace(FaceEntry entry, Pose3d? preparedNativePose = null, EngineRobot? expectedOwner = null, int generation = 0)
    {
        using var mutation=NativePoseScope?.Invoke();
        lock(_gate)
        {
            if (expectedOwner is not null && !NativeOwnerCurrent(expectedOwner, generation)) return;
            if(entry.TimesObserved<=3&&!entry.HasName)return;
            if(_nativeLastPose is not null && _nativeLastStamp==entry.LastObservedTimestamp)
            {
                var main = expectedOwner is not null ? expectedOwner.GetCheckedMainPose() : NativeMainPose?.Invoke();
                if(main is null){Behavior.SteppedBehavior.ReportMissing("M14-001 native face cache actual MAIN unavailable; prior cache retained");return;}
                float Norm(Pose3d pose)
                {var t=Manipulation.PlacementProducerGeometry.Vector(main,pose);float x=(float)t.X,y=(float)t.Y,z=(float)t.Z;float sum=x*x;sum=sum+y*y;sum=sum+z*z;return MathF.Sqrt(sum);}
                float oldDistance = Norm(_nativeLastPose), newDistance = Norm(preparedNativePose ?? entry.HeadPose);
                if (expectedOwner is not null && !NativeOwnerCurrent(expectedOwner, generation)) return;
                if(!(oldDistance>newDistance))return; // ARM BLE also keeps old on unordered.
            }
            if (expectedOwner is not null && !NativeOwnerCurrent(expectedOwner, generation)) return;
            _nativeLastPose??=new Pose3d("");_nativeLastPose.Assign(preparedNativePose ?? entry.HeadPose);
            _nativeLastStamp=entry.LastObservedTimestamp;
        }
    }
    //004F5AE2..5B38: false allows ROOT-flatten/reparent; map removal does not clear the cache.
    internal bool CopyNativeLastFace(Pose3d currentOrigin,bool requireCurrentOrigin,Pose3d output)
        => CopyNativeLastFaceTimestamp(currentOrigin,requireCurrentOrigin,output)!=0;
    internal uint CopyNativeLastFaceTimestamp(Pose3d currentOrigin,bool requireCurrentOrigin,Pose3d output)
    {
        using var mutation=NativePoseScope?.Invoke();
        lock(_gate)
        {
            if(_nativeLastStamp==0||_nativeLastPose is null)return 0;
            using var root=_nativeLastPose.FindRoot();
            if(ReferenceEquals(root.Node,currentOrigin.Node)){output.Assign(_nativeLastPose);return _nativeLastStamp;}
            if(requireCurrentOrigin)return 0;
            using var flat=_nativeLastPose.GetWithRespectToRoot();output.Assign(flat);output.SetParent(currentOrigin);return _nativeLastStamp;
        }
    }
}
