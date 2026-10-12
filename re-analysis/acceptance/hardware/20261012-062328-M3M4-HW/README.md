# M3M4-HW robot run, 2026-10-12 (manager's judgement)

- **Run:** the operator's robot (firmware 2457), stack at 10b9c6d8, script `scripts/m3m4-hw-draft.md` (approved
  2026-10-11).
- **What is in git:** every file except `frames.jsonl` (53 MB, sha256
  f5268ffd85413e38410c25180c4d635f923e2f3773a82ae1886bd2bddc09b544, kept by the operator).
  `image-encodings.jsonl` is stored gzipped (original sha256
  15e13431ae1ef6069b79804afa1914ad4d9176ead889e3ca6d17883158121d3e). `hashes.json` lists the originals.

| check | script verdict | manager reading |
|---|---|---|
| ORIGIN (M4-021) | INCONCLUSIVE (no states before the update) | The robot reports the update's origin 1 / frame 0 on all 133 states after it. Against 20260924-202748-CONTROL (no update, origin 0), it reports the update values. |
| NV_READS (M3-036) | OBSERVED_FACTORY_LENGTH1_REPLIES_NO_REREQUEST_CASE | Six Length = 1 factory reads, each answered once with the data. The size+16 half is still unanswered. |
| COLOR_IMAGES (M3-016) | OBSERVED_COLOUR_FRAMES | EnableColorImages alone gives half-width (160×240 JPEG, 3 components) colour frames, flagged by data byte 0x01 on encoding 8. |
| FACE_ROWS (M3-008) | OBSERVED_ROW_MAPPING_DIFFERS (script) | All 5 patterns **agree** with the engine model; the script's text matching was too strict (harness defect). Each frame is shown as it arrives. |
| MOTOR_CAL (M4-013) | OBSERVED_REPORTS_STARTED_AND_FINISHED | Honoured. The head was already at its lower stop, so there was no visible motion. |
| CUBE_TELEM (M4-024) | OBSERVED_ACCEL_ONLY_WITH_STREAM_ON | ObjectAccel only while StreamObjectAccel is on; the event messages come without it. |
| M3-050 (all phases) | — | Every real chunk is encoding 8. The 29 odd rows have random image ids: these are frames misparsed as ImageChunk (harness defect). |

**Harness defects, queued:**
1. FACE_ROWS answer grading is literal text.
2. image-encodings counts non-ImageChunk frames.
3. ORIGIN needs states before the update, but the engine sends the update before the first state.
4. The photo prompt accepted a directory path.

No verdict settles a record. HARDWARE_VERIFIED records what the robot did, not where the behaviour came from.
