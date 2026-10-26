using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class ActionBehaviorDiagnosticTests
{
    private sealed class Probe(Func<CancellationToken,Task<int>> initial) : ActionBehavior("R1Probe","R1Probe")
    {
        internal readonly ConcurrentQueue<string> Events=new();
        internal readonly ConcurrentQueue<int> Results=new();
        protected override void OnStart()=>Begin("initial action",initial);
        internal void Begin(string name,Func<CancellationToken,Task<int>> action)
            =>RunAction(name,action,r=>{Events.Enqueue("callback:"+r);Results.Enqueue(r);},-77);
    }
    private static BehaviorContext Context(M4ControlTests.Rig rig)=>new(){Robot=rig.Robot,Triggers=new AnimationTriggerMap()};
    private static TaskCompletionSource<T> Source<T>()=>new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static Task Complete(Task work)=>work.WaitAsync(TimeSpan.FromSeconds(10));
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<int> ThrowAfterAwait(Task gate)
    {await gate;throw new InvalidOperationException("R1 async original failure");}
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Exception CapturedFailure(string message)
    {try{throw new ArgumentException(message);}catch(Exception error){return error;}}

    [Fact]
    public async Task AsyncFaultRetainsActionTypeMessageAndRealStackBeforeTickFallback()
    {
        // reach: unit (minimal behavior/action probe; actual RunAction Task.Run/continuation/Post/Update).
        using var rig=new M4ControlTests.Rig();using var scope=new BehaviorScope();var gate=Source<bool>();
        var probe=new Probe(_=>ThrowAfterAwait(gate.Task));probe.Step+=probe.Events.Enqueue;var context=Context(rig);
        await probe.StartAsync(context,scope,default);var work=probe.AsyncWorkCompletion;
        try
        {
            gate.TrySetResult(true);await Complete(work);
            var diagnostic=Assert.Single(probe.Trace.Where(s=>s.Contains("faulted:")));
            Assert.Contains("initial action",diagnostic);Assert.Contains("System.InvalidOperationException",diagnostic);
            Assert.Contains("R1 async original failure",diagnostic);Assert.Contains(nameof(ThrowAfterAwait),diagnostic);
            Assert.Empty(probe.Results);Assert.True(probe.HasPostedWork);
            probe.Update(context,0);Assert.Equal(-77,Assert.Single(probe.Results));
            var events=probe.Events.ToArray();Assert.True(Array.FindIndex(events,s=>s.Contains("faulted:"))<Array.FindIndex(events,s=>s=="callback:-77"));
        }
        finally{gate.TrySetResult(true);await Complete(work);probe.Stop(BehaviorStopReason.Completed);}
    }

    [Fact]
    public async Task MultipleFaultsRetainEveryAggregateAndNestedInnerDetail()
    {
        // reach: unit (controlled multi-fault Task input; actual completion continuation and tick callback).
        using var rig=new M4ControlTests.Rig();using var scope=new BehaviorScope();var source=Source<int>();
        var probe=new Probe(_=>source.Task);probe.Step+=probe.Events.Enqueue;var context=Context(rig);
        await probe.StartAsync(context,scope,default);var work=probe.AsyncWorkCompletion;
        try
        {
            var first=CapturedFailure("first independent failure");var nested=CapturedFailure("nested independent failure");
            source.TrySetException(new Exception[]{first,new AggregateException("outer aggregate",new InvalidOperationException("middle inner",nested))});
            await Complete(work);
            string diagnostic=Assert.Single(probe.Trace.Where(s=>s.Contains("faulted:")));
            foreach(string text in new[]{"first independent failure","outer aggregate","middle inner","nested independent failure","System.ArgumentException","System.InvalidOperationException",nameof(CapturedFailure)})
                Assert.Contains(text,diagnostic);
            Assert.Empty(probe.Results);probe.Update(context,0);Assert.Equal(-77,Assert.Single(probe.Results));
        }
        finally{source.TrySetResult(0);await Complete(work);probe.Stop(BehaviorStopReason.Completed);}
    }

    [Fact]
    public async Task CancelledTaskLogsActualCancellationWithLabelledObservationStack()
    {
        // reach: unit (controlled cancelled Task; actual completion observation/Post/Update, no invented origin).
        using var rig=new M4ControlTests.Rig();using var scope=new BehaviorScope();var source=Source<int>();
        var probe=new Probe(_=>source.Task);probe.Step+=probe.Events.Enqueue;var context=Context(rig);
        await probe.StartAsync(context,scope,default);var work=probe.AsyncWorkCompletion;
        try
        {
            source.TrySetCanceled();await Complete(work);
            var diagnostic=Assert.Single(probe.Trace.Where(s=>s.Contains("cancelled (")));
            Assert.Contains("initial action",diagnostic);Assert.Contains("System.Threading.Tasks.TaskCanceledException",diagnostic);
            Assert.Contains("observation stack",diagnostic);Assert.Contains("original cancellation throw site may be unavailable",diagnostic);
            Assert.Contains("ActionBehavior",diagnostic);Assert.Empty(probe.Results);
            probe.Update(context,0);Assert.Equal(-77,Assert.Single(probe.Results));
        }
        finally{source.TrySetResult(0);await Complete(work);probe.Stop(BehaviorStopReason.Completed);}
    }

    [Fact]
    public async Task SuccessRetainsResultAndTickBoundaryWithoutFailureDiagnostic()
    {
        // reach: unit (controlled successful action Task; actual continuation/Post/Update).
        using var rig=new M4ControlTests.Rig();using var scope=new BehaviorScope();var source=Source<int>();
        var probe=new Probe(_=>source.Task);probe.Step+=probe.Events.Enqueue;var context=Context(rig);
        await probe.StartAsync(context,scope,default);var work=probe.AsyncWorkCompletion;
        try
        {
            source.TrySetResult(42);await Complete(work);Assert.Empty(probe.Results);
            Assert.DoesNotContain(probe.Trace,s=>s.Contains("faulted:")||s.Contains("cancelled ("));
            probe.Update(context,0);Assert.Equal(42,Assert.Single(probe.Results));
        }
        finally{source.TrySetResult(0);await Complete(work);probe.Stop(BehaviorStopReason.Completed);}
    }

    [Fact]
    public async Task LateFaultAfterStopRemainsVisibleWithoutRunningStoppedCallback()
    {
        // reach: unit (action ignores cancellation to expose late fault; actual Stop/continuation/Post/Update).
        using var rig=new M4ControlTests.Rig();using var scope=new BehaviorScope();var source=Source<int>();
        var probe=new Probe(_=>source.Task);probe.Step+=probe.Events.Enqueue;var context=Context(rig);
        await probe.StartAsync(context,scope,default);var work=probe.AsyncWorkCompletion;
        try
        {
            probe.Stop(BehaviorStopReason.Interrupted);source.TrySetException(CapturedFailure("late stopped failure"));await Complete(work);
            Assert.Contains(probe.Trace,s=>s.Contains("initial action faulted:")&&s.Contains("late stopped failure"));
            probe.Update(context,0);Assert.Empty(probe.Results);
        }
        finally{source.TrySetResult(0);await Complete(work);probe.Stop(BehaviorStopReason.Completed);}
    }

    [Fact]
    public async Task ReplacementSuppressesStaleCallbackButKeepsItsLateDiagnostic()
    {
        // reach: unit (two controlled actions ignore cancellation; actual replacement/stale guard on tick).
        using var rig=new M4ControlTests.Rig();using var scope=new BehaviorScope();var old=Source<int>();var next=Source<int>();
        var probe=new Probe(_=>old.Task);probe.Step+=probe.Events.Enqueue;var context=Context(rig);
        await probe.StartAsync(context,scope,default);var oldWork=probe.AsyncWorkCompletion;Task nextWork=Task.CompletedTask;
        try
        {
            probe.Begin("replacement action",_=>next.Task);nextWork=probe.AsyncWorkCompletion;
            old.TrySetException(CapturedFailure("late replaced failure"));await Complete(oldWork);
            Assert.Contains(probe.Trace,s=>s.Contains("initial action faulted:")&&s.Contains("late replaced failure"));
            probe.Update(context,0);Assert.Empty(probe.Results);
            next.TrySetResult(9);await Complete(nextWork);Assert.Empty(probe.Results);
            probe.Update(context,1);Assert.Equal(9,Assert.Single(probe.Results));
        }
        finally{old.TrySetResult(0);next.TrySetResult(0);await Complete(oldWork);await Complete(nextWork);probe.Stop(BehaviorStopReason.Completed);}
    }
}
