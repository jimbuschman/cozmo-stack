// fidelity: M6-022
namespace Cozmo.Robot.Animation.Wwise;

/// <summary>
/// The five LFO shapes the per-voice modulator evaluator dispatches on (M6-022 V28, 0x9E2BD0, modulator-
/// evaluator report Q1.7). The values are the native's own case numbers: 0 sine, 1 triangle, 2 square,
/// 3 saw up, 4 saw down. The class name of the modulator is UNKNOWN.
/// </summary>
public enum WwiseLfoShape
{
    Sine = 0,
    Triangle = 1,
    Square = 2,
    SawUp = 3,
    SawDown = 4,
}

/// <summary>
/// The five shape functions of the per-voice modulator evaluator (M6-022 V28, modulator-evaluator report
/// Q1.7). Each is a pure function of the phase, so it is testable without the evaluator's pool bookkeeping.
///
/// <para><b>Sine.</b> The native folds the phase into <c>[0, pi/2]</c> and evaluates
/// <c>0.5*(1 + x*(0.9999966 - 0.16664828*x^2 + 0.008306325*x^4 - 0.00018363654*x^6))</c>; the constants
/// are the report's <c>0x3F7FFFC7</c>, <c>0x3E2AA5D9</c>, <c>0x3C081741</c>, <c>0xB9408E8F</c>. At
/// <c>x=0</c> the result is <c>0.5</c> and at <c>x=pi/2</c> it is 1, so the shape is <c>0.5*(1+sin)</c>.</para>
///
/// <para><b>Triangle/square/saw.</b> Over the normalized phase in <c>[0,1)</c>: triangle is
/// <c>2*phase</c> then <c>2*(1-phase)</c>; square is 1 then 0; saw-up is the phase; saw-down is
/// <c>1-phase</c>.</para>
/// </summary>
public static class WwiseLfoShapes
{
    // fidelity: M6-022

    /// <summary>0x9E4014: the sine polynomial's <c>0.9999966</c>.</summary>
    public const float SineC0 = 0.9999966f;
    /// <summary>0x9E4010: the sine polynomial's <c>-0.16664828</c>.</summary>
    public const float SineC1 = -0.16664828f;
    /// <summary>0x9E3324: the sine polynomial's <c>0.008306325</c>.</summary>
    public const float SineC2 = 0.008306325f;
    /// <summary>0x9E3310: the sine polynomial's <c>-0.00018363654</c>.</summary>
    public const float SineC3 = -0.00018363654f;

    /// <summary>
    /// The sine shape at a phase in radians (0x9E416C). Folds to <c>[0, pi/2]</c> with the native's
    /// quadrant signs and evaluates the polynomial; <c>0.5*(1+sin)</c>.
    /// </summary>
    public static float Sine(double phaseRadians)
    {
        double twoPi = 2.0 * Math.PI;
        double x = phaseRadians % twoPi;
        if (x < 0) x += twoPi;
        double sign = 1.0;
        if (x <= Math.PI / 2) { /* quadrant 1 */ }
        else if (x <= Math.PI) { x = Math.PI - x; }
        else if (x <= 3.0 * Math.PI / 2) { x -= Math.PI; sign = -1.0; }
        else { x = twoPi - x; sign = -1.0; }

        double x2 = x * x;
        double poly = x * (SineC0 + x2 * (SineC1 + x2 * (SineC2 + x2 * SineC3)));
        return (float)(0.5 * (1.0 + sign * poly));
    }

    /// <summary>The triangle shape at a normalized phase in <c>[0,1)</c> (0x9E4024).</summary>
    public static float Triangle(double phase)
    {
        double p = Wrap01(phase);
        return (float)(p < 0.5 ? 2.0 * p : 2.0 * (1.0 - p));
    }

    /// <summary>The square shape at a normalized phase in <c>[0,1)</c> (0x9E3ECC): 1 then 0.</summary>
    public static float Square(double phase) => Wrap01(phase) < 0.5 ? 1f : 0f;

    /// <summary>The saw-up shape at a normalized phase in <c>[0,1)</c> (0x9E3E1C).</summary>
    public static float SawUp(double phase) => (float)Wrap01(phase);

