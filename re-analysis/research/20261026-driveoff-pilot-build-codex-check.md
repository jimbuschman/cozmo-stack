# Gatefix final independent verdict — scoped CODE VERIFIED / PASS; whole pilot unfinished

**PASS for the reviewed gatefix candidate only. No remaining blocking finding in these corrected bytes.** This permits a reviewable candidate checkpoint/integration; it does not call the whole job or DriveOff behavior complete, READY or accepted. The historical initial PASS was revoked after the real full-gate failures; that history remains below. No official fidelity status was changed.

Checked isolated builder HEAD `00fcd999d14350662fe507e698265be7c16cf0f7` plus its frozen **30 changed/new source/test C# files**. Independently verified all30 per-file SHA256 values twice (including after execution), zero mismatches. Source-list artifact `.scratch/driveoff-pilot/gatefix-source-sha256.txt`: normalized CRLF→LF UTF8 SHA256 **4e7a97c3bf1ab407c7a05d654b76a97d0b2c667d9f7b80a01efd11706f9f7154**; raw CRLF bytes SHA256 **af3219272ca019cb9f06e3e933a8b0d3a1fb79c63bed01a03cacee3686b1d6d1**. This explains the metadata convention; actual source bytes match exactly. Per-file list follows this verdict.

Independent execution after builder's explicit stable/host-release notice:

| Artifact stem under builder `.scratch/driveoff-pilot/` | Actual result |
|---|---|
| `codex-independent-gatefix` | exit0,138/138 PASS,0 skipped,20s; TRX executed138,failed0,error0,aborted0,notExecuted0 |
| `codex-independent-gatefix-d1d2` | exit0,36/36 PASS,0 skipped,3s; TRX executed36,failed0,error0,aborted0,notExecuted0 |

Both commands used explicit `C:/Program Files/dotnet/dotnet.exe`, `--no-build --no-restore --nologo -v:q -clp:ErrorsOnly`, exact filters/loggers recorded in `.scratch/driveoff-pilot/codex-independent-gatefix-commands.txt`; full logs and TRX are retained. First filter's obsolete M11 method-name fragment matched no case; only omitted R2FloorPlacement class and renamed ConfigurationHostDirtyFlag were run in the second filter, not a repeat of138. Builder's separate376/376 PASS0skip26s is inspected supporting evidence, not an independent run and not added to these counts. Independent `fidelity.py --check` passed710; `git diff --check` exit0 (only preexisting working-copy CRLF notices).

Closed reviewed blockers and actual reach:

- Native attach writes actual configuration force after carrying assignment/before confirmer and visibly diagnoses absent native owner. Later confirmer-failure timing is source-order coverage, not an invented test seam.
- Canonical tracker receives actual AI-after-helper ticks, source clocks/logs and sensor pause inputs; actual Robot retirement forces it before real AbortDocking, then captured AI+2C retires storage. Host observer disposal does not own it. Specialized Freeplay Spark writer remains at that feature's explicit missing boundary.
- Retiring actual manipulation detaches only its installed device/path/world delegates and own Log relay; borrowed handles preserve shared services and later replacements. Runtime-created overhead detector is cleared only by identity; preexisting/replacement detector survives.
- Approved D2 standalone owns one coherent clock/private whiteboard and its copied failure storage, borrows actual Docking/Carrying/Paths/Follower, preserves D1's host-only last-registration configuration hooks, and cannot alter native clock/flags/producers. Original R2 timing/cap/expiry expectations passed unchanged. Controlled storage retirement is host ownership, not a native allocator/destructor claim.
- D1 native pending/root-cache/threshold/height/audio-request tests enter the actual native producer, while host controls remain separate. W2 proves its own NV enqueue-before-callback through captured subscribers without global Mood61 counts or assertions swallowed inside the callback. Exact C3 teardown position and loader barriers remain tested.
- Camera handoff tests receive real handshake/manufacturing metadata, complete actual calibration NV queue and preserve captured-state/error/host-light behavior. Before-any-raw-state case remains explicitly a host-calibration empty-history leaf test, not native readiness or successful worker/drain evidence.
- Actual configured PutDownDispatch, ActionList/5A completion, current selected-name versus retained clip, raw vision mean/quality/drain-failure/reset, canonical Mood/Needs, helper/manager/Spark ownership and concrete animation report paths were independently exercised offline. Custom standalone trigger now actually resolves/plays its authored clip while canonical inputs remain unchanged.
- Face and Flip wire oracles use independent pre-action inputs/ordered f32 formula; no emitted endpoint was used to derive its own expectation. Face's physical plant conversion remains UNKNOWN; source Face speed220/decel166.66667 versus current60/500 is an explicit preexisting gap, not graded fidelity success.

**Limits / handoff:** code verified and specific real offline production paths are distinguished from hardware (no run by this verifier) and acceptance (none granted). The whole pilot remains unfinished: actual audio control owner/active observer/render/output joins; live ROI/GetROI/proposal producer availability; general Meet/Feeding/specialized Freeplay and selected Look body/changed-config boundaries; existing pose/map/other missing paths remain visible. Passing regression tests do not close these dependencies or certify phone numeric bit identity.

Governing scope correction (operator, 2026-10-26): a behavior boundary is what changes ITS observable result WHILE RUNNING; known visible MISSING permits handoff. This supersedes earlier recommendations holding M13-017/M13-040 for neighboring audio, ROI or other-activity gaps.
Propose OWN-record settlement checks for **M13-017** (005C0980/005C0B10/005C0B18/005C0BB8/005C0DA8..005C0EE2) and **M13-040** (0051293C/00512AB4/00511990..00511C2D/00506B70..00506C48/00506F98..00506FB8/00639840): no concrete remaining behavior-changing defect was established in their checked claimed paths.
Also retain **M7-026** (0067C084,0067B67E,0058A95C,004F0D2C..74) and **M8-027** (00664708/006634F4; checked eight-kind callable census) as OWN-record settlement candidates.
Independent A+B agreement adds **M7-023** (00511150/005111B6/0067AE18..74), **M11-060** (006B229C..230C/006B50C8..D8, actual worker), and **M12-043** (006177F0..17C32/004E65C6/0062C5A2..616): no own residual identified in these checked claimed paths.
These are seven OWN-record settlement proposals; no status change, neighboring-record completion or broader subsystem acceptance is made here. Code/tests remain unchanged.
Code and tests are unchanged by this correction; manager’s subsequent full gate passed **6361/6361, zero skipped**. Historical initial42-failure/revoked-PASS evidence below remains intact; neighboring recorded gaps remain visible and are not declared fixed.
No source/test/manifest edits, binary reading, test execution, commit or hardware action for this scope correction. Selected model/reasoning telemetry remains unobservable.

Initial full gate remains **6312 PASS /42 FAIL /0skip /6354total,exit1,7m25s**. Intermediate failures and specific correction records remain below; they are not erased by this bounded verdict.

## Gatefix literal current record reread

```json
[
  {
    "id": "M13-017",
    "title": "BehaviorDriveOffCharger: runnable on the charger, the drive distance and the leaving-the-contacts update",
    "status": "IMPLEMENTATION_GAP",
    "evidence": [
      "ctor 0x005C0980 stores 96.0 + json extraDistanceToDrive_mm at +0x11C (0x005C09D4/0x005C09E2/0x005C09E6); IsRunnableInternal 0x005C0B10 returns robot+0x34A (the on-charger-platform flag; contacts are +0x338)",
      "InitInternal 0x005C0B18 takes the reaction lock, pushes driving animations when the animation state is 3 (0x005C0B4A) and transitions only when robot+0x355 == 0 (0x005C0B54); +0x355 is the robot's OffTreadsState (OnTreads = 0), written only by Robot::CheckAndUpdateTreadsState 0x00512088/0x0051208E and named by OffTreadsStateFromString 0x0078DF58 (OnTreads 0, InAir 1, OnBack 2, OnLeftSide 3, OnRightSide 4, OnFace 5, Falling 6)",
      "TransitionToDrivingForward 0x005C0BB8 drives +0x11C (0x005C0C02/0x005C0C08)",
      "UpdateInternal 0x005C0DA8: once off the platform (robot+0x34A == 0) it records the time at robot->[+0x264]->[+0x18]+0x44 and returns 2 (0x005C0DF4..0x005C0E0A); while still on the platform and robot+0x355 != 0 it calls StopActing(false,false) (0x005C0DB6/0x005C0DC4) and waits; on the platform with +0x355 == 0 it transitions to driving forward (0x005C0E18)"
    ]
  },
  {
    "id": "M7-026",
    "title": "MoodManager::GetSimpleMood and the mood time MoodManager+0x130 (inputs to the animation group draw)",
    "status": "IMPLEMENTATION_GAP",
    "evidence": [
      "re-analysis/research/20261012-m5-x9-extraction.md section 4f: GetSimpleMood 0x0067C084 (Default; Happy > 0.33f -> Happy; Confident < -0.29f -> Sad; Happy < -0.33f -> Sad; constants 0x0067C0C0..0x0067C0C8); the group draw reads MoodManager+0x130 (0x0058A95C), written only by MoodManager::Update(t) 0x0067B67E and Reset/ctor."
    ]
  },
  {
    "id": "M8-027",
    "title": "ProtoHandle subscriber-callable census: which callables the 8 ProtoHandle kinds hold at teardown and what destroying them does",
    "status": "IMPLEMENTATION_GAP",
    "evidence": [
      "re-analysis/research/overnight/20261015-ov53-55-check.md: ProtoHandle vtables (8) confirmed; EngineToGame release 0x00664708, unlink 0x006634F4 (nulls the callable, decrements node refcount +0x20, frees at zero 0x0066352E..0x00663560).",
      "re-analysis/research/overnight/20261016-ov105-107-108-answer.md: void() signal 0x0105DF8C: one subscriber via RegisterTransport 0x0083B9A0..0x0083BE4C (bind vt 0x0103795C trivial); Dispatch signal TransferQueueMgr+0xC: two RegisterTask::$_0 (vt 0x01031418, destroy memory-only) from TransferTask::Init 0x006A4358..0x006A438E; release bodies 0x004EF138, 0x0051D8F8, 0x00591550, 0x0064E618, 0x00664708, 0x00664EB8, 0x006A3EFC, 0x0083BED0 identical; ~TransferQueueMgr 0x006A3860..0x006A3898."
    ]
  },
  {
    "id": "M13-040",
    "title": "Contact-true charger observation: live ingress, located identity, Known pose and ordered world/contact effects",
    "status": "IMPLEMENTATION_GAP",
    "evidence": [
      "RobotState sync gate 0x0051293C..942, contact bit12 0x00512AAC/AAE and SetOnCharger call 0x00512AB4; SetOnCharger 0x00511990..0x00511C2D observes on every true input before old-contact gate 0x00511BA8/BB0. Contacts +0x338; platform +0x34A.",
      "ChargerDockPose 0x004EA304..0x004EA35A: Z rotation pi bits 0x40490FDB, translation (30,0,0), robot pose parent and ChargerDockPose name. Located family4/type13 first/current-origin query 0x00511A1C..A52, 0x0061EBF2..0x0061EC26; factory and connected identity/unique SetID 0x00511AD6..0x00511B40, 0x004EF468..0x004EF528.",
      "Known observation 0x00511B4A -> 0x00506B70..0x00506C48: reparent/root flatten, existing/new branch, SetPoseHelper 0x0050609C, distance -1, confirmer count1/reference/existing timestamp. Actionable cache invalidation 0x004E01E4..226; common SetPose 0x004EF590..646. AddLocatedObject 0x00622AC0..EB0 includes obstacle deletion, VizManager pointer store obj+0x54 (0x00622D24..0x00622D30; CozmoContext+0x24, ctor 0x004EA734; read by EraseVisualization 0x004EA5E8..0x004EA5F2): viz, no effect on 1-4 and notification.",
      "Notifications 0x00506F98/FA8/FB8 world/map/state-change; unchanged return 0x00506296..29A; changed nonzero new state map 0x005062B4 and active light recipient. 0x00639840 compares NEW state r3 with1: Known trigger0x25/layer2, other changed trigger1/layer2; unchanged no request.",
      "Contact edge/store ordering 0x00511A66..ACC, 0x00511B50..0x00511C14; platform setter 0x00511D44..DCA; existing footprint tick 0x00513CD8..0x00513E2A and M4-019 threshold send 0x0063427E, 0x006342C8..CE, 0x00634364..368.",
      "re-analysis/research/20261021-m13-017-located-charger-check.md: independent check of A1-A11/P1-P4 with corrected A9; supplementary mount and exhaustive caller census excluded."
    ]
  }
]
```

## Exact frozen gatefix file hashes

