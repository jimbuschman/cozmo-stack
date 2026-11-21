# EVENT-FILTER-2

The Claude channel now names the native triggers and affected behavior, shortens face observations, leaves the initiating client's completions in its tool result, and pushes falling and landing. Compact action windows summarize matched animation start/end or abort pairs. Full MCP reads and the C# and loopback TCP feeds retain the original records and significance flags.

| Item | Evidence and check |
| --- | --- |
| Reactions | The engine pair is old/new at E2G `0x88` (M8-012, `0x005A20AA..0x005A20D6`, `0x005A2EE8`). The host snapshots the manager's actual old/new behavior IDs at the send edge and renders the same shipped names as `get_state`. The production CubeMoved replay checks the formatted start alongside the unchanged two-byte report. |
| Faces | The existing `0x46` observation supplies the signed ID, name, and world pose. The current published robot pose supplies the reference (M1-053), and distance and bearing are null when origins differ. The feed folds a temporary ID only if a single nearby approach matches within 200 mm in the same origin and the last observation was 0.3 to 10 seconds ago. An engine ID change takes precedence. A named observation starts a new recognition if the identity or name changed, or if the approach expired. |
| Completions and falls | The push view filters the `0x5A` completion by the existing caller token, including between two bridges sharing one robot. Polls and action windows can still read the completion. Falling and falling-to-treads come from the existing OffTreads state change. |
| Animations | Compact `during.animations` pairs existing CA/CB or abort events by playback tag and gives clip name, start/end timestamps and cursors, and result. Unmatched edges remain individual. No action, sensor or playback logic changed. |

The archived session 4 channel originally sent 104 events and 44,884 UTF-8 bytes of notification content. The controlled comparison feeds the same archived sequence into both projections and measures event-array bytes without transport wrappers. It uses the recorded `0x87` behavior IDs for `0x88` reaction starts and retains the active name for the ending edge. The before/after counts below describe that replay, whose report-ingress inputs differ from the archived channel transport count.

## Verification

Build `9aae2c5b` passed all 8,835 tests in 59.02 minutes with the local recordings enabled. All 27 focused event and compatibility cases passed.

Session 4 replay significant events: 106 / 39415 UTF-8 bytes before, 103 / 30000 UTF-8 bytes after (event arrays, excluding transport wrappers). The fidelity check passed for 765 records.
