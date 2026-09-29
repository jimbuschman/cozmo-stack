CLAIMED opencode 2026-09-29 14:02

Job R-BEH: close the remaining gaps in M7-behaviour and M8-framework, plus the policy
review's C records M8-004, M8-008 and M8-009. (M9-016 moved to R-M9 on 2026-09-29;
M15 to R-M15.)

## Triage (step 2), 2026-09-29

Manifest at job start: 374 records. Scope: every IMPLEMENTATION_GAP of M7-behaviour and
M8-framework, plus the policy-review C records M8-004/M8-008/M8-009.

Kinds: **compare** (code may already match the rows), **missing source** (a piece is not in
the rows and needs extraction), **cross-layer wiring** (needs another layer's built piece),
**blocked** (needs something not built anywhere, or HARDWARE_ONLY/BLOCKED_EXTERNAL).

### M8-framework

- **M8-004** (COMPATIBILITY_POLICY -> C): **missing source / cross-layer.** The engine's
  `IBehavior::IBehavior` 0x005bbb74 writes zero to +0x100/+0x104 (0x005bbd28); selection is
  by the activity chooser or the reaction map, not an in-code score. The stack's code-built
  behaviours carry 1.0/5.0. The config-driven chooser path (`ScoringChooser`/`FreeplaySystem`)
  exists; the in-code scores drive only the test-seam `BehaviorManager.ChooseAndSwitch`.
  Build: engine zero default + chooser selection. Blocked by M8-013's missing
  `SelectionBSRunnableChooser::HandleExecuteBehavior` / `ExecuteBehavior` message (M2/M10).
- **M8-008** (COMPATIBILITY_POLICY -> C): **compare/build.** The engine's
  `CalibrateMotorAction::CheckIfDone` 0x00547D38 has no timeout; `IAction` writes -1 at +0x74
  (0x00540c9e/0x00540ca2/0x00540ca6). The stack waits `CalibrationAllowanceSec = 5.0`. Build:
  wait with no timeout. Self-contained.
- **M8-009** (COMPATIBILITY_POLICY -> C): **compare/build.** `IBehavior::Stop` 0x005bd08c
  releases in fixed order (a) reaction locks 0x5bd12c, (b) idle 0x5bd148, (c) motion profile
  0x5bd158, (d) track locks 0x5bd16c..0x5bd174. The stack's `BehaviorScope.Dispose` releases
  most-recent-first. Build: fixed category order. Self-contained.
- **M8-011**: **missing source.** `SmartDelegateToHelper`'s callee
  `BehaviorHelperComponent::DelegateToHelper` 0x0056dad8 and its helper-stack runtime are
  unowned by any record. Buildable from the `.so` (extraction).
- **M8-013**: **cross-layer wiring.** Choosers built; `SelectionChooser.RequestBehavior` has no
  production caller (needs `SelectionBSRunnableChooser::HandleExecuteBehavior` 0x0060ac2c and
  the `ExecuteBehavior` message, M2/M10); concrete activity bodies are M7/M15.
- **M8-014**: **missing source / cross-layer.** `AIWhiteboard::UpdateBeaconRender` 0x0056aa3c
  and the three MessageEngineToGame handlers (tags 68/69/53) are unowned; `VizManager` is
  M11/M12.

### M7-behaviour

- **M7-012**: **missing source.** Mood model built and wired, but the engine broadcasts a
  MoodState message to the app (`MoodManager::SendEmotionsToGame` 0x0067b724..0x0067b7f8);
  the stack has no MoodState protocol type and no app-facing consumer of
  `CozmoEngine.PostGameMessage`. Buildable from the `.so` + M2.
- **M7-014**: **missing source + cross-layer.** (a) 9 classes whose table is among the 13
  recovered but whose call site is M12/M14/M15-owned; (b) five built behaviours
  (ReactToImpact, DriveOffCharger, Singing, AcknowledgeCubeMoved, ReactToOnCharger) whose
  lock tables are not recovered. Buildable partly (extract the 5 tables).
- **M7-015**: **compare.** ReactToPickup gates on robot+0x355/+0x338; M7-021 says those are
  built; the derived classifier is M10-owned. Confirm the code matches.
- **M7-018**: **missing source + cross-layer.** No config-driven factory binding; missing
  class implementations are the M12/M13/M14/M15-owned ones plus FistBump, Hiccup,
  ReactToSparked (M7).
- **M7-019**: **missing source + cross-layer.** ReactToCliff: the 0x55b554 action and the
  0x5c0ca8 helper are unread; robot+0xD9/+0xD8 unknown. ReactToPickup: M14 face detector,
  SayTextAction, the +0x11C/+0x120 retry arithmetic. ReactToSparked: M15 spark system.
- **M7-020**: **missing source + cross-layer.** `HandleActionEnded` has no live caller (no
  ActionList action-ended callback); the MoodState broadcast is the same seam as M7-012.
- **M7-021**: **compare.** The four fields and the pickup gate are built; open is the log-only
  state-name helper 0x005c0ca8 +0x58 store and its Info log line. Confirm whether any
  behaviour-changing reader exists.
- **M7-022**: RECOVERABLE_GAP (non-live developer test), outside this job's IMPLEMENTATION_GAP
  scope. Left as is.

## Progress

- (next) Policy records M8-004/M8-008/M8-009: verify the review's citations, correct,
  `--approve M8-framework`, build, verify, commit.