```text
3cc0fb779b522d1873184f3fdef224e2914bce146a4222f8362c205d8adad967  cozmo-stack/src/Cozmo.Robot/Behavior/AIComponent.cs
ff40fa0fd41b89b29f806b05459178f437e51e277506ebfd88412c1d909206c5  cozmo-stack/src/Cozmo.Robot/Behavior/NativeBehaviorRuntime.cs
0641d0f6de0cd96b4258bf09fda9b9ef799351dd2fed8724d0db27dee96172f5  cozmo-stack/src/Cozmo.Robot/CozmoRobot.cs
1d46a8d9312a2d5f36cd372b9667487fe0e0689a0fb0ee7bf5ad92878fd92c97  cozmo-stack/src/Cozmo.Robot/Manipulation/AIWhiteboard.cs
2f5d0588c0ac78fbf4a2b62efc131771b862f069d9d19222c3f216cb407e1437  cozmo-stack/src/Cozmo.Robot/Manipulation/LiftGeometry.cs
f0b46fe6358bf25cbb7f7b1bd5f84e5aaa791a79d997f04c0a1debd46ef8a263  cozmo-stack/src/Cozmo.Robot/Manipulation/ManipulationSystem.cs
304f569710a09f742903dfb7099b1cbf58c12be1f5af2399c6612a05c0a4818e  cozmo-stack/src/Cozmo.Robot/Manipulation/NativeConfigurationManager.cs
96b6fc7cd3530a43bf5e842caa5e8254e0ae6ca32c5ddd283f1adf0afb5c50f0  cozmo-stack/tests/Cozmo.Protocol.Tests/BehaviorObjectiveReportTests.cs
6f85ee51142f326f225815daaff0a71f8b9c81f3f6c6bf22e13c940b1c656e82  cozmo-stack/tests/Cozmo.Protocol.Tests/CoreReviewTests.cs
22191ba678fec1ce148eba7a6b56679b636784cabd29b569fd139ee7be9abf15  cozmo-stack/tests/Cozmo.Protocol.Tests/DesiredFaceDistortionTests.cs
5dc588177e50abbef8497ad7b9132d14e099941e5f4be9199837433dc7b016ce  cozmo-stack/tests/Cozmo.Protocol.Tests/FaceTests.cs
e38ace00d875d1f44b9f0f64885cb5fc77ad3ed6954f507cf16366dd1b5ba333  cozmo-stack/tests/Cozmo.Protocol.Tests/M11SmallReadsBuilderTests.cs
e74babeef148c6948d2ce40be1fe2c06aa4b949952a75028a5eb59da465a39c6  cozmo-stack/tests/Cozmo.Protocol.Tests/M12RVisBuildTests.cs
c86649411f993dbfc6d31b234013ab0aef2529106a049056fa138d172fbc0a46  cozmo-stack/tests/Cozmo.Protocol.Tests/M13ConfigurationAudioNotifyTests.cs
d5aba3dca31d988aa7b2f4f311e35480b5a37012c6201c164b6f72988e4ac5e8  cozmo-stack/tests/Cozmo.Protocol.Tests/M13ConfigurationPendingTests.cs
bab5a7f369bea79c17866643bd35753050b5b183a603712867a32c0606f5f11f  cozmo-stack/tests/Cozmo.Protocol.Tests/M15MutationCheckedRowsTests.cs
3cf6ad4c905b309933bad7e3b26a75cbd591fce17c0b5144433e0c0c9e8d9cec  cozmo-stack/tests/Cozmo.Protocol.Tests/M15RestorationCheckedRowsTests.cs
3828d568a72fa7e05f2371dc39b70067db2f3270b00040c97a04a3e5c2912da6  cozmo-stack/tests/Cozmo.Protocol.Tests/M3DeviceTests.cs
60bc9e81d88f10dbecb4be975bb05dcfb3a35bc5eb46ef33407634af99e9d4e3  cozmo-stack/tests/Cozmo.Protocol.Tests/M3M4ReviewFixTests.cs
3b2f9f474e1b47a5825cc5180eb58600376aa924f626fc6d29bb89297ae112d4  cozmo-stack/tests/Cozmo.Protocol.Tests/M5BuilderCorrectionsTests.cs
2ba89f48ee29db74f9d8b73a0eb0b22e49aceb00af3be1c61641e02636bb316b  cozmo-stack/tests/Cozmo.Protocol.Tests/M5X13BuilderTests.cs
da3e6e7e78e206ac95f470e9c03c4b2402f4114203d3d57d2619ab1ae491db2f  cozmo-stack/tests/Cozmo.Protocol.Tests/M5X20BuilderTests.cs
f55621a85b9d76bfea9008bd6a30fc828db2c044bd001fdc20f4a4c2e9ef4d9a  cozmo-stack/tests/Cozmo.Protocol.Tests/M7BatchThreeBTests.cs
10bfd3b932946efbb82f3edd4c08062c003e6b1b0cfb459ea989b36d789a08dd  cozmo-stack/tests/Cozmo.Protocol.Tests/M7LifetimeCheckedRowsTests.cs
d3fcd82d40b662241ee1512f77add696f1926f24b21b0954cf5bd6a7e7f05126  cozmo-stack/tests/Cozmo.Protocol.Tests/M7ManagerLifetimeTests.cs
36bdd79c1f521c46b016e2d1e47dc25d35b4d59b7caea325549dc02367669e60  cozmo-stack/tests/Cozmo.Protocol.Tests/M8BatchThreeDTests.cs
cdc896add7af4483752b31abce8757b4d7b7cb6227196c1875c9ca60ef7a32b3  cozmo-stack/tests/Cozmo.Protocol.Tests/ManipRig.cs
233d5de5ce1af5068e440009f366b09115edc4af4a10cfee36e68a3fbff9eaca  cozmo-stack/tests/Cozmo.Protocol.Tests/NativeTrackerOwnerTests.cs
a52b0586a9b24dbc9f346485996cb5889290ad2390660462a4b9baa2cb0d54ac  cozmo-stack/tests/Cozmo.Protocol.Tests/SparkManagerInputCheckedRowsTests.cs
b84ada5afedc2e63c1c9c921bcab0a91ac05a5ba32f74acdb71884c2de54655f  cozmo-stack/tests/Cozmo.Protocol.Tests/StandaloneManipulationOwnerTests.cs
```

---

# Post-full-gate addendum — prior scoped PASS REVOKED pending identified corrections

Manager full gate on integrated d3d373ab **FAILED:6312pass/42fail/0skip,total6354,7m25s,exit1**. Immutable log `.scratch/driveoff-pilot-full-gate-1.log`, failure names `.scratch/driveoff-pilot-full-gate-1-failures.json`. Earlier focused passes remain their actual results; they did not expose all ownership/output changes. The earlier scoped PASS below is superseded by this addendum, not erased.

I missed a real D1 producer join during the original consumer audit: Carrying SetObjectAsAttachedToLift now writes only publichost Configurations.ForceUpdate, which intentionally stopped forwarding to native Robot state. Current M12-008 step9 (known6331AE then primary6331B2/B6/B8/BC) requires actual boundRobot→World34→ConfigManager94 byte+C=1 AFTER SetCarrying and BEFORE AddLiftRelativeObservation6331C8. This nativeforce write must be restored independently of the public extension, including a later confirmer failure; pre-attach failures must not force. Borrowed public facades also overwrite canonical Docking.Configurations and attach anonymous untracked Log relays to shared Docking. Dispose correctly must not retire canonical callbacks, but each disposedhost must detach its own diagnostic relay. Both are genuine correction items, not merely stale test oracles.

CORE010 failing callback-removal/null assertions assume each public facade owns Docking; the new source-backed lifetime uses actualRobot-owned canonical Docking/Carrying. Revised tests must retain canonical callback/owner until actual native retirement while proving facade-owned followers/relays retire and active sibling remains functional. Required exact corrections/source tests were sent manager before implementation; no changes to active manager checkout during fullgate.

Mood.Update CALLSITE independently reread after known67B5D4: entire normalbody67B5D4..6B0 has NO early dt-return. Belowfloor logs67B644 and rejoins67B670; storescapturedt→13067B67E, updatesall9 emotions, unconditionallycallsSendEmotions67B6A4. Native physical/external/Robot gates remain at already checked enclosingphase/sender, not a new dt suppression. Thus added tag61 after an admitted normalRobotMoodphase is real output, not testnoise. Exact payload/delivery pass does not by itself settle all failing readiness/lifetime fixtures; those are being classified separately.

DesiredFaceDistortion fullgate failure compares delegate.Target identity, not output: its test supplies NOcustomNeeds, so stack.Needs is actual Engine.Needs; new facade closure invokes the same canonicalproducer. Known/primary TrackLayer64EDEC..EDF4 reads context→NeedsManager34→component3D4; >epsilon AddGlitchEDF8..EE14. Preserve canonicalstreamer and valid standalonecontext custominputs. Replace storage-identity oracle only with actual sampledoutput/cache/Glitch proof and customstandalone isolation before/during/afterDispose; explicit offlineNeeds admission remains available.

Face/M13-028 testwire-coordinate findings: Flip norm+20 was incorrectly compared to absolute XEnd after storedForward correction; preserve independent3Dvs2D travel proof and sourcef32 endpoint from prestate, no physicalrange relaxation. Face physicalMAIN X grade depends on unproved Rig Forwardendpoint→MAINfeedback conflation; no guessed+20 conversion/new380grade. A tests-only wire characterization must explicitly retain nativeFace speed/decel contradictions as gaps, never call that behavior source-complete.

No new full-suite or hardware run by checker; no production/tests/manifest/status edits. Remaining42 failures require exact caller/prestate/owner classification before any oracle changes. Final candidate PASS/recordsettlement recommendations suspended until correction/review/verification; wholepilot remains unfinished.

---

# Final independent build review — PASS for the scoped candidate; whole pilot IN PROGRESS

**Verdict: PASS for the checked implementation candidate at the final 55 source/test files below. No outstanding blocking finding in that scoped diff.** This authorizes manager/builder checkpoint commit and manager-owned full gate/Claude review; it does not certify the whole DriveOff behavior finished. Earlier PENDING/failure entries below are preserved chronological evidence, superseded only by the specific corrections/rechecks recorded here.

Final handoff identifier: builder aggregate SHA256 `2d8bae25556a44297a1884e191088c293a6f7b24773276e28944106e29b00d31`; independently checked **all 55 per-file SHA256 values with zero mismatches** against builder `.scratch/driveoff-pilot/candidate-sha256.txt`. Final Mood.cs SHA256 `59f6d72f8f8406c6a1219a9c7c56f0577df0ca7fdc9b1f97af386a0e14af30ea`; actual `// fidelity: M7-026` at341 above GetSimpleMood is a nonbehavioral owning annotation, not a status change. Final DrivingAnimationHandler.cs `c6b9d01bb49af438a7dcf3dfd69aecfd0e2cc6e0cef0a8552e6d212bfdd1736a`; M5X15BuilderTests.cs `a28503b9feb0aa021f03da104622eb465804c26fe103c8c85b6b8dc44d81a7ac`. Unchanged prior-reviewed files reused by hash; later corrections received affected-only review.

Independent actual execution (no overlapping builder hosts):

| Run / artifact stem under builder `.scratch/driveoff-pilot/` | Actual result and scope |
|---|---|
| `codex-independent-focused` | exit0,303/303 pass,0skip,1m7s. Actual configured PutDown/factory/chooser/action/path/completion plus native config, camera-worker failure/drain/reset, facade and affected regressions. Exact earlier candidate; not rerun wholesale after narrow corrections. |
| `codex-independent-c7` | exit0,7/7 pass,0skip,1s. Root/activity nonnull objective guards plus unchanged configured route. Later null branch separately reviewed/tested below. |
| `codex-independent-final-affected` | exit1,46pass/1fail/0skip,total47,3s. Actual final driving Mood/Needs+C7 null/non-null+native mood/API and legacy classes. All46 unchanged cases passed; sole failure was unrelated periodic Mood diagnostic in an overbroad stack-log collector. **This run is not relabelled a pass.** |
| `codex-independent-collector` | exit0,1/1 pass,0skip,212ms. Only the corrected failing fixture, exact ordered stack/overflow assertions retained. No production suppression or behavior change. |
| `codex-final-fidelity.log` | explicit Python313 `-X utf8 re-analysis/tools/fidelity.py --check`, exit0,710records; no manifest/status/evidence changes. |

Commands/filters and failure details are preserved below; every test run used explicit dotnet, `--no-build --no-restore --nologo -v:q`, TRX and console log. Final collector filter was `FullyQualifiedName~M5X15BuilderTests.DrivingStackErasesFirstMatchAndChangesCurrentOnlyOnInit`. No checker full suite, hardware, commits, pushes or production/test edits.

