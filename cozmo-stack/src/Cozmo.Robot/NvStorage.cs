using Cozmo.Protocol;

namespace Cozmo.Robot;

// fidelity: M1-041, M3-022
/// <summary>
/// The robot-level NV storage owner (<c>NVStorageComponent</c>). One component serves every reader, so a read
/// is a queued request rather than an independent subscription:
/// <list type="bullet">
/// <item>requests are FIFO and exactly one is in flight (M1 CD20: the on-idle callbacks run only when the
/// request deque is empty and nothing is in flight);</item>
/// <item>each queued request owns its callback;</item>
/// <item>a reply's <c>NVOpResult.Length</c> is an <b>index</b>, not a byte count; the entry's data is taken from
/// index 0 regardless of arrival order (the CONTROL capture arrived 5,6,7,0,3,2,1,4,15, and index 0 held the
/// 56-byte calibration). Multi-blob placement at index * <see cref="BlobStride"/> is unresolved;</item>
/// <item>MORE (3) and SCHEDULED (1) keep the request in flight; OKAY (0) completes it with the assembled
/// bytes; any other result completes it with that result and no data;</item>
/// <item>a disconnect discards the queue and the in-flight request without invoking any read callback.</item>
/// </list>
///
/// <b>Unresolved</b> (not guessed here): the full SCHEDULED / NO_DO semantics, duplicate or invalid index
/// handling, mismatched tag/op handling, whether a terminal non-OK result retains data, and the callbacks of
/// the startup Lab/Needs/backup reads. The camera calibration is a single-blob entry at index 0, which is what
/// <see cref="EntryBytes"/> returns; multi-blob assembly is left to a later pass.
/// </summary>
public sealed class NvStorageComponent : IDisposable
{
    /// <summary>The engine's blob stride: a reply's index is in units of this many bytes. This stack keys replies by index (see the class summary).</summary>
    public const int BlobStride = 1024;

    /// <summary>NVOperation (Unity <c>NVOperation</c>): READ, WRITE, ERASE, WIPEALL.</summary>
    public const byte OpRead = 0, OpWrite = 1, OpErase = 2, OpWipeAll = 3;

    /// <summary>NVResult (Unity <c>NVResult</c>): OKAY, SCHEDULED, NO_DO, MORE, NOT_FOUND.</summary>
    public const sbyte ResultOkay = 0, ResultScheduled = 1, ResultNoDo = 2, ResultMore = 3, ResultNotFound = -1;

    private sealed class PendingRequest
    {
        public required uint Tag { get; init; }
        public required int Length { get; init; }
        public required byte Op { get; init; }
        public byte[] Data { get; init; } = Array.Empty<byte>();
        public Action<NvResult>? Callback { get; init; }
        /// <summary>Replies keyed by their index (<c>NVOpResult.Length</c>); the entry's data is taken from index 0.</summary>
        public readonly SortedDictionary<int, byte[]> Blobs = new();
    }

    private readonly CozmoRobot _robot;
    private readonly object _gate = new();
    private readonly Queue<PendingRequest> _queue = new();
    private PendingRequest? _inFlight;
    private readonly List<Action> _onIdle = new();
    private readonly List<string> _log = new();

    internal NvStorageComponent(CozmoRobot robot)
    {
        _robot = robot;
        robot.Message += OnMessage;
    }

    /// <summary>The read request lines, for the conformance log.</summary>
    public IReadOnlyList<string> Log { get { lock (_gate) return _log.ToArray(); } }

    /// <summary>True when nothing is queued and nothing is in flight (M1 CD20's idle condition).</summary>
    public bool IsIdle { get { lock (_gate) return _inFlight is null && _queue.Count == 0; } }

    /// <summary>Queues a READ and delivers the terminal result to <paramref name="callback"/>.</summary>
    public void Read(uint tag, int length, Action<NvResult> callback) => Enqueue(new PendingRequest { Tag = tag, Length = length, Op = OpRead, Callback = callback });

    /// <summary>Queues any NV operation; the callback owns the terminal result.</summary>
    public void Request(uint tag, int length, byte op, byte[] data, Action<NvResult> callback) =>
        Enqueue(new PendingRequest { Tag = tag, Length = length, Op = op, Data = data, Callback = callback });

