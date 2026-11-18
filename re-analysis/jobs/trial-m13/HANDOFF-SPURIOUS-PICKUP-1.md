# SPURIOUS-PICKUP-1

All six reported pickup/return pairs follow the firmware's IsPickedUp bit8 in F0, then its clearing. Running the shipped classifier confirms the same six pairs. Clearing only bit8 removes all selected transitions with the other samples unchanged. This establishes what the firmware reported; it does not establish that the operator physically lifted the robot or explain the firmware detector's internals.

The classifier thresholds/timers are unchanged. One separate live admission gap was found and fixed: ReactToPickup inherited an untranscribed charger-platform gate that returned true. Its actual vtable slot0x0102C274 points to0x0059EC12, return false. The carrying slot0x0102C278 points to0x00607DB0, return true, and is now explicit too. This gate correction does not remove the six recorded pairs in the isolated replay; it must not be presented as their cause.

## Whole production path

| Step | Primary evidence and result |
| --- | --- |
| Wire / raw input | Generated RobotState.Read in RobotMessages.g.cs;92 bytes including F0 tag. Status wire offset77; pitch29; accel49/53/57; gyro61/65/69; wheels33/37; cliff81..88. Robot::UpdateFullRobotState0x0051291C reads this input. Only pose origin/frame IDs are rebound for the fixture. |
| Native IMU filter |0x005129A4..0x00512A70 copies accel/gyro, computes magnitude and filters it with bits3D4CCCD0/3F733333; axes use old*3F666666 + new*3DCCCCD0. Native constructor zeroes filter/current/candidate0x005100E0..0x005100FE. |
| Head / raw flags | CheckAndUpdateTreadsState0x00511E00 gates on robot+314 head-calibrated0x00511E1C..22. Status mask0x20 falling at511EC4 and8 picked-up at511F04. Head calibration D1 ID2 start/completion from the recorded prefix is honored by the oracle. |
| Other possible classifications | onSide: abs(abs(filteredAY)-9800)<3000; physical onBack center1.3002703189849854rad, tolerance0.2617993950843811; simulator center1.6824973821640015. Native Radians pitch extreme >1.919862151145935 or <−1.3962633609771729; level abs(pitch)<=0.7853981852531433. Constants/branches511E00..511FEA and5121FC/5122A0..5122BC. The oracle executes the actual Radians operations84CC90/84CD12/84CC0A/84C87C rather than plain Python comparisons. |
| Candidate / commit | Normal candidate persistence250ms; onBack additional750ms. Falling sets candidate6 and backdates now−250 at511F36..44. Picked-up from OnTreads sets InAir and backdates now−250 at512244..512258. Clear picked-up with no side/back/extreme sets OnTreads and backdates now−250 at511F48..98. Thus these short pulses commit immediately; there is no250ms raw-bit debounce to add. Commit eligibility511FD4..FE8, actual state store51208E, payload512092, message ctor512098 and external-interface Broadcast5120AA. |
| Manager trigger | GenericReactionStrategy checks running-or-runnable then its WantsToRun. Pickup callback0x0060DDCE reads committed+355==1. Returned event53 filter0x0060DE36..56 requires enabled && state0. Manager sticky first-action gate5A3558..357A, reaction checks/order5A3550..37AA, switch5A1E6A..1F1C and native start/stop reports5A22B4/5A20AA. The replay uses actual strategies, manager, scoped actions and queued app delivery. |
| Pickup admission / Init | Pickup vptr+20 relocation102C270 ->607DAC=true, +24 relocation102C274 ->59EC12=false, +28 relocation102C278 ->607DB0=true. Actual configured common base inputs remain. Init60775E sets scale1 and queues WaitAction(robot,0.5f), completion StartAnim. Existing source-backed native runner is reused. |
| Interruption / visible result | Every selected picked-up pulse ends before that0.5s wait. The real return preempts pickup before StartAnim, so no pickup animation/calibration is expected. In frustration the second pickup also cancels the first return wait. Final return Init608500 queues WaitAction(robot,0.5f); callback6085C4..60868E only calibrates the head if abs(pitchDegrees)>10, otherwise ends quietly. The selected final returns are level. No clip is invented to make an observation visible. |

