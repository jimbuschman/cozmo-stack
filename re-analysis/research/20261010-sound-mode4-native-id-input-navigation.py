"""Two original mode4 IDs: raw engine words and exact MOVW low-half seed navigation."""
from pathlib import Path
import importlib.util,sys,json,struct,hashlib
sys.argv.append('--light')
b=Path(__file__).resolve().parent
spec=importlib.util.spec_from_file_location('native',b/'20261007-queue5-native.py');n=importlib.util.module_from_spec(spec);spec.loader.exec_module(n)
from capstone.arm import *
targets=(725225627,196431345);hits=[];seeds=[]
for target in targets:
 needle=struct.pack('<I',target);at=n.raw.find(needle)
 while at>=0:
  mapped=[hex(va+at-off) for va,size,off in n.loads if off<=at<off+size]
  hits.append({'target':target,'fileOffset':at,'LOADAddresses':mapped});at=n.raw.find(needle,at+1)
for va,size,off in n.loads:
 if va>=0xF00000 or va+size<=0x4A0000:continue
 data=n.read(va,size);start=max(0,0x4A0000-va)
 for thumb,step in ((False,4),(True,2)):
  md=n.capstone.Cs(n.capstone.CS_ARCH_ARM,n.capstone.CS_MODE_THUMB if thumb else n.capstone.CS_MODE_ARM);md.detail=True
  for pos in range((start+step-1)//step*step,size-4,step):
   a=va+pos
   if thumb:
    h,h2=struct.unpack_from('<HH',data,pos)
    if h&0xFBF0!=0xF240:continue
    imm=((h&15)<<12)|((h&0x400)<<1)|((h2&0x7000)>>4)|(h2&255)
   else:
    w=struct.unpack_from('<I',data,pos)[0]
    if w&0x0FF00000!=0x03000000:continue
    imm=((w>>4)&0xF000)|(w&0xFFF)
   tt=[t for t in targets if t&0xFFFF==imm]
   if not tt:continue
   ins=list(md.disasm(data[pos:pos+96],a));head=ins[0] if ins else None
   if not head or head.id!=ARM_INS_MOVW:continue
   dest=head.operands[0].reg;joined=[]
   for x in ins[1:]:
    if x.id==ARM_INS_MOVT and x.operands[0].reg==dest and x.operands[1].imm in [t>>16 for t in tt]:joined.append(hex(x.address))
    if dest in x.regs_access()[1]:break
   seeds.append({'address':hex(a),'mode':'Thumb' if thumb else 'ARM','instruction':head.mnemonic+' '+head.op_str,'targets':tt,'sameRegisterUnclobberedMOVTMatches':joined,'limit':'Encoding/local linear navigation only; mixed-mode, branches, calls and alternate constant assembly require primary attribution.'})
r={'engineSHA256':hashlib.sha256(n.raw).hexdigest(),'targets':list(targets),'rawWordHits':hits,'MOVWSeeds':seeds,'limit':'Only exact little-endian words at every file alignment and selected same-register MOVW/MOVT encodings; zero results do not prove all caller/constant/name/runtime mutation absence.'}
(b/'20261010-sound-mode4-native-id-input-navigation.json').write_text(json.dumps(r,indent=2)+'\n',encoding='utf-8',newline='\n')
print('raw',len(hits),'MOVW',len(seeds),'joined',sum(bool(x['sameRegisterUnclobberedMOVTMatches']) for x in seeds));print(json.dumps(seeds,indent=2))
