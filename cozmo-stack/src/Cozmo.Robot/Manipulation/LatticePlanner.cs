using System.Text.Json;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Manipulation;

/// <summary>
/// The binary32 helpers the planner's engine functions share (M13-003, M13-004, M13-018). Every function states the instruction range it follows.
/// The engine computes in <c>float</c> except where an instruction widens (<c>vcvt.f64.f32</c>): those widenings are kept, in the order the instructions do them.
/// bionic's <c>atan2f</c>, <c>cosf</c>, <c>sinf</c> and <c>hypotf</c> are not shipped (they are system libraries), so the host's <see cref="MathF"/> stands in for them.
/// </summary>
// fidelity: M13-003, M13-004, M13-018
internal static class EngineF
{
    public static float Bits(uint bits) => BitConverter.UInt32BitsToSingle(bits);
    /// <summary>6.283185307179586, the double literal 0x401921FB54442D18 the angle loops add (e.g. 0x00851614).</summary>
    public const double TwoPiD = 6.283185307179586;
    /// <summary>3.141592653589793, 0x400921FB54442D18 (0x00855132, 0x00841394).</summary>
    public const double PiD = 3.141592653589793;
    public static readonly float Pi = Bits(0x40490FDB);
    public static readonly float NegPi = Bits(0xC0490FDB);
    public static readonly float TwoPi = Bits(0x40C90FDB);
    public static readonly float NegTwoPi = Bits(0xC0C90FDB);

    /// <summary><c>Radians::rescale()</c> 0x0084C87C in binary32 (also the <c>Radians(float)</c> constructor 0x0084C832).</summary>
    public static float Rescale(float a)
    {
        if (a > NegPi && a <= Pi) return a;                                       // 0x0084C892..0x0084C8A8
        if (!(MathF.Abs(a) < 10f))                                                // vcmpe |a|, 10.0f; bpl 0x0084C902 (also taken for NaN)
        {
            float t = MathF.Ceiling(a / TwoPi + -0.5f);                            // vdiv, vadd -0.5, ceilf
            int n = float.IsNaN(t) ? 0 : (int)Math.Clamp(t, int.MinValue, int.MaxValue);
            return a - (float)n * TwoPi;                                           // vcvt.s32, vcvt.f32.s32, vmul, vsub
        }
        if (!(a > NegPi)) { do a += TwoPi; while (a <= NegPi); }                   // 0x0084C8BC..0x0084C8D8
        if (a > Pi) { do a += NegTwoPi; while (a > Pi); }                          // 0x0084C8DC..0x0084C8FC
        return a;
    }

    /// <summary><c>Anki::operator-(Radians, Radians)</c> 0x0084CA18: a float subtraction of the two (already rescaled) values, then a rescale.</summary>
    public static float RadiansMinus(float a, float b) => Rescale(Rescale(a) - Rescale(b));

    /// <summary><c>Radians::angularDistance(this, other, flag)</c> 0x0084CD74: <c>other - this</c> in float; negative with the flag clear gets +2pi; with the flag set a positive value gets -2pi.</summary>
    public static float AngularDistance(float thisAngle, float otherAngle, bool flag)
    {
        float d = otherAngle - thisAngle;
        if (d < 0f && !flag) return d + TwoPi;                                      // 0x0084CD8C..0x0084CD94
        float s2 = d + NegTwoPi;
        float s4 = flag ? s2 : d;
        return d > 0f ? s4 : d;
    }

    /// <summary><c>hypotf</c> (bionic, not shipped): the double sum of squares, square-rooted and narrowed.</summary>
    public static float Hypot(float a, float b) => (float)Math.Sqrt((double)a * a + (double)b * b);

    /// <summary>A <c>vsqrt.f32</c> of <c>(float)(dx*dx + dy*dy)</c> with the squares formed in double, as <c>GetDistanceBetween</c> 0x00850DCC and the primitive import do.</summary>
    public static float SqrtOfDoubleSquares(float dx, float dy)
    {
        float s = (float)((double)dx * (double)dx + (double)dy * (double)dy);
        return MathF.Sqrt(s);
    }

    /// <summary>
    /// The wrap and the index every theta in the planner goes through (<c>IsInCollision(State_c)</c> 0x0085160A..0x0085167A, <c>CheckGoal</c> 0x0085903C..0x008590BA,
    /// <c>RoundSafe</c> 0x00855B08..0x00855B7C): add 2pi in double while negative, subtract it while at or above 2pi, scale by env+0x10, <c>roundf</c>, and the remainder by env+8.
    /// </summary>
    public static int ThetaIndex(float theta, float invAngleStep, int numAngles)
    {
        float t = theta;
        if (t < 0f) { do t = (float)((double)t + TwoPiD); while (t < 0f); }
        while ((double)t >= TwoPiD) t = (float)((double)t + -TwoPiD);
        float scaled = t * invAngleStep;
        uint r = (uint)MathF.Round(scaled, MidpointRounding.AwayFromZero);
        return (int)(r % (uint)numAngles);
    }
}

/// <summary>A <c>State_c</c>: the continuous state (x mm, y mm, heading radians), all binary32.</summary>
public readonly record struct StateC(float X, float Y, float Theta);

/// <summary>
/// A lattice <c>State</c> (<c>x</c> and <c>y</c> in cells as 16-bit values, <c>theta</c> a heading index) and its <c>StateID</c>: heading in bits 0..3, <b>x in bits 4..17</b> (14 bits) and
/// <b>y in bits 18..31</b>, as <c>State::GetStateID</c> 0x0084FC58 packs it (<c>(y &lt;&lt; 18) | ((x &amp; 0x3FFF) &lt;&lt; 4) | (theta &amp; 0xF)</c>), <c>SuccessorIterator::Next</c> packs
/// it (0x00851500..0x00851514) and <c>State::State(StateID)</c> 0x0084F88C unpacks it (<c>sbfx(id, 4, 14)</c> is x, <c>id &gt;&gt; 18</c> is y; <c>State::Import</c> reads "x" into +0 and "y" into +2).
/// </summary>
// fidelity: M13-003, M13-018
public readonly record struct LatticeState(short X, short Y, byte Theta)
{
    public uint Id => unchecked(((uint)(int)Y << 18) | (((uint)(int)X << 4) & 0x3FFF0u) | ((uint)Theta & 0xFu));
    public static LatticeState FromId(uint id) => new((short)((int)(id << 14) >> 18), (short)((int)id >> 18), (byte)(id & 0xF));
}

/// <summary>
/// One action of the motion-primitive set (<c>cozmo_mprim.json</c> "actions"): its index, name, extra cost
/// factor (a binary32, <c>asFloat</c>) and whether it drives backwards.
/// </summary>
public sealed record PrimitiveAction(int Index, string Name, float ExtraCostFactor, bool Reverse);

/// <summary>
/// One sampled pose of a primitive (<c>Anki::Planning::IntermediatePosition</c>, 0x14 bytes): x and y in mm and the heading in radians relative to the start cell (binary32,
/// <c>State_c::Import</c>), the heading's index byte at +0xC (rounded by env+0x10, 0x00853F7E..0x00853FB4) and the soft-collision reciprocal at +0x10 (M13-003, M13-004).
/// </summary>
public readonly record struct IntermediatePose(float X, float Y, float Theta, byte ThetaIndex, float Reciprocal);

/// <summary>
/// One motion primitive (<c>Anki::Planning::MotionPrimitive</c>): from a start heading index, the action moves
/// the robot by (EndX, EndY) cells to heading EndTheta, through the listed intermediate poses. <see cref="Cost"/> is the primitive's traversal cost, computed by
/// <c>MotionPrimitive::Create</c> in binary32 (M13-004).
/// </summary>
public sealed record MotionPrimitive(int ActionIndex, int StartTheta, short EndX, short EndY, int EndTheta,
                                     IReadOnlyList<IntermediatePose> Intermediate, float Cost)
{
    /// <summary>The primitive's <c>straight_length_mm</c> (<c>asFloat</c>, 0x008540D0); zero when absent.</summary>
    public float StraightLengthMm { get; init; }

    /// <summary>The <c>arc</c> block as the engine reads it (<c>asFloat</c> each, 0x008541D4..0x0085421A): centre, radius, start angle and sweep.</summary>
    public (float CenterX, float CenterY, float Radius, float StartRad, float SweepRad)? Arc { get; init; }

    /// <summary><c>turn_in_place_direction</c> (<c>asDouble</c>, 0x00854278); null otherwise.</summary>
    public double? TurnInPlaceDirection { get; init; }
}

/// <summary>
/// The engine's motion-primitive set, ASSET <c>config/engine/cozmo_mprim.json</c>, imported as <c>xythetaEnvironment::ParseMotionPrims</c> 0x00852014 and
/// <c>MotionPrimitive::Create</c> 0x00853DD0 do it: the resolution, its reciprocal, the angle definitions and every cost and reciprocal in binary32 (with the double
/// widenings the instructions have), the jsoncpp readers (<c>asFloat</c>, short, unsigned byte) as the engine uses them.
/// </summary>
// fidelity: M13-001, M13-004
public sealed class MotionPrimitiveSet
{
    /// <summary>env+0: <c>resolution_mm</c> via <c>GetValueOptional&lt;float&gt;</c> (0x0085203C).</summary>
    public float ResolutionMm { get; private init; }
    /// <summary>env+4: <c>1.0f / resolution</c> (<c>vdiv.f32</c> at 0x008520B0).</summary>
    public float InverseResolution { get; private init; }
    /// <summary>
    /// The JSON <c>num_angles</c> (env+8). The production <c>xythetaEnvironment::Init(Json const&amp;)</c> 0x00851F9E does not overwrite it; the hard-coded-16 override is on the uncalled
    /// <c>Init(char const*)</c> 0x008528A8 (M13-019). The shipped asset's value is 16.
    /// </summary>
    // fidelity: M13-019
    public int NumAngles { get; private init; }
    /// <summary>env+0xC: <c>(float)(2pi / 16.0)</c>, set by the environment constructor (0x00851EF2..0x00851F0C) and not changed by the JSON parse.</summary>
    public float AngleStep { get; } = (float)(EngineF.TwoPiD / 16.0);
    /// <summary>env+0x10: <c>1.0f / env+0xC</c> (0x00851F10).</summary>
    public float InverseAngleStep { get; }
    /// <summary>env+0x38: <c>angle_definitions</c> via <c>asFloat</c> (0x008521FA).</summary>
    public IReadOnlyList<float> Angles { get; private init; } = Array.Empty<float>();
    public IReadOnlyList<PrimitiveAction> Actions { get; private init; } = Array.Empty<PrimitiveAction>();
    /// <summary>Primitives by start heading index (env+0x14).</summary>
    public IReadOnlyList<IReadOnlyList<MotionPrimitive>> ByAngle { get; private init; } = Array.Empty<IReadOnlyList<MotionPrimitive>>();

    private MotionPrimitiveSet() => InverseAngleStep = 1.0f / AngleStep;

