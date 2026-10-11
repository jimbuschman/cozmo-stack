# Q14-040: concrete singing action gates

| Obligation | Status | Remaining decision |
| --- | --- | --- |
| Q14-040 | PARTIAL | Actual trigger-group resolver/animation Init inputs; selected sequential compound append/ignore-policy/move-next products and behaviour lock/StartActing interfaces. The concrete60-second timeout and zero render-wait/start-delay products are paid below. |

Checkpoint631. Queue5 scope guard reread before every step; decisions only. Primary `20261010-sound-singing-action-gates-native.txt`; engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1.

| Step | Primary | Exact decision / boundary |
| --- | --- | --- |
| AG631A actual three child inputs |5EEDBC..5EEE62;GOT1041EA4=544229 | Singing constructs three actual TriggerAnimationAction products in order:202, configuredbehavior124,203. Each incoming loopcount r3=1; stackbool1,byte0,timeout42700000,finalbool0. Each append uses actualcompound slot20 with inputfailure-ignore0, not an assumed unconditional skip. |
| AG631B selected timeout product |544228..2D4;GOT1042740=543C09;543C08..CBE;GOT103EB28=10215B4 | Trigger ctor forwards incoming args to PlayAnimationAction ctor, which stores incoming timeout94. Loopcount is1 here; only loopcount0 AND timeout exactly60 substitutes7F7FFFFF, so singing retains exact60. Actualderived table10215BC published after base ctor. |
| AG631C actual virtual gates |10215D8..E8 relocatedslots;52B0BA..C0;545210..14 | Selected Init1C=54443C, CheckIfDone20=5441C0, start-delay24=52B0BA returns0, render-wait28=52B0BE returns0, timeout2C=545210 returnsfield94. No unread generic delay/render provider remains for these singing-created products. |
| AG631D timeout precedes Init/completion |540D4A..DAA;540E7C..EFE;540EEA..F08 | Timer F32 currentseconds; negative start74 records now. First compare now against F32(start74+selectedtimeout60); GE produces3000018 before Init/CheckIfDone. Failure callbacks traverse physicalnext4 and receive exactresult. Start-delay and render-wait gates are retained by native wrapper, but both selected providers return0 here. Unordered comparisons follow actual native flags, not a new finite clamp. |
| AG631E Init and retry order |540DAC..E7A;540F0C..F9C | Before selected Init succeeds, call1C; nonzero result then entersresult/retry logic without setting70. Successful0 sets70=1 before CheckIfDone20. Completion topbyte4 with retries8 nonzero decrementsretry, restores74=-1,70=0,18=2000001 and returnsrunning1000000. Other result propagates with callbacks. Actual CheckIfDone returns0 whenbyte89nonzero; otherwise3000001 ifbyte8Anonzero, else1000000. |
| AG631F trigger empty/resolution failures |54443C..498;5445F4..636 | Empty configuredgroupBC immediately returns300000C. Nonempty group callsactualresolver4AAE58; storesreturnedanimation78; emptyreturnedanimation leaves300000C. Nonempty invokesPlayAnimationAction::Init viaGOT1042758=543F21; returnedresult retained. The later animation-tag map and diagnostic event do not turn an earlier nonzeroInit into success; complete positive branch reaches0 at5445F0. |
| AG631G compound local result policy |54F70C..994;tablebytes54F802..806 | Derivedupdate24nonzero returns300001C. Childupdate occurs only aftercompoundstartdelayA0 gate. Childtopbyte0 movesnext;1 remainsrunning;2/3 runcompoundcallbacks then consultactualShouldIgnoreFailure;4 firstchecksRetriesRemain==1 and mayresetallchildren before running again, otherwise falls into2/3. Otherhighbytes return0. Ignore==1 callsMoveToNextAction; otherwiseStoreUnionAndDelete occurs BEFORE returningoriginalchildresult. Do not collapse these to any-failure-abort or automatic skip. |
| AG631H ignore lookup boundary |GOT1042AAC=54F171;54F170..1BC | Unsigned rawchildpointer key lookup incompound8C tree; absentreturns0. Presentcallsstoredboolfunction atentry18 withactualresult/childref. Actual singing append input0 is positive; its exactpublication/callback and MoveToNextAction still needjoins, not a newglobalcompoundfamily audit. |

out of scope: action diagnostic string/vector allocation/free and tree/list backing mechanics; failure values, action order, retry and scheduling gates remain exact above.

Counts102 native+8 scope CHECKED/30 PARTIAL;140 AUDIBLE/0 INTERNAL. No Q15 or production/fidelity changes. Continue actual resolver, compound append/policy/move-next and behaviour lock/start interfaces.
