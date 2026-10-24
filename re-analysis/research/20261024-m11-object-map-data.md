| Item / step | Coverage | What the original does | Address with pasted instructions / earlier checked row | Record |
| --- | --- | --- | --- | --- |
| 1a. Constructor/add arguments | CHECKED | Type3, both last-image stamps, copied ObjectID and copied polygon; no pose field. | `0x00684A56: strb r5,[r4,#4]`; `0x00684A6E: strd r1,r0,[r4,#0x10]`; `0x00684A78: blx #0x4a5ad0`. Reuse R/RC O7 for add admission/quad. | M11-074; M14-007 payload |
| 1b. Clone and actual use | CHECKED | Clone allocates0x24 bytes, copies base fields/ID and deep-copies vertex vector. NodeContent(data&) invokes virtual+8 Clone. | `0x00684A90: movs r0,#0x24`; `0x00684ACA: blx #0x4a5ad0`; `0x0068C456: ldr r2,[r0,#8]`. | M11-045 / M11-074 |
| 1c. Equals and actual use | CHECKED | Null false; type mismatch false; same u32 ObjectID true. Polygon and timestamps ignored. NodeContent equality dispatches vt+0xC after type/pointer tests. | `0x00684AFA: ldr r1,[r1,#0x14]`; `0x00684AFE: cmp r2,r1`; `0x0068C4DA: ldr r3,[r0,#0xc]`. Reuse E checked content equality; Q §4. | M11-045 / M11-079 shared equality |
| 2a. Whole-map wrapper | CHECKED | Copy callback, pass processor map+0x10; root quad padding0→ImportQuad2d→FastPolygon→node Transform. Null root skipped; no dirty-byte write. | `0x0068142A: blx #0x4bca74`; `0x0068A436: movs r2,#0`; `0x0068A460: blx #0x4bca8c`. Reuse QC omitted fact4 and Q §4. | M11-046 |
| 2b. Node order/replacement | CHECKED | Nonzero overlap: children in vector order→TryAutoMerge→own callback; changed currently-leaf content ForceSet, internal result dropped. No subdivision or override-table test. | Reuse Q row4/§4 and QC: `0x006883D8` TryAutoMerge call; `0x0068846E` ForceSet call. Changed descendant results are not ORed into return. | M11-046 / M11-045 callbacks |
| 3. Derived ID filter | CHECKED | Callback tests type3 first; then reads derived data+0x14 ID vs captured ID. Matching type3 returns fresh base removal data with fresh equal stamps; type4/base returns same pointer. | `0x006810F4: cmp r0,#3`; `0x006810F6: bne #0x681172`; `0x0068110E: ldr r1,[r7,#0x14]`. Reuse R/RC D2–D3 and L M9. | M11-074 |
| 4. Narrow prior coverage | CHECKED | R/RC already covers add/removal callback, Q/QC Transform; E equality. New bounded detail: exact constructor layout/vtable and Clone/caller binding. | R O7/D2–D3; QC §4/omitted4; E content-equality row; ctor0x00684A38, Clone0x00684A8C, vtable0x01030A2C. | No status change |

# R-M11-OBJECT-DATA — research queue row63

Answers [the request](requests/20261024-m11-object-map-data.md). Job1 was pushed as4f539ae5 before starting this job; fresh pull reported up to date. Operator authorizes committing/pushing this report only, superseding the research README's usual no-commit rule. No production, manifest, inventory or queue edits. CHECKED means scoped evidence coverage, not fidelity promotion or code acceptance.

Binary `resources/lib/armeabi-v7a/libcozmoEngine.so`3.4.0-1204, SHA256 `02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`. Native instructions/ELF relocations, never pseudo-C, support all claims. known.py/index boundaries: ctor0x00684A38/72, Clone0x00684A8C/70, Equals0x00684AE8/34, map wrapper0x0068140C/76, processor whole-map Transform0x0068A414/282, AddObservableObject0x0067ECFC/980, node Insert0x00686CB4/48, NodeContent(data&)0x0068C450/28, NodeContent==0x0068C4BE/38. Ran known.py for these body/interface addresses;0x0068110E is **not in an indexed function**. Callback instructions below are an explicit checked window0x006810E0..0x00681178, not a guessed function extent. A lookup0x0068C4BC identified the tail of the shared-pointer overload, not the clone overload; resolved clone-overload target via PLT0x004BCF3C to0x0068C450 and ran known.py there. Thumb symbols use bit0; even instruction addresses and indexed sizes alone define body extents.

