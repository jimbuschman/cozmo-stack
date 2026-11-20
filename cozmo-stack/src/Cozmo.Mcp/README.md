# Cozmo MCP console

`cozmo-mcp` exposes the existing `Cozmo.Bridge` commands to a local Claude client.
It connects using the same `HardwareEngineOptions` and readiness gate as
`m15-cube-hw`. No setup questions are read from stdin: stdin/stdout carry MCP,
and diagnostics go to stderr. Only the operator runs it with a robot.

Stderr also narrates posture, reaction and behavior changes, animation robot
start/end/abort reports with clip duration and tracks, action results, and cube
and face events. Track labels describe the clip's authored tracks; they do not
claim that a motor physically moved. These lines and clip identities are saved
as `live-narration` events. MCP stdout remains JSON-RPC only.

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

There are 28 tools: the bridge's 24 commands, with their argument
schemas, plus `get_state`, `get_camera_image`, `read_events` and `list_animations`.
Each description states success and failure meaning. The static `cozmo-guide`
resource at `cozmo://guide` contains the [operating guide](CozmoGuide.md), including
the complete `get_state` field reference. Discover it with `resources/list`, then
read it with `resources/read` and `uri=cozmo://guide`. It is available after MCP
initialization even while the robot is starting.

Needs start full and stay full by default. This supplies the app's refill input
after the stored needs load, then uses the engine's global needs pause (decay,
action deltas/rewards and local notifications pause together). It writes the PC
needs copy through the native pause handler and adds no robot NV write.
Append `"--needs", "real"` to the configuration arguments for the original decay.
At runtime, `set_needs_mode` takes `{"mode":"full"}` or `{"mode":"real"}`.
In real mode, `set_needs` takes `{"repair":0.7,"energy":0.8,"play":0.9}`;
native clamping/reporting still applies. `get_state.needs` reports the mode,
loaded/applied status, pause and levels. These host inputs have no native action
result code; their tool result confirms the setting/report instead.

Start by asking Claude: “Use Cozmo get_state and list_animations. Tell me whether
he is connected and ready.” Engine startup runs in the background; tools are
listed immediately, and state/admission reports retain the engine's real readiness.
After startup, try “Set his head to 0.2 radians and read his events.”
Object IDs are engine **world** IDs from the state/event feed.

