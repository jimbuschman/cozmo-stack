# Pre-extraction: open M5, M10 and M15 implementation gaps

- **Date:** 2026-09-30
- **Kind:** independent extraction
- **HEAD:** `5a1fa5b450493cca4cda2ea756043272a40ee0ac`
- **Scope rule:** current `IMPLEMENTATION_GAP` records in M5-animation, M10-derived and M15-freeplay whose `unresolved` does not begin with `built` (26 records: 14 M5, 6 M10, 6 M15).
- **Write scope:** this report only; no code, inventory, manifest, branch or commit changes.

Each section first quotes the current manifest text. The extraction table then names the native production path and current C# entry. `UNKNOWN` means the shipped sources inspected here do not settle that fact; it is not a suggested implementation. Addresses are Thumb VAs unless a row explicitly names another shipped ELF or Unity source. Float constants are stated as IEEE-754 binary32/binary64 bits when established.

## M5-animation

### M5-005

> **Title:** Variability: RandIntInRange on the static keyframe RNG per GetStreamMessage, unclamped  
> **Status/location:** IMPLEMENTATION_GAP — `cozmo-stack/src/Cozmo.Robot/Animation/AnimationScheduler.cs`  
> **Evidence:** C2/C3 RandIntInRange(a-v, a+v) with strb truncation; gap1 RNG semantics 0x0082F9B0..0x0082FAC6  
> **Effect:** keyframe values vary differently  
> **Unresolved:** Calibration audit (re-analysis/research/20260929-audit-calibration.md, Codex; manager re-checked the cited addresses in the binary 2026-09-30): for seed 0 the engine reads /dev/urandom once (0x0082F898) and seeds mt19937 with that word even if it is zero (0x0082F89C..0x0082F8A4); EngineRandom.EntropySeed loops until the word is nonzero. No test covers the production entropy branch.

**C# entry:** `EngineRandom(uint)` / `EntropySeed()`; head and lift keyframe `GetStreamMessage` paths in `AnimationScheduler`.

| step | address | what it does; gates, order and failure | floats / unsettled |
|---|---|---|---|
| Construct static keyframe RNG | 0x004D6C08..0x004D6C10; RNG ctor 0x0082F7CC..0x0082F8C8 | The `.init_array` constructs `IKeyFrame::sRNG` with seed 0. `SetSeed(name,0)` constructs `std::random_device("/dev/urandom")`, performs exactly one read, and passes that u32 to mt19937 even when it is zero. A random-device failure behavior was not recovered. | Entropy word is runtime `UNKNOWN`; zero is a valid recovered input, not a retry signal. |
| Draw | 0x0082F9B0..0x0082FA0A | `GetNextDbl=(d0+d1*2^32)*2^-64` from two mt19937 draws; integer range code follows at 0x0082FA0A..0x0082FAC6. | binary64 scales are exact powers of two. |
| Head/lift variability | 0x004F8C08..0x004F8C4A; 0x004F8F80..0x004F8FC4 | On every `GetStreamMessage`, when variability is nonzero, draw `RandIntInRange(value-var,value+var)` and store with `strb`; there is no clamp. Head interprets the byte as s8, lift as u8. Draw occurs at stream time, before message construction. | no float in the range path. |
| C# delta | `EngineRandom.cs:24,33-38` | `EntropySeed` repeats until nonzero, so the production stream differs only on a zero first entropy word. | Exact correction: one read, accept all 32-bit values. |

### M5-011

> **Title:** Group choice: mood, head window and cooldown filters; RandDbl(sum w) weighted draw; fallback to the Default mood  
> **Status/location:** IMPLEMENTATION_GAP — `cozmo-stack/src/Cozmo.Robot/Animation/AnimationLibrary.cs`  
> **Evidence:** D4 0x0058C4C0..0x0058C7BE; D5 0x0058A946..0x0058AB2E; D6 0x0058AB30..0x0058AE2A; R-ANIM pre-extraction part 1 item 4 4.1..4.12: group loop 0x0058A622..0x0058A73C, entry DefineFromJson 0x0058C4CC..0x0058C7D2, ctor 0x0058C4B0, backup 0x0058AC2A  
> **Effect:** a different clip is chosen  
> **Unresolved:** The reject-and-continue rules, the absence of Weight/Mood/HeadAngle defaults and the add-before-define group lifetime are settled (C5). The mood and the cooldown time come from MoodManager (M7): unset, Default and this machine's monotonic clock.

**C# entry:** `AnimationLibrary.GetAnimationNameFromGroup` -> `AnimationGroup.GetAnimationName` (`AnimationLibrary.cs:418,112`).

| step | address | what it does; gates, order and failure | floats / unsettled |
|---|---|---|---|
| Define group | 0x0058A622..0x0058A73C; entry 0x0058C4CC..0x0058C7D2 | Add the group by name before defining its entries. Each bad entry is rejected and the loop continues; the already-added group remains. Clip name must resolve. `Weight`, `Mood`, `HeadAngleMin/Max` have no source default; `CooldownTime_Sec` defaults to 0 and `UseHeadAngle` to false. | degrees-to-radians is binary32 in the entry loader; exact source operands are the values supplied by JSON. Missing Weight/Mood/window value: `UNKNOWN` (uninitialised/native JSON destination), not zero. |
| Candidate filter | 0x0058A946..0x0058AA74 | In list order require requested mood, then head window only when `UseHeadAngle`, then cooldown end <= MoodManager time. Accumulate weights only for survivors. | Mood when not supplied by caller is Default; current time is MoodManager+0x130. Its initialization/clock ownership is M7; value here is `UNKNOWN` until that seam exists. |
| Weighted draw | 0x0058AA76..0x0058AB2E | Draw `RandDbl(sumWeight)` from the context RNG, subtract each surviving weight in order, choose on first result `<0`; if rounding never crosses, choose the last survivor. Set chosen clip-name cooldown to `now+cooldown`. | binary64 draw/accumulation. Context RNG is shared with streamer/layers (R3), not `sRNG`. |
| Fallback | 0x0058AB30..0x0058AE2A | No candidates and mood != Default: recurse once with Default. In Default/non-strict use M5-014 backup; strict failure logs and returns empty. | ±0.05 rad = binary32 `0x3D4CCCCD` in the backup comparisons. |

### M5-013

> **Title:** faceAnimations: one sprite frame per stream frame, empty frames skipped, two RLE variants per image chosen by _firstScanLine, index reset on abort  
> **Status/location:** IMPLEMENTATION_GAP — `cozmo-stack/src/Cozmo.Robot/Animation/FaceAnimationLibrary.cs`  
> **Evidence:** C12 0x004F9708..0x004F98C4; FaceAnimationManager 0x00581254..0x005812C4; GetFrame 0x005817B4..0x005817CA; M3 B4; R-ANIM pre-extraction part 1 item 5 5.1..5.8: Threshold 0x00871E98, libopencv_core 0x7A7A2, variant store 0x00581254, GetFrame 0x005817B4, GetStreamMessage 0x004F9812, IsDone 0x004F9770  
> **Effect:** sprite faces look different  
> **Unresolved:** The threshold is strictly above 0x80, the variant and lazy index reset are settled, and an unknown name (0 frames) is done at once (C5). The writers of keyframe +0x28 (the IsDone override) are RECOVERABLE_GAP (0x004F9770, 0x004F97DE).

**C# entry:** `FaceAnimationLibrary.Add/GetVariant`; face-animation runtime in `AnimationScheduler` consumes the library one frame per stream frame.

| step | address | what it does; gates, order and failure | floats / unsettled |
|---|---|---|---|
| Load image | Threshold 0x00871E98; shipped OpenCV core 0x0007A7A2 | Threshold is strict `pixel > 0x80`. Build and store two encoded images: one with even rows cleared, one with odd rows cleared. | integer pixels only. |
| Select variant | 0x00581254..0x005812C4; 0x005817B4..0x005817CA | `GetFrame(index,_firstScanLine)` picks the opposite-cleared variant required by the current scan-line parity. Out-of-range/missing frame reports failure. | no floats. |
| Stream | 0x004F9708..0x004F98C4 | Unknown animation name has zero frames and is done immediately. Each call consumes at most one sprite frame; an empty frame advances index without sending. A non-empty frame emits the face message, then advances. Abort resets the index lazily. | Failure to fetch logs and consumes according to the cited body. The purpose and writers of keyframe `+0x28`, which override `IsDone` at 0x004F9770/0x004F97DE, remain `UNKNOWN`. |

### M5-014

