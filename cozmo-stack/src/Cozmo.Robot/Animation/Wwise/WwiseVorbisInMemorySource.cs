// fidelity: M6-025, M6-002
namespace Cozmo.Robot.Animation.Wwise;

/// <summary>
/// The in-memory Vorbis source class (vtable <c>0x103E0B8</c>, 0xD0 bytes, C32.3): <c>vt+0x28 = 0xAB0B20</c> StartStream, <c>vt+0x30 = 0xAB0448</c> decode, <c>vt+0x74 = 0xAB0374</c> loop / end, <c>vt+0xC = 0xAB032C</c>,
/// <c>vt+0x2C = 0xAB0FC0</c> close (research live-bodies-6 V10, V17, V19, V20, V22 and C34.3 S6; C36.1). Its frame block is <c>F = S+0x3C</c> (<see cref="Frame"/>: <c>[S+0x3C]</c> frames, <c>[S+0x40]</c> status, <c>[S+0x44]</c> state,
/// <c>[S+0x48]</c> consumed, <c>D = S+0x4C</c>, <c>[S+0x5C]</c> the setup, <c>[S+0x80]</c> the output block, <c>[S+0x84]</c> the channel word, <c>[S+0x8C]</c> / <c>[S+0x90]</c> the bytes available and the ready byte), the vorb block is at
/// <c>S+0x94</c> (<see cref="VorbBlock"/>), <c>[S+0xBC]</c> is the sample rate, <c>[S+0xC0]</c> the seek-table copy, <c>[S+0xC8]</c> the current packet and <c>[S+0xCC]</c> the data base (modelled as <see cref="CurrentOffsetC8"/>
/// from the data base). The media is resident: no stream, no copy buffer, no buffering gate. The lifecycle slots (<c>vt+0x14</c>, <c>vt+0x18 = 0xAB0750</c>, <c>vt+0x64 = 0xAB05E4</c>; V21) are not read and not built.
/// </summary>
public sealed class WwiseVorbisInMemorySource : IWwiseSourceCommon
{
    private readonly WwiseVorbisEngineContext _ctx;
    private readonly WwisePoolFree? _poolFree;
    private readonly Func<bool> _tryAlloc;
    private readonly Action<object>? _baseDestructor;
    private byte[]? _seekCopy;                                                  // [S+0xC0]

    /// <summary>Creates the source (the C# counterpart of the object <c>0xA562B8</c> builds for mode 3 with plug-in <c>0x40001</c>).</summary>
    public WwiseVorbisInMemorySource(WwisePlayingInstance pbi, WwiseVorbisEngineContext context, Func<bool> tryAlloc, WwisePoolFree? poolFree = null, Action<object>? baseDestructor = null)
    {
        Pbi = pbi ?? throw new ArgumentNullException(nameof(pbi));
        _ctx = context ?? throw new ArgumentNullException(nameof(context));
        _tryAlloc = tryAlloc ?? throw new ArgumentNullException(nameof(tryAlloc));
        _poolFree = poolFree;
        _baseDestructor = baseDestructor;
        LoopCount38 = pbi.LoopCount1B8;                                         // 0xA7329C (the base constructor chain): u16[S+0x38] = u16[pbi+0x1B8]
    }

    /// <summary><c>F = S+0x3C</c>.</summary>
    internal WwiseVorbisFrameBlock Frame { get; } = new();

    /// <inheritdoc />
    public WwisePlayingInstance Pbi { get; }

    /// <inheritdoc />
    public Func<bool> TryAlloc => _tryAlloc;

    /// <summary><c>[S+8]</c>: the <c>akd </c> chunk pointer (V17: borrowed from the media, no copy).</summary>
    public WwiseBytePtr Extra08 { get; private set; }

    /// <summary><c>[S+0x10]</c> (byte): the flags (bit 0 the <c>0xA56650</c> start latch).</summary>
    public byte Flags10 { get; set; }

    /// <inheritdoc />
    public uint TotalSamples14 { get; private set; }

    /// <inheritdoc />
    public uint SampleBase18 { get; set; }

    /// <summary><c>[S+0x1C]</c>: the data size.</summary>
    public uint DataSize1C { get; private set; }

