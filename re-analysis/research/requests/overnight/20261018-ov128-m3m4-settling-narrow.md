# Research request: M3+M4 settling residue (narrow)

- **Date:** 2026-10-18
- **Requested by:** manager
- **Kind:** narrow extraction, four questions
- **Answer file:** `re-analysis/research/overnight/20261018-ov128-m3m4-settling-narrow.md`

## Why this is needed

M3+M4 is the acceptance target. The Opus settling passes (`research/20261018-m3m4-settling-a.md`, `-b.md`; read them
first) left these source questions open. Re-read the current manifest records named below before answering. Under
AGENTS.md "What we reproduce", answer only what touches robot traffic, robot behaviour, C# API delivery or readiness, or
downstream engine state; say "no such effect" with citations where that is the answer.

## Questions

1. **M11-097, RobotStateHistory query and admission results.** Give rows for:
   - ComputeStateAt (prior reads around 0x00531784..0x00531808): every result code, the interpolation between the bracketing
     states (types, arithmetic), the `withInterpolation`/flag argument, and what the out-parameters hold on each result;
   - GetLastStateWithFrameID: search order, result codes, outputs;
   - AddRobotStateToHistory (0x00512B9C..0x00512F0C): every result path (on-ramp 0x00512BE2, ContainsOriginID 0x00512C44,
     add failure 0x00512C38, the origin-reject warning) and what each does to robot+0x390.
   Then compare each with the stack's history (`Vision/VisionSystem.cs`, `Sensors.cs`, `UnexpectedMovement.cs`) and say
   what differs.
2. **M4-052, relative-mode SetObjectLights.** MakeStateRelativeToXY 0x004E0DEC (called at 0x00637EE8), and the located
   lookup GetLocatedObjectByIdHelper(id, −1) 0x00637E82 with its dynamic_cast to ActiveCube: the body (which LED goes where,
   from which pose and point, rounding/angle arithmetic), the failure paths, and what reaches the wire.
3. **M4-051, the completion-callback guard.** What 0x564A64 (read in CubeLightComponent::Update at 0x006379C6..0x00637A48)
   is, who writes it, and when the callback is therefore invoked or skipped.
4. **M3-037, short raw image payloads.** The 2026-09-30 NEEDS EXTRACTION: SetNextImage 0x00652B04's copy/move of the image
   buffer, and so what a raw gray/RGB payload shorter than rows*cols (or *3) yields (AddChunk resets end = begin 0x004F1DA2,
   reserve 0x38400 0x004F1DAA). Also: can the engine ever request or receive raw encodings on the live path (evidence beyond
   M3-050)?
