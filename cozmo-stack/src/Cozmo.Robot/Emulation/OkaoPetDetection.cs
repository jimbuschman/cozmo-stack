using System.Diagnostics;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Emulation;

internal sealed class PetGuestFailure(string key, int code, uint result, string? detail = null) : Exception(detail ?? $"OMCV Result Code={code}")
{
    internal string Key { get; } = key;
    internal int Code { get; } = code;
    internal uint Result { get; } = result;
}

internal sealed record RawPetDetection(int[] Words);

// PetTracker owns one guest detector/result pair across successive full-size images.
internal sealed class OkaoPetDetection : IDisposable
{
    private readonly ArmMachine _machine;
    private uint _detector, _result, _image, _count, _info;
    private int _width, _height;
    private bool _faulted, _disposed;
    internal byte VersionMajor { get; private set; }
    internal byte VersionMinor { get; private set; }
    internal Action<string, string>? Warning { get; set; }
    internal List<string> DisposalErrors { get; } = new();

    internal OkaoPetDetection(string directory, Func<uint>? cpuClock = null)
    {
        _machine = new ArmMachine(directory, new[] { "libc++_shared.so", "libcozmoEngine.so" });
        try
        {
            // Android clock() ticks at 1 MHz; this clock is local to this guest machine.
            _machine.Overrides["clock"] = _ =>
            {
                if (cpuClock is not null) return cpuClock();
                using var process = Process.GetCurrentProcess();
                return unchecked((uint)(process.TotalProcessorTime.Ticks / 10));
            };
            _machine.Relocate();
            uint version = _machine.Malloc(8);
            int versionStatus = Call("GetVersion", version, version + 1);
            VersionMajor = (byte)_machine.R8(version); VersionMinor = (byte)_machine.R8(version + 1);
            _machine.Free(version);
            if (versionStatus != 0) throw new PetGuestFailure("PetTracker.Init.OmcvVersionFail", versionStatus, 1);
        }
        catch (PetGuestFailure) { Dispose(); throw; }
        catch { _faulted = true; _machine.Dispose(); throw; }
    }

    internal void Initialize(IReadOnlyDictionary<string, int> settings)
    {
        try
        {
            _detector = unchecked((uint)Call("CreateHandle", 5, unchecked((uint)settings["MaxPets"])));
            if (_detector == 0) throw new PetGuestFailure("PetTracker.Init.FaceLibCommonHandleAllocFail", 0, 0x01000000);
            _result = unchecked((uint)Call("CreateResultHandle", _detector));
            if (_result == 0) throw new PetGuestFailure("PetTracker.Init.OmcvCreateResultHandleFail", 0, 0x01000000);
            Setup("SetAngle", "PetTracker.Init.OmcvSetAngleFailed", 0, 0x803803);
            Setup("MV_SetSearchCycle", "PetTracker.Init.OmcvSetSearchCycleFailed", V("InitialSearchCycle"), V("NewSearchCycle"));
            Setup("SetSizeRange", "PetTracker.Init.OmcvSetSizeRangeFailed", V("MinFaceSize"), V("MaxFaceSize"));
            Setup("SetThreshold", "PetTracker.Init.FaceLibSetThresholdFailed", V("DetectionThreshold"));
            Setup("MV_SetLostParam", "PetTracker.Init.FaceLibSetLostParamFailed", V("TrackLostCount"), V("TrackLostCount"));
            Setup("MV_SetSteadinessParam", "PetTracker.Init.FaceLibSetSteadinessFailed", V("TrackSteadiness"), V("TrackSteadiness"));
            uint V(string name) => unchecked((uint)settings[name]);
        }
        catch (PetGuestFailure) { throw; }
        catch { _faulted = true; throw; }
    }

