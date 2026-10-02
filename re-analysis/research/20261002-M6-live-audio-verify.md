# M6 live audio: Opus verification of Codex's check, and correction C30

- **Date:** 2026-10-02
- **Checks:** `20260930-M6-live-audio-check.md` (Codex)
- **How:** an Opus `cozmo-verifier` from the manager session disassembled every cited range with capstone and read
  the literals as raw words. It parsed the banks itself for D8, E2 and L8.
- **Adopted as:** correction C30 in `re-analysis/inventory/M6-wwise-bank.md`

## Verdicts

- **Codex's 23 claims against the Sonnet extraction:** all confirmed in the binary. All 23 were **already corrected**:
  - by `20260929-B-M6b-4-citation-check.md`, which C23 adopts by reference;
  - by C23.1, C24.1 and C24.8;
  - by C28 (W2, W3, L5, R1) and C29.3.

  Codex judged them against the report, not against the later corrections.
- **Codex's 10 claims against the C#:** 7 confirmed (C24.3, C24.4, C24.5, C25.6, C26.4, C27.7, C29.6). 3 partly right:
  - **C24.2** is a visible seam, not a hidden one;
  - **C24.9**'s fields go unset, but nothing audible changes on shipped data;
  - **C28.6** belongs to the music PBI path, not the Sound-path limiter.
- **"Wired only in tests":** confirmed, and wider than Codex said. No production code constructs `WwisePlaybackBridge`,
  `WwiseVoiceLinker`, `WwisePlaybackLimiter`, `WwiseVoiceBusPass`, `WwiseEventRuntime`, `WwiseFrameDriver`,
  `WwiseVoiceEngine` or `WwiseRobotAudioPath` (`WwiseRobotAudioPath.cs:206` says "Not wired"). Every runner uses the
  direct-decode `WwiseAudioSource`. The gap stays visible: M6-016, 017, 022, 025 and 026 are IMPLEMENTATION_GAP.
- **New, found by the verifier:**
  - **N1, NotReadyCheck's rounding.** `WwiseVoiceLinker.cs:833` uses `MathF.Round(AwayFromZero)`. The engine (0xA54518..0xA54564)
    adds ±0.5f and truncates with `vcvt.s32.f32`. The two differ, for example at 0.49999997f and at odd values from
    2^23 upward. The C# also returns 0 for a null source, where the engine dereferences it.
  - **N2, the C2 range term is f64.** The divisor is the double 0x41DFFFFFFFC00000; Codex's 0x4F000000 is wrong.
  - **N3, the check's own A9 correction is inexact.** Replaced by C30.2.
  - **N4, C24.2 and the C29 residuals contradict each other** on 0xA54A30.

## C30 rows

C30 replaces or extends earlier rows only where the source says something new.

