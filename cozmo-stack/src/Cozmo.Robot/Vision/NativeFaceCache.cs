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
    internal Func<IDisposable>? NativePoseScope;
    private Pose3d? _nativeLastPose;
    private uint _nativeLastStamp;
    //004F4956..4A38: this owned pose is independent of the live face map.
    private void UpdateNativeLastFace(FaceEntry entry, EngineRobot expectedOwner, int generation)
    {
        using var mutation=NativePoseScope?.Invoke();
        lock(_gate)
        {
            if (!NativeOwnerCurrent(expectedOwner, generation)) return;
            if(entry.TimesObserved<=3&&!entry.HasName)return;
            if(_nativeLastPose is not null && _nativeLastStamp==entry.LastObservedTimestamp)
            {
                var main = expectedOwner.GetCheckedMainPose();
                float Norm(Pose3d pose)
                {var t=Manipulation.PlacementProducerGeometry.Vector(main,pose);float x=(float)t.X,y=(float)t.Y,z=(float)t.Z;float sum=x*x;sum=sum+y*y;sum=sum+z*z;return MathF.Sqrt(sum);}
                float oldDistance = Norm(_nativeLastPose), newDistance = Norm(entry.HeadPose);
                if (!NativeOwnerCurrent(expectedOwner, generation)) return;
                if(!(oldDistance>newDistance))return; // ARM BLE also keeps old on unordered.
            }
            if (!NativeOwnerCurrent(expectedOwner, generation)) return;
            _nativeLastPose??=new Pose3d("");_nativeLastPose.Assign(entry.HeadPose);
            _nativeLastStamp=entry.LastObservedTimestamp;
        }
    }
    /// <summary>
    /// <c>FaceWorld::UpdateFaceOrigins(old, new)</c> 0x004F5644..0x004F5866 (census LOCALIZE-CROSS-ORIGIN-1 X12), called by LocalizeToObject at 0x005159A8.
    /// Each face in ascending face ID whose head pose's immediate parent is the old origin (IsParentOf, 0x004F5688..0x004F568E) is converted with
    /// <c>GetWithRespectTo(headPose, newOrigin)</c> (0x004F56C6): success -> the FlatteningFace debug, head pose := the result (0x004F5722), count + 1;
    /// failure -> the HeadPoseUpdateFailed warning and the face keeps its pose. Then, when the last-observed timestamp (+0x20) is not 0 and the
    /// last-observed pose (+0x14) is an immediate child of the old origin, that pose is converted in place (0x004F57F6); failure -> the
    /// UpdateLastObservedPoseFailed warning. Returns the count; sends nothing.
    /// </summary>
    // fidelity: M11-044
    internal int UpdateFaceOrigins(uint oldOriginId, uint newOriginId)
    {
        var owner = NativeOwner?.Invoke();
        if (owner is null)
        {
            Behavior.SteppedBehavior.ReportMissing("M11-044 FaceWorld::UpdateFaceOrigins 0x004F5644: no native Robot owner (origin list Robot+0x294) is bound to this face world; faces not moved");
            return 0;
        }
        using var mutation = NativePoseScope?.Invoke();
        using var diagnostics = Pose3d.UseSourceLog((l, c, k, t) => owner.Engine.EmitSourceLog(l, c, k, t), () => owner.Engine.Das.ForceFlushNow());
        var oldOrigin = owner.GetNativeOrigin(oldOriginId);                      // 0x004F5654
        var newOrigin = owner.GetNativeOrigin(newOriginId);                      // 0x004F5662
        string oldName = PoseDiagnostics.CString(oldOrigin.Name), newName = PoseDiagnostics.CString(newOrigin.Name);
        int count = 0;
        lock (_gate)
        {
            foreach (var entry in _faces.OrderBy(p => p.Key).Select(p => p.Value).ToArray())   // std::map at FaceWorld+4, ascending face ID
            {
                var head = entry.HeadPose;                                       // node+0x108 = entry+0xF4
                if (head?.Node?.Parent is null || !ReferenceEquals(head.Node.Parent, oldOrigin.Node)) continue;   // IsParentOf, immediate parent
                using var converted = new Pose3d("");
                if (Pose3d.GetWithRespectTo(head, newOrigin, converted))
                {
                    owner.Engine.EmitSourceLog(EngineLogLevel.Debug, "FaceRecognizer", "FaceWorld.UpdateFaceOrigins.FlatteningFace",
                        FormattableString.Invariant($"Flattened FaceID:{entry.Id} w.r.t. {newName}"));
                    head.Assign(converted);                                      // 0x004F5722 headPose := tmp
                    // The stack's mirrors of entry+0xF4 (the translation a no-parts observation keeps, the rotation and the root the queries test).
                    if (entry.NativeTrackingTranslation is not null) entry.NativeTrackingTranslation = head.Translation;
                    if (entry.NativeTrackingRotation is not null) entry.NativeTrackingRotation = head.Quaternion;
                    entry.NativeObservationOrigin = newOrigin.Node;
                    count++;
                }
                else
                    owner.Engine.EmitSourceLog(EngineLogLevel.Warning, null, "FaceWorld.UpdateFaceOrigins.HeadPoseUpdateFailed",
                        FormattableString.Invariant($"Head pose of FaceID:{entry.Id} is w.r.t. to old origin {oldName} but failed to flatten to be w.r.t new origin {newName}"));
            }
            // FaceWorld+0x20 (timestamp) / +0x14 (pose): converted in place (0x004F57F6).
            if (_nativeLastStamp != 0 && _nativeLastPose?.Node?.Parent is { } parent && ReferenceEquals(parent, oldOrigin.Node) &&
                !Pose3d.GetWithRespectTo(_nativeLastPose, newOrigin, _nativeLastPose))
                owner.Engine.EmitSourceLog(EngineLogLevel.Warning, null, "FaceWorld.UpdateFaceOrigins.UpdateLastObservedPoseFailed",
                    $"Failed to flatten last observed pose from {oldName} to {newName}");
        }
        return count;
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
