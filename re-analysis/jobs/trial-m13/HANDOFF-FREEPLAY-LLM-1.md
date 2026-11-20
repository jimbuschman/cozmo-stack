# FREEPLAY-LLM-1 handoff

| Input | Source and path | MCP observation |
| --- | --- | --- |
| `freeplay` enabled | Unity `Robot.cs:1767–1784` sends the Freeplay activity. Native G2E 0x92 calls SetCurrentActivity at 0x005A4FA2–0x005A4FB6. | The toggle returns `submitted` with the requested setting and the currently observed state. After delivery, `get_state.freeplay.active` is true. |
| Freeplay with severe low energy | ActivityFreeplay update 0x005AD9D0 and desired behavior 0x005AE29C. Shipped `behaviorSystem/activities_config.json` gives NeedsSevereLowEnergy priority 2 and a strict-priority chooser. BehaviorManager selects at 0x005A2A74–0x005A2AB4. | `NeedsSevereLowEnergy` selects `Needs_SevereLowEnergyGetIn` on entry. |
| MCP turn, then authored animation | The existing standalone app bracket follows Unity `Cozmo.Hub/NeedsHub_2018.cs:376–390,486–490`. It selects Selection and delivers G2E 0x93 Wait/-1 (0x0060AAAE–0x0060AAC8), which starts no action (0x0059EC34–0x0059EC3C). Switching retires an ordinary running behavior and preserves an active reaction (0x005A119E–0x005A11D8). Robot::Update dispatches ActionList at 0x005140BC. | Each command runs while enabled is true, active is false, and `pausedForCommand` is true. Native in-place turn (Init 0x00545FA0, CheckIfDone 0x0054642C) sends body motion and completes on F0/C4. The animation group map resolves `CodeLabHappy` to `ag_codelab_happy`, which plays through 0x0054432C and 0x00543F20 and completes on the matching CA/CB. |
| Last command retires, or Freeplay is switched off | The app bracket re-enters Freeplay after ActionList unlinks the last action. Native SetCurrentActivity selects and stamps +0x50 from engine time at 0x005A11E8–0x005A122A and updates the GameControl flag. Explicit off uses Unity's Selection and Wait/-1. | After each completion, Freeplay becomes active and the chooser selects `Needs_SevereLowEnergyState`. The reply and `get_state` report `lastResumedAtEngineSeconds` from the activity stamp. After explicit off, Freeplay stays off and Selection keeps Wait. |

The November 18 capture's enable call at host 502058.259 ms was followed by `Wait → Needs_SevereLowEnergyGetIn` and later `Needs_SevereLowEnergyState`. Its disable call at 547039.390 ms ended in Selection and Wait. The capture contains a 0.785 rad turn at 1.5 rad/s with 10 rad/s² acceleration and a `CodeLabHappy` request in earlier windows. The replay uses those exact arguments inside the recorded activity sequence, sets the critical starting energy through the native SetNeedsLevels path (0x00697E78), and supplies offline motor and animation acknowledgements. Both commands returned SUCCESS. Both resumes were observed on the completion's engine tick under the replay clock. The original needs and mood owners remained live, with energy 0.05 and simple mood Default at both command completions (MoodManager::GetSimpleMood 0x0067C084). This measured delay is specific to the replay.

`freeplay`, `turn-in-place`, `animation` and `get_state` now include the same Freeplay snapshot with enabled, active, pausedForCommand, activity, behavior and lastResumedAtEngineSeconds. The timestamp describes the last automatic command resume for that robot connection and starts empty. The toggle also gives requestedEnabled, so a posted command is distinguishable from the applied state. This change observes the existing action bracket and chooser. It adds no engine action or protocol traffic and changes no fidelity classification.

## Two-minute operator check

1. Put Cozmo on his treads with room to turn. Ask Claude to “let Cozmo play on his own.” Confirm Freeplay active and note the child activity or behavior.
2. While he is doing something, ask Claude to “turn about 45 degrees, then play CodeLabHappy.” Observe each command ending and Cozmo going back to an activity. Compare the two reply snapshots and the latest `get_state`. The activity can differ with needs, surroundings, and active reactions.
3. Ask Claude to “turn Freeplay off.” Confirm `get_state` shows Freeplay disabled and Selection with Wait once any active reaction clears. Observe for the rest of the two minutes.

The combined sequence passed offline. Operator robot acceptance remains open.

## Verification

Build `13f86c26` passed all 8,815 tests in 60.90 minutes with the local recordings enabled. All 76 focused Freeplay, bridge and reaction tests passed. The fidelity check passed for 765 records.

Set `COZMO_LLM_SESSION_BUNDLE` to the 20261118-075139-LLM-SESSION directory to read the recorded MCP inputs. The offline fixture also runs with the documented turn and animation values when that bundle is absent.
