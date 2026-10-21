# R-M11-POSE-HISTORY — pose/history production comparison

Request: `requests/20261021-m11-pose-history-settling.md`. Examined HEAD 8626fcfd. Independent extraction and read-only comparison; pending manager checking. Coverage opened before tracing.

| Record / step | Native behavior and instruction citation | Current production file/line and caller | Existing test / reach | Disposition |
| --- | --- | --- | --- | --- |
| 051 / admission | Nonempty raw map: stale drop only when newest>window and t<newest−window (0x00530D2C..D38); u32 t−newest versus rounded/clamped f64 timeout (D3A..DB6); process u8 gap count (DB8..E30); sixth gap clears **raw only**, returns1 (E68..E78). Empty raw skips these tests and does not reset the counter (D0A→E82). | `Vision/NativeRobotStateHistory.cs`:66–102, AddRawOdomStateCore; called from EngineRobot.AddIncomingStateToHistory via actual Sensors full-state dispatch. | RobotStateHistoryAdmissionTests.SixthLargeGapClearsOnlyRawAndAnOlderKeyWrapsTheDelta / EmptyRawMapDoesNotResetTheGapRun: packet dispatch. | **Supported** for gates; timeout writer closure still UNKNOWN (F1). |
| 051 / parent, duplicate, cull | HasParent → GetParent → parent.IsRoot (0x00530E82..EA4); reject nonflattened with error, preserve map (F00..F7C). Emplace duplicate warns/returns1 and does not cull (EB0..ED6). Cull keeps cutoff keys; guards raw count<2/newest<window (0x005309D8..A0A), prefix-erases raw/vision/computed and corresponding key index (A0E..C38). No per-origin purge. | Same file:94–140. Clear clears raw/vision/computed/both indices; sixth-gap ClearRaw does not. | DuplicateAndBadOriginDoNotAdmitOrRunTheTail: dispatch; CullingKeepsTheCutoffAndErasesTheKeyIndexPrefixRatherThanPairs: **unit**, auxiliary indices explicitly seeded. | **Supported**; auxiliary index producer is M11-044, not established by this cull test. |
| 052 / raw query | lower_bound; empty/missing bracket returns1 untouched (0x0053143A..474); exact full pose+0x5B state copy (480..49C). Different **pose frame IDs**, not origins, return0x06000000 before flag (4BA..4CE). Flag0 checks same root, chooses nearer key with tie-before (584..5A4,634..648). Flag1 GetWithRespectTo(after,before), u32 differences→f32 division→Interpolate (4FE..560). | `Vision/NativeRobotStateHistory.cs`:145–181, GetRawStateAtCore. Internal callers use real parented HistRobotState; public At is a host facade. | BracketFailure…, FrameIdMismatchPrecedesTheFlagTest…, NearestTie…: direct-query unit tests using dispatch-built entries. Cliff consumer provides flag1 production reach. | **Supported**; old nearest/tie-later stand-in removed. Authority's “origin ids” is stale (F2). |
| 052 / interpolate | Select state base using strict f<**0x3EFFFEB0**, NaN selects after; copy state, preserve before.frame; blend head/lift/wheels with separate f32 subtract/multiply/add; fill cliffs with FFFF then blend/round only cliff0 (0x00530698..766). Translation is before.t + f·relative.t, yaw via Radians, parent=before.GetParent (76A..85A). | Same file:186–213. Preserves base serialized timestamp/pose; geometric pose is separate. Root-before guard is the recorded M1-034 skip, not an invented native return. | InterpolationUsesNativeWidthsAndOnlyCliffChannelZero and threshold theory: unit, independent literal bits/thresholds; DetectedCliffUsesInterpolatedRobotHistoryAndIsPlacedInTheWorld: dispatch→world. | **Supported algorithm**; libm/pose dependencies remain named; no hardware/new equivalence corpus run. |
| 097 / exact and raw selection | ComputeStateAt first checks exact vision key and copies full state, flag-independent (0x00531792..7E6); otherwise raw query, propagate nonzero (7E8..804), then choose latest vision key≤selected raw time and compare frame IDs (808..894). | Same file:215–230, ComputeStateAtCore; vision-only writer EngineRobot.SetNewPose. | ExactVisionStateHasPriorityEvenWithNoRawBracket / LastFrameQueryRequiresRawEvenWithAnExactVisionState: unit query, real SetNewPose producer. | **Supported**. |
| 097 / correction chain | For newer vision frame, build delta from raw anchor parent; frame-increase delta is accumulated **before** stop comparison (0x00531904..0x00531A7E). Apply vision pose and its parent; write only output pose/frame/head/lift (A82..AC6), not full raw state. GetParent(A) only when A.frame==R.frame (1904..193E); other null-parent sites 1A00/1AA0. | Same file:231–260; B34's narrowed guard is present. | CorrectionAccumulatesTheBoundaryDeltaBeforeStoppingAndOnlyWritesFourFields: unit with sentinel timestamp/status/cliffs; M3M4ReviewFixTests.CorrectionDoesNotAskForAnchorParentWhenItsFrameDiffersAndNoFrameIncreaseOccurs. | **Supported**; broad B33 guard superseded, F3. |
| 097 / last-frame query | Empty raw fails even if vision-only has a match (0x00531E3C..E42). Reverse raw-first search with early frame<requested stop, then reverse vision; full-copy success (E46..F22, F88..FB8). Error/flag on failure, output retained (F46..F86, 0x0053204C..090). | Same file:263–275, GetLastStateWithFrameIDCore; used by admission for mismatched frame. | LastFrameQueryRequiresRawEvenWithAnExactVisionState; DuplicateAndBadOrigin…; direct missing-query assertions. | **Supported**. Empty-history text sparse but it names real failure; no behavioral blocker from wording alone. |
| 097 / RobotState input and result | Treads-boundary Delocalize resets mismatch count and jumps to tail (0x00512B92..BAA); otherwise ramp branch or origin admission (BE2..C54), flatten incoming pose, x/y from flattened input (0x00513116..13A), z from current pose or last state of incoming frame (0x0051307A..110); AddRaw result returned by AddRobotStateToHistory (0x00513598..5C0). Tail result is UpdateCurrPoseFromHistory XOR1; frame mismatch count/101st Delocalize remain M11-053. | `NativeStateHistoryAdmission.cs`:12–55; `Sensors.cs`:1020–1090. No history-success fixture in production path. Ramp true input unreachable per OV127/M2-024. | DuplicateAndBadOrigin…, TreadsStateDelocalizesBeforeTail…, CORE008 treads regression and threshold dispatch tests. | **Supported current join**; broader Delocalize/ownership remain separate. |
| 097 / current robot pose | ComputeStateAt(newest raw key, flag0), express result w.r.t. current world origin, SetPose on success, bool false on failure (0x005135F8..0x005136F6; flag store 13618, query1361C, transform1365A, SetPose13664). | `NativeStateHistoryAdmission.cs`:59–76; `Sensors.cs`:644 in threshold schedule. SetPose/MainPose updates are present in NativeDelocalization.cs. | Dispatch admission/threshold tests observe real MainPose consequence. | **Supported bounded join**; comment incorrectly credits M11-064 (F4), not a code-behavior defect. |
| 065 / scalar arithmetic used by history | double quaternion normalization strict threshold 0x3E56A09E19D672B6, reciprocal fast branch else sqrt/divide (0x00849DB8..E52); separate f64 quaternion products then normalize (0x00849EF4..F8E); point rotation narrows quaternion to f32 without renormalizing (0x0084ACA6..ACE2→0x00849C1C..CF8). Transform3d copies f32 translation, rotates/adds before rotation composition; precompose alias sees updated rotation/translation (0x0084B92A..BB98). Radians keeps (−pi,pi], -pi→+pi, flag-aware assignment (0x0084C828..936). | `Vision/NativePoseArithmetic.cs`:25–64,132–161; `NativeRadians.cs`:4–42; Pose3d delegates through these types. | NativePoseBuilderTests normalization/cancellation/alias/bit-constant tests; B-POSE-2 branch tests; real history uses these helpers. | **Supported scalar pieces**, not all of 065. R31/R32 native origin serialization APIs remain unbuilt/unreconciled (F5). |
| 066 / frame-tree arithmetic | Null-input false; same **wrapper** input warning/copy/parent, not identity; shared-root gate; depth walks and post-step early exits; combine Tt^-1*Tf then parent to to (0x00845A34..E04). Root helper copies roots or FindRoot/GetWithRespectTo (0x004DF628..6DC). Do not optimize immediate-parent case into a copy; rounding differs. | `Vision/Geometry.cs`:344–387; PoseTreeNode HasSameRoot/Depth; native admission and history query paths now use real origin nodes. Legacy WithRespectTo/IsSameAs branches at389–417 remain explicitly candidates. | NativePoseBuilderTests.ThreeDeepTreeAndSameObjectAliasFollowDistinctNativeResults; PoseTreeVerifyBuilderTests; dispatch admission tests. | **Supported native-node path; whole record held** for legacy caller inputs/remaining ownership mapping, F6. |
| 067 / copy/parent | Copy allocates new node and retains parent (0x008458F0..92A; node ctor0x00845FE4); non-null assignment mutates destination node, copies parent/T/name and appends _COPY if nonempty; same wrapper no-op, different wrappers of same node still append (0x00845940..0x00845A2E). SetParent shares the parent node, with owner check0x00844A14..A7A. | Geometry.cs:273–323. C# Parent reference differs internally from C++ shared_ptr, but node identity and in-place assignment are modeled. Dispose emits M11-077 MISSING; null assignment emits unbuilt NotLastOwner diagnostic. | DeepCopyAndAssignmentKeepNodeIdentityAndSharedWrappersObserveWrites; PoseTreeVerifyBuilderTests; B-POSE-5 adopted PASS. | **Supported defined copy/parent semantics**; raw shared_ptr machinery need not be imitated, but reachable diagnostics/child lifetime effects must be accounted for, F7. |
| 067 / Rodrigues and matrix repair | Fixed CV_32F native callers via 0x008499B0..9A10 / 0x0084A3F4..45C / 0x0084ADFC..AEBC; adopted corrected OpenCV cal3d0xF28E..FB9A, core0x595C0..59B56/hypot0x56EB0: range rejection→zero→identity, double Jacobi/SVD/GEMM with narrowing. | `Vision/NativeRodrigues.cs`:12–166; Geometry.cs Mat3.Orthonormalized/ToRotationVector; NativePoseArithmetic.FromMatrix. Code matches the checked recipe's range, RNG, pair order, zero-fill and normalization structure. | RodriguesRoundTripsIdentityHalfTurnAndRejectsNonfiniteMatrix; NativePoseBuilderTestsBPose2 half-turn asymmetry. Both **unit**, no claim of live camera-root equivalence. | **Supported against adopted checked rows**, not a fresh complete OpenCV binary audit or whole-record settlement. |
| 068 / less-used math and sharing | Checked Euler/X/Y/Abs/list ApplyTo, 2d arithmetic/plane fields, Radians assignment, GetParent/FindRoot sharing. Corrected NaN/tie and destination-flag rules in 20261014-pose-rows-recheck.md; native addresses preserved in evidence snapshot below. GetParent(root) faults at0x008452BA, root-safe FindRoot is separate. | Geometry.cs:73–145,315–327; NativePose2d.cs:5–75; NativePoseArithmetic.cs:156–161. Wider Pose2d owner/tree APIs not modeled by the constructor/plane carrier. | AlternateEulerSetsOnlyTheYFlagAndVectorTransformSupportsAliasing; EulerAssignmentRetainsTheExistingDestinationFlag; alias 2d test; B-M11-2 root-safe tests. | **Supported checked units; whole claim not settled**, F8. No-call double-point/IsValid bodies do not justify a new unsupported API substitute. |
| Consumer / cliff | ComputeStateAt(cliff.timestamp,true), nonzero fails; native call0x006349A6, flag0x0063499E. World insertion is downstream M4-038/M11-062/M13-030. | Sensors.cs:842–862,885; real robot.PoseHistory at857; optional test seam not installed in production. | DetectedCliffUsesInterpolatedRobotHistoryAndIsPlacedInTheWorld: received CliffEvent→query→real world. | **Supported history input/result join**; does not settle every downstream map/geometry path. |
| Consumer / unexpected movement | Query first-increment timestamp with flag1; distinct success,0x06000000,other gates (0x0063E636,0x0063E74A). Movement::Update runs before current state admission (0x00512B5C). | Sensors.cs:90–97 binds query to real robot history; UnexpectedMovement.cs:263–295 consumes its result; no rewind/write is built there. | MovementQueryIsBoundToRealHistoryWithInterpolation: unit calls bound delegate, not detector trigger. No end-to-end response proof. | **Supported query join; unbuilt response** SetNewPose/absolute localization/obstacle is M10-007. Classifier is next independent job, not a dependency on publication here. |
| Consumer / image frame | SetNextImage queries min(imageTs,newestRaw), flag1; success handoff, origin mismatch logs/returns0, other failure returns error, neither failure hands on (0x00652C22..C78;0x00653158/15A). Next native stage additionally GetLastStateWithFrameID(current frame), GetHistoricalCameraPose, homography lookup, VisionPoseData.Set (0x00652CFC..0x00652D58). | VisionSystem.cs:118 frame event subscription; OnFrame751, QueryFramePose762–787, HandOverFrame804, ProcessCapture965–978. History property at267 returns EngineRobot.PoseHistory. Actual query join restored; remaining PoseData is host facade at NativeRobotStateHistory.cs:281–287. | M3M4ReviewFixTests.CameraHandoffReachesObjectObservedAndItsHostLightCommandDoesNotHoldFrameGate: real image chunks/RobotState; HostFramePose…: unit direct ProcessCapture. | **Supported query/handoff restoration; unbuilt full post-query assembly** M11-049. No whole camera-path claim, F9. |
| Consumer / face actions | ComputeTurnTowardsImagePointAngles uses flag0 at0x005187F6 and rejects nonzero query before using heading/head. | Vision/FaceActions.cs:125–135 uses ComputeStateAt(imageTimestamp,false), not At; called by Behavior/FaceBehaviors.cs:714. Other face/movement actions still read Latest flat facade. | Flag0 nearest/tie currently direct-query unit tests; no production action→wire test found for this exact branch. | **Supported history result/flag join**, remaining angle/action/world-input paths M14-005/M12/M13; no statement they are complete. |

