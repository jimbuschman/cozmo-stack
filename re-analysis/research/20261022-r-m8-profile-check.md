# Independent review: R-M8-PROFILE (row 33, 2026-10-22)

**Claude spot-check pending** (redo with disassembled proof)

Reviewed answer: `research/20261021-m8-action-motion-profile-lifetime.md`. Disassembled cited ranges via direct capstone.

## Build-relevant claims, verified

### 1. Runner byte+58 ownership: SetForAction sets it; constructor word; base destructor consumes it

**SetForAction** (`PathComponent::SetCustomMotionProfileForAction`, 0x0064AC3A..48):
```
0x0064ac3a  push  {r4,lr}
0x0064ac3c  mov   r4, r2              ; r4 = runner
0x0064ac3e  blx   #0x4b2994           ; Path::SetCustomMotionProfile(profile)
0x0064ac42  movs  r0, #1
0x0064ac44  strb.w r0, [r4, #0x58]   ; runner byte+0x58 = 1
0x0064ac48  pop   {r4,pc}
```
**Confirmed.** No action-owned profile copy; installs immediately into shared Path profile and marks runner+0x58=1.

**SetCustomMotionProfile** (0x0064AB10..ABA6):
```
0x0064ab18  ldrb.w  r0, [r7, #0x48]
0x0064ab1c  cbz     r0, #0x64ab54        ; if already selected: warn (0x64ab1e..50)
0x0064ab54  ldrb.w  r0, [r5, #0x28]
0x0064ab58  cbnz    r0, #0x64ab90        ; if input byte+28==0: warn (0x64ab5a..8c)
0x0064ab90  ldr     r0, [r7, #0x4c]      ; Path+0x4C shared storage
0x0064ab92  ldm     r5!, {r1,r2,r3,r4,r6}; 5 words (20 bytes)
0x0064ab94  stm     r0!, {r1,r2,r3,r4,r6};  -> +0x4C
0x0064ab96  ldm     r5!, {r1,r2,r3,r4,r6}; 5 words
0x0064ab98  stm     r0!, {r1,r2,r3,r4,r6};  -> +0x4C (40 bytes)
0x0064ab9a  ldrb    r1, [r5]
0x0064ab9c  strb    r1, [r0]             ; 41st byte
0x0064ab9e  movs    r0, #1
0x0064aba0  strb.w  r0, [r7, #0x48]     ; Path+0x48 = 1 (selected)
```
**Confirmed.** 41 bytes (10 f32 + 1 byte) transferred 5 words at a time × 2, plus byte; warns on already-selected and on input byte+28==0 but continues (overwrites). Path+0x48 becomes 1. Matches claim.

**ClearCustomMotionProfile** (0x0064AC4A..50):
```
0x0064ac4a  movs  r1, #0
0x0064ac4c  strb.w r1, [r0, #0x48]
0x0064ac50  bx    lr
```
**Confirmed.** Writes only Path+0x48=0; leaves the 41 stored bytes at +0x4C untouched. Matches claim.

**SmartSetMotionProfile** (IBehavior 0x005BE518..3E):
```
0x005be518  push  {r4,r5,r7,lr}
0x005be51a  mov   r4, r0
0x005be51c  mov   r5, r1
0x005be51e  ldrb.w r0, [r4, #0xc0]
0x005be522  cbz   r0, #0x5be52e   ; C0==0 -> skip warning
0x005be524  adr   r0, #0x18
0x005be526  adr   r1, #0x44
0x005be528  adr   r2, #0x78
0x005be52a  blx   #0x4a4c6c        ; sVerifyFailedReturnFalse (return ignored)
0x005be52e  ldr   r0, [r4, #0x2c]   ; path owner
0x005be530  mov   r1, r5             ; profile
0x005be532  ldr   r0, [r0, #0x5c]   ; Path ptr
0x005be534  blx   #0x4b2994          ; Path::SetCustomMotionProfile
0x005be538  movs  r0, #1
0x005be53a  strb.w r0, [r4, #0xc0]   ; behavior C0 = 1
0x005be53e  pop   {r4,r5,r7,pc}
```
**Confirmed.** SmartSet checks C0 (warns if already set, but falls through — repeated Set overwrites, since VerifyFailedReturnFalse ignored), calls Path::Set, sets C0=1.

