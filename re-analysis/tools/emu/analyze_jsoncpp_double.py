"""Independently round exact decimal rationals for all ordinary bit mismatches."""
import json,csv,sys
from fractions import Fraction
from decimal import Decimal
from pathlib import Path
out=Path(sys.argv[1]);stats={};examples=[]
def nearest(text):
 x=Fraction(Decimal(text));sign=text.startswith('-');x=abs(x)
 if not x:return int(sign)<<63
 e=x.numerator.bit_length()-x.denominator.bit_length()
 if x<Fraction(2)**e:e-=1
 if e>1023:return (int(sign)<<63)|0x7ff0000000000000
 unit=Fraction(2)**(max(e,-1022)-52)
 q,r=divmod(x.numerator*unit.denominator,x.denominator*unit.numerator);den=x.denominator*unit.numerator
 if r*2>den or (r*2==den and q&1):q+=1
 if q>=1<<53:q>>=1;e+=1
 if e>1023:return (int(sign)<<63)|0x7ff0000000000000
 return (int(sign)<<63)|(((e+1023)<<52)|(q-(1<<52)) if e>=-1022 else q)
with open(out/'20261005-jsoncpp-oracle-differences.tsv','w',encoding='utf-8',newline='') as stream:
 writer=csv.writer(stream,delimiter='\t');writer.writerow(['category','input-json','native-bits','dotnet-bits-or-error','errno','normalized-state','byte-end-offset','differences','exact-nearest-even'])
 for line in (out/'20261005-jsoncpp-oracle-differences.jsonl').open(encoding='utf-8'):
  row=json.loads(line);raw=row['native']['raw'];net=row['dotnet'];expected='not-applicable'
  if 'raw-bits-or-format' in row['differences'] and row['category'] not in ['special','locale-fr-FR']:
   expected=f"{nearest(row['input']):016X}"
   key=('dotnet-correct' if expected==net.get('bits') else 'dotnet-not-nearest')+' / '+('native-correct' if expected==raw['bits'] else 'native-not-nearest')
   stats[key]=stats.get(key,0)+1
   if len(examples)<12:examples.append({'input':row['input'],'native':raw['bits'],'dotnet':net.get('bits'),'nearest_even':expected,'category':row['category']})
  writer.writerow([row['category'],json.dumps(row['input']),raw['bits'],net.get('bits',net.get('error')),raw['errno'],row['native']['num_get_normalized']['state'],raw['end_offset'],','.join(row['differences']),expected])
result={'bit_mismatch_exact_rational_checks':stats,'examples':examples};(out/'20261005-jsoncpp-oracle-rational-summary.json').write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps(result,indent=2))