> **Title:** Cooldown keyed by clip name across groups; the Default-mood backup rule within +-0.05 rad, else the first entry  
> **Status/location:** IMPLEMENTATION_GAP — `cozmo-stack/src/Cozmo.Robot/Animation/AnimationLibrary.cs`  
> **Evidence:** D5 cooldown set on a pick; MoodManager::Update 0x0067B5E6; D6 0x0058AB30..0x0058AE2A; D7 0x0058BA28..0x0058BA98; R-ANIM pre-extraction part 1 item 4 4.8..4.12: CooldownTime default 0x0058C868, UseHeadAngle 0x0058C734, HeadAngle 0x0058C770, backup 0x0058AC2A  
> **Effect:** clips repeat or are skipped differently  
> **Unresolved:** No defaults for Weight, Mood or HeadAngleMin/Max (C5). For an entry without UseHeadAngle the head-angle fields are uninitialised stack; the forced choice (SD2) is that such an entry is outside every head window, so the backup falls to the first entry - put to the policy review. The cooldown time is MoodManager+0x130 (M7): unset, this machine's monotonic clock stands in.

**C# entry:** `AnimationGroup.GetAnimationName` and the library-wide cooldown map in `AnimationLibrary.cs:112`.

| step | address | what it does; gates, order and failure | floats / unsettled |
|---|---|---|---|
| Cooldown map | 0x0058BA28..0x0058BA98 | Key is animation clip name, so the same clip shares a deadline across groups. On cooldown iff `deadline > now`. Normal weighted pick writes `now+CooldownTime`; backup does not. | time is binary64 MoodManager time; initialization is `UNKNOWN` at this layer. |
| Default backup | 0x0058AB30..0x0058AE2A; selection 0x0058AC2A | Only after Default mood has no normal candidate and only when non-strict. Among Default entries whose current head lies in `[min-0.05,max+0.05]`, ignoring `UseHeadAngle`, choose the smallest remaining cooldown. If none, choose first list entry. | 0.05f = `0x3D4CCCCD`. For `UseHeadAngle=false`, min/max are uninitialised: their comparison result is `UNKNOWN`; the existing C# forced choice “never qualifies” is policy, not extracted fact. |
| Failure | same | Strict mode or non-Default fallback exhaustion logs and returns empty. | No additional clamp/default. |

### M5-017

> **Title:** Audio alternative: r = RandDbl(1), first with lower <= r <= upper skipping |p| < 1e-5, else nothing  
> **Status/location:** IMPLEMENTATION_GAP — `cozmo-stack/src/Cozmo.Robot/Animation/AnimationScheduler.cs`  
> **Evidence:** C14 0x004F9AEC..0x004F9CBE, 0x004F9DAC..0x004F9DFC  
> **Effect:** a different sound plays  
> **Unresolved:** Calibration audit (re-analysis/research/20260929-audit-calibration.md, Codex; manager re-checked the cited addresses in the binary 2026-09-30): the audio-alternative draw uses the entropy-seeded keyframe RNG, so it inherits M5-005's zero-entropy retry defect (EngineRandom.cs vs 0x0082F86A..0x0082F8A4). The named tests inject nonzero seeds.

**C# entry:** audio keyframe runtime `ChooseAlternative` (`AnimationScheduler.cs:1582,1623`) using `_keyframeRng`.

| step | address | what it does; gates, order and failure | floats / unsettled |
|---|---|---|---|
| Draw | 0x004F9AEC..0x004F9B30 | `GetAudioRefIndex(true)` draws binary64 `RandDbl(1)` from the same entropy-seeded static keyframe RNG as M5-005, then casts the draw to binary32. | bound 1.0 = binary64 `0x3FF0000000000000`; cast is binary32. Entropy first word remains runtime `UNKNOWN`; zero must be accepted. |
| Walk | 0x004F9B30..0x004F9CBE | In source order skip `abs(p)<1e-5`; intervals are inclusive at both lower and upper ends; choose the first containing the binary32 draw. | 1e-5f = `0x3727C5AC`. |
| No match | 0x004F9DAC..0x004F9DFC | Return index -1, then static default audio ref `{eventId=0,volume=1.0}`; nothing plays. | 1.0f = `0x3F800000`. |

### M5-018

> **Title:** Frames per Update while ShouldProcessAnimationFrame: empty buffer and keyframes left, or audio ready while audio exists  
> **Status/location:** IMPLEMENTATION_GAP — `cozmo-stack/src/Cozmo.Robot/Animation/AnimationScheduler.cs`  
> **Evidence:** A15 0x0057CC6C..0x0057CCA6; M3 C15, C16  
> **Effect:** animations stream faster or slower  
> **Unresolved:** The audio animation is a stand-in for RobotAudioClient's RobotAudioAnimation (M6): it exists for an animation with RobotAudio keyframes, starts each at its trigger in Update, is always ready, and is complete once its track is at the end and nothing plays; the engine's states (C16, Q5), whether an audio-less animation gets one, and the first frame's latency are M6's. A sound whose samples are not rendered yet sends silence for the frame and keeps its place.

**C# entry:** `AnimationScheduler.UpdateStreamLocked` -> `ShouldProcessAnimationFrame` (`AnimationScheduler.cs:1173,1158`).

| step | address | what it does; gates, order and failure | floats / unsettled |
|---|---|---|---|
| Frame-loop predicate | 0x0057CC6C..0x0057CCA6 | False whenever the outgoing buffer is non-empty. Without a RobotAudioAnimation: true iff animation keyframes remain. With one: true iff keyframes remain, or audio object says it is ready/has work according to its state. | no float literal. |
| Per-update order | A15 plus M3 C15/C16 | Drain buffered bytes first; a failed drain returns immediately. Then build/drain frames one at a time while the predicate holds and budget permits. A late tick catches up frame-by-frame, never by jumping the stream clock. | Audio object creation for an audio-less clip, exact readiness states, and first-frame audio latency are `UNKNOWN` here and belong to M6 extraction. |
| Not-ready sample | M6 seam named in unresolved | The source-backed integration rule is to send silence for that frame and keep the audio position. | Exact Wwise readiness/failure result is `UNKNOWN` in this record. |

### M5-020

> **Title:** Expressions: the shipped Code Lab AnimationTrigger mapping and Unity Random; no invented faces  
> **Status/location:** IMPLEMENTATION_GAP — `cozmo-stack/src/Cozmo.Robot/Animation/Expressions.cs`  
> **Evidence:** operator decision 2026-09-29: no invented faces; rebuild on the shipped Code Lab expression mapping; CodeLabGame.GetAnimationTriggerForScratchIndex CodeLabGame.cs:3094, Random.Range(1,34) :3101, Random.Range(1,14) :3106; R-ANIM pre-extraction part 1 item 11 R3..R8: RandomRangeInt 0x10BCE8, xorshift128 0x10BD00..0x10BD2C, InitState 0x10BC00, time(NULL) seed 0x71ED4..0x71F04  
> **Effect:** none unless a caller asks for one  
> **Unresolved:** The Code Lab AnimationTrigger mapping and the xorshift128 generator/range rule are settled and built (C5). The seed value comes from Mono's Environment.get_TickCount in libmono.so, which is not read, so it is UNKNOWN; the stack uses the host's Environment.TickCount as the forced stand-in (SD2, time-seeded randomness) - put to the policy review.

**C# entry:** `Expressions.TriggerForIndex` (`Expressions.cs:120,124`) and `UnityRandom.Shared`.

| step | address/source | what it does; gates, order and failure | floats / unsettled |
|---|---|---|---|
| Map | shipped `CodeLabGame.cs:3094..3244` | Nonzero scratch indices map by the shipped switch; out-of-range returns `Count`. Index 0 draws vertical `Random.Range(1,14)` or nonvertical `Random.Range(1,34)`, then applies the same switch. There is no named-expression face synthesis. | integer-only. |
| Unity PRNG | `libunity.so` 0x0010BC00, 0x0010BCE8, 0x0010BD00..0x0010BD2C | Four-word xorshift128 state; `Range(int,int)` uses the recovered modulo/range rule and consumes one state transition. | no floats. |
| Seed | Mono call path unresolved | Unity initializes from Mono `Environment.get_TickCount`; the implementation/body in shipped `libmono.so` was not read. | Exact seed value, wrap and clock basis are `UNKNOWN`; host `Environment.TickCount` is a compatibility choice, not source recovery. |

### M5-021

> **Title:** Eye fill = shipped OpenCV 3.1.0 fillConvexPoly LINE_4 and ellipse2Poly; DrawEye outline and lid polygons; _firstScanLine offset; fill order  
> **Status/location:** IMPLEMENTATION_GAP — `cozmo-stack/src/Cozmo.Robot/Animation/ProceduralFaceRenderer.cs`  
> **Evidence:** gap1 D1..D6 DrawEye 0x0058520E..0x00585896; gap3 A1..A10 libopencv_imgproc fillConvexPoly 0x00042F58, FillConvexPoly 0x00042958, Line 0x000419DC, LineIterator 0x00041810, clipLine 0x000410DC (matches stock 3.1.0); gap3 B1..B3 ellipse2Poly 0x00043C48 (SinTable 0x000E7910, vcvtr ties-to-even)  
> **Effect:** the eyes are drawn with different pixels  
> **Unresolved:** The trig in DrawEye and GetTransformationMatrix is MathF.Tan/Cos/Sin (bionic libm may differ in the last ulp, which can move a rounded point at a tie); the float operation order of the point transform is m00·x + m01·y + m02. Rounding assumes the default FPSCR mode (MD2).