    /// <summary>
    /// The <c>RobotActionParams</c> defaults the environment constructs (ctor 0x0084EDE6 as called at 0x00851EE2), stored as DOUBLES at env+0x60, +0x68, +0x70 and +0x78:
    /// half wheel base 24.0 (0x4038000000000000), max velocity 60.0 (0x404E000000000000), max reverse velocity 25.0 (0x4039000000000000) and 1/60 (0x3F91111111111111).
    /// <c>RobotActionParams::Import</c> has no callers and the asset has no such keys, so these stand (M13-004).
    /// </summary>
    public const double HalfWheelBaseMm = 24.0;
    public const double MaxVelocityMmps = 60.0;
    public const double MaxReverseVelocityMmps = 25.0;
    public static readonly double InverseMaxVelocity = BitConverter.Int64BitsToDouble(0x3F91111111111111);

    public static string ObbRelativePath => Path.Combine("assets", "cozmo_resources", "config", "engine", "cozmo_mprim.json");

    /// <summary>Loads the set from an OBB root; null when the file is not there or the parse fails.</summary>
    public static MotionPrimitiveSet? FromObb(string obbRoot)
    {
        var p = Path.Combine(obbRoot, ObbRelativePath);
        if (!File.Exists(p)) return null;
        try { return Parse(File.ReadAllText(p)); }
        catch (InvalidDataException) { return null; }   // ReadMotionPrimitives returns 0 -> no planner
    }

    /// <summary>jsoncpp <c>asFloat</c>: the JSON number narrowed to binary32.</summary>
    private static float AsFloat(JsonElement e) => (float)e.GetDouble();

    public static MotionPrimitiveSet Parse(string json)
    {
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var root = doc.RootElement;
        float res = AsFloat(root.GetProperty("resolution_mm"));
        int n = (int)root.GetProperty("num_angles").GetUInt32();
        var angles = root.GetProperty("angle_definitions").EnumerateArray().Select(AsFloat).ToArray();
        var actions = root.GetProperty("actions").EnumerateArray()
            .Select(a => new PrimitiveAction((int)a.GetProperty("index").GetDouble(), a.GetProperty("name").GetString() ?? "",
                                             AsFloat(a.GetProperty("extra_cost_factor")),
                                             a.TryGetProperty("reverse_action", out var r) && r.GetBoolean()))
            .OrderBy(a => a.Index).ToArray();
        // fidelity: M13-001
        // ParseMotionPrims 0x00852014 aborts and returns 0 when either count differs from num_angles
        // (0x0085223C, 0x0085225A); the messages are the engine's.
        if (angles.Length != n)
            throw new InvalidDataException($"ERROR: numAngles is {n}, but we read {angles.Length} angle definitions");
        var angleEntries = root.GetProperty("angles").EnumerateArray().ToArray();
        if (angleEntries.Length != n)
            throw new InvalidDataException("error: could not find key 'angles' in motion primitives");

        var set = new MotionPrimitiveSet { ResolutionMm = res, InverseResolution = 1.0f / res, NumAngles = n, Angles = angles, Actions = actions };
        var byAngle = new List<IReadOnlyList<MotionPrimitive>>();
        int start = 0;
        foreach (var ang in angleEntries)
        {
            var prims = new List<MotionPrimitive>();
            foreach (var p in ang.GetProperty("prims").EnumerateArray())
            {
                // MotionPrimitive::Create returns 0 on a bad primitive; ParseMotionPrims logs "Failed to import motion primitive" and stops (r7 = 1 at 0x008523B4, the loop at 0x00852402)
                if (!TryCreate(p, start, set, out var prim, out var why))
                    throw new InvalidDataException($"Failed to import motion primitive: {why}");
                prims.Add(prim!);
            }
            byAngle.Add(prims);
            start++;
        }
        // PopulateReverseMotionPrims 0x008544C0 (env+0x20), after the forward set exists
        // fidelity: M13-001, M13-004, M13-019
        return new MotionPrimitiveSet
        {
            ResolutionMm = res, InverseResolution = 1.0f / res, NumAngles = n, Angles = angles, Actions = actions, ByAngle = byAngle,
            Reflected = BuildReflected(byAngle, n),
        };
    }

    /// <summary>
    /// <c>MotionPrimitive::Create</c> 0x00853DD0: <c>action_index</c> (unsigned byte), <c>end_pose</c> through <c>State::Import</c> (x, y shorts and theta a byte), the intermediate poses through
    /// <c>State_c::Import</c> (three floats) with each pose's reciprocal and heading index, then the straight / arc / turn-in-place cost, the action's extra cost factor and the 1e-6 failure gates.
    /// </summary>
    // fidelity: M13-004
    private static bool TryCreate(JsonElement p, int startTheta, MotionPrimitiveSet env, out MotionPrimitive? prim, out string why)
    {
        prim = null; why = "";
        // a per-primitive extra_cost_factor is rejected (0x00853FFC, M13-004)
        if (p.TryGetProperty("extra_cost_factor", out _)) { why = "ERROR: individual primitives shouldn't have cost factors. Old file format?"; return false; }
        if (!p.TryGetProperty("action_index", out var ai)) { why = "no action_index"; return false; }
        int actionIndex = (byte)(int)ai.GetDouble();                                  // GetValueOptional<unsigned char>: jsoncpp asUInt of the number
        if (actionIndex >= env.Actions.Count) { why = $"action_index {actionIndex} has no action"; return false; }
        var action = env.Actions[actionIndex];
        // State::Import 0x0084F8A4: "x"/"y" are shorts in grid cells, "theta" an unsigned byte heading index (0x0084F920/0x0084F940/0x0084F962)
        var end = p.GetProperty("end_pose");
        short ex = (short)(int)end.GetProperty("x").GetDouble();                      // jsoncpp asInt truncates a real value (the asset writes 1.0 and 5)
        short ey = (short)(int)end.GetProperty("y").GetDouble();
        byte eth = (byte)(int)end.GetProperty("theta").GetDouble();

        var inter = new List<IntermediatePose>();
        foreach (var q in p.GetProperty("intermediate_poses").EnumerateArray())
        {
            // State_c::Import 0x0084FD70: x_mm, y_mm, theta_rads, each GetValueOptional<float>
            float qx = AsFloat(q.GetProperty("x_mm")), qy = AsFloat(q.GetProperty("y_mm")), qt = AsFloat(q.GetProperty("theta_rads"));
            float recip = 0f;
            if (inter.Count > 0)
            {
                var prev = inter[^1];
                float dx = qx - prev.X, dy = qy - prev.Y;                              // 0x00853EBA, 0x00853EC2: vsub.f32
                float dist = EngineF.SqrtOfDoubleSquares(dx, dy);                      // vcvt.f64 x2, vmul.f64 x2, vadd.f64, vcvt.f32.f64, vsqrt.f32 (0x00853EC6..0x00853EDE)
                float dth = MathF.Abs(EngineF.RadiansMinus(qt, prev.Theta));           // Radians(cur), Radians(prev), operator-, vabs.f32 (0x00853EF8..0x00853F24)
                // 0x00853F28..0x00853F46: d0 = (double)|dth|; d0 = env+0x60 * d0; d0 = env+0x78 * d0; d0 += (double)dist; vcvt.f32.f64; recip = 1.0f / that
                double dd = MaxVelocityMmps == 0 ? 0 : InverseMaxVelocity * (HalfWheelBaseMm * (double)dth);
                float sum = (float)(dd + (double)dist);
                recip = 1.0f / sum;
            }
            // 0x00853F5A..0x00853FB4: wrap the heading into [0, 2pi) in double steps, scale by env+0x10, roundf, remainder by env+8
            byte idx = (byte)EngineF.ThetaIndex(qt, env.InverseAngleStep, env.NumAngles);
            inter.Add(new IntermediatePose(qx, qy, qt, idx, recip));
        }

        // cost: 0x00854036..0x0085437C
        double d8, d9;
        if (action.Reverse) { d9 = MaxReverseVelocityMmps; d8 = 1.0 / d9; }          // 0x00854048..0x00854050
        else { d9 = MaxVelocityMmps; d8 = InverseMaxVelocity; }                        // 0x00854078..0x0085407C (env+0x68 and env+0x78)
        float cost = 0f;
        float straight = 0f;
        double straightD = p.TryGetProperty("straight_length_mm", out var sl) ? sl.GetDouble() : 0.0;
        if (!(straightD == 0.0 || double.IsNaN(straightD)))                           // vcmp.f64 d0, #0; beq/bvs 0x00854144
        {
            cost = (float)(d8 * Math.Abs(straightD) + (double)cost);                   // vabs.f64, vmul.f64 d8*|len|, vadd.f64 with the float cost widened, vcvt.f32.f64
            straight = (float)straightD;                                               // asFloat 0x008540D0
        }
        (float, float, float, float, float)? arc = null;
        double? turnDir = null;
        if (p.TryGetProperty("arc", out var ja))
        {
            double sweepD = Math.Abs(ja.GetProperty("sweepRad").GetDouble());          // the first asDouble, sign bit cleared (bfc at 0x00854190)
            double radiusD = Math.Abs(ja.GetProperty("radius_mm").GetDouble());        // the second asDouble
            // d0 = radius + env+0x60; d0 = sweep * d0; d0 = d8 * d0; s0 = (float)d0 (0x008541A4..0x008541B0)
            float term = (float)(d8 * (sweepD * (radiusD + HalfWheelBaseMm)));
            cost = cost + term;                                                         // vadd.f32 (0x008541BC)
            arc = (AsFloat(ja.GetProperty("centerPt_x_mm")), AsFloat(ja.GetProperty("centerPt_y_mm")), AsFloat(ja.GetProperty("radius_mm")),
                   AsFloat(ja.GetProperty("startRad")), AsFloat(ja.GetProperty("sweepRad")));
        }
        else if (p.TryGetProperty("turn_in_place_direction", out var td))
        {
            double dir = td.GetDouble();
            turnDir = dir;
            // 0x00854280..0x008542EE: Radians(angles[start]), Radians(angles[end]); angularDistance(start, end, dir < 0); term = (float)(d8 * (env+0x60 * |dth|)); cost += term
            float a0 = EngineF.Rescale(env.Angles[startTheta]);
            float a1 = EngineF.Rescale(env.Angles[eth]);
            float dth = EngineF.AngularDistance(a0, a1, dir < 0);
            float term = (float)(d8 * (HalfWheelBaseMm * (double)MathF.Abs(dth)));
            cost = cost + term;
        }
        // 0x00854322..0x0085437C: base cost below 1e-6 fails (printf, return 0); then * the action's extra cost factor, and the product must be at least 1e-6 as well
        if ((double)cost < 1e-6) { why = $"base action cost is {cost} for action {actionIndex}"; return false; }
        cost = cost * action.ExtraCostFactor;
        if ((double)cost < 1e-6) { why = $"cost is {cost} for action {actionIndex}"; return false; }
        prim = new MotionPrimitive(actionIndex, startTheta, ex, ey, eth, inter, cost)
        {
            StraightLengthMm = straight,
            Arc = arc,
            TurnInPlaceDirection = turnDir,
        };
        return true;
    }

