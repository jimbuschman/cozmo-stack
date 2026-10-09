using System.Collections.Concurrent;
using Cozmo.Robot.Behavior;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// Executes captured async continuations on the fixture thread. Tests wait for a posted continuation,
/// rather than allowing an arbitrary sleep to decide whether the worker has caught up.
/// The watchdog diagnoses a lost signal; it never selects a behavioral result.
/// </summary>
internal sealed class SignalTestContext : SynchronizationContext, IDisposable
{
    private readonly SynchronizationContext? _previous;
    private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State, bool Continuation)> _work = new();
    private readonly AutoResetEvent _posted = new(false);
    private int _notificationQueued;
    // Shared across helper calls: a queued command still needs firmware even when another
    // behavior helper already drained the continuation that queued it. Only Pump clears this.
    private int _modelWorkPending;
    internal bool ModelWorkPending => Volatile.Read(ref _modelWorkPending) != 0;
    internal void MarkFirmwareWorkPending()
    {
        Volatile.Write(ref _modelWorkPending, 1);
        Notify();
    }
    internal void BeginFirmwarePump() => Volatile.Write(ref _modelWorkPending, 0);

    private SignalTestContext()
    {
        _previous = Current;
        SetSynchronizationContext(this);
    }

    public static SignalTestContext Install() => new();

    public static Task Schedule(Func<Task> action)
    {
        if (Current is not SignalTestContext context)
            throw new InvalidOperationException("Install the signal context before scheduling an async action.");
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        // Keep Task.Run's asynchronous start boundary, while executing the action and its awaits
        // on the fixture's captured context instead of racing a ThreadPool worker.
        context.Post(async _ =>
        {
            try { await action(); completion.TrySetResult(); }
            catch (OperationCanceledException error) { completion.TrySetCanceled(error.CancellationToken); }
            catch (Exception error) { completion.TrySetException(error); }
        }, null);
        return completion.Task;
    }

    public override void Post(SendOrPostCallback callback, object? state)
    {
        _work.Enqueue((callback, state, true));
        _posted.Set();
    }

    public void Notify()
    {
        if (Interlocked.CompareExchange(ref _notificationQueued, 1, 0) != 0) return;
        _work.Enqueue((_ => Volatile.Write(ref _notificationQueued, 0), null, false));
        _posted.Set();
    }

    public static int Drain()
    {
        if (Current is not SignalTestContext context) return 0;
        int count = 0;
        // Drain one snapshot. Newly posted work belongs to the next modeled engine/firmware tick.
        int available = context._work.Count;
        while (available-- > 0 && context._work.TryDequeue(out var work))
        {
            work.Callback(work.State);
            if (work.Continuation) count++;
        }
        return count;
    }

    public static void Step()
    {
        if (Current is not SignalTestContext context)
            throw new InvalidOperationException("Install the signal context before starting the async operation.");
        (SendOrPostCallback Callback, object? State, bool Continuation) work;
        while (!context._work.TryDequeue(out work))
        {
            if (!context._posted.WaitOne(TimeSpan.FromMinutes(2)))
                throw new TimeoutException("No async continuation was posted; the fixture lost its completion signal.");
        }
        work.Callback(work.State);
    }

    public static void StepContinuation()
    {
        if (Current is not SignalTestContext context)
            throw new InvalidOperationException("Install the signal context before awaiting an async continuation.");
        while (true)
        {
            (SendOrPostCallback Callback, object? State, bool Continuation) work;
            while (!context._work.TryDequeue(out work))
            {
                if (!context._posted.WaitOne(TimeSpan.FromMinutes(2)))
                    throw new TimeoutException("No async continuation was posted; the fixture lost its completion signal.");
            }
            work.Callback(work.State);
            if (work.Continuation) return;
        }
    }

    public static void Until(Func<bool> done, Action? pump = null)
    {
        while (!done())
        {
            pump?.Invoke();
            int continuations = Drain();
            // A continuation may queue the next firmware command. Give that command its modeled
            // transport/engine tick before awaiting another continuation from its response.
            bool firmwareNeedsPump = pump is not null && Current is SignalTestContext context
                && context.ModelWorkPending;
            if (!done() && continuations == 0 && !firmwareNeedsPump) Step();
        }
    }

    public static void Run(Task task, Action? pump = null)
    {
        if (Current is SignalTestContext context)
            _ = task.ContinueWith(_ => context.Notify(), CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        Until(() => task.IsCompleted, pump);
        task.GetAwaiter().GetResult();
    }

    public static T Result<T>(Task<T> task, Action? pump = null)
    {
        Run(task, pump);
        return task.GetAwaiter().GetResult();
    }

    public static void AdvanceBehavior(SteppedBehavior behavior)
    {
        int continuations = Drain();
        // The streamer is advanced by the manual fixture clock. Once its ticket ends, wait for
        // the existing default-scheduler continuation to queue the behavior callback before
        // advancing model time again. Waiting earlier would prevent the streamer from ticking.
        if (behavior.AwaitingCompletedAnimationCallback) Run(behavior.AnimationCallbackCompletion);
        // An async child can await an animation ticket too. Its manual streamer must keep ticking.
        if (continuations == 0 && behavior.AwaitingAsyncCompletion && !behavior.HasPostedWork
            && !behavior.ManualAnimationInProgress
            && !(Current is SignalTestContext pendingContext && pendingContext.ModelWorkPending))
        {
            if (Current is SignalTestContext context)
                _ = behavior.AsyncWorkCompletion.ContinueWith(_ => context.Notify(), CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            Step();
        }
    }

    public void Dispose()
    {
        SetSynchronizationContext(_previous);
        // A canceled production timer may still post after fixture disposal. Keep its signal alive;
        // no callback is executed after the test's scope ends.
    }
}
