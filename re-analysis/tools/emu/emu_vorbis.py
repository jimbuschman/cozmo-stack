"""Decode a shipped Wwise Vorbis sound with the engine's own decoder, under Unicorn.

It drives libcozmoEngine.so's own functions, the way the Vorbis source init 0x00AB0A30 does:
    0x00AB6380 setup init (block sizes)        0x00AB63E0 setup parse (the setup packet, the codebooks)
    0x00AB3264 decoder-state init (buffers)    0x00AB7E40 frame loop, repeatedly: one PCM block per call
The Wwise allocator (0x00A7A7F4 / 0x00A7A894 / 0x00A7A914 / 0x00A7A988) and the libc/AEABI imports (the PLT
thunks the Ghidra index lists: memcpy/memset/memmove, __aeabi_mem*, the integer divides) are stood in for by
simple Python versions. Any other import stops the run with its name.

The state layout, from the decompilation (a navigation aid) and the instructions:
    F = the frame-loop block; D = F + 0x10, the decoder state
    D+0x00..0x08 bit reader, D+0x0C channels, D+0x10 setup, D+0x14 work[], D+0x18 overlap[],
    D+0x1C start (-1 before the first packet), D+0x20 end, D+0x24/0x28 previous/current block flag,
    D+0x2C u16 start skip, D+0x2E u16 end trim, D+0x30 window-saved flag
    F+0x00 frames out, F+0x04 status, F+0x08 state (4 = end of file), F+0x0C bytes consumed,
    F+0x48 AkChannelConfig (numChannels | type << 8 | mask << 12), F+0x50 data length, F+0x54 ready flag

    python re-analysis/tools/emu/emu_vorbis.py <wem id> [<wem id> ...]    -> vorbis_<id>.wem.f32 (planar blocks)
    python re-analysis/tools/emu/emu_vorbis.py --fixtures <wem id> ...    -> Fixtures/vorbis_native.txt
    (--spectrum / --memo also record the IMDCT inputs / the floor1 post values, for localising a difference)

The fixture holds, per sound, the channels, the float count and the SHA-256 of the engine's output
(binary32, little-endian, each block planar), which WwiseVorbisWholeDecodeNativeTests checks the C# against.
A .f32 already written by this tool in the current directory is reused rather than decoded again.
"""
import os, struct, sys, zipfile
from unicorn import Uc, UC_ARCH_ARM, UC_MODE_ARM, UC_HOOK_CODE, UC_HOOK_MEM_UNMAPPED
from unicorn.arm_const import *
from so import RAW, LOADS

HEAP, HEAP_SIZE = 0x10000000, 0x04000000
STACK, RET = 0x04000000, 0x05000000
ALLOC, ALLOC_ALIGN, FREE_A, FREE_B = 0x00A7A7F4, 0x00A7A894, 0x00A7A914, 0x00A7A988
INDEX = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'decomp', 'libcozmoEngine', 'index.tsv')
SETUP_INIT, SETUP_PARSE, STATE_INIT, FRAME_LOOP = 0x00AB6380, 0x00AB63E0, 0x00AB3264, 0x00AB7E40
ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..')


