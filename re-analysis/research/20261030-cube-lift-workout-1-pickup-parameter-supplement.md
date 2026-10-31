# CUBE-LIFT-WORKOUT-1 pickup parameter byte mapping

Read-only extractor supplement, 2026-10-30. Answers the resumed builder's narrow question: which helper fields receive workout pickup parameter bytes +0C and +0D? The original source packet and independent check remain unchanged. No implementation, record, inventory, approval, git, .NET or hardware changes.

Evidence: ARM/Thumb instructions in `resources/lib/armeabi-v7a/libcozmoEngine.so`. `known.py PickupBlockParameters` found no symbol (the exported spelling is `PickupBlockParamaters`); `known.py PickupBlockHelper` identified factory 0x005B553C and constructor 0x005B7A4C. Existing local `ranges.py` disassembled exact ranges 0x005B553C..0x005B557A, 0x005B7A4C..0x005B7AE0 and 0x005D80A0..0x005D80E8 with PLT resolution. No decompiler claim is used.

| Step | Native instruction evidence | Result |
|---|---|---|
| Workout parameter bytes | 0x005D80BC `movs r6,#0`; 0x005D80C2 `strh.w r6,[sp,#0x2C]`; 0x005D80C6 `movs r1,#1`; 0x005D80CC `strb.w r1,[sp,#0x2D]`. Parameter object starts at sp+0x20. | Parameter +0C=0, +0D=1. |
| Actual factory arguments | 0x005D80DE `strd r7,r8,[sp]`; 0x005D80E2 calls CreatePickupBlockHelper. Factory 0x005B5554 `ldrd r0,r1,[sp,#0x28]`; 0x005B5558 `strd r0,r1,[sp]`; 0x005B5564 calls the constructor without changing either stack argument. | Target and parameter pointers pass unchanged. |
| Actual constructor mapping | 0x005B7A7E `ldrd r6,r5,[sp,#0x30]` receives target and parameter pointers. 0x005B7ACA `ldrh r1,[r5,#0xC]`; 0x005B7AD0 `strh.w r1,[r4,#0x104]`. | The two adjacent bytes copy directly: parameter +0C to helper +104 and parameter +0D to helper +105. Thus this workout constructs helper +104=0, +105=1. |

This settles the raw constructor mapping only. It adds no inferred semantic names for either byte and no new ownership or whole-record closure claim.
