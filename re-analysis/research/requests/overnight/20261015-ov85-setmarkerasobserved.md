# Overnight research request OV85 (M11-013: ObservableObject::SetMarkerAsObserved)

- **Binary:** libcozmoEngine.so 3.4.0-1204.
- **Record:** M11-013. Re-read it, and M11-004, in `re-analysis/fidelity_manifest.json`.
- **Why:** builder job B-M11-3 is BLOCKED on this body (`jobs/status/B-M11-3.md`).
- **Answer file:** `research/overnight/20261015-ov85-setmarkerasobserved.md`.
- **Format:**
  - a coverage table first;
  - then the rows: step, what the original does, the citation, the classification.
- **Tooling:** use capstone with skipdata. Thumb literals are pc+4 aligned.
- **Rules:** `research/README.md`. M1-034 applies to crash paths.

## The task

Read `ObservableObject::SetMarkerAsObserved(ObservedMarker*, u32 timestamp, float)` at 0x00876DA8..0x008770CC completely.
Its live caller is CreateObjectsFromMarkers at 0x0062559C; the float argument is 0x3DCCCCCD.

Give:
- how a known marker is selected (code match, geometry and thresholds, with the exact f32/f64 order and unordered
  branches);
- when the timestamp store at 0x00876F86 runs, and on which node field (+0x50);
- the two Warning calls at 0x00876FA8 and 0x00876FC4: key, format, arguments, and what happens after each;
- the return value;
- every other state write.

Contrast it with the plural SetMarkersAsObserved 0x00876CCC. End with the build rows.
