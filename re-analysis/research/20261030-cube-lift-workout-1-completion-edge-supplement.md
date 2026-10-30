# CUBE-LIFT-WORKOUT-1 CompleteCurrentWorkout edge contract

Read-only extractor supplement, 2026-10-30. Answers manager source question 6 about the existing public/canonical completion path. Original frozen read and prior supplements remain unchanged; no code, tests, official records, git, .NET or hardware changes.

`known.py 0x00573DEC` was run before a new explicit disassembly of 0x00573DE8..0x00573E30 in `resources/lib/armeabi-v7a/libcozmoEngine.so`. The complete `CompleteCurrentWorkout` body is 0x00573DEC..0x00573E30. Source facts:

| Input/step | Instructions | Observable/downstream result |
|---|---|---|
| Current equals end | 0x00573DF0 loads vector end (+4); 0x00573DF2 loads current (+0C); compare at DF4 and conditional return at DF8. | Defined immediate no-op. No clock/event/advance or native completed-count increment. A freshly constructed empty component has both pointers zero and reaches this branch. |
| Null current differs from nonempty end | No null check follows equality. 0x00573DFC reads byte of current+0x30. | Null dereference, not a defined alternate completion branch. The retained prefix after a failed initial load can leave current zero with nonempty vector, as the parser supplement established; callers must not treat failed initialization as a valid current input. If encountered, the standing M1-034 rule permits the smallest skip/log; this does not establish a successful completion. |
| Empty completion event | 0x00573DFC..0x00573E08 derives std::string length; 0x00573E0A jumps to E24 when zero. | No MoodManager lookup, clock query or emotion event. It still proceeds to normal last-entry comparison/advance. |
| Nonempty event | 0x00573E0C..0E gets robot and canonical MoodManager; E12 calls GetCurrentTimeInSeconds; E16 sets time argument; E1A sets current event-string pointer; E1C calls TriggerEmotionEvent. | Event receives native clock and occurs before advancement. No native event-result gate is present. Host missing recipient/input is not a native null branch; failing clearly before advancement is valid host error handling. |
| State after event | 0x00573E20 reloads end; E22 reloads current. | The advancement uses component state after the event returns. Do not snapshot and overwrite the old index across an outward callback. |
| Last versus next | E24 subtracts 0x40 from end; E26 compares current; E2A adds 0x40 only when different; E2C stores only when different. | Last repeats; any earlier current advances once. Empty-event path uses the end/current already loaded at entry; nonempty path uses the post-event reload. |

There is no native completed-workout counter mutation in this full body. A host diagnostic count may be implemented independently, but must not be mistaken for a sourced native counter, drive another native state decision, or report successful completion on the defined no-op/missing-input paths.

This answer does not declare the public method excluded, close M13-010 or claim the complete MoodManager recipient. It supplies the narrow source edge contract needed to compare the existing public method and guarded canonical overload.
