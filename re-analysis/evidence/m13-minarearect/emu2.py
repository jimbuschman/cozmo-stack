import struct, math, sys
from emu import *
def full_minAreaRect(pts):
    uc=mk(); n=len(pts)
    # GOT canary
    canary=0x60040000; uc.mem_write(canary,struct.pack("<I",0x12345678))
    for r in so.dynamic_relocations:
        if r.has_symbol and r.symbol.name=="__stack_chk_guard":
            uc.mem_write(r.address,struct.pack("<I",canary))
    data=0x60010000
    uc.mem_write(data,b"".join(struct.pack("<ff",float(x),float(y)) for x,y in pts))
    inarr=0x60011000; uc.mem_write(inarr,struct.pack("<II",0x01030000|0x80000000|13,data))  # dummy
    outbuf=0x60012000; steps=0x60013000
    st={"n":{}}
    outres=0x60030000
    SP0=0x7f100000; sp=SP0-0x2000
    uc.mem_write(sp,b"\0"*0)  # noop
    uc.reg_write(UC_ARM_REG_SP,sp)
    uc.reg_write(UC_ARM_REG_R0,outres); uc.reg_write(UC_ARM_REG_R1,inarr)
    uc.reg_write(UC_ARM_REG_LR,SENT|1)
    heap=[0x60100000]
    def hdr(u,dst,nrows,dptr):
        u.mem_write(dst,struct.pack("<IIII",0x42ff0000|0x4000|13,2,nrows,1)+struct.pack("<I",dptr))
        u.mem_write(dst+0x28,struct.pack("<II",0,steps)); u.mem_write(steps,struct.pack("<II",8,8))
        st["n"][dst]=nrows
    def ret(u): u.reg_write(UC_ARM_REG_PC,u.reg_read(UC_ARM_REG_LR))
    def g(r): return uc.reg_read(r)
    def hook(u,a,size,ud):
        if a==0x17218:
            d=struct.unpack("<d",struct.pack("<II",g(UC_ARM_REG_R0),g(UC_ARM_REG_R1)))[0]; v=math.sqrt(d)
            r0,r1=struct.unpack("<II",struct.pack("<d",v)); u.reg_write(UC_ARM_REG_R0,r0);u.reg_write(UC_ARM_REG_R1,r1); ret(u)
        elif a==0x17704:
            y=struct.unpack("<d",struct.pack("<II",g(UC_ARM_REG_R0),g(UC_ARM_REG_R1)))[0]
            x=struct.unpack("<d",struct.pack("<II",g(UC_ARM_REG_R2),g(UC_ARM_REG_R3)))[0]
            v=math.atan2(y,x); r0,r1=struct.unpack("<II",struct.pack("<d",v)); u.reg_write(UC_ARM_REG_R0,r0);u.reg_write(UC_ARM_REG_R1,r1); ret(u)
        elif a==0x183ae:  # _InputArray::getMat(this... ) r0=dst Mat r1=array
            dst=g(UC_ARM_REG_R0); arr=g(UC_ARM_REG_R1)
            if arr==inarr: hdr(u,dst,n,data)
            else:
                obj=struct.unpack("<I",bytes(u.mem_read(arr+4,4)))[0]  # the Mat r6
                rows=struct.unpack("<I",bytes(u.mem_read(obj+8,4)))[0]; dp=struct.unpack("<I",bytes(u.mem_read(obj+0x10,4)))[0]
                hdr(u,dst,rows,dp)
            ret(u)
        elif a==0x1723c:
            m=g(UC_ARM_REG_R0); u.reg_write(UC_ARM_REG_R0,st["n"][m]); ret(u)
        elif a==0x17650: u.reg_write(UC_ARM_REG_R0,0); ret(u)   # fixedType
        elif a==0x174d0:  # _OutputArray::create(rows=r1, cols=r2, type=r3)
            arr=g(UC_ARM_REG_R0); obj=struct.unpack("<I",bytes(u.mem_read(arr+4,4)))[0]
            hdr(u,obj,g(UC_ARM_REG_R1),outbuf); ret(u)
        elif a in (0x184cc,): ret(u)
        elif a==0x171e8: u.reg_write(UC_ARM_REG_R0,heap[0]); heap[0]+=0x10000; ret(u)
        elif a==SENT: u.emu_stop()
        elif a in (0x17170,):raise RuntimeError("cv::error")
    uc.hook_add(UC_HOOK_CODE,hook)
    try: uc.emu_start(0x9c760|1,SENT,count=5000000)
    except UcError as e: print("ERR",e,hex(uc.reg_read(UC_ARM_REG_PC))); raise
    return struct.unpack("<5f",bytes(uc.mem_read(outres,20)))
if __name__=="__main__":
    print(full_minAreaRect([(0,0),(2,0),(2,1),(0,1)]))