## Earlier checked rows to reuse

- **R/RC:** [overnight/20261014-ov42-map-producers.md](overnight/20261014-ov42-map-producers.md) O7 and D1–D3, with [overnight/20261015-ov42-44-check.md](overnight/20261015-ov42-44-check.md), OV42 O1–O8/D1–D3. Add's pose/cache/height/family admission and removal origin/type/ID capture are already checked. Do not replace these with all-located-object polygon sync.
- **Q/QC:** [20261013-quadtree-rest.md](20261013-quadtree-rest.md) row4/§4, with [20261013-quadtree-rest-check.md](20261013-quadtree-rest-check.md), Transform build rows and omitted fact4 (whole-map overload). Reuse node traversal/replacement and wrapper geometry; correct old caller label via RC (actual callers aging and removal, not uncalled FlagInterestingEdgesAsUseless).
- **E:** [overnight/20261015-ov50-52-check.md](overnight/20261015-ov50-52-check.md), “content-equality targets (vt+0xC)”: ObservableObject0x00684AE8..0x00684B08 null/type/u32ID. This is already an independently checked Equals body.
- **L:** [20261022-m13-017-groundwork.md](20261022-m13-017-groundwork.md) M7–M9, adopted relevant joins in [20261023-m13-017-groundwork-tests-claude-check.md](20261023-m13-017-groundwork-tests-claude-check.md). M9 is deliberately a callback **window**, not a discovered index entry. Charger type4 base payload remains separate.
- Existing ForceSet/TryAutoMerge/type-change callbacks are [20261013-play-area-check.md](20261013-play-area-check.md), with Q/QC's later geometry/merge corrections. They are named lower owners, not re-extracted here.

## 1. Content-3 data, constructor, Clone and Equals

**Caller inputs, reuse R O7/RC.** Accepted type3 branch imports object's root-relative bounding quad (virtual+0x50,padding0) into a polygon, obtains **Robot::GetLastImageTimeStamp**, and invokes ctor0x0067EF00 with r0=stack data,r1=ObservableObject pointer,r2=imported polygon,r3=timestamp. After constructing the payload it imports the same quad a second time into the polygon passed to map+8 Insert0x0067EF20. Payload polygon and insertion geometry are separate vector copies of the same prepared quad, not aliases to a live moving pose. No ObjectID or pose pointer is used as a substitute for that vertex payload.

**Complete data layout** (ctor0x00684A38..0x00684A7E; size0x24 from Clone):

