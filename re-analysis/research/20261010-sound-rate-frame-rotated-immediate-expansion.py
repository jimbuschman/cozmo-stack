"""Q14 rate/frame ARM rotated-immediate constrained pointer navigation. Never global indirect/alias absence proof."""
from pathlib import Path
import importlib.util,sys,struct,hashlib,json
sys.argv.append('--light')
spec=importlib.util.spec_from_file_location('native',Path(__file__).with_name('20261007-queue5-native.py'));n=importlib.util.module_from_spec(spec);spec.loader.exec_module(n)
from capstone.arm import *
lo,hi=0x105243C,0x1052450
qlo,qhi=0x108DF90,0x108DF9C
gotlo,gothi=0x103FF00,0x1040400
print('ENGINE SHA256',hashlib.sha256(n.raw).hexdigest())
print('NAVIGATION ONLY: ARM/Thumb PC literals and selected MOVW/MOVT propagation; 768-byte local possible-branch windows, conditional instructions possible, call-clobbers; actual bodies must be reopened.')
R=lambda a: lo<=a<hi or qlo<=a<qhi
seeds=0;events={}
def predicate_mask(cc):
 def accepts(bits):
  N=bool(bits&8);Z=bool(bits&4);C=bool(bits&2);V=bool(bits&1)
  return {ARM_CC_EQ:Z,ARM_CC_NE:not Z,ARM_CC_HS:C,ARM_CC_LO:not C,ARM_CC_MI:N,ARM_CC_PL:not N,ARM_CC_VS:V,ARM_CC_VC:not V,ARM_CC_HI:C and not Z,ARM_CC_LS:not C or Z,ARM_CC_GE:N==V,ARM_CC_LT:N!=V,ARM_CC_GT:not Z and N==V,ARM_CC_LE:Z or N!=V}.get(cc,True)
 return sum(1<<bits for bits in range(16) if accepts(bits))

def execute(data,pos,a,thumb):
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
 work=[(a,{},0xffff)];visited=set();steps=0
 while work and steps<4096:
  addr,env,flags=work.pop();key=(addr,tuple(sorted(env.items())),flags)
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

  if mn.startswith('pop') and any(o.type==ARM_OP_REG and o.reg==ARM_REG_PC for o in ops):
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
     imm=((w>>4)&0xf000)|(w&0xfff);seed=imm in range(0x243C,0x2450) or imm in range(0xDF90,0xDF9C) or imm in range(0xFF00,0x10000) or imm<0x400
   else:
    h=struct.unpack_from('<H',data,j)[0]
    if h&0xF800==0x4800:loc=((a+4)&~3)+((h&0xff)<<2)
    elif h in (0xF8DF,0xF85F):loc=((a+4)&~3)+(struct.unpack_from('<H',data,j+2)[0]&0xfff)*(1 if h==0xF8DF else -1)
    elif h&0xFBF0==0xF240:
     half=struct.unpack_from('<H',data,j+2)[0]
     imm=((h&0xf)<<12)|((h&0x400)<<1)|((half&0x7000)>>4)|(half&0xff)
     seed=imm in range(0x243C,0x2450) or imm in range(0xDF90,0xDF9C) or imm in range(0xFF00,0x10000) or imm<0x400
     if seed:execute(data,j,a,thumb)
     continue
    else:continue
    try:literal=n.word(loc)
    except ValueError:continue
   if literal is not None:
    base=(a+(4 if thumb else 8)+literal)&0xffffffff
    seed=gotlo-192<=base<gothi or lo-192<=base<hi or qlo-192<=base<qhi or gotlo<=literal<gothi or R(literal)
   if seed:execute(data,j,a,thumb)
print('processed candidate windows',seeds)
for key,item in sorted(events.items()):
 item['seeds']=sorted(set(item['seeds']));print(json.dumps(item,separators=(',',':')))
print('LIMIT: encoding/navigation local windows only; mixed-mode false candidates, shifted index, unmodelled branch flags/loop bounds, path merge, stack aliases, external mutation, computed/indirect inputs and pointer escape need primary checks. No exhaustive runtime writer absence.')
