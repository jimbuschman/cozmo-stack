# X4 extraction — M9-wwise-music (singing)

Read-only extraction, 2026-09-28, job X4. Citations are VAs in
`resources/lib/armeabi-v7a/libcozmoEngine.so` (ELF VAs; engine code is Thumb, the
Wwise runtime region 0x0095E540..0x00AE2E40 is ARM). The Ghidra decompilation
(`re-analysis/decomp/libcozmoEngine/`) was a navigation aid only. The approved M6
inventory (`re-analysis/inventory/M6-wwise-bank.md`) is primary source already
checked by the manager and is cited where it settles a row.

Existing records checked: M9-001..M9-027 (from `re-analysis/fidelity_manifest.json`).
None of the M9 records has an inventory file; they are claims only.

## 1. Production path

| # | step | what the original does | citation | record | classification |
|---|------|------------------------|----------|--------|----------------|
| S1 | BehaviorSinging ctor reads config | reads `displayNameKey` (len 0xe), `audioSwitchGroup` (len 0x10), `audioSwitch` (len 0xb) via `JsonTools::GetValueOptional` | `0x005ee920 adr r1,#0x148`; `0x005ee924 movs r2,#0xe`; `0x005ee930 blx #0x4a5fec`; group `0x005ee94e movs r2,#0x10`; switch `0x005ee982 movs r2,#0xb` | M9-001 | EXACT_SOURCE |
| S2 | default tempo trigger | `this+0x124 = 0x23f` before the switch resolves | `0x005ee8ea movw r1,#0x23f`; `0x005ee8ee str.w r1,[r4,#0x124]` | NEW | EXACT_SOURCE |
| S3 | group string -> enum | `audioSwitchGroup` -> `SwitchGroupTypeFromString` -> `this+0x11c` | `0x005ee96e blx #0x4b537c`; `0x005ee972 str.w r0,[r4,#0x11c]` | M9-001 | EXACT_SOURCE |
| S4 | group/state -> tempo trigger | `0xb215bb17`->state +0x120/trigger 0x201; `0xc8a59578`->trigger 0x1ff; `0xe017e775`->trigger 0x200; else group `0xc8a59578`, state 0, trigger 0x1ff | group hashes `0x005ee9a4..0x005ee9c0`; triggers `0x005ee9d2`, `0x005ee9ea`, `0x005ee9fa`; store `0x005ee9fe` | M9-001 | EXACT_SOURCE |
| S5 | InitInternal posts the switch FIRST | `PostRobotSwitchState(robot+0x58, this+0x11c, this+0x120)` | `0x005eeb46 ldrd r1,r2,[r7,#0x11c]`; `0x005eeb4c ldr r0,[r3,#0x58]`; `0x005eeb4e blx #0x4ac2ec` | M9-002 | EXACT_SOURCE |
| S6 | then the reaction lock | `SmartDisableReactionsWithLock(this, this+0x40, &DAT_00c6f590)` | `0x005eeb52 ldr.w r2,[pc,#0x4fc]`; `0x005eeb56 add.w r1,r7,#0x40`; `0x005eeb5e blx #0x4b28ec` | NEW | EXACT_SOURCE |
| S7 | per connected cube: one RollingAverage | `BlockWorld::FindConnectedActiveMatchingObjects`, then one map entry per cube, `+0x18=0`, `+0x1c=1` | `0x005eecca blx #0x4b53d0`; `0x005eecd2 str.w sb,[r0,#0x18]`; `0x005eecd6 str r1,[r0,#0x1c]` | NEW | EXACT_SOURCE |
| S8 | one ShakeListener per cube | ctor args 0.5, 2.5, 3.9 | `0x005eecf4 movw r3,#0x999a`; `0x005eecfc mov.w r1,#0x3f000000`; `0x005eed00 movt r2,#0x4020`; `0x005eed04 movt r3,#0x4079`; `0x005eed08 blx #0x4b12b4` | M9-017 | EXACT_SOURCE |
| S9 | register with the cube accel component | `CubeAccelComponent::AddListener(robot+0x278, objectId, listener, &DAT_00c6f5bc)`; entry is `0x00635474`, **not** the cited `0x0063547E` | `0x005eed56 blx #0x4a92bc`; body `0x00635474` | M9-017 | EXACT_SOURCE (citation corrected) |
| S10 | AddListener turns the cube stream on, only for a new object | when `[r6+0x30]==0` it builds `StreamObjectAccel{enable=1,id}` and sends it | `0x006354e8 ldr r0,[r6,#0x30]`; `0x006354ec bne #0x6355b0`; `0x00635558 strb.w r1,[sp,#0x10]`; `0x00635562 blx #0x4b9018`; `0x0063556e blx #0x4a5368` | M9-017 (omits the condition) | EXACT_SOURCE |
| S11 | shake callback -> per-cube running mean | `avg += (m-avg)/count; count++` on `this+0x134[id]+0x18` | `0x005ef4b2 vsub.f32 s4,s16,s0`; `0x005ef4bc vdiv.f32 s2,s4,s2`; `0x005ef4c0 vadd.f32 s0,s0,s2`; `0x005ef4c4 vstr s0,[r0,#0x18]` | NEW | EXACT_SOURCE |
| S12 | UpdateInternal takes the max over cubes, then resets | walks `this+0x134`, keeps the largest `+0x18`, writes `+0x18=0`, `+0x1c=1` | `0x005ef0ec vldr s4,[r6,#0x18]`; `0x005ef0fa vmovgt.f32 s2,s4`; `0x005ef0fe strd r3,r2,[r6,#0x18]` | M9-003 | EXACT_SOURCE |
| S13 | normalise / clamp / smooth | `v = max/3000`; `new = 0.5*old + 0.5*clamp(v,0,1)` at `this+0x140` | `0x005ef12e vdiv.f32 s2,s2,s4` (3000 at `0x005ef27c`); `0x005ef16c vmul.f32 s6,s8,s6`; `0x005ef176 vadd.f32 s0,s10,s6`; `0x005ef17e vstr s0,[r7,#0x140]` | M9-003 | EXACT_SOURCE |
| S14 | post the vibrato parameter | `PostRobotParameter(robot+0x58, 0xc20f49df, smoothed)` every tick | `0x005ef184 movw r1,#0x49df`; `0x005ef188 movt r1,#0xc20f`; `0x005ef18c blx #0x4ac2f8` | M9-003 | EXACT_SOURCE |
| S15 | shake-duration log | if smoothed > 0.1 stamp; when it drops, log `robot.song_shake_duration_ms` if elapsed > 500 ms | `0x005ef190 vldr s0,0.1`; `0x005ef1dc cmp.w r5,#0x1f4`; `0x005ef214 blx #0x4a4f90` | NEW | EXACT_SOURCE |
| S16 | UpdateInternal result | returns 2 while `this+0x84 == 0`, else 1 | `0x005ef240 ldr.w r1,[r7,#0x84]`; `0x005ef244 movs r0,#2`; `0x005ef24a movne r0,#1` | NEW | EXACT_SOURCE |
| S17 | three animations in order | get-in `0x202`, tempo `this+0x124`, get-out `0x203`, each 60 s, appended to a sequential compound | `0x005eedd6 movw r2,#0x202`; `0x005eee06 ldr.w r2,[r7,#0x124]`; `0x005eee4a movw r2,#0x203`; `0x005eee0c movt r8,#0x4270` | M9-002 | EXACT_SOURCE |
| S18 | StartActing | `IBehavior::StartActing(this, compound, callback)` | `0x005eee8c blx #0x4b297c` | M9-002 | EXACT_SOURCE |
| S19 | StopInternal posts 0 first, then removes listeners | `PostRobotParameter(robot+0x58, 0xc20f49df, 0)`; then `RemoveListener` per tracked cube | `0x005ef2be movw r1,#0x49df`; `0x005ef2c2 movt r1,#0xc20f`; `0x005ef2c6 movs r2,#0`; `0x005ef2ca blx #0x4ac2f8`; `0x005ef2e0 blx #0x4a92c8` | M9-003 | EXACT_SOURCE |
| S20 | PostRobotParameter dispatch | game object 7 when `[client+0x3c]==2` (on robot) else 6; vtable `+0x18`, time 0, curve 0 | `0x00599f68 ldrb.w ip,[r0,#0x3c]`; `0x00599f76 movs r3,#6`; `0x00599f78 cmp.w ip,#2`; `0x00599f7e moveq r3,#7`; `0x00599f6c ldr.w lr,[r3,#0x18]` | NEW (interface to M6) | EXACT_SOURCE |
| S21 | PostRobotSwitchState dispatch | same game-object rule; vtable `+0x14` with (gameObj, group, state) | `0x00599f9a movs r3,#6`; `0x00599fa0 cmp.w lr,#2`; `0x00599fa6 moveq r3,#7`; `0x00599fa8 ldr.w ip,[ip,#0x14]` | NEW (interface) | EXACT_SOURCE |
| S22 | HPF per axis | `y = a*(y + x - xPrev)`, then `xPrev = x` | `0x006365b8 vadd.f32 s0,s0,s6`; `0x006365c8 vsub.f32 s0,s0,s8`; `0x006365cc vmul.f32 s0,s6,s0`; `0x006365d0 vstr s0,[r1]`; `0x00636610 ldm.w r4,{r2,r3,r5}` | M9-017 | EXACT_SOURCE |
| S23 | ShakeListener hysteresis | squared magnitude; trigger `>3.9^2` idle, hold `>2.5^2` while shaking; callback gets the squared magnitude | `0x006367ca vmul.f32 s0,s0,s0`; `0x006367e2 addeq.w r1,r4,#0x2c`; `0x006367f4 vcmpe.f32 s0,s2`; `0x006367fe movgt r0,#1`; `0x0063680c vmov r1,s0` | M9-017 | EXACT_SOURCE |
| S24 | engine audio-frame budget | `UpdateAmountToSend` allows `(r8-sb)+14` frames and a 30000-byte budget | `0x0057c79e add.w r1,r1,#0xe`; `0x0057c798 movw r0,#0x7530`; `0x0057c7ac strd r0,r1,[r4,#0x98]` | M9-016 | EXACT_SOURCE |
| S25 | Wwise runtime modulator factory | `r1==1` -> one class, `r1==0` -> the other; 0x48-byte objects, vtables stored | `0x009d7b70 subs r5,r1,#0`; `0x009d7b78 beq #0x9d7c1c`; `0x009d7b84` branch; `0x009d7bcc str r2,[r4]` | NEW | EXACT_SOURCE |
| S26 | LFO update reads its properties | property reads and phase accumulator `+0x34..+0x48` | `0x009d671c` (body) | M9-006/008/025 (runtime side) | EXACT_SOURCE (property map only) |
| S27 | modulator binding applied through its own curve | runtime evaluates each binding curve and accumulates onto the named property | `0x00a6e848..0x00a6f133` | M9-009 | EXACT_SOURCE (structure) |
| S28 | step-mode container selection | global 64-bit LCG `s = s*0x5851F42D4C957F2D + 1`, output `s>>33`; k-th eligible with a blocked list | `0x0098a780 movw r3,#0xf42d`; `0x0098a784 movt r3,#0x5851`; `0x0098a788 movw r2,#0x7f2d`; `0x0098a78c movt r2,#0x4c95`; `0x0098a7b8 lsr r0,r3,#1` | M9-022 (contradicts BLOCKED_EXTERNAL) | EXACT_SOURCE; full detail M6 gapA rows 3.4-3.7 |
| S29 | RTPC scaling is applied after the curve | scaling 2 is the dB map `-20*log10(1-y)` / `20*log10(1+y)`; scaling 0 leaves it unchanged | `0x00a14f88 vcmpe.f32 s12,#0`; `0x00a14fd8 vmla.f32 s15,s10,s12`; `0x00a14fec vmov.f32 s12,#20.0`; `0x00a15030 vmul.f32 s12,s15,s12` | M9-007 (contradicted) | EXACT_SOURCE; M6 gapA 5.3 |
| S30 | EQ and limiter are in the .so | Parametric EQ create `0x00aa257c` (registered `0x004deb8c`), Peak Limiter create `0x00aa18f4` (registered `0x004deb18`) | as listed; M6-013 / gapC 4.3-4.7 | M9-026 (contradicted) | EXACT_SOURCE |

