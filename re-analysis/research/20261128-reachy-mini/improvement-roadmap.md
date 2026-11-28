# Improvement roadmap

Research snapshot, 28 November 2026. Cozmo source is `1bd44f259ee4d4c75ee6182c75a25920669e4d86`. Reachy conversation app is `622a3a67582a1eb45c82b51251fa9f61a999b51d`. Cozmo paths below are relative to the root of its repository, and Reachy paths are relative to the conversation-app repository. Rankings are hypotheses about visible benefit, not measured outcomes. The five IDs are proposals for later review, not queue entries. See `implementation-candidates.md` for scope, acceptance and validation details.

## Benefit ranking and delivery phases

| Benefit rank | Candidate | Expected visible benefit | Complexity | Confidence | Suggested phase |
|---|---|---|---|---|---|
| 1 | RC-01 verified intent outcomes | Fewer false claims that Cozmo acted, and clearer recovery when an action is refused, interrupted, stale or physically unconfirmed | Medium | High on the observed reporting gap, medium on implementation | First, bounded reliability work |
| 2 | RC-02 interruptible host-microphone conversation | More natural turn-taking with Cozmo's existing voice and a user-controlled microphone | High | Medium, physical audio and cancellation require measurement | Prototype after RC-01 or in a separate isolated host process |
| 3 | RC-03 bounded turn context | More appropriate replies using existing activity, needs, observations and significant events | Medium | Medium | After RC-01 establishes trustworthy outcomes |
| 4 | RC-04 optional native listening and waiting acknowledgement | Cozmo appears attentive while listening and thinking | Medium | Medium-low until animation admission and resource conflicts are tested | After RC-02 supplies explicit listening and waiting states |
| 5 | RC-05 consented durable preference recall | Cozmo remembers an explicitly approved preference across sessions | Medium | Medium | Later, after session context and identity boundaries are understood |

Rank is benefit to the user, while phase reflects dependencies and validation cost. All five are separately switchable host/C2 work where appropriate, preserve native behavior ownership and require baseline comparison. C2 library defaults and MCP executable defaults differ. The library uses `Baseline` with switches off, while MCP uses `McpDefaults` unless `--baseline` is specified (`cozmo-stack/src/Cozmo.Robot/CozmoC2Settings.cs:7-29`, `cozmo-stack/src/Cozmo.Mcp/CozmoGuide.md:20`).

## Quick wins

**RC-01, first slice.** Correlate the existing tool reply, native action tag and subsequent event cursor, and distinguish admission, terminal engine result and a fresh physical postcondition. Start with a small set such as face turn and charger contact. Existing guidance already instructs callers to examine results and fresh observations (`cozmo-stack/src/Cozmo.Mcp/CozmoGuide.md:5-15,37-43,98`). The Reachy app's asynchronous task status and correlated result delivery show an instructive pattern, though its missing `response.done` and disconnected-session cases can drop results (`src/reachy_mini_conversation_app/huggingface_realtime.py:849-918`, `src/reachy_mini_conversation_app/tools/background_tool_manager.py:127-193`). Adapt the principle to Cozmo's existing receipts rather than copy its task manager.

## High-value architectural improvements

**RC-02.** Test a host microphone, explicit push-to-talk, cancellable inference and playback, and existing Cozmo speech delivery. Reachy's audio chunks and interruption machinery demonstrate the interaction pattern (`src/reachy_mini_conversation_app/huggingface_realtime.py:389-422,1030-1044,1154-1176`). Its local detector first ducks audio and server VAD confirms interruption (`huggingface_realtime.py:1316-1346`). That is an adaptation target, not evidence of hardware equivalence. Establish cancellation behavior and audible latency on Cozmo before promising barge-in or a physical mute. Current speech measurements differ by date and pathway. The 20 November report observed long latency, while later offline work reports improvement pending physical validation (`re-analysis/research/20261120-llm-session-3-notes.md:20-30`, `re-analysis/SCOPE.md:15-23`).

**RC-03.** Assemble bounded, timestamped read-only turn context from `get_state` and `read_events`, including gaps and unavailable values. Cozmo already publishes activity, reaction, needs and sightings (`cozmo-stack/src/Cozmo.Mcp/CozmoGuide.md:102-120`). Filter repeated events and avoid claiming fresh perception from an old face or pose. Reachy's profile and memory are assembled into session instructions (`src/reachy_mini_conversation_app/prompts.py:36-70`), while its on-demand camera image is attached as a separate conversation input (`src/reachy_mini_conversation_app/tools/camera.py:38-56`, `src/reachy_mini_conversation_app/huggingface_realtime.py:929-950`). This supports selective context, not a recommendation to stream images continuously.