    /// <summary><c>[S+0x20]</c>: the data payload offset.</summary>
    public uint HeaderSize20 { get; private set; }

    /// <inheritdoc />
    public uint Word24 { get; private set; }

    /// <inheritdoc />
    public uint Word28 { get; private set; }

    /// <inheritdoc />
    public WwiseChunkContainer Container2C { get; } = new();

    /// <inheritdoc />
    public ushort LoopCount38 { get; set; }

    /// <summary><c>[S+0x94..0xB9]</c>: the 0x26-byte vorb block (<c>fmt+0x1C</c>).</summary>
    public byte[] VorbBlock { get; } = new byte[0x26];

    /// <summary><c>[S+0xBC]</c>: the sample rate (<c>fmt+4</c>).</summary>
    public uint SampleRateBC { get; private set; }

    /// <summary><c>[S+0x84]</c>: the channel word (<c>fmt+0x14</c>).</summary>
    public uint ChannelWord84 { get => Frame.ChannelConfig; private set => Frame.ChannelConfig = value; }

    /// <summary><c>[S+0x44]</c>: the header state (1 after the seek table copy, 3 running; 4 after the last packet).</summary>
    public int State44 => Frame.State;

    /// <summary><c>[S+0x40]</c>: the status of the last decode.</summary>
    public int Status40 => Frame.Status;

    /// <summary><c>[S+0x88]</c>: the frame count of the last decode.</summary>
    public uint LastFrames88 { get; private set; }

    /// <summary><c>[S+0xCC]</c>: the data base (the media buffer at the payload's offset).</summary>
    public WwiseBytePtr DataBaseCC { get; private set; }

    /// <summary><c>[S+0xC8] - [S+0xCC]</c>: the current packet's offset from the data base.</summary>
    public uint CurrentOffsetC8 { get; private set; }

    /// <summary>False while <c>[S+0xC8]</c> is still the constructor's null (<c>0xAB0D3C</c> is its first store).</summary>
    public bool C8IsSet { get; private set; }

    /// <summary><c>[S+0xC0]</c>: the seek-table copy (null is the zero pointer).</summary>
    internal byte[]? SeekCopyC0 { get => _seekCopy; set => _seekCopy = value; }

    /// <summary><c>[S+0x80]</c>: the output block of the last decode (planar floats).</summary>
    public float[]? OutputBlock80 { get => Frame.Output; internal set => Frame.Output = value; }

    /// <summary><c>[S+0x5C]</c>: the setup (the cache record's value).</summary>
    public WwiseVorbisSetup? Setup5C => Frame.Dsp.Setup;

    /// <summary>The decoder's leading skip still to drop (<c>u16[S+0x4C+0x2C]</c>).</summary>
    public ushort SkipD2C => unchecked((ushort)Frame.Dsp.Skip);

    /// <summary>The decoder's end trim (<c>u16[S+0x4C+0x2E]</c>).</summary>
    public ushort TrimD2E => unchecked((ushort)Frame.Dsp.Trim);

    // the vorb block's fields at their S offsets (S+0x94 + k)
    private uint SeekTableSizeA0 => BitConverter.ToUInt32(VorbBlock, 0xC);      // [S+0xA0]
    private uint DataOffsetA4 => BitConverter.ToUInt32(VorbBlock, 0x10);        // [S+0xA4]
    private ushort MaxPacketA8 => BitConverter.ToUInt16(VorbBlock, 0x14);       // u16[S+0xA8]
    private uint LoopStartPacketOffset94 => BitConverter.ToUInt32(VorbBlock, 0);   // [S+0x94]
    private uint LoopEndPacketOffset98 => BitConverter.ToUInt32(VorbBlock, 4);  // [S+0x98]
    private ushort LoopBeginExtra9C => BitConverter.ToUInt16(VorbBlock, 8);     // u16[S+0x9C]
    private ushort LoopEndExtra9E => BitConverter.ToUInt16(VorbBlock, 0xA);     // u16[S+0x9E]
    private ushort LastGranuleExtraAA => BitConverter.ToUInt16(VorbBlock, 0x16);   // u16[S+0xAA]

