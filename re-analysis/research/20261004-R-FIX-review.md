| Record | Commit scope | Coverage | Limit |
|---|---|---|---|
| M3-024 | c978ee9 | PARTIAL | CreateAudioAnimation599FF0..59A130 branch/order compared; firmware setter and on-device iterator/drraw paths remain unchecked. Postconstruction state4/5 deletion remains a declared gap. |
| M5-005 | c978ee9 | CHECKED | Changed entropy-seed path 0x0082F860..0x0082F8C8 and independent fixed-sequence tests compared. Scope is this diff. |
| M5-017 | c978ee9 | CHECKED | Same changed entropy-seed path and tests; unchanged probability selection is outside this diff. |
| M6-003 | c978ee9 | PARTIAL | Decoder geometry defect/test below; full source lifecycle not audited. |
| M10-002 | c978ee9 | CHECKED | Changed threshold literal 0x0063DAB0/B6 and its f32 consumer 0x0063E4B6..C2 compared; scope is this diff. |
| M10-009 | c978ee9 | PARTIAL | Predicate6143CA and constructor569A80 checked; claimed exhaustive absence of subsequent writers is not independently certified. |
| M10-010 | c978ee9 | CHECKED | Changed firmware-handler branches compared at5368F4..53698E; null promotion8EACCE..8EAD02 and nonobject throw8EADC2..8EAE64 checked. Existing JSON parser fidelity and declared outer exception-catching gap are not certified by this diff check. |
| M10-011 | c978ee9 | CHECKED | Changed fall-reward subscription compared with5350AA..5350C2 and later broadcast535186..53519C; threshold447A0000 at5352A8, ordinal17=Fall confirmed in shipped Unity enum. FreeplayTests boundary/order test checked; declared log-only gaps retained. |
| M11-002 | 4521eea | CHECKED | Changed nearest-row initialization and strict threshold branches0x008C098C/0x008C0A72/0x008C0B68..BC2 compared. Scope is this diff. |
| M11-003 | 4521eea | CHECKED | Changed AddFace literals/axis signs compared at0x004E5442..0x004E5608. Manifest retains the double-rotation gap; this checks the changed constants. |
| M11-006 | 4521eea | CHECKED | Changed constants checked: cluster angle MOVW625498/MOVT6254A6 yields3DB2B8C3; passed through6254E0. Flat angle MOVW505F10/MOVT505F16 yields3EB2B8C2, passed505F1A..505F22. Diff scope is these constants; declared shared double pose gap remains. |
| M11-008 | 4521eea | CHECKED | Changed normal-angle literal and locator zero-size/padding call compared at0x006220E2..22102 and0x0060BCF0..BD08. Unchanged padding gap is retained in manifest. |
| M11-010 | 4521eea | CHECKED | Changed visibility gates/order87E4A8..E87C, projection85E264..E418, field-of-view85E13C..E1C6 and normalization50E0C0..E130 compared. Diagonals then corner order0,2,1,3 and depth addition order match. Declared double pose, occluder and libm gaps retained; independent numeric/order tests inspected. This does not certify those shared gaps. |
| M11-015 | 4521eea | CHECKED | Changed constructor speed/tolerance bits0x00545A28..5AB0 compared with current TurnTowardsPose constants. Scope is the two changed constants. |
| M11-019 | 4521eea | CHECKED | Changed constructor/tread/frame-mismatch triggers51031E..328,512B88..BAA,512F14..F22/512F82..F9C compared; threshold65hex, reset before Delocalize, frame-match reset512EAE. Removed origin-change stand-in is absent from native triggers inspected. Independent live-message101st boundary test checked. Declared origin allocation/cliff hook gaps retained; exhaustive absence of indirect callers is not certified. |
| M11-020 | 4521eea | CHECKED | Changed kernel arithmetic8992BA..8993D2 checked: binary32 diagonals, averaged fractions, factor3FB50481, roundf then signed conversion, unclamped boxFilter call. Declared exception handling gap remains; boundary tests independently match recovered arithmetic. |
| M11-022 | 4521eea | CHECKED | Changed rectangle extent6ABB38..BBD4 checked: truncate coordinates then integer extrema, max-min with no+1. Marker append8754E0..5566 has no code/center deduplication in its loop. Diff scope is removal of extent+1 and merge; extraction bodies remain separate paths. |
| M11-023 | 4521eea | PARTIAL | Manifest/diff inventory only; independent instruction comparison remains open. |
| M11-029 | 4521eea | PARTIAL | Manifest/diff inventory only; independent instruction comparison remains open. |
| M11-031 | 4521eea | CHECKED | Removed decoder contrast gate compared against Extract0x008A0078..D0; refinement retains its separate brightness gate. Scope is this diff. |
| M11-036 | 4521eea | CHECKED | Changed frame gates654D60..6550AC compared: nonempty list requests computed state, failure log/drop, origin log/drop, per-marker timestamp comparison; kept list passed654DF4. Empty input still updates world; docking only nonempty kept list. C# changed gates/order and independent failed-history/origin tests inspected. Rotation, key-validity, silent6000000 drop, historical camera and docking/result interfaces remain declared gaps. |
| M11-039 | 4521eea | PARTIAL | Native pointer-identity/null gates0x0085DEDC..DF18 and caller0x006B1E3E..E80 compared; detector memory-reset equivalence remains unverified. |
| M11-054 | cf36fb5 | CHECKED | Bounded CameraModel addition compared with constructor50FF32..FFBA: neck C1500000/00000000/42440000; camera418C28F6/00000000/C1000000; robot+2CC parent passed in r3. Old vision translation explicitly retained as an unbuilt gap. No whole-solver acceptance or quaternion-composition fidelity inferred. |
| M12-001 | c978ee9 | PARTIAL | Flipping size.Y/f32 literal42624EEF and distance-threshold arithmetic0x00550102..1A8 compared; other offset-geometry changes remain unchecked. |
| M12-002 | c978ee9 | CHECKED | Changed u16 increment0x0064A3C2..3CA compared: wraps through0, no skip. Scope is this diff. |
| M12-007 | c978ee9 | PARTIAL | Manifest/diff inventory only; independent instruction comparison remains open. |
| M12-010 | c978ee9 | PARTIAL | Manifest/diff inventory only; independent instruction comparison remains open. |
| M12-012 | c978ee9 | CHECKED | Changed tolerance MOVW63C66C/MOVT63C670 forms3E32B8C2; passed into Radians and resting-flat call63C676/63C67E. Current widened-float constant and independent bit/boundary tests compared. Existing double resting-flat arithmetic remains declared gap; scope is this constant change. |
| M12-017 | c978ee9 | PARTIAL | Manifest/diff inventory only; independent instruction comparison remains open. |
| M12-019 | c978ee9 | CHECKED | Changed clamp tolerance MOVW63C182/MOVT63C188 forms3F32B8C2; Radians63C18C then clamp63C194. C# widened-float constant and independent boundary tests compared. Existing double clamp arithmetic remains declared gap. |
| M13-002 | 4521eea | CHECKED | Changed norm/drive addition55EEDA..EF1C and55F0EA..F124 compared: float squared products, sequential sums, sqrt, then float drive offset. Lift call tolerance40A00000 and ctor defaults41200000/41A00000 at548A50..A78 confirmed. Diff removes speed5 stand-in; independent numeric and wire tests inspected. Declared double GetPossiblePoses gap remains. |
| M13-003 | 4521eea | PARTIAL | Manifest/diff inventory only; independent instruction comparison remains open. |
| M13-004 | 4521eea | PARTIAL | Manifest/diff inventory only; independent instruction comparison remains open. |
| M13-005 | 4521eea | PARTIAL | Manifest/diff inventory only; independent instruction comparison remains open. |
| M13-008 | 4521eea | PARTIAL | Manifest/diff inventory only; independent instruction comparison remains open. |
| M13-009 | 4521eea | CHECKED | Changed constructor/pose literals4E9B6C..9C38, docked pose4EA1A0..A202, predock4E9FB0..EA122 and static initializer4D6BC4..BDE compared. Literal3FC90FDB at4EA184; unsigned actionType0/1 gate, one output, parent marker and translation(0,-250,-15.5) match. Declared quaternion/double pose and no-live-caller gaps retained; tests independently name bits/gates. |
| M13-011 | 4521eea | CHECKED | Changed PathSegment float storage/wire copies compared507F3A..508028: line four geometry words, arc five, point-turn four plus normalized byte, profile+1C/+20/+24. Tests inspect transmitted bit words for independent supplied segments. Declared producer/path-construction gaps remain. |
| M13-012 | 4521eea | CHECKED | Changed lift gate54E58A..E5C6 compared: binary32 height versus42340000, BPL skips equal/greater/unordered, constructor arguments42340000/40A00000/00000000. Current strict less-than and independent live-entry low/equal tests match. Declared M13-008 action-body gaps retained. |
| M13-013 | 4521eea | PARTIAL | Manifest/diff inventory only; independent instruction comparison remains open. |
| M13-015 | 4521eea | PARTIAL | Manifest/diff inventory only; independent instruction comparison remains open. |
| M13-016 | 4521eea | CHECKED | Changed constructor table553370..553440, pre-action table5532B8..3324, SelectDockAction5534CC..DE and Verify5534E0..35A2 compared. Binary32 clampC1800005 and result precedence04000003 then04000002 then04000003 then0 match; invalid dock0300001A. Independent bit/branch tests inspected. Live Verify inputs remain an explicit gap; scope is these changed bodies. |
| M13-017 | 4521eea | PARTIAL | Manifest/diff inventory only; independent instruction comparison remains open. |
| M13-018 | 4521eea | PARTIAL | Manifest/diff inventory only; independent instruction comparison remains open. |
| M13-021 | cf36fb5 | PARTIAL | Scalar loop518344..8620 and fallback54B428..B564 compared; output-order defectD6 at25th-pass convergence. Constants and scalar fallback match; caller fallback gate and tests inspected, shared pose algebra remains declared gap. Remaining TurnTowardsPose Init coverage open. |
| M14-001 | 4521eea | CHECKED | Changed eye-distance and camera translation arithmetic87DC68..DD98/87DE24..E070 compared; inverse calibration85EF3A..EF9E and normalization50E0C0..E130 checked. Bits3727C5AC/42780000, box zero-eye slots and arithmetic order match diff. Declared libm/camera-pose gaps retained; tests use independently stated numeric results with libm tolerance. |
| M14-003 | 4521eea | CHECKED | Changed pan-cap/acceleration565672..B4 plus constructor5646BC..4764 and head-speed5650E2..5104 compared:3D0EFA35,3F46D3F2,3E32B8C2, duration3E19999A/3ECCCCCD; head quotient unclamped and accel461C4000. Independent constants/pan boundary tests checked. Declared ActionList/tick, stop criteria and shared Radians/libm gaps retained. |
| M14-004 | 4521eea | CHECKED | Changed timestamp width/recent predicate: ctor0x006117A6, writer0x00611F28 and RecentlyReacted0x00611DD0..E14 compared. Scope is this diff. |
| M14-005 | 4521eea | CHECKED | Changed subtraction/atan2f/addition path0x0051879C..88AE compared. Existing MathF and double-Radians gaps remain explicitly recorded. Scope is the changed arithmetic. |
| M15-004 | c978ee9 | PARTIAL | Multiplier loop and damaged-part predicate0x0069C214..C2B8/CCAC..CCDC compared; config sort/parser not yet independently closed. Manifest already records NaN mismatch. |
| M15-008 | c978ee9, cf36fb5 | PARTIAL | Manifest/diff inventory only; independent instruction comparison remains open. |
| M15-009 | c978ee9, cf36fb5 | PARTIAL | FireEmotionEvents5E002C..0096 compared: AreAllCubesInBeacons()==1 selects29-byteHikingBroughtLastCubeToBeacon at5E00CC, else25-byteHikingBroughtCubeToBeacon at5E00B0; MoodManager stored-time getter thenevent. AreAllCubesInBeacons56B450..B61A and predicate56D20E..D258 compared: no beacon/carried cube false; known objects only, any beacon margin0, increment only return==1; compare count against BlockWorld+48 family2 map value (absent0). That map writer identity remains explicitly declared MISSING in C#. Floor route still partial inM15-019..024. |
| M15-011 | c978ee9 | CHECKED | Changed full-pose forwarding5E5F1A..5E5F30 and AddBeacon pose copy56C39C..C3E8 compared; center keeps translation including Z and rotation. Independent NavigationTests full-pose case inspected. Scope is this diff. |
| M15-012 | c978ee9 | PARTIAL | Native wait initialization/subscription/order defect D4; remaining put-down callback and action paths not fully reviewed. |
| M15-013 | c978ee9 | PARTIAL | Native selection/caller defect below; full tree construction not audited. |
| M15-015 | c978ee9 | PARTIAL | Native accumulator/update/pause branches56EC1A..EDDC/56EECC..EF14/56EFF8..F096 and timer getters checked; production-clock defectD5. Full configuration seeding still open. |
| M15-019 | c978ee9, cf36fb5 | PARTIAL | Manifest/diff inventory only; independent instruction comparison remains open. |
| M15-020 | c978ee9, cf36fb5 | PARTIAL | Manifest/diff inventory only; independent instruction comparison remains open. |
| M15-021 | c978ee9, cf36fb5 | PARTIAL | TryToPlaceAt5DFEDC..FFC0/compound ctor554AFC..BBAC and callback5E188C..1B60 compared: arguments0/0/1/41200000, retained pose/attempt, category4 carried-id gate and signedattempt<=2, increment before retry; failure log then located-object gate→SetFailedToUse(reason2, candidatepose). Tests independently supply categories/attempts. Shared compound tick/ignore-failure and timeout boundaries remain unclosed. |
| M15-022 | c978ee9, cf36fb5 | PARTIAL | Manifest/diff inventory only; independent instruction comparison remains open. |
| M15-023 | c978ee9, cf36fb5 | PARTIAL | NoFree branch5DF69C..F73A, floatstamp59C314..322 and cooldown5DF148..194 compared. Stamp→log→event order and24-byte event string match. Whiteboard rendering call closure/config binding remain unchecked; tests inspect live no-free order. |
| M15-024 | c978ee9, cf36fb5 | PARTIAL | SetFailedToUse56B6D0..B870 cap table56B8A4={1,1,10,1}, oldest eviction thenfloat stamp/append compared. FindMatchingEntry56BF98..C06C id-1 traversal and list order; set traversal56BAB4..BB26; EntryMatches56C070..C10A strict expiry/NaN branches andB727C5AC compared. C# direct bodies match these inspected decisions. Same-parent spherical gate847054..70D4 and angle bypass847132..7166 compared; GetAngleDiff84A694..A746 confirms declared quaternion-versus-matrix rounding gap. GetObjectFailureTable56B8F4..B968 unsigned enum gate and TBB dispatch to+14/+20/+2C/+38, invalid enum static map, compared. Cap/expiry/distance/angle tests inspected; they use independently named outcomes. Parent re-expression and quaternion fidelity remain the declared gaps. |
| M15-025 | c978ee9, cf36fb5 | CHECKED | Changed explicit needs-id1F call5DF59C..5A0 precedes pose search5DF5E0; success callback5E18AC..191A logs thenFireEmotionEvents and has no needs/failure-memory call. C# order and independently named no-free/success callback assertions inspected. Scope is this needs ordering diff. |
| M15-026 | cf36fb5 | PARTIAL | InitInternal5DF348..F3DA target reset, carrying branch, action pointer and float stamp result compared. Selection distance5DFB3A..B9C uses ordered binary32 squares/adds, best+B727C5AC strictGT replacement and finalBPL rejection. Allocation/start5DF95E..F9B2 and callback5E0FD8..1214 categories0/3/4, strict carried-id gate, signedattempt<=2, increment-before-retry and located-object-gated failure0 compared. Callback tests supply raw categories independently. No false completion claim for the declared DriveToPickup stand-in. Selection error branches5DF9C6..FA68 and spent-attempt tail5E121A..1288 now compared: log then return, or log then failure-record tail. GetCandidate5DFDA4..FDCE unsigned index bound and stored-family forwarding read. Candidate-vector reuse/family model and shared behavior-finish semantics still require comparison; declared action-body gap retained. |

