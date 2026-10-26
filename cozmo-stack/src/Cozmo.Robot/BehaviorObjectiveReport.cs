using System.Buffers.Binary;
using Cozmo.Robot.Behavior;

namespace Cozmo.Robot;

public sealed partial class CozmoEngine
{
    // fidelity: M7-019, M13-017, M1-056, M5-056
    // IBehavior::BehaviorObjectiveAchieved 0x005BE228..308; adapter0x005BE3A8..3D6.
    // The native name is supplied by checked callers; this does not invent an enum-name map.
    internal void ReportBehaviorObjective(uint objective,string nativeObjectiveName,string behaviorName,byte broadcast=1)
    {
        if(broadcast==1 && Robot?.HasExternalInterface==true)
        {
            byte[] payload=new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(payload,objective); // union0x007285FE, pack0x0071BEAE
            DeliverAnimationReportToApp(0x8B,payload); // rvalue0x006625B0: enqueue before synchronous local emit
            AnimationMessages.Deliver(0x8B,true,objective);
            if(Robot?.NativeActivityRecipientsConstructed!=true)
                SteppedBehavior.ReportMissing("Objective recipients: actual constructed Activity owners/subscription readiness unavailable; Sparked0x005B181E and Socialize0x005B07C2 effects are not established by local delivery alone");
        }
        // 0x005BE24A..308: both diagnostics run even when publication is suppressed.
        EmitSourceLog(EngineLogLevel.Info,"Behaviors","IBehavior.BehaviorObjectiveAchieved",
            $"Behavior:{behaviorName}, Objective:{nativeObjectiveName}");
        EmitSourceLog(EngineLogLevel.Event,null,"robot.freeplay_objective_achieved",behaviorName,
            new Dictionary<string,string>{{"$data",nativeObjectiveName}});
    }
}