    /// <summary>Queues a READ and waits for its terminal result.</summary>
    public async Task<NvResult> ReadAsync(uint tag, int length, TimeSpan? timeout = null)
    {
        var tcs = new TaskCompletionSource<NvResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        Read(tag, length, r => tcs.TrySetResult(r));
        var t = timeout ?? TimeSpan.FromSeconds(3);
        var done = await Task.WhenAny(tcs.Task, Task.Delay(t)).ConfigureAwait(false);
        if (done == tcs.Task) return await tcs.Task.ConfigureAwait(false);
        lock (_gate) _log.Add($"no terminal NVOpResult for tag 0x{tag:X8} within {t.TotalSeconds:F1}s");
        return new NvResult(ResultNoDo, Array.Empty<byte>());
    }

    private void Enqueue(PendingRequest r)
    {
        lock (_gate) { _queue.Enqueue(r); if (_inFlight is null) StartNextLocked(); }
    }

    private void StartNextLocked()
    {
        if (_queue.Count == 0) { _inFlight = null; RunOnIdleLocked(); return; }
        _inFlight = _queue.Dequeue();
        _log.Add($"NV request tag=0x{_inFlight.Tag:X8} op={_inFlight.Op} length={_inFlight.Length} data={_inFlight.Data.Length}B");
        _robot.SendMessage(new NVCommand
        {
            Tag = _inFlight.Tag, Length = _inFlight.Length, Op = _inFlight.Op, Unknown = 0, Data = _inFlight.Data,
        }, flush: true);
    }

    /// <summary>
    /// Adds a one-shot on-idle callback (M1 CD20, CB22). It is appended and runs on the next
    /// <see cref="ProcessOnIdle"/> with the queue empty and nothing in flight; it never runs at the moment it is
    /// added, so a read queued later in the same connection broadcast is waited for.
    /// </summary>
    public void OnIdle(Action callback) { lock (_gate) _onIdle.Add(callback); }

    /// <summary>
    /// ProcessOnIdleCallbacks (CD20, 0x00645B10..0x00645B26): runs the pending callbacks only when the queue is
    /// empty and nothing is in flight. Called from Robot::Update's NVStorage step (CD12) and when a request
    /// completes.
    /// </summary>
    public void ProcessOnIdle()
    {
        Action[] run;
        lock (_gate)
        {
            if (_inFlight is not null || _queue.Count != 0 || _onIdle.Count == 0) return;
            run = _onIdle.ToArray();
            _onIdle.Clear();
        }
        foreach (var a in run) a();
    }

    private void RunOnIdleLocked() => ProcessOnIdle();

    /// <summary>
    /// A disconnect discards the queue and the in-flight request without invoking any read callback, and drops
    /// the pending on-idle callbacks (the robot they belonged to is gone).
    /// </summary>
    public void OnDisconnected()
    {
        lock (_gate) { _queue.Clear(); _inFlight = null; _onIdle.Clear(); }
    }

    private void OnMessage(RobotMessage m) { if (m is NVOpResult r) OnResult(r); }

    private void OnResult(NVOpResult r)
    {
        PendingRequest? req;
        bool complete = false;
        sbyte result;
        byte[] data = Array.Empty<byte>();
        lock (_gate)
        {
            req = _inFlight;
            if (req is null || req.Tag != r.Tag) return;      // no request owns this tag
            result = r.Result;
            if (r.Data.Length > 0) req.Blobs[r.Length] = r.Data;   // keyed by index (Length is an index, not a byte count)
            _log.Add($"NVOpResult tag=0x{r.Tag:X8} op={r.Op} result={r.Result} index={r.Length} data={r.Data.Length}B");
            switch (r.Result)
            {
                case ResultMore:
                case ResultScheduled:
                    return;                                    // still in flight
                case ResultOkay:
                    data = EntryBytes(req);
                    complete = true;
                    break;
                default:
                    complete = true;                           // terminal error, no data
                    break;
            }
            if (complete) StartNextLocked();
        }
        if (complete) req.Callback?.Invoke(new NvResult(result, data));
    }

    /// <summary>
    /// The completed entry's bytes. The camera calibration is a single-blob entry at index 0 (the CONTROL
    /// capture's index 0 held the valid 56-byte calibration); multi-blob assembly is unresolved and is not
    /// guessed here.
    /// </summary>
    private static byte[] EntryBytes(PendingRequest req) =>
        req.Blobs.TryGetValue(0, out var b0) ? b0 : Array.Empty<byte>();

    public void Dispose() { _robot.Message -= OnMessage; }
}

/// <summary>The terminal result of one NV request: the NVResult and the assembled bytes (empty on an error).</summary>
public readonly record struct NvResult(sbyte Result, byte[] Data);
