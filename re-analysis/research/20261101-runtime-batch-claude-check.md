# Claude check: canonical Freeplay parent and runtime inputs batch

Date: 2026-11-01. Branch `codex-m13-manager-2` at `e77435d2`, merged to main at `da7a584c`. Full suite on the merge:
7187/7187, 0 skipped. Jobs: HICCUP-STRATEGY-1, CANONICAL-FREEPLAY-PARENT-1, PYRAMID-RUNTIME-INPUTS-1,
TALLSTACK-RUNTIME-INPUTS-1.

**Verdict: PASS for all four, with one finding about reach.** The agreed done-test is met, and every cited path checked
below matches. But ordinary Freeplay still does nothing on its own, so the job order changes (see Reach).

## Checked in the .so and the C#

| Path | Addresses | Result |
|---|---|---|
| High-level change | 0x005A106C..0x005A1230 | Same value returns. Otherwise old deselect, switch to null only when the trigger is 0x16, resume always cleared, store, OnSelected, stamp +0x50, GameControl = (value != 1), choose only when the saved trigger was 0x16. `AdmitFreeplay` and `PrepareHighLevelChange` follow this. |
| Adapter tail | 0x005A4FA2..0x005A4FE0 | First Freeplay stamp when +0x54 is negative. The non-Freeplay branch clears +0x58 and +0x60; `HighLevelAdapterTail(false, ...)` has no caller yet, because other high-level activities stay unbuilt. |
| Put-down handler (M15-060) | 0x005AD9F8..0x005ADA7E | Treads byte 0, selected child, IDs 7 and 8, forced ID at +0x91, RequiredSparkId == 0x55, full OnDeselected, clear +0x80. Matches `OnRobotPutDown`; the raw-sentinel change is right. |
| Subscription (M15-060) | helper 0x005AD7BC..0x005AD846, tag 0x35 | Canonical runtime subscribes only with the external interface and unsubscribes in `Dispose`. |
| Hiccup evaluation | 0x00610694..0x006108A4 | Feature 6, unlock, disabled latch cleared before the state branch, strict occurrence gate, severe <= 1 cures, whiteboard +0x74 set before spacing, spacing drawn before the latch skip, post-decrement count, report before trigger write, needs 0x1A only after runnable. |
| Hiccup animations | tables 0xC76818, 0xC7681C; 0x0061062C | State 2 = 227 HiccupPlayerCure, state 3 = 231 HiccupSelfCure; 226 HiccupGetIn when remaining == total - 1, else 225 Hiccup. |
| Hiccup cure and disable | 0x0061098C..0x006109C4, 0x00610AF8..0x00610B40 | DAS, Reset, state, delay added only for a player cure, needs 0x19 (HiccupsEndGood) or 0x18 (HiccupsEndBad). Energy bracket then Repair, critical 3. |
| Hiccup tag 53 | 0x006109CC..0x00610A3E | States 5 (OnFace) and 2 (OnBack) write 5.0 at +0x220 and state 1. State 0 with state 1 is a player cure. C# enum matches Unity `OffTreadsState`. |
| Hiccup reset and destructor (M10-016) | 0x006101A0.., 0x0061024C..0x00610280 | Count drawn before occurrence, both counters set, start cleared, false report, whiteboard clear. Destructor: unsigned now > threshold sends the DAS event, then global clear, then base. 0x0057E010 is 0.5f at +0x1C0. |
| Pyramid intentions 9, 10, 11 | 0x00570988..0x00570B40 | Full cache then base cache with no fall-through; height-2 stack top; carried ID; any stack vetoes; CanPick. Static uses MIN of best9 and MAX of the object, or IsRestingAtHeight(0, 30). Top excludes both. |
| Tallest stack | 0x0062D844..0x0062D8C0 | First strictly greater unsigned height; empty gives an expired weak object. |

**Production construction:** all four run on the canonical path. `CozmoRobot.BindNativeBehaviorManager` constructs the
runtime before the reaction map, the Hiccup strategy receives the configured target and the shared whiteboard, and
Pyramid and TallStack suppliers are bound in the runtime constructor, with no test override. The batch also fixed real
construction bugs: tall-stack recipient storage was allocated after its first reader, and `Find` lost configured
behaviours once a reaction took them.

## Reach: what canonical Freeplay does today

The agreed job test passes: a shipped activity is picked by the canonical parent and a built behaviour starts with real
path traffic, in `CanonicalFreeplayPicksShippedPutDownAndStartsDriveOff`. A temporary probe ran 5 simulated minutes on
the real path, on and off the charger, with no pickup, cube or face.

- **Ordinary Freeplay starts no behaviour.** It selects Socialize, then PlayAlone at 300 s. Both end in `specialized
  child unavailable`, which retains the empty slot and reports MISSING. The only messages sent are backpack lights and
  NV commands. This is honest, with no invented behaviour, but a robot left alone sits still.
- **Why it holds:** `FreeplaySystem.ChooseActivityBehavior` refuses non-BehaviorsOnly types, PlayAlone and the needs
  activities (M8-013), and Look desired selection (M15-006). The hold stops the picker reaching Hiking or NothingToDo.
- **What does run:** PutDownDispatch within 5 s of a put-down on the charger, and Sparks. Six of 14 Spark activities have
  a Sparked owner; the other eight are refused.
- **Bound behaviours in ordinary activities:** NothingToDo 6/6, PutDownDispatch 2/2, Socialize 5/7, PlayAlone 8/10,
  Hiking 9/12, Singing 0/36. The missing ones include PounceOnMotion, FPPeekABoo, GuardDog, the Singing family and
  Hiking_LookInPlaceForUnknown.

So the target gap is no longer the parent. It is the child activities, and the behaviours are mostly built for them.

## Records

- **M15-060 settled,** EXACT_SOURCE on its own path, with test
  `DriveOffPilotTests.CanonicalParentKeepsPutdownSubscriptionAfterObserverDispose`. Its location moves from `Lights.cs`
  to `FreeplaySystem.cs`, where the handler and tag are.
- **M10-016 settled,** EXACT_SOURCE on its own destructor path, with test `HiccupStrategyTests`. Its location moves to
  `HiccupReactionStrategy.cs`.
- M15-006, M15-008, M15-031, M12-049 and the Pyramid and TallStack lifecycle records stay open, as the handoffs say.

**Limits kept open:** `SwitchToBehaviorBaseCore` on the ordinary branch relies on no resume being held, where the engine
always clears it. Explicit host subactivity admission keeps raw spark IDs. Neither is reachable through the high-level
path built today. GuardDog's disconnect stays a visible MISSING: the handoff is right that copying the standalone
binding would hide a missing owner, which overrides the CHECK 12 note.

## Progress

Records settled across the 18 start-path jobs: 5. Behaviours finished: 0 of 178. A behaviour also needs a robot run to
count as finished, so `m13-driveoff-hw --freeplay` is the first run through canonical Freeplay.
`DriveOffPilotTests.SparkOnChargerDrivesOffThroughCanonicalFreeplay` covers that route offline.
