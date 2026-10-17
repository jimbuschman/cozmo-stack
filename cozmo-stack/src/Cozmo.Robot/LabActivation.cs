using System.Text;
using Cozmo.Transport;
using Cozmo.Robot.Behavior;
namespace Cozmo.Robot;

internal delegate void LabSourceLog(EngineLogLevel level,string? channel,string key,string text,IReadOnlyDictionary<string,string>? fields=null);

// fidelity: M15-052
internal readonly record struct LabActivation(byte Status,byte[] Variation);
internal sealed class LabMissingSource : Exception {}

/// <summary>Checked activation callers; M15-062 lookups and vector owners are explicit inputs.</summary>
internal sealed class LabActivationRuntime
{
    internal required Func<LabDefinition> Definition;
    internal required LabSourceLog Log;
    internal bool Enabled=true; // AnkiLab ctor7EBB60 stores1; disable path is unreachable (M15-049).
    internal object AssignmentGate {get;init;}=new(); // actual CozmoExperiments._gate in production
    internal Func<byte[],LabExperiment?>? FindExperiment;
    internal Func<int,byte[],byte[],LabAssignmentDef?>? FindAssignment;
    internal Func<LabExperiment,byte[],LabVariation?>? FindVariation;
    internal Func<byte,byte[]>? StatusName;
    internal Func<long> ClockMicroseconds=()=>DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()*1000;
    internal Func<byte[],uint>? ParseSystemDate;
    internal List<LabAssignmentDef>? Assignments40;
    internal LabAssignmentSignal? AssignmentSignal64;
    internal static void Missing(string text)
    {SteppedBehavior.ReportMissing("M15-052/M15-062 "+text);throw new LabMissingSource();}
    internal static string RawText(byte[] b)
    {
        try{return new UTF8Encoding(false,true).GetString(b);}
        catch(DecoderFallbackException){Missing("raw non-UTF8 printf/metadata byte recipient in Lab report; no replacement characters fabricated");throw;}
    }
    internal static string CString(string s){int zero=s.IndexOf('\0');return zero<0?s:s[..zero];}
    private static string Text(byte[] b)=>RawText(b.AsSpan(0,Array.IndexOf(b,(byte)0) is var n && n>=0?n:b.Length).ToArray());
    private void Info(string key,string text)=>Log(EngineLogLevel.Info,"Unnamed",key,text);
    private LabExperiment? Lookup(byte[] key)
    {if(FindExperiment is not {} f)Missing("FindExperiment PLT0x004CA6F4 in ActivateExperimentInternal0x007EC134; no guessed lookup");return FindExperiment!(key);}
    private LabAssignmentDef? Assignment(int offset,byte[] key,byte[] user)
    {if(FindAssignment is null)Missing($"FindAssignment PLT0x004CA700, lab+0x{offset:X} owner; no empty vector inferred");return FindAssignment!(offset,key,user);}
    private byte[] Name(byte status)
    {if(StatusName is null)Missing("AssignmentStatus EnumToString PLT0x004BDC98 at0x007ECFC8/0x007EC4CE input");return StatusName!(status);}
    internal uint? AutoActivate(byte[] user,IReadOnlyList<byte[]> tags)
    {
        try
        {
            var experiments=Definition().Experiments;if(experiments is null)Missing("definition vector+34 default producer before0x007ECF58");
            uint count=0;
            foreach(var e in experiments!)
            {
                if(e.ActivationMode is null)Missing("experiment activation-mode byte+10 constructor default");
                if(e.ActivationMode!=1)continue;
                if(e.Key is null)Missing("experiment key+4 constructor default");
                uint now=unchecked((uint)(ClockMicroseconds()/1000000));
                var result=Activate(e.Key!,user,tags,now);if(result is null)return null;
                Info("AnkiLab.AutoActivateExperimentsForUser",$"{Text(e.Key!)} : {Text(result.Value.Variation)} : {Text(Name(result.Value.Status))}");
                count=unchecked(count+1);
            }
            return count;
        }
        catch(LabMissingSource){return null;}
    }
    internal LabActivation? Activate(byte[] key,byte[] user,IReadOnlyList<byte[]> tags,uint now)
    {
        try{var result=ActivateInternal(key,user,tags,now);Report(new(key.ToArray(),user.ToArray(),result.Variation.ToArray()),result.Status);return result;}
        catch(LabMissingSource){return null;}
    }
    internal LabActivation ActivateInternal(byte[] key,byte[] user,IReadOnlyList<byte[]> tags,uint now)
    {
        // Output variation is cleared before the first lookup.
        var empty=Array.Empty<byte>();var experiment=Lookup(key);if(experiment is null)return new(1,empty);
        var a=Assignment(0x58,key,user);if(a is not null)return new(8,a.Variation.ToArray());
        if(!IsRunning(experiment,now))return new(3,empty);
        a=Assignment(0x40,key,user);if(a is not null)return new(6,a.Variation.ToArray());
        if(!MatchesAudience(experiment,tags))return new(4,empty);
        a=Assignment(0x4C,key,user);if(a is not null)return new(7,a.Variation.ToArray());
        if(!Enabled){Info("AnkiLab.ActivateExperiment.LabIsDisabled","");return new(5,empty);}
        byte bucket=HashBucket(key,user);
        Info("AnkiLab.ActivateExperiment.bucket",$"{Text(key)} : {bucket}");
        var variation=SelectVariation(experiment,bucket);
        if(variation is null)return new(5,empty);
        Info("AnkiLab.ActivateExperiment",$"{Text(key)} : {bucket} : {Text(variation)}");
        Assign(new(key.ToArray(),user.ToArray(),variation.ToArray()));
        return new(6,variation.ToArray());
    }
    internal void Assign(LabAssignmentDef assignment)
    {
        // Lookup is a required native recipient, distinct from app-side collection conventions.
        var old=Assignment(0x40,assignment.Key,assignment.User);
        lock(AssignmentGate)
        {
            if(Assignments40 is null)Missing("lab+40 assignment vector storage owner0x007ECEF8..0x007ECF4A");
            if(old is null)Assignments40!.Add(assignment);
            else
            {
                int index=Assignments40!.FindIndex(entry=>ReferenceEquals(entry,old));
                if(index<0)Missing("FindAssignment returned an entry outside explicit lab+40 vector");
                Assignments40[index]=old with{Variation=assignment.Variation.ToArray()};
            }
        }
        if(AssignmentSignal64 is null)Missing("observer chain owner+64 at0x007ECF32; assignment retained");
        if(AssignmentSignal64!.Head is not null)AssignmentSignal64.Dispatch(Assignments40!);
    }
    internal bool IsRunning(LabExperiment e,uint now)
    {
        uint start=Date(e.Start),stop=Date(e.Stop),pause=Date(e.Pause),resume=Date(e.Resume);
        return start<=now && unchecked(stop-1)>=now && (unchecked(pause-1)>=now || unchecked(resume-1)<now);
    }
    private uint Date(byte[]? value)
    {
        if(value is null)Missing("date-string constructor default before0x007EDC88");
        if(value!.Length==0)return 0;
        if(ParseSystemDate is null)Missing("system strptime/timegm recipient0x00831C68..0x00831CAC, format %Y-%m-%dT%T; no guessed parser");
        return ParseSystemDate!(value);
    }
    internal static bool MatchesAudience(LabExperiment e,IReadOnlyList<byte[]> supplied)
    {
        if(e.AudienceTags is null)Missing("required-tag vector constructor default+48 at0x007EDDB4");
        if(e.AudienceTags!.Count==0)return true;
        var comparer=Comparer<byte[]>.Create((a,b)=>a.AsSpan().SequenceCompareTo(b));
        var required=e.AudienceTags.OrderBy(x=>x,comparer).ToArray();var have=supplied.OrderBy(x=>x,comparer).ToArray();
        int i=0,j=0,count=0;
        while(i<required.Length&&j<have.Length)
        {int c=comparer.Compare(required[i],have[j]);if(c<0)i++;else if(c>0)j++;else{count++;i++;j++;}}
        return count==required.Length;
    }
    internal byte[]? SelectVariation(LabExperiment e,byte bucket)
    {
        if(e.Population is null||e.Variations is null||e.Key is null)Missing("variation definition constructor defaults0x007EE0FC");
        Info("AnkiLab.GetExperimentVariation.bucket",$"{Text(e.Key!)} : {bucket}");
        int sum=0;
        foreach(var v in e.Variations!)
        {
            if(v.Population is null||v.Key is null)Missing("variation key/fraction constructor defaults+0/+C");
            int fraction=v.Population!.Value;if(fraction==0)continue;
            uint n=(uint)((float)(e.Population!.Value*fraction)/100f);
            byte start=unchecked((byte)sum);
            if(n!=0)
            {
                byte end=unchecked((byte)(start+255+n)),nominal=unchecked((byte)(start+fraction-1));
                Info("AnkiLab.GetExperimentVariation.bucket.range",$"|[{start,2} - {end,2}] - {nominal,2}| : {Text(v.Key!)}");
                if(bucket>=start&&bucket<=end)
                {Info("AnkiLab.GetExperimentVariation.bucket.assign",$"{Text(e.Key!)} : {bucket} : {Text(v.Key!)}");return v.Key!.ToArray();}
            }
            sum=start+fraction;
        }
        return null;
    }
    internal byte HashBucket(byte[] key,byte[] user)
    {
        if(key.Length==0)AssertEmpty("experimentKey","!experimentKey.empty()");
        if(user.Length==0)AssertEmpty("userId","!userId.empty()");
        ushort crc=0xFFFF;
        foreach(byte value in key.Concat(user))
        {
            uint t=(uint)((crc>>8)^value)<<8;
            for(int bit=0;bit<8;bit++)t=(t&0x8000)!=0?(t<<1)^0x1021:t<<1;
            crc=unchecked((ushort)((crc<<8)^t));
        }
        float divisor=BitConverter.UInt32BitsToSingle(0x4423D70A);
        uint bucket=(uint)MathF.Floor((float)crc/divisor);
        return (byte)Math.Min(bucket,255u);
    }
    internal Action? AssertCallstackAndFlush;
    internal Action? AssertLogFlush;
    private void AssertEmpty(string name,string expression)
    {
        Log(EngineLogLevel.Error,null,$"CalculateExperimentHashBucket: {name} can not be empty",$"Assertion Failed: {expression}");EngineErrorState.StoreAndMaybeBreak();
        // sDumpCallstack target0x0080B0A6 is bx lr. sLogFlush follows at7EE468/7EE4F8;
        // both are defined, non-aborting calls. No synthetic host stack trace is emitted.
        if(AssertCallstackAndFlush is {} call)call();
        else if(AssertLogFlush is {} flush)flush();
        else SteppedBehavior.ReportMissing("M15-052 sLogFlush recipient after empty-hash assertions0x007EE468/0x007EE4F8 ->0x0080DA7C is unbound; hash continues");
    }
    internal void Report(LabAssignmentDef assignment,byte status)
    {
        var d=Definition();if(d.ProjectId is null||d.Version is null||d.Revision is null)Missing("report project/version/revision constructor defaults0x007EC4C0");
        byte[] lowered=Name(status).Select(b=>b is >=65 and <=90?(byte)(b+32):b).ToArray();
        string prefix=$"{RawText(d.ProjectId!)}-{d.Version}.{d.Revision}",name=RawText(lowered),key=Text(assignment.Key),variation=Text(assignment.Variation),user=Text(assignment.User);
        string data,eventKey,group=variation;
        if(status<=2){data=prefix+":"+name;eventKey="experiment.invalid";}
        else
        {
            var experiment=Lookup(assignment.Key);
            LabVariation? resolved=null;
            if(experiment is not null)
            {
                if(FindVariation is null)Missing("FindVariation PLT0x004CA76C in report0x007EC4C0");
                resolved=FindVariation!(experiment,assignment.Variation);
            }
            if(status>5&&resolved is null)
            {StackDiagnostics.Write("M1-034 M15-052: skip ReportExperimentAssignmentResult null variation dereference0x007ECA9C; assignment retained");return;}
            string resolvedName=resolved?.Key is {} bytes?Text(bytes):"";
            data=$"{prefix}:{RawText(assignment.Key)}:{(resolved?.Key is {} raw?RawText(raw):"")}:{name}";eventKey=status<=5?"experiment.unassigned":"experiment.assigned";
            if(status>5)group=resolvedName;
        }
        data=CString(data);
        Log(status<=2?EngineLogLevel.Warning:EngineLogLevel.Event,null,eventKey,key,new Dictionary<string,string>{{"$user",user},{"$group",group},{"$data",data}});
        Log(EngineLogLevel.Debug,"Unnamed","AnkiLab."+eventKey,$"{key}:{variation}:{user} - {data}");
    }
}
