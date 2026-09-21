# M15 — freeplay, needs and the autonomy layer (2026-09-20)

What M15 added to `cozmo-stack/` so the stack chooses and runs behaviours on its own, where each piece comes
from in `libcozmoEngine.so` 3.4.0-1204 and the OBB's configs, and what is labelled. Code:
`src/Cozmo.Robot/Behavior/Needs.cs` (`NeedsConfig`, `DecayConfig`, `NeedsActionDelta`, `NeedsState`,
`NeedsManager`), `Activities.cs` (`Graph2d`, `ScoredBehaviorEntry`, `ScoringChooser`, `StrictPriorityChooser`,
`SelectionChooser`, `ActivityStrategy`, `FreeplayInputs`, `Activity`, `ActivityTreeLoader`), `FreeplaySystem.cs`,
`FreeplayStack.cs`, `ExplorerBehaviors.cs` (`LookAroundParams`, `ExploreLookAroundInPlaceBehavior`, `PanAndTilt`,
`FindFacesBehavior`, `DriveInDesperationBehavior`, `ExpressNeedsBehavior`, `PlayAnimOnNeedsChangeBehavior`,
`WaitBehavior`, `EarnedSparksBehavior`, `ExplorerBehaviors.LoadShipped`); the `freeplay` conformance command;
tests `tests/Cozmo.Protocol.Tests/FreeplayTests.cs` (11).

## 1. The engine's decision architecture, as reproduced

```
BehaviorManager::Update                      FreeplaySystem.Tick
  CheckReactionTriggerStrategies               manager.CheckReactions (M10 strategies; a running reaction holds)
  GetCurrentActivity → IActivity::               Current activity (Freeplay's sub-activity)
    GetDesiredActiveBehavior                     .Chooser.GetDesiredActiveBehavior(current)
      ScoringBSRunnableChooser /                   ScoringChooser: flat × penalties + running bonus
      StrictPriorityBSRunnableChooser              StrictPriorityChooser: first runnable
    ChooseInterludeBehavior                      the interlude chooser between two behaviours
  SwitchToBehaviorBase                         manager.StartAsync(id)
  IBehavior::Update                            manager.Update
ActivityFreeplay::ChooseNextBehavior          keep / end / pick (put-down, spark, wants-to-end + finished)
  CalculateDesiredActivityFromObjects          DesiredActivityFromObjects (faces, cubes → Socialize / PlayAlone / Hiking)
  PickNewActivityForSpark / priority order     PickNewActivity: desired first, then priority; strategy.WantsToStart; chooser yields
IActivityStrategy::WantsToStart / WantsToEnd  ActivityStrategy (cooldown, durations, mood scorer, needs brackets, pyramid, spark)
NeedsManager / NeedsState                     NeedsManager (levels, brackets, decay, action deltas, severe-expressed flags)
```

