using System.Text.RegularExpressions;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Vision;
using Cozmo.Transport;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M11: marker detection, the nearest-neighbour decoder over the engine's own library, camera and cube
/// geometry, pose estimation, the world model's located/visible semantics and the real cube locator under the
/// M10 cube-moved reaction. Synthetic frames are rendered from the extracted library images, so every test is
/// offline; the hardware-facing pieces (NV calibration, SetBodyAngle) are exercised at the message level.
/// </summary>
public class VisionTests
{
    /// <summary>
    /// The marker library is Anki's data, extracted locally from the user's libcozmoEngine.so by the build;
    /// without it the tests that need it return early, like the OBB-dependent tests do.
    /// </summary>
    private static readonly MarkerLibrary? LibOrNull = MarkerLibrary.EmbeddedOrNull;
    private static MarkerLibrary Lib => LibOrNull!;
    private static bool NoLibrary => LibOrNull is null;

    private static double Deg(double rad) => rad * 180 / Math.PI;

    // ------------------------------------------------------------------ library

    [Fact]
    public void TheEmbeddedLibraryHasTheEnginesShape()
    {
        if (NoLibrary) return;
        Assert.Equal(598, Lib.NumImages);
        Assert.Equal(150, Lib.NumLabels);
        Assert.Equal(598 * 1024, Lib.Images.Length);
        // every label but INVALID has four images; INVALID has two
        for (int l = 0; l < Lib.NumLabels; l++)
            Assert.Equal(l == MarkerLibrary.InvalidLabel ? 2 : 4, Lib.RowsForLabel(l).Count());
        // every cube marker code has four labels, one per rotation
        foreach (var t in new[] { ObjectType.Block_LIGHTCUBE1, ObjectType.Block_LIGHTCUBE2, ObjectType.Block_LIGHTCUBE3 })
            foreach (var m in CubeGeometry.CubeMarkers(t))
            {
                var labels = Lib.LabelsForCode(m.Code).ToList();
                Assert.Equal(4, labels.Count);
                Assert.Equal(new[] { 0f, 90f, 180f, 270f }, labels.Select(l => Lib.OrientationDeg[l]).OrderBy(x => x));
            }
        // the probe grid is 32 equally spaced columns and rows inside the square
        Assert.Equal(7149, Lib.ProbeCentersX[0]);
        Assert.Equal(25619, Lib.ProbeCentersY[31]);
        Assert.Equal(new short[] { 0, 205, 0, -205, 0 }, Lib.ProbePointsX);
        Assert.Equal(new short[] { 0, 0, 205, 0, -205 }, Lib.ProbePointsY);
    }

    [Fact]
    public void TheLibraryFileMatchesTheToolsLayout()
    {
        // the committed blob is what the extraction tool writes; the loader must reject anything else
        Assert.Throws<FormatException>(() => MarkerLibrary.Parse(new byte[32]));
    }

    // ------------------------------------------------------------------ decoder

    [Theory]
    [InlineData(MarkerType.LightCubeI_Front)]
    [InlineData(MarkerType.LightCubeJ_Top)]
    [InlineData(MarkerType.LightCubeK_Left)]
    [InlineData(MarkerType.Charger)]
    [InlineData(MarkerType.Star5)]
    [InlineData(MarkerType.Sdk3Hexagons)]
    public void ARenderedMarkerDecodesToItsOwnCode(MarkerType code)
    {
        if (NoLibrary) return;
        foreach (var label in Lib.LabelsForCode(code))
        {
            int row = Lib.RowsForLabel(label).First();
            var img = MarkerRenderer.Render(Lib, row, 160);
            var corners = new[] { new Vec2(0, 0), new Vec2(0, 160), new Vec2(160, 0), new Vec2(160, 160) };
            var dec = new MarkerDecoder(Lib);
            var m = dec.Extract(img, corners, 1, out var reason);
            Assert.True(m is not null, $"label {label}: {reason}");
            Assert.Equal(code, m!.Code);
            Assert.Equal(Lib.OrientationDeg[label], m.OrientationDeg);
            Assert.True(m.Distance < 20, $"distance {m.Distance}");
        }
    }

    [Fact]
    public void EveryLibraryRowDecodesToItsLabel()
    {
        if (NoLibrary) return;
        var dec = new MarkerDecoder(Lib);
        int wrong = 0;
        for (int row = 0; row < Lib.NumImages; row++)
        {
            var img = MarkerRenderer.Render(Lib, row, 96);
            var probes = dec.GetProbeValues(img, Homography.FromUnitSquare(new[] { new Vec2(0, 0), new Vec2(0, 96), new Vec2(96, 0), new Vec2(96, 96) }));
            var m = dec.GetNearestNeighbor(probes);
            if (m.Label != Lib.Labels[row]) wrong++;
        }
        Assert.Equal(0, wrong);
    }

    [Fact]
    public void ARotatedMarkerDecodesWithTheRotatedLabelAndReorderedCorners()
    {
        if (NoLibrary) return;
        // draw the orientation-0 image of a code, but hand the decoder the quad rotated a quarter turn:
        // the library's 90/180/270 labels exist precisely so the code still decodes, and the corners come back
        // reordered so that TL is the marker's own top-left
        int row = MarkerRenderer.RowForCode(Lib, MarkerType.LightCubeI_Front, 0);
        var img = MarkerRenderer.Render(Lib, row, 160);
        var dec = new MarkerDecoder(Lib);
        var tl = new Vec2(0, 0); var bl = new Vec2(0, 160); var tr = new Vec2(160, 0); var br = new Vec2(160, 160);
        var straight = dec.Extract(img, new[] { tl, bl, tr, br }, 1, out _)!;
        foreach (var rotated in new[] { new[] { bl, br, tl, tr }, new[] { br, tr, bl, tl }, new[] { tr, tl, br, bl } })
        {
            var m = dec.Extract(img, rotated, 1, out var reason);
            Assert.True(m is not null, reason);
            Assert.Equal(MarkerType.LightCubeI_Front, m!.Code);
            Assert.Equal(straight.Corners, m.Corners);
        }
    }

    [Fact]
    public void ABlankQuadIsRejected()
    {
        if (NoLibrary) return;
        var img = new GrayImage(100, 100);
        img.Fill(200);
        var dec = new MarkerDecoder(Lib);
        var m = dec.Extract(img, new[] { new Vec2(0, 0), new Vec2(0, 99), new Vec2(99, 0), new Vec2(99, 99) }, 1, out var reason);
        Assert.Null(m);
        Assert.Contains("border", reason);
    }

    /// <summary>
    /// M11-002: <c>GetProbeValues</c>'s nearest-pixel rule applies the same sign branch to <b>both</b>
    /// axes (0x0089F0DA..0x0089F12E): <c>floor(c + 0.5)</c> when <c>c &gt; 0</c>, else <c>ceil(c − 0.5)</c>.
    /// The old y rule, an unconditional <c>ceil(y − 0.5)</c>, gave 2 for 2.5 where the engine gives 3.
    /// </summary>
    [Fact]
    public void TheProbeSamplingRoundsBothAxesWithTheEnginesSignBranch()
    {
        Assert.Equal(3, MarkerDecoder.RoundNearest(2.5));
        Assert.Equal(2, MarkerDecoder.RoundNearest(2.4));
        Assert.Equal(-3, MarkerDecoder.RoundNearest(-2.5));
        Assert.Equal(-2, MarkerDecoder.RoundNearest(-2.4));
        Assert.Equal(1, MarkerDecoder.RoundNearest(0.5));
        Assert.Equal(0, MarkerDecoder.RoundNearest(0.0));
        Assert.Equal(-1, MarkerDecoder.RoundNearest(-0.5));
    }

    /// <summary>
    /// M11-002: the disambiguation average is an integer division (<c>__aeabi_idiv</c> at 0x008C0C70) and
    /// the match is rejected when that integer average reaches 1.25 × the threshold
    /// (0x008C0C7E..0x008C0C96). 125 / 2 is 62, not 62.5, so it is accepted where double division would
    /// have rejected it; 126 / 2 is 63 and is rejected.
    /// </summary>
    [Fact]
    public void TheAmbiguityAverageIsIntegerDivisionAgainstOnePointTwoFive()
    {
        Assert.False(MarkerDecoder.RejectAmbiguous(125, 2, 50));   // 62 < 62.5
        Assert.True(MarkerDecoder.RejectAmbiguous(126, 2, 50));    // 63 >= 62.5
        Assert.False(MarkerDecoder.RejectAmbiguous(62, 1, 50));
        Assert.True(MarkerDecoder.RejectAmbiguous(63, 1, 50));
    }

    /// <summary>
    /// M11-002: <c>cv::normalize(query, query, 0, 255, NORM_MINMAX)</c> (0x008C09D2) rounds through
    /// OpenCV's <c>saturate_cast&lt;u8&gt;</c> (<c>cvRound</c>), which is round half to even. With the range
    /// 0..102 the scale is 2.5: 1 lands on 2.5 and must become the even 2 (not 3), 3 lands on 7.5 and must
    /// become the even 8 (not 7).
    /// </summary>
    [Fact]
    public void TheMinMaxNormalisationRoundsHalfToEvenLikeOpenCv()
    {
        var q = MarkerDecoder.NormalizeMinMax(new byte[] { 0, 1, 3, 102 });
        Assert.Equal(0, q[0]);
        Assert.Equal(2, q[1]);
        Assert.Equal(8, q[2]);
        Assert.Equal(255, q[3]);
    }

    /// <summary>
    /// M11-031: <c>ComputeBrightDarkValues</c> uses <c>Parameters+0x3C</c> = 1.01 (0x0087538E,
    /// 0x0089FD10..0x0089FD30), not a 1.0 <c>dark &gt;= bright</c>. With the border at 100 and the interior
    /// at 101 the 1.0 gate would pass; 100 × 1.01 = 101 does not. The border/interior assignment itself is
    /// still a RECOVERABLE_GAP (M11-031), kept as the stack had it.
    /// </summary>
    [Fact]
    public void TheDecoderContrastGateUsesTheEnginesOnePointZeroOneRatio()
    {
        if (NoLibrary) return;
        Assert.Equal(1.01, new QuadDetectorParameters().MinContrastRatio);

        var img = new GrayImage(200, 200);
        for (int y = 0; y < 200; y++)
            for (int x = 0; x < 200; x++)
                img[x, y] = (byte)((x < 20 || x >= 180 || y < 20 || y >= 180) ? 100 : 101);
        var corners = new[] { new Vec2(0, 0), new Vec2(0, 200), new Vec2(200, 0), new Vec2(200, 200) };
        var dec = new MarkerDecoder(Lib);
        var m = dec.Extract(img, corners, 1, out var reason);
        Assert.Null(m);
        Assert.Contains("border not darker", reason);
    }