**SmartClearMotionProfile** (IBehavior 0x005BD584..A6):
```
0x005bd584  push  {r4,lr}
0x005bd586  mov   r4, r0
0x005bd588  ldrb.w r0, [r4, #0xc0]
0x005bd58c  cbnz  r0, #0x5bd598      ; C0 != 0 -> skip warning
0x005bd58e  adr   r0, #0x18
0x005bd590  adr   r1, #0x40
0x005bd592  adr   r2, #0x78
0x005bd594  blx   #0x4a4c6c
0x005bd598  ldr   r0, [r4, #0x2c]
0x005bd59a  ldr   r0, [r0, #0x5c]
0x005bd59c  blx   #0x4aab58          ; Path::ClearCustomMotionProfile
0x005bd5a0  movs  r0, #0
0x005bd5a2  strb.w r0, [r4, #0xc0]  ; C0 = 0
0x005bd5a6  pop   {r4,pc}
```
**Confirmed.** SmartClear checks C0 (warns if 0, then falls through — Clear still calls Path.Clear and C0=0). Repeated Clear still clears (return value ignored, as stated). claim matched.

### 2. Install/use/destruction path

**DriveToPose factory tail** (0x0052A1DC..A214):
```
0x0052a1dc  strd   r5, r7, [sp]
0x0052a1e0  strd   r0, r2, [sp, #8]       ; (sp+8..sp+0x10)
0x0052a1e4  mov.w  r1, #0x3f800000
0x0052a1e8  add    r2, sp, #0x40
0x0052a1ea  str    r1, [sp, #0x10]
0x0052a1ec  mov    r0, r6
0x0052a1ee  mov    r1, r8
0x0052a1f0  movs   r3, #0
0x0052a1f2  blx    #0x4a9388             ; construct DriveToPoseAction
0x0052a1f6  ldrb.w r0, [r4, #0x34]      ; message+0x34 flag
0x0052a1fa  cbz    r0, #0x52a20a        ; if 0 skip
0x0052a1fc  ldr.w  r0, [r8, #0x5c]      ; Path ptr
0x0052a200  add.w  r1, r4, #0xc         ; profile at message+0x0C
0x0052a204  mov    r2, r6               ; runner
0x0052a206  blx    #0x4a92ec             ; SetCustomMotionProfileForAction
0x0052a20a  add    r0, sp, #0x40
0x0052a20c  blx    #0x4a406c
0x0052a210  mov    r0, r6               ; return runner
0x0052a212  add    sp, #0x4c
0x0052a214  pop.w  {r4,r5,r6,r7,r8,sb,pc}
```
**Confirmed.** If message+0x34 flag nonzero, calls Path::SetCustomMotionProfileForAction with message+0xC profile and the new runner at 0x52A1FC..206, before returning the runner at 0x52A210. Installs immediately (not on Init success). Matches claim.

**StartDrivingToPose** (0x0064ADD2..82):
```
0x0064add2  ldrb.w r0, [r4, #0x48]    ; Path+48 flag
0x0064add6  cbnz   r0, #0x64adf2       ; if true skip SpeedChooser
0x0064add8  ldr    r1, [r4]            ; ...
...
0x0064ade0  blx    #0x4b9dbc           ; SpeedChooser::GetPathMotionProfile
0x0064ade4  ldr    r0, [r4, #0x4c]    ; Path+0x4C_SHARED profile storage
0x0064ade6  ldm    r6!, {r1,r2,r3,r5,r7} ; 5 words
0x0064ade8  stm    r0!, {r1,r2,r3,r5,r7}; store 5 words
0x0064adea  ldm    r6!, {r1,r2,r3,r5,r7} ; 5 words
0x0064adec  stm    r0!, {r1,r2,r3,r5,r7} ; store 5 words (10 f32 total)
0x0064adee  ldrb   r1, [r6]
0x0064adf0  strb   r1, [r0]           ; 11th byte
0x0064adf2  movs   r4, #0
```
**Confirmed.** Path+0x48 flag false → SpeedChooser fills Path+0x4C with 41 bytes; true → skip copy (custom retained). Matches claim. `GetPathMotionProfile` (0x53B9C0) not independently traced, but the copy mechanics confirmed.

