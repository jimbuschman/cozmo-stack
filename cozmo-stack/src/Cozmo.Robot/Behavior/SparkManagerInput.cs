using Cozmo.Protocol;
using Cozmo.Transport;

namespace Cozmo.Robot.Behavior;

// fidelity: M15-057
// Internal carrier: the public app input exposes only ActivateSpark, not cases1..3.
internal readonly record struct SparkManagerMessage(byte Tag,int UnlockId=0)
{
    internal const ushort UnionTag=0x0096;
    internal byte[] ToUnionBytes()
    {
        var w=new CladWriter().U16(UnionTag).U8(Tag);
        if(Tag is 0 or 1)w.I32(UnlockId);
        return w.ToArray(); // checked Pack757A68..757AB4; no native alignment padding
    }
}

public sealed partial class BehaviorManager
{
    // fidelity: M15-058 — ctor5A08DA..5A0900, full-width words and separate bytes.
    internal int ActiveSparkId {get;private set;}=0x55;
    internal int RequestedSparkId {get;private set;}=0x55;
    internal byte ActiveSparkSoft {get;private set;}
    internal byte RequestedSparkSoft {get;private set;}
    internal byte SparkCancelLatch {get;private set;}
    internal float SparkSwitchTime {get;private set;}=BitConverter.Int32BitsToSingle(unchecked((int)0xBF800000));
    internal float FreeplayStartTime {get;private set;}=BitConverter.Int32BitsToSingle(unchecked((int)0xBF800000));
    private RetainedSubscription? _sparkInput;
    private readonly HashSet<string> _sparkMissing=new();
    // Lower native lookup results are explicit test inputs. Production never falls back
    // to Unity's authority-2 enum or Enum.ToString for either table.
    internal Func<int,string?>? SparkNameInput;
    internal Func<byte,string?>? SparkTagNameInput;
    private void SparkMissing(string text)=>SteppedBehavior.ReportMissing(text,_sparkMissing);

    // fidelity: M15-057
    internal void AttachSparkInput()
    {
        _sparkInput ??= _context.Robot.Engine.AnimationMessages.SubscribeMessage(SparkManagerMessage.UnionTag,false,
            message=>HandleSparkManagerMessage((SparkManagerMessage)message!));
    }

    // fidelity: M15-057
    internal void HandleSparkManagerMessage(SparkManagerMessage message)
    {
        switch(message.Tag)
        {
            case 0:
                SetNativeRequestedSpark(message.UnlockId,0);
                if(message.UnlockId==0x55)SparkCancelLatch=1; // never cleared by a noncancel request
                SparkMissing("M15-006/M15-059 ActivateSpark delivered to native manager fields; raw-ID Freeplay pick/switch and specialized ActivitySparked owner/lifecycle0x005B1518..0x005B20D6 are unbuilt. Existing string Freeplay seam is not substituted.");
                break;
            case 1: // no Unity sender; no public stack API exposes SparkUnlocked
                SetNativeRequestedSpark(message.UnlockId,1);
                break;
            case 2:
                SparkMissing("M15-057 case2 0x005A3CA0..0x005A3D5A: RequestATrick selector/configured weights and ProgressionUnlock.IsUnlocked owner are absent; no default unlocked/uniform RNG substituted. AddInventoryAmount(1,-3) is unconditional after selector but its native inventory recipient is absent; meta.spark_random_trick depends on selector result and is not fabricated.");
                break;
            case 3:
                SparkMissing("M15-057 case3 0x005A3DCA..0x005A3DD6: native game-request behavior+0x30/running byte input is unrepresented; no assumption that it is null and no synthetic +0x38 latch.");
                break;
            default:
                if(SparkTagNameInput is not {} tagName){SparkMissing("M15-057 bad-tag0x005A3D66: native BehaviorManagerMessageUnionTagToString input is unbuilt; UnhandledMessageUnionTag Error/errG cannot be completed with an invented name.");break;}
                _context.Robot.Engine.EmitSourceLog(EngineLogLevel.Error,null,"BehaviorManager.HandleEvent.UnhandledMessageUnionTag",$"Unexpected tag {message.Tag} '{tagName(message.Tag) ?? "(null)"}'");
                EngineErrorState.StoreAndMaybeBreak();
                break;
        }
    }

    // fidelity: M15-058
    internal void SetNativeRequestedSpark(int id,byte soft)
    {
        RequestedSparkSoft=soft;RequestedSparkId=id; // stores precede name lookup and Info
        if(SparkNameInput is not {} name){SparkMissing("M15-058 SetRequestedSpark0x005A3EC2: native UnlockId EnumToString table0x010340C0 is not built; field stores retained, setter Info withheld. UnityUnlockId is not a native lookup.");return;}
        _context.Robot.Engine.EmitSourceLog(EngineLogLevel.Info,"Behaviors","BehaviorManager.SetRequestedSpark",$"requested {(soft!=0?"soft":"hard")} spark is '{name(id) ?? "(null)"}'");
    }

    // fidelity: M15-058
    // Checked leaf, but the actual Freeplay pick-success join is still unbuilt.
    internal void SwitchToNativeRequestedSpark()
    {
        if(SparkNameInput is not {} name){SparkMissing("M15-058 SwitchToRequestedSpark0x005A41A8/0x005A41B0: native UnlockId names missing before Info/state writes; switch withheld. Its successful native Freeplay picker caller remains M15-006.");return;}
        _context.Robot.Engine.EmitSourceLog(EngineLogLevel.Info,"Behaviors","BehaviorManager.SwitchToRequestedSpark",
            $"switching active spark from '{name(ActiveSparkId) ?? "(null)"}' -> '{name(RequestedSparkId) ?? "(null)"}'. From {(ActiveSparkSoft!=0?"soft":"hard")} -> {(RequestedSparkSoft!=0?"soft":"hard")}");
        SparkSwitchTime=_context.Robot.Engine.Timer.SecondsF;
        SparkCancelLatch=0;ActiveSparkId=RequestedSparkId;ActiveSparkSoft=RequestedSparkSoft;
        // requested+60/+64 remain intact; no acknowledgment just because a request arrived.
    }
}
