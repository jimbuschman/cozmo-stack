# M8-framework gap pass 3 — four corrected rows

Agent: cozmo-extractor (read-only). Date: 2026-09-28.
Primary source: `resources/lib/armeabi-v7a/libcozmoEngine.so` (ELF ARMv7/Thumb). Every citation is an address in that file with the instruction at it. Raw addresses were disassembled with the existing scratch helper `.scratch/disasm_addr.py`, started before each IT block so capstone keeps the condition suffixes. `re-analysis/decomp/libcozmoEngine/` was not needed.

Result: the verifier is right on all four items. The three addresses were off by one instruction (A1), by one instruction (C3) and by six bytes into a 4-byte `add.w` (C4c); the enum names were wrong. The gap-2 mapping (`0 = CliffDetected`, `0x14 = UnexpectedMovement`, `0x15 = Count`, `0x16 = NoneTrigger`) is confirmed independently from the table and from `ReactionTriggerFromString`.

| step | what the original does | citation (address + instruction) | record | classification |
|---|---|---|---|---|
| A1 (corrected) | Row 5, current-behaviour class test against `0x2e`. `0x005a2fe0` is the compare (`0x005a2fde` is `movs r6,#0`), and the manager flag store is at `0x005a305c`, the third instruction of the `ittt eq` block that starts at `0x005a3056`; `0x005a305a` is `moveq r1,#1`, not the store. | `0x005a2fe0 cmp r7,#0x2e`; `0x005a2fd6 ldrb.w r7,[r7,#0x64]`; `0x005a3054 cmp r6,#1`; `0x005a3056 ittt eq`; `0x005a3058 ldreq r0,[r4,#0x30]`; `0x005a305a moveq r1,#1`; `0x005a305c strbeq.w r1,[r0,#0x220]` (gap-1 cited `0x005a305a strbeq.w`) | NEW (row 5) | EXACT_SOURCE |
| C3 (corrected) | `IBehavior::SmartDelegateToHelper`: on a successful `DelegateToHelper` the weak ref is taken by `blx __add_weak` at `0x005bec6e`; `0x005bec6c` is `mov r0,r5`. | `0x005bec66 ldrd r7,r5,[r5]`; `0x005bec6a cbz r5,#0x5bec72`; `0x005bec6c mov r0,r5`; `0x005bec6e blx #0x4a5020` (PLT `std::__ndk1::__shared_weak_count::__add_weak()`); `0x005bec72 str.w r7,[r4,#0xc4]`; `0x005bec7a str.w r5,[r4,#0xc8]` (gap-1 cited `0x005bec6c blx add_weak`) | M8-001 | EXACT_SOURCE |
| C4c (corrected) | `BehaviorManager::RemoveDisableReactionsLock`: the `__tree::find` on the trigger map is at `0x005a3aa8`; `0x005a3aae` is the second halfword of the 4-byte `add.w r1,r7,#0x24` at `0x005a3aac`, not a call. | `0x005a3a9e add.w r0,r7,#0x20`; `0x005a3aa4 ldrb.w fp,[r7,#0x10]`; `0x005a3aa8 blx #0x4b0da4` (PLT `std::__ndk1::__tree<...std::__ndk1::basic_string...>::find<...>(... ) const`); `0x005a3aac add.w r1,r7,#0x24`; `0x005a3ab0 cmp r0,r1`; `0x005a3ab2 beq #0x5a3b76` (gap-1 cited `0x005a3aae blx __tree::find`) | M8-001 / M7-014 | EXACT_SOURCE |
| B5 (corrected) | `IBehavior::Resume`: the two special triggers are `0x14 = UnexpectedMovement` and `0 = CliffDetected` (not `Count` / `NoneTrigger`). Numeric control flow unchanged: `cmp r5,#0x14` / `cmpne r5,#0` / `bne 0x5bcf7e`; for those two it reads `+0x114`, and only when the pre-increment value is `>= 1` does it set `+0x118 = now + 15.0`, build `"TooManyResumesCliffOrMovement"` and call `MoodManager::TriggerEmotionEvent`; the first such trigger (`+0x114 == 0`) still takes the normal resume path. The suppression window (`+0x118`) is consumed by `IsRunnableScored` (`0x005bda28`). | table `0x10337c0`: `+0x00 -> 0xc1ae42 "CliffDetected"`, `+0x50 -> 0xc1258b "UnexpectedMovement"`, `+0x54 -> 0xc1966a "Count"`, `+0x58 -> 0xc1af22 "NoneTrigger"`; `0x0077065c cmp r0,#0x16`; `0x00770668 add r1,pc`; `0x0077066a ldr.w r0,[r1,r0,lsl #2]`; `ReactionTriggerFromString 0x00770674`: `0x007706c2 strb.w r6,[sp,#0xc]` (r6=0, `CliffDetected`), `0x007708e6 strb.w r0,[sp,#0x14c]` (r0=0x14, `UnexpectedMovement`), `0x00770902 strb.w r0,[sp,#0x15c]` (r0=0x15, `Count`), `0x00770914 strb.w r1,[sp,#0x16c]` (r1=0x16, `NoneTrigger`); string `0x5bd04c "TooManyResumesCliffOrMovement"` (via `0x005bcf44 adr r1,#0x104` -> `0x005bd04c`); `0x005bcf16 cmp r5,#0x14`; `0x005bcf18 it ne`; `0x005bcf1a cmpne r5,#0` | M8-001 | EXACT_SOURCE |

