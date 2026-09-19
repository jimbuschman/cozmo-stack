using Cozmo.Robot.Animation;
using Cozmo.Robot.Animation.Wwise;

namespace Cozmo.Conformance;

/// <summary>
/// Offline diagnostics for Cozmo's own sound library: what an audio event resolves to, and how much of the
/// shipped library is playable. No robot involved.
/// </summary>
public static class WwiseTool
{
    /// <summary>
    /// <c>wwise &lt;sound-dir&gt; [--event &lt;id-or-name&gt;] [--clip &lt;name&gt; --assets &lt;dir&gt;] [--coverage] [--limit N]</c>
    /// </summary>
    public static int Run(string[] a)
    {
        var dir = a.Length > 1 && !a[1].StartsWith("--") ? a[1] : null;
        if (dir is null)
        {
            Console.WriteLine("need a sound directory, e.g. the OBB's cozmo_resources/sound or sound_meta");
            return 1;
        }
        var ev = Arg(a, "--event");
        var clip = Arg(a, "--clip");
        var assets = Arg(a, "--assets");
        bool coverage = a.Contains("--coverage");
        int limit = int.TryParse(Arg(a, "--limit"), out var l) ? l : 20;

        using var lib = WwiseSoundLibrary.Load(dir);
        Console.WriteLine($"loaded {lib.Banks.Count} banks, {lib.EventIds.Count} events, " +
                          $"{lib.NamedEventCount} named, {lib.MediaFileCount} media files");
        foreach (var b in lib.Banks)
            Console.WriteLine($"  {b.Name,-16} version {b.Version}  id {b.BankId}  " +
                              $"{b.Objects.Count} objects, {b.EmbeddedMedia.Count} embedded media");
        if (lib.Banks.Count == 0)
        {
            Console.WriteLine("no banks found: point this at the directory holding the .bnk files");
            return 1;
        }

        if (Arg(a, "--decode") is { } one) return DecodeOne(lib, one);
        if (a.Contains("--validate")) return Validate(lib, limit);
        if (ev is not null) return Report(lib, Resolve(lib, ev));
        if (clip is not null) return ForClip(lib, clip, assets);
        if (coverage) return Coverage(lib, limit);

        Console.WriteLine("\nnothing asked for. Use --event <id-or-name>, --clip <name> --assets <dir>, or --coverage");
        return 0;
    }

    private static uint? Resolve(WwiseSoundLibrary lib, string spec) =>
        uint.TryParse(spec, out var id) ? id : lib.IdOf(spec);

    /// <summary>Prints the whole chain for one event, as evidence rather than as a claim.</summary>
    private static int Report(WwiseSoundLibrary lib, uint? id)
    {
        if (id is null) { Console.WriteLine("no event with that id or name"); return 1; }
        var r = lib.Resolve(id.Value);
        Console.WriteLine($"\nevent {r.EventId}");
        Console.WriteLine($"  {r.Name ?? "(no name in SoundbanksInfo.xml)"}");
        Console.WriteLine($"  -> bank {r.Bank ?? "(none)"}");
        foreach (var (aid, atype, target) in r.Actions)
        {
            string what = atype == 0x0403 ? "Play" : atype == 0 ? "missing" : $"0x{atype:X4} (not a Play)";
            Console.WriteLine($"  -> action {aid} {what} -> target {target}");
        }
        if (r.Media.Count == 0) { Console.WriteLine($"  !! {r.Problem}"); return 1; }
        if (r.Media.Count > 1)
            Console.WriteLine($"  -> {r.Media.Count} alternatives (a container chooses one at play time)");
        foreach (var m in r.Media)
        {
            Console.WriteLine($"  -> media id {m.MediaId}");
            Console.WriteLine($"     -> {m.Source}");
            if (m.Media is not { } md) { Console.WriteLine($"     !! {m.Problem}"); continue; }
            string dur = md.Duration is { } d ? $"{d.TotalSeconds:F2}s" : "unknown";
            Console.WriteLine($"     -> codec {Describe(md)}  {md.Channels}ch {md.SampleRate}Hz  duration {dur}");
            Console.WriteLine($"     -> {(md.Codec == WwiseCodec.Adpcm ? "decodes here" : "NOT DECODED by this build")}");
        }
        return 0;
    }

    private static string Describe(WwiseMedia m) => m.Codec switch
    {
        WwiseCodec.Adpcm => "ADPCM (IMA, 4-bit)",
        WwiseCodec.Vorbis => $"Wwise Vorbis (setup {m.Vorbis?.SetupPacketLength ?? 0} bytes, " +
                             $"blocksizes 2^{m.Vorbis?.BlockSize0Pow}/2^{m.Vorbis?.BlockSize1Pow})",
        _ => $"unknown (format tag 0x{m.FormatTag:X4})",
    };