    /// <summary>
    /// <c>PopulateReverseMotionPrims</c> 0x008544C0: the reflected primitive set the heuristic expansion walks
    /// (env+0x20). For each forward primitive it negates the end-pose x and y (16-bit), sets the end-pose theta
    /// byte to the forward primitive's <b>start</b> heading index, stores the result in the bucket of the forward
    /// <b>end</b> theta, and copies the cost (+4), the <c>Path</c> (+0x2C), the intermediate-pose vector (+0x10)
    /// and the cached bbox (+0x1C..+0x28) unchanged. The start theta byte (+1) is copied unchanged too, so a reflected primitive's start and end heading bytes are
    /// always equal.
    /// </summary>
    // fidelity: M13-018
    public IReadOnlyList<IReadOnlyList<MotionPrimitive>> Reflected { get; private init; } = Array.Empty<IReadOnlyList<MotionPrimitive>>();

    private static IReadOnlyList<IReadOnlyList<MotionPrimitive>> BuildReflected(IReadOnlyList<IReadOnlyList<MotionPrimitive>> byAngle, int n)
    {
        var buckets = new List<MotionPrimitive>[n];
        for (int i = 0; i < n; i++) buckets[i] = new List<MotionPrimitive>();
        for (int start = 0; start < byAngle.Count; start++)
            foreach (var p in byAngle[start])
                buckets[p.EndTheta].Add(p with { EndX = unchecked((short)-p.EndX), EndY = unchecked((short)-p.EndY), EndTheta = start });
        return buckets.Select(b => (IReadOnlyList<MotionPrimitive>)b).ToArray();
    }

    /// <summary>The heading index of a continuous heading, as the planner computes it (<see cref="EngineF.ThetaIndex"/>).</summary>
    public int ThetaIndex(float angleRad) => EngineF.ThetaIndex(angleRad, InverseAngleStep, NumAngles);
}

/// <summary>A 2-D point as the engine's <c>Point&lt;2, float&gt;</c>.</summary>
public readonly record struct P2f(float X, float Y);

/// <summary>
/// The engine's <c>Anki::Polygon&lt;2, float&gt;</c> and <c>ConvexPolygon</c> operations the planner's obstacle path uses, in binary32: <c>ComputeCentroid</c> 0x0050238A,
/// <c>GetEdgeAngle</c> 0x008413F0, the <c>ConvexPolygon</c> constructor 0x008412F0 (which reverses a polygon whose first turn is to the left), <c>RadialExpand</c> 0x00841580 and
/// <c>Polygon::ImportQuad2d</c> 0x0050096C.
/// </summary>
// fidelity: M13-003
public static class PolygonF
{
    /// <summary><c>Polygon::ComputeCentroid</c> 0x0050238A: <c>inv = 1.0f / (float)n</c>, then every point's <c>inv * p</c> added in order to a running sum that starts at zero.</summary>
    public static P2f Centroid(IReadOnlyList<P2f> pts)
    {
        if (pts.Count == 0) return new P2f(0, 0);
        float inv = 1.0f / (float)pts.Count;
        float x = 0, y = 0;
        foreach (var p in pts) { x = x + inv * p.X; y = y + inv * p.Y; }
        return new P2f(x, y);
    }

    /// <summary><c>Polygon::GetEdgeAngle(i)</c> 0x008413F0: <c>atan2f(p[(i+1)%n].y - p[i].y, p[(i+1)%n].x - p[i].x)</c> with float differences.</summary>
    public static float EdgeAngle(IReadOnlyList<P2f> pts, int i)
    {
        var a = pts[i]; var b = pts[(i + 1) % pts.Count];
        return MathF.Atan2(b.Y - a.Y, b.X - a.X);
    }

    /// <summary><c>sub_8554d8</c>: the edge vector <c>p[(i+1)%n] - p[i]</c> in float.</summary>
    public static P2f EdgeVector(IReadOnlyList<P2f> pts, int i)
    {
        var a = pts[i]; var b = pts[(i + 1) % pts.Count];
        return new P2f(b.X - a.X, b.Y - a.Y);
    }

    /// <summary>
    /// The <c>ConvexPolygon</c> constructor 0x008412F0: the angle of edge 1 less the angle of edge 0 (floats), plus 2pi in double, <c>fmod</c> by 2pi (double), narrowed to float; a result
    /// below pi (a left turn, a counter-clockwise polygon) reverses the point order (<c>std::reverse</c> at 0x008413B6..0x008413D0). The polygons the planner then works with are clockwise.
    /// </summary>
    public static P2f[] ConvexPolygon(IReadOnlyList<P2f> input)
    {
        var pts = input.ToArray();
        if (pts.Length == 0) return pts;
        float a1 = MathF.Atan2(pts[(2) % pts.Length].Y - pts[1 % pts.Length].Y, pts[2 % pts.Length].X - pts[1 % pts.Length].X);
        int next0 = pts.Length != 1 ? 1 : 0;
        float a0 = MathF.Atan2(pts[next0].Y - pts[0].Y, pts[next0].X - pts[0].X);
        float diff = a1 - a0;
        float turn = (float)(((double)diff + EngineF.TwoPiD) % EngineF.TwoPiD);          // vadd.f64 2pi, fmod(x, 2pi), vcvt.f32.f64 (0x00841378..0x00841398)
        if ((double)turn < EngineF.PiD) Array.Reverse(pts);                              // vcmpe.f64 d0, pi; bpl skips the reversal (0x008413A0..0x008413A8)
        return pts;
    }

    /// <summary>
    /// <c>ConvexPolygon::RadialExpand(float)</c> 0x00841580: a negative amount only warns; otherwise the centroid is taken once and every point moves to <c>v + (by / hypotf(v - c)) * (v - c)</c> in
    /// float: the division first, then the multiplication, then the addition (a zero-length offset divides by zero and leaves NaN, as the engine's does).
    /// </summary>
    public static P2f[] RadialExpand(IReadOnlyList<P2f> poly, float by, Action<string>? warn = null)
    {
        if (by < 0f) { warn?.Invoke("called expand with a negative distance."); return poly.ToArray(); }
        var c = Centroid(poly);
        var outp = new P2f[poly.Count];
        for (int i = 0; i < poly.Count; i++)
        {
            float dx = poly[i].X - c.X, dy = poly[i].Y - c.Y;
            float len = EngineF.Hypot(dx, dy);
            float scale = by / len;
            outp[i] = new P2f(scale * dx + poly[i].X, scale * dy + poly[i].Y);
        }
        return outp;
    }

    /// <summary>
    /// <c>Quadrilateral::SortCornersClockwise</c> 0x004E7EFC and <c>Polygon::ImportQuad2d</c> 0x0050096C: the quad's centroid is <c>((c0 + c2) + c1) + c3</c> times 0.25f (0x004DF7BE); the
    /// angle of each corner from it is <c>atan2f(y - cy, x - cx)</c>; the corners sorted by ascending angle are (s0, s1, s2, s3), the quadrilateral is rebuilt as (s0, s3, s1, s2) and the
    /// polygon takes corners 0, 1, 3, 2: (s0, s3, s2, s1), a clockwise order.
    /// </summary>
    public static P2f[] ImportQuad2d(IReadOnlyList<P2f> corners)
    {
        // Quadrilateral::ComputeCentroid
        float cx = corners[0].X; float cy = corners[0].Y;
        cx = corners[2].X + cx; cy = corners[2].Y + cy;
        cx = corners[1].X + cx; cy = corners[1].Y + cy;
        cx = corners[3].X + cx; cy = corners[3].Y + cy;
        cx *= 0.25f; cy *= 0.25f;
        var order = Enumerable.Range(0, 4).Select(i => (Angle: MathF.Atan2(corners[i].Y - cy, corners[i].X - cx), Name: i))
                              .OrderBy(t => t.Angle).ToArray();                          // std::sort of (float, CornerName) by the float (ties are not distinguishable for a real quad)
        return new[] { corners[order[0].Name], corners[order[3].Name], corners[order[2].Name], corners[order[1].Name] };
    }
}

/// <summary>
/// The engine's <c>Anki::FastPolygon</c> built from a polygon (ctor 0x008416F0): the edge vectors (<c>CreateEdgeVectors</c> 0x0084173C: <c>(-dy*inv, dx*inv)</c> with <c>inv = 1.0f / sqrtf(dx*dx + dy*dy)</c>
/// per edge, binary32), the centre and bounding box (<c>ComputeCenter</c> 0x008418A4), the circumscribed and inscribed radii squared (<c>ComputeCircles</c> 0x00841980, with the squares of the
/// circumscribed one formed in double) and <c>Contains(x, y)</c> 0x00841BA0.
/// </summary>
// fidelity: M13-003
public sealed class FastPolygonF
{
    private readonly P2f[] _pts;
    private readonly (float Nx, float Ny, int Index)[] _edges;
    public float MinX { get; private set; } = float.MaxValue;
    public float MaxX { get; private set; } = -float.MaxValue;
    public float MinY { get; private set; } = float.MaxValue;
    public float MaxY { get; private set; } = -float.MaxValue;
    public float CenterX { get; private set; }
    public float CenterY { get; private set; }
    public float CircumscribedRadiusSquared { get; private set; }
    public float InscribedRadiusSquared { get; private set; } = float.MaxValue;
    public IReadOnlyList<P2f> Points => _pts;

    public FastPolygonF(IReadOnlyList<P2f> polygon)
    {
        _pts = polygon.ToArray();
        // CreateEdgeVectors 0x0084173C: only for two or more points
        var edges = new List<(float, float, int)>();
        if (_pts.Length >= 2)
        {
            for (int i = 0; i < _pts.Length; i++)
            {
                var a = _pts[i]; var b = _pts[(i + 1) % _pts.Length];
                float dx = b.X - a.X, dy = b.Y - a.Y;
                float s = dx * dx;                                                      // vmul s2 = dx*dx
                s = s + dy * dy;                                                        // vadd
                float inv = 1.0f / MathF.Sqrt(s);                                       // vsqrt.f32, vdiv.f32
                edges.Add((-(inv * dy), inv * dx, i));                                  // vnmul.f32 s2 = -(inv*dy); vmul.f32 s0 = inv*dx
            }
        }
        _edges = edges.ToArray();
        ComputeCenter();
        ComputeCircles();
    }

    private void ComputeCenter()
    {
        var c = PolygonF.Centroid(_pts);
        CenterX = c.X; CenterY = c.Y;
        if (_pts.Length == 0) { MinX = float.MaxValue; MaxX = -float.MaxValue; MinY = float.MaxValue; MaxY = -float.MaxValue; return; }
        float mnx = float.MaxValue, mxx = -float.MaxValue, mny = float.MaxValue, mxy = -float.MaxValue;
        foreach (var p in _pts)
        {
            if (p.X < mnx) mnx = p.X;                                                   // vcmpe; vmovmi
            if (p.X > mxx) mxx = p.X;                                                   // vmovgt
            if (p.Y < mny) mny = p.Y;
            if (p.Y > mxy) mxy = p.Y;
        }
        MinX = mnx; MaxX = mxx; MinY = mny; MaxY = mxy;
    }