# R-FIX and R-FIX2 — defects only, incomplete review

Pulled first. Reviewed commit identities `4521eea` (31 original records), `c978ee9` (22 original records plus seven new floor-path records), and additionally identified `cf36fb5` because it is an R-FIX2 build commit. The table lists every manifest record changed by those implementation commits, excluding subsystem review-state objects. This is 63 distinct records: the requested 53 original records, seven new M15 records, and three additional records touched/introduced by batch C (M13-021, M11-054, M15-026). Plan/claim commits and R-FIX3 are not reviewed implementation changes.

This is **not** a completed 53-record native review. Most table entries are only inventoried. No HOLDS conclusions appear below. Evidence is native instructions, current C# and the target commit diff; tests were read, not run. No code/manifest changes or commits. [Native companion](20261004-R-FIX-native.txt).

## DEFECT D1 — current activity identity is lost before the pick

Record quoted before comparison:

```json
{
  "id": "M15-013",
  "title": "Activity tree construction, desired names and discarded activityPriority",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "ActivityFreeplay::CreateFromConfig 0x005AD478",
    "ActivityStrictPriority constructor 0x005B23DC",
    "activities_config.json"
  ],
  "unresolved": "built, awaiting strong verification (R-FIX2, 2026-10-03): PickNewActivity is the engine's PickNewActivityForSpark (0x005ADC44): activities bucketed by required spark in config order (0x005AD676..0x005AD69E), only the requested spark's bucket is walked, +0x91 (forced) then +0x90 (desired from objects) take only that activity id without asking a strategy (0x005ADC6A..0x005ADC7E), otherwise the first activity whose strategy WantsToStart; ActivityIDFromString returns 0 for an unknown name (0x0076706C..0x007670D4); a missing bucket warns (0x005ADCD8). Still open: the engine's askCurrent argument is not a parameter here; FreeplaySystem.GetDesiredActiveBehavior loops up to SubActivities.Count passes where the engine re-picks at most once per tick (M15-002's)."
}
```

