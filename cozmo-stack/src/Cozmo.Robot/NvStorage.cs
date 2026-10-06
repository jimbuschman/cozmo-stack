using Cozmo.Protocol;

namespace Cozmo.Robot;

// fidelity: M1-041, M3-022, M3-025, M3-026, M3-027, M3-028, M3-029, M3-030, M3-031, M3-033, M3-034, M3-035
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
/// completion first (the calibration is installed and vision enabled there); the completion then only sets state 0
/// (0x006437EE; SetState 0x00642B0C runs no callbacks), so the on-idle callback (ready to stream) runs on the
/// next <see cref="Update"/>'s state-0 path (0x006456EC) or at once from <see cref="OnIdle"/> when idle
/// (AddOneShotOnIdleCallback, 0x00645C32);</item>
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
            // fidelity: M3-030
            // 0x00643568..0x00643596: the engine's reassembly buffer is the caller's +0x54 vector, so each applied
            // blob lands in the sink as it arrives, zero-filled to the write end. The callback is handed this
            // vector (0x006436B6..0x00643714), so the sink and the buffer stay equal.
            if (Sink is { } sink)
            {
                while (sink.Count < size) sink.Add(0);
                for (int i = 0; i < count; i++) sink[offset + i] = source[sourceOffset + i];
            }
        }

