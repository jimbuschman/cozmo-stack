"""Q14 original rate/frame aliases: stored/returned values from existing observation seeds only; no admission/absence proof. Never global indirect/alias absence proof."""
from pathlib import Path
import importlib.util,sys,struct,hashlib,json
from collections import deque
sys.argv.append('--light')
spec=importlib.util.spec_from_file_location('native',Path(__file__).with_name('20261007-queue5-native.py'));n=importlib.util.module_from_spec(spec);spec.loader.exec_module(n)
from capstone.arm import *
import lief
elf=lief.parse(str(n.path))
# Preserve n.loads for data reads; restrict only scan seeds to executable sections.
exec_sections=[(x.virtual_address,x.size,x.offset) for x in elf.sections if x.has(lief.ELF.Section.FLAGS.EXECINSTR)]
targets=tuple(range(0x105243C,0x1052450))+tuple(range(0x108DF90,0x108DF9C))
lowwords={x&0xFFFF for x in targets}|{(x|1)&0xFFFF for x in targets}
gotsections=[x for x in elf.sections if x.name in ('.got','.got.plt')]
gotlo=min(x.virtual_address for x in gotsections);gothi=max(x.virtual_address+x.size for x in gotsections)
R=lambda a:a in targets
print('ENGINE SHA256',hashlib.sha256(n.raw).hexdigest())
print('NAVIGATION ONLY: existing rate/frame observation seeds only; formed/stored/returned pointer values and indirect target candidates; original LOAD data reads; executable-section seed enumeration; full ELF GOT bounds; Thumb/ARM MOVW, flags, width-aware loads, fair local worklist; no runtime binding/input absence.')
print('GOT BOUNDS',hex(gotlo),hex(gothi))
seeds=0;events={}
def predicate_mask(cc):
 def accepts(bits):
  N=bool(bits&8);Z=bool(bits&4);C=bool(bits&2);V=bool(bits&1)
  return {ARM_CC_EQ:Z,ARM_CC_NE:not Z,ARM_CC_HS:C,ARM_CC_LO:not C,ARM_CC_MI:N,ARM_CC_PL:not N,ARM_CC_VS:V,ARM_CC_VC:not V,ARM_CC_HI:C and not Z,ARM_CC_LS:not C or Z,ARM_CC_GE:N==V,ARM_CC_LT:N!=V,ARM_CC_GT:not Z and N==V,ARM_CC_LE:Z or N!=V}.get(cc,True)
 return sum(1<<bits for bits in range(16) if accepts(bits))

