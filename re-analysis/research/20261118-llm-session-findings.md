# LLM-SESSION-FINDINGS-1: pickup, speech, and unknown face

Binary: libcozmoEngine.so 3.4.0-1204, SHA256
02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1.
Read against the worker at cb08a4e6 plus this diff. The operator authorized the
available local bundle copies on 2026-11-18. No robot was contacted for this work.

## First pickup

The bundle 20261118-075139-LLM-SESSION records MCP request 9 at 89641.2656 ms,
completion at 93719.8289 ms (4078.5633 ms), action tag 5, result 0x02000000
CANCELLED_WHILE_RUNNING. This was an approach, not a failed lift: DriveToPose
3000014 was still FollowingPath at 93453.9774 ms. At 93695.7529 ms the reaction
manager unlocked tracks; at 93701.7159 ms it reported ObjectPositionUpdated.
It selected AcknowledgeObject at 93704.9602 ms, cancelled DriveToPickupObject[5]
at 93705.8419 ms, aborted the path at 93715.3809 ms, and started its
TurnTowardsObject child at 93718.7051 ms. There is no MCP cancellation in that
interval. Driving off the charger is preceding context, not the cancelling call.

The native dispatch checks strategies at 0x005A3550, stops motors/unlocks tracks
at 0x005A3610..0x005A3682, and switches at 0x005A25E4..0x005A26DA.
ObjectPositionUpdated's gate at 0x006113E8..0x00611470 rejects carrying,
picking/placing, and on-charger-platform states; it does not exclude an approach
merely because its action type is DriveToPickupObject. DockingComponent+4 is the
IS_PICKING_OR_PLACING mirror, not an approach-action ownership flag (the native
IDockAction reader is 0x0055234A..0x00552398; Docking.cs documents this mirror).
BehaviorAcknowledgeObject builds its turn/verify/animation sequence at
0x00603644..0x00603730 and calls StartActing at 0x00603730. StartActing supplies
QueueAction position 0 (NOW), retries 0 at 0x005BDB5E..0x005BDB6C. With empty
pending queue, QueueNow cancels the current runner at 0x0053E350 and deletes it
at 0x0053E35E before inserting the incoming runner. The stack follows that path:
ObjectBehaviors.BeginIteration -> StartOwnedAction -> StartActing ->
QueueActionGuardedNow -> ActionQueue.QueueNowGuarded -> old.Cancel().

Thus the cancellation *after this reaction is selected* matches the engine.
Do not suppress reactions around external pickup as a repair. A narrower read
found three actual differences upstream, now corrected together:

1. PoseHelper 0x006124BA..0x006124FC first calls reacted.GetWithRespectTo(observed,
   temporary). A failed conversion returns false (not movement). On success it
   compares observed.IsSameAs(temporary, {80,80,80}, angleTolerance, delta, angle),
   then negates that result. The old stack directly negated
   reacted.IsSameAs(observed), turning an unrelated-origin failure into movement
   and omitting the native conversion/frame/rounding sequence. Equal distance
   thresholds select IsSameAs's spherical-distance branch, not a per-axis box.
   ShouldReactToTarget still ORs this result with unsigned elapsed > 600000 ms
   (0x00612598..0x006125A6); failed geometry does not suppress an expired timer.
2. ReactionData owns distinct ObservedPose and ReactedPose value snapshots.
   Existing observations assign into ObservedPose at 0x006123B6 and, when disabled,
   ReactedPose at 0x006123C8. New records copy-construct observed at 0x006123DA,
   default-construct reacted at 0x00612404, and optionally assign it at
   0x00612426. RobotReactedToId assigns observed to reacted at 0x00612ADE. The
   old C# record retained/aliased the caller's Pose3d objects. Besides allowing a
   mutable world observation to rewrite the reacted snapshot, that accidentally
   enters the special from==to GetWithRespectTo branch after the native helper
   is restored. Records now own/copy/dispose two distinct poses; no special-case
   identity bypass was invented to conceal the aliasing.
