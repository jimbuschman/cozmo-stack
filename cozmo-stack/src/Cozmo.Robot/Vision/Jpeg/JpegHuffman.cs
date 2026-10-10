namespace Cozmo.Robot.Vision.Jpeg;

// fidelity: M3-001, M3-018
// jdhuff.c (sequential and progressive Huffman decoding, merged in libjpeg 9; J24..J30, J57..J60, J108). The bit reader
// works on a copy of the source position and the reservoir that is committed only when a whole MCU has been decoded
// (BITREAD_SAVE_STATE); a suspension returns false and leaves the committed state alone (J29).
internal readonly record struct JpegBlock(short[] A, int O);

internal sealed class DerivedHuff
{
    public readonly int[] MaxCode = new int[18];
    public readonly int[] ValOffset = new int[17];
    public JpegHuffTable Pub = null!;
    public readonly int[] LookNBits = new int[256];
    public readonly byte[] LookSym = new byte[256];
}

internal sealed partial class JpegDecoder
{
    private const int HuffLookahead = 8;
    private const int MinGetBits = 25;      // BIT_BUF_SIZE (32) - 7

    private struct BitState
    {
        public uint GetBuffer;
        public int BitsLeft;
        public int Next;
        public int Avail;
    }

    // ---- entropy decoder state (huff_entropy_decoder)
    private BitState _bitstate;                       // bitread_perm_state: get_buffer and bits_left persist across MCUs
    private readonly int[] _lastDcVal = new int[MaxCompsInScan];
    private int _eobRun;
    private int _restartsToGo;
    private bool _insufficientData;
    private readonly DerivedHuff?[] _dcDerivedTbls = new DerivedHuff?[4];
    private readonly DerivedHuff?[] _acDerivedTbls = new DerivedHuff?[4];
    private readonly DerivedHuff?[] _derivedTbls = new DerivedHuff?[4];     // progressive: one table per slot
    private DerivedHuff? _acDerivedTbl;
    private readonly DerivedHuff?[] _dcCurTbls = new DerivedHuff?[DMaxBlocksInMcu];
    private readonly DerivedHuff?[] _acCurTbls = new DerivedHuff?[DMaxBlocksInMcu];
    private readonly int[] _coefLimit = new int[DMaxBlocksInMcu];
    private int[][]? _coefBits;                       // progressive scan bookkeeping (JWRN_BOGUS_PROGRESSION only)
    private enum Decoder { None, Sequential, DcFirst, AcFirst, DcRefine, AcRefine }
    private Decoder _decoder;

    private static readonly int[] ExtendTest = new int[16]
    {
        0, 0x0001, 0x0002, 0x0004, 0x0008, 0x0010, 0x0020, 0x0040, 0x0080, 0x0100, 0x0200, 0x0400, 0x0800, 0x1000, 0x2000, 0x4000,
    };

    private static int HuffExtend(int x, int s) => x < ExtendTest[s] ? x + ((-1) << s) + 1 : x;