| # | earlier text | the source says | citation |
| --- | --- | --- | --- |
| C30.1 | C23.1 | (a) on result 1 with `+0x1D8 < 0` (`0xA54578 bge` not taken), `0xA54580` runs and the call returns 1. (b) `0xA54580` returns at once when source `[+0x10]` bit1 is set (`0xA54584..0xA5458C`); otherwise it sets `voice+0xE8 \|= 1` and calls `0xA0428C(mgr=[GOT], [pbi'+0x134], 0x9BD138(pbi'))` with pbi' = `[voice+8]`; a null `[voice+8]` reaches the deliberate fault (`udf`) at `0xA545D8..0xA545DC`. (c) The window is `trunc_s32((float)(u32)(([0x108D90C+0x1C]+1) * u16[0x1052440]) * [pbi+0x164] + (product > 0 ? 0.5f : -0.5f))`, with 0.5f = 0x3F000000 and -0.5f = 0xBF000000. (d) The arguments are `[pbi'+0x1DC]` and `[pbi'+0x1E0]` | `0xA544C4..0xA545DC` |
| C30.2 | C23 item 7 A9, and the check's correction | **The note-off dispatcher `0xA3E688`.** The list-A replay (code 1) skips an entry whose `[e+8]->vt+0x10()` returns 0xB (`0xA3E6F4..0xA3E71C`). It then walks list B `[[r5+8]+0x2C]` (`0xA3E738..0xA3E7D8`): code 1 is skipped; otherwise, if `[pbi+0xE0] != 0`, it calls `node->vt+0xA8` with a stack block built from `pbi+0x1E5/+0x1E6/+0x1E8/+0x1C`, `[r5+0x14]`, `[r5+0x18]`, the constant 3 and `[r8+0x14]`. **`0xA3E920`, per entry:** first `pbi+0x38 = [ctx+0x18]` when `pbi+0x34 == 0`, else `0x9E805C(pbi+0x34, [ctx+0x18])`. If `[ctx+0xC]->vt+0x10() != 0`: code := 3, `pbi+0x1F8 = -1`, and `0xA01280(pbi, &{0,4,0}, r2 = that result)`. Otherwise code 2 calls `pbi->vt+0x1C(pbi, [ctx+0x10])`; code 3 stores `[ctx+0x18]` to +0x1F8 and calls `0xA01280` with r2 = (`[ctx+0x18] == -1`); other codes are skipped | `0xA3E688..0xA3E82C`; `0xA3E920..0xA3E9F4` |
| C30.3 | C23 2A A10 | `0x9A080C(dB, level)`: returns 0x1F with no store when dB < 0xC2C0999A or dB > 0. Returns 1 with no store when `level > [0x105241C]` (signed; initial value 3). Otherwise it stores `[0x105241C] = level` **before** `powf`, and computes `s16 = dB * 0x3D4CCCCD`. The fast term is 0 when `s16 < 0xC2140000`, else the polynomial with 0x4BD49A78, 0x4E7E0000, 0x3EA67F46, 0x3CAA70DE and 0x3F272DDB (non-fused `vmla`). It stores the raw dB. The threshold is `powf(0x41200000, s16)` when that is greater than the fast term, else the fast term. STMG passes level 2 (`0x9B0B3C`) | `0x9A080C..0x9A0908` |
| C30.4 | C23 2C C2, and the check | The range term is computed in **double**: the 64-bit LCG `s = s * 0x5851F42D4C957F2D + 1` (`0x108D868`); `f = (f64)(s32)(hi>>1) / 0x41DFFFFFFFC00000`; `r = trunc_s32(0x3FE0000000000000 + f * (f64)(s32)(max-min))` with non-fused `vmla.f64`. The result is min + r, or just min when max == min, then `sxth`. The LCG draws only when min != max | `0xA1E32C..0xA1E3A8` |
| C30.5 | the check's 5.7 correction | The second parentless-bus store also writes `[0x108D9B0+0x14] = -1` (`0x9C4054`, `0x9C4060`) | `0x9C4030..0x9C4064` |
| C30.6 | C23.2 / row 21 | The aux path `0xA43434` creates no connection when the voice already has one whose +0x30 is that line (`0xA43514..0xA43528 → 0xA435E4`). It still stores `+0x1CC \|= 1` first | `0xA43504..0xA43534` |
| C30.7 | C1.3 (check) | `0xA024B8` returns at once if `params+0x114 == node` or `params+0x84 != 0`. If the new bus from `0x9F4BB8` differs from a non-null `params+0x11C`, a reset runs first: +0xA8 and +0xB0 bits 0-1 cleared; +0x90 = 0x3F800000; the words at +0x8C, +0x94..+0xA4, +0xAC, +0xB4..+0xBC, +0xE0, +0xEC, +0xF4..+0x104 and +0x114..+0x11C zeroed; 16 bytes at +0xC0 and at +0xD0 zeroed; bytes +0xE4 and +0xE7 zeroed; the tenth argument is then 0 | `0xA024B8..0xA025C8` |
| C30.8 | C24.2 against the C29 residuals | **Manager decision (2026-10-02):** C24.2's reading of 0xA54A30 has not been checked by Opus. The C29 residuals therefore stand, and 0xA54A30 stays RECOVERABLE_GAP until the builder's verifier confirms or corrects C24.2 | `M6-wwise-bank.md` C24.2 vs C29 |

## What the C# needs (no status rises)

- **M6-025, `IWwiseVoiceSource.StartStream`** (`WwiseVoiceBusEngine.cs:94`) returns the raw `vt+0x28` int and takes
  `([owner+0x1DC], [owner+0x1E0])`. AddSrc (`WwisePlaybackBridge.cs:820-834`) passes any code other than 1 or 0x3F
  through to step 8 unchanged. `NotReadyCheck` (`WwiseVoiceLinker.cs:818-838`) uses the same arguments and C30.1(c).
  The two tests that encode a bool must be rewritten from the binary.
- **M6-025, the linker:**
  - it owns the C24.3 Init stores and results, leaving only bus `vt+0x98(3)` and the FX holder as seams;
  - it owns the `0x9EA23C` scan, append and remove, with the seam narrowed to the `0xA22A3C` build;
  - `InitVoiceA54A30` follows C30.8.
- **M6-022/M6-025, `WwiseConnectionDescriptor.Reserve`:** it can return 2, through the same allocation-failure hook
  as the limiter and the bridge.
- **M6-022/M6-026, `WwiseVoiceBusPass.VoicePass`:** it calls `0x9D3CC0` (WalkPendingVoices), `0xA43D24` and `0xA39564`
  (PerFrameA39564) through required collaborators. An unread body throws; it is never skipped.
- **The music PBI, `0xA381F4`** (M9, or a new record; not M6-026):
  - its arguments are `(priority s17, out *r5)`;
  - early returns at `0xA38234`/`0xA3825C` against the 1.0f limits at `[G'+0xC]`/`[G'+0x30]`;
  - it writes `*out = 0x21`;
  - it returns 1 when `[G+0x48]+1-[G] <= u16[G+0x2C]`;
  - otherwise it calls `0xA37100(&G+0x20, max, prio, 0, 1, 1, &{0}, 2)`.

## C30.W: production wiring

**The engine's path:**
1. An Anki audio keyframe goes to RobotAudioClient, which posts the event on game object 7.
2. The action executes: `0xA62A1C` Play, then `0xA379D8`, with the limiter `0x9ED2CC` and the PBI.
3. `vt+0x14` → `0xA4304C` → AddSrc `0xA558AC` → `0xA42DEC`, or the pending list.
4. Each audio-thread Perform runs, in order:
   - `0xA36AC4`;
   - `0x9FF308`;
   - `0x9D3C98` (`0x9D3644`, then `0x9D3864` → `0xA431A8`);
   - `0x9E6D2C`;
   - `0xA57FF8` → `0xA44D4C` → the voice pass `0xA44948` (`0x9D3CC0`, `0xA43D24`, `0xA39564`, the voices, the buses);
   - `0xA38420`;
   - tick++.
5. Robot_Bus_1 goes to the Hijack.

**The C# host:** `WwiseRobotAudioPath` (M6-016) owns one composition, built once:
- `WwiseEventRuntime`, whose `PlaybackBridge` is a `WwisePlaybackBridge` holding the `WwisePlaybackLimiter` and the
  `WwiseVoiceLinker`;
- `WwiseFrameDriver`, which renders through a `WwiseVoiceEngine` over a `WwiseVoiceBusPass` and its collaborators.

That host is the `IAnimationAudioSource` set on `CozmoAnimations.AudioSource`. It replaces the direct-decode
`WwiseAudioSource` on the robot path.

**Until then:** no test may cite M6-025 or M6-026 as covering the live path.