## Medium-term improvements

**RC-04.** Add an opt-in, short native listening or waiting acknowledgement only after tracking its admission and release. Reachy maps local or server voice activity to listening and suppresses breathing, and uses a queued gesture for expression (`src/reachy_mini_conversation_app/huggingface_realtime.py:332-342`, `src/reachy_mini_conversation_app/moves.py:301-306,335-345`). Cozmo has native behaviors, authored animations and reactions, so test resource compatibility instead of copying a continuous movement loop (`cozmo-stack/src/Cozmo.Robot/Behavior/BehaviorManager.cs:1011-1040`, `cozmo-stack/src/Cozmo.Mcp/CozmoGuide.md:12-15`).

**RC-05.** Prototype explicit consent, inspect and delete controls for a small host-side store of durable preferences. Reachy already has bounded persistent facts and memory tools (`src/reachy_mini_conversation_app/memory.py:18-23,147-169`, `src/reachy_mini_conversation_app/tools/remember.py:12-52`). Cozmo face enrollment and recognition are separate engine functions. A recognized name alone is insufficient to decide who consented, and host preferences must not mutate native identity or needs.

## Long-term experimental capabilities

Evaluate proactive reasoning, selective event-triggered inference, local/cloud routing and richer episodic retrieval only after the five candidates have measurable acceptance results. No additional implementation candidate is proposed here. Reachy's 180-second idle policy selects weighted local actions (`src/reachy_mini_conversation_app/conversation_handler.py:111-147`, `src/reachy_mini_conversation_app/idle_policy.py:60-90`). It does not establish an autonomous goal planner. Cozmo's freeplay chooser and reactions already provide an autonomous substrate (`cozmo-stack/src/Cozmo.Robot/Behavior/FreeplaySystem.cs:156-193,221,374`).

## Ideas investigated but not recommended

- Replacing native arbitration with an LLM or a Reachy-style motor loop risks conflicting motion on a mobile robot. Preserve reaction and native action admission (`cozmo-stack/src/Cozmo.Robot/Behavior/BehaviorManager.cs:640-750,1011-1040`).
- A parallel LLM emotion model would compete with existing mood and needs (`cozmo-stack/src/Cozmo.Robot/Behavior/Mood.cs:61-97,302-353`, `cozmo-stack/src/Cozmo.Robot/Behavior/Needs.cs:209-228,369-484`). Present verified state as context instead.
- Continuous raw camera upload or continuous inference has unmeasured latency, bandwidth and privacy costs. Begin with significant events and on-demand observations.
- Copying Reachy implementation code requires a destination and dependency license review. The conversation repository is Apache-2.0 (`LICENSE:1-3,89-108`), but the compatibility of each destination and model asset was not established. The proposals above adapt concepts and require no direct source reuse.

## Existing work and overlap

The inspected snapshot contains historical handoffs for `LLM-BRIDGE-CORE-1`, `LLM-BRIDGE-MCP-1`, `LLM-SENSES-1`, `LLM-EVENTS-REPLAY-1`, `MCP-ACTION-WHY-1`, `FACE-TRACK-LLM-1` and `FREEPLAY-LLM-1` under `re-analysis/jobs/trial-m13/`. These are direct overlap with RC-01 and RC-03 and must be read before any future job is opened. `VOICE-ROBOT-DELIVERY-1`, `VOICE-ANIMATION-FIT-1` and `MCP-SESSION-4-1` handoffs overlap RC-02/04. `re-analysis/jobs/QUEUE.md:118,131,135` contains broader blocked vision, speech lifetime and mapping work, relevant to dependencies. This is an overlap check against the checked snapshot, not a live queue status assertion. Reconcile against the current queue and active branches at implementation time. No queue entry or active branch was changed for this research.

**Concurrent work at the pinned commit.** The active trial queue specifically lists C2-GO-HOME-1, C2-LINK-RECONNECT-1, C2-FACE-REQUESTS-1 and C2-MAP-EXPLORE-1 as open, with C2-MAP-IMAGE-1 in progress. RC-01 can consume face or charger evidence and expire old requests without implementing any of those behaviors or taking over link recovery. RC-03 should read the map and events already exposed. These proposals carry no change to queue order. [QUEUE.md L587–591](https://github.com/jimbuschman/cozmo-stack/blob/1bd44f259ee4d4c75ee6182c75a25920669e4d86/re-analysis/jobs/trial-m13/QUEUE.md#L587-L591)