Final affected static disposition: native consumers use retained native publication instead of unknown→empty/false; BuildPyramid target reads no longer consume public host cache; bool-only Straight API call shape restored; empty/read-failed Mood config follows defined null-input continuation; actual Mood app raw+typed enqueue precedes synchronous local61 under actual external owner guard; root/activity Look objective nonnull overrides are visibly unavailable while omitted/explicitnull preserve41, with admission guard before Stop/publication; actual bound driving owner reads Mood (rawSad1) then canonical Needs for other raw values, override-first and M1 null skip preserve prior triplet. New tests distinguish Boolean-seam order tests from real canonical-level producer cases. Changed Navigation expectations derive from captured source inputs, not emitted outputs. Cleanup/join order and real5A/C6/CA/CB test reach were checked.

**RECORDS-TO-SETTLE candidates for Claude (no status upgrade here):** M7-026, now including actual driving consumer004F0D2C..74 and group cached130 bindings, exact SimpleMood raw mapping/thresholds and captured native update clock; M8-027, whose CURRENT unresolved already establishes every held callable destruction memory-only, confirmed against managed unlink/retirement semantics. Current literal record quotes and exact recommendations are below. Do not extend these named-record candidates to whole Mood/M8 subsystem acceptance.

**Remaining records/whole-pilot STOPs:** M13-017 and M13-040 remain whole-record IMPLEMENTATION_GAP handoff items: actual object1 audio/scoped active observer/scheduler/render path is unbound; live detector ROI/rectangle/GetROI producer and other enabled vision handler/lifecycle availability remain; general Meet selector/Feeding.Update and selected Look action remain explicit feature boundaries; noncube footprints and broader named cached-pose/component lifetime obligations have not been certified. Current positive map-height/cache/relative/insertion and native pending/container producers must not be described as wholly absent. M12-049 still owns unclosed BuildPyramid callbacks; M13-013 owns derived DriveOffChargerContacts, not this plain Straight path. No unknown is converted to no-effect or exclusion. Operator D1 public configuration adapter remains isolated standalone extension. Hardware verification and acceptance are **not performed**.

Evidence classification: implemented/source-compared code and real **offline** configured-path reach with explicitly admitted resources, states, calibration, RNG, raw frames/ROI/status are verified as described. Passing tests do not promote provenance. Actual selected model/reasoning telemetry is unobservable. Queued only: stale nonbehavioral comments/diagnostic wording, including old M8-027 captured-finalization prose; no extra mechanism or verify round requested.

---

# Drive-off pilot: independent build check — PENDING

No implementation verdict yet. The builder is working in isolated codex-driveoff-pilot-build from read/contract commitac8d42a4. This report is the checker's prepared verification plan, not a claim that any test exists or passed. No code/tests/git changes or concurrent test execution by this checker. Actual model/reasoning telemetry is unobservable.

## Required production reach and independent expected observations

1. **Configured entry and real action.** Load the shipped DriveOff JSON through the actual BehaviorContainer factory with the same robot/context used by configured chooser/manager admission. Verify absent extraDistance gives96f and shipped extras give156f/141f, with native eligibility supplied from actual platform/raw carrying/off-treads/severe/whiteboard owners. Explicitly distinguish entry into an activity from an already-selected admitted-state boundary. For entry: Timerfloat→chooser→PublicState.UpdateActivity order (005B3142/314A/3154); native Feeding2/MeetCozmo6/PutDownDispatch13, no added activity triplet/idle for these exact trigger-absent configs. A manually constructed ShippedBehaviors instance or reflection/private completion call does not satisfy the production claim.

2. **Admission, packet words and completion.** Trace actual StartActing callback-before-tag-before-busy/QueueNOW, ActionList deferred Init and real-tag matching E2G5A delivery (005BDACC;005BF0BE→005BE1E6). Native completion clears tag before busyfalse/callback synchronously; a host RunAction/Post tick is a different route. Verify one line with main-pose yaw, stored ForwardPose XY and separate f32 product/add operations; signed defaultspeed100/-80,accel200,decel500 (005470F0/00547628..AA). Mutate carry after SetPose to distinguish stored-center from late recomputation. Observe Clear3C→line3D sevenf32→Execute41 with actual C6/currentsegment feedback and same owner. No lattice plan is requested; no elapsed-time synthetic success. Assert real failure/cancel/reentrant callback and teardown behavior where source packets specify it.

3. **Concrete animation identity and API payload.** On retry, force different chosen names while retaining the first cached clip. Expected PLAYoldclip but REPORTcurrentselectedname+78 in variant1/string (005441EC/F2;00544472..7A;00543F3A..42). Test cannot use clip.Name as the expected completion oracle. Check actual CA/CB/D5/5F lifecycle, end-wins, finite-loop handling and matching streamer clear on destruction including null==null. Start/loop/end actions must be the actual handler producers, with cancel reentrancy and track-lock behavior retained. Source-complete evidence must cover every changed concrete-action claim, not just a callback bridge.

4. **Captured clock, mood and whiteboard.** Use a controllable source clock that advances between phase/callback observations. Each engine tick captures one f32 Timerseconds; Mood.Update before AI/actions uses the captured value and caches+130. Group selection uses actual GetSimpleMood and cached+130 even if wall/source time advances afterward (00513BE6/BEA,00513E84..8A,0058A954/95C). No duplicate Freeplay.Advance. Success fullresultword0 delivers objective4 public/API before local callbacks, then triggers actual Mood event using its own sourceTimer capture. Verify shared robot-lifetime LastDriveOffCharger float stamp across distinct contexts and later eligibility; default-1 must not reset when a facade/context changes. Separate periodic mood API output from same-tick event mutation.

5. **Vision gate.** Exercise a drain with multiple results where an earlier handler fails and a later succeeds: aggregate staysfailure, all source-ordered results/timestamp/broadcast work still completes, then platform/Mood/AI/ActionList/Path phases are skipped. Next empty or successful drain returnsfalse and allows phases; no sticky historical status (00654480/482,006544EE/500,00654A74/78,00654AC0..CE). ProcessedImage carries raw u8 imageMean from resultbyteC8 when mask100 is set, otherwise0; never normalize it to bool. Unsupported processors must not manufacture success. Tests must enter the actual mailbox/drain producer, not merely set the final engine delegate.

6. **Normal contact, map/configuration/audio.** Admit synchronized full RobotStates; contacttrue observes old-owned pose before new incoming history admission, retains charger identity/Known root pose and actual map cache/relative/quad/insert bindings. Conditional pending frame-tail rebuild must use source all3 configurations, including FIRST/processedIDs/duplicates/prune/nativecount/tallest. Use independently source-derived adversarial inputs already supplied in the read packets (greedy AB/AC suppresses legacyBC; fourvertical seed groups can retain duplicate first3; FIRSTupper-neighbor tie). Exercise root/MAX-asymmetric dimensions, rawcarry, scalar+.5 center, strict boundaries and known cv orientation exception→defined RectangleFallback (EH004E64FE→004E65C6→004E6778), rather than generic managed-gap fallback. Unknown owner/root/geometry must preserve the entire prior native publication/rootcache/flags/counters and retry visibly. Verify tallest/publicstate then A-before-B audio. In Feeding2 the source world-event branch is suppressed; in non2 state a stacks>1 transition produces checked audio event requests. Inspect actual requests/order, not just cache contents or shared tick association.

7. **Objective recipients and public API hold.** Objective4 filters use actual constructed activity owners/subscription lifetimes; Sparked configured mismatch has no mutation; Socialize preselection M1-034 skip/log must not fabricate phase0, while actual OnSelected initialization survives deselection and owner retirement. Other objectives/changed configs stay their named gaps. Whole public Cache/Stacks/Bases/Pyramids/MID/FirstSeenSec/ConfigurationSeen adapter remains unchanged pending operator decision; engine consumers may not accidentally retain its all-pairs results. No dependent API change or finished-whole-pilot claim until that hold is explicitly resolved.

## Review and evidence rules

Compare the entire stable diff with the approved rows/primary packets, then inspect actual production call reach and meaningful independent expected values. Reuse prior native witnesses; reopen only a concrete changed ambiguity. Tests of copied helpers, private completion or substituted final delegates are unit evidence and must be labelled accordingly. Run independent focused production/regression checks only after the builder's testhost stops and manager authorizes; do not race shared artifacts or repeat a broad suite. Manager owns the once-only final full gate.

Every threaded test must release deterministic barriers in finally and join all runtime workers before pose/parent/event/rig disposal, including failed assertions. Look for source-state races and callback reentrancy; do not add generic locks or listener catches without a specific defect and approved behavior. Verify no unsupported input is published as native empty/success. Inspect negative-control restoration and actual assertion results; do not overstate quiet logs or unobserved hardware.

Final review will state implementation/code verification, actual offline production reach, hardware and acceptance separately; quote current records before whole-record/residual claims; list exact remaining gaps and any defect correction. No manifest statuses, evidence, approvals or review states are changed here. **Current verdict: PENDING, no build PASS.**

## D1 operator disposition — supersedes the adapter hold

Operator decision delivered through origin/main5ae4d66a REVIEW.md approves option1: retain the WHOLE public configuration adapter as a documented standalone stack extension. Its existing public snapshot/MID/FirstSeenSec/ConfigurationSeen semantics must be preserved, and it MUST NOT feed ANY behavior or native engine state. The previous POLICY HOLD is resolved; this does not turn the adapter into recovered native behavior.

Final review must search all consumers, including BehaviorOnConfigSeen.Recent, and reject any retained behavior dependency on legacy Cache/FirstSeenSec/all-pairs data. OnConfigSeen's source recipient must consume native per-type counts and its own f32 clock state: clock now; dt=now-previous evaluation; store previous=now; for each tracked type, growth above saved count stores common growth-stamp=now, then saves current count; result dt<4.99999 AND abs(growth-stamp-now)<1e-5 (005DB2D4..005DB396). This is not per-arrangement first-seen age, reacted-ID history or a hybrid native-ID/legacy timestamp cache. Use actual source recipient clock/order and explicit input availability, not a fresh invented timer. Preserve public extension independently and verify behavior/native consumers cannot accidentally call it. No new native extraction or code/test change by checker.

Verification checklist item7's pending hold wording is superseded by this explicit decision. Requirement now: unchanged public extension semantics PLUS complete isolation from every behavior/native state consumer, with actual native-count/time recipient tests. Whole-pilot build verdict remains PENDING until stable diff review and authorized independent execution.

### Narrow read refinements reopened before wiring

Completion-union serializer/default/string width, actual resultbyteC8 producer/processed-image layout, and Q17 config/scoring arithmetic require supplemental source confirmation. Getter/consumer evidence alone was insufficient; the initial whole-read PASS overstated closure and is provisional for these exact units. Builder has stopped those units. Independent check will record their exact primary answer before implementation. Preserve public extension query semantics (GetTallestStack/membership included) while using explicit native accessors for all behavior/engine consumers under D1. No source/tests/git changes by checker; verdict remains PENDING.

Pre-wire U1/U2, V1–V4, Q1/Q2, N1–N8 and DOWN source refinements independently PASSed and recorded with exact primary addresses in the read-codex-check addendum. Notably processed-image byte is RAWimageMean, not bool; actual00712FFA call establishes timestamp/count/modes/mean order. Plain DriveStraight union wire06; animation uses String8 narrowed count+bytes. Mood null-input keeps actual curves/watchers/update, clears global repetition without inventing a default. DOWN has no carried exclusion. Chooser uses Robot.Context+14 RNG, not System.Random. Final implementation verdict remains PENDING; no tests or code changed by checker.

Chooser ownership and bonus distinction follow-up PASS recorded in read-check: canonical context RNG sharedwithgroups; chooserbonus(0,0) differsbehaviorrunninggraph(0,1); Feeding actualuniversalchooser precedesstagemap. Audio endpoint recipient remainsOPEN: no wholebackendclaim fromA/Btestdelegates. D1 Seen projection diagnostic-only afteractualnativeadmission, publicqueryextensionunchanged. Final buildverdict PENDING.

DriveOff-only removal of host Library!=null admission prerequisite sourcePASS; retain actualbase/internal gates and downstreamanimationreadiness diagnostics. RealCA/CBfixture mustloadactualassets; nullLibraryadmissiontest separate. Finalplatformfalsechooser tick is NOTcoveredbyonchargercompetitorrejection; exactderivedeligibility/scoring/RNG path needsclosure. Audioendpoints remainrealSTOP, nofacade-onlyPASS. BuildverdictPENDING.

