namespace Cozmo.Robot.Vision.Jpeg;

// fidelity: M3-001, M3-018
// jdmaster.c, jdapistd.c, jdmainct.c, jdpostct.c, jdsample.c and jdcolor.c as shipped: output geometry (J13, J21, J78, J77),
// master selection (J20, J23, J104, J105), the simple main controller (J106), the separate upsampler (J110: no fancy
// interpolation exists in this build; fullsize / h2v1 / h2v2 / integral replication) and the colour deconverters
// (J54..J56, J73, J74, J104).
internal sealed partial class JpegDecoder
{
    // ---- jpeg_calc_output_dimensions (J13, J77, J78; scale 1/1 only: OpenCV passes scale_denom = 1)
    private void CalcOutputDimensions()
    {
        if (_state != State.Ready) throw Fatal("JERR_BAD_STATE");
        CoreOutputDimensions();
        // chroma components are scaled up by the IDCT instead of being upsampled (J78)
        foreach (var compptr in CompInfo)
        {
            int ssize = MinDctHScaledSize;
            while (ssize <= DctSize && (MaxHSampFactor * MinDctHScaledSize) % (compptr.HSampFactor * ssize * 2) == 0) ssize *= 2;
            compptr.DctHScaledSize = ssize;
            ssize = MinDctVScaledSize;
            while (ssize <= DctSize && (MaxVSampFactor * MinDctVScaledSize) % (compptr.VSampFactor * ssize * 2) == 0) ssize *= 2;
            compptr.DctVScaledSize = ssize;
            // aspect ratio of the transform is capped at 2
            if (compptr.DctHScaledSize > compptr.DctVScaledSize * 2) compptr.DctHScaledSize = compptr.DctVScaledSize * 2;
            else if (compptr.DctVScaledSize > compptr.DctHScaledSize * 2) compptr.DctVScaledSize = compptr.DctHScaledSize * 2;
        }
        foreach (var compptr in CompInfo)
        {
            compptr.DownsampledWidth = DivRoundUp((long)ImageWidth * ((long)compptr.HSampFactor * compptr.DctHScaledSize), (long)MaxHSampFactor * BlockSize);
            compptr.DownsampledHeight = DivRoundUp((long)ImageHeight * ((long)compptr.VSampFactor * compptr.DctVScaledSize), (long)MaxVSampFactor * BlockSize);
        }
        switch (OutColorSpace)
        {
            case JpegColorSpace.Grayscale: OutColorComponents = 1; break;
            case JpegColorSpace.Rgb: case JpegColorSpace.YCbCr: OutColorComponents = 3; break;
            case JpegColorSpace.Cmyk: case JpegColorSpace.Ycck: OutColorComponents = 4; break;
            default: OutColorComponents = NumComponents; break;
        }
    }

    // jpeg_core_output_dimensions with scale_num/scale_denom = 1/1: block_size/block_size scaling
    private void CoreOutputDimensions()
    {
        // scale 1/1: the first k with scale_num * block_size <= scale_denom * k is k = block_size, so the output is
        // ceil(image * k / block_size) = the image size, transformed with block_size x block_size DCTs (J13, J77)
        OutputWidth = (int)DivRoundUp((long)ImageWidth * BlockSize, BlockSize);
        OutputHeight = (int)DivRoundUp((long)ImageHeight * BlockSize, BlockSize);
        MinDctHScaledSize = BlockSize;
        MinDctVScaledSize = BlockSize;
    }

    // ---- output side state
    private SampleArray[]? _mainBuffer;
    private bool _bufferFull;
    private int _rowgroupCtr;
    private int[] _rowgroupHeight = Array.Empty<int>();
    private int _nextRowOut;
    private long _rowsToGo;
    private SampleArray?[] _colorBuf = Array.Empty<SampleArray?>();
    private SampleArray?[] _upsampled = Array.Empty<SampleArray?>();     // the per-component row sources handed to the colour converter
    private int[] _upsampledRowOffset = Array.Empty<int>();
    private enum UpMethod { Noop, FullSize, H2V1, H2V2, Int }
    private UpMethod[] _upMethods = Array.Empty<UpMethod>();
    private int[] _hExpand = Array.Empty<int>();
    private int[] _vExpand = Array.Empty<int>();
    private enum ColorConv { Null, Grayscale, GrayRgb, YccRgb, RgbGray, RgbRgb, YcckCmyk, Rgb1Gray, Rgb1Rgb }
    private ColorConv _colorConv;
    private int[]? _crR, _cbB, _crG, _cbG;
    private int[]? _rgbYTab;

