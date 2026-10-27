using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Cozmo.Conformance;
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
    private sealed class JournalOutput : System.IO.TextWriter
    {
        internal readonly ConcurrentQueue<string> Lines=new();
        private int _active;
        internal int MaxActive;
        internal readonly TaskCompletionSource<bool> DiagnosticEntered=Source<bool>();
        private readonly TaskCompletionSource<bool> _release=Source<bool>();
        internal void Release()=>_release.TrySetResult(true);
        public override System.Text.Encoding Encoding=>System.Text.Encoding.UTF8;
        public override void WriteLine(string? value)
        {
            int active=Interlocked.Increment(ref _active);
            int prior;
            do {prior=Volatile.Read(ref MaxActive);if(prior>=active)break;}
            while(Interlocked.CompareExchange(ref MaxActive,active,prior)!=prior);
            try
            {
                if(value is not null && (value.Contains("faulted:")||value.Contains("cancelled (")))
                {DiagnosticEntered.TrySetResult(true);_release.Task.GetAwaiter().GetResult();}
                Thread.Yield();Lines.Enqueue(value??"");
            }
            finally{Interlocked.Decrement(ref _active);}
        }
    }
    private sealed record ReactionLine(double t,string text);

    [Theory] [InlineData(false,false)] [InlineData(false,true)] [InlineData(true,false)] [InlineData(true,true)]
    public async Task SharedToolJournalRetainsWorkerDiagnosticsWithConcurrentOrdinaryWritesAndSnapshots(bool cancelled,bool reactionShape)
    {
        // Host sink regression: the same shared journal used by ManipTool.Say and Reactions.Line.
        // Actual RunAction completion/Step/Post/Update; no hardware, callback lock or continuation substitution.
        using var rig=new M4ControlTests.Rig();using var scope=new BehaviorScope();using var output=new JournalOutput();
        var strings=new DiagnosticJournal<string>(output);var objects=new DiagnosticJournal<object>(output);
        void Line(string text)
        {
            if(reactionShape)objects.Write(new ReactionLine(1.25,text),$"[{1.25,7:F2}s] {text}");
            else strings.Write(text,text);
        }
        string[] Snapshot()=>reactionShape?objects.Snapshot().Cast<ReactionLine>().Select(x=>x.text).ToArray():strings.Snapshot();
        var action=Source<int>();var go=Source<bool>();var diagnosticSeen=Source<bool>();
        var probe=new Probe(_=>action.Task);var context=Context(rig);
        void Step(string text){Line(text);if(text.Contains(cancelled?"cancelled (":"faulted:"))diagnosticSeen.TrySetResult(true);}
        probe.Step+=Step;
        await probe.StartAsync(context,scope,default);var work=probe.AsyncWorkCompletion;
        var snapshots=new ConcurrentQueue<string[]>();var writersReady=Source<bool>();int writersStarted=0;
        // Dedicated synchronous workers keep held sink writes independent of thread-pool injection.
        var producers=Enumerable.Range(0,4).Select(producer=>Task.Factory.StartNew(()=>
        {
            go.Task.GetAwaiter().GetResult();output.DiagnosticEntered.Task.GetAwaiter().GetResult();
            if(Interlocked.Increment(ref writersStarted)==4)writersReady.TrySetResult(true);
            for(int i=0;i<500;i++){Line($"ordinary:{producer}:{i}");snapshots.Enqueue(Snapshot());}
        },CancellationToken.None,TaskCreationOptions.LongRunning,TaskScheduler.Default)).ToArray();
        var completion=Task.Factory.StartNew(()=>
        {
            go.Task.GetAwaiter().GetResult();
            if(cancelled)action.TrySetCanceled();
            else action.TrySetException(new Exception[]{CapturedFailure("journal first failure"),new AggregateException("journal aggregate",CapturedFailure("journal nested failure"))});
        },CancellationToken.None,TaskCreationOptions.LongRunning,TaskScheduler.Default);
        try
        {
            go.TrySetResult(true);
            await Complete(output.DiagnosticEntered.Task);await Complete(writersReady.Task);
            output.Release(); // Worker Step has entered the sink while ordinary producer writes are in flight.
            await Complete(Task.WhenAll(producers.Append(completion)));await Complete(work);await Complete(diagnosticSeen.Task);
            Assert.Empty(probe.Results);Assert.True(probe.HasPostedWork);
            string[] beforeTick=Snapshot();
            string diagnostic=Assert.Single(beforeTick.Where(x=>x.Contains(cancelled?"cancelled (":"faulted:")));
            if(cancelled)
            {Assert.Contains("TaskCanceledException",diagnostic);Assert.Contains("observation stack",diagnostic);Assert.Contains("ActionBehavior",diagnostic);}
            else
            {foreach(string text in new[]{"journal first failure","journal aggregate","journal nested failure",nameof(CapturedFailure),"System.ArgumentException"})Assert.Contains(text,diagnostic);}
            Assert.DoesNotContain(beforeTick,x=>x=="initial action -> -77");
            probe.Update(context,0);Assert.Equal(-77,Assert.Single(probe.Results));
            var final=Snapshot();
            Assert.True(Array.IndexOf(final,diagnostic)<Array.IndexOf(final,"initial action -> -77"));
            for(int p=0;p<4;p++)for(int i=0;i<500;i++)Assert.Equal(1,final.Count(x=>x==$"ordinary:{p}:{i}"));
            Assert.Equal(2003,final.Length); // start + diagnostic + tick result, plus every ordinary producer entry
            foreach(var snapshot in snapshots)Assert.Equal(final.Take(snapshot.Length),snapshot);
            Assert.Equal(1,output.MaxActive);
            Assert.Equal(final.Select(x=>reactionShape?$"[{1.25,7:F2}s] {x}":x),output.Lines);
            // A previously returned acceptance array remains independent of subsequent appends.
            Assert.Equal(2002,beforeTick.Length);
        }
        finally
        {
            go.TrySetResult(true);output.DiagnosticEntered.TrySetResult(true);output.Release();action.TrySetResult(0);
            await Complete(Task.WhenAll(producers.Append(completion)));await Complete(work);
            probe.Stop(BehaviorStopReason.Completed);probe.Step-=Step;
        }
    }

}
