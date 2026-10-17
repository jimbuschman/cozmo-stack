namespace Cozmo.Robot.Behavior;

// fidelity: M15-045
// Every storage object is an explicit native input. A missing storage owner is distinct from a known empty container.
internal static class AiTeardownMissing
{
    internal static bool At(string text){SteppedBehavior.ReportMissing("M15-045/M15-063 "+text);return false;}
}
internal sealed class NativeFunctionFinalizer
{
    internal bool Empty,Inline;
    internal Func<bool>? Destroy10,DestroyDelete14;
    internal bool Destroy(string address)
    {
        if(Empty)return true;
        var body=Inline?Destroy10:DestroyDelete14;
        return body?.Invoke()??AiTeardownMissing.At("concrete std::function "+(Inline?"vt+10":"vt+14")+" at "+address);
    }
}
internal sealed class NativeVectorFinalizer<T>
{
    internal required IList<T> Elements;
    internal required int End;
    internal bool Allocated;
    internal Func<T,bool>? DestroyElement;
    internal Func<bool>? FreeAllocation;
    internal bool Destroy(string address,Func<T,bool>? checkedElement=null)
    {
        if(!Allocated)return true;
        while(End!=0)
        {
            var element=Elements[--End]; // native end store BEFORE element finalization
            var destroy=checkedElement??DestroyElement;
            if(destroy is null)return AiTeardownMissing.At("element recipient "+address);
            if(!destroy(element))return false;
        }
        return FreeAllocation?.Invoke()??AiTeardownMissing.At("vector allocation owner "+address);
    }
}
internal sealed class NativeSharedVectorFinalizer
{
    // Control is shared_ptr's second word; a null first word does not suppress a nonnull control release.
    internal required IList<object?> Controls;
    internal required int End;
    internal bool Allocated;
    internal Func<object,bool>? ReleaseShared;
    internal Func<bool>? FreeAllocation;
    internal bool Destroy(string address)
    {
        if(!Allocated)return true;
        while(End!=0)
        {
            var control=Controls[--End];
            if(control is not null && (ReleaseShared?.Invoke(control)!=true))
                return AiTeardownMissing.At("shared control/pointee release "+address);
        }
        return FreeAllocation?.Invoke()??AiTeardownMissing.At("shared vector allocation "+address);
    }
}
internal sealed class NativeProcessFinalizerNode
{
    internal NativeProcessFinalizerNode? Left,Right;
    internal NativeFunctionFinalizer? Callable30;
    internal Func<bool>? DestroySet24,DestroySet18,DeleteNode;
    internal static bool Destroy(NativeProcessFinalizerNode? node)
    {
        if(node is null)return true;
        if(!Destroy(node.Left)||!Destroy(node.Right))return false; // 569FC2..CE
        if(node.Callable30 is null)return AiTeardownMissing.At("ProcessInfo function storage+30 at569FD2");
        if(!node.Callable30.Destroy("569FD2..569FEA"))return false;
        if(node.DestroySet24?.Invoke()!=true)return AiTeardownMissing.At("ProcessInfo string-set+24 at569FF2");
        if(node.DestroySet18?.Invoke()!=true)return AiTeardownMissing.At("ProcessInfo string-set+18 at569FFC");
        return node.DeleteNode?.Invoke()??AiTeardownMissing.At("ProcessInfo node delete56A006");
    }
}
internal sealed class NativeHelperFinalizer
{
    internal NativeFunctionFinalizer? Completion28,Completion10;
    internal NativeSharedVectorFinalizer? SharedHelpers4;
    internal object? Factory0;
    internal Func<object,bool>? DeleteFactory;
    internal Func<bool>? DeleteComponent;
    internal bool Destroy()
    {
        if(Completion28 is null)return AiTeardownMissing.At("BehaviorHelper storage producer+38 at569D38");
        if(!Completion28.Destroy("569D38..4E"))return false;
        if(Completion10 is null)return AiTeardownMissing.At("BehaviorHelper storage producer+20 at569D50");
        if(!Completion10.Destroy("569D50..66"))return false;
        if(SharedHelpers4 is null)return AiTeardownMissing.At("BehaviorHelper shared-vector+4 at569D68");
        if(!SharedHelpers4.Destroy("569D68..6C shared IHelper"))return false;
        var factory=Factory0;Factory0=null;
        if(factory is not null&&(DeleteFactory?.Invoke(factory)!=true))return AiTeardownMissing.At("factory free after null store569D6E..76");
        return DeleteComponent?.Invoke()??AiTeardownMissing.At("BehaviorHelper allocation owner569D7C");
    }
}
internal sealed class NativeObjectCacheEntryFinalizer
{
    internal NativeFunctionFinalizer? Callable28;
    internal object? Filter24;
    internal Func<object,bool>? ClearFilterPredicates60;
    internal Func<object,int,bool>? DestroyFilterSet;
    internal Func<object,bool>? DeleteFilter;
    internal Func<bool>? DestroyObjectIdSet18;
    internal bool LongString4;
    internal Func<bool>? DeleteStringC;
    internal bool Destroy()
    {
        if(Callable28 is null)return AiTeardownMissing.At("ObjectInteractionCacheEntry function storage56A0AA..C0");
        if(!Callable28.Destroy("56A0AA..C0"))return false;
        var filter=Filter24;Filter24=null;
        if(filter is not null)
        {
            if(ClearFilterPredicates60?.Invoke(filter)!=true)return AiTeardownMissing.At("filter callable-list+60 at56A0CE");
            foreach(int offset in new[]{0x54,0x48,0x3C,0x30,0x24,0x18,0xC,0})
                if(DestroyFilterSet?.Invoke(filter,offset)!=true)return AiTeardownMissing.At($"filter set+{offset:X} at56A0D2..11C");
            if(DeleteFilter?.Invoke(filter)!=true)return AiTeardownMissing.At("filter delete56A122");
        }
        if(DestroyObjectIdSet18?.Invoke()!=true)return AiTeardownMissing.At("entry ObjectID-set+18 at56A12C");
        return !LongString4 || (DeleteStringC?.Invoke()??AiTeardownMissing.At("entry long-string delete56A138"));
    }
}
internal sealed class NativeObjectCacheHashNode
{
    internal NativeObjectCacheHashNode? Next;
    internal required NativeObjectCacheEntryFinalizer Entry;
    internal Func<bool>? DeleteNode;
}
internal sealed class NativeObjectCacheFinalizer
{
    internal NativeObjectCacheHashNode? Head;
    internal object? Buckets0;
    internal Func<object,bool>? DeleteBuckets;
    internal Func<bool>? DeleteOwner;
    internal bool Destroy()
    {
        var node=Head;
        while(node is not null)
        {
            var next=node.Next; // 56A090, before mapped entry destruction
            if(!node.Entry.Destroy())return false;
            if(node.DeleteNode?.Invoke()!=true)return AiTeardownMissing.At("hash node free56A09A");
            node=next;
        }
        var buckets=Buckets0;Buckets0=null; // 56A07C, not before deallocate chain
        if(buckets is not null&&DeleteBuckets?.Invoke(buckets)!=true)return AiTeardownMissing.At("hash buckets free56A080");
        return DeleteOwner?.Invoke()??AiTeardownMissing.At("ObjectInteractionInfoCache owner free569D2C");
    }
}
internal sealed class NativeWorkoutFinalizerEntry
{
    internal bool LongName;
    internal Func<bool>? FreeName;
    internal NativeVectorFinalizer<object?>? Scorers24,Scorers18;
    internal bool Destroy()
    {
        if(LongName&&FreeName?.Invoke()!=true)return AiTeardownMissing.At("WorkoutConfig long string56A160");
        if(Scorers24 is null)return AiTeardownMissing.At("WorkoutConfig scorer-vector producer56A164");
        if(!Scorers24.Destroy("56A168 EmotionScorer"))return false;
        return Scorers18?.Destroy("56A170 EmotionScorer")??AiTeardownMissing.At("WorkoutConfig scorer-vector producer56A16C");
    }
}
internal sealed class NativePlainTreeFinalizerNode
{
    internal NativePlainTreeFinalizerNode? Left,Right;
    internal Func<bool>? DeleteNode;
    internal static bool Destroy(NativePlainTreeFinalizerNode? node)
    {
        if(node is null)return true;
        if(!Destroy(node.Left)||!Destroy(node.Right))return false;
        return node.DeleteNode?.Invoke()??AiTeardownMissing.At("RequestGame tree node delete56A21C");
    }
}
internal sealed class NativeAiTeardown
{
    internal NativeHelperFinalizer? Helper;
    internal NativeObjectCacheFinalizer? ObjectCache;
    internal NativeVectorFinalizer<NativeSharedVectorFinalizer>? BorderRegions; // 569F5A outer then569F86 shared BorderSegment; pointee M15-063
    internal NativeProcessFinalizerNode? ProcessRoot;
    internal bool ProcessRootKnown;
    internal Func<bool>? DeleteAnalyzer;
    internal NativeVectorFinalizer<NativeWorkoutFinalizerEntry>? Workout;
    internal Func<bool>? DeleteWorkout;
    internal NativeSharedVectorFinalizer? SevereHandles;
    internal Func<bool>? DeleteSevere;
    internal bool SevereVtableReset,WhiteboardVtableReset;
    internal Func<bool>? WhiteboardBeacons,WhiteboardPossible,DeleteWhiteboard;
    internal NativeSharedVectorFinalizer? WhiteboardHandles;
    internal Func<int,bool>? WhiteboardFailureTree;
    internal bool RequestTreesKnown;
    internal NativePlainTreeFinalizerNode? RequestTree18,RequestTreeC;
    internal NativeSharedVectorFinalizer? RequestGameVector;
    internal Func<bool>? DeleteRequestGame;
    internal bool Destroy(int offset)
    {
        switch(offset)
        {
            case 0x10:return Helper?.Destroy()??AiTeardownMissing.At("helper native storage owner569D30");
            case 0x14:return ObjectCache?.Destroy()??AiTeardownMissing.At("object cache native storage owner569D1E");
            case 0x8:
                if(BorderRegions is null)return AiTeardownMissing.At("analyzer border-region/vector producer569D94");
                if(!BorderRegions.Destroy("569F5A outer region ->569F86 shared BorderSegment",region=>region.Destroy("569F86 BorderSegment control")))return false;
                if(!ProcessRootKnown)return AiTeardownMissing.At("ProcessInfo tree topology producer569D9C");
                return NativeProcessFinalizerNode.Destroy(ProcessRoot)&&(DeleteAnalyzer?.Invoke()??AiTeardownMissing.At("analyzer allocation569DA6"));
            case 0x1C:
                if(Workout is null)return AiTeardownMissing.At("Workout config-vector owner569D08 ->56A140");
                return Workout.Destroy("56A140..184",entry=>entry.Destroy())&&(DeleteWorkout?.Invoke()??AiTeardownMissing.At("Workout allocation569D0C"));
            case 0x30:
                SevereVtableReset=true;
                return (SevereHandles?.Destroy("SevereNeeds handle-vector572A30")??AiTeardownMissing.At("SevereNeeds handle-vector owner572A30"))&&Call(DeleteSevere,"SevereNeeds deleting owner572A3C");
            case 0x18:
                WhiteboardVtableReset=true;
                if(!Call(WhiteboardBeacons,"AIBeacon vector56A33E M15-063 pointee")||!Call(WhiteboardPossible,"PossibleObject list56A346 M15-063 pointee"))return false;
                foreach(int address in new[]{0x38,0x2C,0x20,0x14})
                    if(WhiteboardFailureTree?.Invoke(address)!=true)return AiTeardownMissing.At($"FailureInfo tree+{address:X} at56A350..36E M15-063 pointee");
                return (WhiteboardHandles?.Destroy("AIWhiteboard handles56A376")??AiTeardownMissing.At("AIWhiteboard handles owner56A376"))&&Call(DeleteWhiteboard,"AIWhiteboard deleting owner56A38E");
            case 0x20:
                if(!RequestTreesKnown)return AiTeardownMissing.At("RequestGame tree topology+18/+C at56A1E6/1F0");
                return NativePlainTreeFinalizerNode.Destroy(RequestTree18)&&NativePlainTreeFinalizerNode.Destroy(RequestTreeC)&&(RequestGameVector?.Destroy("RequestGame shared handle vector56A1FA")??AiTeardownMissing.At("RequestGame handle-vector owner56A1FA"))&&Call(DeleteRequestGame,"RequestGame allocation569CFE");
            default:return AiTeardownMissing.At($"unbound native AI child+{offset:X} owner");
        }
    }
    private static bool Call(Func<bool>? recipient,string address)=>recipient?.Invoke()??AiTeardownMissing.At(address);
}
