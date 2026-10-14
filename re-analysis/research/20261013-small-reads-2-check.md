# Opus check of R-SMALL-2 (2026-10-13)

- **Checked:** `research/20261013-small-reads-2.md` (Codex, overnight P3), against libcozmoEngine.so. Read-only
  cozmo-extractor.
- **Adopted:** by the manager on 2026-10-13.
- **Result:**
  - **M15-044** is fully read and moves to IMPLEMENTATION_GAP.
  - **M2-022** is split. Its read parts go to the new record M2-023 (IMPLEMENTATION_GAP). M2-022 keeps the two unknown
    broadcast recipients and the C5-inside-Clear sliver as RECOVERABLE_GAP.
- **Scratch:** a unicorn emulation of the UnlockId map builder is in `.scratch/rsmall2/` (`emu_unlock.py`,
  `unlock_pairs.txt`).

## M15-044: UnlockId table, readAsJson, InitConfig

1. **The static map:**
   - **Construction:** guard acquire, then 0x51 (81) pairs, then the `__cxa_atexit` dtor, then guard release
     (0x00794D8E..0x007957A8).
   - **Codex's 81 rows are exact,** including:
     - the gaps at 4, 7, 8, 58 and 60;
     - Count = 85.
   - **Cross-checks:**
     - the emulation of 0x00794DB2..0x00795768;
     - all 81 `bl 0x4E02B2` sites;
     - Unity `UnlockId.cs` (authority 2).
2. **Lookup:**
   - **Hit:** returns node+0x14 (0x007957AC..0x007957BE).
   - **Miss:**
     - writes `error: string '<name>' is not a valid UnlockId value` + endl to **std::cerr** (GOT 0x0103FAD8), not to
       DAS (0x007957C0..0x00795834; strings 0x00795B40, 0x00795B50);
     - returns 0 (Invalid).
     - **Build:** write the same line to the stack process's stderr.
3. **readAsJson(Scope, path, root)** (0x0084BFD4..0x0084C004):
   - calls `pathToResource`, then the filename overload, and returns the parse bool (0x0084C1FE);
   - the filebuf open uses `fopen(path, "r")`. **On failure** it sets failbit and **still parses** (0x0084C076..0x0084C0C2);
   - **on false:**
     - `sErrorF("DataPlatform.readAsJson", "Failed to read [%s]", path)` (0x0084C0CC..0x0084C0E6);
     - `_errG` = 1, plus break-on-error (0x0084C10C..0x0084C11C);
     - if `getFormattedErrorMessages()` is non-empty: `sChanneledDebugF("Unnamed", "DataPlatform.readAsJson", "Json reader
       errors [%s]", msgs)` (0x0084C12A..0x0084C15C).
4. **JsonCpp Reader** (allowComments = 1, strictRoot = 0; 0x008E075A..0x008E12EC):
   - **Comments:** a trailing comment is attached to the root.
   - **Empty or invalid first token:** sets the offsets and adds the error "Syntax error: value, object or array
     expected."; the root's type and payload are unchanged.
   - **Object or array:** the root is swapped to an empty object/array first. The partial result is kept on failure.
   - **Depth > 1000:** throws, then terminate. That is the M1-034 standing rule.
