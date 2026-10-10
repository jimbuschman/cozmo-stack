namespace Cozmo.Robot.Vision.Jpeg;

// fidelity: M3-001, M3-018
// jdmarker.c as shipped (J12, J25, J46..J51, J53): marker reader over the memory source.
internal sealed partial class JpegDecoder
{
    // ---- the memory source (J25): fill_input_buffer returns false (suspension), skip_input_data advances inside the
    // supplied bytes or exhausts them (the residual is kept in the original at source+0x1C and is never read again).
    private bool FillInputBuffer() => false;

    private void SkipInputData(long numBytes)
    {
        if (numBytes > _avail)
        {
            _next += _avail;
            _avail = 0;
            return;
        }
        _next += (int)numBytes;
        _avail -= (int)numBytes;
    }

    // INPUT_VARS style helpers: a Reader copy is committed with Sync() only at the points libjpeg calls INPUT_SYNC.
    private struct Reader
    {
        public int Next;
        public int Avail;
    }

    private Reader Load() => new Reader { Next = _next, Avail = _avail };
    private void Sync(in Reader r) { _next = r.Next; _avail = r.Avail; }

    private bool Byte(ref Reader r, out int v)
    {
        if (r.Avail == 0)
        {
            if (!FillInputBuffer()) { v = 0; return false; }
            r.Next = _next;
            r.Avail = _avail;
        }
        r.Avail--;
        v = _buf[r.Next++];
        return true;
    }

    private bool TwoBytes(ref Reader r, out int v)
    {
        v = 0;
        if (!Byte(ref r, out int hi)) return false;
        if (!Byte(ref r, out int lo)) return false;
        v = (hi << 8) + lo;
        return true;
    }

    // first_marker
    private bool FirstMarker()
    {
        var r = Load();
        if (!Byte(ref r, out int c)) return false;
        if (!Byte(ref r, out int c2)) return false;
        if (c != 0xFF || c2 != 0xD8) throw Fatal("JERR_NO_SOI");
        UnreadMarker = c2;
        Sync(r);
        return true;
    }

    // next_marker (J50)
    private bool NextMarker()
    {
        var r = Load();
        int c;
        for (; ; )
        {
            if (!Byte(ref r, out c)) return false;
            while (c != 0xFF)
            {
                _discardedBytes++;
                Sync(r);
                if (!Byte(ref r, out c)) return false;
            }
            do
            {
                if (!Byte(ref r, out c)) return false;
            } while (c == 0xFF);
            if (c != 0) break;
            _discardedBytes += 2;
            Sync(r);
        }
        if (_discardedBytes != 0)
        {
            Warn("JWRN_EXTRANEOUS_DATA", (int)_discardedBytes, c);
            _discardedBytes = 0;
        }
        UnreadMarker = c;
        Sync(r);
        return true;
    }

    private bool GetSoi()
    {
        if (_sawSoi) throw Fatal("JERR_SOI_DUPLICATE");
        for (int i = 0; i < 16; i++)
        {
            _arithDcL[i] = 0;
            _arithDcU[i] = 1;
            _arithAcK[i] = 5;
        }
        RestartInterval = 0;
        JpegColorSpaceValue = JpegColorSpace.Unknown;
        ColorTransform = 0;
        CCIR601Sampling = false;
        SawJfifMarker = false;
        SawAdobeMarker = false;
        AdobeTransform = 0;
        _sawSoi = true;
        return true;
    }

