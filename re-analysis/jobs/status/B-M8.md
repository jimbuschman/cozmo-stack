DONE 2026-09-28 21:59

Commit: acb9f95 (B-M8: build the M8 framework; settle 7 records). Verifier PASS; full suite 1721/1721; fidelity.py --check clean.

Records settled EXACT_SOURCE: M8-001, M8-002, M8-003, M8-005, M8-006, M8-007, M8-012.
Records left IMPLEMENTATION_GAP (cross-layer/unowned):
- M8-011: the SmartDelegateToHelper callee BehaviorHelperComponent::DelegateToHelper 0x0056dad8 and its helper-stack runtime are unowned; the reaction-lock manager side is M7-014.
- M8-013: SelectionChooser.RequestBehavior has no production caller because SelectionBSRunnableChooser::HandleExecuteBehavior 0x0060ac2c and the ExecuteBehavior message are M2/M10; the concrete activity bodies are M7/M15.
- M8-014: AIWhiteboard::UpdateBeaconRender 0x0056aa3c and the three handler bodies (tags 68/69/53) are unowned (VizManager / M11/M12).
Policies unchanged: M8-004, M8-008, M8-009, M8-010.

Inventory correction C1 (gap pass 6, six MISSING answers) folded in and re-approved; FIDELITY_GAPS.md regenerated.

For the integrator: new records are needed for the unowned components above (BehaviorHelperComponent, UpdateBeaconRender + its three handlers, SelectionBSRunnableChooser::HandleExecuteBehavior) before M8-011/013/014 can settle.

Robot check the operator may run (optional; write the steps, do not wait):
1. Cozmo on the floor, off the charger, on the robot's Wi-Fi; one cube powered nearby.
2. From cozmo-stack: git pull; dotnet run --project src/Cozmo.Conformance -- behavior 172.31.1.1 --obb "<unpacked OBB dir>" --seconds 60 --no-react
3. It exercises the built behaviour selection/switching (the M8 tick, choosers, scoring) for 60 s. Copy the printed bundle to Downloads and tell the manager.

No other window's files were touched.
