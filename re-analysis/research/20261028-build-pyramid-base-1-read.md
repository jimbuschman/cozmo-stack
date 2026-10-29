# BUILD-PYRAMID-BASE-1 — bounded primary-source read

Requested reader startup: `gpt-6.1-sol`, effort `medium` (explicit request). No independent backend telemetry was provided or inferred. Read-only extractor; no implementation, tests, hardware, git mutation or official metadata edits.

Root: manager worktree `C:/Users/JimBu/source/repos/jimbuschman/cozmo-stack-codex2/.scratch/worktrees/codex-m13-manager-2`. PROJECT_STATE read first, then AGENTS, job/AUTONOMY/PLAN/BEHAVIOURS, extractor rules and research README. Engine SHA256 verified `02263C07F6BB60F4D7F351A3667DCA84FD3E6C0FC6F838E18B5DE7CF4E2989E1`. `known.py` ran before native lookups (class search first, then narrow callees/callbacks). Ghidra bodies are absent in this worktree; index used for navigation only. All claims below checked from raw bytes through Capstone; scratch dumps retained. Thumb-to-ARM veneers need decoding at veneer+4, not treating the entire veneer as Thumb.

## Entry answer and bounded scope

BuildPyramidBase is **both independently instantiated/configured and the C++ base used by BuildPyramid**. It is not an abstract/nonobservable row to drop.

- Factory switch: `0x0059C8A4: tbh [pc,r5,lsl#1]`; class case3 is table halfword `0x0059C8AE`, target `0x0059C9AC`; allocates0x168 (`C9AC`), calls its constructor at`C9BC` via`0x004AFA3C`, shared pointer at`C9C6`. Class case2 at`0x0059C98A` independently constructs derived BuildPyramid.
- Shipped `re-analysis/obb/assets/cozmo_resources/config/engine/behaviorSystem/behaviors/freeplay/buildPyramid/buildPyramidBase.json:2-5`: behaviorClass/behaviorID BuildPyramidBase, requiredUnlockId BuildPyramid, displayNameKey behavior.BuildPyramid. This proves config, not admission by itself.
- Derived ctor`0x005DBC8E` calls Base ctor via`0x004B47A0`; installs its nonempty Robot callback into Base+0x140 at`0x005DBCA4..005DBCB2`. Standalone Base ctor leaves callable+0x150=0 (`0x005DCA7A`).
- Actual activity owner: ctor`0x005A7416..005A745C` finds ID4 into+0xC0 and ID3 into+0xC8. Native name map confirms ID3=BuildPyramid (`0x0076B578..0076B5AA`, literal0x00C198C5) and ID4=BuildPyramidBase (`0x0076B596..0076B5C8`, literal0x00C19E56). The string is built before its enum byte is stored; do not pair each mov with the following string.
- `ChooseNextBehaviorBuilding 0x005A8AF2..005A8BB6` prefers+0xC8 (derived) when active or runnable, then+0xC0 (Base) when active or runnable; each new selection calls UpdatePyramidAssignments then SetPyramidBaseLights. This is a real native independent caller despite neither ID appearing in the JSON build chooser array. Whole ActivityBuildPyramid admission/phase/lights remains M15-051/054/037; this report reads only that necessary join.

Does-it-matter: Base changes target IDs, placement offsets, pickup/place helper order, cube-state lights, a -40mm drive and trigger20 observation action, and cache-dependent completion timing. Its ctor ownership/container details matter only through those retained values/callback recipients.

## State and input contract

Base fields: static ObjectID+0x120; base ObjectID+0x128; top ObjectID+0x130; previous base-cache count+0x134; base-appeared time+0x138; all-configs-lost time+0x13C; optional observation-finished callback+0x140 (callable pointer+0x150); state+0x158 (driving0, placing1, observing2); flag+0x15C (standalone constructor0; not reset by Base reset); Robot pointer+0x160. `0x005DCA50..005DCA96` establishes IDs=-1, count/state/flag/callback0, times=-1f and saved Robot. ObjectID wrappers' +0 words/vptr are not IDs.

Required native inputs, not invented host defaults:

1. GetBestObjectForIntention results9(base),10(static),11(top), including their invalid=-1 value. The Base owns their consumption and storage; generic cache production/filter maintenance is an input boundary, below.
2. Actual strong native pyramid-base cache at BlockConfigurationManager+0x48 and full-pyramid cache+0x64, including **availability separately from an empty vector**. Source compares real begin/end; substituting an absent producer as empty changes runnable and stop results. Current M12-049 retains the native producer gap.
3. CarryingComponent+8 actual carried ID; located static/base objects and their pose/dimensions; current origin and located-object query input; Robot pose.
4. BaseStationTimer current seconds as f32 at the exact samples below. It is not robot/image time, and the time of the optional observation callback is action-completion dispatch, not a fabricated delay.
5. Activity selection/unlock admission, inherited IBehavior helper/action host, lower motion/dock/action-result inputs. Missing production suppliers remain MISSING; this read does not certify all shared records.

## Own path inventory

`READ` means native behavior established here, not a manifest status mutation. `UNKNOWN / RECOVERABLE_GAP` marks an explicitly unrecovered producer or broader dependency. Existing records are quoted verbatim in the appendix before conclusions.

