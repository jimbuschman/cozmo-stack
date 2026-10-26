# Drive-off pilot: bounded UpdateImageQuality extraction — 2026-10-26

READY FOR INDEPENDENT CHECK BY research_reconciliation. This unit deliberately swaps roles: acceptance_blockers extracted; another agent checks before implementation. No code/tests/git/manifest changes or hardware run. Actual model/reasoning telemetry is unobservable. Primary libcozmoEngine.so SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1; all new function reads followed known.py preflights. Actual instructions/data are authority, decompiler only navigation. This is the complete370-byte UpdateImageQuality body00655578..006556EA plus its exact result/camera/API interfaces; it is not a new vision/DSP inventory.

## Current records before scope claims

```json
{
  "id": "M11-035",
  "title": "The VisionComponent per-mode result handlers and the frame broadcast",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "VisionComponent::UpdateAllResults 0x006542EC runs markers 0x006544A2, faces 0x00654510, pets 0x0065457E, motion 0x006545E8, overhead edges 0x00654652, tool code 0x006546BC, computed calibration 0x00654726, image quality 0x00654790, laser points 0x006547FA",
    "then CheckMailbox 0x00654A74 and the RobotProcessedImage broadcast 0x00654A14/0x00654A1C"
  ]
}
```

```json
{
  "id": "M11-080",
  "title": "CheckImageQuality: ROI weighting and MeterFromDetections, the proposal stage with NaN/boundary rules, the quality 1/2/3 decisions, the ImageQuality statics from vision_config, and Update's mode-7 block",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/overnight/20261014-ov14-m11-057-check.md: ROI head 0x006B2D4C..0x006B2F58; proposal 0x006B2F5A..0x006B30AC; statics 0x01051088..0x01051094 loaded at 0x006B07D8..0x006B0E3E (SetAutoExposureParams call 0x006B0A14); Update mode-7 block 0x006B55E2..0x006B5690."
  ]
}
```

```json
{
  "id": "M11-059",
  "title": "VisionComponent::Init and VisionSystem::Init: the config reads, IsInitialized and the face-album load-or-erase branch",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "VisionComponent::Init 0x00650D20: ImageQuality.InitialExposureTime_ms into .data 0x01051054 (0x00650DAE..0x00650DCA; rows 4a..4l; vision_config.json:46 = 16); VisionSystem::Init (0x006B0658) called at 0x00650E36 (blx 0x4BA200), then cmp r6,#0; beq.w 0x650FB4 (0x00650E3C..0x00650E3E); non-zero: sErrorF 0x00650E58, the debug-break gate 0x00650E90..0x00650E9A, return 0x00650FB0",
    "Zero: 0x00650FB4, a branch at 0x00650FE8..0x00650FFC, then LoadFaceAlbumFromRobot blx 0x4BA23C at 0x0065108A (body 0x006512A0 reads 0x184000 at 0x006512F0 and 0x183000 at 0x00651330); the other branch 0x0065100C..0x00651086 (EraseAllFaces 0x00651014, BroadcastLoadedNamesAndIDs 0x00651032) is unread; VisionSystem::IsInitialized 0x006B2CD6",
    "re-analysis/research/overnight/20261014-ov15-16-17-check.md: C1-C5 0x00650D56..0x00650FAE; A1-A7 0x00650FF8..0x00651630; S1-S10 VisionSystem::Init (profiler keys at 0x006B0E96/0x006B0ECC, SetAutoExposureParams 0x006B0A14, schedules 0x006B13C0.., +0x58 = 1 at 0x006B141A); SetExposureParameters 0x0085F374..0x0085F4AA; N1 Robot ctor gate 0x005103BC..0x005103D4; N2 LoadRobotConfigs 0x0052267C."
  ]
}
```

```json
{
  "id": "M11-060",
  "title": "VisionSystem::Update's application of the pending SetNextCameraParams to the current exposure and gain",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "VisionSystem::SetNextCameraParams 0x006B229C queues the params (M3-021 1d); the consumer in VisionSystem::Update (read from 0x006B4D5C) is UNKNOWN",
    "re-analysis/research/overnight/20261014-ov15-16-17-check.md: B1-B5: consumer 0x006B50C8..0x006B50D8 (after the gates 0x006B4D66..0x006B4D80 and both drains 0x006B4FB6..0x006B50C6, before ApplyCLAHE 0x006B50F2); producer 0x006B229C..0x006B230C; ctor 0x006B002A..0x006B0056."
  ]
}
```

