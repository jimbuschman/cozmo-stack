# DOCK-OVERSHOOT-1

## Claims and evidence

- The direct public pickup reaches the retained PickupObject leaf. The constructor defaults at 0x00550386–0x005503AA and 0x005536EA–0x005536F8 supply zero correction offsets, speeds 60/200/500, method 2 and final flag true. The request's selected marker/target and the slots are stored at 0x0063BAAC–0x0063BAB6 and 0x0063BB72–0x0063BB7A. The reliable builder at 0x0063BD50–0x0063BDC4 uses tag **0x42**, written at 0x007A8D56. Run e9520f frame 861 contains the expected low pickup body (zero, 60, 200, 500, action 0, flags 0/0, method 2, final flag 1).
- Vision computes the historical robot pose and applies the admitted marker list before invoking the correction at 0x00654D92 and 0x00654DEC–0x00654E4A. The native producer checks target, rotation bracket, unique retained marker and matching timestamp at 0x0063BE9C–0x0063C142. It writes X minus slot 0x14, Y plus slot 0x18, raw Z and clamped yaw plus pi/2 and slot 0x1C at 0x0063C14A–0x0063C1CE. The reliable send at 0x0063C548–0x0063C57A uses tag 0x48 (0x007A91A6). The managed retained path follows these inputs.
- Reproduction uses the 14 pre-B8 correction timestamps from the run's robot-states-and-dock.jsonl. For each, join the type 2, ID 0, origin 1 cube pose in observations.jsonl. Use the matching F0 raw state or the matching same-origin/frame bracket. Native history at 0x0053147E–0x0053149C and 0x005314FE–0x00531560 uses the exact key or the after-pose expressed in the before-pose frame. Scale that relative translation by the timestamp fraction, then add the before translation and interpolate yaw (0x0053076A–0x005307F4). Compose the near LightCubeJ_Top face using its +22 mm local Z and source face rotation (0x004E5442–0x004E55DC). Transform into the computed robot frame and apply zero slots. The reported orientation is already upright within numeric noise, so the yaw matches the clamp result.
- At t11115 the recorded X/Y/Z/yaw is (116.672782898, −4.352085114, 15.316717148, 0.003504113); the independent reconstruction gives (116.672783331, −4.352085101, 15.316715830, 0.003504072). At t12745 the last pre-result X was 54.566917419 mm (replay 54.566909528 mm). Across all 14 samples the largest translation residual was 0.000018215 mm and largest angle residual 0.000000062 rad. This is a numerical replay using the stack's published input poses. It provides no evidence of an extra 13 mm in the dock request or correction geometry.
- Firmware B8 reported success at frame 1183. The late frame 1349 correction was sent after B8 and lift-up. The native target gate does not read B8 as an independent stop condition. There is no same-timestamp cube report to reconstruct that packet. Its timing places it after the physical miss.
- The harness watched 0x47 (SetControllerGains). The raw sidecar kept the actual request, but the phase timeline omitted it. The corrected filter watches the generated DockWithObject message ID, preserving the other three IDs and the timeline's host capture order.

## Files and tests

- Cozmo.Conformance/M15CubeHw.cs uses the protocol message IDs in the phase timeline and updates the generated README and source description. The active pickup handoff's wire label is corrected. Historical captures remain the original evidence.
- Native pickup, docking correction and hardware harness suites passed 137/137. The public fake-robot pickup reaches 0x42 and the real marker carrier reaches 0x48. The no-network `m15-cube-hw --validate` check resolved all 10 fidelity records. The independent wire replay above directly checks the captured request and 14 corrections. The batch gate is recorded in QUEUE.md.

## MISSING

- The bundle lacks raw frames/marker corners, NV calibration payload and independent physical pose measurements. The capture cannot isolate a camera or PnP bias, fork/contact geometry, or the firmware's closed-loop stop target. M11-094's 22.1 mm map footprint is not a firmware target. No motion compensation or physical cause is established.
- The physical rerun is operator-owned. The run still records the cube being pushed, followed by NoLoadDetected and the pickup verification failure.

## Workers

Reader requested gpt-6.1-sol at medium effort. Builder and independent reviewer used the configured default request. The manager re-opened the packet writer and union tags in the shipped ELF, decoded the wire and reproduced the 14-row calculation. Review confirmed the evidence boundary and the timeline filter correction.

## Records

- M12-003 and M12-005 own the exact request sequence and body. M12-013 owns the exact timestamp-first correction layout. M12-014 retains the approved policy for the last two unwritten bytes.
- M12-019 owns the wider clamp, M11-052 historical pose, M11-050 historical camera, and M11-003 marker geometry. This numerical cross-check and harness correction propose no settlement.
