using System.Buffers.Binary;
using Cozmo.Transport;
namespace Cozmo.Robot.Vision;

// fidelity: M11-070, M11-044
public sealed partial class RobotStateHistory
{
    internal SourceLogSink? SourceLog;
    internal Action? ForceFlush;
    internal sealed class VisionOnlyStamp
    {
        internal Pose3d Pose=new("");
        internal byte[] State=new byte[0x5B];
    }
    internal readonly SortedDictionary<uint,VisionOnlyStamp> VisionOnly=new();
    internal bool RejectEmplaceForTest;
    internal uint AddVisionOnlyState(uint timestamp,Pose3d pose,uint frame,float head,float lift)
        => WithGate(()=>AddVisionOnlyStateCore(timestamp,pose,frame,head,lift));
    private uint AddVisionOnlyStateCore(uint timestamp,Pose3d pose,uint frame,float head,float lift)
    {
        lock(_gate)
        {
            if(pose.HasParent)
            {
                Pose3d parent=null!;
                if(!pose.TryGetParent(ref parent))return 1;
                bool flattened;
                using(parent)flattened=parent.IsRoot; // 0x005310B4/C2/C8/D0, public checks then wrapper destruction.
                if(!flattened)
                // fidelity: M11-086 — 0x005311CA, name-only path is the %s argument at 0x005311E6.
                {Error("RobotStateHistory.AddVisionOnlyState.NonFlattenedPose",FormattableString.Invariant($"Pose object inside pose stamp should be flattened ({PoseDiagnostics.CString(pose.GetNamedPathToRoot(false))})"));return 1;}
            }
            uint newest=_entries.Count==0?0:_entries.Last().Key;
            if(_entries.Count!=0&&newest>WindowMs&&timestamp<newest-WindowMs)
            {Error("RobotStateHistory.AddVisionOnlyState.TooOld",FormattableString.Invariant($"Pose at t={unchecked((int)timestamp)} too old to add. Newest time={unchecked((int)newest)}, windowSize={unchecked((int)WindowMs)}"));return 1;}
            var state=new byte[0x5B];
            static void U(byte[] b,int o,uint v)=>BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(o),v);
            U(state,0,1);U(state,4,frame);U(state,8,1);
            U(state,0x28,BitConverter.SingleToUInt32Bits(head));U(state,0x2C,BitConverter.SingleToUInt32Bits(lift));
            U(state,0x48,0x40A00000);U(state,0x4C,0x300);U(state,0x50,uint.MaxValue);U(state,0x54,uint.MaxValue);state[0x5A]=0xFF;
            if(VisionOnly.TryGetValue(timestamp,out var current))
            {current.Pose.Assign(pose);state.CopyTo(current.State,0);return 0;}
            if(RejectEmplaceForTest)
            {Error("RobotStateHistory.AddVisionOnlyState.EmplaceFailed",FormattableString.Invariant($"Emplace of pose with t={unchecked((int)timestamp)}, frameID={unchecked((int)frame)} failed"));return 1;}
            var entry=new VisionOnlyStamp();entry.Pose.Assign(pose);entry.State=state;VisionOnly.Add(timestamp,entry);
            CullToWindowSize();
            return 0;
        }
    }
    private void Error(string key,string message)
    {SourceLog?.Invoke(EngineLogLevel.Error,null,key,message);EngineErrorState.StoreAndMaybeBreak();}
}