## 2. Is the Wwise runtime present? (the M9-013/014/022/024/025/026 question)

**Yes.** The M6 inventory's finding stands and I re-checked the code directly:
`0x009d7b6c` constructs the runtime modulator objects; `0x0098a6d4` contains the
container-selection LCG; `0x00aa257c` / `0x00aa18f4` are the EQ / limiter plug-in
create functions; `0x00a14f88` is the RTPC scaling conversion. There are no Wwise
strings (the runtime is stripped), but the code is there. So every M9 record whose
authority is "no Wwise runtime ships in the APK" is **wrong in its authority**.

| record | current | should be | exactly what to read |
|--------|---------|-----------|----------------------|
| M9-022 | BLOCKED_EXTERNAL | RECOVERABLE_GAP, largely already read | M6-006/007/008; step selection `0x0098a6d4`; random `0x00a08a44`; sequence `0x00a0a524`; PlayInternal `0x00a0afdc`; switch `0x00a2c730`; continuous `0x00a0abc4` (M6 gapA rows 3.4-3.9). The "random by weight avoiding the last" wording is also wrong. |
| M9-024 | BLOCKED_EXTERNAL | RECOVERABLE_GAP | envelope update `0x009d5934` (property 15 branch) and the modulator binding `0x00a6e848`. The SDK name of property 15 may stay unavailable, but the behaviour is in the binary. |
| M9-025 | BLOCKED_EXTERNAL | RECOVERABLE_GAP | LFO update `0x009d671c` (property map read); read the consumer of the LFO output struct `+0x34..+0x48` for the waveform. Not read this pass. |
| M9-026 | BLOCKED_EXTERNAL | RECOVERABLE_GAP, already read | M6-013 / gapC: EQ coefficient `0x00aa25e0`, execute `0x00aa2a84`; limiter setup `0x00aa19cc`, DSP `0x00aa0eb4`. |
| M9-013 | BLOCKED_EXTERNAL | RECOVERABLE_GAP (not located this pass) | runtime present; MIDI dispatch not located. Candidates: HIRC music handlers `0x009b3ebc`/`0x009bbf9c`, source factory `0x00a78d10`. |
| M9-014 | BLOCKED_EXTERNAL | RECOVERABLE_GAP (not located this pass) | same path; velocity code not found. |

