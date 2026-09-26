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

    // ------------------------------------------------------------------ front end

    /// <summary>
    /// The dark mask is the engine's: <c>BinomialFilter</c> 0x008A2344 is the separable five-tap
    /// [1 4 6 4 1] with a <c>&gt;&gt; 4</c> after each pass and the edge pixel standing in for the taps that
    /// fall outside, and the binarize loop 0x00890BB6 marks a pixel dark when
    /// <c>(filtered * 0xCCCC) &gt;&gt; 16 &gt; pixel</c> - 0xCCCC being <c>scaleImage_thresholdMultiplier</c> at
    /// the parameters' +0xC (0x00875314), with one pyramid level from +4.
    /// </summary>
    [Fact]
    public void TheDarkMaskIsABinomialFilterAndAQ16Threshold()
    {
        var p = new QuadDetectorParameters();
        Assert.Equal(0xCCCC, p.DarkThresholdQ16);
        Assert.Equal(1, p.PyramidLevels);

        var flat = new GrayImage(8, 8);
        flat.Fill(100);
        Assert.All(QuadDetector.BinomialFilter(flat).Pixels, v => Assert.Equal(100, v));

        // one bright pixel in a dark row: the kernel's own weights, 1 4 6 4 1 over 16
        var spike = new GrayImage(9, 1);
        spike.Pixels[4] = 160;
        var f = QuadDetector.BinomialFilter(spike);
        Assert.Equal(new byte[] { 0, 0, 10, 40, 60, 40, 10, 0, 0 }, f.Pixels);

        // and the threshold: 130 filtered against 100 is dark, against 105 is not
        Assert.True((130 * p.DarkThresholdQ16) >> 16 > 100);
        Assert.False((130 * p.DarkThresholdQ16) >> 16 > 105);
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
    /// How small a marker the front end will take, which is decided by <c>component_minimumNumPixels</c>
    /// (100, the parameters' +0x4C): a marker twenty pixels on a side leaves enough dark pixels and one of
    /// sixteen does not. That floor is the engine's, and it is what puts a ceiling on how far away a cube
    /// can be seen.
    /// </summary>
    [Fact]
    public void AMarkerSmallerThanTheComponentFloorIsNotFound()
    {
        if (NoLibrary) return;
        var found = new List<(int Side, int Markers)>();
        foreach (int side in new[] { 20, 16 })
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
        Assert.Equal(100, new QuadDetectorParameters().MinComponentPixels);
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
        NvResult? got = null;
        nv.Read(CameraCalibration.NvEntryTag, CameraSettings.CalibrationReadLength, r => got = r);
        // The CONTROL capture's parts arrived 5,6,7,0,3,2,1,4,15: NVOpResult.Length is the index, so arrival
        // order must not matter. Index 0 held the valid 56-byte calibration; the other blobs are other indices.
        void Part(int index, byte[] data) =>
            rig.Send(new NVOpResult { Tag = CameraCalibration.NvEntryTag, Op = 0, Result = NvStorageComponent.ResultMore, Length = index, Data = data });
        foreach (var i in new[] { 5, 6, 7, 3, 2, 1, 4, 15 }) Part(i, new byte[] { 1, 2, 3, 4 });
        Part(0, bytes);
        rig.Send(new NVOpResult { Tag = CameraCalibration.NvEntryTag, Op = 0, Result = NvStorageComponent.ResultOkay, Length = 0, Data = Array.Empty<byte>() });

        Assert.NotNull(got);
        Assert.Equal(0, got!.Value.Result);
        Assert.Equal(CameraCalibration.WireSize, got.Value.Data.Length);       // exactly 56, not concatenated
        Assert.Equal(291.2, CameraCalibration.Unpack(got.Value.Data).FocalLengthY, 3);

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
        robot.Engine.NvStorage!.Read(CameraCalibration.NvEntryTag, CameraSettings.CalibrationReadLength, _ => { });
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
        public VisionFrameResult Frame(Pose3d? cube, float x = 0, float y = 0, float angle = 0, float head = 0, RobotStatusFlag flags = 0, float gz = 0)
        {
            State(x, y, angle, head, flags, gz);
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

    [Fact]
    public void AnUnconnectedCubeIsSeenButNotAdded()
    {
        if (NoLibrary) return;
        using var rig = new WorldRig(connectCube: false);
        var r = rig.Frame(CubeAhead(), head: -0.15f);
        Assert.NotEmpty(r.Markers);
        Assert.Empty(r.Objects);
        Assert.Contains(rig.Log, l => l.Contains("not connected"));
        Assert.Empty(rig.Vision.World.Objects);
        // the offline switch the tools use
        rig.Vision.World.AllowUnconnectedObjects = true;
        var r2 = rig.Frame(CubeAhead(), head: -0.15f);
        Assert.Single(r2.Objects);
        Assert.Equal(1u, r2.Objects[0].Object.ObjectId);
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
        for (int i = 0; i < 4; i++) rig.Frame(null, head: -0.15f, flags: RobotStatusFlag.IsMoving);
        Assert.Equal(0, o.UnobservedCount);
        for (int i = 0; i < 4; i++) rig.Frame(null, head: -0.15f, gz: 0.5f);
        Assert.Equal(0, o.UnobservedCount);
        Assert.True(o.IsLocated);
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
}
