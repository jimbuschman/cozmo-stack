// fidelity: M6-025
namespace Cozmo.Robot.Animation.Wwise;

// The stream functions of the source classes (M6-wwise-bank.md C33.3; research live-bodies-3 sections 8 and 9 with the verifier's corrections). Every function below was disassembled in
// libcozmoEngine.so (ARM) when this file was written; addresses are in the comments. S is the source object (0xFC bytes for the streamed Vorbis class, vtable 0x103E138); `[S+0xC]` is the PBI.
//
// Production entry. Engine: the voice's AddSrc (0xA55A84 / 0xA558AC) and NotReadyCheck (0xA544BC) call StartStream through 0xA56650 (the latch of [S+0x10] bit 0), which calls the source's
// vt+0x28: 0xAB22D4 for the streamed Vorbis class (vtable 0x103E138), 0xA7538C for the three PCM / ADPCM stream classes (0x103D840, 0x103D8C8, 0x103D950). The C# counterparts are
// WwiseVorbisStreamSource.StartStreamAB22D4 and WwisePcmAdpcmStreamSource.StartStreamA7538C, which WwiseVorbisVoiceSource / WwiseAdpcmVoiceSource.StartStream call, reached through
// WwiseVoiceSourceStart.StartA56650 from WwisePlaybackBridge.AddSrc and WwiseVoiceLinker.NotReadyCheck. The voice pass retry is real: 0xA56650 leaves the latch clear for any result but 1, so the
// next pass calls StartStream again (a 0x3F is the "not ready yet" the pass retries).
//
// Batch 5e (C36): the Vorbis packet decode 0xAB7E40, the output hand-off 0xA73490, the setup cache 0xAB2D74, the DSP allocation 0xAB3264 / 0xAB3428, the walker 0x9CD340, the walker's extra output 0xA7596C, the start
// position 0xA736D4, the Vorbis seek lookup 0xAB1020, the ADPCM / PCM header parses 0xA73ABC / 0xA75BC4, the ADPCM decode 0xA73D34 and the seek lookups 0xA739E8 / 0xA75B1C are built (WwiseVorbisEngine.cs,
// WwiseWaveWalker.cs, this file, WwiseVorbisStreamSource.cs). Still named seams that throw WwiseMissingBehaviourException when unset, never a default: the buffering callback 0xA059D8 and the PCM decode 0xA75E34
// (outline only in C36).

/// <summary>A pointer into a byte array (the engine's <c>[S+0x40]</c>, <c>[S+0xEC]</c>, the format pointer). The null pointer has no array.</summary>
public readonly struct WwiseBytePtr
{
    /// <summary>The array, or null for the null pointer.</summary>
    public byte[]? Array { get; }

    /// <summary>The index of the byte the pointer addresses.</summary>
    public int Index { get; }

    /// <summary>Creates a pointer.</summary>
    public WwiseBytePtr(byte[]? array, int index) { Array = array; Index = index; }

    /// <summary>True for the null pointer.</summary>
    public bool IsNull => Array is null;

    /// <summary><c>ptr + n</c>.</summary>
    public WwiseBytePtr Add(int n) => new(Array, Index + n);

    /// <summary>The byte at <c>ptr + offset</c>.</summary>
    public byte this[int offset] => Array![Index + offset];

    /// <summary><c>ldrh [ptr + offset]</c>.</summary>
    public ushort U16(int offset) => BitConverter.ToUInt16(Array!, Index + offset);

    /// <summary><c>ldr [ptr + offset]</c>.</summary>
    public uint U32(int offset) => BitConverter.ToUInt32(Array!, Index + offset);
}

/// <summary>
/// The fields of the source block <c>[[S+0xC]+0x150]</c> that the stream functions read (D4 <c>0xA74564</c>, F1 <c>0xA7482C</c>, F6 <c>0xA74E00</c>). The offsets are the engine's. How the node's source
/// descriptor (<see cref="WwiseSourceDescriptor"/>, <c>node+0x5C</c>, built by <c>0xA1EA68</c>) maps onto these offsets is not settled by an adopted row, so the block is an explicit input.
/// </summary>
public sealed class WwiseSourceBlock150
{
    /// <summary><c>+4</c>: the source (media) id; -1 with no name and no id returns 2 from CreateAuto's caller.</summary>
    public uint SourceId04 { get; init; }

    /// <summary><c>+0xC</c> (byte): bit 0 language specific; bit 1 the prefetch bit that F1 / F6 test.</summary>
    public byte Bits0C { get; init; }

    /// <summary><c>+0xD</c> (byte): bit 0 company id; bit 1 automatic stream; bit 2 open by name; bit 3 with bit 0 selects cache id -1 (<c>0xA745B8 tst r2,#9</c>).</summary>
    public byte Bits0D { get; init; }

    /// <summary><c>+0x10</c>: the name (the pointer is non-null only for a name-based source).</summary>
    public string? Name10 { get; init; }

    /// <summary><c>+0x14</c>: the plug-in word whose high half is the codec id (<c>0xA745D8 lsr ip,ip,#0x10</c>).</summary>
    public uint Plugin14 { get; init; }
}

/// <summary>The info block <c>0xA059D8</c> receives (<c>sp+4..+0xC</c> of the callback blocks).</summary>
public sealed class WwiseBufferingInfo
{
    /// <summary><c>+0</c>: the estimate, <c>(u32)(float(left + avail) / throughput)</c> (0 for status 2).</summary>
    public uint Estimate { get; init; }

    /// <summary><c>+4</c>: 2 when the status is 2, 0x11 when it is 0x11 or the buffered amount reached the nominal, else 1.</summary>
    public uint Flag { get; init; }
}

/// <summary>
/// What the source stream functions take from the host: the memory the prefetch pointer addresses, the named seams of the bodies no adopted row reads (each unset one throws
/// <see cref="WwiseMissingBehaviourException"/> when reached), the pool-free sink and the Vorbis engine's process-wide state (<see cref="Vorbis"/>).
/// </summary>
public sealed class WwiseStreamSourceSeams
{
    /// <summary>The bank memory that <c>[pbi+0x1DC]</c> addresses (the media table's DATA buffers).</summary>
    public WwiseBankMemory? Memory { get; init; }

    /// <summary>The Vorbis engine's process-wide state: the setup cache <c>*0x108E638</c>, the shared work record <c>0x108E648</c> and the packed codebook library (host input). One instance per engine: sources that share it share the globals.</summary>
    public WwiseVorbisEngineContext Vorbis { get; init; } = new();

    /// <summary><c>0xA059D8(*0x108D8F8, [pbi+0x140], S, &amp;info)</c>: the buffering callback. Not read; reached only when bit 22 of <c>[pbi+4]</c> is set.</summary>
    public Action<uint, WwiseStreamSourceBase, WwiseBufferingInfo>? BufferingCallbackA059D8 { get; init; }

    /// <summary>
    /// The pool free <c>0xA7A988</c> / <c>0xA7A914</c> (C34.3 S10) the close bodies call: the sink receives the field name and the pointer handle (0 for a field the C# holds as an object). The accounting
    /// (<c>used = used - 4 - 0xA7B6C4(ptr)</c>, the heap's free list) is not modelled. Optional: null reports nothing.
    /// </summary>
    public WwisePoolFree? PoolFree { get; init; }

    /// <summary>The pitch node's consumption of the delivered block (<c>0xA52DA8..0xA53050</c>; unread, M6-004): required by <see cref="WwisePitchNodeIntake"/> when a source delivers frames.</summary>
    public WwisePitchNodeConsume? PitchNodeConsumeA52DA8 { get; init; }

    /// <summary>The pitch node's last-buffer path <c>0xA52EBC</c> (the result 0x11; unread): required by <see cref="WwisePitchNodeIntake"/> when a source returns 0x11.</summary>
    public WwisePitchNodeConsume? PitchNodeEndOfStreamA52EBC { get; init; }

    /// <summary>
    /// The base destructors the source destructors end with (<c>0xA75A0C</c> for the streamed Vorbis, ADPCM and PCM classes, <c>0xA73304</c> for the in-memory classes; V22 names them, no adopted row reads their
    /// bodies). Required by the destructors; unset throws.
    /// </summary>
    public Action<object>? BaseDestructor { get; init; }
}

/// <summary>
/// The common part of the source classes' stream state and functions (the base constructor <c>0xA5627C</c>, <c>0xA74504</c>; F1 to F6). Field names keep the engine's offsets. The numeric fields start
/// at 0; the base constructors' stores to them are not adopted (see the file remarks).
/// </summary>
public abstract class WwiseStreamSourceBase : IWwiseSourceCommon
{
    /// <summary>The stream manager (<c>[0x108D798+0x10]</c>).</summary>
    protected WwiseStreamManager Manager { get; }

    /// <summary>The PBI (<c>[S+0xC]</c>).</summary>
    public WwisePlayingInstance Pbi { get; }

    /// <summary>The source block (<c>[[S+0xC]+0x150]</c>).</summary>
    public WwiseSourceBlock150 Src { get; }

    /// <summary>The seams.</summary>
    protected WwiseStreamSourceSeams Seams { get; }

    /// <summary><c>[S+0x10]</c> (byte): bit 0 the start latch (written only by <c>0xA56650</c>), bit 1 the buffering gate. The base constructor <c>0xA5627C</c> clears bits 0 and 1.</summary>
    public byte Flags10 { get; set; }

    /// <summary><c>[S+0x14]</c>: the total sample count.</summary>
    public uint TotalSamples14 { get; set; }

    /// <summary><c>[S+0x18]</c>: the sample base of the seek point.</summary>
    public uint SampleBase18 { get; set; }

    /// <summary><c>[S+0x1C]</c>: the data size.</summary>
    public uint DataSize1C { get; set; }

    /// <summary><c>[S+0x20]</c>: the data payload offset (the header size).</summary>
    public uint HeaderSize20 { get; set; }

    /// <summary><c>[S+0x24]</c>.</summary>
    public uint Word24 { get; set; }

    /// <summary><c>[S+0x28]</c>.</summary>
    public uint Word28 { get; set; }

    /// <summary><c>[S+0x2C]</c>: the marker container's count (<see cref="Container2C"/>).</summary>
    public uint Word2C => Container2C.Count;

    /// <summary>
    /// The chunk container <c>[S+0x2C]</c> (<c>{count, array}</c>, <c>0x9D4B20</c>): the walker <c>0x9CD340</c> fills it (unread, so it is host input) and every close frees it (C34.3 S2). <see cref="Word2C"/> keeps the walker's
    /// first output word as the earlier batch read it.
    /// </summary>
    public WwiseChunkContainer Container2C { get; } = new();

    /// <summary><c>[S+0x38]</c> (u16): the loop count.</summary>
    public ushort LoopCount38 { get; set; }

