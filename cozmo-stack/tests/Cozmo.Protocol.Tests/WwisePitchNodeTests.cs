using System.Runtime.InteropServices;
using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M6-022 / M6-004 / M6-025 (C36.3, C38.1): the voice order <c>0xA44630</c> with the pitch node's pass <c>0xA53134</c>, the intake <c>0xA52D4C</c>, the consumption <c>0xA52DA8</c> (output allocation, first-buffer silent fill, start-offset
/// skip, <c>Execute</c>, marker carry, output position, pitch factor, source release, pending-source arm <c>0xA52B90</c> with the format change <c>0xA47528</c>) and the release <c>0xA52800</c>, driven through the live entry
/// <see cref="WwiseLiveVoice.Render"/> and compared with the engine's own code under Unicorn (<c>emu_pitch.py</c>: the real <c>0xA44630</c> on a hand-built voice; the source classes' <c>vt+0x30</c>, <c>vt+0xC</c>, <c>vt+0x28</c>,
/// <c>0xA549A0</c>, the new PBI's context calls and <c>0xA55C14</c> are logged stand-ins). The expected values are the engine's, never this implementation's.
/// </summary>
public class WwisePitchNodeTests
{
    internal sealed class Block
    {
        public int Res, Valid, NMark;
        public uint Pos, DSeed, MSeed;
    }

    internal static List<Block> ParseBlocks(string spec)
    {
        var list = new List<Block>();
        if (spec == "-" || spec.Length == 0) return list;
        foreach (var b in spec.Split(','))
        {
            var f = b.Split('/');
            list.Add(new Block { Res = Convert.ToInt32(f[0], 16), Valid = int.Parse(f[1]), Pos = Convert.ToUInt32(f[2], 16), DSeed = Convert.ToUInt32(f[3], 16), NMark = int.Parse(f[4]), MSeed = Convert.ToUInt32(f[5], 16) });
        }
        return list;
    }

    /// <summary>A source of the engine's shape whose <c>vt+0x30</c> delivers scripted blocks (data from the test generator) and logs its calls.</summary>
    internal sealed class ScriptSource : IWwisePitchNodeSource
    {
        private readonly List<string> _events;
        private readonly List<Block> _script;
        private readonly string _name;
        private readonly int _channels;
        private readonly bool _isFloat;
        private readonly uint _rate, _channelWord;
        private int _next;
        public WwisePlayingInstance Pbi { get; }
        public WwisePlayingInstance[] MarkerPbis { get; }
        public int StartResult { get; set; } = 1;

        internal ScriptSource(List<string> events, string name, List<Block> script, WwisePlayingInstance pbi, WwisePlayingInstance[] markerPbis, int channels, bool isFloat, uint rate, uint channelWord)
        {
            _events = events; _name = name; _script = script; Pbi = pbi; MarkerPbis = markerPbis; _channels = channels; _isFloat = isFloat; _rate = rate; _channelWord = channelWord;
        }

        public int Channels => _channels;
        public int SampleRate => (int)_rate;
        public bool HasPitchNode => true;
        public WwiseDecodeState Io { get; set; } = new();
        public WwisePlayingInstance? Owner => Pbi;
        public bool StartStreamSucceeded { get; set; }
        public void ReleaseOutput() => _events.Add($"rel{_name}");
        public int StartStream(uint a, uint b) { _events.Add($"start{_name}({a},{b})"); return StartResult; }

        public int Render(WwiseVoiceBuffer buffer)
        {
            _events.Add($"s30_{_name}({Io.MaxFrames})");
            int k = _next++;
            if (k >= _script.Count)
            {
                Io.ValidFrames = 0;
                Io.Code28 = 0x2E;
                buffer.Result = 0x2E;
                return 0x2E;
            }
            var blk = _script[k];
            var d = new Xs(blk.DSeed);
            Array data;
            if (_isFloat) { var f = new float[blk.Valid * _channels]; for (int i = 0; i < f.Length; i++) f[i] = d.F32(); data = f; }
            else { var s = new short[blk.Valid * _channels]; for (int i = 0; i < s.Length; i++) s[i] = d.S16(); data = s; }
            Io.Data = data; Io.ChannelConfig = _channelWord; Io.Scratch08 = 0x2D;
            Io.MaxFrames = (ushort)blk.Valid; Io.ValidFrames = (ushort)blk.Valid;
            var m = new Xs(blk.MSeed);
            var marks = new WwiseMarkerWindowEntry[blk.NMark];
            uint span = (uint)Math.Max(blk.Valid, 1) + 8;
            for (int i = 0; i < blk.NMark; i++)
                marks[i] = new WwiseMarkerWindowEntry(MarkerPbis[i], m.Below(span), m.Below(100), m.Below(1000), m.Below(100));
            Io.MarkerCount = (ushort)blk.NMark;
            Io.Markers = blk.NMark == 0 ? null : marks;
            Io.Position = blk.Pos; Io.Total = (uint)(100000 + k); Io.Rate = _rate;
            Io.Code28 = blk.Res;
            buffer.Result = blk.Res;
            return blk.Res;
        }
    }