    // jpeg_make_d_derived_tbl (J28)
    private DerivedHuff MakeDDerivedTbl(bool isDc, int tblno, DerivedHuff? existing)
    {
        if (tblno < 0 || tblno >= 4) throw Fatal("JERR_NO_HUFF_TABLE");
        var htbl = isDc ? _dcHuffTblPtrs[tblno] : _acHuffTblPtrs[tblno];
        if (htbl == null) throw Fatal("JERR_NO_HUFF_TABLE");
        var dtbl = existing ?? new DerivedHuff();
        dtbl.Pub = htbl;
        var huffsize = new int[257];
        var huffcode = new int[257];
        int p = 0;
        for (int l = 1; l <= 16; l++)
        {
            int i = htbl.Bits[l];
            if (i < 0 || p + i > 256) throw Fatal("JERR_BAD_HUFF_TABLE");
            while (i-- > 0) huffsize[p++] = l;
        }
        huffsize[p] = 0;
        int numsymbols = p;
        int code = 0;
        int si = huffsize[0];
        p = 0;
        while (huffsize[p] != 0)
        {
            while (huffsize[p] == si)
            {
                huffcode[p++] = code;
                code++;
            }
            if (code >= (1 << si)) throw Fatal("JERR_BAD_HUFF_TABLE");
            code <<= 1;
            si++;
        }
        p = 0;
        for (int l = 1; l <= 16; l++)
        {
            if (htbl.Bits[l] != 0)
            {
                dtbl.ValOffset[l] = p - huffcode[p];
                p += htbl.Bits[l];
                dtbl.MaxCode[l] = huffcode[p - 1];
            }
            else
            {
                dtbl.MaxCode[l] = -1;
            }
        }
        dtbl.MaxCode[17] = 0xFFFFF;
        Array.Clear(dtbl.LookNBits);
        p = 0;
        for (int l = 1; l <= HuffLookahead; l++)
        {
            for (int i = 1; i <= htbl.Bits[l]; i++, p++)
            {
                int lookbits = huffcode[p] << (HuffLookahead - l);
                for (int ctr = 1 << (HuffLookahead - l); ctr > 0; ctr--)
                {
                    dtbl.LookNBits[lookbits] = l;
                    dtbl.LookSym[lookbits] = htbl.HuffVal[p];
                    lookbits++;
                }
            }
        }
        if (isDc)
        {
            for (int i = 0; i < numsymbols; i++)
            {
                int sym = htbl.HuffVal[i];
                if (sym < 0 || sym > 15) throw Fatal("JERR_BAD_HUFF_TABLE");
            }
        }
        return dtbl;
    }

    // jpeg_fill_bit_buffer (J26): false = suspension (nothing committed)
    private bool FillBitBuffer(ref BitState st, int nbits)
    {
        uint getBuffer = st.GetBuffer;
        int bitsLeft = st.BitsLeft;
        int next = st.Next;
        int avail = st.Avail;
        bool noMore = false;
        if (UnreadMarker == 0)
        {
            while (bitsLeft < MinGetBits)
            {
                if (avail == 0)
                {
                    if (!FillInputBuffer()) return false;
                    next = _next;
                    avail = _avail;
                }
                avail--;
                int c = _buf[next++];
                if (c == 0xFF)
                {
                    do
                    {
                        if (avail == 0)
                        {
                            if (!FillInputBuffer()) return false;
                            next = _next;
                            avail = _avail;
                        }
                        avail--;
                        c = _buf[next++];
                    } while (c == 0xFF);
                    if (c == 0)
                    {
                        c = 0xFF;
                    }
                    else
                    {
                        UnreadMarker = c;
                        noMore = true;
                        break;
                    }
                }
                getBuffer = (getBuffer << 8) | (uint)c;
                bitsLeft += 8;
            }
        }
        else
        {
            noMore = true;
        }
        if (noMore)
        {
            if (nbits > bitsLeft)
            {
                if (!_insufficientData)
                {
                    Warn("JWRN_HIT_MARKER");
                    _insufficientData = true;
                }
                getBuffer <<= MinGetBits - bitsLeft;
                bitsLeft = MinGetBits;
            }
        }
        st.GetBuffer = getBuffer;
        st.BitsLeft = bitsLeft;
        st.Next = next;
        st.Avail = avail;
        return true;
    }

    // CHECK_BIT_BUFFER
    private bool CheckBits(ref BitState st, int nbits)
    {
        if (st.BitsLeft < nbits)
        {
            if (!FillBitBuffer(ref st, nbits)) return false;
        }
        return true;
    }

    private static int GetBits(ref BitState st, int nbits)
    {
        st.BitsLeft -= nbits;
        return (int)(st.GetBuffer >> st.BitsLeft) & ((1 << nbits) - 1);
    }

    // jpeg_huff_decode (J27): the slow path; returns -1 when it must suspend
    private int HuffDecodeSlow(ref BitState st, DerivedHuff htbl, int minBits)
    {
        int l = minBits;
        if (!CheckBits(ref st, l)) return -1;
        int code = GetBits(ref st, l);
        while (code > htbl.MaxCode[l])
        {
            code <<= 1;
            if (!CheckBits(ref st, 1)) return -1;
            code |= GetBits(ref st, 1);
            l++;
        }
        if (l > 16)
        {
            Warn("JWRN_HUFF_BAD_CODE");
            return 0;
        }
        return htbl.Pub.HuffVal[code + htbl.ValOffset[l]];
    }

