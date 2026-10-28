# SPARKS-STACK-1 — PlaceRel completion unused-ID policy

Operator2026-10-28 explicitly approved: **“Approve zero unused ID slots.”** This is an approved compatibility policy, not a claim that the native unused raw words are source-exact or automatically covered by M1-034. Official record/inventory/status allocation and approval remain Claude-owned.

## Source result and why a decision was required

`PlaceRelObjectAction::GetCompletionUnion` (`0055515C..00555344`) computes/installs a chain, then unconditionally overwrites it with a final object-interaction payload (`00555324..0055533E`). Independent A/B reads of the final native setter/serializer show a normal payload with target ID, count1 and flagfalse. Its fixed five-word ID array contains `[target, 0, 0, phone-stack-address, 0]` on the normal located-target path: unused filter scratch contributes the address word. The const setter `0075C3F0..0075C436` copies all20 ID bytes and the count/flag; Pack `0075B4F0..0075B536` serializes all five IDs, count and flag.

The stack's public `ObjectInteractionCompletion` exposes all five words and `PackBody` serializes them. A count of1 therefore does not hide the unused raw fields. The normal phone address is not established as undefined merely because it depends on memory layout; AGENTS.md's standing M1-034 skip covers undefined/terminating behavior, not arbitrary defined differences. No source-backed chain or phone address is invented.

## Approved output boundary

For the source-supported normal guard—selected dock action explicitly2 or3 and actual target located—deliver:

```text
Word0 = source target ID
Word1 = Word2 = Word3 = Word4 = 0
Count = 1
Flag = false
```

Only the four unused ID slots are normalized. Preserve native result, type/tag, meaningful target, count/flag, completion timing/order and scalar/internal helper callbacks. Apply this in the PlaceRel completion producer; do not globally rewrite other actions' completion arrays or publish the earlier accumulated chain as the final payload.

When the source cannot establish meaningful fields on missing/unselected/uninitialized paths, this policy does not invent them. Keep payload availability explicit and apply only independently proved M1-034 smallest-unit skip/log handling, retaining available scalar result/callback processing. It does not authorize a success result, guessed target, count or flag.

## Exact operator checkpoint

Asked: “May I zero the four unused ID slots in PlaceRelObject completion reports, preserving the target ID, count=1 and flag=false? Two independent native checks found that one unused slot contains a phone stack address. This would be an explicit compatibility policy; formal records would remain with Claude.”

Answer: **“Approve zero unused ID slots.”**

The proposed new owner for currently unowned PlaceRel Verify/completion must state this exact policy boundary; it must not label the normalized raw fields EXACT_SOURCE. Existing narrower M12-017/025/034/037 records retain their own scopes and statuses. No manifest, approved inventory, snapshot or official record status is changed by this document.