    /// <summary>The saw-down shape at a normalized phase in <c>[0,1)</c> (0x9E36D4).</summary>
    public static float SawDown(double phase) => (float)(1.0 - Wrap01(phase));

    /// <summary>Evaluates one shape at a normalized phase in <c>[0,1)</c>; sine takes radians and is refused here.</summary>
    public static float Evaluate(WwiseLfoShape shape, double normalizedPhase) => shape switch
    {
        WwiseLfoShape.Sine => throw new NotSupportedException(
            "the sine shape takes a phase in radians; use WwiseLfoShapes.Sine (0x9E416C)"),
        WwiseLfoShape.Triangle => Triangle(normalizedPhase),
        WwiseLfoShape.Square => Square(normalizedPhase),
        WwiseLfoShape.SawUp => SawUp(normalizedPhase),
        WwiseLfoShape.SawDown => SawDown(normalizedPhase),
        _ => throw new NotSupportedException($"M6-022 V28: LFO shape {(int)shape} is outside 0..4"),
    };

    private static double Wrap01(double phase)
    {
        double p = phase % 1.0;
        return p < 0 ? p + 1.0 : p;
    }
}

/// <summary>
/// The two filter coefficients the modulator evaluator's recurrence uses (M6-022 V28, 0x9E35C0..0x9E3664,
/// correction C15 Y1). <c>c2 = sqrt((2-cos w)^2 - 1) - (2-cos w)</c> and <c>c1 = 1 + c2</c>, with
/// <c>w = 2*pi * (24000/f)^damping * 24000/48000</c> for <c>f &lt;= 48000</c> and
/// <c>w = 2*pi * 0.5^damping * 24000/48000</c> for <c>f &gt; 48000</c>. The damping == 0 branch is the
/// pass-through <c>c1=1, c2=0</c> (0x9E5004). The native's NaN guard 0x9E52E4 is defensive: the argument
/// <c>(2-cos w)^2 - 1</c> is <c>&gt;= 0</c> for every real <c>w</c>.
/// </summary>
public readonly record struct WwiseLfoCoefficients(float C1, float C2)
{
    // fidelity: M6-022

    /// <summary>The native's <c>d9 = 48000.0</c>, <c>d8 = 24000.0</c> and <c>d10 = 2*pi</c> (0x9E2F90/0x9E2F98/0x9E2FA0).</summary>
    public const double MixRateHz = 48000.0;
    /// <summary>Half the mix rate, the coefficient recompute's <c>d8</c> (0x9E2F98).</summary>
    public const double HalfMixRateHz = 24000.0;

    /// <summary>
    /// The coefficient recompute (0x9E35C0..0x9E3664, C15 Y1). <c>damping == 0</c> returns
    /// <c>(1, 0)</c> (0x9E35C4 -> 0x9E5004). Otherwise <c>t = (f &lt;= 48000) ? 24000/f : 0.5</c>,
    /// <c>t = t^(-damping)</c>, <c>w = t*24000/48000*2*pi</c>, <c>a = 2-cos(w)</c>,
    /// <c>c2 = sqrt(a*a - 1) - a</c> (<c>vnmls.f64 d17,d11,d11</c> = <c>a*a - 1</c>, C15 Y1) and
    /// <c>c1 = c2 + 1</c>.
    /// </summary>
    public static WwiseLfoCoefficients FromFrequencyAndDamping(float frequencyHz, float damping)
    {
        if (damping == 0f)
            return new WwiseLfoCoefficients(1f, 0f);                 // 0x9E35C4 -> 0x9E5004

        double f = frequencyHz;
        double t = f <= MixRateHz ? HalfMixRateHz / f : 0.5;         // 0x9E35DC..0x9E35E8
        t = Math.Exp(-damping * Math.Log(t));                        // 0x9E35F0 log, 0x9E3600 vnmul, 0x9E3608 exp
        double w = t * HalfMixRateHz / MixRateHz * (2.0 * Math.PI);  // 0x9E3610..0x9E3618
        double a = 2.0 - Math.Cos(w);                                // 0x9E362C
        double arg = a * a - 1.0;                                    // 0x9E3630 vnmls.f64 d17,d11,d11 (C15 Y1)
        double s = Math.Sqrt(arg);                                   // 0x9E3634; the 0x9E52E4 NaN path is defensive
        float c2 = (float)(s - a);                                   // 0x9E3644/0x9E364C
        float c1 = c2 + 1f;                                          // 0x9E3650 (s12 starts 1.0)
        return new WwiseLfoCoefficients(c1, c2);                     // 0x9E3660/0x9E3664
    }

    /// <summary>
    /// V28-coeff phase increment <c>[r6+0xa0] = (f &lt; 48000) ? f/48000 : 1.0</c>, times <c>2*pi</c> when
    /// <c>shape == 0</c> (0x9E3668..0x9E3688). <paramref name="radians"/> selects the shape-0 case.
    /// </summary>
    public static float PhaseIncrement(float frequencyHz, bool radians)
    {
        float inc = frequencyHz < MixRateHz ? (float)(frequencyHz / MixRateHz) : 1f;
        return radians ? inc * (float)(2.0 * Math.PI) : inc;
    }

    /// <summary>
    /// V28-coeff radians/cycles conversion on a shape change (0x9E369C/0x9E51B0): entering the sine shape
    /// multiplies the cycle phase by <c>2*pi</c>; leaving it multiplies by <c>1/(2*pi)</c>.
    /// </summary>
    public static float ConvertPhase(float phase, bool wasRadians, bool isRadians)
    {
        if (wasRadians == isRadians) return phase;
        return isRadians
            ? phase * (float)(2.0 * Math.PI)
            : phase * (float)(1.0 / (2.0 * Math.PI));
    }
}

