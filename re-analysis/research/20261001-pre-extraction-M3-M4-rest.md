# Pre-extraction: remaining M3/M4 implementation gaps (Q21)

Date: 2026-10-01  
Manifest: `re-analysis/fidelity_manifest.json` at `954c092`  
Primary source: `libcozmoEngine.so` 3.4.0-1204.

## Scope reduction

I compared every current M3-device/M4-control `IMPLEMENTATION_GAP` with
`jobs/B-CORE.md`, `jobs/B-CORE2.md`, all three B-CORE verifier reports, and
`20260930-bcore-extractions.md`.  The requested residual is exactly four
records, all in M4: **M4-009, M4-017, M4-018 and M4-019**.  There is no M3
record left in this request's scope.  In particular M3-001/010/013/018/021/032
were B-CORE verifier scope; M3-023/027/033/034 and M4-003 were covered by the
B-CORE extraction; and the remaining NV/readiness/motion records belong to the
two build jobs.

The rows below identify the production C# entry but do not change it.  An
`UNKNOWN` is retained where the binary rows still do not establish a
cross-layer input.

## M4-009 — cube tracking and motion events

> **Current manifest text.** Status `IMPLEMENTATION_GAP`. Title: “Cube
> tracking: ObjectAvailable / ObjectConnectionState, Moved / Stopped /
> UpAxisChanged handling with the charger and carry filters, per-object
> IsMoving.” Authority/evidence: `libcozmoEngine.so` 3.4.0-1204,
> `S1,S2,CD10a..CD10c,LC8a..LC8e`. Unresolved: “wire the
> `[robot+0x280]+0xC` dock-target exclusion (C11.1) into the Moved/Stopped
> broadcasts. The slot <= 4 bound is enforced on the connection path but not
> in Cubes.Handle, which uses the slot as the stack's BlockWorld ObjectID;
> separating ObjectID from slot is M11 work (LC8e).” Location:
> `Cozmo.Robot/Cubes.cs`.

**C# production entry.** Robot message dispatch reaches `CozmoCubes.Handle`
(`Cubes.cs:522-581`) and the vision/world consumer (`VisionSystem.cs:591-620`).
The dock target is owned by `DockingSystem.DockTargetObjectId`
(`Manipulation/Docking.cs:151,360-364`) and exposed to the whole manipulation
stack at `ManipulationSystem.cs:54-57`.  This is not the wire slot.

| step | address | what the engine does | gates / order | failure result | float bits |
|---|---|---|---|---|---|
| Receive connection state | 0x00533B56..0x00533CB2 | Reads robot `objectID` as the active slot. Slot `>4` returns. Connected calls `AddConnectedActiveObject(slot,factory,type)` then `Robot::HandleConnectedToObject`; disconnected removes then handles disconnect. | Bound precedes every lookup/mutation/broadcast. Connected order is add → robot handler → game broadcast. | Bad slot: silent return. Add failure/error branches log and suppress the normal object result; full text of the two error branches is M11-042. | none |
| Convert slot identity to engine ObjectID | 0x00623040..0x00623A8C | Reuses a matching located object where possible or allocates/sets an ObjectID, then inserts the connected object. The game-side connection broadcast carries that engine ObjectID, not slot. | `activeID >=5` fails; same slot/factory/type returns existing id; replacement removes first. | `-1` on rejected create; exact `CreateActiveObjectByType` non-cube behavior remains **UNKNOWN (M11-042)**. | none |
| Moved lookup and filters | 0x00533E4C..0x005341BA | Maps message activeID to the connected object. Rejects charger id `robot+0x334`; then applies double-tap movement suppression. Sets active and located copies moving/dirty before deciding the game broadcast. | lookup → charger → double-tap → state/dirty updates → carry/dock broadcast exclusion. | Unknown id warns and returns; charger/double-tap log and return. | accel x/y/z are copied as three f32 words without arithmetic: exact incoming bits |
| Dock-target exclusion (Moved) | 0x0053416E/0x00534174 | Suppresses `ObjectMoved` when object ID equals `[[robot+0x280]+0xC]`. | Applied with the carried-object exclusion after internal IsMoving/dirty updates, so suppression affects only the game broadcast. | no broadcast; state changes remain | none |
| Stopped lookup and state | 0x00534636..0x00534AA4 | Same lookup and charger filter. Calls the double-tap predicate but discards its result, clears IsMoving/dirty copies, then applies carry/dock broadcast exclusion. | The discarded predicate must not suppress Stopped. | Unknown/charger return; suppressed broadcast leaves state cleared. | none |
| Dock-target value | 0x0063BA1E/0x0063BA2A; 0x0063BAAC/0x0063BAB6 | `DockingComponent+0xC` defaults to ObjectID `-1`; `DockWithObject` copies the target engine ObjectID into it. `AbortDocking` does not reset it. | Writer runs as docking begins, before motion reports. It is not the carried object (`CarryingComponent+8`). | stale last dock target persists after abort by source | none |
| Up-axis change | 0x00534D66..0x00534E1E | Maps active id, substitutes engine ObjectID, emits viz and broadcasts `ObjectUpAxisChanged`. Stores no per-object axis. | No charger/carry/dock filter. | Unknown id logs error and returns. | none |
| Robot-side production of events | outside app package | Firmware decides whether to forward Moved/Stopped/UpAxis/Accel. | **UNKNOWN / M4-024 HARDWARE_ONLY.** No app-side enable send was found for Moved/Stopped/UpAxis. | UNKNOWN | incoming words only |