    private int Call(string name, params uint[] args) => unchecked((int)_machine.Call(_machine.Symbol("OMCV_PD_" + name), args));
    internal void ObserveSetupForTest(Action<string> observe)
    {
        foreach (string name in new[] { "CreateHandle", "CreateResultHandle", "SetAngle", "MV_SetSearchCycle", "SetSizeRange", "SetThreshold", "MV_SetLostParam", "MV_SetSteadinessParam" })
        {
            string captured = name;
            _machine.ObserveAddress(_machine.Symbol("OMCV_PD_" + captured), _ => observe(captured));
        }
    }
    internal void OverrideDetectForTest(Func<int> result) => _machine.HookAddress(_machine.Symbol("OMCV_PD_Detect"), _ => result());
    internal void OverrideDeleteResultForTest(int result) => _machine.HookAddress(_machine.Symbol("OMCV_PD_DeleteResultHandle"), _ => result);
    internal void ObserveDeleteHandleForTest(Action observe) => _machine.ObserveAddress(_machine.Symbol("OMCV_PD_DeleteHandle"), _ => observe());
    private void Setup(string name, string key, params uint[] args)
    {
        var values = new uint[args.Length + 1]; values[0] = _detector; args.CopyTo(values, 1);
        int result = Call(name, values);
        if (result != 0) throw new PetGuestFailure(key, result, 0x03000000);
    }

    internal IReadOnlyList<DetectedPet> Detect(GrayImage image)
    {
        var pets = new List<DetectedPet>();
        DetectRaw(image, raw =>
        {
            int[] words = raw.Words;
            var type = words[1] switch { 0x100000 => PetType.Cat, 0x80000 => PetType.Dog, _ => PetType.Unknown };
            if (type == PetType.Unknown) { Warning?.Invoke("PetTracker.Update.InvalidPetType", $"ObjectType={words[1]}"); return; }
            int width = words[6], height = words[7];
            pets.Add(new DetectedPet(words[2], type, new FaceRect((float)unchecked(words[4] - width / 2),
                (float)unchecked(words[5] - height / 2), (float)width, (float)height), words[3])
                { TrackerResultFlag = words[11] == 1 });
        });
        return pets;
    }

    internal IReadOnlyList<RawPetDetection> DetectRaw(GrayImage image, Action<RawPetDetection>? onResult = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_faulted) throw new InvalidOperationException("Pet detector guest is faulted");
        try
        {
            if (_image == 0 || _width != image.Width || _height != image.Height)
            {
                if (_image != 0) _machine.Free(_image);
                _image = _machine.Malloc((ulong)image.Pixels.Length); _width = image.Width; _height = image.Height;
            }
            if (_count == 0) { _count = _machine.Malloc(16); _info = _machine.Malloc(56); }
            _machine.Write(_image, image.Pixels);
            int status = unchecked((int)_machine.Call(_machine.Symbol("OMCV_PD_Detect"),
                new[] { _detector, _image, (uint)_width, (uint)_height }, new[] { _result }));
            if (status != 0) throw new PetGuestFailure("PetTracker.Update.OmcvDetectFail", status, 1);
            _machine.Fill(_count, 16, 0);
            status = Call("GetResultCount", _result, 0x180000, _count);
            if (status != 0) throw new PetGuestFailure("PetTracker.Update.OmcvGetResultCountFail", status, 1);
            int count = unchecked((int)_machine.R32(_count));
            var pets = new List<RawPetDetection>();
            for (int i = 0; i < count; i++)
            {
                _machine.Fill(_info, 56, 0);
                status = Call("GetResultInfo", _result, 0x180000, (uint)i, _info);
                if (status != 0) throw new PetGuestFailure("PetTracker.Update.OmcvGetResultInfoFail", status, 1,
                    $"Detection index {i} of {count}. OMCV Result Code={status}");
                var words = new int[14];
                for (int word = 0; word < words.Length; word++) words[word] = unchecked((int)_machine.R32(_info + 4u * (uint)word));
                var raw = new RawPetDetection(words);
                pets.Add(raw);
                onResult?.Invoke(raw);
            }
            return pets;
        }
        catch (PetGuestFailure) { throw; }
        catch { _faulted = true; throw; }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            if (!_faulted)
            {
                try
                {
                    if (_result != 0 && Call("DeleteResultHandle", _result) != 0)
                        DisposalErrors.Add("PetTracker.Destructor.OmcvDeleteResultHandleFail");
                }
                finally
                {
                    if (_detector != 0 && Call("DeleteHandle", _detector) != 0)
                        DisposalErrors.Add("PetTracker.Destructor.OmcvDeleteHandleFail");
                }
            }
        }
        finally { _machine.Dispose(); }
    }
}
