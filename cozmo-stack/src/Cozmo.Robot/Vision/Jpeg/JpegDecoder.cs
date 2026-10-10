namespace Cozmo.Robot.Vision.Jpeg;

// fidelity: M3-001, M3-018
// The shipped JPEG decoder: libopencv_imgcodecs.so carries the IJG libjpeg 9 decompressor (version 90, struct size 0x1E8,
// J10) driven by OpenCV's JpegDecoder (readHeader 0x15844, readData 0x15B60; rows J9..J115 of
// re-analysis/research/20261006-M3M4-rows-extraction.md). This port follows that code module by module (jdmarker,
// jdinput, jdmaster, jdhuff/jdphuff, jdarith, jdcoefct, jddctmgr/jidctint, jdsample, jdcolor, jdmainct). Library longjmp
// (error_exit) becomes JpegFatalException; warnings are recorded and do not change decoding. The memory source returns
// false from fill_input_buffer (J25), so running out of bytes is a suspension, exactly as in the original.

/// <summary>A fatal libjpeg error (the original's error_exit longjmp to the setjmp in readHeader / readData, J11).</summary>
internal sealed class JpegFatalException : Exception
{
    public JpegFatalException(string code) : base(code) { Code = code; }
    public string Code { get; }
}

internal enum JpegColorSpace
{
    Unknown = 0, Grayscale = 1, Rgb = 2, YCbCr = 3, Cmyk = 4, Ycck = 5,
}

internal sealed class JpegComponent
{
    public int ComponentId;
    public int ComponentIndex;
    public int HSampFactor;
    public int VSampFactor;
    public int QuantTblNo;
    public int DcTblNo;
    public int AcTblNo;
    public long WidthInBlocks;
    public long HeightInBlocks;
    public int DctHScaledSize;
    public int DctVScaledSize;
    public long DownsampledWidth;
    public long DownsampledHeight;
    public bool ComponentNeeded;
    public int McuWidth;
    public int McuHeight;
    public int McuBlocks;
    public int McuSampleWidth;
    public int LastColWidth;
    public int LastRowHeight;
    public ushort[]? QuantTable;     // latched copy (natural order), null until latched
    public int[]? DctTable;          // multiplier table (islow: the raw quantisers as ints)
    public int CurMethod = -1;
}

internal sealed class JpegHuffTable
{
    public readonly byte[] Bits = new byte[17];
    public readonly byte[] HuffVal = new byte[256];
}

internal sealed partial class JpegDecoder
{
    public const int DctSize = 8;
    public const int DctSize2 = 64;
    public const int MaxComponents = 10;
    public const int MaxCompsInScan = 4;
    public const int MaxSampFactor = 4;
    public const int DMaxBlocksInMcu = 10;
    public const int JpegMaxDimension = 65500;

    // ---- the memory source (OpenCV's, J25): next_input_byte / bytes_in_buffer over one buffer
    private byte[] _buf = Array.Empty<byte>();
    private int _next;
    private int _avail;

