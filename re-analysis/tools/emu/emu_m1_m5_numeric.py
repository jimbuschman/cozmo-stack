"""Strict shipped-instruction numeric oracle. Unknown imports fail closed.

Run from any directory; install Unicorn in research/_m5013_dependencies.
No engine routine or phone math import is replaced with a Python model.
"""
from pathlib import Path
import sys, struct, json, hashlib, random
ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT/'re-analysis/research/_m5013_dependencies'))
import lief
from unicorn import Uc, UC_ARCH_ARM, UC_MODE_ARM, UC_HOOK_CODE
from unicorn.arm_const import *

class Native:
    def __init__(self, library='libcozmoEngine.so'):
        self.path = ROOT/'resources/lib/armeabi-v7a'/library
        self.elf = lief.parse(str(self.path))
        self.uc = Uc(UC_ARCH_ARM, UC_MODE_ARM)
        pages = set()
        for s in self.elf.segments:
            if s.type != lief.ELF.Segment.TYPE.LOAD: continue
            for p in range(s.virtual_address & ~4095, (s.virtual_address+s.virtual_size+4095)&~4095, 4096):
                if p not in pages: self.uc.mem_map(p,4096); pages.add(p)
            self.uc.mem_write(s.virtual_address,bytes(s.content))
        for p in [0x20000000,0x21000000,0x22000000,0x23000000]: self.uc.mem_map(p,0x100000)
        self.uc.reg_write(UC_ARM_REG_C1_C0_2, 0xf << 20)
        self.uc.reg_write(UC_ARM_REG_FPEXC, 0x40000000)
        self.imports = {}
        self.heap = 0x20080000
        byname = {}
        for r in self.elf.relocations:
            if r.type.name not in ['ARM_ABS32','ARM_GLOB_DAT','ARM_JUMP_SLOT'] or not r.has_symbol: continue
            v = r.symbol.value
            if not v:
                name = r.symbol.name
                if name not in byname:
                    a = 0x23000000+4*len(byname); byname[name]=a; self.imports[a]=name
                    self.uc.mem_write(a,struct.pack('<I',0xe12fff1e))
                v = byname[name]
            if r.type.name == 'ARM_ABS32': v += self.word(r.address)
            self.put(r.address,v)
        self.uc.hook_add(UC_HOOK_CODE,self.hook,begin=0x23000000,end=0x230fffff)
        self.uc.hook_add(UC_HOOK_CODE,self.hook,begin=0x22000000,end=0x22000000)
        self.uc.hook_add(UC_HOOK_CODE,lambda u,a,s,d:u.emu_stop(),begin=0x4fac10,end=0x4fac10)
        self.load_shipped_division()
    def load_shipped_division(self):
        """Resolve the compiler division import to its actual shipped STLport body."""
        self.div_path=ROOT/'resources/lib/armeabi-v7a/libstlport_shared.so'
        elf=lief.parse(str(self.div_path)); base=0x30000000; pages=set()
        for s in elf.segments:
            if s.type != lief.ELF.Segment.TYPE.LOAD: continue
            for p in range((base+s.virtual_address)&~4095,(base+s.virtual_address+s.virtual_size+4095)&~4095,4096):
                if p not in pages: self.uc.mem_map(p,4096);pages.add(p)
            self.uc.mem_write(base+s.virtual_address,bytes(s.content))
        own={s.name:base+s.value for s in elf.symbols if s.value}
        for r in elf.relocations:
            a=base+r.address
            if r.type.name == 'ARM_RELATIVE': self.put(a,self.word(a)+base)
            elif r.type.name in ['ARM_ABS32','ARM_GLOB_DAT','ARM_JUMP_SLOT'] and r.has_symbol:
                value=own.get(r.symbol.name)
                if value is None:
                    address=0x23000000+4*len(self.imports)
                    self.imports[address]=r.symbol.name;value=address
                    self.uc.mem_write(address,struct.pack('<I',0xe12fff1e))
                if r.type.name == 'ARM_ABS32': value += self.word(a)
                self.put(a,value)
        for r in self.elf.relocations:
            if r.has_symbol and r.symbol.name=='__aeabi_ldivmod' and not r.symbol.value:
                assert r.type.name in ['ARM_GLOB_DAT','ARM_JUMP_SLOT']
                self.put(r.address,own['__aeabi_ldivmod'])
    def put(self,a,v): self.uc.mem_write(a,struct.pack('<I',v & 0xffffffff))
    def word(self,a): return struct.unpack('<I',self.uc.mem_read(a,4))[0]
    def hook(self,uc,a,size,data):
        if a == 0x22000000: uc.emu_stop()
        if a in self.imports:
            name=self.imports[a]
            args=[uc.reg_read(r) for r in [UC_ARM_REG_R0,UC_ARM_REG_R1,UC_ARM_REG_R2]]
            if name in ['__aeabi_memclr4','__aeabi_memclr']: uc.mem_write(args[0],bytes(args[1]))
            elif name in ['__aeabi_memcpy4','__aeabi_memcpy','__aeabi_memmove']:
                uc.mem_write(args[0],bytes(uc.mem_read(args[1],args[2])))
            elif name == '__cxa_guard_acquire': uc.reg_write(UC_ARM_REG_R0,0 if self.word(args[0]) & 1 else 1)
            elif name == '__cxa_guard_release': self.put(args[0],1)
            elif name == '__cxa_atexit': uc.reg_write(UC_ARM_REG_R0,0)
            elif name in ['_Znwj','_Znaj']:
                p=self.heap; self.heap += (max(1,args[0])+15)&~15
                if self.heap >= 0x20100000: raise RuntimeError('phone allocation arena exhausted')
                uc.mem_write(p,bytes(args[0])); uc.reg_write(UC_ARM_REG_R0,p)
            elif name in ['_ZdlPv','_ZdaPv']: pass
            else: raise RuntimeError('unsupported import: '+name)
    def run(self,a,*args):
        self.uc.reg_write(UC_ARM_REG_SP,0x210f0000)
        self.uc.reg_write(UC_ARM_REG_LR,0x22000001)
        self.uc.reg_write(UC_ARM_REG_FPSCR,0)
        for r,v in zip([UC_ARM_REG_R0,UC_ARM_REG_R1,UC_ARM_REG_R2,UC_ARM_REG_R3],args): self.uc.reg_write(r,v)
        for i,v in enumerate(args[4:]): self.put(0x210f0000+4*i,v)
        self.uc.emu_start(a|1,0x22000000,count=200000)
        if self.uc.reg_read(UC_ARM_REG_PC)&~1 != 0x22000000: raise RuntimeError('instruction limit')
        return self.uc.reg_read(UC_ARM_REG_R0),self.uc.reg_read(UC_ARM_REG_R1)

