# Opus check of R-BACKUP-UNLOCK (2026-10-13)

- **Checked:** `research/20261013-backup-unlock-and-stoull.md` (Codex), against libcozmoEngine.so (E:) and
  libc++_shared.so (C:). Read-only cozmo-extractor; helpers are in `.scratch/bu/`.
- **Adopted:** by the manager on 2026-10-13. This file is a checked source for job B-M15-1. Use the Codex report together
  with the corrections below.
- **Result:** almost every cited body holds. There are five corrections (C1–C5) and new steps N1–N4.

## Corrections and new steps

- **C1, the live entry:** the stored std::function (vtable E:0x01020A18, slot 6 = 0x0052BE7A) goes to
  HandleRequestUnlockDataFromBackup 0x0051C5E4. **0x00529BC0 has no caller**, so it is dead.
- **The gate:** the subscription happens only if ctx->extIf is non-null (E:0x005257E8..0x005257EE). That is true in the
  stack.
- **C2, the stale destination:** if the unlock block holds fewer than 3 whole words and no zero, the copy loop reads past
  the unpacked words into an unconstructed buffer, whose first words are deterministic:
  - word 0 = 0x182000 (the find key, 0x0051C696);
  - word 1 = 13 (the "robotBackups/" size, 0x0051C646);
  - word 2 = a freed heap pointer;
  - word 3 onward = uninitialised.

  (Read at 0x0051C6BC, at most 0x200 words, stopping at the first 0.)
  - **The manager rules:** this falls under the **M1-034 standing rule** (operator, 2026-10-13): reading freed or
    uninitialised memory. The stack pushes the deterministic words 0 and 1 exactly as the original would, then stops
    before word 2 (the freed pointer), logs the skip visibly, naming M1-034 and M15-043, and carries on (it sends
    UnlockStatus with what was collected).
- **C3:** a readAsJson failure in InitConfig logs UnlockConfigJsonNotFound and then **continues**. A null root then also
  logs MissingDefaults. (The root state after a failed read is UNKNOWN.)
- **C4:** the "LargestInt out of UInt range" literal is at E:0x008E8F40.
- **N1, a per-entry Info:** InitConfig logs channel "ProgressionUnlockComponent.InitDefaults", event
  "ProgressionUnlockComponent.DefaultValue", "%s defaults to true" (EnumToString(id)) for **every** entry, duplicates
  included (E:0x0064BC74..0x0064BCD0).
  - A non-string entry gives Error "…InvalidDefaultData", errG, and the loop continues.
- **N2:** UnlockIdFromString's miss writes to cerr, "error: string '…' is not a valid UnlockId value", returns 0, and 0
  is inserted. The shipped file never reaches it. The name→value table (E:0x00794D84..0x007957AC) isn't decoded yet;
  Unity UnlockId.cs is the authority-2 cross-check.
- **N3:** UnlockStatus::Pack writes the count as a **u8** (E:0x00717D42), then every element. Only a crafted backup with
  more than 255 ids is affected. This is an app-boundary note: the stack's C# API carries the list.
- **N4, M15-033:** the committed map's rehash (E:0x0051DD18..0x0051DDA6) and __rehash (E:0x0051DDA8..0x0051DEC0) are
  standard libc++, now read. **__next_prime is fully read** (C:0x0003B038..0x0003B656: the small table up to 211, then
  the 210-wheel and trial division), so the "exact up to 211 buckets" limit is lifted.

## Confirmed as in the report

- **The unlock handler:** the context gate (the NullContext error, with an empty format) → dir → selector → parser →
  find 0x182000 → Unpack (non-owning buffer, ReadBytes(4) up to 0x200) → collect → Info "Found unlock data in backup file
  %s" → UnlockStatus{ids, 1}, broadcast through UiMessageHandler (app delivery first, then local subscribers).
- **The fallbacks:** NoBackup, FailedToParseBackup and NoUnlockData, each with its Info, then
  GetDefaultUnlocks(ctx) → UnlockStatus{defaults, 1}.
- **std::stoull** (C:0x00065378..0x0006550E):
  - it saves errno, sets it to 0, and calls strtoull (**bionic: EQUIVALENT_IMPLEMENTATION**);
  - ERANGE → out_of_range "stoull: out of range";
  - no conversion → invalid_argument "stoull: no conversion".

  **These throws fall under the M1-034 standing rule.** That supersedes the earlier "operator question pending" notes on
  M15-040 and M15-042.
- **JsonCpp asUInt and getMemberNames:** the dispatch, ranges and messages, as tabled.
