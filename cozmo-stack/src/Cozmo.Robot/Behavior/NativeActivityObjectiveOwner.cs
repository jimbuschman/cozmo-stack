using System.Text;
using System.Text.Json;

namespace Cozmo.Robot.Behavior;

// One retained activity owner contains the Socialize phase, objective map and single 8B subscription.
internal sealed class NativeActivityObjectiveOwner : IDisposable
{
    private sealed record Potential(uint Objective,string Behavior,uint Unlock,float Probability,uint Min,uint Max);
    private readonly Activity _activity;
    private readonly EngineRobot _robot;
    private readonly IReadOnlyDictionary<string,IBehavior> _bound;
    private readonly RetainedSubscription? _subscription;
    private readonly bool _objective4Absent;
    private readonly NativeFindFacesSocialize? _search;
    private readonly NativeInteractWithFaces? _interact;
    private readonly List<Potential> _potential=new();
    private readonly SortedDictionary<uint,uint> _counts=new();
    private readonly uint _maxSearch;
    private bool _selectedInternalObserved,_retired,_inputAvailable=true,_populationAvailable=true;
    private long _epoch;
    private int _phase;
    private uint _searchSnapshot,_runSnapshot;
    private IBehavior? _special;
    internal int Phase=>_phase;
    internal IReadOnlyDictionary<uint,uint> Counts=>_counts;
    internal IBehavior? Special=>_special;
    internal bool InputAvailable=>_inputAvailable;
    internal NativeActivityObjectiveOwner(Activity activity,EngineRobot robot,JsonElement config,IReadOnlyDictionary<string,IBehavior>? bound=null)
    {
        _activity=activity;_robot=robot;_bound=bound??new Dictionary<string,IBehavior>();
        if(activity.Type=="Sparked")
            _objective4Absent=config.TryGetProperty("behaviorObjective",out var objective) && objective.ValueKind==JsonValueKind.String && objective.GetString()!="DroveAsIntended";
        else
        {
            // ActivitySocialize constructor retains the two configured instances at +0x68 and +0x70.
            _search=_bound.GetValueOrDefault("FindFaces_socialize") as NativeFindFacesSocialize;
            _interact=_bound.GetValueOrDefault("InteractWithFaces") as NativeInteractWithFaces;
            _objective4Absent=config.TryGetProperty("requiredObjectives",out var objectives) && objectives.ValueKind==JsonValueKind.Array &&
                objectives.EnumerateArray().All(e=>e.TryGetProperty("objective",out var value) && value.ValueKind==JsonValueKind.String && value.GetString()!="DroveAsIntended");
            if(config.TryGetProperty("maxNumFindFacesSearchIterations",out var max))_maxSearch=max.GetUInt32();
            if(config.TryGetProperty("requiredObjectives",out objectives) && objectives.ValueKind==JsonValueKind.Array)
                foreach(var item in objectives.EnumerateArray())
                {
                    string? name=item.GetProperty("objective").GetString();
                    // PotentialObjectives uses BehaviorObjectiveFromString (0x00769558).
                    if (!BehaviorObjectives.TryFromString(name, out var id))
                    {
                        _robot.Engine.EmitStackLog($"error: string '{name}' is not a valid BehaviorObjective value");
                        id=BehaviorObjective.Unknown; // Native lookup miss at 0x00769A98.
                    }
                    string behavior=item.GetProperty("behaviorID").GetString()!;
                    uint unlock=item.TryGetProperty("ignoreIfLocked",out var u)?NativeUnlockNames.FromBytes(Encoding.UTF8.GetBytes(u.GetString()!)):0x55;
                    float probability=item.TryGetProperty("probabilityToRequireObjective",out var p)?p.GetSingle():1f;
                    uint min=item.TryGetProperty("randomCompletionsNeededMin",out var lo)?lo.GetUInt32():1u;
                    uint upper=item.TryGetProperty("randomCompletionsNeededMax",out var hi)?hi.GetUInt32():1u;
                    _potential.Add(new((uint)id,behavior,unlock,probability,min,upper));
                }
        }
        if(robot.HasExternalInterface)_subscription=robot.Engine.AnimationMessages.SubscribeMessage(0x8B,true,Receive);
    }
    internal void OnSelectedInternal()
    {
        if(_activity.Type!="Socialize" || _retired || !ReferenceEquals(_robot.Engine.Robot,_robot))return;
        long epoch=++_epoch;_selectedInternalObserved=true;_phase=0;_counts.Clear();_populationAvailable=_inputAvailable=true;
        foreach(var item in _potential)
        {
            if(_retired||epoch!=_epoch||!ReferenceEquals(_robot.Engine.Robot,_robot))return;
            if(item.Unlock!=0x55)
            {
                if(_robot.Progression is not {} progression || !progression.TryIsUnlocked(item.Unlock,out bool unlocked))
                {_populationAvailable=false;Unavailable("actual progression unlock state unavailable");return;}
                if(!unlocked)continue;
            }
            if(item.Probability<1f)
            {
                double chance=_robot.Engine.ContextRandom.RandDbl(1);
                if(_retired||epoch!=_epoch||!ReferenceEquals(_robot.Engine.Robot,_robot))return;
                if(!(chance<(double)item.Probability))continue;
            }
            uint count=unchecked((uint)_robot.Engine.ContextRandom.RandIntInRange(unchecked((int)item.Min),unchecked((int)item.Max)));
            if(_retired||epoch!=_epoch||!ReferenceEquals(_robot.Engine.Robot,_robot))return;
            _counts[item.Objective]=unchecked(_counts.GetValueOrDefault(item.Objective)+count);
        }
    }
    private void Unavailable(string text)
    {_inputAvailable=false;SteppedBehavior.ReportMissing("Socialize actual input unavailable: "+text);}
    private IBehavior? Resolve(string id)
    {
        if(_bound.TryGetValue(id,out var value))return value;
        if(Enum.TryParse<BehaviorID>(id,false,out var nativeId) && _robot.Engine.HostBehaviorMap?.ContainsKey(nativeId)==true)
        {
            _robot.InvalidateConfiguredScoreHistory(id);
            Unavailable("configured "+id+" behavior is unbuilt");
        }
        return null;
    }
    private bool? Runnable(IBehavior? behavior,BehaviorContext context,bool runningShortcut)
    {
        if(behavior is null)return false;
        if(behavior is not SteppedBehavior stepped){Unavailable("actual running/readiness owner for "+behavior.Id);return null;}
        if(runningShortcut && stepped.EngineRunning)return true;
        var ready=stepped.EvaluateHostReadiness(context);
        if(!ready.Available){Unavailable("readiness for "+behavior.Id);return null;}
        return ready.Runnable;
    }
    internal ChooserDecision Desired(IBehavior? current,ChooserDecision normal,BehaviorContext context)
    {
        if(_retired || !ReferenceEquals(_robot.Engine.Robot,_robot))
        {Unavailable("original activity owner unavailable");return normal with {InputUnavailable=true};}
        if(normal.InputUnavailable || normal.Behavior is not null)return normal; // 005B0282..2DA: any nonnull returns.
        if(!_selectedInternalObserved || !_populationAvailable)
        {Unavailable("phase/objective owner unavailable");return normal with {InputUnavailable=true};}
        _inputAvailable=true;
        long epoch=_epoch;
        bool Valid()=>!_retired && epoch==_epoch && ReferenceEquals(_robot.Engine.Robot,_robot);
        ChooserDecision Missing()=>normal with {InputUnavailable=true};
        ChooserDecision Chosen(IBehavior? target)=>normal with {Behavior=target};
        if (_phase is 0 or 1 or 2 && (_search is null || _interact is null))
        {
            Unavailable("retained FindFaces_socialize or InteractWithFaces owner");
            return Missing();
        }
        if(_phase==0)
        {
            var interact=Runnable(_interact,context,false);if(interact is null || !Valid())return Missing();
            if(interact.Value){_phase=2;return Chosen(_interact);}
            var ready=Runnable(_search,context,false);if(ready is null || !Valid())return Missing();
            if(ready.Value)
            {
                _searchSnapshot=_search!.SearchIterations;_phase=1;return Chosen(_search); // 0x005B033A..3E
            }
            return normal;
        }
        if(_phase==1)
        {
            var interact=Runnable(_interact,context,false);if(interact is null || !Valid())return Missing();
            if(interact.Value){_phase=2;return Chosen(_interact);}
            if(_maxSearch!=0 && unchecked(_search!.SearchIterations-_searchSnapshot)>=_maxSearch)
            {_phase=6;return normal;} // 0x005B0520..38
            var ready=Runnable(_search,context,true);if(ready is null || !Valid())return Missing();
            return ready.Value?Chosen(_search):normal;
        }
        if(_phase==2)
        {
            var ready=Runnable(_interact,context,true);if(ready is null || !Valid())return Missing();
            if(ready.Value)return Chosen(_interact);
            ready=Runnable(_search,context,true);
            if(ready is null || !Valid())return Missing();_phase=1;
            return ready.Value?Chosen(_search):normal;
        }
        if(_phase==3)
        {
            if(_counts.Count==0){_phase=6;return normal;}
            var candidates=new List<IBehavior>();
            foreach(var item in _potential)
            {
                if(!_counts.ContainsKey(item.Objective))continue;
                var candidate=Resolve(item.Behavior);if(!_inputAvailable)return Missing();
                var ready=Runnable(candidate,context,false);if(ready is null || !Valid())return Missing();
                if(ready.Value)candidates.Add(candidate!);
            }
            if(candidates.Count==0){_phase=6;return normal;}
            int index=_robot.Engine.ContextRandom.RandIntInRange(0,candidates.Count-1);
            if(!Valid())return Missing();
            var chosen=candidates[index];
            if(chosen is not SteppedBehavior step){Unavailable("chosen objective run count +80");return Missing();}
            _special=chosen;_runSnapshot=unchecked((uint)step.ScoreIncreaseCount);_phase=4;return Chosen(chosen);
        }
        if(_phase==4)
        {
            if(_special is not SteppedBehavior special){Unavailable("retained objective run count +80");return Missing();}
            if(current is null && unchecked((uint)special.ScoreIncreaseCount)>_runSnapshot){_phase=6;return normal;}
            var ready=Runnable(special,context,true);if(ready is null || !Valid())return Missing();
            return ready.Value?Chosen(special):normal;
        }
        if(_phase==5 && ReferenceEquals(current,_special) && _special is SteppedBehavior {EngineRunning:true})return Chosen(_special);
        return normal;
    }
    private void Receive(object? payload)
    {
        using var owner=_robot.Engine.EnterSynchronousOwner();
        if(_retired || !ReferenceEquals(_robot.Engine.Robot,_robot) || payload is not uint objective)return;
        if(_activity.Type=="Socialize" && !_selectedInternalObserved)
        {_robot.Engine.EmitStackLog("M1-034 Socialize objective recipient 0x005B07C2: skip pre-first-selection uninitialized phase read for "+_activity.Id);return;}
        if(_activity.Type!="Socialize")
        {
            if(objective==4 && _objective4Absent)return;
            SteppedBehavior.ReportMissing($"{_activity.Type} objective recipient {_activity.Id}: objective {objective}/changed configuration is outside the checked objective4 miss; no state mutation invented");return;
        }
        if(!_populationAvailable){Unavailable("objective delivery needs unbuilt population state");return;}
        long epoch=++_epoch;
        if(_phase==2 && objective==(uint)BehaviorObjective.InteractedWithFace){_phase=3;return;}
        uint remaining=(uint)_counts.Count;
        if(_counts.TryGetValue(objective,out uint count))
        {
            remaining=unchecked(count-1);if(remaining==0)_counts.Remove(objective);else _counts[objective]=remaining;
        }
        if(remaining==0 && _phase==4)
        {
            if(_special is SteppedBehavior {EngineRunning:true} behavior)behavior.StopOnNextActionComplete();
            if(_retired || epoch!=_epoch || !ReferenceEquals(_robot.Engine.Robot,_robot))return;
            _phase=5;
        }
    }
    public void Dispose(){if(_retired)return;_retired=true;++_epoch;_subscription?.Dispose();_counts.Clear();_potential.Clear();_special=null;}
}
