# Opus check of R-SMALL-1 (2026-10-13)

- **Checked:** `research/20261013-small-reads-1.md` (Codex, overnight P2), against libcozmoEngine.so. Read-only
  cozmo-extractor. Its scratch is in `.scratch/rsmall/`.
- **Adopted:** by the manager on 2026-10-13.
- **Result:**
  - **M11-072** moves to IMPLEMENTATION_GAP.
  - **M11-073** is split. The census and fault semantics go to the new record M11-076 (IMPLEMENTATION_GAP). M11-073 keeps
    the producer invariants as RECOVERABLE_GAP.
- **No contradicted records.** Two citation corrections, below.

## M11-072: RobotDelocalized (E2G tag 0x32) recipients, in order

Every Codex claim is confirmed. **Corrections and precision:**
- **The CubeLight callback** reaches HandleMessage<RobotDelocalized> 0x0063A390 through veneer 0x008CCCFC and PLT
  0x004B93C0, not by a direct branch.
- **The base PositionUpdate AlwaysHandleInternal** calls ResetReactionData **only when the tag is 0x32** (0x0061221E..
  0x00612224). The vt+0x2C tail call (0x00612236) is unconditional.

**The order** (Delocalize broadcasts through Broadcast(const&) 0x00662508 after a `cbz` on the interface at 0x00510D90):

| Row | Recipient | What it does | Addresses |
| --- | --- | --- | --- |
| R1 | Game | Game delivery (vt+0x48) first | 0x00662522 |
| R2 | BlockConfigurationManager (constructed in BlockWorld) | +0xC = 1 | 0x006172A6..0x006172B6; subscribed at 0x00616C76, gated on HasExternalInterface at 0x00616B9E |
| R3, R4 | CantHandleTallStack, twice (one per container: BehaviorManager 0x005A08D6, BehaviorSystemManager 0x005A5812) | HandleEvent 0x005BF268: AlwaysHandle → on 0x32, +0x124 = 0, then ClearStack (vt+0x90; clears +0x11C/+0x120 and releases the weak ref, 0x005ED1E4..0x005ED1F8); then vt+0x68/+0x74 by +0xA1 | the vt+0x68/+0x74 slots are `bx lr` (0x0059EC46, 0x0059EC4C); a non-0x32 tag reaches sErrorF 0x005ED26A, unreachable |
| R5 | CubeLightComponent | +0x21 = 1, skip carried objects, PlayLightAnim (the arguments belong to the CubeLight record) | 0x0063A390..0x0063A400; subscribed at 0x00637276, second of its helpers |
| R6 | FacePositionUpdated strategy | No-op ResetReactionData (it writes only a stack copy, 0x00612238..0x006122BE), then return | 0x0060CF6C |
| R7 | ObjectPositionUpdated strategy | The same, then return | 0x0061154A |
| R8 | CubeMoved strategy | The trigger-8 gate, then the 0x32 tag falls through doing nothing. HandleRobotDelocalized 0x0060C038 is a `bx lr` with no caller. | 0x0060BEA8..0x0060BF64 |

**Order of the emit:**
- After game delivery, the emitter walks the slot list in insertion order (append-before-head 0x006646A6..0x006646B0;
  walk 0x00663C74..0x00663CA6). Empty functions are skipped.
- Insertion follows construction order:
  - BlockWorld (0x0050FC66), then the BehaviorManager container (0x0050FCAC), then the BehaviorSystemManager container
    (0x0050FCBE), then CubeLightComponent (0x0050FDDE);
  - the strategies come later, from InitReactionTriggerMap (0x00510396), in shipped-map order: FacePositionUpdated,
    then ObjectPositionUpdated, then CubeMoved.
  - A strategy whose behaviour is missing is deleted (0x005A1872).
- **The construction-time Delocalize** (0x00510324) reaches only R1–R5, because the strategies don't exist yet.

