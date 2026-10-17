using Cozmo.Protocol;
namespace Cozmo.Robot.Behavior;

// fidelity: M15-056
internal sealed class FeedingCubeControllerUnit
{
    internal required int Id;
    internal required SourceLogSink Log;
    internal int? State4;
    internal object? Inner;
    internal uint Inner28;
    internal object? Listener,ListenerControl;
    internal int? InnerId;
    internal Action? SetCubeLights5A65AC;
    internal Action<object>? RetainListener;
    internal Action<int,object?>? RemoveListener6356AC;
    internal Action<object>? ReleaseListener;
    internal Action<object>? DeleteInner;
    internal Action? DeleteOuter;
    internal Func<int,string>? StateName;
    private bool Name(int state,out string text)
    {text="";if(StateName is null){SteppedBehavior.ReportMissing("M15-056 controller state-name table01025228 input");return false;}text=StateName(state);return true;}
    internal bool SetState(int state,float time)
    {
        if(State4 is null){SteppedBehavior.ReportMissing("M15-061 FeedingCubeController constructor5A6038..5A60B0 state+4 owner");return false;}
        if(State4==state)
        {
            if(!Name(state,out var sameName))return false;
            Log(EngineLogLevel.Warning,null,"FeedingCubeController.SetControllerState.StateAlreadySet",$"Attempting to set new controller state {sameName}, but that is already the state");return true;
        }
        if(state!=2){SteppedBehavior.ReportMissing("M15-061 FeedingCubeController state0/1/3..6 bodies0x005A6100; checked state2 only");return false;}
        if(!ClearController())return false;
        // 5A61B6 reloads state AFTER ClearController's callees, before logging.
        if(State4!=state)
        {
            if(!Name(state,out var name))return false;
            if(Inner is null){StackDiagnostics.Write("M1-034 M15-056: skip null controller-inner log ID read0x005A61D2; earlier clear retained");return false;}
            if(InnerId is null){SteppedBehavior.ReportMissing("M15-061 controller inner ObjectID producer at0x005A61D2");return false;}
            Log(EngineLogLevel.Info,"Feeding","FeedingCubeController.SetControllerState.NewState",$"Feeding cube controller for id {InnerId.Value} switched to state {name}");
        }
        State4=2;return true;
    }
    internal bool ClearController()
    {
        if(Inner is null){StackDiagnostics.Write("M1-034 M15-056: skip null controller-inner store at0x005A641A; prior state retained");return false;}
        Inner28=0x28;
        if(SetCubeLights5A65AC is null){SteppedBehavior.ReportMissing("M15-061 SetCubeLights recipient after inner+28 store0x005A640E..6470");return false;}
        SetCubeLights5A65AC();
        if(Inner is null){StackDiagnostics.Write("M1-034 M15-056: skip ClearController null inner reload0x005A642A after lights; prior store retained");return false;}
        var listener=Listener;var control=ListenerControl;
        if(control is not null)
        {
            if(RetainListener is null){SteppedBehavior.ReportMissing("M15-061 ClearController listener shared-count recipients5A6436/6448");return false;}
            RetainListener(control);
        }
        // RemoveListener is called even for an empty shared pair.
        if(InnerId is null||RemoveListener6356AC is null)
        {SteppedBehavior.ReportMissing("M15-061 ClearController inner ObjectID/RemoveListener0x006356AC; prior retain kept, no host RemoveListener substitute");return false;}
        RemoveListener6356AC(InnerId.Value,listener);
        if(control is not null)
        {if(ReleaseListener is null){SteppedBehavior.ReportMissing("M15-061 ClearController temporary shared release5A6448; earlier removal retained");return false;}ReleaseListener(control);}
        if(Inner is null){StackDiagnostics.Write("M1-034 M15-056: skip ClearController null inner reload0x005A6450 after removal; earlier effects retained");return false;}
        var oldControl=ListenerControl;Listener=null;ListenerControl=null;
        if(oldControl is not null)
        {if(ReleaseListener is null){SteppedBehavior.ReportMissing("M15-061 ClearController stored listener release5A6458");return false;}ReleaseListener(oldControl);}
        return true;
    }
    internal bool Destroy(bool deleting=true)
    {
        var inner=Inner;Inner=null; // dtor5A60C0 nulls owned pointer before listener release/free
        if(inner is not null)
        {
            if(ListenerControl is {} listener)
            {
                if(ReleaseListener is null){SteppedBehavior.ReportMissing("M15-061 controller dtor shared-listener release5A60C0..6100");return false;}
                ReleaseListener(listener);
            }
            if(DeleteInner is null){SteppedBehavior.ReportMissing("M15-061 controller inner allocation owner deletion5A60C0..6100");return false;}DeleteInner(inner);
        }
        if(deleting){if(DeleteOuter is null){SteppedBehavior.ReportMissing("M15-061 deleting controller outer allocation owner5A60E0..6100");return false;}DeleteOuter();}
        return true;
    }
}

