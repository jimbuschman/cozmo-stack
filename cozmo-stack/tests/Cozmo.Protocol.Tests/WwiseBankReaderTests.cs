using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M6-025 K17, K18, K19 (C34.4): the bank reader <c>R = BM+4</c> (0x9BBB04, 0x9BBB5C, 0x9BBB90, 0x9BBBA4, 0x9BBBE0, 0x9BBC14, 0x9BBE5C, 0x9BBF64, 0x9BBF9C), in memory mode and in stream mode. The expected result of every step in
/// <see cref="BankReaderOracle"/> is the engine's own output from re-analysis/tools/emu/emu_reader.py (the real functions under Unicorn, a Python stand-in for the stream object's vtable); none comes from running this implementation.
/// </summary>
public class WwiseBankReaderTests
{
    public static IEnumerable<object[]> Names() => BankReaderOracle.Scenarios.Keys.Select(k => new object[] { k });

    private sealed class StubStream : IWwiseReaderStream
    {
        private readonly ReaderConfig _cfg;
        private readonly byte[] _data;
        private int _pos, _reads, _waits, _skips;
        public readonly List<string> Log = new();

        public StubStream(ReaderConfig cfg)
        {
            _cfg = cfg;
            _data = new byte[cfg.StreamLength];
            for (int i = 0; i < _data.Length; i++) _data[i] = (byte)((i * 7 + 3) & 0xFF);
        }

        public int ReadVt1C(Span<byte> dst, uint bytes, float deadline, sbyte priority, out uint actual)
        {
            _reads++;
            Log.Add($"r1C:{bytes}:{BitConverter.SingleToUInt32Bits(deadline):x8}:{priority}:f1");
            actual = 0;
            if (_cfg.ReadFail == _reads) return 9;
            int n = Math.Min((int)bytes, _data.Length - _pos);
            if (_cfg.ReadCap >= 0) n = Math.Min(n, _cfg.ReadCap);
            _data.AsSpan(_pos, n).CopyTo(dst);
            _pos += n;
            actual = (uint)n;
            return 1;
        }

        public int WaitVt34()
        {
            _waits++;
            Log.Add("wait");
            return _cfg.WaitFail == _waits ? 5 : 1;
        }

        public int SkipVt28(uint bytes, out uint actual)
        {
            _skips++;
            Log.Add("s28:" + bytes);
            actual = 0;
            if (_cfg.SkipFail == _skips) return 9;
            int n = Math.Min((int)bytes, _data.Length - _pos);
            if (_cfg.SkipCap >= 0) n = Math.Min(n, _cfg.SkipCap);
            _pos += n;
            actual = (uint)n;
            return 1;
        }

        public byte StatusVt24() { Log.Add("st"); return (byte)_cfg.Status; }

        public void ReleaseVt8() => Log.Add("rel");
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void TheReaderGivesTheEnginesResultsAndStreamCalls(string name)
    {
        var (cfg, ops, steps) = BankReaderOracle.Scenarios[name];
        int allocCalls = 0;
        var memory = new WwiseBankMemory { AllocationFails = () => ++allocCalls == cfg.AllocFail };
        var reader = new WwiseBankReader(memory);
        StubStream? stream = null;
        byte[]? window = null;
        if (cfg.Memory is not null)
        {
            var m = Convert.FromHexString(cfg.Memory);
            reader.SetupMemory9BBB90(m, 0, (uint)m.Length);
        }
        else
        {
            window = new byte[0x1000];
            reader.SetWindow(window);
            reader.MinDirectC = cfg.MinDirect;
            reader.BlockSize10 = cfg.Block;
            reader.Throughput20 = cfg.Throughput;
            reader.Priority24 = unchecked((sbyte)cfg.Priority);
            stream = new StubStream(cfg);
            reader.Stream = stream;
            reader.Reset9BBB04();
        }
        for (int i = 0; i < ops.Length; i++)
        {
            allocCalls = 0;
            stream?.Log.Clear();
            int freesBefore = memory.FreeCalls;
            var parts = ops[i].Split(':');
            int n = parts.Length > 1 && int.TryParse(parts[1], out var parsed) ? parsed : 0;
            string result = "";
            var extraLog = new List<string>();
            switch (parts[0])
            {
                case "read":
                {
                    var dst = new byte[n + 4];
                    Array.Fill(dst, (byte)0xEE);
                    int r = reader.Read9BBC14(dst.AsSpan(0, n), out int got);
                    result = $"r={r} got={got} data={Convert.ToHexString(dst, 0, got).ToLowerInvariant()}";
                    break;
                }
                case "raw":
                {
                    var dst = new byte[n + 4];
                    Array.Fill(dst, (byte)0xEE);
                    int r = reader.ReadRaw9BBF64(dst.AsSpan(0, n));
                    result = $"r={r} data={Convert.ToHexString(dst, 0, n).ToLowerInvariant()}";
                    break;
                }
                case "skip":
                {
                    int r = reader.Skip9BBF9C((uint)n, out uint skipped);
                    result = $"r={r} skipped={skipped}";
                    break;
                }
                case "window":
                {
                    uint leftBefore = reader.Left8;
                    var p = reader.Window9BBE5C((uint)n);
                    string where;
                    if (p.IsNull) where = "null";
                    else if (cfg.Memory is not null) where = "mem+" + p.Index;
                    else if (ReferenceEquals(p.Array, window)) where = "win+" + p.Index;
                    else where = "tmp+" + p.Index;
                    int take = cfg.Memory is not null ? (int)Math.Min((uint)n, leftBefore) : n;
                    string data = !p.IsNull && take > 0 && take <= 64 ? Convert.ToHexString(p.Array!, p.Index, take).ToLowerInvariant() : "";
                    result = $"p={where} data={data}";
                    break;
                }
                case "freetemp": reader.FreeTemp9BBBE0(); break;
                case "release": result = $"r={reader.Release9BBBA4()}"; break;
                case "reset": reader.Reset9BBB04(); break;
                case "heur":
                {
                    float f = float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
                    int r = reader.SetHeuristics9BBB5C(f, byte.Parse(parts[2]));
                    result = $"r={r} thr={BitConverter.SingleToUInt32Bits(reader.Throughput20):x8} prio={(byte)reader.Priority24}";
                    break;
                }
                default: throw new InvalidOperationException(ops[i]);
            }
            var log = new List<string>();
            for (int k = 0; k < allocCalls; k++) log.Add("alloc");
            if (stream is not null) log.AddRange(stream.Log);
            for (int k = freesBefore; k < memory.FreeCalls; k++) log.Add("free");
            string state = $"left={reader.Left8} cur={(cfg.Memory is null ? reader.WindowCursor4 : 0)} mem={reader.MemoryIndex18} stream={(reader.Stream is not null ? 1 : 0)} temp={(reader.Temp14 != 0 ? 1 : 0)}";
            Assert.Equal(steps[i], $"{result} | {string.Join(' ', Ordered(log))} | {state}");
        }
    }

    // the engine's order is alloc, then the stream calls, then free; the harness collects them in that order
    private static IEnumerable<string> Ordered(List<string> log) => log;
}
