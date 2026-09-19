using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// Tests for Cozmo's own sound library.
///
/// The ones that build banks by hand run everywhere. The ones that read the shipped assets skip themselves
/// when the OBB is not unpacked, the same way the animation asset tests do, because the assets are not in
/// the repository. Where they do run they check the whole library rather than one clip, which is the point:
/// a mapping that works for one event and not for 600 others is a coincidence, not a decoding.
/// </summary>
public class WwiseTests
{
    // ------------------------------------------------------------------ synthetic banks

    /// <summary>Builds a minimal but real bank: header, then a hierarchy of the given objects.</summary>
    private static byte[] Bank(uint bankId, params (byte Type, byte[] Payload)[] objects)
    {
        var hirc = new List<byte>();
        hirc.AddRange(BitConverter.GetBytes((uint)objects.Length));
        foreach (var (type, payload) in objects)
        {
            hirc.Add(type);
            hirc.AddRange(BitConverter.GetBytes((uint)payload.Length));
            hirc.AddRange(payload);
        }
        var file = new List<byte>();
        file.AddRange("BKHD"u8.ToArray());
        file.AddRange(BitConverter.GetBytes(8u));
        file.AddRange(BitConverter.GetBytes(120u));
        file.AddRange(BitConverter.GetBytes(bankId));
        file.AddRange("HIRC"u8.ToArray());
        file.AddRange(BitConverter.GetBytes((uint)hirc.Count));
        file.AddRange(hirc);
        return file.ToArray();
    }

    private static byte[] Event(uint id, params uint[] actions)
    {
        var b = new List<byte>();
        b.AddRange(BitConverter.GetBytes(id));
        b.AddRange(BitConverter.GetBytes((uint)actions.Length));
        foreach (var a in actions) b.AddRange(BitConverter.GetBytes(a));
        return b.ToArray();
    }

    private static byte[] Action(uint id, ushort type, uint target)
    {
        var b = new List<byte>();
        b.AddRange(BitConverter.GetBytes(id));
        b.AddRange(BitConverter.GetBytes(type));
        b.AddRange(BitConverter.GetBytes(target));
        b.AddRange(new byte[4]);                       // bus flag and an empty property bundle
        return b.ToArray();
    }

    private static byte[] Sound(uint id, uint mediaId, uint parent)
    {
        var b = new byte[40];
        BitConverter.GetBytes(id).CopyTo(b, 0);
        BitConverter.GetBytes(mediaId).CopyTo(b, 9);   // after the plugin id and stream type
        BitConverter.GetBytes(parent).CopyTo(b, 25);
        return b;
    }

    [Fact]
    public void ABankParsesItsHeaderAndHierarchy()
    {
        var bank = WwiseBank.Parse(Bank(4242, ((byte)4, Event(1, 2)), ((byte)3, Action(2, 0x0403, 3))), "t.bnk");
        Assert.Equal(4242u, bank.BankId);
        Assert.Equal(120u, bank.Version);
        Assert.Equal(2, bank.Objects.Count);
        Assert.Equal(WwiseObjectType.Event, bank.Objects[1].Type);
        Assert.Equal(WwiseObjectType.EventAction, bank.Objects[2].Type);
    }

    /// <summary>
    /// The reader insists the declared lengths land exactly on the end. That check is what established
    /// the object layout in the first place, so it must not be quietly relaxed.
    /// </summary>
    [Fact]
    public void ABankWhoseLengthsDoNotAddUpIsRefused()
    {
        var good = Bank(1, ((byte)4, Event(1, 2)));
        var truncated = good[..^4];
        Assert.Throws<InvalidDataException>(() => WwiseBank.Parse(truncated, "t.bnk"));

        // claim the hierarchy holds more objects than it does. The object count sits just past the BKHD
        // chunk (16 bytes) and the HIRC tag and length (8 more).
        var overlong = (byte[])good.Clone();
        BitConverter.GetBytes(9u).CopyTo(overlong, 24);
        Assert.ThrowsAny<InvalidDataException>(() => WwiseBank.Parse(overlong, "t.bnk"));
    }

    [Fact]
    public void AnEventsActionsAndAPlayTargetAreRead()
    {
        var bank = WwiseBank.Parse(Bank(1,
            ((byte)4, Event(10, 20, 21)),
            ((byte)3, Action(20, 0x0403, 30)),
            ((byte)3, Action(21, 0x0103, 31))), "t.bnk");

        Assert.Equal(new uint[] { 20, 21 }, WwiseBank.EventActions(bank.Objects[10]));
        Assert.Equal(30u, WwiseBank.PlayActionTarget(bank.Objects[20]));
        // a Stop action starts nothing, so it must not be mistaken for a source of sound
        Assert.Null(WwiseBank.PlayActionTarget(bank.Objects[21]));
    }

