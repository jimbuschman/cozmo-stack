# HIKING-BRING-CUBE-TO-BEACON-1: Claude check (manager, 2026-10-30)

**Verdict: PASS. Merged** from `codex-m13-manager-2` 35f305a9; the code is unchanged since the full gate on ebab4ee0 (6866/6866).
**Nothing settled.**

Read: `research/20261030-hiking-bring-cube-to-beacon-1-read.md`. Handoff:
`jobs/trial-m13/HANDOFF-HIKING-BRING-CUBE-TO-BEACON-1.md`. Owner:
`cozmo-stack/src/Cozmo.Robot/Behavior/NativeBringCubeToBeaconBehavior.cs`. Only the canonical `Hiking_BringCubeToBeacon`
configuration is built through it (the factory row, with a visible MISSING for any other configuration).

## Checked against the .so

| Read row | Address | Finding |
|---|---|---|
| R2 cooldown | 0x005DF148..0x005DF194 | Skips the test when \|stamp\| < 1e-5f. Otherwise it refuses when f32(f32(stamp + [this+0x130]) + (−1e-5f)) > timer seconds (`bgt`). This agrees with the read. |
| I1/I2 Init | 0x005DF348..0x005DF3DE | Stores −1 at +0x12C. If carrying, the target is vector[0].id followed by TransitionToObjectPickedUp; otherwise TransitionToPickUpObject(1). With an action the result is 0. With no action, GetActiveBeacon is dereferenced with no null check (0x005DF396), and the result is 1 iff \|now − stamp\| ≥ 1e-5f (`movpl`, so NaN gives 1). This agrees with the read. The C# `!(abs < eps)` gives the same NaN result. |
| P4 pickup callback | 0x005E0FD8..0x005E1288 | Category 0 → PickedUp. Category 4 with carried == target → PickedUp. Category 4 with attempt ≤ 2 (`cmp r0,#2; bgt`) → TransitionToPickUpObject(attempt + 1) (0x005E1210). A spent category 4 logs and falls through to 0x005E1166. Category 3 also reaches SetFailedToUse(obj, 0) (0x005E1186) through a located lookup with family −1. Every other category returns (0x005E1214). This agrees with the C# `PickupDone`. |
| B2 delocalization | 0x0056A686..0x0056A69A | RemovePossibleObjectsFromZombieMaps, UpdatePossibleObjectRender, then a tail call to ClearAllBeacons. No failure table is cleared. The stack's `OnRobotDelocalized` now does exactly this (`AIWhiteboardClear.cs`), and the AIComponent stage in `CozmoRobot.cs` calls it. This closes the beacon-clearing item I had queued. |
| zombie predicate | 0x0056A6B2..0x0056A6C8, 0x00620AB8..0x00620AD2 | It uses each entry's pose root ID. IsZombiePoseOrigin is false when the root is the current origin ([[w]+0x294]+0x10); otherwise it is `!AnyRemainingLocalizableObjects(id)`. This agrees with the C# loop. |
| K2 family-2 count | 0x0061C23C..0x0061C25A | BlockWorld's map at +0x48 is incremented (`ldr r1,[r0,#0x14]; adds r1,#1; str`) on the definition path. I checked the increment, but not the count of three built-in definitions. I accept that count on the Codex supplement (`…-family-count-unknown.md`) and its codex-check. The C# constant is commented to say it is not a universal invariant. |

## Read against the C# owner (no blocking finding)

- **Gates:** RunnableGate20/24 are false and RunnableGate28 is true (G1). `KeepsRunningWithoutAction => HasCurrentAction` (U1).
- **The stack-target and pickup-target split is kept.** The stack callback's failure path uses the captured stack ID; the
  retry checks that the carried object is the picked-up target (S4).
- **Floor:** NeedActionCompleted("PickupCube") runs before FindFreePoseInBeacon, whatever it returns (F1). On the
  NoFreePoses branch the stamp comes before HikingNoLocationAtBeacon, and no action starts (F5). The floor callback makes
  no needs call. It retries with the same copied pose, and any other nonzero category sets PlaceObjectAt with that pose (F6).
- **Unlock 10:** if it is unavailable, the run stops with a visible MISSING. It is no longer treated as unlocked.
- **The M1-034 skips** at 0x005DF36A (an empty cache when carrying) and 0x005DF396 (a null beacon) are visible MISSINGs.
  They name the owning record and the address.

## Settlement

- **M15-025 is not settled.** Its own order is correct on the native owner. But the handoff lists Hiking's parent
  admission as MISSING, so no production chooser reaches this owner yet. This is the same reason as for ExpressNeeds,
  Wait and TAB.
- **The other records are not settled either.** M15-008, M15-009, M15-019..M15-024 and M15-026 keep their shared or
  child gaps (DriveToPickupObjectAction's body, the libm and pose arithmetic, the native child results, the mood and
  needs sinks).

## Queued (not blocking)

1. **The lifecycle record** (read rows C1/C2, G1, R1–R5, U1/U2, T1) was added as M15-086 with the operator's approval (2026-10-30).
2. **Annotations:**
   - Wording that sits in frozen evidence needs the operator:
     - the M13-006 evidence says "an int at +0x10"; it is an f32 stamp.
   - Unresolved text only (manager):
     - the M15-011 unresolved says "full copied robot pose"; it is GetWithRespectToRoot;
     - the M15-022 unresolved says normal Ground verification is clear; the normal `true,false` enables it;
     - the M15-026 unresolved still calls the pickup child the old DockHelper stand-in.
3. **`CozmoRobot.cs`:** the AIComponent stage still prints the "body/recipient is not built" MISSING after calling the
   whiteboard. The text should name only the unbuilt recipients.
4. **Codex's cleanup:** the test reach labels, and the blank line at the end of `BringBeaconFirmwareScene.cs`.
5. **SparksBringCubeToBeacon** stays not started. Its 5 s cooldown and GatherCubes admission are not built.
