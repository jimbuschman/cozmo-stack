# M8 ActionList review — partial (2026-10-21)

HEAD97bdc60f; reviewed Clear rows from `20261021-m8-actionlist-completion-settling.md`.
Actual Thumb instructions independently read, not decompiler output:

- ActionList.Clear53D916..D93A returns if clearing byte+C already set, otherwise
  sets it, destroys the tree, resets root/count/sentinel and clears the flag.
- Tree destroy53FB5C..FB82 recursively destroys left atFB68, right atFB70,
  then queue atFB78 and frees node. This is tree postorder, not sorted keys.
- ActionQueue.Clear53F96A..F9AC sets its reentry guard, calls Cancel only for
  current actionF97C, deletes currentF98A, then directly deletes pending actions
  F9A0 until sentinel and clears guard. No watcher drain is introduced here.

Current ActionList.Clear preserves the guards but enumerates sorted dictionary
values. It explicitly reports this ordering MISSING for more than one queue.
The initial report is right to retain it as a gap. Whether a particular supported
teardown exposes different wire/API/retained-state output still requires the
survivor and owner analysis; this check does not establish that every difference
in container traversal is observable or mandate a C++ tree implementation.

Current M8-015: IMPLEMENTATION_GAP, “ActionList/ActionQueue/IAction lifecycle,
cancellation and Robot teardown”; evidence includes current-cancel/pending-delete
0x0053E6A8..0x0053E6E0,0x005409E2..0x00540A40,0x00541146..0x00541238,0x0053FAB2
and the manager-adopted lifecycle extraction. Current M8-024: RECOVERABLE_GAP,
“Pending actions surviving Cancel(-1) through ~Robot: which shipped completion
callbacks re-queue after CANCELLED, IBehavior +0xA0..+0xA2 at teardown, other
tag-0x5A subscribers, and whether ~BehaviorManager/~BSM queue actions”; evidence
is OV18's callback/mechanism table, including53FA86..FAB2 and53E6E8..E6EE.
Neither status or evidence changed.

**Verdict: checked Clear mechanics and existing ordering gap only.** FeedingEat
survivor answer, callback ownership, full deletion effects, runner/tick ordering,
typed completion and the rest of the initial M8 report remain pending review.
No full-record pass, settlement, builder release, implementation or tests.
