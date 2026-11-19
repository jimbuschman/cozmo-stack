# SPEECH-LATENCY-1 handoff

SCOPE.md A: say a sentence in his voice. Built and measured offline; not a new robot pass.

## Result and change

Moved voice initialization from the first synthesis request to Robot construction when the initialized runtime audio owner is present. The provider remains owned by that Robot and its existing retired synthesis queue disposes it. Asset-free offline fixtures retain their lazy provider. Initialization errors remain visible as MISSING M14-006.

The original sentence's first AudioSample measured **17.923 s before the watchdog, 15.299 s with the watchdog alone, and 12.068 s with voice initialization moved earlier** on this PC. Final voice initialization took 2.094 s before the MCP call. This removes loading from the first command; it does not eliminate native synthesis time. All four texts, including the warm repeat, have identical native PCM **and complete robot-bound audio bytes** across all three builds.

What the operator should hear: the same Ryan/Cozmo voice, words, duration and audio content. The first utterance no longer pays for loading the voice. Expect a substantial synthesis pause (about 12 s for this sentence in this measurement), not immediate speech. The historic robot run's ~11 s pre-speech / 15.5 s total is a different run and is not a controlled performance comparison. No robot latency or audible completeness claim is made here.

## Complete production path and primary evidence

All native addresses refer to libcozmoEngine.so SHA256 02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1.

| Step | Stack path | Primary evidence / result |
| --- | --- | --- |
| Voice before requests | InitSpeechComponent -> GetSpeechProvider -> SpeechProvider ctor | Robot ctor 0x0050FDB4 calls TextToSpeechComponent ctor; stores +0x268 at 0x0050FDB8. Component ctor 0x0069E3FC constructs the provider; wrapper 0x0069F7FE constructs the impl. Impl 0x0069FA7C..0x0069FF3E resolves config/platform/locale, then calls Java **loadVoice synchronously at 0x0069FDCE**, with resource path, configured voice, engine user ID/password/license (0x0069FD3E..0x0069FDCE). Old stack deferred this work to the synthesis worker. |
| Host command | MCP say-text -> bridge -> SayEnglishSentenceAsync -> QueueEnglishSpeech -> direct SayTextAction | Host supplies style **3**, duration scalar 1, pitch 0, no trigger. Native SayText handler 0x0052AB00..0x0052AB40 and style constructor 0x00560B48..0x00560C96 define these fields; GetAudioEvent style 3 selects processing event 0xBF0BACE1 (0x0069EE20..0x0069EE9A). Existing app/audio readiness gates unchanged. |
| Async admission | CreateSpeech -> serialized worker | 0x0069E4C8..0x0069E80A: nonzero byte ID, state 1, Dispatch::Async at 0x0069E7C2. Lambda 0x0069F442 calls CreateAudioData; only **after it returns** does it take the mutex, find the bundle and set state 2 with complete data at 0x0069F468..0x0069F46A. Removed operation discards data; failure state 0 remains. |
| Text / controls | SpeechProvider.Synthesize -> AcapelaTts.Synthesize | 0x0069EF1C..0x0069EF52 appends '?' only for style 2. Provider 0x006A0210..0x006A057C selects 22050 Hz mono (0x006A02D4..0x006A02DA), replaces Cozmo at 0x006A03F6, computes speech rate at 0x006A042A, sets sample-append target at 0x006A043C and calls Java createAudioData at 0x006A0454 with text, rate, shaping, pitch; clears target after return at 0x006A0462. Shipped config/engine/tts_config.json android.en is Ryan / speed 50 / shaping 100; scalar 1 gives rate 50, Java pitch 100. |
| Native synthesis | shipped libacattsandroid.so under ArmMachine | Shipped sources/com/anki/cozmo/CozmoTextToSpeech.java:121..156: set rate, VOICESHAPE, pitch, speak, wait for isSpeaking false, stop. sources/com/acapelagroup/android/tts/acattsandroid.java:490..537 does stop/reset, starts nSpeak on its thread; stop:584..603 calls nStop and sleeps 100 ms. Stack worker performs nSpeak synchronously on its own thread, retaining the two stop waits. No rate/pitch/wait/deadline tuning. |
| Full-buffer conversion | native s16 -> float[] -> state 2 | 0x0069EFD2..0x0069F03E allocates for the returned sample count and converts every sample with f32 multiplier 3.0518509e-05 before the lambda marks ready. |
| Playback admission | SayTextAction.Init -> TakeSpeech -> runtime Acquire / Offer | 0x00561498..0x005614B8 waits for state 2 then PrepareAudioEngine. 0x0069E9A8..0x0069EB00 computes duration, gives the **whole** WavePortal container ownership at 0x0069EA52, clears operation data. It does not play from incomplete synthesis. |
| First / later samples | live event -> shared NativeWwiseVoiceSession -> native Wwise -> Hijack frames -> scheduler -> AudioSample | Direct action builds its keyframe/live animation at 0x0056153C..0x005615D8 after PrepareAudioEngine. Existing native host offers through 0x008DC1B9, posts through 0x009A6704, renders through 0x0099F130. Wwise renders while the platform sink consumes output and the streamer sends frames. Successful AudioSample tag 0x8E follows native budget gate 0x0057BF96..0x0057BFCC, 744 bytes / 22320 Hz. |
| Completion | native event end -> live animation end -> watcher -> bridge/MCP | Existing direct speech / shared audio lifecycle unchanged. Every measured call reached native result 0 and the MCP response. Fixture models firmware animation ACKs / played counters, not speech readiness or native event end. |