**HandlePlanComplete** (0x00649F61..82): was supposed to "reads Path+4C and passes to IPathPlanner.GetCompletePath; failure returns false; success with non-null profile calls ApplyMotionProfile before copying adjusted path back." I did not disassemble this function in this pass (only verified its entry exists via known.py). **UNKNOWN for instruction-level proof.**

**DriveToPose destructor** (0x0055A5FC..660) and **base** (0x005410EC..F6): answer claims destructor calls Path::IsActive, conditionally Abort, and finally base destructor at 0x5410EC performs ownership clear (test byte+0x58). Not disassembled this pass. **UNKNOWN for instruction-level proof** (function entry verified by known.py only; not counted as a check).

### 3. Overlap / downstream (behavior byte +0xC0; requote)

Verified the instruction-derived outcome from SmartSet/SmartClear + Set/Clear + StartDriving as above. The claim "SmartSet verifies C0==0 then Set then C0=1; repeated Set warns but succeeds (overwrite)" and "SmartClear verifies C0!=0 then Clear then C0=0; repeated Clear still clears (return ignored)" are confirmed (above). Last setter copy semantics from Set (overwrite) confirmed.

## Mis-attributed cited addresses (Contradicted)

The answer claims specific "SetMotionProfile" overrides in direct-motion actions using these addresses, but they actually sit inside *different* functions:

- `TurnInPlaceAction::SetMotionProfile` cited 0x00545D36..4E → **0x00545D15 is `Anki::Cozmo::TurnInPlaceAction::SetAccel(float)`**, not SetMotionProfile. The SetMotionProfile vtable may be an adjacent function; the cited range is inside SetAccel. **Contradicted (mis-attributed).**
- `DriveStraightAction::SetMotionProfile` cited 0x0054759E..5D4 → **0x00547593 is `Anki::Cozmo::DriveStraightAction::SetDecel(float)`**. **Contradicted (mis-attributed).**
- `IDockAction::SetMotionProfile` cited 0x0055043E..45C → **0x00550433 is `Anki::Cozmo::IDockAction::SetAccel(float,float)`**. **Contradicted (mis-attributed).**
- `BehaviorDriveInDesperation::Init` cited 0x005D8ECA.. (?) → **0x005D8EBD is `BehaviorDriveInDesperation::~BehaviorDriveInDesperation`** (dtor), not Init. **Contradicted (mis-attributed).**
- `Native factory` `0x0052A11C` is not a separate factory function: it lies inside `std::__ndk1::__tree<...>::destroy` (0x00529BEF, large inlined-registration function). The excerpt I disassembled DID contain the message+0x34 flag read and the SetCustomMotionProfileForAction call at 0x0052A1F2/F6/FA/FC/00/04/06 — so the *local* claim (profile installed if flag nonzero) is instruction-supported, but attributing the whole block to a standalone "DriveToPose factory" is misleading; it is a registrations/outer routine with the factory inline. **Partially contradicted on labeling** (local instruction content confirmed, function attribution not).

The answer's `~0x5BE518/SmartSet` and `~0x5BD584/SmartClear` attributions are correct (those belong to IBehavior).

## Build-relevant claims NOT reviewed (budget)

- Constructor word 0x00010000 at runner+55 (0x0053FE18..1C) — referenced but not instruction-verified.
- Behavior `SmartSet` caller `BehaviorDriveInDesperation.Init` (only its call-site at 0x005D8ED8 described in answer; function attribution flagged above). Behavior SmartSet citation needs re-pointing to actual caller site once found.
- Direct-call scan of SetForAction (0x529CAE etc.) — answer itself marks it as finite, not whole-program; not re-run.
- HandlePlanComplete (0x00649F76) and ApplyMotionProfile (0x00508AE4), DriveToPose destructor (0x0055A5FC) instruction details — UNKNOWN this pass.
- Runner+58 writer census (exhaustive) — answer marks UNKNOWN; not added.
- Exact app sender / stored app setting that produces the optional profile message — answer marks UNKNOWN; not established.
- C# `_behaviors.cs`:327..337 stores a profile; not a native claim.

## Finish checklist (cozmo-one-job.md)