    // ------------------------------------------------------------------ front end

    /// <summary>
    /// M11-005: <c>Matrix::SolveLeastSquaresWithCholesky&lt;float&gt;</c> 0x0088DE68. The factorisation is in
    /// natural pivot order and writes <c>1/L[i][i]</c> on the diagonal; a pivot below FLT_EPSILON
    /// (1.1920929e-07) sets the out-flag true and returns the right-hand side unchanged (no substitution),
    /// which the caller treats as the accept path (H6). For <c>A=[[4,2],[2,3]]</c>, <c>b=[1,2]</c> the exact
    /// solution is <c>[-0.125, 0.75]</c>.
    /// </summary>
    [Fact]
    public void TheCholeskySolveUsesTheEnginesPivotAndOutFlagPolarity()
    {
        // a normal 2x2 system: flag false and the exact solution
        var a = new float[,] { { 4f, 2f }, { 2f, 3f } };
        var x = CornerRefinement.SolveLeastSquaresWithCholesky(a, new[] { 1f, 2f }, out bool degenerate);
        Assert.False(degenerate);
        Assert.Equal(-0.125f, x[0], 6);
        Assert.Equal(0.75f, x[1], 6);

        // a zero first pivot: flag true, and the RHS comes back unchanged (0x0088DF58 -> 0x0088E132)
        var z = CornerRefinement.SolveLeastSquaresWithCholesky(
            new float[,] { { 0f, 0f }, { 0f, 1f } }, new[] { 5f, 6f }, out degenerate);
        Assert.True(degenerate);
        Assert.Equal(new[] { 5f, 6f }, z);

        // a degenerate pivot after a good first one: still flag true and the RHS unchanged
        var w = CornerRefinement.SolveLeastSquaresWithCholesky(
            new float[,] { { 1f, 0f }, { 0f, 0f } }, new[] { 3f, 4f }, out degenerate);
        Assert.True(degenerate);
        Assert.Equal(new[] { 3f, 4f }, w);
    }

    /// <summary>
    /// M11-032: the shipped selector byte is 1, so the live dark mask is the ecvcs integral-image variant,
    /// not the binomial path. The shipped window bank is {4, 8, 16} (G1.1b), maxScale 17 (G1.2c), the box
    /// reciprocals are 101&gt;&gt;13, 227&gt;&gt;16 and 241&gt;&gt;18 (F2), and <c>numFilters3</c>
    /// 0x0088F61A picks <c>v = (|f1-f0| &gt; |f2-f1|) ? f1 : f2</c> (a strict <c>&gt;</c>) then marks the
    /// pixel dark when <c>((v * 0xCCCC) &gt;&gt; 16) &gt; pixel</c>.
    /// </summary>
    [Fact]
    public void TheLiveDarkMaskIsTheEcvcsBoxFilterBinarise()
    {
        var p = new QuadDetectorParameters();
        Assert.Equal(0xCCCC, p.DarkThresholdQ16);
        Assert.Equal(new[] { 4, 8, 16 }, EcvcsExtractor.WindowBank(p));
        Assert.Equal(17, EcvcsExtractor.MaxScale(EcvcsExtractor.WindowBank(p)));
        Assert.Equal((101, 13), EcvcsExtractor.FilterCoefficients(4));
        Assert.Equal((227, 16), EcvcsExtractor.FilterCoefficients(8));
        Assert.Equal((241, 18), EcvcsExtractor.FilterCoefficients(16));

        // B1: 100 * 0xCCCC >> 16 = 79, so 79 is not dark and 78 is.
        Assert.Equal(79, (100 * p.DarkThresholdQ16) >> 16);
        Assert.Equal((byte)1, EcvcsExtractor.NumFilters3(0, 100, 100, 78, p.DarkThresholdQ16));
        Assert.Equal((byte)0, EcvcsExtractor.NumFilters3(0, 100, 100, 79, p.DarkThresholdQ16));
        // an exact tie in the adjacent gaps keeps f2 (strict >)
        Assert.Equal((byte)1, EcvcsExtractor.NumFilters3(0, 10, 20, 10, 0x10000));

        // a flat image has every box mean equal, so no pixel is dark. The image must be at least as large as
        // the border (the ctor 0x008A4D6A requires numBorderPixels <= rows and cols), so 64x64, not 9x9.
        var flat = new GrayImage(64, 64);
        flat.Fill(100);
        Assert.All(EcvcsExtractor.BinaryMask(flat, p).Pixels, v => Assert.Equal((byte)0, v));

        // the non-live binomial filter is retained as a faithful transcription, but is not the live mask
        Assert.All(QuadDetector.BinomialFilter(flat).Pixels, v => Assert.Equal(100, v));
    }

    /// <summary>
    /// <c>IsQuadrilateralReasonable</c> 0x00892B18: the first three corners' cross product must reach
    /// <c>quads_minQuadArea</c> (25, the parameters' +0x30), the four corner cross products must agree in
    /// sign, one diagonal's two triangles must be within the symmetry threshold
    /// (<c>max &lt;&lt; 8 &lt; 512 * min</c>, +0x34 being 512 in 8.8) and every corner must be at least two
    /// pixels from the image's edge (+0x38).
    /// </summary>
    [Fact]
    public void TheQuadGeometryTestIsTheEngines()
    {
        var p = new QuadDetectorParameters();
        Assert.Equal(25, p.MinQuadArea);
        Assert.Equal(512, p.QuadSymmetryThresholdQ8);
        Assert.Equal(2, p.MinDistanceFromEdge);

        // corners arrive in the engine's order: upper left, lower left, upper right, lower right
        // a square well inside the image passes
        var square = new[] { new Vec2(50, 50), new Vec2(50, 90), new Vec2(90, 50), new Vec2(90, 90) };
        Assert.True(QuadDetector.IsQuadrilateralReasonable(square, 320, 240, null, out bool swapped));
        Assert.False(swapped);

        // the same square with its winding reversed is accepted, and reported as needing the swap
        var reversed = new[] { new Vec2(50, 50), new Vec2(90, 50), new Vec2(50, 90), new Vec2(90, 90) };
        Assert.True(QuadDetector.IsQuadrilateralReasonable(reversed, 320, 240, null, out swapped));
        Assert.True(swapped);

        // too small: the first cross product is under 25
        var tiny = new[] { new Vec2(50, 50), new Vec2(50, 53), new Vec2(54, 50), new Vec2(54, 53) };
        Assert.False(QuadDetector.IsQuadrilateralReasonable(tiny, 320, 240));

        // not convex
        var dart = new[] { new Vec2(50, 50), new Vec2(50, 90), new Vec2(90, 50), new Vec2(60, 60) };
        Assert.False(QuadDetector.IsQuadrilateralReasonable(dart, 320, 240));

        // convex but lopsided: neither diagonal splits it within a factor of two
        var wedge = new[] { new Vec2(50, 50), new Vec2(50, 200), new Vec2(250, 50), new Vec2(250, 56) };
        Assert.False(QuadDetector.IsQuadrilateralReasonable(wedge, 320, 240));

        // against the edge
        var atEdge = new[] { new Vec2(1, 50), new Vec2(1, 90), new Vec2(41, 50), new Vec2(41, 90) };
        Assert.False(QuadDetector.IsQuadrilateralReasonable(atEdge, 320, 240));
    }

    /// <summary>
    /// The size filter <c>InvalidateSmallOrLargeComponents</c> uses the operands computed inline from the
    /// frame (M11-026, S4): <c>min = round((0.03*dmin)^2 - (0.024*dmin)^2)</c> = 19 and
    /// <c>max = round((0.97*dmax)^2 - (0.776*dmax)^2)</c> = 34685 for a 320x240 frame. A marker twenty
    /// pixels on a side leaves a component well above the floor; a tiny one does not.
    /// </summary>
    [Fact]
    public void AMarkerSmallerThanTheComponentFloorIsNotFound()
    {
        if (NoLibrary) return;
        Assert.Equal((19, 34685), new QuadDetectorParameters().ComponentSizeRange(320, 240));
        var found = new List<(int Side, int Markers)>();
        foreach (int side in new[] { 20, 6 })
        {
            var frame = new GrayImage(320, 240);
            frame.Fill(140);
            double x0 = 150, y0 = 100;
            // the renderer takes TL, BL, TR, BR
            var order = new[] { new Vec2(x0, y0), new Vec2(x0, y0 + side), new Vec2(x0 + side, y0), new Vec2(x0 + side, y0 + side) };
            MarkerRenderer.Draw(frame, Lib, MarkerRenderer.RowForCode(Lib, MarkerType.LightCubeI_Top), order);
            var det = new MarkerDetector(new QuadDetector(), new MarkerDecoder(Lib));
            found.Add((side, det.Detect(frame, 1).Count));
        }
        Assert.Equal(1, found[0].Markers);
        Assert.Equal(0, found[1].Markers);
    }

