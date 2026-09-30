import random,struct,math
import numpy as np
from emu2 import full_minAreaRect
from port import *
random.seed(7); bad=0; cnt=0
def same(a,b): return all(struct.pack('<f',float(x))==struct.pack('<f',float(y)) for x,y in zip(a,b))
cases=[]
for t in range(250):
    n=random.randint(3,8)
    pts=[(f32(random.uniform(-5,5)),f32(random.uniform(-5,5))) for _ in range(n)]
    if random.random()<.4: pts=pts+pts
    cases.append(pts)
# cube-like: 8 corners scaled, rotated by yaw and by tilt
def cube(size,yaw,pitch=0,roll=0):
    from itertools import product
    out=[]
    cy,sy=math.cos(yaw),math.sin(yaw);cp,sp=math.cos(pitch),math.sin(pitch);cr,sr=math.cos(roll),math.sin(roll)
    for sx,sy_,sz in product((-.5,.5),repeat=3):
        x,y,z=sx*size[0],sy_*size[1],sz*size[2]
        # roll about x, pitch about y, yaw about z
        y,z=y*cr-z*sr,y*sr+z*cr
        x,z=x*cp+z*sp,-x*sp+z*cp
        x,y=x*cy-y*sy,x*sy+y*cy
        out.append((f32(x),f32(y)))
    return out
for yaw in [0,0.3,math.pi/2,1.0,-0.7,math.pi,2.5]:
    cases.append(cube((44.,44.,44.),yaw))
    cases.append(cube((44.,44.,44.),yaw,0.3,0.2))
for c in cases:
    try: p=minAreaRect(c)
    except AssertionError: continue
    e=full_minAreaRect([(float(x),float(y)) for x,y in c]); cnt+=1
    if not same(p,e): bad+=1; print("MISMATCH",c,p,e)
print("cases",cnt,"bad",bad)