C# locations (current checkout): `cozmo-stack/src/Cozmo.Robot/Behavior/FreeplaySystem.cs:201`, `:205`, `:343`, `:386`, `:387`. The affected pick body was rewritten in `c978ee9`; the premature EndActivity integration was retained.

Engine behaviour: `PickNewActivityForSpark` retains `this+0x80` throughout candidate enumeration. At **0x005ADC88..0x005ADC90**, it compares candidate pointer against that current pointer. A different candidate calls WantsToStart (**0x005ADC92..0x005ADC9E**). The same candidate goes to **0x005ADCA2..0x005ADCB0**: with askCurrent=1 it calls WantsToEnd and accepts it only when false; with askCurrent !=1 it skips it. `OnDeselected` is reached only after a different selected pointer has been determined, in the later switch body; it is not performed before the scan.

C# first takes `(wantsEnd && behaviorFinished)` and calls EndActivity. EndActivity performs OnDeselected (which starts cooldown) and sets `Current=null`. `barred` remains null for this branch. PickNewActivity then walks the bucket and asks WantsToStart for every candidate, including the activity just ended.

Concrete divergent case: a current activity with WantsToEnd=true, WantsToStart=true after deselection (for example no cooldown), no running behaviour, no forced/desired override and no pending reward. Native skips that current candidate and can select the next one; C# can immediately reselect it. Even when cooldown prevents reselection, C# has changed the candidate's cooldown state before the engine's scan would deselect it. Preserve current-pointer identity and askCurrent through the native selection boundary. The record already acknowledges missing askCurrent; this finding gives the resulting branch defect, not a new claim that the record was settled.

