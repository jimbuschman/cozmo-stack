"""Existing11 helper calls only: bounded post-call incoming-register use/kill frontiers."""
from pathlib import Path
import json,importlib.util,sys
sys.argv.append('--light');p=Path(__file__).parent
spec=importlib.util.spec_from_file_location('native',p/'20261007-queue5-native.py');n=importlib.util.module_from_spec(spec);spec.loader.exec_module(n)
from capstone.arm import *
md=n.capstone.Cs(n.capstone.CS_ARCH_ARM,n.capstone.CS_MODE_ARM);md.detail=True
original=json.loads((p/'20261011-sound-rate-frame-live-helper-witness.json').read_text(encoding='utf-8'))['rate_frame_live_observations'];calls={}
for e in original:
 if not e['callee_identity'].startswith('__aeabi_'):continue
 a=int(e['address'],16);reg=ARM_REG_R2 if e['kind']=='live-r2-at-call' else ARM_REG_R3
 calls.setdefault(a,{'helper':e['callee_identity'],'registers':set()})['registers'].add(reg)
def predicate_mask(cc):
 def accepts(bits):
  N=bool(bits&8);Z=bool(bits&4);C=bool(bits&2);V=bool(bits&1)
  return {ARM_CC_EQ:Z,ARM_CC_NE:not Z,ARM_CC_HS:C,ARM_CC_LO:not C,ARM_CC_MI:N,ARM_CC_PL:not N,ARM_CC_VS:V,ARM_CC_VC:not V,ARM_CC_HI:C and not Z,ARM_CC_LS:not C or Z,ARM_CC_GE:N==V,ARM_CC_LT:N!=V,ARM_CC_GT:not Z and N==V,ARM_CC_LE:Z or N!=V}.get(cc,True)
 return sum(1<<bits for bits in range(16) if accepts(bits))


rows=[];lines=[]
for call,item in sorted(calls.items()):
 start=call+4;limit=start+256;work=[(start,frozenset(item['registers']),0xffff,[])];seen=set();frontiers=[];instructions={}
 while work:
  a,pending,flags,chain=work.pop()
  if not pending:frontiers.append({'address':f'{a:08X}','kind':'all tracked values defined before read','path':chain});continue
  key=(a,pending,flags)
  if key in seen:continue
  seen.add(key)
  in_extra=any(lo<=a<hi for lo,hi in ((0x9AFAE0,0x9AFAFC),(0x9AF9E0,0x9AF9F4),(0x9FD788,0x9FD798),(0x9FD890,0x9FD894)))
  if not (start<=a<limit or in_extra):frontiers.append({'address':f'{a:08X}','kind':'outside local bound','pending':[md.reg_name(r) for r in sorted(pending)],'path':chain});continue
  ins=next(md.disasm(n.read(a,4),a,count=1),None)
  if not ins:frontiers.append({'address':f'{a:08X}','kind':'undecoded','path':chain});continue
  reads,writes=ins.regs_access();use=pending&set(reads);new=pending-set(writes);chain=chain+[f'{a:08X}']
  instructions[a]={'address':f'{a:08X}','bytes':ins.bytes.hex(),'instruction':ins.mnemonic+' '+ins.op_str,'reads':[md.reg_name(r) for r in reads],'writes':[md.reg_name(r) for r in writes]}
  if ins.cc not in (ARM_CC_AL,ARM_CC_INVALID):
   pm=predicate_mask(ins.cc);skip=flags&(~pm&0xffff);executed=flags&pm
   if skip:work.append((a+4,pending,skip,chain))
   if not executed:continue
   flags=executed

  if use:frontiers.append({'address':f'{a:08X}','kind':'possible tracked-value read','registers':[md.reg_name(r) for r in sorted(use)],'instruction':ins.mnemonic+' '+ins.op_str,'path':chain});continue
  if ins.id in (ARM_INS_BL,ARM_INS_BLX):frontiers.append({'address':f'{a:08X}','kind':'call with tracked value still live','pending':[md.reg_name(r) for r in sorted(new)],'instruction':ins.mnemonic+' '+ins.op_str,'path':chain});continue
  if ins.update_flags or (ins.id==ARM_INS_VMRS and 'apsr' in ins.op_str):flags=0xffff
  if ins.id==ARM_INS_B and ins.operands and ins.operands[0].type==ARM_OP_IMM:work.append((ins.operands[0].imm,frozenset(new),flags,chain));continue
  terminal=ins.id in (ARM_INS_BX,ARM_INS_UDF) or ARM_REG_PC in writes
  if terminal:frontiers.append({'address':f'{a:08X}','kind':'terminal before tracked-value read','pending':[md.reg_name(r) for r in sorted(new)],'instruction':ins.mnemonic+' '+ins.op_str,'path':chain});continue
  work.append((a+4,frozenset(new),flags,chain))
 rows.append({'call':f'{call:08X}','helper':item['helper'],'tracked_registers':[md.reg_name(r) for r in sorted(item['registers'])],'instructions':[instructions[a] for a in sorted(instructions)],'frontiers':frontiers,'limit':'Local256-byte graph plus4 primary reopened branch-entry slices; consistent NZCV predicates; calls,out-of-bound/indirect/terminal instructions stop; no helper body or global alias absence proof.'})
 lines.append(f'{call:08X} {item["helper"]}')
 for a in sorted(instructions):
  x=instructions[a];lines.append(x['address']+' '+x['bytes']+' '+x['instruction'])
 for x in frontiers:lines.append('FRONTIER '+json.dumps(x,separators=(',',':')))
 lines.append('')
r={'scope':'Existing11 helper calls /12 rate-frame register observations only.','rows':rows}
Path(__file__).with_suffix('.json').write_text(json.dumps(r,indent=2)+'\n',encoding='utf-8')
Path(__file__).with_suffix('.txt').write_text('\n'.join(lines)+'\n',encoding='utf-8')
for x in rows:print(x['call'],x['helper'],[(y['address'],y['kind']) for y in x['frontiers']])
