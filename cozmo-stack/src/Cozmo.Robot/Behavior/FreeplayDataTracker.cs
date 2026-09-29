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
/// (<c>+0x20/+0x24</c>) while the pause set is empty, rounds, reports, zeroes the accumulator and sets
/// <c>+0x18 = now + 30.0</c>. The pause set's internal setter (0x0056EECC) flushes the running segment when
/// pausing, and <c>ClearFreeplayPauseFlag</c> 0x0056EFF8 stamps <c>+0x10 = now</c> when the set becomes empty.
/// </summary>
// fidelity: M15-015
public sealed class FreeplayDataTracker
{
    /// <summary>The report interval: the constructor sets next-send to now + 30.0 (0x0056EBF6/0x0056EC04).</summary>
    public const double ReportIntervalSec = 30.0;

    /// <summary>A reported active time of 37 s or more is the <c>DataTooHigh</c> error (0x0056EC48).</summary>
    public const double MaxActiveSec = 37.0;

    private readonly Func<double> _clockSec;
    /// <summary>
    /// The pause set, the accumulator and the timestamps are written by the engine-thread callbacks
    /// (<c>OffTreads.SetFreeplayPauseFlagOffTreads</c>, <c>Sensors.OnChargerPlatformChanged</c>) and
    /// read/written on the tick. A short lock on this gate makes the two agree; the approach is a lock,
    /// not a marshal to the tick, so a pause is visible as soon as the callback returns.
    /// </summary>
    private readonly object _gate = new();
    private readonly HashSet<FreeplayPauseFlag> _paused = new();
    private double _accumulatedSec;   // +0x20/+0x24: the accumulated active time, in seconds
    private double _lastResumeSec;    // +0x10/+0x14: the clock the current running segment started at
    private double _nextSendSec;      // +0x18

    public FreeplayDataTracker(Func<double> clockSec)
    {
        _clockSec = clockSec;
        // Ctor 0x0056EBD4: +0x10/+0x14 = 0 and +0x18 = now + 30.0. The last timestamp stays 0 until a
        // SendData stamps it, so a pause before the first send has no running segment to flush.
        _lastResumeSec = 0;
        _nextSendSec = clockSec() + ReportIntervalSec;
    }

    /// <summary>The pause flags whose union pauses accumulation (the name table at 0x01023554).</summary>
    public IReadOnlyCollection<FreeplayPauseFlag> PausedFlags { get { lock (_gate) return _paused.ToArray(); } }

    /// <summary>Whether any pause source is set.</summary>
    public bool IsPaused { get { lock (_gate) return _paused.Count > 0; } }

    /// <summary>The active time accumulated since the last report.</summary>
    public double ActiveSeconds { get { lock (_gate) return _accumulatedSec; } }

    /// <summary>The moment the next report is due (<c>+0x18</c>).</summary>
    public double NextSendSec { get { lock (_gate) return _nextSendSec; } }

    /// <summary>Raised with the active freeplay seconds by a report under <see cref="MaxActiveSec"/>.</summary>
    public event Action<double>? ActiveFreeplayTime;

    /// <summary>The engine's log lines, including the <c>DataTooHigh</c> error.</summary>
    public event Action<string>? Log;

    /// <summary>
    /// <c>FreeplayDataTracker::SetFreeplayPauseFlag(flag, paused)</c> 0x0056EEBC: add the flag to the paused
    /// set when paused, erase it otherwise. The pause set's internal setter (0x0056EECC) flushes the running
    /// segment into the accumulator when the set was empty, and <c>ClearFreeplayPauseFlag</c> 0x0056EFF8
    /// stamps the last-timestamp when the set becomes empty. The flags are GameControl, Spark, OffTreads and
    /// OnCharger.
    /// </summary>
    // fidelity: M15-015
    public void SetFreeplayPauseFlag(FreeplayPauseFlag flag, bool paused)
    {
        lock (_gate)
        {
            if (paused)
            {
                if (_paused.Count == 0)
                {
                    // Becoming paused (0x0056EECC): flush the running segment, but only when the stored
                    // timestamp is non-zero (the ctor leaves it 0 until the first send).
                    double now = _clockSec();
                    if (_lastResumeSec != 0) _accumulatedSec += now - _lastResumeSec;
                }
                _paused.Add(flag);
            }
            else if (_paused.Remove(flag) && _paused.Count == 0)
            {
                // Becoming unpaused (ClearFreeplayPauseFlag 0x0056EFF8): the running segment starts now.
                _lastResumeSec = _clockSec();
            }
        }
    }

    /// <summary>
    /// <c>FreeplayDataTracker::Update</c> 0x0056EC1A: only send when now has reached <c>+0x18</c>. The
    /// accumulation is in <see cref="SendData"/> (C1 §4).
    /// </summary>
    // fidelity: M15-015
    public void Update(double nowSec)
    {
        lock (_gate) { if (nowSec >= _nextSendSec) SendDataCore(nowSec); }
    }

    /// <summary>
    /// <c>FreeplayDataTracker::ForceUpdate</c> 0x0056EEB8 is <c>SendData</c>, called from the
    /// <c>BehaviorSystemManager</c> destructor (0x005110D4): flush whatever has accumulated.
    /// </summary>
    // fidelity: M15-015
    public void ForceUpdate() { lock (_gate) SendDataCore(_clockSec()); }

    /// <summary>
    /// <c>FreeplayDataTracker::SendData</c> 0x0056EC48: while the pause set is empty, add the running segment
    /// (<c>now - lastTimestamp</c>) to the accumulator. Then <b>only when the accumulator is non-zero</b>
    /// (<c>0x0056EC58/0x0056EC5C</c>): round it, and under 37 s emit <c>robot.active_freeplay_time</c>,
    /// otherwise log <c>DataTooHigh</c>. The accumulator is always zeroed, the last timestamp is stamped
    /// <c>now</c> when unpaused, and the next send is always <c>now + 30.0</c>.
    /// </summary>
    // fidelity: M15-015
    public void SendData(double nowSec) { lock (_gate) SendDataCore(nowSec); }

    private void SendDataCore(double nowSec)
    {
        if (_paused.Count == 0)
        {
            _accumulatedSec += nowSec - _lastResumeSec;
        }
        // 0x0056EC58: the report/error block is guarded by the accumulator being non-zero; the reset and
        // the next-send stamp below still run when it is zero.
        if (_accumulatedSec != 0)
        {
            double seconds = Math.Round(_accumulatedSec, MidpointRounding.AwayFromZero);
            if (seconds < MaxActiveSec)
            {
                Log?.Invoke($"robot.active_freeplay_time {seconds:F0}");
                ActiveFreeplayTime?.Invoke(seconds);
            }
            else
            {
                Log?.Invoke($"error: FreeplayDataTracker.SendData.DataTooHigh {seconds:F0} >= {MaxActiveSec:F0}");
            }
        }
        _accumulatedSec = 0;
        if (_paused.Count == 0) _lastResumeSec = nowSec;
        _nextSendSec = nowSec + ReportIntervalSec;
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