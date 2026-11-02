using Cozmo.Protocol;
namespace Cozmo.Robot.Vision;

// fidelity: M11-098, M11-035
// Immutable tag 0x49 payload; Pack follows 0x713E2A..3F22, size 44.
internal readonly record struct NativeObservedMotion(uint Timestamp,float ImageArea,short ImageX,short ImageY,
    float GroundArea,short GroundX,short GroundY,float Top,short TopX,short TopY,
    float Left,short LeftX,short LeftY,float Right,short RightX,short RightY)
{
    internal byte[] PackBody()=>new CladWriter().U32(Timestamp).F32(ImageArea).I16(ImageX).I16(ImageY)
        .F32(GroundArea).I16(GroundX).I16(GroundY).F32(Top).I16(TopX).I16(TopY)
        .F32(Left).I16(LeftX).I16(LeftY).F32(Right).I16(RightX).I16(RightY).ToArray();
}
