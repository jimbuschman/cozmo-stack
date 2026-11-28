# LLM and robot as a team: what Reachy Mini shows, applied to Cozmo

Research, 2026-11-28. Authority 6 (background reading); it settles nothing.
Question (operator): how should an LLM and a robot with his own autonomy work together? Concrete complaint: in
sessions the LLM calls go-home with the charger far out of sight, although Cozmo's marker detector only registers the
charger at about 15 cm, instead of using its own camera view to steer him close and then handing over.

Sources read (not run):
- `reachy_mini_conversation_app` at 622a3a6 (cited as `app:`), `reachy_mini` SDK at a4e5fca (cited as `sdk:`).
- Cozmo stack at b5351866: CozmoGuide.md, CozmoBridge.Commands.cs, CozmoMcpServer.cs, GoHome/Explore/FaceRequests,
  session notes 3–7, QUEUE.md rows LLM-TEAMWORK-1, LLM-OUTCOMES-1, LLM-TURN-CONTEXT-1.

## The short answer

Reachy does not solve our problem. It is a fixed-base head with no map, no navigation, no docking and no
marker-range limit, so it never has to decide "steer him closer first, then hand over". What it does show well:

1. the model's standing instructions sit in the session prompt itself, not in a document it may not open;
2. every tool description says *when* to use it, not only what success looks like;
3. tools run in the background and the model keeps talking; their results arrive later;
4. the robot keeps its own low-level life (breathing, face tracking, speech gestures), the model only toggles it.

Cozmo's gap is mostly the first two. His guide and tool texts describe mechanics and result codes in detail, but
nowhere say "he is the body, you are the eyes and judgement; his goal actions need him close; get him there". The
queued LLM-TEAMWORK-1 already targets this; this report adds where it should go further.

## 1. Division of labour

**Reachy, on its own (no model involved):**
- Breathing idle: an endless BreathingMove starts after a short inactivity unless listening
  (app:`moves.py:8-9`, `moves.py:502-518`).
- Face tracking runs in the daemon: detect, ease the head toward the face, hold through short gaps, recentre after
  2 s of loss (sdk:`daemon/backend/abstract.py:357`, `:775-812`).
- Tracking is paused while the robot speaks, once a face is locked (app:`moves.py:456-472`).
- Speech gestures: a small local MiniLM model picks a gesture from what the user said and what the robot says
  (app:`gestures.py:19-21`; triggered at app:`huggingface_realtime.py:1130`, `:1138-1140`). A reply that moves the
  robot through a tool gets no gesture on top (`:1133-1135`).
- Local idle choice after 180 s without conversation, without asking the model: 60% do nothing, 16% dance,
  16% emotion, 8% head move (app:`conversation_handler.py:42`, `:115-131`; `idle_policy.py:1-11`, `:60-65`).
  Idle results are not sent to the model (app:`huggingface_realtime.py:890`).
- "Safety" means smooth motion only: interpolation and blends, slow settle while listening (app:`moves.py:20-24`).
- One move queue, primary moves mutually exclusive, single control point (app:`moves.py:3-6`).

**The model drives:** which dance or emotion, head direction, whether tracking is on, when to take a picture, sleep.
The SDK's own guidance states the split: "The LLM doesn't control motors directly … LLM decides → Tool call → Queue
move → Control loop executes smoothly" (sdk:`skills/ai-integration.md:23-32`).

**How the prompt states the split:** barely. The default profile says only "The head can move
(left/right/up/down/front)" and "Enable head tracking when looking at a person; disable otherwise"
(app:`profiles/default/profile.md:68`, `:70`). Nothing tells the model what the robot does by itself.

## 2. Tool shape

Default profile tools (app:`profiles/default/profile.md:3-23`), plus the system tools task_status/task_cancel:

| Tool | Granularity | Note |
|---|---|---|
| dance / stop_dance | goal (named move, repeat count) | "Non-blocking" (app:`tools/dance.py:41`) |
| play_emotion / stop_emotion | goal (emotional intent) | (app:`tools/play_emotion.py:233`) |
| camera | sense, with a required `question` | (app:`tools/camera.py:15-36`) |
| move_head | coarse primitive: 5 fixed directions, ±40° yaw / ±30° pitch | (app:`tools/move_head.py:18-38`) |
| sweep_look | fixed scan routine, 14 s | (app:`tools/sweep_look.py:17-20`, `:103-105`) |
| head_tracking | toggle of a robot-side skill | (app:`tools/head_tracking.py:13-27`) |
| idle_do_nothing, go_to_sleep, volume_control, robot_status, remember, forget | misc | |
| task_status / task_cancel | background-task control | (app:`tools/task_status.py:21-35`, `task_cancel.py:20-35`) |

- No continuous motion parameters, and no look-at-point tool, although the SDK has `look_at_image(u, v)` using the
  camera matrix and distortion (sdk:`reachy_mini.py:775-831`). The app never exposes it.
- Descriptions steer with "Use when…" and "Do not use for…": camera (app:`tools/camera.py:15-22`, including "do not
  ask for clarification, call this tool"), head_tracking (`tools/head_tracking.py:14-16`), go_to_sleep
  (`tools/go_to_sleep.py:15-18`), idle_do_nothing (`tools/idle_do_nothing.py:14-17`).
- No guidance on combining tools into a sequence anywhere. No multi-step tool besides fixed routines (sweep_look).
- Background: every model tool call starts as a background task and the conversation continues
  (app:`huggingface_realtime.py:1204-1222`; `tools/background_tool_manager.py:1-6`). `needs_response = False` lets a
  tool skip the spoken follow-up (app:`tools/core_tools.py:66-71`). Progress reporting exists in the manager
  (`background_tool_manager.py:196-222`) but no shipped tool uses it.

## 3. Vision for the model

- The model gets an image only when it calls `camera`. The JPEG is attached as a user-message `input_image` after the
  function result (app:`huggingface_realtime.py:929-950`); the text result just says `image_attached`
  (`:424-431`). No streaming frames.
- The model cannot point at image locations: there is no pixel output and no tool that takes one. Vision is for
  describing ("never invent details", app:`profiles/default/profile.md:67`).
- Motion from vision is entirely robot-side: the daemon tracker follows faces; the model only switches it on
  (app:`moves.py:441-455`). Model vision and robot perception never meet.
- So Reachy has nothing on "the model sees the target, steers the robot until the robot's own perception takes
  over". The SDK's `look_at_image` is the only piece, and it is unused by the app.

## 4. Keeping the model in the loop

- Completion: when a background tool finishes, its result is sent as `function_call_output` and, if the tool wants a
  response or failed, a new model response is requested once all parallel calls are in
  (app:`huggingface_realtime.py:889-918`, `:967-975`). Errors are always surfaced (`:968-970`).
- But "finished" means the move was *queued*: move_head returns `looking left` right after queueing
  (app:`tools/move_head.py:73-76`); sweep_look returns its planned total duration (`tools/sweep_look.py:103-105`).
  No physical confirmation anywhere.
- Status: task_status lists running tasks with elapsed time and optional progress (app:`tools/task_status.py:73-106`);
  task_cancel stops one (`tools/task_cancel.py:56-74`). Tasks time out after one day (`background_tool_manager.py:105`).
- When the robot cannot do it: the tool returns `{"error": …}` (e.g. `tools/move_head.py:78-80`) and the profile
  says "Admit mistakes briefly and correct them" (app:`profiles/default/profile.md:59-60`). No help or next step.

Cozmo is already ahead here: his actions wait for the native result and return `why`, `during` and reports.

## 5. Prompting

The profile is the session instruction (app:`prompts.py:36-71`); memory is prepended. Relevant lines of the default
(app:`profiles/default/profile.md`):
- "You are Reachy Mini: a friendly, compact robot assistant…" (27)
- "If unsure, admit it briefly and offer help" (42)
- "Use tools only when helpful and summarize results briefly." (64)
- "Whenever the user asks to show or express an emotion … call play_emotion in that turn; prior calls and speech do
  not perform it." (65) — a rule against claiming an action without calling the tool.
- "Use the camera for real visuals only — never invent details." (67)
- "The head can move (left/right/up/down/front)." / "Enable head tracking when looking at a person; disable
  otherwise." (68, 70)

The tedai profile describes the body in three lines (app:`profiles/tedai/profile.md:20-22`) and adds "Never describe
or comment on your own actions, tools, or process" (`:32`). No profile tells the model it is the robot's mind working
alongside the robot's own reflexes. The prompts are about voice and character, not teamwork.

## 6. Mapping to Cozmo, ranked by effect on the complaint

What Cozmo has now, in brief:
- The MCP `instructions` string, the only text a client is sure to show the model, is mechanics only: "Read the
  cozmo-guide resource … Check get_state readiness and get_camera_image before acting. Use world object IDs, inspect
  during/why…" (`cozmo-stack/src/Cozmo.Mcp/CozmoMcpServer.cs:125-127`).
- The guide opens with state, map, IDs and events (`CozmoGuide.md:3-16`). The one teamwork hint is buried: "Use the
  camera to pre-position Cozmo for a clear, short approach…" (`:7`). The 15 cm charger fact is at `:94`, far down.
- Tool descriptions say only Success/Failure. go-home reads as a do-everything goal: "Return to the remembered
  charger, scanning in place to rejoin its map when needed" (`CozmoBridge.Commands.cs:98`). No "needs" or "use
  instead" for go-home, explore, mount-charger, search-for-face or come-here (`:95-106`).
- go-home with no anchored memory runs a full 360° look-around and then fails with only a reason and stage list
  (`CozmoBridge.GoHome.cs:33-37`, `:50-54`). The scan cannot help if the charger is beyond marker range.
- The sessions already proved the pattern works: the LLM docked by hand with turns and drives from one good
  sighting (`20261120-llm-session-3-notes.md:140-154`); "drive the robot by hand in short drives, each checked on
  camera … Only then call pickup/stack/mount. Every action done that way succeeded"
  (`20261123-llm-session-5-notes.md:63-68`); "nothing detected does not mean nothing there"
  (`20261120-llm-session-4-notes.md:91-94`).
- Retrying his goal action has a cost in his personality: repeated mount failures triggered ReactToFrustrationMajor,
  which drove him away mid-retry (`20261126-llm-session-6-track-face-notes.md:79-80`).

### Rank 1. Put the teamwork rule where the model always sees it (extends LLM-TEAMWORK-1 part 1)

Reachy puts its behaviour rules in the session prompt itself (app:`prompts.py:36-71`). Cozmo's guide is a resource
the model has to choose to read. TEAMWORK-1 adds a "Working together" section to the guide; that is right but not
enough. Also add three sentences to the MCP `instructions` string (`CozmoMcpServer.cs:125`), for example:

> You are Cozmo's eyes and judgement; he is the body and has his own reflexes and personality. His own perception is
> short-range: he registers the charger only within about 15 cm, cubes a little farther. When you can see a target
> in get_camera_image that he has not reported, steer him toward it with short turn-in-place and drive-straight
> steps, looking after each, and hand over to his own action (mount-charger, pickup, turn-to-face) once he reports it.

Keep the guide section as TEAMWORK-1 specifies; make it the first section (before `CozmoGuide.md:3`).

### Rank 2. Tool descriptions that say when, and when not (new; not in any queued row)

Copy Reachy's "Use when … Do not use for …" pattern (app:`tools/camera.py:15-22`, `tools/go_to_sleep.py:15-18`) into
the goal tools, keeping the existing Success/Failure text after it:
- go-home: "Use when the charger is in chargerMemory with anchored=true, or when you cannot see it either. If the
  charger is visible in your camera image but not in knownChargers, steer him to within about 15 cm first, then call
  mount-charger."
- mount-charger: "Use once knownChargers lists it from a fresh sighting, square-on and close. Do not retry more than
  once without moving him: repeated failures raise his frustration and he may drive off."
- explore: "Use to map unknown ground when you have no target in view. Not for reaching something you can see."
- search-for-face / come-here: "Use when no face is in your image. If you can see the person, turn him toward them
  first."
- pickup / roll / stack: "Pre-position square to the cube's marked face with short moves when clutter is near."

These texts are host-side LLM wording, no engine behaviour; every number quoted is already sourced in the guide
(`CozmoGuide.md:94`, `:96`, `:35`).

### Rank 3. go-home refuses fast with help instead of a blind 360 (extends LLM-TEAMWORK-1 part 3)

TEAMWORK-1 adds a `help` object on failure. Extend: when go-home has no anchored charger goal at start
(`GoHome.cs:53`), offer the LLM the choice before spending a long scan. Option A (preferred, least change): keep the
scan but cap the reported `help` to say "his scan finds the charger only within about 15 cm; look with
get_camera_image and steer". Option B: a `scan: false` argument that returns the help immediately. Same for explore's
`interrupted` result (`CozmoBridge.Explore.cs:89-97`). Help values stay from existing state only, as TEAMWORK-1 says.

### Rank 4. Turn toward an image point (LLM-TEAMWORK-1 part 2, confirmed and extended)

Reachy's SDK has the exact primitive, `look_at_image(u, v)` from the camera matrix and distortion
(sdk:`reachy_mini.py:775-831`), which supports TEAMWORK-1's design. Reachy's app never exposes it, so there is no
evidence on how well a model uses it. Two extensions worth a later row, not this one:
- Return, with the angles, the expected new pixel position of the point, so the model can check its own aim on the
  next image.
- An `approach-image-point` (turn, then drive a capped distance toward a floor pixel, from the exposure head angle
  `headAngleRad` and the camera pose) would turn three or four tool calls per step into one. It needs the engine's
  camera height and calibration cited before building; it is a C2 host extension, never an engine change.

### Rank 5. Don't block the model during long goal actions (new, from Reachy's background tools)

Reachy starts every tool as a background task and injects the result when it ends
(app:`huggingface_realtime.py:1204-1222`, `:889-975`). Cozmo's go-home and explore hold the MCP call for up to
minutes, and the session-6 notes show the LLM cannot reach anything while its own call blocks
(`20261126-llm-session-6-track-face-notes.md:37-39`). track-face already returns on admission. Proposal: publish
go-home's existing stage strings (`GoHome.cs:28-32`) and explore's leg results as significant events, so the channel
and `read_events` show progress, and the LLM can cancel and take over (for example on "looking around in place" when
it can already see the charger). Converting go-home to return on admission is a larger change; events first.

### Rank 6. Who owns the body while the LLM steers (new, guide text)

Reachy only runs its idle when nothing else moves and after 180 s quiet (app:`conversation_handler.py:121-124`).
Cozmo's Freeplay and reactions can take the body mid-steer (frustration drive-off, session 6 above). Add one guide
line: while steering him step by step, hold a named `lock-reactions` for the triggers that fight the task, or turn
Freeplay off, and give them back after handover. Do not lock cliff protection.

### Points where Reachy has nothing to add

- Outcomes (LLM-OUTCOMES-1): Reachy reports a move as done when it is queued (app:`tools/move_head.py:73-76`). Cozmo
  already waits for the native result. No change to that row.
- Turn context (LLM-TURN-CONTEXT-1): Reachy gives the model no state summary at turn start, only memory facts
  (app:`prompts.py:68-71`). No change to that row; its guide line should sit in the new first section.
- Handover between model vision and robot perception: none in Reachy.

### What does not transfer to a mobile robot

- Stewart-platform head poses, body yaw blending, antennas, breathing, speech-gesture model: Reachy body specifics.
  Cozmo has Freeplay, idle animations and reactions for that.
- Random idle tool policy: Cozmo's Freeplay is the engine's own and must stay in charge.
- "Safety" as smooth interpolation: a mobile robot's safety is cliffs, falls and unexpected movement, which Cozmo's
  engine owns. Reachy has no equivalent.
- sweep_look as a head-only scan: Cozmo must turn his body; Hiking_LookInPlace360 is his native scan.
- Daemon face tracking as a new subsystem: Cozmo's engine already tracks faces natively (track-face).

## Uncertainty

- No session has yet tested whether text in the `instructions` string changes Claude's choices more than the guide
  does; the next LLM session should compare.
- The 15 cm figure comes from one setup (`CozmoGuide.md:94`); range varies with pose and light.
- Reachy findings are from source only; neither robot was run.