    private void ComputeCircles()
    {
        CircumscribedRadiusSquared = 0f; InscribedRadiusSquared = float.MaxValue;
        if (_pts.Length < 3)
        {
            // 0x00841A8A..0x00841AE2: the first other point's offset, in double squares narrowed to float; the inscribed value is 0
            int k = _pts.Length == 1 ? 0 : 1;
            float dx = _pts[k].X - _pts[0].X, dy = _pts[k].Y - _pts[0].Y;
            InscribedRadiusSquared = 0f;
            CircumscribedRadiusSquared = (float)((double)dx * (double)dx + (double)dy * (double)dy);
            return;
        }
        float maxCirc = 0f, minIns = float.MaxValue;
        for (int i = 0; i < _pts.Length; i++)
        {
            float dy = _pts[i].Y - CenterY, dx = _pts[i].X - CenterX;
            float d2 = (float)((double)dx * (double)dx + (double)dy * (double)dy);
            if (maxCirc < d2) { CircumscribedRadiusSquared = d2; maxCirc = d2; }
            var e = _edges[i];
            float cx = CenterX - _pts[e.Index].X, cy = CenterY - _pts[e.Index].Y;
            float dot = e.Nx * cx;
            dot = dot + e.Ny * cy;
            dot = dot * dot;
            if (minIns > dot) { InscribedRadiusSquared = dot; minIns = dot; }
        }
    }

    /// <summary><c>FastPolygon::Contains(float, float)</c> 0x00841BA0.</summary>
    public bool Contains(float x, float y)
    {
        if (MinX > x) return false;                                                     // 0x00841BA4..0x00841BB0
        if (MaxX < x) return false;
        if (MinY > y) return false;
        if (MaxY < y) return false;
        float dx = x - CenterX, dy = y - CenterY;
        float d2 = dy * dy + dx * dx;                                                   // vadd s4 = s6 + s4
        if (d2 > CircumscribedRadiusSquared) return false;                              // bgt 0x00841C62
        if (d2 < InscribedRadiusSquared) return true;                                   // bmi 0x00841C66
        foreach (var e in _edges)
        {
            var v = _pts[e.Index];
            float dot = e.Nx * (x - v.X);
            dot = dot + e.Ny * (y - v.Y);
            if (!(dot > 0f)) continue;                                                  // ble 0x00841C24 is taken for an unordered (NaN) compare too
            return false;
        }
        return true;
    }
}

/// <summary>A <c>xythetaEnvironment::SuccessorIterator</c> result.</summary>
public readonly record struct LatticeSuccessor(uint NextId, int ActionIndex, float Cost, float SoftPenalty, MotionPrimitive Prim);

/// <summary>
/// The engine's <c>Anki::Planning::xythetaEnvironment</c>: the primitive set plus the obstacles the plan must avoid, per heading (env+0x44), as the engine builds and queries them.
///
/// <b>One obstacle table per heading.</b> <c>AddObstacleWithExpansion(obstacle, robot, theta, penalty)</c> 0x00855528 stores <c>FastPolygon(ExpandCSpace(obstacle, robot))</c> with the penalty in the
/// heading's bucket; <c>IsInCollision(State_c)</c> 0x008515F8, <c>IsInCollision(State)</c> 0x008515BC, <c>IsInSoftCollision</c> 0x00851708 and <c>GetCollisionPenalty</c> 0x008517B0 read that table.
/// <c>ExpandCSpace</c> 0x008550E8 is a merge of the obstacle's edges and the robot's reversed edges by polar angle, starting at <c>obstacle[0] - robot[k]</c> (binary32).
///
/// <b>What the import puts in it.</b> <c>LatticePlannerImpl::ImportBlockworldObstaclesIfNeeded</c> 0x004FD4B8: the paddings are the robot's 7 and the obstacle's 6 (or 2 and 1 for the function's own
/// <c>true</c> argument); each obstacle polygon becomes a <c>ConvexPolygon</c> (clockwise), is radially expanded by the obstacle padding and is expanded into configuration space for each
/// heading against the robot's bounding quad at that heading, with the constant penalty 0.1f. <b>The engine takes the polygons from the memory map</b> (<c>FindContentIf</c> for content type 3,
/// <c>ObstacleObservable</c>, in the hash set's order); this stack's memory map is not the engine's quad tree (M14-007), so <see cref="ImportBlockWorldObstacles"/> feeds the world's located
/// objects' bounding quads in its place: MISSING, a visible stand-in.
/// </summary>
// fidelity: M13-003
public sealed class LatticeEnvironment
{
    /// <summary>The robot bounding quad's canonical corners (<c>Robot::GetBoundingQuadXY</c> 0x00514DB0): 22.1f (0x41B0CCCC), 27.1f (0x41D8CCCD), -27.1f (0xC1D8CCCD), -55.9f (0xC25F999A).</summary>
    public static readonly float RobotFrontMm = EngineF.Bits(0x41B0CCCC);
    public static readonly float RobotBackMm = EngineF.Bits(0xC25F999A);
    public static readonly float RobotHalfWidthMm = EngineF.Bits(0x41D8CCCD);

    /// <summary>7.0f, the padding the import gives the robot's quad (<c>vmov.f32 s16,#7.0</c> at 0x004FD4EE).</summary>
    public const float RobotPaddingMm = 7.0f;
    /// <summary>6.0f, the radial expansion it gives each obstacle (0x004FD4F2).</summary>
    public const float ObstaclePaddingMm = 6.0f;
    /// <summary>2.0f, the robot padding for the function's own true argument (0x004FD4EA).</summary>
    public const float TightRobotPaddingMm = 2.0f;
    /// <summary>1.0f, the obstacle padding for that argument (0x004FD4E4).</summary>
    public const float TightObstaclePaddingMm = 1.0f;
    /// <summary>0.1f (0x3DCCCCCD at 0x004FE0CE), the penalty the import gives every obstacle it adds.</summary>
    public static readonly float ObstaclePenalty = EngineF.Bits(0x3DCCCCCD);
    /// <summary>1000.0f (0x447A0000): a penalty at or above it is a hard collision (0x008516D4, 0x00851382).</summary>
    public static readonly float HardPenalty = EngineF.Bits(0x447A0000);

    private readonly List<(FastPolygonF Poly, float Penalty)>[] _buckets;
    private int _obstacleCount;

    public LatticeEnvironment(MotionPrimitiveSet prims)
    {
        Primitives = prims;
        _buckets = new List<(FastPolygonF, float)>[prims.NumAngles];
        for (int i = 0; i < _buckets.Length; i++) _buckets[i] = new();
    }

    public MotionPrimitiveSet Primitives { get; }
    /// <summary>The number of obstacles added since the last <see cref="ClearObstacles"/> (each is stored once per heading).</summary>
    public int ObstacleCount => _obstacleCount;
    /// <summary>The C-space polygons of one heading, in the order they were added.</summary>
    public IReadOnlyList<(FastPolygonF Poly, float Penalty)> Bucket(int theta) => _buckets[theta];
    /// <summary>The engine's CoreTechPrint / warning lines, for tests and the log.</summary>
    public event Action<string>? Log;
    internal void RaiseLog(string line) => Log?.Invoke(line);

    /// <summary>Whether the tight padding pair was the one the last import used.</summary>
    public bool TightPadding { get; set; }
    public float RobotPadding => TightPadding ? TightRobotPaddingMm : RobotPaddingMm;
    public float ObstaclePadding => TightPadding ? TightObstaclePaddingMm : ObstaclePaddingMm;

    /// <summary><c>xythetaEnvironment::ClearObstacles()</c> 0x00851FB6: every heading's bucket is emptied.</summary>
    public void ClearObstacles()
    {
        foreach (var b in _buckets) b.Clear();
        _obstacleCount = 0;
    }

    /// <summary>
    /// The robot's bounding quad at a heading, padded: <c>Robot::GetBoundingQuadXY(pose, padding)</c> 0x00514D74. The canonical quad is (TL, BL, TR, BR) = (front, -w), (front, w), (back, -w),
    /// (back, w); a non-zero padding adds (p, -p), (p, p), (-p, -p), (-p, p) corner by corner; each corner is rotated by <c>RotationMatrix2d(angle)</c> = [cosf, -sinf; sinf, cosf] with
    /// <c>x' = x*c + y*(-s)</c>, <c>y' = x*s + y*c</c> (separate float operations), and the pose translation (zero here) is added. The pose's rotation goes through the engine's quaternion
    /// (<c>Pose3d(Radians, Z_AXIS, ...)</c>, <c>GetAngleAroundZaxis</c>); that round trip is not transliterated, so the heading is used directly (MISSING).
    /// </summary>
    // fidelity: M13-003
    public static P2f[] RobotQuad(float headingRad, float paddingMm)
    {
        var q = new[] { new P2f(RobotFrontMm, -RobotHalfWidthMm), new P2f(RobotFrontMm, RobotHalfWidthMm), new P2f(RobotBackMm, -RobotHalfWidthMm), new P2f(RobotBackMm, RobotHalfWidthMm) };
        if (paddingMm != 0f)
        {
            float p = paddingMm, n = -paddingMm;
            var pad = new[] { new P2f(p, n), new P2f(p, p), new P2f(n, n), new P2f(n, p) };
            for (int i = 0; i < 4; i++) q[i] = new P2f(q[i].X + pad[i].X, q[i].Y + pad[i].Y);
        }
        float c = MathF.Cos(headingRad), s = MathF.Sin(headingRad);
        float negS = -s;
        for (int i = 0; i < 4; i++)
        {
            float x = q[i].X, y = q[i].Y;
            float nx = x * c + y * negS;
            float ny = x * s + y * c;
            q[i] = new P2f(nx, ny);
        }
        return q;
    }

