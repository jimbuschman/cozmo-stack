# Q14-104/131: envelope local and allocated link defaults

Checkpoint689; guard reread before each step. Primary `20261010-sound-attachment-e-tail-stores-native.txt`, `20261010-sound-attachment-e-tail-inputs-native.txt`; same engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1. Three original raw candidates; existing EV14/OA16 ownership and exact control decisions retained.

9E57C8 writes0 at fp−34. Actual9E52F8 prologue sets fp=postpushSP+20; fp stays a stack frame base, so destination is a local, not node parent34. EV14's exact envelope completion marker/fill decisions remain unchanged; no control-stream arithmetic classified as PCM equivalence.

9E7C84/9E7C88: actual9E7BF4 calls allocation50, saves returnedr0 inr3, publishes it to incoming owner18; null sets rawreturn34 and skips initialization. Nonnull publishes same separate allocation to owner8, capacityC=4, then initializes four14-byte link records with next pointers at0/14/28/3C. These two candidates clear the third record'sC/10 fields, under the captured native address-not-zero condition. They do not store into owner34/38. OA16's parameter vector cleanup matches the link geometry; no all-owner alias absence or state-retirement acceptance is claimed.

out of scope: allocation/free-list mechanism; stop descendants after the separate allocation and record destination are established.

Original104/131 remain PARTIAL for actual admitted destinations and alias questions. Counts107 native+8 scope CHECKED/25 PARTIAL,140 AUDIBLE/0 INTERNAL. No production/fidelity/inventory/hardware/Q15 edits.