3. The position-update constructor installs angle f32 0x3F490FDB at
   0x00612182..0x0061218A (45 degrees). ObjectPositionUpdated calls this base
   at 0x0061107E and does not override it. The old decimal 0.785398 rounded to
   0x3F490FD8; the constant now carries the exact native f32 value. Distance
   remains 0x42A00000 (80 mm) and time remains 0x000927C0 (600000 ms), from
   0x00612198..0x006121B8. The named constant MathF.PI / 4f folds to exactly
   0x3F490FDB, with an address/hex comment and a literal-word regression
   assertion as CHECKLIST section 4 permits. This preserves the public const
   double API while doing the constant computation in f32. This is an
   instruction-backed correction, not tuning.

The old helper MISSING comment is closed by these addresses; no larger M10-003
settlement is claimed. The capture does not contain the strategy's private
reacted pose/time snapshots. It reports only one ObjectPositionUpdated reaction
before 94.5 s (the one that cancelled pickup), plus earlier Vision origin-mismatch
diagnostics at 86613/87393 ms. Those diagnostics alone do not prove that this
particular cancellation was caused by unrelated stored pose origins. Exact
same-target selection in a complete native replay remains unproven; the actual
cancelling stack call and its native QueueNow behavior are established.

Tests use native parented poses for the existing 40 mm / 100 mm / 69 degree
cases, exercise unrelated origins at both sides of the strict 600000 ms fallback,
and verify caller mutation cannot rewrite the reacted snapshot. Existing
ActionListTests.Q7_QueueNowWithEmptyPendingCancelsCurrentThenAtEnd checks
cancel-before-delete; reaction bindings/owned action tests remain in the gate.

## The sentence

The actual route is bridge say-text -> SayEnglishSentenceAsync ->
QueueEnglishSpeech(fitName:false) -> SayTextAction(direct:true). It uses style 0,
duration scalar 1, pitch 0; Name_Normal's random traits are NOT consulted.
SpeechProvider reads android.en in config/engine/tts_config.json: Ryan, speed 50,
shaping fallback 100. The complete input string is passed to Acapela, with no
punctuation split or sentence-length truncation. The app authority is
sources/com/anki/cozmo/CozmoTextToSpeech.java's
doLoadVoice/doCreateAudioData sequence (89..156), spot-read from the shared
primary-source checkout. sources/com/acapelagroup/android/tts/acattsandroid.java
490..540 calls stop before nSpeak, and stop at 583..604 sleeps 100 ms.
CozmoTextToSpeech calls stop again after its 100 ms isSpeaking poll loop.
The host AcapelaTts javaGlue path therefore includes two 100 ms stop waits
within preparation; it runs nSpeak synchronously on its synthesis worker and
checks the text-end callback rather than reproducing the Java thread/poll.
Native CreateSpeech dispatches work asynchronously at 0x0069E7C2;
GetOperationState reads the operation under its mutex at 0x0069E944..0x0069E978.
SayTextAction::Init at 0x00561488 checks state: not ready returns RUNNING at
0x005615DE; ready state 2 calls PrepareAudioEngine at 0x005614B8. Native
PrepareAudioEngine is 0x0069E9A8..0x0069EBAC. The direct branch builds a live
animation/audio keyframe and PlayAnimationAction at 0x00561544..0x005615D8.
CheckIfDone at 0x00561D4C delegates to its retained child. There is no recovered
fixed 11-second wait to reproduce or shorten.

The recorded boundaries (all bundle milliseconds) are:

| Boundary | Time | Delta from prior boundary |
|---|---:|---:|
| MCP input, complete text | 36756.2635 | - |
| SayText preconditions met (synthesis state ready, Init succeeded) | 47737.5083 | 10981.2448 |
| Outbound StartAnimation tag 1 | 47897.7524 | 160.2441 |
| Inbound AnimationStarted tag 1 | 47930.0449 | 32.2925 |
| Outbound EndAnimation | 52010.3019 | 4080.2570 |
| Inbound AnimationEnded tag 1 | 52262.0273 | 251.7254 |
| Action completion log | 52294.3831 | 32.3558 |
| MCP output | 52301.6092 | 7.2261 |

