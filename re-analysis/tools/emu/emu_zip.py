"""Run the engine's own zip-index functions (Thumb) under Unicorn, for WwiseAnkiResolverTests.

0x8DE3BC (the pread loop) and 0x8DE1D0 (the end-of-central-directory scan and the central-directory walk) are run on a
memory-backed "file": pread is hooked to read from a Python bytes object (and, per case, to fail), and the map's
operator[] (0x8DE454) is hooked to record the {dataOffset, size} stores. Everything else is the engine's own code.

    python re-analysis/tools/emu/emu_zip.py

The expected values in WwiseAnkiResolverTests (Zip*) are this script's output (M6-wwise-bank.md C33.1, research
live-bodies-3 section 4), not the C#'s. The script prints one JSON object per case.
"""
import json
import struct
import sys

from emu_common import Emu, BASE, RET
from emu_svc import SvcEmu
from unicorn import UC_HOOK_INTR
from unicorn.arm_const import *

IDX = BASE + 0x1000          # the index object: [0] fd, [4] base, [8] size, [0xC] map
FILE_BUF = BASE + 0x10000
ERRNO = BASE + 0x200
CELLS = BASE + 0x400         # value cells handed out by the hooked operator[]

PTHREAD_LOCK = 0x4D3064


def make_emu(data, preads=None):
    """preads: optional list of scripted (ret, errno) results consumed by the first pread calls; then the real read."""
    e = SvcEmu()
    state = {'preads': list(preads or []), 'calls': [], 'cells': {}, 'next': CELLS, 'names': []}

    def pread(em):
        fd, buf, n, off = em.reg(0), em.reg(1), em.reg(2), em.reg(3)
        state['calls'].append((n, off))
        if state['preads']:
            ret, err = state['preads'].pop(0)
            if ret is not None:
                em.w32(ERRNO, err)
                return ret & 0xFFFFFFFF
        if off >= len(data):
            return 0
        chunk = data[off:off + n]
        em.uc.mem_write(buf, chunk)
        return len(chunk)
    e.svc_hook(0x4D2590, pread)
    e.svc_hook(0x4A6E20, lambda em: ERRNO)
    e.svc_hook(0x4A42A0, lambda em: em.alloc(em.reg(0)))            # operator new
    e.svc_hook(0x4A40CC, lambda em: None)                           # operator delete
    e.svc_hook(0x4A48AC, lambda em: em.uc.mem_write(em.reg(0), bytes(em.reg(1))) or None)   # __aeabi_memclr8(ptr, n)
    e.svc_hook(0x4A403C, lambda em: em.uc.mem_write(em.reg(0), bytes(em.reg(1))) or None)   # __aeabi_memclr4
    e.svc_hook(0x4D37F0, lambda em: (em.uc.mem_write(em.reg(0), bytes(em.uc.mem_read(em.reg(1), em.reg(2)))), em.reg(0))[1])   # memcpy

    def read_string(em, p):
        b0 = em.r8(p)
        if b0 & 1:
            n = em.r32(p + 4)
            q = em.r32(p + 8)
        else:
            n = b0 >> 1
            q = p + 1
        return bytes(em.uc.mem_read(q, n)) if n else b''

    def opidx(em):                                                  # 0x8DE454(map, key) -> value pointer
        name = read_string(em, em.reg(1))
        cell = state['next']
        state['next'] += 16
        em.w32(cell, 0xDEAD)
        em.w32(cell + 4, 0xBEEF)
        state['cells'][name] = cell
        state['names'].append(name)
        return cell
    e.svc_hook(0x8DE454, opidx, thumb=True)
    e.state = state
    e.uc.mem_write(FILE_BUF, data)
    return e


def thumb_call(e, addr, *args, stack=()):
    return e.call(addr | 1, *args, stack=stack)


def new_index(e, size, base=0):
    e.uc.mem_write(IDX, bytes(0x40))
    e.w32(IDX, 3)                  # a valid descriptor
    e.w32(IDX + 4, base)
    e.w32(IDX + 8, size)


def case_readat(name, data, preads, off, n):
    e = make_emu(data, preads)
    new_index(e, len(data))
    buf = BASE + 0x2000
    e.uc.mem_write(buf, bytes([0xAA]) * 0x40)
    r = thumb_call(e, 0x8DE3BC, IDX, buf, off, n)
    out = bytes(e.uc.mem_read(buf, min(n, 0x40) if n else 0x10))
    print(json.dumps({'case': name, 'result': r, 'buffer': out.hex(), 'preads': e.state['calls']}))


