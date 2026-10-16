using Cozmo.Robot;
using Xunit;
namespace Cozmo.Protocol.Tests;
[Collection("ObjectID process statics")]
public sealed class M8TeardownCheckedRowsTests
{
    private sealed class Probe:ActionRunner
    {
        internal Probe():base(8,0){}
        internal int Ends,Cancels,Preps;
        internal Action? AtDestroy;
        public override uint CheckIfDone()=>0x01000000;
        public override bool CanInterrupt()=>true;
        public override void Cancel(){Cancels++;base.Cancel();}
        public override void Prep(){Preps++;base.Prep();}
        public override void WatcherEnding(){Ends++;AtDestroy?.Invoke();base.WatcherEnding();}
    }
    [Fact] public void CancelDuringListClearReturnsOneBeforeVisitingRemainingPending()
    {
        var list=new ActionList();var first=new Probe();var second=new Probe();bool observed=false;
        list.QueueAction(QueueActionPosition.AtEnd,first);list.QueueAction(QueueActionPosition.AtEnd,second);
        list.GameSend=_=>{if(observed)return;observed=true;Assert.True(list.Cancel(-1));Assert.Equal(0,second.Ends);Assert.Equal(0,second.Cancels);};
        list.Clear();Assert.True(observed);Assert.Equal(1,first.Ends);Assert.Equal(1,second.Ends);Assert.Equal(0,first.Cancels);Assert.True(list.IsEmpty);
    }
    [Theory][InlineData(0)][InlineData(5)]
    public void ClearRejectsNowAndParallelByPreppingAndDeletingNewRunner(int position)
    {
        var list=new ActionList();var old=new Probe();var incoming=new Probe();int result=-1;
        list.QueueAction(QueueActionPosition.AtEnd,old);
        list.GameSend=_=>result=list.QueueAction((QueueActionPosition)position,incoming);
        list.Clear();Assert.Equal(1,result);Assert.Equal(1,incoming.Preps);Assert.Equal(1,incoming.Ends);Assert.Equal(0,incoming.Cancels);Assert.True(list.IsEmpty);
        // Queue's deletion is established; an IBehavior surviving its owners is not manufactured here.
    }
    [Fact] public void ReentrantSameTagDeleteReturnsZeroBeforeAnotherDestructor()
    {
        var q=new ActionQueue();var action=new Probe();bool? nested=null;
        action.AtDestroy=()=>nested=q.Delete(action);
        Assert.True(q.Delete(action));Assert.False(nested);Assert.Equal(1,action.Preps);Assert.Equal(1,action.Ends);
    }
    [Fact] public void ReentrantPendingCancelStopsAtDeleteGuardWithoutTouchingSuccessor()
    {
        var q=new ActionQueue();var first=new Probe();var second=new Probe();bool? nested=null;
        q.QueueAtEnd(first);q.QueueAtEnd(second);
        first.AtDestroy=()=>{nested=q.Cancel(-1);Assert.Equal(0,second.Ends);};
        Assert.True(q.Cancel(-1));Assert.False(nested);Assert.Equal(1,first.Ends);Assert.Equal(1,second.Ends);Assert.Equal(0,first.Cancels);Assert.Equal(0,second.Cancels);
    }
    [Fact] public void InterruptedSkipsExternalBroadcastButRetainsWatcherDestructionRecord()
    {
        var list=new ActionList();var game=new List<RobotCompletedAction>();var deferred=new List<RobotCompletedAction>();
        list.GameSend=game.Add;list.RegisterActionEndedCallback(deferred.Add);
        var action=new Probe{Watcher=list.Watcher,State=0x03000009};var q=new ActionQueue();
        Assert.True(q.Delete(action));Assert.Empty(game);Assert.Empty(deferred);list.Watcher.Update();Assert.Equal(0x03000009u,Assert.Single(deferred).Result);
    }
    [Fact] public void ConstructionAndCurrentVersusPendingCancellationKeepNativeResults()
    {
        var list=new ActionList();var running=new Probe();var pending=new Probe();var game=new List<RobotCompletedAction>();list.GameSend=game.Add;
        Assert.Equal(0x02000001u,running.State);list.QueueAction(QueueActionPosition.AtEnd,running);list.Update();Assert.Equal(0x01000000u,running.State);
        list.QueueAction(QueueActionPosition.AtEnd,pending);Assert.True(list.Cancel(-1));
        Assert.Equal(new[]{0x02000000u,0x02000001u},game.Select(r=>r.Result));Assert.Equal(1,running.Cancels);Assert.Equal(0,pending.Cancels);
    }
}
