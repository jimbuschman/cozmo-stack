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

    /// <summary>The word at <c>+8</c> of each element the array holds (0 for none).</summary>
    public List<uint> ElementPtrs8 { get; } = new();

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

/// <summary>Which in-memory source class a <see cref="WwiseInMemorySourceFields"/> belongs to.</summary>
public enum WwiseInMemoryKind
{
    /// <summary>PCM in-memory, vptr <c>0x103D740</c>: <c>vt+0x2C = 0xA73128</c>.</summary>
    Pcm,

    /// <summary>ADPCM in-memory, vptr <c>0x103D6C0</c>: <c>vt+0x2C = 0xA72AF4</c>.</summary>
    Adpcm,

    /// <summary>Vorbis in-memory, vptr <c>0x103E0B8</c>: <c>vt+0x2C = 0xAB0FC0</c>.</summary>
    Vorbis,
}

/// <summary>
/// The fields the in-memory classes' closes read (S2, S3, S6). They are written by the in-memory StartStream bodies, which are unread (C33 batch 5b MISSING), so they are host input: the chunk container, <c>[src+0x44]</c> (ADPCM
/// decode buffer), <c>[src+0x80]</c> with <c>[src+0x3C]</c> and <c>[src+0xC0]</c> (Vorbis), and the Vorbis DSP teardown seam.
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

    /// <summary><c>[src+0x80]</c> (Vorbis): a block freed with <c>[src+0x3C]</c> cleared (<c>0xAB032C</c>).</summary>
    public uint Ptr80 { get; set; }

    /// <summary><c>[src+0x3C]</c> (Vorbis).</summary>
    public uint Word3C { get; set; }

    /// <summary><c>[src+0xC0]</c> (Vorbis): a block <c>0xAB0FC0</c> frees.</summary>
    public uint PtrC0 { get; set; }

    /// <summary><c>0xAB3428(src+0x4C)</c> (<c>0xAB0FCC</c>): the Vorbis DSP teardown. Not adopted; required for the Vorbis class.</summary>
    public Action? DspTeardownAB3428 { get; set; }

    /// <summary>
    /// <c>vt+0x2C</c>: PCM <c>0xA73128</c> (the container, S2); ADPCM <c>0xA72AF4</c> (<c>vt+0xC = 0xA72520</c>: a non-null <c>[src+0x44]</c> is freed and cleared, then the container, S3); Vorbis <c>0xAB0FC0</c> (<c>0xAB3428(src+0x4C)</c>,
    /// <c>vt+0xC = 0xAB032C</c>: a non-null <c>[src+0x80]</c> is freed with <c>[src+0x3C]</c> and <c>[src+0x80]</c> cleared, a non-null <c>[src+0xC0]</c> freed and cleared, then the container, S6).
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
            case WwiseInMemoryKind.Vorbis:
                (DspTeardownAB3428 ?? throw new WwiseMissingBehaviourException(
                    "M6-025 S6: the Vorbis DSP teardown 0xAB3428 (0xAB0FCC) is not adopted; supply WwiseInMemorySourceFields.DspTeardownAB3428"))();
                if (Ptr80 != 0) { free?.Invoke("[src+0x80]", Ptr80); Word3C = 0; Ptr80 = 0; }   // 0xAB032C..0xAB035C (vt+0xC)
                if (PtrC0 != 0) { free?.Invoke("[src+0xC0]", PtrC0); PtrC0 = 0; }       // 0xAB0FE0..0xAB1000
                Container2C.Release9D4B20(free);                                        // 0xAB100C b 0xA73128
                break;
        }
    }
}
