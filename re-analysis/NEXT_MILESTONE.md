# Choosing the next milestone, from the behaviour dependency inventory

> **2026-09-20, after M14:** M9–M14 are complete offline (`WWISE_MUSIC.md`, `DERIVED_STATE.md`, `VISION.md`,
> `MANIPULATION.md`, `NAVIGATION.md`, `FACES.md`). The inventory is 107 of 178 implementable plus 14 face
> behaviours implemented behind the OKAO boundary. The next milestone is **M15**: the freeplay / explorer
> layer; see `HANDOFF.md` "Next task". The analysis below is the original record and is superseded where it
> says otherwise.


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

### Where the Singing audio actually lives, found with that hash

With the hash established, the two names a `Singing` behaviour carries can be looked for directly in the
banks. Both appear, once each, in `Cozmo.bnk`:

| name | id | found in |
| --- | --- | --- |
| `Cozmo_Sings_80Bpm` | 3366294904 | HIRC object 914766641, payload offset 196 |
| `Cozmo_Sings_Aba_Daba` | 2234458138 | the same object, payload offset 290 |

That object is **HIRC type 12, `MusicSwitchContainer`** — not the plain `SwitchContainer` (type 6) that
was expected.

**This joins two gaps that looked separate.** M6 left 90 events unresolved, 46 of them pointing into the
music hierarchy (`MusicSegment`, `MusicPlaylistContainer`, `MusicSwitchContainer`). The 39 Singing
behaviours turn out to need that same hierarchy. So one subsystem — reading the music hierarchy and its
switch resolution — unlocks the Singing behaviours *and* closes most of what M6 could not resolve. That
makes it a better-value milestone than the behaviour count alone suggested.

### What remains for that milestone

* **HIRC type 12, `MusicSwitchContainer`**: the switch-group and switch-state ids sit at payload offsets
  196 and 290 in the one object examined, but the records around them, and the child each state selects,
  are not yet read. Offsets from a single object are not a layout.
* **`MusicSegment` (type 10) and `MusicTrack` (type 11)**: music tracks hold their sources differently
  from `Sound` objects, which is why M6's walk does not reach them.
* **`STMG` in `Init.bnk`** holds the state and switch group definitions. Its state-group section parses —
  6 groups, with sane ids and transition times — but the switch-group section after it does not yet,
  because the state-group record length is not fully established. An ordinary parsing job, not a
  conceptual gap.

Nothing here was implemented beyond the hash, and nothing claims to work.

## Not chosen, and why

* **Vision** (24 behaviours) is a larger subsystem than switch-state audio for fewer behaviours, and its
  foundation — camera frames — is already frozen and verified, so it is not blocked on groundwork that
  could be laid overnight.
* **Localization and navigation** together account for 1 behaviour in the inventory. Whatever their value
  for a complete stack, the dependency data does not support doing them next.
* **Charger/docking** is 7 behaviours and needs hardware to verify.

## Recommended order

**Superseded as of 2026-09-19 — see below.** M9 is analysed and ready, but it is not next.

1. **Run cube hardware acceptance.** Largest unlock in the inventory, code already written.
2. **M9: Wwise switch-state audio.** 39 behaviours, offline-testable, foundation established.
3. Re-run the inventory afterwards; both of the above change the numbers it reports.

## What is actually next: the Source Fidelity Sweep

**Superseded again on 2026-09-19: the sweep ran (SOURCE_FIDELITY_AUDIT.md), both hardware retests passed, and M9
began; increment 1 is in [WWISE_MUSIC.md](WWISE_MUSIC.md).** The paragraphs below are kept as the record of why the
sweep came first.

The reason comes out of M5's procedural face renderer. It shipped as "ours, an interpretation of the
parameter names", passed its tests, and survived two hardware failures in that state — because nothing ever
forced the question of whether `libcozmoEngine.so` could simply answer it. It could: the canvas size, both
eye positions, the nominal eye box, the corner construction, the lid maths and the rounding mode were all
sitting in `ProceduralFaceDrawer`, and every invented constant turned out to be wrong. See
[PROCEDURAL_FACE.md](PROCEDURAL_FACE.md) and [HANDOFF.md](HANDOFF.md).

That is a process failure, not a one-off bug, and the sweep is what checks whether it happened elsewhere:
values chosen because they looked right, behind comments that admit it, in code whose tests were written
from the same assumption and therefore cannot falsify it.

Adding M9's 39 behaviours on top of an unswept foundation would mean more code resting on guesses that
nobody has gone back to check. The inventory analysis above stays valid and M9 stays the right *feature*
milestone; it simply is not the right *next* one.

## After M9 (2026-09-19): the inventory re-run

`behavior_inventory.py` now files the 39 Singing behaviours as **implementable with M9 (switch-state audio)**:

| blocker | behaviours |
| --- | ---: |
| requires cubes | 62 |
| **implementable with M9 (switch-state audio)** | **39** |
| requires vision/person detection | 24 |
| freeplay/explorer-specific | 18 |
| requires robot state not yet derived | 11 |
| requires charger/docking | 7 |
| game-specific | 5 |
| **implementable with M1-M7 now** | **5** |
| developer-only | 4 |
| unclear | 2 |
| requires localization/world model | 1 |

44 of 178 are now runnable offline. M9's hardware acceptance is pending (`HARDWARE_TEST_PLAN.md` item A)
and nothing that follows depends on it.

**Recommended next: M10 — derived robot state (11 behaviours) and the cube reactions.** The engine's
off-treads classifier runs on IMU data the robot already streams, so it can be read from the binary and
reproduced offline against the committed captures; the cube reactions run on the M4 message path whose
discovery is hardware-observed. Neither needs a robot to build, and cube acceptance (item B) is what
freezes the second half. Vision (24) remains the larger subsystem; charger/docking (7) needs hardware to
verify at every step.

## After M10 (2026-09-19): the inventory re-run

`behavior_inventory.py` now classifies from what is implemented: the M10 reaction classes, the corrected
`PlayAnim` / `PlayAnimWithFace` reading, and per-behaviour verdicts read from the binary:

| blocker | behaviours |
| --- | ---: |
| requires cubes | 55 |
| **implementable with M9 (switch-state audio)** | **39** |
| requires vision/person detection | 23 |
| **implementable with M1-M7 now** | **19** |
| freeplay/explorer-specific | 16 |
| **implementable with M10 (derived robot state)** | **9** |
| requires charger/docking | 5 |
| developer-only | 4 |
| game-specific | 2 |
| unclear | 2 |
| requires localization/world model | 1 |
| implemented; waits on cube localization (vision) | 1 |
| requires navigation/path planning | 1 |
| requires the app's spark system | 1 |

**67 of 178 are runnable offline** (44 after M9). `ReactToCubeMoved` is built and waits only for a cube pose.
M10's hardware acceptance is pending (`HARDWARE_TEST_PLAN.md` items G–J) and nothing that follows depends on it.

**Recommended next: M11 — vision, first slice: marker detection and cube localisation.** It is the single
largest unblocker left: 55 cube behaviours and the finished cube-moved reaction need a located cube, and the
face/pet set (23) is the same pipeline's next stage. The engine's `VisionSystem`, `BlockWorld` and marker code
are exported; M3 already delivers camera frames. Charger/docking (5) and navigation (1) come after a world model.
