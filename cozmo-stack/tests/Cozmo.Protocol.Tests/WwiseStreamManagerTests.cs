using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M6-025 (C33.2, C33.4): the stream manager, the device, CreateAuto, the auto stream and the I/O seam. Every expected value is the engine's own output, produced by
/// re-analysis/tools/emu/emu_stream.py (scenarios create, stream, position, heur, nominal: the real CreateAuto 0x95F334, the device 0x960F34 / 0x9664B8, the stream's constructor and 0x964730 / 0x964590,
/// the deferred open 0x96382C / 0x963890, and the stream methods run under Unicorn, with a stand-in resolver and hook, the mutex and pool calls stood in, and each transfer completed through the real
/// 0x966644); none comes from running this implementation. A test names the scenario (s0.., p.., heur_.., create_..) and the addresses it checks.
/// </summary>
public class WwiseStreamManagerTests
{
    private static string Nodes(WwiseAutoStream s) => string.Join(";", s.Nodes.Select(n => $"{n.Buffer!.Start}/{n.Buffer.Size}/{n.Offset}/{n.State & 7}"));

    private static string D(WwiseAutoStream s) =>
        $"b2d={s.Flags2D} b6c={s.Granted6C} b6e={s.Flags6E} f48={s.LoopStart48} f4c={s.LoopEnd4C} f54={s.Buffered54} p38={s.Position38} nodes=[{Nodes(s)}]";

    // ------------------------------------------------------------------ CreateAuto, the device (create_*)

    [Fact]
    public void C1_CreateAutoByIdDeferredBuildsTheStreamAndItsOpenRecord()
    {
        // emu_stream.py create_plain: result 1; b2d = 5 (bit 0 set by the constructor; bit 2 set by 0x964730 because the deferred descriptor has size 0, 0x964860..0x964878); block size 1 ([S+0x28], [S+0x5C]);
        // throughput = the engine's float bits 0x3F800000 (1.0f); loop start/end 0 (min(fileSize 0, loopEnd 0)); [S+0x50] = 0x4000 (granularity - granularity % 1), [S+0x58] = 1; the record: id 77, mode 0,
        // [+0x24] = 2 (bit 0 clear = by id, bit 1 set = flags given); flags +0x12 set to 1 by CreateAuto (0x95F380); the cache id -1 from the flags.
        var rig = new StreamRig();
        WwiseAutoStream? s = null;
        var flags = new WwiseFileFlags { CodecId = 1, LanguageSpecific = 1, Byte11 = 1, CacheId = 0xFFFFFFFF };
        Assert.Equal(1, rig.Manager.CreateAutoById(77, flags, StreamRig.Heur(0x3F800000), null, ref s, false));
        Assert.NotNull(s);
        Assert.Equal(1, flags.Byte12);
        Assert.Equal((byte)5, s!.Flags2D);
        Assert.Equal((1u, 1u, 0x4000u, 1u), (s.BlockSize28, s.BlockSize5C, s.BufferSize50, s.MinBuffer58));
        Assert.Equal(0x3F800000u, BitConverter.SingleToUInt32Bits(s.Throughput44));
        Assert.Equal((0u, 0u, 0xFFFFFFFFu, (byte)1, (byte)0), (s.LoopStart48, s.LoopEnd4C, s.CacheId40, s.Byte6D, s.Priority2C));
        Assert.Equal((0UL, 0u, 0u), (s.Position38, s.Buffered54, 0u));
        Assert.Equal((byte)0, s.Flags6E);
        var rec = s.Record0C!;
        Assert.Equal((77u, 0u, true), (rec.FileId, rec.Mode, rec.Flags is not null));
        Assert.Equal(1u, rec.Flags!.CodecId);
        Assert.Equal(1, rec.Flags.LanguageSpecific);
        Assert.Same(s, rig.Device.Streams[0]);                                                              // linked at the head of the device list (0x962A58..0x962A80)
    }

    [Fact]
    public void C2_TheHeuristicsAreClampedAndCopiedAsTheOracleShows()
    {
        // emu_stream.py create_throughput_below_one: throughput 0.5f (0x3F000000) -> stored 0x3F800000 (vmovle, 0x9647F4); priority 50 -> b2c = 50.
        // create_loop_range: throughput 0x40000000, loop start 123, loop end 600, min buffers 3, priority 0x64 -> f44 = 0x40000000, f48 = 123 (aligned to the block size 1), f4c = 0 (min(file size 0, 600),
        // 0x964814..0x96482C), b6d = 3, b2c = 100. create_loop_end_past_file: codec 0, loop end 5000 -> f4c = 0.
        var rig = new StreamRig();
        var s = rig.Create(77, StreamRig.Heur(0x3F000000, 0, 0, 0, 50));
        Assert.Equal((0x3F800000u, (byte)50), (BitConverter.SingleToUInt32Bits(s.Throughput44), s.Priority2C));
        s = rig.Create(77, StreamRig.Heur(0x40000000, 123, 600, 3, 0x64));
        Assert.Equal((0x40000000u, 123u, 0u, (byte)3, (byte)100), (BitConverter.SingleToUInt32Bits(s.Throughput44), s.LoopStart48, s.LoopEnd4C, s.Byte6D, s.Priority2C));
        var h = new WwiseStreamHeuristics();
        s.GetHeuristics961664(h);                                                                           // vt+0x14 (0x961664): throughput, loop start, loop end, [S+0x6D], [S+0x2C]
        Assert.Equal((0x40000000u, 123u, 0u, (byte)3, (byte)100), (BitConverter.SingleToUInt32Bits(h.Throughput), h.LoopStart, h.LoopEnd, h.MinNumBuffers, h.Priority));
        s = rig.Create(77, StreamRig.Heur(0x40400000, 0, 5000, 0, 1), flags: new WwiseFileFlags { CodecId = 0 });
        Assert.Equal((0x40400000u, 0u, (byte)1), (BitConverter.SingleToUInt32Bits(s.Throughput44), s.LoopEnd4C, s.Priority2C));
        // create_priority_too_high / create_negative_throughput: result 31 (0x1F), no stream, flags +0x12 untouched (0x95F340..0x95F35C)
        WwiseAutoStream? none = null;
        var f = new WwiseFileFlags { CodecId = 1 };
        Assert.Equal(0x1F, rig.Manager.CreateAutoById(77, f, StreamRig.Heur(0x3F800000, 0, 0, 0, 0x65), null, ref none, false));
        Assert.Equal(0x1F, rig.Manager.CreateAutoById(77, f, StreamRig.Heur(0xBF800000), null, ref none, false));
        Assert.Null(none);
        Assert.Equal(0, f.Byte12);
    }