## DEFECT D2 — the early end branch omits the pending reward gate

Same record/context quoted above; affected C# integration `FreeplaySystem.cs:201` (and unconditional WantsToEnd call at `:188`). Native **0x005AE356..0x005AE368** reaches the early WantsToEnd call only when the incoming behaviour is null **and** `NeedsManager+0x3D8` is zero. Nonzero pending reward branches directly to the current activity's desired-behaviour calculation at 0x005AE65A. WantsToEnd is called at **0x005AE376** only after those gates.

C# computes WantsToEnd immediately and ends the activity when wantsEnd && behaviorFinished, without a SparksRewardPending gate. A pending reward with an ended behaviour therefore can trigger EndActivity, which communicates/clears that reward and selects another activity; native preserves the activity through this early gate. The later C# reward check at `:250` does not repair the earlier EndActivity. This integration defect is in the production path claimed by the revised pick, although its branch predates the commit. Full M15-002 coverage is outside the 53 original records and remains open.

## DEFECT D3 — undersized ADPCM geometry is assigned an unsupported successful empty result; test blesses it

Current record quoted before comparison:

```json
{
  "id": "M6-003",
  "title": "IMA ADPCM exactly: header predictor is sample 0, 63 nibbles, diff ((2n+1)*step)>>3, interleaved int16, 64 frames per block",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "decoder 0x00A7A194..0x00A7A3C4 (step table 0x00FFD650, index 0x00FFD708)",
    "callers 0x00A725F8..0x00A72624, 0x00A73E88..0x00A73EAC, 0x00A740E0..0x00A7410C",
    "step table 0x00FFD650 = the standard IMA 89 i16 (7,8,9,...,32767); index table 0x00FFD708 = 16 i16 (-1,-1,-1,-1,2,4,6,8,-1,-1,-1,-1,2,4,6,8), 16-bit entries",
    "channel mapping half 0 = channel 0, half 1 = channel 1 (callers 0x00A72618, 0x00A73EA0, 0x00A74100; the 5th argument only sets the interleave stride channels*2 at 0x00A7A1C4)",
    "no source-side channel gate: only wFormatTag == 2 (0x00A72704, 0x00A73B2C); the 7 shipped stereo format-tag-2 media (blockAlign 72, 4 bits) decode and route"
  ],
  "unresolved": "built, awaiting strong verification (R-FIX2, 2026-10-03): the decoder is generic in the channel count (0x00A72618, 0x00A73EA0, 0x00A74100): channel c's block at c*36 inside each block with the file's blockAlign as the stride, the header step index unclamped with the engine's table window (167 words after the 89-entry step table, rodata 0x00FFD650), updates clamped to 0..88, a negative product shifted as (p+7)>>3 (0x00A7A240..0x00A7A24C); tests are Unicorn emulations of the shipped decoder FUN_00a7a194. Still open: a blockAlign below 36*channels or channels < 1 decodes to nothing where the engine reads out of bounds; channels == 0 (WwiseMedia.Parse accepts it) still divides by zero in WwiseVoiceSources.cs:369; WwiseAudioSource's pcm is now empty, not null, for such files."
}
```

