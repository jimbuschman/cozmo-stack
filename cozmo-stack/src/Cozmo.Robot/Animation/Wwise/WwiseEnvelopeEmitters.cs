// fidelity: M9-036
namespace Cozmo.Robot.Animation.Wwise;

/// <summary>The checked release/decay/attack-1 NEON emitters, also shared by explicitly supplied bridge ramps.</summary>
public static class WwiseEnvelopeEmitters
{
    // 9E58E0 / 9E59EC / 9E5B0C. Count and stage scheduling are separate inputs.
    public static float Ramp(Span<float> output, float start, float slope, ref float maximum)
    {
        if (output.Length == 0) return start;
        if ((output.Length & 3) != 0) throw new ArgumentException("native modulation blocks must be multiples of four");
        float h2 = slope + slope, h3 = slope * 3f, h4 = slope * 4f;
        output[0] = 0f + start; output[1] = slope + start;
        output[3] = h3 + start; output[2] = h2 + start;
        for (int i = 4; i < output.Length; i += 4)
            for (int lane = 0; lane < 4; lane++) output[i + lane] = h4 + output[i + lane - 4];
        float carry = h4 + output[^4];
        if (carry < 0f) carry = 0f;
        else if (carry > 1f) carry = 1f;
        // ARM unordered compares take PL, keeping start and the old maximum.
        float candidate = start < carry ? carry : start;
        if (maximum < candidate) maximum = candidate;
        return carry;
    }

    public static float Sustain(Span<float> output, float value, ref float maximum)
    { output.Fill(value); if (maximum < value) maximum = value; return value; }
    public static float Delay(Span<float> output, ref float maximum)
    { output.Clear(); if (maximum < 0f) maximum = 0f; return 0f; }
}

public sealed class WwiseModulationMaximumState
{
    public WwiseTransitionState? Output30 { get; set; }
    public float Current4C { get; set; }
}
public readonly record struct WwiseModulationBinding(float Bias, float Scale, uint Parameter);
public sealed class WwiseModulationBindingNode
{
    public required WwiseModulationMaximumState State { get; init; }
    public List<WwiseModulationBinding> Bindings { get; } = new();
    public WwiseModulationBindingNode? Next { get; set; }
}
public static class WwiseModulationMaximum
{
    // 9E84C8..9E859C: no buffer-pointer test; output+C is distinct from current/output+8.
    public static float Product(WwiseModulationBindingNode? head, uint lowMask, uint highMask)
    {
        float product = 1f;
        for (var node = head; node is not null; node = node.Next)
            foreach (var binding in node.Bindings)
            {
                uint id = binding.Parameter;
                uint selectedBit = RShift(lowMask, id) | LShift(highMask, unchecked(32u - id)) |
                    RShift(highMask, unchecked(id - 32u));
                bool selected = (selectedBit & 1) != 0;
                if (!selected) continue;
                float x = node.State.Output30?.RunningMax ?? node.State.Current4C;
                float factor = binding.Scale * x; factor = binding.Bias + factor; // non-fused VMLA.F32
                product *= factor;
            }
        return product;
    }
    private static uint RShift(uint value, uint count) => (count & 0xFF) < 32 ? value >> (int)(count & 0xFF) : 0;
    private static uint LShift(uint value, uint count) => (count & 0xFF) < 32 ? value << (int)(count & 0xFF) : 0;
    // A55C78: product is the first operand; A4AF50's opposite order stays with that connection owner.
    public static float VolumeFactor(float base1C, WwiseModulationBindingNode? owner28)
        => Product(owner28, 1, 0) * base1C;
}