    /// <summary>
    /// <c>xythetaEnvironment::ExpandCSpace(obstacle, robot)</c> 0x008550E8, the engine's merge of two clockwise polygons: the start index k of the robot's edges is the one whose edge angle plus pi
    /// is the smallest counter-clockwise <c>angularDistance</c> from the obstacle's first edge; the first point is <c>obstacle[0] - robot[k]</c>; then the obstacle's edges (added) and the robot's
    /// (subtracted) are taken in order of their angular distance from that first angle (the obstacle's on a strict win, the robot's otherwise), the leftovers are appended, and the closing
    /// duplicate of the first point is dropped. All in binary32; <c>Radians(float)</c> rescales every angle.
    /// </summary>
    // fidelity: M13-003
    public static P2f[] ExpandCSpace(IReadOnlyList<P2f> obstacle, IReadOnlyList<P2f> robot)
    {
        int nA = obstacle.Count, nB = robot.Count;
        float baseAngle = EngineF.Rescale(PolygonF.EdgeAngle(obstacle, 0));
        float best = float.MaxValue; int k = 0;
        for (int i = 0; i < nB; i++)
        {
            float angB = EngineF.Rescale((float)((double)PolygonF.EdgeAngle(robot, i) + EngineF.PiD));
            float dist = EngineF.AngularDistance(angB, baseAngle, false);
            if (dist < best) { best = dist; k = i; }                                    // vcmpe s0, s16; itt mi
        }
        var a0 = obstacle[0]; var bk = robot[k];
        float cx = a0.X - bk.X, cy = a0.Y - bk.Y;
        var result = new List<P2f> { new(cx, cy) };
        int iA = 0, cntB = 0, curB = k;
        if (nA != 0 && nB != 0)
        {
            while (true)
            {
                float angB = EngineF.Rescale((float)((double)PolygonF.EdgeAngle(robot, curB) + EngineF.PiD));
                float dB = EngineF.AngularDistance(angB, baseAngle, false);
                float angA = EngineF.Rescale(PolygonF.EdgeAngle(obstacle, iA));
                float dA = EngineF.AngularDistance(angA, baseAngle, false);
                if (dA < dB)
                {
                    var v = PolygonF.EdgeVector(obstacle, iA);
                    cx = v.X + cx; cy = v.Y + cy;                                       // vadd.f32 s0 = s2 + s0
                    iA++;
                }
                else
                {
                    var v = PolygonF.EdgeVector(robot, curB);
                    cx = cx - v.X; cy = cy - v.Y;                                       // vsub.f32 s0 = s2 - s0
                    curB = (curB + 1) % nB; cntB++;
                }
                result.Add(new P2f(cx, cy));
                if (cntB >= nB || iA >= nA) break;                                      // 0x00855466..0x00855478
            }
        }
        for (; iA < nA; iA++)                                                           // 0x0085523C..0x0085528A
        {
            var v = PolygonF.EdgeVector(obstacle, iA);
            cx = v.X + cx; cy = v.Y + cy;
            result.Add(new P2f(cx, cy));
        }
        for (; cntB < nB; cntB++)                                                       // 0x0085529A..0x008552EE
        {
            var v = PolygonF.EdgeVector(robot, curB);
            cx = cx - v.X; cy = cy - v.Y;
            result.Add(new P2f(cx, cy));
            curB = (curB + 1) % nB;
        }
        result.RemoveAt(result.Count - 1);                                              // 0x008552F0..0x008552F4: the last point repeats the first
        return result.ToArray();
    }

    /// <summary>
    /// <c>xythetaEnvironment::AddObstacleWithExpansion(obstacle, robot, theta, penalty)</c> 0x00855528: a theta outside the table prints a message and uses 0 (0x0085554C..0x00855560);
    /// the C-space polygon goes into that heading's bucket with the penalty.
    /// </summary>
    // fidelity: M13-003
    public void AddObstacleWithExpansion(IReadOnlyList<P2f> obstacle, IReadOnlyList<P2f> robot, int theta, float penalty)
    {
        if ((uint)theta >= (uint)_buckets.Length) { Log?.Invoke($"AddObstacleWithExpansion: theta {theta} is out of range"); theta = 0; }
        _buckets[theta].Add((new FastPolygonF(ExpandCSpace(obstacle, robot)), penalty));
    }

    /// <summary>
    /// The import loop (0x004FDF8C..0x004FE1E6) for a list of obstacle polygons: each becomes a <c>ConvexPolygon</c> and is radially expanded by the obstacle padding; then for each heading the
    /// robot's padded quad at that heading becomes a <c>ConvexPolygon</c> (through <c>ImportQuad2d</c>) and every obstacle is expanded against it with the penalty 0.1f. The obstacle count is the
    /// number of polygons imported.
    /// </summary>
    // fidelity: M13-003
    public void ImportObstaclePolygons(IEnumerable<IReadOnlyList<P2f>> polygons, bool tightPadding)
    {
        TightPadding = tightPadding;
        ClearObstacles();
        var obstacles = polygons.Select(p => PolygonF.RadialExpand(PolygonF.ConvexPolygon(p), ObstaclePadding)).ToList();
        for (int t = 0; t < Primitives.NumAngles; t++)
        {
            var robot = PolygonF.ConvexPolygon(PolygonF.ImportQuad2d(RobotQuad(Primitives.Angles[t], RobotPadding)));
            foreach (var o in obstacles) AddObstacleWithExpansion(o, robot, t, ObstaclePenalty);
        }
        _obstacleCount = obstacles.Count;
    }

    /// <summary>
    /// A rectangle at a pose as one obstacle (the polygon the memory map would hold for an object's quad), added without clearing the others: its corners as floats, then the import's polygon
    /// path. <paramref name="penalty"/> replaces the import's 0.1f for tests that need a hard obstacle (the engine's import never does).
    /// </summary>
    public void AddRectangleObstacle(Pose3d pose, double lengthX, double widthY, string name = "", float? penalty = null)
    {
        var corners = new[] { new Vec3(-lengthX / 2, -widthY / 2, 0), new Vec3(lengthX / 2, -widthY / 2, 0), new Vec3(lengthX / 2, widthY / 2, 0), new Vec3(-lengthX / 2, widthY / 2, 0) }
            .Select(p => { var w = pose.Apply(p); return new P2f((float)w.X, (float)w.Y); }).ToArray();
        AddPolygonObstacle(PolygonF.ImportQuad2d(corners), penalty ?? ObstaclePenalty);
    }

    /// <summary>Adds one obstacle polygon (not cleared first): <see cref="ImportObstaclePolygons"/>'s per-obstacle step.</summary>
    public void AddPolygonObstacle(IReadOnlyList<P2f> polygon, float penalty)
    {
        var o = PolygonF.RadialExpand(PolygonF.ConvexPolygon(polygon), ObstaclePadding);
        for (int t = 0; t < Primitives.NumAngles; t++)
        {
            var robot = PolygonF.ConvexPolygon(PolygonF.ImportQuad2d(RobotQuad(Primitives.Angles[t], RobotPadding)));
            AddObstacleWithExpansion(o, robot, t, penalty);
        }
        _obstacleCount++;
    }

    /// <summary>
    /// STAND-IN for the memory-map read of <c>ImportBlockworldObstaclesIfNeeded</c> (MISSING: <c>MapComponent::GetCurrentMemoryMapHelper</c>, <c>FindContentIf</c> with the
    /// content-type-3 predicate 0x00502410 and the hash-set order, 0x004FDF1C..0x004FDF8C): every located object except the one being carried becomes an obstacle from its footprint, charger
    /// included (the engine's predicate excludes the charger's content type 4). <paramref name="tightPadding"/> is the function's own first bool argument: true selects 2/1, false 7/6.
    /// </summary>
    // fidelity: M13-003
    public void ImportBlockWorldObstacles(BlockWorld world, uint? carriedObjectId, bool tightPadding, IEnumerable<uint>? ignore = null)
    {
        var skip = new HashSet<uint>(ignore ?? Array.Empty<uint>());
        if (carriedObjectId is { } c) skip.Add(c);
        var polys = new List<IReadOnlyList<P2f>>();
        foreach (var o in world.LocatedObjects)
        {
            if (skip.Contains(o.ObjectId)) continue;
            var size = CubeGeometry.SizeOf(o.Type);
            var flat = new Pose3d(Mat3.AboutZ(o.Pose.AngleAroundZ), o.Pose.Translation with { Z = 0 });
            if (o.Type == Cozmo.Protocol.ObjectType.Charger_Basic)
                flat = flat.Compose(new Pose3d(Mat3.Identity, new Vec3(size.X / 2, 0, 0)));      // the charger's origin is its front lip
            polys.Add(PolygonF.ImportQuad2d(new[] { new Vec3(-size.X / 2, -size.Y / 2, 0), new Vec3(size.X / 2, -size.Y / 2, 0), new Vec3(size.X / 2, size.Y / 2, 0), new Vec3(-size.X / 2, size.Y / 2, 0) }
                .Select(p => { var w = flat.Apply(p); return new P2f((float)w.X, (float)w.Y); }).ToArray()));
        }
        ImportObstaclePolygons(polys, tightPadding);
    }

    /// <summary>
    /// <c>xythetaEnvironment::IsInCollision(State_c)</c> 0x008515F8: the heading wraps and rounds to a bucket (<see cref="EngineF.ThetaIndex"/>); hard collision only - a containing
    /// polygon whose penalty is at least 1000.0f (<c>vcmpe.f32</c>, <c>bge</c> at 0x008516D8/0x008516DC).
    /// </summary>
    // fidelity: M13-003
    public bool IsInCollision(float xMm, float yMm, float thetaRad)
    {
        int idx = EngineF.ThetaIndex(thetaRad, Primitives.InverseAngleStep, Primitives.NumAngles);
        foreach (var (poly, penalty) in _buckets[idx])
            if (poly.Contains(xMm, yMm) && penalty >= HardPenalty) return true;
        return false;
    }

    /// <summary><c>IsInCollision(State)</c> 0x008515BC: the cell coordinates to mm (<c>res * (float)x</c>), the heading from the table, then the same test.</summary>
    public bool IsInCollision(LatticeState s)
    {
        float x = Primitives.ResolutionMm * (float)s.X, y = Primitives.ResolutionMm * (float)s.Y;
        return IsInCollision(x, y, Primitives.Angles[s.Theta]);
    }

    /// <summary><c>xythetaEnvironment::IsInSoftCollision(State)</c> 0x00851708: any containing polygon in the state's heading bucket.</summary>
    // fidelity: M13-003
    public bool IsInSoftCollision(LatticeState s)
    {
        float x = Primitives.ResolutionMm * (float)s.X, y = Primitives.ResolutionMm * (float)s.Y;
        foreach (var (poly, _) in _buckets[s.Theta]) if (poly.Contains(x, y)) return true;
        return false;
    }

    /// <summary><c>GetCollisionPenalty(State)</c> 0x008517B0: the first containing polygon's penalty, or 0.0f.</summary>
    // fidelity: M13-003
    public float PenaltyAt(LatticeState s)
    {
        float x = Primitives.ResolutionMm * (float)s.X, y = Primitives.ResolutionMm * (float)s.Y;
        foreach (var (poly, penalty) in _buckets[s.Theta]) if (poly.Contains(x, y)) return penalty;
        return 0f;
    }

