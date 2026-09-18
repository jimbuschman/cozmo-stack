import sys, bisect, lief, capstone, cpp_demangle
so=lief.ELF.parse(sys.argv[1]); raw=open(sys.argv[1],"rb").read()
def dem(n):
    try: return cpp_demangle.demangle(n)
    except Exception: return n
fs=sorted((s.value&~1,s.name) for s in so.dynamic_symbols if s.shndx and s.value and str(s.type)=="TYPE.FUNC")
addrs=[a for a,_ in fs]
def owner(a):
    i=bisect.bisect_right(addrs,a)-1
    return dem(fs[i][1]) if i>=0 else "?"
t=so.get_section(".text"); code=bytes(t.content)
md=capstone.Cs(capstone.CS_ARCH_ARM, capstone.CS_MODE_THUMB); md.skipdata=True
lo,hi=int(sys.argv[2]),int(sys.argv[3])
for ins in md.disasm(code,t.virtual_address):
    if ins.mnemonic in("movw","mov.w","movs","mov") and "#" in ins.op_str:
        try: v=int(ins.op_str.split("#")[1],0)
        except: continue
        if lo<=v<=hi: print(f"0x{ins.address:08x} {ins.mnemonic} {ins.op_str}  in {owner(ins.address)}")
