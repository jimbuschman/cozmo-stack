# SCORING-FIX-1 — READY FOR CLAUDE

Rebased manager branch on origin/main 08e9dc34; read AUTONOMY and REVIEW including DIRECTION and the five ACTION rows. This is job 1 of the requested five-job batch. Hiking remains preserved WIP; no status or approval changes.

Changes in Activities.cs and FreeplayTests.cs:

- ScoringChooser evaluates bound native candidates in ascending BehaviorID byte order, preserving public Entries configuration order. Native unique map insertion 0x0060A2C4..2E0, ascending successor 0x0060A3EE..3F8/0x0060A5A2..5BE; positive nonrunning draw 0x0060A462/466/49E..4BC, exact ties retain the first candidate 0x0060A4EA..4F6. Native IDs 32/42/78 give Hiking_DriveOffCharger, KnockOverCubes, ReactToObstacle. Unknown host-only IDs keep their relative input order after native IDs.
- Typed Pounce score-state hook: when not running, f32(lastGroundMotion + notRunningBoredSeconds) < f32(now) selects boredomMultiplier; multiply inherited base score (0x005F83BE..8404). Equality and unordered comparisons keep factor one; running keeps factor one. The motion stamp belongs to the concrete owner, not the generic last-stop history.
- Typed LookInPlaceMemoryMap running hook subtracts lowPriorityScoreReduction when unsigned completedSectorCount >= prioritySectorCount (0x005E4F2C..50), without clamping. Both overrides precede the common acting bonus and penalty, as the virtual dispatch at 0x005BEF70..98/FC6 and running-base forwarding 0x005BEEDE..E4 establish.

Evidence: extractor and independent reviewer directly decoded the actual engine ELF, rather than treating check5 as proof. Scratch read/review notes are in .scratch/scoring-fix-1/. Source/static review PASS. New tests are explicitly unit reach: substituted behavior owners/state prove numeric ordering, draw ownership/count, ties, strict threshold/equality/NaN and override-before-bonus/penalty arithmetic. They do not prove native parent admission or full Pounce/Look lifecycle reach. Affected FreeplayTests and M8BatchThreeCTests: **93/93 PASS, zero skipped**, test/shutdown exits 0, log .scratch/scoring-fix-1/affected-1.log. Original postflight retained one transient dotnet process (8332, parent13396, command unavailable); separate follow-up count zero, nothing killed.

Records: M15-002, M8-003 and M8-013 remain IMPLEMENTATION_GAP; no whole-record settlement proposed. MISSING: concrete PounceOnMotion and LookInPlaceMemoryMap state/lifecycle owners are still absent, and their factory remains visibly unimplemented. No fabricated timestamp, sector count or admission was added. Other scoring overrides and broader chooser/framework joins remain open.

Builder question resolved: native emplace_unique differs from existing duplicate-name ToDictionary failure. A narrow scan found nine shipped scoring objects, 125 entries, no within-chooser duplicate names or native IDs; generated native-ID mappings match the ELF and supported bindings introduce no aliases. This difference is unreachable for those shipped lists; no new collision policy was invented.

Workers requested: scoring_read gpt-6.1-sol medium; scoring_build and scoring_review DEFAULT model/effort overrides omitted. Actual backends unobserved. Reviewer behavioral fix rounds 0, compiler corrections 0. All builder MISSING/questions are recorded above. No hardware, main push, official fidelity status or approval edits. Full gate deferred to the batch tip under AUTONOMY Speed changes; READY FOR CLAUDE awaits that pass.

## Completed batch gate (2026-10-31)

Full gate **7087/7087 PASS, 0 failed, 0 skipped** at `af83ed19e0856a8234a9742df04ce9be8a109e08`; gate/shutdown0. Logs `.scratch/start-path-fix-batch-gate-2{,-console}.log`. First gate at520ad93c was7084/7087 with three source-contradicted/stale test assertions; those test-only corrections passed76/76 and the full rerun. Production code did not change between those gates.

Effective rebased job commit `c946e1c7` (main8d425383; source-equivalent rebase); older hashes above preserve historical read/test context. Batch test correction `af83ed19`. Only metadata follows this passing tip. Fidelity statuses/evidence/titles/approvals are unchanged; M15-098/100 remain proposals for Claude, no whole M7-019/M15-006 settlement. Hiking code/test remain unchanged on remote parking branchbb5ea84a and are restored next.
