namespace Cozmo.Robot.Animation;

// fidelity: M5-028, M5-029, M3-012
/// <summary>
/// One read-only observation from <see cref="AnimationScheduler.Diagnostic"/> (B-M5-32; operator decision 2026-10-20,
/// "M5 decisions before the rerun"). These observe engine state at the native point each record names. They are
/// instrumentation for the M5 hardware harness, not behaviour: they draw no random numbers, send nothing and change no
/// ordering. <see cref="Timestamp"/> is <see cref="System.Diagnostics.Stopwatch.GetTimestamp"/> taken at the observed
/// point (before the send, for a send gate), so a subscriber can bound it against its own Stopwatch clock.
/// </summary>
public abstract record AnimationDiagnostic(long Timestamp);

// fidelity: M5-028
/// <summary>
/// One <c>FaceLayerManager::KeepFaceAlive</c> call (A33; the blink timer at manager+0x14). <see cref="CallNumber"/>
/// rises by one for every call observed while a subscriber is attached. <see cref="BlinkTimerBeforeMs"/> is the timer
/// before the call's 60 decrement (0x0058D382..0x0058D38C); <see cref="BlinkTimerAfterMs"/> is the timer when the call
/// returns: the decremented value, or the new deadline drawn at 0x0058D54C..0x0058D550 when the blink branch ran.
/// <see cref="DrawMinMs"/>/<see cref="DrawMaxMs"/> are the bounds that draw used (after the max &lt;= min fallback),
/// null when no blink was generated.
/// </summary>
public sealed record KeepFaceAliveObservation(long Timestamp, long CallNumber, int BlinkTimerBeforeMs,
    int BlinkTimerAfterMs, bool BlinkGenerated, int? DrawMinMs, int? DrawMaxMs) : AnimationDiagnostic(Timestamp);

// fidelity: M5-029
/// <summary>
/// One <c>StreamLayers</c> frame (A36/Q1.8). <see cref="FaceFlag"/> is the layered face flag ApplyLayersToAnim(null)
/// returned (0x0064F204..0x0064F22C), which StreamLayers tests before buffering the face (0x0057C63C..0x0057C646).
/// <see cref="AudioBufferSequence"/> and <see cref="FaceBufferSequence"/> are the send-buffer sequence numbers of the
/// frame's audio message and of its face message (null when no face was buffered); they equal
/// <see cref="SendGateObservation.BufferSequence"/> of the same messages at the send gate, which is how a subscriber
/// joins this frame to the outbound stream order. <see cref="FrameNumber"/> rises by one per observed frame.
/// </summary>
public sealed record LayerFrameObservation(long Timestamp, long FrameNumber, long StreamTimeMs, bool FaceFlag,
    bool BackpackFlag, long AudioBufferSequence, long? FaceBufferSequence) : AnimationDiagnostic(Timestamp);

// fidelity: M3-012
/// <summary>
/// One message offered at the stream's send gate (<c>SendBufferedMessages</c>, C14, 0x0057BF96..0x0057BFCC), or the
/// direct EndOfAnimation send that bypasses it (0x0057C464..0x0057C496, <see cref="DirectBypass"/>).
/// <see cref="RemainingBytes"/>/<see cref="RemainingAudioFrames"/> are the allowances left at that moment (C9,
/// 0x0057C6F6..0x0057C7AC, less the earlier successful sends of the same Update). <see cref="Admitted"/> is whether the
/// buffered gate let it through (not applicable, and false, for a direct End); <see cref="Sent"/> whether the send
/// succeeded (only a successful send is counted, 0x0057BFAE). <see cref="BufferSequence"/> numbers buffered messages
/// in buffering order (0 when buffered with no subscriber attached, and for a direct End). <see cref="BudgetEpoch"/>
/// names the <see cref="BudgetRefreshObservation"/> whose allowances apply (0 when that refresh was not observed).
/// <see cref="MessageId"/> is the EngineToRobot tag, null when the buffered entry names no catalogued message.
/// Zero-size entries (stack bookkeeping, no robot message) are not reported.
/// </summary>
public sealed record SendGateObservation(long Timestamp, long BufferSequence, long BudgetEpoch, byte? MessageId,
    int Size, bool CountsAsAudioFrame, int RemainingBytes, int RemainingAudioFrames, bool Admitted, bool Sent,
    bool DirectBypass) : AnimationDiagnostic(Timestamp);

// fidelity: M3-012
/// <summary>
/// One <c>UpdateAmountToSend</c> (C9, 0x0057C6F6..0x0057C7AC): the played counters it read (C10), the streamed
/// counters at that moment (C11..C13), and the byte and audio-frame allowances it stored.
/// </summary>
public sealed record BudgetRefreshObservation(long Timestamp, long BudgetEpoch, int BytesPlayed, int FramesPlayed,
    int BytesStreamed, int FramesStreamed, int BytesToSend, int FramesToSend) : AnimationDiagnostic(Timestamp);