    /// <summary><c>[S+8]</c>: the <c>akd </c> chunk pointer (<c>0xA7596C</c>; borrowed from the prefetch when bit 4 of <c>[S+0x5E]</c> is set, else an owned copy).</summary>
    public WwiseBytePtr Extra08 { get; set; }

    /// <inheritdoc />
    public Func<bool> TryAlloc => Manager.TryAlloc;

    /// <summary><c>[S+0x3C]</c>: the stream (<c>IAkAutoStream</c>), null before CreateAuto.</summary>
    public WwiseAutoStream? Stream3C;

    /// <summary><c>[S+0x40]</c>: the data pointer.</summary>
    public WwiseBytePtr Data40 { get; set; }

    /// <summary><c>[S+0x44]</c>: the bytes left at <see cref="Data40"/>.</summary>
    public uint Left44 { get; set; }

    /// <summary><c>[S+0x48]</c>: the file offset of <see cref="Data40"/>.</summary>
    public uint FileOffset48 { get; set; }

    /// <summary><c>[S+0x4C]</c>: the real stream offset (the end of the data handed over so far).</summary>
    public uint StreamOffset4C { get; set; }

    /// <summary><c>[S+0x50]</c>: the bytes to skip at the start of the next buffer.</summary>
    public uint Skip50 { get; set; }

    /// <summary><c>[S+0x54]</c>: the loop start byte.</summary>
    public uint LoopStartByte54 { get; set; }

    /// <summary><c>[S+0x58]</c>: the loop end byte.</summary>
    public uint LoopEndByte58 { get; set; }

    /// <summary><c>[S+0x5C]</c> (u16): the loop counter.</summary>
    public ushort LoopCounter5C { get; set; }

    /// <summary><c>[S+0x5E]</c> (byte): bit 0 end reached, bit 1 the current buffer is the prefetch (not owned), bit 2 first buffer initialised, bit 3 the flag of the last <c>0xA746A8</c>, bit 4 prefetch used.</summary>
    public byte Bits5E { get; set; }

    /// <summary>The creator.</summary>
    protected WwiseStreamSourceBase(WwiseStreamManager manager, WwisePlayingInstance pbi, WwiseSourceBlock150 src, WwiseStreamSourceSeams seams)
    {
        Manager = manager ?? throw new ArgumentNullException(nameof(manager));
        Pbi = pbi ?? throw new ArgumentNullException(nameof(pbi));
        Src = src ?? throw new ArgumentNullException(nameof(src));
        Seams = seams ?? throw new ArgumentNullException(nameof(seams));
        // 0xA7329C (the base constructor chain; read for the oracle, not adopted by C33): u16[S+0x38] = u16[pbi+0x1B8] (1 when the PBI is null). The loop count is therefore the PBI's at construction,
        // which 0xAB12B4 reads (0xAB142C) before 0xAB22D4 stores it again.
        LoopCount38 = pbi.LoopCount1B8;
    }

    /// <summary>The class's <c>vt+0x78</c> header parse: <c>(S, data) -&gt; result</c>; 1 is success.</summary>
    protected abstract int ParseHeader(WwiseBytePtr data);

    /// <summary>vt+0x7C, the seek lookup of the class (Vorbis <c>0xAB1020</c>, ADPCM <c>0xA739E8</c>, PCM <c>0xA75B1C</c>): <c>(S, sample) -&gt; (result, sampleBase, byteOffset)</c>.</summary>
    protected abstract (int Result, uint SampleBase, uint ByteOffset) SeekLookup(uint sample);

    /// <summary><c>vt+0x7C(S, sample, &amp;samples, &amp;bytes)</c>: the class's seek lookup (<see cref="SeekLookup"/>) as a public slot.</summary>
    public (int Result, uint SampleBase, uint ByteOffset) VtSeekLookup7C(uint sample) => SeekLookup(sample);

    /// <inheritdoc />
    public abstract int LoopOrEnd74(int r1);

    /// <summary>
    /// The walker <c>0x9CD340(data, length, &amp;fmt, S+0x2C, S+0x24, S+0x28, S+0x1C, S+0x20, &amp;akd, 0)</c> as the four header parses call it: the loop words are stored when the walker wrote them (every result but 0x1F), the data size and
    /// offset only at the data chunk.
    /// </summary>
    protected WwiseWaveWalk RunWalker9CD340(WwiseBytePtr data, uint length)
    {
        var w = WwiseWaveWalker.Walk9CD340(data, length, Container2C, wantAkd: true, wantSeek: false, Manager.TryAlloc);
        if (w.WroteLoops) { Word24 = w.Word24; Word28 = w.Word28; }
        if (w.WroteData) { DataSize1C = w.DataSize1C; HeaderSize20 = w.DataOffset20; }
        return w;
    }

    /// <summary>
    /// <c>0xA7596C(S, &amp;{size, ptr})</c> (V16): with bit 4 of <c>[S+0x5E]</c> (the prefetch is used) <c>[S+8]</c> borrows the pointer and 1 is returned; else <c>size</c> bytes are allocated from the pool (null: <c>[S+8] = 0</c>, 0x34),
    /// copied to <c>[S+8]</c>, and 1 is returned. The callers ignore the result.
    /// </summary>
    protected int WalkerExtraA7596C(WwiseBytePtr ptr, uint size)
    {
        if ((Bits5E & 0x10) != 0)                                               // 0xA7596C..0xA75978
        {
            Extra08 = ptr;                                                      // 0xA75980..0xA75988
            return 1;
        }
        if (!Manager.TryAlloc()) { Extra08 = default; return 0x34; }            // 0xA759A8 bl 0xA7A7F4; 0xA759B0; 0xA759CC
        var copy = new byte[size];
        Array.Copy(ptr.Array!, ptr.Index, copy, 0, size);                       // 0xA759C0 memcpy
        Extra08 = new WwiseBytePtr(copy, 0);
        return 1;
    }

    /// <summary><c>0xA736D4(S)</c>: the start position in samples for a start offset (V20), shared by every class.</summary>
    internal uint StartPositionA736D4() => WwiseSourceStart.StartPositionA736D4(this);

    private WwiseAutoStream Stream => Stream3C ?? throw new InvalidOperationException("M6-025: [S+0x3C] is null (the engine would dereference it)");

    // ---------------------------------------------------------------- C34.3: the close and the duration

    /// <summary>
    /// <c>0xA759D8(S)</c> (S4, S5, S7; also <c>vt+0x2C</c> of the class <c>0x103D8C8</c>): a non-null <c>[S+0x3C]</c> gets its <c>vt+8</c> (IAkAutoStream Destroy, <c>0x9654E4</c>) and is cleared, then the container (<c>0xA73128</c> =
    /// <c>0x9D4B20(S+0x2C)</c>) is released (<c>0xA759D8..0xA75A08</c>).
    /// </summary>
    protected void ReleaseStreamA759D8()
    {
        if (Stream3C is { } stream)                                             // 0xA759E0..0xA759E4
        {
            stream.Destroy9654E4();                                             // 0xA759EC..0xA759F4 vt+8
            Stream3C = null;                                                    // 0xA759F8..0xA759FC
        }
        Container2C.Release9D4B20(Seams.PoolFree);                              // 0xA75A08 b 0xA73128
    }

    /// <summary>
    /// <c>vt+0x34</c> = <c>0xA72F5C(S)</c> (S1): <c>loops = u16[pbi+0x1B8]</c>; 0 returns 0.0f (<c>0xA72F6C..0xA72F80</c>). Otherwise, in single precision, <c>(float)u32[S+0x14] + (float)(loops - 1) * (float)u32([S+0x28] + 1 - [S+0x24])</c> (<c>vmla.f32</c>,
    /// not fused), times <c>1000.0f</c>, divided by <c>(float)u32</c> of <c>vt+0x70(S)</c> = <c>0xA72F50</c> = <c>[pbi+0x158]</c> (<c>0xA72F88..0xA72FE0</c>). A rate of 0 divides to infinity or NaN as the engine does.
    /// </summary>
    public float Duration34A72F5C() => WwiseSourceStart.Duration34A72F5C(this);

    // ---------------------------------------------------------------- D4: 0xA74564

    /// <summary>
    /// D4 <c>0xA74564(S, bufferSettings, minBuffers)</c>: <c>src = [[S+0xC]+0x150]</c>; with <c>[src+0x10] == 0</c> and <c>[src+4] == -1</c> the result is 2. The heuristics are throughput 1.0f
    /// (<c>0x3F800000</c>), loop start 0, loop end 0, <c>minNumBuffers</c> and the priority <c>(int)float[pbi+0x1C0]</c> (a byte). The flags: company id <c>byte[src+0xD] &amp; 1</c>, codec id
    /// <c>[src+0x14] &gt;&gt; 16</c>, no custom parameter, language specific <c>byte[src+0xC] &amp; 1</c>, <c>byte[src+0xD] &gt;&gt; 1 &amp; 1</c> in the third byte, cache id <c>-1</c> when <c>byte[src+0xD] &amp; 9</c> else <c>[src+4]</c>, no
    /// prefetch. <c>byte[src+0xD] &amp; 4</c> opens by name (manager vt+0x18, name <c>[src+0x10]</c>), else by id (vt+0x1C, <c>[src+4]</c>); <c>bSyncOpen</c> is 0; the stream is stored at <c>[S+0x3C]</c>.
    /// </summary>
    internal int CreateStreamA74564(WwiseStreamBufferSettings? bufferSettings, byte minBuffers)
    {
        var src = Src;
        if (src.Name10 is null && src.SourceId04 == 0xFFFFFFFF) return 2;       // 0xA74578..0xA74698
        var heur = new WwiseStreamHeuristics
        {
            Throughput = BitConverter.Int32BitsToSingle(0x3F800000),            // 0xA74588 mov r4,#0x3f800000
            LoopStart = 0, LoopEnd = 0,                                         // 0xA74598, 0xA745A0
            MinNumBuffers = minBuffers,                                         // 0xA7458C strb r2,[sp,#0x20]
            Priority = unchecked((byte)TruncS32(Pbi.Priority1C0)),              // 0xA7459C..0xA745AC vcvt.s32.f32; strb
        };
        var flags = new WwiseFileFlags
        {
            CompanyId = (uint)(src.Bits0D & 1),                                 // 0xA745F4 movne ip,#1 (tst r2,#1)
            CodecId = src.Plugin14 >> 16,                                       // 0xA745D8
            CustomParamSize = 0, CustomParam = 0,                               // 0xA745E8, 0xA745EC
            LanguageSpecific = (byte)(src.Bits0C & 1),                          // 0xA745C8 and r4,r4,#1; strb r4,[sp,#0x34]
            Byte11 = (byte)((src.Bits0D >> 1) & 1),                             // 0xA745D0 ubfx r4,r2,#1,#1; strb r4,[sp,#0x35]
            CacheId = (src.Bits0D & 9) != 0 ? 0xFFFFFFFF : src.SourceId04,      // 0xA745B8..0xA745C0, 0xA74648
            PrefetchBytes = 0,                                                  // 0xA745F0 str ip,[sp,#0x3c]
        };
        if ((src.Bits0D & 4) != 0)                                              // 0xA745FC ands ip,r2,#0xff (r2 = byte & 4)
            return Manager.CreateAutoByName(src.Name10, flags, heur, bufferSettings, ref Stream3C, false);   // 0xA7463C vt+0x18
        return Manager.CreateAutoById(src.SourceId04, flags, heur, bufferSettings, ref Stream3C, false);    // 0xA7467C vt+0x1C
    }