    // ---- header state (jpeg_decompress_struct fields)
    public int ImageWidth;
    public int ImageHeight;
    public int DataPrecision;
    public int NumComponents;
    public JpegColorSpace JpegColorSpaceValue;
    public JpegColorSpace OutColorSpace;
    public int OutColorComponents;
    public int OutputWidth;
    public int OutputHeight;
    public int ScaleNum = 1;
    public int ScaleDenom = 1;
    public JpegComponent[] CompInfo = Array.Empty<JpegComponent>();
    private readonly ushort[]?[] _quantTblPtrs = new ushort[]?[4];
    private readonly JpegHuffTable?[] _dcHuffTblPtrs = new JpegHuffTable?[4];
    private readonly JpegHuffTable?[] _acHuffTblPtrs = new JpegHuffTable?[4];
    private readonly byte[] _arithDcL = new byte[16];
    private readonly byte[] _arithDcU = new byte[16];
    private readonly byte[] _arithAcK = new byte[16];
    public int RestartInterval;
    public bool IsBaseline;
    public bool ProgressiveMode;
    public bool ArithCode;
    public bool DoFancyUpsampling = true;
    public bool DoBlockSmoothing = true;
    public bool CCIR601Sampling;
    public int ColorTransform;           // JCT_NONE; the LSE marker sets 1 (JCT_SUBTRACT_GREEN, L8), get_soi clears it (L10)
    public bool SawJfifMarker;
    public bool SawAdobeMarker;
    public int AdobeTransform;
    public int MaxHSampFactor;
    public int MaxVSampFactor;
    public int BlockSize;
    public int LimSe;
    /// <summary>cinfo->natural_order: the zigzag table of the block size (sizes 9..16 use the 8x8 one, J77).</summary>
    public int[] NaturalOrder = JpegTables.NaturalOrder8;
    public int MinDctHScaledSize;
    public int MinDctVScaledSize;
    public long TotalIMcuRows;
    public int CompsInScan;
    public readonly JpegComponent?[] CurCompInfo = new JpegComponent?[MaxCompsInScan];
    public long McusPerRow;
    public long McuRowsInScan;
    public int BlocksInMcu;
    public readonly int[] McuMembership = new int[DMaxBlocksInMcu];
    public int Ss, Se, Ah, Al;
    public int InputScanNumber;
    public int OutputScanNumber;
    public long InputIMcuRow;
    public long OutputIMcuRow;
    public long OutputScanline;
    public int UnreadMarker;
    public int NumWarnings;
    public readonly List<string> Warnings = new();
    /// <summary>
    /// What libjpeg's standard error manager would print to stderr: it prints only the first warning of a decoder (num_warnings == 0, trace_level 0;
    /// emit_message, J11), as "message\n" with the message table's text and the call's arguments. Null if there was none.
    /// </summary>
    public string? FirstWarning { get; private set; }

    // ---- marker reader state
    private bool _sawSoi;
    private bool _sawSof;
    private int _nextRestartNum;
    private long _discardedBytes;

    // ---- input controller state
    private bool _inHeaders = true;
    private bool _hasMultipleScans;
    private bool _eoiReached;
    private bool _consumeIsData;     // inputctl->consume_input == coef->consume_data (else consume_markers)

    // ---- decompression state (global_state)
    private enum State { Start, InHeader, Ready, PreLoad, PreScan, Scanning, Stopping }
    private State _state = State.Start;

    private const int JpegSuspended = 0, JpegReachedSos = 1, JpegReachedEoi = 2, JpegRowCompleted = 3, JpegScanCompleted = 4;

    public JpegDecoder(byte[] data)
    {
        // readHeader: next_input_byte = m_buf.ptr(), bytes_in_buffer = m_buf.total() (J12)
        _buf = data;
        _next = 0;
        _avail = data.Length;
    }

    // WARNMS / WARNMS1 / WARNMS2: a warning does not change decoding; the first one is what the standard error manager prints
    private void Warn(string code, int a1 = 0, int a2 = 0)
    {
        if (NumWarnings == 0) FirstWarning = WarningText(code, a1, a2);
        NumWarnings++;
        if (Warnings.Count < 64) Warnings.Add(code);      // the codes of the first warnings, for diagnostics; the count is exact
    }

    // the message table of the shipped libjpeg (libopencv_imgcodecs.so rodata 0xC3Bxx..0xC3Dxx) for the warnings this port raises
    private static string WarningText(string code, int a1, int a2) => code switch
    {
        "JWRN_EXTRANEOUS_DATA" => $"Corrupt JPEG data: {(uint)a1} extraneous bytes before marker 0x{a2:x2}",
        "JWRN_HIT_MARKER" => "Corrupt JPEG data: premature end of data segment",
        "JWRN_HUFF_BAD_CODE" => "Corrupt JPEG data: bad Huffman code",
        "JWRN_ARITH_BAD_CODE" => "Corrupt JPEG data: bad arithmetic code",
        "JWRN_MUST_RESYNC" => $"Corrupt JPEG data: found marker 0x{a1:x2} instead of RST{a2}",
        "JWRN_ADOBE_XFORM" => $"Unknown Adobe color transform code {a1}",
        "JWRN_NOT_SEQUENTIAL" => "Invalid SOS parameters for sequential JPEG",
        "JWRN_BOGUS_PROGRESSION" => $"Inconsistent progression sequence for component {a1} coefficient {a2}",
        "JWRN_TOO_MUCH_DATA" => "Application transferred too many scanlines",
        "JWRN_JFIF_MAJOR" => $"Warning: unknown JFIF revision number {a1}.{a2:d2}",
        _ => throw new InvalidOperationException(code),
    };

