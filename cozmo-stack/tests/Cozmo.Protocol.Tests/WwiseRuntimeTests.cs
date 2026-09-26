using Cozmo.Robot;
using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// Tests for the M6-001/005/018 runtime reader details: the STMG default table, the RTPC parameter varint,
/// the source-plugin parameter block, the BKHD feedback flag, the bus conditional bits, the LayerCntr outer
/// order, the hash's native copy bound, and the internal Wwise format constants.
///
/// The synthetic tests pin the recovered field order; the shipped-asset test pins the actual numbers the
/// inventory records (37 parameters, both Cozmo defaults 1.0). A green shipped test is not by itself proof
/// of source fidelity, so the synthetic one is what fixes the layout.
/// </summary>
public class WwiseRuntimeTests
{
    // ------------------------------------------------------------------ builders

    private static void U32(List<byte> b, uint v) => b.AddRange(BitConverter.GetBytes(v));
    private static void F32(List<byte> b, float v) => b.AddRange(BitConverter.GetBytes(v));
    private static void U16(List<byte> b, ushort v) => b.AddRange(BitConverter.GetBytes(v));

    private static byte[] Chunk(string tag, byte[] body)
    {
        var b = new List<byte>();
        b.AddRange(System.Text.Encoding.ASCII.GetBytes(tag));
        U32(b, (uint)body.Length);
        b.AddRange(body);
        return b.ToArray();
    }

    /// <summary>A bank of arbitrary chunks, with a 20-byte BKHD so the feedback flag is addressable.</summary>
    private static byte[] File(uint bankId, bool feedback, params byte[][] chunks)
    {
        var bkhd = new List<byte>();
        U32(bkhd, 120);                       // version
        U32(bkhd, bankId);
        U32(bkhd, 0);                         // dword 2
        U32(bkhd, feedback ? 1u : 0u);        // dword 3: the feedback flag
        U32(bkhd, 0);
        var file = new List<byte>();
        file.AddRange(Chunk("BKHD", bkhd.ToArray()));
        foreach (var c in chunks) file.AddRange(c);
        return file.ToArray();
    }

    private static byte[] Hirc(params (byte Type, byte[] Payload)[] objects)
    {
        var h = new List<byte>();
        U32(h, (uint)objects.Length);
        foreach (var (type, payload) in objects)
        {
            h.Add(type);
            U32(h, (uint)payload.Length);
            h.AddRange(payload);
        }
        return Chunk("HIRC", h.ToArray());
    }

    /// <summary>The 28-byte empty node block the shipped reader consumes: parent at offset 7.</summary>
    private static void NodeBlock(List<byte> b, uint parent, byte positioning = 0, bool feedback = false)
    {
        b.Add(0);            // fx byte
        b.Add(0);            // no fx
        b.Add(0);            // bOverrideAttachmentParams
        U32(b, 0);           // bus
        U32(b, parent);
        b.Add(0);            // bits
        b.Add(0);            // property count
        b.Add(0);            // ranged count
        b.Add(positioning);
        b.Add(0);            // aux bits
        b.AddRange(new byte[6]);   // advanced settings
        U32(b, 0);           // state-group count
        U16(b, 0);           // RTPC count
        if (feedback) U32(b, 0);   // the four bytes the BKHD feedback flag adds
    }

    private static byte[] SoundPayload(uint id, uint plugin, uint media, uint parent,
                                       byte[]? sourceParams = null, byte positioning = 0, bool feedback = false)
    {
        var b = new List<byte>();
        U32(b, id);
        U32(b, plugin);
        b.Add(0);            // stream type
        U32(b, media);
        U32(b, 0);           // source size
        b.Add(0);            // source bits
        if (sourceParams is not null) { U32(b, (uint)sourceParams.Length); b.AddRange(sourceParams); }
        NodeBlock(b, parent, positioning, feedback);
        return b.ToArray();
    }

    // ------------------------------------------------------------------ STMG

    private static byte[] StmgPayload()
    {
        var b = new List<byte>();
        F32(b, -80f);              // volume threshold
        U16(b, 256);               // max voices
        U32(b, 1);                 // state groups
        U32(b, 11); U32(b, 250); U32(b, 1);
        U32(b, 1); U32(b, 2); U32(b, 3);
        U32(b, 1);                 // switch groups
        U32(b, 22); U32(b, 33); b.Add(4); U32(b, 1);
        U32(b, 5); U32(b, 6); U32(b, 7);
        U32(b, 2);                 // parameters
        U32(b, 100); F32(b, 1.0f); U32(b, 0); F32(b, 0); F32(b, 0); b.Add(0);
        U32(b, 0x83); F32(b, 64.0f); U32(b, 1); F32(b, 2); F32(b, 3); b.Add(1);
        U32(b, 0); U32(b, 0);      // the two unrecovered trailing counts
        return b.ToArray();
    }