    internal static int TruncS32(float v)
    {
        if (float.IsNaN(v)) return 0;
        if (v >= 2147483648f) return int.MaxValue;
        if (v <= -2147483648f) return int.MinValue;
        return (int)v;
    }

    // ---------------------------------------------------------------- the prefetch pointer

    private WwiseBytePtr ResolvePrefetch(uint address)
    {
        if (address == 0) return default;
        var memory = Seams.Memory ?? throw new WwiseMissingBehaviourException("M6-025 F1: [pbi+0x1DC] is a bank-memory address; supply WwiseStreamSourceSeams.Memory");
        var (block, offset) = memory.Resolve(address);
        return new WwiseBytePtr(block, offset);
    }

    // ---------------------------------------------------------------- F1: 0xA7482C

    /// <summary>
    /// F1 <c>0xA7482C(S, &amp;has)</c>: <c>has = 0</c>; no prefetch (result 1) when bit 1 of <c>byte[src+0xC]</c> is clear or bit 7 of <c>byte[pbi+0x1BD]</c> is set. Otherwise <c>data = [pbi+0x1DC]</c>,
    /// <c>size = [pbi+0x1E0]</c>, <c>[S+0x44] = size</c>, <c>have = data != 0 &amp;&amp; size != 0</c>; bit 1 of <c>[S+0x5E]</c> := have; <c>has = have</c>; not have returns 1. Else bit 4 of <c>[S+0x5E]</c> is set,
    /// <c>vt+0x78(S, data)</c> and <c>0xA746A8(S, data, 1)</c> (a result other than 1 is returned); with <c>u16[S+0x5C] == 0</c> <c>stream vt+0x3C(size, begin, &amp;real)</c> (not 1 returns 2) gives
    /// <c>[S+0x4C] = real</c>, <c>[S+0x50] = size - real</c>; then <c>[S+0x40] += [S+0x20]</c>, <c>[S+0x44] -= [S+0x20]</c>, <c>[S+0x48] += [S+0x20]</c>; 1.
    /// </summary>
    internal int PrefetchStartA7482C(out bool has)
    {
        has = false;                                                            // 0xA74834 strb ip,[r1]
        if ((Src.Bits0C & 2) == 0 || (Pbi.Flags1BD & 0x80) != 0) return 1;      // 0xA74840..0xA74850
        uint dataAddress = Pbi.Word1DC;                                         // 0xA74860
        uint size = Pbi.Word1E0;                                                // 0xA74864
        bool have = dataAddress != 0 && size != 0;                              // 0xA7486C..0xA74878
        Left44 = size;                                                          // 0xA74874 str lr,[r0,#0x44]
        Bits5E = (byte)((Bits5E & ~2) | (have ? 2 : 0));                        // 0xA74884 bfi ip,r2,#1,#1
        has = have;                                                             // 0xA74888 strb r2,[r1]
        if (!have) return 1;                                                    // 0xA74890 beq 0xA74968
        var data = ResolvePrefetch(dataAddress);
        Bits5E |= 0x10;                                                         // 0xA748A4..0xA748A8
        int r = ParseHeader(data);                                              // 0xA748AC vt+0x78
        if (r != 1) return r;                                                   // 0xA748B4..0xA748B8
        r = ProcessBufferA746A8(data, 1);                                       // 0xA748D8 bl 0xA746A8 (r2 = 1)
        if (r != 1) return r;                                                   // 0xA748DC..0xA748E0
        if (LoopCounter5C == 0)                                                 // 0xA748E4..0xA748E8
        {
            uint left = Left44;                                                 // 0xA74928
            int s = Stream.SetPosition9657E0(left, 0, out long real);           // 0xA74944 vt+0x3C
            if (s != 1) return 2;                                               // 0xA74948..0xA74950
            Skip50 = unchecked(left - (uint)real);                              // 0xA7495C
            StreamOffset4C = (uint)real;                                        // 0xA74960
        }
        Data40 = Data40.Add(unchecked((int)HeaderSize20));                      // 0xA74904..0xA7490C
        Left44 = unchecked(Left44 - HeaderSize20);                              // 0xA74908..0xA74914
        FileOffset48 = unchecked(FileOffset48 + HeaderSize20);                  // 0xA74910..0xA74918
        return 1;
    }

    // ---------------------------------------------------------------- F2, F3

    /// <summary>
    /// F2 <c>0xA74970(S)</c>: <c>[S+0x40] = 0</c>; GetHeuristics; <c>heur[0xD] = (int)float[pbi+0x1C0]</c>; SetHeuristics; <c>GetBuffer(&amp;buf, &amp;size, 0)</c>; 0x2D or 0x11: a size of 0 returns 2, else
    /// <c>[S+0x44] = size</c>, <c>0xA746A8(S, buf, 0)</c> and 1 gives <b>0x2D</b>; any other result (including 0x2E) is returned unchanged. The result of <c>0xA746A8</c> when not 1 is returned as it is.
    /// </summary>
    internal int GetBufferWrapperA74970()
    {
        Data40 = default;                                                       // 0xA74988 str r5,[r4,#0x40]
        var h = new WwiseStreamHeuristics();
        Stream.GetHeuristics961664(h);                                          // 0xA74998 vt+0x14
        h.Priority = unchecked((byte)TruncS32(Pbi.Priority1C0));                // 0xA749A8..0xA749B8
        Stream.SetHeuristics964E4C(h);                                          // 0xA749C4 vt+0x18
        int r = Stream.GetBuffer965B1C(out var buf, out int offset, out uint size, wait: false);   // 0xA749E4 vt+0x40 (r3 = 0)
        if (r != 0x2D && r != 0x11) return r;                                   // 0xA749EC..0xA749F4
        if (size == 0) return 2;                                                // 0xA749F8..0xA74A00
        Left44 = size;                                                          // 0xA74A10
        r = ProcessBufferA746A8(new WwiseBytePtr(buf, offset), 0);              // 0xA74A20
        return r == 1 ? 0x2D : r;                                               // 0xA74A24..0xA74A28 moveq r0,#0x2d
    }

    /// <summary>
    /// F3 <c>0xA746A8(S, buf, flag)</c>: <c>[S+0x40] = buf + [S+0x50]</c>; bit 3 of <c>[S+0x5E]</c> := flag; <c>[S+0x4C] = [S+0x44] + [S+0x4C]</c>; <c>[S+0x48] = [S+0x50] + old [S+0x4C]</c>; <c>[S+0x44] -= [S+0x50]</c>.
    /// With the loop count 0: an end offset below <c>[S+0x58]</c> returns 1 with <c>[S+0x50] = 0</c>, else <c>[S+0x44]</c> is trimmed by the excess and the wrap runs. With a loop count: when
    /// <c>count - counter == 1</c> (the last pass) the end of the data is <c>[S+0x1C] + [S+0x20]</c>, before it returns 1 with <c>[S+0x50] = 0</c>, at or past it the buffer is trimmed, bit 0 of <c>[S+0x5E]</c> is set and 1
    /// returned; otherwise past <c>[S+0x58]</c> the wrap runs. The wrap: <c>stream vt+0x3C([S+0x54], begin)</c> not 1 returns 2; <c>u16[S+0x5C]++</c>; <c>[S+0x50] = loopStart - real</c>; <c>[S+0x4C] = real</c>; with one pass
    /// remaining the heuristics' loop end is cleared. Returns 1.
    /// </summary>
    internal int ProcessBufferA746A8(WwiseBytePtr buf, int flag)
    {
        ushort count = LoopCount38;                                             // 0xA746AC
        uint skip = Skip50;                                                     // 0xA746B8
        uint oldStream = StreamOffset4C;                                        // 0xA746C0
        uint left = Left44;                                                     // 0xA746C4
        Data40 = buf.IsNull ? default : buf.Add(unchecked((int)skip));         // 0xA746C8..0xA746D0
        Bits5E = (byte)((Bits5E & ~8) | ((flag & 1) << 3));                     // 0xA746D4 bfi r5,r2,#3,#1
        uint r2 = unchecked(left + oldStream);                                  // 0xA746D8
        StreamOffset4C = r2;                                                    // 0xA746E8
        FileOffset48 = unchecked(skip + oldStream);                             // 0xA746E4, 0xA746EC
        left = unchecked(left - skip);                                          // 0xA746E0
        Left44 = left;                                                          // 0xA746F0
        if (count == 0)                                                         // 0xA746F4 beq 0xA74748
        {
            uint loopEnd = LoopEndByte58;                                       // 0xA74748
            if (r2 < loopEnd) { Skip50 = 0; return 1; }                         // 0xA7474C..0xA74758 -> 0xA74754
            Left44 = unchecked(left - (r2 - loopEnd));                          // 0xA74768..0xA74770
        }
        else
        {
            ushort counter = LoopCounter5C;                                     // 0xA746F8
            if (count - counter == 1)                                           // 0xA746FC..0xA74700 (a 32-bit subtraction of two halfwords)
            {
                uint end = unchecked(DataSize1C + HeaderSize20);                // 0xA74708..0xA74710
                if (r2 < end) { Skip50 = 0; return 1; }                         // 0xA74714..0xA74718 -> 0xA74754
                Left44 = unchecked(left - (r2 - end));                          // 0xA7471C..0xA7472C
                Bits5E |= 1;                                                    // 0xA74734..0xA7473C (count - counter == 1: the beq is not taken only for != 1)
                return 1;
            }
            uint loopEnd = LoopEndByte58;                                       // 0xA7481C
            if (r2 < loopEnd) { Skip50 = 0; return 1; }                         // 0xA74820..0xA74828
            Left44 = unchecked(left - (r2 - loopEnd));                          // 0xA7471C..0xA7472C (the shared tail)
            // 0xA74724 cmp r0,#1: r0 = count - counter here, which is not 1 (the case above took the last pass)
        }
        // 0xA74774: the wrap
        uint loopStart = LoopStartByte54;                                       // 0xA7477C
        int s = Stream.SetPosition9657E0(loopStart, 0, out long real);          // 0xA747A0 vt+0x3C
        if (s != 1) return 2;                                                   // 0xA747A4..0xA747A8
        ushort newCounter = unchecked((ushort)(LoopCounter5C + 1));             // 0xA747B0..0xA747C4
        LoopCounter5C = newCounter;                                             // 0xA747CC strh
        Skip50 = unchecked(loopStart - (uint)real);                             // 0xA747C8, 0xA747D0
        StreamOffset4C = (uint)real;                                            // 0xA747D4
        if (count == 0) return 1;                                               // 0xA747C0 cmp r2,#0; 0xA747D8 beq 0xA74760
        int remaining = count - newCounter;                                     // 0xA747DC rsb r6,r3,r2
        if (remaining != 1) return 1;                                           // 0xA747E0..0xA747E4 bne 0xA74760
        var h = new WwiseStreamHeuristics();
        Stream.GetHeuristics961664(h);                                          // 0xA747F8 vt+0x14
        h.LoopEnd = 0;                                                          // 0xA74800 str r5,[sp,#0x10]
        Stream.SetHeuristics964E4C(h);                                          // 0xA7480C vt+0x18
        return remaining;                                                       // 0xA74814 mov r0,r6 (= 1)
    }

