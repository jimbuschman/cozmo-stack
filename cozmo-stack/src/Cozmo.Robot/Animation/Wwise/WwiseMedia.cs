using System.Buffers.Binary;

namespace Cozmo.Robot.Animation.Wwise;

/// <summary>Which codec a <c>.wem</c> holds. The two that appear in this build are named; anything else is carried as its raw tag.</summary>
public enum WwiseCodec
{
    /// <summary>Unrecognised: the format tag is reported but nothing is decoded.</summary>
    Unknown = 0,
    /// <summary>Format tag 2. IMA-style 4-bit ADPCM in fixed blocks. 227 files in this build.</summary>
    Adpcm = 2,
    /// <summary>Format tag 0xFFFF with a Wwise vorb extension. 1987 files in this build.</summary>
    Vorbis = 0xFFFF,
}

/// <summary>
/// The header of one Wwise media file. A <c>.wem</c> is a RIFF container holding a <c>fmt</c> chunk and a
/// <c>data</c> chunk; in this build there are never any others.
/// </summary>
public sealed class WwiseMedia
{
    public required WwiseCodec Codec { get; init; }
    /// <summary>The raw <c>wFormatTag</c>, kept so an unrecognised codec can still be reported honestly.</summary>
    public required ushort FormatTag { get; init; }
    public required int Channels { get; init; }
    public required int SampleRate { get; init; }
    /// <summary>Bytes per ADPCM block. Zero for Vorbis, which is not block-aligned.</summary>
    public required int BlockAlign { get; init; }
    /// <summary>The <c>data</c> chunk.</summary>
    public required ReadOnlyMemory<byte> Data { get; init; }
    /// <summary>Vorbis only: the parsed vorb extension. Null for every other codec.</summary>
    public WwiseVorbisHeader? Vorbis { get; init; }

    /// <summary>
    /// How many samples per channel this file holds, where that is known without decoding it.
    ///
    /// Vorbis states it outright. ADPCM is computed from the block layout, which this build's own header
    /// fields corroborate: at 44100 Hz with a 36-byte block and 24806 average bytes per second, the
    /// blocks-per-second works out at 689.05 and so 64.0 samples per block, exactly the
    /// <c>(BlockAlign - 4 per channel) * 2</c> that the IMA block layout gives.
    /// </summary>
    public int? SampleCount => Codec switch
    {
        WwiseCodec.Vorbis => (int)(Vorbis?.SampleCount ?? 0),
        WwiseCodec.Adpcm when BlockAlign > 4 * Channels =>
            Data.Length / BlockAlign * ((BlockAlign - 4 * Channels) * 2 / Channels),
        _ => null,
    };

    /// <summary>Playing time, where <see cref="SampleCount"/> is known.</summary>
    public TimeSpan? Duration =>
        SampleCount is { } n && SampleRate > 0 ? TimeSpan.FromSeconds(n / (double)SampleRate) : null;

