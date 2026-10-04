namespace Cozmo.Robot.Behavior;

/// <summary>
/// <c>Anki::Cozmo::FreeplayDataTracker</c> (0x0056EBD4..0x0056EFD0): accumulates the robot's active freeplay
/// time while none of its four pause sources is set, and reports it every thirty seconds as
/// <c>robot.active_freeplay_time</c>. Created by <c>AIComponent</c> at AIComponent+0x2c and updated from
/// <c>AIComponent::Update</c> (0x00569A78 / 0x00569F46).
///
/// Correction C1 §4 (2026-09-29): the accumulation happens in <c>SendData</c>, not in <c>Update</c>.
/// <c>Update</c> 0x0056EC1A only checks <c>now &gt;= +0x18</c> and tail-calls <c>SendData</c>; <c>SendData</c>
/// 0x0056EC48 reads the clock, adds <c>now - lastTimestamp</c> (<c>+0x10/+0x14</c>) to the accumulator
/// (<c>+0x20/+0x24</c>, both u64 nanoseconds) while the pause set is empty, rounds, reports, zeroes the accumulator and sets
/// <c>+0x18 = now + 30.0f</c> (the only f32 seconds field). The pause set's internal setter (0x0056EECC) flushes the running
/// segment when pausing, and <c>ClearFreeplayPauseFlag</c> 0x0056EFF8 stamps <c>+0x10 = now</c> when the set becomes empty.
/// </summary>
// fidelity: M15-015
public sealed class FreeplayDataTracker
{
    /// <summary>The report interval: the constructor sets next-send to now + 30.0f (vmov.f32 #30.0 / vadd.f32, 0x0056EBF6/0x0056EC00).</summary>
    public const float ReportIntervalSec = 30.0f;

    /// <summary>A reported active time of 37 s or more is the <c>DataTooHigh</c> error (<c>cmp r3, #0x25; blt</c>, 0x0056ED24).</summary>
    public const int MaxActiveSec = 37;

    /// <summary>The nanoseconds-to-seconds divisor of <c>SendData</c> (the double literal at 0x0056EE28 is 1.0e9).</summary>
    private const double NanosPerSec = 1.0e9;

    /// <summary>
    /// <c>BaseStationTimer::GetCurrentTimeInNanoSeconds</c> 0x0084BCB7: the u64 nanosecond clock the accumulated time and the resume time
    /// are kept in (+0x20/+0x24 and +0x10/+0x14).
    /// </summary>
    private readonly Func<ulong> _clockNanos;
    /// <summary>
    /// The pause set, the accumulator and the timestamps are written by the engine-thread callbacks
    /// (<c>OffTreads.SetFreeplayPauseFlagOffTreads</c>, <c>Sensors.OnChargerPlatformChanged</c>) and
    /// read/written on the tick. A short lock on this gate makes the two agree; the approach is a lock,
    /// not a marshal to the tick, so a pause is visible as soon as the callback returns.
    /// </summary>
    private readonly object _gate = new();
    private readonly HashSet<FreeplayPauseFlag> _paused = new();
    private ulong _accumulatedNanos;   // +0x20/+0x24: the accumulated active time, u64 nanoseconds
    private ulong _lastResumeNanos;    // +0x10/+0x14: the nanosecond clock the current running segment started at
    private float _nextSendSec;        // +0x18: f32 seconds

    /// <summary><c>BaseStationTimer::GetCurrentTimeInSeconds</c> 0x0084BCA9: the f32 (<c>vcvt.f32.f64</c> of ns / 1e9, 0x0084BC7C..0x0084BC8C).</summary>
    private static float SecondsF(ulong nanos) => (float)((double)nanos / NanosPerSec);

    public FreeplayDataTracker(Func<ulong> clockNanos)
    {
        _clockNanos = clockNanos;
        // Ctor 0x0056EBD4: +0x10/+0x14 = 0 and +0x18 = GetCurrentTimeInSeconds() + 30.0f. The last timestamp stays 0 until a
        // SendData or the clear of the last pause flag stamps it, so a pause before then has no running segment to flush.
        _lastResumeNanos = 0;
        _nextSendSec = SecondsF(_clockNanos()) + ReportIntervalSec;
    }

    /// <summary>A seconds clock (the stack's one clock) read as the engine's nanosecond timer: whole nanoseconds, rounded.</summary>
    public FreeplayDataTracker(Func<double> clockSec) : this(() => (ulong)Math.Round(clockSec() * NanosPerSec)) { }

    /// <summary>The pause flags whose union pauses accumulation (the name table at 0x01023554).</summary>
    public IReadOnlyCollection<FreeplayPauseFlag> PausedFlags { get { lock (_gate) return _paused.ToArray(); } }

    /// <summary>Whether any pause source is set.</summary>
    public bool IsPaused { get { lock (_gate) return _paused.Count > 0; } }

    /// <summary>The active time accumulated since the last report, in u64 nanoseconds (+0x20/+0x24).</summary>
    public ulong AccumulatedNanos { get { lock (_gate) return _accumulatedNanos; } }

    /// <summary>The active time accumulated since the last report, in seconds (a view of <see cref="AccumulatedNanos"/>).</summary>
    public double ActiveSeconds { get { lock (_gate) return _accumulatedNanos / NanosPerSec; } }

    /// <summary>The u64 nanosecond stamp the running segment started at (+0x10/+0x14).</summary>
    public ulong LastResumeNanos { get { lock (_gate) return _lastResumeNanos; } }

    /// <summary>The f32 second the next report is due (<c>+0x18</c>).</summary>
    public float NextSendSec { get { lock (_gate) return _nextSendSec; } }

