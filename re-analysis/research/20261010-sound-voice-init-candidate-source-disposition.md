# Q14-033: all128 remaining immediate-call encodings attributed

Checkpoint794; queue5 guard reread before each step; decisions only. Engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1. Answers only793's remaining128 concrete encodings; no new function family or per-sample DSP descent.

The ELF has no ARM/Thumb mapping symbols. A source address above95E540 is not enough to reject a candidate. Instead `20261010-sound-voice-init-candidate-owner-entries-native.txt` reopens actual entries, prior returns/literals and successive body boundaries. The decompiler's nearest indexed functionA53EA8 does not enclose the six543xx/544xx candidates; its stale nearest-start attribution is not used. Actual bodiesA542C8 andA543A0 are recovered from primary return/literal/new-entry sequences. Their backing/free callees stop at interfaces.

`-candidate-source-boundaries.py/.json` decodes primary ARM bytes from those entries, following local immediate branch edges and conservative conditional fallthroughs. Calls remain interface instructions; callees are not followed. BX/PC writes/returns and UDF stop that path; no normal fallthrough through a trap into its literal island. Every candidate's containing instruction has a stored finite predecessor chain back to its decoded entry. The complete local address/bytes/instruction/edge witnesses are in JSON and `-native.txt`. These establish instruction boundaries, not successful runtime predicates or shipped input reachability.

| Containing ARM body entry | Rejected Thumb upper-halfword encodings |
| --- | --- |
|A542C8|2|
|A543A0|4|
|A54480|3|
|A544BC|10|
|A545F0|5|
|A54650|23|
|A548B8|1|
|A548C0|7|
|A5495C|3|
|A549A0|5|
|A54F1C|65|

All128 are second halfwords of these positively linked ARM instructions. None supplies a Thumb branch at an actual source instruction boundary. Together with793's ten individually attributed false encodings and four real ARM entry calls, every one of the142 finite candidates is now accounted for:four positive direct callers,138 wrong-mode/inside-instruction encodings. The source-boundary correction did not remove the fourth callerA41C68; its complete PT540C handoff/attachment/Init/stop/start gate remains positively joined alongside VI523A's three sites.

Q14-033 remains PARTIAL for the original computed/indirect/re-init input proof and liveE0/E4 writer-alias proof. This resolves the explicit128-candidate remainder; it does not prove every alternate/interior/computed/copy transfer absent, introduce a universal foreign-pointer model, or certify initialization always succeeds. No zero/default/instruction-region argument is used as runtime unreachability.

out of scope: backing/I-O, excluded positioning/source branches and per-sample DSP. Coverage unchanged108 native+8 scope CHECKED/24 PARTIAL/0 NOT DONE;140 AUDIBLE/0 INTERNAL. Research only; no production, fidelity, inventory, hardware or Q15 changes.
