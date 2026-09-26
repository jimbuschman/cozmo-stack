// fidelity: M6-001
using System.Buffers.Binary;

namespace Cozmo.Robot.Animation.Wwise;

/// <summary>
/// Property ids used in a node's property bundle, numbered as bank version 120 numbers them.
///
/// The numbering is the public one (wwiser's list for this bank version). The entries that matter here
/// are confirmed by the shipped values themselves: <see cref="MidiTargetNode"/> (56) on the three
/// Cozmo_Sings containers holds the id of the blend container their MIDI notes go to;
/// <see cref="MidiKeyRangeMin"/>/<see cref="MidiKeyRangeMax"/> (49/50) on the note containers hold
/// integer MIDI keys 48..61; <see cref="Volume"/> (0) holds small decibel offsets, <see cref="Pitch"/> (2)
/// cent offsets of 30 and 100, <see cref="Loop"/> (58) zero for "loop until told to stop", and
/// <see cref="MidiPlayOnNoteType"/> (46) is 2 on the container that plays at note-off.
/// </summary>
public enum WwiseProp : byte
{
    Volume = 0, Pitch = 2, LowPass = 3, HighPass = 4, BusVolume = 5, MakeUpGain = 6, Priority = 7,
    PriorityDistanceOffset = 8, MuteRatio = 11, PanLR = 12, PanFR = 13, CenterPercent = 14, DelayTime = 15,
    TransitionTime = 16, Probability = 17, DialogueMode = 18,
    MidiTrackingRootNote = 45, MidiPlayOnNoteType = 46, MidiTransposition = 47, MidiVelocityOffset = 48,
    MidiKeyRangeMin = 49, MidiKeyRangeMax = 50, MidiVelocityRangeMin = 51, MidiVelocityRangeMax = 52,
    MidiChannelMask = 53, PlaybackSpeed = 54, MidiTempoSource = 55, MidiTargetNode = 56,
    AttachedPluginFxId = 57, Loop = 58, InitialDelay = 59,
}

/// <summary>
/// One RTPC or modulator binding on a node: what drives it, what it drives, and the curve.
///
/// <see cref="SourceType"/> is 0 for a game parameter and 2 for a modulator; both readings come from the
/// shipped banks, where every type-0 source id is an id listed under "Game Parameter" in a bank definition
/// file and every type-2 source id is an LFO or envelope object in the same bank.
///
/// <see cref="ParamId"/> on a node names one of that node's properties with the same numbering
/// <see cref="WwiseProp"/> uses (0 Volume, 2 Pitch). On a modulator object it names one of the modulator's
/// own parameters, in a numbering of its own that starts at the first one an RTPC can drive: the singing
/// vibrato LFO binds <c>cozmo_singing_vibrato</c> to parameter 0 over a curve from 0 to 100, which is the
/// depth in per cent.
/// </summary>
public sealed record WwiseRtpc(uint SourceId, byte SourceType, byte Accumulate, uint ParamId, uint CurveId,
                               byte Scaling, IReadOnlyList<(float From, float To, uint Interp)> Points)
{
    /// <summary>A binding whose source is a modulator object rather than a game parameter.</summary>
    public const byte ModulatorSource = 2;
    /// <summary>A binding driven by a game parameter the game posts.</summary>
    public const byte GameParameterSource = 0;

    /// <summary>
    /// The curve at <paramref name="x"/>, clamped to the end points outside the range the curve defines.
    ///
    /// Each point carries the interpolation to use from it to the next (gapA 5.3). Every shape the runtime
    /// implements is here: 4 linear, 9 constant, 0 Log3, 1 Sine, 2 Log1, 3 InvSCurve, 5 SCurve, 6 Exp1,
    /// 7 SineRecip and 8 Exp3. An unknown code falls back to linear and sets <paramref name="reduced"/>.
    /// The result is the raw curve value; <see cref="ApplyScaling"/> applies the scaling byte.
    /// </summary>
    public double Evaluate(double x, out bool reduced)
    {
        reduced = false;
        if (Points.Count == 0) return 0;
        if (Points.Count == 1 || x <= Points[0].From) return Points[0].To;
        if (x >= Points[^1].From) return Points[^1].To;
        for (int i = 0; i + 1 < Points.Count; i++)
        {
            var (x0, y0, interp) = Points[i];
            var (x1, y1, _) = Points[i + 1];
            if (x < x0 || x > x1) continue;
            double span = x1 - x0;
            double t = span <= 0 ? 1 : (x - x0) / span;
            if (interp > 9) reduced = true;
            return y0 + (y1 - y0) * Shape(interp, t);
        }
        return Points[^1].To;
    }

    /// <summary>The interpolation weight for parameter <paramref name="t"/> in [0,1] (gapA 5.3).</summary>
    private static double Shape(uint interp, double t) => interp switch
    {
        9 => 0,                                                      // constant: hold the left value
        0 => 1 - Math.Pow(1 - t, 3),                                 // Log3
        // Sine: sin(t·π/2). The native fast polynomial evaluates sin on [0,1]; the runtime result the
        // inventory records (event_volume 0.5 → −3.01 dB) requires the π/2 argument, which is used here.
        1 => Math.Sin(t * Math.PI / 2),
        2 => t * (3 - t) / 2,                                        // Log1
        3 => t <= 0.5 ? Math.Sin(Math.PI * t) / 2 : 1 - Math.Sin(Math.PI - Math.PI * t) / 2,  // InvSCurve
        5 => SCurve(t),                                             // SCurve
        6 => t * (t + 1) / 2,                                        // Exp1
        7 => 1 - Math.Cos(Math.PI * t / 2),                          // SineRecip
        8 => t * t * t,                                              // Exp3
        _ => t,                                                      // 4 linear (and unknown codes)
    };

    private static double SCurve(double t)
    {
        double u = Math.PI * Math.PI * t * t;                        // (πt)²
        return 0.0006967 + u * (0.2476748 + u * (-0.0196138 + 0.00048483 * u));
    }

    /// <summary>
    /// The scaling byte applied after the curve (gapA 5.3): 2 is dB via ±20·log10(1∓|y|), 3 is 10^y,
    /// 4 is 10^(0.05y), anything else leaves the value unchanged. The native fast log/pow are approximated
    /// here by the standard library; the semantic and the clamp at ±764.616 dB match the rows.
    /// </summary>
    public static double ApplyScaling(byte scaling, double y) => scaling switch
    {
        2 => y >= 1 ? 764.616 : y <= -1 ? -764.616
             : y >= 0 ? -20 * Math.Log10(1 - y) : 20 * Math.Log10(1 + y),
        3 => y < -37 ? 0 : Math.Pow(10, y),
        4 => 0.05 * y < -37 ? 0 : Math.Pow(10, 0.05 * y),
        _ => y,
    };

    /// <summary>The curve value at <paramref name="x"/> with the scaling byte applied.</summary>
    public double EvaluateScaled(double x, out bool reduced) => ApplyScaling(Scaling, Evaluate(x, out reduced));
}

