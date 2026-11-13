# CHARGER-BOUNDING-QUAD-1 merged (manager, 2026-11-13)

Worker tip e51582f0 (job commits 483f548f + gate fix e9f9a7b0; full gate 8381/8381 on e9f9a7b0; verifier F1/F2 fixed).
Handoff: `re-analysis/jobs/trial-m13/HANDOFF-CHARGER-BOUNDING-QUAD-1.md`.

**Re-read in the `.so`:**

| Address | What | Matches the job |
| --- | --- | --- |
| 0x0062BA7A..0x0062BA86 | the neighbour lambda: `ldr r6,[r0,#0x50]` (the candidate's own GetBoundingQuadXY), `movs r3,#0` (padding 0), `blx r6` | yes: per-class quad, padding 0, no class check |
| 0x0062BA8C | then the quad-intersection call | yes: size read only after the quads intersect |
| 0x0062C1EE..0x0062C1F4 | the obstacle predicate: `ldr r1,[r0,#0x2C]; blx r1` (the object's own GetSize) | yes: per-class size |
| 0x0062C1FC | then the rotation of the size vector (`blx 0x4A4864`) | yes |
| 0x0087713A | ObservableObject::GetBoundingQuadXY (base; the charger's slot) | yes |
| 0x004EA672 | the charger's size getter: `this + vbase + 0xF0` | yes: a member read; the (96,80,31) value is the ctor's |

**Verdict: PASS.** The engine applies no class check at these sites, so a located charger takes part like any other
object. The robot runtime no longer throws when a charger is located; RollBlockOnSide is chosen again in the
whole-stack test. The SDK-custom-object stand-in stays a visible MISSING (unreachable: SDK mode not supported).

No record settled; no status changed.

## Follow-ups released

- **ROLL-AFTER-DRIVE-STANDALONE-1** (worker proposal): after its DriveTo succeeds, the roll never starts in the
  whole-stack test (the helper's completion doesn't reach BehaviorHelperComponent). Stack defect or rig artifact is
  open; a live-path question, so released.
- **GET-SIZE-SLOT-RECORD-1** (verifier Q1/Q2 and the M13-007 authority note): records only.