    // get_lse (L1..L8; IMG 0x22004..0x223DA): the JPEG-LS preset marker whose only accepted form is the fixed 22-byte "subtract green" payload.
    // Every byte is an INPUT_BYTE: running out of bytes suspends (the marker is read again from its start, nothing is committed until the
    // last byte matched). The tests run in the order the shipped code makes them; each failure is an error_exit (the decode then fails).
    // fidelity: M3-001, M3-018
    private bool GetLse()
    {
        if (!_sawSof) throw Fatal("JERR_SOF_BEFORE");                    // L2: msg_code 0x3C, parameter "LSE"
        if (NumComponents <= 2) throw Fatal("JERR_CONVERSION_NOTIMPL");  // L3 then L7: msg_code 0x1C
        var r = Load();
        if (!TwoBytes(ref r, out int length)) return false;
        if (length != 0x18) throw Fatal("JERR_BAD_LENGTH");              // L4: msg_code 0x0C
        if (!Byte(ref r, out int id)) return false;
        if (id != 0x0D) throw Fatal("JERR_UNKNOWN_MARKER");              // L5: msg_code 0x46, parameter UnreadMarker (0xF8)
        // L6: the 22-byte payload after the length, in the order of the wire: ID 0D (above), 00 FF, 03, id of component 1, id of
        // component 0, id of component 2, 80, then 14 bytes 00 00 | 00 00 | 00 | 00 01 | 00 00 | 00 | 00 01 | 00 00.
        // Any mismatch is L7 (msg_code 0x1C, JERR_CONVERSION_NOTIMPL).
        if (!TwoBytes(ref r, out int v)) return false;
        if (v != 0x00FF) throw Fatal("JERR_CONVERSION_NOTIMPL");
        if (!Byte(ref r, out v)) return false;
        if (v != 3) throw Fatal("JERR_CONVERSION_NOTIMPL");
        if (!Byte(ref r, out v)) return false;
        if (v != CompInfo[1].ComponentId) throw Fatal("JERR_CONVERSION_NOTIMPL");
        if (!Byte(ref r, out v)) return false;
        if (v != CompInfo[0].ComponentId) throw Fatal("JERR_CONVERSION_NOTIMPL");
        if (!Byte(ref r, out v)) return false;
        if (v != CompInfo[2].ComponentId) throw Fatal("JERR_CONVERSION_NOTIMPL");
        if (!Byte(ref r, out v)) return false;
        if (v != 0x80) throw Fatal("JERR_CONVERSION_NOTIMPL");
        ReadOnlySpan<int> tail = stackalloc int[] { 0x0000, 0x0000, 0x00, 0x0001, 0x0000, 0x00, 0x0001, 0x0000 };
        for (int i = 0; i < tail.Length; i++)
        {
            bool single = i == 2 || i == 5;     // the two one-byte zeros
            if (single)
            {
                if (!Byte(ref r, out v)) return false;
            }
            else if (!TwoBytes(ref r, out v)) return false;
            if (v != tail[i]) throw Fatal("JERR_CONVERSION_NOTIMPL");
        }
        ColorTransform = 1;                                              // L8: JCT_SUBTRACT_GREEN; nothing else is stored
        Sync(r);
        return true;
    }

    // get_sof (J46)
    private bool GetSof(bool isBaseline, bool isProg, bool isArith)
    {
        IsBaseline = isBaseline;
        ProgressiveMode = isProg;
        ArithCode = isArith;
        var r = Load();
        if (!TwoBytes(ref r, out int length)) return false;
        if (!Byte(ref r, out int precision)) return false;
        if (!TwoBytes(ref r, out int height)) return false;
        if (!TwoBytes(ref r, out int width)) return false;
        if (!Byte(ref r, out int ncomp)) return false;
        DataPrecision = precision;
        ImageHeight = height;
        ImageWidth = width;
        NumComponents = ncomp;
        length -= 8;
        if (_sawSof) throw Fatal("JERR_SOF_DUPLICATE");
        if (ImageHeight <= 0 || ImageWidth <= 0 || NumComponents <= 0) throw Fatal("JERR_EMPTY_IMAGE");
        if (length != NumComponents * 3) throw Fatal("JERR_BAD_LENGTH");
        if (CompInfo.Length == 0)
        {
            CompInfo = new JpegComponent[NumComponents];
            for (int i = 0; i < NumComponents; i++) CompInfo[i] = new JpegComponent();
        }
        for (int ci = 0; ci < NumComponents; ci++)
        {
            var c = CompInfo[ci];
            c.ComponentIndex = ci;
            if (!Byte(ref r, out int id)) return false;
            c.ComponentId = id;
            // duplicate component ids are replaced by the largest id seen so far + 1 (J46)
            for (int j = 0; j < ci; j++)
                if (CompInfo[j].ComponentId == id) { c.ComponentId = -1; break; }
            if (c.ComponentId == -1)
            {
                int m = 0;
                for (int j = 0; j < ci; j++) if (CompInfo[j].ComponentId > m) m = CompInfo[j].ComponentId;
                c.ComponentId = m + 1;
            }
            if (!Byte(ref r, out int hv)) return false;
            c.HSampFactor = (hv >> 4) & 15;
            c.VSampFactor = hv & 15;
            if (!Byte(ref r, out int tq)) return false;
            c.QuantTblNo = tq;
        }
        _sawSof = true;
        Sync(r);
        return true;
    }

