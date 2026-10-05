# M9 Wwise music and singing inventory

**State:** prepared by job I-M9 and frozen with `python re-analysis/tools/fidelity.py --approve M9-wwise-music`. Every prior source-fidelity claim starts as IMPLEMENTATION_GAP until the build job compares the live C# path with this inventory. Four runtime questions remain RECOVERABLE_GAP after the three allowed targeted passes; one listening result remains HARDWARE_ONLY.

## Where this comes from

- **Primary source:** `resources/lib/armeabi-v7a/libcozmoEngine.so` 3.4.0-1204, the six shipped Wwise banks and their decoded media/configuration under `re-analysis/obb/`.
- **X4:** `re-analysis/research/20260928-X4-M9-wwise-music-extraction.md` traces the engine singing behavior, cube-shake path, RobotAudioClient dispatch, audio budget and the statically linked Wwise runtime.
- **Gap passes:** `20260928-I-M9-gap1-extraction.md` reads the envelope class/property-15 path; gap2 reads the LFO class/state and bounds the missing waveform consumer; gap3 reads and rejects the proposed MIDI candidates, corrects the RNG provenance, and records citation verification.
- **M6 input:** the approved `re-analysis/inventory/M6-wwise-bank.md` owns the general Wwise bank reader, runtime container machinery, voice/bus engine, EQ, limiter and Hijack path. M9 owns how singing data and behavior use those interfaces.
- **Verification:** every row changing or contradicting an existing M9 record plus S1-S21 (21 additional rows) was opened in the shipped ELF. The proposed `0x009B3EBC` MIDI interpretation failed and is not retained; it is an object-loader fallback. No other checked row failed.
- Existing C# was inspected only to choose record locations. It is not evidence.

## Records

