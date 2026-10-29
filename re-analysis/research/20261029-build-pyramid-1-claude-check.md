# BUILD-PYRAMID-1 — Claude check (manager, 2026-10-29)

**Verdict: PASS. Settled: M12-028 and M12-047 (EXACT_SOURCE).** Merged ccaff079 (code 9dcc3e3a).

The derived BuildPyramid (ID 3, class 2) now runs on the engine's logic:
- the ctor, runnable check, Init routing and top pickup;
- top placement, with the 0x00C6C691 lock and enum 8;
- the success reaction and the failure reaction.

The legacy top loop's field roles were also corrected. Still MISSING and visible: the cache and intention producers
(M12-049), nonzero-offset placement (M12-042/M12-022) and the shared geometry math (M12-043).

## Checked by Claude

- **Top placement 0x005DC08C..0x005DC304:**
  1. SetState 5, then SmartDisableReactionsWithLock.
  2. Look up the static (+0x11C) and base (+0x124) objects. If either is null, warn with +0x120, set +0x120 = -1 and
     stop.
  3. Copy the manager+48 PyramidBase strong-pointer vector.
  4. For each entry: skip it unless ContainsBlock(base) and ContainsBlock(static).
  5. GetBaseInteriorMidpoint(robot, static, base); on false, break.
  6. GetZRotatedPointAboveObjectCenter(static, 0), then the relative pose; on false, break.
  7. CreatePlaceRelObjectHelper(robot, this, static, onGround false, {x, y, byte 0}), then SmartDelegateToHelper.
  8. Continue the loop.

  `NativeBuildPyramidBehavior.PlaceTop` matches.
- **Base placement 0x005DCDCC..0x005DCECA:**
  1. State 1; clear the state lights.
  2. If the static object is null, warn and clear +0x120.
  3. Otherwise UpdateBlockPlacementOffsets(&x, &y), then CreatePlaceRelObjectHelper(static, onGround true, {x, y,
     byte 0}), then delegate.

  `NativeBuildPyramidBaseBehavior.Place` matches.
- **Stack producer 0x005C9580..0x005C961A:** CreatePlaceRelObjectHelper, then SmartDelegateToHelper\<StackBlocks\>,
  then fall back to TransitionToPickingUpBlock. Consistent with the STACK-BLOCKS-1 tuple.

## Settled

- **M12-028:** all three behaviour-owned PlaceRelObject parameter producers. The midpoint math, the nonzero-offset
  consumer and the cache producers each have their own record.
- **M12-047:**
  - the helper stop semantics, built in B-M12-6 against the 2026-10-16 overnight checks;
  - the top loop above.

  The old unresolved text names the fields wrongly ("top+124", "moving-base+120"). The settlement note gives the
  correct roles: static +0x11C/+0x120 (intention 10), base +0x124/+0x128 (intention 9), top +0x12C/+0x130 (intention 11).

Init, the reactions (state 6, objective 3, LiftSafe 0x17; the failure drive -20/20, head 25, lift 0, wait 1, head 15)
and the state-light class rest on Codex's independent checks. M12-043, M12-049 and M15-051/054 stay partial. Codex's
proposed new derived-lifecycle record, like the Base one, needs the operator's OK.
