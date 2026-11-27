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

internal sealed class FaceGuestFailure(string key,uint result,string detail = "") : Exception(key)
{
    internal string Key { get; }=key;
    internal string Detail { get; }=detail;
    internal uint Result { get; }=result;
}


/// <summary>
/// Omron OKAO face detection, the shipped code statically linked in libcozmoEngine.so, run in the embedded emulator with the
/// settings Anki's FaceTracker::Impl::Init uses (research/20261103-okao-acapela-probes.md; oracle
/// re-analysis/tools/emu/emu_okao.py). Video mode: detections carry state between calls, so frames must be fed in order on one
/// instance. Not thread-safe; one instance per vision thread. After any emulation error the instance is faulted.
/// </summary>
public sealed partial class OkaoFaceDetection : IDisposable
{
    public const int RawWords = 16;
    private static readonly (string Name, string FailureSuffix, uint[] Args)[] Setup =
    {
        ("OKAO_DT_MV_SetDelayCount", "DelayCount", new uint[] { 1 }),            // 0x0086B984
        ("OKAO_DT_MV_SetSearchCycle", "SearchCycle", new uint[] { 2, 2, 5 }),     // 0x0086BC40..48
        ("OKAO_DT_MV_SetDirectionMask", "DirectionMask", new uint[] { 0 }),         // 0x0086BD0A..0E
        ("OKAO_DT_MV_SetPoseExtension", "PoseExtension", new uint[] { 1, 1 }),      // 0x0086BEA2..A8
        ("OKAO_DT_MV_SetAccuracy", "Accuracy", new uint[] { 0 }),              // 0x0086BF6A..6E
        ("OKAO_DT_SetAngle", "Angle", new uint[] { 1, 0x803803 }),          // 0x0086B77E..88
        ("OKAO_DT_SetSizeRange", "Size\u0012Range", new uint[] { 48, 640 }),          // 0x0086B7F0 / 0x0086B7F4
        ("OKAO_DT_SetThreshold", "Threshold", new uint[] { 500 }),              // 0x0086B9F2
    };

    private readonly ArmMachine _m;
    private uint _co, _dt, _res, _detect, _count, _info;
    private uint _buf, _cnt, _infoBuf;
    private int _w, _h;
    private bool _faulted;
    private bool _stockEager;
    internal Func<string,uint?>? StockCreateOverrideForTest { get; set; }
    internal Func<string,int?>? StockDeleteOverrideForTest { get; set; }
    internal List<string> StockCleanupFailures { get; }=new();
    private uint _pt, _ptResult, _scratch;
    private readonly EngineRandom _faceOrder = new(1);
    internal Action<string>? Diagnostic { get; set; }
    internal Action<string,string>? SourceWarning { get; set; }

    public OkaoFaceDetection(string libDir, byte poison = 0x00)
        : this(libDir,poison,false)
    {
    }