| record | status | what | rows |
| --- | --- | --- | --- |
| M9-001 | IMPLEMENTATION_GAP | `BehaviorSinging` reads the optional display/switch strings, defaults the tempo trigger to `0x23f`, maps the three group hashes to state/tempo triggers, and uses the documented fallback. | X4 S1-S4; `0x005EE8DC..0x005EEA0F` |
| M9-002 | IMPLEMENTATION_GAP | Init posts the switch, acquires its reaction lock, constructs get-in/tempo/get-out as a 60-second sequential compound, and starts acting, in that order. | X4 S5-S6, S17-S18; `0x005EEB30..0x005EEE9F` |
| M9-003 | IMPLEMENTATION_GAP | Per-cube running means are updated from shake callbacks; each tick takes the maximum, resets the means, divides by 3000, clamps and half-smooths it, posts vibrato, logs shake durations over 500 ms, returns 2/1 by acting state, and Stop posts zero before removing listeners. | X4 S7, S11-S16, S19 |
| M9-004 | IMPLEMENTATION_GAP | The three music switch containers, decision trees, meters and MIDI target are read from the shipped bank. | X4 existing-record review; M6 bank inventory |
| M9-005 | IMPLEMENTATION_GAP | The singing MIDI sources are SMF tracks with division 9600 and their effective meter/tempo and clip-duration relationships. | X4 existing-record review; shipped-bank evidence |
| M9-006 | IMPLEMENTATION_GAP | HIRC type 21 LFO and type 22 Envelope payloads are read; the runtime factory constructs their two 0x48-byte classes and installs the corresponding vtables. | X4 S25-S26; gap1 E1-E2; gap2 L1 |
| M9-007 | IMPLEMENTATION_GAP | The note-off envelope binding uses RTPC scaling 2 after its `(0,0)->(1,-1)` curve: the result is the Wwise +/-20-log map, not a direct one-decibel maximum. | X4 S29 and contradiction; `0x00A14F88..0x00A15038`; M6 gapA 5.3/5.6 |
| M9-008 | IMPLEMENTATION_GAP | The vibrato LFO binding and its depth RTPC are read from the bank and the behavior posts the controlling parameter every tick. | X4 S14, S26; M6 bank inventory |
| M9-009 | IMPLEMENTATION_GAP | Each modulator binding is evaluated through its own curve and accumulated onto the named target property by the shipped runtime. | X4 S27; `0x00A6E848..0x00A6F133` |
| M9-010 | IMPLEMENTATION_GAP | Singing note-on/off layers and loop/release data determine held-note lifetime; the production renderer must be compared against the shipped sampler data. | X4 existing-record review; shipped-bank evidence |
| M9-011 | IMPLEMENTATION_GAP | Rendering follows the shipped Robot_Bus_1 chain, including its two EQ positions, limiter and Hijack order, rather than peak normalization. | X4 existing-record review; M6-013 and gapC |
| M9-012 | IMPLEMENTATION_GAP | MIDI note tracking is disabled on every shipped node; bank flags and the runtime MIDI pitch-override path are part of the comparison. | X4 existing-record review; M6 gapE 3.3 |
| M9-013 | RECOVERABLE_GAP | Whether MIDI notes are routed into the get-in branch is in the shipped runtime, but the event-dispatch path has not been located. | X4 open question 1; gap3 M1-M2 |
| M9-014 | RECOVERABLE_GAP | Whether note velocity implicitly changes voice gain when no bank binding asks for it is in the shipped runtime, but its consumer has not been located. | X4 open question 1; gap3 M1-M2 |
| M9-015 | IMPLEMENTATION_GAP | Each play's container choices must use Wwise's own per-play selection path and 64-bit LCG; the former claim that the engine supplied a generator is rejected. | X4 S28/contradiction; gap3 M3; M6-007 |
| M9-016 | COMPATIBILITY_POLICY | Streaming remains block-at-a-time so live vibrato can affect future audio; 66 ms is this stack's explicit lead policy, while the original engine budgets 30,000 bytes and 14 frames beyond playback. | X4 S24; `0x0057C780..0x0057C7BF` |
| M9-017 | IMPLEMENTATION_GAP | One ShakeListener per connected cube uses the source-backed HPF, squared-magnitude hysteresis and callback value. AddListener enables object acceleration only for the first listener for that object. | X4 S8-S10, S22-S23; corrected `0x006354E8`, `0x00635558..0x0063556E` |
| M9-018 | IMPLEMENTATION_GAP | The shipped singing Stop event targets the three playing tempo containers; any ancestor generalization must be compared rather than presumed exact. | X4 existing-record review; shipped-bank evidence |
| M9-019 | IMPLEMENTATION_GAP | Every shipped blend container has zero blend tracks, as read from all six banks. | X4 existing-record review; shipped-bank evidence |
| M9-020 | IMPLEMENTATION_GAP | Clip BeginTrim/length and held-note release at clip end are driven by the shipped music data and must be compared with the production renderer. | X4 existing-record review; shipped-bank evidence |
| M9-021 | IMPLEMENTATION_GAP | Every Play action of a multi-action event is dispatched; this must be compared against the shipped event data and runtime interface. | X4 existing-record review; shipped-bank evidence |
| M9-022 | IMPLEMENTATION_GAP | Runtime container selection is present. Step/random/sequence semantics use the recovered Wwise algorithms, including its own LCG and blocked-list eligibility; the former “weighted avoiding last” claim is rejected. | X4 S28; `0x0098A6D4..0x0098A7B8`; M6 gapA 3.4-3.9 |
| M9-023 | HARDWARE_ONLY | No shipped artifact establishes how the stock robot/app recording sounded after phone/runtime/hardware effects. | X4 existing-record review |
| M9-024 | RECOVERABLE_GAP | The envelope runtime and property-15 decision branch are read, but the consumer proving whether note-off stops the attached voice is not. | gap1 E2-E3 |
| M9-025 | RECOVERABLE_GAP | The LFO property map, randomized values, phase state and type-specific phase offsets are read, but the per-voice waveform-sample consumer is not. | gap2 L1-L3 |
| M9-026 | IMPLEMENTATION_GAP | Parametric EQ and peak limiter arithmetic ship in the `.so`; their recovered coefficient/setup/DSP paths replace the former BLOCKED_EXTERNAL claim. | X4 S30; M6-013/gapC; `0x00AA257C`, `0x00AA18F4` |
| M9-027 | IMPLEMENTATION_GAP | The high/low-pass setting and robot-rate/Nyquist consequence must be compared with the exact recovered EQ path rather than retained as an unchecked equivalence claim. | X4 existing-record review; M6-013 |
| M9-028 | IMPLEMENTATION_GAP | RobotAudioClient posts singing parameters and switches to game object 7 on-robot or 6 off-robot, using vtable slots `+0x18` and `+0x14` respectively with zero transition time/curve for parameters. | X4 S20-S21; `0x00599F60..0x00599FBF` |

