// fidelity: M6-025
namespace Cozmo.Robot.Animation.Wwise;

/// <summary>
/// The outputs of the WAVE walker <c>0x9CD340</c> as its callers (<c>0xAB12B4</c>, <c>0xAB0B20</c>, <c>0xA73ABC</c>, <c>0xA75BC4</c>) consume them (research live-bodies-6 V15, with the verifier's edge).
/// The walker writes through its pointers as it goes: the loop words are zeroed on entry (every result but 0x1F), <see cref="DataSize1C"/> and <see cref="DataOffset20"/> only at the data chunk.
/// </summary>
public sealed class WwiseWaveWalk
{
    /// <summary>The result: 1 at the data chunk, 0x1F for null arguments, 7 for a bad header, 8 for a truncated chunk, or the cue allocation's 0x34.</summary>
    public int Result { get; set; } = 1;

    /// <summary>The format chunk payload (<c>[fmtOut+4]</c>): a pointer into the buffer; null when no <c>fmt </c> chunk was seen.</summary>
    public WwiseBytePtr Format { get; set; }

    /// <summary>The format chunk's size (<c>[fmtOut]</c>).</summary>
    public uint FormatSize { get; set; }

    /// <summary>True when the loop words were stored (<c>0</c> on entry, then the sampler loop): every result but 0x1F.</summary>
    public bool WroteLoops { get; set; }

    /// <summary><c>[S+0x24]</c>: the loop start sample.</summary>
    public uint Word24 { get; set; }

    /// <summary><c>[S+0x28]</c>: the loop end sample.</summary>
    public uint Word28 { get; set; }

    /// <summary>True when the data chunk stored its size and offset.</summary>
    public bool WroteData { get; set; }

    /// <summary><c>[S+0x1C]</c>: the data chunk's size.</summary>
    public uint DataSize1C { get; set; }

    /// <summary><c>[S+0x20]</c>: the data chunk's payload offset from the start of the buffer.</summary>
    public uint DataOffset20 { get; set; }

    /// <summary>The <c>akd </c> chunk (<c>{size, ptr}</c>): size 0 and a null pointer when none was seen. A non-empty one makes the callers run <c>0xA7596C</c>.</summary>
    public WwiseBytePtr Akd { get; set; }

    /// <summary>The <c>akd </c> chunk's size.</summary>
    public uint AkdSize { get; set; }

    /// <summary>The <c>seek</c> chunk (<c>{size, ptr}</c>) when the caller asked for it (none of the four callers do).</summary>
    public WwiseBytePtr Seek { get; set; }

    /// <summary>The <c>seek</c> chunk's size.</summary>
    public uint SeekSize { get; set; }
}

/// <summary>
/// The WAVE chunk walker <c>0x9CD340(data, size, &amp;fmt, markers, &amp;loopStart, &amp;loopEnd, &amp;dataSize, &amp;dataOffset, &amp;akd, &amp;seek)</c> (V15). It replaces the test double of batch 5b.
/// </summary>
public static class WwiseWaveWalker
{
    private const uint Riff = 0x46464952, Wave = 0x45564157, Xwma = 0x414D5758;
    private const uint Data = 0x61746164, List = 0x5453494C, Seek = 0x6B656573, Labl = 0x6C62616C, Smpl = 0x6C706D73, Cue = 0x20657563, Fmt = 0x20746D66, Akd = 0x20646B61;

    private static byte At(WwiseBytePtr data, long offset)
    {
        var a = data.Array!;
        long i = data.Index + offset;
        if (i < 0 || i >= a.Length) throw new WwiseMissingBehaviourException("M6-025 V15: the walker reads memory beyond the buffer the C# holds (the engine reads the adjacent bytes)");
        return a[i];
    }

    private static uint U32(WwiseBytePtr data, long offset) => At(data, offset) | ((uint)At(data, offset + 1) << 8) | ((uint)At(data, offset + 2) << 16) | ((uint)At(data, offset + 3) << 24);

