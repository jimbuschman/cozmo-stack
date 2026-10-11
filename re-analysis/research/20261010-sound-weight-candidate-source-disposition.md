# Q14-008: all weight-setter interior branch encodings attributed

Checkpoint795; queue5 guard reread before each step. Engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1. Decisions only; resolves776's29 retained concrete encodings, not a fresh caller or selector-family census.

`20261010-sound-weight-candidate-owner-entries-native.txt` establishes actual ARM entries, returns, out-of-line branches and adjacent literal islands. Nearby decompiler sizes fail to enclose several candidates and are not treated as primary function or mode evidence. `-candidate-source-boundaries.py/.json` follows primary local ARM instruction boundaries from those entries; conditional edges are conservative, calls remain interfaces, BX/PC writes/returns/UDF stop that path. Every one of28 containing code instructions has a finite saved predecessor chain to its decoded entry. No region-address-only or missing-caller argument is used.

| Actual containing ARM body | Upper-halfword encodings |
| --- | --- |
|A0785C|1|
|A07A50|1|
|A07B3C|1|
|A07C6C|6|
|A07DDC|3|
|A081A0|1|
|A0828C|1|
|A084D0|8|
|A08544|3|
|A0855C|3|

The29th candidateA07A16 is the upper half of exact ARM NOP wordE320F000 atA07A14. SourceA07A10 unconditionally branches backward toA07958; earlierA07A00 returns with its frame restored. The NOP precedes literalsA07A18..30 (several have positive PC loads), then distinct small methodsA07A34/44 and next frameA07A50. There is no normal fallthrough supplying a Thumb call or node/childID/weight. The raw word and preceding branch are individually recorded; this does not prove every hypothetical computed transfer into that padding absent.

Together with776's positively decoded ThumbVSTR at88CA1C, all30 finite immediate interior-entry encodings are now accounted for. No admitted weight-setter caller or runtime node/childID/weight is found. Existing WI609/CA153/PW152/TR18/RJ423 local mutation and authored weight controls remain positive; authored weights are not a runtime setter call.

Q14-008 stays PARTIAL for the original actual shipped setter-input or complete bounded call-edge exclusion proof. The finite interior encoding list is paid; indirect/relative/copied/alternate inputs remain UNKNOWN. Stop this lead; no global absence or runtime-unreachability claim.

out of scope: bitmap/playlist backing and excluded bit5 ping-pong. Coverage unchanged108 native+8 scope CHECKED/24 PARTIAL/0 NOT DONE;140 AUDIBLE/0 INTERNAL. Research only; no production, fidelity, inventory, hardware or Q15 changes.
