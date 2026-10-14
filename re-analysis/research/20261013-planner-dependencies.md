# R-PLAN-DEPS (M13-034), 2026-10-13

## Item 1 coverage — DidObjectsChange and writers

| Step | Behavior | Native citation |
|---|---|---|
| D1 | DidObjectsChange returns byte BlockWorld+54, without clearing it. | 0x00623020..0x00623024 |
| D2 | Constructor initializes it0 through alias sl=this+4C, store[sl+8]. | 0x0061BD60..0x0061BD6A |
| D3 | CreateFixedCustomObject sets1 after AddLocatedObject. | 0x0061E2C8..0x0061E2D8 |
| D4 | AddAndUpdateObjects sets1 after finding the matched located object, updating observation times/localization candidates/occluders, and broadcasting observation. Does not require a changed transform. | 0x0062112A..0x00621146; store0x006211E6 |
| D5 | AddMarkerlessObject sets1 on successful nonintersecting addition, after AddLocatedObject. | 0x00622588..0x00622596 |
| D6 | ClearLocatedObjectHelper sets1 after its deletion/stack-recursion branch; null input does not reach store. | 0x00625C78 onward; recursion0x00625E74..0x00625E7A, store0x00625E82 |
| D7 | UpdateObservedMarkers clears0 only in nonempty observed-marker-list branch, after setting image timestamp and lift occluder and before creating objects. Empty-list branch does not clear. | 0x00624F70..0x00624FAC |
| D8 | Direct byte-store scan over exported BlockWorld functions finds D3-D7; constructor uses offset alias as D2. DidObjectsChange is not a comparison with last-map-change time+58. | [writer scan](20261013-planner-dependencies-writers.txt), constructor/read above |

## Item 2 coverage — planning helpers

| Step | Behavior | Native citation |
|---|---|---|
| H1 | FindClosest discretizes query x/y/yaw using roundf; float differences, double +/-2pi followed by float narrowing; modulo heading count. Empty plan returns index0, distance FLT_MAX. | 0x00856398..0x00856480; empty0x008569A4..0x008569BA |
| H2 | First pass visits each action's initial discrete state. Exact x/y/theta match returns that index/distance0. Otherwise compare position-only Euclidean distance and retain first strict improvement. ApplyAction(false) advances state. | 0x008564D0..0x008565E8; state advance afterward |
| H3 | Second pass visits primitive intermediate positions except final sample; exact discrete match returns0 distance, otherwise compare primitive-relative continuous x/y distance. Retain earlier ties. No continuous segment projection. | 0x00856688..0x008569BA; exact0x00856806..0x0085681E ->0x008569CC |
| H4 | Optional stdout diagnostics, not planner logs; output distance written at return, including0 exact. | 0x00856310..0x00856398; 0x008569BA..0x00856A38 |
| H5 | PlanIsSafe empty input returns0 leaving outputs untouched. Nonempty clears output actions, seeds start, skips closestIndex actions without collision checking, initializes output start/state. | 0x00850B88..0x00850C62 |
| H6 | Reapply remaining actions with checking; fail iff newPenalty > double(oldPenalty)+0.5. Append prefix while retention enabled, update endpoint, disable further retention after endpoint moves farther than tolerance; keep checking remainder. | 0x00850C7A..0x00850D6E |
| H7 | All checked actions safe returns1. Collision prints action/index/old/new penalty and returns0 with retained prefix and its endpoint. | 0x00850D72..0x00850D92 |
| H8 | PathIsSafe empty returns0 without clearing output; nonempty clears output, seeds State_c from first segment start/supplied angle, sums ApplyPathSegment(true,1e-5), appends safe whole segments; sum>1e-5 fails. | 0x00850E34..0x00850F16 |
| H9 | ApplyPathSegment obtains endpoint first. No checking: write endpoint and return0. Checking: sample-count and type-specific stepping; obstacles by heading bucket, polygon Contains, weighted accumulated penalty. Early threshold exit leaves State_c unadvanced. | 0x00850610..0x008509EE |
| H10 | Normal completion writes endpoint and returns **0**, not subthreshold accumulated penalty. Unknown kind logs Error, sets error flag, optional break, returns1000. | 0x008509F4..0x00850A70 |
| H11 | xythetaPlan::Append copies source start x/y/theta only when destination action vector empty; always inserts source action/penalty pairs at end. No continuity check or collision pass. | 0x00851870..0x0085189A |