    // ---------------------------------------------------------------- F4, 0xA74A8C

    /// <summary>
    /// <c>0xA74A8C(S)</c>: GetHeuristics, then SetHeuristics with the loop words <c>([S+0x54], [S+0x58])</c> (0 and 0 when <c>u16[S+0x38] == 1</c>); bit 0 of <c>[S+0x5E]</c> is cleared and bit 1 of <c>[S+0x10]</c> becomes
    /// <c>(byte[pbi+0x1BD] &gt;&gt; 6) &amp; 1</c>.
    /// </summary>
    internal void UpdateHeuristicsA74A8C()
    {
        var h = new WwiseStreamHeuristics();
        Stream.GetHeuristics961664(h);                                          // 0xA74AA8 vt+0x14
        if (LoopCount38 != 1) { h.LoopStart = LoopStartByte54; h.LoopEnd = LoopEndByte58; }   // 0xA74AB8..0xA74AC8
        else { h.LoopStart = 0; h.LoopEnd = 0; }
        Stream.SetHeuristics964E4C(h);                                          // 0xA74AD8 vt+0x18
        Bits5E = (byte)(Bits5E & ~1);                                           // 0xA74AE8 bfc r3,#0,#1
        Flags10 = (byte)((Flags10 & ~2) | (((Pbi.Flags1BD >> 6) & 1) << 1));    // 0xA74AF0..0xA74AFC
    }

    /// <summary>
    /// F4 <c>0xA74BA0(S)</c>: <c>pos = 0xA736D4(S)</c> (a seam); <c>pos &gt;= [S+0x14]</c> returns 2; <c>vt+0x7C(S, pos, &amp;[S+0x18], &amp;byteOffset)</c> not 1 returns 2; <c>u16[S+0x5C] = 0</c>;
    /// <c>stream vt+0x3C(byteOffset, begin, &amp;real)</c> not 1 returns 2; <c>[S+0x50] = byteOffset - real</c>, <c>[S+0x4C] = real</c>; <c>0xA74A8C(S)</c>; <c>[pbi+0x1B4] = pos - [S+0x18]</c>; bit 7 of
    /// <c>byte[pbi+0x1BD]</c> and <b>bits 0 and 1</b> of <c>byte[pbi+0x1BE]</c> are cleared (<c>0xA74C60 and r2,r2,#0xfe</c>, <c>0xA74C6C bfi r2,r8,#1,#1</c>); returns 1.
    /// </summary>
    internal int SeekToStartOffsetA74BA0()
    {
        uint pos = StartPositionA736D4();                                       // 0xA74BAC
        if (pos >= TotalSamples14) return 2;                                    // 0xA74BB4..0xA74BBC
        var (r, sampleBase, byteOffset) = SeekLookup(pos);                      // 0xA74BE8 vt+0x7C
        SampleBase18 = sampleBase;                                              // the out pointer is &[S+0x18]
        if (r != 1) return 2;                                                   // 0xA74BEC..0xA74BF4
        LoopCounter5C = 0;                                                      // 0xA74C08
        int s = Stream.SetPosition9657E0(byteOffset, 0, out long real);         // 0xA74C24 vt+0x3C
        if (s != 1) return 2;                                                   // 0xA74C28..0xA74C2C
        Skip50 = unchecked(byteOffset - (uint)real);                            // 0xA74C38..0xA74C3C
        StreamOffset4C = (uint)real;                                            // 0xA74C40
        UpdateHeuristicsA74A8C();                                               // 0xA74C44
        Pbi.Word1B4 = unchecked(pos - SampleBase18);                            // 0xA74C58, 0xA74C64
        Pbi.Flags1BD = (byte)(Pbi.Flags1BD & ~0x80);                            // 0xA74C68 bfi r1,r8,#7,#1
        Pbi.Flags1BE = (byte)(Pbi.Flags1BE & ~3);                               // 0xA74C60, 0xA74C6C: bits 0 and 1
        return 1;
    }

    // ---------------------------------------------------------------- F5: 0xA74C80

    /// <summary>
    /// F5 <c>0xA74C80(S)</c>: bit 1 of <c>[S+0x10]</c> := <c>(byte[pbi+0x1BD] &gt;&gt; 6) &amp; 1</c>; loop: <c>GetBuffer(&amp;buf, &amp;[S+0x44], 0)</c>; 0x2E returns <b>0x3F</b>; a result other than 0x11 and 0x2D returns 2;
    /// <c>vt+0x78(S, buf)</c>: 0x3F loops again, <b>any other result but 1 is returned unchanged</b> (<c>0xA74D20 cmp r0,#1; bne 0xA74D0C</c>); 1 with bit 7 of <c>byte[pbi+0x1BD]</c> set runs <c>0xA74BA0</c> and drops the
    /// buffer (bit 1 of <c>[S+0x5E]</c> set: clear it; else ReleaseBuffer; then <c>[S+0x44] = 0</c>); with it clear <c>GetPosition(NULL)</c>, <c>0xA746A8(S, buf, 0)</c> and the re-alignment
    /// <c>d = [S+0x20] - skip - position</c>: <c>[S+0x44] -= d</c>, <c>[S+0x40] += d</c>, <c>[S+0x48] += d</c>. Bit 2 of <c>[S+0x5E]</c> is set and the result returned.
    /// </summary>
    internal int GenericStartTailA74C80()
    {
        Flags10 = (byte)((Flags10 & ~2) | (((Pbi.Flags1BD >> 6) & 1) << 1));    // 0xA74C8C..0xA74CA8
        while (true)
        {
            int r = Stream.GetBuffer965B1C(out var buf, out int offset, out uint size, wait: false);   // 0xA74CF4 vt+0x40(&buf, S+0x44, 0)
            Left44 = size;                                                      // the size out-pointer is &[S+0x44] (zeroed by GetBuffer first)
            if (r == 0x2E) return 0x3F;                                         // 0xA74CFC..0xA74D08
            if (r != 0x11 && r != 0x2D) return 2;                               // 0xA74CB0..0xA74D14
            var ptr = new WwiseBytePtr(buf, offset);
            int hr = ParseHeader(ptr);                                          // 0xA74CD0 vt+0x78
            if (hr == 0x3F) continue;                                           // 0xA74CD4 cmp r0,#0x3f; beq loop
            if (hr != 1) return hr;                                             // 0xA74D20..0xA74D0C
            int result;
            if ((Pbi.Flags1BD & 0x80) != 0)                                     // 0xA74D28..0xA74D38
            {
                result = SeekToStartOffsetA74BA0();                             // 0xA74D40
                if (Left44 != 0)                                                // 0xA74D44..0xA74D4C
                {
                    if ((Bits5E & 2) != 0) Bits5E = (byte)(Bits5E & ~2);        // 0xA74D54..0xA74D60 (r4 = 0: bfine r3,r4,#1,#1)
                    else Stream.ReleaseBuffer964D64();                          // 0xA74DEC vt+0x44
                    Left44 = 0;                                                 // 0xA74D6C
                }
            }
            else
            {
                ulong position = Stream.GetPosition961870(out _);               // 0xA74D8C vt+0x38 (GetPosition(NULL))
                uint skip = Skip50;                                             // 0xA74DA4
                result = ProcessBufferA746A8(ptr, 0);                           // 0xA74DB0
                uint d = unchecked(HeaderSize20 - skip - (uint)position);       // 0xA74DB4..0xA74DC4
                Left44 = unchecked(Left44 - d);                                 // 0xA74DC8..0xA74DCC
                Data40 = Data40.Add(unchecked((int)d));                         // 0xA74DD4..0xA74DD8
                FileOffset48 = unchecked(FileOffset48 + d);                     // 0xA74DDC..0xA74DE4
            }
            Bits5E |= 4;                                                        // 0xA74D70..0xA74D7C
            return result;
        }
    }

    // ---------------------------------------------------------------- the buffering gate and the callback block

    /// <summary>The stack word the Query out variable holds when the stream is not opened (the seam's).</summary>
    private uint Stale => Manager.IoSeam.StaleQueryWord;

    /// <summary>
    /// The gate shared by F6 (<c>0xA74EC0..0xA74EFC</c>), F7 (<c>0xA75528..</c>, <c>0xA754E8..</c>), G1 (<c>0xAB24EC</c>, <c>0xAB24B4</c>) and G4 (<c>0xAB2BFC</c>): bit 1 of <c>[S+0x10]</c> clear gives 1; otherwise
    /// <c>QueryBufferingStatus</c>: 0x2D / 0x2E give <c>([S+0x44] + avail) &gt;= nominal ? 1 : 0x3F</c> (the left is read before the query), 0x11 gives 1, any other status is the result.
    /// </summary>
    protected int BufferingGate()
    {
        if ((Flags10 & 2) == 0) return 1;                                       // 0xAB2BFC ldrb; tst #2; beq 0xAB2C48
        var stream = Stream;
        uint left = Left44;                                                     // 0xAB2C1C
        uint avail = Stale;                                                     // sp+0x10: stale unless Query writes it
        int status = stream.Query961C1C(ref avail);                             // 0xAB2C28 vt+0x28
        if (status == 0x2D || status == 0x2E)                                   // 0xAB2C30..0xAB2C3C
            return unchecked(left + avail) >= stream.GetNominalBuffering961AD8() ? 1 : 0x3F;   // 0xAB2D2C..0xAB2D4C
        if (status == 0x11) return 1;                                           // 0xAB2C40..0xAB2C48
        return status;                                                          // 0xAB2C34 mov r5,r0
    }

