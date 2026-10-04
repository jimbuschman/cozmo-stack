using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// B-M6b-4 batch 5f, C37.1 (the value-change delivery of an RTPC set to a playing instance) and C31 R5.3 / R4.8 (<c>0xA548C0</c>). Every expected value is the engine's own output from
/// <c>re-analysis/tools/emu/emu_rtpcset.py</c> (the real <c>0xA114D8 -> 0xA10C84 -> 0x9868B0 -> 0xA1B254 -> 0xA1A6A4 -> 0xA02EC0 -> 0xA02CE4 -> 0x9BDDE0</c>, the real <c>0xA14E28</c>, the voice pre-pass step
/// <c>0xA55750</c> with the real <c>0x9FF368</c> and <c>0xA4B93C</c> gain store under Unicorn: <see cref="WwiseRtpcSetOracle"/>) or from <c>emu_decode_cases.py</c> (the real <c>0xA44630</c> with the real
/// <c>0xA548C0</c>: <see cref="WwiseVorbisEngineOracle.Pull"/>); none is the C#'s own output. Floats are compared as bit patterns (a NaN result as NaN only: the host's NaN payload is not the ARM default NaN).
/// TEST DOUBLES, named where used: <c>0xA1B5FC</c> (the gate of a first set; unread), the pre-pass tail <c>0xA43D6C..0xA43EFC</c>, the send-table tail of <c>0xA4B93C</c> and the context's CalcEffectiveParams (<c>vt+0x24</c>);
/// each is a required seam in the C# and the oracle stubs the same bodies.
/// </summary>
public class WwiseRtpcDeliveryTests
{
    private static uint Bits(float f) => BitConverter.SingleToUInt32Bits(f);
    private static float F(uint b) => BitConverter.UInt32BitsToSingle(b);

    private static bool IsNaN(uint b) => (b & 0x7F800000) == 0x7F800000 && (b & 0x007FFFFF) != 0;

