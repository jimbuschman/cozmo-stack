"""Q14-104/131: finite output-query candidate word witnesses; no payload/callee tracing."""
from pathlib import Path
import importlib.util,sys,json,hashlib
sys.argv.append('--light')
b=Path(__file__).resolve().parent
spec=importlib.util.spec_from_file_location('native',b/'20261007-queue5-native.py');n=importlib.util.module_from_spec(spec);spec.loader.exec_module(n)
md=n.capstone.Cs(n.capstone.CS_ARCH_ARM,n.capstone.CS_MODE_ARM)
cs=json.loads((b/'20261010-sound-routing-query-interior-callers.json').read_text())['candidates']
cs=[c for c in cs if c['mode']=='Thumb' and int(c['address'],16)>=0x95E540]
assert len(cs)==138 and all(int(c['address'],16)%4==2 for c in cs)
rows=[]
with (b/'20261010-sound-routing-query-candidate-word-native.txt').open('w',encoding='utf-8',newline='\n') as f:
 f.write('ENGINE SHA256 '+hashlib.sha256(n.raw).hexdigest()+'\nLOCAL WORD INTERPRETATIONS ONLY; mode/input/alias admission is not proved; no callee descent\n')
 for c in cs:
  a=int(c['address'],16)&~3
  i=next(md.disasm(n.read(a,4),a),None)
  row={'candidate':c,'containingAddress':hex(a),'bytes':n.read(a,4).hex(),'containingARMInterpretation':i.mnemonic+' '+i.op_str if i else None,'limit':'ARM word interpretation at natural alignment; local byte witness, not full control-flow/mixed-mode/input/alias proof.'}
  rows.append(row)
  f.write('CANDIDATE '+c['address']+' '+c['instruction']+'\n')
  for at in (a-4,a,a+4):
   x=next(md.disasm(n.read(at,4),at),None)
   f.write(hex(at)+' '+n.read(at,4).hex()+' '+(x.mnemonic+' '+x.op_str if x else 'UNDECODED')+'\n')
r={'engineSHA256':hashlib.sha256(n.raw).hexdigest(),'count':len(rows),'rows':rows}
(b/'20261010-sound-routing-query-candidate-word-witness.json').write_text(json.dumps(r,indent=2)+'\n',encoding='utf-8')
print('witnesses',len(rows),'undecoded',sum(x['containingARMInterpretation'] is None for x in rows))
