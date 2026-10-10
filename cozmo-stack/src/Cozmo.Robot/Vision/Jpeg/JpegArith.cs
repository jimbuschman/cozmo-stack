namespace Cozmo.Robot.Vision.Jpeg;

// fidelity: M3-001, M3-018
// jdarith.c (J61..J67): arithmetic-coded JPEG (SOF9 sequential, SOF10 progressive) is compiled into the shipped decoder. The
// arithmetic decoder does not support suspension: running out of input is fatal (JERR_CANT_SUSPEND, J62).
internal sealed partial class JpegDecoder
{
    private const int DcStatBins = 64;
    private const int AcStatBins = 256;

    private int _arC;
    private int _arA;
    private int _arCt;
    private readonly int[] _arLastDcVal = new int[MaxCompsInScan];
    private readonly int[] _arDcContext = new int[MaxCompsInScan];
    private readonly byte[]?[] _arDcStats = new byte[]?[16];
    private readonly byte[]?[] _arAcStats = new byte[]?[16];
    private readonly byte[] _arFixedBin = { 113 };
    private int _arRestartsToGo;
    private enum ArithDecoder { Sequential, DcFirst, AcFirst, DcRefine, AcRefine }
    private ArithDecoder _arDecoder;

    // jinit_arith_decoder
    private void ArithInit()
    {
        if (ProgressiveMode)
        {
            _coefBits = new int[NumComponents][];
            for (int ci = 0; ci < NumComponents; ci++)
            {
                _coefBits[ci] = new int[DctSize2];
                Array.Fill(_coefBits[ci], -1);
            }
        }
        for (int i = 0; i < 16; i++)
        {
            _arDcStats[i] = null;
            _arAcStats[i] = null;
        }
        _arFixedBin[0] = 113;
    }

    // start_pass (arithmetic, J61)
    private void ArithStartPass()
    {
        if (ProgressiveMode)
        {
            bool bad = false;
            if (Ss == 0)
            {
                if (Se != 0) bad = true;
            }
            else
            {
                if (Se < Ss || Se > LimSe) bad = true;
                if (CompsInScan != 1) bad = true;
            }
            if (Ah != 0)
            {
                if (Ah - 1 != Al) bad = true;
            }
            if (Al > 13) bad = true;
            if (bad) throw Fatal("JERR_BAD_PROGRESSION");
            for (int ci = 0; ci < CompsInScan; ci++)
            {
                int cindex = CurCompInfo[ci]!.ComponentIndex;
                var coefBitPtr = _coefBits![cindex];
                if (Ss != 0 && coefBitPtr[0] < 0) Warn("JWRN_BOGUS_PROGRESSION", cindex, 0);
                for (int coefi = Ss; coefi <= Se; coefi++)
                {
                    int expected = coefBitPtr[coefi] < 0 ? 0 : coefBitPtr[coefi];
                    if (Ah != expected) Warn("JWRN_BOGUS_PROGRESSION", cindex, coefi);
                    coefBitPtr[coefi] = Al;
                }
            }
            if (Ah == 0) _arDecoder = Ss == 0 ? ArithDecoder.DcFirst : ArithDecoder.AcFirst;
            else _arDecoder = Ss == 0 ? ArithDecoder.DcRefine : ArithDecoder.AcRefine;
        }
        else
        {
            if (Ss != 0 || Ah != 0 || Al != 0 || (Se < DctSize2 && Se != LimSe)) Warn("JWRN_NOT_SEQUENTIAL");
            _arDecoder = ArithDecoder.Sequential;
        }
        for (int ci = 0; ci < CompsInScan; ci++)
        {
            var compptr = CurCompInfo[ci]!;
            if (!ProgressiveMode || (Ss == 0 && Ah == 0))
            {
                int tbl = compptr.DcTblNo;
                if (tbl < 0 || tbl >= 16) throw Fatal("JERR_NO_ARITH_TABLE");
                _arDcStats[tbl] ??= new byte[DcStatBins];
                Array.Clear(_arDcStats[tbl]!);
                _arLastDcVal[ci] = 0;
                _arDcContext[ci] = 0;
            }
            if (!ProgressiveMode || Ss != 0)
            {
                int tbl = compptr.AcTblNo;
                if (tbl < 0 || tbl >= 16) throw Fatal("JERR_NO_ARITH_TABLE");
                _arAcStats[tbl] ??= new byte[AcStatBins];
                Array.Clear(_arAcStats[tbl]!);
            }
        }
        _arC = 0;
        _arA = 0;
        _arCt = -16;
        _arRestartsToGo = RestartInterval;
    }

    private bool ArithDecodeMcu(JpegBlock[] mcu)
    {
        switch (_arDecoder)
        {
            case ArithDecoder.Sequential: return ArithDecodeMcuSequential(mcu);
            case ArithDecoder.DcFirst: return ArithDecodeMcuDcFirst(mcu);
            case ArithDecoder.AcFirst: return ArithDecodeMcuAcFirst(mcu);
            case ArithDecoder.DcRefine: return ArithDecodeMcuDcRefine(mcu);
            default: return ArithDecodeMcuAcRefine(mcu);
        }
    }