## Item 3 coverage — path building and capacity

| Step | Behavior | Native citation |
|---|---|---|
| B1 | AppendToPath leaves existing destination intact, walks all actions from plan start, validates heading/action lookup, skips requested count but still advances state. | 0x00855D90..0x00855E78 |
| B2 | After skip reaches0, call primitive AddSegmentsToPath at current resolution-scaled x/y and heading-table value. Advance x/y by primitive signed16 offsets, theta by its end byte. | 0x00855DFA onward; primitive call0x00855E38 |
| B3 | AddSegmentsToPath copies40-byte primitive segments, offsets their geometry, merges same-sign lines and coincident/same-speed point turns, otherwise appends; arcs always append. | 0x0085590C..0x00855AB8 |
| B4 | Primitive helper ignores AppendSegment failure; lookup failure prints and returns from AppendToPath with partial output. | 0x00855A74..0x00855A7C; lookup diagnostic branch in0x00855D90..0x00855E78 |
| B5 | Path ctor allocates128*40bytes, initializes only segment kind words, count0/capacity byte128. | 0x0085C714..0x0085C746 |
| B6 | AppendLine/PointTurn/Segment test signed count byte>=0 (counts0..127 accepted); increment to128 after last allowed append. Full returns0 with CoreTechPrint; no write/count increment. | 0x0085CE88..0x0085CEE0; 0x0085D3C8..0x0085D43A; 0x0085D46C..0x0085D4A6 |
| B7 | PointTurn writes kind3, x/y/angle/tolerance, shortest flag byte, and speed/accel/decel. AppendSegment copies all40bytes. No grow allocation on append. | 0x0085D3F0..0x0085D424; 0x0085D484..0x0085D490 |

## Item 4 coverage — ComputePath goal loop

| Step | Behavior | Native citation |
|---|---|---|
| G1 | F4-disabled branch uses two acceptance thresholds0.01,1000; first pass with any accepted goal wins. | 0x004FE780..0x004FE7F2; pass advance0x004FE988..0x004FE998 |
| G2 | Compute xyz squared distance in float: subtract each coordinate, x*x then +y*y then +z*z. Only test collision if no accepted goal yet OR candidate is strictly closer. | 0x004FE7F6..0x004FE86A |
| G3 | Round x/y against reciprocal resolution; normalize float yaw with double +/-2pi/narrow, scale/round/modulo, pack State. | 0x004FE86E..0x004FE94A |
| G4 | Accept penalty strictly below threshold; update distance/index and found bit. No tie replacement. | 0x004FE94C..0x004FE976 |
| G5 | Byte-truncated loop index; after success call slot2C single-goal ComputePath, store chosen byte+2A and propagate result. No valid goal logs/returns0. | 0x004FE978..0x004FE9E0 |

## Item 5 coverage — ApplyMotionProfile subranges

| Step | Behavior | Native citation |
|---|---|---|
| M1 | For linear/arc choose downstream boundary speed0 or previous reverse-vector speed; choose upstream reference profile.speed or0 according to original start/point-turn boundary, with special single-input behavior. | 0x00508C14..0x00508C42, 0x00508C66 |
| M2 | Required decel=(downstream²-upstream²)/(length*-2), binary32. Tiny abs<1e-5 keeps current speed; otherwise compare profile decel with >1 excess criterion, possibly recompute signed sqrt speed. | 0x00508C6A..0x00508D4C |
| M3 | Point-turn profile uses abs(profile.turnSpeed) with copied sign; resets continuity flag. Copy adjusted segments to reverse vector. | 0x00508C44..0x00508C64; 0x00508D50..0x00508D90 |
| M4 | Split line: signed(length-tail) to midpoint using cosf/sinf of end heading, first speed=current, second=downstream with current sign, or signed20 if downstream0; retain accel/decel; append both. | 0x00508F48..0x0050906E |
| M5 | Normal completion PrintPath then returns1; unknown kind Warning returns0; reverse temporary vector destroyed. Capacity failures of output appends are ignored. | 0x00509070..0x005090DE |

