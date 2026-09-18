import sys, struct, lief, capstone, cpp_demangle
so=lief.ELF.parse(sys.argv[1]); raw=open(sys.argv[1],"rb").read()
def dem(n):
    try: return cpp_demangle.demangle(n)
    except Exception: return n
syms={}
for s in so.dynamic_symbols:
    if s.shndx and s.value: syms[s.value&~1]=s.name
# PLT: map stub -> symbol using .rel.plt order (each stub 12 bytes after 20-byte PLT0)
plt=so.get_section(".plt"); pltrels=[r for r in so.pltgot_relocations]
pltmap={}
for i,r in enumerate(pltrels):
    pltmap[plt.virtual_address+20+12*i]=r.symbol.name if r.has_symbol else f"plt{i}"
# GOT: map got entry -> symbol via dynamic relocations
gotmap={r.address:r.symbol.name for r in so.dynamic_relocations if r.has_symbol}
def va2off(va):
    for seg in so.segments:
        if seg.type==lief.ELF.Segment.TYPE.LOAD and seg.virtual_address<=va<seg.virtual_address+seg.virtual_size:
            return va-seg.virtual_address+seg.file_offset
def rd32(va):
    o=va2off(va); return struct.unpack_from("<I",raw,o)[0] if o is not None else None
def cstr(va):
    o=va2off(va)
    if o is None: return None
    e=raw.find(b"\0",o,o+240); s=raw[o:e]
    try: t=s.decode("ascii")
    except: return None
    return t if len(t)>=2 and all(32<=ord(c)<127 for c in t) else None
def describe(v):
    out=[]
    if (v&~1) in syms: out.append(dem(syms[v&~1]))
    if v in gotmap: out.append("GOT->"+dem(gotmap[v]))
    s=cstr(v)
    if s: out.append(f'"{s}"')
    return " ".join(out)
md=capstone.Cs(capstone.CS_ARCH_ARM, capstone.CS_MODE_THUMB); md.detail=True
def target(symname):
    for a,n in syms.items():
        if n==symname: return a
def disasm(symname, maxlen=0x1400, maxlines=2000):
    tgt=target(symname)
    if tgt is None: print("no symbol",symname); return
    off=va2off(tgt); code=raw[off:off+maxlen]
    print(f"\n===== {dem(symname)} @ 0x{tgt:08x} =====")
    regs={}; n=0
    for ins in md.disasm(code,tgt):
        if ins.address!=tgt and ins.address in syms: print("  ---- next symbol ----"); break
        m,o=ins.mnemonic,ins.op_str; note=""
        if m in ("bl","blx","b","b.w","bl.w") and o.startswith("#"):
            t=int(o[1:],16)
            if t in pltmap: note=f"  ; PLT {dem(pltmap[t])}"
            elif (t&~1) in syms: note=f"  ; {dem(syms[t&~1])}"
        elif m=="ldr" and "[pc, #" in o:
            r=o.split(",")[0]; imm=int(o.split("#")[1].rstrip("]"),0)
            v=rd32(((ins.address+4)&~3)+imm); regs[r]=("pcload",v,ins.address); note=f"  ; ={v:#x}"
        elif m=="ldr.w" and "[pc, #" in o:
            r=o.split(",")[0]; imm=int(o.split("#")[1].rstrip("]"),0)
            v=rd32(((ins.address+4)&~3)+imm); regs[r]=("pcload",v,ins.address); note=f"  ; ={v:#x}"
        elif m=="add" and o.endswith(", pc"):
            r=o.split(",")[0]
            if r in regs and regs[r][0]=="pcload":
                v=(regs[r][1]+ins.address+4)&0xffffffff; regs[r]=("abs",v); note=f"  ; {r}={v:#x} {describe(v)}"
        elif m=="addw" and ", pc, #" in o:
            r=o.split(",")[0]; imm=int(o.split("#")[1],0)
            v=((ins.address+4)&~3)+imm; regs[r]=("abs",v); note=f"  ; {r}={v:#x} {describe(v)}"
        elif m=="movw":
            r,imm=o.split(", "); regs[r]=("movw",int(imm[1:],0))
        elif m=="movt":
            r,imm=o.split(", ")
            if r in regs and regs[r][0]=="movw":
                v=regs[r][1]|(int(imm[1:],0)<<16); regs[r]=("abs",v); note=f"  ; {r}={v:#x} {describe(v)}"
        elif m=="ldr" and o.startswith(("r","s","f")) and "[r" in o and "]" in o and ", #" not in o:
            r,src=o.split(", [",1); src=src.rstrip("]")
            if src in regs and regs[src][0]=="abs":
                v=regs[src][1]
                if v in gotmap: note=f"  ; {r}=&{dem(gotmap[v])}"; regs[r]=("sym",gotmap[v])
        print(f"0x{ins.address:08x}  {m:8s} {o}{note}")
        n+=1
        if (m in ("pop","pop.w") and "pc" in o) or (m=="bx" and o=="lr") or n>=maxlines: break
for s in sys.argv[2:]: disasm(s)
