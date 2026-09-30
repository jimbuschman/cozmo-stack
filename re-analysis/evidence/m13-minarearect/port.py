import numpy as np, math, functools
f32=np.float32
def sign(v): return (1 if v>0 else 0)-(1 if v<0 else 0)
def sklansky(arr,start,end,nsign,sign2):
    incr=1 if end>start else -1
    stack=[0]*(len(arr)+3)
    pprev=start;pcur=pprev+incr;pnext=pcur+incr;ss=3
    if start==end or (arr[start][0]==arr[end][0] and arr[start][1]==arr[end][1]):
        return [start]
    stack[0]=pprev;stack[1]=pcur;stack[2]=pnext
    end+=incr
    while pnext!=end:
        cury=arr[pcur][1];nexty=arr[pnext][1];by=f32(nexty-cury)
        if sign(by)!=nsign:
            ax=f32(arr[pcur][0]-arr[pprev][0]);bx=f32(arr[pnext][0]-arr[pcur][0]);ay=f32(cury-arr[pprev][1])
            conv=f32(f32(ay*bx)*f32(1)-f32(ax*by)) if False else f32(f32(ay*bx)-f32(ax*by))
            if sign(conv)==sign2 and (ax!=0 or ay!=0):
                pprev=pcur;pcur=pnext;pnext+=incr;stack[ss]=pnext;ss+=1
            else:
                if pprev==start:
                    pcur=pnext;stack[1]=pcur;pnext+=incr;stack[2]=pnext
                else:
                    stack[ss-2]=pnext;pcur=pprev;pprev=stack[ss-4];ss-=1
        else:
            pnext+=incr;stack[ss-1]=pnext
    ss-=1
    return stack[:ss]
def cmp_lt(a,b): return a[0]<b[0] or (a[0]==b[0] and a[1]<b[1])
def convex_hull(points,clockwise=True):
    pts=[(f32(x),f32(y)) for x,y in points]
    total=len(pts)
    arr=sorted(pts,key=functools.cmp_to_key(lambda a,b:-1 if cmp_lt(a,b) else (1 if cmp_lt(b,a) else 0)))
    miny=0;maxy=0
    for i in range(1,total):
        y=arr[i][1]
        if arr[miny][1]>y: miny=i
        if arr[maxy][1]<y: maxy=i
    out=[]
    if arr[0][0]==arr[total-1][0] and arr[0][1]==arr[total-1][1]:
        return [arr[0]]
    tl=sklansky(arr,0,maxy,-1,1); tr=sklansky(arr,total-1,maxy,-1,-1)
    if not clockwise: tl,tr=tr,tl
    for i in range(len(tl)-1): out.append(arr[tl[i]])
    for i in range(len(tr)-1,0,-1): out.append(arr[tr[i]])
    stop=tr[1] if len(tr)>2 else (tl[len(tl)-2] if len(tl)>2 else -1)
    bl=sklansky(arr,0,miny,1,-1); br=sklansky(arr,total-1,miny,1,1)
    if clockwise: bl,br=br,bl
    if stop>=0:
        chk=bl[1] if len(bl)>2 else (br[2-len(bl)] if len(bl)+len(br)>2 else -1)
        if chk==stop or (chk>=0 and arr[chk][0]==arr[stop][0] and arr[chk][1]==arr[stop][1]):
            bl=bl[:2];br=br[:2]
    for i in range(len(bl)-1): out.append(arr[bl[i]])
    for i in range(len(br)-1,0,-1): out.append(arr[br[i]])
    return out
