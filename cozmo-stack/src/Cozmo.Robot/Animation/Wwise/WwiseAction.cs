// fidelity: M6-006

using System.Buffers.Binary;

namespace Cozmo.Robot.Animation.Wwise;

/// <summary>
/// The type-specific parameters a bank Action carries after the common fields (gapA 1.7, 1.9).
/// The factory at 0xA60C1C switches on <c>(type &amp; 0xFF00)</c>; only the three kinds Cozmo.bnk uses
/// (Play, Stop, Seek) have recovered bodies. A kind whose body is not recovered is refused by
/// <see cref="WwiseAction.TryRead"/> rather than parsed with a guessed length.
/// </summary>
public abstract record WwiseActionParams;

/// <summary>
/// Play params (vfunc +0x28 = 0xA62984, gapA 1.9): a u8 fade curve and a u32 bank id. All 721 shipped
/// Play actions have exactly these five trailing bytes.
/// </summary>
/// <param name="FadeCurve">
/// The u8 stored into +0x22 bits 0..4. Play execute reads the curve back as <c>+0x22 &amp; 0x1F</c>
/// (gapA 1.11), so the value is masked to five bits here.
/// </param>
/// <param name="BankId">The u32 stored at +0x24.</param>
public sealed record WwisePlayParams(byte FadeCurve, uint BankId) : WwiseActionParams;

/// <summary>
/// Stop params (0xA79E30, 0xA622A4; gapA 1.9, gapD D5.2): a u8 fade curve, a type-specific reader that
/// consumes zero bytes in every shipped Stop (0xA60284, correcting gapA 1.9's 0xA663C8), then an
/// exception list of u32 count × {u32 id, u8 isBus}.
/// </summary>
public sealed record WwiseStopParams(byte FadeCurve, IReadOnlyList<(uint Id, bool IsBus)> Exceptions)
    : WwiseActionParams;

/// <summary>
/// Seek params (0xA64460; gapA 1.9, gapD D5.3): u8 relative, f32 value, f32 min, f32 max, u8 snap, then
/// the same exception list as Stop.
/// </summary>
/// <remarks>
/// The bank order above is gapA 1.9's. gapD D5.3 describes the execute body against internal offsets
/// (+0x30 value, +0x34 min, +0x38 max, +0x3C b0 percent, +0x3D snap) that do not line up field for
/// field with that order. The parse follows the explicit bank layout; the execute body's field mapping
/// is left unresolved and Seek execute does not apply a value (see <see cref="WwiseEventRuntime"/>).
/// </remarks>
public sealed record WwiseSeekParams(byte Relative, float Value, float Min, float Max, byte Snap,
                                     IReadOnlyList<(uint Id, bool IsBus)> Exceptions) : WwiseActionParams;