    private static string H(uint v) => v.ToString("X8");

    private static WwisePlayingInstance NewPbi()
        => new(new WwisePlayInitParams { PlayingId = 0x4321, TargetNodeId = 1 }, 1, new WwiseSourceDescriptor(0, 1, 5, 0, 0), new byte[0x44], null, continuous: false);

    private static (string Short, string Tail) Snapshot(WwiseLiveVoice voice, List<string> events, WwisePlayingInstance pbi1, WwisePlayingInstance pbi2, ScriptSource? src2, WwisePlayingInstance[] markerPbis, WwiseMixBus? bus)
    {
        var S = voice.Buffer.State;
        var node = voice.PitchNode;
        string dh = "-";
        if (S.Data is float[] fd && (S.Code28 == 0x2D || S.Code28 == 0x11) && S.ValidFrames != 0)
        {
            int ch = (byte)S.ChannelConfig;
            var bytes = new List<byte>();
            for (int c = 0; c < ch; c++) bytes.AddRange(MemoryMarshal.AsBytes(fd.AsSpan(c * S.MaxFrames, S.ValidFrames)).ToArray());
            dh = WwiseResamplerTests.Sha(bytes.ToArray());
        }
        string marks = S.MarkerCount != 0 && S.Markers is not null
            ? string.Join(";", S.Markers.Take(S.MarkerCount).Select(e => $"{0x7B000 + Array.IndexOf(markerPbis, e.Pbi):X}.{e.Offset:X}.{e.Id:X}.{e.Position:X}.{e.Label:X}"))
            : "-";
        string st = string.Join(" ", new[] { (uint)S.Code28, S.Scratch08, S.ChannelConfig, S.MaxFrames, S.ValidFrames, S.MarkerCount, S.Position, S.Word1C, S.Total, S.Rate }.Select(H)) + " " + dh + " " + marks;
        var h = node.Held60;
        string held = string.Join(" ", new[] { (uint)h.ValidFrames, h.MaxFrames, h.MarkerCount, h.Position, h.Scratch08 }.Select(H)) + " d" + (h.Data is null ? 0 : 1);
        var o = node.Out;
        string outs = string.Join(" ", new[] { (uint)o.ValidFrames, o.MaxFrames, o.MarkerCount, o.Position, o.Word1C, o.Total, o.Rate }.Select(H)) + " d" + (o.Data is null ? 0 : 1);
        string flags = string.Join(" ", new[] { node.ByteB8, node.ByteB9, node.ByteBA }.Select(b => b.ToString("X2")));
        string pbi = string.Join(" ", new[] { pbi1.Word1B4, pbi1.Flags1BD, pbi1.Flags1BE, pbi1.StartOffset, pbi2.StartOffset }.Select(H));
        string links = $"{(ReferenceEquals(node.Pbi, pbi2) ? 1 : 0)} {(ReferenceEquals(node.Upstream, src2) ? 2 : 1)}";
        string busx = bus is null ? "" : $"{(uint)bus.EState:X} {bus.State:X} {bus.Frames:X4} {WwiseMixKernelTests.CanonHash(bus.Buffer)}";
        return ($"{string.Join(" ", events)} | {st}", $"{held} | {outs} | {flags} | {pbi} | {links} | {WwiseResamplerTests.Snap(node.Resampler)} | {WwiseResamplerTests.Hex(node.Resampler.History)} | {busx}");
    }

