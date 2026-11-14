using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// MIN-AREA-RECT-FOOTPRINT-1 (records M13-007, M13-024, M11-105). BlockWorld's FindObjectOnTopOrUnderneathHelper 0x0062601C, ported as
/// BlockConfigurationManager.FindObjectOnTopOrUnderneathInRootFrame (reached from BlockWorld.cs FindObjectOnTopOrUnderneathHelper at MarkObjectDirty,
/// MarkObjectUnknown, ClearLocatedObjectHelper, UpdatePoseOfStackedObjects, CanInteract and SetObjectAsAttachedToLift), reads each object's OWN slots:
/// the reference's +0x50 at the re-rooted refPose, padding 0 (H3 0x0062604C..0x0062605C) and +0x2C through GetDimInParentFrame&lt;Z&gt; (H4 0x00626070);
/// each candidate's +0x50, padding 0 (P3 0x0062BA7A..0x0062BA86) and, only after the quads intersect, its +0x2C (P5 0x0062BAA4..0x0062BAB6). Height test
/// P6 0x0062BAC2..0x0062BB0E: |targetZ - (cand.z + candDim * (onTop ? -0.5 : +0.5))| &lt;= tol + 1e-5f, targetZ = ref.z + refDim * (onTop ? +0.5 : -0.5).
///
/// Every expected quad below was computed by the census's independent port (scratch port.py / cases.py, a copy of
/// re-analysis/evidence/m13-minarearect/port.py, the port validated bit for bit against the shipped OpenCV on 270 inputs) from the cited formulas
/// (census MIN-AREA-RECT-FOOTPRINT-1 Q4), never by running this code; the f32 bits are given. Quads are in storage order [s0, s3, s1, s2].
/// Sizes: cube 44^3 (0x004E382C); Charger (96, 80, 31) (0x004EA672 -> +0xF0, 0x004E9D12..0x004E9D32); CliffDetection 0xF (20, 40, 50) (GetSizeByType 0x005026EC).
/// </summary>
// reach: unit (FindObjectOnTopOrUnderneathInRootFrame and the slot +0x50 called directly on objects with chosen flat poses; the objects are not seen
// through vision nor inserted by their producers. M13FindOnTopSameRootTests.M13_007_C9_TheClearSearchDoesNotExcludeMarkerlessObjects drives the same
// helper through BlockWorld's ClearLocatedObjectHelper.)
public sealed class MinAreaRectFootprintTests
{
    private static ObservableObject Obj(uint id, ObjectType type, Mat3 r, double x, double y, double z) =>
        new(id, type, CubeGeometry.IsCube(type) ? CubeGeometry.CubeMarkers(type) : Array.Empty<KnownMarker>())
        { Pose = new Pose3d(r, new Vec3(x, y, z)), PoseState = PoseState.Known };

    private static float F(uint bits) => BitConverter.UInt32BitsToSingle(bits);

    private static Quadrilateral Quad(ObservableObject o)
    {
        Assert.True(o.TryObservableBoundingQuad50(o.NativePose, 0f, out var q));
        return q!;
    }

    private static void Corner(Quadrilateral q, int i, float x, float y)
    {
        Assert.Equal(BitConverter.SingleToUInt32Bits(x), BitConverter.SingleToUInt32Bits(q[i].X));
        Assert.Equal(BitConverter.SingleToUInt32Bits(y), BitConverter.SingleToUInt32Bits(q[i].Y));
    }

    // Census Q4: the tilted coordinates may move by one ulp with the stack's double-to-float quaternion (the f32 quaternion fed to Rotation3d::operator*
    // is M11-065's boundary); the integer-valued corners cannot.
    private static void WithinOneUlp(float expected, float actual) =>
        Assert.InRange(Math.Abs(BitConverter.SingleToInt32Bits(actual) - BitConverter.SingleToInt32Bits(expected)), 0, 1);