## Decisions

- **SD1 (exact, always).** Applied to every shipped behavior. All former EXACT_SOURCE records return to IMPLEMENTATION_GAP until the build job compares the whole production path. No missing Wwise rule is filled with a plausible renderer rule.
- **SD2 (engine undefined behavior).** Not applied. No M9 row needs a deterministic replacement for undefined engine behavior.
- **SD3 (later-layer ownership).** M6 owns the general bank/runtime/voice/bus implementation. M9 owns the singing-specific behavior, data and use of that runtime. S24 is recorded as an M6 interface, not duplicated as an M9 record.
- **SD4 (housekeeping).** X4 and all three I-M9 gap reports are committed and reproduced verbatim below.
- **MD1.** M9-016 stays `COMPATIBILITY_POLICY`: 66 ms is explicitly the stack's choice, not a recovered original constant. The source-backed original budget remains part of the record.
- **MD2.** M9-013, M9-014, M9-024 and M9-025 remain live `RECOVERABLE_GAP` after the three permitted focused passes, so `source_investigation_exhausted` is false.
- **MD3.** M9-023 stays `HARDWARE_ONLY`; a hardware pass may verify output but cannot strengthen source provenance.

## Existing evidence contradicted or too weak

- **M9-007:** contradicted. Scaling 2 is applied after the bank curve, so the curve's `-1` is not directly minus one decibel.
- **M9-013, M9-014, M9-024 and M9-025:** “no Wwise runtime ships” is false. The runtime is statically linked; the exact remaining questions are recoverable and named in the gap reports.
- **M9-015:** provenance contradicted. Wwise owns and advances the LCG at `0x0098A780`; the engine does not hand it the claimed generator.
- **M9-017:** `0x0063547E` was mid-function and did not prove the send. The correct send is `0x00635558..0x0063556E`, gated by the first-listener test at `0x006354E8`.
- **M9-022:** contradicted in both authority and wording. Runtime code is present and the recovered eligibility/blocked-list algorithm replaces “weighted avoiding the last.”
- **M9-026:** contradicted authority. Both plug-ins, their registration, setup and DSP bodies are in the `.so` and are owned by M6-013.
- **M9-004..006, M9-010, M9-012, M9-018..021 and M9-027:** X4 did not independently reparse their bank evidence. The already approved M6 bank inventory is the accepted primary input; these still become implementation gaps for production-path comparison.
- **M9-011:** its routing/order is supported, but the former equivalence claim depended on M9-026's false blocked premise.
- **M9-016:** its source side is supported, but the 66 ms value is a stack policy and is classified accordingly.

## Appendices

The following appendices reproduce the complete extraction inputs verbatim.


## Appendix A - X4 extraction

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



## Appendix B - gap pass 1

# I-M9 gap pass 1 — envelope runtime

Read-only extractor pass, 2026-09-28. Citations are ARM-mode virtual addresses in
`resources/lib/armeabi-v7a/libcozmoEngine.so`. The decompilation was used only for
navigation.

