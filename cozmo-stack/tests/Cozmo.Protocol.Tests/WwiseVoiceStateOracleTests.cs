// fidelity: M6-022
using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M6-022 C41.1..C41.3, rows 1.1..1.19 and the verification's corrections A1, D1..D5: the voice state machine V7 (<c>0xA54F1C</c>) on the owner PBI, against the engine's own code. Every expected value is the engine's output
/// (<see cref="WwiseVoiceStateOracle"/>, written by <c>re-analysis/tools/emu/emu_v7.py</c>: the real V7 with the real <c>0xA4C584</c>, <c>0xA0228C</c> / <c>0xA022E8</c>, <c>0xA56650</c> and the PBI <c>vt+0x3C</c> / <c>vt+0x28</c> slots under Unicorn; the callees
/// <c>0xA4BC58</c>, <c>0xA4B4B0</c>, <c>0xA54A30</c>, <c>0x99CC40</c>, <c>0x9FFAD4</c> and the voice, holder and source vtable slots are logging stand-ins that return what the case says), never the C#'s.
/// The test runs <see cref="WwiseVoiceBusPass.RunVoiceStateMachine"/> through the same stand-ins and compares the return, the pass block <c>S</c>, the voice, the PBI, the connections' flags, the PBI's list counters and the ordered call log.
/// </summary>
public sealed class WwiseVoiceStateOracleTests
{
    private static uint U(string hex) => Convert.ToUInt32(hex, 16);

    private static float F(string hex) => BitConverter.UInt32BitsToSingle(U(hex));

    private static string H(float f) => BitConverter.SingleToUInt32Bits(f).ToString("x");

    private sealed class OracleSource : IWwiseVoiceSource
    {
        private readonly Action<string> _log;
        private readonly int _start;
        public OracleSource(Action<string> log, int start, (float, float)? gain8) { _log = log; _start = start; Gain8 = gain8; }
        public int Channels => 1;
        public int SampleRate => 48000;
        public int Render(WwiseVoiceBuffer buffer) => 0x2D;
        public bool StartStreamSucceeded { get; set; }
        public (float At0, float At4)? Gain8 { get; }
        public int StartStream(uint arg1DC, uint arg1E0) { _log($"R_{arg1DC:x}_{arg1E0:x}"); return _start; }
    }

    private static Dictionary<string, string> Fields(string input)
    {
        var d = new Dictionary<string, string>();
        foreach (var part in input.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            int eq = part.IndexOf('=');
            d[part[..eq]] = part[(eq + 1)..];
        }
        return d;
    }

