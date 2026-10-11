# Q14 rate/frame navigation: saved cap states and two channel-loop dispositions

Checkpoint826;guard reread before each step;decisions only. Coverage116 CHECKED /24 PARTIAL /0 NOT DONE;140 AUDIBLE /0 INTERNAL unchanged. Research only.

The companion script instruments819's unchanged160 seed/mode windows and4096-state bound. It preserves all456 event records exactly,including their seed sets;it does not rerun a new executable census or increase the bound to manufacture more negative results. New JSON retains all231 pending states at the15 saved caps,including instruction address,known register values and possible NZCV mask. These are symbolic navigation states,not actual runtime input witnesses. Each pending_count equals the number of saved states;none is silently truncated.

Two smallest cap frontiers have a positive source/scope disposition:

| Seed | Saved pending states | Primary contract and disposition |
| --- | --- | --- |
|A7255C|A725FC andA72630;R4=FC(252) in both|Direct ADPCM renderA72554 loads channel byte intoFP atA7257C. A725DC rejectszero loop;counterR4 starts0,A7260C increments1 perchannel call,A7261C/24 comparesFP/R4 and loops until equal. For retained sourcechannels1/2,the body executes one/two calls and exits withcounter1/2. Savedcounter252 is outside those admitted branch bounds. out of scope:census's >2-channel source branch;this is not another admitted alias/writer.|
|A73DBC|A73E9C;R7=11D(285)|Streamed ADPCM render loads channel byte intoR5 atA73DE0. A73E68 testszero;A73E74 initializesR7=0;A73E94 increments1;A73EA4/AC comparesR5/R7 and loops. Retained sourcechannels1/2 make one/two calls. Savedcounter285 is impossible on those branches(and exceeds the loaded byte's255 maximum). This symbolic path cannot establish an admitted frame-pointer escape.|

The shared decoder callA7A194 has explicit push/pop preserving R4/R5/R6/R7/R8/SB/SL/FP;the companion captures only those ABI boundaries,not the sample reconstruction body. Existing AC/AS/AT/AR contracts already paid decoding-call order,64-frame geometry and output/carry/refill controls. This checkpoint does not waive WEM decoding fidelity or introduce ADP-1 equivalence for ADPCM;it excludes the census's source branch and retains caller decisions.

The other13 capped windows remain explicit at9877F8,989FF4,98A190,9C4094,9C40C0,9D5974,9E6D2C,9EBE6C,9EDF5C,9EEC1C,9F12E8,A41E64,A613F8. Their228 saved pending states are available for bounded primary inspection. No claim that all their paths are exhausted,no new scalar writer,no extra admitted helper/provider and no all-runtime pointer/alias absence proof follows from these two channel-loop cuts. Six original rate/frame obligations043/092/099/120/124/130 remain PARTIAL/AUDIBLE.

The exact WEM decoding body is already paid by Q14-004/AC and is not reopened here. It is not classified as an ADP-1 exception. out of scope:backing allocation/storage/I-O and excluded source branches. Required frame/sample boundaries and parameter decisions stay exact. No production,manifest,inventory,PROJECT_STATE,hardware or Q15 changes.