    /// <summary>Raised with the active freeplay seconds (the rounded integer, 0x0056ED1C) by a report under <see cref="MaxActiveSec"/>.</summary>
    public event Action<double>? ActiveFreeplayTime;

    /// <summary>The engine's log lines, including the <c>DataTooHigh</c> error.</summary>
    public event Action<string>? Log;

    /// <summary>
    /// <c>FreeplayDataTracker::SetFreeplayPauseFlag(flag, paused)</c> 0x0056EEBC: paused calls the setter 0x0056EECC, otherwise
    /// <c>ClearFreeplayPauseFlag</c> 0x0056EFF8. The setter reads the nanosecond clock and, when the set was empty and the stored
    /// timestamp is non-zero (a u64 test, 0x0056EEF8), adds <c>now - lastResume</c> to the accumulator (0x0056EF00..0x0056EF10), then
    /// inserts the flag. The clear erases the flag and, when the set has become empty (0x0056F082), stamps <c>+0x10 = now</c> (0x0056F096).
    /// The flags are GameControl, Spark, OffTreads and OnCharger.
    /// </summary>
    // fidelity: M15-015
    public void SetFreeplayPauseFlag(FreeplayPauseFlag flag, bool paused)
    {
        lock (_gate)
        {
            if (paused)
            {
                ulong now = _clockNanos();                       // 0x0056EEDA, read before the set test
                if (_paused.Count == 0 && _lastResumeNanos != 0)
                    unchecked { _accumulatedNanos += now - _lastResumeNanos; }
                _paused.Add(flag);
            }
            else if (_paused.Count != 0 && _paused.Remove(flag) && _paused.Count == 0)
            {
                _lastResumeNanos = _clockNanos();                // 0x0056F088..0x0056F096
            }
        }
    }

    /// <summary>
    /// <c>FreeplayDataTracker::Update</c> 0x0056EC1A: reads <c>GetCurrentTimeInSeconds</c> (f32) and returns while it is below <c>+0x18</c>
    /// (<c>vcmpe.f32</c>, <c>it lt</c>, 0x0056EC2E..0x0056EC38; an unordered compare also returns), else tail-calls <see cref="SendData"/>.
    /// </summary>
    // fidelity: M15-015
    public void Update()
    {
        lock (_gate)
        {
            float now = SecondsF(_clockNanos());
            if (!(now >= _nextSendSec)) return;
            SendDataCore();
        }
    }

    /// <summary>
    /// <c>FreeplayDataTracker::ForceUpdate</c> 0x0056EEB8 is <c>SendData</c>, called from the
    /// <c>BehaviorSystemManager</c> destructor (0x005110D4): flush whatever has accumulated.
    /// </summary>
    // fidelity: M15-015
    public void ForceUpdate() { lock (_gate) SendDataCore(); }

    /// <summary>
    /// <c>FreeplayDataTracker::SendData</c> 0x0056EC48: reads the nanosecond clock; while the pause set is empty adds
    /// <c>now - lastResume</c> (u64) to the accumulator (0x0056EC72..0x0056EC84). Then <b>only when the accumulator is non-zero</b>
    /// (<c>orrs</c> 0x0056ECF6): <c>round(acc / 1e9)</c> converted to s32 (0x0056ED00..0x0056ED1C); under 37 (<c>blt</c>, signed) it emits
    /// <c>robot.active_freeplay_time</c> with that integer, otherwise it logs <c>DataTooHigh</c>. The accumulator is always zeroed, the
    /// resume stamp is set to <c>now</c> while the set is empty (0x0056EDC0..0x0056EDC4), and the next send is always
    /// <c>GetCurrentTimeInSeconds() + 30.0f</c>, a second read of the clock (0x0056EDC8..0x0056EDDC).
    /// </summary>
    // fidelity: M15-015
    public void SendData() { lock (_gate) SendDataCore(); }

    private void SendDataCore()
    {
        ulong now = _clockNanos();
        if (_paused.Count == 0)
            unchecked { _accumulatedNanos += now - _lastResumeNanos; }
        if (_accumulatedNanos != 0)
        {
            double rounded = Math.Round((double)_accumulatedNanos / NanosPerSec, MidpointRounding.AwayFromZero);   // C round()
            int seconds = rounded >= int.MaxValue ? int.MaxValue : (int)rounded;                                  // vcvt.s32.f64 saturates
            if (seconds < MaxActiveSec)
            {
                Log?.Invoke($"robot.active_freeplay_time {seconds}");
                ActiveFreeplayTime?.Invoke(seconds);
            }
            else
            {
                Log?.Invoke($"error: FreeplayDataTracker.SendData.DataTooHigh Trying to send a freeplay time of {seconds} sec ({_accumulatedNanos} nanos), but update period is {30.0:F6}");
            }
        }
        _accumulatedNanos = 0;
        if (_paused.Count == 0) _lastResumeNanos = now;
        _nextSendSec = SecondsF(_clockNanos()) + ReportIntervalSec;
    }
}

/// <summary>
/// The four <c>FreeplayDataTracker</c> pause sources, in the order of the name table at 0x01023554:
/// GameControl, Spark, OffTreads, OnCharger. GameControl is set by
/// <c>BehaviorManager::SetCurrentActivity</c> when the high-level activity is not Freeplay (0x005A106C);
/// Spark by <c>GetDesiredActiveBehaviorInternal</c> after a pick (0x005AE4E2); OffTreads by
/// <c>Robot::CheckAndUpdateTreadsState</c> (0x005121F4); OnCharger by <c>Robot::SetOnChargerPlatform</c>
/// (0x00511DB0).
/// </summary>
public enum FreeplayPauseFlag { GameControl = 0, Spark = 1, OffTreads = 2, OnCharger = 3 }
