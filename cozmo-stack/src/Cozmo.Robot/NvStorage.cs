using Cozmo.Protocol;

namespace Cozmo.Robot;

// fidelity: M1-041, M3-022, M3-025, M3-026, M3-027, M3-028, M3-029, M3-030, M3-031, M3-035
/// <summary>
/// The robot-level NV storage owner (<c>NVStorageComponent</c>). One component serves every reader, so a read
/// is a queued request rather than an independent subscription:
/// <list type="bullet">
/// <item>requests are FIFO and exactly one is in flight (M3-026; M1 CD20: the on-idle callbacks run only when the
/// request deque is empty and nothing is in flight);</item>
/// <item>a READ validates its tag first: an invalid tag is not sent, and the callback gets
/// <c>(-6, empty)</c> (M3-026);</item>
/// <item>the component computes the request's <c>Length</c> from the tag: the factory size table's value, else
/// 0x400 (M3-027);</item>
/// <item>the engine treats a reply's <c>NVOpResult.Length</c> as a <b>blob index</b>, not a byte count, and places
/// each blob at <c>index * 1024 - hdr</c> where <c>hdr</c> is 16 for index &gt; 0 on a non-factory base (M3-029).
/// A factory tag (&lt; 0) skips the non-factory 16-byte header entirely (M3-028);</item>
/// <item>MORE (3) keeps the request in flight; OKAY (0) completes it with the assembled bytes; <c>NOT_FOUND</c>
/// (-1) and any other result complete it with that result (M3-030);</item>
/// <item>a negative result in {-8,-7,-5,-4} resends the identical command up to 8 times, then completes with
/// <c>ReadOpFailed</c> (M3-031); a 5 s robot-clock timeout delivers <c>-4</c> with no retry (<see cref="Update"/>,
/// M3-031);</item>
/// <item><b>terminal ordering (M3-022 / M1 CD20):</b> on a terminal result the request's own callback runs to
/// completion first (the calibration is installed and vision enabled there), and only then, with the queue empty
/// and nothing in flight, does an on-idle callback run (ready to stream);</item>
/// <item>a disconnect discards the queue and the in-flight request without invoking any read callback or timeout
/// (M3-035).</item>
/// </list>
/// </summary>
public sealed class NvStorageComponent : IDisposable
{
    /// <summary>The engine's blob stride: a reply's index is in units of this many bytes (M3-029).</summary>
    public const int BlobStride = 1024;

    /// <summary>NVOperation (Unity <c>NVOperation</c>): READ, WRITE, ERASE, WIPEALL.</summary>
    public const byte OpRead = 0, OpWrite = 1, OpErase = 2, OpWipeAll = 3;

    /// <summary>NVResult (Unity <c>NVResult</c>): OKAY, SCHEDULED, NO_DO, MORE, NOT_FOUND.</summary>
    public const sbyte ResultOkay = 0, ResultScheduled = 1, ResultNoDo = 2, ResultMore = 3, ResultNotFound = -1;

    /// <summary>M3-027: a non-factory READ's hard-coded request length (0x400), not the size table's value.</summary>
    public const int NonFactoryReadLength = 0x400;

    /// <summary>M3-027/M3-031: the timeout is 5000 robot-clock ticks after the request is armed, or after the last blob.</summary>
    public const uint ReadTimeoutTicks = 5000;

    /// <summary>
    /// M3-031: ResendLastCommand (0x645C6A..0x645D7A) increments +0xF4 (0-based, reset by the send/arm) and resends
    /// while <c>+0xF4 &lt; +0xF5 = 8</c>, so 7 resends (8 transmissions) then <c>ReadOpFailed</c>.
    /// </summary>
    public const int MaxReadResends = 7;

    /// <summary>M3-028: the non-factory 16-byte header, whose u32[0] is the magic "OMZC".</summary>
    public const int NvHeaderSize = 16;
    /// <summary>M3-028: the header magic <c>0x435A4D4F</c>.</summary>
    public const uint NonFactoryHeaderMagic = 0x435A4D4F;

