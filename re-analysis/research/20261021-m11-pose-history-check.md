# M11 pose/history review — partial (2026-10-21)

Examined HEAD f395e545. This is an independent bounded check of F4 in
`20261021-m11-pose-history-settling.md` (c419483d), not approval of its complete
history/pose coverage or settlement recommendations. No production edits or tests.

**Verdict: F4 citation mismatch confirmed; queue cleanup, not a demonstrated
behavior defect.** `NativeStateHistoryAdmission.cs:57` cites M11-064 and the
play-area check as support for UpdateCurrPoseFromHistory. The current manifest
M11-064 is IMPLEMENTATION_GAP, titled “UpdateObservedMarkers: the robot.play_area_size
event (60 s gate, GetExploredRegionAreaM2) and the localization-failure chain
(AddAndUpdateFailed early return, BlockWorldUpdateFailed, LocalHandlerFailed)”.
Its evidence is the play-area 0x00624F0E..0x00624F4E and observed-marker failure
chain, not this helper. M11-097 is IMPLEMENTATION_GAP, titled “RobotStateHistory
query and admission results: ComputeStateAt, GetLastStateWithFrameID and
AddRobotStateToHistory's result”; its OV128 A8 evidence explicitly names the
UpdateCurrPoseFromHistory result tail. That is relevant existing ownership,
but does not silently add this body's evidence to its frozen record.

## Independent native check

Located function through `decomp/libcozmoEngine/index.tsv`, then disassembled
actual ARMv7/Thumb instructions in the shipped libcozmoEngine.so, full executable
body 0x005135F8..0x005136FA. Resolved PLT calls, rather than trusting pseudo-C:

- history robot+0x390 at 0x00513606; newest timestamp call 0x0051360C;
  flag0 stored on stack 0x00513612/618; ComputeStateAt 0x0051361C;
- nonzero result skips to cleanup with false at 0x00513620..624;
- target pose pointer loaded through robot+0x294, owner+0x14 at
  0x00513626/62C; GetWithRespectTo(history pose, target, temporary) 0x00513654..65A;
- false conversion branches 0x0051365E to diagnostic and false; true calls
  Robot::SetPose at 0x00513664 and sets result1 at 0x00513668;
- failure diagnostic obtains historical root and both names 0x0051367C..68E,
  logs at 0x005136B8, sets result0 at 0x005136E4; return 0x005136F2..6F6.

Current C# helper calls ComputeStateAt(NewestTimeStamp, false), converts relative
to GetNativeOrigin(CurrentOriginId), sets pose only on success and returns false
otherwise. These visible branches agree with this bounded native check. This
check does **not** establish that the managed origin lookup is always identical
to the native owner+0x14 pointer, or reverify ComputeStateAt, SetPose and all their
scalar/tree dependencies. It cannot prove full live-path equivalence.

## Remaining work

Correct the comment/citation during the next relevant builder batch; preserve
record gaps and add actual helper ownership/evidence before stronger settlement.
No behavioral correction or builder release follows from this comment finding.
F1 has a new timeout answer (6d43bd2a), still unreviewed. F2's stale authority
wording exists in current M11-052, but its native branches were not independently
rechecked in this pass. F3 and F5–F9, scalar/tree internals and the broader report's
whole-record recommendations remain pending. The camera continuation and pose
serialization caller reads are separate bounded jobs. No hardware claim.


## Follow-up: raw query control flow (HEAD 46c8a213)

Partial review extended to GetRawStateAt executable body 0x00531430..0x0053164A,
independently disassembled from the shipped binary with resolved PLT calls.
Compared NativeRobotStateHistory.cs GetRawStateAtCore. No tests run.

- Empty/lower-bound/end/earliest-key gates 0x0053143A..474 return1 before output
  writes. Exact key writes output time, assigns pose, copies **0x5B** state bytes
  at 0x00531480..49C. This also corrects the current authority's old 0x5C wording.
- Node+0x24 mismatch 0x005314BA..4C0 precedes flag compare 0x005314CA/4CE.
  The adopted OV128 evidence identifies this as frame ID, whereas the current
  authority text still calls it origin ID. This pass checks ordering/offset;
  it does not independently re-extract the RobotState serializer field layout.
- Flag1 converts after pose relative to before at 0x00531504; failure follows
  diagnostic path to 0x06000000. Unsigned deltas convert to f32 separately then
  divide at 0x00531510..524. Interpolate call 0x00531534 precedes output copy
  and requested timestamp store at 0x00531558.
- Flag0 calls HasSameRootAs 0x00531588. Deltas after-t and t-before compare at
  0x00531598; unsigned >= branches to **before** 0x00531634. Otherwise after
  is selected. Thus ties choose before. Failed root check also returns0x06000000
  at 0x0053162E after diagnostics, without output state/time stores.

Current code matches these control/data-selection rows, including unsigned
fraction inputs and nearest tie-before. No query-branch rewrite is warranted.
Current M11-052 is IMPLEMENTATION_GAP, titled “RobotStateHistory::GetRawStateAt
interpolation and HistRobotState::Interpolate”; evidence includes OV128 H3-H8
which already states the corrected flag/frame/tie contract. Its authority still
says “two origin ids” and “0x5C bytes”, and its effect describes nearest output
as erroneous without limiting that statement to flag1. Reconcile that stale
text before settlement; no frozen field changed here.

This is not a complete Interpolate/pose-tree review: arithmetic, root fault
handling, assignment internals and live consumers remain under their existing
records. Earlier F4 finding stands. Whole report approval is still pending.
