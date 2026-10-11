# Q14-040: queue replacement and retirement

| Obligation | Status | Specific remaining decision |
| --- | --- | --- |
| Q14-040 | PARTIAL | Selected trigger mapping and animation admission/completion; positive reaction disable/start-switch recipients. Queue replacement controls are now read through their native completion/report interfaces. |

Checkpoint 636. Request: `requests/20261007-codex-queue-5.md`, Q14. Decisions only; scope guard reread before each step. Primary: `20261010-sound-singing-queue-retirement-native.txt`; engine SHA256 02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1.

| Step | Native evidence | Exact decision / boundary |
| --- | --- | --- |
| QR636A corrected tail recipient | 53E36E; Thumb 8CB48C / ARM 8CB490..494; literal 8CB498=FFBDF458; PLT4AA8F4; GOT1042588=53E16D | The actual tail target is QueueAtEnd, not QueueAtFront. ARM PC8CB49C + signed literal reaches4AA8F4. Corrects SQ635E and removes the unrelated QueueAtFront/Interrupt branch from this selected join. |
| QR636B Cancel state gate | GOT1042594=5409DD; 5409DC..A44 | If action18==2000001, return without mutation. Otherwise log then store2000000 to18. Cancel does not itself invoke action callbacks, destroy the action or synchronously run the replacement. |
| QR636C completion preparation | 540750..7C0 | Already prepared byte55 nonzero logs/returns. Otherwise invoke actual action virtualslotC into action1C completion storage, then set55=1 AFTER callback. Victim-specific completion payload is an existing action interface; this does not invent a census of every possible interrupted action class. |
| QR636D reentrant retirement guard | GOT1042558=53F9E5; 53F9E4..FA24 | Null referenced action returns0. Nonnull takes actual actiontag60 and inserts into queue10 retiring-tag set. Existing tag (inserted flag0) skips all preparation/destruction/list mutation/reporting and returns0. New tag selects entry and prepares completion before report snapshot or deleting the action. |
| QR636E snapshot before destruction | 53FA28..7C | HasExternalInterface must return exactly1. If true and action18!=3000009, snapshot GetRobotCompletedActionMessage into temporary storage and obtain external interface. Other cases suppress reporting. The actual deleting virtualslot4 runs next; referenced current/action pointer is then set0. No report reads the freed action. |
| QR636F unlink before report | 53FA7E..C6 | If iterator differs from list sentinel queue+4, unlink it, decrement pendingcountC, free node and publish successor to iterator. Only afterward, when a report interface was selected, construct/send the snapshot via its actualslot1C. Retiring tag stays present across this report callback; erase tag only afterward, then clear temporary completion/message storage. Return inserted flag normalized to1. |
| QR636G exact replacement order | SQ635E; 53E25A..2F6; 53E34E..370 | PendingcountC>0: retirement precedes retrybyte8=0 and physical front insertion of incoming compound. PendingcountC==0: Cancel current if nonnull, retirement, then QueueAtEnd. Actual caller ignores retirement's return; preserve this even when reentrant retirement returns0. This is not an inferred successful deletion guarantee. |
| QR636H tail publication | 53E16C..1EA | Nonnull incoming compound: retrybyte8 gets supplied0 before allocation, append physical tail node with rawcompound pointer8, update links, incrementcountC, return0. Null rejects1 with diagnostic path. Queue storage allocation/free is excluded; tail position, count and retry publication are retained exact decisions. |

out of scope: retiring-tag tree/list backing allocation, free and diagnostics; admission, cancel/retirement order, snapshot/report suppression, reentrant tag gate, publication and scheduling position are retained above.

This pays the selected queue-recipient question without treating another action's completion payload or the lower-layer external interface as a new sound obligation. Q14-040 still retains selected trigger/animation and positive reaction control inputs. Counts:102 native +8 scope CHECKED /30 PARTIAL /140;140 AUDIBLE /0 INTERNAL. Q15 remains parked.
