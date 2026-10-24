| Item / step | Coverage | What the original does | Address with pasted instructions / earlier checked row | Record |
| --- | --- | --- | --- | --- |
| 1. Wrapper data and processor inputs | CHECKED | Fold eleven enum/bool pairs; construct base output data with supplied type and identical timestamps; processor at map+0x10; dirty map+8=1 on normal return. | `0x006813DC: strb.w sl,[sp,#4]`; `0x006813E2: strd sb,sb,[sp,#8]`; `0x006813F6: blx #0x4bca68`. Reuse OV11 row19/check g. | M11-047; callers M11-074/089 |
| 2a. Cache and seed selection | CHECKED | Refresh key(high=seedType,low=mask), reuse clean cache or FindBorders; seed nodes from maintained type set, not arbitrary polygon scanning. | `0x0068A036: mov r1,r6`; `0x0068A038: mov r2,r4`; `0x0068A03A: blx #0x4bd170`. Reuse OV11 rows4–8/check b,d,i. | M11-047 / M11-045 |
| 2b. Neighbor walk/order | CHECKED | Native quadrant transition table, ordered facing-side descent and checked-contact state machine. Side contacts; no root-exterior or corner-only neighbor. | Reuse OV11 rows1–3,8–18 and check a,c,d,e; table0x01051064; FindSingleNeighbor0x006881B4..0x00688214; FindBorders0x0068AB98..0x0068B096. No repeated extraction. | M11-047 |
| 2c. Deduplication and snapshot | CHECKED | Waypoint `from` pointer identity dedup; first occurrence copies raw centre XY to separate vector before any insert. | `0x0068A050: ldr.w r0,[sl]`; `0x0068A194: adds r1,#0xc`; `0x0068A198: ldr.w r3,[r1,r2,lsl #2]`. Reuse OV11 row20/check f. | M11-047 |
| 2d. Apply snapshot | CHECKED | Re-read root for each copied point; one-point Polygon→FastPolygon→QuadTreeNode::Insert with supplied data, result ignored. | `0x0068A1E2: ldr r4,[r0,#0x24]`; `0x0068A1F0: movs r2,#1`; `0x0068A206: blx #0x4bcda4`. Reuse OV11 row21/check f. | M11-047 / M11-045 |
| 3. Required callees / no Expand | CHECKED | Neighbor lookup is checked above; centre is node+0xC/+0x10, not interface midpoint; direct node insert bypasses MemoryMap/QuadTree insertion wrapper and expansion budget. | `0x0068A1FA: blx #0x4bca80`; `0x0068A1FE: ldr r2,[sp,#4]`; `0x0068A202: ldr r3,[sp,#0xc]`. OV11 row21; checked tree report row1f for node insertion. | M11-045/047; ray consumer M11-046 |
| 4. Prior checked coverage | CHECKED | All requested algorithms already extracted and adopted: OV11 rows1–22/check a–i, with later operator seed-order decision. Wrapper/snapshot/insertion instructions above reconfirm bindings. | OV11 check f/g; `0x006813FC: strb r0,[r4,#8]`; `0x0068A1B8: add.w sl,sl,#0x10`; `0x0068A29A: adds r5,#8`. | No status change |

# R-M11-FILLBORDER — queue research row62

Answers [the request](requests/20261024-m11-fillborder.md). Read PROJECT_STATE, AGENTS, research README and request; initial pull to46bf1620. Operator's explicit report-only commit/push instruction overrides the usual no-commit research lane rule. CHECKED denotes bounded evidence coverage, not implementation acceptance or a fidelity promotion.

Primary source: `resources/lib/armeabi-v7a/libcozmoEngine.so`3.4.0-1204; SHA256 `02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`. Ran installed Python313 `known.py` before instruction checks of0x006813B0 and0x00689FAC. Index/known boundaries: FillBorderInternal0x006813B0/84 bytes, FillBorder0x00689FAC/812 bytes. Instruction addresses even; Thumb symbol low bit is not a boundary inference. Existing helper bodies are reused from checked rows, not re-extracted or cited from pseudo-C.

## Checked evidence to reuse

