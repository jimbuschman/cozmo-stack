using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Transport;
using Xunit;
namespace Cozmo.Protocol.Tests;

[Collection("ObjectID process statics")]
public sealed class M8ActionCheckedRowsTests
{
    private sealed class Probe:ActionRunner
    {
        internal bool Locked;
        internal float Clock,Pre,Post;
        internal uint MovingMask;
        internal readonly List<string> Owners=new();
        internal uint Result=EngineActionResult.Running;
        internal int InitResult,InitCalls,CheckCalls,UnionCalls,PrepCalls,Ends;
        internal readonly List<string> Calls=new();
        internal readonly Queue<float> TimeoutReads=new();
        internal int TimeoutCalls;
        internal Probe():base(8,1){}
        protected override string ActionName=>"probe";
        protected override float EngineClockSeconds=>Clock;
        protected override float PreDelaySeconds=>Pre;
        protected override float PostDelaySeconds=>Post;
        protected override bool HeadTrackMoving=>(MovingMask&1)!=0;
        protected override bool LiftTrackMoving=>(MovingMask&2)!=0;
        protected override bool BodyTrackMoving=>(MovingMask&4)!=0;
        protected override bool AreAllTracksLockedBy(uint mask,string owner){Owners.Add(owner);return true;}
        protected override void StopHeadTrack()=>Calls.Add("head");
        protected override void StopLiftTrack()=>Calls.Add("lift");
        protected override void StopBodyTrack()=>Calls.Add("body");
        public override float TimeoutSeconds{get{TimeoutCalls++;return TimeoutReads.Count>0?TimeoutReads.Dequeue():base.TimeoutSeconds;}set=>base.TimeoutSeconds=value;}
        protected override bool AreAnyTracksLocked(uint mask)=>Locked;
        protected override void LockTracks(uint mask,string owner)=>Calls.Add("lock");
        protected override void UnlockTracksInternal(uint mask,string owner)=>Calls.Add("unlock");
        public override int Init(){InitCalls++;Calls.Add("init");return InitResult;}
        public override uint CheckIfDone(){CheckCalls++;return Result;}
        public override bool CanInterrupt()=>true;
        public override uint GetCompletionUnion()=>unchecked((uint)++UnionCalls);
        public override void Prep(){PrepCalls++;Calls.Add("prep");base.Prep();}
        public override void WatcherEnding(){Ends++;Calls.Add("delete");base.WatcherEnding();}
    }
    private sealed class Input:IActionRunner
    {
        public string NativeName=>"input";
        public uint State{get;set;}=0x02000001;
        public byte RetriesRemain{get;set;}
        public uint OriginalTag{get;set;}
        public uint Tag{get;set;}
        public int Type{get;set;}=8;
        public uint RequiredTrackMask{get;set;}
        public bool SuppressTrackLocking{get;set;}
        internal int Preps,Deletes;
        public uint Update()=>State;
        public void Cancel(){if(State!=0x02000001)State=0x02000000;}
        public void Reset(bool unlockTracks)=>State=0x02000001;
        public bool CanInterrupt()=>true;
        public void Prep()=>Preps++;
        public void WatcherEnding()=>Deletes++;
        public void UnlockTracks(){}
        public bool SetTag(uint tag){Tag=tag;return true;}
    }
    private sealed record Entry(EngineLogLevel Level,string? Channel,string Key,string Text);
    private static ActionList List(List<Entry> entries,bool disabled=false,uint? robotId=7)=>new(sourceLog:(l,c,k,t)=>entries.Add(new(l,c,k,t)),robotId:robotId){ExternalActionsDisabled=disabled};
    [Fact]
    public void NullActionIsErrorAndSetsTheSourceErrorFlagWithoutCreatingAQueue()
    {
        bool old=EngineErrorState.ErrorFlagSet;
        try
        {
            EngineErrorState.ErrorFlagSet=false;var logs=new List<Entry>();var list=List(logs);
            Assert.Equal(1,list.QueueAction(QueueActionPosition.AtEnd,null));Assert.True(list.IsEmpty);
            Assert.Equal(new Entry(EngineLogLevel.Error,null,"ActionList.QueueAction.NullAction","Can't queue null action"),Assert.Single(logs));Assert.True(EngineErrorState.ErrorFlagSet);
        }
        finally{EngineErrorState.ErrorFlagSet=old;}
    }
    [Theory]
    [InlineData(0u,false)] [InlineData(1u,true)] [InlineData(1000000u,true)] [InlineData(1000001u,false)]
    [InlineData(2000000u,false)] [InlineData(2000001u,true)] [InlineData(3000000u,true)] [InlineData(3000001u,false)]
    public void ExternalDisabledUsesTheTwoExactUnsignedTagRanges(uint tag,bool discarded)
    {
        var logs=new List<Entry>();var list=List(logs,true);var action=new Input{Tag=tag};
        Assert.Equal(0,list.QueueAction(QueueActionPosition.AtEnd,action));
        Assert.Equal(discarded?1:0,action.Deletes);Assert.Equal(discarded?0:1,list.QueueAt(0)!.PendingCount);
        if(discarded)Assert.Equal(new Entry(EngineLogLevel.Info,"Unnamed","ActionQueue.QueueAction.ExternalActionsDisabled","Ignoring DRIVE_STRAIGHT action while external actions are disabled"),Assert.Single(logs));else Assert.Empty(logs);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void BadTagDiscardBindsTheWatcherBeforeDeleteAndHasTheCorrectDiagnostic(bool disabled)
    {
        bool old=EngineErrorState.ErrorFlagSet;
        try
        {
            EngineErrorState.ErrorFlagSet=false;var logs=new List<Entry>();var list=List(logs,disabled);
            var game=new List<RobotCompletedAction>();list.GameSend=game.Add;
            var action=new Probe{State=0x03000006};
            Assert.Equal(0,list.QueueAction((QueueActionPosition)255,action)); // discard front end precedes invalid position
            Assert.Equal(1,action.Ends);Assert.Equal(0,list.QueueAt(0)!.PendingCount);Assert.Equal(0x03000006u,Assert.Single(game).Result);
            var entry=Assert.Single(logs.Where(e=>e.Key.StartsWith("ActionQueue.QueueAction.")));
            Assert.Equal(disabled?EngineLogLevel.Info:EngineLogLevel.Error,entry.Level);
            Assert.Equal(disabled?"Ignoring DRIVE_STRAIGHT action while external actions are disabled":"Failed to set tag, deleting action DRIVE_STRAIGHT",entry.Text);
            Assert.Equal(!disabled,EngineErrorState.ErrorFlagSet);
        }
        finally{EngineErrorState.ErrorFlagSet=old;}
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ArmUnorderedPreDelayWaitsWithoutInit(bool nanClock)
    {
        var logs=new List<Entry>();var list=List(logs);var action=new Probe{Clock=nanClock?float.NaN:0,Pre=nanClock?0:float.NaN};
        list.QueueAction(QueueActionPosition.AtEnd,action);Assert.Equal(0,list.Update());
        Assert.Equal(0,action.InitCalls);Assert.Equal(0,action.CheckCalls);Assert.Equal(0x01000000u,action.State);
        list.Clear();
    }
    [Fact]
    public void RunningDoesNotRunCompletionCallbacksAndTerminalCallbacksWalkLiveAppends()
    {
        var logs=new List<Entry>();var list=List(logs);var action=new Probe();var results=new List<uint>();
        action.AddCompletionCallback(r=>{results.Add(r);action.AddCompletionCallback(results.Add);});
        list.QueueAction(QueueActionPosition.AtEnd,action);list.Update();Assert.Empty(results);
        action.Result=0;list.Update();Assert.Equal(new uint[]{0,0},results);Assert.True(list.IsEmpty);
        Assert.Contains(logs,e=>e.Key=="IAction.Update.PreconditionsMet"&&e.Level==EngineLogLevel.Debug&&e.Channel=="Actions");
        Assert.Contains(logs,e=>e.Key=="IActionRunner.Update.ActionCompleted"&&e.Text==$"probe [{unchecked((int)action.Tag)}] succeeded with state SUCCESS.");
    }
    [Fact]
    public void TimeoutReadsTheVirtualTimeoutAgainForItsLog()
    {
        var logs=new List<Entry>();var list=List(logs);var action=new Probe{StartTime=0,Clock=1};action.TimeoutReads.Enqueue(1);action.TimeoutReads.Enqueue(2);
        list.QueueAction(QueueActionPosition.AtEnd,action);Assert.Equal(1,list.Update());
        Assert.Equal(2,action.TimeoutCalls);Assert.Equal(0,action.InitCalls);
        Assert.Contains(logs,e=>e.Key=="IAction.Update.TimedOut"&&e.Text=="probe timed out after 2.0 seconds.");
    }
    [Fact]
    public void RetryUsesTheActualRobotIdAndDoesNotReacquireTheRunnerLock()
    {
        var logs=new List<Entry>();var list=List(logs,robotId:42);var action=new Probe{InitResult=0x04000000,RetriesRemain=1};
        list.QueueAction(QueueActionPosition.AtEnd,action,1);list.Update();Assert.Equal(0x01000000u,action.State);Assert.Equal(-1f,action.StartTime);
        Assert.Contains(logs,e=>e.Key=="IAction.Update.CurrentActionFailedRetrying"&&e.Text=="Robot 42 failed running action probe. Retrying.");
        action.InitResult=0;list.Update();Assert.Equal(1,action.Calls.Count(s=>s=="lock"));list.Clear();
    }
    [Fact]
    public void TracksLockedIsDeletedWithoutInitRetryOrActionCompletedLog()
    {
        var logs=new List<Entry>();var list=List(logs);var action=new Probe{Locked=true,RetriesRemain=3};var results=new List<RobotCompletedAction>();
        list.RegisterActionEndedCallback(results.Add);Assert.True(action.SetTag(0xF0000001));list.QueueAction(QueueActionPosition.AtEnd,action,3);Assert.Equal(1,list.Update());
        Assert.True(list.IsEmpty);Assert.Equal(0,action.InitCalls);Assert.Equal(0,action.CheckCalls);Assert.Equal((byte)3,action.RetriesRemain);
        Assert.Equal(1,action.Ends);Assert.Equal(1,action.PrepCalls);Assert.Equal(0x03000019u,Assert.Single(results).Result);
        Assert.Contains(logs,e=>e.Key=="IActionRunner.Update.TracksLocked"&&e.Text=="Action probe [-268435455] not running because required tracks are locked");
        Assert.DoesNotContain(logs,e=>e.Key=="IActionRunner.Update.ActionCompleted");
    }
    [Fact]
    public void CurrentIsNullAtBroadcastAndNoReplacementRunsUntilNextQueueTick()
    {
        var logs=new List<Entry>();var list=List(logs);var action=new Probe{Result=0};var next=new Probe();
        list.GameSend=r=>{Assert.Null(list.QueueAt(0)!.Current);Assert.False(list.Cancel(r.Tag));list.QueueAction(QueueActionPosition.Next,next);};
        list.QueueAction(QueueActionPosition.AtEnd,action);list.Update();
        Assert.Equal(0,next.InitCalls);Assert.Same(next,list.QueueAt(0)!.Front);list.GameSend=null;list.Clear();
    }
    [Fact]
    public void PendingUnlinksBeforeBroadcastAndARequeueAfterTheLastDeletionSurvivesCancel()
    {
        var logs=new List<Entry>();var list=List(logs);var action=new Probe();var next=new Probe();
        list.GameSend=r=>{Assert.Equal(0,list.QueueAt(0)!.PendingCount);list.QueueAction(QueueActionPosition.AtEnd,next);};
        list.QueueAction(QueueActionPosition.AtEnd,action);Assert.True(list.Cancel(-1));
        Assert.Same(next,list.QueueAt(0)!.Front);Assert.Equal(0x02000001u,next.State);list.GameSend=null;list.Clear();
    }
    [Fact]
    public void GameSnapshotUsesTheDerivedGetterButWatcherDestructionUsesPrepCache()
    {
        var logs=new List<Entry>();var list=List(logs);var action=new Probe();var game=new List<RobotCompletedAction>();var deferred=new List<RobotCompletedAction>();
        list.GameSend=game.Add;list.RegisterActionEndedCallback(deferred.Add);list.QueueAction(QueueActionPosition.AtEnd,action);list.Cancel(-1);
        Assert.Equal(2u,Assert.Single(game).CompletionUnion);list.Update();Assert.Equal(1u,Assert.Single(deferred).CompletionUnion);Assert.Equal(2,action.UnionCalls);
    }
    [Fact]
    public void WatcherInsertionOfALaterCallbackIsVisitedInTheSameRecord()
    {
        var logs=new List<Entry>();var list=List(logs);var order=new List<int>();bool added=false;
        list.RegisterActionEndedCallback(_=>{order.Add(1);if(!added){added=true;list.RegisterActionEndedCallback(_=>order.Add(2));}});
        list.QueueAction(QueueActionPosition.AtEnd,new Probe{Result=0});list.Update();Assert.Equal(new[]{1,2},order);
    }
    [Fact]
    public void ClearReentryReturnsBeforeTouchingAnotherQueue()
    {
        var logs=new List<Entry>();var list=List(logs);int broadcasts=0;
        list.GameSend=_=>{broadcasts++;list.Clear();};list.QueueAction(QueueActionPosition.AtEnd,new Probe());list.Clear();
        Assert.Equal(1,broadcasts);Assert.True(list.IsEmpty); // outer clearing guard, not an assertion of native postorder tree shape
    }
    [Fact]
    public void StoredTerminalValueSkipsInitAndStillRunsTheRunnerCompletionLog()
    {
        var logs=new List<Entry>();var list=List(logs);var action=new Probe{State=0};
        list.QueueAction(QueueActionPosition.AtEnd,action);list.Update();Assert.Equal(0,action.InitCalls);Assert.Equal(0,action.CheckCalls);
        Assert.Contains(logs,e=>e.Key=="IActionRunner.Update.ActionCompleted");
    }
    [Fact]
    public void F32PrePlusPostAssociationRoundsAfterEachAdd()
    {
        var logs=new List<Entry>();var list=List(logs);var action=new Probe{State=0x01000000,Initialized=true,StartTime=1,Clock=1,Pre=BitConverter.UInt32BitsToSingle(0x33800000),Post=BitConverter.UInt32BitsToSingle(0x33800000)};
        list.QueueAction(QueueActionPosition.AtEnd,action);list.Update();Assert.Equal(1,action.CheckCalls);list.Clear();
    }
    [Fact]
    public void NaNStartIsNotRestampedAndWaitsWithoutInit()
    {
        var logs=new List<Entry>();var list=List(logs);var action=new Probe{StartTime=float.NaN};
        list.QueueAction(QueueActionPosition.AtEnd,action);list.Update();Assert.True(float.IsNaN(action.StartTime));Assert.Equal(0,action.InitCalls);list.Clear();
    }
    [Fact]
    public void NowAndClearStoresIncomingRetriesAfterCancelCallbacks()
    {
        var logs=new List<Entry>();var list=List(logs);var old=new Probe();var incoming=new Probe{RetriesRemain=99};
        list.QueueAction(QueueActionPosition.AtEnd,old);list.Update();
        list.GameSend=_=>Assert.Equal((byte)99,incoming.RetriesRemain);
        list.QueueAction(QueueActionPosition.NowAndClearRemaining,incoming,7);Assert.Equal((byte)7,incoming.RetriesRemain);
        list.GameSend=null;list.Clear();
    }
    [Fact]
    public void SelfErasedWatcherCallbackSkipsItsUndefinedSuccessorButKeepsLaterRecords()
    {
        var logs=new List<Entry>();var list=List(logs);var delivered=new List<uint>();int handle=0;
        handle=list.RegisterActionEndedCallback(_=>Assert.True(list.UnregisterActionEndedCallback(handle)));
        list.RegisterActionEndedCallback(r=>delivered.Add(r.Tag));
        var a=new Probe{Result=0};var b=new Probe{Result=0};list.QueueAction(QueueActionPosition.AtEnd,a);list.QueueAction(QueueActionPosition.InParallel,b);
        list.Update();Assert.Equal(new[]{b.Tag},delivered);
    }
    [Fact]
    public void DestructorStopsUseUnsignedCurrentOwnerAndLogOriginalTagBeforeUnlock()
    {
        var logs=new List<Entry>();var list=List(logs);var action=new Probe{MovingMask=7};Assert.True(action.SetTag(0xF0000001));
        list.QueueAction(QueueActionPosition.AtEnd,action);list.Update();action.Calls.Clear();logs.Clear();list.Cancel(-1);
        Assert.Equal(new[]{"prep","delete","head","lift","body","unlock"},action.Calls);
        Assert.Equal(new[]{"4026531841","4026531841","4026531841"},action.Owners);
        Assert.Contains(logs,e=>e.Key=="IActionRunner.Destroy.StopMovement"&&e.Channel=="Actions"&&e.Text==$"Stopping movement on the following tracks since they were locked and are still moving: HEAD_TRACK, LIFT_TRACK, BODY_TRACK, [probe][{unchecked((int)action.OriginalTag)}]");
    }

    [Theory]
    [InlineData(6)] [InlineData(255)]
    public void InvalidPositionSetsErrorWithoutDeletingOrCreatingQueue(byte position)
    {
        bool old=EngineErrorState.ErrorFlagSet;
        try
        {
            EngineErrorState.ErrorFlagSet=false;
            var list=List(new());var action=new Input();
            Assert.Equal(1,list.QueueAction((QueueActionPosition)position,action));
            Assert.True(EngineErrorState.ErrorFlagSet);Assert.True(list.IsEmpty);Assert.Equal(0,action.Deletes);
            // Exact Error key/format/EnumToString remains MISSING, not a fabricated log oracle.
        }
        finally{EngineErrorState.ErrorFlagSet=old;}
    }

}