Audio A1–A7 independently source-checked in read-codex-check: control-scoped per-rowPASS; A6 ONLYfresh/noactivevoice, explicitSTOPactiveconsumer/scheduler/render/output. Requiredminimalunit is canonicalrealowner/readiness/nativeinput/sharedFIFO/1901/scopedstate+query+eligibleobservers/sourceaudio-tail, notfacade/testcallback/unuseddict. NoaudioimplementationorfullaudioPASS fromthischeck. BuildverdictPENDING.

C6 six competing eligibility gates/base score virtuals independentlyPASSed; Interact exactselectorcaller/costbody identified but liveFaceWorld/pose-anglebindings remainpreciseSTOPifnotalreadycovered. CanonicalPutDownDispatch mayclose specificconfiguredDriveOff route withactualLookcandidate/commonstate/order/RNG, noLookactionbody beforeDOfinish; noMeet/Feedingclosure inferred. Details inreadcheck. Qualityextraction newpacket independentlyPASSedbyresearch_reconciliation forreceiver/transport/camera only; actualROI/clippingproduceravailability notcertified. BuildverdictPENDING.

## Standalone facade isolation — preliminary read, not final code PASS

Reviewed current builder FreeplayStack/FreeplaySystem/BehaviorManager and unchanged EngineNeedsOwner offline adapter. The design preserving historical Create(ctx,clock,needs,...) as an independent standalone manager is compatible with the canonical engine owner correction: existing UseNeedsForOfflineTest explicitly throws on a live engine, so implicit custom-needs engine replacement was not a previously working live path. Offline tests that need the engine to tick their replacement Needs must invoke that explicit test-only admission; preserving ctx.Needs alone does not give engine ownership. Report this intentional fixture setup change honestly rather than saying every prior offline side effect is unchanged.

Actual current diff still needs narrow isolation confirmation before final PASS: BehaviorManager's bindSensorOwner:false prevents replacing the sensor delegate but does NOT prevent its constructor/BindFreeplayInputs from assigning ctx.Arbiter.ManagerDispatchesReactions; Create defaults ctx.Mood to canonical native owner and can register a host-clock watcher if its callbackID is0; and CreateNative returns public Context referencing the canonical object, so passing that Context into historical Create currently mutates its Needs/AI/Penalty fields. Independent manager identity alone cannot establish no canonical-owner mutation. These concrete public alias/input cases were sent to manager/builder for a guard or source-backed availability proof; no proposed generic refactor or unsupported new API exclusion. Standalone custom contexts/inputs must remain usable, and returned native observer Dispose/Tick must remain observational.

No tests/build/source edits by checker. Pending final stable diff and focused execution; this paragraph is an issue list for the active implementation, not the completed review verdict.

C7 LC1–9 independent sourcePASS recorded: actualfactory/typedbase/rootJSON and completector+LoadConfigwrite-set, inactive46recipient/Stopno-op, rawflags/base scores/commonhistory. ScopeNONRUNNINGlosingLook duringDOrunning; nextselectedLook remainsexplicitowningMISSING. Noactionbody/featurecompletenessclaim. No tests bychecker; finalbuildPENDING.


## Stable-core static review checkpoint — PENDING, blocking corrections identified

Read-only review of builder checkout, against baseline ac8d42a4 and the independently checked native contracts. No checker tests/builds executed; builder testhost is active. This checkpoint is not a final build PASS. The concrete configuration geometry/producer text was read completely: signed MIN/MAX identity, MAX-first geometric binding, UP/seed versus DOWN carried asymmetry, root/local coordinate distinctions, independent per-container queries/processed sets, old-first reuse without current dedup, ordered pyramid pruning, constructor-empty snapshots, and staged publication were compared with the checked CM13-05 and whole-pilot packets. The action diffs preserve separate current selected name and cached playback clip, concrete default6/String8 wire, synchronous matching-tag callback, and semantic clip destructor equality. These observations do not close the remaining full-pilot source stops or integration tests.

Blocking finding D1: CubeGameBehaviors.cs BuildPyramidBaseBehavior.Targets (lines591–599 in this snapshot) still reads M.Configurations public host membership twice and public PyramidBases.FirstOrDefault. These directly select behavior targets and violate the operator-approved isolated-extension boundary. Use native IDs/cache for these reads; no native midpoint is needed. The authorized OnConfigSeen Recent diagnostic projection after actual native admission is a separate exception and must not become an eligibility input.

Blocking UNKNOWN finding: ManipulationSystem.cs native accessor family (lines119–135) returns empty arrays/null/false when InputsAvailable=false, although NativeConfigurationManager correctly retained its complete prior native publication. This converts unavailable geometry into fabricated absence and may change source behavior eligibility/target exclusions. Preserve the retained publication for consumers when one exists, or explicitly skip the entire unavailable consumer unit without changing prior state; a diagnostic alone does not make empty a source result. Strong-cache accessor has the same availability issue to review with this correction. Sent both findings to manager and builder before any correction.

API compatibility question sent for resolution: replacing DriveStraightAction optional-speed constructor removes the prior named-only shouldPlayAnimation call shape. Preserve that supported source-level call shape with a compatible overload unless the manager establishes it was not a supported API contract; direction-dependent two-argument speed must remain the newly recovered default. No unrelated API redesign requested.

Snapshot SHA256 values below identify bytes read, not approval of files with findings. Any later changes need affected recheck; unchanged files need not be read wholesale again.

- `Manipulation/NativeBlockConfigurations.cs`: `387a6e706fb6ff6a2b75b365ee6a1f2b735ab229983acf4b77b958a0e52f30d3`
- `Manipulation/NativeConfigurationGeometry.cs`: `0bdfb280399871570450c53f1c6fb13c17b99520b789b3ae56d29b4ba79d163e`
- `Manipulation/NativeConfigurationManager.cs`: `920cd85d29178b41dacb1d3f33896e0258364192bf05e68a9d15e6767da78375`
- `Manipulation/BlockConfigurations.cs`: `5302886a73e3a23439d39838fa92fab1b5518e4eaf18cf78e90709d9dccdb006`
- `Manipulation/ManipulationSystem.cs`: `d30948d25689408a620309d57d4937e0039231ed73288c2bea171d7a959fd568`
- `Vision/BlockWorld.cs`: `fc834f0df2cfb047592c055275303e5c29b4e706a86e9fb1cd2aaf23a0964d4b`
- `Behavior/CubeGameBehaviors.cs`: `42e66c6f5346228fa336bcc2635adf0b86d9fce20a1a66f1d3a2742f7d85fbce`
- `Behavior/ChargerBehaviors.cs`: `bc9e0a3ba65057554955dfe9c060a542c41e9fcab004ce3338c5ce1b7d1591bc`
- `Behavior/SteppedBehavior.Actions.cs`: `ba0fd6612c40b4cfd553eb38472fcd3cfe8301e532c0198c1ba66f4e3edda424`
- `Manipulation/DriveActions.cs`: `618f16898c9a6176c110822749759f0dfece17367127f9e61810b36394803748`
- `Actions/ActionWatcher.cs`: `65eee66f7ea331ef6b6f6bc151c2520c674091f8b8c25f9c2ffcc4d9da1ff498`
- `Actions/IActionRunner.cs`: `f1f73d16586f0acbb1bfab31c7cf7e6e0b42bcb271df7c4318ca5acbc434107e`
- `Actions/RobotCompletedAction.cs`: `ad0d1481f1a443a25346d4abb4cbfd4539845e1ee497f4725487a8139cd4a3a3`
- `ActionCompletionReport.cs`: `79251fc3eaad742c50054a6bcf4697cec40a03ac2f4282f38a4d962c39eec385`
- `Animation/TriggerLiftSafeAnimationAction.cs`: `53f95d7c968bba87b2c71dda2ce9aba28b401fecb77987762577e781ccf5506a`
- `Animation/AnimationScheduler.cs`: `769b42d53dc6d6f6f5c2c1acebb30a464e16188eb472871c82db9b9e6e2777af`
- `Animation/CozmoAnimations.cs`: `758e347b793bf95c723b3789f63d71258f05b112eba3d491d209ed93bf66b1f4`


### Affected core corrections — static recheck

Builder correction now inspected: BuildPyramidBase.Targets consumes native membership and MIN/MAX IDs; no host cache/MID input remains in those three reads. Existing canonical native manager accessors now expose its complete retained snapshot on InputsAvailable=false with an UNKNOWN diagnostic, and absent owner raises typed NativeConfigurationUnavailable rather than fabricating empty/false. The strong-cache copy likewise returns the retained snapshot; its existing explicit null/unavailable caller boundary remains separate from a valid constructed empty cache. Direction-dependent DriveStraight defaults retain the historical named shouldPlayAnimation call shape through a bool-only overload. These specific static findings are resolved in the inspected bytes. Focused tests are still builder-owned/running; no independently executed tests or whole build PASS asserted.

- `Manipulation/ManipulationSystem.cs` corrected SHA256 `2d51d7b565eba3b73baf578fd73d5efb3731dc93cc609c3c69e27fe60424c60b`
- `Manipulation/BlockConfigurations.cs` corrected SHA256 `6fb207c654b562e0bd73e4a645c61242996a7cb7d32507c5561f2413a2a602d9`
- `Behavior/CubeGameBehaviors.cs` corrected SHA256 `42e66c6f5346228fa336bcc2635adf0b86d9fce20a1a66f1d3a2742f7d85fbce`
- `Manipulation/DriveActions.cs` corrected SHA256 `618f16898c9a6176c110822749759f0dfece17367127f9e61810b36394803748`


## Remaining-unit static checkpoint — PENDING integration and tests

Read actual configured runtime/base inputs, concrete losing Look candidate, actual per-activity objective subscription owners, facade ownership guard, native Mood binding/loading/clock, Vision worker/result queue/raw mean/quality receiver and Camera API diff. No checker tests/builds executed while builder hosts run. Correctly bounded: only configured PutDownDispatch admission is built; unsupported specialized activities retain prior selection visibly. A selected Look action retains prior execution and reports MISSING rather than running a fake successful action. Constructor source default41 is a filter invariant, not permission to suppress changed objective configurations.

Two additional concrete defects sent to manager/builder and accepted for correction: (1) Look objective callback is unconditional no-op with no root/activity scoring objective41 guard, despite the proof applying only to absent/default41; (2) MoodModel.LoadNative parses a present empty/unreadable resource without handling failed-reader/null continuation, so watcher/owner construction can be lost. N1-N8 requires visible failed-input handling followed by retained/default curves and actual watcher/update continuation. No broad mood parser or objective implementation expansion requested. These findings remain pending affected source/tests recheck.

Facade alias corrections inspected: canonical Context or a nonnull canonical Arbiter is rejected before constructing the standalone manager or mutating ctx fields; default standalone Mood is independently constructed; explicit canonical Mood is not given an extra host-clock ActionList watcher even when native callbackID is0. Host Dispose uses its own manager/severe owner and canonical observer Tick is observation-only. Canonical ownership mutation findings from preliminary pass are addressed statically; integration regressions still required.

Vision receiver static comparison: raw mean byte and ascending set-bit mode vector, tag43 timestamp/count/U32modes/mean payload; per-drain aggregate reset and continued result drain; entry-captured Mood clock; quality unsigned strict elapsed threshold, raw prior-quality assignment after synchronous broadcast, repeat globals reread; camera validation before reliable request/pending/API and live auto flag are represented. Producer ROI/rectangle/clipping and other handler stops remain explicit; no full vision source/acceptance claim. Worker completion queues owned pose copies and result application moves host FrameProcessed outside processing lock; final lifecycle tests/joins and changed fixture cleanup still need review.

Source bytes inspected (SHA256; this list is a checkpoint, NOT whole-file or whole-pilot PASS):

