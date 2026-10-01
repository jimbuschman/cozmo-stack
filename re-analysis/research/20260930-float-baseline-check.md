# Float-literal baseline check (Q9)

## Coverage audit (2026-10-01)

| baseline item(s) | coverage | unchecked basis affecting conclusions |
| --- | --- | --- |
| Baseline integrity: all 430 distinct file/literal rows | CHECKED | None. The fixture was reparsed at HEAD; 427 rows are live and the three named rows are absent from their named source files. |
| 87 OpenCV sine-table rows | CHECKED | None. All 451 C# array words were independently compared with all 451 words in `libopencv_imgproc.so` at `0x000E7910`; the arrays are byte-identical. |
| 273 Wwise window-table rows | CHECKED | None. All 3,968 C# words in `VWin256` through `VWin4096` were independently compared with `libcozmoEngine.so` at `0x01054490..0x01058290`; the arrays are byte-identical. |
| 36 live non-table rows detailed by record above “Remaining 31” | CHECKED | None. Each source literal/type, native word/width and result was rechecked; every printed address was re-read in the binary or shipped asset. |
| Remaining 31 live non-table rows | CHECKED | None. Each source literal/type, native word/width and result was rechecked; every printed address was re-read in the binary. |
| Three stale rows | CHECKED | None. `DockActions.cs 0.523599`, `DriveActions.cs 0.523599`, and `FaceActions.cs 0.785398` have no live token occurrence. |

No conclusion below rests on unchecked work. “For each literal” means each of the fixture's distinct `(file, decimal-token)` rows, which is what the lint baseline stores; comments and repeated uses of the same token do not create additional baseline rows. The recheck also examined every live code use when a token serves more than one native operation (notably `CornerRefinement.cs 1.4142135`).

Date: 2026-09-30; completeness re-audit 2026-10-01

Baseline: `origin/main` / `415b9e0` after the required pull

Input: `cozmo-stack/tests/Cozmo.Protocol.Tests/Fixtures/float_literal_baseline.txt`

## Baseline integrity

The request says 437 literals. The current baseline has **430 data rows** (432 physical lines including its two comments), of which **427 still occur** in source and three are stale:

| Stale row | Current state |
|---|---|
| `Cozmo.Robot/Manipulation/DockActions.cs 0.523599` | no occurrence in the file |
| `Cozmo.Robot/Manipulation/DriveActions.cs 0.523599` | no occurrence in the file |
| `Cozmo.Robot/Vision/FaceActions.cs 0.785398` | no occurrence in the file (the live fine-turn bound is now represented separately) |

Those three have no current C# type or bits and should be removed from the lint baseline; they are not behavioral findings.

## Direct table checks

These rows are binary32 (`f` suffix), and their C# parse bits equal the corresponding shipped table word for every current entry:

| Record | Baseline rows | Shipped source | Result |
|---|---:|---|---|
| M5-021 / M5-032 | 87 distinct `OpenCv310.cs` sine-table decimals | all 451 C# `SinTable` words versus the shipped `libopencv_imgproc.so` range `0x000E7910..0x000E801C` | all 451 words byte-identical; every baseline row MATCH, binary32 |
| M6-002 | 273 distinct `WwiseVorbisNative.Windows.cs` window-table decimals | all 3,968 C# words in `VWin256`, `VWin512`, `VWin1024`, `VWin2048`, and `VWin4096` versus `libcozmoEngine.so` `0x01054490..0x01058290` | all 3,968 words byte-identical; every baseline row MATCH, binary32 |

The repeated literals in those source arrays collapse to one baseline row per file/literal, which is why 360 baseline rows cover more than 360 table elements.

## Confirmed mismatches, grouped by record

### M12-001 — `PreActionPose.cs 56.5771`

| C# location/type | C# bits | Engine value | Engine bits/width | Result |
|---|---|---|---|---|
| `PreActionPose.cs:84`, `double` | `56.5771` = binary64 `0x404C49DE69AD42C4`; converting it to f32 gives `0x42624EF3` | 56.5770835876 at `0x004E5CB2..0x004E5CBC` | `0x42624EEF`, binary32 | **MISMATCH** |

The four-ULP error reaches the live flipping pose before path planning.

### M12-010 — six SearchForBlock constants

| Baseline literal / C# location | C# type and effective f32 bits | Engine address | Engine bits/width | Result |
|---|---|---|---|---|
| `0.261799`, `SearchActions.cs:34` | double `0x3FD0C15097C80842`; f32 `0x3E860A85` | `0x00546A3E` | `0x3E860A92`, f32 | **MISMATCH** |
| `0.349066`, `SearchActions.cs:36` | double `0x3FD65718EB895076`; f32 `0x3EB2B8C7` | `0x00546A4E` | `0x3EB2B8C2`, f32 | **MISMATCH** |
| `0.0698132`, `SearchActions.cs:42` | double `0x3FB1DF4722D4405F`; f32 `0x3D8EFA39` | `0x00546ED2` | `0x3D8EFA35`, f32 | **MISMATCH** |
| `0.0349066`, `SearchActions.cs:44` | double `0x3FA1DF4722D4405F`; f32 `0x3D0EFA39` | `0x00546E56` | `0x3D0EFA35`, f32 | **MISMATCH** |
| `0.0872665`, `SearchActions.cs:168` (negative in use) | double magnitude `0x3FB65718EB895076`; f32 `0x3DB2B8C7` / `0xBDB2B8C7` | request constants at `0x005BB13E..0x005BB146` and `0x005BB27E..0x005BB28A` | `0x3DB2B8C2` / `0xBDB2B8C2`, f32 | **MISMATCH** |
| `0.785398`, `SearchActions.cs:170` | double `0x3FE921FAFC8B007A`; f32 `0x3F490FD8` | action constants at `0x005BB0C4..0x005BB0CE`, `0x005BB204..0x005BB20E`, and `0x005BB33A..0x005BB344` | `0x3F490FDB` (signed as required), f32 | **MISMATCH** |

### M12-012 — `ManipulationBehaviors.cs 0.174533`

| C# location/type | C# effective bits | Engine address/value | Engine bits/width | Result |
|---|---|---|---|---|
| `ManipulationBehaviors.cs:259`, `double` | binary64 `0x3FC65718EB895076`; f32 `0x3E32B8C7` | `0x0063C66C..0x0063C67E`, ten-degree `Radians` argument | `0x3E32B8C2`, f32 | **MISMATCH** |

### M12-019 — `Docking.cs 0.698132`

| C# location/type | C# effective bits | Engine address/value | Engine bits/width | Result |
|---|---|---|---|---|
| `Docking.cs:136`, `double` | binary64 `0x3FE65718EB895076`; f32 `0x3F32B8C7` | `0x0063C182..0x0063C194`, 40-degree ClampPoseToFlat bound | `0x3F32B8C2`, f32 | **MISMATCH** |

### M10-002 — `UnexpectedMovement.cs 0.174533`

`UnexpectedMovement.cs:136` is binary32 `0x3E32B8C7`; the constructor stores `0x3E32B8C2` at `0x0063DAB0..0x0063DAB6` and the live `vcmpe.f32` consumes it at `0x0063E4B6..0x0063E4C2`. **MISMATCH, five ULP.**

### M11-006 / M11-008 — BlockWorld angles

| Baseline literal / C# location | C# effective f32 | Engine address | Engine f32 | Result |
|---|---|---|---|---|
| `0.0872665`, `BlockWorld.cs:480` | double `0x3FB65718EB895076`; f32 `0x3DB2B8C7` | cluster angle `0x00625498..0x006254E0` | `0x3DB2B8C3`, f32 | **MISMATCH** |
| `0.349066`, `BlockWorld.cs:487` | double `0x3FD65718EB895076`; f32 `0x3EB2B8C7` | pose clamp `0x00505F10..0x00505F22` | `0x3EB2B8C2`, f32 | **MISMATCH** |
| `0.785398`, `BlockWorld.cs:511` | double `0x3FE921FAFC8B007A`; f32 `0x3F490FD8` | visibility call `0x006220E2..0x006220EC` | `0x3F490FDB`, f32 | **MISMATCH** |

