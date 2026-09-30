import struct, math, random, sys
import lief
from unicorn import *
from unicorn.arm_const import *
IMG="C:/Users/jbuschman/Downloads/cozmo-stack-r2/resources/lib/armeabi-v7a/libopencv_imgproc.so"
raw=open(IMG,"rb").read(); so=lief.ELF.parse(IMG)
def mk():
    uc=Uc(UC_ARCH_ARM,UC_MODE_THUMB)
    for s in so.segments:
        if s.type==lief.ELF.Segment.TYPE.LOAD:
            base=s.virtual_address&~0xfff; end=(s.virtual_address+s.virtual_size+0xfff)&~0xfff
            try: uc.mem_map(base,end-base)
            except UcError: 
                # overlap; map only missing pages
                for p in range(base,end,0x1000):
                    try: uc.mem_map(p,0x1000)
                    except UcError: pass
            uc.mem_write(s.virtual_address, raw[s.file_offset:s.file_offset+s.physical_size])
    uc.mem_map(0x7f000000,0x200000); uc.mem_map(0x60000000,0x100000)
    c=uc.reg_read(UC_ARM_REG_C1_C0_2); uc.reg_write(UC_ARM_REG_C1_C0_2,c|(0xf<<20)); uc.reg_write(UC_ARM_REG_FPEXC,0x40000000)
    return uc
SENT=0x60080000
def run_calipers(pts):
    """emulate libopencv_imgproc minAreaRect from 0x9c8c0 (post-convexHull) on hull points (list of (x,y) float32)."""
    uc=mk(); n=len(pts)
    data=0x60010000
    uc.mem_write(data,b"".join(struct.pack("<ff",*p) for p in pts))
    hdr=0x60020000  # Mat header
    uc.mem_write(hdr+0x10,struct.pack("<I",data))
    out=0x60030000
    SP0=0x7f100000; sp=SP0-68-0x4ec
    uc.mem_write(SP0-4,struct.pack("<I",SENT|1))
    canary=0x60040000; uc.mem_write(canary,struct.pack("<I",0x12345678))
    uc.mem_write(sp+0x24,struct.pack("<I",canary)); uc.mem_write(sp+0x4e4,struct.pack("<I",0x12345678))
    uc.reg_write(UC_ARM_REG_SP,sp); uc.reg_write(UC_ARM_REG_R4,out); uc.reg_write(UC_ARM_REG_R6,hdr)
    def ret(u): u.reg_write(UC_ARM_REG_PC,u.reg_read(UC_ARM_REG_LR))
    heap=[0x60050000]
    def hook(u,addr,size,ud):
        a=addr
        if a==0x17218 or a==0x1721c:  # PLT sqrt (stub start)
            pass
        if a==0x17218:
            r0,r1=u.reg_read(UC_ARM_REG_R0),u.reg_read(UC_ARM_REG_R1)
            d=struct.unpack("<d",struct.pack("<II",r0,r1))[0]; v=math.sqrt(d)
            r0,r1=struct.unpack("<II",struct.pack("<d",v)); u.reg_write(UC_ARM_REG_R0,r0);u.reg_write(UC_ARM_REG_R1,r1); ret(u)
        elif a==0x17704:
            g=lambda r:u.reg_read(r)
            y=struct.unpack("<d",struct.pack("<II",g(UC_ARM_REG_R0),g(UC_ARM_REG_R1)))[0]
            x=struct.unpack("<d",struct.pack("<II",g(UC_ARM_REG_R2),g(UC_ARM_REG_R3)))[0]
            v=math.atan2(y,x); r0,r1=struct.unpack("<II",struct.pack("<d",v)); u.reg_write(UC_ARM_REG_R0,r0);u.reg_write(UC_ARM_REG_R1,r1); ret(u)
        elif a==0x1723c: u.reg_write(UC_ARM_REG_R0,n); ret(u)
        elif a in (0x6f404,0x184cc): ret(u)
        elif a==0x171e8: u.reg_write(UC_ARM_REG_R0,heap[0]); heap[0]+=0x10000; ret(u)
        elif a==SENT: u.emu_stop()
        elif a in (0x17170,0x170f8): raise RuntimeError("cv::error/String::allocate hit at %x"%a)
    uc.hook_add(UC_HOOK_CODE,hook)
    # PLT stubs: hooks fire on stub first instruction; find stub addrs
    try: uc.emu_start(0x9c8c0|1,SENT,count=2000000)
    except UcError as e: print("ERR",e,hex(uc.reg_read(UC_ARM_REG_PC))); raise
    o=struct.unpack("<5f",bytes(uc.mem_read(out,20)))
    return o  # cx,cy,w,h,angle
if __name__=="__main__":
    pts=[(0,0),(2,0),(2,1),(0,1)]
    print(run_calipers([(float(x),float(y)) for x,y in pts]))