    [Fact]
    public void AnStmgChunkIsReadInTheRuntimeFieldOrder()
    {
        var bank = WwiseBank.Parse(File(1, false, Chunk("STMG", StmgPayload())), "t.bnk");
        var s = bank.Stmg;
        Assert.NotNull(s);
        Assert.Equal(-80f, s!.VolumeThreshold);
        Assert.Equal(256, s.MaxVoices);

        var g = Assert.Single(s.StateGroups);
        Assert.Equal(11u, g.Id);
        Assert.Equal(250u, g.DefaultTransitionMs);
        Assert.Equal((1u, 2u, 3u), Assert.Single(g.States));

        var sw = Assert.Single(s.SwitchGroups);
        Assert.Equal(22u, sw.Id);
        Assert.Equal(33u, sw.RtpcId);
        Assert.Equal(4, sw.Flags);
        Assert.Equal((5u, 6u, 7u), Assert.Single(sw.Switches));

        Assert.Equal(2, s.Params.Count);
        Assert.Equal(1.0f, s.DefaultOf(100));
        Assert.Equal(0u, s.Params[100].RampType);
        Assert.False(s.Params[100].BuiltIn);
        Assert.Equal(64.0f, s.DefaultOf(0x83));
        Assert.Equal(1u, s.Params[0x83].RampType);
        Assert.True(s.Params[0x83].BuiltIn);
        Assert.Null(s.DefaultOf(999));
    }

    /// <summary>
    /// A non-zero entry count in either of the two sections after the parameter table is refused, because
    /// their bodies are not recovered. The shipped chunk has both zero.
    /// </summary>
    [Fact]
    public void AnStmgSectionAfterTheParameterTableIsRefusedRatherThanGuessed()
    {
        var body = StmgPayload();
        // The second trailing count is the last four bytes; make it non-zero.
        BitConverter.GetBytes(3u).CopyTo(body, body.Length - 4);
        Assert.Throws<InvalidDataException>(() => WwiseBank.Parse(File(1, false, Chunk("STMG", body)), "t.bnk"));
    }

    [Fact]
    public void TheShippedStmgTableHoldsTheRecoveredDefaults()
    {
        if (WwiseAssets.Library is not { } lib || lib.Stmg is not { } s) return;
        Assert.Equal(37, s.Params.Count);
        Assert.Equal(1.0f, s.DefaultOf(0xD2687048));   // event_volume
        Assert.Equal(1.0f, s.DefaultOf(0x637C1240));   // robot_volume
        Assert.Equal(0u, s.Params[0xD2687048].RampType);
        Assert.Equal(0u, s.Params[0x637C1240].RampType);
    }

    /// <summary>
    /// The M6 shipped-bank sweep, through the loader the newer Wwise tests use (<see cref="WwiseAssets"/>,
    /// i.e. the banks inside <c>AudioAssets.zip</c>). The older <c>WwiseTests</c> sweeps look for a
    /// <c>sound_meta</c> directory that the unpacked OBB does not have, so they return early and report a
    /// pass without loading anything; this one asserts the six banks are actually present and that every
    /// covered hierarchy object consumes its payload exactly, which is the M6-001 requirement.
    ///
    /// It skips only when no shipped assets are unpacked at all. When the archive is present, a missing or
    /// unreadable bank is a failure, not a skip.
    /// </summary>
    [Fact]
    public void TheShippedBanksLoadAndEveryCoveredObjectConsumesExactly()
    {
        if (WwiseAssets.SoundDir is not { } dir) return;   // no unpacked OBB: nothing to verify
        using var lib = WwiseSoundLibrary.Load(dir);

        Assert.Equal(6, lib.Banks.Count);
        Assert.All(lib.Banks, b => Assert.Equal(120u, b.Version));
        Assert.Equal(835, lib.EventIds.Count);

        var covered = lib.CheckHierarchy();
        Assert.NotEmpty(covered);
        foreach (var (type, count, exact, problems) in covered)
        {
            Assert.True(count > 0, $"{type} reported no objects");
            Assert.True(exact == count,
                $"{type}: {exact}/{count} consumed exactly; first problem: {(problems.Count > 0 ? problems[0] : "?")}");
        }
    }

