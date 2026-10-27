# Drive-off pilot — READY FOR CLAUDE

- Branch: `codex-m13-manager-2`, rebased on main `337879ab`; N3 withdraws N2, so no split proposal.
- Production commits: **e85f87ed**, **99de2d0a** (rebased pilot/corrections). Test-only FIX **5c25c9a7**: `R2FloorPlacementTests.cs`; no new production behavior or binary claim. Three positive units hold action time independently of transport pumping; original assertions and exact30s timeout test retained. New regression holds poll callbacks across>31s transport time, then requires actual status latch/clear, one verify and lock release; old-clock negative control fails Timeout.
- **Two consecutive full gates PASS on clean committed `b273fe62`:6362/6362 each,0failed/0skipped,6m20s then6m54s.** Logs `.scratch/pilot-fix-full-1.log` and `-2.log`; independent FIX check36/36 PASS. Earlier production checks138/138 and36/36 PASS. [FIX review](../../research/20261026-driveoff-pilot-fix-codex-check.md), [exact commands/failed attempts](DRIVE-OFF-PILOT-FIX-builder.md). Unit fixture inputs/substituted verify are not full caller or hardware evidence.
- Boundary: drive-off's observable result while it runs. Recorded neighboring MISSING paths do not hold this handoff. Official statuses/approvals are unchanged; Claude settles records. No new hardware or subsystem acceptance claim.

## Claims / binary addresses

| Changed behavior | Address / authority |
|---|---|
| DriveOff distance96+extra (shipped156/141), native platform/treads/carry eligibility, reaction/driving/severe-needs lifecycle, wait/redrive and exit stamp; success-only objective4 before Mood. |0059CABC;005C0980..005C0EE2|
| Contact-true ingress observes before contact-edge effects; current-origin identity/Known pose, cache/world/map/light/platform ordering preserved. |0051293C/00512AB4;00511990..00511DCA;00506B70..00506C48;00506F98;00639840|
| Straight uses100/-80,200/500, main yaw plus stored ForwardXY, separate f32 endpoint operations and fresh Clear/Line/Execute; raw carry controls stored-20/+0 offset. |005470F0;0054762A..005476AA;00515138;00517EC0;0085C714|
| Real StartActing callback/tag/QueueNOW and synchronous matching5A completion; straight union6, animation union1/String8 narrowed count and bytes. |005BDACC..005BDC12;005BF0BE/005BE1E6;0075C868/0075C940;006C22DC..006C2326|
| Animation retry retains the played clip but reports current selected name; destruction checks stream identity. |00544472;00543F3A;005441EC;00543DB2|
| Actual PutDown entry/time/activity13; each-tick chooser, losing typed Look candidate/history, shared RNG and exact f32/f64 scoring; default objective41 filtering. |005B3142..005B3422;005B387C;005EF804/005EFD54;0060A134/0060A482;005BF00C|
| Native Mood construction/update uses captured frame time; defined missing-resource JSON path retains curves/watchers; raw/typed61 precedes local delivery. |00510134/00510356;00513BE2/00513E84;0067CD54..0067CF00;0067B736..0067B796|
| SimpleMood thresholds/cached130 feed groups and empty-stack driving; actual Needs state supplies the needs branch. |0067C084;0067B67E;0058A954/0058A95C;004F0D2C..004F0D74;006960A8/0069CB4C|
| Actual Mood retirement preserves ActionList-null gates, reverse C3/children and owner-null-after-destruction. |00511150/005111B6/005111C4;0067AE18..0067AE74;004EF184..004EF1CE|
| Three native configuration containers preserve source order/IDs/equality/prune, root geometry/midpoint/ties, asymmetric DOWN carrying and defined rectangle fallback. Unknown work retains prior publication. |0061682C/00616B58;006177F0..00617C32;006192A8..00619448;0062C72E;004E64FE/004E65C6|
| OnConfigSeen uses native count growth/float timestamps; public cached-midpoint/FirstSeen/events remain isolated host extensions. |005DB112/005DB2D4..005DB396;operator D1|
| Successful lift attachment forces native configuration before observation confirmation, independently of host ForceUpdate. |006331AE..006331CC (write006331BC)|
| Canonical AI updates tracker after helpers; actual retirement forces before AbortAll, then destroys storage once; real sensor/activity pause inputs. |00569F44/00569F46;0051111A;005121F4/00511DB0/005A106C|
| Real vision FIFO aggregates handler failure, completes tails and gates later phases;43 carries ordered modes/raw sampled mean. |006542EC..00654ACE;006B2CDC/006B5136;00712FFA..0071301A|
| Pending camera words load→flag-clear→current-store before quality; quality keeps camera-before-debounce, u32 timing and post-broadcast reloads,57/2F before7C. |006B229C..006B230C;006B50C8..006B50D8;00655578..006556EA;006500B4..0065017C|
| Actual Sparked/Socialize objective recipients; undefined preselection Socialize state skips visibly under M1-034, defined objective4 does not mutate. |005B181E;005AFDF6/005B07C2;005AFF60/005B08AC|
| Explicit standalone clock/private whiteboard preserves controlled stamps/expiry/caps; borrowers preserve native owners. Owned callbacks/log/storage retire by identity; custom manual triggers still execute. |operator D2;native clock0059C314..0059C322;host implementation freedom under AGENTS|