/// <summary>
/// The per-voice LFO recurrence (M6-022 V28, modulator-evaluator report Q1.7):
/// <c>out[n] = gain[n] * shape(phase) * c1 - out[n-1] * c2</c>, with the gain ramping from its start by its
/// per-sample step and the phase advancing then wrapping. The native vectorises the loop (NEON) but C15 Y2
/// establishes the recurrence is scalar VFP: there is no lane order to recover. This is that scalar form.
/// </summary>
public sealed class WwiseLfoEvaluator
{
    // fidelity: M6-022

    /// <summary>The last output sample (the native state's <c>+0x08</c>), carried between blocks.</summary>
    public float LastOutput { get; private set; }

    /// <summary>The phase, in radians for the sine shape and in cycles otherwise (the native <c>v+0x9c</c>).</summary>
    public double Phase { get; set; }

    /// <summary>The filter coefficients (the native <c>v+0x94</c>/<c>v+0x98</c>).</summary>
    public WwiseLfoCoefficients Coefficients { get; set; }

    /// <summary>Evaluates <paramref name="count"/> samples into <paramref name="output"/>.</summary>
    /// <param name="shape">The LFO shape (0..4).</param>
    /// <param name="phaseIncrement">The phase step per sample (cycles, or radians for the sine shape).</param>
    /// <param name="gainStart">The first sample's gain.</param>
    /// <param name="gainStep">The per-sample gain step (the native's per-block ramp).</param>
    public void Evaluate(WwiseLfoShape shape, double phaseIncrement, float gainStart, float gainStep, Span<float> output)
    {
        bool radians = shape == WwiseLfoShape.Sine;
        float prev = LastOutput;
        float gain = gainStart;
        double phase = Phase;

        for (int n = 0; n < output.Length; n++)
        {
            float shapeValue = radians ? WwiseLfoShapes.Sine(phase) : WwiseLfoShapes.Evaluate(shape, phase);
            float y = gain * shapeValue * Coefficients.C1 - prev * Coefficients.C2;
            output[n] = y;
            prev = y;

            phase += phaseIncrement;
            if (radians)
            {
                if (phase >= 2.0 * Math.PI) phase -= 2.0 * Math.PI;
            }
            else if (phase >= 1.0)
            {
                phase -= 1.0;
            }

            gain += gainStep;
        }

        LastOutput = prev;
        Phase = phase;
    }
}

