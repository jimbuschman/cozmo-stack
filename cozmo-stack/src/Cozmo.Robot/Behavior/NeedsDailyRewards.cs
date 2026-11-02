using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using Cozmo.Robot.Animation;

namespace Cozmo.Robot.Behavior;

public sealed record StarUnlockedReport(int Level,int Maximum,int Stars);
public sealed record NeedsRewardReport(int Type,string Data,bool InventoryIsFull);
public sealed record StarLevelCompletedReport(int Level,int Maximum,IReadOnlyList<NeedsRewardReport> Rewards);

// fidelity: M15-093, M15-071 — finite daily and weighted producer on the retained Needs owner.
public sealed partial class NeedsManager
{
    private bool NativeRewardContextCurrent(CozmoEngine engine)=>engine.NeedsContextAlive&&ReferenceEquals(engine.Needs,this);
    // Needs+4 is loaded where the source uses a recipient (0x00697118, 0x00699468, 0x006994FC); no other owner is consulted.
    private EngineRobot? RewardRobot(string recipient)
    {
        var owner=_nativeConnectedRobot;
        if(owner is not null&&!owner.Lifetime.Destroying)return owner;
        SteppedBehavior.ReportMissing("M1-034/M15-093 skipped null or retired Needs+4 robot dereference before "+recipient);return null;
    }
    private InventoryOwner? RewardInventory()
    {
        if(RewardRobot("Inventory")?.Inventory is {} inventory)return inventory;
        SteppedBehavior.ReportMissing("M15-093/M15-071 original Needs robot inventory owner unavailable");return null;
    }
    private ProgressionUnlockOwner? RewardProgression()
    {
        if(RewardRobot("Progression")?.Progression is {} progression)return progression;
        SteppedBehavior.ReportMissing("M15-093 original Needs robot progression owner unavailable");return null;
    }
    private bool TryLevel(int level,out NativeStarLevel value)
    {
        if(_nativeStarThresholds is not null&&_nativeStarThresholds.TryGetLevel(level,out var found)&&found is not null){value=found;return true;}
        value=null!;SteppedBehavior.ReportMissing(_nativeStarThresholds is {Count:0}?"M1-034/M15-093 skipped empty StarRewardsConfig invalid level access at 0x0068E7A4":"M15-093 StarRewardsConfig level input unavailable at 0x0068E7A4");return false;
    }
    private bool RewardRandomKnown(CozmoEngine engine)
    {
        if(engine.NativeLocalNotifications?.RandomHistoryKnown==true)return true;
        SteppedBehavior.ReportMissing("M15-093/M15-071 shared RNG history unavailable before reward draw");return false;
    }
    internal static byte[] PackStarUnlocked(int level,int maximum,int stars)
    {
        byte[] body=new byte[12];BinaryPrimitives.WriteInt32LittleEndian(body,level);BinaryPrimitives.WriteInt32LittleEndian(body.AsSpan(4),maximum);BinaryPrimitives.WriteInt32LittleEndian(body.AsSpan(8),stars);return body;
    }
    internal static byte[] PackStarCompleted(int level,int maximum,IReadOnlyList<NativeNeedsReward> rewards)
    {
        using var stream=new MemoryStream();
        void WriteWord(int value){Span<byte> b=stackalloc byte[4];BinaryPrimitives.WriteInt32LittleEndian(b,value);stream.Write(b);}
        WriteWord(level);WriteWord(maximum);stream.WriteByte(unchecked((byte)rewards.Count));
        foreach(var reward in rewards){WriteWord(reward.Type);int length=unchecked((byte)reward.Data.Length);stream.WriteByte((byte)length);stream.Write(reward.Data,0,length);stream.WriteByte(reward.InventoryIsFull?(byte)1:(byte)0);}
        return stream.ToArray();
    }
    private bool SendStarUnlocked(CozmoEngine engine)
    {
        var report=new StarUnlockedReport(State.NativeUnlockLevel,State.NativeStarsForNextUnlock,State.NativeStarsAwarded);
        if(!engine.NeedsContextExternalInterfaceAvailable)
        {SteppedBehavior.ReportMissing("M1-034/M15-093 skipped StarUnlocked null interface dereference at 0x006986F6");return NativeRewardContextCurrent(engine);}
        engine.DeliverNurture(0x67,PackStarUnlocked(report.Level,report.Maximum,report.Stars),report);return NativeRewardContextCurrent(engine);
    }
    private bool SendStarCompleted(CozmoEngine engine,List<NativeNeedsReward> rewards)
    {
        if(!engine.NeedsContextExternalInterfaceAvailable)
        {SteppedBehavior.ReportMissing("M1-034/M15-093 skipped StarLevelCompleted null interface dereference at 0x006987C2");return NativeRewardContextCurrent(engine);}
        var report=new StarLevelCompletedReport(State.NativeUnlockLevel,State.NativeStarsForNextUnlock,rewards.Select(r=>new NeedsRewardReport(r.Type,Encoding.UTF8.GetString(r.Data),r.InventoryIsFull)).ToArray());
        engine.DeliverNurture(0x66,PackStarCompleted(report.Level,report.Maximum,rewards),report);return NativeRewardContextCurrent(engine);
    }
    // 0x00697118..0x00697184; the single inventory read also serves Add, because the draw has no callback.
    private bool TryAward(CozmoEngine engine,int target,float lowPct,float highPct,int lowMinimum,int highMinimum,out int raw)
    {
        raw=0;if(RewardInventory() is not {} inventory)return false;
        int current=inventory.GetAmount(1);float difference=(float)unchecked(target-current);
        int low=Math.Max(AnimationScheduler.VcvtS32(difference*lowPct+0.5f),lowMinimum);
        int high=Math.Max(AnimationScheduler.VcvtS32(difference*highPct+0.5f),highMinimum);
        if(!RewardRandomKnown(engine))return false; // first actual draw: the known prefix stays applied
        raw=engine.ContextRandom.RandIntInRange(low,high);inventory.Add(1,raw);return NativeRewardContextCurrent(engine);
    }
    // 0x006993F8: `index` is the caller's level for the descriptor vector and all five star scalars (0x00699416..0x006994B6).
    internal bool ProcessNativeLevel(CozmoEngine engine,int index,bool prior,ref int counter,List<NativeNeedsReward> output)
    {
        if(_nativeStarThresholds is {Count:0})return true; // GetRewardsForLevel leaves the caller's empty vector unchanged.
        if(!TryLevel(index,out var source))return false;
        var rewards=source.Rewards.Select(r=>new NativeNeedsReward(r.Type,r.Data.ToArray(),r.InventoryIsFull)).ToArray();
        foreach(var reward in rewards)
        {
            if(!NativeRewardContextCurrent(engine))return false;
            if(reward.Type==3){output.Add(reward);continue;}
            if(reward.Type is <0 or >3)continue;
            if(reward.Type==0)
            {
                if(prior)continue;
                if(RewardInventory() is not {} inventory)return false;
                int space=inventory.GetSpace(1);
                if(!TryAward(engine,source.Target,source.MinimumPct,source.MaximumPct,source.Minimum,source.MinimumMaximum,out int raw))return false;
                output.Add(new(0,Encoding.ASCII.GetBytes(raw.ToString(CultureInfo.InvariantCulture)),space!=-1&&space<raw));
            }
            else
            {
                uint id=NativeUnlockNames.FromBytes(reward.Data);if(id==0)continue; // 0x006994F2..0x006994FA, before Progression
                if(RewardProgression() is not {} progression)return false;
                if(!progression.TryIsUnlocked(id,out bool unlocked)){SteppedBehavior.ReportMissing("M15-095 unlock override input unavailable");return false;}
                if(unlocked)continue;progression.SetUnlock(id,true);if(!NativeRewardContextCurrent(engine))return false;
                output.Add(reward);if(reward.Type==2)State.ForceNextSong=unchecked((int)id);if(prior)counter=unchecked(counter-1);
            }
        }
        return true;
    }
    private bool CompleteNativeStarLevel(CozmoEngine engine)
    {
        if(_nativeStarThresholds is {Count:0})
        {SteppedBehavior.ReportMissing("M1-034/M15-093 skipped level completion invalid empty-config level access at 0x0068E7A4");return NativeRewardContextCurrent(engine);}
        if(!TryLevel(State.NativeUnlockLevel,out var level))return false;
        int counter=level.PriorLimit;var rewards=new List<NativeNeedsReward>();
        for(int i=0;i<State.NativeUnlockLevel&&counter>=1;i++)if(!ProcessNativeLevel(engine,i,true,ref counter,rewards))return false;
        if(!ProcessNativeLevel(engine,State.NativeUnlockLevel,false,ref counter,rewards))return false;
        State.NativeUnlockLevel=unchecked(State.NativeUnlockLevel+1);State.NativeStarsAwarded=0;
        State.NativeStarsForNextUnlock=_nativeStarThresholds!.GetMax(State.NativeUnlockLevel);
        return SendStarCompleted(engine,rewards);
    }
    // Returns null when a required source input is unavailable; 0/1 are native results.
    private bool _nativeDailyHistoryKnown=true;
    internal int? UpdateNativeStars(bool force)
    {
        var engine=HostEngine;
        if(engine is null||!NativeRewardContextCurrent(engine))return null;
        if(!State.NativePreviousBrackets.TryGetValue(NeedId.Play,out var previous)){previous=0;State.NativePreviousBrackets[NeedId.Play]=previous;}
        if(previous==0){if(!force)return 0;} // 0x00696AD6, 0x00696AEA..0x00696AEC: a forced call goes to the calendar with no bracket refresh
        else
        {
            var current=State.GetNeedBracket(NeedId.Play);if(!NativeRewardContextCurrent(engine))return null;
            if(!force&&current!=0)return 0;
        }
        if(!_nativeDailyHistoryKnown){SteppedBehavior.ReportMissing("M15-093 earlier daily-star source effects remain unavailable");return null;}
        int? result=UpdateNativeStarDay(engine);
        if(result is null&&NativeRewardContextCurrent(engine))_nativeDailyHistoryKnown=false;
        return result;
    }
    private int? UpdateNativeStarDay(CozmoEngine engine)
    {
        if(!NativeNotificationCalendar.TryLocalDay(State.NativeLastStarMicros/1_000_000L,out int oldYear,out int oldDay))
        {SteppedBehavior.ReportMissing("M15-093 stored last-star local calendar unavailable");return null;}
        long now=NativeSystemClockMicros();if(!NativeRewardContextCurrent(engine))return null;
        if(!NativeNotificationCalendar.TryLocalDay(now/1_000_000L,out int newYear,out int newDay))
        {SteppedBehavior.ReportMissing("M15-093 current local calendar unavailable");return null;}
        engine.EmitSourceLog(EngineLogLevel.Info,"NeedsSystem","NeedsManager.UpdateStarsState","Checking star day");if(!NativeRewardContextCurrent(engine))return null;
        if(oldYear==newYear&&oldDay==newDay)return 0;
        engine.EmitSourceLog(EngineLogLevel.Info,"NeedsSystem","NeedsManager.UpdateStarsState.NewDay","Awarding daily star");if(!NativeRewardContextCurrent(engine))return null;
        State.NativeLastStarMicros=now;State.NativeStarsAwarded=unchecked(State.NativeStarsAwarded+1);if(!SendStarUnlocked(engine))return null;
        if(State.NativeStarsAwarded>=State.NativeStarsForNextUnlock&&!CompleteNativeStarLevel(engine))return null;
        _=PossiblyStartNativeRobotWrite(true);if(!NativeRewardContextCurrent(engine))return null; // 0x00696BE2..0x00696BEA: no result test before Generate
        if(TryLocalNotificationsGenerate is {} generate)_=generate();
        else {engine.NativeLocalNotifications?.MarkHistoryUnavailable();SteppedBehavior.ReportMissing("M15-093 LocalNotifications.Generate unavailable after daily star");}
        return NativeRewardContextCurrent(engine)?1:null;
    }
    internal bool? TryNativeFreeplayReward(CozmoEngine engine,NeedsActionId action,bool notificationKnown)
    {
        if(RewardInventory() is not {} inventory)return null;
        int amount=inventory.GetAmount(1);
        if(_nativeStarThresholds is {Count:0})
        {SteppedBehavior.ReportMissing("M1-034/M15-071 skipped freeplay gate invalid empty-config level access at 0x0068E7A4");return false;}
        if(!TryLevel(State.NativeUnlockLevel,out var level))return null; // 0x00696D8A captures the level once
        float chance=1f-(float)amount/(float)level.FreeplayTarget;if(chance<level.FreeplayChance)chance=level.FreeplayChance;
        if(!notificationKnown||!RewardRandomKnown(engine))return null; // first actual draw
        if(!(engine.ContextRandom.RandDbl(1.0)<(double)chance))return false;
        if(!TryLevel(State.NativeUnlockLevel,out level))return null; // 0x00696E0A captures the level once
        if(!TryAward(engine,level.FreeplayTarget,level.FreeplayMinimumPct,level.FreeplayMaximumPct,level.FreeplayMinimum,level.FreeplayMinimumMaximum,out int raw))return null;
        if(!_nativePendingKnown){SteppedBehavior.ReportMissing("M15-071 previous pending reward unavailable after inventory award");return null;}
        if(_sparksRewardPending){CommunicateNativeRewardContext(engine);if(!NativeRewardContextCurrent(engine))return null;}
        _nativeRewardAmount=raw;_sparksRewardPending=true;_nativePendingKnown=true;_nativeRewardPayloadKnown=true;
        _nativeRewardDisplayKey="needs.FreeplaySparksReward."+action;Interlocked.Increment(ref _nativePendingHistoryEpoch);
        return true;
    }
}
