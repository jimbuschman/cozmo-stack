# R-M11-TIMEOUT — history admission timeout input

- Date: 2026-10-21. Kind: independent extraction; narrow follow-up to
  `research/20261021-m11-pose-history-settling.md` F1 (a lead, not adopted evidence).
- Answer: `re-analysis/research/20261021-m11-history-timeout-input.md`.
- Records: M11-051; read the current M1 transport owner records as dependencies.

Close the remaining input question for AddRawOdomState's timeout comparison.
Start with ReliableConnection::sConnectionTimeoutInMS at 0x01051F48, GOT 0x0103EA54,
Init store 0x0062F070 and Configure store 0x0062F172. Recover both words of the f64
value, all supported config/caller inputs, and whether any live write or escaped
alias can change it. Trace the admitted value into the history comparison and
identify existing C# production wiring versus a mutable test-only input.

Search research/inventory first, especially adopted OV128 and the current record's
evidence. Do not re-extract the already-read history admission/interpolation body
or audit transport generally. A bounded writer proof must state its limits; an
unresolved alias is UNKNOWN, not a reason to choose a plausible constant.

Record HEAD and quote the current record title/status/evidence. Table first:
step | native behavior and instruction citation | current production file/line |
record | supported/unbuilt/UNKNOWN. Use decomp/libcozmoEngine/index.tsv to locate
bodies; decompilation is not evidence. Finish with record impact, any necessary
new approval, and literal-based regression design. No code, test, manifest or
inventory edits, hardware work or settlement. Commit/push only the named answer
under the queued-research rule, then continue; no pending report must be adopted first.