**Implementation boundary.** M4 can wire `DockTargetObjectId` into both
broadcast exclusions now.  Slot-to-engine-ObjectID separation must use the
result of the connected-object/world mapping; it must not relabel a slot as a
BlockWorld id.  Creation/reuse details still owned by M11-042 stay explicit
rather than guessed.

## M4-017 — backpack lights and headlight LimitedExposure side effect

> **Current manifest text.** Status `IMPLEMENTATION_GAP`. Title: “Backpack
> lights: priority, Off resent every tick while no source, charging state
> machine, shared locator, wire conversion, headlight.” Evidence:
> `LB1..LB6,LB4a..LB4i`. Unresolved: “reproduce the EnableMode(14) call before
> the send: it queues LimitedExposure, applied at the next
> VisionSystem::Update (M11). No reader of mask bit 14 was found (E9); M11 owns
> that.” Location: `Cozmo.Robot/Lights.cs`.

**C# production entry.** `CozmoLights.SetHeadlight` is
`Lights.cs:361-372`. The lights component explicitly documents the missing M11
queue at lines 362-368; today it goes directly to the robot send.

| step | address | what the engine does | gates / order | failure result | float bits |
|---|---|---|---|---|---|
| Game entry | 0x00632456 → 0x00632344 | Game `SetHeadlight` tail-calls `BodyLightComponent::SetHeadlight(enable)`. | No other native caller found. | none | none |
| Queue vision mode first | 0x0063234A..0x00632364 | Calls `VisionComponent::EnableMode(14, enable)` on `robot+0x258`. | This call is strictly before constructing/sending SetHeadlight. Its result is ignored. | null VisionSystem logs `VisionComponent.EnableMode.NullVisionSystem`, sets debug-break byte and returns 1; headlight still proceeds | none |
| Cross component boundary | 0x006527AC..0x006527B6 | Non-null component tail-calls `VisionSystem::SetNextMode`, which appends `{mode=14,bool}` to its deque. | Queue now; no synchronous mask change. | allocation failure semantics **UNKNOWN**; normal return 0 | none |
| Apply queued mode | 0x006B4FB6..0x006B4FE4 | At the next processed image, `VisionSystem::Update` repeatedly applies front mode and pops it. | FIFO order; apply before pop. No image means it remains queued. | none exposed | none |
| Change mode mask | 0x006B1A76..0x006B1CC6 | Enable clears Idle bit 0 and sets bit 14; disable clears bit 14 and restores Idle if mask becomes zero. Already-in-state is a no-op. | logs only on an actual change | returns 0 | masks `0x00000001`, `0x00004000`; no floats |
| Send headlight | 0x00632368..0x00632380 (send call 0x0063237E) | Sends SetHeadlight `{bool enable}`, reliable=1, hot=0. | Always attempted after EnableMode, including its failure. | ordinary Robot::SendMessage result is not used here | none |
| Downstream meaning of LimitedExposure | whole-text read scan reported in E9 | No direct caller of `VisionComponent::IsModeEnabled`, no constant bit-14 read and no `ShouldProcessVisionMode(14)` were found. | The mask mutation is established; a behavioral consumer is not. | **UNKNOWN (M11 boundary).** Do not invent exposure behavior. | none |

## M4-018 — cube-light default selection and localization refresh

> **Current manifest text.** Status `IMPLEMENTATION_GAP`. Title: “Cube lights:
> WakeUp on connection and reconnect, layers and PlayLightAnim gates,
> SetObjectLights / SetLEDs, SetCubeGamma, CubeID, CubeLights, white balance,
> default-layer anims.” Evidence: `S5..S8,LC1..LC8,LC6`. Unresolved: “wire
> the robot-is-localized flag into the RobotDelocalized refresh gate (C11.2);
> the default-layer carried/visible choice needs M11. EnableGameLayerOnly
> matches C13.1 and C13.4; the single-object case with no ObjectInfo is not
> settled by the rows (unreachable from the app). SetLocalizedTo
> (0x0051245E/0x005124EE) and the runtime Robot::Delocalize callers are M11.”
> Location: `Cozmo.Robot/Lights.cs`.

