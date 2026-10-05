using System.IO.Compression;
using System.Security.Cryptography;
using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// A shipped Vorbis medium as the engine's header parse sees it (0xAB12B4 / 0xAB0B20 through the real walker 0x9CD340): the fmt chunk's fields and the data chunk's packets.
/// </summary>
internal sealed class EngineMedia
{
    public byte[] Wem = Array.Empty<byte>();
    public int Channels;
    public uint Rate, Cfg, Samples;
    public byte[] Vorb = new byte[0x26];                    // fmt + 0x1C
    public uint DataSize, DataOffset;
    public uint SeekSize, AudioOff, Hash, Alloc;
    public ushort MaxPacket, LastGranuleExtra, LoopEndExtra, LoopBeginExtra;
    public byte Bs0, Bs1;
    public byte[] Audio = Array.Empty<byte>();
    public byte[] SetupBytes = Array.Empty<byte>();

    public static EngineMedia Parse(byte[] wem)
    {
        var walk = WwiseWaveWalker.Walk9CD340(new WwiseBytePtr(wem, 0), (uint)wem.Length, null, wantAkd: false, wantSeek: false, () => true);
        Assert.Equal(1, walk.Result);
        var fmt = walk.Format;
        Assert.Equal(0xFFFF, fmt.U16(0));
        var m = new EngineMedia
        {
            Wem = wem, Channels = fmt.U16(2), Rate = fmt.U32(4), Cfg = fmt.U32(0x14), Samples = fmt.U32(0x18), DataSize = walk.DataSize1C, DataOffset = walk.DataOffset20,
        };
        for (int i = 0; i < 0x26; i++) m.Vorb[i] = fmt[0x1C + i];
        m.SeekSize = BitConverter.ToUInt32(m.Vorb, 0xC);
        m.AudioOff = BitConverter.ToUInt32(m.Vorb, 0x10);
        m.MaxPacket = BitConverter.ToUInt16(m.Vorb, 0x14);
        m.LastGranuleExtra = BitConverter.ToUInt16(m.Vorb, 0x16);
        m.LoopBeginExtra = BitConverter.ToUInt16(m.Vorb, 8);
        m.LoopEndExtra = BitConverter.ToUInt16(m.Vorb, 0xA);
        m.Alloc = BitConverter.ToUInt32(m.Vorb, 0x18);
        m.Hash = BitConverter.ToUInt32(m.Vorb, 0x20);
        m.Bs0 = m.Vorb[0x24];
        m.Bs1 = m.Vorb[0x25];
        int dataAt = (int)m.DataOffset;
        m.Audio = wem.AsSpan(dataAt + (int)m.AudioOff, (int)(m.DataSize - m.AudioOff)).ToArray();
        int setupSize = BitConverter.ToUInt16(wem, dataAt + (int)m.SeekSize);
        m.SetupBytes = wem.AsSpan(dataAt + (int)m.SeekSize + 2, setupSize).ToArray();
        return m;
    }

    public List<(int Off, int Size)> Packets()
    {
        var list = new List<(int, int)>();
        int off = 0;
        while (off + 2 <= Audio.Length)
        {
            int size = BitConverter.ToUInt16(Audio, off);
            list.Add((off, size));
            off += 2 + size;
        }
        return list;
    }
}

/// <summary>
/// M6-002 / M6-025 (C36.1, C36.4): the Vorbis stream integration around the bit-exact decode, against the engine's own functions run under Unicorn (re-analysis/tools/emu/emu_decode.py; the generated
/// WwiseVorbisEngineOracle.cs). Every expected value is the engine's output: the frame loop 0xAB7E40 (with 0xAB3780, 0xAB3520, 0xAB3978), the decoder-state allocation 0xAB3264 / 0xAB3428, the setup cache 0xAB2D74 /
/// 0xAB3120, the output hand-off 0xA73490, the walker 0x9CD340, the start position 0xA736D4; none comes from running this implementation. The decode itself runs for real in the C# (full IMDCT etc.): the whole decode of
/// shipped media is compared with the engine byte for byte (SHA-256 of every call's output), through both drives, and with the existing offline decode of M6-002.
/// </summary>
public class WwiseVorbisEngineTests
{
    // ------------------------------------------------------------------ fixtures