### M11-015 — body-turn parameters

| Baseline literal / C# location | C# effective f32 | Engine address | Engine f32 | Result |
|---|---|---|---|---|
| `0.0349066`, `VisionSystem.cs:1065` | double `0x3FA1DF4722D4405F`; f32 `0x3D0EFA39` | TurnInPlace tolerance `0x00545A78..0x00545A84` | `0x3D0EFA35`, f32 | **MISMATCH** |
| `5.23599`, `VisionSystem.cs:1068` | double `0x4014F1A75CD0BB6F`; f32 `0x40A78D3B` | TurnInPlace max speed `0x00545A28..0x00545A44` | `0x40A78D36`, f32 | **MISMATCH** |

### M14-003 — face-tracking parameters

| Baseline literal / C# location | C# effective f32 | Engine address | Engine f32 | Result |
|---|---|---|---|---|
| `0.0349066`, `FaceActions.cs:488` | double `0x3FA1DF4722D4405F`; f32 `0x3D0EFA39` | pan/tilt tolerance `0x005646BC..0x005646D8` | `0x3D0EFA35`, f32 | **MISMATCH** |
| `0.776672`, `FaceActions.cs:508` | double `0x3FE8DA7F3CF70154`; f32 `0x3F46D3FA` | max head angle `0x005646DC..0x005646E8` | `0x3F46D3F2`, f32 | **MISMATCH** |
| `0.174533`, `FaceActions.cs:514` | double `0x3FC65718EB895076`; f32 `0x3E32B8C7` | sound threshold `0x00564722..0x0056473E` | `0x3E32B8C2`, f32 | **MISMATCH** |

### M15-012 — `ManipulationBehaviors.cs 0.349066`

`ManipulationBehaviors.cs:135` supplies `-0.349066f`, binary32 `0xBEB2B8C7`; the native post-put-down head action uses `0xBEB2B8C2` at `0x005C81A6..0x005C81BE`. **MISMATCH, five ULP.**

### M6-010 — Wwise fast-power polynomial

The three rounded literals in `WwiseGain.cs:620` miss the words loaded by the native fast `dBToLin` polynomial (`0x00A15254..0x00A1525C`, also used at `0x00A4DA68..0x00A4DA74`):

| Baseline literal | C# binary32 | Engine binary32 | Result |
|---|---|---|---|
| `0.3251898f` | `0x3EA67F47` | `0x3EA67F46` (0.325189769268...) | **MISMATCH** |
| `0.0208058f` | `0x3CAA70ED` | `0x3CAA70DE` (0.020805772394...) | **MISMATCH** |
| `0.6530434f` | `0x3F272DDA` | `0x3F272DDB` (0.653043448925...) | **MISMATCH** |

### M6-002 / M6-022 — non-table Wwise literals that match

| File/literal | C# bits | Engine source | Engine bits/width | Result |
|---|---|---|---|---|
| `WwiseVorbisNative.cs 0.70710677f` | `0x3F3504F3` | NEON IMDCT unit trig | `0x3F3504F3`, f32 | MATCH |
| `WwiseMixer.cs 0.70710677f` | `0x3F3504F3` | stereo-to-mono panner entry `0x00FFA970` | `0x3F3504F3`, f32 | MATCH |
| `WwiseModulatorEvaluator.cs 0.9999966f` | `0x3F7FFFC7` | `0x009E4014` / pool `0x00A1529C` | `0x3F7FFFC7`, f32 | MATCH |
| `WwiseModulatorEvaluator.cs 0.16664828f` (negative in use) | `0x3E2AA5D9` | `0x009E4010` / pool `0x00A15298` | `0x3E2AA5D9` (`0xBE2AA5D9` signed), f32 | MATCH |
| `WwiseModulatorEvaluator.cs 0.008306325f` | `0x3C081741` | `0x009E3324` / pool `0x00A15294` | `0x3C081741`, f32 | MATCH |
| `WwiseModulatorEvaluator.cs 0.00018363654f` (negative in use) | `0x39408E8F` | `0x009E3310` / pool `0x00A15290` | `0x39408E8F` (`0xB9408E8F` signed), f32 | MATCH |
| `WwiseVoiceBusEngine.cs 1.0009619f` | `0x3F801F85` | meter gain `0x00A50FD4` | `0x3F801F85`, f32 | MATCH |

### M6-002 curve evaluator — double-width substitutions

The native SCurve coefficients and scaling clamp are binary32:

| Baseline literal | C# binary64 | Narrowed C# f32 | Native value and evidence | Result |
|---|---:|---:|---|---|
| `0.0196138` | `0x3F9415A3D6337DDC` | `0x3CA0AD1F` | `0x3CA0AD34` at `0x00A152A4` | **MISMATCH (value and width)** |
| `0.2476748` | `0x3FCFB3CECF058C31` | `0x3E7D9E76` | `0x3E7D9E76` at `0x00A152A8` | **MISMATCH (width)** |
| `764.616` | `0x4087E4ED916872B0` | `0x443F276D` | positive/negative clamps `0x443F2770`/`0xC43F2770` constructed at `0x00A14F98..0x00A14FCC` and `0x00A15060..0x00A15074` | **MISMATCH (value and width)** |

`WwiseHierarchy.cs:93-109` evaluates all three in binary64, whereas the native path is binary32 throughout.

### M5-020 — neutral-face asset literals

`ProceduralFace.cs:338,344` contains `9.169666f` (`0x4112B6F4`), `1.214333f` (`0x3F9B6F44`), `-10.206374f` (`0xC1234D4F`) and `1.222037f` (`0x3F9C6BB5`). The extracted shipped `anim_singlepose_01.bin` entry `anim_neutral_eyes_01` contains those same little-endian words at file offsets `0xF0`, `0xF8`, `0xA0`, and `0xA8`, respectively. The native loader and C# both consume them as binary32, so all four **MATCH** the shipped asset value and width.

## Remaining 31 current non-table rows

The table below closes every current non-table row not already detailed above. `C# bits` are the bits of the literal at its declared C# type; where the C# type is `double`, the parenthesised word is the value after the production cast to binary32. A width difference is a mismatch even when that later narrowing happens to recover the native word.

