# What Reachy Mini can teach Cozmo

**Keep Cozmo's native engine in charge of his body and personality. Build the conversation client around it. Start by making the client distinguish a submitted request, an engine result and an observed physical outcome.** In a recorded session, a face turn reported success while the operator saw no turn. The current API already explains how that can happen. Using the evidence it supplies would improve today's experience and give spoken conversation a trustworthy foundation. [20261126-llm-session-7-notes.md L14–47](https://github.com/jimbuschman/cozmo-stack/blob/1bd44f259ee4d4c75ee6182c75a25920669e4d86/re-analysis/research/20261126-llm-session-7-notes.md#L14-L47) · [CozmoBridge.Commands.cs L60–65](https://github.com/jimbuschman/cozmo-stack/blob/1bd44f259ee4d4c75ee6182c75a25920669e4d86/cozmo-stack/src/Cozmo.Bridge/CozmoBridge.Commands.cs#L60-L65)

### 1. What does Reachy Mini do better?

It treats listening, responding, playing audio and being interrupted as parts of the same conversation. A user can begin speaking while audio is playing. The app can duck or flush output, cancel associated gestures and continue the new turn. It also persists short user facts across sessions and can express a profile through authored gestures. The source establishes those mechanisms. Their acoustic performance on Cozmo is unknown. [huggingface_realtime.py L389–422](https://github.com/pollen-robotics/reachy_mini_conversation_app/blob/622a3a67582a1eb45c82b51251fa9f61a999b51d/src/reachy_mini_conversation_app/huggingface_realtime.py#L389-L422) · [huggingface_realtime.py L1275–1346](https://github.com/pollen-robotics/reachy_mini_conversation_app/blob/622a3a67582a1eb45c82b51251fa9f61a999b51d/src/reachy_mini_conversation_app/huggingface_realtime.py#L1275-L1346) · [memory.py L130–169](https://github.com/pollen-robotics/reachy_mini_conversation_app/blob/622a3a67582a1eb45c82b51251fa9f61a999b51d/src/reachy_mini_conversation_app/memory.py#L130-L169)

### 2. What does Cozmo already do better?

He chooses activities, reacts to physical events, expresses needs and maintains a world in which faces, cubes, charger poses and hazards have different meanings. Freeplay works without an LLM. His bridge already exposes action tags, refusal reasons, filtered events, map and camera reads. In session 7, low play drove a native needs expression, and speech successfully interrupted Freeplay. Those observations argue for reusing these paths. [BehaviorManager.cs L1011–1040](https://github.com/jimbuschman/cozmo-stack/blob/1bd44f259ee4d4c75ee6182c75a25920669e4d86/cozmo-stack/src/Cozmo.Robot/Behavior/BehaviorManager.cs#L1011-L1040) · [CozmoBridge.cs L158–189](https://github.com/jimbuschman/cozmo-stack/blob/1bd44f259ee4d4c75ee6182c75a25920669e4d86/cozmo-stack/src/Cozmo.Bridge/CozmoBridge.cs#L158-L189) · [CozmoMcpServer.cs L194–216](https://github.com/jimbuschman/cozmo-stack/blob/1bd44f259ee4d4c75ee6182c75a25920669e4d86/cozmo-stack/src/Cozmo.Mcp/CozmoMcpServer.cs#L194-L216) · [20261126-llm-session-7-notes.md L8–21](https://github.com/jimbuschman/cozmo-stack/blob/1bd44f259ee4d4c75ee6182c75a25920669e4d86/re-analysis/research/20261126-llm-session-7-notes.md#L8-L21) · [20261126-llm-session-7-notes.md L47–60](https://github.com/jimbuschman/cozmo-stack/blob/1bd44f259ee4d4c75ee6182c75a25920669e4d86/re-analysis/research/20261126-llm-session-7-notes.md#L47-L60)

### 3. Which five improvements matter most?

| Benefit rank | Future candidate | What the person would notice |
|---|---|---|
| 1 | **RC-01 · Verify intent outcomes** | Cozmo can tell you a request was blocked, the target was lost, or the outcome is uncertain instead of claiming he did it. |
| 2 | **RC-02 · Interruptible host-microphone conversation** | You can take the next turn while keeping Cozmo's own voice. Start with push-to-talk and measure the audible tail before attempting hands-free interruption. |
| 3 | **RC-03 · Bounded turn context** | Replies account for what Cozmo was doing, what he recently saw, what he needs and which action is outstanding. |
| 4 | **RC-04 · Optional listening acknowledgement** | A brief authored cue can show he heard you while inference runs. He skips it when another behavior has the relevant tracks. |
| 5 | **RC-05 · Consented preference recall** | An explicitly selected profile can remember and forget a preference across sessions, independently of face enrollment. |

The [roadmap](improvement-roadmap.md) distinguishes expected benefit from delivery dependencies. The [candidate specifications](implementation-candidates.md) include testing, hardware validation and baseline requirements.

### 4. Which changes would make him feel more alive?

RC-02 and RC-04 close two perceptible gaps: knowing when Cozmo is listening and being able to interrupt him. Let Freeplay, expressions and native gaze continue during model latency wherever current ownership allows. Reachy itself pauses the tracking contribution during speech when it already has a face lock. Continuous motion on every track is unnecessary for a natural exchange. [moves.py L441–472](https://github.com/pollen-robotics/reachy_mini_conversation_app/blob/622a3a67582a1eb45c82b51251fa9f61a999b51d/src/reachy_mini_conversation_app/moves.py#L441-L472) · [NativeTrackFaceRunner.cs L250–260](https://github.com/jimbuschman/cozmo-stack/blob/1bd44f259ee4d4c75ee6182c75a25920669e4d86/cozmo-stack/src/Cozmo.Robot/Vision/NativeTrackFaceRunner.cs#L250-L260)

### 5. Which changes would make him more intelligent?

RC-03 gives reasoning a fresh account of the world. RC-01 lets the next decision depend on what actually happened. RC-05 can add continuity across visits when the user wants it. A larger model cannot infer whether an unobserved robot turned, identify a person from an old face pose, or make an unanchored map commandable. [Evidence on action outcomes, perception and memory](findings.md)

### 6. Which changes would make him more reliable?

RC-01 first. Expire a request when its session goes away, reconcile results by native tag where available, and require action-specific evidence before claiming success. Follow with RC-03's observation age and feed-gap handling. Keep the existing supervisor for diagnosing the recorded silent drops; their root cause remained open in the inspected project rollup. [CozmoBridge.ActionResults.cs L101–116](https://github.com/jimbuschman/cozmo-stack/blob/1bd44f259ee4d4c75ee6182c75a25920669e4d86/cozmo-stack/src/Cozmo.Bridge/CozmoBridge.ActionResults.cs#L101-L116) · [SessionProcessSupervisor.cs L143–189](https://github.com/jimbuschman/cozmo-stack/blob/1bd44f259ee4d4c75ee6182c75a25920669e4d86/cozmo-stack/src/Cozmo.Mcp/SessionProcessSupervisor.cs#L143-L189) · [SCOPE.md L115–125](https://github.com/jimbuschman/cozmo-stack/blob/1bd44f259ee4d4c75ee6182c75a25920669e4d86/re-analysis/SCOPE.md#L115-L125)

### 7. What should we avoid copying?

Skip Reachy's fixed-base pose blending, antenna or head trajectories, random idle distribution and provider-specific speech output. Cozmo already owns mobile track arbitration, cliff reactions, a personality loop and a distinctive voice. Avoid a second mood engine, blanket reaction locks, constant camera-to-model input, or replaying physical commands automatically after reconnect. A direct model endpoint, a memory backend such as BlipShell, and an always-running planner each need a demonstrated benefit before becoming dependencies. [Comparative findings](findings.md) · [Ownership design](proposed-cozmo-architecture.md)

### 8. Is the current direction sound?

Yes. The separation between an external reasoning client and a reconstructed native engine preserves both personality and offline behavior. The library baseline disables all nine inspected C2 switches. The MCP executable enables its existing defaults unless launched with `--baseline`. Preserve that distinction and expose any newly approved behavior extension by name in settings and logs. The project's A 16/16 and B 40/40 totals count fidelity to the original, including known physical limitations. [CozmoC2Settings.cs L6–46](https://github.com/jimbuschman/cozmo-stack/blob/1bd44f259ee4d4c75ee6182c75a25920669e4d86/cozmo-stack/src/Cozmo.Robot/CozmoC2Settings.cs#L6-L46) · [SCOPE.md L112–125](https://github.com/jimbuschman/cozmo-stack/blob/1bd44f259ee4d4c75ee6182c75a25920669e4d86/re-analysis/SCOPE.md#L112-L125)

### 9. What should go first?

Take one face-turn trace through RC-01 from user request to result. Demonstrate that native SUCCESS with no fresh face pose produces “I couldn't verify that I turned toward you.” Then replay an interrupted tracking request and a dropped session. The first slice uses evidence already available, needs no new model or motor behavior, and can be evaluated before changing how Cozmo sounds or moves.

**Verification limits.** This was source research across three pinned repositories with spot checks of earlier local robot logs. Neither robot, a live AI provider, nor a full local speech pipeline was exercised. The historical 9–17-second robot speech onset and later roughly three-second offline result are different measurements. Direct code or asset reuse was not recommended. [Revisions, execution paths and hardware evidence](architecture-comparison.md) · [License and dependency findings](findings.md)
