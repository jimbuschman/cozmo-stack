namespace Cozmo.Robot.Vision.Jpeg;

// fidelity: M3-001, M3-018
// jdcoefct.c (J68..J70, J106): the coefficient controller. A single-scan sequential file is decoded MCU by MCU straight into
// the IDCT (decompress_onepass); anything with several scans (and every progressive file) is first absorbed into zero-initialised
// whole-image coefficient arrays (consume_data) and transformed afterwards (decompress_data or, for an unfinished progressive
// file, decompress_smooth_data).
internal sealed partial class JpegDecoder
{
    private long _mcuCtr;
    private int _mcuVertOffset;
    private int _mcuRowsPerIMcuRow;
    private short[][]? _wholeImage;        // per component: [row * rowStride + col] * 64
    private int[]? _wholeStride;           // blocks per row (padded to a multiple of h_samp_factor)
    private readonly JpegBlock[] _mcuBuffer = new JpegBlock[DMaxBlocksInMcu];
    private short[] _mcuStorage = new short[DMaxBlocksInMcu * DctSize2];
    private bool _useCBuffer;
    private bool _smoothing;
    private int[]? _coefBitsLatch;

    // jinit_d_coef_controller
    private void InitCoefController(bool needFullBuffer)
    {
        _useCBuffer = needFullBuffer;
        _mcuCtr = 0;
        _mcuVertOffset = 0;
        if (needFullBuffer)
        {
            _wholeImage = new short[NumComponents][];
            _wholeStride = new int[NumComponents];
            for (int ci = 0; ci < NumComponents; ci++)
            {
                var c = CompInfo[ci];
                long w = RoundUp(c.WidthInBlocks, c.HSampFactor);
                long h = RoundUp(c.HeightInBlocks, c.VSampFactor);
                long total = w * h * DctSize2;
                if (total > int.MaxValue / 2) throw Fatal("JERR_OUT_OF_MEMORY");
                try
                {
                    _wholeImage[ci] = new short[total];
                }
                catch (OutOfMemoryException)
                {
                    throw Fatal("JERR_OUT_OF_MEMORY");
                }
                _wholeStride[ci] = (int)w;
            }
        }
        else
        {
            for (int i = 0; i < DMaxBlocksInMcu; i++) _mcuBuffer[i] = new JpegBlock(_mcuStorage, i * DctSize2);
        }
    }

    // start_input_pass (coef): input_iMCU_row = 0; start_iMCU_row
    private void CoefStartInputPass()
    {
        InputIMcuRow = 0;
        StartIMcuRow();
    }

    private void StartIMcuRow()
    {
        if (CompsInScan > 1)
        {
            _mcuRowsPerIMcuRow = 1;
        }
        else
        {
            if (InputIMcuRow < TotalIMcuRows - 1) _mcuRowsPerIMcuRow = CurCompInfo[0]!.VSampFactor;
            else _mcuRowsPerIMcuRow = CurCompInfo[0]!.LastRowHeight;
        }
        _mcuCtr = 0;
        _mcuVertOffset = 0;
    }

    // start_output_pass (coef)
    private void CoefStartOutputPass()
    {
        if (_wholeImage != null)
        {
            _smoothing = DoBlockSmoothing && SmoothingOk();
        }
        OutputIMcuRow = 0;
    }

    private bool SmoothingOk()
    {
        bool smoothingUseful = false;
        if (!ProgressiveMode || _coefBits == null) return false;
        _coefBitsLatch ??= new int[NumComponents * 6];
        for (int ci = 0; ci < NumComponents; ci++)
        {
            var q = CompInfo[ci].QuantTable;
            if (q == null) return false;
            if (q[0] == 0 || q[1] == 0 || q[8] == 0 || q[16] == 0 || q[9] == 0 || q[2] == 0) return false;
            var bits = _coefBits[ci];
            if (bits[0] < 0) return false;
            for (int coefi = 1; coefi <= 5; coefi++)
            {
                _coefBitsLatch[ci * 6 + coefi] = bits[coefi];
                if (bits[coefi] != 0) smoothingUseful = true;
            }
        }
        return smoothingUseful;
    }

