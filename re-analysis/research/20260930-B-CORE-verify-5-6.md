# B-CORE batches 5 and 6: Opus verification

- **Date:** 2026-09-30
- **Verifies:** bee7266 (batch 5), 23c65ac (batch 6), b8c1525 (DONE); code at HEAD 6bfaec0
- **Verifier:** Opus `cozmo-verifier`, from the manager session.
- **Result:** FAIL. **No record settles.** Each record's `unresolved` carries its verdict, starting "Opus verification
  of B-CORE". The summary is below.

| record | verdict | main reason |
| --- | --- | --- |
| M3-010 | not yet | bit-exact (Unicorn: 0 mismatches on 331,086 inputs); needs a test through PopFrame |
| M1-029 | needs extraction | System.Text.Json stands in for the shipped jsoncpp; depth limit off by one; decodeNumber unread |
| M2-002 | not yet | PlaceObjectOnGroundAction's status-bit 0x4 gate (0x005549B0) is missing |
| M3-001 | not yet | codec: StbImageSharp stands in for libjpeg 9; the gray path otherwise holds |
| M3-018 | not yet | invented log texts; no empty check after imdecode (0x004F2250); codec |
| M3-037 | needs extraction | the "policy" bytes are partly the previous frame's (0x004F1DA2, 0x004F1DAA) |
| M3-013, M3-021, M3-032 | not yet | the DONE commit changed prose only; M3-032 has no fidelity tag |

## Process findings

- **The build used unapproved rows.** It built from the research file's rows (11a..11r, 1a..1k, 12a..12k) without an
  inventory correction.
- **Two weak tests.** `M3_010_C6_*` compares two outputs of the implementation. `M3_018_I2` asserts only substrings.

The verifier's scratch tools, including the Unicorn mu-law oracle and a .NET JSON probe, were in the manager's
scratchpad and were not kept.
