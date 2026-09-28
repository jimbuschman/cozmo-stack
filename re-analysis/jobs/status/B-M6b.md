BLOCKED 2026-09-28 11:11 — the single batch is far too large for one job: the M6-022 voice/bus engine is not built, M6-002 is not built, and nothing is wired into the live audio path. The evidence is now complete enough to build, but the build is several jobs.

## What is committed and pushed (main)

| commit | scope |
| --- | --- |
| `8ddc45f` | Correction C12: the M6-022 callee bodies, the source classes, the bus-output tail, the modulator evaluator; five extraction reports; inventory re-approved; `fidelity.py --check` clean |
| `427d27a` | M6-022 engine skeleton (`WwiseVoiceEngine.cs`: output-device state/gates, SetOutputDevice, JNI poll, Perform order) and the M6-017 audio-thread lifecycle; verifier PASS; full suite 1705/1705, 0 skipped |

Both commits were pushed. `fidelity.py --check` passes; the pre-push gate ran the full suite green.

## Why it is blocked

The implementer stopped on a `MISSING` list (18 items) because C11 named the M6-022 callees but gave no bodies. The manager ran five bounded extraction passes, wrote them into the inventory as correction C12, and re-approved. The implementer then built only the device-state/JNI/Perform skeleton: the **voice pass, bus pass, per-voice DSP chain and four group members are exposed as seams and not built**, and M6-002 and the wiring were not attempted. The implementer's assessment (accepted): the native voice/source/bus/PBI object graph is a substantial design that cannot be done faithfully in the same session as the extraction.

The job's completion bar is "the robot's audio comes from the engine's path". It does not.

## What remains (each is a substantial job on its own)

1. **M6-022 voice/bus object model and DSP composition.** Transliterate the voice/source/bus/PBI classes and compose the per-voice chain from the existing modules: `0xA54F1C` (full state machine), `0xA44630`, `0xA548C0`, `0xA53134`, `0xA52D4C`, `0xA4FBEC`/`0xA45E9C`, the bus pass `0xA44C18`, `0xA4FEF8`, `0xA4F754`/`0xA4E974`, idle removal `0xA43F64`, the four group members (`0xA36AC4`, `0x9FF308`, `0x9D3C98`, `0x9E6D2C`) including the `0x9E2BD0`/`0x9E52F8` modulator evaluator, and the PBI flush `0xA38420`. The reports give the bodies; this is a large design task.
2. **M6-002 Vorbis packet driver.** Build the driver rows (entry `0x00AB3780`, inverse `0x00AB6B14`, framing `0x00AB7E40`, window/overlap `0x00AB3520`) into `WwiseVorbisNative.cs`, and the source render wrappers (`0xAB0448` streamed / `0xAB1550` in-memory, emit `0xA73490`); replace the NVorbis core on the production path. Residual: stream reset `0x00AB3978` (RECOVERABLE_GAP), native work-buffer ownership (RECOVERABLE_GAP).
3. **Live-path wiring.** Replace `WwisePlayback.Resolve`, `WwiseAudioSource.Produce`/`ToRobotRate`, `WwiseBusChain` and `WwiseVorbis.Decode` as the M6 plan describes; wire `WwiseRobotAudioPath` as the M5 adapter; keep the M5 `IAnimationAudioSource` seam, M3 framing and `WwiseSongRenderer`.
4. **M6-023 / M6-024**: the app audio-input dispatch and the bank/scene loading call sites.

Remaining explicit RECOVERABLE_GAP / HARDWARE_ONLY / UNKNOWN residuals (recorded in C12 and M6-022's `unresolved`): the bus metering DSP identity `0xA50044..0xA50FD0`, `0xA25FF8`, the `0x9E2BD0`/`0x9E52F8` NEON lane order, `0x9FD910`, the FX-slot object identity, the `[node+0x70]` device sub-object identity, the `0x0108D95C` callback-registry owner, the four group members' class names, and the sink/OpenSL values.

## Recommendation

Split B-M6b into separate jobs, in order:
- **B-M6b-1**: build the M6-002 packet driver + Vorbis source render wrappers (self-contained, no dependency on the engine).
- **B-M6b-2**: build the M6-022 voice/bus object model and DSP composition.
- **B-M6b-3**: wire the whole runtime into the live audio path (plus M6-023/M6-024).

Each should be its own job with its own verifier pass. The evidence for all three is already committed (C12 + the five reports in `re-analysis/research/20260928-B-M6b-*.md`).

## Operator listening check (for when the wiring lands)

Not written yet: the layer does not produce audio from the engine's path, so there is nothing to listen to. When B-M6b-3 lands, the check is: run an animation with a known SFX/voice event (`anim_bored_01` was the old hardware pass), confirm the sound is audible and matches the original app, then run the singing behaviour (M9) and confirm the song still plays.