| piece | engine source | label |
| --- | --- | --- |
| Activity tree | ASSET `behaviorSystem/activities_config.json` (Selection, MeetCozmo, Feeding, Freeplay with 24 `subActivities` by `activityID` + `activityPriority`: 14 sparks at 0, the three needs activities at 1–3, PutDownDispatch 10, Socialize 11, Singing 12, PlayWithHumans 13, BuildPyramid 14, PlayAlone 15, Hiking 16, NothingToDo 17) and `activities/**` (`behaviorChooser`, `interludeBehaviorChooser`, `activityStrategy`, drive / idle triggers, `requireSpark`, `needsActionID`, `desiredActivityNames`); `ActivityFreeplay::CreateFromConfig` ("activityID" / "activityPriority" keys, "desiredActivityNames" → face-and-cube / face-only / cube-only / neither) | ASSET + NATIVE keys |
| Scoring chooser | `ScoringBSRunnableChooser` 0x00609ED8..0x0060A7A0 ("chooser", "behaviors", "scoring", "scoreBonusForCurrentBehavior"; `GetDesiredActiveBehavior`: `IBehavior::EvaluateScore`, `ScoreBonusForCurrentBehavior(running duration)`, "behavior '%s' has score of %f, so is interrupting running behavior '%s' which scored %f", random tie-break) | NATIVE |
| Scored entry | `IBehavior::ReadFromScoredJson` 0x005BC4xx (`flatScore`, `emotionScorers`, `repetitionPenalty`, `runningPenalty`, `considerThisHasRunForBehaviorObjective`); `EvaluateScore` 0x005BEF60: (internal score + running bonus) × running penalty, 0 unless `IsRunnableBase`, × repetition penalty (graph over seconds since the last run) | NATIVE; `boredomMultiplier` parsed, not applied (its source, the boredom emotion, is DEFERRED) |
| Strict-priority chooser | `StrictPriorityBSRunnableChooser::GetDesiredActiveBehavior` 0x0060B2xx: the first `IsRunnable` in the list | NATIVE |
| Activity strategy | `IActivityStrategy` constructor 0x005B4EDA (`activityCanEndDurationSecs`, `activityShouldEndDurationSecs`, `cooldownBaseSecs`, `cooldownRandomnessSecs`, `startInCooldown`, `requiredRecentOnTreadsEventSecs`, `requiredMinStartMoodScore`, `startMoodScorer`, `featureGate`, `wantsToRunStrategyConfig`); `WantsToStart` 0x005B529C (feature gate, cooldown, `MoodScorer::EvaluateEmotionScore`), `WantsToEnd` 0x005B5444 (duration), `RandomizeCooldown`; `ActivityStrategyNeeds` (an `InNeedsBracket` wants-to-run strategy, `higherPriorityStrategyConfig`), `ActivityStrategySevereNeedsTransition` (`ExpressNeedsTransition`), `ActivityStrategyPyramid` 0x005B4970 (unlock, `FindLocatedMatchingObjects`, 300 s, weak bases / pyramids), `ActivityStrategyFPPlayWithHumans` (`RequestGameComponent::IdentifyNextGameTypeToRequest`: the app's), `ActivityStrategySpark` | NATIVE structure; the pyramid rule's 0.4 random factor and the feature gate DEFERRED |
| Freeplay selection | `ActivityFreeplay::Update` / `ChooseNextBehavior` 0x005AE2xx: "Picking new activity because '%s' wants to end, and behavior finished", "…because '%s' was requested", "…to match spark '%s'", "The new activity '%s' picked no behavior", "Picked no activity", "There was no activity, and no activity was selected"; `HandleMessage<RobotOffTreadsStateChanged>`: "Kicking out '%s' on put down so we pick up a new one"; `CalculateDesiredActivityFromObjects` 0x005ADF4C ("robot.goal_from_face_and_cube %d:%d"); `IActivity::OnSelected` ("robot.freeplay_goal_started", idle animation) / `OnDeselected` ("robot.freeplay_goal_ended", cooldown); `IActivity::ChooseInterludeBehavior` ("Activity %s is inserting interlude %s between behaviors %s and %s") | NATIVE flow; the desired-from-objects activity is tried first in `PickNewActivity` (INFERRED ordering); the second gate on the answer - a null pick bars the activity from the re-pick, a new behaviour while the strategy wants to end ends it - is NATIVE (0x005AE68C..0x005AE704) |
| Needs | ASSET `needs_config.json` (levels 0.03..1, brackets Full ≥ 0.99, Normal ≥ 0.6 / 0.6 / 0.5, Warning ≥ 0.27 / 0.21 / 0.14 for Repair / Energy / Play, decay period 60 s, fullness cooldown 1200 s), `needs_decay_config.json` (per-minute rates by level band, connected / unconnected), `needs_action_config.json` (`actionDeltas` ± range, `freeplaySparksRewardWeight`); `NeedsManager::Update` → `ApplyDecayAllNeeds` ("Decaying need index %d with elapsed time of %f seconds", /60), `RegisterNeedsActionCompleted` ("needs.action_completed", `ApplyDelta`, `DetectBracketChangeForDas`), `NeedsState::GetNeedBracket` / `IsNeedAtBracket` / `GetLowestNeedAndBracket` | ASSET + NATIVE; decay modifiers, damaged parts, stars, persistence DEFERRED; the decay-band reading INFERRED |

## 2. The behaviours (16 shipped configs, plus FindFaces ×3 on the face pipeline)

| class (configs) | engine | what was read |
| --- | --- | --- |
| `ExploreLookAroundInPlaceBehavior` (SparksLookInPlace, Hiking_LookInPlace360) | 0x005E1D80..0x005E3A10 | `LoadConfig`'s 40 keys; `InitInternal` (motion profile, lift low, "Starting first iteration"), `DecideTurnDirection` (`RandDbl` vs `s0_MainTurnCWChance`), `TransitionToS1_OppositeTurn` … `S7_IterationEnd` ("Done %.2f deg so far", "Reached cone side %d", 2π, "Starting another iteration", "Done (reached max iterations)"), `CreateBodyAndHeadTurnAction` (a `PanAndTiltAction`), `IsRunnableInternal` (carrying, recent locations) |
| `FindFacesBehavior` (FindFaces_socialize, SparksFindFaces, FeedingFindFacesSevere; face pipeline) | 0x005C1936..0x005C1E40 | `maxFaceAgeToLook_ms`, `TransitionToLookAtLastFace` / `TransitionToLookUp`, "transitioning to base class, setting initial body direction" |
| `DriveInDesperationBehavior` (Needs_SevereLowEnergyState, Needs_SevereLowRepairState) | 0x005D8AE0..0x005D9F60 | config keys; "idling for %f sec"; `RandomizeNumDrivingRounds` (`RandIntInRange(1, 3)`, "driving to %d random points next time we drive"); `GetRandomDrivingPose` (40..100 mm, 50..150°, random side, "angle=%fdeg, dist=%fmm"); `DriveToPoseAction` (0.174533 tolerance) with the profile; `TransitionToRequest` (`TriggerAnimationAction` in a `TurnTowardsFaceWrapperAction(π)`); with cubes `TransitionToDriveToCube` / `LookAtCube` |
| `ExpressNeedsBehavior` (Needs_MildLow{Energy,Play,Repair}Request, Needs_SevereLowPlayBored, Needs_SevereLowPlayRequest, Needs_SevereLowEnergyForcedGetOut) | 0x005EE19C..0x005EE8C0 | `need`, `needBracket`, `cooldown`, `requiredSevereNeedsState`, `shouldClearExpressedState`, `caresAboutExpressedState`, `animTriggers`; `IsRunnableInternal` (`IsNeedAtBracket`, `GetCooldownSec` from the graph at the level); `InitInternal` (`TurnTowardsFaceAction(−1, π, false)` + `TriggerAnimationAction`s) |
| `PlayAnimOnNeedsChangeBehavior` (Needs_SevereLow{Energy,Play,Repair}GetIn) | 0x005BFDxx | `need`; `ShouldGetInBePlayed` (`IsNeedAtBracket(Critical)`), `StopInternal` marks "SevereNeedExpressed" |
| `WaitBehavior` (Needs_Wait, Wait) | no exported body | holds the slot |
| `EarnedSparksBehavior` (EarnedSparks) | 0x005DAEC2..0x005DAF80 | the pending-reward byte, 0xA4 EarnedSparks lift-safe |

## 3. What is not native, and is labelled

* `PickNewActivity` tries the desired-from-objects activity before the priority order (INFERRED); the engine's
  exact interleaving of "desired" and "priority" in `ChooseNextBehavior` was not traced instruction by
  instruction.
* `needsActionID` belongs to the **behaviour**, not the activity (2026-09-21, M15-008):
  `IBehavior::NeedActionCompleted` 0x005BE40C reports the running behaviour's own configured id when the
  caller names none, and sixteen behaviours call it; an activity's id is read into `IActivity+0x1C` and
  never looked at again. Sparks, stars and the sparks economy are the app's, but a pending reward is handed
  over when an activity ends (`NeedsManager::SparksRewardCommunicatedToUser`, M15-006).
* `boredomMultiplier` (dead data), the feature gate, decay modifiers, damaged parts and persistence are
  done; the pyramid strategy's randomness is the strategy-wide `RandDbl(randomness)`.
* The look-around's body speed profile is passed to `PanAndTilt`; the head speeds (`sxt` / `sxh`) are parsed
  and not applied (the head command carries its own maximum) — LOCAL.
* Behaviours the tree names that this stack does not build stay in the choosers as "not built" (the app's game
  requests, the memory-map explorer behaviours, PounceOnMotion, the face games) — `freeplay --tree` lists them.

## 4. Offline evidence

`FreeplayTests` (11): the shipped needs config decays and brackets as configured and the Feed action refills;
the scoring chooser weighs flat scores, repetition penalties and the running bonus and reports "not built" /
"not runnable"; the strict chooser picks the first runnable and yields to a higher one; strategies respect
cooldowns, durations, the mood gate, the needs brackets, the severe transition and the pyramid rule; the
shipped tree loads with its priorities, choosers, strategies and triggers; the stack binds most of what the
tree names and lists the rest; the look-around scans one full turn through its states and stops; drive-in-
desperation idles, drives to random points and requests; ExpressNeeds, the get-in and EarnedSparks follow the
brackets and flags; freeplay picks Hiking for nothing seen and PlayAlone for a cube after a put-down, inserts an
interlude, ends an activity on its duration and respects cooldowns (fakes); a Critical energy need takes the
NeedsSevereLowEnergy activity, plays the get-in once and runs the desperation drive until feeding refills it,
then Hiking resumes (the real tree and behaviours); and the whole stack on the fake robot side, sitting on the
charger with a cube on its side in view, picks PlayAlone, drives off the charger (score 1000) and rolls the
cube (RollBlockOnSide). Regression: the full suite, 630 tests at the end of M15.

## 5. Hardware pending

Item Z in `HARDWARE_TEST_PLAN.md`: `freeplay <ip> --obb <obb> --seconds 300 --acceptance`.

## 6. What remains before the hardware / integration phase

See `HANDOFF.md` "What remains".
