# R-M11-FRAME-POSE — SetNextImage success continuation

- Date: 2026-10-21. Kind: independent extraction and read-only production comparison.
- Answer: `re-analysis/research/20261021-m11-frame-pose-assembly.md`.
- Records: M11-049/M11-033 and M11-097 boundary; confirm actual ownership against
  current manifest before recommending any evidence changes.
- Lead: `research/20261021-m11-pose-history-settling.md` F9, pending review.

Read SetNextImage's success continuation 0x00652CFC..0x00652D58 and only its required
callees: GetLastStateWithFrameID(current frame), GetHistoricalCameraPose,
LookupGroundPlaneHomography and VisionPoseData::Set. Recover inputs, returned
fields, failure branches, time/frame/origin selection, copy/parent behavior and
handoff order under the component mutex. Identify each producer the continuation
needs and compare with current QueryFramePose/HandOverFrame and the host PoseData
facade. Report explicit unknown helper fields instead of fabricating a frame carrier.

Search research/inventory before tracing. Reuse B-M3M4-33/34's adopted query-time,
flag and failure gates and OV128's history query; do not reopen those settled
comparisons. The history timeout writer is another request. Do not extract marker
detection, whole camera exposure, OKAO or the entire world model. Trace downstream
consumers just far enough to establish which fields affect robot/API/state.

Record HEAD and primary record title/status/evidence. First table: step | native
instructions/literals | current production file/line and caller | record |
supported/unbuilt/UNKNOWN. Use decomp/libcozmoEngine/index.tsv for navigation only.
End with remaining builder units, evidence/approval impact and production image-input
regression designs (including failure/no-handoff cases), not tests based on the
current host facade. No code, tests, manifest/inventory edits, hardware or settlement.
Commit/push only the named answer under the queued-research rule, then continue.
Do not wait for the other queued reports or a build before doing this read.