def generate():
    n = Native(); rng = random.Random(0x20261007)
    edges = [0,0x80000000,0x7f800000,0xff800000,0x7fc00000,0x7fa00001,1,0x80000001,
             0x40490fda,0x40490fdb,0x40490fdc,0xc0490fda,0xc0490fdb,0xc0490fdc,
             0x411fffff,0x41200000,0xc1200000,0x7f7fffff,0xff7fffff]
    inputs = edges+[rng.getrandbits(32) for _ in range(2048)]
    rows=[]; blocked=[]
    # Install the shipped no-warning callback, rather than invoking an uninitialized logger.
    n.run(0x584b48,0)
    colors=[[0,0,0,0],[255,0,0,255],[0,255,0,255],[0,0,255,255],[255,255,255,255]]
    for channel in range(4):
        for value in range(256):
            c=[0,0,0,0];c[channel]=value;colors.append(c)
    colors += [[rng.randrange(256) for _ in range(4)] for i in range(1024)]
    for c in colors:
        n.uc.reg_write(UC_ARM_REG_SP,0x210f0000)
        n.uc.mem_write(0x210f0018,bytes(c))
        n.uc.emu_start(0x4fabdd,0x4fac10,count=1000)
        rows.append({'function':'rgb555_fragment','inputs':c,'expected':n.uc.reg_read(UC_ARM_REG_R0)})
    n.uc.mem_write(0x20000000,bytes([0xa5])*0xb0)
    n.run(0x583660,0x20000000)
    rows.append({'function':'face_constructor','expected':[n.word(0x20000000+4*i) for i in range(44)]})
    for parameter in [1,3]:
        n.run(0x583660,0x20000000)
        n.put(0x20000000+4*(19+parameter),0x7fc00000)
        words=[n.word(0x20000000+4*i) for i in range(44)]
        n.run(0x584568,0x20000000,0x20001000,0x20001004,0x20001008,0x2000100c)
        rows.append({'function':'eye_box','regression':True,'inputs':words,
            'expected':[n.word(0x20001000+4*i) for i in range(4)]})
    for case in range(256):
        words=[rng.choice(edges) if case<64 else rng.getrandbits(32) for _ in range(44)]
        words[38]=0 # no owned distorter; the bounding-box function ignores it
        n.uc.mem_write(0x20000000,struct.pack('<44I',*words))
        n.run(0x584568,0x20000000,0x20001000,0x20001004,0x20001008,0x2000100c)
        rows.append({'function':'eye_box','inputs':words,'expected':[n.word(0x20001000+4*i) for i in range(4)]})
    for parameter in range(19):
        for b in edges+[rng.getrandbits(32) for _ in range(16)]:
            n.uc.mem_write(0x20000000,bytes(0xb0)); n.run(0x583660,0x20000000)
            # Clip's fourth argument is the input float in r3 (soft-float ABI).
            try:
                v,_=n.run(0x5847a8,0x20000000,0,parameter,b)
                rows.append({'function':'eye_clip','parameter':parameter,'input':b,'expected':v})
            except RuntimeError as e: blocked.append({'function':'eye_clip','parameter':parameter,'input':b,'reason':str(e)})
    for name,address in [('mu_law',0x597ad8),('lift_angle_to_height',0x516f9c),('lift_height_to_angle',0x5170b0)]:
        for b in edges:
            try:
                v,_=n.run(address,b)
                rows.append({'function':name,'input':b,'expected':v})
            except RuntimeError as e: blocked.append({'function':name,'address':f'{address:08X}','input':b,'reason':str(e)})
    for b in edges+[0x3f000000,0x3f800000]:
        for a in [0x20000000,0x20002000,0x20003000]: n.run(0x583660,a)
        try:
            n.run(0x584290,0x20000000,0x20002000,0x20003000,b,0)
            rows.append({'function':'face_interpolate_default','input':b,'expected':[n.word(0x20000000+4*i) for i in range(44)]})
        except RuntimeError as e: blocked.append({'function':'face_interpolate_default','input':b,'reason':str(e)})
    for b in edges:
        n.uc.mem_write(0x20004000,bytes(0x100)); n.uc.mem_write(0x20005000,bytes(0x100))
        try:
            n.run(0x58cfc4,0x20004000,b,0,0x40a00000,0x40a00000,0x3f800000,0x3f800000,0,33,0x20005000)
            rows.append({'function':'eye_shift','input':b,'expected_bytes':bytes(n.uc.mem_read(0x20005000,0xd0)).hex()})
        except RuntimeError as e: blocked.append({'function':'eye_shift','input':b,'reason':str(e)})
    n.run(0x583660,0x20000000);n.put(0x20001000,0)
    try:
        v,_=n.run(0x585f18,0x20000000,0x20001000)
        rows.append({'function':'blink','expected_return':v,'expected_time':n.word(0x20001000),
            'expected':[n.word(0x20000000+4*i) for i in range(44)]})
    except RuntimeError as e: blocked.append({'function':'blink','reason':str(e)})
    # An explicit 1x1 Mat header, output and input objects are distinct.
    n.uc.mem_write(0x20000000,bytes(0x60)); n.uc.mem_write(0x20002000,bytes(0x60))
    n.put(0x2000200c,1);n.put(0x20002010,1)
    try:
        n.run(0x8728d8,0x20000000,0x20002000)
        rows.append({'function':'to_gray','expected_bytes':bytes(n.uc.mem_read(0x20000000,0x60)).hex()})
    except RuntimeError as e: blocked.append({'function':'to_gray','reason':str(e)})
    for b in inputs:
        n.put(0x20000000,b); n.put(0x20000004,1)
        try:
            n.run(0x84c87c,0x20000000)
            rows.append({'function':'rescale','input':b,'expected':n.word(0x20000000)})
        except RuntimeError as e: blocked.append({'function':'rescale','input':b,'reason':str(e)})
    for _ in range(1024):
        values=[rng.choice(edges) if rng.randrange(4)==0 else rng.getrandbits(32) for i in range(3)]
        for i,b in enumerate(values): n.put(0x20000000+8*i,b); n.put(0x20000004+8*i,1)
        try:
            v,_=n.run(0x84cc0a,0x20000000,0x20000008,0x20000010)
            rows.append({'function':'near','inputs':values,'expected':v})
        except RuntimeError as e: blocked.append({'function':'near','inputs':values,'reason':str(e)})
    # Both initialization and draws execute shipped code. Empty short string suppresses optional seed logging.
    for seed in [1,2,5489,0xffffffff,0x80000000]:
        n.uc.mem_write(0x20000000,bytes(0xa00)); n.uc.mem_write(0x20001000,bytes(12))
        n.run(0x82f860,0x20000000,0x20001000,seed)
        draws=[]
        for _ in range(1300): draws.append(n.run(0x82fac8,0x20000000)[0])
        rows.append({'function':'mt19937','seed':seed,'expected':draws})
        n.run(0x82f860,0x20000000,0x20001000,seed)
        n.uc.mem_write(0x200009c8,struct.pack('<dd',0.,1.))
        draws=[]
        for _ in range(650):
            lo,hi=n.run(0x82f9b0,0x20000000); draws.append(f'{hi:08X}{lo:08X}')
        rows.append({'function':'double','seed':seed,'expected':draws})
    cv=Native('libopencv_imgproc.so')
    for _ in range(512):
        w,h=rng.choice([0,1,64,128,1024]),rng.choice([0,1,32,64,1024])
        points=[rng.randrange(-2048,2049) for i in range(4)]
        cv.uc.mem_write(0x20000000,struct.pack('<4i',*points))
        cv.uc.mem_write(0x20001000,struct.pack('<2i',w,h))
        try:
            # This Size_ constructor makes the by-value Size indirect in the shipped C++ ABI.
            v,_=cv.run(0x410dc,0x20001000,0x20000000,0x20000008)
            rows.append({'function':'clip_line','inputs':[w,h]+points,'expected_return':v,
                'expected':list(struct.unpack('<4i',cv.uc.mem_read(0x20000000,16)))})
        except RuntimeError as e: blocked.append({'function':'clip_line','inputs':[w,h]+points,'reason':str(e)})
    dest=ROOT/'cozmo-stack/tests/Cozmo.Protocol.Tests/Fixtures/m1_m5_numeric_oracle.json'
    dest.write_text(json.dumps({'engine_sha256':hashlib.sha256(n.path.read_bytes()).hexdigest(),
        'generator':'re-analysis/tools/emu/emu_m1_m5_numeric.py','random_seed':'20261007',
        'opencv_sha256':hashlib.sha256(cv.path.read_bytes()).hexdigest(),
        'division_library_sha256':hashlib.sha256(cv.div_path.read_bytes()).hexdigest(),
        'fpscr':0,'rows':rows,'blocked':blocked},indent=2)+'\n')
    print(json.dumps({'fixture':str(dest),'rows':len(rows),'blocked':len(blocked)}))

if __name__ == '__main__': generate()