## Item 6 coverage — MinimalAngle operands/logs

| Step | Behavior | Native citation |
|---|---|---|
| N1 | s18 is narrowed double-sqrt distance; s16 is2.5. Compare s18,s16, skip forward travel if <=; abs initial Radians widened to double and compared to exact double0.01. | 0x00503C7E; 0x00503E40..0x00503EA2; double literal0x00503DD0 |
| N2 | Final delta=desired heading minus updated cursor heading; abs widened to double, compare0.01. | 0x00504040..0x00504078; double literal0x00504270 |
| N3 | Conditional final turn uses signed1rad/s, accel/decel10, tolerance0x3D0EFA35, shortesttrue; logs delta then sets cursor heading. | 0x0050407A..0x0050412A |
| N4 | Final-position log always on forced generation, even no final turn; x/y and heading degrees from cursor, not goal translation. | 0x0050412E..0x00504174 |

## Source and scope

Answers [requests/20261013-planner-dependencies.md](requests/20261013-planner-dependencies.md), following [the checked planner report](20261013-planner-internals-check.md). Binary3.4.0-1204 `resources/lib/armeabi-v7a/libcozmoEngine.so`, SHA256 `02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`. Native ELF addresses; Thumb bit omitted. Local Capstone `skipdata=True`; native instructions/literals are authority, Ghidra only navigation. Coverage means primary reads for manager verification, not a status promotion. No implementation or manifest edits.

### 1. Change flag lifetime

The flag is byte+54; last-change time+58 is separate. Constructor0x0061BD6A writes0. Five subsequent static byte writes were found in the exported BlockWorld family and checked with receiver data flow: four set1 and one clears0. Fixed custom addition sets after AddLocatedObject; markerless addition sets after nonintersecting success; observation processing sets after broadcasting a matched located instance, even with no recovered transform-change condition; ClearLocatedObjectHelper sets after its applicable deletion/stack handling. UpdateObservedMarkers clears only for a **nonempty input marker list**, before CreateObjectsFromMarkers. Consequently an empty observation tick can leave an earlier1 sticky; DidObjectsChange itself does not consume/reset it. A nonempty tick clears before later processing can set it again, including before later failure paths. Read access is a raw byte load.

The writer scan includes CreateFixedCustomObject0x0061E2D8, missed by searching only0062 decomp files. Constructor uses[this+4C]+8, so literal-offset-only scan also misses it. This inventory combines owner-function scan, decomp leads and manual alias inspection; it does not assert an exhaustive proof against arbitrary computed aliases anywhere in the entire binary. Caller task/restart behavior is already M13-033, not reclassified here.

### 2. Closest action and safety outputs

FindClosestPlanSegmentToPose returns **an action index**, not a geometric projection or fraction. Empty: index0, distanceFLT_MAX(0x7F7FFFFF). Discretize robot x/y by float reciprocal-resolution multiplication, roundf and signed conversion; yaw repeatedly widened to double, +/-double2pi, narrowed to float, scale/round/modulo. Pass1 examines action initial states in order, exact discrete x/y/theta -> immediate distance0/index; otherwise nearest position uses float scale/subtraction, double squares/sum and double sqrt, narrowed to float. Strict improvement keeps earlier equal distance; heading affects exact match, not Euclidean distance. Each action advances through ApplyAction(false).