| Step | Native behavior and observable consequence | Primary citation | Ownership / read disposition |
|---|---|---|---|
| E1 | Config/factory independent Base and derived call; real activity selection as above. | factory0x0059C9AC..C9C6; ctor0x005DBC8E; activity0x005A7416..745C,0x005A8AF2..8BB6; shipped config:2-5 | NEW Base lifecycle; M15-051/054 necessary caller slice READ, broader activity open |
| R1 | Refreshes base9,static10,top11 in this order. Returns changed if any ID differs. Stores static+120,base+128,top+130 even when invalid. |0x005DCAFA..005DCB4E, calls0x005DCB06/10/1A; comparisonsCB1E..42 | NEW Base lifecycle READ |
| R2 | GetBest first EnsureInformationValid(intention), then finds entry and copies entry+0x24 ID to ObjectID output. This is not a nearest-object query at the consumer. |0x00571010..0057103A | Existing generic cache input boundary READ consumer, full cache producer UNKNOWN |
| R3 | IsRunnable calls Refresh; requires static!=-1,base!=-1,top==-1, then copies actual +48 strong cache and returns true iff empty. No AreAllBlockIDsUnique call here. |0x005DCAAC..005DCAEC | NEW Base lifecycle READ; cache content M12-049 input |
| I1 | Reset count0, appeared=-1, lost=-1. Does not clear target IDs, flag or optional callback. Init then if carried ID!=-1 enters Place, else Drive/Pickup; returns0. |0x005DCB58..005DCB7E; reset0x005DCB80..B90 | NEW Base lifecycle READ |
| D1 | Sets driving state0 and state label. Installs one state-light entry for base ID with trigger0x20, modifier bytes zero, retain flag false. |0x005DCBBC..005DCC12 (base ID atCBE2,trigger32 atCBEA,modifier memclrCBF8) | NEW producer READ; shared M7-030 setter already settled, no duplicate |
| D2 | Creates PickupBlockHelper using saved base ObjectID+124, animation trigger0x16, Radians0, both flag bytes0. Constructor's temporary0x23F is overwritten by0x16 before call. |0x005DCC16..005DCC46; helper factory callCC46 | NEW Base lifecycle READ; shared pickup helper remains its own ownership |
| D3 | Delegates helper with success callable vtable0x01029224, failure callable empty. Success loads captured Base then tail-calls Place; ignores helper result beyond success dispatch. |0x005DCC5A..005DCC78; data0x01029224+0x18=0x005DDB93;0x005DDB92..B94; veneer0x008CC4E0..E8 resolves0x004B4800 | NEW Base lifecycle READ; M8-011 shared delegate |
| P1 | Sets placing state1,label; clears state lights with empty vector,false; looks up **static** ID+11C in BlockWorld. |0x005DCDCC..005DCE08 | NEW state/transition READ; M12-028 producer |
| P2 | If static absent warns, sets static ID=-1, delegates nothing. Keeps prior other IDs, state1. |0x005DCE88..005DCECA; warning at0x005DCE9C,invalid writeCEC6 | M12-028 READ defined failure (not M1-034) |
| P3 | Static present: parameters A=B=0 then candidate updater; final low=1, chosen A/B, skip-check byte0. Creates PlaceRelObjectHelper for static ID. |0x005DCE0E..005DCE44; low inSB1,skip finalCE32=0 | M12-028 READ (base slice, no new producer record) |
| P4 | Delegates Place helper success callable vtable0x01029264, failure empty. Success loads Base then Observing. |0x005DCE58..005DCE76; data0x0102927C=0x005DDBF7;0x005DDBF6..BF8; veneer0x008CC4F0..F8 resolves0x004B4914 | NEW Base lifecycle READ; M12-042 helper and M8-011 own their full shared internals |
| O1 | Sets observing state2,label; creates sequential compound. Child1 DriveStraight(distance=-40f,speed40f,flag1), added false,false. |0x005DD4AC..005DD506; -40=0xC2200000,40=0x42200000 | NEW Base lifecycle READ; lower drive action shared |
| O2 | Child2 TriggerLiftSafeAnimationAction(trigger20,loops1,interrupt1,track-mask0,timeout60f,finalflag0), added false,false. Starts compound with captured Base+Robot completion callable. |0x005DD514..005DD56C;60=0x42700000 | NEW Base lifecycle READ; shared action lower path retained |
| O3 | Completion callable invokes Base+140 with captured Robot only if callable+150 nonnull; otherwise returns. It does not itself grant objective/reward or set flag/state. Standalone Base has no callback. |data0x010292BC=0x005DDC63;0x005DDC62..C74;ctor0x005DCA7A;StartActing0x005DD56C | NEW Base lifecycle READ |
| O4 | Derived Base+140 callback is a separate real recipient; derived ctor installs it. Derived init may enter Base Place/Pickup when both config caches empty. This report ends at that recipient; whole top-building/response is not claimed. |0x005DBCA4..BCB2;0x005DBF32..BF7E | M12-049 callback/top path boundary; NEW Base own complete recipient dispatch, downstream derived open |
| U1 | If prior count0 and current base cache nonempty, samples current seconds into appeared time. If prior count>0 and BOTH current base/full caches empty, samples lost time. |0x005DD026..005DD06A | NEW Base lifecycle READ; true native cache producer required |
| U2 | Samples current seconds again. While base cache nonempty and state<=1, stop iff `(appeared+2f)+epsilon < now` using ordered single-precision additions, strict comparison. Epsilon word0x3727C5AC (~1e-5f), literal0x005DD130. Observing state2 suppresses this stop. |0x005DD074..005DD0BA;literal005DD130 | NEW Base lifecycle READ |
| U3 | If base cache empty and full cache empty: lost must be strictly>epsilon and `(lost+2f)+epsilon < now` to stop. The lost timestamp is not cleared here. |0x005DD0BC..005DD0F8 | NEW Base lifecycle READ |
| U4 | Also stops if flag+15C nonzero AND full cache nonempty. Standalone flag initialized0, Base never writes1. |0x005DD100..005DD10C;ctor005DCA88 | NEW Base lifecycle READ; derived flag writer outside Base own path |
| U5 | A stop above calls StopWithoutImmediateRepetitionPenalty then returns2 immediately. Otherwise stores current base-cache count then tail-calls IBehavior::UpdateInternal: returns1 iff +84 action pointer nonnull, else2. |0x005DD10E..005DD12C;veneer0x008CC130..138 resolves0x004B2A90;body0x005BDA56..DA62 | NEW Base lifecycle READ; shared manager/helper update order still shared framework ownership |
| G1 | GetBase/Static/Top ID getters returnfalse without writing output when stored ID=-1; otherwise copy actual ID to output+4 and returntrue. |0x005DD5F4..005DD64C | NEW Base lifecycle READ; M15-051 consumers partial |
| G2 | AreAllBlockIDsUnique returns true when static orbase invalid; if both valid and topinvalid returns static!=base; otherwise all three pairwise different. Not called by Base runnable above. |0x005DDA60..005DDAB6 | NEW Base utility READ; no added gate |

## Base placement geometry (existing M12-028)

| Step | Native behavior | Primary citation | Disposition |
|---|---|---|---|
| B1 | Looks up static; null warns and leaves caller A/B untouched (caller initialized0). Otherwise clone static pose; get X/Y/Z dimensions in that pose rotation. |0x005DD150..005DD1B2;null0x005DD1FC..234 | READ |
| B2 | dx=dimX+12f,dy=dimY+12f. Ordered candidates (+dx,0),(0,+dy),(0,-dy),(-dx,0). A/B initially first candidate, so total rejection keeps first. |0x005DD1B6..005DD2C0 | READ |
| B3 | For EACH candidate calls CheckBaseBlockPoseIsFree **without supplying candidate coordinates**. It looks up static+base, returnsfalse with warning if either absent. So clearance is candidate-independent; do not add per-candidate occupancy. |call0x005DD2DC..DE;body0x005DD65C..6D0 | READ |
| B4 | Clearance creates allowed family set{2,1}, ignores static ID, default current-origin-only/none latest-only flags0, then obtains base dimensions. Pose tested: (baseDimX,baseDimY,0), zero Z rotation, parent=static GetZRotatedPointAboveObjectCenter(0). Extent vector=base dimensions (X,Y,Z). |0x005DD6D2..005DD87A;family words0x00C6CC40={2,1};IDignore0x005DD780..7A8;pose0x005DD80A..850 | READ; filter meaning M11-004 shared |
| B5 | FindLocatedObjectClosestToHelper applies supplied filter and predicate, returns object ornull. Predicate computes absolute vector, compares its Euclidean norm strictly below current extent-vector norm; if accepted replaces extentvector with absdelta. Base uses result==null as free. |0x0061F8EC..0x0061F9E0;predicate0x00627F9A..0062805E;Base0x005DD8E8..8EE | READ; this is norm threshold, not independent per-axis box bounds |
| B6 | If free, candidate pose uses candidate X/Y and **negative static dimZ**, zero rotation, parent=static GetZRotatedPointAboveObjectCenter(0). Computes squared distance to Robot pose; keeps first strict smaller distance against initial FLT_MAX(0x7F7FFFFF at0x005DD41C). |0x005DD2E6..005DD370 | READ |
| B7 | DistSQ result buffer is the same stack storage initialized with candidate.x before conversion. The call's bool is ignored; on failed conversion its output retention therefore matters. No success/zero substitution established by this caller. |candidate.x store0x005DD302;call0x005DD34C;immediatevldr0x005DD350 | Caller READ; shared ComputeDistanceSQBetween failure semantics remain dependency; do not invent a new fallback |

## Explicit dependency/gap boundaries

