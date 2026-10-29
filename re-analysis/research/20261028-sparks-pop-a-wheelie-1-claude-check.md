# SPARKS-POP-A-WHEELIE-1 — Claude check (manager, 2026-10-28)

**Verdict: PASS. Nothing settled.** Merged 858a7470 (code f3a301e9).

Binding-only change: BehaviorContainer admits the SparksPopAWheelie configuration; ActivityTreeLoader binds the native
Sparked owner with public activity 24; NativeSparkedActivity maps objective PoppedWheelie to 22. The values are cited
to the native enum tables (read S2: ActivityID 24/0x18, objective 22/0x16, behaviour 152, needs 34, unlock 13).
Objective 22 is the same 0x16 the ordinary Pop reports (completion lambda 0x005C7CBC). The Pop class itself is
unchanged from POP-A-WHEELIE-1 (`research/20261028-pop-a-wheelie-1-claude-check.md`); the Sparked owner was checked
in SPARKS-KNOCKOVER-1.

Records: none settled. M13-041 names only the ordinary instance; widening it to SparksPopAWheelie is an operator
checkpoint. M13-015 stays partial (legacy manual callers).

Process note: Codex merged main with FIX-KNOCKOVER-RACE-1 open but finished this job first. The FIX is still owed.