**Exhaustiveness** (Codex left it UNKNOWN; now checked):
- every vt+0x28 Subscribe site with a 60-instruction window;
- the callers of all three SubscribeToTags wrappers (23 + 7 + 2);
- the generic factory table 0x00C75AC0;
- the tag-argument helpers;
- GetEToGMessageTypeFromString has no caller.

Residual: an indirect vt+0x28 call whose slot load lies more than 60 instructions before the call ("not seen", not a
proof).

## M11-073 / M11-076: GetParent and FindRoot on a root pose

**Settled, now M11-076 (IMPLEMENTATION_GAP):**
- **GetParent on a root** builds Pose3d(name), then WrapExistingNode(null), then `ldr r0,[r6,#0x48]` with r6 = 0: a
  SIGSEGV at **0x008452BA**. That falls under the **M1-034 standing rule**: skip, keep the prior state, log visibly.
- **Public FindRoot is root-safe:** 0x0084524E `cbz`, then it wraps the node itself (0x0084526A..0x0084526E).
- **PoseTreeNode::FindRoot** starts from the parent, and a null parent would fault at 0x008452E0. No unguarded caller was
  found.
  - **Its cycle guard (omitted by Codex):** after 1000 iterations (0x00845318) it calls sErrorF and
    sDebugBreakOnError (0x0084533C..0x00845376) and returns the current node.
- **The census:** 55 direct calls (30 GetParent via PLT 0x004A74D4; 25 FindRoot via PLT 0x004A40F0). These match Codex's
  table, with one correction: **0x004DF674 is FindRoot**, not GetParent.
- **The flatten helper 0x004DF628:**
  - **A root input** is copied, so the output is a root.
  - **Otherwise** it does FindRoot, then GetWithRespectTo.
  - **On failure:** sErrorF (0x004DF69C), and the output is the fresh root.
- **Guarded rows:**
  - the history inserts (0x00530E84, 0x005310B4);
  - ToPoseStruct3d's guards on the original node (0x00846D70/0x00846D7C);
  - IsSameAs (0x00846FA4);
  - SanityCheckOwnership (0x00848030).
- **Undefined-tree crash points** (the M1-034 standing rule; they arise only after a flatten failure on an invalid tree):
  - 0x00846D82;
  - 0x0062BA30 (closure created in FindObjectOnTopOrUnderneathHelper 0x006260C6..0x006260D8 over the flattened object
    pose).

**Still unread (M11-073, RECOVERABLE_GAP):** the producer invariants that would show whether the 23 unguarded (P) rows can
ever see a root in normal running.
- **Robot pose (ten rows):**
  - robot+0x298 starts as a bare root (0x0050FE9C) until Delocalize's SetNewPose (0x00510B4C).
  - SetPose 0x0051502C refuses an input not rooted at the world origin.
  - So the robot pose is a root only if a ramp or charger object pose is a root (SetOnRamp 0x0051664E/0x00516672,
    SetPoseOnCharger 0x005168BA).
- **Object poses** (0x006219A0, 0x00633CE4, 0x0062BA30): InitPose 0x004EF562; the ObjectPoseConfirmer setters
  0x00505DE0..0x00506F16; BlockWorld add/update.
- **PreActionPose marker parenting:** 0x0050DBE0, 0x0050DDA8, 0x0050DFFA, 0x0050F9FE.
- **History-pose producers:** UpdateFullRobotState 0x00512BEE; SetNewPose 0x0051271C; LocalizeToObject
  0x00515774/0x005159C8; LocalizeToMat 0x005160BC/0x0051612C; VisionSystem::UpdatePoseData 0x006B26E8. These cover
  0x00530804, 0x0053193E, 0x00531A00 and 0x00531AA0.
- **The pose-cluster input:** 0x0062A588.
- **The callers of SendAbsLocalizationUpdate** (0x0051275C).
- **GetWithRespectTo 0x00845A34:**
  - whether its output takes the reference pose as parent;
  - what it leaves in the output on failure.
- **Also unread:** whether any robot-pose reader runs between the Robot ctor and Delocalize.

No root-to-fault witness in normal running has been shown.
