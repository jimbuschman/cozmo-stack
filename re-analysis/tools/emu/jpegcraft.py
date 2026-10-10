"""A small JPEG assembler and baseline encoder for the decode oracle's corpus (M3-001 / M3-018).

It writes the marker segments by hand (SOI, APPn, DQT, SOF0/1/2/9/10, DHT, DAC, DRI, SOS, RSTn, EOI), encodes coefficient blocks with
the Annex K Huffman tables (any sampling factors, interleaved or one scan per component, restart intervals), and can emit random bytes
as entropy data (every bit string is a valid arithmetic-coded stream, and the standard Huffman tables rarely reject random bits), so a
corpus reaches the progressive, arithmetic, restart, resynchronisation, zero-padding and error branches of the shipped decoder without
needing a progressive or arithmetic encoder. Nothing here decides what the decoder does; the oracle (emu_jpeg_decode.py) does.
"""
import random
import struct

# Annex K standard Huffman tables: (bits[1..16], huffval)
STD_DC_LUM = ([0, 1, 5, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0], list(range(12)))
STD_DC_CHR = ([0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0], list(range(12)))
STD_AC_LUM = ([0, 2, 1, 3, 3, 2, 4, 3, 5, 5, 4, 4, 0, 0, 1, 0x7D], [
    0x01, 0x02, 0x03, 0x00, 0x04, 0x11, 0x05, 0x12, 0x21, 0x31, 0x41, 0x06, 0x13, 0x51, 0x61, 0x07, 0x22, 0x71, 0x14, 0x32, 0x81, 0x91, 0xA1, 0x08,
    0x23, 0x42, 0xB1, 0xC1, 0x15, 0x52, 0xD1, 0xF0, 0x24, 0x33, 0x62, 0x72, 0x82, 0x09, 0x0A, 0x16, 0x17, 0x18, 0x19, 0x1A, 0x25, 0x26, 0x27, 0x28,
    0x29, 0x2A, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39, 0x3A, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48, 0x49, 0x4A, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58, 0x59,
    0x5A, 0x63, 0x64, 0x65, 0x66, 0x67, 0x68, 0x69, 0x6A, 0x73, 0x74, 0x75, 0x76, 0x77, 0x78, 0x79, 0x7A, 0x83, 0x84, 0x85, 0x86, 0x87, 0x88, 0x89,
    0x8A, 0x92, 0x93, 0x94, 0x95, 0x96, 0x97, 0x98, 0x99, 0x9A, 0xA2, 0xA3, 0xA4, 0xA5, 0xA6, 0xA7, 0xA8, 0xA9, 0xAA, 0xB2, 0xB3, 0xB4, 0xB5, 0xB6,
    0xB7, 0xB8, 0xB9, 0xBA, 0xC2, 0xC3, 0xC4, 0xC5, 0xC6, 0xC7, 0xC8, 0xC9, 0xCA, 0xD2, 0xD3, 0xD4, 0xD5, 0xD6, 0xD7, 0xD8, 0xD9, 0xDA, 0xE1, 0xE2,
    0xE3, 0xE4, 0xE5, 0xE6, 0xE7, 0xE8, 0xE9, 0xEA, 0xF1, 0xF2, 0xF3, 0xF4, 0xF5, 0xF6, 0xF7, 0xF8, 0xF9, 0xFA])
