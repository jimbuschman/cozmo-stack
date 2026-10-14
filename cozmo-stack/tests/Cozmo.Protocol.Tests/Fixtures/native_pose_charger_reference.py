# Independent test oracle from checked pose R3/R4/R17 and the adopted M13 min-area-rect reference.
# No C# production call or observed result is read. Library trig is the allowed host libm boundary.
import ctypes,math,struct,importlib.util,numpy as np,json
from pathlib import Path
spec=importlib.util.spec_from_file_location('native_rect','re-analysis/evidence/m13-minarearect/port.py');rect=importlib.util.module_from_spec(spec);spec.loader.exec_module(rect)
f=np.float32;c=ctypes.CDLL('ucrtbase')
for name in ('cosf','sinf','atan2f'):
 fun=getattr(c,name);fun.argtypes=[ctypes.c_float]*(2 if name=='atan2f' else 1);fun.restype=ctypes.c_float
bits=lambda v:struct.unpack('<I',struct.pack('<f',v))[0]
rows=[]
for yaw,tx,ty in ((0,100,50),(.3,-20,10),(3,250,-75),(-1.2,0,0)):
 half=f(f(yaw)*f(.5));w=float(c.cosf(half));z=float(c.sinf(half));n=w*w;n+=z*z
 if abs(1-n)<struct.unpack('<d',struct.pack('<Q',0x3E56A09E19D672B6))[0]:scale=2/(n+1);w=scale*w;z=scale*z
 else:scale=math.sqrt(n);w=w/scale;z=z/scale
 zz=f(z*z);wz=f(w*z);m00=f(f(f(zz+f(0))*f(-2))+f(1));m11=f(f(1)-f(zz+zz));m01=f(f(0)-wz);m01=f(m01+m01);m10=f(wz+wz)
 for x,y in ((m00,m01),(m10,m11)):
  norm=f(math.sqrt(float(f(f(x*x)+f(y*y)))))
  assert bits(norm)==0x3F800000,'fixture needs the separate checked R18 repair oracle'
 pts=[]
 for x in (0,96):
  for y in (-40,40):
   for zcoord in (0,31):
    px=f(f(f(f(x)*m00)+f(f(y)*m01))+f(f(zcoord)*f(0)))
    py=f(f(f(f(x)*m10)+f(f(y)*m11))+f(f(zcoord)*f(0)))
    pts.append((px,py))
 rr=rect.rr_points(rect.minAreaRect(pts));cx=f(f(f(f(rr[0][0]+rr[2][0])+rr[1][0])+rr[3][0])*f(.25));cy=f(f(f(f(rr[0][1]+rr[2][1])+rr[1][1])+rr[3][1])*f(.25))
 rr.sort(key=lambda p:c.atan2f(f(p[1]-cy),f(p[0]-cx)))
 order=(0,3,1,2);gold=[]
 for i in order:gold.extend((bits(f(f(tx)+rr[i][0])),bits(f(f(ty)+rr[i][1]))))
 rows.append({'yaw':yaw,'tx':tx,'ty':ty,'quad':gold})
 print(yaw,','.join('0x%08Xu'%v for v in gold))