    [Theory]
    [InlineData(0u, 0x800u, 0u, 1, 0x4000u, 0x800u)]       // create_buffer_0x800: [S+0x58] = 0x800
    [InlineData(0x4000u, 0u, 0u, 1, 0x4000u, 0x4000u)]     // create_buffer_explicit_4000: [S+0x50] = [S+0x58] = 0x4000
    [InlineData(0x4001u, 0u, 0u, 2, 0u, 0u)]               // create_buffer_explicit_not_multiple: 2 (0x4001 > granularity: the explicit size must not exceed it)
    [InlineData(0x8000u, 0u, 0u, 2, 0u, 0u)]               // create_buffer_explicit_too_large: 2
    public void C3_TheBufferGeometryFollowsTheOraclesRules(uint size, uint min, uint block, int expected, uint size50, uint min58)
    {
        // M6-025 D5 (0x964590..0x964730): emu_stream.py create_buffer_*.
        var rig = new StreamRig();
        WwiseAutoStream? s = null;
        int r = rig.Manager.CreateAutoById(78, new WwiseFileFlags { CodecId = 1 }, StreamRig.Heur(0x3F800000), new WwiseStreamBufferSettings { BufferSize = size, MinBufferSize = min, BlockSize = block }, ref s, false);
        Assert.Equal(expected, r == 1 ? 1 : r);
        if (expected == 1)
        {
            Assert.Equal((size50, min58), (s!.BufferSize50, s.MinBuffer58));
        }
        else Assert.Null(s);
    }

    [Fact]
    public void C4_AZeroSizeFileSetsTheEofBitAndASyncOpenSkipsTheRecord()
    {
        // create_zero_size_file: result 1 (the stream is created; bit 2 is set for size 0 either way); create_sync_open (the resolver reports the size, bSyncOpen = 1): result 1, b2d = 0x11
        // (bit 0, bit 4: [S+0x2D] |= 0x10, 0x95F4C0..0x95F4CC), no record ([S+0xC] = 0), the resolver opened id 80.
        var rig = new StreamRig(fileSize: 0);
        Assert.Equal((byte)5, rig.Create().Flags2D);
        var sync = new StreamRig(fileSize: 1000, defer: false);
        WwiseAutoStream? s = null;
        Assert.Equal(1, sync.Manager.CreateAutoById(80, new WwiseFileFlags { CodecId = 1 }, StreamRig.Heur(0x3F800000), null, ref s, true));
        Assert.Equal((byte)0x11, s!.Flags2D);
        Assert.Null(s.Record0C);
        Assert.Equal(new[] { "id:80" }, sync.Resolver.Opens);
        Assert.Equal(1000L, s.FileDesc10!.FileSize);
    }

    [Fact]
    public void C5_CreateAutoByNameUsesTheNameAndRefusesANullName()
    {
        // M6-025 D3 by name (0x95F0E8..0x95F31C): a null name returns 0x1F first (0x95F0EC cmp r1,#0; beq 0x95F118); the resolver's vt+8 gets the name; the record is built by 0x960A08 (a copy of the name).
        var rig = new StreamRig(defer: false);
        WwiseAutoStream? s = null;
        Assert.Equal(0x1F, rig.Manager.CreateAutoByName(null, new WwiseFileFlags(), StreamRig.Heur(0x3F800000), null, ref s, false));
        Assert.Equal(1, rig.Manager.CreateAutoByName("snd.wem", new WwiseFileFlags { CodecId = 1 }, StreamRig.Heur(0x3F800000), null, ref s, true));
        Assert.Equal(new[] { "name:snd.wem" }, rig.Resolver.Opens);
        var deferred = new StreamRig();
        Assert.Equal(1, deferred.Manager.CreateAutoByName("snd.wem", new WwiseFileFlags { CodecId = 1 }, StreamRig.Heur(0x3F800000), null, ref s, false));
        Assert.Equal("snd.wem", s!.Record0C!.Name);
    }

    [Fact]
    public void C6_CreateAutoFailuresFollowTheCodes()
    {
        // M6-025 D3 (0x95F3E4..0x95F4FC, verifier correction 5): a resolver result other than 1 gives 0x42 when it was 0x42, else 2; a synchronous open with iFileSize < 1 gives 2 with NO hook close; a device index
        // out of range or empty gives 2; the allocation failure of the descriptor gives 2; a stream creation failure with a synchronous open closes the file through the hook (0x95F4F0..0x95F500).
        var rig = new StreamRig();
        WwiseAutoStream? s = null;
        var failing = new FailingResolver(0x42);
        rig.Manager.Resolver = failing;
        Assert.Equal(0x42, rig.Manager.CreateAutoById(1, new WwiseFileFlags(), StreamRig.Heur(0x3F800000), null, ref s, false));
        failing.Code = 9;
        Assert.Equal(2, rig.Manager.CreateAutoById(1, new WwiseFileFlags(), StreamRig.Heur(0x3F800000), null, ref s, false));
        rig.Manager.Resolver = rig.Resolver;
        rig.Resolver.DeviceId = 5;                                                                          // [fileDesc+0x18] beyond the device count (unsigned bound)
        Assert.Equal(2, rig.Manager.CreateAutoById(1, new WwiseFileFlags(), StreamRig.Heur(0x3F800000), null, ref s, false));
        rig.Resolver.DeviceId = 0;
        rig.Manager.AllocationFails = () => true;
        Assert.Equal(2, rig.Manager.CreateAutoById(1, new WwiseFileFlags(), StreamRig.Heur(0x3F800000), null, ref s, false));
        rig.Manager.AllocationFails = null;
        var sync = new StreamRig(fileSize: 0, defer: false);
        var hook = sync.Resolver;
        Assert.Equal(2, sync.Manager.CreateAutoById(1, new WwiseFileFlags(), StreamRig.Heur(0x3F800000), null, ref s, true));   // iFileSize < 1 with bSyncOpen
        Assert.Null(s);
    }

