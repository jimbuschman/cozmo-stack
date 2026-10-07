"""Decode the shipped 79-way TBH; constructor bodies remain their layer's work."""
from pathlib import Path
import importlib.util,struct,contextlib
h=Path(__file__).resolve().parent
s=importlib.util.spec_from_file_location('native',h/'20261007-queue5-native.py')
n=importlib.util.module_from_spec(s);s.loader.exec_module(n)
base=0x59c8a8
targets=[base+2*x for x in struct.unpack('<79H',n.read(base,158))]
rows=[]
with (h/'20261007-q17-factory-census-native.txt').open('w',encoding='utf-8') as out,contextlib.redirect_stdout(out):
 print('ENGINE SHA256',n.hashlib.sha256(n.raw).hexdigest())
 for ordinal,a in enumerate(targets):
  z=next((x for x in sorted(set(targets)) if x>a),0x59d404)
  print('CLASS',ordinal,'TBH',hex(base+ordinal*2),'TARGET',hex(a))
  n.dump(a,z,True)
  md=n.capstone.Cs(n.capstone.CS_ARCH_ARM,n.capstone.CS_MODE_THUMB)
  calls=[]
  for i in md.disasm(n.read(a,z-a),a):
   if i.mnemonic in ('bl','blx') and i.op_str.startswith('#'):
    target=int(i.op_str[1:],16)&~1;info=n.enclosing(target)
    name=n.symbols.get(target,'') or (info[3] if info else '')
    calls.append(f'0x{i.address:08X} → {name} (0x{target:08X})')
  rows.append(f'| {ordinal} | 0x{base+ordinal*2:08X} | 0x{a:08X}..0x{z:08X} | '+ '; '.join(calls)+' |')
 n.dump(0x59d404,0x59d408,True)
(h/'20261007-q17-factory-census-rows.md').write_text('''### Behavior class factory dispatch (BC5)

`20261007-q17-factory-census-native.txt` decodes all79 halfword entries from the shipped TBH. At0059C89A compare unsigned class with4E; larger classes go to0059D404. Rows show actual calls, including allocation, constructor and shared ownership adapter. Robot and unchanged config are passed into each constructor. Construction bodies belong to their individual behavior records; this factory does not establish those bodies. C# host: Behavior/BehaviorContainer.cs CreateBehavior.

| Class ordinal | TBH entry | Instruction block (exclusive end) | Ordered native calls |
|---|---|---|---|
'''+ '\n'.join(rows)+'\n',encoding='utf-8')
print('Decoded',len(rows),'class entries')
