# M10–M15 and M7/M8 acceptance: what stands in the way (2026-10-20)

**Monitor update (2026-10-21, 3609db0c):** computed-history core producer/reader/state/reset review supports a bounded build. HELD `jobs/B-M11-18.md` contains concrete proposed M11-098 inventory/evidence, scope and regressions; operator approval then manager integration/freezing is the next checkpoint. Marker camera/key consumer and broad alias/lifetime claims remain outside it. Severe-need answer pending review. Three research jobs remain open: external-action admission, M7-024 boundary, actionable cached-pose lifetime. No record/status change or builder release in this monitor.

**Builder release (2026-10-21):** operator authorized queueing B-M13-9 after the checked-contract handoff. M13-040 now explicitly owns the unbuilt every-contact-true charger observation. Its checked A1-A11/P1-P4 contract (A9 corrected) is integrated into M13 inventory; B-M13-9 is open. Earlier approval-pending notes below are historical. No whole M13-017 settlement, mount build or hardware run is authorized by this job.

**Review update (Codex, operator-authorized, 2026-10-21):** contact charger contract
checked against native instructions in `research/20261021-m13-017-located-charger-check.md`.
One behavioral correction: A9 light trigger0x25 is selected by **new** state Known,
not old state. Current Lights already agrees. Every-true contact observation and
missing Sensors join confirmed. Next: integrate corrected contact evidence into
an explicit owning inventory record and obtain the applicable inventory approval.
No builder release, manifest settlement or hardware run; the report's exhaustive
mount caller census remains outside this bounded check. Ongoing monitoring now
includes active independent research reviews, not only queue upkeep.

**Queue handoff (2026-10-21):** the four initial research answers are published
(8626fcfd, c419483d, e0c71224, 9d1efe80), all pending independent review/adoption.
The charger report corrects the initial rising-edge shorthand: observation runs
on every contact-true input; only edge events are gated. Review that contract first.
M10 reports exact-constant and unbound cancel/detach findings; these are review
leads, not approved builder instructions. M11 reports the core history joins now
built, with timeout-input and frame-pose assembly residue. M8 retains typed
completion and lifecycle gaps. Three independent narrow reads replenish the live
queue: history timeout input, animation completion payload, frame-pose assembly.
Builder release still requires reviewed evidence and applicable inventory approval.
The operator requested ongoing push monitoring/queue upkeep; a 10-minute chat
monitor is active. No records or acceptances changed by this handoff.

**Manager review (2026-10-21): APPROVED, with three conditions.** Counts, review states, live/non-live gaps,
hardware-only and blocked lists, SetOnCharger 0x00511990 (+0x338 read at 0x00511A66/0x00511BA8), SetPoseOnCharger
0x00516870 and M11-016's 2026-10-14 decision were checked against the manifest and the `.so`.
1. **Research queue update (operator, 2026-10-21):** keep 3–4 independent reading jobs queued.
   M13-017 first, then M11 pose/history, M8 ActionList/completion, M10 classifier settling passes.
   These reports run sequentially without waiting for the previous report's adoption or build.
   Each implementation still requires reviewed evidence and the applicable inventory approval;
   this does not release held builder jobs. Live requests are in `jobs/QUEUE.md`.
2. **Hardware in small, early checks, not only at step 7.** Each finished group gets a short targeted robot check.
   First: after M13-017 is built, dock Cozmo, drive him off, and check the platform flag and cliff threshold 50 hold
   until the footprint leaves the charger (M4-019, M13-017).
3. **Hardware scripts use the plain M5 format** (part list, WATCH NOW/STOP WATCHING, 1–4 answers, OK/PROBLEM/COULD NOT
   CHECK lines, every expectation cited). Change a harness only when a run shows a real defect.

