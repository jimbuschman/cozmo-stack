"""Bounded research fixtures; executes shipped map and loader code with emu_elf.

No pthread replacement: a missing thread import is a STOP, not a serial oracle.
Stack/heap poison and caller objects are fixtures, not recovered phone state.
"""
from pathlib import Path
import sys, struct, math, json
ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 're-analysis/research/_m5013_dependencies'))
sys.path.insert(0, str(ROOT / 're-analysis/tools/emu'))
from emu_elf import Machine, Abort, BASES
from unicorn.arm_const import UC_ARM_REG_PC, UC_ARM_REG_LR, UC_ARM_REG_R0
from unicorn import UC_HOOK_CODE
B = BASES['libcozmoEngine.so']

def machine(poison):
    m = Machine(libs=['libc++_shared.so', 'libcozmoEngine.so'], poison=poison)
    # Bionic ceilf, soft-float ABI. Only integral loads 1..4 are used here.
    def ceilf(q):
        x = struct.unpack('<f', struct.pack('<I', q.uc.reg_read(UC_ARM_REG_R0)))[0]
        return struct.unpack('<I', struct.pack('<f', float(math.ceil(x))))[0]
    m.libc['ceilf'] = ceilf
    # Same AEABI memory-copy boundary already supplied by emu_elf.
    m.libc['__aeabi_memcpy4'] = m.libc['__aeabi_memcpy']
    m.libc['__aeabi_memcpy8'] = m.libc['__aeabi_memcpy']
    m.relocate()
    return m

def blank_map(m, addr):
    # RobotDataLoader ctor 0x0051ED78..84: zero 4 words, max_load_factor=1.
    m.wr(addr, bytes(16) + struct.pack('<f', 1.0))

def state(m, table):
    chain = []
    node = m.r32(table + 8)
    while node:
        chain.append(m.r32(node + 8))
        node = m.r32(node)
    buckets = []
    for i in range(m.r32(table + 4)):
        pred = m.r32(m.r32(table) + 4 * i)
        first = m.r32(pred) if pred else 0
        buckets.append(m.r32(first + 8) if first else None)
    return dict(size=m.r32(table+12), buckets=m.r32(table+4), chain=chain,
                bucket_first_key=buckets)

def map_run():
    m = machine(0xA5)
    table, key = m.malloc(20), m.malloc(4)
    blank_map(m, table)
    results = []
    for value in (0, 2, 3, 1, 0):
        m.w32(key, value)
        m.poison_stack()
        m.call(B + 0x0052191B, table, key, count=1000000)
        results.append(dict(insert=value, **state(m, table)))
    print(json.dumps(dict(test='M5-046 shipped operator[]', results=results)))

def loader_run(poison):
    m = machine(poison)
    loader, closure = m.malloc(0x280), m.malloc(8)
    m.wr(loader, bytes(0x280))
    blank_map(m, loader+0x44)
    m.w32(closure+4, loader)
    files = sorted((ROOT / 're-analysis/obb/assets/cozmo_resources/assets/animationGroups').rglob('*.json'))[:4]
    if len(files) != 4:
        raise RuntimeError('fixture needs four shipped group JSON paths')
    for path in files:
        raw = str(path).encode()
        data, string = m.malloc(len(raw)+1), m.malloc(12)
        m.wr(data, raw+b'\0')
        m.wr(string, bytes(12))
        m.call(B+0x004E02B3, string, data, len(raw), count=1000000)
        m.call(B+0x00524343, closure, string, count=1000000)
    seen = []
    watched = {B+0x0051FC74:'LoadAnimationGroups', B+0x00521AFC:'DispatchWorker.Process',
               B+0x00521D7C:'LoadAnimationGroupFile', B+0x0058A680:'entry construction in group DefineFromJson'}
    def observe(uc, addr, size, data):
        if addr in watched:
            seen.append(dict(address=hex(addr-B), function=watched[addr]))
    m.uc.hook_add(UC_HOOK_CODE, observe)
    m.poison_stack()
    try:
        m.call(B+0x0051FC75, loader, count=2000000)
        outcome = 'returned'
    except Abort as e:
        outcome = str(e)
    print(json.dumps(dict(test='M5-047 loader attempt', poison=hex(poison),
                         fixture='zeroed loader; ctor map fields; four paths through shipped collect lambda; no DataPlatform/context',
                         files=[str(p.relative_to(ROOT)) for p in files], seen=seen, outcome=outcome,
                         pc=hex(m.uc.reg_read(UC_ARM_REG_PC)), lr=hex(m.uc.reg_read(UC_ARM_REG_LR)))))

if __name__ == '__main__':
    map_run()
    loader_run(0)
    loader_run(0xA5)