**C# entry:** `ProceduralFaceRenderer.DrawFace` -> eye polygon construction/fill (`ProceduralFaceRenderer.cs:74` onward).

| step | address | what it does; gates, order and failure | floats / unsettled |
|---|---|---|---|
| Geometry | 0x0058520E..0x005857F0 | Four outline arcs; lower and upper lids use binary32 tan/cos, roundf, angle truncated to int, `ellipse2Poly` delta 10; transform each point in operation order `m00*x + m01*y + m02`; add `_firstScanLine` offset. | degrees-to-radians literal and all pose values are binary32. Exact bionic `tanf/cosf/sinf` last-bit behavior is `UNKNOWN` until the shipped libm path is extracted. |
| ellipse2Poly | `libopencv_imgproc.so` 0x00043C48, SinTable 0x000E7910 | Shipped OpenCV 3.1 uses its sin table and `vcvtr` ties-to-even point rounding. | default FPSCR rounding mode is assumed by shipped code; runtime mode is `UNKNOWN` if changed externally. |
| Fill | 0x005857E0..0x00585896; imgproc 0x00042F58/0x00042958/0x000419DC/0x00041810/0x000410DC | Order: outline 255; AddOffNoise if distorter; upper lid 0; lower lid 0. LINE_4 draws each edge with 4-connected `LineIterator`, clips, then fills inclusive 16.16 spans using truncating dx. | integer raster after rounded vertices. |

### M5-022

> **Title:** Disconnect and teardown: a failed send only returns; ~Robot AbortAll cancels actions, aborts path and docking, sends AbortAnimation 0x8D and StopAllMotors  
> **Status/location:** IMPLEMENTATION_GAP — `cozmo-stack/src/Cozmo.Robot/Animation/AnimationScheduler.cs`  
> **Evidence:** A37 0x0051194C..0x0051197C; 0x00511120; 0x0051154A; ~AnimationStreamer 0x0057AF58  
> **Effect:** the robot keeps animating or moving after teardown  
> **Unresolved:** PathComponent::Abort and AbortDocking (A37) are M12/M13's and not run at teardown. The extra DriveWheels(0) after StopAllMotors is this stack's (PROJECT_STATE decision note), not the engine's.

**C# entry:** robot/animation teardown through `CozmoAnimations.Dispose`; scheduler send-failure path in `AnimationScheduler`.

| step | address | what it does; gates, order and failure | floats / unsettled |
|---|---|---|---|
| Ordinary send failure | streamer UpdateStream/Update, A37 | Return the transport/send error immediately. Do not send End, clear stream state, or advance past the failed bytes. | no floats. |
| Robot teardown order | 0x0051194C..0x0051197C; AbortAll 0x00511120; sends 0x0051154A | `~Robot` calls `AbortAll`: cancel ActionList (which can abort a play action), abort PathComponent, abort docking, send AbortAnimation tag `0x8D`, then `StopAllMotors`; only afterward is `~AnimationStreamer` reached. | Whether a dropped physical link receives the attempted sends is M1/hardware, not changed here. |
| C# delta | current `CozmoAnimations.Dispose` / teardown | Path abort and docking abort are absent; an extra `DriveWheels(0)` is sent after StopAllMotors. | Exact correction is native order with no extra wheel command. |

### M5-027

> **Title:** Idle animations: the idle stack, PushIdle/RemoveIdle, idle InitStream with tag 0xFF, ProceduralLive, the no-animation path  
> **Status/location:** IMPLEMENTATION_GAP — `cozmo-stack/src/Cozmo.Robot/Animation/AnimationScheduler.cs`  
> **Evidence:** A1 0x0057A064..0x0057A0AC; A28 0x0057D03A..0x0057D060, 0x0057D122..0x0057D1E2; A29 0x0057D064..0x0057D448; A30 0x0057B914..0x0057BD6E; R-ANIM pre-extraction part 1 item 9 9a..9c: +0x64 writers 0x00579FC6, 0x0057B926, 0x0057BD66, 0x0057D022, 0x0057D3F6; drain 0x0057D168; HasResponse 0x00670AD0  
> **Effect:** idle behaviour differs  
> **Unresolved:** Audit 2026-09-29 (re-analysis/research/20260929-audit-complete.md): the settlement did not hold. when a clip's loops run out the engine clears +0x38 and branches past the idle-stack test to HaveLayersToSend/StreamLayers (0x0057D25C..0x0057D260), which can send a new StartOfAnimation; the stack runs the idle that tick, so later tags are off by one; the live-idle entry rests on the invented StreamLive seam (M7-017); the UpdateLiveAnimation error path continues where the engine returns. Earlier note: Settled (C5): +0x64 is set to 1 by an idle's InitStream tail; the Count-top flush refreshes the budgets before the drain; HasAnimationForTrigger is HasResponse.

**C# entry:** `AnimationScheduler.Update`, `PushIdleAnimation`, `RemoveIdleAnimation`, and `InitIdleLocked` (`AnimationScheduler.cs:826..` and update around 0x0057D-equivalent comments).

| step | address | what it does; gates, order and failure | floats / unsettled |
|---|---|---|---|
| Initial stack | 0x0057A064..0x0057A0AC | Stack begins `{Count,"default_anim_lock"}`. Push appends; Remove refuses the last entry, warns for missing/middle lock, and clears the idle-started byte only when the new top is Count. | no floats. |
| No current clip | 0x0057D03A..0x0057D1E2 | Count/empty top: refresh send budgets, flush leftovers, then drain; a drain failure returns. Real idle top: resolve trigger/group, `InitStream(tag=0xFF)`, set +0x64 at the Init tail. `HasAnimationForTrigger` is the response lookup. | failure to resolve logs/follows A29; no invented clip. |
| Clip ends this tick | 0x0057D25C..0x0057D260 | Clear current clip and branch **past** the idle-stack test to layer-send handling. A layer can emit a new StartOfAnimation. The idle is not initialized until a later tick. | This is the current C# ordering defect. |
| ProceduralLive | 0x0057D3F6; UpdateLiveAnimation 0x0057D5F8.. | Native procedural live invokes UpdateLiveAnimation; a nonzero result logs and returns from Update. | The only C# production route is the M7 `StreamLive` seam, which appends its own frames and suppresses this generator: production ownership remains `UNKNOWN`/unbuilt. |

### M5-030

> **Title:** Live idle (UpdateLiveAnimation): gates, body/lift/head wiggles with their parameters, LiveIdleTurn eye shift, lock and carry checks  
> **Status/location:** IMPLEMENTATION_GAP — `cozmo-stack/src/Cozmo.Robot/Animation/AnimationScheduler.cs`  
> **Evidence:** A35 0x0057D5F8..0x0057DA82; gap1 K2 0x0058CFCE..0x0058D04A; K10 0x0064F3C8..0x0064F498; R2..R4  
> **Effect:** the live idle moves differently  
> **Unresolved:** CarryingComponent (+0x284, M12) is not on this robot, so the lift's carrying gate reads clear. With ProceduralLive pushed by the StreamLive seam (the M7 idle behaviour, an M7-017 interface) the generator does not run, as that behaviour appends its own keyframes.

**C# entry:** `AnimationScheduler.UpdateLiveAnimationLocked` (`AnimationScheduler.cs:1057`).

| step | address | what it does; gates, order and failure | floats / unsettled |
|---|---|---|---|
| Outer gates | 0x0057D606..0x0057D624 | Return 0 without decrement unless live enabled, stream time >= `TimeBeforeWiggle`, and DockingComponent is not picking/placing. | timer/params are signed integers in ms. |
| Countdown order | 0x0057D626..0x0057D6BC | Body, lift, head every call. If moving/not-in-position, corresponding track locked, or duration+spacing > 0: subtract 60 from duration and skip. Lift also skips/counts down while carrying. Spacing is never decremented. | integer 60 ms. CarryingComponent absence in C# means the gate is always clear. |
| Body | 0x0057D6CC..0x0057D978 | Draw duration, speed, then binary64 `RandDblInRange(0,1)`. If `r > p8`, point turn and `LiveIdleTurn` eye shift; otherwise straight radius `0x7FFF` and remove prior eye shift. Then append body keyframe, set spacing. | eye args: 33=`0x42040000`, 64=`0x42800000`, 32=`0x42000000`, 1.1=`0x3F8CCCCD`, .85=`0x3F59999A`, .1=`0x3DCCCCCD`. |
| Lift/head | 0x0057D97E..0x0057DA78; 0x0057D812..0x0057DA4C | Lift draws duration, appends `(u8)p13,(u8)p14`, sets spacing. Head draws duration, truncates `headRad*57.2958f` to s32 then s8, appends with variability, sets spacing. | 57.2958 literal must be copied from the cited binary32 word; decimal spelling alone is insufficient. |
| Append failure | 0x0057DD6C/0x0057DE68/0x0057DF18; helper 0x005791C8..0x0057926E | Track refuses only when size >1000; no trigger-order check. Failure logs track-specific message and returns 1; caller logs LiveUpdateFailed and returns from update. | no float. |