**First job: M13-017, the missing located charger.** The stack lacks the observation on
every contact-true SetOnCharger input. After contacts drop, the no-charger branch clears the
platform flag and sends cliff threshold 400 before the footprint leaves the platform.
This caused the false cliff stop in M5 run 3; the cancellation itself was faithful.
Start from `research/20261020-m5-drive-cliff-stop.md`, `inventory/M13-navigation.md`
and the current M13-017 record, not a new investigation of the distance accumulator.
Trace only the missing creation/update join, its world/pose prerequisites and output
order; then approve any new evidence before a separate builder implements it.
Native starting points: SetOnCharger `0x00511990..0x00511C2D`, SetPoseOnCharger
`0x00516870`, platform clearing `0x00513CD8..0x00513E2A`, threshold path M4-019.
The contact flag is +0x338; platform is +0x34A. M13-017's evidence still calls +0x34A
“on contacts”; reconcile that against its corrected unresolved text at approval.
Future regression: received RobotState contact-on/contact-off through the real world
owner, located charger present, platform retained until footprint exit, threshold
and event order. Do not settle all of M13-017 merely by fixing charger creation.

- **Target:** the eight subsystems reach ACCEPTED under PROJECT_STATE.md's plan.
  M1–M5 stay ACCEPTED; M5's bundle is `hardware/20261020-211745-M5-HW-87b83f`.
- **Counts refreshed:** 2026-10-21 after adding M13-040; calculated directly from
  `fidelity_manifest.json`, grouped by subsystem/status, including non-live records.
  All eight review states are currently INVENTORY_APPROVED.
- **Labels:** **[F]** current manifest/source-note fact; **[P]** proposed work;
  **[U]** not established by this planning pass. This is not a fresh fidelity audit.

## Records

| Subsystem | Total | EXACT_SOURCE | EQUIVALENT_IMPLEMENTATION | COMPATIBILITY_POLICY | IMPLEMENTATION_GAP | RECOVERABLE_GAP | HARDWARE_ONLY | BLOCKED_EXTERNAL |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| M10-derived | 17 | 3 | 0 | 0 | 14 | 0 | 0 | 0 |
| M11-vision | 97 | 14 | 0 | 1 | 75 | 6 | 1 | 0 |
| M12-manipulation | 49 | 8 | 0 | 1 | 37 | 2 | 1 | 0 |
| M13-navigation | 40 | 3 | 0 | 0 | 35 | 1 | 1 | 0 |
| M14-faces | 14 | 1 | 0 | 0 | 11 | 1 | 0 | 1 |
| M15-freeplay | 71 | 3 | 0 | 1 | 61 | 4 | 1 | 1 |
| M7-behaviour | 29 | 4 | 0 | 0 | 23 | 2 | 0 | 0 |
| M8-framework | 27 | 0 | 0 | 1 | 24 | 2 | 0 | 0 |
| **Total** | **344** | **36** | **0** | **4** | **280** | **18** | **4** | **2** |

[F] Of the gaps, **279 implementation and 15 recoverable are live_path=true**.
Non-live gaps are M13-036/037 (unsupported object tracking), M12-048 (unreachable
crash candidates), M7-022 (developer test). They need per-record disposition, not
implementation merely to clear the count. SDK recipients M7-024, M8-018, M11-056
and M15-032 still have live gap classifications: confirm their boundaries and record
the existing SDK exclusion individually. Do not exclude the supported high-level
activity input in M15-031 just because its old title says SDK.

## Blockers by subsystem

Every IMPLEMENTATION_GAP needs a settling pass against current code and its entire
claimed production path. “Built, awaiting strong verification” is not a count of
completed implementations: many such records also name unbuilt joins. The groups
below prioritize those joins; exact remaining build counts are **[U]** until each
bounded pass reconciles older unresolved paragraphs with subsequent checked work.