    /// <summary>
    /// The head of the decode slot <c>vt+0x30</c> shared by the streamed Vorbis class (<c>0xAB1550..0xAB15CC</c>, <c>0xAB1A10..0xAB1B0C</c>) and the ADPCM stream class (<c>0xA73D34..0xA73DA8</c>, <c>0xA73F90..0xA7408C</c>; PCM
    /// <c>0xA75E34</c> is outline only): bit 1 of <c>[S+0x10]</c> set: <c>QueryBufferingStatus</c>; 0x2D / 0x2E with <c>[S+0x44] + avail &lt; nominal</c> gives status code <b>0x2E</b> and no decode (with bit 22 of
    /// <c>[pbi+4]</c> set the callback block runs first); at or above the nominal, or the status 0x11, bit 1 of <c>[S+0x10]</c> is cleared for good and the decode runs; any other status is the code and returned. Bit 1 clear:
    /// the decode runs. The callback block runs on every path of bit 22 (with the decode paths' result 0x2D).
    /// </summary>
    protected WwiseDecodeGate DecodeGateShared()
    {
        int r6 = 1;
        if ((Flags10 & 2) != 0)                                                 // 0xAB1550..0xAB1558 / 0xA73D34..0xA73D3C
        {
            var stream = Stream;
            uint left = Left44;                                                 // 0xAB157C
            uint avail = Stale;                                                 // sp+0x18
            int status = stream.Query961C1C(ref avail);                         // 0xAB158C
            r6 = status;                                                        // 0xAB1594
            if (status == 0x2D || status == 0x2E)                               // 0xAB1590..0xAB159C
            {
                if (unchecked(left + avail) < stream.GetNominalBuffering961AD8())   // 0xAB1A10..0xAB1A28
                {
                    r6 = 0x2E;                                                  // 0xAB1A2C movlo r6,#0x2e
                    return CallbackThenResult(r6);                              // 0xAB1A30 blo 0xAB15A8
                }
                return ClearGate();                                             // fall: at or above nominal (0xAB1A34)
            }
            if (status == 0x11) return ClearGate();                             // 0xAB15A0..0xAB15A4 beq 0xAB1A34
            return CallbackThenResult(r6);                                      // 0xAB15A8..0xAB15B8
        }
        if ((Pbi.Flags4 & 0x400000) != 0)                                       // 0xAB15BC..0xAB15C8
        {
            BufferingCallback();                                                // 0xAB1A50..0xAB1B00
            return new WwiseDecodeGate(true, 0x2D);                             // 0xAB1B04 cmp r6,#0x2d; beq 0xAB15CC
        }
        return new WwiseDecodeGate(true, 0x2E);                                 // 0xAB15CC (the result is replaced by the decode)

        WwiseDecodeGate ClearGate()
        {
            Flags10 = (byte)(Flags10 & ~2);                                     // 0xAB1A38..0xAB1A44 bfc r3,#1,#1
            if ((Pbi.Flags4 & 0x400000) == 0) return new WwiseDecodeGate(true, 0x2E);   // 0xAB1A48..0xAB1A4C beq 0xAB15CC
            BufferingCallback();                                                // 0xAB1A50..0xAB1B00 (r6 = 0x2D)
            return new WwiseDecodeGate(true, 0x2D);                             // 0xAB1B04..0xAB1B08 beq 0xAB15CC
        }

        WwiseDecodeGate CallbackThenResult(int result)
        {
            if ((Pbi.Flags4 & 0x400000) != 0) BufferingCallback();              // 0xAB15AC..0xAB15B8 -> 0xAB1A54
            return new WwiseDecodeGate(false, result);                          // 0xAB1B0C str r6,[r7,#0x28]
        }
    }

    /// <summary>
    /// The callback block (F9, <c>0xA74F00..0xA74FC0</c> and its copies): when bit 22 of <c>[pbi+4]</c> is set the status is queried again, an estimate <c>(u32)(float(left + avail) / throughput)</c> (the
    /// division in single precision, <c>vdiv.f32</c>, converted with <c>vcvt.u32.f32</c>) and an info flag are computed, and <c>0xA059D8(*0x108D8F8, [pbi+0x140], S, &amp;info)</c> is called; the result of the caller
    /// is not changed. The body of <c>0xA059D8</c> is not read: an unset seam throws.
    /// </summary>
    protected void BufferingCallback()
    {
        if ((Pbi.Flags4 & 0x400000) == 0) return;                               // 0xA74F00..0xA74F0C
        var stream = Stream;
        uint left = Left44;                                                     // 0xA74F18
        uint avail = Stale;
        int status = stream.Query961C1C(ref avail);                             // 0xA74F28 vt+0x28
        uint estimate = 0;
        uint flag;
        if (status == 2) flag = 2;                                              // 0xA74F34..0xA74F3C
        else
        {
            var h = new WwiseStreamHeuristics();
            stream.GetHeuristics961664(h);                                      // 0xA74F54 vt+0x14
            uint total = unchecked(left + avail);                               // 0xA74F64
            estimate = WwiseAutoStream.CvtU32((float)total / h.Throughput);     // 0xA74F68..0xA74F78
            if (status == 0x11) flag = 0x11;                                    // 0xA74F7C beq 0xA7507C
            else flag = total >= stream.GetNominalBuffering961AD8() ? 0x11u : 1u;   // 0xA74F80..0xA74F9C
        }
        (Seams.BufferingCallbackA059D8 ?? throw new WwiseMissingBehaviourException(
            "M6-025 F9: 0xA059D8 (the buffering callback, called when bit 22 of [pbi+4] is set) is not read; supply WwiseStreamSourceSeams.BufferingCallbackA059D8"))(
            Pbi.PlayingId, this, new WwiseBufferingInfo { Estimate = estimate, Flag = flag });   // 0xA74FBC bl 0xA059D8
    }

    // ---------------------------------------------------------------- F6: 0xA74E00

    /// <summary>
    /// F6 <c>0xA74E00(S)</c> (live caller <c>0xA75610</c> only): the prefetch case (the F1 conditions) runs <c>vt+0x78(S, data)</c> and <c>0xA746A8(S, data, 1)</c> (not 1 is returned), sets the position and skip as F1
    /// does, advances <c>[S+0x40/0x44/0x48]</c> by <c>[S+0x20]</c>, and returns <b>the stream's Start result with no buffering gate</b>. Otherwise Start (not 1 is returned), <c>0xA74C80</c> (not 1 is returned), then the gate
    /// (<see cref="BufferingGate"/>) and the callback block.
    /// </summary>
    internal int BaseStartStreamA74E00()
    {
        if ((Src.Bits0C & 2) != 0 && (Pbi.Flags1BD & 0x80) == 0)                // 0xA74E0C..0xA74E28
        {
            uint dataAddress = Pbi.Word1DC;                                     // 0xA74E2C
            uint size = Pbi.Word1E0;                                            // 0xA74E30
            bool have = dataAddress != 0 && size != 0;                          // 0xA74E38..0xA74E48
            Left44 = size;                                                      // 0xA74E40
            Bits5E = (byte)((Bits5E & ~2) | (have ? 2 : 0));                    // 0xA74E50
            if (have)                                                           // 0xA74E58 beq 0xA74E90
            {
                var data = ResolvePrefetch(dataAddress);
                Bits5E |= 0x10;                                                 // 0xA74E64..0xA74E6C
                int r = ParseHeader(data);                                      // 0xA74E74 vt+0x78
                if (r != 1) return r;                                           // 0xA74E78..0xA74E84
                r = ProcessBufferA746A8(data, 1);                               // 0xA74FC4..0xA74FD0
                if (r != 1) return r;
                if (LoopCounter5C == 0)                                         // 0xA74FE0..0xA74FE8
                {
                    uint left = Left44;                                         // 0xA75034..0xA7503C
                    int s = Stream.SetPosition9657E0(left, 0, out long real);   // 0xA75058 vt+0x3C
                    if (s != 1) return 2;                                       // 0xA7505C..0xA75060
                    Skip50 = unchecked(left - (uint)real);                      // 0xA7506C..0xA75070
                    StreamOffset4C = (uint)real;                                // 0xA75074
                }
                Data40 = Data40.Add(unchecked((int)HeaderSize20));              // 0xA74FEC..0xA7500C
                Left44 = unchecked(Left44 - HeaderSize20);                      // 0xA7500C
                FileOffset48 = unchecked(FileOffset48 + HeaderSize20);          // 0xA75014
                return Stream.Start964CB8();                                    // 0xA75020 blx [vt+0x30]: the result is returned, no gate
            }
        }
        int start = Stream.Start964CB8();                                       // 0xA74E9C vt+0x30
        if (start != 1) return start;
        int tail = GenericStartTailA74C80();                                    // 0xA74EB0
        if (tail != 1) return tail;                                             // 0xA74EB4..0xA74EBC
        int result = BufferingGate();                                           // 0xA74EC0..0xA74EFC
        BufferingCallback();                                                    // 0xA74F00..0xA74FC0
        return result;
    }

    // ---------------------------------------------------------------- the stream's state for tests and the engine's other readers

    /// <summary>The <c>0xA56650</c> latch: bit 0 of <c>[S+0x10]</c> (the voice engine's <c>StartStreamSucceeded</c>).</summary>
    public bool StartLatch
    {
        get => (Flags10 & 1) != 0;
        set => Flags10 = (byte)((Flags10 & ~1) | (value ? 1 : 0));
    }

    /// <summary>The stream (null before CreateAuto).</summary>
    public WwiseAutoStream? Stream3CValue => Stream3C;
}

/// <summary>The three PCM / ADPCM stream classes by vtable.</summary>
public enum WwisePcmAdpcmClass
{
    /// <summary>The class is not named.</summary>
    Unspecified,

    /// <summary>ADPCM streamed, vptr <c>0x103D840</c>, constructor <c>0xA74244</c>.</summary>
    AdpcmStream,

    /// <summary>PCM streamed, vptr <c>0x103D950</c>, constructor <c>0xA76140</c>.</summary>
    PcmStream,

    /// <summary>The third class, vptr <c>0x103D8C8</c> (<c>vt+0x2C = 0xA759D8</c>, no header parse).</summary>
    Class103D8C8,
}

/// <summary>
/// The PCM / ADPCM stream classes (vtables <c>0x103D840</c>, <c>0x103D8C8</c>, <c>0x103D950</c>, all with <c>vt+0x28 = 0xA7538C</c>): F7. The ADPCM class (<c>0x103D840</c>, constructor <c>0xA74244</c>) has the header parse
/// <c>vt+0x78 = 0xA73ABC</c>, the decode <c>vt+0x30 = 0xA73D34</c>, the seek lookup <c>vt+0x7C = 0xA739E8</c>, <c>vt+0xC = 0xA73A14</c> and <c>vt+0x74 = 0xA742C8</c> (C36.2, A01..A05). The PCM class (<c>0x103D950</c>) has the header
/// parse <c>0xA75BC4</c>, the seek lookup <c>0xA75B1C</c> (with <c>vt+0x80 = 0xA75B4C</c>) and <c>vt+0x74 = 0xA742C8</c>; its decode <c>0xA75E34</c> is outline only in C36 and throws. The class <c>0x103D8C8</c> has no header parse.
/// </summary>
public sealed class WwisePcmAdpcmStreamSource : WwiseStreamSourceBase
{
    /// <summary>Creates the source.</summary>
    public WwisePcmAdpcmStreamSource(WwiseStreamManager manager, WwisePlayingInstance pbi, WwiseSourceBlock150 src, WwiseStreamSourceSeams seams)
        : base(manager, pbi, src, seams) { }

