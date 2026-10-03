using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// The idle face as the streamer builds it (R-BEH2 batch 2: M7-004, M7-005, M7-006, M7-007, M7-016). The retired IdleBehavior's
/// own blink, dart and render path is gone; the face is <c>FaceLayerManager::KeepFaceAlive</c> (0x0058D374) layers composed per
/// streamed frame by <c>TrackLayerComponent::ApplyLayersToAnim</c>. Every float is a bit pattern read from the binary (the check-2
/// report <c>re-analysis/research/20261002-R-BEH2-check2-M7-idle-rows.md</c>, sections 1.1, 1.2, 1.6 and C) or from an
/// independent float32 emulation of the disassembled instruction sequence of <c>ProceduralFace::LookAt</c> (0x00584158).
/// </summary>
public class IdleFaceTests
{
    private static float F(uint bits) => BitConverter.Int32BitsToSingle(unchecked((int)bits));
    private static uint Bits(float f) => unchecked((uint)BitConverter.SingleToInt32Bits(f));

    private static readonly float Up = F(0x3F8CCCCD), Down = F(0x3F59999A), Outer = F(0x3DCCCCCD);   // 1.1f, 0.85f, 0.1f (0x0057DCC.., params 28, 29, 27)

    // ---------------------------------------------------------------- M7-006: LookAt, bit for bit

    /// <summary>
    /// (x, y, xMax, yMax) and the engine's results as float32 bit patterns: the face centre after SetFacePosition's clamp
    /// (0x00583B20..0x00583BF8), the two EyeScaleY (0x00584158..0x00584236) and the two EyeCenterX (0x00584244..0x0058427E; the
    /// right one is the left's negation, so -0.0f = 0x80000000 when y &lt;= 0).
    /// The first eight are the dart (xMax = yMax = 5, GenerateEyeShift(map) 0x0058D1EA), the rest the turn shift after
    /// GenerateEyeShift 0x0058CFC4 replaced the limits with 17 and 12. Expected values come from emulating, in float32 and in
    /// the engine's order, yf = (y + yMax) / (yMax * -2.0f) + 1.0f; xf = |x| / xMax; sY = (up - down) * min(yf, 1.0f) + down;
    /// a = min(xf, 1.0f) * outer + 1.0f; x &lt; 0: left = a * sY, right = sY * (2.0f - a); else left = (2.0f - a) * sY, right = sY * a.
    /// Four of the thirteen differ in the last bits from the algebraically equal (1 - o*xf)*sY the C# used to compute.
    /// </summary>
    public static IEnumerable<object[]> LookAtCases() => new[]
    {
        new object[] { -6f, 6f, 5f, 5f,   0xC0C00000u, 0x40C00000u, 0x3F6851EDu, 0x3F3E147Bu, 0x40000000u, 0xC0000000u },
        new object[] { 5f, -3f, 5f, 5f,   0x40A00000u, 0xC0400000u, 0x3F71EB86u, 0x3F93D70Bu, 0x00000000u, 0x80000000u },
        new object[] { 3f, 2f, 5f, 5f,    0x40400000u, 0x40000000u, 0x3F5E978Eu, 0x3F7B020Cu, 0x3F4CCCCDu, 0xBF4CCCCDu },
        new object[] { -2f, -6f, 5f, 5f,  0xC0000000u, 0xC0C00000u, 0x3F926E97u, 0x3F872B03u, 0x00000000u, 0x80000000u },
        new object[] { 0f, 0f, 5f, 5f,    0x00000000u, 0x00000000u, 0x3F79999Au, 0x3F79999Au, 0x00000000u, 0x80000000u },
        new object[] { 6f, -6f, 5f, 5f,   0x40C00000u, 0xC0C00000u, 0x3F7D70A4u, 0x3F9AE148u, 0x00000000u, 0x80000000u },
        new object[] { -4f, 5f, 5f, 5f,   0xC0800000u, 0x40A00000u, 0x3F6B020Du, 0x3F483127u, 0x40000000u, 0xC0000000u },
        new object[] { 1f, 4f, 5f, 5f,    0x3F800000u, 0x40800000u, 0x3F5B851Fu, 0x3F647AE1u, 0x3FCCCCCDu, 0xBFCCCCCDu },
        new object[] { 21f, -10f, 17f, 12f, 0x41880000u, 0xC1200000u, 0x3F78A3D6u, 0x3F97F259u, 0x00000000u, 0x80000000u },
        new object[] { -19f, 7f, 17f, 12f,  0xC1880000u, 0x40E00000u, 0x3F7E06D4u, 0x3F4FD70Au, 0x3F955555u, 0xBF955555u },
        new object[] { 8f, 0f, 17f, 12f,    0x41000000u, 0x00000000u, 0x3F6DDAA8u, 0x3F82AC46u, 0x00000000u, 0x80000000u },
        new object[] { -3f, 10f, 17f, 12f,  0xC0400000u, 0x41200000u, 0x3F62DE11u, 0x3F5AFFCDu, 0x3FD55555u, 0xBFD55555u },
        new object[] { 17f, 3f, 17f, 12f,   0x41880000u, 0x40400000u, 0x3F5970A4u, 0x3F84E148u, 0x3F000000u, 0xBF000000u },
    };

