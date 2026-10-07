# ADP-1 comparison tooling (B-ADP-HARNESS, queue 3 Q3)

`adp_compare.py reference.json candidate.json --output result.json` measures two
captured mono signed-16 little-endian robot output streams. The input contract is
documented in its module docstring. It rejects non-744-byte chunks, gaps, overlaps
and reordered chunks. The shared `case` object must include event, initial states,
switches, RTPC values (float bits), seed/runtime inputs and timed changes. Both
renderers must use the same absolute sample clock; never trim leading silence or
rebase to the first nonzero sample. Save the renderer version hash, engine hash,
executed stages and replaced imports in `provenance`.

The comparator checks structure before calculating PCM error. It reports exact
zero/nonzero regions without inventing an amplitude tolerance. An offset is a
failure; it never shifts a stream to improve its score. PCM metrics are in signed
16-bit sample units. SNR for zero error or zero reference energy is represented
by null plus a reason, avoiding nonstandard JSON Infinity/NaN. `aggregate` combines
sample counts and energies across structurally passing cases rather than averaging
case SNRs. It does not set an acceptance threshold or settle a fidelity record.

Run its six independent hand-stream tests with:

```
python -m unittest discover -s re-analysis/tools/emu -p test_adp_compare.py
```

**MISSING: reference renderer and C# capture adapter.** These files are comparison
tooling, not an event renderer. Existing component oracles replace behavior-changing
engine stages and cannot be joined into a purported reference by inventing their
outputs. The blocked baseline report identifies those stages. Renderer provenance
is not authenticated by this comparator: a manually supplied bundle is not proof
that the shipped engine produced it. No PCM baseline has been measured yet.
