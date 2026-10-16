namespace Cozmo.Robot.Behavior;

// fidelity: M13-038, M13-039
// Values at robot+58's virtual-call boundary; Client is owner observation, not a native argument.
// Backend/Wwise interpretation is M6/M9's recipient.
internal readonly record struct BehaviorAudioRequest(string Client,int Slot,uint Id,uint Value,uint Arg3,uint Extra0=0,uint Extra1=0,bool EmptyCallback=true);
internal readonly record struct SparkedMusicStateInput(uint State);

internal sealed class BehaviorAudioClient : IDisposable
{
    private readonly EngineRobot _robot;
    private readonly BehaviorPublicStateBroadcaster _public;
    private readonly SubscriptionOwners _handles=new();
    internal string Owner {get;}
    internal int ActiveUnlock {get;private set;}=0x55;
    internal int PublishedUnlock {get;private set;}=0x55;
    internal uint MusicState {get;private set;}
    internal bool Active {get;private set;}
    internal int Round {get;private set;}
    internal byte Carrying,Requesting,Stacked,Activity,StageTag=4,GuardStage=5,FeedingStage=1;
    // The checked ctor writes no +2C/+30/+34 values. Their raw-cache producer/input is not guessed as zero.
    private readonly float?[] _needs=new float?[3];
    internal void SetNativeNeedsCache(float first,float second,float third)
    {lock(_public.Gate){_needs[0]=first;_needs[1]=second;_needs[2]=third;}}
    internal float? NeedCache(int index)=>_needs[index];
    internal Func<byte,string?>? ActivityNameInput;
    internal string? ActivityLogChannelInput;
    internal Action<string>? StepForTest;
    internal Action? ActivatingForTest;
    private bool _disposed;
    internal static readonly uint[] RoundStates={0xF0C83E52,0xF0C83E53,0xF0C83E50,0xF0C83E51,0xF0C83E56,0xF0C83E57,0xF0C83E54,0xF0C83E55,0xF0C83E5A,0xF0C83E5B,0xF1C83FC5};
    private static readonly IReadOnlyDictionary<byte,uint> ActivityStates=new Dictionary<byte,uint>
    {{1,0},{2,2320504975},{5,296159617},{7,0},{8,0},{9,0},{12,670611050},{11,670611050},{13,670611050},{15,0},{16,670611050},{10,2890913445}};
    internal BehaviorAudioClient(EngineRobot robot,BehaviorPublicStateBroadcaster broadcaster,string owner,bool hasExternalInterface)
    {
        _robot=robot;_public=broadcaster;Owner=owner;
        try
        {
            if(hasExternalInterface)_handles.Add(robot.Engine.AnimationMessages.SubscribeMessage(0xA3,false,
                value=>RunHandler(()=>SetSparkedMusicState(((SparkedMusicStateInput)value!).State),"0x00592A9E")));
            _handles.Add(broadcaster.Subscribe(message=>RunHandler(()=>HandleRobotPublicStateChange(message),"0x00592B1E"))); // unconditional
        }
        catch{_handles.Retire();throw;}
    }
    internal void Init(){} // 591FC0 bx lr, no re-subscribe/reset
    internal void SetSparkedMusicState(uint value)
    {
        lock(_public.Gate)
        {
            MusicState=value;
            // Lambda passes &client+8, not a frozen copy across nested stage publication.
            HandleSparkUpdates(()=>PublishedUnlock,Round);
        }
    }
    internal void HandleRobotPublicStateChange(BehaviorPublicState message)
    {
        lock(_public.Gate)
        {
            StepForTest?.Invoke("world");HandleWorldEventUpdates(message);
            StepForTest?.Invoke("round");int round=message.Round;
            StepForTest?.Invoke("spark");HandleSparkUpdates(()=>message.UnlockId,round);
            StepForTest?.Invoke("needs");HandleNeedsUpdates(message.Need0,message.Need1,message.Need2);
            StepForTest?.Invoke("dim");HandleDimMusicForActivity(message.Activity);
            StepForTest?.Invoke("activity");
            if(message.Activity==Activity){HandleGuardDogUpdates(message);HandleFeedingUpdates(message);}
            else{UpdateActivityMusicState(message.Activity);Activity=message.Activity;}
            StepForTest?.Invoke("stage cache");if(StageTag!=message.StageTag)StageTag=message.StageTag;
            StepForTest?.Invoke("unlock cache");PublishedUnlock=message.UnlockId;
        }
    }
    private void HandleWorldEventUpdates(BehaviorPublicState message)
    {
        if(message.Activity==2)return;
        if(Carrying!=message.Carrying){Carrying=message.Carrying;Post(Carrying!=0?0xBDA79E15u:0xD1BFFD53u);}
        if(Requesting!=message.Requesting){Requesting=message.Requesting;Post(Requesting!=0?0x6CAF712Cu:0x053A4E2Eu);}
        byte stacked=message.StackHeight>1?(byte)1:(byte)0;
        if(Stacked!=stacked){Stacked=stacked;Post(message.StackHeight>=2?0xEC3490ECu:0x3BA32BC6u);}
    }
    private void HandleSparkUpdates(Func<int> readUnlock,int round)
    {
        int unlock=readUnlock();
        if(unlock==ActiveUnlock){if(round!=Round)UpdateBehaviorRound(unlock,round);return;}
        if(unlock==0x55){Active=false;MusicState=0;ActiveUnlock=0x55;Round=0;return;}
        if(MusicState==0)
        {
            _robot.Engine.EmitSourceLog(EngineLogLevel.Info,"Unnamed","BehaviorAudioClient.HandleRobotPublicStateChange.InvalidMusicState","Attempted to activate sparked music state with invalid music state");return;
        }
        int chosenRound=0;
        if(unlock==0x1D)
        {
            if(_robot.EightiesMusicInput is not {} decision)
            {Missing("M13-010 WorkoutComponent owner/ShouldPlayEightiesMusic 0x00573E30 at 0x00592416 is unbound; no false decision or activation substituted");return;}
            chosenRound=decision()?1:0;
            _public.UpdateBroadcastBehaviorStage(3,(uint)chosenRound); // before Activate; recursively reenters A, then B
        }
        ActivateSparkedMusic(readUnlock(),0x093E7E04,MusicState,chosenRound); // reload after nested publication
    }
    internal bool ActivateSparkedMusic(int unlock,uint state,uint musicSwitch,int round)
    {
        ActiveUnlock=unlock;
        if(unlock==0){Active=false;return false;}
        Active=true;ActivatingForTest?.Invoke();
        UpdateBehaviorRound(unlock,round);
        if(musicSwitch!=0)Switch(0x03B760EF,musicSwitch);
        if(state!=0)Send(new(Owner,0x1C,state,0,0));
        return true;
    }
    internal bool UpdateBehaviorRound(int unlock,int round)
    {
        if(unlock!=ActiveUnlock)return false;
        Round=round;
        if((uint)round>10)return true;
        uint value=RoundStates[round];if(value!=0 && Active)Switch(0x972DC63C,value);
        return true;
    }
    internal void HandleNeedsUpdates(float first,float second,float third)
    {
        var values=new[]{first,second,third};uint[] parameters={0xA214FD39,0xB7B26C62,0x61693ECF};
        for(int i=0;i<3;i++)
        {
            if(_needs[i] is not {} old)
            {Missing($"M13-039 client needs cache +0x{0x2C+i*4:X} read at 0x{(i==0?0x5924C4:i==1?0x592514:0x592560):X8}: checked ctor0x00591E50..0x00591E78 does not supply this value; initial cache input remains unknown, not zero");continue;}
            float value=values[i],delta=value-old;if(delta<0)delta=-delta;
            if(delta<BitConverter.UInt32BitsToSingle(0x3727C5AC))continue;
            _needs[i]=value; // store before SetParameter, including NaN/equal epsilon
            Send(new(Owner,0x18,parameters[i],BitConverter.SingleToUInt32Bits(value),0,0,0));
        }
    }
    private void HandleDimMusicForActivity(byte activity)
    {
        if(activity==Activity)return;
        if(activity==0xF)Post(0xD53B75B2);
        else if(Activity==0xF)Post(0x669A53A8);
    }
    internal void UpdateActivityMusicState(byte activity)
    {
        if(ActivityNameInput is {} names && ActivityLogChannelInput is {} channel)
            _robot.Engine.EmitSourceLog(EngineLogLevel.Info,channel,"RobotAudioClient.SetFreeplayMusic",$"ActivityName '{names(activity)??"(null)"}'");
        else Missing("M13-039 ActivityID EnumToString0x0059202C / RobotAudioClient source channel0x0059201C input absent; activity audio decisions still proceed");
        if(ActivityStates.TryGetValue(activity,out uint value)){Switch(0x972DC63C,0xF0C83E52);Switch(0x8379F505,value);}
        else if(ActivityNameInput is {} lookup && ActivityLogChannelInput is {} sourceChannel)
            _robot.Engine.EmitSourceLog(EngineLogLevel.Info,sourceChannel,"RobotAudioClient.UpdateActivityMusicState.NoFreeplayMusic",$"No freeplay music for ActivityName '{lookup(activity)??"(null)"}'");
    }
    private void HandleGuardDogUpdates(BehaviorPublicState message)
    {
        if(message.StageTag!=1){if(StageTag!=1)GuardStage=5;return;}
        if(StageTag!=1)Switch(0x8379F505,0x3D69880F);
        byte stage=message.Stage(1);
        if(stage==GuardStage)return;
        if(stage>=RoundStates.Length){StackDiagnostics.Write("M1-034 M13-039 GuardDog round-table read 0x005926A8 outside11 words: skip switch/cache tail; prior guard stage retained");return;}
        Switch(0x972DC63C,RoundStates[stage]);GuardStage=stage;
    }
    private void HandleFeedingUpdates(BehaviorPublicState message)
    {
        byte stage=message.Stage(0);
        if(StageTag==0 && stage==FeedingStage)return;
        if(message.StageTag!=0)return;
        FeedingStage=stage;
        if((uint)Round>=RoundStates.Length){StackDiagnostics.Write("M1-034 M13-039 Feeding round-table read 0x00592710 outside11 words: skip switch; earlier feeding-stage store retained");return;}
        Switch(0x972DC63C,RoundStates[Round]);
    }
    private void Switch(uint group,uint value)=>Send(new(Owner,0x14,group,value,1));
    private void Post(uint id)=>Send(new(Owner,8,id,1,0)); // object1, empty callback
    private void Send(BehaviorAudioRequest request)
    {
        if(_robot.BehaviorAudioRecipient is {} recipient)recipient(request);
        else Missing($"M13-039 robot+0x58 audio-controller virtual+0x{request.Slot:X} has no bound object1/device recipient (M6/M9); request0x{request.Id:X8} is not rerouted to singing object7");
    }
    private static void Missing(string text)=>SteppedBehavior.ReportMissing(text);
    private void RunHandler(Action action,string address)
    {
        try{action();}
        catch(Exception error){_robot.Engine.EmitStackLog($"M1-034 M13-039 {Owner} handler at {address}: skip handler remainder after {error.GetType().Name}: {error.Message}");}
    }
    public void Dispose(){lock(_public.Gate){if(_disposed)return;_disposed=true;_handles.Retire();_robot.BehaviorAudioRetiredForTest?.Invoke(Owner);}}
}
