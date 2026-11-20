# SESSION-CHECK-1 handoff

`re-analysis/tools/session_check.py` reads an ordinary MCP LLM-SESSION bundle and prints a Markdown report for Scope A and every Scope B class. It reuses the command, event and personality round declarations from `CommandsHw.cs`, `CommandEventsHw.cs` and `PersonalityHw.cs`. The skills and Freeplay checks follow `M15CubeHw.cs`. Each verdict includes its source. Command checks match the MCP result to the relevant E2G report, or the defined local scalar report for ground placement. The 0x87 behavior and 0x88 reaction checks count entries, as the personality round does. Operator observations and physical appearance remain outside the automated verdict.

```powershell
python re-analysis/tools/session_check.py C:/path/to/20261120-094700-LLM-SESSION
python -m unittest discover -s re-analysis/tools -p test_session_check.py
```

## Local reports

| Session | Passed | Seen without a pass | Not exercised |
| --- | ---: | ---: | ---: |
| [20261119-093050](session-check-1/20261119-093050.md) | 17 | 3 | 53 |
| [20261119-094833](session-check-1/20261119-094833.md) | 25 | 12 | 36 |
| [20261119-182433](session-check-1/20261119-182433.md) | 27 | 8 | 38 |
| [20261120-094700](session-check-1/20261120-094700.md) | 22 | 4 | 47 |

Counts refer to individual checks, including the parts of compound Scope A rows. The four archived copies retain `running` in result.json. The reports use the captured signals and give incomplete sequences a seen verdict. When the decoded message stream ends early, the checker looks for colour image chunks in the remaining raw frames. The 11/19 evening session confirms colour processing and an active Freeplay loop. It lacks the before-command, resume and off sequence required to pass Freeplay control. The 11/19 morning teach succeeded. The corresponding recognition check lacked a matching public report within the original 20-second window. Session 4 confirms a wait followed by admission and records repeated unsuccessful charger mounts.

Each report indexes every MISSING line by its original stack-log line number. The checker attributes a marker using the check’s observation window and source address, record or path name. Named construction gaps can also attach from startup. Unattributed lines remain visible for the manager. The reports do not settle fidelity records or Scope items.

Only the checker, synthetic tests and derived Markdown are committed. The raw session bundles stay local.

## Verification

Build `5a0da2f9` passed all 8,820 tests in 48.91 minutes with the local recordings enabled. All eight synthetic session-check tests passed. The fidelity check passed for 765 records.
