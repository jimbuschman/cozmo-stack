using Cozmo.Robot;
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
    /// <c>wwise &lt;sound-dir&gt; --music &lt;id-or-name&gt; [--switch Group=State ...] [--midi]</c>
    /// <c>wwise &lt;sound-dir&gt; --hierarchy</c>
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

        if (Arg(a, "--decode") is { } one) return DecodeOne(lib, one, Arg(a, "--ogg"));
        if (a.Contains("--validate")) return Validate(lib, limit);
        if (a.Contains("--hierarchy")) return Hierarchy(lib);
        if (a.Contains("--sampler")) return Sampler(lib);
        if (Arg(a, "--music") is { } music) return Music(lib, Resolve(lib, music), Switches(lib, a), a.Contains("--midi"));
        int seed = int.TryParse(Arg(a, "--seed"), out var sd) ? sd : 1;
        if (Arg(a, "--render") is { } render) return Render(lib, Resolve(lib, render), Switches(lib, a), Arg(a, "--wav"), seed, BranchArg(a));
        if (a.Contains("--validate-music")) return ValidateMusic(lib, Arg(a, "--obb"), seed);
        if (ev is not null) return Report(lib, Resolve(lib, ev));
        if (clip is not null) return ForClip(lib, clip, assets);
        if (coverage) return Coverage(lib, limit);

        Console.WriteLine("\nnothing asked for. Use --event <id-or-name>, --clip <name> --assets <dir>, or --coverage");
        return 0;
    }

    private static uint? Resolve(WwiseSoundLibrary lib, string spec) =>
        uint.TryParse(spec, out var id) ? id : lib.IdOf(spec);

    /// <summary>Every <c>--switch Group=State</c> on the command line, by name (from the bank text files or the FNV-1 hash) or by id.</summary>
    private static Dictionary<uint, uint> Switches(WwiseSoundLibrary lib, string[] a)
    {
        var sw = new Dictionary<uint, uint>();
        for (int i = 0; i < a.Length - 1; i++)
        {
            if (a[i] != "--switch") continue;
            var parts = a[i + 1].Split('=', 2);
            if (parts.Length != 2) continue;
            sw[NameOrId(lib, parts[0])] = NameOrId(lib, parts[1]);
        }
        return sw;
    }

    private static uint NameOrId(WwiseSoundLibrary lib, string s) =>
        uint.TryParse(s, out var id) ? id : lib.Names.SwitchGroupId(s) ?? lib.Names.SwitchId(s) ?? WwiseHash.Of(s);

    /// <summary>
    /// Walks the singing sampler and prints what is actually under it: every layer, every per-key
    /// container with its MIDI key range and level, and every recording with the length it decodes to.
    ///
    /// The lengths are the point. The note-on layer's recordings are around five seconds each and carry
    /// Loop = 0, so how a held note ends decides whether a quarter note at 160 bpm sounds for its 375 ms
    /// or for five seconds. The note-off layer's are around six tenths of a second at -14 dB, which is a
    /// release tail. See WWISE_MUSIC.md and the fidelity manifest, M9-010.
    /// </summary>
    private static int Sampler(WwiseSoundLibrary lib)
    {
        const uint target = 110896138;
        if (lib.Node(target) is not { } root) { Console.WriteLine($"node {target} is not readable"); return 1; }
        Console.WriteLine($"\nsinging sampler, MIDI target {target}");
        var totals = new Dictionary<string, (int Count, double TotalMs, double Min, double Max)>();

        void Walk(uint id, int depth, string layer)
        {
            if (lib.Node(id) is not { } n) return;
            string pad = new(' ', 2 + depth * 2);
            var p = n.Params;
            string keys = p.Raw(WwiseProp.MidiKeyRangeMin) is { } lo
                ? $" key {lo}..{p.Raw(WwiseProp.MidiKeyRangeMax)}" : "";
            string vol = p.Float(WwiseProp.Volume) is { } v ? $" {v:+0.#;-0.#;0} dB" : "";
            string pitch = p.Float(WwiseProp.Pitch) is { } c ? $" {c:+0;-0;0} cents" : "";
            string loop = p.Raw(WwiseProp.Loop) is { } l ? $" loop {(l == 0 ? "until stopped" : l.ToString())}" : "";
            string playOn = p.Raw(WwiseProp.MidiPlayOnNoteType) is { } po ? (po == 2 ? " on note-off" : " on note-on") : "";

            if (n is WwiseSoundNode s)
            {
                double ms = 0;
                var bytes = lib.ReadMedia(s.MediaId, out _);
                if (bytes is not null)
                {
                    try { var m = WwiseMedia.Parse(bytes); ms = m.SampleCount is { } n2 && m.SampleRate > 0 ? n2 * 1000.0 / m.SampleRate : 0; }
                    catch (InvalidDataException) { }
                }
                Console.WriteLine($"{pad}sound {s.Id} media {s.MediaId}{vol}{pitch}{loop}  {ms / 1000:F2} s");
                var t = totals.GetValueOrDefault(layer, (0, 0, double.MaxValue, 0));
                totals[layer] = (t.Count + 1, t.TotalMs + ms, Math.Min(t.Min, ms), Math.Max(t.Max, ms));
                return;
            }

            string kind = n.Type.ToString();
            if (n is WwiseRandomSequenceNode rs) kind = rs.IsSequence ? "sequence" : "random";
            Console.WriteLine($"{pad}{kind} {id}{keys}{vol}{pitch}{playOn}{loop}");
            foreach (var kid in n.Children) Walk(kid, depth + 1, layer);
        }

        static string LayerName(uint child) => child switch
        {
            462443456 => "note-on", 774902407 => "note-off", 403781184 => "get-in", _ => child.ToString(),
        };

        foreach (var c in root.Children) Walk(c, 0, LayerName(c));
        Console.WriteLine("\nrecordings per layer");
        foreach (var (layer, t) in totals.OrderBy(k => k.Key))
            Console.WriteLine($"  {layer,-10} {t.Count,4} recordings, {t.Min / 1000:F2}..{t.Max / 1000:F2} s, mean {t.TotalMs / t.Count / 1000:F2} s");
        return 0;
    }

    /// <summary>Runs the hierarchy reader over every object and prints, per type, how many consumed exactly.</summary>
    private static int Hierarchy(WwiseSoundLibrary lib)
    {
        Console.WriteLine("\nhierarchy objects consumed exactly by the node reader");
        bool ok = true;
        foreach (var (type, count, exact, problems) in lib.CheckHierarchy())
        {
            Console.WriteLine($"  {type,-26} {exact,5} of {count,5}");
            foreach (var p in problems) Console.WriteLine($"      {p}");
            ok &= exact == count;
        }
        Console.WriteLine($"  names read from the bank text files: {lib.Names.Count}");
        return ok ? 0 : 1;
    }

    /// <summary>Renders one music event offline, reports what the sampler did, and optionally writes the PCM as a WAV.</summary>
    private static int Render(WwiseSoundLibrary lib, uint? id, IReadOnlyDictionary<uint, uint> switches, string? wav, int seed, uint? without)
    {
        if (id is null) { Console.WriteLine("no event with that id or name"); return 1; }
        using var source = new WwiseAudioSource(lib, ownsLibrary: false, random: new Random(seed));
        if (without is not null)
        {
            source.ExcludeBranches = new HashSet<uint> { without.Value };
            Console.WriteLine($"  leaving out branch {BranchName(without.Value)} of the MIDI target (see WWISE_MUSIC.md, M9-013)");
        }
        var r = source.RenderMusic(id.Value, switches);
        Console.WriteLine($"\nrendered {id} {lib.NameOf(id.Value) ?? ""}: {r.DurationMs:F0} ms, {r.Pcm.Length} samples at {CozmoAudio.SampleRate} Hz");
        Console.WriteLine($"  notes in window {r.NotesInWindow}, sung {r.NotesPlayed}, outside the voice's range {r.NotesSilent}, note-offs {r.NoteOffsPlayed}, audio clips {r.AudioClips}");
        Console.WriteLine($"  raw peak {r.PreLimitPeak:F0} of {short.MaxValue}; output stage gain {r.OutputGainDb:F1} dB (a stand-in for the robot bus limiter); clipped samples after it {r.ClippedSamples}");
        Console.WriteLine($"  modulator bindings acted on {r.ModulationsApplied}; deepest level change {r.ModulationPeakDb:F2} dB, largest pitch change {r.ModulationPeakCents:F0} cents");
        if (r.BusChain is { } bc)
        {
            Console.WriteLine($"  robot bus chain: {string.Join(" -> ", bc.Stages)}");
            Console.WriteLine($"  chain in {bc.InputPeak:F0}, out {bc.OutputPeak:F0}, deepest limiter reduction {bc.LimiterReductionDb:F1} dB");
            foreach (var nt in bc.Notes) Console.WriteLine($"    note: {nt}");
            foreach (var pr in bc.Problems) Console.WriteLine($"    problem: {pr}");
        }
        if (r.VoicesByBranch.Count > 0)
            Console.WriteLine("  voices by branch of the MIDI target: " +
                string.Join(", ", r.VoicesByBranch.OrderBy(k => k.Key).Select(k => $"{BranchName(k.Key)} {k.Value}")));
        foreach (var pr in r.Problems) Console.WriteLine($"  problem: {pr}");
        if (wav is not null && r.Pcm.Length > 0)
        {
            File.WriteAllBytes(wav, Wav(r.Pcm, CozmoAudio.SampleRate));
            Console.WriteLine($"  wrote {Path.GetFullPath(wav)}");
        }
        return r.Problems.Count == 0 && r.Pcm.Length > 0 ? 0 : 1;
    }

    /// <summary>
    /// Renders every shipped song and every other music event, so the whole music hierarchy is exercised
    /// rather than one song: the 39 Singing behaviours' songs when the OBB is given (their configs name
    /// the switches), the three tempo events on their default path, and every event whose Play target is
    /// music. Each row says how many notes were sung and how many fell outside the voice's range.
    /// </summary>
    private static int ValidateMusic(WwiseSoundLibrary lib, string? obb, int seed)
    {
        using var source = new WwiseAudioSource(lib, ownsLibrary: false, random: new Random(seed));
        int ok = 0, bad = 0, silentNotes = 0, sungNotes = 0;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine($"\n{"song / event",-48} {"ms",7} {"notes",6} {"sung",5} {"out",4} {"offs",5} {"rawpk",7} {"outpk",7} {"limit",6}  problems");

        void Row(string label, uint eventId, IReadOnlyDictionary<uint, uint> switches)
        {
            var r = source.RenderMusic(eventId, switches);
            bool good = r.Problems.Count == 0 && r.Pcm.Length > 0 && (r.NotesPlayed > 0 || r.AudioClips > 0);
            if (good) ok++; else bad++;
            silentNotes += r.NotesSilent; sungNotes += r.NotesPlayed;
            Console.WriteLine($"{label,-48} {r.DurationMs,7:F0} {r.NotesInWindow,6} {r.NotesPlayed,5} {r.NotesSilent,4} {r.NoteOffsPlayed,5} {r.PreLimitPeak,7:F0} {r.Peak,7} {(r.BusChain?.LimiterReductionDb ?? r.OutputGainDb),6:F1}  {string.Join("; ", r.Problems)}");
        }

        if (obb is not null)
        {
            foreach (var b in Cozmo.Robot.Behavior.SingingBehavior.LoadShipped(obb))
            {
                var ev = lib.IdOf("Play__Robot_VO__Cozmo_Singing_" + b.SwitchGroupName["Cozmo_Sings_".Length..].ToLowerInvariant());
                if (ev is null) { Console.WriteLine($"{b.Id,-48} no tempo event"); bad++; continue; }
                Row(b.Id, ev.Value, new Dictionary<uint, uint> { [b.SwitchGroupId] = b.SwitchId });
            }
        }
        var none = new Dictionary<uint, uint>();
        foreach (var id in lib.EventIds.OrderBy(x => lib.NameOf(x)))
        {
            if (!source.IsMusicEvent(id)) continue;
            Row(lib.NameOf(id) ?? id.ToString(), id, none);
        }
        Console.WriteLine($"\n{ok} rendered, {bad} did not; {sungNotes} notes sung, {silentNotes} outside the voice's range; {sw.Elapsed.TotalSeconds:F1} s");
        return bad == 0 ? 0 : 1;
    }

    private static byte[] Wav(short[] pcm, int rate)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        int dataBytes = pcm.Length * 2;
        w.Write("RIFF"u8); w.Write(36 + dataBytes); w.Write("WAVE"u8);
        w.Write("fmt "u8); w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16);
        w.Write("data"u8); w.Write(dataBytes);
        foreach (var v in pcm) w.Write(v);
        w.Flush();
        return ms.ToArray();
    }

    /// <summary>Prints how a music event plays under the given switch values, down to each clip's source.</summary>
    private static int Music(WwiseSoundLibrary lib, uint? id, IReadOnlyDictionary<uint, uint> switches, bool showMidi)
    {
        if (id is null) { Console.WriteLine("no event with that id or name"); return 1; }
        var plan = lib.ResolveMusic(id.Value, switches);
        string Name(uint x) => lib.Names.NameOf(x) is { } n ? $"{x} ({n})" : x.ToString();
        Console.WriteLine($"\nevent {plan.EventId} {plan.EventName ?? ""}");
        Console.WriteLine($"  Play target {plan.TargetId} type {plan.TargetType}");
        if (plan.Switch is { } sw)
        {
            Console.WriteLine($"  music switch: tempo {sw.Meter.TempoBpm} bpm, grid {sw.Meter.GridPeriodMs} ms, " +
                              $"arguments [{string.Join(", ", sw.Arguments.Select(g => Name(g.GroupId)))}], " +
                              $"{sw.Tree.Count} tree nodes, MIDI target {(sw.MidiTargetNode is { } m ? Name(m) : "none")}");
            foreach (var g in sw.Arguments)
                Console.WriteLine($"    current value of {Name(g.GroupId)}: {(switches.TryGetValue(g.GroupId, out var v) ? Name(v) : "not set (key 0 path)")}");
            Console.WriteLine($"  selected node {(plan.SelectedNodeId is { } s ? s.ToString() : "none")}");
        }
        if (plan.Playlist is { } pl)
            Console.WriteLine($"  playlist {pl.Id}: {pl.Playlist.Count} items, {pl.Children.Count} segments" + (plan.HasRandomChoice ? " (random choice; one alternative shown)" : ""));
        foreach (var seg in plan.Segments)
        {
            Console.WriteLine($"  segment {seg.SegmentId}: {seg.DurationMs} ms, plays at {seg.TempoBpm} bpm" +
                              (seg.Meter.OverridesParent ? "" : $" (inherited; its own meter says {seg.Meter.TempoBpm})"));
            foreach (var c in seg.Clips)
            {
                var kind = c.IsMidi ? "MIDI" : lib.Describe(c.SourceId, "").Media?.Codec.ToString() ?? "missing";
                Console.WriteLine($"    track {c.TrackId} source {c.SourceId} [{kind}] play at {c.Clip.PlayAtMs} ms, " +
                                  $"trim {c.Clip.BeginTrimMs}..{c.Clip.EndTrimMs} of {c.Clip.SourceDurationMs} ms -> {c.Clip.LengthMs} ms");
                if (c.IsMidi && showMidi && lib.ReadMedia(c.SourceId, out _) is { } bytes)
                {
                    var midi = WwiseMidi.Parse(bytes);
                    Console.WriteLine($"      {midi.TicksPerBeat} ticks per beat, file tempo {midi.FileTempoBpm}, end tick {midi.EndTick}, {midi.Notes.Count} notes");
                    foreach (var n in midi.NotesAt(plan.TempoBpm).Take(12))
                        Console.WriteLine($"        {n.StartMs,9:F1} ms  key {n.Key,3}  vel {n.Velocity,3}  {n.LengthMs,8:F1} ms");
                    if (midi.Notes.Count > 12) Console.WriteLine($"        ... {midi.Notes.Count - 12} more");
                }
            }
        }
        if (plan.Problem is not null) Console.WriteLine($"  problem: {plan.Problem}");
        return plan.Problem is null ? 0 : 1;
    }

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
            Console.WriteLine($"  -> {r.Media.Count} recordings are reachable from the target");

        // What one play actually does, as the containers say: which child a random draws, the order a
        // sequence plays its items in, which branch a switch selects. See WwisePlayback.
        var plan = WwisePlayback.Resolve(lib, id.Value, new Dictionary<uint, uint>(), new Random(1),
                                         new Dictionary<uint, int>(), new Dictionary<uint, uint>());
        Console.WriteLine("  -> one play, with no switches set and a fixed draw:");
        PrintPlan(plan.Root, 5);
        foreach (var p in plan.Problems) Console.WriteLine($"     !! {p}");

        foreach (var m in r.Media)
        {
            Console.WriteLine($"  -> media id {m.MediaId}");
            Console.WriteLine($"     -> {m.Source}");
            if (m.Media is not { } md) { Console.WriteLine($"     !! {m.Problem}"); continue; }
            string dur = md.Duration is { } d ? $"{d.TotalSeconds:F2}s" : "unknown";
            Console.WriteLine($"     -> codec {Describe(md)}  {md.Channels}ch {md.SampleRate}Hz  duration {dur}");
            Console.WriteLine($"     -> {(md.IsDecodable ? "decodes here" : $"NOT DECODED: {md.UndecodableReason}")}");
        }
        return 0;
    }

    /// <summary><c>--without-branch get-in|note-on|note-off|&lt;id&gt;</c>: a child of the MIDI target to leave out.</summary>
    private static uint? BranchArg(string[] a) => Arg(a, "--without-branch") switch
    {
        null => null,
        "get-in" => 403781184u,
        "note-on" => 462443456u,
        "note-off" => 774902407u,
        var s2 when uint.TryParse(s2, out var v) => v,
        _ => null,
    };

    /// <summary>The three children of the singing sampler, by the names the Wwise project gives them.</summary>
    private static string BranchName(uint id) => id switch
    {
        462443456 => "note-on", 774902407 => "note-off", 403781184 => "get-in", _ => id.ToString(),
    };

    /// <summary>Prints one play plan: what sounds, in what order, and what starts together.</summary>
    private static void PrintPlan(WwisePlayNode? node, int indent)
    {
        string pad = new(' ', indent);
        switch (node)
        {
            case null:
                Console.WriteLine($"{pad}(nothing)");
                break;
            case WwisePlaySound s:
                string level = s.GainDb != 0 ? $" {s.GainDb:+0.#;-0.#} dB" : "";
                string pitch = s.Cents != 0 ? $" {s.Cents:+0;-0} cents" : "";
                Console.WriteLine($"{pad}sound {s.SoundId} media {s.MediaId}{level}{pitch}");
                break;
            case WwisePlaySequence q:
                Console.WriteLine($"{pad}then, one after another:");
                foreach (var p in q.Parts) PrintPlan(p, indent + 2);
                break;
            case WwisePlayTogether t:
                Console.WriteLine($"{pad}all at once:");
                foreach (var p in t.Parts) PrintPlan(p, indent + 2);
                break;
        }
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
                bool ok = r.Media.Any(m => m.Media?.IsDecodable == true);
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
        var undecodable = new List<string>();

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
                if (m.Media?.IsDecodable == true) any = true;
            }
            if (any || r.Media.Any(m => m.Media?.IsDecodable == true)) playable++;
            else
            {
                // An event that resolves to media and cannot play any of it: the honest failure to name,
                // because a percentage hides which events are silent and why.
                var why = r.Media.Select(m => m.Media?.UndecodableReason ?? m.Problem ?? "unreadable").Distinct();
                undecodable.Add($"{id} {r.Name ?? ""}: {string.Join("; ", why)}");
            }
        }

        // The events M6 could not resolve because their Play target is in the music hierarchy: count the
        // ones the music resolver now carries to a clip source (audio or MIDI), with no switch values set.
        int musicResolved = 0;
        var noSwitches = new Dictionary<uint, uint>();
        foreach (var id in lib.EventIds)
        {
            var r = lib.Resolve(id);
            if (r.Media.Count > 0 || !r.Actions.Any(a => a.ActionType == 0x0403)) continue;
            var plan = lib.ResolveMusic(id, noSwitches);
            if (plan.Problem is null && plan.Segments.Any(s => s.Clips.Count > 0)) musicResolved++;
        }

        Console.WriteLine($"\ncoverage across the whole shipped library");
        int shouldPlay = events - playsNothing;
        Console.WriteLine($"  events in banks               {events}");
        Console.WriteLine($"  of those, Stop/Pause/Resume   {playsNothing}  (correctly play nothing)");
        Console.WriteLine($"  events that should play       {shouldPlay}");
        Console.WriteLine($"  resolved to media             {resolved}  ({100.0 * resolved / Math.Max(1, shouldPlay):F1}% of those)");
        Console.WriteLine($"  have a decodable alternative  {playable}  ({100.0 * playable / Math.Max(1, shouldPlay):F1}% of those)");
        Console.WriteLine($"  music events reaching a clip  {musicResolved}  (through the music hierarchy, default switch path)");
        Console.WriteLine($"  distinct media referenced     {mediaSeen.Count} of {lib.MediaFileCount} on disk");
        foreach (var (c, n) in perCodec.OrderByDescending(kv => kv.Value))
            Console.WriteLine($"    {c,-8} {n,5}");
        if (undecodable.Count > 0)
        {
            Console.WriteLine($"\n  {undecodable.Count} events resolve to media and can play none of it:");
            foreach (var u in undecodable.Take(limit)) Console.WriteLine($"    {u}");
        }
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
    private static int DecodeOne(WwiseSoundLibrary lib, string spec, string? oggOut)
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
                if (oggOut is not null) { File.WriteAllBytes(oggOut, ogg); Console.WriteLine($"  wrote {oggOut}"); }
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