- `Behavior/NativeBehaviorRuntime.cs`: `fb2214fef04b3fbf52a9c96648edac0d373f252fa6cdae6d80ae26171e5f72b5`
- `NativeBehaviorRuntime.cs`: `5f7e39581738fafe3db50588688642a3c3b467e1ca02403db96b348048d288c2`
- `Behavior/NativeBehaviorBaseInputs.cs`: `f5a33e00a647d76b28364b1deabc773954f7bd7c2e657f0b7aed80adea31512e`
- `Behavior/NativeLookForFaceAndCubeBehavior.cs`: `ec678a4ec646b962d8a73b6af96128e2514b2d3c940aa8addcbc6e833fa0e9c0`
- `Behavior/NativeActivityObjectiveOwner.cs`: `c93c96dcd22977816f420eb148529c186328f0e22b66403d039b876fcaa2d345`
- `Behavior/Activities.cs`: `c79830df788386dbeb176f1241a611029ab01ff260be078fbaf62a18766bdafe`
- `Behavior/BehaviorContainer.cs`: `61814932df5b82d2c969de668c36b9b2aeda2fd10ced1fbe8d17f6a5f8804e94`
- `Behavior/FreeplayStack.cs`: `fa644aff3a7dc3542091a3f890137d405c8c3e765052900ca06ca46091c3875d`
- `Behavior/FreeplaySystem.cs`: `840081077add07246ce85c5b7de5e16e8df6101a803b91f3b4b65c611bcc03e5`
- `Behavior/BehaviorManager.cs`: `0fcd0b0f50fc1ed1e5e19a80687910f5760f5c798ac95165882ea529ff2a3d80`
- `Behavior/Mood.cs`: `3fa3f11557134da81c4f962d75edeb5146c4d5e6ad44a22b3c8d3deb6353e4e1`
- `Behavior/MoodLifetime.cs`: `658b907bbbceaf4c3bb43898ead927caca46ec3485959b1d702d429e43f8d1e9`
- `Behavior/IBehavior.cs`: `8c4014d99b25bc550ef1ba09d920b784581022b1badbd88de409738c67998ec4`
- `Behavior/SteppedBehavior.cs`: `125480ed38534bcbdf28e98f82e5be41270f857c1a6b2cbef5c9112ac1c579c5`
- `Vision/VisionSystem.cs`: `af0aa94d5c1f721d9d65fc1dd06d21161471c08b15a851379c216a7f592c7802`
- `Vision/VisionResultQueue.cs`: `4f085642bc6bf34537b6568c280a108acbc4b05bba8bede8c4b9652c722d3a5b`
- `Vision/VisionImageQualityReceiver.cs`: `c84fb81176d15ced7c13fd98366771e3f7a6f3f63a2c7b8ecb52747f0df6d4fc`
- `Camera.cs`: `c314a51d46a67450ff7f95708bd56277bf83f0b8fe8ab971e5eb307c344a8151`
- `CozmoEngine.cs`: `6130af1031e48da93f1146490bf2d27a6c4ee2ca488cde29580b3d8248271b76`
- `CozmoRobot.cs`: `7baf3a045aa004f6fb40fa716f65779f729b3f09f3cc61ddba3920caa043df28`


Mood delivery join independently primary-PASSed and detailed in read-check: typed-only output omits required rawAPI61/local61. Required actual external guard, appenqueue-before-localemit,37byteU8count9+9f32 payload. Builder correction pending; no test-only suppression accepted. Narrow ScoringChooser.OnSelected absence also checked: Activity005B314A calls chooser vslot20; Scoring vtable0102CBDC+8+20→0060A7BE BX LR, so no missing chooser mutation on the admitted scoring route. No tests executed by checker.


## Independent stable-candidate execution and affected review

Actual independent run from builder worktree (only testhost active; builder held mutations):

```powershell
& 'C:/Program Files/dotnet/dotnet.exe' test cozmo-stack/tests/Cozmo.Protocol.Tests/Cozmo.Protocol.Tests.csproj --no-build --no-restore --nologo -v:q --filter 'FullyQualifiedName~DriveOffPilot|FullyQualifiedName~NavigationTests|FullyQualifiedName~FreeplayTests|FullyQualifiedName~CorrectionTests|FullyQualifiedName~HelperStopAndPyramidLoopTests|FullyQualifiedName~M12CheckedRowsBuilderTests|FullyQualifiedName~BehaviorFrameworkTests' --logger 'trx;LogFileName=codex-independent-focused.trx' --results-directory .scratch/driveoff-pilot
```

Exit0: **303 passed,0 failed,0 skipped**, duration1m7s. Console/TRX: builder `.scratch/driveoff-pilot/codex-independent-focused.log` and `.trx`. This includes current new actual-worker marker-failure/continued-drain/Robot.Update gate/reset test. It is independent execution of builder tests, not independently authored test coverage or hardware evidence. Explicit Python313 `-X utf8 re-analysis/tools/fidelity.py --check` independently exit0,710records; output builder `.scratch/driveoff-pilot/codex-fidelity-check.log`. Manifest statuses unchanged. All51 individual handoff source/test hashes exactly matched `.scratch/driveoff-pilot/candidate-sha256.txt` before execution; no checker code/test changes. Builder-reported aggregate2a91a0b... identifies its serialization convention; exact per-file matches are the independent byte comparison.

Affected static corrections: Mood failed-reader now handles empty/read/parse failure on native load and continues defined null-input, keeping actual owner/watcher/periodic update. SendEmotionsToGame preserves nullRobot/actual captured native external-interface guards; app raw+typed is queued before local61 sync delivery,37byte count9/f32 payload. Standalone AttachRobot captures actual native owner rather than inventing interface presence. Native root-cache disposal (including UNKNOWN single removal, staged/old roots and destruction) now uses actual PoseTreeGate in existing World.FrameGate→manager→pose-tree order. Native retained-publication getters and all three BuildPyramid target reads remain corrected.

The three Navigation oracle changes were read fully: expected action distances derive from fixture captured initial object/main-pose vectors (Flip norm+20; KnockOver distance-85; Ram distance), with independent f32 stored-forward endpoint recipe; old action displacement ranges, speeds, packet count/order and success/arrival assertions remain. No expected distance is backsolved from emitted path endpoint. Those tests remain fixture/offline evidence, with existing libm/quaternion boundary and0.01position tolerance explicitly labelled; no firmware model edit or physical-position relaxation was used.

New threaded vision fixtures join via Vision.Dispose in finally before rig/history/pose/ROI owners retire. Configuration fixtures release actual robot/native publisher before admitted child poses. Actual configured route loads shipped animation assets; raw robot states/CA/CB/C6/calibration and explicit RNG words are supplied inputs. Actual canonical PutDownDispatch factory→manager chooser→DriveStraight ActionList/path wire→received completion is reached; Meet/Feeding/selectedLook are not claimed. Vision tests use real worker/raw-image decode/producer/result drain with substituted admitted calibration/world-status/ROI; they do not claim real robot JPEG or live rectangle collection.

**One final blocker remains at this checkpoint:** the C7 callback's new default41-first filter reads `BehaviorObjective`, but actual native factory/root/scoring JSON never assigns that property. A changed `considerThisHasRunForBehaviorObjective` therefore silently remains41 and misses the promised MISSING branch. Root accepted this finding; builder now owns a narrow actual-root/activity input guard and actualJSON regression. Shipped absent/default41 case passes. Final candidate PASS withheld only pending that affected correction/recheck; no whole-repeat audit or full-suite run requested.

All full-pilot boundaries remain: actual audio object1/scoped active observers/scheduler/render STOP; live ROI rectangle/GetROI producer STOP; Meet selector/Feeding.Update/selectedLook and other named owner/lifecycle handlers remain MISSING. Passing303 tests does not turn these into absence/no-effect/exclusions or whole behavior acceptance. Hardware and acceptance remain unperformed. Actual selected worker model/reasoning telemetry is unobservable.


## Record-wide handoff recommendations — current records, no status changes

Current literal titles/status/evidence reread before these recommendations:

```json
{
  "id": "M7-026",
  "title": "MoodManager::GetSimpleMood and the mood time MoodManager+0x130 (inputs to the animation group draw)",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/20261012-m5-x9-extraction.md section 4f: GetSimpleMood 0x0067C084 (Default; Happy > 0.33f -> Happy; Confident < -0.29f -> Sad; Happy < -0.33f -> Sad; constants 0x0067C0C0..0x0067C0C8); the group draw reads MoodManager+0x130 (0x0058A95C), written only by MoodManager::Update(t) 0x0067B67E and Reset/ctor."
  ]
}
```

```json
{
  "id": "M8-027",
  "title": "ProtoHandle subscriber-callable census: which callables the 8 ProtoHandle kinds hold at teardown and what destroying them does",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/overnight/20261015-ov53-55-check.md: ProtoHandle vtables (8) confirmed; EngineToGame release 0x00664708, unlink 0x006634F4 (nulls the callable, decrements node refcount +0x20, frees at zero 0x0066352E..0x00663560).",
    "re-analysis/research/overnight/20261016-ov105-107-108-answer.md: void() signal 0x0105DF8C: one subscriber via RegisterTransport 0x0083B9A0..0x0083BE4C (bind vt 0x0103795C trivial); Dispatch signal TransferQueueMgr+0xC: two RegisterTask::$_0 (vt 0x01031418, destroy memory-only) from TransferTask::Init 0x006A4358..0x006A438E; release bodies 0x004EF138, 0x0051D8F8, 0x00591550, 0x0064E618, 0x00664708, 0x00664EB8, 0x006A3EFC, 0x0083BED0 identical; ~TransferQueueMgr 0x006A3860..0x006A3898."
  ]
}
```

```json
{
  "id": "M13-017",
  "title": "BehaviorDriveOffCharger: runnable on the charger, the drive distance and the leaving-the-contacts update",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "ctor 0x005C0980 stores 96.0 + json extraDistanceToDrive_mm at +0x11C (0x005C09D4/0x005C09E2/0x005C09E6); IsRunnableInternal 0x005C0B10 returns robot+0x34A (the on-charger-platform flag; contacts are +0x338)",
    "InitInternal 0x005C0B18 takes the reaction lock, pushes driving animations when the animation state is 3 (0x005C0B4A) and transitions only when robot+0x355 == 0 (0x005C0B54); +0x355 is the robot's OffTreadsState (OnTreads = 0), written only by Robot::CheckAndUpdateTreadsState 0x00512088/0x0051208E and named by OffTreadsStateFromString 0x0078DF58 (OnTreads 0, InAir 1, OnBack 2, OnLeftSide 3, OnRightSide 4, OnFace 5, Falling 6)",
    "TransitionToDrivingForward 0x005C0BB8 drives +0x11C (0x005C0C02/0x005C0C08)",
    "UpdateInternal 0x005C0DA8: once off the platform (robot+0x34A == 0) it records the time at robot->[+0x264]->[+0x18]+0x44 and returns 2 (0x005C0DF4..0x005C0E0A); while still on the platform and robot+0x355 != 0 it calls StopActing(false,false) (0x005C0DB6/0x005C0DC4) and waits; on the platform with +0x355 == 0 it transitions to driving forward (0x005C0E18)"
  ]
}
```

```json
{
  "id": "M13-040",
  "title": "Contact-true charger observation: live ingress, located identity, Known pose and ordered world/contact effects",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "RobotState sync gate 0x0051293C..942, contact bit12 0x00512AAC/AAE and SetOnCharger call 0x00512AB4; SetOnCharger 0x00511990..0x00511C2D observes on every true input before old-contact gate 0x00511BA8/BB0. Contacts +0x338; platform +0x34A.",
    "ChargerDockPose 0x004EA304..0x004EA35A: Z rotation pi bits 0x40490FDB, translation (30,0,0), robot pose parent and ChargerDockPose name. Located family4/type13 first/current-origin query 0x00511A1C..A52, 0x0061EBF2..0x0061EC26; factory and connected identity/unique SetID 0x00511AD6..0x00511B40, 0x004EF468..0x004EF528.",
    "Known observation 0x00511B4A -> 0x00506B70..0x00506C48: reparent/root flatten, existing/new branch, SetPoseHelper 0x0050609C, distance -1, confirmer count1/reference/existing timestamp. Actionable cache invalidation 0x004E01E4..226; common SetPose 0x004EF590..646. AddLocatedObject 0x00622AC0..EB0 includes obstacle deletion, VizManager pointer store obj+0x54 (0x00622D24..0x00622D30; CozmoContext+0x24, ctor 0x004EA734; read by EraseVisualization 0x004EA5E8..0x004EA5F2): viz, no effect on 1-4 and notification.",
    "Notifications 0x00506F98/FA8/FB8 world/map/state-change; unchanged return 0x00506296..29A; changed nonzero new state map 0x005062B4 and active light recipient. 0x00639840 compares NEW state r3 with1: Known trigger0x25/layer2, other changed trigger1/layer2; unchanged no request.",
    "Contact edge/store ordering 0x00511A66..ACC, 0x00511B50..0x00511C14; platform setter 0x00511D44..DCA; existing footprint tick 0x00513CD8..0x00513E2A and M4-019 threshold send 0x0063427E, 0x006342C8..CE, 0x00634364..368.",
    "re-analysis/research/20261021-m13-017-located-charger-check.md: independent check of A1-A11/P1-P4 with corrected A9; supplementary mount and exhaustive caller census excluded."
  ]
}
```