/// <summary>
/// One parsed bank Action (gapA 1.7). The common layout is fixed for every action type:
/// <code>
/// u32 id; u16 type; u32 target id; u8 isBus;
/// property bundle: u8 n, n × u8 id, n × u32 value;
/// ranged bundle:   u8 m, m × u8 id, m × (f32 min, f32 max);
/// then the type-specific params (factory 0xA60C1C).
/// </code>
///
/// <para><b>Scope (gapA 1.4).</b> The low bit of <see cref="Type"/> is the scope: set means the action
/// acts on a game object (0x0403, 0x0103, 0x1901) and is skipped when there is none; clear means it is
/// global (0x0102, 0x1204) and runs with a null game object.</para>
///
/// <para><b>Delay (gapA 1.6).</b> At load time the runtime converts property 0x0F and both ranged 0x0F
/// bounds from milliseconds to samples as <c>v·rate/1000</c> with 64-bit division, where <c>rate</c> is
/// the global at GOT 0xFFFFFDDC. That global's value and source are not recovered; the stack substitutes
/// <see cref="WwiseRuntimeSettings.MixRateHz"/> and says so in <see cref="DelaySamples"/>. The ranged
/// bundle is f32, so the native integer-division step does not describe it exactly; that detail is
/// recorded as unresolved and no shipped action exercises it (the only delayed action in the shipped
/// banks, Stop 0x23D2D2C6, has a plain property 0x0F of 200 ms and no ranged 0x0F — gapD D1.10).</para>
/// </summary>
public sealed record WwiseAction(
    uint Id, ushort Type, uint TargetId, bool IsBus,
    IReadOnlyDictionary<byte, uint> Props,
    IReadOnlyDictionary<byte, (float Min, float Max)> RangedProps,
    WwiseActionParams Params)
{
    /// <summary>The action's kind: the high byte of the type (the factory's switch value).</summary>
    public byte Kind => (byte)(Type >> 8);

    /// <summary>True when bit 0 of the type is set: the action needs a game object (gapA 1.4).</summary>
    public bool ObjectScope => (Type & 1) != 0;

    /// <summary>A property value as the float most action properties are, or null when absent.</summary>
    public float? FloatProp(byte id) =>
        Props.TryGetValue(id, out var v) ? BitConverter.Int32BitsToSingle((int)v) : null;

    /// <summary>The raw 32-bit property value, or null when absent.</summary>
    public uint? RawProp(byte id) => Props.TryGetValue(id, out var v) ? v : null;

    /// <summary>
    /// Property 0x0F (DelayTime) converted from milliseconds to samples at load, or null when absent
    /// (gapA 1.6). The conversion rate is <see cref="WwiseRuntimeSettings.MixRateHz"/>, substituted for
    /// the unrecovered native global.
    /// </summary>
    public long? DelaySamples =>
        RawProp((byte)WwiseProp.DelayTime) is { } v ? MsToSamples(v) : null;

    /// <summary>
    /// The ranged 0x0F bounds converted from milliseconds to samples at load, or null when absent
    /// (gapA 1.6). See the record remarks for the unresolved f32 conversion detail.
    /// </summary>
    public (long Min, long Max)? RangedDelaySamples =>
        RangedProps.TryGetValue((byte)WwiseProp.DelayTime, out var r)
            ? (MsToSamples(r.Min), MsToSamples(r.Max)) : null;

    /// <summary>Milliseconds to samples: <c>v·rate/1000</c> (gapA 1.6) at the stack's mix rate.</summary>
    private static long MsToSamples(double ms) => (long)(ms * WwiseRuntimeSettings.MixRateHz / 1000.0);

    /// <summary>
    /// Reads one action, requiring the payload to be consumed exactly. Returns null with a reason for a
    /// non-action object, a kind whose type-specific body is not recovered, or a truncated payload.
    /// </summary>
    public static WwiseAction? TryRead(WwiseObject o, out string? problem)
    {
        problem = null;
        if (o.Type != WwiseObjectType.EventAction)
        {
            problem = $"type {(byte)o.Type} is not an action";
            return null;
        }
        try
        {
            var r = new Reader(o.Payload.Span);
            uint id = r.U32();
            ushort type = r.U16();
            uint target = r.U32();
            bool isBus = r.U8() != 0;

            int n = r.U8();
            var ids = new byte[n];
            for (int i = 0; i < n; i++) ids[i] = r.U8();
            var props = new Dictionary<byte, uint>(n);
            for (int i = 0; i < n; i++) props[ids[i]] = r.U32();

            int m = r.U8();
            var rangedIds = new byte[m];
            for (int i = 0; i < m; i++) rangedIds[i] = r.U8();
            var ranged = new Dictionary<byte, (float, float)>(m);
            for (int i = 0; i < m; i++) ranged[rangedIds[i]] = (r.F32(), r.F32());

            WwiseActionParams p;
            switch ((byte)(type >> 8))
            {
                case 0x04:                                              // Play (0xA62984)
                    p = new WwisePlayParams((byte)(r.U8() & 0x1F), r.U32());
                    break;
                case 0x01:                                              // Stop (0xA79E30 + 0xA622A4)
                    p = new WwiseStopParams(r.U8(), ReadExceptions(ref r));
                    break;
                case 0x1E:                                              // Seek (0xA64460)
                    p = new WwiseSeekParams(r.U8(), r.F32(), r.F32(), r.F32(), r.U8(), ReadExceptions(ref r));
                    break;
                default:
                    problem = $"action {id} type 0x{type:X4}: type-specific params are not recovered in the frozen rows";
                    return null;
            }

            if (r.Remaining != 0)
            {
                problem = $"action {id} type 0x{type:X4}: {r.Remaining} bytes left of {o.Payload.Length}";
                return null;
            }
            return new WwiseAction(id, type, target, isBus, props, ranged, p);
        }
        catch (Exception ex) when (ex is ArgumentOutOfRangeException or IndexOutOfRangeException)
        {
            problem = $"action {o.Id}: ran past the end ({ex.Message})";
            return null;
        }
    }

    /// <summary>The Stop/Seek exception list: u32 count × {u32 id, u8 isBus} (0xA622A4, gapA 1.9).</summary>
    private static IReadOnlyList<(uint Id, bool IsBus)> ReadExceptions(ref Reader r)
    {
        uint count = r.U32();
        var list = new List<(uint, bool)>((int)Math.Min(count, 64));
        for (uint i = 0; i < count; i++) list.Add((r.U32(), r.U8() != 0));
        return list;
    }

    /// <summary>A bounds-checked little-endian cursor; reading past the end throws.</summary>
    private ref struct Reader
    {
        private readonly ReadOnlySpan<byte> _s;
        private int _p;
        public Reader(ReadOnlySpan<byte> s) { _s = s; _p = 0; }
        public int Remaining => _s.Length - _p;
        public byte U8() => _s[_p++];
        public ushort U16() { var v = BinaryPrimitives.ReadUInt16LittleEndian(_s.Slice(_p, 2)); _p += 2; return v; }
        public uint U32() { var v = BinaryPrimitives.ReadUInt32LittleEndian(_s.Slice(_p, 4)); _p += 4; return v; }
        public float F32() { var v = BinaryPrimitives.ReadSingleLittleEndian(_s.Slice(_p, 4)); _p += 4; return v; }
    }
}