    /// <summary>Resolves every audio event an animation clip actually names.</summary>
    private static int ForClip(WwiseSoundLibrary lib, string clipName, string? assets)
    {
        if (assets is null) { Console.WriteLine("--clip also needs --assets <animation-dir>"); return 1; }
        var animLib = AnimationLibrary.Open(assets);
        if (!animLib.HasClip(clipName)) { Console.WriteLine($"no clip '{clipName}'"); return 1; }
        var clip = animLib.GetClip(clipName);
        var events = clip.Keyframes.OfType<AudioKeyframe>().ToList();
        Console.WriteLine($"\nclip '{clipName}': {events.Count} audio keyframes");
        int playable = 0, total = 0;
        foreach (var k in events)
        {
            Console.WriteLine($"\n  at {k.TriggerTimeMs} ms, {k.EventIds.Length} alternative event id(s), volume {k.Volume:F2}");
            foreach (var id in k.EventIds)
            {
                total++;
                if (id is < 0 or > uint.MaxValue) { Console.WriteLine($"    event {id}: out of range"); continue; }
                var r = lib.Resolve((uint)id);
                var codecs = r.Media.Select(m => m.Media?.Codec).ToList();
                bool ok = codecs.Any(c => c == WwiseCodec.Adpcm);
                if (ok) playable++;
                Console.WriteLine($"    event {id} {r.Name ?? "(unnamed)"}");
                Console.WriteLine($"      {r.Media.Count} media, codecs: " +
                                  string.Join(", ", codecs.Select(c => c?.ToString() ?? "unreadable")) +
                                  (ok ? "  -> PLAYABLE" : "  -> not decodable by this build"));
            }
        }
        Console.WriteLine($"\n{playable} of {total} event references are playable from shipped assets");
        return 0;
    }

    /// <summary>
    /// How much of the whole shipped library resolves and decodes. This is the number that says where the
    /// work actually stands, rather than one clip's worth of anecdote.
    /// </summary>
    private static int Coverage(WwiseSoundLibrary lib, int limit)
    {
        int events = 0, resolved = 0, playable = 0, playsNothing = 0;
        var perCodec = new Dictionary<WwiseCodec, int>();
        var mediaSeen = new HashSet<uint>();
        var unresolved = new List<string>();

        foreach (var id in lib.EventIds.OrderBy(x => x))
        {
            events++;
            var r = lib.Resolve(id);
            if (r.Media.Count == 0)
            {
                // An event with no Play action is a Stop, Pause or Resume. Resolving it to no media is the
                // right answer, not a gap, so it is counted apart rather than held against coverage.
                if (!r.Actions.Any(a => a.ActionType == 0x0403)) { playsNothing++; continue; }
                if (unresolved.Count < limit) unresolved.Add($"{id} {r.Name ?? ""}: {r.Problem}");
                continue;
            }
            resolved++;
            bool any = false;
            foreach (var m in r.Media)
            {
                if (!mediaSeen.Add(m.MediaId)) continue;
                var c = m.Media?.Codec ?? WwiseCodec.Unknown;
                perCodec[c] = perCodec.GetValueOrDefault(c) + 1;
                if (c == WwiseCodec.Adpcm) any = true;
            }
            if (any || r.Media.Any(m => m.Media?.Codec == WwiseCodec.Adpcm)) playable++;
        }

        Console.WriteLine($"\ncoverage across the whole shipped library");
        int shouldPlay = events - playsNothing;
        Console.WriteLine($"  events in banks               {events}");
        Console.WriteLine($"  of those, Stop/Pause/Resume   {playsNothing}  (correctly play nothing)");
        Console.WriteLine($"  events that should play       {shouldPlay}");
        Console.WriteLine($"  resolved to media             {resolved}  ({100.0 * resolved / Math.Max(1, shouldPlay):F1}% of those)");
        Console.WriteLine($"  have a decodable alternative  {playable}  ({100.0 * playable / Math.Max(1, shouldPlay):F1}% of those)");
        Console.WriteLine($"  distinct media referenced     {mediaSeen.Count} of {lib.MediaFileCount} on disk");
        foreach (var (c, n) in perCodec.OrderByDescending(kv => kv.Value))
            Console.WriteLine($"    {c,-8} {n,5}  {(c == WwiseCodec.Adpcm ? "decoded" : "not decoded")}");
        if (unresolved.Count > 0)
        {
            Console.WriteLine($"\n  first {unresolved.Count} events that resolve to nothing:");
            foreach (var u in unresolved) Console.WriteLine($"    {u}");
        }
        return 0;
    }

