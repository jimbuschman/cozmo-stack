# Q14-104/131: owned bus lists and registration records

Checkpoint693; guard reread before each step. Primary PBI-registry stores/inputs and owned-list-prefix native companions; engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1. Existing PB6/NREG1 actual fields retained. Eight original candidates.

A0242C/A02480 use saved r5 loaded from owner1E8, and A02C8C uses r1 from same owner1E8 atA02B1C. Known PB6 identifies this owned runtime bus's active/free link lists2C/30/34 and count40: pop freehead34 to append a link, or unlink first owner match then push link onto freehead34. These writes update freehead, not ordinary bank-node parent34. No additional runtime-bus alias absence or callback closure inferred; PB6's list/idle/release order remains exact.

A03180/A031A4/A031B0/A031B4 target actual A03108 separately allocated50-byte registration record r4 (allocation result capturedA03144). Zero38/34 defaults precede raw descriptor20/1C copies to38/34. NREG1 owns exact allocation/default/descriptor/refcount publication order. No input descriptor is overwritten and no node ancestry is created by these four stores.

A04284 actual A0423C saves its second stackargument inr4 and writes descriptor24 there at34 after copying the supplied record payload. **That output destination remains UNKNOWN for attachment alias proof.** No output size, stack-argument placement or record copying proves caller destination harmless/unreachable. Immediate dual-alignment ARM/Thumb branch navigation (`20261010-sound-attachment-registry-output-callers.py/.json`) finds no encoded caller; this does not establish absence, indirect/computed inputs or unreachable output. Next bounded decision step: establish admitted A0423C output pointers before excluding this write.

out of scope: allocation and link-storage mechanisms; stop descendants. Registration callbacks and bus idle/ownership decisions remain exact existing rows.

Original104/131 remain PARTIAL for admitted destinations and alias questions. Counts107 native+8 scope CHECKED/25 PARTIAL,140 AUDIBLE/0 INTERNAL. No production/fidelity/inventory/hardware/Q15 edits.
