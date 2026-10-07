"""Navigation for scalar flag stores through register-plus-constant aliases.

Straight-line approximation, not a CFG/alias proof. Conditional writes, merges,
stack spills, indexed addressing, bulk stores and indirect escapes need review.
Symbol bodies use ELF state; index-only bodies are candidates in both states.
"""
from pathlib import Path
import json, lief, capstone
from capstone.arm import *
root=Path(__file__).resolve().parents[2]
binary=root/'resources/lib/armeabi-v7a/libcozmoEngine.so'
elf=lief.parse(str(binary)); raw=binary.read_bytes()
loads=[s for s in elf.segments if s.type==lief.ELF.Segment.TYPE.LOAD]
functions={}
for s in elf.symbols:
    if s.value and s.size and s.type==lief.ELF.Symbol.TYPE.FUNC:
        functions.setdefault((s.value&~1,bool(s.value&1)),(s.size,s.name,'ELF symbol state'))
for line in (root/'re-analysis/decomp/libcozmoEngine/index.tsv').read_text(encoding='utf-8').splitlines():
    fields=line.split('\t')
    if len(fields)!=5 or fields[2]=='THUNK':continue
    try:a=int(fields[0],16); n=int(fields[1])
    except ValueError:continue
    if (a,True) not in functions and (a,False) not in functions:
        for state in [True,False]:functions[a,state]=(n,fields[4],'index-only: validate state')
def unknown(reg,pc):return (f'{reg}@{pc:08X}',0)
rows=[]
for (a,thumb),(size,name,state) in sorted(functions.items()):
    segment=next((s for s in loads if s.virtual_address<=a and a+size<=s.virtual_address+s.physical_size),None)
    if segment is None:continue
    start=segment.file_offset+a-segment.virtual_address
    md=capstone.Cs(capstone.CS_ARCH_ARM,capstone.CS_MODE_THUMB if thumb else capstone.CS_MODE_ARM)
    md.detail=True; md.skipdata=True
    aliases={}; constants={}; history=[]
    for ins in md.disasm(raw[start:start+size],a):
        if not ins.id:aliases.clear();constants.clear();continue
        ops=ins.operands; pc=ins.address
        mn=ins.mnemonic.split('.')[0]
        # A new basic-block entry deliberately forgets predecessor register facts.
        for reg in range(ARM_REG_R0,ARM_REG_R12+1):aliases.setdefault(reg,unknown(md.reg_name(reg),pc))
        aliases.setdefault(ARM_REG_SP,('sp',0))
        if ins.id in (ARM_INS_STRB,ARM_INS_STRH,ARM_INS_STR) and len(ops)>=2 and ops[1].type==ARM_OP_MEM:
            mem=ops[1].mem; base=aliases.get(mem.base,unknown(md.reg_name(mem.base),pc))
            offset=base[1]+mem.disp
            width={ARM_INS_STRB:1,ARM_INS_STRH:2,ARM_INS_STR:4}[ins.id]
            if not mem.index and base[0]!='sp' and any(offset<=flag<offset+width for flag in [0x28,0x30]):
                if base[1] or width!=1: # New hypotheses beyond literal STRB+28/+30.
                    rows.append({'address':hex(pc),'owner':name,'owner_va':hex(a),'mode':'Thumb' if thumb else 'ARM','state':state,'instruction':ins.mnemonic+' '+ins.op_str,'base_origin':base[0],'base_addend':base[1],'effective_offset':offset,'width':width,'source_constant_candidate':constants.get(ops[0].reg),'preceding':history[-18:]})
        updates={}; constant_updates={}
        if ins.id==ARM_INS_MOV and len(ops)==2 and ops[0].type==ARM_OP_REG:
            dest=ops[0].reg
            if ops[1].type==ARM_OP_REG:
                updates[dest]=aliases.get(ops[1].reg,unknown(md.reg_name(ops[1].reg),pc))
                if ops[1].reg in constants:constant_updates[dest]=constants[ops[1].reg]
            elif ops[1].type==ARM_OP_IMM:constant_updates[dest]=ops[1].imm
        elif ins.id in (ARM_INS_ADD,ARM_INS_SUB) and len(ops) in (2,3) and ops[-1].type==ARM_OP_IMM and ops[0].type==ARM_OP_REG:
            dest=ops[0].reg; source=dest if len(ops)==2 else ops[1].reg
            delta=ops[-1].imm*(1 if ins.id==ARM_INS_ADD else -1)
            base=aliases.get(source,unknown(md.reg_name(source),pc))
            updates[dest]=(base[0],base[1]+delta)
            if source in constants:constant_updates[dest]=(constants[source]+delta)&0xffffffff
        _,written=ins.regs_access()
        for reg in written:
            aliases[reg]=unknown(md.reg_name(reg),pc);constants.pop(reg,None)
        aliases.update(updates);constants.update(constant_updates)
        if ins.id in (ARM_INS_BL,ARM_INS_BLX):
            for reg in [ARM_REG_R0,ARM_REG_R1,ARM_REG_R2,ARM_REG_R3,ARM_REG_R12]:
                aliases[reg]=unknown(md.reg_name(reg),pc);constants.pop(reg,None)
        elif ins.group(capstone.CS_GRP_JUMP):aliases.clear();constants.clear()
        history.append(f'{pc:08X}: {ins.mnemonic} {ins.op_str}')
derived=[row for row in rows if row['base_addend']]
result={'warning':__doc__,'function_interpretations':len(functions),'scalar_candidate_count':len(rows),'derived_candidate_count':len(derived),'direct_wide_candidates_not_retained':len(rows)-len(derived),'candidates':derived}
path=root/'re-analysis/research/20261006-M5-013-affine-store-candidates.json'
path.write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
print(len(derived),'derived scalar-store hypotheses;',len(rows)-len(derived),'direct wide hypotheses counted but not retained; not an absence proof')
