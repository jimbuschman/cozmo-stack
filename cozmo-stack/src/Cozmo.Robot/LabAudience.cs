using System.Text;
namespace Cozmo.Robot;

// Constructor-owned Needs state recipient: same context logical owner survives transition to Init.
internal sealed class LabPreInitNeedsRecipient
{
    internal long? TimeCreatedMicros {get;private set;}=0; // NeedsState constructor 0x69BF04
    internal readonly SortedDictionary<int,float> Levels=new();
    internal readonly SortedDictionary<int,int> Brackets=new();
    internal long? ReadTimeCreated()
    {
        if(InitializedOwner is null)
            foreach(int need in new[]{0,1,2}){Levels.TryAdd(need,0);Brackets.TryAdd(need,-1);}
        return TimeCreatedMicros;
    }
    internal Cozmo.Robot.Behavior.NeedsManager? InitializedOwner {get;private set;}
    internal void Attach(Cozmo.Robot.Behavior.NeedsManager owner){InitializedOwner=owner;TimeCreatedMicros=null;Levels.Clear();Brackets.Clear();}
}
// fidelity: M15-052
internal sealed class LabAudienceTags
{
    internal required LabSourceLog Log;
    internal SortedDictionary<byte[],Func<byte>> Predicates {get;}=new(Comparer<byte[]>.Create((a,b)=>a.AsSpan().SequenceCompareTo(b)));
    internal List<Func<byte[]>> Dynamic {get;}=new();
    internal List<byte[]> Cache {get;}=new();
    internal void RegisterEngineTags(Func<long?> needsState8,Func<long> timeSeconds,Func<string?> language,Func<string?> country)
    {
        Predicates.Add("app_user_d0"u8.ToArray(),()=>
        {
            long? stamp=needsState8();if(stamp is null)LabActivationRuntime.Missing("NeedsState+8 before NeedsManager::Init at0x006A5328, owner M15-062");
            return AppUserD0(stamp!.Value,timeSeconds())?(byte)1:(byte)0;
        });
        Dynamic.Add(()=>Locale("locale_language_",language()));
        Dynamic.Add(()=>Locale("locale_country_",country()));
    }
    private static byte[] Locale(string prefix,string? value)
    {
        if(value is null)LabActivationRuntime.Missing("host locale language/country input0x006A53CC/0x006A54C4; COMPATIBILITY_POLICY input is absent");
        var bytes=Encoding.UTF8.GetBytes(value!);
        // StringToLower8307A4..8307E8 operates on bytes, only ASCII A..Z.
        for(int i=0;i<bytes.Length;i++)if(bytes[i] is >=0x41 and <=0x5A)bytes[i]+=0x20;
        return Encoding.ASCII.GetBytes(prefix).Concat(bytes).ToArray();
    }
    internal static bool AppUserD0(long needsMicros,long nowSeconds)
    {long delta=unchecked((long)(int)nowSeconds-needsMicros/1000000);return delta>=0&&delta<86400;}
    internal IReadOnlyList<byte[]> GetQualified()
    {
        if(Cache.Count!=0)return Cache; // native tests begin==end, not a separate initialized flag
        Cache.Clear();
        foreach(var pair in Predicates)if(pair.Value()==1)Cache.Add(pair.Key.ToArray());
        foreach(var dynamic in Dynamic)Cache.Add(dynamic()); // keep empty/duplicate values
        Log(EngineLogLevel.Event,null,"audience_tags.qualified",LabActivationRuntime.CString(string.Join(" ",Cache.Select(LabActivationRuntime.RawText))));
        return Cache;
    }
    internal bool Verify(IReadOnlyList<byte[]> tags)
    {
        bool valid=true;
        foreach(var tag in tags)
            if(!Predicates.ContainsKey(tag))
            {
                Log(EngineLogLevel.Error,null,"AudienceTags.VerifyTags","got unknown tag: "+LabActivationRuntime.CString(LabActivationRuntime.RawText(tag)));
                Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak();valid=false;
            }
        return valid;
    }
}

// fidelity: M15-052
internal sealed class LabAbInterface
{
    internal static readonly LabAbInterface ProcessGlobals=new();
    internal Delegate? First105D948,Second105D960;
    internal Action<string>? AssignmentObserved;
    // Defined native predicate; its force-URL writer chain is unreachable (M15-049).
    internal static bool ShouldDisable(byte forceFlag,bool forcedAssignmentsNonempty)=>forceFlag!=0&&!forcedAssignmentsNonempty;
    internal void Initialize(Delegate first,Delegate second)
    {First105D948=first;AssignmentObserved?.Invoke("105D948");Second105D960=second;AssignmentObserved?.Invoke("105D960");}
}

// fidelity: M15-052
internal sealed class LabAssignmentSignalNode
{
    internal LabAssignmentSignalNode? Next;
    internal LabAssignmentSignalNode? Previous;
    internal uint RefCount20;
    internal Action<IReadOnlyList<LabAssignmentDef>>? Callback;
}
internal sealed class LabAssignmentSignal
{
    internal LabAssignmentSignalNode? Head;
    internal Action<LabAssignmentSignalNode>? Release4EB0CA;
    internal LabAssignmentSignalNode Connect(Action<IReadOnlyList<LabAssignmentDef>> callback)
    {
        if(Head is null){Head=new LabAssignmentSignalNode{RefCount20=1};Head.Next=Head;Head.Previous=Head;}
        var node=new LabAssignmentSignalNode{Callback=callback,RefCount20=1,Next=Head,Previous=Head.Previous};
        Head.Previous!.Next=node;Head.Previous=node;return node;
    }
    internal void Disconnect(LabAssignmentSignalNode node)
    {
        if(node.Callback is null||node.Previous is null||node.Next is null)return;
        node.Callback=null;node.Previous.Next=node.Next;node.Next.Previous=node.Previous;Release(node);
    }
    internal void Dispatch(IReadOnlyList<LabAssignmentDef> values)
    {
        var current=Head;if(current is null)return;
        current.RefCount20=unchecked(current.RefCount20+1);
        while(true)
        {
            current.Callback?.Invoke(values);
            var next=current.Next;
            if(next is null){StackDiagnostics.Write("M1-034 M15-052: skip null signal-next read0x007ED0D8; earlier callback retained");return;}
            next.RefCount20=unchecked(next.RefCount20+1);
            Release(current);
            current=next;
            if(ReferenceEquals(next,Head)){Release(next);return;}
        }
    }
    private void Release(LabAssignmentSignalNode node)
    {
        if(Release4EB0CA is not {} release)LabActivationRuntime.Missing("observer node release0x004EB0CA, M15-062 concrete subscriber lifetime; no fabricated release");
        Release4EB0CA!(node);
    }
}