    /// <summary>Which of the three vtables the object has (C34.3): the close of each differs. Unspecified is a named stop in <see cref="Close2C"/>.</summary>
    public WwisePcmAdpcmClass Class { get; init; }

    /// <summary><c>[S+0x60]</c> (PCM streamed class): a block the close frees; written by the PCM decode (<c>0xA75E34</c>, outline only), so it is host input.</summary>
    public uint BufferPtr60 { get; set; }

    /// <summary><c>[S+0x64]</c> (PCM streamed class): cleared with <see cref="BufferPtr60"/> by the close; host input.</summary>
    public uint BufferPtr64 { get; set; }

    /// <summary><c>[S+0x60]</c> (ADPCM class): the block alignment (<c>u16[fmt+0xC]</c>, <c>0xA73BB8</c>).</summary>
    public uint BlockAlign60 { get; private set; }

    /// <summary><c>[S+0x64]</c> (ADPCM class): the output block of the last decode (interleaved int16, <c>bytes per frame * u16[0x1052440]</c> bytes); <c>vt+0xC = 0xA73A14</c> frees it.</summary>
    public short[]? Output64 { get; set; }

    /// <summary><c>[S+0x68]</c> (ADPCM class): the partial-block carry buffer (<c>channels * 36</c> bytes, allocated at the first need).</summary>
    public byte[]? Partial68 { get; set; }

    /// <summary><c>u16 [S+0x6C]</c> (ADPCM class): the bytes the carry buffer holds.</summary>
    public ushort PartialBytes6C { get; set; }

    /// <summary>
    /// <c>vt+0x2C</c>: ADPCM streamed <c>0xA7427C</c> (<c>vt+0xC = 0xA73A14</c>: a non-null <c>[S+0x64]</c> is freed and cleared; then <c>0xA759D8</c>, S4); PCM streamed <c>0xA76178</c> (a non-null <c>[S+0x60]</c> is freed and
    /// <c>[S+0x60]</c>, <c>[S+0x64]</c> cleared; then <c>0xA759D8</c>, S5); the class <c>0x103D8C8</c> is <c>0xA759D8</c> itself.
    /// </summary>
    public void Close2C()
    {
        switch (Class)
        {
            case WwisePcmAdpcmClass.AdpcmStream:                                    // vtable 0x103D840
                ReleaseOutputA73A14();                                              // 0xA7427C..0xA74290 vt+0xC
                ReleaseStreamA759D8();                                              // 0xA74298 b 0xA759D8
                break;
            case WwisePcmAdpcmClass.PcmStream:                                      // vtable 0x103D950
                if (BufferPtr60 != 0)                                               // 0xA76178..0xA76184
                {
                    Seams.PoolFree?.Invoke("[S+0x60]", BufferPtr60);                // 0xA76198 bl 0xA7A988
                    BufferPtr60 = 0;                                                // 0xA761A0
                    BufferPtr64 = 0;                                                // 0xA761A4
                }
                ReleaseStreamA759D8();                                              // 0xA761B0 b 0xA759D8
                break;
            case WwisePcmAdpcmClass.Class103D8C8:                                   // vtable 0x103D8C8: vt+0x2C = 0xA759D8
                ReleaseStreamA759D8();
                break;
            default:
                throw new WwiseMissingBehaviourException("M6-025 S4/S5: the PCM / ADPCM stream object's class (its vtable) is not set; its close differs per class");
        }
    }

    /// <summary><c>vt+0xC = 0xA73A14(S)</c> (A04, the ADPCM class): a non-null <c>[S+0x64]</c> is freed (<c>0xA7A914</c>) and cleared.</summary>
    public void ReleaseOutputA73A14()
    {
        if (Output64 is null) return;                                           // 0xA73A14..0xA73A1C
        Seams.PoolFree?.Invoke("[S+0x64]", 0);                                  // 0xA73A34 bl 0xA7A914
        Output64 = null;                                                        // 0xA73A3C
    }

    /// <summary>
    /// <c>vt+0x0 / +0x4</c> of the ADPCM class (<c>0xA73A48</c>, <c>0xA741C8</c>; A04): a non-null <c>[S+0x64]</c> is freed and cleared, a non-null <c>[S+0x68]</c> freed (<c>0xA7A988</c>), then the base destructor <c>0xA75A0C</c> (a seam:
    /// its body is not read).
    /// </summary>
    public void DestroyA741C8()
    {
        ReleaseOutputA73A14();                                                  // 0xA741C8..0xA741FC
        if (Partial68 is not null) Seams.PoolFree?.Invoke("[S+0x68]", 0);       // 0xA74204..0xA7421C
        (Seams.BaseDestructor ?? throw new WwiseMissingBehaviourException(
            "M6-025 A04: the base destructor 0xA75A0C is named by the verifier but its body is not read; supply WwiseStreamSourceSeams.BaseDestructor"))(this);   // 0xA74224 bl 0xA75A0C
    }

    /// <summary>
    /// <c>vt+0x74 = 0xA742C8(S, r1)</c> (A05, the ADPCM and PCM classes): <c>r1 != 0</c> decrements <c>u16[S+0x38]</c> when above 1 and returns 0x11; <c>r1 == 0</c> decrements <c>u16[S+0x5C]</c>, then <c>u16[S+0x38]</c> when above 1,
    /// and returns 0x2D (there is no decoder state to reset).
    /// </summary>
    public override int LoopOrEnd74(int r1)
    {
        ushort loops = LoopCount38;                                             // 0xA742D0
        if (r1 != 0)                                                            // 0xA742C8..0xA742D4
        {
            if (loops > 1) LoopCount38 = unchecked((ushort)(loops - 1));        // 0xA742FC..0xA74304
            return 0x11;                                                        // 0xA74308
        }
        LoopCounter5C = unchecked((ushort)(LoopCounter5C - 1));                 // 0xA742D8..0xA742E8
        if (loops > 1) LoopCount38 = unchecked((ushort)(loops - 1));            // 0xA742DC, 0xA742EC..0xA742F4
        return 0x2D;                                                            // 0xA742E0
    }

    /// <summary>
    /// <c>vt+0x7C</c>, A03: ADPCM <c>0xA739E8(S, pos, &amp;samples, &amp;bytes)</c>: <c>samples = (pos &gt;&gt; 6) &lt;&lt; 6</c>, <c>bytes = [S+0x20] + [S+0x60] * (pos &gt;&gt; 6)</c>, 1. PCM <c>0xA75B1C</c>: <c>samples = pos</c>,
    /// <c>bytes = pos * bpf + [S+0x20]</c> with <c>bpf = vt+0x80 = 0xA75B4C = u16[pbi+0x160] &gt;&gt; 6</c>, 1.
    /// </summary>
    protected override (int Result, uint SampleBase, uint ByteOffset) SeekLookup(uint pos)
    {
        switch (Class)
        {
            case WwisePcmAdpcmClass.AdpcmStream:
                uint blocks = pos >> 6;                                         // 0xA739E8 lsr r1,r1,#6
                return (1, blocks << 6, unchecked(HeaderSize20 + BlockAlign60 * blocks));   // 0xA739F4..0xA73A0C lsl; mla r1,lr,r1,r2
            case WwisePcmAdpcmClass.PcmStream:
                return (1, pos, unchecked(pos * BytesPerFramePcmA75B4C() + HeaderSize20));   // 0xA75B2C..0xA75B44 mla r4,r4,r0,r6
            default:
                throw new WwiseMissingBehaviourException("M6-025 A03: the seek lookup of the class 0x103D8C8 / an unset class is not read");
        }
    }

    /// <summary><c>vt+0x80 = 0xA75B4C(S)</c> (the PCM class): <c>u16[pbi+0x160] &gt;&gt; 6</c>.</summary>
    internal uint BytesPerFramePcmA75B4C() => (uint)((Pbi.Byte160 | (Pbi.Byte161 << 8)) >> 6);

    /// <inheritdoc />
    protected override int ParseHeader(WwiseBytePtr data)
        => Class switch
        {
            WwisePcmAdpcmClass.AdpcmStream => ParseAdpcmA73ABC(data),
            WwisePcmAdpcmClass.PcmStream => ParsePcmA75BC4(data),
            _ => throw new WwiseMissingBehaviourException("M6-025 F8: the class 0x103D8C8 / an unset class has no header parse body read (vt+0x78)"),
        };

    private int HeuristicsAndMinimalBuffer(WwiseStreamHeuristics h, float throughput, uint minimalBufferSize)
    {
        h.Throughput = throughput;                                              // 0xA73CC8 vstr s15,[sp,#0x28]
        if (LoopCount38 != 1) { h.LoopStart = LoopStartByte54; h.LoopEnd = LoopEndByte58; }   // 0xA73CD0..0xA73CE0 vld1.32 d16,[S+0x54]; vst1.32 -> heur+4, heur+8
        h.Priority = unchecked((byte)TruncS32(Pbi.Priority1C0));                // 0xA73CF0..0xA73D00
        var stream = Stream3C ?? throw new InvalidOperationException("M6-025: [S+0x3C] is null (the engine would dereference it)");
        stream.SetHeuristics964E4C(h);                                          // 0xA73D0C vt+0x18
        return stream.SetMinimalBufferSize965244(minimalBufferSize);            // 0xA73D28 vt+0x1C: its result is returned
    }