    /// <summary>T1 (M13-007, M13-024): a cube tilted 0.5 rad about Y at (250, 0, 66) on an identity cube at (200, 0, 22). Census Q4: the tilted quad is
    /// x in [220.14581 (0x435C2554), 279.85419 (0x438BED56)], y in [-22, 22], so it meets the reference's [178, 222] (220.146 &lt; 222). Its dim Z is 44
    /// (rotated Z axis is +3); surface 66 - 22 = 44 = targetZ 22 + 22 -> FOUND. (A yaw-only 44-wide quad at 250 would be [228, 272] and miss.)</summary>
    [Fact]
    public void M13_007_T1_ACubeTiltedAboutYOnACubeIsFound()
    {
        var a = Obj(1, ObjectType.Block_LIGHTCUBE1, Mat3.Identity, 200, 0, 22);
        var b = Obj(2, ObjectType.Block_LIGHTCUBE2, Mat3.AboutY(0.5), 250, 0, 66);
        var qa = Quad(a);
        Corner(qa, 0, F(0x43320000), F(0xC1B00000)); Corner(qa, 1, F(0x43320000), F(0x41B00000));
        Corner(qa, 2, F(0x435E0000), F(0xC1B00000)); Corner(qa, 3, F(0x435E0000), F(0x41B00000));
        var qb = Quad(b);
        WithinOneUlp(F(0x435C2554), qb[0].X); Assert.Equal(F(0xC1B00000), qb[0].Y);
        WithinOneUlp(F(0x435C2554), qb[1].X); Assert.Equal(F(0x41B00000), qb[1].Y);
        WithinOneUlp(F(0x438BED56), qb[2].X); Assert.Equal(F(0xC1B00000), qb[2].Y);
        WithinOneUlp(F(0x438BED56), qb[3].X); Assert.Equal(F(0x41B00000), qb[3].Y);
        Assert.Same(b, BlockConfigurationManager.FindObjectOnTopOrUnderneathInRootFrame(a, new[] { b }, true, 15.0, out bool unavailable));
        Assert.False(unavailable);
    }

    /// <summary>T2 (M13-007, M11-105): the charger as the REFERENCE, identity at (150, 0, 0); a cube identity at (200, 0, 38). Census Q4: the charger's
    /// quad is (150,-40),(150,40),(246,-40),(246,40), which contains the cube's [178, 222]; targetZ = 0 + 31*0.5 = 15.5 (the charger's +0x2C z, H4);
    /// surface 38 - 22 = 16; |15.5 - 16| = 0.5 &lt;= 15.00001 -> FOUND. (The removed planar stand-in, 50 mm between centres, said not found.)</summary>
    [Fact]
    public void M13_007_T2_AChargerReferenceFindsTheCubeOnIt()
    {
        var charger = Obj(20, ObjectType.Charger_Basic, Mat3.Identity, 150, 0, 0);
        var cube = Obj(1, ObjectType.Block_LIGHTCUBE1, Mat3.Identity, 200, 0, 38);
        var q = Quad(charger);
        Corner(q, 0, F(0x43160000), F(0xC2200000)); Corner(q, 1, F(0x43160000), F(0x42200000));
        Corner(q, 2, F(0x43760000), F(0xC2200000)); Corner(q, 3, F(0x43760000), F(0x42200000));
        Assert.Same(cube, BlockConfigurationManager.FindObjectOnTopOrUnderneathInRootFrame(charger, new[] { cube }, true, 15.0, out bool unavailable));
        Assert.False(unavailable);
    }