    private static JpegFatalException Fatal(string code) => new(code);

    // ---- jpeg_read_header(cinfo, TRUE) + the rest of readHeader (J12, J13)
    /// <summary>
    /// OpenCV <c>JpegDecoder::readHeader</c> (0x15844): CreateDecompress, memory source, jpeg_read_header(TRUE), scale 1/1,
    /// jpeg_calc_output_dimensions. Throws <see cref="JpegFatalException"/> where the original longjmps (readHeader returns false).
    /// </summary>
    public void ReadHeader(out int width, out int height, out int components)
    {
        // jpeg_read_header (jdapimin): START -> reset_input_controller + init_source, INHEADER -> consume_input
        if (_state == State.Start)
        {
            _state = State.InHeader;
        }
        int retcode = ConsumeInputHeader();
        switch (retcode)
        {
            case JpegReachedSos:
                break;
            case JpegReachedEoi:
                throw Fatal("JERR_NO_IMAGE");
            case JpegSuspended:
                break;                      // the wrapper does not test the return (J46): the state check below decides
        }
        ScaleNum = 1;
        ScaleDenom = 1;
        CalcOutputDimensions();
        width = OutputWidth;
        height = OutputHeight;
        components = NumComponents;
    }

    // jpeg_consume_input in state INHEADER
    private int ConsumeInputHeader()
    {
        int retcode = ConsumeMarkers();
        if (retcode == JpegReachedSos)
        {
            DefaultDecompressParms();
            _state = State.Ready;
        }
        else if (retcode == JpegReachedEoi)
        {
            if (_sawSof) throw Fatal("JERR_SOF_NO_SOS");
            if (_inHeaders) throw Fatal("JERR_NO_IMAGE");
        }
        return retcode;
    }

    // default_decompress_parms (jdapimin, J103; J16 for the header defaults)
    private void DefaultDecompressParms()
    {
        switch (NumComponents)
        {
            case 1:
                JpegColorSpaceValue = JpegColorSpace.Grayscale;
                OutColorSpace = JpegColorSpace.Grayscale;
                break;
            case 3:
                if (SawJfifMarker)
                {
                    JpegColorSpaceValue = JpegColorSpace.YCbCr;
                }
                else if (SawAdobeMarker)
                {
                    switch (AdobeTransform)
                    {
                        case 0: JpegColorSpaceValue = JpegColorSpace.Rgb; break;
                        case 1: JpegColorSpaceValue = JpegColorSpace.YCbCr; break;
                        default:
                            Warn("JWRN_ADOBE_XFORM", AdobeTransform);
                            JpegColorSpaceValue = JpegColorSpace.YCbCr;
                            break;
                    }
                }
                else
                {
                    int cid0 = CompInfo[0].ComponentId, cid1 = CompInfo[1].ComponentId, cid2 = CompInfo[2].ComponentId;
                    if (cid0 == 1 && cid1 == 2 && cid2 == 3) JpegColorSpaceValue = JpegColorSpace.YCbCr;
                    else if (cid0 == 82 && cid1 == 71 && cid2 == 66) JpegColorSpaceValue = JpegColorSpace.Rgb;
                    else
                    {
                        JpegColorSpaceValue = JpegColorSpace.YCbCr;        // TRACEMS3 at level 1: not printed
                    }
                }
                OutColorSpace = JpegColorSpace.Rgb;
                break;
            case 4:
                if (SawAdobeMarker)
                {
                    switch (AdobeTransform)
                    {
                        case 0: JpegColorSpaceValue = JpegColorSpace.Cmyk; break;
                        case 2: JpegColorSpaceValue = JpegColorSpace.Ycck; break;
                        default:
                            Warn("JWRN_ADOBE_XFORM", AdobeTransform);
                            JpegColorSpaceValue = JpegColorSpace.Ycck;
                            break;
                    }
                }
                else
                {
                    JpegColorSpaceValue = JpegColorSpace.Cmyk;
                }
                OutColorSpace = JpegColorSpace.Cmyk;
                break;
            default:
                JpegColorSpaceValue = JpegColorSpace.Unknown;
                OutColorSpace = JpegColorSpace.Unknown;
                break;
        }
    }

