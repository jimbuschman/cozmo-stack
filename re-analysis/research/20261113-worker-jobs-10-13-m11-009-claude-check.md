# Worker jobs 10-13 merged; M11-009 evidence corrected (manager, 2026-11-13)

## Jobs merged (worker tip 60c4c484)

| Job | Verdict | Notes |
| --- | --- | --- |
| DRIVE-TO-OBJECT-MANUAL-SPEED-1 | PASS | No effect: every supported source passes useManualSpeed 0; recorded on M12-022 |
| SETTLEMENT-READINESS-1 | PASS | `jobs/trial-m13/SETTLEMENT-READINESS.md`; used below for M11-009 |
| FIND-ON-TOP-SAME-ROOT-1 | PASS | Re-read: 0x00626048 calls the GetWithRespectToRoot helper 0x004DF628; 0x006260AC..0x006260B8 adds obj's ID to the ignore set; literal 0x3727C5AC at 0x0062BB5C is 1e-5f. Worker gate 8419/8419; verifier PASS after two blocking rounds |
| LOCALIZE-MULTI-PAIR-CROSS-ORIGIN-TEST-1 | PASS | Production test for X19; no defect. Gate 8420/8420 |

No record settled by these jobs.

## M11-009: evidence corrected, status EXACT_SOURCE unchanged

M11-009 was settled earlier with evidence that cited only the message read (0x00533E54). Re-read now:

- **Moved guard 0x00534116..0x0053413C:** IsCarryingObject through PLT 0x004A6FAC (0x0053411E); `ldrb [obj+0x24]`,
  `cmp #1`, `movne r1,#1`; `orrs r0,r1`; `bne` skip; else MarkObjectDirty(obj, r2=0) through PLT 0x004A7174
  (0x0053413C).
- **Stopped guard 0x00534924..0x0053494A:** the same shape (0x0053492C, 0x0053494A).
- **MarkObjectDirty 0x005075A4:** SetPoseStateHelper(obj, 2 = Dirty) at 0x005075B2; with propagate false it ends
  (0x005075B6).
- **IsCarryingObject 0x00633F88:** carried [+8] or on top [+0x14] (re-read 2026-11-12).

**The stack:** `BlockWorld.ApplyMovedOrStoppedCore` dirties only when `!IsCarryingObject(id) && PoseState == Known`,
with the shared binding now the two-ID body (CARRIED-ONTOP-BINDING-CENSUS-1). Matches.

**Corrected** (standing authorisation for cited corrections): the title adds "when its pose is Known" and "or the cube
resting on it"; the evidence cites both guards, the IsCarryingObject body and MarkObjectDirty. The inventory row
(which still read IMPLEMENTATION_GAP) is brought in line. M11-vision re-approved.

**Ownership ruling:** SetPoseStateHelper's own body (0x0050612C) is M11-007's, and the PoseStateChanged delivery is
M11-038's. Both have their own records, so M11-009 does not own them.

## Operator decision, RETIRE-LEGACY-DRIVE-API-1 (2026-11-13)

The worker's retirement census found the legacy drive types also carry about 12 shipped behaviours in the standalone
FreeplayStack and the Conformance hardware tools (ManipTool N/P/Q/R/S/T/U/X, M5Hw DRIVING). The operator chose
**option 2, retire and rebind**: the standalone stack passes `NativeRobot` and binds the native classes; the tools get an
entry to the native runners; then the legacy types, the holds and the manager's guard are removed.

**Manager answers to the census's Q3–Q6:**
- **Q3, type 6:** retire it with the legacy flow.
- **Q4, M12-004 (UseSecondClosestPreActionPose 0x005BEE56):** an engine behaviour, so it is not dropped. If no native
  runner sets ExcludePoses, record it in M12-004 as an IMPLEMENTATION_GAP with the native caller that would set it, and
  propose a job; do not keep the legacy form alive for it.
- **Q5, P13 IgnoreObstacleIds pass-through:** drop it; the engine has no such input (stack-only, M12-032).
- **Q6, GetPossiblePoses' non-native branch:** remove it if nothing outside the retired types reaches it after the
  rebind; otherwise record what does.
