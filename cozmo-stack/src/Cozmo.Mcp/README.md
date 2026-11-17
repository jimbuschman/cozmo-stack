# Cozmo MCP console

`cozmo-mcp` exposes the existing `Cozmo.Bridge` commands to a local Claude client.
It connects using the same `HardwareEngineOptions` and readiness gate as
`m15-cube-hw`. No setup questions are read from stdin: stdin/stdout carry MCP,
and diagnostics go to stderr. Only the operator runs it with a robot.

## Build and configure on Windows

The examples assume the checkout is `C:\cozmo-stack` and the extracted resources
are `C:\cozmo_resources`. Replace those two roots with your actual paths.
Keep the build inside the checkout so the existing native library lookup finds
`resources\lib\armeabi-v7a`. Use the same extracted files as `m15-cube-hw`.
Build before starting Claude; do not use `dotnet run` as the MCP command.
Launch the built `.exe`, as below: its `CETCompat=false` setting is required by
the existing embedded emulator (`UnicornLongjmp.cs`); a separate `dotnet.exe`
host can have different process mitigation settings.

```powershell
dotnet build C:\cozmo-stack\cozmo-stack\src\Cozmo.Mcp\Cozmo.Mcp.csproj -c Release
& C:\cozmo-stack\cozmo-stack\src\Cozmo.Mcp\bin\Release\net9.0\cozmo-mcp.exe --resources C:\cozmo_resources --validate
```

`--validate` checks options/resources and exits without constructing an engine,
connecting, or creating a session bundle. Normal startup connects immediately;
join the robot's Wi-Fi first. The default robot IP is `172.31.1.1` (`--robot-ip`
overrides it). For authored animation sound and speech, append
`"--banks", "C:\path\to\SoundBanks"` to the
argument list, using the same bank directory as `m15-cube-hw --commands`.

For Claude Desktop, merge this entry into
`%APPDATA%\Claude\claude_desktop_config.json`, then restart Claude:

```json
{
  "mcpServers": {
    "cozmo": {
      "command": "C:\\cozmo-stack\\cozmo-stack\\src\\Cozmo.Mcp\\bin\\Release\\net9.0\\cozmo-mcp.exe",
      "args": [
        "--resources", "C:\\cozmo_resources"
      ]
    }
  }
}
```

For Claude Code, put the same JSON in the checkout's `.mcp.json` (local machine
configuration; do not commit it). Start `claude` there and use `/mcp` to check
the `cozmo` server. Run only one client/robot connection at a time.
These configuration formats follow the official
[Desktop guide](https://github.com/modelcontextprotocol/docs/blob/main/quickstart/user.mdx)
and [Claude Code MCP reference](https://code.claude.com/docs/en/mcp).

## Tools and results

There are 24 tools: the bridge's 21 commands, with their unchanged argument
schemas, plus `get_state`, `read_events` and `list_animations`.

Start by asking Claude: “Use Cozmo get_state and list_animations. Tell me whether
he is connected and ready.” Engine startup runs in the background; tools are
listed immediately, and state/admission reports retain the engine's real readiness.
After startup, try “Set his head to 0.2 radians and read his events.”
Object IDs are engine **world** IDs from the state/event feed.

Tool results include both a text JSON block and `structuredContent`. Action
results retain `admitted`, `engineResult`, `engineResultName`, `detail` and
`ownerRetired`. An admission failure, native failure or invalid arguments sets
MCP `isError`. Freeplay is a posted message; face rename/erase are reports, so
they do not invent a native action result. Poll `read_events` with its returned
`cursor`; `gap` reports buffer eviction. `list_animations` lists loaded mapped
triggers and clip names without choosing/playing one. A pending action does not
block state/event requests. MCP cancellation is passed to the bridge; tracking
can remain active until cancelled. Stopping the client/closing stdin cancels calls
and disposes the engine/connection.

## Session evidence

Each normal process writes
`re-analysis/acceptance/hardware/<yyyyMMdd-HHmmss>-LLM-SESSION/` under its checkout:

- `events.jsonl`: full engine logs, engine-to-game bytes, MCP requests/replies,
  connection reports and startup gate, with elapsed and UTC timestamps.
- `frames.jsonl`: raw inbound/outbound transport frames.
- `messages.jsonl`: decoded messages using the hardware analyzer's retransmission
  and connection-epoch handling, written when the session closes.
- `stack-log.txt`: stack diagnostics, including visible MISSING messages.
- `result.json`: build commit, assembly hash, arguments, start/end, final state
  and host session status. “closed” describes the process, not a hardware pass.

Use `--out <absolute-session-directory>` to choose another empty directory.
Do not commit session bundles. A hard kill can leave the initial result and raw
streams without the final decoded export; the raw frames remain available for
the existing offline analysis tools. No hardware acceptance is claimed here.

## Protocol evidence and offline tests

The official SDK was unavailable in the local package cache (`NU1101`), so this
implements the queue-authorized direct fallback: newline JSON-RPC over stdio,
initialization/version negotiation, tools/list, tools/call, ping and cancellation.
Primary protocol references are the MCP
[transport](https://modelcontextprotocol.io/specification/2025-11-25/basic/transports),
[lifecycle](https://modelcontextprotocol.io/specification/2025-11-25/basic/lifecycle)
and [tools](https://modelcontextprotocol.io/specification/2025-11-25/server/tools)
specifications. Supported versions: 2024-11-05, 2025-03-26, 2025-06-18 and
2025-11-25. It advertises only tools capability; no resources/prompts/subscriptions.

`McpServerTests` drives a serialized in-process client through initialization,
the 24 schemas, real head motor command/ack/result, Freeplay, state, engine face
events/cursor polling, pending-action cancellation, protocol/tool errors and
session frame/message export. Existing bridge tests cover the command mappings.
Real Claude client discovery and robot motion remain the operator's check.