    // get_sos (J49; 0x2199A..0x21BEE): a component id repeated inside this SOS is renamed to the largest id already chosen in
    // this scan + 1, then the SOF record carrying that id is looked up from component 0 (not found: JERR_BAD_COMPONENT_ID)
    private bool GetSos()
    {
        if (!_sawSof) throw Fatal("JERR_SOS_NO_SOF");
        var r = Load();
        if (!TwoBytes(ref r, out int length)) return false;
        if (!Byte(ref r, out int n)) return false;
        if (length != (n + 3) * 2 || n > MaxCompsInScan || (n == 0 && !ProgressiveMode)) throw Fatal("JERR_BAD_LENGTH");
        CompsInScan = n;
        for (int i = 0; i < MaxCompsInScan; i++) CurCompInfo[i] = null;
        for (int i = 0; i < n; i++)
        {
            if (!Byte(ref r, out int cc)) return false;
            if (!Byte(ref r, out int c)) return false;
            for (int j = 0; j < i; j++)
            {
                if (cc == CurCompInfo[j]!.ComponentId)
                {
                    int m = CurCompInfo[0]!.ComponentId;
                    for (int k = 1; k < i; k++) if (CurCompInfo[k]!.ComponentId > m) m = CurCompInfo[k]!.ComponentId;
                    cc = m + 1;
                    break;
                }
            }
            JpegComponent? found = null;
            for (int ci = 0; ci < NumComponents; ci++)
            {
                if (CompInfo[ci].ComponentId == cc) { found = CompInfo[ci]; break; }
            }
            if (found == null) throw Fatal("JERR_BAD_COMPONENT_ID");
            CurCompInfo[i] = found;
            found.DcTblNo = (c >> 4) & 15;
            found.AcTblNo = c & 15;
        }
        if (!Byte(ref r, out int ss)) return false;
        if (!Byte(ref r, out int se)) return false;
        if (!Byte(ref r, out int ahal)) return false;
        Ss = ss;
        Se = se;
        Ah = (ahal >> 4) & 15;
        Al = ahal & 15;
        _nextRestartNum = 0;
        if (n != 0) InputScanNumber++;
        Sync(r);
        return true;
    }

    private bool GetDri()
    {
        var r = Load();
        if (!TwoBytes(ref r, out int length)) return false;
        if (length != 4) throw Fatal("JERR_BAD_LENGTH");
        if (!TwoBytes(ref r, out int tmp)) return false;
        RestartInterval = tmp;
        Sync(r);
        return true;
    }

    // get_dht (J47)
    private bool GetDht()
    {
        var r = Load();
        if (!TwoBytes(ref r, out int length)) return false;
        length -= 2;
        var bits = new byte[17];
        var huffval = new byte[256];
        while (length > 16)
        {
            if (!Byte(ref r, out int index)) return false;
            bits[0] = 0;
            int count = 0;
            for (int i = 1; i <= 16; i++)
            {
                if (!Byte(ref r, out int b)) return false;
                bits[i] = (byte)b;
                count += b;
            }
            length -= 1 + 16;
            if (count > 256 || count > length) throw Fatal("JERR_BAD_HUFF_TABLE");
            for (int i = 0; i < count; i++)
            {
                if (!Byte(ref r, out int b)) return false;
                huffval[i] = (byte)b;
            }
            length -= count;
            JpegHuffTable?[] arr;
            if ((index & 0x10) != 0)
            {
                index -= 0x10;
                if (index < 0 || index >= 4) throw Fatal("JERR_DHT_INDEX");
                arr = _acHuffTblPtrs;
            }
            else
            {
                if (index < 0 || index >= 4) throw Fatal("JERR_DHT_INDEX");
                arr = _dcHuffTblPtrs;
            }
            var t = arr[index] ??= new JpegHuffTable();
            Array.Copy(bits, t.Bits, 17);
            Array.Copy(huffval, t.HuffVal, 256);
        }
        if (length != 0) throw Fatal("JERR_BAD_LENGTH");
        Sync(r);
        return true;
    }

