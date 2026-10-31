# CUBE-LIFT-WORKOUT-1 additional objective value

Read-only extractor supplement, 2026-10-30. Answers the builder's narrow question about the ordinary first workout's configured `PerformedStrongWorkout` objective. Original frozen artifacts remain unchanged; no code, official records, git, .NET or hardware changes.

Primary evidence is `resources/lib/armeabi-v7a/libcozmoEngine.so`, `BehaviorObjectiveFromString` 0x00769558. `known.py PerformedStrongWorkout` found no named function; `known.py BehaviorObjectiveFromString` identified the native function before tracing. Existing local disassembler read the function instructions and resolved its PC-relative string addresses from ELF bytes.

`PerformedStrongWorkout` is **0x13 (19)**. In the function's initializer-list construction, 0x00769780 loads the string displacement; 0x00769788 `add r1,pc` resolves to 0x00C19C83, whose NUL-terminated bytes spell `PerformedStrongWorkout`. 0x00769790 supplies string destination sp+0x130, 0x00769794 length 0x16, and 0x00769796 constructs that string. 0x007697A0 sets r0=0x13 and 0x007697A8 stores the associated value at sp+0x13C. 0x007699EA constructs the string-to-BehaviorObjective map from these 0x10-byte pairs; 0x00769A26..30 finds the supplied string and returns the mapped value.

The adjacent prior pair independently identifies `PerformedWorkout` at string address 0x00C19C72 and value 0x12 (0x0076976E..8C), consistent with the already checked unconditional EndIteration objective. This answer does not supply any other unmapped objective or change the existing objective-delivery frontier.
