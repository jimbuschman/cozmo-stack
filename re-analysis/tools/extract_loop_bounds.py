"""Add "loop_bounds" to native_layouts.json: the fixed-array lengths compared against inside each
CLAD Unpack routine. A fixed array of N elements compiles to a loop whose exit test is `cmp rX, #N`
(preceded by a `cmp rX, #1` entry guard), so the bounds appear in field order.

usage: python extract_loop_bounds.py <apk-root> <scratch>
"""
import json, sys, os, bisect
import lief, capstone, cpp_demangle

ROOT, SP = sys.argv[1], sys.argv[2]
p = os.path.join(ROOT, "resources", "lib", "armeabi-v7a", "libcozmoEngine.so")
so = lief.ELF.parse(p)
raw = open(p, "rb").read()


def va2off(va):
    for seg in so.segments:
        if seg.type == lief.ELF.Segment.TYPE.LOAD and seg.virtual_address <= va < seg.virtual_address + seg.virtual_size:
            return va - seg.virtual_address + seg.file_offset


md = capstone.Cs(capstone.CS_ARCH_ARM, capstone.CS_MODE_THUMB)
syms = {}
for s in so.dynamic_symbols:
    if s.shndx and s.value and s.name.startswith("_Z"):
        try:
            syms[cpp_demangle.demangle(s.name)] = s.value & ~1
        except Exception:
            pass
addrs = sorted(set(syms.values()))

lay = json.load(open(os.path.join(SP, "native_layouts.json")))
for name, v in lay.items():
    if "addr" not in v:
        continue
    a = int(v["addr"], 16)
    i = bisect.bisect_right(addrs, a)
    nxt = addrs[i] if i < len(addrs) else a + 0x800
    bounds = []
    prev_one = False
    for ins in md.disasm(raw[va2off(a):va2off(a) + min(nxt - a, 0x1000)], a):
        if ins.mnemonic in ("cmp", "cmp.w") and "#" in ins.op_str:
            try:
                n = int(ins.op_str.split("#")[1], 0)
            except ValueError:
                continue
            if n == 1:
                prev_one = True
            else:
                if prev_one and n > 1:
                    bounds.append(n)
                prev_one = False
        elif ins.mnemonic in ("pop", "pop.w") and "pc" in ins.op_str:
            break
    v["loop_bounds"] = bounds
json.dump(lay, open(os.path.join(SP, "native_layouts.json"), "w"), indent=1)
print("loop bounds added for", sum(1 for v in lay.values() if v.get("loop_bounds")), "types")
for k, v in sorted(lay.items()):
    if v.get("loop_bounds"):
        print("  %-26s size=%-8s loops=%s bounds=%s" % (k, v.get("size"), v.get("loops"), v["loop_bounds"]))
