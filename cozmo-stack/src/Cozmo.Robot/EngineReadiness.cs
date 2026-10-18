using System.Text;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
namespace Cozmo.Robot;

public sealed record EngineStateTransition(byte OldState,byte NewState);
public sealed record EngineLoadingProgress(float Ratio);
public sealed record EngineStartupFailure(string Reason);

// fidelity: M1-054, M1-055, M1-056, M1-058, M1-059
public sealed partial class CozmoEngine
{
    private byte _engineState;
    private int _loadingProgressBits;
    private Thread? _startupLoader;
    private Exception? _startupLoaderFailure;
    private bool _loadingDone;
    private CozmoAnimations? _startupAnimations;
    private float _lastPolledProgress;
    public byte EngineState=>Volatile.Read(ref _engineState);
    private volatile bool _hostReady;
    /// <summary>The engine-side ready state; native state3 is stored before its host reports are dispatched.</summary>
    public bool IsReady=>EngineState==3 && !StartupFailed;
    /// <summary>True once preceding names/End and the ready notification reach the host dispatcher.</summary>
    public bool IsReadyDelivered=>IsReady && _hostReady;
    // Host-wait plumbing is read at dispatch, so a waiter installed after publication cannot miss readiness.
    internal event Action? ReadyDeliveredInternal;
    private volatile bool _startupFailed;
    public bool StartupFailed=>_startupFailed;
    public string? StartupFailureReason {get;private set;}
    internal bool UiMessageProcessingEnabled {get;private set;}
    internal (string? Language,string? Country) HostLocale;
    public float LoadingProgress=>BitConverter.Int32BitsToSingle(Volatile.Read(ref _loadingProgressBits));
    public event Action<EngineStateTransition>? EngineStateChanged;
    public event Action<EngineLoadingProgress>? EngineLoading;
    public event Action<EngineStartupFailure>? EngineStartupFailed;

