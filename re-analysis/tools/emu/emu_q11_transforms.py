"""Q11 expected outputs: shipped budget function and CompressRLE; no engine models."""
from emu_m1_m5_numeric import Native, ROOT
import json, struct, hashlib, random

def pattern():
    # Independent explicit test input; this does not compute the expected compression.
    bitmap=bytearray(128*32)
    for y in range(32):
        for x in range(128):
            if x in [0,127] or y in [0,31] or (56<=x<=71 and 12<=y<=19):bitmap[y*128+x]=1
    for x0,x1 in [(0,127),(127,0)]:
        x,y=x0,0;dx=abs(x1-x);dy=-31;sx=1 if x<x1 else -1;err=dx+dy
        while True:
            bitmap[y*128+x]=1
            if x==x1 and y==31:break
            e=2*err
            if e>=dy:err+=dy;x+=sx
            if e<=dx:err+=dx;y+=1
    return bytes(bitmap)

def generate():
    n=Native();rows=[];blocked=[]
    for played_bytes in [0,10000,50000]:
        for played_frames in [0,3,5,14,15,100,0xffffffff,0xfffffff1,0x7fffffff,0x80000000]:
            n.uc.mem_write(0x20000000,bytes(0x400));n.uc.mem_write(0x20001000,bytes(0x400))
            n.uc.mem_write(0x20001238,struct.pack('<4I',played_bytes,0,played_frames,0))
            n.run(0x57c6f0,0x20000000,0x20001000)
            rows.append({'function':'budget','played_bytes':played_bytes,'played_frames':played_frames,
                'streamed_bytes':0,'streamed_frames':0,'expected_bytes':n.word(0x20000098),'expected_frames':n.word(0x2000009c)})
    rng=random.Random(0x20261007)
    cases=[('blank',bytes(4096)),('filled',bytes([1])*4096),('test-pattern',pattern()),
           ('alternating',bytes(i%2 for i in range(4096))),('noise',bytes(rng.randrange(2) for i in range(4096)))]
    for label,bitmap in cases:
        canvas=bytearray(8192)
        for y in range(32):canvas[256*y:256*y+128]=bitmap[128*y:128*y+128]
        n.uc.mem_write(0x20002000,bytes(canvas));n.uc.mem_write(0x20000000,bytes(0x60))
        n.put(0x20000004,0x42ff4000);n.put(0x20000008,2);n.put(0x2000000c,64);n.put(0x20000010,128)
        n.put(0x20000014,0x20002000);n.put(0x20000018,0x20002000)
        n.put(0x2000001c,0x20004000);n.put(0x20000020,0x20004000)
        n.put(0x20000030,0x20000100);n.put(0x20000100,128);n.put(0x20000104,1)
        n.uc.mem_write(0x20000200,bytes(12))
        try:
            result,_=n.run(0x581904,0x20000000,0x20000200)
            begin,end=n.word(0x20000200),n.word(0x20000204)
            rows.append({'function':'compress_rle','case':label,'bitmap':bitmap.hex(),'canvas':bytes(canvas).hex(),
                'expected_return':result,'expected':bytes(n.uc.mem_read(begin,end-begin)).hex()})
        except RuntimeError as e:blocked.append({'function':'compress_rle','case':label,'reason':str(e)})
    for name in ['DEFAULT','RED','GREEN','BLUE','BLACK','red']:
        n.uc.mem_write(0x20005000,bytes([2*len(name)])+name.encode()+bytes(12-1-len(name)))
        try:
            pointer,_=n.run(0x83f780,0x20005000)
            rows.append({'function':'named_color','input':name,'expected_bytes':bytes(n.uc.mem_read(pointer,4)).hex()})
        except RuntimeError as e:blocked.append({'function':'named_color','input':name,'reason':str(e)})
    dest=ROOT/'cozmo-stack/tests/Cozmo.Protocol.Tests/Fixtures/q11_transforms_oracle.json'
    dest.write_text(json.dumps({'engine_sha256':hashlib.sha256(n.path.read_bytes()).hexdigest(),
        'generator':'re-analysis/tools/emu/emu_q11_transforms.py','rows':rows,'blocked':blocked},indent=2)+'\n')
    print(json.dumps({'rows':len(rows),'blocked':blocked}))

if __name__=='__main__':generate()
