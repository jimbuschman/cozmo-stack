using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using Cozmo.Transport;

namespace Cozmo.Robot.Behavior;

// M15-014/M15-017: these are fields of the existing context NeedsState, not an additional reward state.
public sealed partial class NeedsState
{
    internal long NativeDateTimeMicros,NativeTimeCreatedMicros,NativeLastDisconnectMicros,NativeLastBackgroundMicros,NativeLastStarMicros,NativeAppOpenMicros;
    internal int NativeOpenAppAfterDisconnect,NativeUnlockLevel,NativeStarsAwarded,NativeStarsForNextUnlock;
    internal uint NativeSerial;
    internal readonly Dictionary<NeedId,NeedBracketId> NativePreviousBrackets=new();
    internal void SetNativePartDamage(int part,byte value)=>_partDamage[part]=value;
    internal IReadOnlyDictionary<int,float> NativeLevels=>_levels;
    internal void CopyNativeFrom(NeedsState source)
    {
        if(ReferenceEquals(this,source))return;
        NativeDateTimeMicros=source.NativeDateTimeMicros;NativeTimeCreatedMicros=source.NativeTimeCreatedMicros;
        NativeLastDisconnectMicros=source.NativeLastDisconnectMicros;NativeLastBackgroundMicros=source.NativeLastBackgroundMicros;
        NativeLastStarMicros=source.NativeLastStarMicros;NativeAppOpenMicros=source.NativeAppOpenMicros;NativeOpenAppAfterDisconnect=source.NativeOpenAppAfterDisconnect;
        NativeUnlockLevel=source.NativeUnlockLevel;NativeStarsAwarded=source.NativeStarsAwarded;NativeStarsForNextUnlock=source.NativeStarsForNextUnlock;
        NativeSerial=source.NativeSerial;ForceNextSong=source.ForceNextSong;ContextRandom=source.ContextRandom;
        _levels.Clear();foreach(var p in source._levels)_levels.Add(p.Key,p.Value);
        _partDamage.Clear();foreach(var p in source._partDamage)_partDamage.Add(p.Key,p.Value);
        _curBrackets.Clear();foreach(var p in source._curBrackets)_curBrackets.Add(p.Key,p.Value);
        NativePreviousBrackets.Clear();foreach(var p in source.NativePreviousBrackets)NativePreviousBrackets.Add(p.Key,p.Value);
        _bracketsDirty=source._bracketsDirty;
    }
}

internal sealed record NativeNeedsReward(int Type,byte[] Data,bool InventoryIsFull);
internal sealed record NativeStarLevel(int? Stars,int? Target,int? PriorLimit,float? MinimumPct,float? MaximumPct,int? Minimum,int? MinimumMaximum,int? FreeplayTarget,float? FreeplayChance,float? FreeplayMinimumPct,float? FreeplayMaximumPct,int? FreeplayMinimum,int? FreeplayMinimumMaximum,IReadOnlyList<NativeNeedsReward> Rewards);

