namespace Cozmo.Robot.Emulation;

/// <summary>
/// The bounded OpenSL interface presented to the shipped Wwise output device. One instance belongs to
/// one ARM machine and is used only by its owner thread. Guest buffers remain owned by Wwise until
/// an explicit host consumption invokes the registered buffer-queue callback. This class supplies no
/// timer, physical audio device, or robot wire scheduling.
/// </summary>
internal sealed class NativeOpenSlBridge
{
    private const uint Success = 0;
    private const uint FeatureUnsupported = 12;
    private const uint Playing = 3;
    private const int QueueCapacity = 4;
    private const uint BlockBytes = 4096; // 1024 stereo i16 frames at 48 kHz

    private readonly ArmMachine _machine;
    private readonly int _ownerThread = Environment.CurrentManagedThreadId;
    private readonly Dictionary<string, uint> _iids = new(StringComparer.Ordinal);
    private readonly Queue<(uint Address, uint Size)> _buffers = new();
    private readonly uint _engineObject, _engine, _mix, _player, _play, _queue;
    private uint _callback, _callbackContext, _queueIndex, _playState = 1;
    private bool _engineAlive, _mixAlive, _playerAlive;

    internal NativeOpenSlBridge(ArmMachine machine)
    {
        _machine = machine ?? throw new ArgumentNullException(nameof(machine));
        foreach (string name in new[] { "ENGINE", "ANDROIDCONFIGURATION", "ANDROIDSIMPLEBUFFERQUEUE", "BUFFERQUEUE", "PLAY", "RECORD" })
        {
            string captured = name;
            machine.DataImports.Add("SL_IID_" + name, m =>
            {
                uint cell = m.StaticAlloc(4);
                uint token = m.StaticAlloc(16);
                m.W32(cell, token);
                _iids.Add(captured, token);
                return cell;
            });
        }

        _engineObject = Object((0, "engine_realize", Ok), (12, "engine_get", EngineGet),
            (16, "engine_register", Ok), (24, "engine_destroy", DestroyEngine));
        _engine = Object((8, "create_player", CreatePlayer), (28, "create_mix", CreateMix));
        _mix = Object((0, "mix_realize", Ok), (16, "mix_register", Ok), (24, "mix_destroy", DestroyMix));
        _player = Object((0, "player_realize", Ok), (12, "player_get", PlayerGet),
            (16, "player_register", Ok), (24, "player_destroy", DestroyPlayer));
        _play = Object((0, "play_state", SetPlayState));
        _queue = Object((0, "enqueue", Enqueue), (4, "clear", Clear),
            (8, "get_state", GetState), (12, "queue_register", RegisterCallback));
        machine.Libc.Add("slCreateEngine", CreateEngine);
    }

    internal int PendingCount => _buffers.Count;
    internal uint PlayState => _playState;
    internal bool HasRegisteredCallback => _callback != 0;
    internal bool IsDestroyed => !_engineAlive && !_mixAlive && !_playerAlive;

    private void RequireOwner()
    {
        if (Environment.CurrentManagedThreadId != _ownerThread)
            throw new InvalidOperationException("OpenSL bridge must run on its ARM owner thread");
    }

    private uint Object(params (uint Offset, string Name, ImportHandler Handler)[] methods)
    {
        uint instance = _machine.StaticAlloc(4);
        uint table = _machine.StaticAlloc(0x80);
        _machine.Fill(table, 0x80, 0);
        _machine.W32(instance, table);
        foreach (var (offset, name, handler) in methods)
        {
            string symbol = "native_opensl_" + name;
            _machine.Libc.Add(symbol, handler);
            _machine.W32(table + offset, _machine.Stub(symbol));
        }
        return instance;
    }

    private static StubResult Ok(ArmMachine m) => Success;

    private StubResult CreateEngine(ArmMachine m)
    {
        RequireOwner();
        if (_engineAlive) throw new EmulationAbort("OpenSL engine already exists");
        m.W32(m.Reg(0), _engineObject);
        _engineAlive = true;
        return Success;
    }

    private StubResult EngineGet(ArmMachine m)
    {
        RequireOwner();
        if (m.Reg(0) != _engineObject || m.Reg(1) != _iids["ENGINE"] || !_engineAlive)
            throw new EmulationAbort("unexpected OpenSL engine interface request");
        m.W32(m.Reg(2), _engine);
        return Success;
    }

    private StubResult CreateMix(ArmMachine m)
    {
        RequireOwner();
        if (m.Reg(0) != _engine || !_engineAlive || _mixAlive)
            throw new EmulationAbort("unexpected OpenSL output-mix creation");
        m.W32(m.Reg(1), _mix);
        _mixAlive = true;
        return Success;
    }