/// <summary>
/// The block every hierarchy node carries (Wwise's NodeBaseParams): routing, parent, the property bundle,
/// state groups and RTPCs. The layout was read from the shipped banks object by object and is checked by
/// exact consumption across every one of them; see <see cref="WwiseHierarchy"/>.
///
/// <para><b><see cref="Bits"/>, and why MIDI note tracking is off everywhere.</b> Across all six banks only
/// three values of this byte occur: 0x00 on 2881 nodes, 0x01 on exactly three, and 0x24 on exactly two.
/// The three that carry 0x01 are precisely the three nodes that set the Priority property and no other
/// node does, which identifies bit 0 as the priority override. The two that carry 0x24 are precisely the
/// singing sampler's note-on and note-off layers (462443456 and 774902407), and bits 2 and 5 occur nowhere
/// else in any bank. Those two bits have to be the pair that makes those layers work — one of them lets the
/// note-off layer's play-on-note-off property take effect, the other stops the note-on layer's
/// indefinitely looping sounds when the note is released — because without both the note-off layer would
/// never sound and a held note would never end. So no bit anywhere in any shipped bank can be the one that
/// enables MIDI note tracking, and no node in any bank sets a tracking root note either (property 45 does
/// not occur). Note tracking is therefore off throughout, and the per-key recordings play at the pitch they
/// were recorded at.</para>
/// </summary>
public sealed record WwiseNodeParams(
    uint BusId, uint ParentId, byte Bits,
    IReadOnlyDictionary<byte, uint> Props,
    IReadOnlyDictionary<byte, (float Min, float Max)> RangedProps,
    IReadOnlyList<WwiseRtpc> Rtpcs,
    IReadOnlyList<(uint GroupId, byte SyncType, IReadOnlyList<(uint StateId, uint InstanceId)> States)> StateGroups)
{
    /// <summary>A property as the float most of them are, or null when the node does not set it.</summary>
    public float? Float(WwiseProp p) =>
        Props.TryGetValue((byte)p, out var v) ? BitConverter.Int32BitsToSingle((int)v) : null;
    /// <summary>A property as the raw 32-bit value; the MIDI key ranges and the MIDI target are integers.</summary>
    public uint? Raw(WwiseProp p) => Props.TryGetValue((byte)p, out var v) ? v : null;
}

/// <summary>A parsed hierarchy node. The concrete records add what their type carries.</summary>
public abstract record WwiseNode(uint Id, WwiseObjectType Type, string Bank, WwiseNodeParams Params, IReadOnlyList<uint> Children);

public sealed record WwiseSoundNode(uint Id, string Bank, WwiseNodeParams Params, uint PluginId, byte StreamType,
                                    uint MediaId, uint InMemorySize, byte SourceBits)
    : WwiseNode(Id, WwiseObjectType.Sound, Bank, Params, Array.Empty<uint>())
{
    /// <summary>
    /// Source plug-ins (Wwise Sine, Silence, Anki Wave Portal) have plugin nibble 2 or 5 and no media
    /// (gapA 2.4).
    /// </summary>
    public bool IsSourcePlugin => (PluginId & 0x0F) is 2 or 5;
}

public sealed record WwiseRandomSequenceNode(uint Id, string Bank, WwiseNodeParams Params, IReadOnlyList<uint> Children,
                                             ushort LoopCount, byte TransitionMode, byte RandomMode, byte Mode, byte Flags,
                                             ushort AvoidRepeatCount, IReadOnlyList<(uint ChildId, int Weight)> Playlist)
    : WwiseNode(Id, WwiseObjectType.RandomSequenceContainer, Bank, Params, Children)
{
    /// <summary>Mode 0 picks one alternative at random by weight; mode 1 steps through the playlist in order.</summary>
    public bool IsSequence => Mode == 1;
}

