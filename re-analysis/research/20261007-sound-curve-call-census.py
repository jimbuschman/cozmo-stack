"""Primary ELF direct-link census; candidates do not prove dynamic-call absence."""
from pathlib import Path
import importlib.util, struct, hashlib
spec=importlib.util.spec_from_file_location('native',Path(__file__).with_name('20261007-queue5-native.py'))
native=importlib.util.module_from_spec(spec);spec.loader.exec_module(native)
section=native.elf.get_section('.text'); base=section.virtual_address; data=bytes(section.content)
targets={0xA14E28,0xA17724,0xA17878}; hits=[]
print('ENGINE SHA256',hashlib.sha256(native.raw).hexdigest())
print('Scope: direct ARM B/BL and Thumb B.W/BL/BLX encodings over .text; indirect calls/data pointers not enumerated.')
for offset in range((-base)%4,len(data)-3,4):
    word=struct.unpack_from('<I',data,offset)[0]
    if word&0x0E000000!=0x0A000000 or word>>28==15: continue
    immediate=word&0xFFFFFF
    if immediate&0x800000: immediate-=0x1000000
    target=(base+offset+8+(immediate<<2))&0xFFFFFFFF
    if target in targets: hits.append((base+offset,target,'ARM BL' if word&0x01000000 else 'ARM B'))
for offset in range(0,len(data)-3,2):
    first,second=struct.unpack_from('<HH',data,offset)
    if first&0xF800!=0xF000: continue
    if second&0xD000==0xD000: pc=base+offset+4; kind='Thumb BL'
    elif second&0xD001==0xC000: pc=(base+offset+4)&~3; kind='Thumb BLX'
    elif second&0xD000==0x9000: pc=base+offset+4; kind='Thumb B.W'
    elif second&0xD000==0x8000 and (first>>6&15)<14: pc=base+offset+4; kind='Thumb B.W conditional'
    else: continue
    sign=first>>10&1; j1=second>>13&1; j2=second>>11&1
    immediate=(sign<<24)|((1^(j1^sign))<<23)|((1^(j2^sign))<<22)|((first&0x3FF)<<12)|((second&0x7FF)<<1)
    if kind=='Thumb B.W conditional':
        immediate=(sign<<20)|(j2<<19)|(j1<<18)|((first&0x3F)<<12)|((second&0x7FF)<<1)
        if sign: immediate-=1<<21
    elif sign: immediate-=1<<25
    target=(pc+immediate)&0xFFFFFFFF
    if target in targets: hits.append((base+offset,target,kind))
for address,target,kind in sorted(hits):
    print(f'{address:08X} -> {target:08X} {kind}')
    native.dump(address-16,address+12,kind.startswith('Thumb'))
print('COUNTS', {f'{target:08X}':sum(item[1]==target for item in hits) for target in sorted(targets)})