| Subsystem | Known unbuilt/partial live paths and recovery work | Settling focus |
| --- | --- | --- |
| M13 | **017 first**; planner obstacle/geometry/previous-plan paths (003, 011, 018, 033–034); charger mount/drive-off and action joins (008–009, 012–017); footprints/frame conversion (023–025); map producers/origins (030–032); public-state producers feeding audio clients (038–039). | All 34 implementation gaps, including the one non-live row. Reconcile B-M13 builds with current M11 pose/map and M12 action dependencies; audio client decisions are in scope, Wwise output remains M6/M9. |
| M11 | Localization/confirmation and history-to-world joins (013, 036–044, 050–053, 097); map/quad-tree production (045–048, 074, 079, 088–094); frame/result/API delivery including ImageChunk (096). Recoverable residue: 016, 048, 056, 073, 084, 096. | All 75 implementation gaps in small groups: pose/history; camera/marker pipeline; world/localization; map/borders; ownership/API output. Reuse B-M3M4-33/34 and `research/20261019-b-m3m4-33-check.md`; do not rebuild accepted lower-layer history consumers. |
| M12 | DriveToPose/PathComponent lifecycle and real goal production (022–024, 032); docking/carry/placement and callback wiring (017–020, 025–045); pyramid cache/callback recovery (049). 048 is non-live. | All 37 implementation gaps, grouped into path execution, docking/carry, placement/pyramid. Successful DriveToPose coverage was transferred here from M5; a cancelled or failed drive does not discharge it. |
| M10 | Reaction strategy construction/parameters and consumers (003–004, 016); unexpected-movement rewind/obstacle production (007); restore/resume join (008); live admission/history for drift detector (015). | All 14 implementation gaps. Start with classifier/IMU rows already built (001–002, 009–011, 013–015, 017), then strategy/response joins. M11 history now exists; old “no history/ActionList” notes need checking, not repetition. |
| M8 | IBehavior lifecycle/switching and scope release (001, 009, 011–013, 019); ActionList/compound completion payloads and teardown (015–017, 020–024); sleep trigger factory has no production writer (021; M5 handoff). Recover callback requeue census 024; disposition SDK 018. | All 24 implementation gaps. 027 is explicitly “settled in substance” and needs code confirmation/removal of the obsolete finalization MISSING. Reuse M5 action checks; check the timer-before-Robot-destruction lead on 015. |
| M7 | Factory/config/strategy binding and concrete reactions (002–003, 014–015, 018–021); real mood production, simple mood and clock (012–013, 020, 026); laser behavior (025, 029). | All 23 implementation gaps. Idle/face rows 005, 007–010, 016–017 overlap now-accepted M5: reconcile its evidence and supported app input before treating old “no live pusher” notes as current defects. Developer constructor/subscriber effects 027–028 need narrow scope checks. |
| M14 | Detector-feature/recognizer scheduling (010–011), full face-album payload/persistence (012), lifetime joins (013), RobotObservedPet production and delivery (014). TTS 006 is externally blocked. | All 11 implementation gaps: geometry/tracking, output/enrollment, then lifetime. Synthetic seam results cannot prove live detector/recognizer coverage. |
| M15 | Needs/persistence and NV consumers (001, 004, 014–016, 027, 029–030, 033–050); activity selection, Sparked/Feeding/GuardDog/Pyramid owners and callbacks (002–013, 019–026, 031, 037, 051–067); synchronous completion census 069 and reward payload 071. | All 61 implementation gaps, split by needs/persistence, activity selection, and one concrete activity at a time. Reuse B-M15-11's checked fixes rather than B-M15-7's superseded FAIL. 070 is log-format-only: narrow impact check/cleanup, not an acceptance blocker by itself. |

**[F] Scope/external obstacles needing explicit disposition:** M11-016's manifest
records the operator's 2026-10-14 decision to port bundled OKAO/OMCV as a later layer;
it is RECOVERABLE_GAP, not unavailable third-party code. Preserve that decision and
make the deferred boundary explicit before claiming M11/M14 acceptance; do not
claim detector-dependent paths complete through seams. M14-006 remains live
BLOCKED_EXTERNAL (Acapela/Wave Portal voice output), and M15-047 remains live
BLOCKED_EXTERNAL (phone FPSCR mode). Neither is settled by an offline pass or a
successful robot run. Source resolution or a recorded operator decision is needed.
Keep M6/M9 last; do not silently drop required M13/M15 audio decisions/API events.

