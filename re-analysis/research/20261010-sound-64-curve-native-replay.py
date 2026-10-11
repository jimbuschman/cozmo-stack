"""Read-only native leaf replay for Q14-115; outputs only research witnesses."""
import sys,json,struct,math,hashlib
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'re-analysis/research/_m5013_dependencies'))
sys.path.insert(0,str(ROOT/'re-analysis/tools/emu'))
sys.dont_write_bytecode=True
from emu_curve import Engine
r=ROOT/'re-analysis/research';data=json.loads((r/'20261010-sound-64-curve-input-witness.json').read_text());engine=Engine();cases=[]
def bits(x):return struct.unpack('<I',struct.pack('<f',x))[0]
def value(x):return struct.unpack('<f',struct.pack('<I',x))[0]
def add(tag,pts,sc,x,hint=0):
 result,idx=engine.run(pts,sc,x,hint)
 cases.append(dict(tag=tag,points=pts,scaling=sc,x=x,hint=hint,result=result,idx=idx))
for row in data['entries']:
 pts=[(int(x,16),int(y,16),s) for x,y,s in row['points']];xs={0x7FC00000,0x7F800000,0xFF800000,0,0x80000000}
 for i,(x,y,s) in enumerate(pts):
  xs.add(x);xs.add((x-1)&0xffffffff);xs.add((x+1)&0xffffffff)
  if i+1<len(pts):
   lo=value(x);hi=value(pts[i+1][0])
   for q in (.001,.25,.5,.75,.999):xs.add(bits(lo+(hi-lo)*q))
 for x in sorted(xs):add(f"shipped:{row['bank']}:{row['objectId']}:{row['ordinal']}",pts,row['scaling'],x)
for shape in list(range(12))+[0xffffffff]:
 for sc in (0,1,2,3,4,5,7):
  for x in (-1.,0.,.001,.25,.5,.75,.999,1.,2.):add(f'shape:{shape}:scale:{sc}',[(bits(0.),bits(0.),shape),(bits(1.),bits(1.),4)],sc,bits(x))
  add(f'shape:{shape}:scale:{sc}:nanx',[(0,0,shape),(bits(1.),bits(1.),4)],sc,0x7fc00000)
for sc in (0,1,2,3,4,5,7):
 for y in (0,0x80000000,bits(.999),bits(-.999),bits(1),bits(-1),bits(1.5),bits(-1.5),bits(-37),bits(-740),bits(38.83),0x7f800000,0xff800000,0x7fc00000):add(f'endpoint:scale:{sc}:y:{y:08X}',[(0,y,4)],sc,0)
for pts in [[(bits(1),bits(3),4),(bits(1),bits(9),4),(bits(2),bits(5),4)],[(bits(2),bits(3),5),(bits(0),bits(9),4),(bits(1),bits(5),4)]]:
 for x in (0.,.5,1.,1.5,2.,3.):
  for h in (0,1,2,100,0xffffffff):add('unsorted/duplicate',pts,0,bits(x),h)
result=dict(engineSha256=hashlib.sha256((ROOT/'resources/lib/armeabi-v7a/libcozmoEngine.so').read_bytes()).hexdigest(),candidateSha256=hashlib.sha256((ROOT/'cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseRtpcCurve.cs').read_bytes()).hexdigest(),cases=cases)
(r/'20261010-sound-64-curve-native-replay.json').write_text(json.dumps(result)+'\n',encoding='utf-8')
print('native cases',len(cases),'shipped',sum(x['tag'].startswith('shipped:') for x in cases))