    /// <summary>T3 (M13-007): the charger as a CANDIDATE, identity at (130, 0, 50), over a cube identity at (200, 0, 22). Census Q4: the charger's quad is
    /// (130,-40),(130,40),(226,-40),(226,40), which meets [178, 222]; surface 50 - 31*0.5 = 34.5 (P5, read after P4); |44 - 34.5| = 9.5 &lt;= 15.00001 -> FOUND.
    /// (The stand-in, 70 mm between centres, said not found.)</summary>
    [Fact]
    public void M13_007_T3_AChargerCandidateIsFoundWithItsOwnQuadAndSize()
    {
        var cube = Obj(1, ObjectType.Block_LIGHTCUBE1, Mat3.Identity, 200, 0, 22);
        var charger = Obj(20, ObjectType.Charger_Basic, Mat3.Identity, 130, 0, 50);
        var q = Quad(charger);
        Corner(q, 0, F(0x43020000), F(0xC2200000)); Corner(q, 1, F(0x43020000), F(0x42200000));
        Corner(q, 2, F(0x43620000), F(0xC2200000)); Corner(q, 3, F(0x43620000), F(0x42200000));
        Assert.Same(charger, BlockConfigurationManager.FindObjectOnTopOrUnderneathInRootFrame(cube, new[] { charger }, true, 15.0, out bool unavailable));
        Assert.False(unavailable);
    }

    /// <summary>T4 (M13-007, M11-105): a markerless CliffDetection (0xF) candidate identity at (200, 0, 69) over a cube identity at (200, 0, 22). Census Q4:
    /// its quad is (190,-20),(190,20),(210,-20),(210,20) (MarkerlessObject 0x00502C32), inside [178, 222]; its +0x2C size is (20, 40, 50), identity -> Z
    /// -> 50; surface 69 - 25 = 44 = targetZ -> FOUND. Derived from the same P5/P6 formula, not port.py: at z = 83 the surface is 58 and |44 - 58| = 14
    /// &lt;= 15.00001 -> FOUND, where a 44 mm height (the removed CubeGeometry.DimInParentFrameZ value for markerless types) would give |44 - 61| = 17 -> not
    /// found; at z = 95, |44 - 70| = 26 -> not found.</summary>
    [Theory]
    [InlineData(69.0, true)]
    [InlineData(83.0, true)]
    [InlineData(95.0, false)]
    public void M13_007_T4_AMarkerlessCandidateIsFoundWithItsSizeFifty(double z, bool expected)
    {
        var cube = Obj(1, ObjectType.Block_LIGHTCUBE1, Mat3.Identity, 200, 0, 22);
        var cliff = Obj(60, ObjectType.CliffDetection, Mat3.Identity, 200, 0, z);
        Assert.Equal(ObjectFamily.MarkerlessObject, cliff.Family);
        var q = Quad(cliff);
        Corner(q, 0, F(0x433E0000), F(0xC1A00000)); Corner(q, 1, F(0x433E0000), F(0x41A00000));
        Corner(q, 2, F(0x43520000), F(0xC1A00000)); Corner(q, 3, F(0x43520000), F(0x41A00000));
        var found = BlockConfigurationManager.FindObjectOnTopOrUnderneathInRootFrame(cube, new[] { cliff }, true, 15.0, out bool unavailable);
        Assert.Equal(expected, ReferenceEquals(found, cliff));
        Assert.False(unavailable);
    }

    /// <summary>M13-024 M9 (census Q2): the calipers' area keep is <c>vcmpe.f32 area, minarea</c> then <c>bhi</c> at imgproc 0x0009CCC8 skipping the store.
    /// <c>bhi</c> is taken when C=1 and Z=0: "greater than" or "unordered". So the store happens for a smaller or EQUAL area (the last minimal one wins),
    /// and is skipped for a greater area and for a NaN on either side.</summary>
    // reach: unit (the keep predicate called directly; no shipped or production footprint is known to produce a NaN area).
    [Fact]
    public void M13_024_TheAreaKeepSkipsAnUnorderedComparison()
    {
        Assert.True(MinAreaRectF.KeepsArea(0.5f, 1f));
        Assert.True(MinAreaRectF.KeepsArea(1f, 1f));
        Assert.False(MinAreaRectF.KeepsArea(2f, 1f));
        Assert.False(MinAreaRectF.KeepsArea(float.NaN, float.MaxValue));
        Assert.False(MinAreaRectF.KeepsArea(1f, float.NaN));
    }
}