**Order conclusion:** Java nSpeak runs on a thread, but the engine worker waits for its complete return/sample buffer before state 2, WavePortal ownership and playback. The stack already has that order. Streaming incomplete TTS would change it. The concrete serialization difference fixed here is voice loading inside the first request instead of at component construction. Wwise rendering and robot streaming already overlap with playback.

## Measurements

Windows, Intel Core i5-11400 @ 2.60 GHz (6 cores / 12 logical processors), .NET 9.0.3, 2026-11-19. Same host, sequential sessions, actual native Acapela and Wwise. A paced silent sink replaces the PC speaker, no robot. InitScene is ready before calls. Timing begins immediately before the in-process MCP client's Send call and ends at the timestamp of the **successful** AudioSample send-gate observation.

Controlled before = current speech route plus **exact ArmMachine.cs from a3625bba = c6c3067d^**, blob 9da8ed354bcd32a423a3384e92e24bf1bf0963d8. Watchdog = blob 780389b29682357b0dbcb8ba8e0be320f83dcf26. Route/probes are held fixed to isolate the deadline change. SpeechOwner, SayTextAction, SpeechProvider and TextToSpeechComponent production behavior had no intervening changes since that parent. Final adds initialization fix. The old file was restored after its build/test exited and is not in the diff.

Single runs, not percentiles or promises. First row before/watchdog has a cold provider; later calls are warm. All final calls use the initialized provider.

| Sentence | Before hook | Watchdog only | Final (voice loaded at initialization) | Samples / chunks |
| --- | ---: | ---: | ---: | ---: |
| Hello! Look how tall I am! | 17.923 s | 15.299 s | 12.068 s | 83532 / 115 |
| Hi. | 3.953 s | 2.785 s | 2.931 s | 18808 / 33 |
| I am ready to play. | 10.193 s | 7.220 s | 7.591 s | 51088 / 74 |
| Hello there. It is a good day to play together. | 21.967 s | 16.306 s | 16.751 s | 114372 / 155 |
| Hello! Look how tall I am! (warm repeat) | 16.031 s | 12.411 s | 11.656 s | 83532 / 115 |

Original sentence phases (named intervals; tiny transitions between worker/provider/text boundaries are not collapsed):

