using Cozmo.Protocol;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot;

// fidelity: M11-097, M11-051, M11-053
public sealed partial class EngineRobot
{
    // Robot::GetPose 0x004EA398, copied while holding the current-pose writer's gates in the same order.
    internal Pose3d CopyCheckedMainPose()
    {
        Pose3d snapshot=null!;
        using var frame=Engine.DelocalizationWorld?.EnterPoseMutation();
        LogCallbackDeferral.RunGated(PoseTreeGate,()=>snapshot=new Pose3d(GetCheckedMainPose()));
        return snapshot;
    }
    internal byte OnRampState; // robot+0x316; no true writer is reachable under M2-024/OV127
    internal uint LastHistoryAdmissionResult {get;private set;}
    internal uint LastFullStateResult {get;set;}
    internal bool CheckHistoryOrigin(RobotState state)
    {
        if(OfflineSeamAcceptsAnyOrigin || ContainsOriginId(state.PoseOriginId))
        {AcceptedState=state;return true;}
        AcceptedState=null;
        Engine.EmitSourceLog(EngineLogLevel.Warning,null,"Robot.UpdateFullRobotState.BadOriginID",
            $"Received RobotState with originID={state.PoseOriginId}, only {_origins.Count} pose origins available");
        return false;
    }
    internal uint AddIncomingStateToHistory(RobotState state)
    {
        uint result=1;
        using var frame=Engine.DelocalizationWorld?.EnterPoseMutation();
        LogCallbackDeferral.RunGated(PoseTreeGate,()=>
        {
            using var incoming=new Pose3d("");
            if(OnRampState==0)
            {
                if(!CheckHistoryOrigin(state))return;
                var origin=GetNativeOrigin(state.PoseOriginId);
                using var pose=new Pose3d(Rotation3d.FromAngleAxis(new Radians(state.Pose.Angle).Value,new Vec3(0,0,1)),
                    new Vec3(state.Pose.X,state.Pose.Y,state.Pose.Z),parent:origin);
                using var flattened=pose.GetWithRespectToRoot();incoming.Assign(flattened);
                float z;
                if(state.PoseFrameId==PoseFrameId)
                {using var current=GetCheckedMainPose().GetWithRespectToRoot();z=(float)current.Translation.Z;}
                else
                {
                    using var prior=new HistRobotState();result=PoseHistory.GetLastStateWithFrameID(state.PoseFrameId,prior);
                    if(result!=0)
                    {
                        Engine.EmitSourceLog(EngineLogLevel.Error,null,"Robot.UpdateFullRobotState.GetLastPoseWithFrameIdError",
                            $"Failed to get last pose from history with frame ID={unchecked((int)state.PoseFrameId)}");
                        Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak();return;
                    }
                    using var root=prior.Pose.GetWithRespectToRoot();z=(float)root.Translation.Z;
                }
                incoming.Node!.Transform=new Transform3d(incoming.Quaternion,new Vec3((float)incoming.Translation.X,(float)incoming.Translation.Y,z));
            }
            else AcceptedState=state; // A2 bypasses origin lookup/input pose construction
            result=PoseHistory.AddRawOdomState(state.Timestamp,incoming,state);
            if(result!=0)Engine.EmitSourceLog(EngineLogLevel.Warning,null,"Robot.UpdateFullRobotState.AddPoseError",
                $"AddRawOdomStateToHistory failed for timestamp={unchecked((int)state.Timestamp)}");
        });
        LastHistoryAdmissionResult=result;return result;
    }
    // OV12 caller flag0, OV128 A8; checked helper boundary in M11-064/20261013-play-area-check.md.
    internal bool UpdateCurrPoseFromHistory()
    {
        bool updated=false;
        using var frame=Engine.DelocalizationWorld?.EnterPoseMutation();
        LogCallbackDeferral.RunGated(PoseTreeGate,()=>
        {
            using var value=new HistRobotState();uint time=0;
            if(PoseHistory.ComputeStateAt(PoseHistory.NewestTimeStamp,ref time,value,false)!=0)return;
            using var current=new Pose3d("");var origin=GetNativeOrigin(CurrentOriginId);
            if(Pose3d.GetWithRespectTo(value.Pose,origin,current))
            {SetPose(current);updated=true;return;}
            using var root=value.Pose.FindRoot();
            Engine.EmitSourceLog(EngineLogLevel.Info,"Unnamed","Robot.UpdateCurrPoseFromHistory.GetWrtParentFailed",
                $"Could not update robot {unchecked((int)NativeRobotId)}'s current pose using historical pose w.r.t. {root.Name} because we are now in frame {origin.Name}.");
        });
        return updated;
    }
}
