"""Original755 candidates into9D2410 only; primary ARM words, no new caller scan.
No body/recipient descent or global alternate-mode/alias exclusion.
"""
from pathlib import Path
import sys,importlib.util,json,hashlib
sys.argv.append('--light')
root=Path(__file__).parent;native=root/'20261007-queue5-native.py'
spec=importlib.util.spec_from_file_location('native',native);n=importlib.util.module_from_spec(spec);spec.loader.exec_module(n)
from capstone.arm import *
old=root/'20261010-sound-routing-destination-interior-callers.json';previous=json.loads(old.read_text(encoding='utf-8'))
assert previous['sha256']==hashlib.sha256(n.raw).hexdigest()
selected=[x for x in previous['candidates'] if 0x9d2410<=int(x['target'][1:],16)<0x9d24c8];assert len(selected)==33
md=n.capstone.Cs(n.capstone.CS_ARCH_ARM,n.capstone.CS_MODE_ARM);md.detail=True
def decode(a):
 ins=next(md.disasm(n.read(a,4),a,count=1),None)
 return {'address':f'{a:08X}','hex':n.read(a,4).hex(),'instruction':ins.mnemonic+' '+ins.op_str if ins else 'INVALID'}
literal_refs=[]
for a in range(0x9d224c,0x9d23e4,4):
 ins=next(md.disasm(n.read(a,4),a,count=1),None)
 if not ins or ins.id!=ARM_INS_LDR:continue
 for op in ins.operands:
  if op.type==ARM_OP_MEM and op.mem.base==ARM_REG_PC and not op.mem.index and (a+8+op.mem.disp)&0xffffffff==0x9d23ec:literal_refs.append(decode(a))
assert literal_refs
rows=[]
for c in selected:
 a=int(c['address'],16);word=a&~3
 if a==0x9d23ec:
  disposition='Literal read on existing9D224C ARM path, after9D23E0 POP-PC; not a fresh caller input.'
  context=[decode(0x9d23dc),decode(0x9d23e0),{'address':'009D23EC','hex':n.read(a,4).hex(),'literal_word':f'{n.word(a):08X}'}]
 else:
  assert a&3==2
  disposition='Upper half of different ARM instruction on recovered Layer path; no Thumb caller admitted or receiver supplied.'
  context=[decode(word-4),decode(word),decode(word+4)]
 rows.append({'original_candidate':c,'disposition':disposition,'primary_context':context})
result={'engine_sha256':hashlib.sha256(n.raw).hexdigest(),'original755_candidates_sha256':hashlib.sha256(old.read_bytes()).hexdigest(),'scope':'Only33 existing755 candidates into9D2410;32 upper-half ARM-word attributions and one literal. No new branch search, function/receiver admission, global alternate-mode/indirect/copied/relative alias exclusion or descendant body extraction. Other34 candidates into9F319C are not covered here.','literal23EC_readers':literal_refs,'attributions':rows}
Path(__file__).with_suffix('.json').write_bytes((json.dumps(result,indent=2)+'\n').encode())
print('33 existing candidates retained; literal readers',literal_refs)
