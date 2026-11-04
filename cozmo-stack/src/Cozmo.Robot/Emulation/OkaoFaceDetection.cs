namespace Cozmo.Robot.Emulation;

/// <summary>One OKAO detection as <c>OKAO_DT_GetRawResultInfo</c> fills it: the raw words, with the fields the engine reads
/// named (FaceTracker::Impl::Update 0x0086D96A..0x0086D998: +0x0C confidence, +0x10/+0x14 centre, +0x1C size, +0x2C the
/// tracking flag compared with 0x100). The meaning of the other words is not read.</summary>
public sealed record OkaoRawFace(int Return, int[] Words)
{
    public int Confidence => Words[3];
    public int CenterX => Words[4];
    public int CenterY => Words[5];
    public int Size => Words[7];
    public int TrackFlag => Words[11];
}

/// <summary>The result of one <c>OKAO_DT_Detect_GRAY</c> call and the reads after it.</summary>
public sealed record OkaoDetectResult(int Return, int CountReturn, IReadOnlyList<OkaoRawFace> Faces);

/// <summary>
/// Omron OKAO face detection, the shipped code statically linked in libcozmoEngine.so, run in the embedded emulator with the
/// settings Anki's FaceTracker::Impl::Init uses (research/20261103-okao-acapela-probes.md; oracle
/// re-analysis/tools/emu/emu_okao.py). Video mode: detections carry state between calls, so frames must be fed in order on one
/// instance. Not thread-safe; one instance per vision thread. After any emulation error the instance is faulted.
/// </summary>
public sealed class OkaoFaceDetection : IDisposable
{
    public const int RawWords = 16;
    private static readonly (string Name, uint[] Args)[] Setup =
    {
        ("OKAO_DT_MV_SetDelayCount", new uint[] { 1 }),            // 0x0086B984
        ("OKAO_DT_MV_SetSearchCycle", new uint[] { 2, 2, 5 }),     // 0x0086BC40..48
        ("OKAO_DT_MV_SetDirectionMask", new uint[] { 0 }),         // 0x0086BD0A..0E
        ("OKAO_DT_MV_SetPoseExtension", new uint[] { 1, 1 }),      // 0x0086BEA2..A8
        ("OKAO_DT_MV_SetAccuracy", new uint[] { 0 }),              // 0x0086BF6A..6E
        ("OKAO_DT_SetAngle", new uint[] { 1, 0x803803 }),          // 0x0086B77E..88
        ("OKAO_DT_SetSizeRange", new uint[] { 48, 640 }),          // 0x0086B7F0 / 0x0086B7F4
        ("OKAO_DT_SetThreshold", new uint[] { 500 }),              // 0x0086B9F2
    };

    private readonly ArmMachine _m;
    private readonly uint _co, _dt, _res, _detect, _count, _info;
    private uint _buf, _cnt, _infoBuf;
    private int _w, _h;
    private bool _faulted;

    public OkaoFaceDetection(string libDir, byte poison = 0x00)
    {
        _m = new ArmMachine(libDir, new[] { "libc++_shared.so", "libcozmoEngine.so" }, poison);
        try
        {
            _m.Relocate();
            _co = _m.Call(_m.Symbol("OKAO_CO_CreateHandle"));                         // 0x0086B598
            _dt = _m.Call(_m.Symbol("OKAO_DT_CreateHandle"), new uint[] { _co, 5, 10 }); // video mode, 0x0086B96E
            if (_co == 0 || _dt == 0) throw new EmulationAbort("OKAO handle creation returned null");
            foreach (var (name, args) in Setup)
            {
                var a = new uint[args.Length + 1];
                a[0] = _dt;
                args.CopyTo(a, 1);
                int r = unchecked((int)_m.Call(_m.Symbol(name), a));
                if (r != 0) throw new EmulationAbort($"{name} returned {r}");
            }
            _res = _m.Call(_m.Symbol("OKAO_DT_CreateResultHandle"), new[] { _co });
            _detect = _m.Symbol("OKAO_DT_Detect_GRAY");
            _count = _m.Symbol("OKAO_DT_GetResultCount");
            _info = _m.Symbol("OKAO_DT_GetRawResultInfo");
        }
        catch
        {
            _m.Dispose();
            throw;
        }
    }

    /// <summary>Runs detection on a grey image (row-major, <paramref name="width"/> × <paramref name="height"/> bytes).</summary>
    public OkaoDetectResult Detect(ReadOnlySpan<byte> gray, int width, int height)
    {
        if (_faulted) throw new InvalidOperationException("OKAO detector is faulted after an emulation error; create a new one");
        if (gray.Length != width * height) throw new ArgumentException("image size does not match width × height");
        try
        {
            if (_buf == 0 || width != _w || height != _h)
            {
                if (_buf != 0) _m.Free(_buf);
                _buf = _m.Malloc((ulong)(width * height));
                _w = width; _h = height;
                if (_cnt == 0) { _cnt = _m.Malloc(16); _infoBuf = _m.Malloc(4 * RawWords); }
            }
            _m.Write(_buf, gray);
            int ret = unchecked((int)_m.Call(_detect, new[] { _dt, _buf, (uint)width, (uint)height }, new uint[] { 0, _res }));
            _m.Fill(_cnt, 16, 0);
            int countRet = unchecked((int)_m.Call(_count, new[] { _res, _cnt }));
            uint n = _m.R32(_cnt);
            var faces = new List<OkaoRawFace>((int)Math.Min(n, 64));
            for (uint i = 0; i < n; i++)
            {
                _m.Fill(_infoBuf, 4 * RawWords, 0);
                int ri = unchecked((int)_m.Call(_info, new[] { _res, i, _infoBuf }));
                var words = new int[RawWords];
                for (int k = 0; k < RawWords; k++) words[k] = unchecked((int)_m.R32(_infoBuf + 4u * (uint)k));
                faces.Add(new OkaoRawFace(ri, words));
            }
            return new OkaoDetectResult(ret, countRet, faces);
        }
        catch
        {
            _faulted = true;
            throw;
        }
    }

    public void Dispose() => _m.Dispose();
}