| Owning record / baseline row | C# type and bits | Native evidence and bits/width | Result |
|---|---|---|---|
| unowned tall-stack parameter in the `M15-012`-tagged `CubeGameBehaviors.cs`: `0.436332` | f64 `0x3FDBECDD0D8CB07D` (f32 `0x3EDF66E8`) | `BehaviorCantHandleTallStack::TransitionToLookingUpAndDown` `0x005ECEFE..0x005ECF08`: `0xBEDF66F3`, f32 (magnitude `0x3EDF66F3`) | **MISMATCH** |
| same path: `0.785398` | f64 `0x3FE921FAFC8B007A` (f32 `0x3F490FD8`) | `0x005ECF58..0x005ECF62`: `0x3F490FDB`, f32 | **MISMATCH** |
| unowned `OnConfigSeen` parameter in `CubeGameBehaviors.cs`: `4.99999` | f64 `0x4013FFFD60E94EE4` (f32 `0x409FFFEB`) | load at `0x005DB37A`, pool `0x005DB3A4`: `0x409FFFEB`, f32 | **MISMATCH (width)** |
| unowned `InteractWithFaces` parameter in `FaceBehaviors.cs`: `0.0698132` | f64 `0x3FB1DF4722D4405F` (f32 `0x3D8EFA39`) | `BehaviorInteractWithFaces::TransitionToDrivingForward` `0x005C25D4..0x005C25FC`: `0x3D8EFA35`, f32 | **MISMATCH** |
| unowned `DriveToFace` parameter in `FaceBehaviors.cs`: `166.667f` | f32 `0x4326AAC1` | `SetDecel` argument at `0x005DA9CC..0x005DA9D6`: `0x4326AAAB`, f32 | **MISMATCH** |
| M7-009 `IdleBehavior.cs`: `57.295780f` | f32 `0x42652EE1` | load/use `0x0057D838`, pool `0x0057DB2C`: `0x42652EE1`, f32 | MATCH |
| M7-020 `Mood.cs`: `3.4028235e38` | f64 `0x47EFFFFFE54DAFF8` (f32 `0x7F7FFFFF`) | first-event result load `0x0067BEA4`, pool `0x0067BED8`: `0x7F7FFFFF`, f32 | **MISMATCH (width)** |
| M10-003 ObjectPositionUpdated in `ObjectBehaviors.cs`: `0.785398` | f64 `0x3FE921FAFC8B007A` (f32 `0x3F490FD8`) | ctor `0x00612182..0x0061218A`: `0x3F490FDB`, f32 | **MISMATCH** |
| unowned `AcknowledgeObject` parameter in the M10-003-tagged file: `0.0872665` | f64 `0x3FB65718EB895076` (f32 `0x3DB2B8C7`) | native pan/tilt setup uses `0x3DB2B8C2`, f32 | **MISMATCH** |
| unowned `AcknowledgeObject` parameter: `0.785398` | f64 `0x3FE921FAFC8B007A` (f32 `0x3F490FD8`) | native maximum turn uses `0x3F490FDB`, f32 | **MISMATCH** |
| M13-008 `ChargerActions.cs`: `0.0349066` | f64 `0x3FA1DF4722D4405F` (f32 `0x3D0EFA39`) | `ConfigureAlignWithChargerAction` begins at `0x0054E1C4`; its head tolerance is constructed at `0x0054E26A..0x0054E276` as `0x3D0EFA35`, f32 | **MISMATCH** |
| unowned docking rotation gate in the M12-tagged `Docking.cs`: `22.9183` | f64 `0x4036EB15B573EAB3` (f32 `0x41B758AE`) | `WasBodyRotatingTooFast` argument `0x0063BEA6..0x0063BEB8`: `0x41B758B4`, f32 degrees | **MISMATCH** |
| M12-023 `DriveActions.cs`: `0.174533` | f64 `0x3FC65718EB895076` (f32 `0x3E32B8C7`) | ctor goal tolerance `0x3E32B8C2`, f32 | **MISMATCH** |
| M12-023 `DriveActions.cs`: `0.261799` (negative in use) | f64 `0x3FD0C15097C80842` | init head target `0xBE860A92`, f32 (magnitude `0x3E860A92`) | **MISMATCH (value and width)** |
| M13-002 `FlipBlockAction.cs`: `0.0872665` | f64 `0x3FB65718EB895076` (f32 `0x3DB2B8C7`) | request setup at `0x0055EE10..0x0055EE3C`, with `0x3DB2B8C2` constructed at `0x0055EE20..0x0055EE2C`, f32 | **MISMATCH** |
| local `StraightLinePlanner` parameter in the M12-002-tagged `RobotPath.cs`: `0.0349066` | f64 `0x3FA1DF4722D4405F` (f32 `0x3D0EFA39`) | native `TurnInPlaceAction` tolerance `0x3D0EFA35`, f32 | **MISMATCH** |
| M10-001 `OffTreads.cs`: `0.261799` | f64 `0x3FD0C15097C80842` | pool `0x005122B0`: f64 `0x3FD0C15240000000` (the exact widening of f32 `0x3E860A92`) | **MISMATCH** |
| M10-001: `0.785398f` | f32 `0x3F490FD8` | pool `0x005122BC`: `0x3F490FDB`, f32 | **MISMATCH** |
| M10-001: `1.30027` | f64 `0x3FF4CDE7EA5F84CB` | pool `0x005122A8`: f64 `0x3FF4CDE840000000` | **MISMATCH** |
| M10-001: `1.39626f` (negative in use) | f32 `0x3FB2B8A6` | extreme-angle construction/use `0x00511E66..0x00511F04`: `0xBFB2B8C2`, f32 | **MISMATCH** |
| M10-001: `1.91986f` | f32 `0x3FF5BDF9` | same native branch: `0x3FF5BE0B`, f32 | **MISMATCH** |
| M11-004 `BlockWorld.cs`: `0.174533` | f64 `0x3FC65718EB895076` (f32 `0x3E32B8C7`) | `CheckForUnobservedObjects` `0x00621C9A..0x00621CAE`: `0x3E32B8C2`, f32 | **MISMATCH** |
| M13-020 head range in `CameraModel.cs`: `0.436332` (negative in use) | f64 `0x3FDBECDD0D8CB07D` (f32 magnitude `0x3EDF66E8`) | `TurnTowardsPoseAction::Init`, minimum `0xBEDF66F3`, f32 | **MISMATCH** |
| M13-020: `0.776672` | f64 `0x3FE8DA7F3CF70154` (f32 `0x3F46D3FA`) | same init, maximum `0x3F46D3F2`, f32 | **MISMATCH** |
| M11-005 `CornerRefinement.cs`: `0.003921569f` | f32 `0x3B808081` | multiply `0x008C6116`, pool `0x008C6474`: `0x3B808081`, f32 | MATCH |
| M11-005: `1.1920929e-07f` | f32 `0x34000000` | compare `0x0088DF50`, pool `0x0088E1B0`: `0x34000000`, f32 | MATCH |
| M11-005: `1.4142135f` | f32 `0x3FB504F3` | RefineQuadrilateral site `0x008C5840`, pool `0x008C5C18`: `0x3FB504F3`; but the earlier kernel-factor site `0x00898F98`, pool `0x00899258`, is `0x3FB50481` | **MISMATCH (mixed uses)** |
| M13-020 `FaceActions.cs`: `0.0872665` | f64 `0x3FB65718EB895076` (f32 `0x3DB2B8C7`) | native default pan tolerance `0x3DB2B8C2`, f32 | **MISMATCH** |
| M14-001 `Faces.cs`: `0.174533` | f64 `0x3FC65718EB895076` (f32 `0x3E32B8C7`) | rotation gate uses `0x3E32B8C2`, f32 | **MISMATCH** |
| M14-001: `0.523599` | f64 `0x3FE0C152B0A6FC59` (f32 `0x3F060A96`) | rotation gate uses `0x3F060A92`, f32 | **MISMATCH** |
| M11 map-pose threshold in `MemoryMap.cs`: `0.349066` | f64 `0x3FD65718EB895076` (f32 `0x3EB2B8C7`) | `MapComponent::UpdateRobotPose` constructs `0x3EB2B8C2` at `0x0067E244..0x0067E24E`, f32 | **MISMATCH** |
| M11-017 `MemoryMap.cs`: `6.00001` | f64 `0x401800029F16B11C` (f32 `0x40C00015`) | compare `0x0067FA14`, pool `0x0067FCD0`: `0x40C00015`, f32 | **MISMATCH (width)** |

The 31 rows therefore add **3 MATCH, 27 MISMATCH, and 1 mixed-use MISMATCH**. They also expose six literals in fidelity-tagged files whose actual behavior is not owned by the nearby manifest record: the two `CantHandleTallStack` angles, `OnConfigSeen` time, `InteractWithFaces` tolerance, `DriveToFace` deceleration, and the docking rotation gate. That is the same “source-backed pieces inside an unowned path” pattern the calibration found.

## Baseline integrity and final result

