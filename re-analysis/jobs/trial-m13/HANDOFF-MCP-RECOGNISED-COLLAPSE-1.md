# MCP-RECOGNISED-COLLAPSE-1 handoff

Compact action windows and `read_events` pages group repeat `face-recognised` entries by face ID and name. The group counts cover the repeats and carry their first and last timestamps and last payload. The first recognition for each face/name and every name change keep their full event, including a change back to a name already seen. Identity changes and other non-sighting events keep their full structured payloads.

The captured `094833` enrolment call now fits in one **17,147-byte** reply. Its first Jim recognition at robot timestamp 100530 and both face ID changes are full events. One group covers the 70 repeats (100595–113485). The full-mode replay retains all 289 events, including all 71 recognitions. Across the 52 recorded windows, the largest compact action was 17,998 bytes and the largest compact read was 18,368 bytes in the focused replay.

A complete compact enrolment reply exceeded the wire budget when sent twice. For this size case, the full compact window is in `structuredContent`, and the text includes the action result, face events and sightings with `during.detailsInStructuredContent: true`. Other oversized windows can still use a continuation. Continuation filters preserve access to recognition and identity changes. The guide documents the text marker and both full modes.

The change is in MCP presentation. It uses the existing engine report provenance. Local capture copies were used for replay.

Full suite **8781/8781** on `881c7022` in 51.64 minutes, including local replays. Focused tests 31/31. Gate `859d5bbbba95497523fb4790f4ec7a552e945a11`. Fidelity and diff checks passed.