    // get_dqt (J48; 0x21D18..0x21F38): after the Pq/Tq byte, a table that has fewer than 64 (8-bit) or 128 (16-bit) bytes left starts as
    // all ones and takes count = bytes (8-bit) or bytes / 2 (16-bit) values; otherwise count = 64. Counts 4, 9, 16, 25, 36 and 49 select
    // the size-specific natural order, every other count the full one.
    private bool GetDqt()
    {
        var r = Load();
        if (!TwoBytes(ref r, out int length)) return false;
        length -= 2;
        while (length > 0)
        {
            if (!Byte(ref r, out int n)) return false;
            length--;
            int prec = n >> 4;
            n &= 0x0F;
            if (n >= 4) throw Fatal("JERR_DQT_INDEX");
            var q = _quantTblPtrs[n] ??= new ushort[64];
            int count;
            if (prec != 0)
            {
                if (length > 127) count = DctSize2;
                else
                {
                    for (int i = 0; i < DctSize2; i++) q[i] = 1;
                    count = length >> 1;
                }
            }
            else
            {
                if (length > 63) count = DctSize2;
                else
                {
                    for (int i = 0; i < DctSize2; i++) q[i] = 1;
                    count = length;
                }
            }
            int[] order = count switch
            {
                4 => JpegTables.NaturalOrder(2),
                9 => JpegTables.NaturalOrder(3),
                16 => JpegTables.NaturalOrder(4),
                25 => JpegTables.NaturalOrder(5),
                36 => JpegTables.NaturalOrder(6),
                49 => JpegTables.NaturalOrder(7),
                _ => JpegTables.NaturalOrder8,
            };
            for (int i = 0; i < count; i++)
            {
                int tmp;
                if (prec != 0) { if (!TwoBytes(ref r, out tmp)) return false; }
                else { if (!Byte(ref r, out tmp)) return false; }
                q[order[i]] = (ushort)tmp;
            }
            length -= count;
            if (prec != 0) length -= count;
        }
        if (length != 0) throw Fatal("JERR_BAD_LENGTH");
        Sync(r);
        return true;
    }

    // get_dac (J67)
    private bool GetDac()
    {
        var r = Load();
        if (!TwoBytes(ref r, out int length)) return false;
        length -= 2;
        while (length > 0)
        {
            if (!Byte(ref r, out int index)) return false;
            if (!Byte(ref r, out int val)) return false;
            length -= 2;
            if (index < 0 || index >= (2 * 16))
                throw Fatal("JERR_DAC_INDEX");
            if (index >= 16)
            {
                _arithAcK[index - 16] = (byte)val;
            }
            else
            {
                _arithDcL[index] = (byte)(val & 0x0F);
                _arithDcU[index] = (byte)(val >> 4);
                if (_arithDcL[index] > _arithDcU[index]) throw Fatal("JERR_DAC_VALUE");
            }
        }
        if (length != 0) throw Fatal("JERR_BAD_LENGTH");
        Sync(r);
        return true;
    }

    private bool SkipVariable()
    {
        var r = Load();
        if (!TwoBytes(ref r, out int length)) return false;
        length -= 2;
        Sync(r);
        if (length > 0) SkipInputData(length);
        return true;
    }

    // APP0 (JFIF) / APP14 (Adobe): the first bytes are examined, the rest skipped (jdmarker get_interesting_appn)
    private bool GetInterestingAppn()
    {
        var r = Load();
        if (!TwoBytes(ref r, out int length)) return false;
        length -= 2;
        int numtoread;
        int appn = UnreadMarker - 0xE0;
        const int limit = 14;
        numtoread = length > 0 ? (length < limit ? length : limit) : 0;
        var data = new byte[14];
        for (int i = 0; i < numtoread; i++)
        {
            if (!Byte(ref r, out int b)) return false;
            data[i] = (byte)b;
        }
        length -= numtoread;
        if (appn == 0) ExamineApp0(data, numtoread, length);
        else ExamineApp14(data, numtoread, length);
        Sync(r);
        if (length > 0) SkipInputData(length);
        return true;
    }

    private void ExamineApp0(byte[] data, int datalen, long remaining)
    {
        if (datalen >= 14 && data[0] == 0x4A && data[1] == 0x46 && data[2] == 0x49 && data[3] == 0x46 && data[4] == 0)
        {
            SawJfifMarker = true;
            // major/minor version bytes: a major other than 1 is a warning (JWRN_JFIF_MAJOR, 0x2130E..0x21356)
            if (data[5] != 1) Warn("JWRN_JFIF_MAJOR", data[5], data[6]);
        }
        else if (datalen >= 6 && data[0] == 0x4A && data[1] == 0x46 && data[2] == 0x58 && data[3] == 0x58 && data[4] == 0)
        {
            // JFXX extension marker: nothing to record
        }
    }

