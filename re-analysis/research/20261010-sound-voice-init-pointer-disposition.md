# Q14-033: VoiceInit address-taking candidate

Checkpoint663; decisions only; scope guard reread before each step. Reproducible bounded navigation `20261010-sound-voice-init-pointer-expansion.py/.txt` restricts the original VoiceInit targetA54A30 to executable sections and checks local ARM/Thumb PC-literal/MOVW construction forms. One candidate window is emitted atA54BC4, with no formed target or indirect-target event. This is not a complete indirect caller or alias proof.

VI663A: fresh primary `20261010-sound-voice-init-pointer-candidate-native.txt` and `20261010-sound-voice-init-pointer-base-native.txt` positively reject that one apparent relative-address lead. A54BC4 loads rawFFFFFE1C fromA54F0C into r2. It is NOT added to that instruction's PC: A54BCC reloads r4 fromSP+C, and A54BD4 reads[r4+r2]. EntryA54A4C/A54A5C/A54A60 forms A54A5C+8+005EB828=104028C and saves that GOT base toSP+C. Hence effective slot10400A8, rather than a VoiceInit code pointer. The next loads form the existing allocator-pool input toA7A7F4; no newly discovered VoiceInit callback is consumed. out of scope: allocator/backing body.

VI663B: VS4/VS14/VS16's three known direct producer and error/re-init gates, and VF's selected format/FX/lifetime decisions remain paid. VW650 still binds actual E0/E4 stores and both caller-mediated output-word updates. The original complete indirect/computed/stack/copied pointer caller proof and additional E0/E4 writer-alias proof remain PARTIAL. The single-window encoding result is neither all-callers proof nor evidence that initialization always succeeds.

Counts107 native+8 scope CHECKED/25 PARTIAL;140 AUDIBLE/0 INTERNAL. No production/fidelity/inventory/hardware/Q15 edits. out of scope: per-sample DSP, storage/I-O and excluded branch payloads.

Correction, checkpoint793: PT540C already contains a fourth direct VoiceInit call atA41C68. The three VS/VI523A sites above remain locally valid but do not exhaust even the known direct callers. See `20261010-sound-voice-init-interior-disposition.md`; indirect and E0/E4 alias proofs remain PARTIAL.
