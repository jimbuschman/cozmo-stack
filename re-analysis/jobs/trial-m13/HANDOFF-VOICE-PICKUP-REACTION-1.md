# VOICE-PICKUP-REACTION-1

## Source path

- The shipped RobotPickedUp registration enters ReactToPickup on InAir (0x60DDCE). Init resets the retry scale and admits a retained 0.5-second WaitAction with a resultless StartAnim callback (0x607750–0x607786). The reaction's Update checks the outstanding action before off-treads, contacts, retry time and cliff raw data (0x607BA8–0x607CC4). The base stop retires the retained action with its callback suppressed (0x607D9C, 0x5BD126).
- StartAnim checks actual carrying and detaches with forget enabled before evaluating the hard spark and face observations (0x607838–0x607864). The time window uses the last processed image, subtracts 500 ms with a floor of zero, and includes the cutoff (0x60786A–0x607880, 0x516EC0, 0x4F5A1E–0x4F5A4E). The query keeps current-root observations and signed IDs, including session IDs. The non-hard branch scans in ascending signed order and takes the first nonempty stored name (0x607884–0x6078C0).
- A named response constructs SayTextAction with Name_Normal intent 3, then sets AcknowledgeFaceNamed and body mask 4, and starts it with an empty callback (0x607A00–0x607A2C). The eager duration and pitch draws and provider request precede admission and the retry draw (0x561100–0x5611DC, 0x607AAE). The setter retains mask 4 for the lift-safe child. That child adds lift only when carrying on treads (0x56147E–0x561486, 0x5616D6–0x5616FC, 0x544714–0x544732). Style and pitch setup precede its playback (0x561728–0x561754).
- The unnamed response starts the ordinary AcknowledgeFaceUnnamed action with one loop, interrupt enabled, body mask 4, and a 60-second timeout (0x607A34–0x607A66). The named and unnamed branches preserve the authored clip and its existing mood-sensitive animation group selection. Neither branch adds a mood event or SayName objective.
- Hard spark or no observed faces reaches the existing pet or whiteboard branches (0x6078E4–0x6079E8). The common retry tail follows StartActing even when admission declines. It draws from [3 × scale, 6 × scale], narrows to float for the deadline, and increments the double scale by the promoted float 0.33 (0x607A8C–0x607AE0).
- Speech retirement clears pending provider work before child retirement (0x5613D8–0x561436). The retained reaction watcher suppresses an ending owner's callback and releases its own action and track acquisitions.

## Files and tests

- `CozmoRobot.cs` and `BehaviorManager.cs` bind the shipped reaction to the original robot during startup and late loading. A missing native binding withholds the reaction with a diagnostic. The standalone construction keeps its existing bindings.
- `CliffPickupBehaviors.cs` retains the wait, snapshots timestamp and current-root face state in one mutation scope, and rechecks the reaction generation before action admission and retry. `SayTextAction.cs` carries the named mask into the existing shared speech path. `TriggerLiftSafeAnimationAction.cs` scopes the track lock and post-log event delivery to the admitted robot.
- `PickupReactionVoiceTests.cs` runs the shipped reaction map with fake firmware and a real processed camera frame. It supplies additional native face observations to exercise signed ordering, the inclusive cutoff and old-root rejection. It checks carry detachment before RNG, hard and soft spark choices, and reentrant cancellation during wait, construction and active audio.
- The named and unnamed playback cases load the shipped assets, feed animation acknowledgements, and check non-silent robot audio, child-tag body-track acquisition and release, and the absence of a SayName objective. The affected run passed 97/97, including existing voice, face and emulator oracle tests. The independent manager run passed 48/48 across pickup reaction, cube pickup, Meet admission and fidelity checks.

## MISSING

- Physical appearance and output scheduling on a real robot require operator evidence.
- Direct coverage of differing raw versus processed image stamps and replacement of the whole robot during this reaction is pending. The tests cover stale roots and same-owner cancellation.
- Calibration telemetry, off-treads classification and the existing pet/hiccup input limitations retain their separate scope.

## Records

- M7-015, M7-019 and M7-021 still cover broader trigger, reaction and calibration obligations. This job addresses the pickup reaction's face branch.
- M14-009 supplies the current-root face query. M14-006 supplies the authored speech recipient, with the new caller's body mask carried through the lift-safe child. Other speech callers and audio boundaries retain their existing scope.
- M12-006 and M12-008 supply retained carry and detach. M15-059 supplies the hard-spark fields. This job uses those recipients under the admitted owner.
- M8-001, M8-015, M8-020 and M8-021 supply the retained action and callback lifecycle. M5 and M6 supply the selected authored clip and audio path. Their remaining obligations stay separate.