public sealed record WwiseSwitchNode(uint Id, string Bank, WwiseNodeParams Params, IReadOnlyList<uint> Children,
                                     byte GroupType, uint GroupId, uint DefaultSwitch, byte IsContinuousValidation,
                                     IReadOnlyList<(uint SwitchId, IReadOnlyList<uint> NodeIds)> Assignments)
    : WwiseNode(Id, WwiseObjectType.SwitchContainer, Bank, Params, Children);

public sealed record WwiseActorMixerNode(uint Id, string Bank, WwiseNodeParams Params, IReadOnlyList<uint> Children)
    : WwiseNode(Id, WwiseObjectType.ActorMixer, Bank, Params, Children);

/// <summary>
/// A blend container. <see cref="BlendTracks"/> is how many blend tracks it groups its children into: a
/// track carries a crossfade RTPC and a curve per child, so a container with tracks does not simply play
/// all of its children at their own level. Not one of the six in the shipped banks has any.
/// </summary>
public sealed record WwiseBlendNode(uint Id, string Bank, WwiseNodeParams Params, IReadOnlyList<uint> Children, int BlendTracks)
    : WwiseNode(Id, WwiseObjectType.BlendContainer, Bank, Params, Children);

/// <summary>
/// Tempo and grid of a music node: grid period and offset in ms, tempo in beats per minute, time
/// signature, and whether this node's meter overrides its parent's (the bank's bMeterInfoFlag). A node
/// that does not override carries the default 120 bpm and plays at the nearest overriding ancestor's tempo.
/// </summary>
public readonly record struct WwiseMeter(double GridPeriodMs, double GridOffsetMs, float TempoBpm, byte BeatsPerBar, byte BeatValue, bool OverridesParent);

public sealed record WwiseMusicSegmentNode(uint Id, string Bank, WwiseNodeParams Params, IReadOnlyList<uint> Children,
                                           byte MusicFlags, WwiseMeter Meter, double DurationMs,
                                           IReadOnlyList<(uint MarkerId, double PositionMs, string Name)> Markers)
    : WwiseNode(Id, WwiseObjectType.MusicSegment, Bank, Params, Children);

/// <summary>One clip on a music track: which source plays, when, and how it is trimmed. All times in ms.</summary>
public readonly record struct WwiseTrackClip(uint TrackIndex, uint SourceId, double PlayAtMs, double BeginTrimMs,
                                             double EndTrimMs, double SourceDurationMs)
{
    /// <summary>How long the clip sounds for: the source trimmed at both ends (the end trim is negative).</summary>
    public double LengthMs => SourceDurationMs + EndTrimMs - BeginTrimMs;
}

public sealed record WwiseMusicTrackNode(uint Id, string Bank, WwiseNodeParams Params, byte MusicFlags,
                                         IReadOnlyList<(uint PluginId, byte StreamType, uint SourceId, uint InMemorySize, byte SourceBits)> Sources,
                                         IReadOnlyList<WwiseTrackClip> Clips, byte TrackType, int LookAheadMs)
    : WwiseNode(Id, WwiseObjectType.MusicTrack, Bank, Params, Array.Empty<uint>())
{
    /// <summary>Plugin id 0x00100001 is Wwise's MIDI codec: the source is a MIDI sequence, not audio.</summary>
    public const uint MidiPluginId = 0x00100001;
    public bool HasMidiSource => Sources.Any(s => s.PluginId == MidiPluginId);
}

/// <summary>A node of a music switch container's decision tree.</summary>
public readonly record struct WwiseDecisionNode(uint Key, uint AudioNodeId, ushort ChildIndex, ushort ChildCount, ushort Weight, ushort Probability);

public sealed record WwiseMusicSwitchNode(uint Id, string Bank, WwiseNodeParams Params, IReadOnlyList<uint> Children,
                                          byte MusicFlags, WwiseMeter Meter, bool ContinuePlayback,
                                          IReadOnlyList<(uint GroupId, byte GroupType)> Arguments, byte TreeMode,
                                          IReadOnlyList<WwiseDecisionNode> Tree)
    : WwiseNode(Id, WwiseObjectType.MusicSwitchContainer, Bank, Params, Children)
{
    /// <summary>The node this container's MIDI notes are sent to, when it names one (property 56).</summary>
    public uint? MidiTargetNode => Params.Raw(WwiseProp.MidiTargetNode);

    /// <summary>
    /// Walks the decision tree with the current switch values, one level per argument. At each level the
    /// child whose key equals the group's current value is taken; failing that, the child with key 0 (the
    /// "any" entry every shipped tree carries first). Returns the selected node id, or null when no path
    /// matches. Group values missing from <paramref name="switches"/> count as 0.
    /// </summary>
    public uint? Select(IReadOnlyDictionary<uint, uint> switches)
    {
        if (Tree.Count == 0) return null;
        var node = Tree[0];
        for (int level = 0; level < Arguments.Count; level++)
        {
            uint want = switches.TryGetValue(Arguments[level].GroupId, out var v) ? v : 0;
            WwiseDecisionNode? exact = null, any = null;
            for (int i = 0; i < node.ChildCount; i++)
            {
                int idx = node.ChildIndex + i;
                if (idx >= Tree.Count) return null;
                var c = Tree[idx];
                if (c.Key == want && exact is null) exact = c;
                if (c.Key == 0 && any is null) any = c;
            }
            var next = exact ?? any;
            if (next is null) return null;
            node = next.Value;
        }
        return node.AudioNodeId;
    }
}