    [Theory]
    [MemberData(nameof(LookAtCases))]
    public void M7_006_LookAtIsTheEnginesFloat32SequenceBitForBit(float x, float y, float xMax, float yMax,
        uint faceX, uint faceY, uint leftScaleY, uint rightScaleY, uint leftCenterX, uint rightCenterX)
    {
        var face = new ProceduralFacePose();
        face.LookAt(x, y, xMax, yMax, Up, Down, Outer);
        Assert.Equal(faceX, Bits(face.FaceCenterX));
        Assert.Equal(faceY, Bits(face.FaceCenterY));
        Assert.Equal(leftScaleY, Bits(face.Left[EyeParam.EyeScaleY]));
        Assert.Equal(rightScaleY, Bits(face.Right[EyeParam.EyeScaleY]));
        Assert.Equal(leftCenterX, Bits(face.Left[EyeParam.EyeCenterX]));
        Assert.Equal(rightCenterX, Bits(face.Right[EyeParam.EyeCenterX]));
        Assert.Equal(0x3F800000u, Bits(face.Left[EyeParam.EyeScaleX]));        // EyeScaleX is untouched
    }

    /// <summary>
    /// M7-006 / M7-010: GenerateEyeShift (0x0058CFC4) stores the caller's xMax into the bounding-box output slot and its
    /// yMax stack slot is the box output too (0x0058CFCE, 0x0058CFDA): the 64.0 / 32.0 the live idle passes are overwritten and the
    /// limits are max(xmin, 128 - xmax) = 17 and max(ymin, 64 - ymax) = 12. So the keyframe is the same face LookAt makes with 17
    /// and 12, bit for bit, whatever limits the caller names.
    /// </summary>
    [Theory]
    [InlineData(21f, -10f, 0x41880000u, 0xC1200000u, 0x3F78A3D6u, 0x3F97F259u)]
    [InlineData(-19f, 7f, 0xC1880000u, 0x40E00000u, 0x3F7E06D4u, 0x3F4FD70Au)]
    [InlineData(8f, 0f, 0x41000000u, 0x00000000u, 0x3F6DDAA8u, 0x3F82AC46u)]
    public void M7_006_GenerateEyeShiftDiscardsTheCallersLimitsAndUsesSeventeenAndTwelve(
        float x, float y, uint faceX, uint faceY, uint left, uint right)
    {
        foreach (var (xm, ym) in new[] { (64f, 32f), (5f, 5f), (1000f, 1000f) })
        {
            var kf = FaceLayerManager.GenerateEyeShift(x, y, xm, ym, Up, Down, Outer, 33);
            Assert.Equal(33u, kf.Trigger);
            Assert.Equal(faceX, Bits(kf.Face.FaceCenterX));
            Assert.Equal(faceY, Bits(kf.Face.FaceCenterY));
            Assert.Equal(left, Bits(kf.Face.Left[EyeParam.EyeScaleY]));
            Assert.Equal(right, Bits(kf.Face.Right[EyeParam.EyeScaleY]));
        }
    }