    private void ExamineApp14(byte[] data, int datalen, long remaining)
    {
        if (datalen >= 12 && data[0] == 0x41 && data[1] == 0x64 && data[2] == 0x6F && data[3] == 0x62 && data[4] == 0x65)
        {
            SawAdobeMarker = true;
            AdobeTransform = data[11];
        }
    }

    // read_markers
    private int ReadMarkers()
    {
        for (; ; )
        {
            if (UnreadMarker == 0)
            {
                if (!_sawSoi)
                {
                    if (!FirstMarker()) return JpegSuspended;
                }
                else
                {
                    if (!NextMarker()) return JpegSuspended;
                }
            }
            switch (UnreadMarker)
            {
                case 0xD8:
                    if (!GetSoi()) return JpegSuspended;
                    break;
                case 0xC0: if (!GetSof(true, false, false)) return JpegSuspended; break;
                case 0xC1: if (!GetSof(false, false, false)) return JpegSuspended; break;
                case 0xC2: if (!GetSof(false, true, false)) return JpegSuspended; break;
                case 0xC9: if (!GetSof(false, false, true)) return JpegSuspended; break;
                case 0xCA: if (!GetSof(false, true, true)) return JpegSuspended; break;
                case 0xC3: case 0xC5: case 0xC6: case 0xC7: case 0xC8: case 0xCB: case 0xCD: case 0xCE: case 0xCF:
                    throw Fatal("JERR_SOF_UNSUPPORTED");
                case 0xDA:
                    if (!GetSos()) return JpegSuspended;
                    UnreadMarker = 0;
                    return JpegReachedSos;
                case 0xD9:
                    UnreadMarker = 0;
                    return JpegReachedEoi;
                case 0xCC: if (!GetDac()) return JpegSuspended; break;
                case 0xC4: if (!GetDht()) return JpegSuspended; break;
                case 0xDB: if (!GetDqt()) return JpegSuspended; break;
                case 0xDD: if (!GetDri()) return JpegSuspended; break;
                case 0xE0: case 0xEE:
                    if (!GetInterestingAppn()) return JpegSuspended;
                    break;
                case >= 0xE1 and <= 0xED: case 0xEF:
                case 0xFE:
                    if (!SkipVariable()) return JpegSuspended;
                    break;
                case 0xF8:          // L1: the LSE marker is dispatched to get_lse (IMG 0x218DA)
                    if (!GetLse()) return JpegSuspended;
                    break;
                case >= 0xD0 and <= 0xD7: case 0x01:
                    break;
                case 0xDC:
                    if (!SkipVariable()) return JpegSuspended;
                    break;
                default:
                    throw Fatal("JERR_UNKNOWN_MARKER");
            }
            UnreadMarker = 0;
        }
    }

    // read_restart_marker (J50)
    private bool ReadRestartMarker()
    {
        if (UnreadMarker == 0)
        {
            if (!NextMarker()) return false;
        }
        if (UnreadMarker == 0xD0 + _nextRestartNum)
        {
            UnreadMarker = 0;
        }
        else
        {
            if (!ResyncToRestart(_nextRestartNum)) return false;
        }
        _nextRestartNum = (_nextRestartNum + 1) & 7;
        return true;
    }

    // jpeg_resync_to_restart (J51)
    private bool ResyncToRestart(int desired)
    {
        int marker = UnreadMarker;
        int action;
        Warn("JWRN_MUST_RESYNC", marker, desired);
        for (; ; )
        {
            if (marker < 0xC0) action = 2;
            else if (marker < 0xD0 || marker > 0xD7) action = 3;
            else
            {
                if (marker == 0xD0 + ((desired + 1) & 7) || marker == 0xD0 + ((desired + 2) & 7)) action = 3;
                else if (marker == 0xD0 + ((desired - 1) & 7) || marker == 0xD0 + ((desired - 2) & 7)) action = 2;
                else action = 1;
            }
            switch (action)
            {
                case 1:
                    UnreadMarker = 0;
                    return true;
                case 2:
                    if (!NextMarker()) return false;
                    marker = UnreadMarker;
                    break;
                default:
                    return true;
            }
        }
    }
}