/// <summary>One playlist entry: a group (SegmentId 0, with children following in order) or a segment leaf.</summary>
public readonly record struct WwisePlaylistItem(uint SegmentId, uint ItemId, uint ChildCount, int SequenceType,
                                                ushort Loop, ushort LoopMin, ushort LoopMax, uint Weight,
                                                ushort AvoidRepeatCount, bool UsesWeight, bool Shuffle);

public sealed record WwiseMusicPlaylistNode(uint Id, string Bank, WwiseNodeParams Params, IReadOnlyList<uint> Children,
                                            byte MusicFlags, WwiseMeter Meter, IReadOnlyList<WwisePlaylistItem> Playlist)
    : WwiseNode(Id, WwiseObjectType.MusicPlaylistContainer, Bank, Params, Children);

/// <summary>
/// Reads the hierarchy objects the music path needs, requiring every object to be consumed exactly.
///
/// There is no Wwise runtime in this package to check against (see <see cref="WwiseBank"/>), so the layout
/// comes from the public description of bank version 120 and was then settled against the shipped banks
/// object by object: every field position below was hand-decoded from real objects (a 44-byte actor-mixer,
/// 46- and 50-byte sounds, a 74-byte random container, the 91-byte blend container the songs target, a
/// 112-byte segment, a 108-byte track, a 198-byte playlist and the 398-byte Cozmo_Sings_80Bpm switch), and
/// the reader then has to consume all 3,490 objects of these nine types in the six banks to the last byte,
/// which it does (2360 sounds, 468 random/sequence containers, 21 switch containers, 31 actor-mixers, 6
/// blend containers, 209 segments, 258 tracks, 14 music switches, 123 playlists). Two details differ from
/// the M6 reader's scanned parent offsets and are corrected here: the state-group count is 32 bits wide in
/// this version, and a Sound whose plugin is a source plug-in carries a four-byte parameter size before its
/// node block, which moves its parent id; that accounts for the 46 sounds M6 could not place.
/// </summary>
public static class WwiseHierarchy
{
    /// <summary>Parses an object into a node, or returns null with the reason when its type is not read here or it does not consume exactly.</summary>
    public static WwiseNode? TryRead(WwiseObject o, out string? problem)
    {
        problem = null;
        try
        {
            var r = new Reader(o.Payload.Span);
            WwiseNode? node = o.Type switch
            {
                WwiseObjectType.Sound => ReadSound(ref r, o),
                WwiseObjectType.RandomSequenceContainer => ReadRandomSequence(ref r, o),
                WwiseObjectType.SwitchContainer => ReadSwitch(ref r, o),
                WwiseObjectType.ActorMixer => ReadActorMixer(ref r, o),
                WwiseObjectType.BlendContainer => ReadBlend(ref r, o),
                WwiseObjectType.MusicSegment => ReadSegment(ref r, o),
                WwiseObjectType.MusicTrack => ReadTrack(ref r, o),
                WwiseObjectType.MusicSwitchContainer => ReadMusicSwitch(ref r, o),
                WwiseObjectType.MusicPlaylistContainer => ReadPlaylist(ref r, o),
                WwiseObjectType.LfoModulator or WwiseObjectType.EnvelopeModulator => ReadModulator(ref r, o),
                WwiseObjectType.AudioBus => ReadBus(ref r, o),
                WwiseObjectType.FxShareSet or WwiseObjectType.FxCustom => ReadEffect(ref r, o),
                _ => null,
            };
            if (node is null) { problem = $"type {(byte)o.Type} is not read"; return null; }
            if (r.Remaining != 0)
            {
                problem = $"{o.Bank} object {o.Id} type {(byte)o.Type}: {r.Remaining} bytes left of {o.Payload.Length}";
                return null;
            }
            return node;
        }
        catch (Exception ex) when (ex is ArgumentOutOfRangeException or IndexOutOfRangeException or InvalidDataException)
        {
            problem = $"{o.Bank} object {o.Id} type {(byte)o.Type}: ran past the end ({ex.Message})";
            return null;
        }
    }

    // ---------------------------------------------------------------- the shared blocks

