# Q14-008: runtime weight setter interior-entry disposition

Checkpoint776; scope guard reread. Same engine hash; primary `20261010-sound-weight-setter-interior-boundaries-native.txt`, existing WI609 full body. Reproducible `-interior-callers.py/.json` targets external immediate branches intoA08024..A080CC, not just the entry. This addresses the original concrete runtime node/childID/weight input question.

Thirty encoding candidates:one ARM and29 Thumb. The apparent ARM88CA1C ->A08058 is actual four-byte ThumbVSTR S0,[SP14], corroborated by its neighboring VFP instructions. It supplies no weight-setter arguments. All29 Thumb encodings lie above95E540; no below-boundary actual Thumb supplier is found. Their location/encoding count does not prove all exact code boundaries, computed or copied calls absent.

ActualA08024 PUSH owns its frame; both exitsA08044/A080C8 POP PC after releasingCbytes. PredecessorA08018 unconditionally branches backward toA07FA4;A0801C/20 are its literal island before the setter PUSH. A080CC begins separate code after the final return. No ordinary preceding fallthrough or positive external-to-body input is established.

WI609/CA153/PW152 actual selector lookup/query/update/total and reset decisions remain paid. Supplied runtime node/childID/weight admission toA08024 is UNKNOWN. Stop this input lead; neither authored RJ423 weights nor the absence of a positive encoded caller proves runtime unreachability.008 PARTIAL;107 native+8 scope CHECKED/25 PARTIAL;140 AUDIBLE/0 INTERNAL unchanged. Research only.

out of scope: bitmap/playlist backing and excluded bit5 ping-pong; no new selector or DSP family is extracted.
