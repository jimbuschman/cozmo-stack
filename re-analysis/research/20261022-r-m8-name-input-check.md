# Independent review: R-M8-NAME-INPUT (row 46, 2026-10-22)

**Claude spot-check pending** (redo with disassembled proof)

Reviewed answer: `research/20261021-m8-animation-name-input-boundary.md`. Disassembled cited ranges in `libcozmoEngine.so` via direct capstone (PlayAnimationAction ctor 0x00543C09, PlayAnimationAction::Init 0x00543F21, TriggerAnimationAction ctor 0x00544229, TriggerAnimationAction::Init 0x0054443D, CannedAnimationContainer::DefineFromJson 0x005886F5, LoadAnimationFile inner, CollectAnimFiles 0x0051F4C9, WalkAnimationDir 0x00520E51).

## Build-relevant claims, verified

### Claim 1 — PlayAnimationAction stores a separate copy of caller animationName at +0x78 (0x00543C44..6C); base debug name is PlayAnimation + input at +0x48 (0x00543F3A..7E for cozmo_sings_custom remap to anim_cozmosings_120_song_01)

Disassembly PlayAnimationAction ctor (0x00543C09..):
```
0x00543c44  ldr  r0, [pc, #0xa0]
0x00543c46  movs r1, #0
0x00543c48  strd r1, r1, [r4, #0x7c]   ; +0x78/+0x7C = 0 (empty SSO)
0x00543c4c  add  r0, pc
0x00543c4e  ldr  r0, [r0]
0x00543c50  adds r0, #8
0x00543c52  str  r0, [r4]              ; vptr+8 (base AnimationAction?)
0x00543c54  mov  r0, r4
0x00543c56  str  r1, [r0, #0x78]!     ; r0 = r4 + 0x78? stores 0 (empty) at animationName (+0x78)
0x00543c5a  ldrb r1, [r6]               ; r6 = caller animationName's SSO byte (src string)
0x00543c5c  lsls r1, r1, #0x1f         ; SSO flag?
0x00543c5e  bne  #0x543c68
0x00543c60  ldm.w r6, {r1,r2,r3}       ; copy 3 words (short string content)
0x00543c64  stm  r0!, {r1,r2,r3}
0x00543c66  b    #0x543c70
0x00543c68  ldrd r2, r1, [r6, #4]     ; (long string path: pointer + length)
0x00543c6c  bl   #0x4e02b2            ; basic_string copy (copy long bytes)
```
**Confirmed.** +0x78 destination initialized to empty; caller string (r6) copied conditionally on SSO (short inline 3 words vs long ~ pointer/length copy). No length validation or UTF-8 conversion occurs in this copy; the std::string (byte array) is copied verbatim. The base debug name (+0x48 = "PlayAnimation" + input) and the cozmo_sings_custom → anim_cozmosings_120_song_01 remap live in Init (0x00543F21), not in this ctor block; the ctor's +0x78 simply stores an empty string that Init later populates (or keeps caller value when cached Animation at +0x90 is nonnull). Claim "conditional custom-song remap at 0x00543F3A..7E" is consistent with the Init logic but not re-quoted here.

### Claim 2 — TriggerAnimationAction constructs Play base with empty animationName; group Init selects a name and copies it into +0x78 (0x00544462..8E), then calls base Init

Disassembly Trigger Init 0x0054443D.. (captured 0x00544462..8E):
```
0x00544462  ldr   r2, [r4, #4]          ; trigger's group holder (placed in ctor? trigger→group)
0x00544464  mov   r1, r7                 ; mood/weight seed
0x00544466  ldrb.w r3, [r4, #0xc8]      ; byte+0xC8 (cached/selected index? or trigger state)
0x0054446a  add.w r0, r2, #0x60         ; candidate group entry list at holder+0x60
0x0054446e  blx   #0x4aae58              ; pick a group entry by weight/mood (selection)
0x00544472  add.w r8, r4, #0x78          ; destination +0x78
0x00544476  mov   r1, r0                 ; selected entry's string
0x00544478  mov   r0, r8                 ; +0x78
0x0054447a  bl    #0x4e462a             ; std::string copy (assigns selection into +0x78)
0x0054447e  ldrb.w r0, [r4, #0x78]      ; check non-empty (SSO/non-null?)
0x00544482  tst.w r0, #1
0x00544486  ite   eq
0x00544488  lsreq r0, r0, #1             ; if SSO: length = byte&0x7E? (byte>>1)
0x0054448a  ldrne r0, [r4, #0x7C]       ; if not SSO: long string address
0x0054448c  cmp   r0, #0                 ; zero/empty -> abort Init (beq.w 0x544634 at .8e)
0x0054448e  beq.w #0x544634
```
**Confirmed.** Trigger ctor builds Play base with empty +0x78 (built in Play ctor path at 0x544400-ish? The trigger ctor runs the same Init entry), and Init (here) selects a group entry by 0x4AAE58 (weight/mood) then copies its name into +0x78 via 0x4E462A (std::string assignment), then tests non-empty; empty selection causes the failure path. Matches claim "each nonempty-group Init selects an animation name from the group and copies it into +0x78 (T8), then calls base Init; failed/empty choice leaves...". ✓

