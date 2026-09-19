using System.Buffers.Binary;

namespace Cozmo.Robot.Animation.Wwise;

/// <summary>One note from a Wwise MIDI source: where it starts and how long it lasts, in ticks.</summary>
public readonly record struct WwiseMidiNote(uint StartTick, uint LengthTicks, byte Channel, byte Key, byte Velocity);

/// <summary>
/// The MIDI sources the Cozmo_Sings songs are built from, as the bank stores them.
///
/// The 46 music tracks in Cozmo.bnk use plugin id 0x00100001, Wwise's MIDI codec, and their sources are
/// small blobs embedded in the bank (173 to 5,831 bytes; the M6 notes called them "plugin blobs beginning
/// 25 80 00 00" and left them alone). Each is a six-byte header followed by an ordinary Standard MIDI File
/// track: delta-time variable-length quantities, running status, channel messages, and an end-of-track
/// meta event. The header is the SMF division as a big-endian 16-bit value, 0x2580 = 9600 ticks per beat,
/// then a little-endian float tempo (80, 100, 110, 120, 160, 200 or 240 bpm across the songs).
///
/// Two facts about how the numbers are meant were settled against the shipped tracks rather than assumed.
/// First, the division is 9600 ticks per beat: for all 46 tracks the source duration Wwise wrote into the
/// track's clip equals end-of-track ticks / 9600 beats at the hierarchy's tempo, to the millisecond.
/// Second, that tempo is the nearest music node above the clip whose meter overrides its parent's
/// (<see cref="WwiseMusic.EffectiveTempo"/>), not the float in this header: nine headers disagree with the
/// duration Wwise computed, and every one of the 46 durations fits the hierarchy rule. So the file tempo is
/// carried but not used. Each sequence ends with two end-of-track meta events; the later one is the end.
/// </summary>
public sealed class WwiseMidi
{
    /// <summary>The header's division: ticks per quarter note. 9600 in every shipped song.</summary>
    public ushort TicksPerBeat { get; }
    /// <summary>The header's tempo. Recorded; the music hierarchy's tempo is what Wwise plays at.</summary>
    public float FileTempoBpm { get; }
    /// <summary>The tick of the end-of-track event, or of the last event when there is none.</summary>
    public uint EndTick { get; }
    /// <summary>Every note, in start order.</summary>
    public IReadOnlyList<WwiseMidiNote> Notes { get; }
    /// <summary>Program-change and controller messages seen, counted by status byte, for reporting.</summary>
    public IReadOnlyDictionary<byte, int> OtherMessages { get; }

    private WwiseMidi(ushort division, float tempo, uint end, List<WwiseMidiNote> notes, Dictionary<byte, int> other)
    {
        TicksPerBeat = division; FileTempoBpm = tempo; EndTick = end; Notes = notes; OtherMessages = other;
    }

    /// <summary>Milliseconds per tick at a given tempo.</summary>
    public double MsPerTick(float tempoBpm) => 60000.0 / tempoBpm / TicksPerBeat;

    /// <summary>The note timeline in milliseconds at the given tempo: start, length, key, velocity.</summary>
    public IEnumerable<(double StartMs, double LengthMs, byte Key, byte Velocity, byte Channel)> NotesAt(float tempoBpm)
    {
        double k = MsPerTick(tempoBpm);
        foreach (var n in Notes) yield return (n.StartTick * k, n.LengthTicks * k, n.Key, n.Velocity, n.Channel);
    }

    /// <summary>Parses a source blob. Throws <see cref="InvalidDataException"/> when it is not this shape.</summary>
    public static WwiseMidi Parse(ReadOnlySpan<byte> b)
    {
        if (b.Length < 7) throw new InvalidDataException("MIDI source shorter than its header");
        ushort division = BinaryPrimitives.ReadUInt16BigEndian(b[..2]);
        float tempo = BinaryPrimitives.ReadSingleLittleEndian(b.Slice(2, 4));
        if (division == 0 || (division & 0x8000) != 0) throw new InvalidDataException($"unsupported MIDI division 0x{division:X4}");

        var notes = new List<WwiseMidiNote>();
        var other = new Dictionary<byte, int>();
        var open = new Dictionary<int, (uint Tick, byte Velocity)>();   // (channel << 8 | key) -> note-on
        int p = 6; uint tick = 0; byte status = 0; uint end = 0; bool sawEnd = false;
        while (p < b.Length)
        {
            tick += ReadVarint(b, ref p);
            if (p >= b.Length) break;
            if ((b[p] & 0x80) != 0) status = b[p++];
            if (status == 0xFF)
            {
                if (p >= b.Length) throw new InvalidDataException("truncated meta event");
                byte type = b[p++];
                uint len = ReadVarint(b, ref p);
                p += (int)len;
                if (type == 0x2F) { end = tick; sawEnd = true; }
                continue;
            }
            if (status == 0xF0 || status == 0xF7) { uint len = ReadVarint(b, ref p); p += (int)len; continue; }
            if (status < 0x80) throw new InvalidDataException("data byte with no running status");
            byte hi = (byte)(status & 0xF0), ch = (byte)(status & 0x0F);
            if (hi == 0xC0 || hi == 0xD0)
            {
                p += 1;
                other[hi] = other.GetValueOrDefault(hi) + 1;
                continue;
            }
            if (p + 2 > b.Length) throw new InvalidDataException("truncated channel message");
            byte d1 = b[p], d2 = b[p + 1]; p += 2;
            int slot = (ch << 8) | d1;
            if (hi == 0x90 && d2 > 0) { open[slot] = (tick, d2); continue; }
            if (hi == 0x80 || hi == 0x90)
            {
                if (open.Remove(slot, out var on)) notes.Add(new WwiseMidiNote(on.Tick, tick - on.Tick, ch, d1, on.Velocity));
                continue;
            }
            other[hi] = other.GetValueOrDefault(hi) + 1;
        }
        if (!sawEnd) end = tick;
        notes.Sort((x, y) => x.StartTick != y.StartTick ? x.StartTick.CompareTo(y.StartTick) : x.Key.CompareTo(y.Key));
        return new WwiseMidi(division, tempo, end, notes, other);
    }

    private static uint ReadVarint(ReadOnlySpan<byte> b, ref int p)
    {
        uint v = 0;
        for (int i = 0; i < 4; i++)
        {
            if (p >= b.Length) throw new InvalidDataException("truncated variable-length quantity");
            byte c = b[p++];
            v = (v << 7) | (uint)(c & 0x7F);
            if ((c & 0x80) == 0) return v;
        }
        throw new InvalidDataException("variable-length quantity longer than four bytes");
    }
}