/// <summary>
/// One type-0 modulator record (M6-022 V28, modulator-evaluator Q3, 0x4C bytes). The <c>+0x00..+0x2c</c>
/// fields are the common transition block; <c>+0x30..+0x48</c> are the oscillator block. The semantic field
/// names are the report's inferred labels, not symbols.
/// </summary>
public sealed class WwiseModulatorRecord0
{
    // fidelity: M6-022
    public uint Id;                 // +0x00
    public int Count04;             // +0x04
    public int Count08;             // +0x08
    public int Time0c;              // +0x0c
    public float F10;               // +0x10
    public int Mode14;              // +0x14
    public float Start18;           // +0x18
    public int I1c;                 // +0x1c
    public float Breakpoint20;      // +0x20
    public float Sustain24;         // +0x24
    public int Count28;             // +0x28
    public int Count2c;             // +0x2c
    public float F30;               // +0x30
    public float C1_34;             // +0x34 (source+0x10)
    public float C2_38;             // +0x38 (source+0x14)
    public float F3c;               // +0x3c (source+0x18)
    public float Phase40;           // +0x40 (fmodf(source+0x1c))
    public float PhaseInc44;        // +0x44 (source+0x20)
    public int Shape48;             // +0x48 (source+0x24)

    /// <summary>The per-record oscillator state (0x28 bytes) carried between calls.</summary>
    public WwiseLfoEvaluator Oscillator { get; } = new();

    /// <summary>The block output the evaluator fills; grown lazily to <see cref="FloatNeeded"/>.</summary>
    public float[] Output { get; set; } = Array.Empty<float>();
}

/// <summary>
/// One type-1 transition record (M6-022 V28, modulator-evaluator Q3, 0x30 bytes). Consumed by
/// <see cref="WwiseTransitionRamp"/> when <see cref="Mode14"/> is non-zero, or by the inline ramp otherwise.
/// </summary>
public sealed class WwiseModulatorRecord1
{
    // fidelity: M6-022
    public uint Id;                 // +0x00
    public int Count04;             // +0x04
    public int Count08;             // +0x08
    public int Time0c;              // +0x0c
    public float F10;               // +0x10
    public int Mode14;              // +0x14
    public float Start18;           // +0x18
    public int I1c;                 // +0x1c
    public float Breakpoint20;      // +0x20
    public float Sustain24;         // +0x24
    public int Count28;             // +0x28
    public int Count2c;             // +0x2c

    /// <summary>The per-record transition state (0x10 bytes).</summary>
    public WwiseTransitionState State { get; } = new();

    /// <summary>The block output the transition fills.</summary>
    public float[] Output { get; set; } = Array.Empty<float>();
}

/// <summary>
/// The type-1 per-record state (M6-022 V28, modulator-evaluator Q2, 0x10 bytes): <c>+0x00</c> output pointer,
/// <c>+0x04</c> status (-1 then 3), <c>+0x08</c> last value, <c>+0x0c</c> running max.
/// </summary>
public sealed class WwiseTransitionState
{
    // fidelity: M6-022
    public int Status = -1;         // +0x04
    public float Last;              // +0x08
    public float RunningMax;        // +0x0c
}

/// <summary>
/// One chunk node of a modulator pool list (M6-022 V28, modulator-evaluator Q1.1/Q1.9, 0x28 bytes): next at
/// <c>+0</c>, buffer1 <c>+4/+8/+0xC</c>, buffer2 <c>+0x10/+0x14/+0x18</c>, float-buffer
/// <c>+0x1C/+0x20/+0x24</c>. The two lists are type-0 (0x4C records, 16 per node) at
/// <c>[arg+0x20]</c> and type-1 (0x30 records, 16 per node) at <c>[arg+0x14]</c>.
/// </summary>
public sealed class WwiseModulatorChunk<T>
{
    // fidelity: M6-022
    public const int Capacity = 16;                 // 0x9E5080/0x9E5084 mov r8,#0x10

    /// <summary><c>+0</c>: the next chunk in the list.</summary>
    public WwiseModulatorChunk<T>? Next;

    /// <summary><c>+4</c>: the record slots; <c>+8</c> used, <c>+0xC</c> capacity.</summary>
    public List<T> Buffer1 { get; } = new(Capacity);

    /// <summary><c>+0x10</c>: the per-record state slots; <c>+0x14</c> used, <c>+0x18</c> capacity.</summary>
    public List<T> Buffer2 { get; } = new(Capacity);

