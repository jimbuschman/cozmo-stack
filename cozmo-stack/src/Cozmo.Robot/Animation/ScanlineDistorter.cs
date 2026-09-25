namespace Cozmo.Robot.Animation;

// fidelity: M5-031
/// <summary>
/// The engine's <c>ScanlineDistorter</c> (M5 inventory gap1 G5..G8, G11): a row shift curve over the face, with jitter,
/// and optional "off" noise. Every draw, GetNextDistortionFrame's hold draw included, goes through one function-static
/// RandomGenerator(1): an mt19937 seeded 1, deterministic (G6, 0x0053A050..0x0053A07E; gap4 R4).
/// </summary>
public sealed class ScanlineDistorter
{
    /// <summary>The distorter's static RNG, seeded with 1 (G6).</summary>
    internal static EngineRandom Rng { get; set; } = new(1u);

    private readonly List<Point> _points;
    private readonly List<(float U, float V)> _noise;

    private struct Point
    {
        public float Pos;
        public int Sign;
        public int Amount;
    }

    private ScanlineDistorter(List<Point> points, List<(float U, float V)> noise)
    {
        _points = points;
        _noise = noise;
    }

    /// <summary>
    /// <c>ScanlineDistorter(maxAmt, noiseProb)</c> (G6, 0x0053A0B0..0x0053A50C): type = RandInt(3) (0..2);
    /// dir = RandDbl(1) &lt; 0.5 ? −1 : +1; control points {pos, sign, amount}, each amount RandIntInRange(1, maxAmt):
    /// type 0 {0, −dir}, {1, +dir}; type 1 {0, −dir}, {U[0.35, 0.65), +dir}, {1, −dir};
    /// type 2 {0, −dir}, {U[0.15, 0.35), +dir}, {U[0.65, 0.85), −dir}, {1, +dir}. With noiseProb &gt; 1e-5,
    /// (u32)(noiseProb·1200) noise points (U[−0.5, 0.5), U[−0.5, 0.5)). Within a point its position is drawn before its
    /// amount (the row does not order the two draws).
    /// </summary>
    public ScanlineDistorter(int maxAmount, float noiseProbability)
    {
        var rng = Rng;
        int type = rng.RandInt(3);
        int dir = rng.RandDbl(1) < 0.5 ? -1 : 1;
        _points = new List<Point>();
        void Add(float pos, int sign) => _points.Add(new Point { Pos = pos, Sign = sign, Amount = rng.RandIntInRange(1, maxAmount) });
        switch (type)
        {
            case 0:
                Add(0f, -dir);
                Add(1f, +dir);
                break;
            case 1:
                Add(0f, -dir);
                Add((float)rng.RandDblInRange(0.35, 0.65), +dir);
                Add(1f, -dir);
                break;
            default:
                Add(0f, -dir);
                Add((float)rng.RandDblInRange(0.15, 0.35), +dir);
                Add((float)rng.RandDblInRange(0.65, 0.85), -dir);
                Add(1f, +dir);
                break;
        }
        _noise = new List<(float U, float V)>();
        if (noiseProbability > 1e-5f)
        {
            uint n = (uint)(noiseProbability * 1200f);
            for (uint i = 0; i < n; i++)
                _noise.Add(((float)rng.RandDblInRange(-0.5, 0.5), (float)rng.RandDblInRange(-0.5, 0.5)));
        }
    }

    /// <summary>A deep copy, as the face's copy constructor makes one.</summary>
    public ScanlineDistorter Clone() => new(new List<Point>(_points), new List<(float U, float V)>(_noise));

    /// <summary>The noise points, for the drawer's AddOffNoise.</summary>
    public IReadOnlyList<(float U, float V)> Noise => _noise;

    /// <summary>The control points as (position, sign, amount), for tests.</summary>
    public IReadOnlyList<(float Pos, int Sign, int Amount)> Points => _points.Select(p => (p.Pos, p.Sign, p.Amount)).ToList();

    /// <summary>
    /// <c>Update(a)</c> (G7, 0x0053A660..0x0053A6A6): each point's amount += RandIntInRange(1, |a|) × (a &lt; 0 ? −sign : sign).
    /// Update(0) still adds ±1.
    /// </summary>
    public void Update(int a)
    {
        var rng = Rng;
        for (int i = 0; i < _points.Count; i++)
        {
            var p = _points[i];
            int r = rng.RandIntInRange(1, Math.Abs(a));
            p.Amount += r * (a < 0 ? -p.Sign : p.Sign);
            _points[i] = p;
        }
    }