5. **InitConfig** (0x0064BB68..0x0064BDB4):
   - **Early exits:** a null ctx returns 0; a null ctx+8 (DataPlatform) returns 0.
   - **The read:** `readAsJson(1, "config/engine/unlock_config_nurture.json")`. **On failure:**
     - `sErrorF("ProgressionUnlockComponent.InitDefaults.UnlockConfigJsonNotFound", "Unlock Json config file %s not
       found.", path)`;
     - `_errG` and break-on-error, **then continue**.
   - **Defaults guard:** if the set at 0x0105B560 is non-empty, return the read bool.
   - **A null root, or `root["defaultUnlocks"]` not an array:**
     - `sErrorF("ProgressionUnlockComponent.InitDefaults.MissingDefaults", "missing key '%s'", "defaultUnlocks")`;
     - `_errG` and break-on-error, then return 0.
     - On an object root, the non-const `operator[]` first inserts a null `defaultUnlocks` member.
   - **Per element:**
     - **a string:** UnlockIdFromString → insert into the set → `sChanneledInfoF("ProgressionUnlockComponent.InitDefaults",
       "ProgressionUnlockComponent.DefaultValue", "%s defaults to true", EnumToString(id))`;
     - **anything else:** `sErrorF("ProgressionUnlockComponent.InitDefaults.InvalidDefaultData", "invalid element in unlock
       id's list (not a string)")`, then `_errG` and break-on-error, then continue.
   - **A non-null root that isn't an object:** `operator[]` throws a LogicError, then terminate (0x008EABA6). The M1-034
     standing rule applies. The shipped file never reaches it.
6. **Callers:**
   - **Init** (0x0064B89E..0x0064B8AA) and **GetDefaultUnlocks** (only when the set is empty, 0x0064C0A2..0x0064C0BC)
     each pass a fresh null root.
   - Both ignore the return value.
   - Init keeps using that root afterwards (0x0064B8C4).

## M2-023 (new, IMPLEMENTATION_GAP): the read parts of M2-022

- **C4, the motor acks:**

  | Action | Fields | Log | Sets |
  | --- | --- | --- | --- |
  | Turn | +0xDB gate, +0xDA id | — | +0xDC = 1 |
  | Head | +0xAA, +0xA9 | `MoveHeadToAngleAction.MotorActionAcked` | +0xAB = 1 |
  | Lift | +0x95, +0x94 | `MoveLiftToHeightAction.MotorActionAcked` | +0x96 = 1 |

  Addresses: 0x0054D3F8..0x0054D462, 0x0054D624..0x0054D68E, 0x0054D748..0x0054D7B2. These handlers cannot destroy
  anything.
- **DA:** +0xC2 = liftLoad ? 1 : 2, with no action-ID filter (0x00557E82..0x00557E9C).
- **CA:** on an ID match with +0x8B, logs Info "Unnamed" "PlayAnimation.StartAnimationHandler" "Animation tag %d started",
  then sets +0x88 = 1 (0x005452F0..0x0054534E).
- **CB** (0x0054541C..0x005454F4):
  - It needs +0x88 set and an ID match.
  - If +0x84 == 0, it only logs; ended is never set.
  - Otherwise it decrements. On a nonzero result it logs Debug. On zero it logs Info and sets +0x89 = 1.