    /// <summary><c>+0x1C</c>: the float-buffer needed length (floats), recomputed each call.</summary>
    public int FloatNeeded;

    /// <summary><c>+0x20</c>: the float-buffer allocated length (persists).</summary>
    public int FloatAllocated;

    /// <summary><c>+0x24</c>: the float-buffer pointer (persists; grown lazily).</summary>
    public float[]? FloatBuffer;
}

/// <summary>
/// The pool/record layer of the per-voice modulator evaluator (M6-022 V28, 0x9E2BD0, modulator-evaluator
/// Q1). It owns the two chunk lists, the clear (used counts and per-call needed length, capacities and float
/// buffers surviving), the lazy float-buffer growth and the tail compaction that frees a node when
/// <c>used &lt; capacity/2</c> (0x9E2F4C..0x9E3150).
///
/// <para><b>Arithmetic.</b> C15 Y2 establishes the <c>0x9E2BD0</c> per-record bodies are scalar VFP, not
/// NEON: the only NEON in the whole body is the two <c>vst1.32</c> node zero-inits at
/// <c>0x9E5068</c>/<c>0x9E5128</c>. There is no lane order to recover, so the scalar recurrence is the
/// settled EXACT_SOURCE form. The record and pool offsets are the report's.</para>
/// </summary>
public sealed class WwiseModulatorPool
{
    // fidelity: M6-022

    /// <summary>The type-0 list (<c>[arg+0x20]</c>).</summary>
    public WwiseModulatorChunk<WwiseModulatorRecord0>? Type0Head { get; private set; }

    /// <summary>The type-1 list (<c>[arg+0x14]</c>).</summary>
    public WwiseModulatorChunk<WwiseModulatorRecord1>? Type1Head { get; private set; }

    /// <summary>0x9E2BDC..0x9E2C48: reset the used counts and the per-call needed length; buffers survive.</summary>
    public void Clear()
    {
        for (var n = Type0Head; n is not null; n = n.Next)
        {
            n.Buffer1.Clear();
            n.Buffer2.Clear();
            n.FloatNeeded = 0;
        }
        for (var n = Type1Head; n is not null; n = n.Next)
        {
            n.Buffer1.Clear();
            n.Buffer2.Clear();
            n.FloatNeeded = 0;
        }
    }

    /// <summary>Appends a type-0 record, allocating a 16-slot chunk when the current one is full.</summary>
    public WwiseModulatorRecord0 AddType0(WwiseModulatorRecord0 record)
    {
        Type0Head ??= new WwiseModulatorChunk<WwiseModulatorRecord0>();
        var node = Type0Head;
        while (node.Buffer1.Count >= WwiseModulatorChunk<WwiseModulatorRecord0>.Capacity && node.Next is not null)
            node = node.Next;
        if (node.Buffer1.Count >= WwiseModulatorChunk<WwiseModulatorRecord0>.Capacity)
        {
            node.Next = new WwiseModulatorChunk<WwiseModulatorRecord0>();
            node = node.Next;
        }
        node.Buffer1.Add(record);
        return record;
    }

    /// <summary>Appends a type-1 record, allocating a 16-slot chunk when the current one is full.</summary>
    public WwiseModulatorRecord1 AddType1(WwiseModulatorRecord1 record)
    {
        Type1Head ??= new WwiseModulatorChunk<WwiseModulatorRecord1>();
        var node = Type1Head;
        while (node.Buffer1.Count >= WwiseModulatorChunk<WwiseModulatorRecord1>.Capacity && node.Next is not null)
            node = node.Next;
        if (node.Buffer1.Count >= WwiseModulatorChunk<WwiseModulatorRecord1>.Capacity)
        {
            node.Next = new WwiseModulatorChunk<WwiseModulatorRecord1>();
            node = node.Next;
        }
        node.Buffer1.Add(record);
        return record;
    }