    // ---- jinit_master_decompress / master_selection (J20, J23, J104, J105)
    private void MasterSelection()
    {
        CalcOutputDimensions();
        PrepareRangeLimitTable();
        long samplesperrow = (long)OutputWidth * OutColorComponents;
        if ((uint)samplesperrow != samplesperrow) throw Fatal("JERR_WIDTH_OVERFLOW");
        // use_merged_upsample: do_fancy_upsampling is true on this path (J16), so the merged upsampler is never selected (J23)
        if (!DoFancyUpsampling && !CCIR601Sampling)
            throw new NotSupportedException("J23: merged upsampling needs do_fancy_upsampling == 0, which nothing on the OpenCV path sets");
        InitColorDeconverter();
        InitUpsampler();
        // jinit_d_post_controller: quantize_colors is 0 (J105), the upsampler feeds the output directly
        InitInverseDct();
        EntropyInit();
        bool useCBuffer = _hasMultipleScans;
        InitCoefController(useCBuffer);
        InitMainController();
        StartInputPass();
    }

    private void InitInverseDct()
    {
        foreach (var c in CompInfo)
        {
            c.DctTable = new int[DctSize2];
            c.CurMethod = -1;
        }
    }

    private void EntropyInit()
    {
        if (ArithCode)
        {
            ArithInit();
        }
        else if (ProgressiveMode)
        {
            _coefBits = new int[NumComponents][];
            for (int ci = 0; ci < NumComponents; ci++)
            {
                _coefBits[ci] = new int[DctSize2];
                Array.Fill(_coefBits[ci], -1);
            }
        }
    }

    private void EntropyStartPass()
    {
        if (ArithCode) ArithStartPass();
        else HuffStartPass();
    }

    private bool EntropyDecodeMcu(JpegBlock[] mcu) => ArithCode ? ArithDecodeMcu(mcu) : HuffDecodeMcu(mcu);

    // ---- jdmainct.c (no context rows: the upsampler never asks for them, J110)
    private void InitMainController()
    {
        int ngroups = MinDctVScaledSize;
        _mainBuffer = new SampleArray[NumComponents];
        for (int ci = 0; ci < NumComponents; ci++)
        {
            var c = CompInfo[ci];
            int rgroup = (c.VSampFactor * c.DctVScaledSize) / MinDctVScaledSize;
            long width = c.WidthInBlocks * c.DctHScaledSize;
            long rows = (long)rgroup * ngroups;
            if (width * rows > int.MaxValue / 2) throw Fatal("JERR_OUT_OF_MEMORY");
            _mainBuffer[ci] = new SampleArray((int)width, (int)rows);
        }
        _bufferFull = false;
        _rowgroupCtr = 0;
    }