Changed C# **`cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseAdpcm.cs:82`** returns an empty sample array when `blockAlign < 36*channels`. Its test **`cozmo-stack/tests/Cozmo.Protocol.Tests/RFix2BTests.cs:412`** asserts that result for channels=2, blockAlign=40, data length=80.

Native caller **0x00A725DC..0x00A72624** skips only the zero-channel loop. For each nonzero channel it computes source offset `c*36` at 0x00A725FC..0x00A72608, loads the file block stride at 0x00A72600 and calls decoder 0x00A7A194 at 0x00A72618. The decoder's per-channel block read is 36 bytes. This caller does not supply a “stride smaller than channel footprint => successful empty PCM” result. With the test's geometry, later channel/block reads cross the media buffer; the resulting native memory contents/fault are UNKNOWN. This finding does not assert the exact native PCM or that shipped media uses that geometry.

The manifest **already discloses** the divergence; no stronger manifest status is contradicted. The defect is the false source-behaviour assertion in the test's summary (“a block too small ... decode[s] to nothing”), and treating the chosen guard as a recovered decoder result. The assertion mirrors that local guard rather than an independently established native result. Separate the known zero-channel caller gate from the unknown out-of-bounds case. Its claim that StartStream/Render “cannot be broken” also is not exercised by this direct DecodeAdpcm test; the current manifest separately discloses zero-channel division in the voice-source adapter.

