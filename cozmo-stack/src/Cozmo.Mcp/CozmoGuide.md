# Cozmo operating guide

## Working with him

- Read `get_state` and check `connection.connected` and `connection.ready` before an action.
- Look with `get_camera_image` before moving or manipulating an object. `annotate` adds matching face/object boxes. This reads the existing camera stream; it does not enable a different vision mode. With `annotate`, the returned image can be older than the newest received frame. Empty boxes mean there were no matching reports for the returned image.
- Use the current `worldObjectId` from `knownCubes` or `knownChargers` for object actions. `factoryId` and `radioObjectId` identify different things. Face actions use the signed `faceId` from state/events; follow `face-id-changed` updates.
- Read the entire action result, including `detail`, `report`, `during` and any `why`. Then check fresh state or camera observations for the physical result.
- While a call is pending, other state, camera and event calls remain available. Use `read_events` with the last returned `cursor` as `since`. Set `waitMs` up to 30000 to wait for matching events. Set `significantOnly: true` to wait for events with the shared `significant` flag. Choose `types` to include or `excludeTypes` to exclude named event types. `limit` counts matching events before compaction and defaults to 100. Reuse the new cursor even if a page has fewer than 100 events. An empty page after waiting is a timeout. `gap=true` means older events were evicted [senses].
- Native reactions and activity changes can interrupt an admitted action. Use `during` to see those events before choosing another command. `track-face` may remain pending until native completion or caller cancellation.
- Before a multi-step task like stacking or enrolling a face, use `lock-reactions` with a task-specific ID and the reaction triggers you need to suppress. For example, lock `["CubeMoved","ObjectPositionUpdated"]` while stacking. Trigger names are case-sensitive. `"all"` disables every mapped trigger while the lock is held. Use `unlock-reactions` with the same ID when the task ends. Other IDs continue to hold their locks.

## Reading results

`isError` is the MCP tool error flag. `structuredContent` contains the same JSON as the text content block.

For motion, animation and speech, `admitted` reports whether the action was accepted; `status=completed` means its attempt ended. `engineResult=0` / `SUCCESS` is the native success code [why]. Nonzero codes retain their native names. `owner-retired` means the original robot owner went away; `not-admitted` means no action was accepted. Check the specific tool's `report` and subsequent observations to interpret success.

Action tools accept optional `waitAdmittedMs` from 0 to 30000. The default of 0 makes one admission attempt. A positive value lets a reaction finish before resubmitting a refused action. An action already admitted keeps its original result, including an interruption. `admissionWait` reports `waitedMs`, the blocking `reactions`, and whether the wait `timedOut`.

**Turn to a face:** an unknown ID can return native SUCCESS and turn nowhere. `report.facePoseAvailable=false` and `detail` identify that case [findings]. An enrolled name alone does not establish a current visible pose.

**Animation:** inspect `report.animationName`, `report.playbackTag`, `truncatedByRobot` and `playbackIssues`. A robot buffer cutoff can leave native SUCCESS while MCP `isError=true`. The issues report observed early ending/BufferFull evidence; tag/name may be null when association is unknown. A selected clip does not establish that the robot played its whole stream. Use the playback observations and what was seen/heard [budget].

`why.engineResultName` preserves the native result name. `why.snapshot` contains captured per-command comparisons/observations; `why.hint` explains them. `why.checkAvailable=false` means no applicable native failure check was captured. Earlier comparisons may have been superseded by later steps. Untagged docking observations do not by themselves establish a failure cause [why].

`during` covers the event window from invocation through completion. Compact replies keep single sightings and other events in `events`. Repeated `face-seen`, `cube-seen`, `charger-seen`, and `robot-state` observations are grouped in `sightings`, with counts, first and last timestamps, and the last payload. Compact `during` and `read_events` also group repeat `face-recognised` events by face ID and name. The counts cover the repeats. Each face's first recognition and every name change in the window keep their full payloads, along with face ID changes, losses, and enrolment, rename and erase events. An oversized `during` page includes a `continuation`. Pass its `since` and `excludeTypes` to `read_events` and keep paging through `through`. When the complete compact action window fits only once, `structuredContent` carries it and the text includes the action result and identity entries with `during.detailsInStructuredContent: true`. Use event cursors to deduplicate any completion already in the action reply. The action's own `cursor` still marks the end of its window.

For a host on the Cozmo machine, start the server with `--event-stream-port 8765`
and send one newline-terminated JSON subscription to `127.0.0.1:8765`, such as
`{"significantOnly":true}`. For interactive Claude Code, add `--channel` to
the MCP server arguments and enable the configured server as a development
channel. It pushes significant event pages after initialization. The server
README has examples for both clients and the in-process C# feed.

Use `duringDetail: "full"` for the complete retained action window at once. On `read_events`, `detail: "full"` keeps all matching event payloads within the chosen limit. Both full options can produce large replies. Poll during long calls when you need a complete history before the feed evicts it [senses].

**Faces:** `teach-face` and `enroll-face` are aliases. Native success returns `faceOperation.outcome=enrolled`, face ID/name and engineResult=0. Failure returns the native reason or `no-report`; a failed save can leave a partial named record. Rename and erase return `confirmed`, with `renamed` or `erased` and the ID/name information. Their confirmations acknowledge the mutation before NV persistence or optional spoken-response completion. Erase's native report name is empty; `lastKnownName` is a labeled host snapshot. Continued sightings can register an unnamed guest, even with the same ID. After enrollment/rename, wait for a fresh named observation separately [faces].

