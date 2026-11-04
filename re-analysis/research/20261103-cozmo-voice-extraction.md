# COZMO-VOICE-1 extraction: SayTextAction from text to speaker (Claude extractor, 2026-11-03; filed by the manager)

The path was read from libcozmoEngine.so unless a row says otherwise. Bank structure comes from
`research/20261007-sound-census-export.jsonl`. That census was produced by the stack's own bank reader and was not re-parsed here;
see open item 1.

**34 rows:** 26 EXACT_SOURCE, 8 RECOVERABLE_GAP, 31 NEW. Against the stack: 0 match, 4 are partial, the rest are not in the stack.

## A. Entry and intent

- **A1. SayText / SayTextWithIntent factories:**
  - **SayText** (0x0052AB00..0x0052AB40): text, style, durationScalar, pitch, playEvent trigger, fitToDuration.
  - **SayTextWithIntent** (0x0052AB50..0x0052AB7C): text, intent, trigger, fitToDuration.
  - The CLAD is in `unity/.../ExternalInterface/SayText.cs:157-166`.
- **A2. Behaviour call sites of the intent constructor:** TurnTowardsFace 0x0054C5A6; DockingTestSimple 0x005CC73A/0x005CC8DC;
  EnrollFace 0x005FD2CC/0x005FD326/0x005FD364; RespondToRenameFace 0x006008D4; ReactToPickup 0x00607A12. The stack logs
  DEFERRED or MISSING at each.
- **A3. Common ctor:** IAction "SayText", type 0x20 (0x00560F94..0x00561062; style ctor 0x00560B48..0x00560C96).
  - "SayTextAnimation" at +0x94.
  - Trigger 0x23F (none).
  - Max duration 30.0f.
- **A4. Intent lookup:** static map at 0x106A938 (0x0056106C..0x005610FA). If it is missing: error, and style, duration and pitch
  stay 0.
- **A5. Text-length bucket:** std::string byte length; the first trait with min ≤ len ≤ max, otherwise the first trait
  (0x0056107A..0x0056113E).
- **A6. GetDuration** (0x00561348..0x005613A8; RandInt 0x0082FA74..0x0082FA94):
  - if step > 1e-5f: `n = (int)trunc((max - min) / step + 1)`, `k = RandInt(n)`, value = `min + step * k`, all in f32; otherwise
    min;
  - two Robot-RNG draws, duration then pitch, only in the intent constructor (0x00561100, 0x00561142).
- **A7. LoadMetadata** `config/engine/sayTextintentConfig.json` (0x005602D8..0x00560895, called from 0x0051F38C):
  - the first definition wins;
  - unknown intents are skipped.
- **A8. Entry keys:** style, durationTraits, pitchTraits (0x00561F48..0x005620A6).
  - An unmapped style defaults to 3, Sentence.
  - Trait defaults (0x00562180..0x0056229C): textLengthMin 0, textLengthMax 0xFFFFFFFF, rangeMin FLT_MIN, rangeMax FLT_MAX,
    step 0. The file's own comment states the min/max defaults the wrong way round.
- **A9. Shipped intents** (asset, sayTextintentConfig.json:44-168): Unprocessed, Text, Cozmo_Says, Name_Normal,
  Name_FirstIntroduction_1, Name_FirstIntroduction_2.

## B. Synthesis

- **B1. GenerateTtsAudio** (0x00560D80..0x00560EC4):
  - name "SayText_" + PII(text);
  - style ≥ 4 is an error;
  - opId = CreateSpeech(text, style, durationScalar). **Pitch is not passed.**
- **B2. CreateSpeech** (0x0069E4C8..0x0069E80B):
  - op id = ++u8, skipping 0;
  - a TtsBundle with state 1;
  - a Dispatch::Async lambda.
- **B3. Component ctor** (0x0069E3B4..0x0069E41A): Dispatch::Create("TextToSpeech", 2).
- **B4. Async lambda** (0x0069F430..0x0069F518): CreateAudioData, then under the mutex:
  - data → state 2;
  - null → error and state 0;
  - bundle gone → the data is freed.
- **B5. Component CreateAudioData** (0x0069EF10..0x0069F040):
  - style 2 appends "?";
  - provider; the samples are s16 × 3.0518509e-05f.