- **M8-027: RECORD-TO-SETTLE candidate for Claude confirmation.** Its CURRENT unresolved explicitly says every held callable release/remove is memory-only, no log/message/invocation and nothing behavioral remains to build. Current UI hub nulls/unlinks callbacks, retires signal token, and checks node value before snapshot dispatch; GC replaces native refcount/storage. No allocator/captured-pointer imitation is needed. The stale captured-finalization diagnostic is queued cleanup, not a reason to manufacture an engine behavior. This is a confirmation recommendation under the record's existing evidence, not whole subscriber graph or subsystem acceptance.
- **M7-026: conditional RECORD-TO-SETTLE candidate, not yet final at this checkpoint.** Actual Mood.GetSimpleMood and group CurrentMood/cached130 bindings were built and reviewed. However an additional owned consumer in CURRENT unresolved, DrivingAnimationHandler004F0D2C..74, still used only a test seam and default/needs fallback. That concrete residual was found during record-wide comparison and released for correction in this same candidate; settlement recommendation waits the affected code/tests. No extension to full Mood schema/event/actions/lifetime acceptance follows from this named record.
- **M13-017: retain IMPLEMENTATION_GAP / remaining-record handoff, no record-wide settlement.** The specific behavior state machine/admission gate/distance/wait/redrive/owned action5A/whiteboard/objective/mood path is now implemented and exercised through canonical configured PutDownDispatch plus admitted full states and real received action/animation events. The record's broader live production path still encounters actual audio recipient/active schedule/render STOP and live default quality ROI/other handler producer availability, with general Meet/Feeding/enclosing activity and selected Look boundaries explicit. Markerless/Ramp/HumanHead footprint inputs and broader named cached-pose/component lifetime obligations have not been certified by this pilot. Do not call those no-effect, excluded or covered by tests of source-backed pieces. Conversely, old claims that all native pose-notification/map-height/cache/insert continuations are simply absent are stale: current source packets G4/G5/S12 and actual code recover positive branches and native staged configuration producer; cite precise residuals instead of repeating old blanket prose.
- **M13-040: retain IMPLEMENTATION_GAP / exact shared-dependency reconciliation, no whole-record settlement.** Existing received synchronized contact-true identity/current-origin/Known observation/cache invalidation/world/map/light/contact ordering remains the checked reused path. New native SetObjectPoseChanged/common container/count/tallest/frame-tail publication closes that earlier concrete producer gap, and current positive map cache/relative-height/quad/insertion bindings exist. The conditional configuration notification still reaches an unbound real object1 audio consumer; live frame/default quality producer and noncube footprint availability plus the current record's broader cached-pose destructor ownership boundaries are not newly certified. Whole title includes ordered world/contact effects, so this candidate cannot erase those dependencies merely because direct contact/platform/action tests pass. This is not a claim that every historical unresolved phrase is still an unimplemented algorithm; manager/Claude must reconcile each current whole-owned boundary.

A source-checked partial candidate can be handed to Claude with this RECORDS-TO-SETTLE versus REMAINING register and a precisely labelled implementation verdict. That does **not** complete the whole DriveOff pilot, grant hardware verification, allocate proposed owners, approve exclusions or change any fidelity status. The operator's whole-behavior goal remains open; the finite checked improvements are reviewable without disguising the real stops.

### C7 actual-input affected recheck

Builder changed only the accepted four files: actual Look root key presence marks objective input unavailable; native activity loader checks actual typed-Look entry.scoring input; callback checks availability before invalid41 no-op; actual PutDown admission checks the relevant chooser candidates before Stop/OnSelected/public-state/motion. No global suppression, arbitrary objective mapping, fabricated score0 or fallback candidate was introduced. The two new tests modify actual copied root/activity JSON and traverse resource factory/tree/subscription/app admission, rather than manually setting the property. Independent affected command uses the same no-build flags/project/logger pattern with `--filter FullyQualifiedName~DriveOffPilotTests`, logs `codex-independent-c7.log/.trx`: exit0 **7/7 passed,0 skipped**,1s. C7 finding resolved in inspected/tested bytes. No whole repeat audit or suite run.

### Additional M7-026 driving consumer primary read contract

Known004F0D2C then primary004F0D2C..74: nonempty override stack wins at30..3A before Mood. Empty stack unconditionally loads actual handler+4 Robot, Robot+440 Mood at3C/3E, calls GetSimpleMood004F0D42. Only RAW1 selects triplet94/90/8C; other raw values use existing Needs.AreNeedsMet at50..66; copy6A..72. There is no native null-Mood-to-default fallback. Missing native owner/Mood is an M1-034 smallest selection skip preserving prior Current with visible M7-026; do not invent needs/default selection. Preserve explicit test override, otherwise read actual BOUND handler owner, not a replaced facade robot.

Numeric correction recorded explicitly: my first message incorrectly labelled raw1's triplet “happy.” Fresh known0067C084+primary0067C084..BE confirms constructor result2(Default), Happy>0.33 changes0, negative Confident/Happy changes1(Sad); current enum is `Happy=0,Sad=1,Default=2`. Thus Driving's raw1 is SAD, while Happy0/Default2 continue needs. The manager caught this label/test error before implementation; corrected contract and actual Sad/Happy/Default/override/null tests were released. No source code was authored by this checker. Final M7-026/candidate verdict remains pending affected build/review/tests.


### Driving selection Needs owner — bounded independent primary PASS

Current literal record before reuse:
```json
{
  "id": "M15-001",
  "title": "NeedsManager initialization, periodic update and need-bracket rules",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "CozmoEngine::Init 0x004EC9E6..0x004ECA0A",
    "NeedsManager::Init 0x00692574",
    "NeedsManager::Update 0x00695C9C",
    "NeedsState::UpdateCurNeedsBrackets 0x0069C12C",
    "needs_config.json",
    "NeedsManager::Init time threading 0x004EC9DA..0x004ECA0A, 0x0069257E, 0x006926CE, 0x006934B0, 0x0069358A..0x0069359E",
    "NeedsManager::PossiblyWriteToDevice 0x00695DC4 (0x03A2C940 against a microsecond clock = 61 s; ApplyDecayForTimeSinceLastDeviceWrite divides by 1e6 at 0x0069532C)"
  ]
}
```

Known preflight for NeedsState::AreNeedsMet, NeedsManager::GetCurNeedsStateMutable and IsNeedBracketMet preceded primary reads006960A8..BA,0069CB4C..B0,00782986..9E plus already reopened004F0D50..66. Actual bound Robot dereferences context+34 NeedsManager; getter returns embedded state+8 after UpdateCurNeedsBrackets with manager+140 config. AreNeedsMet refreshes state bracket cache and checks NeedId0,1,2, short-circuiting false; IsNeedBracketMet returns true only valid bracket0/1, with false fallback for unknown>=5. Current C# NeedsState uses Repair0/Energy1/Play2, Full0/Normal1 and bracket refresh. Canonical EngineNeedsOwner initializes Engine.Needs before RobotManager; bind this actual bound-owner Engine.Needs.State, never a standalone Freeplay context replacement. Existing explicit offline replacement remains the admitted owner in offline tests.

Required source behavior: after no stack override and SimpleMood not raw1, actual Needs all met selects96/92/8E; any Warning/Critical selects95/91/8D. Preserve explicit test seams; missing actualowner/Needs is null dereference under M1-034, skip smallest selection unit preserving Current and visibly diagnose, not invent default false/true. New production-owner tests must mutate canonical needs levels via existing actual component input and assert both selections, without replacing AreNeedsMet delegate; Sad1 must bypass Needs, stack override bypasses both. This is only producer-to-consumer binding and numeric branch verification, not a new whole Needs audit or M15-001 settlement. No code/tests by checker for this read unit.


C7 exact null-boundary follow-up (before final verdict): reopened primary005BC574..57A uses Json.isNull, so explicit JSON null preserves41 exactly like omitted key. The first actual-input guard rejected ANY present key, including this defined no-op; manager accepted a tiny correction to admit explicitnull while retaining MISSING for nonnull unknown overrides. Prior7/7 confirms string-input guards and unchanged shipped path, but final C7 PASS awaits the null correction and combined affected run. This does not invalidate the prior independent303 result for its exact bytes and tested inputs; it prevents overclaiming the newly inspected null branch. No further whole audit or code/test changes by checker.


### Legacy M5 driving oracle disposition (tests-only, before final affected run)

Independently read unchanged M5X15.MissingMoodAndNeedsFallThroughToDefaultAndQueueARealRunner and M5X19.MissingInputSelectionFallsThroughAndNonDriveMissingIsNotDeduplicated after builder reported16pass/2fail. Both use CreateOffline and actual constructed/bound canonical owners, not deliberately missing dependencies. Fresh Mood values0 give Default RAW2; canonical NeedsState initializes configured/default levels and its actual Full/Normal brackets yield all-met. Source004F0D50..66 therefore selects96/92/8E. Old95/default and missing-Mood/Needs assertions are stale inputs, not evidence for changing production. Authorized correction must assert actual canonical prestate (Default2 and each relevant bracketFull/Normal, no test delegates), then exact96 and absence of false M7/M15 missing diagnostics.

M5X15 must retain queue count/actual ActionList updates and strengthen real5A LoopId/type26/result0300000C NO_ANIM_NAME/variant1 empty-name proof in place of obsolete concrete-lifecycle-gap wording. M5X19 keeps exact retirement6cancel-recipient diagnostics unchanged. No firmware change, relaxed acceptance interval, injected completion or production behavior adjustment is justified by those two failures. Builder changes/tests and final affected review remain pending.


### Final combined affected execution — actual failure retained

Independent final affected command used prior no-build project/logger flags and filter `FullyQualifiedName~DriveOffPilotDrivingMoodTests|FullyQualifiedName~DriveOffPilotTests|FullyQualifiedName~DriveOffPilotMoodTests|FullyQualifiedName~M5X15BuilderTests|FullyQualifiedName~M5X19BuilderTests`; log/TRX `codex-independent-final-affected.*`. Actual exit1: **46pass/1fail/0skip,total47,3s**. All new driving Mood/Needs, actualJSON null/non-null objective, native mood/API, real5A and corrected legacy prestate units passed. Only M5X15.DrivingStackErasesFirstMatchAndChangesCurrentOnlyOnInit failed line87: expected two ordered PopDrivingAnimations log keys, actual same two plus legitimate MoodManager.Update.TimeStepTooSmall from the newly live canonical periodic Mood update. Test subscribed to every engine LogEntry, so unrelated diagnostics polluted its exact list. This is a diagnostic collector fixture defect, not evidence to suppress source Mood logging or alter stack behavior. Root authorized only filtering collector to DrivingAnimationHandler.* OR LoopBoundOverflow; retain exact ordered warning/overflow assertions. Reverify only this changed fixture; the unchanged46 passing cases are not silently represented as a47pass run. No fullsuite.

Affected source inspection complete: bound_owner Mood and Engine.Needs, rawSad1 and other0/2 branch, override-first/null-M1 preservation match primary; explicitJSONnull now shares source isNull→preserve41 with omittedkey. New actual-level tests use canonical State.SetNeedLevel with admitted config thresholds; the separate Boolean-seam cases are honestly labelled and test short-circuit order. Each queued action is canceled in finally before robot/resources retire; no new worker barriers or unsafe early pose disposal added. Final scoped verdict waits the single collector correction/run and final hashes.


## Final single-fixture correction and closeout

Independent inspected collector now admits only `DrivingAnimationHandler.*` or exact `LoopBoundOverflow`; all existing exact two-Pop order, current-only-on-Init, first-match removal,1000bound/errorflag and entry-count assertions remain. Production files are unchanged by this fix. Single affected test independently exit0,1pass/0fail/0skip,212ms (`codex-independent-collector.log/.trx`). The previous46pass/1fail execution remains above as the actual history; final changed fixture is verified without rerunning unrelated303 cases. M7-026 owning comment inspected at Mood.cs341; no behavior/test bytes changed for that annotation and no extra test cycle attributed to it. Final55hashes0mismatches and fidelity710exit0 confirmed after annotation. Final scoped candidate PASS, whole pilot IN PROGRESS, with record-wide recommendations and genuine remaining dependencies stated at the top. Manager owns commit/full gate/Claude/hardware/settlement.


## Full-gate triage B — actual owner paths (2026-10-26, no new test run)

The failed gate is retained as 6312 PASS / 42 FAIL / 0 skipped, 6354 total, exit1, 7m25s. The earlier scoped candidate PASS remains revoked pending these affected corrections. Read-only code/source triage does not turn a proposed correction into verified code.

### Mood phase and NV callback evidence

After known.py 67B5D4, the entire normal native Mood.Update body 0067B5D4..0067B6B0 was read: a too-small timestep logs at 67B644 and rejoins 67B670 (including 64A/66A paths), not an early return. Captured now is stored at 67B67E; all nine emotions update at 67B682..6A0; SendEmotionsToGame is unconditional at 67B6A4. Thus Advance(now) followed by SendEmotionsToGame is correct for an admitted normal update, including minimum-dt input. This does not authorize bypassing Robot's earlier calibration/drain gates. Raw61 is a real output, not logging noise.