    private ushort TrimForCount() => LoopCount38 == 1 ? LastGranuleExtraAA : LoopEndExtra9E;   // 0xAB0DE4..0xAB0DEC, 0xAB03B0, 0xAB03EC

    /// <summary>
    /// <c>vt+0x28 = 0xAB0B20(S, data, size)</c> (V17): <c>data</c> null gives 2. The walker <c>0x9CD340(data, size, ...)</c> (non-1 returned); a format tag other than 0xFFFF gives 7. The PBI's format bytes are written as the streamed
    /// header parse does; a non-empty <c>akd </c> chunk is borrowed into <c>[S+8]</c>. <c>[S+0xCC] = data + [S+0x20]</c>, <c>[S+0x14] = fmt[0x18]</c>, the vorb block (<c>fmt+0x1C</c>, 0x26 bytes) goes to <c>S+0x94</c>, <c>[S+0x84]</c> the
    /// channel word, <c>[S+0xBC] = fmt[4]</c>; <c>[S+0x28] == 0</c> becomes <c>total - 1</c>; <c>total &lt;= [S+0x28]</c> or <c>[S+0x24] &gt; [S+0x28]</c> gives 2, as does <c>[S+0x1C] + [S+0x20] != size</c>. Then <c>[S+0x18] = 0</c>,
    /// <c>[S+0xC8] = [S+0xCC]</c>, <c>u16[S+0x38] = u16[pbi+0x1B8]</c> and <c>0xAB0A30</c> (a non-1 result is returned). With bit 7 of <c>[pbi+0x1BD]</c> set (the start-offset block, <c>0xAB0D7C..0xAB0DD4</c>): no seek-table copy gives status 2,
    /// else <c>0xAB04F0</c>; then bit 7 of <c>[pbi+0x1BD]</c> and bits 0 and 1 of <c>[pbi+0x1BE]</c> are cleared, <c>[pbi+0x1B4]</c> is zeroed and the leftover (the old <c>[pbi+0x1B4]</c> when the lookup cleared bit 7, else 0) is added to
    /// <c>[S+0x18]</c>. Then always <c>0xAB3244(S+0x4C, leftover, loops == 1 ? u16[S+0xAA] : u16[S+0x9E])</c> and <c>[S+0x44] = 3</c>; the status is returned. (The early returns above do not touch the PBI bits.)
    /// </summary>
    public int StartStreamAB0B20(WwiseBytePtr data, uint size)
    {
        if (data.IsNull) return 2;                                              // 0xAB0B20..0xAB0B24, 0xAB0D0C
        var walk = WwiseWaveWalker.Walk9CD340(data, size, Container2C, wantAkd: true, wantSeek: false, _tryAlloc);   // 0xAB0B84 bl 0x9CD340
        if (walk.WroteLoops) { Word24 = walk.Word24; Word28 = walk.Word28; }    // the callee's stores through S+0x24, S+0x28
        if (walk.WroteData) { DataSize1C = walk.DataSize1C; HeaderSize20 = walk.DataOffset20; }   // S+0x1C, S+0x20
        if (walk.Result != 1) return walk.Result;                               // 0xAB0B88..0xAB0B94
        var fmt = walk.Format;                                                  // 0xAB0B98
        if (fmt.U16(0) != 0xFFFF) return 7;                                     // 0xAB0B9C..0xAB0BAC
        var pbi = Pbi;
        uint u32 = fmt.U32(0x14);                                               // 0xAB0BB0
        uint rate = fmt.U32(4);                                                 // 0xAB0BDC
        pbi.SourceFormat158 = rate;                                             // 0xAB0BF0
        pbi.Byte162 = (byte)(((pbi.Byte162 & ~3) | 1) | 4);                     // 0xAB0BE4 bfi r3,r0,#0,#2 (r0 = 1); 0xAB0C08 orr r3,r3,#4
        pbi.Byte161 = (byte)fmt.U16(2);                                         // 0xAB0BB4, 0xAB0BEC, 0xAB0BFC
        pbi.Byte160 = 0x20;                                                     // 0xAB0C1C, 0xAB0C28
        pbi.Word15C = u32;                                                      // 0xAB0C20 (+0x15C), 0xAB0C04 (+0x15D), 0xAB0C18 (+0x15E), 0xAB0C24 (+0x15F): the four bytes of u32
        if (walk.AkdSize != 0) Extra08 = walk.Akd;                              // 0xAB0C3C..0xAB0C48
        DataBaseCC = data.Add(unchecked((int)HeaderSize20));                    // 0xAB0C54..0xAB0C5C [S+0xCC] = data + [S+0x20]
        TotalSamples14 = fmt.U32(0x18);                                         // 0xAB0C60..0xAB0C68
        for (int i = 0; i < VorbBlock.Length; i++) VorbBlock[i] = fmt[0x1C + i];   // 0xAB0C6C..0xAB0CA8
        ChannelWord84 = u32;                                                    // 0xAB0CB0..0xAB0CE0
        if (Word28 == 0) Word28 = unchecked(TotalSamples14 - 1);                // 0xAB0CBC..0xAB0CD4
        SampleRateBC = rate;                                                    // 0xAB0CF0
        if (TotalSamples14 <= Word28 || Word24 > Word28) return 2;              // 0xAB0CD8..0xAB0D08
        if (unchecked(DataSize1C + HeaderSize20) != size) return 2;             // 0xAB0D14..0xAB0D24
        SampleBase18 = 0;                                                       // 0xAB0D34
        CurrentOffsetC8 = 0;                                                    // 0xAB0D3C [S+0xC8] = data base
        C8IsSet = true;
        LoopCount38 = pbi.LoopCount1B8;                                         // 0xAB0D44..0xAB0D48
        int status = StartAB0A30();                                             // 0xAB0D4C bl 0xAB0A30
        if (status != 1) return status;                                         // 0xAB0D50..0xAB0D58
        uint skip = 0;
        if ((pbi.Flags1BD & 0x80) != 0)                                         // 0xAB0D68..0xAB0D78
        {
            if (_seekCopy is null)                                              // 0xAB0D7C..0xAB0D84 -> 0xAB0E04
            {
                status = 2;
            }
            else
            {
                status = SeekAB04F0();                                          // 0xAB0D8C
                skip = (pbi.Flags1BD & 0x80) == 0 ? pbi.Word1B4 : 0;            // 0xAB0D94..0xAB0DA0
            }
            pbi.Flags1BE = (byte)(pbi.Flags1BE & ~3);                           // 0xAB0DA8, 0xAB0DB4, 0xAB0DBC, 0xAB0DC4
            pbi.Word1B4 = 0;                                                    // 0xAB0DB8
            pbi.Flags1BD = (byte)(pbi.Flags1BD & ~0x80);                        // 0xAB0DB0, 0xAB0DC0, 0xAB0DC8
            SampleBase18 = unchecked(SampleBase18 + skip);                      // 0xAB0DCC..0xAB0DD4
        }
        WwiseVorbisDsp.ResetAB3244(Frame.Dsp, unchecked((ushort)skip), TrimForCount());   // 0xAB0DD8..0xAB0DF0
        Frame.State = 3;                                                        // 0xAB0DF4..0xAB0DFC
        return status;                                                          // 0xAB0DF8
    }