    /// <summary>
    /// M3-025: the size table (<c>_maxSizeTable</c>, InitSizeTable 0x643B48..0x643CE0). Keys are the exact
    /// non-factory tags (plus the two factory-block keys 0xDE000/0xDE030). The value is the distance to the next
    /// key; 0x198000 is special-cased to 0x64000.
    /// </summary>
    private static readonly Dictionary<uint, int> MaxSizeTable = new()
    {
        [0x180000] = 0x1000, [0x181000] = 0x1000, [0x182000] = 0x1000, [0x183000] = 0x1000,
        [0x184000] = 0x10000, [0x194000] = 0x1000, [0x195000] = 0x1000, [0x196000] = 0x1000,
        [0x197000] = 0x1000, [0x198000] = 0x64000, [0xDE000] = 0x30, [0xDE030] = 0x1DFD0,
    };

    /// <summary>
    /// M3-025/M3-027: the factory size table (<c>_maxFactoryEntrySizeTable</c>, 23 keys, pass 4b Q1, .rodata
    /// 0xC81064). 15 keys hold 1 and 8 hold 0xFFFF; InitSizeTable computes the value by the rule in
    /// <see cref="MaxFactorySizeForEntryTag"/>.
    /// </summary>
    private static readonly Dictionary<uint, int> MaxFactoryEntrySizeTable = new()
    {
        [0x80000000] = 1, [0x80000001] = 1, [0x80000002] = 1, [0x80000003] = 1, [0x80000004] = 1,
        [0x80000005] = 1, [0x80000006] = 1, [0x80000007] = 1, [0x80000008] = 1, [0x80000010] = 1,
        [0x80000011] = 1, [0x80000012] = 1, [0xC0000000] = 1, [0xC0000001] = 1, [0xC0000004] = 1,
        [0x80010000] = 0xFFFF, [0x80020000] = 0xFFFF, [0x80030000] = 0xFFFF, [0x80040000] = 0xFFFF,
        [0x80050000] = 0xFFFF, [0x80060000] = 0xFFFF, [0x80100000] = 0xFFFF, [0x80110000] = 0xFFFF,
    };

    private sealed class PendingRequest
    {
        public required uint Tag { get; init; }
        /// <summary>The request length: computed for a READ (M3-027), or the caller's for the other ops.</summary>
        public int Length { get; set; }
        public required byte Op { get; init; }
        public byte[] Data { get; init; } = Array.Empty<byte>();
        public Action<NvResult>? Callback { get; init; }
        /// <summary>M3-030: the caller's vector sink; it is filled instead of nothing when the callback is empty.</summary>
        public List<byte>? Sink { get; init; }
        /// <summary>M3-030: when set, the completed buffer is re-chunked into 0x400 broadcasts.</summary>
        public bool Broadcast { get; init; }
        /// <summary>M3-028: the non-factory header has been accepted (+0x79).</summary>
        public bool HeaderAccepted { get; set; }
        /// <summary>M3-028: the header's total size (its u32@8).</summary>
        public int? HeaderTotal { get; set; }
        /// <summary>
        /// M3-028 (pass 4b Q3): the fits branch was taken (0x643846 bls 0x6438e2), so the reply vector was resized
        /// to total+16 and the index-0 copy is bounded at the header total. On the re-request path it is not.
        /// </summary>
        public bool HeaderFitsInFirstBlob { get; set; }
        /// <summary>M3-031: the retry counter (+0xF4).</summary>
        public int Retries { get; set; }
        /// <summary>M3-027/M3-029: robot+0x2C + 5000 (5000 before the first RobotState makes +0x2C 0).</summary>
        public uint? Deadline { get; set; }
        /// <summary>M3-031: the last command built, so a retry resends the identical bytes.</summary>
        public NVCommand? LastCommand { get; set; }

        private byte[] _buffer = Array.Empty<byte>();
        /// <summary>The reassembly buffer (zero-filled to its current size, M3-029).</summary>
        public byte[] Buffer => _buffer;