    /// <summary>
    /// M7-006: SetFacePosition (0x00583B20..0x00583BF8) clamps as <c>m = (v &gt; lo) ? v : lo; result = (m &lt; hi) ? m : hi</c>
    /// with lo = -min and hi = 128 - max (64 - max for y): <c>min(max(v, lo), hi)</c>, not <c>max(lo, min(hi, v))</c>. The two
    /// differ when lo &gt; hi, which a face whose eyes are scaled by 3 gives: xmin = 32 + (0 - 15 x 3) = -13 and xmax = 96 +
    /// 45 = 141 so lo = 13 and hi = -13; the engine returns hi, -13.0f (0xC1500000). The y axis likewise: ymin = 32 - 60 = -28,
    /// ymax = 32 + 60 = 92, lo = 28, hi = -28 -> -28.0f (0xC1E00000). A NaN gives lo (the compare is false):
    /// for a default face -17.0f (0xC1880000).
    /// </summary>
    [Fact]
    public void M7_006_SetFacePositionClampsInTheEnginesCompareOrder()
    {
        var big = new ProceduralFacePose();
        foreach (var eye in new[] { big.Left, big.Right }) { eye[EyeParam.EyeScaleX] = 3f; eye[EyeParam.EyeScaleY] = 3f; }
        big.SetFacePosition(0f, 0f);
        Assert.Equal(0xC1500000u, Bits(big.FaceCenterX));
        Assert.Equal(0xC1E00000u, Bits(big.FaceCenterY));

        var plain = new ProceduralFacePose();
        plain.SetFacePosition(float.NaN, float.NaN);
        Assert.Equal(0xC1880000u, Bits(plain.FaceCenterX));        // lo = -17 (xmin 17 = 32 + (0 - 15))
        Assert.Equal(0xC1400000u, Bits(plain.FaceCenterY));        // lo = -12 (ymin 12 = 32 - 20)
        plain.SetFacePosition(100f, -100f);
        Assert.Equal(0x41880000u, Bits(plain.FaceCenterX));        // hi = 128 - 111 = 17
        Assert.Equal(0xC1400000u, Bits(plain.FaceCenterY));
        plain.SetFacePosition(5f, 6f);
        Assert.Equal(0x40A00000u, Bits(plain.FaceCenterX));
        Assert.Equal(0x40C00000u, Bits(plain.FaceCenterY));
    }

    // ---------------------------------------------------------------- M7-005: the blink

    /// <summary>
    /// M7-005: the blink table at 0x00C5AAD8, 7 x 16 bytes {f32 heightMul, f32 widthMul, u32 duration, u32 action}, as bit patterns
    /// (check 2 section C): (0x3F59999A, 0x3F866666, 33, 0), (0x3F19999A, 0x3F99999A, 33, 0), (0x3DCCCCCD, 0x40200000, 33, 0),
    /// (0x3D4CCCCD, 0x40A00000, 33, 1), (0x3E19999A, 0x40000000, 33, 2), (0x3F333333, 0x3F99999A, 33, 3),
    /// (0x3F666666, 0x3F800000, 100, 3).
    /// </summary>
    [Fact]
    public void M7_005_TheBlinkTableIsTheEnginesBitForBit()
    {
        var expected = new (uint H, uint W, int Dur, byte Action)[]
        {
            (0x3F59999A, 0x3F866666, 33, 0), (0x3F19999A, 0x3F99999A, 33, 0), (0x3DCCCCCD, 0x40200000, 33, 0),
            (0x3D4CCCCD, 0x40A00000, 33, 1), (0x3E19999A, 0x40000000, 33, 2), (0x3F333333, 0x3F99999A, 33, 3),
            (0x3F666666, 0x3F800000, 100, 3),
        };
        Assert.Equal(expected.Length, FaceLayerManager.BlinkTable.Length);
        for (int i = 0; i < expected.Length; i++)
        {
            var e = FaceLayerManager.BlinkTable[i];
            Assert.Equal(expected[i].H, Bits(e.HeightMul));
            Assert.Equal(expected[i].W, Bits(e.WidthMul));
            Assert.Equal(expected[i].Dur, e.DurationMs);
            Assert.Equal(expected[i].Action, e.Action);
        }
    }

