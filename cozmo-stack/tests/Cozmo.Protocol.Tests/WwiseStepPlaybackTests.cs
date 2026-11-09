using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

public class WwiseStepPlaybackTests
{
    [Fact]
    public void PlaybackResolvesMediaIdInsteadOfPluginIdAndSkipsMissingObjects()
    {
        string dir = Path.Combine(Path.GetTempPath(), "wwise-step-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllBytes(Path.Combine(dir, "step.bnk"), Bank());
            File.WriteAllBytes(Path.Combine(dir, "999.wem"),
                new byte[] { 0x25, 0x80, 0, 0, 0xF0, 0x42, 0, 0x90, 60, 100, 0x60, 0x80, 60, 0, 0, 0xFF, 0x2F, 0 });
            using var lib = WwiseSoundLibrary.Load(dir);
            Assert.False(lib.IsStepPlayable(11));
            Assert.True(lib.IsStepPlayable(12));
            Assert.False(lib.IsStepPlayable(99));
            var selection = new WwiseSelection(new WwiseRng(0), true);
            var plan = WwisePlayback.Resolve(lib, 90, new Dictionary<uint, uint>(), selection);
            Assert.Empty(plan.Problems);
            Assert.Equal(12u, Assert.Single(plan.Sounds).SoundId);

            // The ordinary call advanced the shared sequence to item 12. The real MIDI recipient
            // must continue at 13 rather than starting its own cursor at item 11.
            var clip = new WwiseTrackClip(0, 999, 0, 0, 0, 1000);
            var midi = new WwiseMusicClipPlan(1, 999, WwiseMusicTrackNode.MidiPluginId, clip);
            var segment = new WwiseMusicSegmentPlan(1, 1000, default, 120, new[] { midi }) { MidiTargetNodeId = 20 };
            var renderer = new WwiseSongRenderer(lib, _ => new short[] { 100, 100 }, selection);
            var voices = renderer.BuildVoices(new WwiseMusicPlan(1) { Segments = new[] { segment } });
            Assert.Equal(13u, Assert.Single(voices.Voices).SoundId);

            // A nested container is playable even when all of its own children fail. The outer
            // selector must not skip it and choose sound 13 instead.
            var nested = WwisePlayback.Resolve(lib, 91, new Dictionary<uint, uint>(),
                new WwiseSelection(new WwiseRng(0), true));
            Assert.Empty(nested.Sounds);
            Assert.Contains(nested.Problems, problem => problem.Contains("container 21 selected no item"));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    private static byte[] Bank()
    {
        static void U16(List<byte> b, ushort v) => b.AddRange(BitConverter.GetBytes(v));
        static void U32(List<byte> b, uint v) => b.AddRange(BitConverter.GetBytes(v));
        static void Node(List<byte> b)
        {
            b.AddRange(new byte[3]); U32(b, 0); U32(b, 0); b.Add(0); b.Add(0);
            b.AddRange(new byte[3]); b.AddRange(new byte[6]); U32(b, 0); U16(b, 0);
        }
        static byte[] Chunk(string name, byte[] body)
        {
            var b = new List<byte>(System.Text.Encoding.ASCII.GetBytes(name));
            U32(b, (uint)body.Length); b.AddRange(body); return b.ToArray();
        }
        static byte[] Sound(uint id, uint plugin, uint media)
        {
            var b = new List<byte>(); U32(b, id); U32(b, plugin); b.Add(1); U32(b, media);
            U32(b, 0); b.Add(0); Node(b); return b.ToArray();
        }
        static byte[] Sequence(uint id, uint[] children)
        {
            var seq = new List<byte>(); U32(seq, id); Node(seq);
            U16(seq, 0); U16(seq, 0); U16(seq, 0); seq.AddRange(new byte[12]);
            U16(seq, 0); seq.Add(0); seq.Add(0); seq.Add(1); seq.Add(0x10);
            U32(seq, (uint)children.Length); foreach (uint child in children) U32(seq, child);
            U16(seq, (ushort)children.Length);
            foreach (uint child in children) { U32(seq, child); U32(seq, 50000); }
            return seq.ToArray();
        }
        var action = new List<byte>(); U32(action, 80); U16(action, 0x0403); U32(action, 20);
        action.AddRange(new byte[] { 0, 0, 0, 3 }); U32(action, 0);
        var ev = new List<byte>(); U32(ev, 90); U32(ev, 1); U32(ev, 80);
        var nestedAction = new List<byte>(); U32(nestedAction, 81); U16(nestedAction, 0x0403); U32(nestedAction, 22);
        nestedAction.AddRange(new byte[] { 0, 0, 0, 3 }); U32(nestedAction, 0);
        var nestedEvent = new List<byte>(); U32(nestedEvent, 91); U32(nestedEvent, 1); U32(nestedEvent, 81);
        var objects = new (byte Type, byte[] Bytes)[] { (2, Sound(11, 1, 0)), (2, Sound(12, 0, 123)), (2, Sound(13, 1, 456)),
            (5, Sequence(20, new uint[] { 11, 99, 12, 13 })), (5, Sequence(21, new uint[] { 11, 99 })),
            (5, Sequence(22, new uint[] { 21, 13 })), (3, action.ToArray()), (4, ev.ToArray()),
            (3, nestedAction.ToArray()), (4, nestedEvent.ToArray()) };
        var h = new List<byte>(); U32(h, (uint)objects.Length);
        foreach (var item in objects) { h.Add(item.Type); U32(h, (uint)item.Bytes.Length); h.AddRange(item.Bytes); }
        var header = new List<byte>(); U32(header, 120); U32(header, 1); U32(header, 0); U32(header, 0); U32(header, 0);
        return Chunk("BKHD", header.ToArray()).Concat(Chunk("HIRC", h.ToArray())).ToArray();
    }
}
