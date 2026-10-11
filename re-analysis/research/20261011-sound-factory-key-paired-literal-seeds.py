"""New first-seed form only: shipped paired PC-literal transfers for original factory/key targets.
All encodings are navigation candidates, not code-mode or runtime admission proof.
"""
from pathlib import Path
import importlib.util,io,contextlib,struct,json,hashlib
path=Path(__file__).with_name('20261011-sound-paired-block-constant-model.py')
spec=importlib.util.spec_from_file_location('paired_model',path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
targets=(0xa3b684,0xa3b694,*range(0x108d9a0,0x108d9b0))
ns={'__file__':str(path.resolve())}
with contextlib.redirect_stdout(io.StringIO()):exec(compile(m.build_source(targets,controls=True),str(path),'exec'),ns)
n=ns['n'];Cs=n.capstone.Cs
from capstone.arm import *
decoders={False:Cs(n.capstone.CS_ARCH_ARM,n.capstone.CS_MODE_ARM),True:Cs(n.capstone.CS_ARCH_ARM,n.capstone.CS_MODE_THUMB)}
for md in decoders.values():md.detail=True
rows=[];selected=[];bounds=[]
for va,size,off in ns['exec_sections']:
 if va>=0xf00000 or va+size<=0x4a0000:continue
 data=n.read(va,size);start=max(0,0x4a0000-va)
 for thumb,step in ((False,4),(True,2)):
  for j in range((start+step-1)//step*step,size-3,step):
   if not thumb:
    w=struct.unpack_from('<I',data,j)[0]
    if w&0x0f7f00f0!=0x014f00d0:continue
   else:
    h=struct.unpack_from('<H',data,j)[0]
    if h&0xfe00!=0xe800:continue
   a=va+j;ins=next(decoders[thumb].disasm(data[j:j+4],a,count=1),None)
   if ins is None or ins.id!=ARM_INS_LDRD or ins.writeback:continue
   ops=ins.operands
   if len(ops)!=3 or any(o.type!=ARM_OP_REG for o in ops[:2]) or ops[2].type!=ARM_OP_MEM or ops[2].mem.base!=ARM_REG_PC or ops[2].mem.index:continue
   base=((a+4)&~3) if thumb else a+8;loc=(base+ops[2].mem.disp)&0xffffffff
   try:words=[n.word(loc+4*k) for k in range(2)]
   except ValueError:continue
   # Match812's literal/GOT vicinity heuristic for each loaded word, adding no old scalar seeds.
   seed=any(any(t-512<=v<t+2 or t-512<=((base+v)&0xffffffff)<t+2 for t in targets) or ns['gotlo']<=v<ns['gothi'] or ns['gotlo']<=((base+v)&0xffffffff)<ns['gothi'] for v in words)
   row={'address':f'{a:08X}','mode':'Thumb' if thumb else 'ARM','instruction':ins.mnemonic+' '+ins.op_str,'instruction_hex':bytes(ins.bytes).hex(),'literal_address':f'{loc:08X}','words':[f'{v:08X}' for v in words],'selected_first_seed':seed}
   rows.append(row)
   if seed:
    captured=io.StringIO()
    with contextlib.redirect_stdout(captured):ns['execute'](data,j,a,thumb)
    bounds.extend(captured.getvalue().splitlines());selected.append(row)
result={'model_sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'baseline_sha256':m.BASELINE_SHA,'engine_sha256':hashlib.sha256(n.raw).hexdigest(),'targets':[f'{x:08X}' for x in targets],'scope':'Paired PC-literal first seeds only, excluding writeback/indexed forms. Existing scalar seeds not repeated. ARM/Thumb encoding candidates do not certify mode, caller admission or absence of indirect/aliased inputs.','paired_literals':rows,'selected_first_seeds':selected,'local_limits':bounds,'events':sorted(ns['events'].values(),key=lambda e:(e['address'],e['kind']))}
Path(__file__).with_suffix('.json').write_bytes((json.dumps(result,indent=2)+'\n').encode())
print('paired PC-literal encodings',len(rows),'selected new first seeds',len(selected),'events',len(result['events']),'limits',len(bounds))
for row in selected:print(json.dumps(row))
