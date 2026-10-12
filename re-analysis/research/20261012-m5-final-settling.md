# M5 final settling pass (Opus, 2026-10-12)

The pass ran on HEAD a6b19bb5 (suite 4361/4361), read-only, in two parts:
- part A: M5-001..018;
- part B: M5-020..046.

Every claim relied on was reopened in `libcozmoEngine.so`. The manager adopted the verdicts on 2026-10-12. This file is
the checked source for jobs B-M5-8 and X-M5-9.

## Settled (13)

**EXACT_SOURCE**
- M5-002, M5-005, M5-017, M5-022, M5-024, M5-028, M5-029, M5-033, M5-034, M5-035, M5-039, M5-040, M5-041 and M5-045.
- Each record's `unresolved` names the ranges.

**EQUIVALENT_IMPLEMENTATION: M5-021**
- The OpenCv310 port matches stock 3.1.0 and the shipped `libopencv_imgproc` (edge dx at 0x00042B8E..0x00042B94).
- Only bionic tanf/cosf/sinf are equivalent (ruling 4.2). The rest is exact.

**Non-blocking notes**
- M5-002:
  - A FlatBuffer face keyframe without eye vectors makes the engine dereference null (0x00583902..0x00583904).
  - `MathF.Max(0f,-0f)` gives +0, where the engine keeps −0 (0x00583A58..0x00583A60).
  - For the JSON centre and scale, the engine reads both members before testing either.
  - The FlatBuffer WrongNumParams warning has no test. No shipped data reaches any of these.
- M5-017: a keyframe built in code with fewer probabilities than events throws in `ChooseAlternative`
  (AnimationScheduler.cs:1830).

## Kept IMPLEMENTATION_GAP: code defects (checked rows for B-M5-8)

**D1 (M5-027, M5-023): flush warnings print the wrong count.** AnimationScheduler.cs:1141 and :1144 print
`Stream.Count`, which includes the zero-size stack-only entries (`BufferLocal` and `BufferFaceFired`, :1644-1657 and
:1551-1557). The engine prints +0x94, the size of the EngineToRobot list (0x0057D142, 0x0057D18E).

**D2 (M5-027): RemoveIdleAnimation searches at most 1000 entries.** The loop is 0x0057BB3E..0x0057BB48
(`r8 += 1; cmp r8,#0x3e8; blo`). After it comes LoopBoundOverflow (0x0057BB60). An entry at index 1000 or later is
never found, so the result is NoLockNameFoundInStack. The stack (AnimationScheduler.cs:945) searches every entry.

**D3 (M5-030): three per-track errors.** UpdateLiveAnimation logs sErrorF with format "" (0xBE3F00), then errG and the
debug break, then returns 1, when `AddKeyFrameToBack<T>` fails:
- head: 0x0057D882, key `AnimationStreamer.UpdateLiveAnimation.AddHeadAngleKeyFrameFailed` (0xBEF48E);
- body: 0x0057D914, key `…AddBodyMotionKeyFrameFailed` (0xBEF40A);
- lift: 0x0057D9E4, key `…AddLiftHeightKeyFrameFailed` (0xBEF44C).

Each comes after the inner `Animation.AddKeyFrameToBack.Failed` and before the outer LiveUpdateFailed. The stack
(AnimationScheduler.cs:1228-1231, :1240-1243, :1253-1256) only returns 1.

**D4 (M5-031): the AddGlitch debug log has the wrong channel.** It is sChanneledDebugF at 0x0064EE42 with channel
`Animations` (0x0064EF68), key `TrackLayerComponent.AddGlitch` (0x0064EF74) and format `Degree %.2f` (0x0064EF94).
TrackLayers.cs:677 passes a null channel.

**D5 (M5-043): the only test cannot fail.** M5FixRowsTests.cs:236-241 asserts `FaceAnimationOverride == 0`, which is
the literal `=> 0` (StreamAnimation.cs:116). FaceAnimationIsDone (AnimationScheduler.cs:1491-1495) never reads it. The
test needs to exercise behaviour.

**M5-032: no test separates the two forms.** `inv·i` and `i/(max−min)` differ in 460 of 2016 cases (audit 2026-09-29).
The code is right (ProceduralFaceRenderer.cs:111-115, 0x00585E28/0x00585E34). A test with binary-derived values is
needed.

**M5-006: the JSON body-keyframe errors.**
- A missing `radius_mm` logs sErrorF `BodyMotionKeyFrame.SetMembersFromJson.MissingRadius`
  "%s: Missing 'radius_mm' field." plus errG (0x004FB8A0..0x004FB8F8).
- An unknown radius token logs BadRadiusString from inside ProcessRadiusString (call 0x004FB7FA, log 0x004FB62C) plus
  errG.
- A missing `durationTime_ms` or `speed` logs `IKeyFrame.GetMemberFromJsonMacro` "Failed to get '%s' from Json file."
  plus errG (0x004FB816, 0x004FB85C).

The stack logs its own text plus a MISSING (JsonClipLoader.cs:101, :223, :230).

**M5-009: the JSON audio-keyframe errors (0x004FA0E8..0x004FA476).** The stack returns strings at
JsonClipLoader.cs:345 and :350, and `DefineAudio` gets no sink (:189). The engine logs:
- sErrorF `%s: The number of audio event IDs (%u) does not match number of probabilities (%zu)` (0x004FA35A);
- `…exceeds 1.0` (0x004FA40E).

Each takes errG and the debug break.

