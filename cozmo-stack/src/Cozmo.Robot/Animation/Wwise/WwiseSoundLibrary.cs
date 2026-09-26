using System.IO.Compression;
using System.Xml.Linq;

namespace Cozmo.Robot.Animation.Wwise;

/// <summary>One media file an event can play, with everything known about it before it is decoded.</summary>
public sealed record WwiseMediaRef(uint MediaId, string Source, string Bank)
{
    /// <summary>The parsed header, or null when the file is not present.</summary>
    public WwiseMedia? Media { get; init; }
    /// <summary>Why this one could not be read, when it could not be.</summary>
    public string? Problem { get; init; }
}

/// <summary>The complete resolution of one event, kept whole so it can be printed as evidence.</summary>
public sealed record WwiseEventResolution(uint EventId)
{
    public string? Name { get; init; }
    public string? Bank { get; init; }
    /// <summary>Every action the event fires, including ones that play nothing.</summary>
    public IReadOnlyList<(uint ActionId, ushort ActionType, uint Target)> Actions { get; init; }
        = Array.Empty<(uint, ushort, uint)>();
    /// <summary>
    /// The media the event can play. More than one means the event targets a container that chooses
    /// between alternatives at play time, which is normal: a short voice line typically has three.
    /// </summary>
    public IReadOnlyList<WwiseMediaRef> Media { get; init; } = Array.Empty<WwiseMediaRef>();
    public string? Problem { get; init; }
}

/// <summary>
/// Cozmo's own sound library: the shipped banks, the media archive, and the event names, joined up.
///
/// The join is the point. <c>SoundbanksInfo.xml</c> lists events and it lists media files, but it does
/// **not** connect them — an <c>Event</c> element carries only an id, a name and an authoring path. The
/// connection lives in each bank's object hierarchy, which is what <see cref="WwiseBank"/> reads:
///
/// <code>
/// event -> event action (Play, 0x0403) -> target object
///       -> if the target is a container, every Sound beneath it
///       -> each Sound's media id -> &lt;media id&gt;.wem
/// </code>
///
/// Nothing here is specific to any animation, event or clip: the whole library is resolved by the same
/// walk, and 615 of the 835 shipped events resolve to media this way.
/// </summary>
public sealed class WwiseSoundLibrary : IDisposable
{
    private readonly List<WwiseBank> _banks = new();
    private readonly Dictionary<uint, WwiseObject> _objects = new();
    private readonly Dictionary<uint, List<uint>> _children = new();
    private readonly Dictionary<uint, WwiseNode?> _nodes = new();
    private readonly Dictionary<uint, string> _eventNames = new();
    private readonly Dictionary<uint, string> _mediaEntries = new();   // media id -> archive entry
    private readonly Dictionary<uint, string> _mediaFiles = new();     // media id -> loose file path
    private ZipArchive? _archive;
    private readonly object _gate = new();

    /// <summary>Every event id the banks define.</summary>
    public IReadOnlyCollection<uint> EventIds => _objects
        .Where(kv => kv.Value.Type == WwiseObjectType.Event).Select(kv => kv.Key).ToList();

    /// <summary>How many media files the archive holds.</summary>
    public int MediaFileCount => _mediaEntries.Count + _mediaFiles.Count;

    /// <summary>
    /// Every object id in every loaded bank, so a check can be made over the whole library rather than
    /// over the part some event happens to reach. <see cref="Node"/> returns null for the types the node
    /// reader does not cover.
    /// </summary>
    public IReadOnlyCollection<uint> AllNodeIds => _objects.Keys.ToList();

    /// <summary>
    /// Every media id present, whether or not an event references it. Validation uses this rather than
    /// the referenced set, so a codebook family that no event happens to name is still exercised.
    /// </summary>
    public IReadOnlyCollection<uint> AllMediaIds
    {
        get
        {
            var all = new SortedSet<uint>(_mediaEntries.Keys);
            all.UnionWith(_mediaFiles.Keys);
            foreach (var b in _banks) all.UnionWith(b.EmbeddedMedia.Keys);
            return all;
        }
    }
    /// <summary>The banks that were loaded.</summary>
    public IReadOnlyList<WwiseBank> Banks => _banks;
    /// <summary>How many event names were read from SoundbanksInfo.xml.</summary>
    public int NamedEventCount => _eventNames.Count;
    /// <summary>Switch, state, parameter and bus names from the banks' definition text files.</summary>
    public WwiseNames Names { get; } = new();