    // HUFF_DECODE: result >= 0, or -1 for a suspension
    private int HuffDecode(ref BitState st, DerivedHuff htbl)
    {
        int nb;
        if (st.BitsLeft < HuffLookahead)
        {
            if (!FillBitBuffer(ref st, 0)) return -1;
            if (st.BitsLeft < HuffLookahead)
            {
                return HuffDecodeSlow(ref st, htbl, 1);
            }
        }
        int look = (int)(st.GetBuffer >> (st.BitsLeft - HuffLookahead)) & ((1 << HuffLookahead) - 1);
        if ((nb = htbl.LookNBits[look]) != 0)
        {
            st.BitsLeft -= nb;
            return htbl.LookSym[look];
        }
        return HuffDecodeSlow(ref st, htbl, HuffLookahead + 1);
    }

    // process_restart (J30)
    private bool ProcessRestart()
    {
        _discardedBytes += _bitstate.BitsLeft / 8;
        _bitstate.BitsLeft = 0;
        if (!ReadRestartMarker()) return false;
        for (int ci = 0; ci < CompsInScan; ci++) _lastDcVal[ci] = 0;
        _eobRun = 0;
        _restartsToGo = RestartInterval;
        if (UnreadMarker == 0) _insufficientData = false;
        return true;
    }

    // BITREAD_LOAD_STATE / BITREAD_SAVE_STATE
    private BitState LoadBits()
    {
        var st = _bitstate;
        st.Next = _next;
        st.Avail = _avail;
        return st;
    }

    private void SaveBits(in BitState st)
    {
        _next = st.Next;
        _avail = st.Avail;
        _bitstate.GetBuffer = st.GetBuffer;
        _bitstate.BitsLeft = st.BitsLeft;
    }