    /// <summary>Reads the header of a <c>.wem</c>. Throws <see cref="InvalidDataException"/> if it is not one.</summary>
    public static WwiseMedia Parse(ReadOnlyMemory<byte> file)
    {
        var s = file.Span;
        if (s.Length < 12 || s[0] != 'R' || s[1] != 'I' || s[2] != 'F' || s[3] != 'F')
            throw new InvalidDataException("not a RIFF file");
        if (s[8] != 'W' || s[9] != 'A' || s[10] != 'V' || s[11] != 'E')
            throw new InvalidDataException("RIFF file is not WAVE");

        ReadOnlyMemory<byte> fmt = default, data = default;
        int off = 12;
        while (off + 8 <= s.Length)
        {
            var tag = s.Slice(off, 4);
            uint size = BinaryPrimitives.ReadUInt32LittleEndian(s.Slice(off + 4, 4));
            int body = off + 8;
            if (size > (uint)(s.Length - body)) break;   // a truncated trailing chunk is not fatal
            if (tag[0] == 'f' && tag[1] == 'm' && tag[2] == 't') fmt = file.Slice(body, (int)size);
            else if (tag[0] == 'd' && tag[1] == 'a' && tag[2] == 't' && tag[3] == 'a') data = file.Slice(body, (int)size);
            off = body + (int)size + ((int)size & 1);    // RIFF pads odd chunks to even
        }
        if (fmt.IsEmpty) throw new InvalidDataException("no fmt chunk");

        var f = fmt.Span;
        if (f.Length < 16) throw new InvalidDataException($"fmt chunk is only {f.Length} bytes");
        ushort tagVal = BinaryPrimitives.ReadUInt16LittleEndian(f[..2]);
        int channels = BinaryPrimitives.ReadUInt16LittleEndian(f.Slice(2, 2));
        int rate = (int)BinaryPrimitives.ReadUInt32LittleEndian(f.Slice(4, 4));
        int blockAlign = BinaryPrimitives.ReadUInt16LittleEndian(f.Slice(12, 2));

        WwiseVorbisHeader? vorb = null;
        var codec = tagVal switch
        {
            2 => WwiseCodec.Adpcm,
            0xFFFF => WwiseCodec.Vorbis,
            _ => WwiseCodec.Unknown,
        };
        if (codec == WwiseCodec.Vorbis) vorb = WwiseVorbisHeader.Parse(fmt);

        return new WwiseMedia
        {
            Codec = codec, FormatTag = tagVal, Channels = channels, SampleRate = rate,
            BlockAlign = blockAlign, Data = data, Vorbis = vorb,
        };
    }
}

/// <summary>
/// The Wwise "vorb" header, which in this build's bank version lives inside the <c>fmt</c> chunk rather
/// than in a chunk of its own.
///
/// Where a <c>fmt</c> chunk is 0x42 bytes and there is no separate <c>vorb</c> chunk, the vorb data begins
/// at <c>fmt + 0x18</c> and is 0x2A bytes long. Every one of the 1987 Vorbis files in this build is that
/// shape. The layout is cross-checked by the assets themselves: the last two bytes decode to blocksize
/// exponents of 8 and 11 (or 9 and 10 in 32 files), which are the only legal Vorbis pairs and could not
/// arise by accident; and <see cref="SampleCount"/> divided by the sample rate gives sane playing times
/// throughout, for example 0.89 to 1.13 seconds for the three alternatives of a one-second voice clip.
/// </summary>
public sealed class WwiseVorbisHeader
{
    /// <summary>Samples per channel in the stream.</summary>
    public required uint SampleCount { get; init; }
    /// <summary>Where the Vorbis setup packet begins, as an offset into the <c>data</c> chunk.</summary>
    public required uint SetupPacketOffset { get; init; }
    /// <summary>Where audio packets begin, as an offset into the <c>data</c> chunk.</summary>
    public required uint FirstAudioPacketOffset { get; init; }
    /// <summary>Identifies which packed codebook set the setup packet indexes into.</summary>
    public required uint Uid { get; init; }
    /// <summary>log2 of the short block size. 8 in all but 32 files, which use 9.</summary>
    public required byte BlockSize0Pow { get; init; }
    /// <summary>log2 of the long block size. 11 in all but 32 files, which use 10.</summary>
    public required byte BlockSize1Pow { get; init; }

    /// <summary>The bytes between the setup packet and the first audio packet.</summary>
    public int SetupPacketLength => (int)(FirstAudioPacketOffset - SetupPacketOffset);

    internal static WwiseVorbisHeader? Parse(ReadOnlyMemory<byte> fmt)
    {
        var f = fmt.Span;
        if (f.Length < VorbOffset + VorbLength) return null;
        var v = f.Slice(VorbOffset, VorbLength);
        return new WwiseVorbisHeader
        {
            SampleCount = BinaryPrimitives.ReadUInt32LittleEndian(v.Slice(0x00, 4)),
            SetupPacketOffset = BinaryPrimitives.ReadUInt32LittleEndian(v.Slice(0x10, 4)),
            FirstAudioPacketOffset = BinaryPrimitives.ReadUInt32LittleEndian(v.Slice(0x14, 4)),
            Uid = BinaryPrimitives.ReadUInt32LittleEndian(v.Slice(0x24, 4)),
            BlockSize0Pow = v[0x28],
            BlockSize1Pow = v[0x29],
        };
    }

    private const int VorbOffset = 0x18;
    private const int VorbLength = 0x2A;
}