    private static WwiseNodeParams ReadNodeParams(ref Reader r, bool feedback)
    {
        // NodeInitialFxParams
        r.U8();
        int numFx = r.U8();
        if (numFx > 0)
        {
            r.U8();                                             // bypass bits
            for (int i = 0; i < numFx; i++) { r.U8(); r.U32(); r.U8(); r.U8(); }
        }
        r.U8();                                                 // bOverrideAttachmentParams
        uint bus = r.U32();
        uint parent = r.U32();
        byte bits = r.U8();
        // NodeInitialParams: ids first, then values
        int n = r.U8();
        var ids = new byte[n];
        for (int i = 0; i < n; i++) ids[i] = r.U8();
        var props = new Dictionary<byte, uint>(n);
        for (int i = 0; i < n; i++) props[ids[i]] = r.U32();
        n = r.U8();
        ids = new byte[n];
        for (int i = 0; i < n; i++) ids[i] = r.U8();
        var ranged = new Dictionary<byte, (float, float)>(n);
        for (int i = 0; i < n; i++) ranged[ids[i]] = (r.F32(), r.F32());
        // PositioningParams (gapA 2.4): one byte; more follows only when b0 and b3 are both set. No shipped
        // node has that, and the body past the attenuation is not in the rows, so it fails rather than
        // silently realigning.
        byte posBits = r.U8();
        if ((posBits & 0x01) != 0 && (posBits & 0x08) != 0)
            throw new InvalidDataException("positioning 3D body (b0 & b3) is not recovered in the frozen rows");
        // AuxParams (gapA 2.4): u8 bits; b3 begins an aux list whose length is not in the rows. The shipped
        // value is read as one byte; the list is left unresolved.
        byte aux = r.U8();
        if ((aux & 0x08) != 0) for (int i = 0; i < 4; i++) r.U32();
        // AdvSettingsParams
        r.U8(); r.U8(); r.U16(); r.U8(); r.U8();
        // StateChunk: 32-bit group count in this version
        uint groups = r.U32();
        var stateGroups = new List<(uint, byte, IReadOnlyList<(uint, uint)>)>((int)Math.Min(groups, 64));
        for (uint g = 0; g < groups; g++)
        {
            uint gid = r.U32(); byte sync = r.U8(); int ns = r.U16();
            var states = new List<(uint, uint)>(ns);
            for (int s = 0; s < ns; s++) states.Add((r.U32(), r.U32()));
            stateGroups.Add((gid, sync, states));
        }
        // InitialRTPC
        int curves = r.U16();
        var rtpcs = new List<WwiseRtpc>(curves);
        for (int c = 0; c < curves; c++) rtpcs.Add(ReadRtpc(ref r));
        // NodeBaseParams: 4 more bytes only when the BKHD feedback flag is set (gapA 2.3). It is 0 in all six
        // shipped banks.
        if (feedback) r.Skip(4);
        return new WwiseNodeParams(bus, parent, bits, props, ranged, rtpcs, stateGroups);
    }

    private static WwiseRtpc ReadRtpc(ref Reader r)
    {
        uint src = r.U32(); byte type = r.U8(); byte acc = r.U8();
        // The parameter id is a Wwise varint (gapA 2.5): 7 bits per byte, bit 0x80 = continue, big-endian
        // accumulation. A one-byte read agrees only while every shipped id is below 0x80.
        uint param = ReadVarint(ref r);
        uint curve = r.U32(); byte scaling = r.U8(); int npts = r.U16();
        var pts = new List<(float, float, uint)>(npts);
        for (int i = 0; i < npts; i++) pts.Add((r.F32(), r.F32(), r.U32()));
        return new WwiseRtpc(src, type, acc, param, curve, scaling, pts);
    }

    /// <summary>Wwise varint (7 bits per byte, bit 0x80 continues, high groups first).</summary>
    private static uint ReadVarint(ref Reader r)
    {
        uint value = 0;
        for (int i = 0; i < 5; i++)                                  // a 32-bit value needs at most 5 groups
        {
            byte b = r.U8();
            value = (value << 7) | (uint)(b & 0x7F);
            if ((b & 0x80) == 0) return value;
        }
        throw new InvalidDataException("varint longer than 5 bytes");
    }

    private static IReadOnlyList<uint> ReadChildren(ref Reader r)
    {
        uint n = r.U32();
        var kids = new uint[n];
        for (int i = 0; i < n; i++) kids[i] = r.U32();
        return kids;
    }

    private static WwiseMeter ReadMeter(ref Reader r)
    {
        double period = r.F64(), offset = r.F64(); float tempo = r.F32(); byte beats = r.U8(), value = r.U8();
        bool overrides = r.U8() != 0;                           // bMeterInfoFlag: 0 inherits, 1 or 255 overrides
        return new WwiseMeter(period, offset, tempo, beats, value, overrides);
    }

    private static void SkipStingers(ref Reader r)
    {
        uint n = r.U32();
        for (uint i = 0; i < n; i++) { r.U32(); r.U32(); r.U32(); r.U32(); r.U32(); r.U32(); }
    }

    private static void SkipTransitionRules(ref Reader r)
    {
        uint n = r.U32();
        for (uint i = 0; i < n; i++)
        {
            uint ns = r.U32(); for (uint k = 0; k < ns; k++) r.U32();
            uint nd = r.U32(); for (uint k = 0; k < nd; k++) r.U32();
            r.Skip(21);                                         // source rule
            r.Skip(24);                                         // destination rule
            if (r.U8() != 0) r.Skip(30);                        // optional transition object
        }
    }

    // ---------------------------------------------------------------- the node types

