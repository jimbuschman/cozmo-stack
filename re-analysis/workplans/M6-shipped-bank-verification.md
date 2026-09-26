# M6 shipped-bank verification concern

Date: 2026-09-26
Scope: read-only verification; no production or test source changed by this investigation.

## Result

The reported result `WwiseTests.EveryShippedBankParsesExactly: Passed` does **not by itself** prove that any shipped bank was opened. In the pre-M6 tree (`2ad71a3`), `WwiseTests.SoundDirs()` searches only for `re-analysis/obb/sound_meta`; if it finds none, `EveryShippedBankParsesExactly` returns before loading a library and xUnit reports a normal pass.

Even when it executes, that test proves the bank/chunk and HIRC envelopes parsed exactly, six banks survived the loader, all are version 120, and 835 events were present. It does **not** run the field-level hierarchy readers whose exact-consumption check is `WwiseSoundLibrary.CheckHierarchy()`.

The authoritative M6 verification should require the real `AudioAssets.zip`, then combine the assertions currently split between:

- `WwiseMusicTests.TheBanksAndNameTablesLoadFromTheShippedArchive`; and
- `WwiseMusicTests.EveryHierarchyObjectInTheShippedBanksConsumesExactly`.

M6 commit `7842392` added `WwiseRuntimeTests.TheShippedBanksLoadAndEveryCoveredObjectConsumesExactly`, which combines the important count/version/event/exact-consumption assertions, but its `if (WwiseAssets.SoundDir is not { } dir) return` still permits the same false pass when assets are unavailable.

## Affected tests and source paths

| Test | Asset path | Missing-asset behavior | What an executing pass proves |
| --- | --- | --- | --- |
| `WwiseTests.EveryShippedBankParsesExactly` (`cozmo-stack/tests/Cozmo.Protocol.Tests/WwiseTests.cs`) | Pre-M6 version: ancestor `re-analysis/obb/sound_meta`; M6 commit `7842392` also falls back to `assets/cozmo_resources/sound` | `SoundDirs() == null` -> normal `return` | Six loaded banks, v120, 835 events; `WwiseBank.Parse` enforces whole-file chunk and HIRC-envelope consumption |
| `WwiseHashTests.EveryShippedBankIdIsItsNameHashed` (`WwiseHashTests.cs`) | ancestor `re-analysis/obb/sound_meta` | no directory -> normal `return` | Six loose banks were individually opened and their IDs match filename hashes; no hierarchy-field exactness |
| `WwiseMusicTests.TheBanksAndNameTablesLoadFromTheShippedArchive` (`WwiseMusicTests.cs`) | `WwiseAssets` -> real sound directory containing `AudioAssets.zip` | `Library == null` -> normal `return` | Six loaded banks, v120, 835 events, media/name tables |
| `WwiseMusicTests.EveryHierarchyObjectInTheShippedBanksConsumesExactly` (`WwiseMusicTests.cs`) | same `WwiseAssets` archive loader | `Library == null` -> normal `return` | Exact field consumption and fixed counts for all 14 covered hierarchy types, 3,604 objects total |
| `WwiseRuntimeTests.TheShippedBanksLoadAndEveryCoveredObjectConsumesExactly` (added by `7842392`) | same `WwiseAssets` archive loader | `SoundDir == null` -> normal `return` | Combines six/v120/835 with nonempty per-type exact-consumption checks, but does not pin the shipped filenames or fixed per-type counts |

`WwiseAssets` is in `cozmo-stack/tests/Cozmo.Protocol.Tests/WwiseAssets.cs`. It walks ancestors from `AppContext.BaseDirectory` and selects the first `re-analysis/obb/assets/cozmo_resources/sound/AudioAssets.zip`. `WwiseSoundLibrary.Load` is in `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseSoundLibrary.cs`: it reads loose banks recursively and reads banks from ZIP entries. It intentionally catches `InvalidDataException`; therefore the required six-bank assertion is what converts a swallowed bank-parse failure into a test failure.

`WwiseBank.Parse` (`WwiseBank.cs`) rejects a chunk sequence that does not end at file end and a HIRC object sequence that does not end at HIRC chunk end. `CheckHierarchy()` separately runs `WwiseHierarchy.TryRead` over every object of every supported type and reports exact payload consumption.

## Actual bank locations

The available unpacked OBB is:

`cozmo-stack/re-analysis/obb/assets/cozmo_resources/sound/`

Its `AudioAssets.zip` contains exactly:

| Bank | Bytes |
| --- | ---: |
| `Dev_Debug.bnk` | 2,573 |
| `Init.bnk` | 3,473 |
| `Music.bnk` | 1,071,008 |
| `SFX.bnk` | 100,795 |
| `UI.bnk` | 42,962 |
| `English(US)/Cozmo.bnk` | 6,236,403 |

The same six files also exist loose under that sound directory. Their SHA-256 values match the ZIP entries and the older root-level research copy at `re-analysis/obb/sound_meta`. That root-level `sound_meta` is not the `sound_meta` child of the currently available unpacked OBB; ancestor searching can nevertheless find it in this checkout, which makes the legacy test's behavior environment/worktree-dependent.

## Commands and observations

Commands used (paths abbreviated only here):

```text
rg -n "EveryShippedBankParsesExactly|sound_meta|WwiseAssets|\.bnk" .
Get-ChildItem . -Filter *.bnk -Recurse -File
[IO.Compression.ZipFile]::OpenRead(...AudioAssets.zip)  # enumerate .bnk entries
Get-FileHash <each loose/meta bank> -Algorithm SHA256
dotnet test ...Cozmo.Protocol.Tests.csproj --filter <four focused Wwise tests>
dotnet test <copied test assembly outside the repository> --filter <three asset tests>
```

Observed in the repository with the real archive visible:

```text
Passed WwiseTests.EveryShippedBankParsesExactly
Passed WwiseMusicTests.TheBanksAndNameTablesLoadFromTheShippedArchive
Passed WwiseMusicTests.EveryHierarchyObjectInTheShippedBanksConsumesExactly
Passed WwiseRuntimeTests.TheShippedBanksLoadAndEveryCoveredObjectConsumesExactly
Total tests: 4; Passed: 4
```

The exact-consumption test pins these totals: Sound 2360, RandomSequence 468, Switch 21, ActorMixer 31, Blend 6, MusicSegment 209, MusicTrack 258, MusicSwitch 14, MusicPlaylist 123, LFO 4, Envelope 7, AudioBus 15, FxShareSet 23, FxCustom 65: **3,604 covered objects**.

The test assembly was then copied to a temporary directory outside every `re-analysis/obb` ancestor and the three asset tests were run there. All three still reported `Passed` in 2.535 s even though none could locate assets. This directly demonstrates the false-pass condition for the legacy test, the existing newer exact-consumption test, and the new combined M6 test as currently written.

## Minimum correction and authoritative procedure

Smallest robust DeepSeek change:

1. Keep one M6-specific combined test (the new runtime test is the natural place).
2. Replace its missing-asset `return` with a failing assertion that prints the expected absolute archive path. This test is the M6 verification gate; ordinary optional asset tests may retain their skip behavior.
3. Load only through `WwiseAssets.SoundDir`, which deterministically selects the real `AudioAssets.zip` layout.
4. Assert the six names exactly (`Init`, `Cozmo`, `SFX`, `Music`, `UI`, `Dev_Debug`), not merely `Banks.Count == 6`; also retain v120 and 835-event assertions.
5. Retain the fixed per-type count table from `WwiseMusicTests.EveryHierarchyObjectInTheShippedBanksConsumesExactly`, and require `Exact == Count` for every one of its 14 types. Do not replace those fixed counts with only `count > 0`.

Then run that one required-assets test explicitly. Its success proves: the archive was found; all six named bank entries were opened; no bank parse failure was silently dropped (count/name assertions); every bank/chunk and HIRC envelope consumed exactly (`WwiseBank.Parse`); and every supported hierarchy object's fields consumed exactly with the shipped fixed counts (`CheckHierarchy`). The two existing `WwiseMusicTests` provide equivalent substantive coverage when run together, but neither is a sufficient M6 gate unchanged because both return normally if assets are absent. DeepSeek's new combined test is likewise not sufficient until its missing-asset return is made a failure and its fixed name/count assertions are strengthened.

## DeepSeek's earlier report

No persisted DeepSeek command line or test result log was found. `re-analysis/workplans/M6-plan.md` states that `EveryShippedBankParsesExactly` is valid regression evidence, and commit `7842392` adds the locator fallback and combined runtime test, but neither is execution evidence. The exact test DeepSeek ran cannot be established from repository source or logs. Depending on its worktree and exact revision, the legacy test could have returned early, found the separate ancestor `sound_meta` copy, or used the new archive fallback. Therefore the reported pass is ambiguous and cannot be cited as proof that the six shipped banks parsed.
