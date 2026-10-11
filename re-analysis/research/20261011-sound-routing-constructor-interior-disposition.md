# Q14-104/131 original constructor destination candidate attribution, checkpoint879

Scope guard reread before every step. Only the34 remaining755 original candidates into9F319C. No branch rescan or new recipient family. `-attribution.py/.json` retains actual bytes, nearby ARM words and the precise limits. Engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1.

32 candidates overlap the upper half of a different ARM instruction;9F318C overlaps the lower half of actual9F3170 constructor STR zero,[R0,8];9F3164 is a literal. Actual9F2E3C LDR SB,[PC,320] reads literal9F3164=0064D440.9F315C branches9F2F70 before the literal.9F3198 POP-PC ends the preceding constructor;9F319C starts with its own PUSH. No fallthrough destination from9F3170 is supplied. These word/literal attributions do not prove arbitrary alternate-mode entry absence.

| Original Thumb candidate | Interior target | Primary ARM word / literal | Disposition |
|---|---|---|---|
|0x9f2a4e|#0x9f3212|009F2A4C mvn r7, #0|upper half, different ARM instruction|
|0x9f2ae6|#0x9f322a|009F2AE4 mov r1, #0|upper half, different ARM instruction|
|0x9f2afa|#0x9f31a4|009F2AF8 cmp r3, #0|upper half, different ARM instruction|
|0x9f2afe|#0x9f3242|009F2AFC mov r2, #0|upper half, different ARM instruction|
|0x9f2b52|#0x9f31fc|009F2B50 cmp r3, #0|upper half, different ARM instruction|
|0x9f2b6e|#0x9f3218|009F2B6C cmp r3, #0|upper half, different ARM instruction|
|0x9f2bce|#0x9f3272|009F2BCC cmp r0, #0|upper half, different ARM instruction|
|0x9f2bde|#0x9f31e8|009F2BDC movw r1, #0x380c|upper half, different ARM instruction|
|0x9f2c8e|#0x9f319e|009F2C8C add r1, r6, #0x10|upper half, different ARM instruction|
|0x9f2ed2|#0x9f3216|009F2ED0 mov sb, r2|upper half, different ARM instruction|
|0x9f2eee|#0x9f3232|009F2EEC mov r1, lr|upper half, different ARM instruction|
|0x9f2efa|#0x9f31a8|009F2EF8 cmp r5, ip|upper half, different ARM instruction|
|0x9f2efe|#0x9f3242|009F2EFC mov r2, ip|upper half, different ARM instruction|
|0x9f2f0e|#0x9f31b2|009F2F0C cmp r0, r4|upper half, different ARM instruction|
|0x9f2f5e|#0x9f3210|009F2F5C cmp r7, r6|upper half, different ARM instruction|
|0x9f2f8a|#0x9f31ec|009F2F88 bx r3|upper half, different ARM instruction; actual indirect target UNKNOWN|
|0x9f30a2|#0x9f31ac|009F30A0 add r5, r3, r5|upper half, different ARM instruction|
|0x9f30ae|#0x9f31b8|009F30AC add r4, r3, r0|upper half, different ARM instruction|
|0x9f311a|#0x9f3224|009F3118 add r1, r3, r1|upper half, different ARM instruction|
|0x9f3136|#0x9f3240|009F3134 add r3, r3, r0|upper half, different ARM instruction|
|0x9f3164|#0x9f31e8|009F3164 0064D440|literal, reader9F2E3C|
|0x9f318c|#0x9f31a0|009F318C str lr, [r0, #8]|lower half, different ARM store|
|0x9f3302|#0x9f322a|009F3300 ldr r0, [r2, r3, lsl #2]|upper half, different ARM instruction|
|0x9f36ae|#0x9f31d2|009F36AC ldr r2, [r0, #4]|upper half, different ARM instruction|
|0x9f36c2|#0x9f31e0|009F36C0 str r2, [sp, #0xc]|upper half, different ARM instruction|
|0x9f36ce|#0x9f3202|009F36CC ldr r3, [r8, #0x1c4]|upper half, different ARM instruction|
|0x9f36d6|#0x9f31f4|009F36D4 str r3, [sp, #8]|upper half, different ARM instruction|
|0x9f36da|#0x9f320e|009F36D8 ldr r3, [r8, #0x1c8]|upper half, different ARM instruction|
|0x9f36de|#0x9f31fc|009F36DC str r3, [sp, #4]|upper half, different ARM instruction|
|0x9f36ee|#0x9f322c|009F36EC ldr sl, [sp, #0x18]|upper half, different ARM instruction|
|0x9f36f2|#0x9f3230|009F36F0 ldr r1, [sp, #0x1c]|upper half, different ARM instruction|
|0x9f36fa|#0x9f3228|009F36F8 ldr r4, [r5]|upper half, different ARM instruction|
|0x9f3732|#0x9f3256|009F3730 ldr fp, [r0, #0x1c4]|upper half, different ARM instruction|
|0x9f373a|#0x9f325e|009F3738 ldr sb, [r0, #0x1c8]|upper half, different ARM instruction|

The actual9F2F88 ARM BX R3 needs a separate explicit limit.9F2E48 saves incoming R0 atSP2C;9F2F70 reloads that receiver,74 sets R1=0,78 reads its vtable,7C loads liveC4,80/84 restores the frame,88 tails the selected method. Rejecting Thumb9F2F8A does not prove that actual indirect target differs from9F319C. No incoming receiver/vtable/admission is supplied here. Keep the target UNKNOWN; do not open a generic callback/control family from this word overlap. Its primary entry/argument/tail instructions are retained in the JSON.

Exactly open remains actual9F319C constructor destination before excluding its overlapping34/36/37 fields from parent34/outputbus38 alias proof, plus the original unrestricted aliases.878 separately retains33 candidates for9D2410; no remaining755 candidate is silently omitted. Stop these34 attribution leads without calling the constructor unreachable or accepting a neighboring product as its receiver.

Q14-104/131 remain PARTIAL, AUDIBLE.117 CHECKED (109 native+8 scope)/23 PARTIAL/0 NOT DONE;140 AUDIBLE/0 INTERNAL. Research only; no production/manifest/inventory/PROJECT_STATE/hardware/Q15 changes. out of scope: allocation/backing/copy and per-sample arithmetic; actual target/destination/control inputs remain in scope.
