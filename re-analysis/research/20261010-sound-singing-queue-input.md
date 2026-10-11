# Q14-040: actual singing queue and acting-state input

| Obligation | Status | Specific remaining step |
| --- | --- | --- |
| Q14-040 | PARTIAL | QueueNow's actual current-action cancel/delete and QueueAtFront recipient; selected trigger mapping/animation admission-completion; positive reaction disable/start-switch recipients. Actualslot80 and duplicate/clearing rejection are paid. |

Checkpoint635, decisions only; scope guard reread. Primary `20261010-sound-singing-queue-input-native.txt`, same engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1.

| Step | Primary | Exact decision / boundary |
| --- | --- | --- |
| SQ635A actualbehavior80 product |5EE8DC..912;GOT103F478=102A328;slot102A3B0=5BF045;5BF044..4A | Singing ctor publishes102A330. Selectedacting-state virtualslot80 ignores incomingbool andstores0 tobehavior104. It doesnotclearactualactingtag84 orconditionallyinvokeanotherbehavior. |
| SQ635B actual QueueAction mode0 interface |53D93C..DA40;AF633C | Actualinputcompoundnonnull,mode0,retries0. Publicqueue firsttests robot2C7/tagrange andactionstate18==3000006; nativeinvalid-state branchesreject. Admittedmode0 reachesQueueActionNow,notappend-end. Othermodebranchesarenotselected here. No missingcaller/reachabilityclaim. |
| SQ635C clearing/duplicate |53DCE4..DD92 | ListclearingbyteC nonzero: nonnullincomingaction PrepForCompletion thenactualdeletingvirtual4,return1. Otherwise traverseallphysicalqueues/childlist rawpointers; incomingpointeralreadyqueuedreturns1 afterwarning; no tag-onlyequality. No matchingpointer returns0. This gate precedesselectedqueue mutation. |
| SQ635D actual queue0 selection |53DCA0..DCDE;GOT1042578=53E24D | Duplicate/clearingnonzero returns1. Else selects/emplacessignedkey0 actionqueue andcallsactualQueueNow(queueentry14,incomingcompound,retrybyte0). Returnedvaluepreserved; generictreeallocationexcluded butkey/recipient exact. |
| SQ635E QueueNow local branches |53E24C..370 | Nullactionreject1. Nonnull withpendingcountC>0 callsDeleteActionAndIter thenstoresretrybyte8 andinsertsincomingnode atphysicalfront, incrementscount,returns0. Withpendingcount0, current0nonnull Cancel first,thenDeleteActionAndIter andtailQueueAtFront. Exactcancel/delete/front products remainpositivecalleejoinstoberead; noassumedunconditionalqueue success. |

out of scope: actionqueue tree/list backing allocation/free; key0 selection, admission/rejection, cancel/deletion order, retrybyte andfront publicationremainexactabove.

Counts102 native+8 scope CHECKED/30 PARTIAL;140 AUDIBLE/0 INTERNAL. Q15 parked. Continue selectedQueueNow recipients, trigger/animation admission-completion andpositive reaction recipients. No production/fidelity orhardwarechanges.

Correction at checkpoint636: SQ635E tail recipient is QueueAtEnd (8CB48C→4AA8F4→53E16C), not QueueAtFront. QR636 supplies actual cancellation/retirement and append-tail contracts; the earlier frontier is historical.