    [Fact]
    public void M6_022_The_voice_order_with_the_pitch_node_matches_the_engine_on_1600_random_scenarios()
    {
        // C36.3, C38.1 (P1-01..P1-11, P1-15): 1600 scenarios of 1..6 voice passes through the live entry WwiseLiveVoice.Render (the engine side runs the real 0xA44630): rates 48000 / 44100 / 32000 / 24000 / 22050 into 48000, 1 or 2 channels, float or int16 sources,
        // cents from the pbi, the 0x380 interpolation flag, start offsets (skip and skip-everything), first-buffer silent fills from [pbi+0x1D8] and [pbi+0x164], 0x2D with no frames, 0x2E, the last block with 0x11, markers (carry with offset 0 and
        // count n only), position carry, pool allocation failures (output and marker array), a pending source (the 0x3F / 2 / other results, the position decrement, the format-compat gate, the format change, the 0x2B / 0x2D result) and the release 0xA52800.
        Assert.True(WwisePitchOracle.Node.Length >= 1500);
        foreach (var row in WwisePitchOracle.Node)
        {
            var halves = row.Split(" ## ");
            var passesExpected = halves[1].Split(" ;; ");
            var parts = halves[0].Split(" ~ ");
            var h = parts[0].Split(' ');
            uint rate = uint.Parse(h[0]); int ch = int.Parse(h[1]); bool isFloat = h[2] == "1";
            float cents = Bits(h[3]);
            uint h1be = Convert.ToUInt32(h[4], 16), b1bd = Convert.ToUInt32(h[5], 16), s1b4 = Convert.ToUInt32(h[6], 16), d1d8 = Convert.ToUInt32(h[7], 16), p164 = Convert.ToUInt32(h[8], 16), w15c = Convert.ToUInt32(h[9], 16);
            int failAt = int.Parse(h[10]), npass = int.Parse(h[11]);
            bool pend = h[12] == "1";
            var events = new List<string>();
            var markerPbis = new[] { NewPbi(), NewPbi(), NewPbi() };
            var pbi1 = NewPbi();
            pbi1.Pitch44 = cents; pbi1.Flags1BE = (byte)h1be; pbi1.Flags1BF = (byte)(h1be >> 8); pbi1.Flags1BD = (byte)b1bd; pbi1.Word1B4 = s1b4; pbi1.StartOffset = d1d8; pbi1.Ratio = BitConverter.UInt32BitsToSingle(p164);
            pbi1.Word15C = w15c; pbi1.SourceFormat158 = rate; pbi1.Byte160 = (byte)WwiseResamplerTests.FmtWord(isFloat, ch); pbi1.Byte161 = (byte)(WwiseResamplerTests.FmtWord(isFloat, ch) >> 8);
            var src1 = new ScriptSource(events, "1", ParseBlocks(parts[1]), pbi1, markerPbis, ch, isFloat, rate, w15c);
            var voice = new WwiseLiveVoice(ch, 1024) { Source = src1, Pbi388 = pbi1 };
            var pbi2 = NewPbi();
            ScriptSource? src2 = null;
            if (pend)
            {
                pbi2.Pitch44 = Bits(h[21]); pbi2.StartOffset = Convert.ToUInt32(h[14], 16); pbi2.Ratio = BitConverter.UInt32BitsToSingle(Convert.ToUInt32(h[15], 16)); pbi2.Word1DC = 0x11; pbi2.Word1E0 = 0x22;
                pbi2.SourceFormat158 = Convert.ToUInt32(h[16], 16); pbi2.Word15C = Convert.ToUInt32(h[17], 16);
                uint fmt2 = Convert.ToUInt32(h[18], 16); pbi2.Byte160 = (byte)fmt2; pbi2.Byte161 = (byte)(fmt2 >> 8);
                pbi2.Flags0E8 = (byte)Convert.ToUInt32(h[19], 16); pbi2.Flags0E9 = (byte)Convert.ToUInt32(h[20], 16);
                src2 = new ScriptSource(events, "2", ParseBlocks(parts[2]), pbi2, markerPbis, ch, h[22] == "1", rate, w15c) { StartResult = (int)Convert.ToUInt32(h[13], 16) };
                voice.Pending = src2;
            }
            if (parts[3] != "-")
                foreach (var slotSpec in parts[3].Split(';'))
                {
                    var sp = slotSpec.Split('/');
                    int index = int.Parse(sp[0]);
                    var r38 = new List<int>(sp[1].Split('.').Select(v => Convert.ToInt32(v, 16)));
                    var r3c = new List<int>(sp[2].Split('.').Select(v => Convert.ToInt32(v, 16)));
                    static int Next(List<int> l) { int v = l[0]; if (l.Count > 1) l.RemoveAt(0); return v; }
                    voice.InsertFxSlots[index] = new WwiseVoiceInsertFxSlot
                    {
                        Execute38Hook = b => { events.Add($"s38({index})"); b.Result = Next(r38); },
                        Execute3CHook = b => { events.Add($"s3C({index})"); b.Result = Next(r3c); },
                        ReleaseVtCHook = () => true,
                    };
                }
            WwiseMixBus? bus = null;
            if (parts[4] != "-")
            {
                var cf = parts[4].Split(' ');
                bus = new WwiseMixBus(default, Array.Empty<WwiseBusFxSlot>(), 1024);
                if (cf[0] == "1") { bus.MixInput(); bus.ReleaseBuffer(); }
                var dd = new Xs(Convert.ToUInt32(cf[5], 16));
                for (int i = 0; i < 1024; i++) bus.Buffer[i] = dd.F32();
                var conn = new WwiseVoiceConnection(bus, ch, 1)
                {
                    C08 = Bits(cf[1]), C0C = Bits(cf[2]), C10 = Bits(cf[3]), C14 = Bits(cf[4]),
                };
                var mm = new Xs(Convert.ToUInt32(cf[6], 16));
                var ma = new float[ch];
                var mb = new float[ch];
                for (int i = 0; i < ch; i++) ma[i] = WwiseMixKernelTests.GainValue(mm);
                for (int i = 0; i < ch; i++) mb[i] = WwiseMixKernelTests.GainValue(mm);
                conn.Mixer.Refresh(ma, 1f);
                conn.Mixer.Refresh(mb, 1f);
                conn.Descriptor.Reserve(ch, 1);                                                          // [conn+0x18] != 0
                voice.Connections.Add(conn);
            }
            int allocs = 0;
            voice.PitchNode.AllocationFails = failAt == 0 ? null : () => ++allocs == failAt;
            voice.PitchNode.SwitchToNextSrcA549A0 = v => { events.Add("switch"); v.Source = v.Pending; };   // the node clears [voice+0xD8] after the seam (0xA549F8)
            voice.PitchNode.ContextVt24A52D38 = _ => events.Add("ctx24");
            voice.PitchNode.ContextVt28A52CD4 = _ => events.Add("ctx28");
            voice.SourceNotReadyA55C14 = (_, _) => events.Add("handler");
            voice.StartStreamFormatWriter = (_, _) => { };                                              // the harness's StartStream stub writes no format bytes
            Assert.True(voice.StartResamplerA5321C());                                                   // 0xA54A30 -> 0xA5321C(N, F2, pbi, [voice+0xEC])
            Assert.Equal(npass, passesExpected.Length);
            for (int pass = 0; pass < npass; pass++)
            {
                var S = voice.Buffer.State;
                voice.Buffer.InitPassBlockA44A00();
                S.Position = 0; S.Word1C = 0; S.Total = 0; S.Rate = 0;                                      // the engine's +0x18..+0x24 are uninitialised stack: the harness zeroes them
                events.Clear();
                try { voice.Render(_ => events.Add("notify")); }
                catch (Exception ex) { throw new Exception($"{halves[0]} pass {pass}: {ex.GetType().Name} {ex.Message}", ex); }
                var (snap, tail) = Snapshot(voice, events, pbi1, pbi2, src2, markerPbis, bus);
                events.Clear();
                voice.ReleaseChainVtC();
                string rel = $"{H((uint)voice.PitchNode.Out.ValidFrames)} {H(voice.PitchNode.Out.MaxFrames)} {H(voice.PitchNode.Out.Data is null ? 0u : 1u)} {H(voice.PitchNode.Resampler.OutputOffset28)}";
                string got = snap + " | " + WwiseResamplerTests.Sha(System.Text.Encoding.UTF8.GetBytes(tail + " | " + rel));
                Assert.True(passesExpected[pass] == got, $"{halves[0]} pass {pass} (python re-analysis/tools/emu/emu_pitch.py --node SEED prints the engine's long form)" + Environment.NewLine + $"engine [{passesExpected[pass]}]" + Environment.NewLine + $"C#     [{got}]" + Environment.NewLine + $"C# long [{snap} | {tail} | {rel}]");
            }
        }
    }

