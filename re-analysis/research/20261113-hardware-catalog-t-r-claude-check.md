# HARDWARE-CATALOG-T-R-JUDGES-1 merged (manager, 2026-11-13)

Worker job commit 237f4379 (full gate 8382/8382, verifier PASS). Handoff:
`re-analysis/jobs/trial-m13/HANDOFF-HARDWARE-CATALOG-T-R-JUDGES-1.md`. Harness only; no hardware script or bundle committed.

**Re-read in the `.so`:**

| Address | What | Matches the judge |
| --- | --- | --- |
| 0x005C3950..0x005C395C | `ldr r0,[r4,#0x14C]; cbz` (tipped set empty skips); `movs r1,#0xD; movs r2,#1; blx 0x4B2C64` (BehaviorObjectiveAchieved 13) | yes: T passes only on objective 13 KnockedOverBlocks |
| 0x005C9EA2..0x005C9EA8 | `movs r1,#0x28; movs r2,#1; blx 0x4B2C64` (BehaviorObjectiveAchieved 40) | yes: R passes only on objective 40 StackedBlock |

**Verdict: PASS.** Each judge now rests on one cited engine signal (the 0x8B objective broadcast, M7-019), as U does.
The world-model stack, carrying, tipped count and attempts print as `observation:` lines and cannot pass or fail a run.
This meets the hardware rule "every expectation is sourced". T and R can be run again.

Records: location repoints (M13-014, M13-015, M12-012) and the M7-019 correction note are cited housekeeping. No
status changed; no record settled.
