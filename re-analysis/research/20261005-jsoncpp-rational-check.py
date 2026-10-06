from fractions import Fraction
from pathlib import Path
import struct

def fbits(x):
 e=(x>>52)&2047;m=x&((1<<52)-1)
 assert e!=2047
 return Fraction(m+(1<<52) if e else m)*Fraction(2)**(e-1075 if e else -1074)*(-1 if x>>63 else 1)
def round_nearest(x):
 if not x:return 0
 sign=x<0;x=abs(x);e=x.numerator.bit_length()-x.denominator.bit_length()
 if x<Fraction(2)**e:e-=1
 unit=Fraction(2)**(max(e,-1022)-52);q,r=divmod(x.numerator*unit.denominator,x.denominator*unit.numerator);den=x.denominator*unit.numerator
 if r*2>den or (r*2==den and q&1):q+=1
 if q>=1<<53:q>>=1;e+=1
 return (int(sign)<<63)|(((e+1023)<<52)|(q-(1<<52)) if e>=-1022 else q)
candidate=0x7c9ffffffffffffc
ulp=Fraction(2)**(((candidate>>52)&2047)-1023-52)
result=round_nearest(fbits(candidate)+ulp)
print(f'Exact rational scaled candidate + ULP: {result:016X}')
assert result==0x7c9ffffffffffffd
restored=result+(0x3500000<<32)
print(f'Restore exponent high-word: {restored:016X}')
print('This validates this individual nearest-mode add/restore only; not input approximation, ratio helpers or production FPSCR.')