    // ---- jdinput.c
    private int ConsumeMarkers()
    {
        if (_eoiReached) return JpegReachedEoi;
        int val = ReadMarkers();
        switch (val)
        {
            case JpegReachedSos:
                if (_inHeaders)
                {
                    InitialSetup();
                    _inHeaders = false;
                }
                else
                {
                    if (!_hasMultipleScans) throw Fatal("JERR_EOI_EXPECTED");
                    StartInputPass();
                }
                break;
            case JpegReachedEoi:
                _eoiReached = true;
                if (_inHeaders)
                {
                    if (_sawSof) throw Fatal("JERR_SOF_NO_SOS");
                }
                else
                {
                    if (OutputScanNumber > InputScanNumber) OutputScanNumber = InputScanNumber;
                }
                break;
        }
        return val;
    }

    private static long DivRoundUp(long a, long b) => (a + b - 1) / b;
    private static long RoundUp(long a, long b)
    {
        a += b - 1;
        return a - (a % b);
    }

    private void InitialSetup()
    {
        if (ImageHeight > JpegMaxDimension || ImageWidth > JpegMaxDimension) throw Fatal("JERR_IMAGE_TOO_BIG");
        if (DataPrecision != 8) throw Fatal("JERR_BAD_PRECISION");
        if (NumComponents > MaxComponents) throw Fatal("JERR_COMPONENT_COUNT");
        MaxHSampFactor = 1;
        MaxVSampFactor = 1;
        foreach (var c in CompInfo)
        {
            if (c.HSampFactor <= 0 || c.HSampFactor > MaxSampFactor || c.VSampFactor <= 0 || c.VSampFactor > MaxSampFactor)
                throw Fatal("JERR_BAD_SAMPLING");
            if (MaxHSampFactor < c.HSampFactor) MaxHSampFactor = c.HSampFactor;
            if (MaxVSampFactor < c.VSampFactor) MaxVSampFactor = c.VSampFactor;
        }
        // block_size, natural_order and lim_Se (J77)
        if (IsBaseline || (ProgressiveMode && CompsInScan != 0))
        {
            BlockSize = DctSize;
            NaturalOrder = JpegTables.NaturalOrder8;
            LimSe = DctSize2 - 1;
        }
        else
        {
            switch (Se)
            {
                case 0: BlockSize = 1; break;
                case 3: BlockSize = 2; break;
                case 8: BlockSize = 3; break;
                case 15: BlockSize = 4; break;
                case 24: BlockSize = 5; break;
                case 35: BlockSize = 6; break;
                case 48: BlockSize = 7; break;
                case 63: BlockSize = 8; break;
                case 80: BlockSize = 9; break;
                case 99: BlockSize = 10; break;
                case 120: BlockSize = 11; break;
                case 143: BlockSize = 12; break;
                case 168: BlockSize = 13; break;
                case 195: BlockSize = 14; break;
                case 224: BlockSize = 15; break;
                case 255: BlockSize = 16; break;
                default: throw Fatal("JERR_BAD_PROGRESSION");
            }
            // sizes 1..8 select the size-specific natural order and lim_Se = Se; sizes 9..16 the 64-entry one with lim_Se = 63
            if (BlockSize <= DctSize)
            {
                NaturalOrder = JpegTables.NaturalOrder(BlockSize);
                LimSe = Se;
            }
            else
            {
                NaturalOrder = JpegTables.NaturalOrder8;
                LimSe = DctSize2 - 1;
            }
        }
        foreach (var c in CompInfo)
        {
            c.WidthInBlocks = DivRoundUp((long)ImageWidth * c.HSampFactor, (long)MaxHSampFactor * BlockSize);
            c.HeightInBlocks = DivRoundUp((long)ImageHeight * c.VSampFactor, (long)MaxVSampFactor * BlockSize);
            c.DctHScaledSize = BlockSize;
            c.DctVScaledSize = BlockSize;
            c.DownsampledWidth = DivRoundUp((long)ImageWidth * ((long)c.HSampFactor * c.DctHScaledSize), (long)MaxHSampFactor * BlockSize);
            c.DownsampledHeight = DivRoundUp((long)ImageHeight * ((long)c.VSampFactor * c.DctVScaledSize), (long)MaxVSampFactor * BlockSize);
            c.ComponentNeeded = true;
        }
        TotalIMcuRows = DivRoundUp(ImageHeight, (long)MaxVSampFactor * BlockSize);
        _hasMultipleScans = CompsInScan < NumComponents || ProgressiveMode;
    }