### M5-031

> **Title:** Glitch: AddGlitch face and backpack layers, GetNextDistortionFrame table, ScanlineDistorter, per-row shift, AddOffNoise  
> **Status/location:** IMPLEMENTATION_GAP — `cozmo-stack/src/Cozmo.Robot/Animation/AnimationScheduler.cs`  
> **Evidence:** gap1 G1, G3..G11 (0x0064EDE8..0x0064EEFE, 0x0053A9A8..0x0053AB1E, 0x0053A0B0..0x0053A898, 0x00585DFE..0x00585E94); gap2 G1..G7 GenerateGlitchLights 0x0058CB64..0x0058CD2A  
> **Effect:** glitches look different  
> **Unresolved:** Audit 2026-09-29 (re-analysis/research/20260929-audit-complete.md): the settlement did not hold. the per-row shift fraction is computed differently (see M5-032); the glitch input is never set on this stack.

**C# entry:** `TrackLayerComponent.AddGlitch` (`TrackLayers.cs:617`), `ScanlineDistorter`, and `ProceduralFaceRenderer.DrawFace`.

| step | address | what it does; gates, order and failure | floats / unsettled |
|---|---|---|---|
| Trigger/add layers | 0x0064EDE8..0x0064EEFE | Only degree > 1e-5 invokes AddGlitch. Generate face distortion and add face layer `Glitch`; generate five backpack keyframes and add backpack layer `Glitch`. Layer application is ascending tag; highest current backpack layer overwrites the whole five-LED keyframe. | 1e-5f=`0x3727C5AC`. Current stack never supplies the production distortion input, so this entry is unreachable. |
| Distortion table/state | 0x0053A9A8..0x0053AB1E; 0x0053A0B0..0x0053A898 | Function-static RNG seed 1. Eleven-entry distortion table, hold draw, per-frame update and eye amount follow G rows. AddOffNoise transforms random points, rounds/clamps, chooses width 1..3; width 2 or 3 writes three pixels. | table float words are the authority; retain them bit-for-bit. |
| Backpack generation | 0x0058CB64..0x0058CD2A | Five frames: Middle red first, then recovered random LED pattern/order. Failure to add layer follows TrackLayer manager result. | source keyframe colours/times are integer fields. |
| Row shift | 0x00585DFE..0x00585E94 | Uses reciprocal-once then multiply, not direct division; details are M5-032. | binary32 operation order is behavior-changing. |

### M5-032

> **Title:** DrawFace: 64x128 canvas, the face transform via shipped cv::warpAffine INTER_NEAREST BORDER_CONSTANT 0, row extent, interlace clearing, scan-line shift  
> **Status/location:** IMPLEMENTATION_GAP — `cozmo-stack/src/Cozmo.Robot/Animation/ProceduralFaceRenderer.cs`  
> **Evidence:** E1 0x00585B30..0x00585D9A; E2 0x00585D9C..0x00585E94; gap1 D4, D5; gap3 C1..C6 warpAffine 0x00081850.., invoker 0x0007FC60..0x0008016A, remapNearest 0x00072C38..0x00072D8C (matches stock 3.1.0); R-ANIM pre-extraction part 1 item 10 10a..10e: GetTransformationMatrix 0x00584FF8, InputArray 0x00585C84, warpAffine imgproc 0x0008193C  
> **Effect:** the face is drawn with different pixels  
> **Unresolved:** Audit 2026-09-29 (re-analysis/research/20260929-audit-complete.md): the settlement did not hold. the engine computes 1/(max-min) once and multiplies (0x00585E24..0x00585E34); the stack divides directly, one ulp different in 460 of 2016 cases; bionic cosf/sinf (0x0058503C) are MathF here, an unrecorded substitution; it rests on M5-021 rows. Earlier note: Settled (C5): the face matrix is float 2x3 CV_32FC1; warpAffine converts it to double.

**C# entry:** `ProceduralFaceRenderer.DrawFace` (`ProceduralFaceRenderer.cs:74`).

| step | address | what it does; gates, order and failure | floats / unsettled |
|---|---|---|---|
| Canvas/eyes | 0x00585B30..0x00585D9A | Allocate 64x128 zero canvas; draw both eyes and derive row extent from transformed eye-box corners. | all geometry before OpenCV matrix conversion is binary32. |
| Face transform | GetTransformationMatrix 0x00584FF8; call 0x00585C84/0x00585C9C; imgproc 0x00081850.. | If not identity, build 2x3 `CV_32FC1`, clone the source because operation is in-place, then `warpAffine(INTER_NEAREST,BORDER_CONSTANT,0)`. Shipped OpenCV converts the matrix to double internally and uses nearest remap. | bionic `cosf/sinf` exact last bits are `UNKNOWN`; source multiply/add order is fixed. |
| Interlace | 0x00585D9C..0x00585DFE | Clear every row with parity `_firstScanLine` inside the recovered row extent. | integer. |
| Scan-line shift | 0x00585E24..0x00585E94 | Compute binary32 reciprocal `r=1/(max-min)` once, then for each row multiply `(row-min)*r`; use that fraction in the distortion shift. Direct per-row division is not equivalent. | Operation sequence is `vdiv.f32` once, then `vmul.f32`; current C# differs by up to one ULP. |

### M5-035

> **Title:** The streamer never reads enabledAnimTracks or skips a locked track; locks only through DisableAnimTracks/EnableAnimTracks; the live idle checks MovementComponent locks  
> **Status/location:** IMPLEMENTATION_GAP — `cozmo-stack/src/Cozmo.Robot/Animation/AnimationScheduler.cs`  
> **Evidence:** B1 0x00537FFC..0x00537FFE; B2 0x00513000..0x0051304E; B3 0x0057C94E..0x0057CA7A; B4 0x0057D636, 0x0057D664, 0x0057D69E  
> **Effect:** tracks are suppressed that the engine would send  
> **Unresolved:** Audit 2026-09-29 (re-analysis/research/20260929-audit-complete.md): the settlement did not hold. B4 (the live idle checks the MovementComponent locks) rests on M5-030 (IMPLEMENTATION_GAP) and is disabled on the only production route by the StreamLive seam.

**C# entry:** `AnimationScheduler` frame build and `UpdateLiveAnimationLocked`; motion locks are exposed by the movement component.

| step | address | what it does; gates, order and failure | floats / unsettled |
|---|---|---|---|
| enabledAnimTracks | 0x00537FFC..0x00537FFE | The game value is stored, but no streamer read exists. It is not a per-frame suppression mask. | no floats. |
| Lock interface | 0x00513000..0x0051304E | Track locking acts through `DisableAnimTracks`/`EnableAnimTracks` commands and MovementComponent lock ownership, not by skipping animation output. | Firmware interpretation of commands belongs to M4/hardware. |
| Normal stream | 0x0057C94E..0x0057CA7A | The streamer visits/sends the due animation tracks regardless of movement locks. Any C# normal-stream lock check is unsupported. | no floats. |
| Live idle exception | 0x0057D636/0x0057D664/0x0057D69E | Only UpdateLiveAnimation checks body/lift/head lock masks before generating new live keyframes. | This path is currently unreachable on the C# production route because M7's StreamLive seam supplies frames; ownership remains incomplete. |

## M10-derived

### M10-001

> **Title:** Off-treads classifier CheckAndUpdateTreadsState: gate, inputs, thresholds, branches, 250 ms debounce commit and every consequence  
> **Status/location:** IMPLEMENTATION_GAP — `cozmo-stack/src/Cozmo.Robot/OffTreads.cs`  
> **Evidence:** A1..A7 CheckAndUpdateTreadsState 0x511E00..0x5121F8 (thresholds 0x5121FC, 0x5122A0..0x5122BC); A8..A15 consequences: Falling DAS + ActionList::Cancel(-1) (0x511FF0..0x512084; gap1 3a..3e), RobotOffTreadsStateChanged broadcast 0x512092, OnTreads 0x512112..0x512184, carried 0x512100, SetOnChargerPlatform 0x512188, pause flag 0x5121E2; gap1 5a IMU filter state zeroed (0x5100E0..0x5100FE); R-ANIM pre-extraction part 2 item 1 O1..O4: Radians operator> 0x84CC90, IsNear 0x84CC0A, operator< 0x84CD12, rescale 0x84C87C  
> **Effect:** the robot state (picked up, on back, on side) is classified differently  
> **Unresolved:** The Radians comparisons are settled (C3): operator> tests the raw diff > 0 and wraps inside IsNear. The M11/M12/M15 consequences are seams.

**C# entry:** `OffTreadsClassifier.Update` (`OffTreads.cs:168` onward), called from the robot-state update path.

