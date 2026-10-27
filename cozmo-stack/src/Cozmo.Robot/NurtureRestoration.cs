using System.Buffers.Binary;
using Cozmo.Robot.Behavior;
using Cozmo.Transport;

namespace Cozmo.Robot;

public sealed record InventoryStatusReport(IReadOnlyList<int> Words);
public sealed record DefaultUnlocksReport(IReadOnlyList<uint> UnlockIds);

// fidelity: M15-034, M15-035
// These are the Robot's actual connection-read recipients, not a second set in a tool.
internal sealed class ProgressionUnlockOwner(EngineRobot robot)
{
    internal readonly SortedSet<uint> Unlocked=new();
    internal bool OverridesKnownEmpty {get;private set;}=true; //0064B86E ctor empty tree, before any NV response.
    internal bool TryIsUnlocked(uint id,out bool unlocked)
    {unlocked=false;if(!OverridesKnownEmpty)return false;unlocked=Unlocked.Contains(id);return true;}
    internal void Init()
    {
        OverridesKnownEmpty=robot.Engine.BackupFiles.InitProgressionDefaults();
        if(!OverridesKnownEmpty)
        {SteppedBehavior.ReportMissing("M15-034 nonempty/malformed freeplayOverrides Init branch 0x0064B928..0x0064B9F8 is unread; remaining Init unit skipped");return;}
        if(robot.Engine.NvStorage!.Read(0x182000,ReadSink)==0)
            robot.Engine.EmitSourceLog(EngineLogLevel.Info,"UnlockComponent","ReadCurrentUnlocksToRobot","Read failed");
    }
    internal void ReadSink(NvResult result)
    {
        if(result.Result==-1)
        {
            robot.Engine.EmitSourceLog(EngineLogLevel.Info,"UnlockComponent","ReadGameUnlocks","No unlock data found on robot unlocking defaults");
            ReplaceDefaults();SendStatus();
            if(!robot.HasExternalInterface)
            {robot.Engine.EmitStackLog("M1-034/M15-034: skipped NotifyGameDefaultUnlocksSet null interface dereference 0x0064C114; installed defaults retained");return;}
            var defaults=BackupFiles.CurrentDefaults();
            robot.Engine.DeliverNurture(0x6E,PackIds(defaults,false),new DefaultUnlocksReport(Array.AsReadOnly(defaults)));
            return;
        }
        if(result.Result<0)
        {
            if(result.Result < -9)
            {SteppedBehavior.ReportMissing("M15-034 ReadGameUnlocks 0x0064CF7A has a null enum-name printf argument; logger treatment is unread");return;}
            Error("Read failed with "+NvStorageComponent.NvResultName(result.Result));return;
        }
        if(result.Data.Length<2048)
        {
            robot.Engine.EmitSourceLog(EngineLogLevel.Info,"Unnamed","ProgressionUnlockComponent.ReadFewerUnlocks","Padding unlock data due to size mismatch");
            robot.Engine.EmitStackLog("M1-034/M15-034: skipped unlock callback restoration; temporary padded data freed at 0x0064CEEA is read by Unpack at 0x0064CF04; prior state kept");return;
        }
        if(result.Data.Length!=2048){Error($"Size mismatch expected 2048, got {result.Data.Length}");return;}
        ReplaceDefaults();
        for(int i=0;i<512;i++)
        {
            uint id=BinaryPrimitives.ReadUInt32LittleEndian(result.Data.AsSpan(i*4,4));
            if(unchecked(id-1)<=0x53)Unlocked.Add(id);
        }
        SendStatus();
    }
    private void ReplaceDefaults(){Unlocked.Clear();Unlocked.UnionWith(BackupFiles.CurrentDefaults());}
    private void Error(string text){robot.Engine.EmitSourceLog(EngineLogLevel.Error,null,"UnlockComponent.ReadGameUnlocks",text);EngineErrorState.StoreAndMaybeBreak();}
    internal void SendStatus()
    {
        if(!robot.HasExternalInterface)return;
        robot.Engine.EmitSourceLog(EngineLogLevel.Info,"UnlockComponent","SendUnlockStatus",$"Sending current unlock status ({Unlocked.Count} unlocked)");
        var ids=Unlocked.ToArray();robot.Engine.DeliverNurture(0x6D,PackIds(ids,true),new BackupUnlockStatus(Array.AsReadOnly(ids),false));
    }
    private static byte[] PackIds(IReadOnlyList<uint> ids,bool trailingFalse)
    {
        var data=new byte[1+4*ids.Count+(trailingFalse?1:0)];data[0]=unchecked((byte)ids.Count);
        for(int i=0;i<ids.Count;i++)BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(1+4*i,4),ids[i]);
        return data;
    }
    internal void Retire()
    {
        SteppedBehavior.ReportMissing("M15-027/M15-034 progression scoped vector+1C and tree+10 concrete recipients remain absent at 0x005111BE..0x005111EE; current unlock tree cleared in checked order, global defaults retained");Unlocked.Clear();OverridesKnownEmpty=false;
    }
}