def calipers(h):
    n=len(h); F=f32
    left=right=top=bottom=0
    x0,y0=h[0]; left_x=right_x=x0; top_y=bottom_y=y0
    vect=[None]*n; inv=[None]*n
    p0=h[0]
    for i in range(n):
        if p0[0]<left_x: left_x=p0[0];left=i
        if p0[0]>right_x: right_x=p0[0];right=i
        if p0[1]>top_y: top_y=p0[1];top=i
        if p0[1]<bottom_y: bottom_y=p0[1];bottom=i
        pt=h[(i+1) if i+1<n else 0]
        dx=F(pt[0]-p0[0]);dy=F(pt[1]-p0[1])
        vect[i]=(dx,dy)
        s=float(dy)*float(dy)+float(dx)*float(dx)
        inv[i]=F(1.0/math.sqrt(s))
        p0=pt
    ax=float(vect[n-1][0]);ay=float(vect[n-1][1]);orient=0
    for i in range(n):
        bx=float(vect[i][0]);by=float(vect[i][1])
        c=ax*by-ay*bx
        if c!=0: orient=F(1) if c>0 else F(-1);break
        ax,ay=bx,by
    assert orient!=0
    base_a=orient;base_b=F(0)
    seq=[bottom,right,top,left]
    minarea=F(3.4028234663852886e38);buf=None
    for k in range(n):
        vx=[vect[s][0] for s in seq];vy=[vect[s][1] for s in seq]
        dp=[F(F(base_b*vy[0])+F(base_a*vx[0])),
            F(F(base_a*vy[1])-F(base_b*vx[1])),
            F(-F(base_b*vy[2])-F(base_a*vx[2])),
            F(-F(base_a*vy[3])+F(base_b*vx[3]))]
        # order of vnmls for [3]: -(a*y3)+b*x3
        cos=[F(dp[i]*inv[seq[i]]) for i in range(4)]
        main=0;mx=cos[0]
        for i in range(1,4):
            if cos[i]>mx: main=i;mx=cos[i]
        s=seq[main]
        lx=F(inv[s]*vect[s][0]);ly=F(inv[s]*vect[s][1])
        if main==0: base_a,base_b=lx,ly
        elif main==1: base_a,base_b=ly,-lx
        elif main==2: base_a,base_b=-lx,-ly
        else: base_a,base_b=-ly,lx
        seq[main]=0 if seq[main]+1==n else seq[main]+1
        dx=F(h[seq[1]][0]-h[seq[3]][0]);dy=F(h[seq[1]][1]-h[seq[3]][1])
        width=F(F(dy*base_b)+F(dx*base_a))
        dx=F(h[seq[2]][0]-h[seq[0]][0]);dy=F(h[seq[2]][1]-h[seq[0]][1])
        height=F(F(dy*base_a)-F(dx*base_b))
        area=F(width*height)
        if not (area>minarea):   # bhi skip when unordered or greater
            minarea=area;buf=(seq[3],base_a,width,base_b,height,seq[0],area)
    li,A1,width,B1,height,bi,_=buf
    A2=F(-B1);B2=A1
    C1=F(F(B1*h[li][1])+F(A1*h[li][0]))
    C2=F(F(A1*h[bi][1])+F(A2*h[bi][0]))
    idet=F(F(1)/F(F(-F(A2*B1))+F(A1*A1)))
    px=F(F(F(-F(C2*B1))+F(C1*A1))*idet)   # C1*B2 - C2*B1, B2==A1
    py=F(F(F(-F(A2*C1))+F(A1*C2))*idet)
    o1=(F(A1*width),F(B1*width)); o2=(F(A2*height),F(B2*height))
    cx=F(px+F(F(o1[0]+o2[0])*F(0.5))); cy=F(py+F(F(o1[1]+o2[1])*F(0.5)))
    w=F(math.sqrt(float(o1[1])**2+float(o1[0])**2)); hh=F(math.sqrt(float(o2[1])**2+float(o2[0])**2))
    ang=F(math.atan2(float(o1[1]),float(o1[0])))
    ang=F(float(F(ang*F(180)))/math.pi)
    return (cx,cy,w,hh,ang)
def rr_points(r):
    cx,cy,w,h,ang=r; F=f32
    _a=float(ang)*math.pi/180.0
    b=F(F(math.cos(_a))*F(0.5)); a=F(F(math.sin(_a))*F(0.5))
    p0=(F(F(cx-F(a*h))-F(b*w)),F(F(cy+F(b*h))-F(a*w)))
    p1=(F(F(cx+F(a*h))-F(b*w)),F(F(cy-F(b*h))-F(a*w)))
    p2=(F(F(cx+cx)-p0[0]),F(F(cy+cy)-p0[1]));p3=(F(F(cx+cx)-p1[0]),F(F(cy+cy)-p1[1]))
    return [p0,p1,p2,p3]
def minAreaRect(points):
    h=convex_hull(points,True)
    assert len(h)>2
    return calipers(h)