The fixture is not the stated 437-entry set at HEAD. It has 432 physical lines: two comments and **430 data rows**. Of those, **427 still occur in source** and three are stale (`DockActions.cs 0.523599`, `DriveActions.cs 0.523599`, `FaceActions.cs 0.785398`). The live 427 rows divide into 87 OpenCV table rows, 273 Wwise Vorbis table rows, and 67 non-table rows. Every row is accounted for above.

Across the 427 live entries, **374 match**, **52 mismatch**, and **one distinct baseline literal is mixed** (`CornerRefinement.cs 1.4142135`: one native use matches and one does not). Counting that mixed row as a failing baseline entry gives **53 failing entries**. The mismatch groups are: M5-020 none; M6-002/M6-010/M6 curve evaluator; M7-009/M7-020 plus unowned behavior constants; M10-001/M10-002/M10-003; M11-004/M11-005/M11-006/M11-008/M11-015/M11-017 plus the map-pose threshold; M12-001/M12-010/M12-012/M12-019/M12-023; M13-002/M13-008/M13-020 plus the local planner; M14-001/M14-003; and M15-012 plus its unowned tall-stack parameters.

This source-file reconciliation is the completeness check for all 430 fixture rows. “Mixed” is kept separate from “mismatch” so the totals do not conceal the two native uses of that one token.

| Baseline source file | Rows | MATCH | MISMATCH | MIXED | STALE |
|---|---:|---:|---:|---:|---:|
| `Animation/OpenCv310.cs` | 87 | 87 | 0 | 0 | 0 |
| `Animation/ProceduralFace.cs` | 4 | 4 | 0 | 0 | 0 |
| `Animation/Wwise/WwiseGain.cs` | 3 | 0 | 3 | 0 | 0 |
| `Animation/Wwise/WwiseHierarchy.cs` | 3 | 0 | 3 | 0 | 0 |
| `Animation/Wwise/WwiseMixer.cs` | 1 | 1 | 0 | 0 | 0 |
| `Animation/Wwise/WwiseModulatorEvaluator.cs` | 4 | 4 | 0 | 0 | 0 |
| `Animation/Wwise/WwiseVoiceBusEngine.cs` | 1 | 1 | 0 | 0 | 0 |
| `Animation/Wwise/WwiseVorbisNative.cs` | 1 | 1 | 0 | 0 | 0 |
| `Animation/Wwise/WwiseVorbisNative.Windows.cs` | 273 | 273 | 0 | 0 | 0 |
| `Behavior/CubeGameBehaviors.cs` | 3 | 0 | 3 | 0 | 0 |
| `Behavior/FaceBehaviors.cs` | 2 | 0 | 2 | 0 | 0 |
| `Behavior/IdleBehavior.cs` | 1 | 1 | 0 | 0 | 0 |
| `Behavior/ManipulationBehaviors.cs` | 2 | 0 | 2 | 0 | 0 |
| `Behavior/Mood.cs` | 1 | 0 | 1 | 0 | 0 |
| `Behavior/ObjectBehaviors.cs` | 2 | 0 | 2 | 0 | 0 |
| `Manipulation/ChargerActions.cs` | 1 | 0 | 1 | 0 | 0 |
| `Manipulation/DockActions.cs` | 1 | 0 | 0 | 0 | 1 |
| `Manipulation/Docking.cs` | 2 | 0 | 2 | 0 | 0 |
| `Manipulation/DriveActions.cs` | 3 | 0 | 2 | 0 | 1 |
| `Manipulation/FlipBlockAction.cs` | 1 | 0 | 1 | 0 | 0 |
| `Manipulation/PreActionPose.cs` | 1 | 0 | 1 | 0 | 0 |
| `Manipulation/RobotPath.cs` | 1 | 0 | 1 | 0 | 0 |
| `Manipulation/SearchActions.cs` | 6 | 0 | 6 | 0 | 0 |
| `OffTreads.cs` | 5 | 0 | 5 | 0 | 0 |
| `UnexpectedMovement.cs` | 1 | 0 | 1 | 0 | 0 |
| `Vision/BlockWorld.cs` | 4 | 0 | 4 | 0 | 0 |
| `Vision/CameraModel.cs` | 2 | 0 | 2 | 0 | 0 |
| `Vision/CornerRefinement.cs` | 3 | 2 | 0 | 1 | 0 |
| `Vision/FaceActions.cs` | 5 | 0 | 4 | 0 | 1 |
| `Vision/Faces.cs` | 2 | 0 | 2 | 0 | 0 |
| `Vision/MemoryMap.cs` | 2 | 0 | 2 | 0 | 0 |
| `Vision/VisionSystem.cs` | 2 | 0 | 2 | 0 | 0 |
| **Total** | **430** | **374** | **52** | **1** | **3** |

## Exhaustive words for the 360 direct-table rows

The source address is the table range named in “Direct table checks”; each row below gives the engine word and the C# word explicitly.

### OpenCv310.cs exhaustive words

