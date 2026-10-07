"""Candidate search for direct flag-displacement byte stores, without name filtering.
Function/mode-bounded where symbols exist; index-only bodies in both states are navigation.
This does not capture every aliased/computed offset and is not an absence proof.
"""
from pathlib import Path
import lief,capstone,json,re
root=Path(__file__).resolve().parents[2];out=root/'re-analysis/research'
b=lief.parse(str(root/'resources/lib/armeabi-v7a/libcozmoEngine.so'))
functions={}
for s in b.symbols:
    if s.value and s.size and s.type==lief.ELF.Symbol.TYPE.FUNC:functions.setdefault((s.value&~1,bool(s.value&1)),(s.size,s.name,'symbol'))
for line in (root/'re-analysis/decomp/libcozmoEngine/index.tsv').read_text(encoding='utf-8').splitlines():
    f=line.split('\t')
    if len(f)!=5 or f[2]=='THUNK':continue
    try:a=int(f[0],16);n=int(f[1])
    except ValueError:continue
    if (a,True) not in functions and (a,False) not in functions:
        for thumb in [True,False]:functions[(a,thumb)]=(n,f[4],'candidate-only')
loads=[s for s in b.segments if s.type==lief.ELF.Segment.TYPE.LOAD]
raw=(root/'resources/lib/armeabi-v7a/libcozmoEngine.so').read_bytes()
def read(a,n):
    for s in loads:
        if s.virtual_address<=a<a+n<=s.virtual_address+s.physical_size:
            p=s.file_offset+a-s.virtual_address;return raw[p:p+n]
    return b''
items=[]
for (a,thumb),(n,name,state) in sorted(functions.items()):
    md=capstone.Cs(capstone.CS_ARCH_ARM,capstone.CS_MODE_THUMB if thumb else capstone.CS_MODE_ARM);md.skipdata=True
    context=[]
    for pc,size,mn,ops in md.disasm_lite(read(a,n),a):
        if mn.startswith('strb') and re.search(r'#(?:0x28|0x30|40|48)\]',ops):
            items.append({'address':hex(pc),'owner_va':hex(a),'owner':name,'mode':'Thumb' if thumb else 'ARM','mode_status':state,'instruction':mn+' '+ops,'preceding_instructions':context[-16:]})
        context.append(f'{pc:08X}: {mn} {ops}')
result={'warning':'Displacement candidates only; literal pools and wrong-mode candidates require validation. Arbitrary aliases and computed offsets remain outside this filter.','function_interpretations':len(functions),'candidates':items}
(out/'20261006-M5-013-byte-store-candidates.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
print(len(items),'candidate stores in',len(functions),'function interpretations')
