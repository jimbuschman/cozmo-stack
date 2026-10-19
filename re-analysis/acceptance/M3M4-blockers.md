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

**Update (2026-10-19, hardware script):** `hardware/scripts/m3m4-hw-2.md` approved for the operator's run. The tool `m3m4-hw` now has 13 checks (the seven HARDWARE_ONLY records, pass B's coverage list, cube lights), with the four harness defects of the 2026-10-12 run fixed. Waiting on the operator.

**Update (2026-10-19, B-M3M4-36 check PASS):** `research/20261019-b-m3m4-36-check.md`. The 16 held records settled (M4-052 EQUIVALENT_IMPLEMENTATION). M3 and M4 hold no gap records; implementation complete. Left: the hardware script and run, then ACCEPTED.

**Update (2026-10-19, settle readiness):** `research/20261019-m3m4-settle.md`. Seven records settled EXACT_SOURCE (M3-012, M3-055, M3-060, M4-043, M4-044, M4-047, M4-050). The VisionSystem/BlockWorld exists only when a host builds one, so the cube, light, cliff and camera-limit paths of the other 16 skip with MISSINGs in a vision-less host. **Operator decision: always build it.** B-M3M4-36 (open), its check, settle the 16, then the hardware script (pass B's 14 coverage items).

**Update (2026-10-19, later): B-M3M4-34 fix review PASS** (inline, `research/20261019-b-m3m4-34-check.md`): production
camera frames reach vision again through the SetNextImage join; the freed-node callback is skipped; the world-event deadlock
is gone. **OV129 adopted** (manager-checked): the marker normal; built in B-M3M4-35, the last M3+M4 build. Then settling and
the hardware script.

**Update (2026-10-19): Opus checks of B-M3M4-31/32/33.** 31 PASS with queued items
(`research/20261019-b-m3m4-31-check.md`); relative cube lights stop at a visible MISSING on KnownMarker::ComputeNormal
0x0087E3FA, requested as OV129. 32 FAIL (`research/20261019-b-m3m4-32-check.md`): the stopped-entry loop calls callbacks native
reads from a freed node (M1-034; M4-051's evidence corrected); a new 10 s deadlock when a host world-event handler calls a
light API. 33 FAIL (`research/20261019-b-m3m4-33-check.md`): the history core is faithful, but production camera frames no
longer reach vision (no recorded decision) and two M11-050 tests were bent; the SetNextImage join was re-read by the manager
(M11-097). All fixes in B-M3M4-34. Then: OV129 build, settling, the hardware script.

**Update (2026-10-18, latest): B-M3M4-30 Opus check FAIL** (`research/20261018-b-m3m4-30-check.md`): after a light
callback Update keeps the old layer; host light entries can interleave with Update; moved cubes never reach the
pose-state light reaction. Fixes in B-M3M4-32, after B-M3M4-31 (claimed). Consequence noted: with the cliff history gated
on M11-097, production never runs the cliff running-stats tail, so its threshold sends cannot happen until M11-097 is built. **Decided (operator, 2026-10-18): build the robot-state
history now** (M11-051/052/053/097 stay M11's records; B-M3M4-33, after B-M3M4-32).

**Update (2026-10-18, later): OV128 adopted** with corrections (`research/overnight/20261018-ov128-check.md`; the answer's
cube-light rotation tables were reversed). M4-052 and M11-097 are now IMPLEMENTATION_GAP; M4-051's guard is read; M3-037
re-based on the M1-034 standing rule. Builder: B-M3M4-30 (claimed), then B-M3M4-31 (M4-052, M4-051 guard, M3-037). The
RobotStateHistory rebuild (M11-051/052/053/097) waits for M11's turn; M4-019/M4-038 keep their handoffs to it.

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
