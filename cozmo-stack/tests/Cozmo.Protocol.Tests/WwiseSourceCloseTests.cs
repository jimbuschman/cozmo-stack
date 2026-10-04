using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>The state of a source object a close scenario starts from (the same fields emu_close.py builds in emulated memory).</summary>
internal sealed class CloseState
{
    public uint Count;
    public bool Array;
    public uint[] Elems = System.Array.Empty<uint>();
    public bool Stream;
    public uint P44, P60, P64, P80, PA4, PC0, PE4, PEC, WF0, WF4, BF8;
}

/// <summary>
/// M6-025 / M6-022 (C34.3 S1..S7, S10): the six source classes' close slots (vt+0x2C: 0xA73128, 0xA72AF4, 0xA7427C, 0xA76178, 0xAB0FC0, 0xAB2958, and 0xA759D8 of the class 0x103D8C8) and the duration slot vt+0x34 = 0xA72F5C.
/// Every expected value is the engine's own output from re-analysis/tools/emu/emu_close.py (the real slots, read from the vtables of the .so, run under Unicorn on a source object with the chunk container at +0x2C,
/// the pointer fields and a stream stand-in at +0x3C; the pool free 0xA7A988 / 0xA7A914, the Vorbis DSP teardown 0xAB3428 and the stream's vt+8 are recorded). The generated WwiseSourceCloseOracle.cs holds the inputs and
/// the outputs. The pool's accounting is not modelled: the frees are compared as the order and the set of freed blocks.
/// </summary>
public class WwiseSourceCloseTests
{
    private static string Token(string field, uint pointer) => field switch
    {
        "[c+4]" => "arr",
        "[src+0x44]" => "44",
        "[S+0x60]" => "60",
        "[S+0x64]" => "64",
        "[src+0x80]" => "80",
        "[src+0xC0]" => "C0",
        "[S+0xA4]" => "A4",
        "[S+0xE4]" => "E4",
        "[S+0xEC]" => "EC",
        _ when field.StartsWith("[c+4][") => pointer.ToString("X"),
        _ => throw new InvalidOperationException(field),
    };

    private static void FillContainer(WwiseChunkContainer c, CloseState st)
    {
        c.Count = st.Count;
        c.ArrayPtr = st.Array ? 0xAAAA0000u : 0u;
        c.ElementPtrs8.AddRange(st.Elems);
    }

    private static string Final(WwiseChunkContainer c, uint p44, uint p60, uint p64, uint p80, uint pA4, uint pC0, uint pE4, uint pEC, uint wF0, uint wF4, uint bF8, bool streamLeft)
        => $"array={(c.ArrayPtr != 0 ? 1 : 0)} bF8={bF8} count={c.Count} p44={p44} p60={p60} p64={p64} p80={p80} pA4={pA4} pC0={pC0} pE4={pE4} pEC={pEC} wF0={wF0} wF4={wF4} stream={(streamLeft ? 1 : 0)} elems={string.Join(",", c.ElementPtrs8.Take(c.ElementPtrs8.Count))}";

