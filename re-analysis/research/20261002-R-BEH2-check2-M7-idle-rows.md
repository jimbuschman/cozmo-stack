# R-BEH2 check 2 of 3: the M7 idle/face rows of the 2026-09-29 audit, checked in libcozmoEngine.so

Date 2026-10-02. Read-only. Scope: Appendix A and B rows M7-005, 006, 007, 008, 009, 010, 013, 016, 017 of
`re-analysis/research/20260929-audit-M7-M8.md`, against `resources/lib/armeabi-v7a/libcozmoEngine.so` (Thumb-2 disassembly
with capstone; `re-analysis/decomp/` used only to navigate), compared with the C# the rebuild will change:
`Behavior/IdleBehavior.cs`, `IdleParameters.cs`, `Mood.cs`, and the streamer port in `Animation/AnimationScheduler.cs`
and `Animation/TrackLayers.cs` (plus `ProceduralFace.cs` for LookAt), all under `cozmo-stack/src/Cozmo.Robot/`. Every address
below was opened in the binary in this pass. Floats are given as the engine's bit patterns (section C). Current manifest
state, quoted from `re-analysis/fidelity_manifest.json`: M7-005, 006, 007, 008, 009, 010, 013, 016, 017 and M5-030 are all
IMPLEMENTATION_GAP (e.g. M7-017 "The exact live-animation wire lifecycle used by idle behavior"; M5-030 "Live idle
(UpdateLiveAnimation): gates, body/lift/head wiggles ...", unresolved "CarryingComponent (+0x284, M12) is not on this robot, so
the lift's carrying gate reads clear").

Commands run: a Thumb disassembler over VA ranges, a symbol lister, caller scanners (movw/literal scans for 0x198, bl-to-PLT
scans), `python re-analysis/tools/fidelity.py --check` (passes: 418 records, 231 IMPLEMENTATION_GAP), a numpy float32
emulation for the LookAt comparison. No repo writes except this file (`git status` shows only
`re-analysis/jobs/status/R-BEH2.md` modified, which is another window's).

---------------------------------------------------------------------------------------------------------------------

## 0. Verdict per row

| row | verdict | summary |
| --- | --- | --- |
| M7-005 | HOLDS | all five sub-claims verified (1.1). 0x5861c4 is the scanline toggle `rsb.w r1,r1,#1; strb r1,[r0]`. |
| M7-006 | HOLDS, two things missed | the omitted clamp is real (0x583b20..0x583bf8, called at 0x584168) and it bites the turn eye shift (x up to 21 against a +-17 clamp), not only "a six pixel dart" (1.2). "LookAt confirmed" is not true bit for bit: the C# LookAt uses a different float association from the engine (1.6, WRONG-F1). |
| M7-007 | HOLDS | verified 0x58cd9e, 0x58ce22, 0x58d220, 0x58eade..0x58eaf0, 0x58e688, 0x58e71a..0x58e724. |
| M7-008 | **WRONG in two bullets**, otherwise HOLDS | the bullets "the +0x194 flag is clear" and "picking or placing is in progress" do NOT decrement: they return 0 at once (0x57d606..0x57d60c, 0x57d61c..0x57d624, both to 0x57d6be). A third no-decrement return, `+0x44 < GetParam<int>(2)` (0x57d612..0x57d61a), is not listed either. Only the MovementComponent flag, AreAnyTracksLocked, the not-yet-due countdown and (lift only) carrying decrement (1.3). M5's own inventory (L2 in `re-analysis/inventory/M5-animation.md`: "a failed gate returns 0 with no decrement") and the C# port already have this right. |
| M7-009 | HOLDS | 0x42652ee1 float multiply and `vcvt.s32.f32` (0x57d838..0x57d84e), 35/8/6, parameter indices, body -> lift -> head, decrement unless due, all four omitted gates. |
| M7-010 | HOLDS, one claim misleading | persistence 0x64f3c8/0x64f41a/0x64f472, removal 0x57d8d2, draw order and call arguments verified. But the `64.0 / 32.0` arguments are discarded by `GenerateEyeShift` (0x58cfc4): the effective LookAt limits are 17 and 12 (1.4). "Removed only by RemoveEyeShift on a straight shuffle" holds for this tag; other RemoveEyeShift callers exist (MovementComponent::Update 0x63e37a) but for other tags. |
| M7-013 | HOLDS, omissions the audit did not list | EvaluateY rule at 0x804c0c, no decay in TriggerEmotionEvent, float widths all verified. Missing from the row: MoodManager::Update floors dt at 1e-4f (0x67b5e6..0x67b612), and Emotion::Update appends a history sample (0x679608) (1.7). |
| M7-016 | HOLDS | per-frame layering and the +33 clock verified. The "+0x38 == 0 and +0x88 > 0" gate is incomplete: the engine also requires (idle == live) or (idle == null and now - +0x88 > +0x1c0) (0x57cfa4..0x57cfbe). |
| M7-017 | HOLDS, one sub-claim UNVERIFIED | streamer internals, seam, clock, lifecycle verified. UNVERIFIED: that FistBump/Bouncer/PeekABoo/ReactToOnCharger push 0x23f over a live idle (the C# `Behavior/` folder has no PushIdleAnimation caller except a doc comment, `ChargerBehaviors.cs:98`). NEW: no code that pushes ProceduralLive (0x198) was found in the binary at all (1.8). |

WRONG rows (corrected facts): only M7-008 bullets 1 and 2 (with the missing third gate). Corrected fact in 1.3.

---------------------------------------------------------------------------------------------------------------------

## 1. Row-by-row evidence

### 1.1 M7-005 (HOLDS)

1. `IdleBehavior` is built in the conformance tool only: `cozmo-stack/src/Cozmo.Conformance/Behavior.cs:54` (src); tests build it too.
2. Per-frame application: `ITrackLayerManager<ProceduralFaceKeyFrame>::ApplyLayersToFrame` 0x58e644 calls the std::function (0x58e710) with
   (track, [layer+0x28], [layer+0x2c]); then `ldr r2,[r8,#0x2c]; add r1,r2,#0x21; str r1,[r8,#0x2c]` (0x58e71a..0x58e724): layer clock +33 per
   application. The caller is `TrackLayerComponent::ApplyFaceLayersToAnim` 0x64f154 (GetFaceHelper(anim track, replace = 1) at 0x64f1ea, then
   ApplyLayersToFrame at 0x64f222): once per streamed frame. C# `IdleBehavior.cs:662` `_robot.Face.SetParameters(pose)` -> `CozmoAnimations.cs:555-562` -> `Display.Show`.
3. `StreamLive` pushes `(ProceduralLive, "StreamLive")` and never pops it (`AnimationScheduler.cs:885`), and the port's own `KeepFaceAlive` runs when
   `_idleAnim == _live` (`:904-917`): two generators.
4. Scanline toggle: 0x5861a0..0x5861c8 (frame action 1: mean of the two eye centres `[+4]`,`[+0x50]` times `vmov.f32 s4,#0.5`; `rsb.w r1,r1,#1; strb r1,[r0]`
   flips the static byte). M5 `TrackLayers.cs` does it (`_scanLines.Drawer = 1 - ...`); `IdleBehavior.cs:557` admits omitting it.
5. Blink-spacing fallback 0x58d4dc..0x58d550: `r1=(int)p0` (min), `r3=(int)p1` (max); `cmp r3,r1; bgt 0x58d548`; else a warning, then `movw r3,#0x7530` (30000) and
   `movw r1,#0x1d4c` (7500) at 0x58d540/0x58d544, then `RandIntInRange(r1,r3)` at 0x58d54c stored to `[this+0x14]`. M5's `KeepFaceAlive` has it.
Also verified: blink table 0xC5AAD8 (7 x 16 bytes {f32 heightMul, f32 widthMul, u32 dur, u32 action}); the drawer uses `[entry+4]` x origScaleX (param 2) and `[entry+0]`
x origScaleY (param 3) (0x5860c4..0x5860fc); GenerateBlink 0x58d2ac accumulates `r8 += dur` before each AddKeyFrameToBackHelper (0x58d2fa..0x58d312); Combine 0x5846a8
and CombineEyeParams 0x584648: add {0,1,4,14,17} (table 0xC5A972), multiply {2,3} (0xC5A977), face +0x9c add, +0xa0/+0xa4 multiply, +0xa8/+0xac add.

### 1.2 M7-006 (HOLDS) and what was missed

`ProceduralFace::SetFacePosition` 0x583b20..0x583bfe: `GetEyeBoundingBox(face,&xmin,&xmax,&ymin,&ymax)` (0x583b40), then
`[face+0xa8] = min(max(x, -xmin), 128.0f - xmax)` and `[face+0xac] = min(max(y, -ymin), 64.0f - ymax)` (vstr at 0x583ba4 and 0x583bf8; constants
0x43000000 at 0x583b4c, 0x42800000 at 0x583b8c). `LookAt` 0x584158 calls it first (0x584168). Default-face box (formula 0x584568..0x58463a: `30.0f*0.5`,
`40.0f*0.5`, `+32.0f`, `+96.0f`): xmin = 32 + (0 - 15) = 17, xmax = 96 + 15 = 111, ymin = 32 - 20 = 12, ymax = 52, so the clamp on the default face the
layers are built on (0x58cfc8, 0x58d18e) is x in [-17, 17], y in [-12, 12]. The dart (|x|,|y| <= 6) never reaches it. The turn eye shift (x in [-21, 21],
y in [-10, 10]) does: |x| of 18..21 is clipped to 17. The audit's "a six pixel dart never reaches" is right for the dart only; the IdleBehavior doc
comment (`:540-542, 755-757`) generalises it to the turn shift, which is wrong.

### 1.3 M7-008 (WRONG in two bullets)

`UpdateLiveAnimation` 0x57d5f8, exact control flow:
- 0x57d606 `ldrb [this+0x194]; cmp #0; beq 0x57d6be`; 0x57d6be is the return (`movs r2,#0; mov r0,r2; ... pop`). No decrement.
- 0x57d612..0x57d61a `r6=[this+0x44]; r0=GetParam<int>(2); cmp r6,r0; blo 0x57d6be`. Unsigned, no decrement.
- 0x57d61c..0x57d624 `ldr r0,[robot+0x280]; ldrb r0,[r0,#4]; cmp #0; bne 0x57d6be`. No decrement.
- Only after those, per track (body 0x57d626.., lift 0x57d654.., head 0x57d68e..): MovementComponent byte `[+9/+0xb/+0xa] != 0` -> decrement; else
  `AreAnyTracksLocked(4/2/1)` -> decrement; else `[this+0x198]+[this+0x1a4] > 0` (signed; lift and head use +0x19c+0x1a8 and +0x1a0+0x1ac) -> decrement; (lift only)
  `[[robot+0x284]+8]+1 != 0` (carrying) -> decrement; otherwise generate. Decrements: 0x57d650, 0x57d68a, 0x57d6ba (`subs r0,#0x3c`).

MovementComponent flag identities (new): `MovementComponent::Update` 0x63e300 stores from RobotState+0x4c: `+9 = status & 1` (IS_MOVING), `+0xa = !(status>>9 & 1)`
(not HEAD_IN_POS 0x200), `+0xb = !(status>>8 & 1)` (not LIFT_IN_POS 0x100), `+0xc = bit 15` (0x63e30a..0x63e340; Unity `RobotStatusFlag.cs`: IS_MOVING = 1,
LIFT_IN_POS = 0x100, HEAD_IN_POS = 0x200). `DockingComponent+4 = status bit 2` (`ubfx r1,r6,#2,#1; strb r1,[r0,#4]` at 0x512a9c/0x512aa0 in
`Robot::UpdateFullRobotState`). The C# port wires exactly these (`CozmoAnimations.cs:178-189`) except Carrying.

Other bullets of the row verified: `+0x44` is written only at 0x57d000 (= 0) and 0x57d446 (+= 0x3c), plus the ctor memclr; `CozmoInstanceRunner::Run` 0x65b3a8:
`movw r0,#0x8700; movt r0,#0x393` = 0x03938700 = 60 000 000 ns (0x65b3d2/0x65b3d8); decrement sites 0x57d650/0x57d68a/0x57d6ba/0x58d388; dart before blink (0x58d3ac before
0x58d48c); the engine RNG is the context RNG `[this+0xa4]` (ctor 0x579fee..0x579ff0). Tests: `KeepAliveTests.cs:44-61` asserts `EngineTickMs == 60` and
`AtMs % 60 == 0` (an artifact of the synthesised ticks, circular); `:99-129` asserts the unsourced own-tracks gate; `:345-349` asserts the turn shift is gone two ticks later,
the opposite of 0x64f472; `:308-310` asserts the C# constants 21/10/33 against literals (right numbers, but no engine behaviour is tested).

### 1.4 M7-010 (HOLDS, one misleading claim)

Verified: draw order at 0x57d6cc..0x57d780: `+0x198=RandInt(p5,p6)`; `speed=RandInt(-p7,p7)`; `RandDblInRange(0.0,1.0)` (doubles, 0x57d72c; compared with `(double)p8` at 0x57d748,
`ble 0x57d8c6` = straight when r <= p8); turn: `x=RandInt(0,0x15)` (0x57d75a), `y=RandInt(-10,10)` (0x57d766/0x57d76a); BodyMotionKeyFrame(sxth speed, radius 0 for a turn or
0x7fff straight, `+0x198`) (0x57d8ea); then `+0x1a4=RandInt(p3,p4)` (0x57d978). The eye-shift call (0x57d7fc): name "LiveIdleTurn" (12 bytes at 0x57dafc), x = (float)(int)(+-1.0 * (float)x)
with the sign from bit 15 of the speed (0x57d790..0x57d7b6), y = (float)y, then the stack arguments `33, 64.0, 32.0, 1.1, 0.85, 0.1` (0x57d7d6..0x57d7f8). Persistence: `AddOrUpdateEyeShift` 0x64f3c8:
when `[tag] != 0` -> `AddToPersistentLayer` (0x64f41a); else a track with a neutral keyframe at trigger 0 (only when dur != 0, 0x64f434) then the shift keyframe, `AddPersistentLayer`
(0x64f472), tag stored. `RemoveEyeShift` is called at 0x57d8d2 in the straight branch and at 0x63e37a in `MovementComponent::Update` (other tags). The KeepFaceAlive layer gate at
0x58d3ac..0x58d3c4 reads `[this+0xc]` (map size): proceed when 0, or 1 and `HasLayerWithTag([this+0x1c])`.

The misleading part: `FaceLayerManager::GenerateEyeShift(x,y,xMax,yMax,up,down,outer,dur,&kf)` 0x58cfc4 stores the caller's xMax into the bounding-box output slot (0x58cfce, `str r3,[sp,#0x18c]`,
then `GetEyeBoundingBox` writes it) and its yMax stack slot is also the box output (`add r1,sp,#0x1a0; str r1,[sp]` at 0x58cfda): both are overwritten. It builds a default face, takes
`xm = max(xmin, 128.0f - xmax)` (0x58cfe8..0x58d016) and `ym = max(ymin, 64.0f - ymax)` (0x58d01a..0x58d030) and calls `LookAt(x, y, xm, ym, up, down, outer)`. So the effective limits are 17
and 12, not 64 and 32. The M5 port's `GenerateEyeShift` (K2) does this; `IdleBehavior.TurnShiftXRange/YRange = 64/32` (`:489-490`) is wrong.

### 1.5 M7-016, M7-017 (HOLD)

Section A has the full Update order. M7-016's gate: `+0x88 > 0` at 0x57cf6a..0x57cf76; `+0x38 == 0` at 0x57cf8a (`cbnz r6`); the idle/timeout gate at 0x57cfa4..0x57cfbe; the call at 0x57cff2.
M7-017: ProceduralLive branch 0x57d064..0x57d080; clock zero 0x57d000; `+0x44 += 60` 0x57d442..0x57d446; re-init 0x57d40c..0x57d426. `CozmoAnimations.cs:322-335` doc comment ("returns false when
a running clip owns the keyframe's track") does not match the code: `AnimationScheduler.StreamLive` refuses whenever `_streaming is not null`, whatever tracks it owns.

### 1.6 Float-association defect in the live LookAt (new, WRONG-F1)

Engine `ProceduralFace::LookAt` 0x584158..0x58428a computes, in float32 and in this order: `yf = min(((y + ymax) / (ymax * -2.0f)) + 1.0f, 1.0f)` (0x58416c..0x5841b0: `vadd s2=y+ymax;
vmul s0=ymax*-2; vdiv s0=s2/s0; vadd s0=s0+1.0`); `xf = min(|x| / xmax, 1.0f)`; `sY = (up - down) * yf + down`; `a = xf * outer + 1.0f`; for x < 0: `left = a * sY`, `right = sY * (2.0f - a)`;
for x >= 0: `left = (2.0f - a) * sY`, `right = sY * a` (0x5841d4..0x584236); then Clip. For y > 0 the eye centres get `+-2 * min(y/ymax, 1)` (0x584244..0x58427e). The C# (`ProceduralFace.cs:232-235`;
the same shape in `IdleBehavior.Gaze`) computes `(yMax - y) / (2f * yMax)` and `(1f - outer * xf)`: algebraically equal, not bit equal. A float32 emulation of both over the real argument domains,
counting draws whose (left, right) pair differs: dart (x,y in -6..6, xMax = yMax = 5, outer 0.1f, up 1.1f, down 0.85f) 68 of 169; turn (x in -17..17, y in -10..10, xMax 17, yMax 12) 362 of 735.
CHECKLIST 4 (operation order) failure in `ProceduralFacePose.LookAt`, the M5 live path used by the KeepFaceAlive darts and the LiveIdleTurn shift.

### 1.7 M7-013 (HOLDS) and what was missed

- EvaluateY 0x804bd0..0x804c44 (section B).
- `MoodManager::TriggerEmotionEvent` 0x67b85c: FindEvent (0x67b874); a null event returns failure (0x67b87e); a channelled info log; `UpdateLatestEventTimeAndGetTimeElapsedInSeconds` (0x67b8d8);
  `EmotionEvent::CalculateRepetitionPenalty` (0x67b8e0); then per affector `Emotion::Add(this + type*0x20, penalty * value)` (`vmul.f32 s0,s16,s0` at 0x67b8f6, `lsl #5` at 0x67b8fa,
  call 0x67b902). No Emotion::Update and no decay, so `Mood.cs:288` (`Advance(nowSec)` first) is wrong. The remainder (0x67b90c..) only writes a debug string of the nine values.
- Decay happens only in `MoodManager::Update(float)` 0x67b5d4 (section B).
- `Mood.cs:377-379` (`if (dt <= 0) return;`, and a first `dt = nowSec - 0`) has no engine counterpart (missed by the audit): the engine's dt is `1e-4f` when the stored last time `[+0x130]` is zero
  and never less than `1e-4f` (0x67b5e6..0x67b612, literal 0x38d1b717 at 0x67b5ea).
- Emotion::Update ends with `bl 0x67945c`: an append to a history ring (capacity 0x80, 8-byte samples {value, dt}) (0x679600..0x679608; ctor 0x679406..0x679420); `GetHistoryValueTicksAgo(n)`
  0x6794f8 reads it. `MoodState` has no history. M8-003's "trackDelta = GetHistoryValueTicksAgo(60)" (0x67c9ee) depends on it.

### 1.8 M7-017: who pushes ProceduralLive? (new, not found)

The engine takes the live path only when the idle stack top is 0x198 (0x57d068 `cmp.w r7,#0x198`). Scans of the whole Thumb .text for `movw/mov/mov.w ..., #0x198` and for a literal-pool word 0x198 find no
push of it (the hits are object sizes, `BehaviorContainer::CreateBehavior`, the streamer ctor naming the live animation with `EnumToString(0x198)` at 0x579ff4, and `AnimationTriggerFromString` 0x7627fe).
The string "ProceduralLive" occurs once in the engine (the enum table, 0xc18792) and nowhere in `re-analysis/obb`; the Unity C# only declares the enum (`AnimationTrigger.cs`). The four engine callers of
`AnimationStreamer::PushIdleAnimation` are `SevereNeedsComponent::SetSevereNeedExpression` 0x573152 (trigger from a std::map), `ActivityFeeding::SetIdleForCurrentStage` 0x5acc70,
`IActivity::SmartPushIdleAnimation` 0x5b3304 and `IBehavior::SmartPushIdleAnimation` 0x5be464 (trigger passed in), plus the game-to-engine `PushIdleAnimation` message. Conclusion: in this binary the
production source of the ProceduralLive push is not established (a RECOVERABLE_GAP candidate; the scans do not cover every data-driven path). With the default stack top (Count 0x23F, ctor 0x57a064..)
the engine never runs UpdateLiveAnimation; only the face keep-alive runs (idle null and 0.5 s after the last stream). The rebuild must not invent a pusher.

---------------------------------------------------------------------------------------------------------------------

## A. Deliverable (a): AnimationStreamer::Update, 0x0057ce5c..0x0057d5d0, the live-idle sequence

Fields: +0x34 current idle animation, +0x38 streaming animation, +0x40 neutral-face animation, +4 base "defaults set" byte, +0x44 u32 idle ms accumulator, +0x48..0x4c idle stack (16-byte entries: trigger u32 plus
std::string), +0x60 TrackLayerComponent, +0x64 idle-initialised byte, +0x68/+0x6c loop count and counter, +0x70 tag, +0x71 startSent, +0x72 endSent, +0x73 "aborted to nothing" byte, +0x88 f32 last-stream time
(ctor `mvn r1,#0x800000` = 0xFF7FFFFF = -FLT_MAX, 0x579fde), +0x94 send-buffer count, +0xa4 context RNG, +0xa8 the live Animation (ctor 0x57a01e..0x57a060, `SetIsLive(true)`), +0x194 live flag (byte; ctor 0;
written 1 only at 0x57d074; never cleared), +0x198/+0x19c/+0x1a0 body/lift/head countdowns, +0x1a4/+0x1a8/+0x1ac spacings (ctor memclr of 0x18 bytes, 0x57a030..0x57a03c), +0x1c0 f32 keep-alive timeout
(0.5f: ctor 0x57a040/0x57a050 and `ResetKeepFaceAliveLastStreamTimeout` 0x57e010; its sole external caller is `ReactionTriggerStrategyHiccup` 0x610778), +0x1c4 u8 turn eye-shift tag.

Caller: `Robot::Update` 0x513bc8: `ActionList::Update` (0x5140bc), then `if (robot[+0x29] && robot[+0x2a])` -> `AnimationStreamer::Update(robot+0x60)` (0x51410c..0x51411e), then `NVStorageComponent::Update`
(0x51416a). +0x29 = SyncTimeAck received (cleared 0x515228, set 0x5366ac); +0x2a = ready to stream (ctor 0; a RobotEventHandler-bound handler sets it, 0x52c3a6). The C# `CozmoEngine.cs:1180-1208` already
reproduces this order and gate (`Engine.AnimationStreamerUpdate = Animations.EngineUpdate`, `CozmoRobot.cs:270`), once per 60 ms tick (0x65b3d2).

Sequence inside Update (S0..S6):

- **S0 (0x57ce66..0x57cf6a), non-behavioural:** debug text and `CozmoContext::SetSdkStatus(2, name)` for +0x38, else +0x34, else a 4-char string (0x57d0e8..0x57d120). Falls into S1.
- **S1 keep-alive block (0x57cf6a..0x57cff4):**
  1. gate A: `[+0x88] > 0.0f` (`vcmpe s0,#0; ble 0x57cff6`). It is -FLT_MAX at construction, so nothing happens until something has streamed.
  2. `TrackLayerComponent::Update()` (0x57cf7a): reads `context+0x34 -> +0x3d4 DesiredFaceDistortionComponent::GetCurrentDesiredDistortion()`; if above 1e-5f (0x3727c5ac) it tail-calls AddGlitch (0x64ede8..0x64ee14).
  3. gate B: `[+0x38] == 0` (0x57cf8a `cbnz r6`).
  4. gate C (0x57cf8c..0x57cfbe, float32): `diff = now - [+0x88]`; run only if `[+0x34] == &[+0xa8]` (the idle is the live animation) OR (`[+0x34] == 0` AND `diff > [+0x1c0]`).
  5. if `[+0x73]`: `SetStreamingAnimation([+0x40], 1, 1, 0)`, `[+0x73] = 0` (0x57cfc0..0x57cfd6).
  6. if `[+4] == 0`: `[+4] = 1; vtable[0](this)` = `SetDefaultParams` (vtable 0x1023894; the relocation at 0x102389c is SetDefaultParams, slot (0x102389c - 0x1023894 - 8)/4 = 0) (0x57cfda..0x57cfea).
  7. `TrackLayerComponent::KeepFaceAlive(+0x60, params map at +0x10)` (0x57cff2) -> `FaceLayerManager::KeepFaceAlive` 0x58d374.
- **S2 streaming branch (0x57cff6..0x57d036):** if `[+0x38] != 0`: `[+0x44] = 0` (0x57d000); if `[+0x72]` and no frames left and `[+0x94] == 0`: loop bookkeeping (0x57d24e: `+0x6c += 1`; if `(+0x68 - 1) < +0x6c`
  then `[+0x38] = 0` and jump straight to S3b (0x57d04e), else `InitStream([+0x38], [+0x70])` and return 0); else `UpdateStream(robot, [+0x38], true)`, `[+0x64] = 0`, `[+0x88] = now`, return its result.
- **S3 no animation streaming (0x57d03a):** read the stack (begin `[+0x48]`, end `[+0x4c]`). Empty, or top == 0x23f (Count): **S3b** `HaveLayersToSend()` -> `StreamLayers(robot)`, return its result (+0x88 NOT
  updated); else if `[+0x94] == 0` return 0; else warn, `UpdateAmountToSend`, `SendBufferedMessages`, and if startSent && count == 0 && !endSent -> `SendEndOfAnimation`.
- **S4 (0x57d064) top != Count:** `sb = [+0x34]`. If top == 0x198 (ProceduralLive): `[+0x194] = 1; [+0x34] = &[+0xa8]; UpdateLiveAnimation(robot)`; **a nonzero result does sErrorF, sets the error flag and returns
  it (no S6)**; zero goes to S6.
- **S5 (0x57d1e4) other top:** a current idle that has not ended and `[+0x64] != 0` goes to S6; otherwise `HasAnimationForTrigger` -> `GetAnimationForTrigger` -> `GetAnimationNameFromGroup(strict=false)` ->
  `CannedAnimationContainer::GetAnimation` -> `[+0x34]`; failure paths return 1 with `[+0x34] = 0` and no `+0x44` change.
- **S6 tail (0x57d3ee..0x57d448):** if `sb != [+0x34]` or `[+0x64] == 0` (or the idle ended): `[+0x64] = 0; InitStream([+0x34], 0xFF); [+0x64] = 1;` result 0 (no UpdateStream that tick); else
  `UpdateStream(robot, [+0x34], false)`, `[+0x88] = now`. Then `[+0x44] += 0x3c` (0x57d442..0x57d446) on both sub-paths.

**Inside UpdateLiveAnimation (0x57d5f8):** gates G1 `[+0x194] != 0`, G2 `[+0x44] >= (int)p2` (unsigned), G3 `[[robot+0x280]+4] == 0`, each returning 0 with no decrement; then body, lift, head in that order as in
1.3 (generation draws on `[+0xa4]` in the order of 1.4; lift 0x57d97e..0x57d9ca; head 0x57d812..0x57d866 with `angle = (s8)trunc(robot[+0x2fc] * 57.29578f)`). It returns 0, or 1 when a keyframe append fails.

**FaceLayerManager::KeepFaceAlive 0x58d374:** `[+0x14] -= 60; [+0x18] -= 60` (0x58d386..0x58d38c); dart if `at(0x16) > 0` and `[+0x18] <= 0` and (`[+0xc] == 0` or (`== 1` and `HasLayerWithTag([+0x1c])`)):
`GenerateEyeShift(map)` 0x58d100 (x = RandInt(-d,d), y = RandInt(-d,d), dur = RandInt((int)p25,(int)p26), `LookAt(x,y,5.0f,5.0f,p28,p29,p27)`, keyframe trigger = dur at 0x58d220), then `AddToPersistentLayer(tag)` or
`AddPersistentLayer("KeepAliveEyeDart", track)` (name at 0x58d5e4), then `[+0x18] = RandInt((int)p20,(int)p21)`; blink if `[+0x14] <= 0`: `GenerateBlink`, `AddLayer("Blink", track, 0)` (name 0x58d600),
`[+0x14] = RandInt(p0,p1)` (7500..30000 fallback).

**Already implemented by the M5 port** (`AnimationScheduler.Advance`, `NoAnimationPathLocked`, `UpdateLiveAnimationLocked`, `TrackLayers.cs`): S1 steps 1-7 (the SetDefaultParams slot identity checks out); S2 including the loop
bookkeeping; S3/S3b including "no +0x88 update" and the flush; the S4 flag and idle assignment; S5 picking; S6 including `+0x44 += 60` (`AnimationScheduler.cs:1021`); UpdateLiveAnimation G1-G3, all three tracks, draw
order, keyframe parameters, the eye-shift calls (K2 with the 17/12 limits and "LiveIdleTurn", `:1086`); KeepFaceAlive, GenerateBlink, GetNextBlinkFrame (table, scanline flip), AddPersistentLayer and
AddToPersistentLayer (`kf.trigger + last.trigger + 33`), ApplyLayersToFrame (+33, frozen clock, persistent hold), GetFaceHelper, Combine. S0 is omitted (non-behavioural). Production wiring of the robot inputs:
`CozmoAnimations.cs:178-189`.

**Lacking or different in the port (what the rebuild must do or fix):**
1. `UpdateLiveAnimationLocked` returns 0 when the top lock name is `"StreamLive"` (`AnimationScheduler.cs:1062`), and `StreamLive` pushes ProceduralLive itself (`:885`): both go. With ProceduralLive on the stack by
   the engine's own route the port's generator runs (G1 needs `_liveFlag`, set in the same branch, `:979-981`).
2. S4 error path: the port logs (`:982`) and continues into the tail (`:1004`); the engine returns the nonzero result and skips S6 (0x57d086..0x57d0e6 -> 0x57d032).
3. Time width: the engine keeps +0x88, +0x1c0 and `now` as float32 seconds (`GetCurrentTimeInSeconds` returns the float in r0 and `str.w r0,[r4,#0x88]` stores it, 0x57d02e/0x57d43e) and uses `vsub.f32` and `vcmpe.f32`
   (0x57cf9a/0x57cfa4); the port holds `_lastStreamSec` (`:429`) and `_keepAliveTimeoutSec` (`:434`) as `double` and derives `nowSec = nowMs / 1000.0` (`:898`). CHECKLIST 4 width.
4. The CarryingComponent input (`[[robot+0x284]+8] != -1`) is not wired in `CozmoAnimations.cs:178-189`: the lift's carrying gate always reads clear (M5-030's own unresolved note).
5. `DesiredFaceDistortion` (the `TrackLayerComponent::Update` glitch) is a `Func<float>?` that nobody sets in production (`AnimationScheduler.cs:485`), so S1 step 2 never fires; the C# has no DesiredFaceDistortionComponent.
6. LookAt float association (1.6), and the SetFacePosition clamp written as `max(-xmin, min(128-xmax, x))` (`ProceduralFace.cs:216-219`) against the engine's `min(max(x,-xmin), 128-xmax)` (equal unless lo > hi or NaN).
7. Who pushes 0x198: not established (1.8). The retired `IdleBehavior` gates `Arbiter.AutonomyEnabled` and `Arbiter.Running > Idle` have no engine counterpart in the streamer; the engine's gating is exactly S1 gates A-C,
   the +0x38 test and the idle-stack top.

What `IdleBehavior` contributes that the engine does not have (all to retire): the `Execute` and `ExecuteMotors` tool flags, the arbiter gates, the 16-tick catch-up, `System.Random`, the direct `Face.SetParameters` render,
the clamp of the head angle to sbyte range (`IdleBehavior.cs:408` against the engine's wrapping `(s8)` of the truncated int), the 64/32 limits, the 33 ms transient turn shift, and `IdleParameters` (a second copy of
the 30 defaults; they do match 0x57db40..0x57dcd6, M7-004).

---------------------------------------------------------------------------------------------------------------------

## B. Deliverable (b): EvaluateY and the Emotion update widths

**`Anki::Util::GraphEvaluator2d::EvaluateY(float x)` 0x804bd0..0x804c44.** Nodes are `{f32 x, f32 y}` (8 bytes: `asrs r0,#3` at 0x804be8; count = (end - begin) >> 3).
```
if (first.x > x)               return first.y             ; 0x804bda..0x804be2 -> 0x804c3c
if (count < 2)                 return first.y             ; 0x804bea..0x804bec
for i = 1 .. count-1:                                      ; 0x804bee..0x804c04
    if (node[i].x >= x) {                                  ; vcmpe s4(node[i].x), s0(x); bge 0x804c08
        gap = node[i].x - node[i-1].x                      ; vsub.f32 at 0x804c10
        if (gap <= 1e-5f)   return node[i-1].y             ; 0x804c0c..0x804c1c -> 0x804c3c : the LEFT node's y
        t = (x - node[i-1].x) / gap                        ; vsub, vdiv.f32 at 0x804c1e..0x804c26
        return node[i-1].y + t * (node[i].y - node[i-1].y) ; vsub, vmul, vadd.f32 at 0x804c2e..0x804c36
    }
return last.y                                              ; the loop falls out at 0x804c06 -> 0x804c3c with r2 = &node[count-1]
```
1e-5f = 0x3727C5AC. All float32. The division is done first (`t = a/b`), then multiplied by dy, then added to left.y. The near-coincident rule returns the LEFT node's y when `gap <= 1e-5f`. The C# `DecayGraph.At`
(`Mood.cs:47`) is double, returns `y1` when `span <= 0`, and interpolates when `0 < span <= 1e-5` (different); the loaders sort nodes by x (`Mood.cs:111`, `:177`); whether the engine's
`GraphEvaluator2d::ReadFromJson` 0x67d128 sorts was not read here (UNVERIFIED).

**`Emotion::Update(const GraphEvaluator2d& g, double now, float dt)` 0x6795a4..0x679612** (`this`+0x18 f32 value, +0x1c f32 decay time; `dt` is the stack argument at `[sp+0x28]`; `now` rides in r2:r3 and is not used
by the arithmetic):
```
old        = g.EvaluateY(this.decay)          ; 0x6795b4
this.decay = this.decay + dt                  ; vadd.f32 0x6795c6, vstr 0x6795ce      (float32)
new        = g.EvaluateY(this.decay)          ; 0x6795d2
f          = (old > 1e-5f) ? new / old : new  ; vcmpe s18,s4; vdiv.f32; it gt; vmovgt  (0x6795e2..0x6795f0)
this.value = this.value * f                   ; vmul.f32 0x6795f8, vstr 0x6795fc
history.push({value, dt})                     ; bl 0x67945c (0x679600..0x679608)
```

**`Emotion::Add(float d)` 0x679618..0x6796c0:** `s = value + d` (float32); `value' = (s > -1.0f) ? ((s >= 1.0f) ? 1.0f : s) : -1.0f` (0x679628..0x67964e; 1.0 and -1.0 are vmov immediates, bits 0x3F800000 and
0xBF800000); stored at +0x18 (0x67967a). The decay clock `[+0x1c] = 0.0` is written iff `(old >= 0) != (value' >= 0)` (teq 0x6796a8, bne 0x6796bc) OR (`|d| > 0.05f` [0x3D4CCCCD, 0x67967e/0x67968a] AND
`(old >= 0) == (d >= 0)`) (0x6796ae..0x6796b6). `Mood.cs:290-300` matches this logic; its width is double (the 0.05 compare is `Math.Abs(double) > 0.05` against the engine's `|float| > 0.05f`).

**`MoodManager::Update(float t)` 0x67b5d4:** `last = [+0x130]`; `dt = (last != 0) ? t - last : 1e-4f` (0x67b5f6, 0x67b604); if `dt < 1e-4f` a warning and `dt = 1e-4f` (0x67b608..0x67b66c; literal 0x38D1B717 at 0x67b5ea);
`[+0x130] = t` (0x67b67e); for type 0..8: `Emotion::Update(StaticMoodData::GetDecayGraph(type), (double)t, dt)` (0x67b682..0x67b69e); then `SendEmotionsToGame()` (0x67b6a4).
**`UpdateLatestEventTimeAndGetTimeElapsedInSeconds` 0x67be48:** an emplace into the f32 map at +0x120: a new key gives FLT_MAX (0x7F7FFFFF at 0x67bea4); an existing key gives `now - old` (`vsub.f32` at 0x67beb2) and
stamps `now` (0x67beae). `CalculateRepetitionPenalty` 0x679bb8 is a tail call of `EvaluateY` on the graph at `EmotionEvent+0x18`. The Emotion stride (0x20, value at +0x18) and the nine-emotion loop match `Mood.cs`.

---------------------------------------------------------------------------------------------------------------------

## C. Deliverable (c): every float constant, as bit patterns

Live-idle defaults, `SetDefaultParams` 0x57db40..0x57dcd6 (index: bits = value), all read from the movw/movt/vmov sequences:
```
 0 0x453B8000 = 3000.0   (HasSettableParameters::SetParam, no clamp)       15 0x42480000 = 50.0
 1 0x457A0000 = 4000.0   (AnimationStreamer::SetParam: clamped to 30000)   16 0x43FA0000 = 500.0
 2 0x447A0000 = 1000.0                                                      17 0x437A0000 = 250.0
 3 0x42C80000 = 100.0                                                       18 0x447A0000 = 1000.0
 4 0x447A0000 = 1000.0                                                      19 0x40C00000 = 6.0
 5 0x437A0000 = 250.0                                                       20 0x437A0000 = 250.0
 6 0x44BB8000 = 1500.0                                                      21 0x447A0000 = 1000.0
 7 0x41200000 = 10.0                                                        22 0x40C00000 = 6.0
 8 0x3F000000 = 0.5                                                         23 0x3F6B851F = 0.92f (0.9200000166893005)
 9 0x42480000 = 50.0                                                        24 0x3F8A3D71 = 1.08f (1.0800000429153442)
10 0x43FA0000 = 500.0                                                       25 0x42480000 = 50.0
11 0x437A0000 = 250.0                                                       26 0x43480000 = 200.0
12 0x44FA0000 = 2000.0                                                      27 0x3DCCCCCD = 0.1f (0.10000000149011612)
13 0x420C0000 = 35.0                                                        28 0x3F8CCCCD = 1.1f (1.100000023841858)
14 0x41000000 = 8.0                                                         29 0x3F59999A = 0.85f (0.8500000238418579)
```
SetParam(1) clamp bound: `GetMaxBlinkSpacingTimeForScreenProtection_ms()` = `movw r0,#0x7530` = 30000 (0x58d8bc), converted with `vcvt.f32.u32`; a larger value gets a warning and is replaced (0x57c07a..0x57c0c6).
GetParam<int> = `vcvt.s32.f32` (0x57dd18), GetParam<u8> = `vcvt.u32.f32` (0x57de58); both first run the lazy `[+4]` default-set (vtable slot 0).

Other constants:
- 0x42652EE1 = 57.29578f (literal 0x57db2c, used at 0x57d838: `vmul s16 = angle * const`, then `vcvt.s32.f32`).
- 0x3F000000 = 0.5f keep-alive timeout `[+0x1c0]` (0x57a040, 0x57e010). 0xFF7FFFFF = -FLT_MAX initial `[+0x88]` (0x579fde).
- UpdateLiveAnimation eye-shift stack arguments: 0x42800000 = 64.0 and 0x42000000 = 32.0 (both discarded by GenerateEyeShift), 0x3F8CCCCD = 1.1f, 0x3F59999A = 0.85f, 0x3DCCCCCD = 0.1f, dur 0x21 = 33;
  RandDblInRange bounds as doubles 0.0 and 0x3FF0000000000000 = 1.0 (0x57d720); `vmov.f32 s0,#+-1.0` for the turn sign.
- GenerateEyeShift(map) LookAt arguments: 5.0f = 0x40A00000 (`movt r3,#0x40a0` at 0x58d1ea) for both xMax and yMax; parameters 0x16, 0x19, 0x1a, 0x1b, 0x1c, 0x1d.
- GenerateEyeShift(8-arg) 0x58cfc4: 0x43000000 = 128.0 (0x58cfec), 0x42800000 = 64.0 (0x58d01a).
- LookAt: -2.0f, 1.0f, 2.0f are `vmov.f32` immediates (0x58416c, 0x584188, 0x5841fc). SetFacePosition: 0x43000000 = 128.0 (0x583b4c), 0x42800000 = 64.0 (0x583b8c).
- GetEyeBoundingBox: 30.0f and 0.5f as vmov immediates (0x584568, 0x584570), 0x42000000 = 32.0 (0x584594), 0x42C00000 = 96.0 (0x5845b0), 0x42200000 = 40.0 (0x5845bc).
- Blink table 0xC5AAD8 (heightMul, widthMul, dur, action): (0x3F59999A 0.85, 0x3F866666 1.05, 33, 0); (0x3F19999A 0.6, 0x3F99999A 1.2, 33, 0); (0x3DCCCCCD 0.1, 0x40200000 2.5, 33, 0);
  (0x3D4CCCCD 0.05, 0x40A00000 5.0, 33, 1); (0x3E19999A 0.15, 0x40000000 2.0, 33, 2); (0x3F333333 0.7, 0x3F99999A 1.2, 33, 3); (0x3F666666 0.9, 0x3F800000 1.0, 100, 3). Lid parameter list 0xC5AB48 =
  {0x10, 0x12, 0x11, 0x0d, 0x0f, 0x0e}. Past the table: face = orig, dur 0x21 = 33 (0x586190). The C# `BlinkTable` (`TrackLayers.cs`) and `IdleBehavior.BlinkFrames` agree with these values; they are decimal `f`
  literals whose float parse equals these bits (checked), but no test asserts the bits.
- 1e-5f = 0x3727C5AC (EvaluateY 0x804c0c; Emotion::Update 0x6795d6; TrackLayerComponent::Update 0x64edfa). 0.05f = 0x3D4CCCCD (Emotion::Add 0x67967e). 1e-4f = 0x38D1B717 (MoodManager::Update 0x67b5ea).
  FLT_MAX = 0x7F7FFFFF (0x67bea4).
- Loop period 0x03938700 = 60 000 000 ns (0x65b3d2/0x65b3d8); 0x3C = 60 ms decrement and increment.
- Blink spacing fallback ints 7500 (0x1d4c) and 30000 (0x7530) at 0x58d540/0x58d544.

---------------------------------------------------------------------------------------------------------------------

## D. Stated by the audit, not confirmed here (UNVERIFIED)

- M7-017: that FistBump, Bouncer, PeekABoo and ReactToOnCharger push 0x23f in a way that the C# `StreamLive` would sit on top of. The mechanism (`TopTrigger != ProceduralLive` -> push on top) is real
  (`AnimationScheduler.cs:885`); the behaviour list was not checked.
- M7-013: whether `GraphEvaluator2d::ReadFromJson` 0x67d128 requires or sorts ascending x; the claim in `Mood.cs` that an empty graph evaluates to 1 (the engine's `EvaluateY` dereferences `begin`
  unconditionally; `EmotionEvent::ReadFromJson` 0x679bc0 calls `GraphEvaluator2d::Clear()` and `AddNode` at 0x679d9e: the default node was not decoded here).
- Which message sets Robot +0x2a (ready to stream): M1's CD12/CD20 say the NVStorage on-idle callback; I found the ctor write and a bound-handler write (0x52c3a6) only.
