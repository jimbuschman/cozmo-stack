"""Finite ARM address-composition navigation for the original Q14-033 census.

One seed root is symbolic; candidate overlap never proves the root is a voice.
Stops at a branch/call, invalid word, or 32 following instructions.
"""
from pathlib import Path
import hashlib, json
import capstone, lief
from capstone.arm import *

p = Path(__file__).resolve().parents[2] / 'resources/lib/armeabi-v7a/libcozmoEngine.so'
sha = hashlib.sha256(p.read_bytes()).hexdigest()
assert sha == '02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1'
b = lief.parse(str(p)); lo, hi = 0x97D000, 0xA7AA00
s = next(s for s in b.sections if s.virtual_address <= lo and hi <= s.virtual_address + s.size)
raw = bytes(s.content)[lo-s.virtual_address:hi-s.virtual_address]
md = capstone.Cs(capstone.CS_ARCH_ARM, capstone.CS_MODE_ARM); md.detail=True
def decode(a):
    return next(md.disasm(raw[a-lo:a-lo+4], a, count=1), None)
def arithmetic(i):
    w = int.from_bytes(i.bytes, 'little')
    if (w >> 25) & 7 != 1 or (w >> 21) & 15 not in (2,4): return None
    n, d = (w >> 16) & 15, (w >> 12) & 15
    if n == 15 or d == 15: return None
    x, rotate = w & 255, ((w >> 8) & 15)*2
    x = ((x >> rotate) | (x << ((32-rotate) & 31))) & 0xFFFFFFFF
    return ('r'+str(d), 'r'+str(n), x if (w >> 21) & 15 == 4 else -x)
def name(reg):
    n=md.reg_name(reg)
    return {'sb':'r9','sl':'r10','fp':'r11','ip':'r12','sp':'r13','lr':'r14','pc':'r15'}.get(n,n)
def extent(i, env):
    # Only scalar/paired scalar stores and integer STM are modeled here.
    # Vector/interleaved VST and VSTM widths remain an explicit separate frontier.
    mem = next((o.mem for o in i.operands if o.type==ARM_OP_MEM), None)
    if i.mnemonic.startswith(('str','vstr')) and mem is not None and not mem.index:
        base = env.get(name(mem.base))
        if base is None: return None
        width = (8 if i.mnemonic.startswith('strd') or
                 (i.mnemonic.startswith('vstr') and i.op_str.startswith('d')) else
                 1 if i.mnemonic.startswith('strb') else
                 2 if i.mnemonic.startswith('strh') else 4)
        return base+mem.disp, width
    if i.mnemonic.startswith('stm') and i.operands and i.operands[0].type==ARM_OP_REG:
        base=env.get(name(i.operands[0].reg))
        if base is None: return None
        count=len(i.operands)-1
        w=int.from_bytes(i.bytes,'little');pbit=(w>>24)&1;up=(w>>23)&1
        start=base+(4 if pbit else 0) if up else base-4*(count-1+pbit)
        return start,4*count
    return None
events={}; seeds=0; bounded=[]
for a in range(lo,hi,4):
    i=decode(a)
    if i is None:continue
    ar=arithmetic(i)
    if ar is None or not 0x80<=ar[2]<=0xE8:continue
    dest,root,offset=ar;env={root:0};env[dest]=offset;seeds+=1
    for j in range(1,33):
        q=decode(a+4*j) if a+4*j<hi else None
        if q is None:break
        if q.group(capstone.CS_GRP_JUMP) or q.group(capstone.CS_GRP_CALL) or q.mnemonic.startswith(('pop','push')):break
        span=extent(q,env)
        if span is not None and span[0]<0xE8 and span[0]+span[1]>0xE0:
            key=(a,q.address,span[0],span[1])
            events[key]=dict(seed=f'{a:08X}',seedInstruction=i.mnemonic+' '+i.op_str,
                             symbolicRoot=root,store=f'{q.address:08X}',instruction=q.mnemonic+' '+q.op_str,
                             bytes=q.bytes.hex(),relativeStart=span[0],width=span[1])
        ar2=arithmetic(q);updates={}
        if ar2 is not None and ar2[1] in env:updates[ar2[0]]=env[ar2[1]]+ar2[2]
        elif q.mnemonic.startswith('mov') and len(q.operands)==2 and all(o.type==ARM_OP_REG for o in q.operands):
            if name(q.operands[1].reg) in env:updates[name(q.operands[0].reg)]=env[name(q.operands[1].reg)]
        try:
            for reg in q.regs_access()[1]:env.pop(name(reg),None)
        except capstone.CsError:
            break
        env.update(updates)
    else:bounded.append(dict(seed=f'{a:08X}',last=f'{q.address:08X}',knownRelativeRegisters=env.copy()))
overlaps=[]
for a in range(lo,hi,4):
    i=decode(a)
    if i is None or not i.mnemonic.startswith(('str','vstr')):continue
    mem=next((o.mem for o in i.operands if o.type==ARM_OP_MEM),None)
    if mem is None or mem.index or mem.disp in (0xE0,0xE4):continue
    span=extent(i,{name(mem.base):0})
    if span is not None and span[0]<0xE8 and span[0]+span[1]>0xE0:
        overlaps.append(dict(address=f'{a:08X}',instruction=i.mnemonic+' '+i.op_str,
                             bytes=i.bytes.hex(),relativeStart=span[0],width=span[1]))
assert {x['address'] for x in overlaps}=={'0099EAD8','00A025A4','00A02604','00A0260C'}
assert any(x['store']=='009F7CE0' and x['relativeStart']==0xD8 and x['width']==16 for x in events.values())
out=dict(engineSha256=sha,interval=[f'{lo:08X}',f'{hi:08X}'],seedCount=seeds,
         windowsAt32=bounded,events=list(events.values()),nonExactStartingOverlaps=overlaps,
         limit='Only positive syntactic ARM ADD/SUB-immediate80..E8 roots, straight-line32-instruction windows, MOV aliases and scalar/paired/STM extents. Conditional feasibility, literal/code boundaries, Thumb, VST/VSTM, other seeds, indexed/composite arithmetic, writeback propagation, branch/call paths and concrete receiver admission remain unproved. No absence inference.')
Path(__file__).with_suffix('.json').write_text(json.dumps(out,indent=2)+'\n',encoding='utf-8')
print(json.dumps(dict(seeds=seeds,windowsAt32=bounded,eventCount=len(events),stores=sorted({v['store'] for v in events.values()}))))
