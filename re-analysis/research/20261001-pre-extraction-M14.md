# Q22 — M14 face gaps, with the actual OKAO boundary

Date: 2026-10-01. Scope: every current `IMPLEMENTATION_GAP` or `RECOVERABLE_GAP` in `M14-faces` at HEAD. There are six, all `IMPLEMENTATION_GAP`: M14-007 through M14-012. This is extraction only; it changes neither code nor manifest.

## Boundary finding

The request's premise that the inside of OKAO is `BLOCKED_EXTERNAL` does **not** hold for the shipped package. `libcozmoEngine.so` defines 177 `OKAO_*` and 30 `OMCV_*` dynamic symbols and contains their ARM bodies in `.text`; they are not unresolved imports. For example, `OKAO_DT_Detect_GRAY` is at `0x009067C8..0x0090681B`, `OKAO_DT_GetResultCount` at `0x00906920..0x0090692F`, `OKAO_DT_GetRawResultInfo` at `0x00906958..0x0090697F`, `OKAO_FR_ExtractHandle_GRAY` at `0x0092C8E4..0x0092CA37`, and `OKAO_FR_Identify` at `0x0092DDB8..0x0092DEE7`. Thus:

- **Anki engine side:** the `Anki::Vision::*` callers, scheduling, gates, output conversion, lifetime, world handoff and NV orchestration, principally `0x004Fxxxx`, `0x0065xxxx`, `0x006Bxxxx` and `0x0086xxxx`.
- **Vendor-origin but still shipped and recoverable:** the called `OKAO_*`/`OMCV_*` bodies around `0x0090xxxx..0x0093xxxx`. Their source provenance is third-party, but their runtime behavior is primary source under the project's “exact, always” rule.
- **Truly external:** only facts the package never contains (for example, source-level vendor names or unshipped training provenance). No scoped runtime operation can be called `BLOCKED_EXTERNAL` merely because its symbol begins `OKAO_`.

This also corrects the inventory's old phrase “dynamic symbols (imports)” and the C# comments at `Faces.cs:37-56` that say the library is unavailable. The manifest already has the same stale premise in M14-010/M14-012; those records remain implementation gaps, but the missing behavior is recoverable.

## Complete ABI boundary used by these six records

The addresses in the “call site” column are Anki-side calls. AAPCS places the first four words in `r0-r3` and further arguments on the stack. “UNKNOWN” means the current instruction extraction did not settle a type/layout; it is not permission to invent one.