- **B6. Provider ctor** (0x0069FA7C, decompiler only; RECOVERABLE_GAP):
  - tts_config.json supplies voice, speed, shaping and cozmo; pitch stays 100;
  - Android en: Ryan, speed 50.
- **B7. Provider CreateAudioData** (0x006A0210..0x006A057D):
  - 22050 Hz mono;
  - "Cozmo" is replaced with the cozmo string;
  - rate = (int)GetSpeechRate;
  - static Java createAudioData(text, rate, shaping, 100).
- **B8. GetSpeechRate** (0x0069F870..0x0069F8D0): `d = scalar > 0.05 ? min(scalar, 20) : 0.05`; `r = speed / d`, clamped to
  [30, 300], NaN → 30.
- **B9. Java bridge** (app input; CozmoTextToSpeech.java:89-156): setSpeechRate, VOICESHAPE, setPitch, speak, poll isSpeaking
  every 100 ms; load sets LEADINGSILENCE and TRAILINGSILENCE to 50.
- **B10. JNI callback** (0x0069FA00..0x0069FA3F): appends the samples.
- **B11. Acapela:** runs under the emulator (`20261103-okao-acapela-probes.md`). The effect of lrand48 is UNKNOWN.

## C. Wwise feed and lifecycle

- **C1. Init:** polls GetOperationState: 0 → ABORT, not 2 → RUNNING (0x00561488..0x00561630).
- **C2. PrepareAudioEngine** (0x0069E9A8..0x0069EB00):
  - duration_ms computed in f64, then f32;
  - GiveWavePortalAudioDataOwnership;
  - ClearOperationData.
- **C3. Duration over 30 s:** an error, but Init continues (0x005614C6..0x00561538).
- **C4. WavePortal** (Anki code inside the engine):
  - one global pending slot, last writer wins; later sources get Init result 2;
  - output at the provider rate; mono float render returns 0x2D, or 0x11 with zero padding;
  - plug-in 0x006412C2 (0x008DC1B8..0x008DDD20; render spot-checked). Registration is a RECOVERABLE_GAP.
  - The stack refuses source plug-ins (WwisePlayback.cs:136).
- **C5. No trigger:** a RobotAudioKeyFrame {GetAudioEvent(style), 1.0, 1.0} at t=0; the animation is set live, then
  PlayAnimationAction(loops 1, interrupt 1, tracks 0, 60.0) (0x0056153C..0x005615DC).
- **C6. Trigger with fitToDuration:** UpdateAnimationToFitDuration, then PlayAnimationAction (0x00561636..0x005616D4).
- **C7. Trigger without fit:** TriggerLiftSafeAnimationAction (0x005616D6..0x005616FC). No TTS keyframe is added, so the canned
  animation must carry Play__Robot_Vo__External_Cozmo_Processing (0xBF0BACE1). Byte hits in 10 files: the
  freeplay_reacttoface_sayname 01-03 animations, reacttoface_longname_01, meetcozmo, meetcozmo_reenrollment_sayname, greeting_02,
  greeting_happy_03, codelab_tasmanian_devil_01 and sdk_speak. Parse them with the M5 loader; this is a RECOVERABLE_GAP.
- **C8. UpdateAnimationToFitDuration** (0x005619F0..0x00561AF8; GetAnimation 0x00561D54..0x00561E8E): appends the trigger's
  animation until the last key time ≥ duration.
- **C9.** The inner action is replaced (0x00561700..0x0056170E).
- **C10. Style → switch** (table 0x00C573B0): 0 Unprocessed 0x334C99DC, 1 and 2 Name 0x2F8B3BF4, 3 Sentence 0x36089802.
- **C11.** PostRobotSwitchState(Cozmo_Voice_Processing 0x15D4FD9E), then PostRobotParameter(External_Process_Pitch 0x78DEBD05,
  pitch). Both happen before the inner action runs (0x00561728..0x00561754).
- **C12. Game object:** 7 when output source [+0x3C] == 2, else 6 (0x00599F62..0x00599FB0). It is the same object as the event
  (0x0059A09E..0x0059A0B6, 0x00596264).