| Original sentence phase (ms) | Before | Watchdog only | Final |
| --- | ---: | ---: | ---: |
| MCP dispatch -> queued | 35.708 | 38.580 | 61.007 |
| Queue -> worker | 1.000 | 1.073 | 1.940 |
| Provider / voice load | 2796.303 | 2169.607 | startup |
| Text normalization | 0.008 | 0.007 | 0.013 |
| Native settings + pre-stop / 100 ms | 101.976 | 108.203 | 114.224 |
| Native Acapela nSpeak | 14678.690 | 12670.729 | 11584.359 |
| Post-stop / 100 ms + copy | 112.921 | 116.041 | 111.397 |
| PCM hash observer + float conversion | 0.597 | 0.573 | 1.471 |
| Action poll / handoff | 30.412 | 23.957 | 21.116 |
| Native WavePortal offer | 4.795 | 1.333 | 4.368 |
| Wwise configure / post / render -> first Hijack | 148.017 | 156.631 | 155.625 |
| First Hijack -> successful send | 10.134 | 9.801 | 8.860 |

Wwise interval includes configuration, posting, worker scheduling and rendering until first actual Hijack frame. Native output continues during playback. Raw JSON also records explicit Render wrapper durations; these **exclude ServiceOutput**, are not complete native rendering cost, and are not used as a CPU total or added to wall intervals.

## Unchanged samples and regression

| Sentence | Native s16 SHA256 | Robot audio SHA256 |
| --- | --- | --- |
| Hello! Look how tall I am! | 7D65C74AE9D975D21A6DE5D006149223F8A539EF42A025804563440CBF7A4E02 | DDA175D2DC8A6AB4631B904959FDA0F777FBB0D1A15442937B4D4D8E24749E2C |
| Hi. | C8415A6A426366E59000AB59572D05CFE8A5B6E63D925B08956F47B86BFDDFA3 | CEAC2181FF51576B7F66215DE39F9C3F2FAEE7FC96D013EFEB4EB3E3C798412F |
| I am ready to play. | B67578BB849603CA10B45CB2EBCDDCD9765D98D0D4CA6B4296D154100AA67A6B | 7B3546ACABAB81F9D5952E4D66A1A224A8FEDD8B6393A8B41EC5FD935423F64F |
| Hello there. It is a good day to play together. | BBEE4AAE7C992E61537508A9F90C19275F97D03FE4C82CDAC8738003F4BF692D | 68F95AEE5F8A39D8D29C5B28C9F48CA42B83DD48A61FB1D2FE573FA19029772C |

Every hash matches before/watchdog/final and the repeated original sentence. PCM hashes cover native s16 little-endian; robot hashes concatenate every AudioSample's 744-byte body through MCP completion. These are change-detection baselines from this route, not an independent oracle for the whole engine.

SpeechLatencyTests.ProfileRealMcpSpeechThroughFirstRobotAudioSample runs the real MCP / speech / shared Wwise / streamer route for four texts and a warm repeat. It asserts voice loading precedes calls and is reused, full PCM precedes WavePortal offer, first native output precedes successful send, native counts / PCM hashes / full wire hashes equal pre-watchdog baselines, and each MCP result succeeds. Its 100 s guard is harness liveness only; no performance threshold is treated as an engine requirement.

Enable COZMO_SPEECH_TIMING_REPORT=<absolute writable json path> and run the class with shipped libraries/assets. Probes are opt-in, read-only and exception-isolated; normal operation has no subscriber. Data: re-analysis/research/20261119-speech-latency-measurements.json. No hardware bundle is committed.

## Validation / limits

- Before, watchdog-only and final profiling runs: PASS (5 successful utterances each); normal builds: 0 errors.
- Fidelity check: PASS, 764 records. **No manifest status/evidence change**, no SCOPE/PROJECT_STATE settlement. Current M14-006 stays BLOCKED_EXTERNAL; M14-013 / M6-024 stay IMPLEMENTATION_GAP. Broad records are not promoted by a timing run.
- Full suite / final gate key: pending handoff-commit run.
- Robot / audible result: operator has not rerun; output identity is measured offline only.
