# Q14 rate/frame: smallest music child-loop cap

Checkpoint827; guard reread before each step. Decisions only; research only. Coverage116 CHECKED /24 PARTIAL /0 NOT DONE;140 AUDIBLE /0 INTERNAL unchanged.

Primary `20261011-sound-music-child-cap-native.txt` reopens9877B4's cursor/count loop and exact rate-load address. Companion witness independently reads the remaining common prefix and child count from each hash-checked shipped type10/12/13 payload, using existing prefix navigation only to locate the first ordinary-property count. It verifies ordinary/range IDs and raw values, positioning C0/C3(no extra payload), aux0(no ID trailer), six advanced bytes, statecount0, each RTPC binding and point span, child vector and the following23-byte meter header. Every nonempty child-vector offset agrees with the earlier navigation, but the count offset is computed from these fields rather than searched for. Archive SHA256d889b6e82606be8e284fedde44daecdbd5406f72bf5c68a1396f319eeecf6145.

All346 music node payloads(209 segments,14 MusicSwitch,123 playlists) have1..84 children.344 have no own RTPC bindings;two playlists have two each. All346 have no own state groups. Meter enabled bytes are0 for235,FF for104 and1 for7; preserve native nonzero truthiness rather than normalize the serialized byte. No claim about inherited or later controls follows.

The two saved states from826's9877F8 cap have different dispositions:

| Saved state | Exact decision and bounded disposition |
| --- | --- |
|9879C8;R7=B9(185)|987820 reads U32 childcount intoSL. Nonzero takes987984;9879A8 setsR7=0.9879C4 increments before the child-ID load/callback. The callback must return1;otherwise9879E8 exits.9879EC/F0 repeats only whileR7!=SL. For these authored vectors, successful iterations are1..count<=84. Saved185 is outside this finite input bound. The callback ABI preservesR7/SL; this claim is conditional on the already-named callback contract, not a proof of arbitrary indirect targets or their success.|
|987838;R5=104028C,SB=0|This is a legitimate meter-header read after the child loop. Do not classify it unreachable from its saved register set. Already-paid MPAY3 owns header/enable/tempo/meter/offset conversion, with987A1C/A28's rate GOT/scalar read independently joined at803. No new writer or pointer publication is established by revisiting this read.|

The witness is an authored-input bound, not proof that arbitrary runtime payloads, altered cursor/debug-skip modes or every callback product are excluded. It removes the impossible185-loop state from this selected bank-input lead and credits the already-covered header state;it does not prove every path in a capped symbolic window exhausted. Original043/092/099/120/124/130 retain their actual all-writer/relative/indirect/stack/copy/admitted-input remainders and remain PARTIAL/AUDIBLE. Q15 source rows MPAY1-4 were read as existing primary navigation only;no Q15 edit or acceptance.

out of scope: backing allocation/storage/I-O, per-sample DSP and excluded branch payloads. No such callee was traced;child ordering, count, callback-result gate and exact timing parameters remain in scope. No production,manifest,inventory,PROJECT_STATE or hardware changes.