## Conclusion and evidence precedence

The older “nearest endpoint stand-in”, “no production history delegate”, “camera frames always stop” and “raw admission purges the prior origin” descriptions are **superseded** in current code. They must not become new build work. B-M3M4-33 built the core; its adopted FAIL identified a camera exclusion and a too-broad root-anchor guard. B-M3M4-34's adopted PASS confirms the bounded fixes, not the entire seven-record production claim.

Read in precedence order: `20261014-pose-rows-recheck.md` overrides corrected rows in `20261013-pose3d-arithmetic.md`, `20261013-rodrigues-and-pose-tree.md` and `20261013-remaining-pose-bodies-check.md`; B-POSE-2..5 later check the changed code; OV12 and OV128's adopted check establish history rows; `20261019-b-m3m4-34-check.md` supersedes the affected findings in `20261019-b-m3m4-33-check.md`. In particular, neither original OV128's broader stack comparison nor B33's intermediate MISSING handoff describes today's code.

Fresh native reads in this pass covered the raw admission body through return, cull body, GetRawStateAt/Interpolate through return, ComputeStateAt and GetLastStateWithFrameID through return, current-pose helper, AddRobotStateToHistory, the selected admission/result/caller ranges, scalar normalize/product/transform/Radians bodies, copy/assignment/SetParent, frame conversion and flattening. Less-used math and OpenCV internals were compared to the **adopted corrected extraction**, not represented as a new full binary audit. Current code and regressions were read; no build, test suite, emulator or hardware run was performed.