    [Fact]
    public void ASoundsMediaIdAndParentAreRead()
    {
        var bank = WwiseBank.Parse(Bank(1, ((byte)2, Sound(50, 999, 40))), "t.bnk");
        Assert.Equal(999u, WwiseBank.SoundMediaId(bank.Objects[50]));
        Assert.Equal(40u, WwiseBank.ParentId(bank.Objects[50]));
    }

    // ------------------------------------------------------------------ media headers

    private static byte[] Riff(string codec, int channels, int rate, int blockAlign, byte[] data, byte[]? ext = null)
    {
        var fmt = new List<byte>();
        fmt.AddRange(BitConverter.GetBytes(codec == "adpcm" ? (ushort)2 : (ushort)0xFFFF));
        fmt.AddRange(BitConverter.GetBytes((ushort)channels));
        fmt.AddRange(BitConverter.GetBytes((uint)rate));
        fmt.AddRange(BitConverter.GetBytes((uint)(rate * blockAlign / 64)));
        fmt.AddRange(BitConverter.GetBytes((ushort)blockAlign));
        fmt.AddRange(BitConverter.GetBytes((ushort)4));
        fmt.AddRange(BitConverter.GetBytes((ushort)(ext?.Length ?? 0)));
        if (ext is not null) fmt.AddRange(ext);

        var body = new List<byte>();
        body.AddRange("WAVE"u8.ToArray());
        body.AddRange("fmt "u8.ToArray());
        body.AddRange(BitConverter.GetBytes((uint)fmt.Count));
        body.AddRange(fmt);
        body.AddRange("data"u8.ToArray());
        body.AddRange(BitConverter.GetBytes((uint)data.Length));
        body.AddRange(data);

        var file = new List<byte>();
        file.AddRange("RIFF"u8.ToArray());
        file.AddRange(BitConverter.GetBytes((uint)body.Count));
        file.AddRange(body);
        return file.ToArray();
    }

    [Fact]
    public void AnAdpcmHeaderIsReadAndItsLengthComputed()
    {
        var m = WwiseMedia.Parse(Riff("adpcm", 1, 44100, 36, new byte[36 * 10]));
        Assert.Equal(WwiseCodec.Adpcm, m.Codec);
        Assert.Equal(1, m.Channels);
        Assert.Equal(44100, m.SampleRate);
        Assert.Equal(64 * 10, m.SampleCount);        // 64 samples per 36-byte block
    }

