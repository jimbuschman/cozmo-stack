# HANDOFF USE-SECOND-CLOSEST-NATIVE-1 (Claude worker, 2026-11-13)

Job 17. Census: `.scratch/USE-SECOND-CLOSEST-NATIVE-1-census.md` (worker-local). A verifier re-scanned the binary with
its own scripts and confirmed both findings.

## Result: no effect, nothing built

**IBehavior::UseSecondClosestPreActionPose (0x005BEE56, M12-004) has no caller in libcozmoEngine.so.**
- Thumb BL/BLX/B.W/Bcc decoded at every halfword of .text, and ARM BL/BLX at every word: no branch to it. The same
  decoder finds the known calls to GetPossiblePoses (0x005B61AE, 0x005BEE66) and RemoveMatchingPredockPose
  (0x005B61BC, 0x005BEE90).
- Its address value occurs once in the file, in its own .dynsym entry (file offset 0x1D5C8). The object uses REL
  relocations (addends in place), so no vtable, GOT, init_array or RELATIVE slot holds it. There is no .rel.plt entry.
- No movw/movt, ADR or literal-plus-pc construction produces it. No other .so, `sources/` or `smali/` reference it.
- Not inlined: RemoveMatchingPredockPose's only callers are 0x005BEE90 (inside this function) and 0x005B61BC (the
  DriveToHelper functor, M12-029).
- **The pickup retry** RespondToPickupResult (0x005B8192..0x005B8198; 0x005B8262..0x005B826A) reaches only
  `StartPickupAction(robot, false)` (0x005B826C..0x005B8272, blx 0x004B22EC). The roll helper has no call either.

So the engine's dock retry excludes no pose beyond M12-029's DriveToHelper filter. The retired DockHelper's exclusion
was never engine behaviour, and adding one to NativePickupBlockHelper / NativeRollBlockHelper would be wrong.

**The body also differs from M12-004's description.** this+0x2C is the Robot& (IBehavior ctor 0x005BBBFA), and the
pose matched is Robot::GetPose (0x005BEE8A bl 0x004EA398, robot+0x298). So it removes the pre-action poses matching
the robot's **current** pose (IsSameAs 100/100/100 mm and 30 degrees, 0x3F060A92; every match erased, rescanning from
the same index), and clears the in-position flag. It does not remove "the pose it just failed from". The evidence
addresses are correct; only the reading is wrong.

## Changes

- M12-004 `unresolved`: the no-effect evidence above; `test`: none.
- Three references that named M12-004 for other records' code:
  - DockActions.cs:253: tag `M12-004, M12-017, M12-020` -> `M12-017, M12-020` (the 7.5-degree IDockAction tolerance);
  - Sensors.cs:1081: the Docking+4 mirror diagnostic now names M12-017, as Docking.cs:980 does;
  - HardwareCatalog check N's FidelityRecords no longer lists M12-004 (a pass would vouch for unreachable behaviour).
- Affected tests: HardwareCatalog/HardwareRunner/FidelityManifest/FidelityLiteralLint classes 109/109.

## For the manager (frozen fields; "no effect is a claim")

- M12-004's disposition: suggested "unreachable engine path", live_path false.
- Its title ("drops the pose it just failed from") and the inventory reading (M12-manipulation.md:24 "gets the
  IDockAction from this+0x2C", :180, :193 "are the retry path") are contradicted, as above.
- Which pose DriveToHelper+0x114 holds on a pickup retry is M12-029's question, not read here.

No record settled.
