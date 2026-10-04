// fidelity: M6-025
namespace Cozmo.Robot.Animation.Wwise;

/// <summary>
/// The streamed Vorbis source class (vtable <c>0x103E138</c>, 0xFC bytes, constructor <c>0xAB1B38</c>): <c>vt+0x28 = 0xAB22D4</c> StartStream, <c>vt+0x30 = 0xAB1550</c> decode, <c>vt+0x74 = 0xAB1138</c> loop / end,
/// <c>vt+0x78 = 0xAB12B4</c> header parse, <c>vt+0x7C = 0xAB1020</c> seek lookup (C32.3: the earlier labels of 0x103E0B8 and 0x103E138 were reversed; <c>0x103E0B8</c> is the 0xD0 in-memory class with <c>vt+0x28 = 0xAB0B20</c>).
/// Header state <c>[S+0x68]</c>: 0 before the header, 1 reading the seek table (<c>[S+0xC4]</c> bytes into <c>[S+0xE4]</c>), 2 reading the setup packet (u16 length prefix; <c>[S+0xEC]</c> the packet,
/// <c>[S+0xF0]</c> payload bytes collected, <c>[S+0xF4]</c> prefix bytes collected, byte <c>[S+0xF8]</c> the copy buffer is owned), 3 running; <c>0xAB7E40</c> writes 4 at the last packet. The constructor zeroes
/// <c>S+0x60..0xEB</c>, so the state starts at 0 (C33.3). The frame block <c>F = S+0x60</c> (<see cref="Frame"/>) holds <c>[S+0x60]</c> frames, <c>[S+0x64]</c> status, <c>[S+0x68]</c> state, <c>[S+0x6C]</c> consumed,
/// the decoder state <c>D = S+0x70</c>, <c>[S+0xA4]</c> the output block, <c>[S+0xA8]</c> the channel word, <c>[S+0xB0]</c> / <c>[S+0xB4]</c> the bytes available and the ready byte. The decode body of <c>vt+0x30</c>
/// is <see cref="DecodeAB1550"/>: its packet decode is <c>0xAB7E40</c> (<see cref="WwiseVorbisFraming.FrameLoopAB7E40"/>) and its output hand-off <c>0xA73490</c> (<see cref="WwiseSourceOutput.HandoffA73490"/>).
/// </summary>
public sealed class WwiseVorbisStreamSource : WwiseStreamSourceBase
{
    private byte[]? _seekTable;                                                 // [S+0xE4]

    /// <summary><c>F = S+0x60</c>: the frame block (<c>0xAB7E40</c>'s argument).</summary>
    internal WwiseVorbisFrameBlock Frame { get; } = new();

    /// <summary><c>[S+0xE4]</c>: the seek-table block (null is the zero pointer). Internal setter: the header parse allocates it; the close tests place one.</summary>
    internal byte[]? SeekTable { get => _seekTable; set => _seekTable = value; }

    /// <summary><c>[S+0x68]</c>: the header state (0 to 3; 4 after the last packet).</summary>
    public int State68 { get => Frame.State; private set => Frame.State = value; }

    /// <summary><c>[S+0xE8]</c>: the seek-table bytes collected.</summary>
    public uint SeekCollected { get; private set; }

    /// <summary><c>[S+0xEC]</c>: the setup packet pointer (a pointer into a buffer, or an owned copy).</summary>
    public WwiseBytePtr SetupPacket { get; internal set; }

    /// <summary><c>[S+0xF0]</c>: the setup payload bytes collected.</summary>
    public uint SetupPayloadCollected { get; internal set; }

    /// <summary><c>[S+0xF4]</c>: the setup prefix bytes collected.</summary>
    public uint SetupPrefixCollected { get; internal set; }

    /// <summary>Byte <c>[S+0xF8]</c>: the setup packet is an owned copy.</summary>
    public bool SetupOwned { get; internal set; }

    /// <summary><c>[S+0x80]</c>: the setup the cache record holds (<c>0xAB2D74</c>'s result), null for the zero pointer.</summary>
    public WwiseVorbisSetup? CodebookHandle80 => Frame.Dsp.Setup;

    /// <summary><c>[S+0xB8..0xDD]</c>: the 0x26-byte vorb block copied from the format chunk (<c>fmt+0x1C</c>).</summary>
    public byte[] VorbBlock { get; } = new byte[0x26];

    /// <summary><c>[S+0xA8]</c>: the channel word (<c>fmt+0x14</c>); its low byte is the channel count (<c>0xAB3264</c>'s argument).</summary>
    public uint ChannelWordA8 { get => Frame.ChannelConfig; private set => Frame.ChannelConfig = value; }

    /// <summary><c>[S+0xE0]</c>: the sample rate (<c>fmt+4</c>).</summary>
    public uint SampleRateE0 { get; private set; }

    /// <summary><c>[S+0x70+0x2C]</c> (u16): the leading skip the decoder still has to drop (<c>0xAB3244</c> stores it; the packet entry consumes it).</summary>
    public ushort LoopStateSkip2C => unchecked((ushort)Frame.Dsp.Skip);

    /// <summary><c>[S+0x70+0x2E]</c> (u16): the end trim (<c>0xAB3244</c>'s second argument).</summary>
    public ushort LoopStateWord2E => unchecked((ushort)Frame.Dsp.Trim);

    /// <summary>True once <c>0xAB3244</c> has run (<c>[L+0x20] = [L+0x1C] = -1</c>).</summary>
    public bool LoopStateInitialised { get; private set; }

    /// <summary>Creates the source.</summary>
    public WwiseVorbisStreamSource(WwiseStreamManager manager, WwisePlayingInstance pbi, WwiseSourceBlock150 src, WwiseStreamSourceSeams seams)
        : base(manager, pbi, src, seams) { }

    /// <summary>
    /// <c>vt+0x2C</c> = <c>0xAB2958(S)</c> (C34.3 S7): <c>0xAB3428(S+0x70)</c> (the DSP teardown, a required seam); <c>vt+0xC</c> = <c>0xAB1100</c>: a non-null <c>[S+0xA4]</c> is freed and <c>[S+0x60]</c>, <c>[S+0xA4]</c> cleared; a non-null
    /// <c>[S+0xE4]</c> is freed and cleared; with byte <c>[S+0xF8] != 0</c> and <c>[S+0xEC] != 0</c> the setup copy is freed and <c>[S+0xEC]</c>, <c>[S+0xF8]</c>, <c>[S+0xF4]</c>, <c>[S+0xF0]</c> cleared; then <c>0xA759D8</c> (the stream's
    /// <c>vt+8</c>, the container).
    /// </summary>
    public void Close2CAB2958()
    {
        WwiseVorbisDsp.TeardownAB3428(Frame.Dsp, Seams.Vorbis.Shared);          // 0xAB2964 bl 0xAB3428(S+0x70)
        ReleaseOutputAB1100();                                                  // 0xAB2970 vt+0xC
        if (_seekTable is not null)                                             // 0xAB2978..0xAB2998
        {
            Seams.PoolFree?.Invoke("[S+0xE4]", 0);                              // 0xAB2990 bl 0xA7A988
            _seekTable = null;
        }
        if (SetupOwned && !SetupPacket.IsNull)                                  // 0xAB299C..0xAB29BC
        {
            Seams.PoolFree?.Invoke("[S+0xEC]", 0);                              // 0xAB29CC bl 0xA7A988
            SetupPacket = default;                                              // 0xAB29D8
            SetupOwned = false;                                                 // 0xAB29DC
            SetupPrefixCollected = 0;                                           // 0xAB29E0
            SetupPayloadCollected = 0;                                          // 0xAB29E4
        }
        ReleaseStreamA759D8();                                                  // 0xAB29EC b 0xA759D8
    }

