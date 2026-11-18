"""Run the shipped IMU filter and off-treads decision prefix, SPURIOUS-PICKUP-1.

Requires unicorn (tested2.1.4), lief and capstone. No robot connection. Imports the
existing local .so loader; no bank/assets upload. Each instance starts with the
native constructor's zero filter/candidate/current fields (5100E0..5100FE).

The actual Thumb instructions5129A4..512A70 copy/filter IMU data;511E00..511FEA
and512228..51225C classify and decide commit eligibility. Only the two timer PLTs
are substituted: the caller supplies BaseStationTimer tick-start milliseconds.
Native Radians constructors/operators/rescale execute through actual ELF PLT
relocations. Execution stops at511FF0 (commit eligible),51219A (no commit), or
512206 (uncalibrated gate). Commit consequences/broadcast/world/visualization
are not emulated. We copy the chosen byte to355 between calls, matching the
native store51208E; production-ingress tests separately cover consequences.
"""
import struct
from unicorn import UC_HOOK_CODE
from unicorn.arm_const import UC_ARM_REG_R4, UC_ARM_REG_R5
from emu_common import Emu, BASE
from so import ELF


class OffTreadsOracle:
    def __init__(self, physical=True, calibrated=True):
        self.e = e = Emu()
        for relocation in ELF.pltgot_relocations:
            if relocation.has_symbol and relocation.symbol.value:
                e.w32(relocation.address, relocation.symbol.value)
        self.robot, self.message = BASE, BASE + 0x1000
        self.now, self.result = 0, None
        e.w8(self.robot + 0x14, int(physical))
        e.w8(self.robot + 0x314, int(calibrated))
        e.hook(0x4A4F6C, lambda _: BASE + 0x2000)
        e.hook(0x4A7B58, lambda _: self.now)

        def decision(u, address, size, context):
            if address in (0x511FF0, 0x51219A, 0x512206):
                self.result = (address == 0x511FF0, e.r8(self.robot + 0x356),
                               e.r32(self.robot + 0x358))
                u.emu_stop()
        e.uc.hook_add(UC_HOOK_CODE, decision)
        e.uc.hook_add(UC_HOOK_CODE, lambda u, a, size, ctx: u.emu_stop(), begin=0x512A72, end=0x512A72)

    def step(self, packet, now_ms, calibrated=None):
        if len(packet) != 92 or packet[0] != 0xF0:
            raise ValueError('Expected the92-byte generated RobotState wire message')
        e, robot = self.e, self.robot
        self.now, self.result = now_ms & 0xFFFFFFFF, None
        if calibrated is not None:
            e.w8(robot + 0x314, int(calibrated))
        e.uc.mem_write(self.message, packet[1:])
        # UpdateFullRobotState512974..512982 constructs/assigns Radians from
        # RobotState.Pose.Pitch (packed body+1C). Run the real float constructor
        #84C832, including rescale/normalization, directly into the owned field.
        e.call(0x84C833, robot + 0x304, struct.unpack_from('<I', packet, 29)[0])
        e.uc.reg_write(UC_ARM_REG_R4, robot)
        e.uc.reg_write(UC_ARM_REG_R5, self.message)
        e.call(0x5129A5)
        old = e.r8(robot + 0x355)
        e.call(0x511E01, robot, self.message)
        if self.result is None:
            raise RuntimeError('Native classifier left the bounded decision path')
        changed, candidate, candidate_time = self.result
        if changed:
            e.w8(robot + 0x355, candidate)
        return dict(old=old, state=e.r8(robot + 0x355), candidate=candidate,
                    candidateTime=candidate_time, changed=changed,
                    filteredAY=e.rf(robot + 0x384))