- **C13. Switch and RTPC delivery** through AudioEngineMessageHandler::Broadcast (0x0059002C..0x0059018E). Ordering against
  PostEvent is a RECOVERABLE_GAP; read up to SetSwitchState 0x008D243C.
- **C14. GetAudioEvent** (0x0069EE20..0x0069EE9A): styles 1 and 3 → Processing 0xBF0BACE1; 2 → Question 0xE125EDA8; 0 and 4 →
  Unprocessed 0xA37DE6BE.
- **C15. CheckIfDone:** returns the inner action's Update (0x00561D4C..0x00561D50).
- **C16. Destructor:** CleanupAudioEngine, then the inner action's PrepForCompletion and delete (0x005613D8..0x00561436,
  0x0069ECA4..0x0069ED06).
- **C17. SetLocale:** sets external_language 0x384A9C0D, default english 0xC9A81C07 (0x00594574..0x00594648). external_name is
  never set (M1-058).

## D. Banks (census-derived)

- **D1. Events:** Processing 3205213409 → action 184678442; Question 3777359272 → action 126787062. Both are 0x403 Play on switch
  677877281. Unprocessed 2742937278 → 753742139 → 663268476. The Play actions' parameters are UNKNOWN (RECOVERABLE_GAP).
- **D2. Switch 677877281:** on external_language, default english, bus Cozmo_Robot_External 2476517424, -5 dB. English
  114912805, german 53142486, japanese 681861550, french 315546930.
- **D3. English 114912805:** a switch on cozmo_voice_processing: name 259796656, sentence 152131813, unprocessed 70808982.
  - Properties: Volume +4, Pitch +105, LPF 38, HPF 15.
  - Envelope 823049354 → Volume.
  - FX: Harmonizer 3538378849 (0x8A0003), Expander 2157284052 (0x6D0003), Compressor 2814082681 (0x6C0003), Parametric EQ
    4014558717 (0x690003).
- **D4. Name and Sentence leaves:** an external_name switch (default on → the WavePortal sound; mock → silence).
  - RTPC External_Process_Pitch → Pitch, from (-1, 0) to (1, +400 cents).
  - LFO 604656094 → Pitch with a zero curve.
  - The FX are inherited.
- **D5. Unprocessed 70808982:** -1 dB, Expander/Compressor/EQ with no Harmonizer, and two Pitch RTPC curves. How two curves on
  one property combine is UNKNOWN (RECOVERABLE_GAP).
- **D6. Other languages:** their own FX. French adds an envelope → Pitch.
- **D7. Bus Cozmo_Robot_External:** Cozmo_Voice_Master_Compressor 2313011259; parent Cozmo_Robot 1723505802. Routing as in M6
  (partly built).
- **D8. Plug-in bodies:** both are inside the engine, in ARM. Their parameter layouts are unread (RECOVERABLE_GAP); in the stack
  they are NOT IN STACK. The Compressor and EQ classes do exist in the stack.
  - Harmonizer 0x8A0003: create 0xAA32BC, SetParams 0xAA5BC4.
  - Expander 0x6D0003: create 0xABCC84, SetParams 0xABCF50.
- **D9. Resampling:** 22050 Hz plus the cents pitch → CAkResampler → Hijack at 22320 Hz in 744-byte chunks (M6-004, M6-015,
  M6-016, M3-011).

## Records

- **M14-006** (BLOCKED_EXTERNAL; unresolved: "the voice model and the plug-in are third-party binaries"): contradicted.
  WavePortal is Anki code in the engine, and Acapela runs under emulation with the engine's own licence. The record should be
  split into records along the rows above.
- **M14-013:** cites only a part of the component, which is consistent with its IMPLEMENTATION_GAP status.

## Open

1. Re-read D2-D7 from the bank bytes with an independent parser.
2. COMPATIBILITY_POLICY candidates: the Java CozmoTextToSpeech calls; the SetLocale locale; which tts_config platform block applies.
3. The Acapela speed strategy. Init's RUNNING poll makes the latency observable.
4. Who owns the Robot RNG seed and draw order (A6) and the M5 group choice inside C8.
5. Send D1, D5 and D8 back as extraction rows.
