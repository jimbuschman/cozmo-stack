# M12-049 callbacks: Claude check of the ChatGPT queue item 1 (manager, 2026-10-31)

**Verdict: behaviour citations PASS, with one naming error. Nothing settled; no status changed.** The report is
`research/20261031-m12-049-callbacks-chatgpt-extraction.md`.

## Error in the report

Step 1.5 names the callbacks `TransitionToDrivingToTopBlock::$_1` and `TransitionToPlacingTopBlock::$_2`, with type strings at
0x00C6C840 and 0x00C6C930. The typeinfo says otherwise. 0x0102908C names 0x00C6C8A0, `TransitionToPlacingTopBlock::$_2`;
0x010290CC names 0x00C6C990, `TransitionToPlacingTopBlock::$_3`. Use these names. Its vtable address points 0x01029068 and
0x010290A8 are right, so no behaviour changes. This is the first wrong citation in four reports.

## Checked directly in the `.so`

- **Construction:** 0x005DC16C..0x005DC178 and 0x005DC27C..0x005DC298 match, with `this` captured in both. Each `operator()` slot
  (vtable +0x18) is 0x005DC78B and 0x005DC7F1.
- **Dispatch:** 0x0056E0FA..0x0056E140 matches. Code 2 takes the callable at +0x10 (the first), code 0 takes the callable at +0x28
  (the second), code 1 takes neither. The empty-function test is at 0x0056E116 and 0x0056E126.
- **First callback:** 0x005DC78A branches through the veneer 0x008CC4CC to 0x004B47DC, TransitionToReactingToPyramid (symbol
  value 0x005DBF88). Steps 1.13-1.17 match 0x005DBF98..0x005DC00A.
- **Second callback:** the gate and flag 0x005DC802..0x005DC814 match. The drive, head and lift constructors match
  0x005DC820..0x005DC89A. The sequence and final StartActing match in the call sweep of 0x005DC8BC..0x005DC976.
- **Not re-read:** the DelegateToHelper store order (1.9), the CarryingComponent constructor and IsCarryingObject (1.19), and the
  landing pads (1.31). The "success" and "failure" labels rest on the argument order and on OnDelegateSuccess and
  OnDelegateFailure; code 2 and code 0 are the established facts.

## What the callbacks do

**Code 2, first callback.** TransitionToReactingToPyramid. `NativeBuildPyramidBehavior.React()` already has this body.

**Code 0, second callback.** Only if CarryingComponent +0x08 is -1, tested alone although IsCarryingObject also tests +0x14:
1. set byte +0x15C of BehaviorBuildPyramid to 1;
2. start, with no completion callback, a sequence of:
   - parallel: DriveStraightAction(-20, 20, false), MoveHeadToAngleAction(0x3EDF66F3 (25 deg), 0x3D0EFA35 (2 deg), 0),
     MoveLiftToHeightAction(0, 5, 0);
   - WaitAction(1.0);
   - MoveHeadToAngleAction(0x3E860A92 (15 deg), 0x3D0EFA35 (2 deg), 0).

## Effect on the stack

`NativeBuildPyramidBehavior.cs` installs ReportMissing recipients at these two vtables, per the M12-049 record. The source is now
known for both: code 2 can call `React()`, and code 0 needs the sequence above. Readers of +0x15C are not read here; that flag's
effect must be traced before the failure path is called complete.

The cache half of M12-049 is queue item 2. The record stays RECOVERABLE_GAP.

## Method note

In queue mode it finished one item per reply, deeper than before. The queue works, at about one reply per item.
