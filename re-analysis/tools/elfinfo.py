import sys, lief
p = sys.argv[1]
b = lief.ELF.parse(p)
print("== HEADER ==")
h=b.header
print("machine", h.machine_type, "type", h.file_type, "entry", hex(h.entrypoint))
print("== SECTIONS ==")
for s in b.sections:
    print(f"{s.name:24s} off=0x{s.offset:08x} va=0x{s.virtual_address:08x} size=0x{s.size:08x} {s.type}")
print("== NEEDED ==")
for l in b.libraries: print(" ", l)
print("== SONAME ==", b.get(lief.ELF.DynamicEntry.TAG.SONAME) if b.has(lief.ELF.DynamicEntry.TAG.SONAME) else None)
exp=[s for s in b.exported_functions]
imp=[s for s in b.imported_functions]
print("== EXPORTED FUNCS:", len(exp), " IMPORTED FUNCS:", len(imp))
dsyms=list(b.dynamic_symbols)
print("== DYNSYM count:", len(dsyms))
ssyms=list(b.symtab_symbols)
print("== SYMTAB count:", len(ssyms))
# JNI / cozmo_ / Unity_ exports
print("== INTERESTING EXPORTS ==")
for f in exp:
    n=f.name
    if n.startswith(("cozmo_","Unity_","Java_","JNI_","anki","Anki")):
        print(f"  0x{f.address:08x} {n}")
print("== TOTAL exports listing written ==")
with open(sys.argv[2],"w",encoding="utf-8") as fo:
    for f in sorted(exp,key=lambda x:x.address): fo.write(f"0x{f.address:08x} {f.name}\n")
with open(sys.argv[3],"w",encoding="utf-8") as fo:
    for f in imp: fo.write(f"{f.name}\n")
