# R-M11-POSE-SERIAL — supported PoseStruct3d boundaries

Independent read-only job; answer `research/20261021-m11-pose-serialization-callers.md`.
Read current manifest M11-065/066 and search research/inventory first. Reuse
`research/20261013-pose3d-arithmetic.md` R31/R32 and the later corrected arithmetic
reads. The lead is F5 in `research/20261021-m11-pose-history-settling.md`, not an
approved assertion that all serialization APIs must be built.

Starting at Pose3d(PoseStruct3d) 0x00846C70..D16 and ToPoseStruct3d
0x00846D38..DEE, identify supported engine callers and the observable payload,
readiness or downstream state each requires. Which callers actually need the
missing C# origin-ID/parent validation and float-quaternion conversion join?
Trace only those caller boundaries, their field producers, branch outcomes and
API/wire consumers. Separate app/SDK-only callers with source evidence; do not
make new product exclusions. State unresolved indirect callers explicitly.

Do not repeat scalar arithmetic extraction or frame-pose camera assembly (another
open request), nor audit the whole pose tree. Return a bounded caller table with
native instructions, current production reach, exact record snapshots, remaining
unknowns and a minimal build/disposition recommendation. Native `.so` is authority;
`decomp/libcozmoEngine/` is navigation. Research-only output, no code/tests,
manifest changes, approvals, hardware or settlement. Publish under research rules.