## RECORDS TO SETTLE

Current statuses remain **IMPLEMENTATION_GAP**. REVIEW N3 on main337879ab independently passed the five rows below; Claude settles them when this FIX merges. M12-043 and M8-027 are not proposed. No official changes.

| Record / current title | Evidence for settlement |
|---|---|
| **M13-017** — BehaviorDriveOffCharger: runnable on the charger, the drive distance and the leaving-the-contacts update |005C0980/005C0B10/005C0B18/005C0BB8/005C0DA8..005C0EE2; actual configured action, contacts/platform, completion and objective/Mood tests.|
| **M13-040** — Contact-true charger observation: live ingress, located identity, Known pose and ordered world/contact effects |0051293C/00511990..00511C2D;004EA304;00506B70/00506F98;00639840; actual received-state/world/map/cache/config/light ordering.|
| **M7-023** — Mood destruction with already-null ActionList dependency |00511150/005111B6/005111C4;0067AE18..0067AE74;004EF184..004EF1CE; actual owner removal/last-retain/null-gate tests.|
| **M7-026** — MoodManager::GetSimpleMood and the mood time MoodManager+0x130 (inputs to the animation group draw) |0067C084/0067B67E;0058A95C;004F0D2C..004F0D74; actual group/driving consumers.|
| **M11-060** — VisionSystem::Update's application of the pending SetNextCameraParams to the current exposure and gain |006B229C..006B230C/006B50C8..006B50D8; actual worker applies before quality; later failure retains copy.|

## Leftovers — do not hold this handoff

- M12-043: midpoint recipient00617A8C..00617C32 and container/cache joins remain MISSING. M8-027: stored callable destructors require enumeration; no settlement claim.

- M12-049: common cache producer built; BuildPyramid callbacks01029068/010290A8 remain MISSING. M12-047 owns its top-placement behavior.
- M12-033: scalar center join built; general marker/rotation/frame API not wholly checked. M8-013: scoring built; executable/analyzer/external selection remains MISSING. M8-015: profile destructor recipient remains MISSING.
- M8-020/021: concrete trigger runner built; legacy replaced/aborted completion and Sleep factory/DAS questions remain separate action records.
- Other activities/selected Look, live ROI/GetROI and generic other-world-type paths remain recorded gaps; possible effects outside running drive-off go to their own behaviors, not a new drive-off reading job.
- Audio object1 controller/readiness/active scheduling/render remains MISSING; M6/M9 stays last. D1/D2 and new recipient owners need named compatibility/ownership records; proposals only.
- Claude robot script: actual configured admission, early platform exit, stored-forward line, start/loop/end feedback and objective-before-Mood. Runs do not block merging; historical run3 covers a7af18cd only.

[Current record/source ledger](../../research/20261026-driveoff-pilot-source-handoff.md) · [Independent check / exact tests](../../research/20261026-driveoff-pilot-build-codex-check.md) · [Builder file/run ledger](DRIVE-OFF-PILOT-builder.md). Code verification is not hardware verification or final acceptance.
