# VOICE-NAMED-REACTION-1

## Checked caller and child timing

- Shipped FacePositionUpdated selects AcknowledgeFace. Its BeginIteration chooses GetBestFaceToTrack, exits on ID zero, and constructs TurnTowardsFace with the selected ID, pi and sayName enabled (0x00602966–0x006029AE). The normal path selects AcknowledgeFaceNamed, AcknowledgeFaceUnnamed and three verification images (0x00602A7C–0x00602AA6). Its greeting callback can instead return NamedFaceInitialGreeting and trigger GreetingSayName (0x00602D8C–0x00602DDE). The existing AcknowledgeFace production adapter is still distinct from the native runner below.
- After the fine-tune child succeeds, the native runner resolves the verified face and creates Name_Normal SayText before the optional trigger callback (0x0054C552–0x0054C5B8). The selected canned trigger and mask are passed to SetAnimationTrigger (0x0054C5BE–0x0054C5CA, 0x0056147E–0x00561486). That setter leaves fit false. Native SayText moves synthesized data to WavePortal, constructs one lift-safe animation child for the trigger, sets Name switch and trait pitch, then updates the child (0x0069E9A8, 0x005616D6–0x005616FC, 0x00561728–0x00561754, 0x00561D4C–0x00561D50).
- The shared transition installs the child, stores state 3 and returns RUNNING (0x0054C5D2–0x0054C5D8, 0x0054C6B4–0x0054C6B8). The unnamed animation branch at 0x0054C706–0x0054C70A uses that same tail. NativeLastFaceRunner.cs now waits until the next action tick to update the installed child, at the boundary corresponding to 0x0054C606–0x0054C60C. The child result still gates needs action 0x29 before setting turned on the current verified face (0x0054C606–0x0054C638).

## Authored audio boundary

- The twelve AcknowledgeFaceNamed group variants in the shipped assets each contain BF0BACE1 alongside two to five other audio keys. NamedFaceInitialGreeting also uses several audio keys. The alternate named PickupHelper/PopAWheelie clips have other audio keys and no BF0BACE1. A named trigger alone therefore does not establish a speech post.
- One AcknowledgeFaceNamed base clip starts event 9FBACE5F at 660 ms, stops its loop with 6F1E60CF at 1320 ms, then posts speech at 1353 ms. Local English Cozmo.bnk HIRC3 actions 0x259643B0 and 0x0B3146D4 act on the same HIRC5 node 0x2E52CADB. The Stop event also plays 0x2BBAA520. The shipped asset timings and bank references were read locally.
- Native RobotAudioAnimation chooses all references up front (0x0059687E–0x00596986), dispatches relative to the first audio key (0x00597F12–0x00597F8E), and posts each to the same Wwise owner and robot game object with its own callback and playing-ID volume (0x0059818C–0x005982A0). The existing single-speech session and ordinary animation PCM use distinct owners. The named branch remains visibly withheld, with a diagnostic naming the shared authored-audio gap.

## MISSING

- The live AcknowledgeFace native action binding and one persistent multi-event Wwise/robot audio recipient remain open. The latter must cover the authored media/stream reads, per-playing callback ledger, stop and loop lifetime, retained state, event timing and one-shot WavePortal data ownership. The isolated Name speech path does not establish those effects. No authored events were changed, no resources were uploaded, and no robot hardware or fidelity status was changed.

## Files and verification

- NativeLastFaceRunner.cs adds the next-tick boundary and makes child replacement tolerate a synchronous host diagnostic during Prep. The no-trigger Name path and the canned authored-audio path report their separate unjoined recipients.
- PutDownBlockNativeTests.cs uses the actual ActionList and fake robot. The first case checks that installing the unnamed animation child does not run it before the next tick, then observes source failure with no needs or turned update. The successful case uses the shipped animation and its robot completion, and observes needs 0x29 while the verified face is still unturned. The cancellation case supplies a synchronous per-runner diagnostic seam during old-child Prep, exercising cleanup without claiming production log reentry through the deferred dispatcher.
- The affected face/speech/emulator suite passed 152/152, including unchanged Acapela, native voice, OKAO, Wwise host and emulation safety tests. Independent source review passed for the bounded timing correction.

Reader used requested gpt-6.1-sol medium. Builder and reviewer used configured defaults. Actual backends unobserved.
Independent manager rerun passed 78/78 with zero skips. Batch full gate passed at e3bed520 with 7890/7890 and zero skips. READY FOR CLAUDE for the bounded slice.
