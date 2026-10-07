"""Checked E3 recipient arithmetic: actual shipped instruction fragments.

Pause 0x00695EAA..0x00695EC2, unpause 0x00695F02..0x00695F6C.
Stops before external notification/write calls; does not claim their behavior.
"""
from emu_m1_m5_numeric import Native,ROOT
from unicorn import UC_HOOK_CODE
from unicorn.arm_const import *
import struct,json,hashlib

def generate():
    n=Native();node=0x20000000;rows=[]
    for stop in [0x695ec2,0x695f6c]:
        n.uc.hook_add(UC_HOOK_CODE,lambda u,a,s,d:u.emu_stop(),begin=stop,end=stop)
    def run(a):
        n.uc.reg_write(UC_ARM_REG_R5,node);n.uc.reg_write(UC_ARM_REG_SP,0x210f0000);n.uc.reg_write(UC_ARM_REG_FPSCR,0)
        n.uc.emu_start(a|1,0x22000000,count=10000)
    for case,(pause,now,deadline) in enumerate([(0x41200000,0x42200000,0),(0x41200000,0x42200000,0x80000000),
            (0x41200000,0x42200000,0x42c80000),(0x41200000,0x42200000,0x7fc00000),
            (0x3f800001,0x42200001,0x42c80000),(0x41200000,0x42200000,0)]):
        n.uc.mem_write(node,bytes(0x400));n.put(node+0x3ac,pause);n.put(node+0x3b0,0x42700000)
        for i in range(3):n.put(node+0x1fc+4*i,0x40e00000);n.put(node+0x208+4*i,deadline)
        run(0x695eaa);assert n.uc.reg_read(UC_ARM_REG_PC)==0x695ec2
        pause_at=n.word(node+0x1d8);remaining=n.word(node+0x3b4)
        n.put(node+0x3ac,now);run(0x695f02);assert n.uc.reg_read(UC_ARM_REG_PC)==0x695f6c
        rows.append(dict(pause_tick=pause,unpause_tick=now,next_before=0x42700000,start_before=0x40e00000,
            deadline_before=deadline,deadline_present=case!=5,pause_at=pause_at,remaining=remaining,
            expected_next=n.word(node+0x3b0),expected_last_decay=[n.word(node+0x1e4+4*i) for i in range(3)],
            expected_pause_start=[n.word(node+0x1f0+4*i) for i in range(3)],
            expected_start=[n.word(node+0x1fc+4*i) for i in range(3)],
            expected_deadline=[n.word(node+0x208+4*i) for i in range(3)],
            expected_bracket=[n.word(node+0x214+4*i) for i in range(3)]))
    (ROOT/'cozmo-stack/tests/Cozmo.Protocol.Tests/Fixtures/needs_unpause_native.json').write_text(json.dumps(dict(
        generator='emu_needs_unpause.py',engine_sha256=hashlib.sha256(n.path.read_bytes()).hexdigest(),rows=rows),indent=2)+'\n')
    print('complete native fragment pairs',len(rows))
if __name__=='__main__':generate()
