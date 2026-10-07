"""Independent navigation for constant-address vtable loads (more PIC idioms).
Not a CFG or dynamic-alias proof. Static ELF base zero; relocations are resolved
for reads. Wrong-state/index-only bodies and literal pools require inspection.
"""
from pathlib import Path
import lief, capstone, struct, json
from capstone.arm import *
root=Path(__file__).resolve().parents[2]
binary=root/'resources/lib/armeabi-v7a/libcozmoEngine.so'
elf=lief.parse(str(binary)); raw=binary.read_bytes()
loads=[s for s in elf.segments if s.type==lief.ELF.Segment.TYPE.LOAD]
functions={}
for s in elf.symbols:
    if s.value and s.size and s.type==lief.ELF.Symbol.TYPE.FUNC:
        functions.setdefault((s.value&~1,bool(s.value&1)),(s.size,s.name,'symbol'))
for line in (root/'re-analysis/decomp/libcozmoEngine/index.tsv').read_text(encoding='utf-8').splitlines():
    f=line.split('\t')
    if len(f)!=5 or f[2]=='THUNK':continue
    try:a=int(f[0],16);size=int(f[1])
    except ValueError:continue
    if (a,True) not in functions and (a,False) not in functions:
        for thumb in [True,False]:functions[a,thumb]=(size,f[4],'candidate: validate state')
def read(a,n):
    for segment in loads:
        if segment.virtual_address<=a and a+n<=segment.virtual_address+segment.physical_size:
            off=segment.file_offset+a-segment.virtual_address;return raw[off:off+n]
    return b''
def word(a):
    data=read(a,4);return struct.unpack('<I',data)[0] if len(data)==4 else None
relocated={}
for r in elf.relocations:
    if r.has_symbol and r.type.name in ['ARM_GLOB_DAT','ARM_JUMP_SLOT','ARM_ABS32']:
        if r.symbol.value:relocated[r.address]=(r.symbol.value+(word(r.address) or 0) if r.type.name=='ARM_ABS32' else r.symbol.value)&0xffffffff
def loaded(a):return relocated.get(a,word(a))
vtable=next(s.value for s in elf.symbols if s.name=='_ZTVN4Anki5Cozmo21FaceAnimationKeyFrameE')
got=next(r.address for r in elf.relocations if r.has_symbol and r.symbol.name=='_ZTVN4Anki5Cozmo21FaceAnimationKeyFrameE')
rows=[]
for (a,thumb),(size,name,state) in sorted(functions.items()):
    md=capstone.Cs(capstone.CS_ARCH_ARM,capstone.CS_MODE_THUMB if thumb else capstone.CS_MODE_ARM)
    md.detail=True;md.skipdata=True; values={};history=[]
    for ins in md.disasm(read(a,size),a):
        if not ins.id:values.clear();continue
        ops=ins.operands;pc=ins.address; changes={}
        values[ARM_REG_PC]=pc+(4 if thumb else 8)
        def operand(op):
            if op.type==ARM_OP_IMM:return op.imm
            if op.type==ARM_OP_REG and not op.shift.type:return values.get(op.reg)
            return None
        if ins.id in (ARM_INS_MOV,ARM_INS_MOVW) and len(ops)==2:
            changes[ops[0].reg]=operand(ops[1])
        elif ins.id==ARM_INS_MOVT and len(ops)==2:
            old=values.get(ops[0].reg)
            if old is not None:changes[ops[0].reg]=(old&0xffff)|(ops[1].imm<<16)
        elif ins.id==ARM_INS_ADR and len(ops)==2:
            displacement=operand(ops[1])
            if displacement is not None:
                adr_pc=((pc+4)&~3) if thumb else pc+8
                changes[ops[0].reg]=(adr_pc+displacement)&0xffffffff
        elif ins.id in (ARM_INS_ADD,ARM_INS_SUB) and len(ops) in (2,3):
            x=values.get(ops[0].reg) if len(ops)==2 else operand(ops[1]); y=operand(ops[-1])
            if x is not None and y is not None:changes[ops[0].reg]=(x+y*(1 if ins.id==ARM_INS_ADD else -1))&0xffffffff
        elif ins.id==ARM_INS_LDR and len(ops)==2 and ops[1].type==ARM_OP_MEM:
            mem=ops[1].mem;base=values.get(mem.base)
            if mem.base==ARM_REG_PC and not mem.index:base=(pc+4)&~3 if thumb else pc+8
            index=values.get(mem.index,0) if mem.index else 0
            if base is not None and index is not None and not ops[1].shift.type:
                address=(base+mem.disp+index*mem.scale)&0xffffffff;value=loaded(address)
                changes[ops[0].reg]=value
                if address==got or value in [vtable,vtable+8]:
                    rows.append({'address':hex(pc),'owner':name,'owner_va':hex(a),'mode':'Thumb' if thumb else 'ARM','state':state,'instruction':ins.mnemonic+' '+ins.op_str,'load_address':hex(address),'loaded_value':hex(value) if value is not None else None,'preceding':history[-12:]})
        _,written=ins.regs_access()
        for reg in written:values.pop(reg,None)
        for reg,value in changes.items():
            if value is not None:values[reg]=value
        if ins.id in (ARM_INS_BL,ARM_INS_BLX):
            for reg in [ARM_REG_R0,ARM_REG_R1,ARM_REG_R2,ARM_REG_R3,ARM_REG_R12]:values.pop(reg,None)
        elif ins.group(capstone.CS_GRP_JUMP):values.clear()
        history.append(f'{pc:08X}: {ins.mnemonic} {ins.op_str}')
result={'warning':__doc__,'vtable':hex(vtable),'got':hex(got),'function_interpretations':len(functions),'references':rows}
(root/'re-analysis/research/20261006-M5-013-vtable-constants.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
print(len(rows),'constant-address vtable load hypotheses; validate native instructions')