    private static WwiseSoundNode ReadSound(ref Reader r, WwiseObject o)
    {
        uint id = r.U32();
        uint plugin = r.U32(); byte stream = r.U8(); uint media = r.U32(); uint size = r.U32(); byte srcBits = r.U8();
        // Source plug-in (gapA 2.4): a u32 parameter-block size followed by that many bytes. The native
        // branch takes plugin & 0xF == 2 or 5 (0x009B9D30 cmp ip,#5; 0x009B9D34 cmpne ip,#2; 0x009B9D38
        // beq). All shipped sources are codecs, so the block is empty; it is still consumed exactly.
        if ((plugin & 0x0F) is 2 or 5) { uint pluginSize = r.U32(); r.Skip((int)pluginSize); }
        var p = ReadNodeParams(ref r, o.FeedbackEnabled);
        return new WwiseSoundNode(id, o.Bank, p, plugin, stream, media, size, srcBits);
    }

    private static WwiseRandomSequenceNode ReadRandomSequence(ref Reader r, WwiseObject o)
    {
        uint id = r.U32();
        var p = ReadNodeParams(ref r, o.FeedbackEnabled);
        ushort loop = r.U16(); r.U16(); r.U16();
        r.F32(); r.F32(); r.F32();                              // transition time and its modulation range
        ushort avoid = r.U16(); byte transMode = r.U8(); byte randomMode = r.U8(); byte mode = r.U8(); byte flags = r.U8();
        var kids = ReadChildren(ref r);
        int n = r.U16();
        var playlist = new List<(uint, int)>(n);
        for (int i = 0; i < n; i++) playlist.Add((r.U32(), r.S32()));
        return new WwiseRandomSequenceNode(id, o.Bank, p, kids, loop, transMode, randomMode, mode, flags, avoid, playlist);
    }

    private static WwiseSwitchNode ReadSwitch(ref Reader r, WwiseObject o)
    {
        uint id = r.U32();
        var p = ReadNodeParams(ref r, o.FeedbackEnabled);
        byte groupType = r.U8(); uint group = r.U32(); uint def = r.U32(); byte cont = r.U8();
        var kids = ReadChildren(ref r);
        uint n = r.U32();
        var assoc = new List<(uint, IReadOnlyList<uint>)>((int)Math.Min(n, 256));
        for (uint i = 0; i < n; i++)
        {
            uint sw = r.U32(); uint m = r.U32();
            var ids = new uint[m];
            for (int k = 0; k < m; k++) ids[k] = r.U32();
            assoc.Add((sw, ids));
        }
        uint np = r.U32();
        for (uint i = 0; i < np; i++) { r.U32(); r.U8(); r.U8(); r.S32(); r.S32(); }
        return new WwiseSwitchNode(id, o.Bank, p, kids, groupType, group, def, cont, assoc);
    }

    private static WwiseActorMixerNode ReadActorMixer(ref Reader r, WwiseObject o)
    {
        uint id = r.U32();
        var p = ReadNodeParams(ref r, o.FeedbackEnabled);
        return new WwiseActorMixerNode(id, o.Bank, p, ReadChildren(ref r));
    }

    private static WwiseBlendNode ReadBlend(ref Reader r, WwiseObject o)
    {
        uint id = r.U32();
        var p = ReadNodeParams(ref r, o.FeedbackEnabled);
        var kids = ReadChildren(ref r);
        // Blend tracks. Every one of the six blend containers in every shipped bank has none, so nothing a
        // crossfade curve would do is lost by reading past them: a blend container here always plays all of
        // its children at the level their own properties give (checked by
        // WwiseMusicTests.NoBlendContainerInAnyShippedBankHasABlendTrack).
        uint layers = r.U32();
        // The real LayerCntr reader (0x9D24D4, gapD D6.3) is NodeBase, children, a u32 layer count whose
        // entries are made by 0xA6D9EC/0xA6DD54, then a u8. That per-layer body is not recovered in the
        // frozen rows, and every one of the six shipped LayerCntr objects has zero layers (checked by
        // WwiseMusicTests.NoBlendContainerInAnyShippedBankHasABlendTrack), so it fails rather than reading
        // an unrecovered body.
        if (layers != 0)
            throw new InvalidDataException("LayerCntr layer body (0xA6DD54) is not recovered in the frozen rows");
        r.U8();                                                 // bIsContinuousValidation (+0x84)
        return new WwiseBlendNode(id, o.Bank, p, kids, (int)layers);
    }

    private static WwiseMusicSegmentNode ReadSegment(ref Reader r, WwiseObject o)
    {
        uint id = r.U32();
        byte flags = r.U8();
        var p = ReadNodeParams(ref r, o.FeedbackEnabled);
        var kids = ReadChildren(ref r);
        var meter = ReadMeter(ref r);
        SkipStingers(ref r);
        double duration = r.F64();
        uint n = r.U32();
        var markers = new List<(uint, double, string)>((int)Math.Min(n, 64));
        for (uint i = 0; i < n; i++)
        {
            uint mid = r.U32(); double pos = r.F64(); int len = (int)r.U32();
            markers.Add((mid, pos, System.Text.Encoding.ASCII.GetString(r.Bytes(len))));
        }
        return new WwiseMusicSegmentNode(id, o.Bank, p, kids, flags, meter, duration, markers);
    }