- **OV11:** [overnight/20261014-ov11-m11-047.md](overnight/20261014-ov11-m11-047.md), numbered rows1–22 and **adopted check** [overnight/20261014-ov11-m11-047-check.md](overnight/20261014-ov11-m11-047-check.md), rows a–i. The check confirms every factual claim and supplies omissions. Its then-open seed-order decision is superseded by AGENTS/current manifest's operator decision: insertion order, everything else exact.
- [20261023-m11-tree-insert-ray-geometry.md](20261023-m11-tree-insert-ray-geometry.md) and [its Claude check](20261023-r-m11-tree-geometry-claude-check.md): checked point-polygon/overlap/node-insert geometry; no repeated geometry extraction.
- Caller rows M7–M8 in [20261022-m13-017-groundwork.md](20261022-m13-017-groundwork.md), and overhead row1i/[type10 stage](20261023-m11-map-legacy-producers.md) with [its Claude check](20261023-r-m11-producers-m12-charger-preaction-claude-check.md). These establish the two live callers and their timestamp sources, not a different border algorithm.

The narrow check answers item4 first: nothing in the requested neighbor, snapshot or insert algorithm needs a new speculative design. OV11 check a–g is the source-backed build contract. The following is its complete scoped calling/ordering contract, retaining the approved ordering input and named lower interfaces.

## Wrapper and live inputs — OV11 row19/check g

Entry registers r0=map,r1=innerType,r2=eleven-pair table,r3=outputType, first stack word=timestamp. Loop offsets0,2,...,20; nonzero bool includes EContentTypeToFlag(enum), OR into u32 flags. No early exit or spatial ROI. Stack data is **base MemoryMapData**, address point0x01030794 resolved through GOT0x0103FA6C; byte+4=low8(outputType), words+8/+0xC=same supplied u32 timestamp. Pass `(processor=map+0x10,innerType,flags,&data)` to0x00689FAC. On normal return set map+8=1 unconditionally, even if no border/insert/change; not OR of insert results (`0x006813FA: movs r0,#1`; `0x006813FC: strb r0,[r4,#8]`). No message is sent by this wrapper. No float arithmetic in enum/mask/data assembly.

Both supported joins here use inner9,output10,mask **0x4F8={3,4,5,6,7,10}** from table0x00C87675 and **fresh GetLastImageTimeStamp**: object add calls0x0067F0BC, overhead calls0x006803AA only when its prepared border-segment vector is nonempty. The doubled object quad and transformed overhead ROI temporary are not arguments to FillBorderInternal. It is global for this map/combination, not clipped by either quad. It does not rewrite every type9 cell directly to10.

## Cached border production — OV11 rows1–18/check a–e,i

Combination key is unsigned u64 `(seedType<<32)|flags`, in ordered map. New entry dirty1/empty waypoint vector. Refresh uses cached vector if clean, otherwise runs FindBorders. InvalidateBorders sets every entry dirty1 **and clears its vector**; occurs on type change and node destruction, not same-type ForceSet. Maintained node-type sets cover0x7C8={3,6,7,8,9,10}; FindBorders obtains typeSets[seedType] (creates empty set if absent), rather than scanning all leaf polygons. This is why arbitrary seed types cannot be substituted for the requested cached inner type.

Native seed order follows pointer-hash list/allocation history; **approved stack departure** iterates seeds in the order nodes entered that type set. No coordinate sorting or unordered/set-only output is authorized. This decision is already recorded by operator2026-10-14 in AGENTS and current M11-047; do not ask it again or preserve OV11's old “operator question” as a new blocker. It changes only the seed-order input to the otherwise exact walk.

Neighbors use node quadrant/parent/children and table0x01051064 indexed q*8+d*2. Directions d0=(+X),d1=(−Y),d2=(−X),d3=(+Y), names N,E,S,W. Table rows `(childIndex,useSibling)` in d0..3 order:

| q | d0 | d1 | d2 | d3 |
| --- | --- | --- | --- | --- |
|0|(2,0)|(1,1)|(2,1)|(1,0)|
|1|(3,0)|(0,0)|(3,1)|(0,1)|
|2|(0,1)|(3,1)|(0,0)|(3,0)|
|3|(1,1)|(2,0)|(1,0)|(2,1)|