| Literal | C# f32 | Engine f32 | Result |
|---:|---:|---:|---|
| `0.0174524` | `0x3C8EF856` | `0x3C8EF856` | MATCH |
| `0.0348995` | `0x3D0EF2C7` | `0x3D0EF2C7` | MATCH |
| `0.0697565` | `0x3D8EDC7F` | `0x3D8EDC7F` | MATCH |
| `0.0871557` | `0x3DB27EB0` | `0x3DB27EB0` | MATCH |
| `0.1045285` | `0x3DD6130A` | `0x3DD6130A` | MATCH |
| `0.1218693` | `0x3DF9969D` | `0x3DF9969D` | MATCH |
| `0.1391731` | `0x3E0E8365` | `0x3E0E8365` | MATCH |
| `0.1564345` | `0x3E20305E` | `0x3E20305E` | MATCH |
| `0.1736482` | `0x3E31D0D5` | `0x3E31D0D5` | MATCH |
| `0.1908090` | `0x3E43636F` | `0x3E43636F` | MATCH |
| `0.2079117` | `0x3E54E6CE` | `0x3E54E6CE` | MATCH |
| `0.2249511` | `0x3E665995` | `0x3E665995` | MATCH |
| `0.2419219` | `0x3E77BA60` | `0x3E77BA60` | MATCH |
| `0.2588190` | `0x3E8483ED` | `0x3E8483ED` | MATCH |
| `0.2756374` | `0x3E8D2058` | `0x3E8D2058` | MATCH |
| `0.2923717` | `0x3E95B1BE` | `0x3E95B1BE` | MATCH |
| `0.3090170` | `0x3E9E377A` | `0x3E9E377A` | MATCH |
| `0.3255682` | `0x3EA6B0E0` | `0x3EA6B0E0` | MATCH |
| `0.3420201` | `0x3EAF1D42` | `0x3EAF1D42` | MATCH |
| `0.3583679` | `0x3EB77BFF` | `0x3EB77BFF` | MATCH |
| `0.3746066` | `0x3EBFCC70` | `0x3EBFCC70` | MATCH |
| `0.3907311` | `0x3EC80DE8` | `0x3EC80DE8` | MATCH |
| `0.4067366` | `0x3ED03FC8` | `0x3ED03FC8` | MATCH |
| `0.4226183` | `0x3ED8616D` | `0x3ED8616D` | MATCH |
| `0.4383711` | `0x3EE0722D` | `0x3EE0722D` | MATCH |
| `0.4539905` | `0x3EE87171` | `0x3EE87171` | MATCH |
| `0.4694716` | `0x3EF05E95` | `0x3EF05E95` | MATCH |
| `0.4848096` | `0x3EF838F7` | `0x3EF838F7` | MATCH |
| `0.5150381` | `0x3F03D989` | `0x3F03D989` | MATCH |
| `0.5299193` | `0x3F07A8CB` | `0x3F07A8CB` | MATCH |
| `0.5446390` | `0x3F0B6D76` | `0x3F0B6D76` | MATCH |
| `0.5591929` | `0x3F0F2744` | `0x3F0F2744` | MATCH |
| `0.5735764` | `0x3F12D5E7` | `0x3F12D5E7` | MATCH |
| `0.5877853` | `0x3F167919` | `0x3F167919` | MATCH |
| `0.6018150` | `0x3F1A108C` | `0x3F1A108C` | MATCH |
| `0.6156615` | `0x3F1D9BFE` | `0x3F1D9BFE` | MATCH |
| `0.6293204` | `0x3F211B24` | `0x3F211B24` | MATCH |
| `0.6427876` | `0x3F248DBA` | `0x3F248DBA` | MATCH |
| `0.6560590` | `0x3F27F37C` | `0x3F27F37C` | MATCH |
| `0.6691306` | `0x3F2B4C25` | `0x3F2B4C25` | MATCH |
| `0.6819984` | `0x3F2E9772` | `0x3F2E9772` | MATCH |
| `0.6946584` | `0x3F31D522` | `0x3F31D522` | MATCH |
| `0.7071068` | `0x3F3504F4` | `0x3F3504F4` | MATCH |
| `0.7193398` | `0x3F3826A7` | `0x3F3826A7` | MATCH |
| `0.7313537` | `0x3F3B39FF` | `0x3F3B39FF` | MATCH |
| `0.7431448` | `0x3F3E3EBD` | `0x3F3E3EBD` | MATCH |
| `0.7547096` | `0x3F4134A6` | `0x3F4134A6` | MATCH |
| `0.7660444` | `0x3F441B7C` | `0x3F441B7C` | MATCH |
| `0.7771460` | `0x3F46F30A` | `0x3F46F30A` | MATCH |
| `0.7880108` | `0x3F49BB13` | `0x3F49BB13` | MATCH |
| `0.7986355` | `0x3F4C7360` | `0x3F4C7360` | MATCH |
| `0.8090170` | `0x3F4F1BBD` | `0x3F4F1BBD` | MATCH |
| `0.8191520` | `0x3F51B3F2` | `0x3F51B3F2` | MATCH |
| `0.8290376` | `0x3F543BCF` | `0x3F543BCF` | MATCH |
| `0.8386706` | `0x3F56B31E` | `0x3F56B31E` | MATCH |
| `0.8480481` | `0x3F5919AE` | `0x3F5919AE` | MATCH |
| `0.8571673` | `0x3F5B6F51` | `0x3F5B6F51` | MATCH |
| `0.8660254` | `0x3F5DB3D7` | `0x3F5DB3D7` | MATCH |
| `0.8746197` | `0x3F5FE714` | `0x3F5FE714` | MATCH |
| `0.8829476` | `0x3F6208DB` | `0x3F6208DB` | MATCH |
| `0.8910065` | `0x3F641901` | `0x3F641901` | MATCH |
| `0.8987940` | `0x3F66175D` | `0x3F66175D` | MATCH |
| `0.9063078` | `0x3F6803CA` | `0x3F6803CA` | MATCH |
| `0.9135455` | `0x3F69DE1E` | `0x3F69DE1E` | MATCH |
| `0.9205049` | `0x3F6BA636` | `0x3F6BA636` | MATCH |
| `0.9271839` | `0x3F6D5BED` | `0x3F6D5BED` | MATCH |
| `0.9335804` | `0x3F6EFF20` | `0x3F6EFF20` | MATCH |
| `0.9396926` | `0x3F708FB2` | `0x3F708FB2` | MATCH |
| `0.9455186` | `0x3F720D82` | `0x3F720D82` | MATCH |
| `0.9510565` | `0x3F737870` | `0x3F737870` | MATCH |
| `0.9563048` | `0x3F74D064` | `0x3F74D064` | MATCH |
| `0.9612617` | `0x3F76153F` | `0x3F76153F` | MATCH |
| `0.9659258` | `0x3F7746EA` | `0x3F7746EA` | MATCH |
| `0.9702957` | `0x3F78654D` | `0x3F78654D` | MATCH |
| `0.9743701` | `0x3F797052` | `0x3F797052` | MATCH |
| `0.9781476` | `0x3F7A67E2` | `0x3F7A67E2` | MATCH |
| `0.9816272` | `0x3F7B4BEC` | `0x3F7B4BEC` | MATCH |
| `0.9848078` | `0x3F7C1C5D` | `0x3F7C1C5D` | MATCH |
| `0.9876883` | `0x3F7CD924` | `0x3F7CD924` | MATCH |
| `0.9902681` | `0x3F7D8236` | `0x3F7D8236` | MATCH |
| `0.9925462` | `0x3F7E1782` | `0x3F7E1782` | MATCH |
| `0.9945219` | `0x3F7E98FD` | `0x3F7E98FD` | MATCH |
| `0.9961947` | `0x3F7F069E` | `0x3F7F069E` | MATCH |
| `0.9975641` | `0x3F7F605C` | `0x3F7F605C` | MATCH |
| `0.9986295` | `0x3F7FA62F` | `0x3F7FA62F` | MATCH |
| `0.9993908` | `0x3F7FD813` | `0x3F7FD813` | MATCH |
| `0.9998477` | `0x3F7FF605` | `0x3F7FF605` | MATCH |

### WwiseVorbisNative.Windows.cs exhaustive words