    /// <summary>
    /// <c>vt+0xC = 0xAB1100(S)</c> (V08): a non-null <c>[S+0xA4]</c> is freed (<c>0xA7A914</c>) and <c>[S+0x60]</c>, <c>[S+0xA4]</c> cleared. The pitch node calls it when it has consumed the whole delivered block.
    /// </summary>
    public void ReleaseOutputAB1100()
    {
        if (Frame.Output is null) return;                                       // 0xAB1100..0xAB1108
        Seams.PoolFree?.Invoke("[S+0xA4]", 0);                                  // 0xAB1120 bl 0xA7A914
        Frame.Frames = 0;                                                       // 0xAB1128 [S+0x60]
        Frame.Output = null;                                                    // 0xAB112C [S+0xA4]
    }

    /// <summary>
    /// <c>vt+0x0 / +0x4</c> (<c>0xAB11BC</c> destructor, <c>0xAB1234</c> deleting destructor; V22 with the verifier's correction): a non-null <c>[S+0xA4]</c> is freed and <c>[S+0x60]</c>, <c>[S+0xA4]</c> cleared; a non-null <c>[S+0x80]</c>
    /// releases the setup record (<c>0xAB3120(*0x108E638, S+0x60)</c>); then the base destructor <c>0xA75A0C</c> (a seam: its body is not read).
    /// </summary>
    public void DestroyAB11BC()
    {
        ReleaseOutputAB1100();                                                  // 0xAB11C0..0xAB11F8 (the same free and clears)
        if (Frame.Dsp.Setup is not null)                                        // 0xAB11FC..0xAB1204
        {
            Seams.Vorbis.SetupCache.ReleaseAB3120(BitConverter.ToUInt32(VorbBlock, 0x20));   // 0xAB1214 bl 0xAB3120(table, S+0x60): the key is [F+0x78] = [S+0xD8]
            Frame.Dsp.Setup = null;
        }
        (Seams.BaseDestructor ?? throw new WwiseMissingBehaviourException(
            "M6-025 V22: the base destructor 0xA75A0C is named by the verifier but its body is not read; supply WwiseStreamSourceSeams.BaseDestructor"))(this);   // 0xAB121C bl 0xA75A0C
    }

    private WwiseAutoStream St => Stream3C ?? throw new InvalidOperationException("M6-025: [S+0x3C] is null (the engine would dereference it)");

    // ---------------------------------------------------------------- G6: 0xAB12B4 (vt+0x78)

    /// <summary>
    /// G6 <c>0xAB12B4(S, data)</c>: <c>0x9CD340(data, [S+0x44], ...)</c> (a seam) not 1 is returned; the format tag must be <c>0xFFFF</c> else 7; the PBI's format bytes are written: <c>+0x162</c> bit 0 := 1, bit 1 := 0 (<c>bfi ip, r0, #0, #2</c> at 0xAB1354 with r0 = 1, the walker's result) and bit 2 set,
    /// <c>+0x15D = (u32 &gt;&gt; 8) &amp; 0xFF</c>, <c>+0x158 = fmt[4]</c>, <c>+0x161 = u16 fmt[2]</c>, <c>+0x15C = u32 &amp; 0xFF</c>, <c>+0x15E = (u32 &gt;&gt; 16) &amp; 0xFF</c>, <c>+0x160 = 0x20</c>, <c>+0x15F = u32 &gt;&gt; 24</c>
    /// (with <c>u32 = fmt[0x14]</c>; the corrected forms of C32.3). A non-null walker extra output runs <c>0xA7596C</c>. The vorb block (0x26 bytes from <c>fmt+0x1C</c>) is copied to <c>[S+0xB8]</c>, <c>[S+0x14] = fmt[0x18]</c>,
    /// <c>[S+0xA8] = u32</c>, <c>[S+0xE0] = fmt[4]</c>; with <c>[S+0x24] == 0 &amp;&amp; [S+0x28] == 0</c> <c>[S+0x28] = [S+0x14] - 1</c>. The loop bytes are <c>[S+0x54] = [S+0x20] + [S+0xC8]</c>, <c>[S+0x58] = [S+0x20] + [S+0x1C]</c> for
    /// <c>u16[S+0x38] == 1</c>, else <c>[S+0x54] = [S+0x20] + [S+0xB8] + [S+0xC4]</c>, <c>[S+0x58] = [S+0x20] + [S+0xC4] + [S+0xBC]</c>. The heuristics get throughput <c>float(fmt[8]) / 1000.0f</c> (<c>0x447A0000</c>), priority
    /// <c>(int)float[pbi+0x1C0]</c> and the loop words by <c>0xA75950</c> (<c>[S+0x54]</c>, <c>[S+0x58]</c>, or 0 and 0 for <c>u16[S+0x38] == 1</c>). A non-zero <c>[S+0xC4]</c> allocates <c>[S+0xE4]</c> (failure returns 0x34); then
    /// <c>[S+0x68] = 1</c> and <c>SetMinimalBufferSize(1)</c>, whose result is returned.
    /// </summary>
    protected override int ParseHeader(WwiseBytePtr data)
    {
        var walk = RunWalker9CD340(data, Left44);                               // 0xAB1308 bl 0x9CD340(data, [S+0x44], ...): the callee's stores through S+0x2C, S+0x24, S+0x28, S+0x1C, S+0x20
        if (walk.Result != 1) return walk.Result;                               // 0xAB130C..0xAB1318
        var fmt = walk.Format;
        if (fmt.U16(0) != 0xFFFF) return 7;                                     // 0xAB1324..0xAB1330
        var pbi = Pbi;
        uint u32 = fmt.U32(0x14);                                               // 0xAB133C ldr sb,[r6,#0x14]
        uint rate = fmt.U32(4);                                                 // 0xAB1340 ldr sl,[r6,#4]
        ushort channels = fmt.U16(2);                                           // 0xAB134C ldrh lr,[r6,#2]
        pbi.Byte162 = (byte)(((pbi.Byte162 & ~3) | 1) | 4);                     // 0xAB1354..0xAB1370 bfi ip,r0,#0,#2 (r0 = 1: bit 0 = 1, bit 1 = 0); orr ip,ip,#4
        byte b15C = (byte)u32;                                                  // 0xAB135C uxtb
        byte b15D = (byte)(u32 >> 8);                                           // 0xAB1368 (r8 | (r7 & 0xF) << 4)
        byte b15E = (byte)(u32 >> 16);                                          // 0xAB1360 ubfx r0,r7,#4,#8
        byte b15F = (byte)(u32 >> 24);                                          // 0xAB137C lsr r2,r7,#0xc
        pbi.SourceFormat158 = rate;                                             // 0xAB1384 str sl,[r3,#0x158]
        pbi.Word15C = (uint)(b15C | (b15D << 8) | (b15E << 16) | (b15F << 24)); // 0xAB1378, 0xAB138C, 0xAB1390, 0xAB1398
        pbi.Byte161 = (byte)channels;                                           // 0xAB1388 strb lr,[r3,#0x161]
        pbi.Byte160 = 0x20;                                                     // 0xAB1394 strb r1,[r3,#0x160]
        if (walk.AkdSize != 0)                                                  // 0xAB1380 cmp ip,#0; 0xAB139C bne 0xAB1500
            WalkerExtraA7596C(walk.Akd, walk.AkdSize);                          // 0xAB1508 bl 0xA7596C(S, &akd): the result is ignored
        uint total = fmt.U32(0x18);                                             // 0xAB13A0 ldr sl,[r6,#0x18]
        TotalSamples14 = total;                                                 // 0xAB13B0 str sl,[r4,#0x14]
        for (int i = 0; i < VorbBlock.Length; i++) VorbBlock[i] = fmt[0x1C + i];   // 0xAB13A4..0xAB13E8 (0x26 bytes)
        ChannelWordA8 = u32;                                                    // 0xAB13F0..0xAB1408 (the byte, the nibble and the bit field rebuild the word)
        SampleRateE0 = rate;                                                    // 0xAB1414 str r3,[r4,#0xe0]
        if (Word24 == 0 && Word28 == 0) Word28 = unchecked(total - 1);          // 0xAB1410..0xAB1428
        uint c4 = BitConverter.ToUInt32(VorbBlock, 0xC);                        // [S+0xC4]
        uint b8 = BitConverter.ToUInt32(VorbBlock, 0x0);                        // [S+0xB8]
        uint bc = BitConverter.ToUInt32(VorbBlock, 0x4);                        // [S+0xBC]
        uint c8 = BitConverter.ToUInt32(VorbBlock, 0x10);                       // [S+0xC8]
        if (LoopCount38 == 1)                                                   // 0xAB142C..0xAB1430 beq 0xAB1534
        {
            LoopStartByte54 = unchecked(HeaderSize20 + c8);                     // 0xAB1534..0xAB153C
            LoopEndByte58 = unchecked(HeaderSize20 + DataSize1C);               // 0xAB1538..0xAB1540
        }
        else
        {
            LoopStartByte54 = unchecked(HeaderSize20 + b8 + c4);                // 0xAB1448..0xAB1450
            LoopEndByte58 = unchecked(HeaderSize20 + c4 + bc);                  // 0xAB144C..0xAB1454
        }
        var h = new WwiseStreamHeuristics();
        St.GetHeuristics961664(h);                                              // 0xAB1474 vt+0x14
        bool flag = LoopCount38 - 1 != 0;                                       // 0xAB1478..0xAB1488 (subs r1,r1,#1; movne r1,#1)
        h.LoopStart = flag ? LoopStartByte54 : 0;                               // 0xA75950
        h.LoopEnd = flag ? LoopEndByte58 : 0;
        h.Throughput = (float)fmt.U32(8) / BitConverter.Int32BitsToSingle(0x447A0000);   // 0xAB14AC..0xAB14B4 vcvt.f32.u32; vdiv.f32 (1000.0f)
        h.Priority = unchecked((byte)TruncS32(pbi.Priority1C0));                // 0xAB14B0, 0xAB14B8..0xAB14C4
        St.SetHeuristics964E4C(h);                                              // 0xAB14D0 vt+0x18
        if (c4 != 0)                                                            // 0xAB14D4..0xAB14DC
        {
            if (!Manager.TryAlloc()) { _seekTable = null; return 0x34; }        // 0xAB151C..0xAB1530 (the pool 0x1052428)
            _seekTable = new byte[c4];                                          // 0xAB1524 str r0,[r4,#0xe4]
        }
        State68 = 1;                                                            // 0xAB14EC str r3,[r4,#0x68]
        return St.SetMinimalBufferSize965244(1);                                // 0xAB14F8 vt+0x1C; 0xAB14FC b 0xAB1314 (r0 is returned)
    }

