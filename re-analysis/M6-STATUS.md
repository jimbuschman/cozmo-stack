# M6 Wwise runtime — working status (resume here)

Authoritative for the M6 implementation effort. Read together with:

- `re-analysis/inventory/M6-wwise-bank.md` (+ `.approved.json`) — the frozen M6 inventory.
- `re-analysis/workplans/M6-plan.md` — the record-by-record plan and the five batches.
- `AGENTS.md` — the process and fidelity rules.
- `PROJECT_STATE.md` — overall project state.

Nothing below supersedes the frozen inventory. Where implementation recovered a fact the frozen
rows did not contain, it is recorded in §7 and must go back to extraction for a new approval before
it is treated as settled.

## 1. Current git state

Base before M6 implementation: `2ad71a3` (merge `origin/docs/m6-plan`).

Committed on `main`:

| commit | scope |
| --- | --- |
| `7842392` | Batch 1: M6-001, M6-005, M6-018 (+ the verification-gap fix, §8) |
| `3827137` | M6-009 part: RTPC curve shapes and post-curve scaling |
| `8b64904` | M6-007: random/sequence step selection engine |

Working tree, **uncommitted and unverified** (M6-006):

- `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseAction.cs`
- `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseEventRuntime.cs`
- `cozmo-stack/tests/Cozmo.Protocol.Tests/WwiseEventRuntimeTests.cs`

The M6-006 worker reported 7 focused tests passing and `fidelity.py --check` passing, but the
manager had not re-run them or reviewed the diff when work stopped. **Verify before committing.**
Nothing else in the repo was touched.

## 2. Batch 1 (committed `7842392`) — M6-001, M6-005, M6-018

### M6-001 — bank/HIRC readers in the runtime field order

- RTPC parameter id is a Wwise varint (7 bits/byte, 0x80 continues, big-endian accumulation);
  `WwiseRtpc.ParamId` is `uint`. (`WwiseHierarchy.ReadVarint`.)
- A Sound source plug-in's `u32` parameter size **and its bytes** are consumed
  (`plugin & 0x0F == 2`).
- Positioning (`b0 & b3`), bus A/B/C conditional bodies, and the LayerCntr layer body **fail
  explicitly** rather than guessing an unrecovered length.
- The BKHD feedback flag (BKHD dword 3) is captured per object (`WwiseObject.FeedbackEnabled`) and
  adds 4 bytes to `NodeBaseParams` and to a bus.
- `ReadBus` rewritten to gapD D6.1 order: no ranged bundle, A/B/C, maxInst(10-bit), channelConfig,
  recovery ms, maxDuck, duck list (18 B each), FX, mixer id + byte, RTPCs, states.
- `ReadBlend` uses the real LayerCntr outer order (NodeBase, children, u32 layer count, u8); a
  non-zero layer count throws.
- **STMG defaults**: new `WwiseStmg` parses the Init.bnk state-manager chunk (see §7) and exposes
  the RTPC default table (`WwiseStmg.DefaultOf`); `WwiseSoundLibrary.Stmg` returns it.

### M6-005 — name hash

`WwiseHash.Of` bounded to the native `GetIDFromString` copy: at most `0x103` bytes **including the
NUL**, i.e. at most `0x102` string bytes hashed (`WwiseHash.MaxBytes = 0x103`). C-string (NUL) and
byte-oriented; no Unicode/culture semantics added.

### M6-018 — internal format policy

New `WwiseRuntimeSettings`: `MixRateHz = 48000`, `SamplesPerFrame = 1024`, `MsPerFrame = 21`,
`QuarterFrameMs = 5`, `LpfChunkSamples = 128`. A **compatibility policy** (the original derives
these from the phone: `min(native, 48000)` and hardware rounding). Kept distinct from
`CozmoAudio` (22320 / 744); neither was changed.

### Files (Batch 1)

