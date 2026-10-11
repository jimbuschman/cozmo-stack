"""Q14-033: source boundaries for the128 retained Thumb encodings; never indirect-input absence."""
from pathlib import Path
import importlib.util,sys,json,hashlib,collections
sys.argv.append('--light')
b=Path(__file__).resolve().parent
spec=importlib.util.spec_from_file_location('native',b/'20261007-queue5-native.py');n=importlib.util.module_from_spec(spec);spec.loader.exec_module(n)
from capstone.arm import *
# Entry boundaries reopened in candidate-owner-entries-native; sizes only bound navigation.
ranges=[(0xA542C8,0xA54394),(0xA543A0,0xA54474),(0xA54480,0xA544BC),(0xA544BC,0xA545F0),(0xA545F0,0xA54650),(0xA54650,0xA548B8),(0xA548B8,0xA548C0),(0xA548C0,0xA5495C),(0xA5495C,0xA549A0),(0xA549A0,0xA54A30),(0xA54F1C,0xA55724)]
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
cs=json.loads((b/'20261010-sound-voice-init-interior-callers.json').read_text())['candidates']
cs=[c for c in cs if c['mode']=='Thumb' and int(c['address'],16)>=0x95E540 and int(c['address'],16)%4==2]
rows=[]
for c in cs:
 a=int(c['address'],16);cont=a&~3;v=allcode.get(cont)
 row={'candidate':c,'containingAddress':hex(cont),'rawWord':hex(n.word(cont))}
 if v:row.update(disposition='upper halfword of local ARM boundary linked to decoded entry',ownerEntry=hex(v[0]),primaryInstruction=v[1])
 elif cont in literalusers:row.update(disposition='PC-relative literal word',literalUsers=literalusers[cont])
 else:row.update(disposition='UNKNOWN source boundary')
 rows.append(row)
r={'engineSHA256':hashlib.sha256(n.raw).hexdigest(),'limit':'Local ARM source instruction boundaries only; conservative conditional branches; calls kept at interfaces; computed transfer/input/alias proofs remain open. Navigation extents are not evidence without decoded entry/edge witnesses.','counts':dict(collections.Counter(r['disposition'] for r in rows)),'candidates':rows,'localFunctions':functions}
(b/'20261010-sound-voice-init-candidate-source-boundaries.json').write_text(json.dumps(r,indent=2)+'\n',encoding='utf-8')
with (b/'20261010-sound-voice-init-candidate-source-boundaries-native.txt').open('w',encoding='utf-8',newline='\n') as f:
 f.write('ENGINE SHA256 '+r['engineSHA256']+'\nLOCAL ARM SOURCE BOUNDARIES; no callee descent; not input/alias absence proof\n')
 for fun in functions:
  f.write('ENTRY '+fun['entry']+' navigation bound '+fun['navigationEnd']+'\n')
  for i in fun['instructions']:f.write(i['address']+' '+i['bytes']+' '+i['instruction']+' predecessor '+str(i['predecessor'])+'\n')
print(json.dumps(r['counts']));print(json.dumps([row for row in rows if row['disposition']!='upper halfword of local ARM boundary linked to decoded entry'],indent=2))