/// <summary>M3-028 (0x643478): TooLittleReadData clears the pending buffer before forcing -3; the engine
/// clears the same +0x54 vector the sink is (0x00643478).</summary>
        public void Clear()
        {
            _buffer = Array.Empty<byte>();
            Sink?.Clear();
        }
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

    /// <summary>M3-033: the queued (not yet sent) request tags in FIFO order, for tests and diagnostics.</summary>
    public IReadOnlyList<uint> QueuedTags { get { lock (_gate) return _queue.Select(r => r.Tag).ToArray(); } }

    /// <summary>M3-033: the tag of the in-flight request, or null when none is (tests and diagnostics).</summary>
    public uint? InFlightTag { get { lock (_gate) return _inFlight?.Tag; } }

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
    /// factory path: an exact factory key is its own base; otherwise, when <c>(tag &amp; 0xFFFF0000) != 0xC0000000</c>
    /// and <c>(tag &amp; 0x7FFF0000) != 0</c> and <c>tag &amp; 0xFFFF0000</c> is a factory key, the base is
    /// <c>tag &amp; 0xFFFF0000</c>; otherwise the sentinel 0x198000. A non-negative tag below 0x198000 takes the
    /// largest <c>_maxSizeTable</c> key at or below it (the tree floor, 0x00644228..0x00644338); anything else
    /// (0x198000 and above, or no key at or below) is the sentinel.
    /// </summary>
    public static uint GetBaseEntryTag(uint tag, Action<string>? log = null)
    {
        if ((tag & 0x80000000u) != 0)                                     // signed <= -1: the factory path
        {
            if (IsFactoryEntryTag(tag)) return tag;
            if ((tag & 0xFFFF0000u) != 0xC0000000u && (tag & 0x7FFF0000u) != 0
                && IsFactoryEntryTag(tag & 0xFFFF0000u))
                return tag & 0xFFFF0000u;
            // 0x006442EC: no factory key matched -> sWarningF(FactoryTagNotFound, "0x%x", tag)
            log?.Invoke($"warning: NVStorageComponent.GetBaseEntryTag.FactoryTagNotFound: 0x{tag:x}");
            return 0x198000u;
        }
        if (tag < 0x198000u)
        {
            uint floor = 0;
            bool found = false;
            foreach (uint key in MaxSizeTable.Keys)
                if (key <= tag && (!found || key > floor)) { floor = key; found = true; }
            if (found) return floor;
        }
// 0x00644274: the coarse test tag >> 15 < 0x33 failed, or no _maxSizeTable key is at or
// below the tag -> sWarningF(TagIsTooSmall, "0x%x", tag)
        log?.Invoke($"warning: NVStorageComponent.GetBaseEntryTag.TagIsTooSmall: 0x{tag:x}");
        return 0x198000u;
    }

    // fidelity: M3-025, M3-027
    /// <summary>
    /// M3-027: a factory READ's request length, the <c>_maxFactoryEntrySizeTable</c> value. InitSizeTable
    /// (pass 1 step 6) computes 0xFFFF when <c>(tag &amp; 0x7FFF0000) != 0</c> and
    /// <c>(tag &amp; 0xFFFF0000) != 0xC0000000</c>, else 1; for 0x80000001 it is 1.
    /// </summary>
    public static int MaxFactorySizeForEntryTag(uint tag) =>
        MaxFactoryEntrySizeTable.TryGetValue(tag, out int value) ? value : 0;

    // fidelity: M3-025
    /// <summary>
    /// M3-025: <c>NVStorage::EnumToString(NVEntryTag)</c> (0x007CEE38..0x007CF0A6): the entry-tag name. The
    /// constants and their strings are the switch arms at 0x7CEE44..0x7CF0A2. GetBaseEntryTag only ever returns a
    /// table key or the sentinel, so every return is covered; an unrecognised tag yields null in the engine.
    /// </summary>
    public static string? NvEntryTagName(uint tag) => tag switch
    {
        0x80000000 => "NVEntry_BirthCertificate",
        0x80000001 => "NVEntry_CameraCalib",
        0x80000002 => "NVEntry_ToolCodeInfo",
        0x80000003 => "NVEntry_CalibPose",
        0x80000004 => "NVEntry_CalibMetaInfo",
        0x80000005 => "NVEntry_ObservedCubePose",
        0x80000006 => "NVEntry_IMUInfo",
        0x80000007 => "NVEntry_CliffValOnDrop",
        0x80000008 => "NVEntry_CliffValOnGround",
        0x80000010 => "NVEntry_PlaypenTestResults",
        0x80000011 => "NVEntry_FactoryLock",
        0x80000012 => "NVEntry_VersionMagic",
        0x80010000 => "NVEntry_CalibImage1",
        0x80020000 => "NVEntry_CalibImage2",
        0x80030000 => "NVEntry_CalibImage3",
        0x80040000 => "NVEntry_CalibImage4",
        0x80050000 => "NVEntry_CalibImage5",
        0x80060000 => "NVEntry_CalibImage6",
        0x80100000 => "NVEntry_ToolCodeImageLeft",
        0x80110000 => "NVEntry_ToolCodeImageRight",
        0xC0000000 => "NVEntry_PrePlaypenResults",
        0xC0000001 => "NVEntry_PrePlaypenCentroids",
        0xC0000004 => "NVEntry_IMUAverages",
        0xFFFFFFFF => "NVEntry_Invalid",
        0x180000 => "NVEntry_GameSkillLevels",
        0x181000 => "NVEntry_OnboardingData",
        0x182000 => "NVEntry_GameUnlocks",
        0x183000 => "NVEntry_FaceEnrollData",
        0x184000 => "NVEntry_FaceAlbumData",
        0x194000 => "NVEntry_NurtureGameData",
        0x195000 => "NVEntry_InventoryData",
        0x196000 => "NVEntry_LabAssignments",
        0x197000 => "NVEntry_SavedCubeIDs",
        0x198000 => "NVEntry_NEXT_SLOT",
        0x1C0000 => "NVEntry_FACTORY_RESERVED1",
        0x1DE000 => "NVEntry_FACTORY_RESERVED2",
        0xDE000 => "NVEntry_FactoryBaseTag",
        0xDE030 => "NVEntry_FactoryBaseTagWithBCOffset",
        _ => null,
    };

    // fidelity: M3-030, M3-031
    /// <summary>
    /// M3-030/M3-031: <c>NVStorage::EnumToString(NVResult)</c> (0x007CFAB0; table 0x01034B50): the result name.
    /// The engine's table is indexed by <c>result + 9</c> and the values are the names below (NVResultFromString
    /// 0x007CFACC confirms each byte: NV_LOOP -8, NV_NO_MEM -7, NV_BAD_ARGS -6, NV_BUSY -5, NV_TIMEOUT -4,
    /// NV_ERROR -3, NV_NO_ROOM -2, NV_NOT_FOUND -1, NV_OKAY 0, NV_SCHEDULED 1, NV_NO_DO 2, NV_MORE 3).
    /// </summary>
    public static string? NvResultName(sbyte result) => result switch
    {
        -9 => "NV_CORRUPT",
        -8 => "NV_LOOP",
        -7 => "NV_NO_MEM",
        -6 => "NV_BAD_ARGS",
        -5 => "NV_BUSY",
        -4 => "NV_TIMEOUT",
        -3 => "NV_ERROR",
        -2 => "NV_NO_ROOM",
        -1 => "NV_NOT_FOUND",
        0 => "NV_OKAY",
        1 => "NV_SCHEDULED",
        2 => "NV_NO_DO",
        3 => "NV_MORE",
        _ => null,
    };

    // fidelity: M3-031
    /// <summary>
    /// M3-031: <c>NVStorage::EnumToString(NVOperation)</c>: NVOP_READ 0, NVOP_WRITE 1, NVOP_ERASE 2,
    /// NVOP_WIPEALL 3 (NVOperationFromString 0x007D0074 stores 0/1/2/3 for those strings; the strings are at
    /// 0x00C2179D..0x00C217BD).
    /// </summary>
    public static string? NvOpName(byte op) => op switch
    {
        OpRead => "NVOP_READ",
        OpWrite => "NVOP_WRITE",
        OpErase => "NVOP_ERASE",
        OpWipeAll => "NVOP_WIPEALL",
        _ => null,
    };

    // fidelity: M11-011, M3-026, M3-030, M15-014
    /// <summary>
    /// Queues a READ and delivers the terminal result to <paramref name="callback"/>. An invalid tag is not sent:
    /// the callback gets <c>(-6, empty)</c> (M3-026). The request length is computed from the tag (M3-027). When
    /// <paramref name="sink"/> is given the assembled bytes are copied into it (M3-030); when
    /// <paramref name="broadcast"/> is set the completed buffer is re-chunked to <see cref="NVStorageOpResultBroadcast"/>.
    /// Returns 1 when the tag is valid and the request was queued, 0 for an invalid tag only (Appendix G Q2;
    /// <c>NVStorageComponent::Read</c> 0x00644E14..0x00644EF7 - no absent-component, capacity or in-flight check).
    /// </summary>
    public int Read(uint tag, Action<NvResult>? callback, List<byte>? sink = null, bool broadcast = false)
    {
        if (!IsValidEntryTag(tag))
        {
            lock (_gate) _log.Add($"warning: NVStorageComponent.Read.InvalidTag: Tag: 0x{tag:x}");
            if (broadcast)
                NVStorageOpResultBroadcast?.Invoke(new NVStorageOpResult(tag, OpRead, -6, 0, Array.Empty<byte>()));
            callback?.Invoke(new NvResult(-6, Array.Empty<byte>()));
            return 0;
        }
        // M3-026 / 0x00644E3A..0x00644E4C: name and log before emplace_back.
        lock (_gate)
        {
            if (NvEntryTagName(tag) is { } name)
                _log.Add($"info: NVStorageComponent.Read.QueueingReadRequest: {name}");
            else
                _log.Add("MISSING: NVStorageComponent.Read.QueueingReadRequest NULL-%s rendering");
            _queue.Enqueue(new PendingRequest { Tag = tag, Op = OpRead, Callback = callback, Sink = sink, Broadcast = broadcast });
        }
        return 1;
    }

    /// <summary>Queues any NV operation with an explicit length; the callback owns the terminal result.</summary>
    public void Request(uint tag, int length, byte op, byte[] data, Action<NvResult> callback) =>
        Enqueue(new PendingRequest { Tag = tag, Length = length, Op = op, Data = data, Callback = callback });

    // fidelity: M15-014
    /// <summary>
    /// <c>NVStorageComponent::Write</c> (Appendix G Q1, Appendix I): the engine returns 0 for an invalid tag,
    /// for a factory tag when <c>+0x15C == 0</c> (the factory data is not loaded), for null data and for an
    /// oversize request (<c>0x00644578..0x006446B8</c>); otherwise the WRITE is queued and returns 1.
    /// <c>NeedsManager::StartWriteToRobot</c> uses this to put the 116-byte <c>NeedsStateOnRobot</c> blob on
    /// the non-factory key 0x194000, which none of the extra zero conditions affect, and treats 0 as
    /// <c>StartWriteToRobot.WriteFailed</c>. This port returns 0 only for the invalid tag.
    /// </summary>
    public int Write(uint tag, byte[] data, Action<NvResult>? callback)
    {
        if (!IsValidEntryTag(tag))
        {
            lock (_gate) _log.Add($"warning: NVStorageComponent.Write.InvalidTag: Tag: 0x{tag:X8}");
            callback?.Invoke(new NvResult(-6, Array.Empty<byte>()));
            return 0;
        }
        Enqueue(new PendingRequest { Tag = tag, Length = data.Length, Op = OpWrite, Data = data, Callback = callback });
        return 1;
    }

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

    // fidelity: M3-026
    /// <summary>
    /// M3-026: <c>Read</c> only validates and pushes onto the deque (+0xF8, 0x644E82). The request is not sent
    /// here; <see cref="Update"/> pops the front and sends it in state 0.
    /// </summary>
    private void Enqueue(PendingRequest r)
    {
        lock (_gate) _queue.Enqueue(r);
    }

    // fidelity: M3-026, M3-027
    /// <summary>
    /// M3-026/M3-027 (ProcessRequest READ, 0x64503E..0x64507C; 0x64536A; 0x645392..0x645484): pop the front
    /// request, compute a READ's Length from the tag, send it reliable and not hot, clear the caller's sink
    /// (+0x54, 0x645448..0x64546E) and arm the 5 s deadline. Only <see cref="Update"/> calls this, in state 0.
    /// </summary>
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
        // M3-030 (0x645448..0x64546E): the caller's sink vector (+0x54) is cleared at arm, not at completion.
        req.Sink?.Clear();
        // M3-027: only the READ path arms the 5 s deadline (+0x74); the write/erase path has its own (out of scope).
        if (read) ArmDeadlineLocked(req);
    }

    // fidelity: M3-027, M3-029
    /// <summary>
    /// M3-027 (pass 1 step 8 / pass 4 1d-5): arm <c>+0x74 = robot+0x2C + 5000</c> unconditionally, where
    /// robot+0x2C is the synchronised clock (written only once +0x29 is set, 0x0051293C..0x00512954; M3-031).
    /// Before the first synced state it is 0, so the deadline is 5000.
    /// </summary>
    private void ArmDeadlineLocked(PendingRequest req) => req.Deadline = SyncedClock + ReadTimeoutTicks;

    // fidelity: M3-031
    /// <summary>
    /// M3-031: robot+0x2C is the synchronised robot clock, written only when robot+0x29 (time synced) is set
    /// (0x0051293C..0x00512954). In this stack that is the RobotState stored by
    /// <c>EngineRobot.UpdateFullRobotState</c> (the state that passed the time-sync gate), not the unfiltered
    /// <c>State.Latest</c>. Before the first synced state it is 0.
    /// </summary>
    private uint SyncedClock => _robot.Engine.Robot?.StoredState?.Timestamp ?? 0u;

    /// <summary>
    /// Adds a one-shot on-idle callback (M1 CD20, CB22; AddOneShotOnIdleCallback 0x00645C20..0x00645C32). It is
    /// appended and then <see cref="ProcessOnIdle"/> is called at once, so the callback runs immediately when the
    /// component is idle (the deque empty and nothing in flight); otherwise it waits for the state-0 path of a
    /// later <see cref="Update"/>.
    /// </summary>
    public void OnIdle(Action callback) { lock (_gate) _onIdle.Add(callback); ProcessOnIdle(); }

    /// <summary>
    /// ProcessOnIdleCallbacks (CD20, 0x00645B08..0x00645B26): runs the pending callbacks only when the queue is
    /// empty and nothing is in flight. Called from <see cref="Update"/>'s state-0 path (0x006456EC) and from
    /// <see cref="OnIdle"/> (AddOneShotOnIdleCallback, 0x00645C32). A request completion does not run it: 0x006437EE
    /// calls only SetState(0), and SetState (0x00642B0C) runs no callbacks.
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

    // fidelity: M3-026, M3-027, M3-030, M3-031
    /// <summary>
    /// NVStorageComponent::Update (0x6456A4). In state 0 (nothing in flight) it calls ProcessRequest, which pops
    /// the front and sends it (0x6456BC..0x6456CC), then ProcessOnIdleCallbacks (0x6456EC); when the queue is empty
    /// ProcessRequest sends nothing and the on-idle callbacks still run here. In state 2 (a read pending) it only
    /// checks the 5 s deadline: when the synchronised clock (robot+0x2C) is strictly greater than <c>+0x74</c> it
    /// delivers <c>(-4, empty)</c> to the callback only - no broadcast chunk - and clears the pending request
    /// (0x64575A..0x6457C0). A completion sets state 0; the next queued request and the on-idle callbacks run on
    /// the next Update's state-0 path, not from the completion.
    /// Called from Robot::Update after Gate A (0x0051416A, CozmoEngine).
    /// </summary>
    public void Update()
    {
        Action<NvResult>? timeoutCallback = null;
        NvResult timeoutResult = default;
        bool runOnIdle = false;
        lock (_gate)
        {
            if (_inFlight is { } req)
            {
                // State 2 (read pending): only the timeout check. The deadline is only ever set for a READ.
                if (req.Deadline is { } deadline)
                {
                    if (SyncedClock > deadline)
                    {
                        _log.Add($"warning: NVStorageComponent.Update.ReadTimeout: Tag: 0x{req.Tag:x}");
                        req.Deadline = null;
                        _inFlight = null;
                        timeoutCallback = req.Callback;
                        timeoutResult = new NvResult(-4, Array.Empty<byte>());
                    }
                }
            }
            else
            {
                // State 0 (0x6456BC..0x6456EC): ProcessRequest sends the front, if any, then ProcessOnIdleCallbacks.
                if (_queue.Count > 0) StartNextLocked();
                runOnIdle = true;
            }
        }
        // M3-030: on timeout only the callback runs; there is no broadcast chunk and no sink fill.
        timeoutCallback?.Invoke(timeoutResult);
        if (runOnIdle) ProcessOnIdle();
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

    private readonly record struct Completion(Action<NvResult>? Callback, NvResult Result, List<NVStorageOpResult>? Broadcasts, bool ReadCallback, uint Tag);

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
            uint baseTag = GetBaseEntryTag(r.Tag, _log.Add);
            if (baseTag != req.Tag)
            {
                _log.Add($"warning: NVStorageComponent.HandleNVOpResult.AckdTagNeverRequested: Tag recvd: 0x{r.Tag:X8}, BaseTag: 0x{baseTag:X8}, ExpectedBaseTag: 0x{req.Tag:X8}");
                return;
            }
            _log.Add($"NVOpResult tag=0x{r.Tag:X8} op={r.Op} result={r.Result} index={r.Length} data={r.Data.Length}B");
            sbyte result = r.Result;

            // fidelity: M15-014
            // Appendix I3 (0x00642F8C..0x00643937): op 0 takes the read header/reassembly path; ops 1-3 (WRITE,
            // ERASE, WIPEALL) take the write terminal (0x00643054..0x00643424). The write terminal completes with
            // the result byte, delivering 0 for a successful write.
            if (req.Op != OpRead)
            {
                completion = WriteTerminalLocked(req, r);
                if (completion is null) return;               // a retry was sent; keep waiting
            }
            else if (result <= -1)
            {
                // fidelity: M3-031
                // M3-031 (0x006431E6..0x00643234): only {-8,-7,-5,-4} are retried; -6 and -1 are not. On a retry
                // ResendLastCommand (0x00645C54) logs Retry and sends, then the caller logs ResentFailedRead; when
                // the counter reaches +0xF5 = 8 it logs NumRetriesExceeded and returns 0, and the caller then logs
                // ReadOpFailed for every negative result (0x006434E4).
                if (IsRetryableResult(result))
                {
                    if (req.Retries < MaxReadResends)
                    {
                        req.Retries++;
                        _log.Add($"info: NVStorageComponent.ResendLastCommand.Retry: Tag: 0x{req.Tag:x}, Op: {NvOpName(req.Op)}, Attempt: {req.Retries}");
                        if (req.LastCommand is { } resend) _robot.SendMessage(resend, flush: true);
                        _log.Add($"info: NVStorageComponent.HandleNVOpResult.ResentFailedRead: Tag 0x{r.Tag:x} resent due to {NvResultName(result)}");
                        return;
                    }
                    _log.Add($"error: NVStorageComponent.ResendLastCommand.NumRetriesExceeded: Tag: 0x{req.Tag:x}, Op: {NvOpName(req.Op)}, Attempts: {MaxReadResends + 1}");
                }
                _log.Add($"warning: NVStorageComponent.HandleNVOpResult.ReadOpFailed: Tag: 0x{r.Tag:x}, op: {NvOpName(req.Op)}, result: {NvResultName(result)}");
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
            // fidelity: M3-030
            // 0x00643600..0x00643694: after reassembly the engine logs the read outcome by the final result:
            // ReadSuccess (result 0, 0x00643640), ReadEntryNotFound (-1, 0x0064360E) or ReadFailed (anything else,
            // 0x0064366E). MORE (3) skips the log (0x00643606 -> 0x0064325A) and never reaches the completion.
            if (req.Op == OpRead) LogReadResult(r, baseTag, result);
        }
        Deliver(completion.Value);
    }

    // fidelity: M3-030
    /// <summary>
    /// M3-030: the read completion's outcome log (0x00643600..0x00643694). The base tag is named through
    /// <c>NVStorage::EnumToString(NVEntryTag)</c> (0x7CEE38); ReadSuccess/ReadEntryNotFound are channeled info,
    /// ReadFailed is a warning. The engine's formats are "BaseTag: %s, result: %s" and, for ReadEntryNotFound,
    /// "BaseTag: %s, Tag: 0x%x, result: %s".
    /// </summary>
    private void LogReadResult(NVOpResult r, uint baseTag, sbyte result)
    {
        string baseName = NvEntryTagName(baseTag) ?? $"0x{baseTag:X8}";
        if (result == 0)
            _log.Add($"info: NVStorageComponent.HandleNVOpResult.ReadSuccess: BaseTag: {baseName}, result: {NvResultName(0)}");
        else if (result == -1)
            _log.Add($"info: NVStorageComponent.HandleNVOpResult.ReadEntryNotFound: BaseTag: {baseName}, Tag: 0x{r.Tag:x}, result: {NvResultName(-1)}");
        else
            _log.Add($"warning: NVStorageComponent.HandleNVOpResult.ReadFailed: BaseTag: {baseName}, result: {NvResultName(result)}");
    }

    // fidelity: M3-031
    private static bool IsRetryableResult(sbyte result) => result is -8 or -7 or -5 or -4;

    // fidelity: M15-014, M3-031
    /// <summary>
    /// The WRITE/ERASE/WIPEALL terminal (Appendix I3; 0x00643054..0x00643424). A negative result resends for
    /// {-8,-7,-5,-4} (i.e. -8..-4 except -6) while retries remain; otherwise it logs <c>WriteOpFailed</c> for
    /// <b>every</b> negative result and completes with that result. A non-negative result logs
    /// <c>WriteSuccess</c> and completes with the reply's own result byte (0 on success; 1/2 deliver 1/2).
    /// Clearing <c>+0x48</c>/<c>+0x1C</c> and <c>SetState(0)</c> are the queue/in-flight reset
    /// <see cref="CompleteLocked"/> performs; the engine's <c>WriteDataForTag</c> backup side effect has no
    /// counterpart here.
    /// </summary>
    private Completion? WriteTerminalLocked(PendingRequest req, NVOpResult response)
    {
        sbyte result = response.Result;
        if (result <= -1)
        {
            if (IsRetryableResult(result))
            {
                if (req.Retries < MaxReadResends)
                {
                    req.Retries++;
                    _log.Add($"info: NVStorageComponent.ResendLastCommand.Retry: Tag: 0x{req.Tag:x}, Op: {NvOpName(req.Op)}, Attempt: {req.Retries}");
                    if (req.LastCommand is { } resend) _robot.SendMessage(resend, flush: true);
                    // 0x006431AA..0x006431E0 formats the received result/tag/op;
                    // ResendLastCommand's own Retry above uses the saved command's tag/op.
                    _log.Add($"info: NVStorageComponent.HandleNVOpResult.ResentFailedWrite: Tag 0x{response.Tag:x} resent due to {NvResultName(result)}, op: {NvOpName(response.Op)}");
                    return null;
                }
                _log.Add($"error: NVStorageComponent.ResendLastCommand.NumRetriesExceeded: Tag: 0x{req.Tag:x}, Op: {NvOpName(req.Op)}, Attempts: {MaxReadResends + 1}");
            }
            _log.Add($"warning: NVStorageComponent.HandleNVOpResult.WriteOpFailed: Tag: 0x{req.Tag:X8}, result: {result}");
            return CompleteLocked(req, result, req.Buffer);
        }
        _log.Add($"info: NVStorageComponent.HandleNVOpResult.WriteSuccess: Tag: 0x{req.Tag:X8}");
        return CompleteLocked(req, result, req.Buffer);
    }

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
        // The resize is one-time (0x00643922): the cap applies only to the blob the header branch sized, so the
        // flag is cleared after the first apply and cannot cap a later duplicate index-0 blob.
        if (req.HeaderFitsInFirstBlob && offset == 0 && sourceSkip == NvHeaderSize
            && req.HeaderTotal is { } total && count > total)
            count = total;
        req.HeaderFitsInFirstBlob = false;
        if (count > 0 && offset >= 0) req.Write(offset, r.Data, sourceSkip, count);
        ArmDeadlineLocked(req);
    }

    // fidelity: M3-030, M3-034
    /// <summary>
    /// M3-030: completes the request, building the broadcast chunks and setting state 0. The callback and the
    /// broadcasts run outside the lock, in the engine's order: the request's own callback first, then the
    /// broadcast. The sink (+0x54) was cleared at arm and is filled blob by blob in <see cref="ApplyBlobLocked"/>,
    /// so nothing is appended here. The completion does not start the next request and does not run the on-idle
    /// callbacks (SetState(0) only, 0x6437EA; SetState 0x00642B0C runs no callbacks); <see cref="Update"/> sends
    /// the next request and runs them.
    /// </summary>
    private Completion CompleteLocked(PendingRequest req, sbyte result, byte[] data)
    {
        List<NVStorageOpResult>? broadcasts = null;
        if (req.Broadcast) broadcasts = BuildBroadcasts(req.Tag, req.Op, result, data);
        req.Deadline = null;
        _inFlight = null;
        return new Completion(req.Callback, new NvResult(result, data), broadcasts, req.Op == OpRead, req.Tag);
    }

    private void Deliver(Completion c)
    {
        // M3-030 / 0x006436B6..0x00643714: only a present read callback logs this,
        // after the outcome log and immediately before invoking the callback.
        if (c.ReadCallback && c.Callback is not null)
        {
            lock (_gate)
                _log.Add(NvEntryTagName(c.Tag) is { } name
                    ? $"debug: NVStorageComponent.HandleNVOpResult.ExecutingReadCallback: {name}"
                    : "MISSING: NVStorageComponent.HandleNVOpResult.ExecutingReadCallback NULL-%s rendering");
        }
        c.Callback?.Invoke(c.Result);
        if (c.Broadcasts is not null)
            foreach (var b in c.Broadcasts) NVStorageOpResultBroadcast?.Invoke(b);
    }

    // fidelity: M3-030
    /// <summary>
    /// M3-030 (0x643718..0x6437D4): re-chunk the buffer into 0x400 blocks; each non-final chunk has result 3 (MORE)
    /// and the final chunk carries the request's actual result (0x0064373C..0x0064375C), with the chunk index in word@4. A
    /// negative result broadcasts once with size 0. A non-negative empty buffer broadcasts nothing.
    /// 0x00643770..0x00643798: the loop is bounded at 1000 chunks (cmp r4,#0x3e8, blo back to 0x643738); if the
    /// 1000th broadcast does not exhaust the buffer it stops and logs LoopBoundOverflow via sErrorF with
    /// "../../../../engine/components/nvStorageComponent.cpp", line 0x4a7.
    /// </summary>
    private List<NVStorageOpResult> BuildBroadcasts(uint tag, byte op, sbyte result, byte[] data)
    {
        var chunks = new List<NVStorageOpResult>();
        if (result < 0)
        {
            chunks.Add(new NVStorageOpResult(tag, op, result, 0, Array.Empty<byte>()));
            return chunks;
        }
        int offset = 0, index = 0;
        while (true)
        {
            if (offset >= data.Length) return chunks;                     // uVar22 == 0 -> return, no log
            int n = Math.Min(data.Length - offset, 0x400);
            sbyte chunkResult = data.Length - offset > 0x400 ? (sbyte)ResultMore : result;
            var slice = new byte[n];
            Array.Copy(data, offset, slice, 0, n);
            chunks.Add(new NVStorageOpResult(tag, op, chunkResult, index, slice));
            offset += n;
            index++;
            if (index >= 1000)
            {
                _log.Add("error: LoopBoundOverflow: ../../../../engine/components/nvStorageComponent.cpp:1191");
                return chunks;
            }
        }
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
