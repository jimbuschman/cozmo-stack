# Complete audit of the settled records (2026-09-29)

Every record that was EXACT_SOURCE on 2026-09-29 (239 of them) was re-checked by an independent `cozmo-verifier`
pass. The first wave's M7/M8 detail is in `20260929-audit-M7-M8.md`. Each pass:
- opened the cited addresses in `libcozmoEngine.so` (and the OpenCV and Unity sources where cited);
- compared the C# with them;
- checked that the whole claimed production path is recorded and is the live path;
- checked the tests for circularity.

The manager re-checked each central finding in the binary before acting on it. The checks are listed per layer below.

**Auditor depth.** M1..M4, M5 part B, M7, M8, M9 and M13 were audited by Opus. After a usage limit, M5 part A, M10,
M15, M11, M12, M14 and M6 were audited by Sonnet. The Sonnet passes found real defects (M11, M15, M14), but they ran
at about half the depth, measured in tool calls. Their layers held 83 of 89, against 72 of 150 for the Opus-audited
layers. Treat the Sonnet layers' pass rates as upper bounds until an Opus recheck of a sample (M12 and M5 part A first).

## Result by layer

| layer | holds | doesn't hold | the defects, in brief |
| --- | --- | --- | --- |
| M1 transport | 24 / 30 | M1-041 F; M1-015, 024, 025, 028, 031 P | Ready-to-stream opens after 1 NV read, not the engine's 15. Parts of paths deferred to prose (the upper-layer reset on RemoveRobot, CD16's Lab read, NeedsManager::Update's tick position, the GoToSleep action). |
| M2 protocol | 14 / 15 | M2-003 P | The inverse lift constant is 0x3F364D90, not the engine's 0x3F364D93, and NaN is handled differently. Off the live path. All 112 message layouts hold. |
| M3 device | 12 / 21 | M3-022, 025, 026, 027, 031 F; M3-023, 029, 030, 035 P | NV `Read()` sends at once; the engine only queues, and sends from Update after Gate A (0x00644E82, 0x006456BC). GetBaseEntryTag's factory branch is inverted (0x006442B2), and positive tags don't take the largest key <= tag. The deadline clock is not the synced one. The timeout broadcasts, and the final-chunk result is forced to 0. |
| M4 control | 9 / 17 | M4-001, 003, 011, 016 F; M4-008, 010, 020, 025 P | Head and lift actions don't take the track lock, so no DisableAnimTracks; the timeout is 5 s, not 30 s (0x0052B0C2). The angle isn't rescaled first (0x0084C87C). Decimal-rounded head limits (0xBEDF66E8 against 0xBEDF66F3). The BlockFilter init isn't gated on the physical robot. IsNear is `<`, not `<=`. |
| M5 animation | 22 / 26 | M5-027 F; M5-031, 032, 035 P | On the tick a clip ends, the engine skips the idle-stack test and streams layers (0x0057D25C..0x0057D260). The row-shift fraction is a reciprocal-then-multiply (0x00585E28). Live-idle claims rest on the invented StreamLive seam. |
| M6 audio | 2 / 2 | none | ADPCM and STMG hold, against independent parses of the shipped assets. |
| M7 behaviour | 3 / 14 | 11 (see the M7/M8 report) | IdleBehavior is a non-production copy. ReactToImpact is always runnable. Eye shift, dart, idle gates. |
| M8 framework | 0 / 8 | 8 (see the M7/M8 report) | Mislabelled vtable slots (vptr+0x48 is InitInternal). The +0x104 clear. PlayAnim, locked tracks, tick order. |
| M9 singing | 5 / 19 | M9-002, 006, 007, 010, 017, 020, 022 F; M9-003, 004, 005, 008, 009, 015, 026 P | Property 15 misread as stop-playback. The note-off envelope runs from the note-on. The note-off makes a fresh random draw; the engine replays the recorded Sound. The shake HPF's first sample. The fast log is replaced by Log10. Draws happen at prewarm, not when each note fires. |
| M10 derived state | 7 / 7 | none | |
| M11 vision | 21 / 28 | M11-002, 006, 008, 015, 022, 036, 039 | The nearest-neighbour search is seeded with the threshold (0x008C098A), so a lone match is never tested for ambiguity. The duplicate-marker merge has no source. Rounded 5°/20°/45°/2° constants. A +1 in the bounding rect. The per-frame gates (0x00654E56, 0x00654E78) are missing. The calibration install resets unconditionally. |
| M12 manipulation | 17 / 17 | none (a stale comment only) | |
| M13 navigation | 3 / 16 | M13-002, 003, 008, 011, 013, 017, 018 F; M13-004, 005, 009, 012, 015, 016 P | The planner reads BlockWorld, where the engine reads the memory map. Nearest-angle heading buckets, where the engine rounds uniformly. 5.0 is a height tolerance, not a lift speed. The charger turn is a path point-turn, where the engine sends setBodyAngle. The drive-off-charger flag is +0x34A, OnChargerPlatform (0x00511D6A). GetCompletePath isn't built. |
| M14 faces | 5 / 6 | M14-001 | The eye geometry is computed in double; the engine uses float, with 1e-5 as 0x3727C5AC. |
| M15 freeplay | 10 / 12 | M15-005 F; M15-003 P | "Two ticks" is a fixed 1/30 s; the engine uses the last tick's real duration (0x0084BCBC). |
| tools | 1 / 1 | none | |
| **total** | **155 / 239** | **84** | 155 of 376 records overall (41%) are exact today. |