## Raw samples

Private source20261117-121734-PERSONALITY-HW-1-77ccf7/messages.jsonl. Its SHA256 is recorded with the244 selected inbound packets in SpuriousPickupInputs.cs. Values below come from actual first bit-on/off F0 samples, not behavior log timestamps. IMU values are raw wire floats; filteredAY is from the full-prefix native oracle. All twelve samples have falling bit0x20 clear. Pair order is express-needs, unexpected-first, unexpected-second, on-charger, frustration-first, frustration-second.

| atMs / source line / firmware timestamp | status hex | pitch rad / filteredAY | accel XYZ | gyro XYZ | wheels L/R | cliff0/1/2/3 |
| --- | --- | --- | --- | --- | --- | --- |
| 334846.4460 / 92203 / 313730 | 0318 | -0.026846 / -382.023 | 3147.654/-598.156/9627.979 | -2.361/-0.018/-4.155 | 0.000/0.000 | 889/0/0/0 |
| 334846.4460 / 92207 / 313760 | 0318 | -0.016782 / -382.023 | 5572.612/-313.149/8073.611 | -2.562/-0.069/-4.575 | 0.000/0.000 | 889/0/0/0 |
| 335041.6187 / 92266 / 313940 | 0310 | 0.032917 / -622.173 | 5118.755/-609.532/8549.621 | 0.018/0.001/0.031 | 0.000/0.000 | 889/0/0/0 |
| 594172.2465 / 170716 / 574425 | 0318 | -0.080527 / -1245.512 | -514.330/-349.074/9090.896 | 0.126/0.458/-0.008 | 0.000/0.000 | 883/0/0/0 |
| 594386.1607 / 170805 / 574635 | 0310 | -0.004102 / -630.172 | 495.769/-968.187/9741.742 | 0.003/0.017/0.015 | 0.000/0.000 | 889/0/0/0 |
| 597243.1415 / 171672 / 577485 | 8319 | -0.088058 / 37.600 | 5239.105/3082.989/4994.813 | -2.627/2.217/-5.745 | -253.000/255.000 | 881/0/0/0 |
| 597403.3218 / 171737 / 577665 | 8011 | 0.012298 / -703.757 | 4439.169/-6606.661/3517.685 | -0.727/3.029/-0.291 | 21.000/0.000 | 889/0/0/0 |
| 651281.5733 / 188619 / 631815 | 0318 | -0.092503 / -896.067 | 1474.135/-1074.765/11808.644 | -1.042/0.899/-0.447 | 0.000/0.000 | 881/0/0/0 |
| 651487.8915 / 188669 / 632025 | 0310 | -0.168191 / -666.750 | 4643.344/682.581/8705.297 | 0.280/0.246/-0.773 | 0.000/0.000 | 841/0/0/0 |
| 665302.0921 / 192166 / 645915 | 0318 | -0.133085 / -876.076 | -1153.202/-1702.859/10353.670 | -0.246/0.176/-0.356 | 0.000/0.000 | 817/0/0/0 |
| 665505.6446 / 192211 / 646095 | 0318 | -0.042060 / -1177.818 | -2097.438/-2003.434/8202.343 | -0.145/-1.161/-0.851 | 0.000/0.000 | 570/0/0/0 |
| 665505.6446 / 192215 / 646125 | 0310 | -0.045945 / -1177.818 | -2734.514/-1753.753/8636.440 | -0.226/-0.682/-0.993 | 0.000/0.000 | 717/0/0/0 |
| 665626.8728 / 192239 / 646245 | 0318 | -0.047575 / -1517.208 | -1602.867/-2501.598/9726.773 | -0.109/-0.122/-0.476 | 0.000/0.000 | 879/0/0/0 |
| 665828.0421 / 192287 / 646425 | 0310 | -0.020330 / -799.009 | 946.631/-444.875/9828.562 | -0.005/-0.216/0.129 | 0.000/0.000 | 888/0/0/0 |