    // ---- jdsample.c jinit_upsampler (J110)
    private void InitUpsampler()
    {
        if (CCIR601Sampling) throw Fatal("JERR_CCIR601_NOTIMPL");
        int n = NumComponents;
        _rowgroupHeight = new int[n];
        _upMethods = new UpMethod[n];
        _hExpand = new int[n];
        _vExpand = new int[n];
        _colorBuf = new SampleArray?[n];
        _upsampled = new SampleArray?[n];
        _upsampledRowOffset = new int[n];
        for (int ci = 0; ci < n; ci++)
        {
            var c = CompInfo[ci];
            int hInGroup = (c.HSampFactor * c.DctHScaledSize) / MinDctHScaledSize;
            int vInGroup = (c.VSampFactor * c.DctVScaledSize) / MinDctVScaledSize;
            int hOutGroup = MaxHSampFactor;
            int vOutGroup = MaxVSampFactor;
            _rowgroupHeight[ci] = vInGroup;
            bool needBuffer = true;
            if (!c.ComponentNeeded)
            {
                _upMethods[ci] = UpMethod.Noop;
                needBuffer = false;
            }
            else if (hInGroup == hOutGroup && vInGroup == vOutGroup)
            {
                _upMethods[ci] = UpMethod.FullSize;
                needBuffer = false;
            }
            else if (hInGroup * 2 == hOutGroup && vInGroup == vOutGroup)
            {
                _upMethods[ci] = UpMethod.H2V1;
            }
            else if (hInGroup * 2 == hOutGroup && vInGroup * 2 == vOutGroup)
            {
                _upMethods[ci] = UpMethod.H2V2;
            }
            else if ((hOutGroup % hInGroup) == 0 && (vOutGroup % vInGroup) == 0)
            {
                _upMethods[ci] = UpMethod.Int;
                _hExpand[ci] = hOutGroup / hInGroup;
                _vExpand[ci] = vOutGroup / vInGroup;
            }
            else
            {
                throw Fatal("JERR_FRACT_SAMPLE_NOTIMPL");
            }
            if (needBuffer)
            {
                long w = RoundUp(OutputWidth, MaxHSampFactor);
                _colorBuf[ci] = new SampleArray((int)w, MaxVSampFactor);
            }
        }
    }

    // ---- jdcolor.c jinit_color_deconverter (J104)
    private void InitColorDeconverter()
    {
        switch (JpegColorSpaceValue)
        {
            case JpegColorSpace.Grayscale:
                if (NumComponents != 1) throw Fatal("JERR_BAD_J_COLORSPACE");
                break;
            case JpegColorSpace.Rgb:
            case JpegColorSpace.YCbCr:
                if (NumComponents != 3) throw Fatal("JERR_BAD_J_COLORSPACE");
                break;
            case JpegColorSpace.Cmyk:
            case JpegColorSpace.Ycck:
                if (NumComponents != 4) throw Fatal("JERR_BAD_J_COLORSPACE");
                break;
            default:
                if (NumComponents < 1) throw Fatal("JERR_BAD_J_COLORSPACE");
                break;
        }
        // L11 (IMG 0x2A4A0): a non-zero color_transform (set by the LSE marker, L8) is legal only for an RGB input, whatever the output space
        if (ColorTransform != 0 && JpegColorSpaceValue != JpegColorSpace.Rgb) throw Fatal("JERR_CONVERSION_NOTIMPL");
        switch (OutColorSpace)
        {
            case JpegColorSpace.Grayscale:
                OutColorComponents = 1;
                switch (JpegColorSpaceValue)
                {
                    case JpegColorSpace.Grayscale:
                    case JpegColorSpace.YCbCr:
                        _colorConv = ColorConv.Grayscale;
                        for (int ci = 1; ci < NumComponents; ci++) CompInfo[ci].ComponentNeeded = false;
                        break;
                    case JpegColorSpace.Rgb:
                        // L11 (0x2A4F4): color_transform 0 -> rgb_gray_convert, 1 -> rgb1_gray_convert, other -> 0x1C
                        _colorConv = ColorTransform switch
                        {
                            0 => ColorConv.RgbGray,
                            1 => ColorConv.Rgb1Gray,
                            _ => throw Fatal("JERR_CONVERSION_NOTIMPL"),
                        };
                        BuildRgbYTable();
                        break;
                    default:
                        throw Fatal("JERR_CONVERSION_NOTIMPL");
                }
                break;
            case JpegColorSpace.Rgb:
                OutColorComponents = 3;
                switch (JpegColorSpaceValue)
                {
                    case JpegColorSpace.Grayscale: _colorConv = ColorConv.GrayRgb; break;
                    case JpegColorSpace.YCbCr: _colorConv = ColorConv.YccRgb; BuildYccRgbTable(); break;
                    case JpegColorSpace.Rgb:
                        // L11 (0x2A57E): 0 -> rgb_convert, 1 -> rgb1_rgb_convert, other -> 0x1C
                        _colorConv = ColorTransform switch
                        {
                            0 => ColorConv.RgbRgb,
                            1 => ColorConv.Rgb1Rgb,
                            _ => throw Fatal("JERR_CONVERSION_NOTIMPL"),
                        };
                        break;
                    default: throw Fatal("JERR_CONVERSION_NOTIMPL");
                }
                break;
            case JpegColorSpace.Cmyk:
                OutColorComponents = 4;
                switch (JpegColorSpaceValue)
                {
                    case JpegColorSpace.Ycck: _colorConv = ColorConv.YcckCmyk; BuildYccRgbTable(); break;
                    case JpegColorSpace.Cmyk: _colorConv = ColorConv.Null; break;
                    default: throw Fatal("JERR_CONVERSION_NOTIMPL");
                }
                break;
            default:
                if (OutColorSpace == JpegColorSpaceValue)
                {
                    OutColorComponents = NumComponents;
                    _colorConv = ColorConv.Null;
                }
                else
                {
                    throw Fatal("JERR_CONVERSION_NOTIMPL");
                }
                break;
        }
    }

