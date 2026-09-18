# Hardware captures

Raw frame logs written by `cozmo-conformance connect` (every UDP frame, both directions, UTC timestamps, hex).
Analyse with `python ../tools/analyze_frame_log.py <log> <catalog.json>` or `cozmo-conformance decode <hex>` /
`replay <hexfile>`.

| file | robot | result |
|---|---|---|
| `2026-09-18_fw2457_hw1.5_smoke_head0.4.log` (+ `.console.txt`) | Cozmo hardware 1.5 (body HW 5, colour 2), firmware **2457** (DDL build 2025-02-11), head serial 0x41d04d9d | PASS: connect 16 ms, identity, handshake, 722 RobotState @ 33.5 Hz over 20 s, SetHeadAngle 0.4 acked and reached (0.399), 0 resends, clean disconnect |
| `2026-09-18_fw2457_hw1.5_smoke_run1.log` | same robot, first run | PASS (same shape) |
| `2026-09-18_fw2457_probe.log` + `2026-09-18_fw2457_probe-results.json` | same robot | M2 subsystem probe: 14 distinct robot messages received, **every one decoded and re-encoded byte-identically, zero conflicts**. Confirmed camera (28 images, 7 chunks each, JPEGMinimizedGray QVGA), IMU (264-sample raw burst), animation state, crash report, cube advertisements, motor calibration |

`build_protocol_definition.py` reads these logs and the probe-results JSON directly, so the canonical
protocol definition's verification status is reproducible from the repository.

Serial numbers and the soft-AP MAC in the trace messages identify one specific robot; the repo is private.