STD_AC_CHR = ([0, 2, 1, 2, 4, 4, 3, 4, 7, 5, 4, 4, 0, 1, 2, 0x77], [
    0x00, 0x01, 0x02, 0x03, 0x11, 0x04, 0x05, 0x21, 0x31, 0x06, 0x12, 0x41, 0x51, 0x07, 0x61, 0x71, 0x13, 0x22, 0x32, 0x81, 0x08, 0x14, 0x42, 0x91,
    0xA1, 0xB1, 0xC1, 0x09, 0x23, 0x33, 0x52, 0xF0, 0x15, 0x62, 0x72, 0xD1, 0x0A, 0x16, 0x24, 0x34, 0xE1, 0x25, 0xF1, 0x17, 0x18, 0x19, 0x1A, 0x26,
    0x27, 0x28, 0x29, 0x2A, 0x35, 0x36, 0x37, 0x38, 0x39, 0x3A, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48, 0x49, 0x4A, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58,
    0x59, 0x5A, 0x63, 0x64, 0x65, 0x66, 0x67, 0x68, 0x69, 0x6A, 0x73, 0x74, 0x75, 0x76, 0x77, 0x78, 0x79, 0x7A, 0x82, 0x83, 0x84, 0x85, 0x86, 0x87,
    0x88, 0x89, 0x8A, 0x92, 0x93, 0x94, 0x95, 0x96, 0x97, 0x98, 0x99, 0x9A, 0xA2, 0xA3, 0xA4, 0xA5, 0xA6, 0xA7, 0xA8, 0xA9, 0xAA, 0xB2, 0xB3, 0xB4,
    0xB5, 0xB6, 0xB7, 0xB8, 0xB9, 0xBA, 0xC2, 0xC3, 0xC4, 0xC5, 0xC6, 0xC7, 0xC8, 0xC9, 0xCA, 0xD2, 0xD3, 0xD4, 0xD5, 0xD6, 0xD7, 0xD8, 0xD9, 0xDA,
    0xE2, 0xE3, 0xE4, 0xE5, 0xE6, 0xE7, 0xE8, 0xE9, 0xEA, 0xF2, 0xF3, 0xF4, 0xF5, 0xF6, 0xF7, 0xF8, 0xF9, 0xFA])

ZIGZAG = [0, 1, 8, 16, 9, 2, 3, 10, 17, 24, 32, 25, 18, 11, 4, 5, 12, 19, 26, 33, 40, 48, 41, 34, 27, 20, 13, 6, 7, 14, 21, 28,
          35, 42, 49, 56, 57, 50, 43, 36, 29, 22, 15, 23, 30, 37, 44, 51, 58, 59, 52, 45, 38, 31, 39, 46, 53, 60, 61, 54, 47, 55, 62, 63]

STD_LUM_Q = [16, 11, 10, 16, 24, 40, 51, 61, 12, 12, 14, 19, 26, 58, 60, 55, 14, 13, 16, 24, 40, 57, 69, 56, 14, 17, 22, 29, 51, 87, 80, 62,
             18, 22, 37, 56, 68, 109, 103, 77, 24, 35, 55, 64, 81, 104, 113, 92, 49, 64, 78, 87, 103, 121, 120, 101, 72, 92, 95, 98, 112, 100, 103, 99]


def seg(marker, payload):
    return b'\xFF' + bytes([marker]) + struct.pack('>H', len(payload) + 2) + bytes(payload)


def soi():
    return b'\xFF\xD8'


def eoi():
    return b'\xFF\xD9'


def app0_jfif(major=1, minor=1, unit=0, xd=1, yd=1, tw=0, th=0):
    return seg(0xE0, b'JFIF\x00' + bytes([major, minor, unit]) + struct.pack('>HH', xd, yd) + bytes([tw, th]))


def app14_adobe(transform, version=100, f0=0, f1=0):
    return seg(0xEE, b'Adobe' + struct.pack('>HHH', version, f0, f1) + bytes([transform]))


def dqt(tables):
    """tables: list of (id, 64 values in natural order, precision 0|1); stored in zigzag order."""
    out = b''
    for tid, vals, prec in tables:
        out += bytes([(prec << 4) | tid])
        for i in range(64):
            v = vals[ZIGZAG[i]]
            out += struct.pack('>H', v) if prec else bytes([v])
    return seg(0xDB, out)


def dqt_raw(tid, payload_values, prec=0):
    """A DQT with an arbitrary number of values (short tables); values are written in the order given."""
    out = bytes([(prec << 4) | tid])
    for v in payload_values:
        out += struct.pack('>H', v) if prec else bytes([v])
    return seg(0xDB, out)


def sof(marker, precision, height, width, comps):
    """comps: list of (id, h, v, tq)."""
    out = bytes([precision]) + struct.pack('>HH', height, width) + bytes([len(comps)])
    for cid, h, v, tq in comps:
        out += bytes([cid, (h << 4) | v, tq])
    return seg(marker, out)


def dht(tables):
    """tables: list of (tc_th byte, bits[16], vals)."""
    out = b''
    for b, bits, vals in tables:
        out += bytes([b]) + bytes(bits) + bytes(vals)
    return seg(0xC4, out)