    /// <summary>
    /// 0x9E2BD0's type-0 evaluation: for each record, ensure the output buffer holds
    /// <paramref name="frames"/> floats (growing lazily when allocated &lt; needed), recompute the
    /// coefficients from the record's C1/C2 and phase increment, and run the scalar shape recurrence.
    /// </summary>
    public void EvaluateType0(int frames)
    {
        for (var n = Type0Head; n is not null; n = n.Next)
        {
            foreach (var rec in n.Buffer1)
            {
                rec.Output = EnsureFloatBuffer(frames, ref n.FloatNeeded, ref n.FloatAllocated, ref n.FloatBuffer);
                rec.Oscillator.Coefficients = new WwiseLfoCoefficients(rec.C1_34, rec.C2_38);
                rec.Oscillator.Phase = rec.Phase40;
                var shape = (WwiseLfoShape)rec.Shape48;
                rec.Oscillator.Evaluate(shape, rec.PhaseInc44, rec.F30, 0f,
                    rec.Output.AsSpan(0, frames));
            }
        }
    }

    /// <summary>0x9E2BD0's type-1 evaluation: run <see cref="WwiseTransitionRamp"/> per record.</summary>
    public void EvaluateType1(int frames)
    {
        for (var n = Type1Head; n is not null; n = n.Next)
        {
            foreach (var rec in n.Buffer1)
            {
                rec.Output = EnsureFloatBuffer(frames, ref n.FloatNeeded, ref n.FloatAllocated, ref n.FloatBuffer);
                WwiseTransitionRamp.Evaluate(rec, frames, rec.Output.AsSpan(0, frames));
            }
        }
    }

    /// <summary>
    /// 0x9E2F4C..0x9E3150: unlink and free every chunk whose used count is below half capacity. The model
    /// frees the float buffer with the node; the native frees buffer1, buffer2, the float buffer and the node.
    /// </summary>
    public int Compact()
    {
        int freed = 0;
        Type0Head = CompactList(Type0Head, ref freed);
        Type1Head = CompactList(Type1Head, ref freed);
        return freed;
    }

    private static WwiseModulatorChunk<T>? CompactList<T>(WwiseModulatorChunk<T>? head, ref int freed)
    {
        while (head is not null && head.Buffer1.Count < WwiseModulatorChunk<T>.Capacity / 2)
        {
            head = head.Next;
            freed++;
        }
        for (var node = head; node?.Next is not null;)
        {
            if (node.Next.Buffer1.Count < WwiseModulatorChunk<T>.Capacity / 2)
            {
                node.Next = node.Next.Next;
                freed++;
            }
            else
            {
                node = node.Next;
            }
        }
        return head;
    }

    private static float[] EnsureFloatBuffer(int frames, ref int needed, ref int allocated, ref float[]? buffer)
    {
        needed = Math.Max(needed, frames);
        if (allocated >= needed && buffer is not null) return buffer;
        allocated = needed;
        buffer = new float[allocated];
        return buffer;
    }
}

/// <summary>
/// 0x9E52F8 (M6-022 V28, modulator-evaluator Q2): the type-1 multi-segment transition ramp. The five
/// segment boundaries are the settled ones:
/// <c>L0 = record[4]&amp;~3</c>, <c>L1 = ((record[0x1c]&gt;&gt;1)+2)&amp;~3</c>,
/// <c>L2 = (record[8]+2)&amp;~3</c>, <c>L3 = (record[0x28]+2)&amp;~3</c>,
/// <c>L4 = (record[0x2c]+2)&amp;~3</c>; <c>t = record[0xc] - n</c>. C15 Y2 establishes the body is scalar
/// VFP (no NEON lane order); the stage arithmetic is the settled EXACT_SOURCE form of report Q2.
/// </summary>
/// <remarks>
/// <b>RECOVERABLE_GAP residual.</b> The report does not settle the per-sample order inside the stage-B
/// (attack) and stage-C (decay) loops. This implementation uses the accumulating convention
/// <c>first sample = V0 + slope</c>: the slope is applied before the first store. That is a choice, not a
/// recovered value, and the modulator-evaluator report marks the per-sample order RECOVERABLE_GAP. The
/// behaviour is kept because it is the settled arithmetic order of the segment; the residual is named here
/// so the manifest can record it in <c>unresolved</c>.
/// </remarks>
public static class WwiseTransitionRamp
{
    // fidelity: M6-022

