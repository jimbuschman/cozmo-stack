# PICKUP-STACK-1 — READY FOR CLAUDE

- Branch `codex-m13-manager-2`, rebased onto main93fa59f0 (AGENTS-only update); current native visual commit `c59de8a9`, N5 `9438e457` (original tested73795c9f/f904d92b). Full-gate candidate `e6973c7a` and current code/records are byte-identical; both recorded passes remain applicable. Shared path for the two shipped pickup variants; full pickup still reports MISSING. No finished-behaviour or hardware claim.

## Changed claims / addresses

| Claim | Primary evidence |
|---|---|
| Runtime factory copies actual target ROOT pose, raises Z by object dimensions, uses half-size and ignores target/family6. |00551FE6..005520BA;00569014|
| Owned Visual positions with parallel TurnPose(PI)+lift preset3/tolerance5, spends one transition tick after positioning, then queries after EVERY Wait update; match0300001D overrides the wait result. |005695BE;00569724|
| Actual MAIN/current origin and calibrated head feed native turning; live MAIN remains the stored parent across ReInit. |004EA398;0054A944..0054AA58;0051335E..005133E8|
| Native head solver uses actual MAIN-parented neck, engine camera transform and actual Vision-camera calibration (constructor/API/NV), sequential f32 operations, checked Radians comparisons and repeated clamp. |0050FF66/6A;00518386..005185DE;00510FFC..00511066;0054AA70..0054ACBA|
| Null calibration/conversion failure uses source fallback; convergence at iteration25 writes output before return1, exhaustion returns0 without writing. Existing public ownerless flat adapter remains separately labelled unverified. |005183E6..00518400;005185B2/B6;0054AB04/9A;0054B428..0054B564|
| Real owned Turn/PanTilt/head children retain native types/tracks/flags, current borrowed turn tag, independent embedded-compound tag, exact construction/update order and false CanInterrupt. |005459D4..00545A24;0054962C..005496CC;00549C48..00549D60;0054B344..0054B3E0;five vtable slots→0052B0B2|
| Actual local43 subscription counts ten mode1 messages strictly after timestamp0, without deduplication. Replacement subscribes before old release; success/replacement/destruction release; direct Cancel does not invent release. |0054CA64;0054CB68;0054CC1C;0054DD88;0054D0A4..0054D0D6|
| Visual preps position then wait, destroys wait before position; TurnPose/PanTilt retire owned children and turn eye/ack state. Normal head9C/9D matches ctor; omitted internalA0 has no normal-caller effect under existing M4-016 C11.3. |00569460..0056950E;00569624..62;0054D18C..0054D1FC;00545B94..BD4;00547F24..40;005485F8..608|
| N5 shared test Rig now snapshots initial transport epoch once into a separate controlled action clock. Deadline tests retain explicit advancement; all old assertions and production clocks remain unchanged. |REVIEW.md N5; host test machinery, no engine evidence required|

## Verification

- Independent READ/BUILD: `20261027-pickup-stack-1-{read,read-codex-check,codex-check}.md`. Actual queued runtime/motor/ack/status and worker→result-drain→43 tests, actual calibration API/NV owner, MAIN ReInit, current-tag retirement, transition/query override and timestamp/lifetime boundaries.
- Independent **466/466 PASS**,0skip,26s; N5 independent **287/287 PASS**,0skip,24s. Counts overlap; not summed. Supplied head-output epsilon tests are units; old raw-clamp control fails both. Old clock-coupling control fails the ORIGINAL status-latch assertion with Timeout; exact candidate bytes restored.
- Two consecutive asset-complete full gates on clean **e6973c7a: 6387/6387 PASS each,0failed/0skipped,exit0**. Run1:06:38:01..06:45:30UTC,6m52s test duration; run2:06:45:30..06:52:48UTC,7m08s (2026-10-27). Logs `.scratch/pickup-stack-full-{1,2}.log` and `-console-{1,2}.log`; fingerprint676806ffe936ee7a52409ea7d2bbfe0767254433. Every actual failure/control is retained in builder and manager logs.

## RECORDS TO SETTLE

- **M13-026**, current IMPLEMENTATION_GAP: “GetAbsoluteHeadAngleToLookAtPose and Robot::ComputeHeadAngleToSeePose”; current evidence0054B428..0054B55A/00518344..00518780 and checkedQ3. Own native body and actual receiver are independently checked/built; tests reach production turning and both calibration producers. Claude should classify using the EXISTING unshipped-libm boundary in current M13-023(e); no phone-last-bit EXACT claim or new departure. Exact iteration25 convergence input was not executed: instruction/code verification only; fixed exhaustion case passes.

## Leftovers / robot script

- **M13-020:** own legacy FaceActions TurnPose recipient still async; new owned subtree alone does not settle the record. **M12-037:** own Setup→IDock executor/turn-object join remains a stand-in.
- Full Pickup/IDock/first-turn/DriveTo/Search/activity admission/action-completion carrier remain visible MISSING; public M11-066 adapter and broader pose/geometry records stay open. No neighbouring gap is used to hold the M13-026 own-path proposal.
- Claude script: exercise finite visual head/body/lift positioning, actual image43 counts and occupancy match; full configured pickup still must report MISSING until its other children exist. Hardware runs do not block this handoff.
