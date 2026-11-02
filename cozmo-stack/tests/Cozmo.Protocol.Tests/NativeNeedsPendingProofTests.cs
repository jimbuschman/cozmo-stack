using Cozmo.Robot.Behavior;
using Cozmo.Robot;
using Cozmo.Protocol;
using Xunit;
using Fixture=Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class NativeNeedsPendingProofTests
{
    [Fact]
    public void FreshContextDirectCommunicationUsesConstructorZeroPayload()
    {
        using var f=new Fixture();var needs=f.Rig.Engine.Needs;
        Assert.True(needs.NativePendingKnownFalse);Assert.True(needs.NativeRewardPayloadKnownForTest);
        var reports=new List<byte[]>();f.Rig.Engine.EngineToGameMessage+=(tag,body)=>{if(tag==0x65)reports.Add(body.ToArray());};
        needs.SparksRewardCommunicatedToUser();f.Rig.Engine.DrainAppDelivery();
        Assert.Equal(new byte[]{0,0,0,0,0},Assert.Single(reports));Assert.True(needs.NativePendingKnownFalse);
    }
    [Fact]
    public void SourceGuardExitsAndShippedZeroWeightsPreserveProof()
    {
        using var f=new Fixture();var needs=f.Rig.Engine.Needs;needs.Connected=true;
        long epoch=needs.NativePendingHistoryEpoch;
        needs.SetPaused(true);Assert.False(needs.RegisterNeedsActionCompleted("Fall"));needs.SetPaused(false);
        needs.Connected=false;Assert.False(needs.RegisterNeedsActionCompleted("Fall"));needs.Connected=true;
        Assert.False(needs.RegisterNeedsActionCompleted("not-a-native-action"));
        Assert.True(needs.RegisterNeedsActionCompleted("Fall"));Assert.True(needs.RegisterNeedsActionCompleted("SeeFace"));
        Assert.False(needs.RegisterNeedsActionCompleted("SeeFace")); // actual source cooldown
        Assert.True(needs.NativePendingKnownFalse);Assert.Equal(epoch,needs.NativePendingHistoryEpoch);
    }
    [Theory]
    [InlineData(0x00000000u,true)] [InlineData(0x3727C5ABu,true)] [InlineData(0xB727C5ABu,true)]
    [InlineData(0x3727C5ACu,false)] [InlineData(0xB727C5ACu,false)] [InlineData(0x7FC00000u,false)]
    public void SuppliedWeightUsesSourceF32MagnitudeBoundary(uint bits,bool preserved)
    {
        using var f=new Fixture();var needs=f.Rig.Engine.Needs;needs.Connected=true;
        var rows=Assert.IsAssignableFrom<IDictionary<string,NeedsActionDelta>>(needs.Actions);var original=rows["Fall"];
        rows["Fall"]=original with {FreeplaySparksRewardWeight=BitConverter.UInt32BitsToSingle(bits)};
        try
        {
            long epoch=needs.NativePendingHistoryEpoch;Assert.True(needs.RegisterNeedsActionCompleted("Fall"));
            Assert.Equal(preserved,needs.NativePendingKnownFalse);
            if(preserved)Assert.Equal(epoch,needs.NativePendingHistoryEpoch);else Assert.True(needs.NativePendingHistoryEpoch>epoch);
        }
        finally {rows["Fall"]=original;}
    }
    [Fact]
    public void ZeroWeightOuterCallDoesNotUndoNestedHostMutation()
    {
        using var f=new Fixture();var needs=f.Rig.Engine.Needs;needs.Connected=true;bool nested=false;
        void OnLog(string text){if(!nested && text.StartsWith("needs.action_completed Fall:")){nested=true;needs.SparksRewardPending=true;}}
        needs.Log+=OnLog;try {Assert.True(needs.RegisterNeedsActionCompleted("Fall"));}finally {needs.Log-=OnLog;}
        Assert.True(nested);Assert.False(needs.TryGetNativePending(out _));Assert.True(needs.SparksRewardPending);
    }
    [Fact]
    public void MissingHostConfigRowIsNotInventedAsZeroOrARewardAttempt()
    {
        using var f=new Fixture();var needs=f.Rig.Engine.Needs;needs.Connected=true;
        var rows=Assert.IsAssignableFrom<IDictionary<string,NeedsActionDelta>>(needs.Actions);var original=rows["Fall"];
        rows.Remove("Fall");long epoch=needs.NativePendingHistoryEpoch;
        try {Assert.False(needs.RegisterNeedsActionCompleted("Fall"));Assert.True(needs.NativePendingKnownFalse);Assert.Equal(epoch,needs.NativePendingHistoryEpoch);}
        finally {rows["Fall"]=original;}
    }
    [Fact]
    public void ZeroWeightOuterCallDoesNotUndoNestedWeightedFrontier()
    {
        using var f=new Fixture();var needs=f.Rig.Engine.Needs;needs.Connected=true;bool nested=false;
        void OnLog(string text){if(!nested && text.StartsWith("needs.action_completed Fall:")){nested=true;needs.RegisterNeedsActionCompleted("RollACube");}}
        needs.Log+=OnLog;try {Assert.True(needs.RegisterNeedsActionCompleted("Fall"));}finally {needs.Log-=OnLog;}
        Assert.True(nested);Assert.False(needs.TryGetNativePending(out _));
    }
    [Fact]
    public void ExplicitCommunicationRecoversOnlyPendingWhenPayloadIsUnknown()
    {
        using var f=new Fixture();var needs=f.Rig.Engine.Needs;needs.SparksRewardPending=true;
        int reports=0;using var sub=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x65,true,_=>reports++);
        needs.SparksRewardCommunicatedToUser();
        Assert.True(needs.TryGetNativePending(out bool pending));Assert.False(pending);
        Assert.False(needs.NativeRewardPayloadKnownForTest);Assert.Equal("",needs.NativeRewardSnapshotForTest().Key);Assert.Equal(0,reports);
    }
    [Fact]
    public void MissingPayloadDiagnosticReentryDoesNotOverwriteNewUnknownState()
    {
        using var f=new Fixture();var needs=f.Rig.Engine.Needs;needs.SparksRewardPending=true;bool nested=false;
        void OnMissing(string text){if(!nested && text.Contains("reward amount/display payload unavailable")){nested=true;needs.SparksRewardPending=true;}}
        SteppedBehavior.ResetMissingForTests();SteppedBehavior.MissingReported+=OnMissing;
        try {needs.SparksRewardCommunicatedToUser();}finally {SteppedBehavior.MissingReported-=OnMissing;}
        Assert.True(nested);Assert.False(needs.TryGetNativePending(out _));Assert.True(needs.SparksRewardPending);
    }
    [Fact]
    public void WeightedActionReportsMissingAndExplicitCommunicationRecoversOnlyPending()
    {
        using var f=new Fixture();var needs=f.Rig.Engine.Needs;needs.Connected=true;var missing=new List<string>();
        int reports=0;using var sub=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x65,true,_=>reports++);
        SteppedBehavior.ResetMissingForTests();SteppedBehavior.MissingReported+=missing.Add;
        try
        {
            Assert.True(needs.RegisterNeedsActionCompleted("RollACube"));Assert.False(needs.TryGetNativePending(out _));
            Assert.Contains(missing,s=>s.Contains("persisted notification determinant unavailable"));
            Assert.Contains(missing,s=>s.Contains("weighted reward withheld: notification effects or shared RNG history unavailable"));
            needs.SparksRewardCommunicatedToUser();Assert.True(needs.NativePendingKnownFalse);
            Assert.False(needs.NativeRewardPayloadKnownForTest);Assert.Equal(0,reports);
            Assert.Contains(missing,s=>s.Contains("reward amount/display payload unavailable"));
        }
        finally {SteppedBehavior.MissingReported-=missing.Add;}
    }
    [Fact]
    public void NoInterfaceClearsPendingBeforeFaultAndPreservesUnmovedKey()
    {
        using var f=new Fixture();var needs=f.Rig.Engine.Needs;
        needs.SupplyNativeRewardForTest(true,4,"A");needs.SparksRewardPending=true;
        f.Rig.Engine.NeedsContextExternalInterfaceAvailable=false;var missing=new List<string>();
        SteppedBehavior.ResetMissingForTests();SteppedBehavior.MissingReported+=missing.Add;
        try
        {
            needs.SparksRewardCommunicatedToUser();var state=needs.NativeRewardSnapshotForTest();
            Assert.True(state.Known);Assert.False(state.Pending);Assert.Equal(4,state.Amount);Assert.Equal("A",state.Key);
            Assert.False(needs.NativeRewardPayloadKnownForTest);Assert.Contains(missing,s=>s.Contains("0x00696E7E"));
        }
        finally {SteppedBehavior.MissingReported-=missing.Add;f.Rig.Engine.NeedsContextExternalInterfaceAvailable=true;}
    }
    [Fact]
    public void ActualFallingStoppedRegistersOnceBeforeDasAndBroadcast()
    {
        using var f=new Fixture(preloadAnimations:true,pumpAnimations:true);var needs=f.Rig.Engine.Needs;
        f.Rig.Robot.ActivateHighLevelActivity("Freeplay");f.Rig.Tick();Assert.False(needs.IsPaused);
        using var observer=FreeplayStack.CreateNative(f.Rig.Robot);var order=new List<string>();
        needs.Log+=line=>{if(line.StartsWith("needs.action_completed Fall:"))order.Add("needs");};
        f.Rig.Robot.Sensors.NeedsActionCompleted+=_=>order.Add("public-action");
        f.Rig.Engine.LogEntry+=entry=>{if(entry.Name=="robot.falling_event")order.Add("das");};
        f.Rig.Robot.Sensors.FallingStoppedInternal+=_=>order.Add("broadcast");
        f.Rig.Data(new FallingStopped{Timestamp=10,DurationMs=20,ImpactIntensity=1000f});f.Rig.Tick();
        Assert.Equal(new[]{"das","broadcast"},order);order.Clear();
        f.Rig.Data(new FallingStopped{Timestamp=11,DurationMs=21,ImpactIntensity=1001f});f.Rig.Tick();
        Assert.Equal(new[]{"needs","public-action","das","broadcast"},order);Assert.True(needs.NativePendingKnownFalse);
    }
    [Fact]
    public void ReplacementDuringFallDoesNotBroadcastThroughNewOwner()
    {
        using var f=new Fixture();var needs=f.Rig.Engine.Needs;f.Rig.Robot.ActivateHighLevelActivity("Freeplay");f.Rig.Tick();
        var original=f.Robot;EngineRobot? replacement=null;int actions=0,broadcasts=0;
        needs.Log+=line=>{if(replacement is null && line.StartsWith("needs.action_completed Fall:")){f.Rig.Engine.Robots.RemoveRobot(1,false);replacement=f.Rig.Engine.Robots.AddRobot(1,withRic:false);}};
        f.Rig.Robot.Sensors.NeedsActionCompleted+=_=>actions++;f.Rig.Robot.Sensors.FallingStoppedInternal+=_=>broadcasts++;
        f.Rig.Data(new FallingStopped{Timestamp=12,DurationMs=22,ImpactIntensity=1001f});f.Rig.Tick();
        Assert.NotNull(replacement);Assert.NotSame(original,replacement);Assert.Equal(0,actions);Assert.Equal(0,broadcasts);
    }
    [Fact]
    public void StandaloneHandleWithSameNeedsDoesNotRegisterFallTwice()
    {
        using var f=new Fixture();var needs=f.Rig.Engine.Needs;f.Rig.Robot.ActivateHighLevelActivity("Freeplay");f.Rig.Tick();
        var context=new BehaviorContext{Robot=f.Rig.Robot,Triggers=new AnimationTriggerMap()};
        string obb=new DirectoryInfo(f.Rig.Engine.Options.ResourcesPath!).Parent!.Parent!.FullName;
        using var standalone=FreeplayStack.CreateStandalone(obb,f.Rig.Robot,context,
            ()=>f.Rig.Engine.Timer.SecondsF,vision:f.M.Vision,m:f.M,needs:needs,withReactions:false);
        int calls=0;needs.Log+=line=>{if(line.StartsWith("needs.action_completed Fall:"))calls++;};
        f.Rig.Data(new FallingStopped{Timestamp=13,DurationMs=23,ImpactIntensity=1001f});f.Rig.Tick();
        Assert.Equal(1,calls);Assert.True(needs.NativePendingKnownFalse);
    }
}
