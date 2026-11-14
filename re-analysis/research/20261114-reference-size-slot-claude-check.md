# REFERENCE-SIZE-SLOT-1 merged (manager, 2026-11-14)

Worker job commit ed9af638 (full gate 8391/8391, verifier PASS). Handoff:
`re-analysis/jobs/trial-m13/HANDOFF-REFERENCE-SIZE-SLOT-1.md`.

**The change:** NativeConfigurationGeometry.Neighbor reads the reference object's size through its own slot +0x2C
(GetDimInParentFrame<Z> at 0x0062606E..0x00626070, re-read 2026-11-13 for M11-105) and its footprint through slot
+0x50 at the re-rooted refPose with padding 0 (0x0062604C..0x0062605C; the +0x50 load re-read 2026-11-13 at
0x0062604E). Before, both went through the cube-only path and threw for a non-cube reference. For a cube the result
is identical.

**Verdict: PASS.** No live caller passes a non-cube reference today (CanInteract admits families 1/2; the other callers
filter to {2,1}), so robot behaviour is unchanged; the per-class path now matches the engine. Tests are hand-derived
(charger reference targetZ 15.5, ProxObstacle reference targetZ 50) with cube regressions.

**Records:** M11-003 evidence: the movt #0x4230 is at 0x004E4D90 (re-read: 0x004E4D8E is `movs r1,#0`); corrected under
the standing authorisation, M11-vision re-approved. The stale "still reads CubeGeometry.SizeOf" sentences in M13-007 and
M11-105 now say the reference side is built. M13-025's location was repointed by the worker. No status changed; no
record settled.

**Queued (verifier):** the axis choice uses the double RotatedParentAxis helper where the engine reads f32; a difference
is possible only at an exact tie and predates this job.