    private static WwiseMusicTrackNode ReadTrack(ref Reader r, WwiseObject o)
    {
        uint id = r.U32();
        byte flags = r.U8();
        uint ns = r.U32();
        var sources = new List<(uint, byte, uint, uint, byte)>((int)Math.Min(ns, 64));
        for (uint i = 0; i < ns; i++) sources.Add((r.U32(), r.U8(), r.U32(), r.U32(), r.U8()));
        uint nc = r.U32();
        var clips = new List<WwiseTrackClip>((int)Math.Min(nc, 64));
        for (uint i = 0; i < nc; i++) clips.Add(new WwiseTrackClip(r.U32(), r.U32(), r.F64(), r.F64(), r.F64(), r.F64()));
        if (nc > 0) r.U32();                                    // number of sub-tracks
        uint na = r.U32();
        for (uint i = 0; i < na; i++)
        {
            r.U32(); r.U32();
            uint npts = r.U32();
            for (uint k = 0; k < npts; k++) { r.F32(); r.F32(); r.U32(); }
        }
        var p = ReadNodeParams(ref r, o.FeedbackEnabled);
        byte trackType = r.U8();
        if (trackType == 3)
        {
            r.U8(); r.U32(); r.U32();
            uint n = r.U32(); for (uint k = 0; k < n; k++) r.U32();
            r.Skip(30);
        }
        int lookAhead = r.S32();
        return new WwiseMusicTrackNode(id, o.Bank, p, flags, sources, clips, trackType, lookAhead);
    }

    private static WwiseMusicSwitchNode ReadMusicSwitch(ref Reader r, WwiseObject o)
    {
        uint id = r.U32();
        byte flags = r.U8();
        var p = ReadNodeParams(ref r, o.FeedbackEnabled);
        var kids = ReadChildren(ref r);
        var meter = ReadMeter(ref r);
        SkipStingers(ref r);
        SkipTransitionRules(ref r);
        bool cont = r.U8() != 0;
        uint depth = r.U32();
        var groupIds = new uint[depth];
        for (int i = 0; i < depth; i++) groupIds[i] = r.U32();
        var args = new List<(uint, byte)>((int)depth);
        for (int i = 0; i < depth; i++) args.Add((groupIds[i], r.U8()));
        uint treeSize = r.U32();
        byte mode = r.U8();
        if (treeSize % 12 != 0) throw new InvalidDataException($"decision tree of {treeSize} bytes is not whole nodes");
        var tree = new List<WwiseDecisionNode>((int)(treeSize / 12));
        for (uint i = 0; i < treeSize / 12; i++)
        {
            uint key = r.U32(); uint value = r.U32(); ushort w = r.U16(); ushort pr = r.U16();
            tree.Add(new WwiseDecisionNode(key, value, (ushort)(value & 0xFFFF), (ushort)(value >> 16), w, pr));
        }
        return new WwiseMusicSwitchNode(id, o.Bank, p, kids, flags, meter, cont, args, mode, tree);
    }

    /// <summary>
    /// An LFO (21) or envelope (22) modulator: an id, then the same three blocks a node carries without
    /// the routing around them — the property bundle, the ranged-property bundle and the RTPC list. All
    /// eleven shipped modulators consume exactly under this layout. See <see cref="WwiseModulatorNode"/>.
    /// </summary>
    private static WwiseModulatorNode ReadModulator(ref Reader r, WwiseObject o)
    {
        uint id = r.U32();
        int n = r.U8();
        var ids = new byte[n];
        for (int i = 0; i < n; i++) ids[i] = r.U8();
        var props = new Dictionary<byte, uint>(n);
        for (int i = 0; i < n; i++) props[ids[i]] = r.U32();
        n = r.U8();
        ids = new byte[n];
        for (int i = 0; i < n; i++) ids[i] = r.U8();
        var ranged = new Dictionary<byte, (float, float)>(n);
        for (int i = 0; i < n; i++) ranged[ids[i]] = (r.F32(), r.F32());
        int curves = r.U16();
        var rtpcs = new List<WwiseRtpc>(curves);
        for (int c = 0; c < curves; c++) rtpcs.Add(ReadRtpc(ref r));
        var p = new WwiseNodeParams(0, 0, 0, props, ranged, rtpcs,
            Array.Empty<(uint, byte, IReadOnlyList<(uint, uint)>)>());
        return new WwiseModulatorNode(id, o.Type, o.Bank, p);
    }

