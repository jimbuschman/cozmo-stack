# HIKING-ROLL-CUBE-1 — type4 drive and IDrive joins

2026-10-28, finite builder source questions. Read-only; no implementation, tests, git or official edits. Frozen main packet unchanged. Requested model gpt-6.1-sol/medium; telemetry unavailable. Known.py preceded original ELF instruction rechecks (same02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1). Scratchcheck `.scratch/hiking-idrive-follow.txt`; no sibling/framework audit. Existing current record title/status/evidence reliance remains quoted in main packet.

## D1 — helper angle and03000013

The approved shared contract already covers type4: `20261028-pickup-children-1-read.md` row3, `20261027-pickup-approach-1-read.md` row2, `20261004-helpers-extraction.md` D03. Original instructions5B5DC2 readhelperF8 andcompare4;5B5DCA readhelper104;type!=4 or104==0→5B5DE0 return unchangedstatus;otherwise5B5DD2 clears104 BEFORE retry through5B5DD6..DDA→5B5CFE..D14→DriveToPreActionPose. This is not a failure normalization and not an unlimited independent counter. Existing actual helperattempt110 maximum3 still controls each retry entry.

Roll5B9AE8..AF6 sets DriveToParameters type4/forcefalse/angle0/useAnglefalse/offset0,0. When rollToUpright121 and anglelookup5B9B06 returns exactly1,5B9B0E reads returnedfloat,5B9B12 writesuseAngletrue atparams+0C,5B9B16 writesangle atparams+8. DriveToHelperctor5B5720..28 copies all six parameterwords tohelperF8..10F, soangle→helper100 anduseAngle→104. Ordinary DriveToPreActionPose5B5870..588E constructs default DriveToObjectAction(target,typefromF8,offset0,useAnglefalse,angle0,manualfalse);5B5892..589E checks104 andonlythen callsSetApproachAngle(helper100) onthat realchild. ForceFC remainsfalse for thisRollcaller, so the force/removal posefun is not installed. Type4 retry with104clear reconstructs default child withoutSetApproachAngle; it doesnot recompute upright angle or preserve staleuseAngle in a separately captured closure. Outer Roll helper may later construct a NEW DriveToHelper with newly computed angle, as its own source state allows.

## D2 — default IDrive subtree and both waits

Existing checked primary `20260929-R-VIS-M12-gap2-extraction.md` Q7.6–Q7.9 plus `20260929-R-VIS-verify-M12-gap2.md` Q7 PASS establish the actual base ctor/AddDock/wait joins. `20261028-knockover-cubes-1-read.md` rows15/18 provide latest common own-lifetime checks. Roll's source9arg caller55C9E6..CA18 supplies type4,distance0f,useApproachAnglefalse,angle0f,useManualSpeedfalse,Radians0,true;55CAF8..CB26 RollToUpright can then callSetApproachAngle.

- The real DriveToObjectAction at55B2C8 retains its ctor-default+150 function=GetPossiblePoses5585F8. Roll has no DriveAndFlip-specific installer. Reuse exact ordinary default preaction algorithm/goal-vector/inPosition/fullpose behavior from M12-029 and PICKUP-CHILDREN; type4 and actualapproach fields are this caller's inputs. No Flip picker, closest-only reduction or manufactured goal.
- Actual base ctor builds innerSeq[Drive Add(false,false),Wait1 Add(false,false)], addsinner toouter withAdd(true,false); then optionalface/objectturn children follow sourceRadiansGT(max,0). This order/ignore gate remains unchanged. Carrytarget branch omits thesebasechildren.
- Wait1 is actual WaitForLambda type33,timeoutFLTMAX; vtable10227B4 operatorcell10227D4=55DE35→55DE34. D4 remains0 in9argctor, so noextraIDrive drivinglight. If retaineddockweakC8 isalive, resolveactualdockC4 and setB9=false (55DE8E..DEEA). Missingdock takesDEFINED NoDockActiondiagnostic/errorflag, then returns1; it isnot null dereference or synthetic failure. For Roll AddDockAction installsactualdockweak, unlikeDriveAndFlip's plainAdd. Wait1 runs only when precedingdrive succeeds withininnercompound. Innerfailure isignored byouter anddoesnot runWait1.
- AddDockAction55B7AC constructs Wait2(vtable10227F4) and appendsit toOUTER first withAdd(false,false), BEFOREdock.55B820..826 copiesdistanceD8 todockBC;55B82C..838 appendsdock withcallerignorefalse andsecondfalse;55B83A..868 retainsdockweakC4/C8 andreturnsitsweakhandle.
- Wait2 operatorcell1022814=55E069→55E068; D4zero again skipsoptional light.55E0C8 checksfunctionF0;nonempty55E0CE..D4 invokesactualfunctionE0 withCURRENT Robot, then55E0E6 returns1. Roll copieshelperF8 callback tocompositeE0 at5B9F68..7A. Thatclasscallback5C8D6E..D76 setsclass129=true. Thus thisflag is set when OUTER reachespre-dock Wait2, even ifinnerdrive failedandwasignored, beforeinnerRollObjectActionInit/dock; do not substitute after-drive or after-dock timing.
- When RollToUpright anglelookup succeeds, IDrive.SetApproachAngle55BFA4 onlybeforeNotStarted02000001 andliveAC/B0 driveweak;55C00E..20 locks it,55C078 onward callsactualDriveToObject.SetApproachAngle withsuppliedfloat. This preservesordinaryposefun; it isnot a replacementcustompicker.

