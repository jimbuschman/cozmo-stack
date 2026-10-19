# OV128 — M3/M4 settling residue

Request: `../requests/overnight/20261018-ov128-m3m4-settling-narrow.md`.

Independent extraction, 2026-10-18, starting HEAD b53f2295; pending manager verification.
Read both 20261018 M3/M4 settling passes first. No production or manifest changes.
Primary source: `resources/lib/armeabi-v7a/libcozmoEngine.so`, SHA-256
`02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`.
Evidence below is instructions, ELF relocations and literals. Decompilation was navigation only.
Inline strings, tables and literal pools are not instructions.

## Coverage table (opened before extraction)

| Question | Required coverage | Evidence / disposition |
| --- | --- | --- |
| 1a | ComputeStateAt: all returns, outputs, interpolation, flag | H1–H9, 0x00531784..0x00531AE8; raw query 0x00531430..0x00531648; interpolation 0x0053068C..0x0053085A |
| 1b | GetLastStateWithFrameID: search order, returns, outputs | H10–H11, 0x00531E34..0x00532090 |
| 1c | UpdateFullRobotState / AddRobotStateToHistory: ramp, origin, failures, robot+0x390 | A1–A8; actual AddRobotStateToHistory 0x00513598..0x005135C0 |
| 1d | Compare VisionSystem.cs, Sensors.cs, UnexpectedMovement.cs | Comparison table; also read RobotStateHistory.cs and NativeVisionOnlyHistory.cs |
| 2 | Relative lights: lookup/cast, pose/point, mapping/arithmetic, failures, wire | L1–L8 and angle/permutation tables; Radians comparisons include epsilon |
| 3 | Completion guard identity, writers, callback invoke/skip | C1–C6; action-tag membership function, not a mutable guard byte |
| 4a | SetNextImage ownership and short raw gray/RGB buffers | I1–I6; three exchanged retained buffers |
| 4b | Live raw encoding request/receive reachability beyond M3-050 | I7–I9; receive accepts raw; actual firmware emission UNKNOWN |

## Current manifest claims re-read

The classifications in the inventory tables retain the current owning-record status, not a promotion.
Instruction findings remain unchecked research until the manager adopts them.

| ID | Current title | Status | Current evidence relevant here |
| --- | --- | --- | --- |
| M11-097 | RobotStateHistory query and admission results: ComputeStateAt, GetLastStateWithFrameID and AddRobotStateToHistory's result | RECOVERABLE_GAP | Settling B; prior 0x00531784..0x00531808 and caller 0x00512B9C..0x00512F0C |
| M11-051 | RobotStateHistory::AddRawOdomState gates (drop, consecutive-gap reset, parent check, duplicate key) | IMPLEMENTATION_GAP | 0x00530CF8..0x00530EC4, CullToWindowSize 0x005309D1 |
| M4-019 | CliffSensorComponent: defaults, CliffEvent/PotentialCliff, the EnableStopOnCliff senders, the 50/400/150 schedule with its running stats, HandleRobotStopped's Evaluate and broadcast gate | IMPLEMENTATION_GAP | S3 raw-history walk, C12.4 failure gate; settling B assigns query/admission handoff to M11-097 |
| M4-038 | CliffSensorComponent::ComputeCliffPose's flag gate and result branch, and HandleCliffEvent's AddCliff call and logs (history query M11-097; BlockWorld helpers M11-062; map insert M13-030) | IMPLEMENTATION_GAP | 0x00634980..0x00634A50; manager split 2026-10-18 |
| M4-052 | Relative-mode SetObjectLights: located lookup, ActiveCube cast, SetLEDs and MakeStateRelativeToXY | RECOVERABLE_GAP | Settling A, lookup 0x00637E82, relative call 0x00637EE8 |
| M4-051 | CubeLightComponent::PlayLightAnim's completion callback: stored with the entry, invoked by Update when the entry ends | IMPLEMENTATION_GAP | Store 0x0063858C, completion 0x006379C6..0x00637A48; guard explicitly unread |
| M3-037 | A raw gray/RGB payload shorter than rows*cols (or *3) reads bytes past its end in the engine (undefined); this stack zero-fills the missing bytes | COMPATIBILITY_POLICY | G2 reserve 0x004F1DA6; raw wrappers 0x004F2BC0/0x004F245A; unresolved SetNextImage copy/move question |

## 1. History queries