    // inputctl->consume_input
    private int ConsumeInput()
    {
        if (_consumeIsData) return CoefConsumeData();
        return ConsumeMarkers();
    }

    private int CoefConsumeData()
    {
        if (!_useCBuffer) return JpegSuspended;     // dummy_consume_data
        // consume_data
        var buffers = new short[MaxCompsInScan][];
        for (int ci = 0; ci < CompsInScan; ci++) buffers[ci] = _wholeImage![CurCompInfo[ci]!.ComponentIndex];
        for (int yoffset = _mcuVertOffset; yoffset < _mcuRowsPerIMcuRow; yoffset++)
        {
            for (long mcuColNum = _mcuCtr; mcuColNum < McusPerRow; mcuColNum++)
            {
                int blkn = 0;
                for (int ci = 0; ci < CompsInScan; ci++)
                {
                    var compptr = CurCompInfo[ci]!;
                    int stride = _wholeStride![compptr.ComponentIndex];
                    long startCol = mcuColNum * compptr.McuWidth;
                    for (int yindex = 0; yindex < compptr.McuHeight; yindex++)
                    {
                        long row = InputIMcuRow * compptr.VSampFactor + yindex + yoffset;
                        for (int xindex = 0; xindex < compptr.McuWidth; xindex++)
                        {
                            _mcuBlocksScratch[blkn++] = new JpegBlock(buffers[ci], (int)((row * stride + startCol + xindex) * DctSize2));
                        }
                    }
                }
                if (!EntropyDecodeMcu(_mcuBlocksScratch))
                {
                    _mcuVertOffset = yoffset;
                    _mcuCtr = mcuColNum;
                    return JpegSuspended;
                }
            }
            _mcuCtr = 0;
        }
        if (++InputIMcuRow < TotalIMcuRows)
        {
            StartIMcuRow();
            return JpegRowCompleted;
        }
        FinishInputPass();
        return JpegScanCompleted;
    }

    private readonly JpegBlock[] _mcuBlocksScratch = new JpegBlock[DMaxBlocksInMcu];

    // decompress_data / decompress_onepass: fill the main controller's sample buffers for one iMCU row
    private int CoefDecompressData(SampleArray[] outputBuf)
    {
        if (!_useCBuffer) return DecompressOnePass(outputBuf);
        return _smoothing ? DecompressSmoothData(outputBuf) : DecompressDataFull(outputBuf);
    }

    private int DecompressOnePass(SampleArray[] outputBuf)
    {
        long lastMcuCol = McusPerRow - 1;
        long lastIMcuRow = TotalIMcuRows - 1;
        for (int yoffset = _mcuVertOffset; yoffset < _mcuRowsPerIMcuRow; yoffset++)
        {
            for (long mcuColNum = _mcuCtr; mcuColNum <= lastMcuCol; mcuColNum++)
            {
                // the entropy decoder expects a zeroed buffer; with lim_Se == 0 (DC only) it is not cleared (J68: "clears according to lim_Se"),
                // so an MCU the decoder skips (insufficient data) keeps the previous MCU's DC and is transformed again
                if (LimSe != 0) Array.Clear(_mcuStorage, 0, BlocksInMcu * DctSize2);
                if (!EntropyDecodeMcu(_mcuBuffer))
                {
                    _mcuVertOffset = yoffset;
                    _mcuCtr = mcuColNum;
                    return JpegSuspended;
                }
                int blkn = 0;
                for (int ci = 0; ci < CompsInScan; ci++)
                {
                    var compptr = CurCompInfo[ci]!;
                    if (!compptr.ComponentNeeded)
                    {
                        blkn += compptr.McuBlocks;
                        continue;
                    }
                    int usefulWidth = mcuColNum < lastMcuCol ? compptr.McuWidth : compptr.LastColWidth;
                    var output = outputBuf[compptr.ComponentIndex];
                    int outputRow = yoffset * compptr.DctVScaledSize;
                    long startCol = mcuColNum * compptr.McuSampleWidth;
                    for (int yindex = 0; yindex < compptr.McuHeight; yindex++)
                    {
                        if (InputIMcuRow < lastIMcuRow || yoffset + yindex < compptr.LastRowHeight)
                        {
                            long outputCol = startCol;
                            for (int xindex = 0; xindex < usefulWidth; xindex++)
                            {
                                InverseDct(compptr, _mcuBuffer[blkn + xindex], output, outputRow, (int)outputCol);
                                outputCol += compptr.DctHScaledSize;
                            }
                        }
                        blkn += compptr.McuWidth;
                        outputRow += compptr.DctVScaledSize;
                    }
                }
            }
            _mcuCtr = 0;
        }
        OutputIMcuRow++;
        if (++InputIMcuRow < TotalIMcuRows)
        {
            StartIMcuRow();
            return JpegRowCompleted;
        }
        FinishInputPass();
        return JpegScanCompleted;
    }