    // ---------------------------------------------------------------- G5: 0xAB3244, 0xAB3264

    /// <summary><c>0xAB3244(L, a, b)</c>: <c>u16[L+0x2C] = a</c>, <c>u16[L+0x2E] = b</c>, <c>[L+0x20] = [L+0x1C] = -1</c>; returns 0.</summary>
    private void SetLoopStateAB3244(ushort a, ushort b)
    {
        WwiseVorbisDsp.ResetAB3244(Frame.Dsp, a, b);
        LoopStateInitialised = true;
    }

    /// <summary>
    /// <c>0xAB3264(S+0x70, byte[S+0xA8])</c> (<see cref="WwiseVorbisDsp.AllocateAB3264"/>): true when it returns non-zero (the caller returns 2).
    /// </summary>
    private bool OutputBuffersFailAB3264()
    {
        if (Frame.Dsp.Setup is null) throw new InvalidOperationException("M6-025 G3: [S+0x80] is null at 0xAB3264 (the engine reads [[D+0x10]+4])");
        Seams.Vorbis.OnDspAllocate?.Invoke((byte)ChannelWordA8);
        return WwiseVorbisDsp.AllocateAB3264(Frame.Dsp, (byte)ChannelWordA8, Seams.Vorbis.Shared, Manager.TryAlloc) != 0;
    }

    private ushort LoopStateWordForCount() => LoopCount38 != 1 ? BitConverter.ToUInt16(VorbBlock, 0xA) : BitConverter.ToUInt16(VorbBlock, 0x16);   // u16[S+0xC2] or u16[S+0xCE]

    /// <summary>
    /// <c>vt+0x74 = 0xAB1138(S, r1)</c> (V07): <c>r1 != 0</c> decrements <c>u16[S+0x38]</c> when it is above 1 and returns 0x11. <c>r1 == 0</c> decrements it when above 1, <c>u16[S+0x5C] -= 1</c>, resets the decoder
    /// (<c>0xAB3244(S+0x70, u16[S+0xC0], loops == 1 ? u16[S+0xCE] : u16[S+0xC2])</c>, so the next packet is a priming packet), <c>[S+0x64] = 0x2D</c>, <c>[S+0x68] = 3</c>, and returns 0x2D.
    /// </summary>
    public override int LoopOrEnd74(int r1)
    {
        ushort loops = LoopCount38;                                             // 0xAB1140 / 0xAB119C
        if (r1 != 0)                                                            // 0xAB1138..0xAB113C
        {
            if (loops > 1) LoopCount38 = unchecked((ushort)(loops - 1));        // 0xAB11A0..0xAB11A8
            return 0x11;                                                        // 0xAB11AC
        }
        if (loops > 1) { loops = unchecked((ushort)(loops - 1)); LoopCount38 = loops; }   // 0xAB1148..0xAB115C
        LoopCounter5C = unchecked((ushort)(LoopCounter5C - 1));                 // 0xAB1150, 0xAB1164, 0xAB116C
        ushort skip = BitConverter.ToUInt16(VorbBlock, 0x8);                    // 0xAB1154 ldrh r1,[r0,#0xc0]
        ushort trim = loops != 1 ? BitConverter.ToUInt16(VorbBlock, 0xA) : BitConverter.ToUInt16(VorbBlock, 0x16);   // 0xAB1170 [S+0xC2]; 0xAB11B4 [S+0xCE]
        SetLoopStateAB3244(skip, trim);                                         // 0xAB1180
        Frame.Status = 0x2D;                                                    // 0xAB118C
        State68 = 3;                                                            // 0xAB1194
        return 0x2D;                                                            // 0xAB1188
    }

    /// <summary>
    /// <c>vt+0x7C = 0xAB1020(S, pos, &amp;samples, &amp;bytes)</c> (V19): <c>pos == 0</c> gives <c>(0, [S+0xC8] + [S+0x20])</c> and 1. Else with no entries (<c>[S+0xC4] / 4</c>) or no table <c>[S+0xE4]</c> it gives (0, 0) and 2. Else the
    /// <c>{u16 samples, u16 bytes}</c> entries are walked until <c>pos &lt; cumulative samples</c>: the result is the samples before that entry and <c>bytes before + [S+0xC4] + [S+0x20]</c> (the first entry: 0 and
    /// <c>[S+0xC8] + [S+0x20]</c>); past the last entry the samples are the table total and the bytes the sum plus the table size plus the header; 1.
    /// </summary>
    protected override (int Result, uint SampleBase, uint ByteOffset) SeekLookup(uint pos)
    {
        uint c4 = BitConverter.ToUInt32(VorbBlock, 0xC);                        // [S+0xC4]: the seek table size
        uint c8 = BitConverter.ToUInt32(VorbBlock, 0x10);                       // [S+0xC8]: the first audio packet's offset
        if (pos == 0) return (1, 0, unchecked(c8 + HeaderSize20));              // 0xAB1020..0xAB1024, 0xAB10BC..0xAB10D4
        uint entries = c4 >> 2;                                                 // 0xAB1034
        if (entries == 0 || _seekTable is null) return (2, 0, 0);               // 0xAB1038..0xAB105C
        uint cumSamples = 0, cumBytes = 0;                                      // r8, r4
        for (uint i = 0; ;)
        {
            uint after = unchecked(cumSamples + BitConverter.ToUInt16(_seekTable, (int)(4 * i)));   // 0xAB106C..0xAB1070
            if (pos < after)                                                    // 0xAB1074..0xAB1078 blo 0xAB10DC
            {
                if (i == 0) return (1, 0, unchecked(c8 + HeaderSize20));        // 0xAB10DC..0xAB10F0
                return (1, cumSamples, unchecked(cumBytes + c4 + HeaderSize20));   // 0xAB10F8, 0xAB1098..0xAB10B0
            }
            i++;                                                                // 0xAB107C
            cumBytes = unchecked(cumBytes + BitConverter.ToUInt16(_seekTable, (int)(4 * (i - 1) + 2)));   // 0xAB1080, 0xAB108C
            cumSamples = after;                                                 // 0xAB1090
            if (entries <= i) break;                                            // 0xAB1084 cmp r6,r5; 0xAB1094 bhi
        }
        return (1, cumSamples, unchecked(cumBytes + c4 + HeaderSize20));        // 0xAB1098..0xAB10B0
    }

