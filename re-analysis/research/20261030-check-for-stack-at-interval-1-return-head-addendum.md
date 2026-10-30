# CHECK-FOR-STACK-AT-INTERVAL-1 — return-head semantic correction

2026-10-30, same read-only reader requested gpt-6.1-sol/medium/fork_none; runtime backend unobserved. Narrow manager request: verify the final PanAndTilt boolean stores and meanings before compilation. `known.py` run first for PanAndTiltAction constructor and Init. Same primary binary SHA256 `02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`. No C# implementation used as evidence; no code, metadata, .NET, git or hardware changes.

**The initial report's absolute-head-zero prose was wrong.** The already-correct literal tuple is `PanAndTiltAction(robot, savedRootYaw, Radians(0), true, false)`. The second boolean false selects **relative head offset zero**: the head target is the robot's current head angle when PanAndTilt Init executes, combined through the engine's Radians addition/rescaling. It does not command an absolute head angle of zero. A nonzero head angle after looking above a cube is not reset to zero by this call.

## Exact argument/store/use chain

| Step | Primary instructions | Established result |
|---|---|---|
| Caller pan/head arguments | 0x005D7890..7894 copies saved extracted yaw into stack14; 0x005D7898..789E constructs Radians(0) at stackC; 0x005D78A4 `add r2,sp,#0x14`; 78A6 `add r3,sp,#0xc` | r2 pan=yaw, r3 head=0 |
| Caller boolean words | 0x005D789C `movs r7,#0`; 78A2 `movs r0,#1`; 78A8 `strd r0,r7,[sp]`; 78AC/78AE this/robot; 78B0 calls PanAndTilt ctor PLT0x004A93AC | First bool1, second bool0, ordinary integer argument ABI; no semantic inference from names |
| Constructor stack adjustment | 0x0054962C pushes eight registers (32 bytes); 9630 subtracts0x60; 969A `ldrd r1,r0,[sp,#0x80]` | Fetches original caller stack0/4 in r1/r0, without reversal |
| Constructor bool fields | 0x005496A4 `strb.w r0,[r4,#0x125]`; 96AA `strb.w r1,[r4,#0x124]`; pan stored at+114 via9682..968A, head at+11C via968E..9696 | +124=first(booltrue); +125=second(boolfalse) |
| Pan use | 0x00549C6A loads pan+114 into r2; 9C6E loads bool+124 into r3; 9C72 calls TurnInPlaceAction(robot,float,bool) | Absolute-pan booleantrue reaches existing checked TurnInPlace contract; no tuple mutation |
| Head true branch | 0x00549CD0 `ldrb.w r0,[r5,#0x125]`; 9CD4 `cbz r0,#0x549ce2`; nonzero9CD6..9CDC copies requested+11C Radians | True uses requested head directly |
| Head false branch | 0x00549CE2 loads robot pointer; 9CE4 points r2 at requested+11C; 9CE8 `ldr.w r1,[r0,#0x2fc]`; 9CEC destination temporary; 9CEE calls `Anki::operator+(float, Radians const&)` | False computes current robot head angle+requested offset; here offset0 |
| Actual head child | 0x00549D08 points r2 at computed temporary stack14; 9D10 calls MoveHeadToAngleAction ctor with it | Computed relative target is the commanded head target, not a dead field |

This corrects the report introduction, R1 semantic prose and expected check(g). The exact call tuple, return callback deadline semantics, target traversal and other own behavior claims remain unchanged. No broad child re-extraction or new record is proposed. M13-020's existing checked PanAndTilt interpretation is consistent with these native stores/branches; this was a reader wording error, not an engine-contract contradiction.

## Original preservation

Before any report edit, exact original bytes were saved to `.scratch/check-stack-read/read-original-before-return-head-correction.md` (ignored task scratch), SHA256 `814685b02e33de506464556d235a3d2cbd6c2063906e5adba0f0c11fa743886b`. Original instruction dump retained separately in `.scratch/check-stack-read/return-bools.txt`.

Revised original report `re-analysis/research/20261030-check-for-stack-at-interval-1-read.md` SHA256 after the correction: `d0c6deb76aa33c757dba044c00f82f3b598e77c62c470f4f1f5bfdc46bd1872d`. Its literal current manifest snapshots were preserved. This addendum is a separately retained correction, not an overwrite of the archived original.

Suggested narrow independent reference: set current robot head to a nonzero angle before PanAndTilt Init, assert head target follows that angle through native Radians addition rather than absolute0; body pan target remains the saved yaw. No test executed here.