## Hardware-only records

[F] All four currently require hardware and lack a recorded settled verification.
No HARDWARE_ONLY rows exist in M10, M14, M7 or M8; that does not waive layer hardware.

| Record | What remains to establish |
| --- | --- |
| M12-041 | Operator robot capture of 0xC6 around cleared/replaced paths, IDs and timing; native consumer `0x0064B620..0x0064B6A0`, timeout `0x006494EC..0x00649580`. Also observe successful DriveToPose under the sourced M12/M13 rules. |
| M13-035 | Which ticks see planner worker states/transient zero (`0x005004DE`, `0x006495E6`). Stack timing observations alone cannot establish phone scheduling. Seek official-app evidence or a recorded disposition; no invented latency oracle. |
| M11-095 | Phone vision-thread owner-count race (`0x00652212/0x0065222E`, `0x008452BA..BE`). A robot run with safe C# counts cannot reproduce/prove it. First assess observable effect and M1-034 applicability; do not introduce the race. |
| M15-068 | Phone config enumeration/subscriber order (`0x00802D70`, `0x00802FF6`, `0x00663C74`). First read the remaining AlwaysHandle branches named in the record to establish observable impact; stack-only runs cannot prove phone ordering. |

## Proposed sequence — one bounded job at a time

1. **M13-017 charger join:** narrow extraction/approval, separate build, affected
   verification and regression. Resolve only the prerequisite world/pose gaps this
   path actually encounters. Then settle its remaining drive-off behavior separately.
2. **M11 foundations:** separate settling jobs for pose/history, image/marker input,
   world/localization, and map/borders. For each: reuse checked research, build only
   confirmed missing paths, verify, settle. Record SDK/deferred-detector boundaries.
3. **M8 action foundation:** ActionList/completion/compounds first, then bounded
   callback-teardown census. This supplies M12/M13 scheduling without waiting for all
   behavior selection to be finished.
4. **M13/M12 movement:** planner geometry/map input; planner worker/status handoff;
   path execution/arrival; docking/carry; placement/pyramid; charger mount/drive-off.
   Each semicolon is a separate candidate job, narrowed further by concrete findings.
5. **M10, M8, M7 behavior inputs:** derived state/drift and unexpected movement;
   IBehavior/scopes/manager switching; strategy factory and reactions; mood/events/
   clock; idle and remaining concrete behavior groups. Resolve cyclic joins together
   only where the actual production path requires it.
6. **M14 and M15:** face/pet output and album jobs with detector/TTS limits explicit;
   needs/persistence; chooser/activity ownership; one activity at a time (Sparked,
   Feeding, GuardDog, Pyramid), then event/subscriber ordering and rewards. Resolve
   FPSCR and deferred/external boundaries before asserting readiness for acceptance.
7. **Final settling and hardware:** reconcile every remaining record at one HEAD,
   including policies, diagnostic gaps and the four hardware-only dispositions.
   Only then write/review sourced hardware scripts; operator runs, manager judges
   bundles, corrections get affected-diff checks, then mark each subsystem ACCEPTED.
   Expectations without engine evidence remain observations. No acceptance by count.

For every job, grep `research/` and `inventory/` first. Locate native bodies through
`decomp/libcozmoEngine/index.tsv` (the charger entry is `0051/00511990.c`); decompilation
is a navigation aid, not instruction evidence. New/changed evidence returns to
inventory approval; no bulk status changes. Verify production wiring as well as
isolated arithmetic. Run fidelity checks and appropriate regressions; the full suite
runs once immediately before an implementation commit, per AGENTS.md.

**This job changes only this plan.** No build, hardware script/run, manifest edit,
acceptance change or commit. The build/settling split remains to be confirmed against
current code in the bounded jobs above.
