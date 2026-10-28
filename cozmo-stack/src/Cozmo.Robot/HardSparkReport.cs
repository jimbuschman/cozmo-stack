using System.Buffers.Binary;

namespace Cozmo.Robot;
public sealed partial class CozmoEngine
{
    // fidelity: M15-059 — 005B171C/005B2094, H9 native83/84 payloads.
    internal void ReportHardSparkStarted(int unlock)
    {
        if(Robot?.HasExternalInterface!=true)return;
        var bytes=new byte[4];BinaryPrimitives.WriteInt32LittleEndian(bytes,unlock);
        DeliverAnimationReportToApp(0x83,bytes);AnimationMessages.Deliver(0x83,true,unlock);
    }
    internal void ReportHardSparkEnded(bool success)
    {
        if(Robot?.HasExternalInterface!=true)return;
        DeliverAnimationReportToApp(0x84,new[]{success?(byte)1:(byte)0});AnimationMessages.Deliver(0x84,true,success);
    }
}
