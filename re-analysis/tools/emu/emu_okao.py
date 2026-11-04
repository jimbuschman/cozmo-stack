"""Oracle for OKAO face detection as the engine sets it up (M14-010 / M11-016 boundary): the Omron OKAO library statically
linked in libcozmoEngine.so, run under Unicorn (loader: emu_elf.py) with Anki's FaceTracker::Impl::Init settings replayed as
direct OKAO calls (research/20261103-okao-acapela-probes.md, OKAO section):

    CO_CreateHandle (0x0086B598, malloc-backed: Set[BW]MemoryArea is never called)
    DT_CreateHandle(co, 5, 10)                       video mode (0x0086B96E; vision_config.json DetectionMode "video")
    DT_MV_SetDelayCount(dt, 1)                       0x0086B984
    DT_MV_SetSearchCycle(dt, 2, 2, 5)                0x0086BC40..48
    DT_MV_SetDirectionMask(dt, 0)                    0x0086BD0A..0E
    DT_MV_SetPoseExtension(dt, 1, 1)                 0x0086BEA2..A8
    DT_MV_SetAccuracy(dt, 0)                         0x0086BF6A..6E
    DT_SetAngle(dt, 1, 0x803803)                     0x0086B77E..88
    DT_SetSizeRange(dt, 48, 640)                     0x0086B7F0/0x0086B7F4
    DT_SetThreshold(dt, 500)                         0x0086B9F2
    DT_CreateResultHandle(co)
    per frame: DT_Detect_GRAY(dt, data, cols, rows, 0, result) (0x0086D75E..0x0086D776), GetResultCount, GetRawResultInfo(i)

The order of the MV/angle/size/threshold calls here is the probe's; Init's own call order is the engine's FaceTracker::Impl ctor
(0x0086B2A8, decompiler only) and is not claimed by this oracle. OKAO itself calls only malloc/free, mem* and the libgcc
division helpers on this path: no libm, time, thread or licence check.

    python -I re-analysis/tools/emu/emu_okao.py            # self-check on generated faces
"""
import os
import struct
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import emu_elf
import emu_engine_objects

SETUP = (
    ('OKAO_DT_MV_SetDelayCount', (1,)), ('OKAO_DT_MV_SetSearchCycle', (2, 2, 5)), ('OKAO_DT_MV_SetDirectionMask', (0,)),
    ('OKAO_DT_MV_SetPoseExtension', (1, 1)), ('OKAO_DT_MV_SetAccuracy', (0,)), ('OKAO_DT_SetAngle', (1, 0x803803)),
    ('OKAO_DT_SetSizeRange', (48, 640)), ('OKAO_DT_SetThreshold', (500,)),
)
RAW_WORDS = 16  # GetRawResultInfo fills at least 0x40 bytes; the engine reads +0x0C..+0x2C (0x0086D96A..0x0086D998)


def s32(v):
    return v - (1 << 32) if v & 0x80000000 else v


class Detector:
    def __init__(self, poison=0x00):
        emu_engine_objects.library_hashes()
        self.m = emu_elf.Machine(libs=['libc++_shared.so', 'libcozmoEngine.so'], poison=poison)
        self.m.relocate()
        e = self.m.libs['libcozmoEngine.so']
        self.S = lambda n: e.syms[n]
        self.co = self.m.call(self.S('OKAO_CO_CreateHandle'))
        self.dt = self.m.call(self.S('OKAO_DT_CreateHandle'), self.co, 5, 10)
        for name, args in SETUP:
            r = s32(self.m.call(self.S(name), self.dt, *args))
            if r != 0:
                raise RuntimeError('%s returned %d' % (name, r))
        self.res = self.m.call(self.S('OKAO_DT_CreateResultHandle'), self.co)
        self.buf = None

    def detect(self, pix, w=320, h=240):
        if self.buf is None:
            self.buf = self.m.malloc(w * h)
            self.cnt = self.m.malloc(16)
            self.info = self.m.malloc(4 * RAW_WORDS)
        self.m.uc.mem_write(self.buf, bytes(pix))
        r = s32(self.m.call(self.S('OKAO_DT_Detect_GRAY'), self.dt, self.buf, w, h, stack=(0, self.res)))
        self.m.uc.mem_write(self.cnt, bytes(16))
        rc = s32(self.m.call(self.S('OKAO_DT_GetResultCount'), self.res, self.cnt))
        n = struct.unpack('<I', bytes(self.m.uc.mem_read(self.cnt, 4)))[0]
        faces = []
        for i in range(n):
            self.m.uc.mem_write(self.info, bytes(4 * RAW_WORDS))
            ri = s32(self.m.call(self.S('OKAO_DT_GetRawResultInfo'), self.res, i, self.info))
            words = [s32(x) for x in struct.unpack('<%dI' % RAW_WORDS, bytes(self.m.uc.mem_read(self.info, 4 * RAW_WORDS)))]
            faces.append({'ret': ri, 'words': words})
        return {'ret': r, 'countRet': rc, 'faces': faces}


if __name__ == '__main__':
    import gen_okao_fixtures
    seq = gen_okao_fixtures.sequence()
    d = Detector()
    for name, pix in seq:
        o = d.detect(pix)
        print(name, o['ret'], len(o['faces']), [f['words'][3:8] for f in o['faces']])
