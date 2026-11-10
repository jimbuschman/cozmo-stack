# AI-BODY-TURN-TO-FACE-1

## Claims and evidence

- Direct G2E 0x2D and nested action 0x24 share the row at 0x010203C0 and wrappers 0x005261B0–0x005261D8. Factory 0x0052AECA normalizes the maximum with Radians at 0x0052AEE2, passes the signed ID and speech flag at 0x0052AEF4, and applies six motor setters at 0x0052AF0E–0x0052AF4E. This public profile uses zero motor overrides, restoring pan 5.2359877/10, tilt 15/20, and 5-degree tolerances. It disables speech and does not request mandatory verification. The absolute normalized bound is shared by the initial turn and the fine tune (0x0054C360–0x0054C37C).
- Signed nonzero IDs use the exact current-root lookup. Zero, a deleted reference, or the null smart reference made without an external interface uses the cached last pose (0x0053B1CC–0x0053B1EC, 0x0053B31A, 0x0054BD66–0x0054BDA0). A missing nonzero target with a valid reference does not fall back. Without a usable pose, the stock optional-verification path completes without motion (0x0054BEB6–0x0054BEBC).
- The retained runner performs the original pose conversion, local 0x46 verification and internal head/body lock (0x0054BDC0–0x0054BE30), ten-image wait (0x0054B79E), optional verification and fine tune. Matching local ID changes and deletions update the smart reference. The new public route uses the existing guarded AtEnd queue with zero retries and an original-owner cancellation. Direct native Now and arbitrary nested queue/retry parameters remain separate (0x00525A34–0x00525AC4, 0x00527950–0x00527A8C).
- Action type 0x29 uses DefaultCompleted variant 6, with no face ID in the body (0x0052B0A6, 0x0053FF14, 0x0075C84C and 0x0075C8F8). The task publishes the terminal scalar after concrete teardown and queue unlink. The original owner retires the smart references and the scoped unqueued route also releases the goal pose.

## Files and tests

- Motion.cs adds the scoped public request and result through the shared AI admission and completion helper. Vision/NativeLastFaceRunner.cs applies the zero profile and default completion variant, releases the scoped resources, and signals the original-owner task.
- AiTurnToFaceControlTests reaches public admission, camera/FaceWorld, real motor messages and observed verification. It covers signed explicit ID, cache versus missing ID, native no-interface fallback, queued remap/delete, max-angle wrap, default profile, exact cancellation and completion, reaction discard, owner replacement and a parallel successor at reference release. The raw assertion checks independent tag/type/result/variant fields instead of equating the earlier raw report to the later watcher copy. Focused 10/10 and affected 224/224 passed. The batch gate is recorded in QUEUE.md.

## MISSING

- Arbitrary motor overrides, named speech/trigger variants and the separate LastFace app message are outside this bounded public request. Recognition input, historical pose and physical motor timing remain separate.

## Workers

Reader requested gpt-6.1-sol at medium effort. Builder and independent reviewer used the configured default request. The manager independently opened the factory and checked its six setter calls. Source follow-up confirmed the native no-interface null-reference fallback. Review checked that AtEnd cannot start its successor before the old current action unlinks.

## Records

- M14-002 owns the verification frame default. The retained face turn also serves M13-014 and other behavior callers. M14-001 owns the face position, M13-020/M13-026 the motor and head projection inputs, and M8-015 the action queue and watcher. No broader record settlement is proposed.