    private int InitializeHostUiProcessing(FirmwareJsonValue? config)
    {
        if(config is not null)
        {
            try
            {
                int port=Json.AsInt(Json.Member(config,"RobotAdvertisingPort"));
                if(unchecked((uint)port)>=0x10000)
                    throw new ArgumentException("EngineConfigJson RobotAdvertisingPort must be in the unsigned 16-bit range.");
            }
            catch(JsonLogicError error)
            {throw new ArgumentException("EngineConfigJson RobotAdvertisingPort is not a valid signed integer.",error);}
        }
        else SteppedBehavior.ReportMissing("M1-054 MessageHandler.Init0x0069D0CE: no host engine-config advertising-port input; existing transport remains owned and configured by M1-005");
        // OV126 routines1/3: host API replaces comms slots; queued supported inputs run before the state switch.
        UiMessageProcessingEnabled=true;
        return 0;
    }
    private void StartHostEngine()
    {
        // SetRandomSeed has just run. Copy the host locale inputs before the state guard; no default invented.
        HostLocale=(Options.LabLocaleLanguage,Options.LabLocaleCountry);
        if(HostLocale.Language is null || HostLocale.Country is null)
            SteppedBehavior.ReportMissing("M1-058 StartEngine.SetLocale0x004ECBE2: host locale language/country not supplied; no invented locale default");
        if(_engineState==3)
        { Error("CozmoEngine.HandleMessage.StartEngine.AlreadyStarted","Engine already started.");return; }
        SetEngineState(1);
    }
    internal void SetEngineState(byte state)
    {
        byte old=_engineState;
        if(old==state)return;
        _hostReady=false;
        Volatile.Write(ref _engineState,state); // native store precedes notification.
        var report=new EngineStateTransition(old,state);
        DeliverAnimationReportToApp(0x7B,new[]{old,state},CaptureAppEvent(EngineStateChanged,report),
            beforeDispatch:()=>
            {
                _hostReady=state==3;
                // Pair the subscriber's Interlocked event add/then flag check with a publishing
                // store-load fence: reading the waiter cannot precede visibility of the ready flag.
                Thread.MemoryBarrier();
                if(state==3)ReadyDeliveredInternal?.Invoke();
            });
        AnimationMessages.Deliver(0x7B,true,report);
        EmitSourceLog(EngineLogLevel.Event,null,"app.engine.state",EngineStateName(old),
            new Dictionary<string,string>{{"$data",EngineStateName(state)}});
    }
    private static string EngineStateName(byte value)=>value switch
    {0=>"Stopped",1=>"WaitingForUIDevices",2=>"LoadingData",3=>"Running",4=>"UpdatingFirmware",_=>"UNKNOWN"};
    private bool HasDesiredHostDevice()
    {
        uint connected=UiMessageProcessingEnabled?1u:0u,desired=0;
        return connected!=0 && connected>=desired; // Direct desired0, M1-058's one connected API host.
    }
    internal void ConfigureStartupLoaders(CozmoAnimations animations)
    {
        _startupAnimations=animations;
        NativeNonConfigLoadingActive=()=>_engineState==2 && !StartupFailed;
        NativeDoNonConfigDataLoading=PollNonConfigLoading;
        NativeSetEngineState=SetEngineState;
    }
    private void AddLoadingProgress(float amount)
    {
        int before,after;
        do
        {
            before=Volatile.Read(ref _loadingProgressBits);
            after=BitConverter.SingleToInt32Bits(BitConverter.Int32BitsToSingle(before)+amount);
        }while(Interlocked.CompareExchange(ref _loadingProgressBits,after,before)!=before);
    }
    internal (int Result,float Progress) PollNonConfigLoading()
    {
        float progress=LoadingProgress; // copy-out first, before done/start/join tests.
        if(_loadingDone)return (1,progress);
        if(Volatile.Read(ref _startupLoaderFailure) is {} failure)
        {FailStartup(failure.Message);return (-1,progress);}
        if(_startupLoader is {} worker)
        {
            if(progress<1f)return (0,progress); // native BMI: unordered does not take this return.
            worker.Join();_startupLoader=null;_loadingDone=true;
            return (1,progress);
        }
        if(Options.ResourcesPath is not {} root)
        {FailStartup("ResourcesPath is missing; M1-058/M1-034 replaces native loading hang at0x0051F394.");return (-1,progress);}
        if(_startupAnimations is not {} animations)
        {FailStartup("M1-055: startup animation owner is unavailable.");return (-1,progress);}
        _startupLoader=new Thread(()=>
        {
            try
            {
                var library=animations.LoadForEngine(root,AddLoadingProgress);
                // M1-055 operator2026-10-17: stack definition order, exact procedural exclusion and first-wins.
                NativeAnimationNameNode? head=null;
                foreach(string name in library.ClipNames.Reverse())
                    if(name!="_PROCEDURAL_")head=new NativeAnimationNameNode(Encoding.UTF8.GetBytes(name),head);
                Robots.NativeCannedAnimationNames=new(head);
                if(!_disposed)Volatile.Write(ref _loadingProgressBits,BitConverter.SingleToInt32Bits(1f));
            }
            catch(Exception error){Volatile.Write(ref _startupLoaderFailure,error);}
        }){IsBackground=true,Name="cozmo-startup-loader"};
        _startupLoader.Start();
        return (0,progress);
    }
    private void PublishLoadingProgress()
    {
        var report=new EngineLoadingProgress(_lastPolledProgress);
        // EngineLoadingDataStatus tag is kept at the typed API boundary until its serializer tag is checked here.
        QueueAppEvent(EngineLoading,report);
    }
    private void FailStartup(string reason)
    {
        if(StartupFailed)return;
        StartupFailureReason=reason;_startupFailed=true;
        // Same handoff as ready: publish the flag before capturing the failure subscribers.
        Thread.MemoryBarrier();
        EmitStackLog("Engine startup failed: "+reason);
        var report=new EngineStartupFailure(reason);
        QueueAppEvent(EngineStartupFailed,report);
    }
    private void StopStartupLoading()
    {
        var worker=_startupLoader;
        if(worker is not null && worker!=Thread.CurrentThread)worker.Join();
        _startupLoader=null;
    }
}
