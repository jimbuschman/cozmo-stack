using System.Buffers.Binary;

namespace Cozmo.Robot.Animation.Wwise;

/// <summary>
/// One entry of the STMG parameter table: an RTPC's default value and ramp. The reader stores the default
/// at the value-store entry's <c>+8</c> and the ramp fields at <c>+0xC/+0x10/+0x14</c> (gapF 1.2).
/// </summary>
public sealed record WwiseStmgParam(uint Id, float Value, uint RampType, float RampUp, float RampDown, bool BuiltIn);

/// <summary>A state group. Its per-state triples are kept raw: the frozen rows read the fields but do not name them.</summary>
public sealed record WwiseStmgStateGroup(uint Id, uint DefaultTransitionMs, IReadOnlyList<(uint A, uint B, uint C)> States);

/// <summary>A switch group. Its per-switch triples are kept raw: the frozen rows read the fields but do not name them.</summary>
public sealed record WwiseStmgSwitchGroup(uint Id, uint RtpcId, byte Flags, IReadOnlyList<(uint A, uint B, uint C)> Switches);

/// <summary>
/// The STMG chunk of <c>Init.bnk</c>: the state manager's state groups, switch→RTPC groups, and the RTPC
/// default table. The defaults are what the value store falls back to, so a voice whose playing id has no
/// value still evaluates at 1.0 (gapF 1.2, 1.3, 1.6).
///
/// The field order below is the native reader at 0x9B0B14, recovered from its disassembly:
/// <list type="number">
/// <item>f32 volume threshold; u16 max voices;</item>
/// <item>u32 state-group count; each <c>{u32 id, u32 default-transition-ms, u32 n, n × 12 bytes}</c>;</item>
/// <item>u32 switch-group count; each <c>{u32 id, u32 rtpc, u8 flags, u32 n, n × 12 bytes}</c>;</item>
/// <item>u32 parameter count; each <c>{u32 id, f32 value, u32 ramp-type, f32 up, f32 down, u8 built-in}</c>;</item>
/// <item>two further u32 counts whose entries are not recovered; both are zero in the shipped chunk.</item>
/// </list>
/// The disassembly's third switch-group field is a one-byte read, and the shipped chunk lands exactly on its
/// parameter table only under that reading (37 parameters at offset 306, ending 8 bytes short). A later
/// count that is non-zero, or any overrun/leftover, is refused rather than realigned.
/// </summary>
public sealed class WwiseStmg
{
    public float VolumeThreshold { get; private init; }
    public int MaxVoices { get; private init; }
    public IReadOnlyList<WwiseStmgStateGroup> StateGroups { get; private init; } = Array.Empty<WwiseStmgStateGroup>();
    public IReadOnlyList<WwiseStmgSwitchGroup> SwitchGroups { get; private init; } = Array.Empty<WwiseStmgSwitchGroup>();
    /// <summary>The RTPC defaults, by parameter id.</summary>
    public IReadOnlyDictionary<uint, WwiseStmgParam> Params { get; private init; } = new Dictionary<uint, WwiseStmgParam>();

    /// <summary>The STMG default for a parameter id, or null when the chunk does not list it.</summary>
    public float? DefaultOf(uint paramId) => Params.TryGetValue(paramId, out var p) ? p.Value : null;

    public static WwiseStmg Parse(ReadOnlyMemory<byte> body, string bank)
    {
        var r = new Reader(body.Span, bank);
        float threshold = r.F32();
        int maxVoices = r.U16();

        uint stateGroups = r.U32();
        var states = new List<WwiseStmgStateGroup>((int)Math.Min(stateGroups, 256));
        for (uint g = 0; g < stateGroups; g++)
        {
            uint id = r.U32(); uint transition = r.U32(); uint n = r.U32();
            var items = new List<(uint, uint, uint)>((int)Math.Min(n, 256));
            for (uint i = 0; i < n; i++) items.Add((r.U32(), r.U32(), r.U32()));
            states.Add(new WwiseStmgStateGroup(id, transition, items));
        }

        uint switchGroups = r.U32();
        var switches = new List<WwiseStmgSwitchGroup>((int)Math.Min(switchGroups, 256));
        for (uint g = 0; g < switchGroups; g++)
        {
            uint id = r.U32(); uint rtpc = r.U32(); byte flags = r.U8(); uint n = r.U32();
            var items = new List<(uint, uint, uint)>((int)Math.Min(n, 256));
            for (uint i = 0; i < n; i++) items.Add((r.U32(), r.U32(), r.U32()));
            switches.Add(new WwiseStmgSwitchGroup(id, rtpc, flags, items));
        }

        uint paramCount = r.U32();
        var pars = new Dictionary<uint, WwiseStmgParam>((int)Math.Min(paramCount, 1024));
        for (uint i = 0; i < paramCount; i++)
        {
            uint id = r.U32(); float value = r.F32(); uint rampType = r.U32();
            float up = r.F32(); float down = r.F32(); bool builtIn = r.U8() != 0;
            pars[id] = new WwiseStmgParam(id, value, rampType, up, down, builtIn);
        }

        // Two more counts follow the parameter table. Their entry bodies are not recovered, and both are
        // zero in the shipped Init.bnk, so a non-zero count is refused rather than guessed.
        uint laterA = r.U32();
        if (laterA != 0) throw new InvalidDataException($"{bank}: STMG has {laterA} entries in an unrecovered section");
        uint laterB = r.U32();
        if (laterB != 0) throw new InvalidDataException($"{bank}: STMG has {laterB} entries in a second unrecovered section");

        if (r.Remaining != 0) throw new InvalidDataException($"{bank}: STMG has {r.Remaining} bytes left over");
        return new WwiseStmg
        {
            VolumeThreshold = threshold, MaxVoices = maxVoices,
            StateGroups = states, SwitchGroups = switches, Params = pars,
        };
    }

    private ref struct Reader
    {
        private readonly ReadOnlySpan<byte> _s;
        private readonly string _bank;
        private int _p;
        public Reader(ReadOnlySpan<byte> s, string bank) { _s = s; _bank = bank; _p = 0; }
        public int Remaining => _s.Length - _p;
        private void Need(int n) { if (_p + n > _s.Length) throw new InvalidDataException($"{_bank}: STMG ends inside a field"); }
        public byte U8() { Need(1); return _s[_p++]; }
        public int U16() { Need(2); var v = BinaryPrimitives.ReadUInt16LittleEndian(_s.Slice(_p, 2)); _p += 2; return v; }
        public uint U32() { Need(4); var v = BinaryPrimitives.ReadUInt32LittleEndian(_s.Slice(_p, 4)); _p += 4; return v; }
        public float F32() { Need(4); var v = BinaryPrimitives.ReadSingleLittleEndian(_s.Slice(_p, 4)); _p += 4; return v; }
    }
}
