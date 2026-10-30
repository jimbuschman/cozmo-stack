# DRIVE-IN-DESPERATION-ENERGY-1: Claude check (manager, 2026-10-30)

**Verdict: FIX REQUIRED. Not merged.** Handoff 422d19b1 (full gate 6992 PASS). **Nothing settled.** Every traced native
step matches the C#. Findings come from reading, not from a run.

## Blocking: an uncaught throw whenever a charger is located

`NativeDriveInDesperationBehavior.cs` `DriveToCube` calls `DockPreActionPoses.GetNative` with
`_m.NativeObstacles(main)` and no catch. `NativeObstacles` (ManipulationSystem.cs:252) throws NativeActionUnavailable on
any stand-in obstacle, and a non-cube such as the charger always has a null quad. The original has no such failure at
GetPreActionPoses 0x005D995C. The call runs inside HandleActionComplete, so the throw unwinds through the action
deletion: later RobotCompletedAction subscribers miss the broadcast, `_deletionTags.Remove` is skipped, and the rest of
that ActionList.Update is lost. A robot in severe low energy has normally just left the charger, so this is a mainstream
state. Every other owner catches it (NativePickupBlockHelper:41, NativeRollBlockHelper:49,
NativePlaceRelObjectHelper:27, NativeDriveToObjectRunner:41).

**Fix:** catch it in the owner, report a visible MISSING naming M12-036 and the address, and continue with the native
continuation without an invented pre-action result. Add a test with a located charger through the cube branch.

## Ruling on the missing sentinel search: not blocking, diagnostic required

With `useCubes`, the MISSING search starts no action, so the behaviour ends and StrictPriority picks it again. Ending is
the original's own continuation after a failed search (the failure callable is null, 0x005D970E..9744), and each cycle
has a real Idle of at least 1.5 s and a drive, so this is not a per-tick spin. It differs from the two rejected loops,
and a MISSING gap doesn't hold the job. But `ReportMissing` deduplicates per process, so later cycles are silent.
**Log it on every occurrence,** as FIX-CHECK-FOR-STACK does (`new HashSet<string>()`), and say in the handoff that the
search motions are skipped.

## Checked and agreeing (verifier, in disassembly)

Factory 0x0059CA9A; vtable 0x010288D4; ctor 0x005D8AE0..8C36; Init 0x005D8ECA..EEE; Update 0x005D9062..90A8; idle and the
strict f32 wait 0x005D8F2C..8FE4; cooldown and selection 0x005D9D5C..9E3A; rounds 0x005D9F14..42; DriveToCube and single
drive tuples; look and request 0x005D9BEE..9C42, 0x005D95DA..9642; the motion-profile join 0x005BE518, 0x005BD584,
0x0064AB10, 0x0064AC4A, 0x0064ADD2 (no regression).

**The random pose holds:** 0x005D92E2..93FE is current R x Rz(yaw + delta), which doubles yaw when planar, not
conventional yaw + delta. An independent recompute gives the test's quaternion.

## Queued

1. Assert the DriveToCube arguments, the look result gate, Request and the final turn plus clear.
2. Add a `// fidelity:` tag and standard `reach:` labels.
3. Add the host-execution and generation guards the six recent native classes have, or state why not.
4. `IBehavior.cs:369` still says the helper returns false; the FreeplayTool warning names Energy only, though Repair
   binds the legacy class too; the legacy XML keeps the swapped units.
5. The proposed lifecycle record goes to the operator.