    internal OkaoFaceDetection(string libDir, byte poison, bool staged)
    {
        _m = new ArmMachine(libDir, new[] { "libc++_shared.so", "libcozmoEngine.so" }, poison);
        try
        {
            _m.Relocate();
            _detect = _m.Symbol("OKAO_DT_Detect_GRAY");
            _count = _m.Symbol("OKAO_DT_GetResultCount");
            _info = _m.Symbol("OKAO_DT_GetRawResultInfo");
            if(staged){_stockEager=true;return;}
            _co = _m.Call(_m.Symbol("OKAO_CO_CreateHandle"));                         // 0x0086B598
            _dt = _m.Call(_m.Symbol("OKAO_DT_CreateHandle"), new uint[] { _co, 5, 10 }); // video mode, 0x0086B96E
            if (_co == 0 || _dt == 0) throw new EmulationAbort("OKAO handle creation returned null");
            foreach (var (name, _, args) in Setup)
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

    internal (byte Major,byte Minor) StockVersion()
    {
        uint version=_m.Malloc(2);
        if(version==0)throw new EmulationAbort("OKAO version output allocation failed");
        byte major,minor;
        try
        {
            _m.Fill(version,2,0);
            int rc=unchecked((int)_m.Call(_m.Symbol("OKAO_CO_GetVersion"),new[]{version,version+1}));
            if(rc!=0)throw new FaceGuestFailure("FaceTrackerImpl.Init.FaceLibVersionFail",1);
            major=(byte)_m.R8(version);minor=(byte)_m.R8(version+1);
        }
        finally { _m.Free(version); }
        return (major,minor);
    }

    internal void InitializeStockHandles()
    {
        _co=StockCreate("OKAO_CO_CreateHandle","FaceTrackerImpl.Init.FaceLibCommonHandleNull");
        _dt=StockCreate("OKAO_DT_CreateHandle","FaceTrackerImpl.Init.FaceLibDetectionHandleAllocFail.VideoMode",_co,5,10);
        foreach(var (name,failureSuffix,args) in Setup)
        {
            var input=new uint[args.Length+1];input[0]=_dt;args.CopyTo(input,1);
            int rc=unchecked((int)_m.Call(_m.Symbol(name),input));
            if(rc!=0)throw new FaceGuestFailure($"FaceTrackerImpl.Init.FaceLibSet{failureSuffix}Failed",0x03000000,
                name == "OKAO_DT_SetThreshold" ? $"FaceLib Result Code={rc}" : "");
        }
        _res=StockCreate("OKAO_DT_CreateResultHandle","FacetrackerImpl.Init.FaceLibDetectionResultHandleAllocFail",_co);
        _pt=StockCreate("OKAO_PT_CreateHandle","FacetrackerImpl.Init.FaceLibPartDetectorHandleAllocFail",_co);
        int mode=unchecked((int)_m.Call(_m.Symbol("OKAO_PT_SetConfMode"),new[]{_pt,1u}));
        if(mode!=0)throw new FaceGuestFailure("FacetrakerImpl.Init.FaceLibPartDetectorConfModeFail",0x03000000);
        _ptResult=StockCreate("OKAO_PT_CreateResultHandle","FacetrackerImpl.Init.FaceLibPartDetectionResultHandleAllocFail",_co);
        _frPtResult=StockCreate("OKAO_PT_CreateResultHandle","FacetrackerImpl.Init.FaceLibPartDetectionResultHandle2AllocFail",_co);
        InitializeStockAttributes();
    }

    internal uint StockCreate(string name,string failure,params uint[] args)
    {
        uint value=StockCreateOverrideForTest?.Invoke(name) ?? _m.Call(_m.Symbol(name),args);
        if(value==0)throw new FaceGuestFailure(failure,0x01000000);
        return value;
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
                var partialFace = ReadFaceRectangle(raw, index, count, out var rect);
                if (partialFace is not null)
                {
                    faces.Add(partialFace);
                    break;
                }
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
                var face = new DetectedFace(unchecked(-raw[2]), rect, leftEye, rightEye,
                    ExpressionValues: (attributeMask & (1 << 10)) != 0 ? attributes.Scores : null, Score: raw[3], RollRad: roll) { NativeFaceDirectionThirdDegrees = third, NativeTrackerFlagChanged = raw[11] != 0x100, NativeFeatures = features, NativeAttributes = attributes, NativeFacingCamera = facing, NativeNameBytes = Array.Empty<byte>() };
                bool enrollable = IsEnrollableDefaultPose(raw[3], raw[9], facing,
                    leftEye is { } le && rightEye is { } re && roll is { } angle ? TrackedFace.GetIntraEyeDistance(le, re, angle) : null);
                faces.Add(recognize?.Invoke(raw, face, enrollable, excluded.Contains(raw[2])) ?? face);
            }
            return faces;
        }
        catch { _faulted = true; throw; }
    }

    // fidelity: M14-011
    /// <summary>
    /// <c>FaceTracker::Impl::IsEnrollable(DETECTION_INFO, TrackedFace)</c> 0x0086E074 for enrollment pose 0, the only pose production
    /// sets. The pose byte is <c>Impl+0x134</c>: the ctor zeroes it (0x0086B2A8) and only <c>Impl::SetFaceEnrollmentMode</c> 0x0086E372
    /// (<c>strb.w r1,[r0,#0x134]</c>) writes it, reached only from <c>VisionComponent::SetFaceEnrollmentMode</c> 0x00657A58, whose only
    /// callers are the two <c>FaceWorld::Enroll</c> overloads, which pass pose 0 (<c>movs r1,#0</c> at 0x004F5CAC and 0x004F5CCE). The
    /// pose 1..6 cases of the switch (0x0086E0CE..0x0086E1D2) are therefore unreachable and not built.
    /// Pose 0: the confidence <c>DETECTION_INFO+0xC</c> must be over 500 (<c>cmp.w r0,#0x1f4; ble</c> 0x0086E080), then (0x0086E0A8..0x0086E0CC)
    /// <c>DETECTION_INFO+0x24</c> must be 0, the intra-eye distance (<c>GetIntraEyeDistance</c> 0x004CF974) at least 16.0 (<c>vcmpe; movge</c>)
    /// and the TrackedFace byte <c>+0x11</c> (facing) set. <paramref name="eyeDistance"/> null (no eye points) can only occur with facing
    /// clear, which already fails the gate.
    /// </summary>
    internal static bool IsEnrollableDefaultPose(int confidence, int detectionWord24, bool facing, double? eyeDistance) =>
        confidence > 500 && detectionWord24 == 0 && eyeDistance is { } distance && distance >= 16 && facing;

