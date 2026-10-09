# M1 verification packet — 2026-10-09

Prepared for the operator’s request in this chat. Source snapshot `b8f087c33098673116d6cb5a3178157f34db0bcb` on main. No new verdicts. Explicit operator authorization to commit/push overrides the ordinary research-lane no-commit rule.

## Coverage

53 current M1 records; 52 included, 1 excluded. Built records and policy/equivalence records with no explicit open MISSING are included. Current non-MISSING verification uncertainty is retained verbatim; inclusion does not mean complete fidelity. Hardware-only and unbuilt recoverable records are excluded. The manager adopted M1-046 U1-U8, M1-047 P1-P4 and M1-053 P2a-P2m in B-M1M2.md; their builds and supplier boundaries are now included.

| Record | Packet | Quoted lines | Native cited intervals |
|---|---|---:|---:|

| M1-001 | [M1-001.md](M1-001.md) | 5 | 9 |

| M1-002 | [M1-002.md](M1-002.md) | 36 | 55 |

| M1-003 | [M1-003.md](M1-003.md) | 6 | 19 |

| M1-004 | [M1-004.md](M1-004.md) | 3 | 5 |

| M1-005 | [M1-005.md](M1-005.md) | 30 | 40 |

| M1-006 | [M1-006.md](M1-006.md) | 48 | 66 |

| M1-007 | [M1-007.md](M1-007.md) | 8 | 30 |

| M1-008 | [M1-008.md](M1-008.md) | 39 | 27 |

| M1-009 | [M1-009.md](M1-009.md) | 18 | 28 |

| M1-010 | [M1-010.md](M1-010.md) | 28 | 89 |

| M1-011 | [M1-011.md](M1-011.md) | 2 | 1 |

| M1-012 | [M1-012.md](M1-012.md) | 6 | 26 |

| M1-013 | [M1-013.md](M1-013.md) | 1 | 0 |

| M1-014 | [M1-014.md](M1-014.md) | 56 | 86 |

| M1-015 | [M1-015.md](M1-015.md) | 151 | 167 |

| M1-016 | [M1-016.md](M1-016.md) | 3 | 7 |

| M1-017 | [M1-017.md](M1-017.md) | 3 | 2 |

| M1-018 | [M1-018.md](M1-018.md) | 8 | 19 |

| M1-019 | [M1-019.md](M1-019.md) | 14 | 61 |

| M1-020 | [M1-020.md](M1-020.md) | 4 | 10 |

| M1-021 | [M1-021.md](M1-021.md) | 13 | 60 |

| M1-022 | [M1-022.md](M1-022.md) | 63 | 134 |

| M1-023 | [M1-023.md](M1-023.md) | 28 | 99 |

| M1-024 | [M1-024.md](M1-024.md) | 17 | 75 |

| M1-025 | [M1-025.md](M1-025.md) | 51 | 119 |

| M1-026 | [M1-026.md](M1-026.md) | 9 | 25 |

| M1-027 | [M1-027.md](M1-027.md) | 14 | 23 |

| M1-028 | [M1-028.md](M1-028.md) | 159 | 192 |

| M1-029 | [M1-029.md](M1-029.md) | 62 | 389 |

| M1-030 | [M1-030.md](M1-030.md) | 13 | 25 |

| M1-031 | [M1-031.md](M1-031.md) | 138 | 135 |

| M1-032 | [M1-032.md](M1-032.md) | 11 | 42 |

| M1-034 | [M1-034.md](M1-034.md) | 43 | 152 |

| M1-035 | [M1-035.md](M1-035.md) | 6 | 21 |

| M1-036 | [M1-036.md](M1-036.md) | 3 | 5 |

| M1-037 | [M1-037.md](M1-037.md) | 2 | 0 |

| M1-038 | [M1-038.md](M1-038.md) | 25 | 29 |

| M1-039 | [M1-039.md](M1-039.md) | 43 | 67 |

| M1-040 | [M1-040.md](M1-040.md) | 24 | 13 |

| M1-041 | [M1-041.md](M1-041.md) | 14 | 38 |

| M1-042 | [M1-042.md](M1-042.md) | 26 | 13 |

| M1-043 | [M1-043.md](M1-043.md) | 21 | 60 |

| M1-044 | [M1-044.md](M1-044.md) | 125 | 132 |

| M1-045 | [M1-045.md](M1-045.md) | 125 | 125 |

| M1-046 | [M1-046.md](M1-046.md) | 154 | 478 |

| M1-047 | [M1-047.md](M1-047.md) | 34 | 237 |

| M1-048 | [M1-048.md](M1-048.md) | 28 | 155 |

| M1-049 | [M1-049.md](M1-049.md) | 14 | 20 |

