namespace Cozmo.Robot.Vision.Jpeg;

// fidelity: M3-001, M3-018
// jddctmgr.c as shipped (J17, J20, J79): the range-limit table, the choice of the inverse DCT body by the component's scaled size and
// the multiplier table (method 0, JDCT_ISLOW: the raw quantisers as ints). The bodies are in JpegIdct / JpegIdctKernels.
internal sealed partial class JpegDecoder
{
    private const int CenterJSample = 128;
    private byte[] _rangeTable = Array.Empty<byte>();     // the 1408-byte allocation of J20; sample_range_limit starts at index 256
    private const int RangeBase = 256;

    // prepare_range_limit_table (J20): 1408 bytes
    private void PrepareRangeLimitTable()
    {
        var table = new byte[5 * 256 + CenterJSample];
        // [0..255] = 0 (negative extension), [256..511] = 0..255, [512..895] = 255, [896..1279] = 0, [1280..1407] = 0..127
        for (int i = 0; i < 256; i++) table[256 + i] = (byte)i;
        for (int i = 512; i < 512 + 384; i++) table[i] = 255;
        for (int i = 0; i < CenterJSample; i++) table[1280 + i] = (byte)i;
        _rangeTable = table;
    }

    // jddctmgr start_pass: the multiplier table of a needed component is its quantisation table as ints (method islow)
    private void IdctStartPass()
    {
        foreach (var compptr in CompInfo)
        {
            if (!JpegIdct.Supported(compptr.DctHScaledSize, compptr.DctVScaledSize))
                throw Fatal("JERR_BAD_DCTSIZE");
            compptr.DctTable ??= new int[DctSize2];
            if (!compptr.ComponentNeeded || compptr.CurMethod == 0) continue;
            var qtbl = compptr.QuantTable;
            if (qtbl == null) continue;
            compptr.CurMethod = 0;
            for (int i = 0; i < DctSize2; i++) compptr.DctTable[i] = qtbl[i];
        }
    }

    private void InverseDct(JpegComponent compptr, JpegBlock block, SampleArray output, int outRow, int outCol)
    {
        JpegIdct.Transform(compptr.DctHScaledSize, compptr.DctVScaledSize, block.A, block.O, compptr.DctTable!, _rangeTable,
            output.Data, outRow * output.Stride + outCol, output.Stride);
    }
}