    /// <summary>
    /// A01 <c>0xA73ABC(S, data)</c> (vt+0x78): the walker with <c>[S+0x44]</c> bytes; a non-1 result is returned; the format tag must be 2 else 7. The PBI: <c>[+0x162] &amp;= 0xF8</c> (int16, interleaved), <c>[+0x158] = rate</c>,
    /// <c>[+0x15C..0x15F]</c> the four bytes of <c>u32[fmt+0x14]</c>, <c>u16[+0x160] = 0x10 | ((channels * 2) &amp; 0x3FF) &lt;&lt; 6</c>; a non-empty <c>akd </c> chunk runs <c>0xA7596C</c>. <c>[S+0x60] = blockAlign</c>, <c>[S+0x14] = ([S+0x1C] * 64) /
    /// blockAlign</c>. With <c>[S+0x28] == 0</c> or the loop count 1: <c>[S+0x54] = [S+0x20]</c>, <c>[S+0x58] = [S+0x20] + [S+0x1C]</c> and <c>[S+0x28] = ([S+0x1C] / blockAlign) * 64 - 1</c>; else <c>[S+0x58] = blockAlign * (([S+0x28] + 1) &gt;&gt; 6) +
    /// [S+0x20]</c>, <c>[S+0x54] = blockAlign * ([S+0x24] &gt;&gt; 6) + [S+0x20]</c> and 7 when the loop end lies below the loop start or the data end lies before either byte offset. Then GetHeuristics; <c>[S+0x28] &gt; [S+0x24]</c> else 2;
    /// <c>[S+0x24] &gt; total</c> or <c>[S+0x28] &gt;= total</c> gives 2. The heuristics get <c>float(rate) * float(blockAlign) / 64000.0f</c>, the loop bytes when the loop count is not 1, the priority; <c>SetMinimalBufferSize(channels * 36)</c> is returned.
    /// </summary>
    private int ParseAdpcmA73ABC(WwiseBytePtr data)
    {
        var walk = RunWalker9CD340(data, Left44);                               // 0xA73B10 bl 0x9CD340(data, [S+0x44], ...)
        if (walk.Result != 1) return walk.Result;                               // 0xA73B14..0xA73B20
        var fmt = walk.Format;                                                  // 0xA73B24
        if (fmt.U16(0) != 2) return 7;                                          // 0xA73B28..0xA73B34
        var pbi = Pbi;
        ushort channels = fmt.U16(2);                                           // 0xA73B48
        uint u32 = fmt.U32(0x14);                                               // 0xA73B4C
        uint rate = fmt.U32(4);                                                 // 0xA73B58
        pbi.Byte162 = (byte)(pbi.Byte162 & 0xF8);                               // 0xA73B5C and r0,r0,#0xfc; 0xA73B64 bfi r0,r5,#2,#1 (r5 = 0)
        ushort bpf = (ushort)((channels << 1) & 0x3FF);                         // 0xA73B54 lsl r3,r3,#1; 0xA73B60 ubfx r3,r3,#0,#0xa
        pbi.SourceFormat158 = rate;                                             // 0xA73B78
        pbi.Word15C = u32;                                                      // 0xA73B88, 0xA73B98, 0xA73B9C, 0xA73BA0
        pbi.Byte161 = (byte)(bpf >> 2);                                         // 0xA73B8C, 0xA73BA4
        pbi.Byte160 = (byte)(0x10 | ((bpf & 3) << 6));                          // 0xA73B94, 0xA73BA8
        if (walk.AkdSize != 0) WalkerExtraA7596C(walk.Akd, walk.AkdSize);       // 0xA73B7C, 0xA73BAC bne 0xA73C80: the result is ignored
        uint blockAlign = fmt.U16(0xC);                                         // 0xA73BB0
        uint dataSize = DataSize1C;                                             // 0xA73BB4
        BlockAlign60 = blockAlign;                                              // 0xA73BB8
        if (blockAlign == 0) throw new WwiseMissingBehaviourException("M6-003 A01: u16[fmt+0xC] == 0 divides by zero in the engine's __aeabi_uidiv (0xA73BC4); the libc behaviour is not in the inventory");
        TotalSamples14 = unchecked((dataSize << 6) / blockAlign);               // 0xA73BC0..0xA73BD8 lsl r0,r6,#6; __aeabi_uidiv
        uint dataStart = HeaderSize20;                                          // 0xA73BCC
        uint dataEnd = unchecked(dataStart + dataSize);                         // 0xA73BD4
        if (Word28 == 0 || LoopCount38 == 1)                                    // 0xA73BDC bne 0xA73C30; 0xA73C34..0xA73C38 beq 0xA73BE0
        {
            LoopStartByte54 = dataStart;                                        // 0xA73BE0
            LoopEndByte58 = dataEnd;                                            // 0xA73BE8
            Word28 = unchecked(((dataSize / blockAlign) << 6) - 1);             // 0xA73BEC..0xA73BFC: stored for [S+0x28] == 0 and for the loop count 1
        }
        else
        {
            uint l0 = Word24, l1 = Word28;                                      // 0xA73C3C, 0xA73BC8
            uint endByte = unchecked(blockAlign * ((l1 + 1) >> 6) + dataStart);   // 0xA73C40..0xA73C4C
            uint startByte = unchecked(blockAlign * (l0 >> 6) + dataStart);     // 0xA73C48..0xA73C50
            LoopEndByte58 = endByte;                                            // 0xA73C5C
            LoopStartByte54 = startByte;                                        // 0xA73C70
            bool bad = l1 < l0 || dataEnd < startByte;                          // 0xA73C54..0xA73C64
            if (dataEnd < endByte) bad = true;                                  // 0xA73C68..0xA73C6C
            if (bad) return 7;                                                  // 0xA73C74..0xA73C78 -> 0xA73B34
        }
        var h = new WwiseStreamHeuristics();
        Stream3C!.GetHeuristics961664(h);                                       // 0xA73C00..0xA73C14 vt+0x14
        if (!(Word28 > Word24)) return 2;                                       // 0xA73C18..0xA73C28 bhi 0xA73C90; mov r0,#2
        if (Word24 > TotalSamples14 || Word28 >= TotalSamples14) return 2;      // 0xA73C90..0xA73C9C
        float throughput = (float)rate * (float)blockAlign / BitConverter.Int32BitsToSingle(0x477A0000);   // 0xA73CA4..0xA73CC8 (64000.0f)
        return HeuristicsAndMinimalBuffer(h, throughput, (uint)channels * 36);  // 0xA73D10..0xA73D28 u16[fmt+2] * 9 << 2
    }

    /// <summary>
    /// A06 <c>0xA75BC4(S, data)</c> (vt+0x78): as A01 with the format tag 0xFFFE else 7; <c>u16[pbi+0x160] = (u16[fmt+0xE] &amp; 0x3F) | ((blockAlign &amp; 3) &lt;&lt; 6)</c>, <c>[pbi+0x161] = (blockAlign &gt;&gt; 2) &amp; 0xFF</c>, <c>[+0x158] = rate</c>,
    /// <c>[+0x15C..0x15F]</c> the bytes of <c>u32[fmt+0x14]</c>, <c>[+0x162] &amp;= 0xF8</c>; <c>[S+0x14] = [S+0x1C] / blockAlign</c>; with <c>[S+0x28] == 0</c> or the loop count 1 <c>[S+0x54] = [S+0x20]</c>, <c>[S+0x58] = data end</c>,
    /// <c>[S+0x28] = total - 1</c>; else the loop bytes are <c>blockAlign * (loopEnd + 1) + [S+0x20]</c> and <c>blockAlign * loopStart + [S+0x20]</c> (no shift) with 7 on the same three tests as A01. There is no <c>[S+0x28] &gt; [S+0x24]</c> or
    /// total check. The heuristics get <c>float(rate * blockAlign) / 1000.0f</c> (an integer product), the loop bytes and the priority; <c>SetMinimalBufferSize(blockAlign)</c> is returned.
    /// </summary>
    private int ParsePcmA75BC4(WwiseBytePtr data)
    {
        var walk = RunWalker9CD340(data, Left44);                               // 0xA75C18 bl 0x9CD340
        if (walk.Result != 1) return walk.Result;                               // 0xA75C1C..0xA75C28
        var fmt = walk.Format;                                                  // 0xA75C2C
        if (fmt.U16(0) != 0xFFFE) return 7;                                     // 0xA75C30..0xA75C44
        var pbi = Pbi;
        uint u32 = fmt.U32(0x14);                                               // 0xA75C50
        uint blockAlign = fmt.U16(0xC);                                         // 0xA75C54
        ushort bits = fmt.U16(0xE);                                             // 0xA75C60
        uint rate = fmt.U32(4);                                                 // 0xA75C6C
        pbi.Byte162 = (byte)(pbi.Byte162 & 0xF8);                               // 0xA75C68, 0xA75C70 (bfi bit 2 <- 0), 0xA75C74
        pbi.Byte160 = (byte)((bits & 0x3F) | ((blockAlign & 3) << 6));          // 0xA75C7C, 0xA75C80, 0xA75C8C
        pbi.Word15C = u32;                                                      // 0xA75C84, 0xA75C98, 0xA75CA0, 0xA75CA8
        pbi.SourceFormat158 = rate;                                             // 0xA75C90
        pbi.Byte161 = (byte)((blockAlign >> 2) & 0xFF);                         // 0xA75CA4, 0xA75CAC
        if (walk.AkdSize != 0) WalkerExtraA7596C(walk.Akd, walk.AkdSize);       // 0xA75C88, 0xA75CB0 bne 0xA75DCC; the result is ignored
        blockAlign = fmt.U16(0xC);                                              // 0xA75DD8 ldrh r8,[r7,#0xc] (the akd path reloads it)
        uint dataSize = DataSize1C;                                             // 0xA75CB4
        if (blockAlign == 0) throw new WwiseMissingBehaviourException("M6-003 A06: u16[fmt+0xC] == 0 divides by zero in the engine's __aeabi_uidiv (0xA75CC0); the libc behaviour is not in the inventory");
        uint total = dataSize / blockAlign;                                     // 0xA75CBC..0xA75CC0
        uint dataStart = HeaderSize20;                                          // 0xA75CC8
        uint dataEnd = unchecked(dataStart + dataSize);                         // 0xA75CD0
        TotalSamples14 = total;                                                 // 0xA75CD4
        if (Word28 == 0 || LoopCount38 == 1)                                    // 0xA75CCC bne 0xA75D84; 0xA75D88..0xA75D8C beq 0xA75CDC
        {
            LoopStartByte54 = dataStart;                                        // 0xA75CE0
            Word28 = unchecked(total - 1);                                      // 0xA75CDC, 0xA75CE4
            LoopEndByte58 = dataEnd;                                            // 0xA75CE8
        }
        else
        {
            uint l0 = Word24, l1 = Word28;                                      // 0xA75D90, 0xA75CC4
            uint endByte = unchecked(blockAlign * (l1 + 1) + dataStart);        // 0xA75D94..0xA75D98
            uint startByte = unchecked(blockAlign * l0 + dataStart);            // 0xA75D9C
            bool bad = dataEnd < endByte || dataEnd < startByte;                // 0xA75DA0..0xA75DB0
            LoopEndByte58 = endByte;                                            // 0xA75DA8
            if (l1 < l0) bad = true;                                            // 0xA75DB4..0xA75DB8
            LoopStartByte54 = startByte;                                        // 0xA75DBC
            if (bad) return 7;                                                  // 0xA75DC0..0xA75DC4 -> 0xA75C40
        }
        var h = new WwiseStreamHeuristics();
        Stream3C!.GetHeuristics961664(h);                                       // 0xA75CEC..0xA75D00 vt+0x14
        float throughput = (float)unchecked(rate * blockAlign) / BitConverter.Int32BitsToSingle(0x447A0000);   // 0xA75D14..0xA75D28 mul; vcvt.f32.u32; vdiv.f32 (1000.0f)
        return HeuristicsAndMinimalBuffer(h, throughput, blockAlign);           // 0xA75D6C..0xA75D7C vt+0x1C(u16[fmt+0xC])
    }