No additional defects or circular tests are asserted by this incomplete pass. Full record-by-record instruction review, numeric dependencies and regression-oracle independence remain open in the coverage table.

## DEFECT D4 — PutDownBlock counts images from before the wait starts

Current record, quoted before contradiction:

```json
{
  "id": "M15-012",
  "subsystem": "M15-freeplay",
  "title": "Put-down image wait and CantHandleTallStack animation trigger",
  "location": "cozmo-stack/src/Cozmo.Robot/Behavior/ManipulationBehaviors.cs",
  "effect": "the post-put-down action sequence and tall-stack reaction animation change",
  "provenance": "libcozmoEngine.so",
  "authority": "libcozmoEngine.so",
  "evidence": [
    "BehaviorPutDownBlock::CreateLookAfterPlaceAction 0x005C8174",
    "BehaviorCantHandleTallStack::TransitionToDisapointment 0x005ED0F0"
  ],
  "status": "IMPLEMENTATION_GAP",
  "unresolved": "built, awaiting strong verification (R-FIX2, 2026-10-03): the put-down look: head angle 0xBEB2B8C2 (0x005C81A6..0x005C81AC); head and the -30 mm drive are one parallel compound (0x005C8196..0x005C822A; the 2-argument DriveStraightAction default speed -80 mm/s, 0xC2A00000 at 0x00547268; the first failed child ends it and a failed parallel skips the image wait and the keep-alive: ignoreFailure=0 at 0x005C8220/0x005C8258); the gate at 0x005C818C..0x005C8194 ([[robot+0x284]+8] != -1) skips the parallel and the image wait when nothing is carried; the carried object is released AFTER the look action ends, only if still carrying (std::function 0x005C8480, SetCarriedObjectAsUnattached(false) at 0x005C84CE; the early release is removed). Still open: the 0x199 keep-alive in TurnTowardsFaceWrapperAction (0x005C8264..0x005C82CC) is deferred; what the behaviour's completion callback does with a failed look compound is not in the inventory; framesBefore is captured before the compound while the engine's WaitForImages starts after it; the head move cannot be cancelled when the drive fails.",
  "hardware_required": false,
  "live_path": true,
  "test": "ManipulationTests.PutDownBlockBacksUpPlaysThePutDownAndLooksDown"
}
```

