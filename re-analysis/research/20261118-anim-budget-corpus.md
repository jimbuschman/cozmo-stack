# Firmware-2457 animation budget measurements (2026-11-18)

Corpus: operator confirmed use the available copies on 2026-11-18. There are 28 bundle copies; 26 contain raw frames. Two older M3M4 bundles have no frames, so their occupancy is unknown, not zero. Only summary statistics and hashes are committed, never a hardware bundle.

Reproduce: python re-analysis/tools/measure_anim_budget.py <hardware-root> <output.json>. Numeric fixture: cozmo-stack/tests/Cozmo.Protocol.Tests/Fixtures/animation_budget_firmware.json.

Unique reliable sends are deduplicated across sequence wrap. Count serialized 8E..9B payload lengths; AudioSample/AudioSilence/End add a frame. F1 supplies signed played counters. All observed epochs start at zero and have no feedback decrease; each established capture has one connection response. Older omitted frames are explicitly camera-only. Unknown schemas, incomplete/reset epochs or nonzero initial feedback fail measurement instead of producing a guessed prefix.

## Distribution by capture

| Bundle | Peak bytes | Peak frames | BufferFull traces |
| --- | ---: | ---: | ---: |
| 20260924-112412-M1-LINK | 0 | 0 | 0 |
| 20260924-202748-CONTROL | 9000 | 12 | 1 |
| 20260925-061148-CONTROL | 7501 | 14 | 0 |
| 20260925-165740-CONTROL | 7500 | 14 | 0 |
| 20260925-165815-CONTROL | 7500 | 14 | 0 |
| 20260925-173447-CONTROL | 7500 | 14 | 0 |
| 20260925-180557-CONTROL | 7506 | 14 | 0 |
| 20261009-112625-M1-PACKED-ZERO | 0 | 0 | 0 |
| 20261012-062328-M3M4-HW | unknown | unknown | unknown |
| 20261018-170546-M1M2-HW | 0 | 0 | 0 |
| 20261019-182707-M3M4-HW | unknown | unknown | unknown |
| 20261020-074223-M5-HW-2cc1cf | 8133 | 15 | 0 |
| 20261025-084737-M13-DRIVEOFF-HW-760add | 658 | 15 | 0 |
| 20261115-162141-M15-CUBE-HW-7b0d18 | 871 | 14 | 0 |
| 20261116-110735-AI-COMMANDS-HW-1-108cc4 | 7910 | 15 | 0 |
| 20261116-111516-M15-CUBE-HW-5fe5cd | 938 | 15 | 0 |
| 20261116-112021-PERSONALITY-HW-1-cbcbad | 1283 | 15 | 0 |
| 20261116-113953-M15-CUBE-HW-4dbca5 | 1195 | 15 | 0 |
| 20261116-192850-AI-COMMANDS-HW-1-864071 | 8183 | 15 | 1 |
| 20261116-193450-PERSONALITY-HW-1-59ef28 | 8192 | 15 | 1 |
| 20261116-194237-M15-CUBE-HW-0cbdb7 | 8068 | 15 | 0 |
| 20261117-052405-AI-COMMANDS-HW-1-bdcbda | 8192 | 15 | 1 |
| 20261117-053040-M15-CUBE-HW-b22d48 | 8190 | 15 | 0 |
| 20261117-053448-PERSONALITY-HW-1-dcd596 | 8192 | 15 | 2 |
| 20261117-055713-M15-CUBE-HW-560b6a | 8189 | 15 | 1 |
| 20261117-121734-PERSONALITY-HW-1-77ccf7 | 8192 | 15 | 3 |
| 20261118-075011-LLM-SESSION | 0 | 0 | 0 |
| 20261118-075139-LLM-SESSION | 8192 | 15 | 2 |

## Every captured BufferFull trace

Times are raw frame atMs/t, not later engine-log delivery time. Trace-time occupancy may already include the firmware's flush/played feedback; it is not capacity. All listed 100-ms, 500-ms and 1000-ms pre-trace peaks agree.

| Bundle | Time ms | Raw args | Bytes/frames at trace | Pre-trace byte peak |
| --- | ---: | --- | ---: | ---: |
| 20260924-202748-CONTROL | 25299.735 | [652, 747] | 1500 / 2 | 9000 |
| 20261116-192850-AI-COMMANDS-HW-1-864071 | 115306.5493 | [2, 13] | 8129 / 11 | 8183 |
| 20261116-193450-PERSONALITY-HW-1-59ef28 | 139678.7173 | [703, 747] | 749 / 1 | 8178 |
| 20261117-052405-AI-COMMANDS-HW-1-bdcbda | 195743.6474 | [743, 747] | 745 / 1 | 8191 |
| 20261117-053448-PERSONALITY-HW-1-dcd596 | 96944.9809 | [56, 66] | 8152 / 10 | 8185 |
| 20261117-053448-PERSONALITY-HW-1-dcd596 | 415069.4668 | [4, 6] | 68 / 0 | 8152 |
| 20261117-055713-M15-CUBE-HW-560b6a | 32052.9325 | [63, 72] | 70 / 0 | 8176 |
| 20261117-121734-PERSONALITY-HW-1-77ccf7 | 117398.8271 | [14, 57] | 60 / 0 | 8188 |
| 20261117-121734-PERSONALITY-HW-1-77ccf7 | 222771.0482 | [4, 13] | 8182 / 11 | 8182 |
| 20261117-121734-PERSONALITY-HW-1-77ccf7 | 767000.966 | [744, 747] | 811 / 1 | 8153 |
| 20261118-075139-LLM-SESSION | 204245.4068 | [6, 13] | 11 / 0 | 8125 |
| 20261118-075139-LLM-SESSION | 230883.7433 | [707, 747] | 756 / 1 | 8151 |

## Selected policy

The smallest pre-error peak is **8125**. The largest observed counter value in a completely trace-free run below that conservative floor is **8117**: charger run 20261117-053040-M15-CUBE-HW-b22d48, frames line 16053 at 138853.7051 ms, 349618 streamed minus 341501 played, 14 frames ahead. Subtract **747** (native AudioSample 745 plus StartOfAnimation 2) for default **7370**. This does not infer firmware allocator overhead.

Clean runs also reached **8190**, overlapping failure-region peaks. Thus this is a conservative empirical guard, not a universal serialized-byte capacity threshold or proof of safety. Native 8192 remains available. No physical successful playback with the guard is claimed.

At the LLM tag-13 window, captured successful sends replayed as fixed offers through the actual C# buffer, with captured F1 feedback supplied, give pre-error peaks **8151 native / 7339 guarded**. Trace-time outstanding is 756 in both after the recorded flush input. Those feedback inputs do not predict that a newly guarded firmware run would flush or play identically.

The controlled native oracle substitutes only the ADD.W literal at **0x0057C706**. At the critical prefix (streamed 479685/5407, played 472279/5395), original C9 gives **786 bytes / 2 frames** and admits 745 bytes; policy C9 gives **0 bytes / 2 frames** and holds it. Native branches, clamps and send/counter code run from the shipped binary. No firmware allocator is emulated.
