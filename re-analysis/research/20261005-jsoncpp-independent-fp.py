"""Independent exact-rational checks of executed native VFP arithmetic/comparisons.
Reads oracle tool, writes research results only. Not a second ARM emulator.
"""
import sys,json,struct,capstone
from pathlib import Path
from fractions import Fraction
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'tools/emu'))
import emu_jsoncpp_double as o
from unicorn import UC_HOOK_CODE
from unicorn.arm_const import *
md=capstone.Cs(capstone.CS_ARCH_ARM,capstone.CS_MODE_THUMB)
registers={f'd{i}':UC_ARM_REG_D0+i for i in range(32)}
registers.update({f's{i}':UC_ARM_REG_S0+i for i in range(32)})
def value(b):
 e=(b>>52)&2047;m=b&((1<<52)-1)
 if e==2047:return None
 return Fraction(m+(1<<52) if e else m)*Fraction(2)**(e-1075 if e else -1074)*(-1 if b>>63 else 1)
def nearest(x,negative_zero=False):
 sign=x<0 or (not x and negative_zero);x=abs(x)
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
pending=None;events=[];skipped=[];frames=[];bigchecks=[]
def bigint(pointer):
 n=o.r(pointer+0x10)
 if n>2000:raise AssertionError('invalid bigint length')
 return sum(o.r(pointer+0x14+4*i)<<(32*i) for i in range(n))
def signed(x):return x-(1<<32) if x>>31 else x
def check(uc,a,size,_):
 global pending
 while frames and frames[-1][0]==a:
  ret,pc,kind,expected,extra=frames.pop();actual=o.reg(0)
  if kind=='compare-big':assert (signed(actual)>0)-(signed(actual)<0)==expected,(hex(pc),kind)
  elif kind=='double-big':
   exp=signed(o.r(extra));assert Fraction(bigint(actual))*Fraction(2)**exp==expected,(hex(pc),kind)
  elif kind=='approx-big':
   assert uc.reg_read(UC_ARM_REG_D0)==expected,(hex(pc),kind,hex(uc.reg_read(UC_ARM_REG_D0)),hex(expected))
   assert o.r(extra[0])==extra[1],(hex(pc),'leading-limb-bitcount')
  else:
   assert bigint(actual)==expected,(hex(pc),kind)
   if kind=='diff-big':assert o.r(actual+0xc)==extra,(hex(pc),'difference-sign')
  bigchecks.append({'pc':f'{pc:08X}','kind':kind})
 if a in [0x7FB1C,0x7FCEC,0x7FDE8,0x7FF80,0x7F9FC,0x80CA8,0x7F910,0x81278]:
  r0,r1,r2=o.reg(0),o.reg(1),o.reg(2);extra=None
  if a==0x7F910:kind='double-big';expected=abs(value(uc.reg_read(UC_ARM_REG_D0)));extra=r0
  else:
   x=bigint(r0)
   if a==0x7FB1C:kind='multiply-big';expected=x*bigint(r1)
   elif a==0x7FCEC:kind='shift-big';expected=x<<r1
   elif a==0x7FDE8:
    y=bigint(r1);kind='diff-big';expected=abs(x-y);extra=int(x<y)
   elif a==0x7FF80:
    y=bigint(r1);kind='compare-big';expected=(x>y)-(x<y)
   elif a==0x7F9FC:kind='pow5-big';expected=x*5**r1
   elif a==0x80CA8:kind='muladd-big';expected=x*r1+r2
   else:
    kind='approx-big';bits=x.bit_length();coeff=(x>>(bits-53)) if bits>=53 else (x<<(53-bits));expected=0x3ff0000000000000|(coeff&((1<<52)-1));extra=(r1,((bits-1)%32)+1)
  frames.append((uc.reg_read(UC_ARM_REG_LR)&~1,a,kind,expected,extra))
 if pending:
  pc,mn,kind,reg,expected=pending
  actual=(uc.reg_read(UC_ARM_REG_FPSCR)&0xf0000000) if kind=='compare' else uc.reg_read(reg)
  if actual!=expected:raise AssertionError(f'{pc:08X} {mn}: expected{expected:016X} actual{actual:016X}')
  events.append({'pc':f'{pc:08X}','instruction':mn,'expected':f'{expected:016X}','kind':kind})
  pending=None
 if a>=0xb0000:return
 ins=next(md.disasm(bytes(uc.mem_read(a,size)),a),None)
 if not ins:return
 if ins.mnemonic in ['vadd.f64','vsub.f64','vmul.f64','vdiv.f64']:
  if uc.reg_read(UC_ARM_REG_FPSCR)&0x3c00000:raise AssertionError('requires nearest/no-FZ/no-DN')
  dest,left,right=ins.op_str.split(', ');lb=uc.reg_read(registers[left]);rb=uc.reg_read(registers[right]);x,y=value(lb),value(rb)
  if x is None or y is None or (ins.mnemonic=='vdiv.f64' and not y):skipped.append(f'{a:08X} {ins.mnemonic} nonfinite/divzero');return
  mn=ins.mnemonic
  z=x+y if mn=='vadd.f64' else x-y if mn=='vsub.f64' else x*y if mn=='vmul.f64' else x/y
  nz=bool((lb^rb)>>63) if mn in ['vmul.f64','vdiv.f64'] else bool((lb>>63) and ((rb>>63) if mn=='vadd.f64' else not(rb>>63)))
  pending=(a,mn,'arithmetic',registers[dest],nearest(z,nz))
 elif ins.mnemonic in ['vcvt.f64.u32','vcvt.f64.s32','vcvt.s32.f64']:
  dest,source=ins.op_str.split(', ');b=uc.reg_read(registers[source])
  if ins.mnemonic=='vcvt.s32.f64':
   x=value(b)
   if x is None or not -(1<<31)<=x<(1<<31):skipped.append(f'{a:08X} out-of-range VCVT');return
   expected=int(x)&0xffffffff
  else:expected=nearest(Fraction(signed(b) if ins.mnemonic=='vcvt.f64.s32' else b))
  pending=(a,ins.mnemonic,'conversion',registers[dest],expected)
 elif ins.mnemonic in ['vcmp.f64','vcmpe.f64']:
  left,right=ins.op_str.split(', ');lb=uc.reg_read(registers[left]);rb=0 if right=='#0' else uc.reg_read(registers[right]);x,y=value(lb),value(rb)
  if x is None or y is None:
   # NaN versus infinity are distinct; do not hide unsupported infinities.
   if ((lb>>52)&2047)==2047 and lb&((1<<52)-1) or ((rb>>52)&2047)==2047 and rb&((1<<52)-1):flags=0x30000000
   else:skipped.append(f'{a:08X} compare infinity');return
  else:flags=0x80000000 if x<y else 0x60000000 if x==y else 0x20000000
  pending=(a,ins.mnemonic,'compare',0,flags)