| step | address | what it does; gates, order and failure | floats / unsettled |
|---|---|---|---|
| Gate/inputs | 0x00511E1C..0x00511E60 | If head-calibrated `+0x314` is zero, return 0 with no changes. Read pitch `+0x304`, filtered accel Y `+0x384`, falling/picked-up status bits, and physical flag `+0x14`; gyro and filtered magnitude are not classifier inputs. | first filtered fields start zero from 0x005100E0..0x005100FE. |
| Classify | 0x00511E66..0x0051225C; literals 0x005121FC/0x005122A0..BC | Compute side, face, back and level in the exact A5/A6 priority: falling; side; face/extreme; back; otherwise level/picked-up/tail. Candidate timestamp adjustments are `now-250`, `now`, or `now+750`. | native binary32 words at cited pool are authoritative: −9800=`0xC6192000`, 3000=`0x453B8000`; other angle thresholds must be copied from 0x005122A0..BC, not rounded decimals. Radians `>` is raw diff >0 and not IsNear(1e-5f=`0x3727C5AC`). |
| Debounce/commit | 0x00511FD0..0x0051208E | Commit only when unsigned `t+250 <= now` and new candidate differs. Effective delays: back 1000 ms; side/face/level-in-air 250 ms; falling/tail-on-treads/pickup-in-air immediate. Return 1 only on commit. | integer ms. |
| Consequences, ordered | 0x00511FF0..0x005121F8 | Enter Falling stamps robot timestamp, emits DAS, `ActionList.Cancel(-1)` wildcard. Leaving emits DAS/duration. Then broadcast state change; OnTreads localizable-object stores; non-treads detach carried object; non-treads clear charger; set Freeplay pause flag OffTreads; Viz; return. | `ActionList.Cancel(-1)` queue/completion semantics are recovered at M10 gap1 3a..3e. |
| C# gaps | `OffTreads.cs` live handlers | Classifier core is present; ActionList cancellation and M11/M12/M15 consequence owners are absent/seams. | Those calls are not optional: implementation must stop with MISSING until their interfaces are built. |

### M10-003

> **Title:** Reaction-strategy factory rules and strategy classes (Cliff, Falling, PickedUp, Shaken, Slope, Frustration, PlacedOnCharger, Sparked, NoPreDockPoses, FistBump, Hiccup, Pet, CubeMoved, FacePositionUpdated, ObjectPositionUpdated)  
> **Status/location:** IMPLEMENTATION_GAP — `cozmo-stack/src/Cozmo.Robot/Behavior/ReactionStrategies.cs`  
> **Evidence:** C12 factory switch 0x60D618; C13..C17 Generic, Shaken, Slope, Frustration; gap1 1 Cliff filter 0x60DC7C..0x60DCA2; gap1 8 strategy predicates; gap2 1..6 PlacedOnCharger 0x614474..0x6144D0, CubeMoved 0x60C04A..0x60C1E2 / 0x60BEB4, Face 0x60CB84..0x60CE20, position-update base 0x612168..0x612B22, Object 0x6114A0..0x611582; R-ANIM pre-extraction part 2 item 5.1: EnabledStateChanged +0x1C is a no-op (0x60B73B) for Shaken/Slope/Frustration  
> **Effect:** reactions trigger differently  
> **Unresolved:** The +0x1C EnabledStateChanged for Shaken, Slope and Frustration is settled as a no-op (C5). The CubeMoved (0x60C03C) and Hiccup (0x610AF8) +0x1C bodies are RECOVERABLE_GAP; FistBump, Sparked, the NoPreDockPoses +0x70 writers and Pet EnabledStateChanged (InitReactedTo) are not built.

**C# entry:** strategy classes and `ShippedReactionStrategies.CreateAll` (`ReactionStrategies.cs:74..516`).

| step | address | what it does; gates, order and failure | floats / unsettled |
|---|---|---|---|
| Factory/generic | 0x0060D618 table; 0x00613948..0x006139EC; 0x0060F3BC..0x0060F454 | Construct the mapped class/events/filter. Generic event filter may latch; false filter leaves old latch. Only when running/runnable is true is WantsToRun called, so latch survives non-runnable periods; timeout requires a recent subscribed tag. Running byte `+0xA1` is established by 0x005BCCBE..0x005BD10C. | timeout 3000 is integer ms. |
| Cliff/falling/basic treads | 0x0060DC7C..0x0060DCA2; switch cases 0x0060D618.. | Cliff only latches while trigger enabled and Cliff is not already current. Falling listens to tag 58, timeout 3000, and its enable filter. PickedUp/back/face/side read the exact off-treads enum states. | no float except classifier inputs owned M10-001. |
| Shaken/slope/frustration | 0x00612FD4..0x0061300C; 0x006144FC..0x00614688; 0x0060EE6E..0x0060EEF4 | Shaken: filtered |a| >16000. Slope: pitch strictly 10..55°, raw-gyro max <0.01, quiet >0.4 s, picked/recent 1.5 s, state<2. Frustration: not current trigger 4, Confident below configured max, cooldown expired, runnable. EnabledStateChanged is no-op. | 16000=`0x467A0000`, .01=`0x3C23D70A`, .4=`0x3ECCCCCD`, 1.5=`0x3FC00000`; angle literals must use cited binary words. |
| PlacedOnCharger | 0x006143F8..0x006144D0 | Event overwrites latch from `onCharger`. First WantsToRun starts 20 s deadline; each call clears latch; true only `now>=deadline && latch`. | 20.0f=`0x41A00000`. |
| Cube/face/object | 0x0060C04A..0x0060C1E2; 0x0060CB84..0x0060CE20; 0x00612168..0x00612B22; 0x006114A0..0x00611582 | Preserve enable-state record reset/seed behavior, target filters, pose/time hysteresis, first-named face and 300/400 mm close thresholds, 4 s cooldown, object family/carried-object gates, and reaction acknowledgements. CubeMoved event handler is gated by trigger 8, not CubeMoved. | 300=`0x43960000`, 400=`0x43C80000`, 4.0=`0x40800000`; base angle/distance words at 0x00612168 body. |
| Unsettled classes | vslots named in unresolved | CubeMoved and Hiccup EnabledStateChanged bodies remain RECOVERABLE_GAP. FistBump, Sparked, NoPreDockPoses writer conditions, and Pet `InitReactedTo` lack complete C# builds. | Their untraced behavior is `UNKNOWN`; do not substitute no-op. |

### M10-004

> **Title:** CheckReactionTriggerStrategies: sticky action gate, map order, disable locks, predicates, StopAllMotors and track unlock, no-break loop, IsReactionTriggerEnabled and lock messages  
> **Status/location:** IMPLEMENTATION_GAP — `cozmo-stack/src/Cozmo.Robot/Behavior/BehaviorManager.cs`  
> **Evidence:** C3 gate 0x5A355A..0x5A357A; C4..C8 0x5A359A..0x5A37CA; gap1 4a IsReactionTriggerEnabled 0x5A40B8; 4b DisableReactionsWithLock 0x5A27F6..0x5A2960; 4c RemoveDisableReactionsLock 0x5A3A52..0x5A3B94; 4e game messages 0x5A4D2A..0x5A4F3A; R-ANIM pre-extraction part 2 item 3.1..3.7: CompletelyUnlockAllTracks 0x640F84..0x641092 (sets MC+0x28+12k, index payload 0x64101A)  
> **Effect:** reactions are enabled, ordered or suppressed differently  
> **Unresolved:** CompletelyUnlockAllTracks is settled (C1); what the firmware does with a track index instead of a mask is HARDWARE_ONLY. This stack models 3 motion tracks (M4-014) where the engine walks 8; the other five can never be locked here. The C3 sticky first-action gate cannot be evaluated without an ActionList (M8) and is logged when absent.

**C# entry:** `BehaviorManager.CheckReactions` (`BehaviorManager.cs:266`).

| step | address | what it does; gates, order and failure | floats / unsettled |
|---|---|---|---|
| Sticky outer gate | 0x005A355A..0x005A357A | `+0x4C |= !ActionList.IsEmpty`; if still zero or map empty return false. Only other writer is removal of lock `sdk`. It never clears. | No ActionList exists in C#, so this gate is currently `UNKNOWN` at runtime and cannot be guessed true. |
| Walk/predicate | 0x005A359A..0x005A35DE; 0x005A37AA..0x005A37CA | Ascending trigger-key order; strategy/behavior pairs in JSON order. Skip trigger with any disable lock. Current none skips interrupt predicate; same trigger uses CanInterruptSelf; other uses CanInterruptOther. Forced strategy clears force after setup. | no floats. |
| Trigger effects | 0x005A3610..0x005A37A6 | StopAllMotors; conditionally warn/unlock all tracks; switch/log. Do not break: later triggers may switch again and log multiple-switch warning. Return true if any switch succeeded. | failure to switch logs and continues. |
| Locks/messages | 0x005A27F6..0x005A2960; 0x005A3A52..0x005A3B94; 0x005A4D2A..0x005A4F3A | Disable in map order, call EnabledStateChanged(false) on first lock, optionally stop current; remove lock, set sticky bit for `sdk`, call EnabledStateChanged(true) when last lock removed. Missing trigger verifies and returns false. | External sender of `sdk` is BLOCKED_EXTERNAL. |
| Complete unlock | 0x00640F84..0x00641092 | Walk all eight track lock sets; for each nonempty set clear it and send a payload containing the **track index**, not a bit mask. | Firmware meaning of index payload is HARDWARE_ONLY. C# models only three tracks; five are absent. |

