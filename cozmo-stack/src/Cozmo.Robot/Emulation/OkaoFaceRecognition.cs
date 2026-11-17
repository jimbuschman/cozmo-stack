using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Emulation;

public sealed partial class OkaoFaceDetection
{
    private uint _frScratchFeature;
    private uint _frFeature, _frPtResult, _frImage;
    private int _frWidth, _frHeight;

    // FR_ExtractHandle_GRAY (0x0092C918..0x0092C92C) reads the PT
    // validity word and twelve points/confidences via PT_GetResult.
    private const uint PartResultDataOffset = 0x0C;
    private const uint PartResultDataLength = 0x90; // 0x00922C08..0x00922C64
    private const uint FeatureBytes = 0xB0; // FR_WriteFeatureToMemory 0x0092CF0C..0x0092CF5C
    internal sealed record RecognitionFrame(byte[] Pixels, int Width, int Height, uint PartsValid, byte[] Parts);
    internal sealed record RecognitionExtraction(int Result, byte[]? Feature);

    internal RecognitionFrame FrCaptureCurrentImage()
    {
        // 0x00865A50..0x00865A78 snapshots the input. After admission,
        // 0x0086DBAA..0x0086DBB2 swaps the part handle used by detection.
        var frame = new RecognitionFrame(_m.Read(_buf, checked((uint)(_w * _h))), _w, _h,
            _m.R32(_ptResult + 4), _m.Read(_ptResult + PartResultDataOffset, PartResultDataLength));
        if (_frPtResult == 0) _frPtResult = _m.Call(_m.Symbol("OKAO_PT_CreateResultHandle"), new[] { _co });
        if (_frPtResult == 0) throw new EmulationAbort("OKAO recognition part-result allocation failed");
        (_ptResult, _frPtResult) = (_frPtResult, _ptResult);
        return frame;
    }

    internal static OkaoFaceDetection CreateRecognitionExtractor(string libDir)
    {
        var worker = new OkaoFaceDetection(libDir, poison: 0, staged: true);
        try
        {
            worker._co = worker._m.Call(worker._m.Symbol("OKAO_CO_CreateHandle"));
            if (worker._co == 0) throw new EmulationAbort("OKAO recognition common-handle allocation failed");
            worker._frFeature = worker._m.Call(worker._m.Symbol("OKAO_FR_CreateFeatureHandle"), new[] { worker._co });
            worker._frPtResult = worker._m.Call(worker._m.Symbol("OKAO_PT_CreateResultHandle"), new[] { worker._co });
            if (worker._frFeature == 0 || worker._frPtResult == 0)
                throw new EmulationAbort("OKAO recognition worker handle allocation failed");
            return worker;
        }
        catch
        {
            worker.Dispose();
            throw;
        }
    }

    internal RecognitionExtraction FrExtractFrame(RecognitionFrame frame)
    {
        // The receive-side PT object owns its own magic and common pointer.
        // Copy only the fields the native extractor consumes, never guest pointers.
        _m.W32(_frPtResult + 4, frame.PartsValid);
        _m.Write(_frPtResult + PartResultDataOffset, frame.Parts);
        uint image = _m.Malloc((uint)frame.Pixels.Length);
        if (image == 0) throw new EmulationAbort("OKAO recognition image allocation failed");
        try
        {
            _m.Write(image, frame.Pixels);
            int result = FrExtract(image, frame.Width, frame.Height, _frPtResult);
            if (result != 0) return new RecognitionExtraction(result, null);
        }
        catch { _faulted = true; throw; }
        finally { _m.Free(image); }

        uint output = _m.Malloc(FeatureBytes);
        if (output == 0) throw new EmulationAbort("OKAO feature transfer allocation failed");
        try
        {
            int result = FrCall("OKAO_FR_WriteFeatureToMemory", _frFeature, output, FeatureBytes);
            return new RecognitionExtraction(result, result == 0 ? _m.Read(output, FeatureBytes) : null);
        }
        catch { _faulted = true; throw; }
        finally { _m.Free(output); }
    }

    internal OkaoFaceFeature FrImportFeature(byte[] feature)
    {
        if (feature.Length != FeatureBytes) throw new ArgumentException("Unexpected OKAO feature size", nameof(feature));
        uint input = _m.Malloc(FeatureBytes);
        if (input == 0) throw new EmulationAbort("OKAO feature transfer allocation failed");
        try
        {
            _m.Write(input, feature);
            // The other guest's feature uses the shipped reader at
            // 0x0092CFA0..0x0092D0A4 before entering the album machine.
            var read = FrOneOut("OKAO_FR_ReadFeatureFromMemory", new[] { _frFeature, input, FeatureBytes });
            if (read.Result != 0 || read.Value != 0)
                throw new EmulationAbort($"OKAO feature transfer returned {read.Result}/{read.Value}");
            return new OkaoFaceFeature(this, _frFeature);
        }
        finally { _m.Free(input); }
    }

    private int FrExtract(uint image, int width, int height, uint partResult)
        => unchecked((int)_m.Call(_m.Symbol("OKAO_FR_ExtractHandle_GRAY"),
            new[] { _frFeature, image, (uint)width, (uint)height }, new[] { 0u, partResult }));

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
            return FrExtract(_frImage, _frWidth, _frHeight, _frPtResult);
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