def dac(entries):
    return seg(0xCC, b''.join(bytes([i, v]) for i, v in entries))


def dri(n):
    return seg(0xDD, struct.pack('>H', n))


def sos(comps, ss=0, se=63, ah=0, al=0):
    out = bytes([len(comps)])
    for cid, td, ta in comps:
        out += bytes([cid, (td << 4) | ta])
    out += bytes([ss, se, (ah << 4) | al])
    return seg(0xDA, out)


def std_dht_all():
    return dht([(0x00, *STD_DC_LUM), (0x10, *STD_AC_LUM), (0x01, *STD_DC_CHR), (0x11, *STD_AC_CHR)])


def stuff(data):
    return bytes(data).replace(b'\xFF', b'\xFF\x00')


class BitWriter:
    def __init__(self):
        self.out = bytearray()
        self.acc = 0
        self.n = 0

    def put(self, code, size):
        self.acc = (self.acc << size) | (code & ((1 << size) - 1))
        self.n += size
        while self.n >= 8:
            self.n -= 8
            b = (self.acc >> self.n) & 0xFF
            self.out.append(b)
            if b == 0xFF:
                self.out.append(0)
        self.acc &= (1 << self.n) - 1

    def flush(self):
        if self.n:
            self.put((1 << (8 - self.n)) - 1, 8 - self.n)


def _codes(bits, vals):
    code = 0
    k = 0
    table = {}
    for l in range(1, 17):
        for _ in range(bits[l - 1]):
            table[vals[k]] = (code, l)
            code += 1
            k += 1
        code <<= 1
    return table


def _cat(v):
    a = abs(v)
    n = 0
    while a:
        n += 1
        a >>= 1
    return n


def _encode_block(bw, blk, last_dc, dc_t, ac_t):
    """blk: 64 coefficients in zigzag order; returns the new DC predictor."""
    diff = blk[0] - last_dc
    n = _cat(diff)
    code, size = dc_t[n]
    bw.put(code, size)
    if n:
        bw.put(diff if diff >= 0 else diff - 1, n)
    run = 0
    for k in range(1, 64):
        v = blk[k]
        if v == 0:
            run += 1
            continue
        while run > 15:
            code, size = ac_t[0xF0]
            bw.put(code, size)
            run -= 16
        n = _cat(v)
        code, size = ac_t[(run << 4) | n]
        bw.put(code, size)
        bw.put(v if v >= 0 else v - 1, n)
        run = 0
    if run:
        code, size = ac_t[0x00]
        bw.put(code, size)
    return blk[0]


def random_blocks(rnd, count, strength=1.0, dc_range=200):
    """count blocks of zigzag coefficients: a DC and a decaying number of small AC values."""
    blocks = []
    for _ in range(count):
        b = [0] * 64
        b[0] = rnd.randint(-dc_range, dc_range)
        n = rnd.choice([0, 1, 2, 3, 5, 8, 12, 20, 40, 63])
        for k in range(1, 64):
            if k <= n and rnd.random() < 0.6:
                mag = max(1, int(rnd.expovariate(1.0 / max(1.0, 12.0 * strength / (1 + k / 4)))))
                b[k] = rnd.choice([-1, 1]) * min(mag, 1000)
        blocks.append(b)
    return blocks


