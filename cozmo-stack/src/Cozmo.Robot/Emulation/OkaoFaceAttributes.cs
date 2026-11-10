using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Emulation;

public sealed partial class OkaoFaceDetection
{
    private uint _ex, _exResult, _sm, _smResult, _gb, _gbResult, _attributeImpl, _attributeFace;

    private void InitializeStockAttributes()
    {
        _ex=StockCreate("OKAO_EX_CreateHandle","FaceTrackerImpl.Init.FaceLibEstimateExpressionHandleAllocFail",_co);
        _exResult=StockCreate("OKAO_EX_CreateResultHandle","FaceTrackerImpl.Init.FaceLibExpressionResultHandleAllocFail",_co);
        _sm=StockCreate("OKAO_SM_CreateHandle","FaceTrackerImpl.Init.FaceLibSmileDetectionHandleAllocFail");
        _smResult=StockCreate("OKAO_SM_CreateResultHandle","FaceTrackerImpl.Init.FaceLibSmileResultHandleAllocFail");
        _gb=StockCreate("OKAO_GB_CreateHandle","FaceTrackerImpl.Init.FaceLibGazeBlinkDetectionHandleAllocFail");
        _gbResult=StockCreate("OKAO_GB_CreateResultHandle","FaceTrackerImpl.Init.FaceLibGazeBlinkResultHandleAllocFail");
    }

    // The stock owner creates handles eagerly; the independent legacy oracle keeps lazy allocation.
    // Scratch carriers are allocated only when the host first projects optional attributes.
    private void EnsureAttributeHandles()
    {
        if (_attributeImpl != 0) return;
        uint Create(string name, params uint[] args)
        {
            uint handle = _m.Call(_m.Symbol(name), args);
            if (handle == 0) throw new EmulationAbort(name + " returned null");
            return handle;
        }
        if(_ex==0)_ex = Create("OKAO_EX_CreateHandle", _co);
        if(_exResult==0)_exResult = Create("OKAO_EX_CreateResultHandle", _co);
        if(_sm==0)_sm = Create("OKAO_SM_CreateHandle");
        if(_smResult==0)_smResult = Create("OKAO_SM_CreateResultHandle");
        if(_gb==0)_gb = Create("OKAO_GB_CreateHandle");
        if(_gbResult==0)_gbResult = Create("OKAO_GB_CreateResultHandle");
        _attributeImpl = _m.Malloc(0x138);
        _attributeFace = _m.Malloc(0x110);
        if (_attributeImpl == 0 || _attributeFace == 0) throw new EmulationAbort("OKAO attribute carrier allocation failed");
    }

    private NativeFaceAttributes ReadAttributes(int mask, int width, int height)
    {
        _m.Fill(_attributeImpl, 0x138, 0);
        _m.Fill(_attributeFace, 0x110, 0);
        _m.W32(_attributeImpl + 0x70, _ptResult);
        _m.W32(_attributeImpl + 0x78, _ex); _m.W32(_attributeImpl + 0x7C, _exResult);
        _m.W32(_attributeImpl + 0x80, _sm); _m.W32(_attributeImpl + 0x84, _smResult);
        _m.W32(_attributeImpl + 0x88, _gb); _m.W32(_attributeImpl + 0x8C, _gbResult);
        _m.W8(_attributeImpl + 0x43, (mask & (1 << 12)) != 0 ? 1u : 0u);
        _m.W8(_attributeImpl + 0x44, (mask & (1 << 13)) != 0 ? 1u : 0u);
        uint baseAddress = _m.Library("libcozmoEngine.so").Base;
        void Run(uint address, string label)
        {
            int status = unchecked((int)_m.Call(baseAddress + address + 1,
                new[] { _attributeImpl, (uint)width, (uint)height, _buf }, new[] { _attributeFace }));
            if (status != 0) Diagnostic?.Invoke($"M14-010 {label} returned {status}");
        }
        if ((mask & (1 << 10)) != 0) Run(0x86D1E8, "expression");
        if ((mask & (1 << 11)) != 0) Run(0x86D3C8, "smile");
        if ((mask & ((1 << 12) | (1 << 13))) != 0) Run(0x86D550, "gaze/blink");
        var scores = Array.AsReadOnly(_m.Read(_attributeFace + 0xB0, 5));
        FaceAttributePair Pair(uint offset) => new(_m.R8(_attributeFace + offset) != 0,
            BitConverter.UInt32BitsToSingle(_m.R32(_attributeFace + offset + 4)),
            BitConverter.UInt32BitsToSingle(_m.R32(_attributeFace + offset + 8)));
        return new NativeFaceAttributes(scores, Pair(0xB8), Pair(0xC4), Pair(0xD0));
    }

    private void DeleteAttributeHandles()
    {
        if (_sm != 0) FrCall("OKAO_SM_DeleteHandle", _sm);
        if (_smResult != 0) FrCall("OKAO_SM_DeleteResultHandle", _smResult);
        if (_gb != 0) FrCall("OKAO_GB_DeleteHandle", _gb);
        if (_gbResult != 0) FrCall("OKAO_GB_DeleteResultHandle", _gbResult);
        if (_exResult != 0) FrCall("OKAO_EX_DeleteResultHandle", _exResult);
        if (_ex != 0) FrCall("OKAO_EX_DeleteHandle", _ex);
        if (_attributeFace != 0) _m.Free(_attributeFace);
        if (_attributeImpl != 0) _m.Free(_attributeImpl);
    }

    private void DeleteStockAttributes(Action<string,uint,string> delete)
    {
        delete("OKAO_SM_DeleteHandle",_sm,"FaceTrackerImpl.Destructor.FaceLibSmileDetectHandleDeleteFail");
        delete("OKAO_SM_DeleteResultHandle",_smResult,"FaceTrackerImpl.Destructor.FaceLibSmileResultHandleDeleteFail");
        delete("OKAO_GB_DeleteHandle",_gb,"FaceTrackerImpl.Destructor.FaceLibGazeBlinkDetectHandleDeleteFail");
        delete("OKAO_GB_DeleteResultHandle",_gbResult,"FaceTrackerImpl.Destructor.FaceLibGazeBlinkResulttHandleDeleteFail");
        delete("OKAO_EX_DeleteResultHandle",_exResult,"FaceTrackerImpl.Destructor.FaceLibExpressionResultHandleDeleteFail");
        delete("OKAO_EX_DeleteHandle",_ex,"FaceTrackerImpl.Destructor.FaceLibEstimateExpressionHandleDeleteFail");
    }
}