    /// <summary>
    /// M7-005: GenerateBlink (0x0058D2AC) accumulates the duration before each AddKeyFrameToBackHelper (0x0058D2FA..0x0058D312) and
    /// the frame past the table is the original face with duration 33 (0x00586190): eight keyframes at 33, 66, 99, 132, 165, 198,
    /// 298, 331; each table frame scales the original eyes' EyeScaleX by the widthMul and EyeScaleY by the heightMul (the drawer
    /// uses [entry+4] on X and [entry+0] on Y, 0x005860C4..0x005860FC); the action-1 frame flips the scanline byte
    /// (<c>rsb.w r1,r1,#1</c> at 0x005861C4) and zeroes the six lid parameters; the last is the original face again.
    /// </summary>
    [Fact]
    public void M7_005_GenerateBlinkIsAnEightKeyframeTrackWithTheEnginesTimesAndScales()
    {
        var scan = new ScanLineState();
        var mgr = new FaceLayerManager(new EngineRandom(1u), scan);
        int before = scan.Drawer;
        var track = mgr.GenerateBlink();
        Assert.NotEqual(before, scan.Drawer);                          // flipped once, by the action-1 frame
        Assert.Equal(1 - before, scan.Drawer);
        Assert.Equal(new uint[] { 33, 66, 99, 132, 165, 198, 298, 331 }, track.Frames.Select(f => f.Trigger).ToArray());

        uint[] h = { 0x3F59999A, 0x3F19999A, 0x3DCCCCCD, 0x3D4CCCCD, 0x3E19999A, 0x3F333333, 0x3F666666 };
        uint[] w = { 0x3F866666, 0x3F99999A, 0x40200000, 0x40A00000, 0x40000000, 0x3F99999A, 0x3F800000 };
        for (int i = 0; i < 7; i++)
        {
            var face = track.Frames[i].Face;
            Assert.Equal(w[i], Bits(face.Left[EyeParam.EyeScaleX]));
            Assert.Equal(h[i], Bits(face.Left[EyeParam.EyeScaleY]));
            Assert.Equal(w[i], Bits(face.Right[EyeParam.EyeScaleX]));
            Assert.Equal(h[i], Bits(face.Right[EyeParam.EyeScaleY]));
        }
        var closed = track.Frames[3].Face;                             // action 1: the six lid parameters zeroed
        foreach (var p in new[] { EyeParam.LowerLidY, EyeParam.LowerLidBend, EyeParam.LowerLidAngle,
                                  EyeParam.UpperLidY, EyeParam.UpperLidBend, EyeParam.UpperLidAngle })
            Assert.Equal(0u, Bits(closed.Left[p]));
        var last = track.Frames[7].Face;
        Assert.Equal(0x3F800000u, Bits(last.Left[EyeParam.EyeScaleX]));
        Assert.Equal(0x3F800000u, Bits(last.Left[EyeParam.EyeScaleY]));
    }

    /// <summary>
    /// M7-005: the blink-spacing fallback (0x0058D4DC..0x0058D550): when the maximum is not above the minimum
    /// (<c>cmp r3,r1; bgt</c>), a warning and RandIntInRange(7500, 30000) (<c>movw r1,#0x1d4c</c>, <c>movw r3,#0x7530</c>) is
    /// the next spacing. The first keep-alive tick blinks at once (the timer starts at 0 and loses 60 before the test, 0x0058D386).
    /// </summary>
    [Fact]
    public void M7_005_AMaximumNotAboveTheMinimumFallsBackToSevenThousandFiveHundredToThirtyThousand()
    {
        var mgr = new FaceLayerManager(new EngineRandom(7u), new ScanLineState());
        var log = new List<string>();
        mgr.Log = log.Add;
        var p = new LiveIdleParams();
        p.SetDefaultParams();
        p[LiveIdleParam.BlinkSpacingMinTime_ms] = 5000;
        p[LiveIdleParam.BlinkSpacingMaxTime_ms] = 5000;
        mgr.KeepFaceAlive(p);
        Assert.Contains(log, l => l.Contains("BadBlinkSpacingParams"));
        Assert.InRange(mgr.BlinkTimerMs, 7500, 30000);
        Assert.Contains(mgr.AllLayers, l => l.Name == "Blink");
    }