## 3. Judgement of each existing record

| record | claimed | judgement | evidence checked |
|--------|---------|-----------|------------------|
| M9-001 | EXACT_SOURCE | confirmed | ctor `0x005ee8dc`; hashes `0x005ee9a4..0x005ee9c0`; triggers `0x005ee9d2/0x005ee9ea/0x005ee9fa` |
| M9-002 | EXACT_SOURCE | confirmed | `0x005eeb4e` switch; `0x005eedd6/0x005eee06/0x005eee4a` animations; `0x005eee8c` |
| M9-003 | EXACT_SOURCE | confirmed | `0x005ef0ec..0x005ef17e`; param `0x005ef18c`; stop `0x005ef2ca` |
| M9-004 | EXACT_SOURCE (banks) | not re-parsed; bank-side | no binary claim to check |
| M9-005 | EXACT_SOURCE (banks) | not re-parsed; bank-side | no binary claim to check |
| M9-006 | EXACT_SOURCE (banks) | partly confirmed | runtime LFO/envelope classes exist (`0x009d7b6c`, `0x009d671c`, `0x009d5934`); the C# bank reader was not compared |
| M9-007 | EXACT_SOURCE | **CONTRADICTED** | scaling 2 is applied: `0x00a14f88..0x00a15038`; M6 gapA 5.3/5.6: object 0x16BEDBEA (=381606890) points (0,0)->(1,-1), scaling 2, fades to -764.6 dB, not "at most one decibel" |
| M9-008 | EXACT_SOURCE | confirmed (runtime side) | `0x009d671c`; post `0x005ef18c` |
| M9-009 | EXACT_SOURCE | confirmed structurally | `0x00a6e848` |
| M9-010 | EXACT_SOURCE | not independently checked | bank-side; consistent with M6-008 |
| M9-011 | EQUIVALENT_IMPLEMENTATION | confirmed (routing/order); its M9-026 reference is now recoverable | chain order EQ->EQ->limiter->Hijack: M6 gapC 3.1; create addresses above |
| M9-012 | EXACT_SOURCE (banks) | not re-parsed; bank-side | runtime MIDI pitch override M6 gapE 3.3 |
| M9-013 | BLOCKED_EXTERNAL | **contradicted authority; RECOVERABLE_GAP** | runtime present; dispatch not located |
| M9-014 | BLOCKED_EXTERNAL | **contradicted authority; RECOVERABLE_GAP** | runtime present; velocity not located |
| M9-015 | EXACT_SOURCE | **provenance contradicted / evidence too weak** | "the engine hands Wwise a generator" is false: Wwise uses its own LCG `0x0098a780`; M6-007. The conclusion may still hold. |
| M9-016 | EQUIVALENT_IMPLEMENTATION | confirmed | `0x0057c79e`; `0x0057c798` |
| M9-017 | EXACT_SOURCE | confirmed, two evidence defects | HPF `0x00636598`; Shake `0x0063679e`; ctor args `0x005eecf4..0x005eed08`. Cited `0x0063547E` is mid-function, not the send; the send is `0x00635558..0x0063556e`, and it fires only when the object has no listener (`0x006354e8`) |
| M9-018 | EQUIVALENT_IMPLEMENTATION | not independently checked | bank-side |
| M9-019 | EXACT_SOURCE (banks) | not independently checked | bank-side |
| M9-020 | EQUIVALENT_IMPLEMENTATION | not independently checked | bank-side |
| M9-021 | EXACT_SOURCE (banks, not live) | not independently checked | bank-side |
| M9-022 | BLOCKED_EXTERNAL | **CONTRADICTED** | runtime present; `0x0098a6d4`; M6 gapA 3.5-3.6; description wrong |
| M9-023 | HARDWARE_ONLY | stands | no recording exists |
| M9-024 | BLOCKED_EXTERNAL | **contradicted authority; RECOVERABLE_GAP** | envelope `0x009d5934`; property 15 semantics not read |
| M9-025 | BLOCKED_EXTERNAL | **contradicted authority; RECOVERABLE_GAP** | LFO `0x009d671c` read for the property map; waveform consumer not read |
| M9-026 | BLOCKED_EXTERNAL | **CONTRADICTED** | EQ/limiter in the .so and already read: M6-013; `0x00aa257c`, `0x00aa18f4` |
| M9-027 | EQUIVALENT_IMPLEMENTATION | not independently checked | Init.bnk + `AUDIO_SAMPLE_RATE` |

