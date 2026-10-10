using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>M4-011, M11-020 and M13-014, against libcozmoEngine.so.</summary>
public class RemainingGapTests
{
    // ------------------------------------------------------------------ M4-011: the advertisement table's order

    /// <summary>
    /// The libc++ unordered_map at Robot+0x47C: 10 then 7 fill a two-bucket table (7 goes in front, its bucket
    /// being empty); 4 grows it to five buckets (2 * 2 | 1, prime), and lands in front; 9 shares 4's bucket and goes
    /// in straight after that bucket's head, which is the list head - so the order is 9, 4, 7, 10.
    /// </summary>
    [Fact]
    public void TheAdvertisementTableIteratesInTheEnginesContainerOrder()
    {
        var t = new ActiveObjectTable<uint>();
        foreach (var k in new uint[] { 10, 7, 4, 9 }) t.Set(k, k);
        Assert.Equal(new uint[] { 9, 4, 7, 10 }, t.Values);
        Assert.True(t.Remove(4));
        Assert.Equal(new uint[] { 9, 7, 10 }, t.Values);
        t.Set(4, 4);
        Assert.Equal(new uint[] { 4, 9, 7, 10 }, t.Values);
    }

    [Fact]
    public void AnRssiTieGoesToTheCubeTheTableVisitsLast()
    {
        var sent = new List<SetPropSlot>();
        float now = 0;
        var c = new CubeConnections(sent.Add, () => now);
        c.OnRobotState(1000);
        // same type, same RSSI: the table order is 0xB1, 0xA0 (0xB1 goes in front of 0xA0's empty bucket), so
        // the last visited - and chosen - is 0xA0
        c.OnObjectAvailable(0xA0, ObjectType.Block_LIGHTCUBE1, 40);
        c.OnObjectAvailable(0xB1, ObjectType.Block_LIGHTCUBE1, 40);
        c.EnableAutoBlockPool(true, 0);
        now = 10;
        c.OnRobotState(1100);
        now = 11;
        c.OnRobotState(1200);
        Assert.Contains(sent, m => m.FactoryId == 0xA0);
        Assert.DoesNotContain(sent, m => m.FactoryId == 0xB1);
        Assert.Contains(c.SlotRequestsSent, m => m.FactoryId == 0xA0);
    }


    // ------------------------------------------------------------------ B-M3M4 build: M4-010 P28 (std::__next_prime)

    /// <summary>
    /// P28 (libc++_shared.so 0x0003B038..0x0003B69F): up to 211 the result is a lower bound in the 48-entry table at 0x0009B480, whose first
    /// entry is 0; above it the smallest prime at or above the input; 0xFFFFFFFC and up raises (0x0003B074). The table and the cases are
    /// the binary's (the table was read from the shipped libc++_shared.so).
    /// </summary>
    [Fact]
    public void M4_010_P28_NextPrimeIsTheSmallestPrimeAtOrAboveTheInputWithTheTablesZero()
    {
        Assert.Equal(new uint[]
        {
            0, 2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37, 41, 43, 47, 53, 59, 61, 67, 71, 73, 79, 83, 89, 97, 101, 103, 107, 109, 113,
            127, 131, 137, 139, 149, 151, 157, 163, 167, 173, 179, 181, 191, 193, 197, 199, 211,
        }, ActiveObjectTable<uint>.SmallPrimes);
        Assert.Equal(0u, ActiveObjectTable<uint>.NextPrime(0));      // the table-defined 0 case
        Assert.Equal(2u, ActiveObjectTable<uint>.NextPrime(1));
        Assert.Equal(2u, ActiveObjectTable<uint>.NextPrime(2));
        Assert.Equal(5u, ActiveObjectTable<uint>.NextPrime(4));
        Assert.Equal(11u, ActiveObjectTable<uint>.NextPrime(8));
        Assert.Equal(13u, ActiveObjectTable<uint>.NextPrime(12));
        Assert.Equal(23u, ActiveObjectTable<uint>.NextPrime(23));     // the engine's 2, 5, 11, 23 bucket counts: 2 | 1 = 5, 2 * 5 | 1 = 11, 2 * 11 | 1 = 23
        Assert.Equal(211u, ActiveObjectTable<uint>.NextPrime(200));
        Assert.Equal(211u, ActiveObjectTable<uint>.NextPrime(211));
        Assert.Equal(223u, ActiveObjectTable<uint>.NextPrime(212));
        Assert.Equal(227u, ActiveObjectTable<uint>.NextPrime(224));
        Assert.Throws<OverflowException>(() => ActiveObjectTable<uint>.NextPrime(0xFFFFFFFCu));
    }

