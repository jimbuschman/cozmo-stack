# ChatGPT queue 5: Claude check (manager, 2026-10-31)

**Verdict: quotes and addresses held, but item 3 stopped short of its rules and mislabelled two strategy fields. Use this note where
the two differ. Nothing settled; no status changed.** The report is `research/20261031-queue-5-chatgpt-extraction.md`. Items 1 and 2
were reported Partial, item 3 Partial, item 4 Done for its direct paths, and items 5 and 6 Partial. Checks targeted conditions, labels,
open points and every rule that later work would build from. A row not named here was not re-read.

## Corrections

**1. Evidence boundaries: the segment rule is backwards.** Virtual address = file offset + 0x1000 (LOAD 0x1019200 at file 0x1018200).
Its addresses are virtual, so no row changes.

**2. Item 3 table: IActivityStrategy +0x04 is the owned IWantsToRunStrategy,** not a robot-related pointer. The ctor saves this+4
(0x005B4F1A..0x005B4F1E) and `wantsToRunStrategyConfig` stores the factory result through it (0x005B50F8..0x005B5104), so 0x005B5100 is
not a "separate pointee". The "nested predicate" in rows 3.23..3.26 is `IWantsToRunStrategy::WantsToRun`: veneer 0x008CBEAC resolves to
PLT 0x004B1F20.

**3. Item 3 table and row 3.11: +0x38 is a FeatureType byte, not a boolean.** `featureGate` goes through FeatureTypeFromString
(0x005B50D6..0x005B50DA). Zero skips the gate; otherwise CozmoFeatureGate::IsFeatureEnabled([Robot+0]+0x10, byte) must return 1. The
shipped value is `Singing` (twice).

**4. Rows 3.21 and 3.22: there is no "unlock conversion".** The call is
`RequestGameComponent::IdentifyNextGameTypeToRequest(robot)` on [Robot+0x264]+0x20, and 0x55 is UnlockId Count, meaning no game to
request.

**5. Item 5: FreeplayDataTracker +0x08 is the set's size, not its root.** The tree is at +0 (0x0056EEEA, 0x0056F00A pass the tracker
itself), so its root is +4. The empty test gives the same result. This is the same slip as queue 4 correction 4.

**6. Row 6.10 overclaims.** The globals are `Anki::Util::_errG` and `_errBreakOnError` (GOT 0x0103E790, 0x0103E78C). This is the error
log machinery, with no effect on 1-4. Return false for a missing need; nothing else follows from it.

**7. Item 6 plain rules: the two maps are the same map.** NeedsManager::InitReset passes manager+0x128 as the NeedsConfig
(0x00693558, 0x0069357A), and Init stores it at NeedsState +0x64 (0x0069C048). So [NeedsState+0x64]+0x18 and manager+0x140 are one
threshold map.

**8. Item 3 names:** the JSON `type` strings are not the class names. Shipped values include `PlayWithHumans` and
`SevereNeedTransition`. Text count of shipped strategies: Spark 14, Simple 11, Needs 2, Pyramid 1, SevereNeedTransition 1,
PlayWithHumans 1, NeedBasedCooldown 1.

## Closed beyond the report

- **WantsToStart, the full cooldown rule (rows 3.12, 3.13; 0x005B52C0..0x005B5360).** Arguments are (robot, deselected, selected). If
  +0x20 > 1e-5, and either deselected > 1e-5 or `startInCooldown` is set:
  - ran = deselected - selected. If 0 < ran <= 2 x GetTimeSinceLastTickInSeconds, wait = 3.0, else wait = +0x20. This is the M15-005
    flat cooldown; the report left it out.
  - If now < wait + deselected, reject.
  - Otherwise +0x20 = +0x1C + RandDbl(+0x24).
- **Mood gate (row 3.14):** reject if `required + (-1e-5) > score`. Literal 0x005B5404 is -1e-5.
- **On-treads gate (row 3.15), only when +0x34 > 1e-5:** t is [Robot+0x264]+0x18 +0x48, written by AIWhiteboard's
  RobotOffTreadsStateChanged handler (0x0056C7D6). Reject if t <= 1e-5, if t - 1e-5 <= selected, or if +0x34 - 1e-5 <= now - t.
- **WantsToEnd (rows 3.18, 3.19), with its literals:** if +0x14 > 1e-5 and +0x14 + selected - 1e-5 > now, return false. If +0x18 > 1e-5
  and +0x18 + selected + 1e-5 < now, return true. Otherwise call the subclass.
- **SetCooldown 0x005B54E4:** writes both +0x1C and +0x20 with the cooldown, and +0x24 with the randomness.
- **Spark ctor:** +0x18 = 0x7F7FFFFF (FLT_MAX) after the base parse, so a configured should-end time is overridden and never fires.
- **FPPlayWithHumans:**
  - `cooldownRejectionBaseSecs` is +0x40 and `cooldownRejectionExponent` +0x44 (ParseFloat, 0x005B3E72, 0x005B3EA4).
  - RequestGameStart stores now at +0x3C (0x005B4128).
  - DenyGameStart: if [+0x4C]+0x75 is nonzero do nothing. Otherwise +0x48 += 1, then SetCooldown(+0x40 x pow(+0x48, +0x44), 0)
    (0x005B4138..0x005B4198). +0x4C is [Robot+0x264]+0x18 (0x005B3E32).
  - WantsToEnd is true if selected + 1e-5 < +0x3C, or if no game remains.
