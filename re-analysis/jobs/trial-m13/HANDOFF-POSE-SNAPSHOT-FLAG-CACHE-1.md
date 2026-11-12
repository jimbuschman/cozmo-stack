# HANDOFF POSE-SNAPSHOT-FLAG-CACHE-1 (Claude worker, 2026-11-11)

This is stack machinery with no engine counterpart, so there was no census; the queue row was the inventory. It comes
from LIVE-OBJECT-POSE-NATIVE-1's verifier finding Q4, which found no production path where it changes a result.

## What changed

**`ObservableObject.Pose` (BlockWorld.cs ~287-293):** the public snapshot cache is now rebuilt when the embedded pose's
legacy frame flag changes. Before, it was rebuilt only on a change of transform, parent or name.
- The rebuilt snapshot carries the flag: the copy constructor, Geometry.cs:277-281, already copies it.
- No `// fidelity:` tag, because no record owns this machinery.
- No other pose cache keyed this way exists. `NativeStateHistoryAdmission.cs:14` makes a fresh copy per call.

## Test

**`LocalizeToObjectSameOriginTests.PoseSnapshotFlagCache1_AFlagOnlyChangeRefreshesTheSnapshot`** (`reach: unit`):
- An unchanged read returns the cached instance.
- A flag-only change, made in place with the same transform, parent and name, returns a new snapshot carrying the flag.
- That new snapshot is then cached.
- It fails without the fix ("Assert.NotSame() Failure").

**Results:** focused classes 138/138. Full gate: see the commit.

## Check

**No separate verifier round:**
- The diff is one condition, plus a comment and the test.
- It makes no engine claim.
- The earlier verifier (Q4) already established that it has no effect on 1–4.
- I reviewed the diff and the test myself.

## Records

**M11-044:** `unresolved` gets one sentence, and `test` gets `PoseSnapshotFlagCache1_*`. No settlement.