    [Fact]
    public void S2_S7_EveryOracleScenarioFreesTheSameBlocksInTheSameOrderAndLeavesTheSameFields()
    {
        // The set of scenarios is emu_close.py's CASES: for each class the empty source, a container with elements (a null element is skipped), a count of 0 with an array (the array is still freed), a null array with a count
        // (only the count is cleared, nothing is freed), then the class's own fields: ADPCM [+0x44]; ADPCM streamed [+0x64]; PCM streamed [+0x60] (it clears [+0x64] with it, and a null [+0x60] keeps [+0x64]); Vorbis in-memory
        // [+0x80] / [+0xC0]; Vorbis streamed [+0xA4], [+0xE4] and the owned setup copy (byte [+0xF8] with [+0xEC]).
        Assert.NotEmpty(SourceCloseOracle.Close);
        foreach (var (name, (kind, st, frees, calls, fields)) in SourceCloseOracle.Close)
        {
            var log = new List<string>();
            var callLog = new List<string>();
            WwisePoolFree sink = (f, p) => log.Add(Token(f, p));
            string final;
            if (kind is "pcm_mem" or "adpcm_mem")
            {
                var fields0 = new WwiseInMemorySourceFields(kind switch { "pcm_mem" => WwiseInMemoryKind.Pcm, _ => WwiseInMemoryKind.Adpcm })
                {
                    Ptr44 = st.P44,
                };
                FillContainer(fields0.Container2C, st);
                fields0.Close2C(sink);
                final = Final(fields0.Container2C, fields0.Ptr44, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, false);
            }
            else if (kind == "vorbis_mem")
            {
                // The in-memory Vorbis class is WwiseVorbisInMemorySource now (its state is its own, no longer host input): the DSP teardown 0xAB3428 is the real one, observed through the decoder state.
                var pbi0 = new WwisePlayingInstance(new WwisePlayInitParams { PlayingId = 1, TargetNodeId = 1 }, 1, new WwiseSourceDescriptor(0, 1, 5, 0, 0), new byte[0x44], null, continuous: false);
                var ctx = new WwiseVorbisEngineContext();
                var m = new WwiseVorbisInMemorySource(pbi0, ctx, () => true, sink);
                m.Frame.Dsp.ArraysAllocated = true;
                m.Frame.Dsp.OverlapAllocated = true;
                ctx.Shared.Users = 1;
                m.OutputBlock80 = st.P80 != 0 ? new float[1] : null;
                m.Frame.Frames = 0x3C3C;
                m.SeekCopyC0 = st.PC0 != 0 ? new byte[4] : null;
                FillContainer(m.Container2C, st);
                m.Close2CAB0FC0();
                if (!m.Frame.Dsp.ArraysAllocated && ctx.Shared.Users == 0) callLog.Add("dsp");
                final = Final(m.Container2C, 0, 0, 0, m.OutputBlock80 is not null ? st.P80 : 0, 0, m.SeekCopyC0 is not null ? st.PC0 : 0, 0, 0, 0, 0, 0, false);
                Assert.True(m.Frame.Frames == (st.P80 != 0 ? 0u : 0x3C3Cu), $"{name}: [src+0x3C] is cleared with [src+0x80] only");
            }
            else
            {
                var rig = new StreamRig(fileSize: 4096, content: new byte[4096]);
                var seams = new WwiseStreamSourceSeams { PoolFree = sink };
                var pbi = new WwisePlayingInstance(new WwisePlayInitParams { PlayingId = 1, TargetNodeId = 1 }, 1, new WwiseSourceDescriptor(0, 1, 5, 0, 0), new byte[0x44], null, continuous: false);
                var block = new WwiseSourceBlock150 { SourceId04 = 5 };
                WwiseAutoStream? stream = st.Stream ? rig.Create() : null;
                if (kind == "vorbis_str")
                {
                    var v = new WwiseVorbisStreamSource(rig.Manager, pbi, block, seams) { Stream3C = stream };
                    FillContainer(v.Container2C, st);
                    v.OutputA4 = st.PA4 != 0 ? new float[1] : null;
                    v.Frame.Dsp.ArraysAllocated = true;
                    v.Frame.Dsp.OverlapAllocated = true;
                    seams.Vorbis.Shared.Users = 1;
                    v.SeekTable = st.PE4 != 0 ? new byte[4] : null;
                    v.SetupPacket = st.PEC != 0 ? new WwiseBytePtr(new byte[8], 0) : default;
                    v.SetupOwned = st.BF8 != 0;
                    v.SetupPayloadCollected = st.WF0;
                    v.SetupPrefixCollected = st.WF4;
                    v.Close2CAB2958();
                    if (!v.Frame.Dsp.ArraysAllocated && seams.Vorbis.Shared.Users == 0) callLog.Add("dsp");     // 0xAB2964 bl 0xAB3428: the real teardown, observed through the decoder state
                    final = Final(v.Container2C, 0, 0, 0, 0, v.OutputA4 is not null ? st.PA4 : 0, 0, v.SeekTable is not null ? st.PE4 : 0, v.SetupPacket.IsNull ? 0u : st.PEC,
                        v.SetupPayloadCollected, v.SetupPrefixCollected, v.SetupOwned ? 1u : 0u, v.Stream3C is not null);
                    if (stream is not null) Assert.True((stream.Flags2D & 8) != 0, $"{name}: [S+0x3C]->vt+8 (Destroy 0x9654E4) ran");
                    if (stream is not null) callLog.Add("stream_vt8");
                }
                else
                {
                    var a = new WwisePcmAdpcmStreamSource(rig.Manager, pbi, block, seams)
                    {
                        Stream3C = stream, Class = kind switch { "adpcm_str" => WwisePcmAdpcmClass.AdpcmStream, "pcm_str" => WwisePcmAdpcmClass.PcmStream, _ => WwisePcmAdpcmClass.Class103D8C8 },
                        BufferPtr60 = kind == "adpcm_str" ? 0 : st.P60, BufferPtr64 = kind == "adpcm_str" ? 0 : st.P64,
                        Output64 = kind == "adpcm_str" && st.P64 != 0 ? new short[2] : null,
                    };
                    FillContainer(a.Container2C, st);
                    a.Close2C();
                    final = Final(a.Container2C, 0, a.BufferPtr60, kind == "adpcm_str" ? (a.Output64 is not null ? st.P64 : 0) : a.BufferPtr64, 0, 0, 0, 0, 0, 0, 0, 0, a.Stream3C is not null);
                    if (stream is not null) Assert.True((stream.Flags2D & 8) != 0, $"{name}: [S+0x3C]->vt+8 (Destroy 0x9654E4) ran");
                    if (stream is not null) callLog.Add("stream_vt8");
                }
            }
            Assert.True(frees.SequenceEqual(log), $"{name}: frees engine [{string.Join(",", frees)}] C# [{string.Join(",", log)}]");
            Assert.True(calls.OrderBy(x => x).SequenceEqual(callLog.OrderBy(x => x)), $"{name}: callee calls engine [{string.Join(",", calls)}] C# [{string.Join(",", callLog)}]");
            Assert.Equal(fields, final);                                                                // every field of the source object after the close, as the engine left it
        }
    }