    // ---------------------------------------------------------------- G1: 0xAB22D4 (vt+0x28)

    /// <summary>
    /// G1 <c>0xAB22D4(S)</c> (the StartStream the voice pass calls through <c>0xA56650</c>). Bit 2 of <c>[S+0x5E]</c> clear: with no stream or <c>[S+0x68] &gt; 2</c> (signed) a stream is created
    /// (<c>0xA74564(S, {0,0,0}, 0)</c>), the prefetch started (<c>0xA7482C</c>) and the stream started (<c>vt+0x30</c>), each non-1 result being returned. <b>has == 0:</b> <c>0xAB2088</c> (not 1 returned), then
    /// <c>0xAB2BFC</c>. <b>has != 0:</b> <c>[S+0x18] = 0</c>, <c>u16[S+0x38] = u16[pbi+0x1B8]</c>, <c>0xAB1C04</c> (not 1 returned, 0x3F possible), <c>0xAB3244(S+0x70, 0, u16[S+0xC2] or u16[S+0xCE])</c>, <c>[S+0x68] = 3</c>,
    /// return 1 with <b>no buffering gate</b> (bit 2 of <c>[S+0x5E]</c> is not set on this path). With a stream and <c>[S+0x68] &lt;= 2</c>: <c>0xAB2088</c> (not 1 returned) then the gate and the callback block. Bit 2 set: bit 1
    /// of <c>[S+0x10]</c> clear gives 1, else the gate; then the callback block.
    /// </summary>
    public int StartStreamAB22D4()
    {
        if ((Bits5E & 4) == 0)                                                  // 0xAB22E4 tst r3,#4; bne 0xAB2350
        {
            if (Stream3C is null || State68 > 2)                                // 0xAB22EC..0xAB2300
            {
                var settings = new WwiseStreamBufferSettings();                 // 0xAB2310..0xAB2318 {0, 0, 0}
                int r = CreateStreamA74564(settings, 0);                        // 0xAB2320
                if (r != 1) return r;                                           // 0xAB2324..0xAB2330
                r = PrefetchStartA7482C(out bool has);                          // 0xAB2430
                if (r != 1) return r;                                           // 0xAB2434..0xAB2438
                r = St.Start964CB8();                                           // 0xAB2448 vt+0x30
                if (r != 1) return r;                                           // 0xAB244C..0xAB2450
                if (!has)                                                       // 0xAB2454..0xAB2458 beq 0xAB262C
                {
                    r = FirstBufferAB2088();                                    // 0xAB2630
                    if (r != 1) return r;                                       // 0xAB2634..0xAB2638
                    return BufferingGateAB2BFC();                               // 0xAB2640 bl 0xAB2BFC
                }
                SampleBase18 = 0;                                               // 0xAB2464 str r5,[r4,#0x18]
                LoopCount38 = Pbi.LoopCount1B8;                                 // 0xAB246C..0xAB2474
                r = HeaderStateMachineAB1C04();                                 // 0xAB247C
                if (r != 1) return r;                                           // 0xAB2480..0xAB2484
                SetLoopStateAB3244(0, LoopStateWordForCount());                 // 0xAB2488..0xAB24A0
                State68 = 3;                                                    // 0xAB24A4..0xAB24AC
                return 1;
            }
            int first = FirstBufferAB2088();                                    // 0xAB2334
            if (first != 1) return first;                                       // 0xAB2338..0xAB2344
            return BufferingGateAB2BFC();                                       // 0xAB24EC (the same gate and callback block as G4)
        }
        return BufferingGateAB2BFC();                                           // 0xAB2350..0xAB25FC (bit 2 set: the gate, bit 1 clear giving 1)
    }

    /// <summary>G4 <c>0xAB2BFC(S)</c>: bit 1 of <c>[S+0x10]</c> clear gives 1, else the gate; then the callback block (F9).</summary>
    internal int BufferingGateAB2BFC()
    {
        int r = BufferingGate();                                                // 0xAB2BFC..0xAB2D4C
        BufferingCallback();                                                    // 0xAB2C4C..0xAB2D18
        return r;
    }

    // ---------------------------------------------------------------- G2: 0xAB2088

    /// <summary>
    /// G2 <c>0xAB2088(S)</c>: bit 1 of <c>[S+0x10]</c> := <c>(byte[pbi+0x1BD] &gt;&gt; 6) &amp; 1</c>; <c>GetBuffer(&amp;buf, &amp;[S+0x44], 0)</c>: 0x2E returns <b>0x3F</b>, anything but 0x2D / 0x11 returns 2. State 0:
    /// <c>vt+0x78(S, buf)</c> (not 1 returned), <c>[S+0x18] = 0</c>, <c>u16[S+0x38] = u16[pbi+0x1B8]</c>, <c>0xA746A8(S, buf, 0)</c> (not 1 returned), the header bytes skipped. Other states: <c>0xA746A8(S, buf, 0)</c>. Then
    /// <c>0xAB1C04</c>: 1 with bit 7 of <c>byte[pbi+0x1BD]</c> clear sets up the loop state, sets bit 2 of <c>[S+0x5E]</c>, <c>[S+0x68] = 3</c> and returns 1; with it set runs <c>0xA74BA0</c>, drops the buffer, clears the PBI
    /// bits, adds the skipped samples to <c>[S+0x18]</c> and finishes the same way, returning the seek's result. 0x3F with bytes left returns 0x3F; with none left the buffer is dropped (bit 1 of <c>[S+0x5E]</c> cleared, else
    /// ReleaseBuffer), <c>[S+0x40] = 0</c> and 0x3F is returned. Any other code is returned.
    /// </summary>
    internal int FirstBufferAB2088()
    {
        Flags10 = (byte)((Flags10 & ~2) | (((Pbi.Flags1BD >> 6) & 1) << 1));    // 0xAB20A8..0xAB20BC
        int r = St.GetBuffer965B1C(out var buf, out int off, out uint size, wait: false);   // 0xAB20C4 vt+0x40(&buf, S+0x44, 0)
        Left44 = size;
        if (r == 0x2E) return 0x3F;                                             // 0xAB20C8..0xAB20F0
        if (r != 0x2D && r != 0x11) return 2;                                   // 0xAB20D0..0xAB20E0
        var ptr = new WwiseBytePtr(buf, off);
        if (State68 == 0)                                                       // 0xAB20FC..0xAB2104
        {
            r = ParseHeader(ptr);                                               // 0xAB2114 vt+0x78
            if (r != 1) return r;                                               // 0xAB211C..0xAB2120
            SampleBase18 = 0;                                                   // 0xAB212C str r5,[r4,#0x18]
            LoopCount38 = Pbi.LoopCount1B8;                                     // 0xAB2130..0xAB2140
            r = ProcessBufferA746A8(ptr, 0);                                    // 0xAB2148
            if (r != 1) return r;                                               // 0xAB214C..0xAB2150
            Data40 = Data40.Add(unchecked((int)HeaderSize20));                  // 0xAB2154..0xAB216C
            Left44 = unchecked(Left44 - HeaderSize20);                          // 0xAB2164, 0xAB2170
            FileOffset48 = unchecked(FileOffset48 + HeaderSize20);              // 0xAB2168, 0xAB2174
        }
        else
        {
            r = ProcessBufferA746A8(ptr, 0);                                    // 0xAB21C4..0xAB21CC
            if (r != 1) return r;                                               // 0xAB21D0..0xAB21D4
        }
        int r5 = HeaderStateMachineAB1C04();                                    // 0xAB217C
        if (r5 == 1)                                                            // 0xAB2180..0xAB2188
        {
            ushort skip;
            if ((Pbi.Flags1BD & 0x80) == 0)                                     // 0xAB21DC..0xAB21EC
            {
                skip = 0;                                                       // 0xAB21E8 ands r1,r3,#0xff (zero)
            }
            else
            {
                r5 = SeekToStartOffsetA74BA0();                                 // 0xAB2228 bl 0xA74BA0
                if (Left44 != 0)                                                // 0xAB222C..0xAB2238
                {
                    if ((Bits5E & 2) != 0) Bits5E = (byte)(Bits5E & ~2);        // 0xAB223C..0xAB2248
                    else St.ReleaseBuffer964D64();                              // 0xAB22C0..0xAB22CC vt+0x44
                    Data40 = default; Left44 = 0;                               // 0xAB2250..0xAB2258
                }
                byte b1bd = Pbi.Flags1BD;                                       // 0xAB2264..0xAB2268
                bool bit7 = (b1bd & 0x80) != 0;                                 // 0xAB226C tst r2,#0x80
                Pbi.Flags1BD = (byte)(b1bd & ~0x80);                            // 0xAB2274..0xAB2278
                uint ip = bit7 ? 0 : Pbi.Word1B4;                               // 0xAB227C ldreq ip,[r3,#0x1b4]; 0xAB2290..0xAB2298 (movne ip,#0)
                skip = bit7 ? (ushort)0 : (ushort)ip;                           // 0xAB229C uxtheq r1,ip
                Pbi.Word1B4 = 0;                                                // 0xAB2284 str r0,[r3,#0x1b4]
                Pbi.Flags1BE = (byte)(Pbi.Flags1BE & ~3);                       // 0xAB2280, 0xAB2288, 0xAB228C
                SampleBase18 = unchecked(SampleBase18 + ip);                    // 0xAB2294..0xAB22A4
            }
            SetLoopStateAB3244(skip, LoopStateWordForCount());                  // 0xAB21F0..0xAB2204
            Bits5E |= 4;                                                        // 0xAB2208..0xAB221C
            State68 = 3;                                                        // 0xAB2218
            return r5;                                                          // 0xAB2210 mov r0,r5
        }
        if (r5 == 0x3F)                                                         // 0xAB218C..0xAB2190
        {
            if (Left44 != 0) return 0x3F;                                       // 0xAB2194..0xAB219C
            if ((Bits5E & 2) != 0) Bits5E = (byte)(Bits5E & ~2);                // 0xAB21A0..0xAB21B0
            else St.ReleaseBuffer964D64();                                      // 0xAB22AC..0xAB22B8
            Data40 = default;                                                   // 0xAB21BC str r3,[r4,#0x40]
            return 0x3F;                                                        // 0xAB21B8
        }
        return r5;                                                              // 0xAB2190 bne 0xAB20E8
    }