o.uc.hook_add(UC_HOOK_CODE,check)
examples=json.loads((Path(__file__).parent/'20261005-jsoncpp-oracle-rational-summary.json').read_text())['examples']
cases=[x['input'] for x in examples]+['0.1','1.7976931348623157e308','2.2250738585072014e-308','3e-324','9007199254740993.0']
import random
rows=[json.loads(line) for line in (Path(__file__).parent/'20261005-jsoncpp-oracle-differences.jsonl').open(encoding='utf-8')]
eligible=[row['input'] for row in rows if 'raw-bits-or-format' in row['differences'] and row['category'] not in ['special','locale-fr-FR']]
cases=list(dict.fromkeys(cases+random.Random(0x102905).sample(eligible,100)))
output=[]
for text in cases:
 events=[];skipped=[];pending=None;frames=[];bigchecks=[]
 result=o.convert(text)
 assert not frames, 'unreturned bigint frame'
 output.append({'input':text,'result':result,'checks':len(events),'skipped':skipped,'events':events,'bigint_checks':bigchecks})
 print(text,len(events),'FP checks',len(bigchecks),'bigint checks',len(skipped),'skips',flush=True)
(Path(__file__).parent/'20261005-jsoncpp-independent-fp-results.json').write_text(json.dumps(output,indent=2),encoding='utf-8')
print('PASS exact arithmetic and comparison checks on every covered executed operation')
