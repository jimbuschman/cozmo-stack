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

### Batch M5-B (built; verifier PASS)

- Records: M5-019 (write-back is the animation's face before the layers combine; already matched, pinned by a test),
  M5-023 (abort resets the streaming keyframe when set, else the idle's; already matched, pinned by a test),
  M5-027 (the +0x64 flag's writers/readers; the Count-top flush order; HasResponse; `RemoveIdleAnimation` clears
  +0x34/+0x64 only when the new top is Count — corrected after the verifier's first FAIL, and C5's wording fixed),
  M5-032 (the face matrix is float CV_32FC1; the widening moved into the warpAffine port).
- Verifier FAILed first on the `RemoveIdleAnimation` clear being over-generalised; fixed and re-verified PASS.
- Stays IMPLEMENTATION_GAP: none of these records is settled yet (the manager settles after batch C).
- No other layer touched.

### Batch M5-C (built; verifier PASS)

- M5-020 rebuilt on the shipped Code Lab mapping: `Expressions.cs` now holds the 65-entry index→AnimationTrigger
  switch transliterated from `CodeLabGame.cs:3109..3244` (default `Count` = 0x23F); `UnityRandom.cs` implements
  libunity's xorshift128 and `Range(min,max)` exactly (R4/R5/R7/R8), seeded from the host `Environment.TickCount`;
  `CozmoFace.ShowExpression`/`HoldExpression` play the mapped trigger through the shipped animation-group path;
  `Neutral` stays the shipped neutral face. No invented poses remain.
- The `Cozmo.Conformance/Anim.cs` `face-expressions` tool is the wiring call site (now needs `--assets`).
- Seed value is UNKNOWN (Mono `get_TickCount` unread) → forced stand-in, flagged for the policy review.
- `AnimationTrigger.Count` is not in the generated enum (the generator strips the sentinel); represented as
  `(AnimationTrigger)0x23F`. The generator's `--check` is out of date for a pre-existing `M7-001` tag line, not this
  diff (for the integrator).
- Verifier PASS (full suite 1958/1958 at that point).

## Settled

Settled EXACT_SOURCE (9): **M5-001, M5-006, M5-010, M5-016, M5-019, M5-023, M5-027, M5-032, M10-002.** Each record's
whole claimed path is built, the rows were verifier-checked against the binary, and the diff was verified PASS.

## Left open, with why

| record | why it stays IMPLEMENTATION_GAP |
| --- | --- |
| M5-011 | mood and cooldown time come from MoodManager (M7) as stand-ins |
| M5-013 | the keyframe +0x28 (IsDone override) writers are RECOVERABLE_GAP (0x004F9770, 0x004F97DE) |
| M5-014 | the UseHeadAngle-less head-angle fields are uninitialised; the SD2 forced choice needs the policy review |
| M5-018 | the live audio animation is M6's; B-M6b-3 is BLOCKED |
| M5-020 | the seed value is a forced stand-in; needs the policy review |
| M5-021 | bionic libm's last-ulp trig; an EQUIVALENT_IMPLEMENTATION candidate for the policy review |
| M5-022 | PathComponent::Abort / AbortDocking are M12/M13's; DriveWheels(0) is this stack's |
| M5-030 | CarryingComponent (M12) and the M7-017 live idle interface |
| M10-001 | the M11/M12/M15 consequences are seams |
| M10-003 | Hiccup/CubeMoved +0x1C bodies RECOVERABLE; FistBump/Sparked/Pet/NoPreDockPoses not built |
| M10-004 | the C3 sticky first-action gate needs the M8 ActionList; the firmware index interpretation is HARDWARE_ONLY |
| M10-007 | RobotStateHistory::ComputeStateAt (M11) and the M4 SetNewPose fields |
| M10-008 | the M8 ActionList and the SetDefaultHeadAndLiftState caller are absent |
| M10-013 | forced policy MD1, to be COMPATIBILITY_POLICY at the next approval |

## For the policy review (do not change status in this job)

- **M5-014** — an entry without UseHeadAngle reads uninitialised head-angle fields; the stack's forced choice is
  "outside every head window". Needs classification.
- **M5-020** — the Unity Random seed value is unknowable; the stack seeds from the host clock (mechanism faithful,
  value a stand-in).
- **M5-021** — MathF vs bionic libm in the last ULP; an EQUIVALENT_IMPLEMENTATION candidate.
- **M10-013** — MD1, already a forced policy.
- **M10-004** — what the firmware does with a track index instead of a mask is HARDWARE_ONLY.