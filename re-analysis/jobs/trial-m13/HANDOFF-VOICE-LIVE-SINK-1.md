# VOICE-LIVE-SINK-1

## Native output and verified boundary

- 0x009EBCD0–0x009EBCF4 passes output device ID 2 (low word 2, high word 0). Kind 2 is descriptor word five at 0x009EBDA0–0x009EBDA8. 0x009EAAC8 → 0x00A1CCFC → 0x00A58E48 selects OpenSL. The host supplies the checked platform geometry, 48 kHz and 1024 frames, at the cached Android frame-size input (0x0108DF94, writer 0x00A591B8, no-JNI default 0x00A578A0–0x00A578CC). The guest allocates and primes a four-block, 4096-frame ring (0x00A58D00–0x00A58D6C, 0x00A58F10–0x00A58F48).
- Per-machine ARM data imports supply separate IID pointer cells (0x0103FDD0–0x0103FDD8, 0x01040188–0x01040190). The bridge follows engine/object, mix, player, play and buffer-queue calls at 0x00A58DE0, 0x00A58C84, 0x00A58580, 0x00A586CC and 0x00A58700–0x00A587D0. Native requests BUFFERQUEUE with the Android locator, primes before entering Playing, and skips an unsupported optional Android configuration interface.
- A caller-supplied consumption releases one borrowed 4096-byte stereo s16 guest block and invokes the registered guest callback 0x00A582F8. The guest owns ring capacity, queue refill, underrun and wakeup decisions (0x00A5836C–0x00A5846C). Stable OpenSL demand reads available frames at 0x00A58054. The 0x009EBE94–0x009EBEC8 gate alone does not rebuild the device. The retry and rebuild are conditional at 0x009EC090–0x009EC0C8. The dummy path has distinct device state and demand (0x009E9564, 0x00A1CFA0).
- A single ARM owner feeds the explicit clock and invokes RenderAudio at 0x0099F130 with synchronous mode configured, reaching Perform at 0x009AF8A8. Native reads libc clock at 0x009AF8D4 and renders against device demand (0x009AF900, 0x009AF9BC). The checked OpenSL path runs in one device lifetime. Production thread and platform pacing remain separate.
- The local voice host transfers samples via WavePortal (0x008DC1B8), uses the checked object 7/bus 1 route, switch and pitch (0x0059962A–0x005999A4, 0x00561728–0x00561754), posts the Sentence event and sets playing-instance volume (0x009A6704, 0x0099FB14). Hijack buffers are copied before reuse at 0x008DC040. Graceful completion checks the matching EndOfEvent and the actual Hijack lifetime (0x008DBD98, 0x008DBDF8), including output after EndOfEvent. Native StopAll uses 0x009A6064.
- Term at 0x0099DCEC runs only after the completed host route. Player, mix and engine destruction comes through 0x009EAF90 and 0x00A58098–0x00A580FC. Cooperative joins validate the guest stop flags and semaphore signals (output 0x009EB0A4–0x009EB0C8, bank 0x00A34DC4). Abrupt release frees the host machine and records the missing active or faulted native shutdown.

## Robot-audio recipient

- WwiseRobotAudioPath retries state 0 until reset clears, then starts buffering without same-update fallthrough (0x00597C6C–0x00597C98).
- Its due check still reads the floored stream-creation offset after the event cursor is exhausted (0x00597CCC–0x00597D10).
- A missing frame returns without moving the cursor. A present empty frame yields 744 zero bytes, and cursor advancement is strict event-time less-than elapsed (0x00597DCE–0x00597E52, 0x00597E80–0x00597E8C). The existing buffer seam retains its prior contract by default.

## Files and verification

- ArmMachine.cs supplies per-machine data imports and non-replacing native observations. NativeOpenSlBridge.cs and NativeWwiseVoiceHost.cs own the checked native host route. NativeWwiseVoiceHostTests.cs loads local shipped banks and uses the synthetic Acapela sample fixture.
- WwiseRobotAudioPath.cs and WwiseRobotAudioPathTests.cs cover reset retry/no fallthrough, the post-event offset equality boundary, and missing versus empty data with strict cursor advancement. Focused recipient suite passed 24/24.
- An independent Python probe and the managed test matched 100 × 744 f32 samples, SHA-256 6e1ca4d3a3b57cf96505d5d83025f754f51c392e111a1ec17534b34f8c01cfe9. Fixed 21,334 µs and rational 1024/48000 input schedules agree in samples. The rational probe emits first frame at 42,666 µs, EndOfEvent at 3,328,000 µs and last frame/Hijack close at 3,349,333 µs. I reran the fixed-clock probe and verified the same hash and event tuple. The unchanged dummy reference has 92 × 744, SHA-256 a0a2b7665c88de12899af203a4c754d9e3b7c619483995ea871e0600c4c60533. No banks or shipped libraries added to git.

## MISSING

- The host proves a stable native sink under supplied clock and consumption inputs. It does not supply live platform callbacks or connect a live robot, and uses a scoped Sentence route. Public SayText and its direct/fitted/canned animation paths remain gated.
- Hijack create/copy/close still need the production RobotAudioController buffer owner (0x005984E8, 0x005985FC, 0x0059878C). Animation readiness, uint32 timing/wrap, producer quota and exact packet cadence remain open. Native streamer refreshes 14-audio / 8192-byte budgets and advances 33 ms per drained frame (0x0057C8E4–0x0057CABE, 0x0057CC6C). AudioSample 0x8E, AudioSilence 0x8F, start 0x9B, completion and abort 0x8D need this live join. Current robot audio playback is unchanged.
- Native active/faulted Term and stock concurrent audio thread timing remain gaps. No hardware or whole-record status claim.

## Proposed M14-006 record split for Claude

1. SayText metadata, action and trait selection, including undefined missing-intent handling (0x005602D8, 0x00560F94, 0x00561186).
2. Speech component operation lifecycle, provider synthesis and owner retirement (0x0069E4C8, 0x0069EF10, 0x0069F430, M14-013). Record the Java provider inputs, locale and platform configuration as their app-boundary policy.
3. WavePortal registration, mode 2, source ownership, pitch-buffer transfer and refill (0x00A7898C, 0x008DD636, 0x008DDC5A), with authored switch/RTPC and FX delivery identified separately.
4. Native output/clock/worker lifecycle under M6-017, M6-018 and M6-022. Record fixed geometry separately from consumption timing. The shipped renderer's Hijack frame lifetime links to M6-013 through M6-016.
5. Public SayText, animation/robot-buffer owner, packet budget, cancellation and delivery (0x00561488–0x00561754 and the recipient addresses above). No whole path settled by the oracle.

Reader workers requested gpt-6.1-sol medium. Builder and independent reviewer used configured defaults. Actual backends unobserved.

The independent review passed after one correction round for shutdown ownership. The final affected suite passed 51/51, including the unchanged dummy oracle and emulator safety. StopAll at step 24 produced 16 × 744 frames with independent hash 748ec3acaae0a1c3d23543efa27e1fb567cfafd8873bf3ca1fcbfe21e510e652. The first stop test used the dummy oracle's 14-frame expectation and was corrected against the independent real-sink run. Graceful and abrupt release tests exercise distinct ownership paths. Batch full gate pending.