    private void PerScanSetup()
    {
        if (CompsInScan == 1)
        {
            var c = CurCompInfo[0]!;
            McusPerRow = c.WidthInBlocks;
            McuRowsInScan = c.HeightInBlocks;
            c.McuWidth = 1;
            c.McuHeight = 1;
            c.McuBlocks = 1;
            c.McuSampleWidth = c.DctHScaledSize;
            c.LastColWidth = 1;
            int tmp = (int)(c.HeightInBlocks % c.VSampFactor);
            if (tmp == 0) tmp = c.VSampFactor;
            c.LastRowHeight = tmp;
            BlocksInMcu = 1;
            McuMembership[0] = 0;
        }
        else
        {
            if (CompsInScan <= 0 || CompsInScan > MaxCompsInScan) throw Fatal("JERR_COMPONENT_COUNT");
            McusPerRow = DivRoundUp(ImageWidth, (long)MaxHSampFactor * BlockSize);
            McuRowsInScan = DivRoundUp(ImageHeight, (long)MaxVSampFactor * BlockSize);
            BlocksInMcu = 0;
            for (int ci = 0; ci < CompsInScan; ci++)
            {
                var c = CurCompInfo[ci]!;
                c.McuWidth = c.HSampFactor;
                c.McuHeight = c.VSampFactor;
                c.McuBlocks = c.McuWidth * c.McuHeight;
                c.McuSampleWidth = c.McuWidth * c.DctHScaledSize;
                int tmp = (int)(c.WidthInBlocks % c.McuWidth);
                if (tmp == 0) tmp = c.McuWidth;
                c.LastColWidth = tmp;
                tmp = (int)(c.HeightInBlocks % c.McuHeight);
                if (tmp == 0) tmp = c.McuHeight;
                c.LastRowHeight = tmp;
                int mcublks = c.McuBlocks;
                if (BlocksInMcu + mcublks > DMaxBlocksInMcu) throw Fatal("JERR_BAD_MCU_SIZE");
                while (mcublks-- > 0) McuMembership[BlocksInMcu++] = ci;
            }
        }
    }

    private void LatchQuantTables()
    {
        for (int ci = 0; ci < CompsInScan; ci++)
        {
            var c = CurCompInfo[ci]!;
            if (c.QuantTable != null) continue;
            int qtblno = c.QuantTblNo;
            if (qtblno < 0 || qtblno >= 4 || _quantTblPtrs[qtblno] == null) throw Fatal("JERR_NO_QUANT_TABLE");
            c.QuantTable = (ushort[])_quantTblPtrs[qtblno]!.Clone();
        }
    }

    private void StartInputPass()
    {
        PerScanSetup();
        LatchQuantTables();
        EntropyStartPass();
        CoefStartInputPass();
        _consumeIsData = true;
    }

    private void FinishInputPass()
    {
        _consumeIsData = false;
    }
}
