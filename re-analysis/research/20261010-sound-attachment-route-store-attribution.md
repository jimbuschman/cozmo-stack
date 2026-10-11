# Q14-104/131: send records and device publication

Checkpoint685; scope guard reread before each step. Primary `20261010-sound-attachment-next-member-stores-native.txt`, `20261010-sound-attachment-route-store-inputs-native.txt`; engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1. Reuses existing SR287/VP4/VP11 actual recipient and device-list publication; five original raw candidates.

9D4318/9D4328 and9D447C/9D448C belong to9D4228's second argument r1: caller VP4/VP11 supplies voice+2C, not a bank node. These are the third14-byte send record's offsetsC/10 (absolute34/38). Its raw input ID at scratch18 and kind at20 are copied without conversion into those fields; selected branches separately retain gain/current-versus-zero initialization. Actual entry retains incomingr1 in place; local old-positive-gain collection uses it as a14-byte stride array. This does not prove all alternative caller aliases absent, nor whole routing closure.

9D4734 receives its base r3 by loading GOT10400B8 at9D46FC (PC+8+66B9B4), actual word108DAFC, then head+8 and next4. It compares record10/14 with the incoming two-word key and on first exact match stores raw incomingr2 to record34 before tailA59390; missing returns. Existing DA498E/SR287 establishes the separately published device list. No input node parent is used as the destination, and no new device-class name is inferred from proximity. A59390 remains its own decision dependency; this attribution does not descend into it or claim the update has no audible effect.

Original104/131 stay PARTIAL for admitted destinations and alias questions; counts107 native+8 scope CHECKED/25 PARTIAL,140 AUDIBLE/0 INTERNAL. No production/fidelity/inventory/hardware/Q15 edits.
