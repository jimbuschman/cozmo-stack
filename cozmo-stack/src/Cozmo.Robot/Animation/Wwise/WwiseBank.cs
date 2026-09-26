using System.Buffers.Binary;

namespace Cozmo.Robot.Animation.Wwise;

/// <summary>
/// The kinds of object a Wwise bank hierarchy holds. Only the ones this reader acts on are named; the
/// rest are carried by number so an unrecognised object is skipped rather than misread.
///
/// The numbering was established from the shipped banks themselves rather than assumed: the count of
/// type-4 objects in each bank equals that bank's <c>IncludedEvents</c> count in SoundbanksInfo.xml
/// exactly, in all six banks (546, 171, 83, 21, 14 and 0), and the number of type-3 objects equals the
/// total number of action references those events make (906). Neither would hold if the numbering were
/// wrong.
/// </summary>
public enum WwiseObjectType : byte
{
    Settings = 1,
    Sound = 2,
    EventAction = 3,
    Event = 4,
    RandomSequenceContainer = 5,
    SwitchContainer = 6,
    ActorMixer = 7,
    AudioBus = 8,
    BlendContainer = 9,
    MusicSegment = 10,
    MusicTrack = 11,
    MusicSwitchContainer = 12,
    MusicPlaylistContainer = 13,
    FxShareSet = 18,
    FxCustom = 19,
    LfoModulator = 21,
    EnvelopeModulator = 22,
}

/// <summary>One object in a bank's hierarchy, kept as its raw payload so nothing is lost in translation.</summary>
public sealed class WwiseObject
{
    public required uint Id { get; init; }
    public required WwiseObjectType Type { get; init; }
    /// <summary>The object body, starting at its id. Interpreted lazily and only where understood.</summary>
    public required ReadOnlyMemory<byte> Payload { get; init; }
    /// <summary>Which bank file this came from, for diagnostics.</summary>
    public required string Bank { get; init; }
    /// <summary>
    /// The bank's BKHD feedback flag (BKHD dword 3, gapA 2.3). When set, a node's NodeBaseParams carries four
    /// more bytes and a bus carries four more. It is zero in all six shipped banks; the reader still honors it.
    /// </summary>
    public bool FeedbackEnabled { get; init; }
}

/// <summary>
/// A reader for Audiokinetic Wwise SoundBank (<c>.bnk</c>) files, enough of one to answer the only
/// question this project asks of a bank: which media files does a given event play?
///
/// This is the one layer of the stack with no authority to check against. Wwise is not linked into
/// <c>libcozmoEngine.so</c> — the binary contains no occurrence of "Wwise", "BKHD", "AK::" or
/// "SoundEngine" — so unlike the robot protocol there is no native implementation here to read. The
/// layout below therefore comes from the public description of the format, and **every field is
/// cross-checked against the shipped assets**, which is what makes it trustworthy:
///
/// * all six banks parse with their chunk lengths consuming the file exactly;
/// * every bank's HIRC object list consumes its chunk exactly;
/// * type-4 object counts equal the per-bank event counts in SoundbanksInfo.xml;
/// * all 835 events parse with a 32-bit action count that lands exactly on the payload end;
/// * the 906 action references resolve to exactly the 906 EventAction objects present;
/// * the media id read from each Sound object names a file that actually exists in AudioAssets.zip,
///   for 2282 of 2360 Sound objects.
///
/// A field that failed any of those would be a field read at the wrong offset.
/// </summary>
public sealed class WwiseBank
{
    /// <summary>Every object in this bank, by id.</summary>
    public IReadOnlyDictionary<uint, WwiseObject> Objects { get; }
    /// <summary>The bank's own id, from its BKHD chunk.</summary>
    public uint BankId { get; }
    /// <summary>The bank format version from BKHD. Every bank in this build is version 120.</summary>
    public uint Version { get; }
    /// <summary>The file this was read from, without its directory.</summary>
    public string Name { get; }

    /// <summary>Media embedded in the bank itself, from its DIDX index: media id to bytes.</summary>
    public IReadOnlyDictionary<uint, ReadOnlyMemory<byte>> EmbeddedMedia { get; }

    /// <summary>
    /// The bank's STMG state-manager chunk, when it has one. Only <c>Init.bnk</c> does, and it carries the
    /// RTPC default table the value store falls back to (gapF 1.2/1.3).
    /// </summary>
    public WwiseStmg? Stmg { get; }