class Engine:
    def __init__(self):
        uc = Uc(UC_ARCH_ARM, UC_MODE_ARM)
        end = max(s.virtual_address + s.virtual_size for s in LOADS)
        uc.mem_map(0, (end + 0xFFFF) & ~0xFFFF)
        for s in LOADS:
            uc.mem_write(s.virtual_address, RAW[s.file_offset:s.file_offset + s.physical_size])
        uc.mem_map(HEAP, HEAP_SIZE)
        uc.mem_map(STACK, 0x100000)
        uc.mem_map(RET, 0x1000)
        uc.reg_write(UC_ARM_REG_C1_C0_2, uc.reg_read(UC_ARM_REG_C1_C0_2) | (0xF << 20))
        uc.reg_write(UC_ARM_REG_FPEXC, 0x40000000)
        self.uc, self.brk = uc, HEAP
        self.imports = {}
        for line in open(INDEX):
            f = line.rstrip('\n').split('\t')
            if len(f) >= 5 and f[2] == 'THUNK' and f[4].startswith('<EXTERNAL>::'):
                self.imports[int(f[0], 16)] = f[4][12:]
        for a in (ALLOC, ALLOC_ALIGN, FREE_A, FREE_B, *self.imports):
            uc.hook_add(UC_HOOK_CODE, self._stub, begin=a, end=a)
        uc.hook_add(UC_HOOK_MEM_UNMAPPED, self._unmapped)
        self.taps = []                                               # (tag, n, floats) at a tapped address
        self.tap_at = {}

    def tap(self, addr, tag, n_reg, buf_reg):
        """Record n floats at buf_reg each time addr is reached (n in n_reg, or a constant)."""
        def grab(uc, a, size, _):
            n = n_reg if isinstance(n_reg, int) and n_reg > 16 else uc.reg_read(UC_ARM_REG_R0 + n_reg)
            p = uc.reg_read(UC_ARM_REG_R0 + buf_reg)
            self.taps.append((tag, n, bytes(uc.mem_read(p, 4 * n))))
        self.uc.hook_add(UC_HOOK_CODE, grab, begin=addr, end=addr)

    def alloc(self, size, align=16):
        self.brk = (self.brk + align - 1) & ~(align - 1)
        p = self.brk
        self.brk += max(size, 4)
        if self.brk > HEAP + HEAP_SIZE:
            raise MemoryError('emulated heap exhausted')
        self.uc.mem_write(p, b'\0' * max(size, 4))
        return p

    def _ret(self, value=None, value2=None):
        uc = self.uc
        if value is not None:
            uc.reg_write(UC_ARM_REG_R0, value & 0xFFFFFFFF)
        if value2 is not None:
            uc.reg_write(UC_ARM_REG_R1, value2 & 0xFFFFFFFF)
        lr = uc.reg_read(UC_ARM_REG_LR)
        cpsr = uc.reg_read(UC_ARM_REG_CPSR)
        uc.reg_write(UC_ARM_REG_CPSR, (cpsr | 0x20) if lr & 1 else (cpsr & ~0x20))
        uc.reg_write(UC_ARM_REG_PC, lr & ~1)

    def _stub(self, uc, addr, size, _):
        r = [uc.reg_read(getattr(__import__('unicorn.arm_const', fromlist=['x']), f'UC_ARM_REG_R{i}')) for i in range(4)]
        if addr == ALLOC:
            self._ret(self.alloc(r[1]))
        elif addr == ALLOC_ALIGN:
            self._ret(self.alloc(r[1], max(r[2], 4)))
        elif addr in (FREE_A, FREE_B):
            self._ret(0)
        else:
            self._import(uc, self.imports[addr], r)

    def _import(self, uc, name, r):
        s32 = lambda v: v - (1 << 32) if v & 0x80000000 else v
        if name in ('memcpy', 'memmove', '__aeabi_memcpy', '__aeabi_memcpy4', '__aeabi_memcpy8',
                    '__aeabi_memmove', '__aeabi_memmove4'):
            if r[2]:
                uc.mem_write(r[0], bytes(uc.mem_read(r[1], r[2])))
            self._ret(r[0])
        elif name == 'memset':                                        # (dest, c, n)
            if r[2]:
                uc.mem_write(r[0], bytes([r[1] & 0xFF]) * r[2])
            self._ret(r[0])
        elif name in ('__aeabi_memset', '__aeabi_memset4', '__aeabi_memset8'):   # (dest, n, c)
            if r[1]:
                uc.mem_write(r[0], bytes([r[2] & 0xFF]) * r[1])
            self._ret()
        elif name in ('__aeabi_memclr', '__aeabi_memclr4', '__aeabi_memclr8'):   # (dest, n)
            if r[1]:
                uc.mem_write(r[0], b'\0' * r[1])
            self._ret()
        elif name in ('__aeabi_idiv', '__aeabi_idivmod'):
            a, b = s32(r[0]), s32(r[1])
            q = abs(a) // abs(b) * (1 if (a < 0) == (b < 0) else -1)
            self._ret(q, a - q * b)
        elif name in ('__aeabi_uidiv', '__aeabi_uidivmod'):
            self._ret(r[0] // r[1], r[0] % r[1])
        else:
            raise RuntimeError(f'unstubbed import {name}, called from {uc.reg_read(UC_ARM_REG_LR):#x}')

    def _unmapped(self, uc, access, addr, size, value, _):
        print(f'unmapped access {access} at {addr:#x}, pc {uc.reg_read(UC_ARM_REG_PC):#x}, lr {uc.reg_read(UC_ARM_REG_LR):#x}')
        return False

    def call(self, fn, *args):
        uc = self.uc
        sp = STACK + 0xF0000
        regs = list(args[:4]) + [0] * (4 - min(4, len(args)))
        extra = list(args[4:])
        if extra:
            sp -= 4 * len(extra)
            uc.mem_write(sp, struct.pack(f'<{len(extra)}I', *[x & 0xFFFFFFFF for x in extra]))
        for i, v in enumerate(regs):
            uc.reg_write(getattr(__import__('unicorn.arm_const', fromlist=['x']), f'UC_ARM_REG_R{i}'), v & 0xFFFFFFFF)
        uc.reg_write(UC_ARM_REG_SP, sp)
        uc.reg_write(UC_ARM_REG_LR, RET)
        uc.reg_write(UC_ARM_REG_CPSR, uc.reg_read(UC_ARM_REG_CPSR) & ~0x20)
        uc.emu_start(fn, RET, count=500_000_000)
        return uc.reg_read(UC_ARM_REG_R0)

    def u32(self, a):
        return struct.unpack('<I', self.uc.mem_read(a, 4))[0]

    def w32(self, a, v):
        self.uc.mem_write(a, struct.pack('<I', v & 0xFFFFFFFF))


def read_wem(name):
    z = zipfile.ZipFile(os.path.join(ROOT, 're-analysis', 'obb', 'assets', 'cozmo_resources', 'sound', 'AudioAssets.zip'))
    b = z.read(next(n for n in z.namelist() if n.endswith('/' + name) or n == name))
    off, fmt, data = 12, None, None
    while off + 8 <= len(b):
        tag, size = b[off:off + 4], struct.unpack_from('<I', b, off + 4)[0]
        if tag == b'fmt ':
            fmt = b[off + 8:off + 8 + size]
        elif tag == b'data':
            data = b[off + 8:off + 8 + size]
        off += 8 + size + (size & 1)
    channels = struct.unpack_from('<H', fmt, 2)[0]
    mask = struct.unpack_from('<I', fmt, 0x14)[0] if len(fmt) >= 0x18 else 0
    v = fmt[0x18:0x18 + 0x2A]
    info = dict(channels=channels, mask=mask,
                samples=struct.unpack_from('<I', v, 0x00)[0],
                setup_off=struct.unpack_from('<I', v, 0x10)[0], audio_off=struct.unpack_from('<I', v, 0x14)[0],
                max_packet=struct.unpack_from('<H', v, 0x18)[0], last_granule_extra=struct.unpack_from('<H', v, 0x1A)[0],
                alloc=struct.unpack_from('<I', v, 0x1C)[0], bs0=v[0x28], bs1=v[0x29])
    return info, data


def decode(name):
    info, data = read_wem(name)
    e = Engine()
    if '--spectrum' in sys.argv:
        e.tap(0x00AB4E34, 'imdct_in', 0, 1)                           # mdct_backward(n, in)
    if '--memo' in sys.argv:
        def memo(uc, a, size, _):                                     # floor1 inverse2(dsp, look, memo, out)
            look, m = uc.reg_read(UC_ARM_REG_R1), uc.reg_read(UC_ARM_REG_R2)
            posts = e.u32(look + 0x1C)
            e.taps.append(('memo', posts, bytes(uc.mem_read(m, 4 * posts)) if m else b'\0' * 4 * posts))
        e.uc.hook_add(UC_HOOK_CODE, memo, begin=0x00AB915C, end=0x00AB915C)
    ch = info['channels']

    # setup: 0x00AB6380 then 0x00AB63E0, with the bump-allocator block 0x00AB2D74 hands it
    size = max(info['alloc'], 0x80000)
    base = e.alloc(size)
    assert e.call(SETUP_INIT, base, info['bs0'], info['bs1']) == 0, 'setup init'
    alloc = e.alloc(16)
    for i, v in enumerate((base, base + 0x30, 0x30, size)):
        e.w32(alloc + 4 * i, v)
    so = info['setup_off']
    ssize = struct.unpack_from('<H', data, so)[0]
    sp = e.alloc(ssize + 16)
    e.uc.mem_write(sp, data[so + 2:so + 2 + ssize])
    reader = e.alloc(16)
    for i, v in enumerate((sp, 0, ssize)):
        e.w32(reader + 4 * i, v)
    rc = e.call(SETUP_PARSE, base, ch, reader, alloc)
    assert rc == 0, f'setup parse returned {rc:#x}'

    # decoder state: F, D = F + 0x10
    F = e.alloc(0x100)
    D = F + 0x10
    e.w32(D + 0x10, base)
    assert e.call(STATE_INIT, D, ch) == 0, 'state init'
    e.w32(D + 0x1C, 0xFFFFFFFF)                                   # start = -1: before the first packet
    e.uc.mem_write(D + 0x2C, struct.pack('<HH', 0, info['last_granule_extra']))   # skip 0, trim (one play)
    e.w32(F + 0x48, ch | (info['mask'] << 12))
    e.uc.mem_write(F + 0x54, b'\x01')                            # the source's ready flag

    audio = data[info['audio_off']:]
    buf = e.alloc(len(audio) + 16)
    e.uc.mem_write(buf, audio)
    outp = e.alloc(4)
    pcm = []
    offset = 0
    while offset + 2 <= len(audio):
        e.w32(F + 0x50, len(audio) - offset)
        e.call(FRAME_LOOP, F, 0xFFFF, buf + offset, outp)
        frames, status, consumed = e.u32(F), e.u32(F + 4), e.u32(F + 0xC)
        if frames:
            p = e.u32(outp)
            pcm.append(bytes(e.uc.mem_read(p, 4 * ch * frames)))
        if consumed == 0 or status == 0x11:
            break
        offset += consumed
        if e.u32(F + 8) == 4:
            break
    out = b''.join(pcm)
    path = f'vorbis_{name}.f32'
    open(path, 'wb').write(out)
    if e.taps:
        with open(f'vorbis_{name}.' + ('memo' if '--memo' in sys.argv else 'taps'), 'wb') as f:
            for tag, n, b in e.taps:
                f.write(struct.pack('<I', n) + b)
    print(f'{name}: {ch} ch, blocks {1 << info["bs0"]}/{1 << info["bs1"]}, {len(out) // 4} floats -> {path}')
    return ch, out


def write_fixture(names):
    import hashlib
    path = os.path.join(ROOT, 'cozmo-stack', 'tests', 'Cozmo.Protocol.Tests', 'Fixtures', 'vorbis_native.txt')
    lines = ['# wem id, channels, floats, SHA-256 of the engine decode (re-analysis/tools/emu/emu_vorbis.py)']
    for name in names:
        cached = f'vorbis_{name}.f32'
        if os.path.exists(cached):
            ch, out = read_wem(name)[0]['channels'], open(cached, 'rb').read()
        else:
            ch, out = decode(name)
        lines.append(f'{name[:-4]} {ch} {len(out) // 4} {hashlib.sha256(out).hexdigest()}')
    open(path, 'w', newline='\n').write('\n'.join(lines) + '\n')
    print('wrote', os.path.normpath(path), len(names), 'sounds')


if __name__ == '__main__':
    names = [n if n.endswith('.wem') else n + '.wem' for n in sys.argv[1:] if not n.startswith('--')]
    if '--fixtures' in sys.argv:
        write_fixture(names)
    else:
        for n in names:
            decode(n)