    /// <summary>
    /// <c>SuccessorIterator::Next</c> 0x0085110C over a state: for each primitive of the state's heading in file order (the reflected set when <paramref name="reverse"/>), the successor
    /// state id, the action index, the total cost <c>soft + (startCost + primitive cost)</c> and the soft part. The collision test is the engine's: a non-turning primitive
    /// (<c>end theta == start theta</c> byte) tests every intermediate pose in the end heading's bucket; a turning one walks the poses last to first, each in its own heading's bucket; a containing
    /// polygon with penalty below 1000.0f adds <c>base + penalty * reciprocal</c> (base 0.0f forward, 1000.0f for a reverse action) and one at or above rejects the primitive. The position is the
    /// start state's, or the NEW state's in reverse mode (0x008511EE..0x00851214). The engine's bounding-box and <c>Bounds</c> shortcuts (0x00851216..0x00851286, 0x0085131E..) only skip polygons whose
    /// boxes cannot contain a pose; they are not needed for the result and are not built.
    /// </summary>
    // fidelity: M13-003
    public IEnumerable<LatticeSuccessor> Successors(LatticeState s, float startCost, bool reverse)
    {
        var set = reverse ? Primitives.Reflected : Primitives.ByAngle;
        float res = Primitives.ResolutionMm;
        float x0f = res * (float)s.X, y0f = res * (float)s.Y;
        foreach (var prim in set[s.Theta])
        {
            short nx = unchecked((short)(s.X + prim.EndX)), ny = unchecked((short)(s.Y + prim.EndY));
            float ox, oy;
            if (!reverse) { ox = x0f; oy = y0f; }
            else { ox = res * (float)nx; oy = res * (float)ny; }
            bool reverseAction = Primitives.Actions[prim.ActionIndex].Reverse;
            float baseCost = reverseAction ? HardPenalty : 0f;                          // the literal pair at 0x008515B0: 0.0f, 1000.0f
            float soft = 0f; bool hard = false;
            bool nonTurning = prim.EndTheta == prim.StartTheta;
            var inter = prim.Intermediate;
            if (nonTurning)
            {
                foreach (var (poly, penalty) in _buckets[prim.EndTheta])
                    for (int i = 0; i < inter.Count; i++)
                    {
                        if (!poly.Contains(ox + inter[i].X, oy + inter[i].Y)) continue;
                        if (penalty >= HardPenalty) { hard = true; goto done; }
                        soft = soft + (baseCost + penalty * inter[i].Reciprocal);
                    }
            }
            else
            {
                for (int i = inter.Count - 1; i >= 0; i--)
                {
                    float px = ox + inter[i].X, py = oy + inter[i].Y;
                    foreach (var (poly, penalty) in _buckets[inter[i].ThetaIndex])
                    {
                        if (!poly.Contains(px, py)) continue;
                        if (penalty >= HardPenalty) { hard = true; continue; }          // the hit flag is set and the walk goes on (0x0085149A, then 0x0085149E)
                        soft = soft + (baseCost + penalty * inter[i].Reciprocal);
                    }
                }
            }
            done:
            if (hard) continue;
            float total = soft + (startCost + prim.Cost);                                // vadd.f32 s2 = start + cost; vadd.f32 s0 = soft + s2 (0x0085150A..0x0085151E)
            var next = new LatticeState(nx, ny, (byte)prim.EndTheta);
            yield return new LatticeSuccessor(next.Id, prim.ActionIndex, total, soft, prim);
        }
    }

    /// <summary>
    /// <c>RoundSafe(State_c, State&amp;)</c> 0x00855AF8: the heading's index, then of the (up to four) cells around the point that are not in collision, the one with the smallest squared distance
    /// (strictly, from 999999.875f, the squares formed in double). False when none is free.
    /// </summary>
    // fidelity: M13-018
    public bool RoundSafe(StateC c, out LatticeState state)
    {
        int theta = EngineF.ThetaIndex(c.Theta, Primitives.InverseAngleStep, Primitives.NumAngles);
        float res = Primitives.ResolutionMm, inv = Primitives.InverseResolution;
        float sx = c.X * inv, sy = inv * c.Y;
        int xf = (int)MathF.Floor(sx), xc = (int)MathF.Ceiling(sx), yf = (int)MathF.Floor(sy), yc = (int)MathF.Ceiling(sy);
        float bestD = EngineF.Bits(0x497423FE);
        short bx = 0, by = 0;
        for (int x = xf; x <= xc; x++)
            for (int y = yf; y <= yc; y++)
            {
                var cand = new LatticeState((short)x, (short)y, (byte)theta);
                if (IsInCollision(cand)) continue;
                float ddx = res * (float)(short)x - c.X, ddy = res * (float)(short)y - c.Y;
                float d2 = (float)((double)ddx * ddx + (double)ddy * ddy);
                if (bestD > d2) { bx = (short)x; by = (short)y; bestD = d2; }
            }
        state = new LatticeState(bx, by, (byte)theta);
        return (double)bestD < BitConverter.Int64BitsToDouble(0x412E847F9999999A);   // vcmpe.f64 with the literal at 0x00855CA8 (999999.8)
    }
}

/// <summary>A plan: the start state and the primitives taken from it (<c>Anki::Planning::xythetaPlan</c>).</summary>
public sealed record LatticePlan(LatticeState Start, IReadOnlyList<MotionPrimitive> Actions, float Cost, int Expansions)
{
    public IEnumerable<LatticeState> States()
    {
        var s = Start; yield return s;
        foreach (var a in Actions) { s = new LatticeState(unchecked((short)(s.X + a.EndX)), unchecked((short)(s.Y + a.EndY)), (byte)a.EndTheta); yield return s; }
    }
}

/// <summary>
/// The engine's <c>Anki::Planning::xythetaPlanner</c> / <c>xythetaPlannerImpl</c> in binary32, with the engine's open list and state table:
/// <c>ComputePath</c> 0x008586A0 (goal check on pop, <c>ExpandState</c>, the max-expansion bound), <c>ExpandState</c> 0x0085A34C, <c>InitializeHeuristic</c> 0x008598DC,
/// <c>ExpandCollisionStatesFromGoal</c> 0x00859BF0, <c>heur</c> 0x0085A780 and <c>heur_internal</c> 0x0085A7B8.
///
/// <b>The open list is the engine's multimap.</b> <c>OpenList</c> is a <c>std::multimap&lt;float, StateID&gt;</c> (0x0084E4DA..0x0084E67C): <c>insert</c> goes after every entry with an equal key, <c>pop</c>
/// takes the first, so equal <c>f</c> pops in insertion order; <c>remove</c> erases one entry. <see cref="OpenList"/> keeps that order.
///
/// <b>The heuristic shares one best-cost map with the goal expansion.</b> <c>ExpandCollisionStatesFromGoal</c> writes the planner's map (planner+0xAC) and <c>heur</c> reads it first; a state not in it
/// goes to <c>heur_internal</c>, whose value is NOT stored back: <c>min over goals of (float)(1/60.0 * (double)distance + (double)goalCost)</c> with the distance <c>GetDistanceBetween(State_c, State)</c>.
///
/// NOT BUILT (MISSING): the reuse of a still-safe previous plan (<c>PlanIsSafe</c>, 0x008587AC..0x008587C6) - the live caller always passes the replan flag, which skips it - and
/// <c>LatticePlannerImpl::GetCompletePath</c> (<see cref="ToPath"/> is a labelled stand-in for it).
/// </summary>
// fidelity: M13-018
public class LatticePlanner
{
    /// <summary>
    /// 30,000,000 = 0x01C9C380: the maximum number of state expansions, the argument <c>DoPlanning</c>
    /// passes to <c>Replan</c> (0x005000F2/0x005000FA; <c>ComputePath</c> warns "exceeded max expansions
    /// of %u, stopping" at 0x00858A96 and returns 0).
    /// </summary>
    // fidelity: M13-018
    public const int MaxExpansions = 30_000_000;

    public LatticePlanner(LatticeEnvironment env) => Env = env;
    public LatticeEnvironment Env { get; }

    /// <summary>
    /// impl+0x108: the pre-plan wait in ms. The ctor 0x004FCCF6 sets 0 and
    /// <c>LatticePlanner::SetArtificialPlannerDelay_ms</c> 0x004FFFEC sets it (M13-018).
    /// </summary>
    // fidelity: M13-018
    public int ArtificialPlannerDelayMs { get; set; }

    /// <summary>impl+0xA1 (= planner context +0x99): the bool <c>StartPlanning</c> stores (0x004FEB7E): true = force a replan.</summary>
    // fidelity: M13-018
    public bool ReplanFlag { get; private set; }

    /// <summary>
    /// impl+0xF2: the run/continue flag, not an abort flag. 1 = keep planning (ctor 0x004FCCCA and
    /// <c>StartPlanning</c> 0x004FF310), 0 = stop (<c>StopPlanning</c> 0x004FD1C8). <c>DoPlanning</c> reads it
    /// during the sleep and passes it to <c>Replan</c>; <c>ComputePath</c> tests it before every pop (0x00858886..0x00858890) (M13-018).
    /// </summary>
    // fidelity: M13-018
    public bool RunFlag { get; private set; } = true;

    /// <summary><c>LatticePlannerImpl::StopPlanning</c> 0x004FD1BA: clear the run flag.</summary>
    // fidelity: M13-018
    public void StopPlanning() => RunFlag = false;

    /// <summary>The engine's <c>DoPlanning</c> result: 0 failure, 3 empty plan, 2 success.</summary>
    // fidelity: M13-018
    public enum PlanningResult { Failure = 0, EmptyPlan = 3, Success = 2 }

    /// <summary>The result of the last <see cref="PlanTo"/>: the engine's <c>DoPlanning</c> codes.</summary>
    // fidelity: M13-018
    public PlanningResult LastPlanningResult { get; private set; } = PlanningResult.Failure;

    /// <summary>
    /// <c>LatticePlannerImpl::StartPlanning</c> 0x004FEB44: store the bool at impl+0xA1, set the run flag to
    /// 1 (0x004FF310) and import the world's obstacles. <paramref name="forceReplan"/> true selects the
    /// 7.0/6.0 padding pair (bool 0 at 0x004FEEFE); false would first select 2.0/1.0 (bool 1 at 0x004FEC38)
    /// and then reuse a safe old plan through <c>FindClosestPlanSegmentToPose</c> 0x00856310 and
    /// <c>PlanIsSafe</c> 0x00850B78 (return 2 at 0x004FF42E). That false branch is <b>unreachable engine
    /// code</b>: all three recovered <c>ComputePath</c> callers pass true (<c>ComputePathHelper</c> 0x004FD330,
    /// <c>FaceAndApproachPlanner</c> 0x004F355C, <c>MinimalAnglePlanner</c> 0x00503BC2; Appendix G 4d), so
    /// only the true/live path is built here.
    /// </summary>
    // fidelity: M13-018
    public void StartPlanning(BlockWorld world, uint? carriedObjectId, bool forceReplan, IEnumerable<uint>? ignore = null)
    {
        ReplanFlag = forceReplan;
        RunFlag = true;
        Env.ImportBlockWorldObstacles(world, carriedObjectId, tightPadding: !forceReplan, ignore);
    }

    /// <summary>
    /// <c>LatticePlannerImpl::DoPlanning</c> 0x00500090: sleep in <c>min(remaining, 10) ms</c> chunks up to
    /// <see cref="ArtificialPlannerDelayMs"/> (impl+0x108) checking the run flag each iteration, then plan.
    /// Returns 0 when the plan failed, 3 when it succeeded with an empty segment list and 2 on success.
    /// </summary>
    // fidelity: M13-018
    public PlanningResult DoPlanning(StateC start, IReadOnlyList<StateC> goals, out LatticePlan? plan)
    {
        if (ArtificialPlannerDelayMs > 0)
        {
            int slept = 0;
            while (slept < ArtificialPlannerDelayMs)
            {
                if (!RunFlag) break;                                                     // 0x005000BE cbz r0, 0x005000EC: the sleep ends and Replan is still called
                int chunk = Math.Min(ArtificialPlannerDelayMs - slept, 10);
                Thread.Sleep(chunk);
                slept += chunk;
            }
        }
        plan = ComputePath(start, goals);
        if (plan is null) return PlanningResult.Failure;
        if (plan.Actions.Count == 0) return PlanningResult.EmptyPlan;
        return PlanningResult.Success;
    }

