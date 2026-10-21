# R-M8-NAME-INPUT — animation completion name input boundary

Independent reading job; answer research/20261021-m8-animation-name-input-boundary.md.
Search research/inventory first; reuse 20261021-m8-animation-completion-payload.md
and its partial check. Do not re-extract union packing/copies, Prep or callbacks.

The report explicitly leaves shipped-name lengths and host encoding unknown.
For supported Play/Trigger animation entry points, identify the actual sources
of the action+0x78 name, including remapping, and their byte/string boundary.
Reuse the adopted 20261012 play-animation extraction. Count shipped animation
name byte lengths using the actual loader's naming rules, stating asset coverage;
distinguish that census from unconstrained API inputs. Compare current C# input
conversion with the native byte-string and generated app UTF-8 boundary. Does
any supported path establish non-UTF-8 or over255 input? UNKNOWN is acceptable;
do not infer input rejection from generated app serializers or invent a policy.
Return sources, counts, concrete boundary examples and remaining operator/API
choices. Read-only research, no code/test, inventory, fidelity or hardware work.
