# Charger-map add review (2026-10-21)

**Codex review; Claude spot-check pending**

Reviewed report `20261021-m11-charger-map-add-join.md`, baseline b08efa68 with B-M13-10 correction 7f42b427. Manager personally disassembled the ranges below from resources/lib/armeabi-v7a/libcozmoEngine.so and resolved call targets through PLT relocations. Bounded findings supported; whole tree/callback closure remains open. No settlement.

## Checked findings

- Caller order at 0x00506F8E..0x00506FB8 and 0x0050628E..0x005062D6: world, map(old), state broadcast, map(current) for changed nonzero new state. Invalid->valid therefore requests Add twice; ID deduplication cannot replace the actual cache state machine.
- 0x0067EA90..0x0067EC4C: family6 early return, invalid state branches, per-object outer cache and root-ID inner lookup, false-cache retry, true-cache IsSameAs with 5.0f each axis and Radians bits0x3E32B8C2; comparison success skips work. Slot+0x5C exactly1 removes then adds. A missing inner entry adds. Complete pose comparison math is not newly certified here.
- 0x0067ECFC..0x0067ED7A: family mapping, supplied-pose GetRootID, existing-origin map lookup before relative pose or height. Missing map returns at 0x0067F0CA/CC. Family table 0x0067F4C0..0x0067F546 and raw jump-table bytes confirm family4 selects type4 for add and type5 for removal.
- 0x0067ED7E..0x0067EE30: actual robot pose input, relative-query result exactly1, height arguments1/15/0, high branch copies original supplied pose and commits added=false. 0x004EA398..0x004EA496 returns robot+0x298 after root consistency diagnostics; it is not a history-query facade.
- 0x0067EEC4..0x0067EF20 and 0x0067EF92..0x0067F0BC: root-relative pose, object's slot+0x50 with padding0, type3 observable data versus base data for charger type4, fresh image stamps, ImportQuad2d then map virtual+8. Cache gets root-relative pose and added=true after insert, independent of its return value. The later scale-by2 temporary does not replace the global fill call arguments9/mask/10/time. The mask literal resolves to0x00C87675 (enum/value pairs).
- Root conversion helper 0x004DF628..0x004DF6DC copies a root directly, otherwise queries with respect to FindRoot; it logs/sets error on failed conversion. No world-origin fallback is justified.
- Removal 0x0067F2C4..0x0067F3DE and callback 0x006810E0..0x00681172 require existing type3 with matching ObjectID before replacing it. Type4 charger base data does not match. No blanket charger-footprint deletion.

## Height defect confirmed; bounded build released from existing checked evidence

Current M12-012 is IMPLEMENTATION_GAP, titled "What makes a cube a valid bottom to stack on: resting flat within ten degrees". Its authority/evidence already include IsPoseTooHigh0x00877954 and GetDimInParentFrame<'Z'>0x00557794 via GetRotatedParentAxis0x004AB8E4. M11-088 remains IMPLEMENTATION_GAP and its approved evidence explicitly requires robot-relative IsPoseTooHigh(1,15,0) at0x0067EDA2..0x0067EDC0. M11-074 remains IMPLEMENTATION_GAP; its evidence includes the O1-O8/cache/add ranges above and double Add caller order.

Reopened complete 0x00877954..0x008779C4, epsilon word0x008779C8=0x3727C5AC; 0x00557794..0x005577D4 with raw jump table; 0x005507A0..0x00550854; indexed matrix accessor0x00557890..0x005578A0. The native selector reads row2, columns0..2. Ordered strict greater comparisons select Y only if abs(Y)>abs(X), then Z only if abs(Z)>abs(selected); ties/unordered retain the earlier axis. GetDim maps abs(axis)3->size.z,2->size.y,other->size.x. Arithmetic and comparison are f32.

CubeGeometry.DimInParentFrameZ currently reads column2 and favors Z on ties. This contradicts the native helper for non-cubic charger dimensions. Existing RotatedParentAxis expresses row/strict-greater semantics. Current Mat3 stores float elements internally and its double indexer only widens them, so abs/comparisons on those widened values do not introduce a new rounding discrepancy. Reuse the existing row selector with the explicit passed matrix; keep the passed relative pose and object size. Add non-cubic row/column disagreement, ties, NaN and f32 boundary regressions. Unit tests must say they substitute matrices; no live map closure claim.

## Open questions and unreleased scope

Current MapTiming still has unbound relative-input, post-height and valid/valid cache recipients. SourceQuadTree still contains unbound geometry/callback recipients. The report's assertion that all tree dependencies are already recovered is a lead to match to their complete current inventory/implementation, not a full-tree PASS from this review. FastPolygon, overlap, expansion, ForceSet, ShiftRoot, cached-type callbacks and border consumers were not re-certified here; their report citations are not being adopted wholesale. Complete shared map build remains unreleased until that comparison. No new research queued.

The next bounded implementation is the confirmed height selector correction under existing M12-012/M11-088 evidence. It cannot settle M13-017, M13-040 or the map records; shared configuration, map/cache ownership, geometry and admitted-frame drive-off tests remain open. Claude spot-check required before acceptance of newly reviewed findings.
