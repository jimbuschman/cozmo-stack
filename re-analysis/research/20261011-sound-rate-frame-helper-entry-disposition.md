# Q14 rate/frame helpers: bounded shipped entry gates

Checkpoint815; guard reread before each step. Exactly814's TEN shipped definitions of the three existing helper interfaces;no arithmetic body or resolver selection reconstruction.

Reproducible -helper-entry-registers.py/.json/.txt reads each exact exported entry in its ELF-declared ARM/Thumb state,records the library SHA256 and instruction bytes/register accesses,and stops at the first control frontier (or both tracked register definitions). All10 entries reach a control frontier before definingR3. None reads incomingR2/R3 before that frontier;five defineR2 from the real scalar operand registers. These are straight-line entry witnesses,NOT whole-function or alternate-branch absence proofs. A later write after an earlier incoming-register read would not establish independence;the explicit read list is preserved.

| Native entry group | Exact first gate |
| --- | --- |
|acatts UIDIV3FEE4;DAS UIDIV28BC4;stlport UIDIV4EDFC|SUBS R2,R1,1 followed byBXEQ LR (Thumb entries haveIT EQ). DivisorR1==1 returns incomingR0 unchanged;R2 has become0,andR3 was not read. Other divisors fall through;no later arithmetic/body claim.|
|gnustl UIDIV9B370|CMP R1,0;BEQ9B3DE. The divisor-zero branch is an actual native entry decision. Its target and fallthrough are unpaid by this prefix;neither incomingR2 norR3 has been read yet.|
|acatts IDIVF4408;BlueDove IDIVDE34;gnustl IDIV9B404;stlport IDIV4EEC4|CMP R1,0;BEQ respectivelyF44BA,DEE6,9B48C,4EF90. Preserve these concrete divisor-zero frontiers rather than assuming a benign result or callback behavior. No incomingR2/R3 read occurs before this branch.|
|DAS L2F29180;opencv_imgcodecs L2FB6968|ORRS R2,R0,R1;IT EQ;BXEQ LR. Both input words0 returnR0 unchanged asraw00000000;R2 is0,andR3 was not read. Nonzero64-bit input falls through;no conversion algorithm,rounding or later helper interface is reconstructed.|

This identifies real scalar short-circuit/zero gates in available shipped source and exact incoming-register non-use up to those gates. It does not certify which provider the engine binds,all helper paths,post-call caller register usage,independent global mutation,or denominator/64-bit zero reachability from actual shipped requests. In particular,zero here is not automatically the census's excluded zero-channel case:the actual caller scalar must establish that relation first.

Next original unpaid boundary:check the existing positive helper calls' post-call reads/definitions and actual scalar inputs;any remaining entry branch/tail/provider interface must retain its exact scope and UNKNOWN disposition. Do not introduce new numerical/provider families or count these conditional entry prefixes as admitted weight/frame setters. Original043/092/099/120/124/130 remain PARTIAL/AUDIBLE;14 positive scalar-writer identities and coverage116 CHECKED /24 PARTIAL /0 NOT DONE unchanged.140 AUDIBLE /0 INTERNAL. Research only;Q15 stays behindQ14. out of scope: per-sample numerical bodies,call-frame/registry backing and dynamic resolver implementation;no such body is descended into.
