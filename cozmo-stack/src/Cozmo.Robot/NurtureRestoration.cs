using System.Buffers.Binary;
using Cozmo.Robot.Behavior;
using Cozmo.Transport;

namespace Cozmo.Robot;

public sealed record InventoryStatusReport(IReadOnlyList<int> Words);
public sealed record InventoryAmountRequest(uint Type,int Amount);
public sealed record DefaultUnlocksReport(IReadOnlyList<uint> UnlockIds);
public sealed record RequestSetUnlockMessage(uint UnlockId,bool Unlocked);
public sealed record RequestSetUnlockResultReport(uint UnlockId,bool Unlocked);

// fidelity: M15-034, M15-035, M15-095
// These are the Robot's actual connection-read recipients, not a second set in a tool.
internal sealed class ProgressionUnlockOwner(EngineRobot robot)
{
    internal readonly SortedSet<uint> Unlocked=new();
    private RetainedSubscription? _requestSubscription;
    internal bool OverridesKnownEmpty {get;private set;}=true; //0064B86E ctor empty tree, before any NV response.
    internal bool TryIsUnlocked(uint id,out bool unlocked)
    {unlocked=false;if(!OverridesKnownEmpty)return false;unlocked=Unlocked.Contains(id);return true;}
    private bool Current=>!robot.Lifetime.Destroying && ReferenceEquals(robot.Progression,this) && ReferenceEquals(robot.Engine.Robot,robot);
    // 0x64C200: ordinary changes write immediately; All and Defaults recurse in ID order.
    internal void SetUnlock(uint id,bool requested)
    {
        if(!Current)return;
        if(id is 1 or 2)
        {
            for(uint child=3;child<=84;child++)
            {
                if(!Current)return;
                if(NativeUnlockNames.Lookup((int)child) is not null)
                    SetUnlock(child,id==1?requested:Array.IndexOf(BackupFiles.CurrentDefaults(),child)>=0);
            }
            if(id==2 && Current)SendDefaults(BackupFiles.CurrentDefaults());
            return;
        }
        if(id<3 || id>84 || NativeUnlockNames.Lookup((int)id) is null)return;
        bool changed=requested?Unlocked.Add(id):Unlocked.Remove(id);
        if(!changed)return;
        byte[] snapshot=new byte[2048];int index=0;
        foreach(uint value in Unlocked)BinaryPrimitives.WriteUInt32LittleEndian(snapshot.AsSpan(index++*4,4),value);
        int accepted=robot.Engine.NvStorage!.Write(0x182000,snapshot,result=>WriteComplete(id,requested,result),false);
        if(Current && accepted==0)robot.Engine.EmitSourceLog(EngineLogLevel.Info,"UnlockComponent","WriteCurrentUnlocksToRobot","Write failed");
    }
    private void WriteComplete(uint id,bool requested,NvResult result)
    {
        if(!Current)return;
        if(result.Result<0)
        {
            robot.Engine.EmitSourceLog(EngineLogLevel.Error,null,"UnlockComponent.WriteCurrentUnlocksToRobot", "Write failed with "+NvStorageComponent.NvResultName(result.Result));
            if(Current)EngineErrorState.StoreAndMaybeBreak();
            return;
        }
        if(!robot.Engine.NeedsContextExternalInterfaceAvailable)return;
        byte[] body=new byte[5];BinaryPrimitives.WriteUInt32LittleEndian(body,id);body[4]=requested?(byte)1:(byte)0;
        robot.Engine.DeliverNurture(0x6C,body,new RequestSetUnlockResultReport(id,requested));
        if(requested&&Current)robot.Engine.RecordNotificationUnlock(id);
    }
    private void SendDefaults(uint[] defaults)
    {
        if(!robot.Engine.NeedsContextExternalInterfaceAvailable)
        {robot.Engine.EmitStackLog("M1-034/M15-095 skipped NotifyGameDefaultUnlocksSet null interface dereference 0x0064C114");return;}
        robot.Engine.DeliverNurture(0x6E,PackIds(defaults,false),new DefaultUnlocksReport(Array.AsReadOnly(defaults)));
    }
    internal void Init()
    {
        OverridesKnownEmpty=robot.Engine.BackupFiles.InitProgressionDefaults();
        if(!OverridesKnownEmpty)
        {SteppedBehavior.ReportMissing("M15-034 nonempty/malformed freeplayOverrides Init branch 0x0064B928..0x0064B9F8 is unread; remaining Init unit skipped");return;}
        if(robot.Engine.NvStorage!.Read(0x182000,ReadSink)==0)
            robot.Engine.EmitSourceLog(EngineLogLevel.Info,"UnlockComponent","ReadCurrentUnlocksToRobot","Read failed");
        if(robot.Engine.NeedsContextExternalInterfaceAvailable && !robot.Lifetime.Destroying)
            _requestSubscription=robot.Engine.AnimationMessages.SubscribeMessage(0xD7,false,p=>
            {
                if(p is not RequestSetUnlockMessage request || !Current)return;
                if(unchecked(request.UnlockId-1)<84)SetUnlock(request.UnlockId,request.Unlocked);
                else robot.Engine.EmitSourceLog(EngineLogLevel.Info,"UnlockComponent","HandleRequestSetUnlock",$"Invalid unlockId {unchecked((int)request.UnlockId)}, ignoring");
            });
    }
    internal void ReadSink(NvResult result)
    {
        if(result.Result==-1)
        {
            robot.Engine.EmitSourceLog(EngineLogLevel.Info,"UnlockComponent","ReadGameUnlocks","No unlock data found on robot unlocking defaults");
            ReplaceDefaults();SendStatus();
            if(!robot.Engine.NeedsContextExternalInterfaceAvailable)
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
        if(!robot.Engine.NeedsContextExternalInterfaceAvailable)return;
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
        _requestSubscription?.Dispose();_requestSubscription=null;
        SteppedBehavior.ReportMissing("M15-027/M15-034 progression remaining scoped vector+1C and tree+10 concrete recipients remain absent at 0x005111BE..0x005111EE; current unlock tree cleared in checked order, global defaults retained");Unlocked.Clear();OverridesKnownEmpty=false;
    }
}

// fidelity: M15-035, M15-094
internal sealed class InventoryOwner
{
    private readonly EngineRobot _robot;
    internal readonly int[] Words=new int[64];
    internal readonly SortedDictionary<int,int> Caps=new();
    internal bool Ready;
    internal readonly long ConstructedAtMicros;
    private static int _zeroRulingReported;
    private readonly List<RetainedSubscription> _subscriptions=new();
    private long _lastWriteMicros;
    internal long LastWriteMicrosForTest=>_lastWriteMicros;
    internal Func<long> ClockMicros=()=> (DateTimeOffset.UtcNow.Ticks-DateTimeOffset.UnixEpoch.Ticks)/10;
    internal bool PendingWrite,WriteInFlight;
    private bool Current=>!_robot.Lifetime.Destroying && ReferenceEquals(_robot.Inventory,this) && ReferenceEquals(_robot.Engine.Robot,_robot);
    internal static void ResetZeroRulingForTest()=>_zeroRulingReported=0;
    internal InventoryOwner(EngineRobot robot)
    {
        _robot=robot;ConstructedAtMicros=ClockMicros();_lastWriteMicros=ConstructedAtMicros;
        if(Interlocked.Exchange(ref _zeroRulingReported,1)==0)
            robot.Engine.EmitStackLog("M15-035 manager ruling 2026-10-15 (OV28-29-30 check, by analogy with M2-017): never-written inventory words 2..63 are supplied as zero until decoded; the report is retained");
    }
    internal void Init()
    {
        _robot.Engine.EmitSourceLog(EngineLogLevel.Info,"InventoryComponent","InventoryComponent.ReadCurrentInventoryFromRobot","");
        if(_robot.Engine.NvStorage!.Read(0x195000,ReadSink)==0)
            _robot.Engine.EmitSourceLog(EngineLogLevel.Info,"InventoryComponent","InventoryComponent.ReadCurrentInventoryFromRobot.ReadFailed","");
        if(_robot.Engine.NeedsContextExternalInterfaceAvailable && !_robot.Lifetime.Destroying)
        {
            _subscriptions.Add(_robot.Engine.AnimationMessages.SubscribeMessage(0xD8,false,p=>{if(Current&&p is InventoryAmountRequest r)Add(r.Type,r.Amount);}));
            _subscriptions.Add(_robot.Engine.AnimationMessages.SubscribeMessage(0xD9,false,p=>{if(Current&&p is InventoryAmountRequest r)Set(r.Type,r.Amount);}));
            _subscriptions.Add(_robot.Engine.AnimationMessages.SubscribeMessage(0xDA,false,_=>{if(Current)SendStatus();}));
        }
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
        if(result.Result==-1&&_robot.Engine.NeedsContextExternalInterfaceAvailable)_robot.Engine.DeliverNurture(0x70,Array.Empty<byte>(),null);
    }
    internal void SendStatus()
    {
        if(!Ready||!_robot.Engine.NeedsContextExternalInterfaceAvailable)return;
        var copy=Words.ToArray();var data=new byte[256];
        for(int i=0;i<64;i++)BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(i*4,4),copy[i]);
        _robot.Engine.DeliverNurture(0x6F,data,new InventoryStatusReport(Array.AsReadOnly(copy)));
    }
    // M15-094: source owner mutation and 5,000,000-us system-clock write throttle.
    internal int GetAmount(uint type)=>Words[checked((int)type)];
    internal int GetSpace(uint type)
    {
        int cap=Caps.GetValueOrDefault(checked((int)type),-1);if(cap==-1)return -1;
        int current=GetAmount(type);return cap<=current?0:unchecked(cap-current);
    }
    internal void Add(uint type,int delta)
    {
        if(!Current)return;
        if(type>=64){SteppedBehavior.ReportMissing("M1-034/M15-094 skipped out-of-range inventory word access");return;}
        Set(type,unchecked(GetAmount(type)+delta));
        if(!Current)return;
        string text=type==1?"spark":"";
        // 0x63D0FA emits change; 0x63D12E rereads the live word only after that callback.
        _robot.Engine.EmitSourceLog(EngineLogLevel.Event,null,"meta.inventory.change",text,
            new Dictionary<string,string>{{"$data",delta.ToString(System.Globalization.CultureInfo.InvariantCulture)}});
        if(!Current)return;
        int balance=GetAmount(type);
        _robot.Engine.EmitSourceLog(EngineLogLevel.Event,null,"meta.inventory.balance",text,
            new Dictionary<string,string>{{"$data",balance.ToString(System.Globalization.CultureInfo.InvariantCulture)}});
    }
    internal void Set(uint type,int amount)
    {
        if(!Current)return;
        if(type>=64){SteppedBehavior.ReportMissing("M1-034/M15-094 skipped out-of-range inventory word access");return;}
        _robot.Engine.EmitSourceLog(EngineLogLevel.Info,"InventoryComponent","InventoryComponent.SetInventoryAmount",$"{type} : {GetAmount(type)}");
        if(!Current)return;
        if(!Ready){_robot.Engine.EmitSourceLog(EngineLogLevel.Info,"InventoryComponent","InventoryComponent.SetInventoryAmount.ComponentNotReady","");return;}
        int cap=Caps.GetValueOrDefault((int)type,-1);Words[(int)type]=cap==-1?amount:Math.Min(amount,cap);
        TryWrite();
    }
    internal void Update(){if(Current&&!WriteInFlight&&PendingWrite)TryWrite();}
    internal void TryWrite()
    {
        if(!Current)return;long now=ClockMicros();
        if(unchecked(now-_lastWriteMicros)<5_000_000){PendingWrite=true;return;}
        _lastWriteMicros=now;Write();
    }
    private void Write()
    {
        if(!Current)return;WriteInFlight=true;
        var data=new byte[256];for(int i=0;i<64;i++)BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(i*4,4),Words[i]);
        int accepted=_robot.Engine.NvStorage!.Write(0x195000,data,WriteComplete,false);
        if(Current&&accepted==0){_robot.Engine.EmitSourceLog(EngineLogLevel.Info,"InventoryComponent","InventoryComponent.WriteCurrentInventoryToRobot.WriteFailed","");WriteInFlight=false;}
    }
    private void WriteComplete(NvResult result)
    {
        if(!Current)return;PendingWrite=false;WriteInFlight=false;
        if(result.Result>=0)SendStatus();
        else{_robot.Engine.EmitSourceLog(EngineLogLevel.Error,null,"InventoryComponent.WriteCurrentInventoryToRobot.WriteFailed","Write failed with "+NvStorageComponent.NvResultName(result.Result));if(Current)EngineErrorState.StoreAndMaybeBreak();}
    }
    internal void Retire(){for(int i=_subscriptions.Count-1;i>=0;i--)_subscriptions[i].Dispose();_subscriptions.Clear();Caps.Clear();}
}

