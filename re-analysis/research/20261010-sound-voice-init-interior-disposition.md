# Q14-033: fourth direct VoiceInit caller and interior-entry census

Checkpoint793; queue5 scope guard reread before each step. Engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1. Research decisions only; no production or fidelity acceptance.

The earlier claim that three direct callers exhaust VoiceInitA54A30 is incorrect. The bounded executable-section ARM/Thumb scan `20261010-sound-voice-init-interior-callers.py/.json` includes entry and interior targets in[A54A30,A54F00), excluding branches whose source is inside that body. It finds142 candidate encodings:10 ARM and132 Thumb. Four ARM entry calls are positively verified in `-interior-boundaries-native.txt`: A41C68, A42FF0, A44BE8, A555C0. This corrects the research caller count; it does not certify indirect/copy/interior caller absence.

| Positive caller | Actual argument and decision join |
| --- | --- |
|A41C68 retained handoff |PT540C's A41B74 input is voiceR0 and supplied requestR1. State28==11, stopgate0 and nonnull secondaryD8 select this path. Save secondaryR6; clearD8 atA41C28 beforeA55D04(voice,0), thenA55A84(voice,savedsecondary,1,0). Attachment result must be1. Move saved voiceR5 intoR0 atA41C64, thenInitA41C68. Non1 follows live48 stop;1 tail-starts saved secondary withA56478. ClearingD8 precedes teardown/attachment; no restoration or new policy/mode default is inferred. This whole decision recipient already exists in PT540C/VF/SA203/NS7. |
|A42FF0 |VI523A/VS16 source attachment gate retains its actual voice receiver, Init result and deletion/error/device-iteration order. Fresh containing primary slice is preserved. |
|A44BE8 |VI523A/VS4 secondary replacement retains its actual voice receiver and attachment-before-Init/result-before-saved-source-start order. Distinct fromA41B74 handoff. |
|A555C0 |VI523A/VS14-15 lazy Init uses actual voiceR4 inR0, failure Stop and suppression of desired render; the existing success recompute/state path is retained. |

The fourth caller is a census omission, not an unread handoff body: PT540C had already recorded the exact attachment/Init gates. Statements calling VI523A's three sites the total have been corrected; its original three local path descriptions remain valid. VI663's finite pointer lead and VW650's three writer *paths* remain separate counts; no E0/E4 writer is added merely because another Init call exists.

Ten additional false encodings are individually reopened in `-interior-false-candidates-native.txt`: six ARM-looking branches below95E540 are actual Thumb MOV+ADD(5C3340), upper half of SUBNE.W(5D4664), VMOV D1,R0,R1(5D9DAC), two literal loads(7C2760), SUB.W(899F9C), or upper half of LDR.W(8AC854). Two alleged Thumb calls at4B0158/4C817C are actual ARM PLT LDR PC instructions. Two aligned Thumb-looking branches atA547F4/A54820 are actual ARM STR LR into voice168/190, not Init calls. No unrelated callee descent follows.

The remaining128 Thumb encodings above95E540 are retained as unattributed navigation candidates, with their addresses/targets in JSON. Their upper-halfword alignment and location alone do NOT reject them or prove absence; mixed code/data and actual source boundaries remain to check. This checkpoint does not count them as admitted callers. A54A30's own frame, earlier returnA54A28, out-of-line cleanup/backbranches, seven literalsF00..F18 and next distinct entryF1C are captured as bounded geometry only.

Q14-033 remains PARTIAL: resolve those concrete interior-entry candidates, any positively supplied indirect/re-init input, and the explicitly required liveE0/E4 alias-writer proof. The complete selected four direct caller gates are now positively joined; never substitute a three-caller list for that proof. out of scope: backing/I-O, excluded positioning/source branches and per-sample DSP.

Coverage unchanged108 native+8 scope CHECKED/24 PARTIAL/0 NOT DONE;140 AUDIBLE/0 INTERNAL. No Q15 settlement.