    // start_pass_huff_decoder (J24, J108): sequential and progressive
    private void HuffStartPass()
    {
        if (ProgressiveMode)
        {
            bool isDcBand = Ss == 0;
            bool bad = false;
            if (isDcBand)
            {
                if (Se != 0) bad = true;
            }
            else
            {
                if (Ss > Se || Se > LimSe) bad = true;
                if (CompsInScan != 1) bad = true;
            }
            if (Ah != 0)
            {
                if (Al != Ah - 1) bad = true;
            }
            if (Al > 13) bad = true;
            if (bad) throw Fatal("JERR_BAD_PROGRESSION");
            for (int ci = 0; ci < CompsInScan; ci++)
            {
                int cindex = CurCompInfo[ci]!.ComponentIndex;
                var coefBitPtr = _coefBits![cindex];
                if (!isDcBand && coefBitPtr[0] < 0) Warn("JWRN_BOGUS_PROGRESSION", cindex, 0);
                for (int coefi = Ss; coefi <= Se; coefi++)
                {
                    int expected = coefBitPtr[coefi] < 0 ? 0 : coefBitPtr[coefi];
                    if (Ah != expected) Warn("JWRN_BOGUS_PROGRESSION", cindex, coefi);
                    coefBitPtr[coefi] = Al;
                }
            }
            if (Ah == 0) _decoder = isDcBand ? Decoder.DcFirst : Decoder.AcFirst;
            else _decoder = isDcBand ? Decoder.DcRefine : Decoder.AcRefine;
            for (int ci = 0; ci < CompsInScan; ci++)
            {
                var compptr = CurCompInfo[ci]!;
                if (isDcBand)
                {
                    if (Ah == 0)
                    {
                        int tbl = compptr.DcTblNo;
                        var made = MakeDDerivedTbl(true, tbl, (uint)tbl < 4 ? _derivedTbls[tbl] : null);       // range-checks the selector first
                        _derivedTbls[tbl] = made;
                    }
                }
                else
                {
                    int tbl = compptr.AcTblNo;
                    var made = MakeDDerivedTbl(false, tbl, (uint)tbl < 4 ? _derivedTbls[tbl] : null);
                    _derivedTbls[tbl] = made;
                    _acDerivedTbl = made;
                }
                _lastDcVal[ci] = 0;
            }
            _bitstate.BitsLeft = 0;
            _bitstate.GetBuffer = 0;
            _insufficientData = false;
            _eobRun = 0;
            _restartsToGo = RestartInterval;
            return;
        }

        // sequential
        if (Ss != 0 || Ah != 0 || Al != 0 || (Se < DctSize2 && Se != LimSe)) Warn("JWRN_NOT_SEQUENTIAL");
        _decoder = Decoder.Sequential;
        for (int ci = 0; ci < CompsInScan; ci++)
        {
            var compptr = CurCompInfo[ci]!;
            int dctbl = compptr.DcTblNo;
            int actbl = compptr.AcTblNo;
            var dmade = MakeDDerivedTbl(true, dctbl, (uint)dctbl < 4 ? _dcDerivedTbls[dctbl] : null);
            _dcDerivedTbls[dctbl] = dmade;
            var amade = MakeDDerivedTbl(false, actbl, (uint)actbl < 4 ? _acDerivedTbls[actbl] : null);
            _acDerivedTbls[actbl] = amade;
            _lastDcVal[ci] = 0;
        }
        for (int blkn = 0; blkn < BlocksInMcu; blkn++)
        {
            int ci = McuMembership[blkn];
            var compptr = CurCompInfo[ci]!;
            _dcCurTbls[blkn] = _dcDerivedTbls[compptr.DcTblNo];
            _acCurTbls[blkn] = _acDerivedTbls[compptr.AcTblNo];
            // per-block coefficient limit (J109): 0 for an unneeded component, otherwise 1 + the zigzag index of the coefficient
            // (min(v, n), min(h, n)) of the n x n block, n = 8 unless lim_Se selects a smaller block size
            if (compptr.ComponentNeeded)
            {
                int v = compptr.DctVScaledSize, hh = compptr.DctHScaledSize;
                int n = LimSe switch { 3 => 2, 8 => 3, 15 => 4, 24 => 5, 35 => 6, 48 => 7, _ => 8 };
                if (LimSe == 0)
                {
                    _coefLimit[blkn] = 1;
                }
                else
                {
                    if (v <= 0 || v > n) v = n;
                    if (hh <= 0 || hh > n) hh = n;
                    _coefLimit[blkn] = 1 + JpegTables.ZigzagLimit(n, v, hh);
                }
            }
            else
            {
                _coefLimit[blkn] = 0;
            }
        }
        _bitstate.BitsLeft = 0;
        _bitstate.GetBuffer = 0;
        _insufficientData = false;
        _restartsToGo = RestartInterval;
    }

    private bool HuffDecodeMcu(JpegBlock[] mcuData)
    {
        switch (_decoder)
        {
            case Decoder.Sequential: return DecodeMcuSequential(mcuData);
            case Decoder.DcFirst: return DecodeMcuDcFirst(mcuData);
            case Decoder.AcFirst: return DecodeMcuAcFirst(mcuData);
            case Decoder.DcRefine: return DecodeMcuDcRefine(mcuData);
            case Decoder.AcRefine: return DecodeMcuAcRefine(mcuData);
            default: throw new InvalidOperationException();
        }
    }

