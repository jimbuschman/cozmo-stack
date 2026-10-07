"""Reopen direct Thumb calls/tails to the shared state-name setter."""
from pathlib import Path
import importlib.util,struct,contextlib
h=Path(__file__).resolve().parent
s=importlib.util.spec_from_file_location('n',h/'20261007-queue5-native.py');n=importlib.util.module_from_spec(s);s.loader.exec_module(n)
sec=n.elf.get_section('.text');data=bytes(sec.content);base=sec.virtual_address
md=n.capstone.Cs(n.capstone.CS_ARCH_ARM,n.capstone.CS_MODE_THUMB)
sites=[]
for off in range(0,len(data)-4,2):
 h1,h2=struct.unpack_from('<HH',data,off)
 if h1&0xf800!=0xf000 or h2&0x8000==0:continue
 for i in md.disasm(data[off:off+4],base+off):
  if i.size==4 and i.mnemonic in ('bl','blx','b.w') and i.op_str.startswith('#') and int(i.op_str[1:],16)&~1==0x5c0ca8:
   sites.append(i.address)
rows=[]
with (h/'20261007-q17-state-callers-native.txt').open('w',encoding='utf-8') as out,contextlib.redirect_stdout(out):
 print('ENGINE SHA256',n.hashlib.sha256(n.raw).hexdigest())
 for a in sites:
  f=n.enclosing(a);name=f[3] if f else 'UNKNOWN navigation owner'
  print('CALLER',name);n.dump(a,a+4,True)
  rows.append(f'| 0x{a:08X} | {name} | Shared RC4 state-name setter; caller state-machine belongs its concrete behavior record. |')
(h/'20261007-q17-state-callers-rows.md').write_text('''### Shared state setter caller census (RC8)

Every row decodes the actual call/tail instruction, rather than treating an old caller count as proof. Owner names are navigation labels; closure/thunk labels can be imprecise. RC4 establishes the callee's behavior; this table identifies callers, not their entire state machines. Concrete M15 activity/freeplay state decisions remain HIGHER-LAYER; M7's reaction state decisions are covered by their quoted records. No generic state transition is invented for an unbuilt class. Host: Behavior/IBehavior.cs SetStateName; concrete transitions stay in their own behavior classes.

| Instruction | Navigation owner | Ownership |
|---|---|---|
'''+ '\n'.join(rows)+'\n',encoding='utf-8')
print('Reopened',len(sites),'direct calls/tails')