```json
{
  "id": "M3-021",
  "title": "Camera exposure/gain parameters: the VisionSystem ctor limits, HandleDefaultCameraParams, SetCameraSettings with IsExposureValid/IsGainValid and the SetCameraParams send, SetNextCameraParams (queue and warning), SetCameraExposureParams",
  "status": "EXACT_SOURCE",
  "evidence": [
    "1a VisionSystem ctor: max 66, min 1, minGain 0.1, maxGain 4.0, cur 16, gain 2.0 (0x006B002A..0x006B004A); 1b Init changes none (0x006B0658)",
    "Handoff, owned by M11-059: 1f VisionComponent::Init reads ImageQuality.InitialExposureTime_ms into .data 0x01051054 (0x00650DAE..0x00650DCA); vision_config.json:46 = 16",
    "1k HandleDefaultCameraParams: no time-sync gate (0x00537114..0x0053712A); needs IsInitialized (0x006B2CD6); min <= init <= max; SetCameraSettings(init, gain) first (0x00657CCA), then SetCameraExposureParams (0x00657D04)",
    "1l SetCameraSettings: IsExposureValid/IsGainValid (0x006B9DAA..0x006B9DBE, 0x006B9E80..0x006B9EA8), sends {f32 g, u16 e, false} reliable (0x0065614A..0x00656166)",
    "1o the engine never requests DefaultCameraParams",
    "Functions (exports): HandleDefaultCameraParams entry 0x00537114, VisionComponent::HandleDefaultCameraParams 0x00657CA0, VisionComponent::SetCameraSettings 0x006560C8, VisionSystem::SetNextCameraParams 0x006B229C, VisionSystem::SetCameraExposureParams 0x006B92F0. Handoffs: VisionSystem::IsInitialized 0x006B2CD6, true after VisionSystem::Init 0x006B0658: M11-059; the consumer of the SetNextCameraParams slot in VisionSystem::Update: M11-060",
    "re-analysis/research/20261011-m3m4-fix-rows.md section 2 (Opus extraction, manager-adopted 2026-10-11): the full log rows (level, channel, key, format, arguments, gates, errG/break and order) and the differences from the C#.",
    "re-analysis/research/20261011-m3m4-fix-rows.md section 13.6 (follow-up Opus extraction, manager-adopted 2026-10-11)."
  ]
}
```

## Handler contract

Let C=VisionComponent, R=VisionProcessingResult. Relevant source fields are C+F4 last observed raw quality, F5 last reported quality, F8 u32 image-timestamp stamp, FC u32 current threshold; C+14 Robot, C+1C VisionSystem, C+329 auto exposure. R+0 timestampU32,R+4 processedMaskU32,R+8 qualityU8,R+C exposureI32,R+10 gainF32,R+14 imageMeanU8. These are separate raw values; neither quality nor imageMean is handler success.

| Row | Exact behavior and order | Primary witness |
|---|---|---|
| IQ1 construction | Ctor r5=0; F8 andFC=0; STRH1 atF4 initializes lastquality1 and lastreported0; STRH100 at328 initializes auto329=1. No guessed zero quality, -1 timestamp or constructor threshold3000. |006500B4/110,00650130,00650138/142,00650178/17C|
| IQ2 whole guard | If Robot+14 physical byte is0, return0 without camera, debounce/state/API. Otherwise if R.quality==0, same no-op. No IsInitialized/resource-ready check occurs in this handler. |0065558C..94,00655600..12; physical setter evidence reused OV14/0051397A|
| IQ3 camera first | If auto329!=0 call SetCameraSettings(R.exposure,R.gain) BEFORE quality comparison/timing, for every admitted nonzero quality including repeated values. Ignore its return/status; it cannot make this handler return failure. Auto disabled skips only camera, not quality notification. Reload R.quality after this call. |00655596..A6|
| IQ4 reset/start | If raw R.quality differs from C.F4 OR C.F8==0: stamp=R.timestamp; lastreported=0; threshold=GLOBAL firstDelay. Then lastquality=R.quality and return0. No notification on this branch, including timestamp0; a later equal-quality sample still sees stamp0 and resets again. |006555A8..B4,006555E4..FC|
| IQ5 wait/suppress | For unchanged quality with stamp!=0: if lastreported==1, no report; otherwise elapsed=uncheckedU32(R.timestamp-stamp), emit only if elapsed>threshold UNSIGNED. Equality waits. No Timer, elapsed seconds, signed compare or host wallclock. Then store current quality and return0 when not emitting. |006555B6..CA,006555FA..FC|
| IQ6 code | Quality1→EngineError6,quality2→4,quality3→5; other nonzero raw quality→7. Literal selection is packed00050406 shifted by8*(quality-1),lowbyte. |006555CC..E2,0065561A..2C|
| IQ7 emit order | sEventF first, debug second, construct EngineErrorCodeMessage and Robot.Broadcast third, clear temporary; AFTER broadcast reload LIVE C.F4 into C.F5, reload LIVE R.timestamp into F8, load GLOBAL repeatDelay into FC, then reload R.quality into F4 through common tail. Do not prematurely freeze all state or update before broadcast. |00655626..38,00655666..90,006556B6..D0,006556D4..E8→006555F6/FA|
| IQ8 status | Every normal branch returns integer0, including no-op, suppressed/wait/reset and emitted reports, and camera validation rejection. Only terminating/native fault machinery is outside normal result. Never set drain failure merely because quality is Dark/Bright, parameters rejected or no report was emitted. |00655600..12 and all branch targets; no alternate normal return|