## 4. Existing records contradicted by the source

- **M9-007** (EXACT_SOURCE): the note-off binding's RTPC scaling is applied, so the
  curve value is not directly dB. Citation `0x00a14f88..0x00a15038`; M6 gapA 5.3
  and 5.6 (approved) state the object 0x16BEDBEA (=381606890) points (0,0)->(1,-1)
  scaling 2 fade to -764.6 dB.
- **M9-022** (BLOCKED_EXTERNAL): authority false (runtime present, `0x0098a780`) and
  the algorithm description false. M6 gapA 3.5-3.6 / inventory line 289.
- **M9-026** (BLOCKED_EXTERNAL): authority false. `0x00aa257c` / `0x00aa18f4`,
  registered `0x004deb8c` / `0x004deb18`; M6-013.
- **M9-013, M9-014, M9-024, M9-025** (BLOCKED_EXTERNAL): authority false; they are
  RECOVERABLE_GAP. The runtime is present; I did not settle their own questions.
- **M9-015** (EXACT_SOURCE): the provenance "the engine hands Wwise a generator" is
  contradicted by the Wwise LCG at `0x0098a780`. Its evidence is too weak.
- **M9-017** (EXACT_SOURCE): evidence address `0x0063547E` is not the send; correct to
  `0x00635558..0x0063556e` and add the "first listener only" condition.