    /// <summary>
    /// Decodes every media file in the library and reports what happened, grouped by reason and by
    /// codebook set. This is the check that matters: one clip decoding proves nothing, and a percentage
    /// that improves because a whole codebook family was skipped would be worse than useless, so failures
    /// are counted and named rather than filtered out.
    /// </summary>
    private static int Validate(WwiseSoundLibrary lib, int limit)
    {
        var codebooks = WwiseAudioSource.TryLoadCodebooks();
        Console.WriteLine($"\ncodebooks: {(codebooks is null ? "NOT FOUND" : $"{codebooks.Count} entries")}");
        if (codebooks is null)
        {
            Console.WriteLine($"  {WwiseAudioSource.CodebookFileName} must be alongside the binary or in third-party/ww2ogg");
            return 1;
        }

        // Every media file the banks reference, not just those an event resolves to.
        // Every media file present, not only those an event names: a codebook family that no event
        // happens to reference would otherwise go untested, and a percentage that looks better because a
        // whole family was skipped is worse than no percentage at all.
        var referenced = new HashSet<uint>();
        foreach (var id in lib.EventIds)
            foreach (var m in lib.ResolveMediaIds(id)) referenced.Add(m);
        var media = new SortedSet<uint>(lib.AllMediaIds);
        media.UnionWith(referenced);
        Console.WriteLine($"decoding all {media.Count} media files " +
                          $"({referenced.Count} of them referenced by an event), this takes a minute...");

        int vorbisTotal = 0, rebuilt = 0, decoded = 0, adpcmTotal = 0, adpcmOk = 0, missing = 0, other = 0;
        var failReasons = new Dictionary<string, int>();
        var failByUid = new Dictionary<uint, int>();
        var okByUid = new Dictionary<uint, int>();
        var rates = new Dictionary<(int Rate, int Ch), int>();
        double totalSeconds = 0;
        int clippedFiles = 0, emptyFiles = 0;
        var examples = new List<string>();

        foreach (var mid in media)
        {
            var bytes = lib.ReadMedia(mid, out _);
            if (bytes is null) { missing++; continue; }
            WwiseMedia parsed;
            try { parsed = WwiseMedia.Parse(bytes); }
            catch (InvalidDataException ex) { other++; Bump(failReasons, $"header: {ex.Message}"); continue; }

            if (parsed.Codec == WwiseCodec.Adpcm)
            {
                adpcmTotal++;
                try { var p = WwiseAdpcm.Decode(parsed); adpcmOk++; Measure(p, parsed.Channels, parsed.SampleRate); }
                catch (InvalidDataException ex) { Bump(failReasons, $"adpcm: {ex.Message}"); }
                continue;
            }
            if (parsed.Codec != WwiseCodec.Vorbis) { other++; continue; }

            vorbisTotal++;
            uint uid = parsed.Vorbis?.Uid ?? 0;
            byte[] ogg;
            try { ogg = WwiseVorbisRebuilder.ToOgg(parsed, codebooks); rebuilt++; }
            catch (Exception ex)
            {
                Bump(failReasons, $"rebuild: {Short(ex.Message)}");
                Bump(failByUid, uid);
                if (examples.Count < limit) examples.Add($"{mid} (uid {uid}): rebuild: {ex.Message}");
                continue;
            }
            try
            {
                var v = WwiseVorbis.Decode(parsed, codebooks);
                decoded++;
                Bump(okByUid, uid);
                Measure(v.Samples, v.Channels, v.SampleRate);
                _ = ogg;
            }
            catch (Exception ex)
            {
                Bump(failReasons, $"decode: {Short(ex.Message)}");
                Bump(failByUid, uid);
                if (examples.Count < limit) examples.Add($"{mid} (uid {uid}): decode: {ex.Message}");
            }
        }

        void Measure(short[] pcm, int ch, int rate)
        {
            if (pcm.Length == 0) { emptyFiles++; return; }
            Bump(rates, (rate, ch));
            totalSeconds += pcm.Length / (double)Math.Max(1, ch) / Math.Max(1, rate);
            int clipped = 0;
            foreach (var s in pcm) if (s is short.MaxValue or short.MinValue) clipped++;
            if (clipped > pcm.Length / 100) clippedFiles++;
        }

        Console.WriteLine($"\nlibrary-wide decode of {media.Count} referenced media files");
        Console.WriteLine($"  Vorbis            {vorbisTotal}");
        Console.WriteLine($"    rebuilt to Ogg  {rebuilt}  ({Pct(rebuilt, vorbisTotal)})");
        Console.WriteLine($"    decoded by NVorbis {decoded}  ({Pct(decoded, vorbisTotal)})");
        Console.WriteLine($"  ADPCM             {adpcmTotal}");
        Console.WriteLine($"    decoded         {adpcmOk}  ({Pct(adpcmOk, adpcmTotal)})");
        Console.WriteLine($"  missing on disk   {missing}");
        Console.WriteLine($"  other/unreadable  {other}");

        Console.WriteLine($"\nsanity checks");
        Console.WriteLine($"  total decoded audio   {TimeSpan.FromSeconds(totalSeconds):hh\\:mm\\:ss}");
        Console.WriteLine($"  files >1% clipped     {clippedFiles}");
        Console.WriteLine($"  files decoding empty  {emptyFiles}");
        Console.WriteLine($"  decoded rate/channels:");
        foreach (var ((rate, ch), n) in rates.OrderByDescending(kv => kv.Value))
            Console.WriteLine($"    {rate,6} Hz {ch}ch  {n}");

        Console.WriteLine($"\nper codebook set (uid)");
        foreach (var uid in okByUid.Keys.Union(failByUid.Keys).OrderByDescending(u => okByUid.GetValueOrDefault(u)))
            Console.WriteLine($"  uid {uid,-12} ok {okByUid.GetValueOrDefault(uid),5}   failed {failByUid.GetValueOrDefault(uid),5}");

        if (failReasons.Count > 0)
        {
            Console.WriteLine($"\nfailures grouped by reason");
            foreach (var (r, n) in failReasons.OrderByDescending(kv => kv.Value))
                Console.WriteLine($"  {n,5}  {r}");
            Console.WriteLine($"\nfirst {examples.Count} failing files");
            foreach (var e in examples) Console.WriteLine($"  {e}");
        }
        return decoded == vorbisTotal && adpcmOk == adpcmTotal ? 0 : 1;
    }

