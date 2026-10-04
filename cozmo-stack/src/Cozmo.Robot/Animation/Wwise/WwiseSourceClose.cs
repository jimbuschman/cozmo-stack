// fidelity: M6-025, M6-022
namespace Cozmo.Robot.Animation.Wwise;

// The source classes' close (vt+0x2C) and duration (vt+0x34) slots (M6-wwise-bank.md C34.3, rows S1..S7 and S10 of research/20261003-B-M6b-4-live-bodies-4.md with its verification).
// Every function was disassembled in libcozmoEngine.so (ARM) when this file was written; the slots were read from the six vtables (PCM in-memory 0x103D740, PCM streamed 0x103D950, ADPCM in-memory 0x103D6C0,
// ADPCM streamed 0x103D840, Vorbis in-memory 0x103E0B8, Vorbis streamed 0x103E138; the streamed class 0x103D8C8 shares 0xA759D8).
//
// Production entry. Engine: the voice's source close 0xA56414 (src vt+0x2C at 0xA5644C; called from AddSrc's destroy path 0xA55964 and the voice Term 0xA53EA8) and the start notification 0xA56478 (src vt+0x34 at
// 0xA56598). The C# counterparts are WwisePlaybackBridge.CloseSourceA56414 (-> IWwiseVoiceSource.Close2C) and WwisePlaybackBridge.StartNotificationA56478 (-> IWwiseVoiceSource.Duration34), reached through the
// bridge's AddSrc and the linker's TermVoice seams.
//
// Replaces: the test-double seams WwisePlaybackBridge.SourceClose2C and SourceDuration34.
//
// What the C# does not hold: the pool the frees return memory to (0xA7A988 / 0xA7A914, S10: used = used - 4 - 0xA7B6C4(ptr); the heap internals are unread). The close bodies report each free through the
// WwisePoolFree sink (field name, pointer) so the order and the set of frees are the engine's; the accounting itself is not modelled. The in-memory classes' state is written by their StartStream bodies
// (0xA72A2C, 0xAB0B20, 0xAB0448 family), which are unread: WwiseInMemorySourceFields carries it as host input.
// Unread, named: the Vorbis DSP teardown 0xAB3428 (called first by both Vorbis closes).

/// <summary>The pool free sink: <paramref name="field"/> names the engine field whose block is freed; <paramref name="pointer"/> is the handle of a raw-pointer field (0 for a field the C# holds as an object).</summary>
public delegate void WwisePoolFree(string field, uint pointer);

/// <summary>
/// The chunk container at <c>[src+0x2C]</c> (<c>0x9D4B20</c>, S2): <c>[c+0]</c> the element count, <c>[c+4]</c> the array of 12-byte elements, whose word at <c>+8</c> is a block the container owns. The walker <c>0x9CD340</c> fills it.
/// </summary>
public sealed class WwiseChunkContainer
{
    /// <summary><c>[c+0]</c>.</summary>
    public uint Count { get; set; }

    /// <summary><c>[c+4]</c>: the array's block (0 for the null pointer).</summary>
    public uint ArrayPtr { get; set; }

    /// <summary>The word at <c>+8</c> of each element the array holds (0 for none): the label pointer of a cue entry.</summary>
    public List<uint> ElementPtrs8 { get; } = new();

    /// <summary>The first two words of each element (the cue entries <c>{dwName, dwPosition}</c> the walker <c>0x9CD340</c> stores; V15).</summary>
    public List<WwiseCuePoint> Cues { get; } = new();

    /// <summary>
    /// <c>0x9D4AE0(c, n)</c>: <c>[c+0] = n</c>; <c>n * 12</c> bytes from the pool become the array (<c>[c+4]</c>); a null allocation clears <c>[c+0]</c> and returns 0x34, else 1. The array's words are not written here:
    /// the walker stores the cue entries. The block's address is not modelled, so a non-null <c>[c+4]</c> is the synthetic handle 1.
    /// </summary>
    public int Alloc9D4AE0(uint n, Func<bool> tryAlloc)
    {
        ArgumentNullException.ThrowIfNull(tryAlloc);
        Count = n;                                                                      // 0x9D4AF4 str r1,[r0]
        if (!tryAlloc())                                                                // 0x9D4B00 bl 0xA7A7F4(pool, n * 12)
        {
            ArrayPtr = 0;                                                               // 0x9D4B08
            Count = 0;                                                                  // 0x9D4B0C
            Cues.Clear();
            ElementPtrs8.Clear();
            return 0x34;                                                                // 0x9D4B10
        }
        ArrayPtr = 1;
        Cues.Clear();
        ElementPtrs8.Clear();
        for (uint i = 0; i < n; i++) { Cues.Add(default); ElementPtrs8.Add(0); }
        return 1;                                                                       // 0x9D4B14
    }