At `cozmo-stack/src/Cozmo.Robot/Behavior/ManipulationBehaviors.cs:187`, `framesBefore` is captured before `RunAction` starts the parallel head/drive pair. Line194 later tests the global frame count against that earlier baseline. Two images during head/drive can therefore make the wait complete immediately after the pair succeeds.

Native `CreateLookAfterPlaceAction` adds the head/drive parallel first (`0x005C8216..0x005C822A`), then a `WaitForImagesAction(robot,2,VisionMode1,0)` (`0x005C8234..0x005C825A`). The sequential driver updates only the current child (`0x0054F79C..0x0054F7DE`). The wait's own Init resets its counter to0 at `0x0054CB7A` and subscribes tag0x43 at `0x0054CB8A..0x0054CBA6`; CheckIfDone requires that local count>=2 (`0x0054CC1E..0x0054CC2C`). Its callback `0x0054DD88..0x0054DDEC` only increments for timestamp>cutoff and requested vision mode present (special mode0x10 accepts any mode). Images received before this child initializes cannot have incremented that local counter.

**Trigger:** a carried block remains attached, head/drive succeeds, and two image events arrive while that pair runs, followed by no qualifying image events. C# starts the keep-alive using those earlier images; native remains RUNNING in WaitForImagesAction. The global-count predicate also fails to preserve the callback's vision-mode gate. Floats are not involved in this finding. No hardware test was run. The examined RFix2BTests do not exercise this image-order scenario.

## DEFECT D5 — M15-015's integer clock is bypassed by production construction

Current record, quoted before comparison:

`json
{
  "id": "M15-015",
  "title": "Freeplay active-time tracker and its four pause sources",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "FreeplayDataTracker constructor 0x0056EBD4",
    "FreeplayDataTracker::SendData 0x0056EC48",
    "FreeplayDataTracker::SetFreeplayPauseFlag 0x0056EEBC",
    "BehaviorManager::SetCurrentActivity 0x005A106C",
    "Robot::CheckAndUpdateTreadsState 0x005121F4",
    "Robot::SetOnChargerPlatform 0x00511DB0",
    "unity/scripts/csharp/Anki.Cozmo/HighLevelActivity.cs"
  ],
  "unresolved": "built, awaiting strong verification (R-FIX2, 2026-10-03): the tracker keeps u64 nanoseconds (0x0056EC50..0x0056EC84, 0x0056EED6..0x0056EF14, 0x0056F082), reports (int)round(acc/1e9) half away from zero with a signed `< 37` compare, the next-send deadline is f32 seconds (float)(ns/1e9)+30.0f re-read each SendData (BaseStationTimer 0x0084BC38), the DataTooHigh log text is the engine's, seeded from InitConfiguration (0x005A0DFC: GameControl paused; the OffTreads and OnCharger flags from the current state, then GameControl cleared). Still open: the seeding is the stack's late-creation substitute (the engine's flags are set on transitions: 0x005121F4, 0x00511DB0); the SendData debug log (0x56ECBE) is omitted."
}
`

Native `GetCurrentTimeInNanoSeconds` **0x0084BCB6..0x0084BCBA** returns the stored tick timestamp's two words at timer+18. `SendData` **0x0056EC50..0x0056EC84** reads that clock and performs unsigned 64-bit subtraction/addition directly; its later seconds getter **0x0084BCA8..0x0084BCAA** reads the stored binary32 tick field. Neither getter queries a second wall clock or reconstructs nanoseconds from seconds.