## Remaining obligations and bounded recommendations

**F1 — 051 timeout input remains explicitly UNKNOWN, not a new hardcoded policy.** Current code models ConnectionTimeoutInMs as mutable f64, default5000. Native symbol is ReliableConnection::sConnectionTimeoutInMS, object0x01051F48, GOT0x0103EA54. This pass re-opened Init store0x0062F070 and Configure store0x0062F172: both supply high word0x40B38800; the report does not claim a complete writer/alias census or fully traced low-register provenance. The adopted OV128 check explicitly leaves that census open. A narrow follow-up should close this existing input gap before a whole-record “all live gate inputs proven” claim. The admission algorithm itself has source-supported production reach; no repeat rewrite is needed.

**F2 — 052 evidence text is internally stale.** Current authority describes 0x005314BA/BC as “two origin ids”, while its later unresolved/adopted evidence correctly says pose_frame_id (RobotState+4 in node+0x24). The effect's nearest-state sentence applies to flag1, not the defined flag0 behavior. The body now blends flag1 and implements nearest tie-before for flag0. Correct the authority/effect wording when settling; do not classify nearest flag0 as a defect. Root-before interpolation is a native fault; the existing M1-034 skip returns a bounded failure while preserving caller state/time and logs visibly. It is not evidence that the native routine returns1 for roots.