M3_031_W2's callback contains Assert.Equal(publications+1, NativeUiDeliveryCount) before order.Add("callback"). A real intervening Mood61 can break that global counter assertion; handler isolation then hides the callback's later test marker, yielding the observed recvd/executing/broadcast list. Proposed correction must observe the actual NV publication/tag/payload and preserve enqueue-before-callback, callback-before-backup-clear, and deferred host delivery. Do not infer a missing production callback from this marker alone, or loosen a global count arbitrarily.

### Canonical tracker — genuine missing producer

Current M15-015, "Freeplay active-time tracker and its four pause sources", IMPLEMENTATION_GAP, evidence FreeplayDataTracker ctor0056EBD4, SendData0056EC48, SetFreeplayPauseFlag0056EEBC, BehaviorManager.SetCurrentActivity005A106C, CheckAndUpdateTreadsState005121F4, SetOnChargerPlatform00511DB0, HighLevelActivity.cs. Current M15-027, "Disconnect persistence, ForceUpdate and AI/progression/inventory destruction", IMPLEMENTATION_GAP, evidence00695908..4A,0056EEB8,0056EC48..EDE2,00569CB2..9DAC,005111BE..EA,00511314..38 plus adopted OV25-27.

Reused checked 20261021-m15-tracker-owner-join.md and personally reread after known.py 51111A: 00511114 loads Robot+264, 00511118 loads AI+2C, 0051111A calls ForceUpdate, then 00511120 AbortAll. AI update00569F44/46 updates tracker after Helpers. The new actual NativeBehaviorRuntime constructs Tracker but supplies no engine SourceLog, no actual tick call, no Lifetime(-2) ForceUpdate binding or AI+2C storage retirement binding; pause routing likewise must address actual owner. This is a genuine observable production omission, not merely the old standalone stack's failed assertion. Required correction: canonical tracker/clock/source log/pause routes, update after Helpers, unconditional destructor force before AbortAll, storage-only child retirement; standalone handles may not take ownership. Required test must use actual runtime.Tracker and actual removal, retaining exact order and no force from host handle disposal. No arithmetic recovery is requested.

### Actual Mood lifetime fixtures

Current M7-023, "Mood destruction with already-null ActionList dependency", IMPLEMENTATION_GAP, evidence0067AE14..74,00511150,005111B6; adopted OV34-35 gates0067AE18..30, children0067AE34..74, subscription retirement004EF184..1CE/00664EB8/0066365C/00663696/0067C228, owner null after delete005111C4.

M7LifetimeCheckedRows's three failing fixtures try to replace an already constructed canonical MoodOwner using a new MoodState or standalone Freeplay.Create. BindNativeLifetime correctly rejects that replacement. Tests must use actual native.MoodOwner, its real watcher ID and retained C3 subscription, and preserve all child order/null gates. Null-Robot leaf can detach that real owner and verify its actual nonzero ID survives without unregister; an arbitrary17 must not be injected through a refused binding. Resource/event fixture setup must occur before actual native construction if required. No source replacement guard should be relaxed.

M5X13 shutdown's new C3 unlink is the actual canonical Mood subscription. It should be checked at Mood's native retirement position, not removed from the oracle as general noise. Abort/join/Hub barriers and their finally cleanup must remain.

### Device callback lifetime — genuine regression

Current M4-027, "Sensor/light/movement/CubeAccel teardown and retained host reset candidate", EXACT_SOURCE, evidence005111EA..0051121A,005113E0..3F6,00511428..488,00511500..510 plus adopted T1-T9/S2. The current new canonical ManipulationSystem owns five device delegates: Motion.IsCarryingObject / PlaceObjectOnGroundAsync, Sensors.UnattachCarriedObjectIfCarrying, Cubes.ExcludeFromMovedBroadcast, Lights.Cubes.IsCarried. Existing device ResetToConstructed retains those hooks; current owned Dispose does not detach them. Therefore real RemoveRobot leaves references to retired Docking/Carrying. Do not alter the reset oracle to ignore this. Store exact installed delegates and clear only those still owned at actual service retirement; borrowed facade disposal must preserve canonical hooks and replacement/user hooks. Equivalent world delegates require the same bounded ownership review.

### Other oracle dispositions (proposed, not tested here)

- M15Restoration's ForceSet C6 test observes custom standalone Needs while the real subscription routes Engine.Needs. Explicit existing offline UseNeedsForOfflineTest(needs) before constructing the handle admits the intended actual owner and preserves deferred app event semantics. Keep info/mutate/send/retirement assertions; do not restore implicit engine replacement.
- M5 G1's obsolete missing-SimpleMood log: actual LoadFrom installs the canonical Mood closure. Assert raw Default2, actual group draw/shared RNG, and retain explicit CurrentMood override.
- M5X20 obsolete concrete Trigger gap: assert actual queued type26 action and 5A variant1 NO_ANIM_NAME failure, retaining repeated-init dedup. Standalone trigger-map fixture must prove custom context/API output while canonical map and Needs delegate remain unchanged before/during/after disposal.
- BehaviorObjective success uses actual canonical Mood after Start. Keep objective-before-mood, full result and raw payload; assert the real owner and no false missing-attachment diagnostic.
- Factory has six registered constructors. Pure empty BehaviorFactoryContext skips DriveOff for missing Manipulation, but constructs typed Look unconditionally. The pure-container count20/NotCreated158 is a typed-object count, not whole Look support or a NativeRobot-only condition. Retain FistBump parameters and assert the actual new entry type.

### Camera calibration fixture

Current M3-005 "At most 3 completed images per event time go on to vision; one EncodedImage at a time", EXACT_SOURCE, evidence00535B0E/00535B28..52 and00652B1C..006530C8. M3-022 "The connection-time CameraCalib read and its callback: the Failed/SizeMismatch/Recvd logs, the distortion zeroing for body hardware <= 6, the +0x48 store", EXACT_SOURCE, evidence006583E2..FA;0065AB68;0065ADE0/0065AE80;0065175E..66.

CozmoRobot's actual HasCameraCalibration reads CameraSettings.Calibration. M3M4ReviewFix.Live only assigns Vision.Calibration; those are distinct preconditions. Correct live fixture should complete the real connection NV tag80000001 callback and assert installed calibration identity, using the existing56-byte recipe (five F32 intrinsics, rows/columns U16, eight F32 distortion). No fake drain/status delegate or timeout increase; before-any-raw-state case must not accidentally inject a raw state through a convenience queue helper.

### Six-correction preliminary affected review

Builder's native-force position (after AttachToLiftChecked/before confirmer), exact facade Log relay detach, and owned-only Docking.Configurations assignment are correct. Face's synchronous pre-Straight independent Main/face/Forward capture and Flip's native relative pose/ordered-f32 norm are independent wire oracles; neither certifies physical stopping. DesiredFaceDistortion now observes canonical component/cache and real Glitch output. One remaining diagnostic finding sent to builder: null-conditional world.NativeConfigurations?.ForceUpdate silently drops the required native force write for absent owner; add visible availability diagnostic rather than fabricated result/host fallback, unless all admitted paths prove owner presence. Later confirmer-failure coverage is static source order only; no unsupported failure seam was added.

No source/test/manifest/status/Claude/REVIEW files were edited by this verifier. Actual model/reasoning telemetry remains unobservable. Whole pilot remains unfinished (audio/ROI/other activity/selected Look and Face caller gaps remain explicit).


### Additional actual-owner fixture release and D1 boundary

Independent code confirmation: EngineRobot.Update selects NativeBehaviorRuntime.UpdateAI at CozmoEngine1311; only absent runtime uses AIComponentUpdate. Thus M8 helper tests must register on actual runtime.AI.Helpers, retaining existing two-call/gate/order assertions; substituting a new AI cannot test the production path. Current M8-011 title "The Smart* scope helpers: idle animation, motion profile, track locks, custom light patterns, helper delegation and the reaction locks", IMPLEMENTATION_GAP, evidence includes SmartDelegateToHelper005BEB10→[Robot+264]+10 at005BEC2C, helper0056DAD8..DB37 and ctor00569AAE/B2. Native AI update/helper joins were already source checked in the pilot packet; no new helper algorithm is proposed.

CozmoRobot constructs and assigns actual BehaviorManagerOwner, initializes that manager's reaction map and attaches Spark once; standalone Freeplay.Create intentionally constructs a different manager. Actual lifetime fixtures must use CreateNative; late-resource empty-map fixture calls the existing InitializeRobotReactions on the same real manager and preserves no deferred lock replay. Current M7-014 "Reaction-lock manager lifetime and concrete per-class lock tables", IMPLEMENTATION_GAP, evidence005BCE3C/005A27E8/005A3A48 and 13 concrete tables. This does not certify all reaction classes.

Current M15-057 "BehaviorManagerMessage handler (G2E 0x96): ActivateSpark, SparkUnlocked, DoATrickRequest (RequestATrick, cost 3, AddInventoryAmount), PlayAGameRequest, bad-tag error", IMPLEMENTATION_GAP, evidence subscription005A15AC..CE, adapter005A5136..4A, dispatch005A3C70..78, case0 005A3C80..9A, case1 005A3DB4..C6, case2 005A3CA0..D5A, case3 005A3DCA..D6. The real 96 subscription updates the concrete owning canonical manager's full-width RequestedSparkId; the fixture must inspect that owner instead of standalone85. Do not add a duplicate subscriber, reroute app input, truncate the ID, loosen cancel latch or manufacture acknowledgement.

D1 test migration approved within checked behavior: public ForceUpdate/Update controls the isolated extension and cannot be used to provoke native notify. Existing M13 pending/audio fixtures must target actual World.NativeConfigurations and robot.BlockConfigurationForceUpdate; a direct actual native Update is explicitly a unit substitute for frame-tail scheduling. Retain pending/root-cache/movement thresholds/height-before-reset/A→B/reset/unchanged behavior. Read-only cache introspection is acceptable, copied helper algorithms or fabricated containers are not. M11 facade dirty-flag test must become explicit host/native isolation before/after delocalization and retain actual R1 retirement. No public host setter alias may be restored.

Tracker design refinement agrees with source: capture actual AI and Tracker before manager+44 clears the runtime field; later+264 destroys captured AI in existing direct slot order, with +30 severe cleanup and +2C storage-only retirement. Runtime.Dispose detaches owned sensor routes, not ForceUpdate/storage. Native Spark pause writes belong to the specialized Freeplay switch path005AE4E2..F2, not a generic app input or observer. Since that activity is visibly rejected before admission, do not invent Spark=false or attach standalone input to native state; this residual prevents whole M15-015 settlement. Existing standalone Spark callback remains local.


### Exact shutdown / NV enqueue oracles and approved D2 design release

M5X13 full-gate log2363..2370 differs at position10 only: expected secondA3 thenEB, actual secondA3 thenC3 thenEB. Actual Robot caller releases managers+44/+48 before ActionList and Mood+440; Mood destructor005111B6 retires C3 through0067AE34/004EF184..1CE before BlockFilter+44C0051122A. Add the single C3 to that exact ordered expectation, rather than filtering it as noise. Preserve all abort/join/loader/hub assertions and finally barrier release/join. This failure is an obsolete absent-owner fixture expectation; it is not evidence of a loader shutdown regression.

W2 replacement oracle needs no new production observer: subscribe an early NVStorageOpResultBroadcast observer before erase, then in completion callback record facts (no asserts), remove early and attach late observer. QueueAppBroadcast captures its subscriber set at the actual NV enqueue. Deferred delivery must invoke exactly the early observer once with the matching NV payload, never the late observer; reversed enqueue/callback ordering would capture the late observer and fail. Assert callback result, no host delivery yet, backup still pending, exact recvd/executing/callback/broadcast order and backup cleared afterward OUTSIDE the isolated callback. Restore subscriptions in finally. This proves the NV report's own enqueue-before-callback independent of intervening Mood61/global UI publication counts and prevents swallowed test assertions masquerading as lost callbacks.

Operator D2 approved a controlled-clock standalone extension. Independent concrete design PASS, not code PASS: canonical owner, borrowed native view, and explicit CreateStandalone(robot,vision,clock) are distinct modes. Standalone owns a private AIWhiteboard with closure()=>ClockSec(), while borrowing actual Docking/Carrying and canonical Paths/Follower. Split Whiteboard ownership from native/path-service ownership. Standalone must not install World/vision/device source callbacks, overwrite Docking.Configurations, subscribe native whiteboard tags, change native clock/force/state, or dispose shared services. In particular constructing PathSender replaces Motion.Path.SendPathMessage and PathFollower subscribes MessageInternal, so manual mode must borrow both instead of silently creating duplicate producers. It disposes its private whiteboard state and own tracked Log relay only. Borrowed native view ClockSec setter must reject clearly/direct caller to standalone, never ignore or mutate actual engine time. Actual internal owner retains engine clock. Existing R2 500/1000/1/45-second/caps oracles remain unchanged; explicit standalone fixture is the appropriate clock owner. Final construction/disposal/output verification is still pending implementation.

