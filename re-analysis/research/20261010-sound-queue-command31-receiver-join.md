# Q14-104/131: command 31 queue receiver and remaining admission boundary

Checkpoint752; request `requests/20261007-codex-queue-5.md`, scope guard reread before every step. Primary engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1. Evidence: `20261010-sound-queue-command-receiver-prologue-native.txt`, `-receiver-entry-native.txt`, `-receiver-restore-native.txt`, `-receiver-callers-native.txt`, `-outer-receiver-native.txt`, `-fixed-production-native.txt`, and `20261010-sound-queue-command31-producer-native.txt`. Navigation scripts/JSON are leads, not indirect-call absence proofs.

| Decision step | Source and disposition |
| --- | --- |
| Audio worker supplies current queue | Actual worker entryA4087C: A40894 loads5FF810; A408B8+8+5FF810=10400D0; A408BC loads current108D870 queue; A408C0 calls9AF8A8. This is a positive supplied receiver, without assuming the singleton never changes. |
| Consumer receives that queue | 9AF8A8 saves incomingR0 asR4 at9AF8AC. 9AF9F8 suppliesR4 asR0, R1=0, R2=local output byte;9AFA08 calls9ADFD8. Actual9ADFD8 retains incomingR0 inR8 at9ADFEC;9AE06C copiesR8 toR4. |
| Command31 dispatch | 9AE0A8 reads record.kind U16 at+2;9AE0B0 dispatches kinds<=37hex. Index31decimal/1Fhex branch9AE134 goes9AEA30: R0=R4, R1=[recordR5+4],9AEA38 calls9ADD94. Thus the supplied queue receiver is paid conditional on this record being consumed; actual command admission remains separate. |
| Temporary R4 reuse | 9AE6E8 saves the queueR4 atSP40 before9AE6F8 replacesR4 withR3;9AE7D8 restores it. Do not claim R4 is unchanged throughout the consumer or turn a regional writer scan into universal control-flow proof. |
| Producer-side consumer call | 9AF778 saves incomingqueueR0 inR4 at9AF78C and kindR1 inSB at9AF788;9AF840 restoresR0=R4 before9AF844 calls9ADFD8 withR1=1/local output byte. Actual99F20C supplies fixed108D870 to9AF778 for kindDhex. This binds another receiver path, without substituting kindD for kind1F. |
| Actual kind1F producer | 9A60B8 returns immediately on arg1==0. Otherwise it retains arg1/2/3 unchanged;9A60D8 setsR1=1Fhex,9A60DC+8+6E7784=108D868,9A60E4 loads fixed8 queue;9A60E8 calls9AF778. The returned record receives unchanged args at4/8/C at9A60F4/F8/FC, before publication barrier/pending-count update. The queue is reloaded after the call. |
| Record kind publication | On successful record allocation,9AF888 writes retainedSB as U16 atrecord+2;9AF88C writes actual size atrecord+0. This positively joins producer kind1F to consumer index1F; it is not a nearby error-return constant. |
| Still open | Actual runtime admission of9A60B8 with nonzero arg1, its payload/control inputs, alternate9AF778/9AF8A8 products and global aliases remain UNKNOWN. Specific immediate-entry navigation `20261010-sound-queue-command31-entry-callers.json` has no candidate; that is not an unreachable or computed-call absence proof. Stop this admission lead at UNKNOWN. |

The false Thumb9AE496 candidate lies inside actual ARM LDR9AE494, not a call. The 103-call navigation to9AF778 was used only to locate the concrete kind1F producer and a known fixed-input witness; those are not 103 new obligations or a completed caller census.

out of scope: backing allocation at9A983C and free-list/storage arithmetic; do not trace the accidentally captured backing body. Only caller-side kind/size publication is used here.

This narrows CQP751's retainedR4 boundary at9AEA38 to conditional known queue products and the named actual-admission gap. It does not settle arbitrary supplied controls or the original all-writer/alias claim.104/131 stay PARTIAL. Counts107 native+8 scope CHECKED/25 PARTIAL;140 AUDIBLE/0 INTERNAL unchanged. Research only; no production or fidelity status changes.