**M5-016: a missing or null `durationTime_ms`.** The engine logs `IKeyFrame.GetMemberFromJsonMacro`
"Failed to get 'durationTime_ms' from Json file." plus errG and the break (0x004FADD2..0x004FAE30). A non-number array
element takes the same path. The stack `Required` throws (JsonClipLoader.cs:318).

**Shared tail (M5-001, M5-006, M5-009, M5-016).** After any keyframe failure, the parent logs
`Animation.DefineFromJson.AddKeyFrameFailure` "Adding %s frame %d failed." plus errG, and the loop exits
(0x00576E74..0x00576EC2). This is already ported for the backpack colour; apply it to every keyframe failure.

**M5-010: the keep-alive clock.**
- The engine's gate (0x0057CF86..0x0057CFAE) and its +0x88 store (0x0057D02A..2E) use the float
  `GetCurrentTimeInSeconds`, which is `(float)(ns / 1e9)` (0x0084BC80).
- The stack uses `(float)(nowMs/1000.0)` from truncated milliseconds (CozmoAnimations.cs:258,
  AnimationScheduler.cs:1002).

**M5-011: the group-entry error tails.**
- A rejected entry takes the shared tail errG plus break (0x0058C6E8..0x0058C6F4).
- The parent then logs `AnimationGroup.DefineFromJson.AddEntryFailure` "Adding animation %d failed." plus errG
  (0x0058A6A2..0x0058A6D8).
- NoAnimations sets errG (0x0058A778..0x0058A78C).

The stack's DefineGroup (AnimationLibrary.cs:820-874) logs without these.

**M5-014: NoHeadAngleWhenUsingHeadAngles.** The engine logs it at 0x0058C7F2, then errG plus break
(0x0058C818 → 0x0058C6E8), then AddEntryFailure plus errG (0x0058A6A2). The stack (AnimationLibrary.cs:871-873) only
logs.

**M5-013: name lookup is exact.** GetNumFrames (0x005816D8) and GetFrame (0x00581790) match names exactly; a miss gives
0 frames, and the keyframe is consumed. The stack's FaceAnimationLibrary.cs:34-36 uses `OrdinalIgnoreCase`.

**M5-018: tests.**
- `AnimationGapTests.ALateTickCatchesUpFrameByFrameNotByJumping` is circular: it tests the unpaced test seam.
- No test drives "non-empty buffer → false" or "no audio animation → HasFramesLeft" (0x0057CC72..0x0057CC8C).

**M5-001: a malformed `.bin`.** `FlatTable.Root` can throw out of Open (AnimationLibrary.cs:473, 320, 332). The engine
has the BinaryDataNull error path.

## Kept IMPLEMENTATION_GAP: steps with no owning record (extraction X-M5-9)

- **M5-001:**
  - the main-thread checks 0x0051F3AA and 0x00521338;
  - the JsonCpp reader-error text 0x0084C124..0x0084C15C;
  - the handling of a non-object root's Json::LogicError (0x008E6D08, under the mutex).
- **M5-008:** the VERIFY failure's callstack dump (0x0080DA08) and log flush (0x0080DA24).
- **M5-011:** `MoodManager::GetSimpleMood` (0x0058A954), an M7 input.
- **M5-020:** the Unity Random seed, an app input. It needs a COMPATIBILITY_POLICY record (UnityRandom.cs:33 uses
  `Environment.TickCount`).
- **M5-037:** the DrivingAnimationHandler +0x4C handles and the +8 owner (0x00511524..0x00511542), and the SetupHandlers
  vector (0x0057AF6C).
- **M5-038:** the concrete M8 TriggerAnimationAction runner (factory 0x0052CEA2..0x0052CFBE). M8-017 owns only the
  compound's execution.
- **M5-044:** the tag-B4 shared subscription's last release (0x0057AFD4).
- **M5-046:** the node history for an absent type or a non-fresh map.

## New records needed (extraction X-M5-9)

1. **The animation-group file loader:**
   - `RobotDataLoader::LoadAnimationGroups` 0x0051FC74 and `LoadAnimationGroupFile` 0x00521D7C;
   - the +0x278 gate, `readAsJson`, and a silent skip of empty JSON (0x00521DAC);
   - the info `RobotDataLoader.LoadAnimationGroupFile.LoadingSpecificAnimGroupFromJson` "Loading '%s' from %s", channel
     Animations (0x00521F0A);
   - the mutex;
   - `AnimationGroupContainer::DefineFromJson` 0x0058B890, which adds by name, with errors at 0x0058B8D6 and 0x0058B934
     (each plus errG).

   The stack (AnimationLibrary.cs:337-350, 889-893) has no empty skip and no info log, invents a parse-error log, and
   lets a non-JSON IO exception propagate. The test `M5LastReadsTests.B_M5_4_GroupDefinitionPrecedesBackpackLoadTime`
   (:186-205) asserts NoAnimations for `{}`, which the binary contradicts.
2. **The face-animation loader:** `FaceAnimationManager::ReadFaceAnimationDir` 0x00580048 and
   `LoadAnimationImageFrames` 0x00580834:
   - frames in sorted order;
   - `stoi` frame numbers, with a negative number an error (0x00580B2E);
   - gaps filled with empty frames (0x00580A32..0x00580AB0);
   - 64×128 required (error 0x00580CC2);
   - eager load at 0x0051F3CE.

   The stack loads lazily inside the tick, sorts case-insensitively, does no gap fill or size check, and throws on a
   non-grayscale PNG.
3. **The M3 stream buffer's count semantics** (D1). The zero-size stack-only entries need an owning row (M3-012..014).