| step | what the original does | citation | record | classification |
|---|---|---|---|---|
| E1 | HIRC types 21 and 22 are constructed by the same factory. Type 21 selects the vtable at `0x0103B1E8`; type 22 selects `0x0103B218`. | factory `0x009D7B6C..0x009D7C97`; vtable words `0x0103B1E8..0x0103B23F` | M9-006 | EXACT_SOURCE |
| E2 | The type-22 update method is `0x009D5934`. It reads the envelope property bundle and ranged-property bundle, applies RTPC overrides, advances its stage state and writes the current normalized result at output `+0x18`. | type-22 vtable `0x0103B230 = 0x009D5934`; update `0x009D5934..0x009D6598`; final clamp/store `0x009D6570..0x009D6598` | M9-024 | EXACT_SOURCE for the property/update path |
| E3 | Property 15 is read by a separate type-22 virtual method. Default/0 selects only property value 1; other event states distinguish property values 1 and 2, including MIDI-status bytes `0x80` and `0x90`. | type-22 vtable `0x0103B23C = 0x009D552C`; property-15 scan and branches `0x009D552C..0x009D55F3` | M9-024 | EXACT_SOURCE for the branch, RECOVERABLE_GAP for its production meaning |

The missing fact is still whether this return value causes `cozmo_singing_note_off`
to stop the attached voice. The next extraction must trace the virtual `+0x24` call
from `0x009D7FC0` through the per-voice object constructed by `0x009E266C`, and then
identify the caller that consumes the boolean from `0x009D552C`. Property-name
guessing from an SDK version is not evidence.

No existing citation failed in this pass. M9-024 remains `RECOVERABLE_GAP`.



## Appendix C - gap pass 2

# I-M9 gap pass 2 — LFO waveform consumer

Read-only extractor pass, 2026-09-28. Citations are ARM-mode virtual addresses in
`resources/lib/armeabi-v7a/libcozmoEngine.so`.

| step | what the original does | citation | record | classification |
|---|---|---|---|---|
| L1 | The type-21 vtable selects `0x009D671C` as its update method. The function reads waveform/type, frequency, depth, smoothing, PWM and phase properties, applies ranged randomization with the Wwise 64-bit LCG, and updates its phase/state block. | type-21 vtable `0x0103B200 = 0x009D671C`; body `0x009D671C..0x009D7727` | M9-025 | EXACT_SOURCE for properties/state |
| L2 | Waveform/type is stored at output `+0x24`; phase/frequency state occupies `+0x34..+0x48`. Type 1 adds a quarter-cycle and type 3 adds a half-cycle before wrapping the phase to `[0,1)`. | stores and phase branches in `0x009D6918..0x009D7727`; specifically type comparisons and phase offsets `0x009D7480..0x009D74BC` | M9-025 | EXACT_SOURCE for phase preparation |
| L3 | The shared voice-instantiation path calls the modulator virtual methods and creates a per-voice state object, but this pass did not identify the later function that converts the prepared phase/type into the actual waveform sample. | `0x009D7FC0..0x009D8137`; `0x009E266C..0x009E2813` | M9-025 | RECOVERABLE_GAP |

Exactly what remains: follow the type-21 per-voice object created at `0x009D7FC0`
(size `0xAC`, vtable `0x0104B268`) through its render/update vtable calls, then map
the reads of the copied LFO state to sine/triangle/square/saw output. The property
map and phase offsets alone do not establish the waveform equation.

M9-025 remains `RECOVERABLE_GAP`.



## Appendix D - gap pass 3 and verifier report

# I-M9 gap pass 3 — MIDI dispatch, RNG conclusion and citation verification

Read-only extractor and adversarial verifier pass, 2026-09-28. Citations are
virtual addresses in `resources/lib/armeabi-v7a/libcozmoEngine.so`; engine code is
Thumb and the Wwise region is ARM.

## MIDI dispatch gap

| step | what the original does | citation | record | classification |
|---|---|---|---|---|
| M1 | `0x009B3260` is the HIRC object-loader switch. Its `0x009B3EBC` block is the fallback/custom loader path, and `0x009BBF9C` is a bounded stream-read helper. They do not establish MIDI event routing or velocity-to-level behavior. | object-loader `0x009B3260..0x009B4033`; fallback `0x009B3EBC..0x009B3F64`; reader `0x009BBF9C..0x009BC17B` | M9-013/M9-014 | RECOVERABLE_GAP |
| M2 | `0x00A78D10` initializes a source object and its vtables/default fields; it does not establish the two requested MIDI semantics. | `0x00A78D10..0x00A78DE3` | M9-013/M9-014 | RECOVERABLE_GAP |
| M3 | Wwise owns the selection RNG. It advances the 64-bit LCG `s = s*0x5851F42D4C957F2D + 1` and uses the high word shifted right by one. The old M9-015 provenance that the engine supplies the generator is contradicted. | `0x0098A780..0x0098A7B8` | M9-015 | EXACT_SOURCE for RNG ownership; the full per-play conclusion must be compared during implementation |