Layout: raw tree at history+0, vision-only at +0xC. Node key +0x10, value +0x14.
HistRobotState has Pose3d at +0 and RobotState at +0xC: constructor 0x005304E6..0x005304F6
assigns pose and copies 0x5B bytes. Thus frame ID is Hist+0x10/node+0x24, head/lift are
Hist+0x34/+0x38, cliff[0] Hist+0x5C (getter 0x00530566..0x0053056E).

| Step | What the original does | Native citation | Record | Classification |
| --- | --- | --- | --- | --- |
| H1 | ComputeStateAt lower_bounds vision-only at requested t. Exact key wins even without raw history, regardless of flag: outTime=t, pose assignment, all 0x5B state bytes copied, return 0. | 0x00531792..0x005317E6; `cmp r0,r7; bhi`, `str r7,[r8]`, memcpy4 size 0x5B | M11-097 | RECOVERABLE_GAP |
| H2 | Otherwise construct local HistRobotState, call GetRawStateAt with unchanged flag. Nonzero returns after destroying local. Raw-query failure paths write neither time nor caller state. | 0x005317E8..0x00531804, 0x00531AD0..0x00531ADA | M11-097 | RECOVERABLE_GAP |
| H3 | GetRawStateAt needs lower_bound(t)!=end and oldest key<=t. Empty, before-oldest and after-newest return 1, outputs unchanged. Exact raw key sets outTime=t, copies entire HistRobotState, returns 0. | 0x0053143A..0x0053149E; failure `movs r0,#1` 0x00531472 | M11-097 | RECOVERABLE_GAP |
| H4 | Between keys, compare **frame IDs**, before testing flag. Different => 0x06000000 even for flag=false. For flag=true require GetWithRespectTo(nextPose,previousPose,relativePose)==1; failure also 0x06000000. Outputs unchanged. This is not a direct comparison of serialized origin IDs. | 0x005314BA..0x005314CE; 0x005314FE..0x0053150A; diagnostics 0x00531564..0x00531582 / 0x005315A6..0x00531632 | M11-097 | RECOVERABLE_GAP |
| H5 | Flag=false/equal frame: HasSameRootAs(next,previous) must return 1; select nearest by unsigned key distances. **Tie chooses earlier.** Copy entire selected state; outTime=selected key; return 0. Root mismatch returns 0x06000000 unchanged outputs. | 0x00531584..0x005315A4: `cmp r3,r2; bhs 0x531634`; 0x00531634..0x00531648; shared copy 0x0053148A..0x0053149E | M11-097 | RECOVERABLE_GAP |
| H6 | Flag=true: f=f32(u32(t-prevKey))/f32(u32(nextKey-prevKey)); separate conversions, f32 division. Interpolate(previous,next,relative,f); full result copy; outTime=t; return 0. | 0x0053150C..0x00531562: `vcvt.f32.u32`, `vdiv.f32`, Interpolate call | M11-097 | RECOVERABLE_GAP |
| H7 | Interpolate takes base RobotState from previous when f<**0x3EFFFEB0 (0.49998998641967773f)**, next otherwise. Copies 0x5C bytes locally, sets frame to previous.frame. Wheel speeds and head/lift: f32(prev + f32(next-prev)*f), individual instructions, no double arithmetic. Initializes both cliff words to 0xFFFFFFFF, replaces **only cliff[0]** with u16(u32(roundf(f32(prevCliff0 + f32(s32(nextCliff0-prevCliff0))*f)))). Other fields retain base values, including timestamp and serialized pose bytes. | 0x00530698..0x00530766; threshold literal 0x00530884; `strd` 0x005306EE, `strh` 0x00530734 | M11-097; overlap M11-052 | RECOVERABLE_GAP |
| H8 | Separate interpolated Pose3d: position=previous pose translation + f*relative pose translation, all three coordinates f32; Z angle=previous Z angle + f*relative Z angle through Radians operators. Construct about Z_AXIS_3D with previous parent. Not quaternion slerp or direct world-position blend. | 0x0053076A..0x00530848; loops 0x00530790..0x005307BE; angle multiply/add 0x005307E0..0x005307F4; parent 0x00530800..0x00530804 | M11-097; overlap M11-052 | RECOVERABLE_GAP |
| H9 | ComputeStateAt selects last vision-only V at/before raw outTime (exact, predecessor, or newest if lower_bound=end). No such V or V.frame<=raw.frame: copy whole raw state, return 0. Otherwise apply correction chain below. Corrected path writes **only output Pose3d, frame from raw, head/lift from raw**; all other caller state fields stay unchanged. outTime remains successful raw-query time. | 0x00531808..0x005318D6; earliest-key escape 0x00531ADE..0x00531AE8; corrected writes 0x00531AB2..0x00531AC6, return 0 0x00531ACE | M11-097 | RECOVERABLE_GAP |
| H10 | GetLastStateWithFrameID fails immediately if **raw tree** empty, even with vision-only entries. Search raw newest-to-oldest, first matching frame wins; stop as soon as frame<requested. If none, same reverse search in vision-only. Raw wins even over a newer matching vision state. | 0x00531E3C..0x00531E42; raw 0x00531E46..0x00531EB2; vision 0x00531EB4..0x00531F22 | M11-097 | RECOVERABLE_GAP |
| H11 | Success assigns selected pose and copies all 0x5B RobotState bytes, returns 0. Empty/not-found logs error, sets _errG, optional debug break, returns 1, output state unchanged. No timestamp-reference output exists on this method. | success 0x00531F88..0x00531FB8; empty 0x00531F46..0x00531F86; missing 0x00532032..0x00532090 | M11-097 | RECOVERABLE_GAP |