    // ------------------------------------------------------------------ M4-011: the persistent pool

    [Fact]
    public void ThePoolIsSavedAsTheEngineWritesItAndLoadedBackAtInit()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cozmo-blockpool-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(dir, "blockPool.txt");
        try
        {
            var sent = new List<SetPropSlot>();
            float now = 0;
            var c = new CubeConnections(sent.Add, () => now);
            c.Init(path);                                  // nothing saved yet: nothing is asked for
            Assert.False(File.Exists(path));
            c.OnRobotState(1000);
            c.OnObjectAvailable(0xABC123, ObjectType.Block_LIGHTCUBE2, 30);
            c.EnableAutoBlockPool(true, 0);
            now = 10;
            c.OnRobotState(1100);
            Assert.Equal("0xabc123,2\n", File.ReadAllText(path));   // AddObjectToPersistentPool saves (0x0061ACE2)

            // a new session: Init loads the file and asks for the cube before any BlockPoolEnabledMessage
            var sent2 = new List<SetPropSlot>();
            var c2 = new CubeConnections(sent2.Add, () => 0);
            c2.Init(path);
            Assert.Equal((0xABC123u, ObjectType.Block_LIGHTCUBE2), c2.PersistentPool[0]);
            c2.OnRobotState(5000);
            c2.OnObjectAvailable(0xABC123, ObjectType.Block_LIGHTCUBE2, 90);
            c2.OnRobotState(5100);
            Assert.Contains(sent2, m => m.FactoryId == 0xABC123 && m.Slot == 0);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void LoadSkipsWhatTheEngineSkips()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cozmo-blockpool-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "blockPool.txt");
        try
        {
            // an empty line, a line without 0x, a line with nothing after the id, then a good one
            File.WriteAllText(path, "\nabc,1\n0x10\n0x20,3\n");
            var c = new CubeConnections(_ => { }, () => 0);
            c.Init(path);
            Assert.Equal((0x20u, ObjectType.Block_LIGHTCUBE3), c.PersistentPool[0]);
            Assert.Equal(0u, c.PersistentPool[1].FactoryId);
        }
        finally { Directory.Delete(dir, true); }
    }

    // ------------------------------------------------------------------ M11-020

    [Fact]
    public void IlluminationNormalisationStretchesTheRegionAndIsPutBack()
    {
        var img = new GrayImage(60, 50);
        for (int y = 0; y < 50; y++) for (int x = 0; x < 60; x++) img[x, y] = (byte)(100 + (x + y) % 7);
        var before = (byte[])img.Pixels.Clone();
        var corners = new[] { new Vec2(20, 15), new Vec2(20, 35), new Vec2(40, 15), new Vec2(40, 35) };
        var region = CornerRefinement.NormalizeIllumination(img, corners, new QuadDetectorParameters());
        Assert.NotNull(region);
        var r = region!.Value;
        // the bounding rectangle grown by 5: x 15..45, y 10..40, as Rect(15, 10, 30, 30)
        Assert.Equal((15, 10, 30, 30), (r.X, r.Y, r.W, r.H));
        byte min = 255, max = 0;
        for (int y = r.Y; y < r.Y + r.H; y++) for (int x = r.X; x < r.X + r.W; x++) { min = Math.Min(min, img[x, y]); max = Math.Max(max, img[x, y]); }
        Assert.Equal((byte)0, min);
        Assert.Equal((byte)255, max);
        Assert.Equal(before[0], img[0, 0]);                  // outside the region nothing changes
        CornerRefinement.RestoreRegion(img, r);
        Assert.Equal(before, img.Pixels);
    }

    // ------------------------------------------------------------------ M13-014

    [Fact]
    public void TheNoPreDockPosesReactionConsumesTheWhiteboardEntryAndTargetsRamIntoBlock()
    {
        using var rig = new Rig();
        var ram = new RamIntoBlockBehavior(rig.M);
        var strategy = new NoPreDockPosesStrategy(rig.M.Whiteboard, ram);
        var ctx = new BehaviorContext { Robot = rig.Robot, Triggers = new AnimationTriggerMap() };
        Assert.False(strategy.ShouldTrigger(ctx, null, 0, M10Support.RunnableBehaviour()));
        rig.M.Whiteboard.NoPreDockPosesObjectId = 7;
        Assert.True(strategy.ShouldTrigger(ctx, null, 0, M10Support.RunnableBehaviour()));
        Assert.Equal(7u, ram.PendingTarget);
        // consumed whether the behaviour then runs or not: the reset precedes IsRunnable (0x00610E60)
        Assert.Null(rig.M.Whiteboard.NoPreDockPosesObjectId);
    }
}
