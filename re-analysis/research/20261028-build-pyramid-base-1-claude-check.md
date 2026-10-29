# BUILD-PYRAMID-BASE-1 — Claude check (manager, 2026-10-28)

**Verdict: PASS. Nothing settled.** Merged 6469bbfe (code 75e48329).

The configured BuildPyramidBase consumer/lifecycle runs natively: target refresh from intentions 9/10/11, runnable
gates, reset, pickup or place on start, low placement, Observe, and the update timers. The cache producers (strong
+48/+64, intentions, M12-049), the nonzero-offset placement tuple (M12-042/M12-022) and noncube virtual dimensions stay
visible MISSING inputs. Nothing is fabricated in their place.

## Checked by Claude

- UpdateInternal 0x005DD01C..0x005DD12C:
  - prev count +0x134 == 0 with a non-empty base list (+0x48/+0x4C) samples Appeared (+0x138);
  - prev > 0 with both the base and the full list (+0x64/+0x68) empty samples Lost (+0x13C);
  - "now" is a separate timer sample;
  - bases present and state +0x158 <= 1: stop if (Appeared + 2) + 1e-5 (literal 0x3727C5AC) < now;
  - bases and full empty, Lost > 1e-5: stop if (Lost + 2) + 1e-5 < now;
  - else flag +0x15C and full non-empty: stop;
  - stop → StopWithoutImmediateRepetitionPenalty, return 2; otherwise prev = base count (8-byte entries), tail to the
    shared update.

  `OnUpdate` matches, including the two distinct timer samples.
- Observe 0x005DD4AC..0x005DD56C: state 2; sequential {DriveStraight(-40.0f, 40.0f, true),
  TriggerLiftSafe(trigger 0x14, 1, true, 0, 60.0f, false)}, both added with (0, 0); StartActing with the callback.
  Matches `Observe`.

Target refresh, the gates, reset/start, pickup with pre-animation 0x16, placement 0x005DCDCC..0x005DCECA, the getters
and the factory binding rest on Codex's independent checks. The failed first gate, a stale M7 factory expectation, was
corrected minimally and passed.

## Records

None settled. M12-028, M12-049, M15-051/054, M8-011, M12-042 and M12-022 stay partial. Codex proposes one new record for
the configured Base lifecycle/consumer/callback/timer scope. That needs the operator's OK.