H9 correction, preserving native call order (R=raw query result, V=selected vision-only):

1. A=raw.lower_bound(V.key). Construct identity/empty-name pose D (0x00531896..0x005318F2).
2. If A.frame==R.frame, express R.pose relative to A.pose.parent into D; then
   D.transform *= inverse(A.pose). GetWithRespectTo's return is **not tested** here
   (0x00531904..0x0053197A).
3. Otherwise walk successors from A. On a frame increase relative to saved boundary B,
   take predecessor P of candidate, express P.pose in B.pose.parent, multiply by inverse(B.pose),
   PreComposeWith that delta into D; update B to P's successor. Stop on R.frame or raw.end
   (0x0053197C..0x00531A80; relative/inverse 0x005319F8..0x00531A2C,
   PreComposeWith 0x00531A36..0x00531A48, successor 0x00531A4C..0x00531A76).
4. D.transform *= V.pose.transform; D.parent=V.pose.parent; assign D and only the three
   raw fields stated in H9 (0x00531A82..0x00531AC6).

This is a call-level pose composition inventory; existing Pose3d/Transform3d helper internals
retain their own evidence obligations. No extra mismatch failure code is invented for ignored returns.

### Admission into robot+0x390

The request's 0x00512B9C..0x00512F0C is **UpdateFullRobotState**, not AddRobotStateToHistory's body.
0x00512C38 is executable `movs r6,#1`.

| Step | What the original does / effect on history | Native citation | Record | Classification |
| --- | --- | --- | --- | --- |
| A1 | Committed OnTreads-boundary change clears +0x2C0, Delocalizes, jumps straight to gyro/stats tail; no raw add for this state. Delocalize's other effects stay with its existing owner. | 0x00512B94..0x00512BAA → 0x00512FB4 | M11-097 / M11-053 | RECOVERABLE_GAP |
| A2 | Construct empty-name Pose3d. Nonzero robot+0x316 goes directly to AddRobotStateToHistory with it, skipping origin lookup/input pose construction. This branch alone does not prove a Ramp can be constructed: retain OV127-check's checked finding that SetOnRamp cannot reach true in this build. | 0x00512BAC..0x00512BD0; 0x00512BE2..0x00512BEE; reachability: ov127-check BA/BB, 0x005164FA..0x005165F2 | M11-097 | RECOVERABLE_GAP |
| A3 | Normal: ContainsOriginID(state+8) false => warning, r6=1, no history mutation/add, no gyro/cliff-stat tail. True => resolve origin, construct incoming position/yaw pose, flatten to root. | 0x00512C3E..0x00512CD4; rejection 0x00512EC4..0x00512F12; tail gate 0x00512FB0..0x00512FB2 | M11-097 | RECOVERABLE_GAP |
| A4 | Matching frame: take z from current robot pose w.r.t. root. Mismatched: GetLastStateWithFrameID(incoming frame); failure goes to common failure without add; success uses that state's flattened z. Set translation=(incoming x,incoming y,selected z), branch to same add. | 0x00512CD8..0x00512D02; lookup/failure 0x0051307A..0x00513110; z/store/add 0x005130EE..0x0051313C | M11-097 | RECOVERABLE_GAP |
| A5 | Actual AddRobotStateToHistory constructs HistRobotState(pose,state); calls AddRawOdomState(state.timestamp,hist) on robot+0x390; returns actual result unchanged. | 0x00513598..0x005135C0; timestamp/history loads 0x005135A8/0x005135AA, call 0x005135B0 | M11-097 | RECOVERABLE_GAP |
| A6 | AddRawOdomState returns 1/no insertion for stale timestamp before newest-window, excessive unsigned delta from newest, parented pose with non-root parent, duplicate key. Excessive delta increments static u8 0x01059ED4: first five reject; sixth clears **raw tree only**, resets counter, still rejects. In-range delta resets counter before parent/emplace checks. | stale 0x00530D2C..0x00530DAC; delta/counter 0x00530DB2..0x00530E80; parent 0x00530E82..0x00530EA4 and 0x00530F00..0x00530F7C; duplicate 0x00530EB0..0x00530EFE | M11-097 / M11-051 | RECOVERABLE_GAP / IMPLEMENTATION_GAP |
| A7 | Successful unique emplace: CullToWindowSize then return 0. If raw count<2 or newest<window, cull returns. Otherwise erase raw/vision-only/computed keys below newest-window and associated key/timestamp-index prefixes. No per-origin purge. | 0x00530EB4..0x00530EC2; cull 0x005309D0..0x00530C38, trees +0/+0xC/+0x18 and indices +0x24/+0x30 | M11-097 / M11-051 | RECOVERABLE_GAP / IMPLEMENTATION_GAP |
| A8 | Nonzero add warns, sets r6=1, retains result, skips current-pose update/schedule/gyro/cliff-stat tail. Zero add calls UpdateCurrPoseFromHistory; frame match and first 100 mismatches run tail, 101st Delocalizes and skips. Current-pose update's result is retained separately; do not invent an admission success. | 0x00512BF2..0x00512C3C; 0x00512D1C..0x00512EB6; 0x00512F14..0x00512FA6; 0x00512FB0..0x00512FD6 | M11-097 / M4-019 | RECOVERABLE_GAP / IMPLEMENTATION_GAP |