    /// <summary>A source of the engine's shape whose vt+0x30 would leave scripted results (the cases that reach it are in the random scenarios): here only its owner PBI, its latch and vt+0x28 are used.</summary>
    private sealed class StubSource : IWwisePitchNodeSource
    {
        private readonly List<string> _events;
        public StubSource(List<string> events) { _events = events; Pbi = NewPbi(); }
        public WwisePlayingInstance Pbi { get; }
        public int StartResult { get; set; } = 1;
        public bool LogStart { get; set; }
        public int Channels => 1;
        public int SampleRate => WwiseRuntimeSettings.MixRateHz;
        public bool HasPitchNode => true;
        public WwiseDecodeState Io { get; set; } = new();
        public WwisePlayingInstance? Owner => Pbi;
        public bool StartStreamSucceeded { get; set; }
        public void ReleaseOutput() => _events.Add("rel");
        public int Render(WwiseVoiceBuffer buffer) => throw new InvalidOperationException("a case that stops before the source loop");
        public int StartStream(uint arg1DC, uint arg1E0)
        {
            if (LogStart) _events.Add($"start({arg1DC},{arg1E0})");
            return StartResult;
        }
    }

    [Fact]
    public void M6_022_The_voice_order_slot_walks_and_0xA548C0_match_the_engine_for_the_cases_that_stop_before_the_pitch_pass()
    {
        // C31 R5.3 (0xA548C0 with the verifier's R4.8 correction), V2-01 (the slot walks): the engine's own 0xA44630 on a hand-built voice (emu_decode_cases.py pull_cases: the real 0xA44630, 0xA53134, 0xA52D4C, 0xA548C0 with its callees
        // 0x9CBACC, 0xA56650 and 0xA05574; the slot, source and callee bodies the harness names log the call and leave the scripted result): the vt+0x38 walk down the slots (an empty one skipped, 0x2B to the next lower, other than 0x2D / 0x11 returns),
        // the vt+0x3C walk up, the slot-3 result going straight to filter A, 0xA548C0 (the play-position update, the stop-offset clamp with an unsigned 32-bit compare and the byte [state+0x2C], the pending source's start through 0xA56650 and its
        // result 2) and the early return before the notify. The cases that reach the pitch pass are tested with the real node (the 1600 random scenarios), because the engine run behind them stopped at the unread consumption.
        int ran = 0;
        foreach (var (name, (slotSpec, node, srcSpec, pitch, f1be, b8, extraSpec, step)) in WwiseVorbisEngineOracle.Pull)
        {
            if (step.Contains("pitch(")) continue;
            ran++;
            var events = new List<string>();
            var extra = extraSpec.Length == 0 ? new Dictionary<string, string>() : extraSpec.Split(';').Select(x => x.Split('=', 2)).ToDictionary(x => x[0], x => x[1]);
            var src = new StubSource(events);
            var voice = new WwiseLiveVoice(1, 64) { Source = src, Pbi388 = src.Pbi };
            voice.Buffer.Result = 0x2B;
            if (slotSpec.Length != 0)
                foreach (var sl in slotSpec.Split(';'))
                {
                    var sp = sl.Split(':');
                    int index = int.Parse(sp[0]);
                    var r38 = new List<int>(sp[1].Split('/').Select(int.Parse));
                    var r3c = new List<int>(sp[2].Split('/').Select(int.Parse));
                    static int Next(List<int> l) { int v = l[0]; if (l.Count > 1) l.RemoveAt(0); return v; }
                    voice.InsertFxSlots[index] = new WwiseVoiceInsertFxSlot
                    {
                        Execute38Hook = b => { events.Add($"s38({index})"); b.Result = Next(r38); },
                        Execute3CHook = b => { events.Add($"s3C({index})"); b.Result = Next(r3c); },
                    };
                }
            voice.SourceNotReadyA55C14 = (_, _) => events.Add("handler");
            voice.StartStreamFormatWriter = (_, _) => { };                                       // the harness's StartStream stub writes no format bytes
            var repo = new WwisePlayPositionRepository(() => 1000);                              // the harness's clock() returns 1000
            voice.PositionRepository = repo;
            src.Pbi.Pitch44 = (float)pitch;
            src.Pbi.Flags1BE = (byte)f1be;
            voice.PitchNode.ByteB8 = (byte)b8;
            voice.PitchNode.ByteB9 = 0xEE;
            voice.PitchNode.Word48 = 0x7777;
            voice.Buffer.ValidFrames = 0x1234;
            voice.PitchNode.Held60.ValidFrames = (ushort)node;
            var S = voice.Buffer.State;
            if (extra.TryGetValue("valid", out var v)) S.ValidFrames = ushort.Parse(v);
            if (extra.TryGetValue("f1f8", out v)) src.Pbi.Field1F8 = uint.Parse(v);
            if (extra.TryGetValue("flags4", out v)) src.Pbi.Flags4 = uint.Parse(v);
            if (extra.TryGetValue("pos", out v)) S.Position = uint.Parse(v);
            if (extra.TryGetValue("w1c", out v)) S.Word1C = uint.Parse(v);
            if (extra.TryGetValue("total", out v)) S.Total = uint.Parse(v);
            if (extra.TryGetValue("rate", out v)) S.Rate = uint.Parse(v);
            StubSource? pending = null;
            if (extra.TryGetValue("pend", out v))
            {
                var pp = v.Split('/').Select(int.Parse).ToArray();
                pending = new StubSource(events) { StartResult = pp[0], LogStart = true, StartStreamSucceeded = pp[1] != 0 };
                pending.Pbi.Word1DC = (uint)pp[2]; pending.Pbi.Word1E0 = (uint)pp[3];
                voice.Pending = pending;
            }
            if (extra.TryGetValue("repo", out v))
            {
                repo.Records.Add(new WwisePlayPositionRecord { PlayingId = 0x4321, Source = src });
                repo.Capacity = 1;
                repo.Stamp20 = long.Parse(v);
            }
            voice.Render(_ => events.Add("notify"));
            string recs = string.Join(";", repo.Records.Select(r => $"{r.PlayingId}/{(ReferenceEquals(r.Source, src) ? 1 : 0)}/{r.Stamp8}/{r.Word10}.{r.Word14}.{r.Word18}.{r.Word1C}"));
            string got = $"{string.Join(",", events)} | end=ret res={voice.Buffer.Result:x} n48={voice.PitchNode.Word48} b9={voice.PitchNode.ByteB9} v={S.ValidFrames} b2c={(voice.Buffer.HasBusParam ? 1 : 0)} s1f8={src.Pbi.Field1F8:x} lat={(pending is null ? -1 : pending.StartStreamSucceeded ? 1 : 0)} repo=[{recs}]";
            Assert.True(step == got, $"{name}: engine [{step}] C# [{got}]");
        }
        Assert.True(ran >= 30, $"ran {ran}");
    }

