using System.Text.Json;
using Cozmo.Robot.Emulation;

namespace Cozmo.Robot.Animation;

/// <summary>The Android speech provider's language configuration and its Acapela bridge.</summary>
internal sealed class SpeechProvider : IDisposable
{
    private readonly AcapelaTts _tts;
    private readonly string _cozmo;
    private readonly int _speed;
    private readonly int _shaping;

    internal SpeechProvider(string resources, string libDir, string language)
    {
        string voice = "Ryan", cozmo = "Cozmo";
        int speed = 100, shaping = 100;
        string path = Path.Combine(resources, "config", "engine", "tts_config.json");
        using (var doc = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip }))
        {
            if (doc.RootElement.TryGetProperty("android", out var platform) && platform.TryGetProperty(language, out var profile))
            {
                if (profile.TryGetProperty("voice", out var v)) voice = v.GetString() ?? voice;
                if (profile.TryGetProperty("speed", out var s)) speed = s.GetInt32();
                if (profile.TryGetProperty("shaping", out var sh)) shaping = sh.GetInt32();
                if (profile.TryGetProperty("cozmo", out var c)) cozmo = c.GetString() ?? cozmo;
            }
        }
        _cozmo = cozmo;
        _speed = speed;
        _shaping = shaping;
        _tts = new AcapelaTts(libDir, javaGlue: true, unixTime: () => DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        try { _tts.LoadVoice(Path.Combine(resources, "tts"), voice); }
        catch { _tts.Dispose(); throw; }
    }

    internal static int SpeechRate(int speed, float scalar)
    {
        // GetSpeechRate propagates NaN; the caller's ARM vcvt.s32.f32 then yields zero.
        if (float.IsNaN(scalar)) return 0;
        float duration = scalar > 0.05f ? MathF.Min(scalar, 20f) : 0.05f;
        float rate = speed / duration;
        if (!(rate >= 30f)) rate = 30f;
        if (rate > 300f) rate = 300f;
        return (int)rate;
    }

    internal float[] Synthesize(string text, byte style, float scalar)
    {
        if (style == 2) text += "?";
        text = text.Replace("Cozmo", _cozmo, StringComparison.Ordinal);
        short[] pcm = _tts.Synthesize(text, SpeechRate(_speed, scalar), _shaping, 100);
        var samples = new float[pcm.Length];
        for (int i = 0; i < pcm.Length; i++) samples[i] = pcm[i] * 3.0518509e-05f;
        return samples;
    }

    public void Dispose() => _tts.Dispose();
}