    /// <summary>
    /// <c>0x9D4DD0(c, pos)</c>: null (-1) with no entries; else the index of the entry with the smallest <c>abs(position - pos)</c> (the signed difference, compared unsigned), the first on a tie.
    /// </summary>
    public int Nearest9D4DD0(uint pos)
    {
        if (Count == 0) return -1;                                                      // 0x9D4DD4..0x9D4DDC
        int best = 0;
        uint bestDiff = Abs(Cues[0].Position, pos);                                     // 0x9D4DF0..0x9D4DFC
        for (int i = 1; i < Count; i++)                                                 // 0x9D4E04..0x9D4E28
        {
            uint diff = Abs(Cues[i].Position, pos);
            if (bestDiff > diff) { best = i; bestDiff = diff; }                         // 0x9D4E14 cmp ip,r3; 0x9D4E18 movhi
        }
        return best;

        static uint Abs(uint position, uint p)
        {
            int d = unchecked((int)(position - p));                                     // rsb ip,r1,ip; cmp ip,#0; rsblt ip,ip,#0
            return unchecked((uint)(d < 0 ? -d : d));
        }
    }

    /// <summary>
    /// <c>0x9D4C24(c, pbi, state, pos)</c>, the marker window: with no entry array, or bit 2 of <c>[pbi+4]</c> clear, nothing is written. Else <c>[state+0x14] = 0</c>, <c>u16[state+0x10] = 0</c>; with no entries it
    /// returns; the entries with <c>pos &lt;= position &lt; pos + u16[state+0xE]</c> (unsigned) are counted into <c>u16[state+0x10]</c>; none returns; otherwise <c>count * 20</c> bytes from the pool (null: pointer and count
    /// cleared) take <c>{pbi, position - pos, id, position, label}</c> per entry in range.
    /// </summary>
    internal void BuildWindow9D4C24(WwisePlayingInstance pbi, WwiseDecodeState state, uint pos, Func<bool> tryAlloc)
    {
        if (ArrayPtr == 0) return;                                                      // 0x9D4C24..0x9D4C2C
        if ((pbi.Flags4 & 4) == 0) return;                                              // 0x9D4C34..0x9D4C3C
        uint frames = state.ValidFrames;                                                // 0x9D4C48 ldrh r6,[r2,#0xe]
        state.Markers = null;                                                           // 0x9D4C50
        state.MarkerCount = 0;                                                          // 0x9D4C54
        if (Count == 0) return;                                                         // 0x9D4C4C, 0x9D4C58
        uint end = unchecked(pos + frames);                                             // 0x9D4C60
        ushort count = 0;
        for (int i = 0; i < Count; i++)                                                 // 0x9D4C68..0x9D4C8C
        {
            uint position = Cues[i].Position;
            if (position >= pos && position < end) { count = unchecked((ushort)(count + 1)); state.MarkerCount = count; }   // 0x9D4C70..0x9D4C84
        }
        if (count == 0) return;                                                         // 0x9D4C90..0x9D4C94
        if (!tryAlloc())                                                                // 0x9D4CBC bl 0xA7A7F4(pool, count * 20)
        {
            state.Markers = null;                                                       // 0x9D4CC8
            state.MarkerCount = 0;                                                      // 0x9D4CCC
            return;
        }
        var list = new List<WwiseMarkerWindowEntry>();
        for (int i = 0; i < Count; i++)                                                 // 0x9D4CF0..0x9D4D24
        {
            var cue = Cues[i];
            if (pos > cue.Position) continue;                                           // 0x9D4CF4..0x9D4CF8 cmp r5,ip; bhi 0x9D4D1C
            if (cue.Position < unchecked(pos + frames))                                 // 0x9D4CFC..0x9D4D00 cmp ip,r6; ldmlo
                list.Add(new WwiseMarkerWindowEntry(pbi, cue.Position - pos, cue.Id, cue.Position, ElementPtrs8[i]));   // 0x9D4D0C..0x9D4D18
        }
        state.Markers = list.ToArray();                                                 // 0x9D4CC8 str r0,[sl,#0x14]
    }

