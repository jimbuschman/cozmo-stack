# Claude check of R-M13-017-REST (answer 321c6ac0), manager, 2026-10-22

**Verdict: the answer's central claim is wrong; not usable as written. Items 7-8 reuse earlier reviewed work.**

- **Wrong:** "the located charger is Markerless … its GetBoundingQuadXY (0x00502C32..70) dictates whether the platform
  flag clears; build obligation remains". The Charger constructor (0x004E9B6C, decomp `004e/004e9b6c.c`) calls
  `ActionableObject::ActionableObject`, not MarkerlessObject's. Only Block (0x004E62A2), MarkerlessObject (0x00502C32),
  Robot (0x00514D74) and ObservableObject (0x0087713A) define GetBoundingQuadXY; Charger defines GetCanonicalCorners
  and inherits the base, so its quad is `ObservableObject::GetBoundingQuadXY` with the charger's corners. That is what
  the stack already does (`ObjectFootprint.ChargerBoundingQuadXY` -> `ObservableObjectBoundingQuadXY`, Sensors.cs:716).
- **Consequence (narrowing, to confirm in M13-017's record):** the platform step (0x00513CD8..0x00513E2A) queries only
  family {4} (0x00513D5C..0x00513D74), i.e. chargers, and the robot's own quad. Markerless, Ramp and HumanHead
  footprints are therefore not on M13-017's path; they belong to M13-024/M13-025 and their own callers.
- **Quality:** Thumb-bit symbol values used again despite the rule (0x00624809, 0x00616D55, 0x004E6491); a non-existent
  instruction (`ldrx`) in item 1; item 9 is unresolved prose. Items 7 (SetObjectPoseChanged) and 8 (map) restate the
  reviewed `20261022-r-m11-pose-config-check.md` and `20261021-m11-charger-map-add-check.md` and add nothing to check.
- **Catch-all 0x004E65C6** (`__cxa_begin_catch` inside GetBoundingQuad<float>): plausible, unconfirmed; the charger
  path through the base quad is the only one M13-017 needs.
