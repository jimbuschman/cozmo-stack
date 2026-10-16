using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Xunit;
namespace Cozmo.Protocol.Tests;

// Expected IDs, order and state words are OV49/OV77's checked rows, not values read from production tables.
[Collection("M5 process statics")]
public sealed class BehaviorAudioClientCheckedRowsTests
{
    private sealed class Fixture : IDisposable
    {
        internal readonly M4ControlTests.Rig Rig=new();
        internal EngineRobot Robot=>Rig.Engine.Robot!;
        internal BehaviorAudioClient A=>Robot.BehaviorAudioA!;
        internal BehaviorAudioClient B=>Robot.BehaviorAudioB!;
        internal BehaviorPublicStateBroadcaster Public=>Robot.BehaviorPublicState;
        internal readonly List<BehaviorAudioRequest> Requests=new();
        internal Fixture(){Rig.ToSynced();Robot.BehaviorAudioRecipient=Requests.Add;}
        public void Dispose()=>Rig.Dispose();
    }
    [Fact]
    public void RobotOwnsTwoInitiallyInactiveClientsAndApiPreservesAllPayloadBits()
    {
        using var f=new Fixture();Assert.NotSame(f.A,f.B);
        foreach(var c in new[]{f.A,f.B})
        {
            Assert.Equal(0x55,c.ActiveUnlock);Assert.Equal(0x55,c.PublishedUnlock);Assert.Equal(0u,c.MusicState);
            Assert.False(c.Active);Assert.Equal(0,c.Round);Assert.Equal((byte)4,c.StageTag);
            Assert.Equal((byte)5,c.GuardStage);Assert.Equal((byte)1,c.FeedingStage);
            Assert.Equal((byte)0,c.Activity);Assert.Equal((byte)0,c.Carrying);Assert.Equal((byte)0,c.Requesting);Assert.Equal((byte)0,c.Stacked);
        }
        Assert.Equal(2,f.Rig.Engine.AnimationMessages.Count(0xA3,false));
        f.A.Init();f.B.Init();Assert.Equal(2,f.Rig.Engine.AnimationMessages.Count(0xA3,false));
        f.Rig.Robot.SetSparkedMusicState(0xF1234567);f.Rig.Tick();
        Assert.Equal(0xF1234567u,f.A.MusicState);Assert.Equal(0xF1234567u,f.B.MusicState);Assert.Empty(f.Requests);
        var native=f.Robot;var retired=new List<string>();native.BehaviorAudioRetiredForTest=retired.Add;
        f.Rig.Engine.Robots.RemoveRobot(CozmoEngine.RobotId,wasConnecting:false);
        Assert.Null(native.BehaviorAudioA);Assert.Null(native.BehaviorAudioB);
        Assert.Equal(new[]{"A","B"},retired);
        Assert.Equal(0,f.Rig.Engine.AnimationMessages.Count(0xA3,false));
    }
    [Fact]
    public void NoExternalInterfaceStillSubscribesToPublicStateAndInitDoesNotReplaceIt()
    {
        using var f=new Fixture();var pub=new BehaviorPublicStateBroadcaster(f.Robot);
        using var c=new BehaviorAudioClient(f.Robot,pub,"X",false);
        int before=f.Rig.Engine.AnimationMessages.Count(0xA3,false),calls=0;
        c.StepForTest=s=>{if(s=="world")calls++;};c.Init();c.Init();
        pub.SendUpdatedState();Assert.Equal(1,calls);Assert.Equal(before,f.Rig.Engine.AnimationMessages.Count(0xA3,false));
        c.Dispose();pub.SendUpdatedState();Assert.Equal(1,calls);
    }
    [Fact]
    public void AHandlesBeforeBInExactHandlerOrderAndNeitherActivatesWithMusicStateZero()
    {
        using var f=new Fixture();var steps=new List<string>();var activation=new List<string>();
        f.A.StepForTest=s=>steps.Add("A:"+s);f.B.StepForTest=s=>steps.Add("B:"+s);
        f.A.ActivatingForTest=()=>activation.Add("A");f.B.ActivatingForTest=()=>activation.Add("B");
        f.Robot.EightiesMusicInput=()=>throw new Exception("zero music must not ask workout");
        var logs=new List<EngineLogEntry>();f.Rig.Engine.LogEntry+=logs.Add;
        f.Public.Update(new(0x1D,0x1D,-1,0x16,null));
        string[] order={"world","round","spark","needs","dim","activity","stage cache","unlock cache"};
        Assert.Equal(order.Select(x=>"A:"+x).Concat(order.Select(x=>"B:"+x)),steps);
        Assert.Empty(activation);Assert.Empty(f.Requests);
        Assert.Equal(2,logs.Count(e=>e.Name=="BehaviorAudioClient.HandleRobotPublicStateChange.InvalidMusicState"));
        Assert.Equal(0x1D,f.A.PublishedUnlock);Assert.Equal(0x1D,f.B.PublishedUnlock);Assert.Equal((byte)4,f.Public.State.StageTag);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public void NestedWorkoutUnlockActivatesAThenBThenAWithIndependentOuterSnapshot(bool eighties)
    {
        using var f=new Fixture();var order=new List<string>();
        f.A.ActivatingForTest=()=>order.Add("A");f.B.ActivatingForTest=()=>order.Add("B");
        f.Robot.EightiesMusicInput=()=>eighties; // explicit already-cached WorkoutComponent decision input
        f.Rig.Robot.SetSparkedMusicState(0x1234);f.Rig.Tick();
        f.Public.Update(new(0x1D,0x1D,-1,0x16,null));
        Assert.Equal(new[]{"A","B","A"},order);
        Assert.Equal(eighties?10:9,f.Requests.Count);
        for(int i=0;i<3;i++)
        {
            var group=f.Requests.Skip(i*3).Take(3).ToArray();
            Assert.Equal(new[]{0x14,0x14,0x1C},group.Select(r=>r.Slot));
            Assert.Equal(new uint[]{0x972DC63C,0x03B760EF,0x093E7E04},group.Select(r=>r.Id));
            Assert.Equal(eighties?0xF0C83E53u:0xF0C83E52u,group[0].Value);
            Assert.Equal(0x1234u,group[1].Value);Assert.All(group,r=>Assert.Equal(order[i],r.Client));
        }
        if(eighties){Assert.Equal("B",f.Requests[^1].Client);Assert.Equal(0xF0C83E52u,f.Requests[^1].Value);}
        Assert.Equal(eighties?1:0,f.A.Round);Assert.Equal(0,f.B.Round);
        Assert.Equal((byte)3,f.Public.State.StageTag);Assert.Equal((byte)4,f.A.StageTag);Assert.Equal((byte)4,f.B.StageTag);
    }
    [Fact]
    public void AppInputKeepsAReferenceToPublishedUnlockAcrossTheNestedStageEmission()
    {
        using var f=new Fixture();f.Robot.EightiesMusicInput=()=>false;
        // Client saw a 1D event independently; the broadcaster still holds55. The lambda passes &client+8.
        f.A.HandleRobotPublicStateChange(f.Public.State with{UnlockId=0x1D});
        Assert.Equal(0x1D,f.A.PublishedUnlock);Assert.Equal(0x55,f.Public.State.UnlockId);
        f.A.SetSparkedMusicState(9);
        Assert.Equal(0x55,f.A.PublishedUnlock);Assert.Equal(0x55,f.A.ActiveUnlock);Assert.True(f.A.Active);
    }
    [Fact]
    public void StageDuplicateWarnsWithoutEmissionAndWideStageUsesWrappedMultiplication()
    {
        using var f=new Fixture();var p=new BehaviorPublicStateBroadcaster(f.Robot);int events=0;
        using var h=p.Subscribe(_=>events++);var logs=new List<EngineLogEntry>();f.Rig.Engine.LogEntry+=logs.Add;
        p.UpdateBroadcastBehaviorStage(3,1);Assert.Equal(1,events);Assert.Equal(0x01010101u,p.State.Stages);
        p.UpdateBroadcastBehaviorStage(3,1);Assert.Equal(1,events);
        var warning=Assert.Single(logs.Where(e=>e.Name=="PublicStateBroadcaster.UpdateUserFacingBehaviorStage"));
        Assert.Equal(EngineLogLevel.Warning,warning.Level);Assert.Null(warning.Channel);
        Assert.EndsWith("Attempting to update a stage that has already been set",warning.Text);Assert.Empty(warning.Data);
        p.UpdateBroadcastBehaviorStage(3,0x100);Assert.Equal(2,events);Assert.Equal(0x01010100u,p.State.Stages);Assert.Equal(1,p.State.Round);
        p.UpdateBroadcastBehaviorStage(4,0);p.UpdateBroadcastBehaviorStage(4,0);Assert.Equal(3,events);
    }
    [Fact]
    public void EmissionWalksTheLiveRingAndNestedEventsRetainTheirOwnState()
    {
        using var f=new Fixture();var p=new BehaviorPublicStateBroadcaster(f.Robot);var trace=new List<string>();
        RetainedSubscription? b=null,d=null;
        using var a=p.Subscribe(_=>{trace.Add("A");b!.Dispose();d??=p.Subscribe(_=>trace.Add("D"));});
        b=p.Subscribe(_=>trace.Add("B"));using var c=p.Subscribe(_=>trace.Add("C"));
        p.SendUpdatedState();Assert.Equal(new[]{"A","C","D"},trace);d?.Dispose();b.Dispose();a.Dispose();c.Dispose();
        trace.Clear();bool nested=false;
        using var a2=p.Subscribe(s=>{trace.Add("A"+s.Activity);if(!nested){nested=true;p.UpdateActivity(2);}});
        using var b2=p.Subscribe(s=>trace.Add("B"+s.Activity));
        p.UpdateActivity(1);Assert.Equal(new[]{"A1","A2","B2","B1"},trace);
    }
    [Fact]
    public void NativeUnlockWritersAndNeedDirtyGatePublishOnlyAtTheCheckedGates()
    {
        using var f=new Fixture();var p=new BehaviorPublicStateBroadcaster(f.Robot);var values=new List<BehaviorPublicState>();
        using var h=p.Subscribe(values.Add);
        p.Update(new(1,0x55,-1,0x16,null));Assert.Empty(values);Assert.Equal(0x55,p.State.UnlockId);
        p.Update(new(1,1,-1,0x16,null));Assert.Equal(1,Assert.Single(values).UnlockId);
        p.Update(new(1,0x55,-1,0x16,null));Assert.Equal(0x55,values[^1].UnlockId);Assert.Equal(2,values.Count);
        float epsilon=BitConverter.UInt32BitsToSingle(0x3727C5AC);
        p.Update(new(0x55,0x55,-1,0x16,(float.BitDecrement(epsilon),0,0)));Assert.Equal(2,values.Count);
        p.Update(new(0x55,0x55,-1,0x16,(epsilon,0,0)));Assert.Equal(3,values.Count);
        p.Update(new(0x55,0x55,-1,0x16,(float.NaN,0,0)));Assert.Equal(4,values.Count);Assert.True(float.IsNaN(p.State.Need0));
    }
    [Fact]
    public void ConstructorNeedsCacheStaysUnknownAndKnownCacheUsesExactEpsilonAndNaNGates()
    {
        using var f=new Fixture();f.A.HandleNeedsUpdates(1,2,3);
        Assert.Empty(f.Requests);Assert.Null(f.A.NeedCache(0));Assert.Null(f.A.NeedCache(1));Assert.Null(f.A.NeedCache(2));
        f.A.SetNativeNeedsCache(0,0,0);
        float epsilon=BitConverter.UInt32BitsToSingle(0x3727C5AC),nan=BitConverter.UInt32BitsToSingle(0x7FC12345);
        f.A.HandleNeedsUpdates(float.BitDecrement(epsilon),0,0);Assert.Empty(f.Requests);
        f.Robot.BehaviorAudioRecipient=r=>{Assert.Equal(r.Value,BitConverter.SingleToUInt32Bits(f.A.NeedCache(r.Id==0xA214FD39?0:r.Id==0xB7B26C62?1:2)!.Value));f.Requests.Add(r);};
        f.A.HandleNeedsUpdates(epsilon,-epsilon,nan);
        Assert.Equal(new uint[]{0xA214FD39,0xB7B26C62,0x61693ECF},f.Requests.Select(r=>r.Id));
        Assert.All(f.Requests,r=>{Assert.Equal(0x18,r.Slot);Assert.Equal(0u,r.Arg3);Assert.Equal(0u,r.Extra0);Assert.Equal(0u,r.Extra1);});
        Assert.Equal(0x7FC12345u,f.Requests[2].Value);
    }
    [Fact]
    public void CarryingAndReactionStoresHaveTheirExactSourceInfoCalls()
    {
        using var f=new Fixture();var p=new BehaviorPublicStateBroadcaster(f.Robot);
        p.ReactionNameInput=_=>"fixture trigger";
        var logs=new List<EngineLogEntry>();f.Rig.Engine.LogEntry+=logs.Add;
        p.Update(new(0x55,0x55,1,3,null));p.Update(new(0x55,0x55,-1,3,null));
        Assert.Equal(new[]{"PublicStateBroadcaster.Update.RobotCarryingObjectChanged","PublicStateBroadcaster.Update.ReactionTriggered","PublicStateBroadcaster.Update.RobotCarryingObjectChanged"},logs.Select(e=>e.Name));
        Assert.EndsWith("Robot is now carrying an object",logs[0].Text);
        Assert.EndsWith("Robot is now reacting to trigger fixture trigger",logs[1].Text);
        Assert.EndsWith("Robot is no longer carrying an object",logs[2].Text);
        Assert.All(logs,e=>{Assert.Equal(EngineLogLevel.Info,e.Level);Assert.Equal("PublicStateBroadcast",e.Channel);Assert.Empty(e.Data);});
    }
    [Theory]
    [InlineData(0,0xF0C83E52u)] [InlineData(1,0xF0C83E53u)] [InlineData(2,0xF0C83E50u)]
    [InlineData(3,0xF0C83E51u)] [InlineData(4,0xF0C83E56u)] [InlineData(5,0xF0C83E57u)]
    [InlineData(6,0xF0C83E54u)] [InlineData(7,0xF0C83E55u)] [InlineData(8,0xF0C83E5Au)]
    [InlineData(9,0xF0C83E5Bu)] [InlineData(10,0xF1C83FC5u)]
    public void RoundTableHasExactWordsAndObjectOne(int round,uint expected)
    {
        using var f=new Fixture();Assert.True(f.A.ActivateSparkedMusic(1,0,0,round));
        var request=Assert.Single(f.Requests);Assert.Equal(0x14,request.Slot);Assert.Equal(0x972DC63Cu,request.Id);Assert.Equal(expected,request.Value);Assert.Equal(1u,request.Arg3);
    }
    [Fact]
    public void RoundStoresBeforeUnsignedBoundsButMismatchedUnlockDoesNotStore()
    {
        using var f=new Fixture();Assert.True(f.A.UpdateBehaviorRound(0x55,-1));Assert.Equal(-1,f.A.Round);Assert.Empty(f.Requests);
        Assert.True(f.A.UpdateBehaviorRound(0x55,11));Assert.Equal(11,f.A.Round);Assert.False(f.A.UpdateBehaviorRound(1,3));Assert.Equal(11,f.A.Round);
        Assert.False(f.A.ActivateSparkedMusic(0,1,1,4));Assert.False(f.A.Active);Assert.Equal(11,f.A.Round);Assert.Empty(f.Requests);
    }
    [Theory]
    [InlineData(1,0u)] [InlineData(2,2320504975u)] [InlineData(5,296159617u)] [InlineData(7,0u)]
    [InlineData(8,0u)] [InlineData(9,0u)] [InlineData(12,670611050u)] [InlineData(11,670611050u)]
    [InlineData(13,670611050u)] [InlineData(15,0u)] [InlineData(16,670611050u)] [InlineData(10,2890913445u)]
    public void ActivityMapTreatsMappedZeroAsAHit(byte activity,uint state)
    {
        using var f=new Fixture();f.A.UpdateActivityMusicState(activity);
        Assert.Equal(new uint[]{0x972DC63C,0x8379F505},f.Requests.Select(r=>r.Id));
        Assert.Equal(new uint[]{0xF0C83E52,state},f.Requests.Select(r=>r.Value));Assert.All(f.Requests,r=>Assert.Equal(1u,r.Arg3));
    }
    [Fact]
    public void WorldCachesStoreBeforePostsAndActivityTwoSkipsAllWorldCacheUpdates()
    {
        using var f=new Fixture();var states=new List<(byte,byte,byte)>();
        f.Robot.BehaviorAudioRecipient=r=>{if(r.Slot==8)states.Add((f.A.Carrying,f.A.Requesting,f.A.Stacked));f.Requests.Add(r);};
        var message=f.Public.State with{Carrying=1,Requesting=1,StackHeight=2,Activity=2};
        f.A.HandleRobotPublicStateChange(message);Assert.Equal((0,0,0),((int)f.A.Carrying,(int)f.A.Requesting,(int)f.A.Stacked));Assert.Empty(states);
        f.Requests.Clear();f.A.HandleRobotPublicStateChange(message with{Activity=1});
        Assert.Equal(new[]{((byte)1,(byte)0,(byte)0),((byte)1,(byte)1,(byte)0),((byte)1,(byte)1,(byte)1)},states);
        Assert.Equal(new uint[]{0xBDA79E15,0x6CAF712C,0xEC3490EC},f.Requests.Where(r=>r.Slot==8).Select(r=>r.Id));
        Assert.All(f.Requests.Where(r=>r.Slot==8),r=>{Assert.Equal(1u,r.Value);Assert.True(r.EmptyCallback);});
    }
    [Fact]
    public void GuardDogKeepsItsAsymmetricExitAndFeedingUsesCachedRoundInsteadOfItsStage()
    {
        using var f=new Fixture();var m=f.Public.State with{StageTag=1,Stages=0x00000300};
        f.A.HandleRobotPublicStateChange(m);
        Assert.Equal(new uint[]{0x3D69880F,0xF0C83E51},f.Requests.Select(r=>r.Value));Assert.Equal((byte)3,f.A.GuardStage);
        f.Requests.Clear();f.A.HandleRobotPublicStateChange(m with{StageTag=2});Assert.Equal((byte)3,f.A.GuardStage);
        f.A.HandleRobotPublicStateChange(m with{StageTag=2});Assert.Equal((byte)5,f.A.GuardStage);Assert.Empty(f.Requests);
        f.A.ActivateSparkedMusic(1,0,0,7);f.Requests.Clear();
        f.A.HandleRobotPublicStateChange(f.Public.State with{UnlockId=2,StageTag=0,Stages=3});
        Assert.Equal(0xF0C83E55u,Assert.Single(f.Requests).Value);Assert.Equal((byte)3,f.A.FeedingStage);Assert.Equal(7,f.A.Round);
    }
    [Fact]
    public void OutOfRangeGuardAndFeedingReadsSkipOnlyTheirUndefinedTails()
    {
        using var f=new Fixture();f.A.HandleRobotPublicStateChange(f.Public.State with{StageTag=1,Stages=0x00000C00});
        Assert.Equal(0x3D69880Fu,Assert.Single(f.Requests).Value);Assert.Equal((byte)5,f.A.GuardStage);
        f.Requests.Clear();f.A.HandleRobotPublicStateChange(f.Public.State with{UnlockId=1,StageTag=0,Stages=2});
        Assert.Empty(f.Requests);Assert.Equal((byte)2,f.A.FeedingStage);Assert.Equal(12,f.A.Round);
    }
    [Fact]
    public void HandlerIsolationContinuesToTheSecondClientAfterARecipientThrows()
    {
        using var f=new Fixture();var trace=new List<string>();
        f.Robot.BehaviorAudioRecipient=r=>{trace.Add(r.Client);if(r.Client=="A")throw new InvalidOperationException("fixture failure");};
        f.Public.UpdateRequestingGame(1);
        Assert.Equal(new[]{"A","B"},trace);Assert.Equal((byte)1,f.A.Requesting);Assert.Equal((byte)1,f.B.Requesting);
    }
    [Fact]
    public void SparkCancellationClearsTheClientFieldsWithoutAddingAnAudioStop()
    {
        using var f=new Fixture();f.Rig.Robot.SetSparkedMusicState(7);f.Rig.Tick();
        f.Public.Update(new(1,1,-1,0x16,null));Assert.True(f.A.Active);Assert.True(f.B.Active);f.Requests.Clear();
        f.Public.Update(new(1,0x55,-1,0x16,null));
        foreach(var client in new[]{f.A,f.B}){Assert.False(client.Active);Assert.Equal(0x55,client.ActiveUnlock);Assert.Equal(0u,client.MusicState);Assert.Equal(0,client.Round);}
        Assert.Empty(f.Requests);
    }
    [Fact]
    public void DimEventsPrecedeActivitySwitchesAndActivityMissHasOnlyItsTwoInfoLogs()
    {
        using var f=new Fixture();f.A.HandleRobotPublicStateChange(f.Public.State with{Activity=15});
        Assert.Equal(new[]{8,0x14,0x14},f.Requests.Select(r=>r.Slot));Assert.Equal(0xD53B75B2u,f.Requests[0].Id);
        f.Requests.Clear();f.A.HandleRobotPublicStateChange(f.Public.State with{Activity=5});
        Assert.Equal(0x669A53A8u,f.Requests[0].Id);Assert.Equal(3,f.Requests.Count);
        f.Requests.Clear();var logs=new List<EngineLogEntry>();f.Rig.Engine.LogEntry+=logs.Add;
        f.A.ActivityNameInput=_=>"fixture activity";f.A.ActivityLogChannelInput="fixture channel";
        f.A.UpdateActivityMusicState(0);Assert.Empty(f.Requests);
        Assert.Equal(new[]{"RobotAudioClient.SetFreeplayMusic","RobotAudioClient.UpdateActivityMusicState.NoFreeplayMusic"},logs.Select(e=>e.Name));
        Assert.All(logs,e=>{Assert.Equal(EngineLogLevel.Info,e.Level);Assert.Equal("fixture channel",e.Channel);Assert.Empty(e.Data);});
        Assert.EndsWith("ActivityName 'fixture activity'",logs[0].Text);Assert.EndsWith("No freeplay music for ActivityName 'fixture activity'",logs[1].Text);
    }
}
