from pathlib import Path
import re,struct
root=Path(__file__).resolve().parent
ins={}
for name in ('20261009-sound-plugin138-setup-design-native.txt','20261009-sound-plugin138-live-design-native.txt'):
 for m in re.finditer(r'^([0-9A-F]{8}):\s+(\S+)\s+(.*)$',(root/name).read_text(encoding='utf-8'),re.M):
  if not m[2].startswith('raw='):ins[int(m[1],16)]=(m[2],m[3])
# Symbolic dependency extraction from already-read native persistent parameter graphs.
# No floating arithmetic execution, sample transform, production implementation or equivalence test.
imports={0x4a415c:'cosf',0x4a4168:'sinf',0x4ab038:'tanf',0x4a4078:'sqrtf'}
def bits(x):return ('bits',x)
variants=[('Init',[0xaa3b68,0xaa3fec,0xaa4090,0xaa3ee8,0xaa3f40,0xaa3e6c,0xaa3f98],0xaa3c6c,
 {'s15':'Rate','s17':'CappedFrequency','s24':'Gain','s16':'Q','s18':bits('00000000'),'s19':bits('3F666666'),'s20':bits('40C90FDB'),'s22':bits('3CCCCCCD'),'s21':bits('C2140000'),'s23':bits('4BD49A78')},['s13','s11','s12','s16','s14','s15']),
 ('Live0',[0xaa48a0,0xaa538c,0xaa546c,0xaa50f0,0xaa5014,0xaa5244,0xaa52dc],0xaa49b8,
 {'s10':'Rate','s14':'CappedFrequency','s17':'Gain','s18':'Q'},['s14','s8','s15','s13','s16','s17']),
 ('Live1',[0xaa4c78,0xaa5588,0xaa563c,0xaa5174,0xaa506c,0xaa5290,0xaa532c],0xaa4d8c,
 {'s14':'Rate','s16':'CappedFrequency','s18':'Gain','s19':'Q','s15':bits('3CCCCCCD')},['s15','s13','s12','s18','s16','s5'])]
