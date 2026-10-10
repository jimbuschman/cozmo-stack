# Q14-051 — concrete inherited MIDI controls (checkpoint574)

Scope guard reread. Original question: whether110896138 routes notes to get-in403781184. GI572/GC573 bind membership, actual128, single-step choice and admission. This closes the original research routing obligation, not whole M9-013 or unconditional PCM output. Primary additions: `20261010-sound-midi-getin-controls-native.txt` and `20261010-sound-midi-getin-controls-witness.jsonl`. Engine SHA256:02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1. Bank SHA256:7a2770c62f3bf31ee4f77389d3a147c85768034bb29c9c84e913a3284a5efbe8.

| Step | Primary / positive join | Exact recovered decision |
| --- | --- | --- |
| GM574A bounded primary inputs |87 complete envelopes/payloads:54 Sound,31 RanSeq, target Layer and parent ActorMixer | Follow get-in's positively serialized children only; add target110896138 and ancestor62050212. No other ancestor branches descended. Native CF/NB/TR prefix layout independently parses source prefix for type2, FX/count, flags/bus/parent and ordinary/ranged property blocks. Raw parsed values are compared to navigation export afterward; all payload hashes/envelopes checked. Child words independently checked on the get-in tree. |
| GM574B inherited path | NJ149/LI1/CF15–17;actual1C=9F1C40;9F402C ctor | Actual parent setter stores child34. Ctor initializes34/3C0; ordinary-count0 preserves established3C0. Primary403781184→110896138→62050212→0; every descendant's parent is in this bounded set. Parent ordinary IDs0/3 are not note-offset or bounds. No fictitious property bundle or cached hierarchy substituted for live34. |
| GM574C recursion and channel | Complete9EE454..9EE940;NS/NE/LN | Own channel property35/defaultFFFF is checked by native ASR/channel then bit0 before any recursion. Mutableflag nonzero recursively processes live34; first non1 propagates. Successful parent return clearsflag before current transforms. There is no override47-bit shortcut in this helper. Parent changes persist on later child failure. Exact fresh/replay invocation/status/flag/copy distinctions remain NS/LN, not guessed reprocessing of every ancestor on every call. |
| GM574D authored/default tuple | CF1/2 and87 primary property blocks; initializer4DDFF0/PDI;NE3–6 | Defaults: note/velocity offsets0, inclusive lower note/velocity0 and upper127, channelFFFF. No ordinary2F/30/33/34/35 in these87 blocks. Twelve31/32 equality gates below accept only48/50/53 after native noteoffset addition and USAT7. Velocity offsets add before clamp1..127 and inclusive bounds. Local successful note/velocity stores happen only after both ranges pass; no rollback of parent mutation. Other ordinary volume/pitch words are not silently relabelled noteoffset2F. |
| GM574E actual live queries | RawFFA77C/780=0B/0C;NE query gates/scopes;R/RV;RD1/LW5/R27 | Current node14 mask independently gates A11590 selectors0B/0C. Returned live values use native sign-dependent +/-half, S32 conversion and integer addition before saturation/clamp/ranges. Constructor A19F94 sets14null; actual registration/mask publication RD1/LW5/R27 remains exact. **No zero-query shortcut, authored-only runtime assumption or global no-writer assertion.** Existing scoped-query rows supply the recipient behavior; these live values stay inputs to the recovered decision chain. |
| GM574F routing answer | GI572/GC573/GM574;NS/NE/DG/LN/C/TR/CA153;checked090/091/103 | Get-in is a real submitted child, with exact MIDI preparation and delay before its128. Nested choice precedes selected-child filtering, so a subsequently rejected pitch branch does not redraw another sample. Membership is settled; 'every note starts a get-in voice' is not claimed. Cited byte-reader9BBF9C is not a MIDI selector. Admitted A78D10 source interfaces already have positive source lifecycle closure, rather than being reopened as arbitrary MIDI suppliers. |

Primary authored pitch gates (integer transformed note, both bounds equal and inclusive):

| Node | Parent | Accepted transformed note |
| --- | --- | --- |
| 681157 | 259413535 | 53 |
| 18959687 | 300432993 | 53 |
| 201447530 | 841935183 | 50 |
| 353945248 | 912407666 | 48 |
| 387457207 | 1070997484 | 50 |
| 404256753 | 355471845 | 50 |
| 481459662 | 875486276 | 48 |
| 787422992 | 300432993 | 53 |
| 789937467 | 50631727 | 53 |
| 796565229 | 259413535 | 53 |
| 914986671 | 439480567 | 48 |
| 1047308731 | 50631727 | 53 |

Original051's target-to-get-in membership/preparation/dispatch question has no remaining unpaid decision. Broader fresh/replay/ownerA8 and MIDI velocity-to-voice obligations049/052/053 keep their current PARTIAL coverage; this report makes no whole-record production claim about those paths. Runtime query inputs above remain explicit source-defined conditions, not unknown behavior replaced with plausible values. No fidelity status changes.

out of scope: backing allocation/storage/I/O, excluded source/blend/positioning branches and isolated per-sample DSP. No infrastructure or waveform bodies traced.

Q14-051 CHECKED;77 nativeCHECKED+8 scopeCHECKED/55 PARTIAL/140;140AUDIBLE/0INTERNAL. Checks: primary envelope/payload hashes, native prefix/property decode, bounded child/parent joins, twelve equality bounds, original quotations and140 impact rows. No production, manifest, inventory, hardware or Q15 edits.
