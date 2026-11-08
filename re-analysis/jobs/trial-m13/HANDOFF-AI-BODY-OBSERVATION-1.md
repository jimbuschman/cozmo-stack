# AI-BODY-OBSERVATION-1

## Claims and source

- Motion.GetAiBodyObservation takes the engine owner lock and reads the current robot. It returns that owner's last published immutable engine state and an advisory head-request flag. The host token distinguishes replacement owners even when they have the same robot ID. It is allocated outside the robot RNG and carries no owner reference. A missing or retiring owner yields no token or state.
- The advisory uses the same readiness predicate as the existing AI head admission: engine Ready delivery after startup names/End (0x004ED5F0–0x004ED60E, 0x004ECCBC), validated connected robot, first synchronized full state (0x0051293C–0x00512948, 0x00513C5C) and the live owner. The reaction gate at 0x005A2106–0x005A2110 is advisory here. The action queue still makes the actual external-tag decision (0x0053D94C–0x0053D970).
- EngineRobotState.cs caches the identical 109-byte projection object at existing publication (0x0052F6E4–0x0052F716, field selection 0x005180D8–0x00518252, pack 0x00711970–0x00711A62). The first synchronized state can store pre-origin fields even if origin admission fails (0x00512C3E–0x00512C4A). The snapshot uses the published engine fields and retains the existing localization owner and report timing.
- Legacy MoveAction head 0x12 and lift 0x13 have native empty completion variant 6 (jump table 0x0053FEEC–0x0053FEED to 0x0053FF0E–0x0053FF14). The existing 5A projection now delivers it for both legacy actions. Other action types and the carrying low-lift placement dispatch retain their own completion owners. Direct motor commands have no ActionRunner completion.
- Native runner teardown builds then sends 5A after destruction/unlink (0x0053FA4C–0x0053FA66, 0x0053FAAA–0x0053FAB2), before that tick's state report (0x005140BC, 0x0052F706–0x0052F716). Retirement uses the original owner (0x0052F248–0x0052F364, 0x00511146). Historical queued reports and the AI command's exact terminal task remain separate.

## Files, tests and gaps

- Motion.cs adds the observation and the shared readiness check and aligns only MoveAction completion. EngineRobotState.cs attaches the immutable report and host token to the owner. AiHeadControlTests.cs reaches production admission, reports and snapshot through the fake robot.
- Tests check pre-state, shared published object identity from its callback, synchronous retiring-owner and same-ID replacement behavior, advisory disable flag, acknowledged head and lift messages and their typed/raw variant 6, and completion before the state report. Existing EngineAppLayerTests cover bad-origin publication. The reaction test sets the actual gate directly. A whole reaction activation was not added to this fixture.
- Final affected suite passed 303/303. The initial 302/303 test expected a pending-cancellation watcher record before it was available. The replacement test drives the native acknowledgement path.
- MISSING remains in the underlying state suppliers, including localization and vision, as recorded in their owners. General movement admission and observation beyond this head-command use are open. No hardware, whole-record settlement or approval changes.

Reader requested gpt-6.1-sol medium. Builder and reviewer used the configured default. Actual backends unobserved. Batch full gate passed at 412b6832 with 7834/7834 tests and zero skips. READY FOR CLAUDE.
