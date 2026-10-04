using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// R-FIX3 round 2, stream Z: the NeedsManager's two clocks (M15-014). Expected values come from the instructions cited on each test in
/// libcozmoEngine.so, never from the implementation's own output.
/// </summary>
public class RFix3ZTests
{
    /// <summary>An epoch-scale system_clock reading in seconds (about 2026); the stopwatch tick clock of a tool is a few seconds.</summary>
    private const double D = 1_790_000_000;

    private static string TempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cozmo-rfix3z-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>
    /// M15-014 (0x00695DC4..0x00695DFA): PossiblyWriteToDevice reads system_clock::now() (0x00695DCC), subtracts the state's DateTime
    /// at +8/+0xC (0x00695DE4), compares with 0x03A2C940 = 61,000,000 microseconds (0x00695DD2/0x00695DDA, 0x00695DEA/0x00695DEC) and
    /// returns on a smaller difference (blt 0x00695DFE); otherwise it stores now into +8 (strd 0x00695DF2) and calls WriteToDevice(false).
    /// WriteToDevice(true) stores system_clock::now() into +8 too (0x00693BC4..0x00693BCC). So a forced write resets the anchor, and the
    /// tick clock (a stopwatch here) takes no part: 3 s on the tick clock must not stand in for the epoch DateTime.
    /// </summary>
    [Fact]
    public void M15_014_TheThrottleAnchorIsTheSystemClockAndAForcedWriteResetsIt()
    {
        var dir = TempDir();
        try
        {
            double tick = 0.5, date = D;
            var needs = new NeedsManager(() => tick, dateClockSec: () => date) { DeviceDirectory = dir };
            int writes = 0;
            var forcedFlags = new List<bool>();
            needs.WriteToDevice = forced => { writes++; forcedFlags.Add(forced); needs.WriteDeviceFile(forced); };

            needs.WriteToDevice(true);                                   // +8 := now (0x00693BC4)
            Assert.Equal(1, writes);
            date = D + 60.999; needs.PossiblyWriteToDevice();
            Assert.Equal(1, writes);                                     // 60.999 s < 61 s: blt
            date = D + 61; needs.PossiblyWriteToDevice();
            Assert.Equal(2, writes);                                     // exactly 61,000,000 us: not less, so it writes
            Assert.False(forcedFlags[1]);                                // WriteToDevice(false) (0x00695DF8)
            date = D + 61 + 60; needs.PossiblyWriteToDevice();
            Assert.Equal(2, writes);                                     // the anchor moved to the write time (strd 0x00695DF2)
            tick = 3.0; date = D + 61 + 61; needs.PossiblyWriteToDevice();
            Assert.Equal(3, writes);                                     // the tick clock is irrelevant
        }
        finally { Directory.Delete(dir, true); }
    }

    /// <summary>
    /// M15-014: a successful device read stores the file's _DateTime into +8/+0xC (AttemptReadFromDevice 0x006936B0..0x006936B6, ReadFromDevice
    /// 0x006998B4..0x00699BB8), so the throttle runs from that DateTime, not from the load time. With the file stamped D and the system
    /// clock at D + 30 s at load, the next write is due at D + 61 s.
    /// </summary>
    [Fact]
    public void M15_014_ALoadAnchorsTheThrottleAtTheFilesDateTime()
    {
        var dir = TempDir();
        try
        {
            var path = Path.Combine(dir, NeedsManager.FixedFileName);
            new NeedsManager(() => 0).Save(path, unixTimeSec: (long)D, serialNumber: 1);

            double date = D + 30;
            var needs = new NeedsManager(() => 0.25, dateClockSec: () => date);
            int writes = 0;
            needs.WriteToDevice = _ => writes++;
            Assert.True(needs.Load(path));
            date = D + 60.999; needs.PossiblyWriteToDevice();
            Assert.Equal(0, writes);                                     // 60.999 s after the file's DateTime
            date = D + 61; needs.PossiblyWriteToDevice();
            Assert.Equal(1, writes);
        }
        finally { Directory.Delete(dir, true); }
    }

    /// <summary>
    /// M15-014 / M1-024 through the engine's entry, NeedsManager::Update(float now) 0x00695C9C (CozmoEngine's NeedsUpdate hook): on a decay tick
    /// it ends with PossiblyWriteToDevice (0x00695CE4..tail). The decay accumulator runs on the tick clock (+0x3AC, the float the engine
    /// passes, 0x004ED632..0x004ED640) while the throttle runs on system_clock::now(), so ticks 10,000 s apart on the tick clock write only when
    /// 61 s have passed on the system clock.
    /// </summary>
    [Fact]
    public void M15_014_TheDecayTickWritesOnlyWhenTheSystemClockHasPassedTheThrottle()
    {
        double date = D;
        var needs = new NeedsManager(() => 0, dateClockSec: () => date);
        int writes = 0;
        needs.WriteToDevice = forced => { Assert.False(forced); writes++; };
        // the anchor: the state's DateTime is 0 until a write stamps it, so a first Possibly write would be due at once; pin it with a forced refresh
        var dir = TempDir();
        try
        {
            needs.DeviceDirectory = dir;
            needs.WriteDeviceFile(true);                                 // +8 := D
            date = D + 30; needs.Update(10_000f);
            Assert.Equal(0, writes);                                     // a decay tick, but 30 s on the system clock
            date = D + 61; needs.Update(20_000f);
            Assert.Equal(1, writes);                                     // 61 s
            date = D + 62; needs.Update(30_000f);
            Assert.Equal(1, writes);                                     // 1 s after the write
        }
        finally { Directory.Delete(dir, true); }
    }

    /// <summary>
    /// M15-014 (ApplyDecayForTimeSinceLastDeviceWrite 0x00695304..0x00695374): elapsed = system_clock::now() - DateTime computed in 64-bit
    /// microseconds, divided by 1,000,000 with __aeabi_ldivmod (0x0069530E..0x0069532C: whole seconds, truncated) and converted with __aeabi_l2f
    /// (0x00695330), so an epoch-scale clock loses nothing: 150.9 s since the write is 150 s, and the Play need decays 0.1 per minute over 150 s
    /// (0.1 * 150 / 60 = 0.25). A float subtraction of two epoch-scale seconds would have a granularity of 128 s.
    /// </summary>
    [Fact]
    public void M15_014_TheElapsedSecondsAreWholeSecondsOfTheSystemClockDifference()
    {
        var rates = new Dictionary<NeedId, IReadOnlyList<(double, double)>>
        { [NeedId.Play] = new[] { (0.0, 0.1) }, [NeedId.Repair] = new[] { (0.0, 0.0) }, [NeedId.Energy] = new[] { (0.0, 0.0) } };
        var decay = new DecayConfig(rates, rates);
        var dir = TempDir();
        try
        {
            double date = D;
            var needs = new NeedsManager(() => 0, NeedsConfig.Default, decay, dateClockSec: () => date) { DeviceDirectory = dir };
            needs.SetLevel(NeedId.Play, 1.0);
            needs.WriteDeviceFile(true);                                 // DateTime := D
            date = D + 150.9;
            needs.ApplyDecayForTimeSinceLastDeviceWrite(false);
            Assert.Equal(0.75, needs.State.GetNeedLevel(NeedId.Play), 5);
        }
        finally { Directory.Delete(dir, true); }
    }
}
