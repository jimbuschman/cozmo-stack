| Item / step | Coverage | What the original does | Address with pasted instructions / checked earlier row | Record |
| --- | --- | --- | --- | --- |
| 1. Narrow prior check | CHECKED | All items2–4 already adopted: Q rows2a–3c/sections2–3, QC build rows, CW Insert/Expand. Historical “unread” text is superseded. | Q/QC named below; QC “Result: items1–6 are confirmed instruction by instruction.” No repeated algorithm extraction. | M11-045/048 |
| 2a. Growth condition/limit | CHECKED | Expand tries growth when outside; node refuses level255 or unsigned level>=max, returning false unchanged; caller passes max8. | `0x006876EE: cmp r7,#0xff`; `0x006876F6: bhs.w #0x687a30`; `0x00685260: movs r2,#8`. Q2a/section2. | M11-045/048 |
| 2b. Geometry/old child | CHECKED | Shift centre by signed half old side using relative direction against −epsilon; construct four sideS/levelL children; old root content/descendants go to opposite quadrant. | Q2a/section2 steps1–4,6: `0x00687984: str r7,[r3,#0x7c]`; `0x0068798A: str r0,[r7]`. | M11-045 |
| 2c. Siblings, notifications, return | CHECKED | Unknown siblings with both stamps=old last; five ordered ForceSets before size/level update; ResetBoundingBox, success log, true. Preserve cached-area effects. | `0x00687962: blx #0x4bcf84`; `0x006879BA: vadd.f32 s0,s0,s0`; `0x006879C0: strb.w r0,[r4,#0x80]`. Q2b/section2 and QC. | M11-045 |
| 3a. Shift selection/refusal | CHECKED | After growth cannot contain, positive budget permits shifts. Boundary flags; opposing sides false, all-inside error false; move centre half side. | `0x00685322: blx #0x4bcdbc`; `0x00686F08: blx #0x4a4540`; Q3a/section3 flags/refusals. | M11-045/048 |
| 3b. Leaf/nonleaf shift | CHECKED | Leaf retains content and returns success after moving bounds; nonleaf creates Unknown half-side children, performs eight checked move/clear cases, destroys old nodes. | `0x00686FF6: blx #0x4bcfc0`; `0x00687000: beq.w #0x6873aa`; `0x00687398: blx #0x4bcff0`. Q3a–3c/section3 eight-case list; PA Swap/Destroy rows. | M11-045 |
| 3c. Callbacks/return | CHECKED | 20 direct ForceSet and12 Swap sites; shared callback order/cache accounting retained. Success log then true; no root ForceSet or size/level change. | `0x006873D8: blx #0x4a505c`; `0x006873FE: movs r2,#1`. Q3b–3c, QC correction and PA bookkeeping rows. | M11-045 |
| 4a. Map/Insert checks | CHECKED | Normal map wrapper supplies shift budget1. All-point NaN guard; outside type5 warns without Expand, others Expand with supplied budget; then one node Insert, dirty OR result. | `0x00685110: mov r2,r8`; `0x00685112: blx #0x4bcd98`; `0x00685120: blx #0x4bcda4`. CW “The quad tree”; T wrapper row. | M11-045 |
| 4b. Expand loops and result use | CHECKED | Recompute direction/containment after each growth; continue while upgraded and outside. Then budgeted ShiftRoot attempts, ignore Boolean and recheck containment. Always dirty. Insert follows expansion once, not repeated per growth. | `0x00685266: mov r8,r0`; `0x00685322: blx #0x4bcdbc`; `0x00685438: movs r0,#1`. Q/QC Expand rows, CW. | M11-045 |

# R-M11-ROOT-GROWTH — queue row65

Answers [request](requests/20261024-m11-root-growth.md). Extraction only; no code, manifest, inventory, queue, branch or test changes. Initial pull3d164d99. Row51 already has tracked answer79da201e; QUEUE loop2 requires no answer file, so it was not rewritten. CHECKED denotes bounded evidence coverage, not acceptance or settlement. The queue's research push rule authorizes committing this report.

Binary: `resources/lib/armeabi-v7a/libcozmoEngine.so`3.4.0-1204, SHA256 `02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`. Ran known.py before native checks for UpgradeRootLevel0x006876D8/868bytes, ShiftRoot0x00686DF8/1532bytes, QuadTree::Insert0x00685008/300bytes, Expand0x00685200/586bytes. Boundaries are known/index extents; Thumb symbol bit0 cleared, not a guessed boundary. Pasted instructions above were read from the binary; previous checked helpers are reused rather than re-extracted.

## Adopted rows, before any new extraction