    /// <summary>
    /// <c>0xAB0A30(S)</c> (V17): a seek table size <c>[S+0xA0] == 0</c> sets <c>[S+0x44] = 1</c>; else the table's copy is allocated (null: 0x34), <c>[S+0x44] = 1</c>, and the table is copied from <c>[S+0xC8]</c>. The setup packet
    /// (<c>u16 size</c> then the bytes) follows it; <c>[S+0xC8]</c> advances past it; <c>0xAB2D74(*0x108E638, S+0x3C, pbi, {ptr + 2, size})</c> (null: 2) gives <c>[S+0x5C]</c>; <c>0xAB3264(S+0x4C, byte[S+0x84])</c> non-zero gives 2; else
    /// <c>[S+0x44] = 3</c> and 1.
    /// </summary>
    private int StartAB0A30()
    {
        uint tableSize = SeekTableSizeA0;                                       // 0xAB0A30
        uint p = CurrentOffsetC8;
        if (tableSize == 0)
        {
            Frame.State = 1;                                                    // 0xAB0A44..0xAB0A48
        }
        else
        {
            if (!_tryAlloc())                                                   // 0xAB0AD8 bl 0xA7A7F4(pool, [S+0xA0])
            {
                _seekCopy = null;                                               // 0xAB0AE0 str r0,[r4,#0xc0]
                return 0x34;                                                    // 0xAB0AE4
            }
            _seekCopy = new byte[tableSize];
            Frame.State = 1;                                                    // 0xAB0AF4
            Array.Copy(DataBaseCC.Array!, DataBaseCC.Index + (int)p, _seekCopy, 0, tableSize);   // 0xAB0B04 memcpy([S+0xC0], [S+0xC8], [S+0xA0])
            p += tableSize;                                                     // 0xAB0B08..0xAB0B10
        }
        ushort setupSize = DataBaseCC.U16((int)p);                              // 0xAB0A5C ldrh r5,[lr],#2
        CurrentOffsetC8 = p + 2u + setupSize;                                   // 0xAB0A6C..0xAB0A78
        var codebooks = _ctx.Codebooks ?? throw new WwiseMissingBehaviourException(
            "M6-025 V17: the packed codebook library is a host input (the engine's static table behind 0xAB63E0); supply WwiseVorbisEngineContext.Codebooks");
        var setupBytes = new ReadOnlyMemory<byte>(DataBaseCC.Array!, DataBaseCC.Index + (int)p + 2, setupSize);
        _ctx.OnSetupAcquire?.Invoke(setupBytes, 0);
        var setup = _ctx.SetupCache.AcquireAB2D74(                              // 0xAB0A8C bl 0xAB2D74
            BitConverter.ToUInt32(VorbBlock, 0x20), BitConverter.ToUInt32(VorbBlock, 0x18), (byte)ChannelWord84, VorbBlock[0x24], VorbBlock[0x25], setupBytes, codebooks, _tryAlloc);
        if (setup is null) return 2;                                            // 0xAB0A90..0xAB0A94, 0xAB0AC0
        Frame.Dsp.Setup = setup;                                                // 0xAB0A98..0xAB0AA4 [S+0x5C] = [result]
        _ctx.OnDspAllocate?.Invoke((byte)ChannelWord84);
        if (WwiseVorbisDsp.AllocateAB3264(Frame.Dsp, (byte)ChannelWord84, _ctx.Shared, _tryAlloc) != 0) return 2;   // 0xAB0AA8 bl 0xAB3264(S+0x4C, byte[S+0x84]); 0xAB0AAC..0xAB0AC0
        Frame.State = 3;                                                        // 0xAB0AB0..0xAB0AB8
        return 1;                                                               // 0xAB0AB4
    }

