# Q14-104/131: independent queued records and sorted list fields

Checkpoint726; guard reread before each step. Primary evidence: queue-tail-fields-native.txt, queue-tail-inputs-native.txt and queue-tail-entries-native.txt (20261010-sound-attachment- prefix). Engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1.

| Original candidates | Destination and retained decision |
| --- | --- |
| 9AA450 / 9AA538 | Actual9AA3DC walks incoming owner10 list. List20 bit0 selects independent38 allocation branch9AA440 or9AA510. First productR4 receives original incomingr1 retainedR6 at34; second clears34 fromSL=0 on bit0-clear branch. Positive38 products, not parent-node stores. Preserve input bit choice and allocation-failure paths; alternate input list remains conditional. |
| 9AAA54 / 9AAA60 / 9AAB5C / 9AAB68 / 9AAB6C | Actual9AA9E4 incomingr0→R5 queue owner, readsfree-head38→IP, stores incoming fourth argument into payload2C and owner4C into payload10. IP stores key4 and payload8 before list insertion. Traversal compares unsigned incomingkeyR6 withentry4; BLO inserts before strictly greater key, equality walks onward, preserving equal-key order. Tail34/new head30 and prior-next are updated before popping free-head38 and incrementing44. When no free node, independentC allocation9AAB44 adds to38 then rejoins same insertion. These stores are active-tail/free-head fields of this local queue contract. Admitted outer receiver beyond supplied callers remains UNKNOWN; field similarity to AW72578 product is not itself a universal receiver proof. |

Exact ordering and key comparison stay in scope. out of scope: backing allocation body; no descent. Original parent/bus writer/alias proof remains PARTIAL. Counts107 native+8 scope CHECKED/25 PARTIAL,140 AUDIBLE/0 INTERNAL unchanged. Research only.