| Literal | C# f32 | Engine f32 | Result |
|---:|---:|---:|---|
| `0.0000113197` | `0x373DE9BE` | `0x373DE9BE` | MATCH |
| `0.0000147849` | `0x37780CA9` | `0x37780CA9` | MATCH |
| `0.0000187121` | `0x379CF7EE` | `0x379CF7EE` | MATCH |
| `0.0000231014` | `0x37C1C9E1` | `0x37C1C9E1` | MATCH |
| `0.0000279526` | `0x37EA7BC0` | `0x37EA7BC0` | MATCH |
| `0.0000332659` | `0x380B86FD` | `0x380B86FD` | MATCH |
| `0.0000390412` | `0x3823C02B` | `0x3823C02B` | MATCH |
| `0.0000452785` | `0x383DE96B` | `0x383DE96B` | MATCH |
| `0.0000519777` | `0x385A02A1` | `0x385A02A1` | MATCH |
| `0.0000667623` | `0x388C02BD` | `0x388C02BD` | MATCH |
| `0.0000748476` | `0x389CF780` | `0x389CF780` | MATCH |
| `0.0000833949` | `0x38AEE44C` | `0x38AEE44C` | MATCH |
| `0.0000924041` | `0x38C1C913` | `0x38C1C913` | MATCH |
| `0.0001018753` | `0x38D5A5E3` | `0x38D5A5E3` | MATCH |
| `0.0001118085` | `0x38EA7ABB` | `0x38EA7ABB` | MATCH |
| `0.0001222036` | `0x390023C7` | `0x390023C7` | MATCH |
| `0.0001330607` | `0x390B8636` | `0x390B8636` | MATCH |
| `0.0001443798` | `0x391764A8` | `0x391764A8` | MATCH |
| `0.0001561608` | `0x3923BF18` | `0x3923BF18` | MATCH |
| `0.0001684037` | `0x39309586` | `0x39309586` | MATCH |
| `0.0001811086` | `0x393DE7F8` | `0x393DE7F8` | MATCH |
| `0.0001942754` | `0x394BB668` | `0x394BB668` | MATCH |
| `0.0002079041` | `0x395A00D5` | `0x395A00D5` | MATCH |
| `0.0002219947` | `0x3968C740` | `0x3968C740` | MATCH |
| `0.0002515616` | `0x3983E407` | `0x3983E407` | MATCH |
| `0.0002670379` | `0x398C0138` | `0x398C0138` | MATCH |
| `0.0002829761` | `0x39945C69` | `0x39945C69` | MATCH |
| `0.0002993761` | `0x399CF594` | `0x399CF594` | MATCH |
| `0.0003162380` | `0x39A5CCBF` | `0x39A5CCBF` | MATCH |
| `0.0003335617` | `0x39AEE1E5` | `0x39AEE1E5` | MATCH |
| `0.0003513472` | `0x39B83506` | `0x39B83506` | MATCH |
| `0.0003695946` | `0x39C1C626` | `0x39C1C626` | MATCH |
| `0.0003883038` | `0x39CB9541` | `0x39CB9541` | MATCH |
| `0.0004074748` | `0x39D5A258` | `0x39D5A258` | MATCH |
| `0.0004271076` | `0x39DFED69` | `0x39DFED69` | MATCH |
| `0.0004472021` | `0x39EA7673` | `0x39EA7673` | MATCH |
| `0.0004677584` | `0x39F53D78` | `0x39F53D78` | MATCH |
| `0.0004887765` | `0x3A00213C` | `0x3A00213C` | MATCH |
| `0.0005102563` | `0x3A05C2B8` | `0x3A05C2B8` | MATCH |
| `0.0005321979` | `0x3A0B8332` | `0x3A0B8332` | MATCH |
| `0.0005546011` | `0x3A1162A6` | `0x3A1162A6` | MATCH |
| `0.0005774661` | `0x3A176118` | `0x3A176118` | MATCH |
| `0.0006007928` | `0x3A1D7E86` | `0x3A1D7E86` | MATCH |
| `0.0006245811` | `0x3A23BAEE` | `0x3A23BAEE` | MATCH |
| `0.0006488311` | `0x3A2A1651` | `0x3A2A1651` | MATCH |
| `0.0006735427` | `0x3A3090AF` | `0x3A3090AF` | MATCH |
| `0.0006987160` | `0x3A372A09` | `0x3A372A09` | MATCH |
| `0.0007243509` | `0x3A3DE25E` | `0x3A3DE25E` | MATCH |
| `0.0007504474` | `0x3A44B9AC` | `0x3A44B9AC` | MATCH |
| `0.0007770054` | `0x3A4BAFF3` | `0x3A4BAFF3` | MATCH |
| `0.0008040251` | `0x3A52C536` | `0x3A52C536` | MATCH |
| `0.0008315063` | `0x3A59F971` | `0x3A59F971` | MATCH |
| `0.0008594490` | `0x3A614CA5` | `0x3A614CA5` | MATCH |
| `0.0008878533` | `0x3A68BED4` | `0x3A68BED4` | MATCH |
| `0.0009167191` | `0x3A704FFA` | `0x3A704FFA` | MATCH |
| `0.0009758351` | `0x3A7FCF2F` | `0x3A7FCF2F` | MATCH |
| `0.0010060853` | `0x3A83DE9F` | `0x3A83DE9F` | MATCH |
| `0.0010367969` | `0x3A87E522` | `0x3A87E522` | MATCH |
| `0.0010679699` | `0x3A8BFB20` | `0x3A8BFB20` | MATCH |
| `0.0010996044` | `0x3A90209A` | `0x3A90209A` | MATCH |
| `0.0011317002` | `0x3A94558F` | `0x3A94558F` | MATCH |
| `0.0011642574` | `0x3A9899FF` | `0x3A9899FF` | MATCH |
| `0.0011972759` | `0x3A9CEDEA` | `0x3A9CEDEA` | MATCH |
| `0.0012307558` | `0x3AA15150` | `0x3AA15150` | MATCH |
| `0.0012646969` | `0x3AA5C430` | `0x3AA5C430` | MATCH |
| `0.0012990994` | `0x3AAA468B` | `0x3AAA468B` | MATCH |
| `0.0013339631` | `0x3AAED860` | `0x3AAED860` | MATCH |
| `0.0013692880` | `0x3AB379AE` | `0x3AB379AE` | MATCH |
| `0.0014050742` | `0x3AB82A77` | `0x3AB82A77` | MATCH |
| `0.0014413216` | `0x3ABCEABA` | `0x3ABCEABA` | MATCH |
| `0.0014780301` | `0x3AC1BA76` | `0x3AC1BA76` | MATCH |
| `0.0015151998` | `0x3AC699AB` | `0x3AC699AB` | MATCH |
| `0.0015528307` | `0x3ACB885A` | `0x3ACB885A` | MATCH |
| `0.0015909226` | `0x3AD08681` | `0x3AD08681` | MATCH |
| `0.0016294757` | `0x3AD59422` | `0x3AD59422` | MATCH |
| `0.0016684898` | `0x3ADAB13A` | `0x3ADAB13A` | MATCH |
| `0.0017079650` | `0x3ADFDDCC` | `0x3ADFDDCC` | MATCH |
| `0.0017479011` | `0x3AE519D4` | `0x3AE519D4` | MATCH |
| `0.0017882983` | `0x3AEA6555` | `0x3AEA6555` | MATCH |
| `0.0018291565` | `0x3AEFC04F` | `0x3AEFC04F` | MATCH |
| `0.0018704756` | `0x3AF52ABF` | `0x3AF52ABF` | MATCH |
| `0.0019122556` | `0x3AFAA4A7` | `0x3AFAA4A7` | MATCH |
| `0.0019544965` | `0x3B001703` | `0x3B001703` | MATCH |
| `0.0019971983` | `0x3B02E36D` | `0x3B02E36D` | MATCH |
| `0.0020403610` | `0x3B05B794` | `0x3B05B794` | MATCH |
| `0.0020839845` | `0x3B089375` | `0x3B089375` | MATCH |
| `0.0021726138` | `0x3B0E6269` | `0x3B0E6269` | MATCH |
| `0.0022176196` | `0x3B11557C` | `0x3B11557C` | MATCH |
| `0.0022630861` | `0x3B145049` | `0x3B145049` | MATCH |
| `0.0023090133` | `0x3B1752D1` | `0x3B1752D1` | MATCH |
| `0.0023554012` | `0x3B1A5D13` | `0x3B1A5D13` | MATCH |
| `0.0024022497` | `0x3B1D6F10` | `0x3B1D6F10` | MATCH |
| `0.0024495588` | `0x3B2088C7` | `0x3B2088C7` | MATCH |
| `0.0024973285` | `0x3B23AA38` | `0x3B23AA38` | MATCH |
| `0.0025455588` | `0x3B26D364` | `0x3B26D364` | MATCH |
| `0.0025942495` | `0x3B2A0449` | `0x3B2A0449` | MATCH |
| `0.0026434008` | `0x3B2D3CE8` | `0x3B2D3CE8` | MATCH |
| `0.0026930125` | `0x3B307D41` | `0x3B307D41` | MATCH |
| `0.0027430847` | `0x3B33C553` | `0x3B33C553` | MATCH |
| `0.0027936173` | `0x3B37151F` | `0x3B37151F` | MATCH |
| `0.0028446103` | `0x3B3A6CA4` | `0x3B3A6CA4` | MATCH |
| `0.0028960636` | `0x3B3DCBE2` | `0x3B3DCBE2` | MATCH |
| `0.0029479772` | `0x3B4132DA` | `0x3B4132DA` | MATCH |
| `0.0030003511` | `0x3B44A18A` | `0x3B44A18A` | MATCH |
| `0.0030531853` | `0x3B4817F3` | `0x3B4817F3` | MATCH |
| `0.0031064797` | `0x3B4B9615` | `0x3B4B9615` | MATCH |
| `0.0031602342` | `0x3B4F1BEF` | `0x3B4F1BEF` | MATCH |
| `0.0032144490` | `0x3B52A981` | `0x3B52A981` | MATCH |
| `0.0032691238` | `0x3B563ECC` | `0x3B563ECC` | MATCH |
| `0.0033242588` | `0x3B59DBCF` | `0x3B59DBCF` | MATCH |
| `0.0033798538` | `0x3B5D808A` | `0x3B5D808A` | MATCH |
| `0.0034359088` | `0x3B612CFC` | `0x3B612CFC` | MATCH |
| `0.0034924239` | `0x3B64E126` | `0x3B64E126` | MATCH |
| `0.0035493989` | `0x3B689D08` | `0x3B689D08` | MATCH |
| `0.0036068338` | `0x3B6C60A1` | `0x3B6C60A1` | MATCH |
| `0.0036647286` | `0x3B702BF1` | `0x3B702BF1` | MATCH |
| `0.0037230833` | `0x3B73FEF9` | `0x3B73FEF9` | MATCH |
| `0.0038411721` | `0x3B7BBC2D` | `0x3B7BBC2D` | MATCH |
| `0.0039009061` | `0x3B7FA658` | `0x3B7FA658` | MATCH |
| `0.0039610999` | `0x3B81CC1D` | `0x3B81CC1D` | MATCH |
| `0.0040217533` | `0x3B83C8E9` | `0x3B83C8E9` | MATCH |
| `0.0040828664` | `0x3B85C991` | `0x3B85C991` | MATCH |
| `0.0041444391` | `0x3B87CE13` | `0x3B87CE13` | MATCH |
| `0.0042064714` | `0x3B89D671` | `0x3B89D671` | MATCH |
| `0.0042689632` | `0x3B8BE2A9` | `0x3B8BE2A9` | MATCH |
| `0.0043319145` | `0x3B8DF2BC` | `0x3B8DF2BC` | MATCH |
| `0.0043953253` | `0x3B9006A9` | `0x3B9006A9` | MATCH |
| `0.0044591954` | `0x3B921E71` | `0x3B921E71` | MATCH |
| `0.0045235250` | `0x3B943A14` | `0x3B943A14` | MATCH |
| `0.0045883139` | `0x3B965991` | `0x3B965991` | MATCH |
| `0.0046535621` | `0x3B987CE9` | `0x3B987CE9` | MATCH |
| `0.0047192696` | `0x3B9AA41A` | `0x3B9AA41A` | MATCH |
| `0.0047854363` | `0x3B9CCF26` | `0x3B9CCF26` | MATCH |
| `0.0048520622` | `0x3B9EFE0C` | `0x3B9EFE0C` | MATCH |
| `0.0049191472` | `0x3BA130CC` | `0x3BA130CC` | MATCH |
| `0.0049866914` | `0x3BA36766` | `0x3BA36766` | MATCH |
| `0.0050546946` | `0x3BA5A1DA` | `0x3BA5A1DA` | MATCH |
| `0.0051231569` | `0x3BA7E028` | `0x3BA7E028` | MATCH |
| `0.0051920781` | `0x3BAA224F` | `0x3BAA224F` | MATCH |
| `0.0052614583` | `0x3BAC6850` | `0x3BAC6850` | MATCH |
| `0.0053312973` | `0x3BAEB22A` | `0x3BAEB22A` | MATCH |
| `0.0054015953` | `0x3BB0FFDE` | `0x3BB0FFDE` | MATCH |
| `0.0054723520` | `0x3BB3516A` | `0x3BB3516A` | MATCH |
| `0.0055435676` | `0x3BB5A6D1` | `0x3BB5A6D1` | MATCH |
| `0.0056152418` | `0x3BB80010` | `0x3BB80010` | MATCH |
| `0.0056873748` | `0x3BBA5D28` | `0x3BBA5D28` | MATCH |
| `0.0057599664` | `0x3BBCBE1A` | `0x3BBCBE1A` | MATCH |
| `0.0058330166` | `0x3BBF22E4` | `0x3BBF22E4` | MATCH |
| `0.0059804926` | `0x3BC3F802` | `0x3BC3F802` | MATCH |
| `0.0060549184` | `0x3BC66856` | `0x3BC66856` | MATCH |
| `0.0061298026` | `0x3BC8DC83` | `0x3BC8DC83` | MATCH |
| `0.0062051451` | `0x3BCB5488` | `0x3BCB5488` | MATCH |
| `0.0062809460` | `0x3BCDD065` | `0x3BCDD065` | MATCH |
| `0.0063572052` | `0x3BD0501A` | `0x3BD0501A` | MATCH |
| `0.0064339226` | `0x3BD2D3A8` | `0x3BD2D3A8` | MATCH |
| `0.0065110982` | `0x3BD55B0D` | `0x3BD55B0D` | MATCH |
| `0.0065887320` | `0x3BD7E64A` | `0x3BD7E64A` | MATCH |
| `0.0066668239` | `0x3BDA755F` | `0x3BDA755F` | MATCH |
| `0.0067453738` | `0x3BDD084C` | `0x3BDD084C` | MATCH |
| `0.0068243817` | `0x3BDF9F10` | `0x3BDF9F10` | MATCH |
| `0.0069038476` | `0x3BE239AC` | `0x3BE239AC` | MATCH |
| `0.0069837715` | `0x3BE4D81F` | `0x3BE4D81F` | MATCH |
| `0.0070641531` | `0x3BE77A69` | `0x3BE77A69` | MATCH |
| `0.0071449926` | `0x3BEA208B` | `0x3BEA208B` | MATCH |
| `0.0072262899` | `0x3BECCA83` | `0x3BECCA83` | MATCH |
| `0.0073080449` | `0x3BEF7853` | `0x3BEF7853` | MATCH |
| `0.0073902575` | `0x3BF229F9` | `0x3BF229F9` | MATCH |
| `0.0074729278` | `0x3BF4DF76` | `0x3BF4DF76` | MATCH |
| `0.0075560556` | `0x3BF798CA` | `0x3BF798CA` | MATCH |
| `0.0076396410` | `0x3BFA55F4` | `0x3BFA55F4` | MATCH |
| `0.0077236838` | `0x3BFD16F5` | `0x3BFD16F5` | MATCH |
| `0.0078081841` | `0x3BFFDBCC` | `0x3BFFDBCC` | MATCH |
| `0.0078931417` | `0x3C01523C` | `0x3C01523C` | MATCH |
| `0.0079785566` | `0x3C02B87E` | `0x3C02B87E` | MATCH |
| `0.0080644288` | `0x3C0420AA` | `0x3C0420AA` | MATCH |
| `0.0081507582` | `0x3C058AC2` | `0x3C058AC2` | MATCH |
| `0.0082375447` | `0x3C06F6C4` | `0x3C06F6C4` | MATCH |
| `0.0083247884` | `0x3C0864B1` | `0x3C0864B1` | MATCH |
| `0.0084124891` | `0x3C09D489` | `0x3C09D489` | MATCH |
| `0.0085892615` | `0x3C0CB9F9` | `0x3C0CB9F9` | MATCH |
| `0.0086783330` | `0x3C0E2F91` | `0x3C0E2F91` | MATCH |
| `0.0087678614` | `0x3C0FA713` | `0x3C0FA713` | MATCH |
| `0.0088578466` | `0x3C112080` | `0x3C112080` | MATCH |
| `0.0089482885` | `0x3C129BD8` | `0x3C129BD8` | MATCH |
| `0.0090391871` | `0x3C141919` | `0x3C141919` | MATCH |
| `0.0091305422` | `0x3C159845` | `0x3C159845` | MATCH |
| `0.0092223540` | `0x3C17195B` | `0x3C17195B` | MATCH |
| `0.0093146223` | `0x3C189C5C` | `0x3C189C5C` | MATCH |
| `0.0094073470` | `0x3C1A2146` | `0x3C1A2146` | MATCH |
| `0.0095005281` | `0x3C1BA81A` | `0x3C1BA81A` | MATCH |
| `0.0095941655` | `0x3C1D30D9` | `0x3C1D30D9` | MATCH |
| `0.0096882592` | `0x3C1EBB81` | `0x3C1EBB81` | MATCH |
| `0.0097828092` | `0x3C204813` | `0x3C204813` | MATCH |
| `0.0098778153` | `0x3C21D68F` | `0x3C21D68F` | MATCH |
| `0.0099732775` | `0x3C2366F5` | `0x3C2366F5` | MATCH |
| `0.0101655700` | `0x3C268D7E` | `0x3C268D7E` | MATCH |
| `0.0107533880` | `0x3C302EFA` | `0x3C302EFA` | MATCH |
| `0.0110534480` | `0x3C351985` | `0x3C351985` | MATCH |
| `0.0121887200` | `0x3C47B332` | `0x3C47B332` | MATCH |
| `0.0122946560` | `0x3C496F86` | `0x3C496F86` | MATCH |
| `0.0133790090` | `0x3C5B33A2` | `0x3C5B33A2` | MATCH |
| `0.0139382130` | `0x3C645D1A` | `0x3C645D1A` | MATCH |
| `0.0146242260` | `0x3C6F9A73` | `0x3C6F9A73` | MATCH |
| `0.0160451800` | `0x3C83712E` | `0x3C83712E` | MATCH |
| `0.0172790660` | `0x3C8D8CD4` | `0x3C8D8CD4` | MATCH |
| `0.0175312640` | `0x3C8F9DBA` | `0x3C8F9DBA` | MATCH |
| `0.0225218930` | `0x3CB87FD5` | `0x3CB87FD5` | MATCH |
| `0.0229534910` | `0x3CBC08F6` | `0x3CBC08F6` | MATCH |
| `0.0245704880` | `0x3CC9480C` | `0x3CC9480C` | MATCH |
| `0.0247201710` | `0x3CCA81F5` | `0x3CCA81F5` | MATCH |
| `0.0270189760` | `0x3CDD56E6` | `0x3CDD56E6` | MATCH |
| `0.0310727950` | `0x3CFE8C60` | `0x3CFE8C60` | MATCH |
| `0.0334638860` | `0x3D09116D` | `0x3D09116D` | MATCH |
| `0.0336379900` | `0x3D09C7FD` | `0x3D09C7FD` | MATCH |
| `0.0354032640` | `0x3D110303` | `0x3D110303` | MATCH |
| `0.0357616000` | `0x3D127AC1` | `0x3D127AC1` | MATCH |
| `0.0373958560` | `0x3D192C66` | `0x3D192C66` | MATCH |
| `0.0385050960` | `0x3D1DB785` | `0x3D1DB785` | MATCH |
| `0.0386915020` | `0x3D1E7AFB` | `0x3D1E7AFB` | MATCH |
| `0.0427069140` | `0x3D2EED72` | `0x3D2EED72` | MATCH |
| `0.0436912760` | `0x3D32F5A0` | `0x3D32F5A0` | MATCH |
| `0.0440880610` | `0x3D3495AF` | `0x3D3495AF` | MATCH |
| `0.0448868370` | `0x3D37DB43` | `0x3D37DB43` | MATCH |
| `0.0515259060` | `0x3D530CD4` | `0x3D530CD4` | MATCH |
| `0.0523867590` | `0x3D569380` | `0x3D569380` | MATCH |
| `0.0528197550` | `0x3D585987` | `0x3D585987` | MATCH |
| `0.0536908800` | `0x3D5BEAF8` | `0x3D5BEAF8` | MATCH |
| `0.0586039880` | `0x3D700ABC` | `0x3D700ABC` | MATCH |
| `0.0602103410` | `0x3D769F1E` | `0x3D769F1E` | MATCH |
| `0.0613704170` | `0x3D7B5F8C` | `0x3D7B5F8C` | MATCH |
| `0.0620715250` | `0x3D7E3EB6` | `0x3D7E3EB6` | MATCH |
| `0.0644359070` | `0x3D83F6F9` | `0x3D83F6F9` | MATCH |
| `0.0758496620` | `0x3D9B5711` | `0x3D9B5711` | MATCH |
| `0.0794969160` | `0x3DA2CF47` | `0x3DA2CF47` | MATCH |
| `0.0864823050` | `0x3DB11DA2` | `0x3DB11DA2` | MATCH |
| `0.0889639840` | `0x3DB632C0` | `0x3DB632C0` | MATCH |
| `0.0909166480` | `0x3DBA3282` | `0x3DBA3282` | MATCH |
| `0.0911972000` | `0x3DBAC599` | `0x3DBAC599` | MATCH |
| `0.0945949110` | `0x3DC1BAFA` | `0x3DC1BAFA` | MATCH |
| `0.0966033140` | `0x3DC5D7F5` | `0x3DC5D7F5` | MATCH |
| `0.1139816300` | `0x3DE96F33` | `0x3DE96F33` | MATCH |
| `0.1205924300` | `0x3DF6F92A` | `0x3DF6F92A` | MATCH |
| `0.1383461700` | `0x3E0DAA9E` | `0x3E0DAA9E` | MATCH |
| `0.1403845300` | `0x3E0FC0F6` | `0x3E0FC0F6` | MATCH |
| `0.1872218600` | `0x3E3FB716` | `0x3E3FB716` | MATCH |
| `0.1926349000` | `0x3E454215` | `0x3E454215` | MATCH |
| `0.2448206500` | `0x3E7AB244` | `0x3E7AB244` | MATCH |
| `0.3786708100` | `0x3EC1E124` | `0x3EC1E124` | MATCH |
| `0.3970277400` | `0x3ECB4738` | `0x3ECB4738` | MATCH |
| `0.4439293300` | `0x3EE34AB5` | `0x3EE34AB5` | MATCH |
| `0.5722153800` | `0x3F127CB5` | `0x3F127CB5` | MATCH |
| `0.5731780200` | `0x3F12BBCB` | `0x3F12BBCB` | MATCH |
| `0.5837361800` | `0x3F156FBC` | `0x3F156FBC` | MATCH |
| `0.6065536400` | `0x3F1B4719` | `0x3F1B4719` | MATCH |
| `0.6220421700` | `0x3F1F3E28` | `0x3F1F3E28` | MATCH |
| `0.6285587300` | `0x3F20E93A` | `0x3F20E93A` | MATCH |
| `0.8241096700` | `0x3F52F8DA` | `0x3F52F8DA` | MATCH |
| `0.8358304700` | `0x3F55F8FC` | `0x3F55F8FC` | MATCH |
| `0.8953300500` | `0x3F65345A` | `0x3F65345A` | MATCH |
| `0.9344109300` | `0x3F6F358E` | `0x3F6F358E` | MATCH |
| `0.9544018300` | `0x3F7453AE` | `0x3F7453AE` | MATCH |
| `0.9644616100` | `0x3F76E6F5` | `0x3F76E6F5` | MATCH |
| `0.9709428000` | `0x3F788FB5` | `0x3F788FB5` | MATCH |
| `0.9787962000` | `0x3F7A9263` | `0x3F7A9263` | MATCH |
| `0.9895048100` | `0x3F7D5030` | `0x3F7D5030` | MATCH |
| `0.9981435600` | `0x3F7F8656` | `0x3F7F8656` | MATCH |
| `0.9996981000` | `0x3F7FEC37` | `0x3F7FEC37` | MATCH |
| `0.9997463500` | `0x3F7FEF60` | `0x3F7FEF60` | MATCH |
| `0.9999738900` | `0x3F7FFE4A` | `0x3F7FFE4A` | MATCH |
| `0.9999980900` | `0x3F7FFFE0` | `0x3F7FFFE0` | MATCH |
| `0.9999999000` | `0x3F7FFFFE` | `0x3F7FFFFE` | MATCH |
| `0.9999999500` | `0x3F7FFFFF` | `0x3F7FFFFF` | MATCH |
| `0.9999999600` | `0x3F7FFFFF` | `0x3F7FFFFF` | MATCH |
