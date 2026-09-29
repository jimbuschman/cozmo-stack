# Research request: the R-BEH2 pre-extraction (M7 behaviours, M8 framework)

- **Date:** 2026-09-29
- **Requested by:** manager (Claude)
- **Kind:** independent extraction
- **Answer file:** `re-analysis/research/20260929-R-BEH2-pre-extraction.md`. Write each item as it finishes, so a partial
  run still leaves finished items.

## Why this is needed

R-BEH closed part of M7/M8 and left the rest needing extraction (`re-analysis/jobs/status/R-BEH.md`, "Triage").
R-BEH2 (`re-analysis/jobs/R-BEH2.md`) builds them. It checks every row before using it.

Follow `re-analysis/research/README.md` and `.opencode/agent/cozmo-extractor.md` exactly: one row per
behaviour-changing step, an address citation or UNKNOWN, and no guesses. The Cozmo engine code is mostly **Thumb**;
cite branch veneers and PLT hops. Message layouts are in the CLAD generated serializers
(`unity/scripts/csharp/Anki.Cozmo.ExternalInterface/` and similar). Each record's current text is in
`re-analysis/fidelity_manifest.json`, and the rows are in `re-analysis/inventory/M7-behaviour.md` and
`M8-framework.md`. Already read, so don't redo it: `re-analysis/research/20260929-R-BEH-M8-framework-gap1-extraction.md`
(the IAction timeout, and the BehaviorHelperComponent runtime).

## Items, in order

1. **M8-008:** the 30.0 s IAction timeout fails the action with result 0x03000018. Where does the head-recalibration
   wait's caller receive that result, and does any M7 behaviour act on it? Name every branch on the result.
2. **M7-014:** the reaction-lock tables of five built behaviours: ReactToImpact, DriveOffCharger, Singing,
   AcknowledgeCubeMoved and ReactToOnCharger. For each: the table's contents, and where it's installed.
3. **M7-012 / M7-020:** `MoodManager::SendEmotionsToGame` 0x0067B724..0x0067B7F8, field by field, with the MoodState
   message layout from CLAD. Also the live caller of `HandleActionEnded`: the ActionList action-ended callback. What
   registers it, and what it passes?
4. **M7-019:** ReactToCliff's `robot+0xD9` / `+0xD8` (writers and meanings), and ReactToPickup's `+0x11C` / `+0x120`
   retry arithmetic.
5. **M7-021:** the state-name string at behaviour `+0x58` (the helper 0x005C0CA8 writes it). Does any
   behaviour-changing code read it, or is it only logged?
6. **M8-014:** `AIWhiteboard::UpdateBeaconRender` 0x0056AA3C, and the three game-message handlers registered with tags
   68, 69 and 53: what each does.
7. **M8-013:** `SelectionBSRunnableChooser::HandleExecuteBehavior` 0x0060AC2C, and the ExecuteBehavior game message's
   layout.
8. **M7-018:** the config-driven behaviour factory: how a shipped behaviour config (`behaviorClass`) is turned into a
   behaviour object. Then the implementations of FistBump, Hiccup and ReactToSparked (their state machines, with
   constants).

## Out of scope

- Any code, inventory or manifest edit. No branches, commits or pushes.
- M11..M14 (R-VIS has them) and M15 (R-M15 has it).

## Answer format

Per item, the table from `.opencode/agent/cozmo-extractor.md` (`step | what the original does | citation | record |
classification`), then contradictions with the current record, rows or code, weak evidence, and open questions.