        public void Write(int offset, byte[] source, int sourceOffset, int count)
        {
            int size = offset + count;
            if (_buffer.Length < size) Array.Resize(ref _buffer, size);
            Array.Copy(source, sourceOffset, _buffer, offset, count);
        }

        /// <summary>M3-028 (0x643478): TooLittleReadData clears the pending buffer before forcing -3.</summary>
        public void Clear() => _buffer = Array.Empty<byte>();
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

    // fidelity: M3-025
    /// <summary>
    /// NVStorageComponent::IsValidEntryTag (M3-025; 0x644148..0x6441A0): non-factory tags must pass the coarse
    /// range test <c>((tag-0x180000) &gt;&gt; 14) &lt;= 0x1e</c>, not be the sentinel 0x198000, be a multiple of
    /// 0x1000 and be an exact <c>_maxSizeTable</c> key. Anything else falls back to the factory tag test and the
    /// factory block [0xDE000, 0xFC000).
    /// </summary>
    public static bool IsValidEntryTag(uint tag)
    {
        if ((tag - 0x180000u) >> 14 <= 0x1e && tag != 0x198000 && (tag & 0xFFFu) == 0 && MaxSizeTable.ContainsKey(tag))
            return true;
        return IsFactoryEntryTag(tag) || (tag >= 0xDE000u && tag < 0xFC000u);
    }

    // fidelity: M3-025
    /// <summary>M3-025: an exact key of the 23-key <c>_maxFactoryEntrySizeTable</c> (pass 4b Q1).</summary>
    public static bool IsFactoryEntryTag(uint tag) => MaxFactoryEntrySizeTable.ContainsKey(tag);

    // fidelity: M3-025
    /// <summary>
    /// GetMaxSizeForEntryTag (M3-025; 0x643FC8..0x64404E): the <c>_maxSizeTable</c> value for an exact key, but 0
    /// for the 0x198000 sentinel (the cited getter requires the key to equal the tag and to be != 0x198000, else it
    /// warns and returns 0), and 0 for an unrecognised tag. 0x198000's raw table value stays 0x64000; only this
    /// getter refuses it. A factory READ uses the factory value instead, never this.
    /// </summary>
    public static int MaxSizeForEntryTag(uint tag) =>
        tag == 0x198000 ? 0 : MaxSizeTable.TryGetValue(tag, out int value) ? value : 0;

    // fidelity: M3-025
    /// <summary>
    /// GetBaseEntryTag (M3-025; 0x6441F8..0x6443F4; pass 4 1c-1..1c-3). A tag whose top bit is set takes the
    /// factory path: an exact factory key is its own base; otherwise, when <c>(tag &amp; ~0xFFFF) == 0xC0000000</c>
    /// and <c>(tag &amp; 0x7FFF0000) != 0</c> and <c>tag &amp; 0xFFFF0000</c> is a factory key, the base is
    /// <c>tag &amp; 0xFFFF0000</c>; otherwise the sentinel 0x198000. A non-negative exact <c>_maxSizeTable</c> key
    /// is its own base; anything else is the sentinel. The tree descent's floor/tie-break for an unrecognised
    /// positive tag was not fully decoded (pass 4 open q2), but every such value returns the sentinel and is
    /// dropped by the reply-accept check, so no live path differs.
    /// </summary>
    public static uint GetBaseEntryTag(uint tag)
    {
        if ((tag & 0x80000000u) != 0)                                     // signed <= -1: the factory path
        {
            if (IsFactoryEntryTag(tag)) return tag;
            if ((tag & 0xFFFF0000u) == 0xC0000000u && (tag & 0x7FFF0000u) != 0
                && IsFactoryEntryTag(tag & 0xFFFF0000u))
                return tag & 0xFFFF0000u;
            return 0x198000u;
        }
        return MaxSizeTable.ContainsKey(tag) ? tag : 0x198000u;
    }

