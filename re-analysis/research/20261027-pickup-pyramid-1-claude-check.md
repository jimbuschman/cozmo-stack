# PICKUP-PYRAMID-1: Claude check (2026-10-27)

Branch `codex-m13-manager-2` at 0936210c (production 283d6b6e, fixtures 951b559b, N4 a01b897c), merged to main.
Codex's independent checks and its full gate (6378/6378, 0 skipped) are the build evidence; this check spot-checks
claims against the binary. The behaviour is not finished: native pickup children and activity admission still
report MISSING.

**PASS, merged.**

- **Pickup Verify 0x00553BE0..0x00554056:** the start stamp at +0x10C comes from Robot+0x2C on first entry; a failed
  dock (0x00553C06/0x00553C0A) bypasses the prefix; with +0xC1 set and +0xC2 == 0 the first pass stores the deadline
  and every pass returns Running (0x01000000), logging once it has expired (0x00553C2C..0x00553C70). +0xC1 is always 1
  for PickupObjectAction (ctor 0x005536F8), so `DockActions.cs` VerifyPrefix testing only +0xC2 has the same result.

**Settled: M15-044** (IMPLEMENTATION_GAP -> EXACT_SOURCE).
- `NativeUnlockNames` (from `UnityUnlockId.cs`) equals the 81 pairs of the emulated builder 0x00794D84..0x00795834
  (`.scratch/rsmall2/unlock_pairs.txt`, compared name for name and value for value), gaps at 4/7/8/58/60, Count 85.
  A miss writes the std::cerr line and returns 0.
- `BackupFiles.ReadJson`: a failed open still parses an empty stream into the given root and logs
  DataPlatform.readAsJson; `InitUnlockConfig` follows rows 5-6 of `20261013-small-reads-2-check.md` (NotFound then
  continue, the defaults-set guard, MissingDefaults for a null root or a non-array, an object root gains a null
  member through `Json.Member`, per-element string/InvalidDefaultData, the std::set as a SortedSet, fresh null roots
  from both callers, M1-034 for a non-object root).