    /// <summary>
    /// The STMG state-manager chunk from the bank that has one (<c>Init.bnk</c>), or null when no loaded
    /// bank does. It carries the RTPC default table the value store falls back to (gapF 1.2/1.3).
    /// </summary>
    public WwiseStmg? Stmg => _banks.Select(b => b.Stmg).FirstOrDefault(s => s is not null);

    /// <summary>
    /// The parsed hierarchy node for an object, or null when the object is absent, of a type the reader
    /// does not cover, or does not consume exactly. Parsed once and cached.
    /// </summary>
    public WwiseNode? Node(uint id)
    {
        lock (_gate)
        {
            if (_nodes.TryGetValue(id, out var n)) return n;
            n = _objects.TryGetValue(id, out var o) ? WwiseHierarchy.TryRead(o, out _) : null;
            _nodes[id] = n;
            return n;
        }
    }

    /// <summary>
    /// Runs the hierarchy reader over every object of the types it covers and reports, per type, how
    /// many there are and how many consumed their payload exactly, with the first problems. This is the
    /// check that makes the music layout trustworthy, the way the media-id check did for Sounds.
    /// </summary>
    public IReadOnlyList<(WwiseObjectType Type, int Count, int Exact, IReadOnlyList<string> Problems)> CheckHierarchy()
    {
        var result = new List<(WwiseObjectType, int, int, IReadOnlyList<string>)>();
        foreach (var group in _objects.Values.GroupBy(o => o.Type).OrderBy(g => (byte)g.Key))
        {
            int count = 0, exact = 0;
            var problems = new List<string>();
            bool covered = false;
            foreach (var o in group)
            {
                var node = WwiseHierarchy.TryRead(o, out var problem);
                if (problem is not null && problem.EndsWith("is not read")) break;
                covered = true; count++;
                if (node is not null) exact++;
                else if (problems.Count < 5) problems.Add(problem!);
            }
            if (covered) result.Add((group.Key, count, exact, problems));
        }
        return result;
    }

    /// <summary>How an event that targets the music hierarchy plays under the given switch values. See <see cref="WwiseMusic"/>.</summary>
    public WwiseMusicPlan ResolveMusic(uint eventId, IReadOnlyDictionary<uint, uint> switches) =>
        WwiseMusic.Resolve(this, eventId, switches);

