# Manager check: the TurnInPlaceAction destructor's eye cleanup (M13-022), 2026-10-15

- **Why:** B-M13-4 stopped BLOCKED because this body had no adopted checked row. The research lead was
  `research/20261007-child-actions-rows.md` X2.
- **What was read:** the manager disassembled 0x00545B94..0x00545C00 directly with `da.py`, and decoded the two ARM
  veneers by hand. This adopts the following as checked rows.

## D1 `~TurnInPlaceAction` (0x00545B94..0x00545BD4)

1. **The vtable is installed first:** GOT word + 8 → [this] (0x00545B98..0x00545BA8).
2. **The eye cleanup runs only when both bytes are non-zero:** byte +0xD8 (0x00545B9A/0x00545BA0, `beq` → skip) and byte
   +0xD9 (0x00545BAC, `cbz` → skip).
   - The call is `TrackLayerComponent::RemoveEyeShift(this.robot(+4)->+0xC0, tag = byte +0xD9, duration = 0)`, at
     0x00545BB2..0x00545BBC. r1 is the u8 at +0xD9, still in r1 from the guard load, and r2 = 0.
   - Then +0xD9 = 0 (0x00545BC0).
3. **Then the C4 handle container is released:** if the control block at +0xE4 is non-null, release_shared
   (0x00545BC4..0x00545BCA). The shared_ptr<ScopedHandleContainer> sits at +0xE0/+0xE4; the landing pad at 0x00545BDA
   confirms it.
4. **Then the base:** a tail call through veneer 0x008CB50C → PLT 0x004AAB4C `IActionRunner::~IActionRunner`
   (0x00545BD4). The veneer words are 0x46C04778, 0xE59FC000, 0xE08CF00F, literal 0xFFBDF630.
5. **No log and no message.**
6. **The landing pad** (0x00545BD8..0x00545BEC) does ~shared_ptr(+0xE0), ~IActionRunner, then 0x004E39F8, which is
   unwind only.

## D0 (0x00545BF4..0x00545BFE)

D1, then a tail call through veneer 0x008CA88C → PLT 0x004A40CC, operator delete. Its literal is 0xFFBD9830.

## What this settles for B-M13-4

The persistent eye layer added at Init (0x00546286) is removed by the destructor exactly as above:
- only if +0xD8 is set and the tag at +0xD9 is non-zero;
- with RemoveEyeShift(tag, 0);
- the tag is then zeroed;
- the C4 owner is released after the eye removal, before the base destructor.

The normal and cancel completion paths reach this destructor. CheckIfDone's own eye-removal branch is already checked
(OV07).
