DONE 2026-09-28 23:40 -05:00

## Commits
- `f1b6e65` B-M7: build M7-behaviour from its inventory; settle 14 records (verifier PASS, suite 1770/1770). (Rebased onto M14's push; FIDELITY_GAPS.md regenerated.)
- `248bac9` B-M7: claim job.
- this status commit.

## Records settled EXACT_SOURCE (14)
M7-001 (AnimationTrigger 575, ReactionTrigger 21, plus new `BehaviorClass.g.cs` 79 and `BehaviorID.g.cs` 179), M7-002 (reaction map loader + map-driven binding; 573/40 animation pairs), M7-003 (ReactToImpact FallingStopped/intensity/5 s wait/trigger 0x1a0/60 s), M7-004 (30 idle tunables), M7-005 (blink table; title now says seven-frame table, eight-keyframe track), M7-006 (eye shift/LookAt), M7-007 (persistent eye dart), M7-008 (60 ms idle timers), M7-009 (head/lift live keyframes; head uses the shipped 57.295780f constant), M7-010 (body shuffle + turn eye shift; zero speed is positive), M7-011 (reaction cooldowns), M7-013 (mood clamp/decay/arithmetic incl. sign-flip clock reset), M7-016 (face layer stack), M7-017 (live-animation wire interface).

## Gaps left
IMPLEMENTATION_GAP:
- **M7-012, M7-020** — the app-facing `MoodState` broadcast: the stack has no `MoodState` protocol type and no consumer of the engine-to-game seam. `HandleActionEnded` has no live caller because the stack has no `ActionList` action-ended callback (M8/M2).
- **M7-014** — the manager side, `_behaviorLock` naming and all 13 tables are built and the four M7-owned call sites are wired. Unwired: the 9 M12/M14/M15 call sites (CubeLiftWorkout, PeekABoo, PutDownBlock, FistBump, Bouncer, GuardDog, Dance, EnrollFace, OnboardingShowCube) and the 5 built classes whose tables are not among the recovered 13 (ReactToImpact, DriveOffCharger, Singing, AcknowledgeCubeMoved, ReactToOnCharger).
- **M7-015** — needs M10 and M7-021.
- **M7-018** — needs the M12-M15 class implementations (and FistBump, Hiccup, ReactToSparked).
- **M7-019** — ReactToCliff needs the `0x55b554`/`0x5c0ca8` helpers (M7-021); ReactToPickup needs M14 and `SayTextAction`; ReactToSparked needs the M15 spark source. The rest of the M7-owned reactions are built and wired.
RECOVERABLE_GAP (unchanged): **M7-021** (live; `robot+0x355`/`+0x338`/`+0x300`/`+0x37c`, `0x0055b554`, `0x005c0ca8`), **M7-022** (non-live developer test).

## Inventory correction
C1 (Appendix H): the extractor-recovered blink table (`0x00c5aad8`), idle head/lift draws (`0x0057d85c`, `0x0057d9c0`), body shuffle (`0x0057d6cc..0x0057d978`, `0x0057d7fc`), the M7-005 title wording, Appendix A row 9's wrong offsets, and the M7-002 row count (22 rows over 21 triggers). M7-behaviour re-approved; `fidelity.py --check` passes.

## Gates
- `python re-analysis/tools/fidelity.py --check`: PASS.
- `@cozmo-verifier`: PASS after fixing two blocking defects (mood sign-flip decay-clock reset, `Emotion::Add 0x00679618`; zero-speed turn eye-shift sign, `UpdateLiveAnimation 0x0057d790..`). Re-verified PASS.
- full suite `dotnet test cozmo-stack/Cozmo.sln`: **1770 passed / 0 failed / 0 skipped**; `AssetPresenceTests` green.

## Robot check the operator may run (optional; write the steps, do not wait)
The M7 idle/reaction path is exercised by the `behavior` conformance tool (the same path M7's `live_path` records rest on):
1. Cozmo on the floor, off the charger, on the robot's Wi-Fi; one cube powered nearby.
2. From `cozmo-stack`: `git pull`, then
   `dotnet run --project src/Cozmo.Conformance -- behavior 172.31.1.1 --obb "<unpacked OBB dir>" --seconds 90 --allow-motion`
3. Leave it alone: it should blink and eye-dart, and with `--allow-motion` do small head/lift/body shuffles. Then pick it up, set it on a cliff edge, and place it on the charger to exercise the reactions.
4. Copy the console output to Downloads and tell the manager.

## For the integrator
- The two mood gaps (the app-facing `MoodState` broadcast and the `ActionList` action-ended callback) are behaviour-changing parts of M7-012/M7-020's path with no record of their own. Before those records are settled they need their own records (the game-message layer and the action-list layer).
- `IdleBehavior`/`ReactiveBehavior` are instantiated only by `Cozmo.Conformance/Behavior.cs` and tests, not by `CozmoRobot`/`FreeplayStack` (pre-existing; noted by the verifier). The final wiring pass may want to run the idle decision layer from the robot tick.
- `M7-014`'s five fallback classes over-suppress reactions not in their (unrecovered) tables; their tables are not among the recovered 13.