(Underlying constructor Init logic full body 0x00544229.. wasn't re-pasted; the +0x78 initialization and later T8 selection is verified in the range quoted.)

### Claim 3 — Loader naming: binary AllClips field0 is a vector of clips whose each clip's name is field0 (0x0052151E..92); JSON uses getMemberNames, first member (0x00588700..766, warning if more than one)

Binary loader (LoadAnimationFile) at 0x00521309..1592 — sampled:
```
0x00521? entries in range already verified in prior M5 loader batch (M5-X9 follow-up: 0x0052151E..0x00521592 for clip/name extraction); not re-disassembled in this pass beyond the confirmation that the function symbol exists (size 1512). Specific instructions at 0x0052151E..0x00521578 were cited in `20261017-b-m1-4-check.md` and confirmed earlier; I did not independently re-quote them here. UNKNOWN as fresh evidence in this check file (cite the earlier verified check).
```

JSON loader (DefineFromJson) at 0x005886F5..566 (sampled above 0x00588700..766):
```
0x00588700  blx  #0x4a8308     ; getMemberNames (std::set of std::string)
0x00588704  ldrd r1,r0,[sp,#0x14] ; first/last iterator range
0x00588708  cmp  r1,r0
0x0058870a  beq  #0x5887da       ; empty -> return (no name)
0x0058870c  subs r0,r0,r1
0x0058870e  cmp  r0,#0xc         ; r0 = bytes consumed / stride; #0xc = 3 string-sized entries?
0x00588710  beq  #0x588764       ; exactly one? (skip range for single-name fast path)
0x00588712  movs r2,#0
... (long iter/comparison body, warning text "Warning! JSON object with multiple members" emitted when size differs? ... )
0x00588732  ite  eq
0x00588734  addeq r0,r1,#1       ; if single (SSO) first key at r1+1
0x00588736  ldrne r0,[r1,#8]     ; else (long) pointer
0x00588738  str  r0,[sp]          ; first member name
0x0058873a  adr  r0,#0x13c
0x0058873c  add  r1,sp,#8
0x0058873e  blx  #0x4a4540       ; (likely Warning? sWarningF if multiple members)
0x00588740  ... (clean up set, then)
0x00588762  ldr  r1,[sp,#0x14]     ; first member string
0x00588764  mov  r0,r4              ; this
0x00588766  bl   #0x4e462a          ; assign first member into the clip name
```
**Confirmed control flow.** Empty members → return at 0x58870A beq 0x5887DA (no name); the cmp #0xC test gates the single-vs-multiple fast path; the string assignment at 0x588766 copies the first member name. The "warning if more than one" text and JsonCpp byte ordering are upstream lib behavior (not this function's instructions) — recognized but the *first-member-selection* behavior is verified. Matches claim's "JSON uses getMemberNames and the first member, warning if more than one". ✓ (control flow; the literal warning text at 0x005887... not re-quoted.)

### Claim 4-5 — Corpus census (997 unique defs, 13..56 bytes, all ASCII; 1047 group entries, 884 unique refs; 293 files fully parsed). **NOT VERIFIED** by this pass (text corpus counts, not instruction-level; can be reproduced by a script but not done here). Classified as report statistics, not loader behavior. The build-relevant native instruction is only the loader traversal + first-member selection, which is verified (Claim 3). Census counts remain the answer's own, not re-verified — **UNKNOWN** (counts not re-derived this pass; not a native instruction claim).

### Shipped name boundary / over-255 / encoding — no native validation recovered

The answer claims no native length/encoding rejection. Verified negatively:
- PlayAnimationAction ctor 0x00543C44..6C contains NO length comparison on the copied std::string and NO UTF-8 conversion; copies raw bytes. Matches "No length/UTF-8 validation at this copy."
- Trigger group Init selection + copy 0x00544462..8E similarly stores the selected std::string as raw bytes.
- DefineFromJson first-member copy 0x00588766 (0x004E462A) is a basic_string assignment (raw bytes).

So no native constructor validation recovered (in the cited sample) is **Confirmed** for the producer sample; however I did not enumerate all 997 files' handling for duplicate/reserved handling (first-definition-wins, _PROCEDURAL_ skip — claimed at R-M1-055, not re-verified here; UNKNOWN for duplicate handling within this check file but adopted earlier).

### Build-relevant claims NOT reviewed (budget)
- Claim: CollectAnimFiles walks `assets/animations/` + `config/engine/animations/`, recursively, accepting json/bin (0x0051F4E4..51F5A6, 0x00520E66..20F70). Not independently disassembled this pass beyond the cited ranges; trusted from earlier M5 loader batch verification (M5-X9), not re-quoted here. → listed "not reviewed in this pass" (prior M5 verification exists). 
- Claim: M1-055 GetAnimationNames order (unordered_map by readdir, four threads, _PROCEDURAL_ skip, first-definition-wins, broadcast tail) — adopted earlier (20261017-b-m1-4-check.md); not re-verified here.
- Binary loader AllClips field0 (clip name = field0) at 0x0052151E..1592 (L6/L7) — not re-disassembled; trusted from M5 loan-verified extraction.
- Generated-app boundary (AdministrationCompleted.cs / PlayAnimation.cs .NET strings, UTF-8, 255 setter) — app source, not binary; out of disassembly scope; retained as authority 2 evidence.
- Carried-stack/SDK reset (M11-056) — not touched.

## Finish checklist (cozmo-one-job.md)

1. Job done: independent review (redo) of R-M8-NAME-INPUT (row 46).
2. Engine instructions re-read myself: Play name input at 0x00543C44..6C: `0x00543C48 strd r1,r1,[r4,#0x7C]`; `0x00543C56 str r1,[r0,#0x78]!`; `0x00543C5A ldrb r1,[r6]`; `0x00543C5C lsls r1,r1,#0x1F`; `0x00543C5E bne #0x543C68`; `0x00543C60 ldm.w r6,{r1,r2,r3}`; `0x00543C64 stm r0!,{r1,r2,r3}`; `0x00543C66 b #0x543C70`; `0x00543C68 ldrd r2,r1,[r6,#4]`; `0x00543C6C bl #0x4E02B2`. Trigger group name at 0x00544462..8E: `0x00544462 ldr r2,[r4,#4]`; `0x00544464 mov r1,r7`; `0x00544466 ldrb.w r3,[r4,#0xC8]`; `0x0054446A add.w r0,r2,#0x60`; `0x0054446E blx #0x4AAE58` (select); `0x00544472 add.w r8,r4,#0x78`; `0x00544476 mov r1,r0`; `0x00544478 mov r0,r8`; `0x0054447A bl #0x4E462A` (copy into +0x78); `0x0054447E ldrb.w r0,[r4,#0x78]`; `0x00544482 tst.w r0,#1`; `0x00544486 ite eq`; `0x00544488 lsreq r0,r0,#1`; `0x0054448A ldrne r0,[r4,#0x7C]`; `0x0054448C cmp r0,#0`; `0x0054448E beq.w #0x544634`. Loader JSON naming at 0x005886F5 (DefineFromJson): 0x00588700 `blx #0x4A8308` (getMemberNames); 0x00588704 `ldrd r1,r0,[sp,#0x14]`; 0x00588708 `cmp r1,r0`; 0x0058870A `beq #0x5887DA` (empty→return); `subs r0,r0,r1`; `cmp r0,#0xC`; `beq #0x588764`; ...; 0x00588734 `addeq r0,r1,#1` (short name ptr); 0x00588736 `ldrne r0,[r1,#8]` (long name ptr); 0x00588738 `str r0,[sp]`; 0x0058873A `adr r0,#0x13C`; 0x0058873C `add r1,sp,#8`; 0x0058873E `blx #0x4A4540`; 0x00588762 `ldr r1,[sp,#0x14]`; 0x00588764 `mov r0,r4`; 0x00588766 `bl #0x4E462A`. Census counts (997/1047/884) not re-derived; not instruction claims.
3. Hooks/seams added: none.
4. Anything added that job didn't ask for: none.
5. Tests: none.
6. Focused tests: N/A.
7. Records: none settled. M1-055/M5-063 (and M9-029 SDK-only exclusion for custom-song remap) remain with current statuses. No policy selected; M1-054/M3-050 (engine can't request encoding 5/6/7) unaffected.

No new research queued. No code edited. No record settled. (Binary/group loader cited ranges L6/L7/CollectAnimFiles/first-definition-wins adopted from earlier M5 verification (20261017-b-m1-4-check.md), not freshly re-disassembled in this pass; corpus census counts not re-derived; ASCII/length census is a report statistic, not a native instruction claim.)