def encode_baseline_scan(comps, width, height, block_data, tables, restart_interval=0, interleaved=True):
    """
    Entropy data for a baseline scan set.
      comps: list of (id, h, v, dc_table_index, ac_table_index) in frame order
      block_data: per component, a dict {(bx, by): 64 zigzag coefficients} (missing blocks are all zero)
      tables: dict {('dc'|'ac', index): (bits, vals)}
    Returns a list of scans; each scan is (list of component positions in the scan, entropy bytes with RSTn markers).
    """
    maxh = max(c[1] for c in comps)
    maxv = max(c[2] for c in comps)
    enc = {k: _codes(*v) for k, v in tables.items()}
    scans = []

    def blocks_wh(ci):
        _, h, v, _, _ = comps[ci]
        return -(-width * h // (maxh * 8)), -(-height * v // (maxv * 8))

    def run_scan(order_fn, ncomp_in_scan, mcus):
        bw = BitWriter()
        out = bytearray()
        last = [0] * len(comps)
        rst = 0
        for m, mcu in enumerate(mcus):
            if restart_interval and m and m % restart_interval == 0:
                bw.flush()
                out += bw.out
                out += bytes([0xFF, 0xD0 + rst])
                rst = (rst + 1) & 7
                bw = BitWriter()
                last = [0] * len(comps)
            for ci, bx, by in mcu:
                blk = block_data[ci].get((bx, by), [0] * 64)
                _, _, _, td, ta = comps[ci]
                last[ci] = _encode_block(bw, blk, last[ci], enc[('dc', td)], enc[('ac', ta)])
        bw.flush()
        out += bw.out
        return bytes(out)

    if interleaved or len(comps) == 1:
        mcux = -(-width // (maxh * 8))
        mcuy = -(-height // (maxv * 8))
        mcus = []
        for my in range(mcuy):
            for mx in range(mcux):
                mcu = []
                for ci, (_, h, v, _, _) in enumerate(comps):
                    for yy in range(v):
                        for xx in range(h):
                            mcu.append((ci, mx * h + xx, my * v + yy))
                mcus.append(mcu)
        if len(comps) == 1:
            bw_, bh_ = blocks_wh(0)
            mcus = [[(0, x, y)] for y in range(bh_) for x in range(bw_)]
        scans.append((list(range(len(comps))), run_scan(None, len(comps), mcus)))
    else:
        for ci in range(len(comps)):
            bw_, bh_ = blocks_wh(ci)
            mcus = [[(ci, x, y)] for y in range(bh_) for x in range(bw_)]
            scans.append(([ci], run_scan(None, 1, mcus)))
    return scans


def blocks_per_component(comps, width, height):
    maxh = max(c[1] for c in comps)
    maxv = max(c[2] for c in comps)
    out = []
    for c in comps:
        _, h, v = c[0], c[1], c[2]
        # blocks covering the component, padded to whole MCUs for interleaved scans
        mcux = -(-width // (maxh * 8))
        mcuy = -(-height // (maxv * 8))
        out.append((mcux * h, mcuy * v))
    return out


def build_baseline(width, height, comps, rnd, qtables=None, restart_interval=0, interleaved=True, adobe=None, jfif=True,
                   marker=0xC0, strength=1.0):
    """
    A complete baseline file. comps: list of (id, h, v) - component i uses quantisation table 0 (i == 0) or 1 and Huffman table 0 / 1.
    """
    c5 = [(cid, h, v, 0 if i == 0 else 1, 0 if i == 0 else 1) for i, (cid, h, v) in enumerate(comps)]
    qt = qtables or [(0, STD_LUM_Q, 0), (1, [max(1, q // 2 + 1) for q in STD_LUM_Q], 0)]
    tables = {('dc', 0): STD_DC_LUM, ('ac', 0): STD_AC_LUM, ('dc', 1): STD_DC_CHR, ('ac', 1): STD_AC_CHR}
    sizes = blocks_per_component(c5, width, height)
    block_data = []
    for ci, (bw_, bh_) in enumerate(sizes):
        lst = random_blocks(rnd, bw_ * bh_, strength)
        block_data.append({(x, y): lst[y * bw_ + x] for y in range(bh_) for x in range(bw_)})
    scans = encode_baseline_scan(c5, width, height, block_data, tables, restart_interval, interleaved)
    out = soi()
    if jfif:
        out += app0_jfif()
    if adobe is not None:
        out += app14_adobe(adobe)
    out += dqt(qt) + sof(marker, 8, height, width, [(cid, h, v, 0 if i == 0 else 1) for i, (cid, h, v) in enumerate(comps)])
    out += std_dht_all()
    if restart_interval:
        out += dri(restart_interval)
    for idxs, ent in scans:
        out += sos([(c5[i][0], c5[i][3], c5[i][4]) for i in idxs])
        out += ent
    out += eoi()
    return out


def random_entropy(rnd, n, ff_stuffing=True):
    """n random bytes, FF-stuffed (so no accidental markers)."""
    data = bytes(rnd.randrange(256) for _ in range(n))
    return stuff(data) if ff_stuffing else data
