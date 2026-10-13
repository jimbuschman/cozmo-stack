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
    internal CheckedDockingTest Docking { get; } = new();
    internal CheckedLiftLoadTest Lift { get; }
    internal CheckedDevBehaviorSet(EngineRobot robot,int instance)
    {
        Factory=new(robot); Lift=new(robot);
        // fidelity: M7-028 — one visible MISSING per native logger-owning behaviour instance; no directory stand-in.
        foreach(string name in new[]{"FactoryTest","DockingTestSimple","LiftLoadTest"})
            robot.Engine.EmitStackLog($"MISSING: M7-028 | {name}[{instance}] constructor logger/directory side effects remain unread");
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
    internal byte? CurrentState;
    internal float? StateDeadline;
    internal uint FailureTimestamp;
    internal Dictionary<int,uint> TimestampEntries = new();
    internal bool ResultChanged, ShouldWipe, WritesSent;
    internal long ResultTime;
    internal uint ResultField1D8;
    internal bool IsRunnableInternal => Result==0;
    // The 72-byte logger-owned scratch entry has no checked initialization row; never fabricate its bytes.
    internal record QueuedEntry(uint Tag,int StationId,byte Result,long Time,uint Zero,byte[]? Data);
    internal List<QueuedEntry> QueuedWrites = new();
    internal uint QueueCount;
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
        _robot.Engine.EmitStackLog("MISSING: M7-027 | FactoryTestResultEntry scratch bytes copied at 0x005D257E and Pack 0x005D259C have no checked initialization row; queued metadata retained, no NV write invented");
        WritesSent=false;
        if (CurrentState is { } previousState)
        { if (previousState != 15) StateDeadline = _robot.Engine.Timer.SecondsF + 20f; }
        else _robot.Engine.EmitStackLog("MISSING: M7-027 | FactoryTest initial +0x128 state has no checked row; SetCurrState deadline guard 0x005D14AE..0x005D14CA cannot be resolved");
        CurrentState=15; TimestampEntries[15]=_robot.Engine.Timer.TimeStampMs;
    }
}
// fidelity: M7-027
internal sealed class CheckedDockingTest
{
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
    internal uint Loads, All;
    internal CheckedLiftLoadTest(EngineRobot robot)=>_robot=robot;
    internal void Handle(LiftLoad m)
    {
        if(!Running)return;
        Received=true;HasLoad=m.Field0;All=unchecked(All+1);if(HasLoad)Loads=unchecked(Loads+1);
        _robot.Engine.EmitSourceLog(EngineLogLevel.Debug,"Unnamed","BehaviorLiftLoadTest.HandleLiftLoad.HasLoad",$"{unchecked((int)Loads)} / {unchecked((int)All)}");
    }
}
