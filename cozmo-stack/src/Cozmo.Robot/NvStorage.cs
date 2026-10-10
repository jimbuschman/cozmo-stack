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

    /// <summary>M3-031 N9: the retry limit byte (+0xF5), 8 from the constructor (0x006428B8); ResendLastCommand refuses when the incremented counter reaches it.</summary>
    private const int RetryLimit = 8;

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
        /// <summary>
        /// M3-030 R1 (0x00643042..0x00643048): the normalised response tag the callback and its log are named by
        /// (<c>GetBaseEntryTag</c> of the response, or 0x198000 for a WIPEALL response, 0x0064302C). Set when a response
        /// is accepted; the request's own tag until then.
        /// </summary>
        public uint? ResponseBaseTag { get; set; }
        // fidelity: M3-043
        /// <summary>WB3: the size the ERASE was queued with (front +0x40); 0 asks for the tag's maximum size.</summary>
        public uint EraseSize { get; init; }
        /// <summary>WB11: the component's +0xC (the base tag of the write in progress).</summary>
        public uint WriteBaseTag { get; set; }
        /// <summary>WB11/WB15: +0x10, the tag of the next chunk.</summary>
        public uint NextTag { get; set; }
        /// <summary>WB11/WB15: +0x14, how much of <see cref="Data"/> has been put in chunks.</summary>
        public int Offset { get; set; }
        /// <summary>+0x1C: more chunks are still to be sent.</summary>
        public bool MoreData { get; set; }
        /// <summary>+0x48: a write, erase or wipe ack is awaited.</summary>
        public bool AckAwaited { get; set; }
        /// <summary>+0x20: the tag the awaited ack must normalise to (the raw tag for ERASE, 0x198000 for WIPEALL, the base tag for WRITE).</summary>
        public uint AckTag { get; set; }

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

    // fidelity: M3-027
    /// <summary>
    /// The component's one saved <c>NVCommand</c> (N1..N13): the tag (+0xDC), length (+0xE0), op (+0xE4), a byte at +0xE5
    /// that only the constructor writes (0, 0x006428AA) and the <c>Data</c> vector (+0xE8/+0xEC/+0xF0, null at
    /// construction, 0x006428AE..0x006428B4). READ dispatch (0x0064503E..0x0064507C, 0x0064536A..0x0064539E), the
    /// re-request (0x00643888..0x006438BC) and the resend (0x00645CD4..0x00645CEE) all build their outgoing command from
    /// it, copying the header and the Data. Nothing clears it: <c>SetState(0)</c> (0x00642B52..0x00642B60) resets only
    /// +0x48, +0x1C and +0x78, so a completion, a timeout or an idle tick leave it as it was (N2, N11).
    /// </summary>
    internal sealed class SavedCommand
    {
        public uint Tag;                                   // +0xDC
        public int Length;                                 // +0xE0, independent of Data.Length (N13)
        public byte Op;                                    // +0xE4
        /// <summary>+0xE5: written only by the constructor (0).</summary>
        public const byte Byte9 = 0;
        public byte[] Data = Array.Empty<byte>();          // +0xE8: the logical vector; a copy goes into each command

        /// <summary>The outgoing command: the saved header and a copy of the saved Data (0x0064537A..0x00645392).</summary>
        public NVCommand ToCommand() => new() { Tag = Tag, Length = Length, Op = Op, Unknown = Byte9, Data = (byte[])Data.Clone() };

        /// <summary>
        /// N5..N8 (0x00645822..0x00645988): the chunk assembly of <c>Update</c> state 1. The saved tag becomes the
        /// request's current chunk tag (+0x10) and the op 1; the vector's logical end goes back to its begin (the
        /// allocation stays); on offset 0 with +0x15C false the 16-byte header is inserted first, then the chunk is
        /// appended, and the saved Length is the vector's size. The header is the little-endian magic 0x435A4D4F, a zero
        /// u32, the source length as a u32 and a zero u16 at byte 12; bytes 14 and 15 are never written (N6: the halfword
        /// store at sp+0x38 stops at byte 13), so the engine copies whatever the stack held, and they are 0 here
        /// (<see cref="HeaderNeverWrittenBytes"/>). The chunk is min(remaining, 0x400), or min(remaining, 0x3F0) when the
        /// header was inserted. Returns the number of source bytes appended; the caller advances its offset (+0x14) by it
        /// and its next tag (+0x10) by 1 for a factory tag, else 0x400 (0x006458A6..0x006458C2).
        /// </summary>
        public int AssembleWriteChunk(uint chunkTag, byte[] source, int sourceOffset, bool factoryDataFlag)
        {
            Tag = chunkTag;
            Op = OpWrite;
            int remaining = source.Length - sourceOffset;
            int chunk = Math.Min(remaining, 0x400);
            var data = new List<byte>();
            if (sourceOffset == 0 && !factoryDataFlag)
            {
                var header = new byte[NvHeaderSize];                       // bytes 14..15 stay 0: never written (N6)
                BitConverter.GetBytes(NonFactoryHeaderMagic).CopyTo(header, 0);
                BitConverter.GetBytes((uint)source.Length).CopyTo(header, 8);
                data.AddRange(header);
                chunk = Math.Min(remaining, 0x3F0);
            }
            for (int i = 0; i < chunk; i++) data.Add(source[sourceOffset + i]);
            Data = data.ToArray();
            Length = Data.Length;                                          // 0x00645918..0x00645920
            return chunk;
        }
    }

    /// <summary>The two bytes of the 16-byte write header that the engine never writes (N6): offsets 14 and 15.</summary>
    internal static readonly int[] HeaderNeverWrittenBytes = { 14, 15 };

    private readonly CozmoRobot _robot;
    private readonly object _gate = new();
    private SavedCommand _saved = new();

    /// <summary>
    /// A new component starts with the saved command as the constructor leaves it: byte +0xE5 = 0 and Data null (0x006428AA..0x006428B4).
    /// Tag, Length and op (+0xDC..+0xE4) are never written by the constructor: 0 under policy M3-037.
    /// </summary>
    // fidelity: M3-037
    private void ResetSavedToConstructed() => _saved = new SavedCommand();
    /// <summary>+8: 0 idle, 1 write/erase/wipe pending, 2 read pending.</summary>
    private int _state;
    /// <summary>+0x78: a read has been dispatched and SetState(0) has not run since (0x00645422, 0x00642B5C).</summary>
    private bool _readPending;
    /// <summary>+0x50: the tag of the last dispatched read; SetState(0) leaves it (0x0064542A).</summary>
    private uint _readTag = 0x198000;   // +0x50: the constructor stores 0x198000 (0x00642872, 0x0064289E); SetState(0) leaves it
    private readonly Queue<PendingRequest> _queue = new();
    private PendingRequest? _inFlight;
    private readonly List<Action> _onIdle = new();
    private readonly object _idleExecution = new();
    private readonly List<string> _log = new();

    internal NvStorageComponent(CozmoRobot robot)
    {
        ConstructedSetStateLocked();
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
            // A null name prints "(null)": bionic's printf for a NULL %s (a system-library boundary, manager ruling 2026-10-10).
            _log.Add($"info: NVStorageComponent.Read.QueueingReadRequest: {NvEntryTagName(tag) ?? "(null)"}");
            _queue.Enqueue(new PendingRequest { Tag = tag, Op = OpRead, Callback = callback, Sink = sink, Broadcast = broadcast });
        }
        return 1;
    }

    /// <summary>
    /// Compatibility entry for callers that name the operation: it calls the engine's own API (<see cref="Write(uint, byte[]?, Action{NvResult}?, bool)"/>,
    /// <see cref="Erase"/>, <see cref="WipeAll"/>, <see cref="Read"/>), so every check, the preceding erase and the chunk loop apply. The length
    /// argument is not used: the engine computes every length itself.
    /// </summary>
    public void Request(uint tag, int length, byte op, byte[] data, Action<NvResult> callback, bool broadcast = false)
    {
        switch (op)
        {
            case OpWrite: Write(tag, data, callback, broadcast); break;
            case OpErase: Erase(tag, callback, broadcast); break;
            case OpWipeAll: WipeAll(callback, broadcast); break;
            default: Read(tag, callback, null, broadcast); break;
        }
    }

    // fidelity: M3-043, M15-014
    /// <summary>
    /// The vector overload (WA1..WA3, 0x006443F4): a null vector logs NullData (info) and returns false with no broadcast and no callback;
    /// otherwise the pointer overload runs with the vector's own, unpadded length. Returns 1 when the write was queued, else 0.
    /// </summary>
    public int Write(uint tag, byte[]? data, Action<NvResult>? callback, bool broadcast = false)
    {
        if (data is null)
        {
            lock (_gate) _log.Add($"info: NVStorageComponent.Write.NullData: {TagNameOrMissing(tag)}");
            return 0;
        }
        return Write(tag, data, (uint)data.Length, callback, broadcast);
    }

    // fidelity: M3-043, M15-014
    /// <summary>
    /// The pointer overload (WA5..WA14, 0x006444FC). Every check is evaluated, none short-circuits another: the tag (InvalidTag), a factory tag
    /// without +0x15C (FactoryTagNotAllowed), null data (NullData), and the size (GetMaxSizeForEntryTag, minus 0x10 without +0x15C, the count padded
    /// up to a multiple of 4, rejected when count - 1 is not below the limit as unsigned values: InvalidSize). Any failure broadcasts (-6, op 1) when
    /// asked, then calls the callback with -6, and returns 0. Success queues an ERASE of the tag (no callback, no broadcast) behind the debug log
    /// PrecedingWriteWithErase, then the WRITE with a copy of the padded data, logs DataQueued and returns 1. Nothing is sent here. The 0..3 padding
    /// bytes are read past the caller's count in the engine (WA14, never defined); they are 0 here under policy M3-037.
    /// // fidelity: M3-037
    /// </summary>
    public int Write(uint tag, byte[]? data, uint numBytes, Action<NvResult>? callback, bool broadcast = false)
    {
        bool valid = true;
        lock (_gate)
        {
            string name = TagNameOrMissing(tag);
            if (!IsValidEntryTag(tag))
            {
                _log.Add($"warning: NVStorageComponent.Write.InvalidTag: Tag: {name} (0x{tag:x})");
                valid = false;
            }
            if (IsFactoryEntryTag(tag) && !FactoryWritesAllowed)
            {
                _log.Add($"warning: NVStorageComponent.Write.FactoryTagNotAllowed: Tag: {name} (0x{tag:x})");
                valid = false;
            }
            if (data is null)
            {
                _log.Add($"warning: NVStorageComponent.Write.NullData: {name}");
                valid = false;
            }
            uint max = (uint)MaxSizeForEntryTagLogged(tag);
            uint limit = FactoryWritesAllowed ? max : unchecked(max - 0x10);
            uint n = numBytes;
            uint pad = 4 - (n & 3);
            if (pad < 4) n += pad;
            if (unchecked(n - 1) >= limit)
            {
                _log.Add($"warning: NVStorageComponent.Write.InvalidSize: Tag: {name}, {n} bytes (limit {(int)limit} bytes)");
                valid = false;
            }
            if (valid)
            {
                if (!FactoryWritesAllowed)
                {
                    _log.Add($"debug: NVStorageComponent.Write.PrecedingWriteWithErase: Tag: {name}");
                    _queue.Enqueue(new PendingRequest { Tag = tag, Op = OpErase });
                }
                var vector = new byte[n];
                Array.Copy(data!, vector, (int)Math.Min(numBytes, (uint)data!.Length));
                _queue.Enqueue(new PendingRequest { Tag = tag, Op = OpWrite, Data = vector, Callback = callback, Broadcast = broadcast });
                _log.Add($"debug: NVStorageComponent.Write.DataQueued: {name} - numBytes: {n}");
            }
        }
        if (valid) return 1;
        if (broadcast) NVStorageOpResultBroadcast?.Invoke(new NVStorageOpResult(tag, OpWrite, -6, 0, Array.Empty<byte>()));
        callback?.Invoke(new NvResult(-6, Array.Empty<byte>()));
        return 0;
    }

    // fidelity: M3-043
    /// <summary>
    /// Erase (0x006449C0, 0x00644A40; the queueing side of the sibling ops in the rows): an invalid tag warns Erase.InvalidEntryTag, a factory tag
    /// without +0x15C warns with Write's own key FactoryTagNotAllowed; either broadcasts (-6, op 2) when asked, calls the callback with -6 and
    /// returns false. Otherwise the ERASE is queued with <paramref name="size"/> (0 = the tag's maximum size at dispatch), logged Erase.Queued.
    /// </summary>
    public int Erase(uint tag, Action<NvResult>? callback, bool broadcast = false, uint size = 0)
    {
        bool valid = true;
        lock (_gate)
        {
            string name = TagNameOrMissing(tag);
            if (!IsValidEntryTag(tag))
            {
                _log.Add($"warning: NVStorageComponent.Erase.InvalidEntryTag: Tag: {name} (0x{tag:x})");
                valid = false;
            }
            else if (IsFactoryEntryTag(tag) && !FactoryWritesAllowed)
            {
                _log.Add($"warning: NVStorageComponent.Write.FactoryTagNotAllowed: Tag: {name} (0x{tag:x})");
                valid = false;
            }
            if (valid)
            {
                _queue.Enqueue(new PendingRequest { Tag = tag, Op = OpErase, Callback = callback, Broadcast = broadcast, EraseSize = size });
                _log.Add($"debug: NVStorageComponent.Erase.Queued: {name}");
            }
        }
        if (valid) return 1;
        if (broadcast) NVStorageOpResultBroadcast?.Invoke(new NVStorageOpResult(tag, OpErase, -6, 0, Array.Empty<byte>()));
        callback?.Invoke(new NvResult(-6, Array.Empty<byte>()));
        return 0;
    }

    // fidelity: M3-043
    /// <summary>WipeAll (0x00644C10): no tag or flag checks; the WIPEALL is queued and logged WipeAll.Queued (empty format).</summary>
    public int WipeAll(Action<NvResult>? callback, bool broadcast = false)
    {
        lock (_gate)
        {
            _queue.Enqueue(new PendingRequest { Tag = 0, Op = OpWipeAll, Callback = callback, Broadcast = broadcast });
            _log.Add("debug: NVStorageComponent.WipeAll.Queued: ");
        }
        return 1;
    }

    // fidelity: M3-043
    /// <summary>WipeFactory (0x00644CB8): without +0x15C it logs the error "Must be allowed to write to factory addresses", stores the error flag and returns false.</summary>
    public int WipeFactory(Action<NvResult>? callback)
    {
        lock (_gate)
            _log.Add("error: NVStorageComponent.WipeFactory.NotAllowed: Must be allowed to write to factory addresses");
        Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak();
        return 0;
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

    // fidelity: M3-026, M3-027, M3-043
    /// <summary>
    /// ProcessRequest (0x00644FD4..0x006456A4, WB1): pops the front request and dispatches on its op (READ 0x64503E, WRITE 0x645248,
    /// ERASE 0x64507E, WIPEALL 0x645188). Only <see cref="Update"/> calls this, in state 0.
    /// </summary>
    private void StartNextLocked()
    {
        if (_queue.Count == 0) { _inFlight = null; return; }
        _inFlight = _queue.Dequeue();
        var req = _inFlight;
        switch (req.Op)
        {
            case OpRead: DispatchReadLocked(req); break;
            case OpErase: DispatchEraseLocked(req); break;
            case OpWrite: DispatchWriteLocked(req); break;
            case OpWipeAll: DispatchWipeAllLocked(req); break;
        }
    }

    private void SendSavedLocked(PendingRequest req)
    {
        var command = _saved.ToCommand();
        // M3-027: the command is reliable and not hot. MessageHandler::SendMessage ignores those arguments
        // (M1-026) and the transport frames robot-bound messages reliably, so flush: true is the existing call.
        _robot.SendMessage(command, flush: true);
    }

    // fidelity: M3-043, M3-037
    /// <summary>
    /// The constructor's last act is SetState(0) (0x006428E8), which always logs. PrevState (+8) is never written by the constructor, so
    /// the engine prints whatever the new object held: 0 here, under policy M3-037 (never-written bytes). The engine builds a new
    /// component per Robot, so this runs at construction and again when a removed robot's component is replaced (<see cref="OnDisconnected"/>).
    /// </summary>
    private void ConstructedSetStateLocked()
    {
        _state = 0;                                                    // M3-037: +8 is uninitialised in the engine
        _readPending = false;                                          // SetState(0) clears +0x78 (0x00642B5C)
        _log.Add("debug: NVStorageComponent.SetState: PrevState: 0, NewState: 0");
    }

    /// <summary>SetState (0x00642B0C..0x00642B64, WB7): logs the transition; only state 0 clears +0x48, +0x1C and +0x78 (the request's flags here).</summary>
    private void SetStateLocked(int state)
    {
        _log.Add($"debug: NVStorageComponent.SetState: PrevState: {_state}, NewState: {state}");
        if (state == 0) _readPending = false;               // 0x00642B52..0x00642B5C: only state 0 clears +0x78 (+0x50 stays)
        _state = state;
    }

    private static string TagNameOrMissing(uint tag) =>
        NvEntryTagName(tag) ?? "(null)";   // bionic printf's NULL %s (manager ruling 2026-10-10)

    private void QueueDataToWriteMissing(uint tag, int bytes) =>
        _log.Add($"MISSING: RobotDataBackupManager::QueueDataToWrite(0x{tag:x}, {bytes} bytes) (M15 recipient, 0x0051AE04): the backup manager is not built");

    private void DispatchReadLocked(PendingRequest req)
    {
        // N3 (0x00645040..0x00645046, 0x0064536A..0x00645392): the saved tag (+0xDC) and op (+0xE4 = 0) are written, the
        // Length (+0xE0) is 0x400 or the factory table value, and the outgoing command is the saved header with a copy
        // of the saved Data (0x00645386), whatever an earlier write left in it. The Length is independent of that
        // vector's size (N13).
        _saved.Tag = req.Tag;
        _saved.Op = OpRead;
        _saved.Length = IsFactoryEntryTag(req.Tag) ? MaxFactorySizeForEntryTag(req.Tag) : NonFactoryReadLength;
        req.Length = _saved.Length;
        _readPending = true;                                // +0x78 = 1 (0x00645422)
        _readTag = req.Tag;                                 // +0x50 = the request's tag (0x0064542A)
        SendSavedLocked(req);
        // N3/N4 (0x006453B8..0x006453D2): after the send, ProcessRequest logs the READ's saved tag and length.
        _log.Add($"debug: NVStorageComponent.ProcessRequest.SendingRead: StartTag: 0x{_saved.Tag:x}, Length: {(uint)_saved.Length}");
        // M3-030 (0x645448..0x64546E): the caller's sink vector (+0x54) is cleared at arm, not at completion.
        req.Sink?.Clear();
        ArmDeadlineLocked(req);
        SetStateLocked(2);                                  // 0x00645474
    }

    // fidelity: M3-043
    /// <summary>
    /// ERASE dispatch (WB3..WB7, 0x0064507E..0x00645186): arms the ack (+0x20 = the raw tag, +0x44 = clock + 5000, +0x48 = 1), saves the header
    /// (tag, op 2, Length = the queued size or the tag's maximum size), logs SendingErase, sends {saved tag, Length, op 2, byte, a copy of the
    /// saved Data} - the Data is whatever the last write chunk left - then calls the backup manager's QueueDataToWrite with an empty vector
    /// (M15: a visible MISSING line) and sets state 1.
    /// </summary>
    private void DispatchEraseLocked(PendingRequest req)
    {
        req.MoreData = false;
        req.AckTag = req.Tag;
        req.Deadline = SyncedClock + ReadTimeoutTicks;
        _saved.Tag = req.Tag;
        _saved.Op = OpErase;
        req.AckAwaited = true;
        _saved.Length = req.EraseSize != 0 ? (int)req.EraseSize : MaxSizeForEntryTagLogged(req.Tag);
        _log.Add($"debug: NVStorageComponent.ProcessRequest.SendingErase: {TagNameOrMissing(req.Tag)} (Tag: 0x{req.Tag:x}) size: {(uint)_saved.Length}");
        SendSavedLocked(req);
        QueueDataToWriteMissing(req.Tag, 0);
        SetStateLocked(1);
    }

    // fidelity: M3-043
    /// <summary>
    /// WRITE dispatch (WB11, 0x00645248..0x00645324): sends nothing. The component takes the queued vector (+0x18) and arms the chunk loop
    /// (+0xC = +0x10 = the tag, +0x14 = 0, +0x1C = 1, +0x20 = the tag, +0x44 = clock + 5000, +0x48 = 0); the backup manager gets a copy of the
    /// data (M15: a visible MISSING line); the log says SendingWrite although nothing has been sent; state 1. The saved header and Data are not
    /// touched.
    /// </summary>
    private void DispatchWriteLocked(PendingRequest req)
    {
        req.WriteBaseTag = req.Tag;
        req.NextTag = req.Tag;
        req.MoreData = true;
        req.Offset = 0;
        req.AckTag = req.Tag;
        req.Deadline = SyncedClock + ReadTimeoutTicks;
        req.AckAwaited = false;
        QueueDataToWriteMissing(req.Tag, req.Data.Length);
        _log.Add($"debug: NVStorageComponent.ProcessRequest.SendingWrite: StartTag: 0x{req.Tag:x} ({TagNameOrMissing(req.Tag)}), timeoutTime: {(int)req.Deadline.Value}, currTime: {(int)SyncedClock}");
        SetStateLocked(1);
    }

    // fidelity: M3-043
    /// <summary>
    /// WIPEALL dispatch (WB8, WB9, 0x00645188..0x00645246): logs with the key the engine spells "NVStoageComponent..." and an empty format, arms
    /// the ack for the sentinel tag 0x198000, saves tag 0 and op 3 and sends the saved header with the STALE Length (+0xE0, not written here:
    /// what the previous request left) and a copy of the stale Data. No backup call. A first-ever WIPEALL sends a Length the engine never
    /// wrote (WB10): 0 here, under policy M3-037. // fidelity: M3-037
    /// </summary>
    private void DispatchWipeAllLocked(PendingRequest req)
    {
        _log.Add("debug: NVStoageComponent.ProcessRequest.SendingWipeAll: ");
        req.AckTag = 0x198000;
        req.MoreData = false;
        req.Deadline = SyncedClock + ReadTimeoutTicks;
        _saved.Tag = 0;
        req.AckAwaited = true;
        _saved.Op = OpWipeAll;
        SendSavedLocked(req);
        SetStateLocked(1);
    }

    // fidelity: M3-043
    /// <summary>
    /// The Update state-1 chunk send (WB15..WB20, 0x00645816..0x0064598A), one chunk per call: assemble the chunk into the saved command
    /// (<see cref="SavedCommand.AssembleWriteChunk"/>), advance the offset and the next tag (+1 for a factory base, else 0x400), log
    /// SendingWriteMsg with the payload bytes, send, then await the ack with a fresh 5 s deadline and a zero retry counter; the last chunk
    /// clears the more-data flag. Header bytes 14..15 are never written by the engine; they are 0 here under policy M3-037.
    /// </summary>
    // fidelity: M3-037
    private void SendChunkLocked(PendingRequest req)
    {
        uint chunkTag = req.NextTag;
        int n = _saved.AssembleWriteChunk(chunkTag, req.Data, req.Offset, factoryDataFlag: FactoryWritesAllowed);
        req.Offset += n;
        req.NextTag += IsFactoryEntryTag(req.WriteBaseTag) ? 1u : 0x400u;
        _log.Add($"debug: NVStorageComponent.Update.SendingWriteMsg: BaseTag: {TagNameOrMissing(req.WriteBaseTag)}, tag: 0x{_saved.Tag:x}, bytesSent: {n}");
        SendSavedLocked(req);
        req.AckAwaited = true;
        req.Retries = 0;
        req.Deadline = SyncedClock + ReadTimeoutTicks;
        if (req.Offset >= req.Data.Length) req.MoreData = false;
    }

    /// <summary>+0x15C: written only by the constructor (0, 0x006428E0) and by BehaviorFactoryTest, which this stack does not run.</summary>
    private const bool FactoryWritesAllowed = false;

    // fidelity: M3-043
    /// <summary>GetMaxSizeForEntryTag (0x00643FC8..0x0064404E) with its warning (WA10): an exact key other than 0x198000, else the warning "0x%x" and 0.</summary>
    private int MaxSizeForEntryTagLogged(uint tag)
    {
        if (tag == 0x198000 || !MaxSizeTable.ContainsKey(tag))
        {
            _log.Add($"warning: NVStorageComponent.GetMaxSizeForEntryTag.InvalidTag: 0x{tag:x}");
            return 0;
        }
        return MaxSizeTable[tag];
    }

    // fidelity: M3-031, M3-027
    /// <summary>
    /// ResendLastCommand (0x00645C54..0x00645D9A): the counter (+0xF4) is incremented first and compared as a u8 against
    /// the limit (+0xF5 = 8, 0x006428B8). At the limit it logs NumRetriesExceeded (sErrorF, then the error-flag store and
    /// the debug-break gate, 0x00645D62..0x00645D76) and returns false. Otherwise it logs Retry (info) with the saved
    /// tag and op, and sends the saved header with a copy of the saved Data (WD9: the same command for a READ, a chunk,
    /// an ERASE or a WIPEALL), and returns true.
    /// </summary>
    private bool ResendLastCommandLocked(PendingRequest req)
    {
        req.Retries = (byte)(req.Retries + 1);
        if (req.Retries >= RetryLimit)
        {
            _log.Add($"error: NVStorageComponent.ResendLastCommand.NumRetriesExceeded: Tag: 0x{_saved.Tag:x}, Op: {NvOpName(_saved.Op)}, Attempts: {RetryLimit}");
            Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak();
            return false;
        }
        _log.Add($"info: NVStorageComponent.ResendLastCommand.Retry: Tag: 0x{_saved.Tag:x}, Op: {NvOpName(_saved.Op)}, Attempt: {req.Retries}");
        _robot.SendMessage(_saved.ToCommand(), flush: true);       // the saved header and a copy of the saved Data, for every op
        return true;
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
    // fidelity: M3-040
    public void ProcessOnIdle()
    {
        // Native dispatch has one engine executor. A competing host caller only
        // appends; it must not invoke the same front concurrently or block a callback
        // waiting for that caller. Monitor permits the native same-thread reentry.
        if (!Monitor.TryEnter(_idleExecution)) return;
        try { ProcessOnIdleCore(); }
        finally { Monitor.Exit(_idleExecution); }
    }

    private void ProcessOnIdleCore()
    {
        lock (_gate)
        {
            if (_inFlight is not null || _queue.Count != 0 || _onIdle.Count == 0) return;
        }
        // 00645B08..00645BAE: test NV state/queue once, invoke the front before pop,
        // and keep draining even if a callback enqueues NV work. An exception leaves
        // the front intact. AddOneShot can reenter this loop; there is no native guard.
        while (true)
        {
            Action callback;
            lock (_gate)
            {
                if (_onIdle.Count == 0) return;
                callback = _onIdle[0];
                _log.Add("debug: NVStorageComponent.ProcessOnIdleCallbacks.ProcessingCallback: ");
            }
            callback();
            lock (_gate)
            {
                _onIdle.RemoveAt(0);
            }
        }
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
        PendingRequest? timeoutRequest = null;
        bool runOnIdle = false;
        lock (_gate)
        {
            if (_inFlight is { } req)
            {
                if (req.Op == OpRead)
                {
                    // State 2 (read pending): only the timeout check.
                    if (req.Deadline is { } deadline && SyncedClock > deadline)
                    {
                        _log.Add($"warning: NVStorageComponent.Update.ReadTimeout: Tag: 0x{req.Tag:x}");
                        timeoutRequest = req;
                        timeoutCallback = req.Callback;
                        timeoutResult = new NvResult(-4, Array.Empty<byte>());
                    }
                }
                else if (req.AckAwaited)
                {
                    // fidelity: M3-043
                    // State 1 with an ack awaited (WC1..WC4, 0x006456F4..0x00645758): the clock strictly past the deadline warns WriteTimeout
                    // with +0x20, calls the callback with -4, then SetState(0). No retry, no broadcast, no backup, no error flag.
                    if (SyncedClock > req.Deadline)
                    {
                        _log.Add($"warning: NVStorageComponent.Update.WriteTimeout: Tag: 0x{req.AckTag:x}");
                        timeoutRequest = req;
                        timeoutCallback = req.Callback;
                        timeoutResult = new NvResult(-4, Array.Empty<byte>());
                    }
                }
                else if (!req.MoreData)
                {
                    // WB14 (0x006459A2..0x006459DE): nothing is left to send.
                    _log.Add("warning: NVStorageComponent.Update.NoDataToWrite: ");
                    SetStateLocked(0);
                    _inFlight = null;
                }
                else SendChunkLocked(req);                  // one chunk per Update call
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
        if (timeoutRequest is not null)
            lock (_gate)
                if (ReferenceEquals(_inFlight, timeoutRequest)) { SetStateLocked(0); _inFlight = null; }
        if (runOnIdle) ProcessOnIdle();
    }

    /// <summary>
    /// A disconnect discards the queue and the in-flight request without invoking any read callback or timeout, and
    /// drops the pending on-idle callbacks (M3-035; the robot they belonged to is gone). The in-flight request's
    /// deadline and retry state die with it, so no local timer can fire afterwards.
    /// </summary>
    public void OnDisconnected()
    {
        // fidelity: M3-038
        // 00643F22..00643F6A: idle functions, queued requests, then active request
        // functions are destroyed without invocation. Managed references are released
        // in that same order; backup-manager destruction remains M15 ownership.
        // fidelity: M3-027
        // N12 (0x00643F26..0x00643F44): the request deque is destroyed, then the saved Data (+0xE8) is freed, before the backup
        // manager's destructor (M15). A later robot's component is built afresh, so its saved Data starts null again (N1).
        lock (_gate) { DestroyLocked(); ConstructedSetStateLocked(); }
    }

    // The destructor's part only: ~NVStorageComponent (0x00643F22..0x00643F6A) calls no SetState; the constructor's
    // SetState(0) line belongs to the next robot's component, which OnDisconnected stands in for.
    private void DestroyLocked() { _onIdle.Clear(); _queue.Clear(); _inFlight = null; ResetSavedToConstructed(); _readTag = 0x198000; _readPending = false; }

    private void OnMessage(RobotMessage m) { if (m is NVOpResult r) OnResult(r); }

    private readonly record struct Completion(Action<NvResult>? Callback, NvResult Result, List<NVStorageOpResult>? Broadcasts, bool ReadCallback, uint Tag, PendingRequest Request, uint NameTag, byte Op);

    private void OnResult(NVOpResult r)
    {
        Completion? completion;
        lock (_gate)
        {
            // fidelity: M3-043
            // WD1/WD2 (0x00642FCC..0x00643048): every ack logs Recvd; a WIPEALL ack (op 3) is normalised to 0x198000 whatever its tag, any other
            // op to GetBaseEntryTag(tag). Ops 1..3 are accepted only while an ack is awaited for that normalised tag (WD3).
            _log.Add($"debug: NVStorageComponent.HandleNVOpResult.Recvd: Tag: 0x{r.Tag:x}, Op: {NvOpName(r.Op) ?? "(null)"}, Result: {NvResultName(r.Result) ?? "(null)"}");
            uint r8 = r.Op == OpWipeAll ? 0x198000u : r.Tag;
            uint baseTag = r.Op == OpWipeAll ? 0x198000u : GetBaseEntryTag(r.Tag, _log.Add);
            if (r.Op is >= OpWrite and <= OpWipeAll)
            {
                var wc = WriteAckLocked(r, baseTag, r8);
                if (wc is null) return;
                completion = wc;
                goto delivered;
            }
            if (r.Op != OpRead)
            {
                _log.Add($"warning: NVStorageComponent.HandleNVOpResult.UnhandledOperation: {NvOpName(r.Op) ?? "(null)"}");
                return;
            }
            // M3-025/M3-026/M3-028 acceptance gate (0x006430BA..0x006430CE): a read reply is taken only when +0x78 (a read was
            // dispatched and SetState(0) has not run since) is non-zero and +0x50 (the dispatched read's tag, left stale by
            // SetState(0)) equals the reply's base tag; otherwise the sWarningF at 0x0064318E and nothing else (0x00643192 -> 0x00643238).
            if (!_readPending || _readTag != baseTag)
            {
                _log.Add($"warning: NVStorageComponent.HandleNVOpResult.AckdTagNeverRequested: Tag recvd: 0x{r8:x}, BaseTag: 0x{baseTag:x}, " +
                         $"ExpectedBaseTag: 0x{_readTag:x} (pending {(_readPending ? 1 : 0)}), BlobSize: {(uint)r.Data.Length}, result: {NvResultName(r.Result) ?? "(null)"}");
                return;
            }
            var req = _inFlight!;                             // +0x78 is set only by the read dispatch and cleared by SetState(0), which also clears _inFlight
            sbyte result = r.Result;
            // fidelity: M3-030
            // R1 (0x00643024..0x00643048): the normalised response tag is 0x198000 for a WIPEALL response (op 3), else
            // GetBaseEntryTag of the response tag; the callback's log names it.
            req.ResponseBaseTag = baseTag;

            // fidelity: M15-014
            // Appendix I3 (0x00642F8C..0x00643937): op 0 takes the read header/reassembly path; ops 1-3 (WRITE,
            // ERASE, WIPEALL) take the write terminal (0x00643054..0x00643424). The write terminal completes with
            // the result byte, delivering 0 for a successful write.
            if (result <= -1)
            {
                // fidelity: M3-031
                // M3-031 (0x006431E6..0x00643234): only {-8,-7,-5,-4} are retried; -6 and -1 are not. On a retry
                // ResendLastCommand (0x00645C54) logs Retry and sends, then the caller logs ResentFailedRead; when
                // the counter reaches +0xF5 = 8 it logs NumRetriesExceeded and returns 0, and the caller then logs
                // ReadOpFailed for every negative result (0x006434E4).
                if (IsRetryableResult(result) && ResendLastCommandLocked(req))
                {
                    _log.Add($"info: NVStorageComponent.HandleNVOpResult.ResentFailedRead: Tag 0x{r.Tag:x} resent due to {NvResultName(result)}");
                    return;
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
                        _log.Add($"warning: NVStorageComponent.HandleNVOpResult.TooLittleReadData: Tag 0x{r.Tag:x}, Got {r.Data.Length}, Expected 1024");   // Expected is the literal 0x400 (0x00643440), not the 0x10 size gate (0x006430F2)
                        req.Clear();                                  // 0x643478: clear the pending buffer before -3
                        result = -3;
                    }
                    else if (BitConverter.ToUInt32(r.Data, 0) != NonFactoryHeaderMagic)
                    {
                        // 0x00643112 bne -> 0x0064348A: sChanneledDebugF, channel NVStorage (0xBFB43A), key 0xBFB993, format
                        // 0xBFB9C5; args the reply tag, the u32 at blob+0, 0x435A4D4F. Result -1 (0x006434DC).
                        _log.Add($"debug: NVStorageComponent.HandleNVOpResult.InvalidHeader: Tag: 0x{r.Tag:x}, Got 0x{BitConverter.ToUInt32(r.Data, 0):x}, Expected 0x{NonFactoryHeaderMagic:x}");
                        result = -1;
                    }
                    else
                    {
                        uint total = BitConverter.ToUInt32(r.Data, 8);
                        int max = MaxSizeForEntryTag(req.Tag);
                        if (total > (uint)(max - NvHeaderSize))
                        {
                            _log.Add($"warning: NVStorageComponent.HandleNVOpResult.InvalidDataSize: Tag 0x{r.Tag:x}, size {total}, maxSizeAllowed {max}");
                            result = -1;
                        }
                        else
                        {
                            req.HeaderAccepted = true;
                            req.HeaderTotal = (int)total;
                            if (total > (uint)(r.Data.Length - NvHeaderSize))
                            {
                                // M3-028: the rest is on the robot; re-request it, reliable and not hot, with no re-arm.
                                _log.Add($"debug: NVStorageComponent.HandleNVOpResult.ReadingRestOfData: Tag: 0x{r.Tag:x}, TotalSize: {total}");
                                // fidelity: M3-027
                                // N10 (0x00643888..0x006438BC): the saved tag (+0xDC) becomes the response tag, the op (+0xE4)
                                // 0 and the Length (+0xE0) total + 16; the command is the saved header with a copy of the
                                // saved Data, which a reply never repopulates. A later resend sends this command again.
                                _saved.Tag = r.Tag;
                                _saved.Op = OpRead;
                                _saved.Length = (int)total + NvHeaderSize;
                                _robot.SendMessage(_saved.ToCommand(), flush: true);
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
            LogReadResult(r, baseTag, result);
        }
    delivered:
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
        string baseName = NvEntryTagName(baseTag) ?? "(null)";   // NULL %s (unreachable: every valid tag is named, 0x007CEE38)
        if (result == 0)
            _log.Add($"info: NVStorageComponent.HandleNVOpResult.ReadSuccess: BaseTag: {baseName}, result: {NvResultName(0)}");
        else if (result == -1)
            _log.Add($"info: NVStorageComponent.HandleNVOpResult.ReadEntryNotFound: BaseTag: {baseName}, Tag: 0x{r.Tag:x}, result: {NvResultName(-1)}");
        else
            _log.Add($"warning: NVStorageComponent.HandleNVOpResult.ReadFailed: BaseTag: {baseName}, result: {NvResultName(result)}");
    }

    // fidelity: M3-031
    private static bool IsRetryableResult(sbyte result) => result is -8 or -7 or -5 or -4;

    // fidelity: M3-043
    /// <summary>
    /// A WRITE, ERASE or WIPEALL ack (WD3..WD8, 0x0064304E..0x00643362). It is accepted only while an ack is awaited (+0x48) for the normalised
    /// tag (+0x20); otherwise it warns AckdTagBaseTagWasNeverSent and changes nothing. A result of 0 or more clears the await and, while chunks
    /// remain, returns without a log or callback (the next Update sends the next chunk). A negative result in {-8, -7, -5, -4} resends the saved
    /// command (ResentFailedWrite); any other, or an exhausted counter, logs WriteOpFailed, clears the await and drops the remaining chunks.
    /// The final ack logs WriteFailed (result != 0) or WriteSuccess, then delivers: broadcast, callback, backup, SetState(0).
    /// </summary>
    private Completion? WriteAckLocked(NVOpResult r, uint sb, uint r8)
    {
        var req = _inFlight;
        if (req is null || req.Op == OpRead || !req.AckAwaited || req.AckTag != sb)
        {
            _log.Add($"warning: NVStorageComponent.HandleNVOpResult.AckdTagBaseTagWasNeverSent: BaseTag: 0x{sb:x}, Tag: 0x{r8:x}");
            return null;
        }
        sbyte result = r.Result;
        req.ResponseBaseTag = sb;
        if (result <= -1)
        {
            if (IsRetryableResult(result) && ResendLastCommandLocked(req))
            {
                _log.Add($"info: NVStorageComponent.HandleNVOpResult.ResentFailedWrite: Tag 0x{r8:x} resent due to {NvResultName(result)}, op: {NvOpName(r.Op)}");
                return null;
            }
            _log.Add($"warning: NVStorageComponent.HandleNVOpResult.WriteOpFailed: Tag: 0x{r8:x}, op: {NvOpName(r.Op)}, result: {NvResultName(result)}");
            req.AckAwaited = false;
            req.MoreData = false;
        }
        else
        {
            req.AckAwaited = false;
            if (req.MoreData) return null;
        }
        string fp = TagNameOrUnreachable(sb);
        if (result != 0)
            _log.Add($"warning: NVStorageComponent.HandleNVOpResult.WriteFailed: BaseTag: {fp}, lastTag: 0x{r8:x}, op: {NvOpName(r.Op)}, result: {NvResultName(result)}");
        else
            _log.Add($"info: NVStorageComponent.HandleNVOpResult.WriteSuccess: BaseTag: {fp}, lastTag: 0x{r8:x}, op: {NvOpName(r.Op)}, result: {NvResultName(result)}");
        return CompleteLocked(req, result, Array.Empty<byte>(), r.Op);
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
    private Completion CompleteLocked(PendingRequest req, sbyte result, byte[] data, byte? messageOp = null)
    {
        byte op = messageOp ?? req.Op;
        // R1 (0x00643042): the broadcast tag (sb) and the callback's name are the normalised response tag.
        uint nameTag = req.ResponseBaseTag ?? req.Tag;
        List<NVStorageOpResult>? broadcasts = null;
        if (req.Broadcast)
        {
            // fidelity: M3-031
            // W2 (0x00643384..0x0064339E): a WRITE, ERASE or WIPEALL completion with the broadcast flag (+0x40) calls
            // BroadcastNVStorageOpResult once, whatever the result's sign: (tag, result, op, index 0, no data, size 0).
            broadcasts = req.Op == OpRead
                ? BuildBroadcasts(nameTag, req.Op, result, data)
                : new List<NVStorageOpResult> { new(nameTag, op, result, 0, Array.Empty<byte>()) };
        }
        return new Completion(req.Callback, new NvResult(result, data), broadcasts, req.Op == OpRead, req.Tag, req, nameTag, op);
    }

    private void Deliver(Completion c)
    {
        if (!c.ReadCallback)
        {
            // fidelity: M3-031
            // W2 (0x00643384..0x00643420), in the engine's order: the broadcast (gated on +0x40), then the callback (gated on
            // +0x38) behind its debug log (0x006433C6, via 0x004A5B84, "%s" with the normalised tag's name), then the
            // backup call (WipeAll for op 3, else WriteDataForTag(tag, result, op == 1)), then SetState(0).
            if (c.Broadcasts is not null)
                foreach (var b in c.Broadcasts) NVStorageOpResultBroadcast?.Invoke(b);
            if (c.Callback is not null)
            {
                lock (_gate) _log.Add($"debug: NVStorageComponent.HandleNVOpResult.ExecutingWriteCallback: {TagNameOrUnreachable(c.NameTag)}");
                c.Callback.Invoke(c.Result);
            }
            lock (_gate)
                _log.Add(c.Op == OpWipeAll
                    ? "MISSING: RobotDataBackupManager::WipeAll (M15 recipient, 0x00643406): the backup manager is not built"
                    : $"MISSING: RobotDataBackupManager::WriteDataForTag(0x{c.NameTag:x}, {c.Result.Result}, {c.Op == OpWrite}) (M15 recipient, 0x00643418): the backup manager is not built");
        }
        else
        {
            // M3-030 R2 / 0x006436B6..0x00643714: only a present read callback logs this,
            // after the outcome log and immediately before invoking the callback.
            if (c.Callback is not null)
            {
                lock (_gate) _log.Add($"debug: NVStorageComponent.HandleNVOpResult.ExecutingReadCallback: {TagNameOrUnreachable(c.NameTag)}");
            }
            c.Callback?.Invoke(c.Result);
            if (c.Broadcasts is not null)
                foreach (var b in c.Broadcasts) NVStorageOpResultBroadcast?.Invoke(b);
        }
        // SetState(0) is after callback/broadcast, not before: 006437EA..006437EE.
        lock (_gate)
            if (ReferenceEquals(_inFlight, c.Request)) { SetStateLocked(0); _inFlight = null; }
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
    /// <summary>
    /// R6 (0x006441F8..0x00644340, 0x007CEE38..0x007CF0A8): the normalised tag is a key of the two initialised tables
    /// (R4, R5) or the sentinel 0x198000, and every one of those has a non-null name in EnumToString, so the callback's
    /// "%s" is never null. A null here would mean a tag the engine cannot produce.
    /// </summary>
    private static string TagNameOrUnreachable(uint tag) =>
        NvEntryTagName(tag) ?? throw new InvalidOperationException($"M3-030 R6: no name for the normalised tag 0x{tag:x}, which the engine cannot produce");

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
                Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak();       // 0x006437BE..0x006437D2: _errG, then the debug-break gate
                return chunks;
            }
        }
    }

    /// <summary>
    /// M3-030: one engine-to-game <c>BroadcastNVStorageOpResult</c> chunk, sent when a request's broadcast flag is
    /// set. The stack models game broadcasts as events, as the other M3 broadcasts do.
    /// </summary>
    public event Action<NVStorageOpResult>? NVStorageOpResultBroadcast;

    // fidelity: M3-038
    public void Dispose()
    {
        _robot.Message -= OnMessage;
        lock (_gate) DestroyLocked();
    }
}

/// <summary>The terminal result of one NV request: the NVResult and the assembled bytes (empty on an error).</summary>
public readonly record struct NvResult(sbyte Result, byte[] Data);

/// <summary>M3-030: one chunk of <c>BroadcastNVStorageOpResult</c> (tag, op, result, word@4 index, data).</summary>
public readonly record struct NVStorageOpResult(uint Tag, byte Op, sbyte Result, int Index, byte[] Data);
