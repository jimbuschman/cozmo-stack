# Q14-104/131: A0423C output argument correction

Checkpoint766; scope guard reread. Primary `20261010-sound-routing-output-entry-native.txt`; same engine hash. This corrects a concrete stack-map error, not a new family or alias exclusion. Exact-entry navigation `-output-input-callers.py/.json` and `-output-pointer-navigation.json` adds no positive supplier; repeated negative navigation is not reachability proof.

Current research claim in `20261010-sound-attachment-pbi-registry-store-attribution.md`:

"A04284 actual A0423C saves its second stackargument inr4 and writes descriptor24 there at34 after copying the supplied record payload."

Actual A0423C pushes five registers, frame delta14hex. Therefore LDR R4,[SP1C] atA04244 loads the THIRD original stack word (entrySP8), the output destination. LDR LR,[SP14] atA04248 loads FIRST original stack word (entrySP0), the nine-word payload pointer. LDR R5,[SP18] atA04268 loads SECOND original stack word (entrySP4), the value eventually written atoutput34. Earlier SP1C-as-second wording and descriptor24-at34 attribution are incorrect.

| Output field | Actual supplied source |
| --- | --- |
|0 |IncomingR2+44 word, savedR7 atA04254, storedA04264. |
|4 |IncomingR2+24 word, savedR5 atA0424C, storedA04260 beforeR5 is overwritten. |
|8 |IncomingR1, storedA04258. |
|C..2C |Nine words copied from first original stackargument byA0425C..7C. |
|30 |IncomingR3 savedR6, storedA04280. |
|34 |Second original stackargument (entrySP4), loadedA04268, storedA04284. |

The incomingR0 receiver is not a destination: it is overwritten by payload loads. Predecessor returns throughPOP PC A04238; A0423C is a distinct PUSH entry. Actual supplied THIRD-stack output pointer and SECOND-stack value remain UNKNOWN; no source family/object identity or harmless destination is inferred. Original104/131 complete writer/alias requirement remains PARTIAL. The remaining-work rows now name the correct third-stack destination and second-stack value; historical original quotations remain unchanged.

107 native+8 scope CHECKED/25 PARTIAL;140 AUDIBLE/0 INTERNAL unchanged. Research only; no production/fidelity/hardware change.

out of scope: payload/output backing allocation; retain exact copy geometry and field interfaces without tracing storage.
