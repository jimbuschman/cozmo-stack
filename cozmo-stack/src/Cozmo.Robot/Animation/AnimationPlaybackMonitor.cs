using Cozmo.Protocol;

namespace Cozmo.Robot.Animation;

/// <summary>Raw firmware diagnostic. Args remain uninterpreted.</summary>
public sealed record AnimationBufferFullTrace(uint FormatWord, ushort NameId, sbyte Level, IReadOnlyList<int> Args, long ObservedAt);
/// <summary>Host evidence only. Does not rewrite the native action result.</summary>
public sealed record AnimationPlaybackIssue(long InitializedAt, byte? Tag, string? Name,
    bool EndedBeforeEndSent, AnimationBufferFullTrace[] Traces)
{
    public bool TruncatedByRobot => EndedBeforeEndSent || Traces.Length != 0;
}

/// <summary>Read-only host monitor shared by bridge and narration. Selection is not
/// playback; robot CA/CB and successful outgoing End are independent facts.</summary>
// fidelity: M1-063
public sealed class AnimationPlaybackMonitor : IDisposable
{
    private sealed class Playback(long initializedAt, byte? tag, string? name)
    {
        internal readonly long InitializedAt = initializedAt;
        internal readonly byte? Tag = tag;
        internal readonly string? Name = name;
        internal bool Started, EndSent, HostAborted, EarlyEnd;
        internal readonly List<AnimationBufferFullTrace> Traces = new();
        internal bool HasIssue => EarlyEnd || Traces.Count != 0;
        internal AnimationPlaybackIssue Snapshot() => new(InitializedAt, Tag, Name, EarlyEnd, Traces.ToArray());
    }
    private readonly object gate = new();
    private readonly Queue<Playback> history = new();
    private readonly Dictionary<byte, Playback> selectedTags = new(), activeTags = new();
    private Playback? selected, lastStarted;
    private readonly CozmoRobot? robot;
    private bool disposed;
    public event Action<AnimationPlaybackIssue>? Issue;

    public AnimationPlaybackMonitor(CozmoRobot? robot = null)
    {
        this.robot = robot;
        if (robot is null) return;
        robot.Animations.Scheduler.Diagnostic += ObserveDiagnostic;
        robot.Animations.Scheduler.AnimationAborted += ObserveHostAbort;
        // Synchronous host ingress follows native per-tag delivery but precedes the
        // action-list update/completion. Public Message is deferred and can be too late.
        robot.MessageInternal += ObserveMessage;
        robot.Engine.Handler.EndOfAnimationSent += ObserveSuccessfulEnd;
    }

    public void ObserveDiagnostic(AnimationDiagnostic diagnostic)
    {
        lock (gate)
        {
            if (disposed) return;
            if (diagnostic is AnimationClipInitializedObservation init)
            {
                // InitStream identity at 0x0057B674. A new loop/reused tag is a new instance.
                selected = new(init.Timestamp, init.Tag, init.Name);
                selectedTags[init.Tag] = selected;
                Remember(selected);
            }
            else if (diagnostic is SendGateObservation { MessageId: 0x9A, Sent: true })
            {
                // SendEndOfAnimation 0x0057C464..0x0057C496: count only a successful send.
                if (selected is not null) selected.EndSent = true;
            }
        }
    }

    private void Remember(Playback playback)
    {
        if (history.Count == 1024) history.Dequeue();
        history.Enqueue(playback);
    }

    private void ObserveSuccessfulEnd()
    {
        lock (gate)
            if (!disposed && selected is not null) selected.EndSent = true;
    }

    public void ObserveHostAbort(byte tag)
    {
        lock (gate)
        {
            if (disposed) return;
            if (activeTags.TryGetValue(tag, out var active)) active.HostAborted = true;
            if (selectedTags.TryGetValue(tag, out var pending)) pending.HostAborted = true;
        }
    }

    public void ObserveMessage(RobotMessage message)
    {
        AnimationPlaybackIssue? issue = null;
        lock (gate)
        {
            if (disposed) return;
            if (message is AnimationStarted start)
            {
                if (selectedTags.TryGetValue(start.AnimId, out var playback))
                {
                    playback.Started = true;
                    activeTags[start.AnimId] = lastStarted = playback;
                }
            }
            else if (message is AnimationEnded end)
            {
                // A legitimate host abort can also produce CB without End. Do not
                // label that cancellation as a robot buffer cutoff.
                if (activeTags.TryGetValue(end.AnimId, out var playback) &&
                    playback.Started && !playback.EndSent && !playback.HostAborted)
                {
                    playback.EarlyEnd = true;
                    issue = playback.Snapshot();
                }
            }
            else if (message is PrintTrace { NameId: 379 } trace)
            {
                // Shipped config/engine/AnkiLogStringTables.json nameTable[379]:
                // AnimationController.BufferKeyFrame.BufferFull. No argument semantics inferred.
                var playback = ReferenceEquals(lastStarted, selected) ? lastStarted : null;
                if (playback is null)
                {
                    playback = new(System.Diagnostics.Stopwatch.GetTimestamp(), null, null);
                    Remember(playback);
                }
                playback.Traces.Add(new(trace.FormatId, trace.NameId, trace.Level, Array.AsReadOnly((int[])trace.Args.Clone()), System.Diagnostics.Stopwatch.GetTimestamp()));
                issue = playback.Snapshot();
            }
        }
        if (issue is null) return;
        // Publish after the already-enqueued CA/CB host report. The internal
        // model is immediately queryable by an action result racing app delivery.
        if (robot is not null) robot.Engine.QueueAppEvent(Issue, issue);
        else Issue?.Invoke(issue);
    }

    /// <summary>Owned early ends are matched by initialization time/tag/name.
    /// Any raw BufferFull observed during the call is also included; uncertain tag
    /// association stays null rather than borrowing a different stream's identity.</summary>
    public AnimationPlaybackIssue[] IssuesSince(long initializedAt, byte? tag, string? name)
    {
        lock (gate)
        {
            var issues = new List<AnimationPlaybackIssue>();
            foreach (var p in history)
            {
                bool owned = p.InitializedAt >= initializedAt &&
                    (tag is null || p.Tag == tag) && (name is null || p.Name == name);
                var traces = p.Traces.Where(t => t.ObservedAt >= initializedAt).ToArray();
                if (owned && p.EarlyEnd || traces.Length != 0)
                    issues.Add(new(p.InitializedAt, p.Tag, p.Name, owned && p.EarlyEnd, traces));
            }
            return issues.ToArray();
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            history.Clear(); selectedTags.Clear(); activeTags.Clear();
            selected = lastStarted = null;
        }
        if (robot is null) return;
        robot.Animations.Scheduler.Diagnostic -= ObserveDiagnostic;
        robot.Animations.Scheduler.AnimationAborted -= ObserveHostAbort;
        robot.MessageInternal -= ObserveMessage;
        robot.Engine.Handler.EndOfAnimationSent -= ObserveSuccessfulEnd;
    }
}