    /// <summary>
    /// A02 <c>0xA73D34(S, state)</c> (vt+0x30, the ADPCM class): the gate of the decode head (<see cref="WwiseStreamSourceBase.DecodeGateShared"/>); with no bytes left <c>0xA74970</c> (not 0x2D is the code and returned). A pool block of
    /// <c>bytesPerFrame * u16[0x1052440]</c> bytes is the output (null: code 2); a pending partial block (<c>u16[S+0x6C]</c> bytes in <c>[S+0x68]</c>) is completed from the new buffer and decoded as one block per channel; then
    /// <c>nb = min(maxFrames / 64, left / blockAlign)</c> blocks per channel through the decoder <c>0xA7A194(src + 36 * channel, dst + 2 * channel bytes, nb, blockAlign, channels)</c>; the bytes remaining below one block are copied to the carry
    /// buffer (<c>channels * 36</c> bytes, allocated on first need; null: code 2) and the buffer is consumed (bit 1 of <c>[S+0x5E]</c> cleared, else ReleaseBuffer); the frames are <c>(64 * bpf * nb + advanced - base) / bpf</c> and
    /// <c>0xA73490(S, [S+0x64], frames, [pbi+0x158], [pbi+0x15C], state)</c> publishes them.
    /// </summary>
    public void DecodeA73D34(WwiseDecodeState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (Class != WwisePcmAdpcmClass.AdpcmStream)
            throw new WwiseMissingBehaviourException("M6-003 A07: the PCM decode 0xA75E34 is outline only in C36 (not built); the class 0x103D8C8 has no decode read");
        var gate = DecodeGateShared();                                          // 0xA73D3C..0xA73DA4, 0xA73F90..0xA7408C
        if (!gate.Decode) { state.Code28 = gate.Result; return; }               // 0xA7408C str r5,[sb,#0x28]
        if (Left44 == 0)                                                        // 0xA73DA8..0xA73DB0
        {
            int r = GetBufferWrapperA74970();                                   // 0xA74098 bl 0xA74970
            if (r != 0x2D) { state.Code28 = r; return; }                        // 0xA740A0..0xA740A4
        }
        var pbi = Pbi;
        int bpf = ((pbi.Byte160 | (pbi.Byte161 << 8)) >> 6) & 0x3FF;            // 0xA73DD0..0xA73DD8 ldrh; ubfx r8,r8,#6,#0xa
        int channels = (byte)pbi.Word15C;                                       // 0xA73DE0 ldrb r5,[fp,#0x15c]
        int maxFrames = WwiseRuntimeSettings.SamplesPerFrame & 0xFFFF;          // 0xA73DE4 ldrh r1,[r6]: u16[0x1052440]
        if (!Manager.TryAlloc())                                                // 0xA73E04 bl 0xA7A894(pool, bpf * maxFrames, 16)
        {
            Output64 = null;                                                    // 0xA73E10 str r0,[r4,#0x64]
            state.Code28 = 2;                                                   // 0xA74184..0xA74188
            return;
        }
        var outBlock = new short[bpf * maxFrames / 2];
        Output64 = outBlock;                                                    // 0xA73E10
        int outPos = 0;                                                         // sl, as a byte offset from the block start
        uint blockAlign = BlockAlign60;                                         // r3
        int framesMax = maxFrames;                                              // [sp+0xc]
        if (PartialBytes6C != 0)                                                // 0xA73E18..0xA73E2C bne 0xA740B4
        {
            int have = PartialBytes6C;                                          // 0xA740B4 r3
            int rest = (int)blockAlign - have;                                  // 0xA740B8..0xA740C4
            Array.Copy(Data40.Array!, Data40.Index, Partial68!, have, rest);    // 0xA740C8 memcpy([S+0x68] + have, [S+0x40], blockAlign - have)
            for (int i = 0; i < channels; i++)                                  // 0xA740E0..0xA74108
                WwiseAdpcm.DecodeBlocksA7A194(Partial68.AsSpan(36 * i), outBlock.AsSpan(outPos / 2 + i), 1, (int)blockAlign, channels);   // 0xA74100 bl 0xA7A194(src + 0x24 * i, dst + 2 * i, 1, blockAlign, channels)
            outPos += 64 * bpf;                                                 // 0xA74128 add sl,sl,r2 ([sp+0x10] = 64 * bpf)
            framesMax = unchecked((ushort)(framesMax - 0x40));                  // 0xA74118..0xA74134 sub r6,r2,#0x40; uxth
            Data40 = Data40.Add(rest);                                          // 0xA74140..0xA74148
            Left44 = unchecked(Left44 - (uint)rest);                            // 0xA74144..0xA74150
            FileOffset48 = unchecked(FileOffset48 + (uint)rest);                // 0xA7414C..0xA74158
            PartialBytes6C = 0;                                                 // 0xA7415C
        }
        if (blockAlign == 0) throw new WwiseMissingBehaviourException("M6-003 A02: the block alignment is 0: the engine divides by it in __aeabi_uidiv (0xA73E4C); the libc behaviour is not in the inventory");
        uint availBlocks = Left44 / blockAlign;                                 // 0xA73E3C..0xA73E4C
        uint nb = Math.Min((uint)(framesMax >> 6), availBlocks);                // 0xA73E58..0xA73E64 lsr r6,r3,#6; cmp; movhs
        for (int i = 0; i < channels; i++)                                      // 0xA73E68..0xA73EAC
        {
            var src = Data40.Array!.AsSpan(Data40.Index + 36 * i);
            WwiseAdpcm.DecodeBlocksA7A194(src, outBlock.AsSpan(outPos / 2 + i), (int)nb, (int)blockAlign, channels);   // 0xA73EA0 bl 0xA7A194(src + 0x24 * i, dst + 2 * i, nb, blockAlign, channels)
        }
        uint frames = unchecked((uint)(64 * bpf * (int)nb + outPos) / (uint)bpf);   // 0xA73EC8..0xA73ED8 mla r0,[sp+0x10],r6,sl; rsb r0,sl0,r0; uidiv by bpf
        uint consumed = nb * blockAlign;                                        // 0xA73EE4 mul r8,r6,r3
        FileOffset48 = unchecked(FileOffset48 + consumed);                      // 0xA73EF0, 0xA73EFC
        uint left = unchecked(Left44 - consumed);                               // 0xA73EEC
        Left44 = left;                                                          // 0xA73F00
        Data40 = Data40.Add((int)consumed);                                     // 0xA73EF8, 0xA73F04
        frames &= 0xFFFF;                                                       // 0xA73F08 uxth r6,r0
        if (left < blockAlign)                                                  // 0xA73EF4 cmp r2,r3; 0xA73F0C bhs 0xA73F64
        {
            if (Partial68 is null)                                              // 0xA73F10..0xA73F18 beq 0xA74190
            {
                if (!Manager.TryAlloc())                                        // 0xA741A0 bl 0xA7A7F4(pool, channels * 36)
                {
                    Partial68 = null;                                           // 0xA741A8 str r0,[r4,#0x68]
                    state.Code28 = 2;                                           // 0xA74184..0xA74188
                    return;
                }
                Partial68 = new byte[channels * 36];
            }
            PartialBytes6C = unchecked((ushort)left);                           // 0xA73F1C strh r2,[r4,#0x6c]
            if (left != 0) Array.Copy(Data40.Array!, Data40.Index, Partial68, 0, (ushort)left);   // 0xA73F24 memcpy([S+0x68], [S+0x40], u16 left)
            uint remaining = Left44;                                            // 0xA73F2C
            Data40 = Data40.Add((int)remaining);                                // 0xA73F40..0xA73F4C
            FileOffset48 = unchecked(FileOffset48 + remaining);                 // 0xA73F48, 0xA73F50
            Left44 = 0;                                                         // 0xA73F44
            if ((Bits5E & 2) != 0) Bits5E = (byte)(Bits5E & ~2);                // 0xA73F38, 0xA73F54, 0xA73F58..0xA73F60
            else Stream3C!.ReleaseBuffer964D64();                               // 0xA7416C..0xA74178 vt+0x44
        }
        WwiseSourceOutput.HandoffA73490(this, Output64, frames, pbi.SourceFormat158, pbi.Word15C, state);   // 0xA73F64..0xA73F84 bl 0xA73490(S, [S+0x64], frames, [pbi+0x158], [pbi+0x15C], state)
    }

    /// <summary>
    /// F7 <c>0xA7538C(S)</c>: the buffer settings are <c>{0, 0x800, 0}</c> (<c>0xA753A0..0xA753B0</c>). Bit 2 of <c>[S+0x5E]</c> clear: with no stream (<c>[S+0x3C] == 0</c>) <c>0xA74564(S, &amp;settings, 0)</c> (not 1 is
    /// returned) and <c>0xA74E00(S)</c> are the result; with a stream <c>0xA74C80(S)</c> (not 1 is returned) and then the gate and the callback block. Bit 2 set: bit 1 of <c>[S+0x10]</c> clear gives 1, set the gate;
    /// then the callback block.
    /// </summary>
    public int StartStreamA7538C()
    {
        var settings = new WwiseStreamBufferSettings { BufferSize = 0, MinBufferSize = 0x800, BlockSize = 0 };   // 0xA753A0..0xA753B0
        int result;
        if ((Bits5E & 4) == 0)                                                  // 0xA75398 tst r1,#4; bne 0xA753E0
        {
            if (Stream3C is null)                                               // 0xA753B8..0xA753C0
            {
                int c = CreateStreamA74564(settings, 0);                        // 0xA75600 bl 0xA74564 (r2 = [S+0x3C] = 0)
                if (c != 1) return c;                                           // 0xA75604..0xA75608
                return BaseStartStreamA74E00();                                 // 0xA75610 bl 0xA74E00
            }
            int tail = GenericStartTailA74C80();                                // 0xA753C4
            if (tail != 1) return tail;                                         // 0xA753C8..0xA753D4
            result = BufferingGate();                                           // 0xA754F4..0xA75670
        }
        else
        {
            result = BufferingGate();                                           // 0xA753E0..0xA75648 (bit 1 clear -> 1)
        }
        BufferingCallback();                                                    // 0xA753F0..0xA754B4 / 0xA75534..0xA755F0
        return result;
    }
}

/// <summary>
/// The outcome of the gate at the head of a decode slot: <see cref="Result"/> is the code stored in <c>[state+0x28]</c> when <see cref="Decode"/> is false; <see cref="Decode"/> true means the decode of this call runs.
/// </summary>
public readonly record struct WwiseDecodeGate(bool Decode, int Result);