### M10-007

> **Title:** Unexpected-movement response: gate, history lookup, side and obstacle (d = 25), rewind SetNewPose, AddCollisionObstacle, broadcast always, reset  
> **Status/location:** IMPLEMENTATION_GAP — `cozmo-stack/src/Cozmo.Robot/UnexpectedMovement.cs`  
> **Evidence:** B12..B17 0x63E604..0x63E962; gap1 7a GetSizeByType CollisionObstacle {20, 54.2, 67.7} 0x50270E..0x502772; B18 kCreateUnexpectedMovementObstacles has no reader  
> **Effect:** the robot re-localises or maps obstacles differently  
> **Unresolved:** SetNewPose, its AbsoluteLocalizationUpdate and the collision obstacle are not performed: the M4 C9 F1..F3 fields are not in the rows and there is no RobotStateHistory::ComputeStateAt (M11). kCreateUnexpectedMovementObstacles has no reader.

**C# entry:** `UnexpectedMovementDetector.Update` -> `Respond` (`UnexpectedMovement.cs:208,255`).

| step | address | what it does; gates, order and failure | floats / unsettled |
|---|---|---|---|
| Gate/history | 0x0063E604..0x0063E672 | If reaction trigger 20 disabled: side Unknown, no history/rewind/obstacle. Else `ComputeStateAt(startTimestamp,true)`; origin mismatch or other failure logs, returns Unknown and skips response effects. | Exact history interpolation belongs to M11 and is absent; result is `UNKNOWN` until built. |
| Side/pose | 0x0063E68C..0x0063E840; size table 0x0050270E..0x00502772 | Mean wheel deltas vs epsilon determine front/back/left/right. Collision object size is `{20,54.2,67.7}`, so d=25 and offsets are front +47.1, back −80.9, left +52.1, right −52.1. | 20=`0x41A00000`, 54.2=`0x4258CCCD`, 67.7=`0x42876666`; epsilon 1e-5=`0x3727C5AC`; offset arithmetic must remain binary32. |
| Rewind/order | 0x0063E866..0x0063E916; SetNewPose 0x005126EC..0x0051271C | Copy historical pose, overwrite rotation with current rotation, SetNewPose (which updates pose/history and later AbsoluteLocalizationUpdate); even a nonzero SetNewPose result only logs and continues. Then parent obstacle to post-rewind robot pose, root-transform, AddCollisionObstacle. | Current C# only logs MISSING and performs neither effect. |
| Always tail | 0x0063E932..0x0063E962 | Broadcast report even on disabled/history-failure paths, then reset accumulators. `kCreateUnexpectedMovementObstacles` has no reader, so it is no gate. | no additional failure suppression. |

### M10-008

> **Title:** Resume after a reaction: SwitchToReactionTrigger parking, the track unlock rule, head/lift restore from SetDefaultHeadAndLiftState  
> **Status/location:** IMPLEMENTATION_GAP — `cozmo-stack/src/Cozmo.Robot/Behavior/BehaviorManager.cs`  
> **Evidence:** C7 0x5A3610..0x5A3682; C9 0x5A25E4..0x5A26DA; C10 flags 0x60F904; C11 0x5A2BB8..0x5A2C3A, 0x5A1BCC..0x5A1D26; R-ANIM pre-extraction part 2 item 4.1..4.8: ctor 0x5A0864..0x5A0A6C, SetDefaultHeadAndLiftState 0x5A1B40, TryToResume 0x5A2B40, MoveLiftToHeightAction 0x54899C  
> **Effect:** a behaviour resumes differently after a reaction  
> **Unresolved:** Settled (C1, C2): CompletelyUnlockAllTracks; the ctor FLT_MAX in +8/+0xC; the restore gate reads +8 only and requires the action list empty; the compound action's parameters. The restore is not reachable in this stack: there is no ActionList (M8) to answer "is the list empty", and SetDefaultHeadAndLiftState has no production caller (the engine's caller is the game message at 0x5A5042, which this stack replaces with a public API). MoveLiftToHeightAction::Init/CheckIfDone (+0x88/+0x8C/+0x90) are RECOVERABLE_GAP, not needed for this path.

**C# entry:** `BehaviorManager.CheckReactions` -> `SwitchToReactionTrigger`; resume path in manager update; `SetDefaultHeadAndLiftState` (`BehaviorManager.cs:346,377,621..`).

| step | address | what it does; gates, order and failure | floats / unsettled |
|---|---|---|---|
| Park prior behavior | 0x005A25E4..0x005A26DA | Null reaction behavior fails. If strategy `shouldResumeLast`, park existing resume target if present, else the currently running behavior whether reaction or not; otherwise park none. Then SwitchToBehaviorBase. | Generic default true; Shaken/Slope/Frustration false. |
| Trigger unlock | 0x005A3610..0x005A3682 | Stop motors, then unlock all tracks only under the recovered direct-drive/lock condition before switching. | Complete-unlock firmware index remains HARDWARE_ONLY as in M10-004. |
| Store defaults | ctor 0x005A0864..0x005A0A6C; setter 0x005A1B40..0x005A1D26; handler 0x005A5042 | Constructor sets head/lift fields to FLT_MAX. Enable stores both and immediately queues restore only when ActionList empty; disable sets both back to FLT_MAX. Only production caller is game message handler. | FLT_MAX=`0x7F7FFFFF`; current public API is not the production caller. |
| Resume/restore | 0x005A2B40..0x005A2C3A | Restore gate reads head field only, requires ActionList empty, queues parallel head-angle and lift-height actions, then resumes parked behavior according to manager state. | head tolerance native word at action construction (about 2°) must be copied bitwise, not decimal `0.0349066` (`0x3D0EFA39` is only the rounded spelling). |
| Missing C# ownership | current manager | No ActionList means emptiness cannot be evaluated; game dispatch is absent. MoveLift action internals are recoverable but not required to establish this call sequence. | Stop instead of assuming empty. |

### M10-013

> **Title:** Raw accel/gyro before the first RobotState: heap contents in the engine; 0 here (forced policy)  
> **Status/location:** IMPLEMENTATION_GAP — `cozmo-stack/src/Cozmo.Robot/OffTreads.cs`  
> **Evidence:** gap2 7b: no ctor store to +0x35C..+0x377; operator new(0x530) without memset (0x52EE8E..0x52EE9C)  
> **Effect:** the slope reaction could differ before the first state  
> **Unresolved:** The forced policy MD1 (0 before the first RobotState) is implemented; the record stays a forced choice and is COMPATIBILITY_POLICY at the next approval.

**C# entry:** `OffTreadsClassifier` construction initializes `RawAccel/RawGyro` to CLR zero; slope strategy reads `RawGyro` (`ReactionStrategies.cs:253`).

| step | address | what it does; gates, order and failure | floats / unsettled |
|---|---|---|---|
| Allocation/ctor | 0x0052EE8E..0x0052EE9C; Robot ctor scan | `operator new(0x530)` without memset; no ctor store covers `+0x35C..+0x377`. Adjacent memclears do not include raw accel/gyro. | Pre-first-state values are indeterminate heap bits: `UNKNOWN`, not zero and not recoverable as a stable value. |
| First state | 0x005129A4..0x005129BE | UpdateFullRobotState overwrites raw accel at +0x360..368 and gyro at +0x36C..374 before later consumers. | message binary32 bits copied verbatim. |
| Classification | policy, not extraction | Deterministic zero in C# is a forced compatibility policy. This record cannot become EXACT_SOURCE; reclassify to COMPATIBILITY_POLICY as unresolved says. | zero=`0x00000000`, explicitly not the engine's established pre-state value. |

## M15-freeplay

### M15-001

