"""Research-only, read-only byte comparison against the located official prebuilt."""
from pathlib import Path
import hashlib, json
import lief
root=Path(__file__).resolve().parents[2]
a_path=root/'resources/lib/armeabi-v7a/libc++_shared.so'
b_path=Path(__file__).resolve().parent/'20261005-converter-lineage-source/official-r15-libc++_shared.so'
a,b=a_path.read_bytes(),b_path.read_bytes()
assert hashlib.sha256(a).hexdigest()=='8ac5090bbd0be7401af5fff6044ded6f1e6ced692fcd3581fa3cb937b519a16a'
assert hashlib.sha256(b).hexdigest()=='2559653b6f02237ad8e3613fece91ff4cde48779a7e87e6dc067834b5759ede7'
elf=lief.parse(str(a_path))
rows=[]
for lo,hi in [(0x7e570,0x7f3be),(0x7e4bc,0x7e562),(0x7f910,0x7ffe8),(0x80ca8,0x80d4c),(0x81200,0x81314),(0x5eb98,0x5ec24),(0x7ee82,0x7ef12),(0x9fc88,0x9fd90)]:
 seg=next(s for s in elf.segments if s.virtual_address<=lo and hi<=s.virtual_address+s.physical_size)
 offset=lo-seg.virtual_address+seg.file_offset
 x,y=a[offset:offset+hi-lo],b[offset:offset+hi-lo]
 rows.append(dict(start=hex(lo),end_exclusive=hex(hi),equal=x==y,sha256=hashlib.sha256(x).hexdigest()))
print(json.dumps(rows,indent=2))
assert all(row['equal'] for row in rows)