    private int DecompressDataFull(SampleArray[] outputBuf)
    {
        long lastIMcuRow = TotalIMcuRows - 1;
        while (InputScanNumber <= OutputScanNumber && !_eoiReached)
        {
            if (InputScanNumber == OutputScanNumber)
            {
                long delta = Ss == 0 ? 1 : 0;
                if (InputIMcuRow > OutputIMcuRow + delta) break;
            }
            if (ConsumeInput() == JpegSuspended) return JpegSuspended;
        }
        for (int ci = 0; ci < NumComponents; ci++)
        {
            var compptr = CompInfo[ci];
            if (!compptr.ComponentNeeded) continue;
            int blockRows;
            if (OutputIMcuRow < lastIMcuRow)
            {
                blockRows = compptr.VSampFactor;
            }
            else
            {
                blockRows = (int)(compptr.HeightInBlocks % compptr.VSampFactor);
                if (blockRows == 0) blockRows = compptr.VSampFactor;
            }
            var arr = _wholeImage![ci];
            int stride = _wholeStride![ci];
            var output = outputBuf[ci];
            int outputRow = 0;
            for (int blockRow = 0; blockRow < blockRows; blockRow++)
            {
                long row = OutputIMcuRow * compptr.VSampFactor + blockRow;
                long outputCol = 0;
                for (long blockNum = 0; blockNum < compptr.WidthInBlocks; blockNum++)
                {
                    InverseDct(compptr, new JpegBlock(arr, (int)((row * stride + blockNum) * DctSize2)), output, outputRow, (int)outputCol);
                    outputCol += compptr.DctHScaledSize;
                }
                outputRow += compptr.DctVScaledSize;
            }
        }
        if (++OutputIMcuRow < TotalIMcuRows) return JpegRowCompleted;
        return JpegScanCompleted;
    }

