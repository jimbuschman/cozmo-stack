# ChatGPT queue 3: Claude check (manager, 2026-10-31)

**Verdict: mostly sound, with one wrong config key, one false open point, and several wrong or missing labels. Nothing settled; no
status changed.** The report is `research/20261031-queue-3-chatgpt-extraction.md`. Use this note where the two differ. It marked all
eight items Partial, which is the honest status the new rule asked for. Checks were targeted at items 5-8 and at conditions, labels,
data addresses and open points. A row not named here was not re-read.

## Corrections

**1. Item 7 row 1: the JSON key is `strategyType`.** The report says `WantsToRunStrategy.Type`. The `adr` at 0x0061319A is the error
key `IWantsToRunStrategy.ExtractStrategyType.NoTypeSpecified`, passed as the default. The key comes from the `adr` at 0x006131AC:
`JsonTools::ParseString(out, json, "strategyType", default)` at 0x006131B2.

**2. Item 8 rows 12 and 20: there are no two vtable layers.** The "inner" addresses 0x0102E678 and 0x0102E6B8 are the *file offsets*
of 0x0102F678 and 0x0102F6B8. The second load segment has vaddr = file offset + 0x1000 (0x01019200 at 0x01018200). The virtual
addresses hold everything directly:

| vptr | Clone (+0x08) | operator() (+0x18) | Installed by |
| --- | --- | --- | --- |
| 0x0102F678 | 0x0064CB9C | 0x0064CBE8 | WriteCurrentUnlocksToRobot |
| 0x0102F6B8 | 0x0064CDFC | 0x0064CE34 | ReadCurrentUnlocksFromRobot |

So the report's open point closes, and the allocators in rows 13 and 16 are these clone slots, with no other creator to find.

**3. Item 2: RobotPublicState labels.** With the Unity class:

| Offset | Field | Note |
| --- | --- | --- |
| +0x00 | currentSpark (UnlockId) | The report's "robot identifier" is wrong. Default 0x55, the same no-unlock value as IBehavior +0x70 |
| +0x04 | isCubeInLift | |
| +0x05 | isRequestingGame | |
| +0x06 | tallestStackHeight | |
| +0x08 | needsLevels | |
| +0x14 | currentActivity | |
| +0x15 | currentReactionTrigger | Default 0x16 (0x0064D59E..0x0064D5A6) |
| +0x16 | userFacingBehaviorStageStruct: behaviorStageTag | Default 4 |
| +0x17..+0x1A | currentFeedingStage, currentGuardDogStage, currentPyramidConstructionStage, currentWorkoutStage | |

The report's claim that one stage value goes to all four bytes is **right**: 0x0064DB32..0x0064DB3E multiplies by 0x01010101.

**4. Item 3 step 3.14: the "optional/variant value" is a std::string length.** `ldrb +0x14; tst #1; lsreq #1; ldrne +0x18` is libc++
`size()`, the same pattern the report itself recognises at 0x00713792. The gate is: a string at face record +0x14 is not empty. The
binary has no getter symbol to name it, so "name" is likely but unproven.

**5. Item 5 rows 9-12: the two timing gates are sequential, not alternatives.** Rows 8-12 also leave out one branch and two values:
the `beq` at 0x005BD808, constant 0x005BDA1C = -1e-5f, and constant 0x005BDA20 = 1e-5f, loaded at 0x005BD84A and 0x005BD94E. The full
order is in the table below.

**6. Item 6:**
- **Field set:** it misses +0x3C and +0x40. OnSelected copies +0x38..+0x43 into a three-word `DrivingAnimations`
  (0x005B3166..0x005B316C).
- **Row 15:** the quote `0x005B34B0: lsls r4,r3,#0x19` is not an instruction. That address is inside the `add.w` at 0x005B34AE. This is
  the first inexact quote found in any report. The tree holds lock-name strings, not IDs: while +0x64 is nonzero, call
  SmartRemoveDisableReactionsLock with the first node's string (0x005B34B4..0x005B34C6).
- **Row 16:** the test is `cmp r0,#0` at 0x005B34DE and `beq`, not `cbz`.

**7. Item 1 step 1.04:** the compare is end against capacity, not begin against end, as step 1.06 says for the const copy. No effect
on the result.

## Item 5: the gates, in order

Any failed gate returns 0. The last gate decides the result.

| # | Gate | Field and key |
| --- | --- | --- |
| 1 | +0xA1 set: log "Behavior %s is already running", return **1** | +0xA1 is the running flag |
| 2 | +0x1C nonzero: IsProcessRunning must be true, otherwise error log and global flag | No key found, see "Still open" |
| 3 | +0x74 == 3, or it equals SevereNeedsComponent +0x14 | `requiredSevereNeedsState`, default 3 |
| 4 | +0x70 == 0x55, or IsUnlocked(id, true) | `requiredUnlockId`, default 0x55 |
| 5 | If +0x78 >= -1e-5: t = [[Robot+0x264]+0x18]+0x44 (time it left the charger, written 0x005C0DFA) must be >= -1e-5, and +0x78 + t + 1e-5 >= now | `requiredRecentDriveOffCharger_sec`, default -1.0 |
| 6 | If +0x7C >= -1e-5: +0x7C + 1e-5 >= now - BehaviorManager +0x50 | `requiredRecentSwitchToParent_sec`, default -1.0 |
| 7 | If Robot +0x355 (off-treads state, M10 A7) is nonzero: vptr+0x20 must return 1; the base returns 0 | Virtual |
| 8 | If Robot +0x34A (on charger platform, M7) is nonzero: vptr+0x24 must return 1; the base returns 0 | Virtual |
| 9 | If carrying (CarryingComponent +8 != -1): vptr+0x28 must return 1; pure in IBehavior | Virtual |
| 10 | +0x38 null, or WantsToRun true | `wantsToRunStrategyConfig` |
| 11 | now >= +0x118 | Stamp written by Resume (M8) |

