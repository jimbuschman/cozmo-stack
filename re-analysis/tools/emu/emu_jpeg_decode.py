"""Oracle for the camera image decode (M3-001 / M3-018 decode path): the shipped EncodedImage::DecodeImageGray / DecodeImageRGB
run under Unicorn together with the shipped libopencv_imgcodecs / imgproc / core and libc++_shared (emu_elf.py).

    from emu_jpeg_decode import Oracle
    o = Oracle()                                   # poison=0x00, NEON on (HWCAP_NEON in the served /proc/self/auxv)
    r = o.decode(8, payload, gray=True)            # -> dict(result, rows, cols, channels, step, data, logs, exception)

`payload` is the reassembled chunk payload exactly as EncodedImage stores it (for 8/9 the mini payload with its flag byte; for
5/6/7 a complete JPEG file); the object is built with the engine's own constructor and the fields the engine's AddChunk writes
(+0x14 = 320, +0x18 = 240, +0x20 = encoding, the data vector at +0).

Fixture boundaries (see emu_elf.py): heap/stack poison, the parallel_for_ shim, OpenCL off, the libc/libm stand-ins, the
auxv file that decides whether cv::checkHardwareSupport(NEON) is true. Every sWarningF/sErrorF/... the engine raises on the
path is recorded (event name and the rendered format) in `logs`.

Run as a script:  python re-analysis/tools/emu/emu_jpeg_decode.py payload.bin --encoding 8 [--rgb] [--out mat.raw]
(see main() for the options); the corpus generator is gen_jpeg_decode_fixtures.py.
"""
import os
import struct
import sys
import lief

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from emu_elf import Machine, ORDER, Abort, CxxThrow, NORET, M32
from unicorn.arm_const import UC_ARM_REG_PC, UC_ARM_REG_LR

# The libraries the oracle runs, pinned by SHA-256 (the .so files under resources/lib/armeabi-v7a/, local only). A different build refuses to run.
PINNED_SHA256 = {
    'libcozmoEngine.so': '02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1',
    'libopencv_core.so': '8b8c9fcdff9028e5c1b58578d6c20040bebfc2f16c2d2d4bf885f79da56cba8d',
    'libopencv_imgproc.so': '3c4e3ff7e639c61e21c5ef91ea2cc4125bb9bd259ff53f5c0b1eeb9a95054830',
    'libopencv_imgcodecs.so': '4bdf5a45023c7938924f6c0018bf20774ebb5f7c0b2a637c93fe9e61b1f87218',
    'libc++_shared.so': '8ac5090bbd0be7401af5fff6044ded6f1e6ced692fcd3581fa3cb937b519a16a',
}


def verify_library_hashes():
    """Asserts every library the oracle loads is the pinned build; returns {name: sha256}."""
    import hashlib
    from emu_elf import LIBDIR
    out = {}
    for name, want in PINNED_SHA256.items():
        got = hashlib.sha256(open(os.path.join(LIBDIR, name), 'rb').read()).hexdigest()
        if got != want:
            raise SystemExit('%s is not the pinned build: sha256 %s, expected %s' % (name, got, want))
        out[name] = got
    return out


ENC_NAMES = {1: 'RawGray', 2: 'RawRGB', 5: 'JPEGGray', 6: 'JPEGColor', 7: 'JPEGColorHalfWidth', 8: 'JPEGMinimizedGray', 9: 'JPEGMinimizedColor'}

AUXV_NEON = struct.pack('<IIII', 16, 0x1000 | 0x80 | 0x40 | 0x8, 0, 0)       # AT_HWCAP: NEON | VFPv3 | VFP | EDSP
AUXV_NONE = struct.pack('<IIII', 16, 0x80 | 0x40 | 0x8, 0, 0)


