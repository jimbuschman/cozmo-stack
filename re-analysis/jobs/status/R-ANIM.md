# R-ANIM status

CLAIMED opencode/cozmo-manager 2026-09-29 10:40

## Scope

Subsystems: M5-animation, M10-derived. Every IMPLEMENTATION_GAP record as the manifest has it now.

## Triage (22 records; manifest as of 2026-09-29)

Pre-extraction `re-analysis/research/20260929-R-ANIM-pre-extraction.md` answers most missing-source questions
ahead of time; the manager spot-checked M5-006, M5-011, M10-004 and M5-013. Sending the report to
`@cozmo-verifier` before any row is approved.

### M5-animation (15 gaps + M5-020 policy)

| record | kind | pre-extraction | plan |
| --- | --- | --- | --- |
| M5-001 | missing source | F1..T3 answer the five JSON keyframe readers; `throws NotSupportedException` is not the original | build |
| M5-006 | compare/contradict | B1..B6: radius tokens compared case-sensitively; speed stored before radius | build |
| M5-010 | compare | P1..P8: `_resetData` null until the streamer ctor; layer base is the default face | build |
| M5-011 | missing source/contradict | 4.1..4.12: reject rules; no Weight/Mood defaults | build |
| M5-013 | compare/contradict | 5.1..5.8: threshold strictly above 0x80; lazy index reset; `_firstScanLine` | build |
| M5-014 | compare/contradict | 4.8..4.12: no defaults; head-angle fields uninitialised for `UseHeadAngle`-less entries | build + policy note |
| M5-016 | missing source | 6.1..6.5: NamedColors table; unknown name returns DEFAULT | build |
| M5-018 | cross-layer wiring | needs the M6 live audio path; B-M6b-3 is BLOCKED | stays open, naming B-M6b-3 |
| M5-019 | compare | 7a..7f: the animation's face is written back before layers are combined | build |
| M5-021 | compare | trig is MathF vs bionic libm; no new rows | stays open; EQUIVALENT candidate for the policy review |
| M5-022 | cross-layer wiring | M12/M13 `PathComponent::Abort`/`AbortDocking`; DriveWheels(0) is this stack's | stays open |
| M5-023 | compare | 8a..8c: the streaming animation's keyframe is reset when set, else the idle's | build |
| M5-027 | compare | 9a..9c: `+0x64` set by an idle InitStream; budgets refreshed before the drain; HasResponse | build |
| M5-030 | cross-layer wiring | M12 CarryingComponent, M7-017 live idle | stays open |
| M5-032 | compare | 10a..10e: the face matrix is float `CV_32FC1`, converted in warpAffine | build |
| M5-020 | policy (operator) | R1..R8: Code Lab pick is `Random.Range(1,34)/(1,14)`; xorshift128 | rebuild; remove invented faces |

### M10-derived (7 gaps)

| record | kind | pre-extraction | plan |
| --- | --- | --- | --- |
| M10-001 | compare | O1..O4: `Radians` `operator>` raw diff > 0, wrapping inside IsNear | build |
| M10-002 | compare | B7..B11: the +1 paths add l and r; the same-sign decrement is guarded by count > 0 | build |
| M10-003 | missing source | 5.1..5.3: Shaken/Slope/Frustration `+0x1C` is a no-op; Hiccup/others RECOVERABLE | build the three; rest stay open |
| M10-004 | missing source | 3.1..3.7: CompletelyUnlockAllTracks clears every non-empty lock set, sends the index | build |
| M10-007 | cross-layer wiring | M11 RobotStateHistory / M4 SetNewPose | stays open |
| M10-008 | missing source | 3.x, 4.1..4.8: ctor FLT_MAX; restore gate reads +8 only; MoveLiftToHeight fields | build |
| M10-013 | forced policy (MD1) | implemented; stays a forced choice | stays open; policy review |

## Batches

### Batch M10 (built; verifier PASS)

- Pre-extraction rows verified by `@cozmo-verifier` (all behaviour-changing rows PASS; two literal-address typos
  corrected in the report).
- Inventory correction C1 and manifest evidence approved (`--approve M10-derived`).
- Built: M10-001 (Radians operators), M10-004 (CompletelyUnlockAllTracks), M10-008 (ctor FLT_MAX, +8-only restore
  gate, compound action); M10-002 and M10-003 already matched C4/C5 and got tests and corrected comments.
- **Cross-layer file touched:** `cozmo-stack/src/Cozmo.Robot/Motion.cs` (M4-control) — added
  `CozmoMotion.CompletelyUnlockAllTracks()`, a new M10 method at the lock storage; no M4 behaviour changed.
- Stays IMPLEMENTATION_GAP with a precise `unresolved`: M10-003 (Hiccup/CubeMoved/FistBump/Sparked/Pet/NoPreDockPoses
  strategies), M10-004 (the M8 ActionList sticky gate), M10-007 (M11/M4 rewind), M10-008 (the M8 ActionList and the
  SetDefaultHeadAndLiftState caller), M10-013 (forced policy MD1).
- Test fix after the verifier: the vacuous `ShakenSlopeAndFrustrationIgnoreEnabledStateChanged` and the weak Radians
  `10.5f` expectation. Re-verified PASS.

### Batch M5-A (built; verifier PASS)

- Records: M5-001 (five JSON keyframe readers, no throw; CheckRotationSpeed limits), M5-006 (case-sensitive radius
  tokens; JSON string goes only to the token match), M5-010 (null reset data, default layer base), M5-011/M5-014
  (entry reject-and-continue, no Weight/Mood/HeadAngle defaults, the forced UseHeadAngle-less choice), M5-013
  (strictly-above-0x80 threshold, lazy index reset, unknown name consumed), M5-016 (NamedColors, DEFAULT corrected
  from 0xFF00CCFF to 0xFFCC00FF in the stack's 0xRRGGBBAA word).
- Verifier FAILed first on the unknown face-animation name (GetNumFrames returns 0 → IsDone true); fixed and
  re-verified PASS.
- Stays IMPLEMENTATION_GAP: none of these records is settled yet (the manager settles after batch B and C).
- No other layer touched.

### Batch M5-B (pending: M5-019, M5-023, M5-027, M5-032)

### Batch M5-C (pending: M5-020 expressions)