# M12-007 — corrected build contract (operator approved)

**Operator approved the corrected contract and fix on 2026-10-28**, via the explicit reply “Approve corrected contract and fix”. Official inventory/status/approval changes remain reserved for Claude. The earlier approval request and contradiction below are retained for provenance.

Current record: **M12-007 IMPLEMENTATION_GAP**, “The two timed checks a pick-up is verified against”. Approved `re-analysis/inventory/M12-manipulation.md`, row 27, says the first verification stamp is compared against “the object last-observed time plus a dock-action timeout”. The manifest authority text repeats that interpretation. These official files have not been changed.

Independent extractor A and reviewer B reopened the actual instructions and found that interpretation is wrong:

- `0x00553C88..0x00553C8E` calls virtual `IsMoving` with output pointer `sp+0x48`.
- `0x00553D12` reads that output; `0x00553D1E/0x00553D20` adds the selected low/high timeout and compares with the retained first-verify stamp.
- ActiveCube's Observable virtual group at `0x0101CDEC`, slot `+0x10` (`0x0101CDFC`), resolves to thunk `0x004E3860`. The adjusted ActiveObject base uses `[vptr-0x18] = -132`; `IsMoving` returns the bool at ActiveObject `+4` and writes the timestamp at `+8`.
- Paired `SetIsMoving` thunk `0x004E3874` writes those same fields. Existing settled M4-009/CD10a,b evidence identifies motion-state transition writers for moved/stopped input, for connected and located objects. This is distinct from the visual `LastObservedTimestamp` field at ObservableObject `+0x1C`.

Proposed corrected contract: retain the first-verify timestamp and existing unsigned inclusive 500 ms moving check; the second check uses **the motion-state timestamp returned by IsMoving**, plus the selected 500 ms low / 2000 ms high timeout. Recent visual observation must not stand in for this operand.

The pre-correction `PickupObjectAction.VerifyPrefix` uses `target.LastObservedTimestamp`. Passing regressions do not validate that mismatch. M12-007's proposed settlement was withdrawn pending approval and source recovery; any renewed proposal requires the corrected path and independent verification.

J15 now establishes the remaining input contract, independently checked by B: ActiveCube constructor writes defined timestamp 0 and moving=false (`0x004E0C84/0x004E0C86`); source type clones use a fresh constructor and reset both; pose-only copying preserves the destination object's motion fields. Moved input updates each connected/located object only on false-to-true, stopped input only on true-to-false, using the raw incoming robot timestamp. Dirtying guards do not suppress those motion stores. Current EXACT_SOURCE M4-009 already owns these writer semantics; adding the missing canonical timestamp restores its implementation without reclassification. No visual or facade timestamp fallback is authorized.

Requested approval is for this corrected build contract only. Codex will keep the official inventory, manifest statuses and approvals unchanged; Claude retains formal inventory reconciliation and settlement. After approval, the isolated builder can implement the independently checked reader/writer/default contract and regressions that distinguish motion timestamps from visual observations. Other independently verified changes can proceed without waiting for M12-007 settlement.

Evidence: `re-analysis/research/20261028-pickup-children-1-read.md` J14 and independent `20261028-pickup-children-1-read-codex-check.md`. AGENTS.md Process / Per layer requires new approval when an implementation finding changes the approved contract; the trial's stopping rules also flag primary-source contradictions.
