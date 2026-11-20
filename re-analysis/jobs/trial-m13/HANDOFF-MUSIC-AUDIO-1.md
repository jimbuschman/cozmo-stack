# MUSIC-AUDIO-1 handoff

| Recorded input | Engine or app path | Visible result |
| --- | --- | --- |
| F18 authored animation command | G2E animation trigger → AnimationTriggerMap and animation group → audio and body tracks → shared Wwise robot object and Hijack | Robot voice and effects samples, 180 body packets, SUCCESS after frame 538 |
| Optional Unity CodeLab MamboMusic block | GameAudioClient object 4 → Music.bnk segment 773345523 → platform buses | Separate phone music loop when the app invokes the sound block |
| F15 MCP enroll-face, then fresh SayName transition | G2E 0x87 → EnrollFace → conductor object 1 → Music.bnk playlist 5859510 | Original audible say-name cue on the PC |
| F15 enrollment result or command exit | E2G 0x56 or host command exit → standalone M1-064 → native conductor stop → bank action 650118968 | Enrollment-owned PC audio fades over 500 ms |

## Mambo

The animation posts eight robot voice and effects events from `anim_dancing_mambo_02.bin` and never posts the mambo backing track. The recorded action transmitted 147 robot sample packets, so music was absent even though the robot audio path carried the clip's voice and effects. Audible output from the speaker is an operator observation.

| Authored trigger (ms) | Selected event ID | Cozmo.bnk target |
| --- | --- | --- |
| 594 | 3957181661 | Shared_Interested_Long, random container 101503269 |
| 2376 | 1907152766 | Srv_Surprised, 109709110 |
| 2508 | 992526618 | Codelab_Conducting, 1067291276 |
| 3597 | 1844409553 | Sparked_Workout_Lift_Success_Strong, 571556817 |
| 4653 | 1531806306 | Fist_Bump_Giggle, 314987702 |
| 10197 | 430910432 | GP_Shared_Win_Level_1, 850341947 |
| 12573 | 2222654907 | GP_QT_Tap_Effort, 759282137 |
| 14916 | 302804072 | Scrn_Happy, 484602819 |

`AnimationTriggerMap.json:168–170` selects `ag_codelab_dancing_mambo`. The captured RNG choices at the three alternative frames came from 0x004F9AEC–0x004F9D4C. All eight events use Play on a game object and reach `Cozmo_Robot` bus 1723505802 in `Cozmo.bnk`. The native on-robot animation constructor uses object 7 at 0x0059A076–0x0059A0B6. Its routing gives robot 1 bus 2678428988 (registration at 0x0059962A–0x0059966A) and Hijack plug-in 0x112C3 index 1 in `Init.bnk`. The buffer is pulled and encoded at 0x00597DB4. The captured 0x8E frames have 744 encoded samples each.

Unity's separate CodeLab sound block `CodeLabGame.cs:2028–2079,2583–2592` plays event 1905159696 and stops it with 1709157641, on game object 4 (`GameAudioClient.cs:35–38`, `GameObjectType.cs`). In `Music.bnk` those actions both target segment 773345523 under bus 3829101743 → 3991942870 → 3803692087 with no robot Hijack. Track 848661918 uses WEM 633958781, the 183 BPM mambo loop. This is the phone music the operator expected, supplied by the separate app block.

The clip metadata gives 15,444 ms. The scheduler advances authored time by 33 ms and considers only the current body keyframe per frame (0x0057CCAA–0x0057CE5A). Each body key sends a start at counter zero, then a stop on the frame where the counter reaches its duration (0x004FBA8C–0x004FBB0E). The next key waits until the following frame. Applying this recurrence to the 90 authored body keys predicts every one of the 180 recorded body packets, including the final stop at zero-based frame 537. The capture has 469 face packets and 538 audio frames, with a 2.233 s tail after the last face frame. That tail contains body packets and silence. The start/end times were 727118.082–745076.649 ms in the host capture and 727504.505–745465.922 ms for the robot acknowledgements. The last non-silent audio frame was at 742776.563 ms. The offline replay uses captured 0xF1 counters and checks the same 180 body packets, along with the complete frame count and audible robot sample delivery.

## Enrolment

The fresh enrolment engine branch starts the music when it moves from scan to saying the name, under the app's `useMusic` flag (0x005FD2F6–0x005FD310). In the capture the SayName group was selected at 15:50:19.912 UTC. The PC output does not originate in the MeetCozmo activity's Freeplay music update. That path reports no music on entry. The cue is state 0x99EA6572, `Minigame__Meet_Cozmo_Say_Name`, on group 0xEDF036D6 and object 1. The conductor promotes the pending state at 0x008D757C, posts `Play__Music__Play` 2748524418 if needed (0x008D75AA–0x008D75BC), then applies the Wwise group state. `Music.bnk` dispatches switch container 797713379 to playlist 5859510. Its tracks use WEM 766556609 for the say-name music and WEM 277394585 for the Meet Cozmo loop. They reach the platform output. Native enrolment reports completion without a music stop.

The Unity app restores `GetDefaultMusicState()` when leaving the instructions state (`FaceEnrollInstructionState.cs:87–104`, `GameBase.cs:161–164`, `GameAudioClient.cs:136–140`). The minigame result closure requests Freeplay music (`GameBase.cs:1124–1126`). The standalone command has no instructions state to emit that input. The native conductor stop path at 0x008D7758–0x008D777E posts `Stop__Music__Stop` 148746486. Its bank action 650118968 stops the music switch container with a 500 ms fade.


M1-064 supplies the standalone screen-exit input. The MCP command keeps the original cue audible during enrollment and releases its request after success, failure, cancellation, or timeout. The serialized audio worker cancels a pending enrollment cue or fades an active one. A newer music request owns its own state, so the older command cannot stop it. The original G2E enrollment defaults still request the cue. The standalone host drops back to silence from enrollment music because it has no Unity challenge music state to restore.

## Verification

Build `3df057c3` passed all 8,812 tests in 59.71 minutes with the local recordings enabled. The 12 audio replay tests, the focused bridge-message tests and the fidelity check for 765 records passed.

`AiAuthoredAnimationTests` replays the recorded 0xF1 counters and checks the 538-frame robot stream, its keyed body packets and encoded sound. A bank graph test checks both output routes. `McpFaceEnrollmentReplayTests` supplies actual F15 camera pixels and NV calibration from the raw recording, then checks the PCM device boundary for the original music cue followed by silence. `NativeWwisePlatformTests` exercises pending cancellation and overlapping requests on the serialized native worker. No robot run was made for the standalone cleanup.