    // decode_mcu (J29) and decode_mcu_sub (J75): one body for both, because for a needed component the per-block coefficient limit is
    // lim_Se + 1 at scale 1/1 (the cap of J109 always lands on the last coefficient), which makes the second, discarding loop empty
    private bool DecodeMcuSequential(JpegBlock[] mcuData)
    {
        if (RestartInterval != 0)
        {
            if (_restartsToGo == 0)
                if (!ProcessRestart()) return false;
        }
        if (!_insufficientData)
        {
            var br = LoadBits();
            var lastDc = new int[MaxCompsInScan];
            Array.Copy(_lastDcVal, lastDc, MaxCompsInScan);
            var order = NaturalOrder;
            int se = LimSe;
            for (int blkn = 0; blkn < BlocksInMcu; blkn++)
            {
                var block = mcuData[blkn];
                var dctbl = _dcCurTbls[blkn]!;
                var actbl = _acCurTbls[blkn]!;
                int s = HuffDecode(ref br, dctbl);
                if (s < 0) return false;
                int k = 1;
                int coefLimit = _coefLimit[blkn];
                if (coefLimit != 0)
                {
                    if (s != 0)
                    {
                        if (!CheckBits(ref br, s)) return false;
                        int r = GetBits(ref br, s);
                        s = HuffExtend(r, s);
                    }
                    int ci = McuMembership[blkn];
                    s += lastDc[ci];
                    lastDc[ci] = s;
                    block.A[block.O] = (short)s;
                    bool endOfBlock = false;
                    for (; k < coefLimit; k++)
                    {
                        s = HuffDecode(ref br, actbl);
                        if (s < 0) return false;
                        int r = s >> 4;
                        s &= 15;
                        if (s != 0)
                        {
                            k += r;
                            if (!CheckBits(ref br, s)) return false;
                            r = GetBits(ref br, s);
                            s = HuffExtend(r, s);
                            block.A[block.O + order[k]] = (short)s;
                        }
                        else
                        {
                            if (r != 15) { endOfBlock = true; break; }
                            k += 15;
                        }
                    }
                    if (endOfBlock) continue;
                }
                else
                {
                    if (s != 0)
                    {
                        if (!CheckBits(ref br, s)) return false;
                        GetBits(ref br, s);
                    }
                }
                for (; k <= se; k++)
                {
                    s = HuffDecode(ref br, actbl);
                    if (s < 0) return false;
                    int r = s >> 4;
                    s &= 15;
                    if (s != 0)
                    {
                        k += r;
                        if (!CheckBits(ref br, s)) return false;
                        GetBits(ref br, s);
                    }
                    else
                    {
                        if (r != 15) break;
                        k += 15;
                    }
                }
            }
            SaveBits(br);
            Array.Copy(lastDc, _lastDcVal, MaxCompsInScan);
        }
        _restartsToGo--;
        return true;
    }

    // decode_mcu_DC_first (J57)
    private bool DecodeMcuDcFirst(JpegBlock[] mcuData)
    {
        if (RestartInterval != 0)
        {
            if (_restartsToGo == 0)
                if (!ProcessRestart()) return false;
        }
        if (!_insufficientData)
        {
            var br = LoadBits();
            var lastDc = new int[MaxCompsInScan];
            Array.Copy(_lastDcVal, lastDc, MaxCompsInScan);
            for (int blkn = 0; blkn < BlocksInMcu; blkn++)
            {
                var block = mcuData[blkn];
                int ci = McuMembership[blkn];
                var compptr = CurCompInfo[ci]!;
                var tbl = _derivedTbls[compptr.DcTblNo]!;
                int s = HuffDecode(ref br, tbl);
                if (s < 0) return false;
                if (s != 0)
                {
                    if (!CheckBits(ref br, s)) return false;
                    int r = GetBits(ref br, s);
                    s = HuffExtend(r, s);
                }
                s += lastDc[ci];
                lastDc[ci] = s;
                block.A[block.O] = (short)(s << Al);
            }
            SaveBits(br);
            Array.Copy(lastDc, _lastDcVal, MaxCompsInScan);
        }
        _restartsToGo--;
        return true;
    }

    // decode_mcu_AC_first (J58)
    private bool DecodeMcuAcFirst(JpegBlock[] mcuData)
    {
        if (RestartInterval != 0)
        {
            if (_restartsToGo == 0)
                if (!ProcessRestart()) return false;
        }
        if (!_insufficientData)
        {
            int eobrun = _eobRun;
            if (eobrun > 0)
            {
                eobrun--;
            }
            else
            {
                var br = LoadBits();
                var block = mcuData[0];
                var tbl = _acDerivedTbl!;
                var order = JpegTables.NaturalOrder8;
                for (int k = Ss; k <= Se; k++)
                {
                    int s = HuffDecode(ref br, tbl);
                    if (s < 0) return false;
                    int r = s >> 4;
                    s &= 15;
                    if (s != 0)
                    {
                        k += r;
                        if (!CheckBits(ref br, s)) return false;
                        r = GetBits(ref br, s);
                        s = HuffExtend(r, s);
                        block.A[block.O + order[k]] = (short)(s << Al);
                    }
                    else
                    {
                        if (r == 15)
                        {
                            k += 15;
                        }
                        else
                        {
                            eobrun = 1 << r;
                            if (r != 0)
                            {
                                if (!CheckBits(ref br, r)) return false;
                                r = GetBits(ref br, r);
                                eobrun += r;
                            }
                            eobrun--;
                            break;
                        }
                    }
                }
                SaveBits(br);
            }
            _eobRun = eobrun;
        }
        _restartsToGo--;
        return true;
    }

