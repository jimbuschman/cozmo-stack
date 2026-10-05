using System.IO.Compression;
using System.Text;
using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M6-025 / M6-022 (C33.3, C36): the source-class stream functions (0xAB22D4, 0xAB2088, 0xAB1C04, 0xAB2BFC, 0xAB12B4, 0xAB1550 with its packet decode 0xAB7E40 and output hand-off 0xA73490, 0xA7538C, 0xA73ABC, 0xA73D34,
/// 0xA74564, 0xA7482C, 0xA74970, 0xA746A8, 0xA74C80, 0xA74E00), the in-memory Vorbis class and the voice pass retry that drives them. Every expected state and result is the engine's own: the real source-class
/// functions, the stream layer, the WAVE walker 0x9CD340, and (batch 5e) the setup cache, the decoder-state allocation, the frame loop and the hand-off run under Unicorn
/// (re-analysis/tools/emu/emu_stream.py and emu_decode.py; the strings in WwiseStreamSourceOracle.cs and WwiseVorbisEngineOracle.cs are generated from that output); none comes from running this implementation.
/// The scenarios of emu_stream.py that were about the control flow of the packet collection (V1..V11, D4, D5, A1, A2) keep the engine's stand-in for the setup cache and the buffer allocation on the engine side
/// (both succeed, and their arguments are logged); the real cache and allocation are compared with the engine's real ones in WwiseVorbisEngineTests.
/// </summary>
public class WwiseStreamSourceTests
{
    // ------------------------------------------------------------------ fixtures

    private static byte[]? ReadWem(string name)
    {
        if (WwiseAssets.SoundDir is not { } dir) { Assert.Fail("the shipped re-analysis/obb/assets/cozmo_resources/sound/AudioAssets.zip is not present: these tests do not pass silently without it"); return null; }
        using var zip = ZipFile.OpenRead(Path.Combine(dir, "AudioAssets.zip"));
        var entry = zip.GetEntry(name) ?? throw new InvalidOperationException(name);
        using var s = entry.Open();
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    private const string VorbisName = "English(US)/99908739.wem";
    private const string AdpcmName = "998061257.wem";

    private static uint Crc32(byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte b in data)
        {
            crc ^= b;
            for (int i = 0; i < 8; i++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
        }
        return ~crc;
    }

    /// <summary>The real walker 0x9CD340 (V15): production code since batch 5e; W1 compares it with the engine's own results on the shipped media.</summary>
    internal static WwiseWaveWalk Walker(WwiseBytePtr data, uint length)
        => WwiseWaveWalker.Walk9CD340(data, length, new WwiseChunkContainer(), wantAkd: true, wantSeek: false, () => true);

    private sealed class SourceRig
    {
        public readonly StreamRig Rig;
        public readonly WwisePlayingInstance Pbi;
        public readonly WwiseBankMemory Memory = new();
        public readonly byte[]? Prefix;
        public readonly List<string> Setups = new();
        public readonly List<string> Callbacks = new();
        public readonly List<int> OutBuffers = new();
        public bool OutFail;
        private bool _failNext;
        public readonly bool Vorbis;
        public readonly byte[] Wem;
        public WwiseVorbisStreamSource? V;
        public WwisePcmAdpcmStreamSource? A;
        public WwiseStreamSourceSeams Seams = null!;
        public readonly WwiseSourceBlock150 Block;
        public WwiseStreamSourceBase Src => (WwiseStreamSourceBase?)V ?? A!;

        public SourceRig(byte[] wem, int prefixLen, bool vorbis = true, ushort loop = 1, byte flags1BD = 0x44, uint flags4 = 0, uint priorityBits = 0x3F800000, byte bits0c = 0x03,
            uint stale = 0, bool noCodebook = false, bool noCallback = false, byte bits0d = 0x02, string? name = null, bool pcm = false)
        {
            Wem = wem;
            Vorbis = vorbis;
            uint sourceId = vorbis ? 99908739u : 998061257u;
            uint plugin = vorbis ? 0x00040001u : (pcm ? 0x00010001u : 0x00020001u);
            Rig = new StreamRig(fileSize: wem.Length, io: new ManualIo { StaleQueryWord = stale }, content: wem);
            Rig.Manager.AllocationFails = () => { if (!_failNext) return false; _failNext = false; return true; };
            Pbi = new WwisePlayingInstance(new WwisePlayInitParams { PlayingId = 0x1234, TargetNodeId = 1 }, 1,
                new WwiseSourceDescriptor(plugin, 1, sourceId, 0, 0), new byte[0x44], null, continuous: false);
            Pbi.Word15C = 0;                                                                               // the oracle's PBI image is zero-filled (no PBI constructor ran)
            Pbi.LoopCount1B8 = loop;
            Pbi.Flags1BD = flags1BD;
            Pbi.Flags4 = flags4;
            Pbi.Priority1C0 = BitConverter.Int32BitsToSingle(unchecked((int)priorityBits));
            if (prefixLen != 0)
            {
                uint address = Memory.Allocate(prefixLen + 16)!.Value;
                Prefix = Memory.Block(address);
                Array.Copy(wem, Prefix, prefixLen);
                Pbi.Word1DC = address;
                Pbi.Word1E0 = (uint)prefixLen;
            }
            Block = new WwiseSourceBlock150 { SourceId04 = sourceId, Bits0C = bits0c, Bits0D = bits0d, Name10 = name, Plugin14 = plugin };
            var ctx = new WwiseVorbisEngineContext { Codebooks = noCodebook ? null : WwiseVorbisEngineTests.Codebooks() };
            ctx.OnSetupAcquire = (bytes, flag) =>                                                          // the arguments the oracle's stand-in of 0xAB2D74 logs
            {
                var arr = new byte[bytes.Length];
                bytes.CopyTo(arr);
                bool inPrefix = System.Runtime.InteropServices.MemoryMarshal.TryGetArray(bytes, out var seg) && ReferenceEquals(seg.Array, Prefix);
                Setups.Add($"{bytes.Length}/{flag}/{Crc32(arr)}/{(inPrefix ? 1 : 0)}");
            };
            ctx.OnDspAllocate = ch => { OutBuffers.Add(ch); if (OutFail) _failNext = true; };               // the oracle's stand-in of 0xAB3264 logs the channel count and fails when asked
            Seams = new WwiseStreamSourceSeams
            {
                Memory = Memory,
                Vorbis = ctx,
                BaseDestructor = _ => { },                                                                   // TEST DOUBLE of the base destructor 0xA75A0C (unread; it releases the stream, which these tests do not look at)
                BufferingCallbackA059D8 = noCallback ? null : (pid, src, info) => Callbacks.Add($"{pid}/{info.Estimate}/{info.Flag}"),
            };
            if (vorbis) V = new WwiseVorbisStreamSource(Rig.Manager, Pbi, Block, Seams);
            else A = new WwisePcmAdpcmStreamSource(Rig.Manager, Pbi, Block, Seams) { Class = pcm ? WwisePcmAdpcmClass.PcmStream : WwisePcmAdpcmClass.AdpcmStream };
        }

        public int Start() => V is not null ? V.StartStreamAB22D4() : A!.StartStreamA7538C();

        public void Io(uint start, uint size)
        {
            var s = Src.Stream3C!;
            s.RunOpen963890();
            Rig.Deliver(s, start, size);
        }

        private string F40()
        {
            var p = Src.Data40;
            if (p.IsNull) return "0";
            return ReferenceEquals(p.Array, Prefix) ? p.Index.ToString() : "buf";
        }

        private static string Nodes(WwiseAutoStream s) => string.Join(";", s.Nodes.Select(n => $"{n.Buffer!.Start}/{n.Buffer.Size}/{n.Offset}/{n.State & 7}"));

        public string State()
        {
            var src = Src;
            var parts = new List<string>
            {
                $"f10={src.Flags10} f14={src.TotalSamples14} f18={src.SampleBase18} f1c={src.DataSize1C} f20={src.HeaderSize20} f24={src.Word24} f28={src.Word28} h38={src.LoopCount38} stream={(src.Stream3C is null ? 0 : 1)} f40={F40()} f44={src.Left44} f48={src.FileOffset48} f4c={src.StreamOffset4C} f50={src.Skip50} f54={src.LoopStartByte54} f58={src.LoopEndByte58} h5c={src.LoopCounter5C} b5e={src.Bits5E & 0x1F}",
                $"pbi158={Pbi.SourceFormat158} pbi15c={Pbi.Word15C} pbi160={Pbi.Byte160} pbi161={Pbi.Byte161} pbi162={Pbi.Byte162} pbi1b4={Pbi.Word1B4} pbi1bd={Pbi.Flags1BD} pbi1be={Pbi.Flags1BE}",
            };
            if (V is { } v)
                parts.Add($"v68={v.State68} v80={(v.CodebookHandle80 is null ? 0 : 1)} va8={v.ChannelWordA8} ve0={v.SampleRateE0} ve8={v.SeekCollected} vf0={v.SetupPayloadCollected} vf4={v.SetupPrefixCollected} vf8={(v.SetupOwned ? 1 : 0)} l2c={v.LoopStateSkip2C} l2e={v.LoopStateWord2E} vorb={Convert.ToHexString(v.VorbBlock).ToLowerInvariant()}");
            parts.Add($"setups=[{string.Join(";", Setups)}]");
            parts.Add($"cb=[{string.Join(";", Callbacks)}]");
            parts.Add($"ob=[{string.Join(";", OutBuffers)}]");
            var s = src.Stream3C;
            parts.Add(s is null ? "str=none" :
                $"str:b2d={s.Flags2D} b6c={s.Granted6C} b6e={s.Flags6E} b6d={s.Byte6D} f44={BitConverter.SingleToUInt32Bits(s.Throughput44)} f48={s.LoopStart48} f4c={s.LoopEnd4C} f54={s.Buffered54} f58={s.MinBuffer58} p38={s.Position38} nodes=[{Nodes(s)}]");
            return string.Join(" ", parts);
        }
    }

    private static (int[] Results, string[] States) Oracle(string name) => StreamSourceOracle.Cases[name];

    // ------------------------------------------------------------------ W1: the walker double against the engine's walker

    [Fact]
    public void W1_TheWalkerDoubleReproducesTheEnginesWalkerOnTheShippedMedia()
    {
        // emu_stream.py walker / walker_degenerate (the real 0x9CD340 under Unicorn): {result, fmt offset, data offset, data size, loop words}. The double is test-only: C33 does not adopt the walker.
        var plain = ReadWem(VorbisName);
        if (plain is null) return;
        foreach (var (length, result) in new[] { (9289, 1), (600, 1), (200, 1), (90, 8) })
        {
            var w = Walker(new WwiseBytePtr(plain, 0), (uint)length);
            Assert.Equal(result, w.Result);
            if (result == 1) Assert.Equal((20, 94u, 9195u, 0u, 0u), (w.Format.Index, w.DataOffset20, w.DataSize1C, w.Word24, w.Word28));
        }
        var big = ReadWem("1000323750.wem")!;
        var wb = Walker(new WwiseBytePtr(big, 0), (uint)big.Length);
        Assert.Equal((1, 94u, 4120122u), (wb.Result, wb.DataOffset20, wb.DataSize1C));
        Assert.Equal(8, Walker(new WwiseBytePtr(big, 0), 90).Result);
        var loop = ReadWem("English(US)/1013342711.wem")!;
        foreach (uint length in new uint[] { (uint)loop.Length, 600, 200 })   // emu_stream.py walker: lengths 70062 (the whole entry), 600, 200 give result 1; 90 gives 8
        {
            var wl = Walker(new WwiseBytePtr(loop, 0), length);
            Assert.Equal((1, 162u, 69900u, 24302u, 220574u), (wl.Result, wl.DataOffset20, wl.DataSize1C, wl.Word24, wl.Word28));   // the smpl loop: start 24302, end 220574
        }
        Assert.Equal(8, Walker(new WwiseBytePtr(loop, 0), 90).Result);
        Assert.Equal(0x1F, Walker(default, 0).Result);
        Assert.Equal(0x1F, Walker(default, 50).Result);
        Assert.Equal(0x1F, Walker(new WwiseBytePtr(new byte[16], 0), 0).Result);
        Assert.Equal(7, Walker(new WwiseBytePtr(new byte[16], 0), 11).Result);
    }

    // ------------------------------------------------------------------ V1: the first StartStream call of the streamed Vorbis class

    [Theory]
    [InlineData("prefix_covers", 600, 1, 0x3F800000u, 0u, 3)]            // a prefix that covers the seek table and setup packet: 1 with NO buffering gate (0xAB2480..0xAB24B0)
    [InlineData("prefix_exact", 347, 1, 0x3F800000u, 0u, 3)]             // exactly data offset + [S+0xC4] + 2 + setup size: 1, nothing left over
    [InlineData("prefix_one_short", 346, 1, 0x3F800000u, 0u, 3)]         // one byte short: 0x3F (63), the setup payload collected 218 of 219 in an owned copy (vf0 = 218, vf8 = 1)
    [InlineData("prefix_header_only", 94, 1, 0x3F800000u, 0u, 3)]        // the header only: nothing left for the seek table: 0x3F from the state machine's `[S+0x44] == 0`
    [InlineData("prefix_tiny", 40, 1, 0x3F800000u, 0u, 3)]               // the data chunk header is outside the prefix: the walker returns 8, which is returned
    [InlineData("loop_infinite", 600, 0, 0x3F800000u, 0u, 3)]            // loop count 0: loop bytes from the vorb block, SetHeuristics with the loop range (the heavy path)
    [InlineData("loop_three", 600, 3, 0x3F800000u, 0u, 3)]
    [InlineData("priority_7_5", 600, 1, 0x40F00000u, 0u, 3)]             // priority (int)7.5f = 7 in the stream (0xAB14B0..)
    [InlineData("callback_bit", 600, 1, 0x3F800000u, 0x400000u, 3)]      // the has != 0 path has no gate and no callback block: no callback
    [InlineData("prefetch_bit_clear", 600, 1, 0x3F800000u, 0u, 1)]       // [src+0xC] bit 1 clear: no prefetch: 0xAB2088 -> GetBuffer on the unopened stream -> 0x3F
    public void V1_TheFirstCallMatchesTheEnginesResultAndState(string name, int prefixLen, int loop, uint priorityBits, uint flags4, int bits0c)
    {
        var wem = ReadWem(VorbisName);
        if (wem is null) return;
        var (results, states) = Oracle("vorbis_" + name);
        var rig = new SourceRig(wem, prefixLen, loop: (ushort)loop, priorityBits: priorityBits, flags4: flags4, bits0c: (byte)bits0c);
        Assert.Equal(results[0], rig.Start());
        Assert.Equal(states[0], rig.State());
    }

    [Theory]
    [InlineData("retry_no_io_one_short", 346)]
    [InlineData("retry_no_io_tiny", 40)]
    public void V2_ARetryWithoutAnyIoRepeatsTheResultsTheEngineGives(string name, int prefixLen)
    {
        // The voice pass retries by calling StartStream again (0xA56650 leaves the latch clear for any result but 1). Without I/O the one-short stream keeps returning 0x3F from its saved state
        // ([S+0x68] = 2, the setup copy kept); the tiny one fails in the prefetch (8), and its second and third calls find a stream that was never started: GetBuffer gives 0x11 with nothing,
        // 0x9CD340(NULL, 0) returns 0x1F (the walker double reproduces it, W1), and 0x1F is returned (31).
        var wem = ReadWem(VorbisName);
        if (wem is null) return;
        var (results, states) = Oracle("vorbis_" + name);
        var rig = new SourceRig(wem, prefixLen);
        var got = new[] { rig.Start(), rig.Start(), rig.Start() };
        Assert.Equal(results, got);
        Assert.Equal(states[0], rig.State());
    }

    [Theory]
    [InlineData("retry_after_io_one_short", 346)]
    [InlineData("retry_after_io_tiny", 40)]
    public void V3_TheDataThatArrivesAfterTheFirstCallIsReadFromTheEndOfThePrefix(string name, int prefixLen)
    {
        // emu_stream.py: call 1; the I/O thread opens the file and delivers 4096 bytes from [prefix length]; calls 2 and 3. One short: the header completes (v68 = 3, the codebook cache saw the 219-byte setup packet in an owned
        // copy, in_prefix 1: 0) but the gate holds the result at 0x3F: [S+0x44] + available (4095 + 0) is below the nominal buffering 12.971 * 380 = 4928 (0xAB2648..0xAB266C).
        var wem = ReadWem(VorbisName);
        if (wem is null) return;
        var (results, states) = Oracle("vorbis_" + name);
        var rig = new SourceRig(wem, prefixLen);
        var got = new List<int> { rig.Start() };
        rig.Io((uint)prefixLen, 4096);
        got.Add(rig.Start());
        got.Add(rig.Start());
        Assert.Equal(results, got);
        Assert.Equal(states[0], rig.State());
    }

    [Fact]
    public void V4_TwoBuffersPassTheGateAndTheLatchStopsFurtherCalls()
    {
        // emu_stream.py vorbis_retry_after_io_two_buffers: the first call returns 0x3F; two deliveries of 4096 bytes; the second call returns 1 (4095 + 4096 >= 4928). Through the live 0xA56650: the latch is
        // set by the result 1 and a third call returns 1 WITHOUT calling vt+0x28 again.
        var wem = ReadWem(VorbisName);
        if (wem is null) return;
        var (results, states) = Oracle("vorbis_retry_after_io_two_buffers");
        var rig = new SourceRig(wem, 346);
        var source = new FakeVorbisVoiceSource(rig.V!);
        Assert.Equal(results[0], WwiseVoiceSourceStart.StartA56650(source, 0, 0, out bool ran1));
        Assert.True(ran1);
        Assert.False(source.StartStreamSucceeded);                                                         // 0x3F leaves bit 0 of [S+0x10] clear
        rig.Io(346, 4096);
        rig.Rig.Deliver(rig.V!.Stream3C!, 346 + 4096, 4096);
        Assert.Equal(results[1], WwiseVoiceSourceStart.StartA56650(source, 0, 0, out bool ran2));
        Assert.True(ran2);
        Assert.True(source.StartStreamSucceeded);                                                          // 0xA56678..0xA56684: set on exactly 1
        Assert.Equal(states[0], rig.State().Replace("f10=3 ", "f10=2 "));                                  // the oracle calls the raw StartStream: the latch (bit 0 of [S+0x10]) is 0xA56650's
        string before = rig.State();
        Assert.Equal(1, WwiseVoiceSourceStart.StartA56650(source, 0, 0, out bool ran3));
        Assert.False(ran3);
        Assert.Equal(before, rig.State());
    }

    private sealed class FakeVorbisVoiceSource : IWwiseVoiceSource
    {
        private readonly WwiseVorbisStreamSource _s;
        public FakeVorbisVoiceSource(WwiseVorbisStreamSource s) => _s = s;
        public int Channels => 1;
        public int SampleRate => 48000;
        public int Render(WwiseVoiceBuffer buffer) => 0x2E;
        public int StartStream(uint a, uint b) => _s.StartStreamAB22D4();
        public bool StartStreamSucceeded { get => _s.StartLatch; set => _s.StartLatch = value; }
    }

    // ------------------------------------------------------------------ V5: no prefetch, the gate and the callback block

    [Theory]
    [InlineData("noprefetch", 0x44, 0)]
    [InlineData("noprefetch_loop_infinite", 0x44, 1)]
    [InlineData("noprefetch_gate_off", 0x04, 0)]
    public void V5_WithoutAPrefixTheFirstCallReturns0x3FAndTheDataFinishesTheHeader(string name, int flags1BD, int infinite)
    {
        // emu_stream.py vorbis_noprefetch*: call 1 -> 0x3F (the unopened stream gives 0x2E to 0xAB2088); a repeat -> 0x3F; the I/O thread opens and delivers 4096 bytes; call 3 parses the header from the first
        // buffer (0xAB12B4 on [S+0x40], then the state machine): with the gate on (the PBI's bit 6: [S+0x10] bit 1) the result is again 0x3F (3749 + 0 < 4928); with the gate off it is 1 and stays 1.
        var wem = ReadWem(VorbisName);
        if (wem is null) return;
        var (results, states) = Oracle("vorbis_" + name);
        var rig = new SourceRig(wem, 0, loop: (ushort)(infinite == 1 ? 0 : 1), flags1BD: (byte)flags1BD);
        var got = new List<int> { rig.Start() };
        string first = rig.State();
        got.Add(rig.Start());
        rig.Io(0, 4096);
        got.Add(rig.Start());
        string second = rig.State();
        got.Add(rig.Start());
        got.Add(rig.Start());
        Assert.Equal(results, got);
        Assert.Equal(states[0], first);
        Assert.Equal(states[1], second);
        Assert.Equal(states[2], rig.State());
    }

    [Fact]
    public void V6_ASmallFirstBufferKeepsTheGateClosedOnEveryLaterCall()
    {
        // emu_stream.py vorbis_noprefetch_small_buffer(+_call3..5): 400 bytes delivered: the header completes inside it (left 53) and every call returns 0x3F (53 + 0 < 4928).
        var wem = ReadWem(VorbisName);
        if (wem is null) return;
        var rig = new SourceRig(wem, 0);
        rig.Start();
        rig.Io(0, 400);
        var (results, states) = Oracle("vorbis_noprefetch_small_buffer");
        Assert.Equal(results[0], rig.Start());
        Assert.Equal(states[0], rig.State());
        for (int k = 3; k <= 5; k++)
        {
            var (r, st) = Oracle("vorbis_noprefetch_small_buffer_call" + k);
            Assert.Equal(r[0], rig.Start());
            Assert.Equal(st[0], rig.State());
        }
    }

    [Theory]
    [InlineData("below", 4000, 0)]
    [InlineData("above", 6000, 0)]
    [InlineData("two_buffers", 4000, 4000)]
    public void V7_TheGateComparesTheBytesLeftPlusTheBufferedBytesWithTheNominalBuffering(string name, int first, int second)
    {
        // emu_stream.py vorbis_gate_*: [S+0x44] + the Query sum against nominal = (u32)(12.971f * 380.0f) = 4928: below 3653 -> 0x3F three times; 5653 -> 1; 3653 + 4000 -> 1.
        var wem = ReadWem(VorbisName);
        if (wem is null) return;
        var (results, states) = Oracle("vorbis_gate_" + name);
        var rig = new SourceRig(wem, 0);
        rig.Start();
        rig.Io(0, (uint)first);
        if (second != 0) rig.Rig.Deliver(rig.V!.Stream3C!, (ulong)first, (uint)second);
        var got = new[] { rig.Start(), rig.Start(), rig.Start() };
        Assert.Equal(results, got);
        Assert.Equal(states[0], rig.State());
    }

    [Fact]
    public void V8_TheCallbackBlockReportsTheEstimateAndTheFlagWithoutChangingTheResult()
    {
        // emu_stream.py vorbis_gate_callback: bit 22 of [pbi+4] set; 6000 bytes delivered; both calls return 1 and call 0xA059D8 with (playing id 0x1234, estimate (u32)(float(5653) / 12.971f) = 435, flag 0x11:
        // the buffered amount reached the nominal), 0xA74F00..0xA74FC0 and its copies (0xAB2C4C..0xAB2D18).
        var wem = ReadWem(VorbisName);
        if (wem is null) return;
        var (results, states) = Oracle("vorbis_gate_callback");
        var rig = new SourceRig(wem, 0, flags4: 0x400000);
        rig.Start();
        rig.Io(0, 6000);
        Assert.Equal(results, new[] { rig.Start(), rig.Start() });
        Assert.Equal(states[0], rig.State());
        Assert.Equal(new[] { "4660/435/17", "4660/435/17" }, rig.Callbacks);
    }

    [Fact]
    public void V9_AFailedOutputBufferAllocationFailsTheStateThreeAction()
    {
        // emu_stream.py vorbis_output_buffers_fail (0xAB3264 returning -1): the prefix path returns 2 (0xAB1F28..0xAB1F2C movne r0,#2); the state is otherwise that of prefix_covers.
        var wem = ReadWem(VorbisName);
        if (wem is null) return;
        var (results, states) = Oracle("vorbis_output_buffers_fail");
        var rig = new SourceRig(wem, 600) { OutFail = true };
        Assert.Equal(results[0], rig.Start());
        Assert.Equal(states[0], rig.State());
    }

    // ------------------------------------------------------------------ D4: the arguments 0xA74564 gives the stream manager

    [Theory]
    [InlineData("by_id", 0x03, 0x02, 0x3F800000u, 1u, 99908739u, 0u, 1, false)]
    [InlineData("by_id_cache_minus_one_bit0", 0x03, 0x03, 0x3F800000u, 1u, 0xFFFFFFFFu, 1u, 1, false)]    // byte[src+0xD] & 9 != 0: cache id -1; its bit 0 is the company id
    [InlineData("by_id_cache_minus_one_bit3", 0x03, 0x0A, 0x3F800000u, 1u, 0xFFFFFFFFu, 0u, 1, false)]
    [InlineData("by_id_lang_clear", 0x02, 0x02, 0x3F800000u, 1u, 99908739u, 0u, 0, false)]
    [InlineData("by_name", 0x03, 0x06, 0x3F800000u, 1u, 99908739u, 0u, 1, true)]                         // byte[src+0xD] & 4: open by name (manager vt+0x18), the name copied into the record
    public void D4_TheFlagsAndHeuristicsOfTheStreamCreationAreTheEngines(string name, int bits0c, int bits0d, uint priorityBits, uint priority, uint cache, uint company, int lang, bool byName)
    {
        // emu_stream.py create_args_*: the real 0xA74564 -> CreateAuto -> the open record: company id byte[src+0xD] & 1, codec id [src+0x14] >> 16 (4), language byte[src+0xC] & 1, the third flag byte
        // (byte[src+0xD] >> 1) & 1 = 1, +0x12 = 1 (CreateAuto), the cache id -1 when byte[src+0xD] & 9 else [src+4], no prefetch; priority (int)float[pbi+0x1C0] = 1; the stream's throughput
        // 12.971 (float(avg bytes) / 1000, 0xAB14AC) and its minimum buffers byte 1 (a zero byte is stored as 1, 0x964848).
        var wem = ReadWem(VorbisName);
        if (wem is null) return;
        var rig = new SourceRig(wem, 600, bits0c: (byte)bits0c, bits0d: (byte)bits0d, name: byName ? "snd/test.wem" : null);
        Assert.Equal(1, rig.Start());
        var s = rig.V!.Stream3C!;
        Assert.Equal(((byte)priority, cache), (s.Priority2C, s.CacheId40));
        var rec = s.Record0C!;
        Assert.Equal(0u, rec.Mode);
        Assert.Equal(byName ? "snd/test.wem" : null, rec.Name);
        if (!byName) Assert.Equal(99908739u, rec.FileId);
        var f = rec.Flags!;
        Assert.Equal((company, 4u, (byte)lang, (byte)1, (byte)1, cache, 0u), (f.CompanyId, f.CodecId, f.LanguageSpecific, f.Byte11, f.Byte12, f.CacheId, f.PrefetchBytes));
        Assert.Equal((1095731511u, 1u, (byte)1), (BitConverter.SingleToUInt32Bits(s.Throughput44), s.MinBuffer58, s.Byte6D));
    }

    [Fact]
    public void D5_APriorityOutsideTheByteRangeFailsCreateAutoWith0x1F()
    {
        // emu_stream.py create_args_priority_255 / priority_negative: (int)255.0f and (int)-5.0f are bytes 0xFF and 0xFB, above 0x64: CreateAuto returns 0x1F (0x95F354..0x95F35C) and 0xA74564 returns it; no stream.
        var wem = ReadWem(VorbisName);
        if (wem is null) return;
        Assert.Equal(0x1F, new SourceRig(wem, 600, priorityBits: 0x437F0000).Start());
        Assert.Equal(0x1F, new SourceRig(wem, 600, priorityBits: 0xC0A00000).Start());
    }

    // ------------------------------------------------------------------ V10: the decode body's buffering gate

    [Theory]
    [InlineData("below_nominal", 4000, 0x44u, 0u, false)]
    [InlineData("above_nominal", 6000, 0x44u, 0u, false)]
    [InlineData("below_nominal_callback", 4000, 0x44u, 0x400000u, false)]
    [InlineData("above_nominal_callback", 6000, 0x44u, 0x400000u, false)]
    [InlineData("gate_clear", 4000, 0x04u, 0u, false)]
    [InlineData("gate_clear_callback", 4000, 0x04u, 0x400000u, false)]
    [InlineData("error_status", 4000, 0x44u, 0u, true)]
    [InlineData("error_status_callback", 4000, 0x44u, 0x400000u, true)]
    public void V10_TheDecodeGateIsTheEnginesOnReachedAndTheStoredCode(string name, int size, uint flags1BD, uint flags4, bool kill)
    {
        // emu_stream.py vorbis_gate7_*: the real 0xAB1550 with 0xAB7E40 / 0xA73490 stubbed: whether the decode is reached, [state+0x28] when it is not, [S+0x10] after (bit 1 cleared for good once the gate passes,
        // 0xAB1A34..0xAB1A44) and the callbacks. Below the nominal: status code 0x2E and no decode (the callback block first when bit 22 is set); status 2: 2.
        var wem = ReadWem(VorbisName);
        if (wem is null) return;
        var expected = StreamSourceOracle.Gate7[name];
        var rig = new SourceRig(wem, 0, flags1BD: (byte)flags1BD, flags4: flags4);
        rig.Start();
        rig.Io(0, (uint)size);
        rig.Start();
        if (kill) rig.V!.Stream3C!.Kill962028();
        rig.Callbacks.Clear();
        var gate = rig.V!.DecodeGateAB1550();
        Assert.Equal(expected.Reached, gate.Decode);
        if (!expected.Reached) Assert.Equal(expected.Code, gate.Result);
        Assert.Equal(expected.F10, rig.V.Flags10);
        Assert.Equal(expected.Callbacks, string.Join(";", rig.Callbacks));
    }

    // ------------------------------------------------------------------ V12: the decode loop of 0xAB1550 on real data through the real stream layer

    private static string Sha16Block(Array? data, int frames, int bytesPerFrame)
    {
        if (data is null || frames == 0) return "-";
        var bytes = new byte[frames * bytesPerFrame];
        Buffer.BlockCopy(data, 0, bytes, 0, bytes.Length);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant()[..16];
    }

    private static string IoString(WwiseDecodeState st, bool adpcm, WwisePlayingInstance pbi)
    {
        int bpf = adpcm ? (pbi.Byte160 | (pbi.Byte161 << 8)) >> 6 & 0x3FF : 4 * (int)(st.ChannelConfig & 0xFF);
        return $"code={st.Code28} f={st.ValidFrames} mx={st.MaxFrames} cc={st.ChannelConfig:x} pos={st.Position} tot={st.Total} rate={st.Rate} mc={st.MarkerCount} h={Sha16Block(st.Data, st.ValidFrames, bpf)}";
    }

    private static string StreamSrcString(WwiseVorbisStreamSource v)
        => $"S18={v.SampleBase18} h38={v.LoopCount38} h5c={v.LoopCounter5C} b5e={v.Bits5E & 0x1F} L44={v.Left44} f48={v.FileOffset48} | st={v.State68} fr={v.Frame.Frames} fs={v.Status64} fc={v.Consumed6C} sk={v.LoopStateSkip2C} tr={v.LoopStateWord2E} se={v.Frame.Dsp.Start}/{v.Frame.Dsp.End} | a4={(v.OutputA4 is null ? 0 : 1)}";

    private static System.Text.Json.JsonElement Json(string s) => System.Text.Json.JsonDocument.Parse(s).RootElement;

    [Fact]
    public void V12_TheDecodeLoopOnRealDataMatchesTheEngineThroughTheRealStreamLayer()
    {
        // emu_decode_stream.py stream_cases: the real 0xAB1550 (the packet collection of G3, the frame loop 0xAB7E40 per packet, the hand-off 0xA73490) on the shipped media through the real stream layer, with the real setup cache and
        // decoder-state allocation: the code, the io state (frames, config, position, total, rate, markers, the SHA-256 of the delivered block) and the source's state after every call. It covers the first calls, the whole file in three
        // buffers to the 0x11 of the last frames, a packet spanning the end of a buffer (the owned copy, 0x2E until the rest arrives), a split size prefix, a corrupt packet size (status 2) and a failing output allocation.
        foreach (var (name, (prm, steps)) in WwiseVorbisEngineOracle.Stream.Where(x => x.Value.Params.Contains("\"calls\"")))
        {
            var j = Json(prm);
            var wem = ReadWem(j.GetProperty("file").GetString()!)!;
            int corrupt = j.GetProperty("corrupt").GetInt32();
            if (corrupt != 0) { wem[corrupt] = 0xFF; wem[corrupt + 1] = 0xFF; }
            var buffers = j.GetProperty("buffers").EnumerateArray().Select(b => (Start: b[0].GetInt32(), Size: b[1].GetInt32())).ToArray();
            var rig = new SourceRig(wem, 0, flags1BD: 0x04);
            rig.Start();
            rig.Io((uint)buffers[0].Start, (uint)buffers[0].Size);
            int r = rig.Start();
            var got = new List<string> { $"start={r} | {StreamSrcString(rig.V!)}" };
            var stream = rig.V!.Stream3C!;
            foreach (var b in buffers.Skip(1)) rig.Rig.Deliver(stream, (ulong)b.Start, (uint)b.Size);
            int failAlloc = j.GetProperty("fail_alloc").GetInt32();
            if (failAlloc != 0) { int n = 0; rig.Rig.Manager.AllocationFails = () => ++n == failAlloc; }
            var extra = j.GetProperty("extra");
            var state = new WwiseDecodeState { Code28 = 0 };
            state.Code28 = 0;
            for (int i = 0; i < j.GetProperty("calls").GetInt32(); i++)
            {
                if (extra.TryGetProperty(i.ToString(), out var more))
                    foreach (var b in more.EnumerateArray()) rig.Rig.Deliver(stream, (ulong)b[0].GetInt32(), (uint)b[1].GetInt32());
                state.Code28 = 0xABCD;
                state.MaxFrames = 1024;
                rig.V.DecodeAB1550(state);
                got.Add(IoString(state, false, rig.Pbi) + " | " + StreamSrcString(rig.V));
            }
            Assert.True(steps.SequenceEqual(got), $"{name}:\n" + string.Join("\n", steps.Select((s2, k) => k < got.Count && got[k] == s2 ? null : $"  [{k}] engine {s2}\n  [{k}] C#     {(k < got.Count ? got[k] : "(none)")}").Where(x => x is not null).Take(3)));
        }
    }

    [Fact]
    public void V07_V19_TheLoopEndSlotAndTheSeekLookupOfTheStreamedClassMatchTheEngine()
    {
        // emu_decode_stream.py stream_vt74_cases: 0xAB1138 (vt+0x74: r1 != 0 decrements the loop count above 1 and returns 0x11; r1 == 0 also decrements u16[S+0x5C], resets the decoder with the loop skip and the end trim
        // (u16[S+0xCE] when the loop count is 1, else u16[S+0xC2]), sets [S+0x64] = 0x2D and [S+0x68] = 3) and 0xAB1020 (vt+0x7C: the seek-table walk, the first entry's offset is the first audio packet).
        var wem = ReadWem(VorbisName)!;
        foreach (var (name, (prm, steps)) in WwiseVorbisEngineOracle.Stream.Where(x => x.Key.StartsWith("sv_vt74_")))
        {
            int loops = Json(prm).GetProperty("loops").GetInt32();
            var got = new List<string>();
            foreach (int r1 in new[] { 1, 0, 0, 1, 0 })
            {
                var rig = new SourceRig(wem, 600, loop: (ushort)loops, flags1BD: 0x04);
                Assert.Equal(1, rig.Start());
                rig.V!.LoopCounter5C = 0;
                int ret = rig.V.LoopOrEnd74(r1);
                got.Add($"r1={r1} ret={ret:x} h38={rig.V.LoopCount38} h5c={rig.V.LoopCounter5C} st={rig.V.State68} fs={rig.V.Status64} sk={rig.V.LoopStateSkip2C} tr={rig.V.LoopStateWord2E} se={rig.V.Frame.Dsp.Start}/{rig.V.Frame.Dsp.End}");
            }
            var seqRig = new SourceRig(wem, 600, loop: (ushort)loops, flags1BD: 0x04);
            Assert.Equal(1, seqRig.Start());
            got.Add("seq " + string.Join(" ", new[] { 0, 0, 0, 1, 0 }.Select(r1 => { int ret = seqRig.V!.LoopOrEnd74(r1); return $"{r1}:{ret:x}/{seqRig.V.LoopCount38}/{seqRig.V.LoopCounter5C}"; })));
            Assert.True(steps.SequenceEqual(got), $"{name}: engine [{string.Join(" | ", steps)}] C# [{string.Join(" | ", got)}]");
        }
        var seek = WwiseVorbisEngineOracle.Stream["sv_seek_lookup"].Steps;
        var rigS = new SourceRig(wem, 600, loop: 1, flags1BD: 0x04);
        Assert.Equal(1, rigS.Start());
        var gotSeek = new List<string>();
        foreach (uint pos in new uint[] { 0, 1, 500, 4095, 4096, 9000, 20000, 33905, 33906, 40000 })
        {
            var (ret, s, b) = rigS.V!.VtSeekLookup7C(pos);
            gotSeek.Add($"pos={pos} ret={ret} s={s} b={b}");
        }
        rigS.V!.SeekTable = null;
        var (r2, s2, b2) = rigS.V.VtSeekLookup7C(5);
        gotSeek.Add($"notable ret={r2} s={s2} b={b2}");
        Assert.Equal(seek, gotSeek);
    }

    [Fact]
    public void V11_TheUnopenedStreamsStaleWordDecidesTheGateAndIsTheSeams()
    {
        // emu_stream.py vorbis_gate7_unopened: Query leaves the caller's out variable unwritten for a stream that is not opened, so the gate adds the stack's stale word: with a word below the nominal
        // (the oracle's stack held one) the decode is blocked with 0x2E; a seam word above the nominal opens the gate. The word is the seam's (C33.4).
        var wem = ReadWem(VorbisName);
        if (wem is null) return;
        var expected = StreamSourceOracle.Gate7["unopened"];
        var rig = new SourceRig(wem, 0, stale: 0);
        rig.Start();
        var gate = rig.V!.DecodeGateAB1550();
        Assert.Equal((expected.Reached, expected.Code), (gate.Decode, gate.Result));
        Assert.Equal(expected.F10, rig.V.Flags10);
        var other = new SourceRig(wem, 0, stale: 0x10000);
        other.Start();
        Assert.True(other.V!.DecodeGateAB1550().Decode);
    }

    // ------------------------------------------------------------------ the missing bodies

    [Fact]
    public void X1_ABodyThatNoRowReadsStopsTheStreamInsteadOfBeingGuessed()
    {
        // 0xA059D8 (the buffering callback) and 0xA75E34 (the PCM decode, outline only in C36) are not read, and the packed codebook library is a host input: each throws WwiseMissingBehaviourException when reached without its
        // supply (MISSING in the report). The walker, the setup cache, the decoder-state allocation, 0xA736D4, 0xAB1020 and 0xA73ABC are built and no longer stops.
        var wem = ReadWem(VorbisName);
        if (wem is null) return;
        Assert.Throws<WwiseMissingBehaviourException>(() => new SourceRig(wem, 600, noCodebook: true).Start());
        var cb = new SourceRig(wem, 0, flags4: 0x400000, noCallback: true);
        cb.Start();
        cb.Io(0, 6000);
        Assert.Throws<WwiseMissingBehaviourException>(() => cb.Start());
        var adpcm = ReadWem(AdpcmName)!;
        var pcm = new SourceRig(adpcm, 600, vorbis: false, bits0c: 0x02);
        var pcmSource = new WwisePcmAdpcmStreamSource(pcm.Rig.Manager, pcm.Pbi, pcm.Block, pcm.Seams) { Class = WwisePcmAdpcmClass.PcmStream };
        Assert.Throws<WwiseMissingBehaviourException>(() => pcmSource.DecodeA73D34(new WwiseDecodeState()));
    }

    [Fact]
    public void V20_ABitSevenStartOffsetRunsTheRealStartPositionAndSeekThroughTheStreamStartLikeTheEngine()
    {
        // emu_decode_stream.py stream_start_offset_cases: bit 7 of [pbi+0x1BD] with an offset (samples, or a float fraction with bit 0 of [pbi+0x1BE]): 0xAB2088 -> 0xA74BA0 -> 0xA736D4 -> 0xAB1020 -> the stream's SetPosition: the
        // result of the second StartStream, the source state, the PBI's bits (7 of +0x1BD and 0, 1 of +0x1BE cleared, [pbi+0x1B4] = pos - the samples of the seek point) and the stream's position.
        foreach (var (name, (prm, steps)) in WwiseVorbisEngineOracle.Stream.Where(x => x.Key.StartsWith("sv_start_offset_")))
        {
            var j = Json(prm);
            var wem = ReadWem(j.GetProperty("file").GetString()!)!;
            var rig = new SourceRig(wem, 0, flags1BD: 0xC4);
            rig.Pbi.Word1B4 = j.GetProperty("w1b4").GetUInt32();
            rig.Pbi.Flags1BE = (byte)j.GetProperty("f1be").GetInt32();
            rig.Start();
            rig.Io(0, 4096);
            int r = rig.Start();
            var s0 = rig.V!.Stream3C!;
            string got = $"start={r} | {StreamSrcString(rig.V)} | pbi1b4={rig.Pbi.Word1B4} pbi1bd={rig.Pbi.Flags1BD} pbi1be={rig.Pbi.Flags1BE} sp={s0.Position38}";
            Assert.Equal(steps[0], got);
        }
    }

    // ------------------------------------------------------------------ the ADPCM / PCM stream classes (F7)

    [Theory]
    [InlineData("prefix", 600, 1, 0x44, 0x02, false)]
    [InlineData("prefix_loop_infinite", 600, 0, 0x44, 0x02, false)]
    [InlineData("noprefetch", 0, 1, 0x44, 0x02, true)]
    [InlineData("noprefetch_gate_off", 0, 1, 0x04, 0x02, true)]
    [InlineData("prefetch_bit_clear", 600, 1, 0x44, 0x01, true)]
    [InlineData("start_offset_flag", 600, 1, 0xC4, 0x02, false)]
    public void A1_TheStreamClassStartStreamMatchesTheEngine(string name, int prefixLen, int loop, int flags1BD, int bits0c, bool io)
    {
        // emu_stream.py adpcm_*: the real 0xA7538C of the 0xA74244 class with the real header parse 0xA73ABC (A01: batch 5e): the prefix case returns the stream's Start result with no gate (0xA75020);
        // with no prefetch the first call is 0x3F (GetBuffer 0x2E, 0xA74D08), later calls (after 4096 bytes) pass the gate with throughput 1.0f (nominal 380) -> 1.
        var wem = ReadWem(AdpcmName);
        if (wem is null) return;
        var (results, states) = Oracle("adpcm_" + name);
        var rig = new SourceRig(wem, prefixLen, vorbis: false, loop: (ushort)loop, flags1BD: (byte)flags1BD, bits0c: (byte)bits0c);
        var got = new List<int> { rig.Start() };
        string first = rig.State();
        if (io)
        {
            rig.Io(0, 4096);
            got.Add(rig.Start()); got.Add(rig.Start()); got.Add(rig.Start());
        }
        Assert.Equal(results, got);
        Assert.Equal(states[0], first);
        Assert.Equal(states[^1], rig.State());
    }

    [Fact]
    public void A06_ThePcmHeaderParseMatchesTheEngineOnHandBuiltFiles()
    {
        // emu_stream.py sc_pcm: the real 0xA76140 class's StartStream with the real header parse 0xA75BC4 (C36.2 A06; no shipped bank has a PCM source, so the files are hand-built RIFFs): the tag must be 0xFFFE else 7;
        // the PBI's format bytes (the bit depth with the low two bits of the block align in +0x160, its upper bits in +0x161), the total as dataSize / blockAlign, the loop bytes without a shift (7 when the loop end lies beyond
        // the data or below the start), [S+0x28] = total - 1 when it is 0 or the loop count is 1, the heuristics float(rate * blockAlign) / 1000.0f and SetMinimalBufferSize(blockAlign).
        Assert.NotEmpty(StreamSourceOracle.PcmCases);
        foreach (var (name, (blob, loop)) in StreamSourceOracle.PcmCases)
        {
            var wem = Convert.FromHexString(blob);
            var (results, states) = Oracle(name);
            var rig = new SourceRig(wem, wem.Length, vorbis: false, pcm: true, loop: (ushort)loop, bits0c: 0x02);
            Assert.Equal(results[0], rig.Start());
            Assert.Equal(states[0], rig.State());
        }
    }

    [Fact]
    public void A2_TheCallbackBlockAndTheSmallBufferGateOfTheStreamClass()
    {
        // emu_stream.py adpcm_callback: bit 22 set, 400 bytes delivered: 0x3F three times (left 336 < the nominal); the callback ran on the 2nd and 3rd call with the estimate (u32)(336 / throughput) where the throughput is the real parse's float(rate * blockAlign) / 64000.0f, flag 1.
        var wem = ReadWem(AdpcmName);
        if (wem is null) return;
        var (results, states) = Oracle("adpcm_callback");
        var rig = new SourceRig(wem, 0, vorbis: false, bits0c: 0x02, flags4: 0x400000);
        var got = new List<int> { rig.Start() };
        rig.Io(0, 400);
        got.Add(rig.Start()); got.Add(rig.Start());
        Assert.Equal(results, got);
        Assert.Equal(states[0], rig.State());
        var cbText = System.Text.RegularExpressions.Regex.Match(states[0], @"cb=\[(.*?)\]").Groups[1].Value;                 // the callbacks the engine's run made: playing id / estimate / flag
        Assert.Equal(cbText, string.Join(";", rig.Callbacks));
        Assert.Equal(2, rig.Callbacks.Count);
    }

    // ------------------------------------------------------------------ the in-memory Vorbis class (0xAB0B20 ... 0xAB0FC0)

    private static string MemSrcString(WwiseVorbisInMemorySource m)
        => $"S18={m.SampleBase18} h38={m.LoopCount38} | st={m.State44} fs={m.Status40} fr={m.Frame.Frames} lf={m.LastFrames88} c8={(m.C8IsSet ? m.CurrentOffsetC8.ToString() : "x")} sk={m.SkipD2C} tr={m.TrimD2E} se={m.Frame.Dsp.Start}/{m.Frame.Dsp.End} | o80={(m.OutputBlock80 is null ? 0 : 1)} c0={(m.SeekCopyC0 is null ? 0 : 1)}";

    private static string PbiString(WwisePlayingInstance p)
        => $"pbi158={p.SourceFormat158} pbi15c={p.Word15C} pbi160={p.Byte160} pbi161={p.Byte161} pbi162={p.Byte162} pbi1b4={p.Word1B4} pbi1bd={p.Flags1BD} pbi1be={p.Flags1BE}";

    private static (WwiseVorbisInMemorySource M, WwisePlayingInstance Pbi, Action<int> ArmFail) NewMem(ushort loops, byte flags1BD, uint w1b4, byte f1be) => NewMemWithContext(loops, flags1BD, w1b4, f1be, out _);

    private static (WwiseVorbisInMemorySource M, WwisePlayingInstance Pbi, Action<int> ArmFail) NewMemWithContext(ushort loops, byte flags1BD, uint w1b4, byte f1be, out WwiseVorbisEngineContext context)
    {
        var pbi = new WwisePlayingInstance(new WwisePlayInitParams { PlayingId = 0x1234, TargetNodeId = 1 }, 1, new WwiseSourceDescriptor(0x00040001u, 1, 5, 0, 0), new byte[0x44], null, continuous: false);
        pbi.Word15C = 0;
        pbi.LoopCount1B8 = loops;
        pbi.Flags1BD = flags1BD;
        pbi.Word1B4 = w1b4;
        pbi.Flags1BE = f1be;
        int failAt = 0, n = 0;
        var ctx = new WwiseVorbisEngineContext { Codebooks = WwiseVorbisEngineTests.Codebooks() };
        var m = new WwiseVorbisInMemorySource(pbi, ctx, () => failAt == 0 || ++n != failAt, baseDestructor: _ => { });   // the base destructor 0xA73304 is unread: a TEST DOUBLE
        context = ctx;
        return (m, pbi, k => { failAt = k; n = 0; });
    }

    [Fact]
    public void V17_V10_TheInMemoryClassMatchesTheEngineThroughStartDecodeLoopAndSeek()
    {
        // emu_decode_stream.py inmem_cases: the real 0xAB0B20 (walker, PBI format bytes, the vorb block copy, the size and loop gates, 0xAB0A30: the seek-table copy, the setup cache, 0xAB3264, the start-offset block with
        // 0xA736D4 and 0xAB04F0) and 0xAB0448 (the packets decoded until one yields frames, 0xA73490) on shipped media: single plays to the 0x11 of the last frames, loop counts 2, 3 and infinite through 0xAB0374's restart,
        // start offsets (zero, in the first entry, mid table, last entry, beyond the total, folded into a loop, as a float), a size mismatch (2), a null pointer (2), a wrong tag (7) and a failing seek-table copy (0x34).
        // The bit 7 clear of the start-offset block is only reached after the lookup: the early returns leave the PBI as it was.
        Assert.NotEmpty(WwiseVorbisEngineOracle.InMemory);
        foreach (var (name, (prm, steps)) in WwiseVorbisEngineOracle.InMemory.Where(x => x.Key.StartsWith("im_") && x.Value.Params.Contains("\"params\"")))
        {
            var j = Json(prm);
            var wem = ReadWem(j.GetProperty("file").GetString()!)!;
            var p = j.GetProperty("params");
            ushort loops = p.TryGetProperty("loops", out var lp) ? (ushort)lp.GetInt32() : (ushort)1;
            byte flags1BD = p.TryGetProperty("flags_1bd", out var fb) ? (byte)fb.GetInt32() : (byte)0x44;
            uint w1b4 = p.TryGetProperty("w1b4", out var w1) ? (uint)w1.GetInt64() : 0u;
            byte f1be = p.TryGetProperty("f1be", out var f1) ? (byte)f1.GetInt32() : (byte)0;
            var (m, pbi, armFail) = NewMem(loops, flags1BD, w1b4, f1be);
            if (p.TryGetProperty("fail_alloc", out var fa) && fa.GetInt32() != 0) armFail(fa.GetInt32());
            uint size = p.TryGetProperty("size", out var sz) && sz.ValueKind == System.Text.Json.JsonValueKind.Number ? (uint)sz.GetInt32() : (uint)wem.Length;
            bool data = !p.TryGetProperty("data", out var dt) || dt.ValueKind != System.Text.Json.JsonValueKind.False;
            int r = m.StartStreamAB0B20(data ? new WwiseBytePtr(wem, 0) : default, size);
            var got = new List<string> { $"start={r} | {MemSrcString(m)} | {PbiString(pbi)}" };
            int calls = p.TryGetProperty("calls", out var cn) ? cn.GetInt32() : 0;
            var state = new WwiseDecodeState();
            for (int i = 0; i < calls; i++)
            {
                state.Code28 = 0xABCD;
                state.MaxFrames = 1024;
                m.DecodeAB0448(state);
                got.Add(IoString(state, false, pbi) + " | " + MemSrcString(m));
            }
            Assert.True(steps.SequenceEqual(got), $"{name}:\n" + string.Join("\n", steps.Select((s2, k) => k < got.Count && got[k] == s2 ? null : $"  [{k}] engine {s2}\n  [{k}] C#     {(k < got.Count ? got[k] : "(none)")}").Where(x => x is not null).Take(3)));
        }
    }

    [Fact]
    public void V07_V08_TheInMemoryLoopEndSlotAndTheOutputReleaseMatchTheEngine()
    {
        var wem = ReadWem("289339243.wem")!;
        var steps = WwiseVorbisEngineOracle.InMemory["im_vt74"].Steps;
        var got = new List<string>();
        foreach (int loops in new[] { 1, 2, 3, 0 })
            foreach (int r1 in new[] { 1, 0 })
            {
                var (m, pbi, _) = NewMem((ushort)loops, 0x44, 0, 0);
                Assert.Equal(1, m.StartStreamAB0B20(new WwiseBytePtr(wem, 0), (uint)wem.Length));
                int ret = m.LoopOrEnd74(r1);
                got.Add($"loops={loops} r1={r1} ret={ret:x} | {MemSrcString(m)}");
            }
        Assert.Equal(steps, got);
        var rel = WwiseVorbisEngineOracle.InMemory["im_release"].Steps;
        var (m2, pbi2, _) = NewMem(1, 0x44, 0, 0);
        Assert.Equal(1, m2.StartStreamAB0B20(new WwiseBytePtr(wem, 0), (uint)wem.Length));
        var st = new WwiseDecodeState { MaxFrames = 1024 };
        m2.DecodeAB0448(st);
        var gotRel = new List<string> { "before | " + MemSrcString(m2) };
        m2.ReleaseOutputAB032C();
        gotRel.Add("after_release | " + MemSrcString(m2));
        m2.ReleaseOutputAB032C();
        gotRel.Add("release_again | " + MemSrcString(m2));
        Assert.Equal(rel, gotRel);
    }

    private static string CacheState(WwiseVorbisSetupCache c) => $"bc={c.BucketCount} n={c.NodeCount} arr={(c.HasBuckets ? 1 : 0)} dump={string.Join("|", c.DumpChains())}";

    [Fact]
    public void V22_TheDestructorsReleaseTheOutputAndTheSetupRecordLikeTheEngine()
    {
        // emu_decode_stream.py destroy_cases: 0xAB11BC (streamed: frees [S+0xA4], clears [S+0x60] / [S+0xA4], releases the setup record 0xAB3120 for the key [S+0xD8], then the base destructor 0xA75A0C) and 0xAB02E0
        // (in-memory: only the setup record, [S+0x5C] tested, then 0xA73304; the output block stays for vt+0xC). The base destructors are unread: a seam (a test double here); the engine's own run in the oracle executes them.
        var wem = ReadWem(VorbisName)!;
        var rig = new SourceRig(wem, 0, flags1BD: 0x04);
        rig.Start();
        rig.Io(0, 4096);
        rig.Start();
        var st = new WwiseDecodeState { MaxFrames = 1024 };
        rig.V!.DecodeAB1550(st);
        var cache = rig.Seams.Vorbis.SetupCache;
        var steps = WwiseVorbisEngineOracle.Stream["dt_streamed"].Steps;
        Assert.Equal(steps[0], $"before | {CacheState(cache)} | a4={(rig.V.OutputA4 is null ? 0 : 1)} a60={rig.V.Frame.Frames} s80={(rig.V.CodebookHandle80 is null ? 0 : 1)}");
        rig.V.DestroyAB11BC();
        Assert.Equal(steps[1], $"after | {CacheState(cache)} | a4={(rig.V.OutputA4 is null ? 0 : 1)} a60={rig.V.Frame.Frames}");

        var small = ReadWem("289339243.wem")!;
        var (m, pbi, _) = NewMemWithContext(1, 0x44, 0, 0, out var ctx);
        Assert.Equal(1, m.StartStreamAB0B20(new WwiseBytePtr(small, 0), (uint)small.Length));
        var st2 = new WwiseDecodeState { MaxFrames = 1024 };
        m.DecodeAB0448(st2);
        var isteps = WwiseVorbisEngineOracle.Stream["dt_inmem"].Steps;
        Assert.Equal(isteps[0], $"before | {CacheState(ctx.SetupCache)} | o80={(m.OutputBlock80 is null ? 0 : 1)} s5c={(m.Setup5C is null ? 0 : 1)}");
        m.DestroyAB02E0();
        Assert.Equal(isteps[1], $"after | {CacheState(ctx.SetupCache)} | o80={(m.OutputBlock80 is null ? 0 : 1)}");
    }

    // ------------------------------------------------------------------ the ADPCM stream class: decode, seek lookup, loop slot

    private static string AdpcmSrcString(WwisePcmAdpcmStreamSource a)
        => $"S18={a.SampleBase18} h38={a.LoopCount38} h5c={a.LoopCounter5C} b5e={a.Bits5E & 0x1F} L44={a.Left44} f48={a.FileOffset48} f10={a.Flags10} | pb={a.PartialBytes6C} bk={a.BlockAlign60} o64={(a.Output64 is null ? 0 : 1)}";

    [Fact]
    public void A02_TheAdpcmDecodeMatchesTheEngineThroughTheRealStreamLayer()
    {
        // emu_decode_stream.py adpcm_cases: the real 0xA73D34 after the real header parse 0xA73ABC (the PBI's format bytes, the loop bytes, the heuristics, SetMinimalBufferSize(channels * 36)) on the shipped ADPCM stream:
        // the buffering gate, the output block of bytes-per-frame * 1024 bytes, the decoder 0xA7A194 per channel at 36 bytes apart, the partial block carried in a channels * 36 byte buffer when fewer than a block remain
        // and completed from the next buffer (the frames are 64 * (blocks + 1) then, A02's corrected count), the consumed buffer released, 0xA73490's io state; failing output and carry allocations give code 2.
        var wem = ReadWem(AdpcmName)!;
        foreach (var (name, (prm, steps)) in WwiseVorbisEngineOracle.Adpcm.Where(x => x.Value.Params.Contains("\"calls\"")))
        {
            var j = Json(prm);
            var buffers = j.GetProperty("buffers").EnumerateArray().Select(b => (Start: b[0].GetInt32(), Size: b[1].GetInt32())).ToArray();
            var rig = new SourceRig(wem, 0, vorbis: false, bits0c: 0x02, flags1BD: (byte)j.GetProperty("flags_1bd").GetInt32(), flags4: j.GetProperty("flags4").GetUInt32());
            var r = new List<int> { rig.Start() };
            rig.Io((uint)buffers[0].Start, (uint)buffers[0].Size);
            r.Add(rig.Start());
            var stream = rig.A!.Stream3C!;
            foreach (var b in buffers.Skip(1)) rig.Rig.Deliver(stream, (ulong)b.Start, (uint)b.Size);
            var got = new List<string> { $"start={string.Join(",", r)} | {AdpcmSrcString(rig.A)} | {PbiString(rig.Pbi)}" };
            int failAlloc = j.GetProperty("fail_alloc").GetInt32();
            if (failAlloc != 0) { int n = 0; rig.Rig.Manager.AllocationFails = () => ++n == failAlloc; }
            var extra = j.GetProperty("extra");
            var state = new WwiseDecodeState();
            for (int i = 0; i < j.GetProperty("calls").GetInt32(); i++)
            {
                if (extra.TryGetProperty(i.ToString(), out var more))
                    foreach (var b in more.EnumerateArray()) rig.Rig.Deliver(stream, (ulong)b[0].GetInt32(), (uint)b[1].GetInt32());
                state.Code28 = 0xABCD;
                state.MaxFrames = 1024;
                rig.A.DecodeA73D34(state);
                got.Add(IoString(state, true, rig.Pbi) + " | " + AdpcmSrcString(rig.A));
            }
            Assert.True(steps.SequenceEqual(got), $"{name}:\n" + string.Join("\n", steps.Select((s2, k) => k < got.Count && got[k] == s2 ? null : $"  [{k}] engine {s2}\n  [{k}] C#     {(k < got.Count ? got[k] : "(none)")}").Where(x => x is not null).Take(3)));
        }
    }

    [Fact]
    public void A03_A05_TheAdpcmSeekLookupAndLoopEndSlotMatchTheEngine()
    {
        var wem = ReadWem(AdpcmName)!;
        var rig = new SourceRig(wem, 600, vorbis: false, bits0c: 0x02);
        Assert.Equal(1, rig.Start());
        var got = new List<string>();
        foreach (uint pos in new uint[] { 0, 1, 63, 64, 65, 1000, 10000, 20000 })
        {
            var (ret, s, b) = rig.A!.VtSeekLookup7C(pos);
            got.Add($"pos={pos} ret={ret} s={s} b={b}");
        }
        Assert.Equal(WwiseVorbisEngineOracle.Adpcm["ad_seek_lookup"].Steps, got);
        got.Clear();
        foreach (int loops in new[] { 1, 2, 3, 0 })
            foreach (int r1 in new[] { 1, 0, 0, 0 })
            {
                var rg = new SourceRig(wem, 600, vorbis: false, bits0c: 0x02, loop: (ushort)loops);
                Assert.Equal(1, rg.Start());
                rg.A!.LoopCounter5C = 0;
                int ret = rg.A.LoopOrEnd74(r1);
                got.Add($"loops={loops} r1={r1} ret={ret:x} h38={rg.A.LoopCount38} h5c={rg.A.LoopCounter5C}");
            }
        Assert.Equal(WwiseVorbisEngineOracle.Adpcm["ad_vt74"].Steps, got);
    }

    // ------------------------------------------------------------------ the live entry: the voice source adapters and AddSrc

    private static string? FindCodebooks()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null)
        {
            string p = Path.Combine(d.FullName, "third-party", "ww2ogg", "packed_codebooks_aoTuV_603.bin");
            if (File.Exists(p)) return p;
            d = d.Parent;
        }
        Assert.Fail("third-party/ww2ogg/packed_codebooks_aoTuV_603.bin is not present: these tests do not pass silently without it");
        return null;
    }

    [Fact]
    public void L1_TheStreamedAdapterReturnsTheRawResultsThroughAddSrcAndTheRetryDrivesItToOne()
    {
        // The live path: WwisePlaybackBridge.AddSrc -> 0xA56650 -> the adapter's vt+0x28 -> 0xAB22D4. AddSrc returns the raw result (0x3F for a prefix one byte short: the source is kept pending), the
        // voice pass retries through 0xA56650 (the latch is clear), and once the stream layer has delivered enough the result is 1 and the latch is set. The PBI's own format bytes are written by 0xAB12B4 inside
        // StartStream, so the bridge's SourceFormatWriter15C seam is NOT needed (it is left unset, which throws if asked).
        var wem = ReadWem(VorbisName);
        string? books = FindCodebooks();
        if (wem is null || books is null) return;
        var media = WwiseMedia.Parse(wem);
        var codebooks = WwiseCodebookLibrary.Load(books);
        var bridge = new WwisePlaybackBridge().WithTestSeams();
        bridge.SourceFormatWriter15C = null;
        var rig = new StreamRig(fileSize: wem.Length, io: new ManualIo { StaleQueryWord = 0 }, content: wem);
        var memory = new WwiseBankMemory();
        var pbi = bridge.CreatePbi(new WwisePlayInitParams { PlayingId = 0x1234, TargetNodeId = 1 }, 1, new WwiseSourceDescriptor(WwiseSourceFactory.VorbisPlugin, 1, 99908739, 0, 0), continuous: false);
        pbi.Word15C = 0;
        uint address = memory.Allocate(346 + 16)!.Value;
        Array.Copy(wem, memory.Block(address), 346);
        pbi.Word1DC = address; pbi.Word1E0 = 346;
        var setups = new List<string>();
        var vorbisCtx = new WwiseVorbisEngineContext { OnSetupAcquire = (bytes, flag) => setups.Add($"{bytes.Length}/{flag}") };
        var seams = new WwiseStreamSourceSeams
        {
            Memory = memory,
            Vorbis = vorbisCtx,
        };
        bridge.MediaFor = id => media;
        bridge.Codebooks = codebooks;
        bridge.StreamingFor = p => new WwiseStreamingContext
        {
            Manager = rig.Manager, Pbi = p, Seams = seams,
            Block = new WwiseSourceBlock150 { SourceId04 = 99908739, Bits0C = 3, Bits0D = 2, Plugin14 = 0x00040001 },
        };
        var voice = new WwiseLiveVoice(1, 16);
        Assert.Equal(0x3F, bridge.AddSrc(voice, pbi, bActive: false));                                       // prefix one byte short
        var source = Assert.IsType<WwiseVorbisVoiceSource>(voice.Pending);
        Assert.False(source.StartStreamSucceeded);
        Assert.NotNull(source.StreamSource!.Stream3C);
        var stream = source.StreamSource.Stream3C!;
        stream.RunOpen963890();
        rig.Deliver(stream, 346, 4096);
        rig.Deliver(stream, 346 + 4096, 4096);
        Assert.Equal(1, WwiseVoiceSourceStart.StartA56650(source, pbi.Read1DC(), pbi.Read1E0(), out bool ran));   // the voice pass retry
        Assert.True(ran);
        Assert.True(source.StartStreamSucceeded);
        Assert.Equal(48000u, pbi.SourceFormat158);                                                          // written inside StartStream (0xAB1384)
        Assert.Equal(0x4101u, pbi.Word15C);
        Assert.Equal(new[] { "219/0" }, setups);
        // vt+0x30: the gate of 0xAB1550 passes with 8191 >= 4928 bytes buffered, then the engine's own decode runs (the packet loop 0xAB7E40 and the hand-off 0xA73490): the priming packet gives no frames and the first output
        // packet 128 (the packet value, not the 1024 the voice asks for; the oracle's first call of this medium: emu_decode_stream.py sv_first_calls, f=128), which the live voice's pitch node (WwiseLiveVoice.Render: 0xA44630, 0xA53134,
        // 0xA52D4C, C38.1) takes: this medium is 48000 Hz mono float, so the node's resampler is the float bypass 0xA479F4 (a plain copy); [pbi+0x1D8] = -F*P (the voice pass 0xA55228..0xA55244 leaves D in [-F*P, 0)) writes no silent frames.
        // The node releases each source block through vt+0xC (0xAB1100); the voice's loop ends when the stream layer has nothing more (0x2E, the not-ready handler 0xA55C14). Every delivered frame is the existing offline decode, bit for bit.
        pbi.StartOffset = unchecked((uint)-1024);
        var live = new WwiseLiveVoice(1, 1024) { Source = source, Pbi388 = pbi };
        live.SourceNotReadyA55C14 = (_, _) => { };
        Assert.True(live.StartResamplerA5321C());                                                           // 0xA54A30 -> 0xA5321C with the format the StartStream wrote
        live.Buffer.InitPassBlockA44A00();
        live.Render();
        int delivered = live.PitchNode.Out.ValidFrames;
        Assert.True(delivered >= 128, $"delivered {delivered}");
        var offline = WwiseVorbisNative.Decode(media, codebooks);
        var data = (float[])live.PitchNode.Out.Data!;
        for (int i = 0; i < delivered; i++) Assert.Equal(BitConverter.SingleToUInt32Bits(offline[i]), BitConverter.SingleToUInt32Bits(data[i]));
        Assert.Null(source.StreamSource.OutputA4);                                                         // the pitch node released the block through vt+0xC (0xAB1100)
        Assert.Equal(0u, live.PitchNode.Out.Position);                                                     // [node+0xA0]: the position of the first frame                                                    // [node+0xA0]: the position of the first frame
    }

    [Fact]
    public void L4_NotReadyCheckDrivesTheStreamedSourceAndItsWindowUsesTheFrameCount()
    {
        // The live path of the voice pass retry: WwiseVoiceLinker.NotReadyCheck (0xA544BC) -> 0xA56650 -> the adapter's StartStream. A result of 0x3F with a non-negative [pbi+0x1D8] stays pending (0x3F); a result of 1
        // compares [pbi+0x1D8] with the window trunc_s32(float((L + 1) * u16[0x1052440]) * ratio + 0.5f) = (1 + 1) * 0x400 = 2048 (C33.3: the 0x3F at 0xA54570 depends on the frame count, not on the I/O thread):
        // emu_notready.py: offset 2047 -> 1, offset 2048 -> 0x3F, and 0x3F from the source with offset 5 -> 0x3F.
        var wem = ReadWem(VorbisName);
        string? books = FindCodebooks();
        if (wem is null || books is null) return;
        var media = WwiseMedia.Parse(wem);
        var codebooks = WwiseCodebookLibrary.Load(books);
        foreach (var (prefixLen, offset, expected) in new[] { (600, 2047, 1), (600, 2048, 0x3F), (346, 5, 0x3F) })
        {
            var rig = new StreamRig(fileSize: wem.Length, io: new ManualIo { StaleQueryWord = 0 }, content: wem);
            var memory = new WwiseBankMemory();
            var pbi = new WwisePlayingInstance(new WwisePlayInitParams { PlayingId = 0x1234, TargetNodeId = 1 }, 1, new WwiseSourceDescriptor(WwiseSourceFactory.VorbisPlugin, 1, 99908739, 0, 0), new byte[0x44], null, false);
            uint address = memory.Allocate(prefixLen + 16)!.Value;
            Array.Copy(wem, memory.Block(address), prefixLen);
            pbi.Word1DC = address; pbi.Word1E0 = (uint)prefixLen;
            pbi.StartOffset = (uint)offset;
            var seams = new WwiseStreamSourceSeams { Memory = memory };
            var source = new WwiseVorbisVoiceSource(WwiseVorbisSourceKind.Streamed, media, codebooks, streaming: new WwiseStreamingContext
            {
                Manager = rig.Manager, Pbi = pbi, Seams = seams,
                Block = new WwiseSourceBlock150 { SourceId04 = 99908739, Bits0C = 3, Bits0D = 2, Plugin14 = 0x00040001 },
            });
            var linker = new WwiseVoiceLinker(new WwiseMixBusHierarchy(), new WwiseOutputDeviceList(), new List<WwiseLiveVoice>(),
                _ => new WwisePbiRouting { Node = new WwiseRoutingNode { Id = 1 } },
                new WwiseVoiceLinkSeams { SourceOwner = _ => pbi, SourceFlag10Bit1 = _ => true });
            var voice = new WwiseLiveVoice(1, 16) { Source = source, Pbi388 = pbi };
            Assert.Equal(expected, linker.NotReadyCheck(voice, pbi));
            Assert.Equal(expected == 0x3F && prefixLen == 346, !source.StartStreamSucceeded);              // the latch is set only by the raw result 1
        }
    }

    [Fact]
    public void L5_TheInMemoryAdapterPlaysAWholeMediumThroughTheLiveVoiceToTheLastFramesAndEqualsTheResampledOfflineDecode()
    {
        // The live voice entry for the in-memory class: WwiseVoiceSourceStart.StartA56650 -> the adapter's vt+0x28 -> 0xAB0B20 (the bank-memory address resolved), then WwiseLiveVoice.Render passes (the engine's order 0xA44630: the source's
        // vt+0x30 = 0xAB0448 with the frames per call 128 / 576 / 1024, the pitch node's intake 0xA52D4C and consumption 0xA52DA8, the release 0xA52800 after each pass): the last call carries the last frames with the result 0x11.
        // The medium is 32000 Hz mono float, so the voice's node resamples it to 48000 Hz: SetPitch(0) gives the step 0xAAAB (C38.2 P1-18: 32000 -> 0xAAAB, mode 1) and the float mode-1 kernel 0xA49E40 (C38.2 P1-16c) is
        // out_k = in[i-1] + (f * 2^-16) * (in[i] - in[i-1]) with phase 0x10000 + k * step, i = phase >> 16, f = phase & 0xFFFF, for the outputs with i <= N-1. The expected values are that formula applied to the existing offline
        // decode of M6-002 (itself bit-exact against the engine's decode): the chunking of 128 / 576 / 1024 frames into 1024-frame passes must not change a single bit.
        var wem = ReadWem("289339243.wem")!;
        var media = WwiseMedia.Parse(wem);
        var codebooks = WwiseVorbisEngineTests.Codebooks();
        var rig = new StreamRig(fileSize: wem.Length, content: wem);
        var memory = new WwiseBankMemory();
        uint address = memory.Allocate(wem.Length + 16)!.Value;
        Array.Copy(wem, memory.Block(address), wem.Length);
        var pbi = new WwisePlayingInstance(new WwisePlayInitParams { PlayingId = 0x1234, TargetNodeId = 1 }, 1, new WwiseSourceDescriptor(WwiseSourceFactory.VorbisPlugin, 1, 289339243, 0, 0), new byte[0x44], null, false);
        pbi.Word15C = 0;
        pbi.LoopCount1B8 = 1;                                                                               // a single play (the Sound has no loop property: 0xA1E280 gives 1)
        pbi.StartOffset = unchecked((uint)-1024);                                                           // [pbi+0x1D8] = -F*P: the voice pass 0xA55228..0xA55244 leaves D in [-F*P, 0)
        var seams = new WwiseStreamSourceSeams { Memory = memory };
        var source = new WwiseVorbisVoiceSource(WwiseVorbisSourceKind.InMemory, media, codebooks, streaming: new WwiseStreamingContext
        {
            Manager = rig.Manager, Pbi = pbi, Seams = seams, Block = new WwiseSourceBlock150 { SourceId04 = 289339243, Bits0C = 3, Bits0D = 2, Plugin14 = 0x00040001 },
        });
        Assert.True(source.WritesSourceFormatInStartStream);
        Assert.Equal(1, WwiseVoiceSourceStart.StartA56650(source, address, (uint)wem.Length, out bool ran));
        Assert.True(ran);
        Assert.True(source.StartStreamSucceeded);
        Assert.Equal(32000u, pbi.SourceFormat158);                                                          // written inside StartStream (0xAB0BF0)
        var voice = new WwiseLiveVoice(1, 1024) { Source = source, Pbi388 = pbi };
        voice.SourceNotReadyA55C14 = (_, _) => { };
        voice.StartStreamFormatWriter = (_, _) => { };
        Assert.True(voice.StartResamplerA5321C());
        var all = new List<float>();
        var perPass = new List<int>();
        int result = 0;
        for (int pass = 0; pass < 100 && result != 0x11; pass++)
        {
            voice.Buffer.InitPassBlockA44A00();
            voice.Render();
            result = voice.Buffer.Result;
            Assert.Contains(result, new[] { 0x2B, 0x2D, 0x11, 0x2E });
            var S = voice.Buffer.State;
            if (result is 0x2D or 0x11 && S.ValidFrames != 0)
            {
                perPass.Add(S.ValidFrames);
                all.AddRange(((float[])S.Data!).Take(S.ValidFrames));
            }
            voice.ReleaseChainVtC();                                                                        // 0xA5495C: the chain release ends at the node's 0xA52800
        }
        Assert.Equal(0x11, result);
        Assert.Equal(0xAAABu, voice.PitchNode.Resampler.Step30);                                           // 32000 -> 48000 at 0 cents (C38.2 P1-18)
        Assert.All(perPass.Take(perPass.Count - 1), n => Assert.Equal(1024, n));                           // every full pass hands the voice its 1024 asked frames
        var offline = WwiseVorbisNative.Decode(media, codebooks);
        Assert.Equal(3941, offline.Length);                                                                 // the header's SampleCount
        var expected = new List<float>();
        for (long k = 0; ; k++)
        {
            long phase = 0x10000 + k * 0xAAAB;
            long i = phase >> 16;
            if (i > offline.Length - 1) break;
            float l = offline[i - 1], r = offline[i];
            float w = (float)(phase & 0xFFFF) * (1f / 65536f);
            float d = r - l;
            float p = w * d;
            expected.Add(l + p);
        }
        Assert.Equal(expected.Count, all.Count);
        Assert.Equal(expected.Select(BitConverter.SingleToUInt32Bits), all.Select(BitConverter.SingleToUInt32Bits));
        source.Close2C();
    }

    [Fact]
    public void L2_AStreamedAdapterWithoutAContextRefusesToStartInsteadOfFallingBackToTheOfflineDecode()
    {
        var wem = ReadWem(VorbisName);
        string? books = FindCodebooks();
        if (wem is null || books is null) return;
        var source = new WwiseVorbisVoiceSource(WwiseVorbisSourceKind.Streamed, WwiseMedia.Parse(wem), WwiseCodebookLibrary.Load(books));
        Assert.Throws<WwiseMissingBehaviourException>(() => source.StartStream(0, 0));
        Assert.False(source.WritesSourceFormatInStartStream);
        var adpcm = new WwiseAdpcmVoiceSource(WwiseMedia.Parse(ReadWem(AdpcmName)!), streamed: true);
        Assert.Throws<WwiseMissingBehaviourException>(() => adpcm.StartStream(0, 0));
    }

    [Fact]
    public void L3_TheInMemoryKindsKeepTheirEarlierBehaviourBecauseC33DoesNotAdoptTheirBodies()
    {
        // 0xAB0B20 (the in-memory Vorbis StartStream) and 0xA72A2C's class are not adopted: the adapters decode at StartStream and return 1, as before this batch; WritesSourceFormatInStartStream is false.
        var adpcm = ReadWem(AdpcmName);
        if (adpcm is null) return;
        var source = new WwiseAdpcmVoiceSource(WwiseMedia.Parse(adpcm));
        Assert.Equal(1, source.StartStream(0, 0));
        Assert.False(source.WritesSourceFormatInStartStream);
        Assert.Equal(0x2D, source.Render(new WwiseVoiceBuffer(1, 256)));
    }
}