    private sealed class FailingResolver : IWwiseFileLocationResolver
    {
        public int Code;
        public FailingResolver(int code) => Code = code;
        public int OpenById(uint id, uint mode, WwiseFileFlags? flags, ref bool bSyncOpen, WwiseFileDesc fileDesc) => Code;
        public int OpenByName(string name, uint mode, WwiseFileFlags? flags, ref bool bSyncOpen, WwiseFileDesc fileDesc) => Code;
    }

    // ------------------------------------------------------------------ the stream methods (stream scenario)

    [Fact]
    public void S1_QueryAndGetBufferBeforeTheOpenGiveTheOraclesCodesAndLeaveTheOutWordAlone()
    {
        // emu_stream.py s1_query_unopened: result 0x2E with *out untouched (0x961C58 moveq r8,#0x2e before the first write 0x961C70); s1_getbuffer_unopened: result 0x11 (bit 2 is set by the zero-size
        // descriptor and [S+0x38] 0 >= size 0, 0x965D7C..0x965D98), buf 0, size 0.
        var rig = new StreamRig();
        var s = rig.Create();
        var (r, word) = rig.Query(s);
        Assert.Equal((0x2E, 0xCAFEF00Du), (r, word));
        Assert.Equal(0x11, rig.GetBuffer(s, out var data, out int _, out uint size));
        Assert.Null(data);
        Assert.Equal(0u, size);
        Assert.Equal("b2d=5 b6c=0 b6e=0 f48=0 f4c=0 f54=0 p38=0 nodes=[]", D(s));
    }

    [Fact]
    public void S2_StartReadiesTheStreamAndTheOpenCompletesIt()
    {
        // emu_stream.py s2_start: result 1; b2d = 161 (bit 0, bit 5 starved, bit 7 ready: 0x964D00..0x964D0C then 0x964B8C clears bit 2 because the file is not opened, sets bit 7, and the buffered 0 is below the nominal 380,
        // 0x964C60); b6e = 5 (started, bit 2); a second Start returns 1 (0x964CBC..0x964CD0). s3_open: the real 0x963890 gives result 1, b2d = 177 (bit 4 opened, 0x961A84), the record is freed.
        var rig = new StreamRig();
        var s = rig.Create();
        Assert.Equal(1, s.Start964CB8());
        Assert.Equal("b2d=161 b6c=0 b6e=5 f48=0 f4c=0 f54=0 p38=0 nodes=[]", D(s));
        Assert.Equal(1, s.Start964CB8());
        Assert.Equal(1, s.RunOpen963890());
        Assert.Equal("b2d=177 b6c=0 b6e=5 f48=0 f4c=0 f54=0 p38=0 nodes=[]", D(s));
        Assert.Null(s.Record0C);
        var (r, word) = rig.Query(s);
        Assert.Equal((0x2E, 0u), (r, word));                                                                // s4_query_opened_empty: 0x2E, out 0
        Assert.Equal(0x2E, rig.GetBuffer(s, out _, out _, out _));                                          // s4_getbuffer_opened_empty
    }

    [Fact]
    public void S3_DeliveriesGetBuffersAndReleasesFollowTheOracleStepByStep()
    {
        var rig = new StreamRig();
        var s = rig.Create();
        s.Start964CB8();
        s.RunOpen963890();
        // s5: the engine's completion 0x966644 appends the record as state 3; [S+0x54] counts the buffered bytes; bit 5 clears once 700 >= the nominal 380 (0x964C60: bhs)
        Assert.True(rig.Deliver(s, 0, 300));
        Assert.Equal("b2d=177 b6c=0 b6e=5 f48=0 f4c=0 f54=300 p38=0 nodes=[0/300/0/3]", D(s));
        Assert.True(rig.Deliver(s, 300, 400));
        Assert.Equal("b2d=145 b6c=0 b6e=5 f48=0 f4c=0 f54=700 p38=0 nodes=[0/300/0/3;300/400/0/3]", D(s));
        // s6: 0x11 with the out word 700 (bit 5 clear and 700 >= [S+0x54], 0x961CC8..0x961CE0)
        var (q, word) = rig.Query(s);
        Assert.Equal((0x11, 700u), (q, word));
        // s7: GetBuffer grants them in order: 0x2D, the size is buffer.size - offset, [S+0x54] is reduced, bit 5 sets again when it drops below the nominal
        Assert.Equal(0x2D, rig.GetBuffer(s, out var d1, out int o1, out uint z1));
        Assert.Equal((0, 300u), (o1, z1));
        Assert.Equal("b2d=145 b6c=1 b6e=5 f48=0 f4c=0 f54=400 p38=300 nodes=[0/300/0/3;300/400/0/3]", D(s));
        Assert.Equal(0x2D, rig.GetBuffer(s, out var d2, out int o2, out uint z2));
        Assert.Equal((0, 400u), (o2, z2));
        Assert.Equal("b2d=177 b6c=2 b6e=5 f48=0 f4c=0 f54=0 p38=700 nodes=[0/300/0/3;300/400/0/3]", D(s));
        Assert.Equal(StreamTestPattern(0, 300), d1![..300]);                                                // the bytes are the buffers' own
        Assert.Equal(StreamTestPattern(300, 400), d2!);
        Assert.Equal(0x2E, rig.GetBuffer(s, out var none, out _, out uint zNone));                          // s7_getbuffer_none_left
        Assert.Null(none);
        Assert.Equal(0u, zNone);
        // s8: ReleaseBuffer pops the head; 2 with nothing granted
        Assert.Equal(1, s.ReleaseBuffer964D64());
        Assert.Equal("b2d=177 b6c=1 b6e=5 f48=0 f4c=0 f54=0 p38=700 nodes=[300/400/0/3]", D(s));
        Assert.Equal(1, s.ReleaseBuffer964D64());
        Assert.Equal("b2d=177 b6c=0 b6e=5 f48=0 f4c=0 f54=0 p38=700 nodes=[]", D(s));
        Assert.Equal(2, s.ReleaseBuffer964D64());
        // s9: the last buffer reaches the end of the file: 0x11 with the data, bit 2 set, bit 7 cleared (b2d = 21), and 0x11 again afterwards
        rig.Deliver(s, 700, 300);
        Assert.Equal(0x11, rig.GetBuffer(s, out _, out _, out uint z3));
        Assert.Equal(300u, z3);
        Assert.Equal("b2d=21 b6c=1 b6e=5 f48=0 f4c=0 f54=0 p38=1000 nodes=[700/300/0/3]", D(s));
        Assert.Equal(0x11, rig.GetBuffer(s, out _, out _, out _));
        Assert.Equal("b2d=21 b6c=1 b6e=5 f48=0 f4c=0 f54=0 p38=1000 nodes=[700/300/0/3]", D(s));
        // s10: Stop clears bit 0 of [S+0x6E]; Destroy sets bit 3 (b2d = 61), drops every node and restores the granted accounting
        Assert.Equal(1, s.Stop9655B0());
        Assert.Equal("b2d=21 b6c=1 b6e=4 f48=0 f4c=0 f54=0 p38=1000 nodes=[700/300/0/3]", D(s));
        s.Destroy9654E4();
        Assert.Equal("b2d=61 b6c=0 b6e=4 f48=0 f4c=0 f54=0 p38=1000 nodes=[]", D(s));
    }

