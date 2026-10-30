# THINK-ABOUT-BEACONS-1: BehaviorThinkAboutBeacons, one whole behaviour (read, 2026-10-29)

**Read.** cozmo-extractor, in a Claude-run job; read-only.
- Binary: `resources/lib/armeabi-v7a/libcozmoEngine.so` 3.4.0-1204.
- TAB = BehaviorThinkAboutBeacons. Class range 0x005E5AF8..0x005E5F4E, vtable 0x01029A28.

**Checked by Claude (manager) against the .so:** IsRunnableInternal 0x005E5CC2..0x005E5CD8, Init 0x005E5D94..0x005E5E4E,
SelectNewBeacon 0x005E5F0C..0x005E5F3C and +0x28 0x005E5F4C. All agree with the rows below.

## Path table

| # | step | what the original does | citation | record | class |
|---|---|---|---|---|---|
| F1 | factory | Class 0x35: new(0x130), ctor, shared_ptr. | 0x0059D006..0x0059D024 | M7-018 | EXACT_SOURCE |
| F2 | IDs | Hiking_ThinkAboutBeacons (39) and SparksThinkAboutBeacons (157). | BehaviorID.cs:44, :162 | M7-001 | EXACT_SOURCE |
| C1 | ctor | IBehavior ctor; an empty string at +0x11C; vptr; LoadConfig(config["params"]). | 0x005E5AF8..0x005E5B2C | NEW | EXACT_SOURCE |
| L1 | debug name | name + ".BehaviorThinkAboutBeacons.LoadConfig" | 0x005E5B6A..0x005E5B8E | NEW | EXACT_SOURCE |
| L2 | trigger name | ParseString(params, "newAreaAnimTrigger") → the string at +0x11C. It is resolved only at Init. | 0x005E5BB4..0x005E5BEA | NEW | EXACT_SOURCE |
| L3 | radius | ParseFloat(params, "beaconRadius_mm") → f32 at +0x128. | 0x005E5BEC..0x005E5BFA | NEW | EXACT_SOURCE |
| L4 | shipped values | Hiking: HikingReactToNewArea, 175. Sparks: HikingReactToNewArea, 75. | Hiking_thinkAboutBeacons.json:13-16; sparksThinkAboutBeacons.json:12-15 | M7-002 | EXACT_SOURCE |
| V1 | vtable | Overrides: dtors, **+0x28 0x5E5F4C (returns 0)**, Init 0x5E5CDC, IsRunnableInternal 0x5E5CC2. Base: Update, +0x20, +0x24, Resume, Stop, handlers, scoring. | relocations 0x01029A30..0x01029ABC | NEW | EXACT_SOURCE |
| R1 | +0x20 | Base returns 0: **not runnable off treads**. | 0x005BF04C | M8-001 | EXACT_SOURCE |
| R2 | +0x24 | Base returns 0: **not runnable while robot+0x34A**. | 0x0059EC12 | M8-001 | EXACT_SOURCE |
| R3 | +0x28 | Returns 0: **not runnable while carrying**. | 0x005E5F4C | NEW | EXACT_SOURCE |
| R4 | other base gates | The base defaults; the config sets none of them. | 0x005BD778..0x005BD8CA | M8-001 | shared |
| R5 | IsRunnableInternal | **Runnable iff GetActiveBeacon(whiteboard) is null** (no beacons). Nothing else is read. | 0x005E5CC2..0x005E5CD8 | M13-006 | EXACT_SOURCE |
| I1 | Init log | Info "Behaviors", name + ".InitInternal", "Selecting new beacon". | 0x005E5CF8..0x005E5D52 | NEW | log |
| I2 | SelectNewBeacon | Called unconditionally. | 0x005E5D94..0x005E5D98 | M15-011 | EXACT_SOURCE |
| S1 | beacon pose | Robot::GetPose (0x4EA398), then **GetWithRespectToRoot** (0x4DF628). It is never null. | 0x005E5F12..0x005E5F24 | M15-011 | EXACT_SOURCE |
| S2 | AddBeacon | AddBeacon(wb, pose, radius f32 +0x128). | 0x005E5F28..0x005E5F36 | M15-011 | EXACT_SOURCE |
| S3 | AddBeacon body | Appends a 0x14-byte AIBeacon {pose, radius f32 at +0xC, failure time f32 at +0x10 = 0}, then the debug render. | 0x0056C39C..0x0056C3DE | M13-006, M8-014 | EXACT_SOURCE |
| S5 | no choice | No RNG, distance, memory map or limit. | 0x005E5F0C..0x005E5F3C | NEW | EXACT_SOURCE (absence) |
| I3 | trigger | If the string is empty, no action. Otherwise AnimationTriggerFromString; if Count (0x23F), no action. Shipped: HikingReactToNewArea (0xF0) → ag_hiking_rtnewarea. | 0x005E5D9C..0x005E5DF4 | M7-001 | EXACT_SOURCE |
| I4 | action | TriggerAnimationAction(robot, trigger, 1, interruptRunning true, tracks 0, 60.0f, false). Not lift-safe. | 0x005E5DF6..0x005E5E18 | M8-020, M8-021 | EXACT_SOURCE call |
| I5 | StartActing | The RobotCompletedAction overload with an **empty** function: no callback. | 0x005E5E1C..0x005E5E3C | M8-001 | EXACT_SOURCE |
| I6 | Init result | 0 on every path. | 0x005E5E4A..0x005E5E50 | M8-001 | EXACT_SOURCE |
| U1 | Update | Base: Running while there is an action, else Complete. With no action it completes on the first Update. | 0x005BDA56..0x005BDA62 | M8-001 | EXACT_SOURCE |
| T1 | Stop | Base: the beacon is not removed. | 0x005BF050 | M8-001 | EXACT_SOURCE |
| P1 | Resume | Base: refused once the beacon exists. | 0x005BDA94..0x005BDAC8 | M8-001 | EXACT_SOURCE |
| K1 | absent | No objective, emotion, needs action, E2G, lock or DAS. | bodies | NEW | EXACT_SOURCE (absence) |
| W1 | beacon clearing (boundary) | Two paths:<br>(a) Robot::Delocalize → AIComponent::OnRobotDelocalized 0x00569F4E → AIWhiteboard::OnRobotDelocalized 0x0056A686: zombie maps, then ClearAllBeacons;<br>(b) ActivityGatherCubes::OnDeselectedInternal 0x005AF830..0x005AF846.<br>No other callers. | as cited | NEW | interface |
| W2 | beacon readers | TAB, BringCubeToBeacon, ActivityGatherCubes::Update. | index | M15-008, M15-019..M15-023 | shared |
| SC1 | selection | Hiking: Scoring chooser, flatScore 8.0. Sparks GatherCubes: StrictPriority, 4th of 6. | hiking.json:54-58; sparksGatherCubes.json:29-37 | M8-003 | shared |