A6's global is `ReliableConnection::sConnectionTimeoutInMS`: GOT 0x0103EA54, load
0x00530D3A..0x00530D48, initial ELF f64 **5000.0** at 0x01051F48. Code clamps negative to 0,
high to 4294967295.0; for positive values adds 0.5, floor/converts u32
(0x00530D4C..0x00530DB6, literals 0x00530FD0/0x00530FD8). It remains a runtime global;
complete writer census is UNKNOWN in this narrow report, so immutable 5000 is not claimed.

### Stack comparison at starting HEAD

| Path | Current behavior | Difference |
| --- | --- | --- |
| VisionSystem.cs:695–719 | Treads Delocalize, OriginAccepted, History.Add(s) | Void Add; no admission result/frame query/selected-z propagation before downstream stats |
| Vision/RobotStateHistory.cs:36–70 | Purges other-origin entries, appends duplicates, void result | Native keyed rejection, gap counter/reset, parent check absent; Entry also omits frame, wheel and cliff fields |
| RobotStateHistory.cs:97–122 | At nearest, tie later, outside interval accepted | Native requires bracket, tie earlier for flag=false, reports frame/root failure; At is not ComputeStateAt |
| RobotStateHistory.cs:159–188 | ComputeAndInsert uses double fraction and nearest raw pose stand-in | No native interpolation/mismatch result/vision-first query/correction chain. NativeVisionOnlyHistory.cs now supplies vision entries: old comments saying none exist cannot justify ignoring them |
| Sensors.cs:978–985 | Adds every synced cliff sample before origin/treads/admission checks | Native raw map excludes rejected/duplicate and treads-change samples; treads-change still runs current-sample stats tail |
| Sensors.cs:824–836 | Production cliff history MISSING; test-only historical input | Cannot place production cliff through H1–H9; correct handoff is M11-097 |
| Sensors.cs:1050 onward | Origin/frame-count stats gate; add/query failures comment-only | A4/A8 can suppress gyro and cliff stats |
| UnexpectedMovement.cs:167–170,262–299 | Optional ComputeStateAt delegate, no assignment found in src; absent => missing/Unknown side | Semantic enum is adapter, not native numeric values; raw nearest lookup alone does not satisfy query |

These feed robot threshold sends, cliff/world placement, image-pose consumers and movement responses.
No “no effect” claim or production coverage from tests substituting historical poses.

## 2. Relative-mode SetObjectLights

