# Q14 rate/frame: authored bus duck and subscription loop bounds

Checkpoint829; guard reread before each step; decisions only.116 CHECKED /24 PARTIAL /0 NOT DONE;140 AUDIBLE /0 INTERNAL unchanged.

Primary table103ACE0 slots104/108/F0 are9C6420/9C0D08/9F1DE0. Existing BH548A and NB16-20 own the successful reader order:ordinary properties plus nine fixed bytes;duration/raw word/duck count;18-byte duck records;FX count/bypass/seven-byte records/helper ID/shared byte;flag;U16 RTPC subscription count and bindings;named tails. The companion witness independently reads this layout from all15 hash-checked shipped Bus payloads,retaining raw values/records and uninterpreted tail bytes. It does not use the node common-reader layout or add a fictitious range block to the Bus parser.

The actual buses contain duck counts0(12)/1(3) and subscription counts0(10)/1(5). All authored duration inputs are1000 milliseconds. These are bank inputs,not assertions that every live bus limit/duration/subscriber remains immutable or every reader callback succeeds.

The14 saved states from826's9C4094/9C40C0 caps receive individual dispositions in JSON. Five have a positive loop-bound contradiction:

- Duck loop startsR4=0 at9C40F4,increments9C411C before callback,and comparesR4/R6 after a successful callback at9C40FC. Saved9C4100/4140/4148 states haveR4=24(36),outside successful or failing iteration1 for these authored counts.
- Subscription loop startsR6=0 at9C4228,increments9C42B0 after successful subscription,and comparesR7/R6 at9C42B8. Saved9C42B8/42C0 states haveR6=11(17),outside these authored counts0/1.

The other nine states remain explicit. R4 later becomes point count,byte span and remaining size;its mere value22/23 is not a universal counter contradiction. Likewise R6=0 inside the property-ID varint is compatible with the first subscription. No arbitrary register is treated as a loop index merely because its number looks large. Existing NB/BH/RB exact callback/gate/flag/result contracts remain credited;tails are not reopened from a saved cap alone.

This bounds five selected symbolic states against shipped payloads. It is not a proof of all mutable inputs,callback products,debug modes or global rate/frame aliases.043/092/099/120/124/130 remain PARTIAL/AUDIBLE. out of scope: storage/I-O,allocation implementation,per-sample DSP and excluded branches;no such body was descended into. No production,manifest,inventory,PROJECT_STATE,hardware or Q15 edits.