(F = fails, P = partial. A partial means part of the claimed path doesn't hold, so the record can't stay settled.)

## Manager checks in the binary (all hold)

- M2: the literal at 0x00517104 is 0x3F364D93. The ImageRequest send result is discarded (0x0051530C).
- M3: `NVStorageComponent::Read` 0x00644E14 only validates and pushes onto the deque (0x00644E82), with no send. The
  GetBaseEntryTag compare is at 0x006442B2.
- M4: the head limits are `movw/movt` 0xBEDF66F3 (0x00547F44) and 0x3F46D3F2 (0x00547FC2). The C# constants are
  0xBEDF66E8 and 0x3F46D3FA.
- M5: `movs r0,#0; str r0,[r4,#0x38]; b 0x57d04e` (0x0057D25C..0x0057D260). `vdiv.f32 s16, 1.0, s0` (0x00585E28).
- M9: StopInternal `movs r2,#0` (0x005EF2C6). The HPF InitInternal copies the sample and zeroes the output
  (0x00636578..0x0063658C).
- M11: `mov sb,r3` (0x008C093E) and the seed store (0x008C098A); `cmp r1,r0; bgt` (0x008C0B72). The bounding rect is
  plain `max-min` (0x006ABBCA, 0x006ABBCE).
- M12: 75.0 (`movt 0x4296`, 0x004E5A36) and -49.0 (`movt 0xC244`, 0x004E5B0C).
- M13: +0x34A is written by `Robot::SetOnChargerPlatform` (0x00511D6A).
- M15: `GetTimeSinceLastTickInSeconds` returns +0x20 (0x0084BCBC), which UpdateTime computes each tick (`vdiv.f64`,
  0x0084BC7C).

## The patterns (what to guard against)

1. **Wrong structure between right constants.** Missing gates around a path (M4 track locks, M11 frame gates, M7 idle
   gates). Wrong call order (M8 tick order, M5 clip-end tick). Wrong slot identity (M8 vtables).
2. **A non-production copy standing in for the production path:** M7's IdleBehavior, M7-017's StreamLive seam, M5's
   records resting on it.
3. **Part of a path deferred** to prose, a code comment or a gap record, under a settled record (M1, M4-011, M15).
4. **Decimal-rounded float constants**, and double where the engine uses float (M2-003, M4-001, M5-032, M9-007,
   M11-006/008/015, M14-001). This one can be linted mechanically.
5. **Tests that encode the implementation**: circular, or asserting the contradicted behaviour. These appear in nearly
   every failing layer.

## What the numbers say about the builder

The same DeepSeek builder produced 17 of 17 (M12, after many rounds of corrected extraction) and 3 of 16 (M13, from
its own extraction). Quality tracks the input rows and the verifier more than the builder model.

## How to move forward (the manager's plan)

### A. Change how work is set up, before more building

1. **A strong-model pre-extraction for every build job** (Sonnet or Codex), spot-checked by the manager. The jobs that had
   one (R-ANIM, R-VIS, R-DEV) did markedly better.
2. **Every job names the production path:** the engine function, and the C# entry that must carry it. It forbids
   parallel classes. This would have prevented the IdleBehavior copy.
3. **A failure-pattern checklist** in the implementer's and the verifier's instructions: the five patterns above.
4. **Only a strong verifier settles a record.** A cheap-model job ends with its records built but still
   IMPLEMENTATION_GAP ("awaiting verification"). A Claude or Sonnet verifier checks the batch in the binary and
   settles it.
5. **A float-literal lint:** a test that flags long decimal float literals in `// fidelity:`-tagged code, with engine
   constants written as their bit patterns. It starts from a baseline of the existing ones.
6. **Tests take their expected values from the binary,** and each record has at least one test through the live entry
   point.

### B. Rebuild, in order of how much of the robot's behaviour each one touches

1. **The connection and device core:** readiness waits for the whole NV queue (M1-041), the NV queue sends from Update
   after sync (M3-022..031), and moves lock their tracks with the 30 s timeout (M4-001/003/016). Every robot session
   and every motion goes through these. This continues R-DEV (its M3/M1/M2 remainder) plus the demoted records.
2. **The behaviour framework and idle:** R-BEH2 (the 19 M7/M8 records).
3. **Vision and navigation:** R-VIS (M11..M14, including the 21 above).
4. **Singing:** R-M9, after B-M6b-4 lands the live audio.
5. **The small remainders:** M5-027/031/032/035 and M15-003/005, one small job.

### C. Calibrate the audit

An Opus recheck of M12 and M5 part A, the two Sonnet-audited layers that came out clean, to see whether the lighter
passes missed things.

### D. Then the robot

A control-check run after B.1. It changes connection readiness and track locking, which only hardware confirms.