| Step | What the original does | Native citation | Record | Classification |
| --- | --- | --- | --- | --- |
| L1 | ObjectLights+0x74 is mode. Nonzero: located lookup(id,-1), dynamic_cast ObservableObject→ActiveCube. Zero: connected ActiveObject lookup/cast. Lookup/cast null: NullActiveObject Info, result 0x04000000, no SetLEDs/send. | 0x00637E6C..0x00637EB0; failure 0x00637EFC..0x00637F38 | M4-052 | RECOVERABLE_GAP |
| L2 | Located helper includes requested ID, family=-1 leaves family filter empty, no custom callback. FindLocatedObjectHelper(...,true) returns first eligible object or null; default origin mode selects current world origin. | 0x0061F402..0x0061F488, zero flags 0x0061F456; copied flags 0x005C6CE8..0x005C6CEC; origin 0x0061EBF0..0x0061EC26; ID 0x0061ED08..0x0061ED4A; first/null 0x0061ED82..0x0061ED8E,0x0061EEB0..0x0061EF20 | M4-052 | RECOVERABLE_GAP |
| L3 | SetLEDs on located cube's ActiveObject subobject (+0xA4), passing arrays from ObjectLights+0x00..+0x60. Normalizes zero on/off duration cases, sets gamma 0x80. Then MakeStateRelativeToXY with **ObjectLights+0x78 point**, mode +0x74. | 0x00637EB2..0x00637EE8; SetLEDs 0x004E48B0..0x004E49A8 | M4-052 | RECOVERABLE_GAP |
| L4 | Mode0 returns unchanged. Mode1 gets closest corner; mode2 closest face. Other mode: error, _errG/optional break, unchanged return; caller still SetLights. Unexpected LED mask likewise logs and returns without rotation, does not suppress send. | 0x004E0DF4..0x004E0E18; invalid 0x004E0E42..0x004E0E9C; unexpected 0x004E0EAC..0x004E100E; continuation 0x00637EEC..0x00637EF8 | M4-052 | RECOVERABLE_GAP |
| L5 | Corner/face use top marker pose expressed w.r.t. root, its XY/yaw and supplied XY point. GetTopMarker maximizes normal·worldZ, strict greater-than (ties keep earlier). Both compare selected marker code with GetMarker(face=4); mismatch warns and returns default corner6/face4, not failure to caller. | top marker 0x004E69B8..0x004E6A66, seed -FLT_MAX at 0x004E6A7C; flatten 0x004DF628..0x004DF6DC; corner 0x004E118A..0x004E11D8,0x004E15CA..0x004E1608; face 0x004E20F8..0x004E2128,0x004E254A..0x004E2588 | M4-052 | RECOVERABLE_GAP |
| L6 | Corner computes f32 delta=point-marker.xy, direction=Radians(atan2f(dy,dx)), subtracts marker yaw. Face computes yaw-direction then unary-negates/assigns, i.e. direction-minus-yaw with normalization at each operator. Choices below use **Radians epsilon comparisons**, not plain float intervals or round-to-quarter-turn. | corner 0x004E11A4..0x004E11C8,0x004E13BC..0x004E1410,0x004E160A..0x004E1622; face 0x004E22CA..0x004E2390,0x004E258A..0x004E25BE,0x004E2776..0x004E27AA; comparison helpers below | M4-052 | RECOVERABLE_GAP |
| L7 | RotatePatternAroundTopFace moves all seven 32-bit words of each LED (stride 0x1C), not just colors. true source→destination=[1,2,3,0]; false=[3,0,1,2]. Two true calls compose half-turn. | 0x004E30D2..0x004E3148; true table 0x00C45B94=01 02 03 00; false 0x00C45B90=03 00 01 02 | M4-052 | RECOVERABLE_GAP |
| L8 | SetLights reads resulting located ActiveObject state, packs LEDs in order; SetCubeGamma if changed, then CubeID, then CubeLights; reliable/non-hot. CubeID uses that object's active ID, no second connected lookup. Gamma/ID send results ignored; result is final CubeLights send result. | veneer 0x008CCCDC→PLT0x004B9294→0x0063A604; pack 0x0063A63E..0x0063A75C; gamma 0x0063A760..0x0063A792; ID 0x0063A794..0x0063A7D8; lights/result 0x0063A7DC..0x0063A824 | M4-052 / M4-018 | RECOVERABLE_GAP |

Use the actual comparison operators to preserve boundaries. Let norm be Radians::rescale,
e=f32(0x3727C5AC)=9.999999747378752e-6, and:

