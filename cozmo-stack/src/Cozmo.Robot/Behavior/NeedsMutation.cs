using Cozmo.Robot.Animation;

namespace Cozmo.Robot.Behavior;

// fidelity: M15-029, M15-046
public sealed partial class NeedsState
{
    private readonly SortedDictionary<int,byte> _partDamage=new(){{0,0},{1,0},{2,0}};
    private readonly SortedDictionary<int,float> _extraNeedLevels=new();
    internal IReadOnlyDictionary<int,byte> PartDamage=>_partDamage;
    internal void SetPartDamageForTest(int part,byte value)=>_partDamage[part]=value;
    public EngineRandom? ContextRandom {get;set;}
    internal Action<string,IReadOnlyDictionary<string,string>>? PartDamageLog;
    private static readonly float RepairEpsilon=BitConverter.Int32BitsToSingle(0x3727C5AC);

    internal void DamagePartsNative(NeedsActionId action)
    {
        int initial=NumDamagedParts(),total=_partDamage.Count;
        if(initial>=total)return;
        int target=Math.Min(NumDamagedPartsForRepairLevel((float)GetNeedLevel(NeedId.Repair)),total);
        int additional=target-initial;if(additional<1)return;
        if(ContextRandom is not {} rng){SteppedBehavior.ReportMissing("M15-029 PossiblyDamageParts 0x0069CCDE: supplied context RNG is absent; no independent seed, fabricated draw or part mutation");return;}
        for(int i=0;i<additional;i++)
        {
            int draw=rng.RandInt(_partDamage.Count-NumDamagedParts()); // two words, including range one.
            int ordinal=0;
            foreach(var entry in _partDamage)
            {
                if(entry.Value==0){if(draw==0)break;draw--;}
                ordinal++;
            }
            _partDamage[ordinal]=1; // ordinal is used as key; the endpoint can insert key3, with no clamp/retry.
            if((uint)action>0x56 || Enum.GetName(action) is not {} actionName)
            {
                StackDiagnostics.Write("M1-034/M15-029: skipping part-damage event after mutation; invalid action name reaches strlen at 0x0069C6B6");continue;
            }
            string? part=ordinal switch{0=>"Head",1=>"Lift",2=>"Treads",_=>null};
            if(part is null)
            {
                SteppedBehavior.ReportMissing("M15-029 needs.part_damaged 0x0080D114: EnumToString(3) is null; concrete logger null-value recipient is unread. Invalid part-map insertion is preserved, not retried");continue;
            }
            string data=(initial+1+i).ToString(System.Globalization.CultureInfo.InvariantCulture)+":"+actionName;
            if(PartDamageLog is {} log)log(part,new Dictionary<string,string>{{"$data",data}});
            else SteppedBehavior.ReportMissing("M15-029 needs.part_damaged at 0x0069C736 has no structured logger recipient; mutation/draws retained, no fabricated log without its $data");
        }
    }

    public bool ApplyDelta(NeedId need,float center,float variation,NeedsActionId action)
    {
        float old;
        if((uint)need<3)old=(float)_levels[(int)need];
        else if(!_extraNeedLevels.TryGetValue((int)need,out old))_extraNeedLevels[(int)need]=old=0;
        if(ContextRandom is not {} rng){SteppedBehavior.ReportMissing("M15-046 ApplyDelta 0x0069C91A requires supplied context RNG even for zero variation; mutation skipped, no host Random draw");return false;}
        float width=variation+variation;
        double offset=rng.RandDbl((double)width)-(double)variation;
        float delta=center+(float)offset,candidate=old+delta;
        float minimum=(float)_cfg.MinimumNeedLevel,maximum=(float)_cfg.MaximumNeedLevel;
        if(candidate<=minimum)candidate=minimum;else if(candidate>=maximum)candidate=maximum;
        bool full=false;
        // OV28 correction: Full lookup, Energy snap and Repair constraints run only on ordered positive delta.
        if(delta>0)
        {
            if(!_cfg.Brackets.TryGetValue(need,out var brackets))
            {StackDiagnostics.Write("M1-034/M15-046: skipping ApplyDelta missing threshold map at 0x0069C9AE..0x0069C9B4 after RNG consumption; prior level retained");return false;}
            float fullThreshold=(float)brackets.Full;
            full=candidate>=fullThreshold;
            if(need==NeedId.Energy && candidate>=fullThreshold && old<fullThreshold)candidate=maximum;
            if(need==NeedId.Repair)
            {
                int damaged=NumDamagedParts(),count=_cfg.BrokenPartThresholds.Count;
                if(count==0&&damaged>=count)
                {StackDiagnostics.Write("M1-034/M15-046: skipping empty Repair threshold vector read in 0x0069C9D0..0x0069CA7E; prior level retained");return false;}
                float cap=damaged>=count?(float)_cfg.BrokenPartThresholds[count-1]:damaged>0?(float)_cfg.BrokenPartThresholds[damaged-1]:maximum;
                if(candidate>cap)candidate=cap-RepairEpsilon;
                float floor=damaged<count?(float)_cfg.BrokenPartThresholds[damaged]:minimum;
                if(floor>candidate)candidate=floor+RepairEpsilon;
            }
        }
        if((uint)need<3)_levels[(int)need]=candidate;else _extraNeedLevels[(int)need]=candidate;
        _bracketsDirty=true;
        if(delta<0&&need==NeedId.Repair)DamagePartsNative(action);
        return full;
    }
}
