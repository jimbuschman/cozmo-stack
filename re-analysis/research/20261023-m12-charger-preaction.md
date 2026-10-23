| Item / step | Coverage | What the original does | Address with 1–3 pasted instructions / earlier checked row | Record |
| --- | --- | --- | --- | --- |
| 1a. Clear and dispatch | CHECKED | Clear the supplied vector first; unsigned actionType>1 returns empty. Types0/1 each append exactly one element. Does not populate all six vectors in one call. | `0x004E9FC8: str r1,[r4,#4]`; `0x004E9FD4: cmp r5,#1`; `0x004E9FD6: bhi.w #0x4ea120`. | M12-001 / M13-009 |
| 1b. Cache selection/order | CHECKED | Caller selects cache this+0x28+12*type; ascending requested-type order; empty→virtual generate→append copied elements. Type0 cache+0x28, type1+0x34; other four remain empty when generated. | Reuse G Q2.1/Q2.9 checked by GC: `0x004DF8BA: blx r3`; `0x004DF8BE: mov.w sb,#1`; `0x004DF8C8: blx #0x4a4138`. | M12-001 |
| 2a. Static offset source | CHECKED | File-static Pose2d0x01059148 initialised with Radians(+0), x+0, y250f (0x437A0000). | `0x004D6BD6: movt r3,#0x437a`; `0x004D6BDE: movs r2,#0`; `0x004D6BE2: blx #0x4a4a2c`. Reuse old N15 for geometry; confirm initializer binding. | M13-009 |
| 2b. Marker identity | CHECKED | Use exact stored KnownMarker pointer at Charger+0xFC, originally AddMarker(code2,pose,size27×20) result; no marker iteration or observation filter. | `0x004E9C38: blx #0x4a4798`; `0x004E9C3C: str.w r0,[r4,#0xfc]`; `0x004EA036: ldr r5,[sl,#0xfc]!`. | M13-009 / M12-001 |
| 2c. Temporary local pose | CHECKED | Radians(F(p.angle+pi/2_f)), Z axis, translation(p.x,−p.y,−15.5f), immediate parent marker+4. | `0x004E9FF2: vadd.f32 s0,s2,s0`; `0x004EA04A: strd r0,r8,[sp]`; `0x004EA058: blx #0x4a47e0`. | M13-009 |
| 2d. Actual element constructor/scalar | CHECKED | Pose overload0x0050DCFC via PLT0x004A47F8, arguments(type,marker,&temporaryPose,+0.0f). Not distance overload0x0050DB14; not75/40/250. | `0x004EA0E8: movs r3,#0`; `0x004EA0FA: str r3,[sp]`; `0x004EA0FE: blx #0x4a47f8`. | M12-001 |
| 2e. Allocating append | CHECKED | Same ctor/type/marker/pose; int0 is converted to f32+0, one element published. No different scalar on zero capacity. | `0x004EA6F4: vcvt.f32.s32 s0,s0`; `0x004EA704: vstr s0,[sp]`; `0x004EA708: blx #0x4a47f8`. | M12-001 |
| 2f. Stored element parent/fields | CHECKED | Type+0, marker+4, independent pose+8 rerooted to marker pose's parent; scalar+0x18=0; name PreActionPose; calculate height tolerance+0x14. | `0x0050DD12: strd r1,r7,[r5]`; `0x0050DD46: vstr s16,[r5,#0x18]`; `0x0050DDB2: blx #0x4a40fc`. Reuse O “pose ctor” and writer corrections. | M12-001 / M12-038 |
| 3a. Matching ChargerGeometry inputs | CHECKED | Types0/1 count,250f,pi/2_f,−15.5f,marker−pi/2_f/(86,0,22), docked pi_f/(30,0,0) match declared input values. | Native0x004E9FE4 literal0x3FC90FDB; `0x004EA01A: movt r0,#0xc178`; code Vision/ChargerGeometry.cs:59–65,88–90,123–124,135–138; detailed comparison below. | M13-009 |
| 3b. Differences from production cache | CHECKED | Geometry helper returns flattened Pose3d values, no stored type/marker/scalars; actual GenerateStored currently emits MISSING and empty list for Charger_Basic. Parent/frame/arithmetic equality is not established by matching constants. | Native constructor0x004EA0FE and parent0x004EA04A above; code Manipulation/PreActionPose.cs:233–237; Vision/Geometry.cs:327–328. | M12-001 / M13-009 / M13-040 |
| 4. Prior coverage reused | CHECKED | Reuse shared ctor, tolerance, marker-parent, cache and docked-pose rows; newly read bounded charger append/arguments and slow-path scalar forwarding. | O ctor0x0050DCFC..0x0050DE48; GC Q2.1/Q2.9; T OV45 AddMarker parent0x00876860..62/docked0x004EA202; L1–L2/LC. | Same owners; no settlement |

