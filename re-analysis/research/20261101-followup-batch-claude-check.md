# Claude check: five start-path follow-up jobs

Date: 2026-11-01. Branch `origin/codex-m13-manager-2` at `7f72d3e9`; Codex gate 7138/7138 at `a04a9f46`.
Covers HIKING-DRIVE-OFF-CHARGER-1, SPARK-JOIN-1, PYRAMID-STRATEGY-BIND-1, FISTBUMP-STRATEGY-1 and NEEDS-FIX-2.

**Verdict: PASS, merged.** M15-097 settled on its own strategy path. Nothing else is settled.

**The main finding is not a defect in these jobs.** The canonical robot still cannot run Freeplay.
`NativeBehaviorRuntime.AdmitActivity` refuses every activity except PutDownDispatch and executable Sparked owners, so
everything built for Freeplay selection is reachable only through the standalone `FreeplayStack`. See "What this leaves".

## Checked and holding

| Item | Engine | Result |
|---|---|---|
| Needs order: pause, robot, +0x39C gate, clear +0x68 for 0x0D and 0x55, deadline | 0x006960E0..0x0069615A | same; the clear runs before the deadline, so a cooldown-rejected song still clears |
| +0x68 is forceNextSong | 0x00693E7A (WriteToDevice, UnlockId conversion) | confirmed |
| Picker: missing bucket keeps current; empty bucket gives null; forced then desired by ID, no strategy; current uses askCurrent and WantsToEnd; others WantsToStart | 0x005ADC44..0x005ADCB8 | same |
| Deselect, store, SetActivityIDFromSubActivity, OnSelected | 0x005ADDFA..0x005ADE1E | same |
| Latch path sets askCurrent 0 only when active equals requested; request snapshot used by picker and pause; switch only on a changed pick | 0x005AE40C..0x005AE4F2 | same |
| Desired byte cleared on every pick path, including no-change and no-current | 0x005AE5A4..0x005AE5E8 | same |
| requireSpark default 0x55 | 0x005B28F8..0x005B2904 | same |
| Pyramid families 2 then 1, in filter+0x3C | table 0x00C63260; 0x005B4A12..0x005B4A2C | same |
| filter+0x3C is the allowed-family set; default origin mode is the robot frame | FindLocatedObjectHelper: set size at sp+0x7C and copy base sp+0x38; mode at 0x0061EBF2 | same as `BlockWorldFilter.AllowedFamilies` and its default |
| Query, then roll strictly after the deadline, then base, then full; full vetoes; base, or latch with 3 or more, clears +0x41 | 0x005B4A40..0x005B4B00 | same |
| FistBump handler: stamp 0 skips the cooldown; elapsed <= cooldown rejects; draw strictly below the promoted probability; expiry = now + expiration | 0x0060E8C0..0x0060E98E | same |
| FistBump evaluation: not pending false; target +0xA1 clears; strictly past expiry clears; robot+0x355 nonzero false; then the runnable test | 0x0060E7E4..0x0060E86E | same |
| FistBump reset: always clears; stamps only for argument 1 | 0x0060E9FA..0x0060EA14 | same |
| Lock name is own name plus `_behaviorLock` | 0x005BCE46..0x005BCE62, literal 0x00BF2B20 | same |

Production construction, checked for each job:

- **Pyramid:** bound in the `ActivityStrategy` owner constructor for every loaded Pyramid strategy, and resolved at
  query time because the runtime is assigned after the tree. Tests use a resource-backed `CozmoRobot`.
- **Spark:** `SparkNameInput` is bound in the manager constructor, so every path has it.
- **FistBump:** `FistBumpReactionStrategy` has no production caller. That is stated in the job and logged as M7-018, so
  this is dead code until the body exists. It is not a live reaction.
- **Needs:** the public method is exercised; only seeded test state makes `ForceNextSong` nonzero, and its positive
  producer stays a recorded gap.

The four test expectations changed in these jobs are sound: the two Hiking lock keys follow the suffix above; the
Pyramid objective test moved past the shipped 5 s floor; the Spark log tests now include the real names that `Lookup`
produces. None of them weakens another assertion.

The manager exclusion in NEEDS-FIX-2 (Unity onboarding whitelist, +0x39C) is within the manager's scope under AGENTS.md
and is recorded in M15-046 with its reason, and native default-off is kept.

## Not blocking

- **A requested spark still freezes standalone Freeplay when a Sparked activity wins.** The join is built, but a
  standalone `Activity` has no `SparkedOwner`, so that pick stops at `FreeplaySystem.cs:246` with MISSING. The freeze is
  now limited to that case. It ends with the parent job below.
- **One unknown child stops every pick** at `FreeplaySystem.cs:235`. Tests that pick from the shipped tree pass, so
  shipped data does not reach it.
- **Family 1 (Block)** is cited but has no test, because the object model has no ingress for it.

## What this leaves

| Since the start-path work began | Count |
|---|---|
| Jobs merged in the two batches | 14 |
| Records settled | 3 (M15-098, M15-100, M15-097) |
| Behaviours finished | 0 of 178 |

These jobs fixed real defects, so this is not wasted work, but the count will not move until the parent exists. Hiking,
Spark join, Pyramid and Needs all end at the same gap: M15-006 canonical parent admission. Its records and M15-008,
M15-025 and M15-059 name it. The next job is that gap, ahead of any other behaviour, and the first hardware script
waits for it.

**Settled:** M15-097, EXACT_SOURCE on its own path: unlock gate, filter, query before roll, 40% roll per 300 s,
base/full decision, and the BuiltPyramid end. M12-049 keeps the cache producer and M15-006 keeps canonical reach.