- **ActivityStrategyNeeds end, the rule row 3.24 lacks:** null strategy: warn, true. No higher-priority strategy: not WantsToRun. If the
  higher-priority strategy wants to run: when SevereNeedsComponent +0x14 != 3, call ClearSevereNeedExpression, then return true.
  Otherwise not WantsToRun. **That call is a state effect, not a read.**
- **NeedBasedCooldown:** start 1 and end 0 (vtable 0x0102590C, 0x005B45A8, 0x005B45AC). The NeedsState handler ends in
  SetCooldown(graph1(level), graph2(level)) through veneer 0x008CBE6C, so the report's open tail is closed.
- **Pyramid (0x005B4970), so the report's "geometry-search loops" do not exist here:**
  1. Reject unless UnlockId 3 (BuildPyramid) is unlocked ([Robot+0x448], 0x005B498A).
  2. Find located objects with a filter holding families 2 and 1 (table 0x00C63260; which filter set +0x3C is was not read).
  3. If now > +0x3C: +0x40 = RandDbl(1.0) < 0.4, and +0x3C = now + 300.
  4. A full pyramid exists: false. Otherwise a base exists, or (+0x40 and at least 3 objects): clear +0x41, true. Otherwise false.
  5. WantsToEnd returns +0x41. BehaviorObjectiveAchieved with objective 3 (BuiltPyramid) sets it (0x005B4C96).
- **Item 1, event 53:** tag 53 is RobotOffTreadsStateChanged, and the closure calls ActivityFreeplay::HandleMessage (0x005AD9F8) through
  veneer 0x008CBDFC. M15-060 already owns it. Its rule: first payload byte zero, a current activity exists, its ID is not 7, 8 or +0x91,
  and its +0x4C is 0x55: OnDeselected, then clear +0x80.
- **Item 1, ordering:** both shipped `subActivities` values are arrays, and CZString::operator< compares array indices numerically
  (0x008E7E56, 0x008E7E8C). So vectors follow file order. The report's memcmp path is string keys only.
- **Item 1, keys:** ParseUint8's `activityPriority` result is discarded (r0 overwritten at 0x005AD640), as the report says. Shipped
  desired names are Socialize, Socialize, PlayAlone, Hiking.
- **Item 2:** slot +0x28 is `GetDesiredActiveBehavior` on Scoring, Selection and StrictPriority choosers (pure in the base). Their
  bodies were not read here.
- **Item 4:** +0x5C and +0x64 are the current and requested hard/soft flags (log labels `hard`, `soft`).
- **Item 6, the bracket rule row 6.03 omits (0x0069C158..0x0069C1F8):** for each NeedId 0..2, walk that need's thresholds in order. The
  first k with level >= t[k] is the bracket. If none matches, the bracket is n-1. An empty vector gives -1.

## Checked and matching

- **Item 1:** 1.04, 1.11, 1.16, 1.20, 1.21; JsonCpp function names.
- **Item 2:** the wrapper and base Internal, branch by branch.
- **Item 3:** the tbb table targets all seven ctors; `type` key; Simple and Spark returns by vtable; rows 3.23, 3.25, 3.26.
- **Item 4:** all rows.
- **Item 5:** wrapper, set path, 5.07, 5.08, IsActive.
- **Not re-read:** rows 1.22..1.36, 3.03..3.10 beyond ctor names, 5.09..5.11, 6.06..6.09.

## Still open

- AIComponent +0x18 byte +0x75, which gates DenyGameStart. No write found in named AIWhiteboard functions.
- FPPlayWithHumans +0x48 reset: none in the handlers read.
- Activity Freeplay +0x91. The three chooser bodies. [Robot+0]+0x1C's owner (the ActivityID to Json map).

## Records

The concrete strategy rules have no records of their own: M15-003 and M15-005 cite the base only, and M8-023 is teardown. No record
mentions DenyGameStart, BuiltPyramid's strategy use or NeedBasedCooldown. Proposed for operator approval: IMPLEMENTATION_GAP records
for the PlayWithHumans, Pyramid and NeedBasedCooldown strategies, and the Needs end path's ClearSevereNeedExpression. A settled M15-003
must not defer these.

## Method note

Quotes were accurate, and the requested veneer and vtable following worked in items 2, 4 and 5. Item 3 is where it fell back: it cited
gates without stating them, and it ran out before the small bodies. Its rule "state the rule" needs extending to "and the literal
values". Claude's check also found enum names it cannot see, so keep giving it the Unity CLAD gap in mind when reading labels.