def execute(data,pos,a,thumb,initial_env=None):
 global seeds
 md=n.capstone.Cs(n.capstone.CS_ARCH_ARM,n.capstone.CS_MODE_THUMB if thumb else n.capstone.CS_MODE_ARM);md.detail=True
 env={};seen=False
 def val(op,ins):
  if op.type==ARM_OP_IMM:return op.imm&0xffffffff
  if op.type==ARM_OP_REG:
   if op.reg==ARM_REG_PC:return ins.address+(4 if thumb else 8)
   if op.shift.type:return None
   return env.get(op.reg)
 def emit(ins,kind,addr):
  key=(ins.address,thumb,kind,addr);events.setdefault(key,{'address':f'{ins.address:08X}','mode':'Thumb' if thumb else 'ARM','kind':kind,'effective':f'{addr:08X}','instruction':ins.mnemonic+' '+ins.op_str,'seeds':[]})['seeds'].append(f'{a:08X}')
 instructions={i.address:i for i in md.disasm(data[pos:pos+768],a)}
 work=deque([(a,(initial_env or {}).copy(),0xffff)]);visited=set();steps=0
 while work and steps<4096:
  addr,env,flags=work.popleft();key=(addr,tuple(sorted(env.items())),flags)
  if key in visited or addr not in instructions:continue
  visited.add(key);steps+=1;ins=instructions[addr]
  ops=ins.operands;pending={};mn=ins.mnemonic.split('.')[0]
  if ins.cc not in (ARM_CC_AL,ARM_CC_INVALID):
   pm=predicate_mask(ins.cc);skipped=flags&(~pm&0xffff);executed=flags&pm
   if skipped:work.append((ins.address+ins.size,env.copy(),skipped))
   if not executed:continue
   flags=executed
   suffix={ARM_CC_EQ:'eq',ARM_CC_NE:'ne',ARM_CC_HS:'hs',ARM_CC_LO:'lo',ARM_CC_MI:'mi',ARM_CC_PL:'pl',ARM_CC_VS:'vs',ARM_CC_VC:'vc',ARM_CC_HI:'hi',ARM_CC_LS:'ls',ARM_CC_GE:'ge',ARM_CC_LT:'lt',ARM_CC_GT:'gt',ARM_CC_LE:'le'}.get(ins.cc,'')
   if suffix and mn.endswith(suffix):mn=mn[:-len(suffix)]

  # Capstone's instruction ID keeps the base operation while update_flags
  # preserves the S-bit. Normalize only these explicit spelling/ID pairs.
  if mn == 'adds' and ins.id == ARM_INS_ADD:mn='add'
  elif mn == 'subs' and ins.id == ARM_INS_SUB:mn='sub'
  elif mn == 'movs' and ins.id == ARM_INS_MOV:mn='mov'

  # Primary CBZ/CBNZ opcode witnesses are preserved at807; unchanged NZCV.
  if ins.id in (ARM_INS_CBZ,ARM_INS_CBNZ) and len(ops)==2:
   tested=val(ops[0],ins)
   taken=None if tested is None else ((tested==0) if ins.id==ARM_INS_CBZ else (tested!=0))
   if taken is not False:work.append((ops[1].imm,env.copy(),flags))
   if taken is not True:work.append((ins.address+ins.size,env.copy(),flags))
   continue

  if mn.startswith('pop') and any(o.type==ARM_OP_REG and o.reg==ARM_REG_PC for o in ops):
   if not any(o.type==ARM_OP_REG and o.reg==ARM_REG_R0 for o in ops) and R(env.get(ARM_REG_R0,0)):emit(ins,'possible-returned-pointer',env[ARM_REG_R0])
   continue
  if mn=='bx' and ops and ops[0].type==ARM_OP_REG and ops[0].reg==ARM_REG_LR and R(env.get(ARM_REG_R0,0)):
   emit(ins,'possible-returned-pointer',env[ARM_REG_R0])
  if mn in ('str','strd'):
   for op in ops[:2 if mn=='strd' else 1]:
    if op.type==ARM_OP_REG and R(env.get(op.reg,0)):emit(ins,'possible-stored-pointer-value',env[op.reg])
  if mn.startswith('stm') or mn=='push':
   for op in (ops if mn=='push' else ops[1:]):
    if op.type==ARM_OP_REG and R(env.get(op.reg,0)):emit(ins,'possible-block-stored-pointer-value',env[op.reg])

  if (mn.startswith('ldr') or mn.startswith('vldr')) and len(ops)>=2 and ops[1].type==ARM_OP_MEM:
   mem=ops[1].mem;b=(ins.address+(4 if thumb else 8)) if mem.base==ARM_REG_PC else env.get(mem.base)
   if thumb and mem.base==ARM_REG_PC:b&=~3
   idx=env.get(mem.index) if mem.index else 0
   if b is not None and idx is not None and not ops[1].shift.type:
    addr=(b+mem.disp+idx)&0xffffffff
    if R(addr):emit(ins,'read',addr)
    if mem.base==ARM_REG_PC or gotlo<=addr<gothi:
     # Only modeled transfer widths enter the address-value environment.
     width=1 if mn in ('ldrb','ldrsb') else 2 if mn in ('ldrh','ldrsh') else 4 if mn=='ldr' or mn=='vldr' and ins.reg_name(ops[0].reg).startswith('s') else None
     if width is not None:
      try:
       value=int.from_bytes(n.read(addr,width),'little',signed=mn in ('ldrsb','ldrsh'))
       pending[ops[0].reg]=value&0xffffffff
      except ValueError:pass
     if gotlo<=addr<gothi and R(pending.get(ops[0].reg,0)):emit(ins,'GOT-pointer',pending[ops[0].reg])
  elif mn.startswith(('vst1','vst2','vst3','vst4')):
   memops=[o for o in ops if o.type==ARM_OP_MEM]
   regops=[o for o in ops if o.type==ARM_OP_REG and ins.reg_name(o.reg).startswith(('d','q'))]
   if memops:
    b=env.get(memops[0].mem.base)
    # Full-register forms only; lane syntax requires a separate exact width check.
    lane='[' in ins.op_str.split('}')[0]
    if b is not None and regops and not lane:
     size=sum(16 if ins.reg_name(o.reg).startswith('q') else 8 for o in regops)
     for off in range(size):
      dst=(b+off)&0xffffffff
      if R(dst):
       emit(ins,'NEON-full-register-overlap',dst)
       break
  elif mn.startswith(('stm','vstm')) and ops and ops[0].type==ARM_OP_REG:
   b=env.get(ops[0].reg)
   regs=[o for o in ops[1:] if o.type==ARM_OP_REG]
   widths=[8 if ins.reg_name(o.reg).startswith('d') else 4 for o in regs]
   size=sum(widths)
   decrement='db' in mn or 'da' in mn
   before='ib' in mn or 'db' in mn
   unit=widths[0] if widths else 4
   if b is not None and size:
    first=b-size+(0 if before else unit) if decrement else b+(unit if before else 0)
    for off in range(size):
     dst=(first+off)&0xffffffff
     if R(dst):
      emit(ins,'block-write-overlap',dst)
      break
  elif (mn.startswith('str') or mn.startswith('vstr')):
   for op in ops:
    if op.type!=ARM_OP_MEM:continue
    b=env.get(op.mem.base);idx=env.get(op.mem.index) if op.mem.index else 0
    if b is not None and idx is not None and not op.shift.type:
     addr=(b+op.mem.disp+idx)&0xffffffff
     if R(addr):emit(ins,'write',addr)
     else:
      # Preserve existing start-address events; add only writes whose byte span crosses into a target.
      destregs=[o for o in ops if o.type==ARM_OP_REG]
      width=8 if mn.startswith('strd') or mn.startswith('vstr') and destregs and ins.reg_name(destregs[0].reg).startswith('d') else 1 if mn.startswith('strb') else 2 if mn.startswith('strh') else 4
      for byte in range(1,width):
       dst=(addr+byte)&0xffffffff
       if R(dst):
        emit(ins,'scalar-write-overlap',dst)
        break
  elif mn in ('add','sub','adr') and ops and ops[0].type==ARM_OP_REG:
   vv=[val(o,ins) for o in ops[1:]]
   if not thumb and mn in ('add','sub') and int.from_bytes(ins.bytes,'little')&0x02000000 and len(ops)>=3:
    w=int.from_bytes(ins.bytes,'little');rot=((w>>8)&15)*2;imm=w&255
    if rot:imm=((imm>>rot)|(imm<<(32-rot)))&0xffffffff
    vv=[val(ops[1],ins),imm]
   if len(vv)==1 and mn=='adr':vv=[((ins.address+4)&~3) if thumb else ins.address+8,vv[0]]
   elif len(vv)==1:vv=[env.get(ops[0].reg),vv[0]]
   if len(vv)==2 and all(x is not None for x in vv):pending[ops[0].reg]=(vv[0]+(-vv[1] if mn=='sub' else vv[1]))&0xffffffff
  elif mn in ('mov','movw') and len(ops)>=2:
   v=val(ops[1],ins)
   if not thumb and mn=='mov' and int.from_bytes(ins.bytes,'little')&0x02000000:
    w=int.from_bytes(ins.bytes,'little');rot=((w>>8)&15)*2;v=w&255
    if rot:v=((v>>rot)|(v<<(32-rot)))&0xffffffff
   if v is not None:pending[ops[0].reg]=v
  elif mn=='movt' and len(ops)==2 and ops[0].reg in env:pending[ops[0].reg]=(env[ops[0].reg]&0xffff)|((ops[1].imm&0xffff)<<16)
  elif mn in ('bl','blx'):
   for reg in (ARM_REG_R0,ARM_REG_R1,ARM_REG_R2,ARM_REG_R3):
    if R(env.get(reg,0)):emit(ins,'live-'+ins.reg_name(reg)+'-at-call',env[reg])
  for reg,value in pending.items():
   if R(value):emit(ins,'formed-'+ins.reg_name(reg),value)
  if mn in ('blx','bx') and ops and ops[0].type==ARM_OP_REG and R(env.get(ops[0].reg,0)):emit(ins,'indirect-target',env[ops[0].reg])
  for reg in ins.regs_access()[1]:env.pop(reg,None)
  if mn in ('bl','blx'):
   for reg in (ARM_REG_R0,ARM_REG_R1,ARM_REG_R2,ARM_REG_R3,ARM_REG_R12,ARM_REG_LR):env.pop(reg,None)
  env.update(pending)
  if ins.update_flags or mn in ('bl','blx') or (mn=='vmrs' and 'apsr' in ins.op_str):flags=0xffff
  if mn in ('cmp','cmn') and len(ops)==2:
   pair=[val(o,ins) for o in ops]
   if all(v is not None for v in pair):
    left,right=pair
    result=(left-right if mn=='cmp' else left+right)&0xffffffff
    carry=left>=right if mn=='cmp' else left+right>0xffffffff
    overflow=bool(((left^right) if mn=='cmp' else ~(left^right))&(left^result)&0x80000000)
    bits=(8 if result&0x80000000 else 0)|(4 if result==0 else 0)|(2 if carry else 0)|(1 if overflow else 0)
    flags=1<<bits

  if mn.startswith('b') and mn not in ('bl','blx','bic','bfi','bfc') and ops and ops[0].type==ARM_OP_IMM:
   work.append((ops[0].imm,env.copy(),flags))
   continue
  if mn in ('bx',) or mn=='pop' and any(o.type==ARM_OP_REG and o.reg==ARM_REG_PC for o in ops):continue
  work.append((ins.address+ins.size,env.copy(),flags))
 if steps>=4096:print(f'LIMIT local state cap at {a:08X}')
 seeds+=1
# No new executable sweep: reuse only exact seeds of808/809 target observations.
selected=set()
for name in ('20261010-sound-rate-frame-full-got-navigation.txt','20261010-sound-rate-frame-movw-got-delta-navigation.txt'):
 for line in Path(__file__).with_name(name).read_text(encoding='utf-8').splitlines():
  if not line.startswith('{'):continue
  item=json.loads(line)
  for seed in item['seeds']:selected.add((int(seed,16),item['mode']=='Thumb'))
for a,thumb in sorted(selected):
 segment=next((x for x in n.loads if x[0]<=a<x[0]+x[1]),None)
 assert segment
 va,size,off=segment;execute(n.read(va,size),a-va,a,thumb)
print('selected existing observation seeds',len(selected))
print('processed candidate windows',seeds)
for key,item in sorted(events.items()):
 item['seeds']=sorted(set(item['seeds']));print(json.dumps(item,separators=(',',':')))
print('LIMIT: encoding/navigation local windows only; mixed-mode false candidates, shifted index, unmodelled branch flags/loop bounds, path merge, stack aliases, external mutation, computed/indirect inputs and pointer escape need primary checks. No exhaustive runtime writer absence.')
