namespace Cozmo.Robot.Vision.Jpeg;

// fidelity: M3-018
// cv::resize(src, dst, Size(w, h), 0, 0, INTER_LINEAR) for 8-bit images, as the shipped libopencv_imgproc.so computes it
// (J37, J41..J45): double-precision scales, float positions and weights, 2048-scaled signed 16-bit weights, an integer
// horizontal pass and the separately-truncated vertical pass. The FPSCR rounding mode is round-to-nearest (the runtime
// assumption recorded for J42, the same as M1-029).
internal static class OpenCvResize
{
    private const int CoefScale = 1 << 11;       // INTER_RESIZE_COEF_SCALE
    private static readonly double DblEpsilon = BitConverter.Int64BitsToDouble(0x3CB0000000000000);      // DBL_EPSILON = 2^-52

    private static int CvFloor(float v)
    {
        int i = (int)v;
        return i - (i > v ? 1 : 0);
    }

    private static short SaturateShort(float v)
    {
        // cvRound (vcvtr.s32.f32 under round-to-nearest-even) then saturate to the signed 16-bit range (J42)
        int r = (int)MathF.Round(v, MidpointRounding.ToEven);
        if (r < short.MinValue) r = short.MinValue;
        if (r > short.MaxValue) r = short.MaxValue;
        return (short)r;
    }

    /// <summary>
    /// The engine's <c>Resize</c> wrapper (0x00870486..0x008704DE, J37): nothing happens when the sizes are already equal,
    /// otherwise cv::resize with fx = fy = 0 and INTER_LINEAR (method 1).
    /// </summary>
    public static CvMat ResizeLinear(CvMat src, int dstCols, int dstRows)
    {
        if (src.IsEmpty) throw new OpenCvException(-215, "ssize.area() > 0", "void cv::resize(cv::InputArray, cv::OutputArray, cv::Size, double, double, int)", "/Users/build/buildAgent/work/2b4c7202ccc51830/opencv-3.1.0/modules/imgproc/src/imgwarp.cpp", 3229);
        if (src.Cols == dstCols && src.Rows == dstRows) return src;
        int cn = src.Channels;
        double invScaleX = (double)dstCols / src.Cols;
        double invScaleY = (double)dstRows / src.Rows;
        double scaleX = 1.0 / invScaleX, scaleY = 1.0 / invScaleY;
        int iscaleX = (int)Math.Round(scaleX, MidpointRounding.ToEven);
        int iscaleY = (int)Math.Round(scaleY, MidpointRounding.ToEven);
        bool isAreaFast = Math.Abs(scaleX - iscaleX) < DblEpsilon && Math.Abs(scaleY - iscaleY) < DblEpsilon;
        if (isAreaFast && iscaleX == 2 && iscaleY == 2)
            throw new NotSupportedException("J41: INTER_LINEAR with an exact 2x reduction is switched to INTER_AREA by cv::resize; that kernel has no row and is not on the engine's path (160x240 -> 320x240)");
        int width = dstCols * cn;
        var xofs = new int[width];
        var ialpha = new short[width * 2];
        var yofs = new int[dstRows];
        var ibeta = new short[dstRows * 2];
        for (int dx = 0; dx < dstCols; dx++)
        {
            float fx = (float)((dx + 0.5) * scaleX - 0.5);
            int sx = CvFloor(fx);
            fx -= sx;
            if (sx < 0) { fx = 0; sx = 0; }
            if (sx + 1 >= src.Cols)
            {
                if (sx >= src.Cols - 1) { fx = 0; sx = src.Cols - 1; }
            }
            for (int k = 0; k < cn; k++) xofs[dx * cn + k] = sx * cn + k;
            short a0 = SaturateShort((1f - fx) * CoefScale);
            short a1 = SaturateShort(fx * CoefScale);
            for (int k = 0; k < cn; k++)
            {
                ialpha[(dx * cn + k) * 2] = a0;
                ialpha[(dx * cn + k) * 2 + 1] = a1;
            }
        }
        for (int dy = 0; dy < dstRows; dy++)
        {
            float fy = (float)((dy + 0.5) * scaleY - 0.5);
            int sy = CvFloor(fy);
            fy -= sy;
            yofs[dy] = sy;
            ibeta[dy * 2] = SaturateShort((1f - fy) * CoefScale);
            ibeta[dy * 2 + 1] = SaturateShort(fy * CoefScale);
        }
        var dst = new byte[(long)dstRows * width];
        int srcStride = src.Cols * cn;
        var row0 = new int[width];
        var row1 = new int[width];
        for (int dy = 0; dy < dstRows; dy++)
        {
            int sy0 = Clip(yofs[dy], src.Rows);
            int sy1 = Clip(yofs[dy] + 1, src.Rows);
            HResize(src.Data, sy0 * srcStride, xofs, ialpha, row0, width, cn, src.Cols);
            HResize(src.Data, sy1 * srcStride, xofs, ialpha, row1, width, cn, src.Cols);
            int b0 = ibeta[dy * 2], b1 = ibeta[dy * 2 + 1];
            int o = dy * width;
            for (int x = 0; x < width; x++)
            {
                int v = (((b0 * (row0[x] >> 4)) >> 16) + ((b1 * (row1[x] >> 4)) >> 16) + 2) >> 2;
                dst[o + x] = (byte)v;
            }
        }
        return new CvMat(dstRows, dstCols, cn, dst);
    }

    private static int Clip(int x, int b) => x >= 0 ? (x < b ? x : b - 1) : 0;

    private static void HResize(byte[] src, int rowStart, int[] xofs, short[] ialpha, int[] outRow, int width, int cn, int srcCols)
    {
        for (int dx = 0; dx < width; dx++)
        {
            int sx = xofs[dx];
            int a0 = ialpha[dx * 2], a1 = ialpha[dx * 2 + 1];
            int s1 = sx + cn < srcCols * cn ? sx + cn : sx;      // only read with a1 == 0 at the right edge (fx = 0 there)
            outRow[dx] = src[rowStart + sx] * a0 + src[rowStart + s1] * a1;
        }
    }
}
