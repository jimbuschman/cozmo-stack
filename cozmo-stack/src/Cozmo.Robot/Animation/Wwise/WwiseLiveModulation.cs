// fidelity: M9-038
namespace Cozmo.Robot.Animation.Wwise;

public interface IWwiseLiveModulatorState
{
    bool SetParameter(uint index, float value);
}

/// <summary>9E203C setter stores and 9E2ED0/9E2EF0 capture. No new note or phase reset.</summary>
public sealed class WwiseLiveLfoState : IWwiseLiveModulatorState
{
    public float Depth74 { get; private set; }
    public int Attack78 { get; private set; }
    public float Frequency84 { get; private set; }
    public uint Waveform80 { get; private set; }
    public float Smoothing88 { get; private set; }
    public float Pwm8C { get; private set; }
    public bool DirtyA8 { get; private set; }
    public uint? SampleRate { get; set; }
    public Action? GeometryVirtual10 { get; set; }

    public bool SetParameter(uint index, float value)
    {
        switch (index)
        {
            case 0: Depth74 = value / 100f; return true;
            case 1:
                if (SampleRate is not { } rate)
                { WwiseMidiRuntime.Missing("M9-038 sample-rate global input at 0x009E20A8"); return false; }
                double samples = (double)value * rate;
                Attack78 = WwiseMidiTiming.Convert(samples + (samples > 0 ? 0.5 : -0.5)); return true;
            case 2:
                Frequency84 = value; DirtyA8 = true;
                if (GeometryVirtual10 is { } geometry) geometry();
                else WwiseMidiRuntime.Missing("M9-042 LFO geometry vt+0x10 called at 0x009E20F4");
                return true;
            case 3: Waveform80 = SaturateU32(value); DirtyA8 = true; return true;
            case 4: Smoothing88 = value / 100f; DirtyA8 = true; return true;
            case 5: Pwm8C = value / 100f; DirtyA8 = true; return true;
            default: return false;
        }
    }
    internal static uint SaturateU32(float value) => float.IsNaN(value) || value <= 0 ? 0
        : (double)value >= uint.MaxValue ? uint.MaxValue : (uint)value;

    public void Capture(WwiseModulatorRecord0 record)
    {
        if (DirtyA8) WwiseMidiRuntime.Missing("M9-042 LFO coefficient-refresh branch 0x009E35C0 before capture");
        record.Start18 = Depth74; record.I1c = Attack78;
        record.Count28 = BitConverter.SingleToInt32Bits(Frequency84);
        record.Sustain24 = BitConverter.UInt32BitsToSingle(Waveform80);
        record.Count2c = BitConverter.SingleToInt32Bits(Smoothing88); record.F30 = Pwm8C;
        // These fields are native record words. The evaluator's wider initialization remains M9-042.
    }
}

/// <summary>Explicit native tree population, not a substitute for unread state insertion.</summary>
public sealed class WwiseModulatorStateBranch
{
    public IWwiseLiveModulatorState? State { get; set; }
    public SortedList<uint, WwiseModulatorStateBranch> Children { get; } = new();
}

public sealed class WwiseModulatorStateTree
{
    public int Scope { get; set; }
    public IWwiseLiveModulatorState? RootState { get; set; }
    public SortedList<uint, WwiseModulatorStateBranch> Objects { get; } = new();

    public void ReportMissingInsertion() => WwiseMidiRuntime.Missing(
        "M9-042 native state-tree insertion 0x009DD120..0x009DE688; explicit population required");

    // 9DBF78 -> 9D5058 / 9DB810 / 9D9CD8. Ordered array walk, each present prefix before descendants.
    public void Dispatch(uint index, float value, WwiseRtpcScopeKey input)
    {
        var k = input;
        if (Scope >= 1) k = k with { F = 0, B = k.E != 0xFF ? 0 : k.B };
        if (Scope >= 2) k = k with { B = 0, C = 0, D = 0xFF, E = 0xFF };
        if (Scope >= 3) k = k with { A = 0 };
        uint[] keys = [k.A, k.B, k.C, k.D, k.E, k.F];
        if (k == WwiseRtpcScopeKey.Wild) RootState?.SetParameter(index, value);
        Visit(Objects, 0, keys, index, value);
    }

    private static void Visit(SortedList<uint, WwiseModulatorStateBranch> array, int depth, uint[] keys, uint index, float value)
    {
        uint wild = depth == 3 || depth == 4 ? 0xFFu : 0u;
        if (keys[depth] != wild)
        {
            if (array.TryGetValue(keys[depth], out var exact)) Apply(exact, depth, keys, index, value);
            return;
        }
        // Index loop deliberately reads the current arrays; this is not the RTPC subscriber snapshot.
        for (int i = 0; i < array.Count; i++) Apply(array.Values[i], depth, keys, index, value);
    }
    private static void Apply(WwiseModulatorStateBranch branch, int depth, uint[] keys, uint index, float value)
    {
        branch.State?.SetParameter(index, value);
        if (depth < 5) Visit(branch.Children, depth + 1, keys, index, value);
    }
}