    // ------------------------------------------------------------------ RTPC varint and source plug-in

    private static byte[] ModulatorPayload(uint id, uint paramId)
    {
        var b = new List<byte>();
        U32(b, id);
        b.Add(0);                  // property count
        b.Add(0);                  // ranged count
        U16(b, 1);                 // one RTPC
        U32(b, 0xABCD);            // source id
        b.Add(0);                  // source type (game parameter)
        b.Add(1);                  // accumulate (sum)
        WriteVarint(b, paramId);
        U32(b, 0);                 // curve id
        b.Add(0);                  // scaling
        U16(b, 0);                 // point count
        return b.ToArray();
    }

    private static void WriteVarint(List<byte> b, uint v)
    {
        Span<byte> tmp = stackalloc byte[5];
        int i = 4;
        tmp[i] = (byte)(v & 0x7F);
        v >>= 7;
        while (v != 0) { i--; tmp[i] = (byte)(0x80 | (v & 0x7F)); v >>= 7; }
        for (; i < 5; i++) b.Add(tmp[i]);
    }

    [Fact]
    public void AnRtpcParameterIdIsAVarintNotOneByte()
    {
        // 0x83 needs two groups, so a one-byte read would produce 0x03 and leave a byte over.
        var bank = WwiseBank.Parse(File(1, false, Hirc(((byte)21, ModulatorPayload(7, 0x83)))), "t.bnk");
        var node = Assert.IsType<WwiseModulatorNode>(WwiseHierarchy.TryRead(bank.Objects[7], out var problem));
        Assert.Null(problem);
        Assert.Equal(0x83u, Assert.Single(node.Params.Rtpcs).ParamId);
    }

    [Fact]
    public void ASourcePluginsParameterBlockIsConsumed()
    {
        // Plug-in 0x00040002 has low nibble 2, so a u32 size and that many bytes precede the node block.
        var payload = SoundPayload(50, 0x00040002, 999, 40, sourceParams: new byte[] { 1, 2, 3, 4 });
        var bank = WwiseBank.Parse(File(1, false, Hirc(((byte)2, payload))), "t.bnk");
        var node = Assert.IsType<WwiseSoundNode>(WwiseHierarchy.TryRead(bank.Objects[50], out var problem));
        Assert.Null(problem);
        Assert.Equal(40u, node.Params.ParentId);
        Assert.Equal(999u, node.MediaId);
        Assert.True(node.IsSourcePlugin);
    }

    /// <summary>
    /// gapA 2.4: the native source branch takes plugin &amp; 0xF == 2 <b>or 5</b>
    /// (0x009B9D30 cmp ip,#5; 0x009B9D34 cmpne ip,#2; 0x009B9D38 beq), so a nibble-5 source consumes its
    /// u32 parameter-block size and bytes exactly as a nibble-2 one does. No shipped bank exercises a
    /// nibble-5 source, so this synthetic object is the only place the branch is checked.
    /// </summary>
    [Fact]
    public void ASourcePluginsParameterBlockIsConsumedForNibbleFive()
    {
        // Plug-in 0x00040005 has low nibble 5, which the source branch accepts alongside 2.
        var payload = SoundPayload(50, 0x00040005, 999, 40, sourceParams: new byte[] { 1, 2, 3, 4 });
        var bank = WwiseBank.Parse(File(1, false, Hirc(((byte)2, payload))), "t.bnk");
        var node = Assert.IsType<WwiseSoundNode>(WwiseHierarchy.TryRead(bank.Objects[50], out var problem));
        Assert.Null(problem);
        Assert.Equal(40u, node.Params.ParentId);
        Assert.Equal(999u, node.MediaId);
        Assert.True(node.IsSourcePlugin);
    }

    // ------------------------------------------------------------------ conditional branches

    [Fact]
    public void TheBankFeedbackFlagAddsFourBytesToANode()
    {
        var payload = SoundPayload(50, 0x00040001, 999, 40, feedback: true);
        var bank = WwiseBank.Parse(File(1, true, Hirc(((byte)2, payload))), "t.bnk");
        Assert.True(bank.Objects[50].FeedbackEnabled);
        var node = Assert.IsType<WwiseSoundNode>(WwiseHierarchy.TryRead(bank.Objects[50], out var problem));
        Assert.Null(problem);
        Assert.Equal(40u, node.Params.ParentId);
    }