| operation | Anki call site | data into OKAO | data back / Anki failure handling |
| --- | --- | --- | --- |
| `OKAO_DT_Detect_GRAY` | `0x0086D776` | detector handle plus grayscale image description; exact remaining word layout **UNKNOWN** | OKAO status; non-success takes the detector failure path before enumeration. Vendor body `0x009067C8..0x0090681B`. |
| `OKAO_DT_GetResultCount` | `0x0086D7FC` | detector/result handle | integer result count; body `0x00906920..0x0090692F`. |
| `OKAO_DT_GetRawResultInfo` | `0x0086D898`, `0x0086D958` | detector result handle and zero-based result index; exact output-structure type **UNKNOWN** | raw centre/size/pose/tracking fields consumed to construct `TrackedFace`; body `0x00906958..0x0090697F`; a failed item is not appended. |
| `OKAO_CO_ConvertCenterToSquare` | `0x0086D998` | raw centre-form face rectangle; exact ABI words **UNKNOWN** | square-form rectangle copied into the new tracked face; failure semantics **UNKNOWN** from the present row. |
| `OKAO_PT_SetPositionFromHandle` | `0x0086CCC6` | parts handle plus detector-result position handle | status gates the parts pass. |
| `OKAO_PT_DetectPoint_GRAY` | `0x0086CD28` | parts handle, grayscale image and dimensions/stride; exact trailing ABI **UNKNOWN** | status; on failure the parts-valid output remains false. |
| `OKAO_PT_GetResult` | `0x0086CD46` | parts result handle and point-output storage | facial landmark coordinates/confidences; individual per-point threshold values remain **UNKNOWN** in the current extraction. |
| `OKAO_PT_GetFaceDirection` | `0x0086CF32` | parts result handle | signed face-direction angles used by Anki; Anki multiplies with the single-precision constant `0x3C8EFA35`. |
| `OKAO_EX_SetPointFromHandle` | `0x0086D1F8` | expression handle plus parts result handle | status gates expression estimation. |
| `OKAO_EX_Estimate_GRAY` | `0x0086D24C` | expression handle plus grayscale image description; exact tail **UNKNOWN** | status gates result retrieval. |
| `OKAO_EX_GetResult` | `0x0086D2A2` | expression result handle plus five signed-integer output slots | five values; Anki converts each to float and associates the fixed five-entry expression-type table, with no 0.01/0.001 gate. |
| `OKAO_SM_SetPointFromHandle` | `0x0086D3DA` | smile handle plus parts result handle | status gates estimation. |
| `OKAO_SM_Estimate` | `0x0086D404` | smile handle plus image/result context; exact ABI **UNKNOWN** | status gates retrieval. |
| `OKAO_SM_GetResult` | `0x0086D430` | smile result handle plus two signed-integer outputs | success sets valid, storing output 1 times `0x3C23D70A` (0.01f) and output 2 times `0x3A83126F` (0.001f), at `0x0086D474..0x0086D4A0`. |
| `OKAO_GB_SetPointFromHandle` | `0x0086D562` | gaze/blink handle plus parts result handle | status gates estimation. |
| `OKAO_GB_Estimate` | `0x0086D58C` | gaze/blink handle plus image/result context; exact ABI **UNKNOWN** | status gates the two independently enabled outputs. |
| `OKAO_GB_GetGazeDirection` | `0x0086D5E6` | result handle plus two signed-integer output slots | success sets gaze-valid and stores both converted integers unscaled. |
| `OKAO_GB_GetEyeCloseRatio` | `0x0086D638` | result handle plus two signed-integer output slots | success sets blink-valid and multiplies each by `0x3A83126F` (0.001f), at `0x0086D622..0x0086D680`. |
| `OKAO_FR_ExtractHandle_GRAY` | vendor body `0x0092C8E4..0x0092CA37`; Anki caller in the recognition-preparation path still needs instruction-level isolation | grayscale face crop/landmarks and feature handle; exact Anki call ABI **UNKNOWN** | opaque `OkaoFaceFeature` consumed by `RecognizeFace`; this is the missing carrier in C#. |
| `OKAO_FR_GetRegisteredUserNum` | `0x00864100`, `0x00863072` | `(album, int* count)` | zero OKAO return is success; failure propagates according to caller (recognition logs and returns 1 before matching; sanity check returns failure). |
| `OKAO_FR_Identify` | `0x00864222..0x00864254` | `(feature, album, 10, int* ids, int* scores, int* count)` | body `0x0092DDB8..0x0092DEE7`; failure is logged and `RecognizeFace` returns 0/no face; count above ten is diagnosed and clamped to ten. |
| `OKAO_FR_RegisterData` | `0x00865CB8`, `0x008656xx`, `0x00868BE4` | `(album, feature, albumEntry, dataIndex)` | nonzero/error causes the registering/updating/copying operation to return 1. |
| `OKAO_FR_GetRegisteredUsrDataNum` | `0x00865B98` | `(album, albumEntry, int* count)` | error makes `UpdateExistingAlbumEntry` return 1. |
| `OKAO_FR_ClearUser` | `0x00863ABA`, `0x00866B26` | `(album, albumEntry)` | merge treats failure as fatal; ordinary `RemoveUser` logs it but continues erasing Anki bookkeeping. |
| `OKAO_FR_Verify` | `0x00865D94`, `0x00865E28` | `(feature, album, albumEntry, int* score)` | errors in the four-entry replacement algorithm return 1. |
| `OKAO_FR_GetFeatureFromAlbum` | `0x00865E06`, `0x00868BD4` | `(album, albumEntry, dataIndex, feature*)` | returns an opaque feature; any replacement/copy failure returns 1. |
| `OKAO_FR_ClearData` | `0x00865E16` | `(album, albumEntry, dataIndex)` | failure returns 1; successful temporary removal is restored before continuing. |
| `OKAO_FR_IsRegistered` | `0x0086535E`, `0x008652E2`, `0x008630A0`, `0x00868BC0` | `(album, albumEntry, dataIndex, int* result)` | distinguishes free/occupied slots; OKAO call failure returns -1 or rejects album install, depending on caller. |
| `OKAO_FR_RestoreAlbum` | `0x0086739A` | `(common, const void* bytes, unsigned size, albumHandle*)` | restores a temporary live album from NV bytes; failure/null result rejects install. |
| `OKAO_FR_GetAlbumMaxNum` | `0x00868948`, `0x0086896E` | `(album, int* albumMax, int* dataMax)` | both capacity dimensions are checked; failure rejects install. |
| `OKAO_FR_ClearAlbum` | `0x00868B2A` | `(album)` | required before smaller-capacity entry-by-entry copy; failure rejects install. |
| `OKAO_FR_DeleteAlbumHandle` | `0x0086808C` | `(album)` | deletes the temporary restored handle on every applicable exit; no data output. |