Exactly what remains for M9-013/M9-014: identify the runtime object's MIDI-event
entry from the music/actor-mixer vtables reached after HIRC load, trace note-on and
note-off child filtering through the singing MIDI target, and follow the velocity
byte into every voice gain/RTPC input. The three X4 candidates were read and do not
settle either claim. These two records remain `RECOVERABLE_GAP` after the third and
final allowed gap pass.

## Verifier citation check

Every status-changing or contradictory item was opened in the `.so`: M9-007
(`0x00A14F88..0x00A15038`), M9-013/M9-014 (runtime factory and candidates above),
M9-015 (`0x0098A780..0x0098A7B8`), M9-017 (`0x006354E8`,
`0x00635558..0x0063556E`), M9-022 (`0x0098A6D4..0x0098A7B8`), M9-024
(`0x009D552C`, `0x009D5934`), M9-025 (`0x009D671C`) and M9-026
(`0x00AA257C`, `0x00AA18F4`). They support X4's corrections except that X4's
description of `0x009B3EBC` as a possible music handler is rejected: it is an
object-loader fallback and is retained only as a failed search location.

The verifier also opened 21 additional production rows: S1–S21. The checked
instruction ranges were `0x005EE8DC..0x005EEA0F`,
`0x005EEB30..0x005EEB6F`, `0x005EECB0..0x005EED6F`,
`0x005EEDD0..0x005EEE9F`, `0x005EF0C8..0x005EF25F`,
`0x005EF2B0..0x005EF30F`, `0x005EF490..0x005EF4DF`,
`0x00599F60..0x00599FBF`, and `0x00635474..0x006355BF`.
Widths, ordering, constants, first-listener gating, reset behavior and return values
match the report. No other row failed.

Verifier verdict: **PASS**, with the corrected characterization of `0x009B3EBC`
above and with M9-013, M9-014, M9-024 and M9-025 kept visible as
`RECOVERABLE_GAP`.



## Correction C1 (job B-M9, 2026-09-29): M9-002's per-step timeout, M9-003's `+0x84` return, and M9-027's mix rate

Source: an extractor pass for job B-M9, citations are ELF VAs in
`resources/lib/armeabi-v7a/libcozmoEngine.so` (engine code Thumb). It was run
because the build stopped with `MISSING` on M9-002 and M9-003.

### C1a. M9-002 — the 60.0 is a per-step action timeout, not a compound timeout

- The `movt r8,#0x4270` (60.0f) is at **`0x005eedca`**, not `0x005eee0c` as X4 S17
  wrote (`0x005eee0c` is `movs r0,#1`). X4's "60-second sequential compound"
  wording is imprecise.
- The 60.0 is the per-action timeout of each `TriggerAnimationAction` /
  `PlayAnimationAction`, stored at `PlayAnimationAction+0x94` (ctor `0x00544228`,
  store `0x00543c8e`) and returned by vtable slot `+0x2c` (getter `0x00545210`).
  `InitInternal` passes it as the 7th argument of all three actions
  (`0x005eedd0`, `0x005eee0e`, `0x005eee44`; forwarded `0x00544250..0x00544264`).
- The `CompoundActionSequential` itself has no 60-second timeout: its `+0x9c`
  delay is 0 and its `+0xa0` deadline is `-1.0f`. Its ctor is `0x0054f4f8`
  (called `0x005eedb2`); actions are added through vtable `+0x20`
  (`0x0054ec7c`) at `0x005eedee`, `0x005eee28`, `0x005eee62`.
- The ctor's sentinel `id==0 && timeout==60.0 -> FLT_MAX`
  (`0x00543c96..0x00543cb2`) is **not** triggered: these steps pass id 1.