    // ---------------------------------------------------------------------------------------------------- the open list and the state table

    /// <summary>The engine's <c>OpenList</c>: a multimap from <c>f</c> to state, FIFO among equal keys (<c>__emplace_multi</c> inserts at the upper bound).</summary>
    internal sealed class OpenList
    {
        private readonly SortedDictionary<float, LinkedList<uint>> _map = new();
        private readonly Dictionary<LinkedListNode<uint>, float> _keyOf = new();
        public int Count { get; private set; }
        public bool Empty => Count == 0;
        public LinkedListNode<uint> Insert(uint id, float f)
        {
            if (!_map.TryGetValue(f, out var list)) _map[f] = list = new LinkedList<uint>();
            var node = list.AddLast(id);
            _keyOf[node] = f; Count++;
            return node;
        }
        public float TopF() { foreach (var kv in _map) return kv.Key; return float.NaN; }
        public uint Pop()
        {
            foreach (var kv in _map)
            {
                var node = kv.Value.First!;
                Remove(node);
                return node.Value;
            }
            throw new InvalidOperationException("pop on an empty open list");
        }
        public void Remove(LinkedListNode<uint> node)
        {
            float f = _keyOf[node];
            var list = _map[f];
            list.Remove(node);
            if (list.Count == 0) _map.Remove(f);
            _keyOf.Remove(node); Count--;
        }
    }

    /// <summary>A <c>StateEntry</c> (the table value): the open-list handle (+0), the closed tag (+4, -1 while open, 0 once expanded), the parent (+8), the action (+0xC), the soft penalty (+0x10) and g (+0x14).</summary>
    private sealed class StateEntry
    {
        public LinkedListNode<uint>? OpenNode;
        public int ClosedTag = -1;
        public uint Parent;
        public MotionPrimitive? Prim;
        public float Penalty;
        public float G;
    }

    // ---------------------------------------------------------------------------------------------------- the search

    private Dictionary<uint, StateEntry> _table = new();
    private OpenList _open = new();
    /// <summary>The shared best-cost map (planner+0xAC) for tests.</summary>
    internal IReadOnlyDictionary<uint, float> HeuristicMapForTests => _heurMap;
    private readonly Dictionary<uint, float> _heurMap = new();           // planner+0xAC
    private readonly List<(byte Id, LatticeState State)> _goalStates = new();
    private readonly List<(byte Id, StateC C)> _goalC = new();
    private readonly List<(byte Id, float Cost)> _goalCosts = new();
    private int _expansions;
    private int _considerations;
    /// <summary>planner+0x74: the final cost of the last successful plan.</summary>
    public float LastFinalCost { get; private set; }
    /// <summary>planner+0x78: the goal id (the index in the goal list passed in) of the goal state the last plan reached.</summary>
    public int ChosenGoalId { get; private set; }

    /// <summary>
    /// <c>CheckContextStart</c> 0x008596B0: a start in hard collision fails (info log); otherwise the cell is the rounded x and y (<c>roundf(x * invRes)</c>) with the heading index, and a
    /// rounded cell that is in collision goes to <see cref="LatticeEnvironment.RoundSafe"/>, which must find a free neighbour.
    /// </summary>
    // fidelity: M13-018
    public bool CheckContextStart(StateC start, out LatticeState state)
    {
        state = default;
        if (Env.IsInCollision(start.X, start.Y, start.Theta)) return false;
        var set = Env.Primitives;
        float inv = set.InverseResolution;
        float xr = MathF.Round(start.X * inv, MidpointRounding.AwayFromZero);
        float yr = MathF.Round(inv * start.Y, MidpointRounding.AwayFromZero);
        var rounded = new LatticeState((short)(int)xr, (short)(int)yr, (byte)EngineF.ThetaIndex(start.Theta, set.InverseAngleStep, set.NumAngles));
        if (Env.IsInCollision(rounded)) { if (!Env.RoundSafe(start, out rounded)) return false; }
        state = rounded;
        return true;
    }

    /// <summary>
    /// <c>CheckContextGoals</c> 0x008591B0 (with <c>CheckGoal</c> 0x00858F90 inlined): goals in hard collision are dropped first; each remaining goal is rounded like the start (<c>RoundSafe</c> when the
    /// rounded cell is in collision, dropped when that fails) and kept as (id, StateID) and (id, State_c of the rounded cell). Returns whether any goal is left.
    /// </summary>
    // fidelity: M13-018
    public bool CheckContextGoals(IReadOnlyList<StateC> goals, List<(byte Id, LatticeState State)> outStates, List<(byte Id, StateC C)> outC)
    {
        var set = Env.Primitives;
        for (int i = 0; i < goals.Count; i++)
        {
            var g = goals[i];
            if (Env.IsInCollision(g.X, g.Y, g.Theta)) continue;
            float inv = set.InverseResolution;
            float xr = MathF.Round(g.X * inv, MidpointRounding.AwayFromZero);
            float yr = MathF.Round(inv * g.Y, MidpointRounding.AwayFromZero);
            var rounded = new LatticeState((short)(int)xr, (short)(int)yr, (byte)EngineF.ThetaIndex(g.Theta, set.InverseAngleStep, set.NumAngles));
            if (Env.IsInCollision(rounded) && !Env.RoundSafe(g, out rounded)) continue;
            outStates.Add(((byte)i, rounded));
            outC.Add(((byte)i, new StateC(set.ResolutionMm * (float)rounded.X, set.ResolutionMm * (float)rounded.Y, set.Angles[rounded.Theta])));
        }
        return outStates.Count > 0;
    }

    /// <summary>
    /// <c>InitializeHeuristic</c> 0x008598DC: the best-cost map is cleared; the goals are visited LAST TO FIRST; a goal not in soft collision costs 0.0f, otherwise <see cref="ExpandCollisionStatesFromGoal"/>
    /// and a cost above 1000.0f removes the goal by swapping it with the last one and popping (goals, goal states and costs alike). True when a goal is left.
    /// </summary>
    // fidelity: M13-018
    private bool InitializeHeuristic()
    {
        _heurMap.Clear();
        _goalCosts.Clear();
        foreach (var g in _goalStates) _goalCosts.Add((g.Id, 0f));
        for (int i = _goalStates.Count - 1; i >= 0; i--)
        {
            float cost;
            if (Env.IsInSoftCollision(_goalStates[i].State))
            {
                cost = ExpandCollisionStatesFromGoal(_goalStates[i].State.Id);
                if (cost > LatticeEnvironment.HardPenalty)
                {
                    int last = _goalStates.Count - 1;
                    (_goalStates[i], _goalStates[last]) = (_goalStates[last], _goalStates[i]);
                    (_goalC[i], _goalC[last]) = (_goalC[last], _goalC[i]);
                    (_goalCosts[i], _goalCosts[last]) = (_goalCosts[last], _goalCosts[i]);
                    _goalStates.RemoveAt(last); _goalC.RemoveAt(last); _goalCosts.RemoveAt(last);
                    continue;
                }
            }
            else cost = 0f;
            _goalCosts[i] = (_goalCosts[i].Id, cost);
        }
        return _goalStates.Count > 0;
    }

    /// <summary>
    /// <c>ExpandCollisionStatesFromGoal(goal)</c> 0x00859BF0: a Dijkstra from the goal over the REFLECTED primitives with its own open list and closed set. The goal's cost is 0 in the shared
    /// best-cost map; each popped state is looked at: not in soft collision returns its <c>f</c>; otherwise the map keeps the smaller of its value and <c>f</c> (a new key takes <c>f</c>), the state is
    /// closed, and every successor (<c>startCost = f</c>, reverse mode) not already closed is inserted with its total cost. An empty open list or a cleared run flag returns 0.0f; after the 1000-expansion
    /// bound (<c>cmp r6,#0x3E8</c>, 0x00859ECC) it warns and returns the last popped <c>f</c>.
    /// </summary>
    // fidelity: M13-018
    private float ExpandCollisionStatesFromGoal(uint goalId)
    {
        _heurMap[goalId] = 0f;
        var closed = new HashSet<uint>();
        var open = new OpenList();
        open.Insert(goalId, 0f);
        uint expansions = 0;
        float f = 0f;
        while (true)
        {
            if (open.Empty) return 0f;                                                  // 0x00859F0C..0x00859F4A
            if (!RunFlag) return 0f;                                                    // 0x00859C5E..0x00859C68
            f = open.TopF();
            uint s = open.Pop();
            var st = LatticeState.FromId(s);
            if (!Env.IsInSoftCollision(st)) return f;                                   // 0x00859F50
            if (_heurMap.TryGetValue(s, out var old)) { if (f < old) _heurMap[s] = f; } else _heurMap[s] = f;
            closed.Add(s);
            foreach (var succ in Env.Successors(st, f, reverse: true))
                if (!closed.Contains(succ.NextId)) open.Insert(succ.NextId, succ.Cost);
            expansions++;
            if (expansions > 1000) { Env.RaiseLog("exceeded max allowed expansions of 1000"); return f; }
        }
    }

    /// <summary><c>heur(StateID)</c> 0x0085A780: the best-cost map's value when present, else <see cref="HeurInternal"/> (not stored).</summary>
    private float Heur(uint id) => _heurMap.TryGetValue(id, out var v) ? v : HeurInternal(id);

    /// <summary>
    /// <c>heur_internal</c> 0x0085A7B8: over the goals in order, <c>(float)(env+0x78 * (double)GetDistanceBetween(goal State_c, state) + (double)goalCost)</c>, keeping the
    /// smallest (starting at FLT_MAX); no goals gives FLT_MAX.
    /// </summary>
    private float HeurInternal(uint id)
    {
        var st = LatticeState.FromId(id);
        float res = Env.Primitives.ResolutionMm;
        float best = float.MaxValue;
        for (int i = 0; i < _goalC.Count; i++)
        {
            float dy = res * (float)st.Y - _goalC[i].C.Y;                                // GetDistanceBetween(State_c, State) 0x00850DCC
            float dx = res * (float)st.X - _goalC[i].C.X;
            float dist = EngineF.SqrtOfDoubleSquares(dx, dy);
            float h = (float)(MotionPrimitiveSet.InverseMaxVelocity * (double)dist + (double)_goalCosts[i].Cost);
            if (best > h) best = h;
        }
        return best;
    }

