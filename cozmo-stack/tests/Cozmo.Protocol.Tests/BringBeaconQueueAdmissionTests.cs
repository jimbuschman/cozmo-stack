using Cozmo.Robot;
using Xunit;
namespace Cozmo.Protocol.Tests;

// Host-only reentry guard over the actual ActionList/QueueNow and local watcher.
public sealed class BringBeaconQueueAdmissionTests
{
    private sealed class Runner:ActionRunner
    {
        internal Runner():base(8,0){}
        protected override string ActionName=>"queue-guard-test";
        public override bool CanInterrupt()=>false;
        public override uint CheckIfDone()=>EngineActionResult.Running;
        public override uint Update(){State=EngineActionResult.Running;return State;}
    }
    [Fact]
    public void PreviousCompletionReentryNeverInsertsOrCompletesStaleIncoming()
    {
        var list=new ActionList();list.Watcher.HasExternalInterface=true;
        var old=new Runner();var replacement=new Runner();var stale=new Runner();
        var completed=new List<uint>();bool current=true;
        list.Watcher.RobotCompletedActionBroadcast+=r=>
        {
            completed.Add(r.Tag);
            if(r.Tag!=old.Tag)return;
            current=false;
            Assert.Equal(0,list.QueueAction(QueueActionPosition.AtEnd,replacement));
        };
        try
        {
            Assert.Equal(0,list.QueueAction(QueueActionPosition.AtEnd,old));list.Update();
            Assert.Equal(2,list.QueueActionGuardedNow(stale,0,()=>current));
            Assert.True(stale.DestructionObserved);Assert.False(ActionRunnerTagCounter.Global.IsInUse(stale.Tag));
            Assert.DoesNotContain(stale.Tag,completed);Assert.Contains(old.Tag,completed);
            Assert.Same(replacement,list.GetCurrentAction());Assert.False(replacement.DestructionObserved);
        }
        finally{list.Clear();}
    }
    [Fact]
    public void DuplicateDiagnosticReentryKeepsAlreadyQueuedObjectAndWatcher()
    {
        bool current=true;var list=new ActionList(sourceLog:(level,channel,key,text)=>
        {if(key=="ActionList.QueueAction.IsDuplicate")current=false;});
        var already=new Runner();
        try
        {
            Assert.Equal(0,list.QueueAction(QueueActionPosition.AtEnd,already));
            var watcher=already.Watcher;Assert.NotNull(watcher);
            Assert.Equal(2,list.QueueActionGuardedNow(already,0,()=>current));
            Assert.Same(already,list.GetCurrentAction());Assert.Same(watcher,already.Watcher);
            Assert.False(already.DestructionObserved);Assert.True(ActionRunnerTagCounter.Global.IsInUse(already.Tag));
            Assert.Equal(2,list.QueueActionGuardedNow(already,0,()=>false));
            Assert.Same(watcher,already.Watcher);Assert.Same(already,list.GetCurrentAction());
        }
        finally{list.Clear();}
    }}