- Timeout semantics: `IAction::UpdateInternal` (`0x00540d1c`) computes
  `start+timeout` (`0x540d9e..0x540daa`) and, on expiry, returns failure
  `0x3000018` (`0x540e7c..0x540f00`). `CompoundActionSequential::UpdateInternal`
  (`0x0054f70c`) fails the whole compound on a child failure
  (`0x54f7f2..0x54f8f2`) because no ignore-failure predicate is installed (the
  `AddAction` calls pass 0 at `0x005eedee`/`0x005eee28`/`0x005eee62`; wrapper
  test `0x0054ec8c`). **A step that does not complete within 60 s fails the
  compound; it does not advance.**

### C1b. M9-003 — `IBehavior+0x84` is the acting action's tag

- `+0x84` is the tag of the `IActionRunner` the behaviour is currently acting on
  (`action+0x60`), 0 when idle. Zeroed in the ctor (`0x005bbcc4`), set in
  `StartActing` (`0x005bdb4e`), cleared in `StopActing` (`0x005bd3e4`) and
  `HandleActionComplete` (`0x005be1fc`).
- `BehaviorSinging::UpdateInternal` returns 2 while `+0x84 == 0`, else 1
  (`0x005ef240..0x005ef24a`). `IBehavior::Update` (`0x005bd074`) dispatches to
  `UpdateInternal`; `BehaviorManager::Update` (`0x005a2f68`) and
  `BehaviorSystemManager::UpdateActiveBehavior` (`0x005a5cb4`) read **2 as
  `Status::Complete` and call `FinishCurrentBehavior`, 0 as a failed update
  (log + `FinishCurrentBehavior`), and 1 (or other) as keep running.** So 2 and
  0 both finish the behaviour; 1 keeps it running. The stack's
  `IBehavior.Update` returns `bool` (an M8 interface); its `true` corresponds to
  the engine's 1 (running) and its `false` to the engine's 2/0 (finish), which
  is behaviourally equivalent. The 0-vs-2 log distinction is not carried.

### C1c. M9-027 — the Robot_Bus_1 EQ/limiter run at the Wwise mix rate

- gapC 4.6 fixes the bus limiter's `L = sr*lookahead` at the **Wwise mix rate**
  ("At 48 kHz this is 431..."), and M6-018's policy is a 48000 Hz mix. The
  Hijack then resamples the mix to 22320 for the robot (M6-015/gapC 4.8). So the
  shipped `14298 Hz` low-pass is **in-band at the 48000 mix rate** (Nyquist
  24000); the 22320 robot rate applies only after the Hijack.
- The stack currently renders and runs the chain at 22320
  (`CozmoAudio.SampleRate`), so the exact chain is wired but at the wrong rate.
  Moving the Wwise mix to 48000 and resampling through the Hijack is M6-017 /
  M6-018. **M9-027 therefore stays `IMPLEMENTATION_GAP`, `unresolved` naming
  M6-017/M6-018.** The code must state the rate caveat rather than cap the
  low-pass at 22320 as if that were the engine's behaviour.

M9-002's evidence address is corrected to `0x005EEDCA` and its wording to
"per-step 60-second TriggerAnimationAction timeout". M9-003's row now names
`IBehavior+0x84` (the acting action tag). No other row changes.


## Correction A1 (manager audit, 2026-09-29)

The complete audit (`re-analysis/research/20260929-audit-complete.md`) found that some of this subsystem's settled records do not hold. The manager re-checked the central findings in the binary. Those records go back to IMPLEMENTATION_GAP, each with its defect in `unresolved`, to be rebuilt from the cited source. The report's findings are the rows for the rebuild, subject to the rebuilding job's own citation check.

## ADP-1 (operator decision, 2026-10-05)

The audio DSP policy in AGENTS.md (ADP-1) applies to this subsystem. Per-sample DSP arithmetic may become EQUIVALENT_IMPLEMENTATION; every decision stays exact. No record is reclassified by this note. Each mixed record states its boundary in `unresolved`, and moves only when its equivalent part passes the equivalence test (`jobs/B-ADP-HARNESS.md`).
