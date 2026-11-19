# LLM-SESSION-2-READY-1 — READY FOR CLAUDE

SCOPE.md A: The connection for an LLM. Host wording and a static MCP resource;
no controller, action admission, native result, geometry or robot traffic changes.

All 28 tool descriptions now state success and failure meaning. The descriptions
and guide explain `during`, `why`, unavailable checks, animation truncation with
unchanged native SUCCESS, unknown-face SUCCESS without turning, and face operation
confirmation/persistence/observation timing. `get_state` has a complete field/unit
reference, including world/radio/factory IDs, nullable relative poses, stored names
and needs flags. The README's tool count and supported capabilities are current.

The running binary embeds CozmoGuide.md as `Cozmo.Mcp.CozmoGuide.md`; its read-only
resource is named `cozmo-guide`, URI `cozmo://guide`, mimeType `text/markdown`.
Initialization advertises resources and directs the client to it; resources/list
and resources/read expose it after initialization, independently of robot readiness.
Templates are empty; subscriptions/list-change notifications are not advertised.
Only the exact registered URI is read, never an arbitrary filesystem path.

## Source and meaning

| Detail | Existing authority |
| --- | --- |
| Charger approximately 7.5 degrees / 21 mm | HANDOFF-MOUNT-CHARGER-LLM-1.md, recorded final-check table: strict 7.499427-degree yaw limit and 21.423-mm Y threshold. Thresholds depend on generated geometry. The guide preserves that distinction and MAIN-parent comparison axes from HANDOFF-MCP-ACTION-WHY-1.md; it does not prescribe a charger-relative sideways correction. |
| Native result and why/during | HANDOFF-MCP-ACTION-WHY-1.md and HANDOFF-LLM-SENSES-1.md. Results/diagnostic captures and bounded feed are unchanged. |
| Unknown face | HANDOFF-LLM-SESSION-FINDINGS-1.md: native no-pose SUCCESS path, actual report.facePoseAvailable and no turn. |
| Truncation | HANDOFF-ANIM-BUDGET-FIRMWARE-1.md: retain native SUCCESS, report observed cutoff with truncatedByRobot/playbackIssues and MCP isError. |
| Wait interval, camera and coordinates | HANDOFF-LLM-SENSES-1.md: waitMs 0..30000, exact timestamp annotation, nullable exposure head, MAIN-relative coordinates. |
| Face operation semantics | HANDOFF-LLM-TEACH-FACE-1.md: enrolled/renamed/erased with IDs and names; separate NV completion, spoken response and fresh observations. |
| Exact state fields | Existing CozmoBridge.GetState, CozmoBridge.GetCameraImage and HostNeedsStatus. The client test checks each actual root state property appears in the guide. |
| Resources envelope/capability/errors | Official MCP 2025-11-25 resources specification: https://modelcontextprotocol.io/specification/2025-11-25/server/resources. Standard list/read shapes, no optional subscription flags, unknown URI -32002 and bad parameters -32602. |

The guide starts with checking readiness and camera, choosing current world IDs,
waiting with read_events since/waitMs, and inspecting during/why before another
action. It directs the operator to line up square to the charger and verify fresh
contact/charging state. No new automatic physical acceptance condition is invented.

## Validation

Serialized in-process client and affected bridge groups: **61/61**. Three new cases
cover capability/discovery/read of the embedded guide and actual state fields,
initialization/unknown URI/parameter errors, and a lint that fails if any advertised
tool omits explicit Success/Failure result meaning. Existing argument schemas,
real motor command/ack/result, cancellation, camera, event polling, native failure
snapshots and robot-cutoff tests continue to pass.

Normal build: zero errors. **Full clean: 8,749/8,749**, all 17 private cases
enabled, MCP server closed, 56.4277 minutes on **2e67f1b2**.
Gate key: `4b9e7064921b5109f31161db72cb2d2766454d84`.
The manager can reuse this full run only for the same merge key, per AGENTS.md.
Log: `.scratch/m13-guide-full-2e67f1b2.log`; local replay diagnostics:
`.scratch/guide-full-face.json` and `.scratch/guide-full-llm-events.json`.

No fidelity record,
inventory, SCOPE.md or PROJECT_STATE.md is changed. No hardware script/run/bundle
is created or committed. Actual client/robot use stays with the operator.