    // build_ycc_rgb_table (J54)
    private void BuildYccRgbTable()
    {
        _crR = new int[256];
        _cbB = new int[256];
        _crG = new int[256];
        _cbG = new int[256];
        for (int i = 0; i < 256; i++)
        {
            int x = i - 128;
            _crR[i] = (0x166E9 * x + 0x8000) >> 16;
            _cbB[i] = (0x1C5A2 * x + 0x8000) >> 16;
            _crG[i] = -46802 * x;
            _cbG[i] = -22554 * x + 0x8000;
        }
    }

    // build_rgb_y_table (J55)
    private void BuildRgbYTable()
    {
        _rgbYTab = new int[3 * 256];
        for (int i = 0; i < 256; i++)
        {
            _rgbYTab[i] = 19595 * i;
            _rgbYTab[i + 256] = 38470 * i;
            _rgbYTab[i + 512] = 7471 * i + 0x8000;
        }
    }

    // ---- jpeg_start_decompress (J71)
    public void StartDecompress()
    {
        if (_state == State.Ready)
        {
            MasterSelection();
            _state = State.PreLoad;
        }
        if (_state == State.PreLoad)
        {
            if (_hasMultipleScans)
            {
                for (; ; )
                {
                    int retcode = ConsumeInput();
                    if (retcode == JpegSuspended) return;       // the wrapper ignores the return value; the state stays PRELOAD
                    if (retcode == JpegReachedEoi) break;
                }
            }
            OutputScanNumber = InputScanNumber;
        }
        else if (_state != State.PreScan)
        {
            throw Fatal("JERR_BAD_STATE");
        }
        OutputPassSetup();
    }

    private void OutputPassSetup()
    {
        if (_state != State.PreScan)
        {
            PrepareForOutputPass();
            OutputScanline = 0;
            _state = State.PreScan;
        }
        _state = State.Scanning;
    }

    // prepare_for_output_pass: idct start_pass, coef start_output_pass, colour/upsample/post/main start_pass
    private void PrepareForOutputPass()
    {
        IdctStartPass();
        CoefStartOutputPass();
        // upsampler start_pass
        _nextRowOut = MaxVSampFactor;
        _rowsToGo = OutputHeight;
        // main controller start_pass
        _bufferFull = false;
        _rowgroupCtr = 0;
    }

    // ---- jpeg_read_scanlines for one row: returns the number of rows produced (0 on suspension)
    public int ReadScanline(byte[] row)
    {
        if (_state != State.Scanning) throw Fatal("JERR_BAD_STATE");
        if (OutputScanline >= OutputHeight)
        {
            Warn("JWRN_TOO_MUCH_DATA");
            return 0;
        }
        int rowCtr = 0;
        ProcessDataSimpleMain(row, ref rowCtr, 1);
        OutputScanline += rowCtr;
        return rowCtr;
    }

