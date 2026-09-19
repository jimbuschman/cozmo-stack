using System.Buffers.Binary;
using System.Text;

namespace Cozmo.Robot.Animation;

/// <summary>
/// Supplies the sound an audio keyframe asks for.
///
/// Animation audio keyframes carry Wwise event ids, not samples. Turning an id into audio means resolving
/// it through the sound banks to a <c>.wem</c> file and decoding that. This interface is the seam: the
/// animation system asks for an event and gets 16-bit PCM at <see cref="CozmoAudio.SampleRate"/> back, or
/// null when the sound cannot be produced.
///
/// <see cref="SoundBankIndex"/> implements the resolution half against Cozmo's own metadata.
/// <see cref="WavAudioSource"/> lets a caller supply their own audio per event, which is how sound can be
/// played today without a Wwise decoder.
/// </summary>
public interface IAnimationAudioSource
{
    /// <summary>
    /// PCM for this event at <see cref="CozmoAudio.SampleRate"/>, or null when it cannot be produced.
    /// Called on the scheduler's thread when the keyframe fires, so it should be quick; a source that needs
    /// to decode should cache.
    /// </summary>
    short[]? GetPcm(long eventId, float volume);

    /// <summary>A human-readable name for the event, for logs and diagnostics. Null when unknown.</summary>
    string? NameOf(long eventId);
}

/// <summary>
/// An audio source that keeps Wwise switch states, so an event whose sound depends on a switch (the
/// Cozmo_Sings songs) plays what the current state selects. The engine sets these through
/// <c>RobotAudioClient::PostRobotSwitchState</c> before the animation that raises the event starts.
/// </summary>
public interface IAudioSwitchStates
{
    /// <summary>Sets a switch group's current value; both are Wwise ids (FNV-1 of the names).</summary>
    void SetSwitch(uint groupId, uint switchId);
    /// <summary>The current switch values, group id to switch id.</summary>
    IReadOnlyDictionary<uint, uint> Switches { get; }
}

/// <summary>
/// Reads Cozmo's <c>SoundbanksInfo.xml</c> so an audio event id can at least be named, and says which bank
/// it belongs to.
///
/// This resolves ids to <b>names</b>, not to audio. The metadata lists events and it lists files, but it
/// does not link the two: that mapping lives in each bank's HIRC section, which is not parsed here. So this
/// is deliberately half of the job, and it says so rather than pretending otherwise.
/// </summary>
public sealed class SoundBankIndex
{
    private readonly Dictionary<long, (string Name, string Bank)> _events = new();
    private readonly List<string> _files = new();

    /// <summary>Events known, by id.</summary>
    public int EventCount => _events.Count;
    /// <summary>Audio files the metadata lists, whether or not anything can be mapped to them.</summary>
    public int FileCount => _files.Count;

    /// <summary>
    /// Opens the index over an unpacked resources tree, or over an <c>AudioAssets.zip</c> directly. Returns
    /// null when no metadata can be found, so a caller can carry on without audio.
    /// </summary>
    public static SoundBankIndex? Open(string soundRoot)
    {
        var xml = FindMetadata(soundRoot);
        return xml is null ? null : Parse(xml);
    }

    private static string? FindMetadata(string root)
    {
        if (File.Exists(root) && root.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            return ReadFromZip(root);
        foreach (var candidate in new[]
                 {
                     Path.Combine(root, "SoundbanksInfo.xml"),
                     Path.Combine(root, "sound", "SoundbanksInfo.xml"),
                 })
            if (File.Exists(candidate)) return File.ReadAllText(candidate);

        foreach (var zip in new[] { Path.Combine(root, "AudioAssets.zip"), Path.Combine(root, "sound", "AudioAssets.zip") })
            if (File.Exists(zip)) return ReadFromZip(zip);
        return null;
    }

    private static string? ReadFromZip(string zipPath)
    {
        try
        {
            using var z = System.IO.Compression.ZipFile.OpenRead(zipPath);
            var entry = z.GetEntry("SoundbanksInfo.xml");
            if (entry is null) return null;
            using var s = entry.Open();
            using var r = new StreamReader(s, Encoding.UTF8);
            return r.ReadToEnd();
        }
        catch (Exception) { return null; }
    }

    /// <summary>Parses the metadata document.</summary>
    public static SoundBankIndex Parse(string xml)
    {
        var index = new SoundBankIndex();
        var doc = System.Xml.Linq.XDocument.Parse(xml);
        foreach (var bank in doc.Descendants("SoundBank"))
        {
            string bankName = bank.Element("ShortName")?.Value ?? "";
            foreach (var e in bank.Descendants("Event"))
            {
                if (!long.TryParse(e.Attribute("Id")?.Value, out var id)) continue;
                index._events[id] = (e.Attribute("Name")?.Value ?? "", bankName);
            }
        }
        foreach (var f in doc.Descendants("File"))
        {
            var path = f.Element("Path")?.Value;
            if (!string.IsNullOrEmpty(path)) index._files.Add(path);
        }
        return index;
    }

    /// <summary>The event's name, or null when the id is not in the metadata.</summary>
    public string? NameOf(long eventId) => _events.TryGetValue(eventId, out var e) ? e.Name : null;

    /// <summary>The bank the event belongs to, or null.</summary>
    public string? BankOf(long eventId) => _events.TryGetValue(eventId, out var e) ? e.Bank : null;
}

/// <summary>
/// An audio source backed by WAV files the caller supplies, one per event.
///
/// This is how animation audio can be played today. A caller that has extracted or recorded sounds maps
/// them to event ids, by id or by the event's own name through a <see cref="SoundBankIndex"/>, and the
/// animation system streams them on the timeline like any other track.
///
/// Accepts 16-bit PCM WAV, mono or stereo, at any sample rate; stereo is mixed down and the rate converted
/// to <see cref="CozmoAudio.SampleRate"/> by nearest-sample resampling, which is adequate for a speaker
/// this small.
/// </summary>
public sealed class WavAudioSource : IAnimationAudioSource
{
    private readonly Dictionary<long, short[]> _byId = new();
    private readonly SoundBankIndex? _names;

