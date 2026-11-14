# MIN-AREA-RECT-FOOTPRINT-1 merged (manager, 2026-11-14)

Worker job commit 66cec600 (full gate 8398/8398, verifier PASS). Handoff:
`re-analysis/jobs/trial-m13/HANDOFF-MIN-AREA-RECT-FOOTPRINT-1.md`.

**Re-read:**

| Where | What | Matches |
| --- | --- | --- |
| libcozmoEngine 0x004E64FA..0x004E64FE | Block::GetBoundingQuadXY calls cv::minAreaRect (`blx 0x4A48B8`) on the rotated corners | yes: the stack's cube footprint already used it |
| libopencv_imgproc 0x0009CCC0..0x0009CCCA | `vcmpe.f32 s13,s22; vmrs; bhi 0x9CCE2`, else store the new minimum | yes: an unordered (NaN) compare takes `bhi` and skips the store, so an area is kept only when `area <= minarea` |

**Verdict: PASS.** The live on-top search now takes both objects' quads (+0x50) and sizes (+0x2C) from their own
classes, so a charger or markerless object no longer gets the PlanarStandIn; the stand-in is deleted. Cube results are
unchanged. The tests are hand-derived from an independent port of the cited formulas.

No record settled; no status changed. M13-007 now waits only on M13-007-CALLER-CENSUS-1 (job 25, taken).

**Queued (verifier):** an unreachable throw text at BlockConfigurations.cs:544 and a stale doc name near BlockWorld.cs
3035; the evidence port script `re-analysis/evidence/m13-minarearect/port.py` has the old NaN rule (recorded in M13-024).