    private static byte[] StreamTestPattern(ulong start, int size) => new StubResolver().BytesAt(start, size);

    [Theory]
    [InlineData(300L, 0, 300L, "b2d=177 b6c=0 b6e=5 f48=0 f4c=0 f54=0 p38=300 nodes=[]")]               // pos_begin_300: the first ungranted node starts at 0, not 300: everything is released
    [InlineData(450L, 0, 450L, "b2d=177 b6c=0 b6e=5 f48=0 f4c=0 f54=0 p38=450 nodes=[]")]               // pos_begin_450
    [InlineData(10L, 1, 10L, "b2d=177 b6c=0 b6e=5 f48=0 f4c=0 f54=0 p38=10 nodes=[]")]                   // pos_current_plus_10: GetPosition (the head's start, 0) + 10
    [InlineData(0L, 2, 0L, "b2d=21 b6c=0 b6e=5 f48=0 f4c=0 f54=0 p38=1000 nodes=[]")]                     // pos_end_minus_0: the file size 1000, real 0
    public void P1_SetPositionHasTheOraclesResults(long offset, int method, long expectedReal, string expected)
    {
        // M6-025 (0x9657E0..0x96592C): emu_stream.py position scenario, buffers [0,300) and [300,300) delivered, none granted.
        var rig = new StreamRig();
        var s = rig.Create();
        s.Start964CB8(); s.RunOpen963890();
        rig.Deliver(s, 0, 300); rig.Deliver(s, 300, 300);
        Assert.Equal("b2d=145 b6c=0 b6e=5 f48=0 f4c=0 f54=600 p38=0 nodes=[0/300/0/3;300/300/0/3]", D(s));      // p0
        Assert.Equal(1, s.SetPosition9657E0(offset, method, out long r));
        Assert.Equal(expectedReal, r);
        Assert.Equal(expected, D(s));
    }

    [Fact]
    public void P2_ABadMethodOrANegativePositionReturns0x1FAndChangesNothing()
    {
        // emu_stream.py pos_bad_method / pos_negative: result 31 (0x1F: 0x965820, 0x965840), the stream untouched.
        var rig = new StreamRig();
        var s = rig.Create();
        s.Start964CB8(); s.RunOpen963890();
        rig.Deliver(s, 0, 300); rig.Deliver(s, 300, 300);
        Assert.Equal(0x1F, s.SetPosition9657E0(0, 7, out _));
        Assert.Equal(0x1F, s.SetPosition9657E0(-5, 0, out _));
        Assert.Equal("b2d=145 b6c=0 b6e=5 f48=0 f4c=0 f54=600 p38=0 nodes=[0/300/0/3;300/300/0/3]", D(s));
    }

    [Fact]
    public void P3_ANodeThatStartsAtThePositionIsKept()
    {
        // 0x9658F4 beq 0x965934: a first ungranted node whose start + offset equals the position is kept (no release); the status update (0x964B8C) only.
        var rig = new StreamRig();
        var s = rig.Create();
        s.Start964CB8(); s.RunOpen963890();
        rig.Deliver(s, 0, 300); rig.Deliver(s, 300, 300);
        Assert.Equal(1, s.SetPosition9657E0(0, 0, out long real));
        Assert.Equal(0, real);
        Assert.Equal("b2d=145 b6c=0 b6e=5 f48=0 f4c=0 f54=600 p38=0 nodes=[0/300/0/3;300/300/0/3]", D(s));
    }

    // ------------------------------------------------------------------ SetHeuristics, SetMinimalBufferSize, GetNominalBuffering

    private static (StreamRig Rig, WwiseAutoStream S) Fresh(int granted)
    {
        var rig = new StreamRig();
        var s = rig.Create();
        s.Start964CB8(); s.RunOpen963890();
        rig.Deliver(s, 0, 300); rig.Deliver(s, 300, 300); rig.Deliver(s, 600, 300);
        for (int i = 0; i < granted; i++) rig.GetBuffer(s, out _, out _, out _);
        return (rig, s);
    }

