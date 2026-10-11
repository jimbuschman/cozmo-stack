# Q14-104/131: threaded I/O records and transition coefficients

Checkpoint704; guard reread before each step. Same native companions as703, engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1. Seven original candidates; existing SL45/TT1/TT4–6/TT22/DG7 reused, no fresh transition family.

| Candidates | Actual destination and boundary |
| --- | --- |
| A35404/A35410/A35430 | SL45 actualA3538C allocates208-byte threaded-manager backingA353C4, result r0→r3 and published ownerBC before null check. A35404 is post-index STR: it writes offset0 then advancesr2 by38, NOT a store at offset38. A35410 addresses second34-stride record's field4; A35430 addresses secondrecord.next0. out of scope: I/O scheduler record pool/free links; stop descendants. |
| A35870/A358E4 | TT1 actualA35838 transition constructor and TT22 actualA358B8 reset clear coefficient38. Positive TT1 creator separately allocates3C before ctor/init/publication. Reset preserves subscriber backing; no node routing attachment is assigned. |
| A35964/A35F84 | DG7/TT6 admitted retarget and TT4–6 initializer save transition r0→r4. Coefficient flag34bit2 and signed duration gates control exact raw expf result publication38. Existing ordered binary32 expression and global quantum dependency retained; shared timing/input writer closure remains open. This is a control coefficient that changes callback values/timing, NOT per-sample DSP eligible for blanket exclusion. |

The raw immediate navigation list includes addressing writeback; effective destination is checked against instructions before attribution. No all-caller/indirect alias absence inferred. out of scope: phone libm rounding only at imported expf boundary; shipped coefficient gate/constants/order/store stay exact.

Original104/131 remain PARTIAL; counts107 native+8 scope CHECKED/25 PARTIAL,140 AUDIBLE/0 INTERNAL. No production/fidelity/inventory/hardware/Q15 change.
