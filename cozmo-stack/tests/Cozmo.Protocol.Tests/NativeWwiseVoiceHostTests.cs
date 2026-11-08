using System.Buffers.Binary;
using System.Security.Cryptography;
using Cozmo.Robot.Emulation;
using Cozmo.Robot.Animation;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class NativeWwiseVoiceHostTests
{
    [Fact]
    public void InvalidOptionalMusicBankFailsTheHostClearly()
    {
        string? lib = ShippedLibraries.FindLibDir();
        if (lib is null) return;
        string meta = GraphRig.FindMeta() ?? throw new InvalidOperationException("shipped bank metadata missing");
        string archive = Path.Combine(WwiseAssets.SoundDir ?? throw new InvalidOperationException("audio archive missing"), "AudioAssets.zip");
        Assert.Throws<EmulationAbort>(() => new NativeWwiseVoiceHost(lib, GraphRig.ReadBank(meta, "Init.bnk"),
            GraphRig.ReadBank(meta, "English(US)/Cozmo.bnk"), 1024, archive, 1700000000, new byte[] { 1 }));
    }

    [Fact]
    public void SharedMusicAndOrdinaryAudioMatchIndependentNativeMux()
    {
        string? lib = ShippedLibraries.FindLibDir();
        if (lib is null) return;
        string meta = GraphRig.FindMeta() ?? throw new InvalidOperationException("shipped bank metadata missing");
        string archive = Path.Combine(WwiseAssets.SoundDir ?? throw new InvalidOperationException("audio archive missing"), "AudioAssets.zip");
        byte[] music = CannedSpeechOwner.ReadMusicBank(archive) ?? throw new InvalidOperationException("Music.bnk missing");
        using var host = new NativeWwiseVoiceHost(lib, GraphRig.ReadBank(meta, "Init.bnk"),
            GraphRig.ReadBank(meta, "English(US)/Cozmo.bnk"), 1024, archive, 1700000000, music, inspectOutputBuffers: true);
        var platform = new List<byte[]>();
        var frames = new List<float[]>();
        var ends = new List<uint>();
        host.SharedEventEnded += (cookie, _, _) => ends.Add(cookie);
        var schedule = new (uint Time, uint Event, uint Cookie)[]
        { (1500000, 0xD43D2A47, 1), (2919000, 0x9EF22ABC, 2), (5130000, 0xAE983923, 3) };
        uint clock = 0;
        int next = 0;
        for (int tick = 0; tick < 350; tick++)
        {
            uint frame = (uint)((long)(tick + 1) * 1024 * 1000000 / 48000);
            host.AdvanceClock(frame - clock); clock = frame;
            if (tick == 15)
            {
                host.RequestMusicState(0x99EA6572, false, 0);
                host.UpdateMusic(frame / 1000);
            }
            if (tick == 230) host.StopMusic();
            while (next < schedule.Length && schedule[next].Time <= frame)
            {
                var entry = schedule[next++];
                uint playing = host.PostShared(entry.Event, entry.Cookie);
                Assert.NotEqual(0u, playing);
                host.SetEventVolume(playing, 1);
            }
            byte[]? block = host.Step(0, true);
            if (tick >= 15 && tick < 230 && block is not null) platform.Add(block);
            frames.AddRange(host.TakeFrames());
        }
        Assert.Equal(new uint[] { 1, 2, 3 }, ends);
        Assert.Equal(26, frames.Count);
        Assert.Equal("3cc4b1d0d0b170699e9544eb207e7c31aea16c19be3a0a23bbdce95a1624e864", Hash(frames));
        Assert.Equal("b562161320552d1f8293cf304b7d5488a81476b219d6f321d2e513020a7fa387",
            Convert.ToHexString(SHA256.HashData(platform.SelectMany(x => x).ToArray())).ToLowerInvariant());
        host.Dispose();
        Assert.True(host.TerminatedCleanly, host.ShutdownDiagnostic);
    }

    [Fact]
    public void SharedMusicSubmittedBuffersStayUnchangedUntilCompletion()
    {
        string? lib = ShippedLibraries.FindLibDir();
        if (lib is null) return;
        string meta = GraphRig.FindMeta() ?? throw new InvalidOperationException("shipped bank metadata missing");
        string archive = Path.Combine(WwiseAssets.SoundDir ?? throw new InvalidOperationException("audio archive missing"), "AudioAssets.zip");
        byte[] music = CannedSpeechOwner.ReadMusicBank(archive) ?? throw new InvalidOperationException("Music.bnk missing");
        using var host = new NativeWwiseVoiceHost(lib, GraphRig.ReadBank(meta, "Init.bnk"),
            GraphRig.ReadBank(meta, "English(US)/Cozmo.bnk"), 1024, archive, 1700000000, music, inspectOutputBuffers: true);
        var submitted = new Queue<NativeOpenSlBridge.OutputBlock>();
        void Submit()
        {
            while (host.PeekOutputBlock() is { } block)
            {
                submitted.Enqueue(block);
                host.MarkOutputSubmitted(block.Token);
            }
        }
        Submit();
        uint clock = 0;
        for (int tick = 0; tick < 300; tick++)
        {
            uint frame = (uint)((long)(tick + 1) * 1024 * 1000000 / 48000);
            host.AdvanceClock(frame - clock); clock = frame;
            if (tick == 15)
            {
                host.RequestMusicState(0x99EA6572, false, 0);
                host.UpdateMusic(frame / 1000);
                Submit();
            }
            if (tick == 230) { host.StopMusic(); Submit(); }
            var block = submitted.Dequeue();
            host.AcknowledgeOutput(block.Token);
            host.ServiceOutput(0);
            Submit();
        }
        Assert.Equal(0, host.OutputBufferChanges.After);
        Assert.Equal(232, host.OutputBufferChanges.Before);
        host.Dispose();
        Assert.True(host.TerminatedCleanly, host.ShutdownDiagnostic);
    }

    [Fact]
    public void OrdinaryMeetEventsMatchIndependentNativeArchiveReference()
    {
        string? lib=ShippedLibraries.FindLibDir();
        if(lib is null)return;
        string meta=GraphRig.FindMeta() ?? throw new InvalidOperationException("shipped bank metadata missing");
        string archive=Path.Combine(WwiseAssets.SoundDir ?? throw new InvalidOperationException("audio archive missing"),"AudioAssets.zip");
        using var host=new NativeWwiseVoiceHost(lib,GraphRig.ReadBank(meta,"Init.bnk"),
            GraphRig.ReadBank(meta,"English(US)/Cozmo.bnk"),1024,archive,1700000000);
        var schedule=new (uint Time,uint Event,uint Cookie,float Volume)[]
        {
            (0,0x656E441C,1,1),(363000,0x4C49B98F,2,1),(594000,0xF143D426,3,1),(1287000,0x9EF22ABC,4,1),
            (3000000,0x9EF22ABC,17,1),(5211000,0xAE983923,18,.7f),
            (9000000,0xD43D2A47,33,1),(10419000,0x9EF22ABC,34,1),(12630000,0xAE983923,35,.7f)
        };
        var frames=new[]{new List<float[]>(),new List<float[]>(),new List<float[]>()};
        var ended=new List<uint>();
        host.SharedEventEnded+=(cookie,_,_)=>ended.Add(cookie);
        uint clock=0;int next=0;
        for(int tick=0;tick<740;tick++)
        {
            uint frame=(uint)((long)(tick+1)*1024*1000000/48000);
            while(next<schedule.Length && schedule[next].Time<=frame)
            {
                var entry=schedule[next++];
                host.AdvanceClock(entry.Time-clock);clock=entry.Time;
                uint playing=host.PostShared(entry.Event,entry.Cookie);
                Assert.NotEqual(0u,playing);
                host.SetEventVolume(playing,entry.Volume);
                host.Render(0);
            }
            host.Step(frame-clock,true);clock=frame;
            frames[frame<3000000?0:frame<9000000?1:2].AddRange(host.TakeFrames());
        }
        Assert.Equal(new uint[]{1,3,2,4,17,18,33,34,35},ended);
        Assert.Equal(new[]{46,10,23},frames.Select(x=>x.Count));
        Assert.Equal("d9960c03baf1203aaf3bb44342497e40963fac2fc5b1bb27fd4a8eeee6081ccf",Hash(frames[0]));
        Assert.Equal("2f26907421845d90d9f15773f89052f49fe3138ef96abbebfc8b11e717c09948",Hash(frames[1]));
        Assert.Equal("d2fdc2e70b6b0399c01da54da1c0c2862ccec37d21e2cae362a321a47c0e6a93",Hash(frames[2]));
        host.Dispose();
        Assert.True(host.TerminatedCleanly,host.ShutdownDiagnostic);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SharedArchivedEventsMatchIndependentTwoClipNativeReference(bool stopFirst)
    {
        string? lib = ShippedLibraries.FindLibDir();
        if (lib is null) return;
        string meta = GraphRig.FindMeta() ?? throw new InvalidOperationException("shipped bank metadata missing");
        string archive = Path.Combine(WwiseAssets.SoundDir ?? throw new InvalidOperationException("audio archive missing"), "AudioAssets.zip");
        using var host = new NativeWwiseVoiceHost(lib, GraphRig.ReadBank(meta, "Init.bnk"),
            GraphRig.ReadBank(meta, "English(US)/Cozmo.bnk"), 1024, archive, 1700000000);
        float scale = BitConverter.UInt32BitsToSingle(0x38000100);
        float[] samples = AcapelaTtsTests.ProbeSamples().Select(x => x * scale).ToArray();
        host.Offer(samples, 22050);
        host.ClearPending();
        var ends = new List<uint>();
        var endTimes = new List<uint>();
        var lifecycle = new List<uint>();
        host.SharedEventEnded += (cookie, _, _) => { ends.Add(cookie); endTimes.Add(host.ClockMicroseconds); };
        host.HijackOpened += () => lifecycle.Add(host.ClockMicroseconds);
        host.HijackClosed += () => lifecycle.Add(host.ClockMicroseconds);
        var first = new List<float[]>();
        var second = new List<float[]>();
        uint[] ids = { 0x74F0B3E1, 0x9FBACE5F, 0x6F1E60CF, 0xBF0BACE1, 0x120C6C68, 0xD43D2A47 };
        uint[] offsets = { 0, 330000, 990000, 1023000, 1432000, 2244000 };
        uint clock = 0;
        int next = 0;
        bool stopped = false;
        for (int tick = 0; tick < 560; tick++)
        {
            uint frame = (uint)((long)(tick + 1) * 1024 * 1000000 / 48000);
            while (next < 12)
            {
                int clip = next / 6, index = next % 6;
                uint at = (uint)(clip * 6000000) + offsets[index];
                if (at > frame) break;
                host.AdvanceClock(at - clock); clock = at;
                if (index == 0)
                {
                    host.Offer(samples, 22050);
                    host.ConfigureVoice(0x2F8B3BF4, 0.1f);
                }
                uint playing = host.PostShared(ids[index], (uint)(clip * 16 + index + 1));
                Assert.NotEqual(0u, playing);
                host.SetEventVolume(playing, 1);
                host.Render(0);
                next++;
            }
            if (stopFirst && !stopped && frame >= 1100000)
            {
                host.AdvanceClock(1100000 - clock); clock = 1100000;
                host.Stop(); host.Render(0);
                next = 6;
                stopped = true;
            }
            host.Step(frame - clock, true); clock = frame;
            (frame < 6000000 ? first : second).AddRange(host.TakeFrames());
        }
        Assert.Equal(stopFirst ? new uint[] { 1, 2, 4, 3, 17, 18, 19, 21, 22, 20 }
            : new uint[] { 1, 2, 3, 5, 6, 4, 17, 18, 19, 21, 22, 20 }, ends);
        Assert.Equal(stopFirst ? new uint[] { 725333, 1002666, 1109333, 1109333, 6826666, 6997333, 7232000, 8149333, 8490666, 10282666 }
            : new uint[] { 725333, 1002666, 1194666, 2005333, 2538666, 4266666, 6762666, 6997333, 7210666, 7957333, 8512000, 10282666 }, endTimes);
        Assert.Equal(new uint[] { 21333, stopFirst ? 1130666u : 4288000u, 6016000, 10304000 }, lifecycle);
        Assert.Equal(stopFirst ? 33 : 128, first.Count);
        Assert.Equal(129, second.Count);
        Assert.Equal(stopFirst ? "4c6071f9ac6a1002b69142badbfc49f77784f0ca0d5afc0690f37c7528d7ae73"
            : "8a606148ed6b0c36576e18901194f03ca04c7649c71cd0bd629be84c9876641c", Hash(first));
        Assert.Equal(stopFirst ? "0c0242dc9bf6a71236a77c1b5332ca79a11a56b9fa6934968f6ca01d5b0d7f28"
            : "4857a25f655c233eb103142db7e0073e809e366c9dc37faef30a6efcbda0f42e", Hash(second));
        host.AdvanceClock(uint.MaxValue); // shared guest clock wraps after long-lived sessions
        host.AdvanceClock(1);
        host.Dispose();
        Assert.True(host.TerminatedCleanly, host.ShutdownDiagnostic);
    }

    [Fact]
    public void NameSwitchAndPitchMatchIndependentNativeReference()
    {
        string? lib = ShippedLibraries.FindLibDir();
        if (lib is null) return;
        string meta = GraphRig.FindMeta() ?? throw new InvalidOperationException("shipped bank metadata missing");
        using var host = new NativeWwiseVoiceHost(lib, GraphRig.ReadBank(meta, "Init.bnk"),
            GraphRig.ReadBank(meta, "English(US)/Cozmo.bnk"), 1024);
        float scale = BitConverter.UInt32BitsToSingle(0x38000100);
        Assert.Throws<InvalidOperationException>(() => host.ConfigureVoice(0x2F8B3BF4, 0.1f));
        host.Offer(AcapelaTtsTests.ProbeSamples().Select(s => s * scale).ToArray(), 22050);
        host.ConfigureVoice(0x2F8B3BF4, 0.1f);
        Assert.Throws<InvalidOperationException>(() => host.ConfigureVoice(0x2F8B3BF4, 0.1f));
        host.PostOffered(applyDefaultVolume: true);
        for (int i = 0; i < 195; i++) host.Step(21334, consumeOutput: true);
        Assert.Equal(98, host.Frames.Count);
        Assert.Equal("52155a7f13a96f2d372595f01825d6266ce1a35f8f47bf114569f80d90198344", Hash(host.Frames));
        Assert.NotNull(host.EndOfEvent);
        Assert.True(host.HijackEnded);
    }

    [Fact]
    public void CheckedOpenSlSinkMatchesIndependentNativeVoiceReferenceAndTerminates()
    {
        string? lib = ShippedLibraries.FindLibDir();
        if (lib is null) return;
        string meta = GraphRig.FindMeta() ?? throw new InvalidOperationException("shipped bank metadata missing");
        using var host = new NativeWwiseVoiceHost(lib, GraphRig.ReadBank(meta, "Init.bnk"),
            GraphRig.ReadBank(meta, "English(US)/Cozmo.bnk"), 1024);
        float scale = BitConverter.UInt32BitsToSingle(0x38000100);
        float[] samples = AcapelaTtsTests.ProbeSamples().Select(sample => sample * scale).ToArray();
        Assert.Equal(1u, host.Post(samples, 22050));
        int consumed = 0;
        int framesAtEnd = -1;
        for (int tick = 0; tick < 195; tick++)
        {
            byte[]? block = host.Step(21334, consumeOutput: true);
            if (block is not null) { Assert.Equal(4096, block.Length); consumed++; }
            if (framesAtEnd < 0 && host.EndOfEvent is not null) framesAtEnd = host.Frames.Count;
        }
        Assert.Equal(195, consumed);
        Assert.Equal(100, host.Frames.Count);
        Assert.All(host.Frames, frame => Assert.Equal(744, frame.Length));
        Assert.Equal((0xC0FFEEu, 7u, 1u, 0xBF0BACE1u), host.EndOfEvent);
        Assert.InRange(framesAtEnd, 0, 99); // a later Hijack frame follows EndOfEvent
        Assert.True(host.HijackEnded);
        Assert.Equal("6e1ca4d3a3b57cf96505d5d83025f754f51c392e111a1ec17534b34f8c01cfe9", Hash(host.Frames));
        host.Dispose();
        Assert.True(host.TerminatedCleanly);
        Assert.Null(host.ShutdownDiagnostic);
        Assert.Throws<ObjectDisposedException>(() => host.Step(1, false));
    }

    [Fact]
    public void StopCompletesAndAbruptReleaseIsVisible()
    {
        string? lib = ShippedLibraries.FindLibDir();
        if (lib is null) return;
        string meta = GraphRig.FindMeta() ?? throw new InvalidOperationException("shipped bank metadata missing");
        byte[] init = GraphRig.ReadBank(meta, "Init.bnk"), english = GraphRig.ReadBank(meta, "English(US)/Cozmo.bnk");
        float scale = BitConverter.UInt32BitsToSingle(0x38000100);
        float[] samples = AcapelaTtsTests.ProbeSamples().Select(sample => sample * scale).ToArray();
        using (var host = new NativeWwiseVoiceHost(lib, init, english, 1024))
        {
            host.Post(samples, 22050);
            for (int tick = 0; tick < 60; tick++)
            {
                if (tick == 24) host.Stop();
                host.Step(21334, consumeOutput: true);
            }
            Assert.NotNull(host.EndOfEvent);
            Assert.True(host.HijackEnded);
            Assert.Equal(16, host.Frames.Count);
            Assert.Equal("748ec3acaae0a1c3d23543efa27e1fb567cfafd8873bf3ca1fcbfe21e510e652", Hash(host.Frames));
            host.Dispose();
            Assert.True(host.TerminatedCleanly);
        }
        var abrupt = new NativeWwiseVoiceHost(lib, init, english, 1024);
        abrupt.Post(samples, 22050);
        abrupt.Dispose();
        Assert.False(abrupt.TerminatedCleanly);
        Assert.Contains("MISSING", abrupt.ShutdownDiagnostic);
        Assert.Throws<ObjectDisposedException>(() => abrupt.Step(1, false));
    }

    private static string Hash(IReadOnlyList<float[]> frames)
    {
        byte[] bytes = new byte[frames.Sum(frame => frame.Length) * 4];
        int offset = 0;
        foreach (float[] frame in frames)
            foreach (float sample in frame)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, 4), BitConverter.SingleToUInt32Bits(sample));
                offset += 4;
            }
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