    private static void AssertBits(uint[] expected, uint[] actual, string what)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (int i = 0; i < expected.Length; i++)
        {
            if (IsNaN(expected[i])) Assert.True(IsNaN(actual[i]), $"{what}[{i}]: engine NaN, C# 0x{actual[i]:X8}");
            else Assert.True(expected[i] == actual[i], $"{what}[{i}]: engine 0x{expected[i]:X8}, C# 0x{actual[i]:X8}");
        }
    }

    private static WwisePlayingInstance NewPbi(uint a, uint b)
        => new(new WwisePlayInitParams { PlayingId = b, TargetNodeId = 1, GameObjectId = a }, 1, new object(), new byte[0x44], new WwiseGainRtpcKey(a, b), continuous: false);

    /// <summary>Sets the observed fields in the oracle's order: ctx+0x8C, +0x38, +0x90, +0x94, +0x3C, +0x98, +0x9C, +0x40, +0x58, +0x48 = pbi+0x98, +0x44, +0x9C, +0xA0, +0x48, +0xA4, +0xA8, +0x4C, +0x64, +0x54.</summary>
    private static void SetFields(WwisePlayingInstance pbi, uint[] f, byte e9)
    {
        pbi.Field98 = F(f[0]); pbi.Pitch44 = F(f[1]); pbi.Field9C = F(f[2]); pbi.FieldA0 = F(f[3]); pbi.Lpf48 = F(f[4]);
        pbi.FieldA4 = F(f[5]); pbi.FieldA8 = F(f[6]); pbi.Hpf4C = F(f[7]); pbi.Word64 = F(f[8]); pbi.Field54 = F(f[9]);
        pbi.Flags0E9 = e9;
    }

    private static uint[] ReadFields(WwisePlayingInstance pbi)
        => new[] { Bits(pbi.Field98), Bits(pbi.Pitch44), Bits(pbi.Field9C), Bits(pbi.FieldA0), Bits(pbi.Lpf48), Bits(pbi.FieldA4), Bits(pbi.FieldA8), Bits(pbi.Hpf4C), Bits(pbi.Word64!.Value), Bits(pbi.Field54) };

    private static WwiseRtpc CurveOf(WwiseRtpcSetOracle.DCurve c)
    {
        var pts = new List<(float, float, uint)>();
        for (int i = 0; i < c.Pts.Length; i += 3) pts.Add((F(c.Pts[i]), F(c.Pts[i + 1]), c.Pts[i + 2]));
        return new WwiseRtpc(c.Rtpc, 0, 0, 0, 0, (byte)c.Scaling, pts.ToArray());
    }

    private static WwiseRoutingNode[] Holders(int n)
        => Enumerable.Range(0, n).Select(i => new WwiseRoutingNode { Id = (uint)(100 + i), SubscriptionKey10 = (uint)(0x1000 + 0x100 * i) }).ToArray();

    /// <summary>The set path's first write of the OLD value into the exact cell (no subscription exists yet, so nothing is delivered), then the subscriptions and the listeners, ready for the set of the new value.</summary>
    private static (WwiseRtpcStore Store, WwisePlayingInstance[] Pbis, List<uint> CurveLog) Prepare(WwiseRtpcSetOracle.DCase c)
    {
        var store = new WwiseRtpcStore { AllowSubscriptionOrderApproximation = true };          // the harness writes array A in the case's own order (emu_rtpcset.py), not the engine's sorted insert 0xA0F990; the C# uses insertion order
        store.SetRtpcs(c.Rtpc, F(c.Old), c.KeyA, c.KeyB, 0, 0, false);                       // 0xA1404C's immediate set that leaves the cell holding the old value
        var nodes = Holders(c.Nodes);
        var curveLog = new List<uint>();
        store.CurveA14E28 = (cv, x) => { curveLog.Add(Bits(x)); return WwiseRtpcCurveA14E28.Evaluate(cv, x); };   // the real port; the wrapper records x like the oracle's code hook on 0xA14E28
        var pbis = new List<WwisePlayingInstance>();
        foreach (var child in c.Children)
        {
            var pbi = NewPbi(child.A, child.B);
            SetFields(pbi, child.Init, child.InitE9);
            pbis.Add(pbi);
        }
        foreach (var s in c.Subs)
            store.AddSubscription(new WwiseRtpcSubscription
            {
                Key1 = nodes[s.Node].SubscriptionKey10, Param = s.Param, Type = s.Type, Accumulate = s.Accum, Curves = s.Curves.Select(CurveOf).ToArray(),
                // a type-0 subscription targets the context of child 0 and carries its scope key words (the oracle's [e] and [e+0xC..0x23])
                TargetPbi = s.Type == 0 ? pbis[0] : null,
                ScopeKey = s.Scope is { } k ? new WwiseRtpcScopeKey(k[0], k[1], k[2], (byte)k[3], (byte)k[4], k[5]) : WwiseRtpcScopeKey.Wild,
            });
        for (int i = 0; i < pbis.Count; i++)
            foreach (int n in c.Children[i].Nodes) store.AddChildA1973C(nodes[n].SubscriptionKey10, pbis[i], new WwiseGainRtpcKey(c.Children[i].A, c.Children[i].B), c.Children[i].Mask);   // 0xA1973C: the child at the holder with the oracle's mask (the walk 0x9F7390 is tested on its own)
        curveLog.Clear();                                                                    // the old-value write is not under test
        return (store, pbis.ToArray(), curveLog);
    }

    // ------------------------------------------------------------------ the delivery chain

    [Fact]
    public void C37_1_L7_03_to_L7_09_TheValueChangeDeliveryMatchesTheEngineThroughTheSetEntry()
    {
        // 0xA114D8 -> 0xA10C84 (type 2: curve(new) - curve(old) over the slots of the RTPC id, 0xA14E28 at the old then the new value, hint 0) -> 0x9868E4 / 0x9868B0 -> 0xA1B254 (keyed scan 0xA1A6A4 in both variants, the all-wild
        // loop 0xA1B308) -> the PBI's vt+8 = 0xA02EC0 -> 0xA02CE4 -> 0x9BDDE0 (parameters 0, 2, 3, 4, 5, 7). The live entry is the store's set (SetRtpcs, as SetParameterWithPlayingId / SetParameter call it).
        Assert.NotEmpty(WwiseRtpcSetOracle.Delivery);
        foreach (var c in WwiseRtpcSetOracle.Delivery)
        {
            var (store, pbis, curveLog) = Prepare(c);
            store.TransitionGateA1B5FC = (_, _, _) => true;                                 // unused: the cell exists (the old value is in it)
            store.SetRtpcs(c.Rtpc, F(c.New), c.KeyA, c.KeyB, 0, 0, false);                  // 0xA1404C -> 0xA13A88 -> 0xA137D8 -> 0xA12CA0 -> 0xA114D8
            AssertBits(c.CurveLog, curveLog.ToArray(), $"{c.Name} curve log");
            for (int i = 0; i < pbis.Length; i++)
            {
                AssertBits(c.Children[i].After, ReadFields(pbis[i]), $"{c.Name} child {i}");
                Assert.True(c.Children[i].AfterE9 == pbis[i].Flags0E9, $"{c.Name} child {i} dirty byte: engine {c.Children[i].AfterE9}, C# {pbis[i].Flags0E9}");
            }
            // parameter 0xD: 0x97E570(0) writes byte [0x108D7D8+0xC] = 0 and byte [0x108D7D8] = 1 (the oracle starts both at 0xEE: untouched stays 0xEE there, 0 here)
            Assert.Equal(c.Byte0 == 1 ? (byte)1 : (byte)0, store.Byte108D7D8);
            Assert.Equal((byte)0, store.Byte108D7D8_0C);
        }
    }

    [Fact]
    public void C37_1_L7_09_ThePbiContextsVt8MatchesTheEngineForEveryAdoptedParameter()
    {
        // 0xA02EC0 -> 0xA02CE4 -> 0x9BDDE0 called on its own: parameter 0 (+0x98 and the dirty bit), 2 (+0x44), 3 (+0x9C, +0x48), 4 (+0xA4, +0x4C), 5 (+0x64), 7 (+0x54), over special floats.
        Assert.NotEmpty(WwiseRtpcSetOracle.Receive);
        foreach (var r in WwiseRtpcSetOracle.Receive)
        {
            var pbi = NewPbi(7, 1);
            SetFields(pbi, r.Init, r.InitE9);
            WwisePlayPath.DeliverRtpcA02CE4(pbi, r.Param, F(r.Value), F(r.Delta));
            AssertBits(r.After, ReadFields(pbi), $"param {r.Param} value 0x{r.Value:X8} delta 0x{r.Delta:X8}");
            Assert.True(r.AfterE9 == pbi.Flags0E9, $"param {r.Param}: dirty byte engine {r.AfterE9}, C# {pbi.Flags0E9}");
        }
    }

    [Fact]
    public void C37_1_TheUnadoptedParameterIdsAreVisibleStops()
    {
        // 0xA02CE4's own ids 0x11..0x21 and the first-level rows C37.1 does not list (1, 6, 8.., 0x25..0x2D) and the 0x9BDFAC tables are not adopted: MISSING, never a no-op.
        foreach (uint id in new uint[] { 1, 6, 8, 0x10, 0x11, 0x15, 0x1D, 0x21, 0x22, 0x25, 0x2B, 0x2D, 0x2E, 0x44 })
            Assert.Throws<WwiseMissingBehaviourException>(() => WwisePlayPath.DeliverRtpcA02CE4(NewPbi(7, 1), id, 1f, 1f));
    }

    [Fact]
    public void C37_1_TheStepsTheInventoryDoesNotSettleAreVisibleStops()
    {
        var curve = new WwiseRtpc(7001, 0, 0, 0, 0, 2, new[] { (0f, -1f, 1u), (1f, 0f, 4u) });
        WwiseRtpcStore Store(uint type = 2, bool listener = true)
        {
            var s = new WwiseRtpcStore();
            var node = new WwiseRoutingNode { Id = 1, SubscriptionKey10 = 0x1000 };
            s.SetRtpcs(7001, 1f, 7, 0x100, 0, 0, false);
            s.AddSubscription(new WwiseRtpcSubscription { Key1 = 0x1000, Param = 0, Type = type, Accumulate = 1, Curves = new[] { curve } });
            if (listener) s.AddChildA1973C(0x1000, NewPbi(7, 0x100), new WwiseGainRtpcKey(7, 0x100), 0x3FE3FFFE67BDUL);
            return s;
        }
        // the equal-value return of 0xA137D8 (0xA13808..0xA13914) is not adopted
        Assert.Throws<WwiseMissingBehaviourException>(() => Store().SetRtpcs(7001, 1f, 7, 0x100, 0, 0, false));
        // the first set of a key reaches 0xA114D8 only through 0xA1B5FC, which is unread
        var first = new WwiseRtpcStore();
        first.AddSubscription(new WwiseRtpcSubscription { Key1 = 0x1000, Param = 0, Type = 2, Accumulate = 1, Curves = new[] { curve } });
        first.SetRtpcs(7001, 0.5f, 7, 0x100, 0, 0, false);                                   // no child to receive a delta: nothing observable depends on the gate
        first.AddChildA1973C(0x1000, NewPbi(7, 0x100), new WwiseGainRtpcKey(7, 0x100), 0x3FE3FFFE67BDUL);
        Assert.Throws<WwiseMissingBehaviourException>(() => first.SetRtpcs(7001, 0.5f, 7, 0x200, 0, 0, false));   // a first set of another playing id with a child present: 0xA1B5FC decides
        // a caller time, the bypass flag and the explicit-time byte are the type-3 message path 0x9AF158: not read
        Assert.Throws<WwiseMissingBehaviourException>(() => Store().SetRtpcs(7001, 0.5f, 7, 0x100, 0, 0, true));
        Assert.Throws<WwiseMissingBehaviourException>(() => Store().SetRtpcs(7001, 0.5f, 7, 0x100, 0, 0, false, explicitTime: true));
        // the subscriber types whose bodies are RECOVERABLE_GAP
        foreach (uint type in new uint[] { 1, 3, 4, 5, 6 })
            Assert.Throws<WwiseMissingBehaviourException>(() => Store(type).SetRtpcs(7001, 0.5f, 7, 0x100, 0, 0, false));
    }

    [Fact]
    public void C37_1_L7_04d_AType0SubscriptionEvaluatesItsCurvesAndStoresIntoNothingWithoutAContextObject()
    {
        // 0xA10CC8, 0xA10FF4..0xA110C8: the filter on the scope key, 0xA0E81C (the sum of the matching slots at the new value) and 0x9BD100, which stores only into the [ctx+0xD0] object. Without a context object the
        // engine's call returns with nothing stored; with one it is a visible stop; a type-0 subscription with no context at all is not modelled.
        var curve = new WwiseRtpc(7001, 0, 0, 0, 0, 0, new[] { (0f, 0f, 4u), (10f, 100f, 4u) });
        var target = NewPbi(7, 0x100);
        var log = new List<uint>();
        var s = new WwiseRtpcStore { CurveA14E28 = (c, x) => { log.Add(Bits(x)); return WwiseRtpcCurveA14E28.Evaluate(c, x); } };
        s.SetRtpcs(7001, 1f, 7, 0x100, 0, 0, false);
        s.AddSubscription(new WwiseRtpcSubscription { Key1 = 0x5000, Param = 0x1B, Type = 0, Accumulate = 1, Curves = new[] { curve }, TargetPbi = target, ScopeKey = new WwiseRtpcScopeKey(7, 0x100, 0, 0xFF, 0xFF, 0) });
        s.SetRtpcs(7001, 2f, 7, 0x100, 0, 0, false);
        Assert.Equal(new[] { Bits(2f) }, log);                                               // 0xA0E81C evaluates at the new value only
        log.Clear();
        s.SetRtpcs(7001, 3f, 8, 0x100, 0, 0, false);                                         // the key's object 8 differs from the subscription's 7: the filter returns (0xA10CC8..0xA10CE0)
        Assert.Empty(log);
        target.CtxD0 = new object();
        Assert.Throws<WwiseMissingBehaviourException>(() => s.SetRtpcs(7001, 4f, 7, 0x100, 0, 0, false));
        var orphan = new WwiseRtpcStore();
        orphan.AddSubscription(new WwiseRtpcSubscription { Key1 = 0x5000, Param = 0x1B, Type = 0, Accumulate = 1, Curves = new[] { curve } });
        Assert.Throws<WwiseMissingBehaviourException>(() => orphan.SetRtpcs(7001, 1f, 7, 0x100, 0, 0, false));
    }

    [Fact]
    public void C37_1_TheGroupFunctorOfAScopeWithChildrenIsAVisibleStop()
    {
        // 0xA13CF0: a set on a game object with no playing id while the element already has playing-id sub-elements builds G (0xA114D8's byte argument); 0xA1AD68 asks G->vt+0(G, child), whose body is unread.
        var curve = new WwiseRtpc(7001, 0, 0, 0, 0, 2, new[] { (0f, -1f, 1u), (1f, 0f, 4u) });
        var s = new WwiseRtpcStore();
        s.SetRtpcs(7001, 1f, 7, 0x100, 0, 0, false);                                         // a playing-id sub-element under game object 7
        s.SetRtpcs(7001, 1f, 7, 0, 0, 0, false);                                             // the game-object cell
        var node = new WwiseRoutingNode { Id = 1, SubscriptionKey10 = 0x1000 };
        s.AddSubscription(new WwiseRtpcSubscription { Key1 = 0x1000, Param = 0, Type = 2, Accumulate = 1, Curves = new[] { curve } });
        // no child: the engine's fan-out returns before it would ask G, so there is nothing to stop at
        s.SetRtpcs(7001, 0.5f, 7, 0, 0, 0, false);
        s.AddChildA1973C(0x1000, NewPbi(7, 0x100), new WwiseGainRtpcKey(7, 0x100), 0x3FE3FFFE67BDUL);
        Assert.Throws<WwiseMissingBehaviourException>(() => s.SetRtpcs(7001, 0.25f, 7, 0, 0, 0, false));
    }

    // ------------------------------------------------------------------ the registration at Init and the pre-pass, through the live entries

    private sealed class StubSource : IWwiseVoiceSource
    {
        public int Channels => 1;
        public int SampleRate => 48000;
        public int Render(WwiseVoiceBuffer buffer) => 0x2D;
        public int StartStream(uint arg1DC, uint arg1E0) => 1;
        public bool StartStreamSucceeded { get; set; } = true;
    }

    [Fact]
    public void C37_1_AnEventVolumeSetReachesTheVoiceGainThroughTheLiveEntries()
    {
        // The engine chain (emu_rtpcset.py chain cases): the event_volume set delivers the curve delta to the PBI keyed {7, 0x100} (0xA114D8 ... 0x9BDDE0), then the next voice pre-pass 0xA43D24 -> 0xA55750 runs
        // vt+0x28 = 0x9FF414 -> 0x9FF368 (E9 bit 0 set) and the gain store of 0xA4B93C writes voice+0x1C. Here every step is entered through the C# live entry: WwisePlaybackLimiter.InsertPbiA0285C (the PBI Init: 0x9BC5A8 ->
        // 0xA19ECC registers the listener on the node and its parent), WwiseRtpcStore.SetParameterWithPlayingId (SetCozmoEventParameter) and WwiseVoiceBusPass.VoicePass (the pre-pass inside the voice pass).
        Assert.NotEmpty(WwiseRtpcSetOracle.Chain);
        foreach (var chain in WwiseRtpcSetOracle.Chain)
        {
            var c = chain.Delivery;
            var mixer = new WwiseRoutingNode { Id = 62050212, SubscriptionKey10 = 0x2010, SubscriptionMask14 = 1UL, Node40 = 0 };                 // the ActorMixer the shipped event_volume subscription sits on (C37.5)
            var soundRt = new WwiseRoutingNode { Id = 12345, SubscriptionKey10 = 0x1010, Parent = mixer, Node40 = 0 };
            var soundNode = new WwiseSoundNode(12345, "t.bnk", new WwiseNodeParams(0, 0, 0, new Dictionary<byte, uint>(), new Dictionary<byte, (float, float)>(), Array.Empty<WwiseRtpc>(),
                Array.Empty<(uint, byte, IReadOnlyList<(uint, uint)>)>()), WwiseSourceFactory.VorbisPlugin, 1, 12345, 0, 0);
            var store = new WwiseRtpcStore();
            store.Apply(new WwiseStmgParam(c.Rtpc, F(c.Old), 0, 0f, 0f, false));              // the STMG default of event_volume is the old value of the first set
            store.RegisterPlayingId(c.KeyB, c.KeyA);
            store.AddSubscription(new WwiseRtpcSubscription { Key1 = mixer.SubscriptionKey10, Param = c.Subs[0].Param, Type = c.Subs[0].Type, Accumulate = c.Subs[0].Accum, Curves = c.Subs[0].Curves.Select(CurveOf).ToArray() });
            var limiter = new WwisePlaybackLimiter(_ => soundNode)
            {
                RtpcListeners = store,
                RuntimeNodeOf = n => n.Id == 12345 ? soundRt : null,
                RegisterPlayingIdA04D48 = _ => { },
                MediaTable = new WwiseMediaTable(new WwiseBankMemory()),
            };
            var pbi = new WwisePlayingInstance(new WwisePlayInitParams { PlayingId = c.KeyB, TargetNodeId = 12345, GameObjectId = c.KeyA }, 12345, WwiseSourceDescriptor.FromSound(soundNode), new byte[0x44],
                new WwiseGainRtpcKey(c.KeyA, c.KeyB), continuous: false) { NodeE0 = soundNode };
            Assert.Equal(1, limiter.InsertPbiA0285C(soundNode, pbi));                          // the PBI Init
            Assert.Equal(1, store.ListenerCount(mixer.SubscriptionKey10));                     // registered on the parent's holder
            Assert.Equal(0, store.ListenerCount(soundRt.SubscriptionKey10));                   // the sound has no registry (0x9F7E4C): nothing to register at
            // the PBI as CalcEffectiveParams left it (bit 5 of E8 set, E9 bit 0 clear), with the oracle's fields
            SetFields(pbi, c.Children[0].Init, c.Children[0].InitE9);
            pbi.Flags0E8 = chain.E8;
            pbi.Volume3C = F(chain.Pre[0]); pbi.MuteFade40 = F(chain.Pre[1]);
            pbi.Ranges118.Volume = F(chain.Pre[2]); pbi.Fade168 = F(chain.Pre[3]); pbi.Fade16C = F(chain.Pre[4]);

            store.TransitionGateA1B5FC = (_, _, _) => true;                                    // TEST DOUBLE: 0xA1B5FC is unread; the first set of the playing id has no cell
            store.SetParameterWithPlayingId(c.Rtpc, F(c.New), c.KeyB);                          // SetCozmoEventParameter: time 0, curve 0
            AssertBits(chain.Delivered, ReadFields(pbi), $"{c.Name} after the set");
            Assert.Equal(chain.DeliveredE9, pbi.Flags0E9);

            var voice = new WwiseLiveVoice(1, 8) { State = 1, Source = new StubSource(), BusOwner8 = pbi };
            var pass = new WwiseVoiceBusPass(new WwiseMixBusHierarchy(), new WwiseOutputDeviceState())
            {
                SourceOwner = _ => pbi,
                AdvanceTickCounters = () => { },
                NodeCleanup = () => { },
                DuckPrePassTailA43D6C = () => { },                                               // TEST DOUBLE: 0xA43D6C..0xA43EFC is not adopted
                VoiceRefreshTailA4B9BC = _ => { },                                               // TEST DOUBLE: 0xA4B9BC.. is not adopted
            };
            pass.Voices.Add(voice);
            pass.VoicePass();                                                                    // 0xA44948: 0x9D3CC0, 0xA43D24 -> 0xA55750, 0xA39564, then the voice walk (no connection: nothing renders)
            Assert.True(chain.F98 == Bits(pbi.Field98), $"{c.Name} +0x98: engine 0x{chain.F98:X8}, C# 0x{Bits(pbi.Field98):X8}");
            Assert.True(chain.Vol == Bits(pbi.Volume3C), $"{c.Name} +0x3C: engine 0x{chain.Vol:X8}, C# 0x{Bits(pbi.Volume3C):X8}");
            Assert.True(chain.Mute == Bits(pbi.MuteFade40), $"{c.Name} +0x40: engine 0x{chain.Mute:X8}, C# 0x{Bits(pbi.MuteFade40):X8}");
            Assert.Equal(chain.AfterE9, pbi.Flags0E9);
            Assert.True(chain.Gain == Bits(voice.OutputGain), $"{c.Name} voice+0x1C: engine 0x{chain.Gain:X8}, C# 0x{Bits(voice.OutputGain):X8}");
        }
    }

    [Fact]
    public void C37_1_L7_10_ThePrePassStepMatchesTheEngine()
    {
        // 0xA55750(voice) with the real 0x9FF368 and the real 0xA4B93C gain store: E8 bit 5 clear -> the context's vt+0x24 (the oracle and the C# both script it); set with E9 bit 0 -> vt+0x28 -> 0x9FF368; then the gain
        // dBToLin(+0x3C) * +0x40 into voice+0x1C. The calls the oracle stubs (0x9BE28C, 0x9BDA88) are the C#'s required tail seam.
        Assert.NotEmpty(WwiseRtpcSetOracle.PrePass);
        foreach (var c in WwiseRtpcSetOracle.PrePass)
        {
            var pbi = NewPbi(7, 0x100);
            pbi.Flags0E8 = c.E8; pbi.Flags0E9 = c.E9; pbi.Flags1BE = c.B1be;
            pbi.Volume3C = F(c.Pbi[0]); pbi.MuteFade40 = F(c.Pbi[1]); pbi.Field98 = F(c.Pbi[2]); pbi.Ranges118.Volume = F(c.Pbi[3]); pbi.Fade168 = F(c.Pbi[4]); pbi.Fade16C = F(c.Pbi[5]);
            var events = new List<string>();
            var pass = new WwiseVoiceBusPass(new WwiseMixBusHierarchy(), new WwiseOutputDeviceState())
            {
                SourceOwner = _ => pbi,
                CalcEffectiveParamsVt24 = p => { events.Add("calc"); p.Volume3C = F(c.Calc[0]); p.MuteFade40 = F(c.Calc[1]); },     // the oracle's stub of vt+0x24 leaves the same scripted values
                VoiceRefreshTailA4B9BC = _ => events.Add("9BE28C,9BDA88"),
            };
            var voice = new WwiseLiveVoice(1, 8) { State = 1, Source = new StubSource(), BusOwner8 = pbi };
            pass.Voices.Add(voice);
            pass.PrePassVoicesA43D24();
            Assert.True(c.Gain == Bits(voice.OutputGain), $"{c.Name} voice+0x1C: engine 0x{c.Gain:X8}, C# 0x{Bits(voice.OutputGain):X8}");
            Assert.True(c.Vol == Bits(pbi.Volume3C), $"{c.Name} +0x3C: engine 0x{c.Vol:X8}, C# 0x{Bits(pbi.Volume3C):X8}");
            AssertBits(new[] { c.Mute }, new[] { Bits(pbi.MuteFade40) }, $"{c.Name} +0x40");
            Assert.Equal(c.AfterE9, pbi.Flags0E9);
            Assert.Equal(c.Events, string.Join(",", events));
        }
    }

    [Fact]
    public void C37_1_L7_10_ThePrePassOnlyVisitsStateOneVoicesAndStopsAtTheUnadoptedBranches()
    {
        // 0xA43D24..0xA43D6C visits a voice only with [voice+0xDC] == 1; [pbi+0x1BE] & 0x14 takes 0xA55790 (0xA0275C, the stop path), which is not adopted; the tail 0xA43D6C.. and the gain tail are required.
        var pbi = NewPbi(7, 1);
        pbi.Flags0E8 = 0x7D;
        var calls = 0;
        var pass = new WwiseVoiceBusPass(new WwiseMixBusHierarchy(), new WwiseOutputDeviceState()) { SourceOwner = _ => { calls++; return pbi; }, VoiceRefreshTailA4B9BC = _ => { } };
        var stopped = new WwiseLiveVoice(1, 8) { State = 2, Source = new StubSource(), BusOwner8 = pbi };
        var fresh = new WwiseLiveVoice(1, 8) { State = 0, Source = new StubSource(), BusOwner8 = pbi };
        pass.Voices.Add(stopped); pass.Voices.Add(fresh);
        pass.PrePassVoicesA43D24();
        Assert.Equal(0, calls);                                                              // neither voice has state 1
        var live = new WwiseLiveVoice(1, 8) { State = 1, Source = new StubSource(), BusOwner8 = pbi };
        pass.Voices.Add(live);
        pbi.Flags1BE = 0x04;
        Assert.Throws<WwiseMissingBehaviourException>(() => pass.PrePassVoicesA43D24());
        pbi.Flags1BE = 0;
        pbi.Flags0E8 = 0x5D;                                                                 // bit 5 clear: vt+0x24 is a required seam
        Assert.Throws<WwiseMissingBehaviourException>(() => pass.PrePassVoicesA43D24());
        pbi.Flags0E8 = 0x7D;
        pass.VoiceRefreshTailA4B9BC = null;
        Assert.Throws<WwiseMissingBehaviourException>(() => pass.PrePassVoicesA43D24());     // the rest of 0xA4B93C is required
        // the whole pre-pass without its tail is a visible stop, with the voice walk done first
        pass.VoiceRefreshTailA4B9BC = _ => { };
        pass.AdvanceTickCounters = () => { };
        pass.NodeCleanup = () => { };
        Assert.Throws<WwiseMissingBehaviourException>(() => pass.VoicePass());
    }
}