- **UNKNOWN / RECOVERABLE_GAP: native pyramid/full cache producer**, exactly current M12-049 (appendix). Base directly consumes real strong vectors+48/+64; this read did not recover Container::Update/CastNewEntry/PruneFullPyramids/equality or derived top callbacks. No all-pairs host snapshot, empty default, forced terminal state or synthetic winner is evidence. Base consumer accepts only actually available native input; absence remains a named gap. This does not require rereading the whole activity.
- **UNKNOWN / RECOVERABLE_GAP: complete intention9/10/11 production/filter choice.** This read established Refresh/GetBest and three own candidate predicate bodies `0x00570988..00570B40` in scratch. It did not read the complete lazy cache configuration/scoring production path. They depend on the native strong configuration caches and carrying/docking predicates; no closest-ID default is established. No claim that supplied IDs equal real production IDs. This is distinct from M12-049's strong configuration cache producer.
- M8-011 owns SmartDelegate internals and shared failure/update scheduling. This read confirms Base supplies empty failure callbacks; it does not introduce a failure transition. SmartDelegate copied both callables `0x005BEC04..005BEC2C`; Base caller does not branch on delegate return. Pickup/place/action results, cancellation and full public completion payloads remain shared ownership. Ground completion is **existing M12-052**, never a NEW record.
- M12-042 whole helper lifecycle, M12-028 other Stack/top producers, M12-049 derived callbacks/cache content, M15-051/054 whole activity and public manual/generic wrappers remain real partial scopes. This read makes no implementation or settlement claim for their unexamined portions.
- Outside effects running alongside Base (activity music/ROI/other activity strategy/reports) remain one leftover; no broad sibling audit performed. Legacy KnockOver original fault remains UNKNOWN and was not investigated.

## Existing-record conclusions (based on verbatim current snapshot below)

1. **M12-028, IMPLEMENTATION_GAP** already owns base placement parameters, offsets and clearance. This job can contribute its whole base slice; it cannot settle the whole record merely by adding a Base tag because its title also owns Stack/top producers and listed wider discrepancies. No duplicate producer record.
2. **M12-049, RECOVERABLE_GAP** owns derived BuildPyramid helper callbacks and native strong pyramid caches. Base's pickup/place/observation callbacks are different vtables0x01029224/264/2A4, not the record's0x01029068/0x010290A8 top callbacks. New Base own callbacks do not settle M12-049 or erase its missing producer. No contradiction/status weakening asserted.
3. **M15-051/054, IMPLEMENTATION_GAP** already own actual ActivityBuildPyramid lookup/choice slices. This read confirms lookup ID4 Base/ID3 derived and priority but does not settle whole titles.
4. **M7-030, EXACT_SOURCE** is shared state-light setter, not Base's trigger32 producer. Base adds a producer call; no duplicate setter claim or weakening.
5. **NEW candidate coherent own scope:** BehaviorBuildPyramidBase configured lifecycle: intention9/10/11 consumption, runnable/reset/carried branch, pickup→low placement→observation callbacks, state lights, ID getters, cache-timer stop and optional derived continuation. It has real observable effects and no existing record title above owns that whole class. This is an ownership proposal only, not a manifest edit/approval request or status claim.

No C# implementation assessed; no regression test run; no commit. Source rows and exact unknowns are the deliverable. No new departure identified in the read Base path. No original crash/UB/hang claim asserted.

## Exact current manifest snapshot

The following whole records are quoted as loaded from the current manifest. They are claims, not evidence. All title/status/evidence/unresolved text is preserved; this appendix prevents decisions against stale records.

