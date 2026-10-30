using Cozmo.Robot.Vision;
namespace Cozmo.Robot.Behavior;

// fidelity: M13-038, M13-039
// Semantic state fields, not a claim about a generated game-message serializer or native padding.
internal readonly record struct BehaviorPublicState(int UnlockId=0x55,byte Carrying=0,byte Requesting=0,byte StackHeight=0,
    float Need0=0,float Need1=0,float Need2=0,byte Activity=0,byte Reaction=0x16,byte StageTag=4,uint Stages=0)
{
    internal byte Stage(int tag)=>tag is >=0 and <=3?(byte)(Stages>>(tag*8)):(byte)0;
    internal int Round=>Stage(StageTag);
}
internal readonly record struct BehaviorPublicStateInputs(int CurrentUnlock,int RequestedUnlock,int CarriedId,byte Reaction,
    (float First,float Second,float Third)? Needs);

internal sealed class BehaviorPublicStateBroadcaster
{
    internal readonly object Gate=new();
    private readonly EngineRobot _robot;
    private readonly SubscriptionSignal<BehaviorPublicState> _signal=new();
    private BehaviorPublicState _state=new(UnlockId:0x55,Reaction:0x16,StageTag:4);
    private bool _retired;
    internal Func<int,string?>? UnlockNameInput;
    internal Func<int,string?>? StageNameInput;
    internal Func<byte,string?>? ReactionNameInput;
    internal BehaviorPublicState State {get{lock(Gate)return _state;}}
    internal BehaviorPublicStateBroadcaster(EngineRobot robot)
    {
        _robot=robot;
        _signal.UndefinedTail=()=>StackDiagnostics.Write("M1-034 M13-038 freed public-state successor read at 0x0064E678: skip emit tail; state retained");
    }
    internal RetainedSubscription Subscribe(Action<BehaviorPublicState> callback)=>_signal.Subscribe(0,callback);
    internal void SendUpdatedState()
    {
        lock(Gate)
        {
            if(_retired){StackDiagnostics.Write("M1-034 M13-038 retired broadcaster at 0x0064DE5E: skip emit; state retained");return;}
            // Struct copy made before entering the signal. Nested updates cannot mutate the outer event.
            var snapshot=_state;_signal.Deliver(0,snapshot);
        }
    }
    internal void Update(BehaviorPublicStateInputs input)
    {
        lock(Gate)
        {
            bool dirty=false;
            int current=input.CurrentUnlock,requested=input.RequestedUnlock;
            if(current!=_state.UnlockId && !(requested==0x55 && current!=requested))
            {
                _state=_state with{UnlockId=current};dirty=true;
                if(UnlockNameInput is {} name)Info("PublicStateBroadcaster.Update.SparkStateHasChanged",$"Spark state has changed to {name(current)??"(null)"}");
                else Missing("UnlockId EnumToString at 0x0064D634: field store retained; source Info awaits its native name input");
            }
            if(requested==0x55 && current!=requested && _state.UnlockId!=0x55)
            {
                if(UnlockNameInput is {} name)Info("PublicStateBroadcaster.Update.UserCanceldSpark",$"Spark state has changed from {name(_state.UnlockId)??"(null)"}");
                else Missing("UnlockId EnumToString at 0x0064D68A: source cancellation Info awaits its native name input");
                _state=_state with{UnlockId=0x55};dirty=true;
            }
            byte carrying=input.CarriedId!=-1?(byte)1:(byte)0;
            if(carrying!=_state.Carrying)
            {
                _state=_state with{Carrying=carrying};
                Info("PublicStateBroadcaster.Update.RobotCarryingObjectChanged",$"Robot {(carrying!=0?"is now":"is no longer")} carrying an object");
                dirty=true;
            }
            if(input.Reaction!=_state.Reaction)
            {
                _state=_state with{Reaction=input.Reaction};
                if(ReactionNameInput is {} name)Info("PublicStateBroadcaster.Update.ReactionTriggered",$"Robot is now reacting to trigger {name(_state.Reaction)??"(null)"}");
                else Missing("ReactionTrigger EnumToString0x0064D752: reaction store retained; source Info awaits its native name input");
                dirty=true;
            }
            if(input.Needs is {} needs)
            {
                Need(0,needs.First);Need(1,needs.Second);Need(2,needs.Third);
            }
            if(dirty)SendUpdatedState();
            void Need(int index,float value)
            {
                float old=index switch{0=>_state.Need0,1=>_state.Need1,_=>_state.Need2};
                float delta=old-value;if(delta<0)delta=-delta;
                if(delta<BitConverter.UInt32BitsToSingle(0x3727C5AC))return; // unordered writes
                _state=index switch{0=>_state with{Need0=value},1=>_state with{Need1=value},_=>_state with{Need2=value}};dirty=true;
            }
        }
    }
    internal void UpdateActivity(byte activity){lock(Gate){_state=_state with{Activity=activity};SendUpdatedState();}}
    internal void UpdateRequestingGame(byte value){lock(Gate){_state=_state with{Requesting=value};SendUpdatedState();}}
    internal void UpdateStackHeight(byte value){lock(Gate){if(_state.StackHeight==value)return;_state=_state with{StackHeight=value};SendUpdatedState();}}
    internal void UpdateBroadcastBehaviorStage(int tag,uint stage,Func<bool>? current=null)
    {
        lock(Gate)
        {
            int old=_state.StageTag;
            if(old==tag && stage==_state.Stage(tag))
            {
                _robot.Engine.EmitSourceLog(EngineLogLevel.Warning,null,"PublicStateBroadcaster.UpdateUserFacingBehaviorStage","Attempting to update a stage that has already been set");return;
            }
            if(StageNameInput is {} names)
            {
                if(old!=tag)Info("PublicStateBroadcaster.UpdateUserFacingBehaviorStage.SwitchingStageType",$"Moving from behavior stage type {names(old)??"(null)"} to stage type {names(tag)??"(null)"}");
                else Info("PublicStateBroadcaster.UpdateUserFacingBehaviorStage.UpdatingStage",FormattableString.Invariant($"Updating stage type {names(tag)??"(null)"} to stage {unchecked((int)stage)}"));
            }
            else Missing("BehaviorStageTag EnumToString at 0x0064DA72/0x0064DA7A/0x0064DAF6: stage Info name input absent; checked field store/emission proceed");
            if(current is not null&&!current())return;
            _state=_state with{StageTag=unchecked((byte)tag),Stages=unchecked(stage*0x01010101u)};
            SendUpdatedState();
        }
    }
    private void Info(string key,string text)=>_robot.Engine.EmitSourceLog(EngineLogLevel.Info,"PublicStateBroadcast",key,text);
    private static void Missing(string text)=>SteppedBehavior.ReportMissing("M13-038 "+text);
    internal void Retire(){lock(Gate)_retired=true;} // surrounding raw/hash/capture boundary is reported by RobotLifetime
}