class Oracle:
    def __init__(self, poison=0x00, neon=True):
        verify_library_hashes()
        self._ready = False
        self.m = m = Machine(poison=poison)
        self.neon = neon
        self.files = {}
        self.events = []
        self._install()
        m.relocate()
        self._ready = False
        self._run_inits()
        self._warm_up()
        m.mark_heap_base()
        self._ready = True
        self.base = m.libs['libcozmoEngine.so'].base

    # -- shims specific to this oracle
    def _install(self):
        m = self.m
        auxv = AUXV_NEON if self.neon else AUXV_NONE
        fds = {}

        def open_(mm):
            path = mm.cstr(mm.reg(0)).decode('latin1')
            if path == '/proc/self/auxv':
                fd = 100
                fds[fd] = [auxv, 0]
                return fd
            return 0xFFFFFFFF

        def read_(mm):
            fd, buf, n = mm.reg(0), mm.reg(1), mm.reg(2)
            if fd not in fds:
                return 0xFFFFFFFF
            data, pos = fds[fd]
            chunk = data[pos:pos + n]
            fds[fd][1] += len(chunk)
            mm.wr(buf, chunk)
            return len(chunk)
        m.libc['open'] = open_
        m.libc['read'] = read_
        m.libc['close'] = lambda mm: 0

        def parallel_for(mm):
            # cv::parallel_for_(const Range&, const ParallelLoopBody&, double): one call over the whole range, tail-called
            rng, body = mm.reg(0), mm.reg(1)
            vt = mm.r32(body)
            fn = mm.r32(vt + 8)
            mm.setreg(0, body)
            mm.setreg(1, rng)
            mm.uc.reg_write(UC_ARM_REG_PC, fn)          # returns to parallel_for_'s caller
            return NORET
        m.overrides['_ZN2cv13parallel_for_ERKNS_5RangeERKNS_16ParallelLoopBodyEd'] = parallel_for
        def logger(kind):
            def h(mm):
                name = mm.cstr(mm.reg(0)).decode('latin1')
                text = mm.fmt(mm.cstr(mm.reg(2)), 3)
                mm.log.append((kind, name, text))
                return 0
            return h
        sig = 'PKcRKNSt6__ndk16vectorINS3_4pairIS2_S2_EENS3_9allocatorIS6_EEEES2_z'
        m.overrides['_ZN4Anki4Util9sWarningF' + 'E' + sig] = logger('warning')
        m.overrides['_ZN4Anki4Util7sErrorF' + 'E' + sig] = logger('error')
        m.overrides['_ZN2cv3ocl9useOpenCLEv'] = lambda mm: 0
        m.overrides['_ZN2cv3ocl10haveOpenCLEv'] = lambda mm: 0
        m.overrides['_ZN2cv14setNumThreadsEi'] = lambda mm: 0

    def _warm_up(self):
        """One decode of every kind, so the lazily created library statics (thread data, allocators) exist before the heap snapshot."""
        warm = bytes.fromhex('ffd8ffe000104a46494600010100000100010000ffdb004300100b0c0e0c0a100e0d0e1211101318281a181616183123251d283a333d3c3933383740485c4e404457453738506d51575f626768673e4d71797064785c656763ffdb0043011112121815182f1a1a2f634238426363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363ffc00011080010001003012100021101031101ffc4001f0000010501010101010100000000000000000102030405060708090a0bffc400b5100002010303020403050504040000017d01020300041105122131410613516107227114328191a1082342b1c11552d1f02433627282090a161718191a25262728292a3435363738393a434445464748494a535455565758595a636465666768696a737475767778797a838485868788898a92939495969798999aa2a3a4a5a6a7a8a9aab2b3b4b5b6b7b8b9bac2c3c4c5c6c7c8c9cad2d3d4d5d6d7d8d9dae1e2e3e4e5e6e7e8e9eaf1f2f3f4f5f6f7f8f9faffc4001f0100030101010101010101010000000000000102030405060708090a0bffc400b51100020102040403040705040400010277000102031104052131061241510761711322328108144291a1b1c109233352f0156272d10a162434e125f11718191a262728292a35363738393a434445464748494a535455565758595a636465666768696a737475767778797a82838485868788898a92939495969798999aa2a3a4a5a6a7a8a9aab2b3b4b5b6b7b8b9bac2c3c4c5c6c7c8c9cad2d3d4d5d6d7d8d9dae2e3e4e5e6e7e8e9eaf2f3f4f5f6f7f8f9faffda000c03010002110311003f00a14549f44145007fffd9')
        self.imdecode(warm, 0)
        self.imdecode(warm, 1)
        self.resize(2, 2, 1, bytes(4), 4, 2)
        self.resize(2, 2, 3, bytes(12), 4, 2)
        self.cvtcolor(2, 2, 3, bytes(12), 4)
        self.decode(8, bytes(20), True)
        self.decode(9, bytes(20), False)

    def _run_inits(self):
        m = self.m
        for n in ('libopencv_core.so', 'libopencv_imgproc.so', 'libopencv_imgcodecs.so'):
            lib = m.libs[n]
            for f in lib.elf.get(lief.ELF.DynamicEntry.TAG.INIT_ARRAY).array:
                if f in (0, 0xFFFFFFFF):
                    continue
                m.call(lib.base + f)

    # -- helpers
    def eng(self, name):
        return self.m.sym(name)

    def imdecode(self, jpeg, flags, count=2_000_000_000):
        """cv::imdecode(_InputArray(vector<uchar>), flags, Mat*) exactly as the engine calls it (0x004F28C6 / 0x004F21D6)."""
        m = self.m
        if self._ready:
            m.reset_heap()
        m.poison_stack()
        m.log.clear()
        res = {'flags': flags, 'exception': None}
        try:
            buf = m.malloc(max(len(jpeg), 1))
            m.wr(buf, jpeg)
            vec = m.malloc(16)
            m.wr(vec, struct.pack('<III', buf, buf + len(jpeg), buf + len(jpeg)))
            ia = m.malloc(16)
            m.wr(ia, struct.pack('<IIII', 0x81030000, vec, 0, 0))
            mat = m.malloc(0x40)
            m.wr(mat, bytes(0x40))
            m.call(self.eng('_ZN2cv3MatC1Ev'), mat)
            ret = m.malloc(0x40)
            m.wr(ret, bytes(0x40))      # the returned Mat (sret, r0) the engine passes at sp+0x50; its header is a copy of *mat
            m.call_sret(self.eng('_ZN2cv8imdecodeERKNS_11_InputArrayEiPNS_3MatE'), ret, ia, flags, mat, count=count)
            res.update(self.read_mat(mat))
        except CxxThrow as e:
            res['exception'] = f'cxx_throw {e.typename}'
        except Abort as e:
            res['exception'] = f'abort {e}'
        res['logs'] = list(m.log)
        return res

    # ---- direct entries of the shipped imgproc functions the engine calls after the decode (J36, J37, J39, J40, J41..J45)
    def make_mat(self, rows, cols, channels, data):
        """A cv::Mat over a copy of `data` (CV_8UC1 / CV_8UC3), built by the shipped constructor Mat(rows, cols, type, data, step)."""
        m = self.m
        buf = m.malloc(max(len(data), 1))
        m.wr(buf, data)
        mat = m.malloc(0x40)
        m.wr(mat, bytes(0x40))
        typ = 0 if channels == 1 else 16
        # Mat::Mat(int rows, int cols, int type, void* data, size_t step = AUTO_STEP=0): rows, cols in r1, r2; data, step on the stack
        m.call(self.eng('_ZN2cv3MatC1EiiiPvj'), mat, rows, cols, typ, stack=(buf, 0))
        return mat

    def array_in(self, mat):
        ia = self.m.malloc(16)
        self.m.wr(ia, struct.pack('<IIII', 0x01010000, mat, 0, 0))
        return ia

    def array_out(self, mat):
        oa = self.m.malloc(16)
        self.m.wr(oa, struct.pack('<IIII', 0x02010000, mat, 0, 0))
        return oa

    def _run_mat_op(self, fn):
        m = self.m
        if self._ready:
            m.reset_heap()
        m.poison_stack()
        m.log.clear()
        res = {'exception': None}
        try:
            dst = m.malloc(0x40)
            m.wr(dst, bytes(0x40))
            m.call(self.eng('_ZN2cv3MatC1Ev'), dst)
            fn(dst)
            res.update(self.read_mat(dst))
        except CxxThrow as e:
            res['exception'] = f'cxx_throw {e.typename}'
        except Abort as e:
            res['exception'] = f'abort {e}'
        res['logs'] = list(m.log)
        return res

    def resize(self, rows, cols, channels, data, dst_cols, dst_rows, interpolation=1):
        """cv::resize(src, dst, Size(dst_cols, dst_rows), 0, 0, interpolation)."""
        def op(dst):
            m = self.m
            src = self.make_mat(rows, cols, channels, data)
            ia, oa = self.array_in(src), self.array_out(dst)
            # r0 = in, r1 = out, r2 = pointer to Size (cv::Size_<int> has a copy constructor: passed by reference); fx, fy
            # (doubles, 8-aligned) and the interpolation are on the stack
            size = m.malloc(16)
            m.wr(size, struct.pack('<ii', dst_cols, dst_rows))
            m.call(self.eng('_ZN2cv6resizeERKNS_11_InputArrayERKNS_12_OutputArrayENS_5Size_IiEEddi'), ia, oa, size, 0,
                   stack=(0, 0, 0, 0, interpolation), count=2_000_000_000)
        return self._run_mat_op(op)

    def cvtcolor(self, rows, cols, channels, data, code, dcn=0):
        def op(dst):
            m = self.m
            src = self.make_mat(rows, cols, channels, data)
            ia, oa = self.array_in(src), self.array_out(dst)
            m.call(self.eng('_ZN2cv8cvtColorERKNS_11_InputArrayERKNS_12_OutputArrayEii'), ia, oa, code, dcn)
        return self._run_mat_op(op)

    # ---- the scaled inverse DCTs of libopencv_imgcodecs (J17..J22, J79..J102), called directly
    def idct_functions(self):
        """{(w, h): absolute address} of the 32 integer inverse DCT bodies, read back from jddctmgr's start_pass (0x2A608) which stores
        `inverse_DCT[ci]` for the component's DCT_h_scaled_size x DCT_v_scaled_size."""
        if getattr(self, '_idct_fns', None):
            return self._idct_fns
        m = self.m
        base = m.libs['libopencv_imgcodecs.so'].base
        fns = {}
        for h in range(1, 17):
            for v in range(1, 17):
                if not (h == v or h == 2 * v or v == 2 * h):
                    continue
                cinfo, comp, idct, q, dct = (m.malloc(0x400), m.malloc(0x58), m.malloc(0x100), m.malloc(0x100), m.malloc(0x400))
                for a, n in ((cinfo, 0x400), (comp, 0x58), (idct, 0x100), (q, 0x100), (dct, 0x400)):
                    m.wr(a, bytes(n))
                m.w32(cinfo + 0x24, 1)
                m.w32(cinfo + 0xd8, comp)
                m.w32(cinfo + 0x1d8, idct)
                m.w32(comp + 0x24, h)
                m.w32(comp + 0x28, v)
                m.w32(comp + 0x34, 1)
                m.w32(comp + 0x50, q)
                m.w32(comp + 0x54, dct)
                m.call(base + 0x2A609, cinfo)
                fns[(h, v)] = m.r32(idct + 4)
        self._idct_fns = fns
        return fns

    def idct(self, w, h, coef, quant):
        """Run the shipped jpeg_idct_<w>x<h> on one coefficient block (64 signed 16-bit values, natural order) with the multiplier
        table `quant` (64 ints) and return h rows of w samples. The range-limit table is the 1408-byte one of prepare_range_limit_table."""
        m = self.m
        fn = self.idct_functions()[(w, h)]
        if self._ready:
            m.reset_heap()
        m.poison_stack()
        cinfo, comp = m.malloc(0x400), m.malloc(0x58)
        m.wr(cinfo, bytes(0x400))
        m.wr(comp, bytes(0x58))
        table = bytearray(1408)
        for i in range(256):
            table[256 + i] = i
        for i in range(512, 512 + 384):
            table[i] = 255
        for i in range(128):
            table[1280 + i] = i
        tab = m.malloc(1408)
        m.wr(tab, bytes(table))
        m.w32(cinfo + 0x150, tab + 256)
        dct = m.malloc(256)
        m.wr(dct, struct.pack('<64i', *[((q + 2**31) % 2**32) - 2**31 for q in quant]))
        m.w32(comp + 0x54, dct)
        blk = m.malloc(128)
        m.wr(blk, struct.pack('<64h', *coef))
        rows = m.malloc(4 * 16)
        outs = []
        for r in range(h):
            row = m.malloc(32)
            m.wr(row, bytes([0x5A]) * 32)
            m.w32(rows + 4 * r, row)
            outs.append(row)
        try:
            m.call(fn, cinfo, comp, blk, rows, stack=(0,))
        except (Abort, CxxThrow) as e:
            return {'exception': str(e)}
        return {'rows': [list(m.rd(a, w)) for a in outs]}

    def read_mat(self, mat):
        m = self.m
        flags, dims, rws, cls, data, stepp = (m.r32(mat), m.r32(mat + 4), m.r32(mat + 8), m.r32(mat + 0xC), m.r32(mat + 0x10), m.r32(mat + 0x2C))
        ch = ((flags >> 3) & 0x1FF) + 1
        step = m.r32(stepp) if stepp else 0
        out = {'rows': rws, 'cols': cls, 'channels': ch, 'step': step, 'mflags': flags}
        out['data'] = b''.join(m.rd(data + y * step, cls * ch) for y in range(rws)) if data and rws and cls else b''
        return out

    def decode(self, encoding, payload, gray, rows=240, cols=320, count=2_000_000_000, extra_logs=True):
        m = self.m
        if self._ready:
            m.reset_heap()
        m.poison_stack()
        m.log.clear()
        res = {'encoding': encoding, 'gray': gray, 'exception': None, 'logs': []}
        try:
            enc = m.malloc(0x40)
            m.wr(enc, bytes(0x40))
            m.call(self.eng('_ZN4Anki5Cozmo12EncodedImageC1Ev'), enc)
            buf = m.malloc(max(len(payload), 1))
            m.wr(buf, payload)
            m.w32(enc + 0, buf)
            m.w32(enc + 4, buf + len(payload))
            m.w32(enc + 8, buf + len(payload))
            m.w32(enc + 0xC, 0x01020304)           # timestamp the engine copies to the image (+0x3C)
            m.w32(enc + 0x14, cols)
            m.w32(enc + 0x18, rows)
            m.w8(enc + 0x20, encoding)
            img = m.malloc(0x100)
            m.wr(img, bytes(0x100))
            errg = self.eng('_ZN4Anki4Util5_errGE')
            m.w8(errg, 0)                      # the process-global error flag the sErrorF paths set (0x0105DD34)
            if gray:
                m.call(self.eng('_ZN4Anki6Vision5ImageC1Ev'), img)
                fn = self.eng('_ZNK4Anki5Cozmo12EncodedImage15DecodeImageGrayERNS_6Vision5ImageE')
            else:
                m.call(self.eng('_ZN4Anki6Vision8ImageRGBC1Ev'), img)
                fn = self.eng('_ZNK4Anki5Cozmo12EncodedImage14DecodeImageRGBERNS_6Vision8ImageRGBE')
            res['result'] = m.call(fn, enc, img, count=count)
            res['err_flag'] = m.r8(errg)
            flags, rws, cls, data, stepp = m.r32(img + 4), m.r32(img + 0xC), m.r32(img + 0x10), m.r32(img + 0x14), m.r32(img + 0x30)
            ch = ((flags >> 3) & 0x1FF) + 1
            step = m.r32(stepp) if stepp else 0
            res.update(rows=rws, cols=cls, channels=ch, step=step, flags=flags, timestamp=m.r32(img + 0x3C))
            if data and rws and cls:
                res['data'] = b''.join(m.rd(data + y * step, cls * ch) for y in range(rws))
            else:
                res['data'] = b''
        except CxxThrow as e:
            res['exception'] = f'cxx_throw {e.typename}'
        except Abort as e:
            res['exception'] = f'abort {e}'
        res['logs'] = list(m.log)
        return res


