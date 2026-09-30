import struct,random
from emu import *
from port import *
def emu_skl(arr,start,end,nsign,sign2):
    uc=mk(); n=len(arr)
    data=0x60010000; ptrs=0x60020000; stack=0x60030000
    for i,(x,y) in enumerate(arr):
        uc.mem_write(data+8*i,struct.pack("<ff",float(x),float(y))); uc.mem_write(ptrs+4*i,struct.pack("<I",data+8*i))
    SP0=0x7f100000
    uc.mem_write(SP0,struct.pack("<ii",nsign,sign2)); uc.mem_write(SP0-4,b"\0"*0)
    uc.reg_write(UC_ARM_REG_SP,SP0); uc.reg_write(UC_ARM_REG_R0,ptrs); uc.reg_write(UC_ARM_REG_R1,start&0xffffffff)
    uc.reg_write(UC_ARM_REG_R2,end&0xffffffff); uc.reg_write(UC_ARM_REG_R3,stack); uc.reg_write(UC_ARM_REG_LR,SENT|1)
    def hook(u,a,s,ud):
        if a==SENT: u.emu_stop()
    uc.hook_add(UC_HOOK_CODE,hook)
    uc.emu_start(0x3781a|1,SENT,count=100000)
    c=uc.reg_read(UC_ARM_REG_R0)
    return [struct.unpack("<i",bytes(uc.mem_read(stack+4*i,4)))[0] for i in range(c)]
pts=sorted([(-22.,-22.)]*2+[(-22.,22.)]*2+[(22.,-22.)]*2+[(22.,22.)]*2)
pts=[(f32(a),f32(b)) for a,b in pts]
for args in [(0,2,-1,1),(7,2,-1,-1),(0,0,1,-1),(7,0,1,1)]:
    print(args, emu_skl(pts,*args), sklansky(pts,*args))
