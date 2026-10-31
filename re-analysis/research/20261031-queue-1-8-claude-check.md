# ChatGPT queue items 1-8: Claude check (manager, 2026-10-31)

**Verdict: quotes PASS; two interpretations are wrong, and the naming error from item 1 is still in the file. Nothing settled; no
status changed.** The report is `research/20261031-queue-1-8-chatgpt-extraction.md`. Use this note where the two differ. Same binary
hash. Checks were samples, weighted to items 6-8 and to conditions and labels. A row not named here was not re-read.

## Corrections

**1. Item 3 step 3.9: `inventoryIsFull` is wrong.** The report says `(space < awarded) && (awarded != 0)`. The second condition is on
space, not on the amount. At 0x0069965C..0x00699674, `adds r2,#1` sets the flags from `[sp,#0x10]`, the space remaining, so
`movne r2,#1` means space != -1. The correct rule is **`space < awarded && space != -1`**. Space comes from
GetInventorySpaceRemaining 0x0063D2E8: -1 when there is no cap, otherwise `max(cap - current, 0)`.

**2. Item 7: RobotObservedPet's first two field labels are wrong.** The offsets are right; the meaning is not. The report treats the
cached +0x14 as the timestamp because the rotation check reads +0x14. Those are two different objects. The rotation check reads an
input list node, whose TrackedPet starts at +0x08 (0x0050B83A). The cached value is a tree node, with the key at +0x10
(0x0050B846) and TrackedPet at +0x14. So the input +0x14 is TrackedPet +0x0C, and the cached +0x14 is TrackedPet +0x00.

With the Unity class `RobotObservedPet` (petID, timestamp, numTimesObserved, score, img_rect, petType), the payload is:

| Payload | Field | Cached source |
| --- | --- | --- |
| +0x00 | petID (int) | +0x14 |
| +0x04 | timestamp (uint) | +0x20 |
| +0x08 | numTimesObserved (uint) | +0x18 |
| +0x0C | score (f32) | +0x1C |
| +0x10 | img_rect (CladRect) | +0x28..+0x34 |
| +0x20 | petType (u8) | +0x24 |

The report's UNKNOWN for +0x04 and +0x0C is closed by this.

**3. Item 1 step 1.5** still has the wrong names. See `research/20261031-m12-049-callbacks-claude-check.md`.

## Checked and matching

- **Item 2:** filter data 0x00C77770; operator== 0x006174F8..0x00617518 (ordered ids; a base with two equal ids never matches); the
  CastNewEntry compare loop 0x0062C690..0x0062C6AC. The prune body and layout tables were not re-read.
- **Item 3:** dispatch 0x00699448..0x0069945A; level fields 0x00699476..0x006994CE (r5 holds the manager there, r7 the level); the type 2
  write +0x68 and the optional counter 0x006995F8..0x00699612.
- **Item 4:** the initial string `control` (0x006923FC). The value 0x0069718D appears only in the dynamic symbol table, and no relocation
  names the setter. I did not repeat the branch sweep. No caller was found, so the default `needs_level_config.json` is the working
  assumption, not a proven fact.
- **Item 5:** the threshold scan 0x0069C1B0..0x0069C1D2. The stack already has this method (Needs.cs:392).
- **Item 6:** enum 0x0077D3B2, getter 0x0063CFF8, add 0x0063D066, cap logic 0x0063CEF4..0x0063CF14, throttle 0x0063CDE8..0x0063CE1C
  (4,999,999 against the clock), whole-list send 0x0063D31E..0x0063D34C, tag 111 0x00726BCE. The shipped cap is Sparks 999. Unity
  agrees with InventoryType and tag 111.
- **Item 7:** the rotation gate 0x0050B8C8..0x0050B8F4, the +0x25 and count diagnostics 0x0050B9AC..0x0050B9B8, the moves
  0x0050BA2A..0x0050BA68 and tag 72. The Unity Tag enum agrees: RobotObservedPet 72, FreeplaySparksAwarded 101, StarLevelCompleted 102,
  StarUnlocked 103, InventoryStatus 111.
- **Item 8:** the handler 0x005A3C70..0x005A3C9A, SetRequestedSpark, the switch reads and writes, the desired-activity reads, and the
  +0x90 selection, read and clear. All match.

## What this gives the build

- **M15-093:** the ProcessLevelRewards branches (types 0, 1, 2, 3) with the correction above.
- **AwardSparks, final amount:** stored inventory is capped at 999, but the returned award is not, so a broadcast can report more
  than was stored.
- **M12-049:** the cache half is read, except the producer of the full-pyramid list and readers of +0x15C.
- **M14-014:** the broadcast is read. Every admitted pet in the rebuilt cache is broadcast per update, with the correction above. The
  source of the pet input list is unread.

## New gap with no record

InventoryComponent's write path has no record; M15-035 covers only the NV read callback. Its scope is the cap, the 5 second
throttle, and the NV write. The NV write is robot traffic, item 1 of "What we reproduce". The stack has the read side only
(NurtureRestoration.cs). Proposal goes to the operator.

## Method note

Over eight items, every sampled quote was exact, and its own count of 778 checked addresses is believable. But its check covers
quotes only, and both errors are in what the quotes mean: one flag-setting instruction, and one field offset read against the wrong
base. A citation check alone would have passed both. Quality did not drop towards the end of the queue: item 8 is clean, and the
errors are in items 3 and 7. Claude's checks must read conditions and base objects, not just match instructions. Names should be
checked against the Unity CLAD classes.