    // decompress_smooth_data (J69, J70)
    private int DecompressSmoothData(SampleArray[] outputBuf)
    {
        long lastIMcuRow = TotalIMcuRows - 1;
        while (InputScanNumber <= OutputScanNumber && !_eoiReached)
        {
            if (InputScanNumber == OutputScanNumber)
            {
                long delta = Ss == 0 ? 2 : 0;
                if (InputIMcuRow > OutputIMcuRow + delta) break;
            }
            if (ConsumeInput() == JpegSuspended) return JpegSuspended;
        }
        var workspace = new short[DctSize2];
        for (int ci = 0; ci < NumComponents; ci++)
        {
            var compptr = CompInfo[ci];
            if (!compptr.ComponentNeeded) continue;
            int blockRows;
            bool lastRow;
            if (OutputIMcuRow < lastIMcuRow)
            {
                blockRows = compptr.VSampFactor;
                lastRow = false;
            }
            else
            {
                blockRows = (int)(compptr.HeightInBlocks % compptr.VSampFactor);
                if (blockRows == 0) blockRows = compptr.VSampFactor;
                lastRow = true;
            }
            bool firstRow = OutputIMcuRow == 0;
            var arr = _wholeImage![ci];
            int stride = _wholeStride![ci];
            var q = compptr.QuantTable!;
            long Q00 = q[0], Q01 = q[1], Q10 = q[8], Q20 = q[16], Q11 = q[9], Q02 = q[2];
            var output = outputBuf[ci];
            int outputRow = 0;
            for (int blockRow = 0; blockRow < blockRows; blockRow++)
            {
                long absRow = OutputIMcuRow * compptr.VSampFactor + blockRow;
                long curRow = absRow;
                long prevRow = (firstRow && blockRow == 0) ? curRow : absRow - 1;
                long nextRow = (lastRow && blockRow == blockRows - 1) ? curRow : absRow + 1;
                int cur = (int)(curRow * stride) * DctSize2;
                int prev = (int)(prevRow * stride) * DctSize2;
                int next = (int)(nextRow * stride) * DctSize2;
                int DC1, DC2, DC3, DC4, DC5, DC6, DC7, DC8, DC9;
                DC1 = DC2 = DC3 = arr[prev];
                DC4 = DC5 = DC6 = arr[cur];
                DC7 = DC8 = DC9 = arr[next];
                long outputCol = 0;
                long lastBlockColumn = compptr.WidthInBlocks - 1;
                for (long blockNum = 0; blockNum <= lastBlockColumn; blockNum++)
                {
                    Array.Copy(arr, cur + (int)blockNum * DctSize2, workspace, 0, DctSize2);
                    if (blockNum < lastBlockColumn)
                    {
                        DC3 = arr[prev + (int)(blockNum + 1) * DctSize2];
                        DC6 = arr[cur + (int)(blockNum + 1) * DctSize2];
                        DC9 = arr[next + (int)(blockNum + 1) * DctSize2];
                    }
                    int Al;
                    if ((Al = _coefBitsLatch![ci * 6 + 1]) != 0 && workspace[1] == 0)
                        workspace[1] = Estimate(36 * Q00 * (DC4 - DC6), Q01, Al);
                    if ((Al = _coefBitsLatch[ci * 6 + 2]) != 0 && workspace[8] == 0)
                        workspace[8] = Estimate(36 * Q00 * (DC2 - DC8), Q10, Al);
                    if ((Al = _coefBitsLatch[ci * 6 + 3]) != 0 && workspace[16] == 0)
                        workspace[16] = Estimate(9 * Q00 * (DC2 + DC8 - 2 * DC5), Q20, Al);
                    if ((Al = _coefBitsLatch[ci * 6 + 4]) != 0 && workspace[9] == 0)
                        workspace[9] = Estimate(5 * Q00 * (DC1 - DC3 - DC7 + DC9), Q11, Al);
                    if ((Al = _coefBitsLatch[ci * 6 + 5]) != 0 && workspace[2] == 0)
                        workspace[2] = Estimate(9 * Q00 * (DC4 + DC6 - 2 * DC5), Q02, Al);
                    InverseDct(compptr, new JpegBlock(workspace, 0), output, outputRow, (int)outputCol);
                    DC1 = DC2; DC2 = DC3;
                    DC4 = DC5; DC5 = DC6;
                    DC7 = DC8; DC8 = DC9;
                    outputCol += compptr.DctHScaledSize;
                }
                outputRow += compptr.DctVScaledSize;
            }
        }
        if (++OutputIMcuRow < TotalIMcuRows) return JpegRowCompleted;
        return JpegScanCompleted;
    }

    // the K.8 estimate (J70): all in signed 32-bit integers, truncating division
    private static short Estimate(long numLong, long q, int al)
    {
        int num = unchecked((int)numLong);
        int qq = unchecked((int)q);
        int pred;
        if (num >= 0)
        {
            pred = unchecked((qq << 7) + num) / unchecked(qq << 8);
            if (al > 0 && pred >= (1 << al)) pred = (1 << al) - 1;
        }
        else
        {
            pred = unchecked((qq << 7) - num) / unchecked(qq << 8);
            if (al > 0 && pred >= (1 << al)) pred = (1 << al) - 1;
            pred = -pred;
        }
        return (short)pred;
    }
}