This table is the complete set of distinct OKAO operations cited by the scoped production paths. Constructors/configuration and deeper vendor-to-vendor calls are inside shipped OKAO code and need a second extraction if the replacement is to reproduce the vendor implementation itself, rather than provide an ABI-compatible engine seam.

## M14-007 — map and face-approach ray

> **Current manifest:** “The memory map, and the question the face behaviour asks it before driving in”; `IMPLEMENTATION_GAP`. Unresolved: the C# answers from polygons rather than the engine's 10 mm quad tree and must move onto M11-045/M11-046, then recompare the mask/family mapping and `-15/40` behavior.

| step | address | what it does | gates, order, failure, bits |
| --- | --- | --- | --- |
| 1 | `0x005C2420..0x005C24A4` | Builds local `(40,0,0)`, rotates/translates it, and asks the map ray virtual with the 11-pair type table. | `40.0f = 0x42200000`; returns logical negation of collision. |
| 2 | `0x0068176E..0x006817A4` | ORs `EContentTypeToFlag(type)` only for pairs whose enable byte is nonzero, then calls the quad-tree processor. | Enabled blockers are types 3,4,6,7,8,9,10; types 0,1,2,5 are clear. |
| 3 | `0x00685000`, M11-045/M11-046 rows | Quad tree subdivides/answers at its content precision. | `10.0f = 0x41200000`; this observable cell quantisation is the current C# defect. |
| 4 | `0x005C254E..0x005C25xx` | Chooses approach distance and constructs `DriveStraightAction`. | clear: `40.0f = 0x42200000`; blocked: `-15.0f = 0xC1700000`; speed `40.0f = 0x42200000`. Collision changes distance, not whether an action is created. |

C# entry: `Vision/MemoryMap.cs:27,91` and the face behavior caller. No OKAO call occurs. Whole-path dependency: M11-045/046 must be built first. The named test exercises the public map/behavior seam but its expected geometry was derived from the record while the production structure still differed; it cannot settle the record until the live quad tree is used.

## M14-008 — vision-result handoff and FaceWorld lifecycle

> **Current manifest:** “Vision face-result handoff and FaceWorld lifecycle broadcasts”; `IMPLEMENTATION_GAP`. Unresolved: lifecycle events are built, but the enrollment-reached count, recognizer-produced `UpdatedFaceID` list, and EngineToGame messages are missing.

| step | address | what it does | gates, order, failure, bits |
| --- | --- | --- | --- |
| 1 | `0x0086DBC2`, pushes `0x0086DC02/0x0086DCC4` | Recognition output is folded into `VisionProcessingResult+0x54` nodes `{i32 oldID,i32 newID,string name}`. | Occurs after detector/recognizer scheduling; missing feature carrier in M14-010 prevents this producer live in C#. |
| 2 | `0x006551E8..0x00655202` | `VisionComponent::UpdateFaces` consumes every UpdatedFaceID and calls `FaceWorld::ChangeFaceID`. | This list is processed **first**. |
| 3 | `0x00655204..0x0065526C` | Walks tracked faces and sets `FaceWorld+0x24` if node `+0x14 > 0`. | Exact count field is absent from `DetectedFace`; no plausible substitute. |
| 4 | `0x00655274`; `0x004F5316..0x004F54A8` | Calls `FaceWorld::Update`, add/update per tracked face, then expires unnamed faces after 15000 ms. | Ordered after steps 2/3. Named faces do not use the unnamed expiry. |
| 5 | `0x004F4B7A/82`, `0x004F3C2C..0x004F3C64` | Broadcasts observed/deleted messages; delete also removes visualization and map node. | EngineToGame wire is unowned in this record and absent in C#. |

C# entry: `Faces.cs:217,234,244,286`; frame entry `VisionSystem.cs:887`. The test at `FaceTests.cs:629` drives FaceWorld events, but does not produce the engine's feature → recognition → UpdatedFaceID path and cannot test EngineToGame, so it is partly circular/source-shaped.

## M14-009 — return eligibility and enrollment forwarding

> **Current manifest:** “Face return eligibility and enrollment forwarding”; `IMPLEMENTATION_GAP`. Unresolved says `ShouldReturnFace` and mode selection are built and the target reaches the M14-011 album owner.

