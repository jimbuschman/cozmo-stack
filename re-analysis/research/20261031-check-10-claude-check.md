# ChatGPT check 10, mood: Claude check (manager, 2026-10-31)

**Verdict: the mood core holds, with 22 of 37 rows SAME, covering decay, clamps, history, repetition penalty, tick order, the per-tick message
and simple mood. No difference reachable in normal running was found. Nothing settled; no status changed.** The report is
`research/20261031-check-10-chatgpt-extraction.md`. Its 37 rows are 13 MISSING, 2 DIFFERENT, 22 SAME. Rows 15 and 9..14 were re-read in the `.so`
and the C#. SAME rows were not re-read.

## Small fix

| Rows | Difference | Evidence | Records |
|---|---|---|---|
| 15 | **Event names ignore case.** The engine finds names with a case-sensitive `std::map<string>` (0x0067A2F4..0x0067A300). `Mood.cs:63` uses `OrdinalIgnoreCase`. No shipped name differs, so only a mistyped name from an API caller shows it. Change it to ordinal. | `.so`, C# | M7-012 |

## Not reachable in normal running

**Rows 9..14 and 5, 6, 11, 12 (MoodMessage).** The engine handles the four variants, and the stack logs MISSING for each. In the Unity sources
the only sender is the debug `EmotionPane` (SetEmotion). `Robot.AddToEmotion` has no caller, and nothing found sends GetEmotions or
TriggerEmotionEvent as a mood message. The stack has no public API for them. So this is debug-only app traffic. The record stays as it is; if
a public mood API is wanted, it is a feature decision.

## Not blockers

- **Rows 3, 4:** `robot.mood_values` and the central info log. Log only under AGENTS.md.
- **Rows 7, 8:** `MoodManager::Reset` and `GetHistoryValueSecondsAgo`: no live caller shown by the report. Unverified, left open.
- **Rows 16, 17:** lifetime notes; row 17 is unreachable as reported.

## Open

The end list names the JSON readers (`StaticMoodData::ReadFromJson`, `EmotionEvent::ReadFromJson`, `LoadActionCompletedEventMap`) and most
behaviour callers of TriggerEmotionEvent. The event names at those call sites were not compared.

## Method note

Needs and mood both held in their core arithmetic. The real bugs in checks 2..9 were in wiring and behaviour logic, so that is where to aim.
