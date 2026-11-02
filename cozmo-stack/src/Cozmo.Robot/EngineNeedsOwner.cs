using Cozmo.Robot.Behavior;
namespace Cozmo.Robot;

public sealed partial class EngineRobot
{
    // Robot+354, initialized false by 005100EA.
    internal bool NativeDoingCSharpTrick { get; set; }
}

// fidelity: M1-054, M15-001, M15-014, M15-016, M1-024, M1-042
public sealed partial class CozmoEngine
{
    public NeedsManager Needs { get; private set; } = null!;
    private long _needsCurrentConstructorMicros,_needsCandidateConstructorMicros;
    internal LocalNotifications? NativeLocalNotifications {get;private set;}
    internal long NeedsWallClockMicros()=>Options.NeedsSystemClockMicroseconds?.Invoke() ?? (DateTimeOffset.UtcNow.Ticks-DateTimeOffset.UnixEpoch.Ticks)/10;
    private void CaptureNativeNeedsConstructorTimes()
    {
        _needsCurrentConstructorMicros=NeedsWallClockMicros();
        _needsCandidateConstructorMicros=NeedsWallClockMicros();
    }
    // CozmoContext+4 is constructed independently of RobotManager/Needs; absence is an explicit host test input.
    internal bool NeedsContextExternalInterfaceAvailable {get;set;}=true;
    internal bool NeedsContextAlive=>!_disposed;
    internal bool IsOfflineEngine { get; private set; }
    private bool _requestedInitialBlockPool;
    internal Action? RequestInitialBlockPool;

    private void InitializeNeedsOwner()
    {
        // InitExperiments has just run; RobotManager.Init follows (0x004ECA0A before 0x004ECA1A).
        EmitSourceLog(EngineLogLevel.Info,"Unnamed","NeedsManager.Init","Starting Init of NeedsManager"); // 0x00692598.
        Needs=NeedsManager.FromObb(Options.ResourcesPath,()=>Timer.SecondsF,
            deviceDirectory:Options.NeedsDeviceDirectory,contextRandom:ContextRandom,beforeInit:n=>
            {
                Needs=n; // context retains this same logical owner before its Init callbacks
                n.HostEngine=this;n.NativeContextBound=true;
                n.SourceLog=(level,channel,key,text)=>EmitSourceLog(level,channel,key,text);
                n.State.PartDamageLog=(value,data)=>EmitSourceLog(EngineLogLevel.Event,null,"needs.part_damaged",value,data);
                n.TickCount=()=>Timer.TickCount;
                n.PossiblyWriteLabAssignments=Experiments.PossiblyWriteLabAssignmentsToRobot;
                BindNativeNotifications(n);
            },systemClockMicroseconds:NeedsWallClockMicros,constructorTimes:(_needsCurrentConstructorMicros,_needsCandidateConstructorMicros));
        Experiments.AttachInitializedNeeds(Needs);
        Needs.Connected=false; // native constructor manager+4 is null until InitAfterConnection (0x0069211A, 0x00694388)
        Needs.AttachConnectionRead(this);
        ConnectToRobotHandled+=()=>Needs.InitAfterConnection();
        SerialNumberAcquired+=serial=>Needs.InitAfterSerialNumberAcquired(serial);
        NeedsUpdate=Needs.Update; // Only called by the existing state-3 engine tick, never during construction.
        NeedsSetPaused=Needs.SetPaused;
        NeedsRobotDisconnected=Needs.OnRobotDisconnected;
        AnimationMessages.SubscribeMessage(0xC6,false,p=>Needs.ForceSetNeedsLevels((IReadOnlyList<float>)p!));
        SteppedBehavior.ReportMissing("M15-001 NeedsManager.Init: remaining registrations 0x00692668..0x006926C8 and DAS app_start tail 0x00693486 remain unbuilt/uncertified; full StarRewardsConfig and runtime tuning lifecycle remain incomplete");
        if(Options.NeedsDeviceDirectory is null)
            SteppedBehavior.ReportMissing("M15-001 NeedsManager.InitInternal 0x00693466/0x0069347E: host nurture directory not supplied; device read/write remain unbound");
    }