`WwiseHierarchy.cs`, `WwiseBank.cs`, `WwiseHash.cs`, `WwiseSoundLibrary.cs`, `WwiseStmg.cs` (new),
`WwiseRuntimeSettings.cs` (new), plus call-site casts in `WwiseAudioSource.cs`, `WwiseMusicStream.cs`,
`WwiseSongRenderer.cs`. Tests: `WwiseRuntimeTests.cs` (new), `WwiseTests.cs` (SoundDirs fix),
`WwiseModulatorTests.cs`, `WwiseMusicTests.cs`.

## 3. M6-009 part (committed `3827137`) — RTPC curves and scaling

`WwiseRtpc.Evaluate` implements every recovered interpolation shape (4 linear, 9 constant, 0 Log3,
1 Sine, 2 Log1, 3 InvSCurve, 5 SCurve, 6 Exp1, 7 SineRecip, 8 Exp3); `WwiseRtpc.ApplyScaling`
applies the scaling byte (2 dB via ±20·log10(1∓|y|) with the ±764.616 clamp, 3 = 10^y,
4 = 10^(0.05y)); `EvaluateScaled` combines them. Unknown codes fall back to linear and set
`reduced`.

Tests `WwiseRtpcTests.cs` (9) pin the event_volume cross-check (1 → 0 dB, 0.5 → −3.01 dB,
0 → −764.6 dB). **Still open in M6-009**: the scoped value store precedence
(playing id → game object → root → STMG default), sum/product accumulation, and bus empty-key
semantics — those need the live runtime (M6-006).

Note: the inventory's Sine prose says `sin(t·π/2)` while its cited fast polynomial evaluates
`sin(t)` on [0,1]; the runtime result it records (0.5 → −3.01 dB) requires the π/2 form, which is
what is implemented. Recorded here as a transcribed-evidence discrepancy.

## 4. M6-007 (committed `8b64904`) — selection engine

New `WwiseSelection.cs`, self-contained, **not yet wired into production**:

- `WwiseRng`: global LCG `s = s*0x5851F42D4C957F2D + 1; Next() = (u32)(s>>32) >> 1`. Seed is
  `time(NULL)` in the original and is not reproducible; constructor takes an injectable seed and
  defaults to the current Unix time — **COMPATIBILITY_POLICY, not source fidelity**.
- `WwiseSelectionState`: random state (`remaining = counter = len`, `total = remainingWeight =
  50000·len` or the playlist total with weights; played/blocked bitsets; avoid list) and sequence
  state (`forward = 1`, `index = −1`).
- Standard vs shuffle eligibility, k-th-eligible pick, weighted running-sum pick, avoid windows
  `min(avoid, len−1)` (standard) / `min(max(avoid,1), len−1)` (shuffle), and the linear playability
  probe. Sequence wraps or ping-pongs (bank bit 2); first play 0.
- `WwiseSelection`: shared state when bank bit 4 is set (every shipped Cozmo container), otherwise a
  sorted per-game-object map.

Tests `WwiseSelectionTests.cs` (20), with an independent BigInteger LCG oracle.

Explicitly unresolved in the worker's implementation (documented in-code): the probe's post-pick
bookkeeping (§3.5 names only helper `0xA08694`); the zero-denominator case (§3.6b/c); length-1
bookkeeping; continuous containers (§3.3, out of scope).

## 5. M6-006 (UNCOMMITTED) — queued event/action control path

New `WwiseAction.cs` and `WwiseEventRuntime.cs`; tests `WwiseEventRuntimeTests.cs`. Parses the full
Action layout (gapA 1.7: id, u16 type, u32 target, u8 isBus, property bundle, ranged bundle, then
Play/Stop/Seek type-specific params per 1.9) and models: PostEvent → atomic playing id + type-1
queue message; message pump → ExecuteEvent; action-order walk with the `(type & 1)` object-scope
skip; `EnqueueOrExecute` (delay samples → frames with sub-frame remainder; launch tick; FIFO by
launch tick among equal ticks; frames == 0 executes now); GetDelay (prop 0x0F, else table default,
+ ranged 0x0F + LCG draw); Play probability (prop 0x11) and target/fade/initial-delay recording;
gapD D5.x Stop/Seek dispatch; game-object lookup. `AK_INVALID_PLAYING_ID = 0`.