    /// <summary>
    /// <c>TraceNextExteriorBoundary</c> 0x008C6B18 on a solid rectangle. The engine never walks the
    /// component pixel by pixel: it keeps the least and greatest x of every row and the least and
    /// greatest y of every column and stitches those four extent arrays into a staircase, starting at the
    /// rightmost pixel of the top row and going down the right side, left along the bottom, up the left
    /// side and back along the top. For a rectangle that is exactly its perimeter, once round and closed:
    /// the last point of the top pass is the first point of the right-hand one.
    /// </summary>
    [Fact]
    public void TheExteriorBoundaryIsTheEnginesStaircase()
    {
        const int w = 20, h = 12;
        var labels = new int[w * h];
        for (int y = 3; y <= 8; y++)
            for (int x = 4; x <= 11; x++)
                labels[y * w + x] = 7;

        var boundary = QuadCorners.ExteriorBoundary(labels, 7, w, 4, 3, 11, 8);
        Assert.NotNull(boundary);
        // the perimeter of a 8 x 6 rectangle, every point once and the start repeated to close it
        Assert.Equal(2 * (8 + 6) - 4 + 1, boundary!.Count);
        Assert.Equal(boundary[0], boundary[^1]);
        Assert.Equal(boundary.Count - 1, boundary.Distinct().Count());
        Assert.All(boundary, b => Assert.True(b.X == 4 || b.X == 11 || b.Y == 3 || b.Y == 8));
        // it starts at the top right and the second point is directly below it
        Assert.Equal(new QuadCorners.BoundaryPoint(11, 3), boundary[0]);
        Assert.Equal(new QuadCorners.BoundaryPoint(11, 4), boundary[1]);
        // and it turns the bottom right corner before it reaches the bottom left
        int bottomRight = boundary.FindIndex(b => b.X == 11 && b.Y == 8);
        int bottomLeft = boundary.FindIndex(b => b.X == 4 && b.Y == 8);
        Assert.True(bottomRight < bottomLeft);

        // a bounding box with an empty row cannot be traced (0x008C6F70)
        var gapped = new int[w * h];
        for (int x = 4; x <= 11; x++) { gapped[3 * w + x] = 7; gapped[8 * w + x] = 7; }
        Assert.Null(QuadCorners.ExteriorBoundary(gapped, 7, w, 4, 3, 11, 8));
    }

    /// <summary>
    /// The smoothing kernel of <c>ExtractLineFitsPeaks</c> 0x008A5DB8: sigma is the boundary length over
    /// 64 (0x008A5E90), the size is OpenCV's size-to-sigma relation solved backwards and forced odd
    /// (0x008A5ED6..0x008A5F1A), and what the boundary is actually convolved with is that Gaussian put
    /// through <c>cv::filter2D</c> with [-0.5, 0, +0.5] - a derivative of a Gaussian, whose end taps come
    /// out zero because filter2D reflects its border.
    /// </summary>
    [Fact]
    public void TheBoundaryIsSmoothedWithADerivativeOfAGaussian()
    {
        Assert.Equal(1.0f / 64, QuadCorners.SigmaPerBoundaryPoint);
        // 256 boundary points: sigma 4, and ((4 - 0.8) / 0.3 + 1) * 2 + 1 = 24.3, rounded up to 25
        Assert.Equal(25, QuadCorners.KernelSize(4f));
        Assert.Equal(5, QuadCorners.KernelSize(1f));
        Assert.Equal(1, QuadCorners.KernelSize(0.1f));

        var g = QuadCorners.GaussianKernel(25, 4.0);
        Assert.Equal(1.0, g.Sum(), 5);
        Assert.Equal(g[12], g.Max(), 6);
        Assert.Equal(g[11], g[13], 6);

        var d = QuadCorners.DifferentiateKernel(g);
        Assert.Equal(0f, d[0]);
        Assert.Equal(0f, d[^1]);
        Assert.Equal(0.0, d.Sum(), 5);
        Assert.Equal(-d[11], d[13], 6);
        Assert.True(d[11] > 0 && d[13] < 0);
    }

    /// <summary>
    /// The four k-means seeds at 0x008A62D8..0x008A63EE are four equal arcs of the boundary, the quarter
    /// points taken with C's truncating division, and the clustering that follows runs with
    /// <c>KMEANS_USE_INITIAL_LABELS</c> and one attempt - so it never draws a random centre and the same
    /// boundary always gives the same four sides.
    /// </summary>
    [Fact]
    public void TheClusteringStartsFromFourEqualArcs()
    {
        var labels = QuadCorners.InitialLabels(10);
        Assert.Equal(new[] { 0, 0, 1, 1, 1, 2, 2, 3, 3, 3 }, labels);
        Assert.Equal(4, QuadCorners.Clusters);
        Assert.Equal(15, QuadCorners.KMeansMaxIterations);
        Assert.Equal(0.1, QuadCorners.KMeansEpsilon);

        // two well separated groups of unit vectors, seeded with the arcs, stay put and repeat exactly
        var samples = new (float Y, float X)[8];
        for (int i = 0; i < 8; i++) samples[i] = i < 4 ? (1f, 0f) : (0f, 1f);
        var a = QuadCorners.InitialLabels(8); QuadCorners.KMeans(samples, a);
        var b = QuadCorners.InitialLabels(8); QuadCorners.KMeans(samples, b);
        Assert.Equal(a, b);
        // the two directions never share a label; the empty clusters OpenCV is left with take a point
        // each from the largest one rather than being filled at random
        Assert.Empty(a.Take(4).Intersect(a.Skip(4)));
        Assert.Equal(4, a.Distinct().Count());
    }

    /// <summary>
    /// C1.4 (M11-029): the reject at 0x008A64B4..0x008A65F2 compares the float dot of a point's unit
    /// tangent with its kmeans centre against cos(25 deg) = 0x3F6803C9 = 0.90630776; on less-than the
    /// point's label becomes -1.
    /// </summary>
    [Fact]
    public void TheClusterRejectDropsPointsMoreThanTwentyFiveDegreesFromTheirCentre()
    {
        Assert.Equal(0.90630776f, QuadCorners.Cos25Deg, 7);
        var tangents = new[] { (Y: 1f, X: 0f), (Y: 0f, X: 1f) };
        var centers = new[] { (Y: 1f, X: 0f), (Y: 0.70710678f, X: 0.70710678f) };
        var labels = new[] { 0, 1 };
        QuadCorners.ApplyClusterReject(tangents, labels, centers);
        Assert.Equal(0, labels[0]);    // dot = 1.0 >= cos25
        Assert.Equal(-1, labels[1]);   // dot = 0.7071 < cos25
    }

    /// <summary>
    /// C1.2 (M11-022): the ROI/negative mode is <c>Parameters+0x7c</c>, shipped 0 (a single non-negative
    /// pass). Mode 3 selects an empty pass list, so Detect returns nothing; mode 2 is
    /// <c>{false,true}</c>, and the ROI the first pass records masks the marker before the negative pass,
    /// so the marker is still found exactly once.
    /// </summary>
    [Fact]
    public void TheShippedRoiNegativeModeIsASingleNonNegativePass()
    {
        if (NoLibrary) return;
        Assert.Equal(0, new QuadDetectorParameters().NegativeMode);

        var frame = new GrayImage(320, 240);
        frame.Fill(140);
        var corners = new[] { new Vec2(100, 60), new Vec2(100, 160), new Vec2(200, 60), new Vec2(200, 160) };
        MarkerRenderer.Draw(frame, Lib, MarkerRenderer.RowForCode(Lib, MarkerType.LightCubeI_Front), corners);

        var shipped = new MarkerDetector(new QuadDetector(), new MarkerDecoder(Lib));
        Assert.Single(shipped.Detect(frame, 1));

        var empty = new MarkerDetector(new QuadDetector(new QuadDetectorParameters { NegativeMode = 3 }), new MarkerDecoder(Lib));
        Assert.Empty(empty.Detect(frame, 1));

        var twoPass = new MarkerDetector(new QuadDetector(new QuadDetectorParameters { NegativeMode = 2 }), new MarkerDecoder(Lib));
        Assert.Single(twoPass.Detect(frame, 1));
    }

    /// <summary>
    /// The whole of <c>ExtractLineFitsPeaks</c> on a square: four clusters of boundary tangents, a line
    /// fitted to each - across the wider extent, so the two vertical sides are fitted as x of y
    /// (0x008A6714) - and every pair intersected, of which exactly four must land inside the image
    /// (0x008A6BE2). The corners come back clockwise on screen, starting at the top left, rounded to
    /// whole pixels the way the engine rounds into its s16 quad.
    /// </summary>
    [Fact]
    public void TheCornersAreFourLineIntersections()
    {
        const int w = 120, h = 100;
        var labels = new int[w * h];
        for (int y = 20; y <= 70; y++)
            for (int x = 30; x <= 80; x++)
                labels[y * w + x] = 3;
        var boundary = QuadCorners.ExteriorBoundary(labels, 3, w, 30, 20, 80, 70);
        Assert.NotNull(boundary);

        var corners = QuadCorners.ExtractLineFitsPeaks(boundary!, h, w);
        Assert.NotNull(corners);
        Assert.Equal(4, corners!.Length);
        foreach (var c in corners) { Assert.Equal(c.X, Math.Round(c.X)); Assert.Equal(c.Y, Math.Round(c.Y)); }
        var expected = new[] { new Vec2(30, 20), new Vec2(80, 20), new Vec2(80, 70), new Vec2(30, 70) };
        for (int i = 0; i < 4; i++)
        {
            Assert.True(Math.Abs(corners[i].X - expected[i].X) <= 1, $"corner {i} x {corners[i].X}");
            Assert.True(Math.Abs(corners[i].Y - expected[i].Y) <= 1, $"corner {i} y {corners[i].Y}");
        }
    }

    /// <summary>
    /// <c>Quadrilateral&lt;float&gt;::ComputeClockwiseCorners</c> 0x008A1324 sorts the corners by the
    /// angle they make with their own centroid, ascending, which with y down is clockwise on screen
    /// starting from the negative x axis; and the engine rounds each coordinate half away from zero after
    /// clamping it to an s16 (0x008A6C30).
    /// </summary>
    [Fact]
    public void TheCornersAreSortedClockwiseAboutTheirCentroid()
    {
        var shuffled = new[] { new Vec2(10, 90), new Vec2(90, 10), new Vec2(10, 10), new Vec2(90, 90) };
        var cw = QuadCorners.ComputeClockwiseCorners(shuffled);
        Assert.Equal(new Vec2(10, 10), cw[0]);
        Assert.Equal(new Vec2(90, 10), cw[1]);
        Assert.Equal(new Vec2(90, 90), cw[2]);
        Assert.Equal(new Vec2(10, 90), cw[3]);

        Assert.Equal(3.0, QuadCorners.RoundToS16(2.5));
        Assert.Equal(-3.0, QuadCorners.RoundToS16(-2.5));
        Assert.Equal(0.0, QuadCorners.RoundToS16(0.4));
        Assert.Equal(32767.0, QuadCorners.RoundToS16(40000.0));
        Assert.Equal(-32768.0, QuadCorners.RoundToS16(-40000.0));
    }