    private static string RunCase(string input)
    {
        var f = Fields(input);
        var ev = new List<string>();
        void Log(string s) => ev.Add(s);

        // The PBI (the owner [[voice+0xD4]+0xC]).
        int cls = int.Parse(f["cls"]);
        var pbi = new WwisePlayingInstance(new WwisePlayInitParams { PlayingId = U(f["f140"]), TargetNodeId = 1 }, 1, new object(), new byte[0x44], null, continuous: false)
        {
            Field1F8 = U(f["f1f8"]),
            Field54 = F(f["f54"]),
            Byte58 = (byte)U(f["b58"]),
            Field68 = F(f["f68"]),
            Field6C = F(f["f6c"]),
            Ratio = F(f["f164"]),
            Flags0E9 = (byte)U(f["pe9"]),
            Flags1BE = (byte)U(f["b1be"]),
            StartOffset = U(f["f1d8"]),
            Word1DC = U(f["f1dc"]),
            Word1E0 = U(f["f1e0"]),
            Flags4 = U(f["f4"]),
            FieldC4 = F(f["c4"]),
            Fade168 = F(f["f168"]),
            Fade16C = F(f["f16c"]),
            Field98 = F(f["f98"]),
            Flags0E8 = (byte)U(f["e8"]),
            Volume3C = 0f,
            MuteFade40 = 0f,
            Flags1BD = 0,
            Word1B4 = 0,
        };
        pbi.Ranges118.MakeUpGain = F(f["f11c"]);
        pbi.Ranges118.Volume = F(f["f118"]);
        var x = f["x"].Split(':');
        pbi.PbiClass = cls switch { 0 => WwisePbiClass.Base, 1 => WwisePbiClass.Container9883AC, _ => WwisePbiClass.Class103D3B0 };
        if (cls == 1) pbi.Seam99CC40 = _ => { Log("X"); return ((int)U(x[0]), U(x[1]), U(x[2])); };
        var lists = f["lists"] == "-" ? Array.Empty<string>() : f["lists"].Split('.');
        foreach (var l in lists) pbi.LimiterArray1EC.Items.Add(new WwisePbiList { Virtual22 = (ushort)U(l) });

        // The source and the voice.
        (float, float)? gp = null;
        if (f["gp"] != "-") { var g = f["gp"].Split(':'); gp = (F(g[0]), F(g[1])); }
        var src = new OracleSource(Log, (int)U(f["src28"]), gp) { StartStreamSucceeded = f["src10"] == "1" };
        var voice = new WwiseLiveVoice(1, 8)
        {
            Source = src,
            E0 = int.Parse(f["e0"]),
            E4 = int.Parse(f["e4"]),
            FlagsCD = (byte)U(f["cd"]),
            FlagE8 = (U(f["ve8"]) & 1) != 0,
            Word0xF0 = U(f["f0"]),
            BusOwner8 = pbi,
        };
        if (f["h1b4"] == "1") voice.PitchNode.Pbi = pbi;
        voice.PitchNode.Out.Data = new float[1];                                     // a block the pitch node's release (the holder's vt+0xC) frees
        var bus = new WwiseMixBus(default, Array.Empty<WwiseBusFxSlot>(), 8);
        var flags = f["conn"] == "-" ? Array.Empty<string>() : f["conn"].Split('.');
        foreach (var cf in flags) voice.Connections.Add(new WwiseVoiceConnection(bus, 1, 1) { Flags6C = (byte)U(cf) });
        var ramps = f["ramps"].Split('.');
        WwiseVoiceFilterBand[] rm = { voice.Ramp340, voice.Ramp510, voice.Ramp350, voice.Ramp520 };      // the 16-byte records at voice+0x340 / 0x510 / 0x350 / 0x520 are the filter bands (C43.1)
        for (int i = 0; i < 4; i++)
        {
            var r = ramps[i].Split(':');
            rm[i].Current = F(r[0]); rm[i].Target = F(r[1]); rm[i].Steps = (ushort)U(r[2]); rm[i].Dirty = (byte)U(r[3]);
        }
        var sf = f["S"].Split(':');
        var state = voice.Buffer.State;
        state.MaxFrames = (ushort)U(sf[0]); state.ChannelConfig = U(sf[1]); state.Code28 = (int)U(sf[2]);
        voice.Buffer.HasBusParam = sf[3] == "1";

        // The stand-ins.
        var gains = f["gains"].Split('.');
        int gcalls = 0;
        int h10 = (int)U(f["h10"]), h18 = (int)U(f["h18"]);
        var limiter = new WwisePlaybackLimiter(_ => null);
        var pass = new WwiseVoiceBusPass(new WwiseMixBusHierarchy(), new WwiseOutputDeviceState())
        {
            SourceOwner = _ => pbi,
            Limiter = limiter,
            CalcEffectiveParamsVt24 = _ => Log("C"),
            DuckingOverrideA4B4B0 = _ => Log("D"),
            ConnectionGainsOverrideA4BC58 = (v, p, gain, arg5, arg12, outs) =>
            {
                var gc = gains[Math.Min(gcalls, gains.Length - 1)].Split(':');
                gcalls++;
                bool shared = !ReferenceEquals(outs, v.OutputMin50);
                string a12 = arg12 is { } a ? $"{a.PlayingId:x}.{a.Word:x}" : "-";
                Log($"G_c_{v.Word0xF0:x}_{H(gain)}_{arg5:x}_{a12}_{(shared ? 1 : 0)}");
                v.Run2E = gc[0] == "1";
                for (int k = 0; k < 4; k++) outs[k] = F(gc[2 + k]);
                v.P2F = gc[1] == "1";
                return v.P2F;
            },
            StartStreamOverrideA54A30 = v =>
            {
                Log("B");
                if (f["nf0"] != "-") v.Word0xF0 = U(f["nf0"]);
                return (int)U(f["a54a30"]);
            },
        };
        voice.VoiceStop48 = () => Log("S");
        voice.VoiceVt58A53698 = () => Log("V");
        voice.PitchNodeVt10 = s => { Log($"H10_{s:x}"); return h10; };
        voice.PitchNodeVt14 = _ => { };
        voice.FilterAVt14A7666C = r1 => Log($"H14_{r1:x}");
        voice.PitchNodeVt18 = (a, b) => { Log($"H18_{a:x}_{b:x}"); return h18; };
        voice.StartStreamFormatWriter = (_, _) => { };                               // the engine's stand-in for the source's vt+0x28 writes no format bytes

        uint g0 = limiter.GlobalVirtualCount;
        bool ret = pass.RunVoiceStateMachine(voice);

        var o = new List<string>();
        o.Add($"r={(ret ? 1 : 0)}");
        o.Add($"S={state.ChannelConfig:x}.{(uint)state.Code28:x}.{(voice.Buffer.HasBusParam ? 1 : 0)}");
        o.Add($"V={voice.FlagsCD:x}.{(voice.FlagE8 ? 1 : 0)}.{voice.Word0xF0:x}.{(voice.PitchNode.Pbi is null ? 0 : 1)}");
        o.Add("Rm=" + string.Join('.', rm.Select(r => $"{H(r.Current)}:{H(r.Target)}:{r.Steps:x}:{r.Dirty:x}")));
        o.Add($"P={pbi.StartOffset:x}.{H(pbi.FieldC4)}.{pbi.Flags1BE:x}.{pbi.Flags0E9:x}.{H(pbi.Volume3C)}.{H(pbi.MuteFade40)}.{pbi.Flags0E8:x}");
        o.Add($"Q={pbi.Word1B4:x}.{pbi.Flags1BD:x}.{(src.StartStreamSucceeded ? 1 : 0)}");
        o.Add("C=" + (voice.Connections.Count == 0 ? "-" : string.Join('.', voice.Connections.Select(c => c.Flags6C.ToString("x")))));
        o.Add("L=" + (pbi.LimiterArray1EC.Items.Count == 0 ? "-" : string.Join('.', pbi.LimiterArray1EC.Items.Select(l => l.Virtual22.ToString("x")))) + "." + (limiter.GlobalVirtualCount - g0).ToString("x"));
        o.Add("E=" + (ev.Count == 0 ? "-" : string.Join(',', ev)));
        o.Add($"Z={(voice.PitchNode.Out.Data is null ? 1 : 0)}");
        return string.Join(' ', o);
    }

    [Fact]
    public void M6_022_V7_on_the_owner_PBI_matches_the_engines_own_state_machine_on_every_oracle_run()
    {
        // Citation: C41.1 rows 1.1..1.19, C41.2 (the budget [pbi+0x1D8], 0xA55750 before V7), C41.3 (pbi vt+0x3C per class), verification A1, D1..D5; the engine's own 0xA54F1C under Unicorn (emu_v7.py).
        int n = 0;
        foreach (var line in WwiseVoiceStateOracle.Lines)
        {
            if (line.Length == 0) continue;
            int arrow = line.IndexOf(" => ", StringComparison.Ordinal);
            string input = line[2..arrow];
            string expected = line[(arrow + 4)..];
            Assert.True(expected == RunCase(input), $"run {n}\n input    {input}\n engine   {expected}\n managed  {RunCase(input)}");
            n++;
        }
        Assert.True(n >= 5000, $"{n} runs");
    }
}
