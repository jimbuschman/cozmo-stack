# REWARD-GATE-1: Claude check (manager, 2026-11-02)

**Verdict: PASS after two failed verifier rounds. M15-071 and M15-093 settled, M15-111 added. In simulation a weighted needs action
no longer stalls Freeplay. No real robot has run this code.**

Started by Codex (`codex-m13-manager-2`, stopped at the usage limit), finished by a Claude implementer, checked by a separate Claude
verifier. Codex's reading and draft review were leads.

## What the verifier caught

The full suite passed before each of these.

1. **A hardware run could overwrite the robot's stored needs.** `m13-idle-hw` had a needs directory but no DAS or Lab input, so the
   copies never resolved while the write path still ran. Native never writes before 0x00694608. Fixed: `_nativeCopiesResolved`
   guards every robot needs write. `MissingLabInputsNeverOverwriteTheRobotsStoredNeeds` covers it.
2. **Hosts with no directory ran the daily machine on fabricated state.** Fixed: the check does nothing until the copies resolve.
3. **The canonical test supplied inputs no real run had.** Fixed: options come from `HardwareEngineOptions`, which `M13IdleHw` uses,
   with only locale, clock and paths overridden. The test now has a stored robot blob and asserts the first write carries it.
4. **Notification Generate ran every tick** on a host with no history, because a stack stop skipped the deadline
   (native 0x0068CFF0, 0x0068CCBC). Fixed in `Update`, with a test.

## Read directly

| Path | Addresses | Result |
| --- | --- | --- |
| Register order: report, daily, Generate, weight gate, chance, award, old pending | 0x00696366..0x00696402 | Matches |
| Weight gate: absolute value, `mi` with the epsilon, so NaN proceeds | 0x00696380..0x006963A4 | Matches |
| Chance: amount, level read once, 1 - amount/target, floor, strict RandDbl(1) | 0x00696D74..0x00696DF8 | Matches |
| Award: target - amount, percent + 0.5, truncate, signed max with the minimums, inclusive draw, raw add | 0x00697110..0x00697180 | Matches |
| Daily gate with prior bracket zero and force | 0x00696AD6..0x00696AEC | Matches |
| Deadline | 0x0068CCB8..0x0068CCC4 | Fix matches |
| App input | `SegmentationService.cs` 268-272, 334-344, 380-390, 479-499, 547-552 | Store matches |

**Read by the verifier only:** the rest of the star state machine and traversal (0x00696AAC.., 0x00698740, 0x006993F8, 0x0069CBB8),
report packing (0x0071732E, 0x0071715C, 0x00784414), throttles. All held. **Rests on Codex's reviews:** Lab, the Inventory and
Progression owners, and LocalNotifications bodies.

## Plain probe

The canonical test with hardware options, a stored robot blob at level 2 with 1 star, RollACube at 30 s:

| Scenario | Behaviours after 30 s | First EarnedSparks | Idle after the action | Holds |
| --- | --- | --- | --- | --- |
| Same day | 48 | 35.5 s | 4.3 s | 0 |
| New day | 53 | 35.3 s | 4.5 s | 0 |

The new-day run sends one 0x67 and one 0x66. The first robot write carries the stored level and stars.

## Open

- **A failed needs read with no device file writes defaults to the robot.** It matches the engine (0x0069BEC6..0x0069BED4, then
  0x00694EE4), so it isn't a defect, but a first run has no device file yet. The operator decides.
- Failed or short 0x182000 and 0x195000 reads are not compared with native: a later reward could write those blocks without the
  robot's values.
- Register keeps a stack difference: `SetPrevNeedsBrackets` 0x00696228 is not called, and prev is written in Detect. No live Play path
  was found where they differ.
- A null daily result, reachable only on unknown input, skips Detect and the tails, and `_nativeDailyHistoryKnown` survives
  reconnect. Both are unknown-input stops with no native counterpart. `allowRobotTail` over-skips in a case judged unreachable.
- D6 reaches the engine before Needs Init here. Natively Generate at startup reads uninitialised +0x28 (0x0069214C), which the
  M1-034 standing rule covers: the stack uses the store.
- The canonical test takes the robot-and-device branch; the hardware first run is robot only, traced by hand.
- Two possible flakes, unexplained: `NativeTrackerOwnerTests.ActualRobotRetirement...` (null at line 50, 3 s into a full run,
  3 of 3 alone) and `DesiredFaceDistortionTests.FreeplayStackWires...`.

## Records

- **M15-071, M15-093** EXACT_SOURCE on the paths above, with the open items in `unresolved`.
- **M15-111** COMPATIBILITY_POLICY: the host source of SetDeterminantData.
- **M15-062** stays open: Lab was not read here. **M15-018** keeps its status; its wording is corrected.

## Progress

17 records settled across 40 jobs, 1 of 178 finished.
