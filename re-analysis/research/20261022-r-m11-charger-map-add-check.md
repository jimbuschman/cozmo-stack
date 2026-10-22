# Independent review: R-M11-CHARGER-MAP (2026-10-22)

**Claude spot-check pending**

Reviewed answer: `research/20261021-m11-charger-map-add-join.md` (HEAD 19b0a313).
Prior Codex review: `research/20261021-m11-charger-map-add-check.md`.
Independent re-read of cited instructions in `resources/lib/armeabi-v7a/libcozmoEngine.so` via `re-analysis/tools/known.py`. No code, inventory, manifest or queue job changes beyond this file and the QUEUE row status.

## Cited instructions re-read

- MapComponent::UpdateObjectPose 0x0067EA90..0x0067EC4C (452 bytes): family6 early return, invalid-state branches, per-object outer cache + root-ID inner lookup, false-cache retry, true-cache IsSameAs with 5.0f each axis and Radians 0x3E32B8C2; slot+0x5C==1 removes then adds; missing inner entry adds. Confirmed.
- Family mapping 0x0067ECFC..0x0067ED7A: family table at 0x0067F4C0..0x0067F546; family4 selects type4 for add and type5 for removal. Confirmed.
- 0x0067ED7E..0x0067EE30: actual robot pose input, relative-query result==1, height args 1/15/0; high branch copies original supplied pose and commits added=false. Confirmed.
- 0x004EA398..0x004EA496: Robot::GetPose entry/wrapper, returns robot+0x298; not a history-query facade. Confirmed.
- 0x0067EEC4..0x0067EF20 and 0x0067EF92..0x0067F0BC: root-relative pose, slot+0x50 with padding 0, type3 vs base data for charger type4, fresh image stamps, ImportQuad2d then map virtual+8; cache gets root-relative pose + added=true after insert. Confirmed.
- Root conversion helper 0x004DF628..0x004DF6DC: copies root directly, otherwise queries FindRoot; logs/sets error on failed conversion; no world-origin fallback. Confirmed.
- Removal 0x0067F2C4..0x0067F3DE and callback 0x006810E0..0x00681172: require existing type3 with matching ObjectID before replacing; type4 charger base data does not match. Confirmed.
- IsPoseTooHigh 0x00877954..0x008779C4: epsilon 0x3727C5AC; GetDimInParentFrame<'Z'> 0x00557794..0x005577D4 with row2 strict f32 selector (ties/NaN keep earlier axis). Confirmed.
- GetRotatedParentAxis 0x005507A0..0x00550854: row 2 via 0x00557890..0x005578A0. Confirmed.

## Findings

All checked findings in the prior Codex review (20261021-m11-charger-map-add-check.md) hold against the binary. The height defect (CubeGeometry.DimInParentFrameZ reads column 2 and favors Z on ties vs native row-2 strict selector) is confirmed. The family4/type4-add/type5-remove mapping and the type3-matching removal quirk are confirmed. The valid/valid cache path, post-height continuation, and per-object/per-origin cache ownership remain genuinely open (no production owner found). No new unread behavior-changing callee identified in the requested path.

## Finish checklist (cozmo-one-job.md)

1. Job done: independent review of R-M11-CHARGER-MAP (research lane).
2. Engine instructions re-read myself: 0x0067EA90..0x0067EC4C, 0x0067ECFC..0x0067ED7A, 0x0067ED7E..0x0067EE30, 0x004EA398..0x004EA496, 0x0067EEC4..0x0067EF20, 0x0067EF92..0x0067F0BC, 0x004DF628..0x004DF6DC, 0x0067F2C4..0x0067F3DE, 0x006810E0..0x00681172, 0x00877954..0x008779C4, 0x00557794..0x005577D4, 0x005507A0..0x00550854, 0x00557890..0x005578A0, 0x0067F4C0..0x0067F546.
3. Hooks/seams added: none (review only).
4. Anything added that the job didn't ask for: none.
5. Tests: none added (review only).
6. Focused tests: N/A (no code change).
7. Records: none settled. M11-074, M11-088, M12-012 remain IMPLEMENTATION_GAP.

No new research queued. No code edited. No record settled.