    private static (WwiseLiveVoice voice, WwisePlayingInstance pbi, List<string> events) HandVoice(string blocks, uint startOffset, uint word1B4, byte flags1BD = 0x44, float ratio = 1f)
    {
        var events = new List<string>();
        var pbi = NewPbi();
        pbi.StartOffset = startOffset; pbi.Word1B4 = word1B4; pbi.Flags1BD = flags1BD; pbi.Ratio = ratio;
        pbi.Word15C = 0x4101; pbi.SourceFormat158 = 48000; pbi.Byte160 = (byte)WwiseResamplerTests.FmtWord(true, 1); pbi.Byte161 = (byte)(WwiseResamplerTests.FmtWord(true, 1) >> 8);
        var src = new ScriptSource(events, "1", ParseBlocks(blocks), pbi, new[] { NewPbi(), NewPbi(), NewPbi() }, 1, true, 48000, 0x4101);
        var voice = new WwiseLiveVoice(1, 1024) { Source = src, Pbi388 = src.Pbi };
        voice.SourceNotReadyA55C14 = (_, _) => events.Add("handler");
        Assert.True(voice.StartResamplerA5321C());
        voice.Buffer.InitPassBlockA44A00();
        return (voice, pbi, events);
    }

    private static float[] Samples(uint seed, int n)
    {
        var d = new Xs(seed);
        var f = new float[n];
        for (int i = 0; i < n; i++) f[i] = d.F32();
        return f;
    }