// fidelity: M15-035
internal sealed class InventoryOwner
{
    private readonly EngineRobot _robot;
    internal readonly int[] Words=new int[64];
    internal readonly SortedDictionary<int,int> Caps=new();
    internal bool Ready;
    internal readonly long ConstructedAtMicros;
    private static int _zeroRulingReported;
    internal static void ResetZeroRulingForTest()=>_zeroRulingReported=0;
    internal InventoryOwner(EngineRobot robot)
    {
        _robot=robot;ConstructedAtMicros=(DateTimeOffset.UtcNow.Ticks-DateTimeOffset.UnixEpoch.Ticks)/10;
        if(Interlocked.Exchange(ref _zeroRulingReported,1)==0)
            robot.Engine.EmitStackLog("M15-035 manager ruling 2026-10-15 (OV28-29-30 check, by analogy with M2-017): never-written inventory words 2..63 are supplied as zero until decoded; the report is retained");
    }
    internal void Init()
    {
        _robot.Engine.EmitSourceLog(EngineLogLevel.Info,"InventoryComponent","InventoryComponent.ReadCurrentInventoryFromRobot","");
        if(_robot.Engine.NvStorage!.Read(0x195000,ReadSink)==0)
            _robot.Engine.EmitSourceLog(EngineLogLevel.Info,"InventoryComponent","InventoryComponent.ReadCurrentInventoryFromRobot.ReadFailed","");
        if(_robot.HasExternalInterface)SteppedBehavior.ReportMissing("M15-035 Inventory subscription helpers 0x0063CB8C..0x0063CDCC are unread; no guessed command handlers registered");
        var path=_robot.Engine.BackupFiles.Resource("config/engine/inventory_config.json");
        if(path is null)return;
        if(!_robot.Engine.BackupFiles.ReadJson(path,out var root))return;
        try
        {
            var caps=Json.Member(root,"InventoryTypeCaps");
            foreach(var pair in new[]{(0,"Invalid"),(1,"Sparks")})
            {var value=Json.Member(caps,pair.Item2);if(value.Kind!=FirmwareJsonKind.Null)Caps[pair.Item1]=Json.AsInt(value);}
        }
        catch(JsonLogicError){_robot.Engine.EmitStackLog("M1-034/M15-035: skipped inventory cap config after terminating JsonCpp lookup/asInt in 0x0063C998..0x0063C9D4; earlier caps retained");}
    }
    internal void ReadSink(NvResult result)
    {
        Ready=true;
        if(result.Result>=0)
            for(int i=0;i<Math.Min(64,result.Data.Length/4);i++)Words[i]=BinaryPrimitives.ReadInt32LittleEndian(result.Data.AsSpan(i*4,4));
        else if(result.Result!=-1)
        {_robot.Engine.EmitSourceLog(EngineLogLevel.Error,null,"InventoryComponent.ReadCurrentInventoryFromRobot.ReadError","");EngineErrorState.StoreAndMaybeBreak();}
        SendStatus();
        if(result.Result==-1&&_robot.HasExternalInterface)_robot.Engine.DeliverNurture(0x70,Array.Empty<byte>(),null);
    }
    internal void SendStatus()
    {
        if(!Ready||!_robot.HasExternalInterface)return;
        var copy=Words.ToArray();var data=new byte[256];
        for(int i=0;i<64;i++)BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(i*4,4),copy[i]);
        _robot.Engine.DeliverNurture(0x6F,data,new InventoryStatusReport(Array.AsReadOnly(copy)));
    }
    internal void Retire(){Caps.Clear();SteppedBehavior.ReportMissing("M15-027/M15-035 Inventory scoped subscription-vector recipients 0x00511314..0x00511338 remain unread; no command callback invoked during retirement");}
}

public sealed partial class EngineRobot
{
    internal ProgressionUnlockOwner? Progression;
    internal InventoryOwner? Inventory;
    private void InitNurtureOwners()
    {
        Progression=new(this);Inventory=new(this);
        var progression=Progression;var inventory=Inventory;
        Lifetime.Bind(0x448,_=>progression.Retire());
        Lifetime.Bind(0x444,_=>inventory.Retire());
    }
}
public sealed partial class CozmoEngine
{
    internal void DeliverNurture(ushort tag,byte[] data,object? report,Action? typed=null)
    {
        DeliverAnimationReportToApp(tag,data,typed,typedFirst:true);
        AnimationMessages.Deliver(tag,true,report);
    }
}