The on-charger phase name is a harness label; it is not proof of charger contacts. These bit edges have status318/310, except the second unexpected pair8319/8011. The complete selected sequence retains all other flag changes, including contacts/platform-related inputs, rather than rewriting them to fit the phase name.

## Oracle / replay boundaries and reproduction

`python re-analysis/tools/emu/emu_spurious_pickup.py --bundle <private-directory> [--emit-csharp <path>]` requires local unicorn2.1.4, lief and capstone plus the shipped resources/lib/armeabi-v7a/libcozmoEngine.so. It neither connects to a robot nor writes hardware acceptance output. It regenerates244 native per-sample expected states/candidates/timer/filterY bits across five independently initialized windows, and asserts the bit-cleared counterfactual. It also runs all21,181 prefix F0 packets through671800ms, honoring captured head-calibration D1, carrying native filter/state across earlier physical interactions. Both original and bit-cleared full-prefix variants assert the selected12 /0 transitions.

`emu_offtreads.py` executes the actual IMU filter and decision Thumb instructions. Only timer PLTs4A4F6C/4A7B58 are replaced by a caller-controlled tick clock; ELF PLT relocations resolve to actual internal code. It stops at commit eligibility511FF0, no-commit51219A or head-gate512206 and copies the native chosen state byte to355 as51208E would. Commit-side action cancellation, logging, world state and broadcasts are outside the bounded emulator; the C# production-ingress tests cover those separately. This is not an emulation of the whole engine.

The C# tests replay the exact selected packets into an in-memory, actual HW4 native robot runtime with shipped animation metadata, real native reactions/action list and behavior/action app reports. Clock grouping is60ms relative to each window; native fresh-window vectors use the same tick clock. The prefix oracle instead uses floor(arrivalMs/60)*60; this is a declared scheduling model, not recovery of the recording's precise tick phase. No classifier threshold/timer changes, filter seeding, state injection or forced reaction admission is used. A real initial action opens the manager's native sticky C3 gate. Pickup/Returned remain enabled; other high-level reactions and Freeplay are explicitly disabled because their original world/actions are not reconstructed. The recorded six pair order, wait cancellations/success, start/stop reports and no-animation/no-calibration result are all asserted. Native platform-gate rejection has a separate regression.

Shared visible MISSING diagnostics remain narrowly allowed for M11-077 owner removal, M13-039 three client cache inputs, M8-015 running/terminal Viz text, and M8-023/M8-027 ProtoHandle callable destruction. They are not silently classified as recovered by this job. No additional diagnostic outside those enumerated shared markers occurs in these windows.

## Handoff

Root cause of the reported transitions: firmware bit8 pulses, reproduced by the shipped classifier. Exact production change: transcribe Pickup's native platform/carrying admission gates. M7-015 unresolved appended; M7-015 and M10-001 remain IMPLEMENTATION_GAP with frozen authority/evidence/status unchanged. No SCOPE/PROJECT_STATE acceptance changes, no hardware run and no hardware bundle committed. Raw fixtures are selected inbound test inputs only.

Validation:115/115 focused tests;16 new cases; native oracle21,181 prefix packets in each original/bit-cleared variant,12/0 selected transitions; final regenerated fixture SHA256 FF045AF0B4F2ADE61DA3F83BA99B1F0FE01C118E999C7B9758B0EF7441A78A8C matches the committed input byte for byte; fidelity check763 records with statuses unchanged. Full suite8698/8698 passes in28.7710min, including all13 optional private face/charger/narration/physical-reaction replay cases; log .scratch/m13-spurious-validated-bf375ce3.log. No isolated-fault guard failure. Claude should spot-read the bounded oracle stops/PLT resolution, vtable gates and the raw bit-on/off pairs; this handoff does not claim physical pickup correctness or a hardware pass.