    [Fact]
    public void S1_0xA72F5C_TheDurationIsTheEnginesFloatForEveryOracleScenario()
    {
        // C34.3 S1, emu_close.py: (total + (loops - 1) * (loopEnd + 1 - loopStart)) * 1000.0f / rate in single precision (vmla not fused, the u32 / s32 conversions), 0.0f for zero loops, infinity for rate 0.
        Assert.NotEmpty(SourceCloseOracle.Duration);
        foreach (var (kind, total, loopStart, loopEnd, loops, rate, bits) in SourceCloseOracle.Duration)
        {
            var rig = new StreamRig(fileSize: 100, content: new byte[100]);
            var pbi = new WwisePlayingInstance(new WwisePlayInitParams { PlayingId = 1, TargetNodeId = 1 }, 1, new WwiseSourceDescriptor(0, 1, 5, 0, 0), new byte[0x44], null, continuous: false)
            {
                LoopCount1B8 = loops, SourceFormat158 = rate,
            };
            WwiseStreamSourceBase src = kind == "vorbis_str"
                ? new WwiseVorbisStreamSource(rig.Manager, pbi, new WwiseSourceBlock150(), new WwiseStreamSourceSeams())
                : new WwisePcmAdpcmStreamSource(rig.Manager, pbi, new WwiseSourceBlock150(), new WwiseStreamSourceSeams());
            src.TotalSamples14 = total;
            src.Word24 = loopStart;
            src.Word28 = loopEnd;
            float r = src.Duration34A72F5C();
            Assert.True(bits == BitConverter.SingleToUInt32Bits(r), $"{kind} {total}/{loopStart}/{loopEnd}/{loops}/{rate}: engine 0x{bits:X8}, C# 0x{BitConverter.SingleToUInt32Bits(r):X8}");
        }
    }

    [Fact]
    public void S2_S7_AClassWithoutItsStateOrSeamIsAVisibleStop()
    {
        var rig = new StreamRig(fileSize: 100, content: new byte[100]);
        var pbi = new WwisePlayingInstance(new WwisePlayInitParams { PlayingId = 1, TargetNodeId = 1 }, 1, new WwiseSourceDescriptor(0, 1, 5, 0, 0), new byte[0x44], null, continuous: false);
        // The base destructors 0xA75A0C / 0xA73304 are named but unread (V22): the destructors are a visible stop without the seam.
        var vorbis = new WwiseVorbisStreamSource(rig.Manager, pbi, new WwiseSourceBlock150(), new WwiseStreamSourceSeams());
        Assert.Throws<WwiseMissingBehaviourException>(() => vorbis.DestroyAB11BC());
        Assert.Throws<WwiseMissingBehaviourException>(() => new WwiseVorbisInMemorySource(pbi, new WwiseVorbisEngineContext(), () => true).DestroyAB02E0());
        // The PCM / ADPCM stream object's class decides its close.
        var unnamed = new WwisePcmAdpcmStreamSource(rig.Manager, pbi, new WwiseSourceBlock150(), new WwiseStreamSourceSeams());
        Assert.Throws<WwiseMissingBehaviourException>(() => unnamed.Close2C());
        // A source class that has built neither slot throws (the interface default).
        IWwiseVoiceSource plain = new NoCloseSource();
        Assert.Throws<WwiseMissingBehaviourException>(() => plain.Close2C());
        Assert.Throws<WwiseMissingBehaviourException>(() => plain.Duration34());
    }

    private sealed class NoCloseSource : IWwiseVoiceSource
    {
        public int Channels => 1;
        public int SampleRate => 48000;
        public int Render(WwiseVoiceBuffer buffer) => 0x2D;
        public int StartStream(uint arg1DC, uint arg1E0) => 1;
        public bool StartStreamSucceeded { get; set; }
    }
}