    [Fact]
    public void M6_022_The_first_buffer_starts_with_the_silent_frames_of_the_play_delay()
    {
        // C38.1 P1-03 (verification hand case): D = [pbi+0x1D8] = -300, P = [pbi+0x164] = 1.0, F = 1024: n = trunc(((D + P*F)/P) + 0.5) = 724 silent frames, then the source samples 0..299; the 1024 asked are complete (0x2D).
        var (voice, pbi, events) = HandVoice("2D/300/0/ABCD/0/0", unchecked((uint)-300), 0);
        voice.Render(_ => events.Add("notify"));
        Assert.Equal(0x2D, voice.Buffer.Result);
        Assert.Equal(1024, voice.Buffer.State.ValidFrames);
        var data = (float[])voice.Buffer.State.Data!;
        for (int i = 0; i < 724; i++) Assert.Equal(0u, BitConverter.SingleToUInt32Bits(data[i]));
        var expected = Samples(0xABCD, 300);
        for (int i = 0; i < 300; i++) Assert.Equal(BitConverter.SingleToUInt32Bits(expected[i]), BitConverter.SingleToUInt32Bits(data[724 + i]));
        Assert.Equal(0, voice.PitchNode.ByteBA);                                                            // the fill ran once
    }

    [Fact]
    public void M6_022_The_start_offset_skips_the_first_frames_and_skip_everything_consumes_the_block()
    {
        // C38.1 P1-04 (verification hand cases): a start offset [pbi+0x1B4] = 100 with 1024 frames held gives 924 frames from sample 100 (and clears the offset and bits 0, 1 of +0x1BE); an offset at or beyond the held frames skips the whole
        // block ([pbi+0x1B4] = d - v, the block released, no output); bit 7 of [pbi+0x1BD] (an offset the source has not consumed) gives no skip.
        var (voice, pbi, events) = HandVoice("2D/1024/0/1111/0/0", unchecked((uint)-1024), 100);
        voice.Render(_ => events.Add("notify"));
        Assert.Equal(924, voice.PitchNode.Out.ValidFrames);
        var expected = Samples(0x1111, 1024);
        var data = (float[])voice.PitchNode.Out.Data!;
        for (int i = 0; i < 924; i++) Assert.Equal(BitConverter.SingleToUInt32Bits(expected[100 + i]), BitConverter.SingleToUInt32Bits(data[i]));
        Assert.Equal(0u, pbi.Word1B4);

        var (voice2, pbi2, events2) = HandVoice("2D/1024/0/1111/0/0", unchecked((uint)-1024), 2000);
        voice2.Render(_ => events2.Add("notify"));
        Assert.Equal(976u, pbi2.Word1B4);                                                                   // d - v = 2000 - 1024
        Assert.Equal(0, voice2.PitchNode.Out.ValidFrames);
        Assert.Contains("rel1", events2);

        var (voice3, pbi3, events3) = HandVoice("2D/1024/0/1111/0/0", unchecked((uint)-1024), 100, flags1BD: 0xC4);
        voice3.Render(_ => events3.Add("notify"));
        Assert.Equal(1024, voice3.Buffer.State.ValidFrames);                                                // no skip: the whole block
        Assert.Equal(100u, pbi3.Word1B4);
    }