    internal static byte[] ReadWem(string name)
    {
        if (WwiseAssets.SoundDir is not { } dir) { Assert.Fail("the shipped re-analysis/obb/assets/cozmo_resources/sound/AudioAssets.zip is not present: these tests do not pass silently without it"); return Array.Empty<byte>(); }
        using var zip = ZipFile.OpenRead(Path.Combine(dir, "AudioAssets.zip"));
        var entry = zip.GetEntry(name) ?? throw new InvalidOperationException(name);
        using var s = entry.Open();
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    internal static WwiseCodebookLibrary Codebooks()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null)
        {
            string p = Path.Combine(d.FullName, "third-party", "ww2ogg", "packed_codebooks_aoTuV_603.bin");
            if (File.Exists(p)) return WwiseCodebookLibrary.Load(p);
            d = d.Parent;
        }
        Assert.Fail("third-party/ww2ogg/packed_codebooks_aoTuV_603.bin is not present: these tests do not pass silently without it");
        return null!;
    }

    private static string Sha16(float[]? block, int frames, int channels)
    {
        if (block is null || frames == 0) return "-";
        var bytes = new byte[4 * channels * frames];
        Buffer.BlockCopy(block, 0, bytes, 0, bytes.Length);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()[..16];
    }

    private static string Sha(IEnumerable<float[]?> blocks)
    {
        using var h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var b in blocks)
        {
            if (b is null) continue;
            var bytes = new byte[4 * b.Length];
            Buffer.BlockCopy(b, 0, bytes, 0, bytes.Length);
            h.AppendData(bytes);
        }
        return Convert.ToHexString(h.GetHashAndReset()).ToLowerInvariant();
    }

    /// <summary>F and its decoder state as 0xAB0A30 / 0xAB22D4 leave them: the setup (the real cache, the real parse), 0xAB3264, then 0xAB3244(D, skip, trim).</summary>
    private static (WwiseVorbisFrameBlock F, WwiseVorbisEngineContext Ctx) NewFrame(EngineMedia m, WwiseCodebookLibrary cb, int loops, int skip, int cfg)
    {
        var ctx = new WwiseVorbisEngineContext { Codebooks = cb };
        var f = new WwiseVorbisFrameBlock { ChannelConfig = cfg < 0 ? m.Cfg : (uint)cfg };
        var setup = ctx.SetupCache.AcquireAB2D74(m.Hash, m.Alloc, (byte)m.Channels, m.Bs0, m.Bs1, m.SetupBytes, cb, () => true);
        Assert.NotNull(setup);
        f.Dsp.Setup = setup;
        Assert.Equal(0, WwiseVorbisDsp.AllocateAB3264(f.Dsp, m.Channels, ctx.Shared, () => true));
        WwiseVorbisDsp.ResetAB3244(f.Dsp, (ushort)skip, loops == 1 ? m.LastGranuleExtra : m.LoopEndExtra);
        return (f, ctx);
    }

    private static string FrameStep(WwiseVorbisFrameBlock f, int channels)
        => $"f={f.Frames} s={f.Status} c={f.Consumed} st={f.State} cp={f.FramesCopy} h={Sha16(f.Output, (int)f.Frames, channels)}";

    private static string ParityStep(WwiseVorbisFrameBlock f, int channels)
        => $"f={f.Frames} s={f.Status} c={f.Consumed} st={f.State} h={Sha16(f.Output, (int)f.Frames, channels)}";

    // ------------------------------------------------------------------ P1: the prime list

    [Fact]
    public void V11_ThePrimeListIsTheWordsAt0x10045D0()
    {
        // 0xAB2F5C..0xAB2F88 scans the list at 0x10045D0 (27 primes; the 28th word is the 0 the scan never reads, it stops at the address list + 0x68).
        var engine = WwiseVorbisEngineOracle.Primes;
        Assert.Equal(28, engine.Length);
        Assert.Equal(0u, engine[27]);
        Assert.Equal(engine.Take(27).ToArray(), WwiseVorbisSetupCache.Primes.ToArray());
    }

    // ------------------------------------------------------------------ F: the frame loop 0xAB7E40

    [Fact]
    public void V01_V02_TheFrameLoopMatchesTheEngineOnEveryScriptedCall()
    {
        // The status / consumed / state protocol of 0xAB7E40: a priming packet gives (frames 0, 0x2E, consumed = its bytes); a packet that yields frames returns at once with 0x2D (the last: 0x11 with state 4); a packet
        // cut by the available bytes, or no bytes, give 0x2E and the overlap save 0xAB3978 only when the work pointers exist; a size above the max packet gives 2; an allocation failure gives 2 with [F+0x4C] = 0; the channel
        // mask permutes the planes (bit 3: the LFE plane last); the loop count selects the end trim; the initial skip drops leading samples.
        var cb = Codebooks();
        Assert.NotEmpty(WwiseVorbisEngineOracle.Frame);
        foreach (var (name, (file, loops, skip, cfg, calls, steps)) in WwiseVorbisEngineOracle.Frame)
        {
            var m = EngineMedia.Parse(ReadWem(file));
            var (f, ctx) = NewFrame(m, cb, loops, skip, cfg);
            var got = new List<string>();
            foreach (var (start, avail, ready, max, failAt) in calls)
            {
                f.Avail = (uint)avail;
                f.Ready = (byte)ready;
                int n = 0;
                WwiseVorbisFraming.FrameLoopAB7E40(f, max < 0 ? m.MaxPacket : (ushort)max, new WwiseBytePtr(m.Audio, start), () => failAt == 0 || ++n != failAt);
                got.Add(FrameStep(f, m.Channels));
            }
            Assert.True(steps.SequenceEqual(got), $"{name}: engine [{string.Join(" | ", steps)}] C# [{string.Join(" | ", got)}]");
        }
    }

    [Fact]
    public void V03_TwoDecodersShareTheProcessWideWorkBufferLikeTheEngine()
    {
        // 0xAB3780 sets work[ch] = [R+8] + ch * slice on every packet and the insufficient-data tail 0xAB7EF4..0xAB7F14 calls 0xAB3978 whenever [work[0]] != 0; 0xAB3978 copies work[ch] + size into THIS decoder's overlap
        // with no flag test, so after another decoder's packets it saves that decoder's data. The expected steps are the engine's (emu_decode_cases.py interleave_cases: the real 0xAB7E40 on two states of one engine
        // with one record 0x108E648). i_mono_stereo_nodata: A's packet 6 after B's packets and a no-data call differs from i_mono_stereo_no_nodata_call's (a1e874a89fb8 against 8871a54f2784).
        var cb = Codebooks();
        Assert.NotEmpty(WwiseVorbisEngineOracle.Interleave);
        foreach (var (name, (fa, fb, calls, skipA, skipB, sentinel, steps)) in WwiseVorbisEngineOracle.Interleave)
        {
            var ctx = new WwiseVorbisEngineContext { Codebooks = cb };
            var ms = new[] { EngineMedia.Parse(ReadWem(fa)), EngineMedia.Parse(ReadWem(fb)) };
            var fs = new[] { NewFrameIn(ctx, ms[0], cb, skipA), NewFrameIn(ctx, ms[1], cb, skipB) };
            if (sentinel) Array.Fill(ctx.Shared.Mem!, BitConverter.UInt32BitsToSingle(0xA5A5A5A5u));      // the engine run fills [R+8] with 0xA5A5A5A5 after both states exist
            var got = new List<string>();
            foreach (var c in calls.Split(';'))
            {
                var p = c.Split(':');
                int who = int.Parse(p[0]), pkt = int.Parse(p[1]);
                var m = ms[who];
                var f = fs[who];
                if (pkt < 0) { f.Avail = 0; f.Ready = 0; WwiseVorbisFraming.FrameLoopAB7E40(f, m.MaxPacket, new WwiseBytePtr(m.Audio, 0), () => true); }
                else
                {
                    var pk = m.Packets();
                    f.Avail = (uint)(pk[pkt].Size + 2);
                    f.Ready = (byte)(pkt == pk.Count - 1 ? 1 : 0);
                    WwiseVorbisFraming.FrameLoopAB7E40(f, m.MaxPacket, new WwiseBytePtr(m.Audio, pk[pkt].Off), () => true);
                }
                string step = $"{who} | {FrameStep(f, m.Channels)}";
                if (sentinel)
                {
                    var bytes = new byte[ctx.Shared.Mem!.Length * 4];
                    Buffer.BlockCopy(ctx.Shared.Mem, 0, bytes, 0, bytes.Length);
                    step += $" sh={Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()[..12]} ws={fs[0].Dsp.WindowSaved}/{fs[1].Dsp.WindowSaved}";   // a dropped packet leaves the shared buffer and [D+0x30] alone
                }
                got.Add(step);
            }
            Assert.True(steps.SequenceEqual(got), $"{name}: engine [{string.Join(" | ", steps)}] C# [{string.Join(" | ", got)}]");
        }
    }

    [Fact]
    public void V03_AnotherDecodersReallocationLeavesAStaleWorkPointerThatIsAVisibleStop()
    {
        // 0xAB3264 replaces the shared work buffer when channels << 14 exceeds [R+4] (0xAB3324) and does not move the other decoders' work pointers (0xAB3978 never reassigns them): a decoder that decoded before
        // and takes the no-data tail afterwards reads the freed buffer (use-after-free; the engine's value is undefined, the harness's frees are no-ops). MISSING, never a guess: WwiseMissingBehaviourException.
        var cb = Codebooks();
        var ctx = new WwiseVorbisEngineContext { Codebooks = cb };
        var ma = EngineMedia.Parse(ReadWem("1026267421.wem"));          // mono 512 / 1024: need 1 << 14
        var a = NewFrameIn(ctx, ma, cb);
        var pk = ma.Packets();
        for (int i = 0; i < 3; i++)
        {
            a.Avail = (uint)(pk[i].Size + 2);
            a.Ready = 0;
            WwiseVorbisFraming.FrameLoopAB7E40(a, ma.MaxPacket, new WwiseBytePtr(ma.Audio, pk[i].Off), () => true);
        }
        var mb = EngineMedia.Parse(ReadWem("692717205.wem"));           // stereo: 2 << 14 > [R+4]: the buffer is replaced
        NewFrameIn(ctx, mb, cb);
        a.Avail = 0;
        a.Ready = 0;
        Assert.Throws<WwiseMissingBehaviourException>(() => WwiseVorbisFraming.FrameLoopAB7E40(a, ma.MaxPacket, new WwiseBytePtr(ma.Audio, 0), () => true));
    }

    private static WwiseVorbisFrameBlock NewFrameIn(WwiseVorbisEngineContext ctx, EngineMedia m, WwiseCodebookLibrary cb, int skip = 0)
    {
        var f = new WwiseVorbisFrameBlock { ChannelConfig = m.Cfg };
        var setup = ctx.SetupCache.AcquireAB2D74(m.Hash, m.Alloc, (byte)m.Channels, m.Bs0, m.Bs1, m.SetupBytes, cb, () => true);
        Assert.NotNull(setup);
        f.Dsp.Setup = setup;
        Assert.Equal(0, WwiseVorbisDsp.AllocateAB3264(f.Dsp, m.Channels, ctx.Shared, () => true));
        WwiseVorbisDsp.ResetAB3244(f.Dsp, (ushort)skip, m.LastGranuleExtra);
        return f;
    }

    // ------------------------------------------------------------------ D: 0xAB3264 / 0xAB3428

    private static WwiseVorbisSetup DummySetup() => new()
    {
        BlockSize0 = 256, BlockSize1 = 2048, Codebooks = Array.Empty<WwiseVorbisCodebook>(), Floors = Array.Empty<WwiseVorbisFloorSetup>(),
        Residues = Array.Empty<WwiseVorbisResidueSetup>(), Mappings = Array.Empty<WwiseVorbisMappingSetup>(), Modes = Array.Empty<WwiseVorbisMode>(),
    };

    [Fact]
    public void V12_TheDecoderStateAllocationAndTeardownMatchTheEngine()
    {
        // 0xAB3264 / 0xAB3428 and the process-wide record 0x108E648: the arrays, the overlap block, the 0x2000-byte block, the shared work buffer (grown when channels << 14 exceeds it), the user count, the failure of each
        // allocation (the engine leaves what was allocated until the teardown), the teardown's freeing of the shared buffers only at the last user.
        Assert.NotEmpty(WwiseVorbisEngineOracle.Dsp);
        foreach (var (name, (ops, steps)) in WwiseVorbisEngineOracle.Dsp)
        {
            var shared = new WwiseVorbisSharedWork();
            var ds = Enumerable.Range(0, 4).Select(_ => new WwiseVorbisNative.DecoderState { Setup = DummySetup() }).ToArray();
            var got = new List<string>();
            foreach (var op in ops.Split(';'))
            {
                var p = op.Split(':');
                int slot = int.Parse(p[1]);
                int allocs = 0, ret = 0;
                if (p[0] == "alloc")
                {
                    int failAt = int.Parse(p[3]);
                    ret = WwiseVorbisDsp.AllocateAB3264(ds[slot], int.Parse(p[2]), shared, () => { allocs++; return failAt == 0 || allocs != failAt; });
                }
                else WwiseVorbisDsp.TeardownAB3428(ds[slot], shared);
                var d = ds[slot];
                got.Add($"ret={ret} arr={(d.ArraysAllocated ? 1 : 0)} ovl={(d.OverlapAllocated ? 1 : 0)} ch={d.Channels} ws={d.WindowSaved} pf={d.PreviousFlag} cf={d.CurrentFlag} | blk={(shared.HasBlock0 ? 1 : 0)} sz={shared.Size4} work={(shared.HasWork ? 1 : 0)} users={(uint)shared.Users} | allocs={allocs}");
            }
            Assert.True(steps.SequenceEqual(got), $"{name}: engine [{string.Join(" | ", steps)}] C# [{string.Join(" | ", got)}]");
        }
    }

    // ------------------------------------------------------------------ C: 0xAB2D74 / 0xAB3120

    [Fact]
    public void V11_TheSetupCacheMatchesTheEngineThroughTheGrowthFailuresAndReleases()
    {
        // 0xAB2D74: a hit increments the node's count without reading the setup; a miss allocates the node and the setup block, parses (a parse failure frees both and gives null), links the node and grows the bucket
        // array to the next prime (29, 53, 97, ...) when the load is above 0.9f; a growth that fails keeps the old table (and gives null when there is none); 0xAB3120 decrements, unlinks at 0, and frees the bucket array
        // when the last node goes. The expected strings include every bucket's chain (key:count) at the end of the script.
        var cb = Codebooks();
        var m = EngineMedia.Parse(ReadWem("289339243.wem"));
        Assert.NotEmpty(WwiseVorbisEngineOracle.Cache);
        foreach (var (name, (ops, steps)) in WwiseVorbisEngineOracle.Cache)
        {
            var cache = new WwiseVorbisSetupCache();
            var got = new List<string>();
            foreach (var op in ops.Split(';'))
            {
                var p = op.Split(':');
                uint key = uint.Parse(p[1]);
                int allocs = 0;
                string head;
                if (p[0] == "acq")
                {
                    int failAt = int.Parse(p[2]);
                    bool parseFail = p[3] == "1";
                    var setup = cache.AcquireAB2D74(key, 0x1000, 1, 8, 11, parseFail ? ReadOnlyMemory<byte>.Empty : m.SetupBytes, cb, () => { allocs++; return failAt == 0 || allocs != failAt; });
                    head = $"acq {key} ret={(setup is null ? 0 : 1)} rc={(setup is null ? -1 : cache.Find(key)!.RefCount)}";
                }
                else
                {
                    cache.ReleaseAB3120(key);
                    head = $"rel {key}";
                }
                got.Add($"{head} | bc={cache.BucketCount} cap={cache.Capacity} n={cache.NodeCount} arr={(cache.HasBuckets ? 1 : 0)} | allocs={allocs}");
            }
            got.Add("dump " + string.Join("|", cache.DumpChains()));
            Assert.True(steps.SequenceEqual(got), $"{name}: first difference at {Array.FindIndex(steps, (s) => false)}\nengine [{string.Join("\n", steps.Where((s, i) => i >= got.Count || s != got[i]).Take(3))}]\nC#     [{string.Join("\n", got.Where((s, i) => i >= steps.Length || s != steps[i]).Take(3))}]");
        }
    }

    // ------------------------------------------------------------------ H: 0xA73490

    private sealed class FakeSource : IWwiseSourceCommon
    {
        public FakeSource(WwisePlayingInstance pbi) => Pbi = pbi;
        public WwisePlayingInstance Pbi { get; }
        public uint TotalSamples14 { get; set; }
        public uint SampleBase18 { get; set; }
        public ushort LoopCount38 { get; set; }
        public uint Word24 { get; set; }
        public uint Word28 { get; set; }
        public WwiseChunkContainer Container2C { get; } = new();
        public Func<bool> TryAlloc { get; set; } = () => true;
        public int Vt74Result = 0x2D;
        public readonly List<int> Vt74Calls = new();
        public int LoopOrEnd74(int r1) { Vt74Calls.Add(r1); return Vt74Result; }
    }

    private static Dictionary<string, string> Params(string s) => s.Split(';').Select(x => x.Split('=', 2)).ToDictionary(p => p[0], p => p[1]);

    private static WwisePlayingInstance NewPbi() => new(new WwisePlayInitParams { PlayingId = 1, TargetNodeId = 1 }, 1, new WwiseSourceDescriptor(0, 1, 5, 0, 0), new byte[0x44], null, continuous: false);

    private static void FillMarkers(WwiseChunkContainer c, string spec)
    {
        if (spec == "none") return;
        c.ArrayPtr = 1;
        var entries = spec.Length == 0 ? Array.Empty<string>() : spec.Split(',');
        c.Count = (uint)entries.Length;
        for (int i = 0; i < entries.Length; i++)
        {
            var e = entries[i].Split('/');
            c.Cues.Add(new WwiseCuePoint(uint.Parse(e[0]), uint.Parse(e[1])));
            c.ElementPtrs8.Add(0xBEEF0000u + (uint)i);
        }
    }

    [Fact]
    public void V05_TheOutputHandoffMatchesTheEngine()
    {
        // 0xA73490: frames 0 gives 0x2E (only valid frames and the result are written); else the pointer is published, u16 frames at +0xC / +0xE, the config at +4, the position / total / rate, [S+0x18] advanced; with the loop
        // count 1 the result is 0x2D while total > newpos, else vt+0x74(S, 1)'s return (0x11 at the end of a play); otherwise past the loop end the position wraps to the loop start and the result is vt+0x74(S, 0)'s;
        // the marker window of 0x9D4C24 (bit 2 of [pbi+4], entries with pos <= position < pos + frames, 20-byte entries, an allocation failure clears the pointer and count). The io state starts as a pattern in the
        // engine run: untouched fields keep it.
        Assert.NotEmpty(WwiseVorbisEngineOracle.Handoff);
        foreach (var (name, (prm, step)) in WwiseVorbisEngineOracle.Handoff)
        {
            var p = Params(prm);
            var pbi = NewPbi();
            pbi.Flags4 = p.TryGetValue("flags4", out var f4) ? uint.Parse(f4) : 0;
            var s = new FakeSource(pbi)
            {
                TotalSamples14 = uint.Parse(p["total"]), SampleBase18 = uint.Parse(p["pos"]), Word24 = uint.Parse(p["l0"]), Word28 = uint.Parse(p["l1"]), LoopCount38 = ushort.Parse(p["loops"]),
                Vt74Result = p.TryGetValue("vt", out var vt) ? int.Parse(vt) : 0x2D,
            };
            if (p.TryGetValue("markers", out var mk)) FillMarkers(s.Container2C, mk);
            else s.Container2C.ArrayPtr = 1;                                       // the default case has a container with no entries and a non-null array (the engine's S+0x2C = {0, array})
            if (p.ContainsKey("fail_alloc")) s.TryAlloc = () => false;
            var sentinelData = new float[1];
            var sentinelMarkers = new WwiseMarkerWindowEntry[1];
            var state = new WwiseDecodeState
            {
                Data = sentinelData, ChannelConfig = 0x07060504, MaxFrames = 0x0D0C, ValidFrames = 0x0F0E, MarkerCount = 0x1110, Markers = sentinelMarkers, Position = 0x1B1A1918, Total = 0x23222120, Rate = 0x27262524, Code28 = 0x2B2A2928,
            };
            var buf = new float[4];
            WwiseSourceOutput.HandoffA73490(s, buf, uint.Parse(p["frames"]), uint.Parse(p["rate"]), uint.Parse(p["cc"]), state);
            string d = ReferenceEquals(state.Data, buf) ? "buf" : ReferenceEquals(state.Data, sentinelData) ? "untouched" : "other";
            string marks = ReferenceEquals(state.Markers, sentinelMarkers) || state.Markers is null ? "" : string.Join(";", state.Markers.Select(e => $"pbi/{e.Offset}/{e.Id}/{e.Position}/{e.Label:x}"));
            string got = $"d={d} cc={state.ChannelConfig:x} max={state.MaxFrames} valid={state.ValidFrames} mc={state.MarkerCount} arr={(state.Markers is null ? 0 : 1)} pos={state.Position} tot={state.Total} rate={state.Rate} res={state.Code28:x} | S18={s.SampleBase18} | vt=[{string.Join(",", s.Vt74Calls)}] | marks=[{marks}]";
            Assert.Equal(step, got);
        }
    }

    // The pitch node's intake 0xA52D4C, the voice's pull loop 0xA44630 and the consumption 0xA52DA8 are tested in WwisePitchNodeTests (the engine's own code, emu_pitch.py).

    /// <summary>The 0xA548C0 / PBI vt+0x58 getter 0x9CBACC on its own: returns [pbi+0x1F8] and stores -1 (R4.8).</summary>
    [Fact]
    public void R4_8_TheStopOffsetGetterReturnsTheOffsetAndStoresMinusOne()
    {
        var pbi = NewPbi();
        Assert.Equal(0xFFFFFFFFu, pbi.TakeStopOffset9CBACC());                                     // the constructor stores -1 (0xA00318)
        pbi.Field1F8 = 1234;
        Assert.Equal(1234u, pbi.TakeStopOffset9CBACC());                                           // 0x9CBAD4 ldr r0,[r0,#0x1f8]
        Assert.Equal(0xFFFFFFFFu, pbi.Field1F8);                                                   // 0x9CBAD8 str r2,[r3,#0x1f8] with r2 = -1
        Assert.Equal(0xFFFFFFFFu, pbi.TakeStopOffset9CBACC());
    }

    // ------------------------------------------------------------------ W: 0x9CD340

    [Fact]
    public void V15_TheWalkerMatchesTheEngineOnHandBuiltRiffBuffers()
    {
        // 0x9CD340: 0x1F for null arguments; 7 for a size below 12, a bad tag or form, data or cue before fmt; 8 for fewer than 8 bytes left or a non-data chunk larger than the rest; LIST is entered 12 bytes in; the first
        // fmt wins; akd and seek store {size, ptr}; smpl takes the loop words from payload + 0x24 + u32[payload+0x20]; cue stores {dwName, dwPosition, 0} once; an odd chunk skips its pad byte only when it is 0; the data
        // chunk ends the walk (a truncated one is allowed). The untouched outputs hold the engine's sentinels (0xDEAD0000 + the field's offset).
        Assert.NotEmpty(WwiseVorbisEngineOracle.Walker);
        foreach (var (name, (blob, cuts, steps)) in WwiseVorbisEngineOracle.Walker)
        {
            var bytes = Convert.FromHexString(blob);
            var array = new byte[bytes.Length + 16];
            Array.Copy(bytes, array, bytes.Length);
            for (int i = 0; i < cuts.Length; i++)
            {
                var (label, len) = cuts[i];
                var markers = new WwiseChunkContainer();
                var w = name == "w_cue_three_alloc_fail"
                    ? WwiseWaveWalker.Walk9CD340(new WwiseBytePtr(array, 0), (uint)len, markers, wantAkd: true, wantSeek: false, () => false)
                    : WwiseWaveWalker.Walk9CD340(new WwiseBytePtr(array, 0), (uint)len, markers, wantAkd: true, wantSeek: name == "w_seek", () => true);
                if (name == "w_cue_three_alloc_fail")
                {
                    Assert.Equal(steps[0], $"r={w.Result} n={markers.Count} arr={(markers.ArrayPtr != 0 ? 1 : 0)} data={(w.WroteData ? w.DataSize1C : 0)}");
                    continue;
                }
                string fmt = w.Format.IsNull ? "61680@61937" : $"{w.FormatSize}@{w.Format.Index}";
                string lp = w.WroteLoops ? $"{w.Word24}/{w.Word28}" : "3735879716/3735879720";
                string data = w.WroteData ? $"{w.DataSize1C}@{w.DataOffset20}" : "3735879708@3735879712";
                string akd = w.Akd.IsNull ? "0@0" : $"{w.AkdSize}@{w.Akd.Index}";
                string seek = w.Seek.IsNull ? "0@0" : $"{w.SeekSize}@{w.Seek.Index}";
                string cues = string.Join(";", markers.Cues.Select((c, k) => $"{c.Id}/{c.Position}/{markers.ElementPtrs8[k]}"));
                string got = $"{label} r={w.Result} fmt={fmt} lp={lp} data={data} akd={akd} seek={seek} n={markers.Count} arr={(markers.ArrayPtr != 0 ? 1 : 0)} cues=[{cues}]";
                Assert.True(steps[i] == got, $"{name}: engine {steps[i]}\n      C#     {got}");
            }
        }
    }

    [Fact]
    public void V15_AMatchingLablChunkNeedsTheUnread0x9D4BBCSoItIsAVisibleStop()
    {
        // 0x9CD340 calls 0x9D4BBC(markers, index, text, length) for a labl chunk naming a cue point; its body is not read (C36 open): MISSING, never a guess. A SHIPPED file reaches it: 433319711.wem (Vorbis 0xFFFF, stereo 32 kHz) has fmt, a cue chunk (1 point, id 1) and a LIST with a labl of id 1 ("fast"), where the engine calls 0x9D4BBC (0x9CD5A0..0x9CD5AC); the C# stops at its StartStream (listed MISSING).
        var shipped = ReadWem("433319711.wem");
        Assert.Throws<WwiseMissingBehaviourException>(() => WwiseWaveWalker.Walk9CD340(new WwiseBytePtr(shipped, 0), (uint)shipped.Length, new WwiseChunkContainer(), true, false, () => true));
        var blob = Convert.FromHexString(WwiseVorbisEngineOracle.Walker["w_cue_three"].Blob);
        var list = new List<byte>(blob.Take(blob.Length - 40));                     // drop the data chunk header + payload (8 + 32)
        list.AddRange("labl"u8.ToArray());
        list.AddRange(BitConverter.GetBytes(8u));
        list.AddRange(BitConverter.GetBytes(2u));
        list.AddRange("hi\0\0"u8.ToArray());
        list.AddRange("data"u8.ToArray());
        list.AddRange(BitConverter.GetBytes(4u));
        list.AddRange(new byte[4]);
        var arr = list.ToArray();
        Assert.Throws<WwiseMissingBehaviourException>(() => WwiseWaveWalker.Walk9CD340(new WwiseBytePtr(arr, 0), (uint)arr.Length, new WwiseChunkContainer(), true, false, () => true));
    }

    // ------------------------------------------------------------------ S: 0xA736D4

    [Fact]
    public void V20_TheStartPositionMatchesTheEngine()
    {
        // 0xA736D4: the integer path (u64 rate * offset / mix rate), the float path (the offset read as a float fraction of vt+0x34's duration or of total * 1000 / rate; binary32 non-fused; truncating vcvt.u32), the loop
        // fold (applies for loops != 1, L0 < L1, pos > L1; three cases by passes against loops), the marker snap (the nearest marker, the first on a tie) and its second fold.
        Assert.NotEmpty(WwiseVorbisEngineOracle.StartPosition);
        foreach (var (name, (prm, step)) in WwiseVorbisEngineOracle.StartPosition)
        {
            var p = Params(prm);
            var pbi = NewPbi();
            pbi.SourceFormat158 = uint.Parse(p["rate"]);
            pbi.Word1B4 = uint.Parse(p["w1b4"]);
            pbi.LoopCount1B8 = ushort.Parse(p["loops"]);
            pbi.Flags1BE = byte.Parse(p["f1be"]);
            var s = new FakeSource(pbi) { TotalSamples14 = uint.Parse(p["total"]), Word24 = uint.Parse(p["l0"]), Word28 = uint.Parse(p["l1"]), LoopCount38 = ushort.Parse(p["loops38"]) };
            if (p.TryGetValue("markers", out var mk) && mk != "none") FillMarkers(s.Container2C, mk);
            uint pos = WwiseSourceStart.StartPositionA736D4(s);
            Assert.Equal(step, $"pos={pos} h38={s.LoopCount38}");
        }
    }

    // ------------------------------------------------------------------ P: the whole decode of shipped media, both drives, and the census

    private static (List<string> Steps, List<float[]?> Outs, List<(int Frames, int Status, int State)> Calls) DrivePerPacket(EngineMedia m, WwiseCodebookLibrary cb, bool keep)
    {
        var (f, ctx) = NewFrame(m, cb, 1, 0, -1);
        var steps = new List<string>();
        var outs = new List<float[]?>();
        var calls = new List<(int, int, int)>();
        var pk = m.Packets();
        for (int i = 0; i < pk.Count; i++)
        {
            f.Avail = (uint)(pk[i].Size + 2);
            f.Ready = (byte)(i == pk.Count - 1 ? 1 : 0);
            WwiseVorbisFraming.FrameLoopAB7E40(f, m.MaxPacket, new WwiseBytePtr(m.Audio, pk[i].Off), () => true);
            calls.Add(((int)f.Frames, f.Status, f.State));
            if (keep) { steps.Add(ParityStep(f, m.Channels)); outs.Add(f.Output); }
            if (f.Status == 2) break;
        }
        return (steps, outs, calls);
    }

    private static (List<string> Steps, List<float[]?> Outs) DriveWhole(EngineMedia m, WwiseCodebookLibrary cb)
    {
        var (f, ctx) = NewFrame(m, cb, 1, 0, -1);
        var steps = new List<string>();
        var outs = new List<float[]?>();
        int off = 0;
        while (off + 2 <= m.Audio.Length)
        {
            f.Avail = (uint)(m.Audio.Length - off);
            f.Ready = 1;
            WwiseVorbisFraming.FrameLoopAB7E40(f, m.MaxPacket, new WwiseBytePtr(m.Audio, off), () => true);
            steps.Add(ParityStep(f, m.Channels));
            outs.Add(f.Output);
            if (f.Consumed == 0 || f.Status == 0x11 || f.Status == 2) break;
            off += (int)f.Consumed;
            if (f.State == 4) break;
        }
        return (steps, outs);
    }

    [Fact]
    public void V02_TheWholeDecodeOfShippedMediaMatchesTheEngineThroughBothDrivesAndTheOfflineDecode()
    {
        // The engine (emu_decode.py run_parity: the real 0xAB7E40 / 0xAB3780 / 0xAB6B14 ... whole decode under Unicorn) drives each medium one packet per call (the copy-buffer case of 0xAB1550) and with the whole media
        // offered at every call (the in-memory class's 0xAB0448); the C# frame loop on the C# decode must give the same (frames, status, consumed, state, SHA-256 of the block) at every call, the same SHA-256 over all the
        // blocks, and the same bytes as the existing offline decode of M6-002 (WwiseVorbisNative.Decode), whose total equals the header's SampleCount.
        var cb = Codebooks();
        Assert.NotEmpty(WwiseVorbisEngineOracle.Parity);
        foreach (var (file, (channels, samples, perPacket, allSha, whole, wholeSha)) in WwiseVorbisEngineOracle.Parity)
        {
            var wem = ReadWem(file);
            var m = EngineMedia.Parse(wem);
            Assert.Equal(channels, m.Channels);
            Assert.Equal(samples, m.Samples);
            var (steps, outs, calls) = DrivePerPacket(m, cb, keep: true);
            Assert.True(perPacket.SequenceEqual(steps), $"{file}: per-packet drive differs from the engine");
            Assert.Equal(allSha, Sha(outs));
            var (wsteps, wouts) = DriveWhole(m, cb);
            Assert.True(whole.SequenceEqual(wsteps), $"{file}: whole-buffer drive differs from the engine");
            Assert.Equal(wholeSha, Sha(wouts));
            Assert.Equal(allSha, wholeSha);                                          // the two drives give identical bytes
            Assert.Equal((long)samples, calls.Sum(c => (long)c.Frames));            // the total frames equal the header SampleCount (the engine does not truncate)
            Assert.Equal((0x11, 4), (calls[^1].Status, calls[^1].State));
            Assert.True(calls[^1].Frames > 0);                                       // the last call carries frames (the 0x11 of 0xA73490's vt+0x74)

            // the differential: the existing offline decode (M6-002, whole media, truncated to SampleCount) gives the same samples
            var offline = WwiseVorbisNative.Decode(WwiseMedia.Parse(wem), cb);
            Assert.Equal((long)samples * channels, offline.Length);
            Assert.Equal(Sha(new[] { offline }), Sha(outs));
        }
    }

    [Fact]
    public void V02_TheCensusOfEveryShippedVorbisMediumMatchesTheEngine()
    {
        // The census of all shipped Vorbis media (1987 in AudioAssets.zip, format tag 0xFFFF), the engine's one-packet-per-call drive under Unicorn (the per-packet inverse stubbed there: it cannot change a count, a status or a
        // state): per medium the total frames, the number of calls, the last call (frames, status, state) and the set of frames per call. The C# runs the whole decode and must equal every record; and the properties the
        // research asked about hold for all of them: total == header SampleCount, the last call has frames (n > 0) with status 0x11 and state 4, the frames per call are 128 / 576 / 1024 at block sizes 256 / 2048 and
        // 256 / 384 / 512 at 512 / 1024 (plus the last call's own count).
        var cb = Codebooks();
        Assert.Equal(1987, WwiseVorbisEngineOracle.Census.Length);
        if (WwiseAssets.SoundDir is not { } dir) { Assert.Fail("AudioAssets.zip is not present"); return; }
        using var zip = ZipFile.OpenRead(Path.Combine(dir, "AudioAssets.zip"));
        var failures = new System.Collections.Concurrent.ConcurrentBag<string>();
        var longSet = new System.Collections.Concurrent.ConcurrentDictionary<int, byte>();
        var shortSet = new System.Collections.Concurrent.ConcurrentDictionary<int, byte>();
        var media = new List<(int Index, byte[] Wem)>();
        for (int i = 0; i < WwiseVorbisEngineOracle.Census.Length; i++)
        {
            using var s = (zip.GetEntry(WwiseVorbisEngineOracle.Census[i].Name) ?? throw new InvalidOperationException(WwiseVorbisEngineOracle.Census[i].Name)).Open();
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            media.Add((i, ms.ToArray()));
        }
        Parallel.ForEach(media, new ParallelOptions { MaxDegreeOfParallelism = 2 }, item =>
        {
            var rec = WwiseVorbisEngineOracle.Census[item.Index];
            var m = EngineMedia.Parse(item.Wem);
            var (_, _, calls) = DrivePerPacket(m, cb, keep: false);
            long total = calls.Sum(c => (long)c.Frames);
            var per = calls.Select(c => c.Frames).Where(x => x != 0).Distinct().OrderBy(x => x).ToArray();
            var last = calls[^1];
            string got = $"{m.Channels} {m.Samples} {total} {calls.Count} {last.Frames} {last.Status} {last.State} [{string.Join(",", per)}]";
            string want = $"{rec.Channels} {rec.Samples} {rec.Total} {rec.Calls} {rec.LastFrames} {rec.LastStatus} {rec.LastState} [{string.Join(",", rec.PerCall)}]";
            if (got != want) failures.Add($"{rec.Name}: engine {want} C# {got}");
            if (total != m.Samples) failures.Add($"{rec.Name}: total frames {total} != header SampleCount {m.Samples}");
            if (last.Frames <= 0 || last.Status != 0x11 || last.State != 4) failures.Add($"{rec.Name}: the last call is ({last.Frames}, 0x{last.Status:X}, {last.State})");
            var set = m.Bs1 == 11 ? longSet : shortSet;
            foreach (int x in calls.Take(calls.Count - 1).Select(c => c.Frames).Where(x => x != 0).Distinct()) set[x] = 1;
        });
        Assert.True(failures.IsEmpty, string.Join("\n", failures.Take(10)));
        Assert.Equal(new[] { 128, 576, 1024 }, longSet.Keys.OrderBy(x => x).ToArray());          // blocks 256 / 2048
        Assert.Equal(new[] { 256, 384, 512 }, shortSet.Keys.OrderBy(x => x).ToArray());          // blocks 512 / 1024
    }
}