    /// <summary>
    /// <c>ExpandState(StateID)</c> 0x0085A34C: the state's entry must not be closed (0x0085A366..0x0085A370); every successor of it (<c>startCost = g</c>) is considered in primitive order: an existing
    /// open entry with a strictly larger g is replaced (removed from the open list and re-inserted with <c>g' + heur</c>, parent, action and g updated (the penalty stays as first stored), the tag back to -1), a new one is
    /// inserted with <c>g' + heur</c>; a closed entry is left alone; at the end the entry is closed.
    /// </summary>
    // fidelity: M13-018
    private void ExpandState(uint id)
    {
        var entry = _table[id];
        if (entry.ClosedTag == 0) return;                                                // "ExpandState: expanding a closed state" (0x0085A370..0x0085A3C0)
        float g = entry.G;
        foreach (var succ in Env.Successors(LatticeState.FromId(id), g, reverse: false))
        {
            _considerations++;
            float g2 = succ.Cost;
            if (_table.TryGetValue(succ.NextId, out var ex))
            {
                if (ex.ClosedTag == 0) continue;
                if (g2 < ex.G)
                {
                    float h = Heur(succ.NextId);
                    _open.Remove(ex.OpenNode!);
                    ex.OpenNode = _open.Insert(succ.NextId, g2 + h);
                    ex.ClosedTag = -1; ex.Parent = id; ex.Prim = succ.Prim; ex.G = g2;
                }
            }
            else
            {
                float h = Heur(succ.NextId);
                var node = _open.Insert(succ.NextId, g2 + h);
                _table[succ.NextId] = new StateEntry { OpenNode = node, ClosedTag = -1, Parent = id, Prim = succ.Prim, Penalty = succ.SoftPenalty, G = g2 };
            }
        }
        entry.ClosedTag = 0;                                                             // 0x0085A53E..0x0085A544: [entry+4] = [planner+0x8C] = 0
    }

    /// <summary>
    /// <c>xythetaPlannerImpl::ComputePath(maxExpansions, abortFlag)</c> 0x008586A0 for one planning call (the previous-plan reuse is not built, see the class). The goals are validated
    /// (<see cref="CheckContextGoals"/>), the start rounded (<see cref="CheckContextStart"/>), the tables reset, the heuristic initialised, the start inserted at f = 0.0f, and then states are popped:
    /// the run flag is tested first, a popped goal state ends the search and builds the plan (at most 1000 steps), otherwise it is expanded and the expansion count compared with the maximum
    /// (unsigned greater-than fails). An empty open list fails.
    /// </summary>
    // fidelity: M13-018
    /// <summary>The goal-id hash (planner+0x10) of the last <see cref="ComputePath"/>, as built before the heuristic pruned any goal.</summary>
    internal IReadOnlyDictionary<uint, byte> GoalIdMapForTests { get; private set; } = new Dictionary<uint, byte>();

    public LatticePlan? ComputePath(StateC start, IReadOnlyList<StateC> goals, int maxExpansions = MaxExpansions)
    {
        _goalStates.Clear(); _goalC.Clear();
        if (!CheckContextGoals(goals, _goalStates, _goalC)) return null;
        if (!CheckContextStart(start, out var startState)) return null;
        _table = new(); _open = new(); _expansions = 0; _considerations = 0;
        // the goal-id hash (planner+0x10) is filled from the CheckContextGoals output (0x00858760..0x00858780) BEFORE InitializeHeuristic, which prunes only planner+4, +0x24 and +0xA0
        // (0x008598DC..0x00859AB6) and never touches it: a goal that was pruned (cost > 1000) still ends the search when it is popped
        var goalMap = new Dictionary<uint, byte>();
        foreach (var g in _goalStates) goalMap[g.State.Id] = g.Id;
        GoalIdMapForTests = goalMap;
        if (!InitializeHeuristic()) return null;
        uint startId = startState.Id;
        var startNode = _open.Insert(startId, 0f);
        _table[startId] = new StateEntry { OpenNode = startNode, ClosedTag = -1, Parent = startId, G = 0f };
        while (!_open.Empty)
        {
            if (!RunFlag) return null;                                                   // 0x00858886..0x00858890
            uint s = _open.Pop();
            if (goalMap.ContainsKey(s))
            {
                LastFinalCost = _table[s].G;
                ChosenGoalId = goalMap[s];                                              // 0x008589E2..0x008589E4: the goal id byte is stored at +0x78
                return BuildPlan(startState, s);
            }
            ExpandState(s);
            _expansions++;
            if ((uint)_expansions > (uint)maxExpansions) return null;                    // 0x008588C6..0x008588CA: "exceeded max expansions"
        }
        return null;
    }

    /// <summary>
    /// <c>BuildPlan</c> 0x0085A5D8: from the goal state back through the parents to the start (a bounded loop of 1000 steps, 0x0085A610..0x0085A616), then reversed. This stack keeps the primitive
    /// itself where the engine keeps its action index and penalty.
    /// </summary>
    private LatticePlan? BuildPlan(LatticeState start, uint goalId)
    {
        var steps = new List<MotionPrimitive>();
        uint s = goalId;
        int n = 0;
        while (s != start.Id)
        {
            var e = _table[s];
            steps.Add(e.Prim!);
            s = e.Parent;
            n++;
            if (n >= 1000)                                                              // 0x0085A616 cmp r5,#0x3e8; blo loops: the 1000th step falls to the error
            {
                Env.RaiseLog("BuildPlan: the plan has more than 1000 steps");           // 0x0085A634 sErrorF, then the plan built so far is reversed and kept (0x0085A672..)
                break;
            }
        }
        steps.Reverse();
        return new LatticePlan(start, steps, LastFinalCost, _expansions);
    }

    /// <summary>The planner entry from poses: <c>LatticePlannerImpl</c> turns a <c>Pose3d</c> into a <c>State_c</c> (x, y, <c>GetAngleAroundZaxis</c>) as binary32 (0x004FEB84..0x004FEBA6).</summary>
    public static StateC ToStateC(Pose3d pose) => new((float)pose.Translation.X, (float)pose.Translation.Y, (float)EngineRadians.GetAngleAroundZaxis(pose.Rotation));

    /// <summary>
    /// STAND-IN for <c>LatticePlannerImpl::GetCompletePath</c> (MISSING, not built): the plan as robot path segments. Straight primitives in a row merge into one line; an in-place turn is a
    /// point turn to the new lattice heading; a turning primitive is its own <c>straight_length_mm</c> followed by its own <c>arc</c>, both of which <c>cozmo_mprim.json</c> states outright. A final
    /// point turn to the goal's exact heading is appended when the lattice heading differs from it. The segment values are binary32 (the engine's <c>PathSegment</c> words); how the engine
    /// builds them (the primitive's own <c>Path</c> with its speeds, <c>AppendToPath</c>) is not transliterated here.
    /// </summary>
    public IReadOnlyList<PathSegment> ToPath(LatticePlan plan, Pose3d? exactGoal, PathMotionProfile profile)
    {
        var p = profile;
        var path = new List<PathSegment>();
        float res = Env.Primitives.ResolutionMm;
        var states = plan.States().ToList();
        int i = 0;
        while (i < plan.Actions.Count)
        {
            var a = plan.Actions[i]; var s0 = states[i];
            var action = Env.Primitives.Actions[a.ActionIndex];
            bool straight = a.EndTheta == a.StartTheta && (a.EndX != 0 || a.EndY != 0);
            if (straight)
            {
                int j = i;
                while (j + 1 < plan.Actions.Count)
                {
                    var b = plan.Actions[j + 1]; var bAct = Env.Primitives.Actions[b.ActionIndex];
                    if (b.EndTheta == b.StartTheta && (b.EndX != 0 || b.EndY != 0) && bAct.Reverse == action.Reverse) j++; else break;
                }
                var s1 = states[j + 1];
                float speed = action.Reverse ? -p.ReverseSpeedMmps : p.SpeedMmps;
                path.Add(new PathSegment.Line(res * (float)s0.X, res * (float)s0.Y, res * (float)s1.X, res * (float)s1.Y, speed, p.AccelMmps2, p.DecelMmps2));
                i = j + 1;
                continue;
            }
            if (a.EndX == 0 && a.EndY == 0)
            {
                int j = i;
                while (j + 1 < plan.Actions.Count && plan.Actions[j + 1].EndX == 0 && plan.Actions[j + 1].EndY == 0) j++;
                var s1 = states[j + 1];
                path.Add(new PathSegment.PointTurn(res * (float)s0.X, res * (float)s0.Y, Env.Primitives.Angles[s1.Theta], StraightLinePlanner.PointTurnToleranceRad,
                                                   p.PointTurnSpeedRadPerSec, p.PointTurnAccelRadPerSec2, p.PointTurnDecelRadPerSec2, true));
                i = j + 1;
                continue;
            }
            float th0 = Env.Primitives.Angles[a.StartTheta];
            float x0 = res * (float)s0.X, y0 = res * (float)s0.Y;
            float line = a.StraightLengthMm;
            float xl = x0 + MathF.Cos(th0) * line, yl = y0 + MathF.Sin(th0) * line;
            if (MathF.Abs(line) > 0.5f) path.Add(new PathSegment.Line(x0, y0, xl, yl, p.SpeedMmps, p.AccelMmps2, p.DecelMmps2));
            if (a.Arc is { } arc)
                path.Add(new PathSegment.Arc(x0 + arc.CenterX, y0 + arc.CenterY, arc.Radius, arc.StartRad, arc.SweepRad, p.SpeedMmps, p.AccelMmps2, p.DecelMmps2));
            i++;
        }
        if (exactGoal is { } goal)
        {
            var end = states[^1];
            float latticeHeading = Env.Primitives.Angles[end.Theta];
            float goalHeading = (float)goal.AngleAroundZ;
            if (MathF.Abs(EngineF.RadiansMinus(goalHeading, latticeHeading)) > StraightLinePlanner.PointTurnToleranceRad)
                path.Add(new PathSegment.PointTurn(res * (float)end.X, res * (float)end.Y, goalHeading, StraightLinePlanner.PointTurnToleranceRad,
                                                   p.PointTurnSpeedRadPerSec, p.PointTurnAccelRadPerSec2, p.PointTurnDecelRadPerSec2, true));
        }
        return path;
    }

    /// <summary>
    /// Plans from a pose to one of several goal poses; null when no plan is found or the goals are all in
    /// collision. <c>virtual</c> so a caller can substitute the planner; the no-path branch is
    /// <c>DoPlanning</c>'s: Replan == 0 returns 0 (0x00500202) and no path is sent.
    /// </summary>
    // fidelity: M13-005, M13-018
    public virtual (LatticePlan Plan, IReadOnlyList<PathSegment> Path, Pose3d Goal)? PlanTo(Pose3d start, IReadOnlyList<Pose3d> goals, PathMotionProfile profile)
    {
        var goalCs = goals.Select(ToStateC).ToList();
        LastPlanningResult = DoPlanning(ToStateC(start), goalCs, out var plan);
        if (plan is null) return null;
        int gi = ChosenGoalId;
        var goal = goals[gi];
        return (plan, ToPath(plan, goal, profile), goal);
    }
}