    private sealed class OwnerlessSource : IWwisePitchNodeSource
    {
        public int Channels => 1;
        public int SampleRate => WwiseRuntimeSettings.MixRateHz;
        public bool HasPitchNode => true;
        public WwiseDecodeState Io { get; set; } = new();
        public WwisePlayingInstance? Owner => null;
        public bool StartStreamSucceeded { get; set; }
        public void ReleaseOutput() { }
        public int Render(WwiseVoiceBuffer buffer) => 0x2D;
        public int StartStream(uint a, uint b) => 1;
    }

    [Fact]
    public void M6_022_What_no_row_settles_is_a_visible_stop_through_the_live_entry()
    {
        // 0xA548C0 / the slot walk: a filled slot with no vt+0x38 body, the play-position repository, [state+0x1C] (uninitialised stack unless a source writes it), the pending source's format writer and its owner.
        WwiseLiveVoice Rig(out StubSource src)
        {
            src = new StubSource(new List<string>());
            var v = new WwiseLiveVoice(1, 64) { Source = src, Pbi388 = src.Pbi };
            v.InsertFxSlots[3] = new WwiseVoiceInsertFxSlot { Execute38Hook = b => b.Result = 0x2D };
            v.Buffer.Result = 0x2B;
            return v;
        }
        var v1 = Rig(out _);
        v1.InsertFxSlots[3] = new WwiseVoiceInsertFxSlot();
        Assert.Throws<WwiseMissingBehaviourException>(() => v1.Render());                                   // the slot's vt+0x38
        var v2 = Rig(out var s2);
        s2.Pbi.Flags4 = 0x100000; v2.Buffer.State.Position = 5;
        Assert.Throws<WwiseMissingBehaviourException>(() => v2.Render());                                   // [state+0x1C] never written
        v2.Buffer.State.Word1C = 0x3F800000;
        Assert.Throws<WwiseMissingBehaviourException>(() => v2.Render());                                   // no repository
        v2.PositionRepository = new WwisePlayPositionRepository(() => 0);
        v2.Render();
        Assert.Single(v2.PositionRepository.Records);
        var v3 = Rig(out _);
        v3.Pending = new StubSource(new List<string>());
        Assert.Throws<WwiseMissingBehaviourException>(() => v3.Render());                                   // StartStream ran, its format writer is unwired
        var v4 = Rig(out _);
        v4.Pending = new OwnerlessSource();
        Assert.Throws<WwiseMissingBehaviourException>(() => v4.Render());                                   // the pending source's owner
    }