> **Title:** NeedsManager initialization, periodic update and need-bracket rules  
> **Status/location:** IMPLEMENTATION_GAP — `cozmo-stack/src/Cozmo.Robot/Behavior/Needs.cs`  
> **Evidence:** CozmoEngine::Init 0x004EC9E6..0x004ECA0A; NeedsManager::Init 0x00692574; NeedsManager::Update 0x00695C9C; NeedsState::UpdateCurNeedsBrackets 0x0069C12C; needs_config.json; NeedsManager::Init time threading 0x004EC9DA..0x004ECA0A, 0x0069257E, 0x006926CE, 0x006934B0, 0x0069358A..0x0069359E; NeedsManager::PossiblyWriteToDevice 0x00695DC4 (0x03A2C940 against a microsecond clock = 61 s; ApplyDecayForTimeSinceLastDeviceWrite divides by 1e6 at 0x0069532C)  
> **Effect:** need levels, notifications, persistence cadence and bracket observations change  
> **Unresolved:** NeedsManager::Init's StarRewardsConfig (needs_level_config.json), DesiredFaceDistortionComponent (needs_handlers_config.json) and LocalNotifications (local_notification_config.json) initialisation and the 16 message-handler registrations are not built; the SetNeedsPauseStates handler 0x00698918 (which consumes the per-need pause-start +0x1F0) and the NeedsState MessageEngineToGame wire (SendNeedsStateToGame raises the NeedsStateSent seam instead) are unbuilt - this stack has no game-message channel for them. The tick update, per-need decay with the fullness window and per-need pause skip, the bracket cache and the 61 s persistence throttle are built.

**C# entry:** `NeedsManager.InitInternal`, `Update`, `PossiblyWriteToDevice` and `NeedsState.UpdateCurNeedsBrackets` (`Needs.cs:626,712,824,279`).

| step | address | what it does; gates, order and failure | floats / unsettled |
|---|---|---|---|
| Init inputs/order | 0x004EC9DA..0x004ECA0A; 0x00692574..0x00693492 | Pass six config sections and BaseStationTimer seconds. Build Needs config, StarRewards, face distortion, notifications and 16 handlers; `InitInternal` resets, attempts fixed-file read, sends default state on failure, writes device state, emits app_start DAS, then generates notifications. | init time is binary32 BaseStationTimer; next decay=`now+period` at 0x0069358A..9E. Exact mapping of config offsets is named by shipped assets/current inventory. |
| Periodic update | 0x00695C9C..0x00695CFE | If globally paused return. Update local notifications. When `next<=now`, increment next by configured interval (do not set to now+interval), decay all needs with connected flag, send NeedsState action Decay, then tail `PossiblyWriteToDevice`. | float accumulator/order; configured decay values are source asset binary32 parses. |
| Per-need decay | 0x00695CFE..0x00695DC4 | For each need skip if pause flag set; skip inside active fullness deadline; otherwise use connected/unconnected table and update. | tables from `needs_config.json`; preserve parsed binary32. |
| Brackets | 0x0069C12C/0x0069CBCC/0x0069CD80 | Scan shipped per-need thresholds in defined order, cache bracket; getters refresh; invalid need warns/returns enum 4. | thresholds are config binary32; no hard-coded decimal substitute. |
| Persistence | 0x00695DC4; 0x00695304..0x00695374 | Compare microsecond clock elapsed against integer `0x03A2C940=61,000,000` µs = 61 s; on due stamp now and write with refresh=false. | Current throttle must be 61.0 s, not .061. |
| Missing production edges | 0x00698918 and Init registrations | Build the three subsystem initializations, all 16 handlers, per-need pause handler, and actual MessageEngineToGame NeedsState wire. Failure/log results follow native handler/send paths. | Any game-channel substitute is `UNKNOWN` until that channel is built. |

### M15-002

> **Title:** Chooser factory, scoring, strict-priority and selection chooser semantics  
> **Status/location:** IMPLEMENTATION_GAP — `cozmo-stack/src/Cozmo.Robot/Behavior/FreeplaySystem.cs`  
> **Evidence:** BSRunnableChooserFactory::CreateBSRunnableChooser 0x00609C88; ScoringBSRunnableChooser::ReloadFromConfig 0x00609F8C; ScoringBSRunnableChooser::GetDesiredActiveBehavior 0x0060A3D8; StrictPriorityBSRunnableChooser::GetDesiredActiveBehavior 0x0060B23E; SelectionBSRunnableChooser::OnSelected 0x0060AF20  
> **Effect:** the behavior selected for an activity changes  
> **Unresolved:** The SelectionBSRunnableChooser inbound MessageGameToEngine dispatch for ExecuteBehaviorByExecutableType (0x93) / ExecuteBehaviorByID (0x94) (0x0060A848..0x0060A93E, handler 0x0060AA58) is unbuilt, so RequestBehavior has no production caller. The chooser factory, scoring and strict-priority choosers are built.

**C# entry:** `ScoringChooser`, `StrictPriorityChooser`, `SelectionChooser` (`Activities.cs:223,295,331`); factory/configuration from Freeplay loading.

| step | address | what it does; gates, order and failure | floats / unsettled |
|---|---|---|---|
| Factory/config | 0x00609C88; 0x00609F8C | Dispatch chooser type, resolve behavior ids/config; invalid type/config follows logged/null failure path. Preserve vector order. | config scores are parsed floats. |
| Scoring | 0x0060A3D8..0x0060A52A | Evaluate each. Skip score<=0. Running behavior adds graph Y(running duration)+0.1 then floors at .01; non-running adds RandDbl tie noise; apply subclass hook; retain strictly highest; warn if >1 marked running. | .1f=`0x3DCCCCCD`, .01f=`0x3C23D70A`; random draw is context RNG. |
| Strict priority | 0x0060B23E..0x0060B25C | In vector order choose first running byte, else first IsRunnable true, else null. | no floats. |
| Selection request | ctor 0x0060A848..0x0060A93E; handler 0x0060AA58..0x0060AC30 | Subscribe to game tags 0x93/0x94. Resolve by executable type/id and store requested behavior plus `numRuns`; OnSelected consumes according to selection semantics. Invalid resolution follows native log/failure. | C# RequestBehavior has no production caller until this dispatch is built. |

### M15-003

> **Title:** Activity start/end predicates and cooldown lifecycle  
> **Status/location:** IMPLEMENTATION_GAP — `cozmo-stack/src/Cozmo.Robot/Behavior/Activities.cs`  
> **Evidence:** IActivityStrategy constructor 0x005B4EC8; IActivityStrategy::WantsToStart 0x005B529C; IActivityStrategy::RandomizeCooldown 0x005B5408; IActivityStrategy::WantsToEnd 0x005B5444; IActivityStrategy::SetCooldown 0x005B54E4  
> **Effect:** an activity becomes eligible, remains active or ends at different times  
> **Unresolved:** Audit 2026-09-29 (re-analysis/research/20260929-audit-complete.md): the settlement did not hold. InCooldown uses the same fixed TickSec as M15-005.

**C# entry:** `ActivityStrategy.WantsToStart`, `RandomizeCooldown`, `SetCooldown`, `WantsToEnd` (`Activities.cs:582,508,530,632`).

| step | address | what it does; gates, order and failure | floats / unsettled |
|---|---|---|---|
| Defaults | 0x005B4EC8..0x005B4F20 | cooldown/last-start/base=-1, randomness=0, startInCooldown=false, max duration=60, mood/needs thresholds=-1, feature gate=false. | -1=`0xBF800000`, 60=`0x42700000`, 0=`0x00000000`. |
| Start predicate | 0x005B529C..0x005B53F4 | Feature gate; cooldown including short-run rule; deadline; when passed randomize cooldown; mood/need/recent-event conditions; then subclass virtual. Stop at first failure with source result. | Duration comparison uses engine's last-tick float (M15-005), not fixed 1/30. |
| Randomize/set | 0x005B5408..0x005B543A; 0x005B54E4 | `current=base+RandDbl(randomness)`; SetCooldown writes base/current and randomness then follows native lifecycle. | Random draw/result precision follows cited body; retain float stores. |
| End predicate | 0x005B5444..0x005B54D2 | Compare configured min/max durations against now/start; forced max returns true; otherwise subclass virtual decides. | binary32 duration fields. |

### M15-005

> **Title:** Flat three-second cooldown after an activity ends within two ticks  
> **Status/location:** IMPLEMENTATION_GAP — `cozmo-stack/src/Cozmo.Robot/Behavior/Activities.cs`  
> **Evidence:** IActivityStrategy::WantsToStart 0x005B529C; activity times call site 0x005B26FE; IActivityStrategy::RandomizeCooldown 0x005B5408  
> **Effect:** an immediately ending activity is delayed before it can restart  
> **Unresolved:** Audit 2026-09-29 (re-analysis/research/20260929-audit-complete.md): the settlement did not hold. 'two ticks' is 2 x 1/30 s; the engine compares against the last tick's real duration, GetTimeSinceLastTickInSeconds (0x0084BCBC, computed each tick in UpdateTime 0x0084BC38..0x0084BC90), nominally 60 ms; the test is circular.

**C# entry:** cooldown branch inside `ActivityStrategy.WantsToStart` / `InCooldown` (`Activities.cs:557..592`).