Pass2 restarts at plan start, looks up the primitive and its intermediate samples. It examines indices0..sampleCount-2 (vectors shorter than2 skipped). Continuous comparisons are in primitive-relative coordinates: query minus action initial resolution-scaled x/y; sample minus that relative query; float subtraction then double squares/add, **narrow squared sum to float before sqrtf** (0x00856822..0x00856858). This differs from pass1's double sqrt. Exact discretized sample test compares query's original discretized coordinates with **primitive-local** sample coordinates (0x00856806..0x0085681E), without adding the action's world offset; preserve that literal comparison rather than “fixing” the coordinate frames. Exact match returns distance0/current action; otherwise strict improvement updates index. Final plan endpoint is not independently searched after the action loop. Debug Boolean controls stdout `Searching for position near ... along plan of length ...`, per-action `initial`, sample `position ... --> ...`, ` exact: ` and `  closest\n`; it does not change search decisions.

PlanIsSafe returns0 for empty old plan **without clearing outputs**. Nonempty clears prefix actions, seeds state/start, applies actions before startIndex with collision checkingfalse, initializes prefix's start to resulting discrete state, and writes its continuous x/y/theta as output replan start. Remaining ApplyAction(true) results are compared against each stored penalty: widen old penalty to double, add double0.5, compare widened new penalty. Strict increase beyond allowance prints `Collision along plan action %lu (starting from %d) Penalty increased from %f to %f\n`, returns0. No collision ->1.

Retention is bounded by displacement from the closest-state position: append safe action while retention enabled, update output endpoint, measure distance, then disable subsequent appends when distance>tolerance (40 at caller). The first action whose endpoint crosses40 is **already appended**. Once disabled it never resumes if the path comes back. All remaining actions still undergo collision checking, so a far-later collision returns0 with the bounded retained prefix. Endpoint distance uses float differences, double squares/add, narrow to float before sqrtf. Negative/out-of-range startIndex is unchecked; no guessed managed correction is supplied. Terminating/undefined malformed cases use existing M1-034 smallest-unit skip rule, not a new departure.

PathIsSafe and ApplyPathSegment are different from PlanIsSafe's saved-penalty comparison. Empty path returnsfalse and leaves output alone. Nonempty clears output; State_c starts from first segment start and supplied angle. For each segment invoke ApplyPathSegment(check=true, threshold=1e-5), sum returned penalties in float; if sum>1e-5 log Info/`Unnamed`/`xythetaEnvironment.PathIsSafe` / `Collision along path segment %lu with penalty %f, total penalty %f`, returnfalse; otherwise append whole segment. Successful traversal returnstrue. AppendSegment return is ignored.

ApplyPathSegment first reads GetEndPose. When checkingfalse it writes endpoint and returns0. With checkingtrue its literal sampling arithmetic is:

```text
N=max(ceilf(f32(length*4+1)),
      ceilf(f32(abs(Radians(rawFloat(segment+0xC)-state.theta))*0x4122F983)),2)
den=f32(N-1); inv=f32(1/den)
line: dxStep=inv*(endX-startX), dyStep=inv*(endY-startY), angleStep=0
arc: angleStep=inv*rawFloat(segment+0x14)
turn: angleStep=inv*Radians(rawFloat(segment+0xC)-state.theta)
weight: line/arc = den/GetLength(); turn = den/rawFloat(segment+0x14)
sample i=1..N-1; start point i=0 is not checked
```

All products/divisions/position additions above are binary32; angle normalization repeatedly uses double2pi with float narrowing. Line sample position starts from passed State_c x/y plus i*steps, with heading fixed. Arc position uses center+radius*cosf/sinf(startRad+i*angleStep), heading state.theta+i*angleStep. Turn position remains passed state x/y, heading advances. Rounded/modulo heading selects obstacle vector at env+44; visit polygons in stored order, Contains(samplex,sampley), accumulate `weight*polygonPenalty(+44)` in float. At accumulated>=threshold return immediately **without advancing the supplied State_c**. On normal completion write GetEndPose endpoint and return **0**, discarding any subthreshold accumulator (0x008509F4 loads zero into s17). Unknown segment: Error `xythetaEnvironment.ApplyPathSegment` / `Undefined segment %d`, `_errG=1`, optional DebugBreakOnError, return1000.