Slots +0x20, +0x24 and +0x28 have no symbols, so their meaning comes from these gates only. Defaults: ctor 0x005BBCAC..0x005BBCBE.

## Closed beyond the report

- **Item 7, ObstacleDetected:** closure vptr 0x0102D650, operator() 0x006143CA, assigned to StrategyGeneric +0x68. StrategyGeneric's
  WantsToRunInternal (0x00613948) calls it only when the sets at +0x18 and +0x40 are empty and +0x78 is set. Its +0x80 latch is
  ORed into the result and cleared on every call. The latch writer is unread.
- **Item 7, slope rule:** pitch in (10, 55) && quiet for more than 0.4 s && (Robot +0x349 || now - +0x20 < 1.5) && Robot +0x355 < 2.
  The body (0x0061455C..0x00614688) agrees with existing inventory row M10 C16. The factory table is right: type 7 Slope, type 8
  Shaken, and the Unity enum matches. Class sizes for types 1-8: 0x18, 0x1C, 0x88, 0x20, 0x88, 0x1C, 0x48, 0x18
  (0x0061472E..0x006147BC).
- **Item 3, which results fire the callback:** all of them. The `void(Robot&)` StartActing wraps the callback in a closure (vptr
  0x010267E0, operator() 0x005BF8F4) that passes IBehavior +0x2C and tests no result. Only HandleActionComplete's gates apply (M8-001).
  So TransitionToS6_Done runs on any action result. The face callback's `== 0` test is ActionResult SUCCESS.
- **Item 6, idle lock:** OnSelected clears +0x50, so SmartPushIdleAnimation (0x005B32BC) always pushes, under the global string at
  0x0105AE60 plus the activity name, then sets +0x50. OnDeselected first removes the **plain** name (0x005B340C), which is not the
  pushed lock, then SmartRemoveIdleAnimation removes the real one. The first removal's effect is unread.
- **Item 6, chooser:** IActivity +0x24 is the chooser from BSRunnableChooserFactory (0x005B2DA2). Slots +0x20 and +0x24 are
  OnSelected and OnDeselected by relocation.
- **Item 8, all read-callback paths** (0x0064CE34):
  - Result >= 0, exact size: load, clear, take defaults, add ids 1..0x54 from 512 slots, SendUnlockStatus.
  - Result >= 0, too long: error log, no state change.
  - Result >= 0, too short: padded, but the buffer is freed at 0x0064CEEA before Unpack reads it. This use-after-free is already
    handled in `NurtureRestoration.cs` under M1-034/M15-034.
  - NV_NOT_FOUND: take defaults, SendUnlockStatus, then NotifyGameDefaultUnlocksSet.
  - Any other error: error log, no state change.
- **Item 2, subscribers:** two, already recorded in M13-038. Each Robot has two BehaviorAudioClient instances (BehaviorManager, then
  BehaviorSystemManager), and each ctor subscribes unconditionally at 0x00591F08. The stack builds them in `BehaviorAudioOwners.cs`. So a
  stack-height change reaches both clients synchronously. A by-address call scan for 0x0064DDB0 found nothing, so that scan is not
  proof that a function has no callers.
- **Item 4:** `BorderRegion+0` is the region's area sum, per M11-079 (0x006899D0..0x006899F4).

## Checked and matching

- **Item 1:** the filter layout against the stack offsets, vtable slots 0x0102E274, 0x0102E2B4 and 0x0102DD34. **Not named:**
  BlockWorld +0x90, ObservableObject +0x1C and filter +0x6C.
- **Item 3:** tag 70 RobotObservedFace, HighLevelActivity 1 Freeplay, closure slot 0x0102A538. Unity gives the RobotObservedFace
  names: faceID, timestamp, pose, img_rect, name, expression, smileAmount, gaze, blinkAmount, expressionValues, leftEye, rightEye,
  nose, mouth.
- **Item 4:** table 0x00C59AE0 and its 22 bytes, both literals, the enum strings. The rest was not re-read.
- **Item 6:** +0x3D8 and SparksRewardCommunicatedToUser, the duration check, vptr+0x2C.
- **Item 7:** the ChargerEvent tag (57). The PlacedOnCharger rows were not re-read.

## Still open

- **IBehavior +0x1C:** the ctor zeroes it, ReadFromJson doesn't write it, and the only EProcessFromString caller found is IActivity::ReadConfig.
  Its writer is not found, and the item 2 scan shows that search is not proof. If nothing sets it on a behaviour, gate 2 is dead. Do not assume that yet.
- ActivityFreeplay::CalculateDesiredActivityFromObjects 0x005ADF4C: not read. It is what item 3's function actually does.
- StrategyGeneric: config, event tables and AlwaysHandleInternal.
- BehaviorLookForFaceAndCube keys for +0x14D, +0x178 and +0x179.

## Method note

All eight Partial labels were honest, and the three new rules worked: it resolved veneers, closures and literals, and followed error
paths to their end. Its one recurring weakness is quoting multi-branch logic without stating the rule: the IsRunnableBase timing, the
slope rule and the read-callback paths. It has no repo or CLAD names, so it leaves labels blank or guesses them. A virtual address and
a file offset were confused for the first time. Depth in items 5-8 was comparable to items 1-4, and only one correction (the config
key) would have changed behaviour.
