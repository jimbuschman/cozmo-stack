# SPEECH-EMU-SPEED-1 handoff

Release build on this PC, through MCP to the first successful robot-bound `AudioSample`:

| Phrase | Before (s) | After (s) |
| --- | ---: | ---: |
| Hello! Look how tall I am! | 8.981 | 2.913 |
| Hi. | 2.188 | 1.075 |
| I am ready to play. | 5.078 | 1.947 |
| Hello there. It is a good day to play together. | 10.999 | 3.879 |
| Session phrase, warm repeat | 9.431 | 2.748 |

Before was the detached `648aee88` Release build. The preceding Release candidate measured 2.963 s cold and 2.675 s warm for the session phrase. The final Release run spent 2.526 s of the 2.913 s cold interval inside nSpeak. Both builds matched each of the four SPEECH-LATENCY-1 native s16 and complete robot-audio SHA-256 baselines. Offline coverage includes the first send, full packet sequence and action completion. Live robot acceptance is pending.

The baseline profile counted about 55.6 million Acapela basic-block entries for the session phrase. A separate cold/warm translation probe counted 4,854 new Acapela edges versus nine on the repeat, with nearly identical 9.03 s and 9.01 s synthesis intervals. Roughly 3,166 managed imports took 11.2 ms within their delegates, including 5.5 ms in JNI/VM delegates. The invalid-memory hook did not fire in successful profiles. The app-shaped driver still runs both source 100 ms stop waits.

| Hot path | Pinned Acapela evidence and implementation |
| --- | --- |
| Double-precision filter | `libacattsandroid.so` SHA-256 `73c4db9455e1cc0235f731ab110194dbfb7aa44974af7ac74c11de382f135698`. `mglsadf3` at `0x000F2810–0x000F28AA` includes 6,692,040 visits to the `0x000F2870` inner block in the baseline profile. The host retains the separate rounded multiply and add/subtract and writes the source history and register state. Unsupported input, alias or FPSCR cases execute the guest instructions. [Unicorn VFP translation](https://github.com/unicorn-engine/unicorn/blob/2.1.4/qemu/target/arm/translate-vfp.inc.c#L1467-L1515) |
| Coefficients, filter taps and noise | `0x000E252C–0x000E254C` adds the coefficient vector. `butterdf4` `0x000F2B28–0x000F2B94` traverses its two filter stages. `0x000E2C76–0x000E2C9C` batches the same `0x000F2E4A–0x000F2E6E` integer seed update and filter calls before committing the original output, seed and delay state. |
| Queue and copying overhead | `ALF_Queue_read` `0x000E1936–0x000E19D0` and `ALF_Queue_write` `0x000E19D2–0x000E1A64` use the common two/four-byte memcpy case at PLT `0x0001F9A4`. Native memmove `0x0002C57A–0x0002C5AA` uses overlap-safe host copying. The private Thumb entries execute guest BX LR on success and the original displaced first instructions on fallback. Register batches reduce interop calls. [Unicorn register writes and translation exit](https://github.com/unicorn-engine/unicorn/blob/2.1.4/uc.c#L662-L681) |

The speech task at `0x0069E7C2` completes CreateAudioData at `0x0069F442` before marking its own operation ready at `0x0069F468–0x0069F46A`. The action polls ready at `0x00561498–0x005614B8` and gives WavePortal the whole buffer at `0x0069EA52`. Once that handoff occurs, the serialized synthesis queue can prepare the next accepted speech action while the previous buffer plays. Playback and voice synthesis use separate owners.

Direct instruction comparisons cover result registers, FP flags, history and seed bytes, overlap and fallback cases. Focused Release tests 12/12. Fidelity continues to use the existing M14-006/M14-013 and M6-024 provenance.

The complete Release run encountered the pre-existing Wwise compressor oracle failure. The isolated Release compressor test reports 146 of 5,346 differing cases on both `648aee88` and `df217472`, including the same first case and output words. Release supplied the speech timings and focused regressions. The standard Debug run supplies the complete suite gate.

Full standard Debug suite **8785/8785** on `df217472` in 47.24 minutes with local replays. Its instrumented speech check measured 3.196 s cold and 3.182 s warm for the session phrase, with the same PCM and packet hashes. The Release build is the configuration that reached the 3 s target. Gate `54b2690af9f253a41c0ed67c2f88cb32e029ef91`. Fidelity and diff checks passed.
