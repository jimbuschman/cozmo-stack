using System.Text;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Xunit;
namespace Cozmo.Protocol.Tests;
[Collection("M5 process statics")]
public sealed class DriveOffPilotMoodTests
{
    [Fact]
    public void ActualMoodBroadcastQueuesRawAndTypedBeforeLocalAndHonorsExternalGate()
    {
        using var rig=new M4ControlTests.Rig();rig.ToSynced();rig.State();
        var owner=rig.Engine.Robot!;var mood=owner.MoodOwner!;
        var order=new List<string>();var payloads=new List<byte[]>();
        mood.MoodStateBroadcast+=_=>order.Add("typed");
        rig.Engine.EngineToGameMessage+=(tag,body)=>{if(tag==0x61){payloads.Add(body.ToArray());order.Add("raw");}};
        uint before=rig.Engine.NativeUiDeliveryCount;
        using var local=rig.Engine.AnimationMessages.SubscribeMessage(0x61,true,p=>
        {
            Assert.Equal(before+1,rig.Engine.NativeUiDeliveryCount); // app enqueue already happened.
            Assert.Equal(9,Assert.IsType<MoodStateMessage>(p).Emotions.Count);order.Add("local");
        });
        mood.SendEmotionsToGame();Assert.Equal(new[]{"local"},order);
        rig.Engine.DrainAppDelivery();Assert.Equal(new[]{"local","raw","typed"},order);
        var bytes=Assert.Single(payloads);Assert.Equal(37,bytes.Length);Assert.Equal((byte)9,bytes[0]);
        Assert.All(bytes.Skip(1),b=>Assert.Equal((byte)0,b));
        try
        {
            owner.HasExternalInterface=false;before=rig.Engine.NativeUiDeliveryCount;
            mood.SendEmotionsToGame();rig.Engine.DrainAppDelivery();
            Assert.Equal(before,rig.Engine.NativeUiDeliveryCount);Assert.Equal(3,order.Count);
        }
        finally{owner.HasExternalInterface=true;}
    }

    [Theory] [InlineData("")] [InlineData("null")] [InlineData("{")]
    public void MissingOrFailedMainInputKeepsActualMoodOwnerWatcherAndPeriodicOutput(string input)
    {
        string root=Directory.CreateTempSubdirectory("pilot-mood-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(root,"mood_config.json"),input,Encoding.UTF8);
            using var rig=new M4ControlTests.Rig(root);rig.ToSynced();rig.State();
            var owner=rig.Engine.Robot!;var mood=owner.MoodOwner!;
            Assert.NotNull(mood);Assert.NotEqual(0,mood.ActionCallbackId);
            uint before=mood.HistoryCounts[0];
            var reports=new List<MoodStateMessage>();
            mood.MoodStateBroadcast+=reports.Add;
            rig.Tick(10);rig.Engine.DrainAppDelivery();
            var report=Assert.Single(reports);Assert.Equal(9,report.Emotions.Count);
            Assert.All(report.Emotions,value=>Assert.Equal(0f,value));
            Assert.Equal(before+1,mood.HistoryCounts[0]);Assert.Equal(rig.Engine.Timer.SecondsF,mood.LastUpdateSeconds);
            mood.Trigger("DriveOffCharger",rig.Engine.Timer.SecondsF); // absent event warns/returns before empty global penalty.
            Assert.All(report.Emotions,value=>Assert.Equal(0f,value));
        }
        finally{Directory.Delete(root,true);}
    }
}