    /// <summary>
    /// <c>0x9D4B20(c)</c>: a null array only clears the count (<c>0x9D4B28..0x9D4B2C</c> beq <c>0x9D4B9C</c>). Otherwise for each of <c>count</c> elements a non-null <c>[elem+8]</c> is freed and cleared (<c>0x9D4B54..0x9D4B84</c>; the count is
    /// re-read after each free), then the array is freed and <c>[c+4]</c> and <c>[c]</c> cleared (<c>0x9D4B88..0x9D4BA0</c>; a count of 0 goes straight to the array free, <c>0x9D4BA8</c>).
    /// </summary>
    public void Release9D4B20(WwisePoolFree? free)
    {
        if (ArrayPtr == 0) { Count = 0; return; }                                       // 0x9D4B9C
        if (ElementPtrs8.Count < Count) throw new InvalidOperationException("the container's count exceeds the elements its array holds");
        for (int i = 0; i < Count; i++)                                                 // 0x9D4B54..0x9D4B84
        {
            if (ElementPtrs8[i] == 0) continue;                                         // 0x9D4B60..0x9D4B64
            free?.Invoke($"[c+4][{i}]+8", ElementPtrs8[i]);                             // 0x9D4B6C bl 0xA7A988
            ElementPtrs8[i] = 0;                                                        // 0x9D4B78
        }
        free?.Invoke("[c+4]", ArrayPtr);                                                // 0x9D4B90
        ArrayPtr = 0;                                                                   // 0x9D4B98
        Count = 0;                                                                      // 0x9D4BA0
    }
}

/// <summary>A cue point entry of the marker container: the first two words of its 12-byte element.</summary>
public readonly record struct WwiseCuePoint(uint Id, uint Position);

/// <summary>Which in-memory source class a <see cref="WwiseInMemorySourceFields"/> belongs to.</summary>
public enum WwiseInMemoryKind
{
    /// <summary>PCM in-memory, vptr <c>0x103D740</c>: <c>vt+0x2C = 0xA73128</c>.</summary>
    Pcm,

    /// <summary>ADPCM in-memory, vptr <c>0x103D6C0</c>: <c>vt+0x2C = 0xA72AF4</c>.</summary>
    Adpcm,

}

/// <summary>
/// The fields the in-memory ADPCM and PCM classes' closes read (S2, S3). They are written by their StartStream bodies, which are unread, so they are host input: the chunk container and <c>[src+0x44]</c> (ADPCM decode
/// buffer). The in-memory Vorbis class is <see cref="WwiseVorbisInMemorySource"/> (batch 5e), which owns its state.
/// </summary>
public sealed class WwiseInMemorySourceFields
{
    /// <param name="kind">The class.</param>
    public WwiseInMemorySourceFields(WwiseInMemoryKind kind) => Kind = kind;

    /// <summary>The class.</summary>
    public WwiseInMemoryKind Kind { get; }

    /// <summary><c>[src+0x2C]</c>.</summary>
    public WwiseChunkContainer Container2C { get; } = new();

    /// <summary><c>[src+0x44]</c> (ADPCM): a block the close frees (<c>0xA72520</c>).</summary>
    public uint Ptr44 { get; set; }

    /// <summary>
    /// <c>vt+0x2C</c>: PCM <c>0xA73128</c> (the container, S2); ADPCM <c>0xA72AF4</c> (<c>vt+0xC = 0xA72520</c>: a non-null <c>[src+0x44]</c> is freed and cleared, then the container, S3).
    /// </summary>
    public void Close2C(WwisePoolFree? free)
    {
        switch (Kind)
        {
            case WwiseInMemoryKind.Pcm:
                Container2C.Release9D4B20(free);                                        // 0xA73128 add r0,r0,#0x2c; b 0x9D4B20
                break;
            case WwiseInMemoryKind.Adpcm:
                if (Ptr44 != 0) { free?.Invoke("[src+0x44]", Ptr44); Ptr44 = 0; }      // 0xA72520..0xA72548 (vt+0xC)
                Container2C.Release9D4B20(free);                                        // 0xA72B10 b 0xA73128
                break;
        }
    }
}
