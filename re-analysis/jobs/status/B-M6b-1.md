BLOCKED 2026-09-28 16:17 -05:00 Codex — the first divergent numeric stage is now localised to the IMDCT, but the exact bad instruction inside the generated kernel is not established.

## Resume result

- Claim: `9ff47d3`.
- Localisation report: `0c203bf`, `re-analysis/research/20260928-B-M6b-1-numeric-localization.md`.
- Shipped mono media `557491`, five consecutive packets:
  - residue correlations: `1, 1, 1, 1, 1`;
  - post-coupling correlations: `1, 1, 1, 1, 1`;
  - post-floor correlations: `0.9999999796, 0.9999999820, 0.9999999749, 1.0000000118, 1.0000000037`.
- Therefore the first divergent behavior-changing stage is the call at `0x00AB6F04` into `mdct_backward` entry `0x00AB4E34`. The earlier floor/residue candidates are excluded for this shipped path.
- A standard-IMDCT diagnostic substitution was tested and removed. It was not source-faithful for the runtime's packed half-buffer/transition combine and was not committed.

## Blocker

The approved X5 extraction has the instruction listing but no phase-boundary native vectors or worked numeric output. Final-output comparison cannot identify the exact first bad instruction within `0x00AB4E34..0x00AB5A1C`; changing that generated kernel now would be a guess. An exact repair needs native phase captures or a fresh instruction-by-instruction control-flow audit with numeric checks at pre-symmetry, butterfly, stages, terminal butterfly and tail.

No production or test change is retained by this resume. `M6-002` remains `IMPLEMENTATION_GAP`; no record is settled. Existing committed partial implementation remains `96b6f9d`, with corrections C13/C14 and the previously clean fidelity/full-suite results described by `ae202b1`/`268aa6d`.
