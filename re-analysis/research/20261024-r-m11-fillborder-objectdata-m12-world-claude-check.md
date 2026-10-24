# Claude check: R-M11-FILLBORDER (row 62, 4f539ae5), R-M11-OBJECT-DATA (row 63, e039f360), R-M12-PREACTION-WORLD (row 64, f57b04ac) — 2026-10-24

Manager spot-checks of `research/20261024-m11-fillborder.md`, `research/20261024-m11-object-map-data.md` and
`research/20261024-m12-preaction-world.md` (Codex), instructions re-read in libcozmoEngine.so. **All three PASS.**
No record settles. Each mostly reuses earlier checked rows (OV11 rows 1-22 and its check; OV42 O7/D1-D3 with the
ov42-44 check; the quadtree-rest Transform rows; OV04's derived constructor, MakeUnitLength and height rows; M12 gap3
Q2.1/Q2.4-Q2.10 with the G2/G3 corrections) and adds binding instructions, which were the parts re-read.

## Re-read

- FillBorderInternal 0x006813DC..0x006813FC: output type byte at sp+4, the same timestamp at sp+8/+0xC, base vptr at
  sp, processor map+0x10, call 0x004BCA68, then map+8 = 1.
- MemoryMapData_ObservableObject ctor 0x00684A42..0x00684A78: type 3 at +4, the timestamp at +8 and +0xC, the
  derived vptr at +0, the ObjectID vptr and value (object +0x18) at +0x10/+0x14, then the polygon vector copy at +0x18.
- Removal callback 0x006810F2..0x00681110: type byte compared with 3 before the derived ID at +0x14 is read and
  compared with the captured ID.
- Derived world ctor 0x0050DF54..0x0050DF92: the first scalar `a` stored at +0x18; the stored local translation copied
  and passed to MakeUnitLength (0x0050DF6C); the second scalar `b` loaded from the caller's stack (0x0050DF74);
  s = F(len + b) (0x0050DF7C); each component F(s * t_i) (0x0050DF92).
- Charger validity 0x004EA648: `b.w` to the base (no charger-specific test).

## Builder jobs released

- B-M13-23: the border processor (FillBorderInternal + FillBorder) behind the object-add and overhead type-10 calls.
- B-M13-24: content-3 object data, cubes into the native map, and whole-map Transform removal.
- B-M13-25: the derived world constructor and the validity filter for pre-action poses (cubes and charger).
