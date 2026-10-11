# Q14 rate/frame writer proof: NEON full-register store interface

Checkpoint771; scope guard reread. Same engine hash; `20261010-sound-rate-frame-neon-store-expansion.py/.txt` extends RB770's bounded pointer propagation with VST1/2/3/4 full-register write spans. Established bases only, D-register8bytes/Q-register16bytes, contiguous full-register spans; lane forms are explicitly excluded from this model. It examines address/width, not DSP values or lane arithmetic.

4392 local candidate windows emit the same314 existing navigation events as770 and no additional NEON-full-register-overlap event. This supplies no new actual writer candidate for105243C..4F or108DF90..9B. It does not exclude unknown bases, partial/lane stores, computed/relative/copied/stack aliases, writes from an unseeded function or caller mutation. No all-writer absence, input positivity or zero-divisor/provider claim follows.

Existing scalar writers, STM/VSTM boundary770 and RC654's actual ABI/call handoffs remain paid. Original043/092/099/120/124/130 PARTIAL;107 native+8 scope CHECKED/25 PARTIAL;140 AUDIBLE/0 INTERNAL unchanged. Stop this negative lead; research only.

out of scope: per-sample DSP arithmetic/NEON lane order and storage/I-O/backing bodies. Rate/frame timing interfaces remain exact.