Focused test result as reported by the worker: **7 passing** (Action layout, scope skip, zero-delay
on first frame, delayed FIFO, frame/remainder split 4800 → 4 frames + 704, missing event → id 0,
shipped action exact consumption). **Not independently verified.**

Worker-reported unresolved items (all documented in-code): `0xA04F54` EndOfEvent/callback internals;
pending-list cap; the 0x0503 one-frame look-ahead; the delay table default; the ms→samples rate
global identity (substituted `WwiseRuntimeSettings.MixRateHz`); ranged 0x0F f32→samples step;
`PlayInternal`, fade-in application, `0x9EE454/0xA616BC`; fade-in ranged draw `0xA61110`;
initial-delay RTPC `0xA11590`; Stop's `0xA79F08`; Seek's value formula; switch-container resolution
(gapA 4.1, outside this task); game-object registration API; PostEvent callback registration failure.

Worker-reported ambiguities: gapA 1.9 vs gapD D5.2 on Stop's zero-byte reader (both consume zero
bytes); gapA 1.9 bank order vs gapD D5.3 object offsets for Seek (different layers); gapA 1.6
"64-bit division" vs an f32 ranged bundle; gapD D1.4's 0x0503 note vs the inventory's own
correction (continuous containers all use it).

## 6. Verification infrastructure (committed `7842392`)

The independent audit was right: `WwiseTests` sweeps located banks via a `sound_meta` directory that
the unpacked OBB does not have, so `EveryShippedBankParsesExactly` (and every sweep using
`SoundDirs()`) returned early and reported a pass without loading anything.

Fixed:
- `WwiseTests.SoundDirs()` now falls back to the media directory when `sound_meta` is absent, so the
  older sweeps actually run against the banks inside `AudioAssets.zip`.
- New deterministic `WwiseRuntimeTests.TheShippedBanksLoadAndEveryCoveredObjectConsumesExactly`
  loads the six banks through `WwiseAssets` (the working loader), asserts 6 banks / v120 / 835
  events, and asserts every covered hierarchy object consumes exactly. It skips only when no
  unpacked assets exist; a missing/unreadable bank fails.
- Confirmed real: all 125 `Wwise`-filtered tests pass (~1.5 min), including the now-active codec and
  media sweeps.

The `M6-shipped-bank-verification.md` note in `re-analysis/workplans/` was produced by the other
(independent) investigation and is intentionally left untracked.

## 7. Recovered evidence NOT yet in the frozen inventory — STMG reader 0x9B0B14

Recovered by disassembling libcozmoEngine.so (ARM mode; see §9). The frozen rows give the outer
order and the parameter table but not the middle layouts; this fills that gap. **Send to extraction
for a new approval before treating it as settled.**

Init.bnk STMG body is 1095 bytes:

1. `f32` volume threshold (−80.0)
2. `u16` max voices (256)
3. `u32` state-group count (6); each: `u32 id, u32 default-transition-ms, u32 n,
   n × {u32, u32, u32}` (all six shipped groups have n = 0, so 12 bytes each)
4. `u32` switch-group count (4); each: `u32 id, u32 rtpc, u8 flags, u32 n, n × {u32, u32, u32}`.
   The third field is a **1-byte read** (native `mov r2, r0` after the success check); the chunk
   lands exactly on the parameter table only under that reading.
5. `u32` parameter count (37); each: `u32 id, f32 value, u32 ramp-type, f32 up, f32 down,
   u8 built-in` (21 bytes)
6. `u32` post-count A (0), `u32` post-count B (0) — their entry bodies are unrecovered; a non-zero
   count is refused.