- **D5's own body:**
  - It needs +0x88 set and a tag match at payload+4.
  - It logs Info "PlayAnimation.AnimationEventHandler" "Event %s received at time %d while playing animation tag %d".
  - It sends AnimationEvent {payload+0, payload+5} (tag 0x60) through **UiMessageHandler::Broadcast(&&) at 0x006625B0**,
    vtable slot +0x1C. (Codex's 0x00662508 is the `const&` overload.)
  - Order: game delivery first, then the local AnkiEvent emit (0x0054561C..0x0054569E, 0x00545750..0x00545792).
- **C5's own body:**
  - Trigger 0x23F: nothing happens.
  - No HasResponse: a warning.
  - `movingLiftPostDock` != +0x80: a warning.
  - **Otherwise:**
    - an Info log;
    - `new TriggerLiftSafeAnimationAction(robot, trigger, 1, false, 0, 60.0f, false)`;
    - `QueueAction(5, a, 0)`, which goes to AddConcurrentAction (0x0053DF2C..0x0053E004): no Init, no broadcast.
  - **There is no action-state gate.**
  - QueueAction's broadcasting rejections are unreachable here, because `sTagCounter` tags are always ≥ 3000001.
  - The only rejection is IsDuplicateOrCurrentlyClearing:
    - a duplicate gives a warning;
    - while clearing, it calls PrepForCompletion and then the deleting dtor.
- **EF, the direct body:**
  - The robot ID comes from closure+8.
  - **No robot:** `sWarningF("FirmwareUpdater.HandleFlashWriteAck.NoRobot", "No Robot entry with id %u")`.
  - **Status 0 or 0x0A:** updater+0x50 = ack+0.
  - **Any other status:**
    - `sErrorF("FirmwareUpdater.HandleFlashWriteAck.Error", ...)`, then `_errG`;
    - then GotoFailedState(…, 4):
      - an Info log "Was State %s:%s";
      - +0x58 = 2;
      - SendCompleteResultToGame: DAS `robot.firmware_upgrade_complete`, then FirmwareUpdateComplete (tag 0x81) via
        Robot::Broadcast.
  - **Both paths** clear `[r7+4]` through a pointer taken before the broadcast (0x00678064..0x0067811C,
    0x00676D80..0x00676FA6).
- **Re-Init** (Codex confirmed, with these additions):
  - Init is reached only from UpdateInternal (0x00540F34), never from a robot-message handler.
  - **Nonzero Init with +0x70 already set:** CheckIfDone (0x00540F3A).
  - **A failure that is neither RUNNING nor retry:** runs the completion callbacks at +0x64 synchronously
    (0x00540EEA..0x00540EFE).
  - **Timeout:** 0x03000018 with a warning (0x00540E7C..0x00540EB6).
  - **PlayAnimationAction::Init** replaces the handles at +0x98, +0xA0, +0xA8 and +0xB0.
  - **The `IsInSdkMode` branch** (0x0054044E) is out of scope.
- **ConnectToRobot registration:**
  - **Registration:** in the CozmoEngine ctor, `bl 0x4EBB6C` at 0x004EB5CC registers tag 0x4C through UiMessageHandler
    Subscribe (vtable +0x2C) and pushes the handle into the vector.
  - **Dispatch:** invoker 0x004EE936 → veneer 0x004EE94A → PLT 0x004A544C → `CozmoEngine::HandleMessage<ConnectToRobot>`
    0x004ED018.
  - **When it is sent:** an app input, already covered by M2-021.
- **~Robot r5:**
  - Robot+0x450 is the BlockTapFilterComponent.
  - The dtor clears +0x2C and +0x20, releases +0x14, destroys the handle vector, deletes the component, and clears +0x450
    (0x005111EA..0x00511220).
- **ProcessMessages with no robot:** PopData, then +0x38++, then GetFirstRobot. A null robot drops the packet unfiltered
  (0x0069D8BC..0x0069D8D8).

## Still unread (M2-022, RECOVERABLE_GAP)

1. **The local E2G subscribers to tag 0x60 (AnimationEvent) and tag 0x81 (FirmwareUpdateComplete),** and whether they
   run synchronously.
   - **What doesn't settle it:** a heuristic scan of the 42 SubscribeToTags sites, which found nothing.
   - **Still to read:**
     - the vtable+0x28 Subscribe sites;
     - the rodata tag arrays;
     - the JSON-configured sets: StrategyGeneric::ConfigureRelevantEvents 0x006137B6/0x0061389A, and
       ReactionTriggerStrategyGeneric 0x0060F5D8.
   - **Why it matters for EF:** a recipient that mutates the updater's tracked vector would turn EF's writes into stale
     writes (a crash path, M1-034 standing rule).
2. **Whether a robot-message dispatch can run nested inside `ActionList::Clear`** (0x0053D916..0x0053D93A), e.g. from
   action dtors 0x00541084 or 0x00543DAC.

## For the operator

**Firmware update scope.** EF runs only during a firmware update, and the app starts that. The stack accepts every
firmware (M1-040). Whether the stack offers a firmware update at all is an operator scope question. Until it's
answered, EF stays recorded but unbuilt.