| M1-050 | [M1-050.md](M1-050.md) | 19 | 122 |

| M1-051 | [M1-051.md](M1-051.md) | 21 | 66 |

| M1-052 | [M1-052.md](M1-052.md) | 2 | 7 |

| M1-053 | [M1-053.md](M1-053.md) | 32 | 206 |

## Excluded records and reasons

Each current excluded record is reproduced in full below so exclusions do not depend on an older report.

### M1-033

HARDWARE_ONLY; no completed built record to package. Settled by the operator's robot run (2026-10-09, firmware 2457, bundle re-analysis/acceptance/hardware/20261009-112625-M1-PACKED-ZERO): the robot accepts packed frames of types 7 (two reliable IMURequests: ACK 3), 8 (two unreliable pings: both timestamps echoed) and 9 (mixed: ACK 3 and the echo), each with a working single-frame control. Observed for these small frames on this firmware; maximum-size frames, other orders and sequence wraparound were not exercised. Before: whether the robot accepts packed frames of types 7, 8 and 9 from the engine (the link check sent none)

```json
{
  "id": "M1-033",
  "subsystem": "M1-transport",
  "title": "Robot-side transport behaviour",
  "location": "cozmo-stack/src/Cozmo.Transport/RobotLink.cs",
  "effect": "this stack assumes robot behaviour the app package cannot show",
  "provenance": "nothing in the package: the robot firmware is not part of it",
  "authority": "a robot, or a capture of the stock app talking to one",
  "evidence": [
    "part A open question 4: whether the robot sets isReply when it echoes a ping, its own resend timing, and whether it accepts packed frames of types 7, 8 and 9",
    "hardware run re-analysis/acceptance/hardware/20260924-112412-M1-LINK (firmware 2457): the robot sent only type-9 frames (1026); it echoed all 1025 pings with isReply clear and our timestamps; it repeated unacked reliable messages after 24..34 ms (median 33.6 ms). Not observed: whether it accepts packed type 7/8/9 frames from the engine"
  ],
  "status": "HARDWARE_ONLY",
  "unresolved": "Settled by the operator's robot run (2026-10-09, firmware 2457, bundle re-analysis/acceptance/hardware/20261009-112625-M1-PACKED-ZERO): the robot accepts packed frames of types 7 (two reliable IMURequests: ACK 3), 8 (two unreliable pings: both timestamps echoed) and 9 (mixed: ACK 3 and the echo), each with a working single-frame control. Observed for these small frames on this firmware; maximum-size frames, other orders and sequence wraparound were not exercised. Before: whether the robot accepts packed frames of types 7, 8 and 9 from the engine (the link check sent none)",
  "hardware_required": true,
  "live_path": true,
  "test": null,
  "verification": {
    "level": "HARDWARE_VERIFIED",
    "bundles": [
      "re-analysis/acceptance/hardware/20261009-112625-M1-PACKED-ZERO"
    ]
  }
}
```

## Reproduction and artifact boundaries

`python re-analysis/research/20261009-M1-verify-packet/build_packet.py` packages the checked-out snapshot after the offline test TRX exists. It only writes in this directory. The native ELF is read locally and is not uploaded. Per-record files are self-contained for manifest, rows, diff text, native transcripts, and relevant test/result text. Complete shared-file histories intentionally contain unrelated hunks; they are explicitly labeled as supersets rather than assigned invented record-level causal ownership.

Test command: `dotnet test cozmo-stack/Cozmo.sln --logger "console;verbosity=normal" --logger "trx;LogFileName=packet-tests.trx" --results-directory .scratch/m1-final-correction-tests --blame-hang-timeout 5m --blame-hang-dump-type mini`, with normal parallel settings and no processor-count or worker-minimum overrides. The supplied TRX records the actual result; existing oracle blocked imports remain blocked.

## Adopted rows and supplier boundaries

M1-046/-047: [final extraction rows](../20261009-M1-final-extraction.md). M1-053: [projection rows](../20261009-M1-053-projection-rows.md). Manager adoption is quoted in the three record files. Root pose, tracking writers, image-result commit and other suppliers remain owned by their named higher-layer records, including their lifetime gaps. Native member allocation bookkeeping is represented by engine-thread-confined managed handle storage. No supplier or M1 record is settled by this packet.

## Offline run and packet validation

Offline run: 3991 passed, 0 failed, 0 not executed. The full solution suite is recorded without result substitution.

`validate_packet.py` checks the included/excluded partition, verbatim manifest values and inventory lines, exact Git patch histories, native transcript bytes against the local ELF, citation byte coverage, and declared-test result matching. Machine-readable results are in `validation.json`. These are packaging checks, not an Opus/source-fidelity verdict.
