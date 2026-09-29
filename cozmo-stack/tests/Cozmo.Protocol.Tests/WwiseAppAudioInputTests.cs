using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M6-023, the app audio-input dispatch (inventory rows A1-A5 on the Unity side and B1-B6 on the native
/// side). Every expected value here is the row's own:
/// <list type="bullet">
/// <item>the PostAudioEvent wire layout and <c>Size = 10</c>, and MessageAudioClient tag 0 (A4);</item>
/// <item>the envelope tag <c>PostAudioEvent = 1</c> and tags 2..6 (A5);</item>
/// <item><c>_GetPlayId</c>'s ++ / skip-0 and the callback-id rule (A3);</item>
/// <item>the subscription to tags 1..6 (B1) and the tag switch (B2);</item>
/// <item>the callback context built when callbackId != 0 (B5) and the flags formula
/// <c>1 | (ctx&amp;2)&lt;&lt;1 | (ctx&amp;1)&lt;&lt;3</c> (B6);</item>
/// <item>the route into the M6-006 <see cref="WwiseEventRuntime.PostEvent"/>.</item>
/// </list>
/// The callback context's low bits are not settled by the rows, so they are an explicit input, not a value
/// this test or the code invents.
/// </summary>
public class WwiseAppAudioInputTests
{
    // ------------------------------------------------------------------ builders

    private static void U32(List<byte> b, uint v) => b.AddRange(BitConverter.GetBytes(v));
    private static void U16(List<byte> b, ushort v) => b.AddRange(BitConverter.GetBytes(v));

    private static byte[] Chunk(string tag, byte[] body)
    {
        var b = new List<byte>();
        b.AddRange(System.Text.Encoding.ASCII.GetBytes(tag));
        U32(b, (uint)body.Length);
        b.AddRange(body);
        return b.ToArray();
    }

    private static byte[] File(uint bankId, params byte[][] chunks)
    {
        var bkhd = new List<byte>();
        U32(bkhd, 120);            // version
        U32(bkhd, bankId);
        U32(bkhd, 0);
        U32(bkhd, 0);              // feedback flag clear
        U32(bkhd, 0);
        var file = new List<byte>();
        file.AddRange(Chunk("BKHD", bkhd.ToArray()));
        foreach (var c in chunks) file.AddRange(c);
        return file.ToArray();
    }

    private static byte[] Hirc(params (byte Type, byte[] Payload)[] objects)
    {
        var h = new List<byte>();
        U32(h, (uint)objects.Length);
        foreach (var (type, payload) in objects)
        {
            h.Add(type);
            U32(h, (uint)payload.Length);
            h.AddRange(payload);
        }
        return Chunk("HIRC", h.ToArray());
    }

    /// <summary>The 28-byte empty node block the shipped reader consumes.</summary>
    private static void NodeBlock(List<byte> b, uint parent)
    {
        b.Add(0); b.Add(0); b.Add(0);   // fx byte, no fx, overrideAttachment
        U32(b, 0); U32(b, parent);      // bus, parent
        b.Add(0);                       // bits
        b.Add(0);                       // property count
        b.Add(0);                       // ranged count
        b.Add(0);                       // positioning
        b.Add(0);                       // aux
        b.AddRange(new byte[6]);        // advanced settings
        U32(b, 0);                      // state-group count
        U16(b, 0);                      // RTPC count
    }

    private static byte[] SoundPayload(uint id)
    {
        var b = new List<byte>();
        U32(b, id);
        U32(b, 0x00040001);   // codec plug-in (low nibble 1), so no source parameter block
        b.Add(0);             // stream type
        U32(b, 12345);        // media id
        U32(b, 0);            // in-memory size
        b.Add(0);             // source bits
        NodeBlock(b, 0);
        return b.ToArray();
    }

    private static byte[] EventPayload(uint id, params uint[] actions)
    {
        var b = new List<byte>();
        U32(b, id);
        U32(b, (uint)actions.Length);
        foreach (var a in actions) U32(b, a);
        return b.ToArray();
    }

    /// <summary>An Action (gapA 1.7) with no properties, a Play body and the 5-byte Play params (gapA 1.9).</summary>
    private static byte[] PlayActionPayload(uint id, uint target)
    {
        var b = new List<byte>();
        U32(b, id);
        U16(b, 0x0403);
        U32(b, target);
        b.Add(0);             // not a bus
        b.Add(0);             // no properties
        b.Add(0);             // no ranged properties
        b.Add(0);             // fade curve
        U32(b, 0);            // bank id
        return b.ToArray();
    }

    /// <summary>An event 900 -> Play action 100 -> Sound 500, the shape the routing test needs.</summary>
    private static WwiseBank RoutingBank() => WwiseBank.Parse(
        File(1, Hirc(
            (2, SoundPayload(500)),
            (3, PlayActionPayload(100, 500)),
            (4, EventPayload(900, 100)))),
        "t.bnk");

    // ------------------------------------------------------------------ wire layout

    /// <summary>
    /// Row A4: <c>PostAudioEvent</c> is <c>u32 audioEvent, u32 gameObject, u16 callbackId</c> with
    /// <c>Size = 10</c>, and <c>MessageAudioClient</c> tag 0 is PostAudioEvent.
    /// </summary>
    [Fact]
    public void ThePostAudioEventWireIsU32AudioEventU32GameObjectU16CallbackIdAndSizeTen()
    {
        Assert.Equal(10, WwisePostAudioEvent.Size);
        Assert.Equal(0, (byte)WwiseAudioClientTag.PostAudioEvent);

        var bytes = new byte[10];
        BitConverter.GetBytes(0x11223344u).CopyTo(bytes, 0);
        BitConverter.GetBytes(0x55667788u).CopyTo(bytes, 4);
        BitConverter.GetBytes((ushort)0x99AA).CopyTo(bytes, 8);

        var ev = WwisePostAudioEvent.Read(bytes);
        Assert.Equal(0x11223344u, ev.AudioEvent);
        Assert.Equal(0x55667788u, ev.GameObject);
        Assert.Equal((ushort)0x99AA, ev.CallbackId);

        // the explicit little-endian field order, so a reordering would fail rather than round-trip
        Assert.Equal(new byte[]
        {
            0x44, 0x33, 0x22, 0x11,
            0x88, 0x77, 0x66, 0x55,
            0xAA, 0x99,
        }, ev.ToBytes());

        // a shorter body is refused rather than padded
        Assert.Throws<InvalidDataException>(() => WwisePostAudioEvent.Read(new byte[9]));
    }

    // ------------------------------------------------------------------ envelope tags

    /// <summary>Row A5: the envelope tag is 1, and 2..6 are the other audio messages. Row B1: 1..6 are subscribed.</summary>
    [Fact]
    public void TheEnvelopeTagIsPostAudioEventOneAndTagsTwoToSixAreTheOtherAudioMessages()
    {
        Assert.Equal(1, (ushort)WwiseGameToEngineTag.PostAudioEvent);
        Assert.Equal(2, (ushort)WwiseGameToEngineTag.StopAllAudioEvents);
        Assert.Equal(3, (ushort)WwiseGameToEngineTag.PostAudioGameState);
        Assert.Equal(4, (ushort)WwiseGameToEngineTag.PostAudioSwitchState);
        Assert.Equal(5, (ushort)WwiseGameToEngineTag.PostAudioParameter);
        Assert.Equal(6, (ushort)WwiseGameToEngineTag.PostAudioMusicState);

        Assert.Equal(new ushort[] { 1, 2, 3, 4, 5, 6 },
            WwiseAudioInputDispatch.SubscribedTags.Select(t => (ushort)t));
    }

    // ------------------------------------------------------------------ app play id and callback id

    /// <summary>Row A3: <c>_GetPlayId</c> increments and skips 0, so the first id is 1 and a wrap never yields 0.</summary>
    [Fact]
    public void TheAppPlayIdIncrementsAndSkipsZero()
    {
        var client = new WwiseAppAudioClient(0);
        Assert.Equal((ushort)1, client.AllocatePlayId());
        Assert.Equal((ushort)2, client.AllocatePlayId());

        // 65535 + 1 wraps to 0; the skip makes it 1, not 0
        var wrapping = new WwiseAppAudioClient(65535);
        Assert.Equal((ushort)1, wrapping.AllocatePlayId());
        Assert.Equal((ushort)2, wrapping.AllocatePlayId());
    }

    /// <summary>
    /// Row A3: <c>callbackId = (flag != EventNone) ? id : 0</c>. The play id is allocated either way; only
    /// the callback id depends on the flag.
    /// </summary>
    [Fact]
    public void TheCallbackIdIsTheAppPlayIdOnlyWhenTheFlagIsNotEventNone()
    {
        var none = new WwiseAppAudioClient(0);
        var noCallback = none.PostEvent(123, 7, WwiseAudioCallbackFlag.EventNone);
        Assert.Equal((ushort)0, noCallback.CallbackId);
        Assert.Equal((ushort)1, none.PreviousPlayId);          // the id was still allocated

        var complete = new WwiseAppAudioClient(0);
        var withCallback = complete.PostEvent(123, 7, WwiseAudioCallbackFlag.EventComplete);
        Assert.Equal((ushort)1, withCallback.CallbackId);
        Assert.Equal(123u, withCallback.AudioEvent);
        Assert.Equal(7u, withCallback.GameObject);
    }

    // ------------------------------------------------------------------ flags

    /// <summary>
    /// Row B6: <c>flags = 1 | (ctx&amp;2)&lt;&lt;1 | (ctx&amp;1)&lt;&lt;3</c> for a non-null context; the
    /// null context is FUN_008D8CE4's other branch (0x008D8CEC) and gives 0 (0x008D8D30), never the
    /// formula's always-set bit 0.
    /// </summary>
    [Fact]
    public void TheFlagsFormulaIsOneOrTheContextsMarkerAndDurationBits()
    {
        Assert.Equal((byte)1, WwiseAudioCallbackContext.PostEventFlagsFor(0));
        Assert.Equal((byte)9, WwiseAudioCallbackContext.PostEventFlagsFor(1));     // 1 | (1<<3)
        Assert.Equal((byte)5, WwiseAudioCallbackContext.PostEventFlagsFor(2));     // 1 | (2<<1)
        Assert.Equal((byte)13, WwiseAudioCallbackContext.PostEventFlagsFor(3));    // 1 | 4 | 8
        Assert.Equal((byte)13, WwiseAudioCallbackContext.PostEventFlagsFor(0xFF)); // only bits 0 and 1 matter

        var ctx = new WwiseAudioCallbackContext(7, 2);
        Assert.Equal((ushort)7, ctx.CallbackId);
        Assert.Equal((byte)5, ctx.PostEventFlags);

        // the null-context branch is 0, not PostEventFlagsFor(0)'s 1 (0x008D8CEC -> 0x008D8D30)
        Assert.Equal((byte)0, WwiseAudioCallbackContext.PostEventFlagsFor((WwiseAudioCallbackContext?)null));
    }

    // ------------------------------------------------------------------ the dispatch

    /// <summary>
    /// Row B5 / FUN_008D8CE4: the context is built only when callbackId != 0. With no context the flags are
    /// 0 (the null branch at 0x008D8CEC sets 0 at 0x008D8D30); with one they run the formula.
    /// </summary>
    [Fact]
    public void TheDispatchBuildsTheContextOnlyWhenTheCallbackIdIsNotZero()
    {
        var runtime = new WwiseEventRuntime(new[] { RoutingBank() }, new WwiseRng(1));
        var dispatch = new WwiseAudioInputDispatch(runtime);

        var noContext = dispatch.HandleGameEvents(
            (ushort)WwiseGameToEngineTag.PostAudioEvent,
            new WwisePostAudioEvent(900, 7, 0).ToBytes(), 2);
        Assert.Equal(WwiseAudioInputOutcome.Posted, noContext.Outcome);
        Assert.Null(noContext.Context);
        Assert.Equal((ushort)0, noContext.CallbackId);
        Assert.Equal((byte)0, noContext.PostEventFlags);

        var withContext = dispatch.HandleGameEvents(
            (ushort)WwiseGameToEngineTag.PostAudioEvent,
            new WwisePostAudioEvent(900, 7, 5).ToBytes(), 3);
        Assert.NotNull(withContext.Context);
        Assert.Equal((ushort)5, withContext.Context!.Value.CallbackId);
        Assert.Equal((byte)13, withContext.PostEventFlags);
    }

    /// <summary>
    /// The app path's context: AudioMultiplexer::ProcessMessage stores 0xff as the context's first word
    /// (0x008DED46 <c>movs r1,#0xff</c>, 0x008DED4C <c>str r1,[r6]</c>) and 0 at +0x38 (queued, 0x008DED48).
    /// The dispatch supplies 0xff by default, so the flags are 1 | 4 | 8 = 13.
    /// </summary>
    [Fact]
    public void TheAppPathContextIs0xffAt0x008DED46AndGivesFlags13()
    {
        var runtime = new WwiseEventRuntime(new[] { RoutingBank() }, new WwiseRng(1));
        var dispatch = new WwiseAudioInputDispatch(runtime);

        var result = dispatch.HandleGameEvents(
            (ushort)WwiseGameToEngineTag.PostAudioEvent,
            new WwisePostAudioEvent(900, 7, 5).ToBytes());     // no explicit bits: the app path's default

        Assert.NotNull(result.Context);
        Assert.Equal((byte)0xff, result.Context!.Value.ContextBits);
        Assert.Equal((byte)13, result.PostEventFlags);
    }

    /// <summary>Rows B1/B2: tags 1..6 are subscribed, only tag 1 is handled, and anything else is not subscribed.</summary>
    [Fact]
    public void TheDispatchRecognisesTagsOneToSixAndOnlyHandlesPostAudioEvent()
    {
        var runtime = new WwiseEventRuntime(new[] { RoutingBank() }, new WwiseRng(1));
        var dispatch = new WwiseAudioInputDispatch(runtime);
        var body = new WwisePostAudioEvent(900, 7, 0).ToBytes();

        Assert.Equal(WwiseAudioInputOutcome.Posted,
            dispatch.HandleGameEvents(1, body, 0).Outcome);

        foreach (ushort tag in new ushort[] { 2, 3, 4, 5, 6 })
            Assert.Equal(WwiseAudioInputOutcome.SubscribedNotHandled,
                dispatch.HandleGameEvents(tag, body, 0).Outcome);

        Assert.Equal(WwiseAudioInputOutcome.NotSubscribed,
            dispatch.HandleGameEvents(0, body, 0).Outcome);
        Assert.Equal(WwiseAudioInputOutcome.NotSubscribed,
            dispatch.HandleGameEvents(7, body, 0).Outcome);
    }

    /// <summary>
    /// Rows B2-B6: an envelope posted through the dispatch reaches the M6-006 core, which queues the event
    /// and returns its playing id; the dispatch returns that same id, and the game object travels with it.
    /// </summary>
    [Fact]
    public void AnEventPostedThroughTheDispatchReachesTheRuntimeAndReturnsItsPlayingId()
    {
        var runtime = new WwiseEventRuntime(new[] { RoutingBank() }, new WwiseRng(1));
        runtime.RegisterGameObject(7);
        var dispatch = new WwiseAudioInputDispatch(runtime);

        var result = dispatch.HandleGameEvents(
            (ushort)WwiseGameToEngineTag.PostAudioEvent,
            new WwisePostAudioEvent(900, 7, 0).ToBytes(), 0);

        Assert.Equal(WwiseAudioInputOutcome.Posted, result.Outcome);
        Assert.NotEqual(WwiseEventRuntime.InvalidPlayingId, result.PlayingId);
        Assert.Equal(1, runtime.QueuedEventCount);       // queued, not run on the caller's thread (M6-006)
        Assert.Empty(runtime.ExecutedActions);

        runtime.AdvanceFrame();
        var executed = Assert.Single(runtime.ExecutedActions);
        Assert.Equal(result.PlayingId, executed.PlayingId);
        Assert.Equal(100u, executed.ActionId);
        Assert.Equal(7u, executed.GameObjectId);
        Assert.Equal(500u, executed.TargetId);
    }
}