public sealed partial class EngineRobot
{
    internal ProgressionUnlockOwner? Progression;
    internal InventoryOwner? Inventory;
    private void InitNurtureOwners()
    {
        Progression=new(this);Inventory=new(this);
        if(NativeConfiguredScoreFields?.TryGetValue("SparksFireTruckAlarm",out var fireTruck)==true && fireTruck.FireTruckConfig is not null)
            fireTruck.FireTruckProgression=Progression;
        var progression=Progression;var inventory=Inventory;
        Lifetime.Bind(0x448,_=>progression.Retire());
        Lifetime.Bind(0x444,_=>inventory.Retire());
    }
}
public sealed partial class CozmoEngine
{
    /// <summary>Queue the native RequestSetUnlock input on the engine's game-message hub.</summary>
    public void RequestSetUnlock(uint unlockId,bool unlocked)
        {var owner=Robot;Post(()=>{if(owner is not null&&!owner.Lifetime.Destroying&&ReferenceEquals(Robot,owner))AnimationMessages.Deliver(0xD7,false,new RequestSetUnlockMessage(unlockId,unlocked));});}
    public void RequestInventoryAdd(uint type,int amount)=>QueueInventory(0xD8,new InventoryAmountRequest(type,amount));
    public void RequestInventorySet(uint type,int amount)=>QueueInventory(0xD9,new InventoryAmountRequest(type,amount));
    public void RequestInventoryGet()=>QueueInventory(0xDA,null);
    private void QueueInventory(ushort tag,object? input)
    {var owner=Robot;Post(()=>{if(owner is not null&&!owner.Lifetime.Destroying&&ReferenceEquals(Robot,owner))AnimationMessages.Deliver(tag,false,input);});}    internal void DeliverNurture(ushort tag,byte[] data,object? report,Action? typed=null)
    {
        DeliverAnimationReportToApp(tag,data,typed,typedFirst:true);
        AnimationMessages.Deliver(tag,true,report);
    }
}
