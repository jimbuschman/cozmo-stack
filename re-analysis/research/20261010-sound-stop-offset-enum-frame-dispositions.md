# Q14-119: enum initializer frame aliases and consumed literal words

Checkpoint744; request `requests/20261007-codex-queue-5.md`; guard reread before every step. Primary `20261010-sound-stop-offset-enum-stack-aliases-native.txt`, `-enum-initializer-entry-native.txt`, and `-thumb-owner-sample-native.txt`; symbol/branch witness `-thumb-address-owner-navigation.json` and `-enum-init-navigation.json`. Engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1.

| Candidate address | Actual R4 frame origin |
| --- | --- |
| 6C5C6A |6C5846 MOV R4,SP at actual initializer branch target6C5828 |
| 6CFF16 |6CFAFE MOV R4,SP at actual target6CFAE0 |
| 6DCA92 |6DC676 MOV R4,SP at actual target6DC658 |
| 6E1806 |6E1402 ADD R4,SP,8 at actual target6E13E4 |
| 6E7C8C |6E7868 MOV R4,SP |
| 6EEE08 |6EEA1E MOV R4,SP |
| 6F1ADA |6F1700 MOV R4,SP |
| 758C82 |75890E MOV R4,SP |
| 75A7FA |75A484 MOV R4,SP |
| 75E85C |75E4EA MOV R4,SP at actual target75E4CC |
| 765984 |765642 MOV R4,SP |
| 76700A |766CA2 MOV R4,SP |
| 7698D4 |769592 MOV R4,SP |
| 76A6DE |76A330 MOV R4,SP |
| 76B8E0 |76B532 MOV R4,SP at actual target76B514 |
| 777F82 |777BD4 MOV R4,SP |
| 783192 |782E20 MOV R4,SP |
| 795132 |794DC0 MOV R4,SP |
| 7CF4F4 |7CF17E MOV R4,SP |
| 7D1730 |7D13F2 MOV R4,SP |

These20 ADD baseR4+1F0 candidates form addresses into enum/string initializer FRAME records, rather than a PBI receiver. Candidate windows pair those addresses with directSP record stores and existing string-construction calls. For6E1806, retain the extraSP8 bias rather than calling every record an identical offset. AudioMetaData event/parameter/state FromString naming does not make their frame backing a live stop-offset field. Do not descend into their conversion or static-map/hash infrastructure.

The initial contiguous prologue captures include literal islands after unconditional initializer branches in six large functions. Those decoded literal words are not used as instructions. Fresh captures start at the actual branch destinations above; the source-backed SP origin is taken there. ELF method names are navigation support, not a substitute for these receiver-register instructions.

4D9848 likewise derives from actual4D92DC MOV R4,SP, and its same primary window pairs record pointer arithmetic with directSP1EC/1F4/1F8 stores. It is a local initializer record, not an unknown PBI product requiring new callee tracing.

88FFE4/88FFE8 are positively consumed inline literal words: actual88FFC4/FFC6 LDRs read those addresses, whose raw values007AE9EC/007AE9EE feed subsequent PC-relative data address construction. The function ends its nonreturning exception path at88FFDE BLX4A42D0, then padding/literals precede next entry88FFF0. They are not the apparent STRD writeback stores from linear navigation. No empty-disassembly absence argument.

This pays23 additional near-field navigation entries (20 enum frame addresses, one separate frame alias, two literals).29 principal ARM and32 Thumb entries are now attributed by SI741/MA742/TA743/this report;230 Thumb navigation entries remain (197 explicitSP/PC and33 other). These are navigation counts, not new obligations or whole-writer closure. Original119 stays PARTIAL for explicitly complete alias/input proof; no production/fidelity acceptance inferred.

out of scope: frame backing/string/static-map/hash infrastructure; stop there. Counts107 native+8 scope CHECKED/25 PARTIAL,140 AUDIBLE/0 INTERNAL unchanged. Research only.
