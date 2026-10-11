"""Bounded Q14-114 curve evaluator A14E28 pointer-construction navigation. Never global indirect/alias absence proof."""
from pathlib import Path
import importlib.util,sys,struct,hashlib,json
sys.argv.append('--light')
spec=importlib.util.spec_from_file_location('native',Path(__file__).with_name('20261007-queue5-native.py'));n=importlib.util.module_from_spec(spec);spec.loader.exec_module(n)
from capstone.arm import *
import lief
elf=lief.parse(str(n.path))
n.loads=[(x.virtual_address,x.size,x.offset) for x in elf.sections if x.has(lief.ELF.Section.FLAGS.EXECINSTR)]
targets=(0xA14E28,)
lowwords={x&0xffff for x in targets}|{(x|1)&0xffff for x in targets}
gotlo,gothi=0x103FF00,0x1040400
print('ENGINE SHA256',hashlib.sha256(n.raw).hexdigest())
print('NAVIGATION ONLY: ARM/Thumb PC literals and selected MOVW/MOVT propagation; 768-byte local possible-branch windows, conditional instructions possible, call-clobbers; actual bodies must be reopened.')
R=lambda a: (a&~1) in targets
seeds=0;events={}
def execute(data,pos,a,thumb):
 global seeds
 print(f'candidate-window {a:08X} '+('Thumb' if thumb else 'ARM'))
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
 work=[(a,{})];visited=set();steps=0
 while work and steps<4096:
  addr,env=work.pop();key=(addr,tuple(sorted(env.items())))
  if key in visited or addr not in instructions:continue
  visited.add(key);steps+=1;ins=instructions[addr]
  ops=ins.operands;pending={};mn=ins.mnemonic.split('.')[0]
  if mn.startswith('pop') and any(o.type==ARM_OP_REG and o.reg==ARM_REG_PC for o in ops):
   if ins.cc not in (ARM_CC_AL,ARM_CC_INVALID):work.append((ins.address+ins.size,env.copy()))
   continue
  if (mn.startswith('ldr') or mn.startswith('vldr')) and len(ops)>=2 and ops[1].type==ARM_OP_MEM:
   mem=ops[1].mem;b=(ins.address+(4 if thumb else 8)) if mem.base==ARM_REG_PC else env.get(mem.base)
   if thumb and mem.base==ARM_REG_PC:b&=~3
   idx=env.get(mem.index) if mem.index else 0
   if b is not None and idx is not None and not ops[1].shift.type:
    addr=(b+mem.disp+idx)&0xffffffff
    if R(addr):emit(ins,'read',addr)
    if mem.base==ARM_REG_PC or gotlo<=addr<gothi:
     try:pending[ops[0].reg]=n.word(addr)
     except ValueError:pass
     if gotlo<=addr<gothi and R(pending.get(ops[0].reg,0)):emit(ins,'GOT-pointer',pending[ops[0].reg])
  elif (mn.startswith('str') or mn.startswith('vstr')):
   for op in ops:
    if op.type!=ARM_OP_MEM:continue
    b=env.get(op.mem.base);idx=env.get(op.mem.index) if op.mem.index else 0
    if b is not None and idx is not None and not op.shift.type:
     addr=(b+op.mem.disp+idx)&0xffffffff
     if R(addr):emit(ins,'write',addr)
  elif mn in ('add','sub','adr') and ops and ops[0].type==ARM_OP_REG:
   vv=[val(o,ins) for o in ops[1:]]
   if len(vv)==1 and mn=='adr':vv=[((ins.address+4)&~3) if thumb else ins.address+8,vv[0]]
   elif len(vv)==1:vv=[env.get(ops[0].reg),vv[0]]
   if len(vv)==2 and all(x is not None for x in vv):pending[ops[0].reg]=(vv[0]+(-vv[1] if mn=='sub' else vv[1]))&0xffffffff
  elif mn in ('mov','movw') and len(ops)==2:
   v=val(ops[1],ins)
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
  if mn.startswith('b') and mn not in ('bl','blx','bic','bfi','bfc') and ops and ops[0].type==ARM_OP_IMM:
   work.append((ops[0].imm,env.copy()))
   if ins.cc in (ARM_CC_AL,ARM_CC_INVALID):continue
  if mn in ('bx',) or mn=='pop' and any(o.type==ARM_OP_REG and o.reg==ARM_REG_PC for o in ops):continue
  work.append((ins.address+ins.size,env.copy()))
 seeds+=1
for va,size,off in n.loads:
 # The executable LOAD begins before the PLT. Restrict to shipped executable address region.
 if va>=0xF00000 or va+size<=0x4A0000:continue
 data=n.read(va,size);start=max(0,0x4A0000-va)
 for thumb,step in ((False,4),(True,2)):
  for j in range((start+step-1)//step*step,size-196,step):
   a=va+j;literal=None;seed=False
   if not thumb:
    w=struct.unpack_from('<I',data,j)[0]
    if w&0x0F7F0000==0x051F0000:
     loc=a+8+(w&0xfff)*(1 if w&(1<<23) else -1)
     try:literal=n.word(loc)
     except ValueError:continue
    elif w&0x0FF00000==0x03000000:
     imm=((w>>4)&0xf000)|(w&0xfff);seed=imm in lowwords
   else:
    h=struct.unpack_from('<H',data,j)[0]
    if h&0xF800==0x4800:loc=((a+4)&~3)+((h&0xff)<<2)
    elif h in (0xF8DF,0xF85F):loc=((a+4)&~3)+(struct.unpack_from('<H',data,j+2)[0]&0xfff)*(1 if h==0xF8DF else -1)
    else:continue
    try:literal=n.word(loc)
    except ValueError:continue
   if literal is not None:
    base=(a+(4 if thumb else 8)+literal)&0xffffffff
    seed=any(t-512<=base<t+2 or t-512<=literal<t+2 for t in targets)
   if seed:execute(data,j,a,thumb)
print('processed candidate windows',seeds)
for key,item in sorted(events.items()):
 item['seeds']=sorted(set(item['seeds']));print(json.dumps(item,separators=(',',':')))
print('LIMIT: encoding/navigation local windows only; mixed-mode false candidates, shifted index, unmodelled branch flags/loop bounds, path merge, stack aliases, external mutation, computed/indirect inputs and pointer escape need primary checks. No exhaustive runtime writer absence.')
