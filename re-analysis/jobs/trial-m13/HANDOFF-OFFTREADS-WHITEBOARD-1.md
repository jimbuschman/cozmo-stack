# OFFTREADS-WHITEBOARD-1

- The AIWhiteboard constructor clears +0x74 at 0x0056A2DA. ReactionTriggerStrategyHiccup sets the byte at 0x006107A6 after the occurrence and needs gates and clears it on reset at 0x00610212. The configured reactions now read that same whiteboard on the captured robot. A retired robot's callback cannot read through a replacement owner.
- OnBack reads the byte via Robot+0x264 and the component's +0x18 at 0x00608806–0x0060880C after the cliff reading passes. 0x00608820–0x00608838 selects FlipDownFromBack (0xCB) or HiccupRobotOnBack (0xE4). OnFace reads the same byte at 0x00608ADA–0x00608AE4. At 0x00608AF0–0x00608B28 the flag overrides either lift-angle choice with HiccupRobotOnFace (0xE5).
- The reaction tests select each configured native owner with its whiteboard flag set and clear, checking the selected animation starts. The Hiccup strategy tests cover the writers. No fidelity status changes. Operator robot validation and Claude spot-read are pending.

- All 147 reaction, Hiccup, and related behavior regressions passed. Fidelity check passed all 762 records.