    [Fact]
    public void H1_TheFastPathChangesOnlyTheThroughputAndTheMinimumBuffers()
    {
        // emu_stream.py heur_throughput_only: result 1, throughput bits 0x40000000, nothing else; heur_throughput_same_min_changed: [S+0x6D] = 5 (0x9651F4..0x965224); heur_priority_too_high: 0x1F, unchanged.
        var (_, s) = Fresh(0);
        Assert.Equal(1, s.SetHeuristics964E4C(StreamRig.Heur(0x40000000)));
        Assert.Equal((0x40000000u, (byte)1), (BitConverter.SingleToUInt32Bits(s.Throughput44), s.Byte6D));
        Assert.Equal("b2d=145 b6c=0 b6e=5 f48=0 f4c=0 f54=900 p38=0 nodes=[0/300/0/3;300/300/0/3;600/300/0/3]", D(s));
        (_, s) = Fresh(0);
        Assert.Equal(1, s.SetHeuristics964E4C(StreamRig.Heur(0x3F800000, 0, 0, 5)));
        Assert.Equal((0x3F800000u, (byte)5), (BitConverter.SingleToUInt32Bits(s.Throughput44), s.Byte6D));
        (_, s) = Fresh(0);
        Assert.Equal(0x1F, s.SetHeuristics964E4C(StreamRig.Heur(0x3F800000, 0, 0, 0, 0x65)));
        Assert.Equal("b2d=145 b6c=0 b6e=5 f48=0 f4c=0 f54=900 p38=0 nodes=[0/300/0/3;300/300/0/3;600/300/0/3]", D(s));
    }

    [Theory]
    [InlineData(0, 100u, 500u, "b2d=145 b6c=0 b6e=5 f48=100 f4c=500 f54=500 p38=0 nodes=[0/300/0/3;300/300/0/3]")]               // heur_loop_range_no_granted: [600,900) is not contiguous with the loop start 100
    [InlineData(1, 100u, 500u, "b2d=177 b6c=1 b6e=5 f48=100 f4c=500 f54=200 p38=300 nodes=[0/300/0/3;300/300/0/3]")]               // heur_loop_range_one_granted
    [InlineData(2, 450u, 500u, "b2d=177 b6c=2 b6e=5 f48=450 f4c=500 f54=0 p38=600 nodes=[0/300/0/3;300/300/0/3]")]                 // heur_loop_range_two_granted: the loop end 500 <= the granted end 600, so the expected position is the loop start 450
    [InlineData(1, 0u, 5000u, "b2d=145 b6c=1 b6e=5 f48=0 f4c=1000 f54=600 p38=300 nodes=[0/300/0/3;300/300/0/3;600/300/0/3]")]     // heur_loop_end_past_file: clamped to the file size (opened)
    [InlineData(1, 0u, 0u, "b2d=145 b6c=1 b6e=5 f48=0 f4c=0 f54=600 p38=300 nodes=[0/300/0/3;300/300/0/3;600/300/0/3]")]           // heur_loop_end_cleared_again: unchanged range, fast path
    public void H2_TheHeavyPathTrimsTheBuffersThatAreNotContiguousWithTheLoop(int granted, uint loopStart, uint loopEnd, string expected)
    {
        // M6-025 0x964E4C..0x965234: emu_stream.py heur_loop_* (three buffers of 300 bytes at 0, 300, 600; `granted` of them granted). The removed buffers' accounted bytes leave [S+0x54] with the updated loop end.
        var (_, s) = Fresh(granted);
        Assert.Equal(1, s.SetHeuristics964E4C(StreamRig.Heur(0x3F800000, loopStart, loopEnd)));
        Assert.Equal(expected, D(s));
    }

    [Fact]
    public void H3_ALoopRangeSetAndThenClearedRecomputesTheBufferedAmountFromTheLeftNodes()
    {
        // emu_stream.py heur_set_then_clear: one granted; (100, 500) then (0, 0): result 1, b2d 177, f54 = 300 (the ungranted node [300,600) in full once the loop end is 0), nodes 2, p38 300.
        var (_, s) = Fresh(1);
        s.SetHeuristics964E4C(StreamRig.Heur(0x3F800000, 100, 500));
        Assert.Equal(1, s.SetHeuristics964E4C(StreamRig.Heur(0x3F800000, 0, 0)));
        Assert.Equal("b2d=177 b6c=1 b6e=5 f48=0 f4c=0 f54=300 p38=300 nodes=[0/300/0/3;300/300/0/3]", D(s));
    }

    [Theory]
    [InlineData(0u, 1, 1u, 177)]            // minbuf_zero: result 1, [S+0x58] = 1
    [InlineData(1u, 1, 1u, 177)]            // minbuf_one / minbuf_equal_block
    public void H4_SetMinimalBufferSizeKeepsTheBlockSizeForSmallSizes(uint size, int expected, uint min58, int b2d)
    {
        var rig = new StreamRig();
        var s = rig.Create();
        s.Start964CB8(); s.RunOpen963890();
        Assert.Equal(expected, s.SetMinimalBufferSize965244(size));
        Assert.Equal((min58, (byte)b2d), (s.MinBuffer58, s.Flags2D));
    }

    [Fact]
    public void H5_ABufferLargerThanTheStreamBufferStopsTheStreamWithAnError()
    {
        // emu_stream.py minbuf_bigger_than_buffer (0x5000 > [S+0x50] 0x4000): result 2; [S+0x58] = 0x5000 (20480, stored before the compare); b6e = 6 (bit 1 set, bit 0 cleared by Stop, bit 2 kept); b2d = 17 (0x964B8C: not started).
        var rig = new StreamRig();
        var s = rig.Create();
        s.Start964CB8(); s.RunOpen963890();
        Assert.Equal(2, s.SetMinimalBufferSize965244(0x5000));
        Assert.Equal((20480u, (byte)6, (byte)17), (s.MinBuffer58, s.Flags6E, s.Flags2D));
    }

