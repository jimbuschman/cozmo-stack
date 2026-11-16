# COMMANDS-FACE-SETUP-1

The commands harness raises the head through the public head-angle action before each of turn-to-face and track-face. It sets 20 degrees for a repeatable setup. FindFacesLookUp supplies a 20–40 degree head range at `0x005C1AA6..0x005C1AC2`. CreateHeadTurnAction draws the head angle from that range, converts it to radians, and passes it to PanAndTilt at `0x005E331A..0x005E337C`. The chosen float is `0x3EB2B8C2`.

After the head action succeeds, the harness waits for the first fresh public RobotObservedFace report (E2G `0x46`). It provides an operator positioning prompt and supports Ctrl+C during acquisition. The 20-second command window starts with the observed face ID. Tracking still requests cancellation after 10 seconds. Each setup records the head result under M15-103. The dry run validated 18 fidelity references and printed both setup prompts without opening a network connection. Operator hardware confirmation is pending.

Verification also passed 107 harness and Meet Cozmo tests and the 762-record fidelity check.
