import sys, lief, cpp_demangle, collections
b=lief.ELF.parse(sys.argv[1])
def dem(n):
    try: return cpp_demangle.demangle(n)
    except Exception: return n
out=open(sys.argv[2],"w",encoding="utf-8")
ztv=zti=zts=0; classes=set()
for s in b.dynamic_symbols:
    if str(s.type)!="TYPE.OBJECT" or s.shndx==0: continue
    n=s.name; d=dem(n)
    out.write(f"0x{s.value:08x} 0x{s.size:06x} {d}\n")
    if n.startswith("_ZTV"): ztv+=1; classes.add(d.replace("vtable for ",""))
    elif n.startswith("_ZTI"): zti+=1
    elif n.startswith("_ZTS"): zts+=1
print("vtables",ztv,"typeinfo",zti,"typeinfo-names",zts)
anki=[c for c in classes if c.startswith("Anki::")]
print("Anki classes with vtables:",len(anki))
beh=sorted(c for c in anki if "::Behavior" in c or "::Activity" in c or "::Action" in c)
print("behaviors/activities/actions:",len(beh))
for c in beh[:400]: print("  ",c)