    // fidelity: M3-025, M3-027
    /// <summary>
    /// M3-027: a factory READ's request length, the <c>_maxFactoryEntrySizeTable</c> value. InitSizeTable
    /// (pass 1 step 6) computes 0xFFFF when <c>(tag &amp; 0x7FFF0000) != 0</c> and
    /// <c>(tag &amp; 0xFFFF0000) != 0xC0000000</c>, else 1; for 0x80000001 it is 1.
    /// </summary>
    public static int MaxFactorySizeForEntryTag(uint tag) =>
        MaxFactoryEntrySizeTable.TryGetValue(tag, out int value) ? value : 0;

    // fidelity: M3-026, M3-030
    /// <summary>
    /// Queues a READ and delivers the terminal result to <paramref name="callback"/>. An invalid tag is not sent:
    /// the callback gets <c>(-6, empty)</c> (M3-026). The request length is computed from the tag (M3-027). When
    /// <paramref name="sink"/> is given the assembled bytes are copied into it (M3-030); when
    /// <paramref name="broadcast"/> is set the completed buffer is re-chunked to <see cref="NVStorageOpResultBroadcast"/>.
    /// </summary>
    public void Read(uint tag, Action<NvResult>? callback, List<byte>? sink = null, bool broadcast = false)
    {
        if (!IsValidEntryTag(tag))
        {
            lock (_gate) _log.Add($"warning: NVStorageComponent.Read.InvalidTag: Tag: 0x{tag:X8}");
            if (broadcast)
                NVStorageOpResultBroadcast?.Invoke(new NVStorageOpResult(tag, OpRead, -6, 0, Array.Empty<byte>()));
            callback?.Invoke(new NvResult(-6, Array.Empty<byte>()));
            return;
        }
        Enqueue(new PendingRequest { Tag = tag, Op = OpRead, Callback = callback, Sink = sink, Broadcast = broadcast });
    }

    /// <summary>Queues any NV operation with an explicit length; the callback owns the terminal result.</summary>
    public void Request(uint tag, int length, byte op, byte[] data, Action<NvResult> callback) =>
        Enqueue(new PendingRequest { Tag = tag, Length = length, Op = op, Data = data, Callback = callback });