    /// <summary>0x9E52F8: runs the ramp for one record over <paramref name="frames"/> samples.</summary>
    public static void Evaluate(WwiseModulatorRecord1 record, int frames, Span<float> output)
    {
        var state = record.State;
        state.Status = -1;                                          // 0x9E5360 state[4] = -1
        state.RunningMax = record.Start18;                          // 0x9E537C state[0xc] = V0

        int l0 = record.Count04 & ~3;                               // 0x9E5348
        int l1 = ((record.I1c >> 1) + 2) & ~3;                      // 0x9E5320..0x9E5334
        int l2 = (record.Count08 + 2) & ~3;                         // 0x9E533C..0x9E5344
        int l3 = (record.Count28 + 2) & ~3;                         // 0x9E5340..0x9E5368
        int l4 = (record.Count2c + 2) & ~3;                         // 0x9E534C..0x9E5378

        int t = record.Time0c - frames;                             // 0x9E535C t = record[0xc] - n
        float v0 = record.Start18;
        float b = record.Breakpoint20;

        if (v0 > 0f)                                                // 0x9E5324/0x9E5388
        {
            int d = (int)(l1 * (v0 / b));                           // 0x9E53B8/0x9E53C4/0x9E53C8 (vcvt.u32.f32)
            d = (d + 2) & ~3;                                       // 0x9E53D4/0x9E53D8
            t += d;                                                 // 0x9E53DC
            l2 += d;                                                // 0x9E53E0
        }

        if (t != 0)                                                 // 0x9E53EC/0x9E53F4
            v0 = record.F10;

        float last = v0;
        float runningMax = record.Start18;
        int n = frames;
        int written = 0;

        // Stage A (0x9E53F8): zeros until L0.
        if (t < l0)
        {
            int count = Math.Min(l0 - t, n);
            output.Slice(written, count).Clear();
            written += count;
            t += count;
            n -= count;
            runningMax = Math.Max(runningMax, 0f);
        }

        // Stage B (attack, 0x9E540C): slope B/L1 up to min(L2, L1+L0), accumulating from V0. RECOVERABLE_GAP
        // residual: the first sample is V0 + slope (the per-sample order is not settled by the report).
        int attackEnd = Math.Min(l2, l1 + l0);
        if (t < attackEnd)
        {
            int count = Math.Min(attackEnd - t, n);
            float slope = b / l1;                                   // 0x9E544C vdiv
            float value = v0;
            for (int i = 0; i < count; i++)
            {
                value += slope;
                output[written + i] = value;
                runningMax = Math.Max(runningMax, value);
            }
            last = value;
            written += count;
            t += count;
            n -= count;
        }

        // Stage C (decay, 0x9E5C00): slope (1-B)/L1 down to L3, accumulating from the attack end. Same
        // RECOVERABLE_GAP first-sample convention as stage B (first sample = last + slope).
        if (t < l3)
        {
            int count = Math.Min(l3 - t, n);
            float slope = (1f - b) / l1;
            float value = last;
            for (int i = 0; i < count; i++)
            {
                value += slope;
                output[written + i] = value;
                runningMax = Math.Max(runningMax, value);
            }
            last = value;
            written += count;
            t += count;
            n -= count;
        }

        // Stage D (sustain, 0x9E5D20): constant record[0x24] up to L4.
        if (t < l4)
        {
            int count = Math.Min(l4 - t, n);
            for (int i = 0; i < count; i++)
            {
                last = record.Sustain24;
                output[written + i] = last;
            }
            runningMax = Math.Max(runningMax, record.Sustain24);
            written += count;
            t += count;
            n -= count;
        }

        // Stage E (release): slope -prev/L4 for the remainder.
        float releaseStart = last;
        for (; written < output.Length; written++)
        {
            last = releaseStart - releaseStart / Math.Max(1, l4) * (written + 1);
            output[written] = last;
            if (n > 0) n--;
        }

        state.RunningMax = runningMax;
        state.Last = last;
        // 0x9E57E8/0x9E57F4: the curve is complete when every sample was consumed (n == 0) or t >= L4.
        if (n == 0 || t >= l4)
        {
            state.Status = 3;                                       // 0x9E57E8
            state.Last = last;                                      // 0x9E57F4
        }
    }
}