"""Q13 component corpus: actual ARM mixer, no math or engine substitutes.

This is not an event-to-robot renderer. Inputs are synthetic caller buffers.
Run with Python and the same local Unicorn dependencies as emu_m1_m5_numeric.py.
"""
from emu_m1_m5_numeric import Native, ROOT
from unicorn.arm_const import *
import random, struct, json, hashlib

def arm(n,address,*args):
    n.uc.reg_write(UC_ARM_REG_SP,0x210f0000)
    n.uc.reg_write(UC_ARM_REG_LR,0x22000001)
    n.uc.reg_write(UC_ARM_REG_FPSCR,0)
    for register,value in zip([UC_ARM_REG_R0,UC_ARM_REG_R1,UC_ARM_REG_R2,UC_ARM_REG_R3],args):
        n.uc.reg_write(register,value)
    for index,value in enumerate(args[4:]):n.put(0x210f0000+index*4,value)
    n.uc.emu_start(address,0x22000000,count=1000000)
    if n.uc.reg_read(UC_ARM_REG_PC)&~1!=0x22000000:raise RuntimeError('instruction limit')
    return n.uc.reg_read(UC_ARM_REG_R0)

def generate():
    n=Native(); rng=random.Random(0x20261007); rows=[]
    for case in range(128):
        frames=[8,16,32,64,128,1024][case%6]
        # All expected words below come from shipped instructions, not arithmetic here.
        src=[struct.unpack('<I',struct.pack('<f',rng.uniform(-1,1)))[0] for _ in range(frames)]
        dst=[struct.unpack('<I',struct.pack('<f',rng.uniform(-1,1)))[0] for _ in range(frames)]
        start=[0,0x80000000,0x3f800000,0x3f000000,0xbf800000,1,0x00800000,0x3eaaaaab][case%8]
        inc=[0,0x80000000,0x3b800000,0xbb800000,1,0x00800000,0x3a83126f][case%7]
        n.uc.mem_write(0x20000000,struct.pack('<%dI'%frames,*src))
        n.uc.mem_write(0x20010000,struct.pack('<%dI'%frames,*dst))
        arm(n,0xa46668,0x20000000,0x20010000,start,inc,frames)
        rows.append(dict(frames=frames,start=start,increment=inc,source=src,destination=dst,
                         expected=list(struct.unpack('<%dI'%frames,bytes(n.uc.mem_read(0x20010000,frames*4))))))
    out=ROOT/'cozmo-stack/tests/Cozmo.Protocol.Tests/Fixtures/adp_strict_mixer.json'
    out.write_text(json.dumps(dict(generator='emu_adp_strict.py',seed=0x20261007,
        library_sha256=hashlib.sha256(n.path.read_bytes()).hexdigest(),address='0x00A46668',rows=rows),indent=2)+'\n')
    eq=[]
    for case in range(64):
        channels=1+case%2;frames=[0,1,2,15,64,128][case%6];stride=max(4,frames+3)
        source=[struct.unpack('<I',struct.pack('<f',rng.uniform(-1,1)))[0] for _ in range(channels*stride)]
        history=[struct.unpack('<I',struct.pack('<f',rng.uniform(-0.5,0.5)))[0] for _ in range(channels*4)]
        coefficients=[[0x3f800000,0,0,0,0],[0x3e800000,0x3e800000,0x3e800000,0,0],
                      [0x3e800000,0x3e000000,0x3dcccccd,0x3dcccccd,0xbd4ccccd]][case%3]
        if case in [1,2,3]:source=[0]*len(source);history=[0]*len(history)
        n.uc.mem_write(0x20000000,struct.pack('<%dI'%len(source),*source))
        n.uc.mem_write(0x20030000,struct.pack('<IIIHH',0x20000000,channels,0,stride,frames))
        n.uc.mem_write(0x20031000,struct.pack('<5I',*coefficients))
        n.uc.mem_write(0x20032000,struct.pack('<%dI'%len(history),*history))
        arm(n,0xaa2324,0x20030000,0x20031000,0x20032000,channels)
        eq.append(dict(channels=channels,frames=frames,stride=stride,source=source,history=history,coefficients=coefficients,
            expected=list(struct.unpack('<%dI'%len(source),bytes(n.uc.mem_read(0x20000000,len(source)*4)))),
            expected_history=list(struct.unpack('<%dI'%len(history),bytes(n.uc.mem_read(0x20032000,len(history)*4))))))
    (out.parent/'adp_strict_biquad.json').write_text(json.dumps(dict(generator='emu_adp_strict.py',seed=0x20261007,
        library_sha256=hashlib.sha256(n.path.read_bytes()).hexdigest(),address='0x00AA2324',rows=eq),indent=2)+'\n')
    filters=[]
    for case in range(32):
        kind=case//16;address=[0xa766f0,0xa77480][kind]
        channels=1+case%2;frames=[0,1,2,3,4,7,8,15,32,128][case%10];stride=max(4,frames+3);align=4*(case%4)
        source=[struct.unpack('<I',struct.pack('<f',rng.uniform(-1,1)))[0] for _ in range(channels*stride)]
        history=[struct.unpack('<I',struct.pack('<f',rng.uniform(-0.5,0.5)))[0] for _ in range(channels*4)]
        coefficients=[struct.unpack('<I',struct.pack('<f',rng.uniform(-0.1,0.1)))[0] for _ in range(37)]
        if case in [1,2,17,18]:source=[0]*len(source);history=[0]*len(history)
        node=0x20040000;band=node+[0x170,0x180][kind];foff=node+[0x10,0xc0][kind]
        n.uc.mem_write(node,bytes(0x200))
        n.uc.mem_write(foff,struct.pack('<37I',*coefficients))
        n.put(foff+0xa0,0x20050000)
        n.uc.mem_write(0x20050000,struct.pack('<%dI'%len(history),*history))
        # Ready steady-state caller block. No design, allocation or parameter mapping is claimed.
        bypass=1 if case%8==7 else 0
        n.uc.mem_write(band,struct.pack('<IIHBBBBH',0x42200000,0x42200000,8,0,0,0,bypass,0))
        n.put(node+0x190,channels)
        n.put(0x105243c,22320);n.put(0x105244c,8)
        n.uc.mem_write(0x20000000+align,struct.pack('<%dI'%len(source),*source))
        n.uc.mem_write(0x20030000,struct.pack('<IIIHH',0x20000000+align,channels,0,stride,frames))
        arm(n,address,node,0x20030000)
        filters.append(dict(kind=kind,channels=channels,frames=frames,stride=stride,align=align,bypass=bypass,
            source=source,history=history,coefficients=coefficients,
            expected=list(struct.unpack('<%dI'%len(source),bytes(n.uc.mem_read(0x20000000+align,len(source)*4)))),
            expected_history=list(struct.unpack('<%dI'%len(history),bytes(n.uc.mem_read(0x20050000,len(history)*4))))))
    (out.parent/'adp_strict_filters.json').write_text(json.dumps(dict(generator='emu_adp_strict.py',seed=0x20261007,
        library_sha256=hashlib.sha256(n.path.read_bytes()).hexdigest(),addresses=['0x00A766F0','0x00A77480'],rows=filters),indent=2)+'\n')
    probes=[]
    for address in [0xa7a3d8,0xa7a4ac]:
        n.put(0x105243c,22320)
        for word in [0,0x41f00000,0x42480000,0x42c80000,0x7fc00000,0x7f800000]:
            row=dict(address=hex(address),rate=22320,input=word)
            try:row['expected']=arm(n,address,word,0,0)
            except Exception as e:row['blocked']=str(e)
            probes.append(row)
    n.uc.mem_write(0x20000000,bytes(0x100));n.put(0x20000048,22320)
    record=[0,0x3f800000,0x447a0000,0x3f3504f3]
    n.uc.mem_write(0x20001000,struct.pack('<4I',*record))
    row=dict(address='0xaa25e0',object_rate=22320,band=0,record=record)
    try:
        arm(n,0xaa25e0,0x20000000,0,0x20001000)
        row['expected']=[n.word(0x20000004+i*4) for i in range(15)]
    except Exception as e:row['blocked']=str(e)
    probes.append(row)
    (ROOT/'re-analysis/research/20261007-adp-boundary-probes.json').write_text(json.dumps(probes,indent=2)+'\n')
    print('complete shipped calls: mixer',len(rows),'biquad',len(eq),'filters',len(filters))
if __name__=='__main__':generate()