    /// <summary>
    /// M7-005 / M7-016: through the live entry. The engine's blink timer loses 60 per Update (0x0058D386) and is redrawn from
    /// RandIntInRange(p0, p1) = 3000..4000 at each blink (defaults 0x453B8000 / 0x457A0000), so successive blinks are a whole
    /// number of 60 ms Updates apart, between 3000 and 4000 plus up to one tick (4020).
    /// </summary>
    [Fact]
    public void M7_005_LiveBlinksComeEverySixtyMillisecondMultipleBetweenThreeAndFourSeconds()
    {
        var s = new AnimationScheduler(new NullSink(), new Random(11));
        s.PushIdleAnimation(AnimationTrigger.ProceduralLive, "test");
        var seen = new HashSet<byte>();
        var starts = new List<int>();
        for (int i = 0; i < 700; i++)
        {
            s.Advance(60.0 * i);
            foreach (var l in s.Layers.Face.AllLayers)
                if (l.Name == "Blink" && seen.Add(l.Tag)) starts.Add(i);
        }
        Assert.True(starts.Count >= 8, $"only {starts.Count} blinks");
        Assert.Equal(18, starts[0]);                                 // the first keep-alive Update (the 19th: see KeepAliveTests)
        for (int i = 1; i < starts.Count; i++)
            Assert.InRange((starts[i] - starts[i - 1]) * 60, 3000, 4020);
    }

    // ---------------------------------------------------------------- M7-007: the persistent dart layer

    /// <summary>
    /// M7-007: the eye dart is ONE persistent layer "KeepAliveEyeDart" (AddPersistentLayer, name at 0x0058D5E4): the first dart's
    /// track holds only its keyframe (no neutral keyframe at 0, so it jumps) with trigger = the drawn duration (50..200,
    /// params 25, 26); every later dart is added to the same layer with trigger = drawn + the last trigger + 33
    /// (<c>AddToPersistentLayer</c> 0x0058EAA0) - and the layer is never removed, however long the run (ApplyLayersToFrame
    /// 0x0058E644 holds the last face of a persistent layer).
    /// </summary>
    [Fact]
    public void M7_007_TheDartIsOnePersistentLayerWhoseKeyframesChainByDurationPlusThirtyThree()
    {
        var s = new AnimationScheduler(new NullSink(), new Random(3));
        // the dart gate wants no other layer (FaceLayerManager::KeepFaceAlive, [+0xc] == 0 or only its own: 0x0058D3AC..0x0058D3C4),
        // so the tracks are locked to keep the live idle's own "LiveIdleTurn" layer out; a long body keyframe (speed 0) keeps the live
        // animation streaming frames (a keep-alive layer is applied only on a built frame, and an ended live idle is re-initialised
        // instead of streamed), which starts the keep-alive block (+0x88 > 0) and lets the layers run
        s.PushLiveQuietly();
        s.StreamLive(new BodyKeyframe(0, 1_000_000, "STRAIGHT", 0), 0);
        TrackLayer<FaceFrame>? dart = null;
        bool chained = false;
        for (int i = 0; i < 400; i++)
        {
            s.Advance(60.0 * i);
            var layers = s.Layers.Face.AllLayers.Where(l => l.Name == "KeepAliveEyeDart").ToList();
            if (i < 2) { Assert.Empty(layers); continue; }
            Assert.True(layers.Count == 1, $"tick {i}: {string.Join(',', s.FaceLayerNames)}");
            var one = Assert.Single(layers);                         // 400 Updates: the layer is never removed nor duplicated
            if (dart is null)
            {
                dart = one;
                var first = Assert.Single(one.Track.Frames);
                Assert.InRange(first.Trigger, 50u, 200u);
            }
            Assert.Same(dart, one);
            Assert.True(one.IsPersistent);
            var f = one.Track.Frames;
                        for (int k = 1; k < f.Count; k++)
            {
                chained = true;           // AddToPersistentLayer 0x0058EAA0: trigger = drawn duration (50..200) + the last trigger + 33
                Assert.InRange((long)f[k].Trigger - f[k - 1].Trigger - 33, 50, 200);
            }
        }
        Assert.True(chained, "no second dart keyframe was ever seen in the layer");
    }

