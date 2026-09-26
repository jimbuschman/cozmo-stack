# Research lane (Codex / ChatGPT)

Codex works in this repo alongside the opencode manager. This file sets the lane it works in. The rules in `AGENTS.md` still apply.

## The lane

- **Read anything; write only here.** Codex writes only under `re-analysis/research/`. It doesn't edit code, inventories, the manifest or PROJECT_STATE.
- **Stay on main, as it is.** No branches, no worktrees, no commits, no pushes. The manager reads the result and commits it.
- **Requests come from the manager** in `re-analysis/research/requests/<yyyymmdd>-<topic>.md`, using `REQUEST_TEMPLATE.md`. The operator tells Codex which one to do.
- **One answer per request:** `re-analysis/research/<yyyymmdd>-<topic>.md`, which names the request it answers.

## Two kinds of job

1. **Background research:** public documentation and community work, such as Wwise 2016.2, OpenCV 3.1, Tremor/Vorbis, pycozmo and Cozmo community notes.
   - This is **authority 6** (repo notes). It is a lead to check in the engine, never evidence for a fidelity status.
   - Cite every source: a URL, a document and section, or a file and line. Say plainly what is confirmed and what is inference.
2. **Independent extraction:** reading `libcozmoEngine.so` or the Unity code for a question the manager gives.
   - Follow `.opencode/agent/cozmo-extractor.md` exactly: a row per step, an address citation or UNKNOWN, and no guesses.
   - The manager treats it as an extractor report. Its citations are checked before any row goes into an inventory.

## What Codex never does

- Mark anything EXACT_SOURCE, approve an inventory, or change `re-analysis/fidelity_manifest.json`.
- Fill an unknown with a plausible answer.
- Modify or flash robot firmware.
