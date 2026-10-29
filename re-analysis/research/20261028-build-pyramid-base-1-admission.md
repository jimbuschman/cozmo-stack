# BUILD-PYRAMID-BASE-1 inherited admission addendum

Requested by manager after frozen read; same reader requested gpt-6.1-sol/medium, no backend inference. Independent raw confirmation of review section Supplemental inherited join. Original read.md remains SHA256 4DE41CCCE488071ED627EEE01852CD6F440B9B09F3A80BE2F9D181C21410310C. No implementation or official metadata change.

**CONFIRMED**, bounded to the requested inherited join:

- Dynamic relocation at0x0103F3A4 references `_ZTVN4Anki5Cozmo24BehaviorBuildPyramidBaseE`, symbol value0x01029138. Constructor adds8 to the resolved table, so vptr=0x01029140. Raw words at0x01029160/164/168 (vptr+20/+24/+28) are0x005BF04D/0x0059EC13/0x005DDAF5. Low bit is Thumb mode. At0x005BF04C `movs r0,#0; bx lr`;0x0059EC12 likewise0;0x005DDAF4 `movs r0,#1; bx lr`. Return values0/0/1 independently confirmed.
- Relocations at0x01029188 (vptr+48) resolve InitInternal0x005DCB57;0x01029190 (vptr+50) IsRunnableInternal0x005DCAA5;0x0102914C (vptr+C) UpdateInternal0x005DD01D. These correspond to the original report's Base body addresses.
- IsRunnable0x005BD750..D770: r5=this,r4=caller Robot; loads this+2C into r1 atD756 and calls IsRunnableBase atD758. Return other than1 becomes0 and returns atD75C..D762. Only success loads vptr+50 (D768), restores r1=caller Robot (D766), r0=this (D76A), and tail-dispatches D770. No direct jump around the inherited admission gate established.
- IsRunnableBase prologue0x005BD77C/77E establishes r4=this,r5=Robot receiver. AtD864 loads **Robot+355** (not behavior+355). Nonzero calls this vptr+20 (D86C) with r0=this (D86E); result!=1 goes to failure D962 (D874). Base's return0 refuses this condition.
- AtD876 loads **Robot+34A**; nonzero calls this vptr+24 (D87E), r0=this (D880); result!=1 fails D886. Base's return0 refuses this condition.
- D888 loads [Robot+284], D88C loads its+8 carried ID; adds1 and skips gate only for ID=-1 (D88E..D890). Otherwise calls this vptr+28 (D894), r0=this(D896), and fails if result!=1(D89A..D89C). Base's return1 permits carrying at this gate. It does not bypass any other generic gate.
- IBehavior::UpdateInternal0x005BDA56..DA62 loads this+84; returns1 iff nonzero, otherwise2. This is the actual shared tail in the original report, not an inferred helper-active or cache-complete result.

Other generic admission gates retain existing M8-001/M8-013 ownership and uncertainty from the reviewed report's exact current-record appendix. This addendum confirms only the narrow slot/receiver/dispatch facts; it does not settle those whole records or permit missing inputs to imply readiness.

Commands: explicit `C:/Users/JimBu/AppData/Local/Programs/Python/Python313/python.exe` used for all Python calls. known.py run for0x005BF04C,0x0059EC12,0x005BD750,0x005BD864 before new raw lookup;0x005DDAF4 and0x005BDA56 were already class/body lookups in original read and were re-run before their final confirming dumps. Scratch dump.py disassembled raw ranges005BF04C..F050,0059EC12..EC16,005DDAF4..DAF8,005BD750..D772,005BD778..D96A,005BDA56..DA64; vtable.py and rel.py independently read ELF raw words/dynamic relocation symbol values. `admission-raw.txt` retains the generic raw range. No .NET, tests, git mutation, or hardware.
