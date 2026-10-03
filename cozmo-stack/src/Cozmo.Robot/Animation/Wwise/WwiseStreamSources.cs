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
// Not adopted by C33 (named seams that throw WwiseMissingBehaviourException when unset, never a default): the WAVE-header walker 0x9CD340, the codebook cache 0xAB2D74, the Vorbis packet decode
// 0xAB7E40 and output hand-off 0xA73490, the seek-position function 0xA736D4 and the seek lookup 0xAB1020, the callback 0xA059D8, the ADPCM header parse 0xA73ABC and decode 0xA73D34, the PCM parse
// 0xA75BC4, the walker's extra output consumer 0xA7596C, and the base constructors' initial values for the fields that no adopted row names (the C# fields start at 0).

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
/// What the source stream functions take from the host: the named seams of the bodies C33 does not adopt (each unset one throws <see cref="WwiseMissingBehaviourException"/> when reached) and the
/// memory the prefetch pointer addresses.
/// </summary>
public sealed class WwiseStreamSourceSeams
{
    /// <summary>The bank memory that <c>[pbi+0x1DC]</c> addresses (the media table's DATA buffers).</summary>
    public WwiseBankMemory? Memory { get; init; }

    /// <summary>
    /// <c>0x9CD340(data, length, &amp;fmtSize, S+0x2C, S+0x24, S+0x28, S+0x1C, S+0x20, &amp;extra, 0)</c>: the WAVE-header walker. Not read by C33. It returns 1, or another result (7 and 8 are named by the
    /// verifier), and fills <see cref="WwiseWaveWalk"/>.
    /// </summary>
    public Func<WwiseBytePtr, uint, WwiseWaveWalk>? WaveWalker9CD340 { get; init; }

    /// <summary><c>0xAB2D74(globalTable, S+0x60, pbi, &amp;{ptr+2, size, flag})</c>: the codebook / setup cache. Returns the record's <c>[0]</c> handle, or null (the caller returns 2). Not read.</summary>
    public Func<WwiseVorbisStreamSource, WwiseBytePtr, ushort, byte, object?>? CodebookCacheAB2D74 { get; init; }

    /// <summary><c>0xA059D8(*0x108D8F8, [pbi+0x140], S, &amp;info)</c>: the buffering callback. Not read; reached only when bit 22 of <c>[pbi+4]</c> is set.</summary>
    public Action<uint, WwiseStreamSourceBase, WwiseBufferingInfo>? BufferingCallbackA059D8 { get; init; }

    /// <summary><c>0xA736D4(S)</c>: the start position in samples for a start offset (reached only when bit 7 of <c>[pbi+0x1BD]</c> is set). Not read.</summary>
    public Func<WwiseStreamSourceBase, uint>? StartPositionA736D4 { get; init; }

    /// <summary>The seek lookup <c>vt+0x7C</c> (Vorbis <c>0xAB1020</c>): <c>(S, sample) -&gt; (result, sampleBase, byteOffset)</c>. Not read.</summary>
    public Func<WwiseStreamSourceBase, uint, (int Result, uint SampleBase, uint ByteOffset)>? SeekLookup7C { get; init; }

    /// <summary><c>0xA7596C(S, extra)</c>: the consumer of the walker's extra output. Not read.</summary>
    public Func<WwiseStreamSourceBase, object, int>? WalkerExtraA7596C { get; init; }

    /// <summary><c>0xAB3264(S+0x70, channels)</c> with <c>channels = byte[S+0xA8]</c>: the output-buffer allocation (memory behaviour only; the allocation sequence is not adopted): true fails it (the caller returns 2). Null never fails.</summary>
    public Func<byte, bool>? OutputBuffersFailAB3264 { get; init; }

    /// <summary><c>0xAB7E40(S+0x60, u16[S+0xCC], S+0xA4)</c>: the Vorbis packet decode of the streamed class's decode loop (0xAB1550). Not read; it sets <c>[S+0x60]</c>, <c>[S+0x64]</c>, <c>[S+0x6C]</c> and <c>[S+0xA4]</c> (the source's FramesDecoded60, Status64, Consumed6C, OutputA4).</summary>
    public Action<WwiseVorbisStreamSource, ushort>? PacketDecodeAB7E40 { get; init; }

    /// <summary><c>0xA73490(S, [S+0xA4], u16[S+0x60], [S+0xE0], [S+0xA8], state)</c>: the output hand-off of the decode loop. Not read; it may store a code at <c>[state+0x28]</c>.</summary>
    public Action<WwiseVorbisStreamSource, WwiseDecodeState>? OutputHandoffA73490 { get; init; }

