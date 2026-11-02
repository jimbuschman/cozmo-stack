using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Transport;
using Xunit;
namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class NativeProgressionWriteTests
{
    private sealed class Fixture:IDisposable
    {
        internal readonly BackupTestResources Files=new();
        internal readonly CozmoRobot Robot;
        internal CozmoEngine Engine=>Robot.Engine;
        internal EngineRobot Owner=>Engine.Robot!;
        internal ProgressionUnlockOwner Unlock=>Owner.Progression!;
        internal readonly List<NVCommand> Commands=new();
        internal readonly List<(ushort Tag,byte[] Body)> Reports=new();
        internal Fixture(bool contextInterface=true)
        {
            BackupFiles.ClearDefaultsForTest();var options=Files.Options();
            File.WriteAllText(Path.Combine(options.ResourcesPath!,"config","engine","unlock_config_nurture.json"),"{\"defaultUnlocks\":[\"StackTwoCubes\"],\"freeplayOverrides\":[]}");
            Robot=CozmoRobot.CreateForTest(new M4ControlTests.FakePort(),()=>1_000_000_000,options);
            Engine.Robots.AddRobot(CozmoEngine.RobotId,withRic:false);Engine.NeedsContextExternalInterfaceAvailable=contextInterface;Unlock.Init();
            Owner.SendFault=message=>{if(message is NVCommand command)Commands.Add(command);return true;};
            Engine.EngineToGameMessage+=(tag,body)=>Reports.Add((tag,body.ToArray()));
            Engine.NvStorage!.Update();
            Owner.DeliverMessage(new NVOpResult{Tag=0x182000,Op=NvStorageComponent.OpRead,Result=-1,Data=Array.Empty<byte>()});
            Unlock.Unlocked.Clear();Commands.Clear();
            Engine.DrainAppDelivery();Reports.Clear();
        }
        internal void Drain(sbyte writeResult=0)
        {
            var nv=Engine.NvStorage!;int seen=Commands.Count;
            for(int i=0;i<100&&!nv.IsIdle;i++)
            {
                nv.Update();
                while(seen<Commands.Count)
                {
                    var command=Commands[seen++];
                    Owner.DeliverMessage(new NVOpResult{Tag=command.Tag,Op=command.Op,Result=command.Op==NvStorageComponent.OpWrite?writeResult:(sbyte)0,Data=Array.Empty<byte>()});
                }
            }
            Assert.True(nv.IsIdle);Engine.DrainAppDelivery();
        }
        public void Dispose(){Robot.Dispose();Files.Dispose();BackupFiles.ClearDefaultsForTest();}
    }
    [Fact]
    public void CanonicalRobotConstructorInstallsUnlockAndInventorySubscriptions()
    {
        using var resources=new BackupTestResources();BackupFiles.ClearDefaultsForTest();
        try
        {
            using var robot=CozmoRobot.CreateForTest(new M4ControlTests.FakePort(),()=>0,resources.Options());
            var owner=robot.Engine.Robots.AddRobot(CozmoEngine.RobotId,withRic:true)!;
            Assert.Equal(1,robot.Engine.AnimationMessages.SubscriberCountForTest(0xD7,false));
            Assert.Equal(1,robot.Engine.AnimationMessages.SubscriberCountForTest(0xD8,false));
            Assert.Equal(1,robot.Engine.AnimationMessages.SubscriberCountForTest(0xD9,false));
            Assert.Equal(1,robot.Engine.AnimationMessages.SubscriberCountForTest(0xDA,false));
            robot.Engine.RequestSetUnlock(10,true);robot.Engine.Tick();Assert.Contains(10u,owner.Progression!.Unlocked);
        }
        finally{BackupFiles.ClearDefaultsForTest();}
    }
    [Fact]
    public void UnlockSynchronousNvRefusalKeepsMutationAndEmitsNoSuccess()
    {
        bool priorError=EngineErrorState.ErrorFlagSet;
        var field=typeof(NvStorageComponent).GetField("MaxSizeTable",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!;
        var sizes=(Dictionary<uint,int>)field.GetValue(null)!;int prior=sizes[0x182000];
        try
        {
            using var f=new Fixture();sizes[0x182000]=16;EngineErrorState.ErrorFlagSet=false;
            f.Unlock.SetUnlock(10,true);f.Engine.DrainAppDelivery();
            Assert.Contains(10u,f.Unlock.Unlocked);Assert.True(EngineErrorState.ErrorFlagSet);
            Assert.DoesNotContain(f.Reports,r=>r.Tag==0x6C);
        }
        finally{sizes[0x182000]=prior;EngineErrorState.ErrorFlagSet=priorError;}
    }    // 0x63CE9C..0x63CE5E: explicit system-clock input at the original owner.
    [Fact]
    public void InventorySetUsesNativeReadyCapAndFiveSecondBoundary()
    {
        using var f=new Fixture();var inv=f.Owner.Inventory!;long time=inv.ConstructedAtMicros;
        inv.ClockMicros=()=>time;inv.Words[1]=7;inv.Caps[1]=10;
        inv.Set(1,99);Assert.Equal(7,inv.Words[1]);Assert.False(inv.PendingWrite);
        inv.Ready=true;inv.Set(1,99);Assert.Equal(10,inv.Words[1]);Assert.True(inv.PendingWrite);
        time=inv.ConstructedAtMicros+4_999_999;inv.Update();Assert.False(inv.WriteInFlight);
        time++;inv.Update();Assert.True(inv.WriteInFlight);
        Assert.Contains(0x195000u,f.Engine.NvStorage!.QueuedTags);
    }
    [Fact]
    public void InventoryAddUsesSignedArithmeticAndUnchangedSetStillDefers()
    {
        using var f=new Fixture();var inv=f.Owner.Inventory!;inv.Ready=true;inv.Words[1]=7;inv.Caps[1]=10;
        inv.ClockMicros=()=>inv.ConstructedAtMicros;inv.Add(1,-20);Assert.Equal(-13,inv.GetAmount(1));Assert.True(inv.PendingWrite);
        inv.PendingWrite=false;inv.Set(1,-13);Assert.True(inv.PendingWrite);
        Assert.Equal(23,inv.GetSpace(1));inv.Caps[1]=int.MaxValue;inv.Words[1]=-1;Assert.Equal(int.MinValue,inv.GetSpace(1));
        inv.Caps.Remove(1);Assert.Equal(-1,inv.GetSpace(1));
    }
    [Theory]
    [InlineData(true,"10")][InlineData(false,"9")]
    public void InventoryAddEmitsSourceEventsAfterCappedOrDroppedSet(bool ready,string expectedBalance)
    {
        using var f=new Fixture();var inv=f.Owner.Inventory!;inv.Ready=ready;inv.Words[1]=9;inv.Caps[1]=10;
        inv.ClockMicros=()=>inv.ConstructedAtMicros;var events=new List<EngineLogEntry>();
        f.Engine.LogEntry+=e=>{if(e.Name is "meta.inventory.change" or "meta.inventory.balance")events.Add(e);};
        inv.Add(1,5);
        Assert.Equal(new[]{"meta.inventory.change","meta.inventory.balance"},events.Select(e=>e.Name));
        Assert.All(events,e=>{Assert.Equal(EngineLogLevel.Event,e.Level);Assert.Null(e.Channel);Assert.Equal("spark",e.Text);Assert.Single(e.Data);});
        Assert.Equal("5",events[0].Data["$data"]);Assert.Equal(expectedBalance,events[1].Data["$data"]);
    }
    [Fact]
    public void InventoryBalanceRereadsAfterChangeEventCallback()
    {
        using var f=new Fixture();var inv=f.Owner.Inventory!;inv.Ready=true;inv.Words[1]=9;
        inv.ClockMicros=()=>inv.ConstructedAtMicros;var events=new List<EngineLogEntry>();
        f.Engine.LogEntry+=e=>{if(e.Name=="meta.inventory.change")inv.Set(1,33);if(e.Name is "meta.inventory.change" or "meta.inventory.balance")events.Add(e);};
        inv.Add(1,5);Assert.Equal("5",events[0].Data["$data"]);Assert.Equal("33",events[1].Data["$data"]);
    }    [Fact]
    public void InventoryRequestsUseOriginalSubscribedOwnerAndGetReportsCurrentWords()
    {
        using var f=new Fixture();var inv=f.Owner.Inventory!;inv.Init();inv.Ready=true;
        inv.ClockMicros=()=>inv.ConstructedAtMicros;f.Engine.RequestInventorySet(1,4);f.Engine.RequestInventoryAdd(1,3);f.Engine.RequestInventoryGet();
        f.Engine.Tick();f.Engine.DrainAppDelivery();Assert.Equal(7,inv.Words[1]);
        var report=Assert.Single(f.Reports.Where(r=>r.Tag==0x6F));
        Assert.Equal("0000000007000000",Convert.ToHexString(report.Body.AsSpan(0,8)));Assert.True(inv.PendingWrite);
    }    [Fact]
    public void InventorySynchronousNvRefusalClearsFlagsAndRetainsMutationAndClock()
    {
        bool priorError=EngineErrorState.ErrorFlagSet;
        var field=typeof(NvStorageComponent).GetField("MaxSizeTable",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!;
        var sizes=(Dictionary<uint,int>)field.GetValue(null)!;int prior=sizes[0x195000];
        try
        {
            using var f=new Fixture();var inv=f.Owner.Inventory!;inv.Ready=true;inv.PendingWrite=true;
            long now=inv.ConstructedAtMicros+5_000_000;inv.ClockMicros=()=>now;
            sizes[0x195000]=16;EngineErrorState.ErrorFlagSet=false;inv.Set(1,9);f.Engine.DrainAppDelivery();
            Assert.Equal(9,inv.Words[1]);Assert.Equal(now,inv.LastWriteMicrosForTest);
            Assert.False(inv.PendingWrite);Assert.False(inv.WriteInFlight);Assert.True(EngineErrorState.ErrorFlagSet);
            Assert.DoesNotContain(f.Reports,r=>r.Tag==0x6F);
        }
        finally{sizes[0x195000]=prior;EngineErrorState.ErrorFlagSet=priorError;}
    }
    [Fact]
    public void InventoryNegativeWriteCompletionClearsFlagsWithoutStatusOrRollback()
    {
        bool priorError=EngineErrorState.ErrorFlagSet;
        try
        {
            using var f=new Fixture();f.Drain();var inv=f.Owner.Inventory!;inv.Ready=true;inv.PendingWrite=true;
            inv.ClockMicros=()=>inv.ConstructedAtMicros+5_000_000;EngineErrorState.ErrorFlagSet=false;
            inv.Set(1,9);Assert.True(inv.WriteInFlight);f.Drain(-2);
            Assert.Equal(9,inv.Words[1]);Assert.False(inv.PendingWrite);Assert.False(inv.WriteInFlight);
            Assert.True(EngineErrorState.ErrorFlagSet);Assert.DoesNotContain(f.Reports,r=>r.Tag==0x6F);
        }
        finally{EngineErrorState.ErrorFlagSet=priorError;}
    }    // First write completion clears the later throttled change but reports the live word.
    [Fact]
    public void InventoryWriteSnapshotsThenReportsCurrentAndClearsPending()
    {
        using var f=new Fixture();f.Drain();var inv=f.Owner.Inventory!;inv.Ready=true;
        long now=inv.ConstructedAtMicros+5_000_000;inv.ClockMicros=()=>now;
        inv.Set(1,9);Assert.True(inv.WriteInFlight);inv.Set(1,77);Assert.True(inv.PendingWrite);
        f.Drain();Assert.False(inv.PendingWrite);Assert.False(inv.WriteInFlight);
        var report=Assert.Single(f.Reports.Where(r=>r.Tag==0x6F));
        Assert.Equal(256,report.Body.Length);Assert.Equal("000000004D000000",Convert.ToHexString(report.Body.AsSpan(0,8)));
        var writes=f.Commands.Where(c=>c.Op==NvStorageComponent.OpWrite&&c.Tag==0x195000).ToArray();
        Assert.NotEmpty(writes);Assert.Equal("0000000009000000",Convert.ToHexString(writes[0].Data.AsSpan(16,8)));
        int count=f.Engine.NvStorage!.QueuedTags.Count;inv.Update();Assert.Equal(count,f.Engine.NvStorage.QueuedTags.Count);
    }    [Fact]
    public void QueuedUnlockAndInventoryRequestsCannotReachReplacementOwner()
    {
        using var f=new Fixture();var old=f.Owner;
        f.Engine.RequestSetUnlock(10,true);f.Engine.RequestInventorySet(1,99);
        f.Engine.Robots.RemoveRobot(CozmoEngine.RobotId,wasConnecting:false);
        var replacement=f.Engine.Robots.AddRobot(CozmoEngine.RobotId,withRic:false);
        replacement.Progression!.Init();replacement.Inventory!.Init();replacement.Inventory.Ready=true;
        f.Engine.Tick();Assert.DoesNotContain(10u,replacement.Progression.Unlocked);Assert.Equal(0,replacement.Inventory.Words[1]);
        Assert.NotSame(old,replacement);
    }    [Fact]
    public void PublicQueuedRequestReachesOriginalOwnerThroughNativeHub()
    {
        using var f=new Fixture();Assert.Equal(1,f.Engine.AnimationMessages.SubscriberCountForTest(0xD7,false));
        f.Engine.RequestSetUnlock(10,true);f.Engine.Tick();Assert.Contains(10u,f.Unlock.Unlocked);
        f.Drain();Assert.Equal("0A00000001",Convert.ToHexString(Assert.Single(f.Reports.Where(x=>x.Tag==0x6C)).Body));
        int count=f.Commands.Count;f.Engine.RequestSetUnlock(0,true);f.Engine.Tick();f.Drain();Assert.Equal(count,f.Commands.Count);
    }
    [Fact]
    public void ChangedIdWritesLiteralFullSnapshotAndReportsRequestedState()
    {
        using var f=new Fixture();f.Unlock.Unlocked.UnionWith(new uint[]{3,84});f.Unlock.SetUnlock(10,true);
        Assert.Equal(new uint[]{0x182000,0x182000},f.Engine.NvStorage!.QueuedTags);
        f.Drain(1);var chunks=f.Commands.Where(c=>c.Op==NvStorageComponent.OpWrite).ToArray();
        var payload=chunks.SelectMany((c,i)=>i==0?c.Data.Skip(16):c.Data).ToArray();
        Assert.Equal(2048,payload.Length);Assert.Equal("030000000A00000054000000",Convert.ToHexString(payload.AsSpan(0,12)));Assert.All(payload.Skip(12),b=>Assert.Equal(0,b));
        var report=Assert.Single(f.Reports.Where(x=>x.Tag==0x6C));Assert.Equal("0A00000001",Convert.ToHexString(report.Body));
        int count=f.Commands.Count;f.Unlock.SetUnlock(10,true);f.Drain();Assert.Equal(count,f.Commands.Count);
    }
    [Fact]
    public void DefaultsWriteEachChangedSnapshotAndNotifyBeforeCompletions()
    {
        using var f=new Fixture();f.Unlock.Unlocked.UnionWith(new uint[]{3,84});f.Unlock.SetUnlock(2,false);f.Engine.DrainAppDelivery();
        var defaults=Assert.Single(f.Reports.Where(x=>x.Tag==0x6E));Assert.Equal("010A000000",Convert.ToHexString(defaults.Body));
        Assert.Equal(new uint[]{10},f.Unlock.Unlocked);Assert.Equal(6,f.Engine.NvStorage!.QueuedTags.Count);
        f.Drain();var chunks=f.Commands.Where(c=>c.Op==NvStorageComponent.OpWrite).ToArray();
        var starts=chunks.Where(c=>c.Tag==0x182000).ToArray();Assert.Equal(3,starts.Length);
        Assert.Equal("54000000",Convert.ToHexString(starts[0].Data.AsSpan(16,4)));
        Assert.Equal("0A00000054000000",Convert.ToHexString(starts[1].Data.AsSpan(16,8)));
        Assert.Equal("0A000000",Convert.ToHexString(starts[2].Data.AsSpan(16,4)));
        Assert.Equal(new[]{"0300000000","0A00000001","5400000000"},f.Reports.Where(x=>x.Tag==0x6C).Select(x=>Convert.ToHexString(x.Body)));
    }
    [Fact]
    public void NegativeCompletionKeepsMutationAndProducesNoResult()
    {
        bool old=EngineErrorState.ErrorFlagSet;
        try{using var f=new Fixture();EngineErrorState.ErrorFlagSet=false;f.Unlock.SetUnlock(10,true);f.Drain(-2);
            Assert.Contains(10u,f.Unlock.Unlocked);Assert.DoesNotContain(f.Reports,x=>x.Tag==0x6C);Assert.True(EngineErrorState.ErrorFlagSet);}
        finally{EngineErrorState.ErrorFlagSet=old;}
    }
    [Fact]
    public void AllRelockWritesOnlyChangedIdsInAscendingOrder()
    {
        using var f=new Fixture();f.Unlock.Unlocked.UnionWith(new uint[]{3,10});f.Unlock.SetUnlock(1,false);
        Assert.Empty(f.Unlock.Unlocked);f.Drain();
        var starts=f.Commands.Where(c=>c.Op==NvStorageComponent.OpWrite&&c.Tag==0x182000).ToArray();
        Assert.Equal(2,starts.Length);Assert.Equal("0A000000",Convert.ToHexString(starts[0].Data.AsSpan(16,4)));
        Assert.All(starts[1].Data.Skip(16),b=>Assert.Equal(0,b));
        Assert.Equal(new[]{"0300000000","0A00000000"},f.Reports.Where(x=>x.Tag==0x6C).Select(x=>Convert.ToHexString(x.Body)));
    }
    [Fact]
    public void RetirementDropsQueuedWritesAndUnlinksOriginalRequest()
    {
        using var f=new Fixture();var old=f.Owner;var original=f.Unlock;
        original.SetUnlock(10,true);Assert.NotEmpty(f.Engine.NvStorage!.QueuedTags);
        f.Engine.Robots.RemoveRobot(CozmoEngine.RobotId,wasConnecting:false);
        Assert.Equal(0,f.Engine.AnimationMessages.SubscriberCountForTest(0xD7,false));
        f.Engine.DrainAppDelivery();Assert.DoesNotContain(f.Reports,x=>x.Tag==0x6C);
        Assert.True(old.Lifetime.Destroying);Assert.Empty(original.Unlocked);
    }
    [Fact]
    public void ContextInterfaceControlsNativeSubscriptionAndDefaultDelivery()
    {
        using var f=new Fixture(false);Assert.Equal(0,f.Engine.AnimationMessages.SubscriberCountForTest(0xD7,false));
        var stack=new List<string>();f.Engine.StackLogLine+=stack.Add;
        f.Unlock.SetUnlock(2,false);f.Drain();
        Assert.DoesNotContain(f.Reports,x=>x.Tag is 0x6C or 0x6E);
        Assert.Contains(stack,x=>x.Contains("M1-034/M15-095")&&x.Contains("0x0064C114"));
    }
    [Fact]
    public void WriteResultUsesContextInterfaceNotHostRobotFlag()
    {
        using var f=new Fixture();f.Owner.HasExternalInterface=false;f.Unlock.SetUnlock(10,true);f.Drain();
        Assert.Equal("0A00000001",Convert.ToHexString(Assert.Single(f.Reports.Where(x=>x.Tag==0x6C)).Body));
    }
    [Fact]
    public void DefaultsReadsLiveTreeAfterSynchronousWriteLogReentry()
    {
        using var f=new Fixture();f.Unlock.Unlocked.UnionWith(new uint[]{3,84});bool changed=false;
        void Log(EngineLogEntry entry)
        {
            if(changed||entry.Name!="NVStorageComponent.Write.DataQueued")return;
            changed=true;
            string path=Path.Combine(f.Engine.Options.ResourcesPath!,"config","engine","unlock_config_nurture.json");
            File.WriteAllText(path,"{\"defaultUnlocks\":[\"PickupCube\"],\"freeplayOverrides\":[]}");
            BackupFiles.ClearDefaultsForTest();f.Engine.BackupFiles.InitProgressionDefaults();
        }
        f.Engine.LogEntry+=Log;
        try
        {
            f.Unlock.SetUnlock(2,true);f.Engine.DrainAppDelivery();
            Assert.True(changed);Assert.Equal(new uint[]{6},f.Unlock.Unlocked);
            Assert.Equal("0106000000",Convert.ToHexString(Assert.Single(f.Reports.Where(x=>x.Tag==0x6E)).Body));
        }
        finally {f.Engine.LogEntry-=Log;}
    }
}