## Notes on the B5 re-read

- `EnumToString(ReactionTrigger)` (`0x0077065c`) bounds-checks `r0 > 0x16` and otherwise indexes the pointer table at `0x10337c0`; I read the 23 entries directly from the file and they match the gap-2 table exactly.
- `ReactionTriggerFromString` (`0x00770674`) builds the map with a 23-element `initializer_list` of `{std::string, ReactionTrigger}` pairs (16 bytes each). The value byte for entry N is written at `sp+0xc+0x10*N` after the next string is loaded; the four addresses above are the stores for indices 0, 0x14, 0x15 and 0x16. This is independent of the `EnumToString` table and agrees with it.
- `0x5bd04c` is the string address reached by `adr r1,#0x104` at `0x005bcf44`; the `TooManyResumesCliffOrMovement` name is consistent with the corrected trigger names (`CliffDetected` + `UnexpectedMovement`).
- The gap-1 open question 2 ("names unverified") and gap-1 contradicted-record item 12 are now settled by this pass.

## Existing records contradicted by the source

1. **`20260928-I-M8-gap1-extraction.md`, A1** — cited `0x005a305a strbeq.w r1,[r0,#0x220]`. `0x005a305a` is `moveq r1,#1`; the store is `0x005a305c` (inside `ittt eq` at `0x005a3056`).
2. **`20260928-I-M8-gap1-extraction.md`, C3** — cited `0x005bec6c blx add_weak`. `0x005bec6c` is `mov r0,r5`; the call is `0x005bec6e`.
3. **`20260928-I-M8-gap1-extraction.md`, C4c** — cited `0x005a3aae blx __tree::find`. The call is `0x005a3aa8`; `0x005a3aae` is inside `add.w r1,r7,#0x24` at `0x005a3aac`.
4. **`20260928-I-M8-gap1-extraction.md`, B5** — labels `0x14 = Count` and `0 = NoneTrigger` are wrong; they are `0x14 = UnexpectedMovement` and `0 = CliffDetected` (`Count = 0x15`, `NoneTrigger = 0x16`).

## Existing records whose evidence is too weak to keep their status

None new in this pass. The M8-001/M8-003/M8-002/M8-009 evidence weakness flagged in gap 1 stands unchanged; these four corrections do not alter it.

## Open questions for the manager

None. All four items were resolvable from the primary source; no UNKNOWN remains. The only remaining task is to fold these four rows into the gap-1 report (or supersede it) and correct the M8-001 record text that used the old trigger names.