    private StubResult CreatePlayer(ArmMachine m)
    {
        RequireOwner();
        if (m.Reg(0) != _engine || !_engineAlive || !_mixAlive || _playerAlive)
            throw new EmulationAbort("unexpected OpenSL player creation");
        uint source = m.Reg(2);
        uint locator = m.R32(source);
        uint format = m.R32(source + 4);
        // Native caller passes the source structures to the host. Validate the fields whose values
        // the checked OpenSL probe has established before allowing the guest-owned ring to run.
        if (m.R32(locator) != 0x800007BD || m.R32(locator + 4) != QueueCapacity)
            throw new EmulationAbort("unsupported OpenSL source locator or queue capacity");
        uint[] expectedFormat = { 2, 2, 48000000, 16, 16, 3, 2 };
        for (int i = 0; i < expectedFormat.Length; i++)
            if (m.R32(format + (uint)(4 * i)) != expectedFormat[i])
                throw new EmulationAbort("unsupported OpenSL PCM format");
        uint sinkLocator = m.R32(m.Reg(3));
        if (m.R32(sinkLocator) != 4 || m.R32(sinkLocator + 4) != _mix ||
            m.Arg(4) != 1 || m.R32(m.Arg(5)) != _iids["BUFFERQUEUE"] || m.R32(m.Arg(6)) != 1)
            throw new EmulationAbort("unsupported OpenSL output sink or required interface");
        m.W32(m.Reg(1), _player);
        _playerAlive = true;
        return Success;
    }

    private StubResult PlayerGet(ArmMachine m)
    {
        RequireOwner();
        if (m.Reg(0) != _player || !_playerAlive) throw new EmulationAbort("invalid OpenSL player");
        uint iid = m.Reg(1);
        if (iid == _iids["ANDROIDCONFIGURATION"]) return FeatureUnsupported;
        uint result = iid == _iids["PLAY"] ? _play : iid == _iids["BUFFERQUEUE"] ? _queue : 0;
        if (result == 0) throw new EmulationAbort("unsupported OpenSL player interface");
        m.W32(m.Reg(2), result);
        return Success;
    }

    private StubResult RegisterCallback(ArmMachine m)
    {
        RequireOwner();
        if (m.Reg(0) != _queue || !_playerAlive || m.Reg(1) == 0)
            throw new EmulationAbort("invalid OpenSL buffer callback registration");
        _callback = m.Reg(1);
        _callbackContext = m.Reg(2);
        return Success;
    }

    private StubResult SetPlayState(ArmMachine m)
    {
        RequireOwner();
        if (m.Reg(0) != _play || !_playerAlive) throw new EmulationAbort("invalid OpenSL play interface");
        if (m.Reg(1) != Playing) throw new EmulationAbort("unrecovered OpenSL play-state transition");
        _playState = m.Reg(1);
        return Success;
    }

    private StubResult Enqueue(ArmMachine m)
    {
        RequireOwner();
        if (m.Reg(0) != _queue || !_playerAlive || _buffers.Count >= QueueCapacity)
            throw new EmulationAbort("invalid or full OpenSL buffer queue");
        uint address = m.Reg(1), size = m.Reg(2);
        if (address == 0 || (address & 3) != 0 || size != BlockBytes) throw new EmulationAbort("unsupported OpenSL output block");
        _ = m.Span(address, size); // validate the complete guest-owned range now
        if (_buffers.Any(buffer => buffer.Address == address))
            throw new EmulationAbort("OpenSL guest queued a buffer it still owns");
        _buffers.Enqueue((address, size));
        return Success;
    }

    private StubResult Clear(ArmMachine m)
    {
        RequireOwner();
        if (m.Reg(0) != _queue || !_playerAlive) throw new EmulationAbort("invalid OpenSL buffer queue");
        throw new EmulationAbort("OpenSL buffer-queue Clear behavior is not recovered");
    }

    private StubResult GetState(ArmMachine m)
    {
        RequireOwner();
        if (m.Reg(0) != _queue || !_playerAlive) throw new EmulationAbort("invalid OpenSL buffer queue");
        uint output = m.Reg(1);
        m.W32(output, (uint)_buffers.Count);
        m.W32(output + 4, _queueIndex);
        return Success;
    }

    /// <summary>
    /// Copies one guest block at an explicit platform consumption boundary, then calls the guest's
    /// registered completion callback. Returns null while stopped or empty; no wall clock is assumed.
    /// </summary>
    internal byte[]? ConsumeOne()
    {
        RequireOwner();
        if (_playState != Playing || _buffers.Count == 0) return null;
        if (!_playerAlive || _callback == 0) throw new EmulationAbort("OpenSL queue has no live callback");
        var buffer = _buffers.Dequeue();
        byte[] data = _machine.Read(buffer.Address, buffer.Size);
        _queueIndex = unchecked(_queueIndex + 1);
        _machine.Call(_callback, new[] { _queue, _callbackContext }, count: 1_000_000);
        return data;
    }

    private StubResult DestroyPlayer(ArmMachine m)
    {
        RequireOwner();
        if (m.Reg(0) != _player || !_playerAlive) throw new EmulationAbort("invalid OpenSL player destruction");
        _buffers.Clear();
        _callback = _callbackContext = 0;
        _playerAlive = false;
        return Success;
    }
    private StubResult DestroyMix(ArmMachine m)
    {
        RequireOwner();
        if (m.Reg(0) != _mix || !_mixAlive || _playerAlive) throw new EmulationAbort("invalid OpenSL mix destruction");
        _mixAlive = false;
        return Success;
    }
    private StubResult DestroyEngine(ArmMachine m)
    {
        RequireOwner();
        if (m.Reg(0) != _engineObject || !_engineAlive || _playerAlive || _mixAlive)
            throw new EmulationAbort("invalid OpenSL engine destruction");
        _engineAlive = false;
        return Success;
    }
}