F5 is a raw quality byte, NOT a boolean 'already notified'. Good quality1 sets it to1 and suppresses further reports while unchanged. Dark2/Bright3 set2/3 and therefore remain eligible for repeat intervals. In the ordinary non-reentrant path the post-broadcast F4 reload equals the stable quality; the contract still specifies actual live field reload/order rather than assuming an atomic snapshot across callbacks. No extra asynchronous callback/thread policy is invented.

## Constants, inputs and enum data

Global firstDelay at0105104C has compiled U32=3000; repeatDelay01051050 compiled=5000. Actual PIC witness: literal0065578C=009FBA60 plus PC006555EC; literal00655788=009FB96E plus the UNALIGNED register-ADD PC006556E2 (not an ADR-aligned PC). Both loads feed C.FC at006555F6. Reuse adopted OV16 C1/C5 and current VisionComponentQualityConfig: required ImageQuality keys TimeBeforeErrorMessage_ms then literal typo RepeatedErrorMessageInverval_ms read U32 into those globals at00650D56/00650D90, followed by initial exposure. Earlier successful writes remain on later missing-key failure. Shipped vision_config.json supplies3000/300000, distinct from compiled repeat5000. Do not substitute shipped values for a missing/failed config or make globals per-camera immutable constants. Full component Init/readiness remains M11-059.

ImageQuality EnumToString007C6700 table01034A94 points0Unchecked,1Good,2TooDark,3TooBright. EngineErrorCode EnumToString007749C8 table01033910 includes4ImageQualityTooDark,5ImageQualityTooBright,6ImageQualityGood,7Count. Default7 is the handler's raw fallback; normal checked proposal producer produces1/2/3. No inferred new quality enum or quality-based action/admission change exists here.

Exact known logging: sEventF key robot.vision.image_quality (0065572C),format%s (00655748),argument EngineErrorCode name; then Debug channelVisionComponent (00BFD3B2), keyVisionComponent.UpdateImageQuality.BroadcastingImageQualityChange (00BFDA1B), format 'Seeing %s for more than %u > %ums, broadcasting %s' (00655754), arguments current quality name,elapsed,current threshold,error name. Wording alone is best effort under AGENTS; diagnostics must not disguise unavailable source inputs as successful handler completion.

## Producer, drain and API bytes

Mode7 source gate006B55E4/E6 compares ShouldProcessVisionMode return exactly1 atEA/EC; no due mode means no new quality computation. Calls actual CheckImageQuality006B5600 between profiler Tic/Toc. Nonzero status at006B5612 goes through Error/errG/break and directly cleanup006B5692, SKIPPING deque publication; do not enqueue guessed raw quality or treat producer UNKNOWN as native failure. Successful status0 reaches006B566A..72 to ORmask80, then pushes actual header starting VisionSystem+378 through the shared mutex/deque006B5676..8C. Header's proposal fields are quality380,exposure384,gain388; result offsets8/C/10 preserve raw storage, and timestamp378 is the image timestamp. Shared scalar copying at006B2B3A..40 transfers fivewords+byte; current result payload/lifetime drain contract already owns that. Engine UpdateAllResults tests processed bit7 at00654784..8A and invokes this handler00654790; normal handler0 does not promote the per-drain failure aggregate.