// fidelity: M15-056
internal sealed class FeedingDisconnectUnit
{
    internal required CozmoEngine Engine;
    internal SortedDictionary<int,FeedingCubeControllerUnit> Controllers78 {get;}=new();
    internal List<RetainedSubscription> HandlesAC {get;}=new();
    internal int? Stage6C,Selected94;
    internal Func<int,FeedingCubeControllerUnit>? ConstructController;
    internal Func<SortedDictionary<int,FeedingCubeControllerUnit>,bool>? DestroyControllerTree;
    internal void OnSelected()
    {
        SteppedBehavior.ReportMissing("M15-061 surrounding ActivityFeeding OnSelected lifecycle before subscription5AB030 is not built in this batch; checked subscription unit only");
        // No HasExternalInterface test in native Feeding subscription block.
        HandlesAC.Add(Engine.AnimationMessages.SubscribeMessage(0x0D,true,p=>Handle((ObjectConnectionStateMessage)p!)));
    }
    internal bool Handle(ObjectConnectionStateMessage message)
    {
        int id=unchecked((int)message.ObjectID);Controllers78.TryGetValue(id,out var old);
        float minusOne=BitConverter.UInt32BitsToSingle(0xBF800000);
        if(message.ConnectedByte!=0)
        {
            if(ConstructController is null){SteppedBehavior.ReportMissing("M15-061 FeedingCubeController ctor0x005A6038..5A60B0, called0x005AC9CC..CA7C");return false;}
            var created=ConstructController(id); // allocation/constructor BEFORE duplicate check
            if(old is not null)return created.Destroy();
            Controllers78.Add(id,created);
            if(Stage6C is null){SteppedBehavior.ReportMissing("M15-061 Feeding signed stage+6C producer before0x005ACA68; inserted controller retained");return false;}
            return Stage6C<4||created.SetState(0,minusOne);
        }
        if(old is not null)
        {
            if(!old.SetState(2,minusOne))return false;
            // std::tree erase destroys its mapped unique owner before discarding the node.
            if(!old.Destroy())return false;
            Controllers78.Remove(id);
        }
        if(Selected94 is null){SteppedBehavior.ReportMissing("M15-061 Feeding selected cube+94 producer before0x005ACA9E");return false;}
        if(Selected94==id)
        {
            Engine.EmitSourceLog(EngineLogLevel.Info,"Feeding","FeedingActivity.HandleObjectConnectionStateChange.LostConnectionToInteractID",$"object {id} lost connection, clearing cubeIDToEat");Selected94=-1;
        }
        return true;
    }
    internal bool OnDeselected()
    {
        foreach(var controller in Controllers78.Values)
            if(!controller.SetState(2,BitConverter.UInt32BitsToSingle(0xBF800000)))return false;
        if(DestroyControllerTree is null){SteppedBehavior.ReportMissing("M15-061 Feeding controller-tree destroy0x005ABC0A -> PLT0x004B16D4; no guessed destructor traversal order");return false;}
        if(!DestroyControllerTree(Controllers78))return false;
        Controllers78.Clear();
        while(HandlesAC.Count!=0){int last=HandlesAC.Count-1;var handle=HandlesAC[last];HandlesAC.RemoveAt(last);handle.Dispose();}
        SteppedBehavior.ReportMissing("M15-061 surrounding Feeding deselect lifecycle0x005ABB5C..5ABC46; checked state2/map/handle order only");return true;
    }
}

// fidelity: M15-056
internal sealed class GuardDogDisconnectUnit
{
    internal required string Id;
    internal required SourceLogSink Log;
    internal Func<byte>? RunningA1;
    internal Action<ushort,object?>? PreHook5C;
    internal Action<bool,bool>? StopActing;
    internal Func<ushort,string>? TagName;
    internal int? State11C;
    internal string? StateLabel58;
    internal List<RetainedSubscription> Handles20 {get;}=new();
    internal void Subscribe(CozmoEngine engine,bool hasExternalInterface)
    {
        if(!hasExternalInterface)return;
        foreach(ushort tag in new ushort[]{13,15,17})
            Handles20.Add(engine.AnimationMessages.SubscribeMessage(tag,true,p=>HandleEvent(tag,p)));
    }
    internal void HandleEvent(ushort tag,object? payload)
    {
        if(PreHook5C is null){SteppedBehavior.ReportMissing("M15-067 IBehavior pre-event vt+5C at0x005BF268 before running gate; GuardDog owner unbound");return;}
        PreHook5C(tag,payload);
        if(RunningA1 is null){SteppedBehavior.ReportMissing("M15-067 GuardDog running-byte owner+A1 at0x005BF27C; no inactive default inferred");return;}
        if(RunningA1()==0)return; // manager checked: nonrunning GuardDog is bx lr
        if(tag==13){HandleConnection((ObjectConnectionStateMessage)payload!);return;}
        if(tag is 15 or 17){SteppedBehavior.ReportMissing($"M15-067 GuardDog non-connection dispatcher tag{tag} at0x005F4D28..4DE8");return;}
        if(TagName is null){SteppedBehavior.ReportMissing("M15-056 GuardDog E2G EnumToString recipient at0x005F4D96");return;}
        Log(EngineLogLevel.Warning,null,"BehaviorGuardDog.HandleWhileRunning","Received an unhandled E2G event: "+TagName(tag));
    }
    internal bool HandleConnection(ObjectConnectionStateMessage message)
    {
        if(message.ConnectedByte!=0)return true;
        Log(EngineLogLevel.Warning,null,"BehaviorGuardDog.HandleObjectConnectionState",$"Object with ID {unchecked((int)message.ObjectID)} and type {CubeConnections.ObjectTypeName(message.ObjectType)} has disconnected");
        if(StopActing is null){SteppedBehavior.ReportMissing("M15-056/M8-015 GuardDog StopActing(true,false)0x005F4E82 ->0x005BD34C native owner unbound");return false;}
        StopActing(true,false);State11C=8;
        if(StateLabel58 is null){SteppedBehavior.ReportMissing("M15-067 GuardDog old state-label+58 producer before0x005C0CEC; state8 retained");return false;}
        Log(EngineLogLevel.Info,"Behaviors","Behavior.TransitionToState",$"Behavior:{Id}, FromState:{StateLabel58} ToState:BlockDisconnected");StateLabel58="BlockDisconnected";return true;
    }
    internal void DestroyHandles()
    {while(Handles20.Count!=0){int last=Handles20.Count-1;var h=Handles20[last];Handles20.RemoveAt(last);h.Dispose();}}
}
