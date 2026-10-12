# Request: M4-041 and M4-044, cube lights

- **Answer file:** `re-analysis/research/20261012-m4-cube-lights.md`.
- **Read first:** the current manifest records M4-018, M4-041 and M4-044; `research/20261012-m3m4-settling-2.md`;
  `research/20261011-m3m4-fix-rows.md` (the cube-light rows).
- **Rules:**
  - research only;
  - **one coverage table per item, first**;
  - no fidelity claims;
  - nothing leaves this machine.

## 1. M4-041

- **BlendAnimWithCurLights** (the PlayLightAnim blend body): its inputs, the blend arithmetic (every constant as float
  bits; integer widths), and what it writes.
- **The StopLignAnim.Result log** (0x00639300): the current-state serializer it prints. Its exact format, the fields
  in order, and how each is rendered.
- **ActiveObject::GetLEDState** IndexTooLarge / NegativeIndex (0x0063958E/0x006395CC): every caller in the binary
  (use a BL/BLX scan), and whether any is on a live, in-scope path.

## 2. M4-044: the cube-light animation loader

- How cube-light animation files are found:
  - the directory and path scope;
  - the suffix filter;
  - recursion;
  - ordering (readdir or sorted);
  - which collect it shares with the M5 loader (CollectAnimFiles type 2, 0x005241DE).
- **How each file is read:** readAsJson, the empty skip, and the threading (DispatchWorker?).
- **CubeLightAnimationContainer / ParseJsonToAnim** (0x00589910..0x00589CD8):
  - the per-pattern fields and their conversions;
  - each error path (key, format, arguments, errG);
  - InvalidJson and InvalidPattern;
  - duplicate names;
  - what is kept on failure.

## Report

- Per item: the coverage table, the answer, and what stays UNKNOWN.
