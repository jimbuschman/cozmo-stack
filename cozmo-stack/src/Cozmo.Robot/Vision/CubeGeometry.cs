using Cozmo.Protocol;

namespace Cozmo.Robot.Vision;

/// <summary><c>Anki::Cozmo::Block::FaceName</c>: the six faces of a block (NATIVE, from <c>LookupBlockInfo</c>).</summary>
public enum BlockFace : byte { Front = 0, Left = 1, Back = 2, Right = 3, Top = 4, Bottom = 5 }

/// <summary>
/// <c>ObjectFamily</c> (decompiled Unity enum): Invalid −1, Unknown, Block, LightCube, Ramp, Charger, Mat,
/// MarkerlessObject, CustomObject.
/// </summary>
public enum ObjectFamily : sbyte { Invalid = -1, Unknown = 0, Block = 1, LightCube = 2, Ramp = 3, Charger = 4, Mat = 5, MarkerlessObject = 6, CustomObject = 7 }

/// <summary>One marker on a known object: which marker, where it sits on the object, and how big it is.</summary>
public sealed record KnownMarker(MarkerType Code, BlockFace Face, Pose3d PoseOnObject, double SizeMm)
{
    /// <summary>The marker's height when it is not square (the charger's marker is 20 x 27 mm); defaults to <see cref="SizeMm"/>.</summary>
    public double HeightMm { get; init; } = double.NaN;
    private double H => double.IsNaN(HeightMm) ? SizeMm : HeightMm;

    /// <summary>
    /// <c>KnownMarker::_canonicalCorners3d</c> (initialiser 0x004DD7D8, ctor 0x004E9636): the unit marker
    /// lies in the X–Z plane with its normal along −Y; the four points are stored in memory order
    /// <c>[0]=(−½, 0, +½)</c>, <c>[1]=(−½, 0, −½)</c>, <c>[2]=(+½, 0, +½)</c>, <c>[3]=(+½, 0, −½)</c>
    /// (C3.1). <c>Get3dCorners</c> scales X by the marker's width and Z by its height. The decoder pairs
    /// canonical corner <c>i</c> with the detected corner <c>cornerReorder[label][i]</c>
    /// (<c>VisionMarker::Extract</c> 0x8A0130..0x8A018A).
    /// </summary>
    // fidelity: M11-003
    public static readonly Vec3[] CanonicalCorners =
    {
        new(-0.5, 0, 0.5), new(-0.5, 0, -0.5), new(0.5, 0, 0.5), new(0.5, 0, -0.5),
    };

    /// <summary>
    /// The marker's own four corners in the marker's frame, in the engine's memory order (0..3): the
    /// canonical corners with X scaled by the marker's width and Z by its height, as <c>Get3dCorners</c>
    /// scales them. The two are scaled separately because not every marker is square - the charger's is
    /// 20 x 27 - and a solve given square corners for it returns a pose with the error that mismatch
    /// implies.
    /// </summary>
    public Vec3[] Corners3d() => CanonicalCorners.Select(c => new Vec3(c.X * SizeMm, 0, c.Z * H)).ToArray();

    /// <summary>The marker's four corners in the object's frame, in the engine's memory order.</summary>
    public Vec3[] CornersOnObject() => Corners3d().Select(PoseOnObject.Apply).ToArray();

    /// <summary>The marker's four corners in the world for an object pose.</summary>
    public Vec3[] CornersInWorld(Pose3d objectPose) => CornersOnObject().Select(objectPose.Apply).ToArray();

    /// <summary>The outward normal of the marker in the object's frame (the marker's −Y axis, which faces the camera when seen).</summary>
    public Vec3 NormalOnObject => PoseOnObject.Rotation * new Vec3(0, -1, 0);
}

