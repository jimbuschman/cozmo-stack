# Q14 rate/frame helper post-call register frontiers

Checkpoint816; scope guard reread before every step. Only the 11 calls / 12 existing register observations from814 are followed. This is a register-use witness, not a numerical arithmetic reconstruction or full engine alias proof.

The reproducible -registers.py/.json/.txt starts at each existing call's next instruction, stops at definitions, returns or later calls, and retains each instruction's bytes and register-access metadata. Its local 256-byte bound is reopened only at four source-positive branch-entry slices. Conditional paths retain consistent NZCV predicates; VMRS resets the possible predicate states without interpreting floating-point arithmetic.

All four previously bounded branch destinations now have source-backed definitions before any old-register read:9AFAE0 defines R2;9AF9E0 and9FD890 define R3;9FD790 MOVNE /9FD794 MOVEQ define R3 on complementary predicates with unchanged flags. There is no path that skips both conditional definitions. The complete reopened999018 leaf also defines incoming R3 at999018 before reading its replacement at99901C;thus99CCB4's retained R3 is not consumed there.

The remaining three call frontiers are9AF9AC andA58DBC to the already identified UIDIV4BE310, and9EBEAC to4A6694. The latter's exact three-word linkage computes GOT1040F68, whose ELF relocation is __aeabi_uidivmod (undefined in the engine;raw initial resolver4A4010 is not a runtime provider address). This fourth interface is reached by an existing positive frontier, not introduced speculatively. Formal two-scalar ABI information alone does not prove selected native-provider memory effects or incoming-register non-use. Those source prefixes remain the specific next step.

No possible retained-register read is found before the stated local definitions/returns/call frontiers. This does not prove global mutation absence, which runtime provider binds, actual denominator/64-bit input admission, or original all-writer/alias coverage. No new scalar writer is identified. Original043/092/099/120/124/130 remain PARTIAL/AUDIBLE;14 writer identities unchanged. Coverage116 CHECKED /24 PARTIAL /0 NOT DONE;140 AUDIBLE /0 INTERNAL. Q15 remains behindQ14. Research only;no production or manifest changes.

out of scope: per-sample DSP bodies and dynamic resolver infrastructure;neither is reconstructed. The floating-point opcodes in the witness are used only for register and predicate boundaries.
