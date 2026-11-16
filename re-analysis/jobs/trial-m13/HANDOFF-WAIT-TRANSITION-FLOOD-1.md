# WAIT-TRANSITION-FLOOD-1

The 2026-11-16 Freeplay capture contains 1,030 Wait → Wait reports. The median interval is 60.02 ms. Of those reports, 1,021 followed the InteractWithFaces initial-animation lookup within 15 ms. The run logged the missing shared authored audio device for InteractWithFaces. ENGINE-AUDIO-STARTUP-1 supplies that device during engine startup.

- The steady-state chooser gate at `0x005A3062..0x005A3078` calls `ChooseNextScoredBehaviorAndSwitch` on each eligible update. The activity provides a candidate at `0x005A2A4A`. The compare at `0x005A2A74..0x005A2A80` tests the two behavior pointers. A non-null candidate differs from an empty current slot, so it gets another attempt on the next eligible tick.
- The switch retains the old info, clears the current behavior and calls Init at `0x005A1E2A..0x005A1E94`. A nonzero result clears the proposed running pointer at `0x005A1E98..0x005A1EFC`. Both branches write running info and call the transition sender at `0x005A1F12..0x005A1F1C`.
- `SendDasTransitionMessage` checks the external interface at `0x005A22BE..0x005A22C8` and emits equal endpoint IDs. The null endpoints are Wait ID `0xB2`, class `0x39`, executable `0x0B` and an empty display key at `0x005A22EC..0x005A2302`, `0x005A23DA..0x005A2450`. The app delivery is at `0x005A2480` → `0x005A2580..0x005A2588`. The resume path clears current at `0x005A2C5C` and reports a successful resume from that post-stop info at `0x005A2D86..0x005A2D94`. The failure/empty branch goes through the ordinary switch at `0x005A2CCC..0x005A2CF4`.

The existing transition and selection decisions match these paths. The regression drives five engine ticks with a chooser that repeatedly picks a failing Init and checks the exact typed and packed `0x87` report on each tick. A sixth tick with both pointers null checks that the switch does not run. The original audio-triggered churn still needs operator confirmation after the startup fix.

Verification passed 19 focused tests, the 762-record fidelity check and the no-robot Freeplay control dry run.