**F3 — 097's old correction-anchor crash claim was withdrawn, correctly.** 0x00531904 is a frame load, not a newly found invalid end iterator. Guard GetParent(A) at0x0053193E only for equal anchor/raw frame; handle the separate loop-parent and vision-parent faults at0x00531A00/1AA0 at their actual call points. Current implementation and B34 regression do that. The correction intentionally writes only pose/frame/head/lift: it leaves the caller's other state fields untouched. Filling them from raw would introduce behavior. A wider corpus of increasing/decreasing frames is useful; do not “repair” this into a conventional interpolation algorithm.

**F4 — current owner citation mismatch, not a behavioral failure.** NativeStateHistoryAdmission.cs:57–58 calls UpdateCurrPoseFromHistory the checked helper in M11-064/20261013-play-area-check.md. Current M11-064 is IMPLEMENTATION_GAP, **“UpdateObservedMarkers: the robot.play_area_size event (60 s gate, GetExploredRegionAreaM2) and the localization-failure chain (AddAndUpdateFailed early return, BlockWorldUpdateFailed, LocalHandlerFailed)”**; its evidence concerns 0x00624F0E..0x00624F4E and the observed-marker failure chain, not 0x005135F8. Current M11-097 owns the A8 result plumbing and M11-070 owns SetPose/vision-history helpers. Attach the helper's actual body citation to its chosen owner when reconciling these records; do not merely retain the incorrect M11-064 label as proof. This is queued citation cleanup while records stay gaps; it becomes a settlement coverage requirement before any stronger claim.

**F5 — 065 remains broader than the proven scalar core.** PoseStruct3d construction 0x00846C70..D16 widens float quaternion, normalizes it and obtains parent by origin ID. ToPoseStruct3d0x00846D38..DEE flattens, validates origin and parent, copies translation and narrows quaternion. These native bodies were reopened; no matching full APIs/root-validation serialization were found by PoseStruct/ToPoseStruct searches of current Vision code. The existing Mat3/flat pose carriers cannot prove those joins. Determine which supported live/API paths require them; build the sourced slice or record the applicable bounded scope decision. Their absence is already recorded, not repaired by the admission build.

