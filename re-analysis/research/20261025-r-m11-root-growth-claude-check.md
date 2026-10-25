# Claude check: R-M11-ROOT-GROWTH (queue row 65, answer 817d279f)

- **Checked:** `research/20261024-m11-root-growth.md` against libcozmoEngine.so (SHA256 02263c07…e1) and the adopted
  rows it indexes.
- **Verdict: PASS.** It feeds B-M11-19.
- **The central claim holds.** Nothing new had to be read. UpgradeRootLevel, ShiftRoot and Expand were extracted in
  `20261013-quadtree-rest.md` and confirmed "instruction by instruction" by its Opus check
  `20261013-quadtree-rest-check.md`, adopted 2026-10-13, with the build rows sent to M11-045. They were never built: the
  stack still reports `MISSING: M11-048 UpgradeRootLevel` / `ShiftRoot` (`Vision/SourceQuadTree.cs:474`, `:509`). Drive-off
  robot run 2 hit them 21 times (`acceptance/hardware/20261025-084737-M13-DRIVEOFF-HW-760add`).
- **Spot-check.** All ten pasted instructions match the binary (capstone, Thumb):
  - 0x006876EE `cmp r7,#0xff` and 0x006876F6 `bhs.w 0x687a30` (level 255 refusal);
  - 0x00685260 `movs r2,#8` (max level 8);
  - 0x00687984 `str r7,[r3,#0x7c]` (parent);
  - 0x00687962 `blx 0x4bcf84`;
  - 0x00685322 `blx 0x4bcdbc` (the ShiftRoot stub, per known.py);
  - 0x00687398 `blx 0x4bcff0` (DestroyNodes);
  - 0x00685110 `mov r2,r8` and 0x00685112 `blx 0x4bcd98`;
  - 0x006873FE `movs r2,#1`.
- **Consistent with the stack:** the initial root, 160 mm at level 4, is already built (`SourceQuadTree.cs:378`,
  0x00684C28..48). So the answer's 2560 mm limit at level 8 follows from the doubling.

## Points for the builder

- **Budget:** the ordinary map wrapper passes a shift budget of 1. The merge passes its own (row 66). Don't hard-code 1
  inside the tree.
- **Expand order:** it ignores ShiftRoot's Boolean and re-tests Contains. Insert runs once, after the expansion, whether or
  not the polygon was contained. There is no grow-and-retry loop.
- **Area credit:** the explored-area credit is kept through the callback order, not recomputed. Don't "repair" it into a
  geometric union.
- **DestroyNodes,** not ClearDescendants's two-pass order, after a nonleaf shift.
