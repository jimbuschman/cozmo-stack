"""Reopen documented lock sites and static tables from the shipped ELF."""
from pathlib import Path
import importlib.util,re,contextlib
h=Path(__file__).resolve().parent
s=importlib.util.spec_from_file_location('native',h/'20261007-queue5-native.py')
n=importlib.util.module_from_spec(s);s.loader.exec_module(n)
lead=(h/'20261002-R-BEH2-M7-gap1-extraction.md').read_text(encoding='utf-8')
rows=[]
with (h/'20261007-q17-lock-tables-native.txt').open('w',encoding='utf-8') as out,contextlib.redirect_stdout(out):
 print('ENGINE SHA256',n.hashlib.sha256(n.raw).hexdigest())
 for line in lead.splitlines():
  if not line.startswith('| IBehavior::SmartDisable |') and not line.startswith('| IActivity::SmartDisable |'):continue
  p=[x.strip() for x in line.split('|')[1:-1]]
  site=int(p[2],16);print('SITE',p[1]);n.dump(site-0x20,site+4,True)
  if not p[3].startswith('0x'):continue
  addr=int(p[3],16);raw=n.read(addr,42)
  pairs=[(raw[i*2],raw[i*2+1]) for i in range(21)]
  print(f'TABLE {addr:08X}',raw.hex(),pairs)
  assert [x[0] for x in pairs]==list(range(21)),(p[1],pairs)
  mask=''.join(str(x[1]) for x in pairs)
  rows.append(f'| {p[1]} | 0x{site:08X} | 0x{addr:08X} | {mask} |')
 n.dump(0x5a3a48,0x5a3b9e,True)
 n.dump(0x5a4de0,0x5a4e60,True)
 n.dump(0x5a0404,0x5a0414,True)
(h/'20261007-q17-lock-table-rows.md').write_text('''### Reopened reaction-lock table census

Native companion `20261007-q17-lock-tables-native.txt`. Each row reads all42
bytes as21(enum-byte,bool-byte) pairs; every enum ordinal is checked0..20.
Mask columns list stored boolean bytes, not an inferred trigger set.
IActivity sites are M15 owners; their table values are supplied interface data.
Use the caller's own name/scope and install point, not a blanket arbiter mask.

| Caller | Install call | Static table | Bool bytes in trigger ordinal order |
|---|---|---|---|
'''+ '\n'.join(rows)+'\n',encoding='utf-8')
print('Read',len(rows),'static caller rows')