| step | address | what it does; gates, order and failure | floats / unsettled |
|---|---|---|---|
| Supply activity times | 0x005B26FE | Caller passes last start/end times to WantsToStart. Positive duration means the prior activity actually ran. | binary32 seconds. |
| Tick duration | 0x0084BC38..0x0084BC90; getter 0x0084BCBC | UpdateTime stores actual `dt` for the current engine tick; getter returns it. Nominal loop is about 60 ms, but the value is not a constant. | binary32 dt bits vary at runtime: `UNKNOWN` per tick. |
| Short-run gate | 0x005B52EA..0x005B531C | If duration>0 and duration<=`2*lastTickDt`, use flat cooldown 3.0 s; otherwise use configured/randomized cooldown. Then test `now >= lastEnd+cooldown`. | 3.0f=`0x40400000`; multiplication/comparison binary32. Current fixed `2*(1/30)` is wrong. |

### M15-006

> **Title:** Freeplay activity selection and selected/deselected lifecycle  
> **Status/location:** IMPLEMENTATION_GAP — `cozmo-stack/src/Cozmo.Robot/Behavior/FreeplaySystem.cs`  
> **Evidence:** ActivityFreeplay::PickNewActivityForSpark 0x005ADC44; ActivityFreeplay::GetDesiredActiveBehaviorInternal 0x005AE29C; IActivity::OnSelected 0x005B312C; IActivity::OnDeselected 0x005B33B8; IActivity::GetDesiredActiveBehavior 0x005B387C  
> **Effect:** the active activity and behavior, animation state and pending reward handling change  
> **Unresolved:** The inbound ActivateSpark message (sets BehaviorManager+0x60/+0x65, 0x005A3C92..0x005A3C9A) and the requested-activity message (+0x90) are unbuilt, so SetRequestedSpark/RequestNewActivity have no production caller. The selection/re-selection, interlude, null-pick, OnSelected/OnDeselected, reward-pending and the reselect logic are built.

**C# entry:** `FreeplaySystem.Update`/selection flow, `PickNewActivity` (`FreeplaySystem.cs:320`), and `Activity.OnSelected/OnDeselected` (`Activities.cs:755`).

| step | address | what it does; gates, order and failure | floats / unsettled |
|---|---|---|---|
| Decide new activity | 0x005AE29C..0x005AE46E | Priority: changed debug force; requested byte; no current; invalid spark transition; current WantsToEnd with no pending reward. Same-spark reselection uses BehaviorManager+0x65 set only by ActivateSpark UnlockId 0x55. | no floats except time passed to strategies. |
| Pick | 0x005ADC44 | Iterate spark activity vector in config order; current handling depends on flag; ask WantsToStart/WantsToEnd as recovered; first true wins. Deselect old then select new and log. Null remains null. | failure is no selected activity/behavior, not fallback invention. |
| Choose behavior | 0x005B387C; 0x005AE672..0x005AE70A | Ask activity chooser, optionally insert interlude. If chooser returns null, do not immediately repick; if returned behavior is running, reward pending, or activity now WantsToEnd, repick per native branch. | order is behavior-changing. |
| Lifecycle | 0x005B312C; 0x005B33B8 | OnSelected performs chooser/idle/analytics setup in source order. OnDeselected stamps end, chooser hook, removes animations/analyzer/locks, clears state, communicates pending reward, logs, then subclass hook. | no float constants beyond timestamp. |
| Missing callers | 0x005A3C92..0x005A3C9A and requested message path | Build inbound ActivateSpark and requested-activity game messages; otherwise setters have no production owner. | Message failure/absence behavior follows game dispatch; current public calls are not substitutes. |

### M15-016

> **Title:** NeedsManager pause and disconnect transitions  
> **Status/location:** IMPLEMENTATION_GAP — `cozmo-stack/src/Cozmo.Robot/Behavior/Needs.cs`  
> **Evidence:** NeedsManager::SetPaused 0x00695E04; NeedsManager::OnRobotDisconnected 0x00695908; NeedsManager::SetPaused unpause shift 0x00695F02..0x00695F6A (+0x1E4/+0x1F0/+0x214 always; +0x208/+0x1FC when +0x208 != 0); NeedsManager::DetectBracketChangeForDas 0x00695958 (+0x214 read 0x006959D6, elapsed 0x006959FA, update 0x00695BA2 only when force == 0); NeedsManager::HandleMessage<SetNeedsPauseStates> 0x00698918 (+0x1F0 write 0x00698A48, read 0x00698A18; unbuilt game message); NeedsManager::InitAfterConnection 0x00694384..0x0069439C (+4 robot pointer, +0x3D0 = 1, +0x1D4 = 1); CozmoEngine::HandleMessage<ConnectToRobot> 0x004ED018..0x004ED11C (InitAfterConnection 0x004ED10E unconditionally after AddRobot, DASPauseUploadingToServer 0x004ED114); NeedsManager::ApplyDecayForTimeSinceLastDeviceWrite caller HandleMessage<SetGameBeingPaused> 0x006990E8; +0x30 increment 0x006990EC; +0x20 write 0x00698FA6 (unbuilt game message)  
> **Effect:** needs writes, schedules, notifications and bracket telemetry change across pause or disconnect  
> **Unresolved:** The SetPaused callers (SetGameBeingPaused tag 85, SetNeedsPauseState tag 201, RegisterOnboardingComplete 200, EnterSdkMode/ExitSdkMode 241/242) are unbuilt - this stack has no game-message channel for them. The unpause shift now covers +0x1E4/+0x1F0/+0x208/+0x1FC/+0x214; the +0x1F0 consumer (HandleMessage<SetNeedsPauseStates>) and the +0x214 DAS-elapsed consumer (the app-facing DAS wire) are unbuilt. The ConnectToRobot seam that calls NeedsManager::InitAfterConnection (0x004ED10E) is wired from CozmoEngine::ConnectToRobot; the same handler's DASPauseUploadingToServer(1) (0x004ED114) is not built, and J13 states no behaviour for it (MISSING). The SetPaused/OnRobotDisconnected bodies and the always-fired removal wiring are built. HandleMessage<SetGameBeingPaused> 0x00698F44 also calls ApplyDecayForTimeSinceLastDeviceWrite(robot != 0) (0x006990DE..0x006990E8); that caller is unbuilt. The +0x1D4 field that InitAfterConnection also sets (0x00694398) is not modelled: its only consumer is LocalNotifications::ShouldBeRegistered 0x0068D00C, an unbuilt notification path.

**C# entry:** `NeedsManager.SetPaused`, `OnRobotDisconnected`, `InitAfterConnection` (`Needs.cs:951,900,889`).

| step | address | what it does; gates, order and failure | floats / unsettled |
|---|---|---|---|
| SetPaused redundant/pause | 0x00695E04..0x00695ECA | Same state logs and returns with no send/write/notification. Pausing sets flag/time, stores remaining-to-next-update, sends NeedsState NoAction, writes device with refresh=true. | all schedule fields are binary32 BaseStationTimer seconds. |
| Unpause | 0x00695F02..0x00695F78 | Clear flag; elapsed=now-pauseStart; nextUpdate=now+storedRemaining; for each need add elapsed to +1E4,+1F0,+214 always and +208/+1FC only when +208 nonzero; then LocalNotifications.SetPaused(false) and send pause state. No NeedsState send/device write. | preserve binary32 add order. |
| Disconnect | 0x00695908..0x0069594C | Stamp disconnect time, clear app-open counter, if not paused force device write, clear robot ptr, snapshot needs, DetectBracketChangeForDas(true), send levels DAS "disconnect". No NeedsState send. | failures only log through device/DAS paths. |
| Connection | 0x004ED018..0x004ED11C; 0x00694384..0x0069439C | After AddRobot attempt, unconditionally InitAfterConnection: set robot pointer, read-pending=1, notification flag +1D4=1; then call DASPauseUploadingToServer(1). | Meaning/effect of DASPauseUploadingToServer is `UNKNOWN`; +1D4 gates notification conditions 1/2 at 0x0068D00C. |
| Game callers | 0x00698918; 0x00698F44..0x006990F2; tags named above | Implement callers for global pause/SDK/onboarding/per-need pause. SetGameBeingPaused also calls elapsed decay with `robot!=0`, increments +0x30 and writes +0x20. | Absent game-message channel is the production-path gap; do not replace with public API calls. |

## Extraction summary

- **Direct corrections with settled source:** M5-005/017 accept a zero entropy word after one read; M5-022 teardown order; M5-027 clip-end tick; M5-032 reciprocal-then-multiply; M15-003/005 use actual last-tick duration; M15-001 uses a 61-second device-write throttle.
- **Missing production owners:** M5-027/030/035 depend on the unbuilt native live-idle route; M10-004/008 depend on ActionList and game dispatch; M10-007 depends on RobotStateHistory/SetNewPose/BlockWorld; M15-001/002/006/016 require game-message dispatch and notification/wire owners.
- **Remain explicitly unsettled:** M5-013 keyframe `+0x28` writers; M5-014 uninitialised head-window values; M5-018 RobotAudioAnimation states/latency; M5-020 Mono tick-count seed; M5-021/M5-032 bionic libm last bits and runtime FPSCR; M10-003 named strategy bodies; M10-013 pre-state heap contents; M10-004 firmware interpretation of track-index unlock; M15-016 DASPauseUploadingToServer behavior.