    // process_data_simple_main (J106)
    private void ProcessDataSimpleMain(byte[] outRow, ref int outRowCtr, int outRowsAvail)
    {
        if (!_bufferFull)
        {
            if (CoefDecompressData(_mainBuffer!) == JpegSuspended) return;
            _bufferFull = true;
        }
        int rowgroupsAvail = MinDctVScaledSize;
        SepUpsample(_mainBuffer!, ref _rowgroupCtr, rowgroupsAvail, outRow, ref outRowCtr, outRowsAvail);
        if (_rowgroupCtr >= rowgroupsAvail)
        {
            _bufferFull = false;
            _rowgroupCtr = 0;
        }
    }

    // sep_upsample (jdsample.c) + the colour conversion of its rows
    private void SepUpsample(SampleArray[] inputBuf, ref int inRowGroupCtr, int inRowGroupsAvail, byte[] outRow, ref int outRowCtr, int outRowsAvail)
    {
        if (_nextRowOut >= MaxVSampFactor)
        {
            for (int ci = 0; ci < NumComponents; ci++)
            {
                RunUpsample(ci, inputBuf[ci], inRowGroupCtr * _rowgroupHeight[ci]);
            }
            _nextRowOut = 0;
        }
        long numRows = MaxVSampFactor - _nextRowOut;
        if (numRows > _rowsToGo) numRows = _rowsToGo;
        long avail = outRowsAvail - outRowCtr;
        if (numRows > avail) numRows = avail;
        ColorConvert(_nextRowOut, outRow, (int)numRows);
        outRowCtr += (int)numRows;
        _rowsToGo -= numRows;
        _nextRowOut += (int)numRows;
        if (_nextRowOut >= MaxVSampFactor) inRowGroupCtr++;
    }

    private void RunUpsample(int ci, SampleArray input, int inputRow)
    {
        var c = CompInfo[ci];
        switch (_upMethods[ci])
        {
            case UpMethod.Noop:
                _upsampled[ci] = null;
                break;
            case UpMethod.FullSize:
                _upsampled[ci] = input;
                _upsampledRowOffset[ci] = inputRow;
                break;
            case UpMethod.H2V1:
            {
                var dst = _colorBuf[ci]!;
                for (int v = 0; v < MaxVSampFactor; v++)
                {
                    int sp = (inputRow + v) * input.Stride;
                    int dp = v * dst.Stride;
                    for (int s = 0; dp - v * dst.Stride < OutputWidth; s++)
                    {
                        byte b = input.Data[sp + s];
                        dst.Data[dp++] = b;
                        dst.Data[dp++] = b;
                    }
                }
                _upsampled[ci] = dst;
                _upsampledRowOffset[ci] = 0;
                break;
            }
            case UpMethod.H2V2:
            {
                var dst = _colorBuf[ci]!;
                int inrow = 0, outrow = 0;
                while (outrow < MaxVSampFactor)
                {
                    int sp = (inputRow + inrow) * input.Stride;
                    int dp = outrow * dst.Stride;
                    for (int s = 0; dp - outrow * dst.Stride < OutputWidth; s++)
                    {
                        byte b = input.Data[sp + s];
                        dst.Data[dp++] = b;
                        dst.Data[dp++] = b;
                    }
                    Array.Copy(dst.Data, outrow * dst.Stride, dst.Data, (outrow + 1) * dst.Stride, OutputWidth);
                    inrow++;
                    outrow += 2;
                }
                _upsampled[ci] = dst;
                _upsampledRowOffset[ci] = 0;
                break;
            }
            case UpMethod.Int:
            {
                var dst = _colorBuf[ci]!;
                int hExp = _hExpand[ci], vExp = _vExpand[ci];
                int inrow = 0, outrow = 0;
                while (outrow < MaxVSampFactor)
                {
                    int sp = (inputRow + inrow) * input.Stride;
                    int dp = outrow * dst.Stride;
                    for (int s = 0; dp - outrow * dst.Stride < OutputWidth; s++)
                    {
                        byte b = input.Data[sp + s];
                        for (int h = hExp; h > 0; h--) dst.Data[dp++] = b;
                    }
                    if (vExp > 1) for (int v = 1; v < vExp; v++) Array.Copy(dst.Data, outrow * dst.Stride, dst.Data, (outrow + v) * dst.Stride, OutputWidth);
                    inrow++;
                    outrow += vExp;
                }
                _upsampled[ci] = dst;
                _upsampledRowOffset[ci] = 0;
                break;
            }
        }
    }

