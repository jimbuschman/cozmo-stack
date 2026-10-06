"""Reproducible converter/.NET differential corpus. Writes every case and discrepancy."""
import argparse,random,json,subprocess,hashlib,gzip,struct,time
from pathlib import Path
from decimal import Decimal,localcontext
from emu_jsoncpp_double import convert,PATH
ROOT=Path(__file__).resolve().parents[3]
parser=argparse.ArgumentParser();parser.add_argument('--out',type=Path,required=True);parser.add_argument('--random',type=int,default=30000)
a=parser.parse_args();rng=random.Random(0x1029)
cases=[]
def add(category,text,culture=''):cases.append({'category':category,'input':text,'culture':culture})
for i in range(a.random):
 n=rng.randint(1,70);digits=''.join(str(rng.randrange(10)) for _ in range(n));point=rng.randrange(n+1)
 text=digits[:point]+'.'+digits[point:]
 if rng.randrange(2):text='-'+text
 text+='e'+str(rng.randint(-400,330));add('random-decimal',text)
for i in range(2000):
 bits=rng.randrange(1,0x7fefffffffffffff)
 if (bits>>52)==2047:continue
 x=struct.unpack('<d',struct.pack('<Q',bits))[0];y=struct.unpack('<d',struct.pack('<Q',bits+1))[0]
 with localcontext() as ctx:
  ctx.prec=1200;mid=(Decimal(x)+Decimal(y))/2
  add('halfway',str(mid));add('halfway-negative',str(-mid))
for exponent in [-324,-323,-309,-308,307,308,309]:
 for i in range(250):add('boundary',f'{rng.randrange(1,100000000000000000)}e{exponent-16}')
for i in range(1000):
 n=rng.randint(80,600);digits=''.join(str(rng.randrange(10)) for _ in range(n))
 add('long-mantissa',digits[0]+'.'+digits[1:]+'e'+str(rng.randint(-340,310)))
for i in range(1000):add('leading-zero','0'*rng.randint(1,100)+str(rng.randrange(100000000))+'.'+str(rng.randrange(100000000))+'e'+str(rng.randint(-340,310)))
for t in ['1.7976931348623157e308','1.7976931348623159e308','1.8e308','2.2250738585072013e-308','2.2250738585072014e-308','1e-324','2e-324','3e-324','4e-324','5e-324','0.0','-0.0','1e999999999999','-1e999999999999','1e-999999999999','0e99999999999','1e','1e+','1e-','1.','.1','+1.5',' 1.5','1.5 ','','-','00.1','nan','NaN','-nan','nan(123)','nan(0x12)','nan(foo)','inf','-inf','Infinity','infinity','0x1p2','0x1.8p+2','0x1','0x1p','0x0.0000000000001p-1022','1,5','1.5junk','1\u00002','1\u066b5']:
 add('special',t)
for t in ['1,5','1.5','1,234','1.234','-0,0','1,5e2']:add('locale-fr-FR',t,'fr-FR')
requests=''.join(json.dumps(c)+'\n' for c in cases)
dll=Path(__file__).parent/'jsoncpp_dotnet/bin/Debug/net9.0/jsoncpp_dotnet.dll'
run=subprocess.run(['dotnet',str(dll)],input=requests,text=True,capture_output=True,check=True)
net=[json.loads(s) for s in run.stdout.splitlines()];assert len(net)==len(cases)
a.out.mkdir(parents=True,exist_ok=True)
counts={};diffs=0;started=time.time()
with gzip.open(a.out/'20261005-jsoncpp-oracle-cases.jsonl.gz','wt',encoding='utf-8') as allout,open(a.out/'20261005-jsoncpp-oracle-differences.jsonl','w',encoding='utf-8') as differences:
 for i,(case,dotnet) in enumerate(zip(cases,net)):
  native=convert(case['input']);reasons=[]
  raw=native['raw'];wrapper=native['num_get_normalized']
  if dotnet.get('bits')!=raw['bits']:reasons.append('raw-bits-or-format')
  if ('error' in dotnet)!=(wrapper['state']!=0):reasons.append('normalized-acceptance')
  if raw['end_offset']!=len(case['input'].encode()):reasons.append('partial-consumption')
  record={**case,'native':native,'dotnet':dotnet,'differences':reasons}
  allout.write(json.dumps(record)+'\n')
  bucket=counts.setdefault(case['category'],{'cases':0,'differences':0,'bit_differences':0,'acceptance_differences':0})
  bucket['cases']+=1
  if reasons:diffs+=1;bucket['differences']+=1;differences.write(json.dumps(record)+'\n')
  if 'raw-bits-or-format' in reasons:bucket['bit_differences']+=1
  if 'normalized-acceptance' in reasons:bucket['acceptance_differences']+=1
  if i%1000==0:print(f'{i}/{len(cases)} cases, {diffs} discrepancies, {time.time()-started:.1f}s',flush=True)
summary={'cases':len(cases),'discrepancies':diffs,'categories':counts,'seed':'0x1029','binary_sha256':hashlib.sha256(Path(PATH).read_bytes()).hexdigest(),'dotnet_version':subprocess.check_output(['dotnet','--version'],text=True).strip(),'fpscr_in':'00000000','elapsed_seconds':time.time()-started}
(a.out/'20261005-jsoncpp-oracle-summary.json').write_text(json.dumps(summary,indent=2),encoding='utf-8');print(json.dumps(summary,indent=2))