# R-M12-CHARGER-PREACTION — research queue row61

Answers [the request](requests/20261023-m12-charger-preaction.md). Job1 was finished, committed and pushed as393bc407 **before** starting this job; fresh `git pull --rebase` reported up to date. Only this report is written/committed for Job2. Explicit operator publication instruction overrides the research README's usual no-commit/no-push rule. CHECKED is scoped research coverage, not an EXACT_SOURCE promotion or a whole docking implementation claim.

Binary `resources/lib/armeabi-v7a/libcozmoEngine.so`3.4.0-1204, SHA256 `02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`. Native Capstone instructions and ELF symbol/PLT relocations are the evidence, never decompiled text. Ran `known.py` before checking0x004E9FB0,0x0050DB14,0x0050DCFC,0x004D6BC4,0x004EA682,0x004E9B6C,0x004EA1A0,0x004DF850. known.py/index boundaries: generator0x004E9FB0/374 bytes; distance ctor0x0050DB14/86; pose ctor0x0050DCFC/336; initializer0x004D6BC4/54; allocating append0x004EA682/164; Charger ctor variant0x004E9B6C/224; docked helper0x004EA1A0/248; collection0x004DF850/1420. Symbol Thumb bits are cleared to get these instruction addresses, not used to guess ends. This avoids conflating the ctor's adjacent variants or its literal/EH tails with another routine.

## Prior checked coverage and narrow boundary

Paths below relative to `re-analysis/research/`:

- **G/GC:** [20260929-R-VIS-M12-gap3-extraction.md](20260929-R-VIS-M12-gap3-extraction.md) Q2.1/Q2.9/Q2.10, checked in [20260929-R-VIS-verify-M12-gap3.md](20260929-R-VIS-verify-M12-gap3.md), including corrected empty-request and world-offset formula. Reuse collection/cache selection; no second whole collection extraction.
- **O:** [overnight/20261014-ov04-m12-038-check.md](overnight/20261014-ov04-m12-038-check.md), “Omissions (now read; M12-001's ctor bodies)”, **pose ctor**, distance ctor, derived ctor and MakeUnitLength; “Corrections” supplies exact height writer. This is the checked authority for shared constructors, not the cube generator's different argument constants.
- **T:** [overnight/20261015-ov45-47-check.md](overnight/20261015-ov45-47-check.md), OV45: AddMarker SetParent0x00876860..0x00876862; GetWithRespectTo output parent; charger GetRobotDockedPose0x004EA202. Reuse checked marker-parent/docked-pose primitives.
- **L/LC:** [20261022-m13-017-groundwork.md](20261022-m13-017-groundwork.md) L1–L2, adopted by [20261023-m13-017-groundwork-tests-claude-check.md](20261023-m13-017-groundwork-tests-claude-check.md), with [20261023-b-m13-17-claude-check.md](20261023-b-m13-17-claude-check.md). SetPose clears six caches; empty vectors regenerate; charger element adapter explicitly missing. [20261021-m12-predock-cache-lifetime.md](20261021-m12-predock-cache-lifetime.md) source trace4–6 is supplementary copied-output/parent context; it does not settle all generic lifetime effects.
- **N13–N15:** [20260927-X2-M13-navigation-extraction.md](20260927-X2-M13-navigation-extraction.md). Earlier charger dimensions/marker, docked and pre-dock geometry leads, also quoted in current M13-009. No independent check specifically adopting the **complete generator append/scalar** was found. This job supplies those direct instructions; it does not silently upgrade the old bare extraction to an independent check. Already checked docked/marker-parent parts above are reused.

The 374-byte generator has no action-specific geometry loop, no cube face table, no sb rotation loop, no RNG, no robot traffic and no validity filter of its own. Existing downstream collection/world constructor/validity remain their own paths. Its supported local output is a single stored element, not an already filtered world goal.

## 1. Supplied vector, cache destination and order

Generator arguments on entry are `r0=this Charger`, `r1=actionType`, `r2=vector<PreActionPose>&`. It clears **that supplied vector** by walking its end backward, storing end−0x1C then destroying embedded pose at oldEnd−0x14 (element+8),0x004E9FC2..0x004E9FD2. Capacity is retained. After the clear it compares type unsigned to1. Types2,3,4,5 and every larger/negative-as-uint representation return empty. There is no “always append a docking pose” fallback.

Normal caller GC Q2.1 selects the vector at actionable-this+0x28+0xC*type before invoking virtual+8; same dispatch applies to Charger. Concrete vtable identity is already identified by LC's0x004DF8BA→0x004E9FB0 join. The generator itself does not calculate the cache offset or mutate the other five vectors.

| Requested type | Name in M12-001 / current enum | Normal selected cache vector | Elements after this generator | Element.type | Order |
| --- | --- | --- | --- | --- | --- |
| 0 | Docking | +0x28 | 1 | 0 | The single charger-marker element |
| 1 | PlaceRelative | +0x34 | 1 | 1 | The same geometry/scalar, retaining type1 |
| 2 | PlaceOnGround | +0x40 | 0 | — | — |
| 3 | Entry | +0x4C | 0 | — | — |
| 4 | Rolling | +0x58 | 0 | — | — |
| 5 | Flipping | +0x64 | 0 | — | — |

The six vector headers occupy+0x28 through exclusive+0x70. Ascending requested ActionType order in GC means requesting {0,1} collects type0 then type1. It does **not** mean a single type0 call fills both caches. A repeatedly requested unsupported type stays empty and can trigger regeneration again: the collection return byte says a cache needed generation, not that any element survived (GC Q2.10).

At the only successful append, existing capacity calls the ctor at0x004EA0FE and advances vector end by0x1C at0x004EA104. Otherwise allocating append0x004EA116 constructs exactly one and swaps buffer. There is no second push after either branch. Then the temporary input pose is destroyed0x004EA11C.

## 2. Exact element construction

**Static Pose2d source.** Address0x01059148 is resolved by0x004E9FDA/0x004E9FDC. Initializer0x004D6BC4 constructs Radians(+0) and Pose2d(angle,+0,250f): `0x004D6BCA: movs r1,#0`; `0x004D6BD4: movs r3,#0`; `0x004D6BD6: movt r3,#0x437a`; `0x004D6BDE: movs r2,#0`; call0x004D6BE2. This proves the shipped initial value; no arbitrary host setting is an input to this generator. No mutability census beyond this static binding is claimed.

**Marker.** Charger constructor sets marker code2, pose angle raw0xBFC90FDB about Z, translation raw(0x42AC0000,+0,0x41B00000)=(86,0,22), marker size raw(0x41D80000,0x41A00000)=(27,20). AddMarker result is stored at Charger+0xFC (`0x004E9C38..0x004E9C3C`). Generator reloads exactly that pointer0x004EA036 and uses marker pose at pointer+4 as **immediate parent** (`0x004EA048: adds r0,r5,#4`; `0x004EA04A: strd r0,r8,[sp]`). It does not select the nearest/last observed marker, look up by code again, or iterate the known-marker list. T's checked AddMarker SetParent makes the marker pose a child of the object's pose; keep that identity relation.

**Temporary Pose3d Q.** With p=static Pose2d transform:

- angle input = **F(p.angle+f32 bits0x3FC90FDB)**, one `vadd.f32`0x004E9FF2, then Radians(float)0x004E9FFA;
- axis = Z_AXIS_3D()0x004E9FFE;
- x copied unchanged from p.transform+8 (`0x004EA00A: ldr r5,[r0,#8]`), y **VNEG.f32** of p.transform+0xC0x004EA01E, z raw0xC1780000 **−15.5f**0x004EA01A;
- parent = marker.pose; initial name empty; construct at stack+0x34 via0x004EA058.

Under shipped static values, Q has translation **(+0,−250,−15.5)** and Radians(+pi/2_f), parent marker.pose. No f64 addition/negation is performed by these parent instructions. Names: the generator then forms `Charger`+to_string(objectID)+`PreActionPose` and SetName(Q)0x004EA0BA; these string helper calls do not alter Q's transform/parent. The embedded stored pose is subsequently named simply `PreActionPose` by its own constructor O.

**Append's constructor and every argument.** PLT0x004A47F8 has the symbol `PreActionPose(ActionType,KnownMarker const*,Pose3d const&,float)`, even body **0x0050DCFC** (Thumb symbol0x0050DCFD). At the capacity path0x004EA0F4..0x004EA0FE:

| Argument | Native source | Value |
| --- | --- | --- |
| this / destination | vector.end loaded0x004EA0EC | One28-byte stored element |
| r1 actionType | saved original input `[sp+0x40]` | Exactly0 or1, no conversion to docking0 |
| r2 marker | `[sl]` with sl=Charger+0xFC | The same owned KnownMarker pointer |
| r3 poseArg | `add r3,sp,#0x34`0x004EA0FC | Q above, including its marker parent |
| stack float | movs r3,#0 at0x004EA0E8; store `[sp]`0x004EA0FA | **+0.0f**, raw0x00000000 |

Allocating path passes references to type, marker, Q and local **int0** at stack+0x28. Template0x004EA682, index size164, loads the int through its fourth argument reference0x004EA6EE..0x004EA6F0, converts `vcvt.f32.s32`0x004EA6F4, stores float stack argument0x004EA704, calls **the same pose ctor**0x004EA708. Thus no ambiguity from the template's `int` spelling: both capacity cases supply f32+0. No cube distance75/40, no default tolerance constant, and the static250 is **not** this scalar.

**Stored element (reuse O).** Pose ctor writes type/marker at+0/+4 and incoming float+0x18, creates independent pose wrapper+8. It checks `poseArg.IsChildOf(marker.pose)`; this producer has expressly parented Q to that exact marker pose. It obtains marker.pose.GetParent() and calls GetWithRespectTo(Q,thatParent,storedPose), so stored coordinates are **relative to the charger's object pose**, not left marker-local and not yet world/root-relative. Successful conversion retains that parent relationship (T checked output-parent semantics). It names storedPose `PreActionPose`, then computes+0x14.

The actual shared ctor's defined diagnostic paths remain O's: bad child relation logs `PreActionPose.PoseWrtMarkerParentInvalid`, sets error state/conditional debug-break then continues; conversion failure logs `PreActionPose.GetPoseWrtMarkerParentFailed`, same error machinery then continues naming/tolerance. No “throw/drop element on false conversion” is inferred. Generic IsChildOf internals and all malformed pose-tree outcomes are existing M11 owners, not newly certified by this valid producer. A null owned-marker pointer would be an undefined native parent read at0x004EA058/ctor; M1-034 remains the standing smallest-unit skip/log policy, not an invented marker fallback.

**Height tolerance+0x14 (reuse O Corrections).** Read the **stored pose's** f32 translation x,y,z after conversion; take vabs, select s=(|y|<|z| ? |z| : |y|) with MI, then s=(|x|<s ? s : |x|), then **F(s*f32 bits0x3F13CD3A)**. Unordered comparison retains the left operand. Store that f32, not a division by sqrt(3), not max of the original Q components before reparenting. Exact nominal stored geometry is markerTransform*Q; ideal real-number simplification gives identity rotation at(−164,0,6.5) in charger frame. **Those simplified numbers are not asserted as the exact native output bits**: native pose/quaternion conversion, parent transform and f32 rounding remain M11-065/066/067. The formula above is the exact field contract without inventing the output rounding. No additional caller-supplied tolerance argument exists in this append.

**Stored vs later world result.** GC and O's derived ctor already cover later objectPose composition and SetParent(objectPose.GetParent()). Stored+0x18=0 still passes through MakeUnitLength/len+b/rescale and world composition; b=0 is not evidence that one may bypass all native arithmetic or simply reuse the Q wrapper. That downstream world-offset/validity algorithm is not re-extracted here. This report closes the charger **generator inputs/count/order** and the already checked shared-constructor binding, not whole GetCurrentPreActionPoses fidelity or generic pose lifetime.

## 3. Comparison with the current stack

Code paths below under `cozmo-stack/src/Cozmo.Robot/`; line numbers were read at393bc407. Code is the comparison subject, never evidence of native behavior.

| Value / behavior | Native evidence | Current code | Assessment |
| --- | --- | --- | --- |
| Supported types/count |0x004E9FD4/FD6, one push0x004EA0FE/116 |Vision/ChargerGeometry.cs:123–124, actionType>1?empty:one pose | Same helper-level type/count rule. It does not establish actual cache reach. |
| p.x/p.y |initializer0x004D6BDE/BD6: +0/250f |ChargerGeometry.cs:59–60,75,135–137: x0, default distance raw0x437A0000 cast to float and negated | Default input values match. Custom distance overload is a host helper, not a parameter of this native generator. |
| angle |0x004E9FE4 literal0x3FC90FDB and f32 add0x004E9FF2 |ChargerGeometry.cs:62,90,137 | Shipped0+pi/2_f input matches; generic mutable-static behavior not represented by hardcoded0. No evidence such mutation is required. |
| Q.z |0x004EA01A raw0xC1780000 |ChargerGeometry.cs:64–65,137 | −15.5f literal matches. |
| marker local transform |0x004E9BBC..0x004E9C02, pointer result0x004E9C3C |ChargerGeometry.cs:43–54,107–110 | −pi/2_f,(86,0,22), code2 and27×20 values match. Current KnownMarker.PoseOnObject is a value description; it is not by itself native marker/object parent-node identity. |
| docked pose (separate helper) |N14 and T checked0x004EA202: pi_f,Z,(30,0,0), parent charger pose |ChargerGeometry.cs:56–58,115–116 | Input angle/translation match. This separate docked pose is not the generated pre-action element or robot-relative ChargerDockPose. No re-extraction. |
| final helper pose |Q parent marker, constructor reroot stored to object; later derived output parent=objectPose.GetParent() |ChargerGeometry.cs:138 `chargerPose.Compose(Markers[0].PoseOnObject).Compose(onMarker)`; Geometry.cs:327–328 creates fresh transform-only Pose3d | Geometric composition intent matches; actual parent relationship is flattened. Does not reproduce stored marker identity or the native constructor/output-parent sequence. |
| distance and height fields |pose ctor+0x18=0, O height from stored translation |ChargerGeometry.GeneratePreActionPoses returns IReadOnlyList<Pose3d>, no element fields | These are absent from that helper's result. Do not infer+0x18=250 from its distance constant, or a cube distance from type0/1. |
| production cache adapter |GC0x004DF8BA virtual→generator; stored ctor and one push above |Manipulation/PreActionPose.cs:233–237 explicitly logs MISSING and returns empty for Charger_Basic | Actual cache remains unimplemented, intentionally visible under B-M13-17. Matching standalone helper literals do not fill it. |
| numeric sequence |f32 angle add/VNEG and native Radians/Pose3d/shared re-root/height path |ChargerGeometry.cs:137 uses Mat3.AboutZ; Geometry.cs:327–328 transform composition; class comments themselves say poses are double | Input bits match, but this path is not proof of exact native result bits/quaternion round-trip. Current M13-009 already records the arithmetic/integration gap. No bit-identical claim or invented numeric output. |

The standalone helper's clean algebraic position(−164,0,6.5) is therefore useful explanatory geometry, not a stored-element specification sufficient to replace parent/tolerance/distance construction. The missing adapter is broader than returning that world pose in a one-element list.

## Current manifest and remaining uncertainty

Re-read current manifest at393bc407:

- **M12-001**, “Pre-action pose types, their per-type ctor distances and the distance threshold”, **IMPLEMENTATION_GAP**. Current evidence includes pose ctor0x0050DCFC/+0x18 store0x0050DD46, cube constants and shared cache/world path. Current unresolved adopts O ctor bodies but explicitly retains missing full constructor/marker-parent/IsChildOf integration. The charger's two zero scalars are distinct source-backed arguments; cube75/40 must not be applied to it. No current status contradiction or settlement.
- **M13-009**, “The charger's dimensions, its docked pose and its one pre-dock pose”, **IMPLEMENTATION_GAP**. Current evidence explicitly names0x004E9FB0 and the static Pose2d initializer, marker pose and docked pose. Those geometric inputs are confirmed; its evidence did not spell out concrete ctor+zero scalar/capacity forwarding. Current unresolved already records double/native arithmetic and missing live generator join. This report supplies bounded append binding, not proof that those integration gaps are built.
- **M13-040**, “Contact-true charger observation: live ingress, located identity, Known pose and ordered world/contact effects”, **IMPLEMENTATION_GAP**. Evidence includes actionable cache clear0x004E01E4..0x004E0226 and contact observations. B-M13-17's checked clear/regeneration remains credited; shared generator and M12-001/pose lifetime boundaries remain separate. No contact path is retraced or settled here.

No additional unknown argument remains in this generator's successful append. UNKNOWN within this job: exact final composed pose **bits** across every math/pose-tree case (existing M11-065/066/067 owners); full IsChildOf/error reachability/lifetime diagnostics; runtime adapter readiness; complete downstream docking/validity/hardware behavior. None is called no-effect. A later implementation must use the already checked shared contracts or explicitly retain their gaps rather than flattening them silently.

Exact change: this report only. No code, inventory, manifest, queue or project-state changes. No tests/hardware run; source extraction does not gain provenance from tests. Independent source-derived regression obligations (not executed): clear a nonempty supplied vector;0 and1 produce one correctly typed element;2..5 produce none; zero-capacity and retained-capacity agree on+0x18 **+0**; marker identity retained; stored parent=marker.parent and world output parent=objectPose.parent; height computed **after** re-root; repeat generation after SetPose clear. Publish only this report, then stop as requested.