- Near(a,b) = abs(norm(norm(a)-b)) < abs(e), **strict** (0x0084CC0A..0x0084CC5C).
- GT(a,b) first checks f32(a-b)>0, then !Near(a,b) (0x0084CC90..0x0084CCDA).
- GE(a,b) = GT(a,b) || Near(a,b) (0x0084CCDC..0x0084CD10).
- LT(a,b) = GT(b,a), 0x0084CD12..0x0084CD18, veneer0x008D152C→PLT0x004A4528.

Normalization: 0x0084C87C..0x0084C936. Enabled (-pi,pi] values pass; magnitudes <10 outside
range repeatedly add/subtract f32 2pi; larger values subtract f32(s32(ceilf(f32(a/2pi-0.5))))*2pi.
Arithmetic operators normalize: constructor 0x0084C832..0x0084C842, subtract
0x0084CA18..0x0084CA32, subtract-assign/unary minus 0x0084CAD4..0x0084CB26.
Do not replace this with double modulo or ordinary <,>,>=. Circular Near also matters across ±pi.

Corner table, a=normalized direction-minus-yaw, p=f32(pi/2)=0x3FC90FDB, first matching branch:

| Native condition | Mask / logged label | Citation |
| --- | --- | --- |
| GT(a,0) and LT(a,p) | 3 / Back Left | tests 0x004E13EA..0x004E1410, return 0x004E15C6 |
| GT(a,0) and not LT(a,p) | 6 / Front Left | 0x004E1410→0x004E17DE, shared return0x004E1990 |
| not GT(a,0) and GT(a,-p) | 9 / Back Right | 0x004E160A..0x004E1622, return0x004E17DA |
| remaining | 12 / Front Right | 0x004E1622→0x004E19A2, return0x004E1B54 |

Face table, q=0x3F490FDB, r=0x4016CBE4, first matching branch:

| Native condition | Mask | Citation |
| --- | --- | --- |
| LT(a,q) and GE(a,-q) | 1 / Back | 0x004E235A..0x004E2390, return0x004E2546 |
| LT(a,r) and GE(a,q) | 2 | 0x004E258A..0x004E25BE, return0x004E2772 |
| LT(a,-q) and GE(a,-r) | 8 | 0x004E2776..0x004E27AA, return0x004E2960 |
| remaining | 4 | 0x004E2964..0x004E2B16 |

These tables intentionally preserve helper predicates, including fuzzy boundaries, rather than
claiming simple real-number intervals. atan2f's phone-libm exceptional-value/rounding behavior is
not measured here; no guessed special NaN/zero-vector quadrant.

WhichCubeLEDs switch is raw table data at 0x004E0E1C (TBH branch base same address):

| Mask | Rotation |
| --- | --- |
| 1,3 | true twice:0x004E0E34 then0x004E0E3C |
| 2,6 | true once:0x004E0E3C |
| 4,12 | unchanged:0x004E100C |
| 8,9 | false once:0x004E0E9E (veneer0x008CA8AC→PLT0x004A4444) |
| 5,7,10,11; outside1..12 | unexpected-LED diagnostic, unchanged |

SetLEDs zero-duration normalization (0x004E4902..0x004E49A2): both zero => both colors zero,
both durations INT_MAX; on zero only => on color=off color, on duration INT_MAX; off zero only
=> off color=on color, off duration INT_MAX. Transition fields/offset retained; gamma=0x80.

Current Lights.cs:1530–1531 immediately returns 0x04000000 for relative mode, sending nothing.
M4-052's successful path is missing. This changes retained LED state and CubeLights payloads;
there is no no-effect exclusion.

## 3. Completion callback guard

