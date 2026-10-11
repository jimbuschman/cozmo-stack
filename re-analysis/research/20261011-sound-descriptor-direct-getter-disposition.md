# Q14-024 direct getter input frontier, checkpoint867

Scope guard reread before each step. Original actual shared descriptor ID/field recipient remains unpaid. A direct getter call could select that descriptor, so this four-entry check qualifies. It is distinct from841's provider calls and842/861/863/864's slot instruction forms; none of those paid searches or recipient bodies is repeated.

`-navigation.py/.json` reuses841's immediate ARM/Thumb branch decoder (SHA256cc16862765c8337dd7f86682fe0ce5d5cf7a9954f56ccd6907152633ce9a4b55), targeting only9A6CDC/9A6CE4/A3B9AC/A3BB94 and their aligned raw pointer words. Primary `-native.txt`, unchanged engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1, checks all three branch and all three pointer candidates.

| Candidate | Primary disposition |
| --- | --- |
|9A6CE0/9A6CE8 ARM tails | Existing exact wrappers9A6CDC/9A6CE4 first MOV R0,R1, then B A3BB94/A3B9AC. They pass the original ID argument, do not fix it or consume a returned field, and are already SI562's interfaces. |
|9A6846 apparent Thumb B | Actual ARM entry9A6840 PUSH followed by9A6844 SUB SP,SP,28; the candidate is the upper halfword of that ARM stack instruction. No getter edge. |
|101BFEC/101BFF0 raw words | Existing101BFA0 main table slots4C/50 point9A6CE4/9A6CDC, respectively. Pointer publication is not a witnessed caller/ID/field consumer. |
|5EE7CC raw A3BB94 | Actual Thumb5EE74A LDR R1,[PC,80] reads this pool word;5EE750 ADD R1,PC uses alignedPC5EE754, then5EE754 ADD R1,8 gives102A2F0.5EE758 stores that table pointer atSP38, not a getter target. Raw table words102A2E8=0/2EC=102A314 precede102A2F0's005C0E57 method and subsequent Thumb entries. The apparent absolute getter address is a relative table displacement. No callback/body descent follows this rejected pointer lead. |

No new direct getter caller or descriptor ID/field consumption is established. This covers the specified immediate branch forms and aligned exact raw words, not computed/relative/copied pointers, different entries, indirect products, foreign mutations or all runtime inputs. Stop these six paid candidates; no new family follows the unrelated table. Timing/control decisions remain in scope; this report makes no completion or guard-exemption claim about its unrelated methods.

Q14-024 remains PARTIAL, AUDIBLE, for the actual shared-context4C/50 getter ID and descriptor-field decision recipient or complete supported-input exclusion. out of scope: registry/hash/backing and per-sample DSP implementations.117 CHECKED (109 native+8 scope)/23 PARTIAL/0 NOT DONE;140 AUDIBLE/0 INTERNAL. Research only; no production, fidelity, inventory, PROJECT_STATE, hardware or Q15 change.
