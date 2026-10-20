# M5 settling pass, part A (Opus, read-only), 2026-10-19, HEAD 25f9ab57

- **Run:** read-only cozmo-verifier over loading, group choice and the face path: M5-001, 009, 013, 016, 051, 052, 054,
  065, 011, 014, plus the boundaries of M5-003, 021, 032. Records quoted from the current manifest. Addresses re-read
  only where doubtful or changed after settling-2 (07600055): B-M5-11..22 and B-M1-5/6.
- **Commands:** `fidelity.py --check` exit 0 (706 records). Focused `dotnet test` 594/594 (M5AnimationTests,
  M5BuilderCorrections, M5FinalBuilder, M5FixRows, M5JsonColor, M5LastReads, M5X9/X11/X14/X16/X20/X21/X22,
  AnimationAssetTests, AnimationTests, AnimationGroupGateTests, AnimationGapTests, ProceduralFaceRendererTests,
  M12RVisBuildTests, TriggerTests, M3_015_A17).
- **Overall: FAIL.** One blocking finding (the group draw's head-angle source, M5-011/M5-014); cheap to fix.
- **Adoption:** the manager confirmed the blocking finding against the code on 2026-10-19 (`CozmoAnimations.cs`:410
  reads `_robot.State.HeadAngleRad`; `Motion.HeadAngleRad` holds robot+0x2FC from −25°); full adoption with part B.

## Verdicts

| Record | Verdict | Reason |
| --- | --- | --- |
| M5-001 | SETTLE after queued cleanup | FlatBuffer/JSON paths unchanged since B-M5-11 (PASS); B-M1-5/6 load order is the operator's M1-055 decision; duplicate redefinition matches 0x00588338→0x00575734; `unresolved` stale |
| M5-009 | SETTLE | FlatBuffer 0x004F9E54..0x004FA05C and JSON 0x004FA0E8..0x004FA476 re-read, match; tests unlabelled |
| M5-011 | NOT YET (in-layer, cheap) | Draw logic matches 0x0058A946..0x0058AE2A; head input is the raw report with 0 fallback, not robot+0x2FC; mood and clock are M7-026's (clean split) |
| M5-013 | SETTLE after queued cleanup | +0x28 gap closed by M5-058 (EXACT_SOURCE); B-LOG-1 and the CompressRLE note belong to the procedural-face path; CompressRLE fails only at the 64x128 gate |
| M5-014 | NOT YET (same build as M5-011) | Backup 0x0058AB30..0x0058ADBA and container 0x0058BA28..0x0058BA98 match; the same wrong head input feeds the ±0.05 window |
| M5-016 | SETTLE after queued cleanup | Unchanged since B-M5-11; stale MISSING sentence superseded by D3 |
| M5-051 | SETTLE after queued cleanup and a recorded exclusion | Error/errG/break match; G5b is Debug text sink (a) drops; JsonCpp vs System.Text.Json grammar differs only on non-shipped files (all 512 shipped clip/group/map files parse strictly) |
| M5-052 | SETTLE after queued cleanup and a recorded exclusion | Every shipped face decodes as R-M5-052 says; open items are non-shipped PNG variants and non-canonical stoi input; F4 dir order has no observable effect (keyed by name); F20 moot |
| M5-054 | SETTLE | 0x00588074..0x005880E4 re-read; key 0x00588110, text 0x00588148, errG, break, null; five native callers enumerated |
| M5-065 | SETTLE after queued cleanup | 0x0058B6F8..0x0058B768 re-read (key 0x0058B794, text 0x0058B7D0); callers C2/C3 log once; inline-probe UNKNOWN answered |
| M5-003 / 021 / 032 | Boundary holds (EQUIVALENT_IMPLEMENTATION) | Files unchanged since 07600055; only MathF.Cos/Sin/Atan2 (`ProceduralFace.cs`:174-178) and Tan/Cos/Sin (`ProceduralFaceRenderer.cs`:170, 270, 274, 288, 292) are non-exact |

## Objections

- **`CozmoAnimations.cs`:410 (with `AnimationLibrary.cs`:103, :274, :653) | CONTRADICTED (blocking; M5-011, M5-014).**
  The group draw's head angle is `_robot.State.HeadAngleRad`, the raw `Latest?.HeadAngle` (`CozmoRobot.cs`:73), null
  before any state and after a reset (`CozmoRobot.cs`:141), then 0 (`AnimationLibrary.cs`:103). The engine passes
  robot+0x2FC: `ldr.w r3,[r5,#0x2fc]` 0x0057AEE4 → GetAnimationName 0x0057AEF2. The Robot ctor stores 0xBEDF66F3
  (−0.436332 rad) there (0x0051007C/0x00510082, strd 0x00510086). The stack already models +0x2FC as
  `Motion.HeadAngleRad` (`Motion.cs`:157-158, 263-264; −25° from the ctor, written once HeadCalibrated, RS6 clamp
  `Motion.cs`:1463-1468; M4-001, accepted). They diverge before the first state, while the head is uncalibrated, and on
  out-of-range reports, changing the head gate (0x0058AA16..0x0058AA30) and backup window (0x0058AC2A..0x0058AC4C), so
  a different clip plays. The doc comment at `AnimationLibrary.cs`:274 is wrong. No test asserts the join (B6
  substitutes `HeadAngleRad = () => 1`).
- **`AnimationLibrary.cs`:233 | queued.** Group container is OrdinalIgnoreCase; native is `unordered_map<string>` with
  equal_to (0x0058B6FE). Native-reachable input never differs (573 trigger AnimNames match stems exactly; 507 stems, no
  case collisions); only stack-only `PlayGroup(string)`/`GetGroup` reach it.
- **`CozmoAnimations.cs`:503 | queued.** Public `PlayGroup(group, mood)` ignores `mood`.
- **`AnimationLibrary.cs`:651, `CozmoAnimations.cs`:420 | queued.** MISSING lines name M5-011; the owner is M7-026.
- **`AnimationLibrary.cs`:993-997 | queued (input our system never produces).** Group-entry DefineFromJson: the
  `isDouble` at 0x0058C714 tests the Weight value, so the engine always does `asDouble(CooldownTime_Sec)` (0x0058C71E;
  bool → 1.0, string terminates), the stack uses it only if a number; UseHeadAngle via GetValueOptional<bool> (0x004FA5AA)
  converts numbers, the stack accepts only `true`; a missing clip name skips the M5-054 InvalidName log GetAnimation emits
  (0x0058C560). Shipped data reaches none (1047 real cooldowns, 281 bool UseHeadAngle, all 884 entry names exist).
- **`FaceAnimationLibrary.cs`:64 | queued.** Unconditional startup "MISSING: M5-052" line; remove once the exclusion is
  recorded.
- **Manifest `unresolved` | queued.** M5-001: "four contiguous chunks" and "cross-file order HARDWARE_ONLY" superseded
  by M1-055 (also the doc comment `AnimationLibrary.cs`:219); DeviceAudio A14 is M5-041's; B-M1-7 S3 is an input we
  never produce. M5-016: the non-number element MISSING sentence superseded by D3. M5-013: B-LOG-1 and "CompressRLE != 0
  unread" belong to BufferFaceToSend (M5-032/M3); CompressRLE 0x00581904 errors only off 64x128
  (0x00581916..0x0058191C → 0x00581936). M5-065: the OV122 FeedingEat empty-name residual (0x005D61CE) is M15-069's.
- **Tests | queued.** Almost no cited M5 test has a `reach:` label (only M5X16, M5X20). `M5_014_D7_TheCooldownIsSharedByName`
  and two AnimationAssetTests return early and pass when `re-analysis/obb` is absent.
- **Races:** none blocking. `reportedMoodClock` (`CozmoAnimations.cs`:414) non-atomic (MISSING may print twice);
  `Eye.ClippingWarningEnabled` is a process static toggled by the loader, but no face is drawn while it is toggled.
- **Circular tests:** none (G4/G1 use the standard mt19937 seed-5489 words; C13/D6 hand-derived; M5X11/X16 literals from
  binary strings).

## Per record (supported)

- **M5-011 (D5):** mood filter `ldrb [r7,#0x1c]`, `anyMood` set even when head/cooldown fail (0x0058AA64); head gate
  skips min > head or unordered (`bhi` 0x0058AA22) or max < head (`blt` 0x0058AA30) = `AnimationLibrary.cs`:114;
  IsAnimationOnCooldown end > now (0x0058BA46..0x0058BA50); f32 weight sum (0x0058AA60) into RandDbl before the empty
  test (0x0058AA7E/0x0058AA86); pick r −= w on `bmi`, else last (0x0058AA92..0x0058AAB0); cooldown store
  (0x0058AAB4..0x0058AAC8); NoMoodMatch then Default recursion (0x0058AD14..0x0058AD5E); NoAnimation sErrorF/errG/break/""
  (0x0058ADC0..0x0058AE28); mood/time inputs 0x0058A954/0x0058A95C are M7-026's.
- **M5-014 (D6/D7):** backup only when anyMood && !strict (0x0058AB92..0x0058AB9A) over Default entries (0x0058AC0C);
  TimeUntilCooldownOver < best from FLT_MAX (0x0058AC18..0x0058AC28); window min+0xBD4CCCCD / max+0x3D4CCCCD
  (0x0058AC2A..0x0058AC4C) = `AnimationLibrary.cs`:150; no cooldown on backup; NoBackup returns the first entry
  (0x0058ADBA); container 0x0058BA28..0x0058BA98; M5-047 policy at line 147; entry DefineFromJson
  0x0058C4C0..0x0058C820 (Name, GetAnimation, Weight isDouble 0x008EB79C, Mood, Cooldown, UseHeadAngle, angles ×
  0x3C8EFA35 to +0x20/+0x24; errG tail 0x0058C6E8).
- **M5-009:** FlatBuffer volume 1.0, hasAlts true, absent/empty probabilities 1/N (0x004F9F3A..0x004F9F42), mismatch
  log, sum `bgt` 1.0 (0x004F9EFC..0x004F9F04), ids low 32 bits (0x004F9F4C). JSON hasAlts false, volume 1.0; array id
  GetVectorOptional else scalar push (0x004FA1B2..0x004FA1F4); empty vector → 1/N (0x004FA26A→0x004FA39C); mismatch
  0x004FA35A, sum 0x004FA40E; scalar id asUInt64 (0x004FA224), no sum check. = `JsonClipLoader.cs`:348-387,
  `AnimationLibrary.cs`:806-833.
- **M5-001:** SanityCheck 0x0058854C key "CannedAnimationContainer.DefineFromJson" for both paths = Register
  (`AnimationLibrary.cs`:617-621); DefineFromFlatBuf 0x00588322; production load EngineReadiness → LoadForEngine
  (`CozmoAnimations.cs`:363-379) → OpenForLoad; errG has no reader in the stack, so errG-only differences are
  log-equivalent.
- **M5-054:** find, miss: sErrorF, errG, conditional break, return 0 (0x0058807A..0x005880E4). Callers: PlayAnimationAction::Init
  0x00543F78 (M8), GetAnimation(trigger) 0x00561E18 (SayText, not built), streamer 0x0057A1F2/0x0057B14E/0x0057D292
  (`AnimationScheduler.cs`:706, 755, 1254).
- **M5-065:** the string map `find` (PLT 0x4AE890) has two BL callers: GetAnimationGroup 0x0058B6FE and HasGroup
  0x0058B816. GetAnimationGroup callers: 0x00561DE4 (SayText), 0x0057A0E2 (streamer ctor), 0x0057AE80. Matches
  `AnimationLibrary.cs`:628-660 and `AnimationTriggerMap.cs`:181-182.
- **M5-052:** shipped faces: two directories, 151 and 401 files, `name_NNNNN.png` from 0 (gap fill never fires); sort,
  1/8-bit decode, size gate as B-M5-9/10.
- **M5-013:** +0x28 override zero in production (M5-058); ordinal dictionaries (`FaceAnimationLibrary.cs`:34-37).

## Test reach

| Record | Through production | Unit only (what is substituted) |
| --- | --- | --- |
| M5-001 | StartupDeliveryRegressionTests.ShippedStartupPublishesTheWholeListBeforeReady… (not in M5-001's test field); M5X9 LiveLoaderOwnsAThread… | M5_001_D1/J1/F2; M5JsonColor*; M5X9/X11 conversions |
| M5-009 | none asserting audio fields | M5_009_C13; M5X11 MissingOrNullAudioId, EmptyProbabilityVector; M5FinalBuilder JsonFailures |
| M5-011 | none for the provider join | M5_011_D5, G4, AnimationTests, AnimationGroupGateTests, M5X9 group rejections; B6 substitutes HeadAngleRad |
| M5-013 | none labelled | M5ScanLine C12, M5_013_5_x, AnimationAssetTests face track, M5FinalBuilder FaceMapMiss |
| M5-014 | none | M5_014_D6, D7 (skips without the OBB), AnimationGroupGateTests, M5_047/B6 |
| M5-016 | none labelled | M5_016_*, AnimationGapTests, M5JsonColor*, M5X11 null colour |
| M5-051 | M5X21 ReadAsJsonFalse… | M5X9 ReadAsJson*, JsonDepthFatal |
| M5-052 | M5X9 LiveLoaderOwnsAThread… | M5X9 Eager/FaceName/FaceSize/UnsupportedPng |
| M5-054 | none | M5X11 LibraryLookupMiss |
| M5-065 | none (TriggerInitAndIdle uses LoadFrom with a probe behaviour) | M5X16 (5), M5X14, M12RVis |

## Manager actions proposed

1. **Build (M5-011, M5-014):** wire `Library.HeadAngleRad` (`CozmoAnimations.cs`:410) to `_robot.Motion.HeadAngleRad`;
   drop the null→0 fallback or make it 0xBEDF66F3 for standalone libraries; fix the comment at
   `AnimationLibrary.cs`:274; production test with the head uncalibrated and a UseHeadAngle group, asserting the −25°
   choice.
2. **Lead for part B:** `CozmoAnimations.cs`:215 (live idle, M5-030) uses the same `State.HeadAngleRad ?? 0f`; native
   live idle reads robot+0x2FC.
3. **Record (manager exclusions, inputs our system never produces):** M5-051 JsonCpp grammar beyond the shipped files
   and the G5b Debug text; M5-052 non-shipped PNG types/depths and non-canonical stoi; group-entry non-numeric
   CooldownTime_Sec/UseHeadAngle conversions; B-M1-7 S3.
4. **Queued cleanup:** stale `unresolved` (M5-001, 013, 016, 065); MISSING lines name M7-026; remove the startup MISSING
   (`FaceAnimationLibrary.cs`:64); comment at `AnimationLibrary.cs`:219; `PlayGroup` mood; group comparer; reach
   labels; asset tests fail rather than return without the OBB; leading "; " in M7-026's test field.
5. **Settle now:** M5-009, M5-054. After 3 and 4: M5-001, 013, 016, 051, 052, 065. Confirm M5-003/021/032.