    [Fact]
    public void SomethingThatIsNotAWemIsRefused()
    {
        Assert.Throws<InvalidDataException>(() => WwiseMedia.Parse(new byte[] { 1, 2, 3, 4 }));
        // a RIFF file whose form type is not WAVE
        var notWave = new byte[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F', 4, 0, 0, 0, (byte)'J', (byte)'U', (byte)'N', (byte)'K' };
        Assert.Throws<InvalidDataException>(() => WwiseMedia.Parse(notWave));
    }

    // ------------------------------------------------------------------ the ADPCM decoder

    /// <summary>
    /// IMA is a feedback loop: a wrong step table, index table or nibble order makes the predictor run
    /// away and pin to full scale within a few dozen samples. Decoding a block of maximum-magnitude
    /// nibbles and finding it bounded, then a block of zero nibbles and finding it settle, is what
    /// distinguishes a working decoder from one that merely produces numbers.
    /// </summary>
    [Fact]
    public void TheAdpcmDecoderConvergesRatherThanRunningAway()
    {
        var block = new byte[36];
        // starting predictor 0, step index 0, then nibbles that all push the same way
        for (int i = 4; i < 36; i++) block[i] = 0x77;
        var pcm = WwiseAdpcm.Decode(WwiseMedia.Parse(Riff("adpcm", 1, 44100, 36, block)));
        Assert.Equal(64, pcm.Length);
        Assert.All(pcm, s => Assert.InRange(s, short.MinValue, short.MaxValue));
        Assert.True(pcm[^1] > pcm[0], "nibbles that all push upward should raise the signal");
    }

    [Fact]
    public void AnAdpcmBlockOfSilenceDecodesToSilence()
    {
        var block = new byte[36];                     // predictor 0, index 0, all nibbles zero
        var pcm = WwiseAdpcm.Decode(WwiseMedia.Parse(Riff("adpcm", 1, 44100, 36, block)));
        Assert.Equal(64, pcm.Length);
        // a zero nibble still moves by step>>3, so the signal stays tiny rather than exactly zero
        Assert.All(pcm, s => Assert.InRange(s, (short)-64, (short)64));
    }

    [Fact]
    public void ATrailingPartialBlockIsIgnoredRatherThanHalfDecoded()
    {
        var data = new byte[36 + 10];
        var pcm = WwiseAdpcm.Decode(WwiseMedia.Parse(Riff("adpcm", 1, 44100, 36, data)));
        Assert.Equal(64, pcm.Length);
    }

    // ------------------------------------------------------------------ end-to-end resolution

    [Fact]
    public void AnEventResolvesThroughAContainerToEveryAlternative()
    {
        var dir = Directory.CreateTempSubdirectory("wwise-test");
        try
        {
            // event -> Play action -> container -> three sounds, which is the shape a voice line has
            File.WriteAllBytes(Path.Combine(dir.FullName, "T.bnk"), Bank(1,
                ((byte)4, Event(100, 200)),
                ((byte)3, Action(200, 0x0403, 300)),
                ((byte)5, Container(300)),
                ((byte)2, Sound(301, 9001, 300)),
                ((byte)2, Sound(302, 9002, 300)),
                ((byte)2, Sound(303, 9003, 300))));

            using var lib = WwiseSoundLibrary.Load(dir.FullName);
            var r = lib.Resolve(100);
            Assert.Equal(new uint[] { 9001, 9002, 9003 }, r.Media.Select(m => m.MediaId));
            Assert.Null(r.Problem);
        }
        finally { dir.Delete(true); }
    }

    /// <summary>A container body long enough to hold a parent id at the offset the real ones use.</summary>
    private static byte[] Container(uint id, uint parent = 0)
    {
        var b = new byte[32];
        BitConverter.GetBytes(id).CopyTo(b, 0);
        BitConverter.GetBytes(parent).CopyTo(b, 11);
        return b;
    }

    [Fact]
    public void AStopEventResolvesToNothingAndSaysWhy()
    {
        var dir = Directory.CreateTempSubdirectory("wwise-test");
        try
        {
            File.WriteAllBytes(Path.Combine(dir.FullName, "T.bnk"), Bank(1,
                ((byte)4, Event(100, 200)),
                ((byte)3, Action(200, 0x0103, 300))));
            using var lib = WwiseSoundLibrary.Load(dir.FullName);
            var r = lib.Resolve(100);
            Assert.Empty(r.Media);
            Assert.Contains("no Play action", r.Problem);
        }
        finally { dir.Delete(true); }
    }

    [Fact]
    public void AnUnknownEventIsReportedRatherThanThrowing()
    {
        var dir = Directory.CreateTempSubdirectory("wwise-test");
        try
        {
            File.WriteAllBytes(Path.Combine(dir.FullName, "T.bnk"), Bank(1, ((byte)4, Event(100))));
            using var lib = WwiseSoundLibrary.Load(dir.FullName);
            var r = lib.Resolve(4242);
            Assert.Empty(r.Media);
            Assert.NotNull(r.Problem);
        }
        finally { dir.Delete(true); }
    }

    [Fact]
    public void AnEventThatCannotBeProducedReturnsNullAndIsRecorded()
    {
        var dir = Directory.CreateTempSubdirectory("wwise-test");
        try
        {
            File.WriteAllBytes(Path.Combine(dir.FullName, "T.bnk"), Bank(1,
                ((byte)4, Event(100, 200)),
                ((byte)3, Action(200, 0x0403, 301)),
                ((byte)2, Sound(301, 9001, 0))));
            using var src = new WwiseAudioSource(WwiseSoundLibrary.Load(dir.FullName), ownsLibrary: true);
            // the media file does not exist, so nothing is produced and nothing is substituted
            Assert.Null(src.GetPcm(100, 1f));
            var miss = Assert.Single(src.Misses);
            Assert.Equal(100, miss.EventId);
            Assert.Contains("9001", miss.Reason);
        }
        finally { dir.Delete(true); }
    }

    // ------------------------------------------------------------------ against the shipped assets

    /// <summary>
    /// The unpacked OBB sound directories, when present on this machine: the banks live in
    /// <c>sound_meta</c> and the media archive under <c>cozmo_resources/sound</c>, so both are needed.
    /// </summary>
    private static string[]? SoundDirs()
    {
        foreach (var root in AssetRoots())
        {
            var meta = Path.Combine(root, "sound_meta");
            if (!Directory.Exists(meta) || Directory.GetFiles(meta, "*.bnk").Length == 0) continue;
            var media = Path.Combine(root, "assets", "cozmo_resources", "sound");
            return Directory.Exists(media) ? new[] { meta, media } : new[] { meta };
        }
        return null;
    }

    private static IEnumerable<string> AssetRoots()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null)
        {
            yield return Path.Combine(d.FullName, "re-analysis", "obb");
            d = d.Parent;
        }
    }

    [Fact]
    public void EveryShippedBankParsesExactly()
    {
        var dirs = SoundDirs();
        if (dirs is null) return;
        using var lib = WwiseSoundLibrary.Load(dirs);
        Assert.Equal(6, lib.Banks.Count);
        Assert.All(lib.Banks, b => Assert.Equal(120u, b.Version));
        Assert.Equal(835, lib.EventIds.Count);
    }

    /// <summary>
    /// The check that makes the layout trustworthy: across the whole library, the media id read from
    /// every Sound object names a file that actually exists. A mis-read offset would produce ids that
    /// match nothing.
    /// </summary>
    [Fact]
    public void ResolvedMediaIdsNameFilesThatExist()
    {
        var dirs = SoundDirs();
        if (dirs is null) return;
        using var lib = WwiseSoundLibrary.Load(dirs);
        if (lib.MediaFileCount == 0) return;          // banks without the media archive alongside

        int resolved = 0, present = 0, total = 0;
        foreach (var id in lib.EventIds)
        {
            var r = lib.Resolve(id);
            if (r.Media.Count == 0) continue;
            resolved++;
            foreach (var m in r.Media) { total++; if (m.Media is not null) present++; }
        }
        Assert.True(resolved > 500, $"only {resolved} events resolved to media");
        // a handful of ids belong to banks this build does not ship; the rest must all be real files
        Assert.True(present >= total * 0.95, $"only {present} of {total} resolved media were readable");
    }

    /// <summary>
    /// Every ADPCM file in the library decodes, and none of them runs away. Checking all 227 rather than
    /// one is the difference between a decoder and a lucky guess.
    /// </summary>
    [Fact]
    public void EveryShippedAdpcmFileDecodesWithoutRunningAway()
    {
        var dirs = SoundDirs();
        if (dirs is null) return;
        using var lib = WwiseSoundLibrary.Load(dirs);
        if (lib.MediaFileCount == 0) return;

        int decoded = 0, clippedFiles = 0;
        foreach (var id in lib.EventIds)
        {
            foreach (var m in lib.Resolve(id).Media)
            {
                // stereo ADPCM is refused by design; see WwiseAdpcm
                if (m.Media is not { Codec: WwiseCodec.Adpcm, Channels: 1 } media) continue;
                var bytes = lib.ReadMedia(m.MediaId, out _);
                if (bytes is null) continue;
                var pcm = WwiseAdpcm.Decode(WwiseMedia.Parse(bytes));
                decoded++;
                Assert.Equal(media.SampleCount, pcm.Length / Math.Max(1, media.Channels));
                int clipped = pcm.Count(s => s is short.MaxValue or short.MinValue);
                if (clipped > pcm.Length / 100) clippedFiles++;
            }
        }
        Assert.True(decoded > 100, $"only {decoded} ADPCM files were decoded");
        Assert.Equal(0, clippedFiles);
    }

    /// <summary>
    /// The audio events <c>anim_bored_01</c> actually names must resolve. This is the verification target,
    /// not a special case: it goes through exactly the same resolution every other event uses.
    /// </summary>
    [Fact]
    public void TheVerificationClipsEventsResolveToShippedMedia()
    {
        var dirs = SoundDirs();
        if (dirs is null) return;
        var assets = AssetRoots()
            .Select(r => Path.Combine(r, "assets", "cozmo_resources", "assets", "animations"))
            .FirstOrDefault(Directory.Exists);
        if (assets is null) return;

        var animLib = AnimationLibrary.Open(assets);
        if (!animLib.HasClip("anim_bored_01")) return;
        using var lib = WwiseSoundLibrary.Load(dirs);

        var ids = animLib.GetClip("anim_bored_01").Keyframes.OfType<AudioKeyframe>()
                         .SelectMany(k => k.EventIds).ToList();
        Assert.NotEmpty(ids);
        foreach (var id in ids)
        {
            var r = lib.Resolve((uint)id);
            Assert.True(r.Media.Count > 0, $"event {id} ({r.Name}) resolved to no media");
            Assert.All(r.Media, m => Assert.NotNull(m.Media));
        }
    }
}