    /// <summary>
    /// <c>GetEyeDistortionAmount(f)</c> (G8, 0x0053A6B4..0x0053A784): a single point gives 0; otherwise the segment with
    /// p_i ≤ f &lt; p_{i+1} gives (int)roundf((1 − t)·amt_i + t·amt_{i+1}) + RandIntInRange(−3, 3); no segment gives 0.
    /// </summary>
    public int GetEyeDistortionAmount(float f)
    {
        if (_points.Count <= 1) return 0;
        for (int i = 0; i + 1 < _points.Count; i++)
        {
            var a = _points[i];
            var b = _points[i + 1];
            if (a.Pos <= f && f < b.Pos)
            {
                float t = (f - a.Pos) / (b.Pos - a.Pos);
                int v = (int)MathF.Round((1f - t) * a.Amount + t * b.Amount, MidpointRounding.AwayFromZero);
                return v + Rng.RandIntInRange(-3, 3);
            }
        }
        return 0;
    }

    /// <summary>The distortion table <c>GetNextDistortionFrame</c> steps through (G5, 0x00C530C0): {hold probability, amount}.</summary>
    internal static readonly (float HoldProbability, int Amount)[] DistortionFrames =
    {
        (0f, 1), (0f, 1), (0.75f, 2), (0f, 1), (0f, 4), (0f, 10), (0f, -1), (0f, -9), (0.75f, -5), (0f, 2), (0f, -2),
    };

    private static int _frameIndex;

    /// <summary>
    /// <c>ScanlineDistorter::GetNextDistortionFrame(degree, face, dur)</c> (G5, 0x0053A9A8..0x0053AB1E), a static
    /// iterator over <see cref="DistortionFrames"/>: a = (int)roundf(amount·degree); the first entry calls
    /// face.InitScanlineDistorter(a, 0.1), later ones distorter.Update(a); dur = 66 when the hold probability is above 1e-5
    /// and RandDbl(1) &lt; it, else 33. Past the last entry: RemoveScanlineDistorter, dur = 33, the iterator reset, false.
    /// </summary>
    public static bool GetNextDistortionFrame(float degree, ProceduralFacePose face, out int durationMs)
    {
        if (_frameIndex >= DistortionFrames.Length)
        {
            face.RemoveScanlineDistorter();
            durationMs = 33;
            _frameIndex = 0;
            return false;
        }
        var (hold, amount) = DistortionFrames[_frameIndex];
        int a = (int)MathF.Round(amount * degree, MidpointRounding.AwayFromZero);
        if (_frameIndex == 0) face.InitScanlineDistorter(a, 0.1f);
        else face.Distorter?.Update(a);
        durationMs = hold > 1e-5f && Rng.RandDbl(1) < hold ? 66 : 33;
        _frameIndex++;
        return true;
    }

    /// <summary>Resets the static frame iterator (tests).</summary>
    internal static void ResetFrameIterator() => _frameIndex = 0;

    /// <summary>
    /// <c>ScanlineDistorter::AddOffNoise(M, 40, 30, img)</c> (G11, 0x0053A790..0x0053A898), called by DrawEye when the face
    /// has a distorter: for each noise point (u, v), p = M·(u·30, v·40, 1); xc = clamp((int)roundf(p.x), 0, cols − 1),
    /// yc = clamp((int)roundf(p.y), 0, rows − 1); w = RandIntInRange(1, 3); pixels (yc, xc − (w − (w&amp;1))/2 ..
    /// xc + w/2) are set to 0 (1 px for w = 1, 3 px for 2 or 3), out-of-range columns skipped. No mirroring and no
    /// scan-line offset.
    /// </summary>
    public void AddOffNoise(float[] m, float eyeHeight, float eyeWidth, byte[] img, int rows, int cols)
    {
        foreach (var (u, v) in _noise)
        {
            float lx = u * eyeWidth, ly = v * eyeHeight;
            float px = m[0] * lx + m[1] * ly + m[2];
            float py = m[3] * lx + m[4] * ly + m[5];
            int xc = Math.Clamp((int)MathF.Round(px, MidpointRounding.AwayFromZero), 0, cols - 1);
            int yc = Math.Clamp((int)MathF.Round(py, MidpointRounding.AwayFromZero), 0, rows - 1);
            int w = Rng.RandIntInRange(1, 3);
            for (int x = xc - (w - (w & 1)) / 2; x <= xc + w / 2; x++)
                if ((uint)x < (uint)cols) img[yc * cols + x] = 0;
        }
    }
}