Root quadrant4 returns null. Sibling case returns parent's designated child or null if no children. Other case recursively obtains parent's neighbor; if it has children choose designated child, retaining returned neighbor if that child is null (check a correction). AddSmallestNeighbors appends facing-side smallest descendants, `(d+2)%4` using signed remainder and flipped clock. Descendant pairs for clock0: (0,1),(1,3),(3,2),(2,0); nonzero: (1,0),(3,1),(2,3),(0,2). First subtree completes before second. Live neighbor request clock0 therefore selects nonzero-clock facing-side pairs. This handles mixed levels, **side contacts only**, no corner-distance/epsilon test, no synthetic outside-root Unknown cell. All traversal/index choices are integer/pointer operations; no geometric f32/f64 comparisons.

FindBorders stores active combination at proc+0x20, clears vector, sets proc+0x28=1 and builds checked-state map. Per node four direction records have bit vector and remaining−1; initialise only when vector size0, to actual neighbor count. Marking a contact decrements once. Empty side is complete immediately; all four remaining counts must equal0. Work neighbor vector reserves1<<root.level; this is integer sizing, not float geometry.

The exact checked transition machine (OV11 rows8–18/check d):

1. Restart each unfinished seed at d0/index0. First-contact search **marks before type test**; accepts neighbor.typeFlag&mask, otherwise advances directions with index0. No same-type test in this initial search. First waypoint(seed,neighbor,d) sets start flag+0xD=1.
2. Advance neighbor contacts in their prescribed order; exhaust side→rotate direction. Do not skip a side simply because its remaining count is0.
3. **Same inner-type crossing first:** reverse d=(d+2)%4, rebuild neighbor list, find previous current in it, mark reverse contact on neighbor, switch current=neighbor. Append no waypoint. An outer mask also containing innerType does not change this precedence.
4. Masked **unchecked** contact appends(current,neighbor,d), marks it, continues.
5. Masked **already checked** contact ends walk. If vector nonempty and last end flag+0xC=0, append duplicate closing waypoint and set end flag1; otherwise no append.
6. Unmasked contact sets last end flag1 if any, marks and continues **even if already checked**.
7. Repeat from seed while any remaining count nonzero. Finish sets combination dirty0 and proc+0x20=null; proc+0x28 is not reset.

Waypoints are16 bytes `{from,to,direction,endByte,startByte,padding}`. Fast append copies fields then zeroes flag halfword; growth preserves prior entries and initialises end from its bool argument/start0 (check e). Consumers may see duplicate closing contacts: do not use a unique edge collection to replace this vector. These exact transitions, not a broad adjacency predicate, determine the ordered input to FillBorder.

## Snapshot, dedup and point inserts — OV11 rows20–21/check f

FillBorder refreshes the combination once. Iterate its vector begin→end in16-byte strides. Read **from pointer at+0**, ignore `to`,direction,flags for fill selection. Local pointer-identity set uses checked murmur2 over four pointer bytes; duplicate pointer skips. At **first occurrence**, immediately append the node's raw f32 x/y words from node+0xC/+0x10 to a separate Point2 vector. Equal coordinates on distinct nodes are not pointer duplicates. The membership set is never iterated to choose point order. All copying uses integer loads/stores: no widening, centre recalculation, midpoint, averaging or tolerance.

Snapshot completes **before the first tree mutation**. This matters: inserts can merge/delete nodes and invalidate/empty the waypoint combination. The pending centres are copied values, not retained node references to dereference later.

Then traverse saved point vector in8-byte strides. For each point load current root from proc+0x24 (every iteration), construct `Polygon{point}` count1, FastPolygon, then direct **QuadTreeNode::Insert(root,fastPoly,suppliedData,processor)**. Result ignored; destroy temporary geometry, continue. There is **no expansion budget**, no MemoryMap::Insert, no QuadTree::Insert NaN wrapper, and no Expand call on this route. It does not mutate the entire seed square, the outer neighbor or the shared interface. Exact downstream one-point geometry/override/subdivision/ForceSet is existing M11-045, not a new generic fill rule. Epsilon/float operations within those functions are the checked tree report's, including separately rounded f32 node bounds; do not recompute centres as double. For valid finite centre points, one-point FastPolygon has no edges/normals, centre=point and radius²0; overlap/insertion still follows the actual native classifier and level/override rules.