## 5. Records whose evidence is too weak to keep their status

- **M9-004, M9-005, M9-006, M9-010, M9-012, M9-018, M9-019, M9-020, M9-021, M9-027:**
  these rest on shipped-bank facts that were not re-parsed in this pass. A bank pass
  should confirm them, or the manager accepts the M6 bank reading.
- **M9-011:** EQUIVALENT_IMPLEMENTATION rests on M9-026, now recoverable; reclassify
  after M6-013 is wired.
- **M9-016:** the 66 ms lead is this stack's own choice (its own text says so); the
  engine side (`0x0057c79e`, +14 frames) is confirmed. It is a policy, not a recovered
  value.

## 6. NEW steps no M9 record covers

- S2 default trigger `0x23f`; S6 the reaction lock between switch and animations.
- S7/S11 the per-cube RollingAverage is a running mean inside the tick, reset each
  tick (M9-003 only says "largest shake").
- S10 AddListener turns the cube stream on only for the first listener of an object.
- S15 the `robot.song_shake_duration_ms` log with its 500 ms threshold.
- S16 UpdateInternal's 1/2 return.
- S20/S21 the game-object choice 7 on-robot / 6 off-robot for switch and parameter.
- S24 the engine audio-frame budget (+14 frames, 30000 bytes): cited by M9-016 but no
  M9 record owns it (belongs to M5/M6).
- S25-S27 the runtime LFO/Envelope classes and their binding application: assumed by
  M9-006/008/024/025 but not recorded.

## 7. Open questions

1. M9-013/M9-014: runtime present but MIDI dispatch / velocity code not located.
   A targeted pass should read the HIRC music handlers `0x009b3ebc`/`0x009bbf9c` and
   the source factory `0x00a78d10`. Until then they are RECOVERABLE_GAP.
2. M9-025: read the consumer of the LFO output struct `+0x34..+0x48`.
3. M9-024: read the property-15 branch of the envelope update `0x009d5934`.
4. M9-007: decide whether to rewrite (curve is the +/-20log10 map) or retire the
   record, and whether the "0.095 dB" render figure used the wrong model.
5. M9-015: confirm whether the conclusion still holds via Wwise's own per-play RNG.
6. M9-004/006/010/012/019/020/021/027: no bank re-parse this pass; decide whether a
   bank pass is needed before they stay EXACT_SOURCE.
7. M9-017: correct the evidence address and add the first-listener condition.