    [Fact]
    public void TheQuadDetectorFindsARenderedMarkerToSubPixelAccuracy()
    {
        if (NoLibrary) return;
        var frame = new GrayImage(320, 240);
        frame.Fill(140);
        var corners = new[] { new Vec2(100.3, 60.6), new Vec2(96.2, 150.1), new Vec2(190.7, 70.4), new Vec2(200.5, 158.9) };
        MarkerRenderer.Draw(frame, Lib, MarkerRenderer.RowForCode(Lib, MarkerType.LightCubeI_Top), corners);
        var det = new QuadDetector();
        var quads = det.Detect(frame);
        Assert.True(quads.Count >= 1, det.LastStats.ToString());
        var best = quads.OrderBy(q => q.Corners.Zip(corners, (a, b) => (a - b).Length).Max()).First();
        // the quad front end gives whole-pixel corners; the engine's RefineCorners takes them to sub-pixel
        var refined = best.Corners;
        var h = Homography.FromUnitSquare(refined);
        Assert.Equal(CornerRefinement.Outcome.Refined, CornerRefinement.RefineCorners(frame, Lib, ref refined, ref h, det.Parameters));
        for (int i = 0; i < 4; i++)
            Assert.True((refined[i] - corners[i]).Length < 1.5, $"corner {i}: {refined[i]} vs {corners[i]}");
    }

    [Fact]
    public void TheDetectorDecodesMarkersAtSeveralSizesAndPlaces()
    {
        if (NoLibrary) return;
        var det = new MarkerDetector(new QuadDetector(), new MarkerDecoder(Lib));
        foreach (var (code, corners) in new[]
        {
            (MarkerType.LightCubeI_Front, new[] { new Vec2(20, 20), new Vec2(22, 60), new Vec2(58, 18), new Vec2(60, 62) }),
            (MarkerType.LightCubeJ_Back, new[] { new Vec2(200, 100), new Vec2(195, 220), new Vec2(300, 110), new Vec2(305, 215) }),
            (MarkerType.LightCubeK_Right, new[] { new Vec2(120, 90), new Vec2(120, 130), new Vec2(160, 90), new Vec2(160, 130) }),
        })
        {
            var frame = new GrayImage(320, 240);
            frame.Fill(120);
            MarkerRenderer.Draw(frame, Lib, MarkerRenderer.RowForCode(Lib, code), corners);
            var markers = det.Detect(frame, 1);
            Assert.True(markers.Any(m => m.Code == code), $"{code}: found [{string.Join(",", markers.Select(m => m.Code))}]; " +
                                                            string.Join("; ", det.LastQuads.Select(q => q.Reason)) + " " + det.Quads.LastStats);
        }
    }

    [Fact]
    public void TwoMarkersInOneFrameAreBothFound()
    {
        if (NoLibrary) return;
        var frame = new GrayImage(320, 240);
        frame.Fill(150);
        MarkerRenderer.Draw(frame, Lib, MarkerRenderer.RowForCode(Lib, MarkerType.LightCubeI_Front), new[] { new Vec2(30, 60), new Vec2(30, 130), new Vec2(100, 60), new Vec2(100, 130) });
        MarkerRenderer.Draw(frame, Lib, MarkerRenderer.RowForCode(Lib, MarkerType.LightCubeJ_Front), new[] { new Vec2(200, 80), new Vec2(200, 140), new Vec2(260, 80), new Vec2(260, 140) });
        var det = new MarkerDetector(new QuadDetector(), new MarkerDecoder(Lib));
        var markers = det.Detect(frame, 1);
        Assert.Contains(markers, m => m.Code == MarkerType.LightCubeI_Front);
        Assert.Contains(markers, m => m.Code == MarkerType.LightCubeJ_Front);
    }

    [Fact]
    public void ARoomWithoutMarkersYieldsNothing()
    {
        if (NoLibrary) return;
        var frame = new GrayImage(320, 240);
        var rnd = new Random(7);
        for (int y = 0; y < 240; y++)
            for (int x = 0; x < 320; x++)
                frame[x, y] = (byte)Math.Clamp(120 + 60 * Math.Sin(x / 23.0) + 40 * Math.Cos(y / 17.0) + rnd.Next(-20, 20), 0, 255);
        var det = new MarkerDetector(new QuadDetector(), new MarkerDecoder(Lib));
        Assert.Empty(det.Detect(frame, 1));
    }

    // ------------------------------------------------------------------ geometry

    [Fact]
    public void CubeMarkerNormalsPointOutOfTheirFaces()
    {
        foreach (var m in CubeGeometry.CubeMarkers(ObjectType.Block_LIGHTCUBE1))
        {
            var centre = m.PoseOnObject.Translation;
            var normal = m.NormalOnObject;
            Assert.True(normal.Dot(centre.Normalized()) > 0.99, $"{m.Face}: normal {normal} vs centre {centre}");
            // the marker's corners lie on the face plane, 22 mm from the centre
            foreach (var c in m.CornersOnObject()) Assert.InRange(Math.Abs(c.Dot(centre.Normalized())), 21.9, 22.1);
        }
        Assert.Equal(MarkerType.LightCubeI_Front, CubeGeometry.CubeMarkers(ObjectType.Block_LIGHTCUBE1).First(m => m.Face == BlockFace.Front).Code);
        Assert.Equal(MarkerType.LightCubeI_Bottom, CubeGeometry.CubeMarkers(ObjectType.Block_LIGHTCUBE1).First(m => m.Face == BlockFace.Bottom).Code);
        Assert.Equal(MarkerType.LightCubeK_Top, CubeGeometry.CubeMarkers(ObjectType.Block_LIGHTCUBE3).First(m => m.Face == BlockFace.Top).Code);
    }

    [Fact]
    public void TheCameraLooksForwardAndSlightlyDownAtHeadAngleZero()
    {
        var cam = HeadGeometry.CameraPoseInRobot(0);
        var axis = cam.Rotation * new Vec3(0, 0, 1);       // optical axis in the robot frame
        Assert.InRange(axis.X, 0.99, 1.0);
        Assert.InRange(Deg(Math.Asin(-axis.Z)), 3.9, 4.1);  // 4 degrees down
        Assert.InRange(cam.Translation.X, 4.4, 4.6);         // -13 + 17.52
        Assert.InRange(cam.Translation.Z, 66.4, 66.6);       // 49 + 17.52
        // image right is the robot's right (-Y), image down is down
        var right = cam.Rotation * new Vec3(1, 0, 0);
        Assert.InRange(right.Y, -1.0, -0.99);
        var down = cam.Rotation * new Vec3(0, 1, 0);
        Assert.True(down.Z < -0.99);
        // raising the head 20 degrees lifts the optical axis by 20 degrees
        var up = HeadGeometry.CameraPoseInRobot(20 * Math.PI / 180).Rotation * new Vec3(0, 0, 1);
        Assert.InRange(Deg(Math.Asin(up.Z)), 15.9, 16.1);
    }

    [Fact]
    public void PoseEstimationRecoversAKnownCubePose()
    {
        var cal = CameraCalibration.Nominal();
        var robot = HeadGeometry.RobotPose(new RobotPose { X = 10, Y = -5, Angle = 0.2f });
        var camera = new CameraModel(cal, HeadGeometry.CameraPoseInWorld(robot, -0.1));
        var cube = new Pose3d(Mat3.AboutZ(0.35), new Vec3(150, 20, 22));
        var marker = CubeGeometry.CubeMarkers(ObjectType.Block_LIGHTCUBE1).First(m => m.Face == BlockFace.Front);
        var world = marker.CornersInWorld(cube);
        var px = world.Select(w => camera.Project(w)!.Value).ToList();
        var res = PoseEstimation.Solve(camera, marker.CornersOnObject(), px);
        Assert.NotNull(res);
        var recovered = camera.Pose.Compose(res!.ObjectInCamera);
        Assert.True((recovered.Translation - cube.Translation).Length < 1.0, $"{recovered.Translation} vs {cube.Translation}");
        Assert.True(Deg(recovered.Rotation.AngularDistance(cube.Rotation)) < 0.5, $"angle off by {Deg(recovered.Rotation.AngularDistance(cube.Rotation)):F2} deg");
        Assert.True(res.RmsReprojectionPx < 0.01);
    }

    [Fact]
    public void DistortionRoundTrips()
    {
        var cal = CameraCalibration.Nominal() with { DistortionCoefficients = new[] { -0.1, 0.02, 0.001, -0.001, 0.0, 0, 0, 0 } };
        var cam = new CameraModel(cal, Pose3d.Identity);
        foreach (var p in new[] { new Vec2(10, 10), new Vec2(160, 120), new Vec2(300, 230), new Vec2(50, 200) })
        {
            var n = cam.Undistort(p);
            var back = cam.Distort(n);
            Assert.True((back - p).Length < 1e-6, $"{p} -> {n} -> {back}");
        }
    }

    // ------------------------------------------------------------------ calibration