Current manifest reread for the preceding two oracle claims (literal):

```json
[
  {
    "id": "M3-031",
    "title": "NV read retry (7 resends / 8 transmissions, identical resend) and the 5 s timeout (-4, no retry)",
    "status": "EXACT_SOURCE",
    "evidence": [
      "retry set {-8,-7,-5,-4} (0x6431E6..0x6431FA); ResendLastCommand 0x645C6A..0x645D7A: the counter +0xF4 is 0-based (reset by the send/arm at 0x645484), incremented then compared < +0xF5 = 8 (bhs at 0x645C7C), so 7 resends / 8 transmissions, then ReadOpFailed (0x6431FE..0x643234); the second caller is the write/erase path 0x643194..0x6431A2",
      "timeout state 2: +0x78 set and robot+0x2C > +0x74 -> Update.ReadTimeout, cb(nullptr, 0, -4), SetState(0), no retry (0x64575A..0x6457C0)"
    ]
  },
  {
    "id": "M5-055",
    "title": "~CozmoEngine member destruction order (RobotDataLoader abort flag, robot and streamer teardown, weak-signal unlinks)",
    "status": "EXACT_SOURCE",
    "evidence": [
      "re-analysis/research/20261012-m5-settling-2.md: ~RobotDataLoader sets +0x278 with dmb before joining the load thread (0x0051F0D4..0x0051F0E6); the weak-signal unlinks in the streamer and handler teardown (M5-044, M5-053) depend on whether UiMessageHandler is still alive; the ~CozmoEngine member order is not read.",
      "re-analysis/research/20261012-m5-teardown-reads.md item 1 (Opus-checked 14/14): ~CozmoEngine 0x004EC68C..0x004EC722 (+0x64, vectors +0x50/+0x40, +0x3C, +0x38, +0x34 CozmoContext, +0x30 UiMessageHandler, Json +0x18, vector +4) and wrapper 0x004EC79C; ~CozmoContext 0x004EAAAC..0x004EAC14 calls RobotManager::RemoveRobots first (0x004EAABE -> 0x0052F428..0x0052F472); ~Robot 0x00511512..0x0051154A; ~AnimationStreamer 0x0057AF48..0x0057AFDA (+0xC base subscription, tag 0xB4, released last); ~RobotDataLoader 0x0051F0C6..0x0051F0EA (runs after RobotManager); ~UiMessageHandler 0x0066011C..0x00660192; signal destructors 0x006634B8 (E2G) and 0x00663620 (G2E). So the UiMessageHandler and both signal maps are alive when the Robots release their subscriptions."
    ]
  }
]
```


D2/D1 clarification supersedes the overbroad “no World callbacks” phrase above: the two existing fields SetObjectPoseChanged and BlockConfigurationManagerUpdate are HOST-ONLY slots after native-first calls (BlockWorld2449→2450,1611→1612). Explicit standalone retains registration of its existing private public configuration adapter there, preserving ConfigurationSeen/FirstSeen frame updates on its controlled clock. Assignment retains existing single-slot/last-registration behavior; cleanup clears only if that slot still equals its own installed delegate, preserving later replacements. Do not introduce multicast fanout or a previous-callback restoration stack (which could reactivate disposed handles). Native MapClearQuad/UnsetCarrying/CarriedPrimary and device/AI/vision hooks remain forbidden to manual mode. No manual-to-native flag/clock/cache writes. This clarification is an independent read PASS based on the actual host/native split, not a new source algorithm or feature exclusion.


### Owner-2 follow-up read findings (87-case builder run: 81 PASS / 6 FAIL)

No checker test was running. Actual-owned PathSender constructor assigns Motion.Path.SendPathMessage=Send. Actual-owned path retirement must detach only its exact installed delegate if still current; borrowed/manual facades must neither construct a replacement producer nor detach the canonical one. Preserve later user/replacement delegates, no restoration stack.

Camera fixture's RobotValidated=true occurs in OnSuccess before manufacturing info. Current HandleMfgId then emits SendConnectionResponse(Success), which invokes VisionConnected and Camera.OnRobotConnected's real NV read. The fixture supplied FirmwareVersion only, so no actual Success edge/camera request existed. Complete received RobotAvailable and ManufacturingID metadata before SyncTimeAck/fullstate; inspect actual Success/time-sync/firstfull/LastFullStateResult, then advance real NV queue. No unsolicited callback/request or fake drain input is allowed.

After known.py DrivingAnimationHandler::HandleActionCompleted, personally reread entire normal004F0D78..004F0E2E. Matching current LoopId004F0D8E/90 considers repeat/HasPathToFollow004F0D94..DA4 then result004F0DA6/A8. A nonzero loop result reaches optional unlock and PlayEndAnim004F0DC2. EndId match sets state4 at004F0DCE/DD4. Therefore the new M5X20 fixture's three explicit loops perInit produce one automatic end for the latest current LoopId, twice across twoInit calls. Assert exact six loopIDs plus two EndIDs in per-pass order, their concrete type26/result0300000C/variant1/emptyname, total8; do not ignore unknown extras or loosen the total. Production handler change is not warranted.

M8 gate cause still under focused diagnosis: IHelper.UpdateWhileActive has no parent IsRunning/Context gate, and its parent field affects Stop/IsActing only. Starting a Probe mirrors the fixture but is not proven to explain zero ticks. CreateOffline explicitly sets FirstFullStateHandled=true. Empty real Vision drain returnsfalse; installedhook alone likewise does not explain zero ticks. Missing-hook subcase must explicitly sethooknull to grade that intended absence. Preserve1/2/3 and actual canonical helper; inspect gate/stack/log state rather than restoring fallback.

Current literal records reread before these claims:

```json
[
  {
    "id": "M1-028",
    "title": "Initial-connection handshake outcomes",
    "status": "EXACT_SOURCE",
    "evidence": [
      "B29 RobotInitialConnection subscribes to factoryFirmwareVersion 0xD2 (0x0052D1B4), firmwareVersion 0xEE (0x0052D208), robotAvailable 0xC9 (0x0052D260); factoryFirmwareVersion -> 3; firmwareVersion: parse fail or FACTORY build -> 3, no robotAvailable yet -> 1, simulator -> 0 (0x0052D3BE, 0x0052D59E, 0x0052D4F2, 0x0052D7E4); OnNotified 3 -> reason 7, 4 -> reason 8, then SendConnectionResponse (0x0052DDAA..0x0052DDC4, 0x0052DE06); success: filter off (+0x2D), GetManufacturingInfo, then on mfgId 0xED store serial, HW version, colour, $session_id and send RobotConnectionResponse(Success) (0x0052DE6C, 0x0052DE7C, 0x0052E304..0x0052E3A2)",
      "CB7..CB21: RIC state and subscriptions (0x0052D184..0x0052D28E); robotAvailable sets +0x2E (0x0052DC64); OnNotified outcomes (0x0052DDAA..0x0052DDFC); on Success +0x2D is set first, mfgId is subscribed, then GetManufacturingInfo is sent reliable with no timer or retry (0x0052DDD0..0x0052DE7C); the mfgId lambda stores serial, hw and colour (colour only if in {0,2,3,4}), sets $session_id and sends RobotConnectionResponse(Success) (0x0052E304..0x0052E3B2); SendConnectionResponse sets +0x10, releases the handles and broadcasts the 14-byte response, engine subscribers running synchronously (0x0052DF2E..0x0052DF64; 0x006625C6..0x006625FA)",
      "CB14: with no robotAvailable, SendConnectionResponse(1, 0) is called directly, setting no disconnect reason (0x0052D7B8..0x0052D850)",
      "CB31/CB34: there are no handshake timers or retries, and results 1, 3 and 4 keep the link and the Robot (0x0052DD98..0x0052DF80)",
      "CD15/CD16: GetManufacturingInfo follows an accepted firmwareVersion; after the response, the mfgId handler also queues ReadLabAssignmentsFromRobot and ConnectRobotToNeedsManager (0x0052E3AA, 0x0052E3B2)",
      "E1..E6: a second firmwareVersion before mfgId subscribes a second mfgId lambda and sends a second GetManufacturingInfo, but the first mfgId yields exactly one RobotConnectionResponse, because SendConnectionResponse unlinks both lambdas during the emit; a second mfgId reaches only the Robot's HandleRobotSetBodyID (0x0052DF2E..0x0052DFC8; emit 0x0069E27C..0x0069E2B2; unlink 0x0051D338..0x0051D3A4; 0x00532CD0..0x00532CDA)"
    ]
  },
  {
    "id": "M5-063",
    "title": "DrivingAnimationHandler bodies: HandleActionCompleted (E2G 0x5A), PushDrivingAnimations (G2E 0xB5), RemoveDrivingAnimations (G2E 0xB6)",
    "status": "EXACT_SOURCE",
    "evidence": [
      "re-analysis/research/20261012-m5-teardown-members.md: callbacks 0x004F110E -> HandleActionCompleted 0x004F0D78 (engine sender: ActionQueue 0x0053FAAA); 0x004F1186 -> PushDrivingAnimations 0x004F0944; 0x004F120E -> RemoveDrivingAnimations 0x004F0A94; registered only if HasExternalInterface() == 1 (0x004F072C..0x004F0732).",
      "re-analysis/research/20261012-m5-driving-handler-read.md: handler state ctor 0x004F06B0..0x004F0878 / Init 0x004F0FC4..0x004F0FF4; Push gate warning 0x004F094C..0x004F0964 and push_back 0x004F0988..0x004F09FA; Remove gate 0x004F0A9E..0x004F0AB4, empty-stack warning 0x004F0ADA..0x004F0C22 (0xBE4672), first-match erase 0x004F0AE4..0x004F0C6C, LoopBoundOverflow drivingAnimationHandler.cpp:108 0x004F0B5A..0x004F0BB6; UpdateCurrDrivingAnimations only from Init (0x004F0D2C..0x004F0D74: back(), else Sad 0x2C, else needs-met 0x38, else default 0x20); HandleActionCompleted 0x004F0D78..0x004F0E2A with PlayEndAnim 0x004F0EFA..0x004F0F4A, PlayDrivingLoopAnim 0x004F0E58..0x004F0EA0, PlayDrivingEndAnim 0x004F106C..0x004F10C0, lock/unlock helpers 0x004F0EB2..0x004F0EE2 / 0x004F0F4C; ActionIsBeingDestroyed 0x004F0F94..0x004F0FC0."
    ]
  }
]
```


### Latest resource and private-owner checks (candidate not yet PASS)

NativeRuntime ctor's only persistent direct Vision resource write is OverheadEdges ??= new OverheadEdgesDetector(). Retirement must track the instance only when this runtime created it and clear only if the current property is still that instance; preexisting and later replacement detectors remain. Detector is managed, not IDisposable. ApplyOverhead's map.OverheadContext is temporary and already restored in finally. Owned manipulation's persistent callbacks are covered by the separately tracked exact-identity cleanup. This is narrow host resource retirement, not a new native allocator claim.

D2 standalone's private whiteboard owns copied FailureInfo.Pose values (SetFailedToUse calls DeepCopy). ClearAllBeacons alone leaves failure memory and these wrappers retained; current OnRobotDelocalized clears maps without disposing them. A narrow internal RetireStandaloneStorage may dispose current stored failure wrappers under the failure gate, clear tables and beacons, called only from explicit standalone Dispose. Do not rewrite canonical whiteboard destruction, delocalization or cap eviction in this correction. Preserve caller input poses and native whiteboard history; retained private whiteboard must expose empty owned storage afterward.

Static production diff reviewed against released contracts: tracker after Helpers and actual -2 force/captured +264 AI retirement; exact owned sensor/device/path/host-slot cleanup; standalone private clock/whiteboard and borrowed canonical services; source native force after carrying write/before confirmer plus visible absent-owner diagnostic; read-only native root-cache introspection. No further production blocker found in that reviewed diff before the requested overhead/private-whiteboard correction.

Test coverage finding sent manager/builder: M5X20 StandaloneCustomTriggerInputNeverReplacesCanonicalDrivingInput still uses an empty replacement map and only compares identities/GroupFor to itself. It does not satisfy the explicitly requested preservation proof through actual manual trigger execution. Required test-only extension: a distinct nonempty custom map to an authored clip, real PlayAnimBehavior started with standalone.Context, assert resolved clip/trace plus actual scheduler stream/ticket, while canonical driving-map/needs delegate identity stays unchanged before/during/after. Static current RunTriggerAction already resolves Context.Triggers; this is a missing regression proof, not an asserted production defect.
