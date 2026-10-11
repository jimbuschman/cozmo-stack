"""Q14 rate-frame: local stack pointer slot uses; never global writer/alias absence."""
from pathlib import Path
import importlib.util,sys,json,hashlib,collections
sys.argv.append('--light')
b=Path(__file__).resolve().parent
spec=importlib.util.spec_from_file_location('native',b/'20261007-queue5-native.py');n=importlib.util.module_from_spec(spec);spec.loader.exec_module(n)
from capstone.arm import *
# Entry boundaries reopened in candidate-owner-entries-native; sizes only bound navigation.
ranges=[(0x9D5934,0x9D66EC)]
md=n.capstone.Cs(n.capstone.CS_ARCH_ARM,n.capstone.CS_MODE_ARM);md.detail=True
functions=[];allcode={};literalusers={}
for start,end in ranges:
 work=[(start,None)];seen={};insts={}
 while work:
  a,pred=work.pop()
  if a in seen or not(start<=a<end) or a%4:continue
  i=next(md.disasm(n.read(a,4),a),None)
  if not i or i.size!=4:continue
  seen[a]=pred;insts[a]=i
  for o in i.operands:
   if o.type==ARM_OP_MEM and o.mem.base==ARM_REG_PC and not o.mem.index:
    loc=i.address+8+o.mem.disp
    literalusers.setdefault(loc,[]).append(hex(a))
  conditional=i.cc not in (ARM_CC_AL,ARM_CC_INVALID)
  if i.id==ARM_INS_B:
   if i.operands and i.operands[0].type==ARM_OP_IMM:work.append((i.operands[0].imm,a))
   if conditional:work.append((a+4,a))
   continue
  if i.id in (ARM_INS_BL,ARM_INS_BLX):work.append((a+4,a));continue
  # No unknown indirect transfers are followed. Conditional returns retain fallthrough.
  writespc=ARM_REG_PC in i.regs_access()[1]
  pcreg=any(o.type==ARM_OP_REG and o.reg==ARM_REG_PC for o in i.operands)
  terminal=i.id in (ARM_INS_BX,ARM_INS_UDF) or writespc or (i.id in (ARM_INS_POP,ARM_INS_LDM,ARM_INS_LDMDB,ARM_INS_LDMIB) and pcreg)
  if not terminal or conditional:work.append((a+4,a))
 rows=[]
 for a in sorted(insts):
  i=insts[a];row={'address':hex(a),'bytes':i.bytes.hex(),'instruction':i.mnemonic+' '+i.op_str,'predecessor':hex(seen[a]) if seen[a] is not None else None}
  rows.append(row);allcode[a]=(start,row)
 functions.append({'entry':hex(start),'navigationEnd':hex(end),'instructions':rows})

rows=[]
for fun in functions:
 for row in fun['instructions']:
  a=int(row['address'],16);i=insts[a]
  # Selection only. Exact instruction/byte records retain all stack-address and SP-change frontiers.
  mem18=any(o.type==ARM_OP_MEM and o.mem.base==ARM_REG_SP and 0x14<=o.mem.disp<=0x1C for o in i.operands)
  stackaddr=i.id in (ARM_INS_ADD,ARM_INS_SUB,ARM_INS_MOV) and any(o.type==ARM_OP_REG and o.reg==ARM_REG_SP for o in i.operands[1:])
  spchange=ARM_REG_SP in i.regs_access()[1]
  if mem18 or stackaddr or spchange:rows.append(dict(row,nearSlot18=mem18,stackAddressFrontier=stackaddr,SPWrite=spchange))
r={'engineSHA256':hashlib.sha256(n.raw).hexdigest(),'limit':'Finite local control-flow graph from primary entry; stack-SP/alias frontiers selected, no callee descent or global alias absence.','stackFrontiers':rows,'localFunctions':functions}
(b/'20261010-sound-rate-stack-alias-local-frontiers.json').write_text(json.dumps(r,indent=2)+'\n',encoding='utf-8',newline='\n')
with (b/'20261010-sound-rate-stack-alias-local-native.txt').open('w',encoding='utf-8',newline='\n') as f:
 f.write('ENGINE SHA256 '+r['engineSHA256']+'\nLOCAL CONTROL BODY, callee interfaces only; no global input/alias absence\n')
 for fun in functions:
  f.write('ENTRY '+fun['entry']+' navigation bound '+fun['navigationEnd']+'\n')
  for i in fun['instructions']:f.write(i['address']+' '+i['bytes']+' '+i['instruction']+' predecessor '+str(i['predecessor'])+'\n')
print('instruction nodes',len(functions[0]['instructions']));print(json.dumps(rows,indent=2))

# Reconstruct all local edges, not only the saved predecessor tree, for initialization dominance.
mp={int(r['address'],16):r for r in functions[0]['instructions']};edges={};indexed=[]
for a,row in mp.items():
 i=next(md.disasm(bytes.fromhex(row['bytes']),a));cond=i.cc not in (ARM_CC_AL,ARM_CC_INVALID);succ=[]
 if i.id==ARM_INS_B:
  succ.append(i.operands[0].imm)
  if cond:succ.append(a+4)
 elif i.id in (ARM_INS_BL,ARM_INS_BLX):succ.append(a+4)
 else:
  terminal=i.id in (ARM_INS_BX,ARM_INS_UDF) or ARM_REG_PC in i.regs_access()[1] or i.id in (ARM_INS_POP,ARM_INS_LDM,ARM_INS_LDMDB,ARM_INS_LDMIB) and any(o.type==ARM_OP_REG and o.reg==ARM_REG_PC for o in i.operands)
  if not terminal or cond:succ.append(a+4)
 edges[a]=succ
 for o in i.operands:
  if o.type==ARM_OP_MEM and o.mem.base==ARM_REG_SP and o.mem.index:indexed.append(row)
work=[0x9D5934];seen=set()
while work:
 a=work.pop()
 if a in seen or a==0x9D5B64:continue
 seen.add(a);work.extend(x for x in edges.get(a,[]) if x in mp)
reads=(0x9D5C4C,0x9D5E50,0x9D6180)
check={'scope':'Finite local control-flow path check only; calls retained at original interfaces. Not all runtime/global aliases.','dominatesReads':{hex(a):a not in seen for a in reads},'SPIndexedMemory':indexed,'outOfGraphSuccessors':{hex(a):[hex(x) for x in xx if x not in mp] for a,xx in edges.items() if any(x not in mp for x in xx)}}
(b/'20261010-sound-rate-stack-alias-control-path-check.json').write_text(json.dumps(check,indent=2)+'\n',encoding='utf-8',newline='\n')
print(json.dumps(check,indent=2))