    [Fact]
    public void TheCalibrationStructRoundTripsAndIndexedNvResultsAssemble()
    {
        var cal = new CameraCalibration { FocalLengthX = 290.5, FocalLengthY = 291.2, CenterX = 158.7, CenterY = 121.3, Skew = 0, Rows = 240, Columns = 320,
                                          DistortionCoefficients = new[] { -0.05, 0.01, 0, 0, 0, 0, 0, 0 } };
        var bytes = cal.ToBytes();
        Assert.Equal(CameraCalibration.WireSize, bytes.Length);
        var back = CameraCalibration.Parse(bytes);
        Assert.Equal(cal.FocalLengthX, back.FocalLengthX, 3);
        Assert.Equal(240, back.Rows);
        Assert.Equal(-0.05, back.DistortionCoefficients[0], 6);

        using var rig = new WorldRig();
        var nv = rig.Robot.Engine.NvStorage!;

        // M3-028/M3-029: a factory read has no 16-byte header. A single index-0 blob is delivered as-is, so the
        // 56-byte calibration arrives as exactly 56 bytes (0x80000001 is a factory tag; no header/reassembly shift).
        NvResult? got = null;
        nv.Read(CameraCalibration.NvEntryTag, r => got = r);
        rig.Robot.Engine.Tick();                                               // M3-026: Update sends the queued read
        rig.Send(new NVOpResult { Tag = CameraCalibration.NvEntryTag, Op = 0, Result = NvStorageComponent.ResultOkay, Length = 0, Data = bytes });
        Assert.NotNull(got);
        Assert.Equal(0, got!.Value.Result);
        Assert.Equal(CameraCalibration.WireSize, got.Value.Data.Length);       // exactly 56, no header, no stride
        Assert.Equal(291.2, CameraCalibration.Unpack(got.Value.Data).FocalLengthY, 3);

        // M3-029: NVOpResult.Length is a blob index. A blob at index k lands at offset k*1024 on a factory read, so
        // the assembled buffer grows to index*1024 + its length (the CONTROL capture's blobs arrived at indices 0..7
        // and 15, which is why the engine would have delivered 15452 bytes, not 56).
        Assert.Equal(1024, NvStorageComponent.BlobStride);                     // 0x643538 lsls r0,r4,#0xa
        NvResult? placed = null;
        nv.Read(CameraCalibration.NvEntryTag, r => placed = r);
        rig.Robot.Engine.Tick();                                               // M3-026: Update sends the queued read
        var chunk = new byte[] { 9, 8, 7, 6 };
        rig.Send(new NVOpResult { Tag = CameraCalibration.NvEntryTag, Op = 0, Result = NvStorageComponent.ResultMore, Length = 3, Data = chunk });
        rig.Send(new NVOpResult { Tag = CameraCalibration.NvEntryTag, Op = 0, Result = NvStorageComponent.ResultOkay, Length = 0, Data = Array.Empty<byte>() });
        Assert.NotNull(placed);
        Assert.Equal(0, placed!.Value.Result);
        Assert.Equal(3 * 1024 + chunk.Length, placed.Value.Data.Length);
        Assert.Equal(chunk, placed.Value.Data[(3 * 1024)..]);
        Assert.All(placed.Value.Data[..(3 * 1024)], b => Assert.Equal(0, b));   // holes are zero-filled

        Assert.Throws<FormatException>(() => CameraCalibration.Parse(new byte[CameraCalibration.WireSize]));
    }

    /// <summary>
    /// The NV read request, as the recovered engine builds it for <c>NVEntry_CameraCalib</c>: the tag
    /// 0x80000001, length 1 (the factory size table's value for the tag, contradicting the old 0x400), READ, and
    /// a second byte the component's constructor zeroes and never writes again (0x006428AA).
    /// </summary>
    [Fact]
    public void TheNvReadRequestUsesLengthOneForTheCameraCalibration()
    {
        Assert.Equal(0x80000001u, CameraCalibration.NvEntryTag);
        Assert.Equal(1, CameraSettings.CalibrationReadLength);

        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        robot.Transport.OfflineOutbound.Clear();
        robot.Engine.NvStorage!.Read(CameraCalibration.NvEntryTag, _ => { });
        robot.Engine.Tick();                                                       // M3-026: Update sends the queued read
        robot.Transport.OfflineTick();

        List<NVCommand> Commands() => robot.Transport.OfflineOutbound
            .SelectMany(f => f.Messages)
            .Where(sm => sm.Type is ReliableMessageType.SingleReliableMessage
                                 or ReliableMessageType.SingleUnreliableMessage)
            .Select(sm => { try { return RobotMessage.Parse(sm.Payload); } catch { return null; } })
            .OfType<NVCommand>()
            .ToList();
        var end = DateTime.UtcNow.AddSeconds(2);
        while (Commands().Count == 0 && DateTime.UtcNow < end) { robot.Transport.OfflineTick(); Thread.Sleep(2); }
        var cmd = Commands()[0];
        Assert.Equal(CameraCalibration.NvEntryTag, cmd.Tag);
        Assert.Equal(1, cmd.Length);
        Assert.Equal(NvStorageComponent.OpRead, cmd.Op);
        Assert.Equal(0, cmd.Unknown);
        Assert.Empty(cmd.Data);
    }

    [Fact]
    public void TheSetBodyAngleMessagePacksTheEnginesFieldOrder()
    {
        var m = TurnTowardsPose.Message(1.5, 2.0, 3.0, 0.0349066, 0, true, 7);
        var bytes = m.ToBytes();
        Assert.Equal(1.5f, BitConverter.ToSingle(bytes, 1));
        Assert.Equal(2.0f, BitConverter.ToSingle(bytes, 5));
        Assert.Equal(3.0f, BitConverter.ToSingle(bytes, 9));
        Assert.Equal(0.0349066f, BitConverter.ToSingle(bytes, 13));
        Assert.Equal(0, BitConverter.ToUInt16(bytes, 17));
        Assert.Equal(1, bytes[19]);
        Assert.Equal(7, bytes[20]);
        Assert.Equal(21, bytes.Length);
    }

    // ------------------------------------------------------------------ world model

    /// <summary>An offline robot with a connected cube 1, a calibration, and a vision system fed synthetic frames.</summary>
    private sealed class WorldRig : IDisposable
    {
        public readonly CozmoRobot Robot = CozmoRobot.CreateOffline();
        public readonly VisionSystem Vision;
        public readonly CameraCalibration Cal = CameraCalibration.Nominal();
        public uint T = 1000;
        private ushort _seq = 1;
        public readonly List<string> Log = new();

        public WorldRig(bool connectCube = true)
        {
            Deliver(new SubMessage(ReliableMessageType.ConnectionResponse, Array.Empty<byte>(), _seq++));
            Vision = new VisionSystem(Robot, Cal) { Enabled = false };
            Vision.World.Log += Log.Add;
            Vision.Log += Log.Add;
            if (connectCube) Send(new ObjectConnectionState { ObjectID = 7, FactoryID = 0xABCD, ObjectType = ObjectType.Block_LIGHTCUBE1, Connected = true });
        }

        public void Send(RobotMessage m) => Deliver(new SubMessage(ReliableMessageType.SingleReliableMessage, m.ToBytes(), _seq++));

        private void Deliver(SubMessage sm)
        {
            var f = new Frame { Type = ReliableMessageType.MultipleMixedMessages, SeqMin = sm.Seq, SeqMax = sm.Seq, Ack = 0, Messages = new List<SubMessage> { sm } };
            Robot.Transport.ProcessIncoming(FrameCodec.Encode(f));
        }

        /// <summary>One robot state at the current time with a pose and head angle.</summary>
        public void State(float x = 0, float y = 0, float angle = 0, float head = 0, RobotStatusFlag flags = 0, float gz = 0)
        {
            Send(new RobotState { Timestamp = T, Pose = new RobotPose { X = x, Y = y, Angle = angle }, HeadAngle = head, Status = (uint)flags,
                                  Accel = new AccelData { Z = 9800 }, Gyro = new GyroData { Z = gz } });
        }

        /// <summary>Renders what the camera would see of a cube at <paramref name="cube"/> (or an empty frame) and processes it.</summary>
        public VisionFrameResult Frame(Pose3d? cube, float x = 0, float y = 0, float angle = 0, float head = 0, RobotStatusFlag flags = 0, float gz = 0,
                                       float? imuRateY = null, float? imuRateZ = null)
        {
            State(x, y, angle, head, flags, gz);
            // M11-004: every camera frame carries an image IMU sample (ImageImuData 0xF4), which feeds
            // VisionComponent+0xb0's ImuDataHistory; zero rates mean "not rotating". The rotating gate's
            // fail-safe (no bracket -> true) would otherwise skip every unobserved check offline.
            Send(new ImageImuData { ImageId = T, RateX = 0, RateY = imuRateY ?? 0, RateZ = imuRateZ ?? 0, Line2Number = 0 });
            var pd = Vision.History.At(T)!.Value;
            var frame = new GrayImage(Cal.Columns, Cal.Rows);
            frame.Fill(150);
            if (cube is { } c) MarkerRenderer.DrawCube(frame, Lib, new CameraModel(Cal, pd.CameraPose), ObjectType.Block_LIGHTCUBE1, c);
            var r = Vision.ProcessImage(frame, T, T, pd);
            T += 33;
            return r;
        }

        public void Dispose() { Vision.Dispose(); Robot.Dispose(); }
    }

    /// <summary>A cube sitting on the floor in front of the robot, its front face towards the camera.</summary>
    private static Pose3d CubeAhead(double distanceMm = 150, double yMm = 0, double yawRad = 0) =>
        new(Mat3.AboutZ(yawRad), new Vec3(distanceMm, yMm, CubeGeometry.CubeSizeMm / 2));

    [Fact]
    public void ASeenConnectedCubeBecomesLocatedAtItsPose()
    {
        if (NoLibrary) return;
        using var rig = new WorldRig();
        var cube = CubeAhead(140, 10, 0.1);
        var r = rig.Frame(cube, head: -0.15f);
        Assert.True(r.Markers.Count >= 1, string.Join("; ", rig.Log));
        Assert.Contains(r.Markers, m => m.Code == MarkerType.LightCubeI_Front);
        Assert.Single(r.Objects);
        var o = r.Objects[0].Object;
        Assert.Equal(7u, o.ObjectId);
        Assert.Equal(PoseState.Known, o.PoseState);
        Assert.True(o.IsLocated);
        Assert.True((o.Pose.Translation - cube.Translation).Length < 5, $"{o.Pose.Translation} vs {cube.Translation} ({string.Join("; ", rig.Log)})");
        Assert.True(Deg(o.Pose.Rotation.AngularDistance(cube.Rotation)) < 3, $"yaw {Deg(o.Pose.AngleAroundZ):F1}");
        Assert.Equal(UpAxis.ZPositive, o.UpAxisFromPose());
        Assert.NotNull(rig.Vision.World.GetLocatedObjectById(7));
        Assert.True(rig.Vision.Locator.IsLocated(7));
        Assert.InRange(rig.Vision.Locator.DistanceFromRobotMm(7)!.Value, 130, 155);
        Assert.True(rig.Vision.Locator.IsVisibleFromCamera(7));
    }

