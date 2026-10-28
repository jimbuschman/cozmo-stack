namespace Cozmo.Robot.Behavior;

// Proposed shared state-light recipient L1-L3/005A4320..005A45F2, wrapper005BEE38..46.
// The manager owns the ordered value vector, independently of any behavior's lifetime.
internal sealed record BehaviorStateLightInfo(uint ObjectId,uint Trigger,bool Modifier,CubeLightModifier Lights)
{
    internal BehaviorStateLightInfo Copy()=>this with{Lights=Lights with{Leds=Lights.Leds.ToArray()}};
    internal static CubeLightModifier Zero()=>new(new LedPattern[4],0,0,System.Numerics.Vector2.Zero);
}
public sealed partial class BehaviorManager
{
    private readonly List<BehaviorStateLightInfo> _stateLights=new();
    private byte _stateLightsClass=0x39;
    private bool _stateLightsFlag;
    internal (byte Class,bool Flag,IReadOnlyList<BehaviorStateLightInfo> Entries) InspectStateLights()
    {lock(_gate)return(_stateLightsClass,_stateLightsFlag,_stateLights.Select(entry=>entry.Copy()).ToArray());}
    // fidelity: M7-030
    internal void SetBehaviorStateLights(BehaviorClass behaviorClass,IReadOnlyList<BehaviorStateLightInfo> input,bool flag)
    {
        lock(_gate)
        {
            _stateLightsFlag=flag;
            var updated=new HashSet<uint>();var lights=_context.Robot.Lights.Cubes;
            void PlayFailed(BehaviorStateLightInfo entry)=>_context.Robot.Engine.EmitSourceLog(EngineLogLevel.Info,"Behaviors","SetBehaviorStateLights.UpdatingStateLights.FailedToPlayLights",$"Failed to play {entry.Trigger} for object {entry.ObjectId}");
            foreach(var old in _stateLights)
            {
                var next=input.FirstOrDefault(entry=>entry.ObjectId==old.ObjectId);
                if(next is null){lights.StopBehaviorStateLight(old.ObjectId,old.Trigger,true);continue;}
                bool played=lights.StopAndPlayBehaviorStateLight(next.ObjectId,old.Trigger,next.Trigger,next.Modifier,next.Modifier?next.Lights:BehaviorStateLightInfo.Zero());
                if(!played)PlayFailed(next);updated.Add(next.ObjectId);
            }
            foreach(var entry in input)
            {
                if(updated.Contains(entry.ObjectId))continue;
                if(!lights.PlayBehaviorStateLight(entry.ObjectId,entry.Trigger,entry.Modifier,entry.Modifier?entry.Lights:BehaviorStateLightInfo.Zero()))PlayFailed(entry);
            }
            if(!ReferenceEquals(input,_stateLights))
            {var copy=input.Select(entry=>entry.Copy()).ToArray();_stateLights.Clear();_stateLights.AddRange(copy);}
            _stateLightsClass=(byte)behaviorClass;
        }
    }
    // Existing MS4 recipient is called ONLY at SetRunningAndResumeInfo, after Init can replace the state vector.
    // fidelity: M7-030
    private void UpdateStateLightsForRunningInfo(IBehavior? next,IBehavior? oldCurrent,IBehavior? oldResume,ReactionTrigger? oldTrigger,ReactionTrigger? newTrigger)
    {
        lock(_gate)
        {
            if(_stateLights.Count==0||next is null)return;
            if(!Enum.TryParse<BehaviorClass>(next.Class,out var behaviorClass))
            {SteppedBehavior.ReportMissing("State-light MS4 actual native class byte unavailable for "+next.Class+"; prior retained lights unchanged");return;}
            if((byte)behaviorClass==_stateLightsClass)return;
            bool fresh=!ReferenceEquals(next,oldCurrent)&&!ReferenceEquals(next,oldResume);
            bool leftNone=oldTrigger is null&&newTrigger is not null&&!_stateLightsFlag;
            if(!fresh&&!leftNone)return;
            foreach(var entry in _stateLights)_context.Robot.Lights.Cubes.StopBehaviorStateLight(entry.ObjectId,entry.Trigger,true);
            _stateLights.Clear(); // class84/flag85 deliberately retained.
        }
    }
}
