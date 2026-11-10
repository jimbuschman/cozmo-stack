using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Emulation;

public sealed partial class OkaoFaceDetection
{
    private uint _frScratchFeature;
    private uint _frFeature, _frPtResult, _frImage;
    private int _frWidth, _frHeight;
    internal OkaoFaceFeature FrPendingFeature => new(this, _frFeature);
    internal void FrQueueCurrentImage()
    {
        if (_frImage != 0) throw new InvalidOperationException("recognition slot already owns an image");
        if (_frFeature == 0) _frFeature = _m.Call(_m.Symbol("OKAO_FR_CreateFeatureHandle"), new[] { _co });
        if (_frPtResult == 0) _frPtResult = _m.Call(_m.Symbol("OKAO_PT_CreateResultHandle"), new[] { _co });
        if (_frFeature == 0 || _frPtResult == 0) throw new EmulationAbort("OKAO recognition handle allocation failed");
        uint size = checked((uint)(_w * _h));
        _frImage = _m.Malloc(size);
        if (_frImage == 0) throw new EmulationAbort("OKAO recognition image allocation failed");
        _m.Copy(_frImage, _buf, size);
        (_ptResult, _frPtResult) = (_frPtResult, _ptResult);
        _frWidth = _w; _frHeight = _h;
    }
    internal int FrExtractQueued()
    {
        if (_frImage == 0) throw new InvalidOperationException("recognition slot has no image");
        try
        {
            return unchecked((int)_m.Call(_m.Symbol("OKAO_FR_ExtractHandle_GRAY"),
                new[] { _frFeature, _frImage, (uint)_frWidth, (uint)_frHeight }, new[] { 0u, _frPtResult }));
        }
        catch { _faulted = true; throw; }
        finally { _m.Free(_frImage); _frImage = 0; }
    }
    internal uint FrCommon => _co;
    internal int FrCall(string name, params uint[] args) => unchecked((int)_m.Call(_m.Symbol(name), args));
    internal uint FrCreateAlbum()
    {
        if(_stockEager)
        {
            if(_frFeature==0)_frFeature=StockCreate("OKAO_FR_CreateFeatureHandle","FaceRecognizer.Init.FaceLibFeatureHandleAllocFail",_co);
            if(_frScratchFeature==0)_frScratchFeature=StockCreate("OKAO_FR_CreateFeatureHandle","FaceRecognizer.Init.FaceLibMergeFeatureHandleAllocFail",_co);
            return StockCreate("OKAO_FR_CreateAlbumHandle","FaceRecognizer.Init.FaceLibAlbumHandleAllocFail",_co,1000,4);
        }
        if (_frFeature == 0) _frFeature = _m.Call(_m.Symbol("OKAO_FR_CreateFeatureHandle"), new[] { _co });
        if (_frScratchFeature == 0) _frScratchFeature = _m.Call(_m.Symbol("OKAO_FR_CreateFeatureHandle"), new[] { _co });
        if (_frFeature == 0 || _frScratchFeature == 0) throw new EmulationAbort("OKAO FR feature allocation failed");
        uint a = _m.Call(_m.Symbol("OKAO_FR_CreateAlbumHandle"), new[] { _co, 1000u, 4u });
        return a != 0 ? a : throw new EmulationAbort("OKAO_FR_CreateAlbumHandle returned null");
    }
    internal (int Result, int Value) FrOneOut(string name, uint[] args)
    {
        uint p = _m.Malloc(4);
        if (p == 0) throw new EmulationAbort("OKAO FR output allocation failed");
        try
        {
            _m.W32(p, 0);
            int result = FrCall(name, args.Append(p).ToArray());
            return (result, unchecked((int)_m.R32(p)));
        }
        finally { _m.Free(p); }
    }
    internal (int Result, int First, int Second) FrTwoOut(string name, uint album)
    {
        uint p = _m.Malloc(8);
        if (p == 0) throw new EmulationAbort("OKAO FR output allocation failed");
        try
        {
            _m.Fill(p, 8, 0);
            int result = FrCall(name, album, p, p + 4);
            return (result, unchecked((int)_m.R32(p)), unchecked((int)_m.R32(p + 4)));
        }
        finally { _m.Free(p); }
    }
    internal (int Result, int Count) FrIdentify(uint feature, uint album, int max, int[] ids, int[] scores)
    {
        if (max < 0 || max > 10 || ids.Length < max || scores.Length < max) throw new ArgumentOutOfRangeException(nameof(max));
        uint p = _m.Malloc(84);
        if (p == 0) throw new EmulationAbort("OKAO FR identify allocation failed");
        try
        {
            _m.Fill(p, 84, 0);
            int result = unchecked((int)_m.Call(_m.Symbol("OKAO_FR_Identify"),
                new[] { feature, album, (uint)max, p }, new[] { p + 40, p + 80 }));
            int count = unchecked((int)_m.R32(p + 80));
            if (result == 0 && count >= 0)
                for (int i = 0; i < Math.Min(count, max); i++)
                {
                    ids[i] = unchecked((int)_m.R32(p + (uint)(i * 4)));
                    scores[i] = unchecked((int)_m.R32(p + 40 + (uint)(i * 4)));
                }
            return (result, count);
        }
        finally { _m.Free(p); }
    }
    internal (int Result, OkaoFaceFeature Feature) FrGetFeature(uint album, int entry, int index)
    {
        if (_frScratchFeature == 0)
        {
            _frScratchFeature = _m.Call(_m.Symbol("OKAO_FR_CreateFeatureHandle"), new[] { _co });
            if (_frScratchFeature == 0) throw new EmulationAbort("OKAO FR scratch feature allocation failed");
        }
        int result = FrCall("OKAO_FR_GetFeatureFromAlbum", album, unchecked((uint)entry), unchecked((uint)index), _frScratchFeature);
        return (result, new OkaoFaceFeature(this, _frScratchFeature));
    }
    internal (int Status, byte[] Data) FrSerializeAlbum(uint album)
    {
        var size = FrOneOut("OKAO_FR_GetSerializedAlbumSize", new[] { album });
        if (size.Result != 0) return (size.Result, Array.Empty<byte>());
        if (size.Value <= 0) return (1, Array.Empty<byte>());
        uint bytes = _m.Malloc((uint)size.Value);
        if (bytes == 0) throw new EmulationAbort("OKAO album serialization allocation failed");
        try
        {
            int result = FrCall("OKAO_FR_SerializeAlbum", album, bytes, (uint)size.Value);
            return result == 0 ? (0, _m.Read(bytes, (uint)size.Value)) : (result, Array.Empty<byte>());
        }
        finally { _m.Free(bytes); }
    }
    internal (OkaoFaceDetection Owner, uint Handle, int Result) FrRestore(uint common, byte[] data, int size)
    {
        if (size < 0 || size > data.Length) throw new ArgumentOutOfRangeException(nameof(size));
        uint bytes = _m.Malloc((uint)Math.Max(size, 1));
        uint status = _m.Malloc(4);
        if (bytes == 0 || status == 0) throw new EmulationAbort("OKAO FR restore allocation failed");
        try
        {
            _m.Write(bytes, data.AsSpan(0, size));
            _m.W32(status, 0);
            uint handle = _m.Call(_m.Symbol("OKAO_FR_RestoreAlbum"), new[] { common, bytes, (uint)size, status });
            return (this, handle, unchecked((int)_m.R32(status)));
        }
        finally
        {
            _m.Free(bytes);
            _m.Free(status);
        }
    }
}