    // the colour deconverters (J54..J56, J74, J104): num_rows rows starting at input_row of the upsampled planes, written to
    // the single output row buffer (num_rows is 1 for OpenCV: rec_outbuf_height is 1)
    private void ColorConvert(int inputRow, byte[] outRow, int numRows)
    {
        var rl = _rangeTable;
        for (int row = 0; row < numRows; row++)
        {
            int cols = OutputWidth;
            int o = 0;
            switch (_colorConv)
            {
                case ColorConv.Grayscale:
                {
                    var p0 = _upsampled[0]!;
                    int sp = (inputRow + row + _upsampledRowOffset[0]) * p0.Stride;
                    Array.Copy(p0.Data, sp, outRow, 0, cols);
                    break;
                }
                case ColorConv.GrayRgb:
                {
                    var p0 = _upsampled[0]!;
                    int sp = (inputRow + row + _upsampledRowOffset[0]) * p0.Stride;
                    for (int col = 0; col < cols; col++)
                    {
                        byte v = p0.Data[sp + col];
                        outRow[o++] = v; outRow[o++] = v; outRow[o++] = v;
                    }
                    break;
                }
                case ColorConv.YccRgb:
                {
                    var p0 = _upsampled[0]!; var p1 = _upsampled[1]!; var p2 = _upsampled[2]!;
                    int s0 = (inputRow + row + _upsampledRowOffset[0]) * p0.Stride;
                    int s1 = (inputRow + row + _upsampledRowOffset[1]) * p1.Stride;
                    int s2 = (inputRow + row + _upsampledRowOffset[2]) * p2.Stride;
                    for (int col = 0; col < cols; col++)
                    {
                        int y = p0.Data[s0 + col], cb = p1.Data[s1 + col], cr = p2.Data[s2 + col];
                        outRow[o++] = rl[RangeBase + y + _crR![cr]];
                        outRow[o++] = rl[RangeBase + y + ((_cbG![cb] + _crG![cr]) >> 16)];
                        outRow[o++] = rl[RangeBase + y + _cbB![cb]];
                    }
                    break;
                }
                case ColorConv.RgbGray:
                {
                    var p0 = _upsampled[0]!; var p1 = _upsampled[1]!; var p2 = _upsampled[2]!;
                    int s0 = (inputRow + row + _upsampledRowOffset[0]) * p0.Stride;
                    int s1 = (inputRow + row + _upsampledRowOffset[1]) * p1.Stride;
                    int s2 = (inputRow + row + _upsampledRowOffset[2]) * p2.Stride;
                    var t = _rgbYTab!;
                    for (int col = 0; col < cols; col++)
                    {
                        int r = p0.Data[s0 + col], g = p1.Data[s1 + col], b = p2.Data[s2 + col];
                        outRow[col] = (byte)((t[r] + t[g + 256] + t[b + 512]) >> 16);
                    }
                    break;
                }
                case ColorConv.Rgb1Gray:
                {
                    // L15 (rgb1_gray_convert, IMG 0x2A230): r = (c0 + c1 - 128) & 0xFF, g = c1, b = (c2 + c1 - 128) & 0xFF, then the L14 table sum
                    var p0 = _upsampled[0]!; var p1 = _upsampled[1]!; var p2 = _upsampled[2]!;
                    int s0 = (inputRow + row + _upsampledRowOffset[0]) * p0.Stride;
                    int s1 = (inputRow + row + _upsampledRowOffset[1]) * p1.Stride;
                    int s2 = (inputRow + row + _upsampledRowOffset[2]) * p2.Stride;
                    var t = _rgbYTab!;
                    for (int col = 0; col < cols; col++)
                    {
                        int c0 = p0.Data[s0 + col], g = p1.Data[s1 + col], c2 = p2.Data[s2 + col];
                        int r = (c0 + g - 128) & 0xFF, b = (c2 + g - 128) & 0xFF;
                        outRow[col] = (byte)((t[r] + t[g + 256] + t[b + 512]) >> 16);
                    }
                    break;
                }
                case ColorConv.Rgb1Rgb:
                {
                    // L12 (rgb1_rgb_convert, IMG 0x2A1D4): out = ((c0 + c1 - 128) & 0xFF, c1, (c2 + c1 - 128) & 0xFF)
                    var p0 = _upsampled[0]!; var p1 = _upsampled[1]!; var p2 = _upsampled[2]!;
                    int s0 = (inputRow + row + _upsampledRowOffset[0]) * p0.Stride;
                    int s1 = (inputRow + row + _upsampledRowOffset[1]) * p1.Stride;
                    int s2 = (inputRow + row + _upsampledRowOffset[2]) * p2.Stride;
                    for (int col = 0; col < cols; col++)
                    {
                        int c0 = p0.Data[s0 + col], g = p1.Data[s1 + col], c2 = p2.Data[s2 + col];
                        outRow[o++] = (byte)(c0 + g - 128);
                        outRow[o++] = (byte)g;
                        outRow[o++] = (byte)(c2 + g - 128);
                    }
                    break;
                }
                case ColorConv.RgbRgb:
                {
                    var p0 = _upsampled[0]!; var p1 = _upsampled[1]!; var p2 = _upsampled[2]!;
                    int s0 = (inputRow + row + _upsampledRowOffset[0]) * p0.Stride;
                    int s1 = (inputRow + row + _upsampledRowOffset[1]) * p1.Stride;
                    int s2 = (inputRow + row + _upsampledRowOffset[2]) * p2.Stride;
                    for (int col = 0; col < cols; col++)
                    {
                        outRow[o++] = p0.Data[s0 + col]; outRow[o++] = p1.Data[s1 + col]; outRow[o++] = p2.Data[s2 + col];
                    }
                    break;
                }
                case ColorConv.YcckCmyk:
                {
                    var p0 = _upsampled[0]!; var p1 = _upsampled[1]!; var p2 = _upsampled[2]!; var p3 = _upsampled[3]!;
                    int s0 = (inputRow + row + _upsampledRowOffset[0]) * p0.Stride;
                    int s1 = (inputRow + row + _upsampledRowOffset[1]) * p1.Stride;
                    int s2 = (inputRow + row + _upsampledRowOffset[2]) * p2.Stride;
                    int s3 = (inputRow + row + _upsampledRowOffset[3]) * p3.Stride;
                    for (int col = 0; col < cols; col++)
                    {
                        int y = p0.Data[s0 + col], cb = p1.Data[s1 + col], cr = p2.Data[s2 + col];
                        outRow[o++] = rl[RangeBase + 255 - (y + _crR![cr])];
                        outRow[o++] = rl[RangeBase + 255 - (y + ((_cbG![cb] + _crG![cr]) >> 16))];
                        outRow[o++] = rl[RangeBase + 255 - (y + _cbB![cb])];
                        outRow[o++] = p3.Data[s3 + col];
                    }
                    break;
                }
                default:     // null_convert: interleave the planes
                {
                    for (int ci = 0; ci < OutColorComponents; ci++)
                    {
                        var p = _upsampled[ci]!;
                        int sp = (inputRow + row + _upsampledRowOffset[ci]) * p.Stride;
                        int op = ci;
                        for (int col = 0; col < cols; col++)
                        {
                            outRow[op] = p.Data[sp + col];
                            op += OutColorComponents;
                        }
                    }
                    break;
                }
            }
        }
    }

    // ---- jpeg_finish_decompress (J71): returns false when it suspends
    public bool FinishDecompress()
    {
        if (_state == State.Scanning)
        {
            if (OutputScanline < OutputHeight) throw Fatal("JERR_TOO_LITTLE_DATA");
            _state = State.Stopping;
        }
        else if (_state != State.Stopping)
        {
            throw Fatal("JERR_BAD_STATE");
        }
        while (!_eoiReached)
        {
            if (ConsumeInput() == JpegSuspended) return false;
        }
        return true;
    }
}