M11-080/M11-081 existing packets own ROI/histogram/proposal arithmetic. This extraction establishes their CALLER/PAYLOAD/MASK contract only. It does NOT recover WHICH detector rectangles are appended or a missing clipping/ROI branch; current QualityRectangleInput/unavailable producer boundaries remain explicit. Never assume empty ROI, default quality or native success/failure solely to keep the engine tick moving. Those separate producer gaps cannot disappear in a whole camera/vision claim. The already built source proposal can be transported unchanged when its actual producer is available; no second heuristic algorithm is authorized here.

EngineErrorCodeMessage setter007277B0..CE writes outer U16tag007C and one raw U8code; Pack0071A0C2..E2 writes exactly1byte. E.g. Dark notification raw union bytes7C0004, Good7C0006, Bright7C0005. Reuse the existing engine-to-game Broadcast contract: public/app enqueue precedes synchronous local dispatch, with handler isolation perM1-034; do not replace it with only a private diagnostic or callback. The raw report carries no image timestamp/exposure/quality field beyond mapped code.

## Exact camera recipient boundary (reuse settled M3-021)

SetCameraSettings006560C8..61AE requires IsExposureValid==1 then IsGainValid==1; failure does no send/pending/API, but UpdateImageQuality continues its debounce. Valid path: Info log→robot SetCameraParams send→VizManager.SendCameraInfo→VisionSystem.SetNextCameraParams→CurrentCameraParams Robot.Broadcast. No unchanged-value suppression and no send-return gate. Source words/flags at0065614A..66 are gainF32,lowU16exposure,false, reliable1/hot0. SetNextCameraParams00656180 queues ORIGINAL signed exposure/gain. CurrentCameraParams00656188..A4 uses lowU16exposure,gainF32 and LIVE auto329. Existing M11-060 queue overwrite warning/application-before-next-processing behavior must be reused.

Robot SetCameraParams tag57 and serializer are the adopted M3-021/M2 interface. CurrentCameraParams setter00723ACA..FA tags002F; Pack00710F90..C8 writes gainF32 then exposureU16 then bool byte,7bodybytes. Expected report is2F00 + those bytes. Existing CameraSettings internal SetCameraSettings(int,float), CameraQuality.ApplyPendingCameraParams and the public app-delivery queue are reusable production collaborators, not proof that the new mode7 drain callback is bound. Preserve queue/app versus synchronous local delivery meanings and order. Public convenience API naming/representation may suit C#, but these payloads/order/readiness do not change.

## Smallest complete implementation and source-derived checks

One robot/VisionComponent-lifetime receiver with the four initialized fields, physical/auto inputs, actual native drain bit7/result snapshot and existing camera API sender is sufficient. It must emit the actual typed/raw EngineErrorCodeMessage API, use source image timestamps and mutable static timing config, and return0 after every normal branch. No camera-algorithm rewrite, fake complete system-ready bit or default all-frames failure gate belongs here.

Checks derived from instructions, not helper mirrors: first Good atT1000 resets; atT4000 equal3000 delay noerror; at4001 emitsGood6 once and later stableGood suppresses even after repeat threshold. Dark transition at5000 resets; at8001 emits4 then uses configured repeat300000; exactly300000 waits and300001 re-emits. Wrap startFFFFFFF0 thenBA8 gives elapsed3000(wait),BA9 gives3001(emit). Physicalfalse orquality0 leaves ALLfour receiver fields unchanged and no camera/API. Autooff stilldebounces. Valid camera settings are attempted on EVERYadmitted sample before notification, including a waiting/suppressed sample; invalid exposure/gain blocks camera output but does NOT turn handler status nonzero or prevent quality error reporting. Test actual native mask/payload/drain producer→receiver, camera wire/API ordering and subsequent processing gates; direct receiver unit tests alone are not full production reach. All existing worker/pose fixture join-before-dispose rules still apply.

This complete receiver contract closes M11-035's formerly unread debounce/event/API tail only. It does not settle whole M11-035, M11-080, M11-059, other mode handlers or physical camera response. No hardware/source status upgrade, feature exclusion or new compatibility policy. Independent checker must PASS this report before Phase2 code for this unit.
