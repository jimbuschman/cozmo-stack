DONE 2026-09-28 23:45 — B-M6b-2 built the M6-022 live voice/bus engine object model and its DSP composition. Verifier PASS over the whole diff and every fix round; `fidelity.py --check` exits 0; full suite 1751 passed / 0 failed / 0 skipped (AssetPresenceTests green).

## Commit

- `8b369e6` — the whole job: `WwiseVoiceBusEngine.cs`, `WwisePerformGroupMembers.cs`, `WwiseModulatorEvaluator.cs` (new), `WwiseVoiceEngine.cs`/`WwiseBusLifetime.cs` (extended), `WwiseVoiceBusEngineTests.cs` (new), the M6 inventory corrections C15..C19 with re-approval, the manifest and `FIDELITY_GAPS.md`, and four extraction reports. 14 files, +5374/-5.

## What was built (all cited to C11/C12/C15..C19)

- The voice pass and pre-pass (`0x9D3CC0`, `0xA43D24`, `0xA39564`), the voice-list walk, and the per-voice state machine `0xA54F1C` with its callees (`0xA4C584`, `0xA022E8`/`0xA0228C`, `0xA01768`, `0xA4B93C`, `0x9D4228`/`0x9D4108`, `0xA54A30`), including the four parameter ramps, the P2F==0 branch and the E8-gate continuation.
- The per-voice DSP chain composed from the existing modules (resampler, voice filter, gain, mixer, bus FX, Hijack), and `0xA4BC58`'s connection gain/format update with its outputs (`Run2E`, `P2F`, the four minima).
- The bus pass `0xA44C18` with `0xA4FEF8`/`0xA4F754`/`0xA4E974`/`0xA4D994`/`0xA4FD84`/`0xA4F9E0`, the device routing `0x9E9E78`/`0x9E9F08`, and idle removal `0xA43F64`.
- The four Perform group members `0xA36AC4`/`0x9FF308`/`0x9D3C98`/`0x9E6D2C`, the `0x9E2BD0`/`0x9E52F8` modulator evaluator (five shapes, coefficient recompute, pool/record layer, transition ramp) and the V26 completion tail `0x9FD910`.
- The completion bar: `TheBusPassRoutesTheRenderedVoiceToTheRobotOutputBuffer` renders a synthetic source through the voice/bus pass to the device sink.

## Extraction passes (all committed)

Five bounded passes closed the implementer's `MISSING` list and corrected prior report claims:
- C15 `20260928-B-M6b-2-missing-bodies.md` (the six bodies; the modulator `vnmls` sign correction; no NEON lane order in `0x9E2BD0`/`0x9E52F8`; `0x9FD910`; the slot vtables).
- C16 `20260928-B-M6b-2-v7-completion.md` (`0xA552F0`/`0xA4C620`; the budget-step gate).
- C17 `20260928-B-M6b-2-v7-p2f-branch.md` (the P2F==0 continuation, the four ramps, the C8 seam).
- C18 `20260928-B-M6b-2-ramp-targets.md` (`0xA4BC58`'s output arguments and the ramp targets).
- C19 (the `0xA4C584` bit argument is the saved `bus->vt+0x3C` return).

## Records

- **No record is settled.** M6-022 stays `IMPLEMENTATION_GAP` (not wired into the live path; B-M6b-3 does that). Its `unresolved` names the bus-metering DSP identity `0xA50044..0xA50FD0`, `0xA25FF8`, the `0xA4B93C`/`0xA01768` sub-callees, the UNKNOWN class names and `[device+0x70]`/`[device+0x80]` identities, the modulator per-sample order, and the HARDWARE_ONLY sink/OpenSL values.
- M6-017 and the other M6 records are unchanged.

## For B-M6b-3 / later

- The engine is unwired: `WwisePlayback.Resolve`, `WwiseAudioSource.Produce`/`ToRobotRate`, `WwiseBusChain` and `WwiseVorbis.Decode` still stand.
- The ADPCM/Vorbis source render and the `params`-based resampler execution (M6-002/M6-003) are not part of this job; the render test uses a synthetic source.
- Queued cleanup: the `WwiseBusLifetime.cs` doc comment still says `[source+0xC]` is the bus where C18 X3 says it is the PBI; the V7 `[sp+0x2e]`/`P2F`/`E4`/`E0` names and the four ramp-field names stay UNKNOWN.
- No operator hardware run is needed for this job.