    /// <summary>
    /// M11-004 / H1: the native <c>AddAndUpdateObjects</c> does <b>not</b> drop an observed active object
    /// with no connected counterpart. It warns ("Observed active object of type %s but it's not connected.
    /// Is the battery plugged in?", string 0xBF854B at 0x00620E9E) and records a 10 s cooldown in the
    /// <c>unordered_map&lt;int,float&gt;</c> (0x00620ED2..0x00620EDA), then continues to 0x00620EDE. The
    /// observation is kept, and the warning is suppressed for ten seconds.
    /// </summary>
    [Fact]
    public void AnUnconnectedCubeIsStillObservedAndOnlyWarnsOncePerTenSeconds()
    {
        if (NoLibrary) return;
        using var rig = new WorldRig(connectCube: false);
        var r = rig.Frame(CubeAhead(), head: -0.15f);
        Assert.NotEmpty(r.Markers);
        Assert.Single(r.Objects);
        Assert.Equal(1u, r.Objects[0].Object.ObjectId);        // M11-013 policy: id from the type
        Assert.Contains(rig.Log, l => l.Contains("not connected"));
        Assert.Single(rig.Vision.World.Objects);
        // the next frame is inside the 10 s cooldown (the frames are 33 ms apart), so no second warning
        int warnings = rig.Log.Count(l => l.Contains("not connected"));
        rig.Frame(CubeAhead(), head: -0.15f);
        Assert.Equal(warnings, rig.Log.Count(l => l.Contains("not connected")));
        // the offline id switch is retained; it no longer gates whether the object is kept
        rig.Vision.World.AllowUnconnectedObjects = true;
        var r2 = rig.Frame(CubeAhead(), head: -0.15f);
        Assert.Single(r2.Objects);
        Assert.Equal(1u, r2.Objects[0].Object.ObjectId);
        // after the 10 s cooldown (0x00620B1E, kUnconnectedObservationCooldownDuration_sec 0x00C781EC)
        // elapses, the warning is emitted again
        rig.T += 11_000;
        rig.Frame(CubeAhead(), head: -0.15f);
        Assert.Equal(warnings + 1, rig.Log.Count(l => l.Contains("not connected")));
    }

    /// <summary>
    /// A cube whose pose is already Dirty and which is not where it should be is forgotten after two
    /// misses. This is the first of the two cases in <c>CheckForUnobservedObjects</c>: not visible, with
    /// nothing behind it, and Dirty (the branch at 0x0062211E). An empty view is exactly "nothing
    /// behind" - the occluder list holds only what the camera saw this frame.
    /// </summary>
    [Fact]
    public void ADirtyCubeThatIsNotWhereItShouldBeIsForgottenAfterTwoMisses()
    {
        if (NoLibrary) return;
        using var rig = new WorldRig();
        rig.Frame(CubeAhead(), head: -0.15f);
        var o = rig.Vision.World.GetObjectById(7)!;
        Assert.True(o.IsLocated);
        rig.Vision.World.MarkDirty(7);                 // as an ObjectMoved from the cube does
        // same view, cube gone
        var r1 = rig.Frame(null, head: -0.15f);
        Assert.Empty(r1.Forgotten);
        Assert.Equal(1, o.UnobservedCount);
        Assert.True(o.IsLocated);
        var r2 = rig.Frame(null, head: -0.15f);
        Assert.Single(r2.Forgotten);
        Assert.Equal(PoseState.Unknown, o.PoseState);
        Assert.False(rig.Vision.Locator.IsLocated(7));
        Assert.Null(rig.Vision.World.GetLocatedObjectById(7));
    }

    /// <summary>
    /// A cube whose pose is still Known and which vanishes from an otherwise empty view is <b>not</b>
    /// forgotten. Neither case applies: it is not visible, because with nothing in the occluder list
    /// <c>KnownMarker::IsVisibleFrom</c> comes back NOTHING_BEHIND (0x0087E87A), and the other case
    /// wants a Dirty pose. The robot has to see through to something behind where the cube was, or have
    /// been told the cube moved, before it gives the cube up.
    /// </summary>
    [Fact]
    public void AKnownCubeThatVanishesFromAnEmptyViewIsKept()
    {
        if (NoLibrary) return;
        using var rig = new WorldRig();
        rig.Frame(CubeAhead(), head: -0.15f);
        var o = rig.Vision.World.GetObjectById(7)!;
        Assert.Equal(PoseState.Known, o.PoseState);

        for (int i = 0; i < 4; i++) Assert.Empty(rig.Frame(null, head: -0.15f).Forgotten);
        Assert.Equal(PoseState.Known, o.PoseState);
        Assert.Equal(0, o.UnobservedCount);
        Assert.True(rig.Vision.Locator.IsLocated(7));
    }

    [Fact]
    public void ACubeOutOfViewIsNotMarkedUnobserved()
    {
        if (NoLibrary) return;
        using var rig = new WorldRig();
        rig.Frame(CubeAhead(), head: -0.15f);
        // robot turned 90 degrees away: the cube is behind the field of view, not "should be visible"
        for (int i = 0; i < 5; i++) rig.Frame(null, angle: 1.57f, head: -0.15f);
        var o = rig.Vision.World.GetObjectById(7)!;
        Assert.Equal(0, o.UnobservedCount);
        Assert.True(o.IsLocated);
        Assert.False(rig.Vision.Locator.IsVisibleFromCamera(7));
    }

    [Fact]
    public void UnobservedChecksAreSkippedWhileMovingOrRotatingFast()
    {
        if (NoLibrary) return;
        using var rig = new WorldRig();
        rig.Frame(CubeAhead(), head: -0.15f);
        var o = rig.Vision.World.GetObjectById(7)!;
        // Make the object eligible for a miss: a Dirty pose with nothing behind it (0x0062211E). A Known
        // object that is merely not visible is skipped by the "should have been seen" test first, so the
        // gate would never be exercised.
        rig.Vision.World.MarkDirty(7);
        for (int i = 0; i < 4; i++) rig.Frame(null, head: -0.15f, flags: RobotStatusFlag.IsMoving);
        Assert.Equal(0, o.UnobservedCount);            // the moving gate (IS_MOVING) skipped the pass
        // the body gate reads ImuData.rateZ (C3.3), not the robot state's gyro
        for (int i = 0; i < 4; i++) rig.Frame(null, head: -0.15f, imuRateZ: 0.5f);
        Assert.Equal(0, o.UnobservedCount);            // the rotating gate skipped it too
        rig.Frame(null, head: -0.15f);                 // still: the miss advances
        Assert.Equal(1, o.UnobservedCount);
        Assert.True(o.IsLocated);
    }

    /// <summary>
    /// M11-004: the rotating gate is applied on the offline <c>ProcessImage</c> path too (not only
    /// <c>ProcessCapture</c>), so an IMU rate reaches <c>BlockWorld::CheckForUnobservedObjects</c> through
    /// the public entry point the offline rig uses.
    /// </summary>
    [Fact]
    public void TheRotatingGateReachesBlockWorldThroughProcessImage()
    {
        if (NoLibrary) return;
        using var rig = new WorldRig();
        rig.Frame(CubeAhead(), head: -0.15f);
        var o = rig.Vision.World.GetObjectById(7)!;
        rig.Vision.World.MarkDirty(7);                 // eligible for a miss (Dirty + nothing behind)
        rig.Frame(null, head: -0.15f, imuRateZ: 0.5f); // WorldRig.Frame drives Vision.ProcessImage
        Assert.Equal(0, o.UnobservedCount);            // the gate skipped the pass
        rig.Frame(null, head: -0.15f);                 // still: the miss advances, so the object was eligible
        Assert.Equal(1, o.UnobservedCount);
    }

    /// <summary>
    /// M11-004 / H2: <c>MovementComponent::WasMoving(timestamp)</c> reads only the <c>IS_MOVING</c> bit
    /// (bit 0) of the <c>HistRobotState</c> nearest the timestamp (lambda 0x00642672,
    /// <c>HistRobotState[+0x58] &amp; 1</c>). <c>AreWheelsMoving</c> is bit 15 and must not skip the pass.
    /// </summary>
    [Fact]
    public void OnlyTheIsMovingBitSkipsTheUnobservedPass()
    {
        if (NoLibrary) return;
        using var rig = new WorldRig();
        rig.Frame(CubeAhead(), head: -0.15f);
        var o = rig.Vision.World.GetObjectById(7)!;
        rig.Vision.World.MarkDirty(7);                 // the Dirty-with-nothing-behind case applies
        rig.Frame(null, head: -0.15f, flags: RobotStatusFlag.AreWheelsMoving);
        Assert.Equal(1, o.UnobservedCount);            // AreWheelsMoving alone does not gate
        Assert.True(o.IsLocated);
    }

    /// <summary>
    /// M11-004 / C3.3: <c>WasHeadRotatingTooFast</c> 0x00656260 reads <c>ImuData.rateY</c> (ImuData+8) and
    /// <c>WasBodyRotatingTooFast</c> 0x00656384 reads <c>rateZ</c> (+0xC); <c>|rate| &gt; 0.174533</c>
    /// (0x3E32B8C2) is true, and a missing bracket returns true (fail-safe, 0x656306..0x656342).
    /// </summary>
    [Fact]
    public void TheRotatingGateReadsImuRateYAndRateZ()
    {
        var imu = new ImuDataHistory();
        imu.Add(100, 0, 0, 0, 0);
        imu.Add(200, 0, 0.3f, 0, 0);
        Assert.True(imu.WasHeadRotatingTooFast(150, BlockWorld.MaxRotationRateRadPerSec));
        Assert.False(imu.WasBodyRotatingTooFast(150, BlockWorld.MaxRotationRateRadPerSec));
        Assert.True(imu.WasRotatingTooFast(150, BlockWorld.MaxRotationRateRadPerSec));
        // no bracket: fail-safe true; empty history too
        Assert.True(imu.WasRotatingTooFast(500, BlockWorld.MaxRotationRateRadPerSec));
        Assert.True(new ImuDataHistory().WasRotatingTooFast(100, BlockWorld.MaxRotationRateRadPerSec));
    }

    /// <summary>
    /// M11-007 / C3.4: a new <c>PoseConfirmation</c> starts at count 1, the first sighting does not confirm;
    /// the second matching sighting makes 2 and <c>IsReferencePoseConfirmed</c> (0x506340) is
    /// <c>count &gt; 1</c>; a mismatching sighting resets to 1 (0x506A46..0x506A4E).
    /// </summary>
    [Fact]
    public void TheFirstSightingDoesNotConfirmAndTheSecondDoes()
    {
        if (NoLibrary) return;
        using var rig = new WorldRig();
        rig.Frame(CubeAhead(), head: -0.15f);
        var o = rig.Vision.World.GetObjectById(7)!;
        Assert.Equal(1, o.PoseConfirmationCount);
        Assert.False(o.IsPoseConfirmed);
        rig.Frame(CubeAhead(), head: -0.15f);
        Assert.Equal(2, o.PoseConfirmationCount);
        Assert.True(o.IsPoseConfirmed);
        Assert.True(rig.Vision.World.IsObjectConfirmedAtObservedPose(o, o.Pose));
        // a mismatching sighting (70 mm away, over the 35.2 mm extent tolerance) resets the count
        rig.Frame(CubeAhead(220), head: -0.15f);
        Assert.Equal(1, o.PoseConfirmationCount);
        Assert.False(o.IsPoseConfirmed);
    }