    public WavAudioSource(SoundBankIndex? names = null) => _names = names;

    /// <summary>Registers a WAV file for an event id.</summary>
    public void Add(long eventId, string wavPath) => _byId[eventId] = ReadWav(File.ReadAllBytes(wavPath));

    /// <summary>Registers PCM already in memory, at <see cref="CozmoAudio.SampleRate"/>.</summary>
    public void Add(long eventId, short[] pcm) => _byId[eventId] = pcm;

    public string? NameOf(long eventId) => _names?.NameOf(eventId);

    public short[]? GetPcm(long eventId, float volume)
    {
        if (!_byId.TryGetValue(eventId, out var pcm)) return null;
        if (Math.Abs(volume - 1f) < 0.001f) return pcm;
        var scaled = new short[pcm.Length];
        for (int i = 0; i < pcm.Length; i++)
            scaled[i] = (short)Math.Clamp(pcm[i] * volume, short.MinValue, short.MaxValue);
        return scaled;
    }

    /// <summary>Decodes a 16-bit PCM WAV to mono at the robot's sample rate.</summary>
    public static short[] ReadWav(byte[] bytes)
    {
        if (bytes.Length < 44 || Encoding.ASCII.GetString(bytes, 0, 4) != "RIFF"
                              || Encoding.ASCII.GetString(bytes, 8, 4) != "WAVE")
            throw new InvalidDataException("not a RIFF/WAVE file");

        int channels = 1, rate = CozmoAudio.SampleRate, bits = 16;
        int pos = 12;
        short[]? samples = null;
        while (pos + 8 <= bytes.Length)
        {
            string id = Encoding.ASCII.GetString(bytes, pos, 4);
            int size = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(pos + 4));
            int body = pos + 8;
            if (size < 0 || body + size > bytes.Length) size = bytes.Length - body;

            if (id == "fmt ")
            {
                channels = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(body + 2));
                rate = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(body + 4));
                bits = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(body + 14));
            }
            else if (id == "data")
            {
                if (bits != 16) throw new InvalidDataException($"only 16-bit PCM is supported, this is {bits}-bit");
                int total = size / 2;
                var raw = new short[total];
                for (int i = 0; i < total; i++) raw[i] = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(body + i * 2));
                samples = raw;
            }
            pos = body + size + (size & 1);
        }
        if (samples is null) throw new InvalidDataException("no data chunk");

        // mix to mono
        if (channels > 1)
        {
            var mono = new short[samples.Length / channels];
            for (int i = 0; i < mono.Length; i++)
            {
                int sum = 0;
                for (int c = 0; c < channels; c++) sum += samples[i * channels + c];
                mono[i] = (short)(sum / channels);
            }
            samples = mono;
        }

        // nearest-sample rate conversion
        if (rate != CozmoAudio.SampleRate && rate > 0)
        {
            int outLen = (int)((long)samples.Length * CozmoAudio.SampleRate / rate);
            var resampled = new short[outLen];
            for (int i = 0; i < outLen; i++)
                resampled[i] = samples[(int)Math.Min(samples.Length - 1L, (long)i * rate / CozmoAudio.SampleRate)];
            samples = resampled;
        }
        return samples;
    }
}