    // ---------------------------------------------------------------- G3: 0xAB1C04

    private void SetupAppend(byte b, int index)
    {
        SetupPacket.Array![SetupPacket.Index + index] = b;
    }

    /// <summary>
    /// G3 <c>0xAB1C04(S)</c>, the header state machine. State above 2: <c>0xAB3264(S+0x70, byte[S+0xA8])</c> (non-zero gives 2, else 1). <c>[S+0x44] == 0</c> returns <b>0x3F</b>. States 0 and 1: <c>min([S+0xC4] - [S+0xE8],
    /// [S+0x44])</c> bytes are copied into <c>[S+0xE4]</c>, advancing <c>[S+0x40/0x44/0x48]</c>, until <c>[S+0xE8] == [S+0xC4]</c>, then state 2 (a table size of 0 goes straight to 2). State 2: with no bytes left, bit 0 of
    /// <c>[S+0x5E]</c> set returns 2; else bit 1 set is cleared, otherwise ReleaseBuffer; <c>0xA74970</c> (F2): 0x2D continues, <b>0x2E returns 0x3F</b>, 2, 0x11 and 0x34 return 2, any other code goes to the finish. The
    /// 2-byte length is collected (<c>[S+0xF4]</c>), zero-copy when both bytes are in the buffer, else copied into a pool buffer of <c>u16[S+0xCC] + 2</c> bytes (null returns 2); then the payload
    /// (<c>min(size - collected, left)</c>); when complete (<c>[S+0xF0] == size</c>) <c>[S+0xF4] = [S+0xF0] = 0</c> and the finish runs: <c>0xAB2D74(globalTable, S+0x60, pbi, &amp;{ptr + 2, size, 0})</c> (null returns 2),
    /// <c>[S+0x68] = 3</c>, <c>[S+0x80] = [result]</c>, the copy buffer is freed, then the state-3 action.
    /// </summary>
    internal int HeaderStateMachineAB1C04()
    {
        int state = State68;                                                    // 0xAB1C04 ldr r2,[r0,#0x68]
        uint left, f4, r8, size, collected;
        int f2;
    L1c20:
        if (state > 2) goto L1f1c;                                              // 0xAB1C20..0xAB1C24 bgt
        if (Left44 == 0) return 0x3F;                                           // 0xAB1C28..0xAB1C30 -> 0xAB1FA0
        if (state != 2) goto L1f74;                                             // 0xAB1C34..0xAB1C38 bne
    L1c3c:
        left = Left44;                                                          // 0xAB1C3C (r7 = 0)
    L1c40:
        if (left != 0) goto L1dc8;                                              // 0xAB1C40..0xAB1C44 bne
    L1c48:                                                                      // no bytes left
        {
            byte b5e = Bits5E;                                                  // 0xAB1C48
            if ((b5e & 1) != 0) return 2;                                       // 0xAB1C4C..0xAB1C58 bne 0xAB1E68
            if ((b5e & 2) == 0)                                                 // 0xAB1C5C..0xAB1C64 beq 0xAB1E20
            {
                St.ReleaseBuffer964D64();                                       // 0xAB1E20..0xAB1E2C vt+0x44
            }
            else Bits5E = (byte)(b5e & ~2);                                     // 0xAB1C60 strbne r2,[r4,#0x5e]
        }
        f2 = GetBufferWrapperA74970();                                          // 0xAB1C68 / 0xAB1E30 bl 0xA74970
        if (f2 != 0x2D) goto L1e40;                                             // 0xAB1C70..0xAB1C74 / 0xAB1E38..0xAB1E3C
        f4 = SetupPrefixCollected;                                              // 0xAB1C78 (label 0xAB1C78)
        if (f4 > 1) goto L1cdc;                                                 // 0xAB1C7C..0xAB1C80 bhi
        left = Left44;                                                          // 0xAB1C84
        if (left == 0) goto L1c48;                                              // 0xAB1C88..0xAB1C8C beq
        r8 = 2 - f4;                                                            // 0xAB1C90
        if (r8 > left) goto L1de0;                                              // 0xAB1C94..0xAB1C98 bhi
    L1c9c:
        if (r8 != 2) goto L1de4;                                                // 0xAB1C9C..0xAB1CA0
        SetupPacket = Data40;                                                   // 0xAB1CA4..0xAB1CA8 [S+0xEC] = [S+0x40]
    L1cac:
        left = Left44 - r8;                                                     // 0xAB1CB0, 0xAB1CC0
        SetupPrefixCollected = f4 + r8;                                         // 0xAB1CAC, 0xAB1CC4
        Data40 = Data40.Add((int)r8);                                           // 0xAB1CBC, 0xAB1CCC
        Left44 = left;                                                          // 0xAB1CD0
        FileOffset48 += r8;                                                     // 0xAB1CC8, 0xAB1CD4
        if (SetupPrefixCollected != 2) goto L1c40;                              // 0xAB1CD8 bne 0xAB1C40
    L1cdc:
        if (SetupPayloadCollected != 0) goto L1e14;                             // 0xAB1CDC..0xAB1CE4
        size = SetupPacket.U16(0);                                              // 0xAB1CE8..0xAB1CF0
        if (Left44 < size && !SetupOwned)                                       // 0xAB1CF4..0xAB1D00 (bhs 0xAB1D44; byte [S+0xF8] != 0)
        {
            if (!Manager.TryAlloc()) return 2;                                  // 0xAB1D08..0xAB1D20 (u16 size + 2 bytes; null returns 2)
            var copy = new byte[size + 2];
            copy[0] = SetupPacket[0]; copy[1] = SetupPacket[1];                 // 0xAB1D28..0xAB1D30 strh r3,[r0]
            SetupPacket = new WwiseBytePtr(copy, 0);                            // 0xAB1D3C
            SetupOwned = true;                                                  // 0xAB1D40
        }
        goto L1d44;
    L1e14:
        size = SetupPacket.U16(0);                                              // 0xAB1E14..0xAB1E18
    L1d44:
        collected = SetupPayloadCollected;                                      // r2
        size = SetupPacket.U16(0);                                              // r1
        if (collected >= size) goto L1da0;                                      // 0xAB1D44..0xAB1D48 bhs 0xAB1E0C
        if (Left44 == 0) goto L1da0;                                            // 0xAB1D4C..0xAB1D54 beq 0xAB1E0C
        r8 = Math.Min(size - collected, Left44);                                // 0xAB1D58..0xAB1D68
        if (SetupOwned)                                                         // 0xAB1D6C..0xAB1D70 bne 0xAB1EA4
            for (uint i = 0; i < r8; i++) SetupAppend(Data40[(int)i], (int)(collected + 2 + i));   // 0xAB1EA4..0xAB1EB4 memcpy(ec + (collected + 2), [S+0x40], n)
        SetupPayloadCollected = collected + r8;                                 // 0xAB1D78, 0xAB1D88
        Data40 = Data40.Add((int)r8);                                           // 0xAB1D8C, 0xAB1D98
        Left44 -= r8;                                                           // 0xAB1D80, 0xAB1D90
        FileOffset48 += r8;                                                     // 0xAB1D94, 0xAB1D9C
    L1da0:
        if (SetupPrefixCollected == 2 && SetupPayloadCollected == SetupPacket.U16(0)) goto L1ec4;   // 0xAB1DA0..0xAB1DB8
        if (Left44 == 0) goto L1c48;                                            // 0xAB1DBC..0xAB1DC4 (0xAB1E0C reloads [S+0xF4] and joins 0xAB1DA0)
    L1dc8:
        left = Left44;
        f4 = SetupPrefixCollected;                                              // 0xAB1DC8
        if (f4 > 1) goto L1cdc;                                                 // 0xAB1DCC..0xAB1DD0
        r8 = 2 - f4;                                                            // 0xAB1DD4
        if (r8 <= left) goto L1c9c;                                             // 0xAB1DD8..0xAB1DDC bls
    L1de0:
        r8 = 1;                                                                 // 0xAB1DE0
    L1de4:
        f4 = SetupPrefixCollected;
        if (!SetupOwned)                                                        // 0xAB1DE4..0xAB1DEC beq 0xAB1E74
        {
            ushort cc = BitConverter.ToUInt16(VorbBlock, 0x14);                 // u16[S+0xCC]
            if (!Manager.TryAlloc()) { SetupPacket = default; return 2; }       // 0xAB1E7C..0xAB1E94 (the pool 0x1052428; the null is stored at [S+0xEC] first, then 2)
            SetupPacket = new WwiseBytePtr(new byte[cc + 2], 0);                // 0xAB1E90 str r0,[r4,#0xec]
            SetupOwned = true;                                                  // 0xAB1E9C strb r6,[r4,#0xf8]
            f4 = SetupPrefixCollected;                                          // 0xAB1E98
        }
        SetupAppend(Data40[0], (int)f4);                                        // 0xAB1DF0..0xAB1DFC strb r3,[r0,r2]
        goto L1cac;                                                             // 0xAB1E00..0xAB1E08
    L1e40:
        if (f2 == 0x2E) return 0x3F;                                            // 0xAB1E40..0xAB1E44 -> 0xAB1FA0
        r8 = (f2 == 2 || f2 == 0x11) ? 1u : 0u;                                 // 0xAB1E48..0xAB1E54
        if (f2 == 0x34) r8 |= 1;                                                // 0xAB1E58..0xAB1E5C
        if (r8 != 0) return 2;                                                  // 0xAB1E60..0xAB1E68
        return FinishSetup();                                                   // 0xAB1E64 beq 0xAB1ED0
    L1ec4:
        SetupPrefixCollected = 0; SetupPayloadCollected = 0;                    // 0xAB1EC4..0xAB1ECC
        return FinishSetup();
    L1f1c:
        return OutputBuffersFailAB3264() ? 2 : 1;                               // 0xAB1F1C..0xAB1F34 (non-zero gives 2, else 1)
    L1f74:
        {
            uint collectedSeek = SeekCollected;                                 // 0xAB1F74 ldr r2,[r4,#0xe8]
            uint table = BitConverter.ToUInt32(VorbBlock, 0xC);                 // 0xAB1F78 ldr r1,[r4,#0xc4]
            if (collectedSeek < table)                                          // 0xAB1F7C..0xAB1F80 blo 0xAB1FAC
            {
                uint n = Math.Min(table - collectedSeek, Left44);               // 0xAB1FAC..0xAB1FC4
                for (uint i = 0; i < n; i++) _seekTable![collectedSeek + i] = Data40[(int)i];   // 0xAB1FCC bl memcpy
                SeekCollected = collectedSeek + n;                              // 0xAB1FD8, 0xAB1FF4
                Data40 = Data40.Add((int)n);                                    // 0xAB1FEC
                Left44 -= n;                                                    // 0xAB1FF0, 0xAB2000
                FileOffset48 += n;                                              // 0xAB1FF8, 0xAB2004
                if (SeekCollected != table) goto L1f8c;                         // 0xAB2008 bne 0xAB1F8C
                goto L200c;
            }
            if (collectedSeek == table) goto L200c;                             // 0xAB1F84..0xAB1F88 beq
        }
    L1f8c:
        state = State68;                                                        // 0xAB1F8C ldr r2,[r4,#0x68]
        if (state != 2) goto L1c20;                                             // 0xAB1F90..0xAB1F94
        goto L1c3c;                                                             // 0xAB1F98 ldr r3,[r4,#0x44]; b 0xAB1C3C
    L200c:
        State68 = 2; state = 2;                                                 // 0xAB200C..0xAB2014
        goto L1c3c;                                                             // 0xAB2018
    }

