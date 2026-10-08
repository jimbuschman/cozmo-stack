"""Resolve ARM PLT slots from shipped ELF bytes and dynamic relocation records."""
from pathlib import Path
import struct,sys,importlib.util
sys.argv.append('--light')
spec=importlib.util.spec_from_file_location('native',Path(__file__).with_name('20261007-queue5-native.py'))
n=importlib.util.module_from_spec(spec);spec.loader.exec_module(n)
shoff=struct.unpack_from('<I',n.raw,32)[0]
shsize,shcount=struct.unpack_from('<HH',n.raw,46)
sections=[struct.unpack_from('<10I',n.raw,shoff+i*shsize) for i in range(shcount)]
rel={}
for sh in sections:
    if sh[1]!=9:continue
    syms=sections[sh[6]];namessec=sections[syms[6]]
    names=n.raw[namessec[4]:namessec[4]+namessec[5]]
    for p in range(sh[4],sh[4]+sh[5],sh[9]):
        slot,info=struct.unpack_from('<II',n.raw,p)
        name,value,size,binfo,other,index=struct.unpack_from('<IIIBBH',n.raw,syms[4]+(info>>8)*syms[9])
        end=names.find(b'\0',name)
        rel[slot]=(info&255,names[name:end].decode('utf-8'),value)
def immediate(w):
    x=w&255;r=((w>>8)&15)*2
    return ((x>>r)|(x<<(32-r)))&0xffffffff if r else x
for arg in sys.argv[1:]:
    if not arg.startswith('0x'):continue
    a=int(arg,16);w0,w1,w2=struct.unpack('<III',n.read(a,12))
    assert w0&0xfffff000==0xe28fc000 and w1&0xfffff000==0xe28cc000,(arg,'unexpected PLT')
    slot=(a+8+immediate(w0)+immediate(w1)+(w2&4095)*(1 if w2&(1<<23) else -1))&0xffffffff
    print(f'{a:08X}: words={w0:08X},{w1:08X},{w2:08X} slot={slot:08X} raw={n.word(slot):08X} relocation={rel.get(slot)}')