**F6 — 066's live MainPose input note needs narrowing, not blanket dismissal.** The historical note “MainPose update binding remains unbuilt” predates B33: accepted states now go through AddIncomingStateToHistory→UpdateCurrPoseFromHistory→SetPose(MainPose), using actual origins. That foundation is present. Geometry.WithRespectTo/IsSameAs still contain explicit legacy-frame reductions; History.Latest constructs a flat facade through a matrix representation and lacks native caller root semantics. Downstream actions using that facade must be checked individually. Native conversion itself distinguishes same wrapper from two wrappers of one node and copies both transforms before output writes. Do not replace it with generic inverse-frame multiplication or immediate-parent fast copies.

**F7 — 067/077/084 ownership limit.** C# need not emulate a shared_ptr control block merely because the original uses one. What must hold is observable state, shared-node writes, parent lifetime and relevant failure diagnostics. Deep copy, in-place assignment, SetParent and owner counters are present. Geometry.Dispose still reports M11-077's missing NotLastOwner/final-node bookkeeping, and assignment-null has its own unbuilt diagnostic. M11-084 contains a reachable carried-stack origin-rejigger loss-of-owner case; this report does not re-open its census or silently exclude it as internal. For this history slice, retained origins and parented raw/vision entries are the required invariant; cross-thread query/tree writes share NativePoseTreeGate. The existing shared_ptr warning cannot by itself block settlement forever, but the manager must resolve the recorded observable cases and map them to their actual owner before claiming all of 067 complete.

**F8 — 068 partial implementation is not a complete 2d-tree port.** NativePose2d models transforms and constructor plane fields, not the wider PoseBase<Pose2d> owner/tree lifecycle. UnitQuaternion<double>*Point3d, IsValid and the initializer-list constructor have no engine callers in the adopted read; do not add them just to clear a count. Conversely, a newly used host API is not covered by that no-caller result. GetParent(root) fault, sharing and guarded callers remain M11-073/076, not an invented root-to-identity success. No new producer census was performed here.

**F9 — camera and computed-index boundaries remain explicit.** B34 restores the production SetNextImage query and handoff; the direct ProcessCapture helper now uses the same clamped timestamp selection. It does **not** implement the full native success continuation: fresh read0x00652CFC..D58 calls GetLastStateWithFrameID(current frame), GetHistoricalCameraPose, LookupGroundPlaneHomography and VisionPoseData.Set, under the component mutex. Current QueryFramePose returns the existing host PoseData facade instead. This is M11-049's already named unbuilt assembly boundary; helper call internals/returned-field semantics remain **UNKNOWN here**, and require their own narrow extraction before a full frame-pose fidelity claim. ComputeAndInsertStateAt still reports the missing auxiliary index producer after0x00531BAA (M11-044); cull/insert fixtures do not close it. No new all-M11 audit is proposed.

## Per-record recommendations

| Record | Settlement recommendation at this HEAD | Work needed before a whole-record claim |
| --- | --- | --- |
| 051 | Core admission/cull rows supported; retain IMPLEMENTATION_GAP until its remaining timeout-input evidence is reconciled or the claim explicitly bounded. | F1; do not require an unrelated computed-index implementation just to prove raw rejection gates. Preserve index-production gap in 044. |
| 052 | Recommend manager settling the **query/interpolation algorithm rows** after current evidence wording is corrected and dependencies are accounted for. No algorithm rewrite found necessary. | F2; keep libm/scalar provenance in065 and recorded root-fault policy explicit. Broader camera assembly is049, not an automatic052 failure. |
| 065 | Do not settle whole record from the working history/math pieces. | F5/F6; reconcile every R1–R34/R38–R40 claimed API path and supported callers, including origin serialization and legacy reductions, using the later checked arithmetic rows. |
| 066 | Native conversion/root path is built; no wholesale re-port. Keep whole record open. | F6/F7; actual live caller root binding and relevant shared-parent effects, not mere C++ mechanism identity. |
| 067 | Copy/parent units and corrected fixed-CV_32F implementation are present. Do not call all production matrix/tree paths complete. | Resolve recorded owner effects (F7), distinguish fixed caller contract from unsupported CV shapes, and retain libm-only boundary. |
| 068 | Checked listed units are present; retain gap for wider claimed API/lifecycle until per-path disposition. | F8; no need to extract the excluded no-caller bodies anew. |
| 097 | Admission/query/result joins built and source-supported. Recommend bounded query/admission settlement after helper ownership and scalar dependencies are reconciled. | F3/F4; explicitly hand off F9 and downstream responses instead of claiming the entire camera/cliff/face subsystem. No result-success fixtures in production. |

None of these recommendations changes a manifest status, approval, acceptance or hardware verification. Lack of a production test alone is not proof of an implementation defect; conversely a passing isolated helper test does not close a missing caller/recipient.

## Regression oracles and follow-up tests

