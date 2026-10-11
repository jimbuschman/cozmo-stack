# Q14-040: exact singing reaction mask

| Obligation | Status | Remaining decision |
| --- | --- | --- |
| Q14-040 | PARTIAL | Selected action queue/behavior slot80, trigger mapping/animation admission-completion inputs; reaction manager's positive disable/start-switch recipient interfaces. The exact mask and local lock insertion gates are paid. |

Checkpoint634; decisions only, scope guard reread. Primary `20261010-sound-singing-reaction-mask-native.txt`; same engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1. AF633 gives actualarraycaller and selectedreactionmanager.

| Step | Primary | Exact decision / boundary |
| --- | --- | --- |
| RM634A actual21-pair mask |C6F590..5B9;5EEB5C | Exact21 rawbyte(enum,bool) pairs0..20 inenumorder. Bool1 onlyfor1,2,8,10,20; allother0. Not allreactionsdisabled. No enumname fromunreadmetadata needed tosettlewhichnumericinput changes. |
| RM634B physicalmanager iteration |5A2818..46;5A2942..68 | Physicalinorder managerreactiontree40 tosentinel44. Eachactualentrybyte10 selects mask[2*byte+1]; zero skips. Native ignores storedenum-byte half ofmask pair. Currentselectedname lookup occurs beforemaskgate; no bulkdisable ofnonselectedentries. |
| RM634C disable callback and lock order |5A2896..8CC | Forselectedentry, currentlockcount28==0 calls eachphysical12-byte triggerrecord's rawobject0 virtualslot1C withactualmanagerrobot4 andbool0. Existingnonemptylockset skips thesecallbacks. Aftercallbacks, ifnamedlock absent, callsAddDisableLockToTrigger(entry14,name,entryenum). Duplicateexistingnamedlock skipsinsertion. Manager iteration continues aftercallback effects throughnativephysicaltree successor. No newglobaltriggerobject-family census is claimed. |
| RM634D lock unique publication |GOT10447C0=5A05ED;5A0644..6C | Helper searches actualstringlocktree atinputC againstsentinel10. Existing returns0; missing emplacesexactname thenreturns1. It doesnotinvokeadmission/reactivation ortranslateinputenum intoanotherkey; enumparameteronlyfeedsdiagnostic beforethisbody. Genericstring/tree backing is excluded. |
| RM634E active matching reaction stop |5A28CC..2940 | Actualcallerstopflag1: ifmanagercurrentrecord1C.byte10 equalcurrentselectedenum, buildsnullbehavior/sharedref withreasonbyte16 thenSwitchToBehaviorBase beforeproceedingnexttreeentry. This positivecallee remains distinct inputinterface; do not assume itsfullstop/callback body fromname. |

out of scope: stringlock/tree backing and diagnostic vector allocation/free; exactnumeric mask, physicalorder, first-lock callback gates, uniquename publication and active-match switch inputs remainabove.

Counts102 native+8 scope CHECKED/30 PARTIAL;140 AUDIBLE/0 INTERNAL. Q15 remainsparked. No production/fidelity orhardwarechanges. Next actualqueue/slot80 andselectedtrigger/animation admission-completion joins; managerpositive reaction recipients retained asinterfaces.