/// <summary>
/// <c>Block::LookupBlockInfo</c> (0x004E4C8C) and <c>Block::AddFace</c> (0x004E4FE0 region): the light cube is
/// 44.0 mm on a side with 25.0 mm markers, and each face's marker pose is built as
/// <c>Pose3d(angle, axis, translation)</c>:
///
/// | face | rotation | translation |
/// | --- | --- | --- |
/// | Front  | −90° about Z | (−½, 0, 0) |
/// | Left   | 180° about Z | (0, +½, 0) |
/// | Back   | +90° about Z | (+½, 0, 0) |
/// | Right  | 0 | (0, −½, 0) |
/// | Top    | 120° about (−0.577, 0.577, −0.577) | (0, 0, +½) |
/// | Bottom | 120° about (0.577, −0.577, −0.577) | (0, 0, −½) |
///
/// (½ = half the cube size). LIGHTCUBE1's faces carry codes FRONT→6, LEFT→7, BACK→4, RIGHT→8, TOP→9, BOTTOM→5
/// (LightCubeI_*), cube 2 adds 6 and cube 3 adds 12. All NATIVE.
///
/// Note that the marker on the face the engine calls Front sits at −X: the "front" is the face that looks at
/// a robot approaching from −X, and the cube's own +X axis points from that face through the cube.
/// </summary>
// fidelity: M11-003
public static class CubeGeometry
{
    public const double CubeSizeMm = 44.0;
    public const double MarkerSizeMm = 25.0;
    // Block::AddFace's binary32 literals (0x004E5442..0x004E55D6), each the engine's bit pattern widened exactly.
    // fidelity: M11-003
    public const uint MinusHalfPiBits = 0xBFC90FDB;   // movw/movt at 0x004E5442/0x004E5448
    public const uint HalfPiBits = 0x3FC90FDB;        // 0x004E54EA/0x004E54F0
    public const uint PiBits = 0x40490FDB;            // 0x004E557C/0x004E5582
    public const uint TwoPiOverThreeBits = 0x40060A92; // 0x004E5490/0x004E5496 and 0x004E55D6/0x004E55DC
    public const uint AxisBits = 0x3F13CD3A;          // 0x004E54A0/0x004E54A4 (and 0xBF13CD3A negated)
    private static double F32(uint bits) => BitConverter.Int32BitsToSingle(unchecked((int)bits));
    private static readonly double Ax = F32(AxisBits);
    private static readonly double MinusHalfPi = F32(MinusHalfPiBits), HalfPi = F32(HalfPiBits), Pi = F32(PiBits), TwoPiOver3 = F32(TwoPiOverThreeBits);

    // The engine's face-def vector order (Block::LookupBlockInfo 0x004E4C8C; M12-021 H1) is
    // Front, Back, Left, Right, Top, Bottom. The per-face geometry is the same whichever order this list is
    // walked in; it is written in the engine's order so the two line up.
    private static readonly (BlockFace Face, double AngleRad, Vec3 Axis, Vec3 Offset)[] FaceTable =
    {
        (BlockFace.Front, MinusHalfPi, new Vec3(0, 0, 1), new Vec3(-0.5, 0, 0)),
        (BlockFace.Back, HalfPi, new Vec3(0, 0, 1), new Vec3(0.5, 0, 0)),
        (BlockFace.Left, Pi, new Vec3(0, 0, 1), new Vec3(0, 0.5, 0)),
        (BlockFace.Right, 0, new Vec3(0, 0, 1), new Vec3(0, -0.5, 0)),
        (BlockFace.Top, TwoPiOver3, new Vec3(-Ax, Ax, -Ax), new Vec3(0, 0, 0.5)),
        (BlockFace.Bottom, TwoPiOver3, new Vec3(Ax, -Ax, -Ax), new Vec3(0, 0, -0.5)),
    };

    /// <summary>Marker code offsets within a cube's six codes: Back=0, Bottom=1, Front=2, Left=3, Right=4, Top=5 (the enum order).</summary>
    private static int FaceCodeOffset(BlockFace f) => f switch
    {
        BlockFace.Back => 0, BlockFace.Bottom => 1, BlockFace.Front => 2, BlockFace.Left => 3, BlockFace.Right => 4, BlockFace.Top => 5, _ => throw new ArgumentOutOfRangeException(nameof(f)),
    };

    /// <summary>The object type carrying a marker code, or null when the code is not a light-cube marker.</summary>
    public static ObjectType? CubeTypeForMarker(MarkerType code)
    {
        int c = (int)code;
        if (c >= 4 && c <= 9) return ObjectType.Block_LIGHTCUBE1;
        if (c >= 10 && c <= 15) return ObjectType.Block_LIGHTCUBE2;
        if (c >= 16 && c <= 21) return ObjectType.Block_LIGHTCUBE3;
        return null;
    }

    public static bool IsCube(ObjectType t) => t is ObjectType.Block_LIGHTCUBE1 or ObjectType.Block_LIGHTCUBE2 or ObjectType.Block_LIGHTCUBE3;

    /// <summary>The six known markers of a light cube, in the engine's face order.</summary>
    public static IReadOnlyList<KnownMarker> CubeMarkers(ObjectType type)
    {
        int baseCode = type switch
        {
            ObjectType.Block_LIGHTCUBE1 => 4, ObjectType.Block_LIGHTCUBE2 => 10, ObjectType.Block_LIGHTCUBE3 => 16,
            _ => throw new ArgumentException($"{type} is not a light cube", nameof(type)),
        };
        var list = new List<KnownMarker>(6);
        foreach (var (face, angle, axis, offset) in FaceTable)
        {
            var pose = new Pose3d(angle, axis, offset * CubeSizeMm);
            list.Add(new KnownMarker((MarkerType)(baseCode + FaceCodeOffset(face)), face, pose, MarkerSizeMm));
        }
        return list;
    }