    private WwiseBank(string name, uint bankId, uint version,
                      Dictionary<uint, WwiseObject> objects,
                      Dictionary<uint, ReadOnlyMemory<byte>> embedded,
                      WwiseStmg? stmg)
    {
        Name = name; BankId = bankId; Version = version;
        Objects = objects; EmbeddedMedia = embedded; Stmg = stmg;
    }

    /// <summary>Reads a bank from bytes. Throws <see cref="InvalidDataException"/> on anything malformed.</summary>
    public static WwiseBank Parse(ReadOnlyMemory<byte> data, string name)
    {
        var span = data.Span;
        uint bankId = 0, version = 0;
        bool feedback = false;
        var objects = new Dictionary<uint, WwiseObject>();
        var embedded = new Dictionary<uint, ReadOnlyMemory<byte>>();
        ReadOnlyMemory<byte> didx = default, dataChunk = default;
        WwiseStmg? stmg = null;

        // A bank is a flat sequence of FourCC + length chunks. Nothing is nested and nothing is aligned.
        int off = 0;
        while (off + 8 <= span.Length)
        {
            var tag = span.Slice(off, 4);
            uint size = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(off + 4, 4));
            int body = off + 8;
            if (size > (uint)(span.Length - body))
                throw new InvalidDataException($"{name}: chunk at 0x{off:X} claims {size} bytes, past the end of the file");

            if (Match(tag, "BKHD"))
            {
                if (size < 8) throw new InvalidDataException($"{name}: BKHD is only {size} bytes");
                version = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(body, 4));
                bankId = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(body + 4, 4));
                // dword 3 is the feedback flag that adds four bytes to a node's NodeBaseParams (gapA 2.3).
                if (size >= 16) feedback = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(body + 12, 4)) != 0;
            }
            else if (Match(tag, "DIDX")) didx = data.Slice(body, (int)size);
            else if (Match(tag, "DATA")) dataChunk = data.Slice(body, (int)size);
            else if (Match(tag, "STMG")) stmg = WwiseStmg.Parse(data.Slice(body, (int)size), name);
            else if (Match(tag, "HIRC")) ReadHirc(data.Slice(body, (int)size), name, feedback, objects);

            off = body + (int)size;
        }
        if (off != span.Length)
            throw new InvalidDataException($"{name}: chunk lengths end at 0x{off:X}, not at the file end 0x{span.Length:X}");

        // DIDX is a flat table of (media id, offset into DATA, length).
        if (!didx.IsEmpty && !dataChunk.IsEmpty)
        {
            var d = didx.Span;
            if (d.Length % 12 != 0) throw new InvalidDataException($"{name}: DIDX is {d.Length} bytes, not a multiple of 12");
            for (int i = 0; i + 12 <= d.Length; i += 12)
            {
                uint id = BinaryPrimitives.ReadUInt32LittleEndian(d.Slice(i, 4));
                uint o = BinaryPrimitives.ReadUInt32LittleEndian(d.Slice(i + 4, 4));
                uint n = BinaryPrimitives.ReadUInt32LittleEndian(d.Slice(i + 8, 4));
                if (o > (uint)dataChunk.Length || n > (uint)dataChunk.Length - o)
                    throw new InvalidDataException($"{name}: DIDX entry {id} points outside DATA");
                embedded[id] = dataChunk.Slice((int)o, (int)n);
            }
        }
        return new WwiseBank(name, bankId, version, objects, embedded, stmg);
    }

    private static bool Match(ReadOnlySpan<byte> tag, string s) =>
        tag.Length == 4 && tag[0] == s[0] && tag[1] == s[1] && tag[2] == s[2] && tag[3] == s[3];

    /// <summary>
    /// HIRC is a count followed by that many objects, each a type byte, a 32-bit length, and a body that
    /// begins with the object's id. The reader insists the objects consume the chunk exactly, which is
    /// what caught the layout being right in the first place.
    /// </summary>
    private static void ReadHirc(ReadOnlyMemory<byte> chunk, string bank, bool feedback, Dictionary<uint, WwiseObject> into)
    {
        var s = chunk.Span;
        if (s.Length < 4) throw new InvalidDataException($"{bank}: HIRC is only {s.Length} bytes");
        uint count = BinaryPrimitives.ReadUInt32LittleEndian(s[..4]);
        int p = 4;
        for (uint i = 0; i < count; i++)
        {
            if (p + 5 > s.Length) throw new InvalidDataException($"{bank}: HIRC ends inside object {i} of {count}");
            var type = (WwiseObjectType)s[p];
            uint size = BinaryPrimitives.ReadUInt32LittleEndian(s.Slice(p + 1, 4));
            int body = p + 5;
            if (size < 4 || size > (uint)(s.Length - body))
                throw new InvalidDataException($"{bank}: HIRC object {i} claims {size} bytes");
            uint id = BinaryPrimitives.ReadUInt32LittleEndian(s.Slice(body, 4));
            // Later banks win on a duplicate id; in this build there are none.
            into[id] = new WwiseObject
            {
                Id = id, Type = type, Payload = chunk.Slice(body, (int)size), Bank = bank,
                FeedbackEnabled = feedback,
            };
            p = body + (int)size;
        }
        if (p != s.Length)
            throw new InvalidDataException($"{bank}: HIRC objects end at {p}, not at the chunk end {s.Length}");
    }

    // ---------------------------------------------------------------- object field accessors

    /// <summary>
    /// The action ids an Event fires. Layout: id, then a 32-bit count, then that many ids. All 835 events
    /// in this build end exactly at the last id, which is what rules out the 8-bit count used by some
    /// other bank versions.
    /// </summary>
    public static IReadOnlyList<uint> EventActions(WwiseObject e)
    {
        var s = e.Payload.Span;
        if (e.Type != WwiseObjectType.Event || s.Length < 8) return Array.Empty<uint>();
        uint n = BinaryPrimitives.ReadUInt32LittleEndian(s.Slice(4, 4));
        if (8 + 4L * n != s.Length) return Array.Empty<uint>();
        var ids = new uint[n];
        for (int i = 0; i < n; i++) ids[i] = BinaryPrimitives.ReadUInt32LittleEndian(s.Slice(8 + 4 * i, 4));
        return ids;
    }

    /// <summary>
    /// A Play action, if this object is one. The 16-bit value after the id is the action type: 0x0403 is
    /// Play on a game object, and is the only one that starts a sound. 723 of the 906 actions in this
    /// build are 0x0403; the rest are Stop, Pause, Resume and state changes, which play nothing.
    /// </summary>
    public static uint? PlayActionTarget(WwiseObject a)
    {
        var s = a.Payload.Span;
        if (a.Type != WwiseObjectType.EventAction || s.Length < 10) return null;
        ushort type = BinaryPrimitives.ReadUInt16LittleEndian(s.Slice(4, 2));
        if (type != PlayAction) return null;
        return BinaryPrimitives.ReadUInt32LittleEndian(s.Slice(6, 4));
    }

    /// <summary>The action type of a Play action: the high byte is the action, the low byte its scope.</summary>
    public const ushort PlayAction = 0x0403;

    /// <summary>The action type's high byte is its kind: 0x01 Stop, 0x02 Pause, 0x03 Resume, 0x04 Play (the shipped Stop actions are 0x0102/0x0103).</summary>
    public static bool IsStopAction(ushort actionType) => (actionType >> 8) == 0x01;
    public static bool IsPlayAction(ushort actionType) => (actionType >> 8) == 0x04;

    /// <summary>
    /// The media id a Sound object plays. It sits ten bytes in, after the object id, a four-byte plugin
    /// id and a one-byte stream type. Read this way, 2282 of the 2360 Sound objects in this build name a
    /// file that actually exists in AudioAssets.zip; no other offset produces a single match.
    /// </summary>
    public static uint? SoundMediaId(WwiseObject o)
    {
        var s = o.Payload.Span;
        if (o.Type != WwiseObjectType.Sound || s.Length < SoundMediaIdOffset + 4) return null;
        return BinaryPrimitives.ReadUInt32LittleEndian(s.Slice(SoundMediaIdOffset, 4));
    }

    private const int SoundMediaIdOffset = 9;

    /// <summary>
    /// The id of this object's parent, read from its node block by <see cref="WwiseHierarchy"/>.
    ///
    /// M6 found the parent by scanning each object type for the offset whose 32-bit value was another
    /// object's id (Sound 25, containers 11, music nodes 12) and walked only the types where that scan was
    /// unambiguous. M9 replaced the scan with the full node-block layout, which every object of the nine
    /// hierarchy types consumes exactly. Two of M6's offsets were corrected by it: SwitchContainer's parent
    /// is at 11, not 8 (8 read the bus id), and the 46 Sounds M6 could not place are source plug-ins
    /// (Wwise Sine, Silence, Anki Wave Portal), whose parent sits four bytes later behind a plug-in
    /// parameter size. Objects whose type is not read here have no parent to report.
    /// </summary>
    public static uint? ParentId(WwiseObject o) => WwiseHierarchy.TryRead(o, out _)?.Params.ParentId;
}
