using NVorbis;

namespace Cozmo.Robot.Animation.Wwise;

/// <summary>The result of decoding one Wwise Vorbis file.</summary>
public sealed record VorbisDecodeResult(short[] Samples, int Channels, int SampleRate);

/// <summary>
/// Decodes a Wwise Vorbis <c>.wem</c>: rebuilds it into a standard Ogg Vorbis stream, then decodes that.
///
/// The two halves are deliberately separate. <see cref="WwiseVorbisRebuilder"/> is ours, ported from
/// ww2ogg, and does the Wwise-specific work. The actual Vorbis decoding is NVorbis (MIT), used as an
/// ordinary package and confined to this file, so nothing else in the stack depends on it.
/// </summary>
public static class WwiseVorbis
{
    /// <summary>
    /// Decodes to interleaved 16-bit samples. Throws <see cref="InvalidDataException"/> with a specific
    /// reason if the stream cannot be rebuilt or cannot be decoded.
    /// </summary>
    public static VorbisDecodeResult Decode(WwiseMedia media, WwiseCodebookLibrary codebooks)
    {
        var ogg = WwiseVorbisRebuilder.ToOgg(media, codebooks);

        using var ms = new MemoryStream(ogg, writable: false);
        VorbisReader reader;
        try
        {
            reader = new VorbisReader(ms, closeOnDispose: false);
        }
        catch (Exception ex)
        {
            throw new InvalidDataException($"the rebuilt Ogg stream would not open: {ex.Message}", ex);
        }

        using (reader)
        {
            int channels = reader.Channels;
            if (channels < 1) throw new InvalidDataException("the rebuilt stream reports no channels");

            // NVorbis hands back floats; the rest of the stack works in 16-bit.
            var floats = new float[channels * 4096];
            var samples = new List<short>(Math.Max(1024, media.Vorbis is { } v ? (int)v.SampleCount * channels : 1024));
            int read;
            try
            {
                while ((read = reader.ReadSamples(floats, 0, floats.Length)) > 0)
                    for (int i = 0; i < read; i++)
                        samples.Add((short)Math.Clamp((int)MathF.Round(floats[i] * 32767f), short.MinValue, short.MaxValue));
            }
            catch (Exception ex)
            {
                throw new InvalidDataException($"decoding the rebuilt stream failed: {ex.Message}", ex);
            }

            if (samples.Count == 0) throw new InvalidDataException("the rebuilt stream decoded to nothing");
            return new VorbisDecodeResult(samples.ToArray(), channels, reader.SampleRate);
        }
    }
}
