"""Three existing rate/frame PLT identities: raw ARM arithmetic and ELF relocations only."""
from pathlib import Path
import lief,struct,json,hashlib
path=Path('resources/lib/armeabi-v7a/libcozmoEngine.so');raw=path.read_bytes();elf=lief.parse(str(path))
loads=[(x.virtual_address,x.physical_size,x.file_offset) for x in elf.segments if x.type==lief.ELF.Segment.TYPE.LOAD]
def read(a,size):
 for va,n,off in loads:
  if va<=a and a+size<=va+n:return raw[off+a-va:off+a-va+size]
 raise ValueError(a)
def imm(w):
 x=w&255;r=((w>>8)&15)*2
 return ((x>>r)|(x<<(32-r)))&0xffffffff if r else x
rel={x.address:x for x in elf.relocations};mapping=[]
for a,name in ((0x4B3E70,'__aeabi_idiv'),(0x4BE310,'__aeabi_uidiv'),(0x4BAA40,'__aeabi_l2f')):
 code=read(a,12);w0,w1,w2=struct.unpack('<III',code)
 assert w0&0x0FFFF000==0x028FC000 and w1&0x0FFFF000==0x028CC000
 assert w2&0x0FFFF000==0x05BCF000
 slot=(a+8+imm(w0)+imm(w1)+(w2&0xfff))&0xffffffff
 r=rel[slot];assert r.has_symbol and r.symbol.name==name and r.type.name=='ARM_JUMP_SLOT'
 mapping.append({'plt':f'{a:08X}','plt_bytes':code.hex(),'slot':f'{slot:08X}','raw_word':f'{struct.unpack("<I",read(slot,4))[0]:08X}','relocation':r.type.name,'symbol':r.symbol.name,'symbol_value':f'{r.symbol.value:08X}','section_index':r.symbol.shndx})
events=[]
for line in Path(__file__).with_name('20261010-sound-rate-frame-full-got-navigation.txt').read_text(encoding='utf-8').splitlines():
 if line.startswith('{'):
  e=json.loads(line)
  if e['kind'].startswith('live-') and e['effective'].startswith('010524'):events.append(e)
lookup={x['plt']:x['symbol'] for x in mapping};native={'00A548B8':'813 scalar-only leaf','009E2BD0':'813 incomingR3 killed before read'}
for e in events:
 dest=int(e['instruction'].split('#')[1],16)
 e['callee_identity']=lookup.get(f'{dest:08X}',native.get(f'{dest:08X}','UNKNOWN'))
 assert e['callee_identity']!='UNKNOWN'
r={'engine_sha256':hashlib.sha256(raw).hexdigest(),'helper_linkage':mapping,'rate_frame_live_observations':events,'counts':{'register_observations':len(events),'unique_calls':len({e['address'] for e in events}),'helper_register_observations':sum(e['callee_identity'].startswith('__aeabi_') for e in events)},'limit':'Identity/navigation only; standard ABI cross-check is external. Runtime resolver winner and actual helper algorithm/memory effects are not certified.'}
Path(__file__).with_suffix('.json').write_text(json.dumps(r,indent=2)+'\n',encoding='utf-8')
print(mapping);print(r['counts'])
