# Q14-104/131: frame locals and resampler controls

Checkpoint707; guard reread before each step. Primary: attachment-resampler-field-stores-native, attachment-resampler-inputs-native and attachment-resampler-reset-entry-native companions; engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1. Ten original candidates; VI191/VD490/RS1/RS4/RS5 and existing pitch rows reused.

| Candidates | Actual destination and boundary |
| --- | --- |
| A45558/A4597C | Actual respective frame setup sets fp=SP30/SP28 before variable stack reservations; writes fp−38/fp−34 are locals, not positive node attachments. out of scope: sample conversion bodies after this local attribution; stop descendants. |
| A46DB0/A46DB4 | RS1 actualA46D70 embedded resampler constructor clears targetstep34/ramp38. Positive VI191/VD490 voice ctor suppliesV108; source-stage format paths supply their separate embedded resampler. Initial phase2C=10000 and ratio4C=3F800000 retained, no whole-state zero claim. |
| A473EC/A47440/A474D8/A474E0 | RS5 actualA47384 retains incoming resampler r0→r4; clears ramp38 before recomputing changed pitch, publishes target34. First-call path publishes current30/target34 and ramp38=400hex before flag57clear. Exact integer phase/ramp and immediate-versus-ramp controls remain existing rows, not ADP-1 sample interpolation. |
| A47808/A4780C | ActualA4776C retains resampler r0→r4A47798. Flag57/changed-pitch gate and existing powf/ratio/fixed-point conversion publish current30/target34 then ramp38=400hex, flag57clear. No parent or outputbus written. The receiver remains conditional beyond positively supplied embedded products; no alias absence inferred. |

out of scope: per-sample interpolation/conversion arithmetic; stop descendants. Phase, frame counts, ramp and format/pitch selection remain exact controls; imported phone libm rounding remains its separately named boundary. Original104/131 remain PARTIAL; counts107 native+8 scope CHECKED/25 PARTIAL,140 AUDIBLE/0 INTERNAL. Research only.
