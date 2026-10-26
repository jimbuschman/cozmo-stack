# Operator decision: extra configuration APIs

Limited to the drive-off pilot's public configuration adapter. Other independent source reading/checking continues. This is not a hardware wait, external Claude approval request or blanket pause.

`BlockConfigurationManager.ConfigurationSeen`, `FirstSeenSec`, and the public cached midpoint adapter currently describe a host all-pairs snapshot with per-arrangement timestamps. A contact observation can trigger that rebuild and public event, so it affects observable API delivery (AGENTS.md item3).

Narrow primary read and independent confirmation: engine BehaviorOnConfigSeen005DB2D4..005DB396 tracks per-type cache COUNT increases, stores the current float timestamp when count grows, and compares dt<4.99999 plus abs(stamp-now)<1e-5. It does not establish the existing per-arrangement C# event/timestamp API. No current manifest/inventory policy for that extra API was found. This is not evidence that an API is unnecessary or safe to delete.

AGENTS.md says: "Excluding a robot behaviour or a C# API feature needs the operator." Codex cannot silently remove it, change its advertised semantics or certify the host adapter as recovered engine behavior. The earlier CM13-05 partial-slice preservation was explicitly unverified, not an operator policy or whole-behaviour acceptance.

Complete decision requested asynchronously2026-10-26 (supersedes the earlier question naming only the event/time APIs):

1. Retain the WHOLE public configuration adapter as an explicitly documented standalone extension, separate from engine fidelity (recommended to preserve existing API users); record exact host semantics and keep them from feeding native engine state/claims.
2. Change the API to source-backed native IDs and explicit geometry availability, accepting compatibility changes. The source GetBaseInteriorMidpoint00617A8C computes live geometry on demand and can fail; it does not establish a cached nonnullable midpoint field or a fallback zero/old host average.
3. Remove the unsupported public adapter.

The midpoint boundary was independently identified after the first narrow question: native Base allocates24bytes containing IDs, no cached Vec3. A live helper call changes the current snapshot timing/value contract, and an orphaned cached base may have no available geometry. Codex has not silently treated the two-API question as authorization for the entire adapter.

Pending decision: preserve existing API bytes/behavior; do not implement a new policy, remove a feature or declare the whole pilot finished. Complete independent engine-path research and other authorized work. Claude will receive any proposed policy/record changes separately; Codex changes no manifest statuses/approvals. Hardware never blocks the next behaviour.