    // decode_mcu_DC_refine (J59)
    private bool DecodeMcuDcRefine(JpegBlock[] mcuData)
    {
        int p1 = 1 << Al;
        if (RestartInterval != 0)
        {
            if (_restartsToGo == 0)
                if (!ProcessRestart()) return false;
        }
        var br = LoadBits();
        for (int blkn = 0; blkn < BlocksInMcu; blkn++)
        {
            var block = mcuData[blkn];
            if (!CheckBits(ref br, 1)) return false;
            if (GetBits(ref br, 1) != 0) block.A[block.O] |= (short)p1;
        }
        SaveBits(br);
        _restartsToGo--;
        return true;
    }

    // decode_mcu_AC_refine (J60)
    private bool DecodeMcuAcRefine(JpegBlock[] mcuData)
    {
        int p1 = 1 << Al;
        int m1 = (-1) << Al;
        if (RestartInterval != 0)
        {
            if (_restartsToGo == 0)
                if (!ProcessRestart()) return false;
        }
        if (!_insufficientData)
        {
            var br = LoadBits();
            int eobrun = _eobRun;
            var block = mcuData[0];
            var tbl = _acDerivedTbl!;
            var order = JpegTables.NaturalOrder8;
            var newnzPos = new int[DctSize2];
            int numNewnz = 0;
            int k = Ss;
            bool ok = true;
            if (eobrun == 0)
            {
                for (; k <= Se; k++)
                {
                    int s = HuffDecode(ref br, tbl);
                    if (s < 0) { ok = false; break; }
                    int r = s >> 4;
                    s &= 15;
                    if (s != 0)
                    {
                        if (s != 1) Warn("JWRN_HUFF_BAD_CODE");
                        if (!CheckBits(ref br, 1)) { ok = false; break; }
                        if (GetBits(ref br, 1) != 0) s = p1; else s = m1;
                    }
                    else
                    {
                        if (r != 15)
                        {
                            eobrun = 1 << r;
                            if (r != 0)
                            {
                                if (!CheckBits(ref br, r)) { ok = false; break; }
                                r = GetBits(ref br, r);
                                eobrun += r;
                            }
                            break;
                        }
                    }
                    do
                    {
                        int pos0 = block.O + order[k];
                        if (block.A[pos0] != 0)
                        {
                            if (!CheckBits(ref br, 1)) { ok = false; break; }
                            if (GetBits(ref br, 1) != 0)
                            {
                                if ((block.A[pos0] & p1) == 0)
                                {
                                    if (block.A[pos0] >= 0) block.A[pos0] += (short)p1; else block.A[pos0] += (short)m1;
                                }
                            }
                        }
                        else
                        {
                            if (--r < 0) break;
                        }
                        k++;
                    } while (k <= Se);
                    if (!ok) break;
                    if (s != 0)
                    {
                        int pos = order[k];
                        block.A[block.O + pos] = (short)s;
                        newnzPos[numNewnz++] = pos;
                    }
                }
            }
            if (ok && eobrun > 0)
            {
                for (; k <= Se; k++)
                {
                    int pos0 = block.O + order[k];
                    if (block.A[pos0] != 0)
                    {
                        if (!CheckBits(ref br, 1)) { ok = false; break; }
                        if (GetBits(ref br, 1) != 0)
                        {
                            if ((block.A[pos0] & p1) == 0)
                            {
                                if (block.A[pos0] >= 0) block.A[pos0] += (short)p1; else block.A[pos0] += (short)m1;
                            }
                        }
                    }
                }
                if (ok) eobrun--;
            }
            if (!ok)
            {
                while (numNewnz > 0) block.A[block.O + newnzPos[--numNewnz]] = 0;
                return false;
            }
            SaveBits(br);
            _eobRun = eobrun;
        }
        _restartsToGo--;
        return true;
    }
}
