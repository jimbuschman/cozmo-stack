# Q14-008: direct PC-relative address constructions

Checkpoint810; guard reread before each step. Engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1.

This pays a specific computed-address encoding form beyond609's direct/raw-pointer/export search and780's allocated-word self-relative forms. -weight-pc-address-navigation.py/.json reads original ELF EXECINSTR section bytes without changing a native data-read map. It checks aligned ARM immediate ADD/SUB withRn=PC using raw rotated-immediate decoding andPC+8;Thumb16 ADR using alignedPC+4 andimm8*4;Thumb32 ADDW/SUBW withRn=PC using alignedPC+4 andi:imm3:imm8. Either state-bit form of exactlyA08024 is selected. These are encoding/value candidates,not a whole-function mode or runtime-input proof.

The three arithmetic-form populations are17639 ARM,17041 Thumb16,and1073 Thumb32 candidates. None constructsA08024/A08025 in these precisely defined forms. No callee descent or new live weight mutation input follows. Literal-register or table-base-relative/composite address calculations,copied/stack/foreign pointers and indirect transfers remain outside this finite check. Earlier30 immediate interior encodings are already paid by776/795 and are not repeated.

Q14-008 stays PARTIAL/AUDIBLE:an admitted original shipped caller must supply actual node,childID andweight,or a complete bounded call-edge exclusion proof must settle that original frontier. Static authoredweights are not a runtime call;zero address-construction candidates do not prove this method unreachable. Stop this finite encoding lead. Coverage116 CHECKED /24 PARTIAL /0 NOT DONE;140 AUDIBLE /0 INTERNAL. Research only;Q15 stays behindQ14. out of scope: playlist/bitmap backing andbit5 ping-pong;no storage/DSP descendant.
