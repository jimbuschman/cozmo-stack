# SPEECH-ERASE-READY-1

## Say a sentence

MCP `say-text` admits the default direct SayTextAction, style 3, duration scalar 1 and pitch 0 (`0x0052AB00–0x0052AB40`, constructor `0x00560B48–0x00560C96`). The Robot constructs its provider at `0x0050FDB4 → 0x0069E3FC`, and the supplied English voice comes from shipped `tts_config.json`. CreateSpeech assigns a nonzero operation ID and dispatches synthesis at `0x0069E4C8–0x0069E80A`. The configured Acapela voice produces the complete PCM buffer at `0x006A0210–0x006A057C`. The worker moves its converted float buffer to ready at `0x0069F442–0x0069F46A` after conversion at `0x0069EFD2–0x0069F03E`.

SayText waits for ready at `0x00561498–0x005614B8` and gives Wwise the whole buffer at `0x0069EA52`. The direct command builds the live procedural animation and style-3 sound event at `0x0056153C–0x005615D8` and `0x0069EE20–0x0069EE9A`. The streamer sends budgeted 744-byte audio chunks at 22,320 Hz through `0x0057BF96–0x0057BFCC`. [SPEECH-LATENCY-1](HANDOFF-SPEECH-LATENCY-1.md) traces the provider, WavePortal and native renderer. The fixture models the platform sink and firmware play counters and acknowledgements.

The archived command is request 48 in `20261119-182433-LLM-SESSION` (November 20 UTC), 574276.574–587279.670 ms. It returned SUCCESS. The archive lacks its external SayText `0x5A`. Repeating the captured MCP request through the current stack returned SUCCESS, emitted 119 robot audio chunks on the focused run and delivered E2G `0x5A` with SayText type 0x20, external tag 1 and defaultCompleted variant 6. The type table entry at `0x0053FEFA` selects defaultCompleted at `0x0053FF0E–0x0053FF14`. The SayText vtable returns the cached union at `0x0052B0A6`, and the watcher broadcasts it at `0x0053FAAA–0x0053FAB2`. The checker now tests variant 6 after the subaction result list. This replay tests the recorded input and current completion path, with fresh synthesis and simulated firmware.

M14-006/M6-018 diagnostics remain for missing provider, shared WavePortal or robot output at `0x0069FDCE` and `0x0069E9A8–0x0069EB00`. The animation catalog guard remains at `0x0056153C`. Canned name speech has a separate authored-trigger and archive admission path. The original archive has no MISSING entry in request 48’s window. The direct speech gates were available in the replay. The shared M8-023/M8-027 retained-subscription finalization marker at `0x0066350E/0x0066352E–0x00663560` was still emitted. The replay measures the typed and raw completion with that shared ownership gap visible.

## Erase an enrolled face

The MCP command sends the signed ID on G2E `0x89` (`0x0074D22E–0x0074D236`, `0x00658070`). Vision calls the recognizer under its gate at `0x00657A60–0x00657A90`. The exact enrolled key is looked up at `0x008677EC–0x00867866`. Removal visits the stored album keys in signed order, clears the current track and reverse mappings and removes the entry (`0x008666EA–0x00866B90`). The empty export queues album erase at 0x184000 ahead of enrollment erase at 0x183000 through `0x00657144–0x006573EA`. Vision initiates the save at `0x00657A94` and immediately packs E2G `0x57` as the same ID and an empty name at `0x00657A98–0x00657ABE`. App delivery precedes the local event at `0x00662526–0x00662552`. The MCP reply includes the native report and the separately captured pre-erase name.

The fixture restores the album and enrollment from the local `20261117-052405-AI-COMMANDS-HW-1-bdcbda` NV writes from 255000–256000 ms. It erases that recorded identity through MCP, receives the five-byte raw report and typed reply while NV is still pending, and acknowledges the two native erase requests in order. The recognizer is empty after erasure. The checker verifies that the report and the reply both refer to the requested ID, and that the raw name is empty. Missing original recognizer/NV owners and M14-011 serialization failure remain visible at the save/dispatch guards. The existing FaceErase tests also exercise missing ID, stale owner, NV error and a surviving enrollment. The new test checks successful erasure from actual recorded enrollment state.

## Operator rounds

- Ask Cozmo to say one short sentence in his voice, let the audio finish and save the session so the checker can match the native speech completion.
- Ask Cozmo to erase an enrolled face by its current ID and save the session with the matching `0x57` report.

Build `d2b0748b`. All 8,859 C# tests passed, including 34 focused tests with the local recordings. All 20 checker tests passed, and fidelity validation covered 765 records. Claude review and the new operator rounds remain pending.