    /// <summary>
    /// M11-004 / C3.3: the object-match tolerance is the object's extent times 0.8 (a cube's 44 mm gives
    /// 35.2 mm) and the rotation tolerance is pi/4 (thunk 0x4E025C with 0.8 at 0x4E028C, thunk 0x4E0290);
    /// the primary match is the closest located object within them (predicate 0x6281DA).
    /// </summary>
    [Fact]
    public void TheObjectMatchToleranceIsTheExtentTimesZeroPointEightAndFortyFiveDegrees()
    {
        var tol = BlockWorld.ObjectMatchToleranceMm(ObjectType.Block_LIGHTCUBE1);
        Assert.Equal(35.2, tol.X, 6);
        Assert.Equal(35.2, tol.Y, 6);
        Assert.Equal(35.2, tol.Z, 6);
        Assert.Equal(Math.PI / 4, BlockWorld.ObjectMatchAngleRad, 9);

        if (NoLibrary) return;
        using var rig = new WorldRig();
        rig.Frame(CubeAhead(), head: -0.15f);
        var o = rig.Vision.World.GetObjectById(7)!;
        var near = o.Pose;
        Assert.Same(o, rig.Vision.World.FindObjectMatchForObservation(ObjectType.Block_LIGHTCUBE1, near));
        Assert.Same(o, rig.Vision.World.FindObjectMatchForObservation(ObjectType.Block_LIGHTCUBE1,
            new Pose3d(near.Rotation, near.Translation + new Vec3(0, 20, 0))));
        Assert.Null(rig.Vision.World.FindObjectMatchForObservation(ObjectType.Block_LIGHTCUBE1,
            new Pose3d(near.Rotation, near.Translation + new Vec3(0, 40, 0))));
        Assert.Same(o, rig.Vision.World.FindObjectMatchForObservation(ObjectType.Block_LIGHTCUBE1,
            new Pose3d(Mat3.AboutZ(0.7) * near.Rotation, near.Translation)));
        Assert.Null(rig.Vision.World.FindObjectMatchForObservation(ObjectType.Block_LIGHTCUBE1,
            new Pose3d(Mat3.AboutZ(0.9) * near.Rotation, near.Translation)));
    }

    /// <summary>
    /// M11-037 / C3.2: the block-configuration update runs on both the normal and the empty-observed-list
    /// paths (0x6250D4 is reached from both, then 0x62520C). The stacked-pose update runs only on the normal
    /// path (0x6250D0; the empty branch jumps to 0x6250D4).
    /// </summary>
    [Fact]
    public void TheFrameSequenceRunsTheBlockConfigurationHookOnBothPaths()
    {
        if (NoLibrary) return;
        using var rig = new WorldRig();
        int calls = 0;
        rig.Vision.World.BlockConfigurationManagerUpdate = () => calls++;
        rig.Frame(CubeAhead(), head: -0.15f);   // normal path
        Assert.Equal(1, calls);
        rig.Frame(null, head: -0.15f);          // empty-observed-list path
        Assert.Equal(2, calls);
    }

    [Fact]
    public void AMovedReportMakesTheCubeDirtyAndASightingMakesItKnownAgain()
    {
        if (NoLibrary) return;
        using var rig = new WorldRig();
        rig.Frame(CubeAhead(), head: -0.15f);
        var o = rig.Vision.World.GetObjectById(7)!;
        var changes = new List<(PoseState, PoseState)>();
        rig.Vision.World.PoseStateChanged += (_, a, b) => changes.Add((a, b));
        rig.Send(new ObjectMoved { Timestamp = rig.T, ObjectID = 7, AxisOfAccel = UpAxis.ZPositive });
        Assert.Equal(PoseState.Dirty, o.PoseState);
        Assert.True(o.IsLocated);                 // Dirty still counts as located, as the cube-moved strategy needs
        rig.Frame(CubeAhead(160, 0, 0.3), head: -0.15f);
        Assert.Equal(PoseState.Known, o.PoseState);
        Assert.Equal(new[] { (PoseState.Known, PoseState.Dirty), (PoseState.Dirty, PoseState.Known) }, changes);
        Assert.True((o.Pose.Translation - CubeAhead(160, 0, 0.3).Translation).Length < 6);
    }

    [Fact]
    public void ACubeSeenByTwoFacesIsOneObjectWithAJointPose()
    {
        if (NoLibrary) return;
        using var rig = new WorldRig();
        // yawed 40 degrees so the front and right faces both face the camera
        var cube = CubeAhead(130, -10, -0.7);
        var r = rig.Frame(cube, head: -0.2f);
        Assert.True(r.Markers.Count >= 2, $"{r.Markers.Count} markers: {string.Join(",", r.Markers.Select(m => m.Code))} {rig.Vision.Detector.Quads.LastStats}");
        Assert.Single(r.Objects);
        Assert.Equal(2, r.Objects[0].Markers.Count);
        Assert.True((r.Objects[0].Object.Pose.Translation - cube.Translation).Length < 5, $"{r.Objects[0].Object.Pose.Translation} vs {cube.Translation}");
    }

    [Fact]
    public void TheRealLocatorFiresTheCubeMovedReactionOnlyWhenTheCubeIsOutOfSight()
    {
        if (NoLibrary) return;
        using var rig = new WorldRig();
        rig.Frame(CubeAhead(), head: -0.15f);
        var behavior = new AcknowledgeCubeMovedBehavior(rig.Vision.Locator);
        using var strategy = new CubeMovedReactionStrategy(rig.Robot, behavior, rig.Vision.Locator, rig.Vision.World);
        Assert.True(strategy.HasLocator);
        // The CubeMoved STBI (M10-003, gap1 8) returns "running || IsRunnable" through C6 (M10-004); no animation
        // library is loaded here, so the real bound behaviour's IsRunnable is false. A runnable stand-in supplies the
        // gate, while the strategy still writes its target onto the bound behaviour (CubeMoved+0x124).
        var runnable = M10Support.RunnableBehaviour();
        var ctx = new BehaviorContext { Robot = rig.Robot, Triggers = new AnimationTriggerMap() };
        using var manager = new BehaviorManager(ctx);
        manager.AddReaction(strategy, behavior);          // enables the trigger-8 gate the message handler needs

        // the world model saw the cube: the strategy's record is marked observed
        strategy.ObjectObserved(7);
        // the cube starts moving while the camera is still on it: no reaction (IsVisibleFrom is true)
        rig.Send(new ObjectMoved { Timestamp = rig.T, ObjectID = 7, AxisOfAccel = UpAxis.ZPositive });
        rig.T += 1200;
        rig.State(head: -0.15f);
        Assert.True(rig.Vision.Locator.IsVisibleFromCamera(7));
        Assert.False(strategy.ShouldTrigger(ctx, null, 0, runnable));

        // the robot has turned away: the located pose is outside the camera's view
        rig.T += 33;
        rig.State(angle: 1.6f, head: -0.15f);
        Assert.True(rig.Vision.Locator.IsLocated(7));
        Assert.False(rig.Vision.Locator.IsVisibleFromCamera(7));
        Assert.True(strategy.ShouldTrigger(ctx, null, 0, runnable));
        Assert.Equal(7u, behavior.TargetObjectId);      // the strategy writes the target onto its bound behaviour (DerivedStateTests)
    }

    [Fact]
    public void TurnTowardsPoseComputesTheEnginesAngleAndHeadTilt()
    {
        var robot = HeadGeometry.RobotPose(new RobotPose { X = 100, Y = 50, Angle = 0.5f });
        var target = new Pose3d(Mat3.Identity, robot.Apply(new Vec3(200, 100, 22)));
        double turn = TurnTowardsPose.RelativeTurnRad(robot, target);
        Assert.Equal(Math.Atan2(100, 200), turn, 6);
        var cal = CameraCalibration.Nominal();
        double head = TurnTowardsPose.HeadAngleToSee(cal, robot, target.Translation);
        Assert.InRange(head, HeadGeometry.MinHeadAngleRad, HeadGeometry.MaxHeadAngleRad);
        // with that head angle the target projects to the image's middle row
        var cam = new CameraModel(cal, HeadGeometry.CameraPoseInWorld(robot, head));
        var turned = HeadGeometry.RobotPose(new RobotPose { X = 100, Y = 50, Angle = (float)(0.5 + turn) });
        var cam2 = new CameraModel(cal, HeadGeometry.CameraPoseInWorld(turned, head));
        var p = cam2.Project(target.Translation);
        Assert.NotNull(p);
        Assert.InRange(p!.Value.X, 150, 170);
        _ = cam;
    }

    // ------------------------------------------------------------------ AcknowledgeObject

