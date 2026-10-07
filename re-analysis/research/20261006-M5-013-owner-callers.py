"""Symbol-state native caller navigation for three anonymous store recipients."""
from pathlib import Path
import lief, capstone, json
root=Path(__file__).resolve().parents[2]
binary=root/'resources/lib/armeabi-v7a/libcozmoEngine.so'
elf=lief.parse(str(binary)); raw=binary.read_bytes()
loads=[s for s in elf.segments if s.type==lief.ELF.Segment.TYPE.LOAD]
targets={0x4F50F8,0x56D816,0x5D9F50}
rows=[]; seen=set()
for s in elf.symbols:
    if not s.value or not s.size or s.type!=lief.ELF.Symbol.TYPE.FUNC:continue
    key=(s.value&~1,bool(s.value&1))
    if key in seen:continue
    seen.add(key); a,thumb=key
    segment=next((p for p in loads if p.virtual_address<=a and a+s.size<=p.virtual_address+p.physical_size),None)
    if segment is None:continue
    offset=segment.file_offset+a-segment.virtual_address
    md=capstone.Cs(capstone.CS_ARCH_ARM,capstone.CS_MODE_THUMB if thumb else capstone.CS_MODE_ARM)
    md.skipdata=True; history=[]
    for pc,size,mn,ops in md.disasm_lite(raw[offset:offset+s.size],a):
        if mn in ('bl','blx','b.w') and ops.startswith('#0x') and int(ops[1:],16)&~1 in targets:
            rows.append({'caller':s.name,'caller_va':hex(a),'address':hex(pc),'target':ops,'preceding':history[-14:]})
        history.append(f'{pc:08X}: {mn} {ops}')
destination=root/'re-analysis/research/20261006-M5-013-owner-callers.json'
destination.write_text(json.dumps({'scope':'Direct calls from symbol-bounded native functions only; navigation requires receiver checking.','calls':rows},indent=2)+'\n',encoding='utf-8')
print(len(rows),'direct caller candidates')
