# Q14-104/131: kernel frame/scratch and resampler ramp destinations

Checkpoint708; guard reread before each step. Primary: attachment-resampler-kernel-locals-native/receivers-native, attachment-kernel-tail-destinations-native, kernel-scratch-inputs-native and kernel-ramp-slot-native/update-native companions. Engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1. Sixteen original candidates. Existing G2/RS6 dispatch binds incoming fourtharg r3 to resampler; no sample recurrence recovery.

| Candidates | Actual destination and boundary |
| --- | --- |
| A484D0/A48A24 | ActualA48264 saves incoming resamplerr3 inSP2CA482BC; A486FC saves itSP1CA48750. Later positive38 stores reload those exact slots. They publish resampler ramp38, not node outputbus. Scope/count/phase decisions retain existing exact kernel contracts; no per-sample arithmetic exemption for them. |
| A49188/A49254/A492B4/A493F0/A49564 | ActualA4913C fp=postpushSP20. Negative34/38 destinations are frame locals; no node writes. |
| A49834/A49868/A49A4C | ActualA49634 fp=postpushSP20; negative34/38 destinations are frame locals. |
| A49984/A49988 | Same actualA49634: sb=align16(SP+F) atA4968C after fixed stack reservations. These positive34/38 stores are stack scratch lanes; not resampler or node fields. out of scope: sample scratch/conversion arithmetic; stop descendants. |
| A4A458 | ActualA4A2D8 retains incoming resamplerr3 in sl atA4A2DC; stores ramp38. |
| A4A770 | ActualA4A59C saves incoming resamplerr3 inSP8A4A5E8; reloadr1A4A760 then stores ramp38. |
| A4AAAC | ActualA4A958 saves incoming resamplerr3 inSP20A4A9E0, reloadA4AA14. Direct branchA4AA18 reaches common store with it; zero-phase path preserves it inSP0 viaSTMA4AA90 before division and restoresLDMA4AA98. SP0 earlier held outputpointer and latercount: those older values must not be substituted for the actual restored receiver. Store is ramp38. |
| A4AE68 | ActualA4AC04 retains incoming resamplerr3 inr8A4AC08; stores ramp38. |

out of scope: per-sample interpolation/conversion bodies; stop descendants after destination attribution. Frame counts, phase and ramp progress remain exact, with their existing corpus/kernel evidence limits; no new acceptance/equivalence-test claim. Original104/131 stay PARTIAL; counts107 native+8 scope CHECKED/25 PARTIAL,140 AUDIBLE/0 INTERNAL. Research only.