out=['# Plug-in138 persistent type dependency graphs','','Primary: setup-design/live-design native companions; exact engine hash is in those files. Each address row transcribes one native dependency. Copies preserve raw identity; native integer mantissa/exponent operations are retained rather than replaced with pow. VMLA includes old destination before both multiplicands. Native SQRT and imported fallback are separate operations with an unordered-result selection. Gain threshold native MI means ordered less; unordered follows the polynomial branch.','','These graphs bind raw F32 Rate, CappedFrequency, Gain and Q from P138527/P138533. CappedFrequency uses their exact LE/unordered selection. No PCM inputs. Entry type0 skips design; unknown nonzero six-zero fallback remains P138533C. Imports reuse primary-resolved cosf/sinf/tanf/sqrtf from the EQ graph companions; imported phone bodies remain outside shipped engine. Publication is P138534, independently transcribed.','','This is symbolic research, not numerical execution, an implementation or an equivalence test.']
results=[]
for label,starts,stop,initial,outputs in variants:
 vr=[]
 for kind,start in enumerate(starts,1):
  reg=dict(initial);rows=[];cmp=None
  def value(x):
   x=x.strip()
   if x.startswith('#'):return bits(struct.pack('>f',float(x[1:])).hex().upper())
   assert x in reg,(label,kind,hex(addr),x)
   return reg[x]
  def record(address,op,args):
   expr=(op,*args);rows.append((address,op,args));return expr
  def run(pc,end):
   global addr,cmp
   seen=set()
   while pc!=end:
    assert pc not in seen,(label,kind,hex(pc));seen.add(pc);addr=pc
    mn,full=ins[pc];operand=full.split(' ;')[0];args=[x.strip() for x in operand.split(',')]
    if mn=='vldr':
     lit=re.search(r'word\[[^]]+\]=([0-9A-F]{8})',full);assert lit,(label,kind,hex(pc),full)
     reg[args[0]]=bits(lit[1])
    elif mn in ('vmov','vmov.f32'):reg[args[0]]=value(args[1])
    elif mn in ('vmul.f32','vnmul.f32','vadd.f32','vsub.f32','vdiv.f32','vneg.f32','vsqrt.f32','vcvt.u32.f32','vmla.f32'):
     v=([value(args[0])] if mn=='vmla.f32' else [])+[value(x) for x in args[1:]]
     reg[args[0]]=record(pc,mn,v)
    elif mn in ('vcmpe.f32','vcmp.f32'):cmp=[value(x) for x in args]
    elif mn=='vmrs':pass
    elif mn in ('ubfx','lsr','lsl','add'):
     v=[value(args[1])]+[('u32',int(x[1:],0)) for x in args[2:]]
     reg[args[0]]=record(pc,mn+'.raw32',v)
    elif mn=='bl':
     target=int(args[0][1:],0);reg['r0']=record(pc,'phone.'+imports[target],[value('r0')])
    elif mn=='b':pc=int(args[0][1:],0);continue
    elif mn=='bmi':
     target=int(args[0][1:],0);m1,f1=ins[target];m2,f2=ins[target+4]
     assert m1=='vldr' and m2=='b'
     dest=f1.split(',')[0];lit=re.search(r'word\[[^]]+\]=([0-9A-F]{8})',f1);assert lit
     join=int(f2[1:],0);cond=('nativeMI',*cmp)
     run(pc+4,join)
     reg[dest]=record(pc,'select',[cond,bits(lit[1]),value(dest)])
     pc=join;continue
    elif mn=='bne':
     # Actual native unordered self-comparison gates exactly a sqrtf fallback.
     target=int(args[0][1:],0);seq=[ins[target+i*4] for i in range(4)]
     assert [x[0] for x in seq]==['vmov','bl','vmov','b'],(label,kind,hex(pc),seq)
     assert int(seq[1][1][1:],0)==0x4a4078
     src=seq[0][1].split(',')[1].strip();dest=seq[2][1].split(',')[0].strip();join=int(seq[3][1][1:],0)
     assert cmp[0]==cmp[1]
     native=value(dest);fallback=record(target+4,'phone.sqrtf',[value(src)])
     reg[dest]=record(pc,'select',[('unordered',*cmp),fallback,native])
     # Any instructions between branch and return join are already executed before the branch.
     assert join==pc+4,(label,kind,hex(pc),hex(join))
    else:raise ValueError((label,kind,hex(pc),mn,full))
    pc+=4
  run(start,stop)
  vals=[value(x) for x in outputs];vr.append(vals)
  ids={};dag=[]
  def ref(v):
   if isinstance(v,str):return v
   if v[0]=='bits':return 'F32('+v[1]+')'
   if v[0]=='u32':return f'RawU32({v[1]:08X})'
   if v not in ids:
    children=[ref(x) for x in v[1:]];ids[v]=f'n{len(ids)+1}';dag.append((ids[v],v[0],children))
   return ids[v]
  out+=['',f'## {label} type {kind}','','| Native address | Dependency |','|---|---|']
  for address,op,args in rows:out.append(f"| {address:08X} | {op}({', '.join(ref(v) for v in args)}) |")
  out+=['','| Common input | Exact dependency |','|---|---|']+[f'| {n} | {ref(v)} |' for n,v in zip(['N0','N1','N2','D2','D1','Den'],vals)]
  out+=['','| Node | Exact dependency |','|---|---|']+[f"| {n} | {op}({', '.join(c)}) |" for n,op,c in dag]
 results.append(vr)
# Report actual comparison; never weaken symbolic equality to arithmetic equivalence.
assert results[0]==results[1]==results[2]
for k in range(7):
 same=results[0][k]==results[1][k]==results[2][k]
 print(f'Type{k+1}: exact symbolic input dependencies across three paths: {same}')
 out+=['',f'Type{k+1}: exact symbolic input equality across Init/live0/live1 = {same}.']
(root/'20261009-sound-plugin138-type-graphs.md').write_text('\n'.join(out)+'\n',encoding='utf-8')