**Q** = [quadtree remainder](20261013-quadtree-rest.md), coverage2a–3c and sections2–3; **QC** = [adopted check](20261013-quadtree-rest-check.md), result, omitted facts1–3, M11-045 build rows. **CW** = [cliff/world check](20261012-cliff-world-helpers-check.md), “The quad tree” Insert/Expand/growth-gate rows. **PA** = [play-area check](20261013-play-area-check.md), processor/ForceSet/SwapChildrenAndContent/DestroyNodes rows. **T** = [tree geometry report](20261023-m11-tree-insert-ray-geometry.md), wrapper/callee coverage, with [Claude check](20261023-r-m11-tree-geometry-claude-check.md). QC already confirms the full growth and shift algorithms. Thus the request's open question is an evidence-to-builder join, not newly unread native bodies. The following is a focused index to those adopted obligations, with native bindings reconfirmed above.

## Growth contract (Q2a/2b, section2; QC)

Let F mean each individual binary32 operation, no reassociation/FMA; e=bits32(0x3727C5AC). All node geometry arithmetic here is f32, with f64 used for cached areas and widened log arguments as specified below. Root fields: children vector+0/+4/+8, centre XYZ+0xC/+0x10/+0x14, side+0x18, parent+0x7C, level byte+0x80, content shared pointer+0x84/+0x88.

Growth itself checks only level255/max; the outer caller owns the containment decision. With old centre(cx,cy), sideS, levelL, contentD, lastT=D+0xC, input direction centroid−centre, Q section2 steps1–7 are the exact ordered mutation:

1. Move out old children O, clearing root vector. Construct type0 Unknown with first=last=T.
2. hp=F(S*0.5), hn=F(S*−0.5). New x=F(cx+(dx>=−e?hp:hn)); new y=F((dy>=−e?hp:hn)+cy). GE is ordered; unordered takes negative displacement. Comparisons use relative direction, not absolute point coordinates or a strict zero threshold.
3. Construct four sideS, levelL children under root at Q0=(+hp,+hp),Q1=(+hp,−hp),Q2=(−hp,+hp),Q3=(−hp,−hp), relative to new centre; z retained. Each initially has Unknown data. Choose old-root recipient q: positive/positive→Q3, positive/negative→Q2, negative/positive→Q1, negative/negative→Q0, with “positive” meaning >=−e as in Q's native quadrant row0x006878D2..0x00687924. NaN takes the corresponding below-side case.
4. Make separate shared Unknown contentU, first=last=T. ForceSet the other three children to U in index order. Reparent old immediate children to q and transfer O into q; ForceSet(q,D), then ForceSet(root,U). These **five executed calls** occur before changing root size/level.
5. Set side=F(S+S), level=uint8(L+1), ResetBoundingBox. QC omitted fact2 owns bounding-box computation: h=F(side*0.5), mins F(cx−h)/F(cy−h), maxes F(h+cx)/F(h+cy), AxisAlignedQuad fminf/fmaxf and diagonal storage. Success log and cleanup then true. Level255 or level>=max returns false with no mutation; Expand max8 limits ordinary root side to2560mm given the checked160mm/level4 initial root.

Q2b/PA own notifications, not geometric area recomputation. ForceSet snapshots oldExcluded=(old type0 or currently hasChildren), converts incoming removal type5 to fresh base type1 first (QC omitted fact1), replaces shared content, calls OnNodeContentTypeChanged only on type change. Area uses A(S)=double(F(F(S*bits32(0x3A83126F))*F(S*bits32(0x3A83126F)))); callback adds/subtracts in native call order. A known leaf moved to q gains A(oldS); root has children by the time its own ForceSet snapshots, so old explored credit is **not removed**. Do not repair the retained credit as a geometric union. Type9 adds at q then subtracts at root at old-side area; cache membership and border invalidation follow PA's callback rows. Unknown→Unknown does not create a type-change callback. Shared processor rows are already adopted, not UNKNOWN dependencies.

Log Q section2: channel QuadTree, shipped key `QuadTree.UpdgradeRootLevel`, text `Root expanded to level %u. Allowing %.2fm`, side scaled in f32 by raw0x3A83126F then widened f64. It is a normal growth return, not a request to retry the leaf insertion immediately from inside this function.

## Shift contract (Q3a–3c, section3; QC; PA)

Q section3's flags are accumulated per vertex in order, short-circuiting flags already true. With h=F(S*0.5): px if F(F(h+cx)−e)<=x; nx if x<=F(F(cx−h)+e); py/ny analogously. These boundaries are inclusive; a point at the tolerance edge can request shifting. Unordered sets none. Opposing x flags reject first, then opposing y, warning `CantShiftPMx`/`CantShiftPMy` with `Current root size can't accomodate given points`, return false unchanged. No flags (including empty input) logs `QuadTreeNode.ShiftRoot.AllPointsIn` / `We don't need to shift`, sets errG, conditional break-on-error, returns false.