    /// <summary>Decodes one media file by id and reports what came out, for checking a single case quickly.</summary>
    private static int DecodeOne(WwiseSoundLibrary lib, string spec)
    {
        if (!uint.TryParse(spec, out var mid)) { Console.WriteLine($"'{spec}' is not a media id"); return 1; }
        var bytes = lib.ReadMedia(mid, out var source);
        if (bytes is null) { Console.WriteLine($"media {mid} is not in the archive"); return 1; }
        var m = WwiseMedia.Parse(bytes);
        Console.WriteLine($"\nmedia {mid} from {source}");
        Console.WriteLine($"  {Describe(m)}  {m.Channels}ch {m.SampleRate}Hz  " +
                          $"declared duration {m.Duration?.TotalSeconds ?? 0:F2}s");

        short[] pcm; int ch, rate;
        try
        {
            if (m.Codec == WwiseCodec.Adpcm) { pcm = WwiseAdpcm.Decode(m); ch = m.Channels; rate = m.SampleRate; }
            else
            {
                var cbl = WwiseAudioSource.TryLoadCodebooks();
                if (cbl is null) { Console.WriteLine($"  !! {WwiseAudioSource.CodebookFileName} not found"); return 1; }
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var ogg = WwiseVorbisRebuilder.ToOgg(m, cbl);
                Console.WriteLine($"  rebuilt to {ogg.Length} bytes of Ogg in {sw.ElapsedMilliseconds} ms");
                if (Environment.GetEnvironmentVariable("COZMO_OGG_OUT") is { } oggOut)
                { File.WriteAllBytes(oggOut, ogg); Console.WriteLine($"  wrote {oggOut}"); return 0; }
                var v = WwiseVorbis.Decode(m, cbl);
                Console.WriteLine($"  NVorbis finished in {sw.ElapsedMilliseconds} ms");
                pcm = v.Samples; ch = v.Channels; rate = v.SampleRate;
            }
        }
        catch (Exception ex) { Console.WriteLine($"  !! {ex.Message}"); return 1; }

        int clipped = pcm.Count(s => s is short.MaxValue or short.MinValue);
        int peak = pcm.Length == 0 ? 0 : pcm.Max(s => Math.Abs((int)s));
        Console.WriteLine($"  decoded {pcm.Length} samples, {ch}ch {rate}Hz, " +
                          $"{pcm.Length / (double)Math.Max(1, ch) / Math.Max(1, rate):F2}s");
        Console.WriteLine($"  peak {peak}, clipped {clipped}");
        return 0;
    }

    private static string Pct(int n, int of) => of == 0 ? "n/a" : $"{100.0 * n / of:F1}%";
    private static string Short(string s) => s.Length <= 70 ? s : s[..70] + "...";
    private static void Bump<T>(Dictionary<T, int> d, T k) where T : notnull => d[k] = d.GetValueOrDefault(k) + 1;

    private static string? Arg(string[] a, string name)
    {
        for (int i = 1; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
        return null;
    }
}
