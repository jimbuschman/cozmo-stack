namespace Cozmo.Robot.Animation.Wwise;

/// <summary>Mode-2 WavePortal source, created by the source factory and initialized at StartStream.</summary>
public sealed class WwiseWavePortalSource : IWwisePitchNodeSource, IWwiseStreamingVoiceSource
{
    private readonly uint _sourceId;
    private readonly WwiseRuntimeRegistry? _registry;
    private readonly WavePortal _portal;
    private WavePortal.Voice? _voice;

    internal WwiseWavePortalSource(uint sourceId, WwiseRuntimeRegistry? registry, WwisePlayingInstance? owner, WavePortal portal)
    { _sourceId = sourceId; _registry = registry; Owner = owner; _portal = portal; }

    public bool HasPitchNode => true;
    public WwisePlayingInstance? Owner { get; }
    public WwiseDecodeState Io { get; set; } = new();
    public int Channels => 1;
    public int SampleRate => _voice?.SampleRate ?? 0;
    public bool StartStreamSucceeded { get; set; }
    public bool WritesSourceFormatInStartStream => true;

    public int StartStream(uint arg1DC, uint arg1E0)
    {
        if (Owner is null)
            throw new WwiseMissingBehaviourException("M6-025: mode-2 StartStream requires its owner PBI");
        if (_registry is null)
            throw new WwiseMissingBehaviourException("M6-025: mode-2 StartStream requires the HIRC registry");
        // 0xA7898C..0xA78A68: source ID addresses the custom descriptor, whose plugin ID selects the creator.
        if (_registry.Find(WwiseRegistryTable.FxCustom, _sourceId) is not WwiseRuntimeFx fx || fx.PluginId10 != WavePortal.PluginId)
            return 2;
        var (result, voice) = _portal.Initialize(); // creator 0x8DD92A -> 0x8DD624, once
        _voice = voice;
        _voice?.Reset(); // 0xA78B6C -> 0x8DDC38
        if (voice is not null && Owner is { } owner)
        {
            // Low three bits are 5; upper bits of the source byte are uninitialized in native.
            // No modeled reader uses the upper bits. Use zero as the host representation.
            owner.SourceFormat158 = (uint)voice.SampleRate;
            owner.Word15C = 0x4101;
            owner.Byte160 = 0x20;
            owner.Byte161 = 0x01;
            owner.Byte162 = 5;
        }
        return result;
    }

    public int Render(WwiseVoiceBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        if (_voice is not { } voice)
        {
            Io.Data = null;
            Io.MaxFrames = 0;
            Io.ValidFrames = 0;
            Io.MarkerCount = 0;
            Io.Markers = null;
            Io.Position = uint.MaxValue;
            Io.Total = uint.MaxValue;
            Io.Rate = 1;
            Io.Word1C = 0x3F800000;
            Io.Scratch08 = 0x2B;
            Io.Code28 = 2;
            buffer.Result = 2;
            return 2; // 0xA785A4..0xA785E0
        }
        int capacity = Io.MaxFrames; // wrapper 0xA78520 reads +0xC, not valid count at +0xE
        if (capacity == 0)
        {
            Io.Code28 = WavePortal.NoMoreData;
            buffer.Result = WavePortal.NoMoreData;
            return WavePortal.NoMoreData;
        }
        Io.ValidFrames = 0;
        var data = new float[capacity];
        int result = voice.Execute(data); // 0x8DDC5A..0x8DDD20
        Io.Data = data;
        Io.ChannelConfig = 0x4101;
        Io.MaxFrames = (ushort)capacity;
        Io.ValidFrames = (ushort)capacity;
        Io.Scratch08 = (uint)result;
        Io.Code28 = (int)Io.Scratch08; // mode-2 wrapper 0xA7857C
        buffer.Result = result;
        return result;
    }

    public void ReleaseOutput() => Io.Data = null;
    public void Close2C() => _voice = null;
}