    /// <summary>Whether this object type is an active (radio-connected) object: only the light cubes are.</summary>
    public static bool IsActiveObjectType(ObjectType t) => IsCube(t);

    /// <summary>The known markers of any object type this stack models: a light cube's six, the charger's one.</summary>
    public static IReadOnlyList<KnownMarker> MarkersFor(ObjectType type) =>
        type == ObjectType.Charger_Basic ? ChargerGeometry.Markers : CubeMarkers(type);

    /// <summary>The object's size (x, y, z) in its own frame.</summary>
    public static Vec3 SizeOf(ObjectType type) => type == ObjectType.Charger_Basic ? ChargerGeometry.Size : CubeSize;

    /// <summary>The known marker for a code, with its object type; null for codes no modelled object carries.</summary>
    public static (ObjectType Type, KnownMarker Marker)? LookupMarker(MarkerType code)
    {
        if (code == MarkerType.Charger) return (ObjectType.Charger_Basic, ChargerGeometry.Markers[0]);
        if (CubeTypeForMarker(code) is not { } type) return null;
        var m = CubeMarkers(type).First(k => k.Code == code);
        return (type, m);
    }

    /// <summary>The engine's <c>ObservableObject::GetSize()</c> for a cube: 44 mm on every axis.</summary>
    public static Vec3 CubeSize => new(CubeSizeMm, CubeSizeMm, CubeSizeMm);

    /// <summary>
    /// <c>ObservableObject::GetDimInParentFrame&lt;'Z'&gt;</c> 0x00557794: the object's extent along the
    /// parent axis its own Z lies closest to. The axis choice is the one <c>IsRestingFlat</c> makes (the
    /// largest-magnitude component of the object's Z in the parent frame); a cube is 44 on every axis.
    /// </summary>
    // fidelity: M12-012
    public static double DimInParentFrameZ(ObservableObject o) => DimInParentFrameZ(o, o.Pose.Rotation);

    /// <summary>The same with an explicit rotation: <c>IsPoseTooHigh</c> reads the PASSED pose's rotation (<c>GetRotationMatrix(GetTransform(pose))</c>, 0x00877968..0x0087797A), not the object's own.</summary>
    public static double DimInParentFrameZ(ObservableObject o, Mat3 r)
    {
        double x = Math.Abs(r[0, 2]), y = Math.Abs(r[1, 2]), z = Math.Abs(r[2, 2]);
        var size = SizeOf(o.Type);
        if (z >= x && z >= y) return size.Z;
        if (x >= y) return size.X;
        return size.Y;
    }

    /// <summary>
    /// <c>ObservableObject::IsPoseTooHigh(pose, f1, f2, f3)</c> 0x00877954 (PLT 0x4ACFF4): returns
    /// <c>D*f1 + f2 + 1e-5 &lt; D*f3 + pose.translation.z</c>, with <c>D = GetDimInParentFrame&lt;'Z'&gt;</c>.
    /// <c>CanStackOnTopOfObject</c> calls it with <c>f1 = 1.0, f2 = 15.0, f3 = 0.5</c>, i.e.
    /// <c>pose.z &gt; 0.5*D + 15.0 + 1e-5</c> (M12-012 C-E4). The 15.0 is M13-007's.
    /// </summary>
    // fidelity: M12-012
    public static bool IsPoseTooHigh(ObservableObject o, double f1, double f2, double f3) => IsPoseTooHigh(o, o.Pose, (float)f1, (float)f2, (float)f3);

    /// <summary>
    /// <c>ObservableObject::IsPoseTooHigh(pose, f1, f2, f3)</c> 0x00877954..0x008779BC in binary32, in the engine's operation order: <c>D = GetDimInParentFrame&lt;'Z'&gt;</c> (float); <c>s0 = D*f1</c>
    /// (<c>vmul.f32</c> 0x00877990); <c>s16 = D*f3</c> (0x00877994); <c>s18 = s0 + f2</c> (0x00877998); <c>s0 = s16 + pose.z</c> (0x008779AA); <c>s2 = s18 + 1e-5f</c> (literal 0x008779C8 = 0x3727C5AC,
    /// 0x008779AE); true when <c>s2 &lt; s0</c> (<c>vcmpe.f32</c>, <c>mi</c>: ordered).
    /// </summary>
    // fidelity: M12-012, M15-020
    public static bool IsPoseTooHigh(ObservableObject o, Pose3d pose, float f1, float f2, float f3)
    {
        float d = (float)DimInParentFrameZ(o, pose.Rotation);                 // 0x00877968..0x0087797A
        float s0 = d * f1;
        float s16 = d * f3;
        float s18 = s0 + f2;
        float high = s16 + (float)pose.Translation.Z;
        float low = s18 + BitConverter.Int32BitsToSingle(0x3727C5AC);
        return low < high;
    }
}
