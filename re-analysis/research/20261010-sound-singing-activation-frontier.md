# Q14-040: selected activation frontier

| Obligation | Status | Specific remaining step |
| --- | --- | --- |
| Q14-040 | PARTIAL | Selected trigger mapping and PlayAnimation admission/completion input; reaction mask/manager lock recipient; action queue result and actualbehavior virtualslot80. AF633 positively joins the wrappers below; no missingcaller means unreachable. |

Checkpoint633, decisions only, scope guard reread each step. Primary `20261010-sound-singing-activation-frontier-native.txt`; same engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1. AG631/CP632 hold exactsinging threechild inputs, failure policy, timeout andcompoundcadence.

| Step | Primary | Exact decision / boundary |
| --- | --- | --- |
| AF633A reaction lock actual input |5EEB46..60;GOT1045030=5BCE3D;5BCE3C..84 | Audio switch post precedes lock. Singing passesbehavior40 name andactualarray at ThumbADD5EEB5C+4+680A30= C6F590. Wrapper retrievesBehaviorManager frombehavior2C→44; concatenatesnativeprefix+name, callsDisableReactionsWithLock(manager,name,actualarray,1), theninsertsoriginalname intoownbehaviorA4 lockset. No defaultmask substitution. Mask words and manager recipient remain selectednextstep. |
| AF633B actual StartActing wrapper |5EEE82..8C;GOT1045060=5BDDD9;5BDDD8..DE68 | Singing passesactualcompound andcapturedresultfunctor. Wrapper copiesinputresultfunction intoadaptedcompletion function, then callsactualIBehavior StartActing via4B01C8. Returnvalue preserved overtemporarydestruction, no immediate callback orassumedacceptedqueue. |
| AF633C action admission local gates |5BDACC..DC12 | IfA0 nonzero: nonnullactiontailcallsactionvirtualdeleting slot4 andreturns0; nullactionreturns0. Otherwise requireA2 OR A1 nonzero; existingactingtag84 nonzero rejectswithwarning. Acceptedlocalgate copiescompletioncallback into88, storesincomingactiontag60 into84 BEFORE actualbehaviorvirtualslot80(1), thencallsActionList::QueueAction(robot250,mode0,actualaction,0). Queue result0 returns1; nonzero warns/returns0. Nativefailure doesnotclearstored84 here. ActualQueueAction/slot80 recipient stillopen, not repaired byplausiblecleanup. |
| AF633D animation Init concrete state/admission |543F20..414A | Init clears8A andhalfword88 beforeadmission. Cachedanimation90nonnullskipslookup; otherwise exactname special-case/lookup supplies90. Callsactualstreamer admission4AADE0 withloopcount84,byte8C andspecial-casebool; storesreturnlowbyte8B. Zero sets8A=1 andreturns3000001. Nonzero installsengine-gameCA/CB/D5 subscriptions androbot5F subscription, then rereads8B at544128: stillnonzero returns0, zero retains3000001. Exactselected admission result andnotificationcallback products remainopen. No treating successful Init as completed animation. |

out of scope: temporarystrings/functions and reactionlockset/tree storage backing; inputmask, lockrecipient, admission gates, tag publication, callbacks andqueue results are retained decisions.

Counts102 native+8 scope CHECKED/30 PARTIAL;140 AUDIBLE/0 INTERNAL. Next actualmask andlockrecipient, selectedqueue/slot80 andtrigger/animation admission interfaces. Q15 parked; no production/fidelity orhardwareacceptance changes.
