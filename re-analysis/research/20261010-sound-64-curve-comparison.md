# Q14-115: independent 64-entry census and current D1-D9 comparison

Checkpoint615; decisions only; scope guard reread before each step. This answers the explicitly retained C35.4 census/comparison obligation. It does not claim the whole RTPC production path accepted.

Primary instruction companion `20261010-sound-64-curve-comparison-native.txt`: engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1. The raw archive check `20261010-sound-64-curve-input-check.py` reads six shipped ZIP bank entries/HIRC envelopes and native-layout fields directly, without candidate C# or exporter parsing. Each of64 records in `20261010-sound-64-curve-input-witness.json` preserves bank/object/ordinal, absolute bank byte offset/end, rawhex/SHA256, sourceID/type, accumulate, property, curveID, scaling and all physical12-byte points as bit patterns. Native9F7254..9F7304 establishes ID/type/accumulate/VLQ/property/curve/scaling/count/pointer call layout and reader raw-result gate. Existing TR/BR/FS/MR/ST primary rows establish the scoped node/FX/modulator input layouts traversed here; excluded positioning payload assertion never fires on these six banks.

| Bank | RTPC entries |
| --- | --- |
| Dev_Debug.bnk | 2 |
| English(US)/Cozmo.bnk | 39 |
| Init.bnk | 5 |
| Music.bnk | 4 |
| SFX.bnk | 14 |
| UI.bnk | 0 |

Total64. Scaling0=37/scaling2=27; accumulate1=61/accumulate0=3; sourceType0=48/sourceType2=16. Point counts2=35,3=19,4=5,5=2,6=3, total173 points. All abscissae strictly increasing; no count0/1 or duplicated curveID within one object. Every point's shape:1=3,4=142,9=28,6=1,7=1. Actual segment-left shapes (excluding final unused shape words):1=3,4=78,9=28,6=1,7=1. Source/input kinds and accumulation bytes are preserved separately; no human-name inference or default scalar substitution.

Fresh native replay uses the engine's real leafA14E28 through `re-analysis/tools/emu/emu_curve.py` Engine (no function hook or arithmetic substitute). `20261010-sound-64-curve-native-replay.py` builds cases from the independent raw records:1337 shipped cases exercise endpoints, neighboring binary32 inputs, five interior positions per segment, +/-0, infinities and NaN x. Additional cases exercise shapes0..11/FFFFFFFF, scalings0..5/7, signedzero and scaling thresholds/saturation/wrap, plus physical unsorted/duplicate curves with explicit hints. Total2405. All expected output and idx values come from native execution, stored in `20261010-sound-64-curve-native-replay.json`.

The isolated `_q14615_runtime/Replay.csproj` compiles the current production `WwiseRtpcCurve.cs` directly by link, with only a points/scaling record stub. It changes no production source. SDK9.0.201, Release/net9.0. `20261010-sound-64-curve-current-port-results.json`:2405 compared,0 value-bit mismatches,0 idx mismatches,0 NaN-payload differences. The check would count numeric mismatches/idx mismatches as failures and separately reports differing NaN payloads; both counters are0. These observations cover this corpus, not every possible32-bit input.

Current candidate source SHA256: d522b30a8790a0166c1a9ab988a48939e7721800eeb0827eae881e7a793354c3. Source hashes of the legacy helper/store/music caller are recorded below so the comparison is against this revision, not recalled notes.

| Original difference | Current binary32 port / engine comparison | Current legacy helper finding |
| --- | --- | --- |
| D1 width/order | Float intermediates and separated multiply/add stages; literal bit patterns; fresh replay matches native. Store defaultCurveA14E28 points to this port and its accumulators use float in curve order. | WwiseHierarchy.cs57..114 still computes in double with Math.Sin/Cos/Pow/Log10. This is a separate retained implementation discrepancy, not primary evidence. |
| D2 interior equality | Native left-point ordered comparison yields exact point value; exact endpoints/neighbors replayed for all64. | Legacy inclusive segment-right comparison can choose preceding segment at an interior point, preserving the original distinction. |
| D3 shape>=10 | Native zero then scaling dispatch; all such codes/scalings replayed, including literal pow10(0) and signedzero. | Legacy defaults to linear and reduced=true. |
| D4 NaN x | Native ordered comparisons allfail and select final value/index; all64 NaN-x cases match. | Legacy linear-segment path can evaluate with NaN t. |
| D5 physical unsorted/duplicate order | Direct physical order, no sorting or nonpositive-span workaround; fresh duplicate/unsorted/hint cases match. No shipped such abscissae. | Legacy has span<=0?1 and different segment gates. |
| D6 count0 | Native loaderA11888..B0 requires points ANDcountnonnull/nonzero; ordinary bank target2 rejects with1F. A11A68 is append continuation, not the validation instruction address. No shipped count0 and no empty native curve replay claimed. | Legacy Evaluate returns0; exact port throws on direct empty-call input. Neither is claimed as a replacement for the native loader gate, whose full contract remains113. |
| D7 polynomials | Native bit constants and binary32 product/add order, all shape arms exercised including optional shapes absent from shipped records. Fresh outputs match. | Legacy uses ideal sine/cosine/power and rounded SCurve coefficients in double. |
| D8 scaling2 | Native bit decomposition/two-term log, exact binary32 limits/signedzero; threshold/endpoints replay matches. | Legacy ideal Math.Log10 and double764.616 clamps remain. |
| D9 scaling3/4 | Native exponent/mantissa reconstruction, binary32 thresholds and wrap; endpoint corpus exercises both although shipped scaling is0/2 only. Fresh outputs match. | Legacy Math.Pow and double0.05 threshold remain. |

Concrete current legacy caller: `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseMusicStream.cs:146` calls binding.EvaluateScaled, while `WwiseRtpcStore.cs:474` defaults to the binary32 port. This is a code finding for the manager; this comparison does not establish the whole music wiring/input path or repair the caller. Do not label every current curve consumer exact because the port passes.

CHECKED for original Q14-115: independent requested64-entry/scaling/shape/accumulate census and currentD1-D9 comparison are complete, with current legacy discrepancies explicitly reported. Q14-113 retains complete loader/subscription reconciliation; Q14-136 retains that original113 remainder but its census/comparison portion is now paid. No fidelity taxonomy change or code implementation from these findings.

out of scope: curve/registry/storage/allocator backing and per-sample DSP. The inspected curve arithmetic is control-value math and was kept exact. No ADP-1 equivalence or hardware claim. Counts98 native CHECKED+8 scope CHECKED/34 PARTIAL/140;140 AUDIBLE/0 INTERNAL. Q15 remains afterQ14.

Current WwiseHierarchy.cs SHA256 8de397a172fc26148b16ab77b6ba9640fb05fa44e738fa7af65a6f396e5b8b83.

Current WwiseRtpcStore.cs SHA256 54cab6b878e7236f36ff7868ff6249acbe3f5e1150bc525e1433ebace5f62a10.

Current WwiseMusicStream.cs SHA256 54133a3efe3d4738fcacf7eadeadc6e4a104fdf9f4d5082b258e8bdd3f5ba587.
