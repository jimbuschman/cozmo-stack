# PET-FALSE-DETECT-1 handoff

The shipped pet detector reproduces all five classifications from the operator's captured camera frames. The first three are false positives in the face-sighting scenes. The pet rectangle is offset from the reported face in the first two, and the third triggering frame has no face report. The two blue-toy exposures both classify as Cat. The stock reaction admits each tracked ID on its third observation.

| Window (UTC) | Original detector result | First → third robot timestamp (ms) | Pet rectangle (x, y, w, h) |
| --- | --- | --- | --- |
| 15:36:39 | Dog, 963 | 348685 → 349025 | 75, 115, 120, 120 |
| 15:44:14 | Dog, 987 | 805795 → 806065 | 41, 117, 76, 76 |
| 16:00:08 | Cat, 910 | 696765 → 697040 | 45, 48, 60, 60 |
| 00:39:01, blue toy | Cat, 930 | 869565 → 869835 | 30, 80, 120, 120 |
| 00:41:32, blue toy | Cat, 906 | 1021365 → 1021635 | 48, 123, 60, 60 |

| Input to visible result | Source evidence |
| --- | --- |
| F2 RGB/gray camera → scheduled vision image | VisionSystem decodes the captured encoding at 0x006B4B7C–0x006B4C78. Mode 9 is checked at 0x006B529A–0x006B52B8 and marked processed at 0x006B5326. The pet input masks the earlier marker and face rectangles at 0x006B3950–0x006B396E. The test replays the marker, face and pet modes from each captured 0x43 bitset. |
| Image → stock detection and tracking | PetTracker submits the gray image and retained handle pair at 0x0087C9A0–0x0087C9AE. 0x0087C414 supplies the shipped `vision_config.json` threshold of 900. 0x0087CA2A and 0x0087CAA8 read Cat/Dog results with mask 0x180000. The replay executes the original ARM OMCV code and matches the recorded first score, type, rectangle and the two subsequent tracked observations with score −1. |
| Observations → reaction | PetWorld checks body/head IMU for new IDs at 0x0050B8C8–0x0050B8F4, adds one for a retained ID at 0x0050B8A8–0x0050B8AE and emits 0x48 at 0x0050BA2A–0x0050BA68. The strategy checks the one-minute cooldown at 0x00611AF0 and 0x00611DD0–0x00611E14, excludes previously reacted IDs at 0x00611B6E–0x00611BB6, requires count >2 at 0x00611BB8–0x00611BBC, and checks the charger platform at 0x00611C32–0x00611C36. The shipped trigger map chooses ReactToPet. The replay matches the recorded 0x88 transition from None to PetInitialDetection. |
| Reaction → external action refusal | The reaction switch sets the external gates at 0x005A20A8–0x005A2110. ActionQueue checks the queue gate at 0x0053D94C–0x0053D9A2. For both toy windows MCP refuses the head command and identifies PetInitialDetection. |

Set `COZMO_PET_REPLAY_ROOT` to the local directory containing the three recorded session bundles to run the five replay cases. The short windows rebase odometry origins and guest IDs to the fixture. To align the detector's first search with the long session, the fixture starts pet warm-up 2, 0, 1, 3 and 0 pet frames into the respective preambles and runs the captured marker and face modes throughout. It checks the reaction in isolation from unrelated strategies. No production code or fidelity classifications changed. Hardware captures remain local.

Build `bf994ef1` passed all 8,808 Debug tests in 57.03 minutes with the recorded replay fixtures enabled. All 31 focused pet vision and reaction tests passed. The five replay cases checked 252 captured camera frames. `fidelity.py --check` passed all 764 records.