    /// <summary>
    /// The finish of G3 (<c>0xAB1ED0..0xAB1F70</c>): <c>0xAB2D74(globalTable, S+0x60, pbi, &amp;{ptr + 2, u16 size, flag 0})</c> (the seam; null returns 2); <c>[S+0x68] = 3</c>; <c>[S+0x80] = [result]</c>; an owned copy is
    /// freed and <c>[S+0xEC]</c>, <c>[S+0xF8]</c>, <c>[S+0xF4]</c>, <c>[S+0xF0]</c> cleared; then the state-3 action <c>0xAB3264(S+0x70, byte[S+0xA8])</c> (non-zero gives 2, else 1).
    /// </summary>
    private int FinishSetup()
    {
        ushort size = SetupPacket.U16(0);                                       // 0xAB1EE4 ldrh lr,[ip],#2
        var codebooks = Seams.Vorbis.Codebooks ?? throw new WwiseMissingBehaviourException(
            "M6-025 G3: the packed codebook library is a host input (the engine's static table behind 0xAB63E0); supply WwiseVorbisEngineContext.Codebooks");
        var setupBytes = new ReadOnlyMemory<byte>(SetupPacket.Array!, SetupPacket.Index + 2, size);
        Seams.Vorbis.OnSetupAcquire?.Invoke(setupBytes, 0);
        var handle = Seams.Vorbis.SetupCache.AcquireAB2D74(                     // 0xAB1EF4 bl 0xAB2D74(*0x108E638, S+0x60, pbi, &{ptr + 2, size, 0})
            BitConverter.ToUInt32(VorbBlock, 0x20), BitConverter.ToUInt32(VorbBlock, 0x18), (byte)ChannelWordA8, VorbBlock[0x24], VorbBlock[0x25], setupBytes, codebooks, Manager.TryAlloc);
        if (handle is null) return 2;                                           // 0xAB1EF8..0xAB1EFC
        State68 = 3;                                                            // 0xAB1F10
        Frame.Dsp.Setup = handle;                                               // 0xAB1F14 str r3,[r4,#0x80]
        if (SetupOwned && !SetupPacket.IsNull)                                  // 0xAB1F18..0xAB1F3C
        {
            SetupPacket = default;                                              // 0xAB1F58 str r8,[r4,#0xec]
            SetupOwned = false;                                                 // 0xAB1F60
            SetupPrefixCollected = 0; SetupPayloadCollected = 0;                // 0xAB1F64, 0xAB1F68
        }
        return OutputBuffersFailAB3264() ? 2 : 1;                               // 0xAB1F1C..0xAB1F34 (state 3 is above 2)
    }