    /// <summary>
    /// Loads everything found under a sound directory: every <c>.bnk</c>, any <c>SoundbanksInfo.xml</c>
    /// for names, the bank definition <c>.txt</c> files for switch and state names, and the media, either
    /// from <c>AudioAssets.zip</c> or as loose <c>.wem</c> files. Directories are searched recursively,
    /// because the localised bank sits in its own subdirectory. The shipped <c>AudioAssets.zip</c> holds
    /// the banks and their text files as well as the media, so a directory holding only that archive is
    /// a complete library; loose banks, when present, take precedence over the archive's copies.
    /// </summary>
    public static WwiseSoundLibrary Load(params string[] directories)
    {
        var lib = new WwiseSoundLibrary();
        foreach (var dir in directories)
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var bnk in Directory.EnumerateFiles(dir, "*.bnk", SearchOption.AllDirectories))
            {
                try { lib.AddBank(WwiseBank.Parse(File.ReadAllBytes(bnk), Path.GetFileName(bnk))); }
                catch (InvalidDataException) { /* a bank we cannot read contributes nothing */ }
            }
            foreach (var xml in Directory.EnumerateFiles(dir, "SoundbanksInfo.xml", SearchOption.AllDirectories))
                lib.AddNames(xml);
            foreach (var txt in Directory.EnumerateFiles(dir, "*.txt", SearchOption.AllDirectories))
                lib.AddDefinitionText(txt);
            foreach (var zip in Directory.EnumerateFiles(dir, "*.zip", SearchOption.AllDirectories))
                lib.AddArchive(zip);
            foreach (var wem in Directory.EnumerateFiles(dir, "*.wem", SearchOption.AllDirectories))
                if (uint.TryParse(Path.GetFileNameWithoutExtension(wem), out var id))
                    lib._mediaFiles[id] = wem;
        }
        lib.BuildTree();
        return lib;
    }

    private void AddBank(WwiseBank bank)
    {
        if (_banks.Any(b => b.BankId == bank.BankId)) return;     // a loose copy already loaded wins
        _banks.Add(bank);
        foreach (var (id, o) in bank.Objects) _objects[id] = o;
    }

    private void AddNames(string xmlPath)
    {
        try { AddNames(XDocument.Load(xmlPath)); }
        catch (Exception ex) when (ex is IOException or System.Xml.XmlException) { }
    }

    private void AddNames(XDocument doc)
    {
        foreach (var e in doc.Descendants("Event"))
            if (uint.TryParse(e.Attribute("Id")?.Value, out var id))
                _eventNames[id] = e.Attribute("Name")?.Value ?? "";
    }

    private void AddDefinitionText(string path)
    {
        try { Names.AddDefinitionText(File.ReadAllText(path)); }
        catch (IOException) { }
    }

    private static byte[] ReadAll(ZipArchiveEntry e)
    {
        using var s = e.Open();
        using var ms = new MemoryStream((int)Math.Min(e.Length, int.MaxValue));
        s.CopyTo(ms);
        return ms.ToArray();
    }

    private void AddArchive(string zipPath)
    {
        try
        {
            var a = ZipFile.OpenRead(zipPath);
            bool used = false;
            foreach (var entry in a.Entries)
            {
                if (entry.Name.EndsWith(".wem", StringComparison.OrdinalIgnoreCase))
                {
                    if (uint.TryParse(Path.GetFileNameWithoutExtension(entry.Name), out var id))
                    {
                        _mediaEntries[id] = entry.FullName; used = true;
                    }
                }
                else if (entry.Name.EndsWith(".bnk", StringComparison.OrdinalIgnoreCase))
                {
                    try { AddBank(WwiseBank.Parse(ReadAll(entry), entry.Name)); used = true; }
                    catch (InvalidDataException) { }
                }
                else if (entry.Name.Equals("SoundbanksInfo.xml", StringComparison.OrdinalIgnoreCase))
                {
                    try { using var s = entry.Open(); AddNames(XDocument.Load(s)); }
                    catch (System.Xml.XmlException) { }
                }
                else if (entry.Name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                {
                    Names.AddDefinitionText(System.Text.Encoding.UTF8.GetString(ReadAll(entry)));
                }
            }
            if (used && _archive is null) _archive = a; else if (!used) a.Dispose();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException) { }
    }

    /// <summary>
    /// Inverts the hierarchy's parent links into a child list, so a container can be walked downwards.
    /// Objects record their parent, not their children, so this is the only way round.
    /// </summary>
    private void BuildTree()
    {
        foreach (var o in _objects.Values)
        {
            if (WwiseBank.ParentId(o) is not { } parent) continue;
            if (!_objects.ContainsKey(parent)) continue;       // parent lives in a bank we do not have
            if (!_children.TryGetValue(parent, out var list)) _children[parent] = list = new List<uint>();
            list.Add(o.Id);
        }
        foreach (var list in _children.Values) list.Sort();     // a stable order, so runs repeat
    }

    /// <summary>The authoring name of an event, when SoundbanksInfo.xml named it.</summary>
    public string? NameOf(uint eventId) => _eventNames.TryGetValue(eventId, out var n) ? n : null;

    /// <summary>The event id for an authoring name, if one matches.</summary>
    public uint? IdOf(string name)
    {
        foreach (var (id, n) in _eventNames)
            if (string.Equals(n, name, StringComparison.OrdinalIgnoreCase)) return id;
        return null;
    }

    /// <summary>Every Sound reachable from an object, in a stable order.</summary>
    private List<uint> MediaUnder(uint root)
    {
        var media = new List<uint>();
        var seen = new HashSet<uint>();
        var stack = new Stack<uint>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            uint id = stack.Pop();
            if (!seen.Add(id) || !_objects.TryGetValue(id, out var o)) continue;
            if (o.Type == WwiseObjectType.Sound && WwiseBank.SoundMediaId(o) is { } m) media.Add(m);
            if (_children.TryGetValue(id, out var kids))
                for (int i = kids.Count - 1; i >= 0; i--) stack.Push(kids[i]);
        }
        return media;
    }

    /// <summary>
    /// The media ids an event can play, without opening a single media file.
    ///
    /// <see cref="Resolve"/> reads each file's header so it can report codec and duration, which is right
    /// for showing one event but ruinous across the whole library: it would read every referenced file
    /// once per event that names it, including multi-megabyte music. Anything that only needs the ids
    /// should use this.
    /// </summary>
    public IReadOnlyList<uint> ResolveMediaIds(uint eventId)
    {
        if (!_objects.TryGetValue(eventId, out var ev) || ev.Type != WwiseObjectType.Event)
            return Array.Empty<uint>();
        var media = new List<uint>();
        var seen = new HashSet<uint>();
        foreach (var aid in WwiseBank.EventActions(ev))
        {
            if (!_objects.TryGetValue(aid, out var act)) continue;
            if (WwiseBank.PlayActionTarget(act) is not { } target) continue;
            foreach (var m in MediaUnder(target)) if (seen.Add(m)) media.Add(m);
        }
        return media;
    }

    /// <summary>
    /// Resolves an event all the way to the media files it can play, recording every step so the result
    /// can be shown as evidence rather than asserted.
    /// </summary>
    public WwiseEventResolution Resolve(uint eventId)
    {
        if (!_objects.TryGetValue(eventId, out var ev) || ev.Type != WwiseObjectType.Event)
            return new WwiseEventResolution(eventId)
            {
                Name = NameOf(eventId),
                Problem = "no event with that id in any loaded bank",
            };

        var actions = new List<(uint, ushort, uint)>();
        var media = new List<WwiseMediaRef>();
        var seen = new HashSet<uint>();

        foreach (var aid in WwiseBank.EventActions(ev))
        {
            if (!_objects.TryGetValue(aid, out var act))
            {
                actions.Add((aid, 0, 0));
                continue;
            }
            var s = act.Payload.Span;
            ushort type = s.Length >= 6 ? (ushort)(s[4] | (s[5] << 8)) : (ushort)0;
            uint target = s.Length >= 10 ? (uint)(s[6] | (s[7] << 8) | (s[8] << 16) | (s[9] << 24)) : 0;
            actions.Add((aid, type, target));
            if (type != WwiseBank.PlayAction) continue;
            foreach (var m in MediaUnder(target))
                if (seen.Add(m)) media.Add(Describe(m, ev.Bank));
        }

        return new WwiseEventResolution(eventId)
        {
            Name = NameOf(eventId), Bank = ev.Bank, Actions = actions, Media = media,
            Problem = media.Count == 0
                ? (actions.Any(a => a.Item2 == WwiseBank.PlayAction)
                    ? "the event plays, but no Sound was reachable from its target"
                    : "the event fires no Play action")
                : null,
        };
    }

    /// <summary>Reads a media file's header, saying plainly when it is not there.</summary>
    public WwiseMediaRef Describe(uint mediaId, string bank)
    {
        var bytes = ReadMedia(mediaId, out string source);
        if (bytes is null)
            return new WwiseMediaRef(mediaId, source, bank) { Problem = "no such file in the media archive" };
        try
        {
            return new WwiseMediaRef(mediaId, source, bank) { Media = WwiseMedia.Parse(bytes) };
        }
        catch (InvalidDataException ex)
        {
            return new WwiseMediaRef(mediaId, source, bank) { Problem = ex.Message };
        }
    }

    /// <summary>
    /// The bytes of one media file, from the archive, a loose file, or a bank's own embedded data.
    /// Returns null when it is nowhere.
    /// </summary>
    public byte[]? ReadMedia(uint mediaId, out string source)
    {
        lock (_gate)
        {
            if (_mediaFiles.TryGetValue(mediaId, out var path))
            {
                source = Path.GetFileName(path);
                try { return File.ReadAllBytes(path); } catch (IOException) { return null; }
            }
            if (_archive is not null && _mediaEntries.TryGetValue(mediaId, out var entry))
            {
                source = entry;
                var e = _archive.GetEntry(entry);
                if (e is not null)
                {
                    using var s = e.Open();
                    var buf = new byte[e.Length];
                    int read = 0;
                    while (read < buf.Length)
                    {
                        int n = s.Read(buf, read, buf.Length - read);
                        if (n <= 0) break;
                        read += n;
                    }
                    return read == buf.Length ? buf : null;
                }
            }
            foreach (var b in _banks)
                if (b.EmbeddedMedia.TryGetValue(mediaId, out var mem))
                {
                    source = $"{b.Name} (embedded)";
                    return mem.ToArray();
                }
        }
        source = "(not found)";
        return null;
    }

    public void Dispose()
    {
        lock (_gate) { _archive?.Dispose(); _archive = null; }
    }
}