Otherwise recentre by +h/−h/0 per axis with f32 addition and ResetBoundingBox. **Leaf shortcut keeps its content**, creates no children, changes no side/level, logs success and returns true. For nonleaf: move out O, create four Unknown children sideh, leveluint8(L−1), at centre offsets ±F(h*0.5), z retained, first=last=root current last. Q section3 gives all branch-site addresses; reuse it directly. For clarity its move map is:

| Direction | Retained old→new children | Other new children |
| --- | --- | --- |
| +x,+y | 0→3 | Unknown0,1,2 |
| +x,−y | 1→2 | Unknown0,1,3 |
| −x,+y | 2→1 | Unknown0,2,3 |
| −x,−y | 3→0 | Unknown1,2,3 |
| +x | 0→2,1→3 | Unknown0,1 |
| −x | 2→0,3→1 | Unknown2,3 |
| +y | 0→1,2→3 | Unknown0,2 |
| −y | 1→0,3→2 | Unknown1,3 |

Execution visits new indices0..3, interleaving ForceSet(new[i],UnknownU) or SwapChildrenAndContent(new[i],old[j]) exactly as Q3b's addresses list. There are20 direct ForceSet sites,12 direct Swap sites, not19. Swap exchanges child vectors/reparents them, then ForceSets new from old content and old from saved new content. Coordinates/side do not swap. DestroyNodes(O,processor) follows at0x00687398; PA/Q3c specify recurse→OnNodeDestroyed→reset/free order. A former nonleaf's vector remains full of null entries at its callback, so it is not reclassified as leaf for area subtraction. Do not use ClearDescendants' different two-pass order in place of DestroyNodes. Root itself is never ForceSet by ShiftRoot; retained and discarded credits/type sets/borders follow the exact callback sequence, not a fresh area sum.

Success logs QuadTree.ShiftRoot / `Root level is still %u, root shifted. Allowing %.2fm`, f32-scaled side widened f64, then returns true. False return is a refusal/error, not successful clearing. Expand ignores that return and independently tests Contains.

## Caller and insertion order (CW, QC omitted3, T)

MemoryMap::Insert0x006817DE's ordinary wrapper supplies budget1 to QuadTree::Insert; that integer is a **shift-attempt budget after growth**, not a one-level-growth count. Merge supplies its own checked budget at the same interface; do not hardcode1 throughout QuadTree.

Insert checks isnanf across all vertices without early exit; any NaN logs NaNPoly/error/conditional break/callstack and returns without insertion. Otherwise Contains current root: outside removal type5 warns and skips Expand; outside other content calls Expand(polygon,budget). It then calls the current root node Insert **once**, regardless of whether expansion ultimately contained the polygon, and ORs its result into tree dirty. This is not an insert→grow→retry loop: growth/shift precede the sole recursive insertion.

Expand recomputes centroid−root centre componentwise f32, calls UpgradeRootLevel(dir,8), reconstructs FastPolygon/rechecks Contains, repeats while growth true and still outside. It then shifts only while still outside and signed budget>0; ignores ShiftRoot's Boolean, rechecks Contains each attempt, up to budget. Insufficient expansion warns with centroid/root/side widened doubles; dirty is always set1. Q/CW and QC own these checked loop branches, including refusal handling; native0x00685322 is followed by polygon construction/Contains rather than testing returned r0. Node insertion's usual containment/overlap/override rules remain in T/Q checked rows; direct node insertion paths such as FillBorder bypass this expansion wrapper.

## Current records and limits

At3d164d99, **M11-045**, “The memory map's quad tree: Insert, Expand, override rules, subdivision and merging”, **IMPLEMENTATION_GAP**, evidence includes `0x00685008 QuadTree::Insert; 0x00684C28..0x00684C48 root; ... 0x006878D2..0x0068791E quadrant map` and R-VIS gap2/verify. Its unresolved explicitly adopts Q/QC growth/Expand/ShiftRoot and processor rows. **M11-048**, “Unread quad-tree, OpenCV-numeric and vision-handler bodies”, **RECOVERABLE_GAP**, evidence cites original R-VIS “not done” reports, but its later unresolved says: “the quad-tree subset is fully read ... UpgradeRootLevel/Expand, ShiftRoot with20 ForceSet and12 Swap sites ... and moves to M11-045 / M11-046 / M13-032 as build rows.” Earlier unread wording is historical, not an unresolved native algorithm in this request. No status/title/evidence edit or new exclusion.

All requested steps have adopted native rows; no UNKNOWN is required here. This does not settle broader M11-048 OpenCV/handler obligations or prove current C# implementation parity. No tests/hardware were run. Source-derived regression boundaries are Q's eight ordered shift cases, half-side leaf shift retaining content, max-level refusal, −epsilon quadrant decisions, old-leaf growth credit retention, and failed shifts consuming the caller's finite budget before the single insertion. The report is evidence for later builder/checker work only.
