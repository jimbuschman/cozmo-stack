# EVENT-PUSH-1 handoff

Implemented the SCOPE A event feed for an LLM host. `CozmoBridge.StreamEventsAsync` follows the same
bounded cursor feed as `read_events`. The server offers optional LF-delimited JSON on IPv4 loopback
and an independent `--channel` for Claude Code. `read_events` accepts `significantOnly` with its
existing type filters, cursor, and wait. The stream and channel send up to 64 ordered records per
page. Channel output waits for MCP initialization and delivers at most one page per second, so
a burst can arrive in one notification. The [server README](../../../cozmo-stack/src/Cozmo.Mcp/README.md)
has the setup and C# example. [Claude Code channels reference](https://code.claude.com/docs/en/channels-reference)

The bridge assigns significance at append time. The host allowlist, face recognition minute,
sighting deduplication and delivery rate are host policy. Face and object reports use engine tags
0x46/0x47/0x4B and 0x44/0x52. Cube motion and taps use the existing 0x0F/0x10/0x13 app feed.
Physical sources are off-treads 0x00512092..0x005120D0, cliff 0x00535548..0x005356E6 and unexpected
movement 0x0063E932..0x0063E962. Charger contact comes from RobotState status, the input to
SetOnCharger 0x005119AA..0x00511C14. Low battery projects the engine charging-light comparison
off charger at 0x00631B94..0x00631BD8. The engine reaction report is 0x88. Sleep uses 0x2D and the
bridge's existing PauseManager cancellation event. Disconnect keeps its existing report.
The caller token follows admitted bridge motion, speech and put-down roots through the existing
D3/D6 action completion path (0x0053FA4C..0x0053FAB2). It stays in host memory and is absent from the
robot, CLAD and JSON completion payloads. No fidelity status was changed.

`EventPushRecordedReplayTests` takes the local 20261119-182433-LLM-SESSION bundle via
`COZMO_EVENT_PUSH_BUNDLE`. It replays recorded report bodies, off-treads log transitions, and
battery/contact state and compares complete significant JSON records in cursor order across all
three deliveries. The older bundle has scalar action evidence and no caller token. The fixture uses
the recorded external action tag allocation and put-down reply to attribute roots. It reconstructs
cube events from their world IDs and timestamps. It has only textual evidence of the evening's
unexpected-movement reports, so those payloads are covered separately through the native detector in
`PhysicalReactionRecordedReplayTests`. `BridgeEventPushTests` exercises real caller admission,
isolates a second bridge's view of that completion, and checks filtering, TCP resume/gaps, power
transitions, and the Claude capability and notification shape.

The robot and an interactive Claude Code session were not used for this handoff. The local bundle
stays outside Git.

Build `061e1970` passed the full Debug suite (8,791 of 8,791 in 57.13 minutes) and
`fidelity.py --check`. The CLI also accepted the new options under `--validate` with no
connection and rejected port 65536.

Follow-up from DOCK-CHARGER-POSE-1. The derived message export ends at 300 seconds. The replay now reads
contact, power and cliff inputs from the full datagram capture. The three deliveries agreed on all
151 significant records and included the charger contact at the end of the manual mount.