| Offset | Stored value / semantics | Native witness |
| --- | --- | --- |
|+0|Derived MemoryMapData_ObservableObject vptr, address point0x01030A2C|GOT0x0103FA70→vtable symbol0x01030A24,+8; stores0x00684A60|
|+4|Type byte3|0x00684A42 movs r5,#3;0x00684A56 strb|
|+5..+7|Padding, **not initialised as semantic fields** by this ctor|No field store there; do not invent flags or zero-padding behavior|
|+8|first timestamp, supplied u32|0x00684A4C str r3,[r4,#8]|
|+0xC|last timestamp, same supplied u32|0x00684A58 str r3,[r4,#0xc]|
|+0x10|Embedded ObjectID's vptr (ObjectID vtable+8)|0x00684A6A..0x00684A6E final strd; earlier UniqueEnumeratedValue vptr store0x00684A66 is overwritten|
|+0x14|Embedded ObjectID integer value copied from input object's +0x18|0x00684A68 ldr r0,[r1,#0x18];0x00684A6E stores alongside ID vptr|
|+0x18/+0x1C/+0x20|Owned vector begin/end/capacity for Polygon<Point2f>, copying all input vertices in order|0x00684A72 add r0,r4,#0x18;0x00684A78 vector copy call|

There is **no Pose3d, object pointer, movement-state field, marker pointer or direction vector** in this payload. ObjectID+0x14 is the integer inside the embedded ID, not a pose offset. All timestamps/IDs are copied raw32-bit values; vertex XY floats copied without f32/f64 conversion or geometric arithmetic.

**Vtable and tree use.** Relocation0x01030A34 contains Clone Thumb symbol0x00684A8D (slot+8 from address point0x01030A2C);0x01030A38 contains Equals0x00684AE9 (slot+0xC). Index entries give even bodies above. Node Insert0x00686CC4 builds NodeContent(data&) via PLT0x004BCF3C→0x0068C450. That ctor loads incoming data vptr then vt+8 (`0x0068C454: ldr r0,[r1]`;0x0068C456 slot load;0x0068C45A blx r2), wraps returned raw clone in shared ownership. Thus Clone is **actually used on insertion**, not an unused vtable decoration. One cloned content is passed through that node insertion's recursive work; descendants may share it. Caller stack payload/vector lifetime does not invalidate stored cells. This is not evidence for independent value copies at every descendant.

**Clone** allocates0x24 bytes. Copy raw three words+4..+0xF (type/padding and both timestamps), restore derived vptr; restore embedded ObjectID vptr and copy integer+0x14; deep-copy source polygon vector+0x18 into new vector+0x18. No new timestamp query, ID assignment, live object lookup, pose recomposition or polygon simplification. Caller vector changes cannot mutate clone's vertex buffer. Padding is copied as bits, not a behavior-changing comparison key. Generic allocation/unwind machinery is not a second supported success result.

**Equals, reuse E.** Null RHS false. Compare type bytes; mismatch false without derived read. Otherwise compare u32+0x14 for equality. Polygon coordinates/order/length, first/last timestamps and object pose are not compared. Distinct payloads for same ID/type compare equal even if polygons/stamps differ; equal polygon with different IDs does not. No epsilon or float comparison in this method. It does not dynamic_cast; valid producers ensure type3 has the derived layout.

NodeContent equality, reuse Q §4, first fetches both type bytes, then pointer identity, then virtual Equals+0xC; operator!= inverts it. TryAutoMerge and Transform therefore observe this class equality, not arbitrary managed-record/vector equality or pointer identity alone. Same type/ID fresh data can be treated unchanged despite a different polygon. The underlying shared timestamp/merge rules remain M11-045. Null pointers to NodeContent equality fault before its later null branches; Equals(null)==false does not make null a valid Transform deletion result.

## 2. Removal across the whole map — Q/QC Transform rows

**Entry:** selected map receives whole-map TransformContent(callback), slot+0x10, even entry0x0068140C. Copy callable, pass map+0x10 processor. This is **not** the polygon-specific overload at0x0068147C/slot+0x14. Wrapper does not set map dirty byte and does not aggregate a result into that flag.

Processor0x0068A414: null root at+0x24 skips. Otherwise root.MakeQuadXY(pad0), ImportQuad2d, construct FastPolygon, copy callable, invoke node Transform on current root with processor. This gives a polygon covering root bounds, **not the removed object's stored polygon**. Reuse checked root geometry/overlap arithmetic; the wrapper still uses actual geometry (no bypass of GetOverlapType), so no invented behavior for corrupted/nonfinite roots. No expansion or subdivision initiated by Transform.

**Exact node ordering, reuse Q §4/QC:**

1. GetOverlapType==0→false, no children/callback.
2. Visit children in their stored0..3 order; each gets a copied callable. Complete each child's traversal before next. Ignore child return values.
3. If children existed, TryAutoMerge once **after all child transforms**.
4. Reload this node's own content; null→false. Call callback with a shared copy, including internal nodes. Wrap callback result as NodeContent; compare by `!=` using the source-backed class Equals rules.
5. If different **and currently a leaf**, ForceSet returned shared data and return true; otherwise discard replacement and return false. A node made leaf by the preceding merge qualifies. A still-internal node's callback output is discarded. Descendants may have changed while this node returns false.

No callback application restricted only to original leaves, no pre-order own-node callback, no “OR child changes” return, no new type override table or containment test. Changed leaf is replaced whole, through ForceSet and existing callbacks (type-node sets, area and border invalidation) in that native order. ForceSet type5→fresh base type1 with both stamps=incoming.last is already checked M11-045, separate from callback selection. No original object-footprint clipping or ObjectID-region-list deletion.

Float operations here occur in root quad/FastPolygon/overlap, already checked in Q/QC/tree geometry report: f32 cached-bounds/epsilon tests and exact existing mixed-precision polygon circle arithmetic. This report adds **no new f64 geometry**; data selection/equality/removal and traversal are integers/pointers. It does not replace the native root-polygon walk with an assumed equivalent enumeration.

## 3. Derived ID read and base charger exclusion — R/RC D2–D3, L M9

Removal captures selected object's ObjectID, family-derived removal type and fresh GetLastImageTimeStamp, chooses the existing map by **supplied pose-root ID**, then invokes whole-map transform0x0067F368. No map→defined return; family removal type0→warning/return. Family4 computes type5, normal cube-family removal type1 (R/RC D1–D2). These gates are already checked and not re-extracted.

Concrete callable's checked operator window is0x006810E0..0x00681178; vtable address point0x010308F4, operator slot+0x18 at0x0103090C→Thumb0x006810E1. The **index has no function extent for that target**. Do not assert a decompiler-derived bound. In the window r7 is incoming data pointer, r4 its shared control; r6 is callable capture. Move input shared pair out, clear input pair. Then:

- Read byte[data+4]; compare to3 at0x006810F4; branch on mismatch to0x00681172 returning identical data/control. Therefore **base type4 charger data is distinguished before any ID read**; no out-of-object data+0x14 access on valid base charger path.
- Only type3: capture's ID at callable+8 vs integer[data+0x14] at0x0068110E. This is exactly the derived constructor's stored ObjectID value above, not a polygon word, pointer or vptr. Mismatch returns the same payload.
- Match: allocate shared-emplace **base MemoryMapData**, type=capture byte+0xC, first=last=capture word+0x10. Return new base pointer/control. Old shared reference released; original derived polygon/ID payload does not survive as fields of the replacement. Do not preserve original first-observed stamp.

The shared allocation is0x1C bytes including control header, base data starts at allocation+0xC. Thus callback store at **allocation+0x14** is the base timestamp at **data+8**, not a new ObjectID field. Keep allocation-relative and data-relative offsets distinct. Allocated base data has no derived ID/polygon. Callback performs no f32/f64 arithmetic. Native padding bytes are not an extra semantic payload.

Consequently a charger removal request computes5 but cannot directly match existing base type4 in this callback. Do not “repair” it to remove every charger footprint. If type3 matching content were returned as5, downstream ForceSet conversion is a distinct already checked rule; it does not change the initial type3 gate. The ordinary cube match returns base1 and is applied to changed leaves by the exact traversal above.

## Current claims, regression boundary and remaining uncertainty

Current manifest at4f539ae5 read; relevant titles/status/evidence:

- **M11-074**, “Memory-map producers and readers not yet read: MapComponent::UpdateRobotPose (explored area), UpdateObjectPose, RemoveObservableObject, FlagInterestingEdgesAsUseless, ReviewInterestingEdges, ClearRobotToMarkers, FindContentIf/FindIf”, **IMPLEMENTATION_GAP**. Evidence explicitly adopts RC O1–O8,D1–D3 including0x006810E0..0x00681172 and add0x0067F0BC. The derived ID adapter and constructor/Clone details here fit this producer, not a new whole-layer acceptance. Historical unread title does not negate later adopted rows.
- **M11-046**, “The quad tree's polygon Transform and the collision ray walk”, **IMPLEMENTATION_GAP**. Evidence0x0068147C..0x006814F4/0x00688378..0x00688476 plus Q/QC adoption in current unresolved covers the whole-map wrapper and internal callbacks. Current RC correction identifies aging/removal as actual callers; uncalled FlagInterestingEdgesAsUseless is not needed to establish this path.
- **M11-045**, “The memory map's quad tree: Insert, Expand, override rules, subdivision and merging”, **IMPLEMENTATION_GAP**, owns virtual clone/equality use, shared content, merge and ForceSet callbacks. **M11-079**, “Quad-tree border consumer: GetBorders, CalculateBorderWaypointCenter, MakeBorderSegment, HasBorders/HasBorderSeed, and the caller AIInfoAnalysisProcCalculateInterestingRegions”, **IMPLEMENTATION_GAP**, evidence includes E's content-equality targets. Reuse checked u32 ID equality, no timestamp/vector equality invented.

No current settled record is newly contradicted. R O7's brief “ObjectID at+14, polygon+18” is confirmed and made precise, not reversed. No evidence gap is filled by passing tests or a successful robot run. UNKNOWN here: implementation readiness and malformed-producer reach for forged type3 base layouts; full class-reader census beyond the requested Clone/Equals/removal/tree uses. Supported producers enforce the class/type relation described above; no blanket no-effect claim about the polygon payload is made merely because Equals ignores it. Shared diagnostics/fault behavior retain existing owners/M1-034.

Exact change: this report only. No tests/hardware run (research only). Independent source-derived regression obligations, not executed tests: input polygon mutation cannot alter clone; equalID/different polygon or stamps compares equal; differentID/same polygon false; base4 callback never reads+0x14; matching type3→fresh base removal stamps; child-first merge-before-own-callback; internal replacement discarded; new-leaf replacement allowed; wrapper leaves dirty untouched. Commit only this file, pull/rebase and push before Job3.
