# SPARKS-BRING-CUBE-TO-BEACON-1: Claude check (manager, 2026-10-30)

**Verdict: FIX REQUIRED. Not merged.** Handoff `codex-m13-manager-2` 3adec271 (gate on f9cecf16, 6895 PASS).
**Nothing settled.** An adversarial verifier reviewed `git diff 35f305a9 3adec271`, and the manager re-checked the
blocking point in the .so.

## Blocking: the wrong driving-animation entry is removed at the Spark end

- The common OnSelected pushes the configured (Hiking) driving triplet at 0x005B319A under the activity's vtable+0x24
  name. The Spark hook (reached via `blx r2` at 0x005B324E, after that push) pushes the hard triplet 20E/20D/20F at
  0x005B1616 under the same name.
- ResetLightsAndAnimations (0x005B17A2..) gets the name through vtable+0x24 (0x005B17B8) and calls
  RemoveDrivingAnimations.
- RemoveDrivingAnimations 0x004F0A94 walks forward from `[this+8]` (begin), 0x18 bytes per entry (0x004F0B58). At the
  **first** name match it branches to the erase at 0x004F0BB8. That first match is the Hiking triplet, so after the
  Spark end (CheckEnd, or a reaction in state 3) the engine leaves **20E/20D/20F** on top until deselection.
- `NativeSparkedActivity.cs:116-124` (the Gather branch, line 121) removes the hard-spark lease by identity
  (`RemoveOwned(_drivingLease)`), so the stack keeps the Hiking triplet on top. A drive action initialised in that window
  (DrivingAnimationHandler.Init → UpdateCurrent reads the top entry) plays different animations on the robot.
- **Fix:** remove the first entry with the activity's name, as the non-delegate Sparks branch already does
  (`Remove(_activity.Id)`), keeping the lease bookkeeping consistent. Add a test that drives the Spark end through the
  real Update and asserts the top entry is 20E/20D/20F until deselection, then empty.

## Checked and agreeing (verifier, in disassembly)

Gather Update 0x005AF27C..3B2 (order, objective 11 via 0x005AF66C, needs 0x14, latch +0x78, external-interface gate);
the light helpers 0x005AF554..61E, 0x005AF6D0..768 and 0x005AF7B0..808 (layer 1, triggers 16/17); selected, deselected
and destroy 0x005AF818..866; the objective handler 0x005B181E..32; Spark Update 0x005B186C..18CC; OnSelectedInternal
0x005B15D0..16C2; OnDeselectedInternal 0x005B1780..179E; the delegate 0x005B1150..1184 and GetDesired 0x005B1FA4..1FC6;
StrictPriority's per-candidate running byte 0x0060B23E..27E; the Bring config and cooldown 0x005DF082..90 and
0x005DF148..194. The bool/default callers and the other activities' selection are unchanged.

## Not blocking (queue with the fix)

1. **Reach labels.** No new test in `SparksGatherNativeTests.cs` carries a `reach:` label. Several tests set State=2 by
   reflection or call Update/IsRunnable directly. **M15-023 is not settled** on `ConfiguredAliases…` (a unit-level test
   with a supplied stamp) while Sparks admission (M15-006/058) is MISSING.
2. **Untested claimed branches:**
   - the finish edge, Stop17+Play16 versus Play16, and the per-cube flag clear;
   - external-interface-off with no light change;
   - the 5 s cooldown at the 1e-5 epsilon.
3. **Operator visibility, not a defect.** SparksLookInPlace is unbuilt and last in the priority list, so `RetireUnavailable`
   (`NativeBehaviorRuntime.cs:147-158`) often retires the Spark early. It is visible, but it sends no HardSparkEnded, so
   an API consumer sees a start with no end.
4. **Shared changes.** `BehaviorManager.StopAndNullifyLocked` has a new early return on an epoch change during Stop
   (no production path found), and `CubeLightComponent.UpdateLocked` callbacks now run outside the gate. Neither changes
   the order; note both in the handoff.
5. **Cleanup:**
   - `_hardOutputs` is set before the push, where the engine sets it after StartLooping 0x005B1662;
   - the dropped M15-008 MISSING text no longer names PeekABoo/TrackLaser/GuardDog;
   - extra blank lines at the end of the test file.
