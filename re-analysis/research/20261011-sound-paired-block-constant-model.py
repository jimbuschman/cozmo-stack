"""Navigation-only extension of812: paired/block constant loads, no mutable-memory values.
Original LOAD maps, seed enumeration, flags and finite-worklist limits are retained.
"""
from pathlib import Path
import hashlib

BASELINE = Path(__file__).with_name('20261010-sound-weight-formed-pointer-navigation.py')
BASELINE_SHA = '917ad4ba4a8d896fba5993581c99af2dcfcf98062e234c7c930724f07aa9c5cf'

def build_source(targets, controls=False):
    assert hashlib.sha256(BASELINE.read_bytes()).hexdigest() == BASELINE_SHA
    source = BASELINE.read_text(encoding='utf-8')
    marker = "  if (mn.startswith('ldr') or mn.startswith('vldr')) and len(ops)>=2 and ops[1].type==ARM_OP_MEM:"
    assert source.count(marker) == 1
    extension = '''  if mn=='ldrd' and len(ops)==3 and all(o.type==ARM_OP_REG for o in ops[:2]) and ops[2].type==ARM_OP_MEM and not ins.writeback:
   mem=ops[2].mem
   b=((ins.address+4)&~3) if thumb and mem.base==ARM_REG_PC else ins.address+8 if mem.base==ARM_REG_PC else env.get(mem.base)
   # Only unindexed immediate forms are added; post-index/writeback remain unknown.
   if b is not None and not mem.index and not ops[2].shift.type:
    first=(b+mem.disp)&0xffffffff
    for j,op in enumerate(ops[:2]):
     loc=(first+4*j)&0xffffffff
     for byte in range(4):
      if R((loc+byte)&0xffffffff):emit(ins,'paired-read-overlap',(loc+byte)&0xffffffff)
     if mem.base==ARM_REG_PC or gotlo<=loc and loc+4<=gothi:
      try:pending[op.reg]=int.from_bytes(n.read(loc,4),'little')
      except ValueError:pass
      if gotlo<=loc<gothi and R(pending.get(op.reg,0)):emit(ins,'paired-GOT-pointer',pending[op.reg])
  elif mn in ('ldm','ldmia','ldmib','ldmdb') and len(ops)>=2 and all(o.type==ARM_OP_REG for o in ops) and '^' not in ins.op_str:
   base=ops[0].reg;regs=[o.reg for o in ops[1:]];b=env.get(base)
   # Base in the list is legal without writeback (actual9B7EE8); never guess unpredictable writeback.
   if b is not None and base!=ARM_REG_PC and not(ins.writeback and base in regs):
    count=len(regs)
    delta=4 if mn=='ldmib' else -4*count if mn=='ldmdb' else 0
    first=(b+delta)&0xffffffff
    for j,reg in enumerate(regs):
     loc=(first+4*j)&0xffffffff
     for byte in range(4):
      if R((loc+byte)&0xffffffff):emit(ins,'block-read-overlap',(loc+byte)&0xffffffff)
     # Do not read initial BSS, stack, object or heap as a runtime constant.
     if gotlo<=loc and loc+4<=gothi:
      try:pending[reg]=int.from_bytes(n.read(loc,4),'little')
      except ValueError:pass
      if R(pending.get(reg,0)):emit(ins,'block-GOT-pointer',pending[reg])
    if ins.writeback:pending[base]=(b+(-4*count if mn=='ldmdb' else 4*count))&0xffffffff
  elif (mn.startswith('ldr') or mn.startswith('vldr')) and len(ops)>=2 and ops[1].type==ARM_OP_MEM:'''
    source = source.replace(marker, extension)
    source = source.replace('targets=(0xA08024,)', 'targets='+repr(tuple(targets)))
    source = source.replace('one original weight-setter input', 'original factory/key inputs with paired/block constant-load extension')
    signature = 'def execute(data,pos,a,thumb,initial_env=None):'
    assert source.count(signature)==1
    source = source.replace(signature, 'def execute(data,pos,a,thumb,initial_env=None,observer=None):')
    update = '  env.update(pending)\n'
    assert source.count(update)==1
    source = source.replace(update, update+"  if observer is not None:observer(ins,env.copy())\n  if mn.startswith('ldm') and any(o.type==ARM_OP_REG and o.reg==ARM_REG_PC for o in ops[1:]):continue\n")
    if controls:
        source=source.split('for va,size,off in exec_sections:')[0]
    return source