// One retained source-owned StarRewardsConfig is shared by metadata and reward consumers.
internal sealed class NativeStarThresholds
{
    private readonly NativeStarLevel[] _levels;
    private NativeStarThresholds(NativeStarLevel[] levels)=>_levels=levels;
    internal int Count=>_levels.Length;
    internal bool TryGetMax(int level,out int maximum)
    {
        if(_levels.Length==0){maximum=0;return true;}
        int? value=_levels[(uint)level<(uint)_levels.Length?level:_levels.Length-1].Stars;
        maximum=value.GetValueOrDefault();return value.HasValue;
    }
    internal int GetMax(int level)=>TryGetMax(level,out int maximum)?maximum:throw new JsonMissingSource("StarRewardsConfig max-stars scalar unavailable");
    internal bool TryGetLevel(int level,out NativeStarLevel? value)
    {value=_levels.Length==0?null:_levels[(uint)level<(uint)_levels.Length?level:_levels.Length-1];return value is not null;}
    internal static NativeStarThresholds? Parse(string text)
    {
        var result=new List<NativeStarLevel>();
        try
        {
            if(!Json.TryParseFirst(Encoding.UTF8.GetBytes(text),out var document))
            {StackDiagnostics.Write("M15-014 StarRewardsConfig JSON parse failed; config unavailable");return null;}
            using(document)
            {
                var root=document.RootElement;var levels=Json.Member(root,"unlockLevels");
                if(levels.Kind!=FirmwareJsonKind.Array)
                {StackDiagnostics.Write("M15-014 StarRewardsConfig unlockLevels outside checked array input; config unavailable");return null;}
                foreach(var entry in levels.Elements)
                {
                    if(entry.Kind!=FirmwareJsonKind.Object)
                    {StackDiagnostics.Write("M15-014 StarRewardsConfig level entry unavailable; earlier levels retained");break;}
                    bool Has(string key)=>entry.Members.ContainsKey(Convert.ToHexString(Encoding.UTF8.GetBytes(key)));
                    int? I(string key)=>Has(key)?Json.AsInt(Json.Member(entry,key)):null;
                    float? F(string key)=>Has(key)?Json.AsFloat(Json.Member(entry,key)):null;
                    var rewards=new List<NativeNeedsReward>();
                    if(Has("rewards"))
                    {
                        var array=Json.Member(entry,"rewards");
                        for(uint i=0;i<Json.Size(array);i++)
                        {
                            var item=Json.Index(array,i);bool R(string key)=>item.Kind==FirmwareJsonKind.Object&&item.Members.ContainsKey(Convert.ToHexString(Encoding.UTF8.GetBytes(key)));
                            int type=0;byte[] data=Array.Empty<byte>();bool full=false;
                            if(R("rewardType"))type=Encoding.UTF8.GetString(Json.AsStringBytes(Json.Member(item,"rewardType"))) switch{"Sparks"=>0,"Unlock"=>1,"Song"=>2,"MemoryBadge"=>3,_=>0};
                            if(R("data"))data=Json.AsStringBytes(Json.Member(item,"data")).ToArray();
                            if(R("inventoryIsFull")){var v=Json.Member(item,"inventoryIsFull");full=v.Kind switch{FirmwareJsonKind.Boolean=>v.Boolean,FirmwareJsonKind.Null=>false,FirmwareJsonKind.Int=>v.Signed!=0,FirmwareJsonKind.UInt=>v.Unsigned!=0,FirmwareJsonKind.Real=>v.Real!=0,_=>throw new JsonLogicError("Value is not convertible to bool")};}
                            rewards.Add(new(type,data,full));
                        }
                    }
                    result.Add(new(I("numStarsToUnlock"),I("targetSparksTotal"),I("maxPriorLevelUnlocks"),F("minSparksPct"),F("maxSparksPct"),I("minSparks"),I("minMaxSparks"),I("freeplayTargetSparksTotal"),F("freeplayMinSparksRewardPct"),F("freeplayMinSparksPct"),F("freeplayMaxSparksPct"),I("freeplayMinSparks"),I("freeplayMinMaxSparks"),rewards));
                }
                return new(result.ToArray());
            }
        }
        catch(JsonMissingSource e)
        {StackDiagnostics.Write("M15-014 StarRewardsConfig conversion unavailable; earlier levels retained: "+e.Message);return result.Count==0?null:new(result.ToArray());}
        catch(Exception e) when(e is JsonLogicError or JsonRuntimeError)
        {StackDiagnostics.Write("M1-034/M15-014 StarRewardsConfig terminating JsonCpp conversion; current level skipped, earlier levels retained: "+e.Message);return result.Count==0?null:new(result.ToArray());}
    }
}

public sealed record NativeNeedsStateReport(uint[] Levels,int[] Brackets,bool[] Parts,int UnlockLevel,int Stars,int NextStars,int Action,byte[] Variation,int RepairRounds);
internal static class NativeNeedsStateReportCodec
{
    // 0x7169A4: vector counts, raw words and bytes; pstring8 writes its low-byte length.
    internal static byte[] Pack(NativeNeedsStateReport value)
    {
        using var stream=new MemoryStream();
        void Word(uint v){Span<byte> b=stackalloc byte[4];BinaryPrimitives.WriteUInt32LittleEndian(b,v);stream.Write(b);}
        stream.WriteByte(unchecked((byte)value.Levels.Length));foreach(uint x in value.Levels)Word(x);
        stream.WriteByte(unchecked((byte)value.Brackets.Length));foreach(int x in value.Brackets)Word(unchecked((uint)x));
        stream.WriteByte(unchecked((byte)value.Parts.Length));foreach(bool x in value.Parts)stream.WriteByte(x?(byte)1:(byte)0);
        Word(unchecked((uint)value.UnlockLevel));Word(unchecked((uint)value.Stars));Word(unchecked((uint)value.NextStars));Word(unchecked((uint)value.Action));
        int length=unchecked((byte)value.Variation.Length);stream.WriteByte((byte)length);stream.Write(value.Variation,0,length);
        Word(unchecked((uint)value.RepairRounds));return stream.ToArray();
    }
    internal static readonly byte[] InitialVariation=Encoding.ASCII.GetBytes("preholiday_tuning: Unknown (unknown)");
}