    // ---------------------------------------------------------------- G7: 0xAB1550 (vt+0x30), the buffering gate only

    /// <summary>
    /// G7 head <c>0xAB1550(S, state)</c>: see <see cref="WwiseStreamSourceBase.DecodeGateShared"/> (the same body as the ADPCM / PCM decode's head, <c>0xA73D34..0xA7408C</c>).
    /// </summary>
    public WwiseDecodeGate DecodeGateAB1550() => DecodeGateShared();

    // ---------------------------------------------------------------- G7: the decode loop 0xAB15CC..0xAB1B2C

    /// <summary><c>[S+0x60]</c> (u16 as <c>0xA73490</c> reads it): the frames the packet decode produced (<c>[F+0]</c>).</summary>
    public ushort FramesDecoded60 { get => unchecked((ushort)Frame.Frames); set => Frame.Frames = value; }

    /// <summary><c>[S+0x64]</c>: the decode status (0x2E at the start of every packet, 0x2B, 0x2D, 0x11, 2 ...), written by <c>0xAB7E40</c>.</summary>
    public int Status64 { get => Frame.Status; set => Frame.Status = value; }

    /// <summary><c>[S+0x6C]</c>: the bytes <c>0xAB7E40</c> consumed from the packet buffer (the engine's reading of it is <c>0xAB19AC..0xAB19E8</c>).</summary>
    public uint Consumed6C { get => Frame.Consumed; set => Frame.Consumed = value; }

    /// <summary><c>[S+0xB0]</c>: the total bytes of the packet the decode sees (<c>size + 2</c>, plus the bytes left in the buffer when it is not an owned copy).</summary>
    public uint InputB0 { get => Frame.Avail; private set => Frame.Avail = value; }

    /// <summary><c>[S+0xB4]</c> (byte): 1, 0, or <c>left == 0</c> (<c>0xAB187C..0xAB19A8</c>).</summary>
    public byte InputB4 { get => Frame.Ready; private set => Frame.Ready = value; }

    /// <summary><c>[S+0xA4]</c>: the output block of the last decode (planar floats; <c>0xAB7E40</c> allocates it, <c>0xA73490</c> publishes it, <c>vt+0xC</c> frees it).</summary>
    public float[]? OutputA4 { get => Frame.Output; set => Frame.Output = value; }