C# **FreeplayStack.cs:151**, through its required `Func<double>` parameter at **:43**, instantiates the seconds-clock overload. **FreeplayDataTracker.cs:58** reconstructs nanoseconds with `(ulong)Math.Round(clockSec()*1e9)`. The live **Cozmo.Conformance/FreeplayTool.cs:131** supplies `sw.Elapsed.TotalSeconds`, a separate stopwatch that can advance between reads during the same engine tick. The engine already exposes its stored integer tick timestamp at **CozmoEngine.cs:131**, but this construction does not use it. Therefore pause stamps and elapsed segments are not the native tick timestamps even though the accumulator's new storage type is correct.

There is also a lossy numeric boundary: integer timestamp9000000000000001 divided to double seconds then multiplied by1e9 and rounded becomes9000000000000002. This long-uptime example demonstrates why the round trip cannot claim exact integer-clock reproduction; the ordinary live-clock timing difference does not depend on that uptime.

**Correction needed:** wire the production tracker to the stored engine nanosecond tick and its stored binary32 seconds field; preserve caller-supplied offline clocks as explicitly scoped seams. This is an extraction/review finding only; no code was edited.

The tests at **RFix2BTests.cs:172/198/221** directly inject `Func<ulong>` and therefore do not cover this production construction. Their recovered arithmetic expectations are not circular, but their success cannot establish the live clock path. This defect is additional to the manifest's declared late-creation seeding and debug-log gaps.

## DEFECT D6 — 25th-pass convergence loses the assigned head angle

Current record, quoted before comparison:

```json
{
  "id": "M13-021",
  "title": "Unread MovementComponent and TrackLayerComponent bodies next to the turn and head actions",
  "status": "RECOVERABLE_GAP",
  "evidence": [
    "0x00549C60..0x00549D2C the two constructions; 0x0054BB8C, 0x0054BC6C, 0x0054BA84",
    "re-analysis/research/20260929-R-VIS-M12-M13-gap1-extraction.md Q4 and open questions",
    "re-analysis/research/20260929-R-VIS-M13-gap2-extraction.md Q5, open questions; re-analysis/research/20260929-R-VIS-verify-M13-gap2.md objections 2 and 6"
  ],
  "unresolved": "R-FIX2 (2026-10-04): GetAbsoluteHeadAngleToLookAtPose (0x0054B428) and Robot::ComputeHeadAngleToSeePose (0x00518344..0x00518620: the failure results, the 25-pass loop and its unassigned-angle quirk) are now built (a Unicorn run of the shipped instructions reproduces the fallback's expected bits; the converge cases' bits come from a Unicorn run of the real function, with the pose algebra double against the engine's binary32); TurnTowardsPoseAction::Init uses the fallback only on a non-zero result with the engine's warning. Still open: the pose algebra (neck composition, GetCameraPose's pre-multiply, GetInverse) is the stack's double Pose3d narrowed to f32 (reported MISSING); the vision model's head-cam z differs from the engine's -8.0 (M11-054); the no-parent branch (PoseHasParent = false) is test-only; the MovementComponent wire bodies, the eye-shift arguments and the remainder of TurnInPlaceAction::CheckIfDone remain unread. | earlier: read the listed bodies (MovementComponent wire bodies, the eye-shift arguments, the remainder of TurnInPlaceAction::CheckIfDone)."
}
```

Engine `ComputeHeadAngleToSeePose` assigns the output through `Radians::operator=(float)` at **0x005185B2..0x005185B6** immediately when its convergence branch is taken. Only afterwards does it compare the iteration counter against25 at **0x005185C6**. Convergence on pass25 therefore writes the computed angle, warns MaxIterations and returns1. It does not leave the output unassigned. The distinct nonconverging pass25 path increments the counter to26 and returns0 without assigning the output.

C# `cozmo-stack/src/Cozmo.Robot/Vision/VisionSystem.cs:1227` returns1 for `k == 25` before the angle assignment at1228. The output remains the initialized zero on that branch. This contradicts the recovered function’s output contract; the current Init wrapper uses fallback on nonzero result, so this check does **not** claim that this particular mismatch changes that wrapper’s final motor target. The changed function is observable directly through its output parameter.

`R2FloorPlacementTests.cs:1055` covers early convergence and nonconverging exhaustion, but omits convergence on pass25 and its assigned-output-before-error order. The expected numeric values are explicitly attributed to native emulation rather than generated by the implementation; this is omitted boundary coverage, not a circular test.