| step | address | what it does | gates, order, failure, bits |
| --- | --- | --- | --- |
| 1 | `0x004F55B8..0x004F55E2` | Reject stale entry, optionally require id >= 1, then require `Robot::IsPoseInWorldOrigin(facePose)`. | Short-circuit order is stale → id → origin. No float literal in the path. |
| 2 | `0x004F5C9E..0x004F5CB2` | Chooses enrollment mode. | nonzero id → 4; zero id → -1. |
| 3 | `0x00657A58` → `0x006B3394` → `0x0086B276` → `0x0086E372` → `0x008658AC` | Forwards pose/id/mode through VisionComponent, VisionSystem, FaceTracker and Impl to `SetAllowedEnrollments`. | At endpoint stores count/mode, id and target track; missing id record logs/uses track 0. |

C# entries: `Faces.cs:345,367`, `VisionSystem.cs:445`, `FaceRecognizer.cs:339`. No OKAO call occurs during forwarding. The named test (`FaceTests.cs:659`) derives its cases from the cited gates and exercises the public methods; it does not prove the frame scheduler that later consumes the enrollment state.

## M14-010 — detector order, outputs and enrollability

> **Current manifest:** “FaceTracker OKAO call order, detector output handling and enrollment gates”; `IMPLEMENTATION_GAP`. Unresolved claims that nothing beyond the detector seam is buildable because the OKAO outputs are unavailable.

That last sentence is contradicted by the shipped OKAO bodies. The correct disposition is **recover and build**, not `BLOCKED_EXTERNAL`.

| step | address | what it does | gates, order, failure, bits |
| --- | --- | --- | --- |
| 1 | `0x0086D740..0x0086D998` | Detect → count → raw result per index → centre-to-square → append tracked face. | Exact call order is the first four rows of the ABI table. Failed result is not treated as a valid face. |
| 2 | `0x0086DA04..0x0086DB20` | Per face calls parts, expression, smile, then gaze/blink. | This order is behavior-changing; enabled flags gate the optional passes. |
| 3 | `0x0086CCC0..0x0086CFxx` | Parts/landmarks and face direction. | Direction scale uses `0x3C8EFA35`; per-point acceptance thresholds remain **UNKNOWN** and require reading the helper/vendor body. |
| 4 | `0x0086D1F8..0x0086D2FC` | Expression estimation and five-result conversion. | On successful result, five signed ints become floats; no 0.01/0.001 scaling. |
| 5 | `0x0086D3DA..0x0086D4A0` | Smile estimation/output. | Success only: valid flag, `0x3C23D70A` and `0x3A83126F` scale factors. |
| 6 | `0x0086D562..0x0086D680` | Gaze/blink estimation/output. | Separate enable/valid flags; gaze unscaled, eye-close times `0x3A83126F`. |
| 7 | `0x0086E074..0x0086E1AC`; `0x0086E36C` | Computes enrollability from geometry/direction. | Pixel limits 16/64/128/32; degree limits 25/45/10 in the tested order; minimum eye distance is `16.0f = 0x41800000`. |
| 8 | `0x0086DBA6`, `0x0086DBC2` | Sends next feature/track to recognizer, then gets recognition data for the result. | Requires the opaque feature from `OKAO_FR_ExtractHandle_GRAY`; absent in C# today. |

C# entry is `IFaceDetector`/`OkaoFaceDetector` at `Faces.cs:43-56` and the live frame branch at `VisionSystem.cs:887`. The current seam carries rectangle, eyes, expressions, name/score and roll, but not smile, gaze, blink, direction, enrollability count, UpdatedFaceID or opaque feature. `OkaoFaceDetector.IsAvailable == false` means the production branch never runs. `FaceTests` can test conversions/helpers only from code-supplied values; there is no live source-derived detector test.

## M14-011 — recognizer semantics and album ownership

> **Current manifest:** “FaceRecognizer matching, registration, update, merge, album and serialization semantics”; `IMPLEMENTATION_GAP`. Unresolved: the body is built over `IOkaoFaceRecognizer`, but the per-frame feature carrier and `UpdateRecognitionData` track-map semantics remain open; field `EnrolledFaceEntry+0x04` has an unknown source-level name.

The detailed Anki-side algorithm is already instruction-checked in inventory C3/C4/C5. The remaining production-path rows are:

| step | address | what it does | gates, order, failure, bits |
| --- | --- | --- | --- | --- |
| 1 | `0x00863384` | `UpdateRecognitionData` maintains track map `+0xDC`, current track `+0xAC`, and recognition data handed to the frame result. | Exact `+0xAC/+0x20` erase-key semantics remain **UNKNOWN**; this is not owned by the current C#. |
| 2 | `0x008640E4..0x00864CD8` | Recognizes: empty-album registration, Identify, confident/debug/lower-rank/target/new-user branches. | confident score is strictly >750; new-user requires top <550; success registration emits 1000. Identify error returns 0/no face; bookkeeping contradictions return 1. |
| 3 | `0x00865A98..0x0086600E` | Updates existing entry and optionally adds/replaces one of four features. | pre-update age gate is >999,999 us (`0x000F423F`); every OKAO mutation failure returns 1. |
| 4 | `0x008638AC..0x00863B9A` | Merges with limit 5, rewrites LUT, clears removed OKAO users, then removes merged Anki user. | clear failure in merge returns 1; ordinary remove merely logs clear failure. |
| 5 | `0x00867F54..0x00868366` | Restores album, parses enrollment, checks consistency/capacity, swaps or copies, installs maps, deletes temporary handle. | malformed/version/capacity/OKAO failures reject the whole install; equal capacity swaps, only strictly smaller copies. |

All OKAO calls and shapes are in the ABI table above. C# entry: `FaceRecognizer.cs:29,83,252,483,637,709,734,914`; frame integration comment at `VisionSystem.cs:902`. The unit tests in `FaceRecognizerTests.cs` use a fake `IOkaoFaceRecognizer` whose outputs are selected from the extraction, so they are valid algorithm tests but **circular at the vendor boundary** and do not exercise the shipped OKAO bodies or live frame feature carrier.

## M14-012 — NV persistence, replay and completion

> **Current manifest:** “Face album NV persistence, loaded-name replay and enrollment completion”; `IMPLEMENTATION_GAP`. Unresolved: orchestration is built, but album serialization is treated as raw bytes, EngineToGame broadcasts are absent, and the connection-time invocation belongs to M3-033.

| step | address | what it does | gates, order, failure, bits |
| --- | --- | --- | --- | --- |
| 1 | `0x006512EA`, `0x00651330` | Queues NV reads for album tag `0x184000`, then enrollment tag `0x183000`. | Production invocation is connection-time M3-033; absent queue invocation means the helper alone is not live. |
| 2 | `0x0065A876..0x0065A896` | Under vision mutex, passes both byte vectors down to `FaceRecognizer::SetSerializedData`. | Album is restored by `OKAO_FR_RestoreAlbum`; enrollment uses Anki container version `0x0002FACE`. Any install failure rejects adoption. |
| 3 | `0x00651594..0x00651612` | Broadcasts erased-all first, then each loaded known face. | EngineToGame messages are absent; local C# events are not wire-equivalent. |
| 4 | `0x00657180..0x0065720E` | Gets both serialized vectors and checks each against its NV maximum. | Oversize aborts save. Album bytes must come from OKAO serialization; current C# keeps raw input, so it has no live inverse. |
| 5 | `0x006573A0..0x006573EA`, `0x0065745A..` | Pads sizes to four-byte boundaries, then writes album before enrollment; empty pair erases album then enrollment. | Second operation occurs only after first enqueue succeeds. No invented padding content beyond the serialized vector. |
| 6 | `0x005FE2F4..0x005FE528` | Enrollment behavior stop constructs and broadcasts `FaceEnrollmentCompleted`. | EngineToGame layout/path missing. |

C# entries: `VisionSystem.cs:409,428,451,466,504`; OKAO endpoint `FaceRecognizer.cs:914`. `FaceTests.cs:683,718` derives tag/order/padding expectations from source but calls the helper directly and substitutes local events/raw bytes; it does not exercise connection startup, the OKAO serializer/restore inverse, or EngineToGame.

## Build order and ownership result

1. Build M11-045/046, then move M14-007's live map onto that quad tree.
2. Recover the shipped OKAO detector/feature bodies and extend the frame carrier (M14-010). This unlocks the per-frame M14-011 call and M14-008 UpdatedFaceID/enrollment-count production.
3. Recover `UpdateRecognitionData` `0x00863384` and the album serialization writer paired with `OKAO_FR_RestoreAlbum`; finish M14-011/M14-012.
4. Add the owning EngineToGame records/path for M14-008/M14-012, and M3-033's connection-time NV invocation.

Until those dependencies exist, none of the six records owns its whole live production path. The blocker is recoverable shipped code plus cross-record wiring—not hardware, and not a truly external OKAO runtime.