**C# production entry.** Connected-object events enter the cube-light
component from `CozmoCubes`; periodic work is `CubeLightComponent.Update`
(`Lights.cs:643-714`). The required hooks exist at `Lights.cs:540-552` and are
wired in `CozmoRobot.cs:278-288`: carried/visible inputs, `IsLocalized`, and
`OnRobotDelocalized`.

| step | address | what the engine does | gates / order | failure result | float bits |
|---|---|---|---|---|---|
| Connection WakeUp | 0x00639CE4..0x00639D8A | Disconnected returns. Connected light cube `emplace`s ObjectInfo (no overwrite) then `PlayLightAnim(WakeUp=0x26, layer 2)`. | Existing info remains; reconnect still calls Play. | non-light/lookup failure: no light sequence | none |
| Finish WakeUp and choose default | 0x00637B04..0x00637C06; 0x00637D4C..0x00637E02 | Empty layer returns to layer 2 and, on `Update(true)`, chooses Sleep/SleepNoFade, else Carrying if engine ObjectID equals carried id, else Visible when located object's `+0x24==1`, else Connected. | Sleep flags first, then carried, visible, connected. | Missing located/carry input falls through to Connected; exact missing-object diagnostics not behavior-changing | none |
| Delocalized notification | 0x0063A391/0x0063A3A8 | Sets refresh-request byte `comp+0x21=1`. | Notification does not immediately repick. | none | none |
| Localized gate | 0x00637924..0x00637968 | Update tests request and `robot+0x2C4`. While zero it holds the request. On first update with nonzero, clears request and repicks every object's default. | test → clear → all-object repick in the same tick | no localized state means indefinite hold | none |
| Meaning/writers of +0x2C4 | ctor 0x0050FF10; clear 0x00510A4A; set 0x0051245E, 0x005124EE, 0x0051217C | It is the robot-localized flag: default 1, cleared by Delocalize, set by SetLocalizedTo and the treads transition. | It is independent of `IsOnTreads`; published state combines them later. | runtime SetLocalizedTo/Delocalize caller completeness remains **UNKNOWN (M11)** | none |
| EnableGameLayerOnly | 0x00639905..0x00639B36; C13.1/C13.4 | If already in requested state, return. Enable sets static lights, stops layers 1 and 2, marks game-only. Disable differs: all-objects stops only layer 0; single object stops layers 0 and 2, then repicks. | Preserve the all/single asymmetry and exact stop-before-repick order. | Single-object id with no ObjectInfo is unreachable from official app; binary branch result is **UNKNOWN** from current rows. | none |
| Cube wire conversion | 0x0063A654..0x0063A800 | White-balance G and B by 0.6 when R != 0, then SetCubeGamma if changed, CubeID, CubeLights. | gamma → id → lights, all reliable/not-hot | send failures do not roll back the cached gamma in the extracted path | `0.6f = 0x3F19999A`; converted channels truncate to integer; no other float on wire |

The already-wired `IsLocalized` hook is the correct M4-side seam; the M11
work is to make its state transitions source-complete.  Do not replace it with
“has a visible cube” or another inferred condition.

## M4-019 — cliff statistics, threshold schedule and frame gates

> **Current manifest text.** Status `IMPLEMENTATION_GAP`. Title: “Cliff:
> sensor defaults, CliffEvent and PotentialCliff handling,
> EnableStopOnCliff senders, the threshold schedule 50 / 400 / 150.” Evidence:
> `SC1..SC8,SC4a..SC4j,SC6`. Unresolved: “the treads-change path must skip the
> frame logic and run the stats unconditionally with the counter at 0 (C13.2);
> the +0x2C0 counter resets on a frame match and after 0x65 (C12.3); the stats
> gate must also see an AddRobotStateToHistory / GetLastStateWithFrameID
> failure (C12.4, M11). H4's BehaviorManager::RequestCurrentBehaviorEndImmediately
> and ActionList::Cancel(-1) are M7/M8 interfaces. Robot::Update's
> charger-platform clearing and the runtime Delocalize callers are M11
> geometry.” Location: `Cozmo.Robot/Sensors.cs`.

**C# production entry.** Full RobotState routing reaches
`CozmoSensors.Handle` (`Sensors.cs:778-870`). The stateful helpers are
`UpdateCliffRunningStats` at 376, `UpdateCliffDetectThreshold` at 416,
`IncrementSuspiciousCliffCount` at 447, platform handling at 585, and the
RobotStopped route at 717-746. `CozmoEngine.Delocalize` invokes the shared
clear path (`CozmoEngine.cs:1025-1038`).