def main():
    import argparse
    ap = argparse.ArgumentParser(description='Run the shipped camera decode on a file and write the resulting Mat bytes')
    ap.add_argument('file', help='the reassembled payload (encodings 8 and 9: flag byte then entropy data) or a JPEG file (5, 6, 7)')
    ap.add_argument('--encoding', type=int, default=8, help='ImageEncoding value (default 8, JPEGMinimizedGray)')
    ap.add_argument('--rgb', action='store_true', help='run DecodeImageRGB instead of DecodeImageGray')
    ap.add_argument('--imdecode', type=int, default=None, metavar='FLAGS', help='run only cv::imdecode(buf, FLAGS) on the file (no engine dispatch)')
    ap.add_argument('--poison', type=lambda x: int(x, 0), default=0)
    ap.add_argument('--no-neon', action='store_true')
    ap.add_argument('--out', help='write the Mat bytes (rows of cols*channels, no padding) here')
    args = ap.parse_args()
    o = Oracle(poison=args.poison, neon=not args.no_neon)
    data = open(args.file, 'rb').read()
    if args.imdecode is not None:
        r = o.imdecode(data, args.imdecode)
    else:
        r = o.decode(args.encoding, data, gray=not args.rgb)
    print({k: (v if k != 'data' else '%d bytes' % len(v)) for k, v in r.items()})
    if args.out and r.get('data'):
        open(args.out, 'wb').write(r['data'])


if __name__ == '__main__':
    main()
