"""Read-only native navigation: symbol-bounded ARM/Thumb, typed calls and vtable GOT references.
This is a candidate census, not an alias/absence proof.
"""
from pathlib import Path
import lief,capstone,struct,json,sys
from capstone.arm import *
root=Path(__file__).resolve().parents[2]
elf=lief.parse(str(root/'resources/lib/armeabi-v7a/libcozmoEngine.so'))
raw=(root/'resources/lib/armeabi-v7a/libcozmoEngine.so').read_bytes()
loads=[s for s in elf.segments if s.type==lief.ELF.Segment.TYPE.LOAD]
def read(a,n):
    for s in loads:
        if s.virtual_address<=a and a+n<=s.virtual_address+s.physical_size:
            p=s.file_offset+a-s.virtual_address;return raw[p:p+n]
    return b''
def word(a):
    v=read(a,4);return struct.unpack('<I',v)[0] if len(v)==4 else None
got={r.address:r.symbol.name for r in elf.relocations if r.has_symbol}
typed={s.value&~1:s.name for s in elf.symbols if s.value and 'FaceAnimationKeyFrame' in s.name and s.type==lief.ELF.Symbol.TYPE.FUNC}
typed[0x564290]='unnamed FaceAnimationKeyFrame name constructor'
def plt(a):
    md=capstone.Cs(capstone.CS_ARCH_ARM,capstone.CS_MODE_ARM);md.detail=True
    ins=list(md.disasm(read(a,12),a))
    if len(ins)!=3 or [i.mnemonic for i in ins]!=['add','add','ldr']:return None
    def immediate(i):
        ops=[o for o in i.operands if o.type==ARM_OP_IMM]
        if len(ops)==1:return ops[0].imm
        if len(ops)==2:
            x,n=ops[0].imm,ops[1].imm;return ((x>>n)|(x<<(32-n)))&0xffffffff
        return None
    x,y=immediate(ins[0]),immediate(ins[1])
    if x is None or y is None:return None
    return got.get(a+8+x+y+ins[2].operands[-1].mem.disp)
thunks={}
section=elf.get_section('.plt')
assert section is not None
# The shipped PLT does not export individual thunk symbols. Decode it directly.
for a in range(section.virtual_address,section.virtual_address+section.size,4):
    target=plt(a)
    if target and 'FaceAnimationKeyFrame' in target:thunks[a]=target
for s in elf.symbols:
    if s.value and s.size==12 and s.type==lief.ELF.Symbol.TYPE.FUNC:
        target=plt(s.value&~1)
        if target and 'FaceAnimationKeyFrame' in target:thunks[s.value&~1]=target
targets={**typed,**thunks}
functions={}
for s in elf.symbols:
    if s.value and s.size and s.type==lief.ELF.Symbol.TYPE.FUNC:
        key=(s.value&~1,bool(s.value&1));functions.setdefault(key,(s.size,s.name))
references=[];vt=[]
mode='symbols'
if len(sys.argv)>1 and sys.argv[1]=='unnamed':
    mode='unnamed-both-modes'
    previous=json.loads((root/'re-analysis/research/20261006-M5-013-reference-census.json').read_text(encoding='utf-8'))
    assert previous.get('scan_mode')=='symbols', 'Run the symbols pass first; do not append unnamed candidates twice.'
    references=previous['calls'];vt=previous['vtable_got_references']
    additions={}
    for line in (root/'re-analysis/decomp/libcozmoEngine/index.tsv').read_text(encoding='utf-8').splitlines():
        f=line.split('\t')
        if len(f)!=5 or f[2]=='THUNK':continue
        try:a=int(f[0],16);size=int(f[1])
        except ValueError:continue
        if (a,True) not in functions and (a,False) not in functions:
            for thumb in [True,False]:additions[(a,thumb)]=(size,f[4])
    functions=additions
for (a,thumb),(size,name) in sorted(functions.items()):
    md=capstone.Cs(capstone.CS_ARCH_ARM,capstone.CS_MODE_THUMB if thumb else capstone.CS_MODE_ARM);md.detail=True;md.skipdata=True
    literal={}
    for i in md.disasm(read(a,size),a):
        if not i.id:continue
        ops=i.operands
        if i.mnemonic in ['bl','blx','b','b.w'] and ops and ops[0].type==ARM_OP_IMM:
            dst=ops[0].imm&~1
            if dst in targets:references.append({'address':hex(i.address),'owner':name,'owner_va':hex(a),'target':hex(dst),'name':targets[dst],'mode':'Thumb' if thumb else 'ARM','mode_status':'symbol' if mode=='symbols' else 'candidate-only, validate native state'})
        if i.mnemonic.startswith('ldr') and len(ops)==2 and ops[0].type==ARM_OP_REG and ops[1].type==ARM_OP_MEM and ops[1].mem.base==ARM_REG_PC and ops[1].mem.index==0:
            pc=((i.address+4)&~3) if thumb else i.address+8
            literal[ops[0].reg]=(word(pc+ops[1].mem.disp),i.address)
        elif i.mnemonic.startswith('add') and len(ops)>=2 and ops[0].type==ARM_OP_REG and any(o.type==ARM_OP_REG and o.reg==ARM_REG_PC for o in ops[1:]):
            dest=ops[0].reg
            value,where=literal.get(dest,(None,0))
            if value is not None and i.address-where<=16:
                pc=i.address+(4 if thumb else 8)
                g=(pc+value)&0xffffffff
                if 'FaceAnimationKeyFrame' in got.get(g,''):
                    vt.append({'address':hex(i.address),'owner':name,'owner_va':hex(a),'got':hex(g),'symbol':got[g],'mode_status':'symbol' if mode=='symbols' else 'candidate-only, validate native state'})
            literal.pop(dest,None)
        else:
            try:
                for r in i.regs_access()[1]:literal.pop(r,None)
            except capstone.CsError:pass
result={'warning':'Navigation only; indirect calls, PIC idioms outside recognized patterns and arbitrary aliases are not proven absent. Added unnamed bodies are decoded in both modes; neither interpretation is automatically evidence. Inspect every candidate in native instructions.','functions_decoded':len(functions),'scan_mode':mode,'typed_targets':{hex(k):v for k,v in targets.items()},'calls':references,'vtable_got_references':vt}
out=root/'re-analysis/research/20261006-M5-013-reference-census.json'
out.write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
print(f'{len(functions)} symbol-bounded functions; {len(references)} typed call references; {len(vt)} vtable/typeinfo GOT references')