Tool results include both a text JSON block and `structuredContent`. Action
results retain `admitted`, `engineResult`, `engineResultName`, `detail` and
`ownerRetired`. An admission failure, native failure, owner retirement, no-report, invalid arguments
or observed animation truncation sets MCP `isError`. Read `why` and `during`;
an unknown face ID can still return native SUCCESS with
`report.facePoseAvailable=false` and no turn. Freeplay is a posted message; face rename/erase are reports, so
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
2025-11-25. It advertises tools and a static resources capability. The only resource is
`cozmo://guide`; templates are empty and subscriptions/list-change notifications
are not advertised. Resource shapes and errors follow the official
[resources specification](https://modelcontextprotocol.io/specification/2025-11-25/server/resources).

`McpServerTests` drives a serialized in-process client through initialization,
the 28 schemas and the embedded operating guide, real head motor command/ack/result, Freeplay, state, engine face
events/cursor polling, pending-action cancellation, protocol/tool errors and
session frame/message export. Existing bridge tests cover the command mappings.
Real Claude client discovery and robot motion remain the operator's check.

## Seeing and waiting

Call `get_camera_image` with `{}` for a PNG image content block plus metadata:
robot timestamp, exposure-time head angle (null if unavailable), and any matching
face, cube or charger rectangles for the newest received image.
`{"annotate":true}` selects the newest vision-processed image and draws its green
face boxes and amber object boxes. Compare `imageId` to `latestReceivedImageId`
to see when the processed image is older. Before vision has consumed a frame,
the tool returns the newest image with `visionProcessed: false`. Reads use the
existing camera stream and vision settings.

Action responses include `during`, an event page covering delivery from invocation
through completion. It uses the same cursor/gap format as `read_events`, including
animation robot start/end/abort reports with clip names, off-treads transitions,
and the existing behavior, reaction, face and object reports. The feed is bounded:
if `gap` is true, older events were evicted. Poll during long-running actions to
retain the full history.

`read_events({"since":cursor,"waitMs":30000})` waits until an event arrives or the
timeout expires. The range is 0–30000 ms; the default 0 returns immediately.
Concurrent waits do not consume one another's events. MCP cancellation cancels the
wait. A regular tool call can run while another call waits. Use
`{"since":cursor,"waitMs":30000,"significantOnly":true}` to wait only for
significant events. The `significant` flag is on each event record.
The default remains all event types. Reuse the returned cursor after a timeout too.

`get_state` includes `isCharging`, `knownChargers` (world ID and pose state), and
`relative` coordinates for located cubes, chargers and observed faces. Coordinates
are in millimetres, in MAIN robot axes; `distanceMm` is planar distance and
`bearingDeg` is atan2(y,x), positive to the robot's left. A null relative pose means
the object and robot have no common pose root or no robot owner is available.
Enrolled faces that are not currently observed retain their name but have no
invented distance.

## Push events to a local host

Start `cozmo-mcp` with `--event-stream-port 8765` to open a read-only IPv4
loopback TCP stream at `127.0.0.1:8765`. Use a trusted local machine because
local clients can read the active feed. Send one UTF-8 JSON subscription line
terminated by LF within five seconds. A filtered subscription looks like this.

```json
{"since":0,"types":["face-seen","face-recognised","reaction-changed"],"significantOnly":true}
```

All fields are optional. An empty object starts at the live edge with all event
types. `since: 0` asks for all retained records. Each output line has the same
`cursor`, `oldestCursor`, `gap` and ordered `events` as a full MCP page, with up
to 64 records. A quiet subscription waits without sending empty pages. Resume
with the last page cursor after reconnecting to the same process. On `gap: true`,
refresh state because the feed evicted events. A malformed subscription or a
future cursor receives one `error` line before the connection closes.

A C# host with an existing `CozmoRobot` can reference `Cozmo.Bridge` and follow
the same feed in-process. This example starts at the live edge and stops on
cancellation. The host retains ownership of `robot`.

```csharp
using Cozmo.Bridge;
using Cozmo.Robot;

static async Task WatchCozmo(CozmoRobot robot, CancellationToken stop)
{
    using var bridge = new CozmoBridge(robot);
    try
    {
        await foreach (var page in bridge.StreamEventsAsync(
            significantOnly: true, token: stop))
        {
            if (page.Gap)
                Console.WriteLine(bridge.GetState().GetRawText());

            foreach (var item in page.Events)
                Console.WriteLine($"{item.Cursor} {item.Type} {item.Payload}");
        }
    }
    catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
}
```

## Claude Code channel

Add `--channel` to the Cozmo server arguments in Claude Code's MCP
configuration. For a custom server named `cozmo`, start an interactive session
with `claude --dangerously-load-development-channels server:cozmo`. Channels are
a research preview for interactive sessions. Use `read_events` or the local
stream in print (`-p`) and SDK sessions.
[Claude Code channels reference](https://code.claude.com/docs/en/channels-reference)

The channel starts after MCP initialization. Its notifications carry a JSON
event page in `content`, with up to one notification per second. Bursts share
a page in cursor order. A single event can carry `event_type`, `face_id`,
`object_id` and `reason` string metadata. The local listener and channel are
independent and both start disabled.

The default significance list covers newly seen faces, cubes, chargers and
other located objects, face recognition once per face per minute, object pose
loss, cube taps and movement, pickup, return to treads from pickup, on-back,
cliff detection, unexpected movement, reaction transitions, the bridge's own
action completions, sleep and cancellation, robot disconnect, low battery, and
charger contact changes. Face deletion and object pose loss let a later sighting
count as new. Ordinary repeat sightings stay in the full feed.
Use `--significant-events face-recognised,reaction-changed` to replace the
list at startup, or pass `significantEventTypes` when constructing a bridge.
The setting applies to all readers in that process.

## Firmware animation guard

The default animation budget is 7,370 serialized bytes, an operator-approved
empirical guard measured from firmware 2457 captures. It is not a claim about the
firmware allocator's capacity. `--animation-budget-bytes 8192` selects the original
engine constant; the same flag works with `cozmo-conformance m15-cube-hw`. Values
must be 1–30000. The C# setting is `CozmoEngineOptions.AnimationStreamBudgetBytes`.
Neither the 14-frame gate nor any other streaming step changes.

Animation responses preserve `engineResult` / `engineResultName`. If a BufferFull
trace occurs during the call, or its started playback ends before a successful
EndOfAnimation send, `truncatedByRobot` is true, `playbackIssues` gives the observed
tag/name and raw trace arguments, and MCP `isError` is true even if the engine
reports SUCCESS. Narration says “cut off by robot buffer”. A prior host abort is
not misreported as an unexpected robot end. Unknown trace-to-stream identity stays
null. These facts diagnose a cutoff; they do not assert how much was physically
heard or seen.

## Preparing the next LLM session

Read `cozmo-guide` before acting. Look with `get_camera_image`, choose current
world IDs from state/events, and use `read_events` with its returned cursor while
action calls are pending. The [guide](CozmoGuide.md) explains the approximately
7.5-degree / 21-mm recorded charger approach checks and their geometry/axis limits,
unknown-face success without motion, `why`, `during`, animation cutoff reporting,
and the face enrollment/rename/erase confirmation semantics. Its source references
point to the handoffs that established those details. A connected client's actual
robot use remains the operator's check.