    /// <summary>
    /// <c>0xAB04F0(S)</c> (V19, the in-memory seek): <c>pos = 0xA736D4(S)</c>; <c>pos &gt;= [S+0x14]</c> gives 2. <c>pos == 0</c>: offset <c>[S+0xA4]</c>, samples 0. Else with no entries (<c>[S+0xA0] / 4</c>) or no copy 2; the
    /// <c>{u16 samples, u16 bytes}</c> entries are walked until <c>pos &lt; cumulative samples</c>: the first entry gives offset <c>[S+0xA4]</c> and samples 0, a later one <c>bytes before + [S+0xA0]</c> and the samples before it, past the last
    /// the sums plus <c>[S+0xA0]</c>. Then <c>[S+0x18] = samples</c>, <c>[S+0xC8] = [S+0xCC] + offset</c>, <c>[pbi+0x1B4] = pos - samples</c>, bit 7 of <c>[pbi+0x1BD]</c> and bits 0 and 1 of <c>[pbi+0x1BE]</c> cleared; 1.
    /// </summary>
    private int SeekAB04F0()
    {
        uint pos = WwiseSourceStart.StartPositionA736D4(this);                  // 0xAB04F8 bl 0xA736D4
        if (pos >= TotalSamples14) return 2;                                    // 0xAB04FC..0xAB0520
        uint offset, cumSamples = 0;
        if (pos == 0)
        {
            offset = DataOffsetA4;                                              // 0xAB058C
        }
        else
        {
            uint tableSize = SeekTableSizeA0;                                   // 0xAB052C
            uint entries = tableSize >> 2;                                      // 0xAB0534
            if (entries == 0 || _seekCopy is null) return 2;                    // 0xAB0538..0xAB051C
            uint cumBytes = 0;
            uint i = 0;
            bool found = false;
            offset = 0;
            while (true)
            {
                uint after = unchecked(cumSamples + BitConverter.ToUInt16(_seekCopy, (int)(4 * i)));   // 0xAB0558..0xAB055C
                if (pos < after)                                                // 0xAB0560..0xAB0564 blo 0xAB05D4
                {
                    if (i == 0) { offset = DataOffsetA4; cumSamples = 0; }      // 0xAB05D4..0xAB05D8 -> 0xAB058C
                    else offset = unchecked(cumBytes + tableSize);              // 0xAB05DC, 0xAB0584
                    found = true;
                    break;
                }
                i++;                                                            // 0xAB0568
                cumBytes = unchecked(cumBytes + BitConverter.ToUInt16(_seekCopy, (int)(4 * (i - 1) + 2)));   // 0xAB056C, 0xAB0578
                cumSamples = after;                                             // 0xAB057C
                if (entries <= i) break;                                        // 0xAB0570 cmp r5,ip; 0xAB0580 bhi
            }
            if (!found) offset = unchecked(cumBytes + tableSize);               // 0xAB0584
        }
        var pbi = Pbi;
        SampleBase18 = cumSamples;                                              // 0xAB05A4
        CurrentOffsetC8 = offset;                                               // 0xAB05A8, 0xAB05AC
        pbi.Word1B4 = unchecked(pos - cumSamples);                              // 0xAB0598, 0xAB05BC
        pbi.Flags1BE = (byte)(pbi.Flags1BE & ~3);                               // 0xAB05B0, 0xAB05B8, 0xAB05C4, 0xAB05CC
        pbi.Flags1BD = (byte)(pbi.Flags1BD & ~0x80);                            // 0xAB05B4, 0xAB05C0, 0xAB05C8
        return 1;                                                               // 0xAB05A0
    }