| Step | What the original does | Native citation | Record | Classification |
| --- | --- | --- | --- | --- |
| C1 | 0x00564A64 is a Thumb **function**; r0 is action tag. Lower_bounds IActionRunner::sInUseTagSet, returns1 iff exact member, else0. Set object0x0105A8F4 via GOT0x0103EB1C. No boolean at0x564A64. | 0x00564A64..0x00564ABC; compare0x00564A78, exactness0x00564A9E..0x00564AA2; return0/1 0x00564AB4..0x00564ABC | M4-051 | IMPLEMENTATION_GAP |
| C2 | Generic PlayLightAnim initializes tag0, stores callback and pushes entry copy. Callback node+0x28, tag node+0x48. | zero sp+0x98 at0x0063857C; callback0x0063858C; copy value+0x38..+0x40 to node+0x40..+0x48 at0x00638D0C..0x00638D18 | M4-051 | IMPLEMENTATION_GAP |
| C3 | PlayLightAnimFromAction calls generic layer0/no modifier; only when returned true looks up object/current entry and stores caller's tag at node+0x48. Generic callers keep0. | 0x00638220..0x00638290; `ldr r1,[r4]; str r1,[r0,#0x48]` 0x0063828E..0x00638290 | M4-051 | IMPLEMENTATION_GAP |
| C4 | IActionRunner ctor uniquely inserts generated internal tag, retries on collision. SetTag erases prior external tag if different from internal, inserts nonzero new tag and stores only on success. Destructor erases both external/current and internal tags. These mutate action-lifetime membership, not connectivity. | ctor0x0053FE34..0x0053FEC8, insert0x0053FE72; SetTag0x00540102..0x00540190, erase0x00540118/insert0x0054012C; destructor0x00541104/0x00541114 | M4-051; action owner retained | IMPLEMENTATION_GAP |
| C5 | At end or stop, copy callback/tag then **pop entry**. Empty callback skips. Nonempty and tag0 invokes. Nonempty/nonzero tag invokes iff C1 returns exactly1; missing tag skips. Both completion paths use same predicate. | end/stop0x00637998..0x006379BC; copy/pop/check/call0x006379C6..0x006379F4 and0x00637A02..0x00637A2E | M4-051 | IMPLEMENTATION_GAP |
| C6 | Destroy copied function after invoke/skip. A callback may alter lights only after its old entry was popped; dead action suppresses callback but not pop. | 0x006379F8..0x00637A48 | M4-051 | IMPLEMENTATION_GAP |

Who writes the guard: nobody writes instructions at0x00564A64. Relevant writes are the entry tag
(C2/C3) and process-wide set mutations (C4); static set construction/destruction is C++ lifetime
machinery. No separate mutable guard flag. Native action callback bodies remain their owners'
scope. This affects callback delivery/downstream state, so no no-effect claim.

## 4. Short raw images and ownership

| Step | What the original does | Native citation | Record | Classification |
| --- | --- | --- | --- | --- |
| I1 | Receive handler calls AddChunk on mutable robot+0x394 image, then SetNextImage on completion (first two completed images per engine-event time). No raw-encoding rejection here. | 0x00535A7A..0x00535A9A;0x00535B0E..0x00535B9A | M3-037 | COMPATIBILITY_POLICY, rationale under review |
| I2 | New image requires resolution4, sets320x240, end=begin, reserve0x38400=230400. Append valid chunks; no geometry-vs-total check or tail zero fill. Constructor zeroes vector pointers, not future allocation. | ctor0x004F1A7E..0x004F1A94;0x004F1D4A..0x004F1DAE; append0x004F1EAE..0x004F1EC0 | M3-037 | COMPATIBILITY_POLICY, rationale under review |
| I3 | Initialized/enabled/time/calibration/history gates can leave input vector untouched. Calibration/dot-test path decodes incoming image before exchange. Synchronous mode passes incoming image directly to UpdateVisionSystem, retaining vector. | 0x00652B1C..0x00652CE2; query0x00652C44..0x00652C54/failure0x00652DD6..0x00652E5C; early decode0x00652E5E..0x00652E70; synchronous0x0065303A..0x00653050 | M3-037 | COMPATIBILITY_POLICY, rationale under review |
| I4 | Asynchronous SetNextImage **exchanges** incoming vector triple and old pending triple (VisionComponent+0x74), mutex+0x4C. Save old pending, null it, install incoming, null incoming, restore old pending triple to incoming. Also exchanges six metadata words(+0xC..+0x23), then input+0x10 := new pending+0x10. No payload memcpy/allocation here. | 0x00653056..0x0065314E; save0x006530CC..0x006530E8; transfer0x006530FE..0x00653116; restore0x00653128..0x00653148 | M3-037 | COMPATIBILITY_POLICY, rationale under review |
| I5 | Processor exchanges pending+0x74 with processing+0x50; decodes processing by const reference. Afterward resets processing end=begin and pending end=begin under mutex, retaining bytes/capacity. Thus **three recycled slots**: receiver/pending/processing. Receiver's old tail depends on exchanges/scheduling, not always immediately previous frame. | exchange0x0065200E..0x00652090; process/clear0x00652218..0x00652252; UpdateVisionSystem0x00653D28..0x00653D34; decode0x006B4B7A..0x006B4C82 | M3-037 | COMPATIBILITY_POLICY, rationale under review |
| I6 | Raw gray wraps data as geometry-sized Image and CopyTo; raw RGB wraps ImageRGB and CopyTo. Vector length/end not passed. Short payload produces no automatic zero-padding/length failure. Previously written retained tail bytes survive; never-written tail contents UNKNOWN. | gray0x004F2BC0..0x004F2BD0; RGB0x004F245A..0x004F246A; dispatch tables0x004F289C/0x004F21B2 select encodings1/2 | M3-037 | COMPATIBILITY_POLICY, rationale under review |
| I7 | ImageRequest serializes two bytes(mode,resolution), no encoding selector. Connection requests{1,4}; SetRobotImageSendMode forwards mode/resolution. Neither explicitly selects raw gray/RGB. | Pack0x007A3BDA..0x007A3C0E; connection0x005152F0..0x00515308; helper0x004EDE4C..0x004EDEA0 | M3-037 / camera records | COMPATIBILITY_POLICY |
| I8 | Live receiver accepts raw if supplied: AddChunk copies encoding chunk+0xC→image+0x20; only encoding8/first-byte color indicator changes to9. Values1/2 pass through to decode. No request-choice gate rejects raw before vision. | 0x004F1D7A..0x004F1D9C; receiver0x00535A84..0x00535A96; decode dispatch0x006B4B7A..0x006B4C06 | M3-037 | COMPATIBILITY_POLICY; unreachability unproven |
| I9 | Actual firmware emission of raw/short raw in response to mode/resolution/color settings is **UNKNOWN**. Request fields and receiver support cannot prove firmware producer behavior. M3-050's observed encoding8 alone cannot establish universal unreachability. | Boundary: two-byte request above; received encoding0x004F1D7A; firmware producer absent from this path | M3-037 / M3-050 | HARDWARE_ONLY boundary, no manifest edit |