    /// <summary><c>0xA73ABC</c> (ADPCM) / <c>0xA75BC4</c> (PCM) <c>vt+0x78</c>: the header parse of the PCM / ADPCM stream classes. Not adopted.</summary>
    public Func<WwisePcmAdpcmStreamSource, WwiseBytePtr, int>? ParseHeaderPcmAdpcm { get; init; }
}

/// <summary>The outputs of the WAVE walker <c>0x9CD340</c> as <c>0xAB12B4</c> consumes them.</summary>
public sealed class WwiseWaveWalk
{
    /// <summary>The result (1 is success; the verifier names 7 and 8).</summary>
    public int Result { get; init; } = 1;

    /// <summary>The format chunk payload (<c>[sp+0x1C]</c>): a pointer into the buffer.</summary>
    public WwiseBytePtr Format { get; init; }

    /// <summary><c>[S+0x2C]</c>.</summary>
    public uint Word2C { get; init; }

    /// <summary><c>[S+0x24]</c>.</summary>
    public uint Word24 { get; init; }

    /// <summary><c>[S+0x28]</c>.</summary>
    public uint Word28 { get; init; }

    /// <summary><c>[S+0x1C]</c>: the data size.</summary>
    public uint DataSize1C { get; init; }

    /// <summary><c>[S+0x20]</c>: the data payload offset.</summary>
    public uint DataOffset20 { get; init; }

    /// <summary><c>[sp+0x20]</c>: non-null makes <c>0xAB12B4</c> call <c>0xA7596C</c>.</summary>
    public object? Extra { get; init; }
}

/// <summary>
/// The common part of the source classes' stream state and functions (the base constructor <c>0xA5627C</c>, <c>0xA74504</c>; F1 to F6). Field names keep the engine's offsets. The numeric fields start
/// at 0; the base constructors' stores to them are not adopted (see the file remarks).
/// </summary>
public abstract class WwiseStreamSourceBase
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

    /// <summary><c>[S+0x2C]</c>.</summary>
    public uint Word2C { get; set; }

    /// <summary><c>[S+0x38]</c> (u16): the loop count.</summary>
    public ushort LoopCount38 { get; set; }

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

    /// <summary>vt+0x7C, the seek lookup of the class (<c>0xAB1020</c> for Vorbis).</summary>
    protected virtual (int Result, uint SampleBase, uint ByteOffset) SeekLookup(uint sample)
        => (Seams.SeekLookup7C ?? throw new WwiseMissingBehaviourException(
            "M6-025 F4: the seek lookup vt+0x7C (Vorbis 0xAB1020) is not read; supply WwiseStreamSourceSeams.SeekLookup7C"))(this, sample);

    private WwiseAutoStream Stream => Stream3C ?? throw new InvalidOperationException("M6-025: [S+0x3C] is null (the engine would dereference it)");

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
        uint pos = (Seams.StartPositionA736D4 ?? throw new WwiseMissingBehaviourException(
            "M6-025 F4: 0xA736D4 (the start position for a start offset) is not read; supply WwiseStreamSourceSeams.StartPositionA736D4"))(this);   // 0xA74BAC
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

/// <summary>
/// The PCM / ADPCM stream classes (vtables <c>0x103D840</c>, <c>0x103D8C8</c>, <c>0x103D950</c>, all with <c>vt+0x28 = 0xA7538C</c>): F7. The header parse (<c>vt+0x78</c>: <c>0xA73ABC</c> for the ADPCM class,
/// <c>0xA75BC4</c> for the PCM class, none for <c>0x103D8C8</c>) is not adopted and comes from <see cref="WwiseStreamSourceSeams.ParseHeaderPcmAdpcm"/>.
/// </summary>
public sealed class WwisePcmAdpcmStreamSource : WwiseStreamSourceBase
{
    /// <summary>Creates the source.</summary>
    public WwisePcmAdpcmStreamSource(WwiseStreamManager manager, WwisePlayingInstance pbi, WwiseSourceBlock150 src, WwiseStreamSourceSeams seams)
        : base(manager, pbi, src, seams) { }

    /// <inheritdoc />
    protected override int ParseHeader(WwiseBytePtr data)
        => (Seams.ParseHeaderPcmAdpcm ?? throw new WwiseMissingBehaviourException(
            "M6-025 F8: the PCM / ADPCM header parse (vt+0x78: 0xA73ABC, 0xA75BC4) is not adopted; supply WwiseStreamSourceSeams.ParseHeaderPcmAdpcm"))(this, data);

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