    // get_byte: no suspension (J62)
    private int ArithGetByte()
    {
        if (_avail == 0)
        {
            if (!FillInputBuffer()) throw Fatal("JERR_CANT_SUSPEND");
        }
        _avail--;
        return _buf[_next++];
    }

    // arith_decode (J62, J63): `st` addresses a statistics bin as (array, index)
    private int ArithDecode(byte[] stats, int idx)
    {
        while (_arA < 0x8000)
        {
            if (--_arCt < 0)
            {
                int data;
                if (UnreadMarker != 0)
                {
                    data = 0;
                }
                else
                {
                    data = ArithGetByte();
                    if (data == 0xFF)
                    {
                        do data = ArithGetByte(); while (data == 0xFF);
                        if (data == 0)
                        {
                            data = 0xFF;
                        }
                        else
                        {
                            UnreadMarker = data;
                            data = 0;
                        }
                    }
                }
                _arC = (_arC << 8) | data;
                if ((_arCt += 8) < 0)
                {
                    if (++_arCt == 0) _arA = 0x8000;
                }
            }
            _arA <<= 1;
        }
        int sv = stats[idx];
        uint qeWord = JpegTables.ArithTable[sv & 0x7F];
        int nl = (int)(qeWord & 0xFF);
        int nm = (int)((qeWord >> 8) & 0xFF);
        int qe = (int)(qeWord >> 16);
        int temp = _arA - qe;
        _arA = temp;
        temp <<= _arCt;
        if (_arC >= temp)
        {
            _arC -= temp;
            if (_arA < qe)
            {
                _arA = qe;
                stats[idx] = (byte)((sv & 0x80) ^ nm);
            }
            else
            {
                _arA = qe;
                stats[idx] = (byte)((sv & 0x80) ^ nl);
                sv ^= 0x80;
            }
        }
        else if (_arA < 0x8000)
        {
            if (_arA < qe)
            {
                stats[idx] = (byte)((sv & 0x80) ^ nl);
                sv ^= 0x80;
            }
            else
            {
                stats[idx] = (byte)((sv & 0x80) ^ nm);
            }
        }
        return sv >> 7;
    }

    // process_restart (arithmetic, J67)
    private void ArithProcessRestart()
    {
        if (!ReadRestartMarker()) throw Fatal("JERR_CANT_SUSPEND");
        for (int ci = 0; ci < CompsInScan; ci++)
        {
            var compptr = CurCompInfo[ci]!;
            if (!ProgressiveMode || (Ss == 0 && Ah == 0))
            {
                Array.Clear(_arDcStats[compptr.DcTblNo]!);
                _arLastDcVal[ci] = 0;
                _arDcContext[ci] = 0;
            }
            if (!ProgressiveMode || Ss != 0)
            {
                Array.Clear(_arAcStats[compptr.AcTblNo]!);
            }
        }
        _arC = 0;
        _arA = 0;
        _arCt = -16;
        _arRestartsToGo = RestartInterval;
    }

    private void ArithRestartCheck()
    {
        if (RestartInterval != 0)
        {
            if (_arRestartsToGo == 0) ArithProcessRestart();
            _arRestartsToGo--;
        }
    }

    // the DC difference decoder shared by baseline and DC first (J64); returns false when the magnitude overflowed (ct = -1)
    private bool ArithDecodeDcDiff(int ci, int tbl)
    {
        var dcStats = _arDcStats[tbl]!;
        int st = _arDcContext[ci];
        if (ArithDecode(dcStats, st) == 0)
        {
            _arDcContext[ci] = 0;
            return true;
        }
        int sign = ArithDecode(dcStats, st + 1);
        st += 2;
        st += sign;
        int m;
        if ((m = ArithDecode(dcStats, st)) != 0)
        {
            st = 20;
            while (ArithDecode(dcStats, st) != 0)
            {
                if ((m <<= 1) == 0x8000)
                {
                    Warn("JWRN_ARITH_BAD_CODE");
                    _arCt = -1;
                    return false;
                }
                st += 1;
            }
        }
        if (m < (int)((1L << _arithDcL[tbl]) >> 1)) _arDcContext[ci] = 0;
        else if (m > (int)((1L << _arithDcU[tbl]) >> 1)) _arDcContext[ci] = 12 + (sign * 4);
        else _arDcContext[ci] = 4 + (sign * 4);
        int v = m;
        st += 14;
        while ((m >>= 1) != 0) if (ArithDecode(dcStats, st) != 0) v |= m;
        v += 1;
        if (sign != 0) v = -v;
        _arLastDcVal[ci] += v;
        return true;
    }