Existing arithmetic tests use useful independent operands: exact −pi bits; normalization of 1+2^-40; non-normalized quaternion (2,0,0,0) scaling a point by4; a cancellation ordering example; rational half-turn matrix; explicit node/name identity cases. History tests assert the literal threshold0x3EFFFEB0, cliff0-only151 midpoint, 0x06000000 result, sentinel fields that must remain untouched, sixth-gap raw-only clearing, and duplicate failure. These are not circular merely because fixtures dispatch their inputs through the stack. Comparisons of two stack paths, or a Rodrigues round trip alone, would not establish native fidelity; the exact half-turn and adopted asymmetric half-turn checks are the relevant independent assertions.

Reach distinctions matter:

- Raw packet admission, suspicious-threshold scheduling and CliffEvent→world insertion have actual dispatch reach. The live image test builds ImageChunk traffic and observes ObjectObserved; calibration is a supplied host input. It proves restored handoff, not native post-query PoseData equivalence.
- Direct ComputeStateAt/GetRawStateAt calls are **unit queries**, even if their entries were admitted by dispatch. Current reach comments now say this for the prior mislabelled tests. The class's treads test is conservatively labelled unit, but its input/admission consequence does use real dispatch.
- MovementQueryIsBound… calls the installed query delegate. It does not exercise detector gating/response. HostFramePose… calls direct ProcessCapture. Neither is full production caller reach.
- The auxiliary-index cull test explicitly seeds both indices whose real insertion producer is missing. It tests only prefix erasure; it is not an index-production oracle.

Proposed small regressions for a separate builder/reviewer, without code changes here:

1. A real flag0 consumer at an exact midpoint and midpoint+1, with different before/after head angle and yaw, verifies tie-before **and emitted action inputs**. Include frame-ID mismatch and unrelated-root failures with no action send. Until the native action admission path exists, label the boundary test unit rather than pretending it is production.
2. Raw stale cutoff equality, newest==window and gap==timeout versus +1, with real dispatch and assertions on map contents/tail effects. Keep older-key u32 wrap and six-gap preservation of vision states.
3. Separate same-wrapper from same-node/different-wrapper frame conversion; output aliases from or to; immediate-parent conversion with nontrivial float values that distinguish Tt^-1*(Tt*Tf) from a copy. Expected bits should come from the native path or the checked exact operation sequence, not another stack helper call.
4. Correction with multiple rising and decreasing frame IDs, retained sentinel fields, root anchor whose parent is never requested on that branch, and the actual parent-fault branches. Use real SetNewPose as the vision-state producer; do not seed a success result.
5. Image newer than newest raw: assert query selection and full frame processing timestamp separately; frame older than oldest raw and cross-frame mismatch must never reach the mailbox. Once049 is recovered, verify its current-frame fallback/camera/homography payload against native reference rather than the existing PoseData facade.
6. Before completing a downstream History.Latest consumer, test delocalize before the next raw admission and the one-tick empty raw history after the sixth gap. MainPose/current origin may be current while the flat facade refers to an older history/root or is absent. No blanket assumption that Latest equals Robot::GetPose is justified.

## Explicit handoffs and limits

- M11-053: treads-boundary/101st-mismatch Delocalize; no full delocalization re-audit.
- M11-064: marker/world failure chain, not the owner of UpdateCurrPoseFromHistory (F4).
- M11-073/076: history producers admit parentless poses; interpolation and correction guard the specific native root-parent faults under M1-034; FindRoot is root-safe and shares nodes. Full producer census stays outside this pass.
- M11-077/084: retained-parent/lifetime effects and teardown diagnostics (F7), not a demand to replicate undefined/crashing native behavior.
- M11-044/049: computed-index insertion and post-query camera PoseData (F9).
- M10-007: rewind, SetNewPose/publication and collision obstacle after successful movement history lookup; the classifier-only job stops before this response.
- M14-005/M12/M13: actual action/world input bindings and angle/wire outcomes after the proven history query; remaining Latest consumers are leads, not newly proved faults in all callers.

No new supported-feature exclusion or no-effect claim is made. UNKNOWNs above record precisely which source/input closure or integration proof is missing; successful M3/M4 acceptance does not resolve them in bulk.

## Current primary-record snapshot

Read from `re-analysis/fidelity_manifest.json` at examined HEAD. Values below are quoted, not proposed status changes.

### M11-051

Title: "RobotStateHistory::AddRawOdomState gates (drop, consecutive-gap reset, parent check, duplicate key)"

Status: `IMPLEMENTATION_GAP`

Evidence:
```json
[
  "0x00530CF8..0x00530EC4 AddRawOdomState",
  "0x005309D1 CullToWindowSize",
  "re-analysis/research/20260929-R-VIS-verify-M11-batchA4.md"
]
```

### M11-052

Title: "RobotStateHistory::GetRawStateAt interpolation and HistRobotState::Interpolate"

Status: `IMPLEMENTATION_GAP`