## Observable result
- **Robot:** one ag_hiking_rtnewarea clip, which interrupts the running action, locks no tracks and has a 60 s timeout.
- **State:** one beacon at the robot pose w.r.t. its root, radius 175 (Hiking) or 75 (Sparks), failure time 0. After
  that, TAB is not runnable until the beacons are cleared.
- **C# API:** nothing from TAB itself.

## C# stack before the build (facts)
`CubeGameBehaviors.cs:1067-1089` `ThinkAboutBeaconsBehavior` (tagged M15-011) is constructed by hand at
`Behaviors.cs:573-574`.

| Area | Stack now | Engine |
|---|---|---|
| Config | Hard-coded radius (double) and trigger enum | Parsed from params (L2/L3) |
| IsRunnableInternal | Adds `M.RobotPose() is not null` | Beacon test only (R5); the pose condition is **invented** |
| Gates +0x20/+0x24/+0x28 | Not overridden (base true with MISSING) | All 0 |
| Beacon pose | `CopyPreActionRobotPose`, which can be null → Finish with no beacon | GetPose → GetWithRespectToRoot, never null |
| Action | PlayTrigger with no timeout | 60 s timeout, interruptRunning (I4) |
| Clearing | `AIWhiteboard.OnRobotDelocalized` has no caller, and ActivityGatherCubes is unbuilt, so nothing clears beacons in production | W1 (a)/(b) |

## Contradicted
- **M13-006 evidence:** "an int at +0x10". It is the f32 failure time (type only; "active = first" stands).
- **M15-011 unresolved:** "full copied robot pose". It is the robot pose w.r.t. its root (0x004DF628).

## Open
1. W1: the stack never clears beacons after a delocalization. This belongs to the whiteboard and delocalization
   records (M8-014, M11), not TAB.
2. The stack's OnRobotDelocalized also clears failure tables, which 0x0056A686 does not.
3. ParseString/ParseFloat on a missing key: unshipped.
