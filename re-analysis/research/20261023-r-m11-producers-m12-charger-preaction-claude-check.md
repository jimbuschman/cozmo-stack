# Claude check: R-M11-MAP-PRODUCERS (row 60, 393bc407) and R-M12-CHARGER-PREACTION (row 61, 475c6ebc) — 2026-10-23

Manager spot-checks of `research/20261023-m11-map-legacy-producers.md` and `research/20261023-m12-charger-preaction.md`
(Codex), instructions re-read in libcozmoEngine.so. **Both PASS.** No record settles.

## R-M11-MAP-PRODUCERS

Mostly consolidates rows already extracted and checked (R-VIS pre item 6 rows 31-49 with its objection A; OV42 R1-R6
with the ov42-44 check; OV80 E1/E3 with the 20261016 batch check). Re-read:
- UpdateRobotPose order: cliff byte +5 tested at 0x0067E336..0x0067E338; cliff insert 0x0067E398 or clear-of-cliff
  (type 2, 0x0067E3C2) insert 0x0067E3F0; robot footprint GetBoundingQuadXY 0x0067E41E with type 1 (0x0067E424) inserted
  at 0x0067E450; ProcessClearQuad 0x0067E47E; saved-pose assignment 0x0067E486. Matches rows 2c-2f.
- AddVisionOverheadEdges clear stage: width test f32 squares and sum compared with s18 (0x0067FF50..0x0067FF5C); the
  threshold bits 0x43610001 = 225.0000153; ProcessClearQuad on the original clamped quad at 0x006801AE..0x006801B0.
  Matches rows 1d-1g.
The answer also notes that M11-017's evidence wording ("triangle ... or a line to its midpoint") is contradicted by
PC40-41 (three alternatives chosen by the two width tests). Its unresolved already says to build the ClampQuad clear
quad; the manager corrects the evidence wording when M11-017 is next worked.

## R-M12-CHARGER-PREACTION

Re-read in Charger::GeneratePreActionPoses (0x004E9FB0): the supplied-vector clear loop ending 0x004E9FD2; unsigned
type > 1 returns empty (0x004E9FD4..0x004E9FD6); the angle add `vadd.f32` (0x004E9FF2); z = 0xC1780000 (-15.5f,
0x004EA016..0x004EA01A) and the y `vneg.f32` (0x004EA01E); the capacity-path constructor call through PLT 0x004A47F8
(0x004EA0F4..0x004EA0FE) with type from [sp+0x40], the owned marker [sl], the temporary pose at sp+0x34 and a zero
float on the stack; end += 0x1C (0x004EA104). Matches rows 1a, 2c, 2d.

## Builder jobs released

- B-M13-20: UpdateRobotPose writes onto the native tree (rows 2a-2f).
- B-M13-21: AddVisionOverheadEdges writes onto the native tree (rows 1a-1j), then remove the legacy-region fallback in
  HasCollisionRayWithTypes once nothing writes legacy regions.
- B-M13-22: the charger's stored pre-action elements (rows 1a-2f) in GenerateStored.