Total 15545.3457 ms. The first 10.981 s bounds queueing, any lazy provider/voice
initialization, asynchronous synthesis, the two 100 ms stop waits, and
scheduling/tick observation. The host provider is lazy and cached; this capture
does not timestamp its construction or establish whether an earlier internal
operation warmed it. This
capture has no per-phase synthesis timestamps, so splitting that interval into
exact load versus synthesis milliseconds would invent evidence. The next
160 ms includes direct audio-owner acquisition/configuration and animation
start scheduling. Those are host initialization steps, not a native fixed delay.
The robot start/end wire interval is 4331.9824 ms.

Reliable messages were de-duplicated with the connection sequence wrap rule.
Between request and completion the capture has 115 AudioSample (0x8E), 17
AudioSilence (0x8F), and one each Start (0x9B) / End (0x9A); CA/CB both name tag 1.
Audio samples run 48000.2087..51942.0913 ms; no BF buffer-overflow message occurs
in this interval. 115 chunks represent 3833.333 ms at the engine's 744 samples /
22320 Hz. This measures transport content, not an assertion that all chunks
contain audible speech. The host sent its end before the robot's completion;
this is unlike the later early-CB overflow investigated separately.

An offline run of the shipped Acapela library with javaGlue:true, Ryan,
text 'Hello! Look how tall I am!', rate 50, shaping 100, pitch 100 produced
83532 s16 samples at 22050 Hz (3788.2993 ms), speak result 0. Under concurrent
full-suite load, that probe took 19777 ms for synthesis and its initial voice
load took 1795 ms. These are a separate host performance observation, NOT
measured phases of the recorded run or native-phone timing requirements.
The probe also exercised rates 58/55/52/47/45 as exploratory measurements;
those randomized name-intent rates are not this sentence's production input.

Conclusion: the whole text reached the provider, an appropriate length of
streamed audio reached the connection, and the robot acknowledged the normal
end. The available capture contains no microphone recording or decoded
word-level observation. Whether every word was actually audible remains an
operator observation; neither SUCCESS nor this analysis proves it. Existing
M14-006 BLOCKED_EXTERNAL provenance is not upgraded by hardware or these probes.
No speech timing or audio behavior was changed.

## Face ID 1

MCP input 18 was at 166569.0774 ms, completion at 166596.5132 ms: 27.4358 ms,
not literal zero. Native Init uses a valid nonzero SmartFaceID's GetFace at
0x0054BD7C. Null returns through 0x0054BE6C. The constructor writes RequireFacePose (+0x193) = 0 at 0x0054B7B6. With it false,
0x0054BEB6..0x0054BEBC sets state 3 and returns SUCCESS from Init. CheckIfDone's
state 3 branch at 0x0054C606 finds no child, goes through 0x0054C622, and returns
SUCCESS at 0x0054C63C. It neither falls back to another cached face nor turns.
RequireFacePose true instead returns NO_FACE (0x0300000E); that is not the
app-default request. The stack's NativeLastFaceRunner already matches this.

The defect was the host explanation: 'Native face turn ended' hid that no target
pose existed. This diff retains native action/result and adds FacePoseAvailable
(nullable until Init evaluates it) to the host result and bridge report, plus an
explicit no-target detail. It records the actual Init conversion result, never a
pre-admission face lookup which could be stale while queued. No extra world
lookup or motor message is introduced. The four-position public record ctor and
deconstruction are preserved. Unknown ID still returns SUCCESS with completion
variant 6. Tests assert native success, no SetHeadAngle/SetBodyAngle, and bridge
facePoseAvailable=false plus the explanation.

## Fidelity boundary

No manifest, inventory, approval snapshot, PROJECT_STATE or SCOPE changes.
M10-003 / M13-014 / M8-015 remain IMPLEMENTATION_GAP; M14-002 remains EXACT_SOURCE for its
existing constructor-frame-count claim; M14-006 remains BLOCKED_EXTERNAL.
This is a bounded robot-run investigation and host reporting fix, not a claim
that these larger records or a hardware scope item are finished. Claude must
spot-read the cited native path before merging. No hardware bundle is staged.