    internal DetectedFace? ReadFaceRectangle(int[] raw, int index, uint count, out FaceRect rect)
    {
        if (_scratch == 0) _scratch = _m.Malloc(256);
        if (_scratch == 0) throw new EmulationAbort("OKAO part output allocation failed");
        _m.Fill(_scratch, 256, 0);
        int rc = unchecked((int)_m.Call(_m.Symbol("OKAO_CO_ConvertCenterToSquare"),
            new[] { unchecked((uint)raw[4]), unchecked((uint)raw[5]), unchecked((uint)raw[7]), 0u },
            new[] { _scratch, _scratch + 8, _scratch + 16, _scratch + 24 }));
        if (rc == 0)
        {
            int x = unchecked((int)_m.R32(_scratch)), y = unchecked((int)_m.R32(_scratch + 4));
            int right = unchecked((int)_m.R32(_scratch + 24)), bottom = unchecked((int)_m.R32(_scratch + 28));
            rect = new(x, y, right - x, bottom - y);
            return null;
        }
        // 0x86D964..0x86D99E appended the default face and wrote its tracking flag.
        // 0x86DDEA skips the rectangle and timestamp writes and ends this update.
        rect = default;
        SourceWarning?.Invoke("FaceTrackerImpl.Update.FaceLibCenterToSquareFail",
            $"Detection index {index} of {count}. FaceLib Result Code={rc}");
        return new DetectedFace(0, rect) {
            NativeSquareConversionFailed = true, NativeTrackerFlagChanged = raw[11] != 0x100,
            NativeFeatures = NativeFaceFeatures.Empty, NativeAttributes = NativeFaceAttributes.Default,
            NativeNameBytes = Array.Empty<byte>()
        };
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
                if (_stockEager)
                {
                    void Delete(string name,uint handle,string key)
                    { if(handle!=0 && (StockDeleteOverrideForTest?.Invoke(name) ?? FrCall(name,handle))!=0)StockCleanupFailures.Add(key); }
                    Delete("OKAO_FR_DeleteFeatureHandle",_frScratchFeature,"FaceRecognizer.Shutdown.FaceLibRecognitionMergeFeatureHandleDeleteFail");
                    Delete("OKAO_FR_DeleteFeatureHandle",_frFeature,"FaceRecognizer.Shutdown.FaceLibRecognitionFeatureHandleDeleteFail");
                    DeleteStockAttributes(Delete);
                    Delete("OKAO_PT_DeleteResultHandle",_ptResult,"FaceTrackerImpl.Destructor.FaceLibPartDetectionResultHandle1DeleteFail");
                    Delete("OKAO_PT_DeleteResultHandle",_frPtResult,"FaceTrackerImpl.Destructor.FaceLibPartDetectionResultHandle2DeleteFail");
                    Delete("OKAO_PT_DeleteHandle",_pt,"FaceTrackerImpl.Destructor.FaceLibPartDetectorHandleDeleteFail");
                    Delete("OKAO_DT_DeleteResultHandle",_res,"FaceTrackerImpl.Destructor.FaceLibDetectionResultHandleDeleteFail");
                    Delete("OKAO_DT_DeleteHandle",_dt,"FaceTrackerImpl.Destructor.FaceLibDetectorHandleDeleteFail");
                    Delete("OKAO_CO_DeleteHandle",_co,"FaceTrackerImpl.Destructor.FaceLibCommonHandleDeleteFail");
                }
                else
                {
                    if (_frFeature != 0) FrCall("OKAO_FR_DeleteFeatureHandle", _frFeature);
                    if (_frScratchFeature != 0) FrCall("OKAO_FR_DeleteFeatureHandle", _frScratchFeature);
                    if (_frPtResult != 0) FrCall("OKAO_PT_DeleteResultHandle", _frPtResult);
                    DeleteAttributeHandles();
                    if (_ptResult != 0) FrCall("OKAO_PT_DeleteResultHandle", _ptResult);
                }
            }
        }
        finally { _m.Dispose(); }
    }
}
