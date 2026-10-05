# Job B-FACE: the needs-driven face distortion and the live-idle selection

- **Agent:** opencode (DeepSeek) as cozmo-manager, or a Sonnet worker.
- **Type:** build.
- **Rules:** `CHECKLIST.md` applies, and **this job never settles a record** (section 6). Records stay
  IMPLEMENTATION_GAP, with `unresolved` starting "built, awaiting strong verification:".

## Rows

`re-analysis/research/20261004-procedural-live-extraction.md` (Codex), rows P01–P10 and D01–D18. The manager re-checked
the central rows in the binary, and both held:
- **D07/D08/D18:** TrackLayer Update reads context+0x34 → NeedsManager+0x3D4 → `GetCurrentDesiredDistortion`
  (0x0064EDE8..0x0064EE14). It compares with `0x3727C5AC` (`vcmpe.f32`, return if `<=`) and tail-calls AddGlitch with
  the sampled degree.
- **P04:** a top-of-stack 0x198 sets +0x194 = 1 and idle+0x34 = this+0xA8, then calls UpdateLiveAnimation
  (0x0057D070..0x0057D080).

Have `cozmo-verifier` check every other row before building on it.

## Batches

1. **DesiredFaceDistortionComponent** (D01–D17).
   - NeedsManager owns it, at +0x3D4.
   - Init with the robot RNG and the shipped `needs_handlers_config.json` `needsBasedFaceDistortion` block. Keep the
     shipped quirk: the degree parser checks the cooldown graph for emptiness (D06).
   - The getter, in this order:
     - a per-engine-tick cache, keyed on BaseStationTimer's tick count, not wall time;
     - the -1 sentinel;
     - the params, RNG and pause gates;
     - the deadline;
     - the repair-level degree graph, with the 0.1f threshold (`0x3DCCCCCD`);
     - the degree draw **before** the cooldown draw: float32 bounds, then `RandDblInRange` in f64, then a float32
       conversion;
     - the deadline = float32(now + cooldown).
   - Every float as its bit pattern.
2. **The wiring** (D07, D08, D18, P07).
   - `TrackLayerComponent.Update` calls the component every time the streamer's last-stream seconds (+0x88) > 0.
     That is before the keep-alive's no-active-stream gate, and independent of a 0x198 top.
   - AddGlitch only when the degree > `0x3727C5AC`.
   - Replace the `Func<float>` seam that defaults to 0 (`TrackLayers.cs:609-612`).
3. **Live-idle selection and failure** (P01–P06, P08).
   - The embedded live animation at streamer+0xA8, marked live at construction.
   - The initial Count push, the 0.5 s idle timeout (`0x3F000000`) and the top-of-stack 0x198 selection (P04).
   - The LiveUpdateFailed log and the error flag (P05).
   - The shared idle tail (P06).

## Out of scope

**Who pushes 0x198 in production.** No fixed engine producer exists (PARTIAL in the extraction). It arrives through the
PushIdleAnimation message (tag 0xAE, P09/P10) from the app, so it waits on the game-to-engine channel. Leave it as
the records' visible gap. Don't add an automatic push.

## Records

- **Built here:** M7-005, M7-016 and M7-017, for the distortion and selection parts.
- **Affected:** M7-007, M7-008, M7-009 and M7-010, whose `unresolved` names the defaulted distortion.
- **M7-017's text:** the claim that DesiredFaceDistortion "has no source" is contradicted by D01–D18. Correct it.

## Gates

Before each commit:
- `cozmo-verifier` gives PASS on the diff;
- `fidelity.py --check` passes;
- the full suite passes.

Log each batch in `status/B-FACE.md`. Finish with `DONE` or `BLOCKED <reason>`.
