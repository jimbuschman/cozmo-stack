# M1 idle-timeout sleep: source check and integration handoff

| Coverage | Result |
| --- | --- |
| M1-031 / M1-045 | Current titles/status/evidence read; timer handlers, Update and sleep factory read in original instructions |
| Sleep action tree | Child order, constructor arguments and outer queue call recovered |
| Lift preset | Original embedded table read: preset 0 returns float32 32.0 (0x42000000) |
| Trigger names | Verified against shipped Unity AnimationTrigger enum |
| Action/animation runtime | Existing B-ACTIONS extraction is a dependency; not independently verified end-to-end here |
| Changes | Research artifacts only; no production edits, tests, status changes or commit |

Answers the operator's 2026-10-05 "Anything else you can do?" while M1-029 is assigned elsewhere. Adds a bounded handoff to 20260930-B-CORE-b4-extraction.md; does not approve an inventory or settle a record.

## Primary evidence

APK libcozmoEngine.so SHA256 02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1. Thumb disassembly from ELF segments; PLT targets resolved from relocations. Transcript: 20261005-m1-sleep-instructions.txt. Unity enum: unity/scripts/csharp/Anki.Cozmo/AnimationTrigger.cs:215..218 (zero-based enum values counted from first member).

## Current records

M1-031, "Idle-timeout disconnect", IMPLEMENTATION_GAP. Current evidence: StartIdleTimeout 0x0052D030..0x0052D066; Cancel 0x0052D06C; expiry 0x0052CE6E..0x0052CE98; CC1..CC7; factory CC8; engine-tick gate CC10. Unresolved explicitly names unbuilt M1-045.

M1-045, "The idle-timeout go-to-sleep action: CreateGoToSleepAnimSequence queued on the ActionList", IMPLEMENTATION_GAP. Current evidence: queue call 0x0052CE54..0x0052CE6A, factory 0x0052CEA2..0x0052CFC0, caller-scan report. Unresolved: action tree named, not built; tests exercise M1-031, never the action.

## Source inventory for this bounded path

RECOVERED below applies to the stated local fact only. Missing nested behavior remains recoverable; no whole-path source-exact claim is made.

| Step | Original behavior | Citation | Record / result |
| --- | --- | --- | --- |
| S01 | Start reads BaseStationTimer current seconds and adds durations with float32 arithmetic. Face deadline requires robot+0x34E nonzero. | 0x52CFE4..0x52CFF6; vadd.f32 0x52D00E / 0x52D048 | M1-031; recovered |
| S02 | Negative duration rejected via BLT after VCMPE.F32 against zero. Unordered (NaN) does NOT take BLT. Unarmed deadline (-1 exactly) stores resulting candidate; otherwise stores only under MI (candidate below old deadline). | face 0x52CFFC..0x52D02C; disconnect 0x52D034..0x52D066 | M1-031; recovered; differs from C# >=0 gate for NaN |
| S03 | Cancel stores -1 bits 0xBF800000 to both deadlines | 0x52D06C..0x52D072 strd | M1-031; recovered |
| S04 | Update queues sleep when face deadline >0 and <=now; sets deadline to zero BEFORE factory/queue. Then independently tests disconnect, sets it zero and calls the handler disconnect tail. | 0x52CE3C..0x52CE98; queue 0x52CE6A | M1-031/045; recovered local order. Disconnect tail at 0x8CB26C not reread here |
| S05 | Factory creates a CompoundActionSequential with three TriggerAnimationAction children in order 0xD2,0xD5,0xD4 | 0x52CEB4; 0x52CED8/CF0E/CF44; child additions 0x52CEEC/CF22/CF58 | M1-045; recovered constructor/add calls |
| S06 | Each trigger ctor gets (robot, trigger, 1, true, 0, float bits 0x42700000=60, false) | first 0x52CECA..0x52CEDE; repeated 0x52CF02..14 / 0x52CF38..4A | M1-045; recovered raw args; do not relabel 60 as a fixed delay |
| S07 | Trigger constructor forwards relevant args to PlayAnimationAction, stores trigger at +0xB8, final bool at +0xC8 and calls SetAnimGroupFromTrigger | 0x54424C..264; stores 0x544280/29E; call 0x5442A6 | M1-045/M5; forwarding recovered. PlayAnimationAction body and trigger selection/runtime still dependency |
| S08 | Outer CompoundActionParallel receives sequential child FIRST, MoveLiftToHeightAction(preset=0, float=5.0) SECOND | 0x52CF6C; sequential addition 0x52CF80; lift ctor 0x52CF9A..CFA2; lift addition 0x52CFB0 | M1-045; recovered. Sequential-animation-only implementation omits parallel lift |
| S09 | Lift preset overload resolves GetPresetHeight(0), forwards height and float=5.0 plus zero third float to float ctor | 0x548B84..0x548BA2 | M1-045/M4; recovered raw forwarding; float constructor runtime not reread |
| S10 | Preset lookup table returns 32.0 for key 0; other entries 76.0,92.0,-1.0 for keys 1,2,3 | 0x548C36..56; literal 0x548CB0=0x70BA40 plus PC 0x548C44 -> table 0x00C54684; key0 bits 0x42000000; lookup 0x548C80 and load 0x548C84 | M1-045/M4; source table recovered. Do not substitute an arbitrary bottom-lift angle |
| S11 | Factory returns OUTER parallel action. Update queues it on robot+0x250 with position=0, u8=0 | return 0x52CFBA; 0x52CE5A..6A | M1-045; recovered call args; QueueAction semantics are B-ACTIONS dependency |
| S12 | Trigger names: D2 GoToSleepGetIn, D5 GoToSleepSleeping, D4 GoToSleepOff; D3 GoToSleepGetOut is absent from this factory | shipped Unity AnimationTrigger.cs:215..218 | M1-045/M5; verified enum mapping; selected animation clips still depend on shipped animation-group machinery |