    /// <summary>Queues a READ and waits for its terminal result (the request length is computed by the component).</summary>
    public async Task<NvResult> ReadAsync(uint tag, TimeSpan? timeout = null)
    {
        var tcs = new TaskCompletionSource<NvResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        Read(tag, r => tcs.TrySetResult(r));
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

    // fidelity: M3-026, M3-027
    private void StartNextLocked()
    {
        if (_queue.Count == 0) { _inFlight = null; return; }
        _inFlight = _queue.Dequeue();
        var req = _inFlight;
        // M3-027: the component computes the READ length from the tag; the caller no longer passes it.
        bool read = req.Op == OpRead;
        if (read) req.Length = IsFactoryEntryTag(req.Tag) ? MaxFactorySizeForEntryTag(req.Tag) : NonFactoryReadLength;
        var command = new NVCommand { Tag = req.Tag, Length = req.Length, Op = req.Op, Unknown = 0, Data = req.Data };
        req.LastCommand = command;
        _log.Add($"NV request tag=0x{req.Tag:X8} op={req.Op} length={req.Length} data={req.Data.Length}B");
        // M3-027: the command is reliable and not hot. MessageHandler::SendMessage ignores those arguments
        // (M1-026) and the transport frames robot-bound messages reliably, so flush: true is the existing call.
        _robot.SendMessage(command, flush: true);
        // M3-027: only the READ path arms the 5 s deadline (+0x74); the write/erase path has its own (out of scope).
        if (read) ArmDeadlineLocked(req);
    }

    // fidelity: M3-027, M3-029
    /// <summary>
    /// M3-027 (pass 1 step 8 / pass 4 1d-5): arm <c>+0x74 = robot+0x2C + 5000</c> unconditionally. Before the first
    /// RobotState robot+0x2C is 0, so the deadline is 5000; it then fires as soon as a state with a larger clock
    /// arrives. (The clock still cannot advance without a RobotState, so with no state it never actually fires.)
    /// </summary>
    private void ArmDeadlineLocked(PendingRequest req) =>
        req.Deadline = (_robot.State.Latest?.Timestamp ?? 0u) + ReadTimeoutTicks;

    /// <summary>
    /// Adds a one-shot on-idle callback (M1 CD20, CB22). It is appended and runs on the next
    /// <see cref="ProcessOnIdle"/> with the queue empty and nothing in flight; it never runs at the moment it is
    /// added, so a read queued later in the same connection broadcast is waited for.
    /// </summary>
    public void OnIdle(Action callback) { lock (_gate) _onIdle.Add(callback); }

    /// <summary>
    /// ProcessOnIdleCallbacks (CD20, 0x00645B10..0x00645B26): runs the pending callbacks only when the queue is
    /// empty and nothing is in flight. Called from Robot::Update's NVStorage step (CD12), and by the request
    /// completion path only after the completed request's own callback has run.
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

    // fidelity: M3-031
    /// <summary>
    /// The per-tick NVStorageComponent::Update timeout check (state 2): when the deadline is set and
    /// <c>robot+0x2C &gt; +0x74</c>, deliver <c>(-4, empty)</c> and complete. There is no retry on a timeout
    /// (0x64575A..0x6457C0). Called from Robot::Update just before <see cref="ProcessOnIdle"/> (CozmoEngine).
    /// </summary>
    public void Update()
    {
        Completion? completion;
        lock (_gate)
        {
            var req = _inFlight;
            if (req?.Deadline is not { } deadline) return;
            if (_robot.State.Latest is not { } state || state.Timestamp <= deadline) return;
            _log.Add($"warning: NVStorageComponent.Update.ReadTimeout: Tag: 0x{req.Tag:X8}");
            completion = CompleteLocked(req, -4, Array.Empty<byte>());
        }
        Deliver(completion.Value);
    }

    /// <summary>
    /// A disconnect discards the queue and the in-flight request without invoking any read callback or timeout, and
    /// drops the pending on-idle callbacks (M3-035; the robot they belonged to is gone). The in-flight request's
    /// deadline and retry state die with it, so no local timer can fire afterwards.
    /// </summary>
    public void OnDisconnected()
    {
        lock (_gate) { _queue.Clear(); _inFlight = null; _onIdle.Clear(); }
    }

    private void OnMessage(RobotMessage m) { if (m is NVOpResult r) OnResult(r); }

    private readonly record struct Completion(Action<NvResult>? Callback, NvResult Result, List<NVStorageOpResult>? Broadcasts, bool RunOnIdle);

    private void OnResult(NVOpResult r)
    {
        Completion? completion;
        lock (_gate)
        {
            var req = _inFlight;
            if (req is null) return;                          // nothing in flight
            // M3-028/M3-026 accept check (pass 1 step 10 / pass 4 1e-1): the reply's base tag must be the pending
            // request's tag. A valid reply's base equals its own tag; the base comparison also admits a factory
            // reply whose raw tag differs from the request.
            uint baseTag = GetBaseEntryTag(r.Tag);
            if (baseTag != req.Tag)
            {
                _log.Add($"warning: NVStorageComponent.HandleNVOpResult.AckdTagNeverRequested: Tag recvd: 0x{r.Tag:X8}, BaseTag: 0x{baseTag:X8}, ExpectedBaseTag: 0x{req.Tag:X8}");
                return;
            }
            _log.Add($"NVOpResult tag=0x{r.Tag:X8} op={r.Op} result={r.Result} index={r.Length} data={r.Data.Length}B");
            sbyte result = r.Result;

            if (result <= -1)
            {
                // M3-031: only {-8,-7,-5,-4} are retried; -6 and -1 are not.
                if (IsRetryableResult(result))
                {
                    if (req.Retries < MaxReadResends)
                    {
                        req.Retries++;
                        _log.Add($"info: NVStorageComponent.HandleNVOpResult.ResentFailedRead: Tag 0x{r.Tag:X8} resent due to {result}");
                        if (req.LastCommand is { } resend) _robot.SendMessage(resend, flush: true);
                        return;
                    }
                    _log.Add($"warning: NVStorageComponent.HandleNVOpResult.ReadOpFailed: Tag: 0x{r.Tag:X8}, result: {result}");
                }
                completion = CompleteLocked(req, result, req.Buffer);
            }
            else
            {
                bool factory = IsFactoryEntryTag(req.Tag);
                // M3-028: at blob index 0, non-factory, before the header is accepted.
                if (r.Length == 0 && !factory && !req.HeaderAccepted)
                {
                    if (r.Data.Length < NvHeaderSize)
                    {
                        _log.Add($"warning: NVStorageComponent.HandleNVOpResult.TooLittleReadData: Tag 0x{r.Tag:X8}, Got {r.Data.Length}, Expected {NvHeaderSize}");
                        req.Clear();                                  // 0x643478: clear the pending buffer before -3
                        result = -3;
                    }
                    else if (BitConverter.ToUInt32(r.Data, 0) != NonFactoryHeaderMagic)
                    {
                        _log.Add($"warning: NVStorageComponent.HandleNVOpResult.InvalidHeader: Tag: 0x{r.Tag:X8}");
                        result = -1;
                    }
                    else
                    {
                        uint total = BitConverter.ToUInt32(r.Data, 8);
                        int max = MaxSizeForEntryTag(req.Tag);
                        if (total > (uint)(max - NvHeaderSize))
                        {
                            _log.Add($"warning: NVStorageComponent.HandleNVOpResult.InvalidDataSize: Tag 0x{r.Tag:X8}, size {total}, maxSizeAllowed {max}");
                            result = -1;
                        }
                        else
                        {
                            req.HeaderAccepted = true;
                            req.HeaderTotal = (int)total;
                            if (total > (uint)(r.Data.Length - NvHeaderSize))
                            {
                                // M3-028: the rest is on the robot; re-request it, reliable and not hot, with no re-arm.
                                _log.Add($"debug: NVStorageComponent.HandleNVOpResult.ReadingRestOfData: Tag: 0x{r.Tag:X8}, TotalSize: {total}");
                                var rerequest = new NVCommand { Tag = r.Tag, Length = (int)total + NvHeaderSize, Op = OpRead, Unknown = 0, Data = Array.Empty<byte>() };
                                req.LastCommand = rerequest;
                                _robot.SendMessage(rerequest, flush: true);
                                return;
                            }
                            // Fits in the first blob: 0x643922 resizes the reply vector to total+16 (pass 4b Q3).
                            req.HeaderFitsInFirstBlob = true;
                        }
                    }
                }

                // M3-029: reassemble every applied blob (result >= 0, a non-empty blob).
                if (result >= 0 && r.Data.Length != 0) ApplyBlobLocked(req, r, factory);

                // M3-030: MORE keeps waiting; OKAY completes; any other result completes with that result.
                if (result == ResultMore) return;
                completion = CompleteLocked(req, result, req.Buffer);
            }
        }
        Deliver(completion.Value);
    }

    // fidelity: M3-031
    private static bool IsRetryableResult(sbyte result) => result is -8 or -7 or -5 or -4;

    // fidelity: M3-029
    /// <summary>
    /// M3-029 (0x643538..0x6435AE): place the blob at <c>index*1024 - hdr</c>, where <c>hdr</c> is 16 for index &gt; 0
    /// on a non-factory base and 0 otherwise, and blob 0's source skips the 16-byte header. The buffer grows
    /// zero-filled; duplicates overwrite; every applied blob re-arms the deadline.
    /// </summary>
    private void ApplyBlobLocked(PendingRequest req, NVOpResult r, bool factory)
    {
        int index = r.Length;
        int size = r.Data.Length;
        int hdr = index > 0 && !factory ? NvHeaderSize : 0;
        int sourceSkip = index == 0 && !factory ? NvHeaderSize : 0;
        int offset = index * BlobStride - hdr;
        int count = size - sourceSkip;
        // M3-028 (pass 4b Q3): only on the fits branch is the reply vector resized to total+16 (0x643922), which
        // bounds the index-0 copy at the header total (delivered size == HeaderTotal, no 16-byte zero tail). After
        // a Length = size+16 re-request the engine reassembles each blob with count = size - 16 and no TOT cap.
        if (req.HeaderFitsInFirstBlob && offset == 0 && sourceSkip == NvHeaderSize
            && req.HeaderTotal is { } total && count > total)
            count = total;
        if (count > 0 && offset >= 0) req.Write(offset, r.Data, sourceSkip, count);
        ArmDeadlineLocked(req);
    }

    // fidelity: M3-030
    /// <summary>
    /// M3-030: completes the request, filling the sink, building the broadcast chunks and advancing the queue. The
    /// callback, the broadcasts and the on-idle callbacks run outside the lock, in the engine's order: the request's
    /// own callback first, then the broadcast, then (when this was the last request) the on-idle callbacks.
    /// </summary>
    private Completion CompleteLocked(PendingRequest req, sbyte result, byte[] data)
    {
        List<NVStorageOpResult>? broadcasts = null;
        if (req.Broadcast) broadcasts = BuildBroadcasts(req.Tag, req.Op, result, data);
        if (req.Sink is { } sink) { sink.Clear(); sink.AddRange(data); }
        req.Deadline = null;
        _inFlight = null;
        bool startNext = _queue.Count > 0;
        if (startNext) StartNextLocked();
        return new Completion(req.Callback, new NvResult(result, data), broadcasts, !startNext);
    }

    private void Deliver(Completion c)
    {
        c.Callback?.Invoke(c.Result);
        if (c.Broadcasts is not null)
            foreach (var b in c.Broadcasts) NVStorageOpResultBroadcast?.Invoke(b);
        if (c.RunOnIdle) ProcessOnIdle();
    }

    // fidelity: M3-030
    /// <summary>
    /// M3-030 (0x643718..0x6437D4): re-chunk the buffer into 0x400 blocks; each non-final chunk has result 3 (MORE)
    /// and the final chunk result 0, with the chunk index in word@4. A negative result broadcasts once with size 0.
    /// A non-negative empty buffer broadcasts nothing.
    /// </summary>
    private static List<NVStorageOpResult> BuildBroadcasts(uint tag, byte op, sbyte result, byte[] data)
    {
        var chunks = new List<NVStorageOpResult>();
        if (result < 0)
        {
            chunks.Add(new NVStorageOpResult(tag, op, result, 0, Array.Empty<byte>()));
            return chunks;
        }
        int offset = 0, index = 0;
        while (offset < data.Length)
        {
            int n = Math.Min(data.Length - offset, 0x400);
            sbyte chunkResult = offset + n < data.Length ? (sbyte)ResultMore : (sbyte)ResultOkay;
            var slice = new byte[n];
            Array.Copy(data, offset, slice, 0, n);
            chunks.Add(new NVStorageOpResult(tag, op, chunkResult, index, slice));
            offset += n;
            index++;
        }
        return chunks;
    }

    /// <summary>
    /// M3-030: one engine-to-game <c>BroadcastNVStorageOpResult</c> chunk, sent when a request's broadcast flag is
    /// set. The stack models game broadcasts as events, as the other M3 broadcasts do.
    /// </summary>
    public event Action<NVStorageOpResult>? NVStorageOpResultBroadcast;

    public void Dispose() { _robot.Message -= OnMessage; }
}

/// <summary>The terminal result of one NV request: the NVResult and the assembled bytes (empty on an error).</summary>
public readonly record struct NvResult(sbyte Result, byte[] Data);

/// <summary>M3-030: one chunk of <c>BroadcastNVStorageOpResult</c> (tag, op, result, word@4 index, data).</summary>
public readonly record struct NVStorageOpResult(uint Tag, byte Op, sbyte Result, int Index, byte[] Data);
