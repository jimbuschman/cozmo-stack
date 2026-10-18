using Cozmo.Robot.Behavior;
namespace Cozmo.Robot;

// fidelity: M1-054, M15-001, M15-014, M15-016, M1-024, M1-042
public sealed partial class CozmoEngine
{
    public NeedsManager Needs { get; private set; } = null!;
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
                n.HostEngine=this;
                n.SourceLog=(level,channel,key,text)=>EmitSourceLog(level,channel,key,text);
                n.State.PartDamageLog=(value,data)=>EmitSourceLog(EngineLogLevel.Event,null,"needs.part_damaged",value,data);
                n.TickCount=()=>Timer.TickCount;
                n.PossiblyWriteLabAssignments=Experiments.PossiblyWriteLabAssignmentsToRobot;
            });
        Needs.AttachConnectionRead(this);
        ConnectToRobotHandled+=()=>Needs.InitAfterConnection();
        SerialNumberAcquired+=serial=>Needs.InitAfterSerialNumberAcquired(serial);
        NeedsUpdate=Needs.Update; // Only called by the existing state-3 engine tick, never during construction.
        NeedsSetPaused=Needs.SetPaused;
        NeedsRobotDisconnected=Needs.OnRobotDisconnected;
        AnimationMessages.SubscribeMessage(0xC6,false,p=>Needs.ForceSetNeedsLevels((IReadOnlyList<float>)p!));
        SteppedBehavior.ReportMissing("M15-001 NeedsManager.Init: StarRewardsConfig 0x00692612, LocalNotifications Init/Generate 0x0069264A/0x00693492, remaining registrations 0x00692668..0x006926C8, full NeedsState envelope 0x0069383C and DAS app_start tail 0x00693486 remain unbuilt/uncertified");
        if(Options.NeedsDeviceDirectory is null)
            SteppedBehavior.ReportMissing("M15-001 NeedsManager.InitInternal 0x00693466/0x0069347E: host nurture directory not supplied; device read/write remain unbound");
    }

    internal void BindNeedsRobotServices()=>Needs.NvStorage=NvStorage;

    // Explicit unit adapter; production ownership cannot be replaced by a Freeplay handle.
    internal void UseNeedsForOfflineTest(NeedsManager needs)
    {
        if(!IsOfflineEngine)throw new InvalidOperationException("Needs is owned by production engine initialization.");
        needs.EnableUncertifiedPersistenceForTests(); // Explicit offline unit adapter, never a production capability.
        Needs.DetachConnectionRead();
        Needs=needs;
        Needs.HostEngine=this;
        Needs.AttachConnectionRead(this);
        BindNeedsRobotServices();
        NeedsUpdate=Needs.Update;NeedsSetPaused=Needs.SetPaused;NeedsRobotDisconnected=Needs.OnRobotDisconnected;
        if(Robot is not null)Needs.InitAfterConnection();
        if(AcquiredSerialNumber is {} serial)Needs.InitAfterSerialNumberAcquired(serial);
    }
}