    [Fact]
    public void APositioningBodyIsRefusedRatherThanGuessed()
    {
        // b0 and b3 both set begins the 3D body, whose layout past the attenuation is not recovered.
        var payload = SoundPayload(50, 0x00040001, 999, 40, positioning: 0x09);
        var bank = WwiseBank.Parse(File(1, false, Hirc(((byte)2, payload))), "t.bnk");
        Assert.Null(WwiseHierarchy.TryRead(bank.Objects[50], out var problem));
        Assert.Contains("not recovered", problem);
    }

    [Fact]
    public void ABusConditionalBodyIsRefusedRatherThanGuessed()
    {
        var b = new List<byte>();
        U32(b, 60);                 // id
        U32(b, 0);                  // parent
        b.Add(0);                   // property count
        b.Add(0);                   // A
        b.Add(0x01);                // B: b0 set, body not recovered
        var bank = WwiseBank.Parse(File(1, false, Hirc(((byte)8, b.ToArray()))), "t.bnk");
        Assert.Null(WwiseHierarchy.TryRead(bank.Objects[60], out var problem));
        Assert.Contains("not recovered", problem);
    }

    [Fact]
    public void ALayerWithNoLayersReadsTheFrozenOuterOrder()
    {
        var b = new List<byte>();
        U32(b, 70);                 // id
        NodeBlock(b, 0);
        U32(b, 0);                  // child count
        U32(b, 0);                  // layer count
        b.Add(0);                   // bIsContinuousValidation
        var bank = WwiseBank.Parse(File(1, false, Hirc(((byte)9, b.ToArray()))), "t.bnk");
        var node = Assert.IsType<WwiseBlendNode>(WwiseHierarchy.TryRead(bank.Objects[70], out var problem));
        Assert.Null(problem);
        Assert.Equal(0, node.BlendTracks);
    }

    // ------------------------------------------------------------------ hash bound and format constants

    [Fact]
    public void TheNameHashIsBoundedToTheNativeCopy()
    {
        Assert.Equal(0x103, WwiseHash.MaxBytes);

        // The native copy takes min(strlen + 1, 0x103) bytes and hashes strlen of them, so a name of L
        // bytes hashes min(L, 0x102) characters. A 258-byte (0x102) name is exactly at that bound and is
        // hashed whole. The expected value is derived here from the documented FNV-1-over-lowercase
        // definition, not from WwiseHash.Of, so this pins that all 0x102 bytes are hashed rather than
        // echoing the implementation's own truncation.
        var atLimit = new string('a', 0x102);
        Assert.Equal(Fnv1OfLowercaseAscii(atLimit), WwiseHash.Of(atLimit));

        // One byte below the bound the next character still counts, so the hashes differ.
        var below = new string('a', 0x101);
        Assert.NotEqual(WwiseHash.Of(below), WwiseHash.Of(below + "z"));

        // A name whose strlen reaches 0x103 is the native's undefined case (the copy is not
        // NUL-terminated there, so the native strlen reads past it). This stack hashes min(L, 0x102) for
        // it; no assertion is made about the native result.
    }

    /// <summary>
    /// The documented FNV-1 over a lower-cased ASCII name, written from the definition so a bounded hash
    /// is not its own oracle: h = 0x811C9DC5; per byte h = h·16777619, then h ^= byte.
    /// </summary>
    private static uint Fnv1OfLowercaseAscii(string name)
    {
        uint h = 2166136261;
        foreach (char c in name)
        {
            byte b = (byte)(c is >= 'A' and <= 'Z' ? c + 32 : c);
            h = unchecked(h * 16777619);
            h ^= b;
        }
        return h;
    }

    [Fact]
    public void TheInternalWwiseFormatIsDistinctFromTheRobotEndpoint()
    {
        Assert.Equal(48000, WwiseRuntimeSettings.MixRateHz);
        Assert.Equal(1024, WwiseRuntimeSettings.SamplesPerFrame);
        Assert.Equal(21, WwiseRuntimeSettings.MsPerFrame);
        Assert.Equal(5, WwiseRuntimeSettings.QuarterFrameMs);
        Assert.Equal(128, WwiseRuntimeSettings.LpfChunkSamples);
        // The robot endpoint is a different domain and must not have been substituted.
        Assert.NotEqual(CozmoAudio.SampleRate, WwiseRuntimeSettings.MixRateHz);
        Assert.NotEqual(CozmoAudio.SamplesPerFrame, WwiseRuntimeSettings.SamplesPerFrame);
    }
}
