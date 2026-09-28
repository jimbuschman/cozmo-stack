namespace Cozmo.Robot.Vision;

/// <summary>
/// The engine's vision-mode numbers, fixed by <c>Anki::Cozmo::VisionModeFromString</c> 0x796BD4 (M11-034,
/// C2.3). The dispatcher calls <c>ShouldProcessVisionMode</c> with 8, 1, 2, 9, 3, 4, 5, 6, 0xF, 7 in that
/// order (0x006B5122..0x006B55E4). The shipped <c>vision_config.json</c> enables 1, 2, 3, 4, 7, 8, 9, 15
/// and disables 10..13; the default of the unlisted modes 0, 5, 6, 14 is UNKNOWN (not set).
/// </summary>
// fidelity: M11-034
public enum VisionMode
{
    Idle = 0,
    DetectingMarkers = 1,
    DetectingFaces = 2,
    DetectingMotion = 3,
    DetectingOverheadEdges = 4,
    ReadingToolCode = 5,
    ComputingCalibration = 6,
    CheckingQuality = 7,
    ComputingStatistics = 8,
    DetectingPets = 9,
    EstimatingFacialExpression = 10,
    DetectingSmileAmount = 11,
    DetectingGaze = 12,
    DetectingBlinkAmount = 13,
    LimitedExposure = 14,
    DetectingLaserPoints = 15,
    /// <summary>The number of modes (the engine's <c>Count</c>).</summary>
    Count = 16,
}

/// <summary>
/// The engine's per-mode schedule inside <c>AllVisionModesSchedule</c> (M11-034): a bool vector at
/// <c>schedule+0</c> and a wrapping counter at <c>schedule+0xc</c>, read by
/// <c>CheckTimeToProcessAndAdvance</c> <c>0x006AF1F8</c>. The call returns the vector's current bit and then
/// advances the counter, wrapping to 0 when it reaches the vector's length. A one-element <c>{true}</c>
/// schedule therefore returns true on every call.
/// </summary>
// fidelity: M11-034
public sealed class VisionModeSchedule
{
    private readonly bool[] _frames;
    private int _counter;

    public VisionModeSchedule(IEnumerable<bool> frames)
    {
        _frames = frames.ToArray();
        if (_frames.Length == 0) throw new ArgumentException("a schedule needs at least one frame", nameof(frames));
    }

    /// <summary>The bool vector at <c>schedule+0</c>.</summary>
    public IReadOnlyList<bool> Frames => _frames;

    /// <summary>The wrapping counter at <c>schedule+0xc</c>.</summary>
    public int Counter => _counter;

    /// <summary>
    /// <c>AllVisionModesSchedule::CheckTimeToProcessAndAdvance</c> <c>0x006AF1F8</c>: return the current
    /// bit, then advance the wrapping counter.
    /// </summary>
    public bool CheckTimeToProcessAndAdvance()
    {
        bool frame = _frames[_counter];
        if (++_counter >= _frames.Length) _counter = 0;
        return frame;
    }
}

/// <summary>
/// The engine's <c>AllVisionModesSchedule</c> (M11-034): one schedule per vision-mode number.
/// <c>AllVisionModesSchedule::InitDefaultSchedules</c> <c>0x006AEFDE</c> gives all 16 modes a single
/// <c>true</c> and counter 0; the result is the <c>sDefaultSchedules</c> table at <c>0x0105C540</c>.
/// </summary>
// fidelity: M11-034
public sealed class AllVisionModesSchedule
{
    /// <summary>The engine initialises all 16 modes (0x006AEFDE).</summary>
    public const int ModeCount = 16;

    private readonly VisionModeSchedule[] _modes;

    public AllVisionModesSchedule(IReadOnlyList<VisionModeSchedule> modes)
    {
        if (modes.Count != ModeCount) throw new ArgumentException($"expected {ModeCount} mode schedules", nameof(modes));
        _modes = modes.ToArray();
    }

    public VisionModeSchedule this[int mode] => _modes[mode];

    /// <summary><c>AllVisionModesSchedule::InitDefaultSchedules</c> 0x006AEFDE: all 16 modes {true}, counter 0.</summary>
    public static AllVisionModesSchedule InitDefaultSchedules()
    {
        var modes = new VisionModeSchedule[ModeCount];
        for (int i = 0; i < ModeCount; i++) modes[i] = new VisionModeSchedule(new[] { true });
        return new AllVisionModesSchedule(modes);
    }

    /// <summary><c>sDefaultSchedules</c> 0x0105C540.</summary>
    public static AllVisionModesSchedule Default { get; } = InitDefaultSchedules();
}