Offsets in the shipped chunk: counts at 0/6/82/306; params 310..1087; trailing 8 bytes 1087..1095.
`event_volume 0xD2687048` is parameter index 30 with value 1.0; `robot_volume 0x637C1240` is index
12 with value 1.0; both ramp type 0.

Native entry point 0x9B0B14; parameter store `0xA0F594` (entry+8) and ramp `0xA0F5AC`; dispatch
from 0x9B7864 (`cmp r3, sl`, sl = 'STMG'). Per gapF 1.2/1.3.

Also recovered while reading the same function: the state-group and switch-group item triples are
read but their field *meanings* are not named in the rows (kept raw in `WwiseStmg`).

## 8. Batch order and next steps

Plan batches: 1 (done) → 2 (M6-006/007/008/009/017) → 3 (M6-003/004/010/011/012) →
4 (M6-013/014/015/016) → 5 (M6-002).

Done: M6-001, M6-005, M6-018, M6-009 (curves/scaling only), M6-007. M6-006 implemented but
unverified/uncommitted.

Next:
1. Verify M6-006 (run `WwiseEventRuntimeTests`, build, `fidelity.py --check`, review the diff for
   invented behavior), fix failures, commit.
2. M6-017 (audio-thread frame order: messages → pending drain → render/bus/notification flush →
   tick) and M6-008 (continuous containers). Both are heavily RECOVERABLE_GAP; the continuous bodies
   0xA0ABC4 / 0xA09F04 and several timing details are not read. Implement the established parts and
   leave the rest explicitly unresolved.
3. Finish M6-009's value store/accumulation once M6-006's runtime provides scopes.
4. Batches 3–5.
5. Final pass: re-run the whole suite once, update `re-analysis/fidelity_manifest.json` statuses and
   add `// fidelity:` tags for every settled record, then `fidelity.py --check`.

Do not wire the new runtime into `WwisePlayback` / `WwiseAudioSource` / `WwiseSongRenderer` /
`AnimationScheduler` until the live voice/mixer (Batches 3–4) exists, or M9 singing regresses.

## 9. Tooling

- APK: `C:\Users\JimBu\Downloads\com.anki.cozmo_3.4.0-1204_plus_OBB\com.anki.cozmo.apk`;
  extracted engine:
  `C:\Users\JimBu\Downloads\com.anki.cozmo_3.4.0-1204_minAPI21(armeabi-v7a)(nodpi)_apkmirror.com.apk_Decompiler.com\resources\lib\armeabi-v7a\libcozmoEngine.so`
  (17,139,336 bytes).
- Unpacked OBB used by the tests: `re-analysis/obb/assets/cozmo_resources/sound/AudioAssets.zip`
  (141 MB) contains the six banks, `SoundbanksInfo.xml`, the `.txt` definition files and the media.
- Disassembler: `re-analysis/tools/arm_disasm.py` (`pip install capstone lief`; Wwise is ARM mode,
  not Thumb). Example: `python re-analysis/tools/arm_disasm.py 9B0B14 9B0FB0`.
- The extractor scratch folder named in the inventory (`...\scratchpad\extract\M6-gapF\stmg.py`) is
  **gone**; §7 was recovered by disassembling 0x9B0B14 directly.

## 10. Intentionally untracked (do not commit)

`cozmo-stack/re-analysis/obb/`, `cozmo-stack/vision-m3-nv.json`, the four
`re-analysis/acceptance/hardware/20260925-*-CONTROL/` bundles, and
`re-analysis/workplans/M6-shipped-bank-verification.md`.

## 11. Focused test commands

```
cd cozmo-stack
dotnet build src/Cozmo.Robot/Cozmo.Robot.csproj -v q --nologo
dotnet test tests/Cozmo.Protocol.Tests/Cozmo.Protocol.Tests.csproj --nologo -v q --filter "FullyQualifiedName~Wwise"
# then, from the repo root:
python re-analysis/tools/fidelity.py --check
```