The 2026-09-30 ownership question is answered: exchange, not payload copy. Dropped frames keep receiver
allocation; exchanged frames return old pending allocation, which may have come from processor.
For nonempty short payload n<N, new prefix[0,n) is current data; [n,N) is whatever was last written in
that allocation. N=76800 gray or230400 RGB. Previously written bytes can be traced through history;
never-written bytes remain UNKNOWN. The returned recycled vector may already have end=begin;
clearing its used length does not zero its allocation.

Completely empty first chunk also encounters unconditional data[0] at0x004F1D82..0x004F1D86 before
this decode question; no invented empty-payload zero-fill case. These instruction-level memory
observations are not a portable C++ guarantee for out-of-live-vector-element reads. Do not silently
change either M3-037 policy or M1-034 standing fault treatment on this unchecked report.

Current EncodedImageDecode.cs:33–36/:70–73 and CopyRaw:156–160 allocate new zeroed required-length
array, copy min(payload.Length,need). This differs when retained native bytes are nonzero.
Geometry-sized wrappers ignore extra payload bytes: long truncation has separate source support.

## Findings and remaining uncertainty

- M3-037's current rationale is too broad (current title/status/evidence quoted above). SetNextImage
  does not discard every old allocation. Replace the unresolved ownership question with I1–I9 after
  checking; firmware raw-path exclusion remains unproven.
- M11-097/M4-052 remain recoverable gaps pending adoption/build; M4-051 tag/set identity is recovered.
  M4-019/M4-038 keep their production handoffs. Reconcile interpolation with M11-052 rather than
  creating independent divergent implementations. No manifest promotion here.
- No no-effect exclusion for these four questions: they affect traffic, callbacks, pixels or state
  feeding supported consumers. Internal buffer mechanism is free only while observable results agree.
- UNKNOWN: firmware raw/short emission; never-written allocation bytes; phone atan2f exceptional-value
  and rounding behavior not measured; complete connection-timeout writer census. Existing pose-helper
  internals and action callback bodies retain their own owners, not newly claimed complete.
- Suggested later regressions from these rows: bracket failures leave outputs unchanged; exact vision
  priority; frame mismatch with flag=false; nearest tie earlier; f near0.49999; cliff[1..3]=0xFFFF;
  corrected-result sentinel fields; raw-before-vision frame priority; duplicate/gap/origin/treads
  admission; angle epsilon boundaries and all mask permutations of complete LED words; callback
  empty/tag0/live/deleted with pop-before-call; short raw after each buffer exchange. **Proposed,
  not executed production tests.**
- Validation: full cited native ranges read, relocations/literals resolved, stack compared and report
  whitespace checked. No production changes, inventory edits or hardware runs.