Evidence:
```json
[
  "0x00531431..0x005315A6 GetRawStateAt",
  "0x0053068C..0x00530848 Interpolate",
  "re-analysis/research/20260929-R-VIS-verify-M11-batchA4.md",
  "re-analysis/research/overnight/20261014-ov10-m11-042-check.md: H2 Interpolate 0x0053068C..0x00530848 (ctor memcpy 0x5B at 0x005304F4): only cliff channel 0 is blended; channels 1-3 stay 0xFFFF.",
  "re-analysis/research/overnight/20261014-ov12-m11-052-check.md: B1-B11, GetRawStateAt 0x00531430..0x00531648 (including L2 0x005315AC..0x00531632 and the nearest tail 0x00531634..0x00531648), Interpolate 0x0053068C..0x0053085A, HistRobotState ctor 0x005304B0..0x005304FE; caller census with each flag.",
  "re-analysis/research/overnight/20261018-ov128-m3m4-settling-narrow.md H3-H8, confirmed by re-analysis/research/overnight/20261018-ov128-check.md: GetRawStateAt 0x0053143A..0x00531648 (bracket failures return 1, frame-ID mismatch 0x06000000 before the flag test, flag=false nearest with tie to the earlier key, flag=true f32 fraction); Interpolate 0x0053068C..0x0053085A (threshold 0x3EFFFEB0 at 0x00530884, f32 order, cliff words 0xFFFFFFFF then cliff[0] only; pose translation and Z angle)."
]
```

### M11-065

Title: "Pose3d / Rotation3d / Transform3d arithmetic: double unit quaternion, float translation, float point rotation, renormalised composition (rows R1-R34, R38-R40)"

Status: `IMPLEMENTATION_GAP`

Evidence:
```json
[
  "re-analysis/research/20261013-pose3d-arithmetic.md: rows R1-R34 and R38-R40 (storage 0x0084A656.., Normalize 0x00849DB8..0x00849E52, product 0x00849EF4..0x00849F8E, Rotation3d*Point3f 0x0084ACA6 -> UnitQuaternion<float>* 0x00849C1C..0x00849CF8, GetAngleDiffFrom 0x0084A694..0x0084A746, GetAngleAroundZaxis 0x0084AA1C..0x0084AAB8, GetRotationMatrix 0x0084AAE0..0x0084ABEC, Renormalize 0x008494E0..0x0084969E, Transform3d ops 0x0084B92A..0x0084BB98, Pose3d ctors 0x0084683C..0x00846DEE, PoseBase ops 0x0084505C..0x008451EA, IsSameAs_WithAmbiguity 0x00846EA4..0x008471A6, RotationMatrix3d ops 0x0084B0E4..0x0084B1B4)."
]
```

### M11-066

Title: "The pose tree: GetWithRespectTo (null/alias checks, depth walk, Tt^-1*Tf, 1000-step LoopBoundOverflow) and GetWithRespectToRoot (rows R35-R37)"

Status: `IMPLEMENTATION_GAP`

Evidence:
```json
[
  "re-analysis/research/20261013-pose3d-arithmetic.md: rows R35-R37: GetWithRespectTo 0x00845A34..0x00845DFE (FromEqualsTo warning, chain cases a-d, LoopBoundOverflow poseBase_impl.h lines 0x1A0/0x1B6/0x1D4), GetTreeDepth 0x00845E8C..0x00845F02, HasSameParentAs 0x00844D44..0x00844D7C, GetWithRespectToRoot 0x004DF628..0x004DF6DC."
]
```

### M11-067

Title: "cv::Rodrigues (OpenCV 3.1.0, both directions, CV_32F in, double inside, Jacobi SVD) and the pose-tree copy semantics (deep copy ctor, in-place operator=, shared-node SetParent, \"_COPY\" names)"

Status: `IMPLEMENTATION_GAP`

Evidence:
```json
[
  "re-analysis/research/20261013-pose3d-arithmetic.md: \"Not read\" section: cv::Rodrigues via RenormalizeHelper 0x008499B0..0x00849A10, Rotation3d(RotationMatrix3d) 0x0084A3F4..0x0084A45C, RotationMatrix3d(angle, axis) 0x0084ADA4..0x0084AF34; PoseBase::operator= 0x00845940, SetParent 0x00844A14, PoseTreeNode ctors 0x00845F44/0x00845FE4; UnitQuaternion<double>*Point3d 0x00849F90..0x0084A062; Euler/matrix X/Y 0x0084AF98..0x0084B424; IsValid 0x008497D0..0x008498E4; Abs 0x0084B51E; ApplyTo(vector) 0x0065671C; the Pose2d/Transform2d family 0x0084B619..0x0084B92A.",
  "re-analysis/research/20261013-rodrigues-and-pose-tree.md: cvRodrigues2 calib3d 0xF28E..0xFB9A (vector->matrix in double, DBL_EPSILON identity, R = c1*rrt + c*I + s*[r]x; matrix->vector cvCheckArr [-100,100) -> zero, cvSVD -> JacobiSVDImpl_<double> core 0x595C0..0x59B56 with OpenCV hypot 0x56EB0, GEMM U*Vt, s<1e-5 and 180-degree branches, acos), wrapper 0x111F8..0x1133E; PoseBase copy ctor 0x008458F0..0x0084592A, node copy ctor 0x00845FE4..0x00846044, operator= 0x00845940..0x00845A2E, SetParent 0x00844A14..0x00844A7A, node SetParent 0x008449EA..0x00844A10, WrapExistingNode 0x0084529C..0x008452C0, GetWithRespectTo write order 0x00845A34..0x00845E04."
]
```