Mutation invokes existing type-set/area/border invalidation callbacks through node insertion. This FillBorder body **does not mark combination clean again after these inserts**. Wrapper dirty byte is separate from border-combination dirty state; do not collapse them. No whole-map Transform call is involved (M11-046 is a reader/related transform owner, not an alternate fill entry).

Float arithmetic confined to local dedup container sizing is OV11 row7/check i: max load factor1f; bucket and size+1 convert u32→f32; compare F(loadFactor*F(bucketCount)) against F(size+1), grow if smaller (MI; NaN BPL skips growth) or count0. Growth uses ceilf(F(F(size+1)/loadFactor)), truncating u32 conversion and integer max with native doubling request; checked rehash/__next_prime remain generic M4-050/container interfaces. These operations do not reorder the separately appended point vector. No f64 geometry is added by FillBorder.

## Diagnostics, current records and limits

Reuse OV11 row22/check f profiler: static `QuadTreeProcessor.FillBorder`, print interval signed64 5000 written every call, Tic/Toc; whole-millisecond elapsed converted by integer division before averaging. Print only elapsed>5000, last-print time updated before print. Averages f64 signed64 sum/signed32 count when count≥1, otherwise0. DAS interval−1. Existing profiler log delivery/M3 filtering remains its owner; no new diagnostic implementation or audit needed. Border walk itself emits no robot/API message or direct warning/error log. Actual map mutations affect later queries through existing cell data/caches.

Current manifest at46bf1620 re-read:

- **M11-047**, “The quad tree's border walk (FindBorders, AddBorderWaypoint, FillBorder)”, **IMPLEMENTATION_GAP**. Evidence expressly adopts OV11 check a–g and the named wrapper/walk/insert ranges. Current unresolved includes approved insertion-order departure and B-M11-5's historical implementation prerequisite block. The algorithm is recovered; the historical build block is not evidence of missing source. No new status promotion.
- **M11-046**, “The quad tree's polygon Transform and the collision ray walk”, **IMPLEMENTATION_GAP**; evidence0x0068147C..0x006814F4,0x00688378..0x00688476,0x00689D80..0x00689F5C. No contradiction or new transform recovery; its queries consume resulting cells.
- **M11-074**, “Memory-map producers and readers not yet read: MapComponent::UpdateRobotPose (explored area), UpdateObjectPose, RemoveObservableObject, FlagInterestingEdgesAsUseless, ReviewInterestingEdges, ClearRobotToMarkers, FindContentIf/FindIf”, **IMPLEMENTATION_GAP**; current evidence RC O1–O8 includes0x0067F0BC fill caller. Its no-caller ReviewInterestingEdges decision does **not** exclude the live AddObservableObject fill call.
- **M11-089**, “InterestingEdge (type9) map data class, MemoryMap::HasBorders callers, and BehaviorVisitInterestingEdge's consumers of the border output”, **IMPLEMENTATION_GAP**; evidence0x00680286..0x0068030A and base data equality. HasBorders' no-caller decision is unrelated to this live FillBorder path. Caller/consumer closures remain existing owners, not newly settled by this report.

Malformed-topology undefined paths remain OV11 check's M1-034 sites: null content0x0068AD2C/AE4C/AE62/AF30, null parent0x006881E4, invalid direction0x00688190, missing reverse contact0x0068AF10. No invented valid output or raw unchecked partial cache is justified. Keep standing smallest-unit skip/prior-state/log policy. This job does not re-extract these faults or general allocator/error plumbing.

No requested algorithmic step remains UNKNOWN after checked-row reuse. Still UNKNOWN here: actual implementation readiness, every malformed topology's production reach and a robot/oracle run; no whole-layer or order-independent final-set proof is claimed. OV11's suggested set-only invariance was explicitly **not adopted**; the report does not use it. All writes in this scoped production path are accounted for via named shared node-insert owners.

Exact change: this report only; no code, manifest, inventory, queue or project-state edits. No tests/hardware run. Source-derived regression obligations, not tests claimed executed: clean-cache reuse vs invalidation; mixed-level sides vs corner/root exterior; same-type crossing precedence; duplicate closure; distinct pointers/equal coordinates; all centres snapshotted before merge; root reload per point; ignored node-insert result; wrapper dirty1 with no points; approved seed insertion order. Commit only this file, pull/rebase and push before beginning Job2.
