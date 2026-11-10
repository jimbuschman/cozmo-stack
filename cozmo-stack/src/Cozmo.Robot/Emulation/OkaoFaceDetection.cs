namespace Cozmo.Robot.Emulation;

using Cozmo.Robot.Animation;
using Cozmo.Robot.Vision;

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
public sealed partial class OkaoFaceDetection : IDisposable
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
    private uint _pt, _ptResult, _scratch;
    private readonly EngineRandom _faceOrder = new(1);
    internal Action<string>? Diagnostic { get; set; }

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

    /// <summary>Production projection of the source FaceTracker's DT and PT results.</summary>
    internal IReadOnlyList<DetectedFace> DetectTracked(ReadOnlySpan<byte> gray, int width, int height,
        Func<int[], DetectedFace, bool, bool, DetectedFace>? recognize = null,
        Func<int, bool>? knownTrack = null, int enrollmentTrack = 0, int attributeMask = 0)
    {
        if (_faulted) throw new InvalidOperationException("OKAO detector is faulted after an emulation error; create a new one");
        if (gray.Length != width * height) throw new ArgumentException("image size does not match width × height");
        try
        {
            if (_pt == 0)
            {
                _pt = _m.Call(_m.Symbol("OKAO_PT_CreateHandle"), new[] { _co });
                if (_pt == 0) throw new EmulationAbort("OKAO_PT_CreateHandle returned null");
                int mode = unchecked((int)_m.Call(_m.Symbol("OKAO_PT_SetConfMode"), new[] { _pt, 1u }));
                if (mode != 0) throw new EmulationAbort($"OKAO_PT_SetConfMode returned {mode}");
                _ptResult = _m.Call(_m.Symbol("OKAO_PT_CreateResultHandle"), new[] { _co });
                if (_ptResult == 0) throw new EmulationAbort("OKAO_PT_CreateResultHandle returned null");
                _scratch = _m.Malloc(256);
                if (_scratch == 0) throw new EmulationAbort("OKAO part output allocation failed");
            }
            if ((attributeMask & 0x3C00) != 0) EnsureAttributeHandles();
            if (recognize is not null && _frPtResult == 0)
            {
                _frPtResult = _m.Call(_m.Symbol("OKAO_PT_CreateResultHandle"), new[] { _co });
                if (_frPtResult == 0) throw new EmulationAbort("OKAO recognition part result allocation failed");
            }
            if (_buf == 0 || width != _w || height != _h)
            {
                if (_buf != 0) _m.Free(_buf);
                _buf = _m.Malloc((ulong)(width * height));
                _w = width; _h = height;
                if (_cnt == 0) { _cnt = _m.Malloc(16); _infoBuf = _m.Malloc(4 * RawWords); }
            }
            _m.Write(_buf, gray);
            int result = unchecked((int)_m.Call(_detect, new[] { _dt, _buf, (uint)width, (uint)height }, new uint[] { 0, _res }));
            if (result != 0) { Diagnostic?.Invoke($"M14-010 OKAO_DT_Detect_GRAY returned {result}"); return Array.Empty<DetectedFace>(); }
            _m.W32(_cnt, 0);
            result = unchecked((int)_m.Call(_count, new[] { _res, _cnt }));
            if (result != 0) { Diagnostic?.Invoke($"M14-010 OKAO_DT_GetResultCount returned {result}"); return Array.Empty<DetectedFace>(); }
            uint count = _m.R32(_cnt);
            var order = Enumerable.Range(0, checked((int)count)).ToArray();
            var excluded = new HashSet<int>();
            if (order.Length >= 2)
            {
                foreach (int index in order)
                {
                    var raw = ReadRaw((uint)index);
                    if (raw is null) return Array.Empty<DetectedFace>();
                    if (raw[2] != enrollmentTrack && knownTrack?.Invoke(raw[2]) == true) excluded.Add(raw[2]);
                }
                if (excluded.Count == order.Length) excluded.Clear();
                ShuffleIndices(order, _faceOrder);
            }
            var faces = new List<DetectedFace>(order.Length);
            foreach (int index in order)
            {
                int[]? raw = ReadRaw((uint)index);
                if (raw is null) break;
                _m.Fill(_scratch, 256, 0);
                result = unchecked((int)_m.Call(_m.Symbol("OKAO_CO_ConvertCenterToSquare"),
                    new[] { unchecked((uint)raw[4]), unchecked((uint)raw[5]), unchecked((uint)raw[7]), 0u },
                    new[] { _scratch, _scratch + 8, _scratch + 16, _scratch + 24 }));
                if (result != 0)
                {
                    Diagnostic?.Invoke($"MISSING M14-010 CO_ConvertCenterToSquare returned {result}; native retains the partially constructed face");
                    break;
                }
                int x = unchecked((int)_m.R32(_scratch)), y = unchecked((int)_m.R32(_scratch + 4));
                int right = unchecked((int)_m.R32(_scratch + 24)), bottom = unchecked((int)_m.R32(_scratch + 28));
                Vec2? leftEye = null, rightEye = null;
                double? roll = null;
                int? third = null;
                bool facing = false;
                NativeFaceFeatures? features = null;
                bool partsReady = false;
                result = unchecked((int)_m.Call(_m.Symbol("OKAO_PT_SetPositionFromHandle"), new[] { _pt, _res, (uint)index }));
                if (result == 0)
                {
                    result = unchecked((int)_m.Call(_m.Symbol("OKAO_PT_DetectPoint_GRAY"),
                        new[] { _pt, _buf, (uint)width, (uint)height }, new uint[] { 0, _ptResult }));
                    if (result == 0)
                    {
                        _m.Fill(_scratch, 256, 0);
                        result = unchecked((int)_m.Call(_m.Symbol("OKAO_PT_GetResult"), new[] { _ptResult, 12u, _scratch, _scratch + 96 }));
                        if (result == 0)
                        {
                            partsReady = true;
                            var points = new (int X,int Y)[12];
                            for (int i=0;i<points.Length;i++) points[i]=(unchecked((int)_m.R32(_scratch+(uint)(i*8))),unchecked((int)_m.R32(_scratch+(uint)(i*8+4))));
                            features = NativeFaceFeatures.FromPoints(points);
                            leftEye = new Vec2(unchecked((int)_m.R32(_scratch)), unchecked((int)_m.R32(_scratch + 4)));
                            rightEye = new Vec2(unchecked((int)_m.R32(_scratch + 8)), unchecked((int)_m.R32(_scratch + 12)));
                            result = unchecked((int)_m.Call(_m.Symbol("OKAO_PT_GetFaceDirection"), new[] { _ptResult, _scratch + 144, _scratch + 148, _scratch + 152 }));
                            if (result == 0)
                            {
                                float radians = unchecked((int)_m.R32(_scratch + 148)) * BitConverter.UInt32BitsToSingle(0x3C8EFA35);
                                roll = new Radians(radians).Value;
                                third = unchecked((int)_m.R32(_scratch + 152));
                                int d0 = unchecked((int)_m.R32(_scratch + 144));
                                int d1 = unchecked((int)_m.R32(_scratch + 148));
                                int d2 = third.Value;
                                facing = Math.Abs((long)d0) <= 25 && Math.Abs((long)d1) <= 25 && Math.Abs((long)d2) <= 25;
                            }
                            else roll = 0;
                        }
                    }
                }
                if (!partsReady) features = NativeFaceFeatures.Empty; // Defined PT return failures leave the fresh vectors empty.
                if (result != 0 && result != -8) Diagnostic?.Invoke($"M14-010 OKAO parts returned {result}");
                var attributes = partsReady && (attributeMask & 0x3C00) != 0
                    ? ReadAttributes(attributeMask, width, height) : NativeFaceAttributes.Default;
                var face = new DetectedFace(unchecked(-raw[2]), new FaceRect(x, y, right - x, bottom - y), leftEye, rightEye,
                    ExpressionValues: (attributeMask & (1 << 10)) != 0 ? attributes.Scores : null, Score: raw[3], RollRad: roll) { NativeFaceDirectionThirdDegrees = third, NativeTrackerFlagChanged = raw[11] != 0x100, NativeFeatures = features, NativeAttributes = attributes };
                bool enrollable = raw[3] > 500 && raw[9] == 0 && facing && leftEye is { } le && rightEye is { } re && roll is { } angle &&
                    TrackedFace.GetIntraEyeDistance(le, re, angle) >= 16;
                faces.Add(recognize?.Invoke(raw, face, enrollable, excluded.Contains(raw[2])) ?? face);
            }
            return faces;
        }
        catch { _faulted = true; throw; }
    }

    private int[]? ReadRaw(uint index)
    {
        _m.Fill(_infoBuf, 4 * RawWords, 0);
        int result = unchecked((int)_m.Call(_info, new[] { _res, index, _infoBuf }));
        if (result != 0) { Diagnostic?.Invoke($"M14-010 OKAO_DT_GetRawResultInfo returned {result}"); return null; }
        var raw = new int[RawWords];
        for (int i = 0; i < raw.Length; i++) raw[i] = unchecked((int)_m.R32(_infoBuf + (uint)(4 * i)));
        return raw;
    }

    internal static void ShuffleIndices(int[] indices, EngineRandom random)
    {
        for (int i = 0; i + 1 < indices.Length; i++)
        {
            int j = i + random.RandInt(indices.Length - i);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }
    }

    internal void ResetTracking()
    {
        if (_faulted) throw new InvalidOperationException("OKAO detector is faulted");
        try
        {
            int result = unchecked((int)_m.Call(_m.Symbol("OKAO_DT_MV_ResetTracking"), new[] { _dt }));
            if (result != 0) Diagnostic?.Invoke($"M14-010 OKAO_DT_MV_ResetTracking returned {result}");
        }
        catch { _faulted = true; throw; }
    }

    public void Dispose()
    {
        try
        {
            if (!_faulted)
            {
                if (_frFeature != 0) FrCall("OKAO_FR_DeleteFeatureHandle", _frFeature);
                if (_frScratchFeature != 0) FrCall("OKAO_FR_DeleteFeatureHandle", _frScratchFeature);
                if (_frPtResult != 0) FrCall("OKAO_PT_DeleteResultHandle", _frPtResult);
                DeleteAttributeHandles();
                if (_ptResult != 0) FrCall("OKAO_PT_DeleteResultHandle", _ptResult);
            }
        }
        finally { _m.Dispose(); }
    }
}
