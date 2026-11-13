> Worker note (2026-11-12, after this list was written): DRIVE-TO-OBJECT-MANUAL-SPEED-1 is done as "no effect" (be93eeb9): every supported source passes useManualSpeed 0, so M12-032 no longer waits on it. FIND-ON-TOP-SAME-ROOT-1 is in progress and folds in the call-site argument census suggested for M13-007.

# SETTLEMENT-READINESS-1 (Claude worker, 2026-11-12)

Doc only. Every record was re-read in the current re-analysis/fidelity_manifest.json (HEAD 5fe55b0a), with the
handoffs and 2026-11-1x Claude checks that name it. Addresses marked "re-read" were disassembled from
resources/lib/armeabi-v7a/libcozmoEngine.so (capstone) for this job. Everything else is quoted from the records,
handoffs and checks: leads, not proofs.

Key: **B** = behaviour gap on the record's own path (blocks). **H** = hand-off: a part owned by another, unsettled
record (the manager decides whether it blocks under "a settled record owns its whole production path"). **S** = stale
note in unresolved (cleanup, not blocking). **L** = log text only (never blocks). **IC** = inventory correction needed
(frozen field).

Open jobs: RETIRE = RETIRE-LEGACY-DRIVE-API-1 (operator 2026-11-12: retire); MANUAL = DRIVE-TO-OBJECT-MANUAL-SPEED-1;
SAMEROOT = FIND-ON-TOP-SAME-ROOT-1; MULTIPAIR = LOCALIZE-MULTI-PAIR-CROSS-ORIGIN-TEST-1.

---

## M4-009: EXACT_SOURCE (settled 2026-10-19)

Title: "HandleActiveObjectMoved/Stopped/UpAxisChanged: lookup, charger filter, double-tap window, IsMoving on change, the broadcasts".
Latest notes: CARRIED-ONTOP-BINDING-CENSUS-1 corrected the Moved/Stopped broadcast exclusion to the two-ID body
(0x0053417C / 0x0053498C -> IsCarryingObject 0x00633F88). Manager 2026-11-10: C11.1 "only writer DockWithObject" is
contradicted by the IDockAction destructor 0x00557484..0x0055748C.

| Item | Kind | Address | Closing job | Waits on |
| --- | --- | --- | --- | --- |
| Charger filter input: the charger ID at robot+0x334 has one engine writer, MountChargerAction::Init (0x0054E11C..0x0054E120). The stack leaves it unwired, with a visible MISSING (ChargerActions.cs:351; Cubes.cs:550). After a mount the engine drops the charger Moved/Stopped messages and the stack does not. The M4-009 unresolved says only "M13-008 writer remains unwired" | B (input owned by M13-008, IMPLEMENTATION_GAP) | 0x0054E11C..0x0054E120 | NEW **CHARGER-ID-WRITER-1**: wire the robot+0x334 store of MountChargerAction::Init into the charger filter, with a production test | none |
| Dock-target exclusion reset: built in the four native runners and at the end of legacy DockActionBase runs (M12-040) | H (M12-040) | 0x00557474..0x0055748C | none for M4-009 | RETIRE removes DockHelper; the native runners keep the reset |
| C11.1 says DockWithObject is the only writer of DockingComponent+0xC | IC | 0x00557484..0x0055748C | at next approval | none |
| ObjectID admission (slot > 4, -1) and SetID: M11-041 / M11-013 | H | 0x00533B56, 0x00623040, 0x004EF468 | n/a | none |

## M11-009: EXACT_SOURCE

Title: "A cube that reports movement is marked Dirty, unless the robot is carrying it".
Evidence: only "the message is {u32 timestamp, u32 activeId, f32 x, f32 y, f32 z, u8 axis}, read at 0x00533E54" plus two prose lines.
Unresolved (CARRIED-ONTOP-BINDING-CENSUS-1): the guard now reads the two-ID IsCarryingObject; the evidence is "too weak for EXACT_SOURCE as written".