    /// <summary>
    /// <c>vt+0x74 = 0xAB0374(S, r1)</c> (V07): decrements <c>u16[S+0x38]</c> when above 1; <c>r1 != 0</c> returns 0x11. Else <c>[S+0xC8] = [S+0xCC] + [S+0x94] + [S+0xA0]</c>, <c>0xAB3244(S+0x4C, u16[S+0x9C], loops == 1 ? u16[S+0xAA] :
    /// u16[S+0x9E])</c>, <c>[S+0x40] = 0x2D</c>, <c>[S+0x44] = 3</c>; returns 0x2D.
    /// </summary>
    public int LoopOrEnd74(int r1)
    {
        if (LoopCount38 > 1) LoopCount38 = unchecked((ushort)(LoopCount38 - 1));   // 0xAB0374..0xAB0384
        if (r1 != 0) return 0x11;                                               // 0xAB0388..0xAB038C, 0xAB03E4
        CurrentOffsetC8 = unchecked(SeekTableSizeA0 + LoopStartPacketOffset94); // 0xAB0390..0xAB03B8
        WwiseVorbisDsp.ResetAB3244(Frame.Dsp, LoopBeginExtra9C, TrimForCount());   // 0xAB03A8, 0xAB03B0, 0xAB03C8 (0xAB03EC for the loop count 1)
        Frame.Status = 0x2D;                                                    // 0xAB03CC..0xAB03D4
        Frame.State = 3;                                                        // 0xAB03D8..0xAB03DC
        return 0x2D;                                                            // 0xAB03D0
    }