## Concrete integration omission

Current re-analysis/jobs/B-ACTIONS.md builds the queue, lifecycle, compounds and completion. Its named factory replacements are FlipBlockAction, ChargerActions and DockActions. Its record list omits M1-031/M1-045; it does not name QueueGoToSleep or CreateGoToSleepAnimSequence.

Therefore finishing that job as currently written does NOT itself guarantee M1's sleep path is wired. A later manager scope must include the timer -> factory -> queue -> action -> animation/lift path explicitly, based on checked inventory rows. This is a scope finding, not authorization to edit the job.

CozmoEngine.cs:2069 still implements QueueGoToSleep as MISSING log + GoToSleepRequested event. EngineAppLayerTests.cs:1414..1421 validates the event happens before disconnect and expects the MISSING log. This checks timer ordering, not the action tree or its runtime, and cannot settle M1-045.

## Newly checked mismatch: NaN duration

Current IdleTimeoutComponent.Start (CozmoEngine.cs:746) uses duration >= 0 for both durations. In native S02, VCMPE unordered yields N=0,Z=0,C=1,V=0, so BLT (N!=V) is false. An unarmed -1 deadline becomes NaN; C# leaves it -1. Later a finite Start can arm the C# deadline but native's MI comparison against NaN will not replace it without Cancel. This is observable later timer behavior, not merely different storage.

Original Update's first BLE is not taken for unordered, but its second BHI is taken, so NaN deadline itself does not fire. No shipped caller supplying NaN has been established. The manager should record the scope/reachability accurately; do not silently discard this source difference or expand the change beyond the approved inventory.

## Evidence-derived checks for the later implementation

- Exact tree structure, child insertion order, arguments, trigger identities and preset table bits.
- Sleep queue happens before disconnect when both deadlines expire; do NOT assert clips must finish before disconnect. Source has independent deadline checks, not a completion wait.
- Deadline is zeroed before queue call; no automatic retry after queue refusal is visible in Update. Runtime failure/ownership semantics must come from QueueAction and compounds, not an invented retry.
- Zero deadline cannot rearm from a positive candidate without Cancel; shorter future deadline wins, later loses; equality does not replace.
- NaN Start with unarmed vs already-armed deadline; finite Start after NaN; Cancel restores rearming. Establish ordinary zero/negative/infinite inputs too from instruction predicates.
- End-to-end action completion, track locks, lift failure and animation selection need the remaining runtime dependencies before their test oracle is specified.

## Remaining source work / completion limit

QueueAction, child AddAction and compound semantics: use the existing 20261004-actionlist-extraction.md only after manager verification. TriggerAnimationAction Init/SetAnimGroupFromTrigger, PlayAnimationAction Init/completion, MoveLiftToHeightAction float ctor/Init/completion, actual animation groups and runtime callback ordering need end-to-end evidence. Local factory knowledge does not settle that path. NaN inputs from shipped callers are UNKNOWN; no caller scan performed here.

No production code, inventories, manifest, jobs or project state changed. No regression/hardware tests run. No commit under the research-lane rules. Report ready for manager citation checking and bounded inventory integration; M1-031 and M1-045 remain open.
