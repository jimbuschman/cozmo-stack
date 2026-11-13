# USE-SECOND-CLOSEST-NATIVE-1 merged; M12-004 recorded as no effect (manager, 2026-11-13)

Worker job commit bdb823bf (gate 8381/8381). Handoff: `re-analysis/jobs/trial-m13/HANDOFF-USE-SECOND-CLOSEST-NATIVE-1.md`.

## The claim: IBehavior::UseSecondClosestPreActionPose (0x005BEE56) has no caller

**Re-checked independently** (manager script over `resources/lib/armeabi-v7a/libcozmoEngine.so`):
- Thumb BL/BLX decoded at every halfword of every executable segment: no branch to 0x005BEE56. The same scan finds
  the known calls to RemoveMatchingPredockPose 0x004B20A0 (0x005B61BC, 0x005BEE90) and GetPossiblePoses 0x004ABFBC
  (0x005B61AE, 0x005BEE66), so the decoder works.
- The value 0x005BEE56 / 0x005BEE57 as a word anywhere in the file: only at 0x1D5C8, its own .dynsym entry.
- Every relocation section: no entry names its symbol (dynsym index 7499). So no PLT stub, GOT slot or vtable holds it.

**The body**, re-read: `ldr r0,[r6,#0x2C]` (0x005BEE88), `bl 0x004EA398` (0x005BEE8A, Robot::GetPose), then
RemoveMatchingPredockPose (0x005BEE90). this+0x2C is the Robot& (IBehavior ctor 0x005BBBFA), so it removes the poses
matching the robot's current pose. M12-004's old title ("drops the pose it just failed from") misread it.

## Disposition

**M12-004: COMPATIBILITY_POLICY, live_path false: no effect, unreachable engine code.** A function nothing calls
touches none of "What we reproduce" 1–4. The stack builds nothing. This follows the pattern for unreachable engine
paths (M3-046). Title, authority and effect are corrected (a cited correction under the standing authorisation); the
inventory rows (the record row, the M13 dock-retry row and the §2 "keep" note) are brought in line; M12-manipulation
re-approved.

The pickup retry (RespondToPickupResult → StartPickupAction(robot,false), 0x005B826C..0x005B8272) excludes no pose;
the only engine pose removal on that path is M12-029's DriveToHelper functor (0x005B61BC). The retired DockHelper's
exclusion was never engine behaviour.

**The worker's three reference fixes** (DockActions.cs:253 tag, Sensors.cs:1081 diagnostic, HardwareCatalog N's
record list) remove M12-004 from code it never owned. Accepted.

No record settled (COMPATIBILITY_POLICY is a disposition, not a settlement of built code).
