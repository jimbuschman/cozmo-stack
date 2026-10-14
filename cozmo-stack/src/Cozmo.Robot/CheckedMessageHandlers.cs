using System.Buffers.Binary;
using System.Text;
using Cozmo.Protocol;
namespace Cozmo.Robot;

// fidelity: M2-019 — MessageEngineToGame tag 0x2A, source string-field order.
public sealed record EngineRobotCLADVersionMismatch(bool EngineToRobotMismatch, bool RobotToEngineMismatch,
    string EngineEngineToRobot, string EngineRobotToEngine, string RobotEngineToRobot, string RobotRobotToEngine)
{ public byte Tag => 0x2A; }
public sealed partial class CozmoEngine
{
    public event Action<EngineRobotCLADVersionMismatch>? CLADVersionMismatch;
    internal void BroadcastCLADVersionMismatch(EngineRobotCLADVersionMismatch message) => FanOut(CLADVersionMismatch, message);
}
public sealed partial class EngineRobot
{
    // fidelity: M2-019 — copied on EVERY D2; mismatch flags cleared only by the constructor.
    internal byte[] LastFWVersionPayload { get; private set; } = new byte[44];
    internal bool EngineToRobotHashMismatch, RobotToEngineHashMismatch;
    private static string HashText(byte[] bytes) => string.Concat(bytes.Select(b => $" {b:X2}"));
    private void HandleFWVersionInfo(FWVersionInfo message)
    {
        LastFWVersionPayload = message.ToBytes()[1..];
        byte[] e2r = Convert.FromHexString("D53542A17BB89769D8094ECE5A964A9E");
        byte[] r2e = Convert.FromHexString("AB12BABA15729540B41D23167F2459A2");
        string engineE = "", engineR = "", robotE = "", robotR = "";
        if (!message.Field3.AsSpan().SequenceEqual(e2r))
        {
            engineE = HashText(e2r); robotE = HashText(message.Field3);
            Engine.EmitSourceLog(EngineLogLevel.Warning, null, "RobotFirmware.VersionMismatch",
                $"Engine to Robot CLAD version hash mismatch. Robot's EngineToRobot hash = {robotE}. Engine's EngineToRobot hash = {engineE}.");
            EngineToRobotHashMismatch = true;
        }
        if (!message.Field4.AsSpan().SequenceEqual(r2e))
        {
            engineR = HashText(r2e); robotR = HashText(message.Field4);
            Engine.EmitSourceLog(EngineLogLevel.Warning, null, "RobotFirmware.VersionMismatch",
                $"Robot to Engine CLAD version hash mismatch. Robot's RobotToEngine hash = {robotR}. Engine's RobotToEngine hash = {engineR}.");
            RobotToEngineHashMismatch = true;
        }
        if (EngineToRobotHashMismatch || RobotToEngineHashMismatch)
            Engine.BroadcastCLADVersionMismatch(new(EngineToRobotHashMismatch, RobotToEngineHashMismatch, engineE, engineR, robotE, robotR));
    }
    // fidelity: M2-019 — signed enum byte; phone system printf NULL spelling (checked row).
    private void HandleDockingStatus(DockingStatus message)
    {
        int status = unchecked((sbyte)message.Field1);
        string text = status switch { 0 => "STATUS_BACKING_UP", 1 => "STATUS_DOING_HANNS_MANEUVER", _ => "(null)" };
        Engine.EmitSourceLog(EngineLogLevel.Event, null, "robot.docking.status", text);
    }

