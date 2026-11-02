namespace Cozmo.Robot.Vision;

// fidelity: M11-098
// Selected 10x10 ellipse at scale 2, native shape 2 and anchor (5,5).
internal static class NativeMotionKernel
{
    private static readonly string[] Rows={"0000010000","0011111110","0111111111","1111111111","1111111111","1111111111","1111111111","1111111111","0111111111","0011111110"};
    internal static byte[] Ellipse10()=>Rows.SelectMany(row=>row.Select(c=>(byte)(c-'0'))).ToArray();
}