Re-read for this job:
- Moved guard 0x00534116..0x0053413C: bl IsCarryingObject (0x0053411E); ldrb r1,[obj,#0x24]; cmp r1,#1 (PoseState Known); orrs r0,r1; bne skip; else MarkObjectDirty(obj, r2=0) (0x0053413C).
- Stopped guard 0x00534924..0x0053494A: the same shape (0x0053492C, 0x0053494A).
- MarkObjectDirty 0x005075A4: movs r2,#2; blx 0x4A7168, i.e. SetPoseStateHelper(obj, Dirty) (0x005075AC..0x005075B2); then cmp r4,#1; bne, so propagate == 0 ends there (0x005075B6..0x005075B8).
- SetPoseStateHelper 0x0050612C: for a state other than Known, SetLocalizedTo(nullptr) when [Robot+0x2B8] == id (0x0050613C..0x0050614A); store +0x24 (0x00506154); then, behind the gate at 0x0050615A..0x00506168, a tail call through the veneer 0x008CAD1C to PLT 0x004A7024 (the broadcast; M4-049 cites the tail 0x00506288..0x005062DA).

| Item | Kind | Address | Closing job | Waits on |
| --- | --- | --- | --- | --- |
| The guard is "Known AND NOT IsCarryingObject (carried [+8] or on top [+0x14])". The title says only "unless carrying"; the evidence cites neither guard | IC (title and evidence) | 0x0053411E..0x0053413C, 0x0053492C..0x0053494A, 0x00633F88 | settlement step | none |
| Dirty write: MarkObjectDirty(obj,false), then SetPoseStateHelper. That body is cited only by M11-007 (0x0050612C, IMPLEMENTATION_GAP); the MarkObjectDirty body (0x005075A4) is in no record evidence | H / IC | 0x005075A4..0x005075B8, 0x0050612C..0x00506176 | manager ruling: cite both ranges in M11-009 at correction, or settle M11-007 first | none |
| PoseStateChanged app delivery after the Dirty write: M11-038 records "MISSING complete native host/local delivery split" | H (M11-038) | see M11-038 | M11-038 jobs | none |
| Test reach: the dirty half of CarriedOnTopBindingTests is unit (PoseState substituted Known); the M4-009 B-M3M4-36 framed live test observes Dirty for a non-carried cube | not blocking | n/a | none | none |

No behaviour gap found on the M11-009 guard itself. Settling needs only the correction and the ownership ruling.

## M13-007: IMPLEMENTATION_GAP

Title: "FindObjectOnTopOrUnderneathHelper: its found-object test and the tolerance at each of its eleven callers".
Unresolved: footprint/intersection built for yaw-only cubes; "NOT BUILT: cv::minAreaRect (M13-023)"; PlanarStandIn
"differs from the engine" for non-cube or non-yaw-only pairs; OV100 "CONTRADICTED: refPose at 0x00626048 is
GetWithRespectToRoot".

| Item | Kind | Address | Closing job | Waits on |
| --- | --- | --- | --- | --- |
| No default current-origin filter, no same-root predicate; the reference pose must be GetWithRespectToRoot | B | 0x00626048, 0x006260D4, 0x0062BA2C..0x0062BA48, 0x004DF628 | SAMEROOT (released) | SAMEROOT |
| cv::minAreaRect footprint for non-cube or tilted pairs; PlanarStandIn (22 mm, ref z +/- 44) is not the engine | B | helper 0x0062601C..0x0062619A; M13-023 | NEW **MIN-AREA-RECT-FOOTPRINT-1**: port cv::minAreaRect for the helper quads and drop PlanarStandIn (M13-023) | none |
| Per-caller arguments at the 11 sites (0x00507258, 0x00507664, 0x0055526E, 0x005BA136, 0x005C99C0, 0x0061929E, 0x00619358, 0x0062190C, 0x00625E1A, 0x00632F14, 0x0063C730): tolerance, onTop, filter. Example: MarkObjectUnknown ignores the current ID and families 6/7 (0x00507220..0x00507240); BlockWorld.cs:2595 passes no filter. Whether each stack caller matches is UNKNOWN (not censused) | B (UNKNOWN) | listed | fold into SAMEROOT, or NEW **FIND-ON-TOP-CALLERS-CENSUS-1** | SAMEROOT |
| libm (atan2f) in the corner sort | not blocking (EQUIVALENT_IMPLEMENTATION per M13-023(e)) | n/a | none | none |
| Evidence keeps "everywhere else 15 mm" next to its own correction (30.0 at 0x005BA12C; 0x00619358 is the underneath search) | IC (self-corrected in evidence) | 0x005BA12C | at approval | none |

## M11-007: IMPLEMENTATION_GAP

Title: "Two misses before an object pose is forgotten, and two sightings before one is confirmed".
Unresolved: built (confirmer, UpdatePoseInInstance, MarkObjectUnobserved/Unknown/Dirty, SetPoseStateHelper).
"NOT SETTLED, visible: the row wording leaves open whether not-docking-and-not-carried guards both skip clauses
(read as both); MarkObjectUnknown message fields; BlockWorld.DockingObjectId and UnSetCarryObject are not wired ...;
SetLocalizedTo robot-side text, AIComponent::OnRobotRelocalized and ClearRobotToMarkers (map) are not built." (paraphrased quotes)

| Item | Kind | Address | Closing job | Waits on |
| --- | --- | --- | --- | --- |
| Skip-gate wording. Re-read: orrs.w r3,r3,sb; beq 0x00505ED2 (dock target == id OR carried) at 0x00505EAA..0x00505EAE bypasses both skip exits: 0x00505ED8 ([sp+0x6C] != 0 with an existing instance) and 0x00505EF8 (instance == existing, usable for localization, not closeAndSteady, PoseState Known). "Both" is correct | closed by this read; IC (cite the range) | 0x00505EAA..0x00505EF8 | settlement step | none |
| MarkObjectUnknown message: built as 0x52 with the original ID (AI-BODY-OBJECT-POSE-UNKNOWN-1; BlockWorld.cs:2609) | S | 0x005073AA..0x005073B8 | cleanup | none |
| DockingObjectId is wired (Docking.cs); UnSetCarryObject is wired (0x00505FBC) | S (manager, 20261112-settle-m11-043 check) | n/a | cleanup | none |
| OnRobotRelocalized: rendering only, no effect (20261111 cross-origin check) | S | 0x0051246C | cleanup | none |
| SetLocalizedTo robot-side text | L | n/a | none | none |
| The walks in MarkObjectUnknown (0x00507258) and MarkObjectDirty(propagate) (0x00507664) use the M13-007 helper and inherit its gaps (same root, minAreaRect, the family-6/7 filter) | H (M13-007) | 0x00507220..0x00507258, 0x00507664 | SAMEROOT, then MIN-AREA-RECT-FOOTPRINT-1 | SAMEROOT |
| ClearRobotToMarkers (0x00506A98 -> ProcessClearQuad 0x0067F708) on the confirming path is unbuilt with a visible MISSING (BlockWorld.cs:2373, "M8-026 / M11-074"). Memory-map effect (planner/behaviour state, item 4) | H (M8-026 / M11-074; no queue row) | 0x00506A98 | NEW **CLEAR-ROBOT-TO-MARKERS-1** (map owner), or the manager rules it outside the M11-007 claim | none |
| 20-degree clamp on the docking object (0x00505F10..0x00505F22) uses the M11-006 ClampPoseToFlat; the M11-006 C2 chain question is open | H (M11-006) | 0x00877330..0x008774EC | M11-006 settlement | none |

## M12-008: IMPLEMENTATION_GAP

Title: "The pose chain that holds a carried object". Unresolved items (a)..(e), updated by LIFT-RELATIVE-OBSERVATION-PARENT-1 and CARRIED-ONTOP-BINDING-CENSUS-1.

| Item | Kind | Address | Closing job | Waits on |
| --- | --- | --- | --- | --- |
| (a) "BroadcastObjectPoseChanged 0x00506F88 is a counted stub (M12-027 RECOVERABLE_GAP)". ApplyObjectRelativeObservation now calls BlockWorld.BroadcastObjectPoseChanged (BlockWorld.cs about line 3718, "0x00506D48"); LiftGeometry.cs:205-224 still counts UnreadBroadcasts and says "not read" | S (note and code comment stale) | 0x00506D48, 0x00506F88 | cleanup | none |
| (c) Step 7 uses the M13-007 finder; a wrong-root result reaches the warning branch 0x00633154..0x0063318A and the unbuilt AORO NotABlockWorldObject branch 0x00506D56..0x00506DA4, which the engine never reaches | B (through M13-007) | 0x00632F06..0x00632F14 | SAMEROOT | SAMEROOT |
| (c) Tilted or non-cube pair: PlanarStandIn | H (M13-023) | n/a | MIN-AREA-RECT-FOOTPRINT-1 | none |
| Census Q1: Docking.cs ~609/~631 re-assigns and re-parents the top node from TopWrtCarried after step 9 (same values, an extra write) | B? (order only; no observable effect shown or disproved) | 0x00633020 vs 0x006331AE | NEW **LIFT-ATTACH-REPARENT-ORDER-1**: remove the duplicate write, or record evidence of no effect | RETIRE, partly (the UpdateCarriedObjectPose "legacy branch" reads TopWrtCarried; check whether it survives) |
| (e) Robot::ComputeLiftPose and its per-tick call | H (M12-027, IMPLEMENTATION_GAP; B-M12-3 "built, awaiting strong verification") | 0x005151AC | M12-027 settlement check (no queue row) | none |
| The top pose value rests on M11-065 / M11-066 pose arithmetic | H (IMPLEMENTATION_GAP; no queue row) | n/a | pose-arithmetic settlement (no row) | none |

## M11-038: IMPLEMENTATION_GAP

Title: "The BlockWorld game-broadcast entry points". Evidence: BroadcastObjectObservation 0x0061FED8, BroadcastLocatedObjectStates 0x0061E6C0, BroadcastConnectedObjects 0x0061E91C, VisionSystem::CheckMailbox 0x006B2AD4.
Unresolved (rewritten 2026-11-12 by LCO-TAIL-PRODUCTION-TEST-1): 0x53/0x54 built. "Still as before: RobotObservedObject
(Size 69) and RobotMarkedObjectPoseUnknown have no message types; the CheckMailbox result deque has no producer (M11-040);
BroadcastObjectObservation is the ObjectObserved event ...; the .so Pack bodies 0x00714A10, 0x00715370, 0x007153A4 are unread."

| Item | Kind | Address | Closing job | Waits on |
| --- | --- | --- | --- | --- |
| "RobotObservedObject has no message type": contradicted by the code. RobotObservedObjectReport (Pack 0x007131E2, tag 0x44, with TopOrientationRad) was built by AI-BODY-CUBE-OBSERVATION-1 for light cubes | S, so the open scope is unclear | 0x007131E2 | NEW **M11-038-SCOPE-CENSUS-1**: recensus the four entry points against the code and list what is open | none |
| 0x44 for other observed-object classes (charger and others): "Other producer classes ... remain open" (handoff 2026-11-09) | B | 0x0061FED8 | M11-038-SCOPE-CENSUS-1 | none |
| "RobotMarkedObjectPoseUnknown has no message type": contradicted by the code (ObjectPoseUnknownReport.cs, tag 0x52; AI-BODY-OBJECT-POSE-UNKNOWN-1) | S | 0x005073AA..0x005073C0 | cleanup | none |
| CheckMailbox: VISION-MAILBOX-1 (merged 2026-11-09) built the result FIFO and drain (0x006B2AD4..0x006B2CC4, 0x00654A6E..0x00654A7A); MISSING subsets remain (tool-code, computed calibration, laser, debug consumers) | S / B (subset; the census decides which consumers are in scope) | 0x006B2AD4 | M11-038-SCOPE-CENSUS-1 | none |
| Pack bodies unread (symbols re-read): LocatedObjectState::Pack 0x00714A10 (112 bytes), ConnectedObjectStates::Pack(uchar*,uint) 0x00715370 and Pack(SafeMessageBuffer) 0x007153A4. The wire order comes from CLAD only | B (small read) | 0x00714A10, 0x00715370, 0x007153A4 | NEW **M11-038-PACK-READ-1**, or fold into the census | none |
| PoseStateChanged host/local split "at pose broadcast helper 0x0061D5C8" (B-M1-7). 0x0061D5C8 lies inside DeleteLocatedObjects (0x0061D038, size 1844), not in a pose broadcast helper | B + IC (address label) | 0x0061D5C8; 0x00506288..0x005062DA | M11-038-SCOPE-CENSUS-1 | none |
| ObjectObserved public/internal seam at call site 0x006211DC: host payload split "MISSING" | B | 0x006211DC | M11-038-SCOPE-CENSUS-1 | none |

## M12-032: IMPLEMENTATION_GAP

Title: "PathComponent drive-to-pose status machine: fields, SetDriveToPoseStatus, predicates, StartDrivingToPose, Update/UpdatePlanning/HandlePlanComplete, ExecutePath, the 0xC6 PathFollowingEvent handler, the RobotState segment and doling".

| Item | Kind | Address | Closing job | Waits on |
| --- | --- | --- | --- | --- |
| Legacy entry StartDrivingToPose(int, ...): H6/H7 holds, LegacyEntryPathInFlight, no ComputeDriveCenterPose (NativePlannerSet.cs:35), borrowed goals, the legacy-Init MISSING for +0xA4, the legacy CurrentStart in HandlePlanComplete | B (stack-only path; not production per LEGACY-DRIVE-TO-POSE-NATIVE-1, but public API) | 0x0064AE6C..0x0064AE78, 0x00649C14 | RETIRE | RETIRE |
| Manual-speed flag +0x46 from the drive callers: DriveToObject is plumbed (D4 0x005592F6); the 7-arg ctor (0x0055850C / 0x005585A8), IDriveToInteractWithObject (0x0055B2C8) and the runner track mask (0x00558544..0x0055854C) are not | H (M12-022), feeds +0x46 (0x0064AD44) | as listed | MANUAL (taken) | MANUAL |
| Planner internals: choice, status, safety (slot 0x18 -> CheckIsPathSafe 0x00509202), ComputeNewPathIfNeeded in RestartPlannerIfNeeded, cached primitive paths and the GetEndPose union heading | H (M13-033, IMPLEMENTATION_GAP; no queue row) | 0x00649FB6, 0x00649C14..0x00649D14 | NEW **PLANNER-SETTLEMENT-CENSUS-1** (M13-033 against the code) | none |
| VizManager recipients unbuilt; no "no effect" evidence recorded (SendVizMessage bodies not checked) | B? (needs evidence) | UNKNOWN (the record does not list the Viz call sites) | NEW **PATH-VIZ-NO-EFFECT-1**: show the Viz calls touch none of items 1-4, then record it | none |
| "Absent DrivePathAction remains unbuilt": callers and reachability not recorded | UNKNOWN | UNKNOWN | manager: SDK-only (exclusion) or needed? | none |
| Robot events after ClearPath | HARDWARE_ONLY (M12-041); recorded, not blocking | n/a | n/a | n/a |
| P13: the native entry passes IgnoreObstacleIds to the planners; the engine has no such input; no native caller sets it | S (stack-only, no production effect) | 0x0064ACC4 | RETIRE or later cleanup | none |
| "P25-P27 remain incomplete" (B-M12-1) is superseded by PATH-ORIGIN-CHANGE-REPLAN-1; H2/H3 are log-only VERIFYs | S / L | 0x00649830, 0x00649852 | cleanup | none |
| PathSegment.Print, PrintPlan formats | L | n/a | none | none |
| Every built part is "awaiting strong verification" (B-M12-1/2, B-M13-2/3, PATH-ORIGIN-CHANGE-REPLAN-1) | settle check | n/a | settle check after RETIRE | RETIRE |

## M11-044: IMPLEMENTATION_GAP

Title: "PotentialObjectsForLocalizingTo::Insert, UseDiscardedObservation, LocalizeRobot and the confirmed-object pose refresh".

| Item | Kind | Address | Closing job | Waits on |
| --- | --- | --- | --- | --- |
| PATH GUARD: a cross-origin pair is held before S2 while a legacy-entry path is in flight (BlockWorld.PathInFlight -> Path.LegacyEntryPathInFlight; BlockWorld.cs:1256) | B (stack-only) | 0x006499D0 | RETIRE (removes the guard) | RETIRE |
| LocalizeRobot multi-pair join X19 with a real list origin: no test | coverage | 0x0050D354..0x0050D402 | MULTIPAIR (released) | MULTIPAIR |
| "the LocalizeRobot NaN-distance order": the record does not name the compare | UNKNOWN | inside 0x0050D1CC..0x0050D4DA | NEW **LOCALIZE-ROBOT-NAN-ORDER-1**: read the distance compares and the pick order for NaN distances; build it or record it unreachable | none |
| Null-O branch body: unbuilt, visible stop (BlockWorld.cs:1222). The record says both callers pass a filled pair (0x0050D2E4, 0x0050D412) | branch our system never produces: manager exclusion with that citation | 0x00515686..0x005156AC | manager decision | none |
| "whether other components poll the current origin per tick was not censused" | B (RECOVERABLE, UNKNOWN) | Robot+0x294 readers | NEW **ORIGIN-READERS-CENSUS-1**: census the per-tick readers of the current origin and of RobotStateHistory+0x1C | none |
| Readers of RobotStateHistory+0x1C other than 0x0051561E / 0x00515CE6 not censused | B (RECOVERABLE) | n/a | ORIGIN-READERS-CENSUS-1 | none |
| FaceWorld::UpdateFaceOrigins (0x005159A8 -> 0x004F5644) is "tagged M11-044 until a FaceWorld record is assigned"; FaceEntry LegacyProjection is not moved (cross-origin handoff Q5) | IC / ownership (no record of its own) + B? (does LegacyProjection reach the API?) | 0x004F5644..0x004F5866 | manager: name the owner; check LegacyProjection in the census | none |
| The 0x45 pose value rests on M11-006 (clamp C2 chain), M11-065 / M11-066 (pose arithmetic), M11-052 (Interpolate: built, awaiting verification) and M11-097 (query join MISSING) | H (all IMPLEMENTATION_GAP) | n/a | their own settlements | none |
| Delocalize callees and the frame-id-mismatch trigger | H (M11-053) | 0x00510A24..0x00510D98 | M11-053 | none |
| Head debug log "Robot attempting to localize to %s object %d" (argument unread) | L | from 0x005154B0 | none | none |
| The evidence cites none of LocalizeToObject S0..S15 (0x005154B0..0x00515AA8), the cross-origin branch (0x0051583C..0x005159B2, Rejigger 0x00847D7C, Flatten 0x00847F34) or UpdateFaceOrigins; it lists only LocalizeRobot, Insert, SetLocalizedTo and the history ranges | IC | as listed | at settlement | none |
| B-M3M4-36 labels 0x00847F20 / 0x00847FD8 (strings, not functions) are already marked superseded; OV10 "Too strong: the marker loop failure return 1 with no writes" | S / IC | 0x00512442 | cleanup | none |
| Older round-4/5 text (Interpolate stand-in, "+0x2C4 readers not built", "RobotDelocalized carries OLD origin id") predates B-M3M4-33, M11-044-LOCALIZATION-MARKER-CAMERA-1 and LOCALIZE-SAME-ORIGIN-GEOMETRY-1; whether each is still true was not re-checked here | S? | various | ORIGIN-READERS-CENSUS-1 or the settle check | none |

---

## Other inventory corrections

- **M11-006** (not in this list; a dependency of M11-044 and M11-007): evidence line 4 ("ClampPoseToFlat 0x00877330
  itself is the shape this stack already had") is contradicted by the CLAMP-FLAT-NUMERICS-1 census C2..C19. Line 3
  ("multiplies by 0.0174533 (0x00625566)") is superseded in its own unresolved by "5.0f (0x004E024E) * 0x3C8EFA35
  (0x006256FC)". IC.
- **M11-009**, **M4-009 C11.1**, **the M11-044 evidence**, **the M11-038 0x0061D5C8 label**: see the tables.

## Ranking (least remaining first)

1. **M11-009** (EXACT_SOURCE). No behaviour gap on its guard (re-read 0x0053411E..0x0053413C, 0x0053492C..0x0053494A).
   Needs only the IC (title and evidence: "Known AND not IsCarryingObject", the guard ranges, MarkObjectDirty
   0x005075A4..0x005075B8) and the manager ownership ruling on SetPoseStateHelper 0x0050612C (cited only by M11-007).
   **Single job: SETTLE-M11-009-CORRECTION-1**, a manager-side correction at approval. No code.
2. **M4-009** (EXACT_SOURCE). One small behaviour gap: the charger-ID input (robot+0x334, writer 0x0054E11C..0x0054E120)
   is unwired, so the charger filter never fires after a mount. Plus IC C11.1.
   **Single job: CHARGER-ID-WRITER-1.** Does not wait on RETIRE.
3. **M13-007**. Two behaviour gaps: same root and current origin (SAMEROOT, released), and minAreaRect (no job). Caller
   arguments not yet censused. **SAMEROOT** is next; settleable only after **MIN-AREA-RECT-FOOTPRINT-1**.
4. **M11-007**. The re-read above closes its own open question; the rest is stale text, inherited M13-007 gaps (the
   walks), and the ClearRobotToMarkers map step (needs an ownership ruling). Settleable after SAMEROOT and
   MIN-AREA-RECT-FOOTPRINT-1, if ClearRobotToMarkers is ruled to M8-026/M11-074.
5. **M12-008**. Waits on SAMEROOT, the re-parent order (LIFT-ATTACH-REPARENT-ORDER-1, partly after RETIRE), M12-027
   and the pose arithmetic (M11-065/066), all unsettled.
6. **M11-038**. Its unresolved is partly stale (0x44, 0x52 and the mailbox are built), so the true remainder is unknown
   until **M11-038-SCOPE-CENSUS-1**. Known gaps: the Pack bodies (small), other 0x44 producers, the PoseStateChanged
   and ObjectObserved host splits.
7. **M12-032**. Waits on RETIRE and MANUAL; then the planner (M13-033), the VizManager no-effect evidence and a
   DrivePathAction decision.
8. **M11-044**. Waits on RETIRE and MULTIPAIR; then the NaN order, the origin-reader census, the null-O exclusion,
   FaceWorld ownership, a large IC, and five unsettled dependencies (M11-006, M11-065, M11-066, M11-052/097, M11-053).

## Waits on open jobs

| Job | Records |
| --- | --- |
| RETIRE-LEGACY-DRIVE-API-1 | M12-032 (legacy entry, holds, legacy-Init MISSING, P13), M11-044 (path guard), M12-008 (partly: the TopWrtCarried legacy reader), M4-009 (only the legacy dock reset site; no gap) |
| DRIVE-TO-OBJECT-MANUAL-SPEED-1 | M12-032 (+0x46 input; owned by M12-022) |
| FIND-ON-TOP-SAME-ROOT-1 | M13-007, M12-008 (c), M11-007 (walks) |
| LOCALIZE-MULTI-PAIR-CROSS-ORIGIN-TEST-1 | M11-044 |
| none | M11-009, M4-009, M11-038 |