    // fidelity: M7-027 — two BehaviorContainers, manager's instances before system manager's.
    internal CheckedDevBehaviorSet[] DevBehaviors { get; private set; } = Array.Empty<CheckedDevBehaviorSet>();
    internal void ReleaseCheckedDevStorage(){foreach(var set in DevBehaviors)set.Lift.ReleaseSubscriptionStorage();}
    private void InitCheckedDevSubscribers()
    {
        DevBehaviors = new[] { new CheckedDevBehaviorSet(this, 0), new CheckedDevBehaviorSet(this, 1) };
        for (int i=0;i<2;i++)
        {
            var set=DevBehaviors[i]; string suffix=$"[{i}]";
            AddMessage(RobotMessageId.FactoryTestParam, m=>set.Factory.Handle(m), "FactoryTest"+suffix);
            AddMessage(RobotMessageId.ActiveObjectAvailable, m=>set.Factory.Handle(m), "FactoryTest"+suffix);
            AddMessage(RobotMessageId.PickAndPlaceResult, m=>set.Factory.Handle(m), "FactoryTest"+suffix);
            AddMessage(RobotMessageId.FirmwareVersion, m=>set.Factory.Handle(m), "FactoryTest"+suffix);
            AddMessage(RobotMessageId.FactoryFirmwareVersion, m=>set.Factory.Handle(m), "FactoryTest"+suffix);
            AddMessage(RobotMessageId.DockingStatus, m=>set.Docking.Handle((DockingStatus)m), "DockingTestSimple"+suffix);
            AddMessage(RobotMessageId.LiftLoad, m=>set.Lift.Handle((LiftLoad)m), "LiftLoadTest"+suffix);
        }
    }
}
// fidelity: M7-027
internal sealed class CheckedDevBehaviorSet
{
    internal CheckedFactoryTest Factory { get; }
    internal CheckedDockingTest Docking { get; }
    internal CheckedLiftLoadTest Lift { get; }
    internal CheckedDevBehaviorSet(EngineRobot robot,int instance)
    {
        Factory=new(robot);Docking=new(robot);Lift=new(robot,instance);
    }
}
// fidelity: M7-027 — checked handler fields only; no guessed activation/update/logger lifecycle.
internal sealed class CheckedFactoryTest
{
    private readonly EngineRobot _robot;
    internal int StationId=-1;
    internal bool SawCubeOrCharger, PickAndPlacePassed, HeaderMismatch;
    internal string Version="", Build="";
    internal byte Result;
    internal byte? CurrentState=0; // 0x005D0456, strb +0x128.
    internal float? StateDeadline;
    internal uint FailureTimestamp;
    internal Dictionary<int,uint> TimestampEntries = new();
    internal bool ResultChanged, ShouldWipe, WritesSent;
    internal long ResultTime;
    internal uint ResultField1D8;
    internal bool IsRunnableInternal => Result==0;
    // The 72-byte source timestamp array +0x230 has unread initial contents; never fabricate its bytes.
    internal record QueuedEntry(uint Tag,int StationId,byte Result,long Time,uint Zero,byte[]? Data);
    internal List<QueuedEntry> QueuedWrites = new();
    internal uint QueueCount;
    // fidelity: M7-028 — FactoryTestLogger(true), JSON mode, memory only; no StartLog/CloseLog body claim.
    internal readonly CheckedFactoryLogger Logger=new(true);
    internal CheckedFactoryTest(EngineRobot robot) => _robot=robot;
    private void Log(EngineLogLevel level,string key,string text,string? channel=null) => _robot.Engine.EmitSourceLog(level,channel,key,text);
    internal void Handle(RobotMessage message)
    {
        switch(message)
        {
            case FactoryTestParameter p:
                Log(EngineLogLevel.Info,"BehaviorFactoryTest.HandleFactoryTestParameter.Recvd",$"StationID: {unchecked((int)p.Field0)}","Unnamed");
                StationId=unchecked((int)p.Field0); break;
            case ObjectAvailable a:
                if(a.ObjectType is ObjectType.Block_LIGHTCUBE1 or ObjectType.Block_LIGHTCUBE2 or ObjectType.Block_LIGHTCUBE3 or ObjectType.Charger_Basic)SawCubeOrCharger=true;
                break;
            case PickAndPlaceResult p: PickAndPlacePassed=p.Field1 && p.Field3==2; break;
            case FWVersionInfo: break; // Native 0x005D0BD4 is bx lr.
            case FirmwareVersion f:
                if (f.Signature.Length == 0)
                { _robot.Engine.EmitStackLog("MISSING: M7-027 | FactoryTest Json::Reader::parse on an empty firmware vector remains unread"); break; }
                try
                {
                    if(!Json.TryParseFirst(f.Signature,out var doc)) { EndParseFailure(); break; }
                    using(doc)
                    {
                        var root=doc.RootElement;
                        if(root.Kind is not (FirmwareJsonKind.Null or FirmwareJsonKind.Object))throw new JsonLogicError("operator[] requires objectValue");
                        FirmwareJsonValue Get(string key)=>root.Members.GetValueOrDefault(Convert.ToHexString(Encoding.UTF8.GetBytes(key)))??new();
                        Version=Encoding.UTF8.GetString(Json.AsStringBytes(Get("version")));
                        Build=Encoding.UTF8.GetString(Json.AsStringBytes(Get("build")));
                        if(Version!="F1.5.1" || Build!="FACTORY")HeaderMismatch=true;
                    }
                }
                catch(Exception e) when(e is JsonLogicError or JsonRuntimeError)
                { _robot.Engine.EmitStackLog($"M1-034 / M7-027: skipping FactoryTest firmware handler fatal JSON path: {e.Message}"); }
                break;
        }
    }
    private void EndParseFailure()
    {
        const string resultText="PARSE_HEADER_FAILED"; // EnumToString 0x00777B80, table 0x01033A00[0x46].
        if(Result!=0)
        { Log(EngineLogLevel.Warning,"BehaviorFactoryTest.EndTest.TestAlreadyComplete",$"Existing result {resultText} (new result {resultText})");return; }
        Result=0x46;
        Log(EngineLogLevel.Info,"BehaviorFactoryTest.EndTest.PreWriteResult",resultText,"Unnamed");
        FailureTimestamp=_robot.Engine.Timer.TimeStampMs; TimestampEntries[17]=FailureTimestamp;
        _robot.ActionList.Cancel(-1);
        // Checked C2: prev is false here, cur=(0x46==1) false; SendQueuedWrites/WipeFactory unreachable.
        ResultChanged=false; ShouldWipe=false;
        Log(EngineLogLevel.Info,"BehaviorFactoryTest.EndTest.PassFailStatus","prevPassed: 0, currPassed: 0","Unnamed");
        ResultField1D8=0;
        ResultTime=DateTimeOffset.UtcNow.ToUnixTimeSeconds(); // phone time(NULL) is a system-library input.
        QueuedWrites.Add(new(0x80000010,StationId,Result,ResultTime,ResultField1D8,null)); QueueCount=unchecked(QueueCount+1);
        _robot.Engine.EmitStackLog("MISSING: M7-027 | FactoryTestResultEntry 0x48 bytes copied from timestamp array +0x230 at 0x005D257E have unread initial contents; Pack 0x005D259C remains unbuilt, queued metadata retained, no NV write invented");
        WritesSent=false;
        if (CurrentState is { } previousState)
        { if (previousState != 15) StateDeadline = _robot.Engine.Timer.SecondsF + 20f; }
        CurrentState=15; TimestampEntries[15]=_robot.Engine.Timer.TimeStampMs;
    }
}
// fidelity: M7-027
internal sealed class CheckedDockingTest
{
    // fidelity: M7-028 — pathToResource(scope 2), no normalization, ignored CreateDirectory result; no file opens.
    internal string LoggerDirectory {get;}
    internal string LoggerExtension=>".log";
    internal uint LoggerSize=>0x1400000;
    internal CheckedDockingTest(EngineRobot robot){LoggerDirectory=CozmoEngine.AnimationCacheFile(robot.Engine.Options.AnimationCachePath,"dockingTest");_ = EngineFileUtils.CreateDirectory(LoggerDirectory);}
    internal uint Field258, BackingUpCount, Field260, Field268;
    internal bool DidHanns;
    internal void Handle(DockingStatus m) { if(m.Field1==0)BackingUpCount=unchecked(BackingUpCount+1);else if(m.Field1==1)DidHanns=true; }
    internal void InitInternal() { Field258=BackingUpCount=Field260=Field268=0;DidHanns=false; }
}
// fidelity: M7-027 — IBehavior +0x60 no-op, +0x6C only running, +0x78 no-op.
internal sealed class CheckedLiftLoadTest
{
    private readonly EngineRobot _robot;
    internal bool Running, Received, HasLoad;
    internal uint Loads, All; // +0x220, +0x21C.
    // fidelity: M7-028 — checked L4 constructor fields and two external-interface subscriptions.
    internal uint Field11C,Field120,Field124;
    internal byte Field218,Field219=1;
    internal ushort Field224=>(ushort)((Received?1:0)|((HasLoad?1:0)<<8));
    internal string LoggerDirectory {get;}
    internal string LoggerExtension=>".log";
    internal uint LoggerSize=>0x1400000;
    private readonly SubscriptionOwners _externalSubscriptions=new();
    internal CheckedLiftLoadTest(EngineRobot robot,int instance)
    {
        _robot=robot;LoggerDirectory=CozmoEngine.AnimationCacheFile(robot.Engine.Options.AnimationCachePath,"liftLoadTest");_ = EngineFileUtils.CreateDirectory(LoggerDirectory);
        _externalSubscriptions.Add(robot.Engine.AnimationMessages.SubscribeMessage(0x35,true,_=>robot.Engine.EmitStackLog($"MISSING: M7-028 LiftLoadTest[{instance}] 0x35 callback body remains unread")));
        _externalSubscriptions.Add(robot.Engine.AnimationMessages.SubscribeMessage(0x95,false,_=>robot.Engine.EmitStackLog($"MISSING: M7-028 LiftLoadTest[{instance}] 0x95 callback body remains unread")));
        robot.Engine.EmitStackLog($"MISSING: M7-028 LiftLoadTest[{instance}] IBehavior base constructor 0x005D3F18 and subscription destructor ordering remain unread; managed subscription storage retires after robot lifetime teardown");
    }
    internal void ReleaseSubscriptionStorage()=>_externalSubscriptions.Retire();
    internal void Handle(LiftLoad m)
    {
        if(!Running)return;
        Received=true;HasLoad=m.Field0;All=unchecked(All+1);if(HasLoad)Loads=unchecked(Loads+1);
        _robot.Engine.EmitSourceLog(EngineLogLevel.Debug,"Unnamed","BehaviorLiftLoadTest.HandleLiftLoad.HasLoad",$"{unchecked((int)Loads)} / {unchecked((int)All)}");
    }
}
// fidelity: M7-028 — checked L5 constructor mode. StartLog/CloseLog remain unread, no files are opened.
internal sealed class CheckedFactoryLogger(bool jsonMode)
{
    internal readonly bool JsonMode=jsonMode;
    internal readonly FirmwareJsonValue Root=new(); // Json::Value(nullValue), 0x006716D0..0x006716D6.
    internal string Extension=>JsonMode?".json":".log";
    internal bool FileOpen=>false;
}