    /// <summary>
    /// The walk. <c>data</c> or <c>size</c> null: 0x1F (nothing written). Otherwise the loop words are zeroed; a size below 12, a tag other than <c>RIFF</c> or a form other than <c>WAVE</c> / <c>XWMA</c> gives 7. The chunks
    /// follow from offset 12: fewer than 8 bytes left gives 8; any chunk but <c>data</c> larger than the bytes left gives 8 (a truncated data chunk is allowed). <c>LIST</c> continues 12 bytes in; <c>seek</c> and
    /// <c>akd </c> store <c>{size, ptr}</c>; <c>fmt </c> stores the first one; <c>cue </c> (after <c>fmt </c>, with a marker container, once) allocates the entries (<c>0x9D4AE0</c>, 0x34 on failure) and stores
    /// <c>{dwName, dwPosition, 0}</c> of every 24-byte cue point; <c>labl</c> (after the cue chunk) attaches a label with <c>0x9D4BBC</c>, which is not read; <c>smpl</c> with a non-zero loop count takes the loop
    /// words at <c>payload + 0x24 + u32[payload+0x20]</c> + 8 and + 0xC; <c>data</c> (after <c>fmt </c>) stores its size and offset and returns 1. Other chunks are skipped; an odd chunk skips one pad byte only when it is 0
    /// (then <c>size &lt; offset</c> gives 7).
    /// </summary>
    public static WwiseWaveWalk Walk9CD340(WwiseBytePtr data, uint size, WwiseChunkContainer? markers, bool wantAkd, bool wantSeek, Func<bool> tryAlloc)
    {
        ArgumentNullException.ThrowIfNull(tryAlloc);
        if (size == 0 || data.IsNull) return new WwiseWaveWalk { Result = 0x1F };   // 0x9CD340..0x9CD358
        var w = new WwiseWaveWalk { WroteLoops = true };                        // 0x9CD36C..0x9CD37C
        if (size < 12) { w.Result = 7; return w; }                              // 0x9CD364..0x9CD384
        if (U32(data, 0) != Riff) { w.Result = 7; return w; }                   // 0x9CD390..0x9CD3A0
        uint form = U32(data, 8);                                               // 0x9CD3A4
        if (form != Xwma && form != Wave) { w.Result = 7; return w; }           // 0x9CD3B8..0x9CD3C8
        bool fmtSeen = false, cueLoaded = false;                                // the flag word fp: bit 0, bit 2
        uint p = 12;                                                            // 0x9CD368
        while (true)
        {
            uint remaining = unchecked(size - p);                               // 0x9CD418
            if (remaining < 8) { w.Result = 8; return w; }                      // 0x9CD41C..0x9CD420 -> 0x9CD608
            uint tag = U32(data, p);                                            // 0x9CD424
            uint chunk = U32(data, p + 4);                                      // 0x9CD42C
            uint rest = remaining - 8;                                          // 0x9CD428
            long payload = p + 8L;                                              // 0x9CD430
            if (tag != Data && rest < chunk) { w.Result = 8; return w; }        // 0x9CD434..0x9CD43C cmp r2,r7; cmpne r3,r5; blo 0x9CD608
            if (tag == List) { p += 12; continue; }                             // 0x9CD440..0x9CD444 -> 0x9CD5B4: the body is parsed as chunks
            switch (tag)
            {
                case Seek when wantSeek:                                        // 0x9CD538..0x9CD54C
                    w.Seek = data.Add((int)payload);
                    w.SeekSize = chunk;
                    break;
                case Fmt:                                                       // 0x9CD5BC..0x9CD5D0
                    if (!fmtSeen)
                    {
                        fmtSeen = true;
                        w.Format = data.Add((int)payload);
                        w.FormatSize = chunk;
                    }
                    break;
                case Akd when wantAkd:                                          // 0x9CD4F8..0x9CD504
                    w.Akd = data.Add((int)payload);
                    w.AkdSize = chunk;
                    break;
                case Data:                                                      // 0x9CD508..0x9CD534
                    if (!fmtSeen) { w.Result = 7; return w; }
                    w.WroteData = true;
                    w.DataSize1C = chunk;
                    w.DataOffset20 = (uint)payload;
                    w.Result = 1;
                    return w;
                case Smpl:                                                      // 0x9CD470..0x9CD4A0
                    if (U32(data, p + 0x24) != 0)
                    {
                        long e = payload + 0x24 + U32(data, p + 0x28);
                        w.Word24 = U32(data, e + 8);
                        w.Word28 = U32(data, e + 0xC);
                    }
                    break;
                case Cue:                                                       // 0x9CD5D4..0x9CD6BC
                    if (!fmtSeen) { w.Result = 7; return w; }
                    if (markers is not null && !cueLoaded)
                    {
                        uint count = U32(data, p + 8);                          // 0x9CD5F4
                        if (count == 0) { cueLoaded = true; break; }            // 0x9CD5FC -> 0x9CD600
                        int r = markers.Alloc9D4AE0(count, tryAlloc);           // 0x9CD614
                        if (r != 1) { w.Result = r; return w; }                 // 0x9CD618..0x9CD61C
                        for (uint i = 0; i < markers.Count; i++)                // 0x9CD65C..0x9CD6A8
                        {
                            long cp = p + 0xC + 24L * i;
                            markers.Cues[(int)i] = new WwiseCuePoint(U32(data, cp), U32(data, cp + 4));   // {dwName, dwPosition}; the label word is 0 (0x9CD6A4)
                        }
                        cueLoaded = true;                                       // 0x9CD600
                    }
                    break;
                case Labl:                                                      // 0x9CD550..0x9CD5B0
                    if (markers is not null && cueLoaded && markers.Count != 0)
                    {
                        uint id = U32(data, p + 8);
                        for (int i = 0; i < markers.Cues.Count; i++)
                        {
                            if (markers.Cues[i].Id != id) continue;
                            throw new WwiseMissingBehaviourException(
                                "M6-025 V15: a labl chunk names a cue point and the walker calls 0x9D4BBC(markers, index, text, length) (0x9CD5A0..0x9CD5AC), whose body is not read (C36 still open); a shipped file reaches it: 433319711.wem (Vorbis, stereo 32 kHz: fmt, a cue chunk with one point of id 1, a LIST with a labl of id 1)");
                        }
                    }
                    break;
            }
            long next = payload + chunk;                                        // 0x9CD4A4..0x9CD4A8 add r8,r6,r5
            if ((chunk & 1) != 0 && At(data, next) == 0)                        // 0x9CD4B0..0x9CD4B4 ldrb r3,[r6,r5]; cmp r3,#0
            {
                next++;                                                         // 0x9CD4BC
                if (size < next) { w.Result = 7; return w; }                    // 0x9CD4C0..0x9CD4C8 cmp r4,r8; bhs 0x9CD418; b 0x9CD384
            }
            p = (uint)next;
        }
    }
}
