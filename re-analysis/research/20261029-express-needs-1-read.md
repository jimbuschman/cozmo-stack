# EXPRESS-NEEDS-1: BehaviorExpressNeeds, one whole behaviour (read, 2026-10-29)

**Who and how.**
- Read by cozmo-extractor in a Claude-run job; read-only.
- Binary: `resources/lib/armeabi-v7a/libcozmoEngine.so` 3.4.0-1204, disassembled with capstone.
  - PLT thunks were named from index.tsv.
  - The veneers were resolved through their literals:
    - 0x8CBF9C → 0x4B1EE4 EvaluateY;
    - 0x8CC62C → 0x4AAEDC ClearSevereNeedExpression;
    - 0x8CC0BC → 0x4B01B0 ~IBehavior.
- EN = BehaviorExpressNeeds.

**Checked by Claude (manager) against the .so:**
- IsRunnableInternal 0x005EE570..0x005EE5DC;
- InitInternal 0x005EE690..0x005EE788;
- ResumeInternal 0x005EE7D4 and StopInternal 0x005EE7D8..0x005EE7E8;
- the +0x28 slot 0x005EE860;
- the callback 0x005EE89E..0x005EE8B6.

All agree with the rows below.

## Path table

| # | step | what the original does | citation | record | class |
|---|---|---|---|---|---|
| C1 | factory | BehaviorClass 18 (ExpressNeeds): new(0x140), ctor 0x005EE144, shared_ptr | 0x0059CBA4 mov.w r0,#0x140; 0x0059CBB4 blx 0x4afba4; unity BehaviorClass.cs:23 | M7-018 | EXACT_SOURCE |
| C2 | ctor defaults | IBehavior C2(robot,json) at 0x005EE14E. Defaults:<br>• +0x11C need = 3 (Count)<br>• +0x120 bracket = 4 (Count)<br>• +0x124 = new GraphEvaluator2d<br>• +0x128..+0x130 = empty vector&lt;AnimationTrigger&gt;<br>• +0x134 = +0x135 = 0<br>• +0x138 last-success time = 0.0f<br>It subscribes to no messages. | 0x005EE152..0x005EE18E | NEW (M15-072 cites +0x134/+0x135) | EXACT_SOURCE |
| C3 | base JSON gates | The IBehavior ctor sets +0x70 = 0x55, +0x74 = 3 and +0x78 = -1.0f. ReadFromJson sets +0x74 = NeedIdFromString(requiredSevereNeedsState). | 0x005BBCA2..0x005BBCB6; ReadFromJson 0x005BBFB4 | M8-001 | ctor EXACT_SOURCE; the +0x74 store RECOVERABLE_GAP (not re-disassembled) |
| C4 | need | ParseString "need" → NeedIdFromString → +0x11C. Count gives a VERIFY log and construction continues. | 0x005EE19A..0x005EE1E4 | NEW | EXACT_SOURCE |
| C5 | needBracket | ParseString "needBracket" → NeedBracketIdFromString 0x0078252C → +0x120. No validity check. | 0x005EE1F6..0x005EE22E | NEW | EXACT_SOURCE |
| C6 | cooldown | json["cooldown"]:<br>• null → VERIFY NoCooldownConfig, and the graph stays empty;<br>• otherwise GraphEvaluator2d::ReadFromJson(+0x124). A failure gives sErrorF CooldownParsingFailed. | 0x005EE240..0x005EE2E0 | NEW | EXACT_SOURCE (all shipped files parse) |
| C7 | bools | shouldClearExpressedState → +0x134 and caresAboutExpressedState → +0x135, through GetValueOptional&lt;bool&gt;. Absent → 0. | 0x005EE2E4..0x005EE33C | M15-072 | EXACT_SOURCE |
| C8 | animTriggers | Array order: asString → AnimationTriggerFromString. Count (0x23F) is skipped silently; every other result is push_back'd into +0x128. | 0x005EE340..0x005EE3FC | NEW | EXACT_SOURCE |
| C9 | shipped configs | Each config sets need / bracket / triggers / cooldown graph (x = need level, y = s) / extra keys:<br>• **MildLowEnergyRequest:** Energy / Warning / [NeedsMildLowEnergyRequest 302] / (0,20)(0.1,20)(0.5,60)(1,60) / none<br>• **MildLowPlayRequest:** Play / Warning / [303] / same graph / none<br>• **MildLowRepairRequest:** Repair / Warning / [304] / same graph / none<br>• **SevereLowEnergyForcedGetOut:** Energy / Critical / [309] / (0,0) / shouldClear=true, caresAbout=true<br>• **SevereLowPlayBored:** Play / Critical / [329, 327, 330] / (0,30)(0.1,60) / requiredSevereNeedsState Play<br>• **SevereLowPlayRequest:** Play / Critical / [316] / (0,5)(0.1,60) / requiredSevereNeedsState Play | behaviors/freeplay/needs/*.json | M7-002 | EXACT_SOURCE |
| C10 | animation groups | The triggers map to ag_energy_request, ag_play_requestplay_level1_01, ag_repair_reaction_01, ag_energy_getout, ag_play_requestplay_level2_01, and ag_bored / ag_bored_event / ag_bored_getout. | AnimationTriggerMap.json:1212-1325 | M8-021 | cited |
| V1 | vtable | 0x0102A24C. EN overrides only the dtors, +0x28 (0x005EE860), Init, Resume, IsRunnableInternal and Stop. UpdateInternal is the base one, and there is no HandleWhileRunning. | relocations 0x0102A24C..0x0102A2D8 | NEW | EXACT_SOURCE |
| R1 | base gates | +0x20 = 0x005BF04C → 0 (not runnable while robot+0x355). +0x24 = 0x0059EC12 → 0 (not while robot+0x34A). +0x28 = 0x005EE860 → 0 (**not runnable while carrying**). | 0x005BD864..0x005BD89C | M8-001; NEW | EXACT_SOURCE |
| R2 | required severe need | IsRunnableBase: if +0x74 != 3, runnable only when +0x74 == [[robot+0x264]+0x30]+0x14. Runs before IsRunnableInternal. | 0x005BD7E6..0x005BD7F6 | M8-001; M15-072 | EXACT_SOURCE |
| R3 | other base gates | Running byte, AI process, unlock 0x55, the +0x78/+0x7C timers, strategy +0x38 (none in the EN configs), now >= +0x118. | 0x005BD778..0x005BD8C8 | M8-001 | shared |
| R4 | bracket | **If +0x135 and +0x120 == 3:** runnable-so-far only if +0x11C == the selected severe need ([[robot+0x264]+0x30]+0x14); IsNeedAtBracket is skipped. **Otherwise:** IsNeedAtBracket(GetCurNeedsStateMutable, +0x11C, +0x120) must be 1. | 0x005EE57A..0x005EE5AE | NEW; M15-072; M15-001 | EXACT_SOURCE |
| R5 | cooldown | c = GetCooldownSec (direct call). Runnable iff `(c + [+0x138]) < BaseStationTimer seconds`, in f32 with a strict `<`. +0x138 starts at 0.0. | 0x005EE5B0..0x005EE5DC | NEW | EXACT_SOURCE |
| R6 | GetCooldownSec | EvaluateY(+0x124, NeedsState::GetNeedLevel(GetCurNeedsState, +0x11C)). A null graph gives a VERIFY and 0.0, which is unreachable. | 0x005EE5E4..0x005EE626 | NEW; EvaluateY M7-013; GetNeedLevel M15-030 | EXACT_SOURCE call |
| I1 | compound | new(0xAC) CompoundActionSequential(robot) | 0x005EE690..0x005EE69A | M8-017 | EXACT_SOURCE |
| I2 | face turn | TurnTowardsFaceAction(robot, faceID 0, Radians(pi 0x40490FDB) max turn, sayName false), via int ctor 0x0054B8F0. The vptr is then set to **TurnTowardsLastFacePoseAction** (GOT 0x0103EA0C). AddAction(turn, **ignoreFailure 1**). It is always added. | 0x005EE6A4..0x005EE6E8 (movs r3,#1 at 0x005EE6DA) | NEW call; M13-014 / M13-027 | EXACT_SOURCE call |
| I3 | animations | For each trigger in order: TriggerAnimationAction(robot, trigger, numLoops 1, interruptRunning true, tracks 0, timeout 60.0f, strictCooldown false). Not lift-safe. AddAction(anim, **ignoreFailure 0**). | 0x005EE6F2..0x005EE748 (movs r3,#0 at 0x005EE736) | NEW call; M8-005/M8-020/M8-021 | EXACT_SOURCE call |
| I4 | StartActing | StartActing(compound, lambda $_0 capturing this). Init returns 0. | 0x005EE74A..0x005EE788 | M8-001 | EXACT_SOURCE |
| I5 | callback | If the ActionResult category byte (byte 3) == 0 (SUCCESS): +0x138 = BaseStationTimer::GetCurrentTimeInSeconds(). Otherwise unchanged. | 0x005EE89E..0x005EE8B6 | NEW | EXACT_SOURCE (whether the compound reports SUCCESS after an ignored turn failure rests on M8-017) |
| U1 | Update | Base IBehavior::UpdateInternal: ends when the action ends. | vtable +0xC | M8-001 | EXACT_SOURCE slot |
| P1 | Resume | `movs r0,#1; bx lr`: resume always fails. | 0x005EE7D4..0x005EE7D6 | NEW | EXACT_SOURCE |
| S1 | Stop | If +0x134: ClearSevereNeedExpression([[robot+0x264]+0x30]), whatever the reason. Otherwise nothing. | 0x005EE7D8..0x005EE7E8 | M15-072 | EXACT_SOURCE |
| D1 | dtor | Frees members; no observable effect. | 0x005EE7EC..0x005EE858 | NEW | EXACT_SOURCE |
| X1 | absent | No E2G, DAS, mood event, objective, lock or RNG in the EN bodies. | bodies above | NEW | EXACT_SOURCE (absence) |
| A1 | selection | The five non-forced IDs appear only in the StrictPriority interludeBehaviorChooser of six freeplay activities, in the order EarnedSparks, SevereLowPlayBored, SevereLowPlayRequest, MildLowRepairRequest, MildLowEnergyRequest, MildLowPlayRequest. Not scored. | activities/freeplay/*.json | M15-002, M15-006 | shared |
| A2 | Play severe | Selected becomes Play only through PlayAnimOnNeedsChange Stop (M15-072), gated by StrategyExpressNeedsTransition. | 0x006136D8..0x00613706 | M8-006, M15-072 | EXACT_SOURCE strategy |
| A3 | ForcedGetOut reachability | UNKNOWN: no activity, map or Unity referent, and no native FindBehaviorByID(0x2E) literal was found. | scan | NEW | RECOVERABLE_GAP |

## Observable result
- **Robot.** When the gates pass:
  1. Turn toward the last face pose. A failure is ignored.
  2. Play the config's triggers in order (1 loop, 60 s timeout each). The first failing animation ends the compound.
- **State.**
  - The cooldown time moves only on a SUCCESS result.
  - ForcedGetOut clears the severe-need expression on any Stop.
  - Resume is refused.
- **C# API.** Nothing specific to EN; only the base lifecycle broadcasts and the child-action completions.

## C# stack before the build (facts)
All citations are in `ExplorerBehaviors.cs` unless another file is named. `ExpressNeedsBehavior` is at :408-467 and its
loader at :571-579.

**Differences from the engine:**
- **Cooldown stamp:** made at OnStart (:447). The engine stamps only on SUCCESS.
- **Cooldown test:** `null || now-last >= c` in double (:442). The engine uses 0.0 as the start, a strict `<` and f32.
- **Required severe need:** repeated inside Internal (:441).
- **Face turn:** gated on HasAnyFaces with id -1 (:455-458); that gate is invented. The action is disposed by `using`
  while it is still running.
- **Animations:** each one is a separate PlayTrigger and continues after any result (:452). There is no compound and no
  SUCCESS gate.
- **Runnable gates:** no RunnableGate20 or RunnableGate28 override (the base returns true with MISSING).
- **Resume:** not overridden; the base re-runs.
- **Logging:** a stack-only "expressing ..." log (:448).

**Unreached loader branches** (no shipped file reaches them): ignoreCase TryParse, which drops the behaviour on a bad
need or bracket; the invented `(0,20)` cooldown default.

**Matches the engine:** OnStop's SevereNeeds.Clear when ShouldClear (:462-466).

## Existing records contradicted
- **M7 inventory row M7-behaviour.md:687** (not a manifest record):
  - it calls +0x128 "need action ids", but it is the animTriggers vector;
  - it calls pi "turn around", but pi is the max-turn argument;
  - it omits the AddAction flags and the SUCCESS-only callback.
- **M8-006:** its unresolved note "source of [[robot+0x264]+0x30]+0x14 UNKNOWN" is stale; M15-072 names it.

## Open
1. A3: ForcedGetOut reachability is UNKNOWN.
2. C3: the +0x74 store in ReadFromJson was not re-disassembled.
3. The turn child with face 0 (M13-014), and whether the compound returns SUCCESS after an ignored turn failure
   (M8-017).
4. EvaluateY on an empty graph, and unknown bracket strings: unshipped.
5. Not traced: the TurnTowardsFaceAction fine-tune's NeedsAction SeeFace registration (0x0054C2E0).