    /// <summary>
    /// M7-007: the dart gate is <c>at(0x16) &gt; 0</c> (EyeDartMaxDistance, 0x0058D3A?): at 0 no dart layer is ever made, but the
    /// blink still is.
    /// </summary>
    [Fact]
    public void M7_007_ADartDistanceOfZeroMakesNoDartLayer()
    {
        var s = new AnimationScheduler(new NullSink(), new Random(3));
        s.PushLiveQuietly();
        s.StreamLive(new BodyKeyframe(0, 1_000_000, "STRAIGHT", 0), 0);
        s.LiveIdleParameters[LiveIdleParam.EyeDartMaxDistance_pix] = 0;
        bool blink = false;
        for (int i = 0; i < 100; i++)
        {
            s.Advance(60.0 * i);
            Assert.DoesNotContain("KeepAliveEyeDart", s.FaceLayerNames);
            blink |= s.FaceLayerNames.Contains("Blink");
        }
        Assert.True(blink);
    }

    // ---------------------------------------------------------------- M7-016: layer composition

    /// <summary>
    /// M7-016: ApplyLayersToFrame (0x0058E644) applies each layer then adds 33 to its clock (<c>add r1,r2,#0x21; str r1,[r8,#0x2c]</c>
    /// at 0x0058E71A..0x0058E724), once per streamed frame. A "Blink" layer added at start 0 therefore has stream clock 33 x n
    /// after n frames (until its track is at its end and it is removed: eight keyframes, the last at 331).
    /// </summary>
    [Fact]
    public void M7_016_EachStreamedFrameAdds33ToTheLayerClock()
    {
        var tlc = new TrackLayerComponent(new EngineRandom(2u), new ScanLineState());
        var track = new StreamTrack<FaceFrame>();
        track.AddKeyFrameToBack(FaceFrame.Default(0));
        track.AddKeyFrameToBack(FaceFrame.Default(33_000));
        tlc.Face.AddLayer("L", track, 0);
        var layer = Assert.Single(tlc.Face.AllLayers);
        for (int n = 1; n <= 5; n++)
        {
            tlc.ApplyLayersToAnim(null, 0, 0, null, storeFace: false);
            Assert.Equal(33 * n, layer.StreamTime);
        }
    }

    // ---------------------------------------------------------------- M7-004: the defaults

    /// <summary>
    /// M7-004: SetDefaultParams (0x0057DB40..0x0057DCD6), all thirty values as the bit patterns read from the movw/movt/vmov
    /// sequences (check 2 section C).
    /// </summary>
    [Fact]
    public void M7_004_TheThirtyDefaultsAreTheEnginesBitPatterns()
    {
        uint[] bits =
        {
            0x453B8000, 0x457A0000, 0x447A0000, 0x42C80000, 0x447A0000, 0x437A0000, 0x44BB8000, 0x41200000, 0x3F000000, 0x42480000,
            0x43FA0000, 0x437A0000, 0x44FA0000, 0x420C0000, 0x41000000, 0x42480000, 0x43FA0000, 0x437A0000, 0x447A0000, 0x40C00000,
            0x437A0000, 0x447A0000, 0x40C00000, 0x3F6B851F, 0x3F8A3D71, 0x42480000, 0x43480000, 0x3DCCCCCD, 0x3F8CCCCD, 0x3F59999A,
        };
        var p = new LiveIdleParams();
        p.SetDefaultParams();
        Assert.Equal(30, (int)LiveIdleParam.NumParameters);
        for (int i = 0; i < 30; i++) Assert.Equal(bits[i], Bits(p[(LiveIdleParam)i]));
    }

    private sealed class NullSink : IAnimationSink
    {
        public void Face(FaceBitmap bitmap) { }
        public void Audio(byte[]? mulawFrame) { }
        public void Head(sbyte angleDeg, uint durationMs) { }
        public void Lift(byte heightMm, uint durationMs) { }
        public void Body(BodyKeyframe k) { }
        public void AnimationStarted(byte tag) { }
        public void AnimationEnded() { }
        public void BodyStop() { }
        public void Lights(LightsKeyframe k) { }
        public void Event(string eventId) { }
        public void Finished(string clipName, bool completed) { }
    }
}