    [Fact]
    public void H6_GrowingTheMinimumBufferSizeNeedsTheUnreadVt80()
    {
        // The oracle returns 1 for minbuf_bigger_fits (0x1000: [S+0x58] = 4096), running vt+0x80 (0x965F94). That body is not read by C33, so the port stops: MISSING 0x965F94.
        var rig = new StreamRig();
        var s = rig.Create();
        s.Start964CB8(); s.RunOpen963890();
        Assert.Throws<WwiseMissingBehaviourException>(() => s.SetMinimalBufferSize965244(0x1000));
    }

    [Fact]
    public void N1_TheNominalBufferingIsTheThroughputTimesTheTargetLengthTruncated()
    {
        // emu_stream.py nominal: throughput 3.14159f (0x40490FDB) * 380.0f (0x43BE0000) -> 1193 (vcvt.u32.f32, 0x961AF4); a caching stream (bit 6) returns [S+0x44] as an integer (0x961ADC..0x961AE8).
        var rig = new StreamRig();
        var s = rig.Create(5, StreamRig.Heur(0x40490FDB));
        Assert.Equal(0x43BE0000u, BitConverter.SingleToUInt32Bits(rig.Device.TargetLength224));
        Assert.Equal(1193u, s.GetNominalBuffering961AD8());
        s.Flags2D |= 0x40;
        Assert.Equal(1078530011u, s.GetNominalBuffering961AD8());
    }

    // ------------------------------------------------------------------ a transfer in flight (inflight scenario)

    private static (StreamRig Rig, WwiseAutoStream S, WwiseStreamNode Node) InFlight(uint loopEnd = 0)
    {
        var rig = new StreamRig();
        var s = rig.Create(5, StreamRig.Heur(0x3F800000, 0, loopEnd));
        s.Start964CB8(); s.RunOpen963890();
        var buffer = new WwiseStreamBuffer { Start = 200, Data = rig.Resolver.BytesAt(200, 300), Size = 300, CacheId = -1 };
        var node = new WwiseStreamNode { Buffer = buffer, Offset = 0, State = 1 };
        uint loop = s.LoopEnd4C;
        uint accounted = loop != 0 ? (200 >= loop ? 300u : (200 + 300 > loop ? loop - 200 : 300u)) : 300u;   // the oracle's python formula
        s.Buffered54 += accounted;
        s.InFlight70 = node;
        return (rig, s, node);
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(400u)]
    [InlineData(100u)]
    public void F1_CancellingATransferInFlightRestoresTheAccountingAndKeepsTheStreamAlive(uint loopEnd)
    {
        // emu_stream.py inflight_cancel_*: the next position is the in-flight buffer's end (500, 0x9666E8..0x966708); vt+8 says no while a transfer is in flight; vt+0x78 (0x96672C): the record's state becomes 2 and its
        // offset its size (300), the accounted bytes leave [S+0x54] (0 for every loop end: the same formula adds and removes them), [S+0x70] = 0, [S+0x74] = 1; vt+8 still says no (the byte).
        var (_, s, node) = InFlight(loopEnd);
        Assert.Equal(500UL, s.NextPosition9666E8());
        Assert.False(s.TryDestroy965F5C());
        s.CancelInFlight96672C();
        Assert.Equal((2, 300u), (node.State & 7, node.Offset));
        Assert.Null(s.InFlight70);
        Assert.Equal((0u, (byte)1, (byte)177, (byte)5), (s.Buffered54, s.Byte74, s.Flags2D, s.Flags6E));
        Assert.False(s.TryDestroy965F5C());
    }

    [Theory]
    [InlineData(200UL, true)]     // inflight_seek_equal_to_the_buffer_start: kept (node state 1, [S+0x54] 300, [S+0x70] set)
    [InlineData(500UL, false)]    // equal_to_the_buffer_end: cancelled
    [InlineData(100UL, false)]    // elsewhere: cancelled
    public void F2_ASeekKeepsATransferWhoseBufferStartsAtThePositionAndCancelsAnyOther(ulong position, bool kept)
    {
        // M6-025 vt+0x7C (0x9667C0..0x966848): emu_stream.py inflight_seek_*.
        var (_, s, node) = InFlight();
        s.Seek9667C0(position);
        Assert.Equal(kept, s.InFlight70 is not null);
        Assert.Equal(kept ? (1, 0u, 300u, (byte)0) : (2, 300u, 0u, (byte)1), (node.State & 7, node.Offset, s.Buffered54, s.Byte74));
    }

    [Theory]
    [InlineData(1, "b2d=177 b6c=0 b6e=5 f48=0 f4c=0 f54=300 p38=0 nodes=[200/300/0/3]", 3)]    // inflight_complete_success: appended, state 3
    [InlineData(2, "b2d=17 b6c=0 b6e=6 f48=0 f4c=0 f54=0 p38=0 nodes=[]", 1)]                  // error: released, the stream stopped with the error bit
    [InlineData(0x35, "b2d=177 b6c=0 b6e=5 f48=0 f4c=0 f54=0 p38=0 nodes=[]", 1)]              // any other result: released
    public void F3_ACompletionAppendsOnlyASuccessfulTransferThatIsStillInFlight(int result, string expected, int state)
    {
        // M6-025 E5 (0x966644..0x9666E4, 0x964914, 0x964A20): emu_stream.py inflight_complete_*: the engine returns non-zero for a given record (here 1).
        var (_, s, node) = InFlight();
        Assert.True(s.CompleteTransfer966644(node, result, true));
        Assert.Equal(expected, D(s));
        Assert.Equal(state, node.State & 7);
        Assert.Null(s.InFlight70);
        Assert.Equal((byte)0, s.Byte74);
    }