    /// <summary>
    /// G7 <c>0xAB1550(S, state)</c> (vt+0x30), the control flow. After the gate (<see cref="DecodeGateAB1550"/>) a blocked call stores its code in <c>[state+0x28]</c>. Otherwise per packet: <c>[S+0x64] = 0x2E</c>, <c>[S+0x60] = [S+0x6C] = 0</c>;
    /// with no bytes left bit 0 of <c>[S+0x5E]</c> set stores 0x11 and goes to the output stage, else bit 1 is cleared or the buffer released and <c>0xA74970</c> (F2) gives 0x2D (continue), any of 0x11 / 0x2E (stored; the output
    /// stage) or another code (stored; 2). The 2-byte length and the payload are collected as in G3 (a pool copy of <c>u16[S+0xCC] + 2</c> bytes, a null one stores 0x34 then 2). A complete packet sets <c>[S+0xB0]</c>, <c>[S+0xB4]</c>,
    /// runs <c>0xAB7E40(S+0x60, u16[S+0xCC], S+0xA4)</c> (the decode seam), stores <c>[S+0x64]</c> in <c>[state+0x28]</c>, adjusts the data pointer by what the decode reports when the packet was not an owned copy, frees an owned
    /// copy, and loops on 0x2B or 0x2E; 0x2D and 0x11 go to the output stage <c>0xA73490(S, [S+0xA4], u16[S+0x60], [S+0xE0], [S+0xA8], state)</c> (the seam), after which a code of 0x2E with <c>[S+0x40] != 0</c> becomes 0x2D when
    /// <c>[S+0x6C] != 0</c> and 2 otherwise; any other decode status stores 2.
    /// </summary>
    public void DecodeAB1550(WwiseDecodeState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var gate = DecodeGateAB1550();
        if (!gate.Decode) { state.Code28 = gate.Result; return; }                // 0xAB1B0C str r6,[r7,#0x28]
        uint left, f4, r8, size, collected;
        int f2, status;
        bool ok;
        int storedCode;
    L15d4:
        Status64 = 0x2E; FramesDecoded60 = 0; Consumed6C = 0;                  // 0xAB15D4..0xAB15E4 (r8 = 0x2E)
        left = Left44;                                                          // 0xAB15D4 ldr r3,[r4,#0x44]
    L15e8:
        if (left != 0) goto L176c;                                              // 0xAB15E8..0xAB15EC bne
    L15f0:
        {
            byte b5e = Bits5E;                                                  // 0xAB15F0
            if ((b5e & 1) != 0) { ok = true; storedCode = 0x11; goto L193c; }   // 0xAB15F4..0xAB1600 bne 0xAB1934 (r0 = 1, r2 = 0x11)
            if ((b5e & 2) == 0) St.ReleaseBuffer964D64();                       // 0xAB1604..0xAB160C beq 0xAB17C4: vt+0x44
            else Bits5E = (byte)(b5e & ~2);                                     // 0xAB1608 strbne r2,[r4,#0x5e]
        }
        f2 = GetBufferWrapperA74970();                                          // 0xAB1614 / 0xAB17D8 bl 0xA74970
        if (f2 != 0x2D) goto L17e4;                                             // 0xAB1618..0xAB161C
        f4 = SetupPrefixCollected;                                              // 0xAB1620 (label 0xAB1620)
        if (f4 > 1) goto L1684;                                                 // 0xAB1624..0xAB1628 bhi
        left = Left44;                                                          // 0xAB162C
        if (left == 0) goto L15f0;                                              // 0xAB1630..0xAB1634 beq
        r8 = 2 - f4;                                                            // 0xAB1638
        if (r8 > left) goto L1784;                                              // 0xAB163C..0xAB1640 bhi
    L1644:
        if (r8 != 2) goto L1788;                                                // 0xAB1644..0xAB1648
        SetupPacket = Data40;                                                   // 0xAB164C..0xAB1650
    L1654:
        left = Left44 - r8;                                                     // 0xAB1658, 0xAB1668
        SetupPrefixCollected = f4 + r8;                                         // 0xAB1654, 0xAB166C
        Data40 = Data40.Add((int)r8);                                           // 0xAB1664, 0xAB1674
        Left44 = left;                                                          // 0xAB1678
        FileOffset48 += r8;                                                     // 0xAB1670, 0xAB167C
        if (SetupPrefixCollected != 2) goto L15e8;                              // 0xAB1680 bne 0xAB15E8
    L1684:
        if (SetupPayloadCollected != 0) goto L17b8;                             // 0xAB1684..0xAB168C
        size = SetupPacket.U16(0);                                              // 0xAB1690..0xAB1698
        if (Left44 < size && !SetupOwned)                                       // 0xAB169C..0xAB16A8
        {
            if (!Manager.TryAlloc()) { ok = false; storedCode = 0x34; goto L193c; }   // 0xAB16B0..0xAB16C8 beq 0xAB1A04 (r0 = 0, r2 = 0x34)
            var copy = new byte[size + 2];
            copy[0] = SetupPacket[0]; copy[1] = SetupPacket[1];                 // 0xAB16D4 strh r3,[r0]
            SetupPacket = new WwiseBytePtr(copy, 0);                            // 0xAB16E4
            SetupOwned = true;                                                  // 0xAB16E8
        }
        goto L16ec;
    L17b8:
        size = SetupPacket.U16(0);                                              // 0xAB17B8..0xAB17BC
    L16ec:
        collected = SetupPayloadCollected;
        size = SetupPacket.U16(0);
        if (collected >= size) goto L17b0;                                      // 0xAB16EC..0xAB16F0 bhs
        if (Left44 == 0) goto L17b0;                                            // 0xAB16F4..0xAB16FC beq
        r8 = Math.Min(size - collected, Left44);                                // 0xAB1700..0xAB170C
        if (SetupOwned)                                                         // 0xAB1710..0xAB1714 bne 0xAB1830
            for (uint i = 0; i < r8; i++) SetupAppendG7(Data40[(int)i], (int)(collected + 2 + i));   // 0xAB1830..0xAB1840 memcpy(ec + (collected + 2), [S+0x40], n)
        SetupPayloadCollected = collected + r8;                                 // 0xAB171C, 0xAB1730
        Data40 = Data40.Add((int)r8);                                           // 0xAB172C, 0xAB173C
        Left44 -= r8;                                                           // 0xAB1720, 0xAB1738
        FileOffset48 += r8;                                                     // 0xAB1728, 0xAB1740
    L1744:
        if (SetupPrefixCollected == 2 && SetupPayloadCollected == SetupPacket.U16(0)) goto L1850;   // 0xAB1744..0xAB175C
        if (Left44 == 0) goto L15f0;                                            // 0xAB1760..0xAB1768 (0xAB17B0 reloads [S+0xF4] and joins 0xAB1744)
    L176c:
        left = Left44;
        f4 = SetupPrefixCollected;                                              // 0xAB176C
        if (f4 > 1) goto L1684;                                                 // 0xAB1770..0xAB1774
        r8 = 2 - f4;                                                            // 0xAB1778
        if (r8 <= left) goto L1644;                                             // 0xAB177C..0xAB1780 bls
    L1784:
        r8 = 1;                                                                 // 0xAB1784
    L1788:
        f4 = SetupPrefixCollected;
        if (!SetupOwned)                                                        // 0xAB1788..0xAB1790 beq 0xAB1800
        {
            ushort cc = BitConverter.ToUInt16(VorbBlock, 0x14);                 // u16[S+0xCC]
            if (!Manager.TryAlloc()) { SetupPacket = default; ok = false; storedCode = 0x34; goto L193c; }   // 0xAB1814..0xAB1820 beq 0xAB1A04 (the null is stored first)
            SetupPacket = new WwiseBytePtr(new byte[cc + 2], 0);                // 0xAB181C str r0,[r4,#0xec]
            SetupOwned = true;                                                  // 0xAB1828 strb r6,[r4,#0xf8]
            f4 = SetupPrefixCollected;                                          // 0xAB1824
        }
        SetupAppendG7(Data40[0], (int)f4);                                      // 0xAB1794..0xAB17A0 strb r3,[r0,r2]
        goto L1654;                                                             // 0xAB17A4..0xAB17AC
    L17b0:
        goto L1744;                                                             // 0xAB17B0 ldr r1,[r4,#0xf4]; b 0xAB1744
    L17e4:
        storedCode = f2;                                                        // 0xAB17E8 mov r2,r0
        ok = f2 == 0x11 || f2 == 0x2D || f2 == 0x2E;                            // 0xAB17E4..0xAB17F8
        goto L193c;
    L1850:
        {
            uint total = SetupPayloadCollected + 2;                             // 0xAB1850..0xAB1858 add r3,r3,#2 (r3 = [S+0xF0])
            SetupPrefixCollected = 0;                                           // 0xAB185C str r1,[r4,#0xf4]
            InputB0 = total;                                                    // 0xAB1864 str r3,[r4,#0xb0]
            SetupPayloadCollected = 0;                                          // 0xAB1868 str r1,[r4,#0xf0]
            if (LoopCounter5C == 0 && (Bits5E & 1) == 0)                        // 0xAB186C..0xAB1878 bne 0xAB187C / beq 0xAB19F0
            {
                InputB4 = 0;                                                    // 0xAB19F4 strb r1,[r4,#0xb4]
                if (SetupOwned) goto L1898;                                     // 0xAB19F8..0xAB19FC bne 0xAB19A4 (r1 = 0)
                InputB0 = total + Left44;                                       // 0xAB188C..0xAB1894
            }
            else
            {
                InputB4 = 1;                                                    // 0xAB1880 strb r6,[r4,#0xb4]
                if (SetupOwned) InputB4 = Left44 == 0 ? (byte)1 : (byte)0;      // 0xAB1998..0xAB19A4 clz r1,r1; lsr r1,r1,#5
                else InputB0 = total + Left44;                                  // 0xAB188C..0xAB1894
            }
        }
    L1898:
        WwiseVorbisFraming.FrameLoopAB7E40(Frame, BitConverter.ToUInt16(VorbBlock, 0x14), SetupPacket, Manager.TryAlloc);   // 0xAB1898..0xAB18A4 bl 0xAB7E40(S+0x60, u16[S+0xCC], [S+0xEC], S+0xA4)
        status = Status64;                                                      // 0xAB18A8
        state.Code28 = status;                                                  // 0xAB18B4 str r3,[r7,#0x28]
        if (status != 2 && !SetupOwned)                                         // 0xAB18B0 cmp r3,#2; beq 0xAB1B18; 0xAB18BC beq 0xAB19AC
        {
            if (Consumed6C != 0)                                                // 0xAB19AC..0xAB19B4
            {
                uint adj = unchecked(Consumed6C - 2 - SetupPacket.U16(0));      // 0xAB19B8..0xAB19D0
                Data40 = Data40.Add(unchecked((int)adj));                       // 0xAB19D4
                Left44 = unchecked(Left44 - adj);                               // 0xAB19D8
                FileOffset48 = unchecked(FileOffset48 + adj);                   // 0xAB19E0..0xAB19E8
            }
        }
        else if (SetupOwned && !SetupPacket.IsNull)                             // 0xAB18C4..0xAB18CC (the status 2 case enters here only with an owned copy: 0xAB1B18)
        {
            SetupPacket = default;                                              // 0xAB18E8
            SetupOwned = false;                                                 // 0xAB18EC
            SetupPrefixCollected = 0; SetupPayloadCollected = 0;                // 0xAB18F0, 0xAB18F4
        }
        if (status == 0x2B || status == 0x2E) goto L15d4;                       // 0xAB18F8..0xAB1900 beq 0xAB15D4
        {
            int code = state.Code28;                                            // 0xAB1904
            ok = code == 0x11 || code == 0x2D || code == 0x2E;                  // 0xAB1908..0xAB191C
            if (ok) goto L1948;
            state.Code28 = 2;                                                   // 0xAB1924
            return;
        }
    L193c:
        state.Code28 = storedCode;                                              // 0xAB193C str r2,[r7,#0x28]
        if (!ok) { state.Code28 = 2; return; }                                  // 0xAB1944 beq 0xAB1924
    L1948:
        WwiseSourceOutput.HandoffA73490(this, Frame.Output, FramesDecoded60, SampleRateE0, ChannelWordA8, state);   // 0xAB1948..0xAB1964 bl 0xA73490(S, [S+0xA4], u16[S+0x60], [S+0xE0], [S+0xA8], state)
        if (state.Code28 != 0x2E) return;                                       // 0xAB1968..0xAB1970
        if (Data40.IsNull) return;                                              // 0xAB1974..0xAB197C
        state.Code28 = Consumed6C != 0 ? 0x2D : 2;                              // 0xAB1980..0xAB198C / 0xAB1924
    }

    private void SetupAppendG7(byte b, int index) => SetupPacket.Array![SetupPacket.Index + index] = b;
}
