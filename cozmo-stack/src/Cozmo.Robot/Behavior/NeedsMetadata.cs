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

// Source NumStarsToUnlock recipient; other reward fields are intentionally not represented here.
internal sealed class NativeStarThresholds
{
    private readonly int[] _levels;
    private NativeStarThresholds(int[] levels)=>_levels=levels;
    internal int GetMax(int level)=>_levels.Length==0?0:_levels[(uint)level<(uint)_levels.Length?level:_levels.Length-1];
    internal static NativeStarThresholds? Parse(string text)
    {
        try
        {
            if(!Json.TryParseFirst(Encoding.UTF8.GetBytes(text),out var document))
            {StackDiagnostics.Write("M15-014 StarRewardsConfig JSON parse failed; config unavailable");return null;}
            using(document)
            {
                var root=document.RootElement;var levels=Json.Member(root,"unlockLevels");
                if(levels.Kind!=FirmwareJsonKind.Array)
                {StackDiagnostics.Write("M15-014 StarRewardsConfig unlockLevels outside checked array input; config unavailable");return null;}
                var result=new List<int>();
                foreach(var entry in levels.Elements)
                {
                    if(entry.Kind!=FirmwareJsonKind.Object||!entry.Members.ContainsKey(Convert.ToHexString("numStarsToUnlock"u8)))
                    {StackDiagnostics.Write("M15-014 StarRewardsConfig missing numStarsToUnlock; config unavailable");return null;}
                    result.Add(Json.AsInt(Json.Member(entry,"numStarsToUnlock")));
                }
                return new(result.ToArray());
            }
        }
        catch(Exception e) when(e is JsonLogicError or JsonRuntimeError)
        {StackDiagnostics.Write("M1-034/M15-014 StarRewardsConfig terminating JsonCpp conversion; config unit skipped: "+e.Message);return null;}
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
