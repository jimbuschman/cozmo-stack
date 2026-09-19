# Choosing the next milestone, from the behaviour dependency inventory

Decided from `BEHAVIOR_INVENTORY.md` rather than from an earlier roadmap, as instructed.

## What blocks what

Of the 178 shipped behaviours:

| blocker | behaviours | share |
| --- | ---: | ---: |
| requires cubes | 62 | 35% |
| requires Wwise switch-state audio | 39 | 22% |
| requires vision/person detection | 24 | 13% |
| freeplay/explorer-specific (not yet classified further) | 18 | 10% |
| requires robot state not yet derived | 11 | 6% |
| requires charger/docking | 7 | 4% |
| game-specific | 5 | 3% |
| **implementable with M1–M7 now** | **5** | **3%** |
| developer-only | 4 | 2% |
| unclear | 2 | 1% |
| requires localization/world model | 1 | <1% |

## The largest unlock is cubes, and it needs no new subsystem

62 behaviours — more than a third of the whole system — are blocked on cubes. **That work is already
done.** M4's cube support is code-complete and offline-tested through the real message path: discovery,
connection state, tap, movement, up-axis and battery telemetry. What it has never had is a cube to point
the robot at.

So the largest single unlock in the entire inventory costs one hardware session, not a milestone:

```
dotnet run --project src/Cozmo.Conformance -- cubes 172.31.1.1 --acceptance
```

That should be run before any new subsystem is started, because it would move 35% of the behaviour system
from "blocked" to "reachable" for the price of an afternoon. It is listed in the morning hardware tests.

Note the honest caveat: the classifier counts a behaviour as needing cubes if it so much as *mentions*
them, so 62 is an upper bound. Cube acceptance makes those behaviours reachable; it does not by itself
implement them.

## The largest *buildable* unlock is Wwise switch-state audio

Cubes cannot be "begun" overnight — they need hardware. The next largest block that can be worked on
without a robot is the 39 `Singing` behaviours, every one of which selects its audio by **switch state**
rather than by event id:

```json
{
  "behaviorClass": "Singing",
  "behaviorID": "Singing_AbaDaba",
  "audioSwitchGroup": "Cozmo_Sings_80Bpm",
  "audioSwitch": "Cozmo_Sings_Aba_Daba"
}
```

M6 resolves **events**. Switch containers are precisely what it does not do — the same gap that leaves 46
of the 90 unresolved events pointing into switch and music hierarchies. This is a bounded extension of a
frozen layer, entirely offline-testable against banks and `.wem` files already on disk, and it needs no
robot at any point.

### Foundation established tonight

One piece of it is done and verified, because it decides whether the rest is tractable at all.

**Wwise identifies everything by a 32-bit hash of the name, and the hash is FNV-1 over the lower-cased
name.** Verified against this build's own banks, where each bank's `BKHD` id is the hash of its filename:

| name | hash | bank id |
| --- | --- | --- |
| Music | 3991942870 | 3991942870 |
| SFX | 393239870 | 393239870 |
| Cozmo | 2386142475 | 2386142475 |
| UI | 1551306167 | 1551306167 |
| Init | 1355168291 | 1355168291 |
| Dev_Debug | 1453038702 | 1453038702 |

Six of six, no near misses, and it is FNV-1 rather than FNV-1a (multiply then xor). `WwiseHash` implements
it and the tests check it against the bank files rather than a copied list.

This matters because **no name table ships**. Without it, `"audioSwitchGroup": "Cozmo_Sings_80Bpm"` is an
unresolvable string; with it, that name is id 3366294904 and can be looked up in the banks.

### What remains for that milestone

* **`STMG` in `Init.bnk`** holds the state and switch group definitions. Its state-group section parses —
  6 groups, with sane ids and transition times — but the switch-group section that follows does not yet,
  because the state-group record length is not fully established. This is an ordinary parsing job, not a
  conceptual gap.
* **HIRC type 6, `SwitchContainer`** (21 in this build) holds the switch-state to child mapping. Its
  payload layout is not yet read. The earlier parent-offset scan matched only 11 of 21, which is itself a
  hint that its header differs from the other container types.
* Once both are read, a `Singing` behaviour's two names resolve to a child, and from there to media through
  the resolution M6 already has.

Nothing about this was implemented tonight beyond the hash, and nothing claims to work.

## Not chosen, and why

* **Vision** (24 behaviours) is a larger subsystem than switch-state audio for fewer behaviours, and its
  foundation — camera frames — is already frozen and verified, so it is not blocked on groundwork that
  could be laid overnight.
* **Localization and navigation** together account for 1 behaviour in the inventory. Whatever their value
  for a complete stack, the dependency data does not support doing them next.
* **Charger/docking** is 7 behaviours and needs hardware to verify.

## Recommended order

1. **Run cube hardware acceptance.** Largest unlock in the inventory, code already written.
2. **M9: Wwise switch-state audio.** 39 behaviours, offline-testable, foundation established.
3. Re-run the inventory afterwards; both of the above change the numbers it reports.