    [Fact]
    public void ObjectPositionUpdatedFiresOnAFirstSightingAndAgainOnlyAfterAnEightyMillimetreMove()
    {
        // No marker library is needed: the observations are recorded directly (4c), so this runs everywhere.
        using var rig = new WorldRig();
        var behavior = new AcknowledgeObjectBehavior(rig.Vision.World);
        using var strategy = new ObjectPositionUpdatedStrategy(rig.Vision.World, behavior);
        // Robot::GetLastImageTimeStamp (M11): a nonzero stamp, so RobotReactedToId (4h) takes effect.
        strategy.LastImageTimestamp = () => 1u;
        var ctx = new BehaviorContext { Robot = rig.Robot, Triggers = new AnimationTriggerMap() };
        // The strategy writes its targets onto the class it is bound to (AcknowledgeObject+0x14C); the runnable
        // gate C6 checks is supplied by a runnable stand-in, because no animation assets are loaded offline and
        // SteppedBehavior.IsRunnable needs the library.
        var runnable = M10Support.RunnableBehaviour();

        // 4c/4e: a first sighting has no reacted time yet, so it is a target at once.
        strategy.RecordObservation(7, new Pose3d(Mat3.Identity, new Vec3(150, 0, 22)), 1, enabled: true);
        Assert.True(strategy.ShouldTrigger(ctx, null, 0, runnable));
        Assert.Equal(new uint[] { 7 }, behavior.PendingTargets);

        // gap1 8: ObjectPositionUpdated may not interrupt itself; that is the manager's C5 predicate, declared here.
        Assert.False(strategy.CanInterruptSelf);

        // 4h/4d: reacted; the same pose is no longer a target.
        strategy.RobotReactedToId(7);
        Assert.False(strategy.ShouldTrigger(ctx, null, 0, runnable));

        // a 40 mm shift is within the 80 mm tolerance
        strategy.RecordObservation(7, new Pose3d(Mat3.Identity, new Vec3(150, 40, 22)), 2, enabled: true);
        Assert.False(strategy.ShouldTrigger(ctx, null, 0, runnable));

        // 100 mm away is a new position
        strategy.RecordObservation(7, new Pose3d(Mat3.Identity, new Vec3(250, 0, 22)), 3, enabled: true);
        Assert.True(strategy.ShouldTrigger(ctx, null, 0, runnable));

        // a 69 degree turn is over the 45 degree angle tolerance
        strategy.RobotReactedToId(7);
        strategy.RecordObservation(7, new Pose3d(Mat3.AboutZ(1.2), new Vec3(250, 0, 22)), 4, enabled: true);
        Assert.True(strategy.ShouldTrigger(ctx, null, 0, runnable));
    }

    [Fact]
    public void AcknowledgeObjectTurnsVerifiesWithTwoImagesAndPlaysTheReaction()
    {
        if (NoLibrary) return;
        using var rig = new WorldRig();
        rig.Frame(CubeAhead(150), head: -0.15f);
        var turns = new List<(uint, double)>();
        rig.Vision.Locator.TurnOverride = (id, max, ct) => { turns.Add((id, max)); return Task.FromResult(true); };
        var behavior = new AcknowledgeObjectBehavior(rig.Vision.World, rig.Vision.Locator) { Clock = () => 0 };
        behavior.SetTargets(new uint[] { 7 });
        var ctx = new BehaviorContext { Robot = rig.Robot, Triggers = new AnimationTriggerMap() };
        var reacted = new List<uint>();
        behavior.ReactedTo += reacted.Add;
        Assert.Equal(new uint[] { 7 }, behavior.PendingTargets);      // runnable once the animation library is loaded (IsRunnable gates on it)
        behavior.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        Assert.Equal(AcknowledgeObjectBehavior.Phase.Turning, behavior.CurrentPhase);
        Assert.Equal(new[] { (7u, AcknowledgeObjectBehavior.MaxTurnAngleRad) }, turns);

        // the turn's completion is posted; the next tick takes it and starts the visual verification
        SpinUntil(() => { behavior.Update(ctx, 0); return behavior.CurrentPhase == AcknowledgeObjectBehavior.Phase.Verifying; });
        Assert.Equal(7u, behavior.CurrentTarget);
        // one sighting is not enough, two are (NumImagesToWaitFor = 2)
        rig.Frame(CubeAhead(150), head: -0.15f);
        behavior.Update(ctx, 33);
        Assert.Equal(AcknowledgeObjectBehavior.Phase.Verifying, behavior.CurrentPhase);
        rig.Frame(CubeAhead(150), head: -0.15f);
        behavior.Update(ctx, 66);
        // no animation library offline: the trigger play fails and the iteration finishes; the target counts as reacted to
        SpinUntil(() => { behavior.Update(ctx, 99); return reacted.Count == 1; });
        Assert.Equal(new uint[] { 7 }, reacted);
        Assert.Contains(behavior.Trace, l => l.Contains("VisuallyVerifyObjectAction"));
        Assert.Contains(behavior.Trace, l => l.Contains("FinishIteration"));
        behavior.Stop(BehaviorStopReason.Completed);
    }

    [Fact]
    public void AcknowledgeObjectGivesUpOnATargetTheWorldModelCannotLocate()
    {
        using var rig = new WorldRig();                 // runs without the marker library: the world model needs no decoder here
        var behavior = new AcknowledgeObjectBehavior(rig.Vision.World) { Clock = () => 0 };
        behavior.SetTargets(new uint[] { 42 });
        var ctx = new BehaviorContext { Robot = rig.Robot, Triggers = new AnimationTriggerMap() };
        var reacted = new List<uint>();
        behavior.ReactedTo += reacted.Add;
        behavior.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        Assert.False(behavior.Update(ctx, 0));
        Assert.Equal(new uint[] { 42 }, reacted);
        Assert.Contains(behavior.Trace, l => l.Contains("can't get it from blockworld"));
    }

    private static void SpinUntil(Func<bool> cond, int ms = 3000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!cond()) { if (sw.ElapsedMilliseconds > ms) throw new TimeoutException("condition not met"); Thread.Sleep(5); }
    }

    // ------------------------------------------------------------------ the captured robot

    /// <summary>
    /// The fw2457 probe capture carries 28 camera frames of a room with no marker in it. The decoder must run over
    /// real JPEGs without error and must not hallucinate cube markers.
    /// </summary>
    [Fact]
    public void TheCapturedRoomFramesDecodeAndCarryNoCubeMarkers()
    {
        // without the library the frames still decode and the detector finds quads but decodes none; the
        // no-cube-marker claim holds either way
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "hw_fw2457_probe.log");
        if (!File.Exists(path)) return;
        var line = new Regex(@"^(\S+) (TX|RX) ((?:[0-9a-f]{2} ?)+)$");
        using var robot = CozmoRobot.CreateOffline();
        var frames = new List<CameraFrame>();
        robot.Camera.FrameReceived += frames.Add;
        foreach (var raw in File.ReadLines(path))
        {
            var m = line.Match(raw.Trim('﻿'));
            if (!m.Success || m.Groups[2].Value != "RX") continue;
            try { robot.Transport.ProcessIncoming(Hex.Parse(m.Groups[3].Value)); } catch (FormatException) { }
        }
        Assert.True(frames.Count >= 20, $"only {frames.Count} frames");
        var det = new MarkerDetector();
        int cubeMarkers = 0, decoded = 0;
        foreach (var f in frames)
        {
            var gray = GrayImage.FromFrame(f);
            Assert.Equal(f.Width, gray.Width);
            decoded++;
            cubeMarkers += det.Detect(gray, f.Timestamp).Count(m => CubeGeometry.CubeTypeForMarker(m.Code) is not null);
        }
        Assert.Equal(frames.Count, decoded);
        Assert.Equal(0, cubeMarkers);
    }

    /// <summary>
    /// The clustering and flat-snap tolerances are the engine's, not this stack's.
    /// <c>ObservableObjectLibrary::CreateObjectsFromMarkers</c> passes 5 mm (0x40A00000 at 0x006254F4)
    /// and 0.0872665 rad (0x3DB2B8C3 at 0x00625498) to <c>ClusterObjectPoses</c>, and
    /// <c>ObjectPoseConfirmer::UpdatePoseInInstance</c> passes 0.349066 rad (0x3EB2B8C2 at 0x00505F16)
    /// to <c>ClampPoseToFlat</c>. This stack had 20 mm, 10 degrees and 8 degrees.
    /// </summary>
    [Fact]
    public void TheClusteringAndFlatSnapTolerancesAreTheEngines()
    {
        Assert.Equal(5.0, BlockWorld.ClusterDistanceMm);
        Assert.Equal(0.0872665, BlockWorld.ClusterAngleRad, 6);
        Assert.Equal(0.349066, BlockWorld.FlatClampAngleRad, 6);

        // a pose tilted by 15 degrees snaps, one tilted by 25 does not
        var flat = new Pose3d(Mat3.AboutZ(0.3), new Vec3(100, 0, 22));
        var tilted15 = new Pose3d(Mat3.AxisAngle(new Vec3(1, 0, 0), 15 * Math.PI / 180) * flat.Rotation, flat.Translation);
        var tilted25 = new Pose3d(Mat3.AxisAngle(new Vec3(1, 0, 0), 25 * Math.PI / 180) * flat.Rotation, flat.Translation);
        var snapped = BlockWorld.ClampPoseToFlat(tilted15);
        Assert.Equal(1.0, Math.Abs(snapped.Rotation[2, 2]), 4);
        var kept = BlockWorld.ClampPoseToFlat(tilted25);
        Assert.True(Math.Abs(kept.Rotation[2, 2]) < 0.95, "a 25 degree tilt is beyond the engine's 20 and must not snap");
    }

    /// <summary>
    /// M11-003 / C3.1: <c>KnownMarker::_canonicalCorners3d</c> is stored in the order
    /// <c>[0]=(−0.5,0,+0.5)</c>, <c>[1]=(−0.5,0,−0.5)</c>, <c>[2]=(+0.5,0,+0.5)</c>,
    /// <c>[3]=(+0.5,0,−0.5)</c> (static ctor 0x004DD7D8, ctor 0x004E9636), and
    /// <c>VisionMarker::Extract</c> pairs canonical corner <c>i</c> with detected
    /// <c>cornerReorder[label][i]</c> (0x8A0130..0x8A018A; table 0xDC73AC).
    /// </summary>
    [Fact]
    public void TheCanonicalCornerOrderAndTheDecoderPairingAreTheEngines()
    {
        Assert.Equal(new Vec3(-0.5, 0, 0.5), KnownMarker.CanonicalCorners[0]);
        Assert.Equal(new Vec3(-0.5, 0, -0.5), KnownMarker.CanonicalCorners[1]);
        Assert.Equal(new Vec3(0.5, 0, 0.5), KnownMarker.CanonicalCorners[2]);
        Assert.Equal(new Vec3(0.5, 0, -0.5), KnownMarker.CanonicalCorners[3]);
        if (NoLibrary) return;
        // K5: the first four cornerReorder rows (0xDC73AC: 0,1,2,3, 1,3,0,2, 3,2,1,0, 2,0,3,1)
        Assert.Equal(new[] { 0, 1, 2, 3 }, Lib.CornerReorder.Take(4));
        Assert.Equal(new[] { 1, 3, 0, 2 }, Lib.CornerReorder.Skip(4).Take(4));
        Assert.Equal(new[] { 3, 2, 1, 0 }, Lib.CornerReorder.Skip(8).Take(4));
        Assert.Equal(new[] { 2, 0, 3, 1 }, Lib.CornerReorder.Skip(12).Take(4));
    }
}