    [Fact]
    public void M6_022_The_pending_source_arm_and_the_pool_failure_stop_visibly_or_follow_the_engine()
    {
        // 0xA52C9C: 0xA549A0 is an outline only; 0xA52CB4..0xA52CE0: the new PBI's context calls vt+0x24 / vt+0x28 are required; a pool failure of the output allocation stores 0x2 (0xA53040..0xA53048).
        WwiseLiveVoice Rig(byte e8, byte e9, out List<string> events)
        {
            var (voice, _, ev) = HandVoice("11/4/0/1111/0/0", unchecked((uint)-1024), 0);
            events = ev;
            var pbi2 = NewPbi();
            pbi2.Word15C = 0x4101; pbi2.Flags0E8 = e8; pbi2.Flags0E9 = e9;
            voice.Pending = new ScriptSource(ev, "2", new List<Block>(), pbi2, new[] { NewPbi(), NewPbi(), NewPbi() }, 1, true, 48000, 0x4101);
            voice.StartStreamFormatWriter = (_, _) => { };
            return voice;
        }
        var a = Rig(0, 0, out _);
        Assert.Throws<WwiseMissingBehaviourException>(() => a.Render());                                    // SwitchToNextSrcA549A0
        a = Rig(0, 0, out _);
        a.PitchNode.SwitchToNextSrcA549A0 = v => v.Source = v.Pending;
        Assert.Throws<WwiseMissingBehaviourException>(() => a.Render());                                    // ContextVt24A52D38
        a = Rig(0x20, 1, out _);
        a.PitchNode.SwitchToNextSrcA549A0 = v => v.Source = v.Pending;
        a.PitchNode.ContextVt24A52D38 = _ => { };
        Assert.Throws<WwiseMissingBehaviourException>(() => a.Render());                                    // ContextVt28A52CD4
        a.PitchNode.ContextVt28A52CD4 = _ => { };
        a = Rig(0x20, 1, out _);
        a.PitchNode.SwitchToNextSrcA549A0 = v => v.Source = v.Pending;
        a.PitchNode.ContextVt28A52CD4 = _ => { };
        a.Render();
        Assert.Null(a.Pending);                                                                             // 0xA549F8 [voice+0xD8] = 0
        var (voice, _, _) = HandVoice("2D/300/0/ABCD/0/0", unchecked((uint)-300), 0);
        voice.PitchNode.AllocationFails = () => true;
        voice.Render();
        Assert.Equal(2, voice.Buffer.Result);
    }

    private static float Bits(string hex) => BitConverter.UInt32BitsToSingle(Convert.ToUInt32(hex, 16));
}