```json
[
  {
    "id": "M12-028",
    "subsystem": "M12-manipulation",
    "title": "The behaviour-owned PlaceRelObjectParameters producers",
    "location": "cozmo-stack/src/Cozmo.Robot/Manipulation/DriveActions.cs",
    "effect": "a drive checks its pre-action pose with the wrong function, or a pyramid placement uses the wrong offsets",
    "provenance": "not read",
    "authority": "libcozmoEngine.so 3.4.0-1204: the setter 0x005586AC is called at exactly 0x005585F8, 0x0055889C, 0x0055C8A8, 0x0055E2CA, 0x0055E3E0, 0x0055EB74, 0x0055EC2A and 0x005B58C2 (re-analysis/research/20260929-R-VIS-verify-M12-gap2.md Q9); the default functions (0x005585F8, 0x0055889C), the DriveToHelper functor (0x005B58C2) and the PlaceRel installer (0x0055C8A8, never installed on the wire) are read in M12-029. Still unread: the flip-block installer bodies at 0x0055E2CA, 0x0055E3E0, 0x0055EB74, 0x0055EC2A, and the PlaceRelObjectParameters built by BehaviorStackBlocks::TransitionToStackingBlock 0x005C9588 (constant 0x00C69140 = {0,0,1}), BehaviorBuildPyramid::TransitionToPlacingTopBlock 0x005DC266 and BehaviorBuildPyramidBase::TransitionToPlacingBaseBlock 0x005DCE44; R-VIS gap 3 (re-analysis/research/20260929-R-VIS-M12-gap3-extraction.md, verified in re-analysis/research/20260929-R-VIS-verify-M12-gap3.md): the four flip-block installers (0x0055E2CA L1, 0x0055E3E0 L2, 0x0055EB74 L3, 0x0055EC2A L4) and DriveAndFlipBlockAction::GetPossiblePoses are read: see M12-035. Still unread: the PlaceRelObjectParameters producers (0x005C9588, 0x005DC266, 0x005DCE44)",
    "evidence": [
      "0x0055E2CA, 0x0055E3E0, 0x0055EB74, 0x0055EC2A, 0x005B58C2 the installers; 0x005C9588, 0x005DC266, 0x005DCE44 the parameter producers",
      "re-analysis/research/20260929-R-VIS-M12-M13-gap1-extraction.md Q1, Q2 and open questions 2 and 6",
      "re-analysis/research/overnight/20261014-ov02-m12-028-check.md: rows 1-5. Stack 0x005C9520..0x005C961A; top 0x005DC08C..0x005DC322 (+0x005DD134, 0x005DC78A, 0x005DC7F0..0x005DC9A4); base 0x005DCDA4..0x005DCF38; UpdateBlockPlacementOffsets 0x005DD140..0x005DD3B4; CheckBaseBlockPoseIsFree 0x005DD650..0x005DD8FC with 0x0061F8EC..0x0061F9E0 and 0x00627F92..0x00628064."
    ],
    "status": "IMPLEMENTATION_GAP",
    "unresolved": "The flip installers (L1..L4) are built (M12-035). Still unread: the PlaceRelObjectParameters producers (0x005C9588, 0x005DC266, 0x005DCE44), behaviour-owned. Because the legacy PlaceRelObjectAction(m, id, onTop) constructor's arguments have no recorded producer, its callers (CubeGameBehaviors.cs:597/645/1049, ManipulationBehaviors.cs:313) keep the pre-batch behaviour wherever the inventory is silent: Offsets sent unchanged as raw error-signal offsets, CheckPreActionPose true, DockingMethod Default, the pre-batch turn / marker-facing / placement-clear stand-in with no visible-marker wait. Caller-visible changes that remain, each from a source-backed record: onTop true and !CanStackOnTopOfObject -> 0x03000004 (SelectDockAction, M12-017); not carrying -> 0x03000011 instead of 0x03000000; a pre-action check left on now runs through GetPreActionPoses with the validity cone and obstacles (0x03000010 / 0x03000005 / 0x04000001 / 0x03000004 for a carried target); the dock error-signal marker for legacy callers is unchanged (MarkerFacing). Read 2026-10-13 (re-analysis/research/overnight/20261014-ov02-m12-028-check.md): IMPLEMENTATION_GAP, rows 1-5. Tuples: stack (low 0, A=B=0, skip check 1), pyramid top (low 0, midpoint, skip 0), pyramid base (low 1, chosen candidate, skip 0); they replace the stand-in \"Offsets sent unchanged as raw error-signal offsets\". The free check allows only LightCube/Block families in the current origin, ignoring the static block (M11-004 filter). The helper lifecycle is M12-042; the top midpoint geometry is M12-043 (RECOVERABLE_GAP). A second top delegation replaces the first (SmartDelegateToHelper); reachability unread.\n\nB-M12-3 (2026-10-15), built, awaiting strong verification: Stack supplies (low0,A0,B0,skip1), state label/carrying-ID guard/Debug pickup fallback and unconditional score call after delegation. Base supplies (low1,chosen A/B,skip0); four candidates are (+dx,0),(0,+dy),(0,-dy),(-dx,0), 12f gap, first-candidate fallback, candidate-independent allowed-family/current-origin query, strict distances and failed conversion retaining candidate.x. Native vector helper failure logs Error/errG and returns zero. Top applies state/null/membership gates then stops visibly at M12-043 unread corner order; no nearest-block/sideways guess remains. MISSING state-light/reaction-lock bodies and broader producer transitions keep their existing ownership. The legacy ExploreBringCubeToBeacon stand-in is M15-008, not falsely mapped to these three native producers. MISSING: the Stack +12C bottom-target writer before 0x005C956A is not supplied by these checked producer rows; the producer consumes the stored ID (invalid -1 when absent), rather than inserting a closest-target query or early Finish.",
    "hardware_required": false,
    "live_path": true,
    "test": "ManipulationTests; M12RVisBuildTests.M12_035_*; M12RVisBuildTests.M12_028_*; M12CheckedRowsBuilderTests; M12RVisBuildTests; M12RFix2StreamATests; M11RVisBuildTests; NavigationTests.BuildPyramidBasePicksUpOneCubeAndPlacesItBesideTheOther"
  },
  {
    "id": "M8-011",
    "subsystem": "M8-framework",
    "title": "The Smart* scope helpers: idle animation, motion profile, track locks, custom light patterns, helper delegation and the reaction locks",
    "location": "cozmo-stack/src/Cozmo.Robot/Behavior/IBehavior.cs",
    "status": "IMPLEMENTATION_GAP",
    "authority": "libcozmoEngine.so 3.4.0-1204",
    "evidence": [
      "SmartPushIdleAnimation 0x005be41c: if the idle-animation flag +0xb0 is already set it fails through sVerifyFailedReturnFalse (0x005be448 b.w 0x8cbe5c); otherwise AnimationStreamer::PushIdleAnimation(trigger, name) and +0xb0 = 1 (0x005be464..0x005be478).",
      "SmartRemoveIdleAnimation 0x005bd4c8: when +0xb0 is clear it raises VERIFY(%s): Behavior %s is trying to remove an idle, but none is currently set through sVerifyFailedReturnFalse (cbz r0,#0x5bd508 0x005bd4d4; string 0x005bd51e; b.w 0x8cbe5c 0x005bd526); otherwise AnimationStreamer::RemoveIdleAnimation(name) and +0xb0 = 0 (0x005bd4ec/0x005bd500).",
      "SmartSetMotionProfile 0x005be518 verifies +0xc0 == 0 (sVerifyFailedReturnFalse 0x005be52a), calls PathComponent::SetCustomMotionProfile (0x005be534) and sets +0xc0 = 1. SmartClearMotionProfile 0x005bd584 verifies +0xc0 != 0, calls PathComponent::ClearCustomMotionProfile (0x005bd59c) and clears +0xc0.",
      "SmartLockTracks 0x005be5bc emplaces (name -> mask) into the map at +0xb4; on a new key it calls MovementComponent::LockTracks(mask, name, reason) and returns 1; on an existing key it warns \"Attempted to lock tracks with key named %s but key already exists\" and returns 0 (0x005be600/0x005be624/0x005be636). SmartUnLockTracks 0x005be6e0 looks the name up, calls MovementComponent::UnlockTracks(mask, name), erases the entry and returns 1, or warns and returns 0 (0x005be6ee..0x005be716).",
      "SmartSetCustomLightPattern 0x005be7f0: if the ObjectID is already in the vector at +0xcc it logs on the \"Unnamed\" channel and returns 0; otherwise CubeLightComponent::PlayLightAnim(objectID, trigger, callback, true, lights, timeout) and appends the ObjectID to the vector at +0xd0, returning 1 (0x005be806..0x005be8cc).",
      "SmartRemoveCustomLightPattern 0x005be9b0 searches the vector at +0xcc for the element whose +4 equals objectID+4; not found logs \"No custom light pattern is set for object %d\" and returns 0 (0x005bea4e/0x005bea74); found calls CubeLightComponent::StopLightAnimAndResumePrevious(trigger, objectID) for each trigger and erases the element, returning 1 (0x005be9e8/0x005bea34).",
      "SmartDelegateToHelper 0x005beb10 logs on the \"Behaviors\" channel, warns and calls StopHelperWithoutCallback() when the current helper +0xc8 is already live (0x005beba4/0x005bebe8), then calls BehaviorHelperComponent::DelegateToHelper on [robot+0x264]+0x10 (0x005bec2c); on success it stores the helper as a weak ref at +0xc4/+0xc8 through __add_weak at 0x005bec6e, otherwise logs \"SmartDelegateToHelper.Failed\" (0x005bec8c/0x005becb2).",
      "SmartDisableReactionsWithLock 0x005bce3c appends \"_behaviorLock\" to the name, calls BehaviorManager::DisableReactionsWithLock(manager, name+\"_behaviorLock\", table, true) (0x005bce62) and inserts the original name into the per-behaviour set at +0xa4 (0x005bce7e). SmartRemoveDisableReactionsLock 0x005bd470 appends the same suffix, calls BehaviorManager::RemoveDisableReactionsLock (0x005bd48c) and erases the name from +0xa4 (0x005bd4a4). The manager side is M7-014.",
      "SmartDelegateToHelper 0x005beb10 calls BehaviorHelperComponent::DelegateToHelper, whose body is 0x0056dad8..0x0056db37 (PLT 0x004b29d0) at AIComponent+0x10 ([robot+0x264]+0x10, AIComponent constructed at 0x00569aae/0x00569ab2): it clears stack-maintenance vars, copies the two callbacks to +0x10/+0x28, and on an empty helper stack pushes the helper and stores Robot::GetWorldOriginID at +0x40, returning 1. R-BEH C2 read the component and its helper-stack runtime in full (report re-analysis/research/20260929-R-BEH-M8-framework-gap1-extraction.md); the component and the M7/M15 helper subclasses are still unbuilt, so the record stays IMPLEMENTATION_GAP."
    ],
    "effect": "a behaviour leaks or double-releases a track lock, idle animation, motion profile, light pattern, helper or reaction lock",
    "provenance": "the eleven Smart* functions, read at the addresses above",
    "unresolved": "Cross-layer split (manager, 2026-10-10; re-analysis/research/20261010-m3m4-cross-layer-splits.md, Opus extractor at f56329c): overlap ruling: the VisionComponent pre-gate (Gate B, 0x00513C6E..0x00513C9A in Robot::Update) is M3-032's, not this record's; the tag at CozmoEngine.cs:1147 is retagged M3-032 in the build round. Before: Opus verification of R-BEH2 batch 3 (2026-10-03, re-analysis/research/20261003-R-BEH2-verify-3.md): NOT YET. No concrete IHelper subclass exists; SmartDelegateToHelper (SteppedBehavior.cs:724) has no caller; the eight DockHelper/SearchForBlockHelper behaviours are not rewired; the manager ticks from FreeplayStack, not the engine tick; the VisionComponent pre-gate (0x00513C6E) is MISSING; LogStopEvent's layout is guessed. Before: built, awaiting strong verification: the BehaviorHelperComponent (all 12 members: AddHelperToComponent 0x0056da8a, DelegateToHelper 0x0056dad8, PushHelperOntoStackAndUpdate 0x0056dbdc, ClearStackMaintenanceVars 0x0056db4c, StopHelperWithoutCallback 0x0056dcc0 with the stale-bottom compare on an empty stack, ClearStackFromTopToIter 0x0056dce0, ClearStackLifetimeVars 0x0056dcdc, CheckInactiveStackHelpers 0x0056de36, UpdateActiveHelper 0x0056de74 branch by branch, Update), the abstract IHelper base, a minimal AIComponent host run from the engine tick after the first-full-state gate and before ActionRunnerUpdate (0x00513eac), IBehavior::SmartDelegateToHelper (0x005beb10), IBehavior::Stop's own helper stop (0x005bd0f4..0x005bd108) and StopActing's gated helper stop (0x005bd364..0x005bd36e). Still open: the six concrete helper classes' bodies and the callbacks each behaviour passes are unread (RECOVERABLE_GAP) and not built; the existing C# behaviours that use DockHelper.RunAsync/SearchForBlockHelper (PickUpCube, RollBlock, StackBlocks, PickUpAndPutDownCube, CubeLiftWorkout, BuildPyramidBase, RespondPossiblyRoll, DriveInDesperation) are not rewired and no C# class exists for BuildPyramid, RequestGameSimple or OnboardingShowCube; the VisionComponent::UpdateAllResults status and its pre-gate [[robot+0x258]+0x28] != 0 (0x00513c6e..0x00513c9a) have no source here (a seam that reports MISSING once); the BehaviorManager is ticked from FreeplayStack.Tick, not the engine tick, so 'helpers before the manager' is not realised, and four of AIComponent::Update's five steps (the analyzer, the whiteboard, SevereNeeds, FreeplayDataTracker 0x00569f46) stay elsewhere; the world-origin id is Engine.Robot.CurrentOriginId (the engine's value, whose only writer here is the constructor: the origin-change branch is unreachable until a runtime Delocalize exists); LogStopEvent's duration and argument layout are guessed; the ClearStackFromTopToIter error path (0x0056dd3c) omits the global byte store at 0x0056dd88 (unreachable, reported MISSING); the helper bodies run under the component lock; the manifest location still points at IBehavior.cs. OV74 check (re-analysis/research/overnight/20261016-ov69-74-75-77-check.md): LogStopEvent is no longer guessed: 0x005B6850..0x005B68EA (by +0x1C: 0 failure, 2 success, 1+active cancel, 1 otherwise inactive_stop) and the emitter 0x005B698C..0x005B6ACC (negative duration -> IHelper.Stop.InvalidTime error, _errG, 0). B-M12-6 built, awaiting strong verification of M12-047 overlap: LogStopEvent layout/duration/event level corrected from adopted OV74; component empty-stack stale-bottom assumption replaced with visible M1-034 skip at0x0056DCC6, and checked clear-loop error/global store built. SmartDelegateToHelper and weak-lock wrapper production bodies retained. Existing separate helper destructor/callback/concrete lifecycle gaps are not settled.",
    "hardware_required": false,
    "live_path": true,
    "test": "BehaviorFrameworkTests; HelperStopAndPyramidLoopTests; M8BatchThreeDTests"
  },
  {
    "id": "M12-042",
    "subsystem": "M12-manipulation",
    "title": "PlaceRelObjectHelper lifecycle: ctor, Init (pre-action check, DriveTo delegation), StartPlaceRelObject (3-attempt counter), RespondToPlaceRelResult, MarkFailedToStackOrPlace",
    "location": "cozmo-stack/src/Cozmo.Robot/Manipulation/DockActions.cs",
    "status": "IMPLEMENTATION_GAP",
    "authority": "libcozmoEngine.so 3.4.0-1204.",
    "evidence": [
      "re-analysis/research/overnight/20261014-ov02-m12-028-check.md: ctor 0x005B8F00..0x005B8F82; Init 0x005B8FD0..0x005B90FE; Start 0x005B9168..0x005B930E; Respond 0x005B94C0..0x005B952E; MarkFailed 0x005B93A8..0x005B9460; invokers 0x005B956A, 0x005B95D6."
    ],
    "effect": "How a behaviour's place-relative request is driven to, retried and marked failed.",
    "provenance": "Opus check of OV02 (2026-10-13), manager record.",
    "unresolved": "IMPLEMENTATION_GAP: port per the check file (F1-F8, the Respond table, MarkFailed with SetFailedToUse 1 or 2 and the 1-rad-about-Z pose). Dependencies: M12-031 (pre-action check), M12-022 (DriveToHelper), M8-011 (SmartDelegateToHelper), M15-024 (whiteboard). Unread: StopHelperWithoutCallback, IHelper::OnDelegateSuccess/Failure, DriveToHelper body.\n\nB-M12-3 (2026-10-15), built, awaiting strong verification: Bounded checked helper fields, Init precheck, Start counter/precheck/action tuple, Respond table and MarkFailed(1/2 with 1-rad-Z pose) built on the live AI helper component. Existing host action adapter posts the result on the behavior tick. MISSING: DriveToHelper construction/body at 0x005B9038/0x005B91E6 stops the delegate unit; no replacement drive/fake completion. Active-update virtual 0x005B93A4..0x005B93A8 lacks an adopted body row and only retains callback-written status with a visible MISSING. StartActingWithResponseAnim 0x005B927A wrapper remains MISSING, not certified by the async adapter. Existing M8-owned StopHelperWithoutCallback and IHelper delegate-success/failure implementations are reused without new recovery claims.",
    "hardware_required": false,
    "live_path": true,
    "test": "M12CheckedRowsBuilderTests; M12RVisBuildTests; M12RFix2StreamATests; M11RVisBuildTests; NavigationTests.BuildPyramidBasePicksUpOneCubeAndPlacesItBesideTheOther"
  },
  {
    "id": "M12-047",
    "subsystem": "M12-manipulation",
    "title": "Behavior helper stop semantics (StopHelperWithoutCallback, ClearStackFromTopToIter, SmartStopHelper) with IHelper::Stop downstream, and BehaviorBuildPyramid's top-loop source list",
    "location": "cozmo-stack/src/Cozmo.Robot/Manipulation/BlockConfigurations.cs",
    "status": "IMPLEMENTATION_GAP",
    "authority": "libcozmoEngine.so 3.4.0-1204.",
    "evidence": [
      "re-analysis/research/overnight/20261015-ov45-47-check.md: StopHelperWithoutCallback 0x0056DCBC..0x0056DCDA (compares the first stack entry only; ClearStackFromTopToIter then returns 1); ClearStackFromTopToIter 0x0056DCE0..0x0056DD92 (Stop(top, isActive on first pass), pop, release; 1000-bound sErrorF line 0x106, _errG, break); IBehavior SmartStopHelper 0x005BD21C..0x005BD2CE (weak +0xC8, helper +0xC4, log <name>.SmartStopHelper, robot+0x264 -> +0x10); SmartDelegateToHelper ignores the result 0x005BEBE8.",
      "re-analysis/research/overnight/20261016-ov69-74-75-77-check.md: wrapper 0x005BD21C..0x005BD2CE; component match 0x0056DCC0..0x0056DCD6; clear loop 0x0056DCE0..0x0056DD92; IHelper::Stop 0x005B6768..0x005B67F6; vt+0x24 bx lr 0x005B5F1C in 7 helper vtables; LogStopEvent 0x005B6850..0x005B68EA; emitter 0x005B698C..0x005B6ACC; BuildPyramid list 0x005DC08C..0x005DC15E, loop 0x005DC182..0x005DC304."
    ],
    "effect": "What stopping a behaviour helper does, and which pyramid bases BuildPyramid tries.",
    "provenance": "Opus check of OV45-OV47 (2026-10-15), manager record.",
    "unresolved": "RECOVERABLE_GAP: read IHelper::Stop downstream (concrete StopInternal, LogStopEvent, StopActing) and which list BehaviorBuildPyramid's top loop iterates. M1-034 candidate: empty stack at 0x0056DCC6 (reachability unread). B-M12-5 (2026-10-16): actual BuildPyramid top-placement entry reports stack MISSING for unread native list/stop downstream before iterating the host candidate list. No fabricated helper-stop owner, callback suppression or complete behavior claim. Read 2026-10-16 (re-analysis/research/overnight/20261016-ov69-74-75-77-check.md): IMPLEMENTATION_GAP for helper stop and the BuildPyramid list/loop. SmartDelegate rows are M8-011's, StopActing M8-001's. Callbacks and cache contents: M12-049. Empty-stack read 0x0056DCC6: reachability unknown, M1-034 skip if reached. B-M12-6 built, awaiting strong verification: existing IBehavior SmartStop wrapper is retained and regression-driven through weak lock, Info and component first-entry match. Empty stack now visibly skips the undefined0x0056DCC6 read under M1-034 rather than inventing stale-pointer success. ClearStackFromTopToIter re-reads the target value and end around Stop, pops the current end, releases it, preserves first-pass bool ordering, and ports exact LoopBoundOverflow Error text/global error/debug-break at1000. IHelper.Stop retains Info/event/active-cancel/vt24 order and existing no-op concrete tails; LogStopEvent now uses f32 subtraction then signed VCVT truncation, exact status mapping, Event level/name-only text/$data integer duration, negative-integer Error followed by error flag and duration0 event. BuildPyramid top entry looks up static+11C/top+124, warns using/clears moving-base+120, copies native manager+48 strong-cache references independently with duplicates/order, tests ContainsBlock(top) then ContainsBlock(static), calls midpoint/zero-height center/relative conversion, breaks on geometry failure, passes raw relativeXY/lowfalse/skipfalse into existing PlaceRelObjectHelper factory/SmartDelegate, and continues after delegation. Pose reads share FrameGate, released before callbacks, with temporaries retained through delegation and disposed afterward. Native cache producers and success/failure callbacks remain M12-049 ReportMissing joins; midpoint root/distance recipient remains M12-043 ReportMissing. Host all-pairs cache/midpoint is not substituted. Existing M8-001 code already clears matching tag after Cancel regardless of keepAction; no behavior change to that referenced record. HelperStopAndPyramidLoopTests plus corrected M8BatchThreeD empty-case test cover the checked branches and production entries. No whole helper/concrete-action/cache fidelity settlement. B-M12-6 ID-reference cross-check: ContainsBlock arguments keep native +124/+11C references across synchronous delegation (0x005DC188/0x005DC198); the loop rereads current ID fields each iteration and the factory rereads static+11C. Regression LoopRereadsTheReferencedIdsAfterSynchronousDelegation covers this without claiming unread callback bodies.",
    "hardware_required": false,
    "live_path": true,
    "test": "HelperStopAndPyramidLoopTests; M8BatchThreeDTests"
  },
  {
    "id": "M12-049",
    "subsystem": "M12-manipulation",
    "title": "BehaviorBuildPyramid helper callbacks and the pyramid cache contents (Container::Update, CastNewEntryToBestSharedPointer, PruneFullPyramids, PyramidBase::operator==)",
    "location": "cozmo-stack/src/Cozmo.Robot/Manipulation/BlockConfigurations.cs",
    "status": "RECOVERABLE_GAP",
    "authority": "libcozmoEngine.so 3.4.0-1204.",
    "evidence": [
      "re-analysis/research/overnight/20261016-ov69-74-75-77-check.md: callbacks vtables 0x01029068 / 0x010290A8 (built 0x005DC16C..0x005DC17C, passed at 0x005DC298); cache UpdateAllBlockConfigs 0x00616F00 -> Container::Update 0x0061682C -> AddAllConfigsWithObjectToCache 0x0062C56E -> CastNewEntryToBestSharedPointer 0x0062C68C; PruneFullPyramids 0x0062C72E."
    ],
    "effect": "What BuildPyramid does after each placement attempt, and whether the pyramid list can hold duplicates.",
    "provenance": "Opus check of OV69/74/75/77 (2026-10-16), manager record.",
    "unresolved": "RECOVERABLE_GAP: read the two callbacks' operator() slots and the cache bodies (check M11-037/M12-042 ownership first). B-M12-6 boundary: BlockConfigurationManager.CopyNativePyramidBaseCache copies an explicitly supplied native strong-cache vector (manager+48), with visible ReportMissing when its native producer is absent; no copy from host all-pairs snapshot. BuildPyramid checked loop installs explicit ReportMissing callback recipients at vtables0x01029068/0x010290A8; no success/failure state transition is guessed. This record remains RECOVERABLE_GAP and those bodies/cache semantics are unbuilt.",
    "hardware_required": false,
    "live_path": true,
    "test": ""
  },
  {
    "id": "M12-052",
    "subsystem": "M12-manipulation",
    "title": "PlaceObjectOnGroundAction::GetCompletionUnion: the saved carried object as the first ObjectInteraction ID, count 1, flag false",
    "location": "cozmo-stack/src/Cozmo.Robot/Manipulation/NativePlaceGroundRunner.cs",
    "status": "IMPLEMENTATION_GAP",
    "authority": "libcozmoEngine.so 3.4.0-1204",
    "evidence": [
      "GetCompletionUnion 0x00554AE0..0x00554AFA: first ID word = this+0x7C, halfword 0x0001 at +0x14 (count 1, flag false), Set_objectInteractionCompleted 0x00554AF4; the other four ID words are never written.",
      "Saved carried object: ctor 0x00554638..0x0055469C stores +0x7C (0x0055467C); Init 0x00554794. Setter 0x0075C3F0..0x0075C436 copies the 22-byte payload.",
      "re-analysis/research/20261028-sparks-roll-block-1-claude-check.md; SPARKS-ROLL-BLOCK-1 handoff proposal"
    ],
    "effect": "the ActionCompleted payload the C# API delivers when Cozmo puts a cube on the ground",
    "provenance": "SPARKS-ROLL-BLOCK-1 (Codex proposal, 2026-10-28), checked by Claude 2026-10-28; record added with operator approval 2026-10-28.",
    "unresolved": "Built (NativePlaceGroundRunner.GetObjectInteractionCompletion) but not settled. The four unwritten ID words are uninitialised stack in the original: under the M1-034 standing rule the stack withholds only the full public payload with a visible MISSING, and keeps the defined fields (saved ID, count 1, false) and the scalar result. The PlaceRel zero-padding policy (M12-050) does not apply. Not this record: Ground composition/Init/CheckIfDone (M15-022), the wire (M12-015).",
    "hardware_required": false,
    "live_path": true,
    "test": "SparksRollNativeTests"
  },
  {
    "id": "M7-030",
    "subsystem": "M7-behaviour",
    "title": "BehaviorManager::SetBehaviorStateLights: ordered diff against the previous lights, stop/replacement/new play on cube-light layer 1, retained vector and class, and the switch cleanup after Init",
    "location": "cozmo-stack/src/Cozmo.Robot/Behavior/BehaviorStateLights.cs",
    "status": "EXACT_SOURCE",
    "authority": "libcozmoEngine.so 3.4.0-1204",
    "evidence": [
      "SetBehaviorStateLights 0x005A4320..0x005A45F2; IBehavior wrapper 0x005BEE38..0x005BEE46; manager ctor 0x005A0918..0x005A092A.",
      "Lower calls: StopAndPlay 0x006396AC..0x00639722; PlayLightAnim overload 0x006389FC forwards layer 1 (the trailing integer 0 is not the layer).",
      "Switch cleanup MS4 0x005A2114..0x005A226E inside SetRunningAndResumeInfo after Init.",
      "re-analysis/research/20261028-hiking-roll-cube-1-state-lights-read.md, -state-lights-boundary.md, -lower-light-bindings.md with their independent checks; re-analysis/research/20261028-hiking-roll-cube-1-claude-check.md"
    ],
    "effect": "which cube lights a running behaviour shows, when they are replaced or stopped, and whether they are cleared when behaviours switch",
    "provenance": "HIKING-ROLL-CUBE-1 (Codex read and build, 2026-10-28), checked by Claude 2026-10-28; record added with operator approval 2026-10-28 (HANDOFF-HIKING-ROLL-CUBE-1.md).",
    "unresolved": "Settled EXACT_SOURCE by Claude 2026-10-28 (SPARKS-ROLL-BLOCK-1; re-analysis/research/20261028-sparks-roll-block-1-claude-check.md): setter 0x005A4320..0x005A45F2 stores the flag +0x85 first, matches each old entry to the first new entry with the same object ID (none: StopLightAnimAndResumePrevious 0x005A43C2; found: StopAndPlayLightAnim with the new entry's modifier 0x005A441A, ID inserted whatever the result), plays the remaining new entries (PlayLightAnim 0x005A4556), logs failed plays, assigns the vector when distinct and stores the class +0x84 last; StopAndPlay 0x006396AC..0x00639722 is StopLightAnim(old, layer 1, id, false) then PlayLightAnim; MS4 0x005A2114..0x005A226E clears (stop-and-resume each, vector emptied, class/flag kept) only when the incoming class differs from +0x84 and either the behaviour is neither the old current nor the old resume, or the old trigger is None, the new one is not, and the flag is false. Matches BehaviorStateLights.cs and Lights.cs. Built and checked in HIKING-ROLL-CUBE-1 but not settled. The ApplyAnimModifier body and the cube-light engine stay M4-046/M8-011/M8-012. Producers other than Roll (trigger 25 InteractingBehaviorLock) are not checked here.",
    "hardware_required": false,
    "live_path": true,
    "test": "BehaviorStateLightsNativeTests"
  },
  {
    "id": "M15-044",
    "subsystem": "M15-freeplay",
    "title": "UnlockId name-to-value table and InitConfig details: UnlockIdFromString map builder; the root state after a failed readAsJson",
    "location": "cozmo-stack/src/Cozmo.Robot/NativeUnlockNames.cs",
    "status": "EXACT_SOURCE",
    "authority": "libcozmoEngine.so 3.4.0-1204.",
    "evidence": [
      "re-analysis/research/20261013-backup-unlock-and-stoull-check.md: UnlockIdFromString static map builder E:0x00794D84..0x007957AC (undecoded); DataPlatform::readAsJson failure root state (PLT E:0x004A64E4) unread.",
      "re-analysis/research/20261013-small-reads-2-check.md: M15-044 section. UnlockIdFromString map 0x00794D84..0x00795834 (81 pairs, emulated; std::cerr on a miss); readAsJson 0x0084BFD4..0x0084C206; JsonCpp Reader 0x008E075A..0x008E12EC; InitConfig 0x0064BB68..0x0064BDB4; callers 0x0064B89E..0x0064B8C4, 0x0064C09C..0x0064C0C8."
    ],
    "effect": "Which UnlockIds the default unlocks map to, and the logs when the unlock config is missing.",
    "provenance": "Opus check of R-BACKUP-UNLOCK (2026-10-13), manager record.",
    "unresolved": "Settled 2026-10-27 (Claude check, research/20261027-pickup-pyramid-1-claude-check.md): NativeUnlockNames equals the emulated 81-pair builder; BackupFiles.ReadJson and InitUnlockConfig follow rows 1-6 of research/20261013-small-reads-2-check.md.\nEarlier notes: RECOVERABLE_GAP: decode the builder's name->value pairs (cross-check Unity UnlockId.cs, authority 2), and read readAsJson's failure path for the root it leaves. Read 2026-10-13 (re-analysis/research/20261013-small-reads-2-check.md): IMPLEMENTATION_GAP, build rows 1-6 of the check file: the 81-entry table; a miss writes \"error: string '<name>' is not a valid UnlockId value\" to stderr and returns 0; readAsJson parses even when the open fails and logs DataPlatform.readAsJson errors; the callers pass a fresh null root, so a missing file logs UnlockConfigJsonNotFound then MissingDefaults and leaves the set empty; a malformed file keeps the partial object. Crash paths (JsonCpp depth > 1000; a non-object non-null root at operator[]) fall under the M1-034 standing rule.\nB-M15-1 (2026-10-15), built, awaiting strong verification: B-M15-1 did not read or build this native table/failure-root record, as explicitly excluded by that job. Its temporary UnityUnlockId authority-2 fallback is visible as stack MISSING; native table and failed-read root implementation remain for B-M2M15-1.\nB-M15-1b (2026-10-15): Correction to earlier builder wording: only the temporary authority-2 Unity UnlockId fallback is in place. Native 0x00794D84..0x007957AC was not read or built by B-M15-1 or B-M15-1b. This job does not implement or claim the native name table; the M15-044 stack MISSING remains. No native record settlement is implied.\n\nB-M2M15-2 (2026-10-17), built, awaiting strong verification: re-applies parked ed67f3ad onto current main. NativeUnlockNames uses the checked 81 byte-identical name/value pairs (Unity enum cross-check, independent report-table regression), raw stderr miss/flush -> Invalid0. BackupFiles.ReadJson uses the existing supplied-root Reader, even after open failure; partial roots survive false. InitUnlockConfig implements checked early exits, read/error then defaults guard, null/missing/nonarray diagnostics, object-member insertion, per-element conversion/insert/log and M1-034 depth/nonobject skips. Both actual progression Init and backup fallback pass fresh null roots and ignore bool. Preserved B-M15-3 force-read and reuse of the SAME root for freeplayOverrides. Conditional formatted-reader Debug remains M5-051 at0x0084C12A..0x0084C15C; no framework exception text substituted. Reader trailing-comment metadata is unchanged and not claimed reproduced. No status/evidence change; complete subsystem not claimed.\nB-M2M15-2 publication update after manager7c5bfd3b (2026-10-17): builder rule10 now explicitly treats missing log text as best-effort cleanup, not MISSING. Removed the stack MISSING for the unread formatted-reader Debug argument; the existing read/config Error diagnostics remain visible. Formatter detail remains unclaimed, but is not an acceptance blocker. No status change.",
    "hardware_required": false,
    "live_path": true,
    "test": "; M15BackupBuilderTests; M2M15UnlockBuilderTests; M15BackupBuilderTests; M15RestorationCheckedRowsTests"
  },
  {
    "id": "M15-051",
    "subsystem": "M15-freeplay",
    "title": "ActivityBuildPyramid producers: UpdateChooserPhase and the Get*BlockID calls, the Update caller, BehaviorManager +0x58..+0x65 writers, the UpdateMusic target, the +0xC0/+0xC8 behaviour lookups",
    "location": "cozmo-stack/src/Cozmo.Robot/Lights.cs",
    "status": "IMPLEMENTATION_GAP",
    "authority": "libcozmoEngine.so 3.4.0-1204.",
    "evidence": [
      "re-analysis/research/overnight/20261015-ov31-32-33-check.md: UpdateChooserPhase 0x005A8854..0x005A90C8; Get*BlockID 0x005A878A, 0x005A87CC, 0x005A880E; UpdateMusic veneer 0x008CBD5C; FindBehaviorByID(4) 0x005A741C, FindBehaviorByID(3) 0x005A7444, unchecked uses 0x005A9424, 0x005A9458, 0x005A9462.",
      "re-analysis/research/overnight/20261015-ov59-61-check.md: UpdateChooserPhase 0x005A7A54..0x005A7AB6, 0x005A97EC..0x005A9826 (the evidence's 0x005A8854 is GetDesiredActiveBehaviorInternal); group 0x005A8294..0x005A8300 (table 0x00C617BA); notify 0x005A9828..0x005A98F8; assignments 0x005A872C..0x005A884E, getters 0x005DD5F4..0x005DD64C; Update 0x005A92C0..0x005A93D8; stage 0x005A93DC..0x005A94B6; UpdateMusic 0x005A94B8..0x005A94D8 (veneer 0x008CBD5C -> 0x004AF034); manager writers 0x005A08E8..0x005A0900, 0x005A3EB2..0x005A3EE6, 0x005A41A6..0x005A4226, 0x005A3C92..0x005A3C9A; setter callers ActivateSpark, SparkUnlocked 0x005A3DC6, RequestATrick 0x0056E766, CompleteSparkLogic 0x005B1BFC..0x005B1C0A; SwitchToRequestedSpark caller 0x005AE484; lookups 0x005A741C..0x005A7460, 0x0059C98E..0x0059C9C6."
    ],
    "effect": "Which cubes BuildPyramid assigns and when its Update runs, hence its cube lights.",
    "provenance": "Opus check of OV31-OV33 (2026-10-15), manager record.",
    "unresolved": "RECOVERABLE_GAP: read the listed producers; find the vtable caller of Update (only while selected?); find the writers of BehaviorManager +0x58/+0x5C/+0x60/+0x64/+0x65; say whether behaviours 4 and 3 always exist in the shipped config (if absent the unchecked dereferences are M1-034). Read 2026-10-15 (re-analysis/research/overnight/20261015-ov59-61-check.md): IMPLEMENTATION_GAP for (a) phase, notify, group, assignments/getters, Update and stage, UpdateMusic, BehaviorManager writers and setter callers, lookups. Split: behaviour choice M15-054; Update forwarding and spark-state M15-055. Alias writes to +0x58..+0x65 beyond the four direct writers remain UNKNOWN (accepted, not blocking). Only runs while selected (via Freeplay +0x80 or the sparked delegate +0xC4). B-M15-5 built, awaiting strong verification: three actual BuildPyramidBaseBehavior TryGet*BlockId getters follow005DD5F4..005DD64C: invalidFFFFFFFF leaves output unchanged, otherwise copy field then reread validity. Existing selected-ID producers remain candidates, not recovered by these getters. MISSING: no specialized ActivityBuildPyramid instance/tracker/chooser/group/phase/count/stage/timestamp recipient graph; generic ActivityTreeLoader/FreeplaySystem does not construct either required specialized freeplay/sparked instance. Thus phase/notify/group/role writes/strict-f32 Update/stage/UpdateMusic and +C0/+C8 joins remain unbuilt, not detached geometry/phase fixtures. Existing FreeplayInputs requested-spark string proxy is not native BehaviorManager58..65 owner state/four caller chain. Later alias census acceptance is retained and is not called a blocker. UpdateMusic is public-stage broadcast, not audio. Actual IManagedActivity.Update now exposes missing forwarding/ownership boundary on stack channel.",
    "hardware_required": false,
    "live_path": true,
    "test": "LabWritebackCheckedRowsTests.PyramidRoleGettersPreserveOutputOnInvalidAndCopyAllUintBits"
  },
  {
    "id": "M15-054",
    "subsystem": "M15-freeplay",
    "title": "ActivityBuildPyramid behaviour choice: GetDesiredActiveBehaviorInternal, UpdateProperties, ChooseSetup, ChooseBuilding, ThankUser, RespondPossiblyRoll",
    "location": "cozmo-stack/src/Cozmo.Robot/Lights.cs",
    "status": "IMPLEMENTATION_GAP",
    "authority": "libcozmoEngine.so 3.4.0-1204.",
    "evidence": [
      "re-analysis/research/overnight/20261015-ov59-61-check.md: GetDesiredActiveBehaviorInternal 0x005A8854..0x005A89BA (phase 0 chooses nothing 0x005A888C..0x005A8896 -> 0x005A890C); UpdateProperties 0x005A89FC..0x005A8A9C; ChooseSetup 0x005A8A9E..0x005A8AD0; ChooseBuilding 0x005A8AE2..0x005A8BFE; ThankUser 0x005A8C32..0x005A8D76 (+0x99 also on the skipped-current-roll path 0x005A8CB0 -> 0x005A8CFA); RespondPossiblyRoll 0x005A8D8C..0x005A900A (non-upright unrunnable roll falls into the upright branch 0x005A8E50..0x005A8F4C)."
    ],
    "effect": "Which pyramid behaviour runs.",
    "provenance": "Opus check of OV59-OV61 (2026-10-15), manager record.",
    "unresolved": "IMPLEMENTATION_GAP: build with the three corrections. Chooser vt+0x28 and IsRunnable stay with M8-013. M1-034 (not reached with shipped data): +0xC8 0x005A8AFA, +0xC0 0x005A8B5C, +0xB8 0x005A8A12/0x005A8C9E/0x005A8DA8, +0xB0 0x005A8CB4. B-M15-5: checked choice rows read with all manager corrections (phase0 no choice then thank/roll; nonupright nonrunnable roll falls into upright branch; skipped current-roll path also marks thanked99). Specialized ActivityBuildPyramid/tracker/roll/thank and shared-choice ownership are absent; generic chooser dispatch is not these source bodies. No dummy state/chooser/result or side-effect logs built. Existing live IManagedActivity.Update exposes M15-051/M15-054 ownership boundary; status/evidence unchanged.",
    "hardware_required": false,
    "live_path": true,
    "test": ""
  }
]
```
