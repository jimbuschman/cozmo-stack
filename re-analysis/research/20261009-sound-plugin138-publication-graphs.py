from pathlib import Path
import re,struct,json,hashlib
root=Path(__file__).resolve().parent
ins={}
for name in ('20261009-sound-plugin138-setup-design-native.txt','20261009-sound-plugin138-live-design-native.txt'):
 for m in re.finditer(r'^([0-9A-F]{8}):\s+(\S+)\s+(.*)$',(root/name).read_text(encoding='utf-8'),re.M):
  if not m[2].startswith('raw='):ins[int(m[1],16)]=(m[2],m[3])
# Symbolic dependency propagation only; no numeric execution or PCM equivalence claim.
# Integer pointer/register moves and stack loads stay ordered, including load-before-store.
cases=[('Init',0xaa3c6c,0xaa3e68,{'s13':'N0','s11':'N1','s12':'N2','s16':'D2','s14':'D1','s15':'Den','s18':('bits','00000000')},{'r6':('dst',0),'sp':('stack',0)},0),
 ('Live group0',0xaa49b8,0xaa4bc0,{'s14':'N0','s8':'N1','s15':'N2','s13':'D2','s16':'D1','s17':'Den'},{'r4':('plugin',0),'sp':('stack',0)},0xa0),
 ('Live group1',0xaa4d8c,0xaa4f90,{'s15':'N0','s13':'N1','s12':'N2','s18':'D2','s16':'D1','s5':'Den'},{'r4':('plugin',0),'sp':('stack',0)},0x150)]
allstores=[];out=['# Plug-in138 persistent publication dependency graphs','','Primary: setup-design and live-design native companions. Engine SHA256 02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1. Symbolic transcription, not production implementation, numeric execution or an equivalence test. All offsets hex.','','Entry numerators N0,N1,N2,D2,D1 and denominator Den are raw F32 results of the separately retained type graph. No algebraic reassociation. Operation records preserve old destination in VMLA. Constants are raw bits; moves preserve raw identity. Stack LDM reads four words before following stack overwrites. Region0 denotes P+A0/P150. Four words at region-10..-4 repeat normalized N0/Den, not input gain.','','This graph establishes common publication only. It does not assert the input numerators from the seven Init/live type graphs match.']
for label,start,end,reg,ptr,offset in cases:
 reg=dict(reg);ptr=dict(ptr);mem={};rows=[];nodes={};stores=[]
 def val(x):
  if x.startswith('#'):return ('bits',struct.pack('>f',float(x[1:])).hex().upper())
  assert x in reg,(label,hex(addr),x)
  return reg[x]
 def node(op,args):
  key=f'v{addr:08X}';expr=(op,*args);nodes[key]=expr
  rows.append((f'{addr:08X}',key,op,args));return expr
 def address(full):
  m=re.search(r'\[(\w+)(?:, #(-?0x[0-9a-f]+|-?\d+))?\]',full);assert m,(label,hex(addr),full)
  base=ptr[m[1]];return (base[0],base[1]+(int(m[2],0) if m[2] else 0))
 def store(where,value):
  mem[where]=value
  if where[0] in ('dst','plugin'):
   canonical=where[1]-(offset if where[0]=='plugin' else 0)
   stores.append((canonical,value));rows.append((f'{addr:08X}',f'region{canonical:+X}','publish',[value]))
 for addr in range(start,end,4):
  mn,full=ins[addr];args=[x.strip() for x in full.split(',')]
  if mn.startswith(('vdiv.','vneg.','vadd.','vsub.','vmul.','vmla.')):
   values=([val(args[0])] if mn.startswith('vmla.') else [])+[val(x) for x in args[1:]]
   reg[args[0]]=node(mn,values)
  elif mn=='vmov.f32':reg[args[0]]=val(args[1])
  elif mn=='vdup.32':
   q=int(args[0][1:]);m=re.match(r'd(\d+)\[(\d+)\]',args[1]);assert m
   value=val('s'+str(int(m[1])*2+int(m[2])))
   for i in range(q*4,q*4+4):reg['s'+str(i)]=value
  elif mn=='vstr':
   where=address(full);src=args[0]
   values=[val(src)] if src.startswith('s') else [val('s'+str(2*int(src[1:])+j)) for j in range(2)]
   for j,v in enumerate(values):store((where[0],where[1]+4*j),v)
  elif mn=='str':store(address(full),val(args[0]))
  elif mn=='mov':
   if args[1] in ptr:ptr[args[0]]=ptr[args[1]]
   elif args[1].startswith('#'):reg[args[0]]=('bits',f'{int(args[1][1:],0):08X}')
   else:reg[args[0]]=val(args[1])
  elif mn=='add':
   b=ptr[args[1]];ptr[args[0]]=(b[0],b[1]+int(args[2][1:],0))
  elif mn=='ldm':
   base=args[0].rstrip('!');where=ptr[base]
   registers=re.search(r'\{([^}]+)\}',full)[1].split(',')
   values=[mem[(where[0],where[1]+4*j)] for j in range(len(registers))]
   for name,v in zip(registers,values):reg[name.strip()]=v
   if args[0].endswith('!'):ptr[base]=(where[0],where[1]+4*len(registers))
  elif mn=='ldr' and '[r4, #4]' in full:pass # caller reload after completed graph
  else:raise ValueError((label,hex(addr),mn,full))
 assert len(stores)==37,(label,len(stores))
 allstores.append(stores)
 # Assign local dependency names to shared immutable tuples; keep compact DAG rather than nested expansion.
 ids={};dag=[]
 def ref(v):
  if isinstance(v,str):return v
  if v[0]=='bits':return 'F32('+v[1]+')'
  if v not in ids:
   children=[ref(x) for x in v[1:]];ids[v]=f'n{len(ids)+1}';dag.append((ids[v],v[0],children))
  return ids[v]
 out+=['',f'## {label}','','| Native address | Result / destination | Dependency |','|---|---|---|']
 for address,result,op,values in rows:
  out.append('| '+address+' | '+result+' | '+op+'('+', '.join(ref(v) for v in values)+') |')
 out+=['','| Node | Exact dependency |','|---|---|']+[f"| {n} | {op}({', '.join(c)}) |" for n,op,c in dag]
# Compare values by destination, independent of schedule (each schedule remains in rows).
bydest=[dict(v) for v in allstores]
assert bydest[0]==bydest[1]==bydest[2]
assert set(bydest[0])==set(range(-0x10,0x84,4))
out+=['','Validated 37 word publications per path, all 37 exact symbolic destination dependencies identical across Init/live0/live1. Publication schedules are listed separately and need not be reordered. Seven input type graphs remain separate retained work.','']
(root/'20261009-sound-plugin138-publication-graphs.md').write_text('\n'.join(out),encoding='utf-8')
print('Validated three native common graphs, 111 ordered word stores, 37 matching destination dependencies.')