    internal void BindNeedsRobotServices()=>Needs.NvStorage=NvStorage;
    internal void BindNativeNotifications(NeedsManager needs)
    {
        var notifications=new LocalNotifications(this,needs);NativeLocalNotifications=notifications;
        string? path=Options.ResourcesPath is {} root?Path.Combine(root,"config","engine","local_notification_config.json"):null;
        string? json=path is not null&&File.Exists(path)?File.ReadAllText(path):null;
        if(notifications.Initialize(json,needs.NativeInitializationTime))
        {
            needs.TryLocalNotificationsGenerate=notifications.TryGenerate;
            needs.LocalNotificationsUpdate+=()=>notifications.Update(needs.NowSec);
            needs.LocalNotificationsSetPaused+=notifications.SetPaused;
            // fidelity: M15-111 — the host's history, else the persisted store; both arrive on 0xD6 before Needs Init.
            var history=Options.NotificationHistory;
            if(history is null&&!Options.OmitNotificationHistoryForTests&&Options.NeedsDeviceDirectory is {} directory)
            {_notificationHistory=NotificationHistoryStore.LoadOrCreate(directory,NeedsWallClockMicros(),EmitStackLog);history=_notificationHistory.Snapshot();}
            if(history is not null)AnimationMessages.Deliver(0xD6,false,history);
        }
    }
    private NotificationHistoryStore? _notificationHistory;
    // fidelity: M15-111 — UnlockablesManager.HandleOnUnlockRequestSuccess, on the unlock result report.
    internal void RecordNotificationUnlock(uint id)
    {if(_notificationHistory is {} store&&store.RecordUnlock(id,NeedsWallClockMicros()))SetNotificationDeterminants(store.Snapshot());}

    // Explicit unit adapter; production ownership cannot be replaced by a Freeplay handle.
    internal void UseNeedsForOfflineTest(NeedsManager needs)
    {
        if(!IsOfflineEngine)throw new InvalidOperationException("Needs is owned by production engine initialization.");
        NativeLocalNotifications?.Dispose();NativeLocalNotifications=null;
        needs.EnableUncertifiedPersistenceForTests(); // Explicit offline unit adapter, never a production capability.
        Needs.DetachConnectionRead();
        Needs=needs;
        Needs.HostEngine=this;Needs.NativeContextBound=true;
        Needs.AttachConnectionRead(this);
        BindNeedsRobotServices();
        NeedsUpdate=Needs.Update;NeedsSetPaused=Needs.SetPaused;NeedsRobotDisconnected=Needs.OnRobotDisconnected;
        if(Robot is not null)Needs.InitAfterConnection();
        if(AcquiredSerialNumber is {} serial)Needs.InitAfterSerialNumberAcquired(serial);
    }

    /// <summary>Supplies an existing persisted determinant tuple to the context-owned D6 recipient.</summary>
    public void SetNotificationDeterminants(NotificationDeterminants history)
    {
        ArgumentNullException.ThrowIfNull(history);
        if(history.UnlockTimestamps.Count!=85||history.HasPlayed.Count!=85)throw new ArgumentException("Notification history requires exactly 85 timestamps and 85 played values.",nameof(history));
        var copy=new NotificationDeterminants(history.InstallSeconds,history.UnlockTimestamps.ToArray(),history.HasPlayed.ToArray());
        Post(()=>{if(NeedsContextAlive)AnimationMessages.Deliver(0xD6,false,copy);});
    }
    public void RequestNotificationTextKeys()=>Post(()=>{if(NeedsContextAlive)AnimationMessages.Deliver(0xD5,false,null);});
    public void NotifyNotificationManagerReady()=>Post(()=>{if(NeedsContextAlive)AnimationMessages.Deliver(0xD4,false,null);});
}