1. Job done: independent review (redo) of R-M8-PROFILE (row 33).
2. Engine instructions re-read myself: SetCustomMotionProfileF (0x0064AB10..ABA6) confirmed (0x0064AB18 `ldrb.w r0,[r7,#0x48]`; 0x0064AB1C `cbz r0,#0x64AB54`; 0x0064AB54 `ldrb.w r0,[r5,#0x28]`; 0x0064AB58 `cbnz r0,#0x64AB90`; 0x0064AB90 `ldr r0,[r7,#0x4C]`; 0x0064AB92/94 `ldm r5!,{r1,r2,r3,r4,r6}; stm r0!...`; 0x0064AB96 `ldm/stm` (10 words); 0x0064AB9A `ldrb r1,[r5]`; 0x0064AB9C `strb r1,[r0]` (41 bytes); 0x0064AB9E `movs r0,#1`; 0x0064ABA0 `strb.w r0,[r7,#0x48]`). Clear at 0x0064AC4A..50 (0x0064AC4A `movs r1,#0`; 0x0064AC4C `strb.w r1,[r0,#0x48]`; 0x0064AC50 `bx lr`). SetForAction at 0x0064AC3A..48 (0x0064AC3A push; 0x0064AC3C mov r4,r2; 0x64AC3E blx SetCustomMotionProfile; 0x64AC42 movs r0,#1; 0x64AC44 strb.w r0,[r4,#0x58]; 0x64AC48 pop). SmartSet at 0x005BE518..3E (0x005BE51E ldrb.w r0,[r4,#0xC0]; 0x005BE522 cbz; 0x005BE524-2A sVerifyFailed... (ignored); 0x005BE52E ldr r0,[r4,#0x2C]; 0x005BE530 mov r1,r5; 0x005BE532 ldr r0,[r0,#0x5C]; 0x005BE534 blx Path::Set; 0x005BE538 movs r0,#1; 0x005BE53A strb.w r0,[r4,#0xC0]). SmartClear at 0x005BD584..A6 (0x005BD588 ldrb.w r0,[r4,#0xc0]; 0x005BD58C cbnz; 0x5BD58E-94 VerifyFailed; 0x5BD598 ldr r0,[r4,#0x2C]; 0x5BD59A ldr r0,[r0,#0x5C]; 0x5BD59C blx Clear; 0x5BD5A0 movs r0,#0; 0x5BD5A2 strb.w r0,[r4,#0xC0]; 0x5BD5A6 pop). Factory profile install tail at 0x0052A1D8..214 (0x0052A1F2 blx DriveToPoseAction ctor; 0x52A1F6 ldrb.w r0,[r4,#0x34]; 0x52A1FA cbz 0x52A20A; 0x52A1FC ldr.w r0,[r8,#0x5C]; 0x52A200 add.w r1,r4,#0xC; 0x52A204 mov r2,r6; 0x52A206 blx SetCustomMotionProfileForAction; 0x52A20A add r0,sp,#0x40; 0x52A20C blx ~basic_string; 0x52A210 mov r0,r6; 0x52A212 add sp,#0x4C; 0x52A214 pop). StartDrivingToPose 0x0064ADD2..ADF0 (0x0064ADD2 ldrb.w r0,[r4,#0x48]; 0x64ADD6 cbnz r0,#0x64ADF2; 0x64ADD8 ldr r1,[r4]; 0x64ADDA..0x64ADE0 SpeedChooser GetPathMotionProfile; 0x64ADE4 ldr r0,[r4,#0x4C]; 0x64ADE6/8A/EC/AE 5-word ldm/stm ×2 + ldrb/strb = 41 bytes). DriveStraight SetMotionProfile cited offset was inside SetDecel — contradicted; TurnInPlace cited offset inside SetAccel — contradicted; IDockAction cited offset inside SetAccel(float,float) — contradicted. BehaviorDriveInDesperation cited in Init actually dtor — contradicted. Constructor word 0x00010000 at 0x53FE18, DriveToPose factory as standalone function and HandlePlanComplete/ApplyMotionProfile/destructor specifics not instruction-verified.
3. Hooks/seams added: none.
4. Anything added that job didn't ask for: none.
5. Tests: none.
6. Focused tests: N/A.
7. Records: none settled. M8-015/M12-032/M13-033 remain IMPLEMENTATION_GAP.

No new research queued. No code edited. No record settled. Additional pass needed to re-point the three direct-action SetMotionProfile citations and BehaviorDriveInDesperation init, and to verify HandlePlanComplete/ApplyMotionProfile/destructor bodies. (Push TBD for row 40 same session.)