# SHARED-QUEUE-MISSING-1

The four archived MCP sessions contain engine-backed passes for head/lift/drive/turn, an authored animation, and pickup followed by ground placement. The updated [combined report](session-check-1/combined.md) has zero unresolved MISSING markers on the selected passing paths. Claude's main-path spot-check and Scope A count are pending.

## Native path

| Path | Evidence and disposition |
| --- | --- |
| Queue tick | `ActionQueue::Update` passes the running label and action name to the diagnostic calls at `0x0053F65C` and `0x0053F6F2`. It passes empty values at `0x0053F696` and `0x0053F6B4`, then calls `DeleteActionAndIter` at `0x0053F6D0` and maps the result at `0x0053F6D4..0x0053F6E0`. The diagnostic calls supply no queue state or result. |
| Viz label | `SetText` uses a 255-byte format buffer at `0x006C0330..0x006C033E`, constructs a local `MessageViz::SetLabel` at `0x006C038E` (constructor `0x007E8050..0x007E8076`, tag `0x23`), and clears it at `0x006C0394` (tag-specific cleanup `0x007E5D64..0x007E5D8A`). The shipped body sends and enqueues nothing. Both Viz send entries are return-only at `0x006BE610` and `0x006C05EC`. |
| SDK status write | `CozmoContext::SetSdkStatus` at `0x004EAD90..0x004EAD9A` dispatches through `IExternalInterface+0x38` (`UiMessageHandler` vtable slot `0x0102FE54`). The thunk at `0x0066340E..0x00663410` passes the embedded `SdkStatus` at UI +`0x68`. The writer at `0x00663886..0x006638C4` moves the string into `SdkStatus+0x24+12*type`, so type 1 is UI +`0x98`. |
| SDK status consumer | `Update` calls `UpdateSdk` at `0x00662C50`. The consumer returns unless UI +`0xE1` (`SdkStatus+0x79`) is set and an SDK socket at UI +`0xC` or +`0x8` exists (`0x00662F0A..0x00662F20`). The SDK constructor clears the mode byte at `0x0065DCC0`. `EnterSdkMode` reaches `EnterMode` at `0x0066151E`. The external branch sets the byte at `0x0065E16A` and exits via the corresponding clear at `0x0065E2C6`. The internal branch instead sets +`0x78` and +`0x7A` at `0x0065E172/0x0065E176`. The consumer copies three strings beginning at UI +`0x8C` (`0x00662FA2..0x00662FEA`), constructs E2G `SdkStatus` at `0x0066304E` (tag `0x86` at `0x00728142..0x00728144`), and calls virtual +`0x48` `DeliverToGame` with destination 0 at `0x0066305E`. Destination 0 selects the UI socket at +`0x4` (`0x006615FC..0x00661612`, `0x00661650..0x00661666`). The UI can therefore receive this telemetry **during external SDK mode**. SDK mode is excluded by the operator's 2026-10-09 app boundary and M1-051. |
| Ground completion | The ground runner's native type is `0x18` from `0x0055465E..0x00554660`. `PlaceObjectOnGroundAction::GetCompletionUnion` writes saved carried ID from this+`0x7C` at `0x00554AEC..0x00554AEE` and initializes count 1 and flag false at `0x00554AE4..0x00554AE6`. The other four ID words are unwritten. The setter called at `0x00554AF4` copies the carrier at `0x0075C3F0..0x0075C436`. M12-052 already records the M1-034 policy. Internal saved ID, count, flag and scalar result still reach the local `0x5A` recipients. The full public payload is withheld. |

## Build and capture review

- Queue ticks emit a single process-wide `StackDiagnostics` line for M8-015. Ground emits its existing policy as a visible M1-034 line once per runner. Other object completion owners continue to emit their existing MISSING diagnostics when their union is undefined.
- Updated M8-015 and M12-052's `unresolved` text and regenerated the fidelity report. Classifications, evidence fields and Scope counts were preserved.
- The checker retains the exact archived queue lines under resolved markers, including M2643 and M2862. The ground policy lines M7948 and M6022 are also preserved there. The following old generic notices (respectively M7957 and M6031) were emitted about 0.54 ms and 0.44 ms later on the same ground completion. The reports retain these generic lines as unattributed because the text supplies no action identity and other actions also use it. The selected pass uses local tag 3000172, which previously carried the recorded M1-034 policy as a MISSING marker. There are no new physical runs.
- The alternate 11/19 pickup attempts still show M10111, M10129 and M10130. The selected successful pickup uses tag 7 at 351818 ms. Other open rows are unaffected.
- The C# ground regression exercises both source-defined success and failed verification, asserts local fields and results, confirms the public event and raw payload remain withheld, and checks the policy message. The checker regression pairs a matching head and ground completion with the three legacy messages and proves a distinct M8-015 marker still blocks a lift result.

## Verification

Build `547fdd89` passed all 8,829 tests in 52.85 minutes with the local recordings enabled. All 70 focused C# queue, pickup, ground and charger replay tests passed. All 11 offline checker tests passed. The fidelity check passed for 765 records.
