# M3 + M4 acceptance: what stands in the way (manager, 2026-10-18)

- **Target:** M3-device and M4-control reach ACCEPTED, under "Done for a layer" in `PROJECT_STATE.md`, judged by
  AGENTS.md "What we reproduce" (2026-10-17).
- **Examined:** `main` at 797f229e. Sources: the manifest, `jobs/QUEUE.md`, `jobs/STATUS.md`, the check files named below.
- **Labels:** **[F]** checked against the current files; **[E]** estimate; **[U]** unknown.

## Records

[F] 113 records (M3 63, M4 50). 57 EXACT_SOURCE; 27 COMPATIBILITY_POLICY; 1 EQUIVALENT_IMPLEMENTATION (M4-032);
7 HARDWARE_ONLY, all HARDWARE_VERIFIED by `hardware/20261012-062328-M3M4-HW`; **21 IMPLEMENTATION_GAP**, no
RECOVERABLE_GAP.

[F] Every one of the 21 is marked "built, awaiting strong verification". Every build chain that touched them ends in an
Opus PASS (B-M3M4-19/23/24/26/28/29, B-M3M4-27 by Codex); the FAILs before them were fixed in the next job. No M3/M4
builder job is open. What has never run is a settling pass over the records themselves at one HEAD.

| Group | Records | Known remaining pieces (from `unresolved`; to be confirmed by the pass) |
| --- | --- | --- |
| Cubes, block pool | M4-009, M4-010, M4-011, M4-043, M4-050 | M13-008 charger writer (handoff); blockPool.txt root is scope 4 (DataPlatformExternalPath) with no named host input [U] |
| Cube lights | M4-018, M4-037, M4-041, M4-044, M4-045, M4-046, M4-048, M4-049 | relative modifier (M15-037/M11-004 handoff); ActiveObject ctor LED contents (M4-041) [U]; null-object PlayLightAnim 0x00638310..0x0063880C (OV32) [U]; active-object vtable +0xC (M4-049) [U]; M5-051 reader-error text (log only) |
| Stream budget | M3-012 | none named |
| Camera parameters | M3-021 | initial exposure / IsInitialized (M11-059), pending apply (M11-060): handoffs |
| DAS | M3-055, M3-060 | Configure absolute roots and the platform selector: app inputs with no policy record [U] |
| Cliff | M4-019, M4-038 | drone branch (M4-039 policy); ComputeStateAt (M11-051/053); map insert (M11-048/M13-030); Cancel/behaviour end (M8-015/M8-019); gyro detectors (M10-015) |
| Teardown, tracks | M4-027, M4-047 | ~Robot shared+14 release pointee 0x00511206 [U]; PrintLockState MISSING while M4-031 claims the body |

## Sequence

**Update (2026-10-18, night): settling passes adopted.** Both FAIL (`research/20261018-m3m4-settling-a.md`, `-b.md`).
- Settle once tags/cleanup land: M3-012, M3-060, M4-009, M4-010, M4-037, M4-043, M4-044, M4-046, M4-047, M4-050.
- Build (B-M3M4-30, open): M4-041 (contradicted: the ctor zeroes LED state), M4-049 (callback never called), M4-051 (new:
  completion callback), M4-019 (cliff history admits states robot+0x390 never holds), M3-021 (host-thread camera race),
  M4-027/M4-023 (tap filter with no robot), M3-055 (exclusion recorded), M4-048 (production test), tags, reach labels.
- Research (OV128): M11-097 (new: the unowned history query results M4-019/M4-038 hand off to), M4-052 (new: relative
  cube lights), the M4-051 guard, M3-037 (still needs extraction).
- Records: M3-064 (new policy: blockPool.txt root), M4-038 split, M4-011 evidence corrected; M3, M4, M11 re-approved.
- Decisions (operator): drone mode, cube-light game inputs and pool editing not supported (M4-039/040/042); warm-up
  flag, test tones and the DriveWheels tolerance kept (M3-004/017, M4-006). Manager: M3-055 exclusion; M3-021 and M4-023
  rulings.
- Still to do after B-M3M4-30 and OV128: Opus check, the M3+M4 hardware script (pass B lists 14 coverage items), settle,
  ACCEPTED. M4-019 and M4-038 settle by the cross-layer rule once M11-097 owns the history results.


1. **Now:** two read-only Opus settling passes at 797f229e (A: cubes and lights; B: the rest, plus the policy
   confirmation and the hardware coverage list). Output: `research/20261018-m3m4-settling-a.md`, `-b.md`.
2. The manager adopts them: tag/location fixes, title/evidence splits for clean handoffs (cross-layer rule), any policy
   record for an app input, research requests for the narrow unread pieces.
3. One builder job for whatever is IN-LAYER, then its Opus check.
4. The manager writes the M3+M4 hardware script. The 2026-10-12 run predates most of the builds, so it is re-run on the
   final main; the new script covers what pass B lists.
5. Settle the records and set ACCEPTED.

[U] What could lengthen this: an in-layer piece the passes find unread; whether the handoff targets actually cover what
the M4 records leave out.
