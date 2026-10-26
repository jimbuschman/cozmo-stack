# New plan: one behaviour at a time, end to end (operator, 2026-10-26)

**Why:** two weeks of layer-4 work (about 800 commits) left the settled count at 46 of 347 while open gaps grew from 227
to 301. The work is real, but it was read and built in fragments, one hidden dependency per build-and-check cycle, and
finished records were never settled. The operator wants every behaviour, and real progress.

**Progress is measured in finished behaviours**, not in jobs or commits. A behaviour is finished when:
1. its whole production path has been read once, end to end, before any building (every function, clock, input,
   message and record it touches, across M7/M8/M10-M15);
2. all of it is built in one job;
3. it passes one Claude check;
4. it has passed a robot run (batched; runs still never block the next behaviour);
5. every record it covered is settled in that same check, or its `unresolved` names exactly what is left.

**The rules, unchanged:** AGENTS.md evidence rules. No guessing; the engine binary is the evidence.

**The rules, now enforced:**
- **"Does it matter?" comes first.** Exact reproduction applies to "What we reproduce" 1-4: what the robot is sent,
  what it does, what the C# API delivers, and state feeding those. Internal mechanics nobody can observe are built the
  simplest way. A step whose only justification is internal order or ownership needs one line saying which of 1-4 it
  changes, or it isn't built.
- **Settling is part of every job.** The handoff lists each record the job completes, with the evidence. Claude's check
  settles the ones that hold (`fidelity.py`, IMPLEMENTATION_GAP -> EXACT_SOURCE) and writes a claude-check file naming
  them. Codex still never changes statuses itself.

**Order:**
1. **Pilot: drive-off.** It already passes on the robot. Codex lists every record on its path, says which are now
   complete and what is left on the others, then builds what's left. Claude settles. Target: drive-off finished.
2. **The behaviour list.** Codex lists every behaviour the engine runs (from the shipped behaviour configs:
   freeplay activities, reactions, sparks), each with the subsystems it touches, ordered so that shared pieces
   (driving to a cube, docking, picking up) come first. That list is the project's scoreboard, kept in
   `BEHAVIOURS.md` here with a finished/in progress/not started column.
3. **Then one behaviour per job, down the list.**

**Reading guide (authority 6 only):** check whether the public source for Vector's engine (Anki's later robot,
descended from Cozmo's engine) exists and how close it is. If it does, use it only to find where to look in the
binary, never as evidence.

**Daily report:** the manager reports to the operator, once a day: behaviours finished, records settled, open gaps.