def case_entry(name, data, entry_off, pos, size):
    """0x8DE654(index, entryOffset, pos, size, buf): the bound is the archive size, then the pread loop."""
    e = make_emu(data)
    new_index(e, len(data))
    buf = BASE + 0x2000
    e.uc.mem_write(buf, bytes([0xAA]) * 0x40)
    r = thumb_call(e, 0x8DE654, IDX, entry_off, pos, size, stack=(buf,))
    print(json.dumps({'case': name, 'result': r, 'buffer': bytes(e.uc.mem_read(buf, 12)).hex(), 'preads': e.state['calls']}))


def case_parse(name, data):
    e = make_emu(data)
    new_index(e, len(data))
    r = thumb_call(e, 0x8DE1D0, IDX)
    entries = {}
    for k, cell in e.state['cells'].items():
        entries[k.decode('latin1')] = [e.r32(cell), e.r32(cell + 4)]
    print(json.dumps({'case': name, 'result': r, 'entries': entries, 'order': [k.decode('latin1') for k in e.state['names']]}))


def build_zip(entries, comment=b'', trailing=b'', method_bytes=None):
    """entries: list of (name, payload). Stored, with an extra field of 4 bytes in the local header of the entry named 'x.extra'."""
    out = bytearray()
    cd = bytearray()
    for name, payload in entries:
        nb = name.encode('latin1')
        extra = b'\x55\x54\x00\x00' if name.endswith('.extra') else b''
        lho = len(out)
        out += struct.pack('<IHHHHHIIIHH', 0x04034B50, 20, 0, 0, 0, 0, 0, len(payload), len(payload), len(nb), len(extra))
        out += nb + extra + payload
        cd += struct.pack('<IHHHHHHIIIHHHHHII', 0x02014B50, 20, 20, 0, 0, 0, 0, 0, len(payload), len(payload), len(nb), 0, 3, 0, 0, 0, lho) + nb + b'abc'
    cdo = len(out)
    out += cd
    out += struct.pack('<IHHHHIIH', 0x06054B50, 0, 0, len(entries), len(entries), len(cd), cdo, len(comment)) + comment
    return bytes(out) + trailing


if __name__ == '__main__':
    base_zip = build_zip([('English(US)/', b''), ('English(US)/10.wem', b'hello'), ('20.wem', b'world!!'), ('x.extra', b'zz'), ('dup', b'one'), ('dup', b'twotwo')])
    case_parse('plain', base_zip)
    case_parse('comment_below_the_scan_limit', build_zip([('a', b'abc')], comment=b'c' * 0x300))
    case_parse('comment_beyond_the_scan_limit', build_zip([('a', b'abc')], comment=b'c' * 0x500))
    case_parse('empty_archive', build_zip([]))
    bad = bytearray(base_zip)
    cd_off = struct.unpack_from('<I', base_zip, len(base_zip) - 6)[0]
    bad[cd_off] ^= 0xFF                                  # the first central-directory signature
    case_parse('bad_central_signature', bytes(bad))
    bad2 = bytearray(base_zip)
    bad2[0] ^= 0xFF                                      # the first local-header signature
    case_parse('bad_local_signature', bytes(bad2))
    bad3 = bytearray(base_zip)
    bad3[42] ^= 0xFF                                     # the local-header signature of the first non-empty entry
    case_parse('bad_local_signature_nonempty', bytes(bad3))
    case_parse('comment_1024', build_zip([('a', b'abc')], comment=b'c' * 1024))
    case_parse('comment_1025', build_zip([('a', b'abc')], comment=b'c' * 1025))
    case_parse('too_short', b'PK' * 5)
    # the entry read
    base_len = len(base_zip)
    case_entry('entry_inside', base_zip, 131, 0, 7)
    case_entry('entry_one_past_the_entry', base_zip, 131, 0, 8)
    case_entry('entry_past_the_archive', base_zip, 131, base_len, 1)
    case_entry('entry_exactly_to_the_end', base_zip, 0, base_len - 4, 4)
    case_entry('entry_one_past_the_end', base_zip, 0, base_len - 4, 5)
    case_entry('entry_wraps_32_bits', base_zip, 0xFFFFFFFF, 2, 1)
    # the pread loop
    data = bytes(range(64))
    case_readat('whole', data, [], 8, 16)
    case_readat('partial_then_rest', data, [(5, 0)], 8, 16)
    case_readat('eof', data, [], 60, 16)
    case_readat('eintr_then_ok', data, [(0xFFFFFFFF, 4)], 8, 16)
    case_readat('other_error_is_success', data, [(0xFFFFFFFF, 5)], 8, 16)
    case_readat('zero_length', data, [], 8, 0)
