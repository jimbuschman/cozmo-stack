# GET-SIZE-SLOT-RECORD-1 adopted: M11-105 added; M13-007 and M12-036 corrected (manager, 2026-11-13)

Worker note: `re-analysis/jobs/trial-m13/INVENTORY-NOTE-GET-SIZE-SLOT-RECORD-1.md` (commit a3470375; records only).

## Re-read in the `.so`

| Address | What | Matches the note |
| --- | --- | --- |
| 0x00557794..0x005577D2 | GetDimInParentFrame<'Z'>: GetRotatedParentAxis, `adds r0,#3; cmp r0,#6; tbb`; three arms each `ldr r1,[r0,#0x2C]; blx r1`, then +8 / +4 / +0 | yes (S1) |
| 0x005577AA | the tbb table bytes [4,10,16,16,16,10,4]: axis ±3 → z, ±2 → y, else x | yes |
| 0x004E382C..0x004E383A | `adds r0,#0x88; bx lr`, and the virtual thunk at 0x004E3830 | yes (S4) |
| 0x004E9D12..0x004E9D2E | `movt #0x42C0` → [+0xF0], `movt #0x42A0` → [+0xF4], `movt #0x41F8`: 96, 80, 31 | yes (S5) |
| 0x00502DD0 | `adds r0,#0x58; bx lr` | yes (S6) |
| vtable words | 0x0101DBEC and 0x0101CE18 = 0x004E3831; 0x0101E1F0 = 0x004EA673; 0x0101F344 = 0x00502DD1 | yes |
| 0x0062606E..0x00626070 | `mov r0,r4` (the reference object), `blx` GetDimInParentFrame<Z> | yes (S2b) |

## Adopted (standing authorisation for cited inventory corrections)

- **M11-105, new, IMPLEMENTATION_GAP (M11-vision):** the per-class size slot +0x2C and its readers. A new record
  rather than an M13-025 extension, as the note recommends: the slot is on the paths of M13-007, M12-036, M12-012 and
  M12-033, and M13-025 owns the +0x50 footprint. The getter is built (ObservableSize2C); the reference side and the
  other GetDimInParentFrame<Z> readers are open. Tagged at ObservableMapSlots.cs:17.
- **M13-007 authority and evidence[0]:** refPose is GetWithRespectToRoot(obj->GetPose()) (0x00626048 bl 0x004DF628),
  the root-frame pose (confirmed in FIND-ON-TOP-SAME-ROOT-1, 2026-11-13). The eleven callers and tolerances stand.
- **M12-036 authority and evidence:** the rz / centre z / exclusion ranges split as cited; 0x00626BB5 → 0x00626BB4.
- **The RollBlockHelper filter (note §3):** families {1,2}, 30 mm, onTop, current origin; the stack matches. Nothing
  to change.

Re-approved: M11-vision, M12-manipulation, M13-navigation. No record settled.

## Answers to the note's questions

1. M11-105 in M11-vision.
2. S2b (the reference size) is a live gap now: job REFERENCE-SIZE-SLOT-1. The S8 readers stay in M11-105's
   unresolved census until a job owns them.
3. The unsearched writers of Charger+0xF0 / Block+0x88 and the <X>/<Y> bodies stay listed in M11-105's unresolved;
   REFERENCE-SIZE-SLOT-1 reads them only if its path reaches them.

## Not adopted now (queued)

- M13-025 location (the live +0x50 path is ObservableMapSlots.cs / NativeConfigurationGeometry) and its stale
  "built locally in Neighbor" note.
- M11-003's evidence "0x004E4D8E movt" → 0x004E4D90.
