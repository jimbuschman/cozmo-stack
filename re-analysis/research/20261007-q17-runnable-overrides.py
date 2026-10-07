"""Read every concrete eligibility slot in the shipped behavior vtable census."""
import importlib.util,contextlib,re
from pathlib import Path
h=Path(__file__).resolve().parent
s=importlib.util.spec_from_file_location('native',h/'20261007-queue5-native.py');n=importlib.util.module_from_spec(s);s.loader.exec_module(n)
tables=(h/'20261007-q17-vtables-native.txt').read_text(encoding='utf-8').splitlines()
slots={}
class_slots=[]
for line in tables:
    if not line.startswith('VTABLE '):continue
    class_slots.append((line.split()[1],line.split()[2],re.findall(r'\+(20|24|28)=([0-9A-F]{8})',line)))
    for k,v in re.findall(r'\+(20|24|28)=([0-9A-F]{8})',line):
        slots.setdefault(int(v,16)&~1,[]).append((line.split()[2],k))
with (h/'20261007-q17-runnable-overrides-native.txt').open('w',encoding='utf-8',newline='\n') as o,contextlib.redirect_stdout(o):
    print('ENGINE SHA256',n.hashlib.sha256(n.raw).hexdigest())
    for a,owners in sorted(slots.items()):
        print('ELIGIBILITY',f'{a:08X}', ' '.join(f'{name}+{k}' for name,k in owners))
        if not a:print('EXTERNAL __cxa_pure_virtual binding, not null function');continue
        f=n.enclosing(a)
        z=n.end(f) if f and f[0]==a else a+0x10
        if z-a>0x400:z=a+0x80;print('FRAGMENTED: explicit closure required')
        n.dump(a,z,True)
md=n.capstone.Cs(n.capstone.CS_ARCH_ARM,n.capstone.CS_MODE_THUMB)
def behavior(a):
    if not a:return ('external __cxa_pure_virtual','external binding')
    ins=list(md.disasm(n.read(a,8),a))[:2]
    if len(ins)==2 and ins[1].mnemonic=='bx' and ins[1].op_str=='lr':
        if ins[0].mnemonic=='movs' and ins[0].op_str in ('r0, #0','r0, #1'):
            return (ins[0].op_str[-1],f'0x{a:08X}..0x{ins[1].address:08X}')
        if ins[0].mnemonic=='ldrb.w' and ins[0].op_str=='r0, [r0, #0x120]':
            return ('raw byte120',f'0x{a:08X}..0x{ins[1].address:08X}')
    raise RuntimeError(f'Nontrivial eligibility {a:08X}; extract before generating')
with (h/'20261007-q17-runnable-rows.md').open('w',encoding='utf-8',newline='\n') as o:
    o.write('## Concrete eligibility slots: primary vtable census\n\n')
    o.write('Slot20 is off-treads,24 charging,28 carrying. The base IsRunnableBase\nrequires exactly1 at each applicable slot (RB4). Constant0 rejects; constant1\nadmits; raw byte120 therefore only admits when equal1. Table entries are\nrelocated ELF values, not invented defaults.\n\n')
    o.write('| Step | Address | Behavior | Gates | Order | Failure / result | Float bits / UNKNOWN | C# host |\n|---|---|---|---|---|---|---|---|\n')
    for i,(vt,name,cells) in enumerate(class_slots,1):
        cls=re.sub(r'^_ZTVN4Anki5Cozmo\d+','',name).removesuffix('E')
        vals=[behavior(int(v,16)&~1) for k,v in cells]
        addr='; '.join(f'{k}: {b[1]}' for (k,v),b in zip(cells,vals))
        result='; '.join(f'{k}={b[0]}' for (k,v),b in zip(cells,vals))
        o.write(f'| RV{i:02} | vtable0x{vt}; {addr} | {result} | RB4 state gate |20 then24 then28 | Exact1 required | No floats | {cls} eligibility overrides |\n')
