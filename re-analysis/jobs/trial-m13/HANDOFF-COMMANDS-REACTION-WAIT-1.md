# COMMANDS-REACTION-WAIT-1

- Main already includes fd46c879 and 9c023bda. The 15-second admission gate applies to the command and enrollment requests, including the initial face head setup and the retry. It checks the current reaction (0x005A19EC) and Robot+0x2C7 (0x005A2F60, read at 0x0053D94E).
- A terminal 0x02000000 paired with an entry in the synchronous E2G 0x88 stream inside the actual command window is recorded as OBSERVATION. The result names the reaction and keeps attempt 1 in the bundle. Attempt 2 uses the same gate and cannot retry again. Reaction motor preemption remains native behavior (0x005A3610–0x005A3682).
- Keep the cube out of view until turn-to-cube, then start near 45 degrees off-axis and bring the marker into view. The signed angle is sourced from TurnInPlaceAction::CheckIfDone 0x0054642C logs, with the raw line retained. On preemption the angle window ends at the reaction entry.
- The condensed replay uses the queue's 49,522 ms reaction and the 86.8 and -7.5 degree turn observations. It checks the cancellation grade, one retry, successful retry grade, and that earlier and later logs cannot be assigned to the interrupted turn. The full operator bundle was unavailable in this checkout.
- All 106 focused tests and the fidelity check passed. The no-robot command plan validated 22 record references.