    [Fact]
    public void F4_ACancelledOrOrphanRecordIsReleasedEvenOnSuccess()
    {
        // emu_stream.py inflight_complete_cancelled_state (the record's state 2) and inflight_complete_not_in_flight ([S+0x70] == 0): released, nothing appended (0x9666B4, 0x9666C4).
        var (_, s, node) = InFlight();
        node.State = 2;
        Assert.True(s.CompleteTransfer966644(node, 1, true));
        Assert.Equal("b2d=177 b6c=0 b6e=5 f48=0 f4c=0 f54=0 p38=0 nodes=[]", D(s));
        var (_, s2, node2) = InFlight();
        s2.InFlight70 = null;
        Assert.True(s2.CompleteTransfer966644(node2, 1, true));
        Assert.Equal("b2d=177 b6c=0 b6e=5 f48=0 f4c=0 f54=0 p38=0 nodes=[]", D(s2));
    }

    [Fact]
    public void F5_DestroyWithATransferInFlightCancelsItAndTheStreamCannotBeDestroyedYet()
    {
        // emu_stream.py inflight_destroy_flagged: after Destroy (bit 3) vt+8 says no ([S+0x74] = 1 from the cancellation); b2d = 25 (bits 0, 3, 4).
        var (_, s, _) = InFlight();
        s.Destroy9654E4();
        Assert.False(s.TryDestroy965F5C());
        Assert.Equal((byte)25, s.Flags2D);
        Assert.Equal((byte)1, s.Byte74);
    }

    // ------------------------------------------------------------------ the seam

    [Fact]
    public void I1_TheSteppedSeamRunsTheEnginesCompletionAndStopsAtTheNominalBuffering()
    {
        // The seam (C33.4, EQUIVALENT_IMPLEMENTATION; WwiseSteppedStreamIo, class remarks): one Step opens the deferred file with the engine's 0x963890, plans reads of min([S+0x50], file size - position) bytes until
        // [S+0x54] reaches the nominal buffering 380 (throughput 1.0f: 380), and completes each with the engine's 0x966644. The expected numbers follow from those rules and the oracle's nominal formula.
        var io = new WwiseSteppedStreamIo(0xCAFEF00D);
        var rig = new StreamRig(fileSize: 1000, io: io);
        var s = rig.Create();
        s.Start964CB8();
        Assert.Equal(0x2E, rig.Query(s).Result);                                                           // not opened: no I/O has run
        io.Step();
        Assert.Equal(0x10, s.Flags2D & 0x10);                                                              // opened
        Assert.Equal(1, s.Nodes.Count);                                                                    // one read of min(0x4000, 1000) = 1000 bytes: more than the nominal 380
        Assert.Equal((0UL, 1000u), (s.Nodes[0].Buffer!.Start, s.Nodes[0].Buffer!.Size));
        Assert.Equal(1000u, s.Buffered54);
        Assert.Equal(StreamTestPattern(0, 1000), s.Nodes[0].Buffer!.Data);                                 // read through the hook
        Assert.Equal(1, io.TransfersRead);
        io.Step();                                                                                         // nothing more is planned: the position is at the end of the file
        Assert.Equal(1, io.TransfersRead);
        Assert.Equal(0x04, s.Flags2D & 0x04);                                                              // end of file (0x964C34)
        Assert.Equal(0x11, rig.GetBuffer(s, out _, out _, out uint size));                                  // the buffer comes with 0x11: bit 2 is set and the position reaches the file size (0x965C64..0x965C7C)
        Assert.Equal(1000u, size);
    }

    [Theory]
    [InlineData(0u, 0u, 500u, 500UL, 500u, 500UL)]       // emu_stream.py prepare_no_loop: no loop end: the position is the next position, the size the rest of the file ([S+0x50] = 0x4000 > 500... file 1000)
    [InlineData(100u, 400u, 300u, 300UL, 700u, 300UL)]   // prepare_below_loop_end: position 300 < [S+0x4C] = 400: no wrap
    [InlineData(100u, 400u, 400u, 100UL, 900u, 400UL)]   // prepare_at_loop_end: next position 400 >= 400: the position becomes [S+0x48] = 100 (0x966C4C..0x966C74)
    [InlineData(100u, 400u, 450u, 100UL, 900u, 450UL)]   // prepare_past_loop_end: 450 >= 400: wraps to 100
    [InlineData(0u, 400u, 400u, 0UL, 1000u, 400UL)]      // prepare_loop_start_zero: wraps to 0
    public void I6_ThePlanWrapsToTheLoopStartWhenThePositionReachesTheLoopEnd(uint loopStart, uint loopEnd, uint deliveredEnd, ulong expectedPos, uint expectedSize, ulong expectedNext)
    {
        // M6-025 0x966BCC (emu_stream.py prepare_*: the real function under Unicorn up to its buffer allocation 0x966F04): [S+0x4C] != 0 and position >= [S+0x4C] -> position = [S+0x48]. There is no stop at the loop end.
        // The throughput 10.0f (nominal buffering 3800) keeps the seam's own stop at the nominal buffering (a choice, see the class remarks) out of the way: the real 0x966BCC has no such stop.
        var rig = new StreamRig(fileSize: 1000, io: new ManualIo());
        var s = rig.Create(5, StreamRig.Heur(0x41200000));
        s.Start964CB8(); s.RunOpen963890();
        if (loopEnd != 0) Assert.Equal(1, s.SetHeuristics964E4C(StreamRig.Heur(0x41200000, loopStart, loopEnd)));
        rig.Deliver(s, 0, deliveredEnd);
        Assert.Equal(expectedNext, s.NextPosition9666E8());
        var plan = new WwiseSteppedStreamIo(0).PrepareTransfer(rig.Device, s, bSync: false);
        Assert.NotNull(plan);
        Assert.Equal((expectedPos, expectedSize), (plan!.Info!.FilePosition, plan.Info.BufferSize));
        Assert.Equal(expectedPos, plan.Node!.Buffer!.Start);
    }

