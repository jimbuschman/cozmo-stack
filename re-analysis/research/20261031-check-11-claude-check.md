# Claude check: ChatGPT check 11 (repo-only MISSING sweep)

Date: 2026-10-31. Report: `20261031-check-11-chatgpt-extraction.md`, run at `0faf142b`, before the last two batch
commits. It covers `cozmo-stack/src/Cozmo.Robot/Behavior`: 23 EXISTS UNUSED, 201 EXISTS PARTIAL, 15 STILL ABSENT,
26 UNCLEAR. Spot checks were by search on `aba05e45`. This is a lead list and settles nothing.

**Best result:** rows 12 and 13. The canonical robot never binds its manipulation inputs. Tracing them exposed the
motor calibration blocker in `20261031-fix-batch-claude-check.md`.

## Rows checked

| Row | Claim | Verdict |
|---|---|---|
| 3 | NativePublicStateSelected has no production assignment | Wrong: `Activities.cs:1285` |
| 4, 7 | NativeCommonDrivingPush has no production assignment | Wrong: `Activities.cs:1291`, `:1321` |
| 39 | No bound IsUnlocked(3) | Wrong: `Activities.cs:688` |
| 18 | AcknowledgeFace has no greeting-time owner | Wrong: `FaceBehaviors.cs:271` reads the manager; row 76's missing writer stands |
| 14, 204-206 | Cliff objective, Frustration driving | Fixed after the sweep's commit |
| 94 | ChargerEvent "no broadcaster" comment | Stale comment only; the event is connected |
| 12, 13 | Canonical robot never calls BindFreeplayInputs | Right, and the most important row |
| 8-10, 57 | AIWhiteboardHandlers and the beacon renderer have no production constructor | Right |
| 11 | SetNativeNeedsCache has no caller | Right |
| 19-22 | FistBump idle and PanAndTilt delegates unassigned | Right; goes into FISTBUMP-STRATEGY-1 |
| 40, 41 | Pyramid filter and caches unbound | Right; PYRAMID-STRATEGY-BIND-1 |
| 60 | BehaviorAudioRecipient unassigned | Right |
| 90, 92, 210, 211 | SparkGate, WhiteboardHiccupByte, Forced and SparkNameInput have no production writer | Right |
| 133, 155 | Spark raw-ID join missing | Right; SPARK-JOIN-1 |

## Reading it

Of the first 25 rows, six that were checked were wrong or already fixed. So "no production caller" claims need one
search each before use, and its keyword search misses compact `x=y` assignment forms.

EXISTS PARTIAL at 201 rows is too broad to act on: many mean only "something related exists". Rows 15-17 and 25
concern legacy classes with native replacements, and are not worth wiring. Use it per job: when a job touches a file,
read that file's rows.

Next sweeps should ask one sharper question: for each optional delegate or input on a class, is it assigned on the
production construction path? That question found this batch's blocker.