### M11-068

Title: "Remaining pose bodies: Euler ctor and GetEulerAngles, matrix X/Y angles, Abs, ApplyTo(vector), Transform2d/Pose2d, GetParent/FindRoot sharing, Radians operator=, SetParent unowned VERIFY (null-node path unreachable)"

Status: `IMPLEMENTATION_GAP`

Evidence:
```json
[
  "re-analysis/research/20261013-pose3d-arithmetic.md \"Not read\" and re-analysis/research/20261013-rodrigues-and-pose-tree.md \"Unread\": UnitQuaternion<double>*Point3d 0x00849F90..0x0084A062; RotationMatrix3d Euler and X/Y 0x0084AF98..0x0084B424; IsValid 0x008497D0..0x008498E4; Abs 0x0084B51E; ApplyTo(vector) 0x0065671C; Pose2d/Transform2d 0x0084B619..0x0084B92A; GetParent 0x008453D4..0x00845448; FindRoot 0x0084520C..0x0084529C; PoseBase::SetParent null-parent-node log reads [null+0x38]/[null+0x44] (0x00844A46..0x00844A52).",
  "re-analysis/research/20261013-remaining-pose-bodies-check.md (and re-analysis/research/20261013-remaining-pose-bodies.md): UnitQuaternion<double>*Point3d 0x00849F90..0x0084A060 (no caller); Euler/GetEulerAngles/X/Y 0x0084AF98..0x0084B422 (NaN picks the second pair, 0x0084B3A4/0x0084B3FE); IsValid 0x008497DA..0x0084985E and the 0x00849900 ctor (no callers); Abs 0x0084B51E..0x0084B55C; ApplyTo(vector) 0x0065671C..0x0065682E; RotationMatrix2d 0x0084A19C..0x0084A1EE; Transform2d 0x0084B618..0x0084B928; Pose2d ctors 0x008463D8..0x0084660C; GetParent/FindRoot/Wrap 0x0084520C..0x0084541E; Radians operator= 0x0084C846/0x0084C884; SetParent 0x00844A14..0x00844A7A (the null-node path unreachable: no producer of a null node)."
]
```

### M11-097

Title: "RobotStateHistory query and admission results: ComputeStateAt, GetLastStateWithFrameID and AddRobotStateToHistory's result"

Status: `IMPLEMENTATION_GAP`

Evidence:
```json
[
  "re-analysis/research/20261018-m3m4-settling-b.md: no record owned these; consumers M4-019, M4-038, M10-007, M10-015, M11-044, M13-030, M14-005. Prior reads: ComputeStateAt 0x00531784..0x00531808 (research/20260929-R-VIS-pre-extraction.md); AddRobotStateToHistory 0x00512B9C..0x00512F0C (on-ramp 0x00512BE2, ContainsOriginID 0x00512C44, failure 0x00512C38); the C12.4 gate 0x00513088..0x00513110.",
  "re-analysis/research/overnight/20261018-ov128-m3m4-settling-narrow.md, confirmed with corrections by re-analysis/research/overnight/20261018-ov128-check.md: ComputeStateAt vision-only exact key first (H1, 0x00531792..0x005317E6), raw query (H2), vision correction chain (H9, 0x00531808..0x00531AC6; the frame-increase delta accumulates before the stop test 0x00531A7A..0x00531A7E); GetLastStateWithFrameID raw then vision-only, newest first (H10, H11, 0x00531E34..0x00532090); UpdateFullRobotState admission and result paths (A2-A5, A8; 0x00512BAC..0x0051313C, AddRobotStateToHistory 0x00513598..0x005135C0; x, y from the flattened incoming pose; tail returns UpdateCurrPoseFromHistory ^ 1).",
  "re-analysis/research/20261019-b-m3m4-33-check.md, SetNextImage caller re-read by the manager 2026-10-19: r6 = image timestamp [sl,#0xC] (0x00652C22); GetNewestTimeStamp(robot+0x390) (0x00652C2A); `cmp r0,r6; it lo; movlo r6,r0` (0x00652C34..0x00652C3A): the query time is min(imageTs, newest raw key); ComputeStateAt(t, &state sp+0x11C, &time sp+0x120, flag 1) (0x00652C44). Result 0 -> 0x00652CFC (pose data built, frame handed on). Result 0x06000000 -> a log (0x00652C58..0x00652C78), return 0 (0x00653158). Any other result -> a log with the history's sizes and newest/oldest times (0x00652DD6..0x00652E32), return the result (0x0065315A). Neither failure hands the frame on."
]
```
