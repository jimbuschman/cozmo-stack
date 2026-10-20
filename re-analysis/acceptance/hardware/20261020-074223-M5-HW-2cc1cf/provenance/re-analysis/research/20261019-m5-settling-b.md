# M5 settling pass, part B (Opus, read-only), 2026-10-19, HEAD 25f9ab57

- **Run:** read-only cozmo-verifier over streaming, idle and neutral (M5-008, 010, 027, 030, 062, 038), driving
  (M5-063) and teardown (M5-037, 044, 053, 055, 059, 064), the 8 COMPATIBILITY_POLICY records and the hardware coverage
  list. Records quoted from the current manifest; addresses re-read (capstone) only where doubtful or touched after
  2026-10-13 (730ccc23, c41bc704, ff31b1f0, 3c7b7969, 33b80553, fd73bc88, 86d287d6: wiring/locking, plus M5-023's A22/A23).
- **Commands:** `fidelity.py --check` exit 0 (706 records). Focused `dotnet test` 413/413 (M5AnimationTests,
  M5X9/X12–X21, M5FinalBuilder, M5FixRows, AnimationGapTests, AnimationStreamLifecycleTests, KeepAliveTests, M1_031_CC6,
  M3_013_A13_A29_A12, ActionCompletionTests, M1_025_M1_015_CB33, M3M4X12, M5LastReads, M3M4X22).
- **Overall: FAIL.** One blocking finding (M5-030's head input, the defect part A found for the group draw) and one
  record correction needing re-approval (M5-020 `live_path`).
- **Adoption:** by the manager on 2026-10-19, with part A (see `acceptance/M5-blockers.md`).

## Verdicts

| Record | Verdict | Reason |
| --- | --- | --- |
| M5-008 | SETTLE after queued cleanup | A4..A9, A13, A27 match; the VERIFY tail is built (`AnimationScheduler.cs`:762-768; `EngineLog.cs`:87-91 → `Das.ForceFlushNow`); the "MISSING verify callstack / provider flush" text is stale |
| M5-010 | SETTLE after queued cleanup | All three items reopened by the B-M5-13 check are closed (below); `unresolved` still opens "Reopened 2026-10-12" |
| M5-027 | SETTLE after queued cleanup | Loop exhaustion to layers/flush with no idle test (0x0057D24E..0x0057D260 → 0x0057D04E); null-idle skip under M1-034; four per-connection MISSING lines over-report |
| M5-030 | **NOT YET (in-layer, cheap)** | Live idle head input is `robot.State.HeadAngleRad ?? 0f`; native reads robot+0x2FC |
| M5-062 | SETTLE | 0x0057E860..0x0057E8B4 re-read; matches `AnimationScheduler.cs`:661-675, `TrackLayers.cs`:546-553 |
| M5-038 | SETTLE after queued cleanup | Factory arguments match; `SleepTriggerFactory` has no production writer, so production stops at a visible MISSING and the M1-045 fallback; cross-layer handoff to M8-020/M8-021 (actions) and M8-017 (compound); M8-021 does not name the seam |
| M5-063 | SETTLE after queued cleanup | Bodies built and checked (B-M5-15..22 ends in PASS); remaining MISSINGs owned by M7-026, M15-001, M8-020/M8-021, M12-024, M7/M15 callers; MISSING list partly stale |
| M5-037 | SETTLE after queued cleanup | ~AnimationStreamer 0x0057AF58..0x0057AFD4 re-read; matches `Destroy()` (`AnimationScheduler.cs`:1985-2002); no send; stale MISSING text |
| M5-044 | SETTLE after queued cleanup | TLC delete 0x0057AF8C..0x0057AF98, parameter maps 0x0057AFBE..0x0057AFCC, +0xC release last (0x0057AFD4) = `ReleaseParameterSubscription` last; stale text |
| M5-053 | SETTLE after queued cleanup | Release back to front B6/B5/5A then C4/AD/AF/AE (`AnimationSubscriptionOwners.cs`:28-37; `CozmoAnimations.cs`:240-247); stale text |
| M5-055 | SETTLE after queued cleanup | ~CozmoEngine 0x004EC68C..0x004EC722 re-read; matches `CozmoEngine.Dispose` (`CozmoEngine.cs`:2459-2490); timer lead belongs to M8-015 |
| M5-059 | SETTLE | `AnimationTransferCleanup.cs`:10-38 matches the rows |
| M5-064 | SETTLE after queued note | No observable step; engine+0x38 delete skipped when null (`cbz` 0x004EC6F0); record the writer-search scope |

## Objections

1. **`CozmoAnimations.cs`:215 | CONTRADICTED (blocking; M5-030).** `live.HeadAngleRad = () => robot.State.HeadAngleRad ?? 0f;`
   reads the raw report (`CozmoRobot.cs`:73), null before the first state and after a reset, then 0. Native
   UpdateLiveAnimation keeps the Robot in r5 (`mov r5,r1` 0x0057D604), reads `vldr s2,[r5,#0x2fc]` 0x0057D83E, × 0x42652EE1
   (0x0057D838/0x0057D842), `vcvt.s32.f32` 0x0057D84E into HeadAngleKeyFrame (0x0057D85C). The stack models +0x2FC as
   `Motion.HeadAngleRad` (`Motion.cs`:158, 264; −25° from the ctor, written once HeadCalibrated, clamped
   `Motion.cs`:1462-1469). The 0x93 head keyframe sent to the robot carries a different angle before the first state,
   while uncalibrated, and on out-of-range reports. Reached through M5-057's app input. The comments at
   `CozmoAnimations.cs`:197-199 and `AnimationScheduler.cs`:2059-2063 say the opposite of the code. No test asserts the
   join (`M5AnimationTests.cs`:1758, `KeepAliveTests.cs`:359 substitute it). The other live inputs are equivalent:
   MovementComponent::Update writes +9/+0xA/+0xB/+0xC from status bits on every state (0x0063E30A..0x0063E340).
2. **M5-020 (`live_path: false`, "unreachable in the stack") | MANIFEST (re-approval).** `CozmoFace.ShowExpression` /
   `HoldExpression` (`CozmoAnimations.cs`:749-779) are live and play `Expressions.TriggerFor` triggers through
   `PlayTrigger` (Neutral goes through `Display.Show`/`Hold`). Only the index-0 Unity-Random path (`TriggerForIndex`,
   called only by `Cozmo.Conformance/Anim.cs`:306) is tool-only. The basis is the operator's 2026-09-29 decision. The
   "built, awaiting strong verification" prefix contradicts the policy status.
3. **`AnimationSubscriptionOwners.cs`:19-20 (built at `CozmoAnimations.cs`:241) | queued.** Every connection logs
   "MISSING" for the 0xAE/0xAF/0xAD/0xC4 callback recipients. AE/AF are fed by `PushIdleAnimation`/`RemoveIdleAnimation`
   (`AnimationScheduler.cs`:995, 1012) from the host API (M5-057); AD is M5-061; C4 is M9-029. Over-reports; hides nothing.
4. **`AnimationScheduler.cs`:749 | queued.** M5-061 says 0xAD is "never fed", but the public
   `Scheduler.ReplayLastAnimation` runs A27 under `_gate`. Engine behaviour exact; record wording.
5. **Stale `unresolved` | queued.** M5-008 (verify callstack/flush), M5-010 (Reopened…), M5-027 ("X3-X5 bypass idle",
   built at `AnimationScheduler.cs`:1113), M5-037/044/053 (B-M5-1 handle-ownership MISSINGs, superseded by B-M5-13/15),
   M5-063 ("M12-032 HasPathToFollow, PlayStartAnim body", built by B-M12-1 and B-M5-17), M5-055 ("UNKNOWN: engine+0x38"
   is M5-064's).
6. **M8-021 | queued.** Does not name the sleep seam (`CozmoAnimations.cs`:622, `SleepTriggerFactory`) or 0x0052CEDE.
7. **M5-055 timer lead → M8-015.** After `removeInstance` (0x004EC6B4) a later `getInstance()` makes a zeroed
   BaseStationTimer; the only candidate readers are action destructors under ~Robot's ActionList teardown (M8-015). The
   stack does not remove the timer before `RemoveRobots`. Does not touch M5-055's order claim.
8. **Tests | queued.** M5X15 `ParameterMessageUsesBaseSetter…` is labelled unit but enters through
   `SetLiveIdleAnimationParameters` → `Engine.Tick` (production); most M5AnimationTests/M5X9/X12/X14 unlabelled; M5-010,
   M5-027, M5-063 test fields list bare class names with duplicates.
9. **Races:** none blocking (Push/Remove/Replay/Stop take `_gate`; driving B5/B6 via `PostGameMessage`; `Destroy` under `_gate`).
10. **Circular tests:** none (M5_030_L1_L8 head 28 = trunc(0.5·57.2958); M5X15 45000 > 30000 and 3000/4000 from
    0x0057E8A2 → 0x0057C178 and A34; M5_038 0x42700000 and D2/D5/D4 from 0x0052CECA..0x0052CF44).

## Per record (supported)

- **M5-008:** SetStreamingAnimationLocked A4..A9 (`AnimationScheduler.cs`:785-831), IncrementTagCtr 1..0xFE (:838-848);
  custom-song gate M9-029's (:778-783). A13 gate native `+0x72 && !HasFramesLeft && +0x94 == 0`
  (0x0057CFFA..0x0057D010) vs `Stream.IsEmpty` (:1103): zero-size FIFO entries drain with no budget
  (`AnimationScheduler.cs`:203-205), equivalent on the production path. A27 at :749-758 (gate 0x0057B14A).
- **M5-010:** (1) ResetToConstructed uses `SetNeutralResetData(…, reportConstructionFatal: false)` (:2050-2052), skipping
  on a miss (:714-724; 0x0057A1FA → 0x0057A214). (2) The read runs once per Robot from `BindRobotLifetime`
  (`CozmoAnimations.cs`:222); `LoadFrom` no longer calls `LoadNeutralFace`; early-Robot log order asserted by
  `M5X14…RobotConstructionReadsNeutralOnce…`. (3) Faceless neutral under M1-034 (0x0057A20A/0x0057A20C), a visible skip
  keeping prior reset data (:719-722).
- **M5-027:** no-animation path (:1144-1229) = A28/A29/C4; StreamLayers else flush with the +0x94 count (:1232-1241;
  0x0057D04E..0x0057D1E2); ProceduralLive UpdateLiveAnimation first, nonzero returns without the tail
  (0x0057D086..0x0057D0E6); +0x44 += 60 (0x0057D442).
- **M5-030 (apart from objection 1):** gates +0x194 0x0057D606; +0x44 vs GetParam<int>(2) unsigned (`blo` 0x0057D61A);
  DockingComponent+4 0x0057D61C..0x0057D624; MovementComponent+9 0x0057D626..0x0057D62C; AreAnyTracksLocked(4)
  0x0057D636; RadToDeg/VcvtS32 (:1262-1265) bit-exact.
- **M5-062:** size compare (0x0057E870..0x0057E880); SetDefaultParams (0x0057E882..0x0057E88C); base SetParam per pair
  (0x0057E89A..0x0057E8B2) bypassing the 30000 cap; mismatch sErrorF 0x0057E8D0 and errG; engine-thread delivery
  (`CozmoAnimations.cs`:264-268).
- **M5-038:** `GoToSleepAnimation.cs`:13-34, D2/D5/D4 with (1, true, 0, 0x42700000, false); parallel root with the M4-028
  lift child; null child → visible MISSING, null; `QueueGoToSleep` (`CozmoEngine.cs`:2395-2412) raises the fallback.
- **M5-063:** handler wired once per Robot (`CozmoAnimations.cs`:223-232); E2G 0x5A from `robot.ActionList.GameSend`.
- **M5-037/044/053:** ~AnimationStreamer: ClearSendBuffer 0x0057AF58; +0x1B4 handles 0x0057AF6C; live +0xA8; list +0x8C;
  TLC delete; idle vector +0x48; parameter maps; +0xC release 0x0057AFD4. `Destroy()` same order, M5-049's waiter release
  first (policy).
- **M5-055:** ~CozmoEngine: BaseStationTimer clear/removeInstance (0x004EC6A0..0x004EC6B4), VizManager::Disconnect, +0x64
  AnimationTransfer, +0x50, +0x40, +0x3C, +0x38 (`cbz`), +0x34 context (RemoveRobots, loader), +0x30 UiMessageHandler,
  Json, +4 vector. Stack Dispose: Das (M3-055), DestroyAnimationTransferMembers, RemoveRobots in key order without
  disconnect broadcasts (`CozmoEngine.cs`:1666-1678), loader abort/join, hub.
- **M5-059/064:** CleanUp quiet, regular files, "/" join; +0x38 delete skipped when null (0x004EC6EA..0x004EC6F6).

## Test reach

| Record | Through production | Unit only (what is substituted) |
| --- | --- | --- |
| M5-008 | none labelled | M5_008_A4_A5_A9, A13, A27, AnimationGapTests; M5X9 Verify… (reflection) |
| M5-010 | M5X14 RobotConstructionReadsNeutralOnce… (unlabelled) | M5_010_A2/A6_A31/P1_P2; M5X13/X14 faceless and miss |
| M5-027 | M3_013_A13_A29_A12 (unlabelled) | M5_027_*; M5X12 null-idle; M5FinalBuilder flush/overflow |
| M5-030 | **none** | M5_030_L1_L8, KeepAliveTests (`LiveIdleInputs.HeadAngleRad` substituted); M5FinalBuilder LiveAppendFailure |
| M5-062 | M5X15 ParameterMessage… (mislabelled unit) | — |
| M5-038 | EngineAppLayerTests M1_031_CC6 (fallback only) | M5_038_Factory… (stub child) |
| M5-063 | M5X19 (labelled production) | M5X15 driving tests; M5X20/X21 |
| M5-037/044/053 | M5X13 Shutdown… (labelled unit) | M5X9 release order (injected factory); M3M4X12 ClearSendBuffer |
| M5-055 | M5X13 Shutdown… (labelled unit) | M12CheckedRows gate tests |
| M5-059/064 | M5X15 CacheCleanup… (labelled unit) | — |

## Policy confirmation

| Record | Legitimate? | Basis |
| --- | --- | --- |
| M5-020 | Substance yes; record wrong | Operator decision 2026-09-29 makes expressions a stack API; `live_path: false` contradicted (objection 2) |
| M5-042 | Yes; SetSdkStatus half now checked | SetText produces nothing (2026-10-13). SetSdkStatus (0x004EAD90) → context+4 vtbl+0x38 → 0x0066340E → 0x00663886..0x006638C4 only stores into UiMessageHandler+0x68: no send, no log; its reader UpdateSdk reads +0x8C gated on +0xE1 and an SDK socket (0x00662F0A..0x00662F20): SDK-only |
| M5-047 | Yes | Operator departure (AGENTS, M5-047) |
| M5-048 | Yes | App input; seed 0 gives entropy in both; earlier delivery no observable effect |
| M5-049 | Yes | Stack waiter machinery; nothing sent, no engine log (`AnimationScheduler.cs`:1989) |
| M5-057 | Yes | App input; its live path is M5-030 (NOT YET) |
| M5-060 | Yes | App input (cache path) |
| M5-061 | Yes, wording queued | SDK/CodeLab/debug inputs; ReplayLastAnimation reachable through the public Scheduler |

## Hardware coverage for the M5 script

Reuse: `ControlCheck.cs` (ANIM_CANCEL: 0x8D count, no End after cancel, `keepAliveStream`, `neutralReplay`; FACE, AUDIO
AnimationState counters) and `M3M4Hw.cs` (ANIM_BUDGET, TRACK_UNLOCK, DISCONNECT_QUIET, frame recorder, bundle writer).

1. **M5-036 abort:** long clip, cancel mid-stream. Exactly one 0x8D; no EndOfAnimation for the cancelled tag; at most
   one built frame after (A22/A23/A25, M5-023). Robot side: NumAnimBytesPlayed stops, ended/aborted echo, ClientDropCount.
2. **M5-036 echo and tags:** three back-to-back plays: Start tags 1, 2, 3 (IncrementTagCtr 0x0057B660..0x0057B672); idle
   inits 0xFF (A29); record the robot's started/ended tags.
3. **M5-036 Start without End / keep-alive:** abort to nothing: no animation traffic for 0.5 s, then keep-alive face and
   neutral replay with a new tag (A31 0x0057CF6A..0x0057CFF2; M5-010); live idle: Start 0xFF, no End for 10 s (A29/L8).
4. **M5-036 locked tracks:** head locked, clip with head keyframes: 0x93 still streamed; robot head stays put; unlock
   sends one EnableAnimTracks (M4-047).
5. **Audio framing:** every AudioSample 744 bytes, AudioSilence where silent; streamed − played ≤ 14 (C9 0x0057C79E);
   bytes ahead ≤ 8192; ≈ 30 frames/s over ≥ 5 s; NumAudioFramesPlayed tracks FramesStreamed.
6. **Face and backpack layers:** face-layer-only stream sends animFaceImage every frame, BackpackLights on change,
   Start/End around it (A36/Q1.8/Q1.9).
7. **M5-030 live idle (after the fix):** 0x93 head angles within ±HeadAngleVariability of
   (s8)trunc(Motion.HeadAngleRad·0x42652EE1) (0x0057D838..0x0057D84E); before HeadCalibrated if possible: −25 base, not 0;
   body/lift wiggles per A34 defaults.
8. **M5-062 parameters:** BlinkSpacingMaxTime_ms = 45000 accepted uncapped (0x0057E8A2); longer blink spacing over 60 s.
9. **M5-063 driving** (`--allow-drive`, short DriveToPose): start/loop/end triggers queued in parallel (QueueAction(5));
   end completes before the drive result; default triplet (0x004F0D2C..0x004F0D74, 0x004F1016..0x004F105A, M12-024).
10. **M5-038 / M1-045 sleep:** visible M5-038 MISSING, GoToSleepRequested, no D2/D5/D4 (records the known gap).
11. **Teardown (M5-037/053/055/059):** Dispose during a clip: no animation message after DisconnectRequest (ClearSendBuffer
    0x0057AF58; DISCONNECT_QUIET); a stale `<cache>/TestAnim.json` is gone after construction and after Dispose.

## Manager actions proposed

1. Build (M5-030 with part A's M5-011/M5-014): wire `live.HeadAngleRad` (`CozmoAnimations.cs`:215) to
   `robot.Motion.HeadAngleRad`; fix comments `CozmoAnimations.cs`:197-199, `AnimationScheduler.cs`:2059-2063; production
   test (ProceduralLive through the public API, head uncalibrated, 0x93 base −25).
2. Record M5-020: `live_path` true under the 2026-09-29 decision; drop "built, awaiting strong verification".
3. Record M5-042: add the SetSdkStatus read.
4. Handoffs: M5-055 timer lead → M8-015; M8-021 names `SleepTriggerFactory` (0x0052CEDE); M7-026 cites the driving use
   0x004F0D2C..0x004F0D74 at its own approval.
5. Queued cleanup: stale `unresolved` (M5-008, 010, 027, 037, 044, 053, 055, 063); per-connection AE/AF/AD/C4 MISSING
   lines; M5-061 wording; reach labels and duplicate test-field entries; M5-064 search scope.
6. Settle now: M5-059, M5-062. After 5: M5-008, 010, 027, 037, 038, 044, 053, 055, 063, 064. After 1: M5-030. M5-036
   waits for the hardware run.