    // the AC coefficient decoder for k in [first, last] shared by baseline and AC first; returns false on overflow (ct = -1)
    private bool ArithDecodeAc(JpegBlock block, int tbl, int first, int last, int al)
    {
        var acStats = _arAcStats[tbl]!;
        var order = NaturalOrder;
        for (int k = first; k <= last; k++)
        {
            int st = 3 * (k - 1);
            if (ArithDecode(acStats, st) != 0) break;
            while (ArithDecode(acStats, st + 1) == 0)
            {
                st += 3;
                k++;
                if (k > last)
                {
                    Warn("JWRN_ARITH_BAD_CODE");
                    _arCt = -1;
                    return false;
                }
            }
            int sign = ArithDecode(_arFixedBin, 0);
            st += 2;
            int m;
            if ((m = ArithDecode(acStats, st)) != 0)
            {
                if (ArithDecode(acStats, st) != 0)
                {
                    m <<= 1;
                    st = k <= _arithAcK[tbl] ? 189 : 217;
                    while (ArithDecode(acStats, st) != 0)
                    {
                        if ((m <<= 1) == 0x8000)
                        {
                            Warn("JWRN_ARITH_BAD_CODE");
                            _arCt = -1;
                            return false;
                        }
                        st += 1;
                    }
                }
            }
            int v = m;
            st += 14;
            while ((m >>= 1) != 0) if (ArithDecode(acStats, st) != 0) v |= m;
            v += 1;
            if (sign != 0) v = -v;
            block.A[block.O + order[k]] = (short)(v << al);
        }
        return true;
    }

    // decode_mcu (sequential arithmetic)
    private bool ArithDecodeMcuSequential(JpegBlock[] mcu)
    {
        ArithRestartCheck();
        if (_arCt == -1) return true;
        for (int blkn = 0; blkn < BlocksInMcu; blkn++)
        {
            var block = mcu[blkn];
            int ci = McuMembership[blkn];
            var compptr = CurCompInfo[ci]!;
            if (!ArithDecodeDcDiff(ci, compptr.DcTblNo)) return true;
            block.A[block.O] = (short)_arLastDcVal[ci];
            if (!ArithDecodeAc(block, compptr.AcTblNo, 1, LimSe, 0)) return true;
        }
        return true;
    }

    // decode_mcu_DC_first
    private bool ArithDecodeMcuDcFirst(JpegBlock[] mcu)
    {
        ArithRestartCheck();
        if (_arCt == -1) return true;
        for (int blkn = 0; blkn < BlocksInMcu; blkn++)
        {
            var block = mcu[blkn];
            int ci = McuMembership[blkn];
            int tbl = CurCompInfo[ci]!.DcTblNo;
            if (!ArithDecodeDcDiff(ci, tbl)) return true;
            block.A[block.O] = (short)(_arLastDcVal[ci] << Al);
        }
        return true;
    }

    // decode_mcu_AC_first
    private bool ArithDecodeMcuAcFirst(JpegBlock[] mcu)
    {
        ArithRestartCheck();
        if (_arCt == -1) return true;
        var block = mcu[0];
        int tbl = CurCompInfo[0]!.AcTblNo;
        ArithDecodeAc(block, tbl, Ss, Se, Al);
        return true;
    }

    // decode_mcu_DC_refine
    private bool ArithDecodeMcuDcRefine(JpegBlock[] mcu)
    {
        ArithRestartCheck();
        int p1 = 1 << Al;
        for (int blkn = 0; blkn < BlocksInMcu; blkn++)
        {
            if (ArithDecode(_arFixedBin, 0) != 0) mcu[blkn].A[mcu[blkn].O] |= (short)p1;
        }
        return true;
    }

    // decode_mcu_AC_refine (J66)
    private bool ArithDecodeMcuAcRefine(JpegBlock[] mcu)
    {
        ArithRestartCheck();
        if (_arCt == -1) return true;
        var order = JpegTables.NaturalOrder8;
        var block = mcu[0];
        int tbl = CurCompInfo[0]!.AcTblNo;
        var acStats = _arAcStats[tbl]!;
        int p1 = 1 << Al;
        int m1 = (-1) << Al;
        int kex;
        for (kex = Se; kex > 0; kex--)
            if (block.A[block.O + order[kex]] != 0) break;
        for (int k = Ss; k <= Se; k++)
        {
            int st = 3 * (k - 1);
            if (k > kex)
                if (ArithDecode(acStats, st) != 0) break;
            for (; ; )
            {
                int pos = block.O + order[k];
                if (block.A[pos] != 0)
                {
                    if (ArithDecode(acStats, st + 2) != 0)
                    {
                        if (block.A[pos] < 0) block.A[pos] += (short)m1; else block.A[pos] += (short)p1;
                    }
                    break;
                }
                if (ArithDecode(acStats, st + 1) != 0)
                {
                    if (ArithDecode(_arFixedBin, 0) != 0) block.A[pos] = (short)m1; else block.A[pos] = (short)p1;
                    break;
                }
                st += 3;
                k++;
                if (k > Se)
                {
                    Warn("JWRN_ARITH_BAD_CODE");
                    _arCt = -1;
                    return true;
                }
            }
        }
        return true;
    }
}