Shared actual Compound, WaitForLambda, optional face/turn, DriveTo lifecycle, typed completion/proxy, profile, cleanup and callbacks remain existing checked recipients. This supplement supplies caller-specific arguments, weakdock and callback order; it doesnot settle their larger records or certify missing external scene/firmware/progression inputs.

## D3 — exact construction clarification

- R13's frozen-packet label `accel0` is superseded: the third Radians argument is **variation0**, not acceleration. The actual tuple is MoveHeadToAngleAction(robot, target -15deg, tolerance2deg, variation0). Existing checked `20261007-q18-rows.md` H1 / `547EAC..547F40` establishes defaults speed15f (`41700000`), accel20f (`41A00000`), duration0. These remain unless the actual checked profile changes them. Caller556C7A..CA8 supplies those three Radians; it does not set speed/accel to0.
- Place PB2 uses TurnInPlace(angle,false). Existing constructor545A78..84 constructs minimum tolerance `3D0EFA35` (2deg) atB0,545A96 sets1 then545AA4 writesD8=true. The supplied false is its actual constructor flag atC0 (545A98), not a reason to erase the tolerance/D8 defaults. No new turn arithmetic is claimed.
- Roll oldPoseF8 is the target pose expressed WRT **actual MAIN.GetParent**, not MAIN itself:5568C2 calls actual MAIN accessor4EA398,5568CC GetParent,5568DA target.GetWithRespectTo(parent,outF8). A nonroot parent remains that exact parent, not a forced root-flat adapter.
- Saved marker104 comes from `Block::GetTopMarker`556956→4E69B8, not literal Block::FaceName.Top. It iterates actual known markers, flattens each marker pose to root at4E69EC, calls exact KnownMarker.ComputeNormal at4E69F8 and selects the greatest unnormalized normal dot WORLD +Z, strict greater at4E6A2A..40 (first existing candidate retained on ties). Existing M4-052/OV129 checked N1–N4 and corner/normal contracts supply that recipient. Literal marker face5 is used by the separate RollToUpright preaction approach lookup55CCF6 and must not be confused with this geometric top marker.

## D4 — zero-constructor-angle setter consequences

The generic optional-face wording in D2 must be applied to the actual Roll caller, not to its later desired parameter. StartRollingAction5B9F36..4A creates **Radians0** and passes it into DriveToRollObjectAction.55C9E8/55C9F2 copies that zero to the base max-turn argument;55B384..392 RadiansGT(max,0) is false, so the base constructs NO face/object-turn children and weakB4/B8/BC/C0 stay null.

Subsequent setters do not create missing children. SayName55B8CC..D0 seesB8null and returns55B94E; NoName55B9AC..B0 seesB8null and returns55BA2E (no child and no store). MaxTurn55BAFC..BB14 checksC0/B8: null takes warning55BB74..84 (`Can not set angle of null actions (the action were originally constructed with an angle of zero)`) and returns55BBAA..AC, without creating actions. Therefore the parsed ordinary PI/2 / hard-spark0 helper parameter is retained and the setter is called as source, but it does not add face/object turn children to THIS Roll composite. The native IDock turn/visual children still execute independently. This is source-proved no-effect for these three setter calls on this concrete zero-angle instance, not an unsupported-feature exclusion or a generic no-effect claim.

## Current ownership after imported manager metadata