**Settings:** `freeplay` returns `submitted` when its message is posted; observe activity/events for the effect. Needs settings return their confirmed status, including `loaded`/`applied`; they have no native action result. `set_needs` requires real mode and loaded stored needs. Full needs mode refills and pauses the native needs; real mode resumes native changes.

## Approaching the charger

Look at the charger marker, use its current world ID, and line up square before `mount-charger`. The final approach rechecks its current generated predock goal after visual verification.

The recorded poses required absolute yaw error strictly below approximately 7.5 degrees and a Y offset within approximately 21 mm [charger]. The translation thresholds depend on the generated geometry; these are the recorded examples' bounds, not a universal fixed box. The comparisons in `why` use robot MAIN-parent axes. Use the captured live bounds when available and the current camera/state to plan a correction. Seeing the charger or completing a preceding turn does not establish that the final approach passed.

A `why.snapshot.pathArrival` comparison comes from DriveToPose after the firmware path completes. This earlier check compares the full goal rotation within 10 degrees and applies its XYZ tolerances. The snapshot gives the selected goal, arrival pose, and rotation error when the comparison reached it [dock-arrival].

On completion, check fresh `isOnCharger` and `isCharging`. A native failure's `why` may describe the final pose comparison, backup pitch, planner state, or an unavailable check [charger, why].

## `get_state` fields

This is a snapshot of the latest available data. `timestampUtc` is the host read time; observation timestamps belong to the robot clock. A null/unavailable pose or reading is not a zero measurement.

| Field | Meaning |
| --- | --- |
| `timestampUtc` | Host UTC time of this snapshot. |
| `connection` | `connected`, engine `ready`, and transport `link`; readiness comes from delivered engine readiness. |
| `batteryVolts` | Latest robot battery reading, or null without a connected state. |
| `isCarrying`, `carriedObjectId` | Carrying state and engine object ID, or unavailable/null. |
| `isOnCharger`, `isCharging` | Latest native contact/charging state, or null when unavailable. |
| `activity`, `highLevelActivity`, `behavior`, `reaction` | Latest activity and reaction observations; null when unavailable. |
| `reactionLocks` | Current named locks, each with its `id` and mapped `triggers`. Cozmo and the caller can both hold locks. |
| `needs` | `keepFull`, `loaded`, `applied`, `paused`, plus `repair`, `energy`, `play`. The flags distinguish requested mode from applied native needs. |
| `knownFaces` | Signed `faceId`, `name`, `lastObservedTimestamp`, `enrolled`, `relative`. Enrolled names can remain without a current sighting. |
| `knownCubes` | `factoryId`, `radioObjectId`, command `worldObjectId`, `type`, radio `connected`, `poseState`, `lastObservedTimestamp`, `relative`. A located cube need not have a current radio connection. |
| `knownChargers` | Command `worldObjectId`, `poseState`, `lastObservedTimestamp`, `relative`, in the current origin. |

`relative` has `xMm`, `yMm`, `zMm` in robot MAIN axes, planar `distanceMm`, and `bearingDeg` positive to the robot's left. Null means the pose could not be expressed relative to the robot, or no observed pose is available. `go-to-pose` instead takes an established target in the current world origin [senses].

Use `set_camera_color` with `enabled: true` to request colour on the camera's current stream. Use `enabled: false` to request greyscale again. The tool confirms the requested setting. Check `isColor` on a newer frame to see what the robot sent. The same vision modes keep supplying face and marker detections from the grey image derived from each colour frame.

`get_camera_image` returns PNG image content and metadata: `imageId`, `robotTimestamp`, `receivedUtc`, dimensions, `isColor`, `encoding` (effective image encoding after parsing the first chunk), exposure-time `headAngleRad`, `annotated`, matching `faces`/`objects`, and `observationReportsAvailable`. `visionProcessed` tells whether vision consumed the returned frame. `latestReceivedImageId` lets you see when annotation selected an earlier processed frame. Exposure head angle can be null [senses].

`list_animations` returns `loaded`, case-sensitive `triggers`, and `clips`; it reads the catalogue without selecting/playing a clip.

## Sources for the operating details

These are repository handoffs for the behavior already built; hardware acceptance stays with the operator.

- [charger] `re-analysis/jobs/trial-m13/HANDOFF-MOUNT-CHARGER-LLM-1.md`: geometry-dependent bounds and strict heading comparison. Recorded Y threshold 21.423 mm, heading limit 7.499427 degrees; rounded above for operating guidance.
- [dock-arrival] `re-analysis/jobs/trial-m13/HANDOFF-DOCK-CHARGER-POSE-1.md` covers the captured charger pose and the native DriveToPose verdict.
- [why] `re-analysis/jobs/trial-m13/HANDOFF-MCP-ACTION-WHY-1.md`: unchanged native result, captured comparisons, MAIN-parent axes and unknown checks.
- [findings] `re-analysis/jobs/trial-m13/HANDOFF-LLM-SESSION-FINDINGS-1.md`: unknown-face SUCCESS with no turn and `FacePoseAvailable`.
- [budget] `re-analysis/jobs/trial-m13/HANDOFF-ANIM-BUDGET-FIRMWARE-1.md`: robot cutoff reporting while preserving native SUCCESS.
- [senses] `re-analysis/jobs/trial-m13/HANDOFF-LLM-SENSES-1.md`: camera metadata, coordinates, `during`, cursors and waitMs bounds. State field names follow `CozmoBridge.GetState` / `HostNeedsStatus`.
- [faces] `re-analysis/jobs/trial-m13/HANDOFF-LLM-TEACH-FACE-1.md`: enrolled/renamed/erased outcomes and separate persistence/observation timing.