    /// <summary>
    /// An audio bus. The field order is the runtime's own (gapD D6.1): no ranged bundle, then the A/B/C bit
    /// bytes, instance limits, channel config, recovery, duck list, FX chain, mixer id and RTPCs. The
    /// conditional A/B/C bodies are not in the frozen rows; the shipped buses leave them clear, and a set
    /// bit fails rather than guessing a body length. See <see cref="WwiseBusNode"/>.
    /// </summary>
    private static WwiseBusNode ReadBus(ref Reader r, WwiseObject o)
    {
        uint id = r.U32();
        uint parent = r.U32();
        int n = r.U8();
        var ids = new byte[n];
        for (int i = 0; i < n; i++) ids[i] = r.U8();
        var props = new Dictionary<byte, uint>(n);
        for (int i = 0; i < n; i++) props[ids[i]] = r.U32();
        // A bus has no ranged-property bundle.

        byte a = r.U8();                                        // A: b0 → +0x46 b7, b1 → +0x47 b0
        byte b = r.U8();                                        // B: b0..b3 → virtual-voice / aux handling
        if ((b & 0x0F) != 0)
            throw new InvalidDataException("bus B conditional body is not recovered in the frozen rows");
        ushort maxInst = (ushort)(r.U16() & 0x3FF);
        uint channelConfig = r.U32();
        byte c = r.U8();                                        // C: b0 → +0x40, b1 → +0xCC
        uint recoveryMs = r.U32();
        float maxDuck = r.F32();
        uint ducks = r.U32();
        var ducked = new List<(uint, float, uint, uint)>((int)Math.Min(ducks, 32));
        for (uint i = 0; i < ducks; i++)
        {
            uint bus = r.U32(); float volume = r.F32(); uint fadeOut = r.U32(); uint fadeIn = r.U32();
            r.U8(); r.U8();                                     // fade curve and the property it ducks
            ducked.Add((bus, volume, fadeOut, fadeIn));
        }

        int numFx = r.U8();
        var effects = new List<WwiseBusEffect>(numFx);
        if (numFx > 0)
        {
            r.U8();                                             // bypass bits
            for (int i = 0; i < numFx; i++)
                effects.Add(new WwiseBusEffect(r.U8(), r.U32(), r.U8() != 0, r.U8() != 0));
        }
        r.U32(); r.U8();                                        // mixer id + byte (vt+0xE0)
        r.U8();                                                 // +0x45 b5
        int curves = r.U16();
        var rtpcs = new List<WwiseRtpc>(curves);
        for (int i = 0; i < curves; i++) rtpcs.Add(ReadRtpc(ref r));
        uint groups = r.U32();
        var stateGroups = new List<(uint, byte, IReadOnlyList<(uint, uint)>)>((int)Math.Min(groups, 64));
        for (uint g = 0; g < groups; g++)
        {
            uint gid = r.U32(); byte sync = r.U8(); int ns = r.U16();
            var states = new List<(uint, uint)>(ns);
            for (int i = 0; i < ns; i++) states.Add((r.U32(), r.U32()));
            stateGroups.Add((gid, sync, states));
        }
        if (o.FeedbackEnabled) r.Skip(4);                       // only when the BKHD feedback flag is set
        _ = a; _ = c; _ = maxInst; _ = channelConfig; _ = recoveryMs; _ = maxDuck;
        var p = new WwiseNodeParams(0, parent, 0, props, new Dictionary<byte, (float, float)>(), rtpcs, stateGroups);
        return new WwiseBusNode(id, o.Bank, p, effects, ducked);
    }

    /// <summary>
    /// An effect share set or custom instance: id, plug-in, and the plug-in's own parameter block, then a
    /// media list, an RTPC list and a two-byte trailer. All 89 shipped effects consume exactly.
    /// </summary>
    private static WwiseEffectNode ReadEffect(ref Reader r, WwiseObject o)
    {
        uint id = r.U32();
        uint plugin = r.U32();
        int size = (int)r.U32();
        var parameters = r.Bytes(size).ToArray();
        int media = r.U8();
        for (int i = 0; i < media; i++) { r.U8(); r.U32(); }
        int curves = r.U16();
        for (int c = 0; c < curves; c++) ReadRtpc(ref r);
        r.U16();
        return new WwiseEffectNode(id, o.Type, o.Bank, plugin, parameters);
    }

    private static WwiseMusicPlaylistNode ReadPlaylist(ref Reader r, WwiseObject o)
    {
        uint id = r.U32();
        byte flags = r.U8();
        var p = ReadNodeParams(ref r, o.FeedbackEnabled);
        var kids = ReadChildren(ref r);
        var meter = ReadMeter(ref r);
        SkipStingers(ref r);
        SkipTransitionRules(ref r);
        uint n = r.U32();
        var items = new List<WwisePlaylistItem>((int)Math.Min(n, 256));
        for (uint i = 0; i < n; i++)
            items.Add(new WwisePlaylistItem(r.U32(), r.U32(), r.U32(), r.S32(), r.U16(), r.U16(), r.U16(), r.U32(), r.U16(), r.U8() != 0, r.U8() != 0));
        return new WwiseMusicPlaylistNode(id, o.Bank, p, kids, flags, meter, items);
    }

    /// <summary>A bounds-checked little-endian cursor. Reading past the end throws, which TryRead reports.</summary>
    private ref struct Reader
    {
        private readonly ReadOnlySpan<byte> _s;
        private int _p;
        public Reader(ReadOnlySpan<byte> s) { _s = s; _p = 0; }
        public int Remaining => _s.Length - _p;
        public byte U8() => _s[_p++];
        public ushort U16() { var v = BinaryPrimitives.ReadUInt16LittleEndian(_s.Slice(_p, 2)); _p += 2; return v; }
        public uint U32() { var v = BinaryPrimitives.ReadUInt32LittleEndian(_s.Slice(_p, 4)); _p += 4; return v; }
        public int S32() { var v = BinaryPrimitives.ReadInt32LittleEndian(_s.Slice(_p, 4)); _p += 4; return v; }
        public float F32() { var v = BinaryPrimitives.ReadSingleLittleEndian(_s.Slice(_p, 4)); _p += 4; return v; }
        public double F64() { var v = BinaryPrimitives.ReadDoubleLittleEndian(_s.Slice(_p, 8)); _p += 8; return v; }
        public ReadOnlySpan<byte> Bytes(int n) { var v = _s.Slice(_p, n); _p += n; return v; }
        public void Skip(int n) { if (_p + n > _s.Length) throw new IndexOutOfRangeException(); _p += n; }
    }
}