    [Fact]
    public void I7_AtTheEndOfTheFileWithoutALoopNothingIsPlanned()
    {
        // emu_stream.py prepare_at_file_end_no_loop: the real 0x966BCC returns 0 (the next position 1000 is the file size).
        var rig = new StreamRig(fileSize: 1000, io: new ManualIo());
        var s = rig.Create(5, StreamRig.Heur(0x41200000));
        s.Start964CB8(); s.RunOpen963890();
        rig.Deliver(s, 0, 1000);
        Assert.Null(new WwiseSteppedStreamIo(0).PrepareTransfer(rig.Device, s, bSync: false));
    }

    [Fact]
    public void I8_ASecondThreadInsideTheSeamThrows()
    {
        // The seam's thread contract (class remarks): one servicing thread; a stream flagged for destroy whose vt+8 blocks keeps the first thread inside while a second enters.
        var io = new WwiseSteppedStreamIo(0);
        var rig = new StreamRig(fileSize: 1000, io: io);
        var s = rig.Create();
        s.Start964CB8();
        var inside = new ManualResetEventSlim();
        io.ServiceEntered = inside.Set;
        Exception? second = null;
        Thread? first = null;
        lock (s.Lock14)                                         // held here, so the first thread's Step blocks on it inside Service
        {
            first = new Thread(io.Step);
            first.Start();
            inside.Wait();
            var other = new Thread(() => { try { io.Step(); } catch (Exception e) { second = e; } });
            other.Start(); other.Join();
            Assert.IsType<InvalidOperationException>(second);
        }
        first.Join();
    }

    [Fact]
    public void I2_TheImmediateModeRunsTheThreadAtEveryCheckPoint()
    {
        // Immediate: the I/O thread "has run" at each GetBuffer / Query, so the first GetBuffer after Start already has data (the extreme of an I/O thread that is never late).
        var io = new WwiseSteppedStreamIo(0) { Immediate = true };
        var rig = new StreamRig(fileSize: 1000, io: io);
        var s = rig.Create();
        s.Start964CB8();
        Assert.Equal(0x11, rig.GetBuffer(s, out _, out _, out uint size));                                  // the stream reaches the end of the file with its first buffer
        Assert.Equal(1000u, size);
        var late = new WwiseSteppedStreamIo(0);
        var rig2 = new StreamRig(fileSize: 1000, io: late);
        var s2 = rig2.Create();
        s2.Start964CB8();
        Assert.Equal(0x2E, rig2.GetBuffer(s2, out _, out _, out _));                                        // not immediate: the thread has not run
        late.Step();
        Assert.Equal(0x11, rig2.GetBuffer(s2, out _, out _, out _));
    }

    [Fact]
    public void I3_AFileThatFailsToOpenKillsTheStream()
    {
        // 0x963890 returning a failure completes the transfer with 2 (0x966A98..0x966AB0): bit 1 of [S+0x6E] (error), Stop, and QueryBufferingStatus / GetBuffer return 2.
        var io = new WwiseSteppedStreamIo(0);
        var rig = new StreamRig(fileSize: 1000, io: io);
        var s = rig.Create();
        s.Start964CB8();
        rig.Resolver.FileSize = 0;                                                                         // the open reports no size: 0x960C80..0x960CB0 returns 2 for mode 0
        io.Step();
        Assert.Equal(2, rig.Query(s).Result);
        Assert.Equal(2, rig.GetBuffer(s, out _, out _, out _));
        Assert.Equal(0x02, s.Flags6E & 0x02);
        Assert.Null(s.Record0C);                                                                            // the record is freed on a failure (0x9638DC)
    }

    [Fact]
    public void I4_AFlaggedStreamIsDestroyedByTheThreadOnceItCanBe()
    {
        // 0x962A90 / 0x963104: a stream with bit 3 set is unlinked and destroyed when vt+8 (0x965F5C) allows it (no transfer in flight, [S+0x74] clear); its descriptor is closed through the hook when opened.
        var io = new WwiseSteppedStreamIo(0);
        var rig = new StreamRig(fileSize: 1000, io: io);
        var s = rig.Create();
        s.Start964CB8();
        io.Step();
        s.Destroy9654E4();
        Assert.Single(rig.Device.Streams);
        io.Step();
        Assert.Empty(rig.Device.Streams);
        Assert.Null(s.FileDesc10);
    }

    [Fact]
    public void I5_ADeviceThatCannotBeInitialisedFailsAsTheEngineDoes()
    {
        // 0x9616F4: granularity 0 -> 0x1F; settings[4] != 0 with a negative target length -> 0x1F (NaN passes); a deferred device with settings[0x2C] - 1 >= 0x400 -> 0x1F; the I/O memory manager failing -> 2;
        // the thread result is returned. CreateDevice turns any of them into -1 and leaves the slot reserved.
        var io = new WwiseSteppedStreamIo(0) { InitIoMemoryResult = 2 };
        var mgr = new WwiseStreamManager(io);
        var hook = new StubResolver();
        Assert.Equal(-1, mgr.CreateDevice(WwiseDeviceSettings.Anki(), hook));
        Assert.Equal(1, mgr.DeviceCount);
        io.InitIoMemoryResult = 1;
        Assert.Equal(-1, mgr.CreateDevice(new WwiseDeviceSettings { TargetAutoStreamBufferLength28 = -1f }, hook));
        Assert.Equal(0, mgr.CreateDevice(new WwiseDeviceSettings { TargetAutoStreamBufferLength28 = float.NaN }, hook));   // NaN is not "less than 0"
        io.StartThreadResult = 2;
        Assert.Equal(-1, mgr.CreateDevice(WwiseDeviceSettings.Anki(), hook));
        Assert.Equal(-1, mgr.CreateDevice(new WwiseDeviceSettings { SchedulerFlags14 = 4 }, hook));        // neither bit 0 nor bit 1: -1
        Assert.Throws<WwiseMissingBehaviourException>(() => mgr.CreateDevice(new WwiseDeviceSettings { SchedulerFlags14 = 2 }, hook));   // the deferred device is not built
    }
}