Current manifest records were reread after manager516b7704 import. M15-076 owns both StackBlocks instances; it does not absorb Roll. M12-050 owns PlaceRel Verify/completion only; it does not absorb Ground or Roll completion. These titles/evidence leave the main packet's coherent new Roll own-path proposal unchanged. No allocation, status or approval is changed here.

```json
{
  "id": "M15-076",
  "title": "BehaviorStackBlocks, both shipped instances (SparksStackBlock and ordinary StackBlocks): target caches and validity, Init, pickup and PlaceRel helper delegation, one retarget, ground recovery, success objective/animation/needs and stop/reset",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "Factory 0x0059CFE6; class range 0x005C93D0..0x005CA33A: ctor 0x005C93D0, IsRunnableInternal 0x005C9484, Init 0x005C9502, stacking 0x005C9520..0x005C961A, StopInternal 0x005C97F8.",
    "UpdateInternal 0x005C9844..0x005C9A88 (M7 inventory row 885, corrected 2026-10-28): both targets valid skips the neighbour query (0x005C98BC..0x005C98C2); bottom invalid -> FindObjectOnTopOrUnderneathHelper(bottom, 15.0f, filter, true) must return the top (0x005C99C0, 0x005C9A1E); one GetClosestValidBottom retarget (0x005C9A5E..0x005C9A84).",
    "Target caches: entries 0x0056F724..0x0056FDA2, dependencies 0x00571040..0x005710CC (5->3) and 0x004D705E..0x004D7072 (4->2), predicates 0x0057052C..0x0057066A / 0x0057154C, entry cadence 0x005711E4..0x005712B6; progression unlock 9 or stackInAnyOrientation selects 5/3, otherwise 4/2 (0x005C94A6..0x005C9500, 0x005C984E..0x005C988C); GetRotatedParentAxis<'Z'> 0x005507A0..0x00550854.",
    "Success 0x005C9EA4..0x005C9F14: objective 40, optional animation 0x21A with score, then the configured needs action (43 for SparksStackBlock, 42 for StackBlocks).",
    "Shipped configs: SparksStackBlock (activity 27) and freeplay/stackBlocks.json (behaviorID 0xA0, executable 9, unlock 10). Read packets re-analysis/research/20261028-sparks-stack-1-read.md and 20261028-stack-blocks-1-read.md with their independent checks; HANDOFF-SPARKS-STACK-1.md, HANDOFF-STACK-BLOCKS-1.md; re-analysis/research/20261028-sparks-stack-1-claude-check.md, re-analysis/research/20261028-stack-blocks-1-claude-check.md."
  ]
}
```

```json
{
  "id": "M12-050",
  "title": "PlaceRelObjectAction Verify and GetCompletionUnion: dock-success and carry checks, the saved-carried turn child, high-place lift child, failure cleanup, and the ObjectInteraction completion payload",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "Verify 0x005555B4..0x0055581E: selected dock action 2/3 checks, docking success and carry predicates, TurnTowardsObject saved-carried child, first/second Update sequence, failure cleanup, high-place MoveLift child.",
    "GetCompletionUnion 0x0055515C..0x00555344: the chain it builds is overwritten by the final object-interaction payload at 0x00555324..0x0055533E.",
    "Completion setters 0x0075C3F0..0x0075C436 / 0x0075C460..0x0075C4A6 copy the five 32-bit IDs, count and flag; Pack 0x0075B4F0..0x0075B536 serializes all five IDs, count and flag.",
    "Normal located-target path: IDs [target, 0, 0, phone stack address, 0], count 1, flag false (jobs/trial-m13/SPARKS-STACK-1-COMPLETION-POLICY.md).",
    "Read packet re-analysis/research/20261028-sparks-stack-1-read.md and its independent check; HANDOFF-SPARKS-STACK-1.md; re-analysis/research/20261028-sparks-stack-1-claude-check.md"
  ]
}
```

## D5 — concrete Roll admission virtuals

Independent primary checker resolved the actual BehaviorRollBlock vptr `010277B0+8`, reusing the already checked base caller-to-gate mapping. Slot20 cell010277D8 contains005BF04D →005BF04C `movs r0,#0; bx lr`; slot24 cell010277DC contains0059EC13 →0059EC12 `movs r0,#0; bx lr`; slot28 cell010277E0 contains005C8CE9 →005C8CE8 `movs r0,#1; bx lr`. Thus this concrete class's off-treads gate=false, byte34A platform gate=false, carrying gate=true. This is the actual Roll vtable, not inference from StackBlocks candidate flags. Common base eligibility/progression/manager inputs remain their own checked contracts and named gaps.