    /// <summary>
    /// <c>vt+0x30 = 0xAB0448(S, state)</c> (V10): the bytes available are <c>(loops != 1 ? [S+0xA0] + [S+0x98] : [S+0x1C]) - [S+0xC8]</c> (from the data base), the ready byte is set, <c>0xAB7E40(S+0x3C, u16[S+0xA8], [S+0xC8], S+0x80)</c>
    /// decodes as many packets as it takes to produce output, <c>[state+0x28] = [S+0x40]</c>; status 2 returns; else <c>[S+0x88] = [S+0x3C]</c>, <c>[S+0xC8] += [S+0x48]</c> and <c>0xA73490(S, [S+0x80], u16[S+0x3C], [S+0xBC], [S+0x84], state)</c>.
    /// </summary>
    public void DecodeAB0448(WwiseDecodeState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        uint end = LoopCount38 != 1 ? unchecked(SeekTableSizeA0 + LoopEndPacketOffset98) : DataSize1C;   // 0xAB0450..0xAB0480
        Frame.Avail = unchecked(end - CurrentOffsetC8);                         // 0xAB0488..0xAB048C
        Frame.Ready = 1;                                                        // 0xAB0490..0xAB0494
        WwiseVorbisFraming.FrameLoopAB7E40(Frame, MaxPacketA8, DataBaseCC.Add(unchecked((int)CurrentOffsetC8)), _tryAlloc);   // 0xAB049C bl 0xAB7E40
        state.Code28 = Frame.Status;                                            // 0xAB04A0..0xAB04A8
        if (Frame.Status == 2) return;                                          // 0xAB04A4..0xAB04AC
        LastFrames88 = Frame.Frames;                                            // 0xAB04D8
        CurrentOffsetC8 = unchecked(CurrentOffsetC8 + Frame.Consumed);          // 0xAB04D0, 0xAB04DC
        WwiseSourceOutput.HandoffA73490(this, Frame.Output, (ushort)Frame.Frames, SampleRateBC, ChannelWord84, state);   // 0xAB04E4 bl 0xA73490
    }

    /// <summary><c>vt+0xC = 0xAB032C(S)</c> (V08): a non-null <c>[S+0x80]</c> is freed; <c>[S+0x3C]</c>, <c>[S+0x80]</c> cleared.</summary>
    public void ReleaseOutputAB032C()
    {
        if (Frame.Output is null) return;                                       // 0xAB032C..0xAB0334
        _poolFree?.Invoke("[src+0x80]", 0);                                       // 0xAB034C bl 0xA7A914
        Frame.Frames = 0;                                                       // 0xAB0354
        Frame.Output = null;                                                    // 0xAB0358
    }

    /// <summary>
    /// <c>vt+0x2C = 0xAB0FC0(S)</c> (C34.3 S6): <c>0xAB3428(S+0x4C)</c>, <c>vt+0xC</c>, a non-null <c>[S+0xC0]</c> freed and cleared, then the container (<c>0xA73128</c>).
    /// </summary>
    public void Close2CAB0FC0()
    {
        WwiseVorbisDsp.TeardownAB3428(Frame.Dsp, _ctx.Shared);                  // 0xAB0FCC bl 0xAB3428(S+0x4C)
        ReleaseOutputAB032C();                                                  // 0xAB0FD4 vt+0xC
        if (_seekCopy is not null)                                              // 0xAB0FE0..0xAB1000
        {
            _poolFree?.Invoke("[src+0xC0]", 0);
            _seekCopy = null;
        }
        Container2C.Release9D4B20(_poolFree);                                   // 0xAB100C b 0xA73128
    }

    /// <summary>
    /// <c>vt+0x0 / +0x4</c> (<c>0xAB02E0</c>, <c>0xAB03F4</c>; V22): a non-null <c>[S+0x5C]</c> releases the setup record (<c>0xAB3120(*0x108E638, S+0x3C)</c>); then the base destructor <c>0xA73304</c> (a seam: its body is not read).
    /// </summary>
    public void DestroyAB02E0()
    {
        if (Frame.Dsp.Setup is not null)                                        // 0xAB02E0..0xAB02E8
        {
            _ctx.SetupCache.ReleaseAB3120(BitConverter.ToUInt32(VorbBlock, 0x20));   // 0xAB0310 bl 0xAB3120(table, S+0x3C): the key is [F+0x78] = [S+0xB4]
            Frame.Dsp.Setup = null;
        }
        (_baseDestructor ?? throw new WwiseMissingBehaviourException(
            "M6-025 V22: the base destructor 0xA73304 is named by the verifier but its body is not read; supply the baseDestructor argument"))(this);   // 0xAB0318 bl 0xA73304
    }

    /// <summary><c>vt+0x34 = 0xA72F5C</c> (S1).</summary>
    public float Duration34() => WwiseSourceStart.Duration34A72F5C(this);
}
