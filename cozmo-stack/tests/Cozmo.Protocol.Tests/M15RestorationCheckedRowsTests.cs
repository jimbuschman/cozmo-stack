using System.Buffers.Binary;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Transport;
using Xunit;
namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class M15RestorationCheckedRowsTests
{
    private sealed class Port:IEngineTransport
    {
        public bool TimedOut=>false;public event Action<ReceiverEvent>? Received{add{}remove{}}
        public void Start(){}public void Connect(System.Net.IPAddress ip,bool simulated){}
        public void Disconnect(System.Net.IPEndPoint endpoint){}public void SendData(byte[] data){}
    }
    private sealed class Fixture:IDisposable
    {
        internal readonly BackupTestResources Files=new();internal readonly CozmoRobot Robot;
        internal CozmoEngine Engine=>Robot.Engine;internal EngineRobot Native=>Engine.Robot!;
        internal readonly List<(ushort Tag,byte[] Data)> Reports=new();internal readonly List<EngineLogEntry> Logs=new();internal readonly List<string> Stack=new();
        internal Fixture(bool queue=false)
        {
            BackupFiles.ClearDefaultsForTest();SteppedBehavior.ResetMissingForTests();InventoryOwner.ResetZeroRulingForTest();var options=Files.Options();
            string config=Path.GetDirectoryName(BackupTestResources.ShippedConfig())!;
            foreach(var name in new[]{"unlock_config_nurture.json","inventory_config.json"})File.Copy(Path.Combine(config,name),Path.Combine(options.ResourcesPath!,"config/engine",name));
            Robot=CozmoRobot.CreateForTest(new Port(),()=>1_000_000_000,options);
            Engine.EngineToGameMessage+=(t,b)=>Reports.Add((t,b.ToArray()));Engine.LogEntry+=Logs.Add;Engine.StackLogLine+=Stack.Add;
            Engine.Robots.AddRobot(1,withRic:queue);
            if(!queue)Engine.BackupFiles.InitProgressionDefaults();
            Engine.DrainAppDelivery();
            Reports.Clear(); // Constructor RobotDelocalized is independent of the restoration under test.
        }
        public void Dispose(){Robot.Dispose();Files.Dispose();BackupFiles.ClearDefaultsForTest();}
    }
    private static byte[] UnlockData(params uint[] ids)
    {var data=new byte[2048];for(int i=0;i<ids.Length;i++)BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(i*4,4),ids[i]);return data;}
    private static uint[] Ids(byte[] payload)=>Enumerable.Range(0,payload[0]).Select(i=>BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(1+4*i,4))).ToArray();
    // reach: unit (direct native NV sink/message and explicit safe app drain; live NV transport/tick join is not exercised).
    [Fact]
    public void ConnectionQueueCallsTheRealProgressionThenInventorySinks()
    {
        using var f=new Fixture(true);Assert.Equal(new uint[]{0x182000,0x195000},f.Engine.NvStorage!.QueuedTags.Take(2));
        f.Engine.NvStorage.Update();f.Native.DeliverMessage(new NVOpResult{Tag=0x182000,Op=NvStorageComponent.OpRead,Result=-1,Data=Array.Empty<byte>()});f.Engine.DrainAppDelivery();
        Assert.Equal(new ushort[]{0x6D,0x6E},f.Reports.Select(r=>r.Tag));
        f.Reports.Clear();f.Engine.NvStorage.Update();
        f.Native.DeliverMessage(new NVOpResult{Tag=0x195000,Op=NvStorageComponent.OpRead,Result=-1,Data=Array.Empty<byte>()});f.Engine.DrainAppDelivery();
        Assert.True(f.Native.Inventory!.Ready);Assert.Equal(new ushort[]{0x6F,0x70},f.Reports.Select(r=>r.Tag));
        Assert.Equal(256,f.Reports[0].Data.Length);Assert.All(f.Reports[0].Data,b=>Assert.Equal(0,b));Assert.Empty(f.Reports[1].Data);
        Assert.Contains(f.Stack,s=>s.Contains("M15-035 manager ruling"));
    }
    // reach: unit (direct native NV sink/message and explicit safe app drain; live NV transport/tick join is not exercised).
    [Fact]
    public void UnlockSuccessReplacesOldStateWithDefaultsAndUniqueSavedIdsOneThrough84()
    {
        using var f=new Fixture();var owner=f.Native.Progression!;owner.Unlocked.Add(85);
        owner.ReadSink(new NvResult(1,UnlockData(84,1,84,0,85,uint.MaxValue,2)));f.Engine.DrainAppDelivery();
        var report=Assert.Single(f.Reports);Assert.Equal((ushort)0x6D,report.Tag);Assert.Equal(0,report.Data[^1]);
        var expected=BackupFiles.CurrentDefaults().Concat(new uint[]{1,2,84}).Distinct().OrderBy(i=>i).ToArray();
        Assert.Equal(expected,owner.Unlocked);Assert.Equal(expected,Ids(report.Data));Assert.DoesNotContain(85u,owner.Unlocked);
    }
    // reach: unit (direct native NV sink/message and explicit safe app drain; live NV transport/tick join is not exercised).
    [Theory]
    [InlineData(-2,2048)][InlineData(-9,0)][InlineData(0,2049)]
    public void UnlockErrorsKeepStateAndEmitNoReport(int result,int length)
    {
        bool prior=EngineErrorState.ErrorFlagSet;
        try{using var f=new Fixture();f.Native.Progression!.Unlocked.Add(84);EngineErrorState.ErrorFlagSet=false;
        f.Native.Progression.ReadSink(new NvResult((sbyte)result,new byte[length]));f.Engine.DrainAppDelivery();Assert.Equal(new uint[]{84},f.Native.Progression.Unlocked);Assert.Empty(f.Reports);
        Assert.True(EngineErrorState.ErrorFlagSet);var e=Assert.Single(f.Logs.Where(e=>e.Name=="UnlockComponent.ReadGameUnlocks"));Assert.Equal(EngineLogLevel.Error,e.Level);Assert.Null(e.Channel);}
        finally{EngineErrorState.ErrorFlagSet=prior;}
    }
    // reach: unit (direct native NV sink/message and explicit safe app drain; live NV transport/tick join is not exercised).
    [Theory]
    [InlineData(0)][InlineData(1)][InlineData(2047)]
    public void ShortUnlockInputLogsPaddingThenSkipsTheFreedBufferWithoutReplacingState(int length)
    {
        using var f=new Fixture();f.Native.Progression!.Unlocked.Add(84);f.Native.Progression.ReadSink(new NvResult(0,new byte[length]));f.Engine.DrainAppDelivery();
        Assert.Equal(new uint[]{84},f.Native.Progression.Unlocked);Assert.Empty(f.Reports);
        var info=Assert.Single(f.Logs.Where(e=>e.Name=="ProgressionUnlockComponent.ReadFewerUnlocks"));Assert.Equal("Padding unlock data due to size mismatch",info.Text);
        Assert.Contains(f.Stack,s=>s.Contains("M1-034/M15-034")&&s.Contains("0x0064CEEA"));
    }
    // reach: unit (direct native NV sink/message and explicit safe app drain; live NV transport/tick join is not exercised).
    [Fact]
    public void MissingUnlocksInstallDefaultsBeforeTheNullInterfaceNotificationSkip()
    {
        using var f=new Fixture();f.Native.HasExternalInterface=false;f.Native.Progression!.Unlocked.Add(84);f.Native.Progression.ReadSink(new NvResult(-1,Array.Empty<byte>()));f.Engine.DrainAppDelivery();
        Assert.Equal(BackupFiles.CurrentDefaults(),f.Native.Progression.Unlocked);Assert.Empty(f.Reports);Assert.Contains(f.Stack,s=>s.Contains("0x0064C114"));
    }
    // reach: unit (direct native NV sink/message and explicit safe app drain; live NV transport/tick join is not exercised).
    [Theory]
    [InlineData(0)][InlineData(1)][InlineData(3)][InlineData(4)][InlineData(7)][InlineData(8)][InlineData(252)][InlineData(255)][InlineData(256)][InlineData(257)]
    public void InventoryDecodeOverwritesOnlyCompleteWordsAndRetainsTheRest(int length)
    {
        using var f=new Fixture();var owner=f.Native.Inventory!;for(int i=0;i<64;i++)owner.Words[i]=-100-i;
        var data=new byte[length];for(int i=0;i<length/4;i++)BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(i*4,4),1000+i);
        f.Engine.EngineToGameMessage+=(tag,_)=>{if(tag==0x6F)Assert.True(owner.Ready);};owner.ReadSink(new NvResult(1,data));f.Engine.DrainAppDelivery();
        var report=Assert.Single(f.Reports);Assert.Equal((ushort)0x6F,report.Tag);Assert.Equal(256,report.Data.Length);
        for(int i=0;i<64;i++)Assert.Equal(i<Math.Min(64,length/4)?1000+i:-100-i,BinaryPrimitives.ReadInt32LittleEndian(report.Data.AsSpan(i*4,4)));
    }
    // reach: unit (direct native NV sink/message and explicit safe app drain; live NV transport/tick join is not exercised).
    [Theory]
    [InlineData(-1)][InlineData(-2)]
    public void InventoryNegativeResultsStillSendRetainedWordsAndOnlyMissingRequestsDefaults(int result)
    {
        bool prior=EngineErrorState.ErrorFlagSet;try{using var f=new Fixture();var owner=f.Native.Inventory!;owner.Words[63]=-17;EngineErrorState.ErrorFlagSet=false;
        owner.ReadSink(new NvResult((sbyte)result,Array.Empty<byte>()));f.Engine.DrainAppDelivery();Assert.True(owner.Ready);Assert.Equal(result==-1?2:1,f.Reports.Count);Assert.Equal((ushort)0x6F,f.Reports[0].Tag);
        Assert.Equal(-17,BinaryPrimitives.ReadInt32LittleEndian(f.Reports[0].Data.AsSpan(252,4)));Assert.Equal(result==-2,EngineErrorState.ErrorFlagSet);
        if(result==-1)Assert.Equal((ushort)0x70,f.Reports[1].Tag);
        else{var error=Assert.Single(f.Logs.Where(e=>e.Name.EndsWith("ReadError")));Assert.Equal("(tc0000) : ",error.Text);Assert.Empty(error.Data);}}
        finally{EngineErrorState.ErrorFlagSet=prior;}
    }
    // reach: unit (direct native NV sink/message and explicit safe app drain; live NV transport/tick join is not exercised).
    [Fact]
    public void InventoryWithoutInterfaceStillDecodesAndBecomesReadyButSendsNothing()
    {
        using var f=new Fixture();f.Native.HasExternalInterface=false;f.Native.Inventory!.ReadSink(new NvResult(0,BitConverter.GetBytes(-4)));f.Engine.DrainAppDelivery();
        Assert.True(f.Native.Inventory.Ready);Assert.Equal(-4,f.Native.Inventory.Words[0]);Assert.Empty(f.Reports);
    }
    [Fact]
    public void NaNPauseEndPermanentlySuppressesThatNeedsDecay()
    {
        var needs=new NeedsManager(()=>0);needs.SetLevel(NeedId.Play,.8);needs.SupplyFullnessWindowForTest(NeedId.Play,0,float.NaN);
        uint bits=needs.State.LevelBits(NeedId.Play);needs.ApplyDecayAllNeeds(true,100);needs.ApplyDecayAllNeeds(true,1000);Assert.Equal(bits,needs.State.LevelBits(NeedId.Play));
    }
    [Theory]
    [InlineData(0x7FC12345u,0)][InlineData(0x7F800000u,int.MaxValue)][InlineData(0xFF800000u,int.MinValue)][InlineData(0x3F666666u,90000)]
    public void PersistenceQuantizesBinary32TowardZeroWithSaturation(uint bits,int expected)
    {Assert.Equal(expected,NeedsManager.QuantizeLevel(BitConverter.Int32BitsToSingle(unchecked((int)bits))));}
    [Fact]
    public void ForceSetClampsFiniteValuesButKeepsAndCopiesRawNaNPayload()
    {
        var needs=new NeedsManager(()=>0);uint nan=0x7F812345;var order=new List<string>();needs.SourceLog=(l,c,k,t)=>{Assert.Equal(EngineLogLevel.Info,l);Assert.Equal("NeedsSystem",c);Assert.Equal("NeedsManager.ForceSetNeedsLevels",k);order.Add("info");};
        needs.NeedsLevelBitsCopied+=(bits,action)=>{Assert.Equal(nan,bits[2]);Assert.Equal(NeedsActionId.NoAction,action);order.Add("copy");};
        needs.ForceSetNeedsLevels(new[]{-1f,2f,BitConverter.Int32BitsToSingle(unchecked((int)nan))});
        Assert.Equal(new[]{"info","copy"},order);Assert.Equal(0x3CF5C28Fu,needs.State.LevelBits(NeedId.Repair));Assert.Equal(0x3F800000u,needs.State.LevelBits(NeedId.Energy));Assert.Equal(nan,needs.State.LevelBits(NeedId.Play));
        string root=Directory.CreateTempSubdirectory("m15-nan-").FullName,path=Path.Combine(root,"state.json");
        try{needs.Save(path);using var json=System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));Assert.Equal(0,json.RootElement.GetProperty("CurNeedLevel").GetProperty("Play").GetInt32());}
        finally{Directory.Delete(root,true);}
    }
    [Fact]
    public void ShortForceVectorKeepsAllPriorBits()
    {
        var needs=new NeedsManager(()=>0);needs.ForceSetNeedsLevels(new[]{.5f,.6f});Assert.Equal(1,needs.State.GetNeedLevel(NeedId.Repair));
    }
    [Fact]
    public void NaNNowDoesNotTakeThePeriodicDeadlineButStillUpdatesNowAndNotifications()
    {
        var needs=new NeedsManager(()=>0);int reports=0,notifications=0;needs.NeedsStateSent+=_=>reports++;needs.LocalNotificationsUpdate+=()=>notifications++;
        needs.Update(float.NaN);Assert.True(float.IsNaN(needs.NowSec));Assert.Equal(1,notifications);Assert.Equal(0,reports);
    }
    [Fact]
    public void DecayReinsertsAMissingValidNeedAtZeroWithoutAGetNeedLevelError()
    {
        var state=new NeedsState(NeedsConfig.Default);state.RemoveLevelForTest(NeedId.Energy);var logs=new List<string>();state.SourceLog=(l,c,k,t)=>logs.Add(k);
        state.ApplyDecay(NeedId.Energy,new DecayConfig(new Dictionary<NeedId,IReadOnlyList<(double,double)>>(),new Dictionary<NeedId,IReadOnlyList<(double,double)>>()),60,true,1);
        Assert.Equal(0,state.GetNeedLevel(NeedId.Energy));Assert.Equal(new[]{"NeedsState.ApplyDecay"},logs);
    }
    [Fact]
    public void ElapsedProducerTruncatesMicrosecondDifferenceBeforeConvertingToFloat()
    {
        double clock=10;var needs=new NeedsManager(()=>clock);var elapsed=new List<string>();needs.State.SourceLog=(l,c,k,t)=>{if(k=="NeedsState.ApplyDecay")elapsed.Add(t);};
        string root=Directory.CreateTempSubdirectory("m15-elapsed-").FullName,path=Path.Combine(root,"state.json");
        try{needs.Save(path,unixTimeSec:10);clock=11.9;needs.Update((float)clock);elapsed.Clear();needs.Load(path);Assert.All(elapsed,t=>Assert.Contains("elapsed time of 1.000000 seconds",t));Assert.Equal(3,elapsed.Count);}
        finally{Directory.Delete(root,true);}
    }

    [Fact]
    public void LiveForceSetSubscriptionMutatesThenSendsAndRetiresWithItsNeedsHost()
    {
        using var robot=CozmoRobot.CreateOffline();var needs=new NeedsManager(()=>0);
        string obb=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(BackupTestResources.ShippedConfig())!,"../../../.."));
        var stack=FreeplayStack.Create(obb,robot,new BehaviorContext{Robot=robot,Triggers=new AnimationTriggerMap()},()=>0,needs:needs,withReactions:false);
        var order=new List<string>();robot.Engine.LogEntry+=e=>{if(e.Name=="NeedsManager.ForceSetNeedsLevels"){Assert.Equal(1,needs.State.GetNeedLevel(NeedId.Play));Assert.Equal("Game sent ForceSetNeedsLevels message with 0.500000, 0.600000, 0.700000",e.Text);order.Add("info");}};
        needs.NeedsStateSent+=action=>{Assert.Equal(NeedsActionId.NoAction,action);Assert.Equal(.7f,(float)needs.State.GetNeedLevel(NeedId.Play));order.Add("send");};
        robot.Engine.AnimationMessages.Deliver(0xC6,false,new[]{.5f,.6f,.7f});Assert.Equal(new[]{"info","send"},order);
        stack.Dispose();robot.Engine.AnimationMessages.Deliver(0xC6,false,new[]{1f,1f,1f});Assert.Equal(.7f,(float)needs.State.GetNeedLevel(NeedId.Play));
    }
    [Fact]
    public void InventoryZeroRulingIsLoggedOnceAcrossTwoActualRobotOwners()
    {
        using var f=new Fixture();f.Engine.Robots.AddRobot(2,withRic:false);
        Assert.Single(f.Stack.Where(s=>s.Contains("M15-035 manager ruling")));
    }

}