Two literal union reads require care: the common sample-count reads raw+0C even for line/arc, and the turn weight reads float+14, overlapping its shortest-direction byte. Path constructor initializes only kind words, and AppendPointTurn writes only the flag byte at+14 (0x0085D424); it does not initialize neighboring padding. Hence on a freshly allocated turn path this weight can read indeterminate bytes. Do not replace the read with a sensible angular length and claim source fidelity. Record this exact owning M13-034 crash/undefined-memory boundary; the standing M1-034 rule covers skipping that smallest undefined segment while retaining prior state and visibly logging. Shipped primitive-segment reachability/padding contents would need a separate corpus read before claiming every turn instance reaches indeterminacy. Equality at threshold can also early-return without state advancement while PathIsSafe's strict `>` still appends; preserve the distinct comparisons.

xythetaPlan::Append copies source start if destination empty, then concatenates action/penalty records. Nonempty retains old start. No boundary-state assertion inside Append; DoPlanning's mismatch log/transient0 happens before it in the caller. ApplyAction and FastPolygon::Contains are named source interfaces used here, with their internals separately owned; this is not a new whole-A* or polygon audit.

### 3. Path construction, merges and full capacity

AppendToPath neither clears destination nor changes the plan. Start at plan's signed16 x/y and heading; for each action validate heading bucket and action ID, lookup primitive from env+14. Bad lookup prints `ERROR: can't look up prim for angle %d and action id %d\n`, returns with any earlier output retained. StartIndex counts how many actions to skip; skip still applies end-x/end-y and end-theta. Once zero, send primitive's cached segments through AddSegmentsToPath at float(resolution*x), float(resolution*y) and heading-table angle. Integer state offsets narrow back to signed16. There is no synthesizing of primitive arcs from arbitrary endpoint deltas.

MotionPrimitive::AddSegmentsToPath copies40bytes per cached segment then OffsetStart. If destination empty or kind differs, append. Same kind2 arc always append. Same kind1 line merges iff `(oldSpeed>0)==(newSpeed>0)`, replacing only old end x/y; it does not check collinearity/endpoint tolerance, and keeps old profile. Same kind3 turn merges iff abs(oldX-newX), abs(oldY-newY), abs(oldSpeed-newSpeed) are each strictly<1e-5, replacing only old target angle. Otherwise append. Same unknown kind prints `ERROR (AddSegmentsToPath): Undefined segment %d\n`, then appends. It obtains each temporary's endpoint afterward; its final byte result is initialCount minus1 only when initialCount>0 and no append was attempted, else initialCount. AppendToPath ignores that result. “Append attempted” is set even if capacity rejected it.

Path owns128 slots of40bytes (0x1400); count is unsigned byte but append tests its **signed** value. Counts0..127 accepted; accepting127 advances to128, and count128..255 reject. Append* never reallocates. Full CoreTechPrint formats:

- `ERROR (AppendLine): Exceeded path size\n` (0x0085CED0 onward);
- `ERROR (AppendPointTurn): Exceeded path size\n` (0x0085D432);
- `ERROR (AppendSegment): Exceeded path size\n` (0x0085D49E).

Returns1 successful append,0 full, retaining previous count/data on failure. AppendLine defines geometry/profile then increments; turn writes explicit fields/flag; AppendSegment copies all40bytes verbatim including padding. Primitive builder, PathIsSafe and ApplyMotionProfile ignore false returns. Thus a profile split at127 can keep its first half and silently reject second except print, while ApplyMotionProfile still returns1. Capacity failure is **defined native behavior**, not M1-034 termination/undefined-memory policy. Path constructor's spare inline slots and heap array initialize only kind words; no blanket zeroed union assumption is established.

### 4. Goal-loop arithmetic and dispatch

This is the F4-disabled single-goal selection route; default F4 is1 and ordinary multi-goal helper uses a different path. Threshold passes are0.01 then1000. Reinitialize found/best for each pass; stop after a complete pass with any accepted goal. Before first accepted candidate, every candidate is collision-tested; afterward only strict distance improvement is tested. Distance is float xyz subtraction and separate square/add in x,y,z order. Do not use double math, omit z, or optimize toward minimum penalty. Penalty strictly<threshold accepts; equality does not. Equal-distance later goals preserve first accepted goal.

Discretization: float x/y times env reciprocal resolution, roundf, signed integer conversion, signed16 packing; yaw float from Rotation3d::GetAngleAroundZaxis, repeated double+/-2pi/narrowing, float angular scale, roundf and unsigned conversion/modulo, byte heading. GetCollisionPenalty output is compared with `bmi` condition; unordered penalty does not qualify. Selected index and best distance change only on accepted penalty. After pass call virtual+2C on selected goal, retain returned code, write selected byte2A. No candidates logs Info/`Planner`/`LatticePlanner.ComputePath.NoValidTarget` / `could not find valid target out of %lu possible targets`, then returns0.

The iteration comparison uses `uxtb(r8)` against full vector count0x004FE97E..0x004FE984, so inputs>=256 can cycle the byte index rather than reach pass end. This is a defined nontermination lead for oversized inputs; no arbitrary failure result or skip policy is invented. Shipped caller maximum goal-count reachability is UNKNOWN here. For finite ordinary counts<=255 the recovered first-tie/threshold behavior above is direct.

### 5. Motion-profile arithmetic in the requested subranges

Keep the caller's two-pass traversal. Reverse pass copies each40-byte source segment and first initializes sign*profile.speed (linear) or turn profile. Let downstream be0 at end/turn boundary, otherwise the already processed reverse-vector speed. Upstream reference is profile.speed except original first segment or previous original point turn uses0 for a multi-segment input; a single input segment uses profile.speed.

Compute separately in float `downstreamSquared`, `upstreamSquared`, `length*-2`, `(downstreamSquared-upstreamSquared)/(length*-2)`. If abs(requiredDecel)<1e-5, keep current speed and assign profile accel/decel. Otherwise if required>profile.decel **and** abs(required-profile.decel)>1, recompute magnitude with native order: length*-2, multiply profile.decel, subtract downstreamSquared, negate, sqrtf; NaN hardware sqrt invokes sqrtf library. Copy direction sign bit from +/-1 conversion, then set profile accel/decel. Point turn uses abs(turnSpeed) with original sign and angular accel/decel; it resets continuity for earlier segments. Temporary vector gets complete adjusted copies; do not combine this reverse propagation with forward splitting.

The requested split subrange is entered only for a line with a positive tail shorter than line and at least5mm different from full length (caller conditions0x00508E10..0x00508E72). `distance=length-tail`, sign copied from current speed. Midpoint: startX+signedDistance*cosf(endHeading), startY+signedDistance*sinf(endHeading), separate float products/adds. Define first line at original current speed with original accel/decel. Define second from midpoint to original end at downstream speed with current sign; if downstream equals0 choose signed20. Append first then second; ignore both failures. Two successive cosf/sinf evaluations occur in defining the halves; do not reuse a host double vector and claim arithmetic identity.

Normal completion calls PrintPath then sets saved return1; unknown kind is detected during reverse pass, warns `IPathPlanner.ApplyMotionProfile.UnknownSegment` / `Path has invalid segment`, saved return0. Temporary vector cleanup ends by moving saved r4 to r0, so unlike DoPlanning the return code here is preserved. Unknown-kind failure leaves previously cleared destination empty because forward output pass has not started. Output capacity exhaustion is not tested and does not change successful return1. The earlier wrappers ignore even this0 result, a distinct caller behavior already M13-033.

### 6. MinimalAngle operand identity and logs

At0x00503E7A compare **computed distance s18 against2.5 in s16**, both float. It branches `ble` to final-heading section when<=. If travel remains, getAbsoluteVal(initialDelta), widen result float to double, compare against exact double0.01 (bits0x3F847AE147AE147B at0x00503DD0); `ble` skips initial turn to straight line. Final section separately computes desired heading minus current cursor heading, gets absolute value, widens to double, compares the same double literal at0x00504270. Equality skips turn; unordered ARM compare does not take `ble`. A direct comparison against a float0.01 would change some boundary outcomes.

Final turn selects-1rad/s only when delta<0, otherwise+1, uses accel/decel10, tolerancefloat0x3D0EFA35, shortesttrue. It appends at current cursor x/y targeting stored desired heading; logs signed delta widened to double at0x005040FC; then assigns desired heading to cursor regardless of append return. Final-position log0x00504174 runs in forced-generation tail regardless of final turn, reads cursor x/y and Radians.getDegrees, widens each to double. Channels are Info/`Unnamed`:

| Call | Key | Format / payload |
|---|---|---|
| 0x00503D62 | MinimalAnglePlanner.Plan.Backup | `%f`, negative backup distance |
| 0x00503F26 | MinimalAnglePlanner.Plan.Turn0 | `%f`, initial signed delta radians |
| 0x00503FF0 | MinimalAnglePlanner.Plan.Straight | `%f`, computed distance |
| 0x005040FC | MinimalAnglePlanner.Plan.Turn1 | `%f`, final signed delta radians |
| 0x00504174 | MinimalAnglePlanner.FinalPosition | `(%f, %f, %fdeg)`, updated cursor |

Force=false early return2 does not run these generation logs. Final log position is not blindly substituted with exact goal translation: cursor moved through TranslateForward, and arithmetic drift is retained.

## Manifest check and remaining boundary

Current M13-034 title is “Planner dependencies still unread: DidObjectsChange, AppendToPath, PathIsSafe/ApplyPathSegment, Path::Append*, xythetaPlan::Append, FindClosestPlanSegmentToPose, the ComputePath goal loop, ApplyMotionProfile sub-ranges, MinimalAngle compares”, status RECOVERABLE_GAP; evidence lists the request addresses through the checked report's Unread. Current M13-033 is IMPLEMENTATION_GAP, title “Planner side of PathComponent: planner construction, SelectPlannerHelper, lattice ComputePath/ComputeNewPathIfNeeded/StartPlanning (both force values, safe-prefix retention), async worker and F0-F8 flags, StopPlanning guard, GetCompletePath, the short planners, IPathPlanner wrappers, slot 0x20/0x18 safety dispatch”; evidence includes the checked false-branch and wrapper reads. Neither was changed.

This report supplies all six requested item bodies and flag-writer inventory for verification. It does not promote source status or implement the result. Important additional boundaries are the bounded retained prefix (not all safe actions), kind-only Path initialization/turn union read, ApplyPathSegment's discarded subthreshold sum, and ignored capacity failures. Existing M13-033 base GetCompletePath correction in the checked report is retained: base internal returns0 without copy when present28==0; this request does not undo it.

Remaining UNKNOWNs are subordinate ApplyAction/polygon internals outside these named bodies, global computed-pointer alias proof beyond the owner scans, shipped reachability of>=256-goal input, and concrete padding contents on cached primitive turns. The standing M1-034 policy covers established terminating/undefined-memory cases, not defined capacity rejection/nontermination or ordinary floating-point formulas. No new departure is proposed. No tests, hardware, native execution, network endpoints, uploads, branches, commits or pushes.

Reproducible local evidence: [disassembler](20261013-planner-dependencies-disasm.py), [bodies](20261013-planner-dependencies-bodies.txt), [extra](20261013-planner-dependencies-extra.txt), [final reads](20261013-planner-dependencies-final.txt), [writer scan script](20261013-planner-dependencies-writers.py), [writer results](20261013-planner-dependencies-writers.txt). Writes confined to research. Request1 complete before starting request2.
