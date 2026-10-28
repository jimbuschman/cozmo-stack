# HIKING-ROLL-CUBE-1 â€” PlaceBlock and nonempty Roll response joins

2026-10-28, root final finite completeness request. Frozen main source packet unchanged. No implementation/tests/git/official metadata changes. Requested reader gpt-6.1-sol/medium; actual runtime telemetry unavailable. Existing instruction-checked primary rows are reused below; director/lookup/AI+C instructions were directly rechecked against the same SHA02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1 ELF after known.py. No framework audit.

## PlaceBlockHelper is not the direct Ground runner

The EXISTING checked primary `20261007-q17-checked-additions.md` PB1â€“PB4 (also `20261007-M7-M8-rows.md` PB1â€“PB4), and `20261004-helpers-extraction.md` G01 establish:

|Step|Native contract|Citations|
|---|---|---|
|Factory/ctor|IHelper.CreatePlaceBlockHelper forwards actual factory; allocationF0; ctor common IHelper name PlaceBlock, concrete vptr. ShouldCancelDelegates=false.|5B6EE6;5B5588 (allocation5B5590);5B8C1C..C6A;5B8CA6..CA8;H01/H02/PB1.|
|Init/RNG|Actual common RNG double RandDblInRange(PI/4,PI/2), then independent RandDbl(1). If second<.5 negate first by sign-bit XOR. Narrow chosen angle ONCE tofloat32. Construct TurnInPlace(robot,angle,false), StartActing with RespondToTurnAction; return status1C; discard scheduling bool.|PB2,5B8CB0..D50;double bounds3FE921FB54442D18/3FF921FB54442D18,limit3FF0000000000000,threshold3FE0000000000000;round5B8D1E.|
|Turnâ†’Ground|RespondToTurnAction ignores original turn result, constructs sequential compound containing actual PlaceObjectOnGroundAction, AddAction(false,true); schedule through real common owner with RespondToPlacedAction.|PB3,5B8DF4..E44.|
|Completion/update|Update returns retained status1C; placed callback ignores Ground/compound result and unconditionally stores status2. The Roll parent success callback then writes120=true, re-evaluates DetermineAppropriateAction and returns1.|PB4,5B8E60..E68;Roll5BAA82..A92. No success-only Ground test or raw carry clear may replace this.|
|Active/stop/lifetime|Common first update Init/status/delegate ownership H05; actual common Stop cancels owner's action ONLY active==1&&IsActing viaStopActing(false,true), then virtual24. PlaceBlock slot01025F24 and Rollslot01026118 both raw5B5F1Dâ†’5B5F1C BXLR; no class-specific stop hook action/reset/callback. Shared stop stack/proxy/retirement remains exact reused owner.|H05/H06,5B64B8..6659,5B6768..67F7;`overnight/20261015-ov74-m12-047.md` IHelper::Stop downstream/concretevtable rows;common checked cancellationrecipient.|

Ground's existing native action/wire/verify/cleanup payload-availability contract is reused AFTER the PB3 call, not instead of PB2/PB3/PB4. This explicitly preserves random turn and unconditional native placed callback semantics.

## Roll's nonempty converter reaches actual AI response director

EXISTING primary `20261004-helpers-extraction.md` A01â€“A04 and A06 cover converter gating, response construction and callback capture/clear/order. The same report deliberately named the director as a boundary; that boundary was subsequently closed for the relevant tuple in `20261026-pickup-pyramid-1-read.md`, section **Helper/result/director packet (B extraction, A independent PASS)**, converter paragraph (line48): AI+C owner569A94..AA2, fixed director56D86C, lookup56D904. The DIRECT ELF recheck confirms:

1. AIComponent ctor allocates16bytes at569A94..A9A, callsdirector ctor viaPLT4AC928 at569A9E and stores actual owner+C at569AA2. Genericresponse loads actualRobot+264, thenAI+C at5B71F4..F8; call5B71FAâ†’PLT4B2208â†’56D904. Not a global static response table detached from robot lifetime. ExistingOV105â€“108 directorD0/destroyed tree path56D968..D986 remains its lifetime contract.
2. Directorctor56D894 stores key1;56D8B2/56D8B8 stores value1FA.56D8BC stores key0;56D8D6/56D8DA stores value1F9. No third key/config/RNG/timer is constructed. Getter56D904..946 performs exact key lookup; missingâ†’23F.
3. Roll's NONEMPTY converterA06 at5BAC3E..54 maps04000003â†’1;success0â†’2;otherâ†’0. Therefore actual tuple is04000003â†’1â†’trigger1FA(RollBlockRetry),success0â†’2â†’unmapped23F(bypass),everything elseâ†’0â†’trigger1F9(RollBlockRealign). Convertedvalue3 would bypass but Roll mapper does not produce3. CANCELLED also maps0, so native response scheduling precedes RespondToRollingResult's laterCANCELLED-preserve decision; do not shortcut cancel around converter.
4.5B71DA..206 invokes nonempty converter then actual director;missing23F goes bypass. Bypass5B7208..7252 copies original callback, destroys/clears stored callback then converter BEFORE invoking original raw ActionResult andRobot. Response5B7282..72C8 constructs TriggerAnimationAction(actualRobot,chosenTrigger,loops1,interrupttrue,tracks0,timeout60f,strictfalse) and schedules throughIHelper.StartActing. Clears converter. Deferred closure5B7752..77E8 ignores animation result, copies and clears stored original callback before invoking captured ORIGINAL raw ActionResult/currentRobot. Exact common callback mechanismA01â€“A04 reused; Place empty-converter SF2 alone is insufficient evidence.

This completes the two finite source joins and does not claim the broad helper/ActionList/animation/director records settled. No unsupported default-director, zero-RNG, synthetic Ground success or empty-converter substitution. Builder must use the actual canonical RNG/owner/children/director/response scheduler and keep existing visibility for any absent shared input.
