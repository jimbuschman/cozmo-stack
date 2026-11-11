# Claude worker: LOCALIZE-CROSS-ORIGIN-1 and LOCALIZE-TO-MAT-HISTORY-1: Claude check (manager, 2026-11-11)

Merged from `claude-worker` at 4ee0950d. The worker ran its own extractor census and built the change. Its
independent verifier re-disassembled every step; it found the order, the arguments and the failure exits all match
the binary, with no lock inversion. The worker's affected classes passed 901/901.

## LOCALIZE-CROSS-ORIGIN-1: PASS, with a manager guard added

**What is built:** the cross-origin branch of Robot::LocalizeToObject, in the engine order.

| Step | Address |
| --- | --- |
| The split, after S10 | 0x0051583C..0x00515850 |
| RejiggeringOrigins | 0x00515854..0x005158D0 |
| Rejigger | 0x00847D7C..0x00847E84 |
| RejiggerFailed | 0x0051591A |
| UpdateObjectOrigins | 0x00620534..0x006206F4, with the OriginFail path, the UnknownOriginID guard, the non-unique counterpart search and SetObservationTimes |
| Map origins | 0x0051599E / 0x0067E058 |
| Face origins | 0x004F5644 |
| Flatten | 0x00847F34 |

A seen cube from an earlier origin now re-joins the origins and relocalizes the robot into the cube's origin.

**The worker's decision on OnRobotRelocalized (0x0051246C), checked:**
- AIComponent::OnRobotRelocalized loads the whiteboard and tail-calls AIWhiteboard::OnRobotRelocalized (0x0056AA28).
- That calls UpdatePossibleObjectRender and tail-calls another render routine.
- So the callback is rendering only, with no effect on 1–4; not building it is correct.

**The verifier's blocking finding F1, which the worker only made visible:**
- **The engine:** follows the origin change with PathComponent's HandlePossibleOriginChanges (0x0064980C) →
  RejiggerTargetsAndReplan (0x006499D0), unbuilt in the stack (M12-032).
- **The risk:** a path in flight would keep its old-origin segments while the next 0x45 re-bases odometry. That is
  robot traffic from a state the engine would not leave.
- **The manager guard:** while a path is in flight, a cross-origin pair is stopped before S2. No history, pose,
  origin or 0x45 write is made, and there is a visible MISSING naming PATH-ORIGIN-CHANGE-REPLAN-1. The split depends
  only on E and the current origin, which S2..S10 do not change.
- **Same-origin pairs are unaffected:** the robot may still localize to the cube's new copy in the current origin.
- **The production test, path-in-flight case:** now checks no Rejigger, no 0x45 in the cube's old origin, and the
  MISSING.

## LOCALIZE-TO-MAT-HISTORY-1: PASS (uncalled code)

Robot::LocalizeToMat (0x00515CBC..0x0051646C) has no caller in the shipped `.so`. The worker found no branch, PLT
slot, relocation or address word, and the same scripts do find LocalizeToObject's two callers. So it is recorded on
M11-044 as uncalled code with no effect on 1–4, and not built.

**Queued:** annotate the entries that cite addresses inside LocalizeToMat (M11-073's 0x005160BC/0x0051612C,
M4-control C11.2, and two research notes).

**No record settled.**
- M11-044 keeps the null-O body (no caller passes it), the path replan join (PATH-ORIGIN-CHANGE-REPLAN-1) and the
  LocalizeRobot NaN order.
- M11-043 keeps its tolerance, VERIFY text and log formats.
