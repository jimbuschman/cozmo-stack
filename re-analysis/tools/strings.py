import sys, re, lief
b=lief.ELF.parse(sys.argv[1])
ro=b.get_section(".rodata"); data=bytes(ro.content); base=ro.virtual_address
out=open(sys.argv[2],"w",encoding="utf-8"); n=0
for m in re.finditer(rb"[\x20-\x7e]{6,}", data):
    out.write(f"0x{base+m.start():08x} {m.group().decode()}\n"); n+=1
print("strings:",n)
