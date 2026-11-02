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
