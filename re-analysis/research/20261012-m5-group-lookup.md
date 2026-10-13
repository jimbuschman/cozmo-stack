# AnimationGroupContainer::GetAnimationGroup and its callers (Opus extractor, 2026-10-12)

- **Run:** read-only cozmo-extractor, after the B-M5-14 check (`research/20261012-b-m5-14-check.md`).
- **Adopted:** by the manager on 2026-10-12. This is the checked source for the new M5 record and job B-M5-16.

## The lookup

- **Const** `GetAnimationGroup` (0x0058B6F8..0x0058B768):
  - it calls find (0x0058B6FE); a hit returns node+0x14;
  - a miss logs sErrorF, key "AnimationGroupContainer.GetAnimationGroup_Const.InvalidName", format "AnimationGroup
    requested for unknown animation group '%s'." (0x0058B724);
  - then it sets errG (0x0058B74A..0x0058B758), conditionally calls sDebugBreakOnError (0x0058B75E), and returns null.
- **Non-const** (0x0058B6F4): a `b.w` to the veneer 0x008CBA0C, which reaches the const PLT stub 0x004AE884. It is a
  pure forwarder.

## Callers (scan: Thumb BL/BLX/B.W, ARM BL/BLX, veneers, literals)

| # | Site | Caller | On a null result |
|---|---|---|---|
| C1 | 0x00561DE4 | `GetAnimation(trigger, robot)` 0x00561D54 (only from SayTextAction 0x00561A76) | returns null; no further log |
| C2 | 0x0057A0E2 | AnimationStreamer constructor | BadNeutralAnimGroup, errG, break, then TLC::Init. There is no empty-name check, so "" is looked up and logs InvalidName |
| C3 | 0x0057AE80 | `AnimationStreamer::GetAnimationNameFromGroup` 0x0057AE74 | null or IsEmpty → static empty string 0x0105AAC8; no further log |
| C4 | 0x0058B8A6 | `AnimationGroupContainer::DefineFromJson` | "Could not GetAnimationGroup named '%s'." with errG. Unreachable: find-or-insert comes just before |
| C5 | 0x0058B6F4 | the non-const thunk | forwarder |

**C3's callers:**
- TriggerAnimationAction::Init 0x0054446E (M8-021). An empty +0xBC name skips the lookup with a warning.
- AnimationStreamer::Update 0x0057D234, the idle path (M5-027), after HasAnimationForTrigger.
- BehaviorFeedingEat::TransitionToEating 0x005D61BC. Feeding is app-entered. What it does on an empty name is UNKNOWN.

**C1** is SayTextAction, which isn't built (M14-006, M7-019).

**Lookups that bypass the logging function:**
- HasGroup 0x0058B814 does not log and has no callers.
- AddAnimationGroup's find (0x0058B4AC) is the loader's find-or-insert and never logs.
- Inline probes of the map that skip find: UNKNOWN, not searched.

## The stack

| Native | Stack site | Difference |
|---|---|---|
| C2 | `AnimationScheduler.LoadNeutralFace` → `AnimationLibrary.GetFirstAnimationName` → `GetGroup` | no InvalidName or errG; and an empty group name skips the lookup, where native looks up "" |
| C3 via Init | `SteppedBehavior.RunTriggerAction` → `AnimationTriggerMap.Resolve` (GetGroup at :117, then GetAnimationNameFromGroup at :126, which calls GetGroup again at AnimationLibrary.cs:631) | two stack lookups for one native lookup; no log |
| C3 via idle | `AnimationScheduler.PickIdleLocked` → `AnimationLibrary.GetAnimationNameFromGroup` | no log |

**With shipped data,** all 573 trigger-map AnimName values are group stems, none empty. So the miss is reached only by
an early Robot (empty containers), or by an empty or unmapped trigger response.

## Correction

M5-011's "G0 silent empty" (fix rows §8) holds only for a group that exists but is empty. A name that isn't in the
container logs InvalidName and sets errG inside the lookup.
