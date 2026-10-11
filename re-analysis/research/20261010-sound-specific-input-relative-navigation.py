"""Named Q14 input frontiers only: relative words are navigation, never input/unreachability proof."""
from pathlib import Path
import lief,struct,hashlib,json
r=Path(__file__).resolve().parents[2];p=r/'resources/lib/armeabi-v7a/libcozmoEngine.so';e=lief.parse(str(p))
targets={0xa08024:'008 weight setter',0x9cdfe4:'016 settings updater',0x9903f0:'024 conditional getter receiver',0xa0423c:'104/131 output descriptor destination',0xa14e28:'114 curve evaluator'}
values={v+k:(v,label) for v,label in targets.items() for k in (0,1)}
out=[]
for s in e.sections:
 if not s.has(lief.ELF.Section.FLAGS.ALLOC) or s.type==lief.ELF.Section.TYPE.NOBITS:continue
 d=bytes(s.content);va=s.virtual_address
 for pos in range(0,len(d)-3,4):
  a=va+pos;w=struct.unpack_from('<I',d,pos)[0]
  for bias in (0,4,8):
   dest=(a+bias+w)&0xffffffff
   if dest in values:out.append(dict(section=s.name,address=hex(a),word=hex(w),bias=bias,target=hex(dest),obligation=values[dest][1],encoding='word + address + bias'))
  # ARM EHABI PREL31 is a distinct navigation encoding; unwind metadata is not a live callback supplier.
  off=w&0x7fffffff
  if off&0x40000000:off-=0x80000000
  dest=(a+off)&0xffffffff
  if dest in values:out.append(dict(section=s.name,address=hex(a),word=hex(w),bias=0,target=hex(dest),obligation=values[dest][1],encoding='PREL31'))
result=dict(sha256=hashlib.sha256(p.read_bytes()).hexdigest(),limit='Aligned ALLOC-section self-relative word and PREL31 navigation only. Does not cover arbitrary table-base-relative words, transformed pointers, copies, stack or external input. Every candidate needs primary consumer attribution.',candidates=out)
Path(__file__).with_suffix('.json').write_text(json.dumps(result,indent=2)+'\n')
print(json.dumps(out,indent=2))