| step | address | what the engine does | gates / order | failure result | float bits |
|---|---|---|---|---|---|
| Raw cliff update | 0x00512986 → 0x00634016 | Updates cliff component before touch/path/treads/origin processing. | Runs for every time-synced full state, even one later rejected for origin. | no message-specific failure | raw four u16 values |
| Treads transition special path | 0x00512A72..0x00512BAA | When committed state changes to/from OnTreads: zero `robot+0x2C0`, call Delocalize with `(status&2)!=0`, then jump directly to stats body. | Skips history/frame compare and skips the normal `r6` gate; stats/threshold run unconditionally this state. | Delocalize has no returned failure | none |
| Normal history gates | 0x00512BEE..0x00512C38; 0x00513088/0x00513110; 0x00512F0C | `AddRobotStateToHistory` failure, `GetLastStateWithFrameID` failure, or origin miss sets `r6=1`. | Any of these skips both stats and threshold update. | Boolean/nonzero failure only; deeper history cause is **UNKNOWN (M11)**. | none |
| Frame mismatch counter | ctor 0x0050FF08; 0x00512EAE; 0x00512F14..0x00512F88 | Counter is per consecutive mismatch run: reset on match/treads path, increment on mismatch, reset after reaching `0x65` (101) and delocalizing. First 100 mismatches still take stats path. | Origin/history failures leave it untouched. | at 101: error + Delocalize, current state skips stats | none |
| First-frame distance schedule | 0x00512D7A..0x00512DA2; 0x00512E84..0x00512EA6 | For current frame, first accepted matching state sends threshold 50 once; after traveled distance is strictly over 50 mm sends 400 once. | Source flags at +0x528/+0x52C make each one-shot per Robot object/frame regime. | failed robot send is only ordinary warning | distance `50.0f = 0x42480000` |
| Running statistics sample | 0x00634630..0x00634724 | Sample cliff[0] only while body moving, OnTreads, and value > cached threshold. Sliding window 100 with Welford add/removal. | push; if size 101 pop and remove using literal N=100; then variance. | gate false: no state change | removal `100.0f = 0x42C80000`; comparison/data are f32 |
| Suspicious stopped scan | 0x00634840..0x00634846; 0x006343B8..0x006344B0 | RobotStopped stamps last state time. Later, when body stopped, lower_bound that timestamp, walk in order, track min; increment if variance >10000 and min+15 < current cliff. Clear stamp after completed walk. | nonzero stamp and body-not-moving; early failures do not clear stamp, completed walk does. | no history element: clears stamp and no increment | `10000.0f = 0x461C4000`; addend 15 is integer cliff units |
| Threshold decrement | 0x00634514..0x006345AE | If cache <151 return. Else count++, new=max(cache-250,150), send it, count=0. Reachable 400 becomes 150. | cache gate before count | ordinary send failure only | integers/u16 only |
| Clear on Delocalize | 0x00510A5A → 0x006347A4..0x0063480C | Reset count; if cache !=400, cache=400 and send 400; clear deque/mean/variance/M2. | Runs before new origin/pose and localization notification. | ordinary send failure only | all stats reset to `+0.0f = 0x00000000` |
| Charger platform | 0x00511D4C..0x00511DB0; Robot::Update 0x00513C5C..0x00513E2A | Platform transition sends 50 entering, 400 leaving. Off-treads transition clears platform. Robot::Update also clears it when no charger/footprint intersection. | state-change only; contact flag can keep platform true. | Exact charger-footprint predicate belongs to M11 geometry and is **UNKNOWN here**. | geometry inputs not settled by M4 |
| RobotStopped external effects | 0x0053539C..0x00535480 | Evaluate suspiciousness; when cliff sensor enabled request current behavior end, cancel all actions with `-1`, broadcast RobotStopped. No robot message. | behavior request → cancel → broadcast | M7/M8 return/ordering beyond these calls is **UNKNOWN at the M4 boundary** | none |

## Handoff

These four records now have enough app-side rows to build only their named
M4 seams. Remaining uncertainty is deliberately partitioned:

- M4-009: M11 object creation/identity errors; M4-024 firmware forwarding.
- M4-017: any consumer/meaning of VisionMode 14 after its proven mask update.
- M4-018: source-complete runtime localization callers and the unreachable
  no-ObjectInfo branch.
- M4-019: M11 history/charger geometry predicates and M7/M8 behavior/action
  side effects.

None of those boundaries may be replaced by